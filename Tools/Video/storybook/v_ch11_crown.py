"""ch11 「우듬지」 — 대치(`ch11_confront`) **대사 뒤**에 튼다. 우듬지(신록).

세라가 말한 '비상 울타리' 두 곳 — 거대한 나무의 껍질(여기)과 꽃밭 한가운데 유리 온실. 큰 울타리가 무너질 때를
대비해 남겨 둔 자리다. 나무껍질의 이름 무늬는 **ch3 숲 바위와 같은 무늬**다(glyphs 씨앗 3311부터, 칸 배치도 같다).

    샷1  0.0~3.0   거대한 나무 꼭대기 — 신록 사이로 햇살이 반짝, 카메라가 우듬지 위로 올라선다
    샷2  2.4~6.0   나무껍질의 이름 칸(곤충 그림 + 무늬) — 칸마다 금빛으로 켜진다
    샷3  5.4~9.0   시선이 아래로 — 숲 너머 꽃밭 한가운데 유리 온실이 반짝인다
    샷4  8.4~12.0  나무와 온실이 한 화면 — 둘 다 금빛 테두리로 빛난다(종이 두 번)
"""
import math

import numpy as np

from bk_v3 import Sprite, vgrad
from kit import (INK, PAL, H, W, InsectSprite, Mask, Particles, add, bake_layer, blur_arr, book_sky, c, cloud, dot,
                 draw_insect, ease_in_out, ease_out, fbm, glyphs, grow, insect_parts, lerp, light_beam, over, paint,
                 put_layer, radial, rim, smooth, span, sparkle, stamp, sun, zoom)
from sound import hz

DURS = [3.0, 3.0, 3.0, 3.0]
TINT = "#f6ffe8"
CUES = [
    (1.0, 2.4, "나무 꼭대기에도 이름이 있었다."),
    (3.8, 2.2, "숲 바위와 같은 무늬였다."),
    (6.0, 2.6, "저 멀리 꽃밭 온실에도."),
    (9.0, 2.6, "둘 다 비상 울타리였다."),
]

LEAF = "#7fd35a"
LEAF2 = "#5cbf4a"
LEAF3 = "#3f9a35"
LEAF_HI = "#c4ef7a"
BARK = "#a0703f"
BARK2 = "#7a5030"
GOLD = PAL["glow"]

# 이름 칸 — ch3 숲 바위(v_ch3_scholar.py)와 같은 무늬. 그쪽 규칙: 2행×5열, 칸 (r, k)의 씨앗 3311 + r*5 + k,
# glyphs(x−0.31·칸, y+0.16·칸, w=0.62·칸, rows=2, size=0.085·칸, weight=0.018·칸), 곤충 0.24·칸을 y−0.12·칸에.
# 세로 화면(x 437~843)에 칸 다섯은 안 들어가므로 가운데 세 열(k = 1..3)을 그대로 옮긴다 — 금빛 칸 (1,2)·씨앗 3318도 포함.
GLYPH_SEED = 3311
CH3_COLS = 5
CH3_KINDS = [["bfly", "beetle", "dfly", "moth", "mantis"],
             ["dfly", "bfly", "longhorn", "beetle", "firefly"]]
ROWS, COLS, COL0 = 2, 3, 1


def leaf_cluster(m, cx, cy, r, seed, n=9):
    """둥근 잎 덩어리 — 동그라미 여럿(위가 볼록)."""
    g = np.random.default_rng(seed)
    for k in range(n):
        a = math.pi * (1.05 + 0.9 * k / max(1, n - 1)) + g.uniform(-0.15, 0.15)
        rr = r * g.uniform(0.38, 0.55)
        m.circle(cx + math.cos(a) * r * 0.62, cy + math.sin(a) * r * 0.5, rr)
    m.circle(cx, cy, r * 0.62)
    m.ellipse(cx, cy + r * 0.22, r * 0.95, r * 0.45)


def leaf_shape(m, x, y, ln, ang, wid=0.38):
    """잎 한 장(끝이 뾰족한 타원)."""
    pts = []
    for i in range(17):
        t = i / 16
        w = math.sin(math.pi * t) ** 0.8 * ln * wid * 0.5
        pts.append((t * ln, w))
    pts += [(t * ln, -w) for t, w in [(p[0] / ln, p[1]) for p in pts[::-1]]]
    ca, sa = math.cos(ang), math.sin(ang)
    m.poly([(x + px * ca - py * sa, y + px * sa + py * ca) for px, py in pts])


