"""가챠 박스 몬테카를로 시뮬 + UI-코드 정합성 자동 검증.

수치 사본은 두지 않는다 — 전부 game_facts가 코드에서 읽는다. 예전엔 BOX_DEFS가 확률·
가격 사본을 들었고 실버/골드가 드리프트해 존재하지 않는 게임을 시뮬레이션했다
(골드 Legendary 사본 5% vs 실제 45%).
"""
import argparse
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import game_facts  # noqa: E402

if hasattr(sys.stdout, "reconfigure") and sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    sys.stdout.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]

# === 임계값 ===
# 근거: 100연차에 Legendary 0개 확률 50%+ = 천장 없는 가챠 = 사용자 보호 부재. 산업 관행 50~100연 천장.
THRESHOLD_NO_PITY_LEGENDARY_FAIL = 0.50          # 100연차 Legendary 0개 확률 50%+ = FAIL
# 근거: UI 텍스트와 코드 확률 분리는 UX 신뢰 위반. 사용자에게 표시된 값과 실 확률 불일치 = 환불 사유.
THRESHOLD_UI_CODE_MISMATCH_FAIL = True            # UI 텍스트 ≠ 코드 = FAIL
# 근거: UI 표기 가격과 Manager 정본 가격이 갈리면 결제 표시 오인 — 환불 사유.
THRESHOLD_PRICE_MISMATCH_FAIL = True               # CashShopUI 가격 ≠ Manager 가격 = FAIL
# 의미: 차상위 박스의 EV 증가율이 가격 증가율 × 이 비율 미만이면 가성비 함정 WARN.
#       예) 가격 +60%일 때 EV 증가율이 +36%(60×0.6) 미만이면 WARN.
THRESHOLD_VALUE_TRAP_RATIO_WARN = 0.6             # 가격 증가율 대비 EV 증가율 최소 비율
# 근거: 100연차에 평균 15회 이상 중복 = 신규 곤충 획득 효율 너무 낮음. 풀 크기 확대 또는 천장 신설 필요.
THRESHOLD_EXCLUSIVE_DUP_WARN = 15                 # 100연차 평균 중복 15+ = WARN
# 근거: 필드 샤이니 1% vs 가챠 0%는 가챠 매력 저하. 가챠 자체 보너스 디자인 부재 신호.
THRESHOLD_SHINY_GACHA_GAP_WARN = True             # 가챠 샤이니 0% vs 필드 1% = WARN

# === 정본 = 코드. 사본 없음 ===
# 전부 game_facts가 GachaBoxManager.cs / CashShopManager.cs / InsectRewardCalculator.cs /
# InsectEntity.cs에서 직접 읽는다. 추출이 실패하면 낡은 값으로 조용히 시뮬을 돌리는 대신
# 여기서 exit 2로 죽는다. 이 로딩은 import 시점에 돌아서 main()의 try보다 이르므로
# 트레이스백이 새어나가지 않게 여기서 잡는다.
def _load_facts():
    try:
        pcts = game_facts.gacha_rarity_pcts()
        prices = game_facts.box_gem_prices()
        excl = game_facts.gacha_exclusive_chances()
        candy = game_facts.gacha_candy_bonus()
        return (
            {
                box: {
                    "price_manager": prices[box],
                    "rarity_pcts": pcts[box],
                    "exclusive_chance": excl[box],
                    "candy_bonus_min": candy[box][0],
                    "candy_bonus_max": candy[box][1],
                }
                for box in game_facts.BOXES
            },
            game_facts.gacha_exclusive_pool_sizes(),
            game_facts.rarity_multipliers(),
            game_facts.field_shiny_pct(),
            0.0 if not game_facts.gacha_has_shiny() else float("nan"),
        )
    except game_facts.ExtractorBroken as e:
        print(f"추출기 고장: {e}\n", file=sys.stderr)
        print("게임 수치를 코드에서 읽지 못했다. 시뮬 결과는 무의미하므로 실행하지 않는다.",
              file=sys.stderr)
        sys.exit(2)


