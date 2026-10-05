"""ch1~ch4 그림책 영상이 함께 쓰는 도우미 — kit.py에 아직 없는 부품(kit를 고치지 않으려고 여기에 둔다).

핵심은 `Sprite`다. kit의 칠하기(over·add·paint·stamp)는 바탕 dst에 대해 늘 1차식(dst·k + b)이다. 그래서 같은 그리기를
**검은 바탕과 흰 바탕에 한 번씩** 그리면 (k, b)가 나오고, 그걸로 그 그림을 어디에든·몇 배로든·반투명하게 다시 얹을 수 있다.
사람·손·그물처럼 굽는 데 수백 ms 드는 것을 샷마다 한 번만 굽고, 프레임마다는 찍기만 한다.
"""
import math

import numpy as np
from PIL import Image

from kit import H, INK, PAL, W, Mask, blur_arr, c, crop, dot, ground_grad, over, stamp
from silhouette_kit import grass as sk_grass
from silhouette_kit import ridge, shift


# ══════════════════════════ 굽고 찍기 ══════════════════════════

def _xform(a, nw, nh, data):
    """(h,w[,c]) float 배열을 PIL 아핀으로 (nh,nw)에 다시 뜬다(바깥은 0)."""
    if a.ndim == 2:
        im = Image.fromarray(np.ascontiguousarray(a), "F")
        return np.asarray(im.transform((nw, nh), Image.Transform.AFFINE, data, Image.Resampling.BILINEAR),
                          np.float32)
    out = np.empty((nh, nw, a.shape[2]), np.float32)
    for i in range(a.shape[2]):
        im = Image.fromarray(np.ascontiguousarray(a[..., i]), "F")
        out[..., i] = np.asarray(im.transform((nw, nh), Image.Transform.AFFINE, data, Image.Resampling.BILINEAR),
                                 np.float32)
    return out


def _composite(dst, op, b, x0, y0, alpha=1.0):
    h, w = op.shape[:2]
    sx0, sy0 = max(0, -x0), max(0, -y0)
    x0c, y0c = max(0, x0), max(0, y0)
    x1c, y1c = min(dst.shape[1], x0 + w), min(dst.shape[0], y0 + h)
    if x1c <= x0c or y1c <= y0c:
        return
    o = op[sy0:sy0 + (y1c - y0c), sx0:sx0 + (x1c - x0c)]
    bb = b[sy0:sy0 + (y1c - y0c), sx0:sx0 + (x1c - x0c)]
    if o.ndim == 2:
        o = o[..., None]
    reg = dst[y0c:y1c, x0c:x1c]
    if alpha >= 0.999:
        reg *= 1 - o
        reg += bb
    else:
        reg *= 1 - o * alpha
        reg += bb * alpha


class Sprite:
    """draw(dst)로 그리는 그림을 (불투명도 op, 덧칠 b)로 굽는다. origin은 찍는 기준점(상자 안 좌표, 기본 가운데)."""

    def __init__(self, draw, w, h, origin=None):
        w, h = int(w), int(h)
        blk = np.zeros((h, w, 3), np.float32)
        wht = np.ones((h, w, 3), np.float32)
        draw(blk)
        draw(wht)
        op = np.clip(1 - (wht - blk), 0, 1)
        if np.abs(op[..., 0] - op[..., 1]).max() < 2e-3 and np.abs(op[..., 0] - op[..., 2]).max() < 2e-3:
            op = op[..., 0].copy()
        self.op, self.b = op, blk
        self.w, self.h = w, h
        self.ox, self.oy = (w / 2, h / 2) if origin is None else origin

    def blit(self, dst, x, y, scale=1.0, alpha=1.0, sub=True):
        """기준점을 (x,y)에 두고 찍는다. sub=False면 정수 자리(빠름)."""
        if alpha <= 0.003:
            return
        left, top = x - self.ox * scale, y - self.oy * scale
        if abs(scale - 1) < 1e-4 and (not sub or (abs(left - round(left)) < 0.02 and abs(top - round(top)) < 0.02)):
            _composite(dst, self.op, self.b, int(round(left)), int(round(top)), alpha)
            return
        x0, y0 = math.floor(left), math.floor(top)
        nw = int(math.ceil(self.w * scale + (left - x0))) + 1
        nh = int(math.ceil(self.h * scale + (top - y0))) + 1
        # 화면 밖은 뜨지 않는다
        cx0, cy0 = max(x0, 0), max(y0, 0)
        cx1, cy1 = min(x0 + nw, dst.shape[1]), min(y0 + nh, dst.shape[0])
        if cx1 <= cx0 or cy1 <= cy0:
            return
        inv = 1.0 / scale
        data = (inv, 0, (cx0 - left) * inv, 0, inv, (cy0 - top) * inv)
        op = _xform(self.op, cx1 - cx0, cy1 - cy0, data)
        b = _xform(self.b, cx1 - cx0, cy1 - cy0, data)
        _composite(dst, op, b, cx0, cy0, alpha)

    def pan(self, dst, ox, oy=0.0):
        """화면보다 넓은 층을 (ox, oy)에서 잘라 화면 전체에 얹는다(부분 픽셀)."""
        hh, ww = dst.shape[:2]
        op = crop(self.op, ox, oy, ww, hh)
        b = crop(self.b, ox, oy, ww, hh)
        if op.ndim == 2:
            op = op[..., None]
        dst *= 1 - op
        dst += b


