"""ch1 「사라지는 곤충」 — 어르신의 부탁(`ch1_intro`) **대사 뒤**에 튼다. 새벽 초원.

    샷1  0.0~3.5   새벽 초원 광각 — 해 뜨는 하늘, 둥근 언덕, 곤충 하나 없는 고요한 풀밭. 카메라가 천천히 옆으로(시차)
    샷2  2.9~6.5   풀잎 클로즈업 — 앉아 있던 무당벌레·나비·잠자리가 하나씩 안개처럼 흐려져 퐁 사라진다
    샷3  5.9~9.0   은빛 채집망을 쥔 주인공 손(초록 소매) — 역광, 그물 테 안에 해
    샷4  8.4~12.0  펼친 손등에 무당벌레 한 마리가 날아와 내려앉는다. 반짝
"""
import math

import numpy as np

from bk_v1 import Sprite, hand_parts_rot, meadow_w, puff, ribbon
from kit import (PAL, H, W, InsectSprite, Mask, Particles, add, bezier, blur_arr, book_sky, bush, c, cloud, dot,
                 draw_hand, draw_insect, ease_in_out, ease_out, grow, hills, insect_parts, lerp, over, paint,
                 radial, rays_texture, smooth, span, sparkle, stamp, sun, tree, zoom)
from silhouette_kit import bokeh
from sound import hz

DURS = [3.5, 3.0, 2.5, 3.0]
TINT = "#fff4e6"
CUES = [
    (0.8, 2.6, "어제 있던 곤충이 오늘은 안 보인다."),
    (3.8, 2.6, "하나둘, 흐려지듯 사라지고 있다."),
    (6.4, 2.2, "그래서 이 그물을 받았다."),
    (9.0, 2.6, "이 아이와 함께 찾으러 가자."),
]

HERO_SKIN, HERO_SLEEVE = "#f6c9a0", "#3fae6a"


# ══════════════════════════ 샷1 — 새벽 초원 ══════════════════════════

def _bird(f, x, y, s, flap, alpha=0.7):
    m = Mask(int(s * 3) + 10, int(s * 2) + 10)
    cx, cy = m.w / 2, m.h / 2
    lift = s * 0.55 * flap
    m.line(bezier((cx - s, cy - lift), (cx - s * 0.5, cy - lift * 0.4 - s * 0.3), (cx - s * 0.2, cy - s * 0.1), (cx, cy), 8),
           max(1.4, s * 0.16))
    m.line(bezier((cx + s, cy - lift), (cx + s * 0.5, cy - lift * 0.4 - s * 0.3), (cx + s * 0.2, cy - s * 0.1), (cx, cy), 8),
           max(1.4, s * 0.16))
    stamp(f, m.arr(0.5) * alpha, x, y, "#6a4a52")


