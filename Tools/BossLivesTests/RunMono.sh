#!/usr/bin/env bash
set -euo pipefail
# Compiles production sources with tiny presentation doubles. Does not launch Unity.
data="${1:?Usage: RunMono.sh /path/to/Editor/Data}"
root="$(cd "$(dirname "$0")/../.." && pwd)"
output="$(mktemp -d)"; trap 'rm -rf "$output"' EXIT
mono="$data/MonoBleedingEdge/bin/mono"
"$mono" "$data/MonoBleedingEdge/lib/mono/4.5/csc.exe" -nologo -langversion:latest -out:"$output/BossLivesTests.exe" \
 "$root/Tools/BossLivesTests/Program.cs" "$root/Tools/BossLivesTests/UnityDoubles.cs" \
 "$root/Assets/Scripts/BossFight/BossMatch.cs" "$root/Assets/Scripts/BossFight/BossGhostLifeStage.cs" \
 "$root/Assets/Scripts/BossFight/BossPerspectiveFraming.cs" "$root/Assets/Scripts/BossFight/BossLivesState.cs" \
 "$root/Assets/Scripts/BossFight/BossHudPresentation.cs" "$root/Assets/Scripts/BossFight/BossHudSafeArea.cs"
"$mono" "$output/BossLivesTests.exe"
python3 "$root/Tools/BossLivesTests/VerifyAuthoredHud.py"
