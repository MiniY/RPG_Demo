"""No Unity process is started by this suite."""

from __future__ import annotations

import json
import ctypes
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

import unity_guard as guard


class UnityGuardTests(unittest.TestCase):
    def setUp(self):
        fixture_root = Path(__file__).parent / ".test-fixtures"
        fixture_root.mkdir(exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=fixture_root)
        self.addCleanup(self.temp.cleanup)
        self.project = Path(self.temp.name) / "Project"
        (self.project / "Assets").mkdir(parents=True)
        (self.project / "ProjectSettings").mkdir()
        self.state = Path(self.temp.name) / "state"
        self.command = ["Unity.exe", "-batchmode", "-projectPath", str(self.project)]
        self.probe_command = [sys.executable, "-c", "pass"]
        self.free_inventory = lambda: ([], None)
        self.free_lock = lambda _: ("ABSENT", None)

    def run_probe(self, code=0, timeout=False, inventory=None, lock_probe=None):
        # Real harmless process lifecycle; only Unity occupancy is a fixture.
        command = [sys.executable, "-c", "import time; time.sleep(5)" if timeout else f"raise SystemExit({code})"]
        return guard.run(self.project, "task-a", "non-Unity process probe", "NON_UNITY_TEST", command,
                         .1 if timeout else 2, self.state, inventory or self.free_inventory,
                         lock_probe or self.free_lock, command_validator=lambda *_: None)

    def test_canonical_alias_has_same_identity(self):
        self.assertEqual(guard.project_key(self.project), guard.project_key(self.project / ".." / "Project"))

    def test_competing_tasks_and_nested_acquisition(self):
        first = guard.ProjectMutex(self.project, self.state)
        self.assertEqual(first.acquire(), (True, False))
        try:
            second = guard.ProjectMutex(self.project, self.state)
            self.assertEqual(second.acquire(), (False, False))
            self.assertEqual(guard.status(self.project, self.state)["state"], "OWNED")
            child = subprocess.run([sys.executable, "-c",
                                    "import sys; sys.path.insert(0,sys.argv[1]); import unity_guard as g; "
                                    "m=g.ProjectMutex(__import__('pathlib').Path(sys.argv[2]), "
                                    "__import__('pathlib').Path(sys.argv[3])); print(m.acquire()[0])",
                                    str(Path(guard.__file__).parent), str(self.project), str(self.state)],
                                   text=True, capture_output=True, timeout=10)
            self.assertEqual((child.returncode, child.stdout.strip()), (0, "False"))
        finally:
            first.release()
        reopened = guard.ProjectMutex(self.project, self.state)
        self.assertEqual(reopened.acquire(), (True, False))
        reopened.release()

    @unittest.skipUnless(os.name == "nt", "Windows named mutex")
    def test_powershell_datatest_style_holder_competes_for_same_mutex(self):
        name = guard.ProjectMutex(self.project, self.state).name
        script = ("$m=[Threading.Mutex]::new($false,$env:UNITY_GUARD_MUTEX_TEST_NAME); "
                  "$held=$m.WaitOne(0); [Console]::WriteLine($held); "
                  "[Console]::ReadLine() | Out-Null; "
                  "if($held){$m.ReleaseMutex()}; $m.Dispose()")
        environment = {**os.environ, "UNITY_GUARD_MUTEX_TEST_NAME": name}
        holder = subprocess.Popen(["powershell", "-NoProfile", "-NonInteractive", "-Command",
                                   script], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                  stderr=subprocess.PIPE, text=True, env=environment)
        try:
            self.assertEqual(holder.stdout.readline().strip().lower(), "true")
            observed = guard.status(self.project, self.state, self.free_inventory, self.free_lock)
            self.assertEqual(observed["state"], "OWNED")
            checked = subprocess.run([sys.executable, str(Path(guard.__file__)),
                                      "admit-under-lock", "--project", str(self.project)],
                                     capture_output=True, text=True, timeout=20)
            self.assertIn(checked.returncode, (0, 2))
            self.assertNotEqual(json.loads(checked.stdout).get("reason"),
                                "No external project mutex holder")
        finally:
            holder.communicate(input="\n", timeout=10)
        self.assertEqual(holder.returncode, 0)
        self.assertEqual(guard.status(self.project, self.state,
                                      self.free_inventory, self.free_lock)["state"], "FREE")

    def test_internal_admission_command_requires_an_external_mutex_holder(self):
        result = subprocess.run([sys.executable, str(Path(guard.__file__)),
                                 "admit-under-lock", "--project", str(self.project)],
                                capture_output=True, text=True, timeout=10)
        self.assertEqual(result.returncode, 2)
        self.assertEqual(json.loads(result.stdout)["reason"], "No external project mutex holder")

    def test_normal_exit_and_nonzero_exit_leave_reports(self):
        result = self.run_probe()
        self.assertEqual(result["state"], "COMPLETED")
        self.assertEqual(json.loads(Path(result["report"]).read_text())["state"], "COMPLETED")
        failure = self.run_probe(code=7)
        self.assertEqual((failure["state"], failure["exitCode"]), ("FAILED", 7))

    def test_timeout_reclaims_owned_process_and_external_occupancy_blocks(self):
        result = self.run_probe(timeout=True,
                               inventory=lambda: ([{"ProcessId": 91234,
                                                     "CreationDate": "now",
                                                     "CommandLine": 'Unity.exe -projectPath "' + str(self.project) + '"'}], None))
        self.assertEqual(result["state"], "BLOCKED")  # Existing process prevents launch.
        timed = self.run_probe(timeout=True)
        self.assertEqual((timed["state"], timed["cleanup"]["status"], timed["cleanup"]["forced"]),
                         ("TIMEOUT", "PASS", True))

    def test_unknown_external_process_and_unreadable_lock_block(self):
        unknown = self.run_probe(inventory=lambda: ([], "CIM unavailable"))
        self.assertEqual(unknown["admission"]["state"], "UNKNOWN")
        self.assertEqual(unknown["state"], "BLOCKED")
        held = self.run_probe(lock_probe=lambda _: ("HELD", None))
        self.assertEqual(held["admission"]["state"], "BUSY_EXTERNAL")
        ambiguous = self.run_probe(inventory=lambda: ([{"ProcessId": 1,
                                                       "CreationDate": "now",
                                                       "CommandLine": "Unity.exe"}], None))
        self.assertEqual(ambiguous["admission"]["state"], "UNKNOWN")

    def test_corrupt_metadata_is_never_authority(self):
        folder, owner = guard.project_paths(self.project, self.state)
        owner.parent.mkdir(parents=True)
        owner.write_text("{broken", encoding="utf-8")
        self.assertEqual(guard.status(self.project, self.state,
                                      self.free_inventory, self.free_lock)["state"], "FREE")

    def test_wrong_project_command_refused(self):
        with self.assertRaises(ValueError):
            guard.validate_unity_command(self.project, ["Unity.exe", "-projectPath", str(self.state)])
        with self.assertRaises(ValueError):
            guard.validate_unity_command(self.project, ["python.exe", "-projectPath", str(self.project)])

    def test_launcher_crash_releases_os_lock_but_residual_blocks(self):
        retained = None
        if os.name == "nt":
            kernel = ctypes.WinDLL("kernel32", use_last_error=True)
            kernel.CreateMutexW.argtypes = [ctypes.c_void_p, ctypes.c_bool, ctypes.c_wchar_p]
            kernel.CreateMutexW.restype = ctypes.c_void_p
            kernel.CloseHandle.argtypes = [ctypes.c_void_p]
            retained = kernel.CreateMutexW(None, False, guard.ProjectMutex(self.project, self.state).name)
            self.assertTrue(retained)
            self.addCleanup(kernel.CloseHandle, retained)
        child = subprocess.run([sys.executable, "-c",
                                "import os,sys; sys.path.insert(0,sys.argv[1]); "
                                "import unity_guard as g; from pathlib import Path; "
                                "m=g.ProjectMutex(Path(sys.argv[2]),Path(sys.argv[3])); "
                                "assert m.acquire()[0]; os._exit(7)",
                                str(Path(guard.__file__).parent), str(self.project), str(self.state)],
                               timeout=10)
        self.assertEqual(child.returncode, 7)
        mutex = guard.ProjectMutex(self.project, self.state)
        acquired, abandoned = mutex.acquire()
        self.assertTrue(acquired)
        if os.name == "nt":
            self.assertTrue(abandoned)
        mutex.release()
        result = self.run_probe(inventory=lambda: ([{"ProcessId": 91234,
                                                   "CreationDate": "now",
                                                   "CommandLine": 'Unity.exe -projectPath "' + str(self.project) + '"'}], None))
        self.assertEqual(result["admission"]["state"], "BUSY_EXTERNAL")

    def test_pid_reuse_or_two_views_disagree_is_unknown(self):
        payload = {"cim": [{"ProcessId": 12, "CreationDate": "2026-10-15T10:00:00+00:00",
                            "CommandLine": 'Unity.exe -projectPath "' + str(self.project) + '"'}],
                   "process": [{"Id": 12, "StartTime": "2026-10-15T10:10:00+00:00"}]}
        views = lambda: (payload["cim"], payload["process"], [12], None)
        native = lambda _pid: {"state": "LIVE_CONFIRMED", "createdAtUtc": "2026-10-15T10:00:00+00:00",
                               "imagePath": r"C:\Unity\Unity.exe", "exitCode": 259}
        processes, error = guard.process_inventory(native_probe=native, views_probe=views)
        self.assertEqual(len(processes), 1)
        self.assertIn("creation times disagree", error)

    @unittest.skipUnless(os.name == "nt", "Windows process handles")
    def test_real_kernel_handle_distinguishes_live_and_retained_exit(self):
        live = guard.native_process_probe(os.getpid())
        self.assertEqual(live["state"], "LIVE_CONFIRMED", live)
        child = subprocess.Popen([sys.executable, "-c", "pass"])
        child.wait(timeout=10)  # Popen retains the Windows handle after exit.
        exited = guard.native_process_probe(child.pid)
        self.assertEqual(exited["state"], "EXITED_CONFIRMED", exited)
        self.assertEqual(exited["createdAtUtc"] is not None, True)

    def test_powershell_legacy_process_times_are_comparable(self):
        parsed = guard.parse_process_time("/Date(1792065613680)/")
        self.assertEqual(parsed.year, 2026)
        self.assertEqual(parsed.tzinfo.utcoffset(parsed), guard.timezone.utc.utcoffset(parsed))

    def test_native_exited_residual_does_not_block_but_live_unknown_does(self):
        stamp = "2026-10-15T10:00:00+00:00"
        cim = [{"ProcessId": 36408, "CreationDate": stamp, "CommandLine": None}]
        views = lambda: (cim, [], [36408], None)
        exited = lambda _pid: {"state": "EXITED_CONFIRMED", "createdAtUtc": stamp,
                                "imagePath": r"C:\Unity\Unity.exe", "exitCode": 0}
        processes, error = guard.process_inventory(native_probe=exited, views_probe=views)
        self.assertIsNone(error)
        self.assertEqual(processes[0]["nativeState"], "EXITED_CONFIRMED")
        self.assertEqual(guard.admission(self.project, lambda: (processes, error), self.free_lock)["state"], "FREE")
        live = lambda _pid: {"state": "LIVE_CONFIRMED", "createdAtUtc": stamp,
                              "imagePath": r"C:\Unity\Unity.exe", "exitCode": 259}
        processes, error = guard.process_inventory(native_probe=live, views_probe=views)
        self.assertEqual(processes[0]["nativeState"], "LIVE_UNIDENTIFIED")
        self.assertEqual(guard.admission(self.project, lambda: (processes, error), self.free_lock)["state"], "UNKNOWN")

    def test_missing_historical_pid_needs_native_not_found_and_absent_snapshot(self):
        stamp = "2026-10-15T10:00:00+00:00"
        stale = [{"ProcessId": 26772, "CreationDate": stamp, "CommandLine": None}]
        missing = lambda _pid: {"state": "PID_NOT_FOUND", "openError": 87}
        rows, error = guard.process_inventory(native_probe=missing,
            views_probe=lambda: (stale, [], [], None))
        self.assertIsNone(error)
        self.assertEqual(rows[0]["nativeState"], "NOT_FOUND_CONFIRMED")
        self.assertEqual(guard.admission(self.project, lambda: (rows, error), self.free_lock)["state"], "FREE")
        rows, error = guard.process_inventory(native_probe=missing,
            views_probe=lambda: (stale, [], [26772], None))
        self.assertEqual(guard.admission(self.project, lambda: (rows, error), self.free_lock)["state"], "UNKNOWN")

    def test_signaled_handle_without_query_fields_is_still_exited(self):
        old = [{"ProcessId": 36408, "CreationDate": "2026-10-15T10:00:00+00:00",
                "CommandLine": None}]
        native = lambda _pid: {"state": "EXITED_CONFIRMED", "waitResult": 0,
                               "createdAtUtc": None, "imagePath": None,
                               "reason": "process query access denied"}
        rows, error = guard.process_inventory(native_probe=native,
            views_probe=lambda: (old, [], [36408], None))
        self.assertIsNone(error)
        self.assertEqual(rows[0]["identityStatus"], "EXITED_HANDLE_ONLY")
        self.assertEqual(guard.admission(self.project, lambda: (rows, error), self.free_lock)["state"], "FREE")

    @unittest.skipUnless(os.name == "nt", "Windows process handles")
    def test_real_invalid_pid_reports_native_error_code(self):
        missing = guard.native_process_probe(0xFFFFFFFF)
        self.assertEqual(missing["openError"], 87, missing)
        self.assertEqual(missing["state"], "PID_NOT_FOUND", missing)

    def test_native_live_same_project_blocks_and_other_project_is_free(self):
        stamp = "2026-10-15T10:00:00+00:00"
        command = 'Unity.exe -projectPath "' + str(self.project) + '"'
        cim = [{"ProcessId": 20, "CreationDate": stamp, "CommandLine": command}]
        proc = [{"Id": 20, "StartTime": stamp}]
        native = lambda _pid: {"state": "LIVE_CONFIRMED", "createdAtUtc": stamp,
                                "imagePath": r"C:\Unity\Unity.exe", "exitCode": 259}
        processes, error = guard.process_inventory(native_probe=native,
                                                   views_probe=lambda: (cim, proc, [20], None))
        self.assertIsNone(error)
        self.assertEqual(guard.admission(self.project, lambda: (processes, error), self.free_lock)["state"], "BUSY_EXTERNAL")
        cim[0]["CommandLine"] = 'Unity.exe -projectPath "' + str(self.state) + '"'
        processes, error = guard.process_inventory(native_probe=native,
                                                   views_probe=lambda: (cim, proc, [20], None))
        self.assertEqual(guard.admission(self.project, lambda: (processes, error), self.free_lock)["state"], "FREE")

    def test_pid_reuse_native_failure_and_changing_views_fail_closed(self):
        stamp = "2026-10-15T10:00:00+00:00"
        cim = [{"ProcessId": 20, "CreationDate": stamp, "CommandLine": "Unity.exe"}]
        proc = [{"Id": 20, "StartTime": stamp}]
        reused = lambda _pid: {"state": "LIVE_CONFIRMED", "createdAtUtc": "2026-10-15T10:10:00+00:00",
                                "imagePath": r"C:\Unity\Unity.exe", "exitCode": 259}
        processes, error = guard.process_inventory(native_probe=reused,
                                                   views_probe=lambda: (cim, proc, [20], None))
        self.assertIn("creation times disagree", error)
        failed = lambda _pid: {"state": "PROCESS_STATE_UNKNOWN", "reason": "OpenProcess error 5"}
        processes, error = guard.process_inventory(native_probe=failed,
                                                   views_probe=lambda: (cim, proc, [20], None))
        self.assertIn("OpenProcess error 5", error)
        calls = iter([(cim, proc, [20], None), ([], [], [], None)])
        processes, error = guard.process_inventory(native_probe=lambda _pid: {
            "state": "LIVE_CONFIRMED", "createdAtUtc": stamp,
            "imagePath": r"C:\Unity\Unity.exe", "exitCode": 259},
            views_probe=lambda: next(calls))
        self.assertIn("changed during inventory", error)

    def test_native_probe_exception_and_image_mismatch_fail_closed(self):
        stamp = "2026-10-15T10:00:00+00:00"
        cim = [{"ProcessId": 20, "CreationDate": stamp,
                "CommandLine": 'Unity.exe -projectPath "' + str(self.project) + '"'}]
        proc = [{"Id": 20, "StartTime": stamp}]
        views = lambda: (cim, proc, [20], None)

        def denied(_pid):
            raise OSError(5, "Access denied")

        processes, error = guard.process_inventory(native_probe=denied, views_probe=views)
        self.assertEqual(processes[0]["nativeState"], "PROCESS_STATE_UNKNOWN")
        self.assertIn("Access denied", error)
        reused = lambda _pid: {"state": "LIVE_CONFIRMED", "createdAtUtc": stamp,
                                "imagePath": r"C:\Other\python.exe", "exitCode": 259}
        processes, error = guard.process_inventory(native_probe=reused, views_probe=views)
        self.assertIn("PID reused", error)

    def test_empty_inventory_requires_two_complete_views(self):
        calls = 0

        def views():
            nonlocal calls
            calls += 1
            return [], [], [], None

        processes, error = guard.process_inventory(views_probe=views)
        self.assertEqual((processes, error, calls), ([], None, 2))
        self.assertEqual(guard.admission(self.project, lambda: (processes, error), self.free_lock)["internalState"],
                         "PROJECT_FREE_VERIFIED")

    def test_runner_rechecks_admission_immediately_before_spawn(self):
        calls = 0

        def inventory():
            nonlocal calls
            calls += 1
            if calls == 1:
                return [], None
            return [], "process state changed before launch"

        result = guard.run(self.project, "task-a", "fixture", "NON_UNITY_TEST", self.probe_command,
                           2, self.state, inventory, self.free_lock,
                           command_validator=lambda *_: None)
        self.assertEqual(result["state"], "BLOCKED")
        self.assertFalse(result["launched"])
        self.assertEqual(calls, 2)

    def test_invalid_snapshot_manifest_blocks_before_launch(self):
        result = guard.run(self.project, "task-a", "fixture", "NON_UNITY_TEST", self.probe_command,
                           2, self.state, self.free_inventory, self.free_lock,
                           manifest_path=Path(self.temp.name) / "missing.json",
                           command_validator=lambda *_: None)
        self.assertEqual(result["state"], "BLOCKED")
        self.assertIsNone(result["unityPid"])

    def test_dashboard_uses_real_status_and_events(self):
        self.run_probe()
        path = guard.dashboard(self.project, self.state, self.free_inventory, self.free_lock)
        page = path.read_text(encoding="utf-8")
        self.assertIn("当前状态：FREE", page)
        self.assertIn("task-a", page)


if __name__ == "__main__":
    unittest.main()