def wide_shot():
    horizon = 372
    far = book_sky(PAL["dawn"], seed=101)
    add(far, c("#fff0c8"), rays_texture(W, H, 640, 338, count=13, seed=3, spread=math.pi * 1.25, reach=0.85) * 0.09)
    sun(far, 640, 338, r=58, color="#fff0a0", halo=0.55)
    cloud(far, 230, 130, s=0.95, color="#fff3f0", ink="#dcaab6", seed=1)
    cloud(far, 1060, 96, s=0.7, color="#fff3f0", ink="#dcaab6", seed=2)
    cloud(far, 860, 205, s=0.45, color="#fff6ee", ink="#e2b8b8", seed=3)
    hills(far, 103, horizon, 24, "#bdb6e8", ink="#958cc6", freq=(0.002, 0.006), line=1.2, hi=0.05)
    np.clip(far, 0, 1, out=far)

    def mid_draw(d):
        hills(d, 104, 432, 28, "#a5d877", hi=0.12)
        for x, h, s in ((210, 70, 11), (330, 54, 12), (1190, 64, 13)):
            tree(d, x, 432 - 6, h, seed=s, leaf="#7cc85a", leaf2="#5aa845")

    mid = Sprite(mid_draw, W + 90, H, origin=(0, 0))

    def near_draw(d):
        meadow_w(d, 520, 105, flowers=46, top="#90d562", bottom="#5db247")
        tree(d, 95, 560, 300, seed=21, leaf="#5cbf4a", leaf2="#3f9a35")
        bush(d, 1340, 560, 150, seed=22, fill="#4fb43e")
        bush(d, 760, 548, 110, seed=23, fill="#58bb44", berries="#ff8fb0")

    near = Sprite(near_draw, W + 240, H, origin=(0, 0))
    dew = [(x, y) for x, y in np.random.default_rng(106).uniform((0, 540), (W, 680), (24, 2))]
    birds = [(380, 168, 0.0, 9), (452, 188, 1.7, 8), (512, 160, 3.1, 7)]
    motes = Particles(26, 107, (0, 260, W, 560), vel=((-3, 3), (-6, -1)), size=(0.7, 1.5))

    def frame(u):
        f = far.copy()
        cam = ease_in_out(u / 3.5)
        mist_band(f, u)
        mid.pan(f, 70 * cam)
        near.pan(f, 230 * cam)
        for i, (x, y, ph, s) in enumerate(birds):
            fl = 0.5 + 0.5 * math.sin(u * 7.5 + ph)
            _bird(f, x + u * 34, y + math.sin(u * 1.3 + ph) * 6, s, fl * 2 - 1, alpha=0.7)
        for i, (x, y) in enumerate(dew):
            tw = max(0.0, math.sin(u * 2.2 + i * 1.7))
            dot(f, x - 230 * cam, y, 2.0, c("#ffffff"), 0.8 * tw ** 3)
        motes.draw(f, u, c("#fff1c8"), 0.35)
        return f

    _mist = {}

    def mist_band(f, u):
        if "a" not in _mist:
            from kit import fbm
            n = fbm(W + 300, 140, 108, (220, 70), (0.7, 0.3))
            yy = np.linspace(-1, 1, 140, dtype=np.float32)[:, None]
            _mist["a"] = np.clip((n - 0.3) * 1.5, 0, 1) * (1 - yy ** 2) ** 1.5 * 0.55
        a = _mist["a"]
        ox = int(u * 14) % 300
        over(f[330:470], c("#fff1ec"), a[:, ox:ox + W])

    return frame


# ══════════════════════════ 샷2 — 풀잎 위 곤충이 흐려진다 ══════════════════════════

def _blade(m, base, tip, w0, bend=0.0):
    (x0, y0), (x1, y1) = base, tip
    pts = bezier((x0, y0), (x0 + bend * 0.2, lerp(y0, y1, 0.45)), (x1 - bend, lerp(y0, y1, 0.8)), (x1, y1), 18)
    ribbon(m, pts, w0, 1.5)


