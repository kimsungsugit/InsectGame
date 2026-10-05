"""ch12 「이름 없는 자리」 — 관장(하월)을 이긴 뒤(`duel_chief_win`) **대사 뒤**에 튼다. 차분한 회청·회갈.

하월이 삼십 년 동안 쓴 장부들. 그 속 곤충의 절반이 죽었다 — 지키려다 오히려 잃었다. 그가 길을 비켜서고,
안쪽 문 너머 보랏빛 어둠에서 그림자의 눈이 희미하게 뜬다. 무섭지 않게: 어둠은 작고 멀고, 눈은 옅다.

    샷1  0.0~3.0   빈 석판들이 둥글게 둘러선 방 — 위에서 내리는 회색 빛, 떠도는 먼지
    샷2  2.4~6.5   벽면 가득 장부 선반(책등 가득) — 옆으로 천천히
    샷3  5.9~10.0  선반 절반이 먼지에 덮여 빛바랬다 — 바랜 쪽으로 옮겨 간다
    샷4  9.4~14.0  하월의 뒷모습(지팡이)이 옆으로 비켜서고, 안쪽 문 너머 보랏빛 어둠에 노란 눈 둘
"""
import math

import numpy as np

from bk_v3 import puff, vgrad
from kit import (INK, PAL, H, W, Mask, Particles, add, bake_layer, blur_arr, c, dot, draw_eyes, draw_person,
                 ease_in_out, ease_out, fbm, grow, lerp, light_beam, over, paint, person_parts, put_layer, radial, rim,
                 scribble, slab, smooth, span, stamp, stone_wall, zoom)
from sound import hz

DURS = [3.0, 3.5, 3.5, 4.0]
TINT = "#f2f4f8"
CUES = [
    (1.0, 2.6, "삼십 년 동안 쓴 장부들."),
    (4.2, 2.6, "그 속 곤충의 절반이 죽었다."),
    (7.4, 2.6, "지키려다, 오히려 잃었다."),
    (10.6, 2.6, "이제 안쪽으로 가는 길이 열렸다."),
]

WALL = "#b7c2cc"
WALL_LINE = "#93a0ac"
FLOOR = "#b8aa9a"
FLOOR2 = "#978a7c"
SHELF = "#8d7b69"
SHELF2 = "#6f604f"
BACKING = "#5a524c"
SPINES = ("#6f84a3", "#8c6f5a", "#8a5a5e", "#5f7f6c", "#a88f68", "#5e6a8a", "#9a7a52", "#7a6a8c")
GOLD_BAND = "#d9b44a"
DUSTY = "#b9b3ab"


# ══════════════════════════ 샷 1 — 빈 석판의 방 ══════════════════════════

def slab_room():
    img = stone_wall(1201, fill=WALL, line=WALL_LINE, block=(170, 84))
    img *= vgrad(W, H, [(0, "#d9dde2"), (0.6, "#ffffff"), (1, "#ffffff")]) * 0.95
    # 둥근 바닥(타원) — 가운데에 빛이 고인다
    fm = Mask()
    fm.ellipse(640, 560, 820, 200)
    fm.rect(-20, 560, W + 20, H + 20)
    fa = fm.arr(0.8)
    over(img, c(INK), np.clip(grow(fa, 1.6) - fa, 0, 1) * 0.7)
    over(img, vgrad(W, H, [(0, FLOOR), (0.55, FLOOR), (1, FLOOR2)]), fa)
    rm = Mask()
    rm.d.ellipse([(640 - 420) * 2, (520 - 70) * 2, (640 + 420) * 2, (520 + 70) * 2], outline=255, width=6)
    rm.d.ellipse([(640 - 200) * 2, (528 - 34) * 2, (640 + 200) * 2, (528 + 34) * 2], outline=255, width=5)
    over(img, c("#8a7d70"), rm.arr(0.8) * 0.5)
    add(img, c("#fff8ea"), radial(W, H, 640, 520, 330, 1.8) * 0.3)
    light_beam(img, 640, -60, 640, 520, 70, 200, color="#f4f6ff", amount=0.3)
    # 석판 — 타원 둘레(뒤 → 앞). 앞쪽 가운데는 비워 방 안이 보이게
    slabs = []
    for deg in (-172, -150, -126, -102, -78, -54, -30, -8, 12, 168):
        a = math.radians(deg)
        x = 640 + math.cos(a) * 520
        base = 492 + math.sin(a) * 100
        s = 1 + 0.38 * math.sin(a) + (0.25 if deg in (12, 168) else 0.0)
        slabs.append((base, x, s, deg))
    slabs.sort()
    for base, x, s, deg in slabs:
        w, h = 96 * s, 214 * s
        sh = Mask()
        sh.ellipse(x + 8 * s, base + 4, w * 0.7, 10 * s)
        over(img, c("#5a5048"), sh.arr(4) * 0.35)
        slab(img, x, base, w, h, fill="#c9c5be", seed=1210 + deg)
        add(img, c("#ffffff"), _top_light(x, base, w, h) * 0.12)
    return img


