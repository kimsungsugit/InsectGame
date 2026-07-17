"""audit 후보 자동 발굴 — Uncovered 큐가 비면 여기서 다시 채운다.

배경: Uncovered는 사람이 손으로 유지하는 큐였다. 2026-05-27 라운드 이후
Social/PvP/NPC/Village/IAP/Minimap 기능이 대량 유입됐는데 아무도 큐를 채우지
않았고, 큐가 0이 되자 audit_flow_inject/audit_reminder 두 훅이 함께 침묵해
자동화 전체가 멈췄다. 완료 항목을 옮기는 것만으로는 다음 소진 때 같은 일이
반복되므로, 큐 보충 자체를 자동화한다.

기처리 영역은 audit-progress.md 본문에서 읽는다 — 하드코딩 목록이 없으므로
stale해질 수 없다.

채점은 audit 스킬이 실제로 잡아온 회귀 클래스를 그대로 쓴다:
  hot  = OnGUI/Update 안의 new GUIStyle/Color/Rect  (매 프레임 GC 압박)
  find = FindFirstObjectByType / GameObject.Find    (미캐싱 조회)
  leak = 이벤트 += 대비 -= 부족분                    (구독 해제 누락)
  inst = 싱글턴 .Instance 직접 사용                  (null 가드 필요)
  score = hot*3 + find*2 + leak*2 + inst

사용법:
  python -X utf8 .claude/scripts/audit_candidates.py             # 표로 확인
  python -X utf8 .claude/scripts/audit_candidates.py --emit-md   # 큐에 붙일 - [ ] 줄
  python -X utf8 .claude/scripts/audit_candidates.py --emit-md --top 15
"""

import io
import os
import re
import sys
from glob import glob

try:
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
except Exception:
    pass

ROOT = os.environ.get("CLAUDE_PROJECT_DIR") or os.getcwd()
PROGRESS = os.path.join(ROOT, ".claude", "audit-progress.md")
ARCHIVE_DIR = os.path.join(ROOT, ".claude", "audit-archive")
SCRIPTS_DIR = os.path.join(ROOT, "Assets", "Scripts")

MIN_LOC = 80  # 이보다 작은 파일은 audit 라운드를 열 만한 표면이 없다


def strip_cs(s):
    """주석·문자열 제거. { } 나 new Color가 주석/리터럴 안에서 오탐되는 걸 막는다."""
    s = re.sub(r"//[^\n]*", "", s)
    s = re.sub(r"/\*[\s\S]*?\*/", "", s)
    s = re.sub(r"'(?:\\.|[^'\\])'", "", s)
    s = re.sub(r'"(?:\\.|[^"\\])*"', "", s)
    s = re.sub(r'@"(?:[^"]|"")*"', "", s)
    return s


def read_reviewed_text():
    """이미 다룬 영역을 판별할 근거 텍스트(본문 + 아카이브)."""
    chunks = []
    for path in [PROGRESS] + sorted(glob(os.path.join(ARCHIVE_DIR, "*.md"))):
        try:
            with open(path, "r", encoding="utf-8", errors="replace") as fh:
                chunks.append(fh.read())
        except Exception:
            pass
    return "\n".join(chunks)


def has_frame_method(cleaned):
    """OnGUI/Update/LateUpdate/FixedUpdate 보유 여부 = 매 프레임 실행 파일인가."""
    return bool(
        re.search(
            r"\bvoid\s+(?:OnGUI|Update|LateUpdate|FixedUpdate)\s*\([^)]*\)",
            cleaned,
        )
    )


