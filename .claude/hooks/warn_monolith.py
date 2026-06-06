"""PreToolUse hook: 모놀리스 파일 수정 경고 (변경 크기 조건부)"""
import sys, json, io

try:
    sys.stdin = io.TextIOWrapper(sys.stdin.buffer, encoding='utf-8', errors='replace')
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
except Exception:
    pass

raw = sys.stdin.read()
try:
    d = json.loads(raw)
except Exception:
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

tool_name = d.get("tool_name", "")
tool_input = d.get("tool_input", {})
f = tool_input.get("file_path", "")

MONOLITHS = {
    "PlaySceneBootstrap.cs": "4987줄 Bootstrap 모놀리스",
    "BattleScreenUI.cs": "2950줄 배틀UI 모놀리스",
    "RaidBattleUI.cs": "2875줄 레이드UI 모놀리스",
}

target_name = None
target_desc = None
for name, desc in MONOLITHS.items():
    if f.endswith(name):
        target_name = name
        target_desc = desc
        break

if not target_name:
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

# 변경 라인 수 계산: 작은 fix(<20줄)는 위험도 낮으므로 경고 스킵
# Write tool: 전체 덮어쓰기라 항상 경고
should_warn = True
if tool_name == "Edit":
    old_s = tool_input.get("old_string", "") or ""
    new_s = tool_input.get("new_string", "") or ""
    line_diff = abs(new_s.count("\n") - old_s.count("\n")) + max(
        old_s.count("\n"), new_s.count("\n")
    )
    # replace_all 모드면 영향 더 크므로 임계값 낮춤
    if tool_input.get("replace_all"):
        should_warn = line_diff >= 5
    else:
        should_warn = line_diff >= 20

if not should_warn:
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

print(json.dumps({
    "hookSpecificOutput": {
        "hookEventName": "PreToolUse",
        "additionalContext": (
            f"CAUTION: {target_name} ({target_desc}) 수정 중. "
            "Phase별 영향 범위를 확인하고 최소한의 변경만 하세요."
        )
    }
}))
