"""v2 도우미 — ch5_summit · ch6_wall · ch8_vault · ch9_archive가 함께 쓰는 그림·소리 부품.

kit.py를 고치지 않고 그 위에 얹는다(kit은 여러 영상 담당이 함께 쓴다). kit으로 옮길 만한 것은 보고서에 적었다.

- 캔버스 크기를 고를 수 있는 이름 벽(`WallCanvas`) — 세로로 긴 벽(틸트), 칸 하나 클로즈업. 생김은 kit.NameWall과 같다.
- 회상 화면(`sepia`·`MemoryFrame`) — 세피아 + 둥근 테두리 + 옛 필름 티끌.
- 연장 쥔 손(`tool_hand`) — 끌·펜. kit.hand_parts가 큰 회전에서 죽는 것을 우회한다(`hand_parts_rot`).
- 잠든 곤충(`draw_insect_asleep`), 3/4 상자(`crate34`), 유적(`ruin`), 구름 덩어리(`cloud_mass`), 빛줄기(`beam`).
- 소리: 이름의 동기(`name_motif`), 날갯짓(`wings`), 끌(`tap`), 삐걱(`creak`), 얼음(`ice_crack`), 퐁(`puff`).
"""
import math

import numpy as np
from PIL import Image

from kit import (INK, INSECT_COLORS, PAL, H, W, Mask, add, blur_arr, c, dot, draw_hand, draw_insect, fbm, glyphs,
                 grow, hand_parts, insect_parts, lerp, local, over, rim, stamp)
import sound

# ══════════════════════════ 합성 ══════════════════════════


def paste(dst, rgb, a, x, y):
    """rgb(h,w,3)·a(h,w)를 dst의 (x,y) **왼쪽 위**에 덮는다(화면 밖은 잘린다)."""
    h, w = a.shape
    x0, y0 = int(round(x)), int(round(y))
    sx0, sy0 = max(0, -x0), max(0, -y0)
    x0c, y0c = max(0, x0), max(0, y0)
    x1c, y1c = min(dst.shape[1], x0 + w), min(dst.shape[0], y0 + h)
    if x1c <= x0c or y1c <= y0c:
        return
    sa = a[sy0:sy0 + (y1c - y0c), sx0:sx0 + (x1c - x0c)][..., None]
    sr = rgb[sy0:sy0 + (y1c - y0c), sx0:sx0 + (x1c - x0c)]
    reg = dst[y0c:y1c, x0c:x1c]
    reg *= 1 - sa
    reg += sr * sa


def window(img, y0, x0=0.0):
    """큰 캔버스에서 W×H 창을 잘라 새 배열로(부분 픽셀)."""
    from kit import crop
    return np.array(crop(img, x0, y0), np.float32)


def vgrad(h, w, stops):
    """세로 그라데이션 색 배열(h,w,3). stops = [(0..1, '#hex')]."""
    y = np.linspace(0, 1, h, dtype=np.float32)
    out = np.zeros((h, 3), np.float32)
    for i in range(len(stops) - 1):
        p0, c0 = stops[i]
        p1, c1 = stops[i + 1]
        m = (y >= p0) & (y <= p1)
        t = ((y[m] - p0) / max(1e-6, p1 - p0))[:, None]
        t = t * t * (3 - 2 * t)
        out[m] = c(c0) * (1 - t) + c(c1) * t
    return np.repeat(out[:, None, :], w, axis=1)


def beam(dst, x0, y0, x1, y1, w0, w1, color="#fff6c8", amount=0.35, soft=18):
    """비스듬한 빛줄기(가산) — 캔버스 크기와 무관(kit.light_beam은 W×H 고정)."""
    hh, ww = dst.shape[:2]
    m = Mask(ww, hh, 1)
    dx, dy = x1 - x0, y1 - y0
    ln = math.hypot(dx, dy) or 1
    nx, ny = -dy / ln, dx / ln
    m.poly([(x0 + nx * w0, y0 + ny * w0), (x1 + nx * w1, y1 + ny * w1),
            (x1 - nx * w1, y1 - ny * w1), (x0 - nx * w0, y0 - ny * w0)])
    a = m.arr(soft)
    yy = np.linspace(0, 1, hh, dtype=np.float32)[:, None]
    fall = np.clip(1.15 - yy * 0.6, 0, 1) if y1 > y0 else 1.0
    add(dst, c(color), a * amount * fall)


# ══════════════════════════ 회상(세피아) ══════════════════════════

SEPIA_DARK, SEPIA_LIGHT = c("#5a3416"), c("#fff2d6")


def sepia(f, amount=0.85, dark=SEPIA_DARK, light=SEPIA_LIGHT):
    lum = f[..., 0] * 0.3 + f[..., 1] * 0.59 + f[..., 2] * 0.11
    lum = np.clip(lum * 1.04, 0, 1)
    sp = dark + (light - dark) * lum[..., None]
    return f * (1 - amount) + sp * amount


