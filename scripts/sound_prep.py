#!/usr/bin/env python3
"""Time Ritual 9단계 소리 후보 만들기.

사용: python3 -I scripts/sound_prep.py <sound 폴더> <출력 폴더>
원본은 건드리지 않고 48kHz / 16bit / mono WAV 후보와 manifest.json 을 출력 폴더에 쓴다.
구간은 파형 분석(에너지·어택)으로 고른다. 청음으로 고른 것이 아니다.
"""
import json
import math
import os
import subprocess
import sys
import wave

import numpy as np

SR = 48000
FF = "/opt/homebrew/bin/ffmpeg"

SRC = {
    "sand": ("interaction_sounds/Unity_WAV/01_PULL_Sand__nicoproson__CC0.wav", "CC0"),
    "ping": ("interaction_sounds/Unity_WAV/02_SLICE_Glass_Ping__gristi__CC0.wav", "CC0"),
    "sugar": ("interaction_sounds/Unity_WAV/04_POUR_Sugar__davahey__CC0.wav", "CC0"),
    "glitter": ("seal_sounds/01_Jewel_Glitter__opticaillusions__CC0.wav", "CC0"),
    "shimmer": ("seal_sounds/02_Shimmering_Object_2__opticaillusions__CC0.wav", "CC0"),
    "chimes": ("seal_sounds/03_Crystal_Chime_Transitions__newlocknew__CC0.mp3", "CC0 (제작자가 설명에서 크레딧 요청)"),
    "sparkle": ("seal_sounds/04_Achievement_Sparkle__SkySpeira__CC0.wav", "CC0"),
    "harp": ("seal_sounds/05_Glass_Harp_C__InspectorJ__CC-BY-4.0.wav", "CC BY 4.0"),
    "pourdrop": ("seal_sounds/07_Pour_Water_End_Drop__DenysFontanarosa__CC0.wav", "CC0"),
    "drop": ("seal_sounds/08_Water_Drop_One_Shot__beskhu__CC0.aiff", "CC0"),
    "dropui": ("seal_sounds/09_Water_Drop_Notification_8__AleXZavesa__CC-BY-4.0.wav", "CC BY 4.0"),
    "cymbal": ("seal_sounds/10_Finger_Cymbals_3__Joseph_SARDIN__CC0.wav", "CC0"),
}


def decode(path):
    raw = subprocess.run([FF, "-v", "error", "-i", path, "-f", "f32le", "-ac", "1", "-ar", str(SR), "pipe:1"],
                         check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32).astype(np.float64)


def pitch(x, ratio):
    if abs(ratio - 1.0) < 1e-6:
        return x.copy()
    raw = subprocess.run([FF, "-v", "error", "-f", "f32le", "-ar", str(SR), "-ac", "1", "-i", "pipe:0",
                          "-af", f"asetrate={SR * ratio:.3f},aresample={SR}", "-f", "f32le", "pipe:1"],
                         input=x.astype(np.float32).tobytes(), check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32).astype(np.float64)


def env(x, win=0.02):
    n = max(1, int(SR * win))
    k = len(x) // n
    if k == 0:
        return np.zeros(1)
    return np.sqrt(np.mean(x[:k * n].reshape(k, n) ** 2, axis=1))


def fade(x, fin=0.005, fout=0.05):
    y = x.copy()
    a = min(len(y), int(SR * fin))
    b = min(len(y), int(SR * fout))
    if a > 1:
        y[:a] *= np.sin(np.linspace(0, math.pi / 2, a)) ** 2
    if b > 1:
        y[-b:] *= np.cos(np.linspace(0, math.pi / 2, b)) ** 2
    return y


def norm(x, db):
    peak = float(np.max(np.abs(x))) if len(x) else 0.0
    if peak < 1e-9:
        return x, 0.0
    g = 10 ** (db / 20.0) / peak
    return x * g, 20 * math.log10(g)


def cut(x, start, seconds):
    a = max(0, int(start * SR))
    return x[a:a + int(seconds * SR)]


def onset(x, floor_db=-40.0):
    e = env(x, 0.005)
    if not len(e):
        return 0.0
    thr = max(e.max() * 10 ** (floor_db / 20.0), 1e-6)
    idx = np.argmax(e > thr)
    return max(0.0, idx * 0.005 - 0.01)


def events(x, win=0.02, rise=4.0, gap=0.25):
    e = env(x, win)
    noise = np.percentile(e, 20) + 1e-7
    hot = e > noise * rise
    out = []
    i = 0
    quiet = int(gap / win)
    while i < len(hot):
        if hot[i]:
            j = i
            last = i
            while j < len(hot) and j - last <= quiet:
                if hot[j]:
                    last = j
                j += 1
            out.append((i * win, (last + 1) * win, float(e[i:last + 1].max())))
            i = j
        else:
            i += 1
    return out


