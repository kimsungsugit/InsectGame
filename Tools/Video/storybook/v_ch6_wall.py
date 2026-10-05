"""ch6 「이름 벽」 — ch6_secret **대사 앞**에 튼다(설명 영상). 이 장면의 설명을 영상이 맡는다.

뒤에 오는 대사가 "사라졌던 고대의 잠자리예요! / 이게 다 곤충 이름이야? / 여기가 '이름 벽' / 잦아듦은 이름이 바래서 /
그 빈칸을 당신 도감이 메웠어요"이므로, 영상은 그 다섯 가지를 그림으로 먼저 보여 준다.

    샷1  0.0~3.0   신전 — 빛줄기 아래 거대한 이름 벽. 아래(세 사람 뒷모습)에서 위로 틸트, 금빛이 칸을 훑어 오른다
    샷2  2.4~6.2   옛날(세피아·둥근 테두리) — 옛사람의 손이 끌로 이름을 새기면 칸이 켜지고 나비가 그림에서 빠져나와 난다
    샷3  5.6~9.2   한 칸이 잿빛으로 바래고, 금빛 실이 끊기고, 아래 풀잎 위 무당벌레가 흐려져 사라진다 — 빈칸
    샷4  8.6~12.2  펼친 도감에 펜이 무당벌레를 그리면 금빛 실이 벽으로 날아올라 빈칸이 다시 켜진다(틸트 업)
    샷5 11.6~15.0  벽 한가운데가 크게 빛나고 날개 넷 고대 잠자리가 벽에서 빠져나와 날아오른다

샷3·4는 같은 무당벌레 칸이다 — 사라진 이름을 도감이 되돌린다는 걸 한 마리로 잇는다.
"""
import math

import numpy as np

from kit import (INK, PAL, H, W, Mask, Particles, add, bake_layer, blur_arr, c, dot, draw_insect, draw_person,
                 ease_in_out, ease_out, grow, insect_parts, lerp, local, meadow, over, paint, put_layer, radial,
                 rays_texture, smooth, span, sparkle, stamp, zoom)
from kit import book as draw_book
from kit import crop
from silhouette_kit import sprite
import bk_v2 as bk
from sound import hz

DURS = [3.0, 3.2, 3.0, 3.0, 2.8]
TINT = "#fff4e2"
REVERB = 0.45
CUES = [
    (0.6, 2.2, "벽 가득, 곤충 이름이 새겨져 있다."),
    (3.0, 2.8, "옛사람들은 이름을 새겨 곤충을 지켰다."),
    (6.1, 2.6, "이름이 바래면, 그 곤충이 사라졌다."),
    (9.1, 2.6, "도감에 적으면, 이름이 다시 빛난다."),
    (12.1, 2.6, "그리고 사라졌던 곤충이 돌아온다!"),
]

ICON_KINDS = ("bfly", "beetle", "dfly")
WALL_KINDS = ("bfly", "beetle", "dfly", "moth", "firefly", "longhorn", "mantis", "ant")   # 큰 벽은 여러 종(이름이 많다)
STONE, STONE_DARK, RECESS = "#e4c690", "#c9a36a", "#7d5f3c"
FLOOR, FLOOR2 = "#d2ad74", "#b98f58"


def _floor(img, y0, vx, vy, seed=0, w=None):
    """돌바닥 — 그라데이션 + 소실점으로 모이는 줄눈 + 가로 줄눈(멀수록 촘촘)."""
    hh, ww = img.shape[:2]
    m = Mask(ww, hh)
    m.rect(0, y0, ww, hh)
    a = m.arr(0.6)
    yy = np.clip((np.arange(hh, dtype=np.float32)[:, None, None] - y0) / max(1, hh - y0), 0, 1)
    grad = c(FLOOR) * (1 - yy) + c(FLOOR2) * yy
    over(img, grad, a)
    lm = Mask(ww, hh)
    for k in range(-9, 10):
        lm.line([(vx + k * 18, y0), (vx + k * 260, hh + 40)], 2.0)
    yv = y0
    step = 10.0
    while yv < hh:
        lm.line([(0, yv), (ww, yv)], 2.0)
        step *= 1.45
        yv += step
    over(img, c("#9a7446"), lm.arr(0.6) * a * 0.45)
    over(img, c(INK), np.clip(grow(a, 1.4) - a, 0, 1) * 0.6)


