#!/usr/bin/env python3
"""Small, local evidence gate for RPG_Demo tasks. No Unity or Git writes."""

from __future__ import annotations

import argparse
import fnmatch
import hashlib
import json
import os
import re
import subprocess
import sys
import time
import uuid
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path


SCHEMA = 2
RISK_LEVELS = ("L0", "L1", "L2", "L3")
STATUSES = ("PASS", "FAIL", "BLOCKED", "TIMEOUT", "NOT_RUN", "UNKNOWN", "PARTIAL")


def git(repo: Path, *args: str) -> str:
    result = subprocess.run(["git", "-C", str(repo), *args], capture_output=True)
    if result.returncode:
        raise RuntimeError(result.stderr.decode("utf-8", "replace").strip() or "git failed")
    return result.stdout.decode("utf-8", "surrogateescape").strip()


def git_paths(repo: Path, *args: str) -> set[str]:
    result = subprocess.run(["git", "-C", str(repo), *args], capture_output=True)
    if result.returncode:
        raise RuntimeError(result.stderr.decode("utf-8", "replace").strip() or "git failed")
    return {p.decode("utf-8", "surrogateescape").replace("\\", "/")
            for p in result.stdout.split(b"\0") if p}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def dirty_state(repo: Path) -> dict[str, str | None]:
    tracked = git_paths(repo, "diff", "--name-only", "--no-renames", "-z", "HEAD", "--")
    untracked = git_paths(repo, "ls-files", "--others", "--exclude-standard", "-z", "--")
    result: dict[str, str | None] = {}
    for name in sorted(tracked | untracked):
        if name.startswith((".codex/", "learn/", "Tools/EngineeringAssurance/.local/",
                            "Tools/EngineeringAssurance/.test-fixtures/")):
            continue
        path = repo / name
        if path.is_symlink():
            result[name] = "link:" + hashlib.sha256(os.readlink(path).encode("utf-8", "surrogateescape")).hexdigest()
        else:
            result[name] = sha256(path) if path.is_file() else None
    return result


def source_fingerprint(head: str, dirty: dict[str, str | None]) -> str:
    payload = json.dumps({"head": head, "dirty": dirty}, sort_keys=True,
                         ensure_ascii=False, separators=(",", ":"))
    return hashlib.sha256(payload.encode("utf-8", "surrogateescape")).hexdigest()


def repo_identity(repo: Path) -> tuple[str, str, Path]:
    root = Path(git(repo, "rev-parse", "--show-toplevel")).resolve()
    return git(root, "branch", "--show-current"), git(root, "rev-parse", "HEAD"), root


def confined_path(repo: Path, value: str) -> Path:
    path = Path(value)
    resolved = (path if path.is_absolute() else repo / path).resolve()
    if not resolved.is_relative_to(repo):
        raise ValueError(f"Path outside project: {value}")
    return resolved


