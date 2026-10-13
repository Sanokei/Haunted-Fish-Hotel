#!/usr/bin/env bash
set -euo pipefail
data="${1:?Usage: RunMono.sh /path/to/Editor/Data}"
root="$(cd "$(dirname "$0")/../.." && pwd)"
output="$(mktemp -d)"
trap 'rm -rf "$output"' EXIT
mono="$data/MonoBleedingEdge/bin/mono"
"$mono" "$data/MonoBleedingEdge/lib/mono/4.5/csc.exe" -nologo -langversion:latest -out:"$output/RunningJumpTests.exe" \
  "$root/Tools/RunningJumpTests/Program.cs" "$root/Tools/RunningJumpTests/UnityDoubles.cs" \
  "$root/Assets/Scripts/Multiplayer/RunningJumpState.cs" "$root/Assets/Scripts/Multiplayer/GameSideScrollMotor.cs"
"$mono" "$output/RunningJumpTests.exe"
