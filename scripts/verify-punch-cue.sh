#!/bin/sh
# 사용: sh unity/mood-picker/scripts/verify-punch-cue.sh  → preflight + PunchCue/CatchPunch EditMode 테스트 + 배치 렌더 Library/PunchCue/punch_cue.png
set -u
PROJ=$(cd "$(dirname "$0")/.." && pwd)
VER=$(sed -n 's/^m_EditorVersion: //p' "$PROJ/ProjectSettings/ProjectVersion.txt")
UNITY="/Applications/Unity/Hub/Editor/$VER/Unity.app/Contents/MacOS/Unity"
OUT="$PROJ/Library/PunchCue"
mkdir -p "$OUT"
fail=0
busy() { ps -axo args= | awk '$1 ~ /MacOS\/Unity$/ && /unity\/mood-picker/ {f=1} END {exit !f}'; }
wait_unity() { n=0; while busy; do [ $n -ge 60 ] && { echo "FAIL 다른 Unity 30분 넘게 실행 중"; exit 1; }; sleep 30; n=$((n+1)); done; }

echo "== 1. preflight"
if sh "$PROJ/../shared/BuildGate/Preflight~/preflight.sh" "$PROJ" > "$OUT/preflight.log" 2>&1; then echo "PASS  preflight"; else echo "FAIL  preflight"; tail -20 "$OUT/preflight.log"; fail=1; fi

echo "== 2. EditMode"
wait_unity
sh "$PROJ/scripts/run-editmode-tests.sh" "MoodPicker.Tests.(PunchCue|CatchPunch)" > "$OUT/tests.log" 2>&1 || fail=1
grep -E "total=|Failed" "$OUT/tests.log"

echo "== 3. 배치 렌더"
wait_unity
rm -f "$OUT/punch_cue.png"
"$UNITY" -batchmode -projectPath "$PROJ" -buildTarget Android -executeMethod PunchCueRender.RenderBatch -cueOut "$OUT/punch_cue.png" -logFile "$OUT/render.log" > /dev/null 2>&1
code=$?
grep -E "\[MP\] cue-render|error CS" "$OUT/render.log" | sort -u
if [ $code -eq 0 ] && grep -q "\[MP\] cue-render PASS" "$OUT/render.log" && [ -s "$OUT/punch_cue.png" ]; then echo "PASS  배치 렌더"; else echo "FAIL  배치 렌더 (exit $code)"; fail=1; fi
echo "== 결과: $([ $fail = 0 ] && echo PASS || echo FAIL)"
exit $fail
