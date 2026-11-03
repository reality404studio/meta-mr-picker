#!/bin/sh
# 사용: sh unity/mood-picker/scripts/verify-device-log.sh <Unity 빌드 로그> <기기 logcat 발췌> <필요한 [MP] 패턴>...
set -u
BUILD_LOG=$1; DEVICE_LOG=$2; shift 2
fail=0
grep -q "Build Finished, Result: Success" "$BUILD_LOG" || { echo "FAIL 빌드 성공 줄 없음"; fail=1; }
ID=$(sed -n 's/.*\[BuildGate\] 통과. build=\([^ ]*\).*/\1/p' "$BUILD_LOG" | tail -1)
[ -n "$ID" ] || { echo "FAIL 빌드 게이트 통과 줄 없음"; exit 1; }
echo "gate build=$ID"
grep -q "\[MP\] build=$ID" "$DEVICE_LOG" && echo "PASS 기기 build=$ID 일치" || { echo "FAIL 기기 로그의 build= 가 $ID 아님 (헛빌드)"; fail=1; }
for p in "$@"; do
  n=$(grep -c -- "$p" "$DEVICE_LOG")
  [ "$n" -gt 0 ] && echo "PASS '$p' ${n}줄" || { echo "FAIL '$p' 없음"; fail=1; }
done
exit $fail
