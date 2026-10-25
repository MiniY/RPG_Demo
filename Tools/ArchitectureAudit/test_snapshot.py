"""Isolated Git fixtures for fixed static copies and live freshness."""

import contextlib
import io
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from argparse import Namespace
from pathlib import Path
from unittest.mock import patch

import snapshot


class SnapshotTests(unittest.TestCase):
    def setUp(self):
        fixture_parent = Path(__file__).resolve().parents[1] / "EngineeringAssurance/.local/test-fixtures"
        fixture_parent.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=fixture_parent)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        if not self.root.resolve().is_relative_to(fixture_parent.resolve()):
            raise RuntimeError("Unsafe fixture cleanup target")
        self.repo = self.root / "repo"
        self.repo.mkdir()
        self.output = self.root / "snapshots"
        source_tools = Path(snapshot.__file__).parent
        tools = self.repo / "Tools/ArchitectureAudit"
        tools.mkdir(parents=True)
        for name in snapshot.SCAN_FILES[:3]:
            shutil.copyfile(source_tools / name, tools / name)
        assets = self.repo / "Assets"
        assets.mkdir()
        self.code = assets / "Example.cs"
        self.code.write_text("public class Example {}\n", encoding="utf-8")
        (assets / "Example.cs.meta").write_text("fileFormatVersion: 2\nguid: " + "a" * 32 + "\n")
        (self.repo / "Packages").mkdir()
        (self.repo / "Packages/manifest.json").write_text("{}\n")
        self.git("init", "-q")
        self.git("add", "--", "Assets", "Packages", "Tools")
        self.git("-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid",
                 "commit", "-qm", "fixture")

    def git(self, *args):
        return subprocess.check_output(["git", "-C", str(self.repo), *args], text=True).strip()

    def capture(self, action=None, test_reports=None):
        real_run = snapshot.run_scan
        existing = set(self.output.iterdir()) if self.output.exists() else set()

        def interleave(*args, **kwargs):
            if action:
                action()
            return real_run(*args, **kwargs)

        with contextlib.redirect_stdout(io.StringIO()), patch.object(snapshot, "run_scan", side_effect=interleave):
            code = snapshot.create(Namespace(repo=self.repo, out_root=self.output,
                                             scope=["Tools/ArchitectureAudit/"],
                                             test_report=test_reports))
        folder = next(iter(set(self.output.iterdir()) - existing))
        return code, folder, json.loads((folder / "manifest.json").read_text())

    def verify(self, folder):
        with contextlib.redirect_stdout(io.StringIO()):
            code = snapshot.verify(Namespace(snapshot=folder))
        report = max((folder / "reports").glob("verification-*.json"), key=lambda p: p.stat().st_mtime_ns)
        return code, json.loads(report.read_text())

    def test_unrelated_edit_during_scan_preserves_fixed_and_live_scan(self):
        code, folder, _ = self.capture(lambda: (self.repo / "README.md").write_text("other task\n"))
        self.assertEqual(code, 0)
        _, report = self.verify(folder)
        self.assertEqual((report["SNAPSHOT_VERIFICATION"], report["LIVE_WORKTREE_FRESHNESS"]),
                         ("PASS", "PASS"))
        self.assertEqual(report["CHANGE_ATTRIBUTION"], "UNKNOWN")
        self.assertEqual(report["outsideAuthorizedScope"], ["README.md"])

    def test_unrelated_edit_during_copy_is_recorded_without_failing_capture(self):
        real_copy = shutil.copyfile
        marker = self.repo / "README.md"

        def interleave(source, destination, *args, **kwargs):
            answer = real_copy(source, destination, *args, **kwargs)
            if Path(source) == self.code:
                marker.write_text("parallel task\n")
            return answer

        with patch.object(snapshot.shutil, "copyfile", side_effect=interleave):
            code, folder, manifest = self.capture()
        self.assertEqual(code, 0)
        self.assertEqual(manifest["parallelWorktreeChangesDuringCapture"], ["README.md"])
        _, report = self.verify(folder)
        self.assertEqual(report["SNAPSHOT_VERIFICATION"], "PASS")

    def test_related_edit_during_scan_stales_live_only(self):
        code, folder, _ = self.capture(lambda: self.code.write_text("public class Changed {}\n"))
        self.assertEqual(code, 0)
        _, report = self.verify(folder)
        self.assertEqual(report["SNAPSHOT_VERIFICATION"], "PASS")
        self.assertEqual(report["LIVE_WORKTREE_FRESHNESS"], "STALE")
        self.assertIn("Assets/Example.cs", report["changedScanInputs"])

    def test_related_edit_during_copy_blocks_capture(self):
        real_copy = shutil.copyfile

        def interleave(source, destination, *args, **kwargs):
            answer = real_copy(source, destination, *args, **kwargs)
            if Path(source) == self.code:
                self.code.write_text("public class ChangedWhileCopying {}\n")
            return answer

        with patch.object(snapshot.shutil, "copyfile", side_effect=interleave):
            code, _, manifest = self.capture()
        self.assertEqual(code, 2)
        self.assertEqual(manifest["captureStatus"], "RACE_DETECTED")
        self.assertIn("Assets/Example.cs", manifest["changedInputsDuringCapture"])
        race = manifest["captureRaceEvidence"]["Assets/Example.cs"]
        self.assertNotEqual(race["before"]["sha256"], race["after"]["sha256"])

    def test_existing_uncommitted_content_is_captured(self):
        self.code.write_text("public class DirtyAtStart {}\n")
        code, folder, manifest = self.capture()
        self.assertEqual(code, 0)
        self.assertIn("Assets/Example.cs", manifest["sourceDirtyAtCapture"])
        self.assertEqual((folder / "project/Assets/Example.cs").read_text(), self.code.read_text())
        _, report = self.verify(folder)
        self.assertEqual(report["SNAPSHOT_VERIFICATION"], "PASS")

    def test_player_topology_bodies_are_indexed_and_tampering_is_detected(self):
        bodies = {"Player.asmdef": '{"name":"Player"}\n',
                  "Player.asmref": '{"reference":"Player"}\n',
                  "csc.rsp": '-define:PLAYER_TOPOLOGY_PROBE\n'}
        for index, (name, body) in enumerate(bodies.items()):
            (self.repo / "Assets" / name).write_text(body)
            (self.repo / "Assets" / (name + ".meta")).write_text(
                "fileFormatVersion: 2\nguid: " + str(index + 1) * 32 + "\n")
        code, folder, manifest = self.capture()
        self.assertEqual(code, 0)
        graph = json.loads((folder / "reports/graph.json").read_text(encoding="utf-8"))
        indexed = {node["path"]: node for node in graph["nodes"]}
        for name, body in bodies.items():
            with self.subTest(name=name):
                relative = "Assets/" + name
                self.assertIn(relative, indexed)
                self.assertEqual(manifest["inputs"][relative]["mode"], "content")
                copied = folder / "project" / relative
                self.assertEqual(copied.read_text(), body)
                live = self.repo / relative
                live.write_text(body + "// changed topology\n")
                _, report = self.verify(folder)
                self.assertEqual(report["LIVE_WORKTREE_FRESHNESS"], "STALE")
                live.write_text(body)
                copied.write_text(body + "// changed fixed copy\n")
                _, report = self.verify(folder)
                self.assertEqual(report["SNAPSHOT_VERIFICATION"], "BLOCKED")
                copied.write_text(body)

    def test_post_capture_edit_is_stale(self):
        _, folder, _ = self.capture()
        self.code.write_text("public class Later {}\n")
        _, report = self.verify(folder)
        self.assertEqual(report["LIVE_WORKTREE_FRESHNESS"], "STALE")

    def test_two_writers_same_file_remain_unattributed(self):
        _, folder, _ = self.capture()
        for task in ("FirstWriter", "SecondWriter"):
            child = subprocess.run([sys.executable, "-c",
                                    "import os,sys; from pathlib import Path; "
                                    "Path(sys.argv[1]).write_text('public class '+sys.argv[2]+' {}\\n'); "
                                    "print('task='+sys.argv[2]+' pid='+str(os.getpid()))",
                                    str(self.code), task], capture_output=True, text=True, check=True)
            print(child.stdout.strip(), file=sys.stderr)
        _, report = self.verify(folder)
        self.assertEqual(report["CHANGE_ATTRIBUTION"], "UNKNOWN")
        self.assertEqual(report["changedWorktree"][0]["path"], "Assets/Example.cs")

    def test_head_change_is_stale(self):
        _, folder, _ = self.capture()
        (self.repo / "README.md").write_text("new commit\n")
        self.git("add", "README.md")
        self.git("-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid",
                 "commit", "-qm", "next")
        _, report = self.verify(folder)
        self.assertEqual(report["LIVE_WORKTREE_FRESHNESS"], "STALE")
        self.assertNotEqual(report["identityAtCapture"]["head"], report["identityNow"]["head"])

    def test_head_change_during_capture_blocks(self):
        real_copy = shutil.copyfile

        def interleave(source, destination, *args, **kwargs):
            answer = real_copy(source, destination, *args, **kwargs)
            if Path(source) == self.code:
                (self.repo / "README.md").write_text("new head\n")
                self.git("add", "README.md")
                self.git("-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid",
                         "commit", "-qm", "mid-capture")
            return answer

        with patch.object(snapshot.shutil, "copyfile", side_effect=interleave):
            code, _, manifest = self.capture()
        self.assertEqual(code, 2)
        self.assertTrue(manifest["identityChangedDuringCapture"])

    def test_copy_tamper_blocks_fixed_result(self):
        _, folder, _ = self.capture()
        (folder / "project/Assets/Example.cs").write_text("tampered\n")
        code, report = self.verify(folder)
        self.assertEqual(code, 2)
        self.assertEqual(report["SNAPSHOT_VERIFICATION"], "BLOCKED")

    def test_changed_test_log_does_not_become_pass(self):
        log = self.root / "test.log"
        log.write_text("Ran 1 test - OK\n")
        _, folder, _ = self.capture(test_reports=[log])
        log.write_text("overwritten\n")
        _, report = self.verify(folder)
        self.assertEqual(report["SNAPSHOT_VERIFICATION"], "PASS")
        self.assertEqual(report["TEST_EVIDENCE_LINK"], "PARTIAL")
        self.assertEqual(report["testEvidenceErrors"], [str(log)])

    def test_unavailable_live_asset_does_not_erase_fixed_result(self):
        _, folder, _ = self.capture()
        self.code.unlink()
        _, report = self.verify(folder)
        self.assertEqual(report["SNAPSHOT_VERIFICATION"], "PASS")
        self.assertEqual(report["LIVE_WORKTREE_FRESHNESS"], "UNKNOWN")
        self.assertIn("Asset for .meta is missing", report["liveError"])

    def test_transient_unity_test_scene_meta_is_excluded(self):
        transient = self.repo / "Assets/InitTestScene123.unity.meta"
        transient.write_text("fileFormatVersion: 2\nguid: " + "b" * 32 + "\n")
        code, folder, manifest = self.capture()
        self.assertEqual(code, 0)
        self.assertNotIn("Assets/InitTestScene123.unity.meta", manifest["inputs"])
        _, report = self.verify(folder)
        self.assertEqual(report["LIVE_WORKTREE_FRESHNESS"], "PASS")

    def test_missing_required_asset_or_governance_file_blocks(self):
        for name in ("Assets/Example.cs.meta", "Tools/ArchitectureAudit/scan.py"):
            with self.subTest(name=name):
                _, folder, _ = self.capture()
                (folder / "project" / name).unlink()
                code, report = self.verify(folder)
                self.assertEqual(code, 2)
                self.assertEqual(report["SNAPSHOT_VERIFICATION"], "BLOCKED")


if __name__ == "__main__":
    unittest.main()
