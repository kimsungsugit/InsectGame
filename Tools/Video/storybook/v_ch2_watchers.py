"""ch2 「감시자」 — 연못에서 검은 옷의 사내를 처음 만난 대사(`ch2_watchers`) **뒤**에 튼다. 연못(청록).

    샷1  0.0~3.0   연못 — 연잎·갈대·물결. 잠자리 한 마리가 물을 스치고 지나간다
    샷2  2.4~7.0   갈대 너머 명부회 사내의 뒷모습(3/4) — 작은 수첩에 눈금(||||)을 그어 센다. 수첩이 가운데
    샷3  6.4~10.0  사내가 돌아서 안개 속으로 걸어 들어가 흐려진다. 허리춤에 빈 채집통
"""
import math

import numpy as np

from bk_v1 import (Glints, Sprite, blur_rgb, clink, flutter, frog, hand_parts_rot, lily_pad, ripple_ring,
                   tally_strokes, water_static)
from kit import (INK, H, W, InsectSprite, Mask, Particles, bake_layer, blur_arr, book_sky, c, dot,
                 draw_hand, draw_person, ease_in_out, ease_out, fbm, grow, hills, lerp, mist, over, paint,
                 put_layer, reeds, smooth, span, stamp, tree, zoom)
from sound import hz

DURS = [3.0, 4.0, 3.0]
TINT = "#f2fbf6"
CUES = [
    (3.0, 3.0, "수첩에는 곤충 숫자만 가득했다."),
    (6.8, 2.6, "그는 안개 속으로 사라졌다."),
]

POND_SKY = [(0, "#6fc8ee"), (0.5, "#bfeaec"), (1, "#eefadf")]
BANK_Y = 300


def pond_bg(seed, reeds_on=True):
    """연못 배경 — 하늘, 건너편 둑과 나무, 물, 연잎(정적)."""
    img = book_sky(POND_SKY, seed=seed)
    hills(img, seed + 1, BANK_Y - 30, 12, "#a6d98f", ink="#6f9e72", freq=(0.002, 0.007), line=1.2, hi=0.06)
    for x, h, s in ((120, 120, 1), (260, 92, 2), (420, 130, 3), (880, 110, 4), (1010, 140, 5), (1180, 100, 6)):
        tree(img, x, BANK_Y - 14, h, seed=seed + s, leaf="#74c46e", leaf2="#4f9e5a")
    hills(img, seed + 2, BANK_Y, 6, "#8fcf78", ink="#5f8f62", freq=(0.003, 0.01), line=1.2, hi=0.05)
    water_static(img, BANK_Y + 6, H, "#86dad2", "#2f98a8", seed=seed + 3, lines=16, line_alpha=0.35)
    refl = Mask()
    refl.rect(0, BANK_Y + 6, W, BANK_Y + 46)
    over(img, c("#5fb3ad"), blur_arr(refl.arr(0.6), 10) * 0.45)
    pads = Mask()
    veins = Mask()
    for x, y, rx, ry, rot in ((210, 520, 92, 30, 0.1), (370, 610, 120, 38, -0.2), (150, 660, 84, 26, 0.6),
                              (985, 480, 74, 22, 2.4), (1090, 565, 108, 34, 1.9), (905, 650, 124, 40, 3.0),
                              (560, 455, 58, 17, 1.2), (735, 690, 96, 28, 0.4), (470, 380, 40, 11, 2.0),
                              (800, 400, 46, 13, 0.9)):
        lily_pad(pads, x, y, rx, ry, rot)
        for k in range(5):
            a = rot + 0.8 + k * 1.1
            veins.line([(x, y), (x + math.cos(a) * rx * 0.78, y + math.sin(a) * ry * 0.78)], 1.4)
    pa = pads.arr(0.5)
    paint(img, pa, "#6cc04a", hi=0.18, lo=0.12)
    over(img, c("#4f9e3a"), veins.arr(0.5) * pa * 0.5)
    for x, y, s in ((370, 592, 1.0), (1090, 548, 0.85)):
        lotus(img, x, y, s)
    if reeds_on:
        reeds(img, -20, 170, 560, seed=seed + 7, n=11, h=(200, 320), fill="#6fae3e")
        reeds(img, 1110, 1300, 540, seed=seed + 8, n=10, h=(180, 300), fill="#6fae3e")
    np.clip(img, 0, 1, out=img)
    return img


