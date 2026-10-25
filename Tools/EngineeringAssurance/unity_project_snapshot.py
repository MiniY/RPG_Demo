#!/usr/bin/env python3
"""Make a byte-complete Unity source copy; never copy Library or start Unity.

This complements ArchitectureAudit/snapshot.py, whose binary presence markers
are deliberately unsuitable for Unity. It is a preparer, not a test runner.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import sys
import uuid
from datetime import datetime, timezone
from pathlib import Path

from assure import dirty_state, git_paths, repo_identity, sha256, write_new


SOURCE_DIRS = ("Assets", "Packages", "ProjectSettings")


def source_inventory(repo: Path) -> dict[str, str]:
    files = {}
    for name in SOURCE_DIRS:
        folder = repo / name
        if not folder.is_dir() or folder.is_symlink():
            raise ValueError(f"Required Unity directory unavailable: {folder}")
        for base, directories, names in os.walk(folder, followlinks=False):
            for item in directories:
                if (Path(base) / item).is_symlink():
                    raise ValueError(f"Junction/symlink in Unity input: {Path(base) / item}")
            for item in names:
                path = Path(base) / item
                if path.is_symlink() or not path.is_file():
                    raise ValueError(f"Unsupported Unity input: {path}")
                files[path.relative_to(repo).as_posix()] = sha256(path)
    for needed in ("Packages/manifest.json", "ProjectSettings/ProjectVersion.txt"):
        if needed not in files:
            raise ValueError(f"Required Unity file missing: {needed}")
    return dict(sorted(files.items()))


def fingerprint(files: dict[str, str]) -> str:
    encoded = json.dumps(files, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def create(repo: Path, out_root: Path) -> dict:
    repo = repo.resolve()
    branch, head, actual = repo_identity(repo)
    if actual != repo:
        raise ValueError("--repo must be Git worktree root")
    out_root = out_root.resolve()
    if out_root == repo or any(out_root.is_relative_to(repo / name) for name in SOURCE_DIRS):
        raise ValueError("Output cannot be inside Unity input folders")
    before = source_inventory(repo)
    dirty_before = dirty_state(repo)
    untracked = git_paths(repo, "ls-files", "--others", "--exclude-standard", "-z", "--")
    snapshot_id = uuid.uuid4().hex
    folder = out_root / snapshot_id
    project = folder / "project"
    project.mkdir(parents=True, exist_ok=False)
    for name, expected in before.items():
        dest = project / name
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(repo / name, dest)
        if sha256(dest) != expected:
            raise ValueError(f"Copied file differs from pre-copy input: {name}")
    after = source_inventory(repo)
    branch_after, head_after, _ = repo_identity(repo)
    dirty_after = dirty_state(repo)
    changed_worktree = sorted(path for path in set(dirty_before) | set(dirty_after)
                              if dirty_before.get(path, "<absent>") != dirty_after.get(path, "<absent>"))
    changed_inputs = sorted(path for path in set(before) | set(after)
                            if before.get(path) != after.get(path))
    manifest = {
        "schemaVersion": 1, "projectSnapshotId": snapshot_id,
        "createdAtUtc": datetime.now(timezone.utc).isoformat(),
        "creationMethod": "byte-for-byte Assets, Packages, ProjectSettings copy; no Library",
        "sourceRoot": str(repo), "project": str(project),
        "sourceBranch": branch, "sourceHead": head,
        "sourceDirtyAtCapture": dirty_before,
        "untrackedAtCapture": sorted(untracked & before.keys()),
        "trackedChangedAtCapture": sorted((dirty_before.keys() - untracked) & before.keys()),
        "files": before, "sourceFingerprint": fingerprint(before),
        "unityVersion": (project / "ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8-sig").strip(),
        "captureStatus": "PASS" if before == after and (branch, head) == (branch_after, head_after) else "RACE_DETECTED",
        "changedInputsDuringCapture": changed_inputs,
        "changedWorktreeDuringCapture": changed_worktree,
        "sourceIdentityAfterCapture": {"branch": branch_after, "head": head_after},
        "tests": [], "testResult": "NOT_RUN",
    }
    write_new(folder / "manifest.json", manifest)
    return {"projectSnapshotId": snapshot_id, "captureStatus": manifest["captureStatus"],
            "fileCount": len(before), "sourceFingerprint": manifest["sourceFingerprint"],
            "manifest": str(folder / "manifest.json"), "project": str(project),
            "changedInputsDuringCapture": changed_inputs}


def verify(folder: Path) -> dict:
    folder = folder.resolve()
    manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))
    project = folder / "project"
    copied = source_inventory(project)
    repo = Path(manifest["sourceRoot"])
    live = source_inventory(repo)
    branch, head, _ = repo_identity(repo)
    changed_copy = sorted(path for path in set(manifest["files"]) | set(copied)
                          if manifest["files"].get(path) != copied.get(path))
    changed_live = sorted(path for path in set(manifest["files"]) | set(live)
                          if manifest["files"].get(path) != live.get(path))
    result = {"projectSnapshotId": manifest["projectSnapshotId"],
              "SNAPSHOT_VERIFICATION": "PASS" if manifest["captureStatus"] == "PASS" and not changed_copy else "BLOCKED",
              "LIVE_WORKTREE_FRESHNESS": "PASS" if not changed_live and (branch, head) == (
                  manifest["sourceBranch"], manifest["sourceHead"]) else "STALE",
              "changedCopy": changed_copy, "changedLive": changed_live,
              "sourceIdentityNow": {"branch": branch, "head": head},
              "unityTestResult": manifest["testResult"]}
    path = folder / ("verification-" + uuid.uuid4().hex + ".json")
    write_new(path, result)
    result["report"] = str(path)
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="action", required=True)
    new = commands.add_parser("create")
    new.add_argument("--repo", type=Path, default=Path.cwd())
    new.add_argument("--out-root", type=Path,
                     default=Path(__file__).parent / ".local" / "unity-projects")
    check = commands.add_parser("verify")
    check.add_argument("--snapshot", type=Path, required=True)
    args = parser.parse_args()
    try:
        result = create(args.repo, args.out_root) if args.action == "create" else verify(args.snapshot)
        print(json.dumps(result, ensure_ascii=False))
        return 0 if result.get("captureStatus", result.get("SNAPSHOT_VERIFICATION")) == "PASS" else 2
    except (OSError, ValueError, RuntimeError) as error:
        print(json.dumps({"status": "BLOCKED", "reason": str(error)}, ensure_ascii=False))
        return 2


if __name__ == "__main__":
    sys.exit(main())
