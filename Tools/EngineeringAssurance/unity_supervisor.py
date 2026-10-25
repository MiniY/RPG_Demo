"""Single Windows owned-process lifecycle for Unity; reuses unity_guard admission.

The CLI accepts Unity commands only. Python tests exercise execute at the process
boundary with fixture admission/validation and real harmless Windows processes.
"""
from __future__ import annotations

import argparse
import ctypes as c
from datetime import datetime, timedelta, timezone
import hashlib
import json
import math
import os
from pathlib import Path
import re
import subprocess
import sys
import time

import unity_guard as guard

HEAVY_MUTEX = "Global\\RPG_Demo_UnityHeavy_v1"
DWORD, WORD, HANDLE, SIZE = c.c_uint32, c.c_uint16, c.c_void_p, c.c_size_t
TERMINATED = 0xC000013A
_QUARANTINED_LEASES = []  # Keep unverified-cleanup locks alive until Supervisor teardown.


class FileTime(c.Structure):
    _fields_ = [("low", DWORD), ("high", DWORD)]


class StartupInfo(c.Structure):
    _fields_ = [("cb", DWORD), ("reserved", c.c_wchar_p), ("desktop", c.c_wchar_p),
                ("title", c.c_wchar_p), ("x", DWORD), ("y", DWORD), ("xs", DWORD),
                ("ys", DWORD), ("xc", DWORD), ("yc", DWORD), ("fill", DWORD),
                ("flags", DWORD), ("show", WORD), ("reservedSize", WORD),
                ("reservedBytes", HANDLE), ("stdin", HANDLE), ("stdout", HANDLE), ("stderr", HANDLE)]


class StartupInfoEx(c.Structure):
    _fields_ = [("info", StartupInfo), ("attributes", HANDLE)]


class ProcessInfo(c.Structure):
    _fields_ = [("process", HANDLE), ("thread", HANDLE), ("pid", DWORD), ("tid", DWORD)]


class BasicLimits(c.Structure):
    _fields_ = [("processTime", c.c_int64), ("jobTime", c.c_int64), ("flags", DWORD),
                ("minWorkingSet", SIZE), ("maxWorkingSet", SIZE), ("activeLimit", DWORD),
                ("affinity", SIZE), ("priority", DWORD), ("scheduling", DWORD)]


class ExtendedLimits(c.Structure):
    _fields_ = [("basic", BasicLimits), ("io", c.c_uint64 * 6),
                ("processMemory", SIZE), ("jobMemory", SIZE), ("peakProcessMemory", SIZE),
                ("peakJobMemory", SIZE)]


class Native:
    def __init__(self):
        if os.name != "nt":
            raise OSError("Windows Job Objects required")
        self.k = c.WinDLL("kernel32", use_last_error=True)
        signatures = {
            "CloseHandle": ([HANDLE], c.c_int),
            "OpenProcess": ([DWORD, c.c_int, DWORD], HANDLE),
            "WaitForSingleObject": ([HANDLE, DWORD], DWORD),
            "GetProcessTimes": ([HANDLE] + [c.POINTER(FileTime)] * 4, c.c_int),
            "GetExitCodeProcess": ([HANDLE, c.POINTER(DWORD)], c.c_int),
            "QueryFullProcessImageNameW": ([HANDLE, DWORD, c.c_wchar_p, c.POINTER(DWORD)], c.c_int),
            "CreateJobObjectW": ([HANDLE, c.c_wchar_p], HANDLE),
            "SetInformationJobObject": ([HANDLE, c.c_int, HANDLE, DWORD], c.c_int),
            "QueryInformationJobObject": ([HANDLE, c.c_int, HANDLE, DWORD, c.POINTER(DWORD)], c.c_int),
            "IsProcessInJob": ([HANDLE, HANDLE, c.POINTER(c.c_int)], c.c_int),
            "TerminateJobObject": ([HANDLE, DWORD], c.c_int),
            "TerminateProcess": ([HANDLE, DWORD], c.c_int),
            "ResumeThread": ([HANDLE], DWORD),
            "SetHandleInformation": ([HANDLE, DWORD, DWORD], c.c_int),
            "InitializeProcThreadAttributeList": ([HANDLE, DWORD, DWORD, c.POINTER(SIZE)], c.c_int),
            "UpdateProcThreadAttribute": ([HANDLE, DWORD, SIZE, HANDLE, SIZE, HANDLE, HANDLE], c.c_int),
            "DeleteProcThreadAttributeList": ([HANDLE], None),
            "CreateProcessW": ([c.c_wchar_p, c.c_wchar_p, HANDLE, HANDLE, c.c_int, DWORD,
                                 HANDLE, c.c_wchar_p, HANDLE, c.POINTER(ProcessInfo)], c.c_int),
        }
        for name, (arguments, returned) in signatures.items():
            function = getattr(self.k, name)
            function.argtypes, function.restype = arguments, returned

    def check(self, result, api):
        if not result:
            raise OSError(c.get_last_error(), api + " failed")
        return result

    def close(self, handle):
        if handle:
            self.check(self.k.CloseHandle(handle), "CloseHandle")


