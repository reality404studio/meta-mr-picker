#!/bin/sh
# 사용: sh scripts/verify-orion-look.sh [--static] [output-directory]
set -eu
PROJ=$(cd "$(dirname "$0")/.." && pwd)
MODE=${1:-full}
if [ "$MODE" = --static ]; then
  exec python3 -B "$PROJ/Assets/Scripts/Dab/Editor/Verification~/verify_compile.py"
fi
if [ "$MODE" = full ]; then
  OUT="$PROJ/Library/OrionLook"
else
  OUT="$MODE"
fi
mkdir -p "$OUT"
OUT=$(cd "$OUT" && pwd)
VER=$(sed -n 's/^m_EditorVersion: //p' "$PROJ/ProjectSettings/ProjectVersion.txt")
UNITY="/Applications/Unity/Hub/Editor/$VER/Unity.app/Contents/MacOS/Unity"
PRE="$PROJ/../shared/BuildGate/Preflight~/preflight.sh"
SAMPLES="$PROJ/tools/web-orion/samples/chips"

echo '== 1. preflight compilation'
if ! sh "$PRE" "$PROJ" > "$OUT/preflight.log" 2>&1; then
  tail -30 "$OUT/preflight.log"
  exit 1
fi
if ! python3 -B "$PROJ/Assets/Scripts/Dab/Editor/Verification~/verify_compile.py" > "$OUT/static.log" 2>&1; then
  tail -40 "$OUT/static.log"
  exit 1
fi
cat "$OUT/static.log"
echo '== 2. shader gates'
grep -E 'outline-shader|shader-colormask|PASS shader static|PASS gate fixtures' "$OUT/static.log"
for chip in yunseul gradient_sample; do
  [ -s "$SAMPLES/$chip/tile.png" ] || { echo "FAIL missing chip $chip"; exit 1; }
done
set +e
pgrep -f 'Unity.app/Contents/MacOS/Unity.*-projectPath .*unity/mood-picker' > "$OUT/processes.log" 2>&1
PROCESS_STATUS=$?
set -e
case "$PROCESS_STATUS" in
  1) ;;
  0) echo 'FAIL project already open in Unity'; cat "$OUT/processes.log"; exit 1 ;;
  *) echo 'FAIL cannot inspect Unity processes'; cat "$OUT/processes.log"; exit 1 ;;
esac

echo '== 3. EditMode tests with real graphics'
rm -f "$OUT/editmode.xml"
if ! "$UNITY" -batchmode -projectPath "$PROJ" -buildTarget Android \
  -runTests -testPlatform EditMode -testFilter MoodPicker.Tests.OrionLookTests \
  -testResults "$OUT/editmode.xml" -logFile "$OUT/editmode.log" > /dev/null 2>&1; then
  tail -40 "$OUT/editmode.log"
  exit 1
fi
python3 - "$OUT/editmode.xml" <<'PY'
import sys
import xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
cases = list(root.iter('test-case'))
assert len(cases) >= 15, 'OrionLook test cases missing'
assert all(case.get('result') == 'Passed' for case in cases), 'failed/skipped/inconclusive test'
assert root.get('result') == 'Passed', 'test run failed'
print(f'PASS OrionLook EditMode: {len(cases)} tests')
PY

echo '== 4. three spheres, zero lights, black ambient, no skybox'
rm -f "$OUT/orion-look.png" "$OUT/orion-look-white-reference.png"
if ! "$UNITY" -batchmode -projectPath "$PROJ" -buildTarget Android \
  -executeMethod MoodPicker.Dab.OrionLookRender.RenderBatch \
  -orionSamples "$SAMPLES" -orionOut "$OUT" -logFile "$OUT/render.log" > /dev/null 2>&1; then
  tail -50 "$OUT/render.log"
  exit 1
fi
grep -F '[MP] orion-look' "$OUT/render.log"
grep -q '^\[MP\] orion-look PASS$' "$OUT/render.log"
[ -s "$OUT/orion-look.png" ] && [ -s "$OUT/orion-look-white-reference.png" ]
echo 'PASS ORION look: preflight, shader gates, EditMode, GPU render'
