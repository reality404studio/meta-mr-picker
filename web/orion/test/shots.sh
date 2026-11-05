#!/bin/sh
# Headless Chrome screenshots of the three page modes -> test/shots/*.png (1280x720).
set -eu
HERE=$(cd "$(dirname "$0")" && pwd)
ROOT=$(cd "$HERE/.." && pwd)
OUT="$HERE/shots"
PORT=${ORION_SHOT_PORT:-5181}
CHROME=${CHROME:-"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"}
INBOX=$(mktemp -d "${TMPDIR:-/tmp}/orion-shots-inbox.XXXXXX")
mkdir -p "$OUT"

node "$ROOT/server/server.mjs" --port "$PORT" --host 127.0.0.1 --dir "$INBOX" >"$OUT/server.log" 2>&1 &
SERVER_PID=$!
trap 'kill $SERVER_PID 2>/dev/null; rm -rf "$INBOX"' EXIT INT TERM

for i in 1 2 3 4 5 6 7 8 9 10; do
  if curl -sf "http://127.0.0.1:$PORT/health" >/dev/null 2>&1; then break; fi
  sleep 0.3
done
curl -sf "http://127.0.0.1:$PORT/health" >/dev/null

shot() {
  mode=$1
  file=$2
  "$CHROME" --headless=new --use-angle=swiftshader --enable-unsafe-swiftshader --hide-scrollbars \
    --force-device-scale-factor=1 --window-size=1280,720 --virtual-time-budget=10000 \
    --screenshot="$OUT/$file" "http://127.0.0.1:$PORT/?mode=$mode&spin=0" >/dev/null 2>&1
  test -s "$OUT/$file"
  echo "shot $mode -> $OUT/$file"
}

shot editor editor.png
shot hero hero.png
shot look look-web.png
