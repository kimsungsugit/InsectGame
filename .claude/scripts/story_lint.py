"""스토리 정합성 검증. 비트 ID·prerequisite·트리거 대상·분기 도달성·보상·트리거 배선 검사.

quest_lint의 형제다. 스토리 비트는 Story.json(json.load)이라 퀘스트 코드 배열의 정규식
파싱보다 견고하다 — 설계(Docs/StorySystemDesign.md)가 데이터 모델을 JSON으로 정한 이유.

가장 중요한 검사는 트리거 배선(검사 6)이다. 새 trigger.type을 JSON에 넣고 StoryDirector의
EvaluateTriggers switch 케이스나 이벤트 구독을 빠뜨리면, 그 타입을 쓰는 비트가 영영 발화하지
않는다. quest_lint의 "QuestType↔진행 배선"과 정확히 같은 구조 — q_team 회귀의 스토리 등가물.

종료 코드: 0 정상 / 1 데이터 결함 / 2 추출기 고장 (관통 원칙).
"""
import io
import os
import re
import sys

# stdout 재설정은 하위 모듈 import보다 먼저 — data_lint 등이 자기 stdout을 재설정하면서
# 원본 핸들을 닫으면 여기서 "closed file" 오류가 난다.
try:
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
except Exception:
    pass

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import game_facts  # noqa: E402
from game_facts import ExtractorBroken  # noqa: E402
import data_lint  # noqa: E402


def _beat_prereq_cycle(beats) -> str:
    """prerequisiteBeatId 체인 사이클 시작 beatId, 없으면 None. quest_lint와 동형."""
    prereq = {b["beatId"]: (b.get("prerequisiteBeatId") or None) for b in beats}
    for start in prereq:
        seen, cur = set(), start
        while cur:
            if cur in seen:
                return start
            seen.add(cur)
            cur = prereq.get(cur)
    return None


def _beat_gate_cycle(beats) -> str:
    """prerequisiteBeatId + requiredBeatId 두 간선을 **함께** 따라간 사이클 시작 beatId (없으면 None).

    둘은 AND라 어느 쪽으로든 자기 자신에게 돌아오면 그 비트는 영영 열리지 않는다.
    검사 2는 prereq 한 축만 보므로 게이트를 섞은 순환은 거기서 안 잡힌다.
    """
    edges = {}
    for b in beats:
        out = []
        for key in ("prerequisiteBeatId", "requiredBeatId"):
            v = b.get(key) or None
            if v:
                out.append(v)
        edges[b["beatId"]] = out

    WHITE, GREY, BLACK = 0, 1, 2
    color = {k: WHITE for k in edges}

    def dfs(node):
        color[node] = GREY
        for nxt in edges.get(node, ()):
            if nxt not in color:      # 미존재 대상 — 검사 2/15가 따로 잡는다
                continue
            if color[nxt] == GREY:
                return True
            if color[nxt] == WHITE and dfs(nxt):
                return True
        color[node] = BLACK
        return False

    for start in edges:
        if color[start] == WHITE and dfs(start):
            return start
    return None


def _chapter_ordinal(chapter_id: str):
    """본편 챕터의 진행 순번. ch1..ch12 → 1..12, fin → 13, side/npc → None(본편 아님).

    story_lint 안에서만 쓰는 값이다. StoryObjectiveResolver.ChapterRank는 정렬용이라
    fin=1000 / side=2000 같은 센티넬을 쓰는데, 여기서는 "한 챕터 앞"을 빼야 해서
    산술이 되는 연속 번호가 필요하다.
    """
    if not chapter_id:
        return None
    if chapter_id.startswith("ch") and chapter_id[2:].isdigit():
        return int(chapter_id[2:])
    if chapter_id == "fin":
        return 13
    return None


def _max_ancestor_chapter(beat_id: str, by_id: dict, seen=None) -> int:
    """prerequisiteBeatId ∪ requiredBeatId를 거슬러 올라가 만나는 **본편 챕터 최대 순번**.

    선행이 없거나 전부 side/npc면 0(캠페인 시작점과 같은 취급).
    사이클은 검사 2·15가 따로 잡으므로 여기서는 방문 표시로 끊기만 한다.
    """
    if seen is None:
        seen = set()
    if beat_id in seen:
        return 0
    seen.add(beat_id)
    beat = by_id.get(beat_id)
    if beat is None:
        return 0

    best = 0
    for key in ("prerequisiteBeatId", "requiredBeatId"):
        target = beat.get(key) or None
        if not target:
            continue
        parent = by_id.get(target)
        if parent is not None:
            ordinal = _chapter_ordinal(parent.get("chapterId"))
            if ordinal:
                best = max(best, ordinal)
        best = max(best, _max_ancestor_chapter(target, by_id, seen))
    return best