def _top_light(x, base, w, h):
    m = Mask()
    m.ellipse(x, base - h + w * 0.35, w * 0.4, w * 0.2)
    return m.arr(6)


def room_shot():
    img = slab_room()
    motes = Particles(46, 1202, (480, 0, 800, 560), vel=((-5, 5), (-4, 6)), size=(0.6, 1.6), wobble=10)
    far = Particles(30, 1203, (0, 0, W, H), vel=((-3, 3), (-3, 3)), size=(0.5, 1.2))

    def frame(u):
        f = img.copy()
        motes.draw(f, u, c("#fffdf2"), 0.55)
        far.draw(f, u, c("#ffffff"), 0.2)
        return zoom(f, 1.0 + 0.08 * ease_in_out(u / 3.0), 640, 400)

    return frame


# ══════════════════════════ 선반·책등 ══════════════════════════

def shelf_canvas(w, seed, rows, top, gap, book_h, book_w, faded_from=None, fade_w=180):
    """장부 선반(정적) — 칸마다 책등 가득. faded_from(x)부터 오른쪽은 먼지에 덮여 빛바랜다. (그림, 칸 바닥 y 목록)."""
    g = np.random.default_rng(seed)
    img = np.ones((H, w, 3), np.float32) * c(BACKING)
    n = fbm(w, H, seed, (120, 40), (0.7, 0.3))
    img *= (0.85 + 0.25 * n)[..., None]
    floors = [top + gap * (r + 1) for r in range(rows)]
    masks = {k: Mask(w, H) for k in range(len(SPINES))}
    bands, labels, lean = Mask(w, H), Mask(w, H), []
    for fy in floors:
        x = 10.0
        while x < w - 10:
            bw = g.uniform(*book_w)
            bh = g.uniform(*book_h)
            k = int(g.integers(0, len(SPINES)))
            if g.random() < 0.06 and x + bh < w:   # 가끔 눕혀 쌓은 장부
                for j in range(int(g.integers(2, 4))):
                    th = bw * 0.8
                    masks[(k + j) % len(SPINES)].rect(x, fy - th * (j + 1), x + bh, fy - th * j, r=2)
                x += bh + 6
                continue
            masks[k].rect(x, fy - bh, x + bw, fy, r=2)
            for yy in (fy - bh + bh * 0.12, fy - bh * 0.14):
                bands.rect(x + 2, yy - 1.6, x + bw - 2, yy + 1.6)
            if bw > 22 and g.random() < 0.35:
                labels.rect(x + bw * 0.2, fy - bh * 0.62, x + bw * 0.8, fy - bh * 0.45, r=1)
            x += bw + g.uniform(0, 2.5)
    union = np.zeros((H, w), np.float32)
    for k, m in masks.items():
        a = m.arr(0.5)
        union = np.maximum(union, a)
    over(img, c(INK), grow(union, 1.4) * 0.9)
    for k, m in masks.items():
        a = m.arr(0.5)
        cc = c(SPINES[k]) * (0.92 + 0.16 * g.random())
        over(img, cc, a)
    add(img, c("#ffffff"), rim(union, 2, 0, 2) * 0.16)
    over(img, c("#2a2018"), rim(union, -3, 0, 3) * 0.18)
    over(img, c(GOLD_BAND), bands.arr(0.5) * union * 0.85)
    la = labels.arr(0.5) * union
    over(img, c("#efe6d2"), la)
    sm = Mask(w, H)
    for fy in floors:   # 이름표의 판독 불가 줄(아주 작게)
        scribble(sm, 0, fy - book_h[1] * 0.55, w, 1, seed + int(fy), row_h=6, weight=1.0)
    over(img, c("#7a6a58"), sm.arr(0.4) * la * 0.8)
    # 선반 판·칸막이
    sh = Mask(w, H)
    for fy in floors:
        sh.rect(-10, fy, w + 10, fy + 16)
    sh.rect(-10, top - 14, w + 10, top + 4)
    for x in np.arange(-6, w + 10, 310):
        sh.rect(x, top - 14, x + 18, floors[-1] + 16)
    sa = sh.arr(0.5)
    paint(img, sa, SHELF, line=1.6, hi=0.16, lo=0.12)
    if faded_from is not None:
        xs = np.arange(w, dtype=np.float32)
        fade = np.clip((xs - faded_from) / fade_w, 0, 1)[None, :] * np.ones((H, 1), np.float32)
        gray = img.mean(axis=2, keepdims=True)
        img = img * (1 - fade[..., None] * 0.7) + (gray * 0.55 + c(DUSTY) * 0.45) * fade[..., None] * 0.7
        # 먼지 이불 — 책 윗머리와 선반 판 위에 뽀얗게
        dm = Mask(w, H)
        for fy in floors + [top - 14]:   # 선반 판 위 먼지 — 납작하고 뭉실하게
            x = faded_from - fade_w * 0.5
            while x < w:
                ln = g.uniform(30, 90)
                dm.ellipse(x + ln / 2, fy + 1, ln / 2, g.uniform(3, 6))
                x += ln * g.uniform(0.6, 1.1)
        top_dust = np.clip(rim(union, 0, 5, 2) * 1.6, 0, 1)
        da = np.maximum(dm.arr(3.0), top_dust) * fade
        over(img, c("#e4e0da"), da * 0.85)
        nn = fbm(w, H, seed + 7, (40, 12), (0.6, 0.4))
        over(img, c("#d8d4ce"), np.clip((nn - 0.45) * 2.0, 0, 1) * fade * 0.28)
        # 거미줄(모서리에 작게 — 오래 손대지 않았다)
        wm = Mask(w, H)
        for cx, cy in ((w - 330, top + 4), (w - 20, floors[1] + 16)):
            for k in range(5):
                a = math.pi * (0.5 + 0.5 * k / 4)
                wm.line([(cx, cy), (cx + math.cos(a) * 70, cy + math.sin(a) * 70)], 1.2)
            for r in (22, 40, 58):
                pts = [(cx + math.cos(a) * r, cy + math.sin(a) * r) for a in np.linspace(math.pi * 0.5, math.pi, 8)]
                wm.line(pts, 1.0, caps=False)
        over(img, c("#f2f0ec"), wm.arr(0.4) * 0.6)
    return img, floors


