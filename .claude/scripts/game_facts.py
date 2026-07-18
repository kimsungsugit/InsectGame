"""게임 코드가 말하는 사실을 읽는 단일 모듈. 하네스는 수치 사본을 들지 않는다.

왜 있나
-------
`.claude/rules/balance.md`와 CLAUDE.md는 "수치의 단일 출처는 코드"라고 못박는다.
그런데 정작 그걸 검증하는 하네스 자신이 사본을 들고 있었다. gacha_sim.py의 BOX_DEFS는
골드 Legendary를 **5%**로 알고 있었지만 코드는 **45%**였다(GoldThresholds={5,10,23,55}
→ L=100-55). 실버/골드 가격도 800/1200으로 알았지만 실제는 600/750이었다. 브론즈만
맞았다 — 즉 원래 맞았던 사본이 코드 변경을 못 따라간 순수한 드리프트다.

그 위에서 돌린 시뮬은 존재하지 않는 게임을 시뮬레이션했고, "천장 부재" 판정과
"골드 Legendary 5%→7% 상향" 권고는 정반대로 무의미했다. 아이러니하게도 코드는 이미
GetGachaRates(boxId)로 "UI 표기 단일 출처"를 만들어놨다 — 코드가 사본을 없앤 뒤에도
하네스만 사본을 붙들고 있었다.

그래서 사실을 읽는 곳을 하나로 모은다. 여기가 유일한 추출 지점이다.

썩을 때 거짓말하지 않는 법
--------------------------
정규식 추출기는 앞으로도 리팩터링을 따라가지 못한다. 그건 막을 수 없다.
막을 수 있는 건 **썩으면서 거짓말하는 것**이다. 기대한 심볼을 못 찾으면 빈 값을
반환하고 계속 가는 대신 ExtractorBroken으로 죽는다. 호출자는 exit 2로 종료해
"데이터 결함"(exit 1)과 "검증기 자신의 고장"(exit 2)을 구별해야 한다.

조용한 오탐은 무시된다. 무시되는 검증기는 없느니만 못하다.
"""
import os
import re

from cs_strip import strip_cs  # 주석/문자열 제거 — 배선 분석이 주석을 코드로 오인하지 않게

BOXES = ("bronze", "silver", "gold")

PATHS = {
    "gacha": "Assets/Scripts/Core/GachaBoxManager.cs",
    "cash_shop": "Assets/Scripts/Core/CashShopManager.cs",
    "cash_shop_ui": "Assets/Scripts/UI/CashShopUI.cs",
    "reward_calc": "Assets/Scripts/Data/InsectRewardCalculator.cs",
    "tutorial": "Assets/Scripts/Core/TutorialQuestManager.cs",
    "insect_entity": "Assets/Scripts/Spawning/InsectEntity.cs",
    "raid": "Assets/Scripts/Battle/RaidBattleController.cs",
    "game_constants": "Assets/Scripts/Core/GameConstants.cs",
    "trainer_progress": "Assets/Scripts/Core/PlayerProgressController.cs",
    "insect_curve": "Assets/Scripts/Data/InsectLevelCurve.cs",
    "bootstrap": "Assets/Scripts/Core/PlaySceneBootstrap.cs",
    "insect_expansion": "Assets/Scripts/Data/InsectExpansionDefinitions.cs",
    "region_defs": "Assets/Scripts/Core/RegionDefinitions.cs",
    "tutorial_data": "Assets/Scripts/Core/TutorialQuestData.cs",
    "npc_dialogue": "Assets/Scripts/NPC/NpcDialogueDatabase.cs",
    "story_json": "Assets/Resources/Story.json",
    "story_director": "Assets/Scripts/Story/StoryDirector.cs",
    "item_db": "Assets/Scripts/Data/ItemDatabase.cs",
}

RARITIES = ("Common", "Uncommon", "Rare", "Epic", "Legendary")


class ExtractorBroken(Exception):
    """기대한 심볼을 코드에서 찾지 못했다 — 데이터 결함이 아니라 추출기 자신의 고장."""


def _read(key: str) -> str:
    path = PATHS[key]
    if not os.path.isfile(path):
        raise ExtractorBroken(f"{path}가 없다 — 파일이 옮겨갔는가?")
    with open(path, encoding="utf-8") as f:
        return f.read()


def _need(m, what: str, key: str):
    if not m:
        raise ExtractorBroken(f"{PATHS[key]}에서 {what}을(를) 찾지 못했다 — 개명/이동했는가?")
    return m


# ── 가챠 ────────────────────────────────────────────────────────────────────

