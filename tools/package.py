"""Package only self-authored runtime files. No game/third-party DLL discovery."""
import argparse
import re
import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

RUNTIME_FILES = (
    "About/About.xml", "LoadFolders.xml", "Defs/MainButtonDefs/RaidCheck.xml",
    "Languages/English/Keyed/RaidCheck.xml", "Languages/Japanese/Keyed/RaidCheck.xml",
    "Languages/Japanese/DefInjected/MainButtonDef/RaidCheck.xml",
    "1.7/Assemblies/RaidCheck.dll", "README.txt",
)


def validate_mod(mod_root: Path) -> None:
    for relative in RUNTIME_FILES:
        path = mod_root / relative
        if not path.is_file() or path.is_symlink() or not path.resolve().is_relative_to(mod_root.resolve()):
            raise ValueError(f"Missing or unsafe runtime file: {relative}")
        if any((mod_root / parent).is_symlink() for parent in path.relative_to(mod_root).parents if parent != Path('.')):
            raise ValueError(f"Symlinked runtime directory: {relative}")
    try:
        for relative in RUNTIME_FILES:
            if relative.endswith(".xml"):
                ET.parse(mod_root / relative)
        about = ET.parse(mod_root / "About/About.xml").getroot()
        if about.findtext("packageId") != "pengin0503.raidcheck":
            raise ValueError("Wrong mod identity")
        if "1.7" not in [x.text for x in about.findall("supportedVersions/li")]:
            raise ValueError("RimWorld 1.7 support missing")
        button = ET.parse(mod_root / "Defs/MainButtonDefs/RaidCheck.xml").find("MainButtonDef")
        if button is None or button.findtext("tabWindowClass") != "RaidCheck.MainTabWindow_RaidCheck":
            raise ValueError("Wrong main tab type")
        languages = []
        for language in ("English", "Japanese"):
            root = ET.parse(mod_root / f"Languages/{language}/Keyed/RaidCheck.xml").getroot()
            if root.tag != "LanguageData" or len({x.tag for x in root}) != len(root):
                raise ValueError("Invalid or duplicate language keys")
            languages.append({x.tag: x.text or "" for x in root})
        en, ja = languages
        if en.keys() != ja.keys():
            raise ValueError("English/Japanese language keys differ")
        for key in en:
            placeholders = lambda s: sorted(re.findall(r"\{(\d+)(?::[^{}]*)?\}", s))
            if placeholders(en[key]) != placeholders(ja[key]):
                raise ValueError(f"Language placeholders differ: {key}")
    except ET.ParseError as ex:
        raise ValueError("Invalid runtime XML") from ex
    dll = (mod_root / "1.7/Assemblies/RaidCheck.dll").read_bytes()
    if len(dll) < 128 or dll[:2] != b"MZ":
        raise ValueError("RaidCheck.dll is missing or not a PE assembly")


def build_package(mod_root: Path, output: Path) -> Path:
    validate_mod(mod_root)
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for relative in RUNTIME_FILES:
            info = zipfile.ZipInfo("RaidCheck/" + relative, (2026, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            archive.writestr(info, (mod_root / relative).read_bytes())
    return output


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mod", type=Path, default=Path(__file__).resolve().parents[1] / "Mod")
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parents[1] / "dist/RaidCheck-0.1.0-RW1.7.zip")
    args = parser.parse_args()
    print(build_package(args.mod, args.output))


if __name__ == "__main__":
    main()
