"""마지막 장 영상(fin_shadow · fin_return · fin_epilogue)이 함께 쓰는 도우미.

kit.py에 없는 것만 둔다 — 색 빼기(텅 빈 들), 빛으로 된 곤충(돌아가는 이름), 미리 구운 그림자 모습,
상상 장면의 구름 테두리, 반짝 별 캐시, 팔 소매, 잔물결 일렁임. kit로 옮길 만한 것은 보고서에 적는다.
"""
import math

import numpy as np

from kit import (INK, PAL, H, W, Mask, add, bake_layer, blur_arr, book_sky, c, dot, fbm, grow, hills, insect_parts,
                 lerp, meadow, over, paint_fence, paint_plate, put_layer, radial, ridge, rim, stamp)

# 텅 빈 들 — 이름이 빠져 색이 바랜 회보라
GREY_TINT = "#c9c2dc"


def padded(a, blur):
    """블러 전에 둘레를 넉넉히(반경의 3배) 비운다 — 작은 상자 안에서 번짐 끝이 네모나게 잘리지 않게."""
    m = int(math.ceil(blur * 3)) + 2
    return np.pad(a, m)


def luminance(img):
    return img[..., 0] * 0.3 + img[..., 1] * 0.59 + img[..., 2] * 0.11


def drain(img, amount, tint=GREY_TINT, lift=0.12, contrast=1.22):
    """색을 뺀다 — amount는 수(0..1) 또는 화면 크기 알파. 회색에 보랏빛을 살짝 얹는다(대비는 조금 살린다)."""
    lum = np.clip((luminance(img) - 0.55) * contrast + 0.55, 0, 1)
    g = ((lift + (1 - lift) * lum)[..., None] * c(tint)).astype(np.float32)
    if np.ndim(amount) == 2:
        a = amount[..., None]
        return img * (1 - a) + g * a
    return img * (1 - amount) + g * amount


def wash(img, amount=0.35, color="#f6f2ff"):
    """흐린 색 — 종이에 물을 탄 듯 밝고 옅게(상상 장면)."""
    return img * (1 - amount) + c(color) * amount


def field_scene(seed, horizon, sky=None, far="#a8d98a", flowers=50, ground_y=None):
    """초원(색 있는 원본) — 하늘·먼 언덕·풀밭. 색을 뺄 때는 drain()을 덮는다."""
    img = book_sky(sky or PAL["day"], seed=seed)
    hills(img, seed + 1, horizon - 26, 26, far, line=1.2, hi=0.06)
    meadow(img, ground_y if ground_y is not None else horizon, seed + 2, flowers=flowers)
    return img


def fence_posts(x_last, step, n):
    """x_last에서 왼쪽으로 n개의 말뚝 자리."""
    return [x_last - step * k for k in range(n)][::-1]


def fence_row(img, posts, base, pw, ph, kinds, level=0.0, glow=False):
    paint_fence(img, posts, base, pw, ph)
    for i, x in enumerate(posts):
        paint_plate(img, x, base, pw, ph, kinds[i % len(kinds)], level=level, glow=glow)


def broken_rails(img, x_from, x_to, base, pw, ph, side=1):
    """빈칸 쪽으로 끊긴 가로대 토막(말뚝에서 조금만 나온다)."""
    m = Mask()
    for ry in (0.3, 0.74):
        y = base - ph * ry
        x0, x1 = (x_from, x_from + side * (x_to - x_from)) if side > 0 else (x_from, x_to)
        xa, xb = min(x0, x1), max(x0, x1)
        jag = 6 * side
        if side > 0:
            m.poly([(xa, y - pw * 0.13), (xb, y - pw * 0.13 + 4), (xb - jag, y + pw * 0.13), (xa, y + pw * 0.13)])
        else:
            m.poly([(xa - jag, y - pw * 0.13 - 3), (xb, y - pw * 0.13), (xb, y + pw * 0.13), (xa, y + pw * 0.13)])
    a = m.arr(0.6)
    over(img, c(INK), grow(a, 1.6))
    over(img, c(PAL["rail"]), a)


# ══════════════════════════ 그림자의 모습 ══════════════════════════

