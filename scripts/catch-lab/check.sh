#!/bin/sh
# 사용: sh scripts/catch-lab/check.sh <영상> <출력 폴더>
set -eu
HERE=$(cd "$(dirname "$0")" && pwd)
VIDEO=$1
OUT=$2
TMP=$(mktemp -d)
python3 -I "$HERE/catch_lab.py" "$VIDEO" "$OUT"
python3 -I "$HERE/catch_lab.py" "$VIDEO" "$TMP" > /dev/null
if diff "$OUT/hashes.txt" "$TMP/hashes.txt"; then echo "rerun hashes: identical"; else echo "rerun hashes: DIFFER"; exit 1; fi
rm -rf "$TMP"
python3 -I - "$OUT/metrics.json" <<'PY'
import json, sys
m = json.load(open(sys.argv[1]))
fails = []
if not m["tile_seam_ratio_seamless"] < 1.0: fails.append("tile seam >= interior step")
if not m["tile_seam_ratio_seamless"] < m["tile_seam_ratio_raw"]: fails.append("tile seam not reduced")
if m["tile_interior_unchanged"] != 1.0: fails.append("tile interior pixels changed")
if not m["loop_seam_ratio_crossfade"] < m["loop_seam_ratio_raw"]: fails.append("loop seam not reduced")
print("gates:", "PASS" if not fails else "FAIL " + "; ".join(fails))
sys.exit(1 if fails else 0)
PY
cat "$OUT/hashes.txt"
