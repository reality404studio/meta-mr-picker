import json
import subprocess
import sys
import tempfile
from pathlib import Path

from gen_tokens import ROOT


with tempfile.TemporaryDirectory(prefix='orion-token-generator-') as temporary:
    output = Path(temporary)
    original = json.loads((ROOT / 'design/tokens/orion.tokens.json').read_text())

    def reverse_keys(value):
        if isinstance(value, dict):
            return {key: reverse_keys(value[key]) for key in reversed(list(value))}
        if isinstance(value, list):
            return [reverse_keys(item) for item in value]
        return value

    def generate(data, name):
        source = output / (name + '.json')
        source.write_text(json.dumps(data))
        destination = output / name
        subprocess.run([sys.executable, '-B', str(ROOT / 'tools/orion-tokens/gen_tokens.py'),
                        '--input', str(source), '--output-dir', str(destination)], check=True)
        return {path.name: path.read_bytes() for path in destination.iterdir()}

    first = generate(original, 'first')
    assert first == generate(original, 'second') == generate(reverse_keys(original), 'reordered')
    assert b'public const float ZoneHandFolioAboveMeters = 0.07f;' in first['OrionTokens.g.cs']
    assert b'public const float StrokeHandInflateMeters = 0.0015f;' in first['OrionTokens.g.cs']
    original['motion']['pullout.stagger']['staggerSeconds'] = 0.09
    assert first['OrionTokens.g.cs'] != generate(original, 'changed')['OrionTokens.g.cs']
    print('PASS generator: deterministic bytes, key-order independence, meter conversion, stale output detection')
