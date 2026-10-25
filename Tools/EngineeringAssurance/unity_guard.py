#!/usr/bin/env python3
"""Cooperative, fail-closed Unity execution gate for one physical project.

The OS mutex is the exclusion primitive. JSON is diagnostic evidence only.
Unregistered Unity launchers remain an external race and must be integrated.
"""

from __future__ import annotations

import argparse
import ctypes
import hashlib
import html
import json
import ntpath
import os
import re
import subprocess
import sys
import threading
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parent / ".local" / "unity-guard"
_LOCAL_OWNERS: set[str] = set()
_LOCAL_LOCK = threading.Lock()
WAIT_OBJECT_0 = 0
WAIT_ABANDONED = 0x80
WAIT_TIMEOUT = 0x102


def utc() -> str:
    return datetime.now(timezone.utc).isoformat()


def parse_process_time(value: str) -> datetime:
    legacy = re.fullmatch(r"/Date\((-?\d+)(?:[+-]\d{4})?\)/", value)
    if legacy:
        return datetime.fromtimestamp(int(legacy.group(1)) / 1000, timezone.utc)
    parsed = datetime.fromisoformat(value)
    if parsed.tzinfo is None:
        raise ValueError("Process creation time lacks timezone")
    return parsed


def canonical(project: Path) -> str:
    if not project.is_dir() or not (project / "Assets").is_dir() or not (project / "ProjectSettings").is_dir():
        raise ValueError("Project must contain Assets and ProjectSettings")
    return os.path.normcase(str(project.resolve())).casefold()


def project_key(project: Path) -> str:
    return hashlib.sha256(canonical(project).encode("utf-8")).hexdigest()[:24]


def project_paths(project: Path, state_root: Path = ROOT) -> tuple[Path, Path]:
    folder = state_root / project_key(project)
    return folder, folder / "owner.json"


def atomic_json(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + "." + uuid.uuid4().hex + ".tmp")
    try:
        with temporary.open("x", encoding="utf-8") as stream:
            json.dump(data, stream, ensure_ascii=False, indent=2)
            stream.write("\n")
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


class ProjectMutex:
    def __init__(self, project: Path, state_root: Path = ROOT):
        self.key = project_key(project)
        self.name = "Global\\RPG_Demo_UnityProject_" + self.key
        self.folder, _ = project_paths(project, state_root)
        self.handle = None
        self.file = None
        self.acquired = False

    def acquire(self) -> tuple[bool, bool]:
        with _LOCAL_LOCK:
            if self.name in _LOCAL_OWNERS:
                return False, False  # Nested calls must not silently recurse.
            if os.name == "nt":
                kernel = ctypes.WinDLL("kernel32", use_last_error=True)
                kernel.CreateMutexW.argtypes = [ctypes.c_void_p, ctypes.c_bool, ctypes.c_wchar_p]
                kernel.CreateMutexW.restype = ctypes.c_void_p
                kernel.WaitForSingleObject.argtypes = [ctypes.c_void_p, ctypes.c_uint32]
                kernel.WaitForSingleObject.restype = ctypes.c_uint32
                kernel.CloseHandle.argtypes = [ctypes.c_void_p]
                kernel.ReleaseMutex.argtypes = [ctypes.c_void_p]
                kernel.ReleaseMutex.restype = ctypes.c_bool
                self.handle = kernel.CreateMutexW(None, False, self.name)
                if not self.handle:
                    raise OSError(ctypes.get_last_error(), "CreateMutexW failed")
                result = kernel.WaitForSingleObject(self.handle, 0)
                if result == WAIT_TIMEOUT:
                    kernel.CloseHandle(self.handle)
                    self.handle = None
                    return False, False
                if result not in (WAIT_OBJECT_0, WAIT_ABANDONED):
                    kernel.CloseHandle(self.handle)
                    self.handle = None
                    raise OSError(ctypes.get_last_error(), f"WaitForSingleObject returned {result}")
                abandoned = result == WAIT_ABANDONED
            else:  # Allows isolated tests on non-Windows hosts.
                import fcntl
                self.folder.mkdir(parents=True, exist_ok=True)
                self.file = (self.folder / "guard.lock").open("a+b")
                try:
                    fcntl.flock(self.file, fcntl.LOCK_EX | fcntl.LOCK_NB)
                except BlockingIOError:
                    self.file.close()
                    self.file = None
                    return False, False
                abandoned = False
            _LOCAL_OWNERS.add(self.name)
            self.acquired = True
            return True, abandoned

    def release(self) -> None:
        with _LOCAL_LOCK:
            if not self.acquired:
                return
            if os.name == "nt":
                kernel = ctypes.WinDLL("kernel32", use_last_error=True)
                kernel.ReleaseMutex.argtypes = [ctypes.c_void_p]
                kernel.ReleaseMutex.restype = ctypes.c_bool
                kernel.CloseHandle.argtypes = [ctypes.c_void_p]
                if not kernel.ReleaseMutex(self.handle):
                    raise OSError(ctypes.get_last_error(), "ReleaseMutex failed; ownership remains uncertain")
                kernel.CloseHandle(self.handle)
                self.handle = None
            else:
                import fcntl
                fcntl.flock(self.file, fcntl.LOCK_UN)
                self.file.close()
                self.file = None
            _LOCAL_OWNERS.remove(self.name)
            self.acquired = False

    def __enter__(self):
        acquired, abandoned = self.acquire()
        return acquired, abandoned

    def __exit__(self, *_):
        self.release()


