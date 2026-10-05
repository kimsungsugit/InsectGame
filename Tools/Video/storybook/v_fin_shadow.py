"""fin 「이름을 달라는 그림자」 — 최종 대치(`fin_unnamed`) **대사 앞**에 튼다.

설명을 영상이 맡는다: 울타리의 마지막 빈칸에 그림자가 서 있다, 그림자는 빼앗은 모습으로 바뀐다,
빈 이름표를 내밀며 이름을 달라고 한다, 받으면 울타리 안에 자리를 얻는다(상상 장면), 그러니 주지 않는다.
대사는 그 뒤에 "저게 그림자예요"·"절대 주지 마세요"로 짧게 받고 선택지로 넘어간다.

    샷1  0.0~2.8   텅 빈 들, 울타리의 맨 끝 — 말뚝 하나가 빠진 빈칸에 그림자. 아래에 주인공과 파트너의 뒷모습
    샷2  2.2~5.8   모습 바꾸기 — 사마귀 → 나비 → 반딧불이, 바뀔 때마다 일렁인다
    샷3  5.2~8.6   다가와 빈 이름표를 내민다 — 노란 눈이 크다
    샷4  8.0~11.6  「만약」 — 구름 테두리·흐린 색: 이름표에 그림자 무늬가 새겨지고, 그림자가 울타리 안에서 부풀어 덮는다
    샷5 11.0~14.4  다시 지금 — 주인공 뒤로 만난 곤충들이 줄지어 빛나고 울타리 칸이 하나씩 켜진다. 마지막 한 칸만 비었다
"""
import math

import numpy as np

from sound import hz
from kit import (INK, PAL, H, W, InsectSprite, Mask, Particles, add, bezier, blur_arr, c, draw_eyes, draw_insect,
                 draw_person, ease_in_out, ease_out, fbm, grow, lerp, over, paint_plate, person_parts,
                 plate_rect, post_pts, put_layer, rim, smooth, span, stamp, zoom)
from bk_v4 import (FENCE_KINDS, LightBug, ShadowForm, TwoLayer, bloom_from, dist_field, draw_shadow, dream_border,
                   fence_with_gap, glow_spot, ring, spark, wash, wobble_region)

DURS = [2.8, 3.0, 2.8, 3.0, 2.8]
TINT = "#f1ecff"
CUES = [
    (0.5, 2.2, "울타리의 맨 끝, 마지막 빈칸."),
    (2.8, 2.6, "그림자는 빼앗은 모습으로 바뀐다."),
    (5.6, 2.4, "그리고 묻는다. '이름을 줘.'"),
    (8.4, 2.8, "이름을 받으면, 울타리 안에 자리를 얻는다."),
    (11.4, 2.6, "그러니 절대, 이름을 주지 않는다."),
]

KINDS = FENCE_KINDS


def empty_scene(seed, horizon, front_draw):
    """텅 빈 들 — (색 뺀 배경, (색 뺀 앞층 premul, 앞층 알파)). 그림자는 배경과 앞층 사이에 그린다."""
    L = TwoLayer(seed, horizon, front_draw, color=False)
    return L.bg_d, (L.fr_d, L.a)


# ══════════════════════════ 샷1 — 마지막 빈칸 ══════════════════════════

def shot_gap():
    base, pw, ph = 455.0, 44.0, 240.0
    gap, step = 640.0, 165.0
    left = [gap - step * k for k in range(4, 0, -1)]
    bg, front = empty_scene(901, 450, lambda cv: fence_with_gap(cv, left, gap, [gap + step], base, pw, ph, 903))
    hero = person_parts("hero", 150)
    motes = Particles(28, 902, (0, 0, W, H), vel=((-6, 6), (-6, -1)), size=(0.8, 1.8))
    sx, sy = gap, base - 82

    def frame(u):
        f = bg.copy()
        draw_shadow(f, sx, sy + math.sin(u * 1.6) * 3, 0.7, u + 1.0, eyes=ease_out(span(u, 0.3, 0.9)),
                    blink_period=2.0)
        put_layer(f, *front)
        motes.draw(f, u, c("#e8e2f4"), 0.25)
        draw_person(f, "hero", 536, 580, 150, t=u, parts=hero)
        draw_insect(f, "beetle", 590, 568, 20, rot=0.05 * math.sin(u * 2))
        return zoom(f, 1.0 + 0.07 * ease_in_out(u / 2.8), gap - 10, 400)

    return frame


