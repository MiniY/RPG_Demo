"""Narrow Preview adapter; lifecycle stays in existing Guard.run -> Supervisor."""
import argparse
import json
from pathlib import Path
import sys
from artwork import digest, write_json


def run(project, build_report, out, timeout):
    sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "EngineeringAssurance"))
    import unity_guard as guard
    project, out = Path(project).resolve(), Path(out).resolve()
    report = json.loads(Path(build_report).read_text(encoding="utf-8"))
    executable = Path(report["executable"]).resolve()
    if report["status"] != "PASS" or executable.name != "TinySwordsEnemyPackPreview.exe" or not (project / "enemy-recovery-project.json").is_file():
        raise ValueError("UNVERIFIED_PREVIEW_BUILD_OR_PROJECT")
    if out.exists():
        raise ValueError("PLAYER_OUTPUT_ALREADY_EXISTS")
    expected = digest(executable)
    out.mkdir(parents=True)
    command = [str(executable), "-screen-width", "1280", "-screen-height", "720", "-screen-fullscreen", "0",
               "-logFile", str(out / "player.log"), "--enemy-accept", str(out)]

    def validate(actual_project, actual_command):
        if Path(actual_project).resolve() != project or actual_command != command or digest(executable) != expected:
            raise ValueError("PREVIEW_COMMAND_OR_BINARY_CHANGED")

    before = guard.status(project)
    write_json(out / "guard-before.json", before)
    if before["state"] != "FREE":
        raise ValueError("PLAYER_GUARD_NOT_FREE")
    result = guard.run(project, "enemy-pack-player-acceptance", "Enemy Pack visual Preview Player", "Player",
                       command, timeout, command_validator=validate)
    write_json(out / "supervisor.json", result)
    after = guard.status(project)
    write_json(out / "guard-after.json", after)
    lifecycle = result.get("naturalExit") and result.get("exitCode") == 0 and result.get("cleanup", {}).get("status") == "PASS" and not result.get("cleanup", {}).get("forced") and not result.get("cleanup", {}).get("residualPids") and all(result.get("locks", {}).get(k, {}).get("released") for k in ("project", "heavy")) and after["state"] == "FREE"
    evidence_path = out / "player-evidence.json"
    evidence = json.loads(evidence_path.read_text(encoding="utf-8")) if evidence_path.exists() else {}
    receipt = {"status": "PASS" if lifecycle and evidence.get("status") == "PASS" else "FAIL",
               "runId": result["runId"], "executableSha256": expected, "playerEvidenceSha256": digest(evidence_path) if evidence_path.exists() else None,
               "lifecycle": "PASS" if lifecycle else "FAIL", "player": evidence.get("status", "NOT_RUN"),
               "prefabs": evidence.get("prefabs"), "states": evidence.get("states")}
    write_json(out / "receipt.json", receipt)
    return receipt


if __name__ == "__main__":
    p = argparse.ArgumentParser(description=__doc__)
    for name in ("project", "build-report", "out"):
        p.add_argument("--"+name, required=True)
    p.add_argument("--timeout", type=int, default=240)
    a = p.parse_args()
    result = run(a.project, a.build_report, a.out, a.timeout)
    print(json.dumps(result))
    sys.exit(0 if result["status"] == "PASS" else 2)
