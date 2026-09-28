"""스토리 영상 1막 — ch1~ch5. 샷 리스트는 Docs/StoryVideos.md §4.

샷 함수 = 정적 레이어를 한 번 굽고 frame(u)를 돌려준다(u: 샷 안의 초, 디졸브 앞당김 0.6초 포함).
"""
import math

import numpy as np

from silhouette_kit import *  # noqa: F401,F403


# ══════════════════════════ 1. ch1_prologue — 초원 새벽 금빛 ══════════════════════════

GOLD = dict(top="#2d3a66", mid="#c9876a", hor="#ffd79a", sun="#fff2c8", haze="#e8b889",
            far="#8c6f7e", mid_h="#4d3f4c", near="#1f1a22", grass="#141217", rim="#ffd89a")


def ch1_s1():
    """새벽 초원 광각 — 이슬 맺힌 풀, 낮게 뜬 해, 옆으로 흐르는 카메라."""
    mx = 110
    w = W + mx * 2
    bg = sky(w, H, [(0, GOLD["top"]), (0.42, GOLD["mid"]), (0.62, GOLD["hor"]), (1, GOLD["haze"])], seed=1)
    add(bg, col(GOLD["sun"]), radial(w, H, w * 0.52, H * 0.6, 560, 2.4) * 0.9)
    add(bg, col("#ffffff"), radial(w, H, w * 0.52, H * 0.6, 70, 1.2) * 0.9)
    rays = rays_texture(w, H, w * 0.52, H * 0.6, 11, seed=3, spread=math.pi * 0.8) * 0.11
    far_m = ridge(Mask(w, H), 11, H * 0.6, 12, freq=(0.003, 0.009, 0.05))
    treeline(far_m, 110, H * 0.605, 26, density=0.6, amp=8)
    far = Layer(far_m.arr(1.2), haze(col(GOLD["far"]), col(GOLD["hor"]), 0.45), 0.25, (mx, 0))
    mid_m = ridge(Mask(w, H), 12, H * 0.7, 16, freq=(0.0025, 0.007, 0.04, 0.11))
    treeline(mid_m, 120, H * 0.7, 58, w=w * 0.35, density=0.4, amp=6)
    treeline(mid_m, 121, H * 0.71, 44, w=w, density=0.1, amp=12)
    mid = Layer(mid_m.arr(0.8), col(GOLD["mid_h"]), 0.5, (mx, 0))
    near_m = ridge(Mask(w, H), 13, H * 0.8, 14)
    grass(near_m, 14, H * 0.8, H * 0.86, 260, (18, 70), 0.3, (1.5, 3.5), 0, w)
    near = Layer(near_m.arr(0.3), col(GOLD["near"]), 0.8, (mx, 0))
    fg_m = Mask(w, H)
    grass(fg_m, 15, H + 10, H + 30, 90, (140, 330), 0.35, (3.0, 7.0), 0, w)
    fg_a = fg_m.arr(0.5)
    fg = Layer(fg_a, col(GOLD["grass"]), 1.2, (mx, 0))
    fg_rim = rim(fg_a, -2, 2, 1.2)
    fog = fog_band(16, w, H, H * 0.6, H * 0.72, 50, 0.55)
    dew = Particles(70, 17, (0, H * 0.72, W, H), vel=((-3, 3), (-1, 1)), size=(0.6, 1.4), wobble=1.5, twinkle=1.0)
    motes = Particles(40, 18, (0, 0, W, H * 0.8), vel=((2, 8), (-6, -2)), size=(0.8, 2.0))

    def frame(u):
        camx = lerp(-mx * 0.9, mx * 0.9, ease_in_out(u / 3.5))
        f = crop(bg, mx + camx * 0.1, 0).copy()
        add(f, col(GOLD["sun"]), crop(rays, mx + camx * 0.1, 0) * (0.85 + 0.15 * math.sin(u * 1.7)))
        far.draw(f, camx)
        over(f, col(GOLD["hor"]), crop(fog, mx + camx * 0.35 + u * 14, 0))
        mid.draw(f, camx)
        near.draw(f, camx)
        dew.draw(f, u, col("#fff4d6"), 0.9)
        fg.draw(f, camx)
        add(f, col(GOLD["rim"]), crop(fg_rim, mx + camx * 1.2, 0) * 0.35)
        motes.draw(f, u, col("#ffe4a8"), 0.5)
        return f

    return frame


