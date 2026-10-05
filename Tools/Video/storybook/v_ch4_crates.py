"""ch4 「습지의 상자」 — 상자에서 파닥이는 소리를 들은 대사(`ch4_harvest`) **뒤**에 튼다. 저녁 습지(청회·연두).

    샷1  0.0~3.0   안개 낀 습지 — 물 위 낮은 안개, 갈대, 저녁 하늘
    샷2  2.4~6.5   반쯤 잠긴 나무 상자 여럿이 줄지어 — 카메라가 줄을 따라 옆으로. 상자 하나가 덜컥
    샷3  5.9~9.5   상자 틈 클로즈업 — 어둠 속 곤충의 큰 눈 둘이 깜빡이고 더듬이가 꼼지락
    샷4  8.9~12.0  뚜껑의 이름표(판독 불가 + 눈금) 클로즈업, 물방울이 떨어진다
"""
import math

import numpy as np
from PIL import Image

from bk_v1 import Sprite, flutter, frog, ripple_ring, tally_strokes, water_static
from kit import (INK, H, W, Mask, Particles, add, bezier, blur_arr, book_sky, c, crate, crop, dot,
                 ease_in_out, ease_out, fbm, grow, ground_grad, lerp, mist, over, paint, radial, reeds,
                 rotate_pts, scribble, smooth, span, stamp, zoom)
from sound import hz

DURS = [3.0, 3.5, 3.0, 2.5]
TINT = "#f2f2f8"
CUES = [
    (1.0, 2.6, "습지 곳곳에 상자가 숨겨져 있었다."),
    (6.2, 2.6, "틈 사이로 더듬이가 움직였다."),
    (9.2, 2.4, "모두 살아 있는 곤충이었다."),
]

SKY = [(0, "#6c86d4"), (0.42, "#bdb6e4"), (0.78, "#f6c9aa"), (1, "#f8dabd")]
HZ_Y = 345
WATER = ("#a6c3d8", "#4f8798")
REED, REED_TIP = "#a8cc4c", "#8a6236"


def swamp_bg(w, seed, crates_fn=None):
    """습지 배경(폭 w) — 저녁 하늘, 먼 숲, 물, 풀섬, 갈대."""
    img = book_sky(SKY, seed=seed, w=w)
    add(img, c("#ffd9a8"), radial(w, H, w * 0.72, HZ_Y - 30, 420, 1.8) * 0.35)
    far = Mask(w, H)
    rr = np.random.default_rng(seed + 1)
    x = -20
    while x < w + 20:
        rad = rr.uniform(18, 40)
        far.circle(x, HZ_Y - rad * 0.7 + rr.uniform(-6, 6), rad)
        x += rad * rr.uniform(0.8, 1.4)
    far.rect(-20, HZ_Y - 10, w + 20, HZ_Y + 4)
    for _ in range(int(6 * w / W)):                  # 마른 나무
        tx, th = rr.uniform(0, w), rr.uniform(90, 160)
        far.line([(tx, HZ_Y), (tx + rr.uniform(-6, 6), HZ_Y - th)], 6)
        for k in range(3):
            by = HZ_Y - th * rr.uniform(0.45, 0.9)
            sg = 1 if k % 2 else -1
            far.line([(tx, by), (tx + sg * rr.uniform(20, 38), by - rr.uniform(14, 30))], 3.5)
    paint(img, far.arr(0.8), "#8e9cc4", ink="#7482aa", line=1.0, hi=0.04, lo=0.0)
    water_static(img, HZ_Y, H, WATER[0], WATER[1], seed=seed + 2, lines=16, line_alpha=0.3)
    glow = Mask(w, H)
    glow.rect(w * 0.72 - 50, HZ_Y + 6, w * 0.72 + 50, H)
    add(img, c("#ffd2a0"), blur_arr(glow.arr(0.5), 30) * 0.22)
    isl = Mask(w, H)
    for _ in range(int(5 * w / W)):
        ix, iy, ir = rr.uniform(0, w), rr.uniform(HZ_Y + 30, HZ_Y + 110), rr.uniform(60, 140)
        isl.ellipse(ix, iy, ir, ir * 0.16)
    paint(img, isl.arr(0.6), "#8fb04e", ink="#5f7a3a", line=1.2, hi=0.1, lo=0.0)
    if crates_fn:
        crates_fn(img)
    np.clip(img, 0, 1, out=img)
    return img


