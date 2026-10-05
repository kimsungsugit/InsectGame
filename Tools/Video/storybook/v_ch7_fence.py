"""ch7 「움직이는 빈칸」 — 2막 개막(`ch7_opening`) **대사 앞**에 튼다. 0단계 화풍 샘플(Tools/Video/sample_ch7_fence.py)을 옮겼다.

설명을 영상이 맡는다: 이름 벽 = 울타리, 이름 하나 = 말뚝 하나, 이름이 바래면 구멍, 그림자가 그 구멍으로 숨어든다,
벽이 다 차자 숨은 한 칸(움직이는 빈칸)이 드러난다. 대사는 그 뒤에 "방금 저거 봤어?"로 짧게 받는다.

    샷1  0.0~2.6   이름 벽 — 칸마다 곤충 그림과 이름. 금빛이 왼쪽에서 오른쪽으로 훑는다
    샷2  2.0~5.4   울타리 — 말뚝마다 이름표. 안쪽은 볕 드는 풀밭, 바깥은 보랏빛 안개(그림자의 눈이 깜빡)
    샷3  4.8~8.2   구멍 — 가운데 이름표가 바래고, 앉아 있던 나비가 흩어지고, 말뚝이 쓰러진다
    샷4  7.6~11.0  그림자 — 구멍으로 미끄러져 들어와 딱정벌레에 겹친다. 벌레가 검게 바래고 그림자의 눈이 뜬다
    샷5 10.4~14.0  드러남 — 벽의 칸이 차례로 켜지는데 한 칸만 비어 있다. 그 빈칸이 옆으로 미끄러지고 눈을 뜬다
"""
import math

import numpy as np

from sound import hz as sound_hz
from kit import (INK, PAL, H, W, InsectSprite, Mask, NameWall, Particles, add, bake_layer, c, dot, put_layer, draw_insect,
                 draw_shadow, ease_in_out, ease_out, field_bg, finish_fence, grow, insect_parts, lerp, meadow, over,
                 paint_fence, paint_plate, plate_rect, post_pts, radial, rect_pts, rotate_pts, smooth, span, stamp, zoom)

DURS = [2.6, 2.8, 2.8, 2.8, 3.0]
TINT = "#fff8ec"
CUES = [
    (0.3, 2.3, "이름 벽은 사실 울타리였다."),
    (2.8, 2.5, "바깥의 그림자를 막는 울타리."),
    (5.4, 2.5, "이름이 바래면, 구멍이 난다."),
    (8.2, 2.6, "그림자는 그 구멍으로 숨어들었다."),
    (11.2, 2.6, "벽이 다 차자, 숨은 한 칸이 드러났다."),
]

ICON_KINDS = ["bfly", "beetle", "dfly"]
BLANK = (1, 3)   # (행, 열) — 끝까지 안 켜지는 칸. 세로 화면 가운데에 온다


def wall_shot(mode):
    """mode='sweep'(샷1 — 금빛이 훑는다) 또는 'reveal'(샷5 — 칸이 차례로 켜지고 빈칸이 움직인다)."""
    wall = NameWall(seed=401, kinds=ICON_KINDS)
    order = sorted(wall.icons, key=lambda rc: (rc[1], rc[0]))
    rank = {rc: i for i, rc in enumerate(order)}
    dust = Particles(50, 402, (0, 0, W, H), vel=((-3, 3), (-8, -2)), size=(0.6, 1.6))
    bx, by = wall.center(*BLANK)
    step = wall.cell + wall.gap

    def level(u, r, k):
        if mode == "sweep":
            cx, _ = wall.center(r, k)
            sx = lerp(-200, W + 200, span(u, 0.2, 2.4))
            return 0.35 + 0.65 * math.exp(-((cx - sx) / 170) ** 2) + 0.25 * smooth((sx - cx) / 150)
        i = rank[(r, k)]
        return 0.2 + 0.8 * ease_out(span(u, 0.05 + 1.2 * i / len(order), 0.35 + 1.2 * i / len(order)))

    def frame(u):
        f = wall.draw(lambda r, k: level(u, r, k))
        if mode == "reveal":
            slide = ease_in_out(span(u, 1.6, 2.4))
            wob = math.sin(u * 9) * 2.0 * span(u, 1.6, 2.4) * (1 - slide)
            x = bx + slide * step + wob
            eo = ease_out(span(u, 2.4, 2.85))
            wall.blank_cell(f, x, by, eyes=eo, blink=max(0.05, eo))
        dust.draw(f, u, c(PAL["rimlight"]), 0.35)
        if mode == "sweep":
            return zoom(f, 1.1 - 0.1 * ease_in_out(u / 2.6), W * 0.5, H * 0.48)
        zc = ease_in_out(span(u, 0.0, 3.0))
        return zoom(f, 1.0 + 0.22 * zc * zc, bx + step * 0.5, by)

    return frame


