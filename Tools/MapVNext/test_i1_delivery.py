"""Finite offline I1 delivery contracts. Synthetic cases are never game evidence."""
import unittest,copy,tempfile,json,csv
from pathlib import Path
from deliver_i1 import validate,independent_motion,SEEDS,REQUIRED,require_disjoint,require_input_status,bind_replay,require_run,decode_png,require_owner,require_phases,portable

def fixture():
    units=[]
    for seed in SEEDS:
        row={name:'中文说明' for name in REQUIRED};row.update(id=str(seed),human='PENDING',kind='playable',countsForTraversal=True,countsForReady=True,status='PASS',subject='Fixture checker',scope='Synthetic contract only',media=[{'path':'Media/frame.png','build':'b','runId':'r'}],input={'requestedSeed':seed,'status':'READY_SAVED_REOPENED'});units.append(row)
    return dict(build='b',implementationDigest='digest',humanAcceptance='PENDING',mergeReady='NO',fullRequestedSeeds=SEEDS[:],units=units,files={'Media/frame.png':{'build':'b','runId':'r'}},replay=[dict(path='Media/frame.png',buildId='b',runId='r')])
class ContractTests(unittest.TestCase):
    def setUp(self):self.model=fixture()
    def test_complete_chinese_scoped_model(self):self.assertEqual(validate(self.model),[])
    def test_missing_any_chinese_description_rejected(self):
        for field in REQUIRED:
            model=copy.deepcopy(self.model);del model['units'][0][field];self.assertTrue(validate(model))
    def test_bare_english_label_rejected(self):self.model['units'][0]['title']='Seed21193';self.assertTrue(validate(self.model))
    def test_material_not_counted_as_playable(self):self.model['units'][0]['kind']='material';self.assertTrue(validate(self.model))
    def test_rejection_diagnostic_is_preserved(self):
        self.model['units'][0].update(kind='diagnostic',countsForTraversal=False,countsForReady=False,status='REJECTED',media=[],input={'requestedSeed':21193,'status':'BOUNDED_REJECTION_NOT_READY','reason':'VNextFormalLayoutRejected:fixture'});self.assertEqual(validate(self.model),[])
    def test_unqualified_pass_rejected(self):
        for field in ['subject','scope']:
            model=copy.deepcopy(self.model);del model['units'][0][field];self.assertTrue(validate(model))
    def test_human_approval_cannot_be_inferred(self):
        for target,key in [(self.model,'humanAcceptance'),(self.model['units'][0],'human')]:target[key]='PASS';self.assertTrue(validate(self.model));target[key]='PENDING'
    def test_merge_boundary_preserved(self):self.model['mergeReady']='YES';self.assertTrue(validate(self.model))
    def test_fixed_denominator_missing_or_reordered_rejected(self):
        self.model['fullRequestedSeeds']=list(reversed(SEEDS));self.assertTrue(validate(self.model))
    def test_case_duplicate_or_missing_rejected(self):
        self.model['units'][1]=copy.deepcopy(self.model['units'][0]);self.assertTrue(validate(self.model));self.model['units'].pop();self.assertTrue(validate(self.model))
    def test_other_build_or_run_media_rejected(self):
        for field in ['build','runId']:
            model=copy.deepcopy(self.model);model['units'][0]['media'][0][field]='other';self.assertTrue(validate(model))
    def test_missing_resource_rejected(self):self.model['files']={};self.assertTrue(validate(self.model))
    def test_replay_mixed_identity_rejected(self):
        for field in ['buildId','runId','path']:
            model=copy.deepcopy(self.model);model['replay'][0][field]='other';self.assertTrue(validate(model))

class SourceSafetyTests(unittest.TestCase):
    def test_report_owner_rejects_other_build_or_modified_source_inputs(self):
        owner={'source':'source','build':'b','releaseSha256':'fixed','sourceEvidenceDigest':'exact'};require_owner(owner,owner)
        for field in owner:
            with self.assertRaisesRegex(RuntimeError,'OutputOwnerOrExactInputsChanged'):require_owner(owner,{**owner,field:'other'})
    def test_three_distinct_actual_player_phases_required(self):
        require_phases({'pilot':'r1','full':'r2','restore':'r3'})
        for phases in [{'pilot':'r1','full':'r2'},{'pilot':'r1','full':'r1','restore':'r3'},{'pilot':'r1','full':'r2','other':'r3'}]:
            with self.assertRaisesRegex(RuntimeError,'ThreeDistinctPlayerPhasesRequired'):require_phases(phases)
    def test_unknown_or_implementation_failure_not_relabelled_bounded(self):
        for state in ['NOT_RUN','FAIL','IMPLEMENTATION_FAILURE_NOT_READY','BOUNDED_REJECTION_NOT_READY']:
            with self.assertRaisesRegex(RuntimeError,'UnknownOrFailedInput'):require_input_status({'status':state,'reason':'Unknown:fixture'})
    def test_only_explicit_bounded_code_is_accepted(self):
        self.assertFalse(require_input_status({'status':'BOUNDED_REJECTION_NOT_READY','reason':'VNextFormalLayoutRejected:fixture'}))
    def test_outputs_and_archives_cannot_contain_their_inputs(self):
        for paths in [('root','root/player'),('root/player','root'),('root','root'),('root','root/archive.zip')]:
            with self.assertRaisesRegex(RuntimeError,'InputOutputPathsOverlap'):require_disjoint(*paths)
        require_disjoint('source/build','source/delivery','source/package.zip','source/check')
    def test_frame_generation_must_exist_in_both_world_and_movement(self):
        with tempfile.TemporaryDirectory() as root:
            root=Path(root);(root/'World').mkdir();(root/'World/world.json').write_text(json.dumps({'generation':'g','buildId':'b'}));(root/'movement.csv').write_text('phase,generation,buildId\nStair-Up,g,b\n')
            good={'image':'Motion/f.png','phase':'Stair-Up','generation':'g','buildId':'b'};bind_replay(root,[good],'b')
            for field in ['generation','phase','buildId']:
                bad={**good,field:'other'}
                with self.assertRaisesRegex(RuntimeError,'ReplayGenerationNotBound'):bind_replay(root,[bad],'b')
    def test_png_crc_is_not_pixel_decode_proof(self):
        import struct,zlib
        def chunk(kind,data):return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
        raw=b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',1,1,8,2,0,0,0))+chunk(b'IDAT',b'invalid-zlib-stream')+chunk(b'IEND',b'')
        with tempfile.TemporaryDirectory() as root:
            path=Path(root)/'broken.png';path.write_bytes(raw)
            from PIL import Image
            with Image.open(path) as image:image.verify()
            with self.assertRaises(OSError):decode_png(path)
    def test_portable_receipt_cannot_overwrite_zip_or_extract_or_existing_receipt(self):
        with tempfile.TemporaryDirectory() as folder:
            parent=Path(folder);root=parent/'Delivery';receipt=parent/'Delivery-portable-check.json'
            for archive,extract in [(receipt,parent/'Extract'),(parent/'pkg.zip',receipt),(parent/'pkg.zip',parent)]:
                with self.assertRaisesRegex(RuntimeError,'InputOutputPathsOverlap'):portable(root,archive,extract)
                self.assertFalse(archive.exists());self.assertFalse(receipt.exists())
            receipt.write_text('retained original')
            with self.assertRaisesRegex(RuntimeError,'NewArchiveExtractAndReceiptPathsRequired'):portable(root,parent/'pkg.zip',parent/'Extract')
            self.assertEqual(receipt.read_text(),'retained original')
    def test_native_timeline_must_be_exact_supervisor_root(self):
        with tempfile.TemporaryDirectory() as folder:
            project=Path(folder);root=project/'VersionedRuns'/'run';root.mkdir(parents=True);exe=project/'Game.exe';command=[str(exe),'argument'];cleanup={'status':'PASS','forced':False,'residualPids':[],'errors':[]}
            process={'pid':111,'creationTime100ns':'100','createdAtUtc':'time','imagePath':str(exe)}
            receipt={'status':'PASS_LIFECYCLE_ONLY','phase':'player','runId':'run','naturalExit':True,'exitCode':0,'buildInputsAndTreeMatched':True,'cleanup':cleanup,'trace':'VALID_NATIVE_TIME_LINE'}
            sup={'runId':'run','operation':'player','state':'COMPLETED','launched':True,'naturalExit':True,'exitCode':0,'cleanup':cleanup,'locks':{k:{'released':True} for k in ['project','heavy']},'projectPath':str(project),'command':command,'process':process}
            native={'status':'VALID_NATIVE_TIME_LINE','root':111,'records':[{**process,'command':command}]}
            bodies={'receipt.json':receipt,'supervisor.json':sup,'command.json':{'command':command,'runtimeRoot':str(project/'RuntimeData'/'run')},'guard-after.json':{'state':'FREE'},'native-trace.json':native}
            def write(values):
                for name,value in values.items():(root/name).write_text(json.dumps(value))
            write(bodies);require_run(root,'player',project,exe)
            changes=[('receipt.json','trace','INVALID'),('native-trace.json','root',999)]
            for name,key,value in changes:
                bad=copy.deepcopy(bodies);bad[name][key]=value;write(bad)
                with self.assertRaisesRegex(RuntimeError,'RunNativeRootIdentityMismatch'):require_run(root,'player',project,exe)
            for key,value in [('pid',999),('creationTime100ns','900'),('createdAtUtc','foreign'),('imagePath',str(project/'Foreign.exe')),('command',['foreign'])]:
                bad=copy.deepcopy(bodies);bad['native-trace.json']['records'][0][key]=value;write(bad)
                with self.assertRaisesRegex(RuntimeError,'RunNativeRootIdentityMismatch'):require_run(root,'player',project,exe)
            for key in process:
                bad=copy.deepcopy(bodies);del bad['supervisor.json']['process'][key];write(bad)
                with self.assertRaisesRegex(RuntimeError,'RunNativeRootIdentityMismatch'):require_run(root,'player',project,exe)
    def test_pass_receipt_label_without_supervisor_proof_is_rejected(self):
        with tempfile.TemporaryDirectory() as root:
            root=Path(root)
            for name,body in [('receipt.json',{'status':'PASS_LIFECYCLE_ONLY','phase':'player'}),('supervisor.json',{}),('command.json',{}),('guard-after.json',{}),('native-trace.json',{})]:(root/name).write_text(json.dumps(body))
            with self.assertRaisesRegex(RuntimeError,'RunReceiptDoesNotMatchSupervisor'):require_run(root,'player',root)