def canvas(w=W, h=H, color=None):
    f = np.zeros((h, w, 3), np.float32)
    if color is not None:
        f[:] = c(color)
    return f


def blur_rgb(img, r):
    """색 그림 전체를 흐린다(초점 밖 배경)."""
    out = np.empty_like(img)
    for i in range(3):
        out[..., i] = blur_arr(np.clip(img[..., i], 0, 1), r)
    return out


# ══════════════════════════ 땅·물 ══════════════════════════

def meadow_w(img, y, seed, flowers=60, top=None, bottom=None, blades=260):
    """kit.meadow와 같은 풀밭을 화면보다 넓은 그림에도 깐다. 땅 알파를 돌려준다."""
    hh, ww = img.shape[:2]
    gm = ridge(Mask(ww, hh), seed, y, 10, w=ww, freq=(0.003, 0.008)).arr(1.0)
    over(img, c(INK), np.clip(gm - shift(gm, 0, 2.5), 0, 1) * 0.7)
    ground_grad(img, gm, top or PAL["meadow"], bottom or PAL["meadow2"])
    gl = sk_grass(Mask(ww, hh), seed + 3, y + 6, hh + 20, int(blades * ww / W), height=(14, 46),
                  width=(1.6, 3.6), x1=ww).arr(0.5)
    over(img, c(PAL["grass"]), gl * gm)
    if flowers:
        r = np.random.default_rng(seed + 9)
        for _ in range(int(flowers * ww / W)):
            fx, fy = r.uniform(0, ww), r.uniform(y + 20, hh)
            flower(img, fx, fy, 0.6 + 0.8 * (fy - y) / max(1, (hh - y)), PAL["flower"][r.integers(0, 3)])
    return gm


def flower(img, x, y, sc, color):
    fm = Mask(40, 40)
    for k in range(5):
        a = 2 * math.pi * k / 5
        fm.circle(20 + math.cos(a) * 6 * sc, 20 + math.sin(a) * 6 * sc, 4.6 * sc)
    stamp(img, fm.arr(0.4), x, y, color)
    stamp(img, Mask(12, 12).circle(6, 6, 2.4 * sc).arr(0.3), x, y, "#ffcc33")


def water_static(dst, y0, y1, top, bottom, seed=0, lines=18, line_alpha=0.4):
    """물 — kit.water의 정적판(반짝임 없이). 물결선은 프레임마다 `water_glints`로 따로 얹는다."""
    hh, ww = dst.shape[:2]
    m = Mask(ww, hh)
    m.rect(0, y0, ww, y1)
    a = m.arr(0.6)
    ground_grad(dst, a, top, bottom)
    r = np.random.default_rng(seed)
    lm = Mask(ww, hh)
    for _ in range(int(lines * ww / W)):
        yy = r.uniform(y0 + 10, y1 - 4)
        xx = r.uniform(0, ww)
        ln = r.uniform(40, 120) * (0.5 + (yy - y0) / max(1, y1 - y0))
        lm.line([(xx - ln / 2, yy), (xx + ln / 2, yy)], 2.2)
    from kit import add
    add(dst, c("#ffffff"), lm.arr(0.6) * a * line_alpha)
    return a


