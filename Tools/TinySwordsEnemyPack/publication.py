"""Read-only blob audits. Staging and publication require an exact reviewed allowlist."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

from artwork import ART_ROOT, digest, write_json
from recovery_project import public_files


def git(project, *args):
    return subprocess.check_output(["git", "-C", str(project), *args])


def scan_blob(path, data, restricted_hashes):
    errors = []
    sha = hashlib.sha256(data).hexdigest()
    if sha in restricted_hashes:
        errors.append("EXACT_PAID_SOURCE_PAYLOAD")
    signatures = (b"\x89PNG\r\n\x1a\n", b"\xff\xd8\xff", b"\xabKTX", b"PK\x03\x04")
    if data.startswith((b"MZ", b"RIFF", b"GIF89a", b"GIF87a", b"8BPS", b"DDS ")) or any(marker in data for marker in signatures):
        errors.append("BINARY_OR_IMAGE_SIGNATURE")
    if len(data) > 6 and data[4:6] == b"\xe0\xa5":
        errors.append("ASEPRITE_SIGNATURE")
    try:
        text = data.decode("utf-8-sig")
    except UnicodeDecodeError:
        return sha, errors + ["NON_UTF8_BINARY"]
    if "\0" in text:
        errors.append("BINARY_NUL")
    if re.search(r"data:image/[a-zA-Z0-9.+-]+;base64,", text, re.I):
        errors.append("INLINE_IMAGE_DATA")
    if re.search(r"[A-Za-z0-9+/]{180,}={0,2}", text):
        errors.append("UNREVIEWED_LONG_ENCODED_PAYLOAD")
    if re.search(r"(?:gh[pousr]_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{20,}|sk-[A-Za-z0-9]{24,})", text):
        errors.append("CREDENTIAL_PATTERN")
    if re.search(r"[A-Za-z]:[\\/](?:Users|UnityProjects|A_MyThings|RPG_PrivateAssets)[\\/]", text):
        errors.append("PERSONAL_ABSOLUTE_PATH")
    if re.search(r"^version https://git-lfs.github.com/spec/", text):
        errors.append("LFS_POINTER")
    normalized = path.replace("\\", "/")
    if normalized.startswith(ART_ROOT + "/") and not normalized.endswith(".meta"):
        errors.append("RESTRICTED_ART_DIRECTORY_PAYLOAD")
    if Path(path).suffix.lower() not in (".py", ".cs", ".meta", ".anim", ".controller", ".prefab", ".unity", ".json", ".md") and path != ".gitignore":
        errors.append("UNREVIEWED_FILE_KIND")
    if path.endswith(".meta"):
        if not re.search(r"^guid: [0-9a-f]{32}$", text, re.M):
            errors.append("META_GUID_MISSING")
        if "TextureImporter:" in text and ("spriteMode: 2" not in text or "textureCompression: 0" not in text):
            errors.append("UNEXPECTED_TEXTURE_METADATA")
    return sha, errors


def hashes(plan):
    return {r["sha256"] for r in plan["sourceValidation"]["files"] if r["source"].lower().endswith((".png", ".aseprite"))}


def blobs(project, specs):
    """Read actual object bytes through one Git process, with bounded pipe traffic."""
    process = subprocess.Popen(["git", "-C", str(project), "cat-file", "--batch"],
                               stdin=subprocess.PIPE, stdout=subprocess.PIPE)
    result = []
    try:
        for spec in specs:
            if "\n" in spec or "\r" in spec:
                raise ValueError("INVALID_GIT_OBJECT_SPEC")
            process.stdin.write(spec.encode("utf-8") + b"\n"); process.stdin.flush()
            header = process.stdout.readline().split()
            if len(header) != 3 or header[1] != b"blob":
                raise ValueError("EXPECTED_GIT_BLOB: " + spec)
            size = int(header[2])
            data = process.stdout.read(size)
            if len(data) != size or process.stdout.read(1) != b"\n":
                raise ValueError("TRUNCATED_GIT_BLOB")
            result.append(data)
    finally:
        process.stdin.close()
        process.stdout.close()
        code = process.wait()
    if code:
        raise ValueError("GIT_BLOB_READER_FAILED")
    return result


def classify_diff_check(code, output):
    """Preserve Unity's authoritative empty-scalar formatting, reject everything else."""
    if code == 0:
        return {"status": "PASS", "rawExitCode": 0, "unityEmptyScalars": 0, "otherIssues": []}
    lines = output.splitlines()
    accepted, other = [], []
    for i in range(0, len(lines), 2):
        match = re.fullmatch(r"(.+):(\d+): trailing whitespace\.", lines[i])
        if (match and i+1 < len(lines) and Path(match[1]).suffix in (".meta", ".anim", ".prefab", ".controller", ".unity")
                and re.fullmatch(r"\+\s*[A-Za-z_][A-Za-z0-9_]*: +", lines[i+1])):
            accepted.append({"path": match[1], "line": int(match[2])})
        else:
            other.extend(lines[i:i+2])
    safe = code == 2 and bool(accepted) and not other
    return {"status": "REVIEWED_UNITY_EMPTY_SCALARS" if safe else "FAIL", "rawExitCode": code,
            "unityEmptyScalars": len(accepted), "otherIssues": other,
            "policy": "Only Unity-serialized empty key values may retain a space after colon. Source code/nonempty values/other Git errors fail closed; original metadata bytes are never rewritten for lint."}


