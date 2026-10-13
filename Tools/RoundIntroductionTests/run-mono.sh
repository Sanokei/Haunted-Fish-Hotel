#!/usr/bin/env bash
set -euo pipefail
repo=$(cd "$(dirname "$0")/../.." && pwd)
: "${MONO_ROOT:?Set MONO_ROOT to the installed Unity Editor/Data/MonoBleedingEdge directory}"
out=$(mktemp -d)
trap 'rm -rf "$out"' EXIT
mapfile -t files < <(python3 - "$repo" <<'PY'
import glob, pathlib, sys, xml.etree.ElementTree as ET
project=pathlib.Path(sys.argv[1])/'Tools/RoundIntroductionTests/RoundIntroductionTests.csproj'
for item in ET.parse(project).iter('Compile'):
 for f in glob.glob(str(project.parent/item.attrib['Include']), recursive=True): print(f)
PY
)
"$MONO_ROOT/bin/mono" "$MONO_ROOT/lib/mono/4.5/csc.exe" -nologo -out:"$out/RoundTests.exe" "${files[@]}"
HAUNTED_FISH_ROOT="$repo" "$MONO_ROOT/bin/mono" "$out/RoundTests.exe" "$@"
