"""Export actual ScreenCapture frames with their measured, variable durations.

Usage: python Tools/Export-BattleCapture.py Artifacts/battle-final-169
Requires Pillow and an ffmpeg executable (PATH or the local QA cache).
"""
import csv
import json
from pathlib import Path
import shutil
import subprocess
import sys
from PIL import Image, ImageStat

root = Path(__file__).resolve().parents[1]
capture = Path(sys.argv[1]).resolve()
rows = list(csv.DictReader((capture / "timeline.csv").open(encoding="utf-8")))
if not rows:
    raise SystemExit("No frames recorded")
bad_frames = []
for row in rows:
    path = capture / f"frame-{int(row['frame']):05d}.jpg"
    with Image.open(path) as shot:
        if max(ImageStat.Stat(shot.convert("RGB")).mean) < 1:
            bad_frames.append(int(row["frame"]))
if len(bad_frames) > len(rows) // 2:
    raise SystemExit("Capture is predominantly black; do not report visual QA success")

early_hp_changes = []
impact_samples = 0
for previous, current in zip(rows, rows[1:]):
    if (current["phase"] in ("PlayerAttack", "EnemyAttack")
            and current["phase"] == previous["phase"]
            and current.get("impactRevealed") == "False"):
        impact_samples += 1
        for key in ("displayPlayerHp", "displayEnemyHp"):
            if current.get(key) != previous.get(key):
                early_hp_changes.append({"frame": current["frame"], "field": key})
summary = {
    "frames": len(rows), "blackFrames": bad_frames,
    "width": int(rows[0]["width"]), "height": int(rows[0]["height"]),
    "durationSeconds": float(rows[-1]["elapsed"]) - float(rows[0]["elapsed"]),
    "resultReached": any(r["phase"] == "Result" for r in rows),
    "impactSyncSampleCount": impact_samples,
    "preImpactDisplayedHpChanges": early_hp_changes if impact_samples else None,
    "standbySideInversions": [r['frame'] for r in rows
        if r['phase'] in ('PlayerTurn', 'SwapSelect') and r.get('playerX')
        and float(r['playerX']) >= float(r['enemyX'])],
    "limits": "Scripted UI action methods; no physical button hit-test, audio or Android performance validation."
}
(capture / "verification.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
lines = []
for i, row in enumerate(rows):
    lines.append(f"file 'frame-{int(row['frame']):05d}.jpg'")
    duration = float(rows[i + 1]["elapsed"]) - float(row["elapsed"]) if i + 1 < len(rows) else 0.1
    lines.append(f"duration {max(0.001, duration):.6f}")
lines.append(f"file 'frame-{int(rows[-1]['frame']):05d}.jpg'")
(capture / "frames.ffconcat").write_text("\n".join(lines), encoding="utf-8")
ffmpeg = shutil.which("ffmpeg")
if not ffmpeg:
    candidates = list((root / ".claude/cache/ffmpeg-runtime").glob("**/ffmpeg*.exe"))
    if candidates:
        ffmpeg = str(candidates[0])
if not ffmpeg:
    raise SystemExit("Verification written; ffmpeg unavailable for MP4 export")
subprocess.run([ffmpeg, "-y", "-loglevel", "error", "-f", "concat", "-safe", "0",
                "-i", "frames.ffconcat", "-fps_mode", "vfr", "-c:v", "libx264",
                "-pix_fmt", "yuv420p", "-movflags", "+faststart", "battle-review.mp4"],
               cwd=capture, check=True)
print(json.dumps(summary, indent=2))