def gacha_thresholds() -> dict:
    """{"bronze": [C상한, U상한, R상한, E상한], ...} 누적 임계값(roll 0~100).

    출처: GachaBoxManager.cs의 Bronze/Silver/GoldThresholds 상수 배열.
    Get{box}Rarity()는 이 배열을 GetRarityByThresholds()에 넘기는 한 줄일 뿐이므로
    메서드 본체를 파봐야 리터럴이 없다(예전 추출기가 여기서 조용히 실패했다).
    """
    src = _read("gacha")
    out = {}
    for box in BOXES:
        name = box.capitalize() + "Thresholds"
        m = _need(re.search(rf"{name}\s*=\s*\{{([^}}]*)\}}", src), f"{name} 배열", "gacha")
        vals = [float(x) for x in re.findall(r"([\d.]+)f", m.group(1))]
        if len(vals) != 4:
            raise ExtractorBroken(
                f"{name}에서 임계값 4개를 기대했으나 {len(vals)}개 추출 ({vals}) — 구조가 바뀌었는가?"
            )
        if vals != sorted(vals):
            raise ExtractorBroken(f"{name}이 단조증가가 아니다 ({vals}) — 추출이 어긋났는가?")
        out[box] = vals
    return out


def gacha_rarity_pcts() -> dict:
    """{"bronze": {"Common": 55.0, ..., "Legendary": 0.5}, ...} 등급별 확률(%).

    환산식은 GachaBoxManager.cs 주석이 명시한다:
    임계 [a,b,c,d] → C=a, U=b-a, R=c-b, E=d-c, L=100-d.
    """
    out = {}
    for box, t in gacha_thresholds().items():
        a, b, c, d = t
        out[box] = dict(zip(RARITIES, [a, b - a, c - b, d - c, 100.0 - d]))
    return out


def gacha_exclusive_chances() -> dict:
    """{"bronze": 0.2, "silver": 0.3, "gold": 0.5} — 전용(픽업) 곤충이 뽑힐 확률.

    출처: PickRandomInsect()의 boxId 분기. gold/silver는 명시 분기, 나머지는 else.
    """
    src = _read("gacha")
    body = _need(
        re.search(r"PickRandomInsect\s*\([^)]*\)\s*\{(.*?)\n\s{8}\}", src, re.DOTALL),
        "PickRandomInsect() 본체", "gacha",
    ).group(1)
    out = {}
    for box in ("gold", "silver"):
        m = _need(
            re.search(rf'boxId\s*==\s*"box_{box}"\s*\)\s*exclusiveChance\s*=\s*([\d.]+)f', body),
            f"box_{box} exclusiveChance 분기", "gacha",
        )
        out[box] = float(m.group(1))
    m = _need(
        re.search(r"else\s+exclusiveChance\s*=\s*([\d.]+)f", body),
        "기본(else) exclusiveChance", "gacha",
    )
    out["bronze"] = float(m.group(1))
    return out


def gacha_candy_bonus() -> dict:
    """{"bronze": (5, 15), ...} — 박스별 보너스 캔디 범위(양끝 포함).

    출처: OpenBox()의 switch(boxId) 안 Random.Range(min, maxExclusive).
    Unity의 int Random.Range는 상한 배타이므로 max는 -1 해서 돌려준다.
    """
    src = _read("gacha")
    out = {}
    for box in BOXES:
        m = _need(
            re.search(
                rf'case\s+"box_{box}":.*?Random\.Range\((\d+),\s*(\d+)\)', src, re.DOTALL
            ),
            f'case "box_{box}"의 보너스 캔디 Random.Range', "gacha",
        )
        lo, hi_excl = int(m.group(1)), int(m.group(2))
        out[box] = (lo, hi_excl - 1)
    return out


def gacha_exclusive_pool_sizes() -> dict:
    """{"Rare": 3, "Epic": 4, "Legendary": 3} — 등급별 전용 곤충 풀 크기."""
    src = _read("gacha")
    block = _need(
        re.search(r"gachaExclusives\s*=\s*new\s+Dictionary[^{]*\{(.*?)\n\s{8}\};", src, re.DOTALL),
        "gachaExclusives 딕셔너리", "gacha",
    ).group(1)
    out = {}
    for rarity, ids in re.findall(r"InsectRarity\.(\w+)\s*,\s*new\[\]\s*\{([^}]*)\}", block):
        out[rarity] = len(re.findall(r'"[^"]+"', ids))
    if not out:
        raise ExtractorBroken("gachaExclusives에서 등급별 풀을 하나도 못 읽었다 — 구조가 바뀌었는가?")
    return out


# ── 상점 ────────────────────────────────────────────────────────────────────

