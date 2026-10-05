"""ch8 「모래 창고」 — ch8_confront 대사 **뒤**(곧바로 간부전 앞)에 튼다. 모래언덕(밝은 모래색 + 파란 하늘) → 창고 안(황토빛).

직전 대사: 집게 "빈칸마다 구멍이 뚫린다. 그래서 전부 장부에 올린다" … 세라 "몇이나 살아 있죠?" "…장부에는 올라가 있다".

    샷1  0.0~3.0   모래언덕에 반쯤 묻힌 창고 입구 — 발자국이 문으로 이어지고, 카메라가 문 안으로 들어간다
    샷2  2.4~6.5   천장까지 쌓인 상자들, 먼지 속 빛줄기 — 바닥에서 천장으로 틸트
    샷3  5.9~9.5   상자 줄을 따라 옆으로 — 몇 개는 뚜껑이 열린 채 텅 비어 있다
    샷4  8.9~12.0  상자 하나가 빛 웅덩이 속에 조용히 놓여 있다 — 먼지만 떠다닌다(상자는 움직이지 않는다)
"""
import math

import numpy as np

from kit import (PAL, H, W, Mask, Particles, add, book_sky, c, crate, ease_in_out, fbm, grow, lerp, over, paint,
                 radial, ridge, rim, span, sun, zoom)
from silhouette_kit import streaks
import bk_v2 as bk
from sound import hz

DURS = [3.0, 3.5, 3.0, 2.5]
TINT = "#fff4e4"
REVERB = 0.45
CUES = [
    (1.0, 2.6, "창고 안은 끝이 보이지 않았다."),
    (4.2, 2.4, "천장까지 상자가 쌓여 있었다."),
    (6.6, 2.4, "어떤 상자는 텅 비어 있었다."),
    (9.4, 2.2, "이 상자는 너무 조용했다."),
]

SAND, SAND2, SAND_SH = "#f6dca0", "#ecc77f", "#d9a95f"
WOOD = ("#c98b4a", "#b97a3e", "#d39a58")
INSIDE = "#5a3a20"


# ══════════════════════════ 샷1 — 묻힌 창고 입구 ══════════════════════════

def dune(img, seed, y, amp, top, bottom, freq=(0.0018, 0.004), hi=0.35):
    a = ridge(Mask(), seed, y, amp, freq=freq).arr(0.8)
    over(img, c("#b98a4a"), np.clip(grow(a, 1.4) - a, 0, 1) * 0.8)
    over(img, bk.vgrad(H, W, [(0, top), (1, bottom)]), a)
    add(img, c("#fff6dc"), rim(a, 0, 5, 6) * hi)
    return a


