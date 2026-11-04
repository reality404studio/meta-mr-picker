#!/bin/sh
# 사용: sh unity/mood-picker/scripts/verify-dab-shader.sh [paint.json] [texture.png]
#   1) preflight  2) 게이트가 ColorMask RGB 픽스처를 거절  3) 광원 0개 씬 배치 렌더 + 2톤·알파 검사 → Library/CelRender/cel_render.png
set -u
PROJ=$(cd "$(dirname "$0")/.." && pwd)
STUDIO=$(cd "$PROJ/../.." && pwd)
LAB="$STUDIO/studio/apps/mood-picker/nodes/catch/assets/catch-lab"
PAINT=${1:-$LAB/paint.json}
TEX=${2:-$LAB/tile_seamless.png}
VER=$(sed -n 's/^m_EditorVersion: //p' "$PROJ/ProjectSettings/ProjectVersion.txt")
UNITY="/Applications/Unity/Hub/Editor/$VER/Unity.app/Contents/MacOS/Unity"
PRE="$PROJ/../shared/BuildGate/Preflight~/preflight.sh"
CHECKER="$PROJ/../shared/BuildGate/Preflight~/Checker/Checker.csproj"
OUT="$PROJ/Library/CelRender"
mkdir -p "$OUT"
fail=0
ok() { echo "PASS  $1"; }
ng() { echo "FAIL  $1"; fail=1; }

echo "== 1. preflight"
if sh "$PRE" "$PROJ" > "$OUT/preflight.log" 2>&1; then ok "preflight"; else ng "preflight"; tail -20 "$OUT/preflight.log"; fi
grep "shader-colormask" "$OUT/preflight.log"

echo "== 2. 셰이더 게이트 픽스처"
FX="$OUT/fixture-project"
rm -rf "$FX"; mkdir -p "$FX/Assets/Shaders" "$FX/ProjectSettings"
cat > "$FX/Assets/Shaders/BadMask.shader" <<'EOF'
Shader "Fixture/BadMask" { SubShader { Pass { ColorMask RGB } } }
EOF
cat > "$FX/Assets/Shaders/GoodMask.shader" <<'EOF'
Shader "Fixture/GoodMask" { SubShader { Pass { ColorMask RGBA } Pass { ColorMask 0 } Pass { // ColorMask RGB
} } }
EOF
dotnet run --project "$CHECKER" -v q -- "$FX" > "$OUT/fixture.log" 2>&1
grep -q "\[FAIL\] shader-colormask: Assets/Shaders/BadMask.shader:1 ColorMask RGB" "$OUT/fixture.log" && ok "게이트가 ColorMask RGB 를 거절" || ng "게이트가 ColorMask RGB 를 못 잡음"
grep -q "shader-colormask: Assets/Shaders/GoodMask" "$OUT/fixture.log" && ng "게이트가 RGBA / 0 / 주석을 잘못 잡음" || ok "RGBA / 0 / 주석 줄은 통과"
rm -rf "$FX"

echo "== 3. 광원 0개 배치 렌더"
n=0
while pgrep -f "Unity.app/Contents/MacOS/Unity .*-projectPath .*unity/mood-picker" > /dev/null; do
  [ $n -ge 30 ] && { ng "다른 Unity 가 30분 넘게 프로젝트를 열고 있음"; echo "== 결과: FAIL"; exit 1; }
  echo "다른 Unity 실행 중, 60초 대기 ($n)"; sleep 60; n=$((n+1))
done
rm -f "$OUT/cel_render.png"
"$UNITY" -batchmode -projectPath "$PROJ" -buildTarget Android \
  -executeMethod MoodPicker.Dab.CelShaderRender.RenderBatch \
  -celPaint "$PAINT" -celTex "$TEX" -celOut "$OUT/cel_render.png" -logFile "$OUT/render.log" > /dev/null 2>&1
code=$?
grep -E "\[MP\] cel-render|error CS" "$OUT/render.log" | sort -u
if [ $code -eq 0 ] && grep -q "\[MP\] cel-render PASS" "$OUT/render.log" && [ -s "$OUT/cel_render.png" ]; then
  ok "배치 렌더 + 2톤·알파 검사 (exit $code)"
else
  ng "배치 렌더 (exit $code, 로그 $OUT/render.log)"
fi

echo "== 결과: $([ $fail = 0 ] && echo PASS || echo FAIL)"
exit $fail
