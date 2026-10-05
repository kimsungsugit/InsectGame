"""ch5 「산꼭대기」 — ch5_summit 대사 **뒤**에 튼다. 새벽 산(보라 → 금빛).

직전 대사: 라온 "지나온 데가 다 보여" · 세라 "안개 너머로 빛나는 게 고대 유적 … 이제 마지막 장만 남았어요".

    샷1  0.0~3.0   능선 위 세 사람 뒷모습(라온·주인공·세라) — 바람에 머리카락, 발아래 멀리 지나온 초원·연못·숲길
    샷2  2.4~6.0   발아래 구름 바다가 천천히 갈라진다 — 골짜기 바닥에 희미한 금빛
    샷3  5.4~9.5   안개 아래 골짜기에 고대 유적이 금빛으로 빛난다(빛이 차오른다)
    샷4  8.9~12.0  유적으로 아주 느린 줌 — 들보의 이름 무늬와 문간의 빛
"""
import math

import numpy as np

from kit import (PAL, H, W, Mask, Particles, add, book_sky, c, cloud, dot, draw_person, ease_in_out, fbm, grow,
                 mist, over, paint, person_parts, radial, rays_texture, ridge, rim, smooth, span, sparkle, sun, zoom)
from silhouette_kit import peaks, streaks
import bk_v2 as bk
from sound import hz

DURS = [3.0, 3.0, 3.5, 2.5]
TINT = "#fff6ee"
REVERB = 0.35
CUES = [
    (1.0, 2.6, "지나온 길이 모두 내려다보였다."),
    (5.6, 2.8, "안개 너머로 고대 유적이 빛난다."),
    (9.0, 2.6, "곤충 이름의 비밀이 저기 있다."),
]

SKY = [(0, "#7466bd"), (0.3, "#ad8ad0"), (0.56, "#f2b2b8"), (0.78, "#ffd59e"), (1, "#fff0c8")]
FAR1, FAR2, FAR3 = "#b7a6df", "#9a88cf", "#7f6fb8"
ROCK, ROCK_LIT = "#8e7aa8", "#f3c48a"


def _range(img, seed, y, amp, fill, ink="#6a5a96", line=1.2, rough=0.55):
    m = peaks(Mask(), seed, y, amp, rough=rough)
    a = m.arr(0.8)
    paint(img, a, fill, ink=ink, line=line, hi=0.0, lo=0.0)
    add(img, c(ROCK_LIT), rim(a, -4, 3, 4) * 0.35)   # 해 쪽(오른쪽) 능선에 금빛 테
    return a


def _haze(img, y0, y1, color="#f6c6c8", amount=0.5):
    """아래로 갈수록 옅어지는 공기 원근(위 가장자리는 부드럽게)."""
    yy = np.arange(H, dtype=np.float32)[:, None]
    band = np.clip((y1 - yy) / max(1, y1 - y0), 0, 1) * np.clip((yy - (y0 - 60)) / 60, 0, 1)
    over(img, c(color), (band ** 1.2 * amount) * np.ones((1, W), np.float32))


# ══════════════════════════ 샷1 — 능선 위 세 사람 ══════════════════════════