def shelves_shot():
    CW = W + 360
    img, floors = shelf_canvas(CW, 1220, 5, 74, 104, (66, 92), (16, 30))
    beam = Mask(CW, H)
    beam.poly([(CW * 0.15, -20), (CW * 0.32, -20), (CW * 0.62, H + 20), (CW * 0.38, H + 20)])
    beam_a = beam.arr(30)
    motes = Particles(40, 1221, (0, 0, W, H), vel=((-4, 4), (-3, 5)), size=(0.6, 1.5), wobble=8)

    def frame(u):
        k = ease_in_out(span(u, 0.0, 4.1))
        ox = lerp(0, 360, k)
        i = int(ox)
        fx = ox - i
        f = img[:, i:i + W] * (1 - fx) + img[:, i + 1:i + 1 + W] * fx if fx > 0 else img[:, i:i + W].copy()
        add(f, c("#fff6e4"), beam_a[:, i:i + W] * 0.16)
        motes.draw(f, u, c("#fffdf0"), 0.4)
        return zoom(f, 1.06 - 0.06 * k, 640, 330)

    return frame


def faded_shot():
    CW = W + 300
    img, floors = shelf_canvas(CW, 1230, 4, 40, 128, (84, 114), (22, 38), faded_from=760, fade_w=220)
    motes = Particles(60, 1231, (0, 0, W, H), vel=((-3, 3), (4, 12)), size=(0.6, 1.6), wobble=8, twinkle=0.3)

    def frame(u):
        k = ease_in_out(span(u, 0.0, 4.1))
        ox = lerp(0, 300, k)
        i = int(ox)
        fx = ox - i
        f = img[:, i:i + W] * (1 - fx) + img[:, i + 1:i + 1 + W] * fx if fx > 0 else img[:, i:i + W].copy()
        # 먼지가 한 줌 흘러내린다
        if u > 1.6:
            kk = span(u, 1.6, 3.8)
            puff(f, 980 - ox + 30 * kk, floors[0] + 10 + 90 * kk, 40 + 50 * kk, 0.55 * (1 - kk), "#e8e4de", seed=31)
        motes.draw(f, u, c("#f4f1ea"), 0.45)
        return zoom(f, 1.0 + 0.05 * k, 640, 330)

    return frame


