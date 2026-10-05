"""0단계 화풍 비교 샘플 — 2막 개막 「움직이는 구멍」을 두 화풍으로 렌더한다(게임에 연결하지 않는다).

    python -X utf8 Tools/Video/sample_ch7_fence.py [sil|book|all] [--preview 1.0,3.5,...] [--sheet]

산출: Artifacts/style-sample/ch7_fence_<style>.mp4 — **검토용**(자막을 굽고 소리를 섞었다).
게임에 넣을 때는 자막을 굽지 않고(게임이 IMGUI로 얹는다) 소리는 wav로 따로 뺀다(오프닝·꿈 영상과 같은 방식).

두 화풍은 **구도·시간표·움직임이 같다** — 바뀌는 건 색·윤곽·눈뿐이다. 그래야 화풍만 비교된다.

    샷1  0.0~2.6   이름 벽 — 칸마다 곤충 그림과 이름. 금빛이 왼쪽에서 오른쪽으로 훑는다
    샷2  2.0~5.4   울타리 — 말뚝마다 이름표. 안쪽은 볕 드는 풀밭, 바깥은 보랏빛 안개(그림자의 눈이 깜빡)
    샷3  4.8~8.2   구멍 — 가운데 이름표가 바래고, 앉아 있던 나비가 흩어지고, 말뚝이 쓰러진다
    샷4  7.6~11.0  그림자 — 구멍으로 미끄러져 들어와 딱정벌레에 겹친다. 벌레가 검게 바래고 그림자의 눈이 뜬다
    샷5 10.4~14.0  드러남 — 벽의 칸이 차례로 켜지는데 한 칸만 비어 있다. 그 빈칸이 옆으로 미끄러지고 눈을 뜬다
"""
import math
import os
import subprocess
import sys
import time
import wave

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from silhouette_kit import (FPS, H, W, Mask, Particles, add, blur_arr, col, crop, dot, ease_in_out,  # noqa: E402
                            ease_out, fbm, glyphs, grass, lerp, over, radial, ridge, rim, sky, smooth, span,
                            vignette, xf, zoom)

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT_DIR = os.path.join(ROOT, "Artifacts", "style-sample")
FF = os.environ.get("FFMPEG", "C:/Users/kss11/AppData/Local/Programs/Python/Python312/Scripts/ffmpeg.exe")
FONT = "C:/Windows/Fonts/malgunbd.ttf"

DISSOLVE = 0.6
FADE = 0.6
DURS = [2.6, 2.8, 2.8, 2.8, 3.0]
CUES = [
    (0.4, 4.4, "곤충 이름 하나가, 울타리 말뚝 하나."),
    (5.0, 7.8, "이름이 지워지면, 울타리에 구멍이 난다."),
    (8.0, 10.8, "그 구멍으로 그림자가 숨어들었다."),
    (11.0, 13.7, "벽이 다 차자, 숨은 한 칸이 드러났다."),
]

STYLES = {
    "sil": dict(
        label="그림자 화풍",
        sky=[(0, "#121a2c"), (0.45, "#34426a"), (0.78, "#b98a66"), (1, "#e2b088")],
        outside="#262844", fog="#4e5180",
        meadow="#162016", meadow2="#0f170f", grass="#0b120b", flower=None,
        wall="#2b2723", wall_line="#1d1a17", cell="#1b1815", plate="#2a241a",
        fence="#0e120f", rail="#0c0f0d",
        bfly="#0b0e0b", bfly2=None, beetle="#0b0e0b", beetle2=None, dfly="#0b0e0b", dfly2=None, ink="#0b0e0b",
        outline=None, glow="#ffc35a", rimlight="#ffd79a",
        shadow="#020203", shadow_glow="#7b5cff", eye="#d8ccff", blank="#040405",
        tint="#ffe9c8", vign=0.42, paper=0.0,
    ),
    "book": dict(
        label="그림책 화풍",
        sky=[(0, "#86cdfb"), (0.55, "#cdeeff"), (1, "#fff3d2")],
        outside="#6a5aa3", fog="#a497da",
        meadow="#82d35c", meadow2="#5fb845", grass="#3f9a35", flower=["#ff7aa2", "#ffd84d", "#ffffff"],
        wall="#ead6ab", wall_line="#c9b083", cell="#f7e9c6", plate="#ffe49a",
        fence="#d08c4c", rail="#b8743a",
        bfly="#ff9b3d", bfly2="#ffd84d", beetle="#e8413f", beetle2="#2b1d17", dfly="#3aa6f2", dfly2="#d7f1ff",
        ink="#3b2a1e", outline="#3b2a1e", glow="#ffd34d", rimlight="#fff3c4",
        shadow="#2c1e44", shadow_glow="#b9a2ff", eye="#ffe14d", blank="#1f1533",
        tint="#fff8ec", vign=0.16, paper=0.06,
    ),
}

ICON_KINDS = ["bfly", "beetle", "dfly"]
ROWS, COLS, CELL, GAP = 3, 7, 118, 16
WALL_X0 = (W - (COLS * (CELL + GAP) - GAP)) / 2
WALL_Y0 = (H - (ROWS * (CELL + GAP) - GAP)) / 2 - 20
BLANK = (1, 3)   # (행, 열) — 끝까지 안 켜지는 칸


def cell_center(r, c):
    return WALL_X0 + c * (CELL + GAP) + CELL / 2, WALL_Y0 + r * (CELL + GAP) + CELL / 2


def paper_tex(st, seed):
    return fbm(W, H, seed, (180, 50), (0.6, 0.4)) if st["paper"] else None


def apply_paper(f, st, paper):
    if paper is not None:
        f *= (1 + (paper[..., None] - 0.5) * st["paper"] * 2)
    return f


# ══════════════════════════ 그리기 부품 ══════════════════════════

def stamp(dst, a, x, y, color, mode="over"):
    """작은 알파 a를 (x,y) 가운데에 덮는다(또는 더한다)."""
    h, w = a.shape
    x0, y0 = int(round(x - w / 2)), int(round(y - h / 2))
    sx0, sy0 = max(0, -x0), max(0, -y0)
    x0c, y0c = max(0, x0), max(0, y0)
    x1c, y1c = min(dst.shape[1], x0 + w), min(dst.shape[0], y0 + h)
    if x1c <= x0c or y1c <= y0c:
        return
    sub = a[sy0:sy0 + (y1c - y0c), sx0:sx0 + (x1c - x0c)]
    c = col(color) if isinstance(color, str) else np.asarray(color, np.float32)
    if mode == "add":
        add(dst[y0c:y1c, x0c:x1c], c, sub)
    else:
        over(dst[y0c:y1c, x0c:x1c], c, sub)