def lotus(img, x, y, s=1.0):
    m = Mask(140, 110)
    cx, cy = 70, 70
    for a in (-1.0, -0.5, 0.0, 0.5, 1.0):
        m.ellipse(cx + math.sin(a) * 26 * s, cy - math.cos(a) * 24 * s, 11 * s, 26 * s, a)
    paint(img, m.arr(0.5), "#ff9fc2", x, y - 10, hi=0.25, lo=0.1)
    dot(img, x, y - 4, 7 * s, c("#ffe27a"), 0.9)


# ══════════════════════════ 샷1 — 연못과 잠자리 ══════════════════════════

def pond_shot():
    bg = pond_bg(110)
    glints = Glints(26, 111, (0, BANK_Y + 20, W, H - 20), length=(26, 80))
    dfs = {k: InsectSprite("dfly", 48, rot=-math.pi / 2 + k * 0.25, frames=6) for k in (-1, 0, 1)}
    touch_u, tx, ty = 1.5, 640, 430

    def path(u):
        p = span(u, 0.15, 2.95)
        x = lerp(1360, -90, p)
        y = 220 + 212 * math.sin(math.pi * min(1.0, p * 1.0)) ** 2.2
        return x, y

    def frame(u):
        f = bg.copy()
        glints.draw(f, u, amount=0.42, drift=14)
        for k in range(2):
            kk = span(u, touch_u + k * 0.35, touch_u + 1.6 + k * 0.35)
            if 0 < kk < 1:
                ripple_ring(f, tx, ty + 4, 8 + 90 * ease_out(kk), 0.75 * (1 - kk), w=2.4)
        x, y = path(u)
        x2, y2 = path(u + 0.05)
        tilt = max(-1, min(1, round((y2 - y) / max(1.0, abs(x2 - x)) * 1.6)))
        if -60 < x < W + 60:
            dot(f, x, ty + 14 + (ty - y) * 0.15, 10, c("#1f6f7a"), -0.12 * smooth(1 - abs(y - ty) / 120))
            dfs[-tilt].draw(f, x, y, u, rate=40)
        return zoom(f, 1.0 + 0.04 * u / 3.0, W * 0.5, H * 0.55)

    return frame


# ══════════════════════════ 샷2 — 갈대 너머, 수첩의 눈금 ══════════════════════════

MAN_X, MAN_FEET, MAN_H = 425, H + 220, 680
NB_X, NB_Y, NB_W, NB_H = 586, 372, 236, 150     # 수첩 왼쪽 위·크기(펼친 두 쪽)
INK_TALLY = "#2e2430"


def _right_page_strokes():
    rows, out = 4, []
    for r in range(rows):
        y = NB_Y + 22 + r * 31
        out += tally_strokes(15 if r < 3 else 10, NB_X + NB_W / 2 + 12, y, 19, 5.6, 10)
    return out


def nape(d, x, feet, h, face):
    """모자 아래 뒤통수를 머리카락으로 덮는다 — kit 뒷모습은 챙 아래로 살색이 넓게 비쳐 얼굴처럼 읽힌다."""
    hr = h * 0.072
    hx, hy = x + face * h * 0.03, feet - h + hr
    m = Mask(int(hr * 4), int(hr * 4))
    m.ellipse(m.w / 2 - face * hr * 0.35, m.h / 2 + hr * 0.15, hr * 0.86, hr * 0.78)
    stamp(d, m.arr(0.6), hx, hy, "#2b2030")


