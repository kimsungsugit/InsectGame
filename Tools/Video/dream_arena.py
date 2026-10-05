"""「챔피언의 꿈」 도입 영상 — 경기장 입장 8초. 꿈 프롤로그의 Opening 단계에서 틀린다.

    python -X utf8 Tools/Video/dream_arena.py                     # 영상(mp4) 인코딩
    python -X utf8 Tools/Video/dream_arena.py --preview 0.5,3.4   # 지정 시각의 프레임만 PNG로
    python -X utf8 Tools/Video/dream_arena.py --sheet             # 대표 시각 12장 컨택트 시트

산출: Assets/StreamingAssets/Video/dream_arena.mp4 (무음 — 소리는 dream_arena_audio.py가 만든 wav를 게임의
AudioSource가 튼다. 오프닝 프롤로그와 같은 방식이라 영상이 못 틀려도 소리·시계가 어긋나지 않는다).

    0.0 ~ 3.0   선수 입장 터널 — 어둠 속 끝에서 빛이 커지고, 그 빛을 향해 걷는 챔피언의 뒷모습. 2.4부터 빛이 차올라 흰 화면.
    3.0 ~ 7.3   경기장 — 흰빛이 걷히며 관중석 전체가 환호한다. 서치라이트가 훑고, 바닥의 빛무리 속에 챔피언과
                파트너(헤라클레스), 맞은편 상대(태고의 비천룡)가 선다. 카메라는 맞대결 쪽으로 밀고 들어간다.
    6.1         맞대결 — 서치라이트가 두 곤충에 모이고 한 번 번쩍한다(소리의 큰 타격과 같은 시각).
    7.2 ~ 8.0   암전. 이어서 게임이 타이틀 카드를 얹는다.

**시각의 단일 출처는 이 파일의 상수다.** `dream_arena_audio.py`가 모듈로 읽고, 게임의 `DreamPrologueData.IntroSeconds`가
같은 길이를 쓴다(`DreamPrologueTests`가 어긋남을 잡는다). 글씨는 굽지 않는다 — 카드는 게임이 얹는다.
피사체는 가운데 세로 안전영역(가로의 56%)에 둔다 — 세로 화면은 중앙 cover-crop이다.

numpy + PIL + ffmpeg만 쓴다(`silhouette_kit` 재사용).
"""
import math
import os
import subprocess
import sys
import time

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from silhouette_kit import *  # noqa: F401,F403,E402

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(ROOT, "Assets", "StreamingAssets", "Video", "dream_arena.mp4")
FF = os.environ.get("FFMPEG", "C:/Users/kss11/AppData/Local/Programs/Python/Python312/Scripts/ffmpeg.exe")
PREVIEW_DIR = os.environ.get("PREVIEW_DIR", os.path.join(ROOT, "Artifacts", "dream-arena-preview"))

# ── 타임라인 (오디오·게임과 공유) ──
T_TOTAL = 8.0
TUNNEL_END = 3.0          # 흰 화면의 정점 — 큰 '쿵'과 환호가 터지는 시각
WHITE_IN = (2.35, 3.0)    # 터널 끝 빛이 차오른다
WHITE_OUT = (3.0, 3.9)    # 흰빛이 걷히며 경기장이 드러난다
PUSH_IN = (3.0, 7.3)      # 카메라가 맞대결 쪽으로 밀고 들어간다
CONVERGE = (5.2, 6.0)     # 서치라이트가 두 곤충으로 모인다
FACEOFF = 6.1             # 맞대결 번쩍 + 큰 타격
FADE_OUT = (7.2, 8.0)
STEP_PERIOD = 0.52        # 터널 걸음 간격(걸음 소리가 같은 시각을 쓴다)
STEP_FIRST = 0.26

# 장면 배치(출력 픽셀). 가로 화면은 전부 보이고, 세로 화면(cover-crop)은 가운데 약 32%(x 437~843)만 보인다 —
# 그래서 맞대결 쌍(뿔 ↔ 머리)을 화면 한가운데에 둔다. 챔피언은 곁가지라 세로에선 잘려도 이야기가 선다.
DUEL_X = 640.0
POOL = (640.0, 612.0)
BEETLE = (470.0, 600.0, 86.0)       # x, 발끝 y, 크기
DRAGONFLY = (800.0, 452.0, 108.0)
CHAMP = (290.0, 712.0, 330.0)       # x, 발끝 y, 키


def ease_in(t):
    t = min(1.0, max(0.0, t))
    return t * t


