"""「챔피언의 꿈」 도입 음원 — 경기장 입장 8초(환호·발소리·타격)를 합성한다.

    python -X utf8 Tools/Video/dream_arena_audio.py

산출: Assets/Resources/Audio/Dream/dream_arena.wav (44.1kHz 스테레오 16bit, 8초)

영상(dream_arena.py)과 **사건 시각을 공유한다** — 그 파일을 모듈로 읽어 상수를 그대로 쓴다.

    0.0 ~ 3.0   터널 — 벽 너머에서 먹먹하게 새어 드는 환호(저역만)가 점점 커진다. 걸음마다 발소리와 울림.
                낮은 드론이 깔리고 2.0부터 상승음이 차오른다.
    3.0         흰 화면 — 큰 '쿵' + 막혔던 환호가 한꺼번에 터진다(먹먹함이 걷힌다).
    3.4 ~ 5.75  박수가 점점 촘촘해진다. 휘파람 몇 번. 환호는 일렁이며 부푼다.
    5.75 ~ 6.1  숨죽임 — 환호가 푹 꺼진다(상승음도 여기서 끊긴다).
    6.1         맞대결 — 가장 큰 타격 + 환호 정점 + 금속성 울림.
    7.2 ~ 8.0   페이드아웃. 이어서 게임이 타이틀 카드를 얹는다.

외부 음원·scipy 없이 numpy만 쓴다(주파수 영역 필터).
"""
import os
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dream_arena as vp  # noqa: E402  (사건 시각의 단일 출처)

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio", "Dream", "dream_arena.wav")
SR = 44100
T = vp.T_TOTAL
N = int(SR * T)
t = np.arange(N) / SR


def span(a, b, x):
    return np.clip((x - a) / (b - a), 0.0, 1.0)


def sstep(a, b, x):
    u = span(a, b, x)
    return u * u * (3 - 2 * u)


def band_noise(lo, hi, seed):
    """대역 제한 잡음 — 주파수 영역에서 부드러운 경사로 깎는다(scipy 없이)."""
    r = np.random.default_rng(seed)
    x = r.normal(0, 1, N)
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(N, 1 / SR)
    hp = 1 / np.sqrt(1 + (lo / np.maximum(f, 1e-3)) ** 4)
    lp = 1 / np.sqrt(1 + (f / hi) ** 4)
    y = np.fft.irfft(X * hp * lp, N)
    return y / (np.std(y) + 1e-9)


def lowpass(x, fc):
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    return np.fft.irfft(X / np.sqrt(1 + (f / fc) ** 4), len(x))


def smooth_env(points, win=0.06):
    """키프레임 (시각, 값) 목록 → 샘플 단위 포락선(이동평균으로 모서리를 눕힌다)."""
    xs, ys = zip(*points)
    env = np.interp(t, xs, ys)
    k = max(1, int(SR * win))
    return np.convolve(env, np.ones(k) / k, mode="same")


def slow_random(seed, rate, lo, hi):
    """느리게 일렁이는 0..1 난수 포락선 — 환호가 한결같은 잡음이 아니라 사람들의 숨처럼 부푼다."""
    r = np.random.default_rng(seed)
    n = int(T * rate) + 3
    pts = r.uniform(lo, hi, n)
    return np.interp(t, np.arange(n) / rate, pts)


def reverb(x, rt60=1.8, seed=9):
    """합성 잔향 — 감쇠하는 잡음 IR과 FFT 합성곱."""
    n = int(SR * rt60)
    r = np.random.default_rng(seed)
    tt = np.arange(n) / SR
    ir = r.normal(0, 1, n) * np.exp(-6.9 * tt / rt60)
    ir[: int(SR * 0.015)] = 0.0
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-9
    m = 1 << int(np.ceil(np.log2(len(x) + n)))
    return np.fft.irfft(np.fft.rfft(x, m) * np.fft.rfft(ir, m), m)[: len(x)]


