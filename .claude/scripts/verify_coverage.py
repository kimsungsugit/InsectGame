"""에이전트 커버리지 재검증 — 매핑은 문서에서 읽는다.

여기엔 한때 에이전트↔파일 매핑이 144줄짜리 dict로 하드코딩돼 있었다.
`.claude/rules/agent-coordination.md`가 스스로 이렇게 경고할 정도였다:

    "주의: 이 스크립트의 에이전트↔파일 매핑은 하드코딩이라 이 문서·agents/*.md와
     따로 논다. 결과는 '확실한 미할당'이 아니라 **후보**로 읽고..."

실제로 따로 놀았다 — 표에 등장하는 22개 파일 중 8개가 어긋났고 (에이전트,파일) 쌍
10건이 누락됐다. 실질 피해도 있었다: UIScale.cs는 ui-dev.md가 담당으로 명시하는데
매핑에 없어 "미할당"에 거짓으로 올랐다. 경고문을 다는 대신 사본을 없앤다.

단일 출처는 둘이다:
  - `.claude/agents/*.md`             전체 소유권 (불릿의 `Assets/…​.cs`)
  - `.claude/rules/agent-coordination.md`  공유 파일의 (에이전트, 파일) 경계 표

파싱이 아무것도 못 건지면 exit 2로 죽는다 — 빈 매핑으로 "전부 미할당"을 보고하는
게 정확히 이 스크립트가 저지르던 종류의 거짓말이다.
"""
import glob, io, os, re, sys

try:
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
except Exception:
    pass

ROOT = os.environ.get("CLAUDE_PROJECT_DIR") or os.getcwd()
AGENT_DIR = os.path.join(ROOT, ".claude", "agents")
COORD_MD = os.path.join(ROOT, ".claude", "rules", "agent-coordination.md")

# 스템 중간의 점을 허용한다 — `RaidBattleUI.Draw.cs` 같은 partial 분할 파일.
# 예전엔 `[A-Za-z0-9_/]+\.cs`라 점이 든 이름이 **아무 경고 없이 매칭에서 빠졌다**.
# 그런 파일은 agents/*.md에 제대로 적어둬도 파서엔 안 보여, coordination 표가 우연히
# 받아주지 않으면 "미할당"으로 뜨거나(거짓 양성) 공유 표기가 통째로 사라진다(거짓 음성).
_PATH = re.compile(r"Assets/(?:Scripts|Editor)/[A-Za-z0-9_/]+(?:\.[A-Za-z0-9_]+)*\.cs")
_BARE = re.compile(r"`([A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)*\.cs)`")


class MappingBroken(Exception):
    """문서에서 매핑을 못 읽었다 — 데이터가 아니라 이 스크립트의 문제."""


def _read(path):
    try:
        with open(path, encoding="utf-8") as fh:
            return fh.read()
    except OSError as e:
        raise MappingBroken(f"{path}를 읽을 수 없다: {e}")


def owners_from_agent_docs():
    """{에이전트: {파일}} — agents/*.md 불릿에서 추출."""
    out = {}
    docs = sorted(glob.glob(os.path.join(AGENT_DIR, "*.md")))
    if not docs:
        raise MappingBroken(f"{AGENT_DIR}에 에이전트 문서가 없다 — 디렉토리가 옮겨갔는가?")
    for doc in docs:
        name = os.path.splitext(os.path.basename(doc))[0]
        files = set()
        for line in _read(doc).splitlines():
            paths = _PATH.findall(line)
            files.update(paths)
            # `PlayUIConfig.cs` + `PlayUIRefs.cs` 같은 병렬 표기 — 뒤쪽은 경로가 없다.
            # 같은 줄의 앞선 전체 경로에서 디렉토리를 물려받는다.
            if paths:
                d = os.path.dirname(paths[-1])
                for bare in _BARE.findall(line):
                    if not any(p.endswith("/" + bare) for p in paths):
                        files.add(f"{d}/{bare}")
        if files:
            out[name] = files
    if not out:
        raise MappingBroken(
            "agents/*.md에서 담당 파일을 하나도 못 읽었다 — 불릿 형식이 바뀌었는가? "
            "(기대 형식: - `Assets/Scripts/…/X.cs` - 설명)"
        )
    return out


