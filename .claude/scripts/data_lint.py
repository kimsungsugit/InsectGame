"""데이터 정합성 자동 검증. 코드 내 하드코딩된 ID 정의/참조 비교, 고아/누락/중복 검출."""
import argparse
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import game_facts  # noqa: E402
from game_facts import ExtractorBroken  # noqa: E402  — 예외 타입은 game_facts가 소유

if hasattr(sys.stdout, "reconfigure") and sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    sys.stdout.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]

# === 임계값 ===
THRESHOLD_DEAD_METHOD_FAIL = True              # 호출자 0건 메서드 = FAIL
THRESHOLD_MISSING_REF_FAIL = True              # 참조하는데 정의 없는 ID = FAIL
THRESHOLD_ORPHAN_DEF_WARN = True               # 정의했는데 참조 0건 = WARN
THRESHOLD_DUPLICATE_DEF_WARN = True            # 같은 ID 다중 정의 = WARN
THRESHOLD_GACHA_PROB_TOLERANCE = 0.01          # 가챠 확률 합 100% ± 0.01

# === Unity 메서드 콜백 (반사 호출되므로 dead 검출 제외) ===
UNITY_CALLBACKS = {
    "Awake", "Start", "Update", "FixedUpdate", "LateUpdate",
    "OnEnable", "OnDisable", "OnDestroy", "OnGUI",
    "OnApplicationQuit", "OnApplicationPause", "OnApplicationFocus",
    "OnTriggerEnter", "OnTriggerStay", "OnTriggerExit",
    "OnCollisionEnter", "OnCollisionStay", "OnCollisionExit",
    "OnDrawGizmos", "OnDrawGizmosSelected", "OnValidate", "Reset",
}

# ExtractorBroken은 game_facts가 소유한다 (위에서 import). 예외 타입까지 사본을 두면
# 한쪽만 잡히는 사고가 난다.

# === 데이터 소스 경로 ===
PATHS = {
    "bootstrap": "Assets/Scripts/Core/PlaySceneBootstrap.cs",
    "region_defs": "Assets/Scripts/Core/RegionDefinitions.cs",
    "region_mgr": "Assets/Scripts/Core/RegionManager.cs",
    "region_terrain": "Assets/Scripts/Core/RegionTerrainBuilder.cs",
    "region_map_ui": "Assets/Scripts/UI/RegionMapUI.cs",
    "audio_mgr": "Assets/Scripts/Core/AudioManager.cs",
    "cash_shop": "Assets/Scripts/Core/CashShopManager.cs",
    "cash_shop_ui": "Assets/Scripts/UI/CashShopUI.cs",
    "gacha_mgr": "Assets/Scripts/Core/GachaBoxManager.cs",
    "shop_ui": "Assets/Scripts/Core/ShopUIController.cs",
    "spawner": "Assets/Scripts/Spawning/InsectSpawner.cs",
}


def _read(path: str) -> str:
    if not os.path.isfile(path):
        return ""
    try:
        with open(path, encoding="utf-8") as f:
            return f.read()
    except (OSError, UnicodeDecodeError):
        return ""


def _read_clean(path: str) -> str:
    """주석 제거된 파일 내용 반환. 추출기에서 false positive 방지용."""
    return _strip_comments(_read(path))


def _strip_comments(content: str) -> str:
    """C# 한 줄 주석 (//...) 제거. 블록 주석(/*...*/)은 단순 처리만."""
    lines = []
    for line in content.splitlines():
        # `//`이 라인의 첫 비공백이면 주석 라인. 인라인 주석은 보존 (코드도 살릴 수 있음)
        stripped = line.lstrip()
        if stripped.startswith("//"):
            lines.append("")  # 빈 줄로 대체 (라인 번호 보존)
        else:
            # 인라인 주석 제거 (간단 처리, 문자열 안의 //는 무시 못 함 — 보수적 처리)
            idx = line.find("//")
            if idx >= 0 and '"' not in line[:idx]:
                lines.append(line[:idx])
            else:
                lines.append(line)
    return "\n".join(lines)


# ===== 추출기 =====