def leaf_spray(seed, w, h, root, ang0, n=9, ln=(90, 150), fill=LEAF2):
    """가지 끝 잎 다발(앞잎) — 구운 층으로 돌려준다."""

    def draw(cv):   # bake_layer가 두 번 부른다 — 난수는 안에서 새로 뽑아야 두 번이 같은 그림이다
        g = np.random.default_rng(seed)
        stem = Mask(w, h)
        leaves = Mask(w, h)
        veins = Mask(w, h)
        x, y = root
        tip = (x + math.cos(ang0) * 300, y + math.sin(ang0) * 300)
        stem.taper([root, ((x + tip[0]) / 2, (y + tip[1]) / 2 + 10), tip], 12, 5)
        for k in range(n):
            t = 0.25 + 0.75 * k / max(1, n - 1)
            px, py = lerp(x, tip[0], t), lerp(y, tip[1], t)
            a = ang0 + (0.9 if k % 2 else -0.9) * g.uniform(0.6, 1.1)
            L = g.uniform(*ln)
            leaf_shape(leaves, px, py, L, a)
            veins.line([(px, py), (px + math.cos(a) * L * 0.85, py + math.sin(a) * L * 0.85)], 1.8)
        paint(cv, stem.arr(0.5), BARK, line=1.4, hi=0.1)
        la = leaves.arr(0.5)
        paint(cv, la, fill, line=1.6, hi=0.1, lo=0.0)
        over(cv, c(LEAF3), veins.arr(0.4) * la * 0.6)

    return bake_layer(draw, w, h)


# ══════════════════════════ 샷 1 — 우듬지 ══════════════════════════

def canopy_shot():
    HC = H + 170
    img = book_sky(PAL["day"], seed=1101, h=HC)
    sun(img, 1010, 120, 54)
    cloud(img, 260, 120, 0.9, seed=1102)
    cloud(img, 760, 70, 0.6, seed=1103)
    # 멀리 아래 — 숲의 꼭대기들(옅게: 높이 올라왔다)
    far = Mask(W, HC)
    g = np.random.default_rng(1104)
    for k in range(26):
        x = k * 52 + g.uniform(-12, 12)
        far.circle(x, HC - 120 + g.uniform(-20, 30), g.uniform(40, 70))
    far.rect(0, HC - 110, W, HC)
    paint(img, far.arr(0.6), "#a9d98a", ink="#7fae6a", line=1.2, hi=0.12, lo=0.0)
    # 거대한 나무 우듬지 — 뒤 덩어리 → 앞 덩어리
    back, mid, front, branch = Mask(W, HC), Mask(W, HC), Mask(W, HC), Mask(W, HC)
    for k, (x, y, r) in enumerate(((380, 540, 180), (900, 540, 185), (520, 410, 170), (770, 410, 175))):
        leaf_cluster(back, x, y, r, 1110 + k)
    for k, (x, y, r) in enumerate(((640, 300, 190), (470, 580, 190), (820, 580, 190), (640, 640, 220))):
        leaf_cluster(mid, x, y, r, 1120 + k)
    for k, (x, y, r) in enumerate(((560, 790, 200), (740, 800, 190))):
        leaf_cluster(front, x, y, r, 1130 + k)
    for pts, w0, w1 in ((((640, HC + 20), (620, 620), (600, 470)), 46, 22), (((610, 640), (470, 560), (380, 500)), 22, 10),
                        (((630, 600), (800, 520), (900, 470)), 22, 10)):
        branch.taper(list(pts), w0, w1)
    paint(img, back.arr(0.6), LEAF3, line=1.8, hi=0.14, lo=0.1, light=(1, -1))
    paint(img, branch.arr(0.6), BARK, line=1.6, hi=0.14, lo=0.12)
    ma = mid.arr(0.6)
    paint(img, ma, LEAF2, line=1.8, hi=0.2, lo=0.1, light=(1, -1))
    fa = front.arr(0.6)
    paint(img, fa, LEAF, line=1.8, hi=0.22, lo=0.1, light=(1, -1))
    # 잎 결 — 작은 잎 무늬(밝은·어두운)
    lm, dm = Mask(W, HC), Mask(W, HC)
    for k in range(260):
        x, y = g.uniform(0, W), g.uniform(140, HC)
        (lm if k % 2 else dm).ellipse(x, y, g.uniform(7, 13), g.uniform(3, 5), g.uniform(-1, 1))
    cover = np.maximum(ma, fa)
    over(img, c(LEAF_HI), lm.arr(0.5) * cover * 0.55)
    over(img, c(LEAF3), dm.arr(0.5) * cover * 0.35)
    add(img, c("#fff6c0"), rim(np.maximum(cover, back.arr(0.6)), -5, 5, 6) * 0.3)
    # 반짝일 자리 — 우듬지 윗가장자리
    spots = []
    for k in range(14):
        x = g.uniform(380, 900)
        col_a = cover[:, int(x)]
        ys = np.nonzero(col_a > 0.5)[0]
        if len(ys):
            spots.append((x, ys[0] + g.uniform(10, 60), g.uniform(0, 6.28)))
    sprays = [leaf_spray(1140, W, H, (-30, 640), -0.55, n=8, ln=(110, 170), fill=LEAF2),
              leaf_spray(1141, W, H, (W + 30, 40), 2.6, n=8, ln=(110, 170), fill=LEAF)]
    bfly = InsectSprite("bfly", 34, rot=0.4)
    motes = Particles(26, 1142, (0, 0, W, H), vel=((-6, 6), (-10, -2)), size=(0.8, 1.8))

    def frame(u):
        k = ease_in_out(span(u, 0.0, 3.0))
        oy = lerp(170, 20, k)
        f = img[int(oy):int(oy) + H].copy() if oy == int(oy) else img[int(oy):int(oy) + H].copy()
        light_beam(f, 1040, -40, 560, 520, 30, 150, amount=0.16 + 0.04 * math.sin(u * 1.7))
        for x, y, ph in spots:
            tw = max(0.0, math.sin(u * 2.6 + ph)) ** 6
            if tw > 0.05:
                sparkle(f, x, y - oy, 7 + 5 * tw, "#fffbd0", 0.9 * tw, rot=ph)
        bfly.draw(f, lerp(420, 860, u / 3.0), 210 - oy * 0.25 + math.sin(u * 3.0) * 16, u, rate=13)
        motes.draw(f, u, c("#fff8d0"), 0.45)
        # 앞잎 — 더 빨리 내려간다(가까이 있다)
        for (pm, pa), dy in zip(sprays, (k * 120, k * 160)):
            put_layer(f, *_shifted((pm, pa), dy))
        return zoom(f, 1.04 - 0.04 * k, 640, 330)

    return frame


