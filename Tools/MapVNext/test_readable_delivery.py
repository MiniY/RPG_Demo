"""Finite positive/negative contracts for report semantics, never Unity gameplay tests."""
import copy
import json
import pathlib
import sys
import unittest
import uuid

import deliver_readable as delivery


def normal_model():
    unit = dict(id='V00', title='示例：左侧楼梯', purpose='检查接缝', look='看下口',
                actions='同时按两个方向键', normal='沿斜坡走上去', abnormal='纯横移跨层',
                verification='程序记录可用，人工待确认', kind='playable', human_status='AWAITING_REMAINING_ISSUES',
                counts_for_traversal=True, auto_status='PASS', auto_subject='历史程序', auto_scope='双向通行',
                media=[dict(path='Media/frame.png',build='r3d',run_id='run')])
    return dict(build='r3d', feedback=dict(build='r3d',source_file='input.md',source_sha256='0'*64,
                quote='用户仅确认接口视觉',individual_case_reviews=[],p1_closed=False,
                issues=dict(ISSUE01='PASS_SCOPED_TO_R3D_EXTENDED_MATRIX',ISSUE02='AWAITING_USER_CONFIRMATION',
                            ISSUE03='AWAITING_USER_CONFIRMATION',READABILITY='AWAITING_USER_CONFIRMATION')),
                units=[unit],traversal_denominator=1,files={'Media/frame.png':dict(build='r3d',run_id='run')})


class ContractTests(unittest.TestCase):
    def setUp(self):
        self.model=normal_model()

    def reject(self):
        self.assertTrue(delivery.validate_model(self.model))

    def test_chinese_playable_and_scoped_feedback_accepted(self):
        self.assertEqual([],delivery.validate_model(self.model))

    def test_every_missing_required_explanation_rejected(self):
        for key in delivery.REQUIRED:
            with self.subTest(key=key):
                bad=copy.deepcopy(self.model);bad['units'][0].pop(key)
                self.assertTrue(delivery.validate_model(bad))

    def test_english_only_case_title_rejected(self):
        self.model['units'][0]['title']='V00-H0-H1-West-OpenLanding';self.reject()

    def test_material_playable_pass_rejected(self):
        self.model['units'][0]['kind']='material';self.reject()

    def test_material_in_denominator_rejected_even_without_pass(self):
        u=self.model['units'][0];u.update(kind='material',auto_status='NOT_APPLICABLE',counts_for_traversal=False);self.reject()

    def test_material_reference_not_counted_accepted(self):
        u=self.model['units'][0];u.update(kind='material',auto_status='NOT_APPLICABLE',counts_for_traversal=False)
        self.model['traversal_denominator']=0
        self.assertEqual([],delivery.validate_model(self.model))

    def test_pass_without_subject_or_scope_rejected(self):
        for field in ['auto_subject','auto_scope']:
            with self.subTest(field=field):
                bad=copy.deepcopy(self.model);bad['units'][0].pop(field)
                self.assertTrue(delivery.validate_model(bad))

    def test_unfounded_individual_human_pass_rejected(self):
        self.model['units'][0]['human_status']='PASS';self.reject()

    def test_group_feedback_cannot_fabricate_individual_review(self):
        self.model['feedback']['individual_case_reviews']=['V00'];self.reject()

    def test_issue01_feedback_cannot_accept_issue02_or03(self):
        for field in ['ISSUE02','ISSUE03']:
            with self.subTest(field=field):
                bad=copy.deepcopy(self.model);bad['feedback']['issues'][field]='PASS'
                self.assertTrue(delivery.validate_model(bad))

    def test_human_pass_without_source_rejected(self):
        self.model['feedback']['source_file']='';self.reject()

    def test_feedback_build_mismatch_rejected(self):
        self.model['feedback']['build']='r3c';self.reject()

    def test_media_build_and_run_mismatch_rejected(self):
        for field in ['build','run_id']:
            with self.subTest(field=field):
                bad=copy.deepcopy(self.model);bad['units'][0]['media'][0][field]='wrong'
                self.assertTrue(delivery.validate_model(bad))

    def test_explicit_before_and_after_sources_accepted(self):
        before=dict(path='Media/before.png',build='r2',run_id='old',role='before')
        self.model['units'][0]['media'].append(before)
        self.model['files']['Media/before.png']=dict(build='r2',run_id='old')
        self.assertEqual([],delivery.validate_model(self.model))

    def test_wrong_old_media_label_as_after_rejected(self):
        self.model['files']['Media/frame.png']['build']='r2'
        self.model['units'][0]['media'][0]['build']='r2';self.reject()

    def test_missing_media_reference_rejected(self):
        self.model['files'].clear();self.reject()

    def test_closed_p1_rejected(self):
        self.model['feedback']['p1_closed']=True;self.reject()

    def test_duplicate_units_rejected(self):
        self.model['units'].append(copy.deepcopy(self.model['units'][0]));self.reject()