def extract_region_ids() -> set:
    """RegionDefinitions.CreateAll() 안의 regionId 추출. 다음 메서드 정의까지.

    이전엔 PlaySceneBootstrap의 CreateRegions()/CreateExpandedRegions()를 팠다. 두 메서드는
    이제 코드베이스에 0곳이다 — 정의가 RegionDefinitions.cs로 분리됐다.
    """
    content = _read_clean(PATHS["region_defs"])
    if not content:
        raise ExtractorBroken(f"{PATHS['region_defs']}를 읽을 수 없음 — 파일이 옮겨갔는가?")
    pattern = re.compile(
        r"CreateAll\s*\(\)\s*\{(.*?)(?=\n\s*(?:private|public)\s+(?:static\s+)?[\w\.\[\]<>]+\s+\w+\s*\()",
        re.DOTALL
    )
    m = pattern.search(content)
    if not m:
        raise ExtractorBroken(
            f"{PATHS['region_defs']}에서 CreateAll() 본체를 찾지 못함 — 메서드가 개명/이동했는가?"
        )
    ids = set(re.findall(r'regionId\s*=\s*"(\w+)"', m.group(1)))
    if not ids:
        raise ExtractorBroken(
            "CreateAll() 본체에서 regionId를 하나도 못 찾음 — 정의 문법이 바뀌었는가?"
        )
    return ids


def extract_region_refs() -> dict:
    """파일별로 regionId 참조 수집. environmentType case (GetSubAreaColor)는 제외."""
    refs = {}
    # RegionManager switch
    content = _read_clean(PATHS["region_mgr"])
    refs["RegionManager"] = set(re.findall(r'case\s+"(\w+)":\s*return\s+"\w+"', content))
    refs["RegionManager"].update(re.findall(r'return\s+"(\w+)";', content))
    # RegionTerrainBuilder
    content = _read_clean(PATHS["region_terrain"])
    refs["RegionTerrainBuilder"] = set(re.findall(r'case\s+"(\w+)":', content))
    # RegionMapUI: connections 배열 + GetRegionSymbol만 (GetSubAreaColor는 environmentType이라 제외)
    content = _read_clean(PATHS["region_map_ui"])
    refs["RegionMapUI"] = set(re.findall(r'\{\s*"(\w+)"\s*,\s*"\w+"\s*\}', content))
    refs["RegionMapUI"].update(re.findall(r'\{\s*"\w+"\s*,\s*"(\w+)"\s*\}', content))
    # GetRegionSymbol 메서드 본체에서만 case 추출 (다음 메서드 정의 전까지)
    sym_match = re.search(
        r'GetRegionSymbol\s*\([^)]*\)\s*\{(.*?)(?=\n\s*private|\n\s*public)',
        content, re.DOTALL
    )
    if sym_match:
        refs["RegionMapUI"].update(re.findall(r'case\s+"(\w+)":', sym_match.group(1)))
    # AudioManager
    content = _read_clean(PATHS["audio_mgr"])
    refs["AudioManager"] = set(re.findall(r'case\s+"(\w+)":', content))
    return refs


def detect_dead_methods() -> list:
    """PlaySceneBootstrap의 private 메서드 중 참조 0건 검출.
    참조 = 정의 라인 외에서 메서드명이 등장하는 라인 (직접 호출 + 이벤트 += 구독 + 델리게이트 할당 포함).
    Unity 콜백(Awake/Update 등)은 반사 호출이므로 제외."""
    content = _read_clean(PATHS["bootstrap"])
    methods = {}  # name -> 정의 라인
    for m in re.finditer(
        r'^\s*private\s+(?:static\s+)?[\w\.\[\]<>]+\s+(\w+)\s*\(',
        content, re.MULTILINE
    ):
        name = m.group(1)
        if name in UNITY_CALLBACKS:
            continue
        methods[name] = content[:m.start()].count("\n") + 1

    dead = []
    lines = content.splitlines()
    for name, def_line in methods.items():
        ref_count = 0
        for i, line in enumerate(lines, 1):
            if i == def_line:
                continue
            if re.search(rf'\b{re.escape(name)}\b', line):
                ref_count += 1
                break
        if ref_count == 0:
            dead.append((name, def_line))
    return sorted(dead, key=lambda x: x[1])