def evaluate_signals() -> list:
    signals = []
    beats = game_facts.story_beats()
    ids = [b["beatId"] for b in beats]
    idset = set(ids)

    # 1. beatId 유일성
    dups = sorted({x for x in ids if ids.count(x) > 1})
    signals.append(("beatId 중복", "0건",
                    f"{len(dups)}건 ({dups})" if dups else f"0건 ({len(ids)}개 비트)",
                    "FAIL" if dups else "PASS"))

    # 2. prerequisiteBeatId 무결성 (끊김 / 자기참조 / 순환)
    broken = []
    for b in beats:
        p = b.get("prerequisiteBeatId") or None
        if p and p not in idset:
            broken.append(f"{b['beatId']}→{p}(없음)")
        if p and p == b["beatId"]:
            broken.append(f"{b['beatId']} 자기참조")
    cyc = _beat_prereq_cycle(beats)
    if cyc:
        broken.append(f"순환({cyc})")
    signals.append(("prerequisite 무결성", "0건",
                    f"{len(broken)}건 ({broken})" if broken else "0건",
                    "FAIL" if broken else "PASS"))

    # 3. 트리거 param 대상 존재 — RegionEnter/SubAreaEnter→리전, QuestComplete→퀘스트,
    #    CaptureInsect→곤충, LevelReach→정수. param 비었으면(있어도 되는 타입) 통과.
    region_ids = {r[0] for r in game_facts.region_pools()}
    quest_ids = {q["questId"] for q in game_facts.quest_defs()}
    insect_ids = game_facts.all_insect_ids()
    npc_ids = game_facts.story_npc_ids()
    bad_target = []
    for b in beats:
        t = b.get("trigger") or {}
        ttype, param = t.get("type"), (t.get("param") or "")
        if not param:
            continue
        if ttype == "RegionEnter" and param not in region_ids:
            bad_target.append(f"{b['beatId']}:RegionEnter({param})")
        elif ttype == "SubAreaEnter" and param not in region_ids:
            # 서브에리어는 리전에 속하나 별도 ID다. region_pools에 없으면 경고 수준이나,
            # 확실한 리전 ID 대조만 가능하므로 미존재만 잡는다(서브에리어 ID는 리전 밖일 수 있어 완화).
            pass
        elif ttype == "QuestComplete" and param not in quest_ids:
            bad_target.append(f"{b['beatId']}:QuestComplete({param})")
        elif ttype == "CaptureInsect" and param not in insect_ids:
            bad_target.append(f"{b['beatId']}:CaptureInsect({param})")
        elif ttype == "LevelReach" and not param.isdigit():
            bad_target.append(f"{b['beatId']}:LevelReach({param}=비정수)")
        elif ttype == "NpcTalk" and param not in npc_ids:
            bad_target.append(f"{b['beatId']}:NpcTalk({param}=월드 미배치)")
        elif ttype == "GuardianDefeat" and param not in region_ids:
            # param은 수문장을 가진 리전 ID다. 오타면 그 비트는 조용히 영영 미발화한다.
            bad_target.append(f"{b['beatId']}:GuardianDefeat({param})")
        elif ttype == "DexProgress" and not param.isdigit():
            # param은 "이름을 새긴 종 수" 임계값(정수). LevelReach와 같은 형태다.
            bad_target.append(f"{b['beatId']}:DexProgress({param}=비정수)")
    signals.append(("트리거 param 대상 존재", "0건 미존재",
                    f"{len(bad_target)}건 ({bad_target})" if bad_target else "0건",
                    "FAIL" if bad_target else "PASS"))

    # 4. 분기 도달성 — choices[].nextBeatId 존재 + 고아 비트(prereq도 choice 대상도 아님) 없음
    branch_bad = []
    reachable_by_choice = set()
    for b in beats:
        for c in (b.get("choices") or []):
            nxt = c.get("nextBeatId")
            if nxt:
                reachable_by_choice.add(nxt)
                if nxt not in idset:
                    branch_bad.append(f"{b['beatId']}→choice({nxt}=없음)")
    # 고아: prereq도 없고(체인 시작) 어떤 choice의 대상도 아니면서, 트리거도 Immediate가 아닌 비트는
    # 발화 경로가 트리거뿐이라 정상. 여기선 choice 끊김만 FAIL로 본다(고아는 트리거로 열릴 수 있음).
    signals.append(("분기 도달성", "0건 끊김",
                    f"{len(branch_bad)}건 ({branch_bad})" if branch_bad else "0건",
                    "FAIL" if branch_bad else "PASS"))

    # 5. onComplete 보상 ID 존재 + unlockQuestId → 퀘스트 존재
    # 아이템 존재 집합 = capture item ∪ shop 진열ID ∪ shop 지급ID ∪ ItemDatabase 레지스트리.
    # (예전엔 shop 지급ID(rewards)를 _로 버려 exp_boost처럼 ItemDatabase에만 있고 채집망이 아닌
    #  아이템이 오탐으로 걸렸다. game_facts.item_ids()가 런타임 레지스트리 단일 출처.)
    shop_items, shop_rewards = data_lint.extract_cashshop_items()
    item_ids = (data_lint.extract_capture_items() | shop_items | shop_rewards
                | game_facts.item_ids())
    reward_bad = []
    for b in beats:
        oc = b.get("onComplete") or {}
        ri, it, uq = oc.get("rewardInsectId"), oc.get("rewardItemId"), oc.get("unlockQuestId")
        if ri and ri not in insect_ids:
            reward_bad.append(f"{b['beatId']}:곤충({ri})")
        if it and it not in item_ids:
            reward_bad.append(f"{b['beatId']}:아이템({it})")
        if uq and uq not in quest_ids:
            reward_bad.append(f"{b['beatId']}:unlockQuest({uq})")
    signals.append(("onComplete 보상/unlock ID 존재", "0건 미존재",
                    f"{len(reward_bad)}건 ({reward_bad})" if reward_bad else "0건",
                    "FAIL" if reward_bad else "PASS"))

    # 6. 트리거 배선 정합 — JSON이 쓰는 각 trigger.type이 StoryDirector switch + 이벤트 소스에
    #    존재하는가. 누락 시 그 타입 비트가 영구 미발화. 스토리 하네스의 급소.
    wiring = game_facts.story_trigger_wiring()
    used = {(b.get("trigger") or {}).get("type") for b in beats if (b.get("trigger") or {}).get("type")}
    unwired = []
    for tt in sorted(used):
        sw, ev = wiring.get(tt, (False, False))
        if not sw:
            unwired.append(f"{tt}(switch case 없음)")
        elif not ev:
            unwired.append(f"{tt}(이벤트 발화 지점 없음)")
    signals.append(("트리거 배선 정합 (JSON↔StoryDirector)", "0건 미발화",
                    f"{len(unwired)}건 ({unwired})" if unwired
                    else f"0건 (사용 트리거 {len(used)}종 전부 배선됨)",
                    "FAIL" if unwired else "PASS"))

    # 7. requiredRegionId 정합 — 채워진 값은 리전 존재(FAIL), 무param 트리거 무가드는 권고(WARN).
    #    무param CaptureInsect(param 공백)/BattleWin은 위치 무관 발화라 requiredRegionId로 리전을
    #    잠그지 않으면 '늦발화 얼룩'(엉뚱한 리전에서 옛 비트 발화)에 취약(StoryDirector
    #    RegionGateSatisfied가 게이트). requiredRegionId 오타는 런타임엔 조용히 미발화 → 여기서 잡는다.
    #    region_ids는 검사 3에서 계산한 것을 재사용(game_facts 변경 불필요).
    bad_region = []
    unguarded = []
    for b in beats:
        rr = b.get("requiredRegionId") or ""
        t = b.get("trigger") or {}
        ttype, param = t.get("type"), (t.get("param") or "")
        if rr and rr not in region_ids:
            bad_region.append(f"{b['beatId']}:requiredRegion({rr})")
        paramless = ttype == "BattleWin" or (ttype == "CaptureInsect" and not param)
        if paramless and not rr:
            unguarded.append(b["beatId"])
    if bad_region:
        judge7, val7 = "FAIL", f"{len(bad_region)}건 미존재 ({bad_region})"
    elif unguarded:
        judge7, val7 = "WARN", f"{len(unguarded)}건 무가드 권고 ({unguarded})"
    else:
        judge7, val7 = "PASS", "0건 (requiredRegion 실존·무param 전부 가드)"
    signals.append(("requiredRegionId 정합 (리전 게이트)", "0건 미존재·무가드", val7, judge7))

    # 8. 일생 1회 트리거의 스파인 사용 — GuardianDefeat는 leaf 전용이어야 한다.
    #    RegionManager.DefeatGuardian이 idempotent 가드로 리전당 정확히 1회만 이벤트를 쏜다.
    #    그 순간 prereq가 미충족이면 그 비트는 영영 안 열리고, 뒤 비트가 그걸 prereq로 삼고
    #    있으면 캠페인이 거기서 영구 정지한다. QuestComplete와 같은 부류다.
    #
    #    QuestComplete도 같은 부류다. 1막 ch1 체인(ch1_first_capture→…→ch1_guardian_call)이
    #    한때 그렇게 엮여 있어 튜토리얼을 마친 세이브는 라온 소개를 포함한 5비트를 영영 못 봤다.
    #    2026-08-07에 전부 재발화 트리거(CaptureInsect/LevelReach/BattleWin + requiredRegionId)로
    #    옮겨 부채를 청산했다. 지금은 0건이며, 다시 늘면 측정값에 드러난다.
    #    FAIL로 승격하지 않는 이유: leaf로 쓰는 QuestComplete는 정상이고(놓쳐도 체인이 안 끊긴다),
    #    이 검사는 "prereq로 쓰였는가"만 본다 — 그 조건이면 GuardianDefeat와 달리 즉시 위험하진 않다.
    prereq_targets = {b.get("prerequisiteBeatId") for b in beats if b.get("prerequisiteBeatId")}
    once_only = []
    grandfathered = 0
    for b in beats:
        ttype = (b.get("trigger") or {}).get("type")
        if b["beatId"] not in prereq_targets:
            continue
        if ttype == "GuardianDefeat":
            once_only.append(f"{b['beatId']}({ttype})")
        elif ttype == "QuestComplete":
            grandfathered += 1
    note = f" / QuestComplete 기존 {grandfathered}건은 1막 유예" if grandfathered else ""
    signals.append((
        "일생 1회 트리거의 스파인 사용 (GuardianDefeat leaf 강제)",
        "0건",
        (f"{len(once_only)}건 ({once_only})" if once_only else "0건") + note,
        "FAIL" if once_only else "PASS",
    ))

    # 9. cutsceneId 실재성 — 오타는 런타임에 LogWarning만 찍고 컷신이 조용히 안 나온다.
    #    연출 누락은 화면상 티가 안 나서(원래 없던 것처럼 보인다) 배포까지 살아남기 쉽다.
    #    CutsceneLibrary의 const 문자열을 소스에서 읽어 대조한다 — C#에 사본을 만들면
    #    그쪽이 낡는다(이 저장소의 하드코딩 목록이 세 번 어긋난 것과 같은 이유).
    lib_path = os.path.join(
        os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
        "Assets", "Scripts", "Story", "CutsceneLibrary.cs")
    try:
        with open(lib_path, encoding="utf-8") as fh:
            lib_src = fh.read()
    except OSError as exc:
        raise ExtractorBroken(f"CutsceneLibrary.cs를 읽지 못했다: {exc}")

    known_cutscenes = set(re.findall(r'public const string \w+\s*=\s*"([a-z_0-9]+)"', lib_src))
    if not known_cutscenes:
        raise ExtractorBroken(
            "CutsceneLibrary.cs에서 컷신 ID 상수를 하나도 찾지 못했다 — 추출기가 낡았다")

    # 상수로 선언만 하고 TryGet switch에 case가 없으면 역시 발화하지 않는다(같은 무증상 결함).
    dispatched = set(re.findall(r'case (\w+):\s*shots\s*=', lib_src))
    declared_names = dict(re.findall(
        r'public const string (\w+)\s*=\s*"([a-z_0-9]+)"', lib_src))
    undispatched = sorted(v for k, v in declared_names.items() if k not in dispatched)

    missing_cutscene = sorted(
        f"{b['beatId']}→{b['cutsceneId']}"
        for b in beats
        if b.get("cutsceneId") and b["cutsceneId"] not in known_cutscenes)

    problems = missing_cutscene + [f"{c}(switch 미배선)" for c in undispatched]
    used = sum(1 for b in beats if b.get("cutsceneId"))
    signals.append((
        "cutsceneId 실재성 (JSON↔CutsceneLibrary)",
        "0건 미존재",
        f"{len(problems)}건 ({problems})" if problems
        else f"0건 (사용 {used}건 / 정의 {len(known_cutscenes)}종)",
        "FAIL" if problems else "PASS",
    ))

    # 10. requiredQuestId 실재성 — 존재하지 않는 questId를 물면 게이트가 영영 안 열려
    #     그 비트와 뒤 체인 전체가 도달 불가가 된다. 런타임엔 조용히 false만 돌아온다.
    #     ch1_intro가 이걸 써서 튜토리얼과 스토리를 가르므로, 오타 하나면 캠페인이 시작되지 않는다.
    known_quests = {q["questId"] for q in game_facts.quest_defs() if q.get("questId")}
    if not known_quests:
        raise ExtractorBroken("TutorialQuestManager에서 questId를 하나도 찾지 못했다 — 추출기가 낡았다")

    missing_quest = sorted(
        f"{b['beatId']}→{b['requiredQuestId']}"
        for b in beats
        if b.get("requiredQuestId") and b["requiredQuestId"] not in known_quests)

    gated = sum(1 for b in beats if b.get("requiredQuestId"))
    signals.append((
        "requiredQuestId 실재성 (퀘스트 게이트)",
        "0건 미존재",
        f"{len(missing_quest)}건 ({missing_quest})" if missing_quest
        else f"0건 (게이트 {gated}건 / 퀘스트 {len(known_quests)}개)",
        "FAIL" if missing_quest else "PASS",
    ))

    # 11. 특정 곤충 포획 목표가 그 리전에서 실제로 잡히는가.
    #     CaptureInsect에 param을 주면 "그 종을 잡아야" 발화한다. 그런데 requiredRegionId까지
    #     걸려 있으면 **그 리전 풀에 그 종이 없을 때 영영 발화하지 않는다** — 런타임엔 아무 신호도
    #     없고 플레이어는 "왜 다음 이야기가 안 나오지"만 겪는다. 1막 확장이 이 형태를 5건 썼다.
    pool_by_region = {rid: set(ids) for rid, _level, ids in game_facts.region_pools()}
    # 서브에리어 전용 종도 그 리전에서 잡히므로 함께 센다.
    region_src = game_facts._read("region_defs")
    for m in re.finditer(r'regionId\s*=\s*"([a-z_]+)"(.*?)(?=regionId\s*=\s*"|\Z)', region_src, re.S):
        rid, body = m.group(1), m.group(2)
        if rid not in pool_by_region:
            continue
        for block in re.findall(r'exclusiveInsectIds\s*=\s*new\[\]\s*\{(.*?)\}', body, re.S):
            pool_by_region[rid].update(re.findall(r'"([a-z_0-9]+)"', block))

    unreachable_target = []
    for b in beats:
        trig = b.get("trigger") or {}
        if trig.get("type") != "CaptureInsect":
            continue
        species = trig.get("param")
        region = b.get("requiredRegionId")
        if not species or not region:
            continue
        if species not in pool_by_region.get(region, set()):
            unreachable_target.append(f"{b['beatId']}:{species}@{region}")

    targeted = sum(1 for b in beats
                   if (b.get("trigger") or {}).get("type") == "CaptureInsect"
                   and (b.get("trigger") or {}).get("param"))
    signals.append((
        "특정 곤충 포획 목표의 서식 정합",
        "0건 미서식",
        f"{len(unreachable_target)}건 ({unreachable_target})" if unreachable_target
        else f"0건 (지정 포획 {targeted}건)",
        "FAIL" if unreachable_target else "PASS",
    ))

    # 12. stageEnterId/stageExitId 실재성 — 검사 9(cutsceneId)와 같은 무증상 결함이다.
    #     오타면 런타임에 LogWarning만 찍고 NPC가 그냥 안 움직인다. 특히 stageEnterId는
    #     "라온이 뛰어 들어온 다음 말한다"의 순서를 만드는 장치라, 빠지면 지도 반대편에 있는
    #     인물의 대사만 허공에서 뜬다 — 화면이 조용해서 배포까지 살아남는다.
    stage_path = os.path.join(
        os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
        "Assets", "Scripts", "Story", "StoryStageLibrary.cs")
    try:
        with open(stage_path, encoding="utf-8") as fh:
            stage_src = fh.read()
    except OSError as exc:
        raise ExtractorBroken(f"StoryStageLibrary.cs를 읽지 못했다: {exc}")

    known_stages = set(re.findall(r'public const string \w+\s*=\s*"([a-z_0-9]+)"', stage_src))
    if not known_stages:
        raise ExtractorBroken(
            "StoryStageLibrary.cs에서 연출 ID 상수를 하나도 찾지 못했다 — 추출기가 낡았다")

    # 상수만 선언하고 TryGet switch에 case가 없으면 역시 발화하지 않는다(같은 무증상 결함).
    stage_dispatched = set(re.findall(r'case (\w+):\s*steps\s*=', stage_src))
    stage_declared = dict(re.findall(
        r'public const string (\w+)\s*=\s*"([a-z_0-9]+)"', stage_src))
    stage_undispatched = sorted(v for k, v in stage_declared.items() if k not in stage_dispatched)

    missing_stage = sorted(
        f"{b['beatId']}→{b[field]}"
        for b in beats
        for field in ("stageEnterId", "stageExitId")
        if b.get(field) and b[field] not in known_stages)

    # 대사 없는 비트의 stageEnterId는 영영 안 돈다 — 모달이 안 뜨므로 게이트가 걸리지 않는다.
    enter_without_lines = sorted(
        b["beatId"] for b in beats
        if b.get("stageEnterId") and not b.get("lines"))

    stage_problems = (missing_stage
                      + [f"{s}(switch 미배선)" for s in stage_undispatched]
                      + [f"{b}(대사 없음)" for b in enter_without_lines])
    stage_used = sum(1 for b in beats if b.get("stageEnterId") or b.get("stageExitId"))
    signals.append((
        "stageId 실재성 (JSON↔StoryStageLibrary)",
        "0건 미존재",
        f"{len(stage_problems)}건 ({stage_problems})" if stage_problems
        else f"0건 (사용 {stage_used}건 / 정의 {len(known_stages)}종)",
        "FAIL" if stage_problems else "PASS",
    ))

    # 13. stageExitId와 cutsceneId 동시 사용 금지 — 둘 다 StoryBeatCompleted를 구독해
    #     조작(SetFrozen)과 모달 스택을 뺏는다. 함께 걸면 서로의 복구를 덮어써서
    #     조작이 안 돌아오거나 카메라가 컷신 마지막 구도로 굳는다.
    #     런타임에도 StoryStageDirector가 컷신에 양보하지만, 저작 단계에서 막는 편이 낫다.
    both_slots = sorted(
        b["beatId"] for b in beats
        if b.get("stageExitId") and b.get("cutsceneId"))
    signals.append((
        "stageExitId ↔ cutsceneId 배타",
        "0건 동시 사용",
        f"{len(both_slots)}건 ({both_slots})" if both_slots else "0건",
        "FAIL" if both_slots else "PASS",
    ))

    # 14. Story.json의 키가 StoryBeat.cs에 실재하는가.
    #     **JsonUtility는 모르는 키를 조용히 무시한다** — 그래서 오타 하나("stageEnterID")나
    #     이미 없어진 필드("oneShot")를 저작하면 아무 경고 없이 그 저작이 통째로 증발한다.
    #     화면에는 "연출이 원래 없는 것"처럼 보여서 배포까지 살아남는다.
    #     실제로 `oneShot`이 읽는 코드 0인 채 82비트에 남아 있었다(2026-08-17 audit).
    #
    #     필드 목록은 소스에서 읽는다 — 여기에 사본을 만들면 그쪽이 늘 때 이 검사가 낡는다.
    beat_path = os.path.join(
        os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
        "Assets", "Scripts", "Story", "StoryBeat.cs")
    try:
        with open(beat_path, encoding="utf-8") as fh:
            beat_src = fh.read()
    except OSError as exc:
        raise ExtractorBroken(f"StoryBeat.cs를 읽지 못했다: {exc}")

    # 클래스별 {필드명: 타입}. List<T>는 원소 타입 T로 눕혀 중첩 검사에 그대로 쓴다.
    class_fields = {}
    parts = re.split(r"public class (\w+)", beat_src)
    for i in range(1, len(parts) - 1, 2):
        cls_name, body = parts[i], parts[i + 1]
        fields = {}
        for fm in re.finditer(r"^\s*public\s+([\w\.<>\[\]]+)\s+(\w+)\s*(?:=[^;]*)?;", body, re.M):
            ftype, fname = fm.group(1), fm.group(2)
            elem = re.match(r"List<(\w+)>$", ftype)
            fields[fname] = elem.group(1) if elem else ftype
        class_fields[cls_name] = fields

    if not class_fields.get("StoryBeat"):
        raise ExtractorBroken("StoryBeat.cs에서 StoryBeat의 public 필드를 찾지 못했다 — 추출기가 낡았다")

    def _walk_keys(node, cls_name, path, out):
        known = class_fields.get(cls_name)
        if known is None:
            return  # string/int/bool 등 원시 타입 — 더 내려갈 곳이 없다
        for key, value in node.items():
            if key not in known:
                out.append(f"{path}.{key}")
                continue
            child = known[key]
            if isinstance(value, dict):
                _walk_keys(value, child, f"{path}.{key}", out)
            elif isinstance(value, list):
                for idx, item in enumerate(value):
                    if isinstance(item, dict):
                        _walk_keys(item, child, f"{path}.{key}[{idx}]", out)

    orphan_keys = []
    for b in beats:
        _walk_keys(b, "StoryBeat", b.get("beatId") or "(무명)", orphan_keys)

    signals.append((
        "Story.json 키 실재성 (JSON↔StoryBeat 필드)",
        "0건 미매핑",
        f"{len(orphan_keys)}건 ({orphan_keys[:8]})" if orphan_keys
        else f"0건 (비트 {len(beats)}개 / 필드 {len(class_fields['StoryBeat'])}종)",
        "FAIL" if orphan_keys else "PASS",
    ))

    # 15. requiredBeatId 실재성 + 자기참조 + (prereq와 섞인) 순환.
    #     진행 게이트는 여운(echo) 비트의 조기 발화를 막으려고 들어왔다 — 그것들은 "같은 NPC의
    #     직전 여운"만 prereq로 물고 있어서, 시작 지역인 초원에서 라온에게 세 번 말하면 12장
    #     복귀 대사가, 숲에서 세라에게 네 번 말하면 엔딩 에필로그 전문이 보상까지 딸려 나왔다.
    #     오타 난 게이트는 런타임에 조용히 "영영 안 열림"이 되므로 여기서 잡는다.
    gate_broken = []
    for b in beats:
        gate = b.get("requiredBeatId") or None
        if not gate:
            continue
        if gate not in idset:
            gate_broken.append(f"{b['beatId']}→{gate}(없음)")
        if gate == b["beatId"]:
            gate_broken.append(f"{b['beatId']} 자기참조")
    gate_cycle = _beat_gate_cycle(beats)
    if gate_cycle:
        gate_broken.append(f"순환({gate_cycle})")
    gated = sum(1 for b in beats if b.get("requiredBeatId"))
    signals.append((
        "requiredBeatId 무결성 (진행 게이트)",
        "0건",
        f"{len(gate_broken)}건 ({gate_broken})" if gate_broken
        else f"0건 (게이트 {gated}건)",
        "FAIL" if gate_broken else "PASS",
    ))

    # 16. 게이트 대상의 재발화성 — 검사 8이 스파인(prereq)에 대해 하는 판정을 게이트에 대해 한다.
    #     GuardianDefeat는 리전당 1회(RegionManager.DefeatGuardian의 idempotent 가드),
    #     QuestComplete는 퀘스트당 1회다. 그 순간을 놓친 세이브는 게이트가 영영 안 열려
    #     그 비트가 영구 정지한다 — 스파인과 달리 여기는 유예 없이 FAIL이다(새 필드라
    #     물려받은 부채가 없다. 지금 막지 않으면 그대로 부채가 된다).
    once_only_triggers = ("GuardianDefeat", "QuestComplete")
    beat_by_id = {b["beatId"]: b for b in beats}
    bad_gate = []
    for b in beats:
        gate = b.get("requiredBeatId") or None
        if not gate or gate not in beat_by_id:
            continue
        gate_trigger = (beat_by_id[gate].get("trigger") or {}).get("type")
        if gate_trigger in once_only_triggers:
            bad_gate.append(f"{b['beatId']}→{gate}({gate_trigger})")
    signals.append((
        "게이트 대상의 재발화성 (requiredBeatId → 일생 1회 트리거 금지)",
        "0건",
        f"{len(bad_gate)}건 ({bad_gate})" if bad_gate
        else f"0건 (게이트 {gated}건)",
        "FAIL" if bad_gate else "PASS",
    ))

    # 17. 챕터 도달 순서 — N장 비트가 N-1장까지의 진행만으로 열려서는 안 된다.
    #
    #     **이번 결함의 재발 방지기다.** 여운(echo) 비트가 "같은 NPC의 직전 여운"만 prereq로
    #     물고 있어서 진행과 무관하게 발화했다: 시작 지역인 초원에서 라온에게 세 번 말하면
    #     12장 복귀 대사가, 어르신에게 세 번이면 11장의 최대 반전이, 숲에서 세라에게 네 번이면
    #     엔딩 에필로그 전문이 캔디 120 + XP 250과 함께 나왔다. 7건 전부 이 검사에 걸린다.
    #
    #     선행 최대 챕터가 자기 챕터 **-1** 이상이면 통과다. -1인 것은 도착 비트(chN_arrive)가
    #     바로 그 챕터를 여는 비트라 선행이 N-1장일 수밖에 없기 때문이다.
    #     side/npc 챕터는 본편 진행 축이 아니므로 대상에서 뺀다(앰비언트는 언제 봐도 된다).
    beat_index = {b["beatId"]: b for b in beats}
    out_of_order = []
    for b in beats:
        ordinal = _chapter_ordinal(b.get("chapterId"))
        if ordinal is None:
            continue
        reached = _max_ancestor_chapter(b["beatId"], beat_index)
        if reached < ordinal - 1:
            out_of_order.append(f"{b['beatId']}({b['chapterId']}, 선행 최대 ch{reached})")
    main_beats = sum(1 for b in beats if _chapter_ordinal(b.get("chapterId")) is not None)
    signals.append((
        "챕터 도달 순서 (뒷 챕터 비트의 조기 발화)",
        "0건",
        f"{len(out_of_order)}건 ({out_of_order})" if out_of_order
        else f"0건 (본편 비트 {main_beats}개)",
        "FAIL" if out_of_order else "PASS",
    ))

    # 18. 캠페인 시작 시 동시에 자격을 갖는 비트 — 1개를 넘지 않아야 한다.
    #
    #     넘으면 무엇이 먼저 뜰지가 저작이 아니라 우선순위 표(스파인→챕터→order→id)에 맡겨진다.
    #     실제로 그렇게 어긋났다: 개막 `ch1_intro`는 `requiredQuestId: q_move`로 잠겨 있는데
    #     앰비언트 `talk_elder`/`talk_rival`/`talk_scholar`는 아무 게이트가 없어, **튜토리얼 중
    #     HUD 목표가 "마을 어르신에게 말 걸기"로 잡히고 첫 목표 자동 주행까지 태워 보냈다.**
    #     도착해서 말을 걸면 개막이 아니라 잡담이 뜨고 그게 소비됐다.
    #
    #     앰비언트 비트에 `requiredBeatId: ch1_intro`를 걸어 0건으로 만들었다. 0도 정상이다 —
    #     그동안은 튜토리얼 퀘스트 칩이 안내를 맡는다.
    unlocked_at_start = [
        b["beatId"] for b in beats
        if not (b.get("prerequisiteBeatId") or None)
        and not (b.get("requiredBeatId") or None)
        and not (b.get("requiredQuestId") or None)
    ]
    signals.append((
        "캠페인 시작 시 동시 자격 비트",
        "1개 이하",
        f"{len(unlocked_at_start)}개 ({unlocked_at_start})" if len(unlocked_at_start) > 1
        else f"{len(unlocked_at_start)}개",
        "FAIL" if len(unlocked_at_start) > 1 else "PASS",
    ))

    # 19. 명부회 간부 보스전 상대는 **소개 비트**를 가져야 한다.
    #
    #     WorldInteractionController가 도전을 여는 조건이 "그 인물과 이야기를 나눈 적이 있는가"
    #     (StoryObjectiveResolver.HasMetNpc)로 바뀌었다. 예전엔 "이번 대화에서 비트가 안 떴다"만
    #     봐서, 집게·저울·관장처럼 소개가 서브에리어 대치 비트에 걸린 인물은 **리전에 도착해
    #     본진에서 말만 걸면 이름도 모르는 채 보스전이 시작됐다**(최종 보스 포함).
    #
    #     그 대가로 새 잠금 위험이 생겼다: 표에 보스를 추가하면서 스토리 비트를 안 만들면
    #     `HasMetNpc`가 영영 false라 **그 보스와 싸울 수 없다**. 런타임엔 아무 로그도 안 나온다.
    duel_path = os.path.join(
        os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
        "Assets", "Scripts", "NPC", "NpcBossDuels.cs")
    try:
        with open(duel_path, encoding="utf-8") as fh:
            duel_src = fh.read()
    except OSError as exc:
        raise ExtractorBroken(f"NpcBossDuels.cs를 읽지 못했다: {exc}")

    duel_npcs = re.findall(r'storyNpcId\s*=\s*"([a-z_0-9]+)"', duel_src)
    if not duel_npcs:
        raise ExtractorBroken(
            "NpcBossDuels.cs에서 보스 storyNpcId를 하나도 찾지 못했다 — 추출기가 낡았다")

    introduced = set()
    for b in beats:
        if b.get("speakerNpcId"):
            introduced.add(b["speakerNpcId"])
        trig = b.get("trigger") or {}
        if trig.get("type") == "NpcTalk" and trig.get("param"):
            introduced.add(trig["param"])

    unintroduced = sorted(set(duel_npcs) - introduced)
    signals.append((
        "보스 대결 상대의 소개 비트 (없으면 도전 불가)",
        "0건 미소개",
        f"{len(unintroduced)}건 ({unintroduced})" if unintroduced
        else f"0건 (보스 {len(set(duel_npcs))}명)",
        "FAIL" if unintroduced else "PASS",
    ))

    # 20. 월드에 배치된 스토리 NPC는 **전용 앰비언트 대사**를 가져야 한다.
    #
    #     비트가 없을 때(아직 차례가 아니거나 전부 본 뒤) NpcDialogueUI는 마을 주민 풀로
    #     떨어진다. 그래서 명부회 간부가 "산책하기 딱 좋은 날씨예요"를 말했다 — 최종 보스인
    #     관장까지. 예외도 경고도 없고 그럴듯한 대사가 나오므로 눈으로만 잡힌다.
    #
    #     인물을 새로 배치할 때 `StoryNpcLines`에 두 줄 넣는 걸 빠뜨리기 쉬워서 여기서 잡는다.
    world_npcs = game_facts.story_npc_ids()
    ambient_npcs = game_facts.story_npc_ambient_ids()
    voiceless = sorted(world_npcs - ambient_npcs)
    orphan_lines = sorted(ambient_npcs - world_npcs)

    npc_problems = ([f"{n}(앰비언트 없음)" for n in voiceless]
                    + [f"{n}(월드에 없는 인물)" for n in orphan_lines])
    signals.append((
        "스토리 인물 앰비언트 대사 (없으면 주민 잡담으로 떨어짐)",
        "0건 누락",
        f"{len(npc_problems)}건 ({npc_problems})" if npc_problems
        else f"0건 (스토리 인물 {len(world_npcs)}명 전원 전용 대사)",
        "FAIL" if npc_problems else "PASS",
    ))

    return signals


