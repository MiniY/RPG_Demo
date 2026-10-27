import unittest
from publication import scan_blob, classify_diff_check


class PublicBlobBoundaryTests(unittest.TestCase):
    def test_renamed_embedded_paid_png_rejected(self):
        _, errors = scan_blob("Tools/fake.md", b"prefix\x89PNG\r\n\x1a\npixels", set())
        self.assertIn("BINARY_OR_IMAGE_SIGNATURE", errors)

    def test_inline_image_rejected(self):
        _, errors = scan_blob("Tools/fake.json", ('{"image":"data:image/' + 'png;base64,AAAA"}').encode(), set())
        self.assertIn("INLINE_IMAGE_DATA", errors)

    def test_lfs_does_not_hide_restricted_files(self):
        _, errors = scan_blob("Tools/fake.md", ("version https://git-lfs.github.com/" + "spec/v1\noid sha256:abc\n").encode(), set())
        self.assertIn("LFS_POINTER", errors)

    def test_personal_path_and_credentials_rejected(self):
        raw = ("C:" + "/Us" + "ers/private/account ghp_" + "x" * 30).encode()
        _, errors = scan_blob("Tools/fake.md", raw, set())
        self.assertIn("PERSONAL_ABSOLUTE_PATH", errors)
        self.assertIn("CREDENTIAL_PATTERN", errors)

    def test_text_extension_is_not_an_allowance(self):
        _, errors = scan_blob("Tools/fake.md", b"\x00\x01\x02\x03", set())
        self.assertIn("BINARY_NUL", errors)

    def test_safe_structural_metadata(self):
        _, errors = scan_blob("Assets/new.png.meta", ("guid: " + "1"*32 + "\nTextureImporter:\n  spriteMode: 2\n  textureCompression: 0\n").encode(), set())
        self.assertEqual(errors, [])

    def test_unity_empty_scalar_keeps_frozen_bytes(self):
        result = classify_diff_check(2, "Assets/new.png.meta:7: trailing whitespace.\n+  userData: \n")
        self.assertEqual(result["status"], "REVIEWED_UNITY_EMPTY_SCALARS")
        self.assertEqual(result["rawExitCode"], 2)
        self.assertEqual(result["unityEmptyScalars"], 1)

    def test_nonempty_yaml_whitespace_is_not_exempt(self):
        result = classify_diff_check(2, "Assets/new.prefab:7: trailing whitespace.\n+  m_Name: New \n")
        self.assertEqual(result["status"], "FAIL")

    def test_code_empty_scalar_is_not_exempt(self):
        result = classify_diff_check(2, "Tools/new.py:7: trailing whitespace.\n+  userData: \n")
        self.assertEqual(result["status"], "FAIL")

    def test_other_git_errors_cannot_be_accepted_as_formatting(self):
        self.assertEqual(classify_diff_check(128, "fatal: invalid index")["status"], "FAIL")
        mixed = "Assets/new.png.meta:7: trailing whitespace.\n+  userData: \nTools/new.py:1: new blank line at EOF.\n"
        self.assertEqual(classify_diff_check(2, mixed)["status"], "FAIL")


if __name__ == "__main__":
    unittest.main(verbosity=2)
