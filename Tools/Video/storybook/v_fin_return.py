"""fin 「이름이 돌아간다」 — 최종전 승리 뒤(`fin_seal`) **대사 앞**에 튼다(전투 결과 화면 다음).

이긴 뒤 무슨 일이 일어났는지를 영상이 보여 준다: 그림자에게서 빌린 모습들이 빛으로 빠져나와 주인에게 돌아가고,
그림자는 울타리 밖으로 밀려나고, 마지막 구멍에 말뚝이 서며 들판에 색이 돌아온다. 끝은 초록 사마귀 한 마리와 해돋이.
대사는 그 뒤에 "마지막 칸이 메워졌어요!"로 받는다.

    샷1  0.0~2.8   빠져나온다 — 나비·반딧불이·사마귀 모양의 빛이 그림자에서 터져 나오고 그림자가 쪼그라든다
    샷2  2.2~5.8   돌아간다 — 빛이 들판으로 흩어진다(나비는 오른쪽 꽃밭, 반딧불이는 아래 연못). 이름표가 하나씩 켜진다
    샷3  5.2~8.8   밀려난다 — 작아진 그림자가 울타리 바깥 안개로 밀려나며 눈을 한 번 깜빡하고 사라진다
    샷4  8.2~11.8  메워진다 — 마지막 구멍에 새 말뚝이 쑥 서고 이름표가 빛난다. 들판에 색이 퍼진다
    샷5 11.2~14.6  초록 사마귀 — 그 자리 풀잎 위, 해가 뜬다(자막 없음)
"""
import math

import numpy as np

from sound import hz
from kit import (INK, PAL, H, W, InsectSprite, Mask, Particles, add, bake_layer, bezier, book_sky, bush, c, dot,
                 draw_insect, draw_person, ease_in_out, ease_out, grow, hills, insect_parts, lerp,
                 meadow, over, paint_plate, person_parts, plate_rect, post_pts, put_layer, rays_texture, smooth,
                 span, stamp, sun, water, zoom)
from bk_v4 import (FENCE_KINDS, LightBug, Piece, TwoLayer, bloom_from, dist_field, draw_shadow, fence_with_gap,
                   glow_spot, lit_plates, ring, spark, wobble_region)

DURS = [2.8, 3.0, 3.0, 3.0, 2.8]
TINT = "#fff6e6"
CUES = [
    (0.5, 2.2, "빼앗긴 이름들이 빠져나온다."),
    (2.9, 2.6, "이름은 하나씩 주인에게 돌아갔다."),
    (5.9, 2.6, "그림자는 울타리 밖으로 밀려났다."),
    (8.9, 2.6, "마지막 구멍까지, 이제 다 메워졌다."),
]

KINDS = FENCE_KINDS
GOLD = "#ffd86a"


def close_geom():
    return dict(base=640.0, pw=78.0, ph=560.0, left=[130.0, 470.0], right=[810.0, 1150.0], gap=640.0)


def close_layers(seed):
    g = close_geom()
    return TwoLayer(seed, 380, lambda cv: fence_with_gap(cv, g["left"], g["gap"], g["right"], g["base"], g["pw"],
                                                          g["ph"], seed + 5, flowers=24), color=False), g


# ══════════════════════════ 샷1 — 빠져나온다 ══════════════════════════

