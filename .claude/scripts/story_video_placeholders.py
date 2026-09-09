"""자리표시 애니매틱 생성 — Docs/StoryVideos.md의 샷 길이·팔레트로 무자막 색면 영상을 만든다.
AI 생성 영상이 들어오기 전까지 기기 테스트용. 규격: 1280x720, 24fps, H.264 Main, <=2Mbps, +faststart, 무음."""
import subprocess, sys, os, numpy as np
from PIL import Image, ImageFilter

FF = sys.argv[1]
OUT = sys.argv[2]
W, H, FPS = 320, 180, 24
M = 40   # 드리프트 여유
rng = np.random.default_rng(7)

def col(h):  # "#rrggbb" -> np float rgb
    return np.array([int(h[i:i+2], 16) for i in (1, 3, 5)], dtype=np.float32) / 255.0

# 챕터 팔레트 (하늘/땅/강조)
P = {
    "meadow": ("#f3c977", "#5f7a3a", "#fff1c9"), "pond": ("#4f8f8f", "#1f3f45", "#a9d9d0"),
    "forest": ("#5a7d46", "#22331f", "#a3c47a"), "swamp": ("#8a8574", "#3b3a31", "#b6b09a"),
    "mountain": ("#6f5f9a", "#2b2745", "#d6c6ef"), "dunes": ("#d9a55c", "#7a5a2a", "#f6e3b3"),
    "frost": ("#bfe3ee", "#3f6f86", "#ffffff"), "ember": ("#e0622f", "#3a1a12", "#ffb15a"),
    "canopy": ("#7fc05a", "#2f5a2a", "#e6ffb0"), "nameless": ("#8a8a8a", "#2a2a2a", "#d0d0d0"),
}
# (videoId 파일명, 팔레트, 샷 길이 목록) — Docs/StoryVideos.md §4와 같다
VIDEOS = [
    ("ch1_prologue", "meadow", [3.5, 3.0, 2.5, 3.0]),
    ("ch2_watchers", "pond", [3.0, 4.0, 3.0]),
    ("ch3_scholar", "forest", [3.0, 3.0, 3.0, 3.0]),
    ("ch4_crates", "swamp", [3.0, 3.5, 3.0, 2.5]),
    ("ch5_summit", "mountain", [3.0, 3.0, 3.5, 2.5]),
    ("ch8_vault", "dunes", [3.0, 3.5, 3.0, 2.5]),
    ("ch9_archive", "frost", [3.0, 3.0, 4.0, 4.0]),
    ("ch10_kiln", "ember", [3.0, 3.5, 3.5, 2.5, 2.5]),
    ("ch11_crown", "canopy", [3.0, 3.0, 3.0, 3.0]),
    ("ch12_ledger", "nameless", [3.0, 3.5, 3.5, 4.0]),
    ("fin_epilogue", "meadow", [3.0, 3.0, 3.0, 3.0, 3.0]),
]
DISSOLVE = 0.6

def noise_field(seed, scale):
    r = np.random.default_rng(seed)
    small = r.random(((H + 2 * M) // scale + 2, (W + 2 * M) // scale + 2)).astype(np.float32)
    img = Image.fromarray((small * 255).astype(np.uint8)).resize((W + 2 * M, H + 2 * M), Image.Resampling.BICUBIC)
    img = img.filter(ImageFilter.GaussianBlur(scale * 0.6))
    return np.asarray(img, dtype=np.float32) / 255.0

def shot_frames(seed, sky, ground, accent, dur, drift):
    n = int(round(dur * FPS))
    f1, f2 = noise_field(seed, 24), noise_field(seed + 1, 8)
    yy = np.linspace(0, 1, H, dtype=np.float32)[:, None]
    for i in range(n):
        t = i / max(1, n - 1)
        dx = int(drift[0] * t); dy = int(drift[1] * t)
        a = f1[M + dy:M + dy + H, M + dx:M + dx + W]; b = f2[M + dy:M + dy + H, M + dx:M + dx + W]
        grad = np.clip(yy + (a - 0.5) * 0.5, 0, 1)[..., None]
        base = sky * (1 - grad) + ground * grad
        brush = (b - 0.5) * 0.18
        light = np.clip(0.5 + 0.5 * np.cos(t * 1.4 + a * 3.0), 0, 1)[..., None] * 0.22
        frame = np.clip(base + brush[..., None] + accent * light, 0, 1)
        yield frame

def build(name, pal, durs):
    sky, ground, accent = (col(c) for c in P[pal])
    total = sum(durs)
    frames = []
    for k, d in enumerate(durs):
        seed = hash((name, k)) & 0xffff
        drift = (rng.integers(-18, 18), rng.integers(-10, 10))
        tint = 1.0 + 0.08 * ((k % 3) - 1)
        # 디졸브는 앞 샷 끝과 겹치므로 그만큼 길게 뽑아야 총 길이가 저작 길이와 같다.
        frames.append(list(shot_frames(seed, sky * tint, ground, accent, d + (DISSOLVE if k else 0.0), drift)))
    # 디졸브 + 마지막 페이드아웃
    out = []
    for k, seq in enumerate(frames):
        if k == 0:
            out.extend(seq); continue
        m = int(DISSOLVE * FPS)
        for j in range(m):
            a = j / m
            out[-m + j] = out[-m + j] * (1 - a) + seq[j] * a
        out.extend(seq[m:])
    fade = int(0.6 * FPS)
    for j in range(fade):
        out[-fade + j] = out[-fade + j] * (1 - (j + 1) / fade)
    path = os.path.join(OUT, name + ".mp4")
    cmd = [FF, "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
           "-vf", "scale=1280:720:flags=lanczos,gblur=sigma=1.2,noise=alls=6:allf=t,setsar=1",
           "-an", "-c:v", "libx264", "-profile:v", "main", "-level", "4.0", "-pix_fmt", "yuv420p",
           "-b:v", "1600k", "-maxrate", "2000k", "-bufsize", "4000k", "-movflags", "+faststart", path]
    p = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for fr in out:
        p.stdin.write((fr * 255).astype(np.uint8).tobytes())
    p.stdin.close(); p.wait()
    print(f"{name}.mp4 {total:.1f}s {os.path.getsize(path)/1e6:.2f}MB rc={p.returncode}")

os.makedirs(OUT, exist_ok=True)
for v in VIDEOS:
    build(*v)
