#!/usr/bin/env python3
"""Time Ritual 9단계 소리: 사람이 고른 음원만 Unity 로 옮긴다.

사용: python3 -I scripts/sound_stage9.py <sound/prepared_1116> <Assets/Ritual/Audio/Sfx>
선택 근거는 sound/prepared_1116/README_KO.md 에 사람이 적은 메모 (2026-11-17 00:18).
- 빨강·노랑 건드리기: 차임 8번·6번만, "다소 날카롭게 들리므로 조금 무디게" -> 4.5kHz 위 -9dB 셸프, 12kHz 저역통과, 앞 무음 자름(건드림에 바로 반응), 피크 -3dBFS 로 되맞춤
- sfx_gains.json: 파일마다 순간 음량(100ms RMS 최대)을 -18dB 로 맞추는 배율. 엔진이 설정 음량에 곱한다
- 손이 알갱이 속에 있을 때 소리(hand_sand_*)는 쓰지 않는다 -> 건드리기 소리로 통일
- 봉인 여운은 open5 (oct1 은 "너무 거슬림")
- 물방울 계열 합치기 끝소리: 사람이 고른 bubbles (2).wav (11-17 헤드셋 판정 "물방울 합칠때 새로 사운드 추가함. 해당 사운드 편집해서 사용할것")
  -> 네 거품 덩어리 중 세 번째(0.98~1.39초): 앞이 조용해 어택이 깨끗하고 자연스럽게 잦아든다. 5ms 페이드인, 40ms 페이드아웃
"""
import json
import os
import shutil
import subprocess
import sys
import wave

import numpy as np

SR = 48000
FF = "/opt/homebrew/bin/ffmpeg"

COPY = [
    "clock_touch_cymbal.wav",
    "clock_touch_cymbal_short.wav",
    "pull_warm_shimmer_1.wav",
    "pull_warm_shimmer_2.wav",
    "pull_warm_shimmer_3.wav",
    "pull_cool_drop_1.wav",
    "pull_cool_drop_layered.wav",
    "pull_cool_pour_end.wav",
    "touch_cool_drop.wav",
    "obj_warm_ping.wav",
    "drop_layer.wav",
    "pour_bed_sugar_loop.wav",
    "pour_bed_sugar_tail.wav",
    "seal_press_sparkle.wav",
    "seal_bloom_glitter_1.wav",
    "seal_bloom_glitter_2.wav",
    "seal_afterglow_harp_open5.wav",
    "shelf_glass_touch.wav",
    "note_sleep_C.wav",
    "note_move_G.wav",
    "note_work_E.wav",
    "note_drift_A.wav",
    "note_play_D.wav",
]

SOFTEN = {
    "touch_warm_chime_06.wav": "touch_warm_chime_06_soft.wav",
    "touch_warm_chime_08.wav": "touch_warm_chime_08_soft.wav",
}
SOFTEN_FILTER = "highshelf=f=4500:g=-9,lowpass=f=12000:p=1"
EXCERPT = {
    "merge_cool_bubbles.wav": ("../interaction_sounds/bubbles-2_Glaneur-de-sons_104950.wav", 0.98, 1.39, 0.005, 0.04),
}
PEAK_DBFS = -3.0
LEAD_DB = -25.0
REF_DB = -18.0
GAIN_MIN = 0.25
GAIN_MAX = 4.0


def trim_lead(x):
    win = int(0.01 * SR)
    k = len(x) // win
    e = np.sqrt(np.mean(x[:k * win].reshape(k, win) ** 2, axis=1))
    start = int(np.argmax(e > e.max() * 10 ** (LEAD_DB / 20.0)))
    s = max(0, (start - 2) * win)
    y = x[s:].copy()
    n = int(0.010 * SR)
    y[:n] *= np.linspace(0.0, 1.0, n)
    return y, s / SR


def momentary_db(x):
    win = int(0.1 * SR)
    hop = int(0.01 * SR)
    best = 0.0
    for i in range(0, max(1, len(x) - win), hop):
        best = max(best, float(np.sqrt(np.mean(x[i:i + win] ** 2))))
    return 20.0 * np.log10(max(best, 1e-9))


def read(path):
    with wave.open(path, "rb") as w:
        return np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float64) / 32768.0


def decode(path, af=None):
    cmd = [FF, "-v", "error", "-i", path]
    if af:
        cmd += ["-af", af]
    cmd += ["-f", "f32le", "-ac", "1", "-ar", str(SR), "pipe:1"]
    raw = subprocess.run(cmd, check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32).astype(np.float64)


def write(path, x):
    peak = np.max(np.abs(x))
    if peak > 0:
        x = x * (10 ** (PEAK_DBFS / 20.0) / peak)
    pcm = np.clip(np.round(x * 32767.0), -32768, 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


def main(src, dst):
    os.makedirs(dst, exist_ok=True)
    for name in COPY:
        shutil.copyfile(os.path.join(src, name), os.path.join(dst, name))
        print("copy", name)
    for name, out in SOFTEN.items():
        y, cut = trim_lead(decode(os.path.join(src, name), SOFTEN_FILTER))
        write(os.path.join(dst, out), y)
        print("soften", name, "->", out, f"lead cut {cut:.2f}s")
    for out, (rel, a, b, fin, fout) in EXCERPT.items():
        x = decode(os.path.normpath(os.path.join(src, rel)))
        y = x[int(a * SR):int(b * SR)].copy()
        ni, no = int(fin * SR), int(fout * SR)
        y[:ni] *= np.linspace(0.0, 1.0, ni)
        y[-no:] *= np.linspace(1.0, 0.0, no)
        write(os.path.join(dst, out), y)
        print("excerpt", rel, f"{a:.2f}-{b:.2f}s ->", out)
    names, gains = [], []
    for name in sorted(os.listdir(dst)):
        if not name.endswith(".wav"):
            continue
        m = momentary_db(read(os.path.join(dst, name)))
        g = float(np.clip(10 ** ((REF_DB - m) / 20.0), GAIN_MIN, GAIN_MAX))
        names.append(name[:-4])
        gains.append(round(g, 3))
        print(f"gain {name[:-4]:28s} momentary {m:6.1f} dB -> x{g:.2f}")
    with open(os.path.join(dst, "sfx_gains.json"), "w") as f:
        json.dump({"reference_db": REF_DB, "names": names, "gains": gains}, f, indent=1)


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])
