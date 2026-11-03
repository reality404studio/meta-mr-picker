#!/bin/sh
# 작업대(my-mr-studio)의 정본 문서를 docs/ 사본으로 복사한다. 문서는 작업대에서 고치고, 여기선 고치지 않는다.
set -e
HERE="$(cd "$(dirname "$0")/.." && pwd)"
STUDIO="$HERE/../../studio"
cp "$STUDIO/apps/mood-picker/HANDOFF.md"                "$HERE/docs/HANDOFF.md"
cp "$STUDIO/shared/DECIDED.md"                          "$HERE/docs/DECIDED.md"
cp "$STUDIO/shared/REJECTED.md"                         "$HERE/docs/REJECTED.md"
cp "$STUDIO/shared/nodes/unity-guardrails/README.md"    "$HERE/docs/UNITY_GUARDRAILS.md"
cp "$STUDIO/apps/mood-picker/nodes/brief/README.md"     "$HERE/docs/BRIEF.md"
echo "docs/ 동기화 완료"
node "$HERE/scripts/export-board.mjs"