def fence_shot():
    """샷2 — 울타리 전경. 안쪽 풀밭엔 곤충, 바깥 안개엔 그림자의 눈."""
    horizon = H * 0.56
    img = field_bg(horizon, 511)
    base, ph, pw = H * 0.64, 200, 34
    posts = [float(x) for x in np.arange(-30, W + 60, 116)]

    def front(cv):   # 울타리·풀밭은 그림자 **앞**이다 — 그림자는 울타리 바깥에 산다
        paint_fence(cv, posts, base, pw, ph)
        for i, x in enumerate(posts):
            paint_plate(cv, x, base, pw, ph, ICON_KINDS[i % 3])
        meadow(cv, base - 6, 512)

    fence_layer = bake_layer(front)
    motes = Particles(40, 513, (0, H * 0.4, W, H), vel=((-4, 4), (-10, -3)), size=(0.8, 2.0))
    bfly = InsectSprite("bfly", 44, rot=0.5)
    dfly = InsectSprite("dfly", 50, rot=-1.45)

    def frame(u):
        f = img.copy()
        draw_shadow(f, 724, 296 - 12 * math.sin(u * 1.6), 0.52, u + 3.7, alpha=0.92, eyes=1.0)   # 울타리 너머에서 엿본다
        put_layer(f, *fence_layer)
        bfly.draw(f, lerp(W * 0.3, W * 0.46, u / 2.8), H * 0.33 + math.sin(u * 3.1) * 16, u, rate=14)
        dfly.draw(f, lerp(W * 0.86, W * 0.66, ease_in_out(u / 2.8)), H * 0.22 + math.sin(u * 2.2) * 8, u, rate=30)
        draw_insect(f, "beetle", W * 0.42 + 18 * u, H * 0.86, 26, rot=1.4)
        motes.draw(f, u, c(PAL["rimlight"]), 0.4)
        return zoom(f, 1.0 + 0.04 * u / 2.8, W * 0.5, H * 0.6)

    return frame


GAP_BASE, GAP_PH, GAP_PW = H * 0.9, 470, 72
GAP_POSTS = [W * 0.2, W * 0.5, W * 0.8]
GAP_KINDS = ["beetle", "bfly", "dfly"]


def gap_scene(fallen):
    """샷3·4의 정적 배경 — 말뚝 셋을 가까이. 서 있을 땐 가운데 이름표를 비워 둔다(샷이 매 프레임 그린다)."""
    img = field_bg(H * 0.5, 611)
    base, ph, pw, posts = GAP_BASE, GAP_PH, GAP_PW, GAP_POSTS
    if fallen:
        add(img, c(PAL["shadow_glow"]), radial(W, H, W * 0.5, H * 0.52, 260, 2.0) * 0.28)
        pm, rm = Mask(), Mask()
        for x in (posts[0], posts[2]):
            pm.poly(post_pts(x, base, pw, ph))
        for ry in (0.3, 0.74):
            y = base - ph * ry
            rm.poly([(posts[0], y - pw * 0.13), (posts[0] + 120, y - pw * 0.13 + 6), (posts[0] + 104, y + pw * 0.13),
                     (posts[0], y + pw * 0.13)])
            rm.poly([(posts[2], y - pw * 0.13), (posts[2] - 96, y - pw * 0.13 - 4), (posts[2] - 116, y + pw * 0.13),
                     (posts[2], y + pw * 0.13)])
        pm.poly([(W * 0.5 - pw / 2, base), (W * 0.5 - pw / 2, base - 46), (W * 0.5 - 6, base - 30),
                 (W * 0.5 + 8, base - 52), (W * 0.5 + pw / 2, base - 38), (W * 0.5 + pw / 2, base)])  # 부러진 밑동
        finish_fence(img, pm.arr(0.6), rm.arr(0.6))
    else:
        paint_fence(img, posts, base, pw, ph)
    for i in (0, 2):
        paint_plate(img, posts[i], base, pw, ph, GAP_KINDS[i])
    meadow(img, base - 4, 612, flowers=24)
    return img


