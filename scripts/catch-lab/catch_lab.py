"""catch-lab: 테스트 영상 크롭으로 CelExtract · 이음매 없는 타일 · 루프 이음매를 Mac 에서 미리 맞춘다.

사용: python3 -I catch_lab.py <영상> <출력 폴더> [--t 5.0] [--crop x,y,size]
출력: still.png(원본 크롭) ramp.png tile_raw.png tile_seamless.png loop_sheet.png loop_seam.png
      paint.json(CelExtract 파라미터) metrics.json hashes.txt
"""
import argparse
import hashlib
import json
import os
import subprocess
import sys

import numpy as np
from PIL import Image, ImageDraw

SAMPLE_GRID = 64
K = 3
KMEANS_ITERS = 20
BORDER_FRAC = 0.125
LIGHT_GAIN = 2.0
LOOP_SECONDS = 2.0
LOOP_FPS = 12
LOOP_FADE = 6
SHEET_COLS = 6
SHEET_CELL = 160


def grab(video, t, crop, n=1, fps=None):
    x, y, s = crop
    vf = f"crop={s}:{s}:{x}:{y}"
    if fps:
        vf = f"fps={fps}," + vf
    cmd = ["ffmpeg", "-v", "error", "-ss", f"{t}", "-i", video, "-vf", vf,
           "-frames:v", str(n), "-f", "rawvideo", "-pix_fmt", "rgb24", "-"]
    raw = subprocess.run(cmd, check=True, capture_output=True).stdout
    arr = np.frombuffer(raw, dtype=np.uint8)
    got = arr.size // (s * s * 3)
    if got < n:
        sys.exit(f"프레임 {got}/{n} 개만 나옴")
    return arr[: n * s * s * 3].reshape(n, s, s, 3)


def luminance(rgb):
    return rgb[..., 0] * 0.2126 + rgb[..., 1] * 0.7152 + rgb[..., 2] * 0.0722


def sample_grid(img):
    h, w, _ = img.shape
    ys = ((np.arange(SAMPLE_GRID) + 0.5) * h / SAMPLE_GRID).astype(int)
    xs = ((np.arange(SAMPLE_GRID) + 0.5) * w / SAMPLE_GRID).astype(int)
    return img[ys][:, xs].astype(np.float64) / 255.0, ys, xs


def kmeans(px):
    flat = px.reshape(-1, 3)
    lum = luminance(flat)
    order = np.argsort(lum, kind="stable")
    centers = np.stack([flat[order[int(q * (len(order) - 1))]] for q in (0.1, 0.5, 0.9)])
    labels = np.zeros(len(flat), dtype=int)
    for _ in range(KMEANS_ITERS):
        d = ((flat[:, None, :] - centers[None, :, :]) ** 2).sum(-1)
        labels = d.argmin(1)
        for k in range(K):
            m = labels == k
            if m.any():
                centers[k] = flat[m].mean(0)
    rank = np.argsort(luminance(centers), kind="stable")
    centers = centers[rank]
    remap = np.empty(K, dtype=int)
    remap[rank] = np.arange(K)
    return centers, remap[labels].reshape(px.shape[:2])


def light_dir(labels):
    n = labels.shape[0]
    coords = (np.arange(n) + 0.5) / n * 2.0 - 1.0
    yy, xx = np.meshgrid(coords, coords, indexing="ij")
    bright = labels == K - 1
    if not bright.any():
        return [0.0, 0.0, -1.0], 0.0, 0.0
    cx = float(xx[bright].mean())
    cy = float(yy[bright].mean())
    v = np.array([cx * LIGHT_GAIN, -cy * LIGHT_GAIN, -1.0])
    v /= np.linalg.norm(v)
    return [round(float(c), 4) for c in v], cx, cy


def hexrgb(c):
    r, g, b = (int(round(float(v) * 255)) for v in c)
    return f"#{r:02X}{g:02X}{b:02X}"


def overlap_band(s):
    return max(1, int(round(s * BORDER_FRAC)))


def seamless(big, s):
    b = overlap_band(s)
    f = big[: s + b, : s + b].astype(np.float64)
    a = (np.arange(b) / b)
    a = a * a * (3 - 2 * a)
    x = f[:, :s].copy()
    x[:, :b] = f[:, :b] * a[None, :, None] + f[:, s:s + b] * (1 - a[None, :, None])
    y = x[:s].copy()
    y[:b] = x[:b] * a[:, None, None] + x[s:s + b] * (1 - a[:, None, None])
    return np.clip(np.rint(y), 0, 255).astype(np.uint8)


def seam_ratio(img):
    f = img.astype(np.float64)
    interior = (np.abs(np.diff(f, axis=1)).mean() + np.abs(np.diff(f, axis=0)).mean()) / 2
    wrap = (np.abs(f[:, 0] - f[:, -1]).mean() + np.abs(f[0] - f[-1]).mean()) / 2
    return float(wrap / max(interior, 1e-9))


def tile2(img):
    return np.tile(img, (2, 2, 1))


def loop_frames(frames):
    n = len(frames) - LOOP_FADE
    f = frames.astype(np.float64)
    out = f[:n].copy()
    for i in range(LOOP_FADE):
        a = i / LOOP_FADE
        out[i] = f[i] * a + f[n + i] * (1 - a)
    return np.clip(np.rint(out), 0, 255).astype(np.uint8)


def loop_ratio(seq):
    f = seq.astype(np.float64)
    steps = [np.abs(f[i + 1] - f[i]).mean() for i in range(len(f) - 1)]
    wrap = np.abs(f[0] - f[-1]).mean()
    return float(wrap / max(np.mean(steps), 1e-9))