class Glints:
    """물 위 반짝임·짧은 물결선 — 위치만 바뀌는 작은 도장들(프레임마다 싸다)."""

    def __init__(self, n, seed, box, length=(30, 90), width=2.0):
        r = np.random.default_rng(seed)
        self.items = []
        x0, y0, x1, y1 = box
        for _ in range(n):
            ln = r.uniform(*length) * (0.5 + (r.uniform(y0, y1) - y0) / max(1, y1 - y0))
            m = Mask(int(ln) + 12, 12).line([(6, 6), (6 + ln, 6)], width).arr(0.6)
            self.items.append((r.uniform(x0, x1), r.uniform(y0, y1), r.uniform(0, 6.28), r.uniform(0.6, 1.4), m))

    def draw(self, f, t, amount=0.45, drift=18.0, color="#ffffff"):
        for x, y, ph, sp, m in self.items:
            a = 0.5 + 0.5 * math.sin(t * 1.7 * sp + ph)
            stamp(f, m * a * amount, x + math.sin(t * 0.9 * sp + ph) * drift, y, color, "add")


def ripple_ring(f, x, y, r, amount, color="#ffffff", w=2.0, flat=0.24):
    """납작한 물결 고리 하나(가산)."""
    if amount <= 0.01 or r < 1:
        return
    bw, bh = int(r * 2 + 12), int(r * flat * 2 + 12)
    m = Mask(bw, bh)
    m.d.ellipse([6 * m.ss, 6 * m.ss, (bw - 6) * m.ss, (bh - 6) * m.ss], outline=255,
                width=max(1, int(w * m.ss)))
    stamp(f, m.arr(0.6) * amount, x, y, color, "add")


def lily_pad(m, x, y, rx, ry, notch_dir=0.0, notch=0.5):
    """연잎 — 물 위에 누운 납작한 원(가로 타원)에서 쐐기 하나가 빠졌다. notch_dir은 쐐기가 향하는 각(잎 평면에서)."""
    pts = []
    for i in range(48):
        a = notch_dir + notch * 0.5 + (2 * math.pi - notch) * i / 47
        pts.append((x + math.cos(a) * rx, y + math.sin(a) * ry))
    pts.append((x, y))
    m.poly(pts)
    return m


# ══════════════════════════ 연출 부품 ══════════════════════════

def puff(f, x, y, k, size=40, color="#ffffff", seed=0, amount=0.8):
    """퐁 — 동그란 연기 몇 덩이가 부풀며 흩어진다. k는 0..1 진행."""
    if k <= 0 or k >= 1:
        return
    r = np.random.default_rng(seed)
    for i in range(7):
        a = r.uniform(0, 2 * math.pi)
        d = size * (0.2 + 0.9 * k) * r.uniform(0.5, 1.0)
        rad = size * (0.25 + 0.45 * k) * r.uniform(0.7, 1.2)
        px, py = x + math.cos(a) * d, y + math.sin(a) * d * 0.8 - size * 0.4 * k
        m = Mask(int(rad * 2 + 10), int(rad * 2 + 10)).circle(rad + 5, rad + 5, rad).arr(rad * 0.25)
        stamp(f, m * amount * (1 - k) ** 1.3 * min(1.0, k * 6), px, py, color)


def tally_strokes(n, x, y, h, step, group_gap, slant=0.18):
    """눈금(||||) n개를 획 목록으로 — 다섯째 획은 앞 넷을 비스듬히 긋는다. [(p0, p1)]."""
    out = []
    gx = x
    for i in range(n):
        j = i % 5
        if j == 4:
            out.append(((gx - step * 4.3, y + h * 0.75), (gx - step * 0.1, y + h * 0.2)))
            gx += group_gap
        else:
            xx = gx + 0
            out.append(((xx + h * slant * 0.3, y), (xx - h * slant * 0.3, y + h)))
            gx += step
    return out


def gold_outline(f, a, color="#ffd34d", width=2.4, glow=0.6, amount=1.0):
    """알파 a의 바깥 테를 금빛으로(가산)."""
    edge = np.clip(np.clip(blur_arr(a, width) * 2.4, 0, 1) - a * 0.85, 0, 1)
    from kit import add
    add(f, c(color), edge * amount)
    if glow > 0:
        add(f, c(color), blur_arr(a, width * 6) * glow * amount)