class AppendFeedbackTests(unittest.TestCase):
    def setUp(self):
        self.model=normal_model()
        cfg=delivery.read(delivery.HERE/'readable_delivery.json')
        event=copy.deepcopy(cfg['feedback']['append'])
        self.model['build']=event['build']
        self.model['feedback']['build']=event['build']
        for u in self.model['units']:
            for m in u['media']: m['build']=event['build']
        self.model['files']['Media/frame.png']['build']=event['build']
        event.update(source_sha256='1'*64,source_copy='Evidence/Input/addendum.md')
        self.model['files'][event['source_copy']]=dict(sha256='1'*64)
        feedback=self.model['feedback']
        feedback['events']=[copy.deepcopy(feedback),event]
        feedback['legacy_root_is_historical_snapshot']=True
        feedback['current']=delivery.feedback_summary(feedback)
        self.model.update(p1_closed=False,p2_started=False)

    def reject(self):
        self.assertTrue(delivery.validate_model(self.model))

    def test_basic_visual_and_understanding_scoped_acceptance(self):
        self.assertEqual([],delivery.validate_model(self.model))

    def test_current_cannot_promote_operations_random_formal_or_stage(self):
        for field in ['human_operation','random_layout','formal_integration','p1_closed','p2_started']:
            with self.subTest(field=field):
                bad=copy.deepcopy(self.model);bad['feedback']['current'][field]='PASS'
                self.assertTrue(delivery.validate_model(bad))

    def test_event_cannot_expand_accepted_scope(self):
        self.model['feedback']['events'][-1]['accepted'].append('random_layout');self.reject()

    def test_no_fabricated_precise_user_review_time(self):
        self.model['feedback']['events'][-1]['user_reviewed_at']='2026-10-29T12:00:00+08:00';self.reject()

    def test_missing_sync_timezone_rejected(self):
        self.model['feedback']['events'][-1]['synced_at_utc']='2026-10-29T12:00:00';self.reject()

    def test_wrong_reviewed_report_and_build_rejected(self):
        for field in ['report_revision','build']:
            with self.subTest(field=field):
                bad=copy.deepcopy(self.model);bad['feedback']['events'][-1][field]='new-unreviewed'
                self.assertTrue(delivery.validate_model(bad))

    def test_changed_old_feedback_rejected(self):
        self.model['feedback']['events'][0]['quote']='改写旧反馈';self.reject()

    def test_missing_actual_attachment_hash_rejected(self):
        self.model['files'].pop('Evidence/Input/addendum.md');self.reject()

    def test_no_fabricated_individual_answers(self):
        self.model['feedback']['events'][-1]['individual_case_reviews']=['V00'];self.reject()

    def test_actual_stage_cannot_advance(self):
        for field in ['p1_closed','p2_started']:
            with self.subTest(field=field):
                bad=copy.deepcopy(self.model);bad[field]=True
                self.assertTrue(delivery.validate_model(bad))