class _FileTime(ctypes.Structure):
    _fields_ = [("low", ctypes.c_uint32), ("high", ctypes.c_uint32)]


class _ProcessEntry(ctypes.Structure):
    _fields_ = [("size", ctypes.c_uint32), ("usage", ctypes.c_uint32),
                ("pid", ctypes.c_uint32), ("defaultHeap", ctypes.c_size_t),
                ("module", ctypes.c_uint32), ("threads", ctypes.c_uint32),
                ("parentPid", ctypes.c_uint32), ("priority", ctypes.c_int32),
                ("flags", ctypes.c_uint32), ("exe", ctypes.c_wchar * 260)]


def native_unity_pids() -> list[int]:
    """A third, read-only enumeration independent of the PowerShell views."""
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.CreateToolhelp32Snapshot.argtypes = [ctypes.c_uint32, ctypes.c_uint32]
    kernel.CreateToolhelp32Snapshot.restype = ctypes.c_void_p
    kernel.Process32FirstW.argtypes = [ctypes.c_void_p, ctypes.POINTER(_ProcessEntry)]
    kernel.Process32NextW.argtypes = [ctypes.c_void_p, ctypes.POINTER(_ProcessEntry)]
    kernel.CloseHandle.argtypes = [ctypes.c_void_p]
    snapshot = kernel.CreateToolhelp32Snapshot(0x2, 0)
    if snapshot == ctypes.c_void_p(-1).value:
        raise OSError(ctypes.get_last_error(), "CreateToolhelp32Snapshot failed")
    try:
        entry = _ProcessEntry()
        entry.size = ctypes.sizeof(entry)
        result = kernel.Process32FirstW(snapshot, ctypes.byref(entry))
        if not result and ctypes.get_last_error() != 18:
            raise OSError(ctypes.get_last_error(), "Process32FirstW failed")
        pids = []
        while result:
            if entry.exe.casefold() == "unity.exe":
                pids.append(int(entry.pid))
            result = kernel.Process32NextW(snapshot, ctypes.byref(entry))
        if ctypes.get_last_error() != 18:
            raise OSError(ctypes.get_last_error(), "Process32NextW failed")
        return pids
    finally:
        kernel.CloseHandle(snapshot)


