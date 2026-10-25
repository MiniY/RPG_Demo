"""Real Windows process/Job tests. Never launches Unity. Admission is a fixture."""
from __future__ import annotations

import os
import json
import hashlib
from pathlib import Path
import subprocess
import sys
import tempfile
import threading
import time
import unittest
import uuid

import unity_supervisor as supervisor


@unittest.skipUnless(os.name == "nt", "Windows Job Objects required")
class SupervisorTests(unittest.TestCase):
    def setUp(self):
        evidence = os.environ.get("SUPERVISOR_TEST_EVIDENCE")
        if evidence:
            self.root = Path(evidence).resolve() / (self._testMethodName + "-" + uuid.uuid4().hex[:8])
            self.root.mkdir(parents=True)
        else:
            fixtures = Path(__file__).parent / ".test-fixtures"
            fixtures.mkdir(exist_ok=True)
            temporary = tempfile.TemporaryDirectory(dir=fixtures)
            self.addCleanup(temporary.cleanup)
            self.root = Path(temporary.name).resolve()
        self.project = self.root / "project"
        (self.project / "Assets").mkdir(parents=True)
        (self.project / "ProjectSettings").mkdir()

    def request(self, script="print('owned stdout', flush=True)"):
        return dict(schemaVersion=1, runId=uuid.uuid4().hex, taskId="probe", taskName="isolated process probe",
                    operation="NON_UNITY_TEST", projectPath=str(self.project),
                    command=[sys.executable, "-c", script], timeoutSeconds=10,
                    cancelFile=str(self.root / "cancel.request"), caller=supervisor.identity(os.getpid()),
                    stdoutPath=str(self.root / "stdout.log"), stderrPath=str(self.root / "stderr.log"),
                    treeExitGraceSeconds=.2, cleanupTimeoutSeconds=5)

    def execute(self, request):
        return supervisor.execute(request, self.root / "result.json",
                                  admission=lambda _: {"state": "FREE", "reason": "fixture admission"},
                                  command_validator=lambda *_: None)

    def wait_file(self, path):
        deadline = time.monotonic() + 8
        while time.monotonic() < deadline:
            if path.is_file():
                try:
                    return json.loads(path.read_text())
                except json.JSONDecodeError:
                    pass
            time.sleep(.03)
        self.fail("Fixture did not signal ready: " + str(path))

    def tree_request(self):
        marker = self.root / "ready.json"
        child_marker = self.root / "child-ready.json"
        child_script = ("import subprocess,sys,time,json; from pathlib import Path; "
                        "grandchild=subprocess.Popen([sys.executable,'-c','import time; time.sleep(60)']); "
                        f"Path({str(child_marker)!r}).write_text(json.dumps({{'grandchild':grandchild.pid}})); time.sleep(60)")
        script = f"""import subprocess,sys,time,json,os
from pathlib import Path
child=subprocess.Popen([sys.executable,'-c',{child_script!r}])
marker=Path({str(child_marker)!r})
while not marker.exists(): time.sleep(.01)
data=json.loads(marker.read_text())
Path({str(marker)!r}).write_text(json.dumps(dict(root=os.getpid(),child=child.pid,**data)))
time.sleep(60)
"""
        return self.request(script), marker

    def worker(self, request):
        request_file = self.root / "request.json"
        request_file.write_text(json.dumps(request), encoding="utf-8")
        script = ("import sys,json; from pathlib import Path; sys.path.insert(0,sys.argv[1]); "
                  "import unity_supervisor as s; r=s.execute(json.loads(Path(sys.argv[2]).read_text()),"
                  "Path(sys.argv[3]),admission=lambda _:{'state':'FREE','reason':'fixture'},"
                  "command_validator=lambda *_:None); sys.exit(0 if s.successful(r) else 2)")
        output = (self.root / "worker.log").open("wb")
        self.addCleanup(output.close)
        proc = subprocess.Popen([sys.executable, "-c", script, str(Path(supervisor.__file__).parent),
                                 str(request_file), str(self.root / "result.json")], stdout=output, stderr=output)

        def stop_worker():
            if proc.poll() is None:
                Path(request["cancelFile"]).write_text("test cleanup")
                try:
                    proc.wait(timeout=8)
                except subprocess.TimeoutExpired:
                    proc.terminate()  # Only the exact Python fixture Popen object.
                    proc.wait(timeout=8)
        self.addCleanup(stop_worker)
        return proc

    def pin_fixture(self, pid):
        handle = supervisor.ProcessHandle(supervisor.Native(), pid)
        self.addCleanup(handle.close)
        return handle

    def test_natural_exit_has_separate_root_and_cleanup_evidence(self):
        result = self.execute(self.request())
        self.assertEqual((result["state"], result["exitCode"], result["naturalExit"]), ("COMPLETED", 0, True))
        self.assertEqual((result["cleanup"]["status"], result["cleanup"]["forced"],
                          result["cleanup"]["residualPids"]), ("PASS", False, []))
        self.assertIn("owned stdout", Path(result["stdoutPath"]).read_text())

    def test_timeout_reclaims_real_root_and_descendant(self):
        request, marker = self.tree_request()
        request["timeoutSeconds"] = 1.5
        worker = self.worker(request)
        pids = self.wait_file(marker)
        handles = [self.pin_fixture(pid) for pid in pids.values()]
        self.assertEqual(worker.wait(timeout=12), 2)
        result = json.loads((self.root / "result.json").read_text())
        self.assertEqual((result["state"], result["naturalExit"], result["cleanup"]["status"],
                          result["cleanup"]["forced"]), ("TIMEOUT", False, "PASS", True))
        self.assertTrue(all(handle.exited() for handle in handles))

    def test_cancel_reclaims_job_but_leaves_unrelated_process_alive(self):
        foreign = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(60)"])
        self.addCleanup(lambda: (foreign.terminate(), foreign.wait(timeout=5)) if foreign.poll() is None else None)
        request, marker = self.tree_request()
        worker = self.worker(request)
        pids = self.wait_file(marker)
        handles = [self.pin_fixture(pid) for pid in pids.values()]
        Path(request["cancelFile"]).write_text("cancel")
        self.assertEqual(worker.wait(timeout=10), 2)
        result = json.loads((self.root / "result.json").read_text())
        self.assertEqual((result["state"], result["cleanup"]["status"]), ("CANCELLED", "PASS"))
        self.assertTrue(all(handle.exited() for handle in handles))
        self.assertIsNone(foreign.poll())

    def test_caller_exit_reclaims_job_with_pinned_identity(self):
        caller = subprocess.Popen([sys.executable, "-c", "import time; time.sleep(60)"])
        self.addCleanup(lambda: (caller.terminate(), caller.wait(timeout=5)) if caller.poll() is None else None)
        request, marker = self.tree_request()
        request["caller"] = supervisor.identity(caller.pid)
        worker = self.worker(request)
        pids = self.wait_file(marker)
        handles = [self.pin_fixture(pid) for pid in pids.values()]
        caller.terminate()
        caller.wait(timeout=5)
        self.assertEqual(worker.wait(timeout=10), 2)
        result = json.loads((self.root / "result.json").read_text())
        self.assertEqual((result["state"], result["cleanup"]["status"]), ("CALLER_EXITED", "PASS"))
        self.assertTrue(all(handle.exited() for handle in handles))

    def test_supervisor_death_closes_noninherited_job_and_reclaims_tree(self):
        request, marker = self.tree_request()
        worker = self.worker(request)
        pids = self.wait_file(marker)
        handles = [self.pin_fixture(pid) for pid in pids.values()]
        worker.terminate()  # Our exact Supervisor fixture, never a PID-selected Unity.
        worker.wait(timeout=5)
        deadline = time.monotonic() + 5
        while not all(h.exited() for h in handles) and time.monotonic() < deadline:
            time.sleep(.03)
        self.assertTrue(all(h.exited() for h in handles))
        # No forged final cleanup evidence after an uncatchable death.
        self.assertEqual(json.loads((self.root / "result.json").read_text())["state"], "RUNNING")
        lease = supervisor.HeavyMutex(self.project)
        self.assertTrue(lease.acquire()[0])
        lease.release()

    def test_global_mutex_refuses_same_and_different_project_contenders(self):
        request, marker = self.tree_request()
        worker = self.worker(request)
        self.wait_file(marker)
        for name, project in (("same", self.project), ("different", self.root / "other")):
            if name == "different":
                (project / "Assets").mkdir(parents=True)
                (project / "ProjectSettings").mkdir()
            contender = self.request()
            contender.update(projectPath=str(project), stdoutPath=str(self.root / (name + "-out.log")),
                             stderrPath=str(self.root / (name + "-err.log")))
            result = supervisor.execute(contender, self.root / (name + "-result.json"),
                                        admission=lambda _: {"state": "FREE"}, command_validator=lambda *_: None)
            self.assertEqual((result["state"], result["launched"], result["reason"]),
                             ("BLOCKED", False, "HEAVY_MUTEX_OWNED"))
        Path(request["cancelFile"]).write_text("cancel")
        worker.wait(timeout=10)

    def test_wrong_caller_creation_time_blocks_before_launch(self):
        request = self.request()
        request["caller"]["createdAtUtc"] = "2000-01-01T00:00:00+00:00"
        result = self.execute(request)
        self.assertEqual((result["state"], result["launched"]), ("BLOCKED", False))
        self.assertIn("creation time differs", result["reason"])

    def test_final_admission_change_refuses_launch_and_releases_locks(self):
        count = 0

        def admission(_):
            nonlocal count
            count += 1
            return {"state": "FREE" if count == 1 else "UNKNOWN"}

        result = supervisor.execute(self.request(), self.root / "result.json", admission=admission,
                                    command_validator=lambda *_: None)
        self.assertEqual((result["state"], result["launched"]), ("BLOCKED", False))
        self.assertTrue(all(lease["released"] for lease in result["locks"].values()))

    def test_job_assignment_failure_never_resumes_or_orphans_root(self):
        marker = self.root / "must-not-run.txt"
        job = supervisor.OwnedJob(supervisor.Native())
        job.close()  # Real invalid-handle failure from Windows Job assignment at creation.
        self.addCleanup(lambda: job.root.close() if job.root and job.root.handle else None)
        with (self.root / "out.log").open("xb", buffering=0) as out, (self.root / "err.log").open("xb", buffering=0) as err:
            with self.assertRaises(OSError) as failure:
                job.spawn([sys.executable, "-c", f"from pathlib import Path; Path({str(marker)!r}).write_text('bad')"],
                          self.project, out, err, {})
        (self.root / "native-failure.json").write_text(json.dumps({"errorCode": failure.exception.errno,
                                                                 "detail": str(failure.exception)}))
        # Assert using the real root handle, not thread counts or stale PID lists.
        try:
            self.assertTrue(job.root is None or job.root.exited())
            self.assertFalse(marker.exists())
        finally:
            if job.root and not job.root.exited():
                job.terminate()

    def test_post_exit_inventory_failure_is_separate_from_owned_cleanup(self):
        count = 0

        def admission(_):
            nonlocal count
            count += 1
            if count == 3:
                raise OSError(5, "fixture post-exit inventory denied")
            return {"state": "FREE"}

        result = supervisor.execute(self.request(), self.root / "result.json", admission=admission,
                                    command_validator=lambda *_: None)
        self.assertEqual(result["state"], "FAILED")
        self.assertEqual((result["naturalExit"], result["cleanup"]["status"]), (True, "PASS"))
        self.assertEqual(result["postExitAdmission"]["state"], "UNKNOWN")

    def test_changed_snapshot_manifest_fails_binding_without_falsifying_cleanup(self):
        manifest = self.root / "manifest.json"
        manifest.write_text('{}')
        request = self.request(f"from pathlib import Path; Path({str(manifest)!r}).write_text('changed')")
        request["snapshotEvidence"] = {"snapshotId": "probe-snapshot", "manifestPath": str(manifest),
                                       "sha256": hashlib.sha256(manifest.read_bytes()).hexdigest()}
        result = self.execute(request)
        self.assertEqual(result["state"], "FAILED")
        self.assertEqual((result["cleanup"]["status"], result["snapshotEvidenceStatus"]), ("PASS", "FAIL"))

    def test_nonzero_root_exit_keeps_natural_exit_and_cleanup_independent(self):
        result = self.execute(self.request("import sys; print('expected diagnostic',file=sys.stderr); sys.exit(7)"))
        self.assertEqual((result["state"], result["exitCode"], result["naturalExit"], result["cleanup"]["status"]),
                         ("FAILED", 7, True, "PASS"))
        self.assertIn("expected diagnostic", Path(result["stderrPath"]).read_text())

    def test_descendant_outliving_successful_root_is_failure_even_after_cleanup(self):
        request = self.request("import subprocess,sys; subprocess.Popen([sys.executable,'-c','import time; time.sleep(60)'])")
        result = self.execute(request)
        self.assertEqual((result["state"], result["reason"], result["exitCode"], result["naturalExit"]),
                         ("FAILED", "DESCENDANTS_OUTLIVED_ROOT", 0, True))
        self.assertEqual((result["cleanup"]["status"], result["cleanup"]["forced"]), ("PASS", True))

    def test_project_mutex_contention_also_blocks_and_releases_heavy_mutex(self):
        lease = supervisor.guard.ProjectMutex(self.project)
        self.assertTrue(lease.acquire()[0])
        try:
            result = self.execute(self.request())
            self.assertEqual((result["state"], result["reason"], result["launched"]),
                             ("BLOCKED", "PROJECT_MUTEX_OWNED", False))
            self.assertTrue(result["locks"]["heavy"]["released"])
        finally:
            lease.release()

    def test_cli_uses_native_identity_and_rejects_non_unity_command(self):
        path = str(Path(supervisor.__file__).resolve())
        query = subprocess.run([sys.executable, path, "identity", "--pid", str(os.getpid())],
                               capture_output=True, text=True, timeout=5)
        self.assertEqual(query.returncode, 0)
        self.assertEqual(json.loads(query.stdout)["createdAtUtc"], supervisor.identity(os.getpid())["createdAtUtc"])
        request_file = self.root / "request.json"
        request_file.write_text(json.dumps(self.request()))
        rejected = subprocess.run([sys.executable, path, "run", "--request", str(request_file),
                                   "--result", str(self.root / "result.json")],
                                  capture_output=True, text=True, timeout=5)
        self.assertEqual(rejected.returncode, 2)
        self.assertFalse(json.loads(rejected.stdout)["launched"])
        self.assertFalse((self.root / "stdout.log").exists())

    def test_existing_result_is_never_overwritten_or_relaunched(self):
        request = self.request()
        self.execute(request)
        original = (self.root / "result.json").read_bytes()
        with self.assertRaises(FileExistsError):
            self.execute(request)
        self.assertEqual((self.root / "result.json").read_bytes(), original)

    def test_post_exit_inventory_still_holds_both_operating_system_mutexes(self):
        count = 0

        def admission(_):
            nonlocal count
            count += 1
            if count == 3:
                script = ("import sys; from pathlib import Path; sys.path.insert(0,sys.argv[1]); "
                          "import unity_supervisor as s; p=Path(sys.argv[2]); "
                          "print(s.HeavyMutex(p).acquire()[0],s.guard.ProjectMutex(p).acquire()[0])")
                observed = subprocess.run([sys.executable, "-c", script, str(Path(supervisor.__file__).parent),
                                           str(self.project)], capture_output=True, text=True, timeout=5)
                self.assertEqual((observed.returncode, observed.stdout.strip()), (0, "False False"))
            return {"state": "FREE"}

        result = supervisor.execute(self.request(), self.root / "result.json", admission=admission,
                                    command_validator=lambda *_: None)
        self.assertTrue(supervisor.successful(result))

    def test_preexisting_cancel_refuses_launch_without_deleting_request(self):
        request = self.request()
        Path(request["cancelFile"]).write_text("cancel")
        result = self.execute(request)
        self.assertEqual((result["state"], result["launched"], result["reason"]), ("BLOCKED", False, "CANCELLED"))
        self.assertTrue(Path(request["cancelFile"]).is_file())

    def test_unverifiable_job_cleanup_never_explicitly_releases_execution_locks(self):
        request, marker = self.tree_request()
        holder = {}

        def factory(native):
            holder['job'] = supervisor.OwnedJob(native)
            return holder['job']

        def invalidate_owned_handle():
            self.wait_file(marker)
            holder['job'].close()  # Real kernel cleanup, then query gets invalid handle.

        invalidator = threading.Thread(target=invalidate_owned_handle)
        invalidator.start()
        try:
            result = supervisor.execute(request, self.root / "result.json", job_factory=factory,
                                        admission=lambda _: {"state": "FREE"}, command_validator=lambda *_: None)
            invalidator.join(timeout=8)
            self.assertEqual((result["state"], result["cleanup"]["status"]), ("FAILED", "UNKNOWN"))
            self.assertTrue(all(not lease["released"] and lease["quarantined"] for lease in result["locks"].values()))
            script = ("import sys;from pathlib import Path;sys.path.insert(0,sys.argv[1]);"
                      "import unity_supervisor as s;p=Path(sys.argv[2]);"
                      "print(s.HeavyMutex(p).acquire()[0],s.guard.ProjectMutex(p).acquire()[0])")
            observed = subprocess.run([sys.executable, "-c", script, str(Path(supervisor.__file__).parent),
                                       str(self.project)], capture_output=True, text=True, timeout=5)
            self.assertEqual(observed.stdout.strip(), "False False")
        finally:
            invalidator.join(timeout=8)
            # Release only our test's own objects on the thread that acquired
            # them; production exposes no force-unlock command.
            for lease in supervisor._QUARANTINED_LEASES:
                lease.release()
            supervisor._QUARANTINED_LEASES.clear()


if __name__ == "__main__":
    unittest.main()