# ══════════════════════════ 샷 4 — 비켜서는 하월, 안쪽의 어둠 ══════════════════════════

DOOR = (640.0, 182.0, 98.0, 566.0)   # 가운데 x, 아치 꼭대기 y, 반폭, 문턱 y
HAWOL_H = 300.0
FEET = 594.0


def door_room():
    dx, dtop, hw, dbot = DOOR
    img = stone_wall(1240, fill=WALL, line=WALL_LINE, block=(150, 76))
    img *= vgrad(W, H, [(0, "#cfd6dd"), (0.7, "#ffffff"), (1, "#ffffff")]) * 0.95
    # 양옆 선반(장부방이 이어진다)
    shelves, _ = shelf_canvas(W, 1241, 4, 60, 112, (70, 98), (16, 30))
    sm = Mask()
    sm.rect(-20, 40, 390, 520, r=6)
    sm.rect(890, 40, W + 20, 520, r=6)
    sa = sm.arr(0.6)
    over(img, c(INK), grow(sa, 1.6))
    img = img * (1 - sa[..., None]) + shelves * 0.9 * sa[..., None]
    # 바닥
    fm = Mask()
    fm.rect(-20, dbot - 6, W + 20, H + 20)
    fa = fm.arr(0.6)
    over(img, c(INK), np.clip(grow(fa, 1.6) - fa, 0, 1) * 0.7)
    over(img, vgrad(W, H, [(0, FLOOR), (0.8, FLOOR), (1, FLOOR2)]), fa)
    # 아치 문 — 돌 테두리 + 안쪽 보랏빛 어둠
    hole = Mask()
    hole.rect(dx - hw, dtop + hw, dx + hw, dbot)
    hole.ellipse(dx, dtop + hw, hw, hw)
    ha = hole.arr(0.6)
    frame_m = Mask()
    frame_m.rect(dx - hw - 26, dtop + hw, dx + hw + 26, dbot)
    frame_m.ellipse(dx, dtop + hw, hw + 26, hw + 26)
    fr_a = np.clip(frame_m.arr(0.6) - ha, 0, 1)
    paint(img, fr_a, "#a9b3bd", line=1.8, hi=0.16, lo=0.1)
    jm = Mask()   # 아치 돌 이음매
    for k in range(9):
        a = math.pi * (1 + k / 8)
        jm.line([(dx + math.cos(a) * hw, dtop + hw + math.sin(a) * hw),
                 (dx + math.cos(a) * (hw + 26), dtop + hw + math.sin(a) * (hw + 26))], 2.2)
    for y in np.arange(dtop + hw + 40, dbot, 64):
        for sg in (-1, 1):
            jm.line([(dx + sg * hw, y), (dx + sg * (hw + 26), y)], 2.2)
    over(img, c(INK), jm.arr(0.5) * 0.6)
    inner = vgrad(W, H, [(0, "#3e2c66"), (0.6, "#2c1e4a"), (1, "#3a2a5a")])
    over(img, c(INK), np.clip(grow(ha, 1.6) - ha, 0, 1))
    img = img * (1 - ha[..., None]) + inner * ha[..., None]
    add(img, c(PAL["shadow_glow"]), rim(ha, -6, -6, 10) * 0.25 + rim(ha, 6, -6, 10) * 0.25)
    add(img, c(PAL["shadow_glow"]), blur_arr(fr_a, 10) * 0.12)
    # 문 앞 바닥에 번지는 보랏빛
    add(img, c("#9a86d8"), radial(W, H, dx, dbot + 10, 180, 2.0) * 0.18 * fa)
    return img, ha