def blades_shot():
    bg = book_sky([(0, "#ffc59a"), (0.4, "#ffdfb2"), (0.68, "#d3eaa0"), (1, "#86c868")], seed=201)
    back = Mask()
    r = np.random.default_rng(202)
    for _ in range(46):
        x = r.uniform(-20, W + 20)
        _blade(back, (x, H + 20), (x + r.uniform(-50, 50), r.uniform(260, 520)), r.uniform(10, 22), r.uniform(-40, 40))
    over(bg, c("#7fc65c"), blur_arr(back.arr(0.5), 5) * 0.9)
    add(bg, bokeh(W, H, 203, 28, c("#fff3c4"), rmin=14, rmax=48, alpha=0.32), 1.0) if False else None
    bg += bokeh(W, H, 203, 22, c("#fff3c4"), rmin=14, rmax=44, alpha=0.17)
    side = Mask()
    for x, tx, ty, w0, bd in ((60, 150, 120, 70, 60), (190, 230, 300, 46, -30), (1200, 1110, 140, 70, -70),
                              (1080, 1040, 330, 44, 30), (360, 330, 380, 30, 20), (940, 980, 360, 30, -20)):
        _blade(side, (x, H + 30), (tx, ty), w0, bd)
    paint(bg, blur_arr(side.arr(0.5), 2.2), "#4fae3e", line=1.0, hi=0.1)
    main = Mask()
    BLADES = [((470, H + 30), (512, 250), 30, -30), ((616, H + 30), (650, 206), 32, 40), ((830, H + 30), (792, 318), 30, 30),
              ((560, H + 30), (585, 420), 20, 10), ((715, H + 30), (725, 400), 20, -10)]
    for b, t, w0, bd in BLADES:
        _blade(main, b, t, w0, bd)
    paint(bg, main.arr(0.5), "#5ec247", hi=0.18, lo=0.12)
    vein = Mask()
    for b, t, w0, bd in BLADES[:3]:
        vein.line(bezier(b, (b[0] + bd * 0.2, lerp(b[1], t[1], 0.45)), (t[0] - bd, lerp(b[1], t[1], 0.8)), t, 18)[:-2],
                  1.6)
    over(bg, c("#3f9a35"), vein.arr(0.6) * 0.5)
    np.clip(bg, 0, 1, out=bg)
    drops = [(520, 470), (642, 560), (800, 450), (597, 520), (708, 476)]
    for x, y in drops:
        m = Mask(20, 20).circle(10, 10, 4.5).arr(0.4)
        stamp(bg, grow(m, 1.0) * 0.6, x, y, "#3d8a3a")
        stamp(bg, m, x, y, "#d8f6ff")
        dot(bg, x - 1.5, y - 1.5, 1.2, c("#ffffff"), 0.9)

    bugs = [  # (종류, x, y, 크기, 회전, 사라지는 시각)
        ("beetle", 503, 340, 46, -0.1, 0.65),
        ("bfly", 650, 214, 64, 0.25, 1.45),
        ("dfly", 788, 328, 54, -1.5, 2.25),
    ]
    sprites = {"bfly": InsectSprite("bfly", 64, rot=0.25, frames=5)}
    parts = {k: insect_parts(k, s, rot) for k, x, y, s, rot, _ in bugs if k != "bfly"}
    halos = {}
    for k, x, y, s, rot, _ in bugs:
        p = sprites[k].parts[0] if k in sprites else parts[k]
        halos[k] = blur_arr(np.maximum(p["wing"], p["body"]), 6)

    def frame(u):
        f = bg.copy()
        for i, (k, x, y, s, rot, t0) in enumerate(bugs):
            fade = span(u, t0, t0 + 0.75)
            a = 1 - smooth(fade)
            yy = y - 14 * ease_out(fade)
            if a > 0.01:
                if k in sprites:
                    sprites[k].draw(f, x, yy, u, rate=2.6, alpha=a)
                else:
                    draw_insect(f, k, x, yy, s, alpha=a, parts=parts[k])
            if 0 < fade < 1:
                stamp(f, halos[k] * math.sin(math.pi * fade) * 0.85, x, yy, "#fffaf2")
                puff(f, x, yy, span(u, t0 + 0.1, t0 + 1.1), size=s * 0.9, color="#ffffff", seed=11 + i, amount=0.95)
            elif u > t0 + 0.75:
                puff(f, x, yy, span(u, t0 + 0.1, t0 + 1.1), size=s * 0.9, color="#ffffff", seed=11 + i, amount=0.95)
        for i, (x, y) in enumerate(drops):
            tw = max(0.0, math.sin(u * 2.4 + i * 2.1)) ** 4
            if tw > 0.05:
                sparkle(f, x - 1, y - 2, 6, amount=tw * 0.8)
        return zoom(f, 1.0 + 0.045 * u / 3.6, W * 0.5, H * 0.42)

    return frame


# ══════════════════════════ 샷3 — 역광 속 은빛 채집망 ══════════════════════════

NET_C, NET_RX, NET_RY, NET_ROT = (620, 252), 150, 126, 0.12
HAND_POSE = ("open", 0.8)


