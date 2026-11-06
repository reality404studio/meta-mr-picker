import argparse
import json
import re
from decimal import Decimal
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


def identifier(path):
    return ''.join(part[0].upper() + part[1:] for part in re.split(r'[._]', path) if part)


def number(value):
    return format(value, 'f') + 'f'


def generate(data):
    lines = ['// 자동 생성 · 사용: python3 tools/orion-tokens/gen_tokens.py',
             'using System;', 'using System.Globalization;', 'using UnityEngine;', '',
             'namespace MoodPicker.Tokens', '{', '    public static class OrionTokens', '    {']
    names = set()

    def constant(kind, name, value):
        if name in names:
            raise ValueError('Duplicate C# identifier: ' + name)
        names.add(name)
        lines.append(f'        public const {kind} {name} = {value};')

    def visit(path, value, suffix=''):
        name = identifier(path) + suffix
        if isinstance(value, dict):
            for key in sorted(value):
                visit(path + '.' + key if path else key, value[key])
        elif isinstance(value, list):
            for index, item in enumerate(value):
                visit(path, item, str(index))
        elif isinstance(value, bool):
            constant('bool', name, str(value).lower())
        elif isinstance(value, (int, Decimal)):
            constant('float', name, number(value))
            for unit, divisor in [('Mm', 1000), ('Cm', 100)]:
                if identifier(path).endswith(unit):
                    constant('float', identifier(path)[:-2] + 'Meters' + suffix,
                             number(Decimal(value) / divisor))
        elif isinstance(value, str):
            if re.fullmatch(r'#[0-9a-fA-F]{6}', value):
                constant('string', name + 'Hex', json.dumps(value))
                for channel, start in zip('RGB', (1, 3, 5)):
                    constant('float', name + channel, f'{int(value[start:start+2], 16)}f / 255f')
            else:
                constant('string', name, json.dumps(value, ensure_ascii=False))
        else:
            raise ValueError('Unsupported token: ' + path)

    visit('', data)
    lines.extend(['', '        public static Color ToColor(string hex)', '        {',
                  '            if (hex == null || hex.Length != 7 || hex[0] != \'#\' ||',
                  '                !uint.TryParse(hex.Substring(1), NumberStyles.AllowHexSpecifier,',
                  '                    CultureInfo.InvariantCulture, out uint rgb))',
                  '                throw new ArgumentException("Expected #RRGGBB.", nameof(hex));',
                  '            return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);',
                  '        }', '    }', '}', ''])
    return '\n'.join(lines)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--input', type=Path, default=ROOT / 'design/tokens/orion.tokens.json')
    parser.add_argument('--output-dir', type=Path, default=ROOT / 'Assets/Scripts/Tokens')
    args = parser.parse_args()
    data = json.loads(args.input.read_text(encoding='utf-8'), parse_float=Decimal)
    source = generate(data)
    definition = {'name': 'MoodPicker.Tokens', 'rootNamespace': 'MoodPicker.Tokens',
                  'references': [], 'autoReferenced': True, 'noEngineReferences': False}
    args.output_dir.mkdir(parents=True, exist_ok=True)
    (args.output_dir / 'OrionTokens.g.cs').write_bytes(source.encode('utf-8'))
    (args.output_dir / 'MoodPicker.Tokens.asmdef').write_bytes(
        (json.dumps(definition, indent=4) + '\n').encode('utf-8'))


if __name__ == '__main__':
    main()
