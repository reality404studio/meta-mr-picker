#!/bin/sh
# EditMode 테스트를 배치모드로 돌린다. 에디터가 이 프로젝트를 열고 있으면 실패한다 (먼저 닫기).
# 사용: sh unity/mood-picker/scripts/run-editmode-tests.sh  → Library/TestResults/editmode.xml
set -u
PROJ=$(cd "$(dirname "$0")/.." && pwd)
VER=$(sed -n 's/^m_EditorVersion: //p' "$PROJ/ProjectSettings/ProjectVersion.txt")
UNITY="/Applications/Unity/Hub/Editor/$VER/Unity.app/Contents/MacOS/Unity"
OUT="$PROJ/Library/TestResults"
mkdir -p "$OUT"
rm -f "$OUT/editmode.xml"
"$UNITY" -batchmode -nographics -projectPath "$PROJ" -buildTarget Android \
  -runTests -testPlatform EditMode -testResults "$OUT/editmode.xml" -logFile "$OUT/editmode.log"
code=$?
if [ ! -f "$OUT/editmode.xml" ]; then
  echo "결과 XML 없음 (exit $code). 로그: $OUT/editmode.log"
  grep -E "error CS|another Unity instance|Aborting" "$OUT/editmode.log" | head -10
  exit 1
fi
sed -n 's/.*<test-run [^>]*total="\([0-9]*\)"[^>]*passed="\([0-9]*\)"[^>]*failed="\([0-9]*\)".*/total=\1 passed=\2 failed=\3/p' "$OUT/editmode.xml" | head -1
grep -o '<test-case [^>]*fullname="[^"]*"[^>]*result="[^"]*"' "$OUT/editmode.xml" | sed 's/.*fullname="\([^"]*\)".*result="\([^"]*\)".*/  \2  \1/'
echo "exit=$code  xml=$OUT/editmode.xml"
exit $code