# 가챠 EV 가중치 = 게임 내 실제 보상 배율 — 박스 1회 가치를 보상 가치 기준으로 환산.
BOX_DEFS, EXCLUSIVE_POOL_SIZE, RARITY_MULT, FIELD_SHINY_PCT, GACHA_SHINY_PCT = _load_facts()

# Lv30 캔디 비용 (progression_sim과 일치)
LV30_CANDY_COST = int(4 * (1.14 ** 29))


def draw_rarity(box: str, rng: random.Random) -> str:
    """확률 분포에 따라 레어도 1개 추첨."""
    r = rng.uniform(0, 100)
    cum = 0.0
    for rarity in ["Common", "Uncommon", "Rare", "Epic", "Legendary"]:
        cum += BOX_DEFS[box]["rarity_pcts"][rarity]
        if r < cum:
            return rarity
    return "Legendary"


def simulate_pulls(box: str, pulls: int, rng: random.Random) -> dict:
    """1회 시도(pulls회 가챠)의 결과."""
    counts = {"Common": 0, "Uncommon": 0, "Rare": 0, "Epic": 0, "Legendary": 0}
    candy = 0
    for _ in range(pulls):
        rarity = draw_rarity(box, rng)
        counts[rarity] += 1
        candy += rng.randint(BOX_DEFS[box]["candy_bonus_min"],
                              BOX_DEFS[box]["candy_bonus_max"])
    return {"counts": counts, "candy": candy}


