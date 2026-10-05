"""수문장 배지 렌더러 — 13개 배지와 획득 연출용 보조 텍스처를 PNG로 굽는다.

    python -X utf8 Tools/Badges/guardian_badges.py [--sheet]

산출: Assets/Resources/UI/Badges/badge_<regionId>.png (256×256 RGBA) 13장 +
      badge_rays.png · badge_glow.png · badge_sparkle.png (획득 연출).
--sheet면 Artifacts/badges-preview.png에 밝은/어두운 바탕 대조 시트도 떨군다.

**배지 목록의 단일 출처는 코드다**(`Assets/Scripts/Core/GuardianBadges.cs`). 여기 ID가 그쪽과 어긋나면
`GuardianBadgeTests`가 "그림 없음"으로 잡는다 — 리전을 늘리면 두 곳을 함께 고친다.

그리는 법: 1024 캔버스에 도형 마스크를 그리고(4배 슈퍼샘플), 마스크를 흐린 높이맵의 기울기로
빛을 먹여 금속 테두리·문양을 양각으로 만든다. 법랑(가운데 색 면)은 동심원 무늬로 결을 준다.
numpy + PIL만 쓴다(scipy 없음 — 침식은 "흐림 후 문턱"으로 근사한다).

알파 가장자리: Unity 기본 임포트는 alphaIsTransparency가 꺼져 있어 투명 픽셀의 RGB(검정)가
쌍선형 필터로 번져 검은 테두리가 생긴다. 저장 직전에 가장자리 색을 투명 영역으로 번져 둔다.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "UI", "Badges")
SHEET = os.path.join(ROOT, "Artifacts", "badges-preview.png")

S = 1024          # 작업 캔버스(슈퍼샘플)
C = S / 2
FINAL = 256

# ── 금속 팔레트 (위 → 아래) ──
GOLD = ((1.00, 0.89, 0.55), (0.62, 0.42, 0.14))
SILVER = ((0.96, 0.97, 1.00), (0.48, 0.53, 0.62))
IRON = ((0.62, 0.58, 0.70), (0.16, 0.14, 0.20))


# ────────────────────────── 마스크 도구 ──────────────────────────

def canvas():
    img = Image.new("L", (S, S), 0)
    return img, ImageDraw.Draw(img)


def arr(img):
    return np.asarray(img, dtype=np.float32) / 255.0


def to_img(a):
    return Image.fromarray(np.clip(a * 255.0 + 0.5, 0, 255).astype(np.uint8), "L")


def blur(a, r):
    if r <= 0:
        return a
    return arr(to_img(a).filter(ImageFilter.GaussianBlur(r)))


def smooth(a, lo, hi):
    t = np.clip((a - lo) / (hi - lo), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def rounded(a, r):
    """모서리를 둥글린다 — 흐린 뒤 반값 문턱."""
    return smooth(blur(a, r), 0.42, 0.58)


def erode(a, d):
    """d픽셀 안쪽으로 줄인다(근사). 직선 가장자리에서 거리 d의 가우시안 값 ≈ 0.84."""
    return smooth(blur(a, d), 0.80, 0.88)


def dilate(a, d):
    return smooth(blur(a, d), 0.12, 0.20)


def pts_ellipse(cx, cy, rx, ry, rot=0.0, n=120):
    cr, sr = math.cos(rot), math.sin(rot)
    out = []
    for i in range(n):
        t = 2 * math.pi * i / n
        x, y = rx * math.cos(t), ry * math.sin(t)
        out.append((cx + x * cr - y * sr, cy + x * sr + y * cr))
    return out


def ellipse(d, cx, cy, rx, ry, rot=0.0, fill=255):
    d.polygon(pts_ellipse(cx, cy, rx, ry, math.radians(rot)), fill=fill)


def circle(d, cx, cy, r, fill=255):
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=fill)


def bezier(p0, p1, p2, p3, n=48):
    out = []
    for i in range(n + 1):
        t = i / n
        u = 1 - t
        x = u ** 3 * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t ** 3 * p3[0]
        y = u ** 3 * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t ** 3 * p3[1]
        out.append((x, y))
    return out


def stroke(d, pts, w, fill=255):
    """둥근 끝 선 — PIL 선은 끝이 각져서 양 끝에 원을 얹는다."""
    d.line(pts, fill=fill, width=int(w), joint="curve")
    r = w / 2
    for p in (pts[0], pts[-1]):
        circle(d, p[0], p[1], r, fill)


def taper(d, pts, w0, w1, fill=255):
    """굵기가 줄어드는 선 — 뿔·꼬리."""
    n = len(pts) - 1
    for i in range(n):
        w = w0 + (w1 - w0) * (i / max(1, n - 1))
        stroke(d, [pts[i], pts[i + 1]], max(2.0, w), fill)


def mirror(fn):
    """좌우 대칭 문양 — fn(d, sign)을 양쪽으로."""
    def both(d):
        fn(d, -1)
        fn(d, 1)
    return both


# ────────────────────────── 겉모양 13종 ──────────────────────────

def shape_leaf():
    img, d = canvas()
    a, b = 300.0, 430.0
    R = (a * a + b * b) / (2 * a)
    off = R - a
    m1, d1 = canvas()
    circle(d1, C - off, C, R)
    m2, d2 = canvas()
    circle(d2, C + off, C, R)
    lens = np.minimum(arr(m1), arr(m2))
    return rounded(lens, 10)


def shape_drop():
    img, d = canvas()
    oy, R = C + 80, 330
    circle(d, C, oy, R)
    d.polygon([(C, C - 425), (C + 248, oy - 218), (C - 248, oy - 218)], fill=255)
    return rounded(arr(img), 14)


def shape_shield():
    img, d = canvas()
    top, side = C - 390, C + 40
    pts = [(C - 350, top), (C - 120, top - 18), (C, top + 12), (C + 120, top - 18), (C + 350, top), (C + 350, side)]
    pts += bezier((C + 350, side), (C + 350, C + 250), (C + 150, C + 360), (C, C + 425))[1:]
    pts += bezier((C, C + 425), (C - 150, C + 360), (C - 350, C + 250), (C - 350, side))[1:]
    d.polygon(pts, fill=255)
    return rounded(arr(img), 16)


def shape_flower():
    img, d = canvas()
    for k in range(5):
        t = math.radians(-90 + 72 * k)
        circle(d, C + 235 * math.cos(t), C + 235 * math.sin(t), 190)
    circle(d, C, C, 270)
    return rounded(arr(img), 12)


def shape_cloud():
    img, d = canvas()
    for cx, cy, r in ((C - 210, C + 40, 210), (C + 210, C + 40, 210), (C - 90, C - 130, 250),
                      (C + 140, C - 120, 230), (C, C + 120, 270)):
        circle(d, cx, cy, r)
    d.rectangle([C - 330, C + 40, C + 330, C + 330], fill=255)
    return rounded(arr(img), 30)


def shape_mountain():
    img, d = canvas()
    d.polygon([(C - 405, C + 350), (C - 405, C + 60), (C - 60, C - 425), (C + 130, C - 150),
               (C + 245, C - 270), (C + 405, C + 30), (C + 405, C + 350)], fill=255)
    return rounded(arr(img), 26)


def shape_sun():
    img, d = canvas()
    circle(d, C, C, 305)
    for k in range(16):
        t = math.radians(22.5 * k - 90)
        tip = 425 if k % 2 == 0 else 370
        w = math.radians(8.5)
        d.polygon([(C + tip * math.cos(t), C + tip * math.sin(t)),
                   (C + 290 * math.cos(t + w), C + 290 * math.sin(t + w)),
                   (C + 290 * math.cos(t - w), C + 290 * math.sin(t - w))], fill=255)
    return rounded(arr(img), 8)


def shape_circle():
    img, d = canvas()
    circle(d, C, C, 410)
    return arr(img)


def shape_hexagon():
    img, d = canvas()
    d.polygon([(C + 425 * math.cos(math.radians(60 * k - 90)), C + 425 * math.sin(math.radians(60 * k - 90)))
               for k in range(6)], fill=255)
    return rounded(arr(img), 34)


def shape_snowflake():
    img, d = canvas()
    for k in range(6):
        t = math.radians(60 * k - 90)
        w = math.radians(17)
        d.polygon([(C + 432 * math.cos(t), C + 432 * math.sin(t)),
                   (C + 250 * math.cos(t + w), C + 250 * math.sin(t + w)),
                   (C + 250 * math.cos(t - w), C + 250 * math.sin(t - w))], fill=255)
    circle(d, C, C, 318)
    return rounded(arr(img), 12)


def shape_flame():
    img, d = canvas()
    circle(d, C, C + 120, 300)
    pts = [(C - 300, C + 120)]
    pts += bezier((C - 300, C + 120), (C - 330, C - 60), (C - 240, C - 150), (C - 190, C - 250))[1:]
    pts += bezier((C - 190, C - 250), (C - 150, C - 160), (C - 110, C - 150), (C - 80, C - 170))[1:]
    pts += bezier((C - 80, C - 170), (C - 90, C - 300), (C - 20, C - 390), (C + 20, C - 430))[1:]
    pts += bezier((C + 20, C - 430), (C + 40, C - 330), (C + 110, C - 250), (C + 150, C - 240))[1:]
    pts += bezier((C + 150, C - 240), (C + 170, C - 280), (C + 190, C - 300), (C + 230, C - 330))[1:]
    pts += bezier((C + 230, C - 330), (C + 310, C - 180), (C + 330, C - 20), (C + 300, C + 120))[1:]
    d.polygon(pts, fill=255)
    return rounded(arr(img), 16)


def shape_crown():
    img, d = canvas()
    for k in range(5):
        t = math.radians(-160 + 35 * k)
        circle(d, C + 250 * math.cos(t), C - 10 + 250 * math.sin(t), 175)
    circle(d, C, C + 20, 300)
    d.rectangle([C - 360, C + 20, C + 360, C + 250], fill=255)
    d.rounded_rectangle([C - 360, C + 60, C + 360, C + 380], radius=120, fill=255)
    return rounded(arr(img), 20)


def shape_squircle():
    img, d = canvas()
    d.rounded_rectangle([C - 385, C - 385, C + 385, C + 385], radius=150, fill=255)
    return rounded(arr(img), 8)


# ────────────────────────── 문양 13종 ──────────────────────────

def emb_mantis(d, cy=0.0, scale=1.0):
    k = scale

    def P(x, y):
        return (C + x * k, C + (y + cy) * k)

    # 머리(역삼각) + 겹눈
    d.polygon([P(-92, -228), P(92, -228), P(0, -120)], fill=255)
    circle(d, *P(-86, -232), 40 * k)
    circle(d, *P(86, -232), 40 * k)
    # 더듬이
    stroke(d, bezier(P(-26, -260), P(-50, -330), P(-110, -360), P(-150, -370)), 12 * k)
    stroke(d, bezier(P(26, -260), P(50, -330), P(110, -360), P(150, -370)), 12 * k)
    # 가슴 · 배
    stroke(d, [P(0, -130), P(0, 40)], 46 * k)
    d.polygon(pts_ellipse(*P(0, 175), 72 * k, 150 * k), fill=255)
    # 접은 낫다리(겉)
    for s in (-1, 1):
        stroke(d, [P(18 * s, -80), P(128 * s, -10)], 34 * k)
        stroke(d, [P(128 * s, -10), P(70 * s, -150)], 30 * k)
        d.polygon([P(70 * s, -150), P(40 * s, -205), P(92 * s, -160)], fill=255)
        # 걷는 다리
        stroke(d, [P(12 * s, 10), P(120 * s, 90), P(150 * s, 190)], 14 * k)
        stroke(d, [P(12 * s, 40), P(100 * s, 170), P(118 * s, 280)], 14 * k)


def emb_dragonfly(d):
    y0 = 40
    circle(d, C, C - 175 + y0, 50)
    circle(d, C - 40, C - 190 + y0, 34)
    circle(d, C + 40, C - 190 + y0, 34)
    stroke(d, [(C, C - 140 + y0), (C, C - 40 + y0)], 58)
    for i in range(7):
        y = C - 30 + y0 + i * 44
        w = 42 - i * 3.5
        stroke(d, [(C, y), (C, y + 40)], w)
    for s in (-1, 1):
        ellipse(d, C + s * 140, C - 95 + y0, 150, 44, rot=-10 * s)
        ellipse(d, C + s * 130, C - 20 + y0, 132, 40, rot=12 * s)


def emb_beetle(d):
    # 딱지날개 · 앞가슴 · 머리 + 긴 윗뿔
    ellipse(d, C, C + 110, 150, 185)
    ellipse(d, C, C - 95, 118, 82)
    ellipse(d, C, C - 170, 58, 40)
    taper(d, bezier((C, C - 150), (C - 10, C - 250), (C + 40, C - 330), (C + 95, C - 360), n=24), 58, 16)
    stroke(d, [(C + 95, C - 360), (C + 110, C - 330)], 16)
    for s in (-1, 1):
        stroke(d, [(C + 100 * s, C - 60), (C + 205 * s, C - 120), (C + 235 * s, C - 200)], 18)
        stroke(d, [(C + 120 * s, C + 40), (C + 235 * s, C + 50), (C + 265 * s, C + 120)], 18)
        stroke(d, [(C + 120 * s, C + 170), (C + 215 * s, C + 250), (C + 225 * s, C + 330)], 18)


def emb_beetle_grooves(d):
    d.line([(C, C - 60), (C, C + 290)], fill=255, width=10)
    d.line([(C - 110, C - 30), (C + 110, C - 30)], fill=255, width=8)


def emb_butterfly(d, y0=0.0, s_=1.0, tails=True):
    def P(x, y):
        return (C + x * s_, C + (y + y0) * s_)

    for s in (-1, 1):
        d.polygon(pts_ellipse(*P(135 * s, -70), 150 * s_, 105 * s_, math.radians(-32 * s)), fill=255)
        d.polygon(pts_ellipse(*P(100 * s, 85), 102 * s_, 86 * s_, math.radians(28 * s)), fill=255)
        if tails:
            stroke(d, [P(118 * s, 140), P(140 * s, 250)], 24 * s_)
        stroke(d, bezier(P(8 * s, -150), P(20 * s, -220), P(50 * s, -260), P(80 * s, -280)), 10 * s_)
        circle(d, *P(82 * s, -282), 16 * s_)
    stroke(d, [P(0, -140), P(0, 160)], 36 * s_)
    circle(d, *P(0, -160), 30 * s_)


def emb_butterfly_veins(d, y0=0.0, s_=1.0):
    def P(x, y):
        return (C + x * s_, C + (y + y0) * s_)
    for s in (-1, 1):
        d.line([P(30 * s, -60), P(220 * s, -150)], fill=255, width=int(8 * s_))
        d.line([P(30 * s, -40), P(200 * s, -20)], fill=255, width=int(8 * s_))
        d.line([P(30 * s, 40), P(150 * s, 120)], fill=255, width=int(8 * s_))


def emb_swamp(d):
    emb_mantis_head(d, -95)
    for i, y in enumerate((C + 45, C + 130, C + 215)):
        amp, span = 22, 250 - i * 30
        pts = [(C - span + 2 * span * t / 60, y + amp * math.sin(t / 60 * math.pi * 3 + i)) for t in range(61)]
        stroke(d, pts, 30)


def emb_mantis_head(d, y0):
    # 사마귀 머리 정면 — 넓은 역삼각 + 원뿔 겹눈이 위로 솟고, 입 쪽은 좁고 뾰족하다.
    d.polygon([(C - 175, C - 40 + y0), (C - 60, C - 70 + y0), (C + 60, C - 70 + y0), (C + 175, C - 40 + y0),
               (C + 40, C + 110 + y0), (C, C + 150 + y0), (C - 40, C + 110 + y0)], fill=255)
    for s in (-1, 1):
        ellipse(d, C + 150 * s, C - 80 + y0, 52, 82, rot=28 * s)
        stroke(d, bezier((C + 30 * s, C - 70 + y0), (C + 50 * s, C - 170 + y0), (C + 120 * s, C - 230 + y0),
                         (C + 200 * s, C - 250 + y0)), 12)


def emb_swamp_eyes(d):
    # 겹눈의 세로 동공 — 안개 속에서 이쪽을 보는 눈.
    for s in (-1, 1):
        ellipse(d, C + 150 * s, C - 172, 12, 40, rot=28 * s)
    d.line([(C - 60, C - 70), (C, C + 20), (C + 60, C - 70)], fill=255, width=8)


def emb_moth(d, y0=0.0, s_=1.0):
    def P(x, y):
        return (C + x * s_, C + (y + y0) * s_)
    for s in (-1, 1):
        d.polygon(pts_ellipse(*P(165 * s, -40), 185 * s_, 118 * s_, math.radians(-14 * s)), fill=255)
        d.polygon(pts_ellipse(*P(118 * s, 88), 128 * s_, 102 * s_, math.radians(22 * s)), fill=255)
        # 갈고리 날개 끝(아틀라스)
        d.polygon(pts_ellipse(*P(318 * s, -92), 42 * s_, 34 * s_, math.radians(-30 * s)), fill=255)
        # 깃털 더듬이
        d.polygon(pts_ellipse(*P(62 * s, -168), 22 * s_, 62 * s_, math.radians(38 * s)), fill=255)
    stroke(d, [P(0, -135), P(0, 150)], 44 * s_)
    circle(d, *P(0, -145), 32 * s_)


def emb_moth_eyes(d, y0=0.0, s_=1.0):
    def P(x, y):
        return (C + x * s_, C + (y + y0) * s_)
    for s in (-1, 1):
        circle(d, *P(175 * s, -40), 34 * s_)
        circle(d, *P(120 * s, 92), 24 * s_)


def emb_scarab(d):
    ellipse(d, C, C + 110, 128, 150)
    ellipse(d, C, C - 40, 112, 66)
    d.pieslice([C - 70, C - 150, C + 70, C - 30], 180, 360, fill=255)
    for k in range(-2, 3):
        d.polygon([(C + k * 28 - 12, C - 88), (C + k * 28 + 12, C - 88), (C + k * 28, C - 122)], fill=255)
    circle(d, C, C - 220, 62)
    for s in (-1, 1):
        stroke(d, [(C + 60 * s, C - 80), (C + 130 * s, C - 150), (C + 58 * s, C - 225)], 20)
        stroke(d, [(C + 110 * s, C + 20), (C + 210 * s, C + 20), (C + 238 * s, C + 90)], 18)
        stroke(d, [(C + 110 * s, C + 150), (C + 205 * s, C + 220), (C + 205 * s, C + 300)], 18)


def emb_scarab_grooves(d):
    d.line([(C, C - 10), (C, C + 255)], fill=255, width=10)
    circle(d, C, C - 220, 36)


def emb_scythe(d):
    img, dd = canvas()
    circle(dd, C + 30, C + 10, 250)
    cut, dc = canvas()
    circle(dc, C + 125, C - 45, 232)
    blade = np.clip(arr(img) - arr(cut), 0, 1)
    d.bitmap((0, 0), to_img(blade), fill=255)
    # 이빨 세 개
    for t in (200, 235, 270):
        a = math.radians(t)
        x, y = C + 30 + 250 * math.cos(a), C + 10 + 250 * math.sin(a)
        d.polygon([(x, y), (x + 34 * math.cos(a + 1.9), y + 34 * math.sin(a + 1.9)),
                   (x + 40 * math.cos(a), y + 40 * math.sin(a))], fill=255)
    stroke(d, [(C + 60, C + 250), (C + 150, C + 150)], 44)


def emb_hornet(d, y0=0.0, s_=1.0):
    def P(x, y):
        return (C + x * s_, C + (y + y0) * s_)
    circle(d, *P(0, -205), 56 * s_)
    d.polygon(pts_ellipse(*P(0, -108), 78 * s_, 70 * s_), fill=255)
    d.polygon(pts_ellipse(*P(0, 110), 98 * s_, 155 * s_), fill=255)
    d.polygon([P(-26, 250), P(26, 250), P(0, 318)], fill=255)
    for s in (-1, 1):
        d.polygon(pts_ellipse(*P(145 * s, -95), 150 * s_, 42 * s_, math.radians(-28 * s)), fill=255)
        d.polygon(pts_ellipse(*P(130 * s, -40), 118 * s_, 34 * s_, math.radians(-10 * s)), fill=255)
        stroke(d, [P(20 * s, -250), P(60 * s, -300), P(98 * s, -300)], 12 * s_)
        stroke(d, [P(50 * s, -70), P(150 * s, 20), P(170 * s, 110)], 14 * s_)


def emb_hornet_stripes(d, y0=0.0, s_=1.0):
    def P(x, y):
        return (C + x * s_, C + (y + y0) * s_)
    for yy in (40, 105, 170):
        d.line([P(-110, yy), P(110, yy)], fill=255, width=int(20 * s_))


def emb_aurora(d):
    emb_moth(d, 60, 0.78)
    for i, r in enumerate((255, 300)):
        box = [C - r, C + 40 - r, C + r, C + 40 + r]
        d.arc(box, 205, 335, fill=255, width=26)


def emb_ember(d):
    # 용암말벌 — 날개가 옆으로 뻗은 불꽃 두 갈래다(위로 세우면 박쥐처럼 읽힌다).
    for s in (-1, 1):
        pts = [(C + 30 * s, C - 40)]
        pts += bezier((C + 30 * s, C - 40), (C + 90 * s, C - 120), (C + 180 * s, C - 170), (C + 275 * s, C - 195))[1:]
        pts += bezier((C + 275 * s, C - 195), (C + 235 * s, C - 150), (C + 215 * s, C - 128), (C + 205 * s, C - 108))[1:]
        pts += bezier((C + 205 * s, C - 108), (C + 235 * s, C - 98), (C + 265 * s, C - 78), (C + 290 * s, C - 38))[1:]
        pts += bezier((C + 290 * s, C - 38), (C + 220 * s, C - 18), (C + 120 * s, C + 12), (C + 30 * s, C + 12))[1:]
        d.polygon(pts, fill=255)
    d.polygon(pts_ellipse(C, C + 105, 84, 130), fill=255)
    d.polygon([(C - 26, C + 215), (C + 26, C + 215), (C, C + 305)], fill=255)
    ellipse(d, C, C - 10, 62, 56)
    circle(d, C, C - 95, 50)
    for s in (-1, 1):
        stroke(d, bezier((C + 22 * s, C - 130), (C + 40 * s, C - 200), (C + 80 * s, C - 230), (C + 120 * s, C - 232)), 11)
        stroke(d, [(C + 24 * s, C - 60), (C + 44 * s, C - 38)], 12)
    for x, y, r in ((C - 150, C + 170, 18), (C + 170, C + 140, 14), (C + 120, C + 230, 11), (C - 95, C + 250, 12)):
        d.polygon([(x, y - r * 1.6), (x + r, y), (x, y + r * 1.6), (x - r, y)], fill=255)


def emb_ember_stripes(d):
    for yy in (60, 115, 170):
        d.line([(C - 95, C + yy), (C + 95, C + yy)], fill=255, width=16)
    for s in (-1, 1):
        circle(d, C + 26 * s, C - 105, 12)


def emb_worldtree(d):
    # 몸통이 곧 나무 줄기 — 뿌리로 내려간다. 날개는 잎 모양.
    stroke(d, [(C, C - 170), (C, C + 230)], 46)
    circle(d, C, C - 190, 32)
    for s in (-1, 1):
        stroke(d, bezier((C, C + 200), (C + 30 * s, C + 250), (C + 90 * s, C + 270), (C + 150 * s, C + 300)), 20)
        leaf(d, C + 165 * s, C - 105, 165, 84, -32 * s)
        leaf(d, C + 135 * s, C + 85, 118, 64, 34 * s)
        stroke(d, bezier((C + 8 * s, C - 210), (C + 20 * s, C - 270), (C + 60 * s, C - 300), (C + 95 * s, C - 305)), 10)
    stroke(d, [(C, C + 220), (C, C + 305)], 20)


def leaf(d, cx, cy, rx, ry, rot):
    # 뾰족한 잎 — 두 원의 교집합을 회전
    img, dd = canvas()
    a, b = ry, rx
    R = (a * a + b * b) / (2 * a)
    off = R - a
    m1, d1 = canvas()
    circle(d1, C, C - off, R)
    m2, d2 = canvas()
    circle(d2, C, C + off, R)
    lens = to_img(np.minimum(arr(m1), arr(m2)))
    lens = lens.rotate(rot, resample=Image.Resampling.BICUBIC, center=(C, C), translate=(cx - C, cy - C))
    d.bitmap((0, 0), lens, fill=255)


def emb_worldtree_veins(d):
    # PIL의 rotate는 반시계 — 잎 축 방향은 (cos, -sin)이다.
    for s in (-1, 1):
        for cx, cy, half, deg in ((C + 165 * s, C - 105, 140, -32 * s), (C + 135 * s, C + 85, 96, 34 * s)):
            a = math.radians(deg)
            ux, uy = math.cos(a), -math.sin(a)
            d.line([(cx - half * ux, cy - half * uy), (cx + half * ux, cy + half * uy)], fill=255, width=8)
            for t in (-0.45, 0.0, 0.45):
                bx, by = cx + t * half * ux, cy + t * half * uy
                d.line([(bx, by), (bx + 0.32 * half * (ux * 0.6 - uy), by + 0.32 * half * (uy * 0.6 + ux))], fill=255, width=6)
                d.line([(bx, by), (bx + 0.32 * half * (ux * 0.6 + uy), by + 0.32 * half * (uy * 0.6 - ux))], fill=255, width=6)


def emb_blank(d):
    # 빈칸 — 점선 네모. 안은 비어 있다.
    h = 165
    seg = 2 * h / 5
    for i in range(5):
        if i % 2 == 1:
            continue
        a, b = -h + i * seg, -h + (i + 1) * seg
        for (x0, y0, x1, y1) in ((a, -h, b, -h), (a, h, b, h), (-h, a, -h, b), (h, a, h, b)):
            stroke(d, [(C + x0, C + y0), (C + x1, C + y1)], 26)


# ────────────────────────── 배지 표 ──────────────────────────
# (regionId, 겉모양, 금속, 법랑색, 문양, 홈(음각), 테 두께)
BADGES = [
    ("meadow", shape_leaf, GOLD, (0.34, 0.70, 0.28), lambda d: emb_mantis(d, 30, 0.78), None, 34),
    ("pond", shape_drop, GOLD, (0.18, 0.50, 0.90), emb_dragonfly, None, 36),
    ("forest", shape_shield, GOLD, (0.15, 0.42, 0.20), emb_beetle, emb_beetle_grooves, 38),
    ("garden", shape_flower, GOLD, (0.92, 0.46, 0.63), lambda d: emb_butterfly(d, 30, 0.82), lambda d: emb_butterfly_veins(d, 30, 0.82), 34),
    ("swamp", shape_cloud, GOLD, (0.24, 0.40, 0.34), emb_swamp, emb_swamp_eyes, 38),
    ("mountain", shape_mountain, GOLD, (0.56, 0.46, 0.38), lambda d: emb_moth(d, 110, 0.86), lambda d: emb_moth_eyes(d, 110, 0.86), 36),
    ("ruins", shape_sun, GOLD, (0.14, 0.28, 0.64), emb_scarab, emb_scarab_grooves, 34),
    ("hollow", shape_circle, SILVER, (0.17, 0.17, 0.19), emb_scythe, None, 44),
    ("dunes", shape_hexagon, SILVER, (0.86, 0.64, 0.28), lambda d: emb_hornet(d, 0, 0.9), lambda d: emb_hornet_stripes(d, 0, 0.9), 38),
    ("frostline", shape_snowflake, SILVER, (0.50, 0.76, 0.92), emb_aurora, lambda d: emb_moth_eyes(d, 60, 0.78), 32),
    ("emberfall", shape_flame, SILVER, (0.78, 0.24, 0.13), emb_ember, emb_ember_stripes, 34),
    ("canopy", shape_crown, SILVER, (0.22, 0.58, 0.34), emb_worldtree, emb_worldtree_veins, 36),
    ("nameless", shape_squircle, IRON, (0.12, 0.10, 0.16), emb_blank, None, 40),
]


# ────────────────────────── 조명 ──────────────────────────

L = np.array([-0.52, -0.66, 0.54], dtype=np.float32)
L /= np.linalg.norm(L)
H = L + np.array([0, 0, 1], dtype=np.float32)
H /= np.linalg.norm(H)
YY, XX = np.mgrid[0:S, 0:S].astype(np.float32)
RR = np.sqrt((XX - C) ** 2 + (YY - C) ** 2)


def lighting(height, strength):
    gy, gx = np.gradient(height)
    nx, ny, nz = -gx * strength, -gy * strength, np.ones_like(height)
    inv = 1.0 / np.sqrt(nx * nx + ny * ny + nz * nz)
    nx, ny, nz = nx * inv, ny * inv, nz * inv
    lam = np.clip(nx * L[0] + ny * L[1] + nz * L[2], 0, 1)
    spec = np.clip(nx * H[0] + ny * H[1] + nz * H[2], 0, 1) ** 36
    return lam, spec


def metal(palette, lam, spec, tint=1.0):
    top, bot = np.array(palette[0]), np.array(palette[1])
    t = np.clip((YY - (C - 430)) / 860.0, 0, 1)[..., None]
    base = top * (1 - t) + bot * t
    # 금속은 명암 폭이 넓다 — 어두운 쪽을 깊게, 밝은 쪽은 흰 하이라이트로.
    col = base * (0.28 + 0.95 * lam[..., None]) * tint + spec[..., None] * 0.85
    return np.clip(col, 0, 1)


def render(region, shape_fn, pal, enamel, emb_fn, groove_fn, rim):
    outer = shape_fn()
    inner = erode(outer, rim)
    rim_band = np.clip(outer - inner, 0, 1)

    # 문양 마스크 — 법랑 안쪽 여백 안으로 자른다.
    em_img, ed = canvas()
    emb_fn(ed)
    emblem = arr(em_img)
    safe = erode(inner, 18)
    emblem = np.minimum(rounded(emblem, 3), safe)
    groove = np.zeros_like(emblem)
    if groove_fn is not None:
        g_img, gd = canvas()
        groove_fn(gd)
        groove = np.minimum(blur(arr(g_img), 2), emblem)

    rgb = np.zeros((S, S, 3), np.float32)
    alpha = np.zeros((S, S), np.float32)

    # 1) 그림자
    sh = blur(np.roll(np.roll(outer, 16, axis=0), 6, axis=1), 22)
    alpha = sh * 0.55

    # 2) 테두리 금속 — 띠를 흐린 높이맵. 바깥 가장자리 쪽이 둥글게 솟는다.
    h_rim = blur(rim_band, rim * 0.45) + blur(outer, 6) * 0.35
    lam, spec = lighting(h_rim, 60.0)
    rim_col = metal(pal, lam, spec)

    # 3) 법랑 — 동심원 결 + 위 밝고 아래 어두운 기울기 + 테가 드리운 안쪽 그림자
    en = np.array(enamel, np.float32)
    grad = 1.12 - 0.30 * np.clip((YY - (C - 400)) / 800.0, 0, 1)
    guil = 1.0 + 0.045 * np.sin(RR * 0.16) * smooth(RR, 40, 120)
    vign = 1.0 - 0.22 * smooth(RR, 180, 420)
    en_col = en[None, None, :] * (grad * guil * vign)[..., None]
    inner_sh = blur(np.roll(np.roll(1 - inner, 14, axis=0), 10, axis=1), 12) * inner
    en_col *= (1 - 0.55 * inner_sh)[..., None]

    # 4) 문양 금속 — 문양을 흐린 높이맵. 음각 홈은 높이를 깎는다.
    h_em = blur(emblem, 11) + blur(emblem, 4) * 0.5 - groove * 0.7
    lam_e, spec_e = lighting(h_em, 70.0)
    em_col = metal(pal, lam_e, spec_e, tint=1.06)
    em_sh = blur(np.roll(np.roll(emblem, 9, axis=0), 6, axis=1), 6) * inner * (1 - emblem)
    en_col *= (1 - 0.5 * em_sh)[..., None]

    # 합성 (아래 → 위)
    face = inner[..., None] * en_col + (1 - inner[..., None]) * rim_col
    face = emblem[..., None] * em_col + (1 - emblem[..., None]) * face

    # 5) 유광 — 위쪽에 넓고 흐린 흰 반사
    gloss_img, gd2 = canvas()
    ellipse(gd2, C - 70, C - 190, 330, 210, rot=-18)
    gloss = blur(arr(gloss_img), 40) * outer * np.clip(1.0 - (YY - (C - 420)) / 700.0, 0, 1)
    face = face + gloss[..., None] * 0.20

    rgb = face
    a_face = outer
    out_a = a_face + alpha * (1 - a_face)
    out_rgb = rgb * (a_face / np.maximum(out_a, 1e-4))[..., None]   # 그림자는 검정
    return np.clip(out_rgb, 0, 1), np.clip(out_a, 0, 1)


def downsample(rgb, a, size=FINAL):
    """프리멀티플라이 상태로 줄여야 가장자리에 검은 띠가 안 생긴다."""
    pm = np.concatenate([rgb * a[..., None], a[..., None]], axis=2)
    img = Image.fromarray(np.clip(pm * 255 + 0.5, 0, 255).astype(np.uint8), "RGBA")
    small = np.asarray(img.resize((size, size), Image.Resampling.LANCZOS), np.float32) / 255.0
    a2 = small[..., 3]
    rgb2 = small[..., :3] / np.maximum(a2[..., None], 1e-4)
    return np.clip(rgb2, 0, 1), a2


def bleed(rgb, a, passes=6):
    """투명 영역 RGB를 가장자리 색으로 채운다(쌍선형 필터의 검은 번짐 방지)."""
    rgb = rgb.copy()
    known = a > 0.02
    for i in range(passes):
        r = 2 ** i
        w = known.astype(np.float32)
        acc = np.stack([blur_f(rgb[..., c] * w, r) for c in range(3)], axis=2)
        ww = blur_f(w, r)
        fill = acc / np.maximum(ww[..., None], 1e-5)
        take = (~known) & (ww > 1e-4)
        rgb[take] = fill[take]
        known = known | take
    return rgb


def blur_f(a, r):
    # 0..1 범위 밖 값(가중 합)도 흐려야 해서 16비트 대신 float 박스블러 3회로 가우시안 근사
    out = a
    k = max(1, int(r))
    for _ in range(3):
        c = np.cumsum(np.pad(out, ((k + 1, k), (0, 0)), mode="edge"), axis=0)
        out = (c[2 * k + 1:] - c[:-2 * k - 1]) / (2 * k + 1)
        c = np.cumsum(np.pad(out, ((0, 0), (k + 1, k)), mode="edge"), axis=1)
        out = (c[:, 2 * k + 1:] - c[:, :-2 * k - 1]) / (2 * k + 1)
    return out


def save_rgba(path, rgb, a):
    rgb = bleed(rgb, a)
    out = np.concatenate([rgb, a[..., None]], axis=2)
    Image.fromarray(np.clip(out * 255 + 0.5, 0, 255).astype(np.uint8), "RGBA").save(path, optimize=True)


# ────────────────────────── 연출용 보조 텍스처 ──────────────────────────

def make_rays(size=512):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    cx = cy = size / 2
    ang = np.arctan2(y - cy, x - cx)
    r = np.sqrt((x - cx) ** 2 + (y - cy) ** 2) / (size / 2)
    rays = np.clip(np.cos(ang * 12), 0, 1) ** 6 * 0.9 + np.clip(np.cos(ang * 12 + math.pi / 2), 0, 1) ** 10 * 0.45
    fall = np.clip(1 - r, 0, 1) ** 1.6 * smooth(r, 0.08, 0.25)
    a = np.clip(rays * fall, 0, 1)
    return np.ones((size, size, 3), np.float32), a


def make_glow(size=256):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    r = np.sqrt((x - size / 2) ** 2 + (y - size / 2) ** 2) / (size / 2)
    a = np.clip(1 - r, 0, 1) ** 2.2
    return np.ones((size, size, 3), np.float32), a


def make_sparkle(size=128):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    dx, dy = (x - size / 2) / (size / 2), (y - size / 2) / (size / 2)
    r = np.sqrt(dx * dx + dy * dy)
    star = np.clip(1 - (np.abs(dx) ** 0.5 + np.abs(dy) ** 0.5), 0, 1) ** 1.4
    core = np.clip(1 - r * 3.2, 0, 1) ** 1.5
    halo = np.clip(1 - r, 0, 1) ** 3 * 0.35
    a = np.clip(star + core + halo, 0, 1)
    return np.ones((size, size, 3), np.float32), a


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    tiles = []
    for region, shape_fn, pal, enamel, emb_fn, groove_fn, rim in BADGES:
        rgb, a = render(region, shape_fn, pal, enamel, emb_fn, groove_fn, rim)
        rgb, a = downsample(rgb, a)
        save_rgba(os.path.join(OUT_DIR, f"badge_{region}.png"), rgb, a)
        tiles.append((region, rgb, a))
        print("badge", region)
    for name, (rgb, a) in (("badge_rays", make_rays()), ("badge_glow", make_glow()), ("badge_sparkle", make_sparkle())):
        save_rgba(os.path.join(OUT_DIR, f"{name}.png"), rgb, a)
        print("fx", name)

    if "--sheet" in sys.argv:
        cols, cell = 7, FINAL + 16
        rows = (len(tiles) + cols - 1) // cols
        sheet = Image.new("RGB", (cols * cell, rows * cell * 3), (0, 0, 0))
        for band, bg in enumerate(((236, 232, 222), (34, 38, 48), None)):
            for i, (region, rgb, a) in enumerate(tiles):
                x, y = (i % cols) * cell + 8, (band * rows + i // cols) * cell + 8
                back = np.full((FINAL, FINAL, 3), (bg or (34, 38, 48)), np.float32) / 255.0
                src = rgb
                if bg is None:   # 잠김 미리보기 — 게임은 어두운 틴트를 곱해 그린다
                    src = rgb * np.array([0.16, 0.17, 0.2])
                comp = src * a[..., None] + back * (1 - a[..., None])
                sheet.paste(Image.fromarray((comp * 255).astype(np.uint8)), (x, y))
        os.makedirs(os.path.dirname(SHEET), exist_ok=True)
        sheet.save(SHEET)
        print("sheet", SHEET)


if __name__ == "__main__":
    main()