def shot_burst():
    L, g = close_layers(1011)
    cx, cy = 640.0, 300.0
    lights = [  # (빛, 나오는 시각, 도착점)
        (LightBug("mantis", 74), 0.42, (640.0, 118.0), 0),
        (LightBug("bfly", 84, flaps=(0.0, 0.25, 0.5)), 0.5, (790.0, 196.0), 1),
        (LightBug("firefly", 74, color="#fff27a"), 0.58, (488.0, 452.0), 0),
    ]
    sparks = np.random.default_rng(1012).uniform(-1, 1, (36, 3))
    motes = Particles(30, 1013, (0, 0, W, H), vel=((-5, 5), (-8, -2)), size=(0.8, 1.8))

    def frame(u):
        f = L.back()
        shrink = ease_in_out(span(u, 0.35, 2.2))
        trem = span(u, 0.0, 0.4) * (1 - span(u, 0.4, 0.6))
        sc = lerp(1.35, 0.62, shrink)
        draw_shadow(f, cx, cy + 10 * shrink, sc, u * 2 + 4.0, eyes=1.0 - 0.5 * trem, blink_period=9.0)
        if trem > 0:
            wobble_region(f, cx - 200, cy - 170, cx + 200, cy + 170, 6 * trem, u * 3)
        L.front(f)
        # 터짐 — 흰 섬광·금빛 고리·불티
        b = span(u, 0.38, 1.4)
        if 0 < b < 1:
            glow_spot(f, cx, cy, 240, "#fff3c2", 0.9 * (1 - b) ** 2)
            ring(f, cx, cy, 40 + 420 * ease_out(b), 0.9 * (1 - b), color=GOLD, width=5, squash=0.85)
            for sx, sy, sr in sparks:
                d = ease_out(b) * (180 + 160 * abs(sr))
                spark(f, cx + sx * d, cy + sy * d * 0.8, 5 + 3 * abs(sy), (1 - b) * 0.9)
        for light, t0, (tx, ty), flap in lights:
            k = ease_out(span(u, t0, t0 + 1.5))
            if k <= 0:
                continue
            x, y = lerp(cx, tx, k), lerp(cy, ty, k) - math.sin(math.pi * k) * 40
            y += math.sin(u * 2.6 + t0 * 9) * 6 * k
            fl = int((0.5 + 0.5 * math.sin(u * 11)) * 2.99) if flap else 0
            light.draw(f, x, y, alpha=smooth(span(u, t0, t0 + 0.25)), k=fl, glow=1.3)
        motes.draw(f, u, c("#fff3c2"), 0.3 * span(u, 0.4, 1.0))
        return zoom(f, 1.04 - 0.04 * ease_out(span(u, 0.38, 1.0)), cx, cy + 40)

    return frame


# ══════════════════════════ 샷2 — 돌아간다 ══════════════════════════

WIDE = dict(base=400.0, pw=36.0, ph=170.0, step=132.0, gap=640.0)


def wide_posts():
    g = WIDE
    left = [g["gap"] - g["step"] * k for k in range(5, 0, -1)]
    right = [g["gap"] + g["step"] * k for k in range(1, 6)]
    return left, right


POND = (420.0, 500.0)
FLOWERS = (880.0, 478.0)


def wide_extra(cv):
    """안쪽 들판의 연못(왼쪽 아래)과 꽃밭(오른쪽)."""
    m = Mask()
    m.ellipse(POND[0], POND[1], 190, 44)
    pa = m.arr(0.8)
    over(cv, c(INK), grow(pa, 1.6))
    tmp = cv.copy()
    water(tmp, POND[1] - 54, POND[1] + 54, seed=1031)
    over(cv, tmp, pa)
    rng = np.random.default_rng(1032)
    for k in range(5):
        bush(cv, FLOWERS[0] - 150 + k * 96, FLOWERS[1] + 6 + rng.uniform(-10, 14), 100, seed=1033 + k,
             fill="#4fb43e", berries=None)
    for _ in range(46):
        fx, fy = FLOWERS[0] + rng.uniform(-220, 240), FLOWERS[1] + rng.uniform(-30, 56)
        fc = ["#ff7aa2", "#ffd84d", "#ffffff", "#c58bff"][rng.integers(0, 4)]
        fm = Mask(40, 40)
        for j in range(5):
            a = 2 * math.pi * j / 5
            fm.circle(20 + math.cos(a) * 6, 20 + math.sin(a) * 6, 4.8)
        stamp(cv, grow(fm.arr(0.4), 1.0), fx, fy, INK)
        stamp(cv, fm.arr(0.4), fx, fy, fc)
        stamp(cv, Mask(12, 12).circle(6, 6, 2.4).arr(0.3), fx, fy, "#ffcc33")


