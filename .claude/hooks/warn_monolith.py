"""PreToolUse hook: 모놀리스 파일 수정 경고"""
import sys, json

raw = sys.stdin.read()
raw = raw.replace("\\", "/")
d = json.loads(raw)
f = d.get("tool_input", {}).get("file_path", "")

MONOLITHS = {
    "PlaySceneBootstrap.cs": "4987줄 Bootstrap 모놀리스",
    "BattleScreenUI.cs": "2950줄 배틀UI 모놀리스",
    "RaidBattleUI.cs": "2875줄 레이드UI 모놀리스",
}

for name, desc in MONOLITHS.items():
    if f.endswith(name):
        print(json.dumps({
            "hookSpecificOutput": {
                "hookEventName": "PreToolUse",
                "additionalContext": (
                    f"CAUTION: {name} ({desc}) 수정 중. "
                    "Phase별 영향 범위를 확인하고 최소한의 변경만 하세요."
                )
            }
        }))
        sys.exit(0)

print(json.dumps({"suppressOutput": True}))
