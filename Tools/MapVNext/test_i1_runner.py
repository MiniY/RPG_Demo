import unittest,tempfile,json
from pathlib import Path
from unittest.mock import patch
from run_i1 import preserve_font_delta,require_player,lifecycle_qualified
class BuildSourceSafetyTests(unittest.TestCase):
    def test_unchanged_font_has_no_restoration_or_unknown_receipt(self):
        with tempfile.TemporaryDirectory() as root:
            root=Path(root);font=root/'font.asset';font.write_bytes(b'original')
            self.assertTrue(preserve_font_delta(font,b'original',root));self.assertFalse((root/'font-side-effect.json').exists())
    def test_arbitrary_delta_preserved_blocks_even_if_cache_flag_present(self):
        with tempfile.TemporaryDirectory() as root:
            root=Path(root);font=root/'font.asset';before=b'm_ClearDynamicDataOnBuild: 1\nm_FontPolicy: old\n';changed=before.replace(b'old',b'unknown')
            font.write_bytes(changed);self.assertFalse(preserve_font_delta(font,before,root));self.assertEqual(font.read_bytes(),changed)
            self.assertEqual((root/'TMP.before.asset').read_bytes(),before);self.assertEqual((root/'TMP.editor-generated.asset').read_bytes(),changed)
            record=json.loads((root/'font-side-effect.json').read_text('utf-8'));self.assertFalse(record['restored']);self.assertEqual(record['cause'],'NOT_DETERMINED')
    def test_successful_build_report_cannot_bypass_blocked_source_side_effect(self):
        with tempfile.TemporaryDirectory() as root,patch('run_i1.current_id',return_value='sealed'):
            root=Path(root);(root/'BuildValidation.json').write_text(json.dumps({'status':'BUILD_BLOCKED','identity':'sealed','fontDeltaQualified':False}))
            with self.assertRaisesRegex(RuntimeError,'QualifiedBuildLifecycleReceiptRequired'):require_player({'status':'BUILD_SUCCEEDED_PLAYER_NOT_RUN','identity':'sealed','executable':str(root/'MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe')},root)
    def test_a14_always_requires_exact_postqualification_even_if_status_rewritten(self):
        with tempfile.TemporaryDirectory() as folder,patch('run_i1.current_id',return_value='map-vnext-i1-integration-20261030-a14'):
            root=Path(folder);(root/'BuildValidation.json').write_text(json.dumps({'status':'BUILD_INPUTS_AND_LIFECYCLE_VERIFIED','fontDeltaQualified':True}))
            with patch('i1_postbuild_qualification.require_postbuild',side_effect=RuntimeError('ExactPostQualificationStillRequired')):
                with self.assertRaisesRegex(RuntimeError,'ExactPostQualificationStillRequired'):require_player({'implementationDigest':'anything'},root)
    def test_a16_always_requires_its_own_exact_postqualification(self):
        with tempfile.TemporaryDirectory() as folder,patch('run_i1.current_id',return_value='map-vnext-i1-diagnostic-20261030-a16'):
            root=Path(folder);(root/'BuildValidation.json').write_text(json.dumps({'status':'BUILD_INPUTS_AND_LIFECYCLE_VERIFIED','fontDeltaQualified':True}))
            with patch('i1_postbuild_a16.require_postbuild',side_effect=RuntimeError('ExactA16Required')):
                with self.assertRaisesRegex(RuntimeError,'ExactA16Required'):require_player({'implementationDigest':'anything'},root)
    def test_a17_always_requires_its_exact_postqualification_even_after_status_rewrite(self):
        with tempfile.TemporaryDirectory() as folder,patch('run_i1.current_id',return_value='map-vnext-i1-integration-20261030-a17'):
            root=Path(folder);(root/'BuildValidation.json').write_text(json.dumps({'status':'BUILD_INPUTS_AND_LIFECYCLE_VERIFIED','fontDeltaQualified':True}))
            with patch('i1_postbuild_a17.require_postbuild',side_effect=RuntimeError('ExactA17Required')):
                with self.assertRaisesRegex(RuntimeError,'ExactA17Required'):require_player({'implementationDigest':'anything'},root)
    def test_a20_always_requires_its_exact_postqualification_even_after_status_rewrite(self):
        with tempfile.TemporaryDirectory() as folder,patch('run_i1.current_id',return_value='map-vnext-i1-integration-20261030-a20'):
            root=Path(folder);(root/'BuildValidation.json').write_text(json.dumps({'status':'BUILD_INPUTS_AND_LIFECYCLE_VERIFIED','fontDeltaQualified':True}))
            with patch('i1_postbuild_a20.require_postbuild',side_effect=RuntimeError('ExactA20Required')):
                with self.assertRaisesRegex(RuntimeError,'ExactA20Required'):require_player({'implementationDigest':'anything'},root)
    def test_a21_always_requires_its_exact_postqualification_even_after_status_rewrite(self):
        with tempfile.TemporaryDirectory() as folder,patch('run_i1.current_id',return_value='map-vnext-i1-integration-20261030-a21'):
            root=Path(folder);(root/'BuildValidation.json').write_text(json.dumps({'status':'BUILD_INPUTS_AND_LIFECYCLE_VERIFIED','fontDeltaQualified':True}))
            with patch('i1_postbuild_a21.require_postbuild',side_effect=RuntimeError('ExactA21Required')):
                with self.assertRaisesRegex(RuntimeError,'ExactA21Required'):require_player({'implementationDigest':'anything'},root)