def notebook_draw(d):
    """수첩(정적) — 남색 표지, 두 쪽, 왼쪽은 눈금으로 가득."""
    m = Mask()
    m.rect(NB_X - 8, NB_Y - 8, NB_X + NB_W + 8, NB_Y + NB_H + 8, r=8)
    paint(d, m.arr(0.5), "#2f3352", hi=0.1)
    pg = Mask()
    pg.rect(NB_X, NB_Y, NB_X + NB_W / 2 - 2, NB_Y + NB_H, r=3)
    pg.rect(NB_X + NB_W / 2 + 2, NB_Y, NB_X + NB_W, NB_Y + NB_H, r=3)
    over(d, c("#fbf3df"), pg.arr(0.5))
    rl = Mask()
    for r in range(5):
        y = NB_Y + 44 + r * 31
        rl.line([(NB_X + 6, y), (NB_X + NB_W / 2 - 8, y)], 1.0)
        rl.line([(NB_X + NB_W / 2 + 8, y), (NB_X + NB_W - 6, y)], 1.0)
    over(d, c("#bcd3e6"), rl.arr(0.4) * 0.8)
    tm = Mask()
    for r in range(4):
        y = NB_Y + 22 + r * 31
        for p0, p1 in tally_strokes(15, NB_X + 10, y, 19, 5.6, 10):
            tm.line([p0, p1], 2.4)
    over(d, c(INK_TALLY), tm.arr(0.4) * 0.9)
    sh = Mask()
    sh.line([(NB_X + NB_W / 2, NB_Y + 2), (NB_X + NB_W / 2, NB_Y + NB_H - 2)], 3)
    over(d, c("#c9b98f"), sh.arr(1.0) * 0.8)


def ledger_shot():
    bg = blur_rgb(pond_bg(120, reeds_on=False), 3.0)
    bg *= 0.97
    arm = ((NB_X + 20 - MAN_X) / MAN_H, (NB_Y + NB_H - 10 - MAN_FEET) / MAN_H)

    def man_and_book(d):
        draw_person(d, "ledger", MAN_X, MAN_FEET, MAN_H, t=0.0, face=0.3, arm=arm)
        nape(d, MAN_X, MAN_FEET, MAN_H, 0.3)
        notebook_draw(d)
        hp = hand_parts_rot(30, rot=0.5, pose="open", curl=0.9, sleeve_len=0.6)
        draw_hand(d, NB_X + 14, NB_Y + NB_H + 8, 30, parts=hp, skin="#f0c49c", sleeve="#2f3352")

    base = bake_layer(man_and_book)

    def front(d):
        reeds(d, -60, 300, H + 40, seed=125, n=12, h=(380, 640), fill="#5f9e36", tip="#8a5a26")
        reeds(d, 1010, 1340, H + 40, seed=126, n=12, h=(360, 620), fill="#5f9e36", tip="#8a5a26")
        reeds(d, 330, 420, H + 40, seed=127, n=3, h=(260, 330), fill="#5f9e36", tip="#8a5a26")

    fp, fa = bake_layer(front)
    fp, fa = blur_rgb(fp, 2.4), blur_arr(fa, 2.4)
    pen = Sprite(_pen_hand, 220, 220, origin=(60, 120))
    strokes = _right_page_strokes()
    done0 = 10                                  # 첫 줄 앞 두 묶음은 이미 적혀 있다
    t_start, period = 0.5, 0.27
    motes = Particles(18, 128, (300, 120, 1000, 560), vel=((-3, 3), (-5, -1)), size=(0.7, 1.4))

    def frame(u):
        f = bg.copy()
        put_layer(f, *base)
        n_live = (u - t_start) / period
        nb = Mask(NB_W + 40, NB_H + 40)
        ox, oy = NB_X - 20, NB_Y - 20
        tip = None
        for i, (p0, p1) in enumerate(strokes):
            if i < done0:
                k = 1.0
            else:
                j = i - done0
                k = span(n_live, j, j + 0.55)
                if 0 < k < 1 or (tip is None and n_live < j):
                    tip = (lerp(p0[0], p1[0], k), lerp(p0[1], p1[1], k)) if k > 0 else (p0[0], p0[1] - 6)
                if k <= 0:
                    continue
            q1 = (lerp(p0[0], p1[0], k), lerp(p0[1], p1[1], k))
            nb.line([(p0[0] - ox, p0[1] - oy), (q1[0] - ox, q1[1] - oy)], 2.4)
        stamp(f, nb.arr(0.4) * 0.9, NB_X + NB_W / 2, NB_Y + NB_H / 2, INK_TALLY)
        if tip is None:
            tip = (strokes[-1][1][0] + 8, strokes[-1][1][1] - 10)
        pen.blit(f, tip[0], tip[1])
        motes.draw(f, u, c("#ffffff"), 0.3)
        put_layer(f, fp, fa)
        z = 1.0 + 0.07 * ease_in_out(u / 4.6)
        return zoom(f, z, W * 0.5, NB_Y + NB_H * 0.4)

    return frame