def attacks(x, win=0.01, ratio=3.0, apart=1.2):
    e = env(x, win)
    noise = np.percentile(e, 20) + 1e-7
    back = int(0.15 / win)
    out = []
    for i in range(back, len(e)):
        before = e[i - back:i].mean() + noise
        if e[i] > before * ratio and e[i] > noise * 6.0 and (not out or i * win - out[-1] >= apart):
            out.append(i * win)
    return out


def steadiest(x, seconds, step=0.25):
    e = env(x, 0.02)
    n = int(seconds / 0.02)
    med = np.median(e)
    best = None
    for i in range(0, max(1, len(e) - n), int(step / 0.02)):
        w = e[i:i + n]
        if w.mean() < med:
            continue
        cv = w.std() / (w.mean() + 1e-9)
        if best is None or cv < best[0]:
            best = (cv, i * 0.02)
    return best[1] if best else 0.0


def loudest(x, seconds, count, apart):
    e = env(x, 0.02)
    n = int(seconds / 0.02)
    score = np.array([e[i:i + n].mean() for i in range(0, max(1, len(e) - n))])
    picks = []
    order = np.argsort(-score)
    for i in order:
        t = i * 0.02
        if all(abs(t - p) >= apart for p in picks):
            picks.append(t)
        if len(picks) == count:
            break
    return sorted(picks)


def loop(x, start, seconds, xf=0.3):
    n = int(round(seconds * SR))
    k = int(round(xf * SR))
    a = max(0, int(start * SR))
    body = x[a:a + n + k]
    k = min(k, max(0, len(body) - n))
    out = body[:n].copy()
    t = np.linspace(0, math.pi / 2, k)
    out[:k] = body[:k] * np.sin(t) + body[n:n + k] * np.cos(t)
    return out


def peak_hz(x, lo=120.0, hi=6000.0):
    n = min(len(x), SR)
    seg = x[:n] * np.hanning(n)
    spec = np.abs(np.fft.rfft(seg, 1 << 17))
    freqs = np.fft.rfftfreq(1 << 17, 1.0 / SR)
    mask = (freqs >= lo) & (freqs <= hi)
    return float(freqs[mask][np.argmax(spec[mask])])


def mix(parts):
    n = max(off + len(p) for p, off, _ in parts)
    out = np.zeros(n)
    for p, off, gain in parts:
        out[off:off + len(p)] += p * gain
    return out


def write(path, x):
    y = np.clip(x, -1.0, 1.0)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((y * 32767.0).astype("<i2").tobytes())


