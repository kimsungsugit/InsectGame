"""진행 곡선 시뮬 — 곤충게임의 **이원 레벨 구조**를 모델링한다.

이 게임엔 분리된 두 레벨 시스템이 있다(코드로 확인):
  - 트레이너 레벨(PlayerProgressController): **선형** max(floor, base+(lv-1)*growth).
    배틀/포획/레이드/튜토리얼 EXP가 전부 여기로 간다(GainXp → 트레이너).
  - 곤충 레벨: **캔디**로만 큰다(TryLevelUpWithCandy → GetCandyCostForLevel). 곡선은 game_facts가
    실제 배선을 보고 고른다 — 지금은 InsectLevelCurve SO가 배선되지 않아 폴백 선형식(GameConstants.Leveling)이
    정본이다. 2026-09-30까지 이 시뮬은 죽은 지수식(4×1.125^)으로 판정했다(Lv50 한 레벨 실제 102 ↔ 시뮬 1,284).

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
# 근거: 캐릭터 1레벨에 드는 조우 수(같은 레벨 사냥)의 후반/초반비. 요구량이 선형으로 늘어도 EXP가 곤충
# 레벨에 비례하면 상쇄된다. 4배를 넘으면 후반 캐릭터 레벨이 리전을 못 따라가 포획 레벨 제한에 걸린다.
THRESHOLD_TRAINER_PACE_WARN = 4.0
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
            "gap": game_facts.trainer_level_gap(),
            "candy_curve": game_facts.insect_candy_curve(),
            "stat_cost": game_facts.training_stat_cost(),
            "battle": game_facts.battle_rewards_by_rarity(),
            "roster": game_facts.field_roster(),
            "regions": game_facts.region_pools(),
            "shares": game_facts.field_rarity_shares(),
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
    """곤충 Lv->Lv+1 필요 캔디 — 실제로 빠지는 식(game_facts.candy_cost_at)."""
    return game_facts.candy_cost_at(F["candy_curve"], level)


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


def exp_multiplier(trainer: int, insect: int) -> float:
    """TrainerLevelGap.ExpMultiplier 그대로 — 곤충 레벨 배율 × 캐릭터와의 레벨 차 배율."""
    g = F["gap"]
    gap = max(1, insect) - max(1, trainer)
    level = 1 + (max(1, insect) - 1) * g["exp_per_level"]
    if gap >= 0:
        diff = 1 + min(gap, g["higher_cap"]) * g["higher_step"]
    else:
        diff = max(g["lower_min"], 1 + gap * g["lower_step"])
    return level * diff


def exp_reward(rarity: str, insect: int, trainer: int) -> int:
    """InsectRewardCalculator.GetExpReward(data, insectLevel, trainerLevel) — 실제 지급 EXP(부스터 제외)."""
    return max(0, round(reward_per_battle(rarity)["exp"] * exp_multiplier(trainer, insect)))


def expected_exp_same_level(level: int) -> float:
    """같은 레벨 곤충 1마리의 기대 EXP — 필드 전역 등급표 분포."""
    tot = sum(F["shares"].values())
    return sum(F["shares"][r] / tot * exp_reward(r, level, level) for r in RARITIES)


def encounters_per_trainer_level(level: int, scaled: bool = True) -> float:
    """같은 레벨 곤충만 상대할 때 캐릭터 1레벨에 드는 조우 수. scaled=False면 옛 식(등급만)."""
    if scaled:
        per = expected_exp_same_level(level)
    else:
        tot = sum(F["shares"].values())
        per = sum(F["shares"][r] / tot * reward_per_battle(r)["exp"] for r in RARITIES)
    return trainer_xp_to_next(level) / per if per else 0.0


# ── 진행 추정 ──

def trainer_battles(target: int, rarity: str) -> int:
    """트레이너 target 레벨까지 필요한 전투 수(같은 등급·**같은 레벨** 적 기준 — EXP가 곤충 레벨에 비례한다)."""
    total = 0.0
    for level in range(1, target):
        per = exp_reward(rarity, level, level)
        if per <= 0:
            return 0
        total += trainer_xp_to_next(level) / per
    return round(total)


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


def region_rarity_shares(ids) -> dict:
    """리전 풀 하나의 실제 등급 분포 — 전역 등급표(FieldSpawnRules)를 풀에 있는 등급으로 대체해 합친 것.

    필드 스폰은 등급을 먼저 전역 표로 굴리고 종은 그 등급 안에서 리전 풀로 고른다(2026-09-29). 그래서
    풀이 다섯 등급을 다 가지면 모든 리전이 같은 분포이고, 빠진 등급만 가까운 아래 → 위로 옮겨 간다.
    풀 구성은 코드(RegionDefinitions)에서 읽으므로 풀이 바뀌면 여기도 따라간다.
    """
    roster = F["roster"]          # {id: (rarity, weight)}
    available = {roster[i][0] for i in ids if i in roster and roster[i][1] > 0}
    total = sum(F["shares"].values())
    out = {r: 0.0 for r in RARITIES}
    for r in RARITIES:
        to = game_facts.field_rarity_fallback(r, available)
        if to is not None:
            out[to] += F["shares"][r] / total
    return out


def region_income_curve() -> list:
    """[(requiredLevel, E[candy]/전투), ...] 오름차순 — 리전별 기대 캔디.

    리전 등급 분포(region_rarity_shares — 전역 등급표 + 대체)로 전투당 기대 캔디를 낸다. 예전엔 풀의
    spawnWeight 가중이 곧 등급 분포라 뒤 리전일수록 희귀가 흔해 income이 올랐다(유적 ~7캔디). 지금은
    희귀도가 리전과 무관해 풀이 다섯 등급을 다 갖는 한 모든 리전의 income이 같다 — 진행이 올리는 건 레벨뿐이다.
    """
    out = []
    for _rid, req, ids in F["regions"]:
        shares = region_rarity_shares(ids)
        if sum(shares.values()) > 0:
            out.append((req, sum(p * _candy_of(r) for r, p in shares.items())))
    out.sort()
    return out


def insect_candy_battles_realistic(target: int) -> int:
    """곤충 레벨이 리전 진행과 동기화된다는 가정의 현실적 전투 수.

    곤충 레벨 L을 올릴 캔디를, 그 시점 플레이어가 있는 리전(requiredLevel<=L 중 최상위)의
    기대 캔디로 나눠 누적한다. 레이드×3·가챠·튜토리얼은 여전히 별도(추가 하향)라 이 값도 상한 성격이다.
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
    out = ["| Lv | 트레이너 EXP/Lv (선형) | 곤충 캔디/Lv | 트레이너 EXP누적 | 곤충 캔디누적 |",
           "|----|----:|----:|----:|----:|"]
    cum_xp = cum_candy = 0
    checkpoints = {1, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 60, 70, 80}
    for L in range(1, target + 1):
        if L < target:
            cum_xp += trainer_xp_to_next(L)
            cum_candy += insect_candy_cost(L)
        if L in checkpoints:
            out.append(f"| {L} | {trainer_xp_to_next(L):,} | {insect_candy_cost(L):,} | "
                       f"{cum_xp:,} | {cum_candy:,} |")
    return "\n".join(out)