def box_gem_prices() -> dict:
    """{"bronze": 500, "silver": 600, "gold": 750} — 가챠 박스 젬 가격(정본).

    출처: CashShopManager.shopItems의 gemPrice.
    """
    src = _read("cash_shop")
    out = {}
    for box in BOXES:
        m = _need(
            re.search(rf'itemId\s*=\s*"box_{box}".*?gemPrice\s*=\s*(\d+)', src, re.DOTALL),
            f'box_{box}의 gemPrice', "cash_shop",
        )
        out[box] = int(m.group(1))
    return out


def gem_packages() -> list:
    """[(KRW, gems), ...] — 현금 젬 패키지. 출처: CashShopManager의 priceKRW > 0 품목."""
    src = _read("cash_shop")
    out = [
        (int(krw), int(count))
        for krw, count in re.findall(
            r"priceKRW\s*=\s*(\d+)\s*,\s*gemPrice\s*=\s*0\s*,\s*rewardCount\s*=\s*(\d+)", src
        )
        if int(krw) > 0
    ]
    if not out:
        raise ExtractorBroken(
            f"{PATHS['cash_shop']}에서 젬 패키지(priceKRW>0, gemPrice=0)를 찾지 못했다 — "
            "필드 순서나 구조가 바뀌었는가?"
        )
    return sorted(out)


def ui_box_prices() -> dict:
    """{"bronze": 500, ...} — CashShopUI가 화면에 찍는 박스 가격.

    출처: `GetGachaRateText("box_X"), <price>, gems, mobile` 호출 인자.
    가격은 아직 UI 리터럴이라 정본(box_gem_prices)과 갈릴 수 있다 — 그게 검사 대상이다.
    표시 가격과 실제 차감액이 갈리면 결제 오인이므로 값이 아니라 **일치 여부**가 중요하다.
    """
    src = _read("cash_shop_ui")
    out = {}
    for box in BOXES:
        m = _need(
            re.search(rf'GetGachaRateText\(\s*"box_{box}"\s*\)\s*,\s*(\d+)\s*,', src),
            f'box_{box} 박스 카드의 가격 인자', "cash_shop_ui",
        )
        out[box] = int(m.group(1))
    return out


def ui_derives_gacha_rates() -> bool:
    """CashShopUI가 확률 표기를 코드에서 파생받는가(하드코딩이 아닌가).

    한때는 UI가 확률 문자열을 하드코딩해서 "UI 텍스트 vs 코드 확률" 값 비교 검사가
    의미 있었다. 지금은 GetGachaRateText → GachaBoxManager.GetRateText → GetRates →
    *Thresholds로 파생된다(CashShopUI 주석: "하드코딩 금지(공시 위반 방지)").
    그래서 값 비교는 폐물이고, 남은 위험은 **하드코딩이 되돌아오는 회귀**뿐이다.
    """
    src = _read("cash_shop_ui")
    return bool(re.search(r"GetGachaRateText\s*\(", src)) and bool(
        re.search(r"mgr\.GetRateText\s*\(|GachaBoxManager\.Instance", src)
    )


def ui_hardcoded_rate_literals() -> list:
    """UI에 되살아난 확률 표기 리터럴(예: "C:55% U:30%"). 있으면 공시 위반 회귀."""
    src = _read("cash_shop_ui")
    return [
        m.group(0)
        for m in re.finditer(r'"[^"]*\b[CUREL]\s*:\s*\d+(?:\.\d+)?\s*%[^"]*"', src)
    ]


# ── 보상 ────────────────────────────────────────────────────────────────────

def rarity_multipliers() -> dict:
    """{"Common": 1.0, ..., "Legendary": 2.8} — 보상 배율.

    출처: InsectRewardCalculator.GetRarityMultiplier()의 case별 return.
    """
    src = _read("reward_calc")
    body = _need(
        re.search(r"GetRarityMultiplier\s*\([^)]*\)\s*\{(.*?)\n\s{8}\}", src, re.DOTALL),
        "GetRarityMultiplier() 본체", "reward_calc",
    ).group(1)
    out = {}
    for rarity, val in re.findall(r"case\s+InsectRarity\.(\w+):\s*return\s+([\d.]+)f", body):
        out[rarity] = float(val)
    # default(Common)는 case 없이 return일 수 있다
    if "Common" not in out:
        m = re.search(r"(?:default:|^\s*)\s*return\s+([\d.]+)f\s*;", body, re.MULTILINE)
        if m:
            out["Common"] = float(m.group(1))
    missing = [r for r in RARITIES if r not in out]
    if missing:
        raise ExtractorBroken(
            f"GetRarityMultiplier에서 {missing} 배율을 못 읽었다 — 분기 구조가 바뀌었는가?"
        )
    return out