def ridge_shot():
    img = book_sky(SKY, seed=501)
    sun(img, 1010, 286, r=40, halo=0.55)
    cloud(img, 250, 110, 0.6, color="#fbe6f0", ink="#b9a2d6", seed=3)
    cloud(img, 860, 150, 0.45, color="#fbe6f0", ink="#b9a2d6", seed=4)
    _range(img, 502, 300, 70, FAR1, ink="#8a7ab8")
    _range(img, 503, 336, 55, FAR2)
    # 발아래 멀리 — 지나온 초원·연못·숲(아주 작게, 아침 안개에 싸여 저 아래에)
    land = ridge(Mask(), 504, 345, 6, freq=(0.004, 0.01)).arr(1.0)
    over(img, bk.vgrad(H, W, [(0, "#b7d28f"), (0.6, "#93bf76"), (1, "#7aa866")]), land)
    hills = Mask()
    for x, y, rx, ry in ((150, 392, 170, 34), (380, 380, 120, 24), (1110, 400, 190, 40), (860, 420, 110, 22)):
        hills.ellipse(x, y, rx, ry)
    ha = hills.arr(1.0) * land
    over(img, c("#a3cf80"), ha)
    add(img, c("#ffe6b0"), rim(ha, -2, 2, 2) * 0.3)
    forest = Mask()
    rr = np.random.default_rng(505)
    for _ in range(30):
        forest.circle(rr.uniform(930, 1200), rr.uniform(360, 378), rr.uniform(6, 10))
    for _ in range(16):
        forest.circle(rr.uniform(90, 260), rr.uniform(362, 376), rr.uniform(5, 9))
    paint(img, forest.arr(0.6), "#5f9a52", ink="#4a7a52", line=0.8, hi=0.1, lo=0.0)
    pond = Mask()
    pond.ellipse(800, 372, 58, 12)
    pa = pond.arr(0.6)
    paint(img, pa, "#86c8ea", ink="#5a8eae", line=0.8, hi=0.0, lo=0.0)
    add(img, c("#fff2d8"), rim(pa, 0, 2, 2) * 0.5)
    path = Mask()
    path.line([(560, 470), (620, 440), (520, 418), (640, 396), (760, 384), (900, 372)], 3)
    path.line([(560, 470), (420, 430), (300, 395), (200, 372)], 3)
    over(img, c("#f3e3c0"), path.arr(0.6) * land * 0.85)
    _haze(img, 330, 560, "#f3c4cc", 0.5)
    for k, (x, y, sc_) in enumerate(((170, 470, 0.9), (1100, 486, 1.05), (400, 520, 0.7), (930, 548, 0.85),
                                     (60, 560, 0.8))):
        cloud(img, x, y, sc_, color="#fdf0f4", ink="#c6aedd", seed=510 + k)   # 발아래 구름 — 높이 올라왔다
    # 산꼭대기(바위) — 가운데만 솟고 양옆은 가파르게 떨어진다
    def top_y(x):
        d = (x - 640) / 330
        return 500 + 34 * d * d + (170 * (abs(d) - 1) ** 1.2 if abs(d) > 1 else 0) + 4 * math.sin(x * 0.05)

    rm = Mask()
    pts = [(x, top_y(x)) for x in range(-20, W + 40, 16)]
    rm.poly(pts + [(W + 20, H + 10), (-20, H + 10)])
    ra = rm.arr(0.6)
    n = fbm(W, H, 506, (90, 30), (0.7, 0.3))
    rock = bk.vgrad(H, W, [(0, "#a08bb8"), (0.75, "#836f9e"), (1, "#5f4f7a")]) * (0.9 + 0.2 * n)[..., None]
    over(img, c("#4a3a5a"), grow(ra, 1.6))
    over(img, rock, ra)
    add(img, c(ROCK_LIT), rim(ra, 0, 5, 5) * 0.6)
    cracks = Mask()
    for x0, y0, x1, y1 in ((330, 600, 380, 680), (960, 610, 1010, 690), (450, 560, 480, 610), (820, 570, 850, 620)):
        cracks.line([(x0, y0), ((x0 + x1) / 2 + 8, (y0 + y1) / 2), (x1, y1)], 2.2)
    over(img, c("#4a3a5a"), cracks.arr(0.6) * 0.6)
    gr = Mask()
    rg = np.random.default_rng(507)
    for _ in range(18):
        x = rg.uniform(330, 950)
        if 490 < x < 790:
            continue
        y = top_y(x) + 3
        for j in range(3):
            gr.taper([(x + j * 5, y + 4), (x + j * 5 + 8 + j * 3, y - 18 - j * 4)], 3.0, 1.0)
    paint(img, gr.arr(0.5), "#8fb070", ink="#4a3a5a", line=1.0, hi=0.1, lo=0.0)

    def feet(x):
        return top_y(x) + 2

    for x in (548, 640, 734):
        sh = Mask()
        sh.ellipse(x, feet(x) + 2, 30, 6)
        over(img, c("#3c2e4a"), sh.arr(3) * 0.35)
    draw_person(img, "raon", 548, feet(548), 162)
    draw_person(img, "hero", 640, feet(640), 168)
    # 세라는 머리카락·코트가 바람에 날린다 — 위상 몇 개를 미리 굽는다
    period = 2 * math.pi / 2.2
    sera = [person_parts("sera", 196, t=period * k / 12) for k in range(12)]

    def frame(u):
        f = img.copy()
        k = int((u / period) * 12) % 12
        draw_person(f, "sera", 734, feet(734), 196, parts=sera[k])
        streaks(f, u, 508, 14, c("#fff6ec"), 0.35, vx=520.0, length=60.0, y0=120, y1=520)
        return zoom(f, 1.0 + 0.05 * ease_in_out(u / 3.0), 640, 400)

    return frame


