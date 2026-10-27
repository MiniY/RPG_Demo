"""Build a reviewed import plan from the audited local exports (no artwork output)."""
from pathlib import Path
import argparse
import json
import re
from statistics import median
from PIL import Image
from artwork import write_json, clean_relative, validate_inputs, ART_ROOT, TOOL_VERSION


def identifier(value):
    return re.sub(r"[^A-Za-z0-9_]+", "_", value).strip("_")


def create(source, inventory):
    validate_inputs(source, inventory)
    rows = inventory["files"]
    ase = {r["source"]: r["aseprite"] for r in rows if "aseprite" in r}
    assets, entities = [], {}
    names = sorted({Path(r["source"]).parts[1] for r in rows
                    if len(Path(r["source"]).parts) == 3 and Path(r["source"]).parts[1] != "Extra"})
    for name in names:
        entities[identifier(name)] = {"id": identifier(name), "type": name, "variant": "Default",
                                      "category": "MainEnemy", "direction": "Source facing only", "states": []}
    extras_to_body = {"Gnoll Hit": "Gnoll", "Lizard Hit": "Lizard", "Minotaur Guard": "Minotaur",
                      "Panda Guard": "Panda", "Skull Guard": "Skull", "Turtle Guard": "Turtle",
                      "Spear Goblin Strong Attack": "Spear Goblin"}
    aliases = {"Spinning": "Spining", "Windup": "Wind-up", "Attack_Start": "Start",
               "Attack_Loop": "Loop", "Attack_End": "End", "ClubPart1": "Clib_part1",
               "ClubPart2": "Club_part2"}
    for row in rows:
        if row["action"] != "COPY":
            continue
        rel, w, h = row["source"], row["width"], row["height"]
        parts, stem = Path(rel).parts, Path(rel).stem
        is_extra = parts[1] == "Extra"
        group = parts[2] if is_extra else parts[1]
        avatar = "Avatar" in stem
        static = avatar or group in ("Dead Tree", "Gnome Buildings", "Skull decorations", "Cannon") or stem == "Harpoon"
        if group == "Wooden Fence":
            fw = fh = 64
        elif group == "Goblin Hut":
            fw, fh = 192, 256
        elif group == "Pirate Tower":
            fw, fh = 128, 192
        elif static:
            fw, fh = w, h
        else:
            fw = fh = h
        if w % fw or h % fh:
            raise ValueError("NONINTEGRAL_EXPORT_GRID: " + rel)
        n = (w // fw) * (h // fh)
        spec = {"source": rel, "path": row["target"], "sha256": row["sha256"], "width": w, "height": h,
                "frameWidth": fw, "frameHeight": fh, "columns": w // fw, "rows": h // fh,
                "frameCount": n, "ppu": 64, "pivotX": 0.5, "pivotY": 0.5,
                "role": "Avatar" if avatar else "Body" if not is_extra and stem.startswith(group+"_") else "ExtraOrAuxiliary"}
        assets.append(spec)
        if avatar:
            continue
        # Associate real Extra Hit/Guard/Death/Row exports with their original body.
        body = None
        if not is_extra and stem.startswith(group + "_") and not any(
                token in stem for token in ("_Bone", "_Explosion", "_Projectile")):
            body = group
        elif group in extras_to_body:
            body = extras_to_body[group]
        elif group == "Troll Dead" and stem == "Troll_Dead":
            body = "Troll"
        elif stem == "Paddle Shark_Row":
            body = "Paddle Shark"
        if body:
            entity_id = identifier(body)
            state = stem[len(body) + 1:]
        else:
            if not is_extra:
                prefix = stem if stem.startswith(group+"_") else stem.rsplit("_", 1)[0] if "_" in stem else stem
                entity_id = identifier(prefix)
                state = stem[len(prefix)+1:] or (stem.split("_", 1)[1] if stem.startswith(group+"_") else "Static")
            elif group in ("Pig", "Pig Rider Spear Goblin"):
                entity_id = identifier(group)
                prefix = "Pig Rider" if group.startswith("Pig Rider") else "Pig"
                state = stem[len(prefix)+1:]
            elif group in ("Boat", "Cannon", "Cave", "Fish Hut", "Goblin Hut", "Pirate Tower"):
                entity_id = identifier(group)
                state = stem[len(group)+1:] if stem.startswith(group+"_") else "SourceFrames"
            elif group == "Seahorse Boat":
                variant = stem[len(group)+1:]
                entity_id, state = identifier(group+"_"+("Empty" if variant == "Idle" else variant)), "Idle"
            else:
                entity_id, state = identifier(stem), "SourceFrames" if n > 1 else "Static"
        if entity_id not in entities:
            entities[entity_id] = {"id": entity_id, "type": group if is_extra else entity_id,
                                   "variant": "Default", "category": "ExtraCreature" if group in ("Pig", "Pig Rider Spear Goblin") else "Auxiliary",
                                   "direction": "Five authored cannon facings as separate states" if group == "Cannon" else "Source facing only", "states": []}
            if group == "Seahorse Boat":
                entities[entity_id]["variant"] = "Empty" if variant == "Idle" else variant
        durations, timing = [100] * n, "Official PNG playback default: 100 ms; no matching source tag"
        source_tag = None
        candidates = [(p, a) for p, a in ase.items() if str(Path(p).parent) == str(Path(rel).parent)]
        tag_name = aliases.get(state, state.replace("_", " "))
        if state in ("SourceFrames", "Static") and "_" in stem:
            exported_tag = stem.split("_", 1)[1]
            tag_name = aliases.get(exported_tag, exported_tag)
        if group == "Seahorse Boat":
            tag_name = {"Idle": "Idle", "Bomb Fish": "1", "Harpoon Shark": "2", "Paddle Shark": "3"}[variant]
        for p, a in candidates:
            tags = [t for t in a["tags"] if t["name"].casefold() == tag_name.casefold() and t["to"]-t["from"]+1 == n]
            if tags:
                tag = tags[0]
                durations = a["durationsMs"][tag["from"]:tag["to"]+1]
                timing, source_tag = "Matching audited Aseprite frame durations", {"source": p, **tag}
                break
        loop = any(token in state.lower() for token in ("idle", "run", "walk", "move", "spinning", "projectile")) or state == "Attack_Loop"
        if group in ("Fish Hut", "Goblin Hut"):
            loop = False
            timing += "; untagged source sequence, not inferred gameplay states"
        if group == "Wooden Fence":
            # A tile sheet is a set of static variants, never an animated fence.
            for index in range(n):
                entities[entity_id]["states"].append({"name": "Tile_%02d" % index, "artPath": spec["path"],
                    "frameIndices": [index], "durationsMs": [100], "loop": False,
                    "timingEvidence": "Static 64x64 tile variant; not animation", "sourceTag": None})
        else:
            entities[entity_id]["states"].append({"name": identifier(state), "artPath": spec["path"],
                "frameIndices": list(range(n)), "durationsMs": durations, "loop": loop,
                "timingEvidence": timing, "sourceTag": source_tag})
    by_path = {r["path"]: r for r in assets}
    for entity in entities.values():
        # One fixed ground anchor per body canvas; frame-dependent auto-pivots cause jitter.
        idle = next((s for s in entity["states"] if s["name"] == "Idle"), None)
        if entity["category"] in ("MainEnemy", "ExtraCreature") and idle:
            a = by_path[idle["artPath"]]
            with Image.open(Path(source) / a["source"]) as img:
                ground = []
                for i in range(a["frameCount"]):
                    tile = img.crop((i*a["frameWidth"], 0, (i+1)*a["frameWidth"], a["frameHeight"]))
                    bbox = tile.convert("RGBA").getchannel("A").getbbox()
                    if bbox:
                        ground.append(bbox[3])
                base = median(ground)
            for state in entity["states"]:
                a2 = by_path[state["artPath"]]
                if a2["frameHeight"] == a["frameHeight"]:
                    a2["pivotY"] = (a["frameHeight"] - base) / a["frameHeight"]
                    a2["pivotEvidence"] = "Fixed median Idle alpha-ground; shared across body states; no per-frame recentering"
        entity["states"].sort(key=lambda s: (s["name"] != "Idle", s["name"]))
        if not entity["states"]:
            raise ValueError("EMPTY_PREFAB: " + entity["id"])
        existing = [s["name"] for s in entity["states"]]
        if len(existing) != len(set(existing)):
            raise ValueError("DUPLICATE_STATE: " + entity["id"])
        entity["missingSourceAnimations"] = [x for x in ("Hit", "Death") if not any(x.lower() in n.lower() or (x == "Death" and n == "Dead") for n in existing)]
        entity["prefabPath"] = "Assets/Prefabs/Monsters/Tiny Swords Enemy Pack/" + entity["id"] + ".prefab"
    # User scope: monster Prefabs only. Keep auxiliary images audited/imported but
    # create no auxiliary Prefabs, controllers, clips or preview entries.
    monsters = [e for e in entities.values() if e["category"] == "MainEnemy" or e["id"] == "Pig_Rider_Spear_Goblin"]
    return {"schemaVersion": 1, "toolVersion": TOOL_VERSION, "unityVersion": "2022.3.62f3c1",
            "sourceFingerprint": inventory["sourceFingerprint"], "artRoot": ART_ROOT,
            "visualChildName": "Visual", "gameplayIntegrated": False,
            "assets": assets, "prefabs": sorted(monsters, key=lambda e: e["id"]),
            "prefabScope": "MONSTERS_ONLY; auxiliary resources have no Prefab/controller/clip",
            "sourceValidation": {k:v for k,v in inventory.items() if k != "sourceRoot"}}


if __name__ == "__main__":
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--source", required=True)
    p.add_argument("--inventory", required=True)
    p.add_argument("--out", required=True)
    args = p.parse_args()
    plan = create(args.source, json.loads(Path(args.inventory).read_text(encoding="utf-8")))
    write_json(args.out, plan)
    print(json.dumps({"assets": len(plan["assets"]), "prefabs": len(plan["prefabs"]),
                      "states": sum(len(e["states"]) for e in plan["prefabs"]),
                      "mainEnemies": sum(e["category"] == "MainEnemy" for e in plan["prefabs"])}))