def write_new(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x", encoding="utf-8", newline="\n") as output:
        json.dump(payload, output, ensure_ascii=False, indent=2)
        output.write("\n")


def task_paths(repo: Path, task: str) -> tuple[Path, Path]:
    directory = repo / "Tools" / "EngineeringAssurance" / ".local" / "tasks"
    return directory / f"{task}.baseline.json", directory / f"{task}.manifest.json"


def checked_manifest(repo: Path, baseline: dict, supplied: Path) -> tuple[Path, dict]:
    canonical, manifest_path = task_paths(repo, baseline["taskId"])
    if not canonical.is_file() or not manifest_path.is_file():
        raise ValueError("Task baseline or manifest is missing; do not rebaseline this task")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    digest = sha256(canonical)
    if (manifest.get("taskId") != baseline["taskId"] or
            manifest.get("baselineId") != baseline.get("baselineId") or
            manifest.get("baselineSha256") != digest or sha256(supplied) != digest):
        raise ValueError("Task baseline identity or content differs from the first preflight")
    for previous in manifest.get("reports", []):
        path = confined_path(repo, previous["path"])
        if not path.is_file() or sha256(path) != previous["sha256"]:
            raise ValueError(f"Previous verification report missing or changed: {path}")
    return manifest_path, manifest


def replace_json(path: Path, payload: dict) -> None:
    temporary = path.with_name(path.name + "." + uuid.uuid4().hex + ".tmp")
    try:
        with temporary.open("x", encoding="utf-8", newline="\n") as output:
            json.dump(payload, output, ensure_ascii=False, indent=2)
            output.write("\n")
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def matches_scope(path: str, scope: list[str]) -> bool:
    return any(path == pattern or (pattern.endswith("/") and path.startswith(pattern))
               or fnmatch.fnmatchcase(path, pattern) for pattern in scope)


def architecture_check(repo: Path) -> dict:
    script = repo / "Tools/ArchitectureAudit/scan.py"
    if not script.is_file():
        return {"status": "UNKNOWN", "reason": "Existing architecture scanner unavailable"}
    try:
        run = subprocess.run([sys.executable, str(script), "--check"], cwd=repo,
                             capture_output=True, text=True, timeout=90)
    except subprocess.TimeoutExpired:
        return {"status": "TIMEOUT", "reason": "Architecture scan exceeded 90 seconds"}
    except OSError as error:
        return {"status": "UNKNOWN", "reason": f"Architecture scanner could not start: {error}"}
    output = (run.stdout + "\n" + run.stderr).strip()
    return {"status": "PASS" if run.returncode == 0 else "BLOCKED",
            "exitCode": run.returncode, "detail": output[-1600:]}


def preflight(args: argparse.Namespace) -> int:
    start = time.monotonic()
    branch, head, repo = repo_identity(args.repo.resolve())
    if args.risk not in RISK_LEVELS:
        raise ValueError("Unknown risk level")
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]{0,63}", args.task):
        raise ValueError("Task ID must use 1-64 ASCII letters, digits, underscores or hyphens")
    if not args.scope:
        raise ValueError("At least one authorized --scope is required")
    scope = [item.replace("\\", "/") for item in args.scope]
    if any(item.startswith("/") or ":" in item or ".." in item.split("/") or item in (".", "*")
           or item.startswith((".git/", ".codex/", "learn/")) for item in scope):
        raise ValueError("Scope must be a bounded project-relative path or glob")
    canonical, manifest_path = task_paths(repo, args.task)
    output = confined_path(repo, args.out) if args.out else canonical
    if canonical.exists() or manifest_path.exists() or output.exists():
        raise ValueError("Task ID or preflight output already exists; verify the original baseline")
    dirty = dirty_state(repo)
    record = {
        "schemaVersion": SCHEMA, "taskId": args.task, "baselineId": uuid.uuid4().hex,
        "createdAtUtc": datetime.now(timezone.utc).isoformat(),
        "projectRoot": str(repo), "branch": branch, "baseCommit": head,
        "expectedBranch": args.expected_branch,
        "branchStatus": "PASS" if not args.expected_branch or branch == args.expected_branch else "BLOCKED",
        "riskLevel": args.risk, "intent": args.intent, "authorizedScope": scope,
        "dirtyBaseline": dirty, "sourceFingerprint": source_fingerprint(head, dirty),
        "architectureCheck": architecture_check(repo) if args.risk in ("L2", "L3")
        else {"status": "NOT_RUN", "reason": "L0/L1 bounded preflight"},
    }
    record["elapsedMs"] = round((time.monotonic() - start) * 1000)
    write_new(canonical, record)
    write_new(manifest_path, {"taskId": args.task, "baselineId": record["baselineId"],
                              "baselineSha256": sha256(canonical), "reports": []})
    if output != canonical:
        write_new(output, record)
    print(json.dumps({"preflight": str(output), "branch": branch, "branchStatus": record["branchStatus"], "head": head,
                      "dirtyPaths": len(dirty), "architecture": record["architectureCheck"]["status"],
                      "elapsedMs": record["elapsedMs"]}, ensure_ascii=False))
    architecture_blocked = args.risk in ("L2", "L3") and record["architectureCheck"]["status"] != "PASS"
    return 2 if record["branchStatus"] == "BLOCKED" or architecture_blocked else 0


def check_nunit(path: Path) -> tuple[str, str]:
    try:
        root = ET.parse(path).getroot()
        if root.tag != "test-run":
            return "FAIL", "not a NUnit test-run"
        passed = int(root.attrib.get("passed", "0"))
        failed = int(root.attrib.get("failed", "0"))
        total = int(root.attrib.get("total", "0"))
        skipped = int(root.attrib.get("skipped", "0"))
        inconclusive = int(root.attrib.get("inconclusive", "0"))
        result = root.attrib.get("result", "")
        cases = list(root.iter("test-case"))
        if (result == "Passed" and total > 0 and passed == total and failed == 0
                and skipped == 0 and inconclusive == 0 and len(cases) == total
                and all(case.attrib.get("result") == "Passed" for case in cases)):
            return "PASS", f"NUnit passed={passed} failed={failed}"
        return "FAIL", f"NUnit inconsistent result={result} total={total} passed={passed} failed={failed} cases={len(cases)}"
    except (OSError, ET.ParseError, ValueError) as error:
        return "FAIL", f"invalid NUnit XML: {error}"


