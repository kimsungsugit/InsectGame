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


def unregistered_hooks():
    """.claude/settings.json에 등록됐지만 .codex/hooks.json에 없는 훅 목록.

    pairs()는 .py 파일만 복사한다. 훅 **등록**은 못 옮긴다 — 두 파일의 형식이 달라서다
    (.codex/hooks.json은 절대경로를 박은 별도 스키마). 그래서 신규 훅을 추가하면 .py는
    자동으로 미러되는데 codex 쪽 등록은 사람이 손으로 해야 하고, 그 손이 다음 드리프트의
    씨앗이다. 자동으로 못 고치면 최소한 시끄럽게 알린다.
    """
    def _load(p):
        try:
            with open(p, encoding="utf-8") as fh:
                return json.load(fh)
        except Exception:
            return None

    claude = _load(os.path.join(ROOT, ".claude", "settings.json"))
    codex = _load(os.path.join(ROOT, ".codex", "hooks.json"))
    if claude is None or codex is None:
        return []

    def _names(cfg):
        found = set()
        for entries in (cfg.get("hooks", {}) or {}).values():
            for entry in entries:
                for h in entry.get("hooks", []):
                    cmd = h.get("command", "")
                    for tok in cmd.replace("\\", "/").split():
                        # 따옴표를 **먼저** 벗긴다. .codex/hooks.json은 절대경로를 작은따옴표로
                        # 감싸므로("python -X utf8 'C:/…/warn_monolith.py'") 벗기기 전에
                        # endswith(".py")를 보면 전부 놓친다.
                        tok = tok.strip("'\"")
                        if tok.endswith(".py"):
                            found.add(os.path.basename(tok))
        return found

    missing = _names(claude) - _names(codex)
    # scripts/ 의 것(sync_codex 자신 등)은 .codex가 자체 등록하지 않아도 무방하다.
    hook_files = {os.path.basename(p) for p in glob(os.path.join(ROOT, ".claude", "hooks", "*.py"))}
    return sorted(missing & hook_files)


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
        unreg = unregistered_hooks()
        if not targets and not unreg:
            print("codex 미러 동기 상태 — 드리프트 없음")
            return 0
        if targets:
            print(f"파일 드리프트 {len(targets)}건:")
            for src, dst in targets:
                print(f"  {os.path.relpath(src, ROOT)}  ->  {os.path.relpath(dst, ROOT)}")
            print("\n동기화: python -X utf8 .claude/scripts/sync_codex.py --write")
        if unreg:
            # .py는 자동 복사되지만 등록은 형식이 달라 못 옮긴다. 손으로 해야 하므로 알린다.
            print(f"\n.codex/hooks.json 미등록 훅 {len(unreg)}건:")
            for h in unreg:
                print(f"  {h}  — .claude/settings.json엔 있으나 .codex/hooks.json엔 없다")
            print("\n두 파일은 스키마가 달라 자동 복사가 불가하다. .codex/hooks.json에 직접 등록할 것.")
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