def _shifted(layer, dy):
    """구운 층을 아래로 dy픽셀 민다(위는 비운다)."""
    pm, pa = layer
    d = int(round(dy))
    if d == 0:
        return pm, pa
    out_m = np.zeros_like(pm)
    out_a = np.zeros_like(pa)
    if d > 0:
        out_m[d:] = pm[:-d]
        out_a[d:] = pa[:-d]
    else:
        out_m[:d] = pm[-d:]
        out_a[:d] = pa[-d:]
    return out_m, out_a


# ══════════════════════════ 샷 2 — 나무껍질의 이름 칸 ══════════════════════════

CELL, GAP = 118.0, 16.0   # kit.NameWall과 같은 칸(ch3 바위도 같은 비율이면 무늬가 같은 모양이 된다)
PANEL_C = (640.0, 296.0)


def cell_center(r, k):
    x0 = PANEL_C[0] - (COLS * (CELL + GAP) - GAP) / 2
    y0 = PANEL_C[1] - (ROWS * (CELL + GAP) - GAP) / 2
    return x0 + k * (CELL + GAP) + CELL / 2, y0 + r * (CELL + GAP) + CELL / 2


def bark_bg(seed=1150):
    """나무껍질 — 세로로 갈라진 골 + 이끼·새순(신록)."""
    img = vgrad(W, H, [(0, "#b07a46"), (1, "#8e6236")])
    n = fbm(W, H, seed, (120, 40, 12), (0.5, 0.3, 0.2))
    img *= (0.86 + 0.28 * n)[..., None]
    g = np.random.default_rng(seed + 1)
    dark, light = Mask(), Mask()
    for k in range(34):
        x = g.uniform(-20, W + 20)
        pts = []
        y = -20
        while y < H + 30:
            pts.append((x + math.sin(y * 0.012 + k) * 10 + g.uniform(-3, 3), y))
            y += 40
        dark.taper(pts, g.uniform(5, 13), g.uniform(4, 10))
        light.taper([(px + 9, py) for px, py in pts], 2.2, 1.6)
    over(img, c(BARK2), dark.arr(1.0) * 0.8)
    over(img, c("#c89460"), light.arr(0.8) * 0.5)
    # 이끼·새순 — 모서리에
    moss = Mask()
    for x, y, r in ((60, 640, 110), (190, 700, 90), (1210, 90, 100), (1110, 30, 80), (1240, 600, 70)):
        leaf_cluster(moss, x, y, r, int(x + y))
    paint(img, moss.arr(0.6), "#8fd45e", line=1.4, hi=0.2, lo=0.08)
    sprout = Mask()
    for x, y, a in ((150, 560, -1.3), (190, 590, -0.6), (1150, 170, 2.4), (1100, 140, 1.8)):
        leaf_shape(sprout, x, y, 90, a, 0.42)
    paint(img, sprout.arr(0.5), LEAF, line=1.5, hi=0.2, lo=0.1)
    return img


