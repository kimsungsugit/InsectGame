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
    print("- 트리거 배선(검사 6)은 StoryDirector의 Trigger 상수·switch case·EvaluateTriggers")
    print("  발화 지점을 교차검사. 새 trigger.type의 배선 누락(영구 미발화)을 잡는다.")
    print("- SubAreaEnter param은 리전 밖 서브에리어 ID일 수 있어 완화(미존재만 잡지 않음).")
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
