"""스토리 영상 15편 렌더러 — 그림책 화풍 + 소리(Docs/StoryVideos.md §3·§4).

    python -X utf8 Tools/Video/storybook/render.py <이름,...|all> [--preview 1.0,4.5] [--sheet] [--subs] [--portrait]
                                                     [--no-audio] [--out <폴더>]

산출: Assets/StreamingAssets/Video/<이름>.mp4 — 1280×720 · 24fps · H.264 Main · CRF 23(상한 2 Mbps) · AAC 96k 스테레오 · +faststart.
**자막은 굽지 않는다** — 게임이 IMGUI로 얹는다. `--subs`는 미리보기에만 자막을 얹어 시각·구도를 본다.
`--preview`는 지정 시각 프레임을 Artifacts/storybook-preview/에 PNG로, `--sheet`는 그 프레임들(없으면 샷마다 둘)을 한 장으로,
`--portrait`는 세로 화면(가운데 32%)으로 잘라 본다.

영상 하나 = `v_<이름>.py` 모듈:
    DURS   샷 길이 목록(초). **총 길이 = 합** — 디졸브(0.6초)는 뒤 샷을 그만큼 앞당겨 겹친다.
    SHOTS  샷을 만드는 함수 목록. 각 함수는 정적 레이어를 한 번 굽고 frame(u)(u = 샷 안의 초)를 돌려준다.
    TINT   마감 색조('#hex').
    score(sc)  소리 — sound.Score에 사건을 얹는다. sc.shot(k)가 k번째 샷의 시작 시각.
    CUES   (선택) StoryVideoLibrary.cs에 아직 없는 영상의 자막 초안 [(at, dur, 문구)].

**자막·길이의 단일 출처는 `Assets/Scripts/Story/StoryVideoLibrary.cs`다.** 여기서 그 파일을 읽어 미리보기 자막을 얹고,
샷 합이 저작 길이와 다르면 멈춘다(마지막 자막이 영상 밖으로 나간다).
"""
import importlib
import math
import os
import re
import subprocess
import sys
import tempfile
import time

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import kit  # noqa: E402
from kit import FPS, H, W, col, smooth, vignette  # noqa: E402
import sound  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "StreamingAssets", "Video")
PREVIEW_DIR = os.path.join(ROOT, "Artifacts", "storybook-preview")
LIBRARY = os.path.join(ROOT, "Assets", "Scripts", "Story", "StoryVideoLibrary.cs")
FF = os.environ.get("FFMPEG", "C:/Users/kss11/AppData/Local/Programs/Python/Python312/Scripts/ffmpeg.exe")
FONT = "C:/Windows/Fonts/malgunbd.ttf"
DISSOLVE = 0.6
FADE = 0.6
MAX_BYTES = 4_000_000
VIGN = vignette(strength=0.16)

# 편성 — 파일 이름(= videoId에서 vid_를 뺀 것). 순서는 이야기 순서.
NAMES = ["ch1_prologue", "ch2_watchers", "ch3_scholar", "ch4_crates", "ch5_summit", "ch6_wall", "ch7_fence",
         "ch8_vault", "ch9_archive", "ch10_kiln", "ch11_crown", "ch12_ledger", "fin_shadow", "fin_return",
         "fin_epilogue"]


# ══════════════════════════ 저작(StoryVideoLibrary.cs) ══════════════════════════

def library_entry(name):
    """StoryVideoLibrary.cs에서 (길이, [(at, dur, 문구)])를 읽는다. 없으면 None."""
    try:
        src = open(LIBRARY, encoding="utf-8").read()
    except OSError:
        return None
    m = re.search(r'new StoryVideoDefinition\(\s*\w+\s*,\s*"' + re.escape(name) + r'\.mp4"\s*,\s*([\d.]+)f\s*,\s*new\[\]\s*\{(.*?)\}\s*\)\s*;',
                  src, re.S)
    if not m:
        return None
    cues = [(float(a), float(d), t) for a, d, t in
            re.findall(r'new StoryVideoCue\(\s*([\d.]+)f\s*,\s*([\d.]+)f\s*,\s*"((?:[^"\\]|\\.)*)"\s*\)', m.group(2))]
    return float(m.group(1)), cues