class MemoryFrame:
    """옛날 화면 — 둥근 테두리(바깥은 종이색) + 안쪽 비네트 + 필름 티끌·흔들리는 밝기.

    세로 화면(가운데 32%)에서도 옛날로 읽히도록 안쪽 비네트를 가로로 좁게 건다.
    edge·paper로 바깥색을 바꾼다(얼음에 비친 회상은 하늘빛 서리 테두리).
    """

    def __init__(self, inset=30, radius=90, paper="#f4e4c2", edge="#7a5232", seed=0, frost=False, vig=0.55):
        m = Mask()
        m.rect(inset, inset, W - inset, H - inset, r=radius)
        self.inner = m.arr(3)
        ring = np.clip(grow(self.inner, 2.2) - self.inner, 0, 1) + np.clip(self.inner - (1 - grow(1 - self.inner, 3.0)), 0, 1)
        self.ring = np.clip(ring, 0, 1)
        n = fbm(W, H, seed + 31, (160, 50, 14), (0.55, 0.3, 0.15))
        self.border = np.ones((H, W, 3), np.float32) * c(paper)
        self.border *= (0.84 + 0.32 * n)[..., None]
        if frost:   # 서리 무늬 — 가는 흰 줄
            fm = Mask(W, H, 1)
            r = np.random.default_rng(seed + 5)
            for _ in range(70):
                x, y = r.uniform(0, W), r.uniform(0, H)
                a = r.uniform(0, math.pi)
                ln = r.uniform(10, 40)
                fm.line([(x, y), (x + math.cos(a) * ln, y + math.sin(a) * ln)], 1.2)
            add(self.border, c("#ffffff"), fm.arr(0.6) * 0.5)
        self.edge = c(edge)
        yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
        d = ((xx - W / 2) / (W * 0.34)) ** 2 + ((yy - H * 0.46) / (H * 0.52)) ** 2
        self.vig = (vig * np.clip(d - 0.25, 0, 1.6) ** 1.3 / 1.6 ** 1.3)[..., None]   # 갈색 타원 비네트(세로 화면에도 위아래가 보인다)
        self.vig_col = c(edge) * 0.9
        self.rng_seed = seed

    def apply(self, f, t, flicker=0.025, specks=6):
        f = f * (1 - self.vig) + self.vig_col * self.vig
        f *= 1 + flicker * math.sin(t * 23.0) * math.sin(t * 7.3)
        r = np.random.default_rng(int(t * 24) + self.rng_seed * 1000)
        for _ in range(specks):    # 필름 티끌
            dot(f, r.uniform(0, W), r.uniform(0, H), r.uniform(0.6, 1.6), -c("#3a2414"), r.uniform(0.15, 0.4))
        if r.random() < 0.35:      # 세로 긁힘
            x = r.uniform(W * 0.2, W * 0.8)
            f[:, int(x):int(x) + 1] *= 0.9
        a = self.inner[..., None]
        out = f * a + self.border * (1 - a)
        over(out, self.edge, self.ring * 0.8)
        return out


# ══════════════════════════ 이름 벽(캔버스 크기 자유) ══════════════════════════

GREY_CELL = "#cdc6ba"