def ch1_s2():
    """풀잎 사이 곤충 셋이 하나씩 안개처럼 흐려져 사라진다."""
    bg = sky(W, H, [(0, "#c8906c"), (0.5, "#f1c586"), (1, "#8a7a4e")], seed=21, texture=0.02)
    bg += bokeh(W, H, 22, 26, col("#fff0c0"), 18, 60, 0.22)
    add(bg, col("#fff3cf"), radial(W, H, W * 0.62, H * 0.35, 520, 2.0) * 0.5)
    blades = [  # (밑동 x, 끝 x, 끝 y, 굵기, 흔들림 위상)
        (170, 260, 60, 34, 0.0), (330, 520, 120, 26, 1.3), (620, 560, 40, 30, 2.1), (840, 700, 150, 24, 0.7),
        (1010, 1120, 70, 32, 1.8), (1180, 1090, 210, 22, 2.6), (40, 20, 260, 20, 0.4)]
    perch = [(0, 0.55, 0.9), (2, 0.45, 1.65), (4, 0.62, 2.4)]   # (풀잎, 높이 비, 사라지기 시작 시각)
    mist = Particles(90, 23, (0, 0, W, H), vel=((-6, 6), (-30, -12)), size=(2.0, 5.0), wobble=10)

    def blade_pts(b, u):
        x0, x1, y1, wd, ph = b
        sway = math.sin(u * 1.1 + ph) * 10
        return bezier((x0, H + 20), (x0, H * 0.6), (x1 + sway * 0.5, y1 + 120), (x1 + sway, y1), 16), wd

    def frame(u):
        f = bg.copy()
        m = Mask()
        pts_by = []
        for b in blades:
            pts, wd = blade_pts(b, u)
            m.taper(pts, wd, 3)
            pts_by.append(pts)
        a = m.arr(0.6)
        over(f, col("#1b1812"), a)
        with_rim(f, a, -3, 2, col("#ffe0a0"), 0.6)
        for bi, hk, fade_at in perch:
            pts = pts_by[bi]
            px, py = pts[int(hk * (len(pts) - 1))]
            vis = 1.0 - span(u, fade_at, fade_at + 0.7)
            if vis > 0:
                bm = beetle(Mask(), px + 22, py - 6, 28, rot=-0.3 + 0.1 * math.sin(u * 3 + bi)).arr(0.4)
                over(f, col("#141010"), bm * vis)
                add(f, col("#ffe6b0"), rim(bm, -2, 2, 1.5) * vis * 0.8)
            puff = span(u, fade_at, fade_at + 1.3)
            if 0 < puff < 1:
                for k in range(10):
                    ang = k * 0.63 + bi
                    dot(f, px + 14 + math.cos(ang) * 30 * puff, py - 4 - 40 * puff + math.sin(ang) * 10,
                        6 + 10 * puff, col("#fff4dc"), 0.12 * (1 - puff))
        mist.draw(f, u, col("#fff2d8"), 0.18)
        return f

    return frame


def ch1_s3():
    """은빛 그물을 쥔 손 — 해를 등진 역광. 테를 화면 가운데에 두고 해를 그 안에 넣는다."""
    sun = (W * 0.53, H * 0.4)
    bg = sky(W, H, [(0, "#6d5a73"), (0.4, "#f0b27a"), (0.66, "#ffe3a8"), (1, "#9a7a52")], seed=31)
    add(bg, col("#fff6d8"), radial(W, H, sun[0], sun[1], 460, 2.2))
    add(bg, col("#ffffff"), radial(W, H, sun[0], sun[1], 70, 1.0))
    bg += bokeh(W, H, 32, 18, col("#fff0c0"), 20, 70, 0.14)
    rays = rays_texture(W, H, sun[0], sun[1], 12, seed=33, spread=math.pi * 2) * 0.09

    def frame(u):
        f = bg.copy()
        add(f, col("#fff0c8"), rays * (0.8 + 0.2 * math.sin(u * 2)))
        sway = 0.035 * math.sin(u * 1.3)
        F = (430.0, 640.0)                      # 주먹
        a = -1.08 + sway                        # 자루 방향(오른쪽 위)
        d = (math.cos(a), math.sin(a))
        R = (F[0] + d[0] * 440, F[1] + d[1] * 440)
        ring_rot = a + math.pi / 2
        m = Mask()
        m.line([(F[0] - d[0] * 90, F[1] - d[1] * 90), (F[0] + d[0] * 305, F[1] + d[1] * 305)], 13)
        fist(m, F[0], F[1], 62, rot=-0.62 + sway * 0.5)
        ring = Mask().ellipse(R[0], R[1], 150, 124, ring_rot)
        ia = Mask().ellipse(R[0], R[1], 139, 113, ring_rot).arr(0.4)
        net = Mask()
        for k in range(-7, 8):   # 그물코 — 테 안쪽만 남긴다
            net.line(xf([(k * 22, -150), (k * 22 + 10, 150)], R[0], R[1], 1, ring_rot + 0.5), 1.5, caps=False)
            net.line(xf([(-150, k * 22), (150, k * 22 + 8)], R[0], R[1], 1, ring_rot + 0.5), 1.5, caps=False)
        body = m.arr(0.5)
        ra = np.clip(ring.arr(0.4) - ia, 0, 1)
        over(f, col("#d8dde4"), net.arr(0.3) * ia * 0.45)
        over(f, col("#1c1716"), body)
        over(f, col("#aab3be"), ra)
        with_rim(f, body, 2, 3, col("#ffe5a8"), 0.7, 2.5)
        add(f, col("#ffffff"), rim(ra, 2, 3, 2) * 0.8)
        return f

    return frame