def _pillar(img, x0, x1, y_top, y_bot):
    """세로 홈 기둥 — 기둥머리·받침이 있다."""
    hh, ww = img.shape[:2]
    m = Mask(ww, hh)
    m.rect(x0, y_top, x1, y_bot)
    m.rect(x0 - 16, y_top - 34, x1 + 16, y_top + 4, r=6)
    m.rect(x0 - 12, y_bot - 6, x1 + 12, y_bot + 26, r=4)
    a = m.arr(0.6)
    paint(img, a, STONE, hi=0.14, lo=0.16)
    fl = Mask(ww, hh)
    for k in range(1, 5):
        x = x0 + (x1 - x0) * k / 5
        fl.line([(x, y_top + 16), (x, y_bot - 16)], 2.6)
    over(img, c(STONE_DARK), fl.arr(0.6) * a * 0.8)


# ══════════════════════════ 샷1 — 거대한 이름 벽(틸트 업) ══════════════════════════

TW, TH = W, 1600


def tall_wall_shot():
    wall = bk.WallCanvas(TW, TH, rows=11, cols=9, cell=92, gap=14, y0=170, seed=401, kinds=WALL_KINDS)
    floor_y = 1336

    def temple(cv):
        mm = Mask(TW, TH)
        mm.rect(0, 0, 44, TH)
        mm.rect(TW - 44, 0, TW, TH)
        over(cv, c(RECESS), mm.arr(0.6))
        fr = Mask(TW, TH)   # 위쪽 띠(조각 무늬)
        fr.rect(0, 0, TW, 132)
        fa = fr.arr(0.6)
        paint(cv, fa, "#d8b67c", hi=0.0, lo=0.25, light=(0, -1))
        zz = Mask(TW, TH)
        pts = [(x, 92 + (14 if (x // 40) % 2 else -14)) for x in range(-40, TW + 80, 40)]
        zz.line(pts, 4)
        for x in range(60, TW, 120):
            zz.circle(x, 42, 14)
        over(cv, c("#a8834e"), zz.arr(0.6) * fa)
        over(cv, c(INK), np.clip(grow(fa, 1.4) - fa, 0, 1) * 0.7)
        _pillar(cv, 44, 150, 170, floor_y - 30)
        _pillar(cv, TW - 150, TW - 44, 170, floor_y - 30)
        _floor(cv, floor_y, TW / 2, floor_y - 260)
        add(cv, c("#fff0c0"), radial(TW, TH, 640, 1420, 360, 1.6) * 0.22)   # 사람들 발밑의 빛 웅덩이
        sh = Mask(TW, TH)
        for x in (565, 640, 718):
            sh.ellipse(x, 1422, 34, 8)
        over(cv, c("#6b4a2a"), sh.arr(3) * 0.35)
        draw_person(cv, "raon", 565, 1420, 118, arm=(0.16, -1.18))
        draw_person(cv, "hero", 640, 1420, 122)
        draw_person(cv, "sera", 718, 1420, 142, t=0.4)
        for bx, w0, w1, amt in ((150, 40, 170, 0.26), (430, 30, 120, 0.2), (-80, 50, 200, 0.14)):
            bk.beam(cv, bx, -60, bx + 640, TH, w0, w1, "#fff4c8", amt, soft=22)
        add(cv, c("#fff3c8"), radial(TW, TH, 120, 0, 520, 1.8) * 0.35)

    layer = bake_layer(temple, TW, TH)
    dim = wall.draw(wall.img.copy(), lambda r, k: 0.42)
    lit = wall.draw(wall.img.copy(), lambda r, k: 1.12)
    put_layer(dim, *layer)
    put_layer(lit, *layer)
    bw = np.zeros((TH, TW), np.float32)   # 빛줄기가 닿은 칸은 늘 조금 더 밝다
    for bx, w0, w1 in ((150, 40, 170), (430, 30, 120)):
        m = Mask(TW, TH, 1)
        dx, dy = 640, TH + 60
        ln = math.hypot(dx, dy)
        nx, ny = -dy / ln, dx / ln
        m.poly([(bx + nx * w0, -60 + ny * w0), (bx + dx + nx * w1, TH + ny * w1), (bx + dx - nx * w1, TH - ny * w1),
                (bx - nx * w0, -60 - ny * w0)])
        bw = np.maximum(bw, m.arr(20))
    bw *= 0.45
    motes = Particles(150, 6011, (0, 0, TW, TH), vel=((-3, 5), (-10, -3)), size=(0.7, 1.8))
    rows = np.arange(H, dtype=np.float32)

    def frame(u):
        y0 = lerp(TH - H - 2, 0, ease_in_out(span(u, 0.5, 2.8)))
        sy = lerp(TH + 120, -260, ease_in_out(span(u, 0.3, 2.7)))
        d = bk.window(dim, y0)
        li = bk.window(lit, y0)
        band = np.exp(-((rows + y0 - sy) / 190.0) ** 2)[:, None]
        wgt = np.clip(0.12 + band * 0.9 + crop(bw, 0, y0), 0, 1)
        f = d + (li - d) * wgt[..., None]
        motes.draw(f, u, c(PAL["rimlight"]), 0.45, oy=y0)
        return zoom(f, 1.06 - 0.06 * ease_out(span(u, 0.0, 3.0)), W / 2, H * 0.5)

    return frame


# ══════════════════════════ 샷2 — 옛날, 이름을 새기다 ══════════════════════════

def carve_shot():
    wall = bk.WallCanvas(W, H, rows=2, cols=3, cell=290, gap=40, y0=-195, seed=611,
                         kinds_at={(1, 0): "beetle", (1, 1): "bfly", (1, 2): "dfly", (0, 0): "dfly", (0, 1): "beetle",
                                   (0, 2): "bfly"})
    base = wall.draw(wall.img.copy(), lambda r, k: 0.9, skip={(1, 1)})
    cx, cy = wall.center(1, 1)
    k = wall.k
    frame_fx = bk.MemoryFrame(seed=62)
    rng = np.random.default_rng(612)
    chips = rng.uniform(-1, 1, (60, 3))
    icon_y = cy - 8 * k
    old_skin, old_sleeve = "#f0c8a8", "#efe8da"

    def frame(u):
        f = base.copy()
        p = span(u, 0.25, 1.75)
        lit = smooth(span(u, 1.72, 2.02))
        lv = 0.25 + 0.95 * lit - 0.15 * smooth(span(u, 2.05, 2.6))
        fill_a = smooth(span(u, 1.75, 2.1))
        fly = span(u, 2.2, 3.7)
        g, cells = wall.carve_mask(1, 1, max(0.001, p)) if p < 1 else (None, None)
        wall.draw_cell(f, 1, 1, lv, icon_alpha=fill_a if fly <= 0 else 0.0, outline=1.0, glyph=g)
        tip = None
        if u < 2.7:   # 새기는 끌 — 이름 무늬의 끝을 따라간다
            if cells:
                lim = max(1, int(len(cells) * p))
                gx, gy = cells[min(lim, len(cells)) - 1]
                tipx = cx - wall.cell_a.shape[1] / 2 + gx + 11 * k
            else:
                tipx = cx + wall.cell * 0.29
            tipy = cy + wall.cell * 0.25 + 13 * k
            tip = (tipx, tipy)
            tapk = abs(math.sin(u * 19.0)) * (1 - smooth(span(u, 1.7, 1.8)))
            back = ease_in_out(span(u, 1.85, 2.6))
            th = 0.9
            tx = tipx + math.cos(th) * (8 * tapk) + back * 320
            ty = tipy + math.sin(th) * (8 * tapk) + back * 420
            bk.tool_hand(f, (tx, ty), th, 150, 66, "chisel", skin=old_skin, sleeve=old_sleeve)
        fly_pos = None
        if fly > 0:   # 나비가 그림에서 빠져나와 난다
            e = ease_in_out(fly)
            x = lerp(cx, cx + 330, e) + math.sin(fly * 9) * 18 * fly
            y = lerp(icon_y, -90, e ** 1.3) + math.sin(fly * 14) * 6
            sz = wall.cell * 0.25 * (1 + 0.3 * e)
            flap = 0.1 + 0.6 * (0.5 + 0.5 * math.sin(u * 16))
            draw_insect(f, "bfly", x, y, sz, rot=0.35 * e, flap=flap)
            fly_pos = (x, y, e)
        f = bk.sepia(f, 0.8)
        # 빛은 세피아 뒤에 금빛으로 얹는다 — 옛날 그림 속에서도 이름의 빛은 금빛이다
        if lit > 0:
            stamp(f, wall.cell_glow * lit * (0.9 - 0.5 * smooth(span(u, 2.1, 2.9))), cx, cy, PAL["glow"], "add")
            if u < 2.9:
                for j in range(5):
                    a = j * 1.26 + 0.4
                    rr = 120 + 90 * span(u, 1.75, 2.6)
                    sparkle(f, cx + math.cos(a) * rr, cy + math.sin(a) * rr * 0.8, 14 * (1 - span(u, 2.0, 2.9)) + 1,
                            amount=lit * (1 - span(u, 2.2, 2.9)))
        if tip is not None and 0.25 < u < 1.8:   # 돌가루
            ph = (u * 19.0 / math.pi) % 1.0
            for sx, sy, sr in chips[:12]:
                dot(f, tip[0] + sx * 14 + sx * 46 * ph, tip[1] - 24 * ph * (1 + abs(sy)) + sy * 8, 1.6 + sr,
                    c("#fff4dc"), 0.8 * (1 - ph))
        if fly_pos is not None:
            x, y, e = fly_pos
            if fly < 0.2:
                stamp(f, wall.cells[(1, 1)]["glow"] * (1 - fly / 0.2), cx, icon_y, PAL["glow"], "add")
            for j in range(9):   # 반짝이 꼬리
                q = max(0.0, fly - j * 0.03)
                ee = ease_in_out(q)
                dot(f, lerp(cx, cx + 330, ee) + math.sin(q * 9) * 18 * q, lerp(icon_y, -90, ee ** 1.3) + 34,
                    3.4 - j * 0.28, c(PAL["glow"]), 0.8 * (1 - j / 9))
        f = frame_fx.apply(f, u)
        return zoom(f, 1.0 + 0.04 * u / 3.8, W / 2, H * 0.45)

    return frame


# ══════════════════════════ 샷3 — 이름이 바래면 ══════════════════════════

def _cell_world(seed_meadow=631):
    """벽 세 칸(나비·무당벌레·잠자리) 아래로 풀밭이 이어진 그림 — 샷3의 바탕."""
    wall = bk.WallCanvas(W, H, rows=1, cols=3, cell=230, gap=50, y0=58, seed=401,
                         kinds_at={(0, 0): "bfly", (0, 1): "beetle", (0, 2): "dfly"})
    img = wall.draw(wall.img.copy(), lambda r, k: 1.0, skip={(0, 1)})
    meadow(img, 350, seed_meadow, flowers=26)
    lg = Mask()
    lg.rect(-10, 322, W + 10, 352)
    paint(img, lg.arr(0.6), "#d5b682", hi=0.2, lo=0.2)
    return wall, img


def fade_shot():
    wall, img = _cell_world()
    cx, cy = wall.center(0, 1)
    bx, by = 640, 462
    leaf = Mask()
    leaf.ellipse(bx + 6, by + 44, 170, 36, -0.08)
    leaf.taper([(bx + 168, by + 32), (bx + 236, by + 70)], 9, 4)
    la = leaf.arr(0.6)
    paint(img, la, "#5cc24a", hi=0.2, lo=0.12)
    rib = Mask()
    rib.line([(bx - 150, by + 54), (bx + 166, by + 30)], 2.8)
    over(img, c("#3f9a35"), rib.arr(0.5) * la)
    BS = 54
    bug = insect_parts("beetle", BS, rot=0.0)
    rng = np.random.default_rng(633)
    smoke = rng.uniform(-1, 1, (16, 3))
    y_top, y_bot = cy + wall.cell / 2 + 4, by - 52

    def frame(u):
        f = img.copy()
        lv = lerp(1.0, 0.04, smooth(span(u, 0.55, 1.6)))
        grey = 0.78 * smooth(span(u, 0.8, 1.9))
        icon_a = 1 - smooth(span(u, 1.6, 2.3))
        glyph_a = 1 - 0.78 * smooth(span(u, 1.2, 2.3))
        wall.draw_cell(f, 0, 1, lv, icon_alpha=icon_a, grey=grey, glyph_alpha=glyph_a)
        thr = (1 - smooth(span(u, 0.7, 1.55))) * (0.75 + 0.25 * math.sin(u * 47) * span(u, 0.6, 1.5) + 0.25)
        if thr > 0.01:   # 이름과 곤충을 잇는 금빛 실
            tm = local(80, y_bot - y_top + 10)
            ox, oy = tm.w / 2, tm.h / 2
            pts = [(ox + math.sin(yv * 0.06 + u * 3) * 6, yv - y_top + 4) for yv in np.linspace(y_top, y_bot, 30)]
            tm.line(pts, 4.6)
            ta = tm.arr(0.6)
            mid = (y_top + y_bot) / 2
            stamp(f, np.clip(blur_arr(ta, 8) * 1.6, 0, 1) * thr, 640, mid, PAL["glow"], "add")
            stamp(f, ta * thr, 640, mid, "#fff2b0")
            for j in range(3):
                q = (u * 0.9 + j / 3) % 1.0
                yv = lerp(y_top, y_bot, q)
                dot(f, 640 + math.sin(yv * 0.06 + u * 3) * 6, yv, 4, c("#fff6d0"), thr * 0.9)
        drain = 0.88 * smooth(span(u, 0.95, 1.9))
        ba = 1 - smooth(span(u, 1.9, 2.4))
        wig = math.sin(u * 7) * 0.06 * (1 - drain)
        if ba > 0.01:
            p = bug if abs(wig) < 0.01 else insect_parts("beetle", BS, rot=wig)
            draw_insect(f, "beetle", bx, by, BS, drain=drain, alpha=ba, parts=p)
        if 1.85 < u < 3.0:   # 퐁 — 연기처럼 흩어진다
            q = span(u, 1.85, 3.0)
            for sx, sy, sr in smoke:
                r_ = 7 + 8 * q + 3 * sr
                sp = sprite(r_)
                stamp(f, sp * 0.55 * (1 - q), bx + sx * (16 + 60 * q), by - 6 + sy * 14 - 70 * q * (0.6 + 0.4 * abs(sx)),
                      "#ece6dc")
        return zoom(f, 1.0 + 0.05 * ease_in_out(u / 3.6), 640, 330)

    return frame


# ══════════════════════════ 샷4 — 도감에 적으면 ══════════════════════════

BH = 1100


def relight_shot():
    wall = bk.WallCanvas(W, BH, rows=3, cols=5, cell=170, gap=26, y0=40, seed=401, kinds=ICON_KINDS)
    target = (1, 2)
    cx, cy = wall.center(*target)
    img = wall.draw(wall.img.copy(), lambda r, k: 0.85, skip={target})
    lg = Mask(W, BH)
    lg.rect(-10, 630, W + 10, 664)
    paint(img, lg.arr(0.6), "#d5b682", hi=0.2, lo=0.2)
    _floor(img, 664, W / 2, 420)
    bkx, bky, bw_, bh_ = 500, 850, 600, 310
    sh = Mask(W, BH)
    sh.rect(bkx - bw_ / 2 + 10, bky - bh_ / 2 + 22, bkx + bw_ / 2 + 22, bky + bh_ / 2 + 26, r=14)
    over(img, c("#5a3e22"), sh.arr(12) * 0.45)
    draw_book(img, bkx, bky, bw_, bh_, open_=1.0, cover="#2f7f5a", lines_seed=64, progress=0.5)
    dx, dy, s = 652, 838, 50
    bug = insect_parts("beetle", s, rot=0.0)
    union = np.maximum(bug["wing"], bug["body"])
    outline = np.clip(grow(union, 2.0) - union, 0, 1)   # 펜 선(윤곽만)
    hb = bug["box"] / 2
    yy, xx = np.mgrid[0:bug["box"], 0:bug["box"]].astype(np.float32)
    ang = (np.arctan2(yy - hb, xx - hb) + math.pi / 2) % (2 * math.pi)
    rx, ry = 0.72 * s, 0.8 * s
    curve = [(652, 790), (548, 680), (742, 470), (cx, cy + 40)]
    from silhouette_kit import bezier
    path = bezier(*curve, n=60)

    def frame(u):
        y0 = lerp(BH - H - 2, 60, ease_in_out(span(u, 1.55, 2.7)))
        f = bk.window(img, y0)
        # 무당벌레 그림 — 윤곽이 펜 끝을 따라 그려지고, 색이 차오르고, 금빛으로 빛난다
        th = 2 * math.pi * ease_in_out(span(u, 0.15, 1.45))
        rev = np.clip((th - ang) / 0.35 + 0.0, 0, 1) if th < 2 * math.pi - 0.01 else 1.0
        fill_a = smooth(span(u, 1.35, 1.75))
        stamp(f, outline * rev * (1 - fill_a), dx, dy - y0, INK)
        if fill_a > 0:
            draw_insect(f, "beetle", dx, dy - y0, s, alpha=fill_a, parts=bug)
        gl = smooth(span(u, 1.6, 1.9)) * (1 - 0.7 * smooth(span(u, 2.0, 2.8)))
        if gl > 0:
            stamp(f, blur_arr(union, 10) * gl, dx, dy - y0, PAL["glow"], "add")
        if u < 2.4:
            back = ease_in_out(span(u, 1.5, 2.2))
            tx = dx + rx * math.sin(th) + back * 220
            ty = dy - y0 - ry * math.cos(th) + back * 260
            bk.tool_hand(f, (tx, ty), 0.85, 112, 40, "pen")
        # 금빛 실이 벽으로 — 머리가 앞서고 꼬리가 남는다
        q = ease_in_out(span(u, 1.75, 2.5))
        fade = 1 - smooth(span(u, 2.55, 3.2))
        if q > 0 and fade > 0:
            n = max(2, int(len(path) * q))
            pts = path[:n]
            xs = [p[0] for p in pts]
            ys = [p[1] - y0 for p in pts]
            x0b, x1b, y0b, y1b = min(xs) - 30, max(xs) + 30, min(ys) - 30, max(ys) + 30
            tm = Mask(int(x1b - x0b) + 2, int(y1b - y0b) + 2)
            tm.line([(px - x0b, py - y0b) for px, py in zip(xs, ys)], 5.0)
            ta = tm.arr(0.6)
            mx, my = x0b + tm.w / 2, y0b + tm.h / 2
            stamp(f, np.clip(blur_arr(ta, 10) * 1.7, 0, 1) * fade, mx, my, PAL["glow"], "add")
            stamp(f, ta * fade, mx, my, "#fff2b0")
            hx, hy = xs[-1], ys[-1]
            if q < 1:
                sparkle(f, hx, hy, 24, amount=1.0, rot=u * 3)
            for j in range(6):
                i = max(0, n - 1 - j * 6)
                dot(f, xs[i] + math.sin(u * 9 + j) * 8, ys[i] + 6, 2.2, c("#fff6d0"), 0.8 * fade)
        # 빈칸이 다시 켜진다
        on = smooth(span(u, 2.45, 2.85))
        lv = lerp(0.04, 1.0, on) + 0.15 * math.exp(-((u - 2.75) / 0.18) ** 2)
        wall.draw_cell(f, *target, lv, icon_alpha=on, grey=0.78 * (1 - on), glyph_alpha=lerp(0.22, 1.0, on), dy=-y0)
        if on > 0:
            stamp(f, wall.cell_glow * on * (0.6 - 0.3 * span(u, 2.85, 3.5)), cx, cy - y0, PAL["glow"], "add")
            burst = span(u, 2.5, 3.3)
            for j in range(7):
                a = j * 0.9 + 0.3
                rr = 100 + 110 * ease_out(burst)
                sparkle(f, cx + math.cos(a) * rr, cy - y0 + math.sin(a) * rr * 0.7, 13 * (1 - burst) + 1,
                        amount=1 - burst, rot=a)
        return f

    return frame


# ══════════════════════════ 샷5 — 고대 잠자리가 돌아온다 ══════════════════════════

def return_shot():
    # kit.NameWall과 같은 배치(ch7 샷1·5와 같은 벽) — 가운데 칸만 고대 잠자리
    cell, gap = 118, 16
    gh = 3 * (cell + gap) - gap
    wall = bk.WallCanvas(W, H, rows=3, cols=7, cell=cell, gap=gap, y0=(H - gh) / 2 - 20, seed=401, kinds=ICON_KINDS,
                         kinds_at={(1, 3): "ancient"})
    mid = (1, 3)
    cx, cy = wall.center(*mid)
    dim = wall.draw(wall.img.copy(), lambda r, k: 0.78, skip={mid})
    lit = wall.draw(wall.img.copy(), lambda r, k: 1.18, skip={mid})
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    dist = np.sqrt((xx - cx) ** 2 + ((yy - cy) * 1.3) ** 2)
    rays = rays_texture(W, H, cx, cy, count=18, seed=651, spread=2 * math.pi, reach=0.75)
    halo = radial(W, H, cx, cy, 420, 1.5)
    rng = np.random.default_rng(652)
    burst = rng.uniform(0, 1, (22, 3))
    motes = Particles(40, 653, (0, 0, W, H), vel=((-6, 6), (-26, -10)), size=(0.8, 2.0))
    icon_y = cy - 8 * wall.k

    def frame(u):
        wave = 760 * ease_out(span(u, 0.9, 2.2))
        wgt = np.clip((wave - dist) / 160, 0, 1) * 0.9 + 0.1 * smooth(span(u, 0.2, 1.0))
        f = dim + (lit - dim) * wgt[..., None]
        g = smooth(span(u, 0.15, 1.1)) * (1 - 0.45 * smooth(span(u, 2.0, 3.0)))
        add(f, c(PAL["glow"]), halo * 0.45 * g)
        add(f, c("#fff3c0"), rays * (0.28 + 0.08 * math.sin(u * 5)) * g)
        emerge = span(u, 1.05, 2.3)
        lv = 0.78 + 0.6 * smooth(span(u, 0.2, 1.0))
        wall.draw_cell(f, *mid, lv, icon_alpha=1 - smooth(span(u, 1.05, 1.35)), outline=smooth(span(u, 1.05, 1.4)))
        stamp(f, wall.cell_glow * g * 0.9, cx, cy, PAL["glow"], "add")
        if emerge > 0:
            e = ease_out(emerge)
            s = lerp(wall.cell * 0.25, 112, e)
            x = cx + math.sin(u * 2.1) * 10 * e
            y = lerp(icon_y, 214, e) + math.sin(u * 3.3) * 6 * span(u, 2.0, 3.4)
            flap = 0.5 + 0.5 * math.sin(u * 30)
            draw_insect(f, "ancient", x, y, s, flap=flap, glow=0.9 * e)
        if 1.05 < u < 2.6:   # 반짝임이 터진다
            q = span(u, 1.05, 2.6)
            for a, r0, sz in burst:
                ang = a * 2 * math.pi
                rr = 60 + 520 * ease_out(q) * (0.5 + r0 * 0.5)
                px, py = cx + math.cos(ang) * rr, cy + math.sin(ang) * rr * 0.75
                if sz > 0.6:
                    sparkle(f, px, py, 10 + 8 * sz * (1 - q), amount=1 - q, rot=ang)
                else:
                    dot(f, px, py, 3 + 2 * sz, c("#fff6c8"), 0.9 * (1 - q))
        motes.draw(f, u, c("#fff3c4"), 0.5 * smooth(span(u, 1.0, 1.6)))
        shake = 2.5 * span(u, 0.5, 1.0) * (1 - span(u, 1.0, 1.4))
        return zoom(f, 1.0 + 0.05 * ease_in_out(span(u, 1.8, 3.4)), cx + shake * math.sin(u * 70), H * 0.4)

    return frame


SHOTS = [tall_wall_shot, carve_shot, fade_shot, relight_shot, return_shot]


def score(sc):
    """신전의 울림(방 + 물방울) · 끌 · 칸이 켜질 때 이름의 동기 · 바랠 때 내려가는 종 · 펜 · 상승 아르페지오 + 날갯짓."""
    T = sc.total
    s = [sc.shot(k) for k in range(5)]
    sc.room([(0, 0.8), (T, 0.8)], amp=0.03)
    for at in (0.6, 1.9, 7.2, 8.4, 10.0, 13.7):
        sc.drip(at, 0.07, f=1300 + 300 * math.sin(at))
    # 바탕 화음 — D장조 → (바랠 때) b단조 → 다시 D장조, 끝에 밝게
    sc.pad([hz("D3"), hz("A3"), hz("D4"), hz("F#4")],
           [(0, 0), (0.8, 0.9), (s[2] + 0.4, 0.8), (s[2] + 1.4, 0.0), (s[3] + 1.6, 0.0), (s[3] + 2.6, 0.7),
            (s[4], 0.8), (T, 0.6)], amp=0.028)
    sc.pad([hz("B2"), hz("F#3"), hz("D4")], [(0, 0), (s[2] + 0.4, 0), (s[2] + 1.5, 0.8), (s[3] + 1.4, 0.7),
                                             (s[3] + 2.4, 0.0), (T, 0)], amp=0.032, detune=0.004)
    sc.pad([hz("A4"), hz("D5"), hz("F#5")], [(0, 0), (s[4] + 0.6, 0), (s[4] + 1.4, 0.8), (T, 0.6)], amp=0.02)
    # 샷1 — 금빛이 칸을 훑어 오른다
    for k, nt in enumerate(("D5", "F#5", "A5", "D6")):
        sc.pluck(0.5 + k * 0.5, hz(nt), 0.055, dur=1.6, pan=-0.4 + k * 0.25, kind="bell")
    sc.boom(0.15, 70, 50, 2.0, 0.08)
    # 샷2 — 옛날: 끌로 새기면 칸이 켜지고 나비가 난다
    t = s[1] + 0.25
    while t < s[1] + 1.72:
        bk.tap(sc, t, 0.12, f=1500 + 200 * math.sin(t * 7), pan=0.15)
        t += 0.165
    sc.scratch(s[1] + 0.3, 1.4, 0.025)
    sc.pluck(s[1] + 1.76, hz("D4"), 0.08, dur=2.0, kind="bell")
    bk.name_motif(sc, s[1] + 1.8, amp=0.13)
    bk.wings(sc, s[1] + 2.25, 1.35, amp=0.03, rate=7, lo=200, hi=1600, pan=0.4)
    sc.whoosh(s[1] + 2.3, 0.9, 0.06, 600, 3000, pan_from=0.0, pan_to=0.7)
    # 샷3 — 이름이 바랜다: 내려가는 종, 퐁
    for k, nt in enumerate(("A5", "F#5", "D5", "B4", "F#4")):
        sc.pluck(s[2] + 0.6 + k * 0.26, hz(nt), 0.075, dur=1.2, pan=0.1, kind="bell")
    bk.puff(sc, s[2] + 1.9, 0.1)
    sc.boom(s[2] + 1.95, 90, 50, 0.9, 0.12)
    # 샷4 — 펜이 그리고, 금빛 실이 오르고, 칸이 다시 켜진다
    sc.scratch(s[3] + 0.15, 1.3, 0.035)
    for k, nt in enumerate(("D5", "E5", "F#5", "A5", "B5", "D6")):
        sc.pluck(s[3] + 1.75 + k * 0.12, hz(nt), 0.05, dur=0.8, pan=-0.3 + k * 0.08, kind="bell")
    sc.sparkle(s[3] + 2.45, 0.6, density=16, amp=0.035)
    bk.name_motif(sc, s[3] + 2.5, amp=0.13)
    # 샷5 — 벽이 크게 빛나고 고대 잠자리가 돌아온다
    sc.whoosh(s[4] + 0.2, 1.0, 0.05, 400, 2400, pan_from=-0.2, pan_to=0.2)
    sc.boom(s[4] + 1.05, 120, 60, 0.8, 0.12)
    sc.sparkle(s[4] + 1.05, 1.2, density=18, amp=0.04)
    for k, nt in enumerate(("D5", "F#5", "A5", "D6", "F#6", "A6", "D7")):
        sc.pluck(s[4] + 1.05 + k * 0.09, hz(nt), 0.075, dur=1.4, pan=-0.6 + k * 0.2, kind="box")
    bk.wings(sc, s[4] + 1.15, 2.1, amp=0.045, rate=23, lo=120, hi=900)
    for nt in ("D4", "F#4", "A4", "D5"):
        sc.pluck(s[4] + 2.05, hz(nt), 0.06, dur=2.4, kind="bell")