class ShadowForm:
    """그림자가 빌린 곤충 모습 — kit.shadow_insect와 같은 칠을 미리 굽는다(매 프레임 마스크·블러를 다시 안 만든다)."""

    def __init__(self, kind, s, rot=0.0, flaps=(0.0,)):
        self.kind, self.s = kind, s
        self.items = []
        for fl in flaps:
            p = insect_parts(kind, s, rot, fl)
            u = np.maximum(p["wing"], p["body"])
            sb = max(3, s * 0.12)
            sp = blur_arr(padded(p["spot"], sb), sb) if kind == "firefly" else None
            self.items.append(dict(u=u, glow=blur_arr(padded(u, 10), 10) * 0.6, ink=grow(u, 1.8),
                                   rim=rim(u, 2, 2, 3) * 0.7, eyes=p["eyes"], box=p["box"], hr=p["hr"], spot=sp))

    def draw(self, f, x, y, t, alpha=1.0, k=0, eyes=1.0):
        if alpha <= 0.01:
            return
        it = self.items[k % len(self.items)]
        wob = 1 + 0.08 * math.sin(t * 5)
        stamp(f, it["glow"] * alpha * wob, x, y, PAL["shadow_glow"], "add")
        stamp(f, it["ink"] * alpha, x, y, "#1a1030")
        stamp(f, it["u"] * alpha, x, y, PAL["shadow"])
        stamp(f, it["rim"] * alpha, x, y, PAL["shadow_glow"], "add")
        if it["spot"] is not None:     # 반딧불이 모습 — 꽁무니가 흐린 보랏빛으로 빛난다
            stamp(f, it["spot"] * alpha * (0.7 + 0.3 * math.sin(t * 4)), x, y, "#c9b4ff", "add")
        cc = it["box"] / 2
        for ex, ey in it["eyes"]:
            dot(f, x + ex - cc, y + ey - cc, it["hr"] * 1.2, c(PAL["eye"]), 0.45 * alpha * eyes)
            dot(f, x + ex - cc, y + ey - cc, it["hr"] * 0.75, c(PAL["eye"]), 0.95 * alpha * eyes)


class LightBug:
    """빛으로 된 곤충 — 그림자에서 빠져나온 이름. 금빛 테두리로 모양이 읽히게, 번짐은 은은하게."""

    def __init__(self, kind, s, rot=0.0, flaps=(0.0,), color="#ffd86a"):
        self.color = color
        self.items = []
        for fl in flaps:
            p = insect_parts(kind, s, rot, fl)
            u = np.maximum(p["wing"], p["body"])
            gb, hb = max(5, s * 0.16), max(12, s * 0.45)
            self.items.append(dict(u=u, body=p["body"], glow=blur_arr(padded(u, gb), gb),
                                   halo=blur_arr(padded(u, hb), hb), edge=grow(u, 1.6),
                                   core=rim(u, 2, 2, 2)))

    def draw(self, f, x, y, alpha=1.0, k=0, glow=1.0):
        if alpha <= 0.01:
            return
        it = self.items[k % len(self.items)]
        stamp(f, it["halo"] * 0.32 * alpha * glow, x, y, self.color, "add")
        stamp(f, it["glow"] * 0.34 * alpha * glow, x, y, self.color, "add")
        stamp(f, it["edge"] * 0.85 * alpha, x, y, "#d9922e")
        stamp(f, it["u"] * 0.92 * alpha, x, y, "#ffe9a6")
        stamp(f, it["body"] * 0.9 * alpha, x, y, "#fff8dc")
        stamp(f, it["core"] * 0.6 * alpha, x, y, "#ffffff", "add")


# ══════════════════════════ 반짝임·빛 ══════════════════════════

_SPARK = {}


def _spark_sprite(size):
    key = int(round(size))
    if key not in _SPARK:
        s = max(2, key)
        m = Mask(int(s * 2.6) + 8, int(s * 2.6) + 8)
        cc = m.w / 2
        pts = [(0, -1.2), (0.18, -0.18), (1.2, 0), (0.18, 0.18), (0, 1.2), (-0.18, 0.18), (-1.2, 0), (-0.18, -0.18)]
        m.poly([(cc + x * s, cc + y * s) for x, y in pts])
        a = m.arr(0.4)
        _SPARK[key] = (a, blur_arr(padded(a, s * 0.4), s * 0.4))
    return _SPARK[key]