def name_panel(img):
    """나무껍질에 새긴 이름 칸 판 — 둥근 홈 + 칸 + 새긴 곤충 그림 + 무늬. (무늬 알파, 칸 정보)."""
    pw = COLS * (CELL + GAP) - GAP + 48
    ph = ROWS * (CELL + GAP) - GAP + 48
    pm = Mask()
    pm.rect(PANEL_C[0] - pw / 2, PANEL_C[1] - ph / 2, PANEL_C[0] + pw / 2, PANEL_C[1] + ph / 2, r=26)
    pa = pm.arr(0.6)
    over(img, c(INK), grow(pa, 1.8) * 0.85)
    over(img, c("#e2bd88"), pa)
    over(img, c("#5a3a1e"), rim(pa, 5, 5, 6) * 0.5)            # 홈의 위·왼 그늘(새겼다)
    add(img, c("#ffffff"), rim(pa, -4, -4, 4) * 0.16)
    cm = Mask()
    for r in range(ROWS):
        for k in range(COLS):
            x, y = cell_center(r, k)
            cm.rect(x - CELL / 2, y - CELL / 2, x + CELL / 2, y + CELL / 2, r=10)
    ca = cm.arr(0.6)
    over(img, c("#8a5a30"), grow(ca, 1.2) * 0.6)
    over(img, c("#d6ab74"), ca)
    over(img, c("#6a4422"), rim(ca, 3, 3, 3) * 0.45)
    gm = Mask()
    cells = []
    for r in range(ROWS):
        for k in range(COLS):
            x, y = cell_center(r, k)
            kk = k + COL0   # ch3 칸의 열 번호
            glyphs(gm, x - CELL * 0.31, y + CELL * 0.16, CELL * 0.62, 2, GLYPH_SEED + r * CH3_COLS + kk,
                   size=CELL * 0.085, weight=CELL * 0.018)
            kind = CH3_KINDS[r][kk]
            p = insect_parts(kind, CELL * 0.24, rot=0.1 * math.sin(r * 3 + kk))
            ia = np.maximum(p["wing"], p["body"])
            # 새긴 그림(나뭇빛) — 빛이 들면 그 위에 색 그림이 겹친다
            draw_insect(img, kind, x, y - CELL * 0.12, CELL * 0.24, parts=p, colors=("#c49460", "#7a5030", "#a87444"))
            cells.append((r, k, x, y, kind, p, blur_arr(ia, 12)))
    ga = gm.arr(0.4)
    over(img, c("#6a4422"), ga * 0.75)
    return ga, cells, ca


def bark_shot():
    img = bark_bg()
    glyph_a, cells, cell_a = name_panel(img)
    glyph_glow = blur_arr(glyph_a, 3)
    panel_glow = np.clip(blur_arr(grow(cell_a, 6), 26) - cell_a * 0.7, 0, 1)
    cell_glow = {}
    for r, k, x, y, kind, p, ia in cells:
        m = Mask(int(CELL) + 60, int(CELL) + 60)
        m.rect(30, 30, CELL + 30, CELL + 30, r=10)
        cell_glow[(r, k)] = blur_arr(m.arr(0.5), 14)
    order = [(r, k) for k in range(COLS) for r in range(ROWS)]          # 왼쪽 열부터 — 손끝이 훑듯
    lit_at = {rc: 0.75 + 0.3 * i for i, rc in enumerate(order)}
    # 칸 하나의 영역(무늬 빛을 칸 단위로 켜려고)
    region = {}
    for r, k, x, y, *_ in cells:
        rm = np.zeros((H, W), np.float32)
        rm[int(y - CELL / 2):int(y + CELL / 2), int(x - CELL / 2):int(x + CELL / 2)] = 1
        region[(r, k)] = rm
    shade = Particles(7, 1155, (0, 0, W, H), vel=((-14, 14), (-6, 6)), size=(26, 40), wobble=30, twinkle=0.2)

    def frame(u):
        f = img.copy()
        lit = np.zeros((H, W), np.float32)
        for r, k, x, y, kind, p, ia in cells:
            lv = smooth(span(u, lit_at[(r, k)], lit_at[(r, k)] + 0.35))
            if lv <= 0:
                continue
            pulse = 1 + 0.12 * math.sin(u * 4 + r + k)
            stamp(f, cell_glow[(r, k)] * 0.55 * lv * pulse, x, y, GOLD, "add")
            stamp(f, ia * 0.8 * lv, x, y - CELL * 0.12, GOLD, "add")
            draw_insect(f, kind, x, y - CELL * 0.12, CELL * 0.24, parts=p, alpha=lv)
            lit += region[(r, k)] * lv
            fl = span(u, lit_at[(r, k)], lit_at[(r, k)] + 0.6)
            if 0 < fl < 1:
                sparkle(f, x + CELL * 0.32, y - CELL * 0.3, 10 + 8 * fl, "#fff6c0", 1 - fl)
        add(f, c(GOLD), glyph_glow * lit * 0.9)
        over(f, c("#c27a10"), glyph_a * lit)   # 금빛 글줄 — 밝은 칸 위에서도 읽히게 짙은 금색
        shade.draw(f, u, c("#fff3c0"), 0.06)        # 잎 사이로 드는 햇빛 얼룩
        add(f, c(GOLD), panel_glow * 0.35 * smooth(span(u, 0.8, 2.6)) * (1 + 0.15 * math.sin(u * 3.3)))
        return zoom(f, 1.0 + 0.045 * ease_in_out(u / 3.6), PANEL_C[0], PANEL_C[1])   # 세로 화면에서 칸이 잘리지 않게

    return frame


