"""ch10 「잿불 갱도」 — 대치(`ch10_confront`) **대사 뒤**에 튼다. 잿불 골짜기(주홍·숯).

라온이 뛰어들고 천장이 무너졌다. 먹이 라온을 끌어냈다. 상자는 하나밖에 못 꺼냈다. 먹은 장부를 덮었다.
무너지는 갱도도 아이용이다 — 둥근 돌, 뭉게 먼지, 겁주지 않는 긴장(흔들림은 짧게, 아무도 다치는 그림이 없다).

    샷1  0.0~3.0   잿불 갱도 — 버팀목이 줄지어 선 굴, 끝이 주황으로 달아올라 있다. 불똥이 떠오른다
    샷2  2.4~6.5   들보가 무너진다 — 가운데가 꺾여 내려앉고 둥근 돌이 쏟아진다. 흔들림·먼지·불똥
    샷3  5.9~10.0  돌무더기 속에서 뻗은 빨간 소매(라온)를 검은 소매(먹)가 잡아 끌어낸다
    샷4  9.4~12.5  재 위에 놓인 상자 하나
    샷5 11.9~15.0  먹의 손이 두꺼운 장부를 천천히 덮는다(탁)
"""
import math

import numpy as np

from bk_v3 import Arm, Sprite, offset, puff, shake, stone_sprite, vgrad
from kit import (INK, H, W, Mask, Particles, add, bake_layer, blur_arr, put_layer, book_sky, c, crate, dot, ease_in_out, ease_out, fbm,
                 glow_at, grow, lerp, over, paint, radial, rim, scribble, smooth, span, sparkle, stamp, zoom)
from sound import hz

DURS = [3.0, 3.5, 3.5, 2.5, 2.5]
TINT = "#fff1e0"
CUES = [
    (2.8, 2.6, "천장이 무너져 내렸다."),
    (6.2, 3.0, "먹이 라온을 끌어냈다."),
    (9.8, 2.4, "꺼낸 상자는 하나뿐이었다."),
    (12.2, 2.4, "먹은 장부를 덮었다."),
]

# 색 — 주홍·숯. 숯은 윤곽과 그늘에만 쓰고 면은 밝게 둔다(기준 작품과 같은 밝기)
ROCK = "#c4734a"
ROCK_DARK = "#9a5236"
ROCK_LIT = "#f0a466"
TIMBER = "#a8703e"
TIMBER2 = "#8a5a30"
EMBER = "#ffb04a"
EMBER2 = "#ff7a2e"
GLOW = "#ffd36b"
ASH = "#c2b3a6"
ASH2 = "#9c8c80"
STONE = "#a8907e"

VP = (640.0, 300.0)                      # 굴의 소실점
BACK = (566.0, 236.0, 714.0, 354.0)       # 굴 끝 구멍
NEAR = (-60.0, -70.0, W + 60.0, H + 80.0)


def _rect_at(t):
    """굴 끝(0)과 화면 바깥(1) 사이 깊이 t의 단면 사각형(원근 — 가까울수록 빨리 커진다)."""
    k = t ** 2.2
    return tuple(lerp(b, n, k) for b, n in zip(BACK, NEAR))


def _frame_masks(x0, y0, x1, y1, pw, posts, beams):
    """버팀목 한 짝 — 기둥 둘 + 들보 + 모서리 버팀대."""
    posts.rect(x0, y0, x0 + pw, y1)
    posts.rect(x1 - pw, y0, x1, y1)
    beams.rect(x0 - pw * 0.4, y0, x1 + pw * 0.4, y0 + pw * 1.1, r=pw * 0.2)
    br = (x1 - x0) * 0.16
    beams.line([(x0 + pw * 0.6, y0 + pw * 1.0 + br), (x0 + pw * 0.6 + br, y0 + pw * 1.0)], pw * 0.55)
    beams.line([(x1 - pw * 0.6, y0 + pw * 1.0 + br), (x1 - pw * 0.6 - br, y0 + pw * 1.0)], pw * 0.55)


_CORRIDOR = {}


