"""ch3 「바위의 이름」 — 세라가 합류하는 대사(`ch3_reach_forest`) **뒤**에 튼다. 숲(이끼 초록).

    샷1  0.0~3.0   숲속 이끼 바위에 새겨진 칸 — 칸마다 곤충 그림 + 이름 무늬(이름 벽의 축소판). 나뭇잎 사이 빛
    샷2  2.4~6.0   몇 칸은 회색으로 바래 있다. 세라 손끝(크림 소매)이 바랜 칸의 무늬를 따라 훑는다
    샷3  5.4~9.0   손끝이 닿은 칸이 금빛으로 반짝이고 곤충 그림이 또렷해진다
    샷4  8.4~12.0  숲 너머 먼 산등성이 위 유적(금빛 윤곽). 앞에 세라와 주인공의 뒷모습

**무늬 씨앗 규칙(ch11과 공유)**: 칸 (행 r, 열 k)의 이름 무늬는 `glyphs(m, x, y, w=0.62·칸, rows=2, seed=3311 + r*5 + k,
size=0.085·칸, weight=0.018·칸)`이다(2행×5열, 칸 = 칸 한 변 px). 크기를 칸에 비례시키면 어느 배율에서든 같은 무늬다.
ch11 「나무껍질에 같은 무늬」는 같은 씨앗(3311~3320)과 같은 비율로 그리면 된다. 금빛으로 켜지는 칸은 (1,2) — 씨앗 3318.
"""
import math

import numpy as np

from bk_v1 import Sprite, hand_parts_rot
from kit import (INK, INSECT_COLORS, PAL, H, W, Mask, Particles, add, bake_layer, blur_arr, book_sky, c, crop, dot,
                 draw_hand, draw_insect, draw_person, ease_in_out, ease_out, fbm, glyphs, grow, insect_parts, lerp,
                 over, paint, put_layer, radial, rays_texture, smooth, span, sparkle, stamp, tree, zoom)
from silhouette_kit import bokeh, temple
from sound import hz

DURS = [3.0, 3.0, 3.0, 3.0]
TINT = "#f6fbea"
CUES = [
    (0.8, 2.2, "바위에 곤충 이름이 새겨져 있다."),
    (3.2, 2.2, "바랜 이름도 군데군데 보였다."),
    (5.8, 2.6, "손끝이 닿자, 이름이 반짝였다."),
    (8.9, 2.6, "답은 저 너머 유적에 있다."),
]

GLYPH_SEED = 3311          # ch11이 같은 무늬를 그린다 — 바꾸지 말 것
ROWS, COLS, CELL, GAP = 2, 5, 128, 16
ROCK_C = (640, 330)
KINDS = [["bfly", "beetle", "dfly", "moth", "mantis"],
         ["dfly", "bfly", "longhorn", "beetle", "firefly"]]
FADED = {(0, 3), (1, 0), (1, 2), (1, 3)}
TARGET = (1, 2)
GRAY = ("#c4c0b6", "#a29d93", "#d6d2c9")
SERA_SKIN, SERA_SLEEVE = "#f6c9a0", "#e9dcb8"


def cell_center(r, k):
    x0 = ROCK_C[0] - (COLS * (CELL + GAP) - GAP) / 2
    y0 = ROCK_C[1] - (ROWS * (CELL + GAP) - GAP) / 2
    return x0 + k * (CELL + GAP) + CELL / 2, y0 + r * (CELL + GAP) + CELL / 2


def _rock_outline():
    r = np.random.default_rng(3301)
    ph = r.uniform(0, 6.28, 3)
    pts = []
    for i in range(140):
        a = 2 * math.pi * i / 140
        k = 1 + 0.05 * math.sin(3 * a + ph[0]) + 0.03 * math.sin(7 * a + ph[1]) + 0.015 * math.sin(13 * a + ph[2])
        pts.append((ROCK_C[0] + math.cos(a) * 480 * k, ROCK_C[1] + 20 + math.sin(a) * 285 * k))
    return pts