def hole_shot():
    """샷3 — 가운데 이름표가 바래고, 나비가 흩어지고, 말뚝이 쓰러진다."""
    img = gap_scene(fallen=False)
    gap_img = gap_scene(fallen=True)
    base, ph, pw, cx = GAP_BASE, GAP_PH, GAP_PW, GAP_POSTS[1]
    sparks = np.random.default_rng(631).uniform(-1, 1, (40, 3))
    bfly = InsectSprite("bfly", 46)
    x0, y0, x1, y1 = plate_rect(cx, base, pw, ph)
    pcx, pcy = (x0 + x1) / 2, (y0 + y1) / 2
    crack = Mask(90, 30).line([(10, 15), (34, 9), (52, 18), (80, 11)], 3).arr(0.5)
    left_end, right_end = GAP_POSTS[0] + 112, GAP_POSTS[2] - 106   # 부러진 그루터기 끝(gap_scene과 맞춘다)

    def frame(u):
        fall = ease_in_out(span(u, 1.4, 2.25))
        lv = 1 - smooth(span(u, 0.3, 1.1))
        if fall > 0:
            # 말뚝은 바랜 이름표를 단 채 쓰러지고, 안쪽 가로대 토막은 제자리에서 기울며 떨어진다
            f = gap_img.copy()
            ang, dy, px, py = fall * 1.25, fall * 40, cx + pw / 2, base
            fade = 1 - span(u, 2.0, 2.4)
            pm, rm, plm = Mask(), Mask(), Mask()
            pm.poly(rotate_pts(post_pts(cx, base, pw, ph), px, py, ang, dy))
            drop = 330 * fall * fall
            for ry in (0.3, 0.74):
                y = base - ph * ry
                for xa, xb, sg in ((left_end, cx - pw / 2, 1), (cx + pw / 2, right_end, -1)):
                    rm.poly(rotate_pts(rect_pts(xa, y - pw * 0.13, xb, y + pw * 0.13), (xa + xb) / 2, y,
                                       sg * 0.45 * fall, drop))
            plm.poly(rotate_pts(rect_pts(*plate_rect(cx, base, pw, ph)), px, py, ang, dy))
            pa, ra, la = pm.arr(0.6) * fade, rm.arr(0.6) * fade, plm.arr(0.6) * fade
            over(f, c(INK), grow(np.maximum(np.maximum(pa, ra), la), 1.6))
            over(f, c(PAL["rail"]), ra)
            over(f, c(PAL["fence"]), pa)
            over(f, c("#bfb5a6"), la)
        else:
            f = img.copy()
            paint_plate(f, cx, base, pw, ph, "bfly", level=lv, icon_alpha=1 - smooth(span(u, 0.6, 1.2)))
            if u > 1.1:
                stamp(f, crack * span(u, 1.1, 1.3), pcx, pcy + 70, "#1a1210")
        ba = 1 - smooth(span(u, 0.8, 1.5))
        if ba > 0.01:
            bfly.draw(f, cx, base - ph - 34, u, rate=5, alpha=ba)
        if 0.8 < u < 2.4:
            k = span(u, 0.8, 2.4)
            for sx, sy, sr in sparks:
                dot(f, cx + sx * 60 + sx * 80 * k, base - ph - 34 - 120 * k * (0.5 + abs(sy)) + sy * 20, 2.5 + sr,
                    c(PAL["glow"]), 0.8 * (1 - k))
        shake = 6 * span(u, 2.15, 2.25) * (1 - span(u, 2.25, 2.6))
        return zoom(f, 1.03 + 0.03 * u / 2.8, W * 0.5 + shake * math.sin(u * 60), H * 0.6)

    return frame


