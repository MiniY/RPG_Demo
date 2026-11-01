"""Finite synthetic admission negatives; never Player or font-display evidence."""
import unittest, tempfile, copy, json
from pathlib import Path
from unittest.mock import patch
import i1_postbuild_a21 as q

def input_fixture():
    before={'Assets':{q.FONT.removeprefix('Assets/'):q.BEFORE,'script.cs':'source'},'Packages':{},'ProjectSettings':{}}
    after=copy.deepcopy(before);after['Assets'][q.FONT.removeprefix('Assets/')]=q.GENERATED
    return before,after

def lifecycle_fixture(project,build):
    command=[str(project/'Unity.exe'),'-batchmode','-executeMethod','MapVNextI1Build.Build']
    cleanup={'status':'PASS','forced':False,'residualPids':[],'errors':[]}
    process={'pid':1,'creationTime100ns':'10','createdAtUtc':'utc','imagePath':command[0]}
    sup={'runId':q.RUN_ID,'operation':'build','state':'COMPLETED','launched':True,'naturalExit':True,'exitCode':0,'cleanup':cleanup,'locks':{k:{'released':True} for k in ('project','heavy')},'process':process,'command':command,'projectPath':str(project)}
    receipt={'status':'FAIL','runId':q.RUN_ID,'buildInputsAndTreeMatched':False,'cleanup':cleanup,'naturalExit':True,'exitCode':0,'trace':'VALID_NATIVE_TIME_LINE'}
    native={'root':1,'status':'VALID_NATIVE_TIME_LINE','records':[{**process,'command':command}]}
    return receipt,sup,native,{'command':command,'buildRoot':str(build)},{'state':'FREE'}

class PostBuildTests(unittest.TestCase):
    def test_only_exact_a21_identity(self):
        q.require_identity(q.BUILD_ID,q.DIGEST)
        for identity in [(q.BUILD_ID+'x',q.DIGEST),(q.BUILD_ID,'unknown'),(None,q.DIGEST)]:
            with self.assertRaisesRegex(RuntimeError,'OnlyExactA21'):q.require_identity(*identity)
    def test_known_cache_delta_and_restored_inputs(self):
        before,after=input_fixture();q.validate_inputs(before,before,after)
    def test_added_missing_or_changed_current_input_rejected(self):
        before,after=input_fixture()
        for kind in ['added','missing','changed']:
            current=copy.deepcopy(before)
            if kind=='added':current['Assets']['extra.cs']='unreviewed'
            elif kind=='missing':del current['Assets']['script.cs']
            else:current['Assets']['script.cs']='unknown'
            with self.assertRaisesRegex(RuntimeError,'CurrentInputs'):q.validate_inputs(current,before,after)
    def test_wrong_before_or_generated_font_rejected(self):
        for side in ['before','after']:
            before,after=input_fixture();(before if side=='before' else after)['Assets'][q.FONT.removeprefix('Assets/')]='unknown'
            with self.assertRaisesRegex(RuntimeError,'Font'):q.validate_inputs(before,before,after)
    def test_other_original_delta_or_missing_input_root_rejected(self):
        before,after=input_fixture();after['Assets']['script.cs']='changed'
        with self.assertRaisesRegex(RuntimeError,'OnlyReviewedFontDelta'):q.validate_inputs(before,before,after)
        before,after=input_fixture();del after['Packages']
        with self.assertRaisesRegex(RuntimeError,'FullAfterInputRoots'):q.validate_inputs(before,before,after)
    def test_pinned_evidence_changes_and_missing_files_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);f=root/'proof';f.write_bytes(b'original');binding={'proof':q.sha(f)};q.require_bindings(root,binding)
            f.write_bytes(b'unknown')
            with self.assertRaisesRegex(RuntimeError,'EvidenceChanged'):q.require_bindings(root,binding)
            f.unlink()
            with self.assertRaises(FileNotFoundError):q.require_bindings(root,binding)
    def test_exact_original_failed_receipt_and_clean_native_lifecycle(self):
        project=Path.cwd().resolve();build=project/'build';q.validate_lifecycle(*lifecycle_fixture(project,build),project,build)
    def test_original_failure_cannot_be_rewritten_or_different_run(self):
        project=Path.cwd().resolve();build=project/'build'
        for field,value in [('status','PASS_LIFECYCLE_ONLY'),('buildInputsAndTreeMatched',True),('runId','other')]:
            data=lifecycle_fixture(project,build);data[0][field]=value
            with self.assertRaises(RuntimeError):q.validate_lifecycle(*data,project,build)
    def test_forced_residual_or_unreleased_lock_rejected(self):
        project=Path.cwd().resolve();build=project/'build'
        for variant in ['forced','residual','lock']:
            data=lifecycle_fixture(project,build)
            if variant=='forced':data[1]['cleanup']['forced']=True
            elif variant=='residual':data[1]['cleanup']['residualPids']=[2]
            else:data[1]['locks']['heavy']['released']=False
            with self.assertRaisesRegex(RuntimeError,'LifecycleNotQualified'):q.validate_lifecycle(*data,project,build)
    def test_unknown_duplicate_or_foreign_native_root_rejected(self):
        project=Path.cwd().resolve();build=project/'build'
        for variant in ['unknown','duplicate','pid','creation','command']:
            data=lifecycle_fixture(project,build);native=data[2]
            if variant=='unknown':native['status']='UNKNOWN'
            elif variant=='duplicate':native['records']*=2
            elif variant=='pid':native['root']=2
            elif variant=='creation':native['records'][0]['creationTime100ns']='foreign'
            else:native['records'][0]['command']=['other.exe']
            with self.assertRaises(RuntimeError):q.validate_lifecycle(*data,project,build)
    def test_command_or_project_mismatch_rejected(self):
        project=Path.cwd().resolve();build=project/'build';data=lifecycle_fixture(project,build);data[3]['buildRoot']=str(project/'other')
        with self.assertRaisesRegex(RuntimeError,'CommandMismatch'):q.validate_lifecycle(*data,project,build)
    def test_saved_append_qualification_cannot_replace_current_recheck(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);(root/'PostBuildQualification-a21.json').write_text('{"status":"anything"}')
            with patch.object(q,'core',side_effect=RuntimeError('CurrentInputsChanged')):
                with self.assertRaisesRegex(RuntimeError,'CurrentInputsChanged'):q.require_postbuild(root,root)
    def test_qualification_creation_never_overwrites_original(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);p=root/'PostBuildQualification-a21.json';p.write_text('retain')
            with patch.object(q,'core',return_value={'status':q.STATUS}):
                with self.assertRaises(FileExistsError):q.qualify(root,root)
            self.assertEqual(p.read_text(),'retain')
    def test_tool_review_and_exact_group_required(self):
        with tempfile.TemporaryDirectory() as folder:
            project=Path(folder);reviewdir=project/'review';reviewdir.mkdir();tool=project/'tool.py';tool.write_text('read only')
            manifest={'files':[{'path':'tool.py','sha256':q.sha(tool)}]};(reviewdir/'manifest.json').write_text(json.dumps(manifest))
            review={'reviewPerformed':True,'reviewer':'/root/i1_contract_review','status':'APPROVED_EXACT_A21_POSTBUILD_ADMISSION_TOOLS','openFindings':0,'reviewedManifestSha256':q.sha(reviewdir/'manifest.json'),'axes':{'Standards':'NO_OPEN_FINDINGS','Spec':'NO_OPEN_FINDINGS'}}
            (reviewdir/'review.json').write_text(json.dumps(review))
            with patch.object(q,'REVIEW_REL','review'),patch.object(q,'TOOL_FILES',{'tool.py'}):
                q.reviewed_tools(project);tool.write_text('unknown')
                with self.assertRaisesRegex(RuntimeError,'EvidenceChanged'):q.reviewed_tools(project)
                tool.write_text('read only');review['reviewedManifestSha256']='wrong';(reviewdir/'review.json').write_text(json.dumps(review))
                with self.assertRaisesRegex(RuntimeError,'IndependentPostBuildToolReview'):q.reviewed_tools(project)