def corridor(seed=1010):
    """잿불 갱도 정적 배경 — 원근 굴(천장·벽·바닥) + 버팀목 + 레일 + 달아오른 끝. (그림, 등불 자리)."""
    if seed in _CORRIDOR:
        return _CORRIDOR[seed]
    bx0, by0, bx1, by1 = BACK
    nx0, ny0, nx1, ny1 = NEAR
    img = np.zeros((H, W, 3), np.float32)
    n = fbm(W, H, seed, (160, 50, 16), (0.55, 0.3, 0.15))
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    d = np.sqrt(((xx - VP[0]) / 640) ** 2 + ((yy - VP[1]) / 420) ** 2)
    lit = np.clip(1 - d, 0, 1) ** 1.6          # 굴 끝에서 오는 불빛
    # 벽·천장·바닥 — 면마다 칠하고 윤곽은 모서리 선으로만
    walls = [
        ([(nx0, ny0), (bx0, by0), (bx0, by1), (nx0, ny1)], ROCK, 1.0),       # 왼벽
        ([(nx1, ny0), (bx1, by0), (bx1, by1), (nx1, ny1)], ROCK, 0.92),      # 오른벽
        ([(nx0, ny0), (nx1, ny0), (bx1, by0), (bx0, by0)], ROCK_DARK, 0.9),  # 천장
        ([(nx0, ny1), (nx1, ny1), (bx1, by1), (bx0, by1)], "#a8705a", 1.0),  # 바닥
    ]
    for pts, fill, k in walls:
        m = Mask()
        m.poly(pts)
        a = m.arr(0.6)
        base = c(fill) * k * (0.86 + 0.28 * n[..., None])
        shade = lerp(base * 0.78, c(ROCK_LIT) * k, lit[..., None] * 0.85)
        over(img, shade, a)
    # 바닥 결 — 재가 쌓인 밝은 줄
    fl = Mask()
    for k in range(9):
        t = (k + 0.5) / 9
        y = lerp(by1, ny1, t ** 2.2)
        hw = lerp((bx1 - bx0) / 2, (nx1 - nx0) / 2, t ** 2.2)
        fl.line([(VP[0] - hw * 0.95, y), (VP[0] + hw * 0.95, y)], 1.0 + 4 * t ** 2)
    add(img, c("#ffd9b0"), fl.arr(1.2) * 0.08)
    # 모서리 선(윤곽)
    em = Mask()
    for a, b in (((nx0, ny0), (bx0, by0)), ((nx1, ny0), (bx1, by0)), ((nx0, ny1), (bx0, by1)),
                 ((nx1, ny1), (bx1, by1))):
        em.line([a, b], 3.0)
    over(img, c(INK), em.arr(0.8) * 0.75)
    # 벽의 둥근 돌 윤곽(가까운 벽일수록 크다)
    g = np.random.default_rng(seed + 3)
    sm = Mask()
    for _ in range(28):
        t = g.uniform(0.25, 1.0)
        side = g.choice([-1, 1])
        r0 = _rect_at(t)
        x = r0[0] + g.uniform(0, 0.06) * (r0[2] - r0[0]) if side < 0 else r0[2] - g.uniform(0, 0.06) * (r0[2] - r0[0])
        y = g.uniform(r0[1] + 30, r0[3] - 30)
        r = 10 + 46 * t ** 2
        sm.d.ellipse([(x - r) * 2, (y - r * 0.7) * 2, (x + r) * 2, (y + r * 0.7) * 2], outline=255,
                     width=max(2, int(2 + 3 * t)))
    over(img, c(INK), sm.arr(0.6) * 0.22)
    # 달아오른 금 — 벽의 갈라진 틈에서 주황빛
    cm = Mask()
    for k in range(10):
        t = g.uniform(0.3, 0.95)
        r0 = _rect_at(t)
        side = -1 if k % 2 == 0 else 1
        x = (r0[0] + 30 * t) if side < 0 else (r0[2] - 30 * t)
        y = g.uniform(r0[1] + 60, r0[3] - 80)
        pts = [(x, y)]
        for j in range(4):
            x += side * g.uniform(6, 26) * t
            y += g.uniform(14, 40) * t
            pts.append((x, y))
        cm.line(pts, 2 + 3 * t)
    ca = cm.arr(0.5)
    add(img, c(EMBER2), blur_arr(ca, 8) * 0.8)
    over(img, c("#ffe08a"), ca)
    # 굴 끝 — 달아오른 구멍
    hole = Mask()
    hole.rect(bx0, by0, bx1, by1)
    ha = hole.arr(0.5)
    over(img, vgrad(W, H, [(0, "#ffb35a"), (0.42, "#ffe39a"), (0.5, "#ffd070"), (1, "#ff9a4a")]), ha)
    add(img, c(GLOW), radial(W, H, VP[0], VP[1] + 20, 260, 1.8) * 0.55)
    # 레일
    rail = Mask()
    for sg in (-1, 1):
        rail.poly([(VP[0] + sg * 18, by1), (VP[0] + sg * 24, by1), (VP[0] + sg * 230, H + 40), (VP[0] + sg * 196, H + 40)])
    sl = Mask()
    for k in range(10):
        t = (k + 0.3) / 10
        y = lerp(by1 + 2, ny1, t ** 2.2)
        hw = lerp(32, 300, t ** 2.2)
        th = 2 + 14 * t ** 2.2
        sl.rect(VP[0] - hw, y - th / 2, VP[0] + hw, y + th / 2, r=th * 0.3)
    paint(img, sl.arr(0.6), TIMBER2, line=1.2, hi=0.08, lo=0.0)
    paint(img, rail.arr(0.6), "#8c8790", line=1.2, hi=0.25, lo=0.0)
    # 버팀목 — 먼 것부터
    lamps = []
    for k, t in enumerate((0.18, 0.36, 0.56, 0.8)):
        x0, y0, x1, y1 = _rect_at(t)
        x0 += (x1 - x0) * 0.02
        x1 -= (x1 - x0) * 0.02
        pw = 6 + 44 * t ** 2.2
        pm, bm = Mask(), Mask()
        _frame_masks(x0, y0 + (y1 - y0) * 0.02, x1, y1, pw, pm, bm)
        shade = 0.82 + 0.18 * (1 - t)
        paint(img, pm.arr(0.6), c(TIMBER) * shade, line=1.2 + 1.2 * t, hi=0.12, lo=0.12)
        paint(img, bm.arr(0.6), c(TIMBER2) * shade, line=1.2 + 1.2 * t, hi=0.12, lo=0.12)
        if k in (1, 2):
            lamps.append((VP[0] + (x1 - x0) * (0.22 if k == 1 else -0.26), y0 + pw * 1.1, 6 + 22 * t ** 1.5))
    # 등불 — 들보에 매단 작은 등
    for lx, ly, r in lamps:
        lm = Mask()
        lm.line([(lx, ly), (lx, ly + r * 1.2)], max(1.0, r * 0.08))
        over(img, c(INK), lm.arr(0.5))
        m = Mask()
        m.rect(lx - r * 0.55, ly + r * 1.2, lx + r * 0.55, ly + r * 2.4, r=r * 0.25)
        paint(img, m.arr(0.5), "#ffe27a", line=1.3, hi=0.3, lo=0.0)
        add(img, c(GLOW), radial(W, H, lx, ly + r * 1.8, r * 9, 2.0) * 0.45)
    _CORRIDOR[seed] = (img, lamps)
    return _CORRIDOR[seed]


def embers(seed, n=46, box=(0, 0, W, H), up=(-40, -14)):
    return Particles(n, seed, box, vel=((-10, 10), up), size=(0.7, 2.0), wobble=10, twinkle=0.8)


def draw_embers(f, p, u, amount=1.0):
    p.draw(f, u, c(EMBER2), 0.55 * amount)
    p.draw(f, u + 0.37, c("#ffe08a"), 0.35 * amount)


