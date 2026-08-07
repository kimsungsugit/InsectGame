"""퀘스트 정합성 검증. 보상 ID·prerequisite 체인·QuestType 배선·대화 리전키 검사.

data_lint의 형제다. data_lint는 리전/샵/가챠 ID를 전담하고 이미 12검사라, 퀘스트는 섞지
않고 별 파일로 둔다(관심사 분리, 독립 실행/CI).

가장 중요한 검사는 QuestType↔진행 배선(검사 5)이다. 새 퀘스트 목표 타입을 추가하면 5곳
(QuestType enum / 배열 / Notify 메서드 / **게임플레이 호출부** / 이벤트 등록)을 건드려야
하는데, 호출부나 이벤트 등록이 누락되면 그 퀘스트가 IncrementProgress에 영영 도달 못 해
영구 정지한다. 실제로 q_team(SetTeam)이 TeamChanged 미구독으로 멈춘 회귀가 있었다
(TutorialQuestManager.cs:322 주석). 이 검사가 그 회귀를 잡는다.

종료 코드: 0 정상 / 1 데이터 결함 / 2 추출기 고장 (관통 원칙 — data_lint와 동일).
"""
import glob
import io
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import game_facts  # noqa: E402
from game_facts import ExtractorBroken  # noqa: E402
import data_lint  # noqa: E402

try:
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
except Exception:
    pass


def extract_boss_duel_rewards():
    """`NpcBossDuels`의 (storyNpcId, rewardItemId) 목록.

    표가 C# 하드코딩 배열이라 소스를 읽는다(퀘스트 배열과 같은 형태).
    추출이 0건이면 표가 비었거나 파서가 낡은 것이므로 `ExtractorBroken`을 던진다 —
    빈 목록을 "위반 0건"으로 읽으면 검사가 조용히 죽는다(이 파일의 관통 원칙).
    """
    path = os.path.join("Assets", "Scripts", "NPC", "NpcBossDuels.cs")
    if not os.path.exists(path):
        raise ExtractorBroken(f"{path}: 파일 없음 — 경로가 바뀌었으면 이 추출기도 고칠 것")

    with io.open(path, encoding="utf-8", errors="replace") as f:
        src = f.read()

    # new BossDuel { ... storyNpcId = "x" ... rewardItemId = "y" ... } — 필드 순서에 의존하지
    # 않도록 블록을 먼저 자르고 그 안에서 각각 찾는다.
    out = []
    for block in re.findall(r"new\s+BossDuel\s*\{(.*?)\}", src, re.S):
        npc = re.search(r'storyNpcId\s*=\s*"([^"]+)"', block)
        item = re.search(r'rewardItemId\s*=\s*"([^"]+)"', block)
        if npc and item:
            out.append((npc.group(1), item.group(1)))

    if not out:
        raise ExtractorBroken(
            "NpcBossDuels에서 대결을 하나도 못 읽었다 — 표 구조가 바뀌었는지 확인할 것")
    return out


def notify_called_in_gameplay(method: str) -> bool:
    """게임플레이 코드(TutorialQuestManager 밖)에서 `.method(`가 호출되나.

    notify 경로 QuestType은 전용 Notify 메서드가 게임플레이 시스템(배틀/포획 등)에서
    호출돼야 진행된다. 메서드 정의만 있고 호출부가 없으면 그 QuestType은 영구 정지다.
    """
    pat = re.compile(r"\." + re.escape(method) + r"\s*\(")
    for path in glob.glob("Assets/Scripts/**/*.cs", recursive=True):
        if "TutorialQuestManager" in path:
            continue
        try:
            with open(path, encoding="utf-8") as f:
                if pat.search(f.read()):
                    return True
        except (OSError, UnicodeDecodeError):
            continue
    return False