def entrance_shot():
    img = book_sky(PAL["day"], seed=801)
    sun(img, 1060, 110, r=50, halo=0.5)
    dune(img, 802, 330, 46, "#f9e2ad", "#efcf8e", hi=0.3)
    dune(img, 803, 380, 30, SAND, SAND2)
    # 창고 정면(반쯤 묻혔다)
    fx0, fx1, fy0, fy1 = 380, 900, 180, 500
    fm = Mask()
    fm.rect(fx0, fy0, fx1, fy1)
    fa = fm.arr(0.6)
    paint(img, fa, "#b98a5a", hi=0.12, lo=0.18)
    pl = Mask()
    for y in range(int(fy0) + 36, int(fy1), 36):
        pl.line([(fx0 + 4, y), (fx1 - 4, y)], 2.2)
    over(img, c("#946a40"), pl.arr(0.6) * fa * 0.7)
    roof = Mask()
    roof.poly([(fx0 - 40, fy0 + 6), (fx1 + 40, fy0 + 6), (fx1 + 20, fy0 - 30), (fx0 - 20, fy0 - 30)])
    paint(img, roof.arr(0.6), "#8a5a34", hi=0.2, lo=0.1)
    for x in (fx0 + 10, fx1 - 10):
        bm = Mask()
        bm.rect(x - 14, fy0, x + 14, fy1)
        paint(img, bm.arr(0.6), "#7a4e2c", hi=0.14, lo=0.1)
    # 문 — 안은 어둡고, 상자 그림자와 따뜻한 빛이 비친다
    dx0, dx1, dy0, dy1 = 530, 750, 262, 500
    dm = Mask()
    dm.rect(dx0, dy0, dx1, dy1, r=10)
    da = dm.arr(0.6)
    over(img, bk.vgrad(H, W, [(0, "#3a2414"), (1, "#5a3a20")]), da)
    for k, (cx, w, h) in enumerate(((600, 70, 50), (676, 80, 60), (640, 60, 44))):
        cm = Mask()
        base = dy1 - 8 - (k == 2) * 60
        cm.rect(cx - w / 2, base - h, cx + w / 2, base)
        over(img, c("#6e4826"), cm.arr(0.8) * da * 0.9)
    add(img, c("#ffcf80"), radial(W, H, 650, 330, 110, 1.6) * da * 0.25)
    frame_m = Mask()
    frame_m.rect(dx0 - 16, dy0 - 18, dx1 + 16, dy1)
    frame_a = np.clip(frame_m.arr(0.6) - grow(da, 0.8), 0, 1)
    paint(img, frame_a, "#6a4224", hi=0.16, lo=0.1)
    door = Mask()   # 한쪽 문짝은 활짝 열려 비스듬히
    door.poly([(dx1 + 2, dy0 + 4), (dx1 + 70, dy0 + 26), (dx1 + 70, dy1 - 6), (dx1 + 2, dy1)])
    paint(img, door.arr(0.6), "#9c6a3e", hi=0.1, lo=0.16)
    # 모래가 정면 양옆을 덮었다
    sd = Mask()
    sd.poly([(0, 330), (220, 330), (420, 380), (500, 470), (560, 520), (0, 560)])
    sd.poly([(W, 320), (1080, 330), (880, 360), (790, 450), (740, 520), (W, 560)])
    sa = sd.arr(4)
    over(img, c("#b98a4a"), np.clip(grow(sa, 1.4) - sa, 0, 1) * 0.6)
    over(img, bk.vgrad(H, W, [(0, SAND), (1, SAND2)]), sa)
    add(img, c("#fff6dc"), rim(sa, 0, 5, 6) * 0.3)
    near = dune(img, 804, 500, 14, SAND2, SAND_SH, freq=(0.002, 0.006), hi=0.25)
    # 발자국이 문으로
    fp = Mask()
    for i in range(14):
        t = i / 13
        x = lerp(600, 640, t) + math.sin(t * 4) * 50 + (12 if i % 2 else -12) * lerp(1.0, 0.5, t)
        y = lerp(H - 20, 506, t ** 0.85)
        s = lerp(1.4, 0.5, t)
        fp.ellipse(x, y, 10 * s, 6 * s, 0.3 if i % 2 else -0.3)
    fpa = fp.arr(0.6)
    over(img, c("#b47c40"), fpa * 0.75)
    add(img, c("#fff6dc"), rim(fpa, 0, -2, 2) * 0.4)
    rp = Mask()   # 바람 무늬
    for k in range(8):
        y = 560 + k * 22
        rp.line([(x, y + 6 * math.sin(x * 0.02 + k)) for x in range(0, W + 20, 40)], 1.6)
    over(img, c("#d9a95f"), rp.arr(0.8) * near * 0.5)
    sand = Particles(70, 805, (0, 100, W, H), vel=((60, 140), (-6, 6)), size=(0.6, 1.4), wobble=10)

    def frame(u):
        f = img.copy()
        streaks(f, u, 806, 16, c("#fff3d0"), 0.4, vx=380.0, length=50.0, y0=300, y1=620)
        sand.draw(f, u, c("#fff0c8"), 0.5)
        z = ease_in_out(span(u, 0.5, 3.0))
        return zoom(f, 1.0 + 1.5 * z * z, 640, lerp(360, 384, z))

    return frame


# ══════════════════════════ 샷2 — 천장까지 쌓인 상자 ══════════════════════════

TH2 = 1500