def native_process_probe(pid: int) -> dict:
    """Pin one PID to a Windows process object while checking its lifetime."""
    if os.name != "nt":
        return {"state": "PROCESS_STATE_UNKNOWN", "reason": "Windows required"}
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.OpenProcess.argtypes = [ctypes.c_uint32, ctypes.c_bool, ctypes.c_uint32]
    kernel.OpenProcess.restype = ctypes.c_void_p
    kernel.GetProcessTimes.argtypes = [ctypes.c_void_p] + [ctypes.POINTER(_FileTime)] * 4
    kernel.QueryFullProcessImageNameW.argtypes = [ctypes.c_void_p, ctypes.c_uint32,
                                                  ctypes.c_wchar_p, ctypes.POINTER(ctypes.c_uint32)]
    kernel.WaitForSingleObject.argtypes = [ctypes.c_void_p, ctypes.c_uint32]
    kernel.WaitForSingleObject.restype = ctypes.c_uint32
    kernel.GetExitCodeProcess.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_uint32)]
    kernel.CloseHandle.argtypes = [ctypes.c_void_p]
    attempts = []

    def open_with(rights: int):
        handle = kernel.OpenProcess(rights, False, pid)
        attempts.append({"rights": hex(rights), "error": None if handle else ctypes.get_last_error()})
        return handle

    # Keep a SYNCHRONIZE handle open while obtaining a query handle. This pins
    # the process object, so a recycled PID cannot join two different objects.
    handle = open_with(0x1000 | 0x00100000)
    sync_handle = handle or open_with(0x00100000)
    query_handle = handle or (open_with(0x1000) if sync_handle else None)
    if not sync_handle:
        errors = [item["error"] for item in attempts]
        if errors and all(error == 87 for error in errors):
            return {"state": "PID_NOT_FOUND", "openError": 87, "openAttempts": attempts,
                    "reason": "OpenProcess reports invalid PID"}
        return {"state": "PROCESS_STATE_UNKNOWN", "openError": errors[0],
                "openAttempts": attempts, "reason": f"OpenProcess errors {errors}"}
    try:
        wait = kernel.WaitForSingleObject(sync_handle, 0)
        if wait not in (WAIT_OBJECT_0, WAIT_TIMEOUT):
            raise OSError(ctypes.get_last_error(), f"WaitForSingleObject returned {wait}")
        result = {"state": "EXITED_CONFIRMED" if wait == WAIT_OBJECT_0 else "LIVE_CONFIRMED",
                  "waitResult": wait, "openAttempts": attempts, "createdAtUtc": None,
                  "imagePath": None, "exitCode": None}
        if not query_handle:
            result["reason"] = "Wait succeeded but process query access denied"
            if wait == WAIT_TIMEOUT:
                result["state"] = "LIVE_UNIDENTIFIED"
            return result
        created, exited, kernel_time, user_time = (_FileTime() for _ in range(4))
        if kernel.GetProcessTimes(query_handle, ctypes.byref(created), ctypes.byref(exited),
                                  ctypes.byref(kernel_time), ctypes.byref(user_time)):
            ticks = (created.high << 32) | created.low
            if ticks:
                result["createdAtUtc"] = datetime.fromtimestamp(
                    (ticks - 116444736000000000) / 10000000, timezone.utc).isoformat()
            else:
                result["timeError"] = "zero creation time"
        else:
            result["timeError"] = ctypes.get_last_error()
        exit_code = ctypes.c_uint32()
        if kernel.GetExitCodeProcess(query_handle, ctypes.byref(exit_code)):
            result["exitCode"] = exit_code.value
        else:
            result["exitError"] = ctypes.get_last_error()
        size = ctypes.c_uint32(32768)
        image = ctypes.create_unicode_buffer(size.value)
        if kernel.QueryFullProcessImageNameW(query_handle, 0, image, ctypes.byref(size)):
            result["imagePath"] = image.value
        else:
            result["imageError"] = ctypes.get_last_error()
        if not result["createdAtUtc"]:
            result["reason"] = "GetProcessTimes creation time unavailable"
            if wait == WAIT_TIMEOUT:
                result["state"] = "LIVE_UNIDENTIFIED"
        return result
    except (OSError, ValueError, OverflowError) as error:
        return {"state": "PROCESS_STATE_UNKNOWN", "reason": str(error)}
    finally:
        if query_handle and query_handle != sync_handle:
            kernel.CloseHandle(query_handle)
        kernel.CloseHandle(sync_handle)


