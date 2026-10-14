#!/usr/bin/env bash
set -euo pipefail
# Type-check against installed real assemblies. Never launches Unity or an input runtime.
data="${1:?Usage: CheckUnityApi.sh /path/to/Unity/Editor/Data [Package ScriptAssemblies directory]}"
root="$(cd "$(dirname "$0")/../.." && pwd)"
out="$(mktemp -d)"; trap 'rm -rf "$out"' EXIT
reference="${2:-}"
if [[ -z "$reference" ]]; then
 reference="$(find "$data/Resources/PackageManager/ProjectTemplates/libcache" -name Unity.InputSystem.dll -print -quit)"
 reference="$(dirname "$reference")"
fi
for dll in Unity.InputSystem.dll Unity.TextMeshPro.dll UnityEngine.UI.dll; do test -f "$reference/$dll"; done
refs=("-r:$data/MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll")
while IFS= read -r dll; do refs+=("-r:$dll"); done < <(find "$data/Managed/UnityEngine" -maxdepth 1 -name 'UnityEngine*.dll' -print)
while IFS= read -r dll; do refs+=("-r:$dll"); done < <(find "$data/Managed" -maxdepth 1 -name 'UnityEditor*.dll' -print)
for dll in Unity.InputSystem.dll Unity.TextMeshPro.dll UnityEngine.UI.dll; do refs+=("-r:$reference/$dll"); done
"$data/MonoBleedingEdge/bin/mono" "$data/MonoBleedingEdge/lib/mono/4.5/csc.exe" -nologo -target:library -langversion:latest -define:UNITY_EDITOR \
 -out:"$out/InputPrompts.UnityApiCheck.dll" "${refs[@]}" \
 "$root"/Assets/Scripts/InputPrompts/*.cs "$root/Assets/Editor/InputPromptNativeValidation.cs" \
 "$root/Assets/Scripts/Multiplayer/MemberMenuTutorial.cs" \
 "$root/Assets/Scripts/Multiplayer/HotelAudioRouting.cs" \
 "$root/Assets/Scripts/Tutorials/TutorialRoleContent.cs"
printf '%s\n' "PASS production presenters, resolver, device tracker, MemberMenuTutorial consumer and authored native validator compile against real Unity/InputSystem/TMP/UI APIs." "Package assemblies: $reference" "Compilation only: no Unity import, native execution, physical hardware or rendered UI pass."