def stat_training_cost(iv: int, rarity: str) -> int:
    """능력치(개체값) iv → +1 캔디. TrainingPricing.StatCost와 같은 식(계수는 GameConstants.Training)."""
    c = F["stat_cost"]
    if iv >= c["max_iv"]:
        return 0
    return max(1, round(c["base"] * (c["growth"] ** iv) * MULT[rarity]))


def render_trainer_exp_section() -> str:
    """캐릭터 EXP — 곤충 레벨·레벨 차 배율(TrainerLevelGap). 참고 표 + 신호 5의 근거."""
    g = F["gap"]
    lines = ["## 캐릭터 EXP — 곤충 레벨 × 레벨 차 (TrainerLevelGap)",
             f"- 레벨 배율 1 + (곤충Lv−1)×{g['exp_per_level']} · 차 배율: 높으면 +{g['higher_step']:.0%}/Lv"
             f"(최대 {1 + g['higher_cap'] * g['higher_step']:.1f}배), 낮으면 −{g['lower_step']:.0%}/Lv"
             f"(최저 {g['lower_min']}배)",
             f"- 포획 제한: 레벨 차 +{g['grace']} 초과 1Lv마다 최종 확률 −{g['capture_drop']:.0%}"
             f"(최저 ×{g['capture_min']})",
             "| 같은 레벨 | 일반 EXP | 전설 EXP | 캐릭터 1Lv당 조우(현행) | (등급만 보던 옛 식) |",
             "|----|----:|----:|----:|----:|"]
    for lv in (5, 20, 40, 60, 75):
        lines.append(f"| Lv{lv} | {exp_reward('Common', lv, lv)} | {exp_reward('Legendary', lv, lv)} | "
                     f"{encounters_per_trainer_level(lv):.1f} | {encounters_per_trainer_level(lv, scaled=False):.1f} |")
    lines.append("")
    lines.append("| 캐릭터 Lv30 · 곤충 Lv | " + " | ".join(str(L) for L in (15, 20, 25, 30, 35, 40, 45)) + " |")
    lines.append("|----|" + "----:|" * 7)
    for r in ("Common", "Rare", "Legendary"):
        lines.append(f"| {r} EXP | " + " | ".join(str(exp_reward(r, L, 30)) for L in (15, 20, 25, 30, 35, 40, 45)) + " |")
    return "\n".join(lines)


