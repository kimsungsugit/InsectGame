"""ch10_kiln · ch11_crown · ch12_ledger가 함께 쓰는 도우미(kit.py에 아직 없는 것).

- `Sprite`  — 그림책 칠(윤곽·단색·테·그늘)을 RGBA 한 장으로 구워 두고 매 프레임 찍기만 한다(회전·배율·알파).
- `stone`   — 둥근 돌 모양(무너진 갱도·돌무더기). 아이용이라 모서리가 둥글다.
- `puff`    — 둥근 먼지 구름(뭉게구름처럼 동그라미 몇 개를 흐린다).
- `Arm`     — 소매가 긴 손(kit.hand_parts의 소매는 상자 안에 갇혀 2.2배 길이가 한계다).
- `shake`   — 카메라 흔들림(잦아드는 사인).
"""
import math

import numpy as np
from PIL import Image

from kit import INK, Mask, blur_arr, c, grow, hand_parts, rim, stamp, xf


# ══════════════════════════ 구워 둔 그림 ══════════════════════════

class Sprite:
    """draw(dst, cx, cy)가 칠한 것을 (w, h) 상자에 구워 둔다 — 검정·흰 바탕에 두 번 칠해 알파를 되찾는다.

    가산(add) 칠도 그대로 살아난다(두 바탕의 차이에서 빠진다). put()은 premultiplied 합성이다.
    """

    def __init__(self, w, h, draw):
        w, h = int(w), int(h)
        black = np.zeros((h, w, 3), np.float32)
        white = np.ones((h, w, 3), np.float32)
        draw(black, w / 2, h / 2)
        draw(white, w / 2, h / 2)
        a = np.clip(1 - (white - black).mean(axis=2), 0, 1)
        self.rgb = black          # premultiplied
        self.a = a
        self.w, self.h = w, h

    def put(self, dst, x, y, alpha=1.0, rot=0.0, scale=1.0, dim=None):
        """(x, y)에 가운데를 맞춰 찍는다. rot은 라디안(시계 반대), dim=(색, 양)이면 그 색으로 물들인다."""
        rgb, a = self.rgb, self.a
        if abs(rot) > 1e-3 or abs(scale - 1) > 1e-3:
            rgb, a = _transform(rgb, a, rot, scale)
        if alpha < 0.999:
            rgb, a = rgb * alpha, a * alpha
        if dim is not None:
            col, k = dim
            rgb = rgb * (1 - k) + c(col) * a[..., None] * k
        _composite(dst, rgb, a, x, y)


def _transform(rgb, a, rot, scale):
    h, w = a.shape
    nw, nh = max(2, int(w * scale)), max(2, int(h * scale))
    out_rgb = []
    for ch in range(3):
        im = Image.fromarray(rgb[..., ch].astype(np.float32), "F")
        if scale != 1:
            im = im.resize((nw, nh), Image.Resampling.BILINEAR)
        if rot:
            im = im.rotate(math.degrees(rot), Image.Resampling.BILINEAR, expand=True)
        out_rgb.append(np.asarray(im, np.float32))
    im = Image.fromarray(a.astype(np.float32), "F")
    if scale != 1:
        im = im.resize((nw, nh), Image.Resampling.BILINEAR)
    if rot:
        im = im.rotate(math.degrees(rot), Image.Resampling.BILINEAR, expand=True)
    return np.stack(out_rgb, axis=2), np.asarray(im, np.float32)


def _composite(dst, rgb, a, x, y):
    h, w = a.shape
    x0, y0 = int(round(x - w / 2)), int(round(y - h / 2))
    sx0, sy0 = max(0, -x0), max(0, -y0)
    x0c, y0c = max(0, x0), max(0, y0)
    x1c, y1c = min(dst.shape[1], x0 + w), min(dst.shape[0], y0 + h)
    if x1c <= x0c or y1c <= y0c:
        return
    sa = a[sy0:sy0 + (y1c - y0c), sx0:sx0 + (x1c - x0c)]
    sr = rgb[sy0:sy0 + (y1c - y0c), sx0:sx0 + (x1c - x0c)]
    reg = dst[y0c:y1c, x0c:x1c]
    reg *= (1 - sa[..., None])
    reg += sr


# ══════════════════════════ 돌·먼지 ══════════════════════════

def stone_pts(cx, cy, r, seed, squash=0.78):
    """둥글둥글한 돌 — 반지름이 조금씩 다른 다각형(아래가 살짝 납작)."""
    g = np.random.default_rng(seed)
    n = 14
    ph = g.uniform(0, 6.28, 2)
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        k = 1 + 0.12 * math.sin(a * 2 + ph[0]) + 0.07 * math.sin(a * 3 + ph[1])
        pts.append((cx + math.cos(a) * r * k, cy + math.sin(a) * r * k * squash))
    return pts


