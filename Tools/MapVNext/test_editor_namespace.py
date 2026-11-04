import tempfile
import types
import unittest
from pathlib import Path
from unittest.mock import patch
import captured_inputs


class PrivateEditorNamespaceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='shop-editor-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.path = self.root / 'ProjectSettings/ProjectSettings.asset'
        self.path.parent.mkdir()
        self.before = b'PlayerSettings:\r\n  companyName: Original\r\n  productName: RPG_Demo\r\n  runInBackground: 0\r\n'
        self.path.write_bytes(self.before)

    def test_namespace_precedes_test_and_restores_exact_bytes_after_exception(self):
        guard = types.SimpleNamespace(status=lambda _: {'state': 'FREE'})
        with patch.dict('sys.modules', unity_guard=guard):
            with self.assertRaisesRegex(ValueError, 'test failed'):
                with captured_inputs.private_editor_settings(self.root, 's35-edit'):
                    data = self.path.read_bytes()
                    self.assertIn(b'companyName: CodexShopEditorAcceptance\r\n', data)
                    self.assertIn(b'productName: s35-edit\r\n', data)
                    self.assertEqual(data.count(b'\r\n'), self.before.count(b'\r\n'))
                    raise ValueError('test failed')
        self.assertEqual(self.path.read_bytes(), self.before)

    def test_unknown_owner_blocks_before_change(self):
        guard = types.SimpleNamespace(status=lambda _: {'state': 'UNKNOWN'})
        with patch.dict('sys.modules', unity_guard=guard):
            with self.assertRaisesRegex(RuntimeError, 'IdleProject'):
                with captured_inputs.private_editor_settings(self.root, 's35-edit'):
                    self.fail('must not enter')
        self.assertEqual(self.path.read_bytes(), self.before)

    def test_unexpected_delta_is_preserved_for_diagnosis(self):
        guard = types.SimpleNamespace(status=lambda _: {'state': 'FREE'})
        with patch.dict('sys.modules', unity_guard=guard):
            with self.assertRaisesRegex(RuntimeError, 'UnexpectedEditorNamespaceSettingsDelta'):
                with captured_inputs.private_editor_settings(self.root, 's35-edit'):
                    self.path.write_bytes(self.path.read_bytes() + b'unknown edit\r\n')
        self.assertTrue(self.path.read_bytes().endswith(b'unknown edit\r\n'))

    def test_live_owner_after_test_prevents_settings_rewrite(self):
        calls = iter(['FREE', 'BUSY_EXTERNAL'])
        guard = types.SimpleNamespace(status=lambda _: {'state': next(calls)})
        with patch.dict('sys.modules', unity_guard=guard):
            with self.assertRaisesRegex(RuntimeError, 'LiveOrUnknownOwner'):
                with captured_inputs.private_editor_settings(self.root, 's35-edit'):
                    pass
        self.assertIn(b'CodexShopEditorAcceptance', self.path.read_bytes())


if __name__ == '__main__':
    unittest.main()
