#!/bin/sh
set -eu
PROJ=$(cd "$(dirname "$0")/.." && pwd)
TOKEN_TMP=$(mktemp -d "${TMPDIR:-/tmp}/orion-tokens-sync.XXXXXX")
trap 'rm -rf "$TOKEN_TMP"' EXIT HUP INT TERM
python3 -B "$PROJ/tools/orion-tokens/gen_tokens.py" --output-dir "$TOKEN_TMP"
diff -u "$PROJ/Assets/Scripts/Tokens/OrionTokens.g.cs" "$TOKEN_TMP/OrionTokens.g.cs"
diff -u "$PROJ/Assets/Scripts/Tokens/MoodPicker.Tokens.asmdef" "$TOKEN_TMP/MoodPicker.Tokens.asmdef"
echo 'PASS token sync: generated C# and asmdef match JSON'
python3 -B "$PROJ/tools/orion-tokens/test_generator.py"
python3 -B "$PROJ/tools/orion-tokens/verify_compile.py"