class FileTests(unittest.TestCase):
    def fixture(self):
        # Retained under the requested receipt directory: no recursive test cleanup.
        root=OUTPUT/'ContractFixtures'/uuid.uuid4().hex;root.mkdir(parents=True)
        (root/'Media').mkdir();(root/'Media/frame.png').write_bytes(b'retained-fixture')
        (root/'MVP_Windows_x64').mkdir();(root/'MVP_Windows_x64/demo.exe').write_bytes(b'fixture-player')
        (root/'Evidence/r3d').mkdir(parents=True);(root/'Report').mkdir()
        model=normal_model();model['files']['Media/frame.png']['sha256']=delivery.sha(root/'Media/frame.png')
        delivery.save(root/'display-metadata.json',model)
        delivery.save(root/'Evidence/r3d/build-tree.json',delivery.tree(root/'MVP_Windows_x64'))
        delivery.save(root/'Report/frame-evidence.json',{})
        fields=''.join('<p>'+delivery.e(model['units'][0][field])+'</p>' for field in delivery.REQUIRED)
        (root/'Report/index.html').write_text('<html lang="zh-CN"><article id="V00"><img src="../Media/frame.png">'+fields+'</article></html>','utf-8')
        return root

    def test_complete_relative_package_accepted(self):
        self.assertEqual('PASS',delivery.check_package(self.fixture())['status'])

    def test_missing_image_file_rejected(self):
        root=self.fixture();model=delivery.read(root/'display-metadata.json')
        model['files']['Media/missing.png']=model['files'].pop('Media/frame.png')
        delivery.save(root/'display-metadata.json',model)
        self.assertEqual('FAIL',delivery.check_package(root)['status'])

    def test_broken_link_and_fragment_rejected(self):
        root=self.fixture();(root/'Report/index.html').write_text('<a href="missing.html">原图</a><a href="#missing">定位</a>','utf-8')
        self.assertGreaterEqual(len(delivery.check_package(root)['errors']),2)

    def test_data_dll_change_rejected_even_if_exe_unchanged(self):
        root=self.fixture();(root/'MVP_Windows_x64/changed.dll').write_bytes(b'drift')
        self.assertEqual('FAIL',delivery.check_package(root)['status'])

    def test_metadata_present_but_template_placeholder_not_expanded_rejected(self):
        root=self.fixture()
        (root/'Report/index.html').write_text('<article id="V00">{descriptions(u)} {related}</article>','utf-8')
        errors=delivery.check_package(root)['errors']
        self.assertTrue(any('页面没有呈现元数据字段' in x for x in errors))


OUTPUT=pathlib.Path(sys.argv[1]).resolve() if len(sys.argv)>1 else delivery.REPO/'Artifacts/MapAcceptance/ReadableContractChecks'


if __name__=='__main__':
    OUTPUT.mkdir(parents=True,exist_ok=True)
    suite=unittest.defaultTestLoader.loadTestsFromModule(sys.modules[__name__])
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    delivery.save(OUTPUT/'positive-negative.json',dict(status='PASS' if result.wasSuccessful() else 'FAIL',
        framework='Python unittest; actual executed report contracts, not Unity tests',
        tests_run=result.testsRun,failures=[str(x) for x in result.failures],errors=[str(x) for x in result.errors],
        source_hashes={p.name:delivery.sha(p) for p in delivery.HERE.glob('readable*') if p.is_file()},
        generator_sha256=delivery.sha(delivery.HERE/'deliver_readable.py'),
        user_readability_acceptance='USER_UNDERSTOOD_SCOPED_TO_R3D_READABLE_V1_FROM_ADDENDUM',
        human_operation_acceptance='NO_NEW_HUMAN_PASS',random_layout_acceptance='NOT_DELIVERED_OR_ACCEPTED_FOR_THIS_CANDIDATE'))
    raise SystemExit(0 if result.wasSuccessful() else 2)
