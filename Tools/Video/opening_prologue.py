"""오프닝 프롤로그 영상 렌더러 — 오프닝 일러스트 3장(가로/세로)을 20초 2.5D 모션 영상으로 만든다.

    python -X utf8 Tools/Video/opening_prologue.py landscape|portrait [--preview 2.0,6.5,...]

산출: Assets/StreamingAssets/Video/opening_prologue_<orient>.mp4 (무음 — 소리는 opening_audio.py가 만든 wav를
게임의 AudioSource가 튼다). --preview면 영상 대신 지정 시각의 프레임만 PNG로 스크래치에 떨군다.

**타임라인은 OpeningSequenceState(Assets/Scripts/Opening)의 상수와 맞물린다.** 내레이션·타이틀은 게임이
IMGUI로 얹으므로 여기서는 그 자리를 비워 두기만 한다. 한쪽 시각을 바꾸면 반드시 다른 쪽도 고친다.

    0.0 ~ 9.6   그림1 밤숲 길 — 빛이 하나씩 꺼지고(잦아듦), 길 끝에 그물을 든 검은 코트가 선다.
                남은 빛은 그 그물로 빨려 들어가 사라진다. 8.3~9.4 암전.
    9.6 ~ 15.0  그림2 파트너 — 어둠 속에서 떠오르고, 꺼졌던 빛이 하나둘 돌아온다(테마곡 10.0 시작).
    14.0~ 15.0  그림2→3 맞춤 컷(두 그림의 눈을 같은 자리·같은 크기에 겹친다).
    15.0 ~ 20.0 그림3 빛나는 잎 — 16.0 곡의 강세에 빛이 번쩍이고 타이틀이 뜬다. 19.2~20.0 페이드아웃.

numpy + PIL + ffmpeg만 쓴다. 메모리를 아끼려고 샷은 필요한 구간에서만 올렸다가 버린다.
"""
import os
import subprocess
import sys
import time

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "Assets", "Resources", "UI", "Opening", "opening_0{}_{}.jpg")
OUT_DIR = os.path.join(ROOT, "Assets", "StreamingAssets", "Video")
FF = os.environ.get("FFMPEG", "C:/Users/kss11/AppData/Local/Programs/Python/Python312/Scripts/ffmpeg.exe")
FPS = 24
T_TOTAL = 20.0

# ── 타임라인 (OpeningSequenceState와 같은 값) ──
S1_END = 9.6
DIM = (1.5, 7.5)            # 숲이 가라앉는 구간
VANISH1 = (1.2, 6.6)        # 그림 속 빛이 꺼지는 구간
FIGURE_IN = (4.3, 5.9)      # 길 끝 실루엣
COLLECT = (4.9, 6.1, 8.1)   # 빨려가기 시작 / 첫 도착 / 마지막 도착
DIP1 = (8.3, 9.4)           # 암전
S2_IN = (9.8, 10.9)
REVIVE2 = (10.8, 14.0)      # 그림2의 빛이 돌아오는 구간
D23 = (14.0, 15.0)          # 맞춤 컷
TITLE_HIT = 16.0
FADE_OUT = (19.2, 20.0)

ORIENT = sys.argv[1] if len(sys.argv) > 1 else "landscape"
if ORIENT not in ("landscape", "portrait"):
    raise SystemExit("orientation must be landscape|portrait")
PREVIEW = None
if "--preview" in sys.argv:
    PREVIEW = [float(v) for v in sys.argv[sys.argv.index("--preview") + 1].split(",")]
SCRATCH = os.environ.get("PREVIEW_DIR", os.path.join(ROOT, "Artifacts", "opening-prologue-preview"))

if ORIENT == "landscape":
    SW, SH, W, H = 1920, 1080, 1280, 720
else:
    SW, SH, W, H = 1080, 1920, 720, 1280

rng = np.random.default_rng(11)


# ───────────────────────── 수학 도우미 ─────────────────────────
def smooth(u):
    u = np.clip(u, 0.0, 1.0)
    return u * u * u * (u * (u * 6 - 15) + 10)


def sstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def _box1d(a, r, axis):
    if r < 1:
        return a
    pad = [(0, 0)] * a.ndim
    pad[axis] = (r + 1, r)
    p = np.pad(a, pad, mode="edge")
    c = np.cumsum(p, axis=axis, dtype=np.float64)
    n = a.shape[axis]
    hi = np.take(c, np.arange(2 * r + 1, 2 * r + 1 + n), axis=axis)
    lo = np.take(c, np.arange(0, n), axis=axis)
    return ((hi - lo) / (2 * r + 1)).astype(np.float32)


def blur_f(arr, sigma):
    """float 가우시안 근사(박스 3회) — 이 PC의 Pillow는 'F' 모드 GaussianBlur를 지원하지 않는다."""
    r = max(1, int(round((np.sqrt(12 * sigma * sigma / 3 + 1) - 1) / 2)))
    out = arr.astype(np.float32)
    for _ in range(3):
        out = _box1d(_box1d(out, r, 0), r, 1)
    return out


def lum(a):
    return a[..., 0] * 0.299 + a[..., 1] * 0.587 + a[..., 2] * 0.114


# ───────────────────────── 카메라 ─────────────────────────
def crop_box(cam, u):
    (z0, cx0, cy0), (z1, cx1, cy1) = cam
    z = z0 + (z1 - z0) * u
    cx = cx0 + (cx1 - cx0) * u
    cy = cy0 + (cy1 - cy0) * u
    w, h = SW / z, SH / z
    cx = min(max(cx, w / 2), SW - w / 2)
    cy = min(max(cy, h / 2), SH - h / 2)
    return (cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2)


def clamped_center(z, cx, cy):
    w, h = SW / z, SH / z
    return min(max(cx, w / 2), SW - w / 2), min(max(cy, h / 2), SH - h / 2)


def to_screen(cam, u, p):
    x0, y0, x1, y1 = crop_box(cam, u)
    return np.array([(p[0] - x0) * W / (x1 - x0), (p[1] - y0) * H / (y1 - y0)], np.float32)


def warp(arr, cam, u):
    box = crop_box(cam, u)
    if arr.ndim == 2:
        return np.asarray(Image.fromarray(arr.astype(np.float32)).resize((W, H), Image.Resampling.BICUBIC, box=box))
    u8 = Image.fromarray((np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8))
    return np.asarray(u8.resize((W, H), Image.Resampling.LANCZOS, box=box)).astype(np.float32) / 255.0


