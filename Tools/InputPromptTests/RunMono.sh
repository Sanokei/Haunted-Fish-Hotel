#!/usr/bin/env bash
set -euo pipefail
# Production C# with source-boundary doubles only; no Unity/native/device claim.
data="${1:?Usage: RunMono.sh /path/to/Unity/Editor/Data}"
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="$(mktemp -d)"; trap 'rm -rf "$out"' EXIT
mono="$data/MonoBleedingEdge/bin/mono"
"$mono" "$data/MonoBleedingEdge/lib/mono/4.5/csc.exe" -nologo -langversion:latest -out:"$out/InputPromptTests.exe" \
 "$root/Tools/InputPromptTests/UnityDoubles.cs" "$root/Tools/InputPromptTests/Program.cs" \
 "$root"/Assets/Scripts/InputPrompts/*.cs
"$mono" "$out/InputPromptTests.exe"
