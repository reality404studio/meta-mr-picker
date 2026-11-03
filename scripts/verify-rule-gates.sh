#!/bin/sh
# 사용: sh unity/mood-picker/scripts/verify-rule-gates.sh   (에디터 닫고, 약 10분)
set -u
PROJ=$(cd "$(dirname "$0")/.." && pwd)
cd "$PROJ" || exit 1
VER=$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)
UNITY="/Applications/Unity/Hub/Editor/$VER/Unity.app/Contents/MacOS/Unity"
PRE="$PROJ/../shared/BuildGate/Preflight~/preflight.sh"
BK="$PROJ/Library/verify-rule-gates-backup"
fail=0
ok() { echo "PASS  $1"; }
ng() { echo "FAIL  $1"; fail=1; }

rm -rf "$BK"; mkdir -p "$BK"
for f in Assets/Scripts/FirstLight/FirstLight.cs Assets/Oculus/OculusProjectConfig.asset Assets/Editor/MoodPickerBuild.cs; do
  mkdir -p "$BK/$(dirname "$f")"; cp "$f" "$BK/$f"
done
restore() {
  for f in Assets/Scripts/FirstLight/FirstLight.cs Assets/Oculus/OculusProjectConfig.asset Assets/Editor/MoodPickerBuild.cs; do cp "$BK/$f" "$f"; done
  rm -rf Assets/Scripts/_Mutation Assets/Scripts/_Mutation.meta
}
trap restore EXIT INT TERM

echo "== 0. 깨끗한 상태"
sh "$PRE" . > "$BK/pre0.log" 2>&1 && ok "preflight 통과" || ng "preflight 가 깨끗한 상태에서 실패"

echo "== 1. 되돌리기 (뮤테이션 6개를 한 번에)"
sed -i '' '1s|^|// 이유 주석\n|' Assets/Scripts/FirstLight/FirstLight.cs
sed -i '' 's/handTrackingSupport: [12]/handTrackingSupport: 0/' Assets/Oculus/OculusProjectConfig.asset
python3 -I - Assets/Editor/MoodPickerBuild.cs <<'EOF'
import sys,re
p=sys.argv[1]; t=open(p).read()
t=re.sub(r"(static void BuildDevApkBatch\(\)\s*\{)", r"\1\n        BuildDevApk();", t, count=1)
open(p,"w").write(t)
EOF
mkdir -p Assets/Scripts/_Mutation
cat > Assets/Scripts/_Mutation/Mutation.cs <<'EOF'
public class Mutation
{
    public int width = 1280;
    public string a = "cam.GetTexture()";
    public string b = "Permission.RequestUserPermission";
}
EOF

sh "$PRE" . > "$BK/pre1.log" 2>&1
grep -q "\[FAIL\] comment: Assets/Scripts/FirstLight/FirstLight.cs:1" "$BK/pre1.log" && ok "preflight 가 새 주석 줄에서 실패" || ng "preflight 가 주석을 못 잡음"
grep -q "\[FAIL\] hand-tracking" "$BK/pre1.log" && ok "preflight 가 ControllersOnly 에서 실패" || ng "preflight 가 hand-tracking 을 못 잡음"

"$UNITY" -batchmode -nographics -projectPath "$PROJ" -buildTarget Android -runTests -testPlatform EditMode \
  -testResults "$BK/mut.xml" -logFile "$BK/mut.log" > /dev/null 2>&1
for t in ProjectPassesBuildGateFileChecks HandTracking_IsNotControllersOnly CameraPermission_IsRequestedOnlyByOvrManager \
         RuntimeScripts_DoNotHardcodeCameraResolution PcaGetTexture_IsGuardedByIsPlaying 'BatchEntry_WaitsForMetaEditorReady(&quot;Editor/MoodPickerBuild.cs'; do
  if grep -o "<test-case [^>]*fullname=\"[^\"]*$t[^>]*result=\"Failed\"" "$BK/mut.xml" > /dev/null 2>&1; then ok "되돌리면 실패: $t"; else ng "되돌려도 통과: $t"; fi
done

echo "== 2. 빌드 게이트가 주석에서 빌드를 막는다"
restore
sed -i '' '1s|^|// 이유 주석\n|' Assets/Scripts/FirstLight/FirstLight.cs
"$UNITY" -batchmode -nographics -projectPath "$PROJ" -buildTarget Android -executeMethod MoodPickerBuild.BuildDevApkBatch -logFile "$BK/build_ng.log" > /dev/null 2>&1
code=$?
if [ $code -ne 0 ] && grep -q "\[BuildGate\] \[FAIL\] comment" "$BK/build_ng.log"; then ok "빌드 게이트가 주석 줄에서 빌드 실패 (exit $code)"; else ng "빌드 게이트가 주석을 못 막음 (exit $code)"; fi

echo "== 3. 원복 후 배치 빌드"
restore
"$UNITY" -batchmode -nographics -projectPath "$PROJ" -buildTarget Android -executeMethod MoodPickerBuild.BuildDevApkBatch -logFile "$BK/build_ok.log" > /dev/null 2>&1
code=$?
grep -E "\[BuildGate\] 통과|\[MP\] build result" "$BK/build_ok.log" | sort -u
[ $code -eq 0 ] && ok "배치 빌드 exit 0" || ng "배치 빌드 실패 (exit $code)"

echo "== 결과: $([ $fail = 0 ] && echo PASS || echo FAIL)"
exit $fail