# ══════════════════════════ 샷1 — 안개 낀 습지 ══════════════════════════

def swamp_shot():
    img = swamp_bg(W, 401)
    reeds(img, -30, 260, 610, seed=402, n=12, h=(200, 330), fill=REED, tip=REED_TIP)
    reeds(img, 1000, 1310, 600, seed=403, n=12, h=(190, 320), fill=REED, tip=REED_TIP)
    reeds(img, 520, 700, HZ_Y + 70, seed=404, n=6, h=(60, 110), fill="#9bb84c", tip=REED_TIP)
    np.clip(img, 0, 1, out=img)
    flies = Particles(5, 405, (300, 260, 1000, 520), vel=((-8, 8), (-6, 4)), size=(1.4, 2.0), wobble=14)
    glints = [(x, y) for x, y in np.random.default_rng(406).uniform((200, HZ_Y + 20), (1100, 700), (12, 2))]

    def frame(u):
        f = img.copy()
        for i, (x, y) in enumerate(glints):
            dot(f, x + math.sin(u + i) * 10, y, 1.8, c("#ffffff"), 0.5 * max(0, math.sin(u * 2 + i * 1.3)))
        mist(f, 407, u, HZ_Y - 30, HZ_Y + 90, color="#eceef6", amount=0.4, speed=14)
        mist(f, 408, u + 4, 470, 600, color="#e6e9f2", amount=0.32, speed=22)
        flies.draw(f, u, c("#e9ff8a"), 0.7)
        return zoom(f, 1.0 + 0.05 * ease_in_out(u / 3.0), W * 0.5, H * 0.5)

    return frame


# ══════════════════════════ 샷2 — 줄지어 잠긴 상자 ══════════════════════════

PAN = 560
CRATES = [  # (x, 물선 y, 폭, 높이, 씨앗) — 먼 것(작고 위)과 가까운 것(크고 아래)이 번갈아 선다
    (150, 470, 190, 140, 2), (400, 560, 280, 210, 1), (670, 478, 196, 146, 4), (930, 575, 290, 216, 3),
    (1200, 472, 186, 138, 6), (1450, 556, 276, 206, 5), (1700, 480, 200, 150, 7),
]


def _crates(img):
    ww = img.shape[1]
    front = Mask(ww, H)
    rope = Mask(ww, H)
    for i, (x, wl, w, h, sd) in enumerate(CRATES):
        crate(img, x, wl + h * 0.42, w, h, seed=40 + sd)
        front.rect(x - w / 2 - 14, wl, x + w / 2 + 14, wl + h * 0.42 + 12)
        if i + 1 < len(CRATES):
            nx, nwl, nw, nh, _ = CRATES[i + 1]
            a = (x + w / 2 - 6, wl - h * 0.58 + 10)
            b = (nx - nw / 2 + 6, nwl - nh * 0.58 + 10)
            mid = ((a[0] + b[0]) / 2, max(a[1], b[1]) + 40)
            rope.line(bezier(a, (a[0] + 30, mid[1]), (b[0] - 30, mid[1]), b, 16), 3.2, caps=False)
    fa = front.arr(1.0)
    ground_grad(img, fa * 0.82, *WATER)
    lines = Mask(ww, H)
    for x, wl, w, h, sd in CRATES:
        lines.line([(x - w / 2 - 8, wl + 1), (x + w / 2 + 8, wl + 1)], 2.4)
        lines.line([(x - w / 2 + 10, wl + 14), (x + w / 2 - 20, wl + 14)], 1.6)
    add(img, c("#ffffff"), lines.arr(0.6) * 0.5)
    over(img, c("#7a5a3a"), grow(rope.arr(0.5), 0.8))
    over(img, c("#c8a070"), rope.arr(0.5))