def monte_carlo(box: str, pulls: int, trials: int, seed: int) -> dict:
    """몬테카를로 trials회 반복. 5%/50%/95% 분위 + 평균."""
    rng = random.Random(seed)
    legendary_results = []
    epic_results = []
    candy_results = []
    for _ in range(trials):
        r = simulate_pulls(box, pulls, rng)
        legendary_results.append(r["counts"]["Legendary"])
        epic_results.append(r["counts"]["Epic"])
        candy_results.append(r["candy"])

    def stats(arr):
        s = sorted(arr)
        n = len(s)
        return {
            "min": s[0],
            "p5": s[n // 20],
            "p50": s[n // 2],
            "mean": sum(s) / n,
            "p95": s[int(n * 0.95)],
            "max": s[-1],
            "zero": sum(1 for x in arr if x == 0) / n,
        }

    return {
        "legendary": stats(legendary_results),
        "epic": stats(epic_results),
        "candy": stats(candy_results),
    }


def expected_value(box: str) -> float:
    """RARITY_MULT(보상 배율 정본)로 박스 1회 기댓값 산출."""
    pcts = BOX_DEFS[box]["rarity_pcts"]
    return sum(RARITY_MULT[r] * pcts[r] / 100.0 for r in RARITY_MULT)


def render_box_comparison() -> str:
    """박스 간 가격당 EV 비교 표. 차상위 박스의 효율 증가가 가격 증가에 부합하는지 가시화."""
    out = ["| 박스 | 가격(젬) | Legendary % | EV (가중) | 가격당 EV(×1000) | bronze 대비 효율 |",
           "|------|---------:|------------:|----------:|----------------:|----------------:|"]
    bronze_ev_per_gem = expected_value("bronze") / BOX_DEFS["bronze"]["price_manager"]
    for box, defs in BOX_DEFS.items():
        ev = expected_value(box)
        ev_per_gem = ev / defs["price_manager"]
        ratio = ev_per_gem / bronze_ev_per_gem
        out.append(f"| {box} | {defs['price_manager']} | {defs['rarity_pcts']['Legendary']}% | {ev:.2f} | {ev_per_gem*1000:.3f} | {ratio:.2f}x |")
    return "\n".join(out)


def epic_exclusive_duplicates(box: str, pulls: int, trials: int, seed: int) -> float:
    """Epic 전용곤충 풀에서 중복 등장 평균. (Epic 추첨 횟수 - Epic 풀 크기)의 평균."""
    rng = random.Random(seed + 1)   # 메인 시뮬과 다른 시드
    pool_size = EXCLUSIVE_POOL_SIZE["Epic"]
    exclusive_chance = BOX_DEFS[box]["exclusive_chance"]
    duplicates = []
    for _ in range(trials):
        epic_exclusive_count = 0
        for _ in range(pulls):
            rarity = draw_rarity(box, rng)
            if rarity == "Epic" and rng.random() < exclusive_chance:
                epic_exclusive_count += 1
        # pool_size 이상 뽑으면 그 만큼 중복 발생 (균등 분포 가정 단순화)
        dup = max(0, epic_exclusive_count - pool_size)
        duplicates.append(dup)
    return sum(duplicates) / len(duplicates)


def prob_zero_legendary_analytic(box: str, pulls: int) -> float:
    """해석적 P(Legendary 0개 in N연차) = (1-p)^N."""
    p = BOX_DEFS[box]["rarity_pcts"]["Legendary"] / 100.0
    return (1 - p) ** pulls


# === UI-코드 정합성 ===
# 추출은 전부 game_facts가 소유한다. 한때 data_lint의 extract_cashshop_ui_boxes를 빌려
# 썼는데, 그 추출기는 rateText 문자열 리터럴을 찾다가 0개를 반환하고 있었다 — UI가
# 확률을 코드 파생으로 바꾼 뒤로. 그 결과 이쪽은 "UI에서 추출 실패" FAIL을 찍고
# data_lint 쪽은 공허한 PASS를 찍었다. 같은 고장이 두 얼굴로 나타난 셈이다.

def check_price_mismatch() -> list:
    """UI 표시 가격 vs 정본(Manager) 가격 비교. 표시 ≠ 차감 = 결제 오인."""
    ui_prices = game_facts.ui_box_prices()
    return [
        (box, ui_prices[box], BOX_DEFS[box]["price_manager"])
        for box in game_facts.BOXES
        if ui_prices[box] != BOX_DEFS[box]["price_manager"]
    ]


def evaluate_signals(args) -> list:
    signals = []

    # 1. 천장 부재 — Legendary 0개 확률
    p0 = prob_zero_legendary_analytic(args.box, args.pulls)
    judge = "FAIL" if p0 >= THRESHOLD_NO_PITY_LEGENDARY_FAIL else "PASS"
    signals.append((f"천장 부재: {args.box} {args.pulls}연차 Legendary 0개",
                    f"< {THRESHOLD_NO_PITY_LEGENDARY_FAIL*100:.0f}%",
                    f"{p0*100:.1f}%", judge))

    # 2. 가격 불일치 (UI 표시 vs Manager 정본)
    price_mm = check_price_mismatch()
    judge = "FAIL" if price_mm else "PASS"
    details = (", ".join(f"{b}: UI={u} 정본={m}" for b, u, m in price_mm) if price_mm
               else f"0건 (검사한 박스 {len(game_facts.BOXES)}개)")
    signals.append(("CashShopUI 표시 가격 vs Manager 정본", "0건 불일치", details, judge))

    # 3. UI 확률 표기가 코드 파생인가 (하드코딩 회귀 감시)
    # 값 비교였다가 폐물이 됐다 — UI가 GetGachaRateText → GachaBoxManager.GetRateText →
    # GetRates → *Thresholds로 파생받게 바뀌었다. 남은 위험은 하드코딩 부활뿐이다.
    hardcoded = game_facts.ui_hardcoded_rate_literals()
    if hardcoded:
        judge, details = "FAIL", f"확률 리터럴 {len(hardcoded)}건 부활 ({hardcoded})"
    elif not game_facts.ui_derives_gacha_rates():
        judge, details = "FAIL", "GetGachaRateText → GachaBoxManager 파생 사슬이 끊김"
    else:
        judge, details = "PASS", "코드 파생 + 리터럴 0건"
    signals.append(("UI 확률 표기 = 코드 파생 (하드코딩 회귀)", "파생 유지 + 리터럴 0건",
                    details, judge))

    # 4. 실버 가성비
    bronze_ev = expected_value("bronze")
    silver_ev = expected_value("silver")
    ev_gain = (silver_ev - bronze_ev) / bronze_ev
    price_gain = (BOX_DEFS["silver"]["price_manager"] - BOX_DEFS["bronze"]["price_manager"]) / BOX_DEFS["bronze"]["price_manager"]
    judge = "WARN" if ev_gain < price_gain * THRESHOLD_VALUE_TRAP_RATIO_WARN else "PASS"
    signals.append(("실버 가성비 (브론즈 대비)",
                    f"기댓값 +{price_gain*THRESHOLD_VALUE_TRAP_RATIO_WARN*100:.0f}%+",
                    f"가격 +{price_gain*100:.0f}% / 기댓값 +{ev_gain*100:.0f}%", judge))

    # 4-b. 골드 가성비 (실버 대비) — 차상위 박스가 효율적인지
    silver_ev_b = expected_value("silver")
    gold_ev = expected_value("gold")
    ev_gain_g = (gold_ev - silver_ev_b) / silver_ev_b if silver_ev_b > 0 else 0
    price_gain_g = (BOX_DEFS["gold"]["price_manager"] - BOX_DEFS["silver"]["price_manager"]) / BOX_DEFS["silver"]["price_manager"]
    judge = "WARN" if ev_gain_g < price_gain_g * THRESHOLD_VALUE_TRAP_RATIO_WARN else "PASS"
    signals.append(("골드 가성비 (실버 대비)",
                    f"기댓값 +{price_gain_g*THRESHOLD_VALUE_TRAP_RATIO_WARN*100:.0f}%+",
                    f"가격 +{price_gain_g*100:.0f}% / 기댓값 +{ev_gain_g*100:.0f}%", judge))

    # 4-c. 박스 가성비 역전 — 차상위 박스의 가격당 EV가 차하위보다 낮으면 가성비 함정 신호
    ev_per_gem = {b: expected_value(b) / BOX_DEFS[b]["price_manager"] for b in BOX_DEFS}
    inversions = []
    if ev_per_gem["silver"] < ev_per_gem["bronze"]:
        inversions.append(f"silver({ev_per_gem['silver']*1000:.3f}) < bronze({ev_per_gem['bronze']*1000:.3f})")
    if ev_per_gem["gold"] < ev_per_gem["silver"]:
        inversions.append(f"gold({ev_per_gem['gold']*1000:.3f}) < silver({ev_per_gem['silver']*1000:.3f})")
    if ev_per_gem["gold"] < ev_per_gem["bronze"]:
        inversions.append(f"gold({ev_per_gem['gold']*1000:.3f}) < bronze({ev_per_gem['bronze']*1000:.3f})")
    judge = "WARN" if inversions else "PASS"
    signals.append(("박스 가성비 역전 (×1000 단위)",
                    "역전 0건",
                    "; ".join(inversions) if inversions else "0건",
                    judge))

    # 5. 골드 캔디보너스 가치
    avg_candy = (BOX_DEFS["gold"]["candy_bonus_min"] + BOX_DEFS["gold"]["candy_bonus_max"]) / 2
    candy_ratio = avg_candy / LV30_CANDY_COST
    judge = "WARN" if candy_ratio < 0.5 else "PASS"
    signals.append(("골드 캔디보너스 / Lv30 레벨업비",
                    ">= 50%",
                    f"{candy_ratio*100:.1f}% ({avg_candy:.0f}/{LV30_CANDY_COST})", judge))

    # 6. Epic 전용곤충 중복 (풀이 작아 중복 누적)
    avg_dup = epic_exclusive_duplicates(args.box, args.pulls, min(args.trials, 5000), args.seed)
    judge = "WARN" if avg_dup >= THRESHOLD_EXCLUSIVE_DUP_WARN else "PASS"
    signals.append((f"Epic 전용곤충 {args.pulls}연차 평균 중복 (풀 {EXCLUSIVE_POOL_SIZE['Epic']}종)",
                    f"< {THRESHOLD_EXCLUSIVE_DUP_WARN}",
                    f"{avg_dup:.1f}", judge))

    # 7. 가챠 샤이니
    judge = "WARN" if GACHA_SHINY_PCT < FIELD_SHINY_PCT else "PASS"
    signals.append(("가챠 샤이니 적용",
                    f"{FIELD_SHINY_PCT}%",
                    f"{GACHA_SHINY_PCT}% (코드 미구현)", judge))

    return signals


def render_distribution(mc: dict) -> str:
    out = ["| 항목 | min | p5 | p50 | mean | p95 | max | 0개 비율 |",
           "|------|----:|---:|----:|-----:|----:|----:|---------:|"]
    for key in ["legendary", "epic", "candy"]:
        s = mc[key]
        out.append(f"| {key.title()} | {s['min']} | {s['p5']} | {s['p50']} | {s['mean']:.1f} | {s['p95']} | {s['max']} | {s['zero']*100:.1f}% |")
    return "\n".join(out)


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
        out.append("→ FAIL 1건 이상. PASS 보고 금지.")
    return "\n".join(out)


def main():
    p = argparse.ArgumentParser(description="가챠 박스 몬테카를로 + UI-코드 정합성")
    p.add_argument("--box", default="bronze", choices=["bronze", "silver", "gold", "all"])
    p.add_argument("--pulls", type=int, default=100)
    p.add_argument("--trials", type=int, default=10000)
    p.add_argument("--seed", type=int, default=42)
    args = p.parse_args()

    boxes = ["bronze", "silver", "gold"] if args.box == "all" else [args.box]

    print(f"# gacha-sim — 몬테카를로 {args.trials:,}회, {args.pulls}연차/시도\n")

    print("## 박스 정본 — GachaBoxManager.cs / CashShopManager.cs에서 실시간 추출 (사본 없음)")
    for b, d in BOX_DEFS.items():
        pcts = d["rarity_pcts"]
        print(f"- {b} ({d['price_manager']}젬): C{pcts['Common']}/U{pcts['Uncommon']}/R{pcts['Rare']}/E{pcts['Epic']}/L{pcts['Legendary']}, 픽업{int(d['exclusive_chance']*100)}%")
    print()

    print("## 박스 간 가성비 비교 (가격당 가중 기댓값)")
    print(render_box_comparison())
    print()

    for box in boxes:
        args.box = box
        print(f"## {box} 박스 분포 ({args.pulls}연차 × {args.trials:,}회 시도)")
        mc = monte_carlo(box, args.pulls, args.trials, args.seed)
        print(render_distribution(mc))
        print(f"- 해석적 P(Legendary 0): {prob_zero_legendary_analytic(box, args.pulls)*100:.2f}%")
        print(f"- 박스 1회 기댓값(가중): {expected_value(box):.2f}")
        print()

    # 신호는 box=bronze 기준 1회만 (가격/확률 정합성은 박스 무관 전체)
    args.box = boxes[0]
    print("## 위험 신호 표")
    signals = evaluate_signals(args)
    print(render_signals(signals))
    print()

    print("## 가정 / 한계")
    print("- 천장(pity) 미구현 가정 — 실제 코드와 일치")
    print("- 곤충 풀 균등 무작위 가정 (가중치 데이터 미확인)")
    print("- IV 미적용 — 곤충별 스탯 편차 무시")
    print(f"- random.seed={args.seed} (--seed로 변경 가능)")

    fail_count = sum(1 for s in signals if s[3] == "FAIL")
    return 1 if fail_count > 0 else 0


if __name__ == "__main__":
    sys.exit(main())