class ClosedPostBuildRoutingTests(unittest.TestCase):
    def test_two_reviewed_builds_keep_separate_bound_validators(self):
        from deliver_i1 import postbuild_module
        a14=postbuild_module('map-vnext-i1-integration-20261030-a14')
        a16=postbuild_module('map-vnext-i1-diagnostic-20261030-a16')
        self.assertNotEqual(a14.RUN_ID,a16.RUN_ID)
        self.assertNotEqual(a14.DIGEST,a16.DIGEST)
        with self.assertRaises(RuntimeError):a14.require_identity(a16.BUILD_ID,a16.DIGEST)
        with self.assertRaises(RuntimeError):a16.require_identity(a14.BUILD_ID,a14.DIGEST)
        self.assertIsNone(postbuild_module('map-vnext-i1-diagnostic-20261030-a17'))
    def test_a17_cannot_mix_a16_identity_or_omit_its_own_qualification(self):
        from deliver_i1 import postbuild_module,render
        a17=postbuild_module('map-vnext-i1-integration-20261030-a17');a16=postbuild_module('map-vnext-i1-diagnostic-20261030-a16')
        with self.assertRaises(RuntimeError):a17.require_identity(a16.BUILD_ID,a16.DIGEST)
        self.assertNotEqual(a17.RUN_ID,a16.RUN_ID)
        model=fixture();model.update(build=a17.BUILD_ID,postBuildQualification={'status':a17.STATUS},evidenceLinks=[])
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);render(root,model);text=(root/'index.html').read_text('utf-8')
            self.assertIn('受审的 '+a17.BUILD_ID+' 后置追加资格',text)
            self.assertIn('A/D 负例仅用键盘',text)
            self.assertNotIn('a16 后置追加资格',text)
    def test_a20_cannot_mix_a16_identity_or_omit_its_own_qualification(self):
        from deliver_i1 import postbuild_module,render
        a20=postbuild_module('map-vnext-i1-integration-20261030-a20');a16=postbuild_module('map-vnext-i1-diagnostic-20261030-a16')
        with self.assertRaises(RuntimeError):a20.require_identity(a16.BUILD_ID,a16.DIGEST)
        self.assertNotEqual(a20.RUN_ID,a16.RUN_ID)
        model=fixture();model.update(build=a20.BUILD_ID,postBuildQualification={'status':a20.STATUS},evidenceLinks=[])
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);render(root,model);text=(root/'index.html').read_text('utf-8')
            self.assertIn('受审的 '+a20.BUILD_ID+' 后置追加资格',text)
            self.assertIn('A/D 负例仅用键盘',text)
            self.assertNotIn('a16 后置追加资格',text)
    def test_a21_cannot_mix_a16_identity_or_omit_its_own_qualification(self):
        from deliver_i1 import postbuild_module,render
        a21=postbuild_module('map-vnext-i1-integration-20261030-a21');a16=postbuild_module('map-vnext-i1-diagnostic-20261030-a16')
        with self.assertRaises(RuntimeError):a21.require_identity(a16.BUILD_ID,a16.DIGEST)
        self.assertNotEqual(a21.RUN_ID,a16.RUN_ID)
        model=fixture();model.update(build=a21.BUILD_ID,postBuildQualification={'status':a21.STATUS},evidenceLinks=[])
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);render(root,model);text=(root/'index.html').read_text('utf-8')
            self.assertIn('受审的 '+a21.BUILD_ID+' 后置追加资格',text)
            self.assertIn('A/D 负例仅用键盘',text)
            self.assertNotIn('a16 后置追加资格',text)
    def test_export_uses_exact_validator_review_directory_for_both_closed_builds(self):
        from unittest.mock import patch
        from deliver_i1 import postbuild_module,postbuild_export_folders
        for identity in ['map-vnext-i1-integration-20261030-a14','map-vnext-i1-diagnostic-20261030-a16','map-vnext-i1-integration-20261030-a17','map-vnext-i1-integration-20261030-a20','map-vnext-i1-integration-20261030-a21']:
            module=postbuild_module(identity)
            folders=postbuild_export_folders(module)
            self.assertEqual(folders,[Path(module.QUAL_REL).name,Path(module.REVIEW_REL).name])
            with patch.object(module,'REVIEW_REL','fixed-reviewed-root/new-exact-snapshot'):
                self.assertEqual(postbuild_export_folders(module)[1],'new-exact-snapshot')
    def test_render_qualification_note_uses_current_a16_build_identity(self):
        from deliver_i1 import render
        model=fixture();model.update(build='map-vnext-i1-diagnostic-20261030-a16',postBuildQualification={'status':'REVIEWED_POSTBUILD_CACHE_RECOVERY_A16'},evidenceLinks=[])
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);render(root,model);text=(root/'index.html').read_text('utf-8')
            self.assertIn('受审的 map-vnext-i1-diagnostic-20261030-a16 后置追加资格',text)
            self.assertNotIn('a14 后置追加资格',text)
    def test_a16_report_requires_exact_qualification_after_status_rewrite(self):
        from unittest.mock import patch
        import deliver_i1 as delivery
        import i1_postbuild_a16 as q
        with tempfile.TemporaryDirectory() as folder:
            source=Path(folder);build=source/'build';build.mkdir();run=source/'VersionedRuns/run';run.mkdir(parents=True);receipt=run/'receipt.json';receipt.write_text('{}')
            (build/'BuildReport.json').write_text('{}');(build/'BuildValidation.json').write_text(json.dumps({'receiptPath':str(receipt),'receiptSha256':q.sha(receipt),'status':'BUILD_INPUTS_AND_LIFECYCLE_VERIFIED','fontDeltaQualified':True}))
            with patch.object(q,'verified_qualification',side_effect=RuntimeError('ExactA16Required')):
                with self.assertRaisesRegex(RuntimeError,'ExactA16Required'):delivery.require_build(source,build,q.BUILD_ID,q.DIGEST)

class IndependentMotionTests(unittest.TestCase):
    def measure(self,points,reset=False,gen='g'):
        with tempfile.TemporaryDirectory() as root:
            root=Path(root);(root/'World').mkdir()
            (root/'World/map.json').write_text(json.dumps(dict(buildId='b',generation='g',rock=[dict(id='real-shape-fixture',vertices=[dict(x=x,y=y) for x,y in [(-.5,-.5),(.5,-.5),(.5,.5),(-.5,.5)]])])),encoding='utf-8')
            lines=['phase,keys,physicsTime,frame,bodyX,bodyY,footX,footY,moveX,moveY,height,generation,buildId']
            for n,(x,y) in enumerate(points):
                if reset and n:lines.append('# SETUP_ONLY_NOT_MOVEMENT,fixture')
                lines.append(f'Approach,W,{n*.02},{n},{x},{y},{x},{y},0,1,0,{gen},b')
            (root/'movement.csv').write_text('\n'.join(lines)+'\n',encoding='utf-8');return independent_motion(root,'b')
    def test_safe_real_segment_coordinates(self):self.assertEqual(self.measure([(-1,-1),(1,-1)])['status'],'PASS')
    def test_safe_endpoints_crossing_rock_is_rejected(self):self.assertEqual(self.measure([(-1.5,0),(1.5,0)])['status'],'FAIL')
    def test_explicit_setup_is_not_invented_as_movement(self):
        result=self.measure([(-1.5,0),(1.5,0)],True);self.assertEqual(result['status'],'PASS');self.assertEqual(result['segments'],0)
    def test_actual_radius_overlap_rejected(self):self.assertEqual(self.measure([(.7,0)])['status'],'FAIL')
    def test_missing_same_generation_geometry_rejected(self):
        with self.assertRaisesRegex(RuntimeError,'UnboundMovementSample'):self.measure([(-1,-1)],gen='foreign')
