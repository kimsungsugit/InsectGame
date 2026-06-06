"""UserPromptSubmit hook: 사용자 메시지 직전에 audit 진척 컨텍스트를 자동 주입.

audit 자동 플로우(.claude/rules/audit-flow.md)를 매 응답에 reminder로 주입한다.
사용자가 매번 `/audit` 입력하지 않아도 작업 완료 후 자동 audit이 실행되도록 컨텍스트 강화.

조건:
- audit-progress.md 존재 + Uncovered ≥ 1
- 사용자 메시지에 audit 거부 의사 없음 ("audit 안 해", "그만" 등)

출력: hookSpecificOutput.additionalContext로 1줄 룰 reminder + 다음 영역 명시.
"""

import sys, os, io, json, re

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

user_prompt = d.get("prompt", "") or d.get("user_message", "") or ""

# 사용자가 audit 거부 의사 표명 — 자동 플로우 차단
deny_patterns = [
    r"audit\s*안\s*해", r"audit\s*하지\s*마", r"audit\s*그만", r"audit\s*skip",
    r"skip\s*audit", r"audit\s*건너", r"감사\s*안\s*해"
]
for pat in deny_patterns:
    if re.search(pat, user_prompt, re.IGNORECASE):
        print(json.dumps({"suppressOutput": True}))
        sys.exit(0)

# 진척 파일 확인
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

m = re.search(r"##\s+Uncovered.*?\n(.*?)(?=\n##\s|\Z)", text, re.DOTALL)
if not m:
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

section = m.group(1)
uncovered_count = len(re.findall(r"^\s*-\s*\[\s*\]\s+", section, re.MULTILINE))
if uncovered_count == 0:
    print(json.dumps({"suppressOutput": True}))
    sys.exit(0)

first_match = re.search(r"^\s*-\s*\[\s*\]\s+(.+?)$", section, re.MULTILINE)
next_target = first_match.group(1).strip() if first_match else "?"
if len(next_target) > 70:
    next_target = next_target[:67] + "..."

# 룰 reminder — 작업 완료 후 자동 audit 권장
ctx = (
    f"[AUDIT-FLOW] audit 자동 플로우 활성. Uncovered {uncovered_count}개 — 다음 대상: {next_target}\n"
    f"규칙(.claude/rules/audit-flow.md): 사용자 요청 작업 완료 후 라운드 결과 보고 직후 "
    f"audit skill 자동 실행. 같은 응답 안에서 1회만, P0/P1 자동 처리, P2는 보고 후 결정 위임. "
    f"사용자가 \"audit 안 해\" 등 거부 의사 표명 시 차단."
)

print(json.dumps({
    "hookSpecificOutput": {
        "hookEventName": "UserPromptSubmit",
        "additionalContext": ctx
    }
}))