def shot_scatter():
    g = WIDE
    left, right = wide_posts()
    L = TwoLayer(1021, 400, lambda cv: fence_with_gap(cv, left, g["gap"], right, g["base"], g["pw"], g["ph"], 1022,
                                                       flowers=30, extra=wide_extra))
    posts = left + right
    order = sorted(range(len(posts)), key=lambda i: abs(posts[i] - g["gap"]))   # 가까운 칸부터 켜진다
    d_pond, d_flow = dist_field(*POND), dist_field(*FLOWERS)
    bfly = LightBug("bfly", 46, flaps=(0.0, 0.25, 0.5))
    fire = LightBug("firefly", 40, color="#fff27a")
    mant = LightBug("mantis", 40)
    start = (640.0, 300.0)

    def plate_time(j):
        return 0.55 + 0.21 * j

    def frame(u):
        land_b = span(u, 1.9, 3.2)
        land_f = span(u, 1.6, 3.0)
        mix = np.maximum(bloom_from(d_flow, 260 * ease_out(land_b), 0.6), bloom_from(d_pond, 250 * ease_out(land_f), 0.6))
        f = L.back(mix * 0.9)
        draw_shadow(f, g["gap"], g["base"] - 36, 0.32, u + 2.0, eyes=1.0, blink_period=3.1)   # 쪼그라든 그림자(빈칸 너머)
        L.front(f, mix * 0.9)
        for j, i in enumerate(order):        # 이름표가 하나씩 켜진다 — 빈칸에서 금빛 불티가 날아가 닿는다
            t0 = plate_time(j)
            px = posts[i]
            x0, y0, x1, y1 = plate_rect(px, g["base"], g["pw"], g["ph"])
            pcy = (y0 + y1) / 2
            k = span(u, t0 - 0.35, t0)
            if 0 < k < 1:
                x = lerp(g["gap"], px, ease_in_out(k))
                y = lerp(260, pcy, k) - math.sin(math.pi * k) * 50
                glow_spot(f, x, y, 16, GOLD, 0.9)
                spark(f, x, y, 5, 0.8)
            lv = smooth(span(u, t0, t0 + 0.25))
            if lv > 0:
                paint_plate(f, px, g["base"], g["pw"], g["ph"], KINDS[i % len(KINDS)], level=lv)
                spark(f, px + 14, pcy - 18, 8, (1 - span(u, t0, t0 + 0.5)) * lv)
        # 나비 빛 → 오른쪽 꽃밭, 반딧불이 빛 → 아래 연못, 사마귀 빛은 빈칸 위에서 기다린다
        kb = ease_in_out(span(u, 0.0, 2.1))
        bx = lerp(start[0] + 150, FLOWERS[0] - 70, kb)
        by = lerp(190, FLOWERS[1] - 44, kb) - math.sin(math.pi * kb) * 120 + math.sin(u * 5) * 8
        bfly.draw(f, bx, by, alpha=1 - 0.7 * span(u, 2.2, 3.0), k=int((0.5 + 0.5 * math.sin(u * 11)) * 2.99))
        kf = ease_in_out(span(u, 0.0, 1.8))
        fx = lerp(start[0] - 150, POND[0] + 60, kf)
        fy = lerp(430, POND[1] - 22, kf) - math.sin(math.pi * kf) * 30
        fire.draw(f, fx, fy, alpha=1 - 0.6 * span(u, 1.9, 2.8), glow=1.3)
        if land_b > 0:
            for j in range(5):
                spark(f, FLOWERS[0] - 70 + (j - 2) * 50, FLOWERS[1] - 10 + (j % 2) * 26, 7,
                      (1 - land_b) * span(u, 1.9 + j * 0.08, 2.1 + j * 0.08))
        if land_f > 0:
            glow_spot(f, POND[0] + 60, POND[1], 120, "#fff27a", 0.35 * math.sin(math.pi * land_f))
        mant.draw(f, 640, 168 + math.sin(u * 2.2) * 8, alpha=1.0, glow=1.1)
        return zoom(f, 1.06 - 0.06 * ease_in_out(u / 3.6), 640, 360)

    return frame


