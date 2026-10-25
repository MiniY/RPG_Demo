from __future__ import annotations

import subprocess
import tempfile
import unittest
from pathlib import Path

import unity_project_snapshot as snap


class UnityProjectSnapshotTests(unittest.TestCase):
    def setUp(self):
        fixtures = Path(__file__).parent / ".test-fixtures"
        fixtures.mkdir(exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=fixtures)
        self.addCleanup(self.temp.cleanup)
        root = Path(self.temp.name)
        self.repo = root / "repo"
        (self.repo / "Assets").mkdir(parents=True)
        (self.repo / "Packages").mkdir()
        (self.repo / "ProjectSettings").mkdir()
        (self.repo / "Assets" / "Feature.cs").write_text("class Feature {}", encoding="utf-8")
        (self.repo / "Assets" / "Feature.cs.meta").write_text("guid: 123\n", encoding="utf-8")
        (self.repo / "Packages" / "manifest.json").write_text("{}", encoding="utf-8")
        (self.repo / "ProjectSettings" / "ProjectVersion.txt").write_text("m_EditorVersion: 2022.3.62f3c1", encoding="utf-8")
        subprocess.run(["git", "init", "-q", str(self.repo)], check=True)
        subprocess.run(["git", "-C", str(self.repo), "add", "."], check=True)
        subprocess.run(["git", "-C", str(self.repo), "-c", "user.name=test", "-c",
                        "user.email=test@example.invalid", "commit", "-qm", "baseline"], check=True)
        self.out = root / "copies"

    def test_complete_copy_includes_dirty_and_untracked_with_manifest(self):
        (self.repo / "Assets" / "Feature.cs").write_text("class Changed {}", encoding="utf-8")
        (self.repo / "Assets" / "New.png").write_bytes(b"real asset bytes")
        (self.repo / "Assets" / "New.png.meta").write_text("guid: 456\n", encoding="utf-8")
        result = snap.create(self.repo, self.out)
        self.assertEqual(result["captureStatus"], "PASS")
        folder = Path(result["manifest"]).parent
        self.assertEqual((folder / "project/Assets/New.png").read_bytes(), b"real asset bytes")
        self.assertFalse((folder / "project/Library").exists())
        self.assertEqual(snap.verify(folder)["SNAPSHOT_VERIFICATION"], "PASS")

    def test_live_change_is_stale_but_copy_stays_valid(self):
        folder = Path(snap.create(self.repo, self.out)["manifest"]).parent
        (self.repo / "Assets/Feature.cs").write_text("class Later {}", encoding="utf-8")
        result = snap.verify(folder)
        self.assertEqual((result["SNAPSHOT_VERIFICATION"], result["LIVE_WORKTREE_FRESHNESS"]),
                         ("PASS", "STALE"))
        self.assertEqual(result["changedLive"], ["Assets/Feature.cs"])

    def test_tampered_copy_blocks(self):
        folder = Path(snap.create(self.repo, self.out)["manifest"]).parent
        (folder / "project/Assets/Feature.cs").write_text("tampered", encoding="utf-8")
        self.assertEqual(snap.verify(folder)["SNAPSHOT_VERIFICATION"], "BLOCKED")

    def test_missing_project_inputs_refuse_capture(self):
        (self.repo / "ProjectSettings/ProjectVersion.txt").unlink()
        with self.assertRaisesRegex(ValueError, "ProjectVersion"):
            snap.create(self.repo, self.out)


if __name__ == "__main__":
    unittest.main()
