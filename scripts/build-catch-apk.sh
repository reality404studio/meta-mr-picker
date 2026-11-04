#!/bin/sh
# 사용: sh unity/mood-picker/scripts/build-catch-apk.sh [복사할 apk 이름]  → Builds/catch-dev.apk (+ 이름 주면 그 이름으로 복사)
set -u
PROJ=$(cd "$(dirname "$0")/.." && pwd)
VER=$(sed -n 's/^m_EditorVersion: //p' "$PROJ/ProjectSettings/ProjectVersion.txt")
UNITY="/Applications/Unity/Hub/Editor/$VER/Unity.app/Contents/MacOS/Unity"
n=0
while pgrep -f "projectPath .*unity/mood-picker" > /dev/null; do
  [ $n -ge 60 ] && { echo "다른 Unity 가 너무 오래 열려 있음"; exit 1; }
  sleep 10; n=$((n+1))
done
"$UNITY" -batchmode -nographics -projectPath "$PROJ" -buildTarget Android -executeMethod MoodPickerBuild.BuildCatchApkBatch -logFile "$PROJ/Library/catch-build.log"
code=$?
grep -E "\[BuildGate\] (통과|FAIL)|\[BuildGate\] \[FAIL\]|\[MP\] build result" "$PROJ/Library/catch-build.log" | sort -u
grep -q "\[MP\] build result=Succeeded" "$PROJ/Library/catch-build.log" || { echo "build FAIL (exit $code)"; exit 1; }
[ -n "${1:-}" ] && cp "$PROJ/Builds/catch-dev.apk" "$PROJ/Builds/$1" && echo "copied → Builds/$1"
exit 0
