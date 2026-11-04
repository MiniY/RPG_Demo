import unittest,hashlib
from seal_i1_host import require_review,require_closure,unreviewed_sources,require_baseline_delta
class SealReviewTests(unittest.TestCase):
    def review(self):return dict(reviewedSnapshotManifestSha256='fixed',reviewer='independent',reviewPerformed=True,sealEligible=True,status='STATIC_REVIEW_COMPLETE_NO_OPEN_FINDINGS',openFindings=0)
    def test_exact_eligible_static_review_allows_controlled_trial(self):require_review(self.review(),'fixed')
    def test_changes_required_cannot_be_sealed(self):
        review=self.review();review['status']='CHANGES_REQUIRED'
        with self.assertRaises(RuntimeError):require_review(review,'fixed')
    def test_open_finding_cannot_be_sealed(self):
        review=self.review();review['openFindings']=1
        with self.assertRaises(RuntimeError):require_review(review,'fixed')
    def test_stale_or_unperformed_review_cannot_be_sealed(self):
        with self.assertRaises(RuntimeError):require_review(self.review(),'different')
        review=self.review();review['reviewPerformed']=False
        with self.assertRaises(RuntimeError):require_review(review,'fixed')
    def test_missing_or_boolean_open_finding_count_is_not_zero_integer(self):
        for value in [None,False,True]:
            review=self.review();review['openFindings']=value
            with self.assertRaises(RuntimeError):require_review(review,'fixed')
        review=self.review();del review['openFindings']
        with self.assertRaises(RuntimeError):require_review(review,'fixed')
    def test_unapproved_review_cannot_be_sealed(self):
        review=self.review();review['sealEligible']=False
        with self.assertRaises(RuntimeError):require_review(review,'fixed')
class ClosureReviewTests(unittest.TestCase):
    def test_exact_reviewed_closure_is_parsed_from_the_same_bytes(self):
        body=b'{"closedExistingSourcePaths": []}'
        document,digest=require_closure({'sourceClosureBeforeSha256':hashlib.sha256(body).hexdigest()},body)
        self.assertEqual(document['closedExistingSourcePaths'],[])
    def test_drifted_or_missing_closure_is_rejected(self):
        for snapshot in [{},{'sourceClosureBeforeSha256':'unknown'}]:
            with self.assertRaises(RuntimeError):require_closure(snapshot,b'{}')
class CompilationInventoryTests(unittest.TestCase):
    def test_nul_inventory_preserves_chinese_spaces_and_newlines(self):
        paths=['Assets/临时验证/Probe.cs','Assets/space name/Probe.cs','Assets/new\nline/Probe.asmdef']
        encoded=('\0'.join(paths)+'\0').encode('utf-8')
        self.assertEqual(unreviewed_sources(encoded,set()),paths)
        self.assertEqual(unreviewed_sources(encoded,set(paths)),[])
class CapturedBaselineTests(unittest.TestCase):
    def test_only_declared_baseline_changes_are_allowed(self):
        self.assertEqual(require_baseline_delta({'Assets/a':'old'},{'Assets/a':'new'},['Assets/a']),['Assets/a'])
        with self.assertRaises(RuntimeError):require_baseline_delta({'Assets/a':'old'},{'Assets/a':'new'},[])
    def test_additions_and_deletions_are_not_implicit(self):
        with self.assertRaises(RuntimeError):require_baseline_delta({}, {'Assets/new':'x'}, [])
        with self.assertRaises(RuntimeError):require_baseline_delta({'Assets/old':'x'}, {}, [])