# ══════════════════════════ 샷 3 — 시선이 아래로, 꽃밭의 유리 온실 ══════════════════════════

GH_C = (640.0, 742.0)   # 큰 화면(세로 1120) 안의 온실 바닥 가운데


def greenhouse(cv, x, base, w, seed=0, glints=None):
    """유리 온실 — 둥근 지붕, 흰 창살, 안에 초록 화분. 투명한 유리는 옅은 하늘빛을 덮는다."""
    h = w * 0.62
    body = Mask(cv.shape[1], cv.shape[0])
    body.rect(x - w / 2, base - h * 0.62, x + w / 2, base)
    body.ellipse(x, base - h * 0.62, w / 2, h * 0.42)
    ba = body.arr(0.6)
    plants = Mask(cv.shape[1], cv.shape[0])
    stems = Mask(cv.shape[1], cv.shape[0])
    g = np.random.default_rng(seed)
    flowers = []
    for k, (fx, fh, fr) in enumerate(((-0.36, 0.22, 0.08), (-0.2, 0.3, 0.09), (0.0, 0.62, 0.15), (0.2, 0.32, 0.09),
                                      (0.37, 0.2, 0.08))):
        px, py = x + fx * w, base - h * fh
        stems.line([(px, base - 2), (px, py)], max(1.5, w * 0.014))
        plants.circle(px, py, w * fr)
        for j in range(3):
            flowers.append((px + g.uniform(-0.7, 0.7) * w * fr, py + g.uniform(-0.6, 0.5) * w * fr))
    pots = Mask(cv.shape[1], cv.shape[0])
    for fx in (-0.36, -0.2, 0.2, 0.37):
        pots.rect(x + fx * w - w * 0.045, base - h * 0.1, x + fx * w + w * 0.045, base - 1, r=2)
    over(cv, c(INK), grow(ba, 1.6))
    over(cv, c("#bfe9ee"), ba * 0.62)
    over(cv, c(BARK2), stems.arr(0.5) * ba)
    paint(cv, plants.arr(0.5) * ba, LEAF2, line=1.0, hi=0.14, lo=0.0)
    paint(cv, pots.arr(0.5) * ba, "#e07a4a", line=1.0, hi=0.1, lo=0.0)
    for fx, fy in flowers:
        dot(cv, fx, fy, max(2.0, w * 0.022), c("#ff7aa2" if (fx + fy) % 2 < 1 else "#ffd84d"), 0.95)
    frame_m = Mask(cv.shape[1], cv.shape[0])
    lw = max(1.4, w * 0.012)
    for k in range(1, 6):
        xx = x - w / 2 + w * k / 6
        frame_m.line([(xx, base), (xx, base - h * 0.62)], lw)
    frame_m.line([(x - w / 2, base - h * 0.3), (x + w / 2, base - h * 0.3)], lw)
    for k in range(-2, 3):   # 지붕 살 — 가운데로 모인다
        frame_m.line([(x + k * w * 0.2, base - h * 0.62), (x + k * w * 0.08, base - h * 1.0)], lw)
    frame_m.d.ellipse([(x - w / 2) * 2, (base - h * 1.04) * 2, (x + w / 2) * 2, (base - h * 0.2) * 2],
                      outline=255, width=max(2, int(lw * 2)))
    over(cv, c("#ffffff"), frame_m.arr(0.5) * ba * 0.95)
    add(cv, c("#ffffff"), rim(ba, -5, -5, 6) * 0.35)
    shine = Mask(cv.shape[1], cv.shape[0])
    shine.line([(x - w * 0.28, base - h * 0.95), (x - w * 0.12, base - h * 0.4)], w * 0.04)
    add(cv, c("#ffffff"), shine.arr(1.5) * ba * 0.35)
    if glints is not None:
        glints += _glint_pts(x, base, w)
    return ba


def _glint_pts(x, base, w):
    h = w * 0.62
    return [(x - w * 0.22, base - h * 0.86), (x + w * 0.18, base - h * 0.7), (x + w * 0.36, base - h * 0.2),
            (x - w * 0.4, base - h * 0.3)]


def flower_dots(cv, y0, y1, seed, n, size=(2.0, 5.0), x0=0, x1=W):
    """꽃 점 — 아래(가까이)일수록 크다."""
    g = np.random.default_rng(seed)
    cols = [c(v) for v in ("#ff7aa2", "#ffd84d", "#ffffff", "#ff9b3d", "#d58cff")]
    for _ in range(n):
        y = g.uniform(y0, y1)
        k = (y - y0) / max(1, y1 - y0)
        dot(cv, g.uniform(x0, x1), y, lerp(size[0], size[1], k), cols[g.integers(0, len(cols))], 0.95)