class NativeQualificationTests(unittest.TestCase):
    def test_real_native_status_required_beside_clean_exit_and_released_locks(self):
        clean={'naturalExit':True,'exitCode':0,'cleanup':{'status':'PASS','forced':False,'residualPids':[]},'locks':{name:{'released':True} for name in ['project','heavy']}}
        self.assertTrue(lifecycle_qualified(clean,{'state':'FREE'},True,{'status':'VALID_NATIVE_TIME_LINE'}))
        for native in [{},{'status':None},{'status':'UNKNOWN'},{'status':'INVALID_NATIVE_TIME_LINE'},{'status':'NOT_LAUNCHED_NO_PROCESS_TO_TRACE'}]:
            self.assertFalse(lifecycle_qualified(clean,{'state':'FREE'},True,native))
class A34ClosedRoutingTests(unittest.TestCase):
    def test_a34_always_requires_exact_qualification_even_after_status_rewrite(self):
        with tempfile.TemporaryDirectory() as folder,patch('run_i1.current_id',return_value='map-vnext-i1-integration-20261031-a34'):
            root=Path(folder);(root/'BuildValidation.json').write_text(json.dumps({'status':'BUILD_INPUTS_AND_LIFECYCLE_VERIFIED','fontDeltaQualified':True}))
            with patch('i1_postbuild_a34.require_postbuild',side_effect=RuntimeError('ExactA34Required')):
                with self.assertRaisesRegex(RuntimeError,'ExactA34Required'):require_player({'implementationDigest':'anything'},root)

class A37ClosedRoutingTests(unittest.TestCase):
    def test_a37_always_requires_exact_qualification_even_after_status_rewrite(self):
        with tempfile.TemporaryDirectory() as folder,patch('run_i1.current_id',return_value='map-vnext-i1-diagnostic-20261031-a37'):
            root=Path(folder);(root/'BuildValidation.json').write_text(json.dumps({'status':'BUILD_INPUTS_AND_LIFECYCLE_VERIFIED','fontDeltaQualified':True}))
            with patch('i1_postbuild_a37.require_postbuild',side_effect=RuntimeError('ExactA37Required')):
                with self.assertRaisesRegex(RuntimeError,'ExactA37Required'):require_player({'implementationDigest':'anything'},root)

class A38ClosedRoutingTests(unittest.TestCase):
    def test_a38_always_requires_exact_qualification_even_after_status_rewrite(self):
        with tempfile.TemporaryDirectory() as folder,patch('run_i1.current_id',return_value='map-vnext-i1-diagnostic-20261031-a38'):
            root=Path(folder);(root/'BuildValidation.json').write_text(json.dumps({'status':'BUILD_INPUTS_AND_LIFECYCLE_VERIFIED','fontDeltaQualified':True}))
            with patch('i1_postbuild_a38.require_postbuild',side_effect=RuntimeError('ExactA38Required')):
                with self.assertRaisesRegex(RuntimeError,'ExactA38Required'):require_player({'implementationDigest':'anything'},root)


