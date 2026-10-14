#!/usr/bin/env bash
set -euo pipefail
# Standalone source/Ink parity checks; never launches Unity or touches footage.
: "${MONO_ROOT:?Set MONO_ROOT to the installed Unity Editor/Data/MonoBleedingEdge directory}"
root="$(cd "$(dirname "$0")/../.." && pwd)"
output="$(mktemp -d)"
trap 'rm -rf "$output"' EXIT
mapfile -t ink < <(find "$root/Assets/Plugins/Ink/InkLibs/InkCompiler" "$root/Assets/Plugins/Ink/InkLibs/InkRuntime" -name '*.cs' -type f)
"$MONO_ROOT/bin/mono" "$MONO_ROOT/lib/mono/4.5/csc.exe" -nologo -out:"$output/CompileGuideInk.exe" "$root/Tools/TheatreVideos/CompileGuideInk.cs" "${ink[@]}"
cd "$root"
"$MONO_ROOT/bin/mono" "$output/CompileGuideInk.exe" "$@"
