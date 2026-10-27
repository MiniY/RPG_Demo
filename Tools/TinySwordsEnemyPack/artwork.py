"""Local-only licensed artwork audit and fail-closed restoration. No downloads."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import struct
import sys
import zlib

TOOL_VERSION = "1.0"
ART_ROOT = "Assets/_Resources/Tiny Swords Enemy Pack"


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x", encoding="utf-8", newline="\n") as output:
        json.dump(value, output, ensure_ascii=False, indent=2)
        output.write("\n")


def clean_relative(raw):
    p = PurePosixPath(raw.replace("\\", "/"))
    if p.is_absolute() or any(x in (".", "..") or ":" in x for x in p.parts):
        raise ValueError("UNSAFE_PATH: " + raw)
    cleaned = "/".join(x.replace("(", "").replace(")", "") for x in p.parts)
    if not cleaned or any(not x or x.endswith((".", " ")) for x in cleaned.split("/")):
        raise ValueError("INVALID_TARGET: " + raw)
    return cleaned


def read_aseprite(path):
    data = Path(path).read_bytes()
    size, magic, count, width, height, depth = struct.unpack_from("<I5H", data)
    if size != len(data) or magic != 0xA5E0 or depth != 32:
        raise ValueError("Unsupported/invalid Aseprite header: " + str(path))
    frames, tags, layers = [], [], []
    pos = 128
    for index in range(count):
        length, marker, old_count, duration = struct.unpack_from("<I3H", data, pos)
        new_count = struct.unpack_from("<I", data, pos + 12)[0]
        if marker != 0xF1FA or length < 16 or pos + length > size:
            raise ValueError("Invalid frame extent")
        at, cels = pos + 16, []
        for _ in range(new_count or old_count):
            chunk_size, chunk_type = struct.unpack_from("<IH", data, at)
            if chunk_size < 6 or at + chunk_size > pos + length:
                raise ValueError("Invalid chunk extent")
            payload = data[at + 6:at + chunk_size]
            if chunk_type == 0x2004:
                flags, layer_type, child, _, _, blend, opacity = struct.unpack_from("<6HB", payload)
                n = struct.unpack_from("<H", payload, 16)[0]
                layers.append({"flags": flags, "type": layer_type, "child": child,
                               "blend": blend, "opacity": opacity,
                               "name": payload[18:18+n].decode("utf-8")})
            elif chunk_type == 0x2005:
                layer, x, y, opacity, cel_type = struct.unpack_from("<HhhBH", payload)
                if cel_type in (0, 2):
                    w, h = struct.unpack_from("<HH", payload, 16)
                    pixels = payload[20:] if cel_type == 0 else zlib.decompress(payload[20:])
                    if len(pixels) != w * h * 4:
                        raise ValueError("Invalid decoded cel length")
                    cels.append({"layer": layer, "x": x, "y": y, "opacity": opacity,
                                 "width": w, "height": h, "pixels": pixels})
                elif cel_type == 1:
                    link = struct.unpack_from("<H", payload, 16)[0]
                    if link >= index:
                        raise ValueError("Invalid linked cel")
                    cels.append({"layer": layer, "x": x, "y": y, "opacity": opacity, "link": link})
                else:
                    raise ValueError("Unsupported cel type")
            elif chunk_type == 0x2018:
                total = struct.unpack_from("<H", payload)[0]
                off = 10
                for _ in range(total):
                    first, last, direction, repeat = struct.unpack_from("<HHBH", payload, off)
                    n = struct.unpack_from("<H", payload, off + 17)[0]
                    name = payload[off+19:off+19+n].decode("utf-8")
                    tags.append({"name": name, "from": first, "to": last,
                                 "direction": direction, "repeat": repeat})
                    off += 19 + n
            at += chunk_size
        if at != pos + length:
            raise ValueError("Frame chunks do not cover frame")
        frames.append({"durationMs": duration, "cels": cels})
        pos += length
    if pos != size:
        raise ValueError("Aseprite trailing data")
    return {"width": width, "height": height, "frameCount": count, "depth": depth,
            "durationsMs": [x["durationMs"] for x in frames], "tags": tags, "layers": layers}


def audit(source, out):
    from PIL import Image
    source, out = Path(source).resolve(), Path(out)
    if not source.is_dir() or source.is_symlink():
        raise ValueError("SOURCE_NOT_NORMAL_DIRECTORY")
    for directory in (p for p in source.rglob("*") if p.is_dir()):
        if directory.is_symlink() or directory.stat().st_file_attributes & 0x400:
            raise ValueError("SOURCE_REPARSE_DIRECTORY: " + str(directory))
    files = sorted((p for p in source.rglob("*") if p.is_file()), key=lambda p: p.as_posix().casefold())
    inventory, mapped, ase = [], {}, {}
    for p in files:
        rel = p.relative_to(source).as_posix()
        if p.is_symlink() or p.stat().st_file_attributes & 0x400:
            raise ValueError("REPARSE_FILE: " + rel)
        target = ART_ROOT + "/" + clean_relative(rel)
        collision = target.casefold()
        if collision in mapped:
            raise ValueError("PATH_COLLISION: " + rel + " / " + mapped[collision])
        mapped[collision] = rel
        header = p.read_bytes()[:16].hex()
        row = {"source": rel, "target": target, "bytes": p.stat().st_size,
               "sha256": digest(p), "headerHex": header}
        if p.name == ".DS_Store" or p.name.startswith("._") or "__MACOSX" in p.parts:
            row.update(action="EXCLUDE", reason="Finder/AppleDouble metadata; not game content")
        elif p.suffix.lower() == ".png":
            if not bytes.fromhex(header).startswith(b"\x89PNG\r\n\x1a\n"):
                raise ValueError("PNG_SIGNATURE_MISMATCH: " + rel)
            with Image.open(p) as img:
                img.load()
                row.update(action="COPY", reason="Decoded PNG; existing TextureImporter supports it",
                           width=img.width, height=img.height, mode=img.mode,
                           alphaBounds=img.convert("RGBA").getchannel("A").getbbox())
        elif p.suffix.lower() == ".aseprite":
            info = read_aseprite(p)
            ase[rel] = info
            row.update(action="EXCLUDE", reason="Editable source retained externally; PNG exports available; no Aseprite importer required", aseprite=info)
        elif p.suffix.lower() == ".meta":
            row.update(action="HOLD", reason="Source metadata requires GUID collision review")
        else:
            row.update(action="HOLD", reason="Unknown actual format; explicit review required")
        inventory.append(row)
    canonical = "\n".join(x["source"] + "\0" + x["sha256"] for x in inventory).encode()
    result = {"schemaVersion": 1, "toolVersion": TOOL_VERSION, "sourceRoot": str(source),
              "sourceFingerprint": hashlib.sha256(canonical).hexdigest(), "files": inventory,
              "fileCount": len(inventory), "copyCount": sum(x["action"] == "COPY" for x in inventory),
              "excludedCount": sum(x["action"] == "EXCLUDE" for x in inventory),
              "holdCount": sum(x["action"] == "HOLD" for x in inventory)}
    write_json(out / "source-inventory.json", result)
    print(json.dumps({k: v for k, v in result.items() if k != "files"}, ensure_ascii=False))
    return result


def validate_inputs(source, manifest):
    source = Path(source).resolve()
    actual = {p.relative_to(source).as_posix() for p in source.rglob("*") if p.is_file()}
    expected = {r["source"] for r in manifest["files"]}
    if actual != expected:
        raise ValueError("SOURCE_FILE_SET_CHANGED: missing=" + str(sorted(expected-actual)) + "; extra=" + str(sorted(actual-expected)))
    for r in manifest["files"]:
        p = source / r["source"]
        if p.is_symlink() or p.stat().st_file_attributes & 0x400 or digest(p) != r["sha256"]:
            raise ValueError("SOURCE_SHA_MISMATCH: " + r["source"])


def copy_art(source, project, inventory):
    validate_inputs(source, inventory)
    if inventory["holdCount"]:
        raise ValueError("HOLD_FILES_UNRESOLVED")
    project = Path(project).resolve()
    # Validate every destination before the first write; never partially overwrite valid assets.
    rows = [r for r in inventory["files"] if r["action"] == "COPY"]
    for r in rows:
        p = project / r["target"]
        if not p.resolve().is_relative_to(project) or p.is_symlink():
            raise ValueError("TARGET_OUTSIDE_PROJECT")
        if p.exists() and digest(p) != r["sha256"]:
            raise ValueError("TARGET_SHA_CONFLICT: " + r["target"])
    copied = 0
    for r in rows:
        p = project / r["target"]
        if not p.exists():
            p.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(Path(source) / r["source"], p)
            copied += 1
        if digest(p) != r["sha256"]:
            raise ValueError("COPY_SHA_MISMATCH: " + r["target"])
    return {"status": "NO_OP" if not copied else "COPIED", "copied": copied, "verified": len(rows)}


def outside_git(path):
    path = Path(path).resolve()
    for parent in (path, *path.parents):
        if (parent / ".git").exists():
            raise ValueError("PRIVATE_KIT_INSIDE_GIT: " + str(path))


def create_kit(source, project, plan_path, golden_path, source_archive, kit_path):
    import zipfile
    source, project, kit = Path(source).resolve(), Path(project).resolve(), Path(kit_path).resolve()
    outside_git(kit)
    if kit.exists():
        raise ValueError("KIT_ALREADY_EXISTS")
    plan = json.loads(Path(plan_path).read_text(encoding="utf-8"))
    validate_inputs(source, plan["sourceValidation"])
    golden = json.loads(Path(golden_path).read_text(encoding="utf-8"))
    if golden["status"] != "PASS" or golden["sourceFingerprint"] != plan["sourceFingerprint"]:
        raise ValueError("GOLDEN_NOT_VERIFIED")
    archive = Path(source_archive)
    # Match the purchased archive's real contents, rather than trusting its filename.
    with zipfile.ZipFile(archive) as z:
        bad = z.testzip()
        if bad:
            raise ValueError("SOURCE_ARCHIVE_CRC: " + bad)
        effective = {}
        for entry in z.infolist():
            name = entry.filename
            prefix = source.name + "/"
            if name.startswith(prefix) and not entry.is_dir():
                relative = name[len(prefix):]
                if relative.lower().endswith((".png", ".aseprite")):
                    effective[relative] = hashlib.sha256(z.read(entry)).hexdigest()
        expected = {r["source"]:r["sha256"] for r in plan["sourceValidation"]["files"] if r["source"].lower().endswith((".png", ".aseprite"))}
        if effective != expected:
            raise ValueError("SOURCE_ARCHIVE_CONTENT_DIFFERS")
    copies = [(project / a["path"], "payload/" + a["path"], "PRIVATE_PAID_ART") for a in plan["assets"]]
    metadata = sorted((project / ART_ROOT).rglob("*.meta")) + [project / (ART_ROOT + ".meta")]
    copies += [(p, "payload/" + p.relative_to(project).as_posix(), "PRIVATE_RECOVERY_STATE") for p in metadata]
    copies += [(Path(plan_path), "import-plan.json", "PRIVATE_RECOVERY_STATE"),
               (Path(golden_path), "asset-guid-map.json", "PRIVATE_RECOVERY_STATE"),
               (archive, "source-archive.zip", "PRIVATE_PAID_ART")]
    needed = sum(p.stat().st_size for p, _, _ in copies)
    ancestor = kit.parent
    while not ancestor.exists():
        ancestor = ancestor.parent
    if shutil.disk_usage(ancestor).free < needed * 3 + 1024 * 1024:
        raise ValueError("INSUFFICIENT_PRIVATE_KIT_SPACE")
    kit.mkdir(parents=True)
    files = []
    for src, relative, classification in copies:
        dest = kit / relative
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(src, dest)
        sha = digest(src)
        if digest(dest) != sha:
            raise ValueError("KIT_COPY_SHA_MISMATCH")
        files.append({"path": relative, "sha256": sha, "bytes": dest.stat().st_size, "class": classification})
    write_json(kit / "manifest.json", {"schemaVersion": 1, "toolVersion": TOOL_VERSION,
        "sourceFingerprint": plan["sourceFingerprint"], "files": files,
        "restorationOrder": "Validate public engineering + lawful source + private kit, restore art and metadata, THEN open Unity and verify GUID/fileID against golden"})
    return {"status": "PASS", "kit": str(kit), "files": len(files), "bytes": needed,
            "manifestSha256": digest(kit / "manifest.json"), "sourceArchiveSha256": digest(archive)}


def read_kit(kit):
    kit = Path(kit).resolve()
    if not (kit / "manifest.json").is_file():
        raise ValueError("PRIVATE_KIT_MISSING")
    manifest = json.loads((kit / "manifest.json").read_text(encoding="utf-8"))
    if manifest.get("schemaVersion") != 1:
        raise ValueError("UNSUPPORTED_KIT_VERSION")
    for row in manifest["files"]:
        raw = row["path"]
        if clean_relative(raw) != raw or not (kit / raw).resolve().is_relative_to(kit):
            raise ValueError("UNSAFE_KIT_PATH")
        p = kit / raw
        if not p.is_file() or digest(p) != row["sha256"]:
            raise ValueError("PRIVATE_KIT_SHA_MISMATCH: " + raw)
    return manifest


def duplicate_kit(kit, backup):
    kit, backup = Path(kit).resolve(), Path(backup).resolve()
    read_kit(kit)
    outside_git(backup)
    if backup.exists():
        raise ValueError("BACKUP_ALREADY_EXISTS")
    shutil.copytree(kit, backup)
    read_kit(backup)
    if digest(kit / "manifest.json") != digest(backup / "manifest.json"):
        raise ValueError("BACKUP_MANIFEST_DIFFERS")
    return {"status": "PASS", "kit": str(kit), "backup": str(backup),
            "manifestSha256": digest(backup / "manifest.json"),
            "sourceBackupSha256": digest(backup / "source-archive.zip")}


def require_editor_offline(project):
    sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "EngineeringAssurance"))
    import unity_guard
    result = unity_guard.status(Path(project).resolve())
    if result["state"] != "FREE":
        raise ValueError("RESTORE_REQUIRES_FREE_GUARD: " + result["state"] + "; " + str(result.get("reason")))


def restore(source, project, kit, offline_check=require_editor_offline):
    kit, project = Path(kit).resolve(), Path(project).resolve()
    manifest = read_kit(kit)
    plan = json.loads((kit / "import-plan.json").read_text(encoding="utf-8"))
    public_plan = project / "Tools/TinySwordsEnemyPack/import-plan.json"
    if not public_plan.is_file() or digest(public_plan) != digest(kit / "import-plan.json"):
        raise ValueError("PUBLIC_PLAN_DIFFERS_OR_MISSING")
    validate_inputs(source, plan["sourceValidation"])
    if plan["sourceFingerprint"] != manifest["sourceFingerprint"]:
        raise ValueError("KIT_SOURCE_VERSION_DIFFERS")
    rows = []
    for row in plan["sourceValidation"]["files"]:
        if row["action"] != "COPY":
            continue
        expected = ART_ROOT + "/" + clean_relative(row["source"])
        if row["target"] != expected:
            raise ValueError("SOURCE_TARGET_MAPPING_DIFFERS")
        rows.append((Path(source) / row["source"], expected, row["sha256"]))
    for row in manifest["files"]:
        if row["path"].startswith("payload/") and row["path"].endswith(".meta"):
            relative = row["path"][len("payload/"):]
            if not (relative == ART_ROOT+".meta" or relative.startswith(ART_ROOT+"/")):
                raise ValueError("META_OUTSIDE_ART_LIBRARY")
            rows.append((kit / row["path"], relative, row["sha256"]))
    expected_files = {relative for _, relative, _ in rows}
    art = project / ART_ROOT
    if art.exists():
        for p in art.rglob("*"):
            if p.is_file() and p.relative_to(project).as_posix() not in expected_files:
                raise ValueError("UNEXPECTED_TARGET_FILE: " + p.relative_to(project).as_posix())
    identities = {}
    for src, relative, sha in rows:
        dest = project / relative
        if not dest.resolve().is_relative_to(project):
            raise ValueError("TARGET_OUTSIDE_PROJECT")
        for ancestor in (dest, *dest.parents):
            if ancestor == project.parent:
                break
            if ancestor.exists() and (ancestor.is_symlink() or ancestor.stat().st_file_attributes & 0x400):
                raise ValueError("TARGET_REPARSE_POINT")
        if dest.exists() and digest(dest) != sha:
            raise ValueError("TARGET_SHA_CONFLICT: " + relative)
        if relative.endswith(".meta"):
            match = re.search(r"^guid: ([0-9a-f]{32})$", src.read_text(encoding="utf-8"), re.M)
            if not match or match[1] in identities:
                raise ValueError("INVALID_OR_DUPLICATE_KIT_GUID")
            identities[match[1]] = relative
    for p in (project / "Assets").rglob("*.meta"):
        match = re.search(r"^guid: ([0-9a-f]{32})$", p.read_text(encoding="utf-8"), re.M)
        if match and match[1] in identities and p.relative_to(project).as_posix() != identities[match[1]]:
            raise ValueError("PROJECT_GUID_COLLISION: " + p.relative_to(project).as_posix())
    offline_check(project)
    copied = 0
    for src, relative, sha in rows:
        dest = project / relative
        if not dest.exists():
            dest.parent.mkdir(parents=True, exist_ok=True)
            with dest.open("xb") as output:
                output.write(src.read_bytes())
            copied += 1
        if digest(dest) != sha:
            raise ValueError("RESTORED_SHA_MISMATCH")
    return {"status": "NO_OP" if copied == 0 else "RESTORED", "copied": copied,
            "verified": len(rows), "sourceFingerprint": plan["sourceFingerprint"],
            "kitManifestSha256": digest(kit / "manifest.json"), "guidCount": len(identities)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    a = sub.add_parser("audit")
    a.add_argument("--source", required=True)
    a.add_argument("--out", required=True)
    c = sub.add_parser("copy")
    c.add_argument("--source", required=True)
    c.add_argument("--project", required=True)
    c.add_argument("--inventory", required=True)
    c.add_argument("--receipt", required=True)
    k = sub.add_parser("kit")
    for name in ("source", "project", "plan", "golden", "source-archive", "kit", "receipt"):
        k.add_argument("--"+name, required=True)
    r = sub.add_parser("restore")
    for name in ("source", "project", "kit", "receipt"):
        r.add_argument("--"+name, required=True)
    b = sub.add_parser("backup")
    for name in ("kit", "backup", "receipt"):
        b.add_argument("--"+name, required=True)
    args = parser.parse_args()
    if args.command == "audit":
        audit(args.source, args.out)
    elif args.command == "copy":
        result = copy_art(args.source, args.project, json.loads(Path(args.inventory).read_text(encoding="utf-8")))
        write_json(args.receipt, result)
        print(json.dumps(result))
    else:
        if args.command == "kit":
            result = create_kit(args.source, args.project, args.plan, args.golden, args.source_archive, args.kit)
        elif args.command == "backup":
            result = duplicate_kit(args.kit, args.backup)
        else:
            result = restore(args.source, args.project, args.kit)
        write_json(args.receipt, result)
        print(json.dumps(result))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError) as exc:
        print("FAIL_CLOSED: " + str(exc), file=sys.stderr)
        sys.exit(2)
