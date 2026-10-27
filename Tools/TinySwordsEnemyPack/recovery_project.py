"""Export only reviewed engineering to a new minimal Unity recovery project."""
import argparse
import json
from pathlib import Path
import shutil
from artwork import ART_ROOT, digest, write_json, outside_git


ROOTS = ["Assets/Editor/TinySwordsEnemyPack", "Assets/Tests/PrefabPreview",
         "Assets/Animations/Monsters/Tiny Swords Enemy Pack",
         "Assets/Prefabs/Monsters/Tiny Swords Enemy Pack", "Tools/TinySwordsEnemyPack",
         "Docs/Assets/TinySwordsEnemyPack"]


def public_files(project):
    project = Path(project)
    files = set()
    for root in ROOTS:
        for p in (project / root).rglob("*"):
            if p.is_file() and "__pycache__" not in p.parts:
                files.add(p.relative_to(project).as_posix())
        if (project / (root + ".meta")).exists():
            files.add(root + ".meta")
    for p in (project / ART_ROOT).rglob("*.meta"):
        files.add(p.relative_to(project).as_posix())
    files.add(ART_ROOT + ".meta")
    files.add(".gitignore")
    return sorted(files)


def export(project, target):
    project, target = Path(project).resolve(), Path(target).resolve()
    outside_git(target)
    if target.exists():
        raise ValueError("RECOVERY_TARGET_ALREADY_EXISTS")
    files = public_files(project)
    # These are existing public ancestor IDs; no gameplay code or art is copied.
    ancestor_metas = ["Assets/Editor.meta", "Assets/Tests.meta", "Assets/Animations.meta",
                      "Assets/Animations/Monsters.meta", "Assets/Prefabs.meta",
                      "Assets/Prefabs/Monsters.meta", "Assets/_Resources.meta"]
    files += [p for p in ancestor_metas if (project / p).exists()]
    files += ["ProjectSettings/ProjectVersion.txt", "ProjectSettings/EditorBuildSettings.asset"]
    target.mkdir(parents=True)
    for relative in files:
        src, dest = project / relative, target / relative
        if src.suffix.lower() in (".png", ".aseprite", ".zip", ".exe"):
            raise ValueError("RESTRICTED_MEDIA_IN_ENGINEERING_EXPORT")
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(src, dest)
        if digest(src) != digest(dest):
            raise ValueError("EXPORT_SHA_MISMATCH")
    write_json(target / "Packages/manifest.json", {"dependencies": {
        "com.unity.2d.sprite": "1.0.0", "com.unity.modules.animation": "1.0.0",
        "com.unity.modules.imgui": "1.0.0", "com.unity.modules.imageconversion": "1.0.0",
        "com.unity.modules.screencapture": "1.0.0", "com.unity.modules.physics2d": "1.0.0",
        "com.unity.modules.jsonserialize": "1.0.0"}})
    marker = {"schemaVersion": 1, "kind": "MINIMAL_PUBLIC_ENGINEERING_CLOSURE",
              "artworkCopied": False, "runtimeScripts": ["TinySwordsEnemyPackPreview.cs"],
              "publicFiles": [{"path": p, "sha256": digest(project / p)} for p in sorted(set(files))],
              "planSha256": digest(project / "Tools/TinySwordsEnemyPack/import-plan.json")}
    write_json(target / "enemy-recovery-project.json", marker)
    return {"status": "PASS", "project": str(target), "files": len(set(files)),
            "sourceMediaFiles": len(list(target.rglob("*.png"))), "markerSha256": digest(target / "enemy-recovery-project.json")}


if __name__ == "__main__":
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--project", required=True)
    p.add_argument("--target", required=True)
    p.add_argument("--receipt", required=True)
    a = p.parse_args()
    result = export(a.project, a.target)
    write_json(a.receipt, result)
    print(json.dumps(result))