def spark(f, x, y, size, amount=1.0, color="#fff6c0"):
    """네 갈래 반짝(크기별로 굽어 둔다 — kit.sparkle의 빠른 판)."""
    if amount <= 0.01:
        return
    a, g = _spark_sprite(size)
    stamp(f, g * amount, x, y, color, "add")
    stamp(f, a * amount, x, y, "#ffffff", "add")


_GLOW = {}


def glow_spot(f, x, y, r, color, amount):
    """작은 국소 빛(가산) — 전체 화면 radial보다 싸다. 반지름별로 굽어 둔다."""
    if amount <= 0.01:
        return
    key = int(round(r))
    if key not in _GLOW:
        n = key * 2 + 4
        yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
        d = np.sqrt((xx - n / 2) ** 2 + (yy - n / 2) ** 2) / max(1.0, key)
        _GLOW[key] = np.clip(1 - d, 0, 1) ** 2
    stamp(f, _GLOW[key] * amount, x, y, color, "add")


def thread(f, pts, color="#ffd86a", amount=1.0, width=2.0):
    """금빛 실 한 가닥(점선을 더한다)."""
    for x, y in pts:
        dot(f, x, y, width, c(color), amount)


def ring(f, x, y, r, amount, color=None, width=3.0, squash=1.0):
    """일렁이는 고리(가산) — 모습이 바뀔 때."""
    if amount <= 0.01 or r < 2:
        return
    n = int(r * 2 + width * 6) + 8
    m = Mask(n, n)
    m.d.ellipse([(n / 2 - r) * m.ss, (n / 2 - r * squash) * m.ss, (n / 2 + r) * m.ss, (n / 2 + r * squash) * m.ss],
                outline=255, width=max(1, int(width * m.ss)))
    stamp(f, blur_arr(m.arr(0.5), width * 0.6) * amount, x, y, color or PAL["shadow_glow"], "add")


def wobble_region(f, x0, y0, x1, y1, amp, t, freq=0.06, speed=7.0):
    """사각 영역의 줄마다 가로로 조금씩 밀어 일렁이게 한다(제자리)."""
    x0, y0, x1, y1 = (int(max(0, x0)), int(max(0, y0)), int(min(W, x1)), int(min(H, y1)))
    if x1 - x0 < 4 or y1 - y0 < 4 or amp < 0.3:
        return
    reg = f[y0:y1, x0:x1].copy()
    for r in range(y1 - y0):
        d = int(round(math.sin((y0 + r) * freq + t * speed) * amp))
        if d:
            f[y0 + r, x0:x1] = np.roll(reg[r], d, axis=0)


# ══════════════════════════ 상상 장면 ══════════════════════════

def dream_border(seed=7, inset=(118, 92), bump=58):
    """구름 모양 테두리 — 안쪽(보이는 곳)은 둥근 사각형 + 가장자리의 동그라미들. (안쪽 알파, 테두리 선 알파)."""
    r = np.random.default_rng(seed)
    m = Mask()
    ix, iy = inset
    m.rect(ix, iy, W - ix, H - iy, r=40)
    pts = []
    per = 2 * ((W - 2 * ix) + (H - 2 * iy))
    n = int(per / (bump * 1.35))
    for k in range(n):
        d = per * k / n
        if d < W - 2 * ix:
            x, y = ix + d, iy
        elif d < (W - 2 * ix) + (H - 2 * iy):
            x, y = W - ix, iy + d - (W - 2 * ix)
        elif d < 2 * (W - 2 * ix) + (H - 2 * iy):
            x, y = W - ix - (d - (W - 2 * ix) - (H - 2 * iy)), H - iy
        else:
            x, y = ix, H - iy - (d - 2 * (W - 2 * ix) - (H - 2 * iy))
        rr = bump * r.uniform(0.82, 1.12)
        pts.append((x, y, rr))
        m.circle(x, y, rr)
    inner = m.arr(0.8)
    edge = np.clip(grow(inner, 2.2) - inner, 0, 1)
    return inner, edge