def extract_cashshop_items() -> tuple:
    """CashShopManager의 itemId, rewardItemId 추출. 주석 라인 제외."""
    content = _read_clean(PATHS["cash_shop"])
    items = re.findall(r'itemId\s*=\s*"(\w+)"', content)
    rewards = re.findall(r'rewardItemId\s*=\s*"(\w+)"', content)
    return set(items), set(rewards)


def extract_capture_items() -> set:
    """PlaySceneBootstrap.CreateCaptureItems의 itemId 추출. nested {} 회피 위해 다음 메서드 정의까지로 한정."""
    content = _read_clean(PATHS["bootstrap"])
    pattern = re.compile(
        r"CreateCaptureItems\s*\(\)\s*\{(.*?)(?=\n\s*(?:private|public)\s+(?:static\s+)?[\w\.\[\]<>]+\s+\w+\s*\()",
        re.DOTALL
    )
    m = pattern.search(content)
    if not m:
        return set()
    return set(re.findall(r'itemId\s*=\s*"(\w+)"', m.group(1)))


def extract_cashshop_ui_boxes() -> dict:
    """CashShopUI.DrawBoxCard 호출에서 boxId/price/rateText 추출.
    rateText는 'C:55% U:30% ...' 형식. 반환: {boxId: {'price': int, 'rates': {grade: pct}}}"""
    content = _read_clean(PATHS["cash_shop_ui"])
    boxes = {}
    # 시그니처: DrawBoxCard("boxId", "title", new Color(...), "rateText", price, gems)
    # `new Color(...)` 안의 콤마 때문에 lazy 매칭 사용
    pattern = re.compile(
        r'DrawBoxCard\(\s*"(box_\w+)"\s*,\s*"[^"]+"\s*,.+?,\s*"([^"]+)"\s*,\s*(\d+)\s*,',
        re.DOTALL
    )
    for m in pattern.finditer(content):
        box_id = m.group(1)
        rate_text = m.group(2)
        rates = {r.group(1).upper(): float(r.group(2))
                 for r in re.finditer(r'([CUREL])\s*:\s*([\d.]+)%', rate_text)}
        boxes[box_id] = {"price": int(m.group(3)), "rates": rates}
    return boxes


def extract_cashshop_manager_box_prices() -> dict:
    """CashShopManager.shopItems에서 box_* itemId의 gemPrice 추출."""
    content = _read_clean(PATHS["cash_shop"])
    prices = {}
    for m in re.finditer(
        r'itemId\s*=\s*"(box_\w+)".*?gemPrice\s*=\s*(\d+)',
        content
    ):
        prices[m.group(1)] = int(m.group(2))
    return prices


def extract_gacha_pools() -> dict:
    """GachaBoxManager의 gachaExclusives 사전 영역만 추출."""
    content = _read_clean(PATHS["gacha_mgr"])
    # gachaExclusives 사전 본체만 분리 (`Dictionary<InsectRarity, string[]> gachaExclusives = new ... { ... };`)
    excl_match = re.search(
        r'gachaExclusives\s*=\s*new\s+Dictionary<InsectRarity,\s*string\[\]>\s*\{(.*?)\};',
        content, re.DOTALL
    )
    pools = {}
    if excl_match:
        body = excl_match.group(1)
        # { InsectRarity.Rare, new[] { "id1", "id2" } }
        for m in re.finditer(r'InsectRarity\.(\w+)\s*,\s*new\[\]\s*\{([^}]*)\}', body):
            rarity = m.group(1)
            ids = re.findall(r'"(gacha_\w+)"', m.group(2))
            pools[rarity] = set(ids)
    # exclusiveDisplayNames: { "id", "표시명" }
    name_map = dict(re.findall(r'\{\s*"(gacha_\w+)"\s*,\s*"([^"]+)"\s*\}', content))
    return {"pools": pools, "names": name_map}


