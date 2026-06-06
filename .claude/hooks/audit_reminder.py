"""Stop hook: 응답 끝날 때 audit 미검토 영역 카운트를 알린다.

사용자가 매번 "문제점 분석 및 개선해" 입력 부담 → Stop hook이 진척 상황 보여줌.

조건:
- .claude/audit-progress.md 존재
- Uncovered 섹션의 `- [ ]` 카운트 ≥ 1
- 짧은 한 줄로 알림: "audit 미검토 N개 남음 — /audit으로 다음 1개 자동 처리"

알림이 노이즈가 되지 않게:
- Uncovered = 0이면 침묵
- 진척 파일이 없으면 침묵
"""

import sys, os, io, json, re

try:
    sys.stdin = io.TextIOWrapper(sys.stdin.buffer, encoding='utf-8', errors='replace')
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
except Exception:
    pass

# Stop hook은 stdin으로 JSON을 받지만 본 hook은 출력만 결정하므로 입력은 무시.
try:
    _ = sys.stdin.read()
except Exception:
    pass

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

# `## Uncovered` 섹션 시작 ~ 다음 `##` 또는 EOF
m = re.search(r"##\s+Uncovered.*?\n(.*?)(?=\n##\s|\Z)", text, re.DOTALL)
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

# Stop hook은 hookSpecificOutput.additionalContext 미지원 — systemMessage 사용
print(json.dumps({
    "systemMessage": msg,
    "suppressOutput": True
}))
