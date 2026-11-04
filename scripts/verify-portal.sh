#!/bin/sh
# 사용: sh unity/mood-picker/scripts/verify-portal.sh [테스트 필터]  → preflight + 포탈 EditMode 테스트 (다른 Unity 가 열려 있으면 최대 30분 기다림)
set -u
PROJ=$(cd "$(dirname "$0")/.." && pwd)
OUT="$PROJ/Library/PortalVerify"
mkdir -p "$OUT"
busy() { ps -axo args= | awk '$1 ~ /MacOS\/Unity$/ && /unity\/mood-picker/ {f=1} END {exit !f}'; }
if sh "$PROJ/../shared/BuildGate/Preflight~/preflight.sh" "$PROJ" > "$OUT/preflight.log" 2>&1; then echo "PASS  preflight"; else echo "FAIL  preflight"; tail -20 "$OUT/preflight.log"; exit 1; fi
n=0; while busy; do [ $n -ge 60 ] && { echo "FAIL 다른 Unity 30분 넘게 실행 중"; exit 1; }; sleep 30; n=$((n+1)); done
sh "$PROJ/scripts/run-editmode-tests.sh" "${1:-MoodPicker.Tests.(Portal|Gate|Dip)}"
