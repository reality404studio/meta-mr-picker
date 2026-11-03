#!/usr/bin/env python3
"""
texlab — 실기 전에 맥에서 루프·타일·특징값을 미리 맞춰 보는 오프라인 도구.
웹 프로토타입(web/pigment-lab)과 같은 알고리즘이다. Unity 포팅(unity/PigmentCore)의 기준 출력으로도 쓴다.

필요: python3, numpy, ffmpeg (PATH)

  python3 texlab.py loop  clip.mov  -o out/            # 최적 루프 → 플립북 PNG + JSON + 미리보기 mp4
  python3 texlab.py tile  photo.jpg -o out/            # 이음매 없는 타일 PNG (찰칵 모드)
  python3 texlab.py stats clip.mov  -o out/            # 특징값 + 셰이더 프롬프트 + 레퍼런스 시트

원본 영상은 git 에 넣지 않는다 (board catch 과업 메모). 결과물만 필요한 곳에 복사한다.
"""
import argparse, json, math, os, subprocess, sys
import numpy as np

# ---------------------------------------------------------------- I/O (ffmpeg)

def probe(path):
    out = subprocess.run(['ffprobe', '-v', 'error', '-select_streams', 'v:0',
                          '-show_entries', 'stream=width,height:format=duration', '-of', 'json', path],
                         capture_output=True, text=True, check=True).stdout
    j = json.loads(out)
    s = j['streams'][0]
    return s['width'], s['height'], float(j.get('format', {}).get('duration', 0) or 0)

def crop_filter(path, crop):
    w, h, _ = probe(path)
    if crop:
        x, y, cw, ch = crop
    else:
        side = min(w, h); x, y, cw, ch = (w - side) // 2, (h - side) // 2, side, side
    return f'crop={cw}:{ch}:{x}:{y}'

def read_frames(path, size, fps=None, max_sec=None, crop=None, still=False):
    """정사각 크롭 → size×size, float32 [0,1] sRGB, shape (N, size, size, 3)"""
    vf = [crop_filter(path, crop)]
    if fps and not still: vf.insert(0, f'fps={fps}')
    vf += [f'scale={size}:{size}:flags=area', 'format=rgb24']
    cmd = ['ffmpeg', '-v', 'error']
    if max_sec and not still: cmd += ['-t', str(max_sec)]
    cmd += ['-i', path, '-vf', ','.join(vf)]
    if still: cmd += ['-frames:v', '1']
    cmd += ['-f', 'rawvideo', '-']
    raw = subprocess.run(cmd, capture_output=True, check=True).stdout
    return (np.frombuffer(raw, np.uint8).reshape(-1, size, size, 3).astype(np.float32) / 255.0)

def write_png(img, path):
    h, w = img.shape[:2]
    data = np.clip(np.round(img * 255), 0, 255).astype(np.uint8).tobytes()
    subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', f'{w}x{h}',
                    '-i', '-', path], input=data, check=True)

def write_mp4(frames, fps, path, repeat=3):
    h, w = frames[0].shape[:2]
    p = subprocess.Popen(['ffmpeg', '-v', 'error', '-y', '-f', 'rawvideo', '-pix_fmt', 'rgb24', '-s', f'{w}x{h}',
                          '-r', f'{fps:.6f}', '-i', '-', '-c:v', 'libx264', '-crf', '18', '-pix_fmt', 'yuv420p',
                          '-movflags', '+faststart', path], stdin=subprocess.PIPE)
    for _ in range(repeat):
        for f in frames:
            p.stdin.write(np.clip(np.round(f * 255), 0, 255).astype(np.uint8).tobytes())
    p.stdin.close(); p.wait()

# ---------------------------------------------------------------- 공통 수학

LUMA = np.array([0.2126, 0.7152, 0.0722], np.float32)
def luma(img): return img @ LUMA

def smoothstep01(t): return t * t * (3 - 2 * t)

