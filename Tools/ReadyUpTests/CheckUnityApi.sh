#!/usr/bin/env bash
set -euo pipefail
# Compile against the installed real Unity/UI/InputSystem APIs, without launching Unity.
data="${1:?Usage: CheckUnityApi.sh /path/to/Editor/Data}"
root="$(cd "$(dirname "$0")/../.." && pwd)"
output="$(mktemp -d)"
trap 'rm -rf "$output"' EXIT
reference="$(find "$data/Resources/PackageManager/ProjectTemplates/libcache" -name Unity.InputSystem.dll -print -quit)"
reference="$(dirname "$reference")"
test -f "$reference/UnityEngine.UI.dll"
cat > "$output/Player.cs" <<'CS'
namespace HauntedFish.Multiplayer { public sealed class HotelPlayer : UnityEngine.MonoBehaviour { public static HotelPlayer LocalPlayer; public bool RoundCinematicComplete, RoundReady, InputBlocked; public int RoundReadyCount, RoundParticipantCount; } }
CS
editor_references=()
while IFS= read -r assembly; do editor_references+=("-r:$assembly"); done < <(find "$data/Managed" -maxdepth 1 -name 'UnityEditor*.dll' -print)
"$data/MonoBleedingEdge/bin/mono" "$data/MonoBleedingEdge/lib/mono/4.5/csc.exe" -nologo -target:library -langversion:latest -define:UNITY_EDITOR "${editor_references[@]}" -out:"$output/ReadyGuide.UnityApiCheck.dll" \
  -r:"$data/Managed/UnityEngine/UnityEngine.dll" \
  -r:"$data/Managed/UnityEngine/UnityEngine.VideoModule.dll" \
  -r:"$data/Managed/UnityEngine/UnityEngine.CoreModule.dll" \
  -r:"$data/Managed/UnityEngine/UnityEngine.TextRenderingModule.dll" \
  -r:"$data/Managed/UnityEngine/UnityEngine.UIModule.dll" \
  -r:"$data/MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll" \
  -r:"$reference/UnityEngine.UI.dll" -r:"$reference/Unity.InputSystem.dll" \
  "$output/Player.cs" \
  "$root/Assets/Scripts/Multiplayer/RoundHowToPage.cs" \
  "$root/Assets/Scripts/Multiplayer/RoundHowToGate.cs" \
  "$root/Assets/Scripts/Multiplayer/RoundHowToState.cs" \
  "$root/Assets/Scripts/Multiplayer/RoundHowToSafeArea.cs" \
  "$root/Assets/Scripts/Multiplayer/GameplayPresentationPolicy.cs" \
  "$root/Assets/Editor/GameTutorialPlaybackBatch.cs"
printf '%s\n' 'PASS compile against real Unity/UI/InputSystem APIs with a HotelPlayer boundary double; no Editor/import/weaving/render/network pass claimed.'