def _net_draw(ox, oy):
    """그물·자루·손을 상자(ox, oy 기준) 안에 그리는 함수."""
    cx, cy = NET_C[0] - ox, NET_C[1] - oy
    att_a = 1.0
    ax = cx + NET_RX * math.cos(att_a) * math.cos(NET_ROT) - NET_RY * math.sin(att_a) * math.sin(NET_ROT)
    ay = cy + NET_RX * math.cos(att_a) * math.sin(NET_ROT) + NET_RY * math.sin(att_a) * math.cos(NET_ROT)
    grip = (742 - ox, 500 - oy)
    end = (ax + (grip[0] - ax) * 1.9, ay + (grip[1] - ay) * 1.9)

    def draw(d):
        hh, ww = d.shape[:2]
        pole = Mask(ww, hh).line([(ax, ay), end], 10).arr(0.5)
        paint(d, pole, "#d8b07a", hi=0.2, lo=0.12)
        hoop = Mask(ww, hh).ellipse(cx, cy, NET_RX, NET_RY, NET_ROT).arr(0.5)
        over(d, c("#f6fbff"), hoop * 0.12)
        mesh = Mask(ww, hh)
        for k in range(-24, 25):
            o = k * 17
            mesh.line([(cx + o - 300, cy - 300), (cx + o + 300, cy + 300)], 1.6)
            mesh.line([(cx + o + 300, cy - 300), (cx + o - 300, cy + 300)], 1.6)
        ma = mesh.arr(0.5) * hoop
        over(d, c("#9fb0c6"), ma * 0.8)
        ring = Mask(ww, hh)
        ring.d.ellipse([(cx - NET_RX) * ring.ss, (cy - NET_RY) * ring.ss, (cx + NET_RX) * ring.ss, (cy + NET_RY) * ring.ss],
                       outline=255, width=int(10 * ring.ss))
        ra = ring.arr(0.5)
        ra = _rot_alpha(ra, cx, cy, NET_ROT)
        paint(d, ra, "#e3e9f1", line=1.6, hi=0.35, lo=0.15)
        add(d, c("#fff4d0"), blur_arr(ra, 5) * 0.3)
        hp = hand_parts_rot(60, rot=-0.62, pose=HAND_POSE[0], curl=HAND_POSE[1], sleeve_len=4.0)
        fx, fy = 0.0, -0.62
        rr = -0.62
        wx = grip[0] - (fx * math.cos(rr) - fy * math.sin(rr)) * 60
        wy = grip[1] - (fx * math.sin(rr) + fy * math.cos(rr)) * 60
        draw_hand(d, wx, wy, 60, parts=hp, skin=HERO_SKIN, sleeve=HERO_SLEEVE)
        un = np.maximum(hp["skin"], hp["sleeve"])
        stamp(d, _rim_light(un) * 0.55, wx, wy, "#ffe2a8", "add")

    return draw


def _rot_alpha(a, cx, cy, rot):
    from PIL import Image
    im = Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8), "L")
    im = im.rotate(-math.degrees(rot), resample=Image.Resampling.BILINEAR, center=(cx, cy))
    return np.asarray(im, np.float32) / 255.0


def _rim_light(a):
    from silhouette_kit import rim
    return rim(a, 4, 5, 4)


def net_shot():
    bg = book_sky([(0, "#ffc996"), (0.5, "#ffe2b6"), (1, "#fff1d6")], seed=301)
    sx, sy = NET_C
    add(bg, c("#fff6d8"), radial(W, H, sx, sy, 380, 1.8) * 0.22)
    hills(bg, 302, 520, 22, "#c7a2cf", ink="#9a76a8", freq=(0.002, 0.006), line=1.2, hi=0.04)
    meadow_w(bg, 600, 303, flowers=18, top="#86c460", bottom="#5ea24a")
    sun(bg, sx, sy, r=50, color="#fff6c4", halo=0.38)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    rays = rays_texture(W, H, sx, sy, count=16, seed=304, spread=math.pi * 2, reach=0.9)
    rays *= np.clip((np.hypot(xx - sx, yy - sy) - 56) / 60, 0, 1)
    np.clip(bg, 0, 1, out=bg)
    ox, oy = 430, 80
    net = Sprite(_net_draw(ox, oy), 520, 640, origin=(sx - ox, sy - oy))
    motes = Particles(30, 305, (300, 60, 1000, 600), vel=((-4, 4), (-7, -1)), size=(0.8, 1.8))
    flare = [(0.35, 7, 0.25), (0.62, 12, 0.18), (0.85, 5, 0.3)]

    def frame(u):
        f = bg.copy()
        add(f, c("#fff3cc"), rays * (0.13 + 0.04 * math.sin(u * 2.0)))
        dx, dy = 5 * math.sin(u * 1.5), 3 * math.sin(u * 1.1 + 0.6)
        net.blit(f, sx + dx, sy + dy)
        th = -2.3 + u * 0.9
        gx = sx + dx + NET_RX * math.cos(th) * math.cos(NET_ROT) - NET_RY * math.sin(th) * math.sin(NET_ROT)
        gy = sy + dy + NET_RX * math.cos(th) * math.sin(NET_ROT) + NET_RY * math.sin(th) * math.cos(NET_ROT)
        sparkle(f, gx, gy, 16 + 4 * math.sin(u * 6), amount=0.9)
        if u > 1.2:
            k = span(u, 1.2, 2.0) * (1 - span(u, 2.4, 3.1))
            sparkle(f, sx + dx + 96, sy + dy - 70, 11, amount=k * 0.8, rot=0.3)
        for p, r_, a_ in flare:
            dot(f, lerp(sx, 900, p), lerp(sy, 600, p), r_, c("#fff0c0"), a_)
        motes.draw(f, u, c("#fff1c4"), 0.4)
        return zoom(f, 1.0 + 0.06 * ease_in_out(u / 3.1), W * 0.5, H * 0.42)

    return frame


