import os
import shutil
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import i1_postbuild_a41 as rule
import run_i1


class PortableEditorTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="I1 different checkout ")
        self.addCleanup(self.temp.cleanup)
        self.project = Path(self.temp.name)
        (self.project / "ProjectSettings").mkdir()
        (self.project / "ProjectSettings/ProjectVersion.txt").write_text(
            "m_EditorVersion: 2022.3.62f3c1\n", encoding="utf-8")
        self.editor = self.project / "Unusual Unity installation/Editor/Unity.exe"
        self.editor.parent.mkdir(parents=True)
        self.editor.write_bytes(b"static version probe is mocked; never executed")

    def test_explicit_editor_with_spaces_is_resolved_without_launching_unity(self):
        with patch("run_i1.unity_product_version", return_value="2022.3.62f3c1") as probe:
            self.assertEqual(run_i1.resolve_unity_editor(self.project, self.editor), self.editor.resolve())
            probe.assert_called_once_with(self.editor.resolve())

    def test_mismatched_version_is_rejected(self):
        with patch("run_i1.unity_product_version", return_value="2022.3.63f1"):
            with self.assertRaisesRegex(RuntimeError, "UnityEditorVersionMismatch"):
                run_i1.resolve_unity_editor(self.project, self.editor)

    def test_explicit_missing_editor_does_not_fall_back_to_environment(self):
        with patch.dict(os.environ, {"RPG_I1_UNITY": str(self.editor)}):
            with self.assertRaisesRegex(RuntimeError, "UnityEditorRequired"):
                run_i1.resolve_unity_editor(self.project, self.project / "missing/Unity.exe")

    def test_configured_editor_is_used_when_cli_is_absent(self):
        with patch.dict(os.environ, {"RPG_I1_UNITY": str(self.editor)}), \
                patch("run_i1.unity_product_version", return_value="2022.3.62f3c1"):
            self.assertEqual(run_i1.resolve_unity_editor(self.project), self.editor.resolve())

    def test_wrong_executable_name_is_rejected_before_version_probe(self):
        wrong = self.editor.with_name("Other.exe")
        wrong.write_bytes(b"not unity")
        with patch("run_i1.unity_product_version") as probe:
            with self.assertRaisesRegex(RuntimeError, "UnityEditorRequired"):
                run_i1.resolve_unity_editor(self.project, wrong)
            probe.assert_not_called()

    def test_version_probe_reads_file_metadata_and_does_not_run_editor(self):
        result = type("Probe", (), {"stdout": "2022.3.62f3c1_1623fc0bbb97\n"})()
        with patch("run_i1.subprocess.run", return_value=result) as run:
            self.assertEqual(run_i1.unity_product_version(self.editor), "2022.3.62f3c1")
            args, kwargs = run.call_args
            self.assertEqual(args[0][0], "powershell.exe")
            self.assertIn("FileVersionInfo", args[0][-1])
            self.assertEqual(kwargs["env"]["RPG_I1_EDITOR_PROBE"], str(self.editor))


class PortableReviewedRuleTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="I1 cold rules ")
        self.addCleanup(self.temp.cleanup)
        self.project = Path(self.temp.name)
        self.source = Path(run_i1.__file__).parent / "ReviewedRules/FontCache-a41"
        self.bundle = self.project / "Tools/MapVNext/ReviewedRules/FontCache-a41"
        shutil.copytree(self.source, self.bundle)

    def test_exact_reviewed_pair_works_without_private_artifacts(self):
        self.assertFalse((self.project / "Artifacts").exists())
        self.assertEqual(run_i1.font_rule_evidence(self.project, rule), self.bundle)
        for name in ("qualification.json", "independent-review.json"):
            self.assertEqual(run_i1.sha(self.bundle / name), rule.QUAL_BINDINGS[name])

    def test_tampered_rule_is_rejected_even_if_old_private_pair_is_valid(self):
        old = self.project / rule.QUAL_REL
        shutil.copytree(self.source, old)
        (self.bundle / "qualification.json").write_text("{}", encoding="utf-8")
        with self.assertRaisesRegex(RuntimeError, "OriginalCacheRuleEvidenceChanged"):
            run_i1.font_rule_evidence(self.project, rule)

    def test_incomplete_bundle_cannot_mix_with_private_evidence(self):
        old = self.project / rule.QUAL_REL
        shutil.copytree(self.source, old)
        (self.bundle / "independent-review.json").unlink()
        with self.assertRaisesRegex(RuntimeError, "OriginalCacheRuleEvidenceChanged"):
            run_i1.font_rule_evidence(self.project, rule)

    def test_historical_checkout_can_still_use_its_exact_pair(self):
        old = self.project / rule.QUAL_REL
        shutil.copytree(self.bundle, old)
        shutil.rmtree(self.bundle)
        self.assertEqual(run_i1.font_rule_evidence(self.project, rule), old)

    def test_missing_entire_pair_is_rejected(self):
        shutil.rmtree(self.bundle)
        with self.assertRaisesRegex(RuntimeError, "OriginalCacheRuleEvidenceChanged"):
            run_i1.font_rule_evidence(self.project, rule)


if __name__ == "__main__":
    unittest.main()
