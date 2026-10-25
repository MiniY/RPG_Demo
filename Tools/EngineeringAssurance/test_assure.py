"""Fault injection in disposable Git repositories; never touches the Unity project."""

import json
import subprocess
import tempfile
import unittest
from argparse import Namespace
from pathlib import Path
from unittest.mock import patch

import assure


def passed_xml(count=1):
    cases = "".join(f'<test-case name="case-{i}" result="Passed"/>' for i in range(count))
    return f'<test-run result="Passed" total="{count}" passed="{count}" failed="0" skipped="0" inconclusive="0">{cases}</test-run>'


class AssuranceTests(unittest.TestCase):
    def setUp(self):
        fixture_root = Path(__file__).resolve().parent / ".test-fixtures"
        fixture_root = fixture_root.resolve()
        assert fixture_root.is_relative_to(Path(__file__).resolve().parents[2])
        fixture_root.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=fixture_root)
        self.addCleanup(self.temp.cleanup)
        self.repo = Path(self.temp.name)
        for command in (["git", "init", "-q", str(self.repo)],
                        ["git", "-C", str(self.repo), "config", "user.name", "Test"],
                        ["git", "-C", str(self.repo), "config", "user.email", "test@example.invalid"]):
            subprocess.run(command, check=True, capture_output=True)
        (self.repo / "Assets/Scripts").mkdir(parents=True)
        (self.repo / "Assets/Scripts/ExistingService.cs").write_text("existing", encoding="utf-8")
        subprocess.run(["git", "-C", str(self.repo), "add", "Assets"], check=True, capture_output=True)
        subprocess.run(["git", "-C", str(self.repo), "commit", "-qm", "baseline"], check=True, capture_output=True)

    def preflight(self, risk="L0", scope=None):
        output = self.repo / ".codex/base.json"
        args = Namespace(repo=self.repo, task="case-1", risk=risk, intent="test", scope=scope or ["Assets/Scripts/"],
                         expected_branch=None, out=str(output))
        self.assertEqual(assure.preflight(args), 2 if risk == "L2" else 0)
        return output

    def verify(self, baseline, record):
        record = {**record, "baselineId": json.loads(baseline.read_text(encoding="utf-8"))["baselineId"]}
        record_path = self.repo / ".codex/record.json"
        record_path.write_text(json.dumps(record), encoding="utf-8")
        report = self.repo / f".codex/report-{len(list((self.repo / '.codex').glob('report-*.json')))}.json"
        code = assure.verify(Namespace(repo=self.repo, baseline=baseline, record=record_path, out=str(report)))
        return code, json.loads(report.read_text(encoding="utf-8"))

    def test_existing_dirty_file_is_preserved_and_later_change_detected(self):
        file = self.repo / "Assets/Scripts/ExistingService.cs"
        file.write_text("user work", encoding="utf-8")
        baseline = self.preflight(scope=["Docs/"])
        code, report = self.verify(baseline, {"taskId": "case-1"})
        self.assertEqual(code, 0)
        self.assertEqual(report["filesChangedSincePreflight"], [])
        file.write_text("user work plus task", encoding="utf-8")
        code, report = self.verify(baseline, {"taskId": "case-1"})
        self.assertEqual(code, 2)
        self.assertEqual(report["outsideAuthorizedScope"], ["Assets/Scripts/ExistingService.cs"])

    def test_new_file_outside_scope_is_rejected(self):
        baseline = self.preflight()
        (self.repo / "ProjectSettings").mkdir()
        (self.repo / "ProjectSettings/ProjectSettings.asset").write_text("changed", encoding="utf-8")
        code, report = self.verify(baseline, {"taskId": "case-1"})
        self.assertEqual(code, 2)
        self.assertEqual(report["scopeStatus"], "FAIL")

    def test_wrong_branch_blocks_reuse_of_baseline(self):
        baseline = self.preflight()
        subprocess.run(["git", "-C", str(self.repo), "switch", "-qc", "other"], check=True, capture_output=True)
        code, report = self.verify(baseline, {"taskId": "case-1"})
        self.assertEqual(code, 2)
        self.assertEqual(report["identityStatus"], "BLOCKED")

    def test_xml_pass_with_exit_timeout_cannot_pass(self):
        baseline = self.preflight()
        xml = self.repo / ".codex/test.xml"
        xml.write_text(passed_xml(), encoding="utf-8")
        (self.repo / ".codex/runner.json").write_text('{"exitCode":0,"timedOut":true}', encoding="utf-8")
        fp = assure.source_fingerprint(assure.git(self.repo, "rev-parse", "HEAD"), assure.dirty_state(self.repo))
        record = {"taskId": "case-1", "tests": [{"name": "unity", "status": "PASS", "xml": ".codex/test.xml",
                   "runnerEvidence": ".codex/runner.json", "sourceFingerprint": fp}]}
        code, report = self.verify(baseline, record)
        self.assertEqual(code, 2)
        self.assertEqual(report["deterministicVerification"], "TIMEOUT")

    def test_missing_test_evidence_rejects_pass_claim(self):
        baseline = self.preflight()
        record = {"taskId": "case-1", "tests": [{"name": "unity", "status": "PASS",
                   "sourceFingerprint": assure.source_fingerprint(assure.git(self.repo, "rev-parse", "HEAD"), assure.dirty_state(self.repo))}]}
        code, report = self.verify(baseline, record)
        self.assertEqual(code, 2)
        self.assertEqual(report["tests"][0]["status"], "FAIL")

    def test_stale_test_fingerprint_blocks_pass(self):
        baseline = self.preflight()
        xml = self.repo / ".codex/test.xml"
        xml.write_text(passed_xml(), encoding="utf-8")
        record = {"taskId": "case-1", "tests": [{"name": "unity", "status": "PASS", "xml": ".codex/test.xml",
                   "runnerEvidence": ".codex/runner.json", "sourceFingerprint": "old"}]}
        code, report = self.verify(baseline, record)
        self.assertEqual(code, 2)
        self.assertEqual(report["deterministicVerification"], "BLOCKED")
        self.assertEqual(report["tests"][0]["status"], "BLOCKED")

    def test_l0_does_not_run_architecture_scanner(self):
        baseline = self.preflight(risk="L0")
        self.assertEqual(json.loads(baseline.read_text())["architectureCheck"]["status"], "NOT_RUN")

    def test_unsubstantiated_human_pass_is_unknown(self):
        baseline = self.preflight()
        _, report = self.verify(baseline, {"taskId": "case-1", "humanAcceptance": {"status": "PASS"}})
        self.assertEqual(report["humanAcceptance"], "UNKNOWN")

    def test_valid_nunit_and_natural_exit_are_deterministic_pass_only(self):
        baseline = self.preflight()
        xml = self.repo / ".codex/test.xml"
        xml.write_text(passed_xml(2), encoding="utf-8")
        (self.repo / ".codex/runner.json").write_text('{"exitCode":0,"timedOut":false}', encoding="utf-8")
        fp = assure.source_fingerprint(assure.git(self.repo, "rev-parse", "HEAD"), assure.dirty_state(self.repo))
        record = {"taskId": "case-1", "tests": [{"name": "unity", "status": "PASS", "xml": ".codex/test.xml",
                   "runnerEvidence": ".codex/runner.json", "sourceFingerprint": fp}]}
        code, report = self.verify(baseline, record)
        self.assertEqual(code, 0)
        self.assertEqual(report["deterministicVerification"], "PASS")
        self.assertEqual(report["humanAcceptance"], "NOT_RUN")
        self.assertEqual(report["overall"], "PARTIAL")

    def test_l2_architecture_unavailable_blocks_completion(self):
        baseline = self.preflight(risk="L2")
        code, report = self.verify(baseline, {"taskId": "case-1"})
        self.assertEqual(code, 2)
        self.assertEqual(report["architectureStatus"], "UNKNOWN")
        self.assertEqual(report["overall"], "BLOCKED")

    def test_expected_branch_mismatch_blocks_preflight(self):
        output = self.repo / ".codex/branch.json"
        args = Namespace(repo=self.repo, task="branch-check", risk="L0", intent="test", scope=["Assets/Scripts/"],
                         expected_branch="main", out=str(output))
        self.assertEqual(assure.preflight(args), 2)
        self.assertEqual(json.loads(output.read_text())["branchStatus"], "BLOCKED")

    def test_l3_without_human_approval_stays_blocked(self):
        scanner = self.repo / "Tools/ArchitectureAudit/scan.py"
        scanner.parent.mkdir(parents=True)
        scanner.write_text("print('up to date')\n", encoding="utf-8")
        baseline = self.preflight(risk="L3")
        code, report = self.verify(baseline, {"taskId": "case-1"})
        self.assertEqual(code, 2)
        self.assertEqual(report["architectureStatus"], "PASS")
        self.assertEqual(report["humanApproval"], "UNKNOWN")
        self.assertEqual(report["overall"], "BLOCKED")

    def test_runner_without_timeout_or_exit_status_rejects_pass(self):
        baseline = self.preflight()
        (self.repo / ".codex/runner.json").write_text('{"exitCode":0}', encoding="utf-8")
        (self.repo / ".codex/test.xml").write_text(passed_xml(), encoding="utf-8")
        fp = assure.source_fingerprint(assure.git(self.repo, "rev-parse", "HEAD"), assure.dirty_state(self.repo))
        code, report = self.verify(baseline, {"taskId": "case-1", "tests": [{"name": "unity", "status": "PASS",
            "xml": ".codex/test.xml", "runnerEvidence": ".codex/runner.json", "sourceFingerprint": fp}]})
        self.assertEqual(code, 2)
        self.assertEqual(report["deterministicVerification"], "FAIL")

    def test_same_task_cannot_rebaseline_after_scope_failure(self):
        baseline = self.preflight(scope=["Docs/"])
        (self.repo / "Assets/Scripts/ExistingService.cs").write_text("outside", encoding="utf-8")
        code, report = self.verify(baseline, {"taskId": "case-1"})
        self.assertEqual((code, report["scopeStatus"]), (2, "FAIL"))
        with self.assertRaisesRegex(ValueError, "already exists"):
            assure.preflight(Namespace(repo=self.repo, task="case-1", risk="L0", intent="retry",
                scope=["Assets/Scripts/"], expected_branch=None, out=str(self.repo / ".codex/new-base.json")))
        self.assertEqual(json.loads(baseline.read_text())["authorizedScope"], ["Docs/"])

    def test_reverting_outside_change_preserves_prior_failure(self):
        baseline = self.preflight(scope=["Docs/"])
        file = self.repo / "Assets/Scripts/ExistingService.cs"
        file.write_text("outside", encoding="utf-8")
        self.verify(baseline, {"taskId": "case-1"})
        file.write_text("existing", encoding="utf-8")
        code, report = self.verify(baseline, {"taskId": "case-1"})
        self.assertEqual((code, report["scopeStatus"]), (2, "FAIL"))
        self.assertEqual(report["outsideAuthorizedScope"], [])
        self.assertEqual(report["historicalScopeViolations"], ["Assets/Scripts/ExistingService.cs"])

    def test_new_task_can_take_legitimate_new_baseline(self):
        first = self.preflight(scope=["Docs/"])
        second = self.repo / ".codex/second.json"
        self.assertEqual(assure.preflight(Namespace(repo=self.repo, task="case-2", risk="L0", intent="new",
            scope=["Assets/Scripts/"], expected_branch=None, out=str(second))), 0)
        self.assertNotEqual(json.loads(first.read_text())["baselineId"], json.loads(second.read_text())["baselineId"])

    def test_approved_extension_uses_new_id_and_retains_old_failure(self):
        first = self.preflight(scope=["Docs/"])
        (self.repo / "Assets/Scripts/ExistingService.cs").write_text("expanded work", encoding="utf-8")
        old_code, old_report = self.verify(first, {"taskId": "case-1"})
        self.assertEqual((old_code, old_report["scopeStatus"]), (2, "FAIL"))
        # External human approval is represented by the caller choosing a new task ID;
        # this fixture proves only that the old failure remains inspectable.
        second = self.repo / ".codex/approved-extension.json"
        self.assertEqual(assure.preflight(Namespace(repo=self.repo, task="case-2", risk="L0",
            intent="human-approved continuation", scope=["Assets/Scripts/"],
            expected_branch=None, out=str(second))), 0)
        self.assertTrue((self.repo / ".codex/report-0.json").is_file())
        self.assertEqual(json.loads((self.repo / ".codex/report-0.json").read_text())["scopeStatus"], "FAIL")

    def test_existing_untracked_file_changes_and_rename_are_detected(self):
        extra = self.repo / "Assets/Scripts/Draft.cs"
        extra.write_text("draft", encoding="utf-8")
        baseline = self.preflight(scope=["Docs/"])
        extra.rename(self.repo / "Assets/Scripts/Renamed.cs")
        code, report = self.verify(baseline, {"taskId": "case-1"})
        self.assertEqual(code, 2)
        self.assertEqual(report["outsideAuthorizedScope"], ["Assets/Scripts/Draft.cs", "Assets/Scripts/Renamed.cs"])

    def test_tracked_delete_is_detected(self):
        baseline = self.preflight(scope=["Docs/"])
        (self.repo / "Assets/Scripts/ExistingService.cs").unlink()
        code, report = self.verify(baseline, {"taskId": "case-1"})
        self.assertEqual(code, 2)
        self.assertEqual(report["outsideAuthorizedScope"], ["Assets/Scripts/ExistingService.cs"])

    def test_head_change_blocks_baseline_reuse(self):
        baseline = self.preflight()
        (self.repo / "Assets/Scripts/ExistingService.cs").write_text("committed", encoding="utf-8")
        subprocess.run(["git", "-C", str(self.repo), "commit", "-qam", "next"], check=True, capture_output=True)
        code, report = self.verify(baseline, {"taskId": "case-1"})
        self.assertEqual((code, report["identityStatus"]), (2, "BLOCKED"))

    def test_missing_or_overwritten_prior_report_blocks_next_verify(self):
        baseline = self.preflight()
        _, report = self.verify(baseline, {"taskId": "case-1"})
        report_path = self.repo / ".codex/report-0.json"
        report_path.write_text(json.dumps(report | {"scopeStatus": "PASS forged"}), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "report missing or changed"):
            self.verify(baseline, {"taskId": "case-1"})
        report_path.unlink()
        with self.assertRaisesRegex(ValueError, "report missing or changed"):
            self.verify(baseline, {"taskId": "case-1"})

    def test_baseline_alias_tamper_and_wrong_record_id_are_rejected(self):
        baseline = self.preflight()
        bad = self.repo / ".codex/other-baseline.json"
        content = json.loads(baseline.read_text(encoding="utf-8"))
        bad.write_text(json.dumps(content | {"authorizedScope": ["*"]}), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "content differs"):
            self.verify(bad, {"taskId": "case-1"})
        record_path = self.repo / ".codex/wrong.json"
        record_path.write_text(json.dumps({"taskId": "case-1", "baselineId": "wrong"}), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "baseline ID differs"):
            assure.verify(Namespace(repo=self.repo, baseline=baseline, record=record_path,
                                    out=str(self.repo / ".codex/not-written.json")))

    def test_record_cannot_expand_scope(self):
        baseline = self.preflight(scope=["Docs/"])
        with self.assertRaisesRegex(ValueError, "authorized scope"):
            self.verify(baseline, {"taskId": "case-1", "authorizedScope": ["Assets/Scripts/"]})

    def test_self_declared_approval_does_not_release_l3(self):
        scanner = self.repo / "Tools/ArchitectureAudit/scan.py"
        scanner.parent.mkdir(parents=True)
        scanner.write_text("print('up to date')\n", encoding="utf-8")
        baseline = self.preflight(risk="L3")
        code, report = self.verify(baseline, {"taskId": "case-1", "humanApproval":
            {"approver": "somebody", "evidence": ".codex/claim.json"}})
        self.assertEqual((code, report["humanApproval"], report["overall"]), (2, "UNKNOWN", "BLOCKED"))

    def test_inconsistent_nunit_counts_and_cases_fail(self):
        baseline = self.preflight()
        (self.repo / ".codex/test.xml").write_text(
            '<test-run result="Passed" total="2" passed="2" failed="0"><test-case result="Passed"/></test-run>',
            encoding="utf-8")
        (self.repo / ".codex/runner.json").write_text('{"exitCode":0,"timedOut":false}', encoding="utf-8")
        fp = assure.source_fingerprint(assure.git(self.repo, "rev-parse", "HEAD"), assure.dirty_state(self.repo))
        code, report = self.verify(baseline, {"taskId": "case-1", "tests": [{"name": "unity", "status": "PASS",
            "xml": ".codex/test.xml", "runnerEvidence": ".codex/runner.json", "sourceFingerprint": fp}]})
        self.assertEqual((code, report["deterministicVerification"]), (2, "FAIL"))

    def test_architecture_permission_error_degrades_to_unknown(self):
        scanner = self.repo / "Tools/ArchitectureAudit/scan.py"
        scanner.parent.mkdir(parents=True)
        scanner.write_text("print('ready')\n", encoding="utf-8")
        with patch.object(assure.subprocess, "run", side_effect=PermissionError("denied")):
            check = assure.architecture_check(self.repo)
        self.assertEqual(check["status"], "UNKNOWN")

    def test_architecture_timeout_is_bounded(self):
        scanner = self.repo / "Tools/ArchitectureAudit/scan.py"
        scanner.parent.mkdir(parents=True)
        scanner.write_text("print('ready')\n", encoding="utf-8")
        with patch.object(assure.subprocess, "run",
                          side_effect=subprocess.TimeoutExpired("scan.py", 90)) as run:
            check = assure.architecture_check(self.repo)
        self.assertEqual(check["status"], "TIMEOUT")
        self.assertEqual(run.call_args.kwargs["timeout"], 90)


if __name__ == "__main__":
    unittest.main()
