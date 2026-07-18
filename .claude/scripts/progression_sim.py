"""진행 곡선 시뮬 — 곤충게임의 **이원 레벨 구조**를 모델링한다.

이 게임엔 분리된 두 레벨 시스템이 있다(코드로 확인):
  - 트레이너 레벨(PlayerProgressController): **선형** max(floor, base+(lv-1)*growth).
    배틀/포획/레이드/튜토리얼 EXP가 전부 여기로 간다(GainXp → 트레이너).
  - 곤충 레벨(InsectLevelCurve): **캔디**로만 큰다(TryLevelUpWithCandy). 지수 base*1.14^(lv-1).

옛 progression_sim은 이 둘을 혼동했다. 곤충 XP 곡선(20*1.12^)을 진행 경로로 썼는데,
곤충 XP(GainXp/currentXp)는 코드·UI에 배선만 돼 있고 **어떤 게임플레이도 곤충에 XP를
주지 않는다**(dead 배선). 그 결과 5개 신호가 전부 오탐이었다:
  · 캔디 수입을 배틀 전용·고정 3으로 모델(실제 등급별 2~16.8, 포획·레이드·가챠·튜토리얼)
  · team_size 6(실제 MaxTeamSlots 5)
  · 곤충 지수 XP곡선을 진행 경로로 오인(실제 미사용)
  · 훈련=EXP 소스 오전제(실제 스킬 학습)
  · 튜토리얼 비율 분모=단일 곤충 평생 캔디

이 재설계는 트레이너/곤충을 분리하고, 전투당 보상을 등급별 실제값으로 읽는다.
수치 사본은 두지 않는다 — 전부 game_facts가 코드에서 읽는다.
"""
import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import game_facts  # noqa: E402

if hasattr(sys.stdout, "reconfigure") and sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    sys.stdout.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]

# === 임계값 (밸런스 휴리스틱 — 디자이너 조정 가능) ===
# 근거: 평균 전투 30초 가정 시 4000전투 = 33시간. 곤충 1마리 육성 그라인딩 한계.
# 이 임계값은 **현실 진행**(insect_candy_battles_realistic)에 적용한다 — 곤충 레벨이
# 리전 진행과 동기화되면 비싼 후반 레벨(전체 캔디의 84%가 Lv36+)이 고레어 리전
# income으로 벌린다. Common 고정 상한(insect_candy_battles)은 참고용이며 FAIL 트리거가
# 아니다: 시뮬 자신이 그걸 "최악(순수 Common·배틀만)"이라 명시하면서 그걸로 FAIL을 내면
# 검증기가 자기가 인정한 극단으로 거짓 경보를 울리는 셈이다.
THRESHOLD_BATTLES_FAIL = 4000
# 근거: 트레이너 곡선은 선형이라 후반/초반비가 완만해야 정상. 선형이 5배를 넘으면
# 어딘가 지수가 섞인 것(이탈 구간). 곤충 캔디(지수)는 이 검사 대상이 아니다 — 지수가 설계다.
THRESHOLD_TRAINER_CURVE_WARN = 5.0
# 근거: 팀 전체 동시 Lv50 캔디. MaxTeamSlots 반영. 10만 초과는 캔디 인플레.
THRESHOLD_TEAM_CANDY_FAIL = 100000
# 근거: 튜토리얼 캔디가 초반(Lv1→10) 곤충 캔디 비용의 몇 배인지. 초반 부양 강도.
# 5% 미만이면(엔드게임 대비가 아니라 초반 대비) 신규 인센티브 부족.
THRESHOLD_TUTORIAL_EARLY_WARN = 0.05


def _load_facts():
    # import 시점에 돌아 main()의 예외 처리보다 이르므로 여기서 잡는다.
    try:
        return {
            "mult": game_facts.rarity_multipliers(),
            "tut": game_facts.tutorial_rewards(),
            "raid_mult": game_facts.raid_reward_mult(),
            "team_max": game_facts.team_max_slots(),
            "trainer": game_facts.trainer_xp_curve(),
            "candy_curve": game_facts.insect_candy_curve(),
            "battle": game_facts.battle_rewards_by_rarity(),
            "roster": game_facts.field_roster(),
            "regions": game_facts.region_pools(),
        }
    except game_facts.ExtractorBroken as e:
        print(f"추출기 고장: {e}\n게임 수치를 코드에서 읽지 못했다 — 시뮬을 돌리지 않는다.",
              file=sys.stderr)
        sys.exit(2)