# ══════════════════════════ 샷 1 — 잿불 갱도 ══════════════════════════

def tunnel_shot():
    img, lamps = corridor()
    sparks = embers(1011, 60, (0, 120, W, H + 40))
    big = Particles(8, 1012, (0, 0, W, H), vel=((-14, 14), (-26, -12)), size=(3.5, 6.0), wobble=18, twinkle=0.6)
    hole = radial(W, H, VP[0], VP[1], 230, 1.6)

    def frame(u):
        f = img.copy()
        fl = 0.85 + 0.15 * math.sin(u * 7.3) * math.sin(u * 3.1 + 1)     # 불빛 일렁임
        add(f, c("#ff9a4a"), hole * 0.18 * fl)
        for k, (lx, ly, r) in enumerate(lamps):
            dot(f, lx, ly + r * 1.8, r * 1.4, c(GLOW), 0.25 + 0.1 * math.sin(u * 9 + k))
        draw_embers(f, sparks, u)
        big.draw(f, u, c(EMBER), 0.22)
        return zoom(f, 1.0 + 0.1 * ease_in_out(u / 3.0), VP[0], VP[1] + 20)

    return frame


# ══════════════════════════ 샷 2 — 들보가 무너진다 ══════════════════════════

PX0, PX1, BEAM_Y, PW = 476.0, 804.0, 196.0, 40.0   # 앞 버팀목(세로 화면 안)
CRACK_AT, FALL_AT = 0.85, 1.05


def _beam_half(length, th, side):
    """들보 반쪽 Sprite — 회전축(기둥 위)이 상자 가운데에 오도록 굽는다."""
    box = int(length * 2 + 40)

    def draw(dst, cx, cy):
        m = Mask(box, box)
        if side < 0:
            x0, x1 = cx - 10, cx + length
        else:
            x0, x1 = cx - length, cx + 10
        m.rect(x0, cy - th / 2, x1, cy + th / 2, r=th * 0.2)
        a = m.arr(0.5)
        paint(dst, a, TIMBER2, line=1.8, hi=0.14, lo=0.14)
        gm = Mask(box, box)
        for k in range(3):   # 나뭇결
            y = cy - th * 0.28 + k * th * 0.28
            gm.line([(x0 + 14, y), (x1 - 14, y + 2)], 1.6)
        stamp(dst, gm.arr(0.4) * a * 0.35, cx, cy, INK)
        # 부러진 끝 — 들쭉날쭉
        tip = x1 if side < 0 else x0
        jm = Mask(box, box)
        jm.poly([(tip, cy - th / 2), (tip + side * -1 * 0 + (8 if side < 0 else -8), cy - th * 0.1),
                 (tip, cy + th * 0.15), (tip + (10 if side < 0 else -10), cy + th / 2), (tip, cy + th / 2)])
        stamp(dst, jm.arr(0.5) * 0.6, cx, cy, "#d9a066")

    return Sprite(box, box, draw)


def collapse_bg():
    """샷2 정적 배경 — 갱도를 가까이(확대) + 앞 버팀목의 기둥 둘."""
    base, _ = corridor()
    img = zoom(base, 1.35, VP[0], VP[1] + 40).copy()
    pm = Mask()
    pm.rect(PX0 - PW / 2, BEAM_Y - 10, PX0 + PW / 2, H + 20)
    pm.rect(PX1 - PW / 2, BEAM_Y - 10, PX1 + PW / 2, H + 20)
    paint(img, pm.arr(0.6), TIMBER, line=2.0, hi=0.14, lo=0.16)
    # 천장 바위 띠(들보 위)
    cm = Mask()
    pts = [(-20, -20), (W + 20, -20), (W + 20, BEAM_Y - 70)]
    for x in np.linspace(W + 20, -20, 30):
        pts.append((x, BEAM_Y - 26 - 18 * math.sin(x * 0.021) - 10 * math.sin(x * 0.067)))
    cm.poly(pts)
    ca = cm.arr(0.6)
    n = fbm(W, H, 1021, (90, 30), (0.7, 0.3))
    over(img, c(INK), grow(ca, 1.8))
    over(img, c(ROCK_DARK) * (0.85 + 0.3 * n[..., None]), ca)
    add(img, c("#ffffff"), rim(ca, 0, -3, 3) * 0.1)
    return img, ca