def candidates(project, plan):
    rows = []
    for path in public_files(project):
        data = (Path(project) / path).read_bytes()
        sha, errors = scan_blob(path, data, hashes(plan))
        rows.append({"path": path, "sha256": sha, "bytes": len(data), "class": "E_PUBLIC_SAFE" if not errors else "BLOCKED", "errors": errors})
    return {"status": "PASS" if all(not r["errors"] for r in rows) else "FAIL", "files": rows}


def historical(project, plan):
    raw = git(project, "rev-list", "--objects", "--all")
    ids = {}
    for line in raw.splitlines():
        if b" " in line:
            oid, path = line.split(b" ", 1)
            decoded = path.decode("utf-8", "replace")
            if decoded.casefold().endswith((".png", ".aseprite")):
                ids.setdefault(oid, decoded)
    process = subprocess.Popen(["git", "-C", str(project), "cat-file", "--batch"], stdin=subprocess.PIPE, stdout=subprocess.PIPE)
    matches, count, total = [], 0, 0
    restricted = hashes(plan)
    for oid, path in ids.items():
        process.stdin.write(oid + b"\n"); process.stdin.flush()
        head = process.stdout.readline().split()
        size = int(head[2])
        data = process.stdout.read(size)
        process.stdout.read(1)
        if head[1] != b"blob":
            continue
        count += 1; total += size
        if hashlib.sha256(data).hexdigest() in restricted:
            matches.append({"blob": oid.decode(), "path": path})
    process.stdin.close(); process.wait()
    if not count:
        raise ValueError("HISTORICAL_AUDIT_UNEXPECTEDLY_EMPTY")
    return {"status": "PASS" if not matches else "BLOCKED", "historicalMediaBlobs": count, "bytesRead": total,
            "exactPaidSourceMatches": matches, "scope": "All reachable refs, PNG/Aseprite paths. Separate actual staged/all-new-commit blob scans cover the proposed delivery."}


def staged(project, plan, allowlist):
    allowed = {r["path"]:r for r in allowlist["files"]}
    staged_paths = git(project, "diff", "--cached", "--name-only", "-z").decode().rstrip("\0").split("\0")
    staged_paths = [p for p in staged_paths if p]
    if set(staged_paths) != set(allowed):
        raise ValueError("STAGED_PATH_SET_DIFFERS_FROM_ALLOWLIST")
    rows = []
    ordered = sorted(staged_paths)
    for path, data in zip(ordered, blobs(project, [":"+p for p in ordered])):
        sha, errors = scan_blob(path, data, hashes(plan))
        if sha != allowed[path]["sha256"]:
            errors.append("STAGED_BYTES_DIFFER_FROM_REVIEWED_WORKING_FILE")
        rows.append({"path": path, "sha256": sha, "bytes": len(data), "errors": errors})
    diff = subprocess.run(["git", "-C", str(project), "diff", "--cached", "--check"], stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    diff_check = classify_diff_check(diff.returncode, diff.stdout.decode("utf-8", "replace"))
    diff_check["rawDiagnosticSha256"] = hashlib.sha256(diff.stdout).hexdigest()
    return {"status": "PASS" if rows and all(not r["errors"] for r in rows) and diff_check["status"] != "FAIL" else "FAIL",
            "gitDiffCheck": diff_check, "files": rows}


def commits(project, plan, base, allowlist):
    commit_ids = git(project, "rev-list", "--reverse", base + "..HEAD").decode().splitlines()
    allowed = {r["path"] for r in allowlist["files"]}
    rows = []
    for commit in commit_ids:
        changed = git(project, "diff-tree", "--no-commit-id", "--name-only", "-r", "-z", commit).decode().rstrip("\0").split("\0")
        changed = list(filter(None, changed))
        for path in changed:
            if path not in allowed:
                raise ValueError("UNREVIEWED_PATH_IN_NEW_COMMIT: " + path)
        for path, data in zip(changed, blobs(project, [commit+":"+p for p in changed])):
            sha, errors = scan_blob(path, data, hashes(plan))
            rows.append({"commit": commit, "path": path, "sha256": sha, "errors": errors})
    return {"status": "PASS" if rows and all(not r["errors"] for r in rows) else "FAIL", "commits": commit_ids, "files": rows}


if __name__ == "__main__":
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("mode", choices=("candidates", "history", "staged", "commits"))
    p.add_argument("--project", required=True)
    p.add_argument("--plan", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--allowlist")
    p.add_argument("--base")
    a = p.parse_args()
    plan = json.loads(Path(a.plan).read_text(encoding="utf-8"))
    allowlist = json.loads(Path(a.allowlist).read_text(encoding="utf-8")) if a.allowlist else None
    if a.mode == "candidates": result = candidates(a.project, plan)
    elif a.mode == "history": result = historical(a.project, plan)
    elif a.mode == "staged": result = staged(a.project, plan, allowlist)
    else: result = commits(a.project, plan, a.base, allowlist)
    write_json(a.out, result)
    print(json.dumps({"status": result["status"], "fileCount": len(result.get("files", [])),
                      "historicalMediaBlobs": result.get("historicalMediaBlobs"), "issues": [r for r in result.get("files", []) if r["errors"]][:8]}))
    raise SystemExit(0 if result["status"] == "PASS" else 2)
