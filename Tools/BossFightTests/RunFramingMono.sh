#!/usr/bin/env bash
set -euo pipefail
# Run the production pure camera math without launching native Unity.
editor_data="${1:?Pass the Unity Editor/Data directory}"
repo="$(cd "$(dirname "$0")/../.." && pwd)"
mono="$editor_data/MonoBleedingEdge/bin/mono"
csc="$editor_data/MonoBleedingEdge/lib/mono/4.5/csc.exe"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
cat > "$work/Main.cs" <<'CS'
using System;
class MainRunner
{
    static void Main()
    {
        int checks=0;
        BossPerspectiveFramingTests.Run((ok,name)=>{if(!ok)throw new Exception(name);checks++;});
        Console.WriteLine(checks+" production camera framing checks passed.");
    }
}
CS
"$mono" "$csc" -nologo -out:"$work/FramingTests.exe" "$work/Main.cs" \
  "$repo/Tools/BossFightTests/BossPerspectiveFramingTests.cs" \
  "$repo/Assets/Scripts/BossFight/BossPerspectiveFraming.cs" \
  "$repo/Assets/Scripts/BossFight/BossMatch.cs"
"$mono" "$work/FramingTests.exe"