def collapse_shot():
    img, ceil_a = collapse_bg()
    L = (PX1 - PX0) / 2 + 6
    left, right = _beam_half(L, 46, -1), _beam_half(L, 46, 1)
    g = np.random.default_rng(1022)
    # 떨어지는 돌 — (시작 시각, x, 시작 y, 착지 y, 반지름, 회전 속도)
    rocks = []
    for k in range(14):
        r = g.uniform(26, 48) if k < 9 else g.uniform(14, 22)
        x = 640 + g.uniform(-90, 90)
        rocks.append(dict(t0=FALL_AT + 0.06 + k * 0.06 + g.uniform(0, 0.05), x=x, y0=BEAM_Y - 40 + g.uniform(-16, 8),
                          y1=548 - r * 0.5 - max(0.0, 70 - abs(x - 640) * 0.6) * (k >= 4), r=r,
                          spin=g.uniform(-4, 4), bounce=g.uniform(0.1, 0.3), drift=(x - 640) * g.uniform(0.4, 1.0),
                          spr=stone_sprite(r, 1030 + k, fill=STONE if k % 3 else "#b89a84")))
    sparks = embers(1023, 50, (0, 80, W, H + 40))
    burst = g.uniform(-1, 1, (36, 3))
    trickle = g.uniform(0, 1, (24, 3))
    from bk_v3 import stone_pts
    hole_m = Mask()
    hole_m.poly(stone_pts(640, BEAM_Y - 34, 96, 1029, squash=0.42))
    hole_a = hole_m.arr(0.6)
    hole_ink = grow(hole_a, 1.8)
    hole_rim = rim(hole_a, 0, -4, 6)
    crack = Mask(80, 70).line([(40, 4), (32, 22), (46, 40), (36, 66)], 3.2).arr(0.5)

    def frame(u):
        f = img.copy()
        fall = ease_in_out(span(u, FALL_AT, FALL_AT + 0.55))
        bounce = math.sin(span(u, FALL_AT + 0.55, FALL_AT + 0.95) * math.pi) * 0.06
        ang = 0.62 * fall - bounce
        # 천장 구멍이 열린다(바위 틈 — 어둡지만 불빛이 비친다)
        if u > FALL_AT:
            k = smooth(span(u, FALL_AT, FALL_AT + 0.3))
            over(f, c(INK), hole_ink * k)
            over(f, c("#6a3524"), hole_a * k)
            add(f, c(EMBER2), hole_rim * 0.6 * k)
        # 금이 가기 전 — 부스스 떨어지는 먼지
        for tx, ty, ts in trickle:
            st = 0.1 + tx * 1.0
            if st < u < st + 0.7:
                k = (u - st) / 0.7
                dot(f, 600 + ty * 80, BEAM_Y + 10 + k * k * 300, 1.6 + ts, c("#f2dcc4"), 0.7 * (1 - k))
        # 들보 — 가운데에서 꺾여 내려앉는다
        wob = math.sin(u * 40) * 0.012 * span(u, 0.2, CRACK_AT) * (1 - fall)
        left.put(f, PX0, BEAM_Y + 6, rot=-(ang + wob))
        right.put(f, PX1, BEAM_Y + 6, rot=ang + wob)
        if CRACK_AT < u < FALL_AT + 0.1:
            stamp(f, crack * span(u, CRACK_AT, CRACK_AT + 0.1), 640, BEAM_Y + 6, "#1a1210")
        # 둥근 돌이 쏟아진다
        for rk in rocks:
            t = u - rk["t0"]
            if t < 0:
                continue
            fall_t = math.sqrt(max(1.0, rk["y1"] - rk["y0"]) * 2 / 2200)
            if t < fall_t:
                y = rk["y0"] + 0.5 * 2200 * t * t
                x = rk["x"]
                rot = rk["spin"] * t
            else:
                tb = t - fall_t
                hop = max(0.0, math.sin(min(1.0, tb / 0.35) * math.pi)) * rk["r"] * rk["bounce"] * 2.2
                x = rk["x"] + rk["drift"] * ease_out(min(1.0, tb / 0.6))
                y = rk["y1"] - hop
                rot = rk["spin"] * (fall_t + 0.4 * ease_out(min(1.0, tb / 0.6)))
            grow_k = 0.75 + 0.35 * min(1.0, t / fall_t)   # 떨어지며 카메라 쪽으로 다가온다
            rk["spr"].put(f, x, y, rot=rot, scale=grow_k)
        # 불똥 — 무너질 때 확 퍼진다
        draw_embers(f, sparks, u, 1.0 + 1.2 * span(u, FALL_AT, FALL_AT + 0.2) * (1 - span(u, FALL_AT + 0.4, 2.6)))
        if FALL_AT < u < FALL_AT + 1.4:
            k = (u - FALL_AT) / 1.4
            for bx, by, bs in burst:
                x = 640 + bx * 60 + bx * 260 * ease_out(k)
                y = BEAM_Y + by * 30 - 120 * ease_out(k) * (0.4 + abs(by)) + 160 * k * k
                dot(f, x, y, 2.2 + bs, c(EMBER), 0.9 * (1 - k))
        # 먼지 구름 — 둥글게 부풀었다 가라앉는다
        for k, (px, py, r0, t0, sd) in enumerate(((640, 520, 90, FALL_AT + 0.35, 1), (520, 540, 70, FALL_AT + 0.45, 2),
                                                  (760, 536, 80, FALL_AT + 0.5, 3), (640, BEAM_Y - 10, 70, FALL_AT, 4),
                                                  (600, 460, 110, FALL_AT + 0.7, 5), (700, 470, 100, FALL_AT + 0.8, 6))):
            if u < t0:
                continue
            k2 = (u - t0) / 2.2
            if k2 > 1:
                continue
            puff(f, px + (k - 2.5) * 14 * k2, py - 40 * k2, r0 * (0.5 + 0.9 * ease_out(k2)),
                 0.85 * (1 - k2) ** 1.3, "#ecdcc8", seed=sd, ink=0.5)
        dx, dy = shake(u, FALL_AT, 1.4, 11.0)
        dx2, dy2 = shake(u, 0.2, CRACK_AT, 2.0, 47.0)
        f = zoom(f, 1.05, 640 + dx + dx2, 380 + dy + dy2)
        return f

    return frame


# ══════════════════════════ 샷 3 — 끌어낸다 ══════════════════════════

PILE_C = (640.0, 560.0)


def rubble_layers(seed=1040):
    """돌무더기 — (뒤 돌, 앞 돌) 두 층(kit.bake_layer). 라온의 팔은 그 사이로 뻗는다."""
    g = np.random.default_rng(seed)

    def pile_y(x):
        return PILE_C[1] - 150 * math.exp(-((x - PILE_C[0]) / 260) ** 2) - 20

    stones = []
    for k in range(70):
        x = PILE_C[0] + g.normal(0, 230)
        y = g.uniform(pile_y(x), H + 40)
        r = g.uniform(22, 46) * (1.25 if y > 600 else 1.0)
        stones.append((y, x, r, k))
    stones.sort()
    sprites = [(x, y, stone_sprite(r, seed + k, fill=STONE if k % 4 else "#b49884", crack=k % 3 == 0),
                abs(x - 628) < 80 and y > 478) for y, x, r, k in stones]

    def draw(front):
        def fn(canvas):
            for x, y, sp, near_arm in sprites:   # 팔이 지나는 가운데 위쪽 돌은 앞 층(팔을 덮는다)
                if near_arm == front:
                    sp.put(canvas, x, y)
        return fn

    return bake_layer(draw(False)), bake_layer(draw(True))


