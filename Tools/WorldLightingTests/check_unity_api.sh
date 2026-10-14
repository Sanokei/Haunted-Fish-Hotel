#!/usr/bin/env bash
set -euo pipefail
data="${1:?Usage: check_unity_api.sh /path/to/Unity/Editor/Data}"
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="$(mktemp -d)"; trap 'rm -rf "$out"' EXIT
refs=("-r:$data/MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll")
for dll in "$data"/Managed/UnityEngine/*.dll "$data"/Managed/UnityEditor.*.dll; do refs+=("-r:$dll"); done
"$data/MonoBleedingEdge/bin/mono" "$data/MonoBleedingEdge/lib/mono/4.5/csc.exe" -nologo -define:UNITY_EDITOR -target:library \
  -out:"$out/WorldLighting.UnityApi.dll" "${refs[@]}" "$root/Assets/Editor/WorldArtworkLightingValidation.cs"
printf '%s\n' 'PASS validator compiles against real Unity 6000.3.6f1 APIs, no doubles. Shader import/native runtime validation is NOT established.'
