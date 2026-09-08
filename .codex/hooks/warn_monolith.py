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
# 최상위가 dict가 아니거나(null/배열) tool_input이 명시적 null이면 조용히 나간다.
# d.get("k", {})는 키가 **없을 때만** {}를 주지 실제 null에는 null을 준다.
if not isinstance(d, dict):
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)
if not isinstance(d.get("tool_input"), dict):
    d["tool_input"] = {}
if not isinstance(d.get("tool_response"), dict):
    d["tool_response"] = {}


tool_name = d.get("tool_name", "")
tool_input = d.get("tool_input", {})
f = tool_input.get("file_path", "")

# file_path가 없으면 조용히 나간다. 7개 훅 중 여기만 가드가 없어
# f.endswith()가 None/비문자열에서 터질 수 있었다(check_namespace엔 있다).
if not f or not isinstance(f, str):
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

# 줄 수는 하드코딩하지 않는다 — 파일이 자라면 반드시 stale해지고, 그 숫자가
# 다시 문서로 복사돼 퍼진다. 실제 값은 아래에서 런타임에 센다.
MONOLITHS = {
    "PlaySceneBootstrap.cs": "Bootstrap 모놀리스",
    "BattleScreenUI.cs": "배틀UI 모놀리스",
    "RaidBattleUI.cs": "레이드UI 모놀리스",
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

# 실제 줄 수를 여기서 센다. 이 훅이 모놀리스 줄 수의 단일 출처이며,
# CLAUDE.md/rules는 숫자를 갖지 않고 이 경고를 참조한다.
try:
    with open(f, "r", encoding="utf-8", errors="replace") as fh:
        loc = sum(1 for _ in fh)
    target_desc = f"{loc}줄 {target_desc}"
except Exception:
    pass

print(json.dumps({
    "hookSpecificOutput": {
        "hookEventName": "PreToolUse",
        "additionalContext": (
            f"CAUTION: {target_name} ({target_desc}) 수정 중. "
            "Phase별 영향 범위를 확인하고 최소한의 변경만 하세요."
        )
    }
}))