def _moss_blobs():
    r = np.random.default_rng(3302)
    out = []
    for i in range(46):                      # 위 가장자리를 따라
        a = math.pi + math.pi * (i / 45) + r.uniform(-0.04, 0.04)
        rad = r.uniform(16, 38)
        out.append((ROCK_C[0] + math.cos(a) * 470, ROCK_C[1] + 20 + math.sin(a) * 270 + r.uniform(0, 26), rad))
    for i in range(26):                      # 아래 가장자리
        a = math.pi * (0.1 + 0.8 * i / 25) + r.uniform(-0.05, 0.05)
        out.append((ROCK_C[0] + math.cos(a) * 470, ROCK_C[1] + 20 + math.sin(a) * 280 - r.uniform(0, 20),
                    r.uniform(14, 30)))
    for (x, y) in ((288, 150), (470, 128), (990, 150), (760, 548), (320, 520), (1030, 470), (205, 330)):
        for _ in range(4):
            out.append((x + r.uniform(-30, 30), y + r.uniform(-12, 12), r.uniform(10, 22)))
    return out


class Rock:
    """이끼 바위와 칸 — 바위 좌표(샷1에서 화면과 같다)를 배율 S로 그린다. 바위 점 (px,py)가 화면 (sx,sy)에 온다."""

    def __init__(self, S, p, s):
        self.S, self.p, self.s = S, p, s

    def T(self, x, y):
        return (x - self.p[0]) * self.S + self.s[0], (y - self.p[1]) * self.S + self.s[1]

    def bg(self, seed):
        img = book_sky([(0, "#a8dc8a"), (0.5, "#6fb65c"), (1, "#3f7f3e")], seed=seed)
        tr = Mask()
        rr = np.random.default_rng(seed + 1)
        for _ in range(7):
            x = rr.uniform(0, W)
            tr.rect(x - rr.uniform(20, 46), -10, x + rr.uniform(20, 46), H + 10)
        over(img, c("#5a4a32"), blur_arr(tr.arr(0.5), 10) * 0.45)
        img += bokeh(W, H, seed + 2, 26, c("#fff6c8"), rmin=12, rmax=40, alpha=0.22)
        return img

    def draw(self, img, skip=(), line=None):
        S = self.S
        line = line or 1.6 * min(2.2, S ** 0.6)
        rk = Mask()
        rk.poly([self.T(x, y) for x, y in _rock_outline()])
        ra = rk.arr(0.6)
        paint(img, ra, "#b2b39b", line=line, hi=0.14, lo=0.16)
        n = fbm(W, H, 3303 + int(S * 10), (int(160 * S), int(50 * S), int(14 * S)), (0.5, 0.35, 0.15))
        img *= (1 + (n[..., None] - 0.5) * 0.22 * ra[..., None])
        ck = Mask()
        rr = np.random.default_rng(3304)
        for _ in range(7):
            x, y = rr.uniform(220, 1060), rr.uniform(90, 560)
            pts = [(x, y)]
            for _ in range(4):
                x, y = x + rr.uniform(-30, 30), y + rr.uniform(10, 34)
                pts.append((x, y))
            ck.line([self.T(*q) for q in pts], 1.8 * S ** 0.7, caps=False)
        over(img, c("#7d7a68"), ck.arr(0.5) * ra * 0.6)
        mo = Mask()
        for x, y, r_ in _moss_blobs():
            mo.circle(*self.T(x, y), r_ * S)
        ma = mo.arr(0.6) * np.clip(blur_arr(ra, 3 * S) * 1.4, 0, 1)
        paint(img, ma, "#86c64e", line=line * 0.8, hi=0.2, lo=0.0)
        dots = Mask()
        for x, y, r_ in _moss_blobs()[::3]:
            dots.circle(*self.T(x + r_ * 0.2, y + r_ * 0.25), r_ * 0.35 * S)
        over(img, c("#5e9e3a"), dots.arr(1.0) * ma * 0.6)
        for r in range(ROWS):
            for k in range(COLS):
                if (r, k) in skip:
                    continue
                faded = (r, k) in FADED
                self.cell(img, r, k, 0.0 if faded else 1.0)
        return ra

    # ── 칸 하나(국소 상자) ──
    def cell_parts(self, r, k):
        S = self.S
        cs = CELL * S
        box = int(cs + 40 + 50 * S)
        bx = by = box / 2
        pan = Mask(box, box)
        pan.rect(bx - cs / 2, by - cs / 2, bx + cs / 2, by + cs / 2, r=10 * S)
        pa = pan.arr(0.5)
        inner = Mask(box, box)
        inner.rect(bx - cs / 2 + 4 * S, by - cs / 2 + 4 * S, bx + cs / 2 - 4 * S, by + cs / 2 - 4 * S, r=8 * S)
        ia = inner.arr(0.5 * S)
        gm = Mask(box, box)
        glyphs(gm, bx - cs * 0.31, by + cs * 0.16, cs * 0.62, 2, GLYPH_SEED + r * COLS + k, size=cs * 0.085,
               weight=cs * 0.018)
        kind = KINDS[r][k]
        ip = insect_parts(kind, cs * 0.24, rot=0.1 * math.sin(r * 3 + k))
        ga = gm.arr(0.4)
        sh = np.clip(ia - np.roll(np.roll(ia, int(4 * S), 0), int(4 * S), 1), 0, 1)
        return dict(box=box, pa=pa, ia=ia, ga=ga, kind=kind, ip=ip, s=cs * 0.24, cs=cs,
                    glow=blur_arr(pa, 14 * S), ink=grow(pa, 1.4), sh=sh, gblur=blur_arr(ga, 3 * S))

    def cell(self, img, r, k, level, cp=None, glow=0.0):
        """level 1 = 살아 있는 이름(또렷), 0 = 바랜 이름(회색)."""
        cp = cp or self.cell_parts(r, k)
        x, y = self.T(*cell_center(r, k))
        if glow > 0:
            stamp(img, cp["glow"] * glow, x, y, PAL["glow"], "add")
        stamp(img, cp["ink"], x, y, INK)
        stamp(img, cp["pa"], x, y, lerp(c("#a9a693"), c("#8f8c7c"), level))
        panel = lerp(c("#d2cfc6"), c("#efe4c4"), level)
        if glow > 0:
            panel = lerp(panel, c("#ffefb0"), min(1.0, glow))
        stamp(img, cp["ia"], x, y, panel)
        stamp(img, cp["sh"] * 0.35, x, y, "#3d3a30")
        cols = [None if n is None else lerp(c(g), c(n), level) for g, n in zip(GRAY, INSECT_COLORS[cp["kind"]])]
        draw_insect(img, cp["kind"], x, y - cp["cs"] * 0.12, cp["s"], parts=cp["ip"], colors=cols,
                    alpha=0.55 + 0.45 * level)
        gcol = lerp(c("#b3aea2"), c("#5a4636"), level)
        if glow > 0:
            gcol = lerp(gcol, c("#c88a10"), min(1.0, glow))
            stamp(img, cp["gblur"] * glow * 0.9, x, y, PAL["glow"], "add")
        stamp(img, cp["ga"] * (0.5 + 0.5 * level), x, y, gcol)
        return cp