class CapturedCandidateTests(unittest.TestCase):
    def setUp(self):
        import tempfile,json
        from pathlib import Path
        self.temp=tempfile.TemporaryDirectory(prefix='captured-i1-');self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name);self.project=self.root/'candidate';self.project.mkdir()
        self.manifest=self.root/'snapshot.json';self.receipt=self.root/'materialization.json'
        self.baseline={'sourceRoot':str(self.root/'source'),'snapshotId':'snapshot-fixed','sourceFingerprint':'fixed-source',
                       'sourceGit':{'head':'recorded-provenance-only'},'sourceFiles':{'Assets/A.txt':'a'*64}}
        self.manifest.write_text(json.dumps(self.baseline),encoding='utf-8')
        import captured_inputs as c
        self.binding={'schema':1,'kind':'EXACT_SOURCE_MATERIALIZATION','candidateRoot':str(self.project),'sourceRoot':self.baseline['sourceRoot'],
                      'snapshotManifest':str(self.manifest),'snapshotManifestSha256':c.sha(self.manifest),'snapshotId':'snapshot-fixed',
                      'sourceFingerprint':'fixed-source','sourceFilesExactAtMaterialization':True}
        self.write_receipt()
    def write_receipt(self):
        import json
        self.receipt.write_text(json.dumps(self.binding),encoding='utf-8')
    def test_materialized_candidate_requires_both_exact_roots_and_manifest(self):
        import captured_inputs as c
        from unittest.mock import patch
        with patch('captured_inputs.require_git_boundary',return_value='NO_GIT_SNAPSHOT'):
            self.assertEqual(c.read_baseline(self.project,self.manifest,self.receipt),self.baseline)
            with self.assertRaisesRegex(RuntimeError,'materialization binding required'):c.read_baseline(self.project,self.manifest)
    def test_changed_binding_is_rejected_before_inventory_or_unity(self):
        import captured_inputs as c
        from unittest.mock import patch
        original=dict(self.binding)
        changes={'candidateRoot':str(self.root/'wrong'),'sourceRoot':str(self.root/'other-source'),'snapshotManifestSha256':'0'*64,
                 'snapshotId':'other','sourceFingerprint':'other','sourceFilesExactAtMaterialization':False}
        with patch('captured_inputs.require_git_boundary',return_value='NO_GIT_SNAPSHOT'):
            for key,value in changes.items():
                self.binding={**original,key:value};self.write_receipt()
                with self.assertRaisesRegex(RuntimeError,'BindingMismatch'):c.read_baseline(self.project,self.manifest,self.receipt)
    def test_snapshot_drift_and_traversal_are_rejected(self):
        import captured_inputs as c,json
        from unittest.mock import patch
        self.baseline['sourceFiles']={'Assets/../../escape':'a'*64};self.manifest.write_text(json.dumps(self.baseline),encoding='utf-8')
        with self.assertRaisesRegex(RuntimeError,'BindingMismatch'):c.read_baseline(self.project,self.manifest,self.receipt)
        self.binding['snapshotManifestSha256']=c.sha(self.manifest);self.write_receipt()
        with patch('captured_inputs.require_git_boundary',return_value='NO_GIT_SNAPSHOT'):
            with self.assertRaisesRegex(RuntimeError,'InvalidCapturedSourcePath'):c.read_baseline(self.project,self.manifest,self.receipt)
    def test_parent_git_and_unreadable_git_are_not_candidate_identity(self):
        import captured_inputs as c
        from unittest.mock import patch
        from types import SimpleNamespace
        with patch('captured_inputs.subprocess.run',return_value=SimpleNamespace(returncode=0,stdout=str(self.root).encode(),stderr=b'')):
            with self.assertRaisesRegex(RuntimeError,'InheritedParentGit'):c.require_git_boundary(self.project)
        with patch('captured_inputs.subprocess.run',return_value=SimpleNamespace(returncode=128,stdout=b'',stderr=b'not a git repository')):
            self.assertEqual(c.require_git_boundary(self.project),'NO_GIT_SNAPSHOT')
        with patch('captured_inputs.subprocess.run',return_value=SimpleNamespace(returncode=128,stdout=b'',stderr=b'Permission denied')):
            with self.assertRaisesRegex(RuntimeError,'IdentityUnknown'):c.require_git_boundary(self.project)
    def test_fingerprint_uses_current_candidate_bytes_not_source_head(self):
        import captured_inputs as c
        from unittest.mock import patch
        for p in ['Assets','Packages','ProjectSettings']:(self.project/p).mkdir()
        (self.project/'Packages/manifest.json').write_text('{}',encoding='utf-8')
        (self.project/'ProjectSettings/ProjectVersion.txt').write_text('2022.3.62f3c1',encoding='utf-8')
        asset=self.project/'Assets/A.txt';asset.write_text('before',encoding='utf-8')
        with patch('captured_inputs.require_git_boundary',return_value='NO_GIT_SNAPSHOT'):
            first=c.fingerprint(self.project,self.manifest,self.receipt);asset.write_text('after',encoding='utf-8')
            second=c.fingerprint(self.project,self.manifest,self.receipt)
        self.assertNotEqual(first['sourceFingerprint'],second['sourceFingerprint'])
        self.assertEqual(first['sourceHeadProvenance'],second['sourceHeadProvenance'])

if __name__=='__main__':unittest.main()
