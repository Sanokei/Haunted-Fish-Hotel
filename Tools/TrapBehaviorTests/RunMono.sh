#!/usr/bin/env bash
set -euo pipefail
# Pass the Editor/Data directory from an existing Unity installation. This does
# not start Unity, restore packages, run physics or perform Mirage weaving.
data="${1:?Usage: RunMono.sh /path/to/Editor/Data}"
root="$(cd "$(dirname "$0")/../.." && pwd)"
output="$(mktemp -d)"
trap 'rm -rf "$output"' EXIT
mono="$data/MonoBleedingEdge/bin/mono"
"$mono" "$data/MonoBleedingEdge/lib/mono/4.5/csc.exe" -nologo -langversion:latest -out:"$output/TrapBehaviorTests.exe" \
  "$root/Tools/TrapBehaviorTests/Program.cs" \
  "$root/Assets/Scripts/Multiplayer/TrapImpactRules.cs" \
  "$root/Assets/Scripts/Multiplayer/TrapPassageMotion.cs"
"$mono" "$output/TrapBehaviorTests.exe"
