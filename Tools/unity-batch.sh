#!/usr/bin/env bash
# Run a BatchQA method against the closed project and print the useful log lines.
#   Tools/unity-batch.sh Capture -qaScene Assets/Scenes/Lobby.unity -qaOut Logs/QA/lobby.png
#   Tools/unity-batch.sh Dump -qaScene Assets/Scenes/Game.unity -qaFilter Frame
#   Tools/unity-batch.sh Compiled
set -u
UNITY="${UNITY:-/c/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe}"
PROJECT="$(cd "$(dirname "$0")/.." && pwd -W 2>/dev/null || pwd)"
METHOD="$1"; shift
LOG="$PROJECT/Logs/QA/unity-$METHOD.log"
mkdir -p "$PROJECT/Logs/QA"
"$UNITY" -batchmode -projectPath "$PROJECT" -executeMethod "HauntedFish.EditorTools.BatchQA.$METHOD" "$@" -logFile "$LOG" -quit
code=$?
grep -E "\[BatchQA\]|error CS|Exception|executeMethod|Aborting batchmode" "$LOG" | grep -v "^UnityEngine" | head -40
echo "exit $code (full log: $LOG)"
exit $code