def rescue_shot():
    # 배경 — 가까운 갱도 벽(불빛이 왼쪽 아래에서)
    img = vgrad(W, H, [(0, "#7a3e2a"), (0.5, "#b0603c"), (1, "#8c4a32")])
    n = fbm(W, H, 1041, (120, 40, 14), (0.55, 0.3, 0.15))
    img *= (0.84 + 0.32 * n)[..., None]
    add(img, c("#ff9a4a"), radial(W, H, 260, 600, 700, 1.6) * 0.35)
    add(img, c(GLOW), radial(W, H, 640, 300, 420, 2.0) * 0.18)
    from bk_v3 import stone_pts
    sm = Mask()
    g = np.random.default_rng(1042)
    for k in range(9):   # 뒤 벽의 큰 바위 윤곽(옅게)
        x, y, r = g.uniform(0, W), g.uniform(30, 330), g.uniform(60, 120)
        sm.d.polygon([(px * 2, py * 2) for px, py in stone_pts(x, y, r, 1045 + k)], outline=255, width=5)
    over(img, c(INK), sm.arr(1.0) * 0.18)
    # 쓰러진 들보 한 토막(뒤)
    bm = Mask()
    bm.poly([(170, 330), (520, 410), (510, 450), (160, 372)])
    paint(img, bm.arr(0.6), TIMBER2, line=2.0, hi=0.12, lo=0.14)
    back, front = rubble_layers()
    over(img, c("#3a2018"), blur_arr(back[1], 6) * 0.25)
    put_layer(img, *back)
    raon = Arm(40, -0.12, 9, skin="#f2c094", sleeve="#e8473f", curls=(0.0, 0.25, 0.55, 0.75), cuff="#ffffff")
    meok = Arm(44, -2.3, 17, skin="#f0c49c", sleeve="#2c2c36", curls=(0.0, 0.35, 0.7), widen=1.7)
    sparks = embers(1043, 40, (0, 100, W, H + 30))
    g = np.random.default_rng(1044)
    loose = [(g.uniform(-1, 1), stone_sprite(g.uniform(14, 22), 1050 + k)) for k in range(4)]
    loose_xy = [(596, 456), (664, 452), (578, 474), (684, 470)]

    def frame(u):
        f = img.copy()
        pull = ease_in_out(span(u, 1.75, 3.35))
        reach = ease_out(span(u, 0.35, 1.4))
        grip = smooth(span(u, 1.3, 1.6))
        # 라온의 손 — 처음엔 허우적, 잡히면 꼭 쥔다
        rx = 628 + 34 * pull
        ry = 392 - 150 * pull
        wig = 0.25 + 0.25 * math.sin(u * 6.0) if grip < 0.5 else 0.75
        raon.draw(f, rx + math.sin(u * 5) * 3 * (1 - grip), ry, curl=wig)
        # 앞 돌(팔을 덮는다)
        put_layer(f, *front)
        # 굴러 떨어지는 작은 돌
        for (sg, sp), (lx, ly) in zip(loose, loose_xy):
            k = span(u, 1.8, 2.8)
            side = -1 if lx < 628 else 1
            x = lx + side * 120 * ease_out(k)
            y = ly + 90 * k * k - 30 * math.sin(k * math.pi) * 0.6
            sp.put(f, x, y, rot=side * 4 * k)
        # 먹의 손 — 위에서 내려와 손목을 잡는다
        mx = lerp(1000, 654, reach) + 34 * pull    # 손바닥이 라온의 팔뚝(손목 아래)에 온다
        my = lerp(-60, 414, reach) - 150 * pull
        meok.draw(f, mx, my, curl=0.7 * grip)
        if 1.4 < u < 2.2:   # 꼭 잡는 순간 작은 빛
            k = span(u, 1.4, 2.2)
            sparkle(f, 668, 412, 16 + 10 * k, "#fff3c4", 0.6 * (1 - k))
        # 먼지 — 끌어낼 때
        if u > 1.8:
            k = span(u, 1.8, 3.6)
            puff(f, 600 - 40 * k, 470 - 30 * k, 50 + 50 * k, 0.6 * (1 - k), "#ecdcc8", seed=11, ink=0.4)
            puff(f, 680 + 40 * k, 466 - 26 * k, 40 + 50 * k, 0.55 * (1 - k), "#ecdcc8", seed=12, ink=0.4)
        draw_embers(f, sparks, u, 0.8)
        dx, dy = shake(u, 1.8, 0.5, 3.0, 23.0)
        return zoom(f, 1.04 + 0.04 * ease_in_out(u / 4.1), 640 + dx, 380 + dy)

    return frame


# ══════════════════════════ 샷 4 — 상자 하나 ══════════════════════════