def windows_process_views() -> tuple[list[dict], list[dict], list[int], str | None]:
    if os.name != "nt":
        return [], [], [], "Unity process inventory requires Windows"
    script = ("$ErrorActionPreference='Stop'; "
              "$c=@(Get-CimInstance Win32_Process -Filter \"name = 'Unity.exe'\" | "
              "Select-Object ProcessId,CreationDate,CommandLine); "
              "$p=@(Get-Process -Name Unity -ErrorAction SilentlyContinue | "
              "Select-Object Id,StartTime); "
              "@{cim=$c;process=$p} | ConvertTo-Json -Depth 5 -Compress")
    try:
        result = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", script],
                                capture_output=True, text=True, timeout=15)
        if result.returncode != 0:
            return [], [], [], "CIM/process query failed: " + (result.stderr.strip() or str(result.returncode))
        data = json.loads(result.stdout)
        cim = data.get("cim") or []
        proc = data.get("process") or []
        if isinstance(cim, dict):
            cim = [cim]
        if isinstance(proc, dict):
            proc = [proc]
        return cim, proc, native_unity_pids(), None
    except (OSError, ValueError, KeyError, subprocess.TimeoutExpired) as error:
        return [], [], [], "Unity process inventory unavailable: " + str(error)


def _classified_process(pid: int, cim: dict | None, proc: dict | None,
                        native_listed: bool, native_probe) -> dict:
    result = {"ProcessId": pid, "CreationDate": cim.get("CreationDate") if cim else None,
              "CommandLine": cim.get("CommandLine") if cim else None,
              "getProcessStartTime": proc.get("StartTime") if proc else None,
              "views": {"cim": cim is not None, "getProcess": proc is not None,
                        "toolhelp": native_listed}}
    try:
        native = native_probe(pid)
    except (OSError, ValueError, RuntimeError) as error:
        native = {"state": "PROCESS_STATE_UNKNOWN", "reason": f"Native probe failed: {error}"}
    result.update(nativeState=native.get("state", "PROCESS_STATE_UNKNOWN"),
                  nativeCreatedAtUtc=native.get("createdAtUtc"),
                  nativeImagePath=native.get("imagePath"),
                  nativeExitCode=native.get("exitCode"),
                  nativeWaitResult=native.get("waitResult"),
                  nativeError=native.get("reason"), imageError=native.get("imageError"),
                  nativeOpenError=native.get("openError"), nativeOpenAttempts=native.get("openAttempts"),
                  nativeTimeError=native.get("timeError"), nativeExitError=native.get("exitError"))
    if result["nativeState"] == "PID_NOT_FOUND" and not native_listed:
        result["nativeState"] = "NOT_FOUND_CONFIRMED"
        result["identityStatus"] = "HISTORICAL_PID_ABSENT_FROM_OS_SNAPSHOT"
        return result
    if result["nativeState"] == "EXITED_CONFIRMED" and not result["nativeCreatedAtUtc"]:
        result["identityStatus"] = "EXITED_HANDLE_ONLY"
        result["reason"] = "Signaled process object; creation time unavailable"
        return result
    if result["nativeState"] not in ("LIVE_CONFIRMED", "EXITED_CONFIRMED"):
        result["nativeState"] = "PROCESS_STATE_UNKNOWN"
        result["identityStatus"] = "UNVERIFIED"
        result["reason"] = result["nativeError"] or "Native process state unavailable"
        return result
    try:
        native_time = parse_process_time(result["nativeCreatedAtUtc"])
        for view_time in (result["CreationDate"], result["getProcessStartTime"]):
            if view_time and abs((native_time - parse_process_time(view_time)).total_seconds()) > 2:
                raise ValueError("CIM, Get-Process and native creation times disagree")
    except (TypeError, ValueError) as error:
        result["nativeState"] = "PROCESS_STATE_UNKNOWN"
        result["identityStatus"] = "UNVERIFIED"
        result["reason"] = str(error)
        return result
    image = result["nativeImagePath"]
    if image and ntpath.basename(image).casefold() != "unity.exe":
        result["nativeState"] = "PROCESS_STATE_UNKNOWN"
        result["identityStatus"] = "PID_REUSE_OR_IMAGE_CONFLICT"
        result["reason"] = "PID reused by a non-Unity process"
    elif result["nativeState"] == "LIVE_CONFIRMED" and (
            not image or not cim or not proc or not native_listed or not result["CommandLine"]
            or not result["CreationDate"] or not result["getProcessStartTime"]):
        result["nativeState"] = "LIVE_UNIDENTIFIED"
        result["identityStatus"] = "UNVERIFIED"
        result["reason"] = "Live Unity identity or one process view unavailable"
    else:
        result["identityStatus"] = ("EXITED_OBJECT_MATCHED" if result["nativeState"] == "EXITED_CONFIRMED"
                                    else "VERIFIED_UNITY_PROCESS")
    return result