def crates_shot():
    img = swamp_bg(W + PAN, 410, _crates)

    left = Sprite(lambda d: reeds(d, -40, 170, H + 30, seed=411, n=8, h=(300, 460), fill=REED, tip=REED_TIP),
                  W, H, origin=(0, 0))
    right = Sprite(lambda d: reeds(d, W - 190, W + 30, H + 30, seed=412, n=8, h=(280, 440), fill=REED, tip=REED_TIP),
                   W, H, origin=(0, 0))
    shakes = [(2, 1.1), (4, 2.5)]       # (상자 번호, 시각) — 안에서 파닥인다

    def frame(u):
        cam = PAN * ease_in_out(u / 4.1)
        f = crop(img, cam, 0).copy()
        for k, t0 in shakes:
            kk = span(u, t0, t0 + 0.55)
            if 0 < kk < 1:
                x, wl, w, h, _ = CRATES[k]
                _shake_marks(f, x - cam, wl - h * 0.58, w, kk)
        mist(f, 413, u, HZ_Y - 20, HZ_Y + 80, color="#eceef6", amount=0.34, speed=16)
        left.blit(f, -cam * 1.15, 0, sub=False)
        right.blit(f, (PAN - cam) * 1.15, 0, sub=False)
        return f

    return frame


def _shake_marks(f, x, top, w, k):
    """덜컥! — 상자 양옆 위에 짧은 떨림 줄 셋(그림책 효과선)."""
    a = math.sin(math.pi * k)
    m = Mask(int(w + 140), 120)
    cx = m.w / 2
    for sg in (-1, 1):
        for j in range(3):
            ang = -math.pi / 2 + sg * (0.5 + 0.32 * j)
            r0, r1 = w * 0.5 + 6, w * 0.5 + 30
            m.line([(cx + math.cos(ang) * r0 * 0.9, 100 + math.sin(ang) * 60),
                    (cx + math.cos(ang) * r1 * 0.9, 100 + math.sin(ang) * 86)], 3.2)
    stamp(f, m.arr(0.5) * a, x, top - 10, "#fff6d8", "add")
    stamp(f, m.arr(0.5) * a * 0.6, x, top - 10, INK)


# ══════════════════════════ 샷3 — 틈 속의 눈 ══════════════════════════

GAP_Y0, GAP_Y1 = 286, 384


def _wood(seed, base="#c98b4a", dark="#a86e34"):
    g = fbm(180, H, seed, (40, 10), (0.6, 0.4))
    g = np.asarray(Image.fromarray((g * 255).astype(np.uint8), "L").resize((W, H), Image.Resampling.BICUBIC),
                   np.float32) / 255
    img = np.ones((H, W, 3), np.float32) * c(base)
    img *= (0.84 + 0.3 * g)[..., None]
    gl = Mask()
    r = np.random.default_rng(seed + 1)
    for _ in range(26):
        y, ph, amp = r.uniform(0, H), r.uniform(0, 6.28), r.uniform(2, 7)
        gl.line([(x, y + math.sin(x * 0.006 + ph) * amp) for x in range(-10, W + 20, 20)], r.uniform(1.4, 2.6),
                caps=False)
    over(img, c(dark), gl.arr(0.6) * 0.55)
    return img