def trainer_xp_curve() -> dict:
    """트레이너 레벨 EXP 곡선. 출처: PlayerProgressController.

    선형: max(floor, base + (level-1)*growth). 배틀/포획/레이드/튜토리얼 EXP가 여기로 간다.
    곤충 레벨(캔디, 지수)과는 별개 시스템이다 — progression_sim이 이 둘을 혼동했다.
    """
    src = _read("trainer_progress")
    base = int(_need(re.search(r"baseXpToLevel\s*=\s*(\d+)", src), "baseXpToLevel", "trainer_progress").group(1))
    growth = int(_need(re.search(r"xpGrowthPerLevel\s*=\s*(\d+)", src), "xpGrowthPerLevel", "trainer_progress").group(1))
    maxlv = int(_need(re.search(r"maxLevel\s*=\s*(\d+)", src), "maxLevel", "trainer_progress").group(1))
    # GetXpToNextLevel = Mathf.Max(floor, base + (level-1)*growth)
    floor_m = re.search(r"Mathf\.Max\((\d+),\s*baseXpToLevel", src)
    floor = int(floor_m.group(1)) if floor_m else 1
    return {"base": base, "growth": growth, "floor": floor, "max": maxlv, "kind": "linear"}


def insect_candy_curve() -> dict:
    """곤충 레벨업 캔디 비용 곡선. 출처: InsectLevelCurve.GetCandyCost.

    지수: baseCandyCost * growth^(level-1). 곤충은 이 캔디 경로로만 큰다
    (TryLevelUpWithCandy). 곤충 XP 곡선(GetXpToNextLevel, 20*1.12^)은 배선만 돼 있고
    게임플레이가 곤충에 XP를 주지 않아 미사용이다 — 진행 경로로 쓰면 안 된다.
    """
    src = _read("insect_curve")
    base = int(_need(re.search(r"baseCandyCost\s*=\s*(\d+)", src), "baseCandyCost", "insect_curve").group(1))
    # GetCandyCost 본체의 Mathf.Pow(1.14f, level - 1)
    body = _need(re.search(r"GetCandyCost\s*\([^)]*\)\s*\{(.*?)\n\s{8}\}", src, re.DOTALL),
                 "GetCandyCost() 본체", "insect_curve").group(1)
    growth = float(_need(re.search(r"Pow\(\s*([\d.]+)f", body), "GetCandyCost의 성장률", "insect_curve").group(1))
    maxlv = int(_need(re.search(r"maxLevel\s*=\s*(\d+)", src), "maxLevel", "insect_curve").group(1))
    return {"base": base, "growth": growth, "max": maxlv, "kind": "exponential"}


def battle_rewards_by_rarity() -> dict:
    """등급별 전투/포획 보상 base(등급배율 적용 전). 출처: PlaySceneBootstrap switch(rarity).

    실제 지급 = base * rarity_multiplier. 예: Legendary 캔디 = 6 * 2.8 = 16.8.
    반환: {"Common": {"exp": 5, "candy": 2}, ...}
    """
    src = _read("bootstrap")
    block = _need(
        re.search(r"switch\s*\(\s*rarity\s*\)\s*\{(.*?)itemRewardCount", src, re.DOTALL),
        "switch(rarity) 보상 블록", "bootstrap",
    ).group(1)
    out = {}
    # 각 case (default=Legendary)의 expReward/candyReward를 순서대로 잡는다.
    cases = re.findall(
        r"(?:case\s+InsectRarity\.(\w+)|default)\s*:.*?expReward\s*=\s*(\d+).*?candyReward\s*=\s*(\d+)",
        block, re.DOTALL,
    )
    order = ["Common", "Uncommon", "Rare", "Epic", "Legendary"]
    for i, (name, exp, candy) in enumerate(cases):
        key = name if name else (order[i] if i < len(order) else f"tier{i}")
        out[key] = {"exp": int(exp), "candy": int(candy)}
    missing = [r for r in RARITIES if r not in out]
    if missing:
        raise ExtractorBroken(
            f"switch(rarity)에서 {missing} 보상을 못 읽었다 ({len(cases)}개 case 추출) — 구조가 바뀌었는가?"
        )
    return out