def process_inventory(native_probe=native_process_probe,
                      views_probe=windows_process_views) -> tuple[list[dict], str | None]:
    """Two independent snapshots; native process handles decide each PID's lifetime."""
    if os.name != "nt" and views_probe is windows_process_views:
        return [], "Unity process inventory requires Windows"
    snapshots = []
    for _ in range(2):
        try:
            cim, proc, native_pids, error = views_probe()
        except (OSError, ValueError, RuntimeError, KeyError) as failure:
            return snapshots[-1][1] if snapshots else [], f"Process views failed: {failure}"
        if error:
            return snapshots[-1][1] if snapshots else [], error
        try:
            by_cim = {int(row["ProcessId"]): row for row in cim}
            by_proc = {int(row["Id"]): row for row in proc}
        except (TypeError, ValueError, KeyError) as failure:
            return snapshots[-1][1] if snapshots else [], f"Invalid process view: {failure}"
        pids = set(by_cim) | set(by_proc) | set(native_pids)
        rows = [_classified_process(pid, by_cim.get(pid), by_proc.get(pid),
                                    pid in native_pids, native_probe) for pid in sorted(pids)]
        snapshots.append((pids, rows))
    first_pids, first_rows = snapshots[0]
    second_pids, second_rows = snapshots[1]
    first_by_pid = {row["ProcessId"]: row for row in first_rows}
    second_by_pid = {row["ProcessId"]: row for row in second_rows}
    reasons = []
    if second_pids - first_pids:
        reasons.append("Unity PIDs changed during inventory: new PIDs " + str(sorted(second_pids - first_pids)))
    for pid in first_pids - second_pids:
        second_rows.append(first_by_pid[pid])  # Preserve the first probe in diagnostics.
        if first_by_pid[pid]["nativeState"] not in ("EXITED_CONFIRMED", "NOT_FOUND_CONFIRMED"):
            reasons.append(f"Unity PID {pid} changed during inventory without confirmed exit")
    for pid in first_pids & second_pids:
        a, b = first_by_pid[pid], second_by_pid[pid]
        if a["nativeCreatedAtUtc"] != b["nativeCreatedAtUtc"]:
            reasons.append(f"Unity PID {pid} creation times disagree across snapshots")
        if a["nativeState"] in ("LIVE_UNIDENTIFIED", "PROCESS_STATE_UNKNOWN"):
            reasons.append(f"Unity PID {pid} first probe: {a.get('reason', a['nativeState'])}")
        if a["nativeState"] == "EXITED_CONFIRMED" and b["nativeState"] == "LIVE_CONFIRMED":
            reasons.append(f"Unity PID {pid} changed from exited to live")
        if a["CommandLine"] and b["CommandLine"] and a["CommandLine"] != b["CommandLine"]:
            reasons.append(f"Unity PID {pid} command line changed during inventory")
    for row in second_rows:
        if row["nativeState"] in ("LIVE_UNIDENTIFIED", "PROCESS_STATE_UNKNOWN"):
            reasons.append(f"Unity PID {row['ProcessId']}: {row.get('reason', row['nativeState'])}")
    return second_rows, "; ".join(reasons) if reasons else None


def command_project(command_line: str) -> str | None:
    import re
    match = re.search(r'(?i)(?:^|\s)-projectPath\s+(?:"([^"]+)"|(\S+))', command_line)
    if not match:
        return None
    try:
        return os.path.normcase(str(Path(match.group(1) or match.group(2)).resolve())).casefold()
    except OSError:
        return None