def ch1_s4():
    """손등에 작은 하늘소가 내려앉는다."""
    bg = sky(W, H, [(0, "#9a7e86"), (0.5, "#f2c48a"), (1, "#6f6440")], seed=41, texture=0.02)
    bg += bokeh(W, H, 42, 24, col("#fff0c0"), 16, 56, 0.2)
    add(bg, col("#fff3d0"), radial(W, H, W * 0.34, H * 0.3, 520, 2.0) * 0.6)
    land = 2.0
    wrist = (930.0, 520.0)
    hs = 105.0

    def frame(u):
        f = bg.copy()
        breathe = math.sin(u * 1.2) * 2.5
        m = Mask()
        hand_side(m, wrist[0], wrist[1] + breathe, hs, rot=0.06, curl=0.12, mirror=True)
        a = m.arr(0.6)
        over(f, col("#231b17"), a)
        with_rim(f, a, -2, 3, col("#ffe2a4"), 0.6, 2.5)
        # 손등(손목 기준 로컬 0.45, -0.37)에 앉는다 — 거울이라 x 부호가 바뀐다.
        tx, ty = xf([(-0.45, -0.37)], wrist[0], wrist[1] + breathe, hs, 0.06)[0]
        p = ease_out(span(u, 0.25, land))
        bx = lerp(250, tx, p) + math.sin(u * 7) * 8 * (1 - p)
        by = lerp(170, ty - 9, p) + math.sin(u * 5.3) * 12 * (1 - p)
        settle = span(u, land, land + 0.4)
        wing = (0.5 + 0.5 * math.sin(u * 55)) if u < land else 0.0
        bm = beetle_side(Mask(), bx, by, 26, rot=lerp(0.35, 0.06, max(p, settle)), wing=wing, mirror=True).arr(0.4)
        over(f, col("#120e0c"), bm)
        add(f, col("#ffe6b0"), rim(bm, -2, 2, 1.5) * 0.7)
        return f

    return frame


# ══════════════════════════ 2. ch2_watchers — 연못 청록 ══════════════════════════

def pond_backdrop(seed, hz):
    img = sky(W, H, [(0, "#1d3a46"), (0.3, "#5b9a98"), (hz / H, "#c3e4d8"), (1, "#c3e4d8")], seed=seed)
    add(img, col("#f4fff4"), radial(W, H, W * 0.42, hz - 20, 380, 2.4) * 0.55)
    far = ridge(Mask(), seed + 1, hz - 10, 8, freq=(0.004, 0.02))
    treeline(far, seed + 2, hz - 4, 38, density=0.5, amp=5)
    over(img, haze(col("#2b5157"), col("#c3e4d8"), 0.35), far.arr(1.0))
    return img


def ch2_s1():
    """연못 수면의 반영 — 잠자리 한 마리가 스친다."""
    hz = int(H * 0.46)
    above = pond_backdrop(51, hz)
    fog = fog_band(54, W + 400, H, hz - 50, hz + 10, 40, 0.5)
    touches = (0.9, 1.9, 2.8)

    def path(u):
        return lerp(-80, W + 80, u / 3.4), hz - 58 + math.sin(u * 6) * 10

    def frame(u):
        a = above.copy()
        over(a, col("#dff0e8"), crop(fog, 200 + u * 12, 0))
        f = reflect(a, hz, u, amp=3, tint=col("#10282e"), dark=0.55)
        rm = Mask()
        for tt in touches:
            if u > tt:
                x, _ = path(tt)
                for k in range(3):
                    rr = (u - tt) * 90 - k * 22
                    if rr > 0:
                        ripple(rm, x, hz + 44, rr, 1.4)
        add(f, col("#d8efe6"), rm.arr(0.4) * 0.35)
        x, y = path(u)
        flap = 0.5 + 0.5 * math.sin(u * 48)
        dm = dragonfly(Mask(), x, y, 66, rot=math.pi / 2 + math.sin(u * 3) * 0.05, flap=flap).arr(0.4)
        refl = dragonfly(Mask(), x, 2 * hz - y, 66, rot=math.pi / 2, flap=flap).arr(2.0)
        over(f, col("#0c1c20"), refl * 0.3)
        over(f, col("#0e1a1c"), dm)
        add(f, col("#e8fff6"), rim(dm, -1, 2, 1.2) * 0.6)
        m = Mask()
        reeds(m, 55, H + 20, 16, (280, 440), -20, 200, sway=0.05, t=u)
        reeds(m, 56, H + 20, 20, (260, 420), 1080, W + 20, sway=-0.04, t=u)
        over(f, col("#0b1a1c"), m.arr(1.2))
        return f

    return frame


def ledger_figure(m, x, feet, h, u, face=0.35, writing=True, walk=None):
    """검은 코트의 사내 — 챙 넓은 모자, 손에 작은 수첩."""
    arm = (x + h * 0.13, feet - h * 0.6) if writing else None
    person(m, x, feet, h, "coat", t=u, face=face, arm=arm, walk=walk)
    head_r = h * 0.072
    hx, hy = x + face * h * 0.02, feet - h + head_r
    m.ellipse(hx, hy - head_r * 0.35, head_r * 1.9, head_r * 0.34)
    m.ellipse(hx, hy - head_r * 0.8, head_r * 1.0, head_r * 0.62)
    if writing:
        nx, ny = arm[0] - h * 0.02, arm[1] - h * 0.04
        m.poly(xf([(-h * 0.07, -h * 0.05), (h * 0.05, -h * 0.06), (h * 0.06, h * 0.04), (-h * 0.06, h * 0.05)],
                  nx, ny, 1, -0.15))
        return nx, ny
    return None


