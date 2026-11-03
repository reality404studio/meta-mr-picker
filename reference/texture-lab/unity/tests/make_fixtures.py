#!/usr/bin/env python3
"""C# 포팅 검증용 입력과 기대값을 만든다. (Unity Assets 에 넣지 않는다)
  python3 make_fixtures.py clip.mov photo.jpg fixtures/
→ fixtures/gray.f32 (N×4096 float32), fixtures/tile.rgba (256×256×4 uint8), fixtures/expected.json
"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '..', 'tools'))
import texlab as tl

clip, photo, out = sys.argv[1], sys.argv[2], sys.argv[3]
os.makedirs(out, exist_ok=True)
frames = tl.read_frames(clip, 256, 30, 6)
gray = np.stack([tl.luma(f[::4, ::4]).ravel() for f in frames]).astype(np.float32)
gray.tofile(os.path.join(out, 'gray.f32'))
D = tl.diff_matrix(gray)
r = tl.find_loop(D, 30, 1.0, 3.0, 8)

img = tl.read_frames(photo, 256, still=True)[0]
u8 = np.clip(np.round(img * 255), 0, 255).astype(np.uint8)
rgba = np.concatenate([u8, np.full(u8.shape[:2] + (1,), 255, np.uint8)], -1)
rgba.tofile(os.path.join(out, 'tile.rgba'))
per = tl.periodic(u8.astype(np.float32))
per8 = np.clip(np.round(per), 0, 255).astype(np.uint8)
exp = {
    'frames': int(len(gray)), 'pixels': int(gray.shape[1]),
    'loop': {'start': r['i'], 'end': r['j'], 'score': r['score'], 'baseline': r['base']},
    'tile': {'n': 256, 'seamBefore': tl.seam_error(u8.astype(np.float32)), 'seamAfter': tl.seam_error(per8.astype(np.float32)),
             'checksum': int(per8.astype(np.int64).sum())},
}
json.dump(exp, open(os.path.join(out, 'expected.json'), 'w'), indent=2)
print(json.dumps(exp, indent=2))