F = _load_facts()
MULT = F["mult"]
RARITIES = ("Common", "Uncommon", "Rare", "Epic", "Legendary")


# ── 곡선 (코드 공식 그대로) ──

def trainer_xp_to_next(level: int) -> int:
    """트레이너 Lv->Lv+1 필요 EXP. 선형: max(floor, base+(lv-1)*growth)."""
    c = F["trainer"]
    return max(c["floor"], c["base"] + (level - 1) * c["growth"])


def insect_candy_cost(level: int) -> int:
    """곤충 Lv->Lv+1 필요 캔디. 지수: base*growth^(lv-1)."""
    c = F["candy_curve"]
    return max(1, round(c["base"] * (c["growth"] ** (level - 1))))


def total_trainer_xp(target: int) -> int:
    return sum(trainer_xp_to_next(L) for L in range(1, target))


def total_insect_candy(target: int) -> int:
    return sum(insect_candy_cost(L) for L in range(1, target))


# ── 전투당 보상 (등급별 실제값 = base * 등급배율) ──

def reward_per_battle(rarity: str) -> dict:
    """적 곤충 1마리 처치/포획 시 실제 EXP·캔디. base(등급별) * 등급배율."""
    b = F["battle"][rarity]
    m = MULT[rarity]
    return {"exp": b["exp"] * m, "candy": b["candy"] * m}


# ── 진행 추정 ──

def trainer_battles(target: int, rarity: str) -> int:
    """트레이너 target 레벨까지 필요한 전투 수(같은 등급 적 기준)."""
    per = reward_per_battle(rarity)["exp"]
    return round(total_trainer_xp(target) / per) if per else 0


def insect_candy_battles(target: int, rarity: str) -> int:
    """곤충 1마리 target 레벨까지 캔디를 배틀+포획으로만 모을 때 전투 수.

    배틀 승리와 포획은 같은 GetCandyReward를 준다(둘 다 처치/포획당 1회). 레이드(×3)·
    가챠·튜토리얼은 보너스라 여기 안 넣는다 — 단일 등급 고정 상한(최악=Common)을 본다.
    """
    per = reward_per_battle(rarity)["candy"]
    return round(total_insect_candy(target) / per) if per else 0


def _candy_of(rarity: str) -> int:
    """적 1마리 처치/포획 캔디(정수) = base(등급별) * 등급배율. GetCandyReward와 동일 반올림."""
    return int(F["battle"][rarity]["candy"] * MULT[rarity])


def region_income_curve() -> list:
    """[(requiredLevel, E[candy]/전투), ...] 오름차순 — 리전별 스폰가중 기대 캔디.

    각 리전 insectIds를 spawnWeight로 가중해(InsectSpawner.GetWeightedRandom 그대로) 등급
    분포를 구하고 전투당 기대 캔디를 낸다. 리전 진행에 따라 조우 등급이 오르므로 income도 오른다.
    """
    roster = F["roster"]          # {id: (rarity, weight)}
    out = []
    for _rid, req, ids in F["regions"]:
        tot = 0.0
        acc = 0.0
        for iid in ids:
            if iid in roster:
                rar, w = roster[iid]
                if w > 0:
                    tot += w
                    acc += w * _candy_of(rar)
        if tot > 0:
            out.append((req, acc / tot))
    out.sort()
    return out


def insect_candy_battles_realistic(target: int) -> int:
    """곤충 레벨이 리전 진행과 동기화된다는 가정의 현실적 전투 수.

    곤충 레벨 L을 올릴 캔디를, 그 시점 플레이어가 있는 리전(requiredLevel<=L 중 최상위)의
    기대 캔디로 나눠 누적한다. 캔디 비용이 지수라 후반 레벨(전체의 84%가 Lv36+)이
    고레어 엔드리전 income으로 벌리므로 Common 고정보다 크게 낮다. 레이드×3·가챠·튜토리얼은
    여전히 별도(추가 하향)라 이 값도 상한 성격이다.
    """
    income = region_income_curve()
    if not income:
        return 0

    def income_at(level: int) -> float:
        cur = income[0][1]
        for req, e in income:
            if level >= req:
                cur = e
        return cur

    battles = 0.0
    for level in range(1, target):
        battles += insect_candy_cost(level) / income_at(level)
    return round(battles)


