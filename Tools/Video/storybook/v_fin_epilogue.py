"""fin 후일담 — `fin_epilogue` **대사 뒤**에 튼다. 새벽 금빛 초원 — 1편(ch1_prologue)과 수미상관.

1편은 곤충이 사라진 조용한 새벽에서 시작해 손등에 내려앉는 무당벌레로 끝났다. 후일담은 그 새벽으로 돌아오되,
이번엔 풀벌레 소리가 있고 곁에 곤충들이 난다. 마지막 컷은 1편 샷4와 같은 손·같은 자리·같은 무당벌레다(자막 없음).

    샷1  0.0~3.0   하월의 뒷모습 — 들판에서 손을 펴 하늘소 한 마리를 놓아준다(발치엔 빈 상자)
    샷2  2.4~6.0   라온(팔걸이)과 어르신이 초원 길을 나란히 걸어 마을로 간다
    샷3  5.4~9.0   새 장부 — 먹의 검은 소매 손이 펜으로 이름 칸만 쓴다(곤충 그림 + 이름, 숫자 칸 없음)
    샷4  8.4~12.0  꽃 핀 들판 속 빈 석판 하나 — 여전히 비어 있다. 나비가 잠깐 앉았다 간다
    샷5 11.4~15.0  새벽 초원, 그물 없이 뻗은 손에 무당벌레가 날아와 앉는다(자막 없음)
"""
import math

import numpy as np

from sound import hz
from kit import (INK, PAL, H, W, InsectSprite, Mask, Particles, add, bezier, blur_arr, book_sky, bush, c, crate,
                 dot, draw_hand, draw_insect, draw_person, ease_in_out, ease_out, grow, hills, insect_parts, lerp,
                 meadow, over, paint, person_parts, radial, rim, slab, smooth, span, stamp, sun, tree,
                 zoom)
from silhouette_kit import bokeh, scribble
from bk_v4 import Piece, beetle_wings, hand_parts_safe, sleeve, spark

DURS = [3.0, 3.0, 3.0, 3.0, 3.0]
TINT = "#ffe9c8"
CUES = [
    (1.0, 2.6, "하월은 곤충을 하나씩 놓아주었다."),
    (4.0, 2.6, "라온은 어르신 곁으로 돌아갔다."),
    (7.0, 2.6, "새 장부에는 숫자 칸이 없다."),
    (10.0, 2.6, "빈칸은 또 생길지도 모른다."),
]

GOLD_DAWN = [(0, "#a9bdf0"), (0.4, "#ffd7b0"), (0.7, "#ffe8b8"), (1, "#fff6dc")]


def dawn_field(seed, horizon, sun_xy=None, flowers=60, far="#b9d98f"):
    img = book_sky(GOLD_DAWN, seed=seed)
    if sun_xy:
        add(img, c("#ffe2a8"), radial(W, H, sun_xy[0], sun_xy[1], 420, 2.4) * 0.28)
        sun(img, sun_xy[0], sun_xy[1], 46, color="#fff1a8", halo=0.22)
    hills(img, seed + 1, horizon - 34, 26, "#c6b4d9", ink="#a08cb8", line=1.0, hi=0.04)
    hills(img, seed + 2, horizon - 8, 18, far, line=1.2, hi=0.06)
    meadow(img, horizon + 14, seed + 3, flowers=flowers)
    return img


def bug_rotations(kind, s):
    return {round(a, 1): insect_parts(kind, s, a) for a in np.arange(-3.2, 3.25, 0.1)}


def bug_at(table, rot):
    return table[round(min(3.2, max(-3.2, round(rot, 1))), 1)]


# ══════════════════════════ 샷1 — 하월이 놓아준다 ══════════════════════════

