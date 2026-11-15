#!/usr/bin/env python3
"""Pack src/BobrovMenu.cuix/ (readable XML parts and icons) into BobrovMenu.cuix.

A CUIX file is an Open Packaging Conventions (ZIP) package.  The parts are kept in
the repository unpacked so that every change is reviewable as a text diff; the
packed file installer/BobrovMenu.bundle/Contents/BobrovMenu.cuix is committed as is
(spec, section 9) and must always match the sources:

    python3 scripts/build_cuix.py            # rebuild the committed BobrovMenu.cuix
    python3 scripts/build_cuix.py --check    # fail if BobrovMenu.cuix differs from the sources
    python3 scripts/build_cuix.py --output other.cuix
    python3 scripts/build_cuix.py --unpack edited.cuix   # replace the sources by a CUIX saved in AutoCAD

Entry order, timestamps and permissions are fixed, so the build is reproducible.
--check compares the unpacked entries (names, order, bytes), not the compressed
bytes, so it does not depend on the zlib version of the machine.
Only the Python standard library is used.
"""

import argparse
import sys
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "src" / "BobrovMenu.cuix"
OUTPUT = ROOT / "installer" / "BobrovMenu.bundle" / "Contents" / "BobrovMenu.cuix"

# Fixed timestamp of every ZIP entry (the ZIP format starts at 1980).
ZIP_DATE = (1980, 1, 1, 0, 0, 0)

# OPC metadata goes first, then the parts in the order of _rels/.rels.
FIRST = ["[Content_Types].xml", "_rels/.rels"]
LAST = ["Menu_Package_Info.xml"]


def source_entries(source=SOURCE):
    """Return [(entry name, bytes)] in package order."""
    import xml.etree.ElementTree as ET

    rels = ET.parse(source / "_rels" / ".rels").getroot()
    ordered = list(FIRST)
    for rel in rels:
        target = rel.get("Target", "").lstrip("/")
        if target and target not in ordered:
            ordered.append(target)
    ordered.extend(LAST)

    on_disk = sorted(
        p.relative_to(source).as_posix() for p in source.rglob("*") if p.is_file()
    )
    unlisted = sorted(set(on_disk) - set(ordered))
    if unlisted:
        raise SystemExit(
            "Files in %s are not referenced by _rels/.rels: %s"
            % (source, ", ".join(unlisted))
        )
    missing = [name for name in ordered if not (source / name).is_file()]
    if missing:
        raise SystemExit("Parts referenced by _rels/.rels are missing: " + ", ".join(missing))
    return [(name, (source / name).read_bytes()) for name in ordered]


def build(output, entries):
    output.parent.mkdir(parents=True, exist_ok=True)
    tmp = output.with_name(output.name + ".tmp")
    with zipfile.ZipFile(tmp, "w", zipfile.ZIP_DEFLATED) as package:
        for name, data in entries:
            info = zipfile.ZipInfo(name, ZIP_DATE)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            info.create_system = 0  # MS-DOS, like packages written by System.IO.Packaging
            package.writestr(info, data, compresslevel=9)
    tmp.replace(output)


def read_entries(path):
    with zipfile.ZipFile(path) as package:
        return [(info.filename, package.read(info)) for info in package.infolist()]


def unpack(path, source=SOURCE):
    """Replace the source parts by the entries of a CUIX file (for example one saved by the CUI editor)."""
    entries = read_entries(path)
    names = [name for name, _ in entries]
    if "_rels/.rels" not in names or "[Content_Types].xml" not in names:
        raise SystemExit("%s is not a CUIX package (no _rels/.rels or [Content_Types].xml)" % path)
    for name in names:
        target = (source / name).resolve()
        if source.resolve() not in target.parents:
            raise SystemExit("unsafe entry name in %s: %s" % (path, name))
    for old in sorted(source.rglob("*"), reverse=True):
        if old.is_file():
            old.unlink()
        elif old.is_dir():
            old.rmdir()
    for name, data in entries:
        target = source / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    return len(entries)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true", help="compare the existing CUIX with the sources")
    parser.add_argument("--output", type=Path, default=OUTPUT, help="CUIX file to write or check")
    parser.add_argument("--unpack", type=Path, metavar="CUIX", help="replace src/BobrovMenu.cuix by this CUIX file, then rebuild")
    args = parser.parse_args(argv)

    if args.unpack:
        count = unpack(args.unpack.resolve())
        print("unpacked %s into %s (%d entries)" % (args.unpack, SOURCE.relative_to(ROOT), count))

    entries = source_entries()
    output = args.output.resolve()
    if args.check:
        if not output.exists():
            print("%s does not exist. Run: python3 scripts/build_cuix.py" % output, file=sys.stderr)
            return 1
        actual = read_entries(output)
        if actual != entries:
            expected_names = [name for name, _ in entries]
            actual_names = [name for name, _ in actual]
            if expected_names != actual_names:
                print("Entry list differs.\n  expected: %s\n  actual:   %s" % (expected_names, actual_names), file=sys.stderr)
            else:
                changed = [name for (name, a), (_, b) in zip(entries, actual) if a != b]
                print("Entries differ from the sources: " + ", ".join(changed), file=sys.stderr)
            print("Run: python3 scripts/build_cuix.py", file=sys.stderr)
            return 1
        print("%s matches src/BobrovMenu.cuix (%d entries)" % (output.name, len(entries)))
        return 0

    build(output, entries)
    print("written %s (%d entries)" % (output, len(entries)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