class ProcessHandle:
    def __init__(self, native, pid, handle=None):
        self.native, self.pid = native, int(pid)
        self.handle = handle or native.check(native.k.OpenProcess(0x101000, False, self.pid), "OpenProcess")

    def exited(self):
        code = self.native.k.WaitForSingleObject(self.handle, 0)
        if code not in (0, 258):
            raise OSError(c.get_last_error(), f"WaitForSingleObject returned {code}")
        return code == 0

    def describe(self):
        fields = [FileTime() for _ in range(4)]
        self.native.check(self.native.k.GetProcessTimes(self.handle, *[c.byref(x) for x in fields]),
                          "GetProcessTimes")
        ticks = (fields[0].high << 32) | fields[0].low
        created = datetime(1601, 1, 1, tzinfo=timezone.utc) + timedelta(microseconds=ticks // 10)
        path, size = c.create_unicode_buffer(32768), DWORD(32768)
        self.native.check(self.native.k.QueryFullProcessImageNameW(self.handle, 0, path, c.byref(size)),
                          "QueryFullProcessImageNameW")
        return {"pid": self.pid, "createdAtUtc": created.isoformat(), "creationTime100ns": str(ticks),
                "imagePath": path.value, "state": "EXITED_CONFIRMED" if self.exited() else "LIVE_CONFIRMED"}

    def exit_code(self):
        if not self.exited():
            return None
        code = DWORD()
        self.native.check(self.native.k.GetExitCodeProcess(self.handle, c.byref(code)), "GetExitCodeProcess")
        return code.value

    def close(self):
        self.native.close(self.handle)
        self.handle = None


def identity(pid):
    handle = ProcessHandle(Native(), pid)
    try:
        return handle.describe()
    finally:
        handle.close()


class OwnedJob:
    """Own an unnamed, non-inherited Job and only handles created by this launch."""
    def __init__(self, native):
        self.native, self.root, self.assigned, self.forced = native, None, False, False
        self.resumed, self.description = False, None
        self.handle = native.check(native.k.CreateJobObjectW(None, None), "CreateJobObjectW")
        limits = ExtendedLimits()
        limits.basic.flags = 0x2000  # JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE; no breakaway.
        try:
            native.check(native.k.SetInformationJobObject(self.handle, 9, c.byref(limits), c.sizeof(limits)),
                         "SetInformationJobObject(KILL_ON_JOB_CLOSE)")
        except BaseException:
            self.close()
            raise

    def spawn(self, command, project, stdout, stderr, environment):
        import msvcrt
        n = self.native
        info = ProcessInfo()
        attributes = None
        initialized = False
        try:
            # Only the three dedicated standard handles can be inherited. In
            # particular the Job and mutex handles cannot keep themselves alive.
            with open(os.devnull, "rb") as stdin:
                handles = (HANDLE * 3)(*[msvcrt.get_osfhandle(stream.fileno()) for stream in (stdin, stdout, stderr)])
                try:
                    for handle in handles:
                        n.check(n.k.SetHandleInformation(handle, 1, 1), "SetHandleInformation")
                    size = SIZE()
                    n.k.InitializeProcThreadAttributeList(None, 2, 0, c.byref(size))
                    attributes = c.create_string_buffer(size.value)
                    n.check(n.k.InitializeProcThreadAttributeList(attributes, 2, 0, c.byref(size)),
                            "InitializeProcThreadAttributeList")
                    initialized = True
                    n.check(n.k.UpdateProcThreadAttribute(attributes, 0, 0x20002, c.byref(handles),
                                                         c.sizeof(handles), None, None), "UpdateProcThreadAttribute")
                    jobs = (HANDLE * 1)(self.handle)
                    # Assign at creation, not in a later userspace call. Even
                    # death immediately after CreateProcess cannot orphan a
                    # suspended process outside the KILL_ON_CLOSE Job.
                    n.check(n.k.UpdateProcThreadAttribute(attributes, 0, 0x2000D, c.byref(jobs),
                                                         c.sizeof(jobs), None, None),
                            "UpdateProcThreadAttribute(JOB_LIST)")
                    startup = StartupInfoEx()
                    startup.info.cb, startup.info.flags = c.sizeof(startup), 0x100
                    startup.info.stdin, startup.info.stdout, startup.info.stderr = handles
                    startup.attributes = c.cast(attributes, HANDLE)
                    merged = {**os.environ, **environment}
                    env = c.create_unicode_buffer("\0".join(f"{key}={value}" for key, value in
                                                            sorted(merged.items(), key=lambda x: x[0].upper())) + "\0\0")
                    line = c.create_unicode_buffer(subprocess.list2cmdline(command))
                    n.check(n.k.CreateProcessW(command[0], line, None, None, True,
                                               0x4 | 0x400 | 0x80000 | 0x08000000,
                                               env, str(project), c.byref(startup), c.byref(info)), "CreateProcessW")
                    self.root = ProcessHandle(n, info.pid, info.process)
                    self.assigned = True
                    belongs = c.c_int()
                    n.check(n.k.IsProcessInJob(info.process, self.handle, c.byref(belongs)), "IsProcessInJob")
                    if not belongs.value:
                        self.assigned = False
                        raise OSError("Created process is not in the owned Job")
                    self.description = self.root.describe()
                    if n.k.ResumeThread(info.thread) == 0xFFFFFFFF:
                        raise OSError(c.get_last_error(), "ResumeThread failed")
                    self.resumed = True
                    return self.description
                finally:
                    for handle in handles:
                        n.check(n.k.SetHandleInformation(handle, 1, 0), "SetHandleInformation(noninherit)")
        except BaseException:
            if self.root:
                self.terminate()
                waited = n.k.WaitForSingleObject(self.root.handle, 5000)
                if waited != 0:
                    raise OSError(c.get_last_error(), f"Owned spawn-failure cleanup wait returned {waited}")
            raise
        finally:
            if info.thread:
                n.close(info.thread)
            if initialized:
                n.k.DeleteProcThreadAttributeList(attributes)

    def members(self):
        capacity = 64
        while capacity <= 65536:
            buffer = c.create_string_buffer(8 + c.sizeof(SIZE) * capacity)
            returned = DWORD()
            if self.native.k.QueryInformationJobObject(self.handle, 3, buffer, len(buffer), c.byref(returned)):
                count = DWORD.from_buffer(buffer, 4).value
                return list((SIZE * count).from_buffer(buffer, 8))
            error = c.get_last_error()
            if error != 234:
                raise OSError(error, "QueryInformationJobObject(process list) failed")
            capacity *= 2
        raise OSError("Job process list exceeded bounded query capacity")

    def terminate(self):
        self.forced = True
        if self.assigned:
            self.native.check(self.native.k.TerminateJobObject(self.handle, TERMINATED), "TerminateJobObject")
        elif self.root and not self.root.exited():
            # Membership failed: this is our still-suspended root, held by its
            # original process handle. No PID lookup or foreign process kill.
            self.native.check(self.native.k.TerminateProcess(self.root.handle, TERMINATED), "TerminateProcess(owned suspended)")

    def drain(self, timeout):
        deadline = time.monotonic() + timeout
        while True:
            pids = self.members()
            if not pids and (not self.root or self.root.exited()):
                return []
            if time.monotonic() >= deadline:
                return pids or [self.root.pid]
            time.sleep(.03)

    def close(self):
        self.native.close(self.handle)
        self.handle = None


class HeavyMutex(guard.ProjectMutex):
    def __init__(self, project):
        super().__init__(project)
        self.name, self.folder = HEAVY_MUTEX, guard.ROOT / "heavy"


def absolute(value, label):
    path = Path(value)
    if not path.is_absolute():
        raise ValueError(label + " must be an absolute path")
    return path.resolve()


def finite_limit(value, lower, upper, label):
    number = float(value)
    if not math.isfinite(number) or not lower <= number <= upper:
        raise ValueError(label + " is outside its bounded range")
    return number


def validate_request(request, result_path, command_validator):
    if request.get("schemaVersion") != 1:
        raise ValueError("Supervisor request schemaVersion must be 1")
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]{0,100}", request["runId"]):
        raise ValueError("Invalid runId")
    for key in ("taskId", "taskName", "operation"):
        if not isinstance(request.get(key), str) or not request[key].strip():
            raise ValueError(key + " is required")
    project = absolute(request["projectPath"], "projectPath")
    guard.canonical(project)
    command = request["command"]
    if not isinstance(command, list) or not command or any(not isinstance(x, str) or "\0" in x for x in command):
        raise ValueError("command must contain strings without NUL")
    absolute(command[0], "executable")
    if not Path(command[0]).is_file():
        raise ValueError("Executable is missing")
    command_validator(project, command)
    finite_limit(request["timeoutSeconds"], .05, 86400, "timeoutSeconds")
    finite_limit(request.get("cleanupTimeoutSeconds", 30), 1, 120, "cleanupTimeoutSeconds")
    finite_limit(request.get("treeExitGraceSeconds", 2), 0, 30, "treeExitGraceSeconds")
    paths = [absolute(request[key], key) for key in ("stdoutPath", "stderrPath", "cancelFile")]
    paths += [absolute(str(result_path), "resultPath"), Path(str(result_path) + ".events.jsonl")]
    if len(set(paths)) != len(paths):
        raise ValueError("Output and cancel paths must be distinct")
    caller = request["caller"]
    if not isinstance(caller["pid"], int) or caller["pid"] <= 0:
        raise ValueError("caller.pid must be a positive integer")
    guard.parse_process_time(caller["createdAtUtc"])
    environment = request.get("environment", {})
    if not isinstance(environment, dict) or any(not isinstance(k, str) or not isinstance(v, str) or not k
                                               or "=" in k or "\0" in k + v for k, v in environment.items()):
        raise ValueError("environment must be a string map without NUL or '=' in keys")
    return project