def trainer_curve_ratio(target: int) -> float:
    """트레이너 후반(Lv35+)/초반(Lv1-20) EXP 평균비. 선형이라 완만해야 정상."""
    early = [trainer_xp_to_next(L) for L in range(1, min(21, target))]
    late = [trainer_xp_to_next(L) for L in range(35, target)]
    if not early or not late:
        return 0.0
    return (sum(late) / len(late)) / (sum(early) / len(early))


def tutorial_early_share(early_level: int = 10) -> float:
    """튜토리얼 캔디 / 곤충 Lv1→early_level 캔디 비용. 초반 부양 강도."""
    early_cost = total_insect_candy(early_level)
    return F["tut"]["candy"] / early_cost if early_cost else 0.0


def render_curve_table(target: int) -> str:
    out = ["| Lv | 트레이너 EXP/Lv (선형) | 곤충 캔디/Lv (지수) | 트레이너 EXP누적 | 곤충 캔디누적 |",
           "|----|----:|----:|----:|----:|"]
    cum_xp = cum_candy = 0
    checkpoints = {1, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50}
    for L in range(1, target + 1):
        if L < target:
            cum_xp += trainer_xp_to_next(L)
            cum_candy += insect_candy_cost(L)
        if L in checkpoints:
            out.append(f"| {L} | {trainer_xp_to_next(L):,} | {insect_candy_cost(L):,} | "
                       f"{cum_xp:,} | {cum_candy:,} |")
    return "\n".join(out)


def evaluate_signals(args) -> list:
    signals = []

    # 1. 곤충 Lv50 캔디 전투 수 — 판정은 현실 진행(리전 동기화), 참고로 Common 고정 상한 병기.
    #    Common 고정(ib_worst)은 시뮬 자신이 "최악"이라 부르는 값이라 FAIL 트리거로 쓰지 않는다.
    ib_real = insect_candy_battles_realistic(args.target_level)
    ib_worst = insect_candy_battles(args.target_level, "Common")
    judge = "FAIL" if ib_real >= THRESHOLD_BATTLES_FAIL else "PASS"
    signals.append((f"곤충 Lv{args.target_level} 캔디 전투 수 (현실 진행·리전 동기화)",
                    f"< {THRESHOLD_BATTLES_FAIL:,}",
                    f"{ib_real:,}회 (최악=Common 고정 {ib_worst:,}; 레이드×{F['raid_mult']:.0f}·가챠·튜토리얼 별도 하향)",
                    judge))

    # 2. 팀 전체 동시 Lv50 캔디 비용 (MaxTeamSlots 반영)
    team_candy = total_insect_candy(args.target_level) * args.team_size
    judge = "FAIL" if team_candy >= THRESHOLD_TEAM_CANDY_FAIL else "PASS"
    signals.append((f"팀 {args.team_size}마리 동시 캔디 비용",
                    f"< {THRESHOLD_TEAM_CANDY_FAIL:,}", f"{team_candy:,}", judge))

    # 3. 트레이너 곡선 형태 — 선형이라 후반/초반비가 완만해야 정상.
    #    곤충 캔디(지수)는 여기 검사 대상이 아니다. 지수 성장은 설계다.
    tcr = trainer_curve_ratio(args.target_level)
    judge = "WARN" if tcr > THRESHOLD_TRAINER_CURVE_WARN else "PASS"
    signals.append(("트레이너 EXP 후반/초반비 (선형 곡선)",
                    f"<= {THRESHOLD_TRAINER_CURVE_WARN:.1f}x",
                    f"{tcr:.1f}x", judge))

    # 4. 튜토리얼 초반 부양 — 엔드게임 단일 곤충 평생 캔디가 아니라 초반(Lv1→10) 대비.
    tes = tutorial_early_share(10)
    judge = "WARN" if tes < THRESHOLD_TUTORIAL_EARLY_WARN else "PASS"
    signals.append(("튜토리얼 캔디 / 곤충 Lv1→10 비용",
                    f">= {THRESHOLD_TUTORIAL_EARLY_WARN*100:.0f}%",
                    f"{tes*100:.0f}%", judge))

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
        out.append("→ FAIL 1건 이상. PASS 보고 금지.")
    return "\n".join(out)


