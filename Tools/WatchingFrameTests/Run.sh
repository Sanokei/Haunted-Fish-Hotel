#!/usr/bin/env bash
set -euo pipefail
data="${1:?Usage: Run.sh /path/to/Unity/Editor/Data}"
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="$(mktemp -d)"; trap 'rm -rf "$out"' EXIT
mono="$data/MonoBleedingEdge/bin/mono"
csc="$data/MonoBleedingEdge/lib/mono/4.5/csc.exe"
"$mono" "$csc" -nologo -out:"$out/GazeTests.exe" "$root/Assets/Scripts/Presentation/WatchingFrames/WatchingFrameGaze.cs" "$root/Tools/WatchingFrameTests/Program.cs"
"$mono" "$out/GazeTests.exe"
refs=("-r:$data/MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll")
while IFS= read -r dll; do refs+=("-r:$dll"); done < <(find "$data/Managed/UnityEngine" -maxdepth 1 -name 'UnityEngine*.dll' -print)
"$mono" "$csc" -nologo -target:library -out:"$out/WatchingFrames.dll" "${refs[@]}" "$root"/Assets/Scripts/Presentation/WatchingFrames/*.cs
printf '%s\n' 'PASS controller compiles against actual Unity APIs without gameplay doubles. No native execution, scene import, or shader compilation claimed.'
ui="$(find "$data/Resources/PackageManager/ProjectTemplates/libcache" -name UnityEngine.UI.dll -print -quit)"
input="$(find "$data/Resources/PackageManager/ProjectTemplates/libcache" -name Unity.InputSystem.dll -print -quit)"
cat > "$out/PlayerBoundary.cs" <<'CS'
namespace HauntedFish.Multiplayer {
    public enum HotelControlMode { Fish, Ghost }
    public sealed class HotelPlayer : UnityEngine.MonoBehaviour {
        public static HotelPlayer LocalPlayer;
        public HotelControlMode ControlMode;
        public bool InBossFight, BossWatching;
    }
}
CS
"$mono" "$csc" -nologo -target:library -out:"$out/SpotlightBinding.dll" "${refs[@]}" -r:"$ui" -r:"$input" \
    "$root"/Assets/Scripts/Presentation/WatchingFrames/*.cs "$root/Assets/Scripts/Multiplayer/GameMouseSpotlight.cs" "$out/PlayerBoundary.cs"
printf '%s\n' 'PASS actual GameMouseSpotlight and gallery compile together against real Unity/UI/Input APIs; only unrelated HotelPlayer network boundary is doubled.'
