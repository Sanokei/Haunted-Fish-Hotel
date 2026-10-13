#!/usr/bin/env bash
set -euo pipefail
# Source/adapter tests only. This never launches Unity or a transport.
data="${1:?Usage: RunMono.sh /path/to/Editor/Data}"
root="$(cd "$(dirname "$0")/../.." && pwd)"
output="$(mktemp -d)"
trap 'rm -rf "$output"' EXIT
mono="$data/MonoBleedingEdge/bin/mono"
"$mono" "$data/MonoBleedingEdge/lib/mono/4.5/csc.exe" -nologo -langversion:latest -out:"$output/ReadyUpTests.exe" \
  "$root/Tools/ReadyUpTests/PageAdapterTests.cs" \
  "$root/Tools/RoundIntroductionTests/ReadyUpChecks.cs" \
  "$root/Assets/Scripts/Multiplayer/GameReadyGate.cs" \
  "$root/Assets/Scripts/Multiplayer/GameRoundGate.cs" \
  "$root/Assets/Scripts/Multiplayer/RoundHowToGate.cs" \
  "$root/Assets/Scripts/Multiplayer/RoundHowToState.cs" \
  "$root/Assets/Scripts/Multiplayer/RoundHowToPage.cs" \
  "$root/Assets/Scripts/Multiplayer/GameplayPresentationPolicy.cs"
"$mono" "$output/ReadyUpTests.exe"
"$mono" "$data/MonoBleedingEdge/lib/mono/4.5/csc.exe" -nologo -langversion:latest -out:"$output/SafeAreaTests.exe" \
  "$root/Tools/ReadyUpTests/SafeAreaTests.cs" \
  "$root/Assets/Scripts/Multiplayer/RoundHowToSafeArea.cs"
"$mono" "$output/SafeAreaTests.exe"
