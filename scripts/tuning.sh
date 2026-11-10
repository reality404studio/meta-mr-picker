#!/bin/sh
# 사용: sh scripts/tuning.sh push <tuning.json>   기기의 조율 값을 바꾼다 (0.5초 안에 반영)  ·  sh scripts/tuning.sh pull [out.json]   헤드셋에서 SaveTuning 으로 저장한 값을 가져온다
set -u
ADB=/Applications/Unity/Hub/Editor/6000.3.27f2/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
DIR=/sdcard/Android/data/com.DefaultCompany.moodpicker/files
case "${1:-}" in
  push) "$ADB" push "$2" "$DIR/tuning.json" ;;
  pull) "$ADB" pull "$DIR/tuning-saved.json" "${2:-evidence/thermal/tuning-saved.json}" ;;
  *) sed -n 2p "$0"; exit 1 ;;
esac
