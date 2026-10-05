"""ch9 「얼음 서고」 — ch9_confront 대사 **뒤**(곧바로 저울과 대결)에 튼다. 서릿길(하늘색·흰색).

직전 대사: 저울 "이 서고 목록을 만든 게 너였지. 그럼 뭐가 달라졌지?" 세라가 입을 열었다 닫는다. 세라의 대답은 아직 없다 —
그래서 영상도 대답하지 않는다(손이 반영에 닿다가 멈추고, 입김이 그 위를 덮는다).

    샷1  0.0~3.0   얼음 벽 속에 멈춘 곤충들(눈 감은 채), 하얀 빛이 얼음 위를 쓸고 간다
    샷2  2.4~6.0   얼음 벽 앞에 세라(옆모습)와 저울(옆모습·모자)이 마주 선다 — 입김만 오간다
    샷3  5.4~10.0  얼음에 비친 회상(세피아 + 서리 테두리) — 어린 세라의 손이 긴 목록을 한 줄씩 적어 내려간다
    샷4  9.4~14.0  지금의 세라 손이 그 반영에 닿다가 멈춘다. 얼음에 입김이 서려 반영이 흐려진다
"""
import math

import numpy as np

from kit import (INK, H, W, Mask, Particles, add, blur_arr, c, draw_person, ease_in_out, ease_out, fbm, grow,
                 lerp, local, over, person_parts, radial, rim, scribble, smooth, span, stamp, zoom)
import bk_v2 as bk
from silhouette_kit import sprite
from sound import hz

DURS = [3.0, 3.0, 4.0, 4.0]
TINT = "#f4f9ff"
REVERB = 0.4
CUES = [
    (1.0, 2.6, "얼음 속에 곤충들이 멈춰 있다."),
    (3.6, 2.2, "세라와 저울이 마주 섰다."),
    (6.0, 3.0, "예전에 이 목록을 적은 건 세라였다."),
    (10.2, 3.0, "세라는 아직 대답하지 못했다."),
]

ICE = [(0, "#f2fbff"), (0.45, "#cfeaf8"), (1, "#a6d3ee")]
FROZEN = [("bfly", 640, 286, 66, 0.1), ("beetle", 492, 440, 40, 0.5), ("dfly", 812, 170, 52, -0.6),
          ("longhorn", 904, 430, 40, -0.3), ("moth", 372, 214, 46, 0.25), ("firefly", 742, 470, 32, 0.9),
          ("mantis", 1086, 250, 44, 0.3), ("ant", 236, 420, 30, -0.5), ("bfly", 170, 190, 38, -0.4),
          ("dfly", 1120, 470, 40, 0.7), ("beetle", 560, 120, 30, -0.8), ("moth", 990, 110, 34, 0.5)]