class A20PhaseEvidenceTests(unittest.TestCase):
    def make_inputs(self,root):
        player=root/'Player';player.mkdir()
        for name in ['combat-input.csv','movement.csv']:(player/name).write_text('actual fixture file')
        return player
    def test_pilot_missing_consumer_diagnostics_rejects_then_complete_set_passes(self):
        from deliver_i1 import require_battle_evidence
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);player=self.make_inputs(root)
            with self.assertRaisesRegex(RuntimeError,'pilot:consumer-lifecycle'):require_battle_evidence(root,'map-vnext-i1-integration-20261030-a20','pilot')
            (player/'consumer-lifecycle.jsonl').write_text('{}')
            self.assertEqual(require_battle_evidence(root,'map-vnext-i1-integration-20261030-a20','pilot'),['combat-input.csv','movement.csv','consumer-lifecycle.jsonl'])
    def test_full_without_pilot_only_log_accepts_actual_stage_inputs(self):
        from deliver_i1 import require_battle_evidence
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);self.make_inputs(root)
            self.assertEqual(require_battle_evidence(root,'map-vnext-i1-integration-20261030-a20','full'),['combat-input.csv','movement.csv'])
    def test_full_cannot_drop_original_combat_or_movement_proof(self):
        from deliver_i1 import require_battle_evidence
        for missing in ['combat-input.csv','movement.csv']:
            with tempfile.TemporaryDirectory() as folder:
                root=Path(folder);player=self.make_inputs(root);(player/missing).unlink()
                with self.assertRaisesRegex(RuntimeError,'full:'+missing):require_battle_evidence(root,'map-vnext-i1-integration-20261030-a20','full')
    def test_a17_and_fresh_restore_keep_original_evidence_scope(self):
        from deliver_i1 import require_battle_evidence
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);self.make_inputs(root)
            self.assertEqual(require_battle_evidence(root,'map-vnext-i1-integration-20261030-a17','pilot'),['combat-input.csv','movement.csv'])
            self.assertEqual(require_battle_evidence(root/'fresh','map-vnext-i1-integration-20261030-a20','restore'),[])

class A21PhaseEvidenceTests(unittest.TestCase):
    def make_inputs(self,root):
        player=root/'Player';player.mkdir()
        for name in ['combat-input.csv','movement.csv']:(player/name).write_text('actual fixture file')
        return player
    def test_pilot_missing_consumer_diagnostics_rejects_then_complete_set_passes(self):
        from deliver_i1 import require_battle_evidence
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);player=self.make_inputs(root)
            with self.assertRaisesRegex(RuntimeError,'pilot:consumer-lifecycle'):require_battle_evidence(root,'map-vnext-i1-integration-20261030-a21','pilot')
            (player/'consumer-lifecycle.jsonl').write_text('{}')
            with self.assertRaisesRegex(RuntimeError,'pilot:consumer-visibility-search'):require_battle_evidence(root,'map-vnext-i1-integration-20261030-a21','pilot')
            (player/'consumer-visibility-search.csv').write_text('actual fixture file')
            self.assertEqual(require_battle_evidence(root,'map-vnext-i1-integration-20261030-a21','pilot'),['combat-input.csv','movement.csv','consumer-lifecycle.jsonl','consumer-visibility-search.csv'])
    def test_full_without_pilot_only_log_accepts_actual_stage_inputs(self):
        from deliver_i1 import require_battle_evidence
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);self.make_inputs(root)
            self.assertEqual(require_battle_evidence(root,'map-vnext-i1-integration-20261030-a21','full'),['combat-input.csv','movement.csv'])
    def test_full_cannot_drop_original_combat_or_movement_proof(self):
        from deliver_i1 import require_battle_evidence
        for missing in ['combat-input.csv','movement.csv']:
            with tempfile.TemporaryDirectory() as folder:
                root=Path(folder);player=self.make_inputs(root);(player/missing).unlink()
                with self.assertRaisesRegex(RuntimeError,'full:'+missing):require_battle_evidence(root,'map-vnext-i1-integration-20261030-a21','full')
    def test_a17_and_fresh_restore_keep_original_evidence_scope(self):
        from deliver_i1 import require_battle_evidence
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);self.make_inputs(root)
            self.assertEqual(require_battle_evidence(root,'map-vnext-i1-integration-20261030-a17','pilot'),['combat-input.csv','movement.csv'])
            self.assertEqual(require_battle_evidence(root/'fresh','map-vnext-i1-integration-20261030-a21','restore'),[])

class A34EvidenceScopeTests(unittest.TestCase):
    def test_exact_a34_route_and_static_positive_evidence_cannot_omit_native_or_phase(self):
        from deliver_i1 import postbuild_module,require_battle_evidence
        q=postbuild_module('map-vnext-i1-integration-20261031-a34');old=postbuild_module('map-vnext-i1-integration-20261030-a21')
        self.assertIsNotNone(q)
        with self.assertRaises(RuntimeError):q.require_identity(old.BUILD_ID,old.DIGEST)
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);(root/'Player').mkdir()
            required=['combat-input.csv','movement.csv','consumer-lifecycle.jsonl','consumer-visibility-search.csv','consumer-pursuit-prediction.jsonl','consumer-setup-phase.jsonl','consumer-native-motion.csv']
            for name in required:(root/'Player'/name).write_text('fixture')
            self.assertEqual(require_battle_evidence(root,q.BUILD_ID,'pilot'),required)
            for missing in ['consumer-native-motion.csv','consumer-setup-phase.jsonl','consumer-pursuit-prediction.jsonl']:
                (root/'Player'/missing).unlink()
                with self.assertRaisesRegex(RuntimeError,'ActualPhaseEvidenceRequired'):require_battle_evidence(root,q.BUILD_ID,'pilot')
                (root/'Player'/missing).write_text('fixture')
            self.assertFalse(any('lure' in name for name in required))
    def test_a34_generated_homepage_keeps_21193_failure_and_7727_scope(self):
        from deliver_i1 import render
        data=fixture();data.update(build='map-vnext-i1-integration-20261031-a34',evidenceLinks=[])
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);render(root,data);text=(root/'index.html').read_text('utf8')
            self.assertIn('Seed 7727',text);self.assertIn('居民互挡失败仍保留',text)

class A38DiagnosticExportBoundaryTests(unittest.TestCase):
    def test_a38_diagnostic_cannot_enter_generic_delivery_fallback(self):
        from deliver_i1 import postbuild_module
        with self.assertRaisesRegex(RuntimeError,'DiagnosticA38NotEligibleForDelivery'):
            postbuild_module('map-vnext-i1-diagnostic-20261031-a38')
        self.assertIsNotNone(postbuild_module('map-vnext-i1-integration-20261031-a34'))

class A37DiagnosticExportBoundaryTests(unittest.TestCase):
    def test_a37_diagnostic_cannot_enter_generic_delivery_fallback(self):
        from deliver_i1 import postbuild_module
        with self.assertRaisesRegex(RuntimeError,'DiagnosticA37NotEligibleForDelivery'):
            postbuild_module('map-vnext-i1-diagnostic-20261031-a37')
        self.assertIsNotNone(postbuild_module('map-vnext-i1-integration-20261031-a34'))


class A39FinalEvidenceTests(unittest.TestCase):
    def test_a39_has_exact_own_module_and_requires_every_native_consumer_pilot_file(self):
        from deliver_i1 import postbuild_module,require_battle_evidence
        q=postbuild_module('map-vnext-i1-integration-20261031-a39');self.assertEqual(q.BUILD_ID,'map-vnext-i1-integration-20261031-a39')
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);(root/'Player').mkdir()
            required=['combat-input.csv','movement.csv','consumer-lifecycle.jsonl','consumer-visibility-search.csv','consumer-pursuit-prediction.jsonl','consumer-setup-phase.jsonl','consumer-native-motion.csv']
            for name in required:(root/'Player'/name).write_text('fixture')
            self.assertEqual(require_battle_evidence(root,q.BUILD_ID,'pilot'),required)
            for name in required:
                (root/'Player'/name).unlink()
                with self.assertRaisesRegex(RuntimeError,'ActualPhaseEvidenceRequired'):require_battle_evidence(root,q.BUILD_ID,'pilot')
                (root/'Player'/name).write_text('fixture')
            self.assertEqual(require_battle_evidence(root,q.BUILD_ID,'full'),required[:2]);self.assertEqual(require_battle_evidence(root,q.BUILD_ID,'restore'),[])
    def test_actual_capture_phase_labels_and_generations_cannot_be_repaired_in_report(self):
        from deliver_i1 import require_capture_labels
        frames=[{'image':'Startup.png','phase':'Startup','generation':''},{'image':'s.png','phase':'Spawn','generation':'g'},{'image':'o.png','phase':'Overview','generation':'g'}]
        inputs=[{'status':'READY_SAVED_REOPENED','spawnImage':'s.png','overviewImage':'o.png','generation':'g'}]
        require_capture_labels(frames,inputs,'full')
        for index in range(3):
            wrong=copy.deepcopy(frames);wrong[index]['phase']='previous-trial'
            with self.assertRaisesRegex(RuntimeError,'ActualCapturePhase'):require_capture_labels(wrong,inputs,'full')
        wrong=copy.deepcopy(frames);wrong[2]['generation']='old-generation'
        with self.assertRaisesRegex(RuntimeError,'ActualCapturePhase'):require_capture_labels(wrong,inputs,'full')
        with self.assertRaisesRegex(RuntimeError,'ActualCapturePhase'):require_capture_labels(frames+[frames[1]],inputs,'full')
        restored=frames[:1]+[{'image':'FreshProcess-Restored.png','phase':'FreshProcessRestored','generation':'new-g'}]
        require_capture_labels(restored,[],'restore');restored[1]['generation']=''
        with self.assertRaisesRegex(RuntimeError,'FreshRestoreGenerationMissing'):require_capture_labels(restored,[],'restore')
    def test_a39_homepage_names_unresolved_fault_and_human_boundary(self):
        from deliver_i1 import render
        data=fixture();data.update(build='map-vnext-i1-integration-20261031-a39',evidenceLinks=[])
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);render(root,data);text=(root/'index.html').read_text('utf8')
            for expected in ['根因仍未查明','未复现不能证明修复','合入阻塞','Seed 7727','MERGE_READY=NO','<details><summary>展开技术验证、证据与历史失败']:
                self.assertIn(expected,text)
            self.assertIn('已知问题',(root/'START-HERE.md').read_text('utf8'))