def gap_shot():
    img = _wood(431)
    seam = Mask()
    seam.line([(-10, 120), (W + 10, 120)], 3)
    seam.line([(-10, 560), (W + 10, 560)], 3)
    over(img, c("#5a3a1e"), seam.arr(0.5) * 0.8)
    hole = Mask()
    hole.poly([(-20, GAP_Y0 + 4)] + [(x, GAP_Y0 + 3 * math.sin(x * 0.02)) for x in range(0, W + 20, 20)]
              + [(W + 20, GAP_Y1)] + [(x, GAP_Y1 + 3 * math.sin(x * 0.017 + 1)) for x in range(W, -40, -20)])
    ha = hole.arr(0.6)
    over(img, c(INK), grow(ha, 1.8))
    over(img, c("#1c1310"), ha)
    add(img, c("#3a2a20"), ha * np.clip((np.arange(H)[:, None] - GAP_Y0) / (GAP_Y1 - GAP_Y0), 0, 1) * 0.5)
    bev = Mask()
    bev.rect(-10, GAP_Y0 - 10, W + 10, GAP_Y0)
    add(img, c("#ffffff"), bev.arr(3) * 0.08)
    for x, y in ((80, 60), (1200, 60), (80, 470), (1200, 470), (80, 640), (1200, 640)):
        nm = Mask(30, 30).circle(15, 15, 8).arr(0.4)
        stamp(img, grow(nm, 1.2), x, y, INK)
        stamp(img, nm, x, y, "#9a9aa4")
        dot(img, x - 2, y - 2, 2.5, c("#ffffff"), 0.6)
    wet = Mask()
    for x in range(-20, W + 40, 60):
        wet.circle(x + 20 * math.sin(x), 700 + 10 * math.cos(x * 0.1), 50)
    over(img, c("#6a4626"), wet.arr(18) * 0.35)
    np.clip(img, 0, 1, out=img)
    eye_w = Mask(90, 90).ellipse(45, 45, 33, 31).arr(0.5)
    eyes = (588, 335), (692, 335)

    def frame(u):
        f = img.copy()
        appear = smooth(span(u, 0.25, 0.9))
        look = math.sin(u * 1.6) * 6 if u > 1.0 else -4
        blink = 1.0
        for t0 in (1.95, 3.05):
            if t0 < u < t0 + 0.16:
                blink = 0.12
        if appear > 0.01:
            stamp(f, blur_arr(eye_w, 10) * 0.25 * appear, eyes[0][0], eyes[0][1], "#ffffff", "add")
            stamp(f, blur_arr(eye_w, 10) * 0.25 * appear, eyes[1][0], eyes[1][1], "#ffffff", "add")
            for ex, ey in eyes:
                m = Mask(90, 90).ellipse(45, 45, 33, max(1.5, 31 * blink))
                a = m.arr(0.5) * appear
                stamp(f, grow(a, 1.4), ex, ey, INK)
                stamp(f, a, ex, ey, "#ffffff")
                if blink > 0.5:
                    pm = Mask(90, 90).circle(45 + look, 48, 16).arr(0.5) * a
                    stamp(f, pm, ex, ey, "#1a1210")
                    dot(f, ex + look - 5, ey - 4, 4.5, c("#ffffff"), 0.95 * appear)
        _antennae(f, u)
        return zoom(f, 1.0 + 0.06 * ease_in_out(u / 3.6), W * 0.5, H * 0.46)

    return frame


def _antennae(f, u):
    grow_k = ease_out(span(u, 0.6, 1.5))
    if grow_k <= 0.01:
        return
    bx, by, bw, bh = 640, 230, 520, 300
    m = Mask(bw, bh)
    tips = Mask(bw, bh)
    for sg, ph in ((-1, 0.0), (1, 1.7)):
        root = (bw / 2 + sg * 16, bh - 58)
        wig = math.sin(u * 3.4 + ph) * 22 + math.sin(u * 7.1 + ph) * 6
        end = (bw / 2 + sg * (90 + 40 * grow_k) + wig, bh - 58 - 180 * grow_k)
        pts = bezier(root, (root[0] + sg * 10, root[1] - 70 * grow_k), (end[0] - sg * 20 - wig * 0.5, end[1] + 60),
                     end, 18)
        m.taper(pts, 9, 6)
        tips.ellipse(end[0], end[1], 11, 15, sg * 0.4 + wig * 0.01)
    a = np.maximum(m.arr(0.5), tips.arr(0.5))
    paint(f, a, "#4a3426", bx, by, line=1.4, hi=0.25, lo=0.0)
    # 뿌리 부분은 틈 안(어둠)에 있다
    shade = Mask(bw, bh).rect(0, bh - 60, bw, bh).arr(8)
    stamp(f, shade * a * 0.7, bx, by, "#1c1310")