# ══════════════════════════ 샷2 — 구름 바다가 갈라진다 ══════════════════════════

def clouds_shot():
    hz_y = 236
    img = book_sky(SKY, seed=521, h=H)
    sun(img, 990, 210, r=36, halo=0.5)
    _range(img, 522, hz_y + 20, 50, FAR1, ink="#8a7ab8")
    # 구름 아래 골짜기(갈라지면 보인다)
    valley = bk.vgrad(H, W, [(0, "#8d86c4"), (0.5, "#5f6aa8"), (1, "#3f4f86")])
    vm = Mask()
    vm.rect(0, hz_y + 60, W, H)
    va = vm.arr(2)
    over(img, valley, va)
    for k, (y, amp, fill) in enumerate(((470, 22, "#8c8cc0"), (530, 26, "#6f86a6"), (600, 30, "#55786e"))):
        ra = ridge(Mask(), 530 + k, y, amp, freq=(0.006, 0.017)).arr(0.8)
        over(img, c("#3a3f6a"), np.clip(grow(ra, 1.2) - ra, 0, 1) * 0.8)
        over(img, c(fill), ra)
        add(img, c(ROCK_LIT), rim(ra, -2, 3, 3) * 0.2)
    river = Mask()
    river.line([(640, H), (600, 670), (676, 610), (628, 555), (652, 505)], 7)
    over(img, c("#b9d2f2"), river.arr(0.8) * 0.85)
    mist(img, 529, 0.0, 470, 620, "#c9c2ea", 0.35, speed=0)
    add(img, c(PAL["glow"]), radial(W, H, 640, 560, 220, 1.6) * 0.6)
    back = bk.cloud_mass(W + 200, 300, 523, n=34, rmin=36, rmax=80, top=0.18)
    back = (back[0], back[1] * np.clip(np.linspace(1.9, -0.1, 300)[:, None], 0, 1))   # 아래는 안개처럼 풀린다
    left = bk.cloud_mass(900, 520, 524, n=24, rmin=50, rmax=110, top=0.18, open_side="right")
    right = bk.cloud_mass(900, 520, 525, n=24, rmin=50, rmax=110, top=0.2, open_side="left")
    wisp_l = bk.cloud_mass(520, 260, 526, n=12, rmin=40, rmax=90, top=0.3, open_side="right",
                           colors=("#fffaf6", "#f0e2f6", "#cbbbe8"))
    wisp_r = bk.cloud_mass(520, 260, 527, n=12, rmin=40, rmax=90, top=0.3, open_side="left",
                           colors=("#fffaf6", "#f0e2f6", "#cbbbe8"))

    def frame(u):
        f = img.copy()
        e = ease_in_out(span(u, 0.3, 3.4))
        bk.paste(f, *back, -100 - 60 * e, hz_y - 6)
        bk.paste(f, *left, -230 - 330 * e, hz_y + 50)
        bk.paste(f, *right, 610 + 330 * e, hz_y + 46)
        bk.paste(f, *wisp_l, -120 - 520 * e, 470)
        bk.paste(f, *wisp_r, 880 + 520 * e, 480)
        g = smooth(span(u, 1.4, 3.4))
        if g > 0:
            for j in range(4):
                tw = 0.5 + 0.5 * math.sin(u * 4 + j * 1.7)
                dot(f, 640 + (j - 1.5) * 26, 515 + (j % 2) * 10, 3, c("#fff3b8"), 0.8 * g * tw)
        streaks(f, u, 528, 10, c("#ffffff"), 0.3, vx=420.0, length=70.0, y0=120, y1=360)
        return zoom(f, 1.0 + 0.03 * u / 3.6, 640, 420)

    return frame