def unity_lock_state(project: Path) -> tuple[str, str | None]:
    lockfile = project / "Temp" / "UnityLockfile"
    if not lockfile.exists():
        return "ABSENT", None
    if os.name != "nt":
        return "UNKNOWN", "Unity lock inspection requires Windows"
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.CreateFileW.argtypes = [ctypes.c_wchar_p, ctypes.c_uint32, ctypes.c_uint32,
                                   ctypes.c_void_p, ctypes.c_uint32, ctypes.c_uint32, ctypes.c_void_p]
    kernel.CreateFileW.restype = ctypes.c_void_p
    kernel.CloseHandle.argtypes = [ctypes.c_void_p]
    handle = kernel.CreateFileW(str(lockfile), 0x80000000, 0, None, 3, 0, None)
    if handle == ctypes.c_void_p(-1).value:
        error = ctypes.get_last_error()
        return ("HELD", None) if error == 32 else ("UNKNOWN", f"UnityLockfile error {error}")
    kernel.CloseHandle(handle)
    return "STALE_FILE", None


def admission(project: Path, inventory=process_inventory, lock_probe=unity_lock_state) -> dict:
    processes, error = inventory()
    lock_state, lock_error = lock_probe(project)
    evidence = {"processes": processes, "processError": error,
                "unityLock": lock_state, "unityLockError": lock_error}
    if error or lock_error or lock_state == "UNKNOWN":
        return {"state": "UNKNOWN", "reason": error or lock_error or "Unity lock state unknown",
                "evidence": evidence}
    same = canonical(project)
    for process in processes:
        lifecycle = process.get("nativeState")
        if lifecycle in ("EXITED_CONFIRMED", "NOT_FOUND_CONFIRMED"):
            continue
        if lifecycle in ("LIVE_UNIDENTIFIED", "PROCESS_STATE_UNKNOWN"):
            return {"state": "UNKNOWN", "evidence": evidence,
                    "reason": f"Unity PID {process['ProcessId']} is {lifecycle}"}
        path = command_project(process.get("CommandLine") or "")
        process["projectPath"] = path
        if path is None:
            return {"state": "UNKNOWN", "reason": f"Unity PID {process['ProcessId']} project path unavailable",
                    "evidence": evidence}
        if path == same:
            return {"state": "BUSY_EXTERNAL", "reason": f"Unity PID {process['ProcessId']} uses this project",
                    "evidence": evidence}
    if lock_state == "HELD":
        return {"state": "BUSY_EXTERNAL", "reason": "UnityLockfile is held", "evidence": evidence}
    return {"state": "FREE", "internalState": "PROJECT_FREE_VERIFIED",
            "reason": "Two process snapshots verified and project lock is not held", "evidence": evidence}


def git_identity(project: Path) -> dict:
    def call(*args):
        result = subprocess.run(["git", "-C", str(project), *args], capture_output=True, text=True)
        return result.stdout.strip() if result.returncode == 0 else None
    return {"branch": call("branch", "--show-current"), "head": call("rev-parse", "HEAD")}


def read_owner(path: Path) -> dict | None:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None


def command_output_paths(command: list[str]) -> dict:
    result = {}
    for index, part in enumerate(command[:-1]):
        if part.casefold() in ("-logfile", "-testresults"):
            result[part.lstrip("-")] = command[index + 1]
    return result


def process_creation_time(pid: int) -> str | None:
    if os.name != "nt":
        return None
    script = (f"$ErrorActionPreference='Stop'; "
              f"(Get-Process -Id {pid} -ErrorAction Stop).StartTime.ToString('o')")
    try:
        result = subprocess.run(["powershell", "-NoProfile", "-NonInteractive", "-Command", script],
                                capture_output=True, text=True, timeout=5)
        value = result.stdout.strip()
        if result.returncode == 0 and value:
            datetime.fromisoformat(value)
            return value
    except (OSError, ValueError, subprocess.TimeoutExpired):
        pass
    return None


