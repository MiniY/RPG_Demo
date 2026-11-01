import unittest,hashlib
from seal_i1_host import require_review,require_closure,unreviewed_sources
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
if __name__=='__main__':unittest.main()