def main():
    root, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    src = {k: decode(os.path.join(root, v[0])) for k, v in SRC.items()}
    items = []

    def emit(name, x, use, sources, how, level=-3.0):
        y, gain = norm(x, level)
        write(os.path.join(out, name + ".wav"), y)
        e = env(y, 0.02)
        items.append({
            "file": name + ".wav", "use": use, "seconds": round(len(y) / SR, 3),
            "sources": [{"key": k, "file": SRC[k][0], "license": SRC[k][1]} for k in sources],
            "license": "CC BY 4.0 (출처 표기)" if any("BY" in SRC[k][1] for k in sources) else ("CC0 + 제작자 크레딧 권장" if "chimes" in sources else "CC0"),
            "how": how, "gain_db": round(gain, 1), "peak_dbfs": level,
            "rms_dbfs": round(20 * math.log10(float(np.sqrt(np.mean(y ** 2))) + 1e-9), 1),
            "noise_floor_dbfs": round(20 * math.log10(float(np.percentile(e, 10)) + 1e-9), 1),
        })

    c = src["cymbal"]
    t0 = onset(c)
    emit("clock_touch_cymbal", fade(cut(c, t0, 4.5), 0.003, 2.2), "시계를 처음 건드렸을 때", ["cymbal"], f"어택 {t0:.3f}s 부터 4.5s, 끝 2.2s 페이드")
    emit("clock_touch_cymbal_short", fade(cut(c, t0, 1.6), 0.003, 0.9), "시계를 처음 건드렸을 때 (짧은 판)", ["cymbal"], f"어택 {t0:.3f}s 부터 1.6s")

    s = src["shimmer"]
    for i, t in enumerate(loudest(s, 1.4, 3, 2.0), 1):
        emit(f"pull_warm_shimmer_{i}", fade(cut(s, t, 1.4), 0.03, 0.45), "빨강(play)·노랑(drift) 조각을 뽑을 때", ["shimmer"], f"에너지가 큰 1.4s 구간, 시작 {t:.2f}s")

    ch = src["chimes"]
    hits = attacks(ch)
    for i, a in enumerate(hits[:24], 1):
        nxt = hits[i] if i < len(hits) else len(ch) / SR
        seg = cut(ch, max(0.0, a - 0.01), min(max(0.3, nxt - a - 0.03), 2.0))
        emit(f"touch_warm_chime_{i:02d}", fade(seg, 0.003, 0.3), "빨강(play)·노랑(drift) 조각을 건드릴 때 (변형 중 고르기)", ["chimes"], f"어택으로 나눈 {i}번째 차임, {a:.2f}s 부터 {len(seg) / SR:.2f}s")

    p = src["ping"]
    t0 = onset(p)
    ping = cut(p, t0, 1.3)
    emit("obj_warm_ping", fade(ping, 0.002, 0.4), "빨강·노랑 오브제에서 나는 유리 핑", ["ping"], f"어택 {t0:.3f}s 부터 1.3s")
    emit("shelf_glass_touch", fade(pitch(ping, 0.7), 0.002, 0.6), "선반에 병이 닿는 소리 후보 (명세: 유리 닿는 소리로 닫기)", ["ping"], "유리 핑을 0.7배 속도로 낮춤 (약 -6반음, 길이 1.43배)", -6.0)

    w = src["pourdrop"]
    tail = max(0.0, len(w) / SR - 2.2)
    dv = [e for e in events(w[int(tail * SR):], win=0.01, rise=6.0, gap=0.12)]
    dv = sorted(dv, key=lambda e: -e[2])[:2]
    drops = []
    for i, (a, b, _) in enumerate(sorted(dv), 1):
        seg = fade(cut(w, tail + max(0.0, a - 0.01), 0.7), 0.002, 0.3)
        drops.append(seg)
        emit(f"pull_cool_drop_{i}", seg, "물방울(move)·달(sleep)·불가사리(work) 조각을 뽑을 때", ["pourdrop"], f"끝 2.2s 안의 물방울 어택 {tail + a:.2f}s 부터 0.7s")
    emit("pull_cool_pour_end", fade(cut(w, tail, 2.2), 0.05, 0.4), "물방울 계열을 뽑을 때 (붓기 끝 + 물방울 통째)", ["pourdrop"], f"끝 2.2s ({tail:.2f}s~)")

    d = src["drop"]
    t0 = onset(d)
    one = fade(cut(d, t0, 0.36), 0.001, 0.12)
    emit("drop_layer", one, "물방울 파티클에 겹칠 단발", ["drop"], f"어택 {t0:.3f}s 부터 0.36s")
    if drops:
        emit("pull_cool_drop_layered", mix([(drops[0], 0, 1.0), (pitch(one, 0.8), int(0.07 * SR), 0.4)]),
             "물방울 계열을 뽑을 때 (물방울 + 낮춘 단발을 70ms 뒤에 겹침)", ["pourdrop", "drop"], "pull_cool_drop_1 + drop_layer 0.8배 속도, -8dB, 70ms 지연")

    u = src["dropui"]
    t0 = onset(u)
    emit("touch_cool_drop", fade(cut(u, t0, 1.2), 0.002, 0.35), "물방울(move)·달(sleep)·불가사리(work) 조각을 건드릴 때", ["dropui"], f"어택 {t0:.3f}s 부터 1.2s")

    sd = src["sand"]
    t = steadiest(sd, 2.3)
    emit("hand_sand_rustle_loop", loop(sd, t, 2.0), "손이 알갱이 속에 있을 때 사각거림 (루프, 손 속도로 음량·피치)", ["sand"], f"가장 고른 구간 {t:.2f}s 부터 2.0s, 0.3s 교차 페이드로 이음매 없앰", -9.0)
    for i, t in enumerate(loudest(sd, 0.4, 3, 3.0), 1):
        emit(f"hand_sand_burst_{i}", fade(cut(sd, t, 0.4), 0.01, 0.18), "빠르게 휘저어 흩어질 때·다리가 끊어질 때", ["sand"], f"에너지가 큰 0.4s 구간, 시작 {t:.2f}s", -6.0)

    sg = src["sugar"]
    t = steadiest(sg, 3.3)
    emit("pour_bed_sugar_loop", loop(sg, t, 3.0), "붓는 동안 가장 바닥에 깔리는 소리 (루프, 찰수록 피치 올림)", ["sugar"], f"가장 고른 구간 {t:.2f}s 부터 3.0s, 0.3s 교차 페이드", -9.0)
    ev = events(sg, rise=3.0, gap=0.5)
    end = ev[-1][1] if ev else len(sg) / SR
    emit("pour_bed_sugar_tail", fade(cut(sg, max(0.0, end - 1.4), 1.6), 0.05, 0.5), "붓기가 끝날 때 잦아드는 꼬리", ["sugar"], f"마지막 입자 흐름의 끝 {end:.2f}s 앞 1.4s", -9.0)

    sp = src["sparkle"]
    t0 = onset(sp)
    emit("seal_press_sparkle", fade(cut(sp, t0, 1.7), 0.003, 0.4), "엄지로 꾹 눌러 밀랍이 눌렸을 때", ["sparkle"], f"어택 {t0:.3f}s 부터 끝까지. 원본이 작아(피크 -27dBFS) 크게 올림 → 바닥 소음 확인 필요", -4.0)

    g = src["glitter"]
    for i, t in enumerate(loudest(g, 5.0, 2, 8.0), 1):
        emit(f"seal_bloom_glitter_{i}", fade(cut(g, t, 5.0), 0.25, 1.8), "봉인 Bloom 5초 동안의 별가루 (작고 신비롭게: 음량은 엔진에서 낮게)", ["glitter"], f"에너지가 큰 5.0s 구간, 시작 {t:.2f}s. 원본이 작아 크게 올림", -6.0)

    h = src["harp"]
    f0 = peak_hz(cut(h, 0.5, 1.0))
    low1 = pitch(h, 0.5)
    low2 = pitch(h, 0.25)
    low5 = pitch(h, 0.375)
    emit("seal_afterglow_harp_oct1", fade(cut(low1, 0.0, 10.0), 0.8, 4.5), "봉인이 끝난 뒤 여운 (한 옥타브 낮춤)", ["harp"], f"원본 주 성분 약 {f0:.0f}Hz → 0.5배 속도 ({f0 * 0.5:.0f}Hz), 10s", -9.0)
    emit("seal_afterglow_harp_oct2", fade(cut(low2, 0.0, 12.0), 1.2, 6.0), "봉인이 끝난 뒤 여운 (두 옥타브 낮춤)", ["harp"], f"0.25배 속도 ({f0 * 0.25:.0f}Hz), 12s", -9.0)
    low3 = pitch(cut(h, 0.0, 2.0), 0.125)
    emit("seal_afterglow_harp_oct3", fade(cut(low3, 0.0, 12.0), 1.5, 6.0), "봉인이 끝난 뒤 여운 (세 옥타브 낮춤, 가장 낮음)", ["harp"], f"0.125배 속도 ({f0 * 0.125:.0f}Hz), 원본 앞 2s 를 늘려 12s", -9.0)
    stack = mix([(cut(low2, 0.0, 12.0), 0, 1.0), (cut(low5, 0.0, 12.0), int(0.35 * SR), 0.6), (cut(low1, 0.0, 10.0), int(0.9 * SR), 0.35)])
    emit("seal_afterglow_harp_open5", fade(stack[:int(12.0 * SR)], 1.2, 6.0), "봉인이 끝난 뒤 여운 (낮은 근음 + 5도 + 옥타브, 차례로 들어옴)", ["harp"],
         f"0.25배({f0 * 0.25:.0f}Hz) + 0.375배({f0 * 0.375:.0f}Hz, 0.35s 뒤, -4dB) + 0.5배({f0 * 0.5:.0f}Hz, 0.9s 뒤, -9dB)", -9.0)

    root_hz = f0 * 0.5
    penta = {"C": 1.0, "D": 9 / 8, "E": 5 / 4, "G": 3 / 2, "A": 5 / 3}
    plan = [("sleep", "C", "drop"), ("move", "G", "drop"), ("work", "E", "drop"), ("drift", "A", "ping"), ("play", "D", "ping")]
    bases = {"drop": one, "ping": fade(ping, 0.002, 0.4)}
    for cat, degree, key in plan:
        base = bases[key]
        hz = peak_hz(base)
        target = root_hz * penta[degree]
        while target / hz > 1.5:
            target /= 2.0
        while target / hz < 0.67:
            target *= 2.0
        emit(f"note_{cat}_{degree}", fade(pitch(base, target / hz), 0.002, 0.2), f"{cat} 층이 병에 쌓일 때의 음 (5음 음계 {degree}, 여운의 근음과 맞춤)", [key],
             f"주 성분 추정 {hz:.0f}Hz → {target:.0f}Hz (속도 {target / hz:.3f}배). 추정 조율이라 청음 필요", -6.0)

    json.dump({"rate": SR, "bits": 16, "channels": 1, "harp_peak_hz": round(f0, 1), "note_root_hz": round(root_hz, 1),
               "method": "파형 분석(에너지·어택·고른 정도)으로 구간 선택, mono, 48kHz, 페이드, 피크 정규화. 청음 선별 아님.", "items": items},
              open(os.path.join(out, "manifest.json"), "w"), ensure_ascii=False, indent=1)
    for it in items:
        print(f"{it['file']:34s} {it['seconds']:6.2f}s rms {it['rms_dbfs']:6.1f} noise {it['noise_floor_dbfs']:6.1f} gain {it['gain_db']:+5.1f}dB  {it['license']}")


if __name__ == "__main__":
    main()