# ══════════════════════════ 샷4 — 뚜껑의 이름표와 물방울 ══════════════════════════

TAG = (640, 300, 500, 270, -0.05)    # 가운데 x, y, 폭, 높이, 기울기


def _tag_pts():
    x, y, w, h, rot = TAG
    return rotate_pts([(x - w / 2, y - h / 2), (x + w / 2, y - h / 2), (x + w / 2, y + h / 2), (x - w / 2, y + h / 2)],
                      x, y, rot)


def tag_shot():
    img = _wood(441, base="#b9824a", dark="#94602e")
    seam = Mask()
    for yv in (90, 250, 410, 570):
        seam.line([(-10, yv), (W + 10, yv)], 3)
    over(img, c("#4e3219"), seam.arr(0.5) * 0.8)
    x, y, w, h, rot = TAG
    tm = Mask()
    tm.poly(_tag_pts())
    ta = tm.arr(0.6)
    over(img, c("#2a1a10"), blur_arr(ta, 8) * 0.35)
    paint(img, ta, "#f3ead2", hi=0.12, lo=0.1)
    stain = fbm(W, H, 442, (70, 24), (0.6, 0.4))
    over(img, c("#d9c9a2"), np.clip((stain - 0.5) * 3, 0, 1) * ta * 0.6)
    sm = Mask()
    scribble(sm, x - w * 0.38, y - h * 0.33, w * 0.5, 1, 4421, row_h=30, weight=3.4)
    tl = Mask()
    for row, n in ((0, 22), (1, 19), (2, 12)):
        for p0, p1 in tally_strokes(n, x - w * 0.38, y - h * 0.12 + row * 55, 40, 11, 21):
            tl.line([p0, p1], 3.8)
    ink_a = np.maximum(sm.arr(0.4), tl.arr(0.4))
    ink_a = _rot_about(ink_a, x, y, rot)
    over(img, c("#3b2f3a"), ink_a * 0.9)
    for px, py in _tag_pts()[:2]:
        nx, ny = px + (x - px) * 0.08, py + 18
        nm = Mask(30, 30).circle(15, 15, 8).arr(0.4)
        stamp(img, grow(nm, 1.2), nx, ny, INK)
        stamp(img, nm, nx, ny, "#9a9aa4")
        dot(img, nx - 2, ny - 2, 2.5, c("#ffffff"), 0.6)
    np.clip(img, 0, 1, out=img)
    drops = [(560, 250, 0.25), (735, 330, 1.05), (612, 362, 1.85)]   # (x, 닿는 y, 떨어지기 시작)
    spot = radial(140, 140, 70, 70, 60, 1.2)
    drop_m = Mask(56, 84)
    drop_m.circle(28, 54, 15)
    drop_m.poly([(14.5, 48), (28, 8), (41.5, 48)])
    drop_a = drop_m.arr(0.5)

    def frame(u):
        f = img.copy()
        for i, (dx, dy, t0) in enumerate(drops):
            fall = span(u, t0, t0 + 0.45)
            if 0 < fall < 1:
                yy = lerp(-40, dy - 14, fall * fall)
                stamp(f, grow(drop_a, 1.2) * 0.8, dx, yy, "#4a6a86")
                stamp(f, drop_a, dx, yy, "#cfe8f8")
                dot(f, dx - 3, yy + 6, 2.5, c("#ffffff"), 0.9)
            hit = u - (t0 + 0.45)
            if hit > 0:
                k = min(1.0, hit / 0.6)
                stamp(f, spot * 0.3 * ease_out(min(1.0, hit / 0.35)), dx, dy, "#8a7350")
                if k < 1:
                    ripple_ring(f, dx, dy, 10 + 50 * ease_out(k), 0.9 * (1 - k), w=2.2, flat=0.4)
                    for j in range(7):
                        a = -math.pi * (0.1 + 0.8 * j / 6)
                        r_ = 14 + 60 * ease_out(k)
                        dot(f, dx + math.cos(a) * r_, dy + math.sin(a) * r_ * 0.7 + 40 * k * k, 3.0,
                            c("#e8f6ff"), 0.9 * (1 - k))
                if hit > 0.3:
                    tw = 0.5 + 0.5 * math.sin(u * 4 + i)
                    dot(f, dx - 8, dy - 6, 3, c("#ffffff"), 0.5 * tw)
        jolt = span(u, 1.5, 1.9)
        shake = 5 * math.sin(jolt * math.pi * 6) * (1 - jolt) if 0 < jolt < 1 else 0.0
        z = 1.0 + 0.08 * ease_in_out(u / 3.1)
        return zoom(f, z, W * 0.5 + shake, H * 0.45 + shake * 0.5)

    return frame


