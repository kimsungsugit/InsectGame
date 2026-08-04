"""CI에서 하네스 검사기를 그대로 돌린다. CI는 검사를 재구현하지 않는다.

왜 있나
-------
`.github/workflows/ci.yml`의 lint 잡은 네임스페이스 규칙과 금지 패턴 검사를 **bash로
재구현**하고 있었다. 같은 규칙의 다섯 번째 사본이었다:
  ① `.claude/hooks/check_namespace.py`  ② `.codex/hooks/check_namespace.py`
  ③ `.claude/rules/unity-csharp.md`     ④ CLAUDE.md    ⑤ ci.yml의 bash

사본은 썩는다. 그리고 CI의 사본은 아무도 안 본다 — 이 CI는 2026-04-15에 한 번 돌고
14초 만에 실패한 뒤 3개월간 방치됐다.

훅은 전부 stdin-JSON 인터페이스라(argv 미지원) 합성 JSON만 먹이면 코드 변경 없이
CI에서 재사용된다. 훅은 조언용이라 항상 exit 0이므로, 출력을 판정해 실패시키는 게
이 드라이버의 일이다.

무엇을 CI가 잡나
----------------
세션 안 편집은 훅이 실시간으로 잡는다. CI의 몫은 **세션 밖 편집**이다 —
Codex CLI 병행 사용, 손편집, 다른 기계에서 온 커밋.

종료 코드
---------
  0  이상 없음
  1  위반 발견
  2  검사기 자신의 고장 (data_lint/verify_coverage의 exit 2 전파)
"""
import argparse
import glob
import io
import json
import os
import subprocess
import sys

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

ROOT = os.environ.get("CLAUDE_PROJECT_DIR") or os.getcwd()
HOOKS = os.path.join(ROOT, ".claude", "hooks")
SCRIPTS = os.path.join(ROOT, ".claude", "scripts")

# 파일 단위로 돌릴 훅. (파일, 훅출력) → 위반 여부는 훅이 무언가 말하면 위반으로 본다.
# check_namespace만 CI 차단 대상이다 — 나머지 훅(성능·컴파일 의심)은 조언이라
# 사람이 판단할 문제이고, CI가 막으면 오탐 하나로 push가 멈춘다.
BLOCKING_HOOKS = ["check_namespace.py"]

# 저장소 전체를 보는 스크립트. 종료 코드가 곧 판정이다.
REPO_CHECKS = [
    (["data_lint.py"], "데이터 정합성"),
    (["quest_lint.py"], "퀘스트 정합성"),
    (["story_lint.py"], "스토리 정합성"),
    (["ui_layout_lint.py"], "UI 레이아웃 마진"),
    (["subscription_lint.py"], "UI 구독 소실"),
    (["text_fit_lint.py"], "라벨 잘림"),
    (["sync_codex.py", "--check"], "Codex 미러 동기"),
]


def cs_files():
    out = []
    for pat in ("Assets/Scripts/**/*.cs", "Assets/Editor/**/*.cs", "Assets/Tests/**/*.cs"):
        out += glob.glob(os.path.join(ROOT, pat), recursive=True)
    return sorted(out)


def run_hook(hook, path):
    """훅에 합성 PostToolUse 입력을 먹이고 사람이 읽을 메시지를 돌려준다(없으면 None)."""
    r = subprocess.run(
        [sys.executable, "-X", "utf8", os.path.join(HOOKS, hook)],
        input=json.dumps({"tool_input": {"file_path": path}}),
        capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=ROOT,
    )
    out = (r.stdout or "").strip()
    if not out:
        return None
    try:
        d = json.loads(out)
    except ValueError:
        return f"[훅이 JSON이 아닌 출력을 냈다] {out[:200]}"
    if d.get("suppressOutput"):
        return None
    return (d.get("hookSpecificOutput", {}) or {}).get("additionalContext") or json.dumps(
        d, ensure_ascii=False
    )


def main():
    ap = argparse.ArgumentParser(description="CI에서 하네스 검사기 실행")
    ap.add_argument("--files", nargs="*", help="검사할 .cs (생략 시 전체)")
    args = ap.parse_args()

    targets = args.files or cs_files()
    violations, broken = [], []

    print(f"# ci-check — 하네스 검사기 재사용 ({len(targets)}개 .cs)\n")

    print("## 파일 단위 훅")
    for hook in BLOCKING_HOOKS:
        hits = []
        for f in targets:
            msg = run_hook(hook, f)
            if msg:
                rel = os.path.relpath(f, ROOT).replace(os.sep, "/")
                hits.append((rel, " ".join(msg.split())))
        print(f"- {hook}: {'위반 ' + str(len(hits)) + '건' if hits else f'통과 ({len(targets)}개 검사)'}")
        for rel, msg in hits:
            print(f"    {rel}\n      {msg[:200]}")
        violations += hits

    print("\n## 저장소 단위 검사")
    for cmd, label in REPO_CHECKS:
        r = subprocess.run(
            [sys.executable, "-X", "utf8", os.path.join(SCRIPTS, cmd[0])] + cmd[1:],
            capture_output=True, text=True, encoding="utf-8", errors="replace", cwd=ROOT,
        )
        if r.returncode == 0:
            print(f"- {label}: 통과")
        elif r.returncode == 2:
            print(f"- {label}: **검사기 고장 (exit 2)**")
            broken.append((label, (r.stdout or "") + (r.stderr or "")))
        else:
            print(f"- {label}: **위반 (exit {r.returncode})**")
            tail = [l for l in (r.stdout or "").splitlines() if "FAIL" in l or "요약" in l]
            for l in tail[:8]:
                print(f"    {l.strip()[:200]}")
            violations.append((label, f"exit {r.returncode}"))

    print()
    if broken:
        print("## 검사기 고장")
        for label, out in broken:
            print(f"- {label}")
            for l in out.strip().splitlines()[-4:]:
                print(f"    {l.strip()[:200]}")
        print("\n검사기가 코드를 못 따라갔다. 결과를 신뢰할 수 없으니 검사기부터 고칠 것.")
        return 2
    if violations:
        print(f"## 결과: 위반 {len(violations)}건 — 실패")
        return 1
    print("## 결과: 통과")
    return 0


if __name__ == "__main__":
    sys.exit(main())