def down_shot():
    HC = 1120
    img = book_sky(PAL["day"], seed=1160, h=HC)
    add(img, c("#fff6d0"), radial(W, HC, 1000, 60, 600, 2.0) * 0.4)
    cloud(img, 300, 140, 0.8, seed=1161)
    cloud(img, 1000, 230, 0.6, seed=1162)
    from kit import ridge
    hz_y = 520
    far = ridge(Mask(W, HC), 1163, hz_y - 30, 30, freq=(0.003, 0.009)).arr(1.0)
    paint(img, far, "#a7c9a8", ink="#8aa88a", line=1.2, hi=0.0, lo=0.0)
    mid = ridge(Mask(W, HC), 1164, hz_y + 20, 22, freq=(0.004, 0.01)).arr(0.8)
    paint(img, mid, "#8fcf6a", ink="#6aa04a", line=1.4, hi=0.1, lo=0.0)
    # 숲 띠(꽃밭 뒤)
    g = np.random.default_rng(1166)
    tl = Mask(W, HC)
    for k in range(34):
        tl.circle(k * 40 + g.uniform(-8, 8), 590 + g.uniform(-10, 8), g.uniform(26, 40))
    tl.rect(0, 590, W, 640)
    paint(img, tl.arr(0.6), "#5fb845", ink="#3f7a35", line=1.4, hi=0.14, lo=0.0)
    # 꽃밭 — 온실을 둘러싼 둥근 꽃 이랑(분홍·노랑·보라·흰), 사이사이 풀길
    field = Mask(W, HC)
    field.ellipse(640, 770, 700, 190)
    fa = field.arr(1.0)
    over(img, c("#9fdc72"), fa)
    beds = ("#ff8fb4", "#ffd84d", "#c99cff", "#fff3f6", "#ff9b5a", "#ff8fb4")
    for k, colr in enumerate(beds):
        rx = 620 - k * 82
        ring = Mask(W, HC)
        ring.ellipse(640, 770, rx, rx * 0.27)
        inner = Mask(W, HC)
        inner.ellipse(640, 770, rx - 50, (rx - 50) * 0.27)
        ra = np.clip(ring.arr(0.8) - inner.arr(0.8), 0, 1) * fa
        over(img, c(INK), np.clip(grow(ra, 1.0) - ra, 0, 1) * 0.25)
        over(img, c(colr), ra * 0.95)
        dm = Mask(W, HC)
        for _ in range(int(90 * rx / 620)):
            a = g.uniform(0, 2 * math.pi)
            rr = g.uniform(rx - 46, rx - 4)
            dm.circle(640 + math.cos(a) * rr, 770 + math.sin(a) * rr * 0.27, g.uniform(1.6, 3.4))
        over(img, c(colr) * 0.78, dm.arr(0.4) * ra)
        add(img, c("#ffffff"), dm.arr(0.4) * ra * 0.15)
    glints = []
    gm = Mask(W, HC)   # 온실 앞마당(풀길)
    gm.ellipse(640, 770, 200, 50)
    over(img, c("#b8e68a"), gm.arr(1.0))
    greenhouse(img, GH_C[0], GH_C[1], 250, seed=1167, glints=glints)
    # 가까운 숲의 꼭대기(발아래)
    near = Mask(W, HC)
    for k in range(18):
        x = k * 80 + g.uniform(-20, 20)
        leaf_cluster(near, x, 990 + g.uniform(-30, 30) + abs(x - 640) * 0.05, g.uniform(90, 130), 1170 + k)
    near.rect(0, 1040, W, HC)
    na = near.arr(0.6)
    paint(img, na, LEAF2, line=1.8, hi=0.2, lo=0.1, light=(1, -1))
    lm = Mask(W, HC)
    for k in range(120):
        lm.ellipse(g.uniform(0, W), g.uniform(900, HC), g.uniform(8, 14), g.uniform(3, 5), g.uniform(-1, 1))
    over(img, c(LEAF_HI), lm.arr(0.5) * na * 0.5)
    sprays = [leaf_spray(1171, W, H, (-30, 120), 0.25, n=9, ln=(120, 180), fill=LEAF),
              leaf_spray(1172, W, H, (W + 30, 200), 2.9, n=9, ln=(120, 180), fill=LEAF2)]
    motes = Particles(20, 1173, (0, 0, W, H), vel=((-8, 8), (-8, 4)), size=(0.8, 1.6))

    def frame(u):
        k = ease_in_out(span(u, 0.1, 3.2))
        oy = lerp(0, 400, k)
        f = img[int(oy):int(oy) + H].copy()
        fy = oy - int(oy)
        if fy > 0:
            f = f * (1 - fy) + img[int(oy) + 1:int(oy) + 1 + H] * fy
        tw = span(u, 1.2, 2.2)
        for j, (gx, gy) in enumerate(glints):
            s = max(0.0, math.sin(u * 3.2 + j * 1.7)) ** 4 * tw
            if s > 0.05:
                sparkle(f, gx, gy - oy, 9 + 7 * s, "#ffffff", s, rot=j)
        add(f, c("#fff6c8"), _gh_glow(oy) * 0.25 * tw)
        motes.draw(f, u, c("#fffbe0"), 0.4)
        for layer, mul in zip(sprays, (1.5, 1.8)):
            put_layer(f, *_shifted(layer, -oy * mul))
        return zoom(f, 1.0 + 0.08 * ease_in_out(span(u, 1.6, 3.6)), GH_C[0], GH_C[1] - 400)

    _cache = {}

    def _gh_glow(oy):
        key = int(oy) // 4
        if key not in _cache:
            _cache.clear()
            _cache[key] = radial(W, H, GH_C[0], GH_C[1] - 60 - key * 4, 170, 2.0)
        return _cache[key]

    return frame


