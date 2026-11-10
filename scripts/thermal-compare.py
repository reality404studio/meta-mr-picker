# 사용: python3 scripts/thermal-compare.py <apk> <라벨> [분=10]   → evidence/thermal/<라벨>.csv 와 요약 한 줄. 두 빌드를 각각 돌린 뒤 --report 로 비교
import csv
import re
import statistics
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ADB = "/Applications/Unity/Hub/Editor/6000.3.27f2/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"
PKG = "com.DefaultCompany.moodpicker"
ACTIVITY = PKG + "/com.unity3d.player.UnityPlayerGameActivity"
OUT = ROOT / "evidence/thermal"
FIELDS = ["t", "fps", "target", "app_ms", "gpu_pct", "cpu_pct", "cpu_level", "gpu_level", "temp_c", "stale", "skin_c", "battery_c", "sim_ms", "awake"]


def adb(*args, check=False):
    r = subprocess.run([ADB, *args], capture_output=True, text=True)
    if check and r.returncode:
        raise SystemExit(f"adb {' '.join(args)} 실패: {r.stderr.strip()}")
    return r.stdout


def parse_vrapi(line):
    out = {}
    m = re.search(r"FPS=(\d+)/(\d+)", line)
    if not m:
        return None
    out["fps"], out["target"] = int(m.group(1)), int(m.group(2))
    for key, pattern in (("app_ms", r"App=([\d.]+)ms"), ("gpu_pct", r"GPU%=([\d.]+)"), ("cpu_pct", r"CPU%=([\d.]+)"),
                         ("temp_c", r"Temp=([\d.]+)C"), ("stale", r"Stale=(\d+)")):
        m = re.search(pattern, line)
        if m:
            out[key] = float(m.group(1))
    m = re.search(r"CPU\d/GPU=(\d+)/(\d+)", line)
    if m:
        out["cpu_level"], out["gpu_level"] = int(m.group(1)), int(m.group(2))
    return out


def thermal():
    text = adb("shell", "dumpsys", "thermalservice")
    skin = [float(v) for v in re.findall(r"mValue=([\d.]+)[^}]*mType=3", text)]
    battery = [float(v) for v in re.findall(r"mValue=([\d.]+)[^}]*mType=2", text)]
    return (max(skin) if skin else None, max(battery) if battery else None)


def run(apk, label, minutes):
    OUT.mkdir(parents=True, exist_ok=True)
    if "device" not in adb("devices").split("\n", 1)[-1]:
        raise SystemExit("기기 연결 없음")
    adb("install", "-r", "-g", apk, check=True)
    adb("shell", "am", "force-stop", PKG)
    adb("logcat", "-c")
    adb("shell", "am", "start", "-n", ACTIVITY, check=True)
    rows = []
    start = time.time()
    seen = 0
    print(f"{label}: {minutes}분 측정. 헤드셋을 쓴 채로 같은 동작(30초 쉬고 30초 휘젓기 반복)을 해 주세요", flush=True)
    while time.time() - start < minutes * 60:
        time.sleep(10)
        lines = [l for l in adb("logcat", "-d", "-s", "VrApi").splitlines() if "FPS=" in l]
        tuning = [l for l in adb("logcat", "-d", "-s", "Unity").splitlines() if "[MP] sim " in l]
        skin, battery = thermal()
        sim_ms, awake = None, None
        if tuning:
            m = re.search(r"ms=([\d.]+) awake=([\d.]+)", tuning[-1])
            if m:
                sim_ms, awake = float(m.group(1)), float(m.group(2))
        for line in lines[seen:]:
            v = parse_vrapi(line)
            if v:
                v.update(t=round(time.time() - start), skin_c=skin, battery_c=battery, sim_ms=sim_ms, awake=awake)
                rows.append(v)
        seen = len(lines)
        if rows:
            last = rows[-1]
            print(f"  {last['t']:>4}s fps={last.get('fps')} app={last.get('app_ms')}ms gpu%={last.get('gpu_pct')} cpu%={last.get('cpu_pct')} temp={last.get('temp_c')} skin={skin}", flush=True)
    path = OUT / f"{label}.csv"
    with path.open("w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=FIELDS)
        w.writeheader()
        for r in rows:
            w.writerow({k: r.get(k) for k in FIELDS})
    print(summary(label, rows))


def summary(label, rows):
    def col(k):
        return [r[k] for r in rows if r.get(k) is not None]

    def mean(k):
        v = col(k)
        return round(statistics.mean(v), 2) if v else None

    def last_minus_first(k):
        v = col(k)
        return round(statistics.mean(v[-6:]) - statistics.mean(v[:6]), 2) if len(v) >= 12 else None

    fps = col("fps")
    below = sum(1 for r in rows if r.get("fps") is not None and r.get("target") and r["fps"] < r["target"] - 2)
    return (f"{label}: 샘플 {len(rows)} · FPS 평균 {mean('fps')} (목표-2 미만 {below}개, 최저 {min(fps) if fps else None}) · "
            f"App {mean('app_ms')}ms · GPU% {mean('gpu_pct')} · CPU% {mean('cpu_pct')} · "
            f"CPU/GPU 레벨 최대 {max(col('cpu_level'), default=None)}/{max(col('gpu_level'), default=None)} · "
            f"Temp 평균 {mean('temp_c')}C (끝-처음 {last_minus_first('temp_c')}) · 스킨 끝-처음 {last_minus_first('skin_c')} · sim {mean('sim_ms')}ms")


def report():
    for path in sorted(OUT.glob("*.csv")):
        with path.open() as f:
            rows = [{k: (float(v) if v not in ("", None) else None) for k, v in r.items()} for r in csv.DictReader(f)]
        print(summary(path.stem, rows))


if __name__ == "__main__":
    if len(sys.argv) >= 2 and sys.argv[1] == "--report":
        report()
    elif len(sys.argv) >= 3:
        run(sys.argv[1], sys.argv[2], float(sys.argv[3]) if len(sys.argv) > 3 else 10)
    else:
        raise SystemExit(__doc__ or "사용: thermal-compare.py <apk> <라벨> [분]")