def extract_gacha_normal_pool() -> dict:
    """GachaBoxManager의 `normalPool` — {티어: {insectId, ...}}.

    `gachaExclusives`와 달리 여기 ID는 **일반 곤충**이라 접두가 없다. 티어별로 나뉜 이 배치가
    곧 저가 박스의 상한이므로, DB 실제 rarity와 어긋나면 브론즈 상자가 상위 곤충을 흘린다.
    """
    content = _read_clean(PATHS["gacha_mgr"])
    match = re.search(
        r'normalPool\s*=\s*new\s+Dictionary<InsectRarity,\s*string\[\]>\s*\{(.*?)\n        \};',
        content, re.DOTALL
    )
    if not match:
        raise ExtractorBroken(
            "GachaBoxManager에서 normalPool을 못 읽었다 — 사전 구조가 바뀌었는가?")

    pools = {}
    for m in re.finditer(r'InsectRarity\.(\w+),\s*new\[\]\s*\{(.*?)\}\s*\}', match.group(1), re.DOTALL):
        pools[m.group(1)] = set(re.findall(r'"([^"]+)"', m.group(2)))
    if not pools:
        raise ExtractorBroken("normalPool에서 티어를 하나도 못 읽었다")
    return pools


def extract_insect_rarities() -> dict:
    """{insectId: "Common"|...} — 곤충 등급의 실질 단일 출처 세 곳을 합친다.

    시드 파일 둘(1막·2막 확장)과 부트스트랩의 `CreateStableInsect`(기본 종·가챠 전용).
    셋 다 코드 하드코딩이라 `.asset`을 읽을 필요가 없다.
    """
    out = {}
    seed_files = [
        "Assets/Scripts/Data/InsectExpansionDefinitions.cs",
        "Assets/Scripts/Data/InsectExpansion2Definitions.cs",
    ]
    for path in seed_files:
        content = _read_clean(path)
        for iid, rarity in re.findall(
                r'new InsectSeed\(\s*"([^"]+)",\s*"[^"]*",\s*InsectRarity\.(\w+)', content):
            out[iid] = rarity

    boot = _read_clean(PATHS["bootstrap"])
    for iid, rarity in re.findall(
            r'CreateStableInsect\(\s*"([^"]+)",\s*"[^"]*",\s*InsectRarity\.(\w+)', boot):
        out[iid] = rarity

    if not out:
        raise ExtractorBroken(
            "곤충 등급을 하나도 못 읽었다 — 시드/CreateStableInsect 시그니처가 바뀌었는가?")
    return out


def extract_gacha_probabilities() -> dict:
    """{"bronze": [C상한, U상한, R상한, E상한], ...} 누적 임계값.

    추출 자체는 game_facts가 소유한다 — 여기에 또 사본을 두면 같은 병이 반복된다.
    """
    return game_facts.gacha_thresholds()


# ===== 평가 =====