def _prereq_cycle(quests) -> str:
    """prerequisite 체인에 사이클이 있으면 그 시작 questId, 없으면 None."""
    prereq = {q["questId"]: q["prereq"] for q in quests}
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
    quests = game_facts.quest_defs()
    ids = [q["questId"] for q in quests]
    idset = set(ids)

    # 1. questId 중복
    dups = sorted({x for x in ids if ids.count(x) > 1})
    signals.append(("questId 중복", "0건",
                    f"{len(dups)}건 ({dups})" if dups else f"0건 ({len(ids)}개 퀘스트)",
                    "FAIL" if dups else "PASS"))

    # 2. prerequisite 무결성 — 끊긴 참조 / 자기참조 / 순환
    broken = []
    for q in quests:
        p = q["prereq"]
        if p and p not in idset:
            broken.append(f"{q['questId']}→{p}(없음)")
        if p and p == q["questId"]:
            broken.append(f"{q['questId']} 자기참조")
    cycle = _prereq_cycle(quests)
    if cycle:
        broken.append(f"순환({cycle})")
    signals.append(("prerequisite 무결성", "0건",
                    f"{len(broken)}건 ({broken})" if broken else "0건",
                    "FAIL" if broken else "PASS"))

    # 3. 보상 곤충 ID → 곤충 존재
    insect_ids = game_facts.all_insect_ids()
    bad_insect = [f"{q['questId']}:{q['reward_insect']}"
                  for q in quests if q["reward_insect"] and q["reward_insect"] not in insect_ids]
    signals.append(("보상 곤충 ID 존재", "0건 미존재",
                    f"{len(bad_insect)}건 ({bad_insect})" if bad_insect
                    else f"0건 (곤충 {len(insect_ids)}종 대조)",
                    "FAIL" if bad_insect else "PASS"))

    # 4. 보상 아이템 ID → 아이템 존재
    # 집합 = capture item ∪ shop 진열ID ∪ shop 지급ID ∪ ItemDatabase 레지스트리.
    # (예전엔 shop 지급ID(rewards)를 _로 버려 exp_boost처럼 ItemDatabase에만 있는 아이템이
    #  오탐으로 걸렸다 — story_lint와 동일 수정. game_facts.item_ids()가 런타임 레지스트리.)
    shop_items, shop_rewards = data_lint.extract_cashshop_items()
    item_ids = (data_lint.extract_capture_items() | shop_items | shop_rewards
                | game_facts.item_ids())
    bad_item = [f"{q['questId']}:{q['reward_item']}"
                for q in quests if q["reward_item"] and q["reward_item"] not in item_ids]
    signals.append(("보상 아이템 ID 존재", "0건 미존재",
                    f"{len(bad_item)}건 ({bad_item})" if bad_item
                    else f"0건 (아이템 {len(item_ids)}종 대조)",
                    "FAIL" if bad_item else "PASS"))

    # 4b. 보스 대결 보상 아이템 ID → 아이템 존재 (NpcBossDuels)
    # 퀘스트 보상과 같은 함정을 공유한다: 오타를 물어도 런타임엔 조용히 실패해 승리 보상이
    # 사라진다. `NpcBossDuels` 주석은 "아이템 ID는 ItemDatabase에 실재해야 하며
    # NpcBossDuelTests가 그 정합을 고정한다"고 적었지만, 그 테스트는 실제로는
    # **비어 있지 않은지만** 본다. 아이템 ID 레지스트리를 이미 여기서 모으고 있으므로
    # (여러 소스의 합집합이라 C# 쪽에서 다시 모으면 사본이 생긴다) 검사도 여기 둔다.
    boss_rewards = extract_boss_duel_rewards()
    bad_boss = [f"{npc}:{item}" for npc, item in boss_rewards if item not in item_ids]
    signals.append(("보스 대결 보상 아이템 ID 존재", "0건 미존재",
                    f"{len(bad_boss)}건 ({bad_boss})" if bad_boss
                    else f"0건 (대결 {len(boss_rewards)}건 대조)",
                    "FAIL" if bad_boss else "PASS"))

    # 5. QuestType ↔ 진행 배선 — 배열이 쓰는 각 QuestType이 IncrementProgress에 닿는가.
    #    q_team류 회귀(이벤트 미등록/호출부 누락으로 퀘스트 영구 정지)를 잡는 핵심 검사.
    wiring = game_facts.quest_progress_wiring()
    used_types = {q["type"] for q in quests if q["type"]}
    unreachable = []
    for qt in sorted(used_types):
        paths = wiring.get(qt, [])
        reachable = False
        for via, wired, detail in paths:
            if via == "update":
                reachable = True
            elif via == "event" and wired:
                reachable = True
            elif via == "notify" and notify_called_in_gameplay(detail):
                reachable = True
        if not reachable:
            # 왜 안 닿는지 진단 문구
            if not paths:
                why = "진행 로직 자체 없음"
            elif any(v == "event" and not w for v, w, _ in paths):
                why = "이벤트 핸들러 있으나 SubscribeEvents 미등록"
            else:
                why = "Notify 메서드 있으나 게임플레이 호출부 없음"
            unreachable.append(f"{qt}({why})")
    signals.append(("QuestType↔진행 배선", "0건 정지",
                    f"{len(unreachable)}건 ({unreachable})" if unreachable
                    else f"0건 (사용 타입 {len(used_types)}종 전부 도달 가능)",
                    "FAIL" if unreachable else "PASS"))

    # 6. 대화 RegionLines 키 → 리전 존재
    region_ids = {r[0] for r in game_facts.region_pools()}
    dlg_keys = game_facts.dialogue_region_keys()
    orphan = sorted(dlg_keys - region_ids)
    signals.append(("대화 리전키 정합성", "0건 고아",
                    f"{len(orphan)}건 ({orphan})" if orphan
                    else f"0건 (대화 키 {len(dlg_keys)}개 대조)",
                    "FAIL" if orphan else "PASS"))

    # 7. 서브 퀘스트 정합 — repeatable은 Side 전용(스토리 선형 체인 오염 방지),
    #    repeatable이면 targetIncrement>0(그래야 '할 때마다 목표 상승'이 실제로 일어남).
    #    필드 생략 시 category=Story/repeatable=false라 기존 20개는 자동 통과.
    side_bad = []
    for q in quests:
        cat = q.get("category", "Story")
        rep = q.get("repeatable", False)
        inc = q.get("target_increment", 0)
        if rep and cat != "Side":
            side_bad.append(f"{q['questId']}(repeatable인데 category={cat})")
        if rep and inc <= 0:
            side_bad.append(f"{q['questId']}(repeatable인데 targetIncrement={inc}=목표상승 없음)")
    side_count = sum(1 for q in quests if q.get("category") == "Side")
    signals.append(("서브 퀘스트 정합 (반복/카테고리)", "0건",
                    f"{len(side_bad)}건 ({side_bad})" if side_bad
                    else f"0건 (Side {side_count}개 정합)",
                    "FAIL" if side_bad else "PASS"))

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
    print("# quest-lint — 곤충게임 퀘스트 정합성 검증\n")
    print("## 위험 신호 표")
    signals = evaluate_signals()
    print(render(signals))
    print()
    print("## 가정 / 한계")
    print("- 퀘스트는 TutorialQuestManager 배열에서 읽는다(SO/JSON 아님).")
    print("- 보상 곤충은 필드+가챠전용 ID로 대조. InsectDatabase .asset의 개체별 편차는 미반영.")
    print("- 검사 5는 update/event(구독)/notify(게임플레이 호출) 경로 중 하나라도 닿으면 통과.")
    fail = sum(1 for s in signals if s[3] == "FAIL")
    return 1 if fail else 0


if __name__ == "__main__":
    # 0=정상, 1=데이터 결함, 2=추출기 고장. 1과 2를 가른다(관통 원칙).
    try:
        sys.exit(main())
    except ExtractorBroken as e:
        print(f"\n## 추출기 고장\n\n**{e}**\n")
        print("퀘스트 데이터 결함이 아니라 이 스크립트가 코드를 못 따라간 것이다.")
        print("검증 결과는 신뢰할 수 없다 — 추출기를 먼저 고칠 것.")
        sys.exit(2)