def ice_wall(seed, insects=FROZEN, scale=1.0, dy=0.0, rows=4, cols=5):
    """얼음 벽 — 서로 다른 푸른빛의 얼음 덩어리를 쌓고, 덩어리 속에 곤충이 잠들어(눈 감음) 갇혀 있다."""
    img = bk.vgrad(H, W, [(0, "#d9f0fc"), (0.5, "#a8d4ef"), (1, "#86bde2")])
    r = np.random.default_rng(seed + 1)
    P = [[(W * j / cols + (r.uniform(-40, 40) if 0 < j < cols else 0), H * i / rows + (r.uniform(-30, 30) if 0 < i < rows else 0))
          for j in range(cols + 1)] for i in range(rows + 1)]
    blocks = Mask()
    seam = Mask()
    tints = ("#e8f7ff", "#d4eefb", "#c4e6f8", "#dcf2fd")
    for i in range(rows):
        for j in range(cols):
            q = [P[i][j], P[i][j + 1], P[i + 1][j + 1], P[i + 1][j]]
            cx = sum(p[0] for p in q) / 4
            cy = sum(p[1] for p in q) / 4
            qq = [(cx + (px - cx) * 0.985 - 3 * np.sign(px - cx), cy + (py - cy) * 0.985 - 3 * np.sign(py - cy))
                  for px, py in q]
            bm = Mask()
            bm.poly(qq)
            ba = bm.arr(0.8)
            over(img, c(tints[(i * 3 + j) % 4]), ba * 0.75)
            add(img, c("#ffffff"), rim(ba, -4, -4, 5) * 0.55)              # 덩어리 위·왼 모서리가 빛난다
            over(img, c("#6fa3c8"), rim(ba, 3, 3, 3) * 0.45)
            seam.poly(q)
    n = fbm(W, H, seed, (220, 70, 20), (0.55, 0.3, 0.15))
    add(img, c("#ffffff"), np.clip(n - 0.55, 0, 1)[..., None] * 0.3)
    for kind, x, y, s, rot in insects:
        bk.draw_insect_asleep(img, kind, 640 + (x - 640) * scale, dy + 320 + (y - 320) * scale, s * scale, rot=rot)
    over(img, c("#e2f3fc"), np.full((H, W), 0.3, np.float32))     # 얼음 막 — 곤충이 얼음 속에 있다
    cr = Mask()
    for _ in range(22):  # 가는 금
        x, y = r.uniform(0, W), r.uniform(0, H)
        a = r.uniform(0, math.pi)
        pts = [(x, y)]
        for _ in range(4):
            a += r.uniform(-0.6, 0.6)
            ln = r.uniform(14, 40)
            x, y = x + math.cos(a) * ln, y + math.sin(a) * ln
            pts.append((x, y))
        cr.line(pts, 1.3)
    over(img, c("#7fb2d6"), cr.arr(0.5) * 0.5)
    bub = Mask()
    for _ in range(40):
        x, y, rr = r.uniform(0, W), r.uniform(0, H), r.uniform(2, 6)
        bub.d.ellipse([(x - rr) * 2, (y - rr) * 2, (x + rr) * 2, (y + rr) * 2], outline=255, width=2)
    add(img, c("#ffffff"), bub.arr(0.4) * 0.6)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    for p0, wdt in ((300, 70), (900, 40), (1500, 90)):   # 사선 얼음 결
        add(img, c("#ffffff"), np.exp(-((xx * 0.8 + yy * 0.6 - p0) / wdt) ** 2)[..., None] * 0.12)
    return img


# ══════════════════════════ 샷1 — 얼음 속에 멈춘 곤충들 ══════════════════════════

def frozen_shot():
    img = ice_wall(901)
    add(img, c("#ffffff"), radial(W, H, 640, -60, 700, 1.6) * 0.18)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    diag = xx * 0.75 + yy * 0.66
    snow = Particles(70, 902, (0, 0, W, H), vel=((-8, 4), (18, 34)), size=(0.8, 2.0), wobble=10)

    def frame(u):
        f = img.copy()
        pos = lerp(-300, 1900, ease_in_out(span(u, 0.2, 2.8)))
        add(f, c("#ffffff"), np.exp(-((diag - pos) / 70) ** 2)[..., None] * 0.35)
        snow.draw(f, u, c("#ffffff"), 0.7)
        return zoom(f, 1.0 + 0.06 * ease_in_out(u / 3.0), 640, 300)

    return frame


# ══════════════════════════ 샷2 — 마주 선 두 사람 ══════════════════════════