def evaluate_signals() -> list:
    signals = []

    # 1. 리전 정의 vs 참조 정합성
    defined = extract_region_ids()
    refs = extract_region_refs()
    all_refs = set()
    for v in refs.values():
        all_refs.update(v)

    missing = all_refs - defined
    orphan = defined - all_refs
    judge = "FAIL" if missing else "PASS"
    signals.append((
        "리전 참조 정합성 (정의 없는데 참조)",
        "0건",
        f"{len(missing)}건 ({sorted(missing)})" if missing else "0건",
        judge
    ))

    judge = "WARN" if orphan else "PASS"
    signals.append((
        "리전 고아 (정의했는데 참조 0건)",
        "0건",
        f"{len(orphan)}건 ({sorted(orphan)})" if orphan else "0건",
        judge
    ))

    # 2. (삭제) 리전 중복 정의 — CreateRegions vs CreateExpandedRegions 이분법은 없어졌다.
    #    정의는 RegionDefinitions.CreateAll() 하나뿐이라 중복될 곳이 없다. 두 메서드가
    #    코드에서 사라진 뒤로 이 검사는 빈 집합 ∩ 빈 집합 = PASS만 찍는 공허한 통과였다.

    # 3. dead method 일반 검출 (PlaySceneBootstrap의 private 메서드 중 참조 0건)
    dead_methods = detect_dead_methods()
    judge = "FAIL" if dead_methods else "PASS"
    detail = ", ".join(f"{n}() L{l}" for n, l in dead_methods) if dead_methods else "0건"
    signals.append((
        "dead method 일반 검출 (PlaySceneBootstrap)",
        "0건",
        detail,
        judge
    ))

    # 4. CashShop rewardItemId ↔ CreateCaptureItems 매칭
    shop_items, reward_items = extract_cashshop_items()
    capture_items = extract_capture_items()
    # rewardItemId 중 캐시샵 아이템(gem_*, shop_*, box_*) 외의 ID는 실제 아이템 풀에 있어야.
    # 소비형 아이템 풀은 하드코딩 사본 대신 ItemDatabase(CreateItem) 단일 출처(game_facts.item_ids)를 읽는다
    # — 신규 소비형(golden_censer 등)이 자동 인정돼 거짓 WARN을 안 낸다. candy만 ItemDatabase 밖이라 별도.
    expected_pool = capture_items.union(game_facts.item_ids()).union({"candy"})
    unmapped = reward_items - expected_pool
    judge = "WARN" if unmapped else "PASS"
    signals.append((
        "CashShop rewardItemId ↔ 아이템 풀 매칭",
        "0건 미매칭",
        f"{len(unmapped)}건 ({sorted(unmapped)})" if unmapped else "0건",
        judge
    ))

    # 5. itemId 중복 (CreateCaptureItems와 CashShop shopItems 충돌)
    overlap = capture_items & shop_items
    judge = "FAIL" if overlap else "PASS"
    signals.append((
        "itemId 중복 (CreateCaptureItems ∩ shopItems)",
        "0건 중복",
        f"{len(overlap)}건 ({sorted(overlap)})" if overlap else "0건",
        judge
    ))

    # 6. Gacha 풀 ↔ displayName 매핑
    gacha = extract_gacha_pools()
    pool_ids = set()
    for ids in gacha["pools"].values():
        pool_ids.update(ids)
    name_ids = set(gacha["names"].keys())
    missing_names = pool_ids - name_ids
    orphan_names = name_ids - pool_ids
    judge = "FAIL" if missing_names else "PASS"
    signals.append((
        "Gacha 풀 ID ↔ displayName 매핑 (풀에 있고 이름 없음)",
        "0건",
        f"{len(missing_names)}건 ({sorted(missing_names)})" if missing_names else "0건",
        judge
    ))
    judge = "WARN" if orphan_names else "PASS"
    signals.append((
        "Gacha displayName 고아 (이름 있는데 풀에 없음)",
        "0건",
        f"{len(orphan_names)}건 ({sorted(orphan_names)})" if orphan_names else "0건",
        judge
    ))

    # 7. Gacha 확률 합 100% 검증 — 마지막 임계값과 100 사이 격차가 Legendary 확률(>0)이어야 정상
    probs = extract_gacha_probabilities()
    for box, thresholds in probs.items():
        # 추출 실패 분기는 없다 — extract_gacha_probabilities()가 ExtractorBroken으로 죽는다.
        epic_pct = thresholds[-1] - thresholds[-2]
        legendary_pct = 100 - thresholds[-1]
        is_sorted = thresholds == sorted(thresholds) and len(thresholds) >= 4
        # 서열 규칙: 최고 레어도(전설)가 차상위(에픽)보다 흔하면 안 된다.
        # 봉우리형은 허용한다 — 실버는 Rare 봉우리(L8 ≤ E22)라 정상이다. 하지만 전설이
        # 에픽을 넘으면 등급 서열이 뒤집힌 것이다. 골드 {5,10,23,55}가 L45 > E32로
        # 이 규칙을 어겼는데(전설이 커먼의 9배), 옛 검사는 "단조증가 + L>0"만 봐서
        # 통과시켰다. 45% 오타가 3개월 방치된 이유가 이 구멍이었다.
        legendary_le_epic = legendary_pct <= epic_pct + 1e-6
        ok = legendary_pct > 0 and is_sorted and legendary_le_epic
        note = ""
        if not is_sorted:
            note = " — 임계값 단조증가 아님"
        elif legendary_pct <= 0:
            note = " — Legendary 0%"
        elif not legendary_le_epic:
            note = f" — 전설({legendary_pct:.1f}%) > 에픽({epic_pct:.1f}%) 서열 역전"
        signals.append((
            f"Gacha {box} 등급 분포 (L={legendary_pct:.1f}% E={epic_pct:.1f}%)",
            "단조증가 + Legendary > 0 + 전설 ≤ 에픽",
            f"임계값 {thresholds}{note or ' — 정상'}",
            "PASS" if ok else "FAIL"
        ))

    # 8. CashShop UI 표시 가격 ↔ Manager gemPrice 정합성
    # 가격은 아직 UI 리터럴이라 정본과 갈릴 수 있다. 표시 가격 ≠ 실제 차감액 = 결제 오인.
    ui_prices = game_facts.ui_box_prices()
    mgr_prices = game_facts.box_gem_prices()
    price_mismatches = [
        f"{box}: UI={ui_prices[box]} ≠ Manager={mgr_prices[box]}"
        for box in game_facts.BOXES
        if ui_prices[box] != mgr_prices[box]
    ]
    judge = "FAIL" if price_mismatches else "PASS"
    signals.append((
        "CashShop UI 표시 가격 ↔ Manager gemPrice",
        "0건 불일치",
        f"{len(price_mismatches)}건 ({price_mismatches})" if price_mismatches
        else f"0건 (검사한 박스 {len(game_facts.BOXES)}개)",
        judge
    ))

    # 9. UI 확률 표기가 코드 파생인가 (하드코딩 회귀 감시)
    # 예전엔 "UI 텍스트 % vs 코드 확률" 값 비교였다. 그 검사는 폐물이 됐다 — UI가
    # GetGachaRateText → GachaBoxManager.GetRateText → GetRates → *Thresholds로
    # 파생받게 바뀌었기 때문(CashShopUI 주석: "하드코딩 금지(공시 위반 방지)").
    # 그런데 옛 추출기는 rateText 문자열 리터럴을 찾다가 0개를 반환했고, 그 결과 이 검사와
    # 위 가격 검사가 **둘 다 공허한 PASS**를 찍고 있었다. 값 비교 대신 파생 구조 자체를 지킨다.
    derives = game_facts.ui_derives_gacha_rates()
    hardcoded = game_facts.ui_hardcoded_rate_literals()
    if hardcoded:
        judge, detail = "FAIL", f"확률 리터럴 {len(hardcoded)}건 부활 ({hardcoded})"
    elif not derives:
        judge, detail = "FAIL", "GetGachaRateText → GachaBoxManager 파생 사슬이 끊김"
    else:
        judge, detail = "PASS", "코드 파생 + 리터럴 0건"
    signals.append((
        "CashShop UI 확률 표기 = 코드 파생 (하드코딩 회귀)",
        "파생 유지 + 리터럴 0건",
        detail,
        judge
    ))

    # 10. 가챠 normalPool 티어 = 곤충 DB 실제 rarity
    # GachaBoxManager 주석이 "각 티어의 ID는 InsectDatabase 실제 rarity와 일치해야 함
    # (저가 박스가 상위 곤충을 누출하지 않도록)"이라 못박는데 검사하는 곳이 없었다.
    # `GetDbRarity`는 결과 **표시**만 보정하고 **풀 선택은 바꾸지 않는다** — 배치가 어긋나면
    # 브론즈 상자가 55% 확률로 상위 곤충을 흘리고, 그건 공시한 확률과 실제 분포가 갈리는 것이다.
    # 위 9번이 지키는 건 "표기가 코드에서 파생되는가"이지 "그 코드가 옳은가"가 아니다.
    pool_tiers = extract_gacha_normal_pool()
    known_rarity = extract_insect_rarities()
    tier_mismatch = []
    unknown_ids = []
    pooled_total = 0
    for tier, ids in sorted(pool_tiers.items()):
        for iid in sorted(ids):
            pooled_total += 1
            actual = known_rarity.get(iid)
            if actual is None:
                unknown_ids.append(f"{tier}:{iid}")
            elif actual != tier:
                tier_mismatch.append(f"{iid}(풀={tier} 실제={actual})")

    problems = tier_mismatch + unknown_ids
    signals.append((
        "가챠 normalPool 티어 ↔ 곤충 실제 등급",
        "0건 불일치",
        f"{len(problems)}건 ({problems[:6]})" if problems
        else f"0건 (풀 {pooled_total}종 × 등급 {len(known_rarity)}종 대조)",
        "FAIL" if problems else "PASS"
    ))

    return signals