# ══════════════════════════ 샷2 — 모습 바꾸기 ══════════════════════════

CLOSE = dict(base=640.0, pw=78.0, ph=560.0, posts=(470.0, 810.0))


def close_scene(seed):
    base, pw, ph = CLOSE["base"], CLOSE["pw"], CLOSE["ph"]
    lx, rx = CLOSE["posts"]
    return empty_scene(seed, 380, lambda cv: fence_with_gap(cv, [lx - 340.0, lx], 640.0, [rx, rx + 340.0],
                                                             base, pw, ph, seed + 5, flowers=24))


def shot_forms():
    bg, front = close_scene(911)
    cx, cy = 640.0, 300.0
    mantis = ShadowForm("mantis", 98)
    bfly = ShadowForm("bfly", 112, flaps=(0.0, 0.35, 0.7))
    fire = ShadowForm("firefly", 118)
    # (모습, 나타남 시작, 사라짐 시작)
    plan = [(mantis, -1.0, 1.25), (bfly, 1.55, 2.45), (fire, 2.75, 9.0)]
    morphs = (1.25, 2.45)

    def frame(u):
        f = bg.copy()
        bob = math.sin(u * 2.0) * 5
        for form, t_in, t_out in plan:
            a = smooth(span(u, t_in, t_in + 0.25)) * (1 - smooth(span(u, t_out, t_out + 0.25)))
            if a <= 0.01:
                continue
            k = int((0.5 + 0.5 * math.sin(u * 9)) * 2.99) if form is bfly else 0
            form.draw(f, cx, cy + bob, u, alpha=a, k=k)
        for m0 in morphs:
            k = span(u, m0 - 0.05, m0 + 0.6)
            if 0 < k < 1:
                draw_shadow(f, cx, cy + bob, 0.9 + 0.35 * math.sin(math.pi * k), u * 3, alpha=math.sin(math.pi * k),
                            eyes=0.0)
                for j in range(2):
                    kk = span(k, 0.1 * j, 0.7 + 0.1 * j)
                    if kk > 0:
                        ring(f, cx, cy + bob, 60 + 220 * kk, 0.7 * (1 - kk), squash=0.8, width=3.5)
                wobble_region(f, cx - 230, cy - 200, cx + 230, cy + 200, 9 * math.sin(math.pi * k), u)
        put_layer(f, *front)   # 그림자는 바깥 — 말뚝이 앞이다
        return zoom(f, 1.02 + 0.04 * u / 3.6, cx, cy + 40)

    return frame


# ══════════════════════════ 샷3 — 빈 이름표를 내민다 ══════════════════════════

def blank_plate(w, h):
    pad = 48     # 번짐(blur 14)이 상자 끝에서 잘리지 않게 반경의 3배 넘게 비운다
    m = Mask(int(w) + pad * 2, int(h) + pad * 2)
    m.rect(pad, pad, pad + w, pad + h, r=10)
    a = m.arr(0.5)
    return a, grow(a, 1.6), blur_arr(a, 14)


