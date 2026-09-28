"""스토리 영상 2막·종장 — ch8~ch12, fin. 샷 리스트는 Docs/StoryVideos.md §4.

샷 함수 = 정적 레이어를 한 번 굽고 frame(u)를 돌려준다(u: 샷 안의 초, 디졸브 앞당김 0.6초 포함).
"""
import math

import numpy as np

from silhouette_kit import *  # noqa: F401,F403
from sil_act1 import GLYPH_SEED, GOLD, carve, ledger_figure  # noqa: F401


def lens(m, x, y, length, width, rot):
    """뾰족한 잎 — 가운데가 불룩한 렌즈."""
    pts = []
    for i in range(17):
        t = i / 16
        pts.append((lerp(-length / 2, length / 2, t), -math.sin(t * math.pi) * width / 2))
    for i in range(15, 0, -1):
        t = i / 16
        pts.append((lerp(-length / 2, length / 2, t), math.sin(t * math.pi) * width / 2))
    m.poly(xf(pts, x, y, 1, rot))
    return m


def slab(m, x, base, w, h):
    """빈 석판 — 윗머리가 둥근 선돌."""
    m.rect(x - w / 2, base - h + w * 0.25, x + w / 2, base)
    m.ellipse(x, base - h + w * 0.25, w / 2, w * 0.25)
    return m


# ══════════════════════════ 6. ch8_vault — 모래 황토 ══════════════════════════

def dunes_scene():
    sc = sky(W, H, [(0, "#5a3c22"), (0.35, "#b9824a"), (0.58, "#f3d9a4"), (1, "#e8c68a")], seed=201)
    add(sc, col("#fff3d0"), radial(W, H, W * 0.72, H * 0.4, 420, 2.2) * 0.7)
    for k, (y, c, a) in enumerate(((H * 0.56, "#c99a5c", 30), (H * 0.64, "#a8773e", 40))):
        over(sc, col(c), ridge(Mask(), 202 + k, y, a, freq=(0.002, 0.006, 0.015)).arr(1.0))
    bm = Mask()
    cx, base = W * 0.5, H * 0.74
    bm.rect(cx - 250, base - 230, cx + 250, base)                      # 창고 벽
    bm.poly([(cx - 280, base - 230), (cx + 280, base - 230), (cx + 230, base - 280), (cx - 230, base - 280)])
    wall = bm.arr(0.6)
    over(sc, col("#6b4a2a"), wall)
    beams = Mask()
    for bx in (-250, -125, 0, 125, 250):
        beams.rect(cx + bx - 8, base - 232, cx + bx + 8, base)
    over(sc, col("#4a311b"), beams.arr(0.5) * wall)
    door = Mask().rect(cx - 70, base - 170, cx + 70, base).arr(0.8)
    over(sc, col("#0d0805"), door)
    sand = Mask()
    sand.poly(bezier((-20, base - 40), (260, base - 190), (430, base - 30), (cx, base + 18), 30)
              + bezier((cx, base + 18), (cx + 200, base + 10), (1000, base - 110), (W + 20, base - 90), 30)[1:]
              + [(W + 20, H + 4), (-20, H + 4)])
    ridge(sand, 203, H * 0.86, 12, freq=(0.003, 0.01))
    over(sc, col("#c9985a"), sand.arr(1.0))
    add(sc, col("#fff0c8"), rim(sand.arr(1.0), 2, 3, 2) * 0.5)
    return sc, (cx, base - 85)


def ch8_s1():
    """모래에 반쯤 묻힌 창고 입구 — 카메라가 안으로 들어간다."""
    sc, door = dunes_scene()

    def frame(u):
        f = sc.copy()
        streaks(f, u, 204, 30, col("#fff0d0"), 0.16, vx=500, length=50, y0=H * 0.5, y1=H)
        p = ease_in_out(span(u, 0.6, 3.0))
        f = zoom(f, 1.0 + 1.6 * p * p, door[0], door[1])
        return f * (1 - 0.8 * span(u, 2.3, 3.0))

    return frame


def vault_interior(seed, depth=7):
    """천장까지 쌓인 상자 벽 두 줄이 가운데 소실점으로 모인다."""
    img = sky(W, H, [(0, "#120c07"), (0.5, "#2a1c10"), (1, "#0e0905")], seed=seed, texture=0.03)
    fronts = Mask()
    seams = Mask()
    vx, vy = W * 0.5, H * 0.46
    for i in range(depth, -1, -1):
        k = 0.72 ** i
        for side in (-1, 1):
            x_in = vx + side * W * 0.1 * (1 - k) + side * W * 0.18 * k
            x_out = vx + side * W * 0.62 * k + side * 20
            floor = vy + H * 0.5 * k
            ceil = vy - H * 0.55 * k
            cols = 2
            rows = 5
            cw = (x_out - x_in) / cols
            rh = (floor - ceil) / rows
            for c in range(cols):
                for r_ in range(rows):
                    x0 = x_in + c * cw
                    y0 = ceil + r_ * rh
                    xa, xb = sorted((x0, x0 + cw * 0.94))
                    fronts.rect(xa, y0, xb, y0 + rh * 0.94)
                    crate_seams(seams, xa, y0, xb - xa, rh * 0.94, 3, max(0.8, 1.6 * k))
    fa = fronts.arr(0.6)
    n = fbm(W, H, seed + 3, (90, 20), (0.6, 0.4))
    over(img, col("#5a3f22") * (0.8 + 0.4 * n[..., None]), fa)
    over(img, col("#2a1b0d"), seams.arr(0.5) * fa * 0.7)
    return img, fa


def ch8_s2():
    """천장까지 쌓인 상자들 — 비스듬한 빛줄기 속 먼지."""
    img, fa = vault_interior(211)
    rays = rays_texture(W, H, W * 0.35, -60, 6, seed=212, spread=0.5, center=1.2, reach=1.2)
    add(img, col("#f0b860"), rays * fa * 0.45)
    dust = Particles(120, 213, (0, 0, W, H), vel=((-3, 5), (-2, 4)), size=(0.6, 1.6))

    def frame(u):
        f = img.copy()
        add(f, col("#ffd890"), rays * (0.3 + 0.05 * math.sin(u * 1.4)))
        d = np.zeros_like(f)
        dust.draw(d, u, col("#ffe8b0"), 1.0)
        f += d * (0.15 + rays[..., None] * 1.6)
        return zoom(f, 1.0 + 0.07 * ease_in_out(u / 4.1), W * 0.5, H * 0.46)

    return frame


