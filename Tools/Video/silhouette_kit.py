"""실루엣 삽화 영상 도구 — 스토리 영상 11편(story_silhouettes.py)이 쓰는 그리기·합성 부품.

화풍: 겹겹의 실루엣(먼 것일수록 옅고 푸르다) + 그라데이션 하늘 + 빛 번짐·빛살 + 안개 + 떠다니는 입자.
인물은 뒷모습·옆모습 실루엣뿐이라 얼굴이 없다(Docs/StoryVideos.md §2 — 인게임 모델과 어긋날 일이 없다).
글씨는 판독 불가 긁적임(`scribble`)으로만 — 영상에 글자를 굽지 않는다(자막은 게임이 IMGUI로 얹는다).

numpy + PIL만 쓴다. 정적 마스크는 2배로 그려 줄인다(가장자리 계단 방지).
좌표는 전부 출력 픽셀(1280×720) 기준이고, 레이어는 여백(margin)을 두어 카메라 이동을 받는다.
"""
import math

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

W, H, FPS = 1280, 720, 24
SS = 2


def col(h, mul=1.0):
    """'#rrggbb' → float rgb."""
    return np.array([int(h[i:i + 2], 16) for i in (1, 3, 5)], dtype=np.float32) / 255.0 * mul


def lerp(a, b, t):
    return a + (b - a) * t


def smooth(t):
    t = min(1.0, max(0.0, t))
    return t * t * (3 - 2 * t)


def ease_out(t):
    t = min(1.0, max(0.0, t))
    return 1 - (1 - t) ** 3


def ease_in_out(t):
    t = min(1.0, max(0.0, t))
    return 0.5 - 0.5 * math.cos(math.pi * t)


def span(t, a, b):
    """t가 [a,b]에서 0→1."""
    if b <= a:
        return 1.0 if t >= b else 0.0
    return min(1.0, max(0.0, (t - a) / (b - a)))


# ────────────────────────── 마스크 ──────────────────────────

class Mask:
    """출력 좌표로 그리고 2배 해상도로 굽는 알파 마스크."""

    def __init__(self, w=W, h=H, ss=SS):
        self.w, self.h, self.ss = w, h, ss
        self.img = Image.new("L", (w * ss, h * ss), 0)
        self.d = ImageDraw.Draw(self.img)

    def _p(self, pts):
        s = self.ss
        return [(float(x) * s, float(y) * s) for x, y in pts]

    def poly(self, pts, v=255):
        if len(pts) >= 3:
            self.d.polygon(self._p(pts), fill=v)
        return self

    def circle(self, cx, cy, r, v=255):
        s = self.ss
        self.d.ellipse([(cx - r) * s, (cy - r) * s, (cx + r) * s, (cy + r) * s], fill=v)
        return self

    def ellipse(self, cx, cy, rx, ry, rot=0.0, v=255, n=64):
        cr, sr = math.cos(rot), math.sin(rot)
        pts = []
        for i in range(n):
            a = 2 * math.pi * i / n
            x, y = rx * math.cos(a), ry * math.sin(a)
            pts.append((cx + x * cr - y * sr, cy + x * sr + y * cr))
        return self.poly(pts, v)

    def rect(self, x0, y0, x1, y1, v=255, r=0):
        s = self.ss
        if r > 0:
            self.d.rounded_rectangle([x0 * s, y0 * s, x1 * s, y1 * s], radius=r * s, fill=v)
        else:
            self.d.rectangle([x0 * s, y0 * s, x1 * s, y1 * s], fill=v)
        return self

    def line(self, pts, w, v=255, caps=True):
        if len(pts) < 2:
            return self
        self.d.line(self._p(pts), fill=v, width=max(1, int(round(w * self.ss))), joint="curve")
        if caps and w >= 2:
            for x, y in (pts[0], pts[-1]):
                self.circle(x, y, w / 2, v)
        return self

    def taper(self, pts, w0, w1, v=255):
        n = len(pts) - 1
        for i in range(n):
            w = lerp(w0, w1, i / max(1, n - 1))
            self.line([pts[i], pts[i + 1]], max(1.0, w), v)
        return self

    def arr(self, blur=0.0):
        img = self.img.resize((self.w, self.h), Image.Resampling.LANCZOS)
        if blur > 0:
            img = img.filter(ImageFilter.GaussianBlur(blur))
        return np.asarray(img, dtype=np.float32) / 255.0


def bezier(p0, p1, p2, p3, n=24):
    out = []
    for i in range(n + 1):
        t = i / n
        u = 1 - t
        out.append((u ** 3 * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t ** 3 * p3[0],
                    u ** 3 * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t ** 3 * p3[1]))
    return out


def xf(pts, cx, cy, s=1.0, rot=0.0):
    """점 목록을 (0,0) 기준 회전·배율 후 (cx,cy)로 옮긴다."""
    cr, sr = math.cos(rot), math.sin(rot)
    return [(cx + (x * cr - y * sr) * s, cy + (x * sr + y * cr) * s) for x, y in pts]


def blur_arr(a, r):
    if r <= 0:
        return a
    img = Image.fromarray(np.clip(a * 255 + 0.5, 0, 255).astype(np.uint8), "L")
    return np.asarray(img.filter(ImageFilter.GaussianBlur(r)), dtype=np.float32) / 255.0


# ────────────────────────── 질감 ──────────────────────────

