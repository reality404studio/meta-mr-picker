#!/bin/sh
set -eu
PROJ=$(cd "$(dirname "$0")/.." && pwd)
if [ "${1:-}" = --static ]; then
  exec python3 -B "$PROJ/Assets/Scripts/Folio/Verification~/verify_compile.py"
fi
OUT=${1:-"$PROJ/Library/ChipVerification"}
mkdir -p "$OUT"
OUT=$(cd "$OUT" && pwd)
VER=$(sed -n 's/^m_EditorVersion: //p' "$PROJ/ProjectSettings/ProjectVersion.txt")
UNITY="/Applications/Unity/Hub/Editor/$VER/Unity.app/Contents/MacOS/Unity"
SAMPLES="$PROJ/tools/web-orion/samples/chips"
python3 - "$PROJ" <<'PY'
import json,sys
from pathlib import Path
root=Path(sys.argv[1])
for path,needed in [
    ('Assets/Scripts/Dab/Editor/MoodPicker.Dab.Editor.asmdef', {'MoodPicker.Folio','MoodPicker.Folio.Runtime','MoodPicker.Tokens'}),
    ('Assets/Tests/EditMode/MoodPicker.Tests.EditMode.asmdef', {'MoodPicker.Folio','MoodPicker.Folio.Runtime'})]:
    missing=needed-set(json.loads((root/path).read_text())['references'])
    if missing:
        raise SystemExit(f'FAIL pending approved asmdef patch: {path}: {sorted(missing)}; see /tmp/orion-folio-asmdef.patch')
PY
printf '%s\n' '1. preflight'
if ! sh "$PROJ/../shared/BuildGate/Preflight~/preflight.sh" "$PROJ" > "$OUT/preflight.log" 2>&1; then
  tail -40 "$OUT/preflight.log"
  exit 1
fi
printf '%s\n' '2. shader gates and Roslyn'
if ! sh "$PROJ/scripts/verify-chip.sh" --static > "$OUT/static.log" 2>&1; then
  tail -40 "$OUT/static.log"
  exit 1
fi
cat "$OUT/static.log"
set +e
pgrep -f 'Unity.app/Contents/MacOS/Unity.*-projectPath .*unity/mood-picker' > "$OUT/processes.log" 2>&1
PROCESS_STATUS=$?
set -e
case "$PROCESS_STATUS" in
  1) ;;
  0) printf '%s\n' 'FAIL Unity project already open'; exit 1 ;;
  *) printf '%s\n' 'FAIL cannot inspect Unity processes'; cat "$OUT/processes.log"; exit 1 ;;
esac
printf '%s\n' '3. EditMode'
rm -f "$OUT/editmode.xml"
if ! "$UNITY" -batchmode -projectPath "$PROJ" -buildTarget Android \
  -runTests -testPlatform EditMode -testFilter 'MoodPicker.Tests.ChipTests;MoodPicker.Tests.ChipRuntimeTests;MoodPicker.Tests.FolioTests;MoodPicker.Tests.FolioRuntimeTests' \
  -testResults "$OUT/editmode.xml" -logFile "$OUT/editmode.log" > /dev/null 2>&1; then
  tail -50 "$OUT/editmode.log"
  exit 1
fi
python3 - "$OUT/editmode.xml" <<'PY'
import sys,xml.etree.ElementTree as ET
root=ET.parse(sys.argv[1]).getroot()
cases=list(root.iter('test-case'))
assert len(cases)>=5, 'missing tests'
assert root.get('result')=='Passed' and all(c.get('result')=='Passed' for c in cases), 'failed/skipped tests'
for name in ('ChipTests','ChipRuntimeTests','FolioTests','FolioRuntimeTests'):
    assert any('.'+name+'.' in c.get('fullname','') for c in cases), 'missing '+name
print(f'PASS EditMode: {len(cases)} tests')
PY
printf '%s\n' '4. real GPU: three chips, front/30 degrees, zero lights, ambient black'
rm -f "$OUT/chip-0.png" "$OUT/chip-30.png"
if ! "$UNITY" -batchmode -projectPath "$PROJ" -buildTarget Android \
  -executeMethod MoodPicker.Dab.ChipRender.RenderBatch -chipSamples "$SAMPLES" -chipOut "$OUT" \
  -logFile "$OUT/render.log" > /dev/null 2>&1; then
  tail -60 "$OUT/render.log"
  exit 1
fi
grep -F '[MP] chip' "$OUT/render.log"
grep -q '^\[MP\] chip PASS$' "$OUT/render.log"
[ -s "$OUT/chip-0.png" ] && [ -s "$OUT/chip-30.png" ]
printf '%s\n' 'PASS chip: preflight, shader gates, EditMode, GPU render'