def _leaf(m, x, y, length, width, rot):
    pts = []
    for i in range(21):
        t = i / 20
        pts.append((t * length, width * math.sin(math.pi * t) ** 0.8))
    for i in range(20, -1, -1):
        t = i / 20
        pts.append((t * length, -width * math.sin(math.pi * t) ** 0.8))
    cr, sr = math.cos(rot), math.sin(rot)
    m.poly([(x + px * cr - py * sr, y + px * sr + py * cr) for px, py in pts])


def _front_leaves(seed):
    def draw(d):
        m = Mask()
        r = np.random.default_rng(seed)
        for x0, y0, a0 in ((-20, -10, 0.5), (1300, -20, 2.6)):
            for _ in range(9):
                _leaf(m, x0 + r.uniform(-40, 40), y0 + r.uniform(-20, 60), r.uniform(150, 230), r.uniform(36, 52),
                      a0 + r.uniform(-0.5, 0.5))
        paint(d, m.arr(0.6), "#3f8f3a", hi=0.12, lo=0.1)
    return draw


# ══════════════════════════ 샷1 — 이끼 바위의 칸 ══════════════════════════

def rock_shot():
    rock = Rock(1.0, ROCK_C, ROCK_C)
    img = rock.bg(3310)
    rock.draw(img)
    np.clip(img, 0, 1, out=img)
    leaves = bake_layer(_front_leaves(3312))
    dn = fbm(W + 400, H + 200, 3313, (120, 46), (0.6, 0.4))
    dapple = blur_arr(np.clip((dn - 0.56) * 5, 0, 1), 6)
    motes = Particles(30, 3314, (0, 0, W, H), vel=((-4, 4), (-8, -2)), size=(0.8, 1.8))

    def frame(u):
        f = img.copy()
        add(f, c("#fff4c8"), crop(dapple, 120 + u * 26, 60 + u * 9) * 0.15)
        motes.draw(f, u, c("#fff6cc"), 0.4)
        put_layer(f, *leaves)
        return zoom(f, 1.08 - 0.07 * ease_in_out(u / 3.0), W * 0.5, H * 0.46)

    return frame