def crate_shot():
    base, _ = corridor()
    img = blur_arr_rgb(zoom(base, 1.25, VP[0], VP[1] + 30), 5) * 0.72
    # 재 바닥 — 둥근 둔덕
    gm = Mask()
    pts = [(-20, H + 20)]
    for x in np.linspace(-20, W + 20, 60):
        pts.append((x, 430 + 22 * math.sin(x * 0.006 + 1) + 10 * math.sin(x * 0.021)))
    pts.append((W + 20, H + 20))
    gm.poly(pts)
    ga = gm.arr(0.8)
    n = fbm(W, H, 1061, (100, 30), (0.7, 0.3))
    over(img, c(INK), np.clip(ga - np.roll(ga, 3, axis=0), 0, 1) * 0.6)
    over(img, vgrad(W, H, [(0, ASH), (0.75, ASH2), (1, "#7d6e64")]) * (0.9 + 0.2 * n[..., None]), ga)
    # 재 위 흩어진 숯 조각과 꺼져 가는 불씨
    g = np.random.default_rng(1062)
    for k in range(26):
        x, y = g.uniform(0, W), g.uniform(470, 690)
        r = g.uniform(5, 13)
        m = Mask(40, 40)
        m.poly([(20 + math.cos(a) * r * g.uniform(0.7, 1.1), 20 + math.sin(a) * r * 0.6) for a in np.linspace(0, 6.2, 7)])
        stamp(img, m.arr(0.5), x, y, "#4a3830" if k % 3 else "#5a4038")
        if k % 4 == 0:
            dot(img, x, y - 2, 3, c(EMBER2), 0.6)
    add(img, c("#ffcf8a"), radial(W, H, 640, 420, 330, 1.7) * 0.32)
    # 상자 그림자 + 상자
    sh = Mask()
    sh.ellipse(650, 524, 150, 22)
    over(img, c("#3a2a24"), sh.arr(6) * 0.45)
    crate(img, 640, 528, 230, 150, seed=1063)
    soot = Mask()
    soot.ellipse(560, 500, 40, 30)
    soot.ellipse(720, 470, 30, 22)
    over(img, c("#3a2a24"), soot.arr(8) * 0.25)
    sparks = embers(1064, 30, (0, 200, W, H + 30), up=(-22, -8))
    flakes = Particles(36, 1065, (0, -20, W, H), vel=((-8, 8), (10, 26)), size=(0.8, 1.8), wobble=14, twinkle=0.3)
    glow = radial(W, H, 640, 430, 260, 2.0)

    def frame(u):
        f = img.copy()
        add(f, c(EMBER), glow * 0.08 * (0.6 + 0.4 * math.sin(u * 4.1)))
        flakes.draw(f, u, c("#e9e1d8"), 0.35)
        draw_embers(f, sparks, u, 0.6)
        return zoom(f, 1.0 + 0.06 * ease_in_out(u / 3.1), 640, 430)

    return frame


def blur_arr_rgb(f, r):
    return np.stack([blur_arr(np.clip(f[..., k], 0, 1), r) for k in range(3)], axis=2)


# ══════════════════════════ 샷 5 — 장부를 덮는다 ══════════════════════════

BK_X, BK_Y, BK_W, BK_H = 725.0, 318.0, 230.0, 300.0   # 책등 x, 가운데 y, 반쪽 폭, 높이
THICK = 16.0
CLOSE_AT = 1.95


def _page_sprite(seed, side):
    """장부 한 쪽(페이지 면) — 줄마다 판독 불가 이름 + 눈금 칸."""
    box_w, box_h = int(BK_W + 20), int(BK_H + 20)

    def draw(dst, cx, cy):
        m = Mask(box_w, box_h)
        m.rect(cx - BK_W / 2, cy - BK_H / 2, cx + BK_W / 2, cy + BK_H / 2, r=4)
        a = m.arr(0.5)
        paint(dst, a, "#fbf1dc", line=1.4, hi=0.0, lo=0.06)
        lm = Mask(box_w, box_h)
        rows = 9
        for k in range(rows + 1):
            y = cy - BK_H / 2 + 22 + k * (BK_H - 44) / rows
            lm.line([(cx - BK_W / 2 + 12, y), (cx + BK_W / 2 - 12, y)], 1.0)
        xc = cx + BK_W / 2 - 66
        lm.line([(xc, cy - BK_H / 2 + 16), (xc, cy + BK_H / 2 - 16)], 1.2)
        stamp(dst, lm.arr(0.4) * a * 0.5, cx, cy, "#c9a77a")
        sm = Mask(box_w, box_h)
        scribble(sm, cx - BK_W / 2 + 16, cy - BK_H / 2 + 34, BK_W - 96, rows, seed, row_h=(BK_H - 44) / rows,
                 weight=1.8)
        g = np.random.default_rng(seed + 1)
        for k in range(rows):   # 눈금(||||) — 숫자 칸
            y = cy - BK_H / 2 + 22 + (k + 0.5) * (BK_H - 44) / rows
            for j in range(int(g.integers(2, 6))):
                x = xc + 8 + j * 9
                sm.line([(x, y - 8), (x + 1, y + 8)], 1.8)
            if g.random() < 0.5:
                sm.line([(xc + 4, y + 6), (xc + 50, y - 6)], 1.6)
        stamp(dst, sm.arr(0.4) * a, cx, cy, "#5a4a3c")

    return Sprite(box_w, box_h, draw)


def _cover_sprite():
    box_w, box_h = int(BK_W + 28), int(BK_H + 28)

    def draw(dst, cx, cy):
        m = Mask(box_w, box_h)
        m.rect(cx - BK_W / 2 - 6, cy - BK_H / 2 - 6, cx + BK_W / 2 + 6, cy + BK_H / 2 + 6, r=8)
        a = m.arr(0.5)
        paint(dst, a, "#4a3a44", line=1.8, hi=0.16, lo=0.12)
        dm = Mask(box_w, box_h)
        dm.rect(cx - BK_W / 2 + 14, cy - BK_H / 2 + 14, cx + BK_W / 2 - 14, cy + BK_H / 2 - 14, r=6)
        dm2 = Mask(box_w, box_h)
        dm2.rect(cx - BK_W / 2 + 18, cy - BK_H / 2 + 18, cx + BK_W / 2 - 18, cy + BK_H / 2 - 18, r=5)
        stamp(dst, np.clip(dm.arr(0.4) - dm2.arr(0.4), 0, 1) * 0.6, cx, cy, "#c9a24a")
        pm = Mask(box_w, box_h)
        pm.rect(cx - 34, cy - 52, cx + 34, cy + 6, r=4)   # 빈 이름표 자리
        stamp(dst, pm.arr(0.4) * 0.8, cx, cy, "#c9a24a")
        pm2 = Mask(box_w, box_h)
        pm2.rect(cx - 30, cy - 48, cx + 30, cy + 2, r=3)
        stamp(dst, pm2.arr(0.4), cx, cy, "#3a2e36")

    return Sprite(box_w, box_h, draw)