def shot_release():
    img = dawn_field(1101, 420, sun_xy=(990, 352), flowers=46)
    tree(img, 150, 470, 190, seed=1102, sway=4)
    crate(img, 392, 612, 104, 72, lid=1.0, tag=True, seed=1103)          # 빈 상자 — 뚜껑이 열려 있다
    x, feet, h = 572.0, 716.0, 400.0
    arm = (0.40, -0.66)
    hw = (x + arm[0] * h, feet + arm[1] * h)
    p = person_parts("hawol", h, arm=arm)
    draw_person(img, "hawol", x, feet, h, parts=p)
    hp = hand_parts_safe(20, rot=0.62, pose="open", curl=0.15, sleeve_len=0.7)
    draw_hand(img, hw[0], hw[1], 20, parts=hp, skin=PEOPLE_SKIN["hawol"], sleeve="#55565f")
    rim_l = rim(np.maximum(p["top"], p["hair"]), -3, 3, 3)
    bw, bh = p["box"]
    ox, oy = p["origin"]
    stamp(img, rim_l * 0.5, x - ox + bw / 2, feet - oy + bh / 2, "#ffd890", "add")     # 해 쪽 테두리 빛
    palm = (hw[0] + 12, hw[1] - 22)
    table = bug_rotations("longhorn", 26)
    path = bezier(palm, (palm[0] + 40, palm[1] - 140), (palm[0] + 10, palm[1] - 250), (812, 96), 60)
    far_b = [InsectSprite("bfly", 13, rot=0.4), InsectSprite("bfly", 11, rot=-0.3)]
    motes = Particles(34, 1104, (0, 0, W, H), vel=((-5, 5), (-9, -2)), size=(0.8, 2.0))

    def frame(u):
        f = img.copy()
        for i, b in enumerate(far_b):     # 먼저 놓아준 곤충들이 멀리 난다
            b.draw(f, 330 + 260 * i + 40 * u, 150 + 30 * i + math.sin(u * 3 + i) * 8, u + i, rate=16, alpha=0.6)
        fl = span(u, 0.75, 2.9)
        if fl <= 0:
            draw_insect(f, "longhorn", palm[0], palm[1] + math.sin(u * 3) * 1.5, 26, parts=bug_at(table, -0.5))
        else:
            e = ease_in_out(fl)
            i = min(len(path) - 2, int(e * (len(path) - 1)))
            bx, by = path[i]
            nx, ny = path[i + 1]
            bx += math.sin(u * 9) * 3 * e
            rot = math.atan2(nx - bx, -(ny - by))
            beetle_wings(f, bx, by, rot, u, s=26, amount=smooth(span(u, 0.75, 0.95)))
            draw_insect(f, "longhorn", bx, by, 26, parts=bug_at(table, rot))
            if fl < 0.3:
                spark(f, palm[0] + 10, palm[1] - 10, 10, math.sin(math.pi * fl / 0.3))
        motes.draw(f, u, c("#fff1c4"), 0.4)
        return zoom(f, 1.0 + 0.05 * ease_in_out(u / 3.0), 700, 360)

    return frame


PEOPLE_SKIN = {"hawol": "#f0c8a8", "hero": "#f6c9a0", "meok": "#f0c49c"}


# ══════════════════════════ 샷2 — 라온과 어르신 ══════════════════════════

def shot_walk():
    horizon = 400.0
    img = dawn_field(1201, horizon, sun_xy=(250, 330), flowers=70)
    # 마을로 가는 흙길 — 아래 가운데에서 지평선으로 좁아진다
    m = Mask()
    m.poly([(470, H + 10), (860, H + 10), (668, horizon + 22), (644, horizon + 22)])
    pa = m.arr(1.0)
    over(img, c("#d9b77e"), pa * 0.9)
    over(img, c("#b8935c"), np.clip(grow(pa, 1.4) - pa, 0, 1) * 0.6)
    # 길 끝 마을 — 작은 지붕 셋
    for k, (hx, hs) in enumerate(((620, 26), (676, 32), (720, 22))):
        hm = Mask()
        hm.rect(hx - hs * 0.8, horizon + 4 - hs, hx + hs * 0.8, horizon + 8)
        rm = Mask()
        rm.poly([(hx - hs, horizon + 6 - hs), (hx, horizon - hs * 1.7), (hx + hs, horizon + 6 - hs)])
        paint(img, hm.arr(0.5), "#f3e4c4", line=1.0, hi=0.0, lo=0.0)
        paint(img, rm.arr(0.5), ["#d9674e", "#c9573e", "#e07a4f"][k], line=1.0, hi=0.0, lo=0.0)
    tree(img, 1080, 470, 230, seed=1202, sway=-5)
    bush(img, 200, 520, 140, seed=1203, fill="#55b846", berries="#ff7aa2")
    phases = [2 * math.pi * k / 8 for k in range(8)]
    raon = [person_parts("raon", 222, walk=ph, sling=True) for ph in phases]
    elder = [person_parts("elder", 250, walk=ph + 0.6) for ph in phases]
    bf = InsectSprite("bfly", 24, rot=0.3)
    motes = Particles(30, 1204, (0, 0, W, H), vel=((-5, 5), (-8, -2)), size=(0.8, 1.8))

    def frame(u):
        f = img.copy()
        drift = 26 * ease_in_out(u / 3.6)
        k = int(u * 5.0) % 8
        bob = abs(math.sin(u * 5.0 * math.pi / 4)) * 3
        draw_person(f, "elder", 700, 646 - drift - bob, 250, t=u, parts=elder[k])
        draw_person(f, "raon", 584, 652 - drift - bob * 0.8, 222, t=u, parts=raon[(k + 4) % 8])
        bf.draw(f, 540 + 120 * math.sin(u * 0.9), 300 + math.sin(u * 3.3) * 18 - drift, u, rate=15)
        motes.draw(f, u, c("#fff1c4"), 0.35)
        return zoom(f, 1.0 + 0.06 * ease_in_out(u / 3.6), 650, 360)

    return frame