def face_off_shot():
    behind = [b for b in FROZEN if not (440 < b[1] < 960 and b[2] > 380)]   # 두 사람 머리·어깨 뒤는 비운다
    wall = ice_wall(911, insects=behind, scale=0.7, dy=-90)
    img = wall.copy()
    fl = Mask()
    fl.rect(0, 470, W, H)
    fa = fl.arr(1.0)
    over(img, bk.vgrad(H, W, [(0, "#d6eefa"), (1, "#b8dcf0")]), fa)
    add(img, c("#ffffff"), np.clip(grow(fa, 1.2) - fa, 0, 1) * 0.6)
    sx, jx, feet = 548, 738, 548
    sh = Mask()
    for x in (sx, jx):
        sh.ellipse(x, feet + 4, 54, 9)
    over(img, c("#6f9cc0"), sh.arr(4) * 0.4)
    period = 2 * math.pi / 2.2
    sera = [person_parts("sera", 252, t=period * k / 8, face=1.0) for k in range(8)]
    jeoul = [person_parts("jeoul", 268, t=period * k / 8 + 1.0, face=-1.0) for k in range(8)]
    snow = Particles(60, 912, (0, 0, W, H), vel=((-8, 4), (18, 34)), size=(0.8, 2.0), wobble=10)
    puffs = [(0.7, sx + 40, feet - 232, 1), (1.5, jx - 44, feet - 248, -1), (2.3, sx + 40, feet - 232, 1),
             (3.0, jx - 44, feet - 248, -1)]

    def frame(u):
        f = img.copy()
        k = int((u / period) * 8) % 8
        for parts, x, who, hh, fc in ((sera[k], sx, "sera", 252, 1.0), (jeoul[k], jx, "jeoul", 268, -1.0)):
            draw_person(f, who, x, feet, hh, parts=parts)
            bk.profile(f, who, x, feet, hh, fc)
        for t0, x, y, d in puffs:   # 입김
            q = span(u, t0, t0 + 1.2)
            if 0 < q < 1:
                for j in range(3):
                    r_ = 6 + 14 * q + j * 3
                    stamp(f, sprite(r_) * 0.5 * (1 - q) * (1 - j * 0.25), x + d * (10 + 34 * q + j * 8),
                          y - 8 * q - j * 3, "#ffffff")
        snow.draw(f, u, c("#ffffff"), 0.7)
        return zoom(f, 1.16 + 0.05 * ease_in_out(u / 3.6), 643, 400)

    return frame


# ══════════════════════════ 샷3 — 얼음에 비친 회상 ══════════════════════════

ROW_H = 46
PX0, PW_ = 400, 480       # 종이 왼쪽·폭
ROWS = 40


def _list_paper():
    """긴 목록 — 칸마다 네모 칸 + 판독 불가 이름 + 작은 눈금. (빈 종이, 다 쓴 종이)를 돌려준다."""
    hh = ROWS * ROW_H + 200
    blank = np.zeros((hh, W, 3), np.float32) + c("#8a6a48")     # 책상
    n = fbm(W, hh, 931, (200, 40), (0.7, 0.3))
    blank *= (0.9 + 0.2 * n)[..., None]
    gr = Mask(W, hh)
    for y in range(0, hh, 23):
        gr.line([(0, y + 8 * math.sin(y * 0.02)), (W, y + 8 * math.sin(y * 0.02 + 1))], 1.2)
    over(blank, c("#6e5238"), gr.arr(0.8) * 0.3)
    pm = Mask(W, hh)
    pm.rect(PX0, -20, PX0 + PW_, hh + 20)
    pa = pm.arr(0.6)
    over(blank, c("#5a4030"), blur_arr(pa, 10) * 0.4)
    over(blank, c(INK), grow(pa, 1.4))
    over(blank, c("#fbf3df"), pa)
    lm = Mask(W, hh)
    for k in range(ROWS + 4):
        y = 40 + k * ROW_H + ROW_H * 0.8
        lm.line([(PX0 + 16, y), (PX0 + PW_ - 16, y)], 1.2)
    lm.line([(PX0 + 60, 0), (PX0 + 60, hh)], 1.4)
    over(blank, c("#b9cde0"), lm.arr(0.6) * 0.8)
    full = blank.copy()
    sm, bm = Mask(W, hh), Mask(W, hh)
    for k in range(ROWS):
        y = 40 + k * ROW_H
        bm.d.rectangle([(PX0 + 24) * 2, (y + 14) * 2, (PX0 + 44) * 2, (y + 34) * 2], outline=255, width=3)
        scribble(sm, PX0 + 74, y + 22, PW_ * 0.58, 1, 940 + k, row_h=ROW_H, weight=2.0)
        for j in range(1 + k % 4):
            sm.line([(PX0 + PW_ - 70 + j * 9, y + 14), (PX0 + PW_ - 70 + j * 9, y + 32)], 2.0)
        sm.line([(PX0 + 26, y + 24), (PX0 + 32, y + 31), (PX0 + 44, y + 12)], 2.4)
    over(full, c("#4a3a5c"), sm.arr(0.4))
    over(full, c("#4a3a5c"), bm.arr(0.4) * 0.8)
    return blank, full