def dream_dots(f, x, y, t, amount=1.0, fill="#fbf8ff"):
    """생각 풍선 꼬리 — 작은 구름 동그라미 셋(아래에서 위로)."""
    for k, (dx, dy, r) in enumerate(((0, 0, 9), (14, -30, 14), (34, -66, 20))):
        a = amount * (0.4 + 0.6 * max(0.0, min(1.0, t * 3 - k)))
        if a <= 0.01:
            continue
        m = Mask(int(r * 2) + 16, int(r * 2) + 16)
        m.circle(m.w / 2, m.h / 2, r)
        aa = m.arr(0.6) * a
        stamp(f, grow(aa, 1.5), x + dx, y + dy, "#8f86ad")
        stamp(f, aa, x + dx, y + dy, fill)


# ══════════════════════════ 팔·소매 ══════════════════════════

def sleeve(f, x0, y0, x1, y1, w0, w1, color, alpha=1.0):
    """손목(x0,y0)에서 화면 밖(x1,y1)으로 이어지는 소매 — draw_hand의 짧은 소매 뒤에 깐다(국소 상자에 굽는다)."""
    dx, dy = x1 - x0, y1 - y0
    ln = math.hypot(dx, dy) or 1
    nx, ny = -dy / ln, dx / ln
    pts = [(x0 + nx * w0, y0 + ny * w0), (x1 + nx * w1, y1 + ny * w1), (x1 - nx * w1, y1 - ny * w1),
           (x0 - nx * w0, y0 - ny * w0)]
    bx0 = max(-8.0, min(p[0] for p in pts) - 10)
    by0 = max(-8.0, min(p[1] for p in pts) - 10)
    bx1 = min(W + 8.0, max(p[0] for p in pts) + 10)
    by1 = min(H + 8.0, max(p[1] for p in pts) + 10)
    if bx1 <= bx0 or by1 <= by0:
        return None
    m = Mask(int(bx1 - bx0) + 1, int(by1 - by0) + 1)
    m.poly([(px - bx0, py - by0) for px, py in pts])
    a = m.arr(0.6) * alpha
    cx, cy = bx0 + m.w / 2, by0 + m.h / 2
    stamp(f, grow(a, 1.6), cx, cy, INK)
    stamp(f, a, cx, cy, color)
    stamp(f, rim(a, 3, 3, 3) * 0.12, cx, cy, "#ffffff", "add")
    return a


def warm_light(img, x, y, r, color="#fff0c0", amount=0.5):
    add(img, c(color), radial(W, H, x, y, r, 2.2) * amount)


def lerp_img(a, b, t):
    return a * (1 - t) + b * t


def bloom_mask(x, y, r, soft=0.35):
    """(x,y)에서 반지름 r까지 번지는 부드러운 원 알파(색이 돌아오는 자리)."""
    if r <= 1:
        return np.zeros((H, W), np.float32)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    d = np.sqrt((xx - x) ** 2 + (yy - y) ** 2)
    return np.clip((r - d) / max(1.0, r * soft), 0, 1)


_GRID = {}


def dist_field(x, y):
    """(x,y)에서의 거리장 — 같은 중심이면 캐시해 둔다."""
    key = (round(x), round(y))
    if key not in _GRID:
        yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
        _GRID[key] = np.sqrt((xx - x) ** 2 + (yy - y) ** 2)
    return _GRID[key]


def bloom_from(dist, r, soft=0.35):
    if r <= 1:
        return np.zeros(dist.shape, np.float32)
    return np.clip((r - dist) / max(1.0, r * soft), 0, 1)


def lerp_c(a, b, t):
    return lerp(c(a), c(b), t)


# ══════════════════════════ 텅 빈 들(울타리 앞뒤 두 층) ══════════════════════════

FENCE_KINDS = ["beetle", "bfly", "dfly", "firefly", "longhorn", "moth"]


def outside_bg(seed, horizon, sky=None):
    """울타리 바깥까지 보이는 배경 — 하늘·보랏빛 언덕·안개. (색 원본, 바깥 언덕 알파)."""
    img = book_sky(sky or PAL["day"], seed=seed)
    far = ridge(Mask(), seed + 1, horizon - 70, 34, freq=(0.003, 0.009, 0.02)).arr(1.5)
    over(img, c(PAL["outside"]), far)
    yy = np.arange(H, dtype=np.float32)[:, None]
    fogb = np.clip(1 - np.abs(yy - (horizon - 30)) / 120, 0, 1) ** 1.5 * np.ones((1, W), np.float32)
    n = fbm(W, H, seed + 2, (240, 90), (0.7, 0.3))
    over(img, c(PAL["fog"]), fogb * (0.35 + 0.5 * n))
    return img, far