# ══════════════════════════ 샷4 — 손등에 내려앉는 무당벌레 ══════════════════════════

HAND_W, HAND_S, HAND_ROT = (735, 525), 108, -0.42


def _xfp(x, y, s, rot, ox, oy):
    return ox + (x * math.cos(rot) - y * math.sin(rot)) * s, oy + (x * math.sin(rot) + y * math.cos(rot)) * s


def hand_shot():
    bg = book_sky([(0, "#ffcfa0"), (0.45, "#ffe6bf"), (0.75, "#cfe8a0"), (1, "#8ccb6c")], seed=401)
    back = Mask()
    r = np.random.default_rng(402)
    for _ in range(40):
        x = r.uniform(-20, W + 20)
        _blade(back, (x, H + 20), (x + r.uniform(-60, 60), r.uniform(380, 600)), r.uniform(12, 26), r.uniform(-40, 40))
    over(bg, c("#7cc45a"), blur_arr(back.arr(0.5), 6) * 0.9)
    bg += bokeh(W, H, 403, 24, c("#fff1c0"), rmin=16, rmax=50, alpha=0.13)
    add(bg, c("#fff3d0"), radial(W, H, 980, 60, 600, 1.8) * 0.12)
    np.clip(bg, 0, 1, out=bg)
    hp = hand_parts_rot(HAND_S, rot=HAND_ROT, pose="open", sleeve_len=3.6)
    box = hp["box"]

    def hand_draw(d):
        draw_hand(d, box / 2, box / 2, HAND_S, parts=hp, skin=HERO_SKIN, sleeve=HERO_SLEEVE)

    hand = Sprite(hand_draw, box, box)
    land = _xfp(-0.02, -0.6, HAND_S, HAND_ROT, *HAND_W)
    path = bezier((250, 110), (420, 330), (560, 170), (land[0], land[1] - 4), 60)
    bug = {round(a, 2): insect_parts("beetle", 36, a) for a in np.arange(-3.2, 3.25, 0.1)}
    wing = Mask(80, 80)
    glow = radial(320, 320, 160, 160, 160, 1.6)
    stars = [(-46, -40, 2.0, 15), (52, -30, 2.35, 11), (-30, 34, 2.6, 9), (44, 40, 2.15, 8), (0, -66, 2.9, 12)]

    def bug_at(rot):
        return bug[round(min(3.2, max(-3.2, round(rot, 1))), 2)]

    def frame(u):
        f = bg.copy()
        bob = 2.0 * math.sin(u * 1.6)
        hand.blit(f, HAND_W[0], HAND_W[1] + bob)
        fl = span(u, 0.2, 1.75)
        lx, ly = land[0], land[1] + bob
        if fl < 1:
            e = ease_in_out(fl)
            i = min(len(path) - 2, int(e * (len(path) - 1)))
            x, y = path[i]
            nx, ny = path[i + 1]
            x += math.sin(u * 9) * 3 * (1 - e)
            y += math.cos(u * 7) * 3 * (1 - e)
            rot = math.atan2(nx - x, -(ny - y))
            _wings(f, wing, x, y, rot, u, 1.0)
            draw_insect(f, "beetle", x, y, 36, parts=bug_at(rot))
        else:
            k = span(u, 1.75, 2.05)
            hop = math.sin(math.pi * k) * 6
            rot = lerp(math.atan2(path[-1][0] - path[-3][0], -(path[-1][1] - path[-3][1])), -0.35, ease_out(k))
            if k < 1:
                _wings(f, wing, lx, ly - 4 - hop, rot, u, 1 - k)
            stamp(f, glow * 0.45 * ease_out(span(u, 1.8, 2.4)) * (0.85 + 0.15 * math.sin(u * 3)), lx, ly, "#fff0b0", "add")
            draw_insect(f, "beetle", lx, ly - 4 - hop, 36, parts=bug_at(rot))
            for dx, dy, t0, s in stars:
                kk = span(u, t0, t0 + 0.5)
                if 0 < kk < 1:
                    sparkle(f, lx + dx, ly + dy, s * math.sin(math.pi * kk), amount=0.95, rot=u * 0.8)
        return zoom(f, 1.0 + 0.1 * ease_in_out(u / 3.6), lerp(W * 0.5, land[0], 0.7), lerp(H * 0.5, land[1], 0.7))

    return frame


