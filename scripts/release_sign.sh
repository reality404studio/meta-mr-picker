#!/bin/sh
# Time Ritual 제출용 APK 서명 (Meta Horizon 릴리스 채널 업로드용)
#
# 비밀번호는 파일·명령어에 두지 않는다. macOS Keychain 의 TIMERITUAL_KEYSTORE_PASS 를
# envrun 이 실행 순간에만 환경변수로 넣는다 (env.keys 에는 이름만).
#   등록 (네이티브 터미널에서 1회, 값은 프롬프트로):
#     security add-generic-password -a "$USER" -s TIMERITUAL_KEYSTORE_PASS -w
#
# 사용 (앱 레포 루트에서):
#   envrun sh scripts/release_sign.sh keystore   # 키스토어 1회 생성 (있으면 멈춤 — 덮어쓰지 않는다)
#   envrun sh scripts/release_sign.sh sign       # Builds/time-ritual-release-unsigned.apk → Builds/time-ritual-release.apk
#   sh scripts/release_sign.sh verify            # 서명·디버그 여부·패키지·버전 확인 (비밀번호 불필요)
#
# 키스토어는 이 앱의 영구 서명 키다. 한 번 업로드하면 같은 키로만 업데이트할 수 있으니 백업한다.
set -eu
UNITY=/Applications/Unity/Hub/Editor/6000.3.27f2/PlaybackEngines/AndroidPlayer
BT="$UNITY/SDK/build-tools/36.0.0"
export JAVA_HOME="$UNITY/OpenJDK"
KS="${TIMERITUAL_KEYSTORE:-$HOME/.keystores/timeritual-release.keystore}"
ALIAS=timeritual
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
IN="$ROOT/Builds/time-ritual-release-unsigned.apk"
OUT="$ROOT/Builds/time-ritual-release.apk"

need_pass() {
  if [ -z "${TIMERITUAL_KEYSTORE_PASS:-}" ]; then
    echo "TIMERITUAL_KEYSTORE_PASS 가 주입되지 않음 — envrun 으로 실행 (env.keys, Keychain 등록 확인: envcheck)" >&2
    exit 2
  fi
}

case "${1:-}" in
  keystore)
    need_pass
    if [ -e "$KS" ]; then echo "이미 있음: $KS (덮어쓰지 않는다)" >&2; exit 1; fi
    mkdir -p "$(dirname "$KS")"
    chmod 700 "$(dirname "$KS")"
    "$JAVA_HOME/bin/keytool" -genkeypair -keystore "$KS" -storetype PKCS12 -alias "$ALIAS" \
      -keyalg RSA -keysize 4096 -validity 10000 -dname "CN=Time Ritual" \
      -storepass:env TIMERITUAL_KEYSTORE_PASS -keypass:env TIMERITUAL_KEYSTORE_PASS
    chmod 600 "$KS"
    echo "만듦: $KS — 백업할 것"
    ;;
  sign)
    need_pass
    [ -f "$IN" ] || { echo "없음: $IN (MoodPickerBuild.BuildTimeRitualReleaseApkBatch 먼저)" >&2; exit 1; }
    [ -f "$KS" ] || { echo "없음: $KS (keystore 먼저)" >&2; exit 1; }
    rm -f "$OUT" "$OUT.aligned"
    "$BT/zipalign" -p -f 4 "$IN" "$OUT.aligned"
    "$BT/apksigner" sign --ks "$KS" --ks-key-alias "$ALIAS" \
      --ks-pass env:TIMERITUAL_KEYSTORE_PASS --key-pass env:TIMERITUAL_KEYSTORE_PASS \
      --v1-signing-enabled true --v2-signing-enabled true --v3-signing-enabled false --out "$OUT" "$OUT.aligned"
    rm -f "$OUT.aligned" "$OUT.idsig"
    echo "서명: $OUT"
    sh "$0" verify
    ;;
  verify)
    APK="${2:-$OUT}"
    "$BT/apksigner" verify -v --print-certs "$APK" | grep -E "Verified using v1|Verified using v2|certificate DN"
    "$BT/aapt2" dump badging "$APK" | grep -E "^package:|application-debuggable|^application: label" || true
    if "$BT/aapt2" dump badging "$APK" | grep -q application-debuggable; then echo "실패: debuggable (개발 빌드)" >&2; exit 1; fi
    if "$BT/apksigner" verify --print-certs "$APK" | grep -q "CN=Android Debug"; then echo "실패: 디버그 키 서명" >&2; exit 1; fi
    if ! "$BT/apksigner" verify -v "$APK" | grep -q "Verified using v2 scheme (APK Signature Scheme v2): true"; then echo "실패: v2 서명 없음 (Meta 업로드는 v2 필요 — minSdk 32 에선 apksigner 가 v3 만 쓰므로 v3 를 끈다)" >&2; exit 1; fi
    echo "통과: 디버그 아님, 정식 키 서명, v2 서명 있음"
    ;;
  *)
    sed -n '2,14p' "$0"
    exit 2
    ;;
esac