def shadow_shot():
    """샷4 — 그림자가 구멍으로 들어와 딱정벌레에 겹친다. 벌레가 검게 바래고 그림자의 눈이 뜬다."""
    img = gap_scene(fallen=True)
    bx, by = W * 0.58, H * 0.66
    leaf = Mask()
    leaf.ellipse(bx, by + 40, 120, 34, -0.12)   # 자막 띠(아래 150px) 위에 둔다
    la = leaf.arr(0.8)
    over(img, c(INK), grow(la, 1.6))
    over(img, c("#4fbf3c"), la)
    beetle_p = insect_parts("beetle", 52, rot=-0.3)

    def frame(u):
        f = img.copy()
        slide = ease_in_out(span(u, 0.0, 1.6))
        sx, sy = lerp(W * 0.5, bx, slide), lerp(H * 0.5, by, slide)
        env = span(u, 1.6, 2.4)
        sc = lerp(0.45, 1.0, slide) * (1 - 0.75 * ease_in_out(env))
        drain = smooth(span(u, 1.7, 2.4))
        draw_insect(f, "beetle", bx, by, 52, rot=-0.3, drain=drain, parts=beetle_p,
                    shadow_eyes=ease_out(span(u, 2.2, 2.7)))
        draw_shadow(f, sx, sy, sc, u, alpha=1 - smooth(span(u, 2.0, 2.45)), eyes=1 - env)
        return zoom(f, 1.0 + 0.16 * ease_in_out(span(u, 0.8, 2.8)), lerp(W * 0.5, bx, 0.8), lerp(H * 0.6, by, 0.7))

    return frame


SHOTS = [lambda: wall_shot("sweep"), fence_shot, hole_shot, shadow_shot, lambda: wall_shot("reveal")]


def score(sc):
    """따뜻한 화음 → 어두운 화음. 종소리(이름), 부서짐(말뚝), 바람(그림자), 오르는 종(벽이 찬다), 쿵(빈칸의 눈)."""
    T = sc.total
    s = [sc.shot(k) for k in range(5)]
    sc.pad([130.81, 261.63, 329.63, 392.0], [(0, 0), (0.6, 1), (s[2] - 0.2, 1), (s[2] + 0.8, 0), (T, 0)], amp=0.07)
    sc.pad([65.41, 130.81, 155.56, 196.0], [(0, 0), (s[2], 0), (s[2] + 1.2, 0.85), (s[4], 0.85), (s[4] + 0.6, 0.5),
                                            (T - 1.2, 0.8), (T, 0)], amp=0.07, detune=0.004)
    sc.wind([(0, 0), (s[1], 0.0), (s[1] + 0.8, 0.5), (s[2] + 0.4, 0.3), (s[3], 0.6), (s[3] + 2.0, 0.4),
             (s[4], 0.1), (T, 0.0)], amp=0.04)
    for k, nt in enumerate(("C5", "E5", "G5", "C6", "G5")):          # 샷1 — 금빛이 훑는다
        sc.pluck(0.5 + k * 0.42, sound_hz(nt), 0.12, pan=-0.6 + k * 0.3, kind="bell")
    sc.blink(s[1] + 1.2)                                              # 샷2 — 바깥 그림자의 눈
    sc.boom(s[1] + 1.2, 70, 55, 1.2, 0.16)
    for k, nt in enumerate(("A5", "F5", "D5", "A4")):                 # 샷3 — 이름표가 바랜다
        sc.pluck(s[2] + 0.3 + k * 0.22, sound_hz(nt), 0.1, dur=1.0, pan=0.2, kind="bell")
    sc.noise(s[2] + 1.1, 0.25, 1500, 6000, 0.22, decay=14)            # 금이 간다
    sc.boom(s[2] + 2.15, 110, 45, 0.9, 0.5)                           # 말뚝이 쓰러진다
    sc.noise(s[2] + 2.15, 0.5, 80, 900, 0.28, decay=7)
    sc.whoosh(s[3] + 0.1, 1.6, 0.22, 300, 1500, pan_from=-0.2, pan_to=0.4)   # 샷4 — 그림자가 미끄러진다
    sc.boom(s[3] + 1.95, 160, 50, 0.6, 0.32)                          # 벌레에 겹친다
    sc.chime(s[3] + 2.25, sound_hz("A#5"), 0.08, dur=1.4, pan=-0.4)
    sc.chime(s[3] + 2.26, sound_hz("B5"), 0.08, dur=1.4, pan=0.4)
    scale = ("C5", "D5", "E5", "G5", "A5", "C6", "D6", "E6")
    for k in range(14):                                               # 샷5 — 칸이 차례로 켜진다
        nt = scale[k % 8]
        sc.pluck(s[4] + 0.1 + k * 0.09, sound_hz(nt) * (2 if k >= 8 else 1), 0.06, dur=0.9, pan=-0.7 + k * 0.1,
                 kind="bell")
    sc.noise(s[4] + 1.6, 0.8, 120, 700, 0.15, decay=2.5)              # 빈칸이 미끄러진다
    sc.boom(s[4] + 2.4, 90, 38, 1.2, 0.55)                            # 눈을 뜬다
    sc.blink(s[4] + 2.45, 0.12)