def burst(at, dur, lo, hi, seed, decay, amp=1.0):
    """at초에 시작하는 짧은 잡음 덩어리(타격·박수)."""
    out = np.zeros(N)
    i0 = int(at * SR)
    n = int(dur * SR)
    if i0 >= N:
        return out
    n = min(n, N - i0)
    r = np.random.default_rng(seed)
    x = r.normal(0, 1, n)
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(n, 1 / SR)
    X *= 1 / np.sqrt(1 + (lo / np.maximum(f, 1e-3)) ** 4) / np.sqrt(1 + (f / hi) ** 4)
    y = np.fft.irfft(X, n)
    y /= np.max(np.abs(y)) + 1e-9
    out[i0:i0 + n] = y * np.exp(-np.arange(n) / SR / decay) * amp
    return out


def boom(at, f0, f1, dur, amp):
    """내려앉는 저음 '쿵' — 사인 주파수가 f0→f1로 미끄러진다."""
    out = np.zeros(N)
    i0 = int(at * SR)
    n = min(int(dur * SR), N - i0)
    if n <= 0:
        return out
    tt = np.arange(n) / SR
    freq = f1 + (f0 - f1) * np.exp(-tt / (dur * 0.22))
    phase = 2 * np.pi * np.cumsum(freq) / SR
    env = np.exp(-tt / (dur * 0.42)) * np.minimum(1.0, tt / 0.004)
    out[i0:i0 + n] = np.sin(phase) * env * amp
    return out


def whistle(at, dur, f0, f1, amp):
    out = np.zeros(N)
    i0 = int(at * SR)
    n = min(int(dur * SR), N - i0)
    if n <= 0:
        return out
    tt = np.arange(n) / SR
    freq = f0 + (f1 - f0) * (tt / dur) + 28 * np.sin(2 * np.pi * 6.5 * tt)
    phase = 2 * np.pi * np.cumsum(freq) / SR
    env = np.sin(np.pi * np.minimum(1.0, tt / dur)) ** 0.7
    out[i0:i0 + n] = (np.sin(phase) + 0.25 * np.sin(2 * phase)) * env * amp
    return out