_HAWOL = {}


def hawol(f, x, u, face, walk):
    """하월을 칠한다 — 마스크는 (얼굴 각, 걸음 위상) 단계로 캐시."""
    key = (round(face, 1), None if walk is None else round((walk % (2 * math.pi)) / (2 * math.pi) * 12) % 12)
    if key not in _HAWOL:   # 외투 자락 일렁임(t)은 고정 — 단계가 적어야 캐시가 맞는다(한 프레임 0.5초 안)
        wk = None if walk is None else key[1] / 12 * 2 * math.pi
        _HAWOL[key] = person_parts("hawol", HAWOL_H, t=0.0, face=key[0], walk=wk)
    draw_person(f, "hawol", x, FEET, HAWOL_H, parts=_HAWOL[key])
    # 세운 외투 깃 — 모자 아래 흰 머리 타원이 '얼굴'로 읽히지 않게 뒤통수 아래를 깃으로 덮는다(kit 인물 좌표와 같은 식)
    h = HAWOL_H
    hr, hunch = h * 0.072, h * 0.06
    face = key[0]
    hx = x + face * h * 0.03 + hunch * 0.8
    hy = FEET - h + hr + hunch * 0.6
    m = Mask(120, 110)
    cx, cy = 60 + face * hr * 0.25, 40
    m.poly([(cx - hr * 1.2, cy + hr * 1.75), (cx - hr * 0.86, cy + hr * 0.7), (cx - hr * 0.3, cy + hr * 0.6),
            (cx + hr * 0.3, cy + hr * 0.6), (cx + hr * 0.86, cy + hr * 0.7), (cx + hr * 1.2, cy + hr * 1.75)])
    a = m.arr(0.5)
    paint(f, a, "#5d5e68", hx - 60 + 60, hy - 40 + 55, line=1.5, hi=0.12, lo=0.1)


def step_aside_shot():
    img, door_a = door_room()
    dx, dtop, hw, dbot = DOOR
    door_glow = radial(W, H, dx, (dtop + dbot) / 2, 160, 1.6) * door_a
    shadow_m = Mask(240, 40)
    shadow_m.ellipse(120, 20, 80, 12)
    shadow_a = shadow_m.arr(4)
    motes = Particles(30, 1242, (0, 0, W, H), vel=((-3, 3), (-3, 4)), size=(0.6, 1.4))
    swirl = Particles(16, 1243, (dx - hw, dtop + 40, dx + hw, dbot), vel=((-6, 6), (-10, -2)), size=(1.0, 2.4),
                      wobble=14, twinkle=0.8)
    X0, X1 = 640.0, 466.0
    WALK0, WALK1 = 1.0, 2.5

    def frame(u):
        f = img.copy()
        pulse = 0.5 + 0.5 * math.sin(u * 1.8)
        add(f, c("#7a62c4"), door_glow * (0.12 + 0.08 * pulse))
        swirl.draw(f, u, c(PAL["shadow_glow"]), 0.35)
        # 노란 눈 둘 — 비켜선 뒤에 희미하게 뜨고 한 번 깜빡인다
        eo = smooth(span(u, 2.7, 3.5)) * 0.6
        if eo > 0.01:
            blink = 1.0 if not (3.95 < u < 4.1) else 0.12
            draw_eyes(f, dx, dtop + hw + 150, 0.62, eo, blink=blink, spread=28)
        # 하월 — 지팡이를 짚고, 옆걸음으로 비켜선다(옆모습까지만)
        k = ease_in_out(span(u, WALK0, WALK1))
        x = lerp(X0, X1, k)
        walking = WALK0 < u < WALK1
        face = -0.7 * smooth(span(u, WALK0 - 0.2, WALK0 + 0.2)) if u < WALK1 else lerp(-0.7, 0.45, smooth(span(u, WALK1, WALK1 + 0.5)))
        stamp(f, shadow_a * 0.35, x + 10, FEET + 2, "#4a4038")
        hawol(f, x, u, face, (u - WALK0) * 7.0 if walking else None)
        motes.draw(f, u, c("#fffdf2"), 0.35)
        z = 1.0 + 0.1 * ease_in_out(span(u, 2.0, 4.6))
        return zoom(f, z, dx, 380)

    return frame