def snapshot_identity(project: Path, manifest_path: Path | None) -> dict | None:
    if manifest_path is None:
        return None
    from unity_project_snapshot import verify
    manifest_path = manifest_path.resolve()
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if Path(manifest["project"]).resolve() != project.resolve():
        raise ValueError("Snapshot manifest project differs from Unity project")
    result = verify(manifest_path.parent)
    if result["SNAPSHOT_VERIFICATION"] != "PASS":
        raise ValueError("Unity project snapshot verification blocked")
    return {"projectSnapshotId": manifest["projectSnapshotId"],
            "sourceBranch": manifest["sourceBranch"], "sourceHead": manifest["sourceHead"],
            "sourceFingerprint": manifest["sourceFingerprint"],
            "manifest": str(manifest_path), "manifestSha256": hashlib.sha256(manifest_path.read_bytes()).hexdigest(),
            "verificationReport": result["report"],
            "liveFreshnessAtStart": result["LIVE_WORKTREE_FRESHNESS"]}


def append_event(folder: Path, event: dict) -> None:
    folder.mkdir(parents=True, exist_ok=True)
    with (folder / "events.jsonl").open("a", encoding="utf-8") as stream:
        stream.write(json.dumps(event, ensure_ascii=False) + "\n")


def status(project: Path, state_root: Path = ROOT, inventory=process_inventory,
           lock_probe=unity_lock_state) -> dict:
    folder, owner_file = project_paths(project, state_root)
    mutex = ProjectMutex(project, state_root)
    try:
        acquired, abandoned = mutex.acquire()
        if not acquired:
            return {"state": "OWNED", "owner": read_owner(owner_file),
                    "ownerEvidence": "metadata unverified; OS mutex currently held",
                    "project": canonical(project), "checkedAtUtc": utc()}
        decision = admission(project, inventory, lock_probe)
        return {**decision, "abandonedMutex": abandoned, "owner": read_owner(owner_file),
                "project": canonical(project), "checkedAtUtc": utc()}
    except OSError as error:
        return {"state": "UNKNOWN", "reason": str(error), "project": canonical(project), "checkedAtUtc": utc()}
    finally:
        mutex.release()


def validate_unity_command(project: Path, command: list[str]) -> None:
    if not command or Path(command[0]).name.casefold() != "unity.exe":
        raise ValueError("run accepts a Unity.exe command only")
    for index, part in enumerate(command[:-1]):
        if part.casefold() == "-projectpath" and os.path.normcase(str(Path(command[index + 1]).resolve())).casefold() == canonical(project):
            return
    raise ValueError("Unity command must contain -projectPath for this exact project")


def run(project: Path, task: str, task_name: str, operation: str, command: list[str],
        timeout: float, state_root: Path = ROOT, inventory=process_inventory,
        lock_probe=unity_lock_state, manifest_path: Path | None = None,
        command_validator=None) -> dict:
    """Compatibility command adapter; all process lifecycle belongs to Supervisor."""
    from unity_supervisor import execute, identity
    (command_validator or validate_unity_command)(project, command)
    if not task or not task_name or not operation or timeout <= 0:
        raise ValueError("task, task name, operation and positive timeout are required")
    folder, _ = project_paths(project, state_root)
    run_id = uuid.uuid4().hex
    report_path = folder / "runs" / f"{run_id}.json"
    request = {"schemaVersion": 1, "runId": run_id, "taskId": task, "taskName": task_name,
               "operation": operation, "projectPath": str(project.resolve()), "command": command,
               "timeoutSeconds": timeout, "caller": identity(os.getppid()),
               "stdoutPath": str(report_path.with_suffix(".stdout.log").resolve()),
               "stderrPath": str(report_path.with_suffix(".stderr.log").resolve()),
               "cancelFile": str(report_path.with_suffix(".cancel.request").resolve())}
    if manifest_path:
        request["legacySnapshotManifest"] = str(manifest_path.resolve())
    atomic_json(report_path.with_suffix(".request.json"), request)
    return execute(request, report_path.resolve(), admission=lambda path: admission(path, inventory, lock_probe),
                   command_validator=command_validator or validate_unity_command, state_root=state_root)