class PortableQualificationTests(unittest.TestCase):
    def fixture(self):
        return {'buildId':q.BUILD_ID,'implementationDigest':q.DIGEST,'originalRunId':q.RUN_ID,'status':q.STATUS,'reviewedAdmissionToolGroup':{'files':[{'path':name,'sha256':'synthetic'} for name in sorted(q.TOOL_FILES)],'manifestSha256':'manifest','reviewSha256':'review'}}
    def test_portable_index_always_contains_complete_original_build_chain(self):
        index=q.portable_evidence_bindings(self.fixture())
        for name,digest in q.BINDINGS.items():self.assertEqual(index['Evidence/BuildRun/'+name],digest)
        for name,digest in q.QUAL_BINDINGS.items():self.assertEqual(index['Evidence/PostBuild/FontCacheQualification-a21/'+name],digest)
    def test_portable_other_build_status_or_missing_tool_dependency_rejected(self):
        for key,value in [('buildId','other'),('status','PASS'),('originalRunId','foreign')]:
            data=self.fixture();data[key]=value
            with self.assertRaises(RuntimeError):q.portable_evidence_bindings(data)
        data=self.fixture();data['reviewedAdmissionToolGroup']['files'].pop()
        with self.assertRaisesRegex(RuntimeError,'CompleteToolGroup'):q.portable_evidence_bindings(data)
    def test_portable_missing_original_chain_index_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);p=root/'Evidence/Build/PostBuildQualification-a21.json';p.parent.mkdir(parents=True);p.write_text(json.dumps(self.fixture()))
            model={'build':q.BUILD_ID,'postBuildQualification':{'status':q.STATUS,'path':'Evidence/Build/PostBuildQualification-a21.json'},'postBuildEvidenceBindings':{},'files':{}}
            with self.assertRaisesRegex(RuntimeError,'BindingIndexIncomplete'):q.check_portable_evidence(root,model)
    def test_blocked_portable_build_cannot_omit_qualification_marker(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);p=root/'Evidence/Build/BuildValidation.json';p.parent.mkdir(parents=True);p.write_text('{"status":"BUILD_BLOCKED"}')
            with self.assertRaisesRegex(RuntimeError,'KnownA21RequiresExplicitQualifiedMarker'):q.check_portable_evidence(root,{'build':q.BUILD_ID})
    def test_normal_pass_build_does_not_adopt_stray_a21_filename(self):
        import deliver_i1 as delivery
        with tempfile.TemporaryDirectory() as folder:
            source=Path(folder);build=source/'Build-other';build.mkdir();run=source/'VersionedRuns/build';run.mkdir(parents=True)
            receipt=run/'receipt.json';receipt.write_text('{}');exe=build/'MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe'
            report={'status':'BUILD_SUCCEEDED_PLAYER_NOT_RUN','identity':'other','implementationDigest':'other-digest','errors':0,'executable':str(exe)}
            validation={'receiptPath':str(receipt),'receiptSha256':q.sha(receipt),'identity':'other','status':'BUILD_INPUTS_AND_LIFECYCLE_VERIFIED','fontDeltaQualified':True}
            for p,data in [(build/'BuildReport.json',report),(build/'BuildValidation.json',validation),(build/'build-tree.json',{}),(build/'build-inputs.json',{}),(run/'build-inputs-after.json',{})]:p.write_text(json.dumps(data))
            (build/'PostBuildQualification-a21.json').write_text('unrelated stray')
            request={'buildRoot':str(build),'command':['MapVNextI1Build.Build']}
            with patch.object(delivery,'require_run',return_value=({},request)),patch.object(delivery,'tree',return_value={}):
                self.assertEqual(delivery.require_build(source,build,'other','other-digest'),(exe,None))

    def test_a21_report_always_checks_pinned_original_even_if_status_rewritten(self):
        import deliver_i1 as delivery
        with tempfile.TemporaryDirectory() as folder:
            source=Path(folder);build=source/'build';build.mkdir();run=source/'VersionedRuns/run';run.mkdir(parents=True);receipt=run/'receipt.json';receipt.write_text('{}')
            (build/'BuildReport.json').write_text('{}');(build/'BuildValidation.json').write_text(json.dumps({'receiptPath':str(receipt),'receiptSha256':q.sha(receipt),'status':'BUILD_INPUTS_AND_LIFECYCLE_VERIFIED','fontDeltaQualified':True}))
            with patch.object(q,'verified_qualification',side_effect=RuntimeError('ExactPostQualificationStillRequired')):
                with self.assertRaisesRegex(RuntimeError,'ExactPostQualificationStillRequired'):delivery.require_build(source,build,q.BUILD_ID,q.DIGEST)
    def test_known_a21_marker_required_even_with_missing_unknown_or_rewritten_validation(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);p=root/'Evidence/Build/BuildValidation.json';p.parent.mkdir(parents=True)
            for state in [None,'UNKNOWN','BUILD_INPUTS_AND_LIFECYCLE_VERIFIED']:
                if state is not None:p.write_text(json.dumps({'status':state}))
                with self.assertRaisesRegex(RuntimeError,'KnownA21RequiresExplicitQualifiedMarker'):q.check_portable_evidence(root,{'build':q.BUILD_ID})
    def test_rejected_review_cannot_be_blessed_by_matching_its_file_sha(self):
        manifest={'files':[{'path':name,'sha256':'fixture'} for name in sorted(q.TOOL_FILES)]}
        review={'reviewPerformed':True,'reviewer':'/root/i1_contract_review','status':'APPROVED_EXACT_A21_POSTBUILD_ADMISSION_TOOLS','openFindings':0,'reviewedManifestSha256':'manifest','axes':{'Standards':'NO_OPEN_FINDINGS','Spec':'NO_OPEN_FINDINGS'}}
        q.validate_tool_review(manifest,review,'manifest')
        for field,value in [('status','CHANGES_REQUIRED'),('openFindings',3),('openFindings',False),('reviewer','other'),('reviewedManifestSha256','wrong'),('axes',{'Standards':'NO_OPEN_FINDINGS','Spec':'CHANGES_REQUIRED'})]:
            invalid={**review,field:value}
            with self.assertRaisesRegex(RuntimeError,'IndependentPostBuildToolReviewRequired'):q.validate_tool_review(manifest,invalid,'manifest')
    def test_other_blocked_build_keeps_original_rejection(self):
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);p=root/'Evidence/Build/BuildValidation.json';p.parent.mkdir(parents=True);p.write_text('{"status":"BUILD_BLOCKED"}')
            with self.assertRaisesRegex(RuntimeError,'BlockedOtherBuildCannotUseA21Exception'):q.check_portable_evidence(root,{'build':'other'})
            p.write_text('{"status":"BUILD_INPUTS_AND_LIFECYCLE_VERIFIED"}')
            q.check_portable_evidence(root,{'build':'other'})
if __name__=='__main__':unittest.main()
