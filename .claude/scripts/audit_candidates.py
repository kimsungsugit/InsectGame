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


INIT_SIG = re.compile(r"\bvoid\s+(?:Awake|Start|OnEnable|AutoWire)\s*\([^)]*\)\s*\{")


def strip_init_bodies(cleaned):
    """Awake/Start/OnEnable/AutoWire 본문을 제거한다.

    부트스트랩 시점의 조회는 이 프로젝트의 정상 패턴이다(AutoWire 캐싱). 그걸 빼지 않으면
    초기화 전용 파일이 '미캐싱 조회'로 큐 최상위에 올라온다 — SceneAutoWire가 실제로 그렇게
    올라왔고(106줄 전체가 Awake, 조회 40건) 라운드를 통째로 낭비시켰다.
    """
    while True:
        m = INIT_SIG.search(cleaned)
        if not m:
            return cleaned
        depth = 1
        i = m.end()
        while i < len(cleaned) and depth > 0:
            c = cleaned[i]
            if c == "{":
                depth += 1
            elif c == "}":
                depth -= 1
            i += 1
        cleaned = cleaned[:m.start()] + cleaned[i:]


def scene_script_guids(root):
    """씬에 실제로 배치된 스크립트의 GUID 집합."""
    guids = set()
    for scene in glob(os.path.join(root, "Assets", "**", "*.unity"), recursive=True):
        try:
            with open(scene, "r", encoding="utf-8", errors="replace") as fh:
                guids.update(re.findall(r"m_Script:.*?guid:\s*([0-9a-f]{32})", fh.read()))
        except Exception:
            pass
    return guids


def file_guid(cs_path):
    try:
        with open(cs_path + ".meta", "r", encoding="utf-8", errors="replace") as fh:
            m = re.search(r"guid:\s*([0-9a-f]{32})", fh.read())
            return m.group(1) if m else None
    except Exception:
        return None


_ALL_CODE = None


def code_mentions(stem, self_path):
    """자기 자신 말고 다른 .cs가 이 클래스명을 **실제 코드에서** 언급하는가.

    주석은 제거하고 본다. 주석 처리로 꺼둔 배선도 '언급'으로 세면 dead code가 살아있는
    것으로 잡힌다 — CaptureItemSpawner가 실제로 그랬다(PlaySceneBootstrap이
    `// 필드 아이템 스폰 비활성화` 주석으로 꺼놨는데 그 주석 때문에 살아있는 것으로 판정).

    한계: 참조를 1단계만 본다. A가 dead인데 A만 B를 부르면 B는 여전히 살아있는 것으로
    잡힌다(CaptureItemPickup ← CaptureItemSpawner). 전이 폐쇄까지 하려면 반복 계산이
    필요한데, 후보 발굴 용도에는 과하다 — Explore가 파일을 열면 금방 드러난다.
    """
    global _ALL_CODE
    if _ALL_CODE is None:
        _ALL_CODE = {}
        for dirpath, _d, filenames in os.walk(SCRIPTS_DIR):
            for fn in filenames:
                if not fn.endswith(".cs"):
                    continue
                p = os.path.join(dirpath, fn)
                try:
                    with open(p, "r", encoding="utf-8", errors="replace") as fh:
                        _ALL_CODE[p] = strip_cs(fh.read())
                except Exception:
                    pass
    # 경로는 반드시 정규화해서 비교한다. os.walk는 OS 구분자(Windows면 백슬래시)를 주는데
    # 호출부가 슬래시 경로를 넘기면 자기 자신이 "다른 파일"로 잡혀 항상 True가 된다.
    me = os.path.normcase(os.path.abspath(self_path))
    needle = re.compile(r"\b" + re.escape(stem) + r"\b")
    for p, body in _ALL_CODE.items():
        if os.path.normcase(os.path.abspath(p)) == me:
            continue
        if needle.search(body):
            return True
    return False


def score_file(path, scene_guids):
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as fh:
            text = fh.read()
    except Exception:
        return None

    loc = text.count("\n") + 1
    if loc < MIN_LOC:
        return None

    cleaned = strip_cs(text)

    # dead code 제외: MonoBehaviour인데 씬에 배치되지도, 다른 코드에서 언급되지도 않으면
    # 실행 자체가 되지 않으므로 점검할 가치가 없다. SceneAutoWire가 이 필터가 없어서
    # 큐 상위를 차지했다(조회 40건이 전부 실행되지 않는 Awake 안에 있었다).
    if "MonoBehaviour" in cleaned:
        guid = file_guid(path)
        stem = os.path.splitext(os.path.basename(path))[0]
        in_scene = guid is not None and guid in scene_guids
        if not in_scene and not code_mentions(stem, path):
            return None

    # hot: 프레임 메서드를 가진 파일의 **힙** 할당 총량.
    # OnGUI 본문만 보면 안 된다 — 이 코드베이스의 OnGUI는 대개 DrawPanel() 같은
    # 하위 메서드로 위임하고, 과거 라운드가 실제로 잡아온 핫스팟도 그 하위
    # 메서드들이었다(DrawInsectItem 등).
    #
    # new Color/Rect/Vector3는 **세지 않는다** — struct라 스택 할당이고 GC가 없다.
    # 옛 채점은 이걸 세서 6라운드 연속 1위 근거가 전부 거짓양성이었다
    # (WorldFieldMultiplayerUI 51, SubAreaEnvironment 46, AccountSettingsUI 37 …).
    hot = 0
    if has_frame_method(cleaned):
        hot += len(re.findall(r"\bnew\s+(?:GUIStyle|GUIContent|Texture2D|Material)\b", cleaned)) * 2
        hot += len(re.findall(r"\bnew\s+(?:List|Dictionary|HashSet)\s*<", cleaned))

    # find: 초기화 본문(Awake/Start/OnEnable/AutoWire)을 뺀 나머지의 조회만 센다.
    runtime = strip_init_bodies(cleaned)
    find = len(re.findall(r"\bFindFirstObjectByType\b|\bFindObjectOfType\b|GameObject\.Find\b", runtime))

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
    scene_guids = scene_script_guids(ROOT)

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
        # score가 0이어도 미검토 파일은 후보다. 채점은 **우선순위**를 매기는 도구이지
        # clean 판정 도구가 아니다 — 실제로 2026-07-17 라운드들에서 채점 근거(struct 할당)는
        # 매번 틀렸는데도 지목된 파일마다 진짜 P0/P1이 나왔다. 점수로 거르면 그런 파일을
        # 통째로 놓친다. 제외는 dead code(score_file이 None 반환)만 한다.
        r = score_file(path, scene_guids)
        if r:
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