def render_training_section() -> str:
    """훈련소 「성장 훈련」의 능력치 비용 — 참고 지표(판정 없음). 기준 개체는 등급별 평균 롤이다
    (RollIV power 2/2.5/3/4/5 → 평균 개체값 약 5/4/4/3/3). 목표는 S급 경계(합 41 = 14/14/13)."""
    income = region_income_curve()
    per_battle = income[0][1] if income else 3.0
    avg_iv = {"Common": 5, "Uncommon": 4, "Rare": 4, "Epic": 3, "Legendary": 3}
    lines = ["## 성장 훈련 — 능력치(개체값) 비용 (참고)",
             f"- 식: {F['stat_cost']['base']} × {F['stat_cost']['growth']}^현재값 × 등급배율 (TrainingPricing.StatCost)",
             f"- 비교 기준: 곤충 Lv1→50 레벨업 총 {total_insect_candy(50):,}캔디",
             "| 등급 | 평균 개체 → S급(14/14/13) | 전투 환산 | 한 능력치 0→15 |",
             "|----|----:|----:|----:|"]
    for r in RARITIES:
        start = avg_iv[r]
        to_s = sum(stat_training_cost(v, r) for tgt in (14, 14, 13) for v in range(start, tgt))
        full = sum(stat_training_cost(v, r) for v in range(0, F["stat_cost"]["max_iv"]))
        lines.append(f"| {r} | {to_s:,} | {to_s / per_battle:,.0f}회 | {full:,} |")
    return "\n".join(lines)


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
    #    곤충 캔디 곡선은 여기 검사 대상이 아니다(형태가 아니라 총량을 신호 1·2가 본다).
    tcr = trainer_curve_ratio(args.target_level)
    judge = "WARN" if tcr > THRESHOLD_TRAINER_CURVE_WARN else "PASS"
    signals.append(("트레이너 EXP 후반/초반비 (선형 곡선)",
                    f"<= {THRESHOLD_TRAINER_CURVE_WARN:.1f}x",
                    f"{tcr:.1f}x", judge))

    # 5. 캐릭터 레벨당 조우 수가 후반에 불어나지 않는가 — EXP가 등급만 볼 때(2026-10-01 전)는
    #    Lv5 13회 → Lv60 107회(8.5배)였다. 레벨 비례 EXP면 선형 요구량과 상쇄돼 완만해야 정상.
    early, late = encounters_per_trainer_level(5), encounters_per_trainer_level(60)
    ratio = late / early if early else 0.0
    judge = "WARN" if ratio > THRESHOLD_TRAINER_PACE_WARN else "PASS"
    signals.append(("캐릭터 레벨당 조우 수 Lv60/Lv5비 (같은 레벨 사냥)",
                    f"<= {THRESHOLD_TRAINER_PACE_WARN:.1f}x",
                    f"{ratio:.1f}x ({early:.0f}회 → {late:.0f}회)", judge))

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

    print(f"# progression-sim — 이원 레벨 (트레이너 EXP / 곤충 캔디)\n")
    print("## 코드에서 읽은 구조")
    print(f"- 트레이너 곡선(선형): max({F['trainer']['floor']}, "
          f"{F['trainer']['base']}+(lv-1)*{F['trainer']['growth']})  ← 배틀/포획/레이드/튜토리얼 EXP")
    print(f"- 곤충 캔디 곡선: {game_facts.curve_label(F['candy_curve'])} (최대 Lv{F['candy_curve']['max']})"
          f"  ← 캔디로만 (TryLevelUpWithCandy, 실제 배선 기준)")
    print(f"- 곤충 XP 곡선은 미사용(dead 배선) — 진행 경로 아님")
    print(f"- 전투당 보상({args.rarity} 적): EXP {reward_per_battle(args.rarity)['exp']:.1f} / "
          f"캔디 {reward_per_battle(args.rarity)['candy']:.1f}  (base × 등급배율 {MULT[args.rarity]}, "
          f"EXP는 Lv1 기준 — 레벨 배율은 「캐릭터 EXP」 절)")
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
    print(render_trainer_exp_section())
    print()
    print(render_training_section())
    print()

    print("## 리전별 캔디 income (전역 등급표 + 빠진 등급 대체)")
    shares = F["shares"]
    tot = sum(shares.values())
    print("- 전역 등급표(FieldSpawnRules): " + " / ".join(f"{r} {shares[r] / tot * 100:.1f}%" for r in RARITIES))
    print("| 리전 | requiredLevel | 풀 등급 분포(C/U/R/E/L %) | E[candy]/전투 |")
    print("|----|----:|----|----:|")
    for rid, req, ids in sorted(F["regions"], key=lambda t: t[1]):
        s = region_rarity_shares(ids)
        dist = "/".join(f"{s[r] * 100:.1f}" for r in RARITIES)
        e = sum(p * _candy_of(r) for r, p in s.items())
        print(f"| {rid} | {req} | {dist} | {e:.2f} |")
    print()

    print("## 위험 신호 표")
    print(render_signals(evaluate_signals(args)))
    print()

    print("## 가정 / 한계")
    print("- 판정(신호1)은 **현실 진행**: 곤충 레벨 L의 캔디를 그 시점 리전(requiredLevel<=L 중")
    print("  최상위)의 기대 캔디로 벌어들인다고 본다. 희귀도가 리전과 무관해져(전역 등급표) 풀이 다섯")
    print("  등급을 다 갖는 리전은 income이 같다 — 후반 리전이 고레어로 캔디를 더 주던 가속은 없다.")
    print("- 캔디는 전역 단일 풀(PlayerCandyInventory)이라 종 무관하게 합산된다 — 종별 캔디 아님.")
    print("- 레이드(×3, 예: Epic 30·Legendary 48캔디)·가챠 박스(5~50)·튜토리얼(336)은 별도라")
    print("  현실 전투 수를 더 낮춘다. 현실 수치도 그 의미에서 상한이다.")
    print("- 리전 income은 FieldSpawnRules 전역 등급표(무아이템)에 풀에 없는 등급의 대체(아래 → 위)를")
    print("  얹은 분포. 레어스폰 아이템/의상 보너스는 미반영(있으면 희귀 이상↑ → income↑).")
    print("- 곤충별 candyReward는 PlaySceneBootstrap의 등급별 하드코딩을 읽는다"
          "(InsectDatabase .asset의 개체별 편차는 미반영).")

    fail = sum(1 for s in evaluate_signals(args) if s[3] == "FAIL")
    return 1 if fail else 0


if __name__ == "__main__":
    sys.exit(main())
