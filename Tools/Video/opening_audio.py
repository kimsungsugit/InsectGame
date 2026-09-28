"""오프닝 프롤로그 음원 — 앞 10초를 합성하고 기존 테마곡(10초)을 뒤에 붙여 20초 wav를 만든다.

    python -X utf8 Tools/Video/opening_audio.py

산출: Assets/Resources/Audio/Opening/opening_prologue.wav (44.1kHz 스테레오 16bit)

테마곡은 D음 드론 계열이고 6.0초에 강세, 9~10초에 D5·F#5로 풀리며 끝난다. 그래서 앞 10초를
**같은 D조**로 깔면 이음매 없이 이어지고, 곡의 강세가 정확히 16.0초(타이틀)에 떨어진다.

    0.0 ~ 9.4   D2·A2·D3 패드 + 바람 + 빛의 잔향(고음 반짝임). 빛이 꺼지는 만큼 반짝임도 잦아든다.
    4.6 ~       단3도(F3)가 스며든다 — 길 끝에 검은 코트가 선다.
    6.1 ~ 8.1   그물에 빛이 닿을 때마다 차가운 핑 + 둔탁한 톡(영상의 Collected와 같은 스케줄).
    9.36        마지막 빛이 꺼진다 — 낮은 붐. 짧은 정적.
    10.0 ~ 20.0 테마곡.

영상과 사건 시각을 공유한다 — opening_prologue.py를 모듈로 읽어 그 스케줄을 그대로 쓴다.
"""
import os
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import opening_prologue as vp  # noqa: E402  (사건 시각의 단일 출처)

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
THEME = os.path.join(ROOT, "Assets", "Resources", "Audio", "Opening", "opening_theme.wav")
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio", "Opening", "opening_prologue.wav")
SR = 44100
T = vp.T_TOTAL
N = int(SR * T)
THEME_AT = 10.0
BOOM_AT = 9.36
t = np.arange(N) / SR
rng = np.random.default_rng(3)


def sstep(a, b, x):
    u = np.clip((x - a) / (b - a), 0.0, 1.0)
    return u * u * (3 - 2 * u)


def band_noise(lo, hi, seed):
    """대역 제한 잡음 — 주파수 영역에서 부드러운 버터워스형 경사로 깎는다(scipy 없이)."""
    r = np.random.default_rng(seed)
    x = r.normal(0, 1, N)
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(N, 1 / SR)
    hp = 1 / np.sqrt(1 + (lo / np.maximum(f, 1e-3)) ** 4)
    lp = 1 / np.sqrt(1 + (f / hi) ** 4)
    y = np.fft.irfft(X * hp * lp, N)
    return y / (np.std(y) + 1e-9)


def pad(freq, detune, harmonics=(1.0, 0.35, 0.15, 0.06), phase=0.0):
    out = np.zeros(N)
    for k, a in enumerate(harmonics, 1):
        for d in (-detune, detune):
            out += a * np.sin(2 * np.pi * (freq * k + d * k) * t + phase * k + d)
    return out / (2 * sum(harmonics))


def reverb(x, rt60=2.4, seed=9):
    """합성 잔향 — 감쇠하는 잡음 IR(고역이 더 빨리 죽게 두 겹)과 FFT 합성곱."""
    n = int(SR * rt60)
    r = np.random.default_rng(seed)
    tt = np.arange(n) / SR
    decay = np.exp(-6.9 * tt / rt60)
    ir = r.normal(0, 1, n) * decay
    # 고역은 빨리 사라진다 — 저역 통과한 사본을 뒤쪽에 더 싣는다
    lowish = np.convolve(r.normal(0, 1, n), np.ones(24) / 24, mode="same") * np.exp(-6.9 * tt / (rt60 * 1.3))
    ir = ir * 0.6 + lowish * 0.9
    ir[: int(SR * 0.012)] = 0.0                      # 초기 반사 전 짧은 틈
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-9
    m = 1 << int(np.ceil(np.log2(len(x) + n)))
    y = np.fft.irfft(np.fft.rfft(x, m) * np.fft.rfft(ir, m), m)[: len(x)]
    return y


def env_decay(at, tau, attack=0.004):
    e = np.zeros(N)
    i0 = int(at * SR)
    if i0 >= N:
        return e
    tt = t[i0:] - at
    e[i0:] = np.minimum(1.0, tt / attack) * np.exp(-tt / tau)
    return e