def ch8_s3():
    """상자 줄을 따라 이동 — 몇몇은 뚜껑이 열린 채 비어 있다."""
    ext = W + 520
    img = sky(ext, H, [(0, "#140d07"), (0.6, "#2c1d10"), (1, "#100a05")], seed=221, texture=0.03)
    body = Mask(ext, H)
    lids = Mask(ext, H)
    holes = Mask(ext, H)
    seams = Mask(ext, H)
    rng = np.random.default_rng(222)
    for row, (base, s_) in enumerate(((H * 0.52, 150), (H * 0.9, 190))):
        x = 30.0
        while x < ext - 40:
            w = s_ * rng.uniform(0.9, 1.1)
            h = s_ * 0.7
            body.rect(x, base - h, x + w, base)
            crate_seams(seams, x, base - h, w, h, 3, 2.0)
            if rng.random() < 0.35:   # 열린 상자 — 안이 비었다
                holes.poly([(x + 6, base - h), (x + w - 6, base - h), (x + w - 18, base - h - 20), (x + 18, base - h - 20)])
                lids.poly(xf([(0, 0), (w, 0), (w, 12), (0, 12)], x + 4, base - h - 10, 1, -0.55))
            x += w + rng.uniform(14, 40)
        body.rect(0, base, ext, base + 14)
    ba = body.arr(0.5)
    over(img, col("#3b2915"), ba)
    over(img, col("#1a1109"), seams.arr(0.5) * ba)
    over(img, col("#050302"), holes.arr(0.5))
    over(img, col("#4a3219"), lids.arr(0.5))
    rays = rays_texture(ext, H, ext * 0.5, -120, 10, seed=223, spread=0.9, center=math.pi / 2, reach=1.3)

    def frame(u):
        ox = 480 * ease_in_out(u / 3.6)
        f = crop(img, ox, 0).copy()
        add(f, col("#f0b860"), crop(rays, ox * 0.6, 0) * 0.35)
        return f

    return frame


def ch8_s4():
    """상자 하나가 조용히 멈춰 있다 — 움직이는 것은 먼지뿐."""
    img = sky(W, H, [(0, "#0c0804"), (0.7, "#1e150b"), (1, "#2a1d10")], seed=231, texture=0.03)
    cone = Mask().poly([(W * 0.46, -20), (W * 0.54, -20), (W * 0.68, H * 0.86), (W * 0.32, H * 0.86)]).arr(30)
    add(img, col("#f0c070"), cone * 0.35)
    add(img, col("#f0c070"), radial(W, H, W * 0.5, H * 0.84, 300, 1.8) * 0.3)
    c = Mask()
    crate(c, W * 0.5 - 150, H * 0.84 - 190, 300, 190)
    c.poly([(W * 0.5 - 150, H * 0.84 - 190), (W * 0.5 + 150, H * 0.84 - 190), (W * 0.5 + 190, H * 0.84 - 230),
            (W * 0.5 - 110, H * 0.84 - 230)])
    ca = c.arr(0.5)
    over(img, col("#4a3319"), ca)
    over(img, col("#1f150a"), crate_seams(Mask(), W * 0.5 - 150, H * 0.84 - 190, 300, 190, 4, 3.0).arr(0.5) * ca)
    add(img, col("#ffe0a0"), rim(ca, 0, 3, 2.5) * 0.6)
    dust = Particles(50, 232, (W * 0.3, 0, W * 0.7, H * 0.85), vel=((-2, 2), (1, 4)), size=(0.6, 1.4))

    def frame(u):
        f = img.copy()
        dust.draw(f, u, col("#ffe8b8"), 0.5)
        return f

    return frame


# ══════════════════════════ 7. ch9_archive — 서릿길 청백 ══════════════════════════