def load(name):
    mod = importlib.import_module("v_" + name)
    total = sum(mod.DURS)
    entry = library_entry(name)
    draft = list(getattr(mod, "CUES", []))
    if entry is not None:
        length, cues = entry
        if abs(length - total) > 1e-3:
            sys.exit(f"[{name}] 샷 합 {total:.2f}초 ≠ StoryVideoLibrary 길이 {length:.2f}초 — 둘을 맞출 것")
        if draft and [(round(a, 2), round(d, 2), t) for a, d, t in draft] != [(round(a, 2), round(d, 2), t) for a, d, t in cues]:
            print(f"[{name}] 모듈 CUES 초안이 StoryVideoLibrary와 다르다 — 미리보기는 초안을 쓴다(라이브러리를 고치거나 초안을 지울 것)")
            cues = draft
    else:
        cues = draft
        print(f"[{name}] StoryVideoLibrary에 아직 없다 — 모듈의 CUES 초안으로 미리 본다")
    for a, d, txt in cues:
        if a + d > total + 1e-3:
            sys.exit(f"[{name}] 자막 '{txt}'가 {a + d:.2f}초에 끝난다 — 영상({total:.2f}초) 밖")
    return mod, total, cues


def timeline(durs):
    """(시작, 끝) — 샷 k≥1은 DISSOLVE만큼 앞서 시작해 겹친다."""
    out, acc = [], 0.0
    for k, d in enumerate(durs):
        out.append((acc - (DISSOLVE if k else 0.0), acc + d))
        acc += d
    return out, acc


# ══════════════════════════ 그림 ══════════════════════════

def make_renderer(mod):
    spans, total = timeline(mod.DURS)
    shots = [None] * len(mod.SHOTS)
    tint = 0.94 + 0.06 * col(getattr(mod, "TINT", "#fff8ec"))
    paper = kit.paper_tex(77)

    def shot(k):
        if shots[k] is None:
            shots[k] = mod.SHOTS[k]()
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
        f = kit.apply_paper(np.array(f, np.float32), paper)
        f = np.clip(f * VIGN * tint, 0, 1)
        if T > total - FADE:
            f = f * (1 - smooth((T - (total - FADE)) / FADE))
        if T < 0.25:
            f = f * smooth(T / 0.25)
        return f

    return frame_at, spans, total


# 미리보기 자막 — 게임(StoryVideoDirector.DrawSubtitle)과 비슷하게: 아래쪽 검은 띠 위 흰 굵은 글씨.
_FONT = {}


def burn_subtitle(img, text, portrait=False):
    w, h = img.size
    size = 30 if not portrait else 26
    if size not in _FONT:
        _FONT[size] = ImageFont.truetype(FONT, size)
    font = _FONT[size]
    d = ImageDraw.Draw(img, "RGBA")
    bw = min(1100, w - 80)
    x0 = (w - bw) / 2
    y0 = h - 150 if not portrait else h - 130
    d.rectangle([x0, y0, x0 + bw, y0 + 96], fill=(0, 0, 0, 140))
    tw = d.textlength(text, font=font)
    if tw > bw - 40:   # 게임은 LabelFit로 줄인다 — 미리보기는 두 줄로만 나눈다
        mid = len(text) // 2
        cut = text.rfind(" ", 0, mid + 6)
        lines = [text[:cut], text[cut + 1:]] if cut > 0 else [text]
    else:
        lines = [text]
    for i, ln in enumerate(lines):
        lw = d.textlength(ln, font=font)
        yy = y0 + 48 - len(lines) * (size * 0.65) + i * size * 1.3
        d.text(((w - lw) / 2, yy), ln, font=font, fill=(245, 242, 230, 255))
    return img


def cue_at(cues, T):
    for a, dur, txt in cues:
        if a <= T <= a + dur:
            return txt
    return None