class WallCanvas:
    """kit.NameWall과 같은 생김의 이름 벽 — 캔버스 크기·칸 크기·칸 수를 고른다.

    칸마다 곤충 그림·이름 무늬를 **따로** 쥐어서, 한 칸만 바래게·새기게·다시 켜지게 할 수 있다.
    """

    def __init__(self, w, h, rows, cols, cell, gap, x0=None, y0=None, seed=401, kinds=("bfly", "beetle", "dfly"),
                 kinds_at=None, glyph_seed=900, fill=None, cell_fill=None, brick=True):
        self.w, self.h, self.rows, self.cols, self.cell, self.gap = w, h, rows, cols, cell, gap
        k = cell / 118.0
        self.k = k
        gw, gh = cols * (cell + gap) - gap, rows * (cell + gap) - gap
        self.x0 = (w - gw) / 2 if x0 is None else x0
        self.y0 = (h - gh) / 2 if y0 is None else y0
        n = fbm(w, h, seed, (max(8, int(200 * k)), max(4, int(70 * k)), max(3, int(20 * k))), (0.5, 0.35, 0.15))
        img = np.ones((h, w, 3), np.float32) * c(fill or PAL["wall"])
        img *= (0.86 + 0.28 * n)[..., None]
        lw = 2.4 * max(1.0, k ** 0.6)
        if brick:
            lm = Mask(w, h)
            bh, bw = 74 * k, 140 * k
            for row in range(-1, int(h / bh) + 3):
                y = row * bh + 20 * k
                lm.line([(0, y), (w, y)], lw)
                x = -bw + (row % 2) * bw / 2
                while x < w + bw:
                    lm.line([(x, y), (x, y + bh)], lw)
                    x += bw
            over(img, c(PAL["wall_line"]), lm.arr(0.8) * 0.55)
        cm = Mask(w, h)
        for r in range(rows):
            for kk in range(cols):
                cx, cy = self.center(r, kk)
                cm.rect(cx - cell / 2, cy - cell / 2, cx + cell / 2, cy + cell / 2, r=10 * k)
        ca = cm.arr(0.6)
        over(img, c(INK), grow(ca, 1.5 * max(1.0, k ** 0.5)) * 0.85)
        over(img, c(cell_fill or PAL["cell"]), ca)
        self.img = img
        cl = local(cell, cell)
        cl.rect(4, 4, cell + 4, cell + 4, r=10 * k)
        self.cell_a = cl.arr(0.6)
        pad = int(cell * 0.35)
        self.cell_glow = blur_arr(np.pad(self.cell_a, pad), cell * 0.12)   # 칸 빛(상자 끝에서 잘리지 않게 여백)
        self.cells = {}
        kinds_at = kinds_at or {}
        for r in range(rows):
            for kk in range(cols):
                kind = kinds_at.get((r, kk)) or kinds[(r * cols + kk) % len(kinds)]
                self.cells[(r, kk)] = self._make_cell(r, kk, kind, glyph_seed)

    def _make_cell(self, r, kk, kind, glyph_seed):
        cell, k = self.cell, self.k
        p = insect_parts(kind, cell * 0.25, rot=0.12 * math.sin(r * 3 + kk))
        ia = np.maximum(p["wing"], p["body"])
        gm = local(cell, cell)
        glyphs(gm, gm.w / 2 - cell * 0.29, gm.h / 2 + cell * 0.25, cell * 0.6, 1, glyph_seed + r * 10 + kk,
               size=11 * k, weight=2.2 * k)
        ga = gm.arr(0.4)
        cx, cy = self.center(r, kk)
        return dict(cx=cx, cy=cy, kind=kind, parts=p, glow=blur_arr(ia, 12 * k) * 0.7,
                    outline=np.clip(grow(ia, 1.6 * k ** 0.5) - ia, 0, 1), glyph=ga, glyph_blur=blur_arr(ga, 3 * k),
                    seed=glyph_seed + r * 10 + kk)

    def set_kind(self, r, kk, kind, glyph_seed=900):
        self.cells[(r, kk)] = self._make_cell(r, kk, kind, glyph_seed)

    def center(self, r, kk):
        return (self.x0 + kk * (self.cell + self.gap) + self.cell / 2,
                self.y0 + r * (self.cell + self.gap) + self.cell / 2)

    def carve_mask(self, r, kk, progress):
        """새기는 중인 이름 무늬(앞에서부터 progress만큼)."""
        cell, k = self.cell, self.k
        gm = local(cell, cell)
        _, cells = glyphs(gm, gm.w / 2 - cell * 0.29, gm.h / 2 + cell * 0.25, cell * 0.6, 1, self.cells[(r, kk)]["seed"],
                          size=11 * k, weight=2.2 * k, progress=progress)
        return gm.arr(0.4), cells

    def draw_cell(self, f, r, kk, lv, icon_alpha=1.0, grey=0.0, glyph_alpha=1.0, glyph=None, outline=0.0, dx=0.0,
                  dy=0.0, icon=True):
        """칸 하나 — lv 빛(0 바램 ~ 1 살아 있음), grey 칸이 잿빛으로 바랜 정도, outline 새김선만 남은 그림."""
        d = self.cells[(r, kk)]
        cx, cy = d["cx"] + dx, d["cy"] + dy
        k = self.k
        if grey > 0:
            stamp(f, self.cell_a * grey, cx, cy, GREY_CELL)
        if outline > 0:
            stamp(f, d["outline"] * outline, cx, cy - 8 * k, "#8a6a48")
        if icon and icon_alpha > 0.01:
            if lv > 0:
                stamp(f, d["glow"] * lv * icon_alpha, cx, cy - 8 * k, PAL["glow"], "add")
            draw_insect(f, d["kind"], cx, cy - 8 * k, self.cell * 0.25, alpha=(0.35 + 0.65 * min(1.0, lv)) * icon_alpha,
                        drain=max(0.0, 0.6 - lv) * 0.9, parts=d["parts"])
        ga = d["glyph"] if glyph is None else glyph
        gb = d["glyph_blur"] if glyph is None else blur_arr(glyph, 3 * k)
        lc = float(np.clip(lv, 0, 1.4))
        stamp(f, ga * (0.35 + 0.5 * min(1.0, lc)) * glyph_alpha, cx, cy, INK)
        if lc > 0:
            stamp(f, gb * lc * 0.5 * glyph_alpha, cx, cy, PAL["glow"], "add")

    def draw(self, f=None, level_fn=lambda r, k: 1.0, skip=()):
        f = self.img.copy() if f is None else f
        for (r, kk) in self.cells:
            if (r, kk) in skip:
                continue
            lv = level_fn(r, kk)
            if lv is None:
                continue
            self.draw_cell(f, r, kk, lv)
        return f


# ══════════════════════════ 손·연장 ══════════════════════════

def hand_parts_rot(s, rot, pose="open", curl=0.0):
    """kit.hand_parts는 |rot|이 크면 소매 직사각형 꼭짓점이 뒤집혀 죽는다 — 0도로 굽고 배열을 돌린다."""
    p = hand_parts(s, 0.0, pose, curl)
    deg = -math.degrees(rot)
    out = dict(p)
    for key in ("skin", "sleeve"):
        im = Image.fromarray(np.clip(p[key] * 255 + 0.5, 0, 255).astype(np.uint8), "L")
        out[key] = np.asarray(im.rotate(deg, resample=Image.Resampling.BICUBIC), np.float32) / 255.0
    return out