def ch2_s2():
    """갈대 너머, 검은 코트가 뒷모습 반쯤으로 수첩에 무언가 적는다."""
    hz = int(H * 0.5)
    above = pond_backdrop(61, hz)
    base = reflect(above, hz, 0.0, amp=2, tint=col("#10282e"), dark=0.6)
    fog = fog_band(62, W + 400, H, H * 0.62, H * 0.9, 60, 0.55)

    def frame(u):
        f = base.copy()
        m = Mask()
        nx, ny = ledger_figure(m, W * 0.55, H * 0.93, 470, u)
        a = m.arr(0.6)
        over(f, col("#0d1416"), a)
        with_rim(f, a, -2, 2, col("#cfeee4"), 0.5, 2.0)
        # 펜 끝이 수첩 위를 오간다 — 글씨는 안 보인다
        pen = Mask().line([(nx - 10 + math.sin(u * 9) * 14, ny - 6 + math.sin(u * 4.3) * 5), (nx + 26, ny - 40)],
                          3.2).arr(0.4)
        over(f, col("#0d1416"), pen)
        over(f, col("#e3f3ee"), crop(fog, 200 + u * 18, 0) * 0.8)
        fg = Mask()
        reeds(fg, 63, H + 40, 22, (380, 640), -40, 380, sway=0.03, t=u)
        reeds(fg, 64, H + 40, 22, (380, 640), 900, W + 40, sway=-0.03, t=u)
        reeds(fg, 65, H + 40, 4, (420, 560), 560, 760, sway=0.02, t=u)
        over(f, col("#081214"), fg.arr(3.0))
        return f

    return frame


def ch2_s3():
    """돌아서 안개 속으로 걸어 들어가 사라진다."""
    hz = int(H * 0.5)
    above = pond_backdrop(61, hz)
    base = reflect(above, hz, 0.0, amp=2, tint=col("#10282e"), dark=0.6)
    fog = fog_band(72, W + 600, H, H * 0.5, H * 0.95, 70, 0.85)

    def frame(u):
        f = base.copy()
        p = ease_in_out(u / 3.6)
        h = lerp(470, 230, p)
        feet = lerp(H * 0.93, H * 0.72, p)
        vis = 1.0 - span(u, 1.5, 3.3)
        m = Mask()
        ledger_figure(m, W * 0.55 + p * 30, feet, h, u, face=lerp(0.35, -0.05, span(u, 0, 0.7)), writing=False,
                      walk=u * 6.5 if u > 0.5 else None)
        over(f, col("#0d1416"), m.arr(0.6) * vis)
        dens = 0.55 + 0.45 * span(u, 0.6, 3.0)
        over(f, col("#e3f3ee"), crop(fog, 300 + u * 22, 0) * dens)
        return f

    return frame


# ══════════════════════════ 3. ch3_scholar — 숲 이끼녹 ══════════════════════════

GLYPH_SEED = 300   # ch11의 나무껍질 각인도 이 씨앗을 쓴다 — "유적과 같은 문양"


def cave_wall(w, h, seed, grain=1.0):
    n = fbm(w, h, seed, (int(220 * grain), int(70 * grain), int(20 * grain)), (0.55, 0.3, 0.15))
    stone = col("#1f271d") * (1 - n[..., None]) + col("#5e6e50") * n[..., None]
    cr = Mask(w, h)
    rng = np.random.default_rng(seed + 7)
    for _ in range(9):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        cr.line(bezier((x, y), (x + rng.uniform(-80, 80), y + 60), (x + rng.uniform(-80, 80), y + 140),
                       (x + rng.uniform(-120, 120), y + 220), 12), rng.uniform(1.5, 3.5), caps=False)
    stone *= (1 - cr.arr(0.8) * 0.55)[..., None]
    # 이끼는 벽 위아래 가장자리에만 옅게 — 가운데까지 덮으면 군복 무늬가 된다.
    band = np.clip(np.abs(np.linspace(-1, 1, h))[:, None] * 1.8 - 0.75, 0, 1)
    moss = np.clip((fbm(w, h, seed + 3, (70, 24), (0.7, 0.3)) - 0.58) * 5, 0, 1) * band
    over(stone, col("#3f5a26"), moss * 0.75)
    add(stone, col("#8ab04a"), np.clip((moss - 0.55) * 2, 0, 1) * 0.12)
    return stone


def carve(img, gm, glow=0.0, glow_col="#ffc35a"):
    """새김 — 홈은 어둡게, 아래오른쪽 턱은 밝게. glow면 홈이 안에서 빛난다."""
    img *= (1 - gm * 0.62)[..., None]
    add(img, col("#c9d8a8"), rim(gm, 1.5, 1.5, 1.0) * 0.35)
    if glow > 0:
        add(img, col(glow_col), (blur_arr(gm, 7) * 0.9 + gm) * glow)
    return img