def stone_sprite(r, seed, fill="#9a8a7c", hi=0.2, lo=0.18, ink=INK, crack=True):
    """돌 하나를 구운 Sprite."""
    from kit import paint
    box = int(r * 2.6) + 12

    def draw(dst, cx, cy):
        m = Mask(box, box)
        m.poly(stone_pts(box / 2, box / 2, r, seed))
        a = m.arr(0.5)
        paint(dst, a, fill, ink=ink, line=1.6, hi=hi, lo=lo)
        if crack and r > 14:
            g = np.random.default_rng(seed + 5)
            lm = Mask(box, box)
            x0 = box / 2 + g.uniform(-0.3, 0.1) * r
            lm.line([(x0, box / 2 - r * 0.3), (x0 + r * 0.2, box / 2), (x0 + r * 0.1, box / 2 + r * 0.25)], 1.6)
            stamp(dst, lm.arr(0.4) * a * 0.45, cx, cy, ink)

    return Sprite(box, box, draw)


_PUFF = {}


def puff_alpha(seed, size=160):
    """먼지 구름 알파(정규 크기) — 동그라미 일곱 개를 흐린다."""
    key = (seed, size)
    if key not in _PUFF:
        g = np.random.default_rng(seed)
        m = Mask(size, size, 1)
        for k in range(7):
            a = 2 * math.pi * k / 7 + g.uniform(-0.3, 0.3)
            rr = size * g.uniform(0.15, 0.22)
            m.circle(size / 2 + math.cos(a) * size * 0.18, size / 2 + math.sin(a) * size * 0.12, rr)
        m.circle(size / 2, size / 2, size * 0.24)
        _PUFF[key] = m.arr(size * 0.04)
    return _PUFF[key]


def puff(dst, x, y, r, alpha, color="#efe2cf", seed=0, ink=0.0):
    """(x, y)에 반지름 r쯤의 먼지 구름. ink>0이면 아주 옅은 윤곽(그림책 느낌)."""
    if alpha <= 0.01 or r < 2:
        return
    base = puff_alpha(seed)
    s = r * 2 / base.shape[0]
    n = max(4, int(base.shape[0] * s))
    a = np.asarray(Image.fromarray((base * 255).astype(np.uint8), "L").resize((n, n), Image.Resampling.BILINEAR),
                   np.float32) / 255.0
    if ink > 0:
        stamp(dst, np.clip(grow(a, 1.2) - a, 0, 1) * ink * alpha, x, y, "#8a7460")
    stamp(dst, a * alpha, x, y, color)
    stamp(dst, rim(a, 2, 2, 3) * 0.25 * alpha, x, y, "#ffffff", "add")


# ══════════════════════════ 소매가 긴 손 ══════════════════════════