class TwoLayer:
    """울타리 바깥(배경)과 안쪽(앞층: 울타리·풀밭)을 따로 굽는다 — 색 있는 판과 색 뺀 판을 함께.

    그림자는 울타리 **바깥**에 산다: 배경 → 그림자 → 앞층 순으로 얹으면 말뚝 사이로 엿본다.
    색이 돌아오는 장면은 mix(화면 알파)로 두 판을 섞는다.
    """

    def __init__(self, seed, horizon, front_draw, sky=None, outside_keep=0.5, amount=0.88, color=True):
        bg, far = outside_bg(seed, horizon, sky)
        prem, a = bake_layer(front_draw)
        amt = amount * (1 - far * (1 - a) * outside_keep)
        full = bg * (1 - a[..., None]) + prem
        self.a = a
        self.bg_d = drain(bg, amt).astype(np.float32)
        self.fr_d = (drain(full, amt) * a[..., None]).astype(np.float32)
        self.bg_c = bg.astype(np.float32) if color else None
        self.fr_c = prem.astype(np.float32) if color else None

    def back(self, mix=None):
        if mix is None:
            return self.bg_d.copy()
        m = mix[..., None] if np.ndim(mix) == 2 else mix
        return self.bg_d + (self.bg_c - self.bg_d) * m

    def front(self, f, mix=None):
        if mix is None:
            return put_layer(f, self.fr_d, self.a)
        m = mix[..., None] if np.ndim(mix) == 2 else mix
        return put_layer(f, self.fr_d + (self.fr_c - self.fr_d) * m, self.a)


def fence_with_gap(cv, left_posts, gap_x, right_posts, base, pw, ph, seed, level=0.0, flowers=40, hole=True,
                   kinds=FENCE_KINDS, extra=None):
    """빈칸 하나가 난 울타리 + 안쪽 풀밭 — 빈칸 양옆 말뚝에서 끊긴 가로대 토막이 조금 나온다."""
    paint_fence(cv, left_posts, base, pw, ph)
    if right_posts:
        paint_fence(cv, right_posts, base, pw, ph)
    stub = (gap_x - left_posts[-1]) * 0.36
    broken_rails(cv, left_posts[-1], left_posts[-1] + stub, base, pw, ph, side=1)
    if right_posts:
        broken_rails(cv, right_posts[0] - stub, right_posts[0], base, pw, ph, side=-1)
    posts = list(left_posts) + list(right_posts)
    for i, x in enumerate(posts):
        paint_plate(cv, x, base, pw, ph, kinds[i % len(kinds)], level=level, glow=level > 0)
    meadow(cv, base - 6, seed, flowers=flowers)
    if extra is not None:
        extra(cv)
    if hole:
        m = Mask()                       # 말뚝이 빠진 자리(흙 구멍)
        m.ellipse(gap_x, base + 2, pw * 0.7, pw * 0.2)
        over(cv, c("#5d566b"), m.arr(0.8))
    return posts


def lit_plates(posts, base, pw, ph, kinds=FENCE_KINDS):
    """켜진 이름표만 구운 층 — 색 뺀 들 위에서도 금빛으로 남는다."""
    def draw(cv):
        for i, x in enumerate(posts):
            paint_plate(cv, x, base, pw, ph, kinds[i % len(kinds)], level=1.0)
    return bake_layer(draw)


# ══════════════════════════ 손·작은 층 ══════════════════════════

def rotate_alpha(a, rot, cx=None, cy=None):
    """알파를 (cx,cy) 둘레로 rot(라디안, 화면에서 시계 방향 +)만큼 돌린다."""
    from PIL import Image
    h, w = a.shape
    cx = w / 2 if cx is None else cx
    cy = h / 2 if cy is None else cy
    im = Image.fromarray(np.ascontiguousarray(a, np.float32), "F")
    im = im.rotate(-math.degrees(rot), resample=Image.Resampling.BILINEAR, center=(cx, cy))
    return np.clip(np.asarray(im, np.float32), 0, 1)


