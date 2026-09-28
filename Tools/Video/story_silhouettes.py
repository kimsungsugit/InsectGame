"""스토리 영상 11편 렌더러 — 실루엣 삽화 화풍(Docs/StoryVideos.md §3·§4의 샷 리스트 그대로).

    python -X utf8 Tools/Video/story_silhouettes.py [이름,...] [--preview 0.5,3.0,...]

산출: Assets/StreamingAssets/Video/<이름>.mp4 (1280×720 · 24fps · H.264 Main · ≤2 Mbps · 무음 · +faststart).
--preview면 영상 대신 지정 시각(영상 전체 기준 초)의 프레임을 Artifacts/story-silhouettes-preview/에 PNG로 떨군다.

**길이와 샷 경계는 문서 §4 표와 같다** — 자막 큐(StoryVideoLibrary)가 그 시각에 맞춰 저작돼 있다.
디졸브(0.6초)는 뒤 샷을 그만큼 앞당겨 겹치므로 총 길이는 샷 길이의 합이다(자리표시 스크립트와 같은 규칙).
피사체는 가운데 56% 안에 둔다 — 세로 화면은 가운데를 잘라(cover) 튼다.

샷 하나 = 정적 레이어를 한 번 굽고, 시간 u(샷 안의 초)를 받아 프레임을 돌려주는 함수.
"""
import math
import os
import subprocess
import sys
import time

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from silhouette_kit import FPS, H, W, col, smooth, vignette  # noqa: E402
import sil_act1 as A1  # noqa: E402
import sil_act2 as A2  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "StreamingAssets", "Video")
PREVIEW_DIR = os.path.join(ROOT, "Artifacts", "story-silhouettes-preview")
FF = os.environ.get("FFMPEG", "C:/Users/kss11/AppData/Local/Programs/Python/Python312/Scripts/ffmpeg.exe")
DISSOLVE = 0.6
FADE = 0.6
VIGN = vignette()


# ══════════════════════════ 공통 장면 부품 ══════════════════════════

# ══════════════════════════ 편성 ══════════════════════════

VIDEOS = {
    "ch1_prologue": ([3.5, 3.0, 2.5, 3.0], [A1.ch1_s1, A1.ch1_s2, A1.ch1_s3, A1.ch1_s4], "#ffe9c8"),
    "ch2_watchers": ([3.0, 4.0, 3.0], [A1.ch2_s1, A1.ch2_s2, A1.ch2_s3], "#d8f2ec"),
    "ch3_scholar": ([3.0, 3.0, 3.0, 3.0], [A1.ch3_s1, A1.ch3_s2, A1.ch3_s3, A1.ch3_s4], "#e2f0c8"),
    "ch4_crates": ([3.0, 3.5, 3.0, 2.5], [A1.ch4_s1, A1.ch4_s2, A1.ch4_s3, A1.ch4_s4], "#e6dcc6"),
    "ch5_summit": ([3.0, 3.0, 3.5, 2.5], [A1.ch5_s1, A1.ch5_s2, A1.ch5_s3, A1.ch5_s4], "#e8d8f0"),
    "ch8_vault": ([3.0, 3.5, 3.0, 2.5], [A2.ch8_s1, A2.ch8_s2, A2.ch8_s3, A2.ch8_s4], "#f2dcb8"),
    "ch9_archive": ([3.0, 3.0, 4.0, 4.0], [A2.ch9_s1, A2.ch9_s2, A2.ch9_s3, A2.ch9_s4], "#dcecf6"),
    "ch10_kiln": ([3.0, 3.5, 3.5, 2.5, 2.5], [A2.ch10_s1, A2.ch10_s2, A2.ch10_s3, A2.ch10_s4, A2.ch10_s5], "#f6d0b0"),
    "ch11_crown": ([3.0, 3.0, 3.0, 3.0], [A2.ch11_s1, A2.ch11_s2, A2.ch11_s3, A2.ch11_s4], "#e2f4c8"),
    "ch12_ledger": ([3.0, 3.5, 3.5, 4.0], [A2.ch12_s1, A2.ch12_s2, A2.ch12_s3, A2.ch12_s4], "#e4e4e6"),
    "fin_epilogue": ([3.0, 3.0, 3.0, 3.0, 3.0], [A2.fin_s1, A2.fin_s2, A2.fin_s3, A2.fin_s4, A2.fin_s5], "#ffe9c8"),
}


def timeline(durs):
    """(시작, 끝, 앞당김) — 샷 k≥1은 DISSOLVE만큼 앞서 시작해 겹친다."""
    out = []
    acc = 0.0
    for k, d in enumerate(durs):
        start = acc - (DISSOLVE if k else 0.0)
        out.append((start, acc + d))
        acc += d
    return out, acc


def grade(frame, tint):
    f = frame * VIGN
    f = f * (0.94 + 0.06 * np.asarray(col(tint)))
    return np.clip(f, 0, 1)


def render(name, times=None):
    durs, makers, tint = VIDEOS[name]
    spans, total = timeline(durs)
    shots = [None] * len(makers)

    def shot(k):
        if shots[k] is None:
            shots[k] = makers[k]()
        return shots[k]

    def frame_at(T):
        f = None
        for k, (s, e) in enumerate(spans):
            if not (s <= T < e or (k == len(spans) - 1 and T >= s)):
                continue
            fr = shot(k)(T - s)
            if f is None:
                f = fr
            else:
                a = smooth((T - s) / DISSOLVE)
                f = f * (1 - a) + fr * a
        for k in range(len(shots)):   # 지난 샷은 버려 메모리를 아낀다
            if shots[k] is not None and spans[k][1] < T - 0.1:
                shots[k] = None
        f = grade(f, tint)
        if T > total - FADE:
            f = f * (1 - smooth((T - (total - FADE)) / FADE))
        if T < 0.25:
            f = f * smooth(T / 0.25)
        return f

    if times is not None:
        os.makedirs(PREVIEW_DIR, exist_ok=True)
        from PIL import Image
        for T in times:
            img = Image.fromarray((np.clip(frame_at(T), 0, 1) * 255).astype(np.uint8), "RGB")
            path = os.path.join(PREVIEW_DIR, f"{name}-{T:05.2f}.png")
            img.save(path)
            print("preview", path)
        return

    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, name + ".mp4")
    cmd = [FF, "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS),
           "-i", "-", "-vf", "noise=alls=5:allf=t,setsar=1", "-an", "-c:v", "libx264", "-profile:v", "main",
           "-level", "4.0", "-pix_fmt", "yuv420p", "-b:v", "1800k", "-maxrate", "2000k", "-bufsize", "4000k",
           "-movflags", "+faststart", path]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    n = int(round(total * FPS))
    t0 = time.time()
    for i in range(n):
        proc.stdin.write((np.clip(frame_at(i / FPS), 0, 1) * 255 + 0.5).astype(np.uint8).tobytes())
    proc.stdin.close()
    rc = proc.wait()
    print(f"{name}.mp4 {total:.1f}s {n}f {os.path.getsize(path) / 1e6:.2f}MB rc={rc} ({time.time() - t0:.0f}s)")


def main():
    args = [a for a in sys.argv[1:]]
    times = None
    if "--preview" in args:
        i = args.index("--preview")
        times = [float(v) for v in args[i + 1].split(",")]
        del args[i:i + 2]
    names = args[0].split(",") if args else list(VIDEOS)
    for name in names:
        render(name, times)


if __name__ == "__main__":
    main()