def memory_shot():
    blank, full = _list_paper()
    hh = blank.shape[0]
    mem = bk.MemoryFrame(seed=93, paper="#e2f3fc", edge="#7fb3d6", frost=True, vig=0.45)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    sheen = np.exp(-((xx * 0.8 + yy * 0.6 - 520) / 120) ** 2) + 0.6 * np.exp(-((xx * 0.8 + yy * 0.6 - 980) / 60) ** 2)
    rows_per_s = 2.0
    pen_row_y = 400.0

    def frame(u):
        prog = 4 + u * rows_per_s           # 지금 쓰는 줄(소수 = 그 줄을 쓴 정도)
        row = int(prog)
        frac = prog - row
        y_row = 40 + row * ROW_H
        oy = y_row - pen_row_y               # 지금 줄이 화면 pen_row_y에 오게 종이를 올린다
        oy = int(max(0.0, oy))
        # 위쪽(다 쓴 줄)은 full, 아래(아직)는 blank — 화면 창만 정수 줄로 잘라 합성한다
        split = int(y_row - oy)
        f = blank[oy:oy + H].copy()
        if split > 0:
            f[:min(H, split)] = full[oy:oy + min(H, split)]
        lm = local(PW_, ROW_H + 10)          # 지금 줄 — 앞에서부터 써 내려간다
        scribble(lm, lm.w / 2 - PW_ / 2 + 74, lm.h / 2 - ROW_H / 2 + 22, PW_ * 0.58, 1, 940 + row, row_h=ROW_H,
                 weight=2.0, progress=frac)
        la = lm.arr(0.4)
        cxr, cyr = PX0 + PW_ / 2, y_row - oy + ROW_H / 2
        stamp(f, la, cxr, cyr, "#4a3a5c")
        # 펜 끝 — 쓴 길이만큼 오른쪽으로
        tipx = PX0 + 74 + PW_ * 0.58 * frac * 0.62 + 8 * math.sin(u * 22)
        tipy = y_row - oy + 22 + 3 * math.sin(u * 31)
        bk.tool_hand(f, (tipx, tipy), 0.8, 116, 44, "pen", skin="#f6c9a0", sleeve="#f0e6c8")
        f = bk.sepia(f, 0.85)
        f = f * 0.88 + c("#cfe8f6") * 0.12          # 얼음에 비친 — 차가운 기운
        add(f, c("#ffffff"), sheen[..., None] * 0.16)
        f = mem.apply(f, u, flicker=0.02, specks=3)
        return zoom(f, 1.03 - 0.03 * u / 4.6, 640, 360)

    return frame


# ══════════════════════════ 샷4 — 닿다가 멈춘 손, 입김 ══════════════════════════