def _leaf(sprite, f, a, hinge_x, cy):
    """책장(넘어가는 반쪽)을 각도 a(0=오른쪽에 펼침, π=왼쪽에 덮음)로 찍는다 — 폭을 cos로 눌러 원근을 흉내."""
    cw = math.cos(a)
    lift = math.sin(a)
    w = max(2, int(abs(cw) * sprite.w))
    sh = 1 + 0.07 * lift
    from PIL import Image
    rgb = np.stack([np.asarray(Image.fromarray(sprite.rgb[..., k], "F").resize((w, int(sprite.h * sh)),
                                                                                 Image.Resampling.BILINEAR))
                    for k in range(3)], axis=2)
    al = np.asarray(Image.fromarray(sprite.a, "F").resize((w, int(sprite.h * sh)), Image.Resampling.BILINEAR))
    shade = 1 - 0.28 * lift
    rgb = rgb * shade
    x = hinge_x + (w / 2 if cw >= 0 else -w / 2)
    from bk_v3 import _composite
    _composite(f, rgb.astype(np.float32), al.astype(np.float32), x, cy - lift * 26)


def ledger_shot():
    # 바탕 — 돌 받침 위(위에서 내려다본다), 왼쪽 위에서 불빛
    img = vgrad(W, H, [(0, "#8a5238"), (1, "#5e3828")])
    n = fbm(W, H, 1071, (140, 40, 12), (0.55, 0.3, 0.15))
    img *= (0.85 + 0.3 * n)[..., None]
    add(img, c("#ffb066"), radial(W, H, 380, 160, 760, 1.5) * 0.38)
    g = np.random.default_rng(1072)
    for k in range(40):   # 재 얼룩
        dot(img, g.uniform(0, W), g.uniform(0, H), g.uniform(3, 9), c("#d9c8b8"), 0.18)
    # 펼친 장부 — 왼쪽 반(정적): 표지 밑단 + 두께 + 왼쪽 페이지
    left_x = BK_X - BK_W / 2
    cover = _cover_sprite()
    page_l = _page_sprite(1073, -1)
    page_r = _page_sprite(1074, 1)
    sh = Mask()
    sh.rect(BK_X - BK_W - 10, BK_Y - BK_H / 2 + 6, BK_X + 20, BK_Y + BK_H / 2 + THICK + 24, r=14)
    over(img, c("#2a1810"), sh.arr(10) * 0.45)
    cm = Mask()
    cm.rect(BK_X - BK_W - 8, BK_Y - BK_H / 2 - 8, BK_X, BK_Y + BK_H / 2 + THICK + 8, r=8)
    paint(img, cm.arr(0.5), "#4a3a44", line=1.8, hi=0.1, lo=0.1)
    tm = Mask()   # 페이지 두께(아래로 겹친 종이 결)
    tm.rect(BK_X - BK_W, BK_Y + BK_H / 2 - 4, BK_X - 2, BK_Y + BK_H / 2 + THICK, r=3)
    paint(img, tm.arr(0.5), "#eadbbd", line=1.2, hi=0.0, lo=0.0)
    lm = Mask()
    for k in range(4):
        y = BK_Y + BK_H / 2 + 2 + k * THICK / 4
        lm.line([(BK_X - BK_W + 6, y), (BK_X - 8, y)], 1.0)
    over(img, c("#b9a07a"), lm.arr(0.4) * 0.8)
    page_l.put(img, left_x, BK_Y)
    # 오른쪽 반의 밑받침(넘어가면 드러나는 자리) — 받침돌에 남는 자국
    rib = Mask()
    rib.line([(BK_X + 4, BK_Y + BK_H / 2 - 10), (BK_X + 14, BK_Y + BK_H / 2 + 46), (BK_X + 4, BK_Y + BK_H / 2 + 70)], 8)
    rib_a = rib.arr(0.5)
    meok = Arm(40, -math.pi / 2 - 0.15, 18, skin="#f0c49c", sleeve="#2c2c36", curls=(0.0, 0.4, 0.8))
    sparks = embers(1075, 30, (0, 120, W, H + 30), up=(-24, -8))
    right_static = img.copy()   # 열려 있을 때의 오른쪽 반
    sh2 = Mask()
    sh2.rect(BK_X - 10, BK_Y - BK_H / 2 + 6, BK_X + BK_W + 24, BK_Y + BK_H / 2 + THICK + 24, r=14)
    over(right_static, c("#2a1810"), np.clip(sh2.arr(10) - sh.arr(10), 0, 1) * 0.45)
    cm2 = Mask()
    cm2.rect(BK_X, BK_Y - BK_H / 2 - 8, BK_X + BK_W + 8, BK_Y + BK_H / 2 + THICK + 8, r=8)
    paint(right_static, cm2.arr(0.5), "#4a3a44", line=1.8, hi=0.1, lo=0.1)
    tm2 = Mask()
    tm2.rect(BK_X + 2, BK_Y + BK_H / 2 - 4, BK_X + BK_W, BK_Y + BK_H / 2 + THICK, r=3)
    paint(right_static, tm2.arr(0.5), "#eadbbd", line=1.2, hi=0.0, lo=0.0)
    over(right_static, c("#b9a07a"), np.roll(lm.arr(0.4), int(BK_W + 4), axis=1) * 0.8)
    page_r.put(right_static, BK_X + BK_W / 2, BK_Y)
    for im in (img, right_static):
        paint(im, rib_a, "#c0392e", line=1.2, hi=0.2, lo=0.0)   # 붉은 갈피끈(먹의 목도리 색)

    def frame(u):
        a = math.pi * ease_in_out(span(u, 0.45, CLOSE_AT))
        if a <= 0.001:
            f = right_static.copy()
        else:
            f = img.copy()
            cw = math.cos(a)
            if cw > 0:
                _leaf(page_r, f, a, BK_X, BK_Y)
            else:
                _leaf(cover, f, a, BK_X, BK_Y - 4)
        # 손 — 넘어가는 끝을 잡고 따라가다, 덮은 표지 위에 얹는다
        edge = BK_X + BK_W * math.cos(a)
        lift = math.sin(a)
        settle = ease_in_out(span(u, CLOSE_AT, CLOSE_AT + 0.6))
        hx = lerp(edge + 64, BK_X - BK_W * 0.5 + 60, settle) if a > 2.0 else edge + 64
        hy = BK_Y + 70 - lift * 70 + 6 * settle
        press = smooth(span(u, CLOSE_AT - 0.05, CLOSE_AT + 0.15)) * (1 - span(u, CLOSE_AT + 0.15, CLOSE_AT + 0.5))
        meok.draw(f, hx, hy + press * 3, curl=0.4 if u < CLOSE_AT else 0.0)
        if u > CLOSE_AT:   # 탁 — 양옆으로 재가 훅
            k = span(u, CLOSE_AT, CLOSE_AT + 1.1)
            for sd, px in ((21, BK_X - BK_W - 10), (22, BK_X + 10)):
                puff(f, px + (px - BK_X + BK_W / 2) * 0.25 * k, BK_Y + BK_H / 2 + 10 - 30 * k, 40 + 50 * k,
                     0.6 * (1 - k), "#e6d6c6", seed=sd, ink=0.4)
        draw_embers(f, sparks, u, 0.7 + 0.8 * span(u, CLOSE_AT, CLOSE_AT + 0.1) * (1 - span(u, CLOSE_AT + 0.2, 2.6)))
        return zoom(f, 1.0 + 0.05 * ease_in_out(u / 3.1), 640, 340)

    return frame


