"""Isolated Git integration tests for the guarded update and installed hooks."""

import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SOURCE_ROOT = Path(__file__).resolve().parents[2]
STUB_SCANNER = r'''
import argparse, hashlib, json, subprocess, sys
from pathlib import Path
root = Path(__file__).resolve().parents[2]
p = argparse.ArgumentParser()
p.add_argument('--portable', action='store_true')
p.add_argument('--check', action='store_true')
p.add_argument('--output-dir', type=Path)
a = p.parse_args()
if (root / 'fail.scan').exists():
    print('injected scanner failure', file=sys.stderr)
    raise SystemExit(3)
content = (root / 'Assets' / 'marker.txt').read_text()
sha = hashlib.sha256(content.encode()).hexdigest()
head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
graph = {'schemaVersion': 1, 'generatedAtUtc': '2026-10-13T00:00:00Z',
         'root': '.', 'head': head, 'sourceBranch': 'main', 'sourceDirty': False,
         'sourceSha256': sha, 'nodes': [{'id': 'asset:' + content}], 'edges': []}
out = a.output_dir or root / 'Docs' / 'architecture-audit'
saved = out / 'graph.json'
if a.check:
    ok = saved.exists() and json.loads(saved.read_text())['sourceSha256'] == sha and (out / 'viewer.html').exists()
    print(json.dumps({'upToDate': ok, 'nodes': 1, 'edges': 0}))
    raise SystemExit(0 if ok else 1)
out.mkdir(parents=True, exist_ok=True)
saved.write_text(json.dumps(graph))
(out / 'viewer.html').write_text('<script>const graph=' + json.dumps(graph) + ';</script>')
print(json.dumps({'nodes': 1, 'edges': 0}))
'''


class UpdateIntegrationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="rpg-audit-hook-test-",
                                                     dir=SOURCE_ROOT / "Docs" / "architecture-audit")
        self.root = Path(self.temporary.name)
        (self.root / "Tools" / "ArchitectureAudit").mkdir(parents=True)
        (self.root / "Assets").mkdir()
        (self.root / "Docs" / "architecture-audit").mkdir(parents=True)
        shutil.copy2(SOURCE_ROOT / "Tools" / "ArchitectureAudit" / "update.py",
                     self.root / "Tools" / "ArchitectureAudit" / "update.py")
        (self.root / "Tools" / "ArchitectureAudit" / "scan.py").write_text(STUB_SCANNER)
        shutil.copytree(SOURCE_ROOT / ".githooks", self.root / ".githooks")
        for hook in (self.root / ".githooks").iterdir():
            hook.chmod(0o755)
        (self.root / "Assets" / "marker.txt").write_text("one")
        self.git("init", "-b", "main")
        self.git("config", "user.name", "Audit Test")
        self.git("config", "user.email", "audit@example.invalid")
        self.git("add", ".")
        self.git("add", "--chmod=+x", ".githooks/post-commit", ".githooks/post-merge",
                 ".githooks/post-checkout", ".githooks/post-rewrite")
        self.git("commit", "-m", "initial fixture")
        initial_update = self.run_update()
        self.assertEqual(initial_update.returncode, 0, initial_update.stderr)
        self.git("add", "Docs/architecture-audit/graph.json", "Docs/architecture-audit/viewer.html",
                 "Docs/architecture-audit/GRAPH-CHANGES.json")
        self.git("commit", "-m", "baseline graph")
        self.git("config", "core.hooksPath", ".githooks")
        self.assertEqual(self.git("config", "--get", "core.hooksPath").stdout.strip(), ".githooks")
        probe = subprocess.run(["git", "hook", "run", "post-commit"], cwd=self.root,
                               capture_output=True, text=True)
        self.assertEqual(probe.returncode, 0, probe.stderr)
        self.assertIn('up-to-date', probe.stdout + probe.stderr)

    def tearDown(self):
        self.temporary.cleanup()

    def git(self, *args):
        result = subprocess.run(["git", *args], cwd=self.root, capture_output=True,
                                text=True)
        if result.returncode:
            self.fail(f"git {args}: {result.stdout} {result.stderr}")
        return result

    def run_update(self):
        return subprocess.run([sys.executable, str(self.root / "Tools" / "ArchitectureAudit" / "update.py")],
                              cwd=self.root, capture_output=True, text=True)

    def graph(self):
        return json.loads((self.root / "Docs" / "architecture-audit" / "graph.json").read_text())

    def test_unchanged_run_is_no_op(self):
        before = (self.root / "Docs" / "architecture-audit" / "graph.json").read_bytes()
        result = self.run_update()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('up-to-date', result.stdout)
        self.assertEqual(before, (self.root / "Docs" / "architecture-audit" / "graph.json").read_bytes())

    def test_main_post_commit_updates_and_reports_difference(self):
        (self.root / "Assets" / "marker.txt").write_text("two")
        self.git("add", "Assets/marker.txt")
        commit = self.git("commit", "-m", "source change")
        self.assertIn('"action": "updated"', commit.stdout + commit.stderr)
        self.assertEqual(self.graph()["nodes"][0]["id"], "asset:two")
        report = json.loads((self.root / "Docs" / "architecture-audit" / "GRAPH-CHANGES.json").read_text())
        self.assertEqual(report["addedNodes"], ["asset:two"])
        self.assertEqual(report["removedNodes"], ["asset:one"])

    def test_other_branch_skips(self):
        self.git("switch", "-c", "feature")
        (self.root / "Assets" / "marker.txt").write_text("two")
        self.git("add", "Assets/marker.txt")
        commit = self.git("commit", "-m", "feature source")
        self.assertIn('"action": "skipped"', commit.stdout + commit.stderr)
        self.assertEqual(self.graph()["nodes"][0]["id"], "asset:one")

    def test_dirty_generated_file_is_preserved(self):
        graph_path = self.root / "Docs" / "architecture-audit" / "graph.json"
        graph_path.write_text(graph_path.read_text() + "\n")
        before = graph_path.read_bytes()
        (self.root / "Assets" / "marker.txt").write_text("two")
        self.git("-c", "core.hooksPath=/dev/null", "add", "Assets/marker.txt")
        self.git("-c", "core.hooksPath=/dev/null", "commit", "-m", "source change")
        result = self.run_update()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('Generated files already have local edits', result.stderr)
        self.assertEqual(graph_path.read_bytes(), before)

    def test_scan_failure_keeps_old_files(self):
        graph_path = self.root / "Docs" / "architecture-audit" / "graph.json"
        before = graph_path.read_bytes()
        (self.root / "Assets" / "marker.txt").write_text("two")
        self.git("-c", "core.hooksPath=/dev/null", "add", "Assets/marker.txt")
        self.git("-c", "core.hooksPath=/dev/null", "commit", "-m", "source change")
        (self.root / "fail.scan").write_text("fail")
        result = self.run_update()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('Consistency scan failed', result.stderr)
        self.assertEqual(graph_path.read_bytes(), before)


if __name__ == "__main__":
    unittest.main()