def _rot_about(a, cx, cy, rot):
    im = Image.fromarray(np.ascontiguousarray(a, np.float32), "F")
    im = im.rotate(-math.degrees(rot), resample=Image.Resampling.BILINEAR, center=(cx, cy))
    return np.clip(np.asarray(im, np.float32), 0, 1)


SHOTS = [swamp_shot, crates_shot, gap_shot, tag_shot]


def score(sc):
    """습지 물·개구리·풀벌레 조금 → 상자 안 파닥임 → 물방울 → 걱정스러운 단조 패드."""
    T = sc.total
    s = [sc.shot(k) for k in range(4)]
    sc.water([(0, 0.0), (0.4, 1.0), (s[2], 0.7), (s[3], 0.4), (T, 0.3)], amp=0.03)
    sc.crickets([(0, 0), (0.5, 1), (s[2], 0.6), (s[2] + 0.5, 0.2), (T, 0.15)], amp=0.01, f=4300)
    sc.wind([(0, 0), (0.8, 0.6), (T, 0.5)], amp=0.022, lo=150, hi=900)
    frog(sc, 0.7, 0.08, 135, pan=-0.4, croaks=2)
    frog(sc, 2.1, 0.06, 160, pan=0.5, croaks=3)
    sc.pad([hz("D3"), hz("A3"), hz("F4")], [(0, 0), (1.0, 0.45), (s[2], 0.45), (s[2] + 0.8, 0.0), (T, 0)], amp=0.035)
    for k, t0 in ((2, 1.1), (4, 2.5)):                                # 샷2 — 상자가 덜컥
        flutter(sc, s[1] + t0, 0.55, 0.07, rate=22, lo=250, hi=2200, pan=-0.2 + 0.2 * k / 2)
        sc.thud(s[1] + t0 + 0.05, 0.12)
    for j in range(6):                                                # 샷3 — 틈 속에서 꼼지락
        flutter(sc, s[2] + 0.5 + j * 0.48, 0.32, 0.03, rate=30, lo=300, hi=2600, pan=0.1 * math.sin(j))
    sc.pluck(s[2] + 0.55, hz("B4"), 0.06, 0.8, kind="xylo")           # 눈을 뜬다
    sc.pluck(s[2] + 0.62, hz("E5"), 0.05, 0.8, kind="xylo")
    for t0 in (1.95, 3.05):
        sc.pluck(s[2] + t0, hz("A5"), 0.04, 0.4, kind="xylo")         # 깜빡
    for dx, dy, t0 in ((560, 250, 0.25), (735, 330, 1.05), (612, 362, 1.85)):   # 샷4 — 물방울
        sc.drip(s[3] + t0 + 0.45, 0.14, f=1300 + (dx - 600) * 1.5)
    flutter(sc, s[3] + 1.5, 0.45, 0.05, rate=24, lo=250, hi=2000)
    sc.thud(s[3] + 1.52, 0.08)
    sc.pad([hz("D3"), hz("F3"), hz("A3"), hz("E4")], [(0, 0), (s[2], 0), (s[2] + 1.0, 0.45), (s[3], 0.6),
                                                      (s[3] + 1.0, 0.9), (T - 0.4, 0.9), (T, 0)], amp=0.048,
           detune=0.005, bright=0.15)