def fbm(w, h, seed, scales=(160, 60, 22), weights=(0.6, 0.3, 0.1)):
    """부드러운 값 잡음(0..1)."""
    r = np.random.default_rng(seed)
    acc = np.zeros((h, w), np.float32)
    for sc, wt in zip(scales, weights):
        gw, gh = max(2, w // sc + 2), max(2, h // sc + 2)
        small = (r.random((gh, gw)) * 255).astype(np.uint8)
        img = Image.fromarray(small, "L").resize((w, h), Image.Resampling.BICUBIC)
        acc += np.asarray(img, np.float32) / 255.0 * wt
    acc /= sum(weights)
    return acc


def sky(w, h, stops, seed=0, texture=0.035):
    """세로 그라데이션 하늘. stops = [(pos 0..1, '#hex'), ...]."""
    y = np.linspace(0, 1, h, dtype=np.float32)
    out = np.zeros((h, 3), np.float32)
    for i in range(len(stops) - 1):
        p0, c0 = stops[i]
        p1, c1 = stops[i + 1]
        m = (y >= p0) & (y <= p1)
        t = ((y[m] - p0) / max(1e-6, p1 - p0))[:, None]
        t = t * t * (3 - 2 * t)
        out[m] = col(c0) * (1 - t) + col(c1) * t
    img = np.repeat(out[:, None, :], w, axis=1)
    if texture > 0:
        n = fbm(w, h, seed + 101, (220, 80), (0.7, 0.3))
        img = img * (1 + (n[..., None] - 0.5) * texture * 2)
    return np.clip(img, 0, 1)


def radial(w, h, cx, cy, r, power=2.0):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / max(1.0, r)
    return np.clip(1 - d, 0, 1) ** power


def rays_texture(w, h, cx, cy, count=14, seed=0, spread=math.pi, center=-math.pi / 2, reach=1.0):
    """광원에서 퍼지는 빛살 알파(0..1)."""
    rng = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    ang = np.arctan2(yy - cy, xx - cx)
    dist = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    acc = np.zeros((h, w), np.float32)
    for _ in range(count):
        a0 = center + (rng.random() - 0.5) * spread
        width = 0.02 + rng.random() * 0.05
        d = np.angle(np.exp(1j * (ang - a0)))
        acc += np.exp(-(d / width) ** 2) * (0.4 + rng.random() * 0.6)
    fall = np.clip(1 - dist / (max(w, h) * reach), 0, 1) ** 1.4
    return np.clip(acc * fall, 0, 1)


def vignette(w=W, h=H, strength=0.42):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.sqrt(((xx - w / 2) / (w / 2)) ** 2 + ((yy - h / 2) / (h / 2)) ** 2) / math.sqrt(2)
    return (1 - strength * np.clip(d, 0, 1) ** 1.8)[..., None]


# ────────────────────────── 합성 ──────────────────────────

def over(dst, color, alpha):
    """dst(HxWx3)에 단색 또는 색 배열을 alpha로 덮는다(제자리)."""
    a = alpha[..., None] if alpha.ndim == 2 else alpha
    dst *= (1 - a)
    dst += np.asarray(color, np.float32) * a
    return dst


def add(dst, color, amount):
    a = amount[..., None] if amount.ndim == 2 else amount
    dst += np.asarray(color, np.float32) * a
    return dst


def shift(a, dx, dy):
    """부분 픽셀 이동 — 두 정수 이동을 섞는다(느린 팬의 계단 방지). 가장자리는 끝값으로 채운다."""
    ix, iy = math.floor(dx), math.floor(dy)
    fx, fy = dx - ix, dy - iy

    def roll(arr, x, y):
        return np.roll(np.roll(arr, y, axis=0), x, axis=1)

    a00 = roll(a, ix, iy)
    if fx == 0 and fy == 0:
        return a00
    a10 = roll(a, ix + 1, iy)
    a01 = roll(a, ix, iy + 1)
    a11 = roll(a, ix + 1, iy + 1)
    return (a00 * (1 - fx) * (1 - fy) + a10 * fx * (1 - fy) + a01 * (1 - fx) * fy + a11 * fx * fy)


def crop(a, ox, oy, w=W, h=H):
    """큰 레이어에서 (ox, oy) 부분 픽셀 창을 잘라낸다. 여백이 없는 축은 정수로 떨어진다."""
    maxx, maxy = a.shape[1] - w, a.shape[0] - h
    ox = min(max(ox, 0.0), float(maxx))
    oy = min(max(oy, 0.0), float(maxy))
    ix, iy = int(math.floor(ox)), int(math.floor(oy))
    fx, fy = ox - ix, oy - iy
    if ix >= maxx:
        ix, fx = maxx, 0.0
    if iy >= maxy:
        iy, fy = maxy, 0.0
    a00 = a[iy:iy + h, ix:ix + w]
    if fx == 0 and fy == 0:
        return a00
    if fy == 0:
        return a00 * (1 - fx) + a[iy:iy + h, ix + 1:ix + 1 + w] * fx
    if fx == 0:
        return a00 * (1 - fy) + a[iy + 1:iy + 1 + h, ix:ix + w] * fy
    a10 = a[iy:iy + h, ix + 1:ix + 1 + w]
    a01 = a[iy + 1:iy + 1 + h, ix:ix + w]
    a11 = a[iy + 1:iy + 1 + h, ix + 1:ix + 1 + w]
    return a00 * (1 - fx) * (1 - fy) + a10 * fx * (1 - fy) + a01 * (1 - fx) * fy + a11 * fx * fy


def zoom(frame, s, cx=W / 2, cy=H / 2):
    """프레임을 (cx,cy) 기준으로 s배 확대(밀어 들어가기)."""
    if abs(s - 1.0) < 1e-4:
        return frame
    img = Image.fromarray(np.clip(frame * 255 + 0.5, 0, 255).astype(np.uint8), "RGB")
    cw, ch = W / s, H / s
    x0, y0 = cx - cw * cx / W, cy - ch * cy / H
    img = img.transform((W, H), Image.Transform.EXTENT, (x0, y0, x0 + cw, y0 + ch), Image.Resampling.BILINEAR)
    return np.asarray(img, np.float32) / 255.0


def rim(mask, dx, dy, width=2.0):
    """역광 테두리 — 광원 쪽 가장자리만 남긴다."""
    moved = shift(mask, dx, dy)
    return blur_arr(np.clip(mask - moved, 0, 1), width * 0.5)


# ────────────────────────── 입자 ──────────────────────────

_SPRITES = {}


def sprite(r):
    r = max(1, int(round(r)))
    if r not in _SPRITES:
        s = r * 2 + 1
        yy, xx = np.mgrid[0:s, 0:s].astype(np.float32)
        d = np.sqrt((xx - r) ** 2 + (yy - r) ** 2) / (r + 0.5)
        _SPRITES[r] = np.clip(1 - d, 0, 1) ** 1.8
    return _SPRITES[r]


def dot(dst, x, y, r, color, a):
    """부드러운 점을 더한다(가산)."""
    sp = sprite(r)
    rr = sp.shape[0] // 2
    x0, y0 = int(round(x)) - rr, int(round(y)) - rr
    x1, y1 = x0 + sp.shape[1], y0 + sp.shape[0]
    sx0, sy0 = max(0, -x0), max(0, -y0)
    x0c, y0c = max(0, x0), max(0, y0)
    x1c, y1c = min(dst.shape[1], x1), min(dst.shape[0], y1)
    if x1c <= x0c or y1c <= y0c:
        return
    patch = sp[sy0:sy0 + (y1c - y0c), sx0:sx0 + (x1c - x0c)]
    dst[y0c:y1c, x0c:x1c] += patch[..., None] * np.asarray(color, np.float32) * a


class Particles:
    """떠다니는 입자 — 먼지·반딧불·불티·눈. 위치는 시간의 함수라 어느 프레임이든 바로 계산된다."""

    def __init__(self, n, seed, box=(0, 0, W, H), vel=((-4, 4), (-12, -4)), size=(1.0, 2.6),
                 wobble=6.0, twinkle=1.0, life=None):
        r = np.random.default_rng(seed)
        self.n = n
        self.box = box
        self.p = np.stack([r.uniform(box[0], box[2], n), r.uniform(box[1], box[3], n)], axis=1)
        self.v = np.stack([r.uniform(vel[0][0], vel[0][1], n), r.uniform(vel[1][0], vel[1][1], n)], axis=1)
        self.s = r.uniform(size[0], size[1], n)
        self.ph = r.uniform(0, 2 * math.pi, n)
        self.wobble = wobble
        self.twinkle = twinkle
        self.life = life

    def draw(self, dst, t, color, alpha=1.0, ox=0.0, oy=0.0):
        x0, y0, x1, y1 = self.box
        bw, bh = x1 - x0, y1 - y0
        for i in range(self.n):
            x = (self.p[i, 0] + self.v[i, 0] * t + math.sin(t * 0.9 + self.ph[i]) * self.wobble - x0) % bw + x0
            y = (self.p[i, 1] + self.v[i, 1] * t - y0) % bh + y0
            tw = 1.0 - self.twinkle * 0.5 * (1 + math.sin(t * 2.3 + self.ph[i] * 3))
            dot(dst, x - ox, y - oy, self.s[i] * 2.2, color, alpha * (0.35 + 0.65 * tw))


# ────────────────────────── 지형·식물 ──────────────────────────

def ridge(m, seed, y, amp, w=None, freq=(0.004, 0.011, 0.03, 0.09), bottom=None, x0=0):
    """들쭉날쭉한 능선 아래를 채운다."""
    w = m.w if w is None else w
    r = np.random.default_rng(seed)
    ph = r.uniform(0, 2 * math.pi, len(freq))
    pts = []
    for x in range(int(x0) - 4, int(x0 + w) + 8, 4):
        v = sum(math.sin(x * f + p) * (amp / (i + 1)) for i, (f, p) in enumerate(zip(freq, ph)))
        pts.append((x, y + v))
    b = bottom if bottom is not None else m.h + 4
    pts += [(x0 + w + 8, b), (x0 - 4, b)]
    m.poly(pts)
    return m


def peaks(m, seed, y, amp, w=None, rough=0.55, levels=8, x0=0.0):
    """들쭉날쭉한 산 능선 — 중점 변위. 사인파 능선은 물결로 읽힌다."""
    w = m.w if w is None else w
    r = np.random.default_rng(seed)
    pts = [0.0, 0.0]
    scale = amp
    for _ in range(levels):
        nxt = []
        for i in range(len(pts) - 1):
            nxt += [pts[i], (pts[i] + pts[i + 1]) / 2 + r.uniform(-1, 1) * scale]
        nxt.append(pts[-1])
        pts = nxt
        scale *= rough
    n = len(pts)
    line = [(x0 - 10 + (w + 20) * i / (n - 1), y - abs(pts[i]) * 1.4 + amp * 0.3) for i in range(n)]
    m.poly(line + [(x0 + w + 10, m.h + 4), (x0 - 10, m.h + 4)])
    return m


def grass(m, seed, y0, y1, count, height=(40, 120), lean=0.25, width=(2.0, 5.0), x0=0, x1=W, sway=0.0):
    r = np.random.default_rng(seed)
    for _ in range(count):
        x = r.uniform(x0, x1)
        base = r.uniform(y0, y1)
        h = r.uniform(*height)
        bend = (r.uniform(-lean, lean) + sway) * h
        m.taper(bezier((x, base), (x, base - h * 0.4), (x + bend * 0.5, base - h * 0.8), (x + bend, base - h), 8),
                r.uniform(*width), 0.8)
    return m


def reeds(m, seed, y0, count, height=(180, 330), x0=0, x1=W, sway=0.0, t=0.0):
    r = np.random.default_rng(seed)
    for i in range(count):
        x = r.uniform(x0, x1)
        h = r.uniform(*height)
        ph = r.uniform(0, 6.28)
        bend = (sway + 0.04 * math.sin(t * 1.3 + ph)) * h
        top = (x + bend, y0 - h)
        m.taper(bezier((x, y0), (x, y0 - h * 0.5), (x + bend * 0.6, y0 - h * 0.85), top, 10), 5.0, 2.0)
        if r.random() < 0.55:   # 부들 이삭
            m.ellipse(top[0] - bend * 0.08, top[1] + 26, 6, 24, rot=math.atan2(bend, h))
    return m


def treeline(m, seed, y, height, w=None, density=0.5, amp=10.0):
    """먼 숲 가장자리 — 작은 둥근 수관이 겹쳐 울퉁불퉁한 띠가 되고 그 아래는 채운다."""
    w = m.w if w is None else w
    r = np.random.default_rng(seed)
    ph = r.uniform(0, 6.28, 2)
    x = -20.0
    while x < w + 20:
        base = y + math.sin(x * 0.006 + ph[0]) * amp + math.sin(x * 0.017 + ph[1]) * amp * 0.4
        hh = height * r.uniform(0.45, 1.0)
        rad = hh * r.uniform(0.35, 0.55)
        m.circle(x, base - hh + rad, rad)
        m.rect(x - rad * 0.9, base - hh + rad, x + rad * 0.9, m.h + 4)
        if r.random() < 0.15:   # 가끔 뾰족한 침엽수
            m.poly([(x - rad * 0.7, base - hh * 0.6), (x, base - hh * 1.5), (x + rad * 0.7, base - hh * 0.6)])
        x += rad * (2.0 - density)
    return m


def tree_round(m, x, y, h, seed, w=None):
    r = np.random.default_rng(seed)
    w = w or h * 0.7
    m.taper([(x, y), (x + r.uniform(-8, 8), y - h * 0.45)], h * 0.07, h * 0.04)
    for _ in range(9):
        m.circle(x + r.uniform(-0.38, 0.38) * w, y - h * r.uniform(0.5, 0.95), h * r.uniform(0.16, 0.28))
    return m


def tree_dead(m, x, y, h, seed):
    r = np.random.default_rng(seed)
    m.taper([(x, y), (x + r.uniform(-10, 10), y - h)], h * 0.06, h * 0.02)
    for _ in range(5):
        by = y - h * r.uniform(0.35, 0.9)
        dx = r.choice([-1, 1]) * h * r.uniform(0.15, 0.35)
        m.taper(bezier((x, by), (x + dx * 0.3, by - h * 0.05), (x + dx * 0.7, by - h * 0.14), (x + dx, by - h * 0.2), 6),
                h * 0.02, h * 0.006)
    return m


# ────────────────────────── 인물 ──────────────────────────

def person(m, x, feet, h, kind="coat", t=0.0, face=0.0, sling=False, arm=None, hair=0.0, bob=0.0, walk=None,
           net=False, bag=False):
    """
    뒷모습 실루엣. kind: coat(긴 코트) · robe(세라, 긴 머리) · kid(주인공, 짧은 윗옷) · old(굽은 등).
    face: -1 왼쪽 옆 ~ 0 뒤 ~ 1 오른쪽 옆(어깨 폭이 줄어든다). walk: 걸음 위상(없으면 서 있음).
    net: 어깨에 멘 채집망(주인공 표지). bag: 옆구리 가방(세라).
    곡선 어깨와 굽은 팔꿈치가 있어야 사람으로 읽힌다 — 사다리꼴 몸통에 막대 팔을 붙이면 로봇이 된다.
    """
    y = feet - bob
    hunch = h * 0.06 if kind == "old" else 0.0
    sw = h * (0.25 if kind != "kid" else 0.23) * (1 - 0.3 * abs(face))
    hr = h * 0.068
    hx = x + face * h * 0.025 + hunch * 0.8
    hy = y - h + hr + hunch * 0.6
    m.ellipse(hx, hy, hr * 0.9, hr)
    if kind == "robe" or hair > 0:
        sway = math.sin(t * 2.2) * h * 0.006
        m.poly([(hx - hr * 0.95, hy - hr * 0.2), (hx + hr * 0.95, hy - hr * 0.2),
                (hx + hr * 1.1 + sway, hy + h * (0.2 + hair)), (hx + sway * 2, hy + h * (0.23 + hair)),
                (hx - hr * 1.1 + sway, hy + h * (0.2 + hair))])
    ys = y - h * 0.8 + hunch                       # 어깨 높이
    waist = y - h * 0.5
    neck = [(hx - hr * 0.35, hy + hr * 0.7), (hx + hr * 0.35, hy + hr * 0.7)]
    # 몸통 — 목에서 어깨로 둥글게 떨어지고 허리에서 좁아진다
    left = bezier(neck[0], (x - sw * 0.3 + hunch, ys - h * 0.02), (x - sw * 0.52 + hunch, ys - h * 0.005),
                  (x - sw * 0.5 + hunch, ys + h * 0.05), 10)
    right = bezier(neck[1], (x + sw * 0.3 + hunch, ys - h * 0.02), (x + sw * 0.52 + hunch, ys - h * 0.005),
                   (x + sw * 0.5 + hunch, ys + h * 0.05), 10)
    if kind in ("coat", "robe", "old"):
        hem = y - h * (0.1 if kind == "coat" else (0.06 if kind == "robe" else 0.16))
        flare = h * (0.19 if kind == "coat" else 0.16)
        flut = math.sin(t * 3.1) * h * 0.014
        body = left + [(x - h * 0.12, waist), (x - flare + flut * 0.5, hem), (x - flare * 0.3, hem + h * 0.012),
                       (x + flare * 0.35, hem + h * 0.01 + flut), (x + flare + flut, hem), (x + h * 0.12, waist)] + right[::-1]
    else:
        body = left + [(x - h * 0.11, waist + h * 0.02), (x - h * 0.115, waist + h * 0.1), (x + h * 0.115, waist + h * 0.1),
                       (x + h * 0.11, waist + h * 0.02)] + right[::-1]
    m.poly(body)
    # 다리 — 허벅지·무릎·정강이·발
    step = math.sin(walk) if walk is not None else 0.0
    for sgn in (-1, 1):
        hip = (x + sgn * h * 0.055, waist + h * 0.06)
        swing = step * sgn * h * 0.06 if walk is not None else 0.0
        knee = (x + sgn * h * 0.06 + swing * 0.6, y - h * 0.26)
        ankle = (x + sgn * h * 0.055 + swing, y - h * 0.025)
        m.taper([hip, knee], h * 0.085, h * 0.06)
        m.taper([knee, ankle], h * 0.06, h * 0.04)
        m.ellipse(ankle[0] + sgn * h * 0.005, y - h * 0.012, h * 0.035, h * 0.018)
    # 팔 — 어깨에서 팔꿈치(살짝 바깥)로, 손까지
    for sgn in (-1, 1):
        sh = (x + sgn * (sw * 0.47) + hunch, ys + h * 0.04)
        if arm is not None and sgn == 1:
            ex, ey = arm
            elbow = (lerp(sh[0], ex, 0.45) + h * 0.05, lerp(sh[1], ey, 0.55) + h * 0.06)
            m.taper([sh, elbow], h * 0.062, h * 0.05)
            m.taper([elbow, (ex, ey)], h * 0.05, h * 0.038)
            m.circle(ex, ey, h * 0.024)
            continue
        if sling and sgn == -1:
            elbow = (sh[0] - h * 0.01, sh[1] + h * 0.17)
            m.taper([sh, elbow], h * 0.064, h * 0.055)
            m.taper([elbow, (x + h * 0.02, elbow[1] - h * 0.03)], h * 0.075, h * 0.06)
            m.line([(sh[0] + h * 0.02, sh[1] - h * 0.03), (x + h * 0.05, elbow[1] - h * 0.02)], h * 0.018)
            continue
        swing = math.sin(walk + math.pi) * sgn * h * 0.04 if walk is not None else 0.0
        elbow = (sh[0] + sgn * h * 0.025 + swing * 0.5, sh[1] + h * 0.16)
        hand_p = (sh[0] + sgn * h * 0.018 + swing, sh[1] + h * 0.31)
        m.taper([sh, elbow], h * 0.062, h * 0.05)
        m.taper([elbow, hand_p], h * 0.05, h * 0.038)
        m.circle(hand_p[0], hand_p[1], h * 0.024)
    if net:
        grip = (x + sw * 0.5 + h * 0.02, ys + h * 0.3)
        top = (x - h * 0.22, y - h * 1.28)
        m.line([grip, top], h * 0.018)
        m.d.ellipse([(top[0] - h * 0.11) * m.ss, (top[1] - h * 0.09) * m.ss, (top[0] + h * 0.11) * m.ss,
                     (top[1] + h * 0.09) * m.ss], outline=255, width=max(1, int(h * 0.016 * m.ss)))
    if bag:
        bx, by = x - sw * 0.55, waist + h * 0.03
        m.rect(bx - h * 0.07, by - h * 0.06, bx + h * 0.05, by + h * 0.06, r=h * 0.015)
        m.line([(bx, by - h * 0.06), (x + sw * 0.35, ys + h * 0.01)], h * 0.012)
    return m


def hand(m, x, y, s, rot=0.0, pose="open", curl=0.0):
    """손 실루엣(손등이 보이는 쪽). 원점이 손목, 손가락은 -y(위)로 뻗는다. rot로 돌린다."""
    palm = [(-0.42, 0), (0.42, 0), (0.5, -0.9), (-0.5, -0.9)]
    m.poly(xf(palm, x, y, s, rot))
    m.ellipse(*xf([(0, -0.8)], x, y, s, rot)[0], 0.52 * s, 0.32 * s, rot)
    # 손목·팔
    m.poly(xf([(-0.36, 0.05), (0.36, 0.05), (0.42, 2.2), (-0.44, 2.2)], x, y, s, rot))
    fingers = [(-0.36, 1.0, -0.12), (-0.12, 1.15, -0.03), (0.12, 1.1, 0.03), (0.34, 0.9, 0.1)]
    for fx, ln, ang in fingers:
        if pose == "grip":
            pts = [(fx, -0.85), (fx + 0.05, -1.3), (fx + 0.1, -1.1), (fx + 0.12, -0.9)]
        elif pose == "point" and fx != -0.12:
            pts = [(fx, -0.85), (fx + 0.03, -1.15), (fx + 0.08, -1.0)]
        else:
            c = curl
            pts = [(fx, -0.85), (fx + ang * ln * 0.5, -0.85 - ln * 0.55 * (1 - c * 0.5)),
                   (fx + ang * ln + c * 0.25, -0.85 - ln * (1 - c * 0.7))]
        m.line(xf(pts, x, y, s, rot), 0.2 * s)
    # 엄지
    th = [(0.42, -0.35), (0.72, -0.6), (0.9, -0.85)] if pose != "grip" else [(0.42, -0.35), (0.62, -0.75), (0.45, -1.05)]
    m.line(xf(th, x, y, s, rot), 0.22 * s)
    return m


def hand_side(m, x, y, s, rot=0.0, curl=0.0, cuff=True, mirror=False):
    """
    옆에서 본 손(손등이 위) — 원점은 손목, 손가락은 +x로 뻗는다. 팔뚝은 -x 쪽으로 화면 밖까지 이어진다.
    실루엣으로 읽히려면 손가락 사이 틈과 엄지의 각이 보여야 한다 — 네모 팔에 손가락만 붙이면 장갑처럼 읽힌다.
    """
    def X(pts, *a):
        return xf([(-px, py) for px, py in pts] if mirror else pts, *a)

    arm = [(-4.0, -0.42), (-1.6, -0.36), (-0.2, -0.28), (0.25, -0.36), (0.75, -0.3), (0.95, -0.12),
           (0.9, 0.2), (0.3, 0.3), (-0.2, 0.3), (-1.6, 0.42), (-4.0, 0.52)]
    m.poly(X(arm, x, y, s, rot))
    if cuff:
        m.poly(X([(-1.75, -0.47), (-1.35, -0.47), (-1.35, 0.52), (-1.75, 0.55)], x, y, s, rot))
    # 손가락 — 검지가 가장 위, 새끼로 갈수록 짧고 아래. curl이면 끝이 아래로 굽는다.
    for k, (by, ln, wd) in enumerate(((-0.22, 1.25, 0.17), (-0.07, 1.3, 0.17), (0.07, 1.15, 0.16), (0.18, 0.92, 0.14))):
        tip_drop = 0.08 + curl * 0.5 + k * 0.03
        pts = bezier((0.8, by), (0.8 + ln * 0.45, by - 0.02), (0.8 + ln * 0.85, by + tip_drop * 0.5),
                     (0.8 + ln * (1 - curl * 0.25), by + tip_drop), 10)
        m.taper(X(pts, x, y, s, rot), wd * s * 1.0, wd * s * 0.72)
    thumb = bezier((0.1, 0.22), (0.45, 0.45), (0.85, 0.5), (1.1, 0.42), 10)
    m.taper(X(thumb, x, y, s, rot), 0.2 * s, 0.13 * s)
    return m


def fist(m, x, y, s, rot=0.0):
    """자루를 쥔 주먹(옆모습) — 원점은 손목, 주먹은 +x 쪽."""
    m.poly(xf([(-4.0, -0.4), (-0.2, -0.3), (-0.2, 0.34), (-4.0, 0.5)], x, y, s, rot))
    m.poly(xf([(-1.75, -0.45), (-1.35, -0.45), (-1.35, 0.5), (-1.75, 0.52)], x, y, s, rot))
    m.ellipse(*xf([(0.35, 0.0)], x, y, s, rot)[0], 0.62 * s, 0.5 * s, rot)
    for k in range(4):   # 말아 쥔 손가락 마디
        m.circle(*xf([(0.55 + k * 0.02, -0.28 + k * 0.2)], x, y, s, rot)[0], 0.17 * s)
    m.taper(xf(bezier((0.0, -0.35), (0.35, -0.6), (0.65, -0.55), (0.8, -0.35), 8), x, y, s, rot), 0.2 * s, 0.14 * s)
    return m


# ────────────────────────── 곤충 ──────────────────────────

def beetle(m, x, y, s, rot=0.0, horn=False, longhorn=False, wing=0.0):
    """딱정벌레 위에서 본 모습 — 머리가 -y. wing>0이면 딱지날개를 펴고 속날개를 친다."""
    if wing > 0:
        for sgn in (-1, 1):
            m.ellipse(*xf([(sgn * 0.75, 0.1)], x, y, s, rot)[0], 0.9 * s, 0.26 * s, rot + sgn * (0.5 + wing * 0.3))
            m.ellipse(*xf([(sgn * 0.45, -0.05)], x, y, s, rot)[0], 0.3 * s, 0.6 * s, rot + sgn * 0.7)
        m.ellipse(x, y, 0.36 * s, 0.6 * s, rot)
    else:
        m.ellipse(*xf([(0, 0.15)], x, y, s, rot)[0], 0.48 * s, 0.66 * s, rot)
    m.ellipse(*xf([(0, -0.55)], x, y, s, rot)[0], 0.34 * s, 0.24 * s, rot)
    m.ellipse(*xf([(0, -0.82)], x, y, s, rot)[0], 0.18 * s, 0.14 * s, rot)
    for sgn in (-1, 1):
        for k, (ly, lx) in enumerate(((-0.45, 0.62), (-0.05, 0.7), (0.35, 0.62))):
            m.line(xf([(sgn * 0.3, ly), (sgn * lx, ly + 0.05 * (k - 1)), (sgn * (lx + 0.12), ly + 0.25)], x, y, s, rot),
                   0.07 * s)
        if longhorn:
            m.line(xf(bezier((sgn * 0.1, -0.9), (sgn * 0.5, -1.4), (sgn * 1.0, -1.3), (sgn * 1.5, -0.6), 12), x, y, s, rot),
                   0.05 * s, caps=False)
        else:
            m.line(xf([(sgn * 0.08, -0.92), (sgn * 0.25, -1.15)], x, y, s, rot), 0.05 * s)
    if horn:
        m.taper(xf(bezier((0, -0.9), (0, -1.3), (0.15, -1.6), (0.3, -1.75), 8), x, y, s, rot), 0.16 * s, 0.05 * s)
    return m


def beetle_side(m, x, y, s, rot=0.0, wing=0.0, mirror=False):
    """옆에서 본 하늘소 — 머리가 +x, 긴 더듬이가 등 위로 휜다. wing>0이면 날개를 편다(날갯짓 위상)."""
    def X(pts):
        return xf([(-px, py) for px, py in pts] if mirror else pts, x, y, s, rot)

    if wing > 0:
        m.poly(X([(-0.2, -0.25), (0.2, -0.3), (-0.4, -1.1 - 0.3 * wing), (-0.9, -0.9 - 0.2 * wing)]), 150)
        m.poly(X([(0.0, -0.2), (0.35, -0.3), (0.1, -0.95 + 0.4 * wing), (-0.35, -0.85 + 0.4 * wing)]), 150)
    m.poly(X([(-1.0, 0.0), (-0.8, -0.26), (0.5, -0.3), (0.72, -0.12), (0.7, 0.12), (-0.6, 0.2)]))
    m.poly(X([(0.62, -0.2), (0.95, -0.22), (1.12, -0.05), (1.0, 0.12), (0.66, 0.12)]))
    for ax in (0.0, 0.25):
        m.line(X(bezier((1.0, -0.2), (1.2 - ax, -0.9), (0.3 - ax, -1.25), (-0.6 - ax, -1.1), 14)), 0.045 * s, caps=False)
    for lx in (-0.55, 0.0, 0.45):
        m.line(X([(lx, 0.1), (lx - 0.12, 0.38), (lx - 0.02, 0.55)]), 0.06 * s)
    return m


def dragonfly(m, x, y, s, rot=0.0, flap=0.0):
    m.circle(*xf([(0, -0.85)], x, y, s, rot)[0], 0.13 * s)
    m.ellipse(*xf([(0, -0.6)], x, y, s, rot)[0], 0.1 * s, 0.18 * s, rot)
    m.taper(xf([(0, -0.45), (0, 0.4), (0, 1.3)], x, y, s, rot), 0.11 * s, 0.05 * s)
    for sgn in (-1, 1):
        for k, (wy, ln, ang) in enumerate(((-0.62, 1.2, -0.08), (-0.45, 1.05, 0.18))):
            a = ang + flap * (0.35 if k == 0 else -0.35)
            m.ellipse(*xf([(sgn * ln * 0.5, wy)], x, y, s, rot)[0], ln * 0.5 * s, 0.1 * s, rot + sgn * a)
    return m


def butterfly(m, x, y, s, rot=0.0, flap=0.0):
    k = 1 - 0.75 * flap   # 날갯짓 — 폭이 좁아진다
    for sgn in (-1, 1):
        m.ellipse(*xf([(sgn * 0.55 * k, -0.25)], x, y, s, rot)[0], 0.6 * k * s, 0.45 * s, rot - sgn * 0.5)
        m.ellipse(*xf([(sgn * 0.42 * k, 0.35)], x, y, s, rot)[0], 0.42 * k * s, 0.36 * s, rot + sgn * 0.45)
        m.line(xf([(sgn * 0.05, -0.55), (sgn * 0.3, -1.0)], x, y, s, rot), 0.04 * s)
    m.taper(xf([(0, -0.5), (0, 0.55)], x, y, s, rot), 0.12 * s, 0.07 * s)
    return m


# ────────────────────────── 소품 ──────────────────────────

def scribble(m, x, y, w, rows, seed, row_h=18, weight=2.0, cols=None, progress=1.0):
    """판독 불가 긁적임 — 글자처럼 보이지만 글자가 아니다. progress로 앞에서부터 드러낸다."""
    r = np.random.default_rng(seed)
    total = rows * w
    drawn = 0.0
    for row in range(rows):
        cx = x
        yy = y + row * row_h
        row_w = w * (0.55 + 0.45 * r.random()) if cols is None else w
        while cx < x + row_w:
            seg = r.uniform(8, 26)
            if drawn + seg > total * progress:
                return m
            n = int(seg / 4) + 2
            pts = [(cx + i * seg / (n - 1), yy + r.uniform(-row_h * 0.22, row_h * 0.22)) for i in range(n)]
            m.line(pts, weight, caps=False)
            cx += seg + r.uniform(5, 12)
            drawn += seg
    return m


def glyphs(m, x, y, w, rows, seed, size=26, weight=4.0, progress=1.0):
    """새김 문양 — 곧은 획과 짧은 갈고리로 된 가짜 고대 문자 줄."""
    r = np.random.default_rng(seed)
    cells = []
    for row in range(rows):
        cx = x
        while cx < x + w - size:
            cells.append((cx, y + row * size * 1.6))
            cx += size * r.uniform(0.9, 1.3)
    lim = int(len(cells) * progress)
    for i, (cx, cy) in enumerate(cells[:lim]):
        rr = np.random.default_rng(seed * 1000 + i)
        for _ in range(rr.integers(2, 4)):
            kind = rr.integers(0, 4)
            if kind == 0:
                m.line([(cx + rr.uniform(0, size), cy), (cx + rr.uniform(0, size), cy + size)], weight)
            elif kind == 1:
                yy = cy + rr.uniform(0.2, 0.8) * size
                m.line([(cx, yy), (cx + size * rr.uniform(0.5, 1), yy)], weight)
            elif kind == 2:
                m.line([(cx, cy + size), (cx + size * 0.5, cy), (cx + size, cy + size)], weight)
            else:
                m.line(bezier((cx, cy + size * 0.3), (cx + size * 0.3, cy - size * 0.1),
                              (cx + size * 0.9, cy + size * 0.2), (cx + size * 0.7, cy + size), 8), weight, caps=False)
    return m, cells


def crate(m, x, y, w, h, lid_open=0.0):
    """나무 상자 윤곽(채움). 판자 틈은 crate_seams로 따로 그린다."""
    m.rect(x, y, x + w, y + h)
    if lid_open > 0:
        m.poly([(x, y), (x + w, y), (x + w - w * 0.05, y - h * 0.5 * lid_open), (x - w * 0.02, y - h * 0.45 * lid_open)])
    return m


def crate_seams(m, x, y, w, h, planks=4, weight=2.0):
    for k in range(1, planks):
        yy = y + h * k / planks
        m.line([(x + 3, yy), (x + w - 3, yy)], weight, caps=False)
    m.line([(x + 4, y + 4), (x + w - 4, y + h - 4)], weight * 1.2, caps=False)
    m.rect(x, y, x + w * 0.07, y + h)
    m.rect(x + w * 0.93, y, x + w, y + h)
    return m


def book(m, cx, cy, w, h, openness=1.0, rot=0.0):
    """펼친 장부 — openness 1이면 두 쪽, 0이면 덮였다(오른쪽 표지가 왼쪽으로 넘어온다)."""
    left = [(-w, -h / 2), (0, -h / 2 - 6), (0, h / 2 - 6), (-w, h / 2)]
    m.poly(xf(left, cx, cy, 1, rot))
    rw = w * (2 * openness - 1)
    right = [(0, -h / 2 - 6), (rw, -h / 2), (rw, h / 2), (0, h / 2 - 6)]
    m.poly(xf(right, cx, cy, 1, rot))
    return m


# ────────────────────────── 장면 부품 ──────────────────────────

class Layer:
    """정적 실루엣 레이어 — 색(단색 또는 배열) + 알파. 카메라 이동을 받으려고 여백을 둔다."""

    def __init__(self, alpha, color, parallax=1.0, margin=(0, 0)):
        self.a = alpha
        self.c = np.asarray(color, np.float32)
        self.k = parallax
        self.mx, self.my = margin

    def draw(self, dst, camx=0.0, camy=0.0):
        a = crop(self.a, self.mx + camx * self.k, self.my + camy * self.k)
        if self.c.ndim == 3:
            c = crop(self.c, self.mx + camx * self.k, self.my + camy * self.k)
        else:
            c = self.c
        over(dst, c, a)


def haze(c_far, c_near, t):
    return np.asarray(c_far) * (1 - t) + np.asarray(c_near) * t


def fog_band(seed, w, h, y0, y1, soft=60.0, density=0.8):
    n = fbm(w, h, seed, (260, 90), (0.7, 0.3))
    yy = np.arange(h, dtype=np.float32)[:, None]
    band = np.clip((yy - (y0 - soft)) / soft, 0, 1) * np.clip(((y1 + soft) - yy) / soft, 0, 1)
    return np.clip((n - 0.35) * 1.6, 0, 1) * band * density


def bokeh(w, h, seed, n, color, rmin=10, rmax=38, alpha=0.18):
    """흐린 빛망울 — 역광 배경."""
    rng = np.random.default_rng(seed)
    acc = np.zeros((h, w), np.float32)
    for _ in range(n):
        x, y, r = rng.uniform(0, w), rng.uniform(0, h), rng.uniform(rmin, rmax)
        m = Mask(w, h, 1).circle(x, y, r).arr(blur=r * 0.25)
        acc += m * rng.uniform(0.4, 1.0)
    out = np.zeros((h, w, 3), np.float32)
    add(out, color, np.clip(acc, 0, 1.5) * alpha)
    return out


def with_rim(dst, mask, dx, dy, color, strength=1.0, width=2.5):
    add(dst, color, rim(mask, dx, dy, width) * strength)


# ────────────────────────── 물·구름·건물 ──────────────────────────

def reflect(img, horizon, t, amp=4.0, freq=0.11, speed=2.2, dark=0.62, tint=None, mix=0.35):
    """수평선 아래를 위쪽 그림의 거울상으로 채운다 — 줄마다 흔들려 물결이 된다. 먼 쪽일수록 덜 흔들린다."""
    out = img.copy()
    h = img.shape[0]
    hz = int(horizon)
    for r in range(hz, h):
        k = (r - hz) / max(1, h - hz)
        src = min(h - 1, max(0, hz - 1 - (r - hz)))
        d = int(round(math.sin(r * freq + t * speed) * amp * (0.25 + k * 1.2)))
        out[r] = np.roll(img[src], d, axis=0)
    out[hz:] *= dark
    if tint is not None:
        out[hz:] = out[hz:] * (1 - mix) + np.asarray(tint, np.float32) * mix * dark
    return out


def ripple(m, cx, cy, r, w=1.6):
    """물 위 동심원 한 겹(납작한 타원 테두리)."""
    m.d.ellipse([(cx - r) * m.ss, (cy - r * 0.22) * m.ss, (cx + r) * m.ss, (cy + r * 0.22) * m.ss],
                outline=255, width=max(1, int(w * m.ss)))
    return m


def temple(m, cx, base, w, h, broken=0.3, seed=0):
    """
    무너진 신전 — 계단 기단, 기둥머리가 있는 기둥, 한쪽이 무너진 들보·박공, 발치의 잔해.
    대칭으로 반듯하게 그리면 은행 아이콘처럼 읽힌다 — 무너짐이 이것을 "유적"으로 만든다.
    """
    r = np.random.default_rng(seed)
    for k in range(3):
        sw = w * (1.02 - k * 0.07)
        m.rect(cx - sw / 2, base - h * 0.045 * (k + 1), cx + sw / 2, base - h * 0.045 * k)
    m.poly([(cx - w * 0.1, base), (cx + w * 0.1, base), (cx + w * 0.07, base - h * 0.135), (cx - w * 0.07, base - h * 0.135)])
    top = base - h * 0.135
    beam = base - h * 0.74
    cols = 7
    cw = w * 0.058
    fallen_from = cx + w * (0.05 + 0.25 * r.random()) if broken > 0 else cx + w
    for i in range(cols):
        x = cx - w * 0.42 + i * (w * 0.84 / (cols - 1))
        ch = top - beam
        if x > fallen_from:
            ch *= r.uniform(0.25, 0.7)
        elif r.random() < broken * 0.4:
            ch *= r.uniform(0.75, 0.95)
        m.rect(x - cw / 2, top - ch, x + cw / 2, top)
        if ch >= (top - beam) * 0.98:
            m.rect(x - cw * 0.8, top - ch - h * 0.025, x + cw * 0.8, top - ch)
    left = cx - w * 0.48
    right = min(cx + w * 0.48, fallen_from + cw)
    m.poly([(left, beam - h * 0.025), (right, beam - h * 0.025), (right - w * 0.02, beam - h * 0.1),
            (left, beam - h * 0.1)])
    peak_x = cx
    if right > peak_x:
        m.poly([(left, beam - h * 0.1), (right, beam - h * 0.1),
                (right, beam - h * 0.1 - (h * 0.2) * (1 - (right - cx) / (w * 0.5))), (peak_x, beam - h * 0.3)])
    else:
        m.poly([(left, beam - h * 0.1), (right, beam - h * 0.1), (right, beam - h * 0.1 - h * 0.2 * (right - left) / (w * 0.48))])
    for _ in range(int(6 + broken * 10)):   # 발치 잔해
        bx = cx + r.uniform(0.0, 0.6) * w
        bs = r.uniform(0.02, 0.05) * w
        m.poly(xf([(-bs, 0), (bs, -bs * 0.2), (bs * 0.8, -bs * 0.9), (-bs * 0.7, -bs * 0.8)], bx, base, 1, r.uniform(-0.5, 0.5)))
    return m


def cloud_bank(w, h, seed, y, thick, count=40, rmin=40, rmax=120, x0=0, x1=None):
    """구름 띠 알파 — 둥근 덩어리를 겹쳐 흐린다."""
    x1 = w if x1 is None else x1
    r = np.random.default_rng(seed)
    m = Mask(w, h, 1)
    for _ in range(count):
        rad = r.uniform(rmin, rmax)
        m.circle(r.uniform(x0, x1), y + r.uniform(-thick * 0.5, thick * 0.5), rad)
    return m.arr(blur=rmin * 0.35)


def cave_mouth(w, h, seed, cx, cy, rx, ry):
    """동굴 안에서 본 입구 — 가장자리가 울퉁불퉁한 구멍 바깥을 채운 알파."""
    r = np.random.default_rng(seed)
    ph = r.uniform(0, 6.28, 3)
    pts = []
    for i in range(120):
        a = 2 * math.pi * i / 120
        k = 1 + 0.08 * math.sin(a * 5 + ph[0]) + 0.05 * math.sin(a * 11 + ph[1]) + 0.03 * math.sin(a * 23 + ph[2])
        pts.append((cx + math.cos(a) * rx * k, cy + math.sin(a) * ry * k))
    hole = Mask(w, h).poly(pts).arr(2.0)
    return 1.0 - hole


def streaks(dst, t, seed, n, color, alpha, vx=900.0, length=40.0, y0=0, y1=H):
    """바람에 날리는 가는 줄 — 빠르게 가로지르는 티끌."""
    r = np.random.default_rng(seed)
    for i in range(n):
        x = (r.uniform(0, W + 400) + vx * t * r.uniform(0.7, 1.3)) % (W + 400) - 200
        y = r.uniform(y0, y1) + math.sin(t * 3 + i) * 6
        for k in range(6):
            dot(dst, x - k * length / 6, y, 1.2, color, alpha * (1 - k / 6))