def hand_parts_safe(s, rot=0.0, pose="open", curl=0.0, sleeve_len=2.2):
    """kit.hand_parts를 rot=0으로 굽고 알파를 돌린다 — kit은 rot가 약 -0.3보다 작으면 소매 사각형이 뒤집혀 PIL이 멈춘다."""
    from kit import hand_parts
    p = hand_parts(s, 0.0, pose, curl, sleeve_len)
    if abs(rot) > 1e-4:
        p = dict(p, skin=rotate_alpha(p["skin"], rot), sleeve=rotate_alpha(p["sleeve"], rot))
    return p


class Piece:
    """작은 상자에 한 번 구운 그림(미리 곱한 색 + 알파) — 매 프레임 (x,y)에 찍는다(정수 자리)."""

    def __init__(self, draw, w, h, origin=None):
        from kit import bake_layer
        self.w, self.h = int(w), int(h)
        self.prem, self.a = bake_layer(draw, self.w, self.h)
        self.ox, self.oy = (self.w / 2, self.h / 2) if origin is None else origin

    def blit(self, dst, x, y, alpha=1.0):
        x0, y0 = int(round(x - self.ox)), int(round(y - self.oy))
        sx0, sy0 = max(0, -x0), max(0, -y0)
        dx0, dy0 = max(0, x0), max(0, y0)
        dx1, dy1 = min(dst.shape[1], x0 + self.w), min(dst.shape[0], y0 + self.h)
        if dx1 <= dx0 or dy1 <= dy0 or alpha <= 0.003:
            return
        a = self.a[sy0:sy0 + dy1 - dy0, sx0:sx0 + dx1 - dx0] * alpha
        p = self.prem[sy0:sy0 + dy1 - dy0, sx0:sx0 + dx1 - dx0] * alpha
        reg = dst[dy0:dy1, dx0:dx1]
        reg *= (1 - a)[..., None]
        reg += p


def beetle_wings(f, x, y, rot, t, s=36, amount=1.0):
    """날아가는 딱정벌레의 속날개(반투명, 빠르게 떤다)."""
    if amount <= 0.01:
        return
    flap = 0.5 + 0.5 * math.sin(t * 70)
    n = int(s * 2.6) + 8
    m = Mask(n, n)
    for sg in (-1, 1):
        a = rot + sg * (1.1 + 0.5 * flap)
        cx, cy = n / 2 + math.sin(a) * s * 0.56, n / 2 - math.cos(a) * s * 0.56
        m.ellipse(cx, cy, s * 0.56, s * 0.2, a - math.pi / 2)
    wa = m.arr(0.5)
    stamp(f, wa * 0.55 * amount, x, y - 2, "#eef7ff")
    stamp(f, np.clip(grow(wa, 1.0) - wa, 0, 1) * 0.35 * amount, x, y - 2, "#8aa8c8")


# ══════════════════════════ 그림자(번짐이 잘리지 않는 판) ══════════════════════════

def draw_shadow(f, x, y, scale, t, alpha=1.0, eyes=1.0, blink_period=2.3):
    """kit.draw_shadow와 같은 그림 — 번짐(blur 14×scale)을 넉넉한 상자에서 굽는다.

    kit 판은 몸 상자 안에서 바로 블러해서, 크게 그리면 보랏빛 번짐이 상자 끝에서 네모나게 잘린다(fin_return 샷1에서 보였다).
    """
    from kit import draw_eyes, shadow_alpha
    w, h = int(230 * scale) + 8, int(190 * scale) + 8
    if w < 12 or h < 12 or alpha <= 0.01:
        return
    a = shadow_alpha(t, w, h) * alpha
    br = 14 * scale
    stamp(f, blur_arr(padded(a, br), br) * 0.55, x, y, PAL["shadow_glow"], "add")
    stamp(f, a, x, y, PAL["shadow"])
    stamp(f, rim(a, 2, 2, 3) * 0.6, x, y, PAL["shadow_glow"], "add")
    if eyes > 0:
        blink = 1.0 if (t % blink_period) > 0.12 else 0.15
        draw_eyes(f, x, y, scale, eyes * alpha, blink)