class A39ExportedRawEvidenceTests(unittest.TestCase):
    # Synthetic portable-shaped files test consistency rejection, never gameplay.
    def fixture(self,root):
        from deliver_i1 import sha,save
        build=getattr(self,'BUILD','map-vnext-i1-integration-20261031-a39');model=fixture();model.update(build=build,implementationDigest='synthetic-digest',playerRuns={k:'synthetic-'+k for k in ['pilot','full','restore']},files={},replay=[],buildTree={})
        def write(path,data):
            path.parent.mkdir(parents=True,exist_ok=True)
            path.write_text(json.dumps(data) if not isinstance(data,str) else data,encoding='utf-8')
        def frames(path,rows):
            with path.open('w',encoding='utf-8',newline='') as stream:
                writer=csv.DictWriter(stream,fieldnames=['image','phase','generation','buildId','keys','physicsTime']);writer.writeheader();writer.writerows(rows)
        for i,(phase,run) in enumerate(model['playerRuns'].items(),1):
            player=root/'Media'/run;player.mkdir(parents=True);generation='00000000-0000-4000-8000-'+str(i).zfill(12)
            def frame(image,label,gen):return dict(image=image,phase=label,generation=gen,buildId=build,keys='W+D' if label.startswith('Stair-') else 'None',physicsTime='1')
            rows=[frame('Startup.png','Startup','')];inputs=[]
            if phase=='restore':
                rows.append(frame('FreshProcess-Restored.png','FreshProcessRestored',generation));write(player/'movement.csv','generation,phase,buildId\n');evidence=root/'Evidence/Runs'/run
                copied=evidence/'restore-input-copy.json';write(copied,{'generation':{'requestedSeed':314159}});write(evidence/'restore-input.json',{'sha256':sha(copied),'originalFormalDataUsed':False})
                write(evidence/'player.log','host='+build+'\nMap generation committed: GenerationId='+generation+'; RequestedSeed=314159; ActualSeed=314159; Size=64x64;\n')
            else:
                ready={'requestedSeed':21193,'status':'READY_SAVED_REOPENED','spawnImage':'s.png','overviewImage':'o.png','generation':generation};inputs=[ready]
                rows +=[frame('s.png','Spawn',generation),frame('o.png','Overview',generation)]
                if phase=='pilot':rows.append(frame('Motion/f.png','Stair-Up-Test',generation))
                write(player/'movement.csv','generation,phase,buildId\n'+generation+',Stair-Up-Test,'+build+'\n');write(player/'combat-input.csv','synthetic')
                write(player/'World/world.json',{'buildId':build,'generation':generation})
                if phase=='pilot':
                    for name in ['consumer-lifecycle.jsonl','consumer-visibility-search.csv','consumer-pursuit-prediction.jsonl','consumer-setup-phase.jsonl','consumer-native-motion.csv']:write(player/name,'synthetic')
                    model['replay']=[{**rows[-1],'path':(Path('Media')/run/'Motion/f.png').as_posix(),'runId':run}]
                else:
                    inputs +=[{'requestedSeed':seed,'status':'BOUNDED_REJECTION_NOT_READY','reason':'VNextFormalLayoutRejected:synthetic'} for seed in SEEDS[1:]]
                    for unit,row in zip(model['units'],inputs):
                        ready_row=row['status']=='READY_SAVED_REOPENED';unit.update(input=row,media=[],kind='playable' if ready_row else 'diagnostic',status='PASS' if ready_row else 'REJECTED',countsForReady=ready_row,countsForTraversal=ready_row)
            for row in rows:write(player/row['image'],'SYNTHETIC-NOT-PNG')
            frames(player/'frames.csv',rows);write(player/'i1-player-evidence.json',{'buildId':build,'implementationDigest':'synthetic-digest','phase':phase,'status':'AUTOMATED_CHECKS_COMPLETE_HUMAN_PENDING','inputs':inputs})
            for path in [x for x in player.rglob('*') if x.is_file()]+[x for x in (root/'Evidence/Runs'/run).rglob('*') if x.is_file()]:
                model['files'][path.relative_to(root).as_posix()]={'sha256':sha(path),'build':build,'runId':run}
        return model
    def mutate_csv(self,root,model,phase,change):
        from deliver_i1 import sha
        run=model['playerRuns'][phase];path=root/'Media'/run/'frames.csv'
        with path.open(encoding='utf-8',newline='') as stream:rows=list(csv.DictReader(stream))
        change(rows)
        with path.open('w',encoding='utf-8',newline='') as stream:
            writer=csv.DictWriter(stream,fieldnames=rows[0].keys());writer.writeheader();writer.writerows(rows)
        model['files'][path.relative_to(root).as_posix()]['sha256']=sha(path)
    def test_consistent_raw_fixture_and_restore_without_world_are_supported(self):
        from deliver_i1 import check_exported_player_evidence
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);model=self.fixture(root);check_exported_player_evidence(root,model);self.assertFalse((root/'Media/synthetic-restore/World').exists())
    def test_reindexed_spawn_phase_or_generation_still_rejected(self):
        from deliver_i1 import check_exported_player_evidence
        for field,value in [('phase','Stair-Up-Test'),('generation','old-generation')]:
            with tempfile.TemporaryDirectory() as folder:
                root=Path(folder);model=self.fixture(root);self.mutate_csv(root,model,'full',lambda rows:rows[1].update({field:value}))
                with self.assertRaisesRegex(RuntimeError,'ActualCapturePhaseOrGenerationMismatch'):check_exported_player_evidence(root,model)
    def test_dynamic_metadata_and_editable_replay_cannot_replace_raw_bindings(self):
        from deliver_i1 import check_exported_player_evidence
        for where,field,value in [('csv','phase','missing-phase'),('csv','generation','old-generation'),('model','phase','missing-phase'),('model','generation','old-generation')]:
            with tempfile.TemporaryDirectory() as folder:
                root=Path(folder);model=self.fixture(root)
                if where=='csv':self.mutate_csv(root,model,'pilot',lambda rows:rows[-1].update({field:value}))
                else:model['replay'][0][field]=value
                with self.assertRaises(RuntimeError):check_exported_player_evidence(root,model)
    def test_full_input_claims_must_equal_original_body(self):
        from deliver_i1 import check_exported_player_evidence
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);model=self.fixture(root);model['units'][0]['input']['generation']='foreign'
            with self.assertRaisesRegex(RuntimeError,'FullInputsDoNotMatchOriginal'):check_exported_player_evidence(root,model)
    def test_restore_first_commit_and_copy_sha_cannot_be_reindexed_away(self):
        from deliver_i1 import check_exported_player_evidence,sha
        for variant in ['log','copy']:
            with tempfile.TemporaryDirectory() as folder:
                root=Path(folder);model=self.fixture(root);base=root/'Evidence/Runs/synthetic-restore';path=base/('player.log' if variant=='log' else 'restore-input-copy.json')
                path.write_text(path.read_text().replace('314159','271828'),encoding='utf-8');model['files'][path.relative_to(root).as_posix()]['sha256']=sha(path)
                with self.assertRaises(RuntimeError):check_exported_player_evidence(root,model)
    def test_public_package_check_reaches_shared_raw_phase_rejection(self):
        from deliver_i1 import check,save
        from unittest.mock import patch
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);model=self.fixture(root);self.mutate_csv(root,model,'full',lambda rows:rows[1].update(phase='previous-trial'));save(root/'manifest.json',model);(root/'index.html').write_text('<html lang="zh-CN"></html>',encoding='utf-8')
            for name in ['RPG_Demo_MapVNext_I1.exe','UnityPlayer.dll']:
                path=root/'MVP_Windows_x64'/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_text('SYNTHETIC-NOT-PLAYER')
            (root/'MVP_Windows_x64/RPG_Demo_MapVNext_I1_Data').mkdir()
            with patch('deliver_i1.postbuild_module') as admission,patch('deliver_i1.tree',return_value={}),patch('deliver_i1.decode_png',return_value=(1,1)):
                admission.return_value.check_portable_evidence.return_value=None;result=check(root)
            self.assertEqual(result['status'],'FAIL');self.assertTrue(any('ActualCapturePhaseOrGenerationMismatch' in error for error in result['errors']))

class A40ExportedRawEvidenceTests(A39ExportedRawEvidenceTests):
    BUILD = 'map-vnext-i1-integration-20261031-a40'