def dashboard(project: Path, state_root: Path = ROOT, inventory=process_inventory,
              lock_probe=unity_lock_state) -> Path:
    current = status(project, state_root, inventory, lock_probe)
    folder, _ = project_paths(project, state_root)
    events = []
    try:
        for line in (folder / "events.jsonl").read_text(encoding="utf-8").splitlines()[-30:]:
            events.append(json.loads(line))
    except (OSError, ValueError):
        pass
    esc = lambda value: html.escape(str(value or ""))
    owner = current.get("owner") or {}
    rows = "".join(f"<tr><td>{esc(e.get('time'))}</td><td>{esc(e.get('taskId'))}</td>"
                   f"<td>{esc(e.get('state'))}</td><td>{esc(e.get('reason'))}</td></tr>" for e in reversed(events))
    body = f"""<!doctype html><html lang='zh'><meta charset='utf-8'><title>Unity 项目执行权</title>
<style>body{{background:#101014;color:#f4f2f7;font:15px/1.6 system-ui,'Microsoft YaHei';margin:3rem auto;max-width:1100px;padding:0 2rem}}
h1,b{{color:#d2a7ff}}article{{background:#1b1921;border:1px solid #52415f;border-radius:10px;padding:1.5rem;margin:1rem 0}}
table{{width:100%;border-collapse:collapse}}td,th{{padding:.6rem;border-bottom:1px solid #3e3548;text-align:left}}code{{color:#c8a5ef;overflow-wrap:anywhere}}</style>
<h1>Unity 项目执行权</h1><p>查询时间：{esc(current['checkedAtUtc'])}。此页面是生成时刻的记录，刷新需重新运行 dashboard。</p>
<article><h2>当前状态：{esc(current['state'])}</h2><p>项目：<code>{esc(current['project'])}</code></p>
<p>登记所有者：{esc(owner.get('taskId') or '无 / 未验证')}；操作：{esc(owner.get('operation'))}</p>
<p>限制：登记信息不等于锁证明；未接入门禁的启动器可能绕过合作锁。</p>
<pre>{esc(json.dumps(current.get('evidence') or current.get('reason') or current.get('ownerEvidence'), ensure_ascii=False, indent=2))}</pre></article>
<article><h2>最近记录</h2><table><tr><th>时间</th><th>任务</th><th>状态</th><th>原因</th></tr>{rows}</table></article></html>"""
    path = folder / "dashboard.html"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(body, encoding="utf-8")
    return path


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="action", required=True)
    for name in ("status", "diagnose", "dashboard", "run", "mutex-name", "admit-under-lock"):
        sub = commands.add_parser(name)
        sub.add_argument("--project", type=Path, required=True)
        if name == "run":
            sub.add_argument("--task", required=True)
            sub.add_argument("--task-name", required=True)
            sub.add_argument("--operation", required=True)
            sub.add_argument("--timeout-seconds", type=float, default=1800)
            sub.add_argument("--snapshot-manifest", type=Path)
            sub.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    try:
        if args.action == "dashboard":
            print(dashboard(args.project))
            return 0
        if args.action == "mutex-name":
            print(ProjectMutex(args.project).name)
            return 0
        if args.action == "admit-under-lock":
            # For a caller that already owns the named OS mutex. This command
            # cannot prove which external process owns it, so never treat its
            # output alone as an execution lease.
            probe = ProjectMutex(args.project)
            acquired, _ = probe.acquire()
            if acquired:
                probe.release()
                result = {"state": "UNKNOWN", "reason": "No external project mutex holder"}
                print(json.dumps(result, ensure_ascii=False, indent=2))
                return 2
            result = admission(args.project)
            print(json.dumps(result, ensure_ascii=False, indent=2))
            return 0 if result["state"] == "FREE" else 2
        if args.action in ("status", "diagnose"):
            result = status(args.project)
            print(json.dumps(result, ensure_ascii=False, indent=2))
            return 0 if result["state"] == "FREE" else 2
        command = args.command[1:] if args.command and args.command[0] == "--" else args.command
        result = run(args.project, args.task, args.task_name, args.operation,
                     command, args.timeout_seconds, manifest_path=args.snapshot_manifest)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        from unity_supervisor import successful
        return 0 if successful(result) else 2
    except (ValueError, OSError, RuntimeError, KeyError) as error:
        print(json.dumps({"state": "UNKNOWN", "error": str(error)}, ensure_ascii=False))
        return 2


if __name__ == "__main__":
    sys.exit(main())
