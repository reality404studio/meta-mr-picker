#!/bin/sh
# 사용: sh scripts/ritual.sh <명령>   시간 채집 리추얼 실기 실험용 (헤드셋을 쓴 채 값이 0.5초 안에 바뀐다)
#   unit 15|20|25        10분 단위 길이 (mm)
#   shape rod|blob       합친 모양: 긴 막대 / 둥근 덩어리
#   nodes on|off         마디 항상 보임 / 당길 때만
#   labels on|off        개발용 "40분" 라벨과 합계
#   glow seep|petals     봉인 뒤 책상 빛: 층 순서로 번지는 웅덩이 / 방사형 빛줄기
#   set <키> <값>        TimeRitualConfig 의 아무 값 (예: set glowIntensity 0.3)
#   push <파일.json>     여러 값을 한 번에
#   reset                하루를 처음부터 (병·조각 비움)
#   install | run | stop | log <파일> | temp
set -u
ADB=/Applications/Unity/Hub/Editor/6000.3.27f2/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
PKG="${PKG:-com.DefaultCompany.timeritual}"
DIR=/sdcard/Android/data/$PKG/files
APK="$(cd "$(dirname "$0")/.." && pwd)/Builds/time-ritual-dev.apk"
TMP="${TMPDIR:-/tmp}/ritual-live.json"
send() { printf '%s\n' "$1" > "$TMP"; "$ADB" shell mkdir -p "$DIR" >/dev/null 2>&1; "$ADB" push "$TMP" "$DIR/ritual.json"; }
case "${1:-}" in
  unit) send "{\"unitLength\": 0.0${2}}" ;;
  shape) [ "$2" = rod ] && send '{"mergedShape": 0}' || send '{"mergedShape": 1}' ;;
  nodes) [ "$2" = on ] && send '{"nodesAlwaysVisible": true}' || send '{"nodesAlwaysVisible": false}' ;;
  labels) [ "$2" = on ] && send '{"showMinutesLabel": true, "showTotals": true}' || send '{"showMinutesLabel": false, "showTotals": false}' ;;
  glow) [ "$2" = petals ] && send '{"glowStyle": 1}' || send '{"glowStyle": 0}' ;;
  set) send "{\"$2\": $3}" ;;
  push) "$ADB" shell mkdir -p "$DIR" >/dev/null 2>&1; "$ADB" push "$2" "$DIR/ritual.json" ;;
  reset) send "{\"resetCounter\": $(date +%s)}" ;;
  install) "$ADB" install -r -g "$APK" ;;
  run) "$ADB" shell am force-stop "$PKG"; "$ADB" logcat -c; "$ADB" shell am start -n "$PKG/com.unity3d.player.UnityPlayerGameActivity" ;;
  stop) "$ADB" shell am force-stop "$PKG" ;;
  log) "$ADB" logcat -v time -s VrApi:I Unity:I > "$2" ;;
  temp) "$ADB" shell dumpsys battery | grep -E "temperature|level|status"; "$ADB" shell dumpsys thermalservice 2>/dev/null | grep -E "Thermal Status|mValue" | head -12 ;;
  *) sed -n 2,12p "$0"; exit 1 ;;
esac