class A40FinalEvidenceTests(unittest.TestCase):
    def test_a40_exact_module_and_original_phase_files(self):
        from deliver_i1 import postbuild_module,phase_evidence_names
        q=postbuild_module('map-vnext-i1-integration-20261031-a40')
        self.assertEqual(q.BUILD_ID,'map-vnext-i1-integration-20261031-a40')
        self.assertEqual(phase_evidence_names(q.BUILD_ID,'pilot'),['combat-input.csv','movement.csv','consumer-lifecycle.jsonl','consumer-visibility-search.csv','consumer-pursuit-prediction.jsonl','consumer-setup-phase.jsonl','consumer-native-motion.csv'])
        self.assertEqual(phase_evidence_names(q.BUILD_ID,'full'),['combat-input.csv','movement.csv'])
        self.assertEqual(phase_evidence_names(q.BUILD_ID,'restore'),[])
    def test_a40_known_fault_human_scope_and_readable_markdown_paragraphs(self):
        from deliver_i1 import render
        data=fixture();data.update(build='map-vnext-i1-integration-20261031-a40',evidenceLinks=[])
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);render(root,data);text=(root/'index.html').read_text('utf8');guide=(root/'START-HERE.md').read_text('utf8')
            for expected in ['根因仍未查明','未复现不能证明修复','合入阻塞','Seed 7727','MERGE_READY=NO','已知问题']:
                self.assertIn(expected,text);self.assertIn(expected,guide)
            self.assertIn('\n\n',guide)
            self.assertNotIn('上下。先打开',guide)

class A41ExportedRawEvidenceTests(A39ExportedRawEvidenceTests):
    BUILD = 'map-vnext-i1-integration-20261031-a41'

class A41FinalEvidenceTests(unittest.TestCase):
    def test_a41_exact_module_and_original_phase_files(self):
        from deliver_i1 import postbuild_module,phase_evidence_names
        q=postbuild_module('map-vnext-i1-integration-20261031-a41')
        self.assertEqual(q.BUILD_ID,'map-vnext-i1-integration-20261031-a41')
        self.assertEqual(phase_evidence_names(q.BUILD_ID,'pilot'),['combat-input.csv','movement.csv','consumer-lifecycle.jsonl','consumer-visibility-search.csv','consumer-pursuit-prediction.jsonl','consumer-setup-phase.jsonl','consumer-native-motion.csv'])
        self.assertEqual(phase_evidence_names(q.BUILD_ID,'full'),['combat-input.csv','movement.csv'])
        self.assertEqual(phase_evidence_names(q.BUILD_ID,'restore'),[])
    def test_a41_known_fault_human_scope_and_readable_markdown_paragraphs(self):
        from deliver_i1 import render
        data=fixture();data.update(build='map-vnext-i1-integration-20261031-a41',evidenceLinks=[])
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);render(root,data);text=(root/'index.html').read_text('utf8');guide=(root/'START-HERE.md').read_text('utf8')
            for expected in ['根因仍未查明','未复现不能证明修复','合入阻塞','Seed 7727','MERGE_READY=NO','已知问题','历史 a40','怪物碰撞','玩家死亡','明确失败']:
                self.assertIn(expected,text);self.assertIn(expected,guide)
            self.assertIn('\n\n',guide)
            self.assertNotIn('上下。先打开',guide)

from unittest.mock import patch