def hash01(i, k):
    """결정적 의사난수 0..1 — 같은 (i, k)는 늘 같은 값이라 프레임 순서와 무관하다."""
    x = math.sin(i * 12.9898 + k * 78.233) * 43758.5453
    return x - math.floor(x)


# ══════════════════════════ 장면 A — 선수 입장 터널 ══════════════════════════

def make_tunnel():
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    nx = (xs - W / 2) / (W / 2)
    ny = (ys - H * 0.5) / (H / 2)
    # 아치형(초타원) 거리 — 터널 단면이 둥근 사각형처럼 보여야 벽·바닥·천장이 읽힌다
    d = np.maximum((np.abs(nx * 0.8) ** 4 + np.abs(ny) ** 4) ** 0.25, 1e-3)
    depth = 1.0 / (d + 0.10)
    grain = fbm(W, H, 5, (90, 30), (0.7, 0.3))
    wall_col = col("#3a2d66")
    floor_mask = (ys > H * 0.5).astype(np.float32)
    dust = Particles(46, 21, (0, 0, W, H), vel=((-6, 6), (-5, 5)), size=(0.8, 1.8), wobble=5.0)

    def frame(t):
        R = 0.075 * math.exp(0.95 * t)
        phase = depth * 3.2 + t * 3.0                  # 갈빗대가 바깥으로 날아간다(앞으로 걷는 느낌)
        rib = np.exp(-(((phase - np.floor(phase)) - 0.5) / 0.085) ** 2)
        halo = np.exp(-np.maximum(d - R * 0.6, 0.0) * 1.7 / max(R, 0.3))
        core = np.clip((R - d) / max(R * 0.4, 0.02), 0, 1)
        lit = 0.22 + 0.78 * halo
        f = wall_col[None, None, :] * (lit * (0.8 + 0.4 * grain) * (0.6 + 0.4 * (1 - rib)))[..., None]
        add(f, col("#ffb877"), rib * (0.18 + 0.82 * halo) * 0.95)         # 불빛에 선 갈빗대 모서리
        add(f, col("#ff9a52"), halo * (0.12 + 0.3 * floor_mask) * 0.55)   # 바닥에 번지는 빛
        f = np.clip(f, 0, 1)
        add(f, col("#fff0d2"), core)
        dust.draw(f, t, col("#ffd9a0"), 0.55 * float(np.clip(R * 2.0, 0.2, 1.0)))

        # 걷는 챔피언 — 빛을 향해 멀어진다(작아지고 올라간다). 빛이 커져서 실루엣이 흰빛에 먹힌다.
        u = span(t, 0.0, TUNNEL_END)
        h = lerp(238.0, 142.0, u)
        feet = lerp(528.0, 436.0, u)
        m = Mask()
        person(m, W / 2, feet, h, kind="kid", t=t, walk=t * (2 * math.pi / (STEP_PERIOD * 2)), face=0.0)
        a = m.arr(0.4)
        over(f, col("#05040b"), a)
        glow = 0.5 + 0.5 * span(t, 0.0, 2.6)
        add(f, col("#ffd49a"), rim(a, 3, 0, 1.8) * 0.55 * glow)
        add(f, col("#ffd49a"), rim(a, -3, 0, 1.8) * 0.55 * glow)

        w = smooth(span(t, *WHITE_IN))
        if w > 0:
            f = f * (1 - w) + col("#fff4dc")[None, None, :] * w
        return f

    return frame


# ══════════════════════════ 장면 B — 경기장 ══════════════════════════

def hercules_side(m, x, y, s, mirror=False, bob=0.0):
    """옆에서 본 헤라클레스장수풍뎅이 — 머리가 +x, 위로 길게 휜 뿔과 아래 짧은 뿔이 집게처럼 맞선다. y는 발끝."""
    def X(pts):
        return xf([(-px, py) for px, py in pts] if mirror else pts, x, y - 0.62 * s - bob, s, 0.0)

    m.ellipse(*X([(-0.18, 0.0)])[0], 0.84 * s, 0.5 * s, 0.0)                       # 딱지날개
    m.ellipse(*X([(0.6, -0.08)])[0], 0.4 * s, 0.4 * s, 0.0)                        # 앞가슴
    m.ellipse(*X([(1.02, 0.12)])[0], 0.21 * s, 0.18 * s, 0.0)                      # 머리
    m.taper(X(bezier((0.78, -0.36), (1.2, -1.05), (1.9, -1.12), (2.28, -0.55), 16)), 0.27 * s, 0.09 * s)   # 위 뿔(가슴에서)
    m.taper(X(bezier((1.14, 0.04), (1.5, -0.24), (1.86, -0.3), (2.14, -0.1), 12)), 0.18 * s, 0.07 * s)     # 아래 뿔(머리에서)
    for lx, spread in ((-0.62, -0.12), (0.0, 0.0), (0.58, 0.14)):                   # 다리
        m.line(X([(lx, 0.36), (lx + spread * 0.5, 0.6), (lx + spread, 0.74)]), 0.11 * s)
    return m


