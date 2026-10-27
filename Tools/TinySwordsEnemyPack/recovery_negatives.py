"""Real-kit refusal exercises; mutate only a new throwaway COPY of lawful source."""
import argparse
import json
from pathlib import Path
import shutil
from artwork import ART_ROOT, digest, restore, write_json


def snapshot(project):
    root = Path(project) / ART_ROOT
    rows = {p.relative_to(project).as_posix(): digest(p) for p in root.rglob("*") if p.is_file()}
    rows[ART_ROOT + ".meta"] = digest(str(root)+".meta")
    return rows


def exercise(source, project, kit, out):
    source, project, kit, out = map(lambda p: Path(p).resolve(), (source, project, kit, out))
    if out.exists():
        raise ValueError("NEGATIVE_OUTPUT_ALREADY_EXISTS")
    out.mkdir(parents=True)
    before = snapshot(project)
    cases = []

    def refused(name, candidate_source, candidate_kit, expected):
        try:
            restore(candidate_source, project, candidate_kit)
        except ValueError as error:
            message = str(error)
            stable = snapshot(project) == before
            passed = expected in message and stable
            cases.append({"name": name, "status": "PASS" if passed else "FAIL", "reason": message,
                          "expectedReason": expected, "validTargetUnchanged": stable})
            if not passed:
                raise
            return
        raise ValueError("NEGATIVE_WAS_NOT_REFUSED: " + name)

    refused("missing_private_kit", source, out / "missing-kit", "PRIVATE_KIT_MISSING")
    wrong = out / "wrong-source-version"
    shutil.copytree(source, wrong)
    image = sorted(wrong.rglob("*.png"))[0]
    image.write_bytes(image.read_bytes() + b"wrong-version-contract-fixture")
    refused("wrong_source_sha", wrong, kit, "SOURCE_SHA_MISMATCH")
    # This is our newly created fixture, never a file in the original source directory.
    image.unlink()
    refused("missing_source_image", wrong, kit, "SOURCE_FILE_SET_CHANGED")
    result = {"status": "PASS", "cases": cases, "targetFilesVerified": len(before)}
    write_json(out / "receipt.json", result)
    return result


if __name__ == "__main__":
    p = argparse.ArgumentParser(description=__doc__)
    for name in ("source", "project", "kit", "out"):
        p.add_argument("--"+name, required=True)
    a = p.parse_args()
    result = exercise(a.source, a.project, a.kit, a.out)
    print(json.dumps({"status": result["status"], "cases": len(result["cases"]), "validTargetUnchanged": True}))