def frog(sc, at, amp=0.12, f=150.0, pan=0.0, croaks=2):
    """개구리 — 낮은 음이 빠르게 떨리는 두어 마디(sound.Score의 바탕 믹서를 쓴다)."""
    from sound import SR
    for k in range(croaks):
        i0, m = sc._seg(at + k * 0.22, 0.16)
        if m <= 0:
            continue
        tt = np.arange(m) / SR
        car = np.sin(2 * np.pi * f * tt) + 0.5 * np.sin(2 * np.pi * f * 2.02 * tt) + 0.25 * np.sin(2 * np.pi * f * 3.1 * tt)
        am = 0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 34 * tt))
        env = np.sin(np.pi * tt / 0.16) ** 0.7
        sc._mix(i0, car * am * env * amp, pan)


def flutter(sc, at, dur=0.5, amp=0.08, rate=26.0, lo=200, hi=1600, pan=0.0):
    """파닥임 — 대역 잡음을 날갯짓 빈도로 끊는다(상자 안 곤충·잠자리 날개)."""
    from sound import SR
    i0, m = sc._seg(at, dur)
    if m <= 0:
        return
    tt = np.arange(m) / SR
    y = sc._band(m, lo, hi) * (0.5 + 0.5 * np.sin(2 * np.pi * rate * tt)) ** 2
    y *= np.sin(np.pi * tt / dur) ** 0.6 * amp
    sc._mix(i0, y, pan)


def clink(sc, at, amp=0.08, pan=0.0):
    """빈 깡통이 달각 — 짧은 금속음 두 개."""
    sc.pluck(at, 1180.0, amp, 0.35, pan, kind="xylo")
    sc.pluck(at + 0.07, 1530.0, amp * 0.7, 0.3, pan, kind="xylo")
    sc.noise(at, 0.05, 2500, 7000, amp * 0.6, decay=60, pan=pan)


def dots_twinkle(f, pts, t, color, size=2.2, amount=0.7):
    for i, (x, y) in enumerate(pts):
        tw = 0.5 + 0.5 * math.sin(t * 2.6 + i * 1.9)
        dot(f, x, y, size, c(color), amount * tw)


def rotate_alpha(a, rot, cx=None, cy=None):
    """알파를 (cx,cy) 둘레로 rot(라디안, 화면에서 시계 방향 +)만큼 돌린다."""
    h, w = a.shape
    cx = w / 2 if cx is None else cx
    cy = h / 2 if cy is None else cy
    im = Image.fromarray(np.ascontiguousarray(a, np.float32), "F")
    im = im.rotate(-math.degrees(rot), resample=Image.Resampling.BILINEAR, center=(cx, cy))
    return np.clip(np.asarray(im, np.float32), 0, 1)


def hand_parts_rot(s, rot=0.0, pose="open", curl=0.0, sleeve_len=2.2):
    """kit.hand_parts의 우회판 — kit은 rot < 약 -0.3에서 소매 끝 사각형의 모서리 순서가 뒤집혀 PIL이 멈춘다.
    rot=0으로 굽고 알파를 돌린다(손목이 상자 가운데라 그대로 맞는다)."""
    from kit import hand_parts
    p = hand_parts(s, 0.0, pose, curl, sleeve_len)
    if abs(rot) > 1e-4:
        p = dict(p, skin=rotate_alpha(p["skin"], rot), sleeve=rotate_alpha(p["sleeve"], rot))
    return p


def ribbon(m, pts, w0, w1=0.0):
    """매끈한 띠(풀잎·줄기) — 점 목록을 따라 폭이 w0→w1로 줄어드는 다각형(taper보다 가장자리가 고르다)."""
    n = len(pts)
    left, right = [], []
    for i, (x, y) in enumerate(pts):
        xa, ya = pts[max(0, i - 1)]
        xb, yb = pts[min(n - 1, i + 1)]
        dx, dy = xb - xa, yb - ya
        ln = math.hypot(dx, dy) or 1.0
        nx, ny = -dy / ln, dx / ln
        w = (w0 + (w1 - w0) * i / max(1, n - 1)) / 2
        left.append((x + nx * w, y + ny * w))
        right.append((x - nx * w, y - ny * w))
    m.poly(left + right[::-1])
    return m