def stack_shot():
    img = bk.vgrad(TH2, W, [(0, "#4a2e18"), (0.5, "#6b4628"), (1, "#7a5230")])
    n = fbm(W, TH2, 821, (160, 50), (0.7, 0.3))
    img *= (0.9 + 0.2 * n)[..., None]
    pl = Mask(W, TH2)
    for x in range(0, W, 64):
        pl.line([(x, 0), (x, TH2)], 2.4)
    over(img, c("#3a2414"), pl.arr(0.8) * 0.4)
    # 천장 들보와 채광 틈
    cm = Mask(W, TH2)
    cm.rect(0, 0, W, 70)
    for x in range(-40, W + 80, 220):
        cm.rect(x, 70, x + 34, 130)
    paint(img, cm.arr(0.6), "#3e2614", hi=0.1, lo=0.0)
    gap = Mask(W, TH2)
    gap.rect(420, 24, 760, 44, r=6)
    over(img, c("#fff2c8"), gap.arr(1.0))
    rng = np.random.default_rng(822)
    floor_y = TH2 - 60
    fm = Mask(W, TH2)
    fm.rect(0, floor_y, W, TH2)
    paint(img, fm.arr(0.6), "#a87a46", hi=0.1, lo=0.0)
    cols = [(640, 156, 112, 0.0), (470, 140, 104, 0.15), (810, 140, 104, 0.15), (306, 130, 98, 0.32),
            (974, 130, 98, 0.32), (146, 124, 92, 0.5), (1134, 124, 92, 0.5)]
    for x, w, h, shade in sorted(cols, key=lambda t: -t[3]):
        base = floor_y + 6
        k = 0
        while base > 120:
            hh = h * rng.uniform(0.9, 1.08)
            ww = w * rng.uniform(0.92, 1.04)
            fill = c(WOOD[rng.integers(0, 3)])
            fill = lerp(fill, c("#3a2414"), shade)
            crate(img, x + rng.uniform(-6, 6), base, ww, hh, fill=fill, plank=lerp(c("#a86e34"), c("#2a1a10"), shade),
                  tag=rng.random() < 0.8, seed=int(rng.integers(0, 999)))
            base -= hh + 2
            k += 1
    for bx, w0, w1, amt in ((560, 30, 160, 0.3), (660, 20, 120, 0.22)):
        bk.beam(img, bx, 30, bx - 260, TH2, w0, w1, "#ffe7b0", amt, soft=24)
    add(img, c("#fff0c8"), radial(W, TH2, 590, 40, 420, 1.8) * 0.3)
    motes = Particles(160, 823, (0, 0, W, TH2), vel=((-3, 3), (-6, 4)), size=(0.6, 1.6))

    def frame(u):
        y0 = lerp(TH2 - H, 0, ease_in_out(span(u, 0.35, 3.8)))
        f = bk.window(img, y0)
        motes.draw(f, u, c("#ffe9b8"), 0.5, oy=y0)
        return f

    return frame


# ══════════════════════════ 샷3 — 상자 줄, 텅 빈 상자 ══════════════════════════

TW3 = 2440


def row_shot():
    img = bk.vgrad(H, TW3, [(0, "#3e2614"), (0.45, "#6b4628"), (0.75, "#8a6036"), (1, "#a87a46")])
    n = fbm(TW3, H, 831, (160, 50), (0.7, 0.3))
    img *= (0.9 + 0.2 * n)[..., None]
    sh = Mask(TW3, H)   # 뒤 선반
    sh.rect(0, 318, TW3, 334)
    for x in range(60, TW3, 420):
        sh.rect(x, 318, x + 22, 548)
    paint(img, sh.arr(0.6), "#5a3a20", hi=0.12, lo=0.0)
    fl = Mask(TW3, H)
    fl.rect(0, 540, TW3, H)
    paint(img, fl.arr(0.6), "#b08250", hi=0.08, lo=0.0)
    rng = np.random.default_rng(832)
    x = 70
    while x < TW3:   # 뒤 줄 — 작고 어둡다
        w = rng.uniform(96, 120)
        bk.crate34(img, x, 318, w, w * 0.66, d=w * 0.3, open_=(1500 < x < 2000 and rng.random() < 0.5), tag=True,
                   fill=WOOD[rng.integers(0, 3)], seed=int(rng.integers(0, 999)), shade=0.45)
        x += w + 40
    opens = {3, 5, 6}   # 앞 줄에서 뚜껑이 열린 채 비어 있는 상자(카메라가 가운데를 지나갈 때 보인다)
    x = 120
    i = -1
    while x < TW3:   # 앞 줄
        i += 1
        w = rng.uniform(180, 210)
        is_open = i in opens
        bk.crate34(img, x, 552, w, 132, d=80, open_=is_open, tag=not is_open, fill=WOOD[rng.integers(0, 3)],
                   seed=int(rng.integers(0, 999)), shade=0.12)
        x += w + 70
    for bx in (500, 1300, 2000):
        bk.beam(img, bx, -20, bx - 220, H, 30, 140, "#ffe7b0", 0.22, soft=24)
    motes = Particles(120, 833, (0, 0, TW3, H), vel=((-3, 3), (-6, 4)), size=(0.6, 1.6))

    def frame(u):
        x0 = lerp(0, TW3 - W, ease_in_out(u / 3.6))
        f = bk.window(img, 0, x0)
        motes.draw(f, u, c("#ffe9b8"), 0.5, ox=x0)
        return f

    return frame