def field_roster() -> dict:
    """{insectId: (rarity, spawnWeight)} — 필드 스폰되는 곤충 전체(weight>0).

    출처: PlaySceneBootstrap.CreateStableInsect(id, name, InsectRarity.X, weight, ...) +
    InsectExpansionDefinitions.new InsectSeed(id, name, InsectRarity.X, weight, ...).
    가챠 전용(weight=0)은 필드 스폰이 없으므로 제외한다. InsectSpawner.GetWeightedRandom이
    이 spawnWeight로 후보를 뽑으므로, 리전 내 실제 조우 등급 분포의 단일 출처다.
    """
    out = {}
    for key, pat in (
        ("bootstrap", r'CreateStableInsect\("([^"]+)",\s*"[^"]+",\s*InsectRarity\.(\w+),\s*([\d.]+)f'),
        ("insect_expansion", r'new InsectSeed\("([^"]+)",\s*"[^"]+",\s*InsectRarity\.(\w+),\s*([\d.]+)f'),
    ):
        for _id, rarity, w in re.findall(pat, _read(key)):
            weight = float(w)
            if weight > 0:
                out[_id] = (rarity, weight)
    if not out:
        raise ExtractorBroken(
            "필드 곤충 로스터를 하나도 못 읽었다 (CreateStableInsect/InsectSeed) — 시그니처가 바뀌었는가?"
        )
    return out


def all_insect_ids() -> set:
    """게임에 존재하는 모든 곤충 ID (필드 + 가챠 전용). 퀘스트 보상 곤충 검증용.

    field_roster는 weight>0만 주므로 별개다 — 보상은 가챠 전용(weight 0) 곤충일 수도 있다.
    """
    ids = set()
    for key, pat in (
        ("bootstrap", r'CreateStableInsect\("([^"]+)"'),
        ("insect_expansion", r'new InsectSeed\("([^"]+)"'),
    ):
        ids |= set(re.findall(pat, _read(key)))
    block = re.search(r"gachaExclusives\s*=.*?\{(.*?)\n\s{8}\};", _read("gacha"), re.DOTALL)
    if block:
        ids |= set(re.findall(r'"(gacha_\w+)"', block.group(1)))
    if not ids:
        raise ExtractorBroken("곤충 ID를 하나도 못 읽었다 (CreateStableInsect/InsectSeed)")
    return ids


def item_ids() -> set:
    """게임에 존재하는 모든 아이템 ID. 보상 아이템(rewardItemId) 검증용 레지스트리.

    ItemDatabase.CreateRuntimeDefault()의 CreateItem("id", ...)가 런타임 아이템 레지스트리다
    (PlayerItemInventory.AddItem이 이 ID로 해석). 채집망은 capture item, exp_boost처럼 상점
    보상으로만 지급되는 아이템은 여기 등록돼 있으나 capture item 목록엔 없다 — 그래서 lint는
    이 집합을 capture item·shop reward와 합집합해 어느 소스의 아이템이든 인정한다.
    """
    ids = set(re.findall(r'CreateItem\(\s*"(\w+)"', _read("item_db")))
    if not ids:
        raise ExtractorBroken('아이템 ID를 하나도 못 읽었다 (ItemDatabase.CreateItem) — 구조가 바뀌었는가?')
    return ids


# ── 퀘스트 ──────────────────────────────────────────────────────────────────

def quest_types_enum() -> list:
    """QuestType enum 목록. 출처: TutorialQuestData.cs."""
    src = _read("tutorial_data")
    m = _need(re.search(r"enum\s+QuestType\s*\{([^}]*)\}", src), "QuestType enum", "tutorial_data")
    return [
        t.strip() for t in m.group(1).split(",")
        if t.strip() and not t.strip().startswith("//")
    ]


def quest_defs() -> list:
    """[{questId, type, prereq, reward_insect, reward_item, reward_item_count, target}, ...]

    출처: TutorialQuestManager의 allQuests 배열. 각 `new TutorialQuest { ... }` 블록을 파싱한다.
    블록 안에 중첩 중괄호가 없어(필드는 전부 리터럴) [^}]* 로 안전하게 자른다.
    """
    src = _read("tutorial")
    arr = _need(
        re.search(r"allQuests\s*=\s*new\s+TutorialQuest\[\]\s*\{(.*?)\n\s*\};", src, re.DOTALL),
        "allQuests 배열", "tutorial",
    )
    out = []
    for block in re.findall(r"new\s+TutorialQuest\s*\{([^}]*)\}", arr.group(1), re.DOTALL):
        def s(name):
            m = re.search(rf'{name}\s*=\s*"([^"]*)"', block)
            return m.group(1) if m else None

        def i(name):
            m = re.search(rf"{name}\s*=\s*(\d+)", block)
            return int(m.group(1)) if m else None

        t = re.search(r"type\s*=\s*QuestType\.(\w+)", block)
        cat = re.search(r"category\s*=\s*QuestCategory\.(\w+)", block)
        rep = re.search(r"repeatable\s*=\s*(true|false)", block)
        out.append({
            "questId": s("questId"),
            "type": t.group(1) if t else None,
            "prereq": s("prerequisiteQuestId"),
            "reward_insect": s("rewardInsectId"),
            "reward_item": s("rewardItemId"),
            "reward_item_count": i("rewardItemCount"),
            "target": i("targetCount"),
            # 분류/반복 상승(필드 생략 시 C# 기본값: category=Story, repeatable=false, increment=0).
            "category": cat.group(1) if cat else "Story",
            "repeatable": (rep.group(1) == "true") if rep else False,
            "target_increment": i("targetIncrement") or 0,
        })
    if not out:
        raise ExtractorBroken("allQuests 배열에서 퀘스트를 하나도 못 읽었다 — 구조가 바뀌었는가?")
    return out