# ───────────────────────── 길 끝의 실루엣 ─────────────────────────
def figure_layer(height):
    """그물을 든 검은 코트(뒷모습에 가까운 3/4) — 명부회 하수의 인상. 얼굴은 그리지 않는다.
    반환: (rgb, alpha, 발끝 기준 오프셋, 그물테 중심 오프셋). 4배로 그려 줄여 가장자리를 부드럽게."""
    ss = 4
    Hf = height * ss
    cw, ch = int(Hf * 1.3), int(Hf * 1.55)
    ox, oy = int(cw * 0.36), int(ch * 0.97)          # 발끝 기준점
    im = Image.new("L", (cw, ch), 0)
    d = ImageDraw.Draw(im)

    def P(x, y):
        return (ox + x * Hf, oy + y * Hf)

    # 다리(밑단 아래로 조금)
    d.polygon([P(-0.075, 0), P(-0.03, 0), P(-0.035, -0.2), P(-0.08, -0.2)], fill=255)
    d.polygon([P(0.03, 0), P(0.078, 0), P(0.075, -0.2), P(0.03, -0.2)], fill=255)
    # 코트 — 밑단이 퍼지고 허리가 들어간 긴 외투. 오른쪽 자락이 바람에 조금 더 벌어진다
    coat = [(-0.175, -0.13), (-0.12, -0.155), (-0.02, -0.14), (0.09, -0.15), (0.205, -0.12),
            (0.16, -0.32), (0.12, -0.5), (0.15, -0.66), (0.155, -0.74), (0.11, -0.785),
            (0.06, -0.8), (-0.06, -0.8), (-0.12, -0.78), (-0.16, -0.73), (-0.15, -0.64),
            (-0.12, -0.5), (-0.14, -0.3)]
    d.polygon([P(x, y) for x, y in coat], fill=255)
    # 그물 쪽 팔 — 어깨에서 앞으로 내민 소매
    d.polygon([P(0.1, -0.74), P(0.16, -0.7), P(0.2, -0.56), P(0.17, -0.52), P(0.12, -0.6)], fill=255)
    # 머리 + 챙 깊은 모자
    d.ellipse([P(-0.055, -0.93), P(0.055, -0.8)], fill=255)
    d.ellipse([P(-0.135, -0.925), P(0.135, -0.885)], fill=255)
    d.polygon([P(-0.07, -0.905), P(0.07, -0.905), P(0.062, -0.99), P(0.03, -1.01), P(-0.03, -1.01), P(-0.062, -0.99)], fill=255)
    # 그물채 — 손에서 비스듬히 위로
    hand, top = P(0.19, -0.54), P(0.52, -1.2)
    d.line([hand, top], fill=255, width=max(2, int(0.014 * Hf)))
    hoop_c = (0.575, -1.27)
    rx, ry = 0.1, 0.055
    d.ellipse([P(hoop_c[0] - rx, hoop_c[1] - ry), P(hoop_c[0] + rx, hoop_c[1] + ry)],
              outline=255, width=max(2, int(0.012 * Hf)))
    # 그물 자루(반투명) — 테 아래로 처진다
    bag = Image.new("L", (cw, ch), 0)
    db = ImageDraw.Draw(bag)
    db.polygon([P(hoop_c[0] - rx, hoop_c[1]), P(hoop_c[0] + rx, hoop_c[1]), P(hoop_c[0] + 0.04, hoop_c[1] + 0.19),
                P(hoop_c[0] - 0.01, hoop_c[1] + 0.22)], fill=110)
    mask = np.maximum(np.asarray(im, np.float32), np.asarray(bag, np.float32)) / 255.0
    small = Image.fromarray((mask * 255).astype(np.uint8)).resize((cw // ss, ch // ss), Image.Resampling.LANCZOS)
    a = np.asarray(small, np.float32) / 255.0
    a = blur_f(a, 0.6)                                # 원경 — 아주 살짝 흐리게
    # 등 뒤 하늘빛이 윤곽을 스친다 — 팽창 윤곽의 위쪽 절반만
    dil = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5)), np.float32) / 255.0
    rim = np.clip(dil - a, 0, 1)
    yy = np.linspace(0, 1, a.shape[0], dtype=np.float32)[:, None]
    rim *= (1 - sstep(0.35, 0.8, yy))
    body = np.array([0.012, 0.018, 0.04], np.float32)
    rimc = np.array([0.30, 0.46, 0.85], np.float32)
    rgb = body * a[..., None] + rimc * rim[..., None] * 0.55
    alpha = np.clip(a * 0.9 + rim * 0.45, 0, 1)
    foot = (ox // ss, oy // ss)
    hoop = (foot[0] + hoop_c[0] * height, foot[1] + hoop_c[1] * height)
    return rgb.astype(np.float32), alpha.astype(np.float32), foot, hoop


# ───────────────────────── 샷(그림 한 장) ─────────────────────────
class Shot:
    def __init__(self, idx, cam_bg, cam_fg, mask_cfg, t0, t1, spot_mode, spot_window=None, figure=None):
        self.idx, self.t0, self.t1 = idx, t0, t1
        self.cam_bg, self.cam_fg = cam_bg, cam_fg
        img = np.asarray(Image.open(SRC.format(idx, ORIENT)).convert("RGB")).astype(np.float32) / 255.0
        assert img.shape[:2] == (SH, SW), img.shape
        self.src = img
        self.mask = self.build_mask(img, **mask_cfg)
        self.detect_spots(img)
        mh = np.clip(self.mask * 1.6, 0, 1)
        keep = 1.0 - mh
        num = blur_f(img * keep[..., None], 48)
        den = blur_f(keep, 48)[..., None] + 1e-4
        self.fill = num / den * 0.85
        self.mh = mh
        self.spot_mode, self.spot_window = spot_mode, spot_window
        self.setup_spot_schedule()
        self.figure = None
        if figure:
            fx, fy, fh = figure
            rgb, a, foot, hoop = figure_layer(fh)
            x0, y0 = int(fx - foot[0]), int(fy - foot[1])
            self.figure = (x0, y0, rgb, a)
            self.hoop_src = (x0 + hoop[0], y0 + hoop[1])

    @staticmethod
    def build_mask(img, cx=0.5, cy=0.5, rx=0.5, ry=0.5, inner=0.62, outer=1.02, bottom=0.0, green_w=0.5, blur=30):
        ys, xs = np.mgrid[0:SH, 0:SW].astype(np.float32)
        r = np.sqrt(((xs / SW - cx) / rx) ** 2 + ((ys / SH - cy) / ry) ** 2)
        pos = sstep(inner, outer, r)
        if bottom > 0:
            pos = np.maximum(pos, sstep(1.0 - bottom, 1.0, ys / SH) * 0.9)
        g_dom = sstep(-0.02, 0.10, img[..., 1] - img[..., 2])
        near = np.clip(g_dom * sstep(0.03, 0.14, lum(img)), 0, 1)
        m = pos * ((1 - green_w) + green_w * near)
        m = blur_f(m, blur)
        m = sstep(0.18, 0.55, m)
        m = blur_f(m, blur * 0.6)
        return np.clip(m, 0, 1).astype(np.float32)

    def detect_spots(self, img, max_spots=70):
        l = lum(img)
        warm = (img[..., 0] > 0.55) & (img[..., 1] > 0.42) & (img[..., 2] < img[..., 1] * 0.8)
        cand = (l > 0.55) & warm
        lu8 = Image.fromarray((np.clip(l, 0, 1) * 255).astype(np.uint8))
        mx = np.asarray(lu8.filter(ImageFilter.MaxFilter(9))).astype(np.float32) / 255.0
        peaks = cand & (np.abs(l - mx) < 1e-3)
        ys, xs = np.nonzero(peaks)
        order = np.argsort(-l[ys, xs])
        spots = []
        for k in order:
            y, x = int(ys[k]), int(xs[k])
            if any((x - sx) ** 2 + (y - sy) ** 2 < 18 ** 2 for sx, sy, _ in spots):
                continue
            y0, y1, x0, x1 = max(0, y - 20), min(SH, y + 21), max(0, x - 20), min(SW, x + 21)
            if int(cand[y0:y1, x0:x1].sum()) > 260:      # 큰 광원(뿔·노을)은 반딧불이 아니다
                continue
            area = int(cand[y0:y1, x0:x1].sum())
            spots.append((x, y, float(np.clip(np.sqrt(max(area, 1) / np.pi) * 2.2 + 5, 6, 20))))
            if len(spots) >= max_spots:
                break
        self.spots = spots
        s = np.zeros((SH, SW), np.float32)
        boxes = []
        for x, y, rad in spots:
            y0, y1 = max(0, int(y - rad * 2)), min(SH, int(y + rad * 2) + 1)
            x0, x1 = max(0, int(x - rad * 2)), min(SW, int(x + rad * 2) + 1)
            yy, xx = np.mgrid[y0:y1, x0:x1]
            d2 = (xx - x) ** 2 + (yy - y) ** 2
            s[y0:y1, x0:x1] = np.maximum(s[y0:y1, x0:x1], np.exp(-d2 / (2 * (rad * 0.75) ** 2)))
            boxes.append(((y0, y1, x0, x1), np.clip(np.exp(-d2 / (2 * (rad * 0.8) ** 2)) * 1.4, 0, 1)[..., None].astype(np.float32)))
        keep = 1 - np.clip(s * 1.5, 0, 1)
        num = blur_f(img * keep[..., None], 14)
        den = blur_f(keep, 14)[..., None] + 1e-4
        self.nofly = num / den
        self.spot_masks = boxes

    def setup_spot_schedule(self):
        n = len(self.spots)
        self.tw_phase = rng.uniform(0, 2 * np.pi, n)
        self.tw_freq = rng.uniform(0.6, 1.8, n)
        self.death = np.full(n, 1e9)
        self.birth = np.full(n, -1e9)
        if self.spot_mode == "vanish":
            a, b = self.spot_window
            k = int(n * 0.78)
            order = rng.permutation(n)[:k]
            self.death[order] = np.sort(np.linspace(a, b, k) + rng.uniform(-0.08, 0.08, k))
        elif self.spot_mode == "revive":
            a, b = self.spot_window
            order = rng.permutation(n)
            self.birth[order] = np.sort(np.linspace(a, b, n) + rng.uniform(-0.06, 0.06, n))

    def spot_off_weight(self, t):
        w = 0.28 * (0.5 + 0.5 * np.sin(self.tw_phase + t * self.tw_freq * 2 * np.pi)) ** 2
        dead = sstep(0.0, 0.45, t - self.death)
        flick = np.where((t > self.death - 0.35) & (t < self.death), 0.35 * (np.sin((t - self.death) * 60) > 0), 0.0)
        unborn = 1 - sstep(0.0, 0.5, t - self.birth)
        return np.clip(np.maximum(np.maximum(w, dead), unborn) + flick * (1 - dead), 0, 1)

    def source_at(self, t):
        src = self.src.copy()
        for (box, m), w in zip(self.spot_masks, self.spot_off_weight(t)):
            if w < 0.01:
                continue
            y0, y1, x0, x1 = box
            a = m * w
            src[y0:y1, x0:x1] = src[y0:y1, x0:x1] * (1 - a) + self.nofly[y0:y1, x0:x1] * a
        if self.figure is not None:
            fa = float(sstep(*FIGURE_IN, t))
            if fa > 0.001:
                x0, y0, rgb, a = self.figure
                h, w = a.shape
                ya, yb, xa, xb = max(0, y0), min(SH, y0 + h), max(0, x0), min(SW, x0 + w)
                sub_a = a[ya - y0:yb - y0, xa - x0:xb - x0][..., None] * fa
                sub_rgb = rgb[ya - y0:yb - y0, xa - x0:xb - x0]
                # 원경이라 대기색을 20% 섞는다 — 새까만 오려붙이기로 보이지 않게
                region = src[ya:yb, xa:xb]
                haze = region * 0.2 + sub_rgb * 0.8
                src[ya:yb, xa:xb] = region * (1 - sub_a) + haze * sub_a
        return src

    def u(self, t):
        return smooth((t - self.t0) / (self.t1 - self.t0))

    def render(self, t):
        u = self.u(t)
        src = self.source_at(t)
        bgsrc = src * (1 - self.mh[..., None]) + self.fill * self.mh[..., None]
        bg = warp(bgsrc, self.cam_bg, u)
        fg = warp(src, self.cam_fg, u)
        m = warp(self.mask, self.cam_fg, u)
        return bg * (1 - m[..., None]) + fg * m[..., None]


# ───────────────────────── 떠다니는 빛 ─────────────────────────
def make_sprite(sig_core, sig_halo, size):
    r = size // 2
    yy, xx = np.mgrid[-r:r + 1, -r:r + 1].astype(np.float32)
    d2 = xx ** 2 + yy ** 2
    return np.exp(-d2 / (2 * sig_core ** 2)) + 0.35 * np.exp(-d2 / (2 * sig_halo ** 2))


SCALE = min(W, H) / 720.0
SPRITES = [make_sprite((1.2 + 0.5 * k) * SCALE, (5 + 2.5 * k) * SCALE, int((41 + 8 * k) * SCALE) | 1) for k in range(4)]
FLY_COL = np.array([1.0, 0.84, 0.38], np.float32)
COLD_COL = np.array([0.62, 0.78, 1.0], np.float32)


def blit(canvas, spr, x, y, col, b):
    r = spr.shape[0] // 2
    xi, yi = int(round(x)), int(round(y))
    ya, yb, xa, xb = yi - r, yi + r + 1, xi - r, xi + r + 1
    sy0, sx0 = max(0, -ya), max(0, -xa)
    ya, xa = max(ya, 0), max(xa, 0)
    yb, xb = min(yb, H), min(xb, W)
    if ya >= yb or xa >= xb:
        return
    canvas[ya:yb, xa:xb] += spr[sy0:sy0 + yb - ya, sx0:sx0 + xb - xa, None] * col * b


class Fireflies:
    def __init__(self, n, t0, t1, rise=0.0, vanish=None, birth_window=None, seed=0, region=(0.05, 0.95, 0.1, 0.9)):
        r = np.random.default_rng(seed)
        x0, x1, y0, y1 = region
        self.p = np.stack([r.uniform(x0, x1, n) * W, r.uniform(y0, y1, n) * H], 1)
        self.v = r.normal(0, 9 * SCALE, (n, 2)) + np.array([0, -rise * SCALE])
        self.ph = r.uniform(0, 2 * np.pi, (n, 3))
        self.fr = r.uniform(0.3, 0.9, (n, 3))
        self.size = r.integers(0, 4, n)
        self.base = r.uniform(0.35, 1.0, n)
        self.t0, self.t1 = t0, t1
        if birth_window:
            a, b = birth_window
            self.birth = np.sort(np.linspace(a, b, n) + r.uniform(-0.1, 0.1, n))
            r.shuffle(self.birth)
        else:
            self.birth = t0 + r.uniform(0, 0.8, n)
        self.death = np.full(n, 1e9)
        if vanish:
            a, b = vanish
            k = int(n * 0.8)
            idx = r.permutation(n)[:k]
            self.death[idx] = np.sort(np.linspace(a, b, k) + r.uniform(-0.1, 0.1, k))

    def draw(self, canvas, t, strength):
        if t < self.t0 or t > self.t1 or strength <= 0.001:
            return
        tt = t - self.t0
        for i in range(len(self.p)):
            a = sstep(0, 0.6, t - self.birth[i]) * (1 - sstep(0, 0.5, t - self.death[i]))
            if a <= 0.005:
                continue
            wob = np.array([np.sin(self.ph[i, 0] + tt * self.fr[i, 0] * 2 * np.pi) * 14,
                            np.sin(self.ph[i, 1] + tt * self.fr[i, 1] * 2 * np.pi) * 9]) * SCALE
            x, y = self.p[i] + self.v[i] * tt + wob
            blink = (0.5 + 0.5 * np.sin(self.ph[i, 2] + tt * self.fr[i, 2] * 2 * np.pi * 1.6)) ** 1.5
            blit(canvas, SPRITES[self.size[i]], x, y, FLY_COL, self.base[i] * (0.25 + 0.75 * blink) * a * strength)


class Collected:
    """그물로 빨려 드는 빛 — 제자리에서 떨다가 끌려가고, 테에 닿는 순간 차갑게 한 번 번쩍이고 꺼진다."""

    def __init__(self, n, seed, region):
        r = np.random.default_rng(seed)
        x0, x1, y0, y1 = region
        self.p0 = np.stack([r.uniform(x0, x1, n) * W, r.uniform(y0, y1, n) * H], 1)
        self.start = np.sort(r.uniform(COLLECT[0], COLLECT[0] + 1.6, n))
        self.arrive = np.sort(np.linspace(COLLECT[1], COLLECT[2], n) + r.uniform(-0.08, 0.08, n))
        self.arrive = np.maximum(self.arrive, self.start + 1.0)
        self.size = r.integers(1, 4, n)
        self.swirl = r.uniform(-1, 1, n) * 60 * SCALE
        self.ph = r.uniform(0, 2 * np.pi, n)

    def draw(self, canvas, t, target, strength):
        for i in range(len(self.p0)):
            if t > self.arrive[i] + 0.35:
                continue
            born = sstep(0.0, 0.6, t - 0.3)
            if t <= self.arrive[i]:
                s = smooth((t - self.start[i]) / (self.arrive[i] - self.start[i]))
                d = target - self.p0[i]
                perp = np.array([-d[1], d[0]]) / (np.linalg.norm(d) + 1e-3)
                pos = self.p0[i] + d * (s ** 1.6) + perp * self.swirl[i] * np.sin(np.pi * s)
                pos = pos + np.array([np.sin(self.ph[i] + t * 5.0), np.cos(self.ph[i] + t * 4.1)]) * 5 * SCALE * (1 - s)
                blink = 0.7 + 0.3 * np.sin(self.ph[i] + t * 9.0)
                blit(canvas, SPRITES[self.size[i]], pos[0], pos[1], FLY_COL, 0.9 * blink * born * strength)
            else:
                k = 1 - (t - self.arrive[i]) / 0.35          # 테에 닿은 뒤 0.35초 차가운 번쩍임
                blit(canvas, SPRITES[3], target[0], target[1], COLD_COL, 0.55 * k * k * strength)


# ───────────────────────── 샷 구성 ─────────────────────────
def shot_configs():
    """방향별 카메라·마스크·실루엣 좌표. 좌표는 원화 픽셀(가로 1920×1080 / 세로 1080×1920) 기준."""
    if ORIENT == "landscape":
        e2, sp2 = (1040.0, 715.0), 245.0        # 그림2·3의 두 눈 중점과 간격 — 맞춤 컷의 기준
        e3, sp3 = (1015.5, 744.0), 148.0
        s1 = dict(cam_bg=((1.06, 960, 540), (1.40, 1060, 560)),
                  cam_fg=((1.06, 960, 560), (1.56, 1085, 625)),
                  mask_cfg=dict(cx=0.55, cy=0.45, rx=0.46, ry=0.5, inner=0.55, outer=0.98, bottom=0.18, green_w=0.55),
                  figure=(1068, 642, 150))
        s2_start = ((1.22, 1060, 600), (1.30, 1100, 625))
        z2e = 1.06
        s3_end = (1.10, 960, 525)
    else:
        e2, sp2 = (594.5, 1367.0), 196.0
        e3, sp3 = (578.5, 1307.5), 147.0
        s1 = dict(cam_bg=((1.04, 540, 960), (1.48, 528, 960)),
                  cam_fg=((1.04, 540, 980), (1.64, 528, 1050)),
                  mask_cfg=dict(cx=0.5, cy=0.42, rx=0.5, ry=0.52, inner=0.55, outer=0.98, bottom=0.25, green_w=0.55),
                  figure=(522, 950, 150))
        s2_start = ((1.24, 594, 1250), (1.32, 600, 1290))
        z2e = 1.06
        s3_end = (1.08, 540, 1010)
    # 그림2 끝 카메라는 눈 중점을 겨누되 화면 밖으로 못 나가게 잘린다 — 잘린 중심으로 그림3 시작을 역산한다.
    c2 = clamped_center(z2e, *e2)
    z3s = z2e * sp2 / sp3
    c3 = (e3[0] - (e2[0] - c2[0]) * z2e / z3s, e3[1] - (e2[1] - c2[1]) * z2e / z3s)
    s2 = dict(cam_bg=(s2_start[0], (z2e, c2[0], c2[1])), cam_fg=(s2_start[1], (z2e, c2[0], c2[1])),
              mask_cfg=dict(cx=0.5, cy=0.5, rx=0.40, ry=0.62, inner=0.62, outer=1.0, bottom=0.12, green_w=0.6))
    s3 = dict(cam_bg=((z3s, c3[0], c3[1]), s3_end),
              cam_fg=((z3s * 1.06, c3[0], c3[1] + 10), s3_end),
              mask_cfg=dict(cx=0.5, cy=0.42, rx=0.48, ry=0.55, inner=0.60, outer=1.0, bottom=0.2, green_w=0.5))
    return s1, s2, s3


def make_shot(k, cfgs):
    s1, s2, s3 = cfgs
    if k == 1:
        return Shot(1, s1["cam_bg"], s1["cam_fg"], s1["mask_cfg"], 0.0, S1_END, "vanish", VANISH1, figure=s1["figure"])
    if k == 2:
        return Shot(2, s2["cam_bg"], s2["cam_fg"], s2["mask_cfg"], S1_END, D23[1], "revive", REVIVE2)
    return Shot(3, s3["cam_bg"], s3["cam_fg"], s3["mask_cfg"], D23[0], T_TOTAL, None)


def shot_weights(t):
    w1 = 1.0 if t < S1_END else 0.0
    w3 = float(sstep(*D23, t)) if t >= D23[0] else 0.0
    w2 = (1.0 - w3) if S1_END <= t < D23[1] else 0.0
    return w1, w2, w3


# ───────────────────────── 렌더 루프 ─────────────────────────
def main():
    cfgs = shot_configs()
    shots = {}
    flies1 = Fireflies(22, 0.0, S1_END, vanish=(1.4, 6.0), seed=1)
    collected = Collected(10, 7, (0.08, 0.92, 0.25, 0.9))
    flies2 = Fireflies(16, S1_END, D23[1], birth_window=(10.9, 14.2), seed=2, region=(0.1, 0.9, 0.12, 0.8))
    flies3 = Fireflies(34, D23[0], T_TOTAL, rise=22, seed=3, region=(0.12, 0.88, 0.35, 1.0))

    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    rr = np.sqrt(((xs / W - 0.5) / 0.5) ** 2 + ((ys / H - 0.5) / 0.5) ** 2)
    vign = (1 - 0.38 * sstep(0.45, 1.25, rr))[..., None]
    grain_rng = np.random.default_rng(5)

    times = PREVIEW if PREVIEW else [f / FPS for f in range(int(round(T_TOTAL * FPS)))]
    proc = None
    out = os.path.join(OUT_DIR, f"opening_prologue_{ORIENT}.mp4")
    if not PREVIEW:
        os.makedirs(OUT_DIR, exist_ok=True)
        cmd = [FF, "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS),
               "-i", "-", "-c:v", "libx264", "-profile:v", "main", "-level", "4.0", "-pix_fmt", "yuv420p",
               "-b:v", "1600k", "-maxrate", "2000k", "-bufsize", "4000k", "-movflags", "+faststart", "-an", out]
        proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    else:
        os.makedirs(SCRATCH, exist_ok=True)

    t_start = time.time()
    for fi, t in enumerate(times):
        weights = shot_weights(t)
        # 필요한 샷만 올리고, 지난 샷은 버린다(메모리)
        for k, w in zip((1, 2, 3), weights):
            if w > 0.001 and k not in shots:
                shots[k] = make_shot(k, cfgs)
        for k in list(shots):
            if weights[k - 1] <= 0.001:
                del shots[k]

        frame = np.zeros((H, W, 3), np.float32)
        for k, w in zip((1, 2, 3), weights):
            if w <= 0.001:
                continue
            layer = shots[k].render(t)
            if k == 1:
                layer = layer * (1.0 - 0.25 * sstep(*DIM, t))
            frame += layer * w

        glow = np.zeros_like(frame)
        if weights[0] > 0:
            flies1.draw(glow, t, 0.9)
            s1 = shots[1]
            target = to_screen(s1.cam_bg, s1.u(t), s1.hoop_src)
            collected.draw(glow, t, target, 1.0)
        if weights[1] > 0:
            flies2.draw(glow, t, 0.85 * min(1.0, weights[1] * 1.5))
        if weights[2] > 0:
            flies3.draw(glow, t, 0.9 * min(1.0, weights[2] * 1.5))
        frame = frame + glow

        # 밝기 봉투 — 페이드인 / 암전 / 그림2 떠오름 / 페이드아웃
        env = float(sstep(0.0, 0.9, t))
        if t < S1_END:
            env *= 1.0 - float(sstep(*DIP1, t))
        elif t < D23[1]:
            env *= float(sstep(*S2_IN, t))
        env *= 1.0 - float(sstep(*FADE_OUT, t))

        # 타이틀 강세 — 곡의 강세(16.0)에 빛이 한 번 번진다
        hit = 0.0
        if t >= TITLE_HIT - 0.12:
            dt = t - TITLE_HIT
            hit = float(sstep(-0.12, 0.05, dt)) * float(np.exp(-max(dt, 0.0) / 0.7))
        bright = np.clip(frame - (0.5 - 0.15 * hit), 0, None)
        small = Image.fromarray((np.clip(bright, 0, 1) * 255).astype(np.uint8)).resize((W // 4, H // 4), Image.Resampling.BILINEAR)
        small = small.filter(ImageFilter.GaussianBlur(5))
        bloom = np.asarray(small.resize((W, H), Image.Resampling.BICUBIC)).astype(np.float32) / 255.0
        pulse = 0.55 + 0.12 * np.sin(t * 2 * np.pi * 0.35) + 0.65 * hit
        frame = frame * (1.0 + 0.08 * hit) + bloom * pulse

        flicker = 1.0 + 0.012 * np.sin(t * 2 * np.pi * 0.7) + 0.006 * np.sin(t * 2 * np.pi * 2.3)
        frame = frame * flicker * vign
        frame = frame + grain_rng.normal(0, 0.011, (H, W, 1)).astype(np.float32)
        frame = frame * env
        u8 = (np.clip(frame, 0, 1) * 255 + 0.5).astype(np.uint8)
        if proc:
            proc.stdin.write(u8.tobytes())
            if fi % 48 == 0:
                print(f"frame {fi}/{len(times)} {time.time() - t_start:.1f}s", flush=True)
        else:
            Image.fromarray(u8).save(os.path.join(SCRATCH, f"{ORIENT}_t{t:05.2f}.png"))
            print(f"preview t={t:.2f} {time.time() - t_start:.1f}s", flush=True)
    if proc:
        proc.stdin.close()
        proc.wait()
        print(f"render {time.time() - t_start:.1f}s -> {out} ({os.path.getsize(out) / 1e6:.2f} MB)")


if __name__ == "__main__":
    main()