def _pen_hand(d):
    """연필을 쥔 오른손(수첩 아래 오른쪽에서 올라온다) — 연필 끝이 상자의 (60,120)."""
    tx, ty = 60, 120
    ex, ey = 118, 48
    pm = Mask(220, 220)
    pm.line([(tx + 3, ty - 5), (ex, ey)], 7)
    paint(d, pm.arr(0.5), "#f2c94c", ink=INK, line=1.2, hi=0.2)
    tm = Mask(220, 220).poly([(tx, ty), (tx + 7, ty - 9), (tx - 1, ty - 11)]).arr(0.4)
    over(d, c("#3b2a1e"), grow(tm, 1.0))
    r = -0.62
    hp = hand_parts_rot(28, rot=r, pose="open", curl=0.85, sleeve_len=2.6)
    fx, fy = lerp(tx, ex, 0.44), lerp(ty, ey, 0.44)
    draw_hand(d, fx - math.sin(r) * 0.62 * 28, fy + math.cos(r) * 0.62 * 28, 28, parts=hp, skin="#f0c49c",
              sleeve="#2f3352")


# ══════════════════════════ 샷3 — 안개 속으로 ══════════════════════════

WALK_H = 400


def _walker(face, walk):
    """사내 + 허리춤 채집통(걸음에 흔들린다)을 굽는다. 상자 아래 가운데가 발."""
    bw, bh = int(WALK_H * 1.4) + 20, int(WALK_H * 1.5) + 20

    def draw(d):
        x, feet = bw / 2, bh - 10
        draw_person(d, "ledger", x, feet, WALK_H, t=walk or 0.0, face=face, walk=walk)
        nape(d, x, feet, WALK_H, face)
        sw = math.sin(walk) if walk is not None else 0.0
        cx = x + WALK_H * 0.15 * (1 - abs(face))
        cy = feet - WALK_H * 0.43
        m = Mask(bw, bh)
        m.line([(x - WALK_H * 0.06, feet - WALK_H * 0.74), (cx, cy - WALK_H * 0.06)], 3.0)
        over(d, c("#5a4632"), grow(m.arr(0.5), 0.8))
        can = Mask(bw, bh)
        ang = sw * 0.18
        can.poly([(cx + (px * math.cos(ang) - py * math.sin(ang)), cy + (px * math.sin(ang) + py * math.cos(ang)))
                  for px, py in ((-14, -24), (14, -24), (15, 26), (-15, 26))])
        paint(d, can.arr(0.5), "#b9c9c6", hi=0.25, lo=0.15)
        lid = Mask(bw, bh).ellipse(cx - 24 * math.sin(ang), cy - 26 * math.cos(ang), 16, 6, ang).arr(0.5)
        paint(d, lid, "#8fa6a3", hi=0.2, lo=0.0)

    return Sprite(draw, bw, bh, origin=(bw / 2, bh - 10))