def quest_progress_wiring() -> dict:
    """{QuestType: [(via, wired, detail), ...]} — 각 QuestType이 IncrementProgress에 닿는 경로들.

    세 경로가 있다:
      - 'update' : Update() 본문이 직접 처리 (Movement). 항상 wired.
      - 'event'  : OnXxx() 핸들러가 NotifyAction(QuestType.Y). SubscribeEvents에 그 핸들러가
                   += 로 등록됐는지가 wired. **q_team 회귀(핸들러는 있으나 미등록)를 잡는 지점.**
      - 'notify' : 외부 Notify 메서드가 처리. wired=None — 게임플레이 호출부 존재는 quest_lint가
                   코드베이스 grep으로 확인한다(game_facts는 단일 파일만 읽는다).

    한 QuestType이 여러 경로를 가질 수 있다(예: Battle = 이벤트 + NotifyBattleWon). quest_lint는
    경로 중 하나라도 reachable이면 통과로 본다.

    주석을 제거하고 분석한다 — `// TeamChanged += OnTeamChanged`처럼 주석 처리된 구독을
    등록으로 오인하면 q_team류 회귀(구독 누락)를 놓친다.
    """
    src = strip_cs(_read("tutorial"))

    upd = re.search(r"void\s+Update\s*\(\)\s*\{(.*?)\n\s{8}\}", src, re.DOTALL)
    update_types = set(re.findall(r"QuestType\.(\w+)", upd.group(1))) if upd else set()

    # 이벤트 핸들러 On___ 이 NotifyAction(QuestType.X)를 부름 → {qtype: handler}.
    # 핸들러가 `=> NotifyAction(...)` 한 줄이거나 `{ if (...) { NotifyAction(...) } }` 중첩
    # 블록일 수 있어, 블록은 중괄호 깊이로 본문을 통째로 떠서 그 안의 NotifyAction을 찾는다.
    # (옛 정규식 [^;{}]* 는 중첩 { 에서 멈춰 VisitRegion/VisitSubArea를 놓쳤다.)
    handler_of = {}  # qtype -> handler name (On___)
    for m in re.finditer(r"\b(On\w+)\s*\([^)]*\)\s*(?:=>\s*([^;]*);|\{)", src):
        handler = m.group(1)
        if m.group(2) is not None:
            body = m.group(2)
        else:
            depth, i = 1, m.end()
            while i < len(src) and depth:
                if src[i] == "{":
                    depth += 1
                elif src[i] == "}":
                    depth -= 1
                i += 1
            body = src[m.end():i]
        for qt in re.findall(r"NotifyAction\(QuestType\.(\w+)\)", body):
            handler_of[qt] = handler

    sub = re.search(r"SubscribeEvents\s*\(\)\s*\{(.*?)\n\s{8}\}", src, re.DOTALL)
    subscribed = set(re.findall(r"\+=\s*(On\w+)", sub.group(1))) if sub else set()

    notify_of = {}  # qtype -> notify method name
    for m in re.finditer(
        r"public\s+void\s+(Notify\w+)\s*\([^)]*\)\s*\{(.*?)\n\s{8}\}", src, re.DOTALL
    ):
        name, body = m.group(1), m.group(2)
        for qt in re.findall(r"QuestType\.(\w+)", body):
            notify_of.setdefault(qt, name)

    out = {}
    for qt in update_types | set(handler_of) | set(notify_of):
        paths = []
        if qt in update_types:
            paths.append(("update", True, "Update() 내부"))
        if qt in handler_of:
            h = handler_of[qt]
            paths.append(("event", h in subscribed, h))
        if qt in notify_of:
            paths.append(("notify", None, notify_of[qt]))
        out[qt] = paths
    return out


# ── 스토리 ──────────────────────────────────────────────────────────────────