# ══════════════════════════ 샷3·4 — 금빛 유적 ══════════════════════════

def _valley(seed, ruin_w, ruin_h, base, sky_h, slopes):
    img = book_sky([(0, "#f0b6c0"), (0.5, "#ffd9a2"), (1, "#fff1cc")], seed=seed)
    _range(img, seed + 1, sky_h, 40, FAR1, ink="#8a7ab8")
    gm = Mask()
    gm.rect(0, sky_h + 30, W, H)
    over(img, bk.vgrad(H, W, [(0, "#9c94c8"), (0.55, "#6f7fae"), (1, "#4f6a8a")]), gm.arr(4))
    n = fbm(W, H, seed + 2, (120, 40), (0.7, 0.3))
    for k, (pts, fill) in enumerate(slopes):
        sm = Mask()
        sm.poly(pts)
        a = sm.arr(0.8)
        over(img, c("#3f3a70"), grow(a, 1.3))
        over(img, c(fill) * (0.88 + 0.24 * n)[..., None], a)
        add(img, c(ROCK_LIT), rim(a, -3, 3, 3) * 0.3)
    add(img, c(PAL["glow"]), radial(W, H, 640, base - ruin_h * 0.4, ruin_w * 1.1, 1.4) * 0.35)
    pm = Mask()   # 유적이 선 둔덕 — 골짜기 바닥으로 완만하게 흘러내린다
    pts = []
    for i in range(41):
        t = i / 40
        x = 640 + (t - 0.5) * ruin_w * 3.2
        d = abs(t - 0.5) * 2
        y = base - 6 + (0 if d < 0.36 else ((d - 0.36) / 0.64) ** 1.6 * (H - base + 60)) + 3 * math.sin(i * 1.7)
        pts.append((x, y))
    pm.poly(pts + [(640 + ruin_w * 1.6, H + 10), (640 - ruin_w * 1.6, H + 10)])
    pa = pm.arr(0.8)
    over(img, c("#3a4f5a"), grow(pa, 1.3))
    over(img, bk.vgrad(H, W, [(0, "#7fa486"), (0.7, "#5c8070"), (1, "#45665a")]), pa)
    add(img, c(PAL["glow"]), rim(pa, 0, 4, 4) * 0.5)
    return img


def ruin_shot(close):
    if close:
        rw, rh, base = 560, 430, 568
        slopes = [([(0, 160), (180, 260), (330, 560), (360, H), (0, H)], "#7a6cb6"),
                  ([(W, 150), (1100, 250), (950, 560), (920, H), (W, H)], "#7a6cb6")]
        img = _valley(541, rw, rh, base, 150, slopes)
    else:
        rw, rh, base = 300, 232, 452
        slopes = [([(0, 220), (300, 300), (520, 520), (560, H), (0, H)], "#8577bf"),
                  ([(W, 200), (1000, 290), (760, 520), (720, H), (W, H)], "#8577bf"),
                  ([(0, 420), (260, 470), (420, H), (0, H)], "#6a5ea6"),
                  ([(W, 410), (1020, 470), (860, H), (W, H)], "#6a5ea6")]
        img = _valley(531, rw, rh, base, 196, slopes)
    cx = 640
    lit_img = img.copy()
    bk.ruin(img, cx, base, rw, rh, glow=0.25, glyph=0.15, door_glow=0.2, seed=55)
    bk.ruin(lit_img, cx, base, rw, rh, glow=1.0, glyph=1.0, door_glow=1.0, seed=55)
    rays = rays_texture(W, H, cx, base - rh * 0.55, count=12, seed=533, spread=1.6, center=-math.pi / 2, reach=0.9)
    halo = radial(W, H, cx, base - rh * 0.45, rw * 1.2, 1.6)
    motes = Particles(36, 534 + close, (cx - rw * 0.7, base - rh * 1.3, cx + rw * 0.7, base + 40),
                      vel=((-4, 4), (-22, -8)), size=(0.8, 1.8))
    mist_seed = 535 + close

    def frame(u):
        if close:
            g = 0.85 + 0.15 * math.sin(u * 2.2)
        else:
            g = smooth(span(u, 0.3, 2.2)) * (0.9 + 0.1 * math.sin(u * 2.5))
        f = img + (lit_img - img) * min(1.0, g * 1.05)
        add(f, c(PAL["glow"]), halo * 0.4 * g)
        add(f, c("#fff3c4"), rays * 0.3 * g)
        mist(f, mist_seed, u, base - (40 if close else 26), base + (60 if close else 50), "#f4e8f6",
             0.45 if close else 0.55, speed=14)
        mist(f, mist_seed + 7, u * 0.7, base - rh * 0.25, base + 10, "#efe2f2", 0.22, speed=-10)
        motes.draw(f, u, c("#fff1b0"), 0.7 * g)
        for j in range(5):
            tw = max(0.0, math.sin(u * 2.6 + j * 2.1))
            if tw > 0.3:
                sparkle(f, cx + math.sin(j * 2.3) * rw * 0.45, base - rh * (0.35 + 0.4 * ((j * 0.37) % 1)),
                        7 + 5 * tw, amount=g * tw)
        if close:
            return zoom(f, 1.0 + 0.12 * ease_in_out(u / 3.1), cx, base - rh * 0.45)
        return zoom(f, 1.0 + 0.06 * ease_in_out(u / 4.1), cx, base - rh * 0.4)

    return frame


