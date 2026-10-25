"""Guarded, repeatable main-branch architecture snapshot update.

The hook and PowerShell entry point both call this file. No Git index or remote
operation occurs here. A clean clone can run --check in CI.
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Docs" / "architecture-audit"
GENERATED = ("graph.json", "viewer.html", "GRAPH-CHANGES.json")


def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT, text=True,
                                   encoding="utf-8", errors="replace").strip()


def fail(message: str, code: int = 2) -> int:
    print(json.dumps({"ok": False, "reason": message}, ensure_ascii=False), file=sys.stderr)
    return code


def edge_key(edge: dict) -> tuple:
    proof = edge["evidence"]
    return (edge["source"], edge["relation"], edge["target"],
            proof["path"], proof["line"])


def diff_report(before: dict | None, after: dict) -> dict:
    old_nodes = {item["id"]: item for item in (before or {}).get("nodes", [])}
    new_nodes = {item["id"]: item for item in after["nodes"]}
    old_edges = {edge_key(item) for item in (before or {}).get("edges", [])}
    new_edges = {edge_key(item) for item in after["edges"]}
    return {
        "schemaVersion": 1,
        "fromSourceSha256": (before or {}).get("sourceSha256"),
        "toSourceSha256": after["sourceSha256"],
        "generatedAtUtc": after["generatedAtUtc"],
        "sourceBranch": after["sourceBranch"],
        "sourceCommitAtGeneration": after["head"],
        "oldNodeCount": len(old_nodes), "newNodeCount": len(new_nodes),
        "oldEdgeCount": len(old_edges), "newEdgeCount": len(new_edges),
        "unresolvedGuidCount": len(after.get("scan", {}).get("unresolvedGuids", [])),
        "duplicateGuidCount": len(after.get("scan", {}).get("duplicateGuids", [])),
        "addedNodes": sorted(new_nodes.keys() - old_nodes.keys()),
        "removedNodes": sorted(old_nodes.keys() - new_nodes.keys()),
        "changedNodes": sorted(k for k in old_nodes.keys() & new_nodes.keys()
                               if old_nodes[k] != new_nodes[k]),
        "addedEdges": [list(key) for key in sorted(new_edges - old_edges)],
        "removedEdges": [list(key) for key in sorted(old_edges - new_edges)],
        "manualReview": [
            "Review ISSUES.md, SYSTEM-OVERVIEW.md, ARCHITECTURE-DIAGRAMS.md and teaching examples when related nodes or edges changed.",
            "Static regeneration does not renew Unity compile, Editor, Play Mode, visual or runtime evidence.",
        ],
    }


def scanner(*args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run([sys.executable, str(ROOT / "Tools" / "ArchitectureAudit" / "scan.py"),
                           "--portable", *args], cwd=ROOT, text=True,
                          encoding="utf-8", errors="replace", capture_output=True)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="CI/read-only consistency check")
    parser.add_argument("--hook", choices=("post-merge", "post-checkout", "post-commit", "post-rewrite"))
    args = parser.parse_args()
    if Path(git("rev-parse", "--show-toplevel")).resolve() != ROOT:
        return fail("Script path and Git project root disagree; no files changed.")
    branch = git("branch", "--show-current")
    if branch != "main" and not args.check:
        print(json.dumps({"ok": True, "action": "skipped", "reason": f"branch {branch or '(detached)'} is not main"}, ensure_ascii=False))
        return 0
    if args.check:
        result = scanner("--check")
        if result.stdout:
            print(result.stdout.strip())
        if result.returncode:
            if result.stderr:
                print(result.stderr.strip(), file=sys.stderr)
            return fail("Committed graph/viewer do not match this source snapshot; run update.ps1 on a clean local main and review the diff.", 1)
        return 0
    if os.environ.get("ARCH_AUDIT_UPDATING") == "1":
        return fail("Nested update suppressed.")
    lock = OUT / ".architecture-update.lock"
    try:
        lock.mkdir()
    except FileExistsError:
        return fail(f"Another update owns {lock}; inspect before removing a stale lock.")
    try:
        # Main snapshots must describe committed inputs. Generated files may be
        # dirty after an earlier hook; never replace those user-visible edits.
        if git("status", "--porcelain=v1", "--untracked-files=all", "--",
               "Assets", "Packages", "ProjectSettings", "Tools/ArchitectureAudit",
               ":(exclude)Tools/ArchitectureAudit/__pycache__/**"):
            return fail("Source or scanner inputs have uncommitted changes on main; commit/review them first, then rerun update.ps1.")
        paths = [str(OUT / name) for name in GENERATED]
        if git("status", "--porcelain=v1", "--untracked-files=all", "--", *paths):
            return fail("Generated files already have local edits or are untracked; review them before rerunning. Nothing was overwritten.")
        result = scanner("--check")
        if result.returncode == 0:
            print(json.dumps({"ok": True, "action": "up-to-date", "branch": branch,
                              "scan": json.loads(result.stdout)}, ensure_ascii=False))
            return 0
        if result.returncode != 1 or not result.stdout:
            return fail("Consistency scan failed: " + result.stderr.strip())
        # os.replace is atomic only on the same volume as the destination.
        with tempfile.TemporaryDirectory(prefix=".architecture-update-stage-", dir=OUT) as temporary:
            stage = Path(temporary)
            rebuilt = scanner("--output-dir", str(stage))
            if rebuilt.returncode:
                return fail("Regeneration failed: " + rebuilt.stderr.strip())
            after = json.loads((stage / "graph.json").read_text(encoding="utf-8"))
            before_path = OUT / "graph.json"
            before = json.loads(before_path.read_text(encoding="utf-8")) if before_path.exists() else None
            report = diff_report(before, after)
            (stage / "GRAPH-CHANGES.json").write_text(
                json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
            if after["sourceBranch"] != "main" or after["sourceDirty"]:
                return fail("Staged graph is not a clean main snapshot.")
            if after.get("scan", {}).get("duplicateGuids"):
                return fail("Staged graph contains duplicate asset GUIDs; review assets before publishing.")
            # Check all staged files before touching the live pair.
            for name in GENERATED:
                if not (stage / name).is_file() or not (stage / name).stat().st_size:
                    return fail(f"Staged {name} is missing or empty.")
            if 'const graph=' not in (stage / "viewer.html").read_text(encoding="utf-8"):
                return fail("Staged viewer does not contain embedded graph data.")
            backups = stage / "backups"
            backups.mkdir()
            replaced: list[str] = []
            try:
                for name in GENERATED:
                    target = OUT / name
                    if target.exists():
                        shutil.copy2(target, backups / name)
                    os.replace(stage / name, target)
                    replaced.append(name)
            except OSError:
                for name in reversed(replaced):
                    target = OUT / name
                    backup = backups / name
                    if backup.exists():
                        os.replace(backup, target)
                    else:
                        target.unlink(missing_ok=True)
                raise
            print(json.dumps({"ok": True, "action": "updated", "branch": branch,
                              "nodes": len(after["nodes"]), "edges": len(after["edges"]),
                              "addedNodes": len(report["addedNodes"]),
                              "removedNodes": len(report["removedNodes"]),
                              "addedEdges": len(report["addedEdges"]),
                              "removedEdges": len(report["removedEdges"]),
                              "unresolvedGuids": report["unresolvedGuidCount"],
                              "report": str(OUT / "GRAPH-CHANGES.json"),
                              "manualReviewRequired": True}, ensure_ascii=False))
            return 0
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        return fail(str(error))
    finally:
        lock.rmdir()


if __name__ == "__main__":
    raise SystemExit(main())