def ch3_s1():
    """숲 동굴 벽 — 이끼 사이 판독 불가 각인, 초록빛이 걸러져 든다."""
    wall = cave_wall(W, H, 81)
    gm, _ = glyphs(Mask(), 380, 250, 520, 4, GLYPH_SEED, size=30, weight=4.5)
    carve(wall, gm.arr(0.5))
    rays = rays_texture(W, H, -120, -160, 9, seed=82, spread=0.5, center=0.62, reach=1.3) * 0.28
    dust = Particles(60, 83, (0, 0, W * 0.8, H), vel=((2, 6), (3, 9)), size=(0.7, 1.8))

    def frame(u):
        f = wall.copy()
        add(f, col("#d8f0a0"), rays * (0.85 + 0.15 * math.sin(u * 1.1)))
        dust.draw(f, u, col("#f0ffd0"), 0.45)
        return zoom(f, 1.0 + 0.05 * ease_in_out(u / 3.0), W * 0.5, H * 0.45)

    return frame


def ch3_s2():
    """손끝이 각인을 훑는다 — 먼지가 떨어진다."""
    wall = cave_wall(W, H, 91, grain=1.6)
    gm, _ = glyphs(Mask(), 150, 300, 1000, 2, GLYPH_SEED, size=52, weight=7)
    carve(wall, gm.arr(0.6))
    add(wall, col("#d8f0a0"), radial(W, H, W * 0.3, H * 0.1, 700, 1.6) * 0.2)
    row_y = 300 + 26

    def tip(u):
        return lerp(960, 420, ease_in_out(span(u, 0.5, 3.4))), row_y + math.sin(u * 2.3) * 4

    def frame(u):
        f = wall.copy()
        tx, ty = tip(u)
        hs = 120.0
        m = Mask()
        # 손끝(로컬 약 2.05, -0.15)이 tip에 오도록 손목을 둔다(거울).
        hand_side(m, tx + 2.05 * hs, ty + 0.2 * hs, hs, rot=-0.08, curl=0.05, mirror=True)
        a = m.arr(0.8)
        over(f, col("#141a12"), a)
        with_rim(f, a, -2, -2, col("#cfe8a0"), 0.5, 2.0)
        for k in range(40):   # 손끝이 지나간 자리에서 먼지가 떨어진다
            ts = 0.5 + k * 0.075
            if u < ts:
                break
            sx, sy = tip(ts)
            age = u - ts
            dot(f, sx + math.sin(k * 1.7) * 6, sy + 12 + 140 * age * age + 20 * age, 1.6, col("#e8e0c0"),
                0.5 * max(0.0, 1 - age / 1.5))
        return f

    return frame


def ch3_s3():
    """각인 한 줄이 희미하게 빛났다가 잦아든다."""
    wall = cave_wall(W, H, 101, grain=1.3)
    gm_all, _ = glyphs(Mask(), 220, 200, 840, 3, GLYPH_SEED + 1, size=40, weight=5.5)
    carve(wall, gm_all.arr(0.5))
    line_m, _ = glyphs(Mask(), 220, 200 + 64, 840, 1, GLYPH_SEED, size=40, weight=5.5)
    line = line_m.arr(0.5)
    carve(wall, line)
    sparks = Particles(26, 102, (220, 230, 1060, 320), vel=((-4, 4), (-16, -6)), size=(0.8, 1.8))

    def frame(u):
        f = wall.copy()
        g = smooth(span(u, 0.7, 1.9)) * (1 - 0.8 * smooth(span(u, 2.3, 3.5)))
        g *= 0.9 + 0.1 * math.sin(u * 13)
        add(f, col("#ffb84a"), (blur_arr(line, 9) * 0.9 + blur_arr(line, 24) * 0.5 + line * 0.8) * g)
        add(f, col("#ffd890"), radial(W, H, W * 0.5, 280, 520, 2.0) * 0.18 * g)
        sparks.draw(f, u, col("#ffd070"), 0.8 * g)
        return f

    return frame


def ch3_s4():
    """동굴 밖 숲 너머, 먼 능선에 유적의 윤곽."""
    sc = sky(W, H, [(0, "#6d8a6a"), (0.45, "#c8d9a0"), (0.62, "#eef0c0"), (1, "#8aa070")], seed=111)
    add(sc, col("#fffbe0"), radial(W, H, W * 0.5, H * 0.46, 420, 2.0) * 0.6)
    ridge_m = ridge(Mask(), 112, H * 0.5, 22, freq=(0.003, 0.01))
    temple(ridge_m, W * 0.5, H * 0.5 - 18, 150, 120, broken=0.4, seed=113)
    over(sc, haze(col("#5d7458"), col("#eef0c0"), 0.4), ridge_m.arr(0.9))
    for k, (y, hgt, c) in enumerate(((H * 0.62, 50, "#4d6644"), (H * 0.72, 70, "#34482e"), (H * 0.84, 96, "#1f2e1b"))):
        t_m = treeline(Mask(), 114 + k, y, hgt, density=0.35, amp=10)
        over(sc, col(c), t_m.arr(0.8 - k * 0.2))
        over(sc, col("#dfe8c0"), fog_band(117 + k, W, H, y - 30, y + 10, 30, 0.25))
    mouth = cave_mouth(W, H, 118, W * 0.5, H * 0.5, W * 0.46, H * 0.46)
    mouth_rim = rim(mouth, 3, 3, 3)

    def frame(u):
        f = zoom(sc, 1.0 + 0.1 * ease_in_out(u / 3.6), W * 0.5, H * 0.46)
        over(f, col("#0b0f0a"), mouth)
        add(f, col("#b8d890"), mouth_rim * 0.35)
        return f

    return frame