def periodic(u):
    """Moisan(2011) 주기+평활 분해의 주기 성분. u: (H,W) 또는 (H,W,C). 가장자리 점프만 흡수한다."""
    if u.ndim == 3:
        return np.stack([periodic(u[..., c]) for c in range(u.shape[2])], -1)
    M, N = u.shape
    v = np.zeros_like(u, dtype=np.float64)
    dx = u[:, -1] - u[:, 0]; v[:, 0] += dx; v[:, -1] -= dx
    dy = u[-1, :] - u[0, :]; v[0, :] += dy; v[-1, :] -= dy
    q = np.arange(M)[:, None]; r = np.arange(N)[None, :]
    den = 2 * np.cos(2 * np.pi * q / M) + 2 * np.cos(2 * np.pi * r / N) - 4
    den[0, 0] = 1.0
    S = np.fft.fft2(v) / den; S[0, 0] = 0
    s = np.real(np.fft.ifft2(S))
    return (u - s).astype(np.float32)

def seam_error(img):
    """랩 경계의 평균 점프 / 내부 이웃 평균 차이. 1 근처면 경계가 내부와 구분 안 됨."""
    n = img.shape[0]
    e = np.abs(img[:, -1] - img[:, 0]).mean() + np.abs(img[-1, :] - img[0, :]).mean()
    b = np.abs(img[:, n // 2] - img[:, n // 2 - 1]).mean() + np.abs(img[n // 2, :] - img[n // 2 - 1, :]).mean()
    return float(e / max(b, 1e-6))

# ---------------------------------------------------------------- 루프 탐색 (웹 pigment-lab 과 동일)

def diff_matrix(gray):
    """gray: (N, P) → D[i,j] = mean |g_i - g_j|"""
    N = len(gray)
    D = np.zeros((N, N), np.float32)
    for i in range(N):
        D[i] = np.abs(gray - gray[i]).mean(1)
    return D

def baseline(D):
    N = len(D)
    return max(1e-4, float(np.median([D[k, k + 1] for k in range(N - 1)])))

def loop_cost(D, i, j, F):
    N = len(D); s = c = 0.0
    for t in range(-1, max(F, 1) + 1):
        a, b = i + t, j + t
        if a < 0 or b >= N: continue
        w = 1.0 if 0 <= t < max(F, 1) else 0.5
        s += D[a, b] * w; c += w
    return s / c

def find_loop(D, fps, min_sec, max_sec, F):
    """전환 j-1 → i 가 가장 덜 티 나는 (i, j). 크로스페이드 F 프레임 구간 전체의 유사도를 본다."""
    N = len(D); base = baseline(D)
    lo = max(round(min_sec * fps), F + 2, 6); hi = max(lo, round(max_sec * fps))
    best = None
    for i in range(1, N):
        for L in range(lo, hi + 1):
            j = i + L
            if j + max(F, 1) > N - 1: break
            c = loop_cost(D, i, j, F) / base - 0.02 * (L / fps)  # 같은 품질이면 긴 루프 선호
            if best is None or c < best[2]: best = (i, j, c)
    forced = best is None
    if forced:  # 클립이 범위보다 짧음: 가능한 가장 긴 것
        j = N - 1 - max(F, 1); i = max(0, j - lo); best = (i, max(i + 2, j), 0)
    i, j, _ = best
    return dict(i=int(i), j=int(j), score=float(loop_cost(D, i, j, F) / base), base=float(base), forced=bool(forced))

def build_loop(frames, i, j, F):
    F = min(F, len(frames) - j)
    out = []
    for t in range(j - i):
        if t < F:
            a = smoothstep01((t + 0.5) / F)
            out.append(frames[j + t] * (1 - a) + frames[i + t] * a)
        else:
            out.append(frames[i + t])
    return out, F

def atlas(frames, max_frames):
    L = len(frames); n = min(L, max_frames)
    idx = [int(t * L / n) for t in range(n)]
    sel = [frames[k] for k in idx]
    cols = math.ceil(math.sqrt(n)); rows = math.ceil(n / cols)
    s = sel[0].shape[0]
    A = np.zeros((rows * s, cols * s, 3), np.float32)
    for k, f in enumerate(sel):  # 행 우선, 왼쪽 위부터
        A[(k // cols) * s:(k // cols + 1) * s, (k % cols) * s:(k % cols + 1) * s] = f
    return A, sel, cols, rows, n

# ---------------------------------------------------------------- 특징값 (프롬프트용)

def texture_stats(frames, fps):
    L = luma(np.stack(frames))  # (N,H,W)
    N, H, W = L.shape
    st = {}
    st['meanRGB'] = [int(round(x)) for x in (np.stack(frames).mean((0, 1, 2)) * 255)]
    p50, p90, p98 = np.percentile(L, [50, 90, 98])
    st['luma'] = dict(p50=float(p50), p90=float(p90), p98=float(p98), max=float(np.percentile(L, 99.8)))
    # 방향성: 공간 스펙트럼 에너지를 가로/세로 주파수 축으로 나눔
    P = (np.abs(np.fft.fftshift(np.fft.fft2(L - L.mean((1, 2), keepdims=True)), axes=(1, 2))) ** 2).mean(0)
    y, x = np.mgrid[-H // 2:H - H // 2, -W // 2:W - W // 2]
    r = np.hypot(x, y); m = (r > 2) & (r < min(H, W) * 0.4); ang = np.arctan2(y, x)
    ex = float((P * np.abs(np.cos(ang)) ** 4 * m).sum()); ey = float((P * np.abs(np.sin(ang)) ** 4 * m).sum())
    st['energyYFreq'] = ey / (ex + ey + 1e-12)   # 1에 가까우면 가로로 누운 띠(세로로 쌓임)
    col = P[:, W // 2][H // 2 + 2:H // 2 + H // 3]; rowp = P[H // 2, :][W // 2 + 2:W // 2 + W // 3]
    st['bandPeriodY'] = float(H / (np.argmax(col) + 2)) / H   # 화면 높이 대비
    st['bandPeriodX'] = float(W / (np.argmax(rowp) + 2)) / W
    # 하이라이트(상위 2%) 모양
    B = L > p98
    def runs(mask2d):
        lens = []
        for row in mask2d:
            d = np.diff(np.concatenate([[0], row.astype(np.int8), [0]]))
            s, e = np.where(d == 1)[0], np.where(d == -1)[0]
            lens += list(e - s)
        return float(np.mean(lens)) if lens else 0.0
    sub = B[::max(1, N // 12)]
    gw = runs(sub.reshape(-1, W)); gh = runs(sub.transpose(0, 2, 1).reshape(-1, H))
    st['glint'] = dict(widthFrac=gw / W, heightFrac=gh / H, aspect=gw / max(gh, 1e-6))
    if N > 4:
        pers = {}
        for d in (1, 2, 4):
            pers[d] = float((B[:-d] & B[d:]).sum() / max(B[:-d].sum(), 1))
        st['glint']['persist'] = pers
        life = 1
        for d in (1, 2, 4):
            if pers[d] > 0.3: life = d
        st['glint']['lifeSec'] = (life + 1) / fps
        lags = [k for k in (1, 2, 3, 4, 6, 8, 12, 16, 24) if k < N]
        curve = {k: float(np.abs(L[:-k] - L[k:]).mean()) for k in lags}
        plateau = np.mean([curve[k] for k in lags if k >= min(8, lags[-1])])
        dec = next((k for k in lags if curve[k] >= 0.9 * plateau), lags[-1])
        st['decorrelateSec'] = dec / fps
        sh = []
        for t in range(N - 1):
            G = np.fft.fft2(L[t]) * np.conj(np.fft.fft2(L[t + 1]))
            c = np.fft.ifft2(G / (np.abs(G) + 1e-9)).real
            iy, ix = np.unravel_index(np.argmax(c), c.shape)
            sh.append([((iy + H // 2) % H) - H // 2, ((ix + W // 2) % W) - W // 2])
        sy, sx = np.median(sh, 0)
        st['driftPerSec'] = dict(x=float(-sx * fps / W), y=float(-sy * fps / H))  # 화면 크기/초
    return st

def prompt_from(st, loop_sec=None):
    L = st['luma']; g = st['glint']
    ey = st['energyYFreq']
    if ey > 0.7:
        struct = f"가로로 길게 누운 띠가 세로로 쌓인 패턴으로 보임 (에너지의 {ey*100:.0f}%가 세로 방향 주파수). 띠 간격은 화면 높이의 약 1/{max(1, round(1/st['bandPeriodY']))}. 노이즈를 가로로 늘려서 사용."
    elif ey < 0.3:
        struct = f"세로로 선 결이 가로로 늘어선 패턴으로 보임 (에너지의 {(1-ey)*100:.0f}%가 가로 방향 주파수). 결 간격은 화면 폭의 약 1/{max(1, round(1/st['bandPeriodX']))}."
    else:
        struct = "뚜렷한 방향성이 약한 등방성 패턴으로 보임."
    lines = [
        "아래 특징을 가진 GLSL 프래그먼트 셰이더를 작성해줘. (uniform: uTime, uv 0~1)",
        "",
        f"[구조] {struct}",
        f"[톤] 중간값 밝기 {L['p50']:.2f}, 90% 지점 {L['p90']:.2f}. 평균색 rgb{tuple(st['meanRGB'])}.",
        f"[하이라이트] 밝기 상위 2%가 {L['p98']:.2f} 이상, 최대 {L['max']:.2f}까지. "
        f"하나당 가로:세로 약 {g['aspect']:.0f}:1, 크기는 화면 폭의 약 {g['widthFrac']*100:.1f}%.",
    ]
    if 'persist' in g:
        d = st['driftPerSec']; speed = math.hypot(d['x'], d['y'])
        move = ("전체 이동(스크롤)은 거의 없음. 제자리에서 일렁이는 것으로 보임" if speed < 0.02
                else f"전체가 초당 화면의 {speed*100:.0f}%만큼 ({'오른쪽' if d['x']>0 else '왼쪽'}{'·아래' if d['y']>0.01 else '·위' if d['y']<-0.01 else ''}) 흐름")
        lines += [
            f"  하이라이트 수명 약 {g['lifeSec']:.2f}초 (1프레임 뒤 {g['persist'][1]*100:.0f}%, 2프레임 뒤 {g['persist'][2]*100:.0f}% 남음).",
            f"[움직임] {move}. 약 {st['decorrelateSec']:.2f}초면 무늬가 완전히 바뀜.",
        ]
        if loop_sec:
            lines.append(f"[루프] {loop_sec:.2f}초 주기로 이음매 없이 반복. 시간 노이즈는 원형 경로(cos/sin)로 샘플링해 루프를 닫을 것.")
    lines += ["[타일] uv 가장자리에서 이음매 없이 반복되도록 주기적 노이즈 사용.", "레퍼런스 프레임 시트 첨부."]
    return "\n".join(lines)

# ---------------------------------------------------------------- 명령

def stem(p): return os.path.splitext(os.path.basename(p))[0]

def cmd_loop(a):
    os.makedirs(a.out, exist_ok=True)
    frames = list(read_frames(a.input, a.tile, a.fps, a.max_sec, a.crop))
    N = len(frames)
    gray = np.stack([luma(f[::a.tile // 64, ::a.tile // 64]).ravel() for f in frames])
    D = diff_matrix(gray)
    r = find_loop(D, a.fps, a.min, a.max, a.fade)
    loop, F = build_loop(frames, r['i'], r['j'], a.fade)
    seam_before = seam_error(loop[0])
    if not a.no_periodic:
        loop = [periodic(f) for f in loop]
    seam_after = seam_error(loop[0])
    A, sel, cols, rows, n = atlas(loop, a.max_frames)
    fps_out = a.fps * n / len(loop)
    name = a.name or stem(a.input)
    png = f'{name}_flipbook_{cols}x{rows}_{n}f_{round(fps_out)}fps.png'
    write_png(A, os.path.join(a.out, png))
    write_mp4(sel, fps_out, os.path.join(a.out, f'{name}_preview.mp4'))
    heat = np.clip(D / (r['base'] * 4), 0, 1) ** 0.7
    write_png(np.repeat(heat[..., None], 3, -1), os.path.join(a.out, f'{name}_similarity.png'))
    meta = {  # HANDOFF §5 물감 데이터의 loop 필드와 같은 키 + 진단값
        'frames': png, 'frameCount': n, 'fps': round(fps_out, 3), 'cols': cols, 'rows': rows,
        'tileSize': a.tile, 'layout': 'row-major, top-left first',
        'loopStartSec': round(r['i'] / a.fps, 3), 'loopEndSec': round(r['j'] / a.fps, 3),
        'crossfadeFrames': F, 'seamScore': round(r['score'], 3), 'seamlessTiling': not a.no_periodic,
        'tileSeam': {'before': round(seam_before, 3), 'after': round(seam_after, 3)},
        'analysisFps': a.fps, 'sourceFrames': N, 'forced': r['forced'],
    }
    with open(os.path.join(a.out, f'{name}_flipbook.json'), 'w') as fh: json.dump(meta, fh, indent=2, ensure_ascii=False)
    print(json.dumps(meta, indent=2, ensure_ascii=False))

def cmd_tile(a):
    os.makedirs(a.out, exist_ok=True)
    img = read_frames(a.input, a.size, crop=a.crop, still=True)[0]
    per = periodic(img)
    name = a.name or stem(a.input)
    write_png(per, os.path.join(a.out, f'{name}_tile_{a.size}.png'))
    # 2×2 로 깔아 경계 확인용
    write_png(np.tile(per, (2, 2, 1)), os.path.join(a.out, f'{name}_tile_{a.size}_check2x2.png'))
    write_png(np.tile(img, (2, 2, 1)), os.path.join(a.out, f'{name}_raw_{a.size}_check2x2.png'))
    res = {'tile': f'{name}_tile_{a.size}.png', 'size': a.size,
           'tileSeam': {'before': round(seam_error(img), 3), 'after': round(seam_error(per), 3)}}
    print(json.dumps(res, indent=2, ensure_ascii=False))

def cmd_stats(a):
    os.makedirs(a.out, exist_ok=True)
    name = a.name or stem(a.input)
    try:
        _, _, dur = probe(a.input)
    except Exception:
        dur = 0
    still = dur == 0 or a.input.lower().endswith(('.jpg', '.jpeg', '.png', '.webp', '.heic'))
    frames = list(read_frames(a.input, 256, a.fps, a.max_sec, a.crop, still=still))
    loop_sec = None
    if len(frames) > 8:
        gray = np.stack([luma(f[::4, ::4]).ravel() for f in frames])
        D = diff_matrix(gray); r = find_loop(D, a.fps, a.min, a.max, a.fade)
        loop_sec = (r['j'] - r['i']) / a.fps
        frames_loop = frames[r['i']:r['j']]
    else:
        frames_loop = frames
    st = texture_stats(frames_loop, a.fps)
    text = prompt_from(st, loop_sec)
    with open(os.path.join(a.out, f'{name}_prompt.txt'), 'w') as fh: fh.write(text + '\n')
    with open(os.path.join(a.out, f'{name}_stats.json'), 'w') as fh: json.dump(st, fh, indent=2, ensure_ascii=False)
    k = min(16, len(frames_loop)); idx = [int(t * len(frames_loop) / k) for t in range(k)]
    small = [f[::2, ::2] for f in (frames_loop[t] for t in idx)]
    cols = math.ceil(math.sqrt(k)); rows = math.ceil(k / cols)
    sheet = np.zeros((rows * 128, cols * 128, 3), np.float32)
    for n, f in enumerate(small):
        sheet[(n // cols) * 128:(n // cols + 1) * 128, (n % cols) * 128:(n % cols + 1) * 128] = f
    write_png(sheet, os.path.join(a.out, f'{name}_reference_sheet.png'))
    print(text)

def parse_crop(s):
    if not s: return None
    v = [int(x) for x in s.split(',')]
    if len(v) != 4: raise argparse.ArgumentTypeError('crop = x,y,w,h')
    return v

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest='cmd', required=True)
    def common(p):
        p.add_argument('input'); p.add_argument('-o', '--out', default='out')
        p.add_argument('--name'); p.add_argument('--crop', type=parse_crop, help='x,y,w,h (기본: 가운데 정사각)')
    p = sub.add_parser('loop', help='최적 루프 → 플립북'); common(p)
    p.add_argument('--fps', type=float, default=30); p.add_argument('--max-sec', type=float, default=6)
    p.add_argument('--min', type=float, default=1.0); p.add_argument('--max', type=float, default=3.0)
    p.add_argument('--fade', type=int, default=8); p.add_argument('--tile', type=int, default=256)
    p.add_argument('--max-frames', type=int, default=64); p.add_argument('--no-periodic', action='store_true')
    p.set_defaults(fn=cmd_loop)
    p = sub.add_parser('tile', help='사진 → 이음매 없는 타일'); common(p)
    p.add_argument('--size', type=int, default=512); p.set_defaults(fn=cmd_tile)
    p = sub.add_parser('stats', help='특징값 + 셰이더 프롬프트'); common(p)
    p.add_argument('--fps', type=float, default=30); p.add_argument('--max-sec', type=float, default=6)
    p.add_argument('--min', type=float, default=1.0); p.add_argument('--max', type=float, default=3.0)
    p.add_argument('--fade', type=int, default=8); p.set_defaults(fn=cmd_stats)
    a = ap.parse_args(); a.fn(a)

if __name__ == '__main__':
    main()