def main():
    p = argparse.ArgumentParser(description="이원 레벨 진행 곡선 시뮬")
    p.add_argument("--target-level", type=int, default=F["candy_curve"]["max"])
    p.add_argument("--rarity", default="Common", choices=list(RARITIES))
    p.add_argument("--avg-battle-sec", type=float, default=30.0)
    p.add_argument("--team-size", type=int, default=F["team_max"],
                   help=f"팀 크기 (기본 = MaxTeamSlots = {F['team_max']})")
    args = p.parse_args()

    print(f"# progression-sim — 이원 레벨 (트레이너 EXP·선형 / 곤충 캔디·지수)\n")
    print("## 코드에서 읽은 구조")
    print(f"- 트레이너 곡선(선형): max({F['trainer']['floor']}, "
          f"{F['trainer']['base']}+(lv-1)*{F['trainer']['growth']})  ← 배틀/포획/레이드/튜토리얼 EXP")
    print(f"- 곤충 캔디 곡선(지수): {F['candy_curve']['base']}*{F['candy_curve']['growth']}^(lv-1)"
          f"  ← 캔디로만 (TryLevelUpWithCandy)")
    print(f"- 곤충 XP 곡선은 미사용(dead 배선) — 진행 경로 아님")
    print(f"- 전투당 보상({args.rarity} 적): EXP {reward_per_battle(args.rarity)['exp']:.1f} / "
          f"캔디 {reward_per_battle(args.rarity)['candy']:.1f}  (base × 등급배율 {MULT[args.rarity]})")
    print(f"- 튜토리얼: 캔디 {F['tut']['candy']} / EXP {F['tut']['exp']}")
    print()

    print("## 누적 곡선")
    print(render_curve_table(args.target_level))
    print()

    print("## 핵심 지표")
    print(f"- 트레이너 Lv{args.target_level} 총 EXP: **{total_trainer_xp(args.target_level):,}** "
          f"→ 전투 **{trainer_battles(args.target_level, args.rarity):,}회** ({args.rarity} 적)")
    print(f"- 곤충 1마리 Lv{args.target_level} 총 캔디: **{total_insect_candy(args.target_level):,}**")
    print(f"    · 현실 진행(리전 동기화): 전투 **{insect_candy_battles_realistic(args.target_level):,}회** ← 판정 대상")
    print(f"    · 최악(Common 고정, 배틀+포획): 전투 **{insect_candy_battles(args.target_level, 'Common'):,}회** (참고 상한)")
    print(f"- 팀 {args.team_size}마리 캔디: **{total_insect_candy(args.target_level)*args.team_size:,}**")
    print()

    print("## 리전별 스폰가중 캔디 income (진행에 따라 상승)")
    print("| requiredLevel | E[candy]/전투 |")
    print("|----:|----:|")
    for req, e in region_income_curve():
        print(f"| {req} | {e:.2f} |")
    print()

    print("## 위험 신호 표")
    print(render_signals(evaluate_signals(args)))
    print()

    print("## 가정 / 한계")
    print("- 판정(신호1)은 **현실 진행**: 곤충 레벨 L의 캔디를 그 시점 리전(requiredLevel<=L 중")
    print("  최상위)의 스폰가중 기대 캔디로 벌어들인다고 본다. 캔디 비용이 지수라 전체의 84%가")
    print("  Lv36+에 몰리고, 그 구간은 고레어 엔드리전(예: 유적 ~7캔디/전투)에서 벌린다.")
    print("- 캔디는 전역 단일 풀(PlayerCandyInventory)이라 종 무관하게 합산된다 — 종별 캔디 아님.")
    print("- 레이드(×3, 예: Epic 30·Legendary 48캔디)·가챠 박스(5~50)·튜토리얼(336)은 별도라")
    print("  현실 전투 수를 더 낮춘다. 현실 수치도 그 의미에서 상한이다.")
    print("- 리전 income은 InsectSpawner.GetWeightedRandom(무아이템)과 동일하게 spawnWeight로")
    print("  가중. 레어스폰 아이템/의상 보너스는 미반영(있으면 고레어↑ → income↑).")
    print("- 곤충별 candyReward는 PlaySceneBootstrap의 등급별 하드코딩을 읽는다"
          "(InsectDatabase .asset의 개체별 편차는 미반영).")

    fail = sum(1 for s in evaluate_signals(args) if s[3] == "FAIL")
    return 1 if fail else 0


if __name__ == "__main__":
    sys.exit(main())