def score_file(path):
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as fh:
            text = fh.read()
    except Exception:
        return None

    loc = text.count("\n") + 1
    if loc < MIN_LOC:
        return None

    cleaned = strip_cs(text)

    # hot: 프레임 메서드를 가진 파일의 스타일/색 할당 총량.
    # OnGUI 본문만 보면 안 된다 — 이 코드베이스의 OnGUI는 대개 DrawPanel() 같은
    # 하위 메서드로 위임하고, 과거 라운드가 실제로 잡아온 핫스팟도 그 하위
    # 메서드들이었다(DrawInsectItem 등). 후보 발굴은 "열어볼 가치"를 재는 것이므로
    # 다소 과대평가가 과소평가보다 낫다. 실제 판정은 Explore가 파일을 읽고 한다.
    hot = 0
    if has_frame_method(cleaned):
        hot += len(re.findall(r"\bnew\s+(?:GUIStyle|GUIContent|Texture2D)\b", cleaned)) * 2
        hot += len(re.findall(r"\bnew\s+(?:Color|Rect)\b", cleaned))

    find = len(re.findall(r"\bFindFirstObjectByType\b|\bFindObjectOfType\b|GameObject\.Find\b", cleaned))

    subs = len(re.findall(r"\+=\s*(?:new\s+\w+\s*\()?\s*(?:On|Handle)\w+", cleaned))
    unsubs = len(re.findall(r"-=\s*(?:new\s+\w+\s*\()?\s*(?:On|Handle)\w+", cleaned))
    leak = max(0, subs - unsubs)

    inst = len(re.findall(r"\b\w+\.Instance\b", cleaned))

    score = hot * 3 + find * 2 + leak * 2 + inst
    return {
        "path": path,
        "loc": loc,
        "hot": hot,
        "find": find,
        "leak": leak,
        "inst": inst,
        "score": score,
    }


def reason_of(r):
    bits = []
    if r["hot"]:
        bits.append(f"프레임 할당 {r['hot']}")
    if r["find"]:
        bits.append(f"미캐싱 조회 {r['find']}")
    if r["leak"]:
        bits.append(f"구독 해제 누락 의심 {r['leak']}")
    if r["inst"]:
        bits.append(f"싱글턴 참조 {r['inst']}")
    return ", ".join(bits) if bits else "표면 점검"


def main():
    emit_md = "--emit-md" in sys.argv
    top = 15
    if "--top" in sys.argv:
        try:
            top = int(sys.argv[sys.argv.index("--top") + 1])
        except Exception:
            pass

    reviewed = read_reviewed_text()

    files = []
    for dirpath, _dirnames, filenames in os.walk(SCRIPTS_DIR):
        for fn in filenames:
            if fn.endswith(".cs"):
                files.append(os.path.join(dirpath, fn))

    results = []
    for path in sorted(files):
        stem = os.path.splitext(os.path.basename(path))[0]
        if stem in reviewed:
            continue  # 이미 다룬 영역
        r = score_file(path)
        if r and r["score"] > 0:
            r["stem"] = stem
            r["rel"] = os.path.relpath(path, SCRIPTS_DIR).replace("\\", "/")
            results.append(r)

    results.sort(key=lambda x: -x["score"])
    picked = results[:top]

    if not picked:
        print("후보 0건 — 미검토 .cs 중 점검 표면이 있는 파일이 없습니다.")
        return 0

    if emit_md:
        for r in picked:
            print(
                f"- [ ] {r['stem']} ({r['rel']}, {r['loc']}줄, score {r['score']}) "
                f"— {reason_of(r)}"
            )
    else:
        total = len(results)
        print(f"미검토 .cs 중 점검 표면 보유: {total}개 (상위 {len(picked)}개 표시)\n")
        print(f"{'score':>5} {'LOC':>5} {'hot':>4} {'find':>4} {'leak':>4} {'inst':>4}  file")
        for r in picked:
            print(
                f"{r['score']:>5} {r['loc']:>5} {r['hot']:>4} {r['find']:>4} "
                f"{r['leak']:>4} {r['inst']:>4}  {r['rel']}"
            )
        print("\n큐에 넣을 마크다운: --emit-md")
    return 0


if __name__ == "__main__":
    sys.exit(main())