SHOTS = [tunnel_shot, collapse_shot, rescue_shot, crate_shot, ledger_shot]


# ══════════════════════════ 소리 ══════════════════════════

def score(sc):
    """불 탁탁·낮은 우르릉 → 붕괴 쿵 + 돌 굴러가는 잡음 → 끌어낼 때 긴장 패드 → 상자(이름의 동기, 여리게) → 장부 탁."""
    T = sc.total
    s = [sc.shot(k) for k in range(5)]
    fall = s[1] + FALL_AT
    sc.fire([(0, 0.0), (0.3, 1.0), (s[1] + 0.6, 1.0), (fall, 0.6), (s[2] + 1.0, 0.7), (s[3] + 0.4, 0.45),
             (s[4], 0.4), (T, 0.25)], amp=0.045)
    sc.wind([(0, 0.3), (s[2], 0.5), (s[3] + 0.5, 0.25), (T, 0.15)], amp=0.03, lo=60, hi=260, gust=0.6)   # 우르릉
    sc.boom(1.4, 55, 38, 2.4, 0.14)
    # 무너지기 전 — 흔들림과 삐걱
    sc.noise(s[1] + 0.2, 0.7, 50, 220, 0.12, decay=2.5)
    sc.noise(s[1] + CRACK_AT, 0.25, 900, 4000, 0.16, decay=18)                         # 나무가 쩍
    for k in range(3):
        sc.pluck(s[1] + CRACK_AT - 0.05 + k * 0.06, 190 - k * 12, 0.05, dur=0.3, kind="xylo")
    # 쿵 — 크게, 그러나 짧게(무섭지 않게)
    sc.boom(fall, 120, 34, 1.8, 0.42)
    sc.noise(fall, 1.2, 60, 700, 0.12, decay=3.2)
    g = np.random.default_rng(1099)
    for k in range(16):                                                               # 돌 굴러가는 소리
        at = fall + 0.1 + k * 0.09 + g.uniform(0, 0.06)
        sc.noise(at, 0.08, 250, 2600, 0.08 * (1 - k / 20), decay=34, pan=g.uniform(-0.6, 0.6))
        if k % 3 == 0:
            sc.boom(at, 160, 80, 0.2, 0.08)
    sc.whoosh(fall + 0.3, 1.6, 0.06, 200, 1200, pan_from=-0.4, pan_to=0.4)            # 먼지가 퍼진다
    # 끌어낸다 — 긴장 패드(단조) → 꽉 잡으면 따뜻하게 풀린다
    sc.pad([hz("D3"), hz("F3"), hz("A3"), hz("C4")],
           [(0, 0), (s[2] - 0.3, 0), (s[2] + 0.8, 0.9), (s[2] + 1.7, 0.9), (s[2] + 2.6, 0.2), (T, 0)],
           amp=0.075, detune=0.006, bright=0.15)
    sc.pluck(s[2] + 1.45, hz("D4"), 0.12, dur=1.0, kind="kalimba")                    # 꽉
    for k in range(4):
        sc.noise(s[2] + 1.85 + k * 0.22, 0.1, 250, 2400, 0.07, decay=30, pan=(-0.4, 0.4)[k % 2])
    sc.pad([hz("F3"), hz("A3"), hz("C4"), hz("F4")],
           [(0, 0), (s[2] + 2.2, 0), (s[2] + 3.2, 0.8), (s[3] + 1.0, 0.7), (s[4] + 1.5, 0.5), (T, 0)],
           amp=0.05, detune=0.003)
    # 상자 하나 — 이름의 동기를 느리고 여리게(끝음을 매듭짓지 않는다)
    sc.melody(s[3] + 0.5, ["D5", "F#5", "A5", "B5", ("A5", 2)], step=0.4, amp=0.1, kind="kalimba", pan=0.1)
    # 장부를 덮는다 — 책장 바람 + 탁
    sc.page(s[4] + 0.6, 0.1)
    sc.whoosh(s[4] + 0.9, 1.0, 0.07, 400, 2400, pan_from=0.5, pan_to=-0.3)
    sc.thud(s[4] + CLOSE_AT, 0.42)
    sc.noise(s[4] + CLOSE_AT, 0.05, 1500, 6000, 0.14, decay=80)
    sc.pad([hz("D3"), hz("A3"), hz("D4")], [(0, 0), (s[4] + CLOSE_AT, 0), (s[4] + CLOSE_AT + 0.3, 0.7), (T, 0.4)],
           amp=0.05)
