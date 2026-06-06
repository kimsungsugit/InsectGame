"""데이터 정합성 자동 검증. 코드 내 하드코딩된 ID 정의/참조 비교, 고아/누락/중복 검출."""
import argparse
import os
import re
import sys

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

# === 데이터 소스 경로 ===
PATHS = {
    "bootstrap": "Assets/Scripts/Core/PlaySceneBootstrap.cs",
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

def extract_region_ids(scope: str = "expanded") -> set:
    """CreateRegions() 또는 CreateExpandedRegions() 안의 regionId 추출. 다음 메서드 정의까지."""
    content = _read_clean(PATHS["bootstrap"])
    method_name = "CreateRegions" if scope == "legacy" else "CreateExpandedRegions"
    pattern = re.compile(
        rf"{method_name}\s*\(\)\s*\{{(.*?)(?=\n\s*(?:private|public)\s+(?:static\s+)?[\w\.\[\]<>]+\s+\w+\s*\()",
        re.DOTALL
    )
    m = pattern.search(content)
    if not m:
        return set()
    return set(re.findall(r'regionId\s*=\s*"(\w+)"', m.group(1)))


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


def extract_gacha_probabilities() -> dict:
    """GetBronzeRarity / Silver / Gold 메서드 본체를 다음 메서드 정의 전까지로 분리 추출."""
    content = _read_clean(PATHS["gacha_mgr"])
    results = {}
    for box in ["Bronze", "Silver", "Gold"]:
        # 메서드 시그니처부터 다음 메서드 정의 전까지 본체 추출
        pattern = re.compile(
            rf'Get{box}Rarity\s*\([^)]*\)\s*\{{(.*?)(?=\n\s*(?:private|public)\s+(?:static\s+)?[\w\.\[\]<>]+\s+\w+\s*\()',
            re.DOTALL
        )
        m = pattern.search(content)
        if not m:
            results[box] = None
            continue
        # 첫 return InsectRarity.Legendary까지의 임계값만
        body = m.group(1)
        legendary_idx = body.find("return InsectRarity.Legendary")
        if legendary_idx >= 0:
            body = body[:legendary_idx]
        thresholds = [float(x) for x in re.findall(r'roll\s*<\s*([\d.]+)f', body)]
        results[box] = thresholds
    return results


# ===== 평가 =====

def evaluate_signals() -> list:
    signals = []

    # 1. 리전 정의 vs 참조 정합성
    expanded = extract_region_ids("expanded")
    legacy = extract_region_ids("legacy")
    refs = extract_region_refs()
    all_refs = set()
    for v in refs.values():
        all_refs.update(v)

    missing = all_refs - expanded
    orphan = expanded - all_refs
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

    # 2. 리전 중복 정의 (Legacy vs Expanded)
    duplicates = expanded & legacy
    judge = "WARN" if duplicates else "PASS"
    signals.append((
        "리전 중복 정의 (CreateRegions vs CreateExpandedRegions)",
        "0건 중복",
        f"{len(duplicates)}건 중복 ({sorted(duplicates)})" if duplicates else "0건",
        judge
    ))

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
    # rewardItemId 중 캐시샵 아이템(gem_*, shop_*, box_*) 외의 ID는 실제 아이템 풀에 있어야
    expected_pool = capture_items.union({"candy", "exp_boost"})  # 알려진 외부 아이템
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
        if thresholds is None:
            signals.append((f"Gacha {box} 확률 합", "100%", "추출 실패", "WARN"))
            continue
        last = thresholds[-1] if thresholds else 0
        legendary_pct = 100 - last
        # Legendary 확률은 양수이고 정렬되어 있어야 정상
        is_sorted = thresholds == sorted(thresholds) and len(thresholds) >= 4
        judge = "PASS" if (legendary_pct > 0 and is_sorted) else "FAIL"
        signals.append((
            f"Gacha {box} 누적 확률 분포 (Legendary={legendary_pct:.2f}%)",
            "임계값 단조증가 + Legendary > 0",
            f"임계값 {thresholds}, Legendary {legendary_pct:.2f}%",
            judge
        ))

    # 8. CashShop UI 박스 가격 ↔ Manager gemPrice 정합성 (UI 하드코딩 검출)
    ui_boxes = extract_cashshop_ui_boxes()
    mgr_prices = extract_cashshop_manager_box_prices()
    price_mismatches = []
    for box_id, ui in ui_boxes.items():
        mgr = mgr_prices.get(box_id)
        if mgr is None:
            price_mismatches.append(f"{box_id}: UI={ui['price']} / Manager 미정의")
        elif mgr != ui["price"]:
            price_mismatches.append(f"{box_id}: UI={ui['price']} ≠ Manager={mgr}")
    judge = "FAIL" if price_mismatches else "PASS"
    signals.append((
        "CashShop UI 박스 가격 ↔ Manager gemPrice",
        "0건 불일치",
        f"{len(price_mismatches)}건 ({price_mismatches})" if price_mismatches else "0건",
        judge
    ))

    # 9. CashShop UI 박스 확률 텍스트 ↔ GachaBoxManager 임계값 정합성
    box_to_box_name = {"box_bronze": "Bronze", "box_silver": "Silver", "box_gold": "Gold"}
    grades = ["C", "U", "R", "E", "L"]
    prob_mismatches = []
    for box_id, ui in ui_boxes.items():
        box_name = box_to_box_name.get(box_id)
        if box_name is None:
            continue
        thresholds = probs.get(box_name)
        if thresholds is None or len(thresholds) < 4:
            continue
        # 누적 임계값 → 단계별 확률
        code_rates = {}
        prev = 0.0
        for grade, t in zip(grades[:4], thresholds):
            code_rates[grade] = t - prev
            prev = t
        code_rates["L"] = 100.0 - prev
        for grade in grades:
            ui_v = ui["rates"].get(grade)
            code_v = code_rates.get(grade)
            if ui_v is None or code_v is None:
                continue
            if abs(ui_v - code_v) > THRESHOLD_GACHA_PROB_TOLERANCE:
                prob_mismatches.append(f"{box_id}.{grade}: UI={ui_v} ≠ Code={code_v:.2f}")
    judge = "FAIL" if prob_mismatches else "PASS"
    signals.append((
        "CashShop UI 박스 확률 텍스트 ↔ GachaBoxManager 임계값",
        f"0건 불일치 (tolerance ±{THRESHOLD_GACHA_PROB_TOLERANCE})",
        f"{len(prob_mismatches)}건 ({prob_mismatches})" if prob_mismatches else "0건",
        judge
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
        expanded = extract_region_ids("expanded")
        legacy = extract_region_ids("legacy")
        refs = extract_region_refs()
        shop_items, reward_items = extract_cashshop_items()
        capture_items = extract_capture_items()
        gacha = extract_gacha_pools()
        print(f"- CreateExpandedRegions regionId: {sorted(expanded)}")
        print(f"- CreateRegions regionId (legacy): {sorted(legacy)}")
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
    print("- 정규식 기반 추출 — 코드 포맷 변경 시 false negative 가능")
    print("- InsectDatabase의 insectId는 .asset 파일에 있어 미검증 (후속 작업)")
    print("- Inspector 직렬화 필드(예: ShopUIController.itemIds[])는 grep 미지원")

    fail_count = sum(1 for s in signals if s[3] == "FAIL")
    return 1 if fail_count > 0 else 0


if __name__ == "__main__":
    sys.exit(main())