def dragonfly_side(m, x, y, s, flap=0.0):
    """옆에서 본 잠자리 — 머리가 -x, 날개 두 쌍이 위로 퍼진다. flap(-1..1)이 날갯짓 위상이다."""
    m.circle(x - 0.95 * s, y, 0.17 * s)                                            # 머리(겹눈)
    m.ellipse(x - 0.6 * s, y - 0.02 * s, 0.27 * s, 0.2 * s, 0.0)                   # 가슴
    m.taper([(x - 0.5 * s, y + 0.02 * s), (x + 0.45 * s, y + 0.07 * s), (x + 1.25 * s, y + 0.1 * s)], 0.13 * s, 0.04 * s)
    for k, (bx, a0, ln) in enumerate(((-0.66, -1.95, 1.3), (-0.5, -1.15, 1.2))):
        a = a0 + flap * (0.42 if k == 0 else -0.42)
        cx, cy = x + bx * s + math.cos(a) * ln * 0.5 * s, y - 0.08 * s + math.sin(a) * ln * 0.5 * s
        m.ellipse(cx, cy, ln * 0.5 * s, 0.1 * s, a, v=200)
    for lx in (-0.7, -0.55, -0.4):                                                  # 앞으로 모은 다리
        m.line([(x + lx * s, y + 0.12 * s), (x + (lx - 0.08) * s, y + 0.36 * s)], 0.04 * s)
    return m


def build_stands():
    """관중석 — 여섯 줄(먼 줄은 옅고 푸르다). 줄마다 정적 실루엣 + 흔들리는 팔 목록."""
    rng = np.random.default_rng(7)
    rows = []
    for r in range(6):
        yb = 270 + r * 30 + r * r * 0.9
        rad = 4.0 + r * 1.55
        step = rad * 2.25
        base = np.arange(-24, W + 24, step)
        xs = base + rng.uniform(-step * 0.3, step * 0.3, len(base))
        m = Mask()
        pts_top = []
        arms = []
        for x in xs:
            y = yb - 0.00009 * (x - W / 2) ** 2 + rng.uniform(-2.0, 2.0)
            m.circle(x, y, rad)
            m.ellipse(x, y + rad * 2.1, rad * 1.8, rad * 1.5)
            pts_top.append((x, y + rad * 1.2))
            if rng.random() < 0.42 and r >= 1:
                side = 1 if rng.random() < 0.5 else -1
                arms.append((x + side * rad * 1.3, y + rad * 1.6, side, rng.uniform(0, 6.28), rng.uniform(2.2, 4.2),
                             rad * rng.uniform(2.6, 4.2)))
        band = pts_top + [(W + 24, yb + 70), (-24, yb + 70)]
        m.poly(band)
        rows.append((m.arr(0.35), arms, rad))
    return rows