# ══════════════════════════ 4. ch4_crates — 습지 회갈 ══════════════════════════

def marsh_above(seed, hz):
    img = sky(W, H, [(0, "#35342f"), (0.35, "#77725f"), (hz / H, "#b3a98f"), (1, "#b3a98f")], seed=seed)
    add(img, col("#e6dcc0"), radial(W, H, W * 0.62, hz - 60, 60, 1.0) * 0.5)
    add(img, col("#d8ccae"), radial(W, H, W * 0.62, hz - 60, 400, 2.2) * 0.35)
    far = treeline(Mask(), seed + 1, hz - 2, 30, density=0.2, amp=4)
    over(img, col("#6b6755"), far.arr(1.5))
    m = Mask()
    for k, (x, h) in enumerate(((180, 230), (330, 150), (880, 260), (1040, 170), (1180, 120))):
        tree_dead(m, x, hz + 4, h, seed + 10 + k)
    over(img, col("#2a2922"), m.arr(0.8))
    return img


def ch4_s1():
    """안개 낀 습지의 해질녘 — 물 위로 낮은 안개가 흐른다."""
    hz = int(H * 0.56)
    above = marsh_above(121, hz)
    fog1 = fog_band(122, W + 600, H, hz - 70, hz + 40, 50, 0.7)
    fog2 = fog_band(123, W + 600, H, hz + 30, H, 80, 0.5)

    def frame(u):
        f = reflect(above, hz, u, amp=2, tint=col("#23231e"), dark=0.5)
        over(f, col("#c9bfa4"), crop(fog1, 300 + u * 20, 0))
        over(f, col("#a89f86"), crop(fog2, 100 + u * 34, 0) * 0.8)
        return f

    return frame


def ch4_s2():
    """반쯤 잠긴 나무 상자들이 줄지어 — 카메라가 줄을 따라 간다."""
    hz = int(H * 0.46)
    above = marsh_above(131, hz)
    water = reflect(above, hz, 0.0, amp=2, tint=col("#23231e"), dark=0.5)
    fog = fog_band(132, W + 600, H, hz - 40, H, 90, 0.55)
    crates = []
    for i in range(8):   # 가까운 왼쪽 → 먼 오른쪽. 간격을 두어야 울타리가 아니라 상자 줄로 읽힌다.
        k = 0.7 ** i
        crates.append((lerp(1150, 170, k), lerp(hz + 6, H * 0.88, k), 230 * k + 22, i))

    def frame(u):
        f = water.copy()
        cam = 160 * ease_in_out(u / 4.1)
        front = Mask()
        topm = Mask()
        seams = Mask()
        refl = Mask()
        for x, wl, s_, i in reversed(crates):
            sx = x - cam * (s_ / 230.0) * 1.6
            bob = math.sin(u * 1.3 + i) * 2
            fh = s_ * 0.55                                 # 물 위로 드러난 앞면 높이
            top = wl - fh + bob
            tilt = math.sin(i * 1.7) * 0.05
            d = s_ * 0.28                                  # 윗면 깊이(오른쪽 위로 비스듬히)
            front.poly(xf([(-s_ / 2, 0), (s_ / 2, 0), (s_ / 2, fh), (-s_ / 2, fh)], sx, top, 1, tilt))
            topm.poly(xf([(-s_ / 2, 0), (s_ / 2, 0), (s_ / 2 + d, -d * 0.55), (-s_ / 2 + d, -d * 0.55)], sx, top, 1, tilt))
            front.poly(xf([(s_ / 2, 0), (s_ / 2 + d, -d * 0.55), (s_ / 2 + d, fh - d * 0.55), (s_ / 2, fh)], sx, top, 1, tilt))
            crate_seams(seams, sx - s_ / 2, top, s_, fh, 3, max(1.0, s_ / 90))
            refl.poly(xf([(-s_ / 2, 0), (s_ / 2 + d, 0), (s_ / 2 + d, fh * 0.8), (-s_ / 2, fh * 0.8)], sx, wl + 2, 1, -tilt))
        over(f, col("#1d1b16"), refl.arr(3.0) * 0.45)
        fa = front.arr(0.5)
        ta = topm.arr(0.5)
        over(f, col("#2b261e"), fa)
        over(f, col("#4a4234"), ta)
        over(f, col("#16140f"), seams.arr(0.4) * fa)
        with_rim(f, np.clip(fa + ta, 0, 1), 2, 2, col("#c8bb98"), 0.3, 2.0)
        over(f, col("#bdb398"), crop(fog, 300 + u * 16, 0) * 0.75)
        return f

    return frame


