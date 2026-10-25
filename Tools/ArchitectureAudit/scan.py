"""Read-only Unity YAML inventory for the local architecture audit.

This is a static index, not a Unity import/compile/Play Mode validator. It reads
Assets only and writes the generated graph and viewer under Docs/architecture-audit.
"""

from __future__ import annotations

import argparse
import collections
import datetime as dt
import hashlib
import json
import re
import subprocess
from pathlib import Path
from xml.etree import ElementTree

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets"
OUT = ROOT / "Docs" / "architecture-audit"
TEMPLATE = Path(__file__).with_name("viewer-template.html")
YAML_EXTENSIONS = {".unity", ".prefab", ".asset", ".controller", ".anim", ".overridecontroller"}
FINGERPRINT_EXTENSIONS = YAML_EXTENSIONS | {".cs", ".asmdef", ".asmref", ".rsp", ".inputactions", ".json", ".uxml", ".uss"}
GUID_RE = re.compile(r"\bguid:\s*([0-9a-f]{32})\b")
FILE_ID_RE = re.compile(r"fileID:\s*(-?\d+)")
DOC_RE = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\s*$", re.M)
TRANSIENT_TEST_SCENE_RE = re.compile(r"^Assets/InitTestScene\d+\.unity$")


def relative(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def system_for(path: str) -> str:
    p = path.lower()
    if p.startswith("assets/_resources/") or p.startswith("assets/sprites/"):
        return "Art Assets"
    if p.startswith("assets/text mesh pro/") or p.startswith("library/packagecache/"):
        return "Unity Packages"
    if "mapgeneration" in p:
        return "Random Map"
    if "towerrun" in p or "blackhole" in p or "mountaingate" in p:
        return "Tower Run"
    if "monster" in p:
        return "Monster"
    if "plant" in p or "animal" in p or "destructible" in p:
        return "Damageable"
    if "reward" in p or "loot" in p:
        return "Reward"
    if "shop" in p or "merchant" in p:
        return "Shop"
    if "inventory" in p or "package" in p or "item" in p:
        return "Inventory"
    if "save" in p or "session" in p:
        return "Save"
    if "/ui/" in p or "hud" in p:
        return "UI"
    if "player" in p or "warrior" in p:
        return "Player"
    if "audio" in p:
        return "Audio"
    if "camera" in p or "minimap" in p:
        return "Camera/Minimap"
    return "Infrastructure"


def first_field(block: str, name: str) -> str | None:
    match = re.search(rf"(?m)^  {re.escape(name)}:\s*(.*)$", block)
    return match.group(1).strip() if match else None


def first_file_id(block: str, name: str) -> str | None:
    value = first_field(block, name)
    match = FILE_ID_RE.search(value or "")
    return match.group(1) if match else None


def documents(raw: str):
    matches = list(DOC_RE.finditer(raw))
    for index, match in enumerate(matches):
        end = matches[index + 1].start() if index + 1 < len(matches) else len(raw)
        block = raw[match.start():end]
        line = raw.count("\n", 0, match.start()) + 1
        yield match.group(1), match.group(2), block, line


def git_head() -> str:
    return subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()


def source_is_dirty() -> bool:
    scope = ["Assets", "Packages", "ProjectSettings", "Tools/ArchitectureAudit"]
    # Attribute changes can leave stat-only M markers even when Git reports no content diff.
    tracked = subprocess.check_output(
        ["git", "diff", "--name-only", "HEAD", "--", *scope],
        cwd=ROOT, text=True, encoding="utf-8", errors="replace")
    untracked = subprocess.check_output(
        ["git", "ls-files", "--others", "--exclude-standard", "--", *scope],
        cwd=ROOT, text=True, encoding="utf-8", errors="replace")
    return any("/__pycache__/" not in name.replace("\\", "/")
               and not name.endswith(".pyc") for name in (tracked + untracked).splitlines())


def runtime_evidence_is_current(item: dict) -> bool:
    """A passed XML counts only while its snapshot inputs match this working tree."""
    proof = item.get("runtimeEvidence")
    if not proof or not proof.get("testSource"):
        return False
    proof_path = Path(proof["path"])
    if len(proof_path.parts) < 5 or proof_path.parts[0] != ".codex":
        return False
    xml_path = ROOT / proof_path
    snapshot_root = ROOT / proof_path.parents[2]
    if not xml_path.is_file():
        return False
    for path in {item["source"], item["target"], proof["testSource"],
                 *proof.get("inputs", [])}:
        current = ROOT / path
        snapshot = snapshot_root / path
        if not current.is_file() or not snapshot.is_file() or current.read_bytes() != snapshot.read_bytes():
            return False
    try:
        result = ElementTree.parse(xml_path).getroot()
    except ElementTree.ParseError:
        return False
    return result.attrib.get("result") == "Passed" and any(
        test.attrib.get("name") == proof["test"] and test.attrib.get("result") == "Passed"
        for test in result.iter("test-case"))


def resolve_curated_line(root: Path, item: dict) -> int:
    """Refresh a moved source line only when its reviewed text is unambiguous."""
    proof = item["evidence"]
    lines = (root / proof["path"]).read_text(encoding="utf-8-sig", errors="replace").splitlines()
    needle = proof["contains"]
    def matches(index: int) -> bool:
        line = lines[index - 1].lstrip()
        return needle in line and not line.startswith(("//", "*", "/*"))

    hint = proof["line"]
    if isinstance(hint, int) and 1 <= hint <= len(lines) and matches(hint):
        return hint
    candidates = [index for index in range(1, len(lines) + 1) if matches(index)]
    if len(candidates) != 1:
        raise ValueError(f"Curated evidence missing or ambiguous ({candidates}): {item}")
    return candidates[0]


def responsibility_candidates(graph: dict) -> dict:
    """Find review candidates from confirmed edges, never infer a runtime defect."""
    grouped = collections.defaultdict(list)
    for edge in graph["edges"]:
        target_method = edge.get("targetMethod", "")
        method_name = target_method.rsplit(".", 1)[-1]
        if (edge.get("evidenceType") == "reviewed source line"
                and re.match(r"^(?:Try)?(?:Save|Commit|Write|Replace|Restore|Apply)", method_name)):
            grouped[(edge["target"], target_method)].append(edge)
    candidates = []
    for (target, method), edges in sorted(grouped.items()):
        sources = sorted({edge["source"] for edge in edges})
        if len(sources) < 2:
            continue
        evidence = [{"component": edge["source"].removeprefix("asset:"),
                     "relation": edge["relation"], "path": edge["evidence"]["path"],
                     "line": edge["evidence"]["line"], "validation": edge["validation"]}
                    for edge in edges]
        references = [{"component": edge["target"].removeprefix("asset:"),
                       "path": edge["evidence"]["path"], "line": edge["evidence"]["line"],
                       "relation": edge["relation"]}
                      for edge in graph["edges"] if edge["target"] in sources
                      and edge["relation"] in {"uses script", "serialized script"}
                      and edge["evidence"]["path"].endswith((".prefab", ".unity"))]
        incoming = [{"component": edge["source"].removeprefix("asset:"),
                     "path": edge["evidence"]["path"], "line": edge["evidence"]["line"],
                     "relation": edge["relation"]}
                    for edge in graph["edges"] if edge["target"] in sources
                    and edge.get("evidenceType") == "reviewed source line"]
        key = target + "|" + method + "|" + "|".join(sources)
        candidates.append({"candidateId": "DR-" + hashlib.sha256(key.encode()).hexdigest()[:10].upper(),
                           "components": [source.removeprefix("asset:") for source in sources],
                           "sharedTarget": target.removeprefix("asset:"), "targetMethod": method,
                           "codeEvidence": evidence, "productionReferences": references,
                           "incomingCallEvidence": incoming,
                           "runtimeRelationship": "UNKNOWN: static calls and serialized references do not establish co-execution",
                           "potentialOverlap": "Multiple entry points submit state to the same write-like method; inspect ownership and ordering",
                           "plausibleNonConflict": "Entry points may own different fields or phases and coordinate through one intended state store",
                           "confidence": "MEDIUM for shared call target; LOW for an actual conflict",
                           "limitations": ["Curated text evidence is not a semantic C# call graph",
                                           "Prefab variants, test doubles, strategies, view models and caches can be intentional",
                                           "Runtime instance count and overwrite order require Unity or targeted tests"],
                           "recommendedReview": ["Trace each entry point's fields and call order",
                                                 "Check scene/prefab wiring and relevant Unity tests",
                                                 "Ask the owner whether these entry points should coordinate"],
                           "humanDecisionRequired": True})
    return {"sourceSha256": graph["sourceSha256"], "head": graph["head"],
            "scope": "Existing curated writer edges plus scene/prefab script references",
            "candidates": candidates,
            "unassessed": ["Manager semantic similarity without shared edges",
                           "Duplicate event handling at runtime", "Multiple live manager instances",
                           "Old and new implementation co-execution", "Duplicate initialization or restore entry points"]}


def event_subscription_candidates(graph: dict, root: Path) -> dict:
    """Repeated same-handler subscriptions in one script, with production asset links."""
    subscription = re.compile(r"\b([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*\+=\s*([A-Za-z_]\w*)\s*;")
    scripts = [node for node in graph["nodes"] if node["kind"] == "script"]
    candidates = []
    for node in scripts:
        path = node["path"]
        if "/Tests/" in path or "/Editor/" in path:
            continue
        lines = (root / path).read_text(encoding="utf-8-sig", errors="replace").splitlines()
        grouped = collections.defaultdict(list)
        removals = collections.Counter()
        for line_no, line in enumerate(lines, 1):
            code = line.split("//", 1)[0]
            for match in subscription.finditer(code):
                grouped[match.groups()].append(line_no)
            for match in re.finditer(r"\b([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*-="
                                     r"\s*([A-Za-z_]\w*)\s*;", code):
                removals[match.groups()] += 1
        references = [{"path": edge["evidence"]["path"], "line": edge["evidence"]["line"],
                       "relation": edge["relation"]} for edge in graph["edges"]
                      if edge["target"] == node["id"] and edge["relation"] in
                      {"uses script", "serialized script"} and
                      edge["evidence"]["path"].endswith((".prefab", ".unity"))]
        for (event, handler), locations in sorted(grouped.items()):
            if len(locations) < 2 or not references:
                continue
            declaration = re.compile(
                r"^\s*(?:(?:public|private|protected|internal|static|async|override|virtual|sealed|new)\s+)*"
                r"[A-Za-z_]\w*(?:<[^>]+>)?\s+" + re.escape(handler) + r"\s*\(")
            handler_lines = [number for number, line in enumerate(lines, 1)
                             if declaration.search(line.split("//", 1)[0])]
            if not handler_lines:
                continue
            key = path + "|" + event + "|" + handler
            candidates.append({
                "candidateId": "EV-" + hashlib.sha256(key.encode()).hexdigest()[:10].upper(),
                "component": path, "event": event, "handler": handler,
                "codeEvidence": [{"path": path, "line": line} for line in locations],
                "handlerMethodEvidence": [{"path": path, "line": line} for line in handler_lines],
                "unsubscribeCount": removals[(event, handler)],
                "productionReferences": references,
                "runtimeRelationship": "UNKNOWN: static subscriptions and asset references do not prove one live instance executes both paths",
                "potentialOverlap": "The same component subscribes the same handler more than once",
                "plausibleNonConflict": "Branches, reset-before-add, prefab variants, or separate lifecycles may prevent double delivery",
                "confidence": "HIGH for repeated source statements and local handler declaration; LOW for duplicate runtime handling",
                "limitations": ["Text matching misses indirection and subscriptions in generated or package code",
                               "Asset references do not establish live instance count or execution order"],
                "recommendedReview": ["Trace lifecycle and control flow around each line",
                                      "Check unsubscribe symmetry and scene/prefab instance count",
                                      "Run a bounded Unity event-delivery test if ambiguity remains"],
                "humanDecisionRequired": True})
    return {"sourceSha256": graph["sourceSha256"], "head": graph["head"],
            "scope": "Repeated += statements per handler in authored script with Unity asset reference",
            "scriptsChecked": len(scripts), "candidates": candidates,
            "blindSpots": ["Different handlers subscribed to the same event may be intentional",
                           "Multiple runtime instances and separate strategies are not decided statically",
                           "No C# control-flow or delegate alias analysis"]}


def build_graph(portable: bool = False, source_context: dict | None = None) -> dict:
    nodes: dict[str, dict] = {}
    edges: dict[tuple, dict] = {}
    guid_to_path: dict[str, str] = {}
    package_guid_to_path: dict[str, str] = {}
    duplicated_guids: list[dict] = []
    unresolved: list[dict] = []
    counters = collections.Counter()
    fingerprint = hashlib.sha256()

    def add_node(node_id: str, label: str, kind: str, path: str, line: int = 1, **extra):
        if node_id not in nodes:
            nodes[node_id] = {"id": node_id, "label": label, "kind": kind,
                              "path": path, "line": line, "system": system_for(path), **extra}

    def add_edge(source: str, relation: str, target: str, path: str, line: int,
                 confidence="high", validation="static", **details):
        key = source, relation, target, path
        if key not in edges:
            if validation == "textual only":
                evidence_type, relation_type = "C# text match", "dependency"
            elif validation.startswith("source confirmed"):
                evidence_type, relation_type = "reviewed source line", "call"
            elif relation.startswith("UnityEvent"):
                evidence_type, relation_type = "UnityEvent YAML", "event"
            elif relation in {"contains GameObject", "has Component", "contains child"}:
                evidence_type, relation_type = "Unity YAML hierarchy", "hierarchy"
            else:
                evidence_type, relation_type = "Unity YAML GUID", "serialized reference"
            edges[key] = {"id": f"e{len(edges)+1}", "source": source, "relation": relation,
                          "target": target, "evidence": {"path": path, "line": line},
                          "evidenceType": evidence_type, "relationType": relation_type,
                          "confidence": confidence, "validation": validation,
                          "occurrences": 1, **details}
        else:
            edges[key]["occurrences"] += 1

    for meta in sorted(ASSETS.rglob("*.meta")):
        asset = Path(str(meta)[:-5])
        if not asset.is_file():
            continue
        # Unity Test Runner can create an ephemeral root-level scene during tests.
        # It is not an authored project scene and may disappear mid-scan.
        if TRANSIENT_TEST_SCENE_RE.fullmatch(relative(asset)):
            continue
        match = re.search(r"(?m)^guid:\s*([0-9a-f]{32})\s*$", meta.read_text(encoding="utf-8-sig", errors="replace"))
        if not match:
            continue
        guid = match.group(1)
        path = relative(asset)
        if guid in guid_to_path:
            duplicated_guids.append({"guid": guid, "paths": [guid_to_path[guid], path]})
        guid_to_path[guid] = path
        counters[asset.suffix.lower()] += 1
        fingerprint.update(path.encode("utf-8"))
        fingerprint.update(meta.read_bytes())

    # PackageCache is used only to classify references, never as authored project data.
    package_root = ROOT / "Library" / "PackageCache"
    if portable:
        package_map_path = Path(__file__).with_name("package-guid-map.json")
        fingerprint.update(package_map_path.read_bytes())
        package_guid_to_path = json.loads(package_map_path.read_text(encoding="utf-8"))
    elif package_root.is_dir():
        for meta in package_root.rglob("*.meta"):
            match = re.search(r"(?m)^guid:\s*([0-9a-f]{32})\s*$",
                              meta.read_text(encoding="utf-8-sig", errors="replace"))
            if match:
                package_guid_to_path[match.group(1)] = relative(Path(str(meta)[:-5]))

    def external_kind(guid: str) -> str:
        if guid in package_guid_to_path:
            return "package"
        if re.fullmatch(r"0{16}[ef]0{15}", guid):
            return "unity-builtin"
        return "unresolved"

    for path in sorted(guid_to_path.values()):
        asset = ROOT / path
        if asset.suffix.lower() in FINGERPRINT_EXTENSIONS:
            fingerprint.update(path.encode("utf-8"))
            fingerprint.update(asset.read_bytes())
        if asset.suffix.lower() == ".cs":
            add_node("asset:" + path, asset.stem, "script", path)
        elif asset.suffix.lower() in {".asmdef", ".asmref", ".rsp"}:
            add_node("asset:" + path, asset.name, "data", path,
                     compilationTopology=asset.suffix.lower()[1:])
        elif asset.suffix.lower() in YAML_EXTENSIONS:
            kind = {".unity": "scene", ".prefab": "prefab", ".asset": "data",
                    ".controller": "animator", ".anim": "animation",
                    ".overridecontroller": "animator override"}[asset.suffix.lower()]
            add_node("asset:" + path, asset.stem, kind, path)

    for path in sorted(guid_to_path.values()):
        asset = ROOT / path
        if asset.suffix.lower() not in YAML_EXTENSIONS:
            continue
        raw = asset.read_text(encoding="utf-8-sig", errors="replace")
        source_id = "asset:" + path
        if source_id not in nodes:
            continue
        parts = list(documents(raw)) if asset.suffix.lower() in {".unity", ".prefab"} else []
        game_objects: dict[str, tuple[str, int]] = {}
        components: dict[str, tuple[str, str, int, str]] = {}
        transforms: dict[str, str] = {}
        for class_id, file_id, block, line in parts:
            if class_id == "1":
                name = first_field(block, "m_Name") or f"GameObject {file_id}"
                node_id = f"go:{path}#{file_id}"
                add_node(node_id, name, "gameobject", path, line, fileID=file_id)
                add_edge(source_id, "contains GameObject", node_id, path, line)
                game_objects[file_id] = (node_id, line)
            elif class_id in {"4", "224"}:
                parent_match = re.search(r"(?m)^  m_Father:\s*\{fileID:\s*(-?\d+)\}", block)
                if parent_match:
                    transforms[file_id] = parent_match.group(1)
            if class_id not in {"1", "1001", "4", "224"}:
                object_id = first_file_id(block, "m_GameObject")
                if object_id and object_id != "0":
                    typename = block.splitlines()[1].rstrip(":") if len(block.splitlines()) > 1 else f"Class {class_id}"
                    components[file_id] = (object_id, typename, line, block)
        for file_id, (object_id, typename, line, block) in components.items():
            go = game_objects.get(object_id)
            if not go:
                continue
            script_ref = re.search(r"(?m)^  m_Script:\s*\{[^\n]*guid:\s*([0-9a-f]{32})", block)
            script_path = guid_to_path.get(script_ref.group(1)) if script_ref else None
            label = Path(script_path).stem if script_path else typename
            node_id = f"component:{path}#{file_id}"
            add_node(node_id, label, "component", path, line, fileID=file_id, gameObject=go[0])
            add_edge(go[0], "has Component", node_id, path, line)
            if script_path:
                add_edge(node_id, "uses script", "asset:" + script_path, path, line)
            elif script_ref:
                guid = script_ref.group(1)
                classification = external_kind(guid)
                if classification == "package":
                    package_path = package_guid_to_path[guid]
                    package_id = "package:" + package_path
                    add_node(package_id, Path(package_path).stem, "package script", package_path)
                    add_edge(node_id, "uses package script", package_id, path, line)
                else:
                    unresolved.append({"kind": "script-guid-" + classification, "path": path,
                                       "line": line, "guid": guid, "component": node_id})
        for file_id, (_, _, line, block) in components.items():
            source_id = f"component:{path}#{file_id}"
            if source_id not in nodes:
                continue
            last_target = None
            for offset, text_line in enumerate(block.splitlines()):
                if "m_Target:" in text_line:
                    match = FILE_ID_RE.search(text_line)
                    last_target = match.group(1) if match else None
                if "m_MethodName:" not in text_line:
                    continue
                method = text_line.split("m_MethodName:", 1)[1].strip()
                if not method or not last_target or last_target == "0":
                    continue
                target_id = f"component:{path}#{last_target}"
                if target_id in nodes:
                    add_edge(source_id, "UnityEvent calls " + method, target_id,
                             path, line + offset)
                else:
                    unresolved.append({"kind": "unityevent-target-unresolved", "path": path,
                                       "line": line + offset, "fileID": last_target, "method": method})
        for class_id, file_id, block, line in parts:
            if class_id not in {"4", "224"}:
                continue
            parent = transforms.get(file_id)
            child_go = first_file_id(block, "m_GameObject")
            if not parent or parent == "0" or not child_go or child_go not in game_objects:
                continue
            parent_block = next((item[2] for item in parts if item[1] == parent), None)
            parent_go = first_file_id(parent_block, "m_GameObject") if parent_block else None
            if parent_go in game_objects:
                add_edge(game_objects[parent_go][0], "contains child", game_objects[child_go][0], path, line)
        part_index = 0
        for line_no, line_text in enumerate(raw.splitlines(), 1):
            while part_index + 1 < len(parts) and parts[part_index + 1][3] <= line_no:
                part_index += 1
            reference_source_id = "asset:" + path
            if parts and parts[part_index][3] <= line_no:
                class_id, file_id = parts[part_index][:2]
                if class_id == "1" and file_id in game_objects:
                    reference_source_id = game_objects[file_id][0]
                elif f"component:{path}#{file_id}" in nodes:
                    reference_source_id = f"component:{path}#{file_id}"
            for match in GUID_RE.finditer(line_text):
                guid = match.group(1)
                if guid == "0" * 32:
                    continue
                target_path = guid_to_path.get(guid)
                if target_path:
                    target_id = "asset:" + target_path
                    if target_id not in nodes:
                        target_asset = ROOT / target_path
                        add_node(target_id, target_asset.stem, "resource", target_path)
                    relation = "serialized GUID reference"
                    if "m_SourcePrefab" in line_text:
                        relation = "prefab source"
                    elif "m_Script" in line_text:
                        relation = "serialized script"
                    add_edge(reference_source_id, relation, target_id, path, line_no)
                else:
                    classification = external_kind(guid)
                    if classification == "package":
                        package_path = package_guid_to_path[guid]
                        package_id = "package:" + package_path
                        add_node(package_id, Path(package_path).stem, "package asset", package_path)
                        add_edge(reference_source_id, "package GUID reference", package_id, path, line_no)
                    elif classification == "unity-builtin":
                        counters["builtinGuidReferences"] += 1
                    else:
                        unresolved.append({"kind": "guid-unresolved", "path": path,
                                           "line": line_no, "guid": guid})
        if parts:
            counters["gameobjects"] += len(game_objects)
            counters["components"] += len(components)

    # This data proves textual references only. It never claims dynamic dispatch or a call graph.
    class_re = re.compile(r"\b(?:class|interface)\s+([A-Za-z_][A-Za-z_0-9]*)")
    classes: dict[str, str] = {}
    for node in list(nodes.values()):
        if node["kind"] != "script":
            continue
        raw = (ROOT / node["path"]).read_text(encoding="utf-8-sig", errors="replace")
        for match in class_re.finditer(raw):
            classes.setdefault(match.group(1), node["path"])
    patterns = [re.compile(r"\bGetComponent(?:InChildren|InParent)?<([A-Za-z_]\w*)>"),
                re.compile(r"\b(?:new|typeof)\s*\(?\s*([A-Za-z_]\w*)\b"),
                re.compile(r"\b([A-Za-z_]\w*)\.Instance\b")]
    for node in list(nodes.values()):
        if node["kind"] != "script":
            continue
        path = node["path"]
        for line_no, line_text in enumerate((ROOT / path).read_text(encoding="utf-8-sig", errors="replace").splitlines(), 1):
            stripped = line_text.strip()
            if stripped.startswith("//") or stripped.startswith("*"):
                continue
            for pattern in patterns:
                for match in pattern.finditer(line_text):
                    target_path = classes.get(match.group(1))
                    if target_path and target_path != path:
                        add_edge(node["id"], "code type reference", "asset:" + target_path,
                                 path, line_no, "medium", "textual only")

    for config_root in (ROOT / "Packages", ROOT / "ProjectSettings"):
        if config_root.is_dir():
            for config_file in sorted(p for p in config_root.rglob("*") if p.is_file()):
                fingerprint.update(relative(config_file).encode("utf-8"))
                fingerprint.update(config_file.read_bytes())
    fingerprint.update(Path(__file__).read_bytes())
    fingerprint.update(TEMPLATE.read_bytes())
    chain_path = Path(__file__).with_name("chain-relations.json")
    chain_details = {}
    if chain_path.exists():
        fingerprint.update(chain_path.read_bytes())
        for detail in json.loads(chain_path.read_text(encoding="utf-8")):
            key = (detail["relation"], detail.get("target"))
            if key in chain_details:
                raise ValueError(f"Duplicate chain relation: {key}")
            chain_details[key] = detail

    curated_path = Path(__file__).with_name("curated-relations.json")
    if curated_path.exists():
        fingerprint.update(curated_path.read_bytes())
        seen_relations = set()
        for item in json.loads(curated_path.read_text(encoding="utf-8")):
            specific_key = (item["relation"], item["target"])
            generic_key = (item["relation"], None)
            details_key = specific_key if specific_key in chain_details else generic_key
            if details_key in chain_details:
                seen_relations.add(details_key)
            item = {**item, **chain_details.get(details_key, {})}
            source = "asset:" + item["source"]
            target = "asset:" + item["target"]
            if source not in nodes or target not in nodes:
                raise ValueError(f"Curated relation has missing node: {item}")
            line = resolve_curated_line(ROOT, item)
            runtime_current = (not portable and runtime_evidence_is_current(item)
                               if item.get("runtimeEvidence") else False)
            if runtime_current:
                fingerprint.update((ROOT / item["runtimeEvidence"]["path"]).read_bytes())
            validation = item.get("validation", "source confirmed; runtime unverified")
            if item.get("runtimeEvidence") and not runtime_current:
                validation = "source confirmed; runtime evidence stale or unavailable"
            add_edge(source, item["relation"], target, item["evidence"]["path"],
                     line, "high", validation,
                     evidenceType="reviewed source line",
                     relationType=item.get("relationType", "call"),
                     chainIds=item.get("chainIds", []),
                     sourceMethod=item.get("sourceMethod", ""),
                     targetMethod=item.get("targetMethod", ""),
                     dataFlow=item.get("dataFlow", ""),
                     runtimeEvidence=item.get("runtimeEvidence") if runtime_current else None)
        unmatched = set(chain_details) - seen_relations
        if unmatched:
            raise ValueError(f"Chain metadata has no curated relation: {sorted(unmatched)}")

    notes_path = Path(__file__).with_name("node-notes.json")
    if notes_path.exists():
        fingerprint.update(notes_path.read_bytes())
        for item in json.loads(notes_path.read_text(encoding="utf-8")):
            node_id = "asset:" + item["path"]
            if node_id not in nodes:
                raise ValueError(f"Node note has missing node: {item}")
            source_lines = (ROOT / item["path"]).read_text(encoding="utf-8-sig", errors="replace").splitlines()
            if not 1 <= item["line"] <= len(source_lines):
                raise ValueError(f"Node note has invalid source line: {item}")
            nodes[node_id]["responsibility"] = item["responsibility"]
            nodes[node_id]["responsibilityEvidenceLine"] = item["line"]

    issues_path = Path(__file__).with_name("issues.json")
    issues = []
    if issues_path.exists():
        fingerprint.update(issues_path.read_bytes())
        for item in json.loads(issues_path.read_text(encoding="utf-8")):
            node_ids = ["asset:" + path for path in item["nodes"]]
            missing = [node_id for node_id in node_ids if node_id not in nodes]
            if missing:
                raise ValueError(f"Issue {item['id']} has missing nodes: {missing}")
            issues.append({**item, "nodes": node_ids})
    for edge in edges.values():
        edge["issueIds"] = [item["id"] for item in issues
                            if edge["source"] in item["nodes"] and edge["target"] in item["nodes"]]

    return {"schemaVersion": 1, "generatedAtUtc": dt.datetime.now(dt.timezone.utc).isoformat(),
            "root": "." if portable else str(ROOT),
            "head": source_context["head"] if source_context else git_head(),
            "sourceBranch": source_context["branch"] if source_context else ("main" if portable else subprocess.check_output(
                ["git", "branch", "--show-current"], cwd=ROOT, text=True).strip()),
            "snapshotMode": "fixed static copy" if source_context else ("repository" if portable else "local audit"),
            "sourceDirty": source_context["sourceDirty"] if source_context else source_is_dirty(),
            **({"snapshotId": source_context["snapshotId"]} if source_context else {}),
            "sourceSha256": fingerprint.hexdigest(),
            "nodes": list(nodes.values()), "issues": issues,
            "edges": list(edges.values()), "scan": {"assetCounts": dict(counters),
            "metaGuidCount": len(guid_to_path), "packageGuidCount": len(package_guid_to_path),
            "duplicateGuids": duplicated_guids,
            "unresolvedGuids": unresolved,
            "edgeValidationCounts": dict(collections.Counter(edge["validation"] for edge in edges.values())),
            "edgeRelationTypeCounts": dict(collections.Counter(edge["relationType"] for edge in edges.values())),
            "limits": ["Static YAML/text inspection only", "Unresolved GUID may belong to packages",
                       "Prefab overrides, managed references and runtime wiring require Unity Editor validation",
                       "Code type references are textual heuristics, not a semantic call graph"]}}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="Check reproducibility without writing files")
    parser.add_argument("--baseline", type=Path, help="Compare this scan with a saved graph.json snapshot")
    parser.add_argument("--portable", action="store_true", help="Reproducible main snapshot without local test/cache dependencies")
    parser.add_argument("--candidates", action="store_true", help="Read-only responsibility candidates from this scan")
    parser.add_argument("--event-candidates", action="store_true", help="Read-only repeated event subscription candidates")
    parser.add_argument("--output-dir", type=Path, help="Stage outputs in another directory")
    parser.add_argument("--source-context", type=Path, help="Origin metadata for a verified static copy")
    args = parser.parse_args()
    context = json.loads(args.source_context.read_text(encoding="utf-8")) if args.source_context else None
    if context and (not args.portable or not all(context.get(key) for key in ("head", "snapshotId"))
                    or "branch" not in context):
        raise ValueError("Source context requires portable scan and complete origin identity")
    graph = build_graph(portable=args.portable, source_context=context)
    if args.candidates:
        print(json.dumps(responsibility_candidates(graph), ensure_ascii=False, indent=2))
        return
    if args.event_candidates:
        print(json.dumps(event_subscription_candidates(graph, ROOT), ensure_ascii=False, indent=2))
        return
    node_ids = {node["id"] for node in graph["nodes"]}
    line_counts = {}
    for edge in graph["edges"]:
        if edge["source"] not in node_ids or edge["target"] not in node_ids:
            raise ValueError(f"Edge has missing endpoint: {edge}")
        path = edge["evidence"]["path"]
        if path not in line_counts:
            source_file = ROOT / path
            if not source_file.is_file():
                raise ValueError(f"Edge evidence file is missing: {path}")
            line_counts[path] = len(source_file.read_text(encoding="utf-8-sig", errors="replace").splitlines())
        if not 1 <= edge["evidence"]["line"] <= line_counts[path]:
            raise ValueError(f"Edge evidence line is invalid: {edge}")
    output_dir = args.output_dir or OUT
    output_dir.mkdir(parents=True, exist_ok=True)
    graph_path = output_dir / "graph.json"
    html_path = output_dir / "viewer.html"
    graph_text = json.dumps(graph, ensure_ascii=False, indent=2) + "\n"
    embedded = json.dumps(graph, ensure_ascii=False, separators=(",", ":")).replace("</", "<\\/")
    html_text = TEMPLATE.read_text(encoding="utf-8").replace("/*__GRAPH_DATA__*/", embedded)
    if args.baseline:
        old = json.loads(args.baseline.read_text(encoding="utf-8"))
        old_nodes = {n["id"]: n for n in old["nodes"]}
        new_nodes = {n["id"]: n for n in graph["nodes"]}
        edge_key = lambda e: (e["source"], e["relation"], e["target"],
                              e["evidence"]["path"], e["evidence"]["line"])
        old_edges = {edge_key(e) for e in old["edges"]}
        new_edges = {edge_key(e) for e in graph["edges"]}
        print(json.dumps({"comparison": str(args.baseline),
                          "addedNodes": sorted(new_nodes.keys() - old_nodes.keys()),
                          "removedNodes": sorted(old_nodes.keys() - new_nodes.keys()),
                          "changedNodes": sorted(k for k in old_nodes.keys() & new_nodes.keys()
                                                 if old_nodes[k] != new_nodes[k]),
                          "addedEdges": len(new_edges - old_edges),
                          "removedEdges": len(old_edges - new_edges)}, ensure_ascii=False))
    if args.check:
        current = json.loads(graph_path.read_text(encoding="utf-8")) if graph_path.exists() else None
        saved_time = current.get("generatedAtUtc") if current else None
        if current:
            current.pop("generatedAtUtc", None)
        expected = dict(graph)
        expected.pop("generatedAtUtc", None)
        if args.portable and current:
            # A commit containing generated files necessarily has a later HEAD.
            expected["head"] = current.get("head")
        saved_graph = {**graph, "generatedAtUtc": saved_time}
        if args.portable and current:
            saved_graph["head"] = current.get("head")
        saved_embedded = json.dumps(saved_graph, ensure_ascii=False,
                                   separators=(",", ":")).replace("</", "<\\/")
        saved_html = TEMPLATE.read_text(encoding="utf-8").replace("/*__GRAPH_DATA__*/", saved_embedded)
        ok = current == expected and html_path.exists() and html_path.read_text(encoding="utf-8") == saved_html
        print(json.dumps({"upToDate": ok, "nodes": len(graph["nodes"]), "edges": len(graph["edges"]),
                          "unresolved": len(graph["scan"]["unresolvedGuids"])}, ensure_ascii=False))
        raise SystemExit(0 if ok else 1)
    graph_path.write_text(graph_text, encoding="utf-8")
    html_path.write_text(html_text, encoding="utf-8")
    print(json.dumps({"nodes": len(graph["nodes"]), "edges": len(graph["edges"]),
                      "unresolved": len(graph["scan"]["unresolvedGuids"]), "viewer": str(html_path)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