def make_arena():
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)

    # ── 하늘(경기장 지붕 아래 어둠 + 먼 조명 빛망울) ──
    bg = sky(W, H, [(0, "#0a0720"), (0.28, "#251a48"), (0.42, "#6b3d5a"), (0.52, "#d98450"), (0.62, "#ffb36e"), (1, "#3a2646")],
             seed=3, texture=0.03)
    for cx, cy, rr, c, a in ((180, 150, 260, "#ffd9a0", 0.55), (1100, 150, 260, "#ffd9a0", 0.55),
                             (640, 90, 330, "#a8c8ff", 0.28), (640, 400, 520, "#ff9a5a", 0.30)):
        add(bg, col(c), radial(W, H, cx, cy, rr, 2.0) * a)
    bg += bokeh(W, H, 9, 46, col("#ffe0b0"), 6, 24, 0.55)
    bg = np.clip(bg, 0, 1)

    # ── 관중석 ──
    rows = build_stands()
    row_cols = [haze(col("#4a3a74"), col("#0b0818"), r / 5.0) for r in range(6)]

    # ── 난간 벽 + 바닥 ──
    wall = Mask().rect(0, 492, W, 528).arr(0.3)
    floor_m = np.clip((ys - 524) / 6.0, 0, 1)
    floor_tex = fbm(W, H, 13, (140, 50), (0.7, 0.3))
    floor_col = col("#1b1430")[None, None, :] * (0.8 + 0.5 * floor_tex[..., None])
    pool = np.clip(1 - np.sqrt(((xs - POOL[0]) / 620) ** 2 + ((ys - POOL[1]) / 112) ** 2), 0, 1) ** 1.25
    ring = Mask().ellipse(POOL[0], POOL[1], 520, 96).ellipse(POOL[0], POOL[1], 511, 90, v=0).arr(0.8)
    ring_glow = blur_arr(ring, 4.0)

    # 파트너·상대·챔피언은 프레임마다 흔들려서 정적 마스크가 아니다
    vig = vignette(W, H, 0.5)
    confetti_a = Particles(70, 31, (0, -40, W, H + 40), vel=((-14, 14), (22, 58)), size=(1.4, 2.8), wobble=14.0, twinkle=0.6)
    confetti_b = Particles(50, 32, (0, -40, W, H + 40), vel=((-14, 14), (18, 48)), size=(1.4, 2.6), wobble=12.0, twinkle=0.6)
    motes = Particles(60, 33, (140, 380, 1160, 700), vel=((-5, 5), (-10, -3)), size=(0.8, 1.8), wobble=4.0)
    flash_spots = []
    rng = np.random.default_rng(41)
    for _ in range(64):
        r = rng.integers(0, 6)
        x = rng.uniform(40, W - 40)
        y = 270 + r * 30 + r * r * 0.9 - 0.00009 * (x - W / 2) ** 2 - rng.uniform(0, 4)
        flash_spots.append((x, y, 4 + r * 1.6))

    beams = (  # (광원 x, 기본 각도(라디안, 0=아래), 흔들림 폭, 위상, 색, 수렴 목표)
        (150, -0.12, 0.34, 0.0, "#ffe2a6", "beetle"),
        (470, 0.0, 0.30, 1.7, "#a8d4ff", "beetle"),
        (830, 0.0, 0.30, 3.1, "#ffe2a6", "dragon"),
        (1130, 0.12, 0.34, 4.6, "#a8d4ff", "dragon"),
    )

    def beam_mask(t, converge):
        warm = Mask(ss=1)
        cool = Mask(ss=1)
        for sx, base, amp, ph, color, tgt in beams:
            sway = base + amp * math.sin(t * 0.85 + ph)
            tx = sx + math.tan(sway) * 760
            ty = 760.0
            goal = (BEETLE[0], BEETLE[1] - 60) if tgt == "beetle" else (DRAGONFLY[0], DRAGONFLY[1])
            tx = lerp(tx, goal[0] + (sx - W / 2) * 0.05, converge)
            ty = lerp(ty, goal[1], converge) if converge > 0 else ty
            dx, dy = tx - sx, ty - (-20.0)
            ln = math.hypot(dx, dy)
            px, py = -dy / ln, dx / ln
            far = 1.0 + 0.9 * (1.0 - converge * 0.55)      # 모일수록 빛줄기가 가늘어진다
            wd = 34 * far
            tip = lerp(1.45, 1.0, converge)                  # 모이면 목표에서 멎는다(뚫고 나가지 않는다)
            ex, ey = sx + dx * tip, -20.0 + dy * tip
            poly = [(sx - px * 7, -20.0 - py * 7), (sx + px * 7, -20.0 + py * 7),
                    (ex + px * wd, ey + py * wd), (ex - px * wd, ey - py * wd)]
            (warm if color == "#ffe2a6" else cool).poly(poly)
        return blur_arr(warm.arr(), 9.0), blur_arr(cool.arr(), 9.0)

    def frame(t):
        f = bg.copy()
        roar = smooth(span(t, 3.0, 3.35))                         # 환호가 터진 뒤
        conv = smooth(span(t, *CONVERGE))
        face = math.exp(-max(0.0, t - FACEOFF) * 6.0) if t >= FACEOFF else 0.0

        # 관중석 — 먼 줄부터. 환호 뒤엔 윗면에 조명 테두리가 선다.
        for r, (a, arms, rad) in enumerate(rows):
            over(f, row_cols[r], a)
            if roar > 0:
                add(f, col("#ffcf8a"), rim(a, 0, -2.2, 1.4) * (0.55 * roar) * (1.0 - r * 0.1))
            if arms and roar > 0:
                am = Mask(ss=1)
                lift = roar
                for (ax, ay, side, ph, sp, ln) in arms:
                    sway = math.sin(t * sp + ph) * rad * 0.9
                    am.line([(ax, ay), (ax + side * rad * 0.6 + sway * 0.5, ay - ln * 0.55 * lift),
                             (ax + side * rad * 0.9 + sway, ay - ln * lift)], max(1.6, rad * 0.42), caps=True)
                aa = am.arr(0.3)
                over(f, row_cols[r], aa)

        # 카메라 플래시 — 환호 뒤에 객석 곳곳에서 번쩍인다
        if roar > 0:
            k = int(t * 11)
            for i, (fx, fy, fr) in enumerate(flash_spots):
                if hash01(i, k) < 0.12 * roar + 0.05 * face:
                    life = 1.0 - ((t * 11) - k)
                    dot(f, fx, fy, fr * 3.2, col("#fff6e0"), 0.55 * life)
                    dot(f, fx, fy, fr * 1.2, col("#ffffff"), 0.9 * life)

        # 난간 + LED 띠
        over(f, col("#0c0816"), wall)
        led = Mask(ss=1).rect(0, 497, W, 500).arr(0.6)
        pulse = 0.65 + 0.35 * math.sin(t * 3.0)
        add(f, col("#ff7a3a"), led * (0.5 + 0.5 * roar) * pulse)
        add(f, col("#ff7a3a"), blur_arr(led, 5.0) * 0.5 * roar)

        # 바닥 + 빛무리
        over(f, floor_col, floor_m)
        add(f, col("#ffd9a0"), pool * floor_m * (0.8 + 0.25 * roar + 0.3 * face))
        add(f, col("#ffb470"), ring_glow * (0.35 + 0.35 * roar))
        add(f, col("#ffe4b8"), ring * (0.35 + 0.35 * roar))

        # 서치라이트(가산) — 객석·바닥 위를 훑다가 맞대결 쪽으로 모인다
        warm, cool = beam_mask(t, conv)
        grad = np.clip(1.15 - ys / (H * 1.25), 0.15, 1.0)
        amount = (0.24 + 0.2 * roar + 0.12 * conv + 0.22 * face) * smooth(span(t, 3.0, 3.5))
        add(f, col("#ffe2a6"), warm * grad * amount)
        add(f, col("#a8d4ff"), cool * grad * amount * 0.9)

        # 상대 뒤 후광 — 어두운 객석 앞에서도 실루엣이 읽히도록 빛이 모인 자리
        dx, dy, ds = DRAGONFLY
        hover = math.sin(t * 2.6) * 7.0
        add(f, col("#ffe6b8"), radial(W, H, dx, dy + hover, 230, 1.7) * (0.55 + 0.4 * conv + 0.5 * face))
        add(f, col("#ffffff"), radial(W, H, dx, dy + hover, 90, 1.5) * (0.25 + 0.4 * conv))
        add(f, col("#ffe6b8"), radial(W, H, BEETLE[0] + 40, BEETLE[1] - 70, 240, 1.7) * (0.35 * conv + 0.4 * face))

        # 입자 — 색종이·먼지
        confetti_a.draw(f, t, col("#ffd36a"), 0.9 * roar, 0, 0)
        confetti_b.draw(f, t, col("#9fd0ff"), 0.8 * roar, 0, 0)

        # 실루엣 — 파트너·상대·챔피언
        bx, by, bs = BEETLE
        bm = Mask()
        hercules_side(bm, bx, by, bs, bob=math.sin(t * 2.2) * 2.0 + 6.0 * face)
        ba = bm.arr(0.35)
        over(f, col("#06040c"), ba)
        add(f, col("#ffe0a8"), rim(ba, 2, -2, 1.6) * 0.5)

        fm = Mask()
        dragonfly_side(fm, dx, dy + hover, ds, flap=math.sin(t * 19.0) * 0.9)
        fa = fm.arr(0.35)
        over(f, col("#08050e"), fa)

        cx, cf, ch = CHAMP
        raise_arm = smooth(span(t, FACEOFF - 0.5, FACEOFF - 0.1))
        arm = (cx + ch * 0.13 + raise_arm * ch * 0.36, cf - ch * 0.45 - raise_arm * ch * 0.5) if raise_arm > 0 else None
        pm = Mask()
        person(pm, cx, cf, ch, kind="kid", t=t, face=0.35, arm=arm)
        pa = pm.arr(0.4)
        over(f, col("#04030a"), pa)
        add(f, col("#ffcf8a"), rim(pa, 3, -1, 2.0) * 0.5)

        motes.draw(f, t, col("#ffe0b0"), 0.5 * roar)

        # 맞대결 번쩍 — 한 번 크게, 빠르게 걷힌다
        if face > 0.001:
            f = f + face * 0.22
        # 카메라: 맞대결 쪽으로 밀고 들어간다 + 맞대결 순간 흔들림
        z = lerp(1.0, 1.26, ease_in_out(span(t, *PUSH_IN)))
        f = np.clip(f * vig, 0, 1)
        # 맞대결 흔들림은 확대 중심을 흔들어 만든다 — 프레임을 굴리면(np.roll) 반대편 가장자리가 말려 들어온다.
        # 중심 이동은 화면에서 약 1/5로 줄어 보이므로(1 - 1/z) 크게 준다.
        shake_x = math.sin(t * 90.0) * 38.0 * face
        shake_y = math.cos(t * 77.0) * 26.0 * face
        f = zoom(f, z, DUEL_X + shake_x, 470.0 + shake_y)

        # 흰빛이 걷힌다
        w = 1.0 - smooth(span(t, *WHITE_OUT))
        if w > 0:
            f = f * (1 - w) + col("#fff4dc")[None, None, :] * w
        return f

    return frame