# ══════════════════════════ 샷3 — 밀려난다 ══════════════════════════

def shot_pushed():
    L, g = close_layers(1041)
    plates = lit_plates(g["left"] + g["right"], g["base"], g["pw"], g["ph"])
    mant = LightBug("mantis", 46)
    puff = np.random.default_rng(1042).uniform(-1, 1, (16, 2))

    def frame(u):
        f = L.back()
        k = ease_in_out(span(u, 0.5, 2.3))
        sx = 640.0 + 30 * math.sin(k * 2.0)
        sy = lerp(330, 258, k)
        sc = lerp(0.85, 0.16, k)
        gone = span(u, 2.75, 3.2)
        eyes_blink = 1.0 if not (2.35 < u < 2.6) else 0.0
        if gone < 1:
            for j in range(5):            # 밀려나는 꼬리(보랏빛 자국)
                tk = max(0.0, k - 0.06 * (j + 1))
                glow_spot(f, 640.0 + 30 * math.sin(tk * 2.0), lerp(330, 258, tk) + 10, 60 * lerp(0.85, 0.16, tk),
                          PAL["shadow_glow"], 0.12 * (1 - j / 5) * span(u, 0.5, 0.8) * (1 - gone))
            draw_shadow(f, sx, sy, sc, u * 2.5 + 1.0, alpha=1 - gone, eyes=eyes_blink, blink_period=99.0)
        if gone > 0:
            for px, py in puff:
                d = ease_out(gone) * 40
                glow_spot(f, sx + px * d, sy + py * d * 0.6, 10, PAL["shadow_glow"], 0.5 * (1 - gone))
        L.front(f)
        put_layer(f, *plates)
        # 안쪽에서 밀어내는 금빛 물결 — 빈칸으로 퍼져 나간다
        for j in range(3):
            w = span(u, 0.3 + 0.45 * j, 1.6 + 0.45 * j)
            if 0 < w < 1:
                ring(f, 640, 560 - 260 * w, 80 + 300 * w, 0.55 * math.sin(math.pi * w), color=GOLD, width=4,
                     squash=0.35)
        mant.draw(f, 640, 120 + math.sin(u * 2.2) * 6, alpha=1.0, glow=1.1)
        return zoom(f, 1.0 + 0.05 * ease_in_out(u / 3.6), 640, 300)

    return frame


# ══════════════════════════ 샷4 — 메워진다 ══════════════════════════

MID = dict(base=455.0, pw=44.0, ph=240.0, step=165.0, gap=640.0)