def shot_offer():
    bg, (fp, fa) = close_scene(911)
    bg = np.stack([blur_arr(bg[..., k], 2.5) for k in range(3)], axis=-1)
    dim = c("#3a2f55")
    bg = (bg * 0.88 + dim * 0.12).astype(np.float32)
    fp = (fp * 0.88 + dim * 0.12 * fa[..., None]).astype(np.float32)
    front = (fp, fa)
    plates = {}

    def plate_at(scale):
        key = round(scale, 2)
        if key not in plates:
            plates.clear()
            plates[key] = blank_plate(150 * scale, 124 * scale)
        return plates[key]

    def frame(u):
        f = bg.copy()
        app = ease_in_out(span(u, 0.0, 2.2))
        sc = lerp(0.95, 1.75, app)
        sx, sy = 640.0, lerp(300, 262, app) + math.sin(u * 1.7) * 4
        draw_shadow(f, sx, sy, sc, u + 4.0, eyes=0.0)
        # 노란 눈 — 크게. 묻는 순간 천천히 한 번 깜빡인다
        bl = 1.0 - 0.9 * math.sin(math.pi * span(u, 2.55, 2.85))
        es = sc * 1.25
        draw_eyes(f, sx, sy - 14 * sc, es, 1.0, blink=bl, spread=28)
        glow_spot(f, sx - 28 * es, sy - 20 * sc, 26 * sc, PAL["eye"], 0.16)
        glow_spot(f, sx + 28 * es, sy - 20 * sc, 26 * sc, PAL["eye"], 0.16)
        put_layer(f, *front)   # 몸은 울타리 바깥 — 빈칸 너머에서 들여다본다
        # 빈 이름표 — 빈칸으로 손을 뻗어 안쪽으로 내민다
        k = ease_out(span(u, 0.6, 1.8))
        if k > 0:
            ps = lerp(0.7, 1.15, k) + 0.04 * ease_in_out(span(u, 1.8, 3.4))
            px, py = 640.0, lerp(sy + 70, 452, k)
            tm = Mask()
            pts = [(sx - 20 + 10 * math.sin(u * 3), sy + 50 * sc), (sx - 46, (sy + py) / 2 + 10),
                   (px - 30, py - 40 * ps), (px - 6, py - 30 * ps)]
            tm.taper(bezier(*pts, 18), 13 * sc, 9)
            ta = tm.arr(1.0) * k
            add(f, c(PAL["shadow_glow"]), blur_arr(ta, 8) * 0.4)
            over(f, c("#1a1030"), grow(ta, 1.6))
            over(f, c(PAL["shadow"]), ta)
            a, ink, glow = plate_at(ps)
            stamp(f, glow * 0.55 * k, px, py, PAL["shadow_glow"], "add")
            stamp(f, ink * k, px, py, INK)
            stamp(f, a * k, px, py, "#ece4d2")
            stamp(f, rim(a, 3, 3, 3) * 0.25 * k, px, py, "#ffffff", "add")
        return zoom(f, 1.0 + 0.05 * ease_in_out(u / 3.4), 640, 360)

    return frame


# ══════════════════════════ 샷4 — 「만약」 ══════════════════════════

def shot_whatif():
    base, pw, ph, step = 330.0, 36.0, 180.0, 128.0
    gap = 640.0
    left = [gap - step * k for k in range(5, 0, -1)]
    right = [gap + step * k for k in range(1, 6)]
    bg, (fp, fa) = empty_scene(921, 340, lambda cv: fence_with_gap(cv, left, gap, right, base, pw, ph, 922,
                                                                    flowers=46))
    img = (bg * (1 - fa[..., None]) + fp).astype(np.float32)
    inner, edge = dream_border(923)
    outside = 1 - inner
    paper = fbm(W, H, 924, (160, 50), (0.7, 0.3))
    frame_col = (c("#fbf8ff") * (0.95 + 0.08 * paper[..., None])).astype(np.float32)
    dist = dist_field(gap, 470)
    gx0, gy0, gx1, gy1 = plate_rect(gap, base, pw, ph)
    pcx, pcy = (gx0 + gx1) / 2, (gy0 + gy1) / 2
    ghost = Mask()
    ghost.poly(post_pts(gap, base, pw, ph))
    ga = ghost.arr(0.6)
    mark = ShadowForm("beetle", 19)      # 이름표에 새겨지는 그림자 무늬(빼앗은 모습 하나)

    def frame(u):
        f = img.copy()
        zc = ease_in_out(span(u, 1.2, 2.6))
        # 1) 이름을 받으면 — 빈칸에 그림자의 말뚝이 들어서고 빈 이름표에 그림자 무늬가 새겨진다
        pa = ease_out(span(u, 0.3, 0.9))
        if pa > 0:
            over(f, c(INK), grow(ga, 1.6) * pa)
            over(f, c("#6b5a8a"), ga * pa)
            paint_plate(f, gap, base, pw, ph, None, level=0.0, glow=False)
            mk = ease_out(span(u, 0.9, 1.6))
            if mk > 0:
                glow_spot(f, pcx, pcy, 40, PAL["shadow_glow"], 0.5 * mk)
                mark.draw(f, pcx, pcy, u, alpha=mk)
        # 2) 그림자가 울타리 **안** 풀밭에서 부풀어 보랏빛으로 덮는다
        g = ease_in_out(span(u, 1.5, 3.4))
        if g > 0:
            cover = bloom_from(dist, 60 + 900 * g, soft=0.5) * (0.62 * g)
            over(f, c("#4a3778"), cover)
            sy = lerp(base + 10, 390, g)
            draw_shadow(f, gap, sy, lerp(0.35, 2.3, g), u * 1.3 + 2.0, eyes=smooth(span(u, 2.3, 2.9)),
                        blink_period=9.0)
        # 처음엔 이름표를 가까이 보다가 물러나며 부풀어 오르는 그림자를 담는다
        f = zoom(f, lerp(2.1, 1.0, zc), lerp(pcx, 640, zc), lerp(pcy, 360, zc))
        # 상상 — 흐린 색, 천천히 일렁임, 구름 테두리
        f = wash(f, 0.24, "#f3eefc")
        wobble_region(f, 0, 0, W, H, 2.2, u, freq=0.025, speed=2.5)
        over(f, frame_col, outside)
        over(f, c("#9a90bb"), edge * 0.9)
        return f

    return frame