# ══════════════════════════ 샷2·3 — 손끝이 바랜 이름을 훑는다 ══════════════════════════

def _finger(s, rot):
    """세라의 가리키는 손 — 기준점이 손가락 끝."""
    hp = hand_parts_rot(s, rot=rot, pose="point", sleeve_len=4.2)
    box = hp["box"]
    fx = box / 2 + (0.06 * math.cos(rot) + 1.7 * math.sin(rot)) * s
    fy = box / 2 + (0.06 * math.sin(rot) - 1.7 * math.cos(rot)) * s

    def draw(d):
        draw_hand(d, box / 2, box / 2, s, parts=hp, skin=SERA_SKIN, sleeve=SERA_SLEEVE)

    return Sprite(draw, box, box, origin=(fx, fy))


def trace_shot():
    S = 1.7
    rock = Rock(S, cell_center(*TARGET), (640, 330))
    img = rock.bg(3320)
    rock.draw(img)
    np.clip(img, 0, 1, out=img)
    finger = _finger(58, -0.5)
    tx, ty = rock.T(*cell_center(*TARGET))
    cs = CELL * S
    gy = ty + cs * 0.16 + cs * 0.085 * 1.25
    x0, x1 = tx - cs * 0.33, tx + cs * 0.3
    motes = Particles(24, 3321, (0, 0, W, H), vel=((-4, 4), (-8, -2)), size=(0.8, 1.8))
    dn = fbm(W + 400, H + 200, 3322, (140, 50), (0.6, 0.4))
    dapple = blur_arr(np.clip((dn - 0.56) * 5, 0, 1), 8)

    def tip(u):
        come = ease_out(span(u, 0.15, 1.0))
        tr = ease_in_out(span(u, 1.0, 3.2))
        x = lerp(x0, x1, tr)
        y = gy + math.sin(tr * math.pi * 3) * 4
        return lerp(x0 + 260, x, come), lerp(gy + 300, y, come)

    def frame(u):
        f = img.copy()
        add(f, c("#fff4c8"), crop(dapple, 80 + u * 20, 40 + u * 8) * 0.14)
        x, y = tip(u)
        if u > 1.0:
            dot(f, x - 4, y, 9, c("#fff2b0"), 0.25 * span(u, 1.0, 1.4))
        finger.blit(f, x, y)
        motes.draw(f, u, c("#fff6cc"), 0.35)
        return zoom(f, 1.0 + 0.05 * ease_in_out(u / 3.6), W * 0.5, H * 0.5)

    return frame, (x1, gy)


def glow_shot():
    S = 2.7
    rock = Rock(S, cell_center(*TARGET), (640, 300))
    img = rock.bg(3330)
    rock.draw(img, skip=(TARGET,))
    np.clip(img, 0, 1, out=img)
    cp = rock.cell_parts(*TARGET)
    tx, ty = rock.T(*cell_center(*TARGET))
    cs = CELL * S
    gy = ty + cs * 0.16 + cs * 0.085 * 1.25
    finger = _finger(84, -0.5)
    rest = (tx + cs * 0.3, gy)
    halo = radial(700, 700, 350, 350, 350, 1.6)
    stars = [(-0.42, -0.4, 0.95, 26), (0.44, -0.3, 1.1, 20), (-0.36, 0.1, 1.25, 16), (0.1, -0.52, 1.35, 22),
             (0.46, 0.22, 1.5, 14), (-0.1, 0.3, 1.6, 12)]
    motes = Particles(30, 3331, (300, 0, 980, 640), vel=((-6, 6), (-16, -4)), size=(0.9, 2.2))
    box = cp["box"]
    local = Rock(S, cell_center(*TARGET), (box / 2, box / 2))     # 칸만 국소 상자에 굽는다
    gray = Sprite(lambda d: local.cell(d, *TARGET, level=0.0, cp=cp), box, box)
    vivid = Sprite(lambda d: local.cell(d, *TARGET, level=1.0, cp=cp), box, box)
    lit = Sprite(lambda d: local.cell(d, *TARGET, level=1.0, cp=cp, glow=1.0), box, box)

    def frame(u):
        f = img.copy()
        lv = ease_in_out(span(u, 0.35, 1.35))
        gl = smooth(span(u, 0.3, 0.9)) * (1 - 0.45 * smooth(span(u, 1.6, 3.2)))
        stamp(f, halo * 0.35 * gl, tx, ty, "#ffe9a0", "add")
        if lv < 0.999:
            gray.blit(f, tx, ty, sub=False)
        vivid.blit(f, tx, ty, alpha=lv, sub=False)
        lit.blit(f, tx, ty, alpha=gl, sub=False)
        for dx, dy, t0, s in stars:
            k = span(u, t0, t0 + 0.7)
            if 0 < k < 1:
                sparkle(f, tx + dx * cs, ty + dy * cs, s * math.sin(math.pi * k), amount=1.0, rot=u)
        if u > 0.9:
            motes.draw(f, u, c("#ffe58a"), 0.6 * span(u, 0.9, 1.4))
        away = ease_in_out(span(u, 1.9, 3.3))
        finger.blit(f, rest[0] + away * 320, rest[1] + away * 380 + 2 * math.sin(u * 3) * (1 - away), sub=False)
        return zoom(f, 1.0 + 0.06 * ease_in_out(u / 3.6), W * 0.5, H * 0.45)

    return frame