def sheet(seq):
    n = len(seq)
    rows = (n + SHEET_COLS - 1) // SHEET_COLS
    canvas = Image.new("RGB", (SHEET_COLS * SHEET_CELL, rows * SHEET_CELL))
    for i, fr in enumerate(seq):
        im = Image.fromarray(fr).resize((SHEET_CELL, SHEET_CELL), Image.Resampling.BOX)
        canvas.paste(im, ((i % SHEET_COLS) * SHEET_CELL, (i // SHEET_COLS) * SHEET_CELL))
    return canvas


def ramp_image(still, centers, labels, ld, cx, cy):
    s = 256
    canvas = Image.new("RGB", (s * 3, s + 60), (240, 238, 230))
    canvas.paste(Image.fromarray(still).resize((s, s), Image.Resampling.BOX), (0, 0))
    lab = (centers[labels] * 255).astype(np.uint8)
    canvas.paste(Image.fromarray(lab).resize((s, s), Image.Resampling.NEAREST), (s, 0))
    dr = ImageDraw.Draw(canvas)
    for k in range(K):
        dr.rectangle([2 * s, k * s // K, 3 * s, (k + 1) * s // K], fill=tuple(int(v * 255) for v in centers[K - 1 - k]))
    c = (s * 1.5, s * 0.5)
    tip = (c[0] + cx * s * 0.5, c[1] + cy * s * 0.5)
    dr.line([c, tip], fill=(213, 42, 44), width=4)
    dr.ellipse([tip[0] - 6, tip[1] - 6, tip[0] + 6, tip[1] + 6], outline=(213, 42, 44), width=3)
    dr.text((8, s + 8), f"light {hexrgb(centers[-1])}  mid {hexrgb(centers[1])}  shadow {hexrgb(centers[0])}", fill=(21, 49, 124))
    dr.text((8, s + 30), f"lightDir(view) {ld}   still | k={K} labels | tones", fill=(21, 49, 124))
    return canvas


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("video")
    ap.add_argument("out")
    ap.add_argument("--t", type=float, default=5.0)
    ap.add_argument("--crop", default="720,560,480")
    a = ap.parse_args()
    crop = tuple(int(v) for v in a.crop.split(","))
    os.makedirs(a.out, exist_ok=True)

    s = crop[2]
    b = overlap_band(s)
    big = grab(a.video, a.t, (crop[0], crop[1], s + b))[0]
    still = np.ascontiguousarray(big[:s, :s])
    px, _, _ = sample_grid(still)
    centers, labels = kmeans(px)
    ld, cx, cy = light_dir(labels)
    tile = seamless(big, s)

    nloop = int(LOOP_SECONDS * LOOP_FPS)
    frames = grab(a.video, a.t, crop, n=nloop + LOOP_FADE, fps=LOOP_FPS)
    raw_seq = frames[:nloop]
    seq = loop_frames(frames)

    Image.fromarray(still).save(os.path.join(a.out, "still.png"))
    ramp_image(still, centers, labels, ld, cx, cy).save(os.path.join(a.out, "ramp.png"))
    Image.fromarray(tile2(still)).save(os.path.join(a.out, "tile_raw.png"))
    Image.fromarray(tile2(tile)).save(os.path.join(a.out, "tile_seamless.png"))
    Image.fromarray(tile).save(os.path.join(a.out, "still_seamless.png"))
    sheet(seq).save(os.path.join(a.out, "loop_sheet.png"))
    pair = np.concatenate([np.concatenate([raw_seq[-1], raw_seq[0]], 1), np.concatenate([seq[-1], seq[0]], 1)], 0)
    Image.fromarray(pair).resize((pair.shape[1] // 2, pair.shape[0] // 2), Image.Resampling.BOX).save(os.path.join(a.out, "loop_seam.png"))

    interior_kept = float((np.abs(tile.astype(int) - still.astype(int)).max(-1) == 0)[b:, b:].mean())
    metrics = {
        "source": os.path.basename(a.video), "t": a.t, "crop": list(crop),
        "tile_seam_ratio_raw": round(seam_ratio(still), 3),
        "tile_seam_ratio_seamless": round(seam_ratio(tile), 3),
        "tile_interior_unchanged": round(interior_kept, 4),
        "tile_overlap_px": b,
        "loop_seam_ratio_raw": round(loop_ratio(raw_seq), 3),
        "loop_seam_ratio_crossfade": round(loop_ratio(seq), 3),
        "loop_frames": int(len(seq)), "loop_fps": LOOP_FPS,
    }
    paint = {
        "mode": "snap",
        "lightTone": hexrgb(centers[-1]), "midTone": hexrgb(centers[1]), "shadowTone": hexrgb(centers[0]),
        "lightDirView": ld,
        "params": {"sampleGrid": SAMPLE_GRID, "k": K, "iters": KMEANS_ITERS, "lightGain": LIGHT_GAIN, "overlapFrac": BORDER_FRAC},
    }
    with open(os.path.join(a.out, "metrics.json"), "w") as fh:
        json.dump(metrics, fh, indent=2, ensure_ascii=False)
    with open(os.path.join(a.out, "paint.json"), "w") as fh:
        json.dump(paint, fh, indent=2, ensure_ascii=False)

    names = sorted(n for n in os.listdir(a.out) if n.endswith((".png", ".json")))
    lines = [f"{hashlib.sha256(open(os.path.join(a.out, n), 'rb').read()).hexdigest()[:16]}  {n}" for n in names]
    with open(os.path.join(a.out, "hashes.txt"), "w") as fh:
        fh.write("\n".join(lines) + "\n")
    print(json.dumps({**metrics, **{k: paint[k] for k in ("lightTone", "midTone", "shadowTone", "lightDirView")}}, ensure_ascii=False))


if __name__ == "__main__":
    main()