def render(signals) -> str:
    out = ["| 항목 | 임계값 | 측정값 | 판정 |", "|------|--------|--------|------|"]
    for name, thr, val, judge in signals:
        out.append(f"| {name} | {thr} | {val} | **{judge}** |")
    fail = sum(1 for s in signals if s[3] == "FAIL")
    warn = sum(1 for s in signals if s[3] == "WARN")
    passn = sum(1 for s in signals if s[3] == "PASS")
    out.append("")
    out.append(f"요약: **{fail} FAIL** / {warn} WARN / {passn} PASS")
    if fail:
        out.append("→ FAIL 1건 이상. PASS 보고 금지. 즉시 정정 필요.")
    return "\n".join(out)


def main():
    print("# story-lint — 곤충게임 스토리 정합성 검증\n")
    print("## 위험 신호 표")
    signals = evaluate_signals()
    print(render(signals))
    print()
    print("## 가정 / 한계")
    print("- 스토리 비트는 Assets/Resources/Story.json에서 json.load로 읽는다(정규식 아님).")
    print("  지연 발화(DeferTrigger)도 배선으로 세되, 큐를 흘리는 지점이 있을 때만 센다.")
    print("- 검사 20은 VillageBuilder의 storyNpcId(주석 제외)와 NpcDialogueDatabase.StoryNpcLines")
    print("  키를 양방향 대조한다. 한쪽에만 있으면 그 인물이 주민 잡담을 하거나 사문화된 대사다.")
    print("- 트리거 배선(검사 6)은 StoryDirector의 Trigger 상수·switch case·EvaluateTriggers")
    print("  발화 지점을 교차검사. 새 trigger.type의 배선 누락(영구 미발화)을 잡는다.")
    print("- SubAreaEnter param은 리전 밖 서브에리어 ID일 수 있어 완화(미존재만 잡지 않음).")
    print("- 진행 게이트(검사 15·16)는 requiredBeatId를 본다. prerequisiteBeatId가 체인의")
    print("  '순서'라면 이쪽은 '단계'이고 둘은 AND다 — 발화(StoryDirector.BeatGateSatisfied)와")
    print("  목표 도출(StoryObjectiveResolver.SelectObjectiveBeat) 양쪽에 같은 게이트가 걸려 있다.")
    print("- 챕터 도달 순서(검사 17)는 prereq∪requiredBeatId를 거슬러 올라가 만나는 본편 챕터")
    print("  최대치를 본다. side/npc 챕터 비트는 진행 축이 아니라 대상에서 뺀다.")
    print("- 검사 18은 세이브 0(진행·퀘스트 모두 비어 있음)에서 자격을 갖는 비트를 센다.")
    print("  2개 이상이면 개막 순서가 저작이 아니라 우선순위 표에 맡겨진다.")
    print("- 검사 19는 NpcBossDuels.cs의 storyNpcId를 정규식으로 읽어 Story.json의")
    print("  speakerNpcId ∪ NpcTalk param과 대조한다(소개 없는 보스 = 영구 도전 불가).")
    fail = sum(1 for s in signals if s[3] == "FAIL")
    return 1 if fail else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except ExtractorBroken as e:
        print(f"\n## 추출기 고장\n\n**{e}**\n")
        print("스토리 데이터 결함이 아니라 이 스크립트가 코드/JSON을 못 따라간 것이다.")
        print("검증 결과는 신뢰할 수 없다 — 추출기를 먼저 고칠 것.")
        sys.exit(2)