def tool_hand(f, tip, theta, length, s, tool="chisel", skin="#f6c9a0", sleeve="#3fae6a", alpha=1.0, curl=0.0):
    """끌·펜을 쥔 손. tip = 연장 끝, theta = 끝에서 손 쪽 방향(라디안, 화면 좌표), length = 끝~주먹 거리."""
    ct, st = math.cos(theta), math.sin(theta)
    gx, gy = tip[0] + ct * length, tip[1] + st * length
    box = int(length * 1.6 + 40)
    m_metal, m_handle = local(box, box), local(box, box)
    o = m_metal.w / 2

    def P(d):
        return (o + (tip[0] + ct * d - gx), o + (tip[1] + st * d - gy))

    if tool == "chisel":
        m_metal.taper([P(0), P(length * 0.5)], s * 0.12, s * 0.22)
        m_handle.taper([P(length * 0.45), P(length * 1.25)], s * 0.3, s * 0.34)
        metal, handle = "#b9c2cc", "#9b6a3c"
    else:   # pen
        m_metal.taper([P(0), P(length * 0.16)], s * 0.04, s * 0.13)
        m_handle.taper([P(length * 0.14), P(length * 1.2)], s * 0.15, s * 0.17)
        metal, handle = "#f0c24a", "#3a3150"
    ma, ha = m_metal.arr(0.5) * alpha, m_handle.arr(0.5) * alpha
    stamp(f, grow(np.maximum(ma, ha), 1.4), gx, gy, INK)
    stamp(f, ha, gx, gy, handle)
    stamp(f, ma, gx, gy, metal)
    stamp(f, rim(np.maximum(ma, ha), -2, -2, 2) * 0.3, gx, gy, "#ffffff", "add")
    rot = theta - math.pi / 2
    wx, wy = gx + 0.62 * s * ct, gy + 0.62 * s * st
    hp = hand_parts_rot(s, rot, "fist", curl)
    draw_hand(f, wx, wy, s, skin=skin, sleeve=sleeve, alpha=alpha, parts=hp)
    return gx, gy


def reach_hand(f, x, y, s, rot, curl=0.0, skin="#f6c9a0", sleeve="#e9dcb8", alpha=1.0, tint=None):
    """편 손(손목 x,y — 손가락은 rot 쪽 '위'). 큰 회전도 된다."""
    hp = hand_parts_rot(s, rot, "open", curl)
    draw_hand(f, x, y, s, skin=skin, sleeve=sleeve, alpha=alpha, parts=hp, tint=tint)


# ══════════════════════════ 곤충 ══════════════════════════

def draw_insect_asleep(f, kind, x, y, s, rot=0.0, alpha=1.0, flap=0.3, colors=None):
    """눈 감은 곤충 — 흰자를 머리색으로 덮고 감은 눈썹(‿)을 긋는다."""
    p = insect_parts(kind, s, rot, flap)
    draw_insect(f, kind, x, y, s, parts=p, alpha=alpha, colors=colors)
    wc, bc, _ = colors or INSECT_COLORS[kind]
    stamp(f, grow(p["white"], 0.8) * alpha, x, y, bc)
    lm = Mask(p["box"], p["box"])
    re = p["hr"] * 0.42
    cr, sr = math.cos(rot), math.sin(rot)
    for ex, ey in p["eyes"]:
        pts = []
        for i in range(7):
            a = math.pi * i / 6
            lx, ly = -math.cos(a) * re, math.sin(a) * re * 0.55
            pts.append((ex + lx * cr - ly * sr, ey + lx * sr + ly * cr))
        lm.line(pts, max(1.2, re * 0.32))
    lum = float(np.dot(c(bc), [0.3, 0.59, 0.11]))
    stamp(f, lm.arr(0.4) * alpha, x, y, "#f4ead8" if lum < 0.35 else "#1a1210")
    return p


# ══════════════════════════ 상자(3/4) ══════════════════════════