def grow(a, r):
    """윤곽용으로 알파를 r픽셀쯤 부풀린다."""
    return np.clip(blur_arr(a, r) * 3.2, 0, 1)


def insect_parts(kind, s, rot=0.0, flap=0.0):
    """곤충 한 마리를 날개·몸·무늬·눈 알파로 나눠 굽는다(국소 상자). 머리는 -y."""
    box = int(3.4 * s) + 12
    c = box / 2
    wing, body, spot, white, pupil = (Mask(box, box) for _ in range(5))

    def P(pts):
        return xf(pts, c, c, s, rot)

    if kind == "bfly":
        k = 1 - 0.7 * flap
        for sg in (-1, 1):
            wing.ellipse(*P([(sg * 0.56 * k, -0.2)])[0], 0.62 * k * s, 0.5 * s, rot - sg * 0.45)
            wing.ellipse(*P([(sg * 0.42 * k, 0.38)])[0], 0.42 * k * s, 0.34 * s, rot + sg * 0.5)
            spot.ellipse(*P([(sg * 0.66 * k, -0.26)])[0], 0.22 * k * s, 0.17 * s, rot)
            spot.circle(*P([(sg * 0.42 * k, 0.42)])[0], 0.11 * k * s)
            body.line(P([(sg * 0.05, -0.7), (sg * 0.24, -1.08)]), 0.04 * s)
            body.circle(*P([(sg * 0.24, -1.08)])[0], 0.06 * s)
        body.line(P([(0, -0.45), (0, 0.6)]), 0.16 * s)
        head, hr = (0, -0.62), 0.16
    elif kind == "beetle":
        for sg in (-1, 1):
            for ly in (-0.22, 0.12, 0.44):
                body.line(P([(sg * 0.42, ly), (sg * 0.74, ly + 0.08), (sg * 0.82, ly + 0.3)]), 0.07 * s)
            body.line(P([(sg * 0.12, -0.8), (sg * 0.32, -1.08)]), 0.05 * s)
        wing.ellipse(*P([(0, 0.12)])[0], 0.6 * s, 0.64 * s, rot)
        for sx, sy, r in ((-0.27, -0.06, 0.12), (0.27, -0.06, 0.12), (-0.22, 0.38, 0.11), (0.22, 0.38, 0.11)):
            spot.circle(*P([(sx, sy)])[0], r * s)
        spot.line(P([(0, -0.45), (0, 0.74)]), 0.035 * s)
        head, hr = (0, -0.6), 0.25
    else:
        for sg in (-1, 1):
            for j, (wy, ln, ang) in enumerate(((-0.42, 1.15, -0.1), (-0.22, 1.0, 0.17))):
                a = ang + flap * (0.35 if j == 0 else -0.35)
                wing.ellipse(*P([(sg * ln * 0.52, wy)])[0], ln * 0.5 * s, 0.14 * s, rot + sg * a)
        body.taper(P([(0, -0.5), (0, 0.3), (0, 1.3)]), 0.16 * s, 0.07 * s)
        head, hr = (0, -0.72), 0.2
    hx, hy = P([head])[0]
    body.circle(hx, hy, hr * s)
    er = 0.5 if kind == "dfly" else 0.42
    eyes = []
    for sg in (-1, 1):
        ex, ey = P([(head[0] + sg * hr * 0.48, head[1] - hr * 0.12)])[0]
        eyes.append((ex, ey))
        white.circle(ex, ey, hr * er * s)
        px, py = P([(head[0] + sg * hr * 0.42, head[1] - hr * 0.22)])[0]
        pupil.circle(px, py, hr * er * 0.55 * s)
    return dict(wing=wing.arr(0.5), body=body.arr(0.5), spot=spot.arr(0.5), white=white.arr(0.4),
                pupil=pupil.arr(0.4), eyes=eyes, box=box, hr=hr * s)


def draw_insect(f, st, kind, x, y, s, rot=0.0, flap=0.0, alpha=1.0, drain=0.0, shadow_eyes=0.0, parts=None):
    """곤충을 화풍대로 칠한다. drain(0..1)은 이름을 빼앗겨 검게 바래는 정도, shadow_eyes는 숨어든 그림자의 눈."""
    p = parts or insect_parts(kind, s, rot, flap)
    union = np.maximum(p["wing"], p["body"]) * alpha
    dark = col("#120d18")
    if st["outline"] is None:                              # 그림자 화풍 — 검은 실루엣 + 금빛 테두리
        stamp(f, union, x, y, col(st[kind]))
        stamp(f, rim(union, -1.6, -1.6, 1.6) * 0.65 * (1 - drain), x, y, col(st["rimlight"]), "add")
    else:                                                   # 그림책 화풍 — 진한 윤곽 + 밝은 색 + 큰 눈
        stamp(f, grow(union, 1.6), x, y, lerp(col(st["outline"]), dark, drain))
        wing_c = col(st[kind + "2"]) if kind == "dfly" else col(st[kind])
        body_c = col(st[kind]) if kind == "dfly" else col(st["ink"])
        spot_c = col(st["beetle2"]) if kind == "beetle" else col(st["bfly2"])
        stamp(f, p["wing"] * alpha * (0.85 if kind == "dfly" else 1.0), x, y, lerp(wing_c, dark, drain))
        stamp(f, p["body"] * alpha, x, y, lerp(body_c, dark, drain))
        if kind != "dfly":
            stamp(f, p["spot"] * p["wing"] * alpha, x, y, lerp(spot_c, dark, drain))
        eye_a = alpha * (1 - drain)
        if eye_a > 0.01:
            stamp(f, p["white"] * eye_a, x, y, col("#ffffff"))
            stamp(f, p["pupil"] * eye_a, x, y, col("#1a1210"))
    if shadow_eyes > 0:
        c = p["box"] / 2
        for ex, ey in p["eyes"]:
            gx, gy = x + ex - c, y + ey - c
            dot(f, gx, gy, p["hr"] * 0.9, col(st["eye"]), 0.9 * shadow_eyes)
            dot(f, gx, gy, p["hr"] * 0.35, col("#ffffff"), 0.9 * shadow_eyes)