def shot_filled():
    g = MID
    left = [g["gap"] - g["step"] * k for k in range(4, 0, -1)]
    right = [g["gap"] + g["step"]]
    L = TwoLayer(1051, 450, lambda cv: fence_with_gap(cv, left, g["gap"], right, g["base"], g["pw"], g["ph"], 1053,
                                                       hole=False))
    plates = lit_plates(left + right, g["base"], g["pw"], g["ph"])
    hero = person_parts("hero", 150)
    dist = dist_field(g["gap"], g["base"])
    pm = Mask()
    pm.poly(post_pts(g["gap"], g["base"], g["pw"], g["ph"]))
    rm = Mask()
    for ry in (0.3, 0.74):
        y = g["base"] - g["ph"] * ry
        rm.rect(left[-1], y - g["pw"] * 0.13, right[0], y + g["pw"] * 0.13)
    post_a, rail_a = pm.arr(0.6), rm.arr(0.6)
    post_ink = grow(post_a, 1.6)
    x0, y0, x1, y1 = plate_rect(g["gap"], g["base"], g["pw"], g["ph"])
    pcx, pcy = (x0 + x1) / 2, (y0 + y1) / 2
    mant = LightBug("mantis", 46)
    bf = InsectSprite("bfly", 30, rot=0.4)
    sparks = np.random.default_rng(1054).uniform(-1, 1, (18, 2))

    def shifted(a, dy):
        out = np.zeros_like(a)
        dy = int(round(dy))
        if dy <= 0:
            return a
        out[dy:] = a[:-dy]
        return out

    def frame(u):
        spread = ease_in_out(span(u, 1.5, 3.5))
        mix = bloom_from(dist, 40 + 1500 * spread, soft=0.45) if spread > 0 else None
        f = L.back(mix)
        # 새 말뚝 — 땅에서 쑥 솟는다(앞층의 풀밭이 아래를 가린다)
        rise = span(u, 0.85, 1.2)
        k = ease_out(rise)
        over_s = math.sin(math.pi * span(u, 1.2, 1.45)) * 10
        dy = (1 - k) * (g["ph"] + 10) - over_s
        if rise > 0:
            ra = rail_a * smooth(span(u, 1.1, 1.3))
            over(f, c(INK), np.maximum(shifted(post_ink, dy), grow(ra, 1.6)))
            over(f, c(PAL["rail"]), ra)
            over(f, c(PAL["fence"]), shifted(post_a, dy))
        L.front(f, mix)
        put_layer(f, *plates)
        if rise > 0:
            lv = smooth(span(u, 1.2, 1.6))
            paint_plate(f, g["gap"], g["base"] + dy, g["pw"], g["ph"], "mantis", level=lv)
            if lv > 0:
                b = span(u, 1.2, 2.0)
                glow_spot(f, pcx, pcy, 110, "#fff3c2", 0.7 * (1 - b))
                for sx, sy in sparks:
                    d = ease_out(b) * 120
                    spark(f, pcx + sx * d, pcy + sy * d, 6, (1 - b) * 0.9)
        # 사마귀 빛이 내려와 구멍에 든다
        kd = ease_in_out(span(u, 0.0, 0.9))
        if u < 1.05:
            mant.draw(f, g["gap"], lerp(110, g["base"] - 10, kd), alpha=1 - span(u, 0.85, 1.05), glow=1.2)
        if spread > 0.3:     # 색이 돌아온 들에 나비가 난다
            a = smooth(span(u, 2.2, 2.7))
            bf.draw(f, lerp(380, 520, span(u, 2.2, 3.6)), 300 + math.sin(u * 3) * 14, u, rate=14, alpha=a)
        draw_person(f, "hero", 536, 580, 150, t=u, parts=hero)
        draw_insect(f, "beetle", 590, 568, 20, rot=0.05 * math.sin(u * 2))
        return zoom(f, 1.08 - 0.08 * ease_in_out(u / 3.6), g["gap"], 400)

    return frame


# ══════════════════════════ 샷5 — 초록 사마귀 ══════════════════════════

SUN_X = 540.0