def verify_test(repo: Path, test: dict, fingerprint: str) -> dict:
    name = str(test.get("name", "unnamed"))
    declared = test.get("status", "UNKNOWN")
    if declared not in STATUSES:
        return {"name": name, "status": "FAIL", "reason": "unknown declared status"}
    if declared != "PASS":
        return {"name": name, "status": declared, "reason": "no PASS claim checked"}
    if test.get("sourceFingerprint") != fingerprint:
        return {"name": name, "status": "BLOCKED", "reason": "tested source fingerprint differs"}
    try:
        runner_path = confined_path(repo, str(test["runnerEvidence"]))
        runner = json.loads(runner_path.read_text(encoding="utf-8"))
        if not isinstance(runner, dict):
            raise ValueError("runner evidence must be a JSON object")
        quality = runner.get("quality") or {}
        if not isinstance(quality, dict):
            raise ValueError("runner quality must be a JSON object")
        if runner.get("timedOut") is True or runner.get("stopReason") in ("TIMEOUT", "EDITOR_EXIT_TIMEOUT_AFTER_XML"):
            return {"name": name, "status": "TIMEOUT", "reason": "runner timed out", "runnerEvidence": str(runner_path)}
        if runner.get("timedOut") is not False:
            raise ValueError("runner evidence lacks explicit non-timeout status")
        if runner.get("stopReason") not in (None, "COMPLETED", "EXIT_0", "NATURAL_EXIT"):
            return {"name": name, "status": "FAIL", "reason": "runner stop reason contradicts PASS"}
        if runner.get("exitCode") != 0 or quality.get("unityExit", "EXIT_0") != "EXIT_0" or (
            quality and quality.get("overall") != "PASS"):
            return {"name": name, "status": "FAIL", "reason": "runner evidence does not show natural exit 0",
                    "runnerEvidence": str(runner_path)}
        xml = confined_path(repo, str(test["xml"]))
        status, reason = check_nunit(xml)
        return {"name": name, "status": status, "reason": reason,
                "xml": str(xml), "xmlSha256": sha256(xml) if xml.is_file() else None,
                "runnerEvidence": str(runner_path), "runnerSha256": sha256(runner_path)}
    except (KeyError, ValueError, OSError, json.JSONDecodeError) as error:
        return {"name": name, "status": "FAIL", "reason": str(error)}