def fog_shot():
    img = book_sky([(0, "#a8d6dc"), (0.55, "#d6ece6"), (1, "#eef6ee")], seed=301)
    hz_y = 330
    hills(img, 302, hz_y - 10, 10, "#b9d6c8", ink="#9cb8ad", freq=(0.002, 0.008), line=1.0, hi=0.03)
    for x, h, s in ((180, 120, 1), (360, 90, 2), (930, 110, 3), (1100, 130, 4)):
        tree(img, x, hz_y - 4, h, seed=310 + s, leaf="#a9cfba", leaf2="#94bba6", trunk="#a99a86")
    water_static(img, hz_y, H, "#9fd6d0", "#4fa5ae", seed=303, lines=14, line_alpha=0.3)
    path = Mask()
    path.poly([(640 - 14, hz_y + 2), (640 + 14, hz_y + 2), (640 + 250, H + 10), (640 - 250, H + 10)])
    pa = path.arr(0.8)
    paint(img, pa, "#d9c79e", ink="#8a7a5a", line=1.0, hi=0.05, lo=0.0)
    edge = Mask()
    r = np.random.default_rng(304)
    for _ in range(60):
        yy = r.uniform(hz_y + 10, H)
        k = (yy - hz_y) / (H - hz_y)
        for sg in (-1, 1):
            xx = 640 + sg * (14 + 236 * k) + r.uniform(-10, 10) * k
            edge.ellipse(xx, yy, 6 + 18 * k, 3 + 7 * k, 0)
    paint(img, edge.arr(0.5), "#7fbf55", line=1.0, hi=0.1)
    reeds(img, 0, 300, 640, seed=305, n=9, h=(180, 300), fill="#7aae4a")
    reeds(img, 980, 1290, 650, seed=306, n=9, h=(170, 290), fill="#7aae4a")
    yy = np.arange(H, dtype=np.float32)[:, None]
    fog_v = np.clip(1 - np.abs(yy - hz_y) / 230, 0, 1) ** 1.3 * np.ones((1, W), np.float32)
    over(img, c("#eef5f3"), fog_v * 0.55)
    np.clip(img, 0, 1, out=img)
    fog_n = fbm(W, H, 307, (260, 90), (0.7, 0.3))
    fog_field = np.clip(0.35 + 0.8 * (fog_n - 0.5), 0, 1) * np.clip(1 - np.abs(yy - 380) / 260, 0, 1)
    turn = [_walker(0.55, None), _walker(0.25, None)]
    walk = [_walker(0.0, 2 * math.pi * k / 8) for k in range(8)]

    def frame(u):
        f = img.copy()
        wk = span(u, 0.55, 3.6)
        d = ease_in_out(wk) * 0.6 + wk * 0.4
        feet = lerp(600, 438, d)
        sc = lerp(1.0, 0.42, d)
        alpha = 1 - smooth(span(u, 2.1, 3.4))
        if u < 0.55:
            spr = turn[0] if u < 0.3 else turn[1]
            spr.blit(f, 640, feet, scale=sc, alpha=alpha)
        else:
            ph = (u - 0.55) * 1.7 * 8
            walk[int(ph) % 8].blit(f, 640, feet, scale=sc, alpha=alpha)
        over(f, c("#eef5f3"), fog_field * (0.15 + 0.55 * smooth(span(u, 0.8, 3.4))))
        mist(f, 308, u, 300, 500, color="#f4f9f7", amount=0.35, speed=16)
        return f

    return frame


SHOTS = [pond_shot, ledger_shot, fog_shot]


def score(sc):
    """물·개구리·잠자리 날갯소리 → 펜 긁는 소리 → 걸음·빈 통 달각 → 낮은 패드."""
    T = sc.total
    s = [sc.shot(k) for k in range(3)]
    sc.water([(0, 0.0), (0.4, 1.0), (s[1] + 0.6, 0.45), (s[2], 0.45), (T, 0.25)], amp=0.035)
    sc.wind([(0, 0), (s[2], 0.2), (s[2] + 1.5, 0.8), (T, 0.6)], amp=0.03, lo=200, hi=1000)
    frog(sc, 0.5, 0.09, 150, pan=-0.5, croaks=2)
    frog(sc, s[1] + 0.9, 0.06, 170, pan=0.6, croaks=1)
    for k in range(6):                                             # 샷1 — 잠자리가 오른쪽에서 왼쪽으로
        flutter(sc, 0.3 + k * 0.42, 0.45, 0.035, rate=38, lo=150, hi=900, pan=0.8 - k * 0.32)
    sc.drip(1.5, 0.1, f=700)
    sc.pad([hz("A2"), hz("E3"), hz("C4")], [(0, 0), (s[1], 0), (s[1] + 1.2, 0.5), (s[2], 0.55), (T - 1.5, 0.8), (T, 0)],
           amp=0.045, bright=0.15)
    sc.pad([hz("D3"), hz("A3"), hz("F#4")], [(0, 0), (0.8, 0.45), (s[1] + 0.6, 0.35), (s[1] + 1.4, 0), (T, 0)],
           amp=0.035)
    t_start, period = 0.5, 0.27                                     # 샷2 — 눈금 한 획마다 사각
    for j in range(15):
        at = s[1] + t_start + j * period
        sc.noise(at, 0.11, 2500, 7500, 0.07 * (0.8 + 0.4 * ((j * 7) % 5) / 5), decay=26, pan=0.15)
    for j in range(12):                                             # 샷3 — 걸음, 빈 통이 달각
        at = s[2] + 0.55 + j / (1.7 * 2)
        if at > T - 0.4:
            break
        amp = 0.11 * (1 - j / 13)
        sc.step(at, amp, soft=True)
        if j % 2 == 0:
            clink(sc, at + 0.05, 0.06 * (1 - j / 13), pan=0.1)