def crate34(f, x, base, w, h, d=None, open_=False, tag=True, fill="#c98b4a", plank="#a86e34", seed=0, alpha=1.0,
            shade=0.0):
    """살짝 위에서 본 나무 상자 — 앞면 + 윗면 + 오른 옆면. open_이면 윗면 대신 텅 빈 속이 보이고 뚜껑이 기댄다.

    (x, base)는 앞면 아래 가운데. shade(0..1)는 어둠 속으로 가라앉힌다.
    """
    d = h * 0.3 if d is None else d
    sk = d * 0.7
    bx0, by0 = x - w / 2 - 14, base - h - d - (h * 0.55 if open_ else 0) - 14
    bw, bh = w + sk + 28 + (w * 0.25 if open_ else 0), base - by0 + 14
    ms = [Mask(int(bw) + 1, int(bh) + 1) for _ in range(6)]   # 앞·위·옆·속·뚜껑·줄
    front, top, side, inner, lid, lines = ms

    def L(pts):
        return [(px - bx0, py - by0) for px, py in pts]

    xl, xr, yt = x - w / 2, x + w / 2, base - h
    front.poly(L([(xl, base), (xl, yt), (xr, yt), (xr, base)]))
    top_pts = [(xl, yt), (xr, yt), (xr + sk, yt - d), (xl + sk, yt - d)]
    top.poly(L(top_pts))
    side.poly(L([(xr, base), (xr, yt), (xr + sk, yt - d), (xr + sk, base - d)]))
    ccx, ccy = bx0 + bw / 2, by0 + bh / 2
    for kk in range(1, 4):
        yy = yt + h * kk / 4
        lines.line(L([(xl + 4, yy), (xr - 4, yy)]), 2.4)
    lines.line(L([(xl + 6, yt + 6), (xr - 6, base - 6)]), 3.0)
    for kk in range(1, 3):
        yy = yt + h * kk / 3
        lines.line(L([(xr + 2, yy), (xr + sk - 2, yy - d)]), 2.0)
    if open_:
        rimw = min(w, d) * 0.12
        inner.poly(L([(xl + rimw, yt - rimw * 0.3), (xr - rimw * 0.4, yt - rimw * 0.3), (xr + sk - rimw, yt - d + rimw * 0.4),
                      (xl + sk + rimw * 0.5, yt - d + rimw * 0.4)]))
        lp = [(xr + sk * 0.3, yt - d * 0.2), (xr + sk * 0.3 + w * 0.22, yt - d * 0.2 - h * 0.05),
              (xr + sk * 0.3 + w * 0.16, yt - d - h * 0.55), (xr + sk * 0.3 - w * 0.06, yt - d - h * 0.5)]
        lid.poly(L(lp))
    else:
        for kk in range(1, 3):
            t = kk / 3
            lines.line(L([(xl + sk * t + 3, yt - d * t), (xr + sk * t - 3, yt - d * t)]), 2.0)
    edges = Mask(int(bw) + 1, int(bh) + 1)
    edges.line(L([(xl, yt), (xr, yt), (xr, base)]), 2.0)
    edges.line(L([(xr, yt), (xr + sk, yt - d)]), 2.0)
    arrs = [m.arr(0.5) * alpha for m in ms]
    fa, ta, sa, ia, la, lna = arrs
    fc = c(fill)
    dk = c("#2a1a10")

    def sh(col_, k):
        return lerp(col_, dk, k)

    if open_:   # 뚜껑은 상자 뒤에 기대 있다 — 먼저 칠한다
        stamp(f, grow(la, 1.6), ccx, ccy, INK)
        stamp(f, la, ccx, ccy, sh(fc * 1.05, shade * 0.7))
        stamp(f, rim(la, 2, 2, 2) * 0.14 * (1 - shade), ccx, ccy, "#ffffff", "add")
    union = np.maximum(np.maximum(fa, ta), sa)
    stamp(f, grow(union, 1.6), ccx, ccy, INK)
    stamp(f, sa, ccx, ccy, sh(fc * 0.72, shade * 0.7))
    stamp(f, ta, ccx, ccy, sh(np.clip(fc * 1.16, 0, 1), shade * 0.7))
    stamp(f, fa, ccx, ccy, sh(fc, shade * 0.7))
    if open_:
        stamp(f, ia, ccx, ccy, sh(c("#3a2616"), shade * 0.5))
        iw = Mask(int(bw) + 1, int(bh) + 1)
        rimw2 = min(w, d) * 0.12
        iw.poly(L([(xl + sk, yt - d), (xr + sk, yt - d), (xr + sk, yt - d + d * 0.55), (xl + sk, yt - d + d * 0.55)]))
        stamp(f, iw.arr(0.5) * ia * alpha, ccx, ccy, sh(fc * 0.6, shade * 0.6))   # 안쪽 뒷벽(빛 받는 면)
        il = Mask(int(bw) + 1, int(bh) + 1)   # 안쪽 왼벽(그늘)
        il.poly(L([(xl + rimw2, yt), (xl + sk + rimw2 * 0.5, yt - d), (xl + sk + rimw2 * 0.5, yt - d + d * 0.55),
                   (xl + rimw2, yt + d * 0.55)]))
        stamp(f, il.arr(0.5) * ia * alpha, ccx, ccy, sh(fc * 0.42, shade * 0.6))
    stamp(f, lna * union, ccx, ccy, sh(c(plank), shade * 0.7))
    stamp(f, edges.arr(0.5) * alpha * 0.8, ccx, ccy, INK)
    stamp(f, rim(union, 2, 2, 2) * 0.16 * (1 - shade), ccx, ccy, "#ffffff", "add")
    if tag:
        from kit import scribble
        tm, sm = Mask(int(bw) + 1, int(bh) + 1), Mask(int(bw) + 1, int(bh) + 1)
        tm.rect(*L([(x - w * 0.19, base - h * 0.62)])[0], *L([(x + w * 0.19, base - h * 0.36)])[0], r=3)
        scribble(sm, x - w * 0.15 - bx0, base - h * 0.56 - by0, w * 0.3, 2, seed, row_h=max(6, h * 0.08), weight=1.6)
        ta2 = tm.arr(0.5) * alpha
        stamp(f, grow(ta2, 1.2), ccx, ccy, INK)
        stamp(f, ta2, ccx, ccy, sh(c("#f3ead2"), shade * 0.6))
        stamp(f, sm.arr(0.4) * ta2, ccx, ccy, "#7a6a58")