class A41ContinuousVideoEvidenceTests(unittest.TestCase):
    # Synthetic graph plus mocked valid decode: record consistency only, never gameplay.
    def video_fixture(self,root):
        import deliver_i1 as d
        from pathlib import Path
        build='map-vnext-i1-integration-20261031-a41';run='own';base=d.A41_VIDEO_BASE;path=base+'ContinuousVideo/'+run+'/owned-player-continuous.mp4'
        model=fixture();model.update(build=build,implementationDigest='synthetic',evidenceLinks=[],playerRuns={'pilot':run},files={})
        def write(path,value):
            p=root/path;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(value if isinstance(value,bytes) else json.dumps(value).encode('utf-8'));model['files'][path]={'sha256':d.sha(p),'build':build,'runId':run};return p
        video=write(path,b'SYNTHETIC_NOT_A_REAL_VIDEO');exe=write('MVP_Windows_x64/RPG_Demo_MapVNext_I1.exe',b'fixture-exe')
        helper=Path(__file__).resolve().parents[2]/'Tools/MapVNext/record_i1_player.py';write(d.A41_VIDEO_HELPER_EXPORT,helper.read_bytes())
        process={'pid':123,'creationTime100ns':'456','createdAtUtc':'2026-10-31T00:00:00+00:00','imagePath':'D:/owned/RPG_Demo_MapVNext_I1.exe'}
        command=[process['imagePath'],'--i1-accept','D:/owned/VersionedRuns/own/Player'];prefix='Evidence/Runs/'+run+'/'
        write(prefix+'supervisor.json',{'runId':'real-fixture-uuid','operation':'player','naturalExit':True,'exitCode':0,'cleanup':{'status':'PASS','forced':False,'residualPids':[]},'locks':{k:{'released':True} for k in ['heavy','project']},'process':process,'command':command})
        write(prefix+'native-trace.json',{'status':'VALID_NATIVE_TIME_LINE','root':123,'records':[{**process,'command':command,'exitObservedAtUtc':'2026-10-31T00:01:00+00:00'}]})
        write(prefix+'receipt.json',{'status':'PASS_LIFECYCLE_ONLY','runId':'real-fixture-uuid'});write(prefix+'command.json',{'command':command})
        write('Media/'+run+'/i1-player-evidence.json',{'buildId':build,'implementationDigest':'synthetic','status':'AUTOMATED_CHECKS_COMPLETE_HUMAN_PENDING'})
        start={'status':'OWNED_WINDOW_CAPTURE_STARTED_NOT_GAMEPLAY_PASS','buildId':build,'run':run,'utc':'2026-10-31T00:00:10+00:00','captureScriptSha256':d.A41_CAPTURE_HELPER_SHA,'ffmpegSha256':d.A41_CAPTURE_FFMPEG_SHA,'process':{'ProcessId':123,'ExecutablePath':process['imagePath'],'CommandLine':'D:/owned/RPG_Demo_MapVNext_I1.exe --i1-accept D:/owned/VersionedRuns/own/Player'},'creationTime100ns':'456','window':{'hwnd':789,'pid':123},'expectedExe':process['imagePath'],'exeSha256':d.sha(exe),'command':['fixture-ffmpeg','-f','gdigrab','-framerate','12','-i','hwnd=789','-an','D:/owned/Authoring/VersionedExtension/FinalVerification-a41/ContinuousVideo/own/owned-player-continuous.mp4']}
        directory=path.rsplit('/',1)[0]+'/';sp=write(directory+'start.json',start)
        end={'status':'CAPTURE_CLOSED_CONTENT_AND_RUN_VALIDATION_PENDING','buildId':build,'run':run,'utc':'2026-10-31T00:00:40+00:00','ffmpegExitCode':0,'noPlayerControlOrTermination':True,'captureOnlyExactWindow':True,'stopReason':'EXACT_PLAYER_EXITED','startSha256':d.sha(sp),'videoSha256':d.sha(video),'videoBytes':video.stat().st_size};write(directory+'capture-result.json',end)
        decoded={'width':1920,'height':1080,'fps':12.0,'decodedFrames':120}
        quality={'status':'ACTUAL_OWNED_WINDOW_VIDEO_DECODED_AND_VISUALLY_VERIFIED','buildId':build,'implementationDigest':'synthetic','visualReview':'AGENT_FRAME_REVIEW_NOT_USER_ACCEPTANCE','validVideos':[{'relativePath':path.removeprefix('Evidence/Reviewed/'),'run':run,'runId':'real-fixture-uuid','sha256':d.sha(video),'collectorExitCode':0,'decoded':decoded}]};write(base+'video-quality-verification.json',quality)
        def save_start():
            sp=write(directory+'start.json',start);end['startSha256']=d.sha(sp);write(directory+'capture-result.json',end)
        return model,path,write,start,end,quality,decoded,save_start

    def test_consistent_graph_requires_actual_decode_before_render(self):
        import deliver_i1 as d
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);m,path,w,s,e,q,decoded,save_start=self.video_fixture(root)
            with patch.object(d,'decode_continuous_video',return_value=decoded) as decoder:
                self.assertEqual(d.validate_continuous_video_evidence(root,m),[path]);d.render(root,m);self.assertEqual(decoder.call_count,2)
            self.assertIn('<video controls preload="metadata" src="'+path+'"',(root/'index.html').read_text('utf-8'))
    def test_self_labelled_video_without_capture_records_cannot_render(self):
        import deliver_i1 as d
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);m=fixture();path=d.A41_VIDEO_BASE+'ContinuousVideo/own/owned-player-continuous.mp4';p=root/path;p.parent.mkdir(parents=True);p.write_bytes(b'OLD_OR_CORRUPT')
            m.update(build='map-vnext-i1-integration-20261031-a41',implementationDigest='synthetic',evidenceLinks=[],playerRuns={'pilot':'own'},files={path:{'build':'map-vnext-i1-integration-20261031-a41','runId':'own','sha256':d.sha(p)}})
            with self.assertRaisesRegex(RuntimeError,'ContinuousVideoMissingOrChangedOriginal'):d.render(root,m)
    def test_no_video_remains_optional_and_never_decodes(self):
        import deliver_i1 as d
        with tempfile.TemporaryDirectory() as folder:
            m=fixture();m.update(build='map-vnext-i1-integration-20261031-a41',evidenceLinks=[],files={})
            with patch.object(d,'decode_continuous_video') as decoder:d.render(Path(folder),m);decoder.assert_not_called()
            self.assertNotIn('id="continuous-video"',(Path(folder)/'index.html').read_text('utf-8'))
    def test_any_missing_raw_dependency_rejects(self):
        import deliver_i1 as d
        for relative in ['start.json','capture-result.json','quality','native','helper']:
            with self.subTest(relative=relative),tempfile.TemporaryDirectory() as folder:
                root=Path(folder);m,path,w,s,e,q,decoded,save_start=self.video_fixture(root);directory=path.rsplit('/',1)[0]+'/'
                target={'quality':d.A41_VIDEO_BASE+'video-quality-verification.json','native':'Evidence/Runs/own/native-trace.json','helper':d.A41_VIDEO_HELPER_EXPORT}.get(relative,directory+relative)
                (root/target).unlink()
                with self.assertRaisesRegex(RuntimeError,'ContinuousVideoMissingOrChangedOriginal'):d.validate_continuous_video_evidence(root,m)
    def test_recreated_or_foreign_native_owner_rejects_after_coherent_rehash(self):
        import deliver_i1 as d
        for change in ['pid','creation','window','exe']:
            with self.subTest(change=change),tempfile.TemporaryDirectory() as folder:
                root=Path(folder);m,path,w,s,e,q,decoded,save_start=self.video_fixture(root)
                if change=='pid':s['process']['ProcessId']=999
                elif change=='creation':s['creationTime100ns']='replacement'
                elif change=='window':s['window']['pid']=999
                else:s['process']['ExecutablePath']='D:/foreign.exe'
                save_start()
                with self.assertRaisesRegex(RuntimeError,'ContinuousVideoCaptured(Owner|Executable)Mismatch'):d.validate_continuous_video_evidence(root,m)
    def test_desktop_or_unknown_capture_dependency_rejects(self):
        import deliver_i1 as d
        for change in ['desktop','script','decoder','fps']:
            with self.subTest(change=change),tempfile.TemporaryDirectory() as folder:
                root=Path(folder);m,path,w,s,e,q,decoded,save_start=self.video_fixture(root)
                if change=='desktop':s['command'][s['command'].index('-i')+1]='desktop'
                elif change=='script':s['captureScriptSha256']='unknown'
                elif change=='decoder':s['ffmpegSha256']='unknown'
                else:s['command'][s['command'].index('-framerate')+1]='30'
                save_start()
                with self.assertRaisesRegex(RuntimeError,'ContinuousVideo(CaptureCommandRejected|UnknownCaptureDependency)'):d.validate_continuous_video_evidence(root,m)
    def test_failed_capture_or_video_byte_mismatch_rejects(self):
        import deliver_i1 as d
        for change in ['failed','sha','size']:
            with self.subTest(change=change),tempfile.TemporaryDirectory() as folder:
                root=Path(folder);m,path,w,s,e,q,decoded,save_start=self.video_fixture(root)
                if change=='failed':e['ffmpegExitCode']=1
                elif change=='sha':e['videoSha256']='old-video'
                else:e['videoBytes']+=1
                w(path.rsplit('/',1)[0]+'/capture-result.json',e)
                with self.assertRaisesRegex(RuntimeError,'ContinuousVideo(CaptureDidNotCloseLawfully|CaptureBytesMismatch)'):d.validate_continuous_video_evidence(root,m)
    def test_wrong_quality_identity_or_actual_decode_result_rejects(self):
        import deliver_i1 as d
        for change in ['run','digest','visual','decode','exit']:
            with self.subTest(change=change),tempfile.TemporaryDirectory() as folder:
                root=Path(folder);m,path,w,s,e,q,decoded,save_start=self.video_fixture(root)
                if change=='run':q['validVideos'][0]['runId']='old-run'
                elif change=='digest':q['implementationDigest']='mixed'
                elif change=='visual':q['visualReview']='NOT_VERIFIED'
                elif change=='exit':q['validVideos'][0]['collectorExitCode']=1
                else:q['validVideos'][0]['decoded']['decodedFrames']=121;decoded={**decoded,'decodedFrames':120}
                w(d.A41_VIDEO_BASE+'video-quality-verification.json',q)
                with patch.object(d,'decode_continuous_video',return_value=decoded):
                    with self.assertRaisesRegex(RuntimeError,'ContinuousVideo(Quality|ActualDecode)'):d.validate_continuous_video_evidence(root,m)
    def test_capture_outside_real_native_timeline_rejects(self):
        import deliver_i1 as d
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);m,path,w,s,e,q,decoded,save_start=self.video_fixture(root);s['utc']='2026-10-31T00:01:20+00:00';save_start()
            with self.assertRaisesRegex(RuntimeError,'ContinuousVideoCaptureOutsideActualRun'):d.validate_continuous_video_evidence(root,m)
    def test_actual_decoder_reads_synthetic_fixed_rate_clip(self):
        # Generated test pattern only; never captured RPG media or gameplay evidence.
        import subprocess,imageio_ffmpeg
        from deliver_i1 import decode_continuous_video
        with tempfile.TemporaryDirectory() as folder:
            p=Path(folder)/'SYNTHETIC_DECODER_TEST_NOT_GAMEPLAY.mp4'
            command=[imageio_ffmpeg.get_ffmpeg_exe(),'-hide_banner','-loglevel','error','-n','-f','lavfi','-i','testsrc=size=160x90:rate=12','-t','1','-an','-c:v','libx264','-pix_fmt','yuv420p',str(p)]
            result=subprocess.run(command,capture_output=True,timeout=30,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
            self.assertEqual(result.returncode,0,result.stderr)
            self.assertEqual(decode_continuous_video(p),{'width':160,'height':90,'fps':12.0,'decodedFrames':12})
    def test_corrupt_movie_is_rejected_by_actual_full_decoder(self):
        from deliver_i1 import decode_continuous_video
        with tempfile.TemporaryDirectory() as folder:
            p=Path(folder)/'corrupt.mp4';p.write_bytes(b'NOT_A_VIDEO')
            with self.assertRaisesRegex(RuntimeError,'ContinuousVideoActualDecodeFailed'):decode_continuous_video(p)
    def test_unknown_decoder_binary_is_rejected_before_execution(self):
        import deliver_i1 as d
        with tempfile.TemporaryDirectory() as folder:
            p=Path(folder)/'unknown.exe';p.write_bytes(b'not-an-approved-decoder')
            with patch('imageio_ffmpeg.get_ffmpeg_exe',return_value=str(p)):
                with self.assertRaisesRegex(RuntimeError,'ContinuousVideoUnknownDecoder'):d.decode_continuous_video(Path(folder)/'clip.mp4')
    def test_public_check_executes_same_missing_capture_gate(self):
        import deliver_i1 as d
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);m,path,w,s,e,q,decoded,save_start=self.video_fixture(root);(root/(path.rsplit('/',1)[0]+'/start.json')).unlink()
            w('MVP_Windows_x64/UnityPlayer.dll',b'fixture');(root/'MVP_Windows_x64/RPG_Demo_MapVNext_I1_Data').mkdir();m['buildTree']=d.tree(root/'MVP_Windows_x64');(root/'index.html').write_text('<html></html>','utf-8');(root/'manifest.json').write_text(json.dumps(m),'utf-8')
            with patch.object(d,'postbuild_module') as admission,patch.object(d,'check_exported_player_evidence'):
                admission.return_value.check_portable_evidence.return_value=None;result=d.check(root)
            self.assertEqual(result['status'],'FAIL');self.assertTrue(any('Continuous original video evidence: ContinuousVideoMissingOrChangedOriginal' in e for e in result['errors']))

if __name__=='__main__':unittest.main()

