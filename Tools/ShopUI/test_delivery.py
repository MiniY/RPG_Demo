import copy
import unittest
from deliver import REQUIRED, validate_model


class ReadableShopDeliveryTests(unittest.TestCase):
    def model(self):
        unit = dict(id='formal-shop', kind='runtime', humanStatus='PENDING_USER', autoStatus='PASS',
                    autoSubject='明确的自动运行探针', autoScope='正式场景商店购买及关闭', media=[])
        unit.update({field: '中文验收说明' for field in REQUIRED})
        return dict(build='actual-sealed-build', sourceCommit='a' * 40, humanStatus='PENDING_USER',
                    units=[unit], files={})

    def test_bounded_chinese_runtime_evidence_allows_pending_human(self):
        self.assertEqual(validate_model(self.model()), [])

    def test_each_missing_or_english_only_readability_field_is_rejected(self):
        for field in REQUIRED:
            for value in ['', 'English-only case label']:
                model = self.model()
                model['units'][0][field] = value
                self.assertTrue(validate_model(model), field)

    def test_unscoped_automatic_pass_or_material_pass_is_rejected(self):
        for field, value in [('autoSubject', ''), ('autoScope', ''), ('kind', 'source-material')]:
            model = self.model()
            model['units'][0][field] = value
            self.assertTrue(validate_model(model))

    def test_human_pass_cannot_be_invented_at_either_level(self):
        for root in [True, False]:
            model = self.model()
            (model if root else model['units'][0])['humanStatus'] = 'PASS'
            self.assertTrue(validate_model(model))

    def test_media_requires_actual_matching_build_and_run(self):
        model = self.model()
        model['units'][0]['media'] = [dict(path='Media/shot.png', build=model['build'], runId='run-real')]
        self.assertTrue(validate_model(model))
        model['files']['Media/shot.png'] = dict(build='old-build', runId='run-real')
        self.assertTrue(validate_model(model))
        model['files']['Media/shot.png']['build'] = model['build']
        self.assertEqual(validate_model(model), [])
        model['units'][0]['media'][0]['build'] = 'old-build'
        model['files']['Media/shot.png']['build'] = 'old-build'
        self.assertTrue(validate_model(model))
        model['units'][0]['media'][0]['role'] = 'historical-reference'
        self.assertEqual(validate_model(model), [])

    def test_sampled_replay_must_be_labelled_and_case_ids_unique(self):
        model = self.model()
        model['files']['Media/idle.gif'] = dict(build=model['build'], runId='run-real', sampledReplay=True)
        model['units'][0]['media'] = [dict(path='Media/idle.gif', build=model['build'], runId='run-real', caption='连续录像')]
        self.assertTrue(validate_model(model))
        model['units'][0]['media'][0]['caption'] = '真实帧采样回放'
        self.assertEqual(validate_model(model), [])
        model['units'].append(copy.deepcopy(model['units'][0]))
        self.assertTrue(validate_model(model))


if __name__ == '__main__':
    unittest.main()
