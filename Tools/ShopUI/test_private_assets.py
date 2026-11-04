from pathlib import Path
import hashlib
import json
import subprocess
import tempfile
import unittest
from unittest.mock import patch
from private_assets import inspect, load_manifest, restore, safe_file


class PrivateAssetTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='shop-private-')
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.project = self.base / 'project'
        self.kit = self.base / 'kit'
        self.project.mkdir()
        self.kit.mkdir()
        rows = []
        for i, name in enumerate(['Assets/Font/MyTMP.asset', 'Assets/ShopUiNightRun/Textures/Test.png']):
            data = ('private-payload-' + str(i)).encode()
            guid = str(i) * 32
            meta = ('fileFormatVersion: 2\nguid: ' + guid + '\n').encode()
            (self.kit / name).parent.mkdir(parents=True, exist_ok=True)
            (self.kit / name).write_bytes(data)
            target = self.project / (name + '.meta')
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(meta)
            rows.append(dict(path=name, sha256=hashlib.sha256(data).hexdigest(), bytes=len(data),
                             guid=guid, metaSha256=hashlib.sha256(meta).hexdigest()))
        self.manifest = dict(schema=1, kind='SHOP_PRIVATE_PAYLOADS_V1', files=rows)
        self.path = self.project / 'private-assets.json'
        self.save_manifest()
        self.git = patch('private_assets.subprocess.run', return_value=subprocess.CompletedProcess(
            [], 128, b'', b'fatal: not a git repository'))
        self.git.start()
        self.addCleanup(self.git.stop)

    def save_manifest(self):
        body = json.dumps(self.manifest).encode()
        self.path.write_bytes(body)
        (self.kit / 'private-assets.json').write_bytes(body)

    def perform(self, check=lambda _: None):
        return restore(self.project, self.path, self.kit, check)

    def assert_no_payloads(self):
        self.assertTrue(all(not (self.project / r['path']).exists() for r in self.manifest['files']))

    def test_exact_complete_restore_is_idempotent_and_preserves_meta(self):
        result = self.perform()
        self.assertEqual(result['status'], 'RESTORED')
        self.assertEqual(result['restored'], 2)
        self.assertTrue(all(r['state'] == 'EXACT' for r in inspect(self.project, self.manifest)))
        self.assertEqual(self.perform()['status'], 'EXACT_NO_OP')

    def test_corrupt_last_payload_is_rejected_before_any_write(self):
        (self.kit / self.manifest['files'][-1]['path']).write_bytes(b'wrong')
        with self.assertRaisesRegex(RuntimeError, 'PayloadMismatch'):
            self.perform()
        self.assert_no_payloads()

    def test_wrong_manifest_or_meta_rejected_before_any_write(self):
        (self.kit / 'private-assets.json').write_bytes(b'{}')
        with self.assertRaisesRegex(RuntimeError, 'ManifestMismatch'):
            self.perform()
        self.save_manifest()
        (self.project / (self.manifest['files'][-1]['path'] + '.meta')).write_bytes(b'unknown')
        with self.assertRaisesRegex(RuntimeError, 'MetadataMismatch'):
            self.perform()
        self.assert_no_payloads()

    def test_existing_unknown_bytes_are_never_overwritten(self):
        target = self.project / self.manifest['files'][-1]['path']
        target.write_bytes(b'user-data')
        with self.assertRaisesRegex(RuntimeError, 'ExistingBytesConflict'):
            self.perform()
        self.assertEqual(target.read_bytes(), b'user-data')
        self.assertFalse((self.project / self.manifest['files'][0]['path']).exists())

    def test_busy_or_unknown_owner_stops_before_writes(self):
        def blocked(_):
            raise RuntimeError('UNKNOWN owner')
        with self.assertRaisesRegex(RuntimeError, 'UNKNOWN'):
            self.perform(blocked)
        self.assert_no_payloads()

    def test_private_kit_cannot_be_a_git_worktree_or_inside_project(self):
        with patch('private_assets.subprocess.run', return_value=subprocess.CompletedProcess([], 0, b'repo', b'')):
            with self.assertRaisesRegex(RuntimeError, 'OutsideAnyGit'):
                self.perform()
        with self.assertRaisesRegex(RuntimeError, 'OutsideProject'):
            restore(self.project, self.path, self.project, lambda _: None)
        self.assert_no_payloads()

    def test_path_traversal_and_absolute_paths_are_rejected(self):
        for name in ['../escape', 'Assets/../escape', 'C:/escape', '/escape', 'Assets\\escape', 'Assets//escape']:
            with self.assertRaisesRegex(RuntimeError, 'UnsafePrivateAssetPath'):
                safe_file(self.project, name)
        self.manifest['files'][0]['path'] = 'Assets/Scripts/Unexpected.cs'
        self.save_manifest()
        with self.assertRaisesRegex(RuntimeError, 'ManifestIdentity'):
            load_manifest(self.path)

    def test_mid_batch_failure_rolls_back_only_new_exact_owned_payloads(self):
        import os
        original = os.link
        def fail_last(source, target):
            if str(target).endswith('Test.png'):
                raise OSError('injected IO error')
            return original(source, target)
        with patch('private_assets.os.link', side_effect=fail_last):
            with self.assertRaisesRegex(OSError, 'injected'):
                self.perform()
        self.assert_no_payloads()
        self.assertEqual(list(self.project.rglob('.shop-restore-*')), [])


if __name__ == '__main__':
    unittest.main()
