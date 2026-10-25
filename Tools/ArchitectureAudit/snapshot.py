"""Capture and verify an immutable-input, static architecture scan copy.

The copy is deliberately not a Unity project: binary assets become presence
markers because scan.py reads their .meta files, not their bodies. Never run
Unity or DataTest against it.
"""

from __future__ import annotations

import argparse
import fnmatch
import hashlib
import json
import os
import shutil
import subprocess
import sys
import uuid
from datetime import datetime, timezone
from pathlib import Path

from scan import FINGERPRINT_EXTENSIONS, TRANSIENT_TEST_SCENE_RE

SCAN_FILES = ("scan.py", "viewer-template.html", "package-guid-map.json",
              "chain-relations.json", "curated-relations.json", "node-notes.json", "issues.json")


def digest(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def git(repo: Path, *args: str) -> str:
    result = subprocess.run(["git", "-C", str(repo), *args], capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError(result.stderr.strip() or "git failed")
    return result.stdout.strip()


def git_paths(repo: Path, *args: str) -> set[str]:
    result = subprocess.run(["git", "-C", str(repo), *args], capture_output=True)
    if result.returncode:
        raise RuntimeError(result.stderr.decode("utf-8", "replace").strip() or "git failed")
    return {part.decode("utf-8", "surrogateescape").replace("\\", "/")
            for part in result.stdout.split(b"\0") if part}


def dirty(repo: Path) -> dict[str, str | None]:
    changed = git_paths(repo, "diff", "--name-only", "--no-renames", "-z", "HEAD", "--")
    changed |= git_paths(repo, "ls-files", "--others", "--exclude-standard", "-z", "--")
    result = {}
    for name in sorted(changed):
        if name.startswith((".codex/", "learn/", "Tools/EngineeringAssurance/.local/",
                            "Tools/EngineeringAssurance/.test-fixtures/")):
            continue
        path = repo / name
        if path.is_symlink():
            result[name] = "link:" + hashlib.sha256(os.readlink(path).encode()).hexdigest()
        else:
            result[name] = digest(path) if path.is_file() else None
    return result


def inventory(repo: Path) -> dict[str, dict]:
    """List every byte read by portable scan, plus asset-presence markers."""
    result: dict[str, dict] = {}

    def include(path: Path, marker: bool = False) -> None:
        if path.is_symlink():
            raise ValueError(f"Snapshot input symlink is unsupported: {path}")
        if not path.is_file():
            raise ValueError(f"Snapshot input missing: {path}")
        name = path.relative_to(repo).as_posix()
        result[name] = {"mode": "presence" if marker else "content",
                        "sha256": None if marker else digest(path)}

    assets = repo / "Assets"
    if not assets.is_dir():
        raise ValueError("Assets directory missing")
    for meta in sorted(assets.rglob("*.meta")):
        asset = Path(str(meta)[:-5])
        name = asset.relative_to(repo).as_posix()
        if TRANSIENT_TEST_SCENE_RE.fullmatch(name):
            continue
        if asset.is_dir():
            continue
        if not asset.is_file():
            raise ValueError(f"Asset for .meta is missing: {asset}")
        include(meta)
        include(asset, asset.suffix.lower() not in FINGERPRINT_EXTENSIONS)
    for directory in ("Packages", "ProjectSettings"):
        folder = repo / directory
        if folder.is_dir():
            for path in sorted(p for p in folder.rglob("*") if p.is_file()):
                include(path)
    tool_dir = repo / "Tools" / "ArchitectureAudit"
    for name in SCAN_FILES:
        path = tool_dir / name
        if path.is_file() or name in SCAN_FILES[:3]:
            include(path)
    return result


def identity(repo: Path) -> dict:
    return {"branch": git(repo, "branch", "--show-current"),
            "head": git(repo, "rev-parse", "HEAD")}


def write_json(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x", encoding="utf-8", newline="\n") as out:
        json.dump(data, out, ensure_ascii=False, indent=2)
        out.write("\n")


def run_scan(project: Path, report_dir: Path, check: bool = False) -> dict:
    command = [sys.executable, str(project / "Tools/ArchitectureAudit/scan.py"),
               "--portable", "--source-context", str(project / "source-context.json"),
               "--output-dir", str(report_dir)]
    if check:
        command.append("--check")
    try:
        run = subprocess.run(command, cwd=project, capture_output=True, text=True, timeout=180)
        return {"command": command, "exitCode": run.returncode,
                "stdout": run.stdout, "stderr": run.stderr}
    except (OSError, subprocess.TimeoutExpired) as error:
        return {"command": command, "exitCode": None, "error": str(error)}


def create(args: argparse.Namespace) -> int:
    repo = args.repo.resolve()
    if Path(git(repo, "rev-parse", "--show-toplevel")).resolve() != repo:
        raise ValueError("--repo must be the Git worktree root")
    out_root = (args.out_root or repo / "Tools/EngineeringAssurance/.local/snapshots").resolve()
    if out_root == repo or any(out_root.is_relative_to(repo / name) for name in
                               ("Assets", "Packages", "ProjectSettings", "Tools/ArchitectureAudit")):
        raise ValueError("Snapshot output must be outside scanner input directories")
    snapshot_id = uuid.uuid4().hex
    folder = out_root / snapshot_id
    project = folder / "project"
    project.mkdir(parents=True, exist_ok=False)
    before_identity = identity(repo)
    before = inventory(repo)
    before_dirty = dirty(repo)
    untracked = git_paths(repo, "ls-files", "--others", "--exclude-standard", "-z", "--")
    for name, entry in before.items():
        destination = project / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        if entry["mode"] == "presence":
            destination.touch(exist_ok=False)
        else:
            shutil.copyfile(repo / name, destination)
            if digest(destination) != entry["sha256"]:
                raise ValueError(f"Copied input differs from inventory: {name}")
    after = inventory(repo)
    after_identity = identity(repo)
    after_dirty = dirty(repo)
    changed_worktree_during_capture = sorted(
        name for name in set(before_dirty) | set(after_dirty)
        if before_dirty.get(name, "<absent>") != after_dirty.get(name, "<absent>"))
    changed_captured_provenance = [name for name in changed_worktree_during_capture
                                   if name in before or name in after]
    manifest = {"schemaVersion": 1, "snapshotId": snapshot_id,
                "createdAtUtc": datetime.now(timezone.utc).isoformat(),
                "creationMethod": "static scan input copy, portable scanner; binary asset presence markers",
                "sourceRoot": str(repo), "sourceIdentity": before_identity,
                "inputs": before, "sourceDirtyAtCapture": before_dirty,
                "untrackedAtCapture": sorted(name for name in before_dirty if name in untracked),
                "trackedChangedAtCapture": sorted(name for name in before_dirty if name not in untracked),
                "parallelWorktreeChangesDuringCapture": changed_worktree_during_capture,
                "authorizedScope": args.scope or [], "captureToolSha256": digest(Path(__file__)),
                "reports": {}, "testReports": []}
    for item in getattr(args, "test_report", None) or []:
        path = Path(item).resolve()
        if not path.is_file():
            raise ValueError(f"Test report missing: {path}")
        manifest["testReports"].append({"path": str(path), "sha256": digest(path),
                                        "status": "RECORDED_NOT_VERSION_BOUND"})
    if before != after or before_identity != after_identity or changed_captured_provenance:
        manifest["captureStatus"] = "RACE_DETECTED"
        manifest["changedInputsDuringCapture"] = sorted(
            name for name in set(before) | set(after) if before.get(name) != after.get(name))
        manifest["captureRaceEvidence"] = {
            name: {"before": before.get(name), "after": after.get(name)}
            for name in manifest["changedInputsDuringCapture"]}
        manifest["identityChangedDuringCapture"] = before_identity != after_identity
        manifest["identityAfterCapture"] = after_identity
        write_json(folder / "manifest.json", manifest)
        print(json.dumps({"snapshotId": snapshot_id, "snapshotVerification": "BLOCKED",
                          "reason": "Source changed during capture", "manifest": str(folder / "manifest.json")},
                         ensure_ascii=False))
        return 2
    context = {**before_identity, "snapshotId": snapshot_id,
               "sourceDirty": any(name in before for name in before_dirty)}
    write_json(project / "source-context.json", context)
    run = run_scan(project, folder / "reports")
    write_json(folder / "reports" / "scan-run.json", run)
    manifest["captureStatus"] = "PASS" if run["exitCode"] == 0 else "SCAN_FAILED"
    manifest["sourceContextSha256"] = digest(project / "source-context.json")
    for name in ("graph.json", "viewer.html", "scan-run.json"):
        path = folder / "reports" / name
        manifest["reports"][name] = digest(path) if path.is_file() else None
    write_json(folder / "manifest.json", manifest)
    print(json.dumps({"snapshotId": snapshot_id, "snapshotVerification": manifest["captureStatus"],
                      "inputCount": len(before), "scanExitCode": run["exitCode"],
                      "manifest": str(folder / "manifest.json")}, ensure_ascii=False))
    return 0 if manifest["captureStatus"] == "PASS" else 2


def matches(path: str, scope: list[str]) -> bool:
    return any(path == pattern or (pattern.endswith("/") and path.startswith(pattern))
               or fnmatch.fnmatchcase(path, pattern) for pattern in scope)


def verify(args: argparse.Namespace) -> int:
    folder = args.snapshot.resolve()
    manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))
    project = folder / "project"
    repo = Path(manifest["sourceRoot"])
    inputs = manifest["inputs"]
    fixed_errors = []
    if manifest.get("captureStatus") != "PASS":
        fixed_errors.append("capture did not pass")
    capture_tool_changed = digest(Path(__file__)) != manifest.get("captureToolSha256")
    context = project / "source-context.json"
    if not context.is_file() or digest(context) != manifest.get("sourceContextSha256"):
        fixed_errors.append("source context differs")
    for name, entry in inputs.items():
        path = project / name
        if (not path.is_file()
                or (entry["mode"] == "content" and digest(path) != entry["sha256"])
                or (entry["mode"] == "presence" and path.stat().st_size != 0)):
            fixed_errors.append(name)
    for name, expected in manifest["reports"].items():
        path = folder / "reports" / name
        if not expected or not path.is_file() or digest(path) != expected:
            fixed_errors.append("report:" + name)
    scan = None if fixed_errors else run_scan(project, folder / "reports", check=True)
    if scan and scan["exitCode"] != 0:
        fixed_errors.append("scan --check failed")
    test_evidence_errors = []
    for item in manifest.get("testReports", []):
        path = Path(item["path"])
        if not path.is_file() or digest(path) != item["sha256"]:
            test_evidence_errors.append(str(path))
    live_error = None
    current_identity = None
    changed_inputs = []
    changed_worktree = []
    previous_dirty = manifest["sourceDirtyAtCapture"]
    current_dirty = {}
    try:
        current_identity = identity(repo)
        current = inventory(repo)
        changed_inputs = sorted(name for name in set(inputs) | set(current)
                                if inputs.get(name) != current.get(name))
        current_dirty = dirty(repo)
        changed_worktree = sorted(name for name in set(previous_dirty) | set(current_dirty)
                                  if previous_dirty.get(name, "<absent>") != current_dirty.get(name, "<absent>"))
    except (OSError, RuntimeError, ValueError) as error:
        live_error = str(error)
    live_status = ("UNKNOWN" if live_error else "PASS" if current_identity == manifest["sourceIdentity"]
                   and not changed_inputs else "STALE")
    outside = [name for name in changed_worktree if not matches(name, manifest["authorizedScope"])]
    report = {"snapshotId": manifest["snapshotId"], "checkedAtUtc": datetime.now(timezone.utc).isoformat(),
              "SNAPSHOT_VERIFICATION": "PASS" if not fixed_errors else "BLOCKED",
              "snapshotErrors": fixed_errors, "captureToolChangedSinceSnapshot": capture_tool_changed,
              "scanCheck": scan,
              "TEST_EVIDENCE_LINK": ("NOT_RUN" if not manifest.get("testReports") else
                                     "PASS" if not test_evidence_errors else "PARTIAL"),
              "testEvidenceErrors": test_evidence_errors,
              "testEvidenceLimit": "Log hashes associate reports; they do not prove tests used these exact snapshot inputs",
              "LIVE_WORKTREE_FRESHNESS": live_status, "liveError": live_error,
              "changedScanInputs": changed_inputs,
              "identityAtCapture": manifest["sourceIdentity"], "identityNow": current_identity,
              "CHANGE_ATTRIBUTION": "UNKNOWN" if changed_worktree or live_error else "NO_CHANGE_OBSERVED",
              "changedWorktree": [{"path": name, "atCapture": previous_dirty.get(name),
                                   "now": current_dirty.get(name)} for name in changed_worktree],
              "outsideAuthorizedScope": outside,
              "attributionLimit": "File hashes show changes, not the human or agent that made them"}
    report_path = folder / "reports" / ("verification-" + uuid.uuid4().hex + ".json")
    write_json(report_path, report)
    print(json.dumps({"snapshotId": manifest["snapshotId"],
                      "SNAPSHOT_VERIFICATION": report["SNAPSHOT_VERIFICATION"],
                      "LIVE_WORKTREE_FRESHNESS": live_status,
                      "CHANGE_ATTRIBUTION": report["CHANGE_ATTRIBUTION"],
                      "changedScanInputs": len(changed_inputs), "outsideScope": len(outside),
                      "report": str(report_path)}, ensure_ascii=False))
    return 0 if not fixed_errors else 2


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    capture = commands.add_parser("capture")
    capture.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    capture.add_argument("--out-root", type=Path)
    capture.add_argument("--scope", action="append")
    capture.add_argument("--test-report", action="append", type=Path,
                         help="Associate an existing test log by hash without claiming version equivalence")
    capture.set_defaults(run=create)
    check = commands.add_parser("verify")
    check.add_argument("--snapshot", type=Path, required=True)
    check.set_defaults(run=verify)
    args = parser.parse_args()
    try:
        return args.run(args)
    except (OSError, RuntimeError, ValueError, json.JSONDecodeError) as error:
        print(f"snapshot error: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
