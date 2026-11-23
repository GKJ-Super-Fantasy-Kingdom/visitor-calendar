"""Build and read back the Thunderstore local-import archive from Release artifacts."""
import hashlib
import json
from pathlib import Path
import struct
from tempfile import TemporaryDirectory
from xml.etree import ElementTree
from zipfile import ZIP_DEFLATED, ZipFile

root = Path(__file__).resolve().parent
listing = root / "thunderstore"
manifest = json.loads((listing / "manifest.json").read_text(encoding="utf-8"))
project = ElementTree.parse(root / "VisitorCalendar.csproj")
version = project.findtext(".//Version")
framework = project.findtext(".//TargetFramework")
if manifest["name"] != "VisitorCalendar" or manifest["version_number"] != version:
    raise ValueError("Project and listing must identify the same release")
dll = root / "bin" / "Release" / framework / "VisitorCalendar.dll"
files = {
    "LICENSE": root / "LICENSE",
    "manifest.json": listing / "manifest.json",
    "README.md": root / "README.md",
    "CHANGELOG.md": root / "CHANGELOG.md",
    "icon.png": listing / "icon.png",
    "plugins/VisitorCalendar.dll": dll,
}
out = root / "dist" / f"VisitorCalendar-{version}.zip"
out.parent.mkdir(exist_ok=True)
with TemporaryDirectory(dir=out.parent) as scratch:
    staged = Path(scratch) / out.name
    with ZipFile(staged, "w", ZIP_DEFLATED) as archive:
        for name, path in files.items():
            archive.write(path, name)
    with ZipFile(staged) as archive:
        if archive.testzip() or archive.namelist() != list(files):
            raise ValueError("Unexpected ZIP contents or failed CRC")
        for name, path in files.items():
            if archive.read(name) != path.read_bytes():
                raise ValueError(f"Archive mismatch for {name}")
        icon = archive.read("icon.png")
        if icon[:8] != b"\x89PNG\r\n\x1a\n" or struct.unpack(">II", icon[16:24]) != (256, 256):
            raise ValueError("Expected a 256 × 256 PNG")
    staged.replace(out)
print(f"{out}\nSHA-256 {hashlib.sha256(out.read_bytes()).hexdigest()}")
