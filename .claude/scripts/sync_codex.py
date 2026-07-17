"""Codex 미러(.codex/, AGENTS.md)를 .claude/ 원본과 동기화한다.

배경: AGENTS.md는 CLAUDE.md의 사본이고 .codex/hooks/*.py는 .claude/hooks/*.py의
사본이다. 손으로 유지하다 보니 반드시 어긋났다 — 실측 시점에 훅 7개 중 6개가
드리프트했고, AGENTS.md는 CLAUDE.md의 경로를 `.Codex/`로 바꿔 적었는데 그런
디렉토리는 존재하지도 않았다(정작 .codex/hooks는 `.claude/audit-progress.md`를
읽는다). 그래서 변환하지 않고 그대로 복사한다 — .claude/가 단일 출처.

사용법:
  python -X utf8 .claude/scripts/sync_codex.py --check   # 드리프트만 보고, 있으면 exit 1
  python -X utf8 .claude/scripts/sync_codex.py --write   # 동기화 수행
  python -X utf8 .claude/scripts/sync_codex.py --hook    # PostToolUse 훅 모드 (stdin JSON)
"""

import io
import json
import os
import shutil
import sys
from glob import glob

try:
    sys.stdin = io.TextIOWrapper(sys.stdin.buffer, encoding="utf-8", errors="replace")
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
except Exception:
    pass

ROOT = os.environ.get("CLAUDE_PROJECT_DIR") or os.getcwd()


def pairs():
    """(원본, 미러) 목록. 전부 무변환 복사 대상."""
    out = [(os.path.join(ROOT, "CLAUDE.md"), os.path.join(ROOT, "AGENTS.md"))]
    for src in sorted(glob(os.path.join(ROOT, ".claude", "hooks", "*.py"))):
        out.append((src, os.path.join(ROOT, ".codex", "hooks", os.path.basename(src))))
    return out


def read(path):
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as fh:
            return fh.read()
    except Exception:
        return None


def drifted():
    """동기화가 필요한 (원본, 미러) 목록."""
    out = []
    for src, dst in pairs():
        s = read(src)
        if s is None:
            continue  # 원본이 없으면 미러도 관리 대상 아님
        if read(dst) != s:
            out.append((src, dst))
    return out


def sync(targets):
    done = []
    for src, dst in targets:
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copyfile(src, dst)
        done.append(os.path.relpath(dst, ROOT).replace("\\", "/"))
    return done


def main():
    argv = sys.argv[1:]

    if "--hook" in argv:
        # PostToolUse: CLAUDE.md나 .claude/hooks/*.py를 건드렸을 때만 동작.
        try:
            d = json.loads(sys.stdin.read())
        except Exception:
            print(json.dumps({"suppressOutput": True}))
            return 0

        fp = (
            d.get("tool_response", {}).get("filePath", "")
            or d.get("tool_input", {}).get("file_path", "")
        ).replace("\\", "/")

        watched = fp.endswith("/CLAUDE.md") or fp.endswith("CLAUDE.md") or (
            "/.claude/hooks/" in fp and fp.endswith(".py")
        )
        if not watched:
            print(json.dumps({"suppressOutput": True}))
            return 0

        targets = drifted()
        if not targets:
            print(json.dumps({"suppressOutput": True}))
            return 0

        done = sync(targets)
        print(json.dumps({
            "hookSpecificOutput": {
                "hookEventName": "PostToolUse",
                "additionalContext": (
                    "SYNC-CODEX: Codex 미러를 자동 동기화했습니다 — "
                    + ", ".join(done)
                    + ". 커밋 시 함께 포함하세요."
                ),
            }
        }))
        return 0

    targets = drifted()

    if "--check" in argv:
        if not targets:
            print("codex 미러 동기 상태 — 드리프트 없음")
            return 0
        print(f"드리프트 {len(targets)}건:")
        for src, dst in targets:
            print(f"  {os.path.relpath(src, ROOT)}  ->  {os.path.relpath(dst, ROOT)}")
        print("\n동기화: python -X utf8 .claude/scripts/sync_codex.py --write")
        return 1

    if "--write" in argv:
        if not targets:
            print("이미 동기 상태 — 할 일 없음")
            return 0
        for p in sync(targets):
            print(f"synced {p}")
        return 0

    print(__doc__)
    return 0


if __name__ == "__main__":
    sys.exit(main())