def shot_mantis():
    horizon = 430.0
    sky = book_sky(PAL["dawn"], seed=1061)
    rays = rays_texture(W, H, SUN_X, horizon - 10, count=16, seed=1062, spread=math.pi * 0.9, reach=0.9)
    # 먼 울타리(온전하다) — 이름표가 모두 켜져 있다
    far = Mask()
    xs = np.arange(-20, W + 40, 64)
    for x in xs:
        far.poly(post_pts(float(x), horizon + 6, 12, 60))
    for ry in (0.3, 0.74):
        far.rect(-20, horizon + 6 - 60 * ry - 2, W + 40, horizon + 6 - 60 * ry + 2)
    fa = far.arr(0.6)

    def front(cv):
        hills(cv, 1063, horizon - 18, 22, "#9fd27a", line=1.2, hi=0.06)
        over(cv, c(INK), grow(fa, 1.2) * 0.8)
        over(cv, c(PAL["fence"]), fa)
        for x in xs:
            dot(cv, float(x), horizon + 6 - 60 * 0.56, 4, c(PAL["glow"]), 0.8)
        meadow(cv, horizon + 4, 1064, flowers=70)
        stem = Mask()
        stem.taper(bezier((470, H + 20), (480, 560), (470, 470), (440, 400), 18), 18, 4)
        stem.taper(bezier((800, H + 20), (790, 560), (812, 470), (850, 420), 18), 16, 4)
        sa = stem.arr(0.6)
        over(cv, c(INK), grow(sa, 1.6))
        over(cv, c("#4fa83c"), sa)
        leaf = Mask()
        leaf.taper(bezier((520, H + 20), (560, 520), (610, 430), (700, 372), 24), 34, 6)
        la = leaf.arr(0.6)
        over(cv, c(INK), grow(la, 1.6))
        over(cv, c("#5cbf4a"), la)

    layer = bake_layer(front)
    sun_piece = Piece(lambda d: sun(d, 80, 80, 50, halo=0.0), 160, 160)
    pose = [insect_parts("mantis", 78, rot=r) for r in (-0.22, -0.16, -0.1)]
    motes = Particles(40, 1065, (0, 0, W, H), vel=((-6, 6), (-10, -3)), size=(0.8, 2.2))
    bf = InsectSprite("bfly", 26, rot=-0.5)

    def frame(u):
        f = sky.copy()
        sy = lerp(470, 352, ease_out(span(u, 0.0, 3.2)))
        add(f, c("#fff0c0"), rays * 0.3 * smooth(span(u, 0.3, 2.0)))
        glow_spot(f, SUN_X, sy, 300, "#fff1b0", 0.45)
        sun_piece.blit(f, SUN_X, sy)
        put_layer(f, *layer)
        k = int(min(2, max(0, (0.5 + 0.5 * math.sin(u * 1.6)) * 2.99)))
        draw_insect(f, "mantis", 668, 330, 78, parts=pose[k])
        glow_spot(f, 668, 330, 150, "#fff3c2", 0.12)
        bf.draw(f, lerp(980, 830, ease_in_out(u / 3.4)), 210 + math.sin(u * 3.2) * 18, u, rate=14,
                alpha=smooth(span(u, 0.6, 1.2)))
        motes.draw(f, u, c("#fff3c4"), 0.45)
        return zoom(f, 1.0 + 0.05 * ease_in_out(u / 3.4), 660, 340)

    return frame


SHOTS = [shot_burst, shot_scatter, shot_pushed, shot_filled, shot_mantis]


# ══════════════════════════ 소리 ══════════════════════════

