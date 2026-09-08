"""PostToolUse hook: 큰 변경 감지 시 verify 8항목 자동 점검 권유.

memory rule (feedback_post_impl_verify)이 "구현 후 /verify 자동 점검"인데 트리거 메커니즘이
없었다. 이 hook이 Edit/Write 변경 크기를 보고 임계값 초과 시 다음 응답에 점검 권유 컨텍스트를
주입한다. 작은 fix는 노이즈 방지 위해 스킵."""

import sys, json, io, os

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
file_path = tool_input.get("file_path", "")

# .cs 파일만 대상 (게임 코드 변경만). .md/.json/.py 변경은 자체 검증 없음.
if not file_path or not file_path.endswith(".cs"):
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

# 변경 라인 수 계산
trigger = False
reason = ""

if tool_name == "Write":
    # 신규 파일 또는 전체 덮어쓰기 — 무조건 권유
    content = tool_input.get("content", "") or ""
    lines = content.count("\n")
    if lines >= 20:
        trigger = True
        reason = f"신규/전체 작성 ({lines}줄)"
elif tool_name == "Edit":
    old_s = tool_input.get("old_string", "") or ""
    new_s = tool_input.get("new_string", "") or ""
    # 추가/삭제된 총 라인 수의 근사치
    line_diff = abs(new_s.count("\n") - old_s.count("\n")) + max(
        old_s.count("\n"), new_s.count("\n")
    )
    if tool_input.get("replace_all"):
        # replace_all은 영향 범위가 커서 임계값 낮춤
        threshold = 10
    else:
        threshold = 50
    if line_diff >= threshold:
        trigger = True
        reason = f"{line_diff}줄 변경 (임계 {threshold})"

if not trigger:
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

# 변경 파일 경로 short (가독성)
short = file_path
try:
    root = os.environ.get("CLAUDE_PROJECT_DIR") or os.getcwd()
    short = os.path.relpath(file_path, root).replace("\\", "/")
except Exception:
    pass

ctx = (
    f"AUTO-VERIFY: 큰 변경 감지 — {short} | {reason}\n"
    "다음 8항목 중 변경에 해당하는 것만 점검 후 결과를 사용자에게 보고:\n"
    "1. 이벤트 구독: OnEnable/AutoWire/OnDisable 짝, 람다 해제 누락\n"
    "2. null 가드: FindFirstObjectByType/GetComponent/Singleton.Instance 즉시 사용\n"
    "3. 성능: Update/OnGUI 안 new GUIStyle/배열, GameObject.Find 캐싱\n"
    "4. Bootstrap: 새 MonoBehaviour 등록, AutoWire 누락\n"
    "5. 세이브 호환: 새 필드 기본값, CloudSaveManager 동기화\n"
    "6. 모놀리스: BattleScreenUI/RaidBattleUI/PlaySceneBootstrap 최소 변경 확인\n"
    "7. 컨벤션: [SerializeField] private, InsectGame.* 네임스페이스\n"
    "8. 데이터 매칭: ID contains 순서 (구체적 먼저), enum↔switch 1:1\n"
    "거짓양성 보고 금지 — 직전 라운드 처리 영역 자동 제외."
)

print(json.dumps({
    "hookSpecificOutput": {
        "hookEventName": "PostToolUse",
        "additionalContext": ctx
    }
}))
