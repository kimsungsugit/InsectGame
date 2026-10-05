"""그림책 화풍 부품 — 스토리 영상 15편(render.py)이 함께 쓰는 그리기 도구.

화풍(0단계에서 고른 것, Tools/Video/sample_ch7_fence.py의 `book`): **진한 갈색 윤곽 + 밝은 단색 + 큰 눈의 곤충**,
종이 결, 약한 비네트. 그림자(이름 잃은 것)는 보랏빛 일렁이는 덩어리에 노란 눈 두 개.

규칙(Docs/StoryVideos.md §2와 같다)
- 사람은 **뒷모습·옆모습**만 — 얼굴을 정면으로 그리지 않는다. 손은 둥근 장갑 모양.
- 글자를 굽지 않는다 — 이름·장부는 판독 불가 무늬(`glyphs`·`scribble`)뿐이다. 자막은 게임이 얹는다.
- 세로 화면은 16:9를 가운데 잘라(cover) 튼다 — **보이는 건 가운데 약 32%(x 437~843)다.** 꼭 보여야 할 것은 그 안에 둔다.
- 아래쪽 약 150px에는 게임이 자막 띠를 얹는다 — 중요한 것을 거기 두지 않는다.

좌표는 전부 출력 픽셀(1280×720). 작은 것(곤충·사람·소품)은 국소 상자에 굽고 `stamp`로 찍는다 — 전체 화면 마스크를
매 프레임 만들면 느리다. 큰 정적 배경은 샷을 만들 때 한 번 굽는다.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
from silhouette_kit import (FPS, H, W, Mask, Particles, add, bezier, blur_arr, col, crop, dot,  # noqa: E402,F401
                            ease_in_out, ease_out, fbm, glyphs, lerp, over, radial, rays_texture, ridge, rim,
                            scribble, shift, sky, smooth, span, vignette, xf, zoom)

# ══════════════════════════ 색 ══════════════════════════

INK = "#3b2a1e"          # 윤곽
INK_DARK = "#120d18"     # 이름을 빼앗겨 바랜 몸
PAPER = 0.06             # 종이 결 세기
SAFE_X0, SAFE_X1 = 437, 843   # 세로 화면에서 보이는 가로 범위
SUB_TOP = H - 150             # 이 아래는 자막 띠

PAL = dict(
    # 하늘
    day=[(0, "#86cdfb"), (0.55, "#cdeeff"), (1, "#fff3d2")],
    dawn=[(0, "#9fb8ef"), (0.45, "#ffd2b0"), (0.75, "#ffe7b8"), (1, "#fff6dc")],
    dusk=[(0, "#6c79c9"), (0.5, "#f3a7a0"), (1, "#ffd9a8")],
    night=[(0, "#1f2a5c"), (0.6, "#3b4f8f"), (1, "#6a7fbf")],
    # 땅·풀
    meadow="#82d35c", meadow2="#5fb845", grass="#3f9a35",
    flower=["#ff7aa2", "#ffd84d", "#ffffff"],
    # 그림자
    shadow="#2c1e44", shadow_glow="#b9a2ff", eye="#ffe14d", blank="#1f1533",
    outside="#6a5aa3", fog="#a497da",
    # 이름 벽·울타리
    wall="#ead6ab", wall_line="#c9b083", cell="#f7e9c6", plate="#ffe49a", glow="#ffd34d",
    fence="#d08c4c", rail="#b8743a",
    # 곤충
    bfly="#ff9b3d", bfly2="#ffd84d", beetle="#e8413f", beetle2="#2b1d17", dfly="#3aa6f2", dfly2="#d7f1ff",
    ink=INK, rimlight="#fff3c4",
)


def c(v):
    """'#hex' 또는 rgb 배열을 rgb로."""
    return col(v) if isinstance(v, str) else np.asarray(v, np.float32)


# ══════════════════════════ 찍기·칠하기 ══════════════════════════

def stamp(dst, a, x, y, color, mode="over"):
    """작은 알파 a를 (x,y) 가운데에 덮는다(또는 더한다). 화면 밖은 잘린다."""
    h, w = a.shape
    x0, y0 = int(round(x - w / 2)), int(round(y - h / 2))
    sx0, sy0 = max(0, -x0), max(0, -y0)
    x0c, y0c = max(0, x0), max(0, y0)
    x1c, y1c = min(dst.shape[1], x0 + w), min(dst.shape[0], y0 + h)
    if x1c <= x0c or y1c <= y0c:
        return
    sub = a[sy0:sy0 + (y1c - y0c), sx0:sx0 + (x1c - x0c)]
    cc = c(color)
    if mode == "add":
        add(dst[y0c:y1c, x0c:x1c], cc, sub)
    elif mode == "mul":
        reg = dst[y0c:y1c, x0c:x1c]
        reg *= 1 - sub[..., None] * (1 - cc)
    else:
        over(dst[y0c:y1c, x0c:x1c], cc, sub)


def grow(a, r):
    """윤곽용으로 알파를 r픽셀쯤 부풀린다."""
    return np.clip(blur_arr(a, r) * 3.2, 0, 1)


def paint(dst, a, fill, x=None, y=None, ink=INK, line=1.6, hi=0.16, lo=0.14, light=(-1, -1)):
    """그림책 칠 한 겹 — 윤곽 → 단색 → 안쪽 위 밝은 테·아래 그늘.

    a가 화면 크기면 x·y를 비우고, 작은 상자면 그 가운데 좌표를 준다. light는 빛이 오는 쪽(기본 왼쪽 위).
    """
    if ink is not None and line > 0:
        _put(dst, grow(a, line), x, y, ink)
    _put(dst, a, x, y, fill)
    if hi > 0:
        _put(dst, rim(a, -light[0] * 3, -light[1] * 3, 3) * hi, x, y, "#ffffff", "add")
    if lo > 0:
        _put(dst, rim(a, light[0] * 4, light[1] * 4, 4) * lo, x, y, "#2a1a10", "mulover")


def _put(dst, a, x, y, color, mode="over"):
    if mode == "mulover":   # 그늘 — 검정을 반투명으로 얹는다
        color, mode = "#2a1a10", "over"
    if x is None:
        if mode == "add":
            add(dst, c(color), a)
        else:
            over(dst, c(color), a)
    else:
        stamp(dst, a, x, y, color, mode)


def bake_layer(draw, w=W, h=H):
    """그리기 함수를 한 번 돌려 **미리 곱한 색 + 알파** 층으로 굽는다 — 정적인 것을 움직이는 것 앞에 두고 싶을 때.

    draw(canvas)는 canvas(h×w×3)에 over/add/paint로 그린다. 검정·흰 바탕에 두 번 그려 알파를 얻으므로
    덮기(over)와 더하기(add, 빛 번짐)가 둘 다 정확히 남는다. 쓸 때는 `put_layer(f, *layer)`.
    **draw는 두 번 불린다** — 난수는 draw **안에서** 씨앗으로 만들 것. 바깥 생성기를 쓰면 두 그림이 달라져 반투명 유령이 남는다.
    """
    black = np.zeros((h, w, 3), np.float32)
    white = np.ones((h, w, 3), np.float32)
    draw(black)
    draw(white)
    a = np.clip(1 - (white - black).mean(axis=2), 0, 1)
    return black, a


def put_layer(dst, premul, a):
    """bake_layer로 구운 층을 얹는다(제자리)."""
    dst *= (1 - a)[..., None]
    dst += premul
    return dst


def pad(a, p):
    """알파 둘레를 p픽셀 비운다 — 작은 상자에서 블러하면 번짐이 상자 끝에서 네모나게 잘린다(반경의 3배는 비울 것)."""
    p = int(p)
    if p <= 0:
        return a
    out = np.zeros((a.shape[0] + p * 2, a.shape[1] + p * 2), np.float32)
    out[p:p + a.shape[0], p:p + a.shape[1]] = a
    return out


def glow_at(dst, a, x, y, color, r=10, amount=0.9):
    stamp(dst, blur_arr(pad(a, r * 3), r) * amount, x, y, color, "add")


def local(w, h):
    """국소 상자 마스크(2배 슈퍼샘플)."""
    return Mask(int(w) + 8, int(h) + 8)


def paper_tex(seed):
    return fbm(W, H, seed, (180, 50), (0.6, 0.4))


def apply_paper(f, paper, amount=PAPER):
    if paper is not None:
        f *= (1 + (paper[..., None] - 0.5) * amount * 2)
    return f


# ══════════════════════════ 배경 ══════════════════════════

def book_sky(stops, seed=0, w=W, h=H):
    return sky(w, h, stops, seed=seed, texture=0.025)


def sun(dst, x, y, r=60, color="#fff1a8", halo=0.5):
    add(dst, c("#fff6d0"), radial(dst.shape[1], dst.shape[0], x, y, r * 6, 2.2) * halo)
    m = local(r * 2 + 8, r * 2 + 8)
    m.circle(m.w / 2, m.h / 2, r)
    paint(dst, m.arr(0.5), color, x, y, ink="#e0a040", line=1.2, hi=0.2, lo=0.0)


def cloud(dst, x, y, s=1.0, color="#ffffff", ink="#9bb6d6", seed=0):
    """뭉게구름 — 동그라미 여럿, 아래가 평평하다."""
    r = np.random.default_rng(seed)
    w, h = int(260 * s), int(130 * s)
    m = local(w, h)
    n = 5
    for k in range(n):
        cx = m.w * (0.18 + 0.64 * k / (n - 1)) + r.uniform(-8, 8) * s
        rr = (28 + 26 * math.sin(math.pi * k / (n - 1)) + r.uniform(-4, 6)) * s
        m.circle(cx, m.h * 0.62 - rr * 0.45, rr)
    m.rect(m.w * 0.14, m.h * 0.5, m.w * 0.86, m.h * 0.78, r=int(18 * s))
    paint(dst, m.arr(0.5), color, x, y, ink=ink, line=1.2, hi=0.0, lo=0.08)


def hills(dst, seed, y, amp, fill, ink=INK, freq=(0.0025, 0.007), line=1.6, hi=0.1, w=None):
    """둥근 언덕 한 겹(화면 크기 알파를 돌려준다)."""
    w = w or dst.shape[1]
    m = ridge(Mask(w, dst.shape[0]), seed, y, amp, w=w, freq=freq)
    a = m.arr(0.8)
    paint(dst, a, fill, ink=ink, line=line, hi=hi, lo=0.0)
    return a


def ground_grad(dst, a, top, bottom):
    """알파 a 영역을 위→아래 그라데이션으로 칠한다(윤곽 없이 — 이미 칠한 땅 위에 덮는다)."""
    hh = dst.shape[0]
    yy = np.linspace(0, 1, hh, dtype=np.float32)[:, None, None]
    grad = c(top) * (1 - yy) + c(bottom) * yy
    over(dst, grad, a)


def tree(dst, x, base, h, seed=0, leaf="#5cbf4a", leaf2="#3f9a35", trunk="#9b6a3c", sway=0.0):
    """둥근 수관 나무 — 동그라미 덩어리 + 굵은 줄기."""
    r = np.random.default_rng(seed)
    w = h * 0.9
    m = local(w + 40, h + 40)
    cx, cy = m.w / 2, m.h * 0.42
    tk = local(w + 40, h + 40)
    tk.taper([(cx, m.h - 20), (cx + sway * 0.3, cy + h * 0.1)], h * 0.12, h * 0.08)
    paint(dst, tk.arr(0.5), trunk, x, base - h / 2 + 10, line=1.4, hi=0.1)
    for k in range(7):
        a = 2 * math.pi * k / 7 + r.uniform(-0.3, 0.3)
        rr = h * r.uniform(0.18, 0.26)
        m.circle(cx + math.cos(a) * h * 0.2 + sway, cy + math.sin(a) * h * 0.14, rr)
    m.circle(cx + sway, cy, h * 0.28)
    a = m.arr(0.5)
    paint(dst, a, leaf, x, base - h / 2 + 10, line=1.6, hi=0.14, lo=0.0)
    sh = local(w + 40, h + 40)
    for k in range(4):
        sh.circle(cx + r.uniform(-0.2, 0.2) * h + sway, cy + h * r.uniform(0.05, 0.2), h * r.uniform(0.1, 0.16))
    stamp(dst, sh.arr(2) * a * 0.35, x, base - h / 2 + 10, leaf2)


def bush(dst, x, base, w, seed=0, fill="#4fb43e", berries=None):
    r = np.random.default_rng(seed)
    m = local(w + 20, w * 0.7 + 20)
    for k in range(5):
        m.circle(m.w * (0.2 + 0.15 * k), m.h * 0.62 - r.uniform(0, w * 0.15), w * r.uniform(0.16, 0.24))
    m.rect(m.w * 0.12, m.h * 0.6, m.w * 0.88, m.h - 10)
    paint(dst, m.arr(0.5), fill, x, base - m.h / 2 + 10, line=1.5, hi=0.14)
    if berries:
        for k in range(6):
            dot(dst, x + r.uniform(-0.35, 0.35) * w, base - r.uniform(0.25, 0.6) * w * 0.7, 3, c(berries), 0.9)


def meadow(img, y, seed, flowers=60, top=None, bottom=None):
    """풀밭 — 언덕 윤곽 + 그라데이션 + 풀잎 + 꽃(샘플과 같다). 땅 알파를 돌려준다."""
    from silhouette_kit import grass
    gm = ridge(Mask(), seed, y, 10, freq=(0.003, 0.008)).arr(1.0)
    over(img, c(INK), np.clip(gm - shift(gm, 0, 2.5), 0, 1) * 0.7)
    ground_grad(img, gm, top or PAL["meadow"], bottom or PAL["meadow2"])
    gl = grass(Mask(), seed + 3, y + 6, H + 20, 260, height=(14, 46), width=(1.6, 3.6)).arr(0.5)
    over(img, c(PAL["grass"]), gl * gm)
    if flowers:
        r = np.random.default_rng(seed + 9)
        for _ in range(flowers):
            fx, fy = r.uniform(0, W), r.uniform(y + 20, H)
            fc = PAL["flower"][r.integers(0, len(PAL["flower"]))]
            sc = 0.6 + 0.8 * (fy - y) / max(1, (H - y))
            fm = Mask(40, 40)
            for k in range(5):
                a = 2 * math.pi * k / 5
                fm.circle(20 + math.cos(a) * 6 * sc, 20 + math.sin(a) * 6 * sc, 4.6 * sc)
            stamp(img, fm.arr(0.4), fx, fy, fc)
            stamp(img, Mask(12, 12).circle(6, 6, 2.4 * sc).arr(0.3), fx, fy, "#ffcc33")
    return gm


def water(dst, y0, y1, top="#5ec6e8", bottom="#2f8fc4", t=0.0, seed=0, sparkle=True):
    """물 — 그라데이션 + 흔들리는 가로 물결선 + 반짝임."""
    hh, ww = dst.shape[:2]
    m = Mask(ww, hh)
    m.rect(0, y0, ww, y1)
    a = m.arr(0.6)
    ground_grad(dst, a, top, bottom)
    r = np.random.default_rng(seed)
    lm = Mask(ww, hh)
    for k in range(18):
        yy = r.uniform(y0 + 10, y1 - 4)
        xx = r.uniform(0, ww) + math.sin(t * 1.4 + k) * 18
        ln = r.uniform(40, 120) * (0.5 + (yy - y0) / max(1, y1 - y0))
        lm.line([(xx - ln / 2, yy), (xx + ln / 2, yy)], 2.2)
    add(dst, c("#ffffff"), lm.arr(0.6) * a * 0.45)
    if sparkle:
        for k in range(14):
            xx, yy = r.uniform(0, ww), r.uniform(y0 + 4, y1)
            tw = 0.5 + 0.5 * math.sin(t * 3 + k * 1.7)
            dot(dst, xx, yy, 2.4, c("#ffffff"), 0.6 * tw)
    return a


def reeds(dst, x0, x1, base, seed=0, t=0.0, fill="#6fae3e", tip="#a0682e", n=14, h=(150, 260)):
    r = np.random.default_rng(seed)
    m = Mask()
    tips = Mask()
    for k in range(n):
        x = r.uniform(x0, x1)
        hh = r.uniform(*h)
        sw = math.sin(t * 1.3 + k) * 8
        pts = bezier((x, base), (x, base - hh * 0.4), (x + sw * 0.5, base - hh * 0.7), (x + sw, base - hh), 10)
        m.taper(pts, 7, 3)
        if k % 2 == 0:
            tips.ellipse(x + sw, base - hh - 14, 6, 20, 0.0)
    paint(dst, m.arr(0.5), fill, line=1.3, hi=0.1)
    paint(dst, tips.arr(0.5), tip, line=1.3, hi=0.1)


def stone_wall(seed, fill="#c9b7a0", line="#9c8670", w=W, h=H, block=(150, 80)):
    """돌벽 배경(정적) — 엇갈린 블록 + 질감."""
    n = fbm(w, h, seed, (200, 70, 20), (0.5, 0.35, 0.15))
    img = np.ones((h, w, 3), np.float32) * c(fill)
    img *= (0.88 + 0.24 * n)[..., None]
    lm = Mask(w, h)
    bw, bh = block
    for row in range(-1, h // bh + 2):
        y = row * bh + 10
        lm.line([(0, y), (w, y)], 2.4)
        off = (row % 2) * bw / 2
        x = -bw + off
        while x < w + bw:
            lm.line([(x, y), (x, y + bh)], 2.4)
            x += bw
    over(img, c(line), lm.arr(0.8) * 0.6)
    return img


def light_beam(dst, x0, y0, x1, y1, width0, width1, color="#fff6c8", amount=0.35):
    """비스듬한 빛줄기(가산)."""
    m = Mask()
    dx, dy = x1 - x0, y1 - y0
    ln = math.hypot(dx, dy) or 1
    nx, ny = -dy / ln, dx / ln
    m.poly([(x0 + nx * width0, y0 + ny * width0), (x1 + nx * width1, y1 + ny * width1),
            (x1 - nx * width1, y1 - ny * width1), (x0 - nx * width0, y0 - ny * width0)])
    add(dst, c(color), m.arr(18) * amount)


# ══════════════════════════ 곤충 ══════════════════════════

KINDS = ("bfly", "beetle", "dfly", "firefly", "mantis", "moth", "ancient", "longhorn", "ant")

# 종류별 (날개, 몸, 무늬) 색 — 그림책 기본값. colors=로 바꿀 수 있다.
INSECT_COLORS = {
    "bfly": ("#ff9b3d", INK, "#ffd84d"),
    "beetle": ("#e8413f", INK, "#2b1d17"),
    "dfly": ("#d7f1ff", "#3aa6f2", None),
    "firefly": ("#5a4a3a", "#3b2a1e", "#fff27a"),
    "mantis": ("#a8e070", "#6cc04a", None),
    "moth": ("#c9a77a", "#8a6a48", "#f2e2c0"),
    "ancient": ("#bfe8ff", "#2f7fb8", "#7fd0ff"),
    "longhorn": ("#3d6fd1", INK, "#ffffff"),
    "ant": ("#3b2a1e", "#3b2a1e", None),
}


def insect_parts(kind, s, rot=0.0, flap=0.0):
    """곤충 한 마리를 날개·몸·무늬·눈 알파로 나눠 굽는다(국소 상자). 머리는 -y, s는 몸 크기(px)."""
    box = int(3.6 * s) + 12
    cc = box / 2
    wing, body, spot, white, pupil = (Mask(box, box) for _ in range(5))

    def P(pts):
        return xf(pts, cc, cc, s, rot)

    hr = 0.2
    head = (0, -0.62)
    if kind in ("bfly", "moth"):
        k = 1 - 0.7 * flap
        big = 1.0 if kind == "bfly" else 0.9
        for sg in (-1, 1):
            wing.ellipse(*P([(sg * 0.56 * k, -0.2)])[0], 0.62 * k * s * big, 0.5 * s, rot - sg * 0.45)
            wing.ellipse(*P([(sg * 0.42 * k, 0.38)])[0], 0.42 * k * s * big, 0.34 * s, rot + sg * 0.5)
            spot.ellipse(*P([(sg * 0.66 * k, -0.26)])[0], 0.22 * k * s, 0.17 * s, rot)
            spot.circle(*P([(sg * 0.42 * k, 0.42)])[0], 0.11 * k * s)
            if kind == "bfly":
                body.line(P([(sg * 0.05, -0.7), (sg * 0.24, -1.08)]), 0.04 * s)
                body.circle(*P([(sg * 0.24, -1.08)])[0], 0.06 * s)
            else:
                body.taper(P([(sg * 0.05, -0.72), (sg * 0.22, -0.98), (sg * 0.34, -1.04)]), 0.07 * s, 0.03 * s)
        body.line(P([(0, -0.45), (0, 0.6)]), 0.16 * s * (1.3 if kind == "moth" else 1.0))
        head, hr = (0, -0.62), 0.16
    elif kind in ("beetle", "longhorn"):
        for sg in (-1, 1):
            for ly in (-0.22, 0.12, 0.44):
                body.line(P([(sg * 0.42, ly), (sg * 0.74, ly + 0.08), (sg * 0.82, ly + 0.3)]), 0.07 * s)
            if kind == "beetle":
                body.line(P([(sg * 0.12, -0.8), (sg * 0.32, -1.08)]), 0.05 * s)
            else:
                body.line(P([(sg * 0.12, -0.8), (sg * 0.5, -1.3), (sg * 0.9, -1.5)]), 0.05 * s)
        if kind == "longhorn":
            wing.ellipse(*P([(0, 0.18)])[0], 0.46 * s, 0.7 * s, rot)
        else:
            wing.ellipse(*P([(0, 0.12)])[0], 0.6 * s, 0.64 * s, rot)
        spots = ((-0.27, -0.06, 0.12), (0.27, -0.06, 0.12), (-0.22, 0.38, 0.11), (0.22, 0.38, 0.11))
        if kind == "longhorn":
            spots = ((-0.2, 0.0, 0.08), (0.2, 0.0, 0.08), (-0.16, 0.42, 0.08), (0.16, 0.42, 0.08))
        for sx, sy, r in spots:
            spot.circle(*P([(sx, sy)])[0], r * s)
        body.line(P([(0, -0.45), (0, 0.74)]), 0.035 * s)
        head, hr = (0, -0.6), 0.25
    elif kind == "firefly":
        for sg in (-1, 1):
            for ly in (-0.1, 0.18, 0.44):
                body.line(P([(sg * 0.3, ly), (sg * 0.55, ly + 0.1), (sg * 0.6, ly + 0.28)]), 0.06 * s)
            body.line(P([(sg * 0.1, -0.72), (sg * 0.3, -1.0)]), 0.045 * s)
            wing.ellipse(*P([(sg * 0.17, 0.12)])[0], 0.2 * s, 0.56 * s, rot + sg * 0.12)
        spot.ellipse(*P([(0, 0.72)])[0], 0.24 * s, 0.26 * s, rot)   # 꽁무니 불빛
        head, hr = (0, -0.55), 0.2
    elif kind == "mantis":
        body.taper(P([(0, -0.3), (0, 0.4), (0, 1.25)]), 0.2 * s, 0.1 * s)
        body.line(P([(0, -0.62), (0, -0.25)]), 0.11 * s)
        for sg in (-1, 1):
            body.line(P([(sg * 0.06, -0.32), (sg * 0.36, -0.62), (sg * 0.3, -0.98)]), 0.09 * s)   # 앞발 낫
            body.line(P([(sg * 0.3, -0.98), (sg * 0.16, -0.78)]), 0.06 * s)
            body.line(P([(sg * 0.08, 0.1), (sg * 0.5, 0.3), (sg * 0.62, 0.66)]), 0.05 * s)
            body.line(P([(sg * 0.08, 0.36), (sg * 0.46, 0.7), (sg * 0.5, 1.08)]), 0.05 * s)
            body.line(P([(sg * 0.08, -0.8), (sg * 0.36, -1.2)]), 0.03 * s)
            wing.ellipse(*P([(sg * 0.1, 0.62)])[0], 0.16 * s, 0.56 * s, rot + sg * 0.1)
        head, hr = (0, -0.74), 0.2
        white.ellipse(*P([head])[0], 0.0001, 0.0001)
    elif kind == "ant":
        body.circle(*P([(0, 0.62)])[0], 0.32 * s)
        body.circle(*P([(0, 0.08)])[0], 0.18 * s)
        for sg in (-1, 1):
            for ly in (-0.05, 0.1, 0.25):
                body.line(P([(sg * 0.1, ly), (sg * 0.5, ly - 0.1), (sg * 0.7, ly + 0.25)]), 0.05 * s)
            body.line(P([(sg * 0.08, -0.72), (sg * 0.3, -0.95), (sg * 0.5, -0.9)]), 0.04 * s)
        head, hr = (0, -0.5), 0.24
    else:   # dfly · ancient(날개 넷의 고대 잠자리 — 더 크고 무늬가 있다)
        big = 1.25 if kind == "ancient" else 1.0
        for sg in (-1, 1):
            for j, (wy, ln, ang) in enumerate(((-0.42, 1.15, -0.1), (-0.22, 1.0, 0.17))):
                a = ang + flap * (0.35 if j == 0 else -0.35)
                wing.ellipse(*P([(sg * ln * 0.52 * big, wy)])[0], ln * 0.5 * s * big, 0.14 * s * big, rot + sg * a)
                if kind == "ancient":
                    spot.ellipse(*P([(sg * ln * 0.78 * big, wy)])[0], 0.12 * s, 0.07 * s, rot + sg * a)
        body.taper(P([(0, -0.5), (0, 0.3), (0, 1.3)]), 0.16 * s, 0.07 * s)
        head, hr = (0, -0.72), 0.2
    hx, hy = P([head])[0]
    body.circle(hx, hy, hr * s)
    er = 0.5 if kind in ("dfly", "ancient", "mantis") else 0.42
    eyes = []
    for sg in (-1, 1):
        ex, ey = P([(head[0] + sg * hr * 0.48, head[1] - hr * 0.12)])[0]
        eyes.append((ex, ey))
        white.circle(ex, ey, hr * er * s)
        px, py = P([(head[0] + sg * hr * 0.42, head[1] - hr * 0.22)])[0]
        pupil.circle(px, py, hr * er * 0.55 * s)
    return dict(wing=wing.arr(0.5), body=body.arr(0.5), spot=spot.arr(0.5), white=white.arr(0.4),
                pupil=pupil.arr(0.4), eyes=eyes, box=box, hr=hr * s, kind=kind)


def draw_insect(f, kind, x, y, s, rot=0.0, flap=0.0, alpha=1.0, drain=0.0, shadow_eyes=0.0, parts=None,
                colors=None, glow=0.0):
    """곤충을 그림책 화풍으로 칠한다.

    drain(0..1) — 이름을 빼앗겨 검게 바래는 정도. shadow_eyes — 숨어든 그림자의 노란 눈.
    glow — 반딧불이 꽁무니·고대 잠자리 무늬 빛(가산). colors=(날개, 몸, 무늬)로 색을 바꾼다.
    """
    p = parts or insect_parts(kind, s, rot, flap)
    wc, bc, sc = colors or INSECT_COLORS[kind]
    dark = c(INK_DARK)
    union = np.maximum(p["wing"], p["body"]) * alpha
    stamp(f, grow(union, 1.6), x, y, lerp(c(INK), dark, drain))
    wa = 0.85 if kind in ("dfly", "ancient") else 1.0
    stamp(f, p["wing"] * alpha * wa, x, y, lerp(c(wc), dark, drain))
    stamp(f, p["body"] * alpha, x, y, lerp(c(bc), dark, drain))
    if sc is not None:
        sa = p["spot"] * alpha if kind in ("firefly", "mantis") else p["spot"] * p["wing"] * alpha
        stamp(f, sa, x, y, lerp(c(sc), dark, drain))
    if glow > 0 and sc is not None:
        stamp(f, blur_arr(p["spot"], max(2, s * 0.25)) * glow * alpha * (1 - drain), x, y, sc, "add")
    hi = rim(union, 2, 2, 2) * 0.18 * (1 - drain)
    stamp(f, hi, x, y, "#ffffff", "add")
    eye_a = alpha * (1 - drain)
    if eye_a > 0.01:
        stamp(f, p["white"] * eye_a, x, y, "#ffffff")
        stamp(f, p["pupil"] * eye_a, x, y, "#1a1210")
    if shadow_eyes > 0:
        cc = p["box"] / 2
        for ex, ey in p["eyes"]:
            gx, gy = x + ex - cc, y + ey - cc
            dot(f, gx, gy, p["hr"] * 0.9, c(PAL["eye"]), 0.9 * shadow_eyes)
            dot(f, gx, gy, p["hr"] * 0.35, c("#ffffff"), 0.9 * shadow_eyes)
    return p


class InsectSprite:
    """날갯짓 프레임을 미리 구워 둔 곤충 — 매 프레임 마스크를 다시 만들지 않는다."""

    def __init__(self, kind, s, rot=0.0, frames=6):
        self.kind, self.s = kind, s
        self.parts = [insect_parts(kind, s, rot, fl) for fl in np.linspace(0, 1, frames)]

    def draw(self, f, x, y, t=0.0, rate=12.0, **kw):
        k = int((0.5 + 0.5 * math.sin(t * rate)) * (len(self.parts) - 1) + 0.5)
        return draw_insect(f, self.kind, x, y, self.s, parts=self.parts[k], **kw)


# ══════════════════════════ 그림자(이름 잃은 것) ══════════════════════════

_SH = {}


def _shadow_fields():
    if not _SH:
        _SH["a"] = fbm(900, 700, 711, (70, 28, 11), (0.55, 0.3, 0.15))
        _SH["b"] = fbm(900, 700, 712, (90, 34, 13), (0.55, 0.3, 0.15))
    return _SH["a"], _SH["b"]


def shadow_alpha(t, w, h):
    n1, n2 = _shadow_fields()
    w, h = min(w, 600), min(h, 520)
    a = crop(n1, 200 + 120 * math.sin(t * 0.8), 160 + 90 * math.sin(t * 1.1 + 1.0), w, h)
    b = crop(n2, 200 + 110 * math.sin(t * 0.7 + 2.0), 160 + 100 * math.sin(t * 0.9), w, h)
    n = 0.5 * (a + b)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    env = 1 - ((xx - w / 2) / (w * 0.44)) ** 2 - ((yy - h * 0.56) / (h * 0.42)) ** 2
    v = env * 0.8 + (n - 0.5) * 1.3 + 0.12
    return np.clip((v - 0.3) / 0.2, 0, 1) ** 1.2


def draw_eyes(f, x, y, scale, amount, blink=1.0, spread=26, color=None):
    """그림자의 두 눈 — 노란 눈 + 세로 동공."""
    for sg in (-1, 1):
        ex, ey = x + sg * spread * scale, y - 6 * scale
        m = Mask(int(60 * scale) + 6, int(50 * scale) + 6)
        m.ellipse(m.w / 2, m.h / 2, 13 * scale, max(0.5, 11 * scale * blink))
        stamp(f, m.arr(0.6) * amount, ex, ey, color or PAL["eye"])
        pm = Mask(m.w, m.h)
        pm.ellipse(pm.w / 2 + 2 * scale, pm.h / 2, 3.2 * scale, max(0.5, 9 * scale * blink))
        stamp(f, pm.arr(0.5) * amount, ex, ey, "#1a1020")


def draw_shadow(f, x, y, scale, t, alpha=1.0, eyes=1.0, blink_period=2.3):
    """일렁이는 보랏빛 덩어리. scale 1 ≈ 230×190px."""
    w, h = int(230 * scale) + 8, int(190 * scale) + 8
    if w < 12 or h < 12 or alpha <= 0.01:
        return
    a = shadow_alpha(t, w, h) * alpha
    stamp(f, blur_arr(pad(a, 42 * scale + 4), 14 * scale) * 0.55, x, y, PAL["shadow_glow"], "add")
    stamp(f, a, x, y, PAL["shadow"])
    stamp(f, rim(a, 2, 2, 3) * 0.6, x, y, PAL["shadow_glow"], "add")
    if eyes > 0:
        blink = 1.0 if (t % blink_period) > 0.12 else 0.15
        draw_eyes(f, x, y, scale, eyes * alpha, blink)


def shadow_insect(f, kind, x, y, s, t, alpha=1.0, rot=0.0, flap=0.0):
    """그림자가 빌려 쓴 곤충 모습 — 검보랏빛 몸 + 일렁이는 테 + 노란 눈."""
    p = insect_parts(kind, s, rot, flap)
    union = np.maximum(p["wing"], p["body"]) * alpha
    wob = 1 + 0.06 * math.sin(t * 5)
    stamp(f, blur_arr(pad(union, 30), 10) * 0.6 * wob, x, y, PAL["shadow_glow"], "add")
    stamp(f, grow(union, 1.8), x, y, "#1a1030")
    stamp(f, union, x, y, PAL["shadow"])
    stamp(f, rim(union, 2, 2, 3) * 0.7, x, y, PAL["shadow_glow"], "add")
    cc = p["box"] / 2
    for ex, ey in p["eyes"]:
        dot(f, x + ex - cc, y + ey - cc, p["hr"] * 0.9, c(PAL["eye"]), 0.95 * alpha)
    return p


# ══════════════════════════ 이름 벽·울타리 ══════════════════════════

def post_pts(x, base, w, h):
    return [(x - w / 2, base), (x - w / 2, base - h + w * 0.55), (x, base - h), (x + w / 2, base - h + w * 0.55),
            (x + w / 2, base)]


def rotate_pts(pts, cx, cy, a, dy=0.0):
    cr, sr = math.cos(a), math.sin(a)
    return [(cx + (px - cx) * cr - (py + dy - cy) * sr, cy + (px - cx) * sr + (py + dy - cy) * cr) for px, py in pts]


def rect_pts(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


def plate_rect(x, base, w, h):
    pw, ph = w * 1.05, w * 0.92
    py = base - h * 0.56
    return x - pw / 2, py - ph / 2, x + pw / 2, py + ph / 2


def finish_fence(f, pa, ra):
    over(f, c(INK), grow(np.maximum(pa, ra), 1.6))
    over(f, c(PAL["rail"]), ra)
    over(f, c(PAL["fence"]), pa)
    add(f, c("#ffffff"), rim(pa, 3, 0, 3) * 0.18)


def paint_fence(f, posts, base, w, h):
    pm, rm = Mask(), Mask()
    for i, x in enumerate(posts):
        pm.poly(post_pts(x, base, w, h))
        if i + 1 < len(posts):
            for ry in (0.3, 0.74):
                rm.rect(x, base - h * ry - w * 0.13, posts[i + 1], base - h * ry + w * 0.13)
    finish_fence(f, pm.arr(0.6), rm.arr(0.6))


def paint_plate(f, x, base, w, h, kind, level=1.0, icon_alpha=1.0, glow=True):
    """말뚝 이름표 — 작은 곤충 그림이 이름이다. level은 빛(1=살아 있는 이름, 0=바랜 이름)."""
    x0, y0, x1, y1 = plate_rect(x, base, w, h)
    pcx, pcy = (x0 + x1) / 2, (y0 + y1) / 2
    pad = 34   # 빛 번짐(blur 10)이 상자 끝에서 네모나게 잘리지 않게 넉넉히
    m = Mask(int(x1 - x0) + pad * 2, int(y1 - y0) + pad * 2)
    m.rect(pad, pad, m.w - pad, m.h - pad, r=6)
    a = m.arr(0.5)
    if level > 0 and glow:
        stamp(f, blur_arr(a, 10) * 0.9 * level, pcx, pcy, PAL["glow"], "add")
    stamp(f, grow(a, 1.4), pcx, pcy, INK)
    stamp(f, a, pcx, pcy, lerp(c("#bfb5a6"), c(PAL["plate"]), 0.25 + 0.75 * level))
    if icon_alpha > 0.01 and kind:
        draw_insect(f, kind, pcx, pcy, (x1 - x0) * 0.3, alpha=icon_alpha, drain=(1 - level) * 0.85)


def field_bg(horizon, seed):
    """울타리 바깥 — 보랏빛 안개 땅과 하늘(샘플 샷2와 같다)."""
    img = book_sky(PAL["day"], seed=seed)
    far = ridge(Mask(), seed + 1, horizon - 70, 34, freq=(0.003, 0.009, 0.02)).arr(1.5)
    over(img, c(PAL["outside"]), far)
    yy = np.arange(H, dtype=np.float32)[:, None]
    fogb = np.clip(1 - np.abs(yy - (horizon - 30)) / 120, 0, 1) ** 1.5 * np.ones((1, W), np.float32)
    n = fbm(W, H, seed + 2, (240, 90), (0.7, 0.3))
    over(img, c(PAL["fog"]), fogb * (0.35 + 0.5 * n))
    return img


class NameWall:
    """이름 벽 — rows×cols 칸, 칸마다 곤충 그림과 판독 불가 이름. 칸의 빛(level)을 프레임마다 정한다."""

    def __init__(self, rows=3, cols=7, cell=118, gap=16, seed=401, kinds=("bfly", "beetle", "dfly"), cy_off=-20):
        self.rows, self.cols, self.cell, self.gap = rows, cols, cell, gap
        self.x0 = (W - (cols * (cell + gap) - gap)) / 2
        self.y0 = (H - (rows * (cell + gap) - gap)) / 2 + cy_off
        n = fbm(W, H, seed, (200, 70, 20), (0.5, 0.35, 0.15))
        img = np.ones((H, W, 3), np.float32) * c(PAL["wall"])
        img *= (0.86 + 0.28 * n)[..., None]
        lm = Mask()
        for row in range(-1, 12):
            y = row * 74 + 20
            lm.line([(0, y), (W, y)], 2.4)
            off = (row % 2) * 70
            for x in range(-140 + off, W + 140, 140):
                lm.line([(x, y), (x, y + 74)], 2.4)
        over(img, c(PAL["wall_line"]), lm.arr(0.8) * 0.55)
        cm = Mask()
        for r in range(rows):
            for k in range(cols):
                cx, cy = self.center(r, k)
                cm.rect(cx - cell / 2, cy - cell / 2, cx + cell / 2, cy + cell / 2, r=10)
        ca = cm.arr(0.6)
        over(img, c(INK), grow(ca, 1.5) * 0.85)
        over(img, c(PAL["cell"]), ca)
        gm = Mask()
        for r in range(rows):
            for k in range(cols):
                cx, cy = self.center(r, k)
                glyphs(gm, cx - cell * 0.29, cy + cell * 0.25, cell * 0.6, 1, 900 + r * 10 + k, size=11, weight=2.2)
        self.img, self.glyph_a = img, gm.arr(0.4)
        self.icons = {}
        for r in range(rows):
            for k in range(cols):
                kind = kinds[(r * cols + k) % len(kinds)]
                p = insect_parts(kind, cell * 0.25, rot=0.12 * math.sin(r * 3 + k))
                ia = np.maximum(p["wing"], p["body"])
                self.icons[(r, k)] = (kind, p, blur_arr(ia, 12) * 0.7)

    def center(self, r, k):
        return (self.x0 + k * (self.cell + self.gap) + self.cell / 2,
                self.y0 + r * (self.cell + self.gap) + self.cell / 2)

    def draw(self, level_fn, skip=()):
        """level_fn(r, k) → 0..1(빛), 또는 None이면 그 칸을 비운다(빈칸). 새 프레임을 돌려준다."""
        f = self.img.copy()
        lit = np.zeros((H, W), np.float32)
        half = self.cell / 2
        for (r, k), (kind, p, glow) in self.icons.items():
            if (r, k) in skip:
                continue
            lv = level_fn(r, k)
            cx, cy = self.center(r, k)
            if lv is None:
                continue
            lit[int(cy - half):int(cy + half), int(cx - half):int(cx + half)] = lv
            stamp(f, glow * lv, cx, cy - 8, PAL["glow"], "add")
            draw_insect(f, kind, cx, cy - 8, self.cell * 0.25, alpha=0.35 + 0.65 * min(1.0, lv),
                        drain=max(0.0, 0.6 - lv) * 0.9, parts=p)
        over(f, c(INK), self.glyph_a * (0.35 + 0.5 * np.clip(lit, 0, 1)))
        add(f, c(PAL["glow"]), blur_arr(self.glyph_a, 3) * lit * 0.5)
        return f

    def blank_cell(self, f, x, y, eyes=0.0, blink=1.0, glow=0.7):
        """빈칸(그림자가 숨은 칸)을 (x,y)에 그린다."""
        m = Mask(self.cell + 40, self.cell + 40)
        m.rect(20, 20, self.cell + 20, self.cell + 20, r=10)
        a = m.arr(1.2)
        stamp(f, blur_arr(a, 18) * glow, x, y, PAL["shadow_glow"], "add")
        stamp(f, a, x, y, PAL["blank"])
        if eyes > 0:
            draw_eyes(f, x, y, 0.85, eyes, blink=blink)


# ══════════════════════════ 사람 ══════════════════════════

# 인물 표지 — 실루엣 영상과 같은 단서(주인공=채집망, 세라=긴 머리·가방, 명부회=챙 넓은 모자)를 색으로도 구분한다.
PEOPLE = {
    "hero": dict(hair="#4a2f1f", skin="#f6c9a0", top="#3fae6a", bottom="#3f63b5", shoe="#e0473e",
                 cap="#ffcf3f", bag="#f28a2e", net=True, hair_len=0.0, build="kid"),
    "sera": dict(hair="#8a4b2a", skin="#f6c9a0", top="#e9dcb8", bottom="#7a8f5a", shoe="#6b4a2e",
                 coat="#e9dcb8", bag="#a0663a", hair_len=0.2, build="robe", scarf="#5aa3a0"),
    "raon": dict(hair="#2b2030", skin="#f2c094", top="#e8473f", bottom="#2f3f6f", shoe="#ffffff",
                 hair_spiky=True, build="kid", stripe="#ffffff"),
    "ledger": dict(hair="#2b2030", skin="#f0c49c", top="#2f3352", bottom="#23263d", shoe="#1c1c26",
                   coat="#2f3352", hat="#23263d", band="#d9b44a", build="coat"),
    "jeoul": dict(hair="#2b2030", skin="#f0c49c", top="#2f3352", bottom="#23263d", shoe="#1c1c26",
                  coat="#3a3f63", hat="#23263d", band="#9fc4e6", build="coat"),
    "meok": dict(hair="#1c1c22", skin="#f0c49c", top="#26262e", bottom="#1e1e26", shoe="#141418",
                 coat="#26262e", build="coat", scarf="#8c3a3a"),
    "hawol": dict(hair="#eceae4", skin="#f0c8a8", top="#4a4a52", bottom="#3a3a42", shoe="#26262c",
                  coat="#55565f", hat="#3a3a42", band="#d9b44a", build="old", cane="#8a6440"),
    "elder": dict(hair="#d8d4cc", skin="#f0c8a8", top="#6aa35a", bottom="#7a6a52", shoe="#5a4030",
                  build="old", cane="#8a6440", bun=True),
}


def person_parts(who, h, t=0.0, walk=None, face=0.0, arm=None, sling=False, wave=0.0):
    """사람 한 명을 부위별 알파로 굽는다(국소 상자). 뒷모습이 기본, face로 옆을 튼다(-1 왼쪽 ~ 1 오른쪽).

    arm=(dx, dy) — 오른손 끝 위치(발 기준 상대 좌표, h 단위)를 주면 그쪽으로 팔을 뻗는다.
    walk — 걸음 위상(라디안). sling — 왼팔을 팔걸이에 감싼다(다친 라온).
    """
    d = PEOPLE[who]
    build = d["build"]
    bw, bh = int(h * 1.4) + 20, int(h * 1.5) + 20
    ox, oy = bw / 2, bh - 10            # 발 기준점
    keys = ("hair", "skin", "top", "bottom", "shoe", "hat", "band", "bag", "pole", "sling", "stripe", "net", "collar")
    parts = {k: Mask(bw, bh) for k in keys}
    x, y = ox, oy
    hunch = h * 0.06 if build == "old" else 0.0
    kid = build == "kid"
    sw = h * (0.23 if kid else 0.26) * (1 - 0.3 * abs(face))
    hr = h * (0.085 if kid else 0.072)
    hx = x + face * h * 0.03 + hunch * 0.8
    hy = y - h + hr + hunch * 0.6
    ys = y - h * 0.78 + hunch
    waist = y - h * (0.47 if kid else 0.5)
    step = math.sin(walk) if walk is not None else 0.0
    # 다리·신발
    for sgn in (-1, 1):
        swing = step * sgn * h * 0.06 if walk is not None else 0.0
        hip = (x + sgn * h * 0.055, waist + h * 0.04)
        knee = (x + sgn * h * 0.06 + swing * 0.6, y - h * 0.24)
        ankle = (x + sgn * h * 0.055 + swing, y - h * 0.04)
        parts["bottom"].taper([hip, knee], h * 0.09, h * 0.07)
        leg = parts["bottom"] if not kid else parts["skin"]
        leg.taper([knee, ankle], h * 0.065, h * 0.05)
        parts["shoe"].ellipse(ankle[0] + sgn * h * 0.006, y - h * 0.022, h * 0.045, h * 0.028)
    if kid:   # 반바지
        parts["bottom"].poly([(x - h * 0.12, waist - h * 0.02), (x + h * 0.12, waist - h * 0.02),
                              (x + h * 0.13, waist + h * 0.17), (x + h * 0.01, waist + h * 0.17),
                              (x, waist + h * 0.08), (x - h * 0.01, waist + h * 0.17), (x - h * 0.13, waist + h * 0.17)])
    # 몸통
    neck = [(hx - hr * 0.4, hy + hr * 0.75), (hx + hr * 0.4, hy + hr * 0.75)]
    left = bezier(neck[0], (x - sw * 0.3 + hunch, ys - h * 0.02), (x - sw * 0.54 + hunch, ys),
                  (x - sw * 0.5 + hunch, ys + h * 0.06), 10)
    right = bezier(neck[1], (x + sw * 0.3 + hunch, ys - h * 0.02), (x + sw * 0.54 + hunch, ys),
                   (x + sw * 0.5 + hunch, ys + h * 0.06), 10)
    if build in ("coat", "robe", "old"):
        hem = y - h * (0.12 if build == "coat" else (0.1 if build == "robe" else 0.2))
        flare = h * (0.2 if build == "coat" else 0.17)
        flut = math.sin(t * 3.1) * h * 0.014
        body = left + [(x - h * 0.12, waist), (x - flare + flut * 0.5, hem), (x - flare * 0.3, hem + h * 0.012),
                       (x + flare * 0.35, hem + h * 0.01 + flut), (x + flare + flut, hem), (x + h * 0.12, waist)] + right[::-1]
    else:
        body = left + [(x - h * 0.12, waist), (x - h * 0.125, waist + h * 0.03), (x + h * 0.125, waist + h * 0.03),
                       (x + h * 0.12, waist)] + right[::-1]
    parts["top"].poly(body)
    if d.get("stripe"):
        parts["stripe"].rect(x - h * 0.13, ys + h * 0.12, x + h * 0.13, ys + h * 0.16)
    # 팔
    for sgn in (-1, 1):
        sh = (x + sgn * (sw * 0.47) + hunch, ys + h * 0.045)
        if arm is not None and sgn == 1:
            ex, ey = x + arm[0] * h, y + arm[1] * h
            elbow = (lerp(sh[0], ex, 0.45) + h * 0.04, lerp(sh[1], ey, 0.55) + h * 0.05)
            parts["top"].taper([sh, elbow], h * 0.07, h * 0.06)
            parts["top"].taper([elbow, (ex, ey)], h * 0.06, h * 0.05)
            parts["skin"].circle(ex, ey, h * 0.032)
            continue
        if sling and sgn == -1:
            elbow = (sh[0] - h * 0.01, sh[1] + h * 0.17)
            parts["top"].taper([sh, elbow], h * 0.07, h * 0.064)
            parts["sling"].taper([elbow, (x + h * 0.03, elbow[1] - h * 0.03)], h * 0.09, h * 0.075)
            parts["sling"].line([(sh[0] + h * 0.02, sh[1] - h * 0.03), (x + h * 0.06, elbow[1] - h * 0.02)], h * 0.02)
            continue
        swing = math.sin(walk + math.pi) * sgn * h * 0.04 if walk is not None else 0.0
        lift = wave * h * 0.3 if sgn == -1 else 0.0
        elbow = (sh[0] + sgn * h * 0.03 + swing * 0.5, sh[1] + h * 0.16 - lift)
        hand_p = (sh[0] + sgn * h * 0.02 + swing, sh[1] + h * 0.3 - lift * 2.2)
        parts["top"].taper([sh, elbow], h * 0.07, h * 0.06)
        parts["top"].taper([elbow, hand_p], h * 0.06, h * 0.05)
        parts["skin"].circle(hand_p[0], hand_p[1], h * 0.032)
    # 머리 — 뒷모습이라 머리카락이 머리 대부분을 덮는다
    parts["skin"].ellipse(hx, hy, hr * 0.92, hr)
    if face != 0:   # 옆으로 틀면 귀·뺨이 보인다
        parts["skin"].ellipse(hx + face * hr * 0.55, hy + hr * 0.2, hr * 0.5, hr * 0.62)
    hair = parts["hair"]
    hair.ellipse(hx - face * hr * 0.12, hy - hr * 0.12, hr * 0.98, hr * 0.92)
    if d.get("hair_len"):
        sway = math.sin(t * 2.2) * h * 0.006
        hl = d["hair_len"]
        hair.poly([(hx - hr * 0.98, hy - hr * 0.1), (hx + hr * 0.98, hy - hr * 0.1),
                   (hx + hr * 1.12 + sway, hy + h * hl), (hx + sway * 2, hy + h * (hl + 0.03)),
                   (hx - hr * 1.12 + sway, hy + h * hl)])
    if d.get("hair_spiky"):
        for k in range(6):
            a = -math.pi * (0.1 + 0.8 * k / 5)
            hair.poly([(hx + math.cos(a - 0.25) * hr * 0.8, hy + math.sin(a - 0.25) * hr * 0.8),
                       (hx + math.cos(a) * hr * 1.45, hy + math.sin(a) * hr * 1.45),
                       (hx + math.cos(a + 0.25) * hr * 0.8, hy + math.sin(a + 0.25) * hr * 0.8)])
    if d.get("bun"):
        hair.circle(hx, hy - hr * 0.95, hr * 0.42)
    if d.get("cap"):
        parts["hat"].ellipse(hx, hy - hr * 0.35, hr * 1.02, hr * 0.72)
        parts["hat"].rect(hx - hr * 0.95, hy - hr * 0.2, hx + hr * 0.95, hy + hr * 0.02)
    if d.get("hat"):
        parts["hat"].ellipse(hx, hy - hr * 0.15, hr * 1.9, hr * 0.38)      # 챙
        parts["hat"].rect(hx - hr * 0.8, hy - hr * 1.15, hx + hr * 0.8, hy - hr * 0.1, r=hr * 0.25)
        if d.get("band"):
            parts["band"].rect(hx - hr * 0.8, hy - hr * 0.42, hx + hr * 0.8, hy - hr * 0.22)
    if d.get("hat") and face == 0:
        # 뒷모습 + 챙 모자 — 세운 깃이 뒤통수 아래를 덮는다. 안 덮으면 챙과 깃 사이 머리 타원이 이목구비 없는 얼굴로 읽힌다.
        parts["collar"].poly([(hx - hr * 1.15, hy + hr * 1.05), (hx - hr * 1.0, hy + hr * 0.1), (hx - hr * 0.3, hy + hr * 0.35),
                              (hx + hr * 0.3, hy + hr * 0.35), (hx + hr * 1.0, hy + hr * 0.1), (hx + hr * 1.15, hy + hr * 1.05)])
    if d.get("scarf"):
        parts["band"].ellipse(hx, hy + hr * 0.95, hr * 0.95, hr * 0.38)
    if d.get("bag"):
        if build == "kid":   # 책가방
            parts["bag"].rect(x - h * 0.11, ys + h * 0.05, x + h * 0.11, ys + h * 0.27, r=h * 0.035)
        else:                # 세라의 어깨 가방
            bx, by = x - sw * 0.6, waist + h * 0.03
            parts["bag"].rect(bx - h * 0.07, by - h * 0.06, bx + h * 0.05, by + h * 0.06, r=h * 0.015)
            parts["bag"].line([(bx, by - h * 0.06), (x + sw * 0.35, ys + h * 0.01)], h * 0.014)
    if d.get("cane"):
        parts["pole"].line([(x + sw * 0.55, ys + h * 0.3), (x + sw * 0.62, y)], h * 0.022)
    net_top = None
    if d.get("net"):
        grip = (x + sw * 0.5 + h * 0.02, ys + h * 0.28)
        net_top = (x - h * 0.24, y - h * 1.3)
        parts["pole"].line([grip, net_top], h * 0.022)
        parts["net"].ellipse(net_top[0], net_top[1], h * 0.13, h * 0.1)
    out = {k: v.arr(0.5) for k, v in parts.items()}
    if net_top is not None:
        tx, ty = net_top
        rim_m = Mask(bw, bh)
        rim_m.d.ellipse([(tx - h * 0.13) * rim_m.ss, (ty - h * 0.1) * rim_m.ss, (tx + h * 0.13) * rim_m.ss,
                         (ty + h * 0.1) * rim_m.ss], outline=255, width=max(1, int(h * 0.02 * rim_m.ss)))
        out["netrim"] = rim_m.arr(0.5)
    out["origin"] = (ox, oy)
    out["box"] = (bw, bh)
    return out


# 그리는 순서(뒤 → 앞)와 부위별 색 키(앞의 키가 있으면 그것)
_PERSON_ORDER = (("bottom", "bottom"), ("shoe", "shoe"), ("top", "coat|top"), ("stripe", "stripe"), ("skin", "skin"),
                 ("hair", "hair"), ("collar", "coat|top"), ("band", "band|scarf"), ("hat", "cap|hat"), ("bag", "bag"), ("pole", "cane|pole"),
                 ("sling", "sling"))
_PERSON_DEFAULT = dict(pole="#9b6a3c", sling="#ffffff")


def draw_person(f, who, x, feet, h, t=0.0, alpha=1.0, parts=None, tint=None, **kw):
    """사람을 칠한다 — (x, feet)가 두 발 가운데. tint=(색, 양)이면 전체를 그 색으로 물들인다(먼 사람·회상)."""
    p = parts or person_parts(who, h, t=t, **kw)
    d = PEOPLE[who]
    bw, bh = p["box"]
    ox, oy = p["origin"]
    cx, cy = x - ox + bw / 2, feet - oy + bh / 2

    def tc(v):
        cc = c(v)
        return lerp(cc, c(tint[0]), tint[1]) if tint else cc

    union = np.zeros_like(p["top"])
    for k, _ in _PERSON_ORDER:
        union = np.maximum(union, p[k])
    if "netrim" in p:
        union = np.maximum(union, p["netrim"])
    stamp(f, grow(union * alpha, 1.6), cx, cy, tc(INK))
    if p["net"].max() > 0:
        stamp(f, p["net"] * alpha * 0.45, cx, cy, tc("#f4f8ff"))
    for k, names in _PERSON_ORDER:
        if p[k].max() <= 0:
            continue
        fill = next((d[n] for n in names.split("|") if isinstance(d.get(n), str)), None) or _PERSON_DEFAULT.get(k)
        if fill:
            stamp(f, p[k] * alpha, cx, cy, tc(fill))
    if "netrim" in p:
        stamp(f, p["netrim"] * alpha, cx, cy, tc("#c9d2dc"))
    stamp(f, rim(union, 3, 3, 3) * 0.14 * alpha, cx, cy, "#ffffff", "add")
    return p


# ══════════════════════════ 손 ══════════════════════════

def hand_parts(s, rot=0.0, pose="open", curl=0.0, sleeve_len=2.2):
    """둥근 장갑 모양 손(손바닥이 아래를 보는 옆모습에 가까운 그림). 원점이 손목, 손가락은 -y(위)."""
    box = int(s * 5.6) + 16
    cc = box / 2
    skin, sleeve = Mask(box, box), Mask(box, box)

    def P(pts):
        return xf(pts, cc, cc, s, rot)

    skin.ellipse(*P([(0, -0.55)])[0], 0.55 * s, 0.62 * s, rot)
    if pose == "point":
        skin.taper(P([(0.05, -0.9), (0.06, -1.7)]), 0.36 * s, 0.3 * s)
        skin.circle(*P([(0.06, -1.7)])[0], 0.16 * s)
        skin.ellipse(*P([(-0.42, -0.62)])[0], 0.2 * s, 0.32 * s, rot - 0.6)
    elif pose == "fist":
        skin.ellipse(*P([(0, -0.62)])[0], 0.62 * s, 0.6 * s, rot)
        skin.ellipse(*P([(-0.46, -0.5)])[0], 0.22 * s, 0.3 * s, rot - 0.4)
    else:   # open — 손가락 넷 + 엄지, curl로 오므린다
        for k, (fx, ln) in enumerate(((-0.33, 0.85), (-0.11, 1.0), (0.11, 0.95), (0.32, 0.78))):
            l = ln * (1 - 0.45 * curl)
            skin.taper(P([(fx, -0.9), (fx * 1.08, -0.9 - l)]), 0.24 * s, 0.2 * s)
            skin.circle(*P([(fx * 1.08, -0.9 - l)])[0], 0.11 * s)
        skin.taper(P([(-0.45, -0.45), (-0.85 + 0.3 * curl, -0.95)]), 0.26 * s, 0.2 * s)
    sleeve.poly(P([(-0.5, 0.0), (0.5, 0.0), (0.56, sleeve_len), (-0.58, sleeve_len)]))
    sleeve.poly(P([(-0.56, -0.08), (0.56, -0.08), (0.56, 0.26), (-0.56, 0.26)]))   # 소맷부리 — 돌려도 뒤집히지 않게 다각형
    return dict(skin=skin.arr(0.5), sleeve=sleeve.arr(0.5), box=box)


def draw_hand(f, x, y, s, rot=0.0, pose="open", curl=0.0, skin="#f6c9a0", sleeve="#3fae6a", alpha=1.0, parts=None,
              tint=None):
    p = parts or hand_parts(s, rot, pose, curl)

    def tc(v):
        cc = c(v)
        return lerp(cc, c(tint[0]), tint[1]) if tint else cc

    union = np.maximum(p["skin"], p["sleeve"]) * alpha
    stamp(f, grow(union, 1.6), x, y, tc(INK))
    stamp(f, p["sleeve"] * alpha, x, y, tc(sleeve))
    stamp(f, p["skin"] * alpha, x, y, tc(skin))
    stamp(f, rim(union, 3, 3, 3) * 0.16, x, y, "#ffffff", "add")
    return p


# ══════════════════════════ 소품 ══════════════════════════

def crate(f, x, base, w, h, lid=0.0, fill="#c98b4a", plank="#a86e34", tag=True, alpha=1.0, seed=0):
    """나무 상자 — 널빤지 줄·못·이름표(판독 불가). lid(0..1)는 뚜껑이 들린 정도."""
    m = local(w + 30, h + 60)
    cx, cy = m.w / 2, m.h - 10 - h / 2
    m.rect(cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2, r=4)
    a = m.arr(0.5) * alpha
    py = base - m.h / 2 + 10
    paint(f, a, fill, x, py, hi=0.12)
    lm = local(w + 30, h + 60)
    for k in range(1, 4):
        yy = cy - h / 2 + h * k / 4
        lm.line([(cx - w / 2 + 4, yy), (cx + w / 2 - 4, yy)], 2.4)
    lm.line([(cx - w / 2 + 6, cy - h / 2 + 6), (cx + w / 2 - 6, cy + h / 2 - 6)], 3.0)
    stamp(f, lm.arr(0.5) * a, x, py, plank)
    if tag:
        tm = local(w + 30, h + 60)
        tm.rect(cx - w * 0.18, cy - h * 0.14, cx + w * 0.18, cy + h * 0.12, r=3)
        ta = tm.arr(0.5) * a
        stamp(f, grow(ta, 1.2), x, py, INK)
        stamp(f, ta, x, py, "#f3ead2")
        sm = local(w + 30, h + 60)
        scribble(sm, cx - w * 0.14, cy - h * 0.08, w * 0.28, 2, seed, row_h=max(6, h * 0.08), weight=1.6)
        stamp(f, sm.arr(0.4) * a, x, py, "#7a6a58")
    if lid > 0:
        lm2 = local(w + 30, h + 60)
        lm2.poly(rotate_pts(rect_pts(cx - w / 2 - 4, cy - h / 2 - 12, cx + w / 2 + 4, cy - h / 2),
                            cx - w / 2, cy - h / 2, -0.9 * lid, 0))
        paint(f, lm2.arr(0.5) * alpha, fill, x, py, hi=0.1)


def book(f, x, y, w, h, open_=1.0, cover="#7a4a2e", page="#fbf3df", lines_seed=0, columns=1, progress=1.0,
         count_column=False, alpha=1.0):
    """책·장부 — 펼침 정도(open_), 판독 불가 줄(progress로 써 내려간다)."""
    m = local(w * 1.1 + 20, h * 1.2 + 20)
    cx, cy = m.w / 2, m.h / 2
    hw = w / 2 * (0.5 + 0.5 * open_)
    m.rect(cx - hw - 6, cy - h / 2 - 6, cx + hw + 6, cy + h / 2 + 6, r=6)
    paint(f, m.arr(0.5) * alpha, cover, x, y, hi=0.08)
    if open_ > 0.2:
        pm = local(w * 1.1 + 20, h * 1.2 + 20)
        pm.rect(cx - hw, cy - h / 2, cx - 3, cy + h / 2, r=3)
        pm.rect(cx + 3, cy - h / 2, cx + hw, cy + h / 2, r=3)
        stamp(f, pm.arr(0.5) * alpha * span(open_, 0.2, 0.6), x, y, page)
        sm = local(w * 1.1 + 20, h * 1.2 + 20)
        rows = max(2, int(h / 26))
        for side in (-1, 1):
            x0 = cx - hw + 12 if side < 0 else cx + 14
            ww = hw - 26
            pr = max(0.0, min(1.0, progress * 2 - (0 if side < 0 else 1)))
            if pr > 0:
                scribble(sm, x0, cy - h / 2 + 16, ww * (0.62 if count_column else 1.0), rows, lines_seed + 2 + side,
                         row_h=h / (rows + 1), weight=1.8, progress=pr)
                if count_column:
                    scribble(sm, x0 + ww * 0.72, cy - h / 2 + 16, ww * 0.26, rows, lines_seed + 9 + side,
                             row_h=h / (rows + 1), weight=1.8, progress=pr)
        stamp(f, sm.arr(0.4) * alpha * span(open_, 0.4, 0.8), x, y, "#5a4a3c")


def slab(f, x, base, w, h, fill="#c7c3bb", glyph=0.0, seed=0, alpha=1.0, glow=0.0):
    """선 석판(둥근 머리) — glyph(0..1)로 새김 무늬가 드러난다."""
    m = local(w + 20, h + 20)
    cx = m.w / 2
    m.rect(cx - w / 2, 10 + w * 0.3, cx + w / 2, m.h - 10)
    m.ellipse(cx, 10 + w * 0.32, w / 2, w * 0.3)
    a = m.arr(0.5) * alpha
    py = base - m.h / 2 + 10
    if glow > 0:
        stamp(f, blur_arr(a, 16) * glow, x, py, PAL["glow"], "add")
    paint(f, a, fill, x, py, hi=0.12)
    if glyph > 0:
        gm = local(w + 20, h + 20)
        glyphs(gm, cx - w * 0.32, 10 + w * 0.5 + 20, w * 0.64, max(1, int(h / 60)), seed, size=14, weight=2.6,
               progress=glyph)
        stamp(f, gm.arr(0.4) * a, x, py, INK)


def lantern_glow(f, x, y, r, color="#ffd27a", amount=0.6):
    add(f, c(color), radial(W, H, x, y, r, 1.8) * amount)


def sparkle(f, x, y, size, color="#fff6c0", amount=1.0, rot=0.0):
    """반짝 — 네 갈래 별."""
    m = local(size * 5.2, size * 5.2)   # 번짐(반경 0.4×size)이 상자 끝에서 잘리지 않게 넉넉히
    cc = m.w / 2
    m.poly(xf([(0, -1.2), (0.18, -0.18), (1.2, 0), (0.18, 0.18), (0, 1.2), (-0.18, 0.18), (-1.2, 0), (-0.18, -0.18)],
              cc, cc, size, rot))
    a = m.arr(0.4) * amount
    stamp(f, blur_arr(a, size * 0.4), x, y, color, "add")
    stamp(f, a, x, y, "#ffffff", "add")


def mist(f, seed, t, y0, y1, color="#ffffff", amount=0.4, speed=12.0, w=W):
    """천천히 흐르는 안개 띠(가산 아님 — 덮는다)."""
    n = _mist_field(seed)
    a = crop(n, 200 + t * speed, 0, w, H)
    yy = np.arange(H, dtype=np.float32)[:, None]
    band = np.clip(1 - np.abs(yy - (y0 + y1) / 2) / max(1, (y1 - y0) / 2), 0, 1) ** 1.2
    over(f, c(color), np.clip(a * band * amount * 1.6, 0, 1))


_MIST = {}


def _mist_field(seed):
    if seed not in _MIST:
        _MIST[seed] = fbm(W + 600, H, seed, (260, 90), (0.7, 0.3))
    return _MIST[seed]


# ══════════════════════════ 마무리 ══════════════════════════

def finish(f, paper=None, tint="#fff8ec", vign=None):
    """그림책 마감 — 종이 결 + 아주 약한 색조. 비네트는 render.py가 건다."""
    apply_paper(f, paper)
    return f