def score(sc):
    """반짝임 폭발 → 이름의 동기 한 음씩 → 휙·멀어지는 쿵 → 나무 탁 → 새·풀벌레가 돌아오고 밝은 칼림바 동기 전체."""
    T = sc.total
    s = [sc.shot(k) for k in range(5)]
    sc.wind([(0, 0.6), (s[1], 0.4), (s[3] + 1.6, 0.3), (s[4], 0.15), (T, 0.1)], amp=0.04, lo=150, hi=900)
    sc.pad([73.42, 146.83, 174.61, 220.0], [(0, 0.5), (0.4, 0.2), (s[1], 0.1), (s[2], 0.25), (s[3], 0.1),
                                            (s[3] + 1.4, 0), (T, 0)], amp=0.05, bright=0.1)
    sc.pad([146.83, 220.0, 293.66, 369.99], [(0, 0), (0.4, 0.5), (s[2], 0.4), (s[3] + 1.4, 0.6), (s[4], 0.9),
                                             (T - 0.5, 1.0), (T, 0.7)], amp=0.05, bright=0.3)
    sc.birds([(0, 0), (s[3] + 1.6, 0), (s[3] + 2.6, 0.6), (s[4], 1.0), (T, 1.0)], amp=0.04, rate=1.4)
    sc.crickets([(0, 0), (s[3] + 1.8, 0), (s[4] + 0.2, 0.7), (T, 0.8)], amp=0.016)
    # 샷1 — 빠져나온다
    sc.noise(0.05, 0.4, 200, 1200, 0.06, decay=3)
    sc.boom(0.38, 120, 60, 0.6, 0.18)
    sc.sparkle(0.38, 1.0, density=22, amp=0.05)
    sc.whoosh(0.4, 1.0, 0.16, 600, 4000, pan_from=0.0, pan_to=0.0)
    for k, nt in enumerate(("D5", "A5", "F#6")):
        sc.chime(0.42 + k * 0.08, hz(nt), 0.08, dur=1.8, pan=-0.4 + 0.4 * k)
    # 샷2 — 이름표가 켜질 때마다 이름의 동기 한 음씩
    motif = ("D5", "F#5", "A5", "B5", "A5", "D6", "F#5", "A5", "B5", "D6")
    for j, nt in enumerate(motif):
        t0 = s[1] + 0.55 + 0.21 * j
        sc.pluck(t0, hz(nt), 0.11, 1.4, pan=-0.6 + 0.12 * j, kind="kalimba")
    sc.sparkle(s[1] + 1.9, 1.0, density=8, amp=0.03, lo=2400, hi=4600)       # 꽃밭
    sc.chime(s[1] + 1.7, hz("A6"), 0.05, pan=-0.6)                           # 연못
    sc.drip(s[1] + 1.75, 0.08, 900)
    # 샷3 — 밀려난다: 휙 + 낮은 쿵이 멀어진다
    sc.whoosh(s[2] + 0.4, 1.6, 0.13, 200, 1600, pan_from=0.0, pan_to=0.2)
    for k in range(3):
        sc.boom(s[2] + 0.7 + 0.55 * k, 85, 40, 1.0, 0.19 * (0.55 ** k))
    sc.blink(s[2] + 2.35, 0.07)
    sc.noise(s[2] + 2.8, 0.4, 600, 3000, 0.04, decay=8)
    # 샷4 — 말뚝이 선다(나무 탁) + 색이 퍼진다
    sc.whoosh(s[3] + 0.1, 0.9, 0.08, 1500, 5000, pan_from=0.0, pan_to=0.0)
    sc.thud(s[3] + 1.12, 0.42)
    sc.noise(s[3] + 1.12, 0.2, 300, 2000, 0.12, decay=20)
    sc.melody(s[3] + 1.25, ["D5", "F#5", "A5", "B5", "A5"], step=0.3, amp=0.12)
    sc.sparkle(s[3] + 1.25, 1.2, density=10, amp=0.035)
    sc.arp(s[3] + 1.8, ["D5", "F#5", "A5", "D6"], count=10, step=0.16, amp=0.05, kind="box")
    # 샷5 — 해피 엔딩: 밝은 칼림바 동기 전체 + 장조 화음
    sc.melody(s[4] + 0.2, [("D5", 1), ("F#5", 1), ("A5", 1), ("B5", 1), ("A5", 2), ("F#5", 1), ("A5", 1),
                           ("D6", 3)], step=0.27, amp=0.13)
    sc.melody(s[4] + 0.2, [("D4", 4), ("G4", 2), ("A4", 2), ("D4", 3)], step=0.27, amp=0.06, kind="kalimba", pan=-0.3)
    for k, nt in enumerate(("D4", "F#4", "A4", "D5", "F#5")):
        sc.pluck(s[4] + 2.65 + k * 0.035, hz(nt), 0.08, 2.4, pan=-0.4 + 0.2 * k, kind="kalimba")
    sc.chime(s[4] + 2.7, hz("D6"), 0.05, dur=2.0)