def main():
    L = np.zeros(N)
    R = np.zeros(N)

    # ── 패드: D2·A2·D3 (+F3 단3도, +D1 서브) ──
    base = sstep(0.0, 2.2, t) * (1 + 0.6 * sstep(7.0, 9.2, t)) * (1 - sstep(9.22, 9.45, t))
    for f, amp, ph in ((73.42, 0.14, 0.0), (110.0, 0.10, 1.1), (146.83, 0.09, 2.3)):
        L += pad(f, 0.22, phase=ph) * amp * base
        R += pad(f, 0.29, phase=ph + 0.7) * amp * base
    minor = sstep(4.6, 6.6, t) * (1 - sstep(9.22, 9.45, t))
    L += pad(174.61, 0.2, phase=0.4) * 0.065 * minor
    R += pad(174.61, 0.26, phase=1.3) * 0.065 * minor
    sub = sstep(7.0, 9.2, t) * (1 - sstep(9.25, 9.4, t))
    L += np.sin(2 * np.pi * 36.71 * t) * 0.07 * sub
    R += np.sin(2 * np.pi * 36.71 * t + 0.3) * 0.07 * sub

    # ── 바람 ──
    wind_env = sstep(0.0, 2.0, t) * (1 - sstep(8.8, 9.5, t)) * (0.62 + 0.38 * np.sin(2 * np.pi * 0.12 * t)
                                                                 * np.sin(2 * np.pi * 0.31 * t + 1.0))
    L += band_noise(160, 900, 1) * 0.036 * wind_env
    R += band_noise(160, 900, 2) * 0.036 * wind_env

    # ── 빛의 잔향 — 반짝임은 빛이 꺼지는 만큼 잦아든다(영상 VANISH1과 같은 구간) ──
    life = sstep(0.0, 1.2, t) * (1 - sstep(vp.VANISH1[0], vp.VANISH1[1], t))
    for f in (1174.66, 1479.98, 1760.0, 2349.32):
        trem_f = rng.uniform(3.0, 7.0)
        trem = (0.5 + 0.5 * np.sin(2 * np.pi * trem_f * t + rng.uniform(0, 6.28))) ** 2
        pan = rng.uniform(-0.6, 0.6)
        s = np.sin(2 * np.pi * f * t) * 0.015 * life * trem
        L += s * (1 - pan)
        R += s * (1 + pan)

    # ── 그물에 닿는 빛 — 영상의 Collected와 같은 난수 스케줄 ──
    col = vp.Collected(10, 7, (0.08, 0.92, 0.25, 0.9))
    for i, at in enumerate(col.arrive):
        pan = float(np.clip((col.p0[i][0] / vp.W - 0.5) * 0.8, -0.5, 0.5))
        e = env_decay(at, 0.22)
        # 차가운 핑 — A6에서 E6로 살짝 떨어진다
        f = 1318.5 + (1760.0 - 1318.5) * np.exp(-np.maximum(t - at, 0) / 0.05)
        ph = 2 * np.pi * np.cumsum(f) / SR
        ping = np.sin(ph) * e * 0.028
        thud = np.sin(2 * np.pi * 62 * t) * env_decay(at, 0.12) * 0.045
        L += (ping + thud) * (1 - pan)
        R += (ping + thud) * (1 + pan)

    # ── 마지막 빛이 꺼진다 — 낮은 붐 ──
    tb = np.maximum(t - BOOM_AT, 0)
    boom_f = 38 + 12 * np.exp(-tb / 0.35)
    boom = np.sin(2 * np.pi * np.cumsum(boom_f) / SR) * env_decay(BOOM_AT, 0.9, attack=0.012) * 0.34
    rumble = band_noise(30, 260, 5) * env_decay(BOOM_AT, 0.28, attack=0.01) * 0.1
    L += boom + rumble
    R += boom + rumble * 0.9

    # 전주에만 공간감을 준다 — 테마곡은 이미 믹스가 끝난 음원이다
    L = L + reverb(L, seed=11) * 0.32
    R = R + reverb(R, seed=12) * 0.32

    # ── 테마곡 ──
    w = wave.open(THEME, "rb")
    assert w.getframerate() == SR and w.getsampwidth() == 2, (w.getframerate(), w.getsampwidth())
    raw = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float64) / 32768.0
    ch = w.getnchannels()
    raw = raw.reshape(-1, ch)
    th_l, th_r = raw[:, 0], raw[:, -1]
    i0 = int(THEME_AT * SR)
    n = min(len(th_l), N - i0)
    L[i0:i0 + n] += th_l[:n]
    R[i0:i0 + n] += th_r[:n]

    # 전주 피크만 누른다(테마곡 음량은 그대로)
    pre = slice(0, i0)
    peak = max(np.abs(L[pre]).max(), np.abs(R[pre]).max())
    if peak > 0.85:
        L[pre] *= 0.85 / peak
        R[pre] *= 0.85 / peak
    L[-int(0.05 * SR):] *= np.linspace(1, 0, int(0.05 * SR))
    R[-int(0.05 * SR):] *= np.linspace(1, 0, int(0.05 * SR))

    out = np.stack([L, R], 1)
    out = np.clip(out, -1, 1)
    pcm = (out * 32767).astype(np.int16)
    with wave.open(OUT, "wb") as wo:
        wo.setnchannels(2)
        wo.setsampwidth(2)
        wo.setframerate(SR)
        wo.writeframes(pcm.tobytes())
    mono = out.mean(1)
    for k in range(0, int(T * 2)):
        seg = mono[int(k * 0.5 * SR):int((k + 1) * 0.5 * SR)]
        rms = np.sqrt((seg ** 2).mean())
        print(f"{k * 0.5:4.1f}s {20 * np.log10(rms + 1e-9):6.1f} dB  peak {20 * np.log10(np.abs(seg).max() + 1e-9):6.1f}")
    print("->", OUT)


if __name__ == "__main__":
    main()