def owners_from_coord_table(known_agents):
    """{에이전트: {파일}} — agent-coordination.md의 공유 파일 경계 표에서 추출.

    표 규칙 셋: ① 파일 셀이 비면 이전 행 상속 ② 백틱 제거
    ③ "RegionManager.cs SubArea 처리" 같은 접미사는 첫 토큰만.
    """
    out = {}
    cur = None
    found_row = False
    for line in _read(COORD_MD).splitlines():
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 2 or set(cells[0]) <= set("- :"):
            continue
        f = cells[0].replace("`", "").strip()
        if f:
            f = f.split()[0]
            if f.endswith(".cs"):
                cur = f
            elif not f.endswith(".cs"):
                continue
        if cur is None:
            continue
        agent = cells[1].replace("`", "").strip()
        if agent in known_agents:
            out.setdefault(agent, set()).add(cur)
            found_row = True
    if not found_row:
        raise MappingBroken(
            f"{COORD_MD}의 공유 파일 표에서 (에이전트, 파일) 쌍을 하나도 못 읽었다 — "
            "표 구조가 바뀌었는가?"
        )
    return out


def build_mapping():
    """agents/*.md(전체 소유권) + coordination 표(공유 경계)를 합친다.

    표는 basename만 준다(`PlayerVisualBuilder.cs`). 전체 경로는 **파일시스템**에서
    찾는다 — agents/*.md의 경로 목록으로만 해석하면, 표에는 있는데 어느 에이전트
    문서에도 안 적힌 파일(PlayerVisualBuilder, ModalUIRegistry 등)이 통째로 누락된다.
    "그 파일이 어디 있나"의 단일 출처는 디스크지 문서가 아니다.
    """
    by_agent = owners_from_agent_docs()

    base_to_path = {}
    for p in glob.glob("Assets/Scripts/**/*.cs", recursive=True) + glob.glob(
        "Assets/Editor/**/*.cs", recursive=True
    ):
        p = p.replace(os.sep, "/")
        base_to_path.setdefault(os.path.basename(p), set()).add(p)

    for agent, bases in owners_from_coord_table(set(by_agent)).items():
        for b in bases:
            for full in base_to_path.get(b, set()):
                by_agent.setdefault(agent, set()).add(full)
    return {k: sorted(v) for k, v in by_agent.items()}


try:
    agents = build_mapping()
except MappingBroken as e:
    print(f"매핑 파싱 실패: {e}\n")
    print("에이전트↔파일 매핑을 문서에서 읽지 못했다. 커버리지 결과는 신뢰할 수 없다 —")
    print("빈 매핑이면 모든 파일이 '미할당'으로 보인다. 파서를 먼저 고칠 것.")
    sys.exit(2)


def n(p):
    return p.replace(chr(92), "/")

# Assets/Editor도 센다. Scripts만 훑으면 ui-dev가 담당으로 적어둔
# Assets/Editor/PlayUIPrefabGenerator.cs가 "실제 없는 파일(고스트)"로 오보고된다.
# 에디터 스크립트도 담당자가 필요한 .cs다.
actual = set(
    n(f)
    for f in glob.glob("Assets/Scripts/**/*.cs", recursive=True)
    + glob.glob("Assets/Editor/**/*.cs", recursive=True)
)
covered = set()
for files in agents.values():
    covered.update(files)

print("=== 커버리지 요약 ===")
print(f"전체 .cs 파일: {len(actual)}개")
print(f"에이전트 커버: {len(actual & covered)}개")
uncovered = actual - covered
print(f"미할당: {len(uncovered)}개")
if uncovered:
    print()
    print("=== 여전히 미할당인 파일 ===")
    for f in sorted(uncovered):
        print(f"  {f}")

ghost = covered - actual
if ghost:
    print()
    print("=== 실제 없는 파일 (고스트) ===")
    for f in sorted(ghost):
        print(f"  {f}")

print()
print("=== 에이전트별 파일 수 ===")
for name, files in sorted(agents.items()):
    real = len(set(files) & actual)
    print(f"  {name:20s}: {real:3d}개")

print()
print("=== 의도적 공유 파일 ===")
file_owners = {}
for name, files in agents.items():
    for f in files:
        file_owners.setdefault(f, []).append(name)
for f, owners in sorted(file_owners.items()):
    if len(owners) > 1:
        fname = f.split("/")[-1]
        print(f"  {fname:40s} <- {', '.join(owners)}")