# ── 그림자(이름 없는 것) ──

_SH = {}


def _shadow_fields():
    if not _SH:
        _SH["a"] = fbm(900, 700, 711, (70, 28, 11), (0.55, 0.3, 0.15))
        _SH["b"] = fbm(900, 700, 712, (90, 34, 13), (0.55, 0.3, 0.15))
    return _SH["a"], _SH["b"]


def shadow_alpha(t, w, h):
    n1, n2 = _shadow_fields()
    a = crop(n1, 200 + 120 * math.sin(t * 0.8), 160 + 90 * math.sin(t * 1.1 + 1.0), w, h)
    b = crop(n2, 200 + 110 * math.sin(t * 0.7 + 2.0), 160 + 100 * math.sin(t * 0.9), w, h)
    n = 0.5 * (a + b)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    env = 1 - ((xx - w / 2) / (w * 0.44)) ** 2 - ((yy - h * 0.56) / (h * 0.42)) ** 2
    v = env * 0.8 + (n - 0.5) * 1.3 + 0.12
    return np.clip((v - 0.3) / 0.2, 0, 1) ** 1.2


def draw_eyes(f, st, x, y, scale, amount, blink=1.0, spread=26):
    """그림자의 두 눈 — 그림자 화풍은 빛 점, 그림책 화풍은 노란 눈 + 세로 동공."""
    for sg in (-1, 1):
        ex, ey = x + sg * spread * scale, y - 6 * scale
        if st["outline"] is None:
            dot(f, ex, ey, 9 * scale, col(st["eye"]), 0.85 * amount)
        else:
            m = Mask(int(60 * scale) + 6, int(50 * scale) + 6)
            m.ellipse(m.w / 2, m.h / 2, 13 * scale, max(0.5, 11 * scale * blink))
            ea = m.arr(0.6) * amount
            stamp(f, ea, ex, ey, col(st["eye"]))
            pm = Mask(m.w, m.h)
            pm.ellipse(pm.w / 2 + 2 * scale, pm.h / 2, 3.2 * scale, max(0.5, 9 * scale * blink))
            stamp(f, pm.arr(0.5) * amount, ex, ey, col("#1a1020"))


def draw_shadow(f, st, x, y, scale, t, alpha=1.0, eyes=1.0):
    w, h = int(230 * scale) + 8, int(190 * scale) + 8
    if w < 12 or h < 12 or alpha <= 0.01:
        return
    a = shadow_alpha(t, w, h) * alpha
    stamp(f, blur_arr(a, 14 * scale) * 0.55, x, y, col(st["shadow_glow"]), "add")
    stamp(f, a, x, y, col(st["shadow"]))
    if st["outline"] is not None:
        stamp(f, rim(a, 2, 2, 3) * 0.6, x, y, col(st["shadow_glow"]), "add")
    if eyes > 0:
        blink = 1.0 if (t % 2.3) > 0.12 else 0.15
        draw_eyes(f, st, x, y, scale, eyes * alpha, blink)


# ── 울타리 ──

def post_pts(x, base, w, h):
    return [(x - w / 2, base), (x - w / 2, base - h + w * 0.55), (x, base - h), (x + w / 2, base - h + w * 0.55),
            (x + w / 2, base)]


def post_shape(m, x, base, w, h):
    m.poly(post_pts(x, base, w, h))
    return m


def rotate_pts(pts, cx, cy, a, dy=0.0):
    cr, sr = math.cos(a), math.sin(a)
    return [(cx + (px - cx) * cr - (py + dy - cy) * sr, cy + (px - cx) * sr + (py + dy - cy) * cr) for px, py in pts]


