"""Stop hook: 응답이 끝날 때 audit 미검토 영역을 알린다.

사용자가 매번 "문제점 분석 및 개선해"를 입력하는 부담을 없애는 게 목적이다.
남은 Uncovered 카운트와 다음 대상을 두 경로로 낸다:
- additionalContext → 모델 (자동 실행 판단용)
- systemMessage     → 사용자 (진척 표시용. 모델은 이걸 읽지 못한다)

침묵 조건:
- stop_hook_active — 이미 Stop 훅에서 이어진 턴 (무한 루프 차단, 필수)
- Uncovered 섹션의 `- [ ]` 카운트가 0
- 진척 파일 없음

주의: 이 훅은 사용자 프롬프트를 볼 수 없어 "audit 안 해" 같은 거부 의사를
판정할 수 없다. 거부 존중은 UserPromptSubmit 쪽(audit_flow_inject)이 담당하며,
자동화의 주 동력도 그쪽이다. 이 훅은 보조 알림이다.
"""

import sys, os, io, json, re

try:
    sys.stdin = io.TextIOWrapper(sys.stdin.buffer, encoding='utf-8', errors='replace')
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
except Exception:
    pass

try:
    d = json.loads(sys.stdin.read())
except Exception:
    # 입력을 못 읽으면 stop_hook_active를 판별할 수 없다. 그 상태로 발화하면
    # 발화 → 모델 작업 → Stop → 또 실패 → 또 발화로 루프가 성립하므로 침묵한다.
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


# 무한 루프 차단 (필수): additionalContext를 주면 모델이 이어서 작업하고, 그 턴이
# 끝나면 Stop이 또 발화한다. stop_hook_active는 "이미 Stop 훅에서 이어진 턴"을
# 뜻하므로 여기서 침묵해야 사용자 턴당 자동 audit이 정확히 1회로 끝난다.
if d.get("stop_hook_active"):
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

# 진척 파일 경로 — settings.json이 프로젝트 루트에서 실행되므로 상대 경로.
progress_path = os.path.join(".claude", "audit-progress.md")

if not os.path.exists(progress_path):
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

try:
    with open(progress_path, "r", encoding="utf-8") as fh:
        text = fh.read()
except Exception:
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

# `## Uncovered` 섹션 시작 ~ 다음 `##` 또는 EOF.
# 줄 시작(^) 앵커 필수 — 앵커가 없으면 본문이나 헤더가 `## Uncovered`를 인용만 해도
# 그 지점이 첫 매칭이 되어 엉뚱한 구간을 세고 카운트 0으로 침묵한다.
m = re.search(
    r"^##\s+Uncovered.*?\n(.*?)(?=\n^##\s|\Z)", text, re.DOTALL | re.MULTILINE
)
if not m:
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

section = m.group(1)
# `- [ ] ` 패턴 카운트 (체크 안 된 항목)
uncovered_count = len(re.findall(r"^\s*-\s*\[\s*\]\s+", section, re.MULTILINE))

if uncovered_count == 0:
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

# 첫 번째 미검토 항목 추출 (다음 audit 대상 미리 보여줌)
first_match = re.search(r"^\s*-\s*\[\s*\]\s+(.+?)$", section, re.MULTILINE)
next_target = first_match.group(1).strip() if first_match else "?"
# 너무 긴 라인 자르기
if len(next_target) > 60:
    next_target = next_target[:57] + "..."

msg = (
    f"audit 미검토 {uncovered_count}개 — 다음: {next_target}. /audit 자동 실행 권장."
)

# Stop 훅은 additionalContext를 지원한다(옛 주석의 "미지원"은 사실이 아니었다).
# systemMessage는 사용자에게만 보이고 모델은 읽지 못하므로, 그것만으로는
# "/audit 자동 실행"이 모델에 전달된 적이 없었다. 둘 다 낸다 —
# additionalContext는 모델에게, systemMessage는 사용자에게.
print(json.dumps({
    "hookSpecificOutput": {
        "hookEventName": "Stop",
        "additionalContext": (
            f"{msg} 자동 플로우 규칙은 .claude/rules/audit-flow.md 참조. "
            "사용자가 audit을 거부했거나 다른 작업을 즉시 요청한 경우에는 실행하지 않는다."
        )
    },
    "systemMessage": msg,
    "suppressOutput": True
}))
