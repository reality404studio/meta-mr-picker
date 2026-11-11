#!/bin/sh
# 사용: sh scripts/pull-stills.sh [받을 폴더=evidence/stills]   헤드셋에서 왼손 엄지 톡으로 저장한 투명 배경 PNG 를 맥으로 가져온다
set -u
ADB=/Applications/Unity/Hub/Editor/6000.3.27f2/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
OUT=${1:-evidence/stills}
mkdir -p "$OUT"
"$ADB" pull /sdcard/Android/data/com.DefaultCompany.moodpicker/files/stills/. "$OUT/"
ls -la "$OUT"