# ══════════════════════════ 샷 4 — 나무와 온실, 둘 다 금빛 ══════════════════════════

GH4_X = 748.0   # 샷4 온실 — 세로 화면(x 437~843) 안


def both_shot():
    img = book_sky(PAL["day"], seed=1180)
    sun(img, 1120, 100, 46)
    cloud(img, 980, 190, 0.7, seed=1181)
    from kit import hills, meadow
    hills(img, 1182, 400, 26, "#a7d98a", ink="#7fae6a", freq=(0.003, 0.008))
    ga = meadow(img, 452, 1183, flowers=90)
    # 거대한 나무(왼쪽 가운데) — 우듬지·줄기를 따로 굽고, 금빛 테두리용 알파를 남긴다
    tm = Mask()
    tm.taper([(505, 470), (500, 380), (486, 300)], 64, 40)
    tm.taper([(495, 340), (420, 280), (360, 250)], 22, 12)
    tm.taper([(500, 330), (590, 270), (650, 250)], 22, 12)
    root = Mask()
    root.poly([(440, 476), (470, 440), (540, 440), (575, 478)])
    trunk_a = np.maximum(tm.arr(0.6), root.arr(0.6))
    crown = Mask()
    for k, (x, y, r) in enumerate(((500, 170, 170), (340, 230, 130), (660, 230, 140), (420, 110, 120),
                                   (590, 100, 125), (500, 60, 110))):
        leaf_cluster(crown, x, y, r, 1184 + k)
    crown_a = crown.arr(0.6)
    paint(img, trunk_a, BARK, line=1.8, hi=0.14, lo=0.14)
    paint(img, crown_a, LEAF2, line=1.8, hi=0.2, lo=0.1, light=(1, -1))
    g = np.random.default_rng(1190)
    lm, dm = Mask(), Mask()
    for k in range(160):
        x, y = g.uniform(170, 830), g.uniform(0, 380)
        (lm if k % 2 else dm).ellipse(x, y, g.uniform(7, 12), g.uniform(3, 5), g.uniform(-1, 1))
    over(img, c(LEAF_HI), lm.arr(0.5) * crown_a * 0.5)
    over(img, c(LEAF3), dm.arr(0.5) * crown_a * 0.35)
    # 나무껍질의 작은 이름 칸(멀리서 보이는 표지)
    pm = Mask()
    pm.rect(486, 392, 518, 426, r=5)
    pa = pm.arr(0.5)
    tree_a = np.maximum(trunk_a, crown_a)
    # 온실 — 오른쪽 꽃밭 가운데
    glints = []
    gh_layer = bake_layer(lambda cv: greenhouse(cv, GH4_X, 492, 176, seed=1191))
    glints += _glint_pts(GH4_X, 492, 176)
    gh_a = gh_layer[1]
    flower_dots(img, 470, 520, 1192, 60, (2.5, 4.0), GH4_X - 120, GH4_X + 100)
    put_layer(img, *gh_layer)
    flower_dots(img, 492, 560, 1193, 40, (3.0, 5.0), GH4_X - 100, GH4_X + 80)
    over(img, c(INK), grow(pa, 1.2))
    over(img, c("#e2bd88"), pa)
    # 금빛 테두리(정적 알파 — 프레임마다 세기만 바꾼다)
    def gold(a):
        line = np.clip(grow(a, 3.0) - a, 0, 1)
        return np.clip(blur_arr(grow(a, 3), 18) * (1 - a * 0.85), 0, 1), line
    tree_glow, tree_line = gold(tree_a)
    gh_glow, gh_line = gold(gh_a)
    motes = Particles(30, 1194, (0, 0, W, H), vel=((-6, 6), (-10, -3)), size=(0.8, 1.8))
    petals = Particles(14, 1195, (0, 0, W, H), vel=((20, 40), (8, 20)), size=(1.6, 2.6), wobble=14, twinkle=0.2)
    burst_tree = [(330, 120), (520, 40), (700, 160), (420, 300), (610, 290)]
    burst_gh = [(GH4_X - 70, 420), (GH4_X + 60, 410), (GH4_X, 380)]
    arc = [(lerp(600, GH4_X, t), lerp(240, 400, t) - math.sin(math.pi * t) * 110) for t in np.linspace(0, 1, 40)]

    def frame(u):
        f = img.copy()
        lt = ease_out(span(u, 0.5, 1.1))
        lg = ease_out(span(u, 1.25, 1.85))
        breathe = 1 + 0.12 * math.sin(u * 3.0)
        if lt > 0:
            add(f, c(GOLD), tree_glow * 0.85 * lt * breathe)
            add(f, c("#fff3b0"), tree_a * 0.1 * lt)
            over(f, c("#ffd84d"), tree_line * lt)
            add(f, c(GOLD), blur_arr(pa, 8) * lt)
        if lg > 0:
            add(f, c(GOLD), gh_glow * 1.0 * lg * breathe)
            add(f, c("#fff3b0"), gh_a * 0.12 * lg)
            over(f, c("#ffd84d"), gh_line * lg)
        for t0, pts in ((0.5, burst_tree), (1.25, burst_gh)):   # 켜지는 순간의 반짝(종소리와 같이)
            k = span(u, t0, t0 + 0.9)
            if 0 < k < 1:
                for j, (bx, by) in enumerate(pts):
                    sparkle(f, bx, by, 8 + 10 * math.sin(math.pi * k), "#fff6c0", math.sin(math.pi * k), rot=j)
        # 두 곳을 잇는 금빛 티끌(둘이 한 짝이라는 것)
        if u > 1.7:
            for j in range(10):
                t = ((u - 1.7) * 0.45 + j / 10) % 1.0
                i = int(t * (len(arc) - 1))
                x, y = arc[i]
                dot(f, x, y, 3.0, c(GOLD), 0.7 * math.sin(math.pi * t) * span(u, 1.7, 2.2))
        for j, (gx, gy) in enumerate(glints):
            s = max(0.0, math.sin(u * 3.0 + j * 1.9)) ** 5
            if s > 0.1:
                sparkle(f, gx, gy, 6 + 6 * s, "#ffffff", s * 0.9, rot=j)
        motes.draw(f, u, c("#fff8d0"), 0.4)
        petals.draw(f, u, c("#ff9ab8"), 0.35)
        return zoom(f, 1.07 - 0.07 * ease_in_out(u / 3.6), 640, 300)

    return frame