# ══════════════════════════ 샷4 — 숲 너머 유적 ══════════════════════════

def ruin_shot():
    img = book_sky([(0, "#86ccf2"), (0.55, "#d6efe6"), (1, "#fff0c4")], seed=3340)
    rng = np.random.default_rng(3341)
    ph = rng.uniform(0, 6.28, 3)

    def ridge_y(x):
        return (392 - 150 * math.exp(-((x - 640) / 210) ** 2) - 60 * math.exp(-((x - 270) / 160) ** 2)
                - 70 * math.exp(-((x - 1030) / 190) ** 2) + 6 * math.sin(x * 0.03 + ph[0]) + 3 * math.sin(x * 0.07 + ph[1]))

    ridge_m = Mask()
    ridge_m.poly([(x, ridge_y(x)) for x in range(-10, W + 14, 4)] + [(W + 10, H + 4), (-10, H + 4)])
    ra = ridge_m.arr(1.0)
    paint(img, ra, "#a6acdf", ink="#7c84b8", line=1.4, hi=0.12, lo=0.0)
    snow = Mask()
    snow.poly([(x, ridge_y(x)) for x in range(520, 764, 4)] + [(x, ridge_y(x) + 26 + 8 * math.sin(x * 0.09))
                                                                for x in range(760, 516, -4)])
    over(img, c("#c9cdf0"), snow.arr(1.0) * ra * 0.7)
    rm = Mask(300, 230)
    temple(rm, 150, 190, 150, 130, broken=0.35, seed=3343)
    rarr = rm.arr(0.5)
    rx, ry = 640, ridge_y(640) + 10 - (190 - 115)
    stamp(img, grow(rarr, 1.2) * 0.8, rx, ry, "#6f74a8")
    stamp(img, rarr, rx, ry, "#8a8fcb")
    stamp(img, np.clip(rarr - np.roll(rarr, 2, 1), 0, 1) * 0.5, rx, ry, "#ffffff", "add")
    edge = np.clip(np.clip(blur_arr(rarr, 1.8) * 2.4, 0, 1) - rarr * 0.85, 0, 1)
    rglow = blur_arr(rarr, 14) * (1 - rarr)
    yy = np.arange(H, dtype=np.float32)[:, None]
    over(img, c("#eef6f0"), np.clip(1 - np.abs(yy - 410) / 60, 0, 1) ** 1.4 * np.ones((1, W), np.float32) * 0.5)
    clear = Mask()
    from silhouette_kit import ridge
    ridge(clear, 3344, 462, 10, freq=(0.003, 0.009))
    paint(img, clear.arr(1.0), "#9ad46a", hi=0.08)
    tl = Mask()
    rr = np.random.default_rng(3345)
    for xa, xb in ((-30, 500), (790, W + 30)):
        x, xs = xa, []
        while x < xb:
            rad = rr.uniform(32, 58)
            tl.circle(x, 452 - rad * 0.6 + rr.uniform(-8, 8), rad)
            tl.circle(x, 500, rad)
            xs.append(x)
            x += rad * rr.uniform(0.9, 1.3)
        tl.rect(xs[0], 452, xs[-1], 540)
    paint(img, tl.arr(0.8), "#5aa84a", hi=0.12)
    path = Mask()
    path.poly([(610, 470), (670, 470), (900, H + 10), (380, H + 10)])
    paint(img, path.arr(1.0), "#d8c08c", ink="#8a7552", line=1.0, hi=0.06, lo=0.0)
    for x, h, s in ((60, 640, 1), (230, 470, 2), (1220, 660, 3), (1060, 480, 4)):
        tree(img, x, H + 40, h, seed=3346 + s, leaf="#3f9a3a", leaf2="#2f7d2f", trunk="#7a5432")
    np.clip(img, 0, 1, out=img)

    def people(d):
        draw_person(d, "sera", 588, 556, 196, t=0.0)
        draw_person(d, "hero", 684, 560, 170, t=0.0)

    folk = bake_layer(people)
    rays = rays_texture(W, H, 140, -120, count=10, seed=3347, spread=0.9, center=0.9, reach=1.2)
    motes = Particles(30, 3348, (0, 80, W, 560), vel=((-4, 4), (-6, -1)), size=(0.8, 1.8))

    def frame(u):
        f = img.copy()
        add(f, c("#fff4cc"), rays * (0.16 + 0.04 * math.sin(u * 1.7)))
        pulse = 0.75 + 0.25 * math.sin(u * 2.6)
        k = ease_out(span(u, 0.1, 1.2))
        stamp(f, rglow * 0.35 * pulse * k, rx, ry, PAL["glow"], "add")
        stamp(f, edge * (0.8 + 0.2 * pulse) * k, rx, ry, "#f0ad14")
        stamp(f, edge * 0.25 * pulse * k, rx, ry, "#fff2b0", "add")
        put_layer(f, *folk)
        motes.draw(f, u, c("#fff3c4"), 0.4)
        if 1.3 < u < 2.4:
            sparkle(f, rx + 52, ry - 52, 12 * math.sin(math.pi * span(u, 1.3, 2.4)), amount=0.9)
        return zoom(f, 1.0 + 0.07 * ease_in_out(u / 3.6), W * 0.5, H * 0.42)

    return frame