def verify(args: argparse.Namespace) -> int:
    start = time.monotonic()
    baseline = json.loads(args.baseline.read_text(encoding="utf-8"))
    record = json.loads(args.record.read_text(encoding="utf-8"))
    branch, head, repo = repo_identity(args.repo.resolve())
    if baseline.get("schemaVersion") != SCHEMA or baseline.get("projectRoot") != str(repo):
        raise ValueError("Preflight schema or project root differs")
    manifest_path, manifest = checked_manifest(repo, baseline, args.baseline.resolve())
    if record.get("taskId") != baseline.get("taskId") or record.get("baselineId") != baseline.get("baselineId"):
        raise ValueError("Task ID or baseline ID differs from preflight")
    if "authorizedScope" in record and record["authorizedScope"] != baseline["authorizedScope"]:
        raise ValueError("Record attempts to change the authorized scope")
    dirty = dirty_state(repo)
    original = baseline["dirtyBaseline"]
    absent = object()
    changed = sorted(name for name in set(original) | set(dirty)
                     if original.get(name, absent) != dirty.get(name, absent))
    outside = [name for name in changed if not matches_scope(name, baseline["authorizedScope"])]
    historical_outside = sorted({path for previous in manifest["reports"]
        for path in json.loads(confined_path(repo, previous["path"]).read_text(encoding="utf-8"))
            .get("outsideAuthorizedScope", [])})
    identity_status = "PASS" if branch == baseline["branch"] and head == baseline["baseCommit"] else "BLOCKED"
    fingerprint = source_fingerprint(head, dirty)
    tests = [verify_test(repo, item, fingerprint) for item in record.get("tests", [])]
    if not tests:
        verification = "NOT_RUN"
    elif all(item["status"] == "PASS" for item in tests):
        verification = "PASS"
    elif any(item["status"] == "TIMEOUT" for item in tests):
        verification = "TIMEOUT"
    elif any(item["status"] == "FAIL" for item in tests):
        verification = "FAIL"
    elif any(item["status"] == "BLOCKED" for item in tests):
        verification = "BLOCKED"
    else:
        verification = "PARTIAL"
    human = record.get("humanAcceptance", {"status": "NOT_RUN"})
    human_status = human.get("status", "UNKNOWN")
    # Agent-authored files cannot authenticate the named human.
    if human_status == "PASS":
        human_status = "UNKNOWN"
    approval = record.get("humanApproval", {})
    authorization_status = "UNKNOWN"
    evidence_paths = sorted({path for item in tests for path in (item.get("xml"), item.get("runnerEvidence")) if path})
    architecture_status = baseline.get("architectureCheck", {}).get("status", "UNKNOWN")
    preflight_blocked = baseline.get("branchStatus") == "BLOCKED" or (
        baseline["riskLevel"] in ("L2", "L3") and architecture_status != "PASS") or (
        baseline["riskLevel"] == "L3")
    result = {
        "schemaVersion": SCHEMA, "taskId": baseline["taskId"],
        "checkedAtUtc": datetime.now(timezone.utc).isoformat(), "projectRoot": str(repo),
        "branch": branch, "baseCommit": baseline["baseCommit"], "resultCommit": head,
        "riskLevel": baseline["riskLevel"], "sourceFingerprint": fingerprint,
        "architectureStatus": architecture_status, "humanApproval": authorization_status,
        "humanApprovalClaim": approval if isinstance(approval, dict) else {},
        "humanApprover": None,
        "evidencePaths": sorted(set(evidence_paths)),
        "identityStatus": identity_status, "scopeStatus": "FAIL" if outside or historical_outside else "PASS",
        "filesChangedSincePreflight": changed, "outsideAuthorizedScope": outside,
        "historicalScopeViolations": historical_outside,
        "existingCapabilitiesReused": record.get("existingCapabilitiesReused", []),
        "architectureImpact": record.get("architectureImpact", "UNKNOWN"),
        "tests": tests, "deterministicVerification": verification,
        "implementerSelfCheck": record.get("implementerSelfCheck", "UNKNOWN"),
        "independentReview": record.get("independentReview", "NOT_RUN"),
        "humanAcceptance": human_status, "unverifiedItems": record.get("unverifiedItems", []),
        "remainingRisks": record.get("remainingRisks", []),
        "overall": "BLOCKED" if identity_status != "PASS" or preflight_blocked or verification == "BLOCKED" else
                   "FAIL" if outside or historical_outside or verification == "FAIL" else
                   "TIMEOUT" if verification == "TIMEOUT" else "PARTIAL",
    }
    result["elapsedMs"] = round((time.monotonic() - start) * 1000)
    output = confined_path(repo, args.out or
        f"Tools/EngineeringAssurance/.local/tasks/{baseline['taskId']}.verification-{uuid.uuid4().hex}.json")
    write_new(output, result)
    manifest["reports"].append({"path": output.relative_to(repo).as_posix(), "sha256": sha256(output)})
    replace_json(manifest_path, manifest)
    print(json.dumps({"report": str(output), "identity": identity_status,
                      "scope": result["scopeStatus"], "deterministicVerification": verification,
                      "overall": result["overall"], "elapsedMs": result["elapsedMs"]}, ensure_ascii=False))
    return 2 if result["overall"] in ("BLOCKED", "FAIL", "TIMEOUT") else 0


def fingerprint(args: argparse.Namespace) -> int:
    _, head, repo = repo_identity(args.repo.resolve())
    dirty = dirty_state(repo)
    print(json.dumps({"head": head, "dirtyPaths": len(dirty),
                      "sourceFingerprint": source_fingerprint(head, dirty)}, ensure_ascii=False))
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    commands = parser.add_subparsers(dest="command", required=True)
    pre = commands.add_parser("preflight")
    pre.add_argument("--task", required=True)
    pre.add_argument("--risk", choices=RISK_LEVELS, required=True)
    pre.add_argument("--expected-branch")
    pre.add_argument("--intent", required=True)
    pre.add_argument("--scope", action="append", required=True)
    pre.add_argument("--out")
    pre.set_defaults(run=preflight)
    check = commands.add_parser("verify")
    check.add_argument("--baseline", type=Path, required=True)
    check.add_argument("--record", type=Path, required=True)
    check.add_argument("--out")
    check.set_defaults(run=verify)
    stamp = commands.add_parser("fingerprint")
    stamp.set_defaults(run=fingerprint)
    args = parser.parse_args()
    try:
        return args.run(args)
    except (OSError, RuntimeError, ValueError, json.JSONDecodeError) as error:
        print(f"assurance error: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