def ice_wall(w, h, seed):
    base = sky(w, h, [(0, "#8fc2dc"), (0.5, "#d4eef8"), (1, "#7cb0cf")], seed=seed, texture=0.02)
    rng = np.random.default_rng(seed + 1)
    for depth in range(3):   # 깊을수록 흐리고 푸르다
        m = Mask(w, h)
        for _ in range(9 - depth * 2):
            x, y = rng.uniform(40, w - 40), rng.uniform(40, h - 40)
            s_ = rng.uniform(30, 55) * (1 - depth * 0.25)
            kind = rng.integers(0, 3)
            rot = rng.uniform(-0.6, 0.6)
            if kind == 0:
                beetle(m, x, y, s_, rot)
            elif kind == 1:
                butterfly(m, x, y, s_ * 1.3, rot)
            else:
                dragonfly(m, x, y, s_ * 1.4, rot)
        over(base, lerp(col("#2a4a60"), col("#8ab8d2"), depth / 2.5), m.arr(1.0 + depth * 3.0) * (0.8 - depth * 0.2))
    cracks = Mask(w, h)
    for _ in range(6):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        pts = [(x, y)]
        for _ in range(4):
            x += rng.uniform(-30, 30)
            y += rng.uniform(30, 80)
            pts.append((x, y))
        cracks.line(pts, rng.uniform(0.8, 1.4), caps=False)
    add(base, col("#ffffff"), cracks.arr(0.8) * 0.3)
    streak = np.asarray(Image.fromarray((fbm(max(8, w // 10), h, seed + 5, (20, 6), (0.6, 0.4)) * 255).astype(np.uint8)).resize(
        (w, h), Image.Resampling.BICUBIC), np.float32) / 255.0
    add(base, col("#ffffff"), np.clip((streak - 0.45) * 0.6, 0, 1))
    return base


def ch9_s1():
    """얼음 벽 속에 봉인된 곤충들 — 차가운 청백 빛."""
    ext = W + 200
    wall = ice_wall(ext, H, 241)
    glints = Particles(40, 242, (0, 0, W, H), vel=((-1, 1), (-1, 1)), size=(0.8, 2.2), twinkle=1.0)

    def frame(u):
        f = crop(wall, 180 * ease_in_out(u / 3.0), 0).copy()
        glints.draw(f, u * 1.5, col("#ffffff"), 0.7)
        return f

    return frame


def ch9_s2():
    """얼음 벽 앞 두 실루엣이 마주 선다 — 얼굴은 보이지 않는다."""
    wall = ice_wall(W, H, 251)
    wall = wall * 0.75 + blur_arr(np.mean(wall, axis=2), 12)[..., None] * 0.25
    floor = Mask().rect(0, H * 0.84, W, H).arr(2.0)
    over(wall, col("#5d8aa6"), floor * 0.7)

    def frame(u):
        f = wall.copy()
        m = Mask()
        b1, b2 = math.sin(u * 1.1) * 2, math.sin(u * 1.1 + 1) * 2
        person(m, W * 0.38, H * 0.9, 430, "robe", t=u, face=0.95, hair=0.08, bob=b1)
        ledger_figure(m, W * 0.63, H * 0.9 - b2, 450, u, face=-0.95, writing=False)
        for x, h_, face, bob in ((W * 0.38, 430, 1, b1), (W * 0.63, 450, -1, b2)):
            hr = h_ * 0.068
            hx = x + face * 0.95 * h_ * 0.025
            hy = H * 0.9 - bob - h_ + hr
            m.ellipse(hx + face * hr * 0.9, hy + hr * 0.12, hr * 0.28, hr * 0.2)   # 코끝
            m.ellipse(hx + face * hr * 0.6, hy + hr * 0.6, hr * 0.35, hr * 0.28)   # 턱
        a = m.arr(0.6)
        refl = np.flipud(a)
        shiftv = int(H * 0.9 * 2 - H)
        rf = np.zeros_like(a)
        if shiftv > 0:
            rf[shiftv:] = refl[:H - shiftv]
        over(f, col("#2a4a60"), blur_arr(rf, 3) * 0.3)
        over(f, col("#0f1c26"), a)
        with_rim(f, a, 0, 2, col("#ffffff"), 0.6, 2.0)
        return f

    return frame


def memory_page(progress, seed=261):
    """세피아 회상 — 젊은 손이 긴 목록을 적는 종이."""
    img = sky(W, H, [(0, "#8a6a44"), (0.5, "#c9a878"), (1, "#7a5a36")], seed=seed, texture=0.06)
    ink = scribble(Mask(), 260, 150, 700, 13, seed + 1, row_h=34, weight=2.4, progress=progress).arr(0.5)
    over(img, col("#3a2814"), ink * 0.8)
    return img


def iced(img, seed):
    """회상을 얼음에 비친 것처럼 — 균열·서리·가장자리 청색."""
    ice = ice_wall(W, H, seed)
    out = img * 0.84 + ice * 0.16
    edge = 1 - radial(W, H, W * 0.5, H * 0.5, 860, 1.6)
    over(out, col("#9fcfe6"), np.clip(edge * 1.3, 0, 1) * 0.55)
    return out


def ch9_s3():
    """얼음에 비친 회상 — 젊은 손이 목록을 적는다(판독 불가), 따뜻한 빛."""
    def frame(u):
        prog = 0.15 + 0.85 * span(u, 0.2, 4.4)
        img = memory_page(prog)
        # 펜 끝 — 적힌 끝자리를 따라간다(근사: 줄 진행)
        row = int(prog * 12.5)
        tx = 260 + (prog * 13 - row) * 640
        ty = 150 + row * 34
        m = Mask()
        fist(m, tx + 95, ty + 72, 46, rot=0.75 + math.pi)   # 주먹은 펜 끝을 향하고 팔은 오른쪽 아래로
        m.line([(tx, ty), (tx + 64, ty + 44)], 5)
        over(img, col("#2a1808"), m.arr(1.2) * 0.9)
        return iced(img, 262)

    return frame


def ch9_s4():
    """지금의 장갑 낀 손이 그 반영 위를 덮는다."""
    page = iced(memory_page(1.0), 262)

    def frame(u):
        f = page.copy()
        p = ease_out(span(u, 0.3, 2.2))
        m = Mask()
        hand(m, lerp(W * 0.6, W * 0.52, p), lerp(H + 240, H * 0.78, p), lerp(110, 150, p), rot=-0.12, pose="open", curl=0.05)
        a = m.arr(lerp(6.0, 1.0, p))
        over(f, col("#101820"), a)
        with_rim(f, a, 0, -2, col("#dff3fb"), 0.5, 2.5)
        frost = span(u, 2.0, 4.6)
        if frost > 0:
            rr = radial(W, H, W * 0.52, H * 0.55, 200 + 700 * frost, 1.2)
            n = fbm(W, H, 263, (50, 14), (0.6, 0.4))
            add(f, col("#ffffff"), np.clip(rr * (n + 0.2) * 1.4 - 0.2, 0, 1) * 0.55 * frost)
        return f

    return frame


# ══════════════════════════ 8. ch10_kiln — 잿불 주홍 ══════════════════════════

def tunnel(seed):
    img = sky(W, H, [(0, "#1a0805"), (0.5, "#3a140a"), (1, "#120503")], seed=seed, texture=0.05)
    add(img, col("#ff7a30"), radial(W, H, W * 0.5, H * 0.52, 320, 1.8) * 0.9)
    add(img, col("#ffd080"), radial(W, H, W * 0.5, H * 0.52, 60, 1.2) * 0.8)
    frames = []
    for i in range(7, -1, -1):
        k = 0.7 ** i
        hw, hh = W * 0.55 * k + 30, H * 0.62 * k + 20
        cx, cy = W * 0.5, H * 0.52
        frames.append((cx, cy, hw, hh, k))
    walls = Mask()
    walls.poly([(0, 0), (W * 0.5 - 40, H * 0.52 - 30), (W * 0.5 - 40, H * 0.52 + 30), (0, H)])
    walls.poly([(W, 0), (W * 0.5 + 40, H * 0.52 - 30), (W * 0.5 + 40, H * 0.52 + 30), (W, H)])
    wa = walls.arr(4.0)
    img *= (1 - wa * 0.55)[..., None]
    return img, frames


def timber(m, cx, cy, hw, hh, k, cap=True):
    t = max(4.0, 26 * k)
    m.rect(cx - hw, cy - hh, cx - hw + t, cy + hh)
    m.rect(cx + hw - t, cy - hh, cx + hw, cy + hh)
    if cap:
        m.rect(cx - hw - t * 0.3, cy - hh, cx + hw + t * 0.3, cy - hh + t)
    return m


def ch10_s1():
    """잿불 갱도 — 불티가 떠다닌다."""
    img, frames = tunnel(271)
    m = Mask()
    for cx, cy, hw, hh, k in frames:
        timber(m, cx, cy, hw, hh, k)
    fa = m.arr(0.6)
    over(img, col("#140604"), fa)
    add(img, col("#ff9a50"), rim(fa, 3, 0, 2.0) * 0.35)
    embers = Particles(90, 272, (0, 0, W, H), vel=((-8, 8), (-40, -14)), size=(0.7, 2.0), wobble=12)

    def frame(u):
        f = img.copy()
        add(f, col("#ff6a20"), radial(W, H, W * 0.5, H * 0.52, 360, 2.0) * (0.15 + 0.08 * math.sin(u * 5.1)))
        embers.draw(f, u, col("#ffb050"), 0.9)
        return zoom(f, 1.0 + 0.06 * ease_in_out(u / 3.0), W * 0.5, H * 0.52)

    return frame


def ch10_s2():
    """들보가 무너진다 — 흔들림, 먼지, 불똥."""
    img, frames = tunnel(281)
    still = Mask()
    for cx, cy, hw, hh, k in frames[:-1]:
        timber(still, cx, cy, hw, hh, k)
    sa = still.arr(0.6)
    over(img, col("#140604"), sa)
    near = frames[-1]
    burst = Particles(140, 282, (W * 0.2, H * 0.1, W * 0.8, H * 0.6), vel=((-160, 160), (-220, 60)), size=(0.8, 2.4), wobble=4)
    dust = fog_band(283, W + 400, H, 0, H, 200, 1.0)
    fall_at = 1.0
    rng = np.random.default_rng(284)
    jit = rng.uniform(-1, 1, (200, 2))

    def frame(u):
        f = img.copy()
        cx, cy, hw, hh, k = near
        m = Mask()
        timber(m, cx, cy, hw, hh, k, cap=False)
        tf = max(0.0, u - fall_at)
        t = max(4.0, 26 * k)
        drop = 900 * tf * tf
        rot = min(1.2, tf * 1.6)
        m.poly(xf([(-hw - t * 0.3, 0), (hw + t * 0.3, 0), (hw + t * 0.3, t), (-hw - t * 0.3, t)], cx + tf * 60, cy - hh + drop, 1, rot * 0.35))
        for j in range(6):   # 떨어지는 나무 조각·돌
            if tf > 0:
                px = cx + (j - 2.5) * 110 + jit[j, 0] * 30
                py = cy - hh + 40 + 1100 * tf * tf * (0.7 + 0.1 * j)
                m.poly(xf([(-18, -10), (20, -8), (16, 12), (-14, 10)], px, py, 1 + j * 0.2, tf * (2 + j)))
        a = m.arr(0.6)
        over(f, col("#140604"), a)
        if tf > 0:
            d = np.zeros_like(f)
            burst.draw(d, tf * 0.8, col("#ffb060"), 1.0)
            f += d * max(0.0, 1 - tf / 2.2)
            over(f, col("#6a3a22"), crop(dust, 200 + u * 30, 0) * min(0.85, tf * 0.7))
        sh = max(0.0, 1 - tf / 1.2) * (1 if tf > 0 else 0)
        i = int(u * 24) % 200
        f = zoom(f, 1.03, W * 0.5 + jit[i, 0] * 18 * sh, H * 0.5 + jit[i, 1] * 14 * sh)
        return f

    return frame


def ch10_s3():
    """검은 소매의 손이 재 속에서 다른 팔을 붙잡아 끌어낸다."""
    img = sky(W, H, [(0, "#2a120a"), (0.5, "#5a2a16"), (1, "#1a0a06")], seed=291, texture=0.06)
    add(img, col("#ff8a40"), radial(W, H, W * 0.3, H * 0.2, 600, 1.6) * 0.35)
    rubble = Mask()
    rng = np.random.default_rng(292)
    for _ in range(40):
        x, y = rng.uniform(-40, W + 40), rng.uniform(H * 0.7, H + 20)
        s_ = rng.uniform(30, 90)
        rubble.poly(xf([(-1, 0.3), (-0.6, -0.7), (0.4, -0.9), (1, -0.1), (0.7, 0.6)], x, y, s_, rng.uniform(0, 6)))
    ra = rubble.arr(0.8)
    ash = Particles(80, 293, (0, 0, W, H), vel=((-6, 6), (8, 24)), size=(0.7, 1.8))

    def frame(u):
        f = img.copy()
        lift = 90 * ease_in_out(span(u, 1.4, 3.8))
        grab = ease_out(span(u, 0.3, 1.3))
        m = Mask()
        # 재 속에서 올라온 팔(왼쪽 아래 → 오른쪽 위)
        hand_side(m, 520, 560 - lift, 95, rot=-0.55, curl=0.35, cuff=False)
        arm_a = m.arr(0.6)
        g = Mask()
        # 검은 소매의 손 — 오른쪽 위에서 내려와 팔뚝을 쥔다
        gx, gy = lerp(1000, 700, grab), lerp(80, 420, grab) - lift
        fist(g, gx, gy, 88, rot=math.pi * 0.72)
        ga = g.arr(0.6)
        over(f, col("#1a0c08"), arm_a)
        over(f, col("#2a1208"), ra)
        add(f, col("#ff9a50"), rim(ra, 0, 3, 2) * 0.35)
        over(f, col("#050202"), ga)
        add(f, col("#ffb070"), rim(ga, -2, 2, 2) * 0.5 + rim(arm_a, -2, 2, 2) * 0.35)
        ash.draw(f, u, col("#d8c0b0"), 0.5)
        return f

    return frame


def ch10_s4():
    """잿빛 재 위에 놓인 상자 하나 — 주위에 불씨가 빛난다."""
    img = sky(W, H, [(0, "#1a0c08"), (0.55, "#3a2a24"), (0.62, "#6a625c"), (1, "#3a3430")], seed=301, texture=0.05)
    ground_n = fbm(W, H, 302, (120, 30), (0.6, 0.4))
    gm = Mask().rect(0, H * 0.62, W, H).arr(8.0)
    over(img, col("#5a544e") * (0.8 + 0.4 * ground_n[..., None]), gm * 0.9)
    c = Mask()
    crate(c, W * 0.5 - 140, H * 0.72 - 170, 280, 170)
    c.poly([(W * 0.5 - 140, H * 0.72 - 170), (W * 0.5 + 140, H * 0.72 - 170), (W * 0.5 + 180, H * 0.72 - 205),
            (W * 0.5 - 100, H * 0.72 - 205)])
    ca = c.arr(0.5)
    over(img, col("#2a1a0e"), ca)
    over(img, col("#120a05"), crate_seams(Mask(), W * 0.5 - 140, H * 0.72 - 170, 280, 170, 4, 3.0).arr(0.5) * ca)
    rng = np.random.default_rng(303)
    coals = [(rng.uniform(W * 0.15, W * 0.85), rng.uniform(H * 0.66, H * 0.95), rng.uniform(1.5, 4.0), rng.uniform(0, 6))
             for _ in range(40)]
    smoke = fog_band(304, W + 300, H, H * 0.2, H * 0.7, 100, 0.4)

    def frame(u):
        f = img.copy()
        for x, y, r_, ph in coals:
            dot(f, x, y, r_ * 2, col("#ff7a30"), 0.5 + 0.4 * math.sin(u * 2.3 + ph))
        add(f, col("#ff9a50"), rim(ca, 0, -3, 2) * 0.35)
        over(f, col("#8a7a70"), crop(smoke, 100 + u * 10, 0) * 0.35)
        return f

    return frame


def ch10_s5():
    """들려 있던 두꺼운 장부가 천천히 덮인다."""
    img = sky(W, H, [(0, "#1a0805"), (0.6, "#3a160a"), (1, "#140604")], seed=311, texture=0.05)
    add(img, col("#ff8a40"), radial(W, H, W * 0.5, H * 0.45, 560, 1.8) * 0.4)

    def frame(u):
        f = img.copy()
        op = 1.0 - ease_in_out(span(u, 0.5, 2.6))
        cx, cy = W * 0.5, H * 0.5
        bw, bh = 250, 330
        cover = Mask()
        book(cover, cx, cy, bw + 12, bh + 16, op)
        pages = Mask()
        book(pages, cx, cy - 4, bw, bh, max(op, 0.5) if op > 0.5 else op)
        ca = cover.arr(0.6)
        over(f, col("#2a0f06"), ca)
        pa = pages.arr(0.6)
        if op > 0.02:
            over(f, col("#caa878"), pa * min(1.0, op * 2))
            ink = scribble(Mask(), cx - bw + 30, cy - bh / 2 + 40, bw - 60, 9, 312, row_h=28, weight=2.2).arr(0.5)
            if op > 0.55:
                ink2 = scribble(Mask(), cx + 30, cy - bh / 2 + 40, (bw - 60) * (2 * op - 1), 9, 313, row_h=28, weight=2.2).arr(0.5)
                ink = np.clip(ink + ink2, 0, 1)
            over(f, col("#3a2010"), ink * pa * 0.8 * min(1.0, op * 2))
        add(f, col("#ffb070"), rim(ca, 0, 3, 2) * 0.5)
        m = Mask()
        fist(m, cx - 60, cy + bh / 2 + 30, 60, rot=-1.4)
        over(f, col("#0a0403"), m.arr(0.6))
        return f

    return frame


# ══════════════════════════ 9. ch11_crown — 우듬지 신록 ══════════════════════════

def leaves_layer(w, h, seed, count, size, cy_bias=0.5):
    m = Mask(w, h)
    rng = np.random.default_rng(seed)
    for _ in range(count):
        x = rng.uniform(-40, w + 40)
        y = rng.normal(h * cy_bias, h * 0.3)
        lens(m, x, y, size * rng.uniform(0.7, 1.3), size * 0.42, rng.uniform(0, math.pi))
    return m.arr(0.6)


def ch11_s1():
    """거대수 꼭대기 — 신록 사이로 햇빛이 깜빡인다."""
    sky_img = sky(W + 80, H + 80, [(0, "#f6ffd8"), (0.5, "#c8ec90"), (1, "#7fc05a")], seed=321)
    add(sky_img, col("#ffffff"), radial(W + 80, H + 80, W * 0.55, H * 0.4, 380, 2.0))
    layers = [(leaves_layer(W + 80, H + 80, 322 + k, 160 - k * 40, 70 + k * 40, 0.45 + k * 0.1), c, k)
              for k, c in enumerate(("#6aa84a", "#3f7a32", "#1f4a1c"))]
    rays = rays_texture(W, H, W * 0.55, H * 0.4, 14, seed=325, spread=math.pi * 2) * 0.25
    br = Mask()
    for a in (-2.2, -1.7, -1.2, -0.7):
        br.taper(bezier((W * 0.5, H + 40), (W * 0.5 + math.cos(a) * 200, H * 0.8), (W * 0.5 + math.cos(a) * 500, H * 0.5 + math.sin(a) * 200),
                        (W * 0.5 + math.cos(a) * 800, H * 0.3 + math.sin(a) * 300), 16), 60, 12)
    ba = br.arr(0.8)

    def frame(u):
        f = sky_img[40:40 + H, 40:40 + W].copy()
        add(f, col("#fff8d0"), rays * (0.7 + 0.3 * math.sin(u * 3.3) * math.sin(u * 1.7)))
        over(f, col("#2a3a18"), ba)
        for a, c, k in layers:
            dx = math.sin(u * (0.8 + k * 0.3) + k) * (4 + k * 4)
            dy = math.cos(u * (0.6 + k * 0.2)) * (3 + k * 3)
            over(f, col(c), crop(a, 40 + dx, 40 + dy))
        return f

    return frame


def ch11_s2():
    """나무껍질에 새겨진 문양 — 3장의 유적 각인과 같은 형태."""
    n = fbm(W, H, 331, (300, 60, 14), (0.4, 0.4, 0.2))
    bark = col("#3a2d1b") * (0.75 + 0.5 * n[..., None])
    grooves = Mask()
    ridges_m = Mask()
    rng = np.random.default_rng(332)
    x = -20.0
    while x < W + 20:
        pts = []
        xx = x
        for y in range(-20, H + 40, 40):
            xx += rng.uniform(-6, 6)
            pts.append((xx, y))
        grooves.line(pts, rng.uniform(3, 7), caps=False)
        ridges_m.line([(px + 9, py) for px, py in pts], rng.uniform(2, 4), caps=False)
        if rng.random() < 0.3:   # 가로로 끊긴 골
            y0 = rng.uniform(0, H)
            grooves.line([(x - 10, y0), (x + 24, y0 + rng.uniform(-8, 8))], 3, caps=False)
        x += rng.uniform(22, 42)
    bark *= (1 - grooves.arr(1.0) * 0.6)[..., None]
    add(bark, col("#8a7450"), ridges_m.arr(1.5) * 0.25)
    gm, _ = glyphs(Mask(), 330, 250, 620, 3, GLYPH_SEED, size=40, weight=5.5)
    carve(bark, gm.arr(0.5))
    dapple = np.clip((fbm(W + 200, H, 333, (120, 40), (0.7, 0.3)) - 0.5) * 3, 0, 1)

    def frame(u):
        f = bark.copy()
        add(f, col("#d8f0a0"), crop(dapple, 100 + math.sin(u) * 60, 0) * 0.2)
        return zoom(f, 1.0 + 0.04 * u / 3.6, W * 0.5, H * 0.45)

    return frame


def greenhouse(m, cx, base, w, h):
    m.rect(cx - w / 2, base - h * 0.45, cx + w / 2, base)
    m.ellipse(cx, base - h * 0.45, w / 2, h * 0.55)
    return m


def valley_greenhouse(w, h, seed, gx, gy, gw):
    img = sky(w, h, [(0, "#bfe89a"), (0.4, "#e8f6c0"), (0.55, "#9ccc6a"), (1, "#4a8a3a")], seed=seed)
    for k, (y, c) in enumerate(((h * 0.5, "#8ab860"), (h * 0.6, "#6aa04a"), (h * 0.75, "#4a8036"))):
        over(img, col(c), ridge(Mask(w, h), seed + k, y, 16, freq=(0.003, 0.012)).arr(1.0))
    rng = np.random.default_rng(seed + 9)
    fl = np.zeros((h, w, 3), np.float32)
    for _ in range(900):
        x, y = rng.uniform(0, w), rng.uniform(h * 0.58, h)
        c = [col("#ffd0e0"), col("#fff0a0"), col("#ffffff"), col("#f0a0c0")][rng.integers(0, 4)]
        dot(fl, x, y, rng.uniform(1.0, 2.4) * (0.6 + (y - h * 0.58) / h), c, 0.9)
    img = np.clip(img + fl * 0.8, 0, 1.2)
    gm = greenhouse(Mask(w, h), gx, gy, gw, gw * 0.7).arr(0.6)
    over(img, col("#cfe8e0"), gm * 0.75)
    panes = Mask(w, h)
    for k in range(-3, 4):
        panes.line([(gx + k * gw / 7, gy), (gx + k * gw / 9, gy - gw * 0.7)], 1.6, caps=False)
    panes.line([(gx - gw / 2, gy - gw * 0.32), (gx + gw / 2, gy - gw * 0.32)], 1.6, caps=False)
    over(img, col("#6a8a7a"), panes.arr(0.4) * gm)
    return img


def ch11_s3():
    """우듬지를 타고 내려다보면 — 멀리 꽃밭의 유리온실이 반짝인다."""
    top = sky(W, H, [(0, "#f6ffd8"), (1, "#a8dc78")], seed=341)
    add(top, col("#ffffff"), radial(W, H, W * 0.5, H * 0.3, 380, 2.0) * 0.8)
    over(top, col("#3f7a32"), leaves_layer(W, H, 342, 140, 110, 0.55))
    over(top, col("#1f4a1c"), leaves_layer(W, H, 343, 70, 170, 0.8))
    bottom = valley_greenhouse(W, H, 344, W * 0.5, H * 0.66, 150)
    tall = np.concatenate([top, bottom], axis=0)
    seam = int(H * 0.25)
    y0, y1 = H - seam, H + seam
    a = np.array([smooth((y - y0) / (y1 - y0)) for y in range(y0, y1)], np.float32)[:, None, None]
    top_ext = np.concatenate([top, np.repeat(top[-1:], seam, axis=0)], axis=0)
    bot_ext = np.concatenate([np.repeat(bottom[:1], seam, axis=0), bottom], axis=0)
    tall[y0:y1] = top_ext[y0:y1] * (1 - a) + bot_ext[0:2 * seam] * a
    fringe = leaves_layer(W, H * 2, 345, 60, 180, 0.35)

    def frame(u):
        oy = H * ease_in_out(span(u, 0.3, 3.4))
        f = crop(tall, 0, oy).copy()
        over(f, col("#1a3a16"), crop(fringe, 0, oy * 1.2))
        glint = max(0.0, math.sin(u * 3.0)) ** 8
        dot(f, W * 0.5 - 30, H * 0.66 - 60 + (H - oy), 14, col("#ffffff"), 0.9 * glint * span(u, 1.5, 3.0))
        return f

    return frame


def ch11_s4():
    """큰 나무와 온실이 한 화면에 — 둘 다 살아 있다."""
    img = valley_greenhouse(W, H, 351, W * 0.66, H * 0.7, 120)
    tree = Mask()
    tree.taper([(W * 0.3, H + 20), (W * 0.31, H * 0.45)], 120, 60)
    for a in (-2.5, -1.9, -1.2, -0.6):
        tree.taper([(W * 0.31, H * 0.55), (W * 0.31 + math.cos(a) * 240, H * 0.4 + math.sin(a) * 160)], 40, 14)
    rng = np.random.default_rng(352)
    for _ in range(26):
        tree.circle(W * 0.31 + rng.normal(0, 170), H * 0.2 + rng.normal(0, 80), rng.uniform(60, 110))
    for _ in range(220):   # 수관 가장자리의 잎 — 둥근 덩어리 윤곽을 깬다
        a = rng.uniform(0, 2 * math.pi)
        rr = rng.uniform(0.8, 1.15)
        lens(tree, W * 0.31 + math.cos(a) * 330 * rr, H * 0.2 + math.sin(a) * 190 * rr, rng.uniform(26, 44), 14, a + 1.2)
    ta = tree.arr(0.5)
    inner = Mask()
    for _ in range(160):
        lens(inner, W * 0.31 + rng.normal(0, 200), H * 0.18 + rng.normal(0, 100), rng.uniform(24, 40), 13, rng.uniform(0, 3.14))
    ia = inner.arr(0.6) * ta

    def frame(u):
        f = img.copy()
        over(f, col("#1f3a18"), ta)
        add(f, col("#4a7a30"), ia * (0.45 + 0.05 * math.sin(u * 2)))
        add(f, col("#e8ffb0"), rim(ta, 3, 3, 2.5) * 0.55)
        glint = max(0.0, math.sin(u * 2.4 + 1)) ** 10
        dot(f, W * 0.66 - 20, H * 0.7 - 55, 16, col("#ffffff"), 0.9 * glint)
        return zoom(f, 1.0 + 0.04 * u / 3.6)

    return frame


# ══════════════════════════ 10. ch12_ledger — 빈칸 무채색 ══════════════════════════

def ch12_s1():
    """빈 석판들이 원형으로 둘러선 방 — 회색."""
    img = sky(W, H, [(0, "#1c1c1e"), (0.5, "#4a4a4c"), (1, "#2a2a2c")], seed=361, texture=0.05)
    floor = Mask().ellipse(W * 0.5, H * 0.78, W * 0.48, H * 0.2).arr(3.0)
    over(img, col("#5a5a5a"), floor * 0.6)
    add(img, col("#e8e8e8"), radial(W, H, W * 0.5, H * 0.72, 380, 1.6) * 0.3)
    shaft = Mask().poly([(W * 0.44, -20), (W * 0.56, -20), (W * 0.64, H * 0.8), (W * 0.36, H * 0.8)]).arr(40)
    slabs = []
    for i in range(12):
        a = 2 * math.pi * i / 12 + 0.2
        x = W * 0.5 + math.cos(a) * W * 0.4
        yb = H * 0.74 + math.sin(a) * H * 0.14
        k = 0.65 + 0.35 * (math.sin(a) + 1) / 2
        slabs.append((yb, x, k))
    slabs.sort()
    back = Mask()
    front = Mask()
    for yb, x, k in slabs:
        target = front if yb > H * 0.74 else back
        slab(target, x, yb, 70 * k, 230 * k)
    ba, fa = back.arr(0.6), front.arr(0.6)
    over(img, col("#3a3a3c"), ba)
    add(img, col("#bdbdbd"), rim(ba, 0, 3, 2) * 0.3)
    dust = Particles(60, 362, (W * 0.35, 0, W * 0.65, H * 0.8), vel=((-2, 2), (2, 6)), size=(0.6, 1.4))

    def frame(u):
        f = img.copy()
        add(f, col("#dcdcdc"), shaft * 0.18)
        dust.draw(f, u, col("#f0f0f0"), 0.4)
        over(f, col("#262628"), fa)
        add(f, col("#cfcfcf"), rim(fa, 0, 3, 2) * 0.3)
        return zoom(f, 1.0 + 0.05 * ease_in_out(u / 3.0), W * 0.5, H * 0.6)

    return frame


def shelves(w, h, seed, dusty=None):
    """바닥부터 천장까지 장부 선반 — 책등 폭·높이·톤이 제각각이다."""
    img = sky(w, h, [(0, "#1a1a1b"), (1, "#2a2a2b")], seed=seed, texture=0.03)
    rng = np.random.default_rng(seed + 1)
    shelf_h = 150
    y = 20
    spines = np.zeros((h, w), np.float32)
    tone = np.zeros((h, w), np.float32)
    boards = Mask(w, h, 1)
    while y < h:
        x = 10.0
        while x < w - 10:
            bw = rng.uniform(14, 34)
            bh = shelf_h * rng.uniform(0.7, 0.95)
            y0, y1 = int(y + shelf_h - bh), int(y + shelf_h)
            x0, x1 = int(x), int(x + bw - 2)
            spines[max(0, y0):max(0, min(h, y1)), x0:x1] = 1.0
            tone[max(0, y0):max(0, min(h, y1)), x0:x1] = rng.uniform(0.25, 0.6)
            if rng.random() < 0.3:   # 책등의 띠
                band = int(y0 + bh * rng.uniform(0.2, 0.7))
                if 0 <= band < h:
                    tone[band:band + 4, x0:x1] *= 0.6
            x += bw
        boards.rect(0, y + shelf_h, w, y + shelf_h + 14)
        y += shelf_h + 14
    out = img * (1 - spines[..., None]) + (col("#6a6560") * tone[..., None] * 1.4) * spines[..., None]
    over(out, col("#141414"), boards.arr(0.5))
    if dusty is not None:
        over(out, col("#a8a6a2"), dusty * 0.55)
    return out


def ch12_s2():
    """벽면을 가득 채운 장부 선반 — 천천히 올려다본다."""
    tall = shelves(W, H * 2, 371)
    shade = radial(W, H, W * 0.5, H * 0.5, 900, 1.2)

    def frame(u):
        oy = H * (1 - ease_in_out(span(u, 0, 4.1)))
        f = crop(tall, 0, oy).copy()
        return f * (0.55 + 0.45 * shade[..., None])

    return frame


def ch12_s3():
    """선반 절반이 먼지에 덮여 빛바래 있다."""
    xs = np.linspace(0, 1, W)[None, :]
    dusty = np.clip((xs - 0.45) * 4, 0, 1) * np.ones((H, 1), np.float32)
    dusty = dusty * (0.7 + 0.3 * fbm(W, H, 381, (80, 20), (0.6, 0.4)))
    img = shelves(W, H, 382, dusty)
    webs = Mask()
    for cx, cy in ((W * 0.82, 30), (W * 0.62, 200)):
        for k in range(6):
            a = k * 0.5
            webs.line([(cx, cy), (cx + math.cos(a) * 160, cy + math.sin(a) * 160)], 1.0, caps=False)
        for r_ in (40, 80, 120):
            webs.d.arc([(cx - r_) * 2, (cy - r_) * 2, (cx + r_) * 2, (cy + r_) * 2], 0, 150, fill=255, width=2)
    add(img, col("#d0d0d0"), webs.arr(0.4) * 0.35)
    dust = Particles(70, 383, (W * 0.4, 0, W, H), vel=((-3, 3), (2, 7)), size=(0.6, 1.5))

    def frame(u):
        f = img.copy()
        dust.draw(f, u, col("#e0e0e0"), 0.5)
        return crop(np.pad(f, ((0, 0), (0, 60), (0, 0)), mode="edge"), 50 * ease_in_out(u / 4.1), 0)

    return frame


def ch12_s4():
    """노인의 뒷모습이 옆으로 비켜서고, 시선이 안쪽 어둠으로 간다."""
    img = sky(W, H, [(0, "#2a2a2c"), (0.6, "#4a4a4c"), (1, "#333335")], seed=391, texture=0.05)
    door = Mask()
    door.rect(W * 0.5 - 150, H * 0.2, W * 0.5 + 150, H * 0.9)
    door.ellipse(W * 0.5, H * 0.2, 150, 110)
    da = door.arr(1.0)
    frame_m = Mask()
    frame_m.rect(W * 0.5 - 180, H * 0.12, W * 0.5 + 180, H * 0.92)
    frame_m.ellipse(W * 0.5, H * 0.14, 180, 130)
    fa = np.clip(frame_m.arr(1.0) - da, 0, 1)
    over(img, col("#5a5a5c"), fa)
    inner = sky(W, H, [(0, "#000000"), (0.6, "#060607"), (1, "#101012")], seed=392, texture=0.0)
    img = img * (1 - da[..., None]) + inner * da[..., None]
    add(img, col("#9a9aa0"), rim(da, 0, 0, 3) * 0.15)

    def frame(u):
        f = img.copy()
        p = ease_in_out(span(u, 1.0, 2.8))
        m = Mask()
        person(m, lerp(W * 0.5, W * 0.18, p), H * 0.95, 430, "old", t=u, face=lerp(0, -0.6, p),
               walk=u * 5 if 1.0 < u < 2.8 else None)
        m.line([(lerp(W * 0.5, W * 0.18, p) + 60, H * 0.95 - 190), (lerp(W * 0.5, W * 0.18, p) + 70, H * 0.95)], 8)
        a = m.arr(0.6)
        over(f, col("#141416"), a)
        add(f, col("#b0b0b4"), rim(a, 2, 0, 2) * 0.3)
        push = ease_in_out(span(u, 2.2, 4.6))
        return zoom(f, 1.0 + 0.45 * push, W * 0.5, H * 0.5)

    return frame


# ══════════════════════════ 11. fin_epilogue — 초원 새벽 금빛 ══════════════════════════

def dawn_field(seed, horizon=0.66, grass_n=200):
    img = sky(W, H, [(0, GOLD["top"]), (0.42, GOLD["mid"]), (horizon, GOLD["hor"]), (1, GOLD["haze"])], seed=seed)
    add(img, col(GOLD["sun"]), radial(W, H, W * 0.5, H * horizon, 520, 2.4) * 0.8)
    far = ridge(Mask(), seed + 1, H * horizon, 10, freq=(0.003, 0.009, 0.05))
    treeline(far, seed + 2, H * horizon + 4, 24, density=0.6, amp=6)
    over(img, haze(col(GOLD["far"]), col(GOLD["hor"]), 0.45), far.arr(1.2))
    near = ridge(Mask(), seed + 3, H * (horizon + 0.12), 12)
    grass(near, seed + 4, H * (horizon + 0.12), H * (horizon + 0.16), grass_n, (16, 60), 0.3, (1.5, 3.2))
    over(img, col(GOLD["near"]), near.arr(0.4))
    return img


def fin_s1():
    """노인의 뒷모습 — 들판에서 손을 펴 곤충 하나를 놓아준다."""
    img = dawn_field(401)

    def frame(u):
        f = img.copy()
        m = Mask()
        x, feet = W * 0.45, H * 0.97
        hand_pt = (x + 150, feet - 300)
        person(m, x, feet, 430, "old", t=u, arm=hand_pt)
        m.ellipse(hand_pt[0] + 14, hand_pt[1] - 4, 22, 9, rot=-0.3)     # 편 손바닥
        for k in range(4):
            m.line([(hand_pt[0] + 20, hand_pt[1] - 6 + k * 3), (hand_pt[0] + 44, hand_pt[1] - 12 + k * 5)], 4.5)
        a = m.arr(0.6)
        over(f, col("#1f1a22"), a)
        with_rim(f, a, -2, 2, col(GOLD["rim"]), 0.6, 2.2)
        rel = span(u, 1.2, 3.6)
        bx = hand_pt[0] + 10 + 260 * rel + math.sin(u * 8) * 6 * rel
        by = hand_pt[1] - 20 - 300 * rel * (1.2 - rel * 0.4)
        wing = (0.5 + 0.5 * math.sin(u * 55)) if rel > 0 else 0.0
        bm = beetle_side(Mask(), bx, by, 20, rot=-0.4 * rel, wing=wing).arr(0.4)
        over(f, col("#120e0c"), bm)
        add(f, col("#ffe6b0"), rim(bm, -1, 2, 1.2) * 0.6)
        return f

    return frame


def fin_s2():
    """새벽 초원을 걷는 두 실루엣 — 한쪽은 팔을 감싼 채."""
    ext = W + 400
    img = dawn_field(411)
    fg = Mask(ext, H)
    grass(fg, 412, H + 10, H + 30, 90, (120, 260), 0.3, (3.0, 6.0), 0, ext)
    fa = fg.arr(0.6)

    def frame(u):
        f = img.copy()
        m = Mask()
        ph = u * 5.2
        person(m, W * 0.44, H * 0.9, 330, "kid", t=u, sling=True, walk=ph, bob=abs(math.sin(ph)) * 4)
        person(m, W * 0.57, H * 0.9, 360, "coat", t=u, walk=ph + 1.6, bob=abs(math.sin(ph + 1.6)) * 4)
        a = m.arr(0.6)
        over(f, col("#1f1a22"), a)
        with_rim(f, a, -2, 2, col(GOLD["rim"]), 0.6, 2.2)
        over(f, col(GOLD["grass"]), crop(fa, 300 * u / 3.6, 0))
        return f

    return frame


def fin_s3():
    """새 장부에 펜이 움직인다 — 이름 칸만 있고 수량 칸이 없다."""
    paper = sky(W, H, [(0, "#d8c49a"), (1, "#b8a070")], seed=421, texture=0.05)
    add(paper, col("#fff0c0"), radial(W, H, W * 0.2, H * 0.1, 900, 1.4) * 0.4)
    rule = Mask()
    rule.line([(W * 0.36, 60), (W * 0.36, H - 60)], 2.5, caps=False)
    for k in range(10):
        rule.line([(W * 0.3, 120 + k * 52), (W * 0.64, 120 + k * 52)], 1.2, caps=False)
    over(paper, col("#8a6a40"), rule.arr(0.4) * 0.5)
    paper *= (0.55 + 0.45 * radial(W, H, W * 0.4, H * 0.3, 1000, 0.8))[..., None]

    def frame(u):
        f = paper.copy()
        prog = span(u, 0.2, 3.5)
        ink = scribble(Mask(), W * 0.38, 104, W * 0.24, 9, 422, row_h=52, weight=2.6, progress=prog).arr(0.5)
        over(f, col("#2a1a0c"), ink * 0.85)
        row = min(8, int(prog * 9))
        tx = W * 0.38 + (prog * 9 - row) * W * 0.2
        ty = 104 + row * 52
        m = Mask()
        m.line([(tx, ty), (tx + 90, ty + 100)], 7)
        fist(m, tx + 150, ty + 170, 58, rot=-2.25)
        a = m.arr(0.6)
        over(f, col("#1a120a"), a * 0.92)
        return f

    return frame


def fin_s4():
    """빈 석판 하나가 여전히 비어 있다."""
    img = sky(W, H, [(0, "#4a4a50"), (0.6, "#9a9a9c"), (0.62, "#6a6a66"), (1, "#3a3a38")], seed=431, texture=0.05)
    s = slab(Mask(), W * 0.5, H * 0.8, 150, 420).arr(0.6)
    over(img, col("#3a3a3c"), s)
    add(img, col("#e0e0e0"), rim(s, -2, 2, 2) * 0.35)
    mist = fog_band(432, W + 300, H, H * 0.6, H, 80, 0.5)

    def frame(u):
        f = img.copy()
        add(f, col("#fff4e0"), radial(W, H, W * 0.3, H * 0.2, 700, 1.6) * 0.12 * span(u, 0, 3.6))
        over(f, col("#b0b0b0"), crop(mist, 100 + u * 12, 0) * 0.5)
        return f

    return frame


def fin_s5():
    """새벽 초원 — 그물 없이 손을 뻗는다. 작은 곤충이 다가와 앉는다."""
    img = dawn_field(441, horizon=0.6, grass_n=260)
    img += bokeh(W, H, 442, 18, col("#fff0c0"), 16, 50, 0.14)
    land = 2.3

    def frame(u):
        f = img.copy()
        m = Mask()
        wx, wy = 1060.0, 600.0
        hand_side(m, wx, wy, 110, rot=0.78, curl=0.0, mirror=True)
        a = m.arr(0.6)
        over(f, col("#231b17"), a)
        with_rim(f, a, -2, 3, col("#ffe2a4"), 0.6, 2.5)
        tx, ty = xf([(-2.05, -0.3)], wx, wy, 110, 0.78)[0]
        p = ease_out(span(u, 0.3, land))
        bx = lerp(330, tx, p) + math.sin(u * 7) * 8 * (1 - p)
        by = lerp(210, ty - 10, p) + math.sin(u * 5.1) * 10 * (1 - p)
        wing = (0.5 + 0.5 * math.sin(u * 55)) if u < land else 0.0
        bm = beetle(Mask(), bx, by, 14, rot=lerp(1.9, 1.2, p), wing=wing).arr(0.4)
        over(f, col("#120e0c"), bm)
        add(f, col("#ffe6b0"), rim(bm, -1, 2, 1.2) * 0.6)
        return f

    return frame