def story_beats() -> list:
    """Story.json의 비트 목록 (파싱된 dict). json.load라 퀘스트 정규식보다 견고하다.

    설계(Docs/StorySystemDesign.md)가 데이터 모델을 JSON으로 정한 이유가 이것이다 —
    lines[]/choices[] 중첩 구조를 정규식으로 자르는 대신 네이티브 파싱한다.
    """
    import json
    path = PATHS["story_json"]
    if not os.path.isfile(path):
        raise ExtractorBroken(f"{path}가 없다 — 스토리 시스템이 아직 없거나 파일이 옮겨갔는가?")
    try:
        with open(path, encoding="utf-8") as f:
            data = json.load(f)
    except (OSError, ValueError) as e:
        raise ExtractorBroken(f"{path} 파싱 실패: {e}")
    beats = data.get("beats")
    if beats is None:
        raise ExtractorBroken(f"{path}에 'beats' 키가 없다 — StoryList 형식이 바뀌었는가?")
    return beats


def story_trigger_wiring() -> dict:
    """{triggerType: (in_switch, has_event_source)} — StoryDirector가 각 트리거를 처리하는가.

    검사 근거: JSON이 쓰는 trigger.type이 (a) EvaluateTriggers switch의 case에 있고
    (b) 그 트리거를 발화하는 이벤트 소스가 구독돼 있어야 비트가 발화한다. q_team 회귀의
    스토리 등가물 — 새 trigger.type을 JSON에 넣고 switch/구독을 빠뜨리면 비트 영구 미발화.

    - in_switch: `case TriggerX:` 또는 상수 정의가 switch 문맥에 존재
    - has_event_source: 그 트리거를 EvaluateTriggers(TriggerX, ...)로 호출하는 지점이 있음
      (Immediate는 Start에서, 나머지는 OnXxx 이벤트 핸들러에서)
    """
    raw = _read("story_director")
    # 상수 정의는 원본에서 읽는다 — strip_cs가 "Y" 리터럴을 지우면 값을 못 읽는다.
    consts = dict(re.findall(r'const\s+string\s+(Trigger\w+)\s*=\s*"(\w+)"', raw))
    if not consts:
        raise ExtractorBroken("StoryDirector에서 Trigger 상수를 못 읽었다 — 정의 형태가 바뀌었는가?")
    # switch case / 발화 지점은 주석 제거 후에 본다 — 주석 처리된 case를 살아있다고 오인 않게.
    src = strip_cs(raw)
    in_switch = set(re.findall(r"case\s+(Trigger\w+)\s*:", src))
    fired = set(re.findall(r"EvaluateTriggers\(\s*(Trigger\w+)", src))

    out = {}
    for const, ttype in consts.items():
        out[ttype] = (const in in_switch, const in fired)
    return out


def dialogue_region_keys() -> set:
    """NpcDialogueDatabase.RegionLines의 regionId 키 집합. 리전 ID 드리프트 검증용."""
    src = _read("npc_dialogue")
    block = _need(
        re.search(r"RegionLines\s*=\s*new\s+Dictionary[^{]*\{(.*?)\n\s*\};", src, re.DOTALL),
        "RegionLines 딕셔너리", "npc_dialogue",
    )
    # collection initializer 형태: ["meadow"] = new[]{...}
    keys = set(re.findall(r'\["(\w+)"\]\s*=', block.group(1)))
    if not keys:
        raise ExtractorBroken("RegionLines에서 regionId 키를 못 읽었다 — 초기화 형태가 바뀌었는가?")
    return keys


def region_pools() -> list:
    """[(regionId, requiredLevel, [insectIds]), ...] — 리전별 메인필드 곤충 풀.

    출처: RegionDefinitions.CreateAll()의 각 RegionData{ regionId, requiredLevel, insectIds }.
    서브에리어(exclusiveInsectIds)는 선택적 고레벨 구역이라 제외 — 메인 진행 경로만 본다.
    """
    src = _read("region_defs")
    pools = []
    for m in re.finditer(
        r'regionId = "(\w+)".*?requiredLevel = (\d+),\s*insectIds = new\[\]\s*\{(.*?)\}',
        src, re.DOTALL,
    ):
        ids = re.findall(r'"([^"]+)"', m.group(3))
        if ids:
            pools.append((m.group(1), int(m.group(2)), ids))
    if not pools:
        raise ExtractorBroken(
            "RegionDefinitions에서 리전 풀(regionId/requiredLevel/insectIds)을 못 읽었다 — 구조가 바뀌었는가?"
        )
    return pools


def team_max_slots() -> int:
    """배틀 팀 최대 슬롯 수. 출처: GameConstants.Battle.MaxTeamSlots.

    progression_sim이 이 값을 6으로 하드코딩해 "팀 6마리 캔디 비용 FAIL"을 냈는데,
    실제는 5다(GameConstantsTests가 단언). 6마리는 게임에 존재하지 않는 팀이라 신호가
    통째로 허수였다.
    """
    src = _read("game_constants")
    m = _need(re.search(r"MaxTeamSlots\s*=\s*(\d+)", src), "MaxTeamSlots", "game_constants")
    return int(m.group(1))