def render_signals(signals: list) -> str:
    out = ["| 항목 | 임계값 | 측정값 | 판정 |", "|------|--------|--------|------|"]
    for name, threshold, value, judge in signals:
        out.append(f"| {name} | {threshold} | {value} | **{judge}** |")
    fail_n = sum(1 for s in signals if s[3] == "FAIL")
    warn_n = sum(1 for s in signals if s[3] == "WARN")
    pass_n = sum(1 for s in signals if s[3] == "PASS")
    out.append("")
    out.append(f"요약: **{fail_n} FAIL** / {warn_n} WARN / {pass_n} PASS")
    if fail_n > 0:
        out.append("→ FAIL 1건 이상. PASS 보고 금지. 즉시 정정 필요.")
    return "\n".join(out)


def main():
    p = argparse.ArgumentParser(description="데이터 정합성 자동 검증")
    p.add_argument("--detail", action="store_true", help="추출된 데이터셋 상세 출력")
    args = p.parse_args()

    print("# data-lint — 곤충게임 데이터 정합성 검증\n")

    if args.detail:
        print("## 추출된 데이터셋")
        defined = extract_region_ids()
        refs = extract_region_refs()
        shop_items, reward_items = extract_cashshop_items()
        capture_items = extract_capture_items()
        gacha = extract_gacha_pools()
        print(f"- RegionDefinitions.CreateAll() regionId: {sorted(defined)}")
        for src, ids in refs.items():
            print(f"- {src} 참조: {sorted(ids)}")
        print(f"- CashShop itemIds: {sorted(shop_items)}")
        print(f"- CashShop rewardItemIds: {sorted(reward_items)}")
        print(f"- CreateCaptureItems itemIds: {sorted(capture_items)}")
        print(f"- Gacha 풀: {gacha['pools']}")
        print(f"- Gacha displayNames: {sorted(gacha['names'].keys())}")
        print()

    print("## 위험 신호 표")
    signals = evaluate_signals()
    print(render_signals(signals))
    print()

    print("## 가정 / 한계")
    print("- 코드 내 하드코딩된 ID만 검증. ScriptableObject(.asset) 직렬화는 미지원")
    print("- 정규식 기반 추출 — 코드 포맷이 바뀌면 추출기가 깨진다. 단 조용히 깨지진")
    print("  않는다: 기대 심볼을 못 찾으면 ExtractorBroken으로 죽고 exit 2를 낸다")
    print("- InsectDatabase의 insectId는 .asset 파일에 있어 미검증 (후속 작업)")
    print("- Inspector 직렬화 필드(예: ShopUIController.itemIds[])는 grep 미지원")

    fail_count = sum(1 for s in signals if s[3] == "FAIL")
    return 1 if fail_count > 0 else 0


if __name__ == "__main__":
    # 종료 코드: 0=이상 없음, 1=데이터 FAIL(진짜 결함), 2=추출기 고장(검증기 자신의 문제).
    # 1과 2를 가르는 게 핵심이다. 섞으면 "늘 빨간불"이 되어 아무도 안 본다.
    try:
        sys.exit(main())
    except ExtractorBroken as e:
        print(f"\n## 추출기 고장\n\n**{e}**\n")
        print("데이터 결함이 아니라 이 스크립트가 코드를 못 따라간 것이다.")
        print("검증 결과는 신뢰할 수 없다 — 추출기를 먼저 고칠 것.")
        sys.exit(2)