# ══════════════════════════ 합성 ══════════════════════════

def build():
    tunnel = None
    arena = None

    def frame_at(t):
        nonlocal tunnel, arena
        if t < TUNNEL_END:
            if tunnel is None:
                tunnel = make_tunnel()
            f = tunnel(t)
        else:
            if tunnel is not None:
                tunnel = None
            if arena is None:
                arena = make_arena()
            f = arena(t)
        if t < 0.35:
            f = f * smooth(t / 0.35)
        if t > FADE_OUT[0]:
            f = f * (1 - smooth(span(t, *FADE_OUT)))
        return np.clip(f, 0, 1)

    return frame_at


def to_u8(f):
    return (np.clip(f, 0, 1) * 255 + 0.5).astype(np.uint8)


def render(times=None):
    frame_at = build()
    if times is not None:
        os.makedirs(PREVIEW_DIR, exist_ok=True)
        for t in times:
            path = os.path.join(PREVIEW_DIR, f"dream-arena-{t:05.2f}.png")
            Image.fromarray(to_u8(frame_at(t)), "RGB").save(path)
            print("preview", path)
        return

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    cmd = [FF, "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS),
           "-i", "-", "-vf", "noise=alls=5:allf=t,setsar=1", "-an", "-c:v", "libx264", "-profile:v", "main",
           "-level", "4.0", "-pix_fmt", "yuv420p", "-b:v", "1800k", "-maxrate", "2000k", "-bufsize", "4000k",
           "-movflags", "+faststart", OUT]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    n = int(round(T_TOTAL * FPS))
    t0 = time.time()
    for i in range(n):
        proc.stdin.write(to_u8(frame_at(i / FPS)).tobytes())
        if i % 24 == 0:
            print(f"  {i}/{n} ({time.time() - t0:.0f}s)", flush=True)
    proc.stdin.close()
    rc = proc.wait()
    print(f"dream_arena.mp4 {T_TOTAL:.1f}s {n}f {os.path.getsize(OUT) / 1e6:.2f}MB rc={rc} ({time.time() - t0:.0f}s)")


def sheet():
    """대표 시각 12장 — 3열 4행 컨택트 시트."""
    times = [0.4, 1.2, 2.0, 2.7, 3.1, 3.5, 4.2, 5.0, 5.8, 6.15, 6.8, 7.6]
    frame_at = build()
    tw, th = 640, 360
    img = Image.new("RGB", (tw * 3, th * 4))
    for i, t in enumerate(times):
        tile = Image.fromarray(to_u8(frame_at(t)), "RGB").resize((tw, th), Image.Resampling.LANCZOS)
        img.paste(tile, ((i % 3) * tw, (i // 3) * th))
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    path = os.path.join(PREVIEW_DIR, "dream-arena-sheet.png")
    img.save(path)
    print("sheet", path)


def main():
    args = sys.argv[1:]
    if "--sheet" in args:
        sheet()
    elif "--preview" in args:
        render([float(v) for v in args[args.index("--preview") + 1].split(",")])
    else:
        render()


if __name__ == "__main__":
    main()
