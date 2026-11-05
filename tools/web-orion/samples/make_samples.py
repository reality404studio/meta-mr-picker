"""Builds tools/web-orion/samples/{orion.export.json, chips/*/tile.png} from real captured chips.

Run from the app repo root (unity/mood-picker):
  python3 tools/web-orion/samples/make_samples.py
Sources: workbench paints (studio/apps/mood-picker/nodes/catch-verify-5047/assets/paints) and the
yunseul tile on branch origin/claude/hopeful-pasteur-9n9kuz. The gradient chip is synthesized here
until the marbling bench exports real ones.
"""
import json
import math
import subprocess
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
CHIPS = OUT / "chips"
PAINTS = ROOT.parents[1] / "studio/apps/mood-picker/nodes/catch-verify-5047/assets/paints"
YUNSEUL_REF = "origin/claude/hopeful-pasteur-9n9kuz:reference/webxr-reaction/assets/yunseul_tile.png"
TILE_PX = 512
LIGHT_DIR = [0.6, 0.65, -0.45]


def hexcolor(rgb):
    return "#%02X%02X%02X" % tuple(int(round(c)) for c in rgb)


def tones(image):
    small = image.convert("RGB").resize((64, 64))
    pixels = sorted(small.getdata(), key=lambda p: 0.2126 * p[0] + 0.7152 * p[1] + 0.0722 * p[2])
    n = len(pixels)

    def mean(sl):
        return [sum(p[i] for p in sl) / len(sl) for i in range(3)]

    return {
        "shadowTone": hexcolor(mean(pixels[: n // 5])),
        "midTone": hexcolor(mean(pixels[n * 2 // 5 : n * 3 // 5])),
        "lightTone": hexcolor(mean(pixels[n * 4 // 5 :])),
    }


def save_tile(image, chip_id):
    folder = CHIPS / chip_id
    folder.mkdir(parents=True, exist_ok=True)
    image = image.convert("RGB")
    if image.size != (TILE_PX, TILE_PX):
        image = image.resize((TILE_PX, TILE_PX), Image.LANCZOS)
    image.save(folder / "tile.png", optimize=True)
    return image


def gradient_tile(stops):
    img = Image.new("RGB", (TILE_PX, TILE_PX))
    px = img.load()
    for y in range(TILE_PX):
        for x in range(TILE_PX):
            t = 0.5 + 0.5 * math.sin((x / TILE_PX) * 2 * math.pi) * 0.6 + 0.4 * math.sin((y / TILE_PX) * 2 * math.pi + 1.3) * 0.5
            t = max(0.0, min(1.0, t))
            seg = t * (len(stops) - 1)
            i = min(int(seg), len(stops) - 2)
            f = seg - i
            px[x, y] = tuple(int(stops[i][c] * (1 - f) + stops[i + 1][c] * f) for c in range(3))
    return img


def main():
    CHIPS.mkdir(parents=True, exist_ok=True)
    yunseul_bytes = subprocess.run(["git", "show", YUNSEUL_REF], cwd=ROOT, check=True, capture_output=True).stdout
    tmp = OUT / ".yunseul.tmp.png"
    tmp.write_bytes(yunseul_bytes)
    yunseul = save_tile(Image.open(tmp), "yunseul")
    tmp.unlink()

    fur_src = PAINTS / "paint_20261104_143524_004"
    fur = save_tile(Image.open(fur_src / "tile.png"), "paint_20261104_143524_004")
    fur_meta = json.loads((fur_src / "paint.json").read_text())

    gradient = save_tile(gradient_tile([(63, 122, 120), (164, 151, 192), (242, 169, 78)]), "gradient_sample")

    def paint(chip_id, image, meta=None):
        p = {"kind": "gradient" if chip_id.startswith("gradient") else "chip", "id": chip_id}
        t = tones(image)
        if meta:
            t = {k: meta[k] for k in ("lightTone", "midTone", "shadowTone")}
        p.update(t)
        p["lightDir"] = meta["lightDir"] if meta else LIGHT_DIR
        p["tile"] = f"chips/{chip_id}/tile.png"
        p["loop"] = None
        return p

    doc = {
        "version": 1,
        "exportedAt": "2026-11-06T03:00:00+09:00",
        "word": {"tracking": 1.0, "thickness": 1.0, "rotationYDeg": 0},
        "letters": [
            {"name": "O1", "offset": [0, 0, 0], "paint": paint("yunseul", yunseul)},
            {"name": "R", "offset": [0, 0, 0], "paint": paint("paint_20261104_143524_004", fur, fur_meta)},
            {"name": "I", "offset": [0, 0, 0], "paint": paint("gradient_sample", gradient)},
            {"name": "O2", "offset": [0, 0, 0], "paint": paint("yunseul", yunseul)},
            {"name": "N", "offset": [0, 0, 0], "paint": paint("gradient_sample", gradient)},
        ],
    }
    (OUT / "orion.export.json").write_text(json.dumps(doc, indent=2) + "\n")
    print("wrote", OUT / "orion.export.json")
    for letter in doc["letters"]:
        print(letter["name"], letter["paint"]["kind"], letter["paint"]["id"], letter["paint"].get("midTone"))


if __name__ == "__main__":
    sys.exit(main())