def ch4_s3():
    """상자 틈으로 — 어둠 속 더듬이와 날개 끝이 아주 조금 움직인다."""
    grain_n = fbm(W, H, 141, (400, 40, 8), (0.3, 0.4, 0.3))
    wood = col("#2a2118") * (0.7 + 0.6 * grain_n[..., None])
    wood *= (1 + np.sin(np.arange(H) * 0.35)[:, None, None] * 0.06)
    gap0, gap1 = int(H * 0.36), int(H * 0.62)
    inside = sky(W, gap1 - gap0, [(0, "#0a0806"), (0.5, "#15110b"), (1, "#070504")], seed=142, texture=0.0)
    add(inside, col("#6a5a3c"), radial(W, gap1 - gap0, W * 0.2, (gap1 - gap0) * 0.5, 500, 2.0) * 0.4)
    edge = Mask().rect(0, gap0 - 3, W, gap0 + 1).rect(0, gap1 - 1, W, gap1 + 3).arr(1.0)

    beam = radial(W, gap1 - gap0, W * 0.3, (gap1 - gap0) * 0.5, 700, 1.4)

    def frame(u):
        f = wood.copy()
        f[gap0:gap1] = inside
        add(f[gap0:gap1], col("#b09060"), beam * 0.25)
        tw = math.sin(u * 3.1) * 0.05 + math.sin(u * 7.7) * 0.015
        m = Mask()
        # 딱정벌레 머리와 앞가슴(아래쪽 판자에 반쯤 가려진다)
        m.ellipse(470, gap1 + 10, 120, 64)
        m.ellipse(580, gap1 - 18, 58, 44)
        for k, (x0, y0) in enumerate(((610, gap1 - 40), (625, gap1 - 26))):
            m.taper(bezier((x0, y0), (x0 + 90, y0 - 70 + tw * 220), (x0 + 230, y0 - 100 + tw * 300),
                           (x0 + 360 + k * 40, y0 - 70 + tw * 260), 22), 7, 2.5)
        body = m.arr(0.6)
        # 접힌 날개 — 반투명 막과 날개맥
        wing = Mask()
        fl = math.sin(u * 5.3) * 5
        wing.poly([(820, gap1 + 20), (1010, gap0 + 30 + fl), (1180, gap0 + 50 + fl), (1060, gap1 + 30)])
        veins = Mask()
        for k in range(5):
            veins.line([(840, gap1 + 10), (1000 + k * 40, gap0 + 36 + fl + k * 4)], 2.0, caps=False)
        wa = wing.arr(0.8) * 0.55 + veins.arr(0.5) * 0.4
        over(f, col("#070504"), body)
        add(f, col("#e0c890"), rim(body, 2, 3, 2.0) * 0.8)
        add(f, col("#c8b080"), wa * 0.45)
        # 판자가 앞을 가린다 — 틈 밖으로 나온 부분은 판자 뒤다
        f[:gap0] = wood[:gap0]
        f[gap1:] = wood[gap1:]
        f *= (1 - edge * 0.4)[..., None]
        return f

    return frame


def ch4_s4():
    """뚜껑에 못 박힌 표찰 — 판독 불가 글씨, 물이 떨어진다."""
    n = fbm(W, H, 151, (600, 50, 10), (0.3, 0.4, 0.3))
    wood = col("#3a2f22") * (0.65 + 0.7 * n[..., None])
    wood = wood * (1 + (np.sin(np.arange(H)[:, None] * 0.09 + n * 6) * 0.07)[..., None])
    wood *= np.clip(1.1 - np.abs(np.linspace(-1, 1, W))[None, :] * 0.25, 0, 1)[..., None]
    for y in (H * 0.12, H * 0.88):
        wood *= (1 - Mask().rect(0, y - 3, W, y + 3).arr(1.5) * 0.7)[..., None]
    tag = Mask().poly(xf([(-230, -130), (230, -142), (236, 130), (-226, 138)], W * 0.5, H * 0.5, 1, -0.04)).arr(0.6)
    paper = col("#b9b09a") * (0.9 + 0.2 * fbm(W, H, 152, (200, 30), (0.6, 0.4))[..., None])
    img = wood.copy()
    over(img, col("#0e0b08"), shift(tag, 6, 8) * 0.5)
    over(img, paper, tag)
    ink = scribble(Mask(), W * 0.5 - 180, H * 0.5 - 70, 360, 6, 153, row_h=28, weight=2.6).arr(0.5)
    over(img, col("#3a342a"), ink * tag * 0.85)
    nail = Mask().circle(W * 0.5, H * 0.5 - 112, 13).arr(0.6)
    over(img, col("#2a2622"), nail)
    add(img, col("#d8d0c0"), rim(nail, 2, 2, 1.5) * 0.6)
    drops_x = (W * 0.5 - 150, W * 0.5 + 40, W * 0.5 + 190)

    def frame(u):
        f = img.copy()
        for k, dx in enumerate(drops_x):
            period = 1.3 + k * 0.35
            ph = (u + k * 0.5) % period
            base_y = H * 0.5 + 132 + 0.04 * (dx - W * 0.5)
            if ph < period * 0.6:
                grow = ph / (period * 0.6)
                dot(f, dx, base_y + 4 * grow, 3 + 3 * grow, col("#e6ecef"), 0.55)
            else:
                fall = ph - period * 0.6
                dot(f, dx, base_y + 8 + 900 * fall * fall, 4, col("#e6ecef"), 0.6)
        return f

    return frame


# ══════════════════════════ 5. ch5_summit — 산 새벽보라 ══════════════════════════