SHOTS = [room_shot, shelves_shot, faded_shot, step_aside_shot]


# ══════════════════════════ 소리 ══════════════════════════

def score(sc):
    """실내 정적·먼지 → 책장 넘김 → 무거운 패드 → (바랜 쪽에서) 단조로 바랜 이름의 동기 → 지팡이 톡·걸음 → 그림자 깜빡."""
    T = sc.total
    s = [sc.shot(k) for k in range(4)]
    sc.room([(0, 0.0), (0.4, 1.0), (T, 1.0)], amp=0.03)
    sc.wind([(0, 0.4), (T, 0.4)], amp=0.012, lo=2500, hi=7000, gust=0.3)            # 먼지 사각임
    sc.pad([hz("A2"), hz("E3"), hz("A3"), hz("C4")], [(0, 0), (1.2, 0.7), (s[2], 0.9), (s[3], 0.8), (s[3] + 2.6, 0.5),
                                                     (T, 0)], amp=0.03, detune=0.004, bright=0.12)
    sc.drip(1.8, 0.05, 900)
    # 장부 — 책장 넘기는 소리
    sc.page(s[1] + 0.7, 0.13)
    sc.page(s[1] + 2.3, 0.11)
    # 바랜 쪽 — 이름의 동기를 단조로(D5 F5 A5 Bb5 A5), 오르골로 아주 여리게
    sc.melody(s[2] + 0.9, ["D5", "F5", "A5", "Bb5", ("A5", 2)], step=0.48, amp=0.08, kind="box", pan=0.2)
    sc.noise(s[2] + 1.6, 1.4, 2000, 6000, 0.02, decay=1.5)                             # 먼지가 흘러내린다
    # 비켜선다 — 지팡이 톡, 발소리(돌바닥)
    tap = s[3] + 0.95
    sc.pluck(tap, 1180, 0.16, dur=0.25, kind="xylo")
    sc.noise(tap, 0.05, 1500, 6000, 0.1, decay=70)
    for j in range(4):
        at = s[3] + 1.1 + j * 0.36
        sc.step(at, 0.1, soft=False)
        if j % 2 == 1:
            sc.pluck(at + 0.05, 1100, 0.1, dur=0.2, kind="xylo")
    # 어둠 쪽 — 낮은 웅웅임이 커지고, 눈이 뜰 때·깜빡일 때 그림자 소리
    sc.pad([hz("D2"), hz("A2"), hz("Eb3")], [(0, 0), (s[3] + 1.6, 0), (s[3] + 3.0, 0.7), (T, 0.5)], amp=0.04,
           detune=0.008, bright=0.05)
    sc.blink(s[3] + 2.8, 0.12)
    sc.blink(s[3] + 3.95, 0.1)
    sc.whoosh(s[3] + 2.4, 1.2, 0.04, 200, 900, pan_from=-0.2, pan_to=0.2)
