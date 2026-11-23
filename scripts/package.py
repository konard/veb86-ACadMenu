#!/usr/bin/env python3
"""Build the manual install archive BobrovMenu-manual-install-v<version>.zip.

Layout of the archive (the same as for acadPaletteSetXDATA, see installer/manual/INSTALL.md):

    AppData/Roaming/Autodesk/ApplicationPlugins/BobrovMenu.bundle/PackageContents.xml
    AppData/Roaming/Autodesk/ApplicationPlugins/BobrovMenu.bundle/Contents/BobrovMenu.cuix
    AppData/Roaming/Autodesk/ApplicationPlugins/BobrovMenu.bundle/Contents/<AutoCAD range>/ACadSmartLeaderMenu.dll
    install.cmd, uninstall.cmd   (CRLF line endings for cmd.exe)
    INSTALL.md
    release-manifest.txt         (version, commit, SHA-256 of every other file)

Usage:

    dotnet build src/ACadSmartLeaderMenu -c Release
    python3 scripts/package.py --version v0.1.7 --commit <sha> [--output-dir artifacts/release]

The committed BobrovMenu.cuix is packed as is; run `python3 scripts/build_cuix.py --check`
first to be sure it matches src/BobrovMenu.cuix.  Only AppVersion of PackageContents.xml is
replaced by the numeric part of --version.  Only the Python standard library is used.
"""

import argparse
import hashlib
import re
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BUNDLE = ROOT / "installer" / "BobrovMenu.bundle"
MANUAL = ROOT / "installer" / "manual"
MENU_BUILD = ROOT / "src" / "ACadSmartLeaderMenu" / "bin" / "Release"
MENU_ASSEMBLY = "ACadSmartLeaderMenu.dll"
MENU_TARGETS = {
    "AutoCAD2020": "net47",
    "AutoCAD2021-2024": "net48",
    "AutoCAD2025": "net8.0-windows",
}
BUNDLE_PREFIX = "AppData/Roaming/Autodesk/ApplicationPlugins/BobrovMenu.bundle/"
ZIP_DATE = (1980, 1, 1, 0, 0, 0)
VERSION_RE = re.compile(r"^v?(\d+(?:\.\d+){1,3})$")


def app_version(version):
    match = VERSION_RE.match(version)
    if not match:
        raise SystemExit("--version must look like v0.0.7 or 1.2.3, got %r" % version)
    return match.group(1)


def package_contents(version):
    text = (BUNDLE / "PackageContents.xml").read_text(encoding="utf-8")
    text, count = re.subn(r'AppVersion="[^"]*"', 'AppVersion="%s"' % app_version(version), text, count=1)
    if count != 1:
        raise SystemExit("AppVersion is missing in PackageContents.xml")
    return text.encode("utf-8")


def crlf(path):
    """cmd.exe needs CRLF; the working copy may have LF (Linux checkout)."""
    text = path.read_bytes().replace(b"\r\n", b"\n")
    return text.replace(b"\n", b"\r\n")


def bundle_entries(version, menu_build):
    entries = [(BUNDLE_PREFIX + "PackageContents.xml", package_contents(version))]
    for path in sorted((BUNDLE / "Contents").rglob("*")):
        if path.is_file():
            entries.append((BUNDLE_PREFIX + path.relative_to(BUNDLE).as_posix(), path.read_bytes()))
    for directory, framework in MENU_TARGETS.items():
        assembly = menu_build / framework / MENU_ASSEMBLY
        if not assembly.is_file():
            raise SystemExit(f"Missing {assembly}. Run dotnet build src/ACadSmartLeaderMenu -c Release.")
        entries.append((f"{BUNDLE_PREFIX}Contents/{directory}/{MENU_ASSEMBLY}", assembly.read_bytes()))
    return entries


def archive_entries(version, commit, menu_build):
    entries = bundle_entries(version, menu_build)
    entries.append(("install.cmd", crlf(MANUAL / "install.cmd")))
    entries.append(("uninstall.cmd", crlf(MANUAL / "uninstall.cmd")))
    entries.append(("INSTALL.md", (MANUAL / "INSTALL.md").read_bytes()))
    lines = [
        "BobrovMenu %s" % version,
        "Commit: %s" % commit,
        "Bobrov XDATA menu: AutoCAD 2020-2024 x64 (R23.1-R24.3); requires acadPaletteSetXDATA",
        "SmartLeader menu: AutoCAD 2020, 2021-2024, 2025+ x64; requires ACadSmartLeader 0.1.4+",
        "",
        "SHA-256:",
    ]
    lines += ["%s  %s" % (hashlib.sha256(data).hexdigest(), name) for name, data in entries]
    entries.append(("release-manifest.txt", ("\r\n".join(lines) + "\r\n").encode("utf-8")))
    return entries


def write_zip(path, entries):
    path.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(path, "w", zipfile.ZIP_DEFLATED) as archive:
        for name, data in entries:
            info = zipfile.ZipInfo(name, ZIP_DATE)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            info.create_system = 0
            # Bit 11: names are UTF-8 (they are ASCII anyway, kept for Windows Explorer).
            info.flag_bits |= 0x800
            archive.writestr(info, data, compresslevel=9)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--version", default="v0.1.0", help="release version, for example v0.1.7")
    parser.add_argument("--commit", default="unknown", help="commit SHA written to release-manifest.txt")
    parser.add_argument("--output-dir", type=Path, default=ROOT / "artifacts" / "release")
    parser.add_argument("--menu-build-dir", type=Path, default=MENU_BUILD, help="SmartLeader menu build directory")
    args = parser.parse_args(argv)

    version = args.version if args.version.startswith("v") else "v" + args.version
    output = args.output_dir / ("BobrovMenu-manual-install-%s.zip" % version)
    write_zip(output, archive_entries(version, args.commit, args.menu_build_dir))
    print("written %s" % output)
    return 0


if __name__ == "__main__":
    sys.exit(main())