class A39ClosedRoutingTests(unittest.TestCase):
    def test_a39_always_requires_exact_qualification_even_after_status_rewrite(self):
        with tempfile.TemporaryDirectory() as folder,patch('run_i1.current_id',return_value='map-vnext-i1-integration-20261031-a39'):
            root=Path(folder);(root/'BuildValidation.json').write_text(json.dumps({'status':'BUILD_INPUTS_AND_LIFECYCLE_VERIFIED','fontDeltaQualified':True}))
            with patch('i1_postbuild_a39.require_postbuild',side_effect=RuntimeError('ExactA39Required')):
                with self.assertRaisesRegex(RuntimeError,'ExactA39Required'):require_player({'implementationDigest':'anything'},root)

class A40ClosedRoutingTests(unittest.TestCase):
    def test_a40_always_requires_exact_qualification_even_after_status_rewrite(self):
        with tempfile.TemporaryDirectory() as folder,patch('run_i1.current_id',return_value='map-vnext-i1-integration-20261031-a40'):
            root=Path(folder);(root/'BuildValidation.json').write_text(json.dumps({'status':'BUILD_INPUTS_AND_LIFECYCLE_VERIFIED','fontDeltaQualified':True}))
            with patch('i1_postbuild_a40.require_postbuild',side_effect=RuntimeError('ExactA40Required')):
                with self.assertRaisesRegex(RuntimeError,'ExactA40Required'):require_player({'implementationDigest':'anything'},root)

class A41ClosedRoutingTests(unittest.TestCase):
    def test_a41_always_requires_exact_qualification_even_after_status_rewrite(self):
        with tempfile.TemporaryDirectory() as folder,patch('run_i1.current_id',return_value='map-vnext-i1-integration-20261031-a41'):
            root=Path(folder);(root/'BuildValidation.json').write_text(json.dumps({'status':'BUILD_INPUTS_AND_LIFECYCLE_VERIFIED','fontDeltaQualified':True}))
            with patch('i1_postbuild_a41.require_postbuild',side_effect=RuntimeError('ExactA41Required')):
                with self.assertRaisesRegex(RuntimeError,'ExactA41Required'):require_player({'implementationDigest':'anything'},root)

if __name__=='__main__':unittest.main()


class StartupExpectedExitContract(unittest.TestCase):
    def test_expected_rejection_requires_clean_natural_lifecycle_and_explicit_exit(self):
        import run_i1
        result={'naturalExit':True,'exitCode':2,'cleanup':{'status':'PASS','forced':False,'residualPids':[]},'locks':{'project':{'released':True},'heavy':{'released':True}}}
        after={'state':'FREE'};native={'status':'VALID_NATIVE_TIME_LINE'}
        self.assertFalse(run_i1.lifecycle_qualified(result,after,True,native))
        self.assertTrue(run_i1.lifecycle_qualified(result,after,True,native,2))
        result['cleanup']['forced']=True
        self.assertFalse(run_i1.lifecycle_qualified(result,after,True,native,2))


class StartupCachePreconditions(unittest.TestCase):
    def test_known_pair_does_not_restore_when_another_input_changed(self):
        import tempfile,hashlib,json,sys
        from pathlib import Path
        from types import SimpleNamespace
        from unittest.mock import patch
        import run_i1
        before=b"unit-before";generated=b"unit-generated"
        digest=lambda value:hashlib.sha256(value).hexdigest()
        rule=SimpleNamespace(BEFORE=digest(before),GENERATED=digest(generated))
        key=run_i1.FONT.removeprefix("Assets/")
        expected={"Assets":{key:digest(before),"other.asset":"old"},"Packages":{},"ProjectSettings":{}}
        changed={"Assets":{key:digest(generated),"other.asset":"new"},"Packages":{},"ProjectSettings":{}}
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);font=root/"font.asset";font.write_bytes(generated);out=root/"out";out.mkdir()
            with patch.dict(sys.modules,{"i1_postbuild_a41":rule}),patch.object(run_i1,"inputs",return_value=changed):
                self.assertFalse(run_i1.recover_known_font_cache(font,before,out,{}, {},expected,root/"build"))
            self.assertEqual(font.read_bytes(),generated)
            self.assertEqual(json.loads((out/"font-recovery-blocked.json").read_text())["status"],"OTHER_PRODUCT_INPUTS_CHANGED_NOT_RESTORED")
