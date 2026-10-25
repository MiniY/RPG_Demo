"""Small source-location regression tests; no Unity project writes."""

import tempfile
import unittest
from pathlib import Path

from scan import event_subscription_candidates, resolve_curated_line, responsibility_candidates


class CuratedEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = self.root / "Example.cs"
        self.proof = {"evidence": {"path": "Example.cs", "line": 2, "contains": "target.Apply"}}

    def test_unique_moved_call_uses_current_line(self):
        self.source.write_text("void Run() {\n    Prepare();\n    target.Apply();\n}\n")
        self.assertEqual(resolve_curated_line(self.root, self.proof), 3)

    def test_missing_call_cannot_pass(self):
        self.source.write_text("void Run() {\n    Prepare();\n}\n")
        with self.assertRaisesRegex(ValueError, "missing or ambiguous"):
            resolve_curated_line(self.root, self.proof)

    def test_ambiguous_move_cannot_choose_another_call(self):
        self.source.write_text("void Run() {\n    Prepare();\n    target.Apply();\n    target.Apply();\n}\n")
        with self.assertRaisesRegex(ValueError, "missing or ambiguous"):
            resolve_curated_line(self.root, self.proof)

    def test_comment_is_not_call_evidence(self):
        self.source.write_text("void Run() {\n    // target.Apply();\n}\n")
        with self.assertRaisesRegex(ValueError, "missing or ambiguous"):
            resolve_curated_line(self.root, self.proof)

    def test_existing_anchor_resolves_ambiguous_text(self):
        self.source.write_text("void Run() {\n    target.Apply();\n    target.Apply();\n}\n")
        self.assertEqual(resolve_curated_line(self.root, self.proof), 2)

    def test_different_names_with_shared_writer_become_candidate(self):
        edges = [{"source": f"asset:{name}.cs", "target": "asset:Store.cs", "targetMethod": "Store.TrySave",
                  "relation": "submits state", "evidenceType": "reviewed source line",
                  "evidence": {"path": f"{name}.cs", "line": 4}, "validation": "source confirmed"}
                 for name in ("Inventory", "Session")]
        result = responsibility_candidates({"edges": edges, "sourceSha256": "x", "head": "h"})
        self.assertEqual(len(result["candidates"]), 1)
        self.assertTrue(result["candidates"][0]["humanDecisionRequired"])

    def test_name_similarity_and_unreviewed_edges_do_not_create_candidate(self):
        edges = [{"source": f"asset:Manager{name}.cs", "target": "asset:Store.cs",
                  "targetMethod": "Store.TrySave", "relation": "code type reference",
                  "evidenceType": "C# text match", "evidence": {"path": f"Manager{name}.cs", "line": 4}}
                 for name in ("A", "B")]
        result = responsibility_candidates({"edges": edges, "sourceSha256": "x", "head": "h"})
        self.assertEqual(result["candidates"], [])

    def test_repeated_event_subscription_needs_production_reference(self):
        path = "Assets/Scripts/Listener.cs"
        source = self.root / path
        source.parent.mkdir(parents=True)
        source.write_text("class Listener {\n EventHub.Changed += Handle;\n"
                          " EventHub.Changed += Handle;\n EventHub.Changed -= Handle;\n"
                          " private void Handle() {}\n}\n")
        graph = {"nodes": [{"id": "asset:" + path, "kind": "script", "path": path}],
                 "edges": [], "sourceSha256": "x", "head": "h"}
        self.assertEqual(event_subscription_candidates(graph, self.root)["candidates"], [])
        graph["edges"].append({"target": "asset:" + path, "relation": "uses script",
                               "evidence": {"path": "Assets/Scene.prefab", "line": 7}})
        found = event_subscription_candidates(graph, self.root)["candidates"]
        self.assertEqual(len(found), 1)
        self.assertEqual([item["line"] for item in found[0]["codeEvidence"]], [2, 3])
        self.assertEqual(found[0]["unsubscribeCount"], 1)
        self.assertEqual(found[0]["handlerMethodEvidence"][0]["line"], 5)

    def test_comments_and_single_subscription_are_not_candidates(self):
        path = "Assets/Scripts/Listener.cs"
        source = self.root / path
        source.parent.mkdir(parents=True)
        source.write_text("// EventHub.Changed += Handle;\nEventHub.Changed += Handle;\n")
        graph = {"nodes": [{"id": "asset:" + path, "kind": "script", "path": path}],
                 "edges": [{"target": "asset:" + path, "relation": "uses script",
                            "evidence": {"path": "Assets/Scene.prefab", "line": 7}}],
                 "sourceSha256": "x", "head": "h"}
        self.assertEqual(event_subscription_candidates(graph, self.root)["candidates"], [])

    def test_repeated_arithmetic_assignment_without_handler_method_is_ignored(self):
        path = "Assets/Scripts/Counter.cs"
        source = self.root / path
        source.parent.mkdir(parents=True)
        source.write_text("class Counter {\n amount += delta;\n amount += delta;\n}\n")
        graph = {"nodes": [{"id": "asset:" + path, "kind": "script", "path": path}],
                 "edges": [{"target": "asset:" + path, "relation": "uses script",
                            "evidence": {"path": "Assets/Scene.prefab", "line": 7}}],
                 "sourceSha256": "x", "head": "h"}
        self.assertEqual(event_subscription_candidates(graph, self.root)["candidates"], [])


if __name__ == "__main__":
    unittest.main()