SHOTS = [ridge_shot, clouds_shot, lambda: ruin_shot(0), lambda: ruin_shot(1)]


def score(sc):
    """높은 바람 · 구름이 갈라질 때 넓어지는 패드 · 유적 빛에 종 몇 개 + 이름의 동기."""
    T = sc.total
    s = [sc.shot(k) for k in range(4)]
    sc.wind([(0, 0.0), (0.5, 1.0), (s[1] + 1.5, 1.0), (s[2], 0.6), (s[3], 0.4), (T, 0.3)], amp=0.06, lo=500, hi=2600,
            gust=0.7)
    sc.wind([(0, 0.6), (T, 0.4)], amp=0.03, lo=150, hi=500, gust=0.3)
    sc.whoosh(0.6, 1.6, 0.05, 900, 4000, pan_from=-0.7, pan_to=0.7)
    sc.whoosh(s[1] + 0.3, 3.0, 0.06, 200, 1300, pan_from=0.0, pan_to=0.0)
    # 갈라질 때 넓어지는 패드(D장조 add9)
    sc.pad([hz("D3"), hz("A3"), hz("D4")], [(0, 0), (1.0, 0.6), (s[1], 0.6), (s[2], 0.9), (T, 0.7)], amp=0.03)
    sc.pad([hz("E4"), hz("F#4"), hz("A4"), hz("D5")], [(0, 0), (s[1] + 0.3, 0), (s[1] + 2.8, 0.8), (s[3], 0.7),
                                                      (T, 0.6)], amp=0.022, bright=0.4, pan_spread=0.9)
    sc.pluck(1.2, hz("A4"), 0.05, dur=2.0, pan=-0.3, kind="bell")
    sc.pluck(2.1, hz("D5"), 0.05, dur=2.0, pan=0.3, kind="bell")
    # 유적이 빛난다 — 종 몇 개 + 이름의 동기
    for k, (dt, nt) in enumerate(((0.6, "A5"), (1.1, "D6"), (1.5, "F#6"))):
        sc.chime(s[2] + dt, hz(nt), 0.06, dur=2.0, pan=-0.4 + k * 0.4)
    sc.sparkle(s[2] + 0.8, 1.6, density=8, amp=0.03)
    bk.name_motif(sc, s[2] + 2.0, amp=0.13)
    sc.sparkle(s[3] + 0.6, 2.0, density=6, amp=0.025)
    for nt in ("D5", "F#5", "A5"):
        sc.pluck(s[3] + 1.4, hz(nt), 0.05, dur=2.2, kind="bell")