def to_img(f):
    return Image.fromarray((np.clip(f, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB")


def portrait_crop(img):
    """세로 720×1280 화면의 cover-crop — 16:9의 가운데 0.316만 보인다. 1280 높이로 키워 돌려준다."""
    vis = int(round(H * 720 / 1280))   # 높이 720을 다 쓰면 보이는 폭은 405(= 1280의 32%)
    x0 = (W - vis) // 2
    return img.crop((x0, 0, x0 + vis, H)).resize((720, 1280), Image.Resampling.LANCZOS)


def preview(name, times=None, sheet=False, subs=False, portrait=False):
    mod, total, cues = load(name)
    frame_at, spans, _ = make_renderer(mod)
    if not times:
        times = []
        for s, e in spans:
            s = max(0.0, s)
            times += [round(s + (e - s) * 0.35, 2), round(s + (e - s) * 0.85, 2)]
    os.makedirs(PREVIEW_DIR, exist_ok=True)
    imgs = []
    for T in times:
        img = to_img(frame_at(T))
        if portrait:
            img = portrait_crop(img)
        if subs:
            txt = cue_at(cues, T)
            if txt:
                img = burn_subtitle(img, txt, portrait)
        imgs.append((T, img))
        if not sheet:
            p = os.path.join(PREVIEW_DIR, f"{name}{'-port' if portrait else ''}-{T:05.2f}.png")
            img.save(p)
            print("preview", p)
    if sheet:
        cols = 3 if not portrait else 5
        rows = (len(imgs) + cols - 1) // cols
        tw, th = (W // 2, H // 2) if not portrait else (720 // 3, 1280 // 3)
        sh = Image.new("RGB", (tw * cols, th * rows), (0, 0, 0))
        dd = ImageDraw.Draw(sh)
        for i, (T, img) in enumerate(imgs):
            x, y = (i % cols) * tw, (i // cols) * th
            sh.paste(img.resize((tw, th), Image.Resampling.LANCZOS), (x, y))
            dd.text((x + 6, y + 4), f"{T:.2f}s", fill=(255, 255, 255))
        p = os.path.join(PREVIEW_DIR, f"{name}{'-port' if portrait else ''}-sheet.png")
        sh.save(p, quality=90)
        print("sheet", p)


# ══════════════════════════ 소리·인코딩 ══════════════════════════

def make_audio(mod, spans, total, path):
    sc = sound.Score(total, [max(0.0, s) for s, _ in spans], seed=abs(hash(mod.__name__)) % 10000)
    mod.score(sc)
    return sc.finish(path, reverb=getattr(mod, "REVERB", 0.3))


def render(name, out_dir=OUT_DIR, audio=True, crf=23):
    mod, total, cues = load(name)
    frame_at, spans, _ = make_renderer(mod)
    os.makedirs(out_dir, exist_ok=True)
    path = os.path.join(out_dir, name + ".mp4")
    wav = None
    cmd = [FF, "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS),
           "-i", "-"]
    if audio:
        wav = os.path.join(tempfile.gettempdir(), f"storybook_{name}.wav")
        make_audio(mod, spans, total, wav)
        cmd += ["-i", wav, "-c:a", "aac", "-strict", "-2", "-b:a", "96k", "-ac", "2", "-ar", "44100"]
    else:
        cmd += ["-an"]
    # 품질 기준(CRF) + 상한 2 Mbps — 그림책 화면은 단색이 넓어 고정 1.8 Mbps의 60% 안팎으로 같은 화질이 난다(ch7: 3.10 → 1.90MB).
    rate = ["-crf", str(crf)] if crf is not None else ["-b:v", "1800k"]
    cmd += ["-vf", "noise=alls=3:allf=t,setsar=1", "-c:v", "libx264", "-profile:v", "main", "-level", "4.0",
            "-pix_fmt", "yuv420p"] + rate + ["-maxrate", "2000k", "-bufsize", "4000k", "-t", f"{total:.3f}",
            "-movflags", "+faststart", path]
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    n = int(round(total * FPS))
    t0 = time.time()
    for i in range(n):
        proc.stdin.write((np.clip(frame_at(i / FPS), 0, 1) * 255 + 0.5).astype(np.uint8).tobytes())
    proc.stdin.close()
    rc = proc.wait()
    size = os.path.getsize(path) if os.path.exists(path) else 0
    flag = "" if size <= MAX_BYTES else f"  ← {MAX_BYTES / 1e6:.0f}MB 초과"
    print(f"{name}.mp4 {total:.1f}s {n}f {size / 1e6:.2f}MB rc={rc} ({time.time() - t0:.0f}s){flag}")
    if wav and os.path.exists(wav):
        keep = os.path.join(PREVIEW_DIR, name + ".wav")
        os.makedirs(PREVIEW_DIR, exist_ok=True)
        os.replace(wav, keep)
    return rc == 0 and size <= MAX_BYTES


def main():
    args = list(sys.argv[1:])

    def flag(f):
        if f in args:
            args.remove(f)
            return True
        return False

    def opt(f):
        if f in args:
            i = args.index(f)
            v = args[i + 1]
            del args[i:i + 2]
            return v
        return None

    sheet, subs, portrait, no_audio = flag("--sheet"), flag("--subs"), flag("--portrait"), flag("--no-audio")
    pv = opt("--preview")
    out = opt("--out") or OUT_DIR
    crf = opt("--crf")
    which = args[0] if args else "all"
    names = NAMES if which == "all" else which.split(",")
    ok = True
    for name in names:
        if pv is not None or sheet:
            preview(name, [float(v) for v in pv.split(",")] if pv else None, sheet, subs, portrait)
        else:
            ok &= render(name, out, not no_audio, int(crf) if crf else 23)
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