def bind_snapshot(evidence):
    if not evidence:
        return None
    path = absolute(evidence["manifestPath"], "snapshotEvidence.manifestPath")
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    if digest != evidence["sha256"] or not evidence.get("snapshotId"):
        raise ValueError("Snapshot manifest hash or ID differs")
    return {**evidence, "binding": "MANIFEST_HASH_BOUND_ONLY"}


def execute(request, result_path, *, admission=guard.admission, command_validator=guard.validate_unity_command,
            state_root=guard.ROOT, job_factory=OwnedJob):
    """Public execution seam. CLI uses real Guard; tests replace only admission/command policy."""
    result_path = Path(result_path).resolve()
    project = validate_request(request, result_path, command_validator)
    result = {"schemaVersion": 1, **{key: request[key] for key in ("runId", "taskId", "taskName", "operation", "command")},
              "projectPath": str(project), "project": guard.canonical(project),
              "requestSha256": hashlib.sha256(json.dumps(request, sort_keys=True).encode()).hexdigest(),
              "state": "RUNNING", "reason": None, "launched": False, "exitCode": None, "naturalExit": False,
              "process": None, "caller": None, "evidenceErrors": [], "snapshotEvidenceStatus": "NOT_REQUIRED",
              "cleanup": {"status": "NOT_REQUIRED", "forced": False,
              "residualPids": [], "errors": [], "reason": None}, "locks": {}, "startedAtUtc": guard.utc(),
              "stdoutPath": request["stdoutPath"], "stderrPath": request["stderrPath"], "resultPath": str(result_path),
              "eventsPath": str(result_path) + ".events.jsonl"}
    result_path.parent.mkdir(parents=True, exist_ok=True)
    with result_path.open("x", encoding="utf-8") as output:
        json.dump(result, output)
    native, caller, job = None, None, None
    heavy, mutex = HeavyMutex(project), guard.ProjectMutex(project, state_root)
    folder, owner_path = guard.project_paths(project, state_root)
    acquired = []
    events, stdout, stderr = None, None, None

    def event(kind, **fields):
        if events:
            try:
                events.write(json.dumps({"atUtc": guard.utc(), "event": kind, **fields}, ensure_ascii=False) + "\n")
                events.flush()
            except OSError as error:
                result["evidenceErrors"].append(str(error))

    def control():
        if caller.exited():
            return "CALLER_EXITED"
        if Path(request["cancelFile"]).exists():
            return "CANCELLED"
        return None

    try:
        events = Path(result["eventsPath"]).open("x", encoding="utf-8")
        native = Native()
        caller = ProcessHandle(native, request["caller"]["pid"])
        result["caller"] = caller.describe()
        if (abs((guard.parse_process_time(result["caller"]["createdAtUtc"]) -
                 guard.parse_process_time(request["caller"]["createdAtUtc"])).total_seconds()) > .000001):
            raise ValueError("Caller PID creation time differs; possible PID reuse")
        if caller.exited():
            result.update(state="BLOCKED", reason="Caller already exited")
            return result
        result["snapshotEvidence"] = bind_snapshot(request.get("snapshotEvidence"))
        if result["snapshotEvidence"]:
            result["snapshotEvidenceStatus"] = "PASS"
        for label, lease in (("heavy", heavy), ("project", mutex)):
            result["locks"][label] = {"name": lease.name, "acquired": False, "released": False}
            held, abandoned = lease.acquire()
            if not held:
                result.update(state="BLOCKED", reason=label.upper() + "_MUTEX_OWNED")
                return result
            acquired.append(lease)
            result["locks"][label].update(acquired=True, abandoned=abandoned)
        if request.get("legacySnapshotManifest"):
            result["sourceSnapshot"] = guard.snapshot_identity(project, Path(request["legacySnapshotManifest"]))
        result["admission"] = admission(project)
        event("INITIAL_ADMISSION", decision=result["admission"])
        if result["admission"]["state"] != "FREE":
            result.update(state="BLOCKED", reason="Initial admission is " + result["admission"]["state"])
            return result
        # Open logs and configure Job before the final safety check, leaving as
        # little work as possible between that check and process creation.
        for key in ("stdoutPath", "stderrPath"):
            Path(request[key]).parent.mkdir(parents=True, exist_ok=True)
        stdout = Path(request["stdoutPath"]).open("xb", buffering=0)
        stderr = Path(request["stderrPath"]).open("xb", buffering=0)
        job = job_factory(native)
        guard.atomic_json(owner_path, {**result, "state": "OWNED"})
        guard.append_event(folder, {"time": guard.utc(), "runId": request["runId"], "taskId": request["taskId"],
                                    "state": "OWNED", "report": str(result_path)})
        result["finalAdmission"] = admission(project)
        event("FINAL_ADMISSION", decision=result["finalAdmission"])
        stop = control()
        if stop or result["finalAdmission"]["state"] != "FREE":
            result.update(state="BLOCKED", reason=stop or "Final admission is " + result["finalAdmission"]["state"])
            return result
        bind_snapshot(request.get("snapshotEvidence"))
        result["process"] = job.spawn(request["command"], project, stdout, stderr, request.get("environment", {}))
        result["launched"] = True
        event("LAUNCHED", process=result["process"], jobKillOnClose=True)
        guard.atomic_json(result_path, result)
        deadline = time.monotonic() + float(request["timeoutSeconds"])
        root_exit_at = None
        while True:
            root_exited = job.root.exited()
            if root_exited:
                result["naturalExit"] = True
                result["exitCode"] = job.root.exit_code()
                if not job.members():
                    result.update(state="COMPLETED" if result["exitCode"] == 0 else "FAILED",
                                  reason=None if result["exitCode"] == 0 else "NONZERO_ROOT_EXIT")
                    break
                root_exit_at = root_exit_at or time.monotonic()
                if time.monotonic() - root_exit_at >= float(request.get("treeExitGraceSeconds", 2)):
                    result.update(state="FAILED", reason="DESCENDANTS_OUTLIVED_ROOT")
                    break
            stop = control()
            if stop or time.monotonic() >= deadline:
                result.update(state=stop or "TIMEOUT", reason=stop or "PROCESS_DEADLINE_EXCEEDED")
                break
            time.sleep(.05)
    except (OSError, ValueError, RuntimeError, KeyError, TypeError) as error:
        result.update(state="FAILED" if result["launched"] else "BLOCKED", reason=f"{type(error).__name__}: {error}")
        event("ERROR", detail=result["reason"])
    except KeyboardInterrupt:
        result.update(state="INTERRUPTED", reason="SUPERVISOR_INTERRUPTED")
    finally:
        if job:
            try:
                if job.root:
                    result["launched"] = job.resumed
                    result["process"] = job.description
                    try:
                        pending = job.members() or not job.root.exited()
                    except OSError as error:
                        result["cleanup"]["errors"].append(str(error))
                        pending = True  # Query failure must not skip owned cleanup.
                    if pending:
                        event("TERMINATE_OWNED_JOB", reason=result["reason"])
                        job.terminate()
                    residual = job.drain(float(request.get("cleanupTimeoutSeconds", 30)))
                    result["exitCode"] = job.root.exit_code()
                    result["cleanup"].update(status="FAIL" if residual else "PASS", forced=job.forced,
                                             residualPids=residual,
                                             rootWaitResult=0 if job.root.exited() else 258,
                                             verifiedAtUtc=guard.utc(),
                                             reason="OWNED_PROCESSES_REMAIN" if residual else None)
                    event("CLEANUP", **result["cleanup"])
            except (OSError, ValueError, RuntimeError, KeyError) as error:
                result["cleanup"].update(status="UNKNOWN", forced=job.forced, reason=str(error))
                result["cleanup"]["errors"].append(str(error))
            finally:
                try:
                    job.close()  # Last-handle kernel fallback, including unhandled death.
                except OSError as error:
                    result["cleanup"].update(status="UNKNOWN", reason=str(error))
                finally:
                    if job.root:
                        try:
                            job.root.close()
                        except OSError as error:
                            result["cleanup"].update(status="UNKNOWN", reason=str(error))
        # These are independent evidence dimensions. A failed inventory or
        # snapshot binding cannot erase an already confirmed empty owned Job.
        if job and job.root:
            try:
                result["postExitAdmission"] = admission(project)
            except (OSError, ValueError, RuntimeError, KeyError) as error:
                result["postExitAdmission"] = {"state": "UNKNOWN", "reason": str(error)}
            if result["postExitAdmission"]["state"] != "FREE" and result["state"] == "COMPLETED":
                result.update(state="FAILED", reason="Post-exit admission is " + result["postExitAdmission"]["state"])
            try:
                if result.get("snapshotEvidence"):
                    bind_snapshot(result["snapshotEvidence"])
                if result.get("sourceSnapshot"):
                    result["snapshotAfterExit"] = guard.snapshot_identity(project, Path(request["legacySnapshotManifest"]))
                    if result["snapshotAfterExit"]["manifestSha256"] != result["sourceSnapshot"]["manifestSha256"]:
                        raise ValueError("Snapshot manifest changed during execution")
            except (OSError, ValueError, RuntimeError, KeyError) as error:
                result["snapshotEvidenceStatus"] = "FAIL"
                result.update(state="FAILED", reason=str(error))
        if result["cleanup"]["status"] in ("FAIL", "UNKNOWN") or result["state"] == "RUNNING":
            result.update(state="FAILED", reason=result["reason"] or "CLEANUP_NOT_CONFIRMED")
        result["unityPid"] = result["process"]["pid"] if result["process"] else None
        result["unityCreatedAtUtc"] = result["process"]["createdAtUtc"] if result["process"] else None
        result["report"] = str(result_path)
        if mutex in acquired:
            try:
                guard.atomic_json(owner_path, result)
                guard.append_event(folder, {"time": guard.utc(), "runId": request["runId"], "taskId": request["taskId"],
                                            "state": result["state"], "reason": result["reason"], "report": str(result_path)})
            except OSError as error:
                result["evidenceErrors"].append(str(error))
        for lease in reversed(acquired):
            label = "heavy" if lease is heavy else "project"
            if job and job.root and result["cleanup"]["status"] in ("FAIL", "UNKNOWN"):
                # A failed process query is not a release certificate. The CLI
                # main thread exits with failure after closing its Job; OS
                # teardown abandons these mutexes. Embedded callers remain
                # fail-closed for the lifetime of that owning thread.
                _QUARANTINED_LEASES.append(lease)
                result["locks"][label]["quarantined"] = True
                continue
            try:
                lease.release()
                result["locks"][label]["released"] = True
            except OSError as error:
                result.update(state="FAILED", reason="Mutex release failed: " + str(error))
        if caller:
            try:
                caller.close()
            except OSError as error:
                result["evidenceErrors"].append(str(error))
        for output in (stdout, stderr):
            if output:
                try:
                    output.close()
                except OSError as error:
                    result["evidenceErrors"].append(str(error))
        result["endedAtUtc"] = guard.utc()
        event("FINAL", state=result["state"], cleanup=result["cleanup"])
        if events:
            try:
                events.close()
            except OSError as error:
                result["evidenceErrors"].append(str(error))
        if result["evidenceErrors"]:
            result.update(state="FAILED", reason=result["reason"] or "EVIDENCE_WRITE_FAILED")
        guard.atomic_json(result_path, result)
    return result


def successful(result):
    return (result.get("state") == "COMPLETED" and result.get("exitCode") == 0 and result.get("naturalExit") is True
            and result.get("cleanup", {}).get("status") == "PASS")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    actions = parser.add_subparsers(dest="action", required=True)
    who = actions.add_parser("identity")
    who.add_argument("--pid", type=int, required=True)
    run = actions.add_parser("run")
    run.add_argument("--request", type=Path, required=True)
    run.add_argument("--result", type=Path, required=True)
    args = parser.parse_args()
    try:
        if args.action == "identity":
            print(json.dumps(identity(args.pid), ensure_ascii=False))
            return 0
        request_path = absolute(str(args.request), "request")
        result_path = absolute(str(args.result), "result")
        if request_path == result_path:
            raise ValueError("Request and result must differ")
        request = json.loads(request_path.read_text(encoding="utf-8-sig"))
        result = execute(request, result_path)
        print(json.dumps(result, ensure_ascii=False))
        return 0 if successful(result) else 2
    except (OSError, ValueError, RuntimeError, KeyError, TypeError) as error:
        print(json.dumps({"schemaVersion": 1, "state": "BLOCKED", "launched": False, "reason": str(error)}))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