# ══════════════════════════ 유적 ══════════════════════════

def ruin(f, cx, base, w, h, stone="#ecd59a", inner="#7a5a3a", glow=1.0, seed=0, glyph=1.0, door_glow=1.0, ink=INK,
         line=1.6):
    """금빛 고대 유적(신전) — 계단·기둥·들보·반쯤 무너진 박공. 기둥마다 윤곽이 따로 선다. 들보에 이름 무늬가 빛난다.

    (cx, base)가 맨 아래 계단 가운데. 국소 상자에 굽고 찍는다.
    """
    r = np.random.default_rng(seed)
    bx0, by0 = cx - w * 0.62, base - h * 1.05
    bw, bh = w * 1.24, h * 1.12

    def M():
        return Mask(int(bw) + 1, int(bh) + 1)

    def L(pts):
        return [(px - bx0, py - by0) for px, py in pts]

    ccx, ccy = bx0 + (int(bw) + 1) / 2, by0 + (int(bh) + 1) / 2
    stoneC = c(stone)
    top = base - h * 0.14
    beam_y = base - h * 0.72
    # 안쪽(문간) 어둠과 문 빛
    im = M()
    im.poly(L([(cx - w * 0.36, top), (cx + w * 0.3, top), (cx + w * 0.3, beam_y), (cx - w * 0.36, beam_y)]))
    ia = im.arr(0.5)
    stamp(f, ia, ccx, ccy, inner)
    dm = M()
    dm.rect(*L([(cx - w * 0.09, beam_y + h * 0.12)])[0], *L([(cx + w * 0.09, top)])[0], r=w * 0.08)
    da = dm.arr(2.0)
    if door_glow > 0:
        stamp(f, blur_arr(da, w * 0.08) * door_glow, ccx, ccy, PAL["glow"], "add")
        stamp(f, da * min(1.0, door_glow), ccx, ccy, "#fff0b0")
    # 계단
    for kk in range(3):
        sw = w * (1.02 - kk * 0.07)
        sm = M()
        sm.rect(*L([(cx - sw / 2, base - h * 0.047 * (kk + 1))])[0], *L([(cx + sw / 2, base - h * 0.047 * kk)])[0])
        a = sm.arr(0.5)
        stamp(f, grow(a, line), ccx, ccy, ink)
        stamp(f, a, ccx, ccy, stoneC * (0.92 + 0.04 * kk))
    # 기둥
    cols = 6
    cw = w * 0.07
    for i in range(cols):
        x = cx - w * 0.4 + i * (w * 0.8 / (cols - 1))
        ch = top - beam_y
        broken = i == cols - 1
        if broken:
            ch *= 0.55
        pm = M()
        pm.rect(*L([(x - cw / 2, top - ch)])[0], *L([(x + cw / 2, top)])[0])
        if not broken:
            pm.rect(*L([(x - cw * 0.8, top - ch - h * 0.03)])[0], *L([(x + cw * 0.8, top - ch + h * 0.005)])[0], r=2)
        else:
            pm.poly(L([(x - cw / 2, top - ch), (x - cw * 0.1, top - ch - h * 0.05), (x + cw * 0.2, top - ch - h * 0.02),
                       (x + cw / 2, top - ch - h * 0.06), (x + cw / 2, top - ch)]))
        pa = pm.arr(0.5)
        stamp(f, grow(pa, line), ccx, ccy, ink)
        stamp(f, pa, ccx, ccy, stoneC)
        fl = M()
        for j in (-0.2, 0.2):
            fl.line(L([(x + cw * j, top - ch + h * 0.04), (x + cw * j, top - h * 0.01)]), max(1.0, w * 0.004))
        stamp(f, fl.arr(0.5) * pa, ccx, ccy, stoneC * 0.78)
    # 들보·박공(오른쪽이 무너졌다)
    right = cx + w * 0.3
    bm = M()
    bm.poly(L([(cx - w * 0.48, beam_y + h * 0.01), (right, beam_y + h * 0.01), (right - w * 0.02, beam_y - h * 0.1),
               (cx - w * 0.48, beam_y - h * 0.1)]))
    bm.poly(L([(cx - w * 0.48, beam_y - h * 0.1), (right - w * 0.02, beam_y - h * 0.1),
               (right - w * 0.02, beam_y - h * 0.1 - h * 0.25 * (1 - (right - cx) / (w * 0.5))), (cx, beam_y - h * 0.34)]))
    ba = bm.arr(0.5)
    stamp(f, grow(ba, line), ccx, ccy, ink)
    stamp(f, ba, ccx, ccy, stoneC * 1.03)
    if glyph > 0:
        gm = M()
        glyphs(gm, cx - w * 0.42 - bx0, beam_y - h * 0.083 - by0, w * 0.68, 1, seed + 3, size=h * 0.05,
               weight=max(1.2, h * 0.008))
        ga = gm.arr(0.4)
        stamp(f, ga * 0.7, ccx, ccy, "#8a6420")
        stamp(f, blur_arr(ga, max(2, h * 0.012)) * glyph, ccx, ccy, PAL["glow"], "add")
    # 잔해
    rb = M()
    for _ in range(9):
        x = cx + r.uniform(0.2, 0.62) * w
        s = r.uniform(0.02, 0.045) * w
        rb.poly(L([(x - s, base), (x + s, base - s * 0.2), (x + s * 0.8, base - s * 0.9), (x - s * 0.7, base - s * 0.8)]))
    ra = rb.arr(0.5)
    stamp(f, grow(ra, line), ccx, ccy, ink)
    stamp(f, ra, ccx, ccy, stoneC * 0.9)
    union = np.maximum(np.maximum(ba, ra), ia)
    if glow > 0:
        stamp(f, rim(union, -3, -3, 3) * 0.5 * glow, ccx, ccy, PAL["glow"], "add")