# ══════════════════════════ 샷5 — 다시 지금 ══════════════════════════

def shot_stand():
    base, pw, ph, step = 400.0, 38.0, 190.0, 130.0
    gap = 720.0
    left = [gap - step * k for k in range(6, 0, -1)]
    right = [gap + step]
    holder = {}

    def draw_front(cv):
        holder["posts"] = fence_with_gap(cv, left, gap, right, base, pw, ph, 932, flowers=36)

    bg, front = empty_scene(931, 410, draw_front)
    posts = holder["posts"]
    hero = person_parts("hero", 225)
    order = sorted(range(len(posts)), key=lambda i: abs(posts[i] - gap), reverse=True)   # 먼 칸부터 켜진다
    bugs = []
    kinds = ["bfly", "beetle", "dfly", "firefly", "longhorn", "moth", "ant"]
    for i, k in enumerate(kinds):
        x = lerp(452, 828, i / (len(kinds) - 1))
        y = 150 + 46 * ((x - 640) / 188) ** 2
        flaps = (0.0, 0.4, 0.8) if k in ("bfly", "moth", "dfly") else (0.0,)
        s = 34 if k == "dfly" else 30
        spr = InsectSprite(k, s, frames=3) if len(flaps) > 1 else None
        bugs.append((k, x, y, s, spr, LightBug(k, s, flaps=flaps)))

    def frame(u):
        f = bg.copy()
        draw_shadow(f, gap, base - 66, 0.52, u + 6.0, eyes=1.0, blink_period=2.6)   # 마지막 빈칸 너머
        put_layer(f, *front)
        for j, i in enumerate(order):          # 울타리 칸이 하나씩 켜진다(먼 칸부터)
            lv = smooth(span(u, 0.55 + 0.3 * j, 0.85 + 0.3 * j))
            if lv > 0:
                paint_plate(f, posts[i], base, pw, ph, KINDS[i % len(KINDS)], level=lv)
        for i, (k, x, y, s, spr, light) in enumerate(bugs):   # 만난 곤충들이 줄지어 빛난다
            t0 = 0.45 + 0.27 * i
            a = smooth(span(u, t0, t0 + 0.35))
            if a <= 0:
                continue
            yy = y + math.sin(u * 2.4 + i) * 5
            fl = int((0.5 + 0.5 * math.sin(u * 10 + i)) * 2.99)
            light.draw(f, x, yy, alpha=0.55 * a, k=fl, glow=1.2)
            if spr is not None:
                spr.draw(f, x, yy, u + i, rate=10, alpha=a)
            else:
                draw_insect(f, k, x, yy, s, alpha=a, glow=0.8 if k == "firefly" else 0.0)
            spark(f, x + 22, yy - 20, 9, (1 - span(u, t0, t0 + 0.6)) * a * 1.2)
        glow_spot(f, 560, 420, 260, "#ffe7a8", 0.22 * smooth(span(u, 0.5, 2.6)))   # 곤충들의 빛이 등 뒤를 비춘다
        draw_person(f, "hero", 560, 585, 225, t=u, parts=hero)
        draw_insect(f, "beetle", 640, 566, 24, rot=0.25)
        return zoom(f, 1.0 + 0.06 * ease_in_out(u / 3.4), 640, 330)

    return frame


SHOTS = [shot_gap, shot_forms, shot_offer, shot_whatif, shot_stand]


# ══════════════════════════ 소리 ══════════════════════════