# ══════════════════════════ 샷3 — 새 장부 ══════════════════════════

BOOK = dict(cx=530.0, cy=320.0, pw=222.0, ph=318.0)
ROWS = 6
HS = 46          # 먹의 손 크기
ROW_KINDS = ["beetle", "bfly", "dfly", "firefly", "mantis", "longhorn", "moth", "ant", "bfly", "beetle", "dfly", "ant"]


def desk():
    """나무 책상 — 널빤지 다섯 장, 이음선과 옅은 나뭇결."""
    img = np.ones((H, W, 3), np.float32) * c("#c58d58")
    r = np.random.default_rng(1301)
    lm, gm = Mask(), Mask()
    ph = 150
    for k in range(-1, H // ph + 2):
        y0 = k * ph + 40
        tone = r.uniform(-0.05, 0.05)
        img[max(0, y0):max(0, min(H, y0 + ph))] *= 1 + tone
        lm.line([(0, y0), (W, y0)], 3.0)
        for j in range(3):
            yy = y0 + r.uniform(20, ph - 20)
            x = r.uniform(-200, 200)
            pts = [(x + i * 80, yy + 6 * math.sin(i * 0.7 + j)) for i in range(20)]
            gm.line(pts, 1.6)
    over(img, c("#8a5a32"), lm.arr(0.6) * 0.8)
    over(img, c("#a8723f"), gm.arr(0.6) * 0.45)
    add(img, c("#fff1cc"), radial(W, H, 240, -80, 900, 1.6) * 0.3)        # 창으로 드는 아침 빛
    return img


def page_rect(side):
    b = BOOK
    x0 = b["cx"] - b["pw"] - 4 if side < 0 else b["cx"] + 4
    return x0, b["cy"] - b["ph"] / 2, x0 + b["pw"], b["cy"] + b["ph"] / 2


def row_pos(side, i):
    x0, y0, x1, y1 = page_rect(side)
    return x0 + 26, y0 + 44 + i * 46


def draw_book(img):
    b = BOOK
    m = Mask()
    m.rect(b["cx"] - b["pw"] - 18, b["cy"] - b["ph"] / 2 - 14, b["cx"] + b["pw"] + 18, b["cy"] + b["ph"] / 2 + 14, r=10)
    over(img, c("#2a1f18"), blur_arr(m.arr(0.6), 14) * 0.45)                 # 책 그림자
    paint(img, m.arr(0.6), "#2b2b33", hi=0.08, lo=0.1)                       # 검은 표지 — 먹의 장부
    pm = Mask()
    for side in (-1, 1):
        pm.rect(*page_rect(side), r=4)
    pa = pm.arr(0.6)
    paint(img, pa, "#fbf3df", line=1.2, hi=0.0, lo=0.08)
    cm = Mask()
    cm.rect(b["cx"] - 10, b["cy"] - b["ph"] / 2, b["cx"] + 10, b["cy"] + b["ph"] / 2)
    over(img, c("#c9b48e"), blur_arr(cm.arr(0.5), 6) * pa * 0.7)             # 가운데 접힘 그늘
    lm = Mask()                                                               # 연한 줄 — 칸은 이름 한 칸뿐
    for side in (-1, 1):
        x0, y0, x1, y1 = page_rect(side)
        for i in range(ROWS):
            yy = y0 + 44 + i * 46 + 16
            lm.line([(x0 + 14, yy), (x1 - 14, yy)], 1.2)
    over(img, c("#d8c7a4"), lm.arr(0.5) * 0.8)
    # 왼쪽 쪽은 이미 다 썼다 — 곤충 그림 + 이름
    sm = Mask()
    for i in range(ROWS):
        x, y = row_pos(-1, i)
        draw_insect(img, ROW_KINDS[i], x, y, 11)
        scribble(sm, x + 22, y + 2, b["pw"] - 66, 1, 1310 + i, row_h=14, weight=1.8)
    over(img, c("#3a3036"), sm.arr(0.4))


def shot_ledger():
    img = desk()
    draw_book(img)
    b = BOOK
    hp = hand_parts_safe(HS, rot=-0.85, pose="fist", sleeve_len=1.4)
    row_t = [(0.35 + 0.52 * i, 0.35 + 0.52 * (i + 1)) for i in range(ROWS)]
    wrow = b["pw"] - 66
    rows_done = {}
    pen = Mask(190, 110)                       # 펜(국소 상자 — 가운데가 끝에서 (75, -37))
    pen.taper([(20, 92), (46, 78), (170, 18)], 4, 10)
    pen_a = pen.arr(0.5)
    pen_ink, pen_hi = grow(pen_a, 1.4), rim(pen_a, 2, 2, 2) * 0.25
    row_parts = [insect_parts(k, 11) for k in ROW_KINDS]

    def row_alpha(i, prog):
        key = (i, round(prog, 2))
        if key not in rows_done:
            sm = Mask(int(wrow) + 30, 30)
            scribble(sm, 6, 15, wrow, 1, 1320 + i, row_h=14, weight=1.8, progress=prog)
            rows_done[key] = sm.arr(0.4)
        return rows_done[key]

    def frame(u):
        f = img.copy()
        tip = None
        for i, (t0, t1) in enumerate(row_t):
            if u < t0:
                break
            x, y = row_pos(1, i)
            ia = smooth(span(u, t0, t0 + 0.12))
            draw_insect(f, ROW_KINDS[6 + i], x, y, 11, alpha=ia, parts=row_parts[6 + i])
            prog = span(u, t0 + 0.14, t1 - 0.04)
            if prog > 0:
                stamp(f, row_alpha(i, prog), x + 22 + wrow / 2 + 9, y + 2, "#3a3036")
            if u < t1:
                tip = (x + 22 + wrow * prog + 6 + math.sin(u * 40) * 1.5, y + 2 + math.sin(u * 23) * 2.5) if prog > 0 \
                    else (x + 4, y - 2)
        if tip is None:   # 다 썼다 — 펜을 살짝 든다
            x, y = row_pos(1, ROWS - 1)
            lift = ease_out(span(u, row_t[-1][1], row_t[-1][1] + 0.5))
            tip = (x + 22 + wrow + 10 + 14 * lift, y - 18 * lift)
        tx, ty = tip
        # 펜 — 끝이 종이에 닿고 오른쪽 위로 누운다
        stamp(f, pen_ink, tx + 75, ty - 37, INK)
        stamp(f, pen_a, tx + 75, ty - 37, "#3a3b46")
        stamp(f, pen_hi, tx + 75, ty - 37, "#ffffff", "add")
        dot(f, tx, ty, 2.0, c("#c9c9d2"), 0.8)
        # 손 — 검은 소매가 오른쪽 아래(쓰는 사람 쪽)에서 들어와 펜대를 쥔다
        gx, gy = tx + 56, ty - 22
        wx, wy = gx + 0.466 * HS, gy + 0.409 * HS
        sleeve(f, wx + 0.75 * HS, wy + 0.66 * HS, wx + 620, wy + 545, HS * 0.5, HS * 0.78, "#26262e")
        draw_hand(f, wx, wy, HS, parts=hp, skin=PEOPLE_SKIN["meok"], sleeve="#26262e")
        return zoom(f, 1.1 - 0.06 * ease_in_out(u / 3.6), b["cx"] + b["pw"] * 0.5, b["cy"])

    return frame


# ══════════════════════════ 샷4 — 빈 석판 ══════════════════════════

def shot_slab():
    horizon = 380.0
    img = book_sky([(0, "#a6cdf5"), (0.5, "#dff0ff"), (1, "#fff3d6")], seed=1401)
    hills(img, 1402, horizon - 20, 22, "#a8d98a", line=1.2, hi=0.06)
    for k, bx in enumerate((150, 330, 980, 1160)):
        bush(img, bx, horizon + 40 + 10 * (k % 2), 150, seed=1403 + k, fill="#4fb43e", berries="#ff7aa2")
    meadow(img, horizon + 30, 1405, flowers=170)
    sx, base, sw, sh = 640.0, 548.0, 156.0, 286.0
    slab(img, sx, base, sw, sh, fill="#cfcbc2", glyph=0.0, glow=0.12)
    # 비어 있는 이름 칸 — 오목한 네모(아무것도 새겨져 있지 않다)
    top = base - sh + sw * 0.32 + 10
    cm = Mask()
    cm.rect(sx - sw * 0.3, top + 36, sx + sw * 0.3, top + 36 + sw * 0.62, r=8)
    ca = cm.arr(0.6)
    over(img, c("#a9a59c"), ca * 0.55)
    over(img, c("#7d786e"), rim(ca, 3, 3, 2.5) * 0.7)
    add(img, c("#ffffff"), rim(ca, -3, -3, 2.5) * 0.35)
    # 석판 앞 풀·꽃 한 겹(석판 발치를 덮는다)
    fm = Mask()
    from silhouette_kit import grass as sk_grass
    sk_grass(fm, 1406, base - 6, base + 14, 70, height=(16, 44), width=(1.6, 3.2), x0=sx - 140, x1=sx + 140)
    over(img, c(PAL["grass"]), fm.arr(0.5))
    petals = Particles(26, 1407, (0, 0, W, H), vel=((18, 34), (6, 16)), size=(1.4, 2.6), wobble=14)
    bf = [InsectSprite("bfly", 30, rot=r) for r in (0.0,)]
    bf2 = InsectSprite("bfly", 22, rot=-0.6)
    seat = (sx + 8, base - sh + 18)

    def frame(u):
        f = img.copy()
        petals.draw(f, u, c("#ffb3cf"), 0.55)
        # 나비가 석판 머리에 잠깐 앉았다 간다
        k_in = ease_in_out(span(u, 0.2, 1.5))
        k_out = ease_in_out(span(u, 2.5, 3.6))
        if k_out <= 0:
            x = lerp(300, seat[0], k_in)
            y = lerp(170, seat[1], k_in) - math.sin(math.pi * k_in) * 50
            rate = 16 if k_in < 1 else 3
            bf[0].draw(f, x, y, u, rate=rate)
        else:
            x = lerp(seat[0], 1000, k_out)
            y = lerp(seat[1], 150, k_out) - math.sin(math.pi * k_out) * 30
            bf[0].draw(f, x, y, u, rate=16)
        bf2.draw(f, 930 - 60 * u, 260 + math.sin(u * 2.7) * 14, u, rate=14, alpha=0.8)
        return zoom(f, 1.0 + 0.08 * ease_in_out(u / 3.6), sx, base - sh * 0.55)

    return frame


# ══════════════════════════ 샷5 — 손에 내려앉는 무당벌레(1편과 같은 자리) ══════════════════════════

HAND_W, HAND_S, HAND_ROT = (735, 525), 108, -0.42      # 1편 샷4와 같은 손


def _xfp(x, y, s, rot, ox, oy):
    return ox + (x * math.cos(rot) - y * math.sin(rot)) * s, oy + (x * math.sin(rot) + y * math.cos(rot)) * s


def shot_hand():
    bg = book_sky([(0, "#ffcfa0"), (0.45, "#ffe6bf"), (0.75, "#cfe8a0"), (1, "#8ccb6c")], seed=1501)
    back = Mask()
    r = np.random.default_rng(1502)
    for _ in range(44):
        x = r.uniform(-20, W + 20)
        tip = (x + r.uniform(-60, 60), r.uniform(380, 600))
        back.taper(bezier((x, H + 20), (x, (H + tip[1]) / 2), tip, tip, 10), r.uniform(12, 26), 2)
    over(bg, c("#7cc45a"), blur_arr(back.arr(0.5), 6) * 0.9)
    # 꽃 망울(흐릿하게) — 1편엔 없던 색
    fl = np.zeros((H, W, 3), np.float32)
    for k in range(18):
        add(fl, c(["#ff9ec0", "#ffe27a", "#ffffff"][k % 3]),
            radial(W, H, r.uniform(0, W), r.uniform(520, 700), r.uniform(12, 22), 1.4) * 0.8)
    bg += fl * 0.7
    bg += bokeh(W, H, 1503, 26, c("#fff1c0"), rmin=16, rmax=50, alpha=0.18)
    add(bg, c("#fff3d0"), radial(W, H, 980, 60, 600, 1.8) * 0.22)
    np.clip(bg, 0, 1, out=bg)
    hp = hand_parts_safe(HAND_S, rot=HAND_ROT, pose="open", sleeve_len=2.6)
    box = hp["box"]
    hand = Piece(lambda d: draw_hand(d, box / 2, box / 2, HAND_S, parts=hp, skin=PEOPLE_SKIN["hero"],
                                     sleeve="#3fae6a"), box, box)
    land = _xfp(-0.02, -0.6, HAND_S, HAND_ROT, *HAND_W)
    path = bezier((250, 110), (420, 330), (560, 170), (land[0], land[1] - 4), 60)
    table = bug_rotations("beetle", 36)
    glow = radial(320, 320, 160, 160, 160, 1.6)
    stars = [(-46, -40, 2.0, 15), (52, -30, 2.35, 11), (-30, 34, 2.6, 9), (44, 40, 2.15, 8), (0, -66, 2.9, 12)]
    company = [(InsectSprite("bfly", 22, rot=0.5), (300, 250), 0.8), (InsectSprite("dfly", 26, rot=-1.4), (1010, 170), 1.7),
               (InsectSprite("bfly", 16, rot=-0.3), (790, 120), 2.6)]
    flies = Particles(16, 1504, (0, 200, W, 560), vel=((-6, 6), (-4, 2)), size=(1.0, 1.8), twinkle=1.0)

    def frame(u):
        f = bg.copy()
        flies.draw(f, u, c("#fff27a"), 0.35)                          # 반딧불이·작은 벌레들의 빛
        for spr, (x0, y0), ph in company:                             # 곁에 곤충들이 난다 — 1편엔 없었다
            spr.draw(f, x0 + 40 * math.sin(u * 0.8 + ph), y0 + 22 * math.sin(u * 1.9 + ph), u + ph, rate=15,
                     alpha=0.85)
        bob = 2.0 * math.sin(u * 1.6)
        sleeve(f, HAND_W[0] + 120, HAND_W[1] + 240, HAND_W[0] + 300, H + 120, 64, 70, "#3fae6a")
        hand.blit(f, HAND_W[0], HAND_W[1] + bob)
        fl_ = span(u, 0.2, 1.75)
        lx, ly = land[0], land[1] + bob
        if fl_ < 1:
            e = ease_in_out(fl_)
            i = min(len(path) - 2, int(e * (len(path) - 1)))
            x, y = path[i]
            nx, ny = path[i + 1]
            x += math.sin(u * 9) * 3 * (1 - e)
            y += math.cos(u * 7) * 3 * (1 - e)
            rot = math.atan2(nx - x, -(ny - y))
            beetle_wings(f, x, y, rot, u, s=36)
            draw_insect(f, "beetle", x, y, 36, parts=bug_at(table, rot))
        else:
            k = span(u, 1.75, 2.05)
            hop = math.sin(math.pi * k) * 6
            rot = lerp(math.atan2(path[-1][0] - path[-3][0], -(path[-1][1] - path[-3][1])), -0.35, ease_out(k))
            if k < 1:
                beetle_wings(f, lx, ly - 4 - hop, rot, u, s=36, amount=1 - k)
            stamp(f, glow * 0.5 * ease_out(span(u, 1.8, 2.4)) * (0.85 + 0.15 * math.sin(u * 3)), lx, ly, "#fff0b0",
                  "add")
            draw_insect(f, "beetle", lx, ly - 4 - hop, 36, parts=bug_at(table, rot))
            for dx, dy, t0, s in stars:
                kk = span(u, t0, t0 + 0.5)
                if 0 < kk < 1:
                    spark(f, lx + dx, ly + dy, s * math.sin(math.pi * kk), 0.95)
        return zoom(f, 1.0 + 0.1 * ease_in_out(u / 3.6), lerp(W * 0.5, land[0], 0.7), lerp(H * 0.5, land[1], 0.7))

    return frame


SHOTS = [shot_release, shot_walk, shot_ledger, shot_slab, shot_hand]


# ══════════════════════════ 소리 ══════════════════════════

def score(sc):
    """1편과 같은 새벽 바람·새 — 이번엔 풀벌레가 있다. 장면마다 작은 소리, 끝은 이름의 동기 전체와 따뜻한 화음."""
    T = sc.total
    s = [sc.shot(k) for k in range(5)]
    sc.wind([(0, 0), (0.5, 0.8), (s[2], 0.5), (s[3], 0.7), (T, 0.4)], amp=0.028, lo=180, hi=1100, gust=0.4)
    sc.birds([(0, 0.7), (s[2], 0.35), (s[3], 0.7), (T, 0.8)], amp=0.028, rate=0.7)
    sc.crickets([(0, 0.5), (s[2], 0.25), (s[3], 0.6), (T, 0.8)], amp=0.014)       # 돌아온 풀벌레
    # 바탕 — D장조 → G → D (따뜻하게)
    sc.pad([hz("D3"), hz("A3"), hz("F#4")], [(0, 0), (1.0, 0.6), (s[2], 0.6), (s[2] + 0.8, 0), (T, 0)], amp=0.038)
    sc.pad([hz("G2"), hz("D3"), hz("B3")], [(0, 0), (s[2], 0), (s[2] + 0.8, 0.6), (s[3] + 0.4, 0.55), (s[3] + 1.2, 0),
                                            (T, 0)], amp=0.038)
    sc.pad([hz("D3"), hz("F#3"), hz("A3"), hz("D4")], [(0, 0), (s[3] + 0.6, 0), (s[3] + 1.8, 0.65), (T - 0.6, 0.8),
                                                       (T, 0.5)], amp=0.04, bright=0.3)
    # 샷1 — 놓아주면 날개 소리가 멀어진다
    sc.whoosh(0.8, 1.2, 0.05, 300, 2200, pan_from=0.0, pan_to=0.6)
    for k in range(4):
        sc.noise(0.8 + k * 0.28, 0.25, 500, 2600, 0.03 * (1 - k * 0.2), decay=8, pan=0.2 * k)
    sc.melody(1.0, ["A4", "D5", ("F#5", 2)], step=0.32, amp=0.08, kind="kalimba", pan=0.3)
    # 샷2 — 걸음(풀밭)과 지팡이
    for k in range(7):
        sc.step(s[1] + 0.3 + k * 0.42, 0.05)
        if k % 2 == 0:
            sc.pluck(s[1] + 0.32 + k * 0.42, hz("E6"), 0.02, 0.3, pan=0.3, kind="xylo")
    sc.melody(s[1] + 0.6, ["F#5", "E5", "D5", ("A4", 2)], step=0.34, amp=0.07, kind="kalimba", pan=-0.2)
    # 샷3 — 펜 소리(이름만 쓴다)
    for i in range(6):
        sc.scratch(s[2] + 0.5 + 0.52 * i, 0.34, 0.035)
    sc.page(s[2] + 0.1, 0.06)
    # 샷4 — 빈 석판: 낮은 종 하나, 나비 날갯짓
    sc.chime(s[3] + 0.6, hz("D5"), 0.06, dur=2.2)
    sc.chime(s[3] + 1.6, hz("A4"), 0.04, dur=2.0, pan=-0.3)
    # 샷5 — 날아와 내려앉는다 → 이름의 동기 전체 + 따뜻한 화음
    land = s[4] + 1.75
    for k in range(5):
        sc.noise(s[4] + 0.25 + k * 0.3, 0.3, 400, 2600, 0.025, decay=6, pan=-0.6 + k * 0.25)
    sc.melody(land, ["D5", "F#5", "A5", "B5", ("A5", 2)], step=0.3, amp=0.13, kind="kalimba")
    sc.sparkle(land + 0.15, dur=1.2, density=9, amp=0.03)
    for k, nt in enumerate(("D4", "F#4", "A4", "D5")):
        sc.pluck(land + 1.95 + k * 0.03, hz(nt), 0.08, 2.4, pan=-0.3 + 0.2 * k, kind="kalimba")
