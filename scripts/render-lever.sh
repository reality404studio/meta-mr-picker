#!/bin/sh
# 사용: sh unity/mood-picker/scripts/render-lever.sh  → Library/PunchLever/lever.png (작은·보통·큰 손 레버 배치 렌더)
set -u
PROJ=$(cd "$(dirname "$0")/.." && pwd)
VER=$(sed -n 's/^m_EditorVersion: //p' "$PROJ/ProjectSettings/ProjectVersion.txt")
UNITY="/Applications/Unity/Hub/Editor/$VER/Unity.app/Contents/MacOS/Unity"
OUT="$PROJ/Library/PunchLever"
mkdir -p "$OUT"; rm -f "$OUT/lever.png"
"$UNITY" -batchmode -projectPath "$PROJ" -buildTarget Android -executeMethod PunchLeverRender.RenderBatch -leverOut "$OUT/lever.png" -logFile "$OUT/render.log" > /dev/null 2>&1
code=$?
grep -E "\[MP\] lever-render|error CS" "$OUT/render.log" | sort -u
[ $code -eq 0 ] && grep -q "\[MP\] lever-render PASS" "$OUT/render.log" && [ -s "$OUT/lever.png" ]