# ══════════════════════════ 구름 ══════════════════════════

def cloud_mass(w, h, seed, n=26, rmin=40, rmax=110, top=0.3, colors=("#fff4ee", "#e6d3f0", "#b7a3dc"),
               ink="#9a88c8", lit="#ffd9b0", lit_amount=0.35, open_side=None, pad=None):
    """구름 바다 덩어리 — (rgb, alpha). 윗면이 울퉁불퉁, 아래는 상자 끝까지 찬다. 위쪽 테에 해 빛.

    open_side='left'|'right'면 그쪽 옆면도 뭉게뭉게하다(갈라지는 구름의 안쪽 가장자리).
    """
    r = np.random.default_rng(seed)
    pad = rmax if pad is None else pad
    m = Mask(w, h)
    x0 = pad if open_side == "left" else 0
    x1 = w - pad if open_side == "right" else w
    for _ in range(n):
        rad = r.uniform(rmin, rmax)
        x = r.uniform(x0, x1)
        y = h * top + r.uniform(-0.1, 0.25) * h + rad * 0.4
        m.circle(x, y, rad)
    m.rect(x0, h * (top + 0.25), x1, h + 10)
    if open_side:
        ex = x1 if open_side == "right" else x0
        y = h * top + rmax * 0.5
        while y < h + rmax:
            rad = r.uniform(rmin, rmax) * 0.8
            m.circle(ex + r.uniform(-0.35, 0.15) * rad * (1 if open_side == "right" else -1), y, rad)
            y += rad * r.uniform(0.9, 1.3)
    a = m.arr(1.2)
    rgb = vgrad(h, w, [(0, colors[0]), (0.55, colors[1]), (1, colors[2])])
    sh = Mask(w, h)
    for _ in range(n // 2):
        rad = r.uniform(rmin, rmax) * 0.8
        sh.circle(r.uniform(0, w), h * (top + 0.25) + r.uniform(0, 0.4) * h, rad)
    over(rgb, c(colors[2]), sh.arr(rmin * 0.5) * 0.35)
    add(rgb, c(lit), rim(a, 0, 6, 6) * lit_amount)
    line = np.clip(grow(a, 1.4) - a, 0, 1)
    over(rgb, c(ink), line * 0.8)
    a = np.clip(np.maximum(a, line), 0, 1)
    return rgb, a


# ══════════════════════════ 소리 ══════════════════════════

NAME_MOTIF = ("D5", "F#5", "A5", "B5", "A5")


def name_motif(sc, at, amp=0.14, step=0.3, kind="kalimba", pan=0.0, octave=0, last=1.0):
    """이름의 동기 — 칼림바 D5 F#5 A5 B5 A5(박 0.3초)."""
    notes = NAME_MOTIF
    if octave:
        notes = tuple(n[:-1] + str(int(n[-1]) + octave) for n in notes)
    sc.melody(at, list(notes[:-1]) + [(notes[-1], 2.0)], step=step, amp=amp, kind=kind, pan=pan)
    return at + step * (len(notes) - 1)


def wings(sc, at, dur, amp=0.05, rate=24.0, lo=140, hi=1100, pan=0.0):
    """날갯짓 — 대역 잡음을 날갯짓 속도로 떨게 한다."""
    i0, m = sc._seg(at, dur)
    if m <= 0:
        return
    tt = np.arange(m) / sound.SR
    env = np.sin(np.pi * np.clip(tt / dur, 0, 1)) ** 0.6
    y = sc._band(m, lo, hi) * (0.45 + 0.55 * np.abs(np.sin(np.pi * rate * tt))) * env * amp
    sc._mix(i0, y, pan)


def tap(sc, at, amp=0.12, f=1650.0, pan=0.0):
    """끌이 돌을 톡 — 짧은 잡음 + 나무 실로폰 한 음."""
    sc.noise(at, 0.05, 1500, 7000, amp, decay=75, pan=pan)
    sc.pluck(at, f, amp * 0.45, dur=0.25, pan=pan, kind="xylo")


def creak(sc, at, dur=0.6, amp=0.05, f=190.0, pan=0.0):
    """나무 삐걱 — 거칠게 떨리는 낮은 톱니."""
    i0, m = sc._seg(at, dur)
    if m <= 0:
        return
    tt = np.arange(m) / sound.SR
    fr = f * (1 + 0.18 * np.sin(2 * np.pi * 1.3 * tt / dur) + 0.04 * np.sin(2 * np.pi * 7 * tt))
    ph = np.cumsum(fr) / sound.SR
    sig = sum(np.sin(2 * np.pi * ph * k) / k for k in range(1, 7))
    grain = 0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 31 * tt + np.sin(2 * np.pi * 5 * tt)))
    env = np.sin(np.pi * np.clip(tt / dur, 0, 1)) ** 0.7
    sc._mix(i0, sig * grain * env * amp, pan)