def _wings(f, m0, x, y, rot, u, amount):
    """날아오는 무당벌레의 속날개(반투명) — 빠르게 떤다."""
    if amount <= 0.01:
        return
    flap = 0.5 + 0.5 * math.sin(u * 70)
    m = Mask(90, 90)
    for sg in (-1, 1):
        a = rot + sg * (1.1 + 0.5 * flap)
        cx, cy = 45 + math.sin(a) * 20, 45 - math.cos(a) * 20
        m.ellipse(cx, cy, 20, 7, a - math.pi / 2)
    wa = m.arr(0.5)
    stamp(f, wa * 0.55 * amount, x, y - 2, "#eef7ff")
    stamp(f, (grow(wa, 1.0) - wa).clip(0, 1) * 0.35 * amount, x, y - 2, "#8aa8c8")


SHOTS = [wide_shot, blades_shot, net_shot, hand_shot]


def score(sc):
    """새벽 바람·먼 새(곤충 소리는 없다) → 사라질 때마다 내려가는 작은 종 → 역광 반짝 → 착지에 따뜻한 칼림바 동기."""
    T = sc.total
    s = [sc.shot(k) for k in range(4)]
    sc.wind([(0, 0), (0.5, 1), (s[2], 0.8), (s[3] + 1, 0.5), (T, 0.4)], amp=0.03, lo=180, hi=1100, gust=0.4)
    sc.birds([(0, 0.7), (s[1], 0.5), (s[1] + 2.5, 0.25), (s[2], 0.45), (T, 0.6)], amp=0.028, rate=0.55)
    sc.pad([hz("D3"), hz("A3"), hz("E4")], [(0, 0), (1.2, 0.7), (s[1] + 0.3, 0.6), (s[1] + 1.0, 0.0), (T, 0)],
           amp=0.04)
    sc.pad([hz("B2"), hz("F#3"), hz("D4")], [(0, 0), (s[1], 0), (s[1] + 0.8, 0.55), (s[2] + 0.2, 0.5), (s[2] + 0.9, 0),
                                              (T, 0)], amp=0.04)
    sc.pad([hz("D3"), hz("F#3"), hz("A3"), hz("D4")], [(0, 0), (s[2], 0), (s[2] + 1.2, 0.65), (T - 1.0, 0.75), (T, 0)],
           amp=0.042, bright=0.3)
    for t0, (a, b) in zip((0.65, 1.45, 2.25), (("A5", "F#5"), ("G5", "E5"), ("F#5", "D5"))):   # 샷2 — 퐁, 사라진다
        at = s[1] + t0 + 0.2
        sc.pluck(at, hz(a), 0.09, 1.2, pan=-0.3, kind="bell")
        sc.pluck(at + 0.16, hz(b), 0.08, 1.4, pan=0.3, kind="bell")
        sc.noise(at, 0.35, 700, 3500, 0.04, decay=9)
    sc.sparkle(s[2] + 0.3, dur=1.6, density=7, amp=0.03)                    # 샷3 — 역광의 반짝임
    sc.chime(s[2] + 1.25, hz("A6"), 0.05, dur=1.6, pan=0.4)
    land = s[3] + 1.75                                                       # 샷4 — 날아와 내려앉는다
    for k in range(5):
        from bk_v1 import flutter
        flutter(sc, s[3] + 0.25 + k * 0.3, 0.32, 0.03, rate=44, lo=400, hi=2600, pan=-0.6 + k * 0.25)
    sc.melody(land, ["D5", "F#5", "A5", "B5", ("A5", 2)], step=0.3, amp=0.13, kind="kalimba")
    sc.sparkle(land + 0.15, dur=1.2, density=9, amp=0.03)
