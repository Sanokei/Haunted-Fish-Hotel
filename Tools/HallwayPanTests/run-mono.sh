#!/usr/bin/env bash
set -euo pipefail
repo=$(cd "$(dirname "$0")/../.." && pwd)
: "${MONO_ROOT:?Set MONO_ROOT to the installed Unity Editor/Data/MonoBleedingEdge directory}"
out=$(mktemp -d)
trap 'rm -rf "$out"' EXIT
mapfile -t files < <(python3 - "$repo" <<'PY'
import glob,pathlib,sys,xml.etree.ElementTree as ET
root=pathlib.Path(sys.argv[1]); project=root/'Tools/IntroductionTests/IntroductionTests.csproj'
for item in ET.parse(project).iter('Compile'):
 if item.attrib['Include']=='Program.cs': continue
 for f in glob.glob(str(project.parent/item.attrib['Include']),recursive=True): print(f)
print(root/'Assets/Scripts/Multiplayer/AtticReturnBlur.cs')
print(root/'Tools/HallwayPanTests/PlaybackTests.cs')
PY
)
"$MONO_ROOT/bin/mono" "$MONO_ROOT/lib/mono/4.5/csc.exe" -nologo -langversion:preview -main:HallwayPanTests -out:"$out/HallwayPanTests.exe" "${files[@]}"
HAUNTED_FISH_ROOT="$repo" "$MONO_ROOT/bin/mono" "$out/HallwayPanTests.exe"
