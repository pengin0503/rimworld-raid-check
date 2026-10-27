import importlib.util
import shutil
import tempfile
import unittest
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("raid_package", ROOT / "tools/package.py")
pkg = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pkg)


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.mod = self.root / "Mod"
        shutil.copytree(ROOT / "Mod", self.mod, ignore=shutil.ignore_patterns("*.dll"))
        dll = self.mod / "1.7/Assemblies/RaidCheck.dll"
        dll.parent.mkdir(parents=True, exist_ok=True)
        dll.write_bytes(b"MZ" + b"\0" * 126)  # Synthetic packaging input, not a game DLL.
        self.zip = self.root / "output.zip"

    def tearDown(self):
        self.tmp.cleanup()

    def test_single_install_root_and_only_explicit_runtime_files(self):
        (self.mod / "private.log").write_text("not for distribution")
        (self.mod / "1.7/Assemblies/Assembly-CSharp.dll").write_bytes(b"MZ-private")
        pkg.build_package(self.mod, self.zip)
        with zipfile.ZipFile(self.zip) as archive:
            names = set(archive.namelist())
            self.assertIn("RaidCheck/About/About.xml", names)
            self.assertIn("RaidCheck/1.7/Assemblies/RaidCheck.dll", names)
            self.assertIn("RaidCheck/Languages/Japanese/Keyed/RaidCheck.xml", names)
            self.assertEqual({"RaidCheck"}, {p.split("/")[0] for p in names})
            self.assertNotIn("RaidCheck/private.log", names)
            self.assertNotIn("RaidCheck/1.7/Assemblies/Assembly-CSharp.dll", names)

    def test_missing_or_invalid_assembly_is_rejected(self):
        dll = self.mod / "1.7/Assemblies/RaidCheck.dll"
        dll.unlink()
        with self.assertRaises(ValueError):
            pkg.build_package(self.mod, self.zip)
        dll.write_bytes(b"not an assembly")
        with self.assertRaises(ValueError):
            pkg.build_package(self.mod, self.zip)

    def test_localization_mismatch_and_bad_xml_are_rejected(self):
        ja = self.mod / "Languages/Japanese/Keyed/RaidCheck.xml"
        ja.write_text("<LanguageData><RC_Title>x</RC_Title></LanguageData>")
        with self.assertRaises(ValueError):
            pkg.build_package(self.mod, self.zip)
        ja.write_text("<broken")
        with self.assertRaises(ValueError):
            pkg.build_package(self.mod, self.zip)

    def test_missing_metadata_or_wrong_package_identity_is_rejected(self):
        about = self.mod / "About/About.xml"
        about.write_text(about.read_text().replace("pengin0503.raidcheck", "different.mod"))
        with self.assertRaises(ValueError):
            pkg.build_package(self.mod, self.zip)

    def test_symlinked_payload_is_rejected(self):
        dll = self.mod / "1.7/Assemblies/RaidCheck.dll"
        other = self.root / "elsewhere.dll"
        other.write_bytes(dll.read_bytes())
        dll.unlink()
        dll.symlink_to(other)
        with self.assertRaises(ValueError):
            pkg.build_package(self.mod, self.zip)

    def test_packaging_is_reproducible(self):
        pkg.build_package(self.mod, self.zip)
        first = self.zip.read_bytes()
        pkg.build_package(self.mod, self.zip)
        self.assertEqual(first, self.zip.read_bytes())

    def test_internal_symlinked_payload_directory_is_rejected(self):
        directory = self.mod / "1.7/Assemblies"
        real_directory = self.mod / "1.7/ActualAssemblies"
        directory.rename(real_directory)
        directory.symlink_to("ActualAssemblies", target_is_directory=True)
        with self.assertRaises(ValueError):
            pkg.build_package(self.mod, self.zip)


if __name__ == "__main__":
    unittest.main()