class Arm:
    """kit.hand_parts의 손 + 길게 이어지는 소매. 손목이 기준점이고 rot은 kit와 같다(손가락 = -y를 rot만큼 돌린 쪽).

    손은 rot=0(손가락 위)으로 굽고 통째로 돌린다 — kit.hand_parts는 rot이 ±90°를 넘으면 소매 끝 사각형의
    x0>x1이 되어 PIL이 ValueError를 낸다. curl 단계별로 윤곽·테까지 미리 구워 둔다(매 프레임 마스크 없음).
    """

    def __init__(self, s, rot, length, skin="#f6c9a0", sleeve="#3fae6a", pose="open", curls=(0.0,), cuff=None,
                 stripe=None, widen=1.0):
        self.s, self.rot = s, rot
        self.skin, self.sleeve = skin, sleeve
        self.cuff, self.stripe = cuff, stripe
        ln = length * s
        hb = int(s * 5.6) + 16           # kit 손 상자
        half = int(max(hb / 2, ln + s)) + 8
        box = half * 2
        self.curls = list(curls)
        self.parts = []
        for cu in self.curls:
            hp = hand_parts(s, 0.0, pose, cu)
            skin_a = np.zeros((box, box), np.float32)
            sleeve_a = np.zeros((box, box), np.float32)
            o = half - hb // 2
            skin_a[o:o + hp["skin"].shape[0], o:o + hp["skin"].shape[1]] = hp["skin"]
            sleeve_a[o:o + hp["sleeve"].shape[0], o:o + hp["sleeve"].shape[1]] = hp["sleeve"]
            hc = o + hb / 2            # kit 손 상자의 가운데 = 손목
            tube = Mask(box, box)
            w0, w1 = s * 0.54, s * 0.6 * widen   # widen>1이면 어깨 쪽으로 넓어지는 외투 소매
            tube.poly([(hc - w0, hc + s * 0.4), (hc - w1, hc + ln), (hc + w1, hc + ln), (hc + w0, hc + s * 0.4)])
            sleeve_a = np.maximum(sleeve_a, tube.arr(0.5))
            extra = None
            if stripe is not None or cuff is not None:
                em = Mask(box, box)
                em.rect(hc - s * 0.7, hc + s * 0.05, hc + s * 0.7, hc + s * 0.42)
                extra = em.arr(0.5) * sleeve_a
            arrs = [skin_a, sleeve_a] + ([extra] if extra is not None else [])
            if abs(rot) > 1e-4:   # 손목(hc, hc) 둘레로 돌린다 — kit 회전은 화면에서 시계 방향
                arrs = [_rot_about(a, hc, hc, rot) for a in arrs]
            skin_a, sleeve_a = arrs[0], arrs[1]
            extra = arrs[2] if extra is not None else None
            union = np.maximum(skin_a, sleeve_a)
            ink = grow(union, 1.6)
            ys, xs = np.nonzero(ink > 0.004)
            y0, y1, x0, x1 = max(0, ys.min() - 4), ys.max() + 5, max(0, xs.min() - 4), xs.max() + 5

            def cut(a):
                return None if a is None else np.ascontiguousarray(a[y0:y1, x0:x1])

            self.parts.append(dict(skin=cut(skin_a), sleeve=cut(sleeve_a), extra=cut(extra), ink=cut(ink),
                                   hi=cut(rim(union, 3, 3, 3) * 0.16), wx=hc - x0, wy=hc - y0))

    def draw(self, f, x, y, curl=0.0, alpha=1.0, tint=None):
        """손목을 (x, y)에. curl은 구워 둔 단계 중 가장 가까운 것."""
        k = int(np.argmin([abs(cu - curl) for cu in self.curls]))
        p = self.parts[k]
        h, w = p["skin"].shape
        cx, cy = x - p["wx"] + w / 2, y - p["wy"] + h / 2

        def tc(v):
            cc = c(v)
            return cc * (1 - tint[1]) + c(tint[0]) * tint[1] if tint else cc

        stamp(f, p["ink"] * alpha, cx, cy, tc(INK))
        stamp(f, p["sleeve"] * alpha, cx, cy, tc(self.sleeve))
        if p["extra"] is not None:
            stamp(f, p["extra"] * alpha, cx, cy, tc(self.cuff or self.stripe))
        stamp(f, p["skin"] * alpha, cx, cy, tc(self.skin))
        stamp(f, p["hi"] * alpha, cx, cy, "#ffffff", "add")


def _rot_about(a, cx, cy, rot):
    """알파 a를 (cx, cy) 둘레로 rot만큼(kit 방향 — 화면에서 시계 방향) 돌린다."""
    im = Image.fromarray(a.astype(np.float32), "F")
    im = im.rotate(-math.degrees(rot), Image.Resampling.BILINEAR, center=(cx, cy))
    return np.asarray(im, np.float32)


# ══════════════════════════ 카메라 ══════════════════════════

def shake(u, at, dur=0.8, amp=8.0, freq=31.0):
    """at에 터져 dur 동안 잦아드는 흔들림 (dx, dy)."""
    if u < at or u > at + dur:
        return 0.0, 0.0
    k = 1 - (u - at) / dur
    k = k * k
    return (math.sin(u * freq) * amp * k, math.cos(u * freq * 1.3 + 1.0) * amp * 0.7 * k)


def offset(f, dx, dy):
    """프레임을 정수 픽셀만큼 민다(가장자리는 끝값으로 — 흔들림용)."""
    ix, iy = int(round(dx)), int(round(dy))
    if ix == 0 and iy == 0:
        return f
    out = np.empty_like(f)
    h, w = f.shape[:2]
    ys = np.clip(np.arange(h) - iy, 0, h - 1)
    xs = np.clip(np.arange(w) - ix, 0, w - 1)
    out[:] = f[ys][:, xs]
    return out


def vgrad(w, h, stops):
    """세로 그라데이션(질감 없음) — stops = [(0..1, '#hex')]."""
    y = np.linspace(0, 1, h, dtype=np.float32)
    xs = [p for p, _ in stops]
    cols = np.stack([c(v) for _, v in stops])
    out = np.stack([np.interp(y, xs, cols[:, k]) for k in range(3)], axis=1)
    return np.repeat(out[:, None, :], w, axis=1).astype(np.float32)