class A41WgcContinuousVideoEvidenceTests(unittest.TestCase):
    # Synthetic record graph; the real D3D capture test is separately labelled.
    def fixture_wgc(self,root):
        import deliver_i1 as d
        m,path,w,start,end,q,decoded,save_start=A41ContinuousVideoEvidenceTests().video_fixture(root)
        for rel in ['record_i1_wgc.py','native/OwnedWindowCapture.exe','native/OwnedWindowCapture.cpp']:
            w(d.A41_WGC_EXPORT+rel,(Path(__file__).parent/rel).read_bytes())
        start.update(captureMethod='WGC_HWND_V1',captureScriptSha256=d.A41_WGC_HELPER_SHA,backendSha256=d.A41_WGC_BACKEND_SHA,backendSourceSha256=d.A41_WGC_SOURCE_SHA,backendCommand=['D:/owned/Tools/MapVNext/native/OwnedWindowCapture.exe','789','123','456'],sourceWidth=1920,sourceHeight=1080)
        start['command']=['fixture-ffmpeg','-f','rawvideo','-pixel_format','bgra','-video_size','1920x1080','-framerate','12','-i','pipe:0','-vf','pad=ceil(iw/2)*2:ceil(ih/2)*2','-an','D:/owned/Authoring/VersionedExtension/FinalVerification-a41/ContinuousVideo/own/owned-player-continuous.mp4']
        end.update(captureMethod='WGC_HWND_V1',backendExitCode=0,forcedOwnedChildren=[],secondaryErrors=[],primaryFailure=None,sourceFrames=120,sourceUpdates=80);save_start()
        return m,path,w,start,end,q,decoded,save_start
    def test_wgc_consistent_owner_graph_still_requires_actual_decoder(self):
        import deliver_i1 as d
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);m,path,w,s,e,q,decoded,save_start=self.fixture_wgc(root)
            with patch.object(d,'decode_continuous_video',return_value=decoded) as decoder:
                self.assertEqual(d.validate_continuous_video_evidence(root,m),[path]);self.assertEqual(decoder.call_count,1)
    def test_wgc_missing_or_unknown_exact_dependency_rejected(self):
        import deliver_i1 as d
        for rel in ['record_i1_wgc.py','native/OwnedWindowCapture.exe','native/OwnedWindowCapture.cpp']:
            with self.subTest(rel=rel),tempfile.TemporaryDirectory() as folder:
                root=Path(folder);m,path,w,s,e,q,decoded,save_start=self.fixture_wgc(root);w(d.A41_WGC_EXPORT+rel,b'coherent-index-but-unknown-bytes')
                with self.assertRaisesRegex(RuntimeError,'UnknownWGCImplementation'):d.validate_continuous_video_evidence(root,m)
    def test_wgc_foreign_surface_owner_or_creation_rejected(self):
        import deliver_i1 as d
        for field in ['hwnd','pid','birth','width','surface','desktop']:
            with self.subTest(field=field),tempfile.TemporaryDirectory() as folder:
                root=Path(folder);m,path,w,s,e,q,decoded,save_start=self.fixture_wgc(root)
                if field=='hwnd':s['backendCommand'][1]='999'
                elif field=='pid':s['backendCommand'][2]='999'
                elif field=='birth':s['backendCommand'][3]='999'
                elif field=='width':s['sourceWidth']=1
                elif field=='surface':s['command'][s['command'].index('-video_size')+1]='640x480'
                else:s['command'][s['command'].index('-i')+1]='desktop'
                save_start()
                with self.assertRaisesRegex(RuntimeError,'ContinuousVideo(WGCOwnerOrSurfaceMismatch|CaptureCommandRejected)'):d.validate_continuous_video_evidence(root,m)
    def test_wgc_failed_forced_or_static_producer_rejected(self):
        import deliver_i1 as d
        for field in ['backend','force','updates','frames','method','secondary','primary']:
            with self.subTest(field=field),tempfile.TemporaryDirectory() as folder:
                root=Path(folder);m,path,w,s,e,q,decoded,save_start=self.fixture_wgc(root)
                if field=='backend':e['backendExitCode']=5
                elif field=='force':e['forcedOwnedChildren']=['OWN_WGC_FORCE_TERMINATED']
                elif field=='updates':e['sourceUpdates']=1
                elif field=='frames':e['sourceFrames']=1
                elif field=='secondary':e['secondaryErrors']=['cleanup unresolved']
                elif field=='primary':e['primaryFailure']='original failure'
                else:s['captureMethod']='UNKNOWN_BACKEND'
                save_start()
                with self.assertRaisesRegex(RuntimeError,'ContinuousVideo(WGCCaptureFailed|UnknownCaptureDependency)'):d.validate_continuous_video_evidence(root,m)
    def test_wgc_actual_decode_framecount_and_size_must_match_surface(self):
        import deliver_i1 as d
        for field in ['frames','size']:
            with self.subTest(field=field),tempfile.TemporaryDirectory() as folder:
                root=Path(folder);m,path,w,s,e,q,decoded,save_start=self.fixture_wgc(root)
                if field=='frames':e['sourceFrames']=121;save_start()
                else:s['sourceWidth']=1918;s['command'][s['command'].index('-video_size')+1]='1918x1080';save_start()
                with patch.object(d,'decode_continuous_video',return_value=decoded):
                    with self.assertRaisesRegex(RuntimeError,'WGCSurfaceOrFrameCountMismatch'):d.validate_continuous_video_evidence(root,m)


class A41WgcOwnedLifecycleTests(unittest.TestCase):
    def functions(self):
        import ast
        source=(Path(__file__).parent/'record_i1_wgc.py').read_text('utf-8');module=ast.parse(source)
        selected=[n for n in module.body if isinstance(n,ast.FunctionDef) and n.name in ['_bounded_header','_cleanup_owned']]
        self.assertEqual(len(selected),2);scope={};exec(compile(ast.Module(body=selected,type_ignores=[]),'actual-reviewed-wgc-functions','exec'),scope);return scope['_bounded_header'],scope['_cleanup_owned']
    def child(self,source):
        import subprocess,sys
        return subprocess.Popen([sys.executable,'-B','-c',source],stdout=subprocess.PIPE,stderr=subprocess.PIPE,bufsize=0,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
    def clean(self,child,readers,cleanup):
        errors=[];forced=[];cleanup([('STUB',child)],errors,forced)
        for thread in readers:thread.join(timeout=2);self.assertFalse(thread.is_alive())
        child.stdout.close();child.stderr.close();self.assertIsNotNone(child.poll());return errors,forced
    def test_actual_owned_header_preserves_following_pixel_bytes(self):
        read,cleanup=self.functions();child=self.child('import sys;sys.stdout.buffer.write(b"WGC_BGRA 640 400\\nPIXEL");sys.stdout.flush()');readers=[]
        try:
            self.assertEqual(read(child,readers,timeout=2),['WGC_BGRA','640','400']);self.assertEqual(child.stdout.read(),b'PIXEL');child.wait(timeout=2)
        finally:self.clean(child,readers,cleanup)
    def test_actual_owned_no_header_deadline_reclaims_child_and_reader(self):
        import time
        read,cleanup=self.functions();child=self.child('import time;time.sleep(30)');readers=[];start=time.monotonic()
        try:
            with self.assertRaisesRegex(RuntimeError,'WGCHeaderDeadlineExceeded'):read(child,readers,timeout=.15)
            self.assertLess(time.monotonic()-start,2)
        finally:errors,forced=self.clean(child,readers,cleanup)
        self.assertEqual(errors,[]);self.assertEqual(forced,['OWN_STUB_FINALLY_TERMINATED'])
    def test_actual_owned_eof_is_not_valid_header(self):
        read,cleanup=self.functions();child=self.child('pass');readers=[]
        try:
            with self.assertRaisesRegex(RuntimeError,'WGCHeaderEOF'):read(child,readers,timeout=2)
        finally:self.clean(child,readers,cleanup)
    def test_actual_owned_nonterminated_header_is_bounded(self):
        read,cleanup=self.functions();child=self.child('import sys;sys.stdout.buffer.write(b"X"*200);sys.stdout.flush()');readers=[]
        try:
            with self.assertRaisesRegex(RuntimeError,'WGCMalformedHeader'):read(child,readers,timeout=2)
        finally:self.clean(child,readers,cleanup)
    def test_first_cleanup_errors_do_not_skip_real_second_child_or_replace_primary(self):
        import subprocess
        read,cleanup=self.functions();second=self.child('import time;time.sleep(30)');primary=ValueError('retained primary header failure');errors=[];forced=[]
        class First:
            def poll(self):return None
            def terminate(self):raise OSError('first terminate failed')
            def wait(self,timeout):raise subprocess.TimeoutExpired('owned-synthetic-first',timeout)
        caught=None;closed_by_cleanup=False
        try:
            try:raise primary
            finally:
                cleanup([('FIRST',First()),('SECOND',second)],errors,forced)
                closed_by_cleanup=second.poll() is not None
        except ValueError as error:caught=error
        finally:
            if second.poll() is None:second.terminate();second.wait(timeout=2)
            second.stdout.close();second.stderr.close()
        self.assertIs(caught,primary);self.assertTrue(closed_by_cleanup);self.assertIsNotNone(second.poll());self.assertEqual(len(errors),2);self.assertIn('FIRST.terminate:',errors[0]);self.assertIn('FIRST.wait:',errors[1]);self.assertEqual(forced,['OWN_FIRST_FINALLY_TERMINATED','OWN_SECOND_FINALLY_TERMINATED'])


class A41AtomicCapturePublicationTests(unittest.TestCase):
    def actual(self):
        import ast
        tree=ast.parse((Path(__file__).parent/'record_i1_wgc.py').read_text('utf-8'));nodes=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='_publish_capture_result'];self.assertEqual(len(nodes),1);scope={'json':json};exec(compile(ast.Module(body=nodes,type_ignores=[]),'actual-publish','exec'),scope);return scope['_publish_capture_result'],tree
    def test_actual_publish_completely_closes_then_renames_and_never_overwrites(self):
        publish,tree=self.actual()
        with tempfile.TemporaryDirectory() as folder:
            out=Path(folder);value={'status':'CAPTURE_CLOSED_CONTENT_AND_RUN_VALIDATION_PENDING'};publish(out,value);self.assertEqual(json.loads((out/'capture-result.json').read_text()),value);before=(out/'capture-result.json').read_bytes();self.assertFalse((out/'capture-result.unpublished.json').exists())
            with self.assertRaises(FileExistsError):publish(out,{'status':'other'})
            self.assertEqual((out/'capture-result.json').read_bytes(),before)
    def test_full_json_then_write_flush_fsync_or_close_error_never_publishes(self):
        import os
        publish,tree=self.actual();original=Path.open
        for phase in ['write','flush','fsync','close']:
            with self.subTest(phase=phase),tempfile.TemporaryDirectory() as folder:
                out=Path(folder);marker=OSError('full bytes then '+phase);value={'status':'CAPTURE_CLOSED_CONTENT_AND_RUN_VALIDATION_PENDING','primaryFailure':None,'secondaryErrors':[]}
                class Stream:
                    def __init__(self,stream):self.stream=stream
                    def __enter__(self):self.stream.__enter__();return self
                    def __getattr__(self,name):return getattr(self.stream,name)
                    def write(self,text):
                        result=self.stream.write(text)
                        if phase=='write':raise marker
                        return result
                    def flush(self):
                        self.stream.flush()
                        if phase=='flush':raise marker
                    def __exit__(self,*args):
                        value=self.stream.__exit__(*args)
                        if phase=='close':raise marker
                        return value
                def opening(path,*args,**kwargs):return Stream(original(path,*args,**kwargs))
                def sync(fd):
                    if phase=='fsync':raise marker
                with patch.object(Path,'open',opening),patch('os.fsync',side_effect=sync):
                    with self.assertRaises(OSError) as caught:publish(out,value)
                self.assertIs(caught.exception,marker);self.assertFalse((out/'capture-result.json').exists());self.assertEqual(json.loads((out/'capture-result.unpublished.json').read_text()),value)
    def test_validator_rejects_complete_but_unpublished_record(self):
        import deliver_i1 as d
        publish,tree=self.actual()
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);m,path,w,start,end,q,decoded,save_start=A41WgcContinuousVideoEvidenceTests().fixture_wgc(root)
            with patch.object(d,'decode_continuous_video',return_value=decoded):self.assertEqual(d.validate_continuous_video_evidence(root,m),[path])
            directory=root/path.rsplit('/',1)[0];canonical=directory/'capture-result.json';canonical.unlink()
            with patch('os.fsync',side_effect=OSError('bytes flushed but fsync failed')):
                with self.assertRaises(OSError):publish(directory,end)
            self.assertEqual(json.loads((directory/'capture-result.unpublished.json').read_text()),end)
            with self.assertRaisesRegex(RuntimeError,'ContinuousVideoMissingOrChangedOriginal'):d.validate_continuous_video_evidence(root,m)
    def test_actual_publication_error_tail_marks_stdout_failed_and_keeps_original_error(self):
        import ast,copy
        publish,tree=self.actual();index=next(i for i,n in enumerate(tree.body) if isinstance(n,ast.Try) and any(isinstance(x,ast.Expr) and isinstance(x.value,ast.Call) and isinstance(x.value.func,ast.Name) and x.value.func.id=='_publish_capture_result' for x in n.body));tail=ast.Module(body=tree.body[index:],type_ignores=[]);original=OSError('close failed after complete unpublished bytes');errors=[];printed=[];result={'status':'CAPTURE_CLOSED_CONTENT_AND_RUN_VALIDATION_PENDING','primaryFailure':None,'secondaryErrors':errors}
        def failed(out,value):raise original
        scope={'_publish_capture_result':failed,'OUT':None,'result':result,'primary':None,'primary_tb':None,'errors':errors,'json':json,'print':lambda value:printed.append(json.loads(value)),'okay':True}
        with self.assertRaises(OSError) as caught:exec(compile(tail,'actual-publish-tail','exec'),scope)
        self.assertIs(caught.exception,original);self.assertEqual(printed[0]['status'],'CAPTURE_FAILED_RETAINED_NOT_QUALIFIED');self.assertIn('close failed',printed[0]['primaryFailure']);self.assertTrue(printed[0]['secondaryErrors'])


