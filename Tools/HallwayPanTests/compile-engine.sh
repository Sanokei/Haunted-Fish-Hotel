#!/usr/bin/env bash
set -euo pipefail
repo=$(cd "$(dirname "$0")/../.." && pwd)
: "${UNITY_EDITOR_DATA:?Set UNITY_EDITOR_DATA to the installed Unity Editor/Data directory}"
mono="$UNITY_EDITOR_DATA/MonoBleedingEdge"
ui=$(find "$UNITY_EDITOR_DATA/Resources/PackageManager/ProjectTemplates/libcache" -path '*/ScriptAssemblies/UnityEngine.UI.dll' -print -quit)
[[ -f "$ui" ]] || { echo 'Installed Unity UI assembly was not found.' >&2; exit 1; }
out=$(mktemp -d); trap 'rm -rf "$out"' EXIT
refs=(); for file in "$UNITY_EDITOR_DATA"/Managed/UnityEngine/*.dll "$UNITY_EDITOR_DATA"/Managed/UnityEditor.*.dll; do refs+=("-r:$file"); done
files=(); for name in GhostSelectionPresentation GhostSelectionWheel AtticReturnBlur CinematicStreaks AtticTransitionAssets RoundIntroductionSettings HallwayFishArtwork HallwayFishStaging GameplayPresentationPolicy HotelPalette GameRoundGate; do files+=("$repo/Assets/Scripts/Multiplayer/$name.cs"); done
for name in StoryUI StoryAnimationMath StoryParallaxRig StoryParallaxPlayer StoryFunctions.Sequences; do files+=("$repo/Assets/Scripts/DialogueSystem/StoryInput/$name.cs"); done
while IFS= read -r file; do files+=("$file"); done < <(find "$repo/Assets/Plugins/Ink/InkLibs/InkRuntime" -name '*.cs')
"$mono/bin/mono" "$mono/lib/mono/4.5/csc.exe" -nologo -define:UNITY_EDITOR -target:library -out:"$out/Hallway.RealEngine.dll" \
  -r:"$mono/lib/mono/4.5/Facades/netstandard.dll" -r:"$ui" -r:"$(dirname "$ui")/Unity.TextMeshPro.dll" "${refs[@]}" "${files[@]}" \
  "$repo/Assets/Scripts/Multiplayer/Lobby/IntroductionPanelShape.cs" \
  "$repo/Assets/Scripts/Multiplayer/Lobby/IntroductionSkipCircle.cs" \
  "$repo/Tools/HallwayPanTests/EngineCompileDependencies.cs" \
  "$repo/Assets/Editor/HallwayIntroPanValidation.cs"
echo 'PASS production cinematic adapter compile against real Unity 6000 engine/UI assemblies (network service doubles; not native execution).'