VIOLET = [(0, "#15142f"), (0.3, "#3f3670"), (0.56, "#b58aa6"), (0.66, "#f2c6a4"), (1, "#f2c6a4")]


def ranges(img, seed, ys, colors, amps):
    for k, (y, c, a) in enumerate(zip(ys, colors, amps)):
        m = peaks(Mask(img.shape[1], img.shape[0]), seed + k, y, a, rough=0.52)
        over(img, col(c), m.arr(1.2 - k * 0.25))
        over(img, col("#d6a8b8"), fog_band(seed + 20 + k, img.shape[1], img.shape[0], y - 20, y + 30, 30, 0.25))
    return img


def ch5_s1():
    """능선 위 두 실루엣의 뒷모습 — 바람에 옷자락이 날린다."""
    sc = sky(W, H, VIOLET, seed=161)
    add(sc, col("#ffe2c0"), radial(W, H, W * 0.7, H * 0.66, 420, 2.2) * 0.7)
    ranges(sc, 162, (H * 0.6, H * 0.66, H * 0.73), ("#7a6a98", "#54497a", "#3a3160"), (40, 50, 60))
    rock = ridge(Mask(), 170, H * 0.84, 18, freq=(0.004, 0.02, 0.07)).arr(0.6)

    def frame(u):
        f = sc.copy()
        over(f, col("#141126"), rock)
        m = Mask()
        person(m, W * 0.45, H * 0.86, 300, "kid", t=u * 1.6, net=True)
        person(m, W * 0.56, H * 0.86, 318, "robe", t=u * 1.6, hair=0.06, face=-0.1, bag=True)
        a = m.arr(0.5)
        over(f, col("#141126"), a)
        with_rim(f, a, -2, 2, col("#ffd2b8"), 0.6, 2.0)
        streaks(f, u, 171, 26, col("#f0d8f0"), 0.18, vx=700, length=60, y0=H * 0.2, y1=H * 0.8)
        return f

    return frame


def ch5_s2():
    """능선 아래 구름이 천천히 갈라진다."""
    sc = sky(W, H, [(0, "#2a2552"), (0.5, "#4c4280"), (1, "#1c1838")], seed=181)
    ranges(sc, 182, (H * 0.55, H * 0.66, H * 0.8), ("#3c3468", "#2c264f", "#1c1836"), (50, 60, 40))
    ext = W + 500
    left = cloud_bank(ext, H, 183, H * 0.55, 260, 70, 60, 170, 0, ext * 0.55)
    right = cloud_bank(ext, H, 184, H * 0.58, 260, 70, 60, 170, ext * 0.45, ext)
    shade = sky(ext, H, [(0, "#f6d6e0"), (0.45, "#cdb0d0"), (0.75, "#7d6b9e"), (1, "#5b4c80")], seed=185, texture=0.0)

    def frame(u):
        f = sc.copy()
        p = ease_in_out(u / 3.6)
        for bank, dx in ((left, 250 + 260 * p), (right, 250 - 260 * p)):
            over(f, crop(shade, dx, 0, W, H), np.clip(crop(bank, dx, 0, W, H) * 1.2, 0, 1))
        return f

    return frame


def valley_ruin():
    sc = sky(W, H, [(0, "#2a2350"), (0.4, "#5a4a86"), (0.62, "#a888b0"), (1, "#3a2f5a")], seed=191)
    for side, seed in ((-1, 192), (1, 193)):
        m = Mask()
        x0 = 0 if side < 0 else W
        pts = [(x0, 0)]
        for k in range(12):
            t = k / 11
            pts.append((x0 - side * (W * 0.34 * (1 - t) ** 0.6 + math.sin(k * 1.9 + seed) * 18), lerp(H * 0.1, H * 1.02, t)))
        pts.append((x0, H))
        m.poly(pts)
        over(sc, col("#1a1534"), m.arr(1.5))
    ruin = temple(Mask(), W * 0.5, H * 0.66, 170, 130, broken=0.3, seed=194).arr(0.8)
    return sc, ruin


def ch5_ruin_shot(push):
    def make():
        sc, ruin = valley_ruin()
        fog1 = fog_band(195, W + 400, H, H * 0.58, H * 0.8, 60, 0.7)
        fog2 = fog_band(196, W + 400, H, H * 0.7, H, 80, 0.6)

        def frame(u):
            f = sc.copy()
            glow = 0.75 + 0.25 * math.sin(u * 2.1)
            add(f, col("#ffb060"), radial(W, H, W * 0.5, H * 0.6, 260, 2.2) * 0.55 * glow)
            over(f, col("#2a2040"), ruin)
            add(f, col("#ffc070"), blur_arr(ruin, 10) * 0.25 * glow)
            over(f, col("#c8b0d8"), crop(fog1, 200 + u * 14, 0) * 0.8)
            over(f, col("#8a78a8"), crop(fog2, 100 + u * 24, 0) * 0.7)
            if push:
                f = zoom(f, 1.0 + 0.28 * ease_in_out(u / 3.1), W * 0.5, H * 0.6)
            return f

        return frame

    return make


ch5_s3 = ch5_ruin_shot(False)
ch5_s4 = ch5_ruin_shot(True)
