"""audit 후보 자동 발굴 — Uncovered 큐가 비면 여기서 다시 채운다.

배경: Uncovered는 사람이 손으로 유지하는 큐였다. 2026-05-27 라운드 이후
Social/PvP/NPC/Village/IAP/Minimap 기능이 대량 유입됐는데 아무도 큐를 채우지
않았고, 큐가 0이 되자 audit_flow_inject/audit_reminder 두 훅이 함께 침묵해
자동화 전체가 멈췄다. 완료 항목을 옮기는 것만으로는 다음 소진 때 같은 일이
반복되므로, 큐 보충 자체를 자동화한다.

기처리 영역은 audit-progress.md 본문에서 읽는다 — 하드코딩 목록이 없으므로
stale해질 수 없다.

후보는 두 종류다:
  신규   = 진척 문서에 이름이 한 번도 안 나온 .cs
  재감사 = 나왔지만 **그 감사 이후 크게 바뀐** .cs

재감사가 왜 필요한가
--------------------
예전엔 `stem in reviewed`(원문 substring)로 이름이 한 번이라도 스치면 **영구 제외**했다.
"처리했는데 그 뒤로 바뀐 파일"이라는 개념이 없어서, 2026-08-03에 실측했을 때 감사 이후
수정된 파일이 76개인데도 "후보 0건"을 냈다. 그 사각지대에 있던 InsectBattleController
(2026-05-20 감사 후 StartDuel·버프 상한·DuelEnded 유입)에서 곧바로 P1이 나왔다 —
의상·아이템 보너스가 전투 첫 턴에 안 붙는 결함이었다.

재감사 우선순위는 **점수가 아니라 감사 이후 변경량(git numstat)**이다. 점수는 파일의
성격(프레임 할당·미캐싱 조회 등)을 말할 뿐 "감사 이후 무엇이 달라졌는가"를 말하지 않는다.
MIN_RECHURN 미만으로 바뀐 파일은 다시 올리지 않는다 — 오타 한 줄에 전 파일이 큐로
되돌아오면 큐가 의미를 잃는다.

git을 못 읽으면 재감사 판정을 건너뛰고 신규 후보만 낸다(그 사실을 출력에 적는다).
파일 mtime은 체크아웃마다 리셋돼 폴백으로 쓰지 않는다.

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

import datetime
import io
import os
import re
import subprocess
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

# 재감사 하한 — 감사 이후 이만큼(추가+삭제 줄)은 바뀌어야 다시 볼 값어치가 있다.
# 0으로 두면 오타 수정 한 줄에도 전 파일이 큐로 되돌아와 큐가 의미를 잃는다.
MIN_RECHURN = 40


sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from cs_strip import strip_cs  # noqa: E402,F401  — 주석/문자열 제거는 cs_strip이 소유



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


# ── 재감사(re-audit) ──
#
# 기존엔 이름이 진척 문서에 한 번이라도 스치면 `stem in reviewed`(substring)로 **영구 제외**됐다.
# "처리했는데 그 뒤로 크게 바뀐 파일"이라는 개념이 없어서, 2026-08-03에 실측했을 때
# **감사 이후 수정된 파일이 76개**인데 스크립트는 "후보 0건"을 냈다. 그 사각지대에 있던
# `InsectBattleController`(2026-05-20 감사 후 StartDuel·버프 상한·DuelEnded 유입)에서
# 곧바로 P1이 나왔다 — 의상·아이템 보너스가 전투 첫 턴에 안 붙는 결함이었다.
#
# 그래서 "이름이 있으면 제외"는 그대로 두되(신규 후보 집합을 넓히지 않는다),
# **마지막 감사일 < 마지막 수정일**이면 재감사 후보로 되살린다.

DATE_RE = re.compile(r"(\d{4}-\d{2}-\d{2})")
# Covered 인덱스:  - [x] Foo + Bar (P1:1, 2026-05-21) — 서술
COVERED_RE = re.compile(r"^-\s*\[x\]\s*([^(\n]+?)\s*\(([^)\n]*)\)")
# Round Log:       - 2026-08-03: Foo (score 0 …) — 서술
ROUNDLOG_RE = re.compile(r"^-\s*(\d{4}-\d{2}-\d{2}):\s*([^(\n—-]+)")
NAME_RE = re.compile(r"\b([A-Z][A-Za-z0-9_]*)\b")


def read_reviewed_dates(text):
    """{클래스명: 가장 늦은 감사일}. 서술 본문이 아니라 **항목 머리**에서만 읽는다.

    프로즈에 스친 이름까지 세면 "다른 라운드에서 언급됨 = 최근 감사됨"이 되어
    정작 오래된 파일의 재감사를 막는다.
    """
    dates = {}

    def put(names_blob, day):
        for name in NAME_RE.findall(names_blob):
            if len(name) < 3:
                continue
            if name not in dates or day > dates[name]:
                dates[name] = day

    for line in text.splitlines():
        m = COVERED_RE.match(line)
        if m:
            found = DATE_RE.findall(m.group(2))
            if found:
                put(m.group(1), max(found))
            continue
        m = ROUNDLOG_RE.match(line)
        if m:
            put(m.group(2), m.group(1))
    return dates


def git_history():
    """{저장소 상대경로: [(날짜, 변경줄수), …]} — 최신순. git 이력 + 워킹트리 변경.

    날짜만으로 재감사를 정하면 "5월 감사 후 3줄만 고친 파일"이 "통째로 다시 쓴 파일"과
    같은 무게가 된다. 그래서 커밋별 변경량(numstat)까지 들고 와, 감사일 이후의 변경량을
    합쳐 우선순위를 매긴다.

    git을 못 쓰면 빈 dict를 돌려주고 호출부는 재감사 없이 기존 동작으로 되돌아간다
    (파일 mtime은 체크아웃마다 리셋돼 신뢰할 수 없으므로 폴백으로 쓰지 않는다).
    """
    out = {}
    try:
        log = subprocess.run(
            ["git", "log", "--format=%x01%ad", "--date=short", "--numstat", "--", "Assets/Scripts"],
            cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=180,
        )
        if log.returncode != 0:
            return {}
        day = None
        for line in log.stdout.splitlines():
            if line.startswith("\x01"):
                day = line[1:].strip()
                continue
            parts = line.split("\t")
            if len(parts) != 3 or not day:
                continue
            add, dele, rel = parts
            if not rel.endswith(".cs"):
                continue
            n = (int(add) if add.isdigit() else 0) + (int(dele) if dele.isdigit() else 0)
            out.setdefault(rel.replace("\\", "/"), []).append((day, n))

        # 아직 커밋 안 된 변경은 "오늘" 자로 맨 앞에 얹는다.
        today = datetime.date.today().isoformat()
        diff = subprocess.run(
            ["git", "diff", "--numstat", "HEAD", "--", "Assets/Scripts"],
            cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=120,
        )
        if diff.returncode == 0:
            for line in diff.stdout.splitlines():
                parts = line.split("\t")
                if len(parts) != 3:
                    continue
                add, dele, rel = parts
                if not rel.endswith(".cs"):
                    continue
                n = (int(add) if add.isdigit() else 0) + (int(dele) if dele.isdigit() else 0)
                out.setdefault(rel.replace("\\", "/"), []).insert(0, (today, n))
    except Exception:
        return {}
    return out


def churn_since(records, since_day):
    """감사일 **이후** 커밋들의 변경 줄 수 합. 같은 날 감사·수정은 세지 않는다."""
    return sum(n for day, n in records if day > since_day)


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
    audited_on = read_reviewed_dates(reviewed)
    history = git_history()
    scene_guids = scene_script_guids(ROOT)

    files = []
    for dirpath, _dirnames, filenames in os.walk(SCRIPTS_DIR):
        for fn in filenames:
            if fn.endswith(".cs"):
                files.append(os.path.join(dirpath, fn))

    results = []
    for path in sorted(files):
        stem = os.path.splitext(os.path.basename(path))[0]
        rel_repo = os.path.relpath(path, ROOT).replace("\\", "/")
        restale = None
        if stem in reviewed:
            # 이미 다룬 영역이지만, 감사 이후 **의미 있게** 수정됐으면 재감사 후보로 되살린다.
            a = audited_on.get(stem)
            recs = history.get(rel_repo)
            if not (a and recs):
                continue
            m = recs[0][0]
            churn = churn_since(recs, a)
            if m <= a or churn < MIN_RECHURN:
                continue
            restale = (a, m, churn)
        # score가 0이어도 미검토 파일은 후보다. 채점은 **우선순위**를 매기는 도구이지
        # clean 판정 도구가 아니다 — 실제로 2026-07-17 라운드들에서 채점 근거(struct 할당)는
        # 매번 틀렸는데도 지목된 파일마다 진짜 P0/P1이 나왔다. 점수로 거르면 그런 파일을
        # 통째로 놓친다. 제외는 dead code(score_file이 None 반환)만 한다.
        r = score_file(path, scene_guids)
        if r:
            r["stem"] = stem
            r["rel"] = os.path.relpath(path, SCRIPTS_DIR).replace("\\", "/")
            r["restale"] = restale
            results.append(r)

    # 미검토(신규)를 먼저 — 아예 본 적이 없어 미지의 폭이 더 크다. 신규끼리는 점수순.
    # 재감사는 **감사 이후 변경량**이 1순위다. 점수(hot/find/inst)는 파일의 성격을 말할 뿐
    # "감사 이후 무엇이 달라졌는가"를 말해주지 않는다 — 5월 감사 후 3줄 고친 모놀리스가
    # 통째로 다시 쓴 파일보다 위에 오는 걸 막는다. 변경량이 같으면 점수로 가른다.
    results.sort(key=lambda x: (
        x["restale"] is not None,
        -(x["restale"][2] if x["restale"] else 0),
        -x["score"],
    ))
    picked = results[:top]

    if not picked:
        print(f"후보 0건 — 미검토 .cs가 없고, 감사 이후 {MIN_RECHURN}줄 이상 바뀐 .cs도 없습니다.")
        if not history:
            print("(git 이력을 못 읽어 재감사 판정을 건너뛰었습니다 — 신규 파일만 본 결과입니다.)")
        return 0

    if emit_md:
        for r in picked:
            if r["restale"]:
                a, m, churn = r["restale"]
                print(
                    f"- [ ] {r['stem']} 재감사 ({r['rel']}, {r['loc']}줄, score {r['score']}) "
                    f"— {a} 감사 이후 {m}까지 {churn}줄 변경"
                )
            else:
                print(
                    f"- [ ] {r['stem']} ({r['rel']}, {r['loc']}줄, score {r['score']}) "
                    f"— {reason_of(r)}"
                )
    else:
        fresh = [r for r in results if not r["restale"]]
        stale = [r for r in results if r["restale"]]
        print(f"후보 {len(results)}개 — 미검토 {len(fresh)} / 재감사 {len(stale)} (상위 {len(picked)}개 표시)\n")
        print(f"{'score':>5} {'LOC':>5} {'hot':>4} {'find':>4} {'leak':>4} {'inst':>4}  file")
        for r in picked:
            mark = (f"  [재감사 {r['restale'][0]}→{r['restale'][1]}, {r['restale'][2]}줄]"
                    if r["restale"] else "")
            print(
                f"{r['score']:>5} {r['loc']:>5} {r['hot']:>4} {r['find']:>4} "
                f"{r['leak']:>4} {r['inst']:>4}  {r['rel']}{mark}"
            )
        print("\n큐에 넣을 마크다운: --emit-md")
    return 0


if __name__ == "__main__":
    sys.exit(main())