def raid_reward_mult() -> float:
    """레이드 보상 배율. 출처: RaidBattleController의 `RewardCandy = ...candyBase * N * ...`.

    상수로 분리돼 있지 않고 계산식에 박힌 매직넘버라 계산식째로 고정해 읽는다.
    캔디와 EXP가 서로 다른 배율을 쓰면 가정이 깨진 것이므로 죽는다.
    """
    src = _read("raid")
    candy = _need(
        re.search(r"RewardCandy\s*=\s*Mathf\.RoundToInt\(\s*candyBase\s*\*\s*([\d.]+)f?\s*\*", src),
        "RewardCandy 계산식의 레이드 배율", "raid",
    )
    exp = _need(
        re.search(r"RewardExp\s*=\s*Mathf\.RoundToInt\(\s*expBase\s*\*\s*([\d.]+)f?\s*\*", src),
        "RewardExp 계산식의 레이드 배율", "raid",
    )
    c, e = float(candy.group(1)), float(exp.group(1))
    if c != e:
        raise ExtractorBroken(
            f"레이드 캔디 배율({c})과 EXP 배율({e})이 다르다 — 단일 배율 가정이 깨졌다"
        )
    return c


def tutorial_rewards() -> dict:
    """{"candy": 336, "exp": 475} — 튜토리얼 퀘스트 보상 총합.

    출처: TutorialQuestManager.cs의 rewardCandy = N / rewardExp = N 대입 전량.
    지급 코드(quest.rewardCandy 등)는 대입이 아니라 자연히 제외된다.
    """
    src = _read("tutorial")
    candy = [int(x) for x in re.findall(r"rewardCandy\s*=\s*(\d+)", src)]
    exp = [int(x) for x in re.findall(r"rewardExp\s*=\s*(\d+)", src)]
    if not candy or not exp:
        raise ExtractorBroken(
            f"튜토리얼 보상 대입을 못 찾았다 (candy {len(candy)}건 / exp {len(exp)}건) — "
            "필드명이 바뀌었는가?"
        )
    return {"candy": sum(candy), "exp": sum(exp), "candy_n": len(candy), "exp_n": len(exp)}


def field_shiny_pct() -> float:
    """필드 샤이니 확률(%). 출처: InsectEntity.cs의 `shiny = Random.value < 0.01f`.

    느슨한 정규식(`shiny\\w*\\s*[=<]\\s*([\\d.]+)f`)을 먼저 썼다가 `cachedShinyShift = -1f`
    같은 무관한 필드를 물어 조용히 0.0을 반환했다. 대입 형태를 통째로 고정한다 —
    형태가 바뀌면 0을 반환하는 대신 ExtractorBroken으로 죽는 게 낫다.
    """
    src = _read("insect_entity")
    m = _need(
        re.search(r"\bshiny\s*=\s*UnityEngine\.Random\.value\s*<\s*([\d.]+)f", src),
        "`shiny = UnityEngine.Random.value < Xf` 형태의 샤이니 확률",
        "insect_entity",
    )
    return float(m.group(1)) * 100.0


def gacha_has_shiny() -> bool:
    """가챠에 샤이니 로직이 있는가. 없으면 필드와의 격차가 위험 신호."""
    return bool(re.search(r"[Ss]hiny", _read("gacha")))


if __name__ == "__main__":
    import io
    import sys

    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    try:
        print("# 코드가 말하는 사실\n")
        print(f"가챠 임계값      : {gacha_thresholds()}")
        for box, pcts in gacha_rarity_pcts().items():
            print(f"  {box:7} 확률(%): " + " / ".join(f"{k[0]}{v:g}" for k, v in pcts.items()))
        print(f"박스 젬 가격     : {box_gem_prices()}")
        print(f"픽업 확률        : {gacha_exclusive_chances()}")
        print(f"보너스 캔디      : {gacha_candy_bonus()}")
        print(f"전용 풀 크기     : {gacha_exclusive_pool_sizes()}")
        print(f"보상 배율        : {rarity_multipliers()}")
        print(f"튜토리얼 보상 합 : {tutorial_rewards()}")
        print(f"필드 샤이니(%)   : {field_shiny_pct()}")
        print(f"가챠 샤이니 존재 : {gacha_has_shiny()}")
    except ExtractorBroken as e:
        print(f"\n추출기 고장: {e}")
        sys.exit(2)