def ice_crack(sc, at, amp=0.18, pan=0.0):
    """얼음 쩡 — 높은 잡음 터짐 + 미끄러져 내려가는 맑은 음."""
    sc.noise(at, 0.09, 2500, 9500, amp, decay=40, pan=pan)
    i0, m = sc._seg(at, 1.3)
    tt = np.arange(m) / sound.SR
    fr = 2600 * np.exp(-tt * 3.0) + 700
    y = np.sin(2 * np.pi * np.cumsum(fr) / sound.SR) * np.exp(-tt * 3.6) * amp * 0.35
    sc._mix(i0, y, pan)
    sc.chime(at + 0.01, 2349.3, amp * 0.25, dur=1.2, pan=-pan)


def puff(sc, at, amp=0.12, pan=0.0):
    """퐁 — 연기처럼 사라질 때."""
    sc.noise(at, 0.45, 250, 2200, amp, decay=7, pan=pan)
    sc.pluck(at, sound.hz("A3"), amp * 0.5, dur=0.8, pan=pan, kind="kalimba")


def breath(sc, at, dur=1.2, amp=0.05, pan=0.0):
    """입김 — 부드러운 숨."""
    sc.whoosh(at, dur, amp, 400, 3200, pan_from=pan, pan_to=pan)


# ══════════════════════════ 옆모습 ══════════════════════════

def profile(f, who, x, feet, h, face, alpha=1.0):
    """kit.draw_person(face=±1) 위에 옆얼굴을 얹는다 — 머리 앞쪽 반이 살색 옆얼굴(코 끝 볼록), 뒤쪽은 머리카락.

    kit의 face 인자는 귀·뺨을 머리카락 **아래**에 그려서 긴 머리·모자 인물은 뒷모습으로 읽힌다. 눈·입은 그리지 않는다.
    """
    from kit import PEOPLE
    d = PEOPLE[who]
    build = d["build"]
    kid = build == "kid"
    hunch = h * 0.06 if build == "old" else 0.0
    hr = h * (0.085 if kid else 0.072)
    hx = x + face * h * 0.03 + hunch * 0.8
    hy = feet - h + hr + hunch * 0.6
    sg = 1 if face > 0 else -1
    box = int(hr * 6) + 16
    o = box / 2
    sk, hair, hat, band = (Mask(box, box) for _ in range(4))
    sk.ellipse(o + sg * hr * 0.42, o + hr * 0.14, hr * 0.66, hr * 0.84)
    sk.circle(o + sg * hr * 1.02, o + hr * 0.06, hr * 0.2)               # 코 끝
    sk.ellipse(o + sg * hr * 0.55, o + hr * 0.72, hr * 0.32, hr * 0.2)   # 턱
    hair.ellipse(o - sg * hr * 0.2, o - hr * 0.3, hr * 0.98, hr * 0.66)  # 이마 머리선
    hair.ellipse(o - sg * hr * 0.45, o + hr * 0.1, hr * 0.62, hr * 0.86)  # 뒤통수
    if d.get("hat"):
        hat.ellipse(o + sg * hr * 0.2, o - hr * 0.15, hr * 1.9, hr * 0.38)
        hat.rect(o - hr * 0.8, o - hr * 1.15, o + hr * 0.8, o - hr * 0.1, r=hr * 0.25)
        if d.get("band"):
            band.rect(o - hr * 0.8, o - hr * 0.42, o + hr * 0.8, o - hr * 0.22)
    if d.get("cap"):
        hat.ellipse(o, o - hr * 0.35, hr * 1.02, hr * 0.72)
        hat.ellipse(o + sg * hr * 0.9, o - hr * 0.05, hr * 0.7, hr * 0.18)   # 모자 챙이 앞으로
    ska, ha, hta, bda = (m.arr(0.5) * alpha for m in (sk, hair, hat, band))
    union = np.maximum(np.maximum(ska, ha), hta)
    stamp(f, grow(union, 1.4), hx, hy, INK)
    stamp(f, ska, hx, hy, d["skin"])
    stamp(f, ha, hx, hy, d["hair"])
    if hta.max() > 0:
        stamp(f, hta, hx, hy, d.get("hat") or d.get("cap"))
        if bda.max() > 0:
            stamp(f, bda, hx, hy, d["band"])
    stamp(f, rim(union, 2, 2, 2) * 0.14 * alpha, hx, hy, "#ffffff", "add")
