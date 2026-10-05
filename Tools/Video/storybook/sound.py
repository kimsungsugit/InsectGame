"""그림책 영상의 소리 — numpy만으로 합성한다(테마곡·외부 음원 없음: 저작권·용량).

영상마다 `score(sc)`가 이 `Score`에 사건을 얹는다. 시각은 영상 전체 기준 초이고, `sc.shot(k)`가 k번째 샷의
시작 시각을 준다(디졸브로 앞당긴 시각 — 그림 스크립트와 같은 시계).

소리의 결: 그림책이라 **밝고 작은 소리**가 기본이다 — 칼림바·종·실로폰 같은 뜯는 소리, 부드러운 패드, 자연음
(바람·물·풀벌레·새·불). 무서운 장면은 단조 패드·낮은 울림·바람으로만 — 아이가 보는 영상이다.
게임은 영상 소리를 마스터 볼륨으로 틀고 그동안 월드 BGM을 0.2로 낮춘다(StoryVideoDirector).
"""
import math
import wave

import numpy as np

SR = 44100

# 음 이름 → 주파수
_NOTE = {"C": -9, "C#": -8, "Db": -8, "D": -7, "D#": -6, "Eb": -6, "E": -5, "F": -4, "F#": -3, "Gb": -3, "G": -2,
         "G#": -1, "Ab": -1, "A": 0, "A#": 1, "Bb": 1, "B": 2}


def hz(name):
    """'A4', 'C#5', 'Bb3' → Hz."""
    n, o = name[:-1], int(name[-1])
    return 440.0 * 2 ** ((_NOTE[n] + (o - 4) * 12) / 12)


def _level(points, x):
    xs = [p[0] for p in points]
    ys = [p[1] for p in points]
    return float(np.interp(x, xs, ys))