def rect_pts(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def plate_rect(x, base, w, h):
    pw, ph = w * 1.05, w * 0.92
    py = base - h * 0.56
    return x - pw / 2, py - ph / 2, x + pw / 2, py + ph / 2


def finish_fence(f, st, pa, ra):
    if st["outline"] is not None:
        over(f, col(st["outline"]), grow(np.maximum(pa, ra), 1.6))
    over(f, col(st["rail"]), ra)
    over(f, col(st["fence"]), pa)
    if st["outline"] is None:
        add(f, col(st["rimlight"]), rim(pa, -2, -1, 2) * 0.35)
    else:
        add(f, col("#ffffff"), rim(pa, 3, 0, 3) * 0.18)


def paint_fence(f, st, posts, base, w, h):
    pm, rm = Mask(), Mask()
    for i, x in enumerate(posts):
        post_shape(pm, x, base, w, h)
        if i + 1 < len(posts):
            for ry in (0.3, 0.74):
                rm.rect(x, base - h * ry - w * 0.13, posts[i + 1], base - h * ry + w * 0.13)
    finish_fence(f, st, pm.arr(0.6), rm.arr(0.6))


def paint_plate(f, st, x, base, w, h, kind, level=1.0, icon_alpha=1.0):
    """말뚝 이름표 — 작은 곤충 그림이 이름이다. level은 빛(1=살아 있는 이름, 0=바랜 이름)."""
    x0, y0, x1, y1 = plate_rect(x, base, w, h)
    pcx, pcy = (x0 + x1) / 2, (y0 + y1) / 2
    m = Mask(int(x1 - x0) + 16, int(y1 - y0) + 16)
    m.rect(8, 8, m.w - 8, m.h - 8, r=6)
    a = m.arr(0.5)
    if level > 0:
        stamp(f, blur_arr(a, 10) * 0.9 * level, pcx, pcy, col(st["glow"]), "add")
    if st["outline"] is not None:
        stamp(f, grow(a, 1.4), pcx, pcy, col(st["outline"]))
        stamp(f, a, pcx, pcy, lerp(col("#bfb5a6"), col(st["plate"]), 0.25 + 0.75 * level))
    else:
        stamp(f, a, pcx, pcy, col(st["plate"]))
        stamp(f, rim(a, -1, -1, 1.5) * 0.5 * level, pcx, pcy, col(st["glow"]), "add")
    if icon_alpha > 0.01:
        s = (x1 - x0) * 0.3
        if st["outline"] is None:
            p = insect_parts(kind, s)
            ia = np.maximum(p["wing"], p["body"]) * icon_alpha
            stamp(f, ia * 0.85, pcx, pcy, col("#0d0b09"))
            stamp(f, (blur_arr(ia, 4) * 0.8 + ia * 0.6) * level, pcx, pcy, col(st["glow"]), "add")
        else:
            draw_insect(f, st, kind, pcx, pcy, s, alpha=icon_alpha, drain=(1 - level) * 0.85)


# ── 장면 배경 ──

def field_bg(st, horizon, seed):
    """바깥 안개 땅(뒤) — 하늘. 울타리와 풀밭은 그 위에 얹는다."""
    img = sky(W, H, st["sky"], seed=seed, texture=0.03)
    far = ridge(Mask(), seed + 1, horizon - 70, 34, freq=(0.003, 0.009, 0.02)).arr(1.5)
    over(img, col(st["outside"]), far)
    yy = np.arange(H, dtype=np.float32)[:, None]
    fogb = np.clip(1 - np.abs(yy - (horizon - 30)) / 120, 0, 1) ** 1.5 * np.ones((1, W), np.float32)
    n = fbm(W, H, seed + 2, (240, 90), (0.7, 0.3))
    over(img, col(st["fog"]), fogb * (0.35 + 0.5 * n))
    return img


def meadow(img, st, y, seed, flowers=60):
    gm = ridge(Mask(), seed, y, 10, freq=(0.003, 0.008)).arr(1.0)
    yy = np.linspace(0, 1, H, dtype=np.float32)[:, None, None]
    grad = col(st["meadow"]) * (1 - yy) + col(st["meadow2"]) * yy
    over(img, grad, gm)
    gl = grass(Mask(), seed + 3, y + 6, H + 20, 260, height=(14, 46), width=(1.6, 3.6)).arr(0.5)
    over(img, col(st["grass"]), gl * gm)
    if st["flower"]:
        r = np.random.default_rng(seed + 9)
        for _ in range(flowers):
            fx, fy = r.uniform(0, W), r.uniform(y + 20, H)
            fc = st["flower"][r.integers(0, len(st["flower"]))]
            sc = 0.6 + 0.8 * (fy - y) / (H - y)
            m = Mask(40, 40)
            for k in range(5):
                a = 2 * math.pi * k / 5
                m.circle(20 + math.cos(a) * 6 * sc, 20 + math.sin(a) * 6 * sc, 4.6 * sc)
            stamp(img, m.arr(0.4), fx, fy, col(fc))
            stamp(img, Mask(12, 12).circle(6, 6, 2.4 * sc).arr(0.3), fx, fy, col("#ffcc33"))
    return gm


def wall_bg(st, seed):
    n = fbm(W, H, seed, (200, 70, 20), (0.5, 0.35, 0.15))
    img = np.ones((H, W, 3), np.float32) * col(st["wall"])
    img *= (0.86 + 0.28 * n)[..., None]
    lm = Mask()
    for row in range(-1, 12):
        y = row * 74 + 20
        lm.line([(0, y), (W, y)], 2.4)
        off = (row % 2) * 70
        for x in range(-140 + off, W + 140, 140):
            lm.line([(x, y), (x, y + 74)], 2.4)
    over(img, col(st["wall_line"]), lm.arr(0.8) * 0.55)
    cm = Mask()
    for r in range(ROWS):
        for c in range(COLS):
            cx, cy = cell_center(r, c)
            cm.rect(cx - CELL / 2, cy - CELL / 2, cx + CELL / 2, cy + CELL / 2, r=10)
    ca = cm.arr(0.6)
    if st["outline"] is not None:
        over(img, col(st["outline"]), grow(ca, 1.5) * 0.85)
        over(img, col(st["cell"]), ca)
    else:
        img *= (1 - ca * 0.45)[..., None]
        add(img, col("#6a5a46"), rim(ca, 2, 2, 1.5) * 0.4)
    gm = Mask()
    for r in range(ROWS):
        for c in range(COLS):
            cx, cy = cell_center(r, c)
            glyphs(gm, cx - 34, cy + 30, 70, 1, 900 + r * 10 + c, size=11, weight=2.2)
    return img, gm.arr(0.4)


# ══════════════════════════ 샷 ══════════════════════════

def wall_shot(st, mode):
    """mode='sweep'(샷1 — 금빛이 훑는다) 또는 'reveal'(샷5 — 칸이 차례로 켜지고 빈칸이 움직인다)."""
    img, glyph_a = wall_bg(st, 401)
    icons = {}
    for r in range(ROWS):
        for c in range(COLS):
            kind = ICON_KINDS[(r * COLS + c) % 3]
            p = insect_parts(kind, 30, rot=0.12 * math.sin(r * 3 + c))
            ia = np.maximum(p["wing"], p["body"])
            glow = (blur_arr(ia, 5) * 0.9 + ia * 0.7) if st["outline"] is None else blur_arr(ia, 12) * 0.7
            icons[(r, c)] = (kind, p, ia, glow)
    order = sorted(icons, key=lambda rc: (rc[1], rc[0]))
    rank = {rc: i for i, rc in enumerate(order)}
    paper = paper_tex(st, 77)
    dust = Particles(50, 402, (0, 0, W, H), vel=((-3, 3), (-8, -2)), size=(0.6, 1.6))
    bx, by = cell_center(*BLANK)
    blank_m = Mask(CELL + 40, CELL + 40)
    blank_m.rect(20, 20, CELL + 20, CELL + 20, r=10)
    blank_a = blank_m.arr(1.2)
    blank_glow = blur_arr(blank_a, 18) * 0.7

    def level_at(u, rc):
        if mode == "sweep":
            cx, _ = cell_center(*rc)
            sx = lerp(-200, W + 200, span(u, 0.2, 2.4))
            return 0.35 + 0.65 * math.exp(-((cx - sx) / 170) ** 2) + 0.25 * smooth((sx - cx) / 150)
        i = rank[rc]
        return 0.2 + 0.8 * ease_out(span(u, 0.05 + 1.2 * i / len(order), 0.35 + 1.2 * i / len(order)))

    def frame(u):
        f = img.copy()
        lit = np.zeros((H, W), np.float32)
        for rc, (kind, p, ia, glow) in icons.items():
            cx, cy = cell_center(*rc)
            lv = level_at(u, rc)
            lit[int(cy - CELL / 2):int(cy + CELL / 2), int(cx - CELL / 2):int(cx + CELL / 2)] = lv
            if st["outline"] is None:
                stamp(f, ia * 0.75, cx, cy - 8, col("#0d0b09"))
                stamp(f, glow * lv, cx, cy - 8, col(st["glow"]), "add")
            else:
                stamp(f, glow * lv, cx, cy - 8, col(st["glow"]), "add")
                draw_insect(f, st, kind, cx, cy - 8, 30, alpha=0.35 + 0.65 * min(1.0, lv), parts=p)
        if st["outline"] is None:
            f *= (1 - glyph_a * 0.6)[..., None]
            add(f, col(st["glow"]), glyph_a * lit * 0.9)
        else:
            over(f, col(st["ink"]), glyph_a * (0.35 + 0.5 * np.clip(lit, 0, 1)))
            add(f, col(st["glow"]), blur_arr(glyph_a, 3) * lit * 0.5)
        if mode == "reveal":
            slide = ease_in_out(span(u, 1.6, 2.4))
            wob = math.sin(u * 9) * 2.0 * span(u, 1.6, 2.4) * (1 - slide)
            x = bx + slide * (CELL + GAP) + wob
            stamp(f, blank_glow, x, by, col(st["shadow_glow"]), "add")
            stamp(f, blank_a, x, by, col(st["blank"]))
            eo = ease_out(span(u, 2.4, 2.85))
            if eo > 0:
                draw_eyes(f, st, x, by, 0.85, 1.0, blink=eo)
        dust.draw(f, u, col(st["rimlight"]), 0.35)
        apply_paper(f, st, paper)
        if mode == "sweep":
            return zoom(f, 1.1 - 0.1 * ease_in_out(u / 2.6), W * 0.5, H * 0.48)
        zc = ease_in_out(span(u, 0.0, 3.0))
        return zoom(f, 1.0 + 0.22 * zc * zc, bx + (CELL + GAP) * 0.5, by)

    return frame


def fence_shot(st):
    """샷2 — 울타리 전경. 안쪽 풀밭엔 곤충, 바깥 안개엔 그림자의 눈."""
    horizon = H * 0.56
    img = field_bg(st, horizon, 511)
    base, ph, pw = H * 0.64, 200, 34
    posts = [float(x) for x in np.arange(-30, W + 60, 116)]
    paint_fence(img, st, posts, base, pw, ph)
    for i, x in enumerate(posts):
        paint_plate(img, st, x, base, pw, ph, ICON_KINDS[i % 3])
    meadow(img, st, base - 6, 512)
    paper = paper_tex(st, 78)
    motes = Particles(40, 513, (0, H * 0.4, W, H), vel=((-4, 4), (-10, -3)), size=(0.8, 2.0))

    def frame(u):
        f = img.copy()
        draw_shadow(f, st, W * 0.76, horizon - 42, 0.32, u + 3.0, alpha=0.75, eyes=1.0)
        # 나는 곤충은 하늘·안개 앞에 둔다 — 그림자 화풍에선 어두운 풀밭 위 검은 몸이 안 보인다
        bxp = lerp(W * 0.16, W * 0.42, u / 2.8)
        draw_insect(f, st, "bfly", bxp, H * 0.33 + math.sin(u * 3.1) * 16, 44, rot=0.5 + 0.15 * math.sin(u * 2),
                    flap=0.5 + 0.5 * math.sin(u * 14))
        draw_insect(f, st, "dfly", lerp(W * 0.9, W * 0.62, ease_in_out(u / 2.8)), H * 0.24 + math.sin(u * 2.2) * 8,
                    50, rot=-1.45, flap=0.5 + 0.5 * math.sin(u * 30))
        draw_insect(f, st, "beetle", W * 0.33 + 18 * u, H * 0.9, 26, rot=1.4)
        motes.draw(f, u, col(st["rimlight"]), 0.4)
        apply_paper(f, st, paper)
        return zoom(f, 1.0 + 0.04 * u / 2.8, W * 0.5, H * 0.6)

    return frame


GAP_BASE, GAP_PH, GAP_PW = H * 0.9, 470, 72
GAP_POSTS = [W * 0.2, W * 0.5, W * 0.8]
GAP_KINDS = ["beetle", "bfly", "dfly"]


def gap_scene(st, fallen):
    """샷3·4의 정적 배경 — 말뚝 셋을 가까이. 서 있을 땐 가운데 이름표를 비워 둔다(샷이 매 프레임 그린다)."""
    img = field_bg(st, H * 0.5, 611)
    base, ph, pw, posts = GAP_BASE, GAP_PH, GAP_PW, GAP_POSTS
    if fallen:
        add(img, col(st["shadow_glow"]), radial(W, H, W * 0.5, H * 0.52, 260, 2.0) * 0.28)
        pm, rm = Mask(), Mask()
        for x in (posts[0], posts[2]):
            post_shape(pm, x, base, pw, ph)
        for ry in (0.3, 0.74):
            y = base - ph * ry
            rm.poly([(posts[0], y - pw * 0.13), (posts[0] + 120, y - pw * 0.13 + 6), (posts[0] + 104, y + pw * 0.13),
                     (posts[0], y + pw * 0.13)])
            rm.poly([(posts[2], y - pw * 0.13), (posts[2] - 96, y - pw * 0.13 - 4), (posts[2] - 116, y + pw * 0.13),
                     (posts[2], y + pw * 0.13)])
        pm.poly([(W * 0.5 - pw / 2, base), (W * 0.5 - pw / 2, base - 46), (W * 0.5 - 6, base - 30),
                 (W * 0.5 + 8, base - 52), (W * 0.5 + pw / 2, base - 38), (W * 0.5 + pw / 2, base)])  # 부러진 밑동
        finish_fence(img, st, pm.arr(0.6), rm.arr(0.6))
        plates = [0, 2]
    else:
        paint_fence(img, st, posts, base, pw, ph)
        plates = [0, 2]
    for i in plates:
        paint_plate(img, st, posts[i], base, pw, ph, GAP_KINDS[i])
    meadow(img, st, base - 4, 612, flowers=24)
    return img


def hole_shot(st):
    """샷3 — 가운데 이름표가 바래고, 나비가 흩어지고, 말뚝이 쓰러진다."""
    img = gap_scene(st, fallen=False)
    gap_img = gap_scene(st, fallen=True)
    base, ph, pw, cx = GAP_BASE, GAP_PH, GAP_PW, GAP_POSTS[1]
    paper = paper_tex(st, 79)
    sparks = np.random.default_rng(631).uniform(-1, 1, (40, 3))
    bfly_parts = [insect_parts("bfly", 46, 0.0, fl) for fl in np.linspace(0, 1, 6)]
    x0, y0, x1, y1 = plate_rect(cx, base, pw, ph)
    pcx, pcy = (x0 + x1) / 2, (y0 + y1) / 2
    crack = Mask(90, 30).line([(10, 15), (34, 9), (52, 18), (80, 11)], 3).arr(0.5)

    left_end, right_end = GAP_POSTS[0] + 112, GAP_POSTS[2] - 106   # 부러진 그루터기 끝(gap_scene과 맞춘다)

    def frame(u):
        fall = ease_in_out(span(u, 1.4, 2.25))
        lv = 1 - smooth(span(u, 0.3, 1.1))
        if fall > 0:
            # 말뚝은 바랜 이름표를 단 채 쓰러지고, 안쪽 가로대 토막은 제자리에서 기울며 떨어진다
            # (그냥 사라지면 튀고, 말뚝에 붙여 돌리면 큰 X자가 된다)
            f = gap_img.copy()
            ang, dy, px, py = fall * 1.25, fall * 40, cx + pw / 2, base
            fade = 1 - span(u, 2.0, 2.4)
            pm, rm, plm = Mask(), Mask(), Mask()
            pm.poly(rotate_pts(post_pts(cx, base, pw, ph), px, py, ang, dy))
            drop = 330 * fall * fall
            for ry in (0.3, 0.74):
                y = base - ph * ry
                for x0, x1, sg in ((left_end, cx - pw / 2, 1), (cx + pw / 2, right_end, -1)):
                    rm.poly(rotate_pts(rect_pts(x0, y - pw * 0.13, x1, y + pw * 0.13), (x0 + x1) / 2, y,
                                       sg * 0.45 * fall, drop))
            plm.poly(rotate_pts(rect_pts(*plate_rect(cx, base, pw, ph)), px, py, ang, dy))
            pa, ra, la = pm.arr(0.6) * fade, rm.arr(0.6) * fade, plm.arr(0.6) * fade
            if st["outline"] is not None:
                over(f, col(st["outline"]), grow(np.maximum(np.maximum(pa, ra), la), 1.6))
            over(f, col(st["rail"]), ra)
            over(f, col(st["fence"]), pa)
            over(f, col("#bfb5a6") if st["outline"] is not None else col(st["plate"]), la)
        else:
            f = img.copy()
            paint_plate(f, st, cx, base, pw, ph, "bfly", level=lv, icon_alpha=1 - smooth(span(u, 0.6, 1.2)))
            if u > 1.1:
                stamp(f, crack * span(u, 1.1, 1.3), pcx, pcy + 70, col("#1a1210"))
        ba = 1 - smooth(span(u, 0.8, 1.5))
        if ba > 0.01:
            fl = 0.5 + 0.5 * math.sin(u * 5)
            draw_insect(f, st, "bfly", cx, base - ph - 34, 46, parts=bfly_parts[int(fl * 5)], alpha=ba)
        if 0.8 < u < 2.4:
            k = span(u, 0.8, 2.4)
            for sx, sy, sr in sparks:
                dot(f, cx + sx * 60 + sx * 80 * k, base - ph - 34 - 120 * k * (0.5 + abs(sy)) + sy * 20, 2.5 + sr,
                    col(st["glow"]), 0.8 * (1 - k))
        apply_paper(f, st, paper)
        shake = 6 * span(u, 2.15, 2.25) * (1 - span(u, 2.25, 2.6))
        return zoom(f, 1.03 + 0.03 * u / 2.8, W * 0.5 + shake * math.sin(u * 60), H * 0.6)

    return frame


def shadow_shot(st):
    """샷4 — 그림자가 구멍으로 들어와 딱정벌레에 겹친다. 벌레가 검게 바래고 그림자의 눈이 뜬다."""
    img = gap_scene(st, fallen=True)
    leaf = Mask()
    leaf.ellipse(W * 0.66, H * 0.78, 120, 34, -0.12)   # 자막 줄(아래 118px) 위에 둔다
    la = leaf.arr(0.8)
    if st["outline"] is not None:
        over(img, col(st["outline"]), grow(la, 1.6))
        over(img, col("#4fbf3c"), la)
    else:
        over(img, col("#0a100a"), la)
    paper = paper_tex(st, 80)
    bx, by = W * 0.66, H * 0.72
    beetle_p = insect_parts("beetle", 52, rot=-0.3)

    def frame(u):
        f = img.copy()
        slide = ease_in_out(span(u, 0.0, 1.6))
        sx, sy = lerp(W * 0.5, bx, slide), lerp(H * 0.55, by, slide)
        env = span(u, 1.6, 2.4)
        sc = lerp(0.45, 1.0, slide) * (1 - 0.75 * ease_in_out(env))
        drain = smooth(span(u, 1.7, 2.4))
        draw_insect(f, st, "beetle", bx, by, 52, rot=-0.3, drain=drain, parts=beetle_p,
                    shadow_eyes=ease_out(span(u, 2.2, 2.7)))
        draw_shadow(f, st, sx, sy, sc, u, alpha=1 - smooth(span(u, 2.0, 2.45)), eyes=1 - env)
        apply_paper(f, st, paper)
        return zoom(f, 1.0 + 0.16 * ease_in_out(span(u, 0.8, 2.8)), lerp(W * 0.5, bx, 0.8), lerp(H * 0.6, by, 0.7))

    return frame


SHOTS = [lambda st: wall_shot(st, "sweep"), fence_shot, hole_shot, shadow_shot, lambda st: wall_shot(st, "reveal")]


# ══════════════════════════ 편성·자막 ══════════════════════════

def timeline():
    out, acc = [], 0.0
    for k, d in enumerate(DURS):
        out.append((acc - (DISSOLVE if k else 0.0), acc + d))
        acc += d
    return out, acc


def cue_layers():
    font = ImageFont.truetype(FONT, 36)
    layers = []
    for a, b, text in CUES:
        img = Image.new("RGBA", (W, 90), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        tw = d.textlength(text, font=font)
        d.text(((W - tw) / 2, 20), text, font=font, fill=(255, 255, 255, 255), stroke_width=4,
               stroke_fill=(20, 14, 10, 255))
        arr = np.asarray(img, np.float32) / 255.0
        layers.append((a, b, arr[..., :3].copy(), arr[..., 3].copy()))
    return layers


def make_renderer(style):
    st = STYLES[style]
    spans, total = timeline()
    shots = [None] * len(SHOTS)
    vign = vignette(strength=st["vign"])
    cues = cue_layers()

    def shot(k):
        if shots[k] is None:
            shots[k] = SHOTS[k](st)
        return shots[k]

    def frame_at(T, subtitles=True):
        f = None
        for k, (s, e) in enumerate(spans):
            if not (s <= T < e or (k == len(spans) - 1 and T >= s)):
                continue
            fr = shot(k)(T - s)
            if f is None:
                f = fr
            else:
                a = smooth((T - s) / DISSOLVE)
                f = f * (1 - a) + fr * a
        for k in range(len(shots)):
            if shots[k] is not None and spans[k][1] < T - 0.1:
                shots[k] = None
        f = np.clip(f * vign * (0.94 + 0.06 * col(st["tint"])), 0, 1)
        if T > total - FADE:
            f = f * (1 - smooth((T - (total - FADE)) / FADE))
        if T < 0.25:
            f = f * smooth(T / 0.25)
        if subtitles:
            for a, b, rgb, al in cues:
                if a <= T <= b:
                    k = min(smooth((T - a) / 0.25), smooth((b - T) / 0.25))
                    y0 = H - 118
                    over(f[y0:y0 + 90], rgb, al * k)
        return f

    return frame_at, total


# ══════════════════════════ 소리 ══════════════════════════

SR = 44100


def synth_audio(total, path):
    """사건 시각에 맞춘 소리 — 따뜻한 화음 → 어두운 화음, 종소리·부서짐·바람·쿵. numpy만 쓴다."""
    n = int(SR * total)
    t = np.arange(n) / SR
    out = np.zeros((n, 2), np.float64)
    rng = np.random.default_rng(5)

    def env(points):
        xs, ys = zip(*points)
        return np.interp(t, xs, ys)

    def tone(freq, amp_env, pan=0.0, detune=0.0):
        sig = sum(w * np.sin(2 * np.pi * freq * k * (1 + detune) * t + rng.uniform(0, 6.28))
                  for k, w in ((1, 1.0), (2, 0.25), (3, 0.08)))
        sig = sig * amp_env
        out[:, 0] += sig * (1 - pan) * 0.5
        out[:, 1] += sig * (1 + pan) * 0.5

    def chime(at, freq, amp=0.18, dur=1.6, pan=0.0):
        i0 = int(at * SR)
        m = min(n - i0, int(dur * SR))
        if m <= 0:
            return
        tt = np.arange(m) / SR
        sig = sum(w * np.sin(2 * np.pi * freq * r * tt) * np.exp(-tt * d)
                  for r, w, d in ((1, 1.0, 3.0), (2.76, 0.4, 5.0), (5.4, 0.2, 8.0)))
        sig *= np.minimum(1, tt / 0.004) * amp
        out[i0:i0 + m, 0] += sig * (1 - pan) * 0.5
        out[i0:i0 + m, 1] += sig * (1 + pan) * 0.5

    def noise_burst(at, dur, lo, hi, amp, decay=8.0):
        i0 = int(at * SR)
        m = min(n - i0, int(dur * SR))
        if m <= 0:
            return
        X = np.fft.rfft(rng.normal(0, 1, m))
        fr = np.fft.rfftfreq(m, 1 / SR)
        X *= (fr > lo) & (fr < hi)
        y = np.fft.irfft(X, m)
        y = y / (np.std(y) + 1e-9) * np.exp(-np.arange(m) / SR * decay) * amp
        out[i0:i0 + m] += y[:, None] * 0.5

    def boom(at, f0, f1, dur, amp):
        i0 = int(at * SR)
        m = min(n - i0, int(dur * SR))
        if m <= 0:
            return
        tt = np.arange(m) / SR
        fr = f0 * (f1 / f0) ** (tt / dur)
        y = np.sin(2 * np.pi * np.cumsum(fr) / SR) * np.exp(-tt * 4.0) * np.minimum(1, tt / 0.01) * amp
        out[i0:i0 + m] += y[:, None] * 0.5

    # 화음 — 0~5초 따뜻하게(C장조), 5초부터 어둡게(C단조)
    warm = env([(0, 0), (0.6, 1), (4.6, 1), (5.6, 0), (total, 0)])
    dark = env([(0, 0), (4.8, 0), (6.0, 0.85), (10.4, 0.85), (11.0, 0.5), (12.6, 0.8), (total - 0.4, 0), (total, 0)])
    for fq, a in ((130.81, 0.07), (261.63, 0.05), (329.63, 0.04), (392.0, 0.035)):
        tone(fq, warm * a, pan=rng.uniform(-0.3, 0.3))
    for fq, a in ((130.81, 0.07), (155.56, 0.045), (196.0, 0.04), (65.41, 0.08)):
        tone(fq, dark * a, pan=rng.uniform(-0.3, 0.3), detune=0.002)
    # 샷1 — 금빛이 훑을 때 종소리
    for k, fq in enumerate((523.25, 659.25, 783.99, 1046.5, 783.99)):
        chime(0.5 + k * 0.42, fq, 0.12, pan=-0.6 + k * 0.3)
    # 샷2 — 바깥 그림자의 눈이 깜빡일 때 낮은 울림
    boom(3.2, 70, 55, 1.2, 0.18)
    # 샷3 — 이름표가 바랜다(내려가는 종) · 나비가 흩어진다 · 금이 간다 · 말뚝이 쓰러진다
    for k, fq in enumerate((880, 698.46, 587.33, 440)):
        chime(5.1 + k * 0.22, fq, 0.1, dur=1.0, pan=0.2)
    noise_burst(5.9, 0.25, 1500, 6000, 0.25, decay=14)
    noise_burst(6.3, 0.7, 200, 1200, 0.18, decay=4)
    boom(6.95, 110, 45, 0.9, 0.55)
    noise_burst(6.95, 0.5, 80, 900, 0.3, decay=7)
    # 샷4 — 그림자가 미끄러져 들어온다(바람) · 벌레에 겹친다(꿀꺽) · 눈(불협 종)
    sweep = env([(0, 0), (7.6, 0), (8.4, 0.3), (9.2, 0.16), (9.6, 0), (total, 0)])
    i0, i1 = int(7.6 * SR), int(9.7 * SR)
    X = np.fft.rfft(rng.normal(0, 1, i1 - i0))
    fr = np.fft.rfftfreq(i1 - i0, 1 / SR)
    X *= np.exp(-((np.log(fr + 1) - np.log(600)) / 0.8) ** 2)
    w = np.fft.irfft(X, i1 - i0)
    w = w / (np.std(w) + 1e-9)
    out[i0:i1, 0] += w * sweep[i0:i1] * 0.5
    out[i0:i1, 1] += w[::-1] * sweep[i0:i1] * 0.5
    boom(9.55, 160, 50, 0.6, 0.35)
    chime(9.85, 932.33, 0.09, dur=1.4, pan=-0.4)
    chime(9.86, 987.77, 0.09, dur=1.4, pan=0.4)
    # 샷5 — 칸이 차례로 켜진다(오르는 종) · 빈칸이 미끄러진다 · 눈을 뜬다
    scale = (523.25, 587.33, 659.25, 783.99, 880.0, 1046.5, 1174.66, 1318.51)
    for k in range(14):
        chime(10.5 + k * 0.09, scale[k % 8] * (2 if k >= 8 else 1), 0.06, dur=0.9, pan=-0.7 + k * 0.1)
    noise_burst(12.0, 0.8, 120, 700, 0.16, decay=2.5)
    boom(12.8, 90, 38, 1.2, 0.6)
    chime(12.85, 466.16, 0.1, dur=1.2, pan=-0.3)
    chime(12.86, 493.88, 0.1, dur=1.2, pan=0.3)
    # 잔향 — 지수 감쇠 잡음을 주파수 영역에서 곱한다
    ir_n = int(SR * 1.6)
    ir = rng.normal(0, 1, ir_n) * np.exp(-np.arange(ir_n) / SR * 4.2)
    ir[0] = 0
    for ch in range(2):
        L = n + ir_n
        wet = np.fft.irfft(np.fft.rfft(out[:, ch], L) * np.fft.rfft(ir * (0.9 + 0.2 * ch), L), L)[:n]
        out[:, ch] += wet / (np.max(np.abs(wet)) + 1e-9) * np.max(np.abs(out[:, ch])) * 0.35
    out *= np.clip((total - t) / 0.6, 0, 1)[:, None]
    out *= 0.89 / (np.max(np.abs(out)) + 1e-9)
    with wave.open(path, "wb") as wf:
        wf.setnchannels(2)
        wf.setsampwidth(2)
        wf.setframerate(SR)
        wf.writeframes((out * 32767).astype(np.int16).tobytes())


# ══════════════════════════ 실행 ══════════════════════════

def render(style, preview=None, sheet=False):
    os.makedirs(OUT_DIR, exist_ok=True)
    frame_at, total = make_renderer(style)
    if preview is not None:
        imgs = []
        for T in preview:
            img = Image.fromarray((np.clip(frame_at(T), 0, 1) * 255 + 0.5).astype(np.uint8), "RGB")
            imgs.append(img)
            if not sheet:
                p = os.path.join(OUT_DIR, f"{style}-{T:05.2f}.png")
                img.save(p)
                print("preview", p)
        if sheet:
            cols = 3
            rows = (len(imgs) + cols - 1) // cols
            tw, th = W // 2, H // 2
            sh = Image.new("RGB", (tw * cols, th * rows), (0, 0, 0))
            for i, img in enumerate(imgs):
                sh.paste(img.resize((tw, th), Image.Resampling.LANCZOS), ((i % cols) * tw, (i // cols) * th))
            p = os.path.join(OUT_DIR, f"{style}-sheet.png")
            sh.save(p)
            print("sheet", p)
        return
    wav = os.path.join(OUT_DIR, "ch7_fence.wav")
    if not os.path.exists(wav) or os.path.getmtime(wav) < os.path.getmtime(__file__):
        synth_audio(total, wav)
    path = os.path.join(OUT_DIR, f"ch7_fence_{style}.mp4")
    cmd = [FF, "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS),
           "-i", "-", "-i", wav, "-vf", "noise=alls=4:allf=t,setsar=1", "-c:v", "libx264", "-profile:v", "main",
           "-pix_fmt", "yuv420p", "-b:v", "2400k", "-c:a", "aac", "-strict", "-2", "-b:a", "160k", "-shortest",
           "-movflags", "+faststart", path]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    n = int(round(total * FPS))
    t0 = time.time()
    for i in range(n):
        proc.stdin.write((np.clip(frame_at(i / FPS), 0, 1) * 255 + 0.5).astype(np.uint8).tobytes())
    proc.stdin.close()
    rc = proc.wait()
    print(f"{os.path.basename(path)} {total:.1f}s {n}f {os.path.getsize(path) / 1e6:.2f}MB rc={rc} ({time.time() - t0:.0f}s)")


def main():
    args = list(sys.argv[1:])
    preview = None
    sheet = "--sheet" in args
    if sheet:
        args.remove("--sheet")
    if "--preview" in args:
        i = args.index("--preview")
        preview = [float(v) for v in args[i + 1].split(",")]
        del args[i:i + 2]
    which = args[0] if args else "all"
    for style in (STYLES if which == "all" else which.split(",")):
        render(style, preview, sheet)


if __name__ == "__main__":
    main()