def touch_shot():
    base = ice_wall(941, insects=[], scale=1.0)
    blank, full = _list_paper()
    refl = bk.window(full, 40 + 9 * ROW_H - 380)
    refl = bk.sepia(refl, 0.85)
    rm = Mask()
    rm.rect(330, 60, 950, 520, r=80)
    ra = rm.arr(30) * 0.42
    img = base.copy()
    over(img, refl, ra)                       # 얼음 속에 남은 흐린 반영
    over(img, c("#e4f4fd"), ra * 0.35)
    add(img, c("#ffffff"), radial(W, H, 640, 0, 700, 1.6) * 0.25)
    fog_c = (612, 300)

    def frame(u):
        f = img.copy()
        fog = smooth(span(u, 1.5, 2.7)) * (1 - 0.35 * smooth(span(u, 3.2, 4.6)))
        if fog > 0:   # 입김이 얼음에 서린다
            r_ = 60 + 150 * ease_out(span(u, 1.5, 2.7))
            fm = local(r_ * 2.6, r_ * 2.0)
            fm.ellipse(fm.w / 2, fm.h / 2, r_, r_ * 0.7)
            stamp(f, blur_arr(fm.arr(0.6), r_ * 0.35) * 0.75 * fog, *fog_c, "#f4fbff")
        reach = ease_out(span(u, 0.2, 2.0))
        back = ease_in_out(span(u, 2.6, 3.8)) * 26
        wx = lerp(1010, 730, reach) + back * 0.8
        wy = lerp(840, 452, reach) + back
        curl = 0.4 * smooth(span(u, 2.0, 2.7))
        bk.reach_hand(f, wx, wy, 70, -0.62, curl=curl, skin="#f6c9a0", sleeve="#e9dcb8")
        return zoom(f, 1.0 + 0.05 * ease_in_out(u / 4.6), 640, 340)

    return frame


SHOTS = [frozen_shot, face_off_shot, memory_shot, touch_shot]


def score(sc):
    """얼음 바람 · 쩡 하는 얼음 소리 · 회상은 오르골 · 끝은 가라앉는 패드."""
    T = sc.total
    s = [sc.shot(k) for k in range(4)]
    sc.wind([(0, 0.6), (s[1], 1.0), (s[2], 0.4), (s[3], 0.7), (T, 0.5)], amp=0.035, lo=1500, hi=6000, gust=0.6)
    sc.wind([(0, 0.8), (T, 0.6)], amp=0.03, lo=150, hi=600, gust=0.4)
    bk.ice_crack(sc, 0.35, 0.08, pan=-0.3)
    for k, nt in enumerate(("E6", "B5", "G5")):
        sc.chime(0.9 + k * 0.5, hz(nt), 0.04, dur=2.2, pan=-0.5 + k * 0.5)
    sc.pad([hz("E5"), hz("B5")], [(0, 0), (0.6, 0.8), (s[1] + 0.5, 0.5), (s[2], 0), (T, 0)], amp=0.015, bright=0.1)
    # 마주 섬 — 낮은 긴장
    sc.pad([hz("E2"), hz("B2"), hz("G3")], [(0, 0), (s[1], 0), (s[1] + 0.8, 0.8), (s[2] + 0.4, 0.3), (s[3], 0.5),
                                            (T, 0)], amp=0.035)
    bk.ice_crack(sc, s[1] + 1.5, 0.06, pan=0.4)
    for at, pan in ((s[1] + 0.7, -0.3), (s[1] + 1.5, 0.3), (s[1] + 2.3, -0.3)):
        bk.breath(sc, at, 1.0, 0.025, pan)
    # 회상 — 오르골 + 펜
    mel = [("A5", 1), ("C6", 1), ("E6", 1), ("D6", 1), ("C6", 1), ("B5", 1), ("A5", 2), ("E5", 1), ("G5", 1),
           ("A5", 2)]
    sc.melody(s[2] + 0.3, mel, step=0.34, amp=0.07, kind="box", pan=0.1)
    sc.scratch(s[2] + 0.2, 4.0, 0.022)
    # 손이 멈추고, 입김, 가라앉는 패드(e단조 → d단조로 내려앉는다)
    sc.pad([hz("E3"), hz("G3"), hz("B3")], [(0, 0), (s[3], 0), (s[3] + 0.6, 0.7), (s[3] + 2.2, 0.0), (T, 0)],
           amp=0.03)
    sc.pad([hz("D3"), hz("F3"), hz("A3"), hz("D2")], [(0, 0), (s[3] + 1.6, 0), (s[3] + 2.8, 0.8), (T, 0.6)],
           amp=0.03)
    bk.breath(sc, s[3] + 1.5, 1.4, 0.04, 0.0)
    sc.pluck(s[3] + 2.1, hz("A4"), 0.04, dur=2.0, kind="bell")
    sc.pluck(s[3] + 2.6, hz("F4"), 0.035, dur=2.4, kind="bell")