class Score:
    def __init__(self, total, shot_starts=(), seed=7):
        self.total = total
        self.n = int(SR * total)
        self.t = np.arange(self.n) / SR
        self.out = np.zeros((self.n, 2), np.float64)
        self.rng = np.random.default_rng(seed)
        self.starts = list(shot_starts)

    # ── 시간 ──
    def shot(self, k):
        return self.starts[k]

    def env(self, points):
        xs, ys = zip(*points)
        return np.interp(self.t, xs, ys)

    def _seg(self, at, dur):
        i0 = max(0, int(at * SR))
        m = min(self.n - i0, int(dur * SR))
        return i0, m

    def _mix(self, i0, sig, pan=0.0):
        m = len(sig)
        if m <= 0:
            return
        self.out[i0:i0 + m, 0] += sig * (1 - pan) * 0.5
        self.out[i0:i0 + m, 1] += sig * (1 + pan) * 0.5

    def _band(self, m, lo, hi, seed_noise=None):
        x = self.rng.normal(0, 1, m) if seed_noise is None else seed_noise
        X = np.fft.rfft(x)
        fr = np.fft.rfftfreq(m, 1 / SR)
        X *= (fr > lo) & (fr < hi)
        y = np.fft.irfft(X, m)
        return y / (np.std(y) + 1e-9)

    # ── 음악 ──
    def pad(self, freqs, env_points, amp=0.06, detune=0.003, bright=0.25, pan_spread=0.4):
        """화음 패드 — env_points는 [(초, 0..1)]."""
        e = self.env(env_points)
        for i, fq in enumerate(freqs):
            pan = (i / max(1, len(freqs) - 1) - 0.5) * pan_spread
            ph = self.rng.uniform(0, 6.28, 3)
            sig = (np.sin(2 * np.pi * fq * self.t + ph[0]) + np.sin(2 * np.pi * fq * (1 + detune) * self.t + ph[1])
                   + bright * np.sin(2 * np.pi * fq * 2 * self.t + ph[2]))
            self._mix(0, sig * e * amp * 0.5, pan)

    def pluck(self, at, freq, amp=0.16, dur=1.4, pan=0.0, kind="kalimba"):
        """뜯는 소리 — kalimba(둥근) · bell(맑은 종) · xylo(나무 실로폰) · box(오르골)."""
        i0, m = self._seg(at, dur)
        if m <= 0:
            return
        tt = np.arange(m) / SR
        if kind == "bell":
            partials = ((1, 1.0, 2.6), (2.76, 0.42, 4.5), (5.4, 0.2, 7.5), (8.9, 0.08, 11))
        elif kind == "xylo":
            partials = ((1, 1.0, 9.0), (3.93, 0.3, 18), (9.2, 0.1, 30))
        elif kind == "box":
            partials = ((1, 1.0, 3.2), (2.0, 0.3, 5), (3.0, 0.18, 7), (4.2, 0.1, 10))
        else:
            partials = ((1, 1.0, 4.0), (2.0, 0.18, 7), (5.95, 0.12, 14))
        sig = sum(w * np.sin(2 * np.pi * freq * r * tt) * np.exp(-tt * d) for r, w, d in partials)
        sig *= np.minimum(1, tt / 0.003) * amp
        self._mix(i0, sig, pan)

    def melody(self, at, notes, step=0.32, amp=0.14, kind="kalimba", pan=0.0, swing=0.0):
        """notes = ['D5', 'F#5', None(쉼표), ...] 또는 [(음, 박 수)]. 박 하나 = step초."""
        tt = at
        for i, nt in enumerate(notes):
            beats = 1.0
            if isinstance(nt, tuple):
                nt, beats = nt
            if nt:
                self.pluck(tt + (swing if i % 2 else 0.0), hz(nt), amp, dur=1.2 + 0.4 * beats, pan=pan, kind=kind)
            tt += step * beats

    def arp(self, at, chord, count=8, step=0.14, amp=0.09, kind="box", up=True, pan=0.0):
        notes = list(chord) if up else list(chord)[::-1]
        for k in range(count):
            self.pluck(at + k * step, hz(notes[k % len(notes)]), amp, dur=1.0, pan=pan + (k % 2 - 0.5) * 0.3,
                       kind=kind)

    # ── 효과음 ──
    def chime(self, at, freq, amp=0.14, dur=1.6, pan=0.0):
        self.pluck(at, freq, amp, dur, pan, kind="bell")

    def sparkle(self, at, dur=1.0, density=14, amp=0.05, lo=1800, hi=4200):
        """반짝임 — 높은 종 여럿을 흩뿌린다."""
        for _ in range(int(density * dur)):
            self.pluck(at + self.rng.uniform(0, dur), self.rng.uniform(lo, hi), amp * self.rng.uniform(0.4, 1.0),
                       dur=0.6, pan=self.rng.uniform(-0.8, 0.8), kind="bell")

    def boom(self, at, f0=90, f1=40, dur=1.2, amp=0.5):
        """낮은 울림(쿵)."""
        i0, m = self._seg(at, dur)
        if m <= 0:
            return
        tt = np.arange(m) / SR
        fr = f0 * (f1 / f0) ** (tt / dur)
        y = np.sin(2 * np.pi * np.cumsum(fr) / SR) * np.exp(-tt * 4.0) * np.minimum(1, tt / 0.01) * amp
        self._mix(i0, y)

    def thud(self, at, amp=0.35):
        """나무 상자가 놓이는 소리 — 짧은 쿵 + 나무결."""
        self.boom(at, 140, 70, 0.35, amp)
        self.noise(at, 0.12, 300, 2500, amp * 0.5, decay=30)

    def noise(self, at, dur, lo, hi, amp, decay=8.0, pan=0.0):
        """잡음 터짐 — 부서짐·흩어짐·바스락."""
        i0, m = self._seg(at, dur)
        if m <= 0:
            return
        y = self._band(m, lo, hi) * np.exp(-np.arange(m) / SR * decay) * amp
        self._mix(i0, y, pan)

    def whoosh(self, at, dur=0.8, amp=0.2, lo=300, hi=2400, rise=True, pan_from=-0.6, pan_to=0.6):
        """휙 — 대역 잡음의 크기가 부풀었다 잦아든다(좌→우로 흐른다)."""
        i0, m = self._seg(at, dur)
        if m <= 0:
            return
        tt = np.arange(m) / m
        y = self._band(m, lo, hi)
        env = np.sin(np.pi * tt) ** (1.5 if rise else 0.8)
        pan = pan_from + (pan_to - pan_from) * tt
        self.out[i0:i0 + m, 0] += y * env * amp * (1 - pan) * 0.5
        self.out[i0:i0 + m, 1] += y * env * amp * (1 + pan) * 0.5

    def step(self, at, amp=0.12, soft=True):
        """발소리 하나(풀밭이면 soft)."""
        self.noise(at, 0.09, 150 if soft else 400, 1600 if soft else 5000, amp, decay=40)
        self.boom(at, 110, 60, 0.12, amp * 0.6)

    def page(self, at, amp=0.12):
        """책장 넘기는 소리."""
        self.whoosh(at, 0.35, amp, 1200, 7000, pan_from=0.3, pan_to=-0.3)
        self.noise(at + 0.3, 0.06, 2000, 8000, amp * 0.6, decay=60)

    def scratch(self, at, dur=1.0, amp=0.05):
        """펜·끌로 긁는 소리 — 짧은 잡음 펄스가 이어진다."""
        k = at
        while k < at + dur:
            self.noise(k, 0.05, 2500, 7000, amp * self.rng.uniform(0.6, 1.0), decay=55)
            k += self.rng.uniform(0.06, 0.14)

    def blink(self, at, amp=0.08):
        """그림자의 눈 깜빡 — 낮고 흐린 두 음."""
        self.pluck(at, hz("D3"), amp, 0.8, -0.3, kind="kalimba")
        self.pluck(at + 0.02, hz("Eb3"), amp, 0.8, 0.3, kind="kalimba")

    def drip(self, at, amp=0.12, f=1400.0):
        i0, m = self._seg(at, 0.25)
        tt = np.arange(m) / SR
        fr = f * (1 + 1.5 * np.exp(-tt * 40))
        y = np.sin(2 * np.pi * np.cumsum(fr) / SR) * np.exp(-tt * 18) * amp
        self._mix(i0, y, self.rng.uniform(-0.5, 0.5))

    # ── 바탕(앰비언트) ──
    def wind(self, env_points, amp=0.05, lo=200, hi=1400, gust=0.5):
        """바람 — 대역 잡음, 천천히 일렁인다."""
        e = self.env(env_points)
        y = self._band(self.n, lo, hi)
        mod = 1 + gust * np.sin(2 * np.pi * 0.23 * self.t + 1.0) * np.sin(2 * np.pi * 0.07 * self.t)
        y2 = np.roll(y, SR // 3)
        self.out[:, 0] += y * e * mod * amp * 0.5
        self.out[:, 1] += y2 * e * mod * amp * 0.5

    def water(self, env_points, amp=0.04):
        """물가 — 낮은 물결 소리 + 가끔 퐁."""
        e = self.env(env_points)
        y = self._band(self.n, 120, 900) * (1 + 0.4 * np.sin(2 * np.pi * 0.5 * self.t))
        self._mix(0, y * e * amp)
        k = 0.3
        while k < self.total - 0.3:
            if _level(env_points, k) > 0.2:
                self.drip(k, amp * 1.5, f=self.rng.uniform(500, 900))
            k += self.rng.uniform(0.6, 1.6)

    def birds(self, env_points, amp=0.05, rate=1.2):
        """새 지저귐 — 짧은 주파수 미끄럼 여럿."""
        k = 0.2
        while k < self.total - 0.2:
            lv = _level(env_points, k)
            if lv > 0.05:
                f0 = self.rng.uniform(2600, 4200)
                for j in range(self.rng.integers(2, 5)):
                    at = k + j * 0.09
                    i0, m = self._seg(at, 0.08)
                    if m <= 0:
                        continue
                    tt = np.arange(m) / SR
                    fr = f0 * (1 + 0.35 * np.sin(np.pi * tt / 0.08)) * (1.1 if j % 2 else 1.0)
                    y = np.sin(2 * np.pi * np.cumsum(fr) / SR) * np.sin(np.pi * tt / 0.08) * amp * lv
                    self._mix(i0, y, self.rng.uniform(-0.7, 0.7))
            k += self.rng.uniform(0.5, 1.6) / rate

    def crickets(self, env_points, amp=0.03, f=4600):
        """풀벌레 — 높은 음이 빠르게 떨며 끊어진다."""
        e = self.env(env_points)
        car = np.sin(2 * np.pi * f * self.t)
        trill = (np.sin(2 * np.pi * 32 * self.t) > 0.2).astype(np.float64)
        gate = (np.sin(2 * np.pi * 1.1 * self.t + 0.5) > -0.2).astype(np.float64)
        y = car * trill * gate
        y = np.convolve(y, np.ones(40) / 40, mode="same")
        self._mix(0, y * e * amp, 0.4)
        car2 = np.sin(2 * np.pi * f * 1.07 * self.t)
        gate2 = (np.sin(2 * np.pi * 0.9 * self.t + 2.0) > -0.1).astype(np.float64)
        y2 = np.convolve(car2 * trill * gate2, np.ones(40) / 40, mode="same")
        self._mix(0, y2 * e * amp * 0.7, -0.5)

    def fire(self, env_points, amp=0.05):
        """불 — 낮은 우르릉 + 탁탁 튀는 소리."""
        e = self.env(env_points)
        y = self._band(self.n, 40, 260) * (1 + 0.3 * np.sin(2 * np.pi * 0.4 * self.t))
        self._mix(0, y * e * amp)
        k = 0.1
        while k < self.total - 0.1:
            lv = _level(env_points, k)
            if lv > 0.05:
                self.noise(k, 0.03, 1500, 7000, amp * 2.4 * lv * self.rng.uniform(0.4, 1.0), decay=120,
                           pan=self.rng.uniform(-0.7, 0.7))
            k += self.rng.uniform(0.04, 0.3)

    def room(self, env_points, amp=0.03):
        """실내 정적 — 아주 낮은 웅웅거림."""
        e = self.env(env_points)
        y = self._band(self.n, 50, 220)
        self._mix(0, y * e * amp)

    # ── 마감 ──
    def finish(self, path, reverb=0.3, fade_out=0.6, peak=0.85):
        out = self.out
        if reverb > 0:
            ir_n = int(SR * 1.4)
            ir = self.rng.normal(0, 1, ir_n) * np.exp(-np.arange(ir_n) / SR * 4.6)
            ir[0] = 0
            for ch in range(2):
                L = self.n + ir_n
                wet = np.fft.irfft(np.fft.rfft(out[:, ch], L) * np.fft.rfft(ir * (0.92 + 0.16 * ch), L), L)[:self.n]
                out[:, ch] += wet / (np.max(np.abs(wet)) + 1e-9) * np.max(np.abs(out[:, ch])) * reverb
        out *= np.clip((self.total - self.t) / fade_out, 0, 1)[:, None]
        out *= np.clip(self.t / 0.05, 0, 1)[:, None]
        mx = np.max(np.abs(out))
        if mx > 0:
            out *= peak / mx
        with wave.open(path, "wb") as wf:
            wf.setnchannels(2)
            wf.setsampwidth(2)
            wf.setframerate(SR)
            wf.writeframes((np.clip(out, -1, 1) * 32767).astype(np.int16).tobytes())
        return path