SHOTS = [rock_shot, lambda: trace_shot()[0], glow_shot, ruin_shot]


def score(sc):
    """숲 새소리·바람 → 손끝이 긁는 소리 → 반짝일 때 이름의 동기 → 샷4 넓은 패드."""
    T = sc.total
    s = [sc.shot(k) for k in range(4)]
    sc.birds([(0, 0.8), (s[1], 0.5), (s[2], 0.3), (s[3], 0.7), (T, 0.8)], amp=0.03, rate=1.0)
    sc.wind([(0, 0), (0.6, 0.8), (s[2], 0.5), (s[3], 1.0), (T, 0.7)], amp=0.024, lo=400, hi=2400, gust=0.5)
    sc.pad([hz("G2"), hz("D3"), hz("B3")], [(0, 0), (1.0, 0.55), (s[2], 0.45), (s[2] + 0.8, 0.0), (T, 0)], amp=0.038)
    sc.scratch(s[1] + 1.05, dur=2.1, amp=0.05)                     # 샷2 — 손끝이 무늬를 훑는다
    glow = s[2] + 0.45                                               # 샷3 — 이름이 반짝인다
    sc.chime(glow, hz("D6"), 0.07, dur=2.0)
    sc.melody(glow + 0.25, ["D5", "F#5", "A5", "B5", ("A5", 2)], step=0.3, amp=0.13, kind="kalimba")
    sc.sparkle(glow + 0.4, dur=1.4, density=10, amp=0.03)
    sc.pad([hz("D3"), hz("A3"), hz("E4"), hz("F#4")], [(0, 0), (s[2] + 0.4, 0), (s[2] + 1.4, 0.45), (s[3], 0.5),
                                                       (s[3] + 1.6, 1.0), (T - 0.6, 1.0), (T, 0)], amp=0.036,
           bright=0.3, pan_spread=0.8)
    sc.chime(s[3] + 1.4, hz("A5"), 0.05, dur=2.0, pan=0.2)
    sc.chime(s[3] + 1.42, hz("D6"), 0.04, dur=2.2, pan=-0.2)
