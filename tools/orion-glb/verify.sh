#!/bin/sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
cd "$ROOT"
case "${1:-}" in
  '') UNITY_CHECK=1 ;;
  --no-unity) UNITY_CHECK=0 ;;
  *) echo "Usage: $0 [--no-unity]" >&2; exit 2 ;;
esac
[ "$#" -le 1 ] || { echo "Too many arguments" >&2; exit 2; }
python3 -B tools/orion-glb/verify_glb.py
node --import ./tools/orion-glb/register_three.mjs tools/orion-glb/verify_three.mjs
if [ "$UNITY_CHECK" -eq 0 ]; then
  echo "PARTIAL PASS: GLB + three.js; SKIP Unity (--no-unity), full card gate unverified"
  exit 0
fi
python3 - <<'PY'
import json
from pathlib import Path
dependencies = json.loads(Path('Packages/manifest.json').read_text())['dependencies']
if not any(name in dependencies for name in ('com.unity.cloud.gltfast', 'com.atteneder.gltfast')):
    raise SystemExit('FAIL Unity prerequisite: glTFast absent from Packages/manifest.json (not installed by verifier)')
PY
if pgrep -f "projectPath .*unity/mood-picker"; then
  echo "FAIL Unity prerequisite: another Unity process may own this project" >&2
  exit 1
else
  PROCESS_STATUS=$?
  if [ "$PROCESS_STATUS" -ne 1 ]; then
    echo "FAIL Unity prerequisite: process list unavailable; use --no-unity for partial validation" >&2
    exit 1
  fi
fi
sh scripts/run-editmode-tests.sh 'MoodPicker.Tests.OrionGlbTests'
python3 - <<'PY'
import xml.etree.ElementTree as ET
root = ET.parse('Library/TestResults/editmode.xml').getroot()
cases = [case for case in root.iter('test-case') if 'MoodPicker.Tests.OrionGlbTests.' in case.get('fullname', '')]
if not cases or any(case.get('result') != 'Passed' for case in cases):
    raise SystemExit('FAIL Unity: missing, skipped or failed OrionGlbTests')
print(f'PASS Unity EditMode: OrionGlbTests passed={len(cases)}')
PY
echo "PASS: GLB + three.js + Unity EditMode"
