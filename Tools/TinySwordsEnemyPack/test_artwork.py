import json
from pathlib import Path
import shutil
import tempfile
import unittest
import zipfile

from PIL import Image
import artwork


class RestorationBoundaryTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory(prefix="enemy-pack-contract-")
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.source, self.project, self.kit = (self.root / p for p in ("Source", "Project", "PrivateKit"))
        image = self.source / "Enemy Pack/Test/Test_Idle.png"
        image.parent.mkdir(parents=True)
        Image.new("RGBA", (16, 16), (0, 255, 0, 255)).save(image)
        inventory = artwork.audit(self.source, self.root / "Audit")
        row = inventory["files"][0]
        self.plan = {"sourceFingerprint": inventory["sourceFingerprint"], "sourceValidation": inventory,
                     "assets": [{"path": row["target"], "sha256": row["sha256"]}]}
        self.planpath = self.project / "Tools/TinySwordsEnemyPack/import-plan.json"
        artwork.write_json(self.planpath, self.plan)
        artwork.copy_art(self.source, self.project, inventory)
        art = self.project / row["target"]
        Path(str(art)+".meta").write_text("fileFormatVersion: 2\nguid: " + "1"*32 + "\n", encoding="utf-8")
        Path(str(self.project / artwork.ART_ROOT)+".meta").write_text("fileFormatVersion: 2\nguid: " + "2"*32 + "\n", encoding="utf-8")
        golden = self.root / "golden.json"
        artwork.write_json(golden, {"status": "PASS", "sourceFingerprint": inventory["sourceFingerprint"]})
        archive = self.root / "source.zip"
        with zipfile.ZipFile(archive, "w") as z:
            z.write(image, "Source/" + image.relative_to(self.source).as_posix())
        artwork.create_kit(self.source, self.project, self.planpath, golden, archive, self.kit)
        self.checkout = self.root / "Checkout"
        targetplan = self.checkout / "Tools/TinySwordsEnemyPack/import-plan.json"
        targetplan.parent.mkdir(parents=True)
        shutil.copyfile(self.planpath, targetplan)

    def restore(self):
        return artwork.restore(self.source, self.checkout, self.kit, offline_check=lambda _: None)

    def test_parentheses_only_and_traversal_rejection(self):
        self.assertEqual(artwork.clean_relative("A (test)/Keep Space_CASE.png"), "A test/Keep Space_CASE.png")
        for raw in ("../bad.png", "/abs.png", "C:/bad.png", "folder./file.png"):
            with self.assertRaises(ValueError):
                artwork.clean_relative(raw)

    def test_name_collision_rejected_before_copy(self):
        p = self.source / "Enemy Pack/Test/Test_(Idle).png"
        shutil.copyfile(self.source / "Enemy Pack/Test/Test_Idle.png", p)
        with self.assertRaisesRegex(ValueError, "PATH_COLLISION"):
            artwork.audit(self.source, self.root / "Collision")
        self.assertFalse((self.root / "Collision/source-inventory.json").exists())

    def test_restore_then_no_op_preserves_all_bytes(self):
        first = self.restore()
        before = {p.relative_to(self.checkout).as_posix(): artwork.digest(p) for p in self.checkout.rglob("*") if p.is_file()}
        second = self.restore()
        after = {p.relative_to(self.checkout).as_posix(): artwork.digest(p) for p in self.checkout.rglob("*") if p.is_file()}
        self.assertEqual(first["status"], "RESTORED")
        self.assertEqual(second["status"], "NO_OP")
        self.assertEqual(before, after)

    def test_wrong_source_fails_before_any_art_written(self):
        (self.source / "Enemy Pack/Test/Test_Idle.png").write_bytes(b"wrong-version")
        with self.assertRaisesRegex(ValueError, "SOURCE_SHA_MISMATCH"):
            self.restore()
        self.assertFalse((self.checkout / artwork.ART_ROOT).exists())

    def test_missing_source_fails_closed(self):
        (self.source / "Enemy Pack/Test/Test_Idle.png").unlink()
        with self.assertRaisesRegex(ValueError, "SOURCE_FILE_SET_CHANGED"):
            self.restore()

    def test_missing_private_kit_fails_closed(self):
        with self.assertRaisesRegex(ValueError, "PRIVATE_KIT_MISSING"):
            artwork.restore(self.source, self.checkout, self.root / "MissingKit", offline_check=lambda _: None)

    def test_kit_tampering_fails_closed(self):
        (self.kit / "asset-guid-map.json").write_text("{}", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "PRIVATE_KIT_SHA_MISMATCH"):
            self.restore()

    def test_existing_valid_asset_not_overwritten_on_conflict(self):
        self.restore()
        target = self.checkout / self.plan["assets"][0]["path"]
        target.write_bytes(b"local-change")
        with self.assertRaisesRegex(ValueError, "TARGET_SHA_CONFLICT"):
            self.restore()
        self.assertEqual(target.read_bytes(), b"local-change")

    def test_duplicate_project_guid_rejected_before_copy(self):
        p = self.checkout / "Assets/Other.meta"
        p.parent.mkdir(parents=True)
        p.write_text("guid: " + "1"*32 + "\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "PROJECT_GUID_COLLISION"):
            self.restore()
        self.assertFalse((self.checkout / artwork.ART_ROOT).exists())

    def test_running_editor_refuses_restoration(self):
        def busy(_):
            raise ValueError("RESTORE_REQUIRES_FREE_GUARD")
        with self.assertRaisesRegex(ValueError, "RESTORE_REQUIRES_FREE_GUARD"):
            artwork.restore(self.source, self.checkout, self.kit, offline_check=busy)
        self.assertFalse((self.checkout / artwork.ART_ROOT).exists())

    def test_orphan_metadata_mismatch_is_not_replaced(self):
        self.restore()
        meta = self.checkout / (self.plan["assets"][0]["path"] + ".meta")
        meta.write_text("guid: " + "3"*32 + "\n", encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "TARGET_SHA_CONFLICT"):
            self.restore()
        self.assertIn("3"*32, meta.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main(verbosity=2)