def main():
    rng = np.random.default_rng(17)

    # ── 터널 → 경기장 환호 ──
    # 총 크기: 터널에서 점점 → 3.12 폭발 → 부풂 → 5.75 숨죽임 → 6.16 정점 → 페이드
    level = smooth_env([(0.0, 0.07), (1.0, 0.13), (2.0, 0.26), (2.9, 0.44), (3.0, 0.48), (3.12, 1.0), (3.6, 0.66),
                        (4.6, 0.74), (5.6, 0.88), (5.75, 0.68), (6.0, 0.26), (6.08, 0.24), (6.16, 1.18), (6.5, 0.96),
                        (7.2, 0.78), (8.0, 0.14)], 0.05)
    sway = 0.78 + 0.22 * slow_random(3, 2.6, 0.0, 1.0)
    chans = []
    for side in (0, 1):
        lo_body = band_noise(220, 1300, 100 + side)                    # 사람 목소리 몸통
        voice = band_noise(500, 2600, 110 + side) * (0.5 + 0.5 * slow_random(7 + side, 3.2, 0.0, 1.0))
        air = band_noise(2600, 6000, 120 + side) * 0.08 * sstep(3.0, 3.4, t)   # 아레나에선 환호의 윗결이 열린다
        raw = (lo_body * 0.8 + voice * 0.55 + air) * level * sway
        low = lowpass(raw, 300.0)
        mid = lowpass(raw, 1500.0)
        w_low = 1 - sstep(0.0, 2.6, t)
        w_mid = sstep(0.0, 2.6, t) * (1 - sstep(2.95, 3.12, t))
        w_full = sstep(2.95, 3.12, t)
        chans.append(low * w_low + mid * w_mid * 0.9 + raw * w_full)
    crowd_l, crowd_r = chans

    # ── 박수 — 점점 촘촘해진다 ──
    rate = np.interp(t, [3.4, 5.75, 6.0, 6.16, 7.0], [14, 120, 25, 160, 90])
    rate = rate * sstep(3.3, 3.5, t) * (1 - sstep(7.2, 8.0, t))
    p = rate / SR
    hits = (rng.random(N) < p) * rng.uniform(0.4, 1.0, N)
    k = int(0.03 * SR)
    kt = np.arange(k) / SR
    kern = np.random.default_rng(4).normal(0, 1, k) * np.exp(-kt / 0.007)
    Kf = np.fft.rfft(kern, 1 << 20)
    claps = np.fft.irfft(np.fft.rfft(hits, 1 << 20) * Kf, 1 << 20)[:N]
    claps = np.convolve(claps, np.array([1.0, -0.6]), mode="same")      # 약한 고역 강조 — 손뼉의 날
    claps *= 0.055 / (np.std(claps) + 1e-9)
    claps_r = np.roll(claps, 37) * 0.9                                   # 좌우를 살짝 어긋나게

    # ── 휘파람·함성 ──
    whistles = (whistle(3.5, 0.4, 1900, 2500, 0.03) + whistle(4.85, 0.35, 2100, 2800, 0.028)
                + whistle(6.3, 0.5, 2300, 3000, 0.035))

    # ── 터널: 발소리·드론·상승음 ──
    steps = np.zeros(N)
    st = vp.STEP_FIRST
    while st < 2.95:
        u = st / vp.TUNNEL_END
        steps += boom(st, 110, 58, 0.16, 0.34 * (1 - 0.45 * u)) + burst(st, 0.05, 250, 2200, int(st * 1000), 0.012, 0.16 * (1 - 0.4 * u))
        st += vp.STEP_PERIOD
    steps_wet = reverb(steps, 1.1, 5) * 0.55 * (1 - sstep(2.9, 3.1, t))
    steps = steps * (1 - sstep(2.95, 3.05, t))
    drone = (np.sin(2 * np.pi * 73.4 * t) * 0.6 + np.sin(2 * np.pi * 110.0 * t) * 0.35 + np.sin(2 * np.pi * 146.8 * t) * 0.18)
    drone *= 0.07 * sstep(0.0, 1.6, t) * (1 - sstep(2.98, 3.2, t))
    riser = band_noise(300, 5000, 33) * sstep(1.8, 3.0, t) ** 2 * (1 - sstep(2.98, 3.0, t)) * 0.2
    riser2 = band_noise(400, 7000, 34) * sstep(4.6, 5.75, t) ** 2 * (1 - sstep(5.75, 5.82, t)) * 0.16

    # ── 타격 ──
    hit1 = boom(vp.TUNNEL_END + 0.03, 78, 28, 1.0, 0.95) + burst(vp.TUNNEL_END + 0.03, 0.9, 90, 2800, 61, 0.28, 0.5)
    hit2 = boom(vp.FACEOFF, 66, 24, 1.15, 1.0) + burst(vp.FACEOFF, 1.0, 120, 3200, 62, 0.34, 0.55)
    clash = burst(vp.FACEOFF, 1.3, 3500, 11000, 63, 0.42, 0.22)
    sub = band_noise(25, 90, 71) * (0.08 + 0.1 * sstep(2.0, 3.0, t) * (1 - sstep(2.98, 3.1, t)) + 0.1 * sstep(3.1, 3.6, t))

    # ── 합성 ──
    arena_mask = sstep(2.95, 3.1, t)
    left = crowd_l + claps + whistles + steps + steps_wet + drone + riser + riser2 + hit1 + hit2 + clash + sub
    right = crowd_r + claps_r + whistles + steps + steps_wet + drone + riser + riser2 + hit1 + hit2 + clash + sub
    # 경기장 잔향 — 환호·박수에만 얹는다(터널 발소리는 따로 울림을 받았다)
    wet = reverb(crowd_l + claps + whistles, 1.9, 8) * 0.22 * arena_mask
    wet_r = reverb(crowd_r + claps_r + whistles, 1.9, 9) * 0.22 * arena_mask
    left = left + wet
    right = right + wet_r

    fade = 1 - sstep(*vp.FADE_OUT, t)
    head = sstep(0.0, 0.12, t)
    stereo = np.stack([left, right], axis=1) * (fade * head)[:, None]
    peak = float(np.max(np.abs(stereo)))
    stereo = np.tanh(stereo / (peak + 1e-9) * 1.25) / np.tanh(1.25) * 0.88      # 부드러운 리미터 + 정규화

    pcm = (np.clip(stereo, -1, 1) * 32767).astype("<i2")
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with wave.open(OUT, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print(f"dream_arena.wav {T:.1f}s {os.path.getsize(OUT) / 1e6:.2f}MB peak={np.max(np.abs(stereo)):.2f}")


if __name__ == "__main__":
    main()