SHOTS = [canopy_shot, bark_shot, down_shot, both_shot]


# ══════════════════════════ 소리 ══════════════════════════

def score(sc):
    """높은 나뭇잎 바람·새 → 무늬가 빛날 때 이름의 동기 → 온실 반짝 → 두 곳에 두 번 울리는 종(화음)."""
    T = sc.total
    s = [sc.shot(k) for k in range(4)]
    sc.wind([(0, 0.5), (s[1], 0.35), (s[2], 0.55), (s[3], 0.4), (T, 0.3)], amp=0.05, lo=700, hi=3200, gust=0.7)
    sc.wind([(0, 0.4), (T, 0.4)], amp=0.03, lo=150, hi=600)
    sc.birds([(0, 1.0), (s[1], 0.4), (s[2], 0.9), (s[3], 0.7), (T, 0.4)], amp=0.04, rate=1.1)
    sc.pad([hz("D4"), hz("F#4"), hz("A4"), hz("D5")], [(0, 0), (1.0, 0.8), (s[1], 0.6), (s[2], 0.8), (T - 0.4, 0.9), (T, 0)],
           amp=0.045, bright=0.2)
    # 칸마다 금빛 — 이름의 동기(칼림바 D5 F#5 A5 B5 A5) + 마지막 칸은 한 옥타브 위 D
    for k, nt in enumerate(("D5", "F#5", "A5", "B5", "A5", "D6")):
        sc.pluck(s[1] + 0.75 + 0.3 * k, hz(nt), 0.15, dur=1.6, pan=-0.5 + 0.2 * k, kind="kalimba")
    sc.sparkle(s[1] + 0.8, 1.8, density=6, amp=0.03)
    sc.scratch(s[1] + 0.2, 0.4, 0.02)
    # 온실 반짝
    sc.sparkle(s[2] + 1.3, 1.6, density=8, amp=0.035, lo=2400, hi=5200)
    sc.pad([hz("G4"), hz("B4"), hz("D5")], [(0, 0), (s[2] + 0.6, 0), (s[2] + 1.6, 0.6), (s[3] + 0.4, 0.3), (T, 0)],
           amp=0.03)
    # 두 곳에 두 번 울리는 종 — 나무(D 화음) → 온실(A 화음 → 다시 D)
    for nt in ("D5", "F#5", "A5", "D6"):
        sc.chime(s[3] + 0.5, hz(nt), 0.09, dur=2.6, pan=-0.4)
    for nt in ("A4", "E5", "A5", "C#6"):
        sc.chime(s[3] + 1.25, hz(nt), 0.08, dur=2.6, pan=0.4)
    sc.arp(s[3] + 2.0, ["D5", "F#5", "A5", "D6"], count=8, step=0.16, amp=0.05, kind="box")
    sc.sparkle(s[3] + 0.5, 2.4, density=5, amp=0.025)
