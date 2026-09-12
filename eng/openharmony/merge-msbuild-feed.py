#!/usr/bin/env python3
"""Create a new SDK feed using the six source-built MSBuild dependency packages."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("sdk_feed", type=Path)
parser.add_argument("msbuild_packages", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
required = {"microsoft.build", "microsoft.build.framework", "microsoft.build.runtime",
            "microsoft.build.tasks.core", "microsoft.build.utilities.core", "microsoft.net.stringtools"}
version = "18.9.4"


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


replacements = {}
for archive in args.msbuild_packages.rglob("*.nupkg"):
    if archive.name.endswith(".symbols.nupkg"):
        continue
    with zipfile.ZipFile(archive) as package:
        specs = [n for n in package.namelist() if "/" not in n and n.endswith(".nuspec")]
        if len(specs) != 1:
            raise ValueError("Expected one package manifest: " + str(archive))
        root = ET.fromstring(package.read(specs[0]))
    metadata = next(n for n in root if n.tag.rsplit("}", 1)[-1] == "metadata")
    fields = {n.tag.rsplit("}", 1)[-1]: n.text for n in metadata}
    identifier = fields["id"].lower()
    if identifier in required and fields["version"] == version:
        if identifier in replacements:
            raise ValueError("Duplicate source-built package: " + identifier)
        replacements[identifier] = archive
if set(replacements) != required:
    raise SystemExit("Missing source-built packages: " + ", ".join(sorted(required - replacements.keys())))

records = json.loads((args.sdk_feed / "manifest.json").read_text())
source_root = args.sdk_feed.resolve(strict=True)
copies = []
replaced = set()
for record in records:
    source = (source_root / record["path"]).resolve(strict=True)
    if not source.is_relative_to(source_root) or source.stat().st_size != record["size"] or digest(source) != record["sha256"]:
        raise ValueError("SDK input integrity check failed: " + record["path"])
    if record["id"] in required and record["version"] == version:
        source = replacements[record["id"]]
        record.update(origin="msbuild-source-build", size=source.stat().st_size, sha256=digest(source))
        replaced.add(record["id"])
    copies.append((source, record["path"]))
if replaced != required:
    raise SystemExit("Input SDK feed does not contain all expected MSBuild package identities")
args.output.mkdir(parents=True, exist_ok=False)
for source, relative in copies:
    destination = args.output / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, destination)
configuration = ET.Element("configuration")
sources = ET.SubElement(configuration, "packageSources")
ET.SubElement(sources, "clear")
ET.SubElement(sources, "add", key="fixed-local", value=str((args.output / "packages").resolve()))
ET.indent(configuration)
ET.ElementTree(configuration).write(args.output / "NuGet.Config", encoding="utf-8", xml_declaration=True)
(args.output / "manifest.json").write_text(json.dumps(records, indent=2) + "\n")
print(f"Prepared {len(records)} fixed SDK inputs with {len(replaced)} source-built MSBuild packages")