def score(sc):
    """텅 빈 바람 → 모습마다 휙 + 깜빡 → 「이름을 줘」의 불협 두 음 → 먹먹한 상상 → 이름의 동기가 장조로 쌓인다."""
    T = sc.total
    s = [sc.shot(k) for k in range(5)]
    sc.wind([(0, 0.0), (0.4, 0.8), (s[3], 0.7), (s[3] + 0.6, 0.25), (s[4], 0.3), (T, 0.15)], amp=0.05, lo=120, hi=700)
    # 낮은 단조 패드(D 단조) — 상상 장면에선 먹먹하게 낮아지고, 마지막엔 장조 패드로 넘어간다
    sc.pad([73.42, 146.83, 174.61, 220.0], [(0, 0), (1.0, 0.7), (s[2], 0.8), (s[3], 0.4), (s[4], 0.3), (s[4] + 1.0, 0),
                                            (T, 0)], amp=0.06, bright=0.1)
    sc.pad([55.0, 110.0, 130.81, 164.81], [(0, 0), (s[3], 0), (s[3] + 0.8, 0.9), (s[4] - 0.2, 0.9), (s[4] + 0.6, 0),
                                           (T, 0)], amp=0.07, bright=0.0, detune=0.006)
    sc.pad([146.83, 220.0, 293.66, 369.99], [(0, 0), (s[4] + 0.2, 0), (s[4] + 1.6, 0.85), (T - 0.4, 0.9), (T, 0.5)],
           amp=0.05, bright=0.3)
    sc.blink(1.0, 0.1)                                                  # 샷1 — 빈칸의 눈이 뜬다
    sc.boom(0.9, 70, 50, 1.2, 0.12)
    for m in (1.25, 2.45):                                              # 샷2 — 모습이 바뀔 때마다
        at = s[1] + m
        sc.whoosh(at - 0.15, 0.7, 0.2, 400, 2600, pan_from=-0.5, pan_to=0.5)
        sc.blink(at + 0.25, 0.09)
    sc.whoosh(s[2] + 0.1, 1.6, 0.16, 200, 1200, pan_from=0.0, pan_to=0.0)   # 샷3 — 다가온다
    at = s[2] + 0.95                                                    # 「이름을 줘」 — 속삭이는 듯한 두 음(불협)
    for k, (n1, n2) in enumerate((("C#4", "D4"), ("A#3", "B3"))):
        t0 = at + k * 0.62
        sc.pluck(t0, hz(n1), 0.07, 1.6, -0.3, kind="kalimba")
        sc.pluck(t0 + 0.01, hz(n2), 0.07, 1.6, 0.3, kind="kalimba")
        sc.noise(t0, 0.45, 1800, 5200, 0.035, decay=4.5, pan=-0.2 + 0.4 * k)
    sc.blink(s[2] + 2.6, 0.11)
    # 샷4 — 상상: 무늬가 새겨지고(긁는 소리) 그림자가 부풀어 덮는다(낮은 울림)
    sc.scratch(s[3] + 0.9, 0.6, 0.04)
    sc.whoosh(s[3] + 1.5, 2.0, 0.18, 120, 900, pan_from=0.0, pan_to=0.0)
    sc.boom(s[3] + 2.7, 80, 34, 1.4, 0.3)
    # 샷5 — 곤충들이 하나씩 빛나며 이름의 동기가 장조로 쌓인다
    motif = ("D5", "F#5", "A5", "B5", "A5", "D6", "F#6")
    for i, nt in enumerate(motif):
        t0 = s[4] + 0.45 + 0.27 * i
        sc.pluck(t0, hz(nt), 0.13, 1.6, pan=-0.6 + 0.2 * i, kind="kalimba")
        sc.pluck(t0 + 0.02, hz(nt) * 2, 0.025, 0.8, pan=-0.6 + 0.2 * i, kind="bell")
    sc.sparkle(s[4] + 0.5, 2.0, density=6, amp=0.03)
    for k, nt in enumerate(("D4", "F#4", "A4", "D5")):                 # 마지막 화음 — 용기
        sc.pluck(s[4] + 2.55 + k * 0.03, hz(nt), 0.09, 2.2, pan=-0.3 + 0.2 * k, kind="kalimba")
    sc.blink(s[4] + 2.9, 0.06)