# ══════════════════════════ 샷4 — 너무 조용한 상자 ══════════════════════════

def quiet_shot():
    img = bk.vgrad(H, W, [(0, "#24160c"), (0.6, "#3a2414"), (1, "#4a2e18")])
    fl = Mask()
    fl.rect(0, 470, W, H)
    over(img, bk.vgrad(H, W, [(0, "#4a3018"), (1, "#5a3a20")]), fl.arr(2))
    add(img, c("#ffd890"), radial(W, H, 640, 470, 330, 1.4) * 0.45)
    bk.beam(img, 760, -40, 640, 480, 50, 170, "#ffe7b0", 0.35, soft=30)
    shm = Mask()
    shm.ellipse(660, 478, 190, 26)
    over(img, c("#1a0e06"), shm.arr(8) * 0.55)
    bk.crate34(img, 630, 478, 260, 176, tag=True, fill="#c48a4c", seed=847)
    for x in (300, 1000):   # 어둠 속 다른 상자들(흐릿하게)
        bk.crate34(img, x, 470, 200, 130, tag=False, fill="#7a5230", seed=x, shade=0.75)
    motes = Particles(50, 841, (480, 0, 860, 480), vel=((-2, 2), (-3, 2)), size=(0.7, 1.7), wobble=8)

    def frame(u):
        f = img.copy()
        motes.draw(f, u, c("#ffe9b8"), 0.6)
        return zoom(f, 1.0 + 0.05 * ease_in_out(u / 3.1), 640, 400)

    return frame


SHOTS = [entrance_shot, stack_shot, row_shot, quiet_shot]


def score(sc):
    """모래 바람 → 실내 울림, 발소리, 상자 삐걱, 끝은 거의 소리가 없는 정적 + 낮은 패드."""
    T = sc.total
    s = [sc.shot(k) for k in range(4)]
    sc.wind([(0, 0.0), (0.3, 1.0), (2.2, 1.0), (s[1] + 0.6, 0.15), (s[2], 0.08), (s[3], 0.0), (T, 0.0)], amp=0.07,
            lo=300, hi=3200, gust=0.8)
    sc.wind([(0, 0.0), (0.3, 0.6), (2.4, 0.5), (s[1] + 0.6, 0.0), (T, 0.0)], amp=0.03, lo=3000, hi=8000, gust=0.4)
    for k, at in enumerate((0.55, 0.95, 1.35, 1.75, 2.15)):
        sc.step(at, 0.07, soft=True)
    sc.room([(0, 0), (s[1], 0), (s[1] + 0.6, 1), (s[3], 0.8), (T, 0.6)], amp=0.035)
    for at in (s[1] + 0.5, s[1] + 0.95, s[1] + 1.4, s[2] + 0.3, s[2] + 0.75, s[2] + 1.2, s[2] + 1.65):
        sc.step(at, 0.05, soft=False)
    bk.creak(sc, s[1] + 1.6, 0.7, 0.04, f=170, pan=-0.4)
    bk.creak(sc, s[1] + 3.0, 0.5, 0.035, f=220, pan=0.5)
    bk.creak(sc, s[2] + 1.2, 0.8, 0.045, f=150, pan=0.2)
    sc.noise(s[1] + 2.2, 1.2, 1500, 5000, 0.015, decay=1.5, pan=0.3)   # 모래가 사르르
    # 낮은 패드 — a단조, 마지막엔 가장 낮은 음만 남는다
    sc.pad([hz("A2"), hz("E3"), hz("C4")], [(0, 0), (s[1], 0.0), (s[1] + 1.0, 0.7), (s[2] + 2.0, 0.8), (s[3], 0.3),
                                            (T, 0.0)], amp=0.03)
    sc.pad([hz("A1"), hz("E2")], [(0, 0), (s[3] - 0.4, 0), (s[3] + 0.8, 0.9), (T, 0.7)], amp=0.04, bright=0.1)
    sc.pluck(s[2] + 0.9, hz("E4"), 0.05, dur=2.0, pan=-0.2, kind="bell")
    sc.pluck(s[2] + 1.9, hz("C4"), 0.045, dur=2.0, pan=0.2, kind="bell")