class SeedLookupScriptTests(unittest.TestCase):
    def test_actual_rendered_script_does_not_match_hidden_other_seed_coordinates(self):
        import re,shutil,subprocess
        from html.parser import HTMLParser
        from deliver_i1 import render
        class Cards(HTMLParser):
            def __init__(self):super().__init__();self.cards=[];self.buttons=[];self.current=None
            def handle_starttag(self,tag,attrs):
                attrs=dict(attrs)
                if tag=='article':
                    self.current={'dataset':{k[5:]:v for k,v in attrs.items() if k.startswith('data-')},'textContent':''};self.cards.append(self.current)
                if tag=='button' and 'data-filter' in attrs:self.buttons.append({'dataset':{'filter':attrs['data-filter']}})
            def handle_data(self,data):
                if self.current is not None:self.current['textContent']+=data
            def handle_endtag(self,tag):
                if tag=='article':self.current=None
        model=fixture();order=[0,7727,-42];model['units']=[next(u for u in model['units'] if u['input']['requestedSeed']==seed) for seed in order]
        model['units'][0]['input']['fixtureCoordinates']='0.77270001,0.5'
        model['units'][2].update(status='REJECTED',kind='diagnostic',countsForReady=False,countsForTraversal=False,media=[])
        model.update(evidenceLinks=[],replay=[])
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);render(root,model);html=(root/'index.html').read_text('utf-8')
        parser=Cards();parser.feed(html);self.assertEqual(len(parser.cards),3)
        self.assertIn('7727',parser.cards[0]['textContent'])
        scripts=re.findall(r'<script>(.*?)</script>',html,re.S);self.assertEqual(len(scripts),1)
        node=shutil.which('node');self.assertIsNotNone(node,'Existing Node runtime is required for real generated JavaScript regression')
        driver=r"""
const fs=require('fs'),vm=require('vm'),input=JSON.parse(fs.readFileSync(0,'utf8'));
const cards=input.cards.map((a,i)=>({...a,testSeed:input.order[i]})),buttons=input.buttons;
const fields={'#seed':{value:''},'#frame':{value:'0'},'#frameInfo':{},'#replayImage':{},'#play':{}};
const document={querySelectorAll(s){if(s==='article')return cards;if(s==='button[data-filter]')return buttons;throw Error('Unexpected selector '+s);},querySelector(s){if(s in fields)return fields[s];throw Error('Unexpected selector '+s);}};
vm.runInNewContext(input.script,{document,Math,setInterval:()=>1,clearInterval:()=>{}});
function search(value){fields['#seed'].value=value;fields['#seed'].oninput();return cards.filter(a=>!a.hidden).map(a=>a.testSeed);}
function kind(value){buttons.find(b=>b.dataset.filter===value).onclick();return cards.filter(a=>!a.hidden).map(a=>a.testSeed);}
const found=search('7727'),spaced=search(' 7727 '),negative=search('-42'),missing=search('999'),empty=search('');
const playable=kind('PASS'),rejected=kind('REJECTED'),all=kind('all');
process.stdout.write(JSON.stringify({found,spaced,negative,missing,empty,playable,rejected,all}));
"""
        process=subprocess.run([node,'-e',driver],input=json.dumps({'cards':parser.cards,'buttons':parser.buttons,'order':order,'script':scripts[0]}),capture_output=True,text=True,encoding='utf-8',timeout=15,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
        self.assertEqual(process.returncode,0,process.stderr);result=json.loads(process.stdout)
        self.assertEqual(result['found'],[7727]);self.assertEqual(result['spaced'],[7727]);self.assertEqual(result['negative'],[-42]);self.assertEqual(result['missing'],[])
        self.assertEqual(result['empty'],order);self.assertEqual(result['playable'],[0,7727]);self.assertEqual(result['rejected'],[-42]);self.assertEqual(result['all'],order)


class MediaProfileExportTests(unittest.TestCase):
    def dependencies(self,d):
        return [(d.A41_VIDEO_HELPER_EXPORT,'Tools/MapVNext/record_i1_player.py')]+[(d.A41_WGC_EXPORT+rel,'Tools/MapVNext/'+rel) for rel in ['record_i1_wgc.py','native/OwnedWindowCapture.exe','native/OwnedWindowCapture.cpp']]
    def export_prefix(self,d):
        admission=d.postbuild_module('map-vnext-i1-integration-20261031-a41')
        return 'Evidence/PostBuild/'+d.postbuild_export_folders(admission)[1]+'/',admission
    def current_group_fixture(self,root):
        import deliver_i1 as d
        data=A41WgcContinuousVideoEvidenceTests().fixture_wgc(root)
        model,path,write,*remaining=data;prefix,admission=self.export_prefix(d)
        for previous,rel in self.dependencies(d):
            target=prefix+rel;content=(root/previous).read_bytes();write(target,content)
            if previous!=target:(root/previous).unlink();model['files'].pop(previous)
        return data
    def test_capture_dependencies_belong_to_actual_admission_export_set(self):
        import deliver_i1 as d
        prefix,admission=self.export_prefix(d);exported={prefix+rel for rel in admission.TOOL_FILES};needed={path for path,rel in self.dependencies(d)}
        self.assertTrue(needed<=exported,'Media dependency not exported by actual current admission: '+repr(sorted(needed-exported)))
    def test_wgc_current_export_only_positive_and_foreign_group_negative(self):
        import deliver_i1 as d
        with tempfile.TemporaryDirectory() as folder:
            root=Path(folder);m,path,w,s,e,q,decoded,save_start=self.current_group_fixture(root)
            with patch.object(d,'decode_continuous_video',return_value=decoded) as decoder:
                self.assertEqual(d.validate_continuous_video_evidence(root,m),[path]);self.assertEqual(decoder.call_count,1)
        for dependency,rel in self.dependencies(d):
            with self.subTest(rel=rel),tempfile.TemporaryDirectory() as folder:
                root=Path(folder);m,path,w,s,e,q,decoded,save_start=self.current_group_fixture(root);prefix,admission=self.export_prefix(d);current=prefix+rel
                w('Evidence/PostBuild/ForeignUnreviewed/'+rel,(root/current).read_bytes());(root/current).unlink();m['files'].pop(current)
                with patch.object(d,'decode_continuous_video') as decoder:
                    with self.assertRaisesRegex(RuntimeError,'ContinuousVideo(MissingOrChangedOriginal|UnknownWGCImplementation)'):d.validate_continuous_video_evidence(root,m)
                    decoder.assert_not_called()
