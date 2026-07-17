"""Lv1->50 진행 곡선 시뮬레이션. 캔디/EXP 누적, 전투 횟수 추정, 비관적 위험 신호 점검.

게임 수치 사본은 두지 않는다 — game_facts가 코드에서 읽는다. 예전엔 튜토리얼 보상을
261/500으로 알고 있었으나 실제는 336/475였다.
"""
import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import game_facts  # noqa: E402

# Windows cp949 환경에서 유니코드 출력 보장
if hasattr(sys.stdout, "reconfigure") and sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    sys.stdout.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]

# === 임계값 (디자이너가 조정 가능) ===
# 근거: 평균 전투 30초 가정 시 4000전투 = 33시간. 이 이상은 그라인딩 한계로 간주.
THRESHOLD_BATTLES_LV50_FAIL = 4000          # 캔디만 경로에서 Lv50 도달 4000+ 전투 = FAIL
# 근거: 후반 곡선이 초반 대비 10배 이상이면 데드존(이탈 구간) — Lv1~20과 Lv35~50 평균 EXP 비교.
THRESHOLD_DEADZONE_RATIO_WARN = 10.0        # Lv35-50 평균 EXP / Lv1-20 평균 EXP > 10x = WARN
# 근거: RARITY_MULT(Common=1.0 vs Legendary=2.8) 자체가 2.8배 격차 — 진행속도 200% 초과 시 P2W 압박.
THRESHOLD_RARITY_GAP_WARN = 2.0             # Common vs Legendary 진행속도 격차 200%+ = WARN
# 근거: 5% 미만은 신규 사용자 진입 인센티브 부족.
# (실측값은 출력 표에서 확인할 것 — 여기 적어두면 또 썩는다)
THRESHOLD_TUTORIAL_RATIO_WARN = 0.05        # 튜토리얼 보상 / 전체 누적 < 5% = WARN
# 근거: 주 5시간 캐주얼 가정 시 80h = 4개월. 이 이상이면 콘텐츠 소진 후 이탈.
THRESHOLD_PLAY_HOURS_WARN = 80.0            # Lv50 도달 추정 플레이 시간 80h+ = WARN
# 근거: 6마리 팀 평균 Lv30 도달 시 캔디 비용 합산 — 100k 초과는 캔디 인플레이션 신호.
THRESHOLD_TEAM_CANDY_FAIL = 100000          # 팀 동시 캔디 비용 100k+ = FAIL

# === 정본 파라미터 (코드와 동기화 필요) ===
# Assets/Scripts/Data/InsectLevelCurve.cs
MAX_LEVEL = 50
BASE_XP = 20
XP_GROWTH = 1.12
BASE_CANDY = 4
CANDY_GROWTH = 1.14

# 정본 = 코드. game_facts가 InsectRewardCalculator.cs / TutorialQuestManager.cs에서 읽는다.
# 튜토리얼 보상은 한때 261/500 사본을 들고 있었으나 실제는 336/475였다 — 퀘스트가
# 추가·조정되는 동안 사본이 안 따라간 드리프트.
def _load_facts():
    # import 시점에 돌아 main()의 예외 처리보다 이르므로 여기서 잡는다.
    try:
        tut = game_facts.tutorial_rewards()
        return (game_facts.rarity_multipliers(), tut["candy"], tut["exp"],
                game_facts.raid_reward_mult())
    except game_facts.ExtractorBroken as e:
        print(f"추출기 고장: {e}\n게임 수치를 코드에서 읽지 못했다 — 시뮬을 돌리지 않는다.",
              file=sys.stderr)
        sys.exit(2)


RARITY_MULT, TUTORIAL_CANDY_TOTAL, TUTORIAL_EXP_TOTAL, RAID_MULT = _load_facts()


def xp_to_next_level(level: int) -> int:
    """Lv->Lv+1 필요 EXP. InsectLevelCurve.cs와 동일 공식."""
    return max(1, int(BASE_XP * (XP_GROWTH ** (level - 1))))


def candy_cost_for_level(level: int) -> int:
    """Lv->Lv+1 필요 캔디. InsectLevelCurve.cs와 동일 공식."""
    return max(1, int(BASE_CANDY * (CANDY_GROWTH ** (level - 1))))


def total_xp_to_level(target: int) -> int:
    return sum(xp_to_next_level(L) for L in range(1, target))


def total_candy_to_level(target: int) -> int:
    return sum(candy_cost_for_level(L) for L in range(1, target))


def battles_required(target_level: int, rarity: str, base_xp_per_kill: float = 8.0) -> int:
    """경험치만 경로에서 Lv50까지 필요한 전투 수."""
    total_xp = total_xp_to_level(target_level)
    avg_xp = base_xp_per_kill * RARITY_MULT.get(rarity, 1.0)
    return int(total_xp / avg_xp + 0.5)


def candy_battles_required(target_level: int, rarity: str, base_candy_per_kill: float = 3.0) -> int:
    """캔디만 경로에서 Lv50까지 필요한 전투 수."""
    total_candy = total_candy_to_level(target_level)
    avg_candy = base_candy_per_kill * RARITY_MULT.get(rarity, 1.0)
    return int(total_candy / avg_candy + 0.5)


def deadzone_ratio(target: int) -> float:
    """후반(Lv35+) EXP 평균 / 초반(Lv1-20) EXP 평균."""
    early = [xp_to_next_level(L) for L in range(1, min(21, target))]
    late = [xp_to_next_level(L) for L in range(35, target)]
    if not early or not late:
        return 0.0
    return (sum(late) / len(late)) / (sum(early) / len(early))


def rarity_gap(target: int, base_xp_per_kill: float) -> float:
    """Common 대비 Legendary 진행 속도 비율 (배수)."""
    common = battles_required(target, "Common", base_xp_per_kill)
    legendary = battles_required(target, "Legendary", base_xp_per_kill)
    if legendary == 0:
        return 0.0
    return common / legendary


def tutorial_share(target: int) -> float:
    """튜토리얼 보상이 전체 누적 보상에서 차지하는 비율 (캔디 기준)."""
    total = total_candy_to_level(target)
    if total == 0:
        return 0.0
    return TUTORIAL_CANDY_TOTAL / total


def render_curve_table(target: int) -> str:
    out = ["| Lv | XP/Lv | Candy/Lv | XP누적 | Candy누적 |",
           "|----|------:|---------:|-------:|----------:|"]
    cum_xp = 0
    cum_candy = 0
    checkpoints = [1, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50]
    for L in range(1, target + 1):
        cum_xp += xp_to_next_level(L) if L < target else 0
        cum_candy += candy_cost_for_level(L) if L < target else 0
        if L in checkpoints:
            out.append(f"| {L} | {xp_to_next_level(L):,} | {candy_cost_for_level(L):,} | {cum_xp:,} | {cum_candy:,} |")
    return "\n".join(out)


def evaluate_signals(args) -> list:
    """위험 신호 표 생성. 각 항목: (이름, 임계값, 측정값, 판정)"""
    signals = []

    # 1. 캔디만 경로 전투 수
    candy_battles = candy_battles_required(args.target_level, args.rarity, args.base_candy)
    judge1 = "FAIL" if candy_battles >= THRESHOLD_BATTLES_LV50_FAIL else "PASS"
    signals.append(("캔디만 경로 전투 수", f"< {THRESHOLD_BATTLES_LV50_FAIL:,}",
                    f"{candy_battles:,}", judge1))

    # 2. 데드존 비율
    dz = deadzone_ratio(args.target_level)
    judge2 = "WARN" if dz > THRESHOLD_DEADZONE_RATIO_WARN else "PASS"
    signals.append(("Lv35-50 / Lv1-20 EXP 평균비", f"<= {THRESHOLD_DEADZONE_RATIO_WARN:.1f}x",
                    f"{dz:.1f}x", judge2))

    # 3. 레어도 격차
    gap = rarity_gap(args.target_level, args.base_xp)
    judge3 = "WARN" if gap >= THRESHOLD_RARITY_GAP_WARN else "PASS"
    signals.append(("Common vs Legendary 진행속도 격차", f"< {THRESHOLD_RARITY_GAP_WARN:.1f}x",
                    f"{gap:.2f}x", judge3))

    # 4. 튜토리얼 비중
    ts = tutorial_share(args.target_level)
    judge4 = "WARN" if ts < THRESHOLD_TUTORIAL_RATIO_WARN else "PASS"
    signals.append(("튜토리얼 보상 / 전체 누적", f">= {THRESHOLD_TUTORIAL_RATIO_WARN*100:.0f}%",
                    f"{ts*100:.1f}%", judge4))

    # 5. 훈련 EXP 기여 (코드 정본: 0)
    signals.append(("훈련 EXP 기여", "> 0", "0 (캔디만 소비)", "WARN"))

    # 6. 추정 플레이 시간 (avg-battle-sec 반영)
    xp_battles = battles_required(args.target_level, args.rarity, args.base_xp)
    est_hours = xp_battles * args.avg_battle_sec / 3600
    judge = "WARN" if est_hours >= THRESHOLD_PLAY_HOURS_WARN else "PASS"
    signals.append((f"추정 플레이 시간 (Lv{args.target_level}, {args.avg_battle_sec}s/전투)",
                    f"< {THRESHOLD_PLAY_HOURS_WARN:.0f}h",
                    f"{est_hours:.1f}h", judge))

    # 7. 팀 동시 캔디 비용 (team-size 반영)
    total_candy = total_candy_to_level(args.target_level)
    team_total = total_candy * args.team_size
    judge = "FAIL" if team_total >= THRESHOLD_TEAM_CANDY_FAIL else "PASS"
    signals.append((f"팀 {args.team_size}마리 동시 캔디 비용",
                    f"< {THRESHOLD_TEAM_CANDY_FAIL:,}",
                    f"{team_total:,}", judge))

    return signals


def render_signals(signals: list) -> str:
    out = ["| 항목 | 임계값 | 측정값 | 판정 |",
           "|------|--------|--------|------|"]
    for name, threshold, value, judge in signals:
        out.append(f"| {name} | {threshold} | {value} | **{judge}** |")
    fail_n = sum(1 for s in signals if s[3] == "FAIL")
    warn_n = sum(1 for s in signals if s[3] == "WARN")
    pass_n = sum(1 for s in signals if s[3] == "PASS")
    out.append("")
    out.append(f"요약: **{fail_n} FAIL** / {warn_n} WARN / {pass_n} PASS")
    if fail_n > 0:
        out.append("→ FAIL 1건 이상. PASS 보고 금지. Phase 4 권장사항 적용 필요.")
    return "\n".join(out)


def main():
    p = argparse.ArgumentParser(description="Lv1->50 진행 곡선 시뮬")
    p.add_argument("--target-level", type=int, default=50, help="목표 곤충 레벨 (기본 50)")
    p.add_argument("--rarity", default="Common", choices=list(RARITY_MULT.keys()))
    p.add_argument("--base-xp", type=float, default=8.0,
                   help="기본 곤충 1마리당 EXP (배율 적용 전, 기본 8)")
    p.add_argument("--base-candy", type=float, default=3.0,
                   help="기본 곤충 1마리당 캔디 (배율 적용 전, 기본 3)")
    p.add_argument("--avg-battle-sec", type=float, default=30.0,
                   help="평균 전투 소요 시간 초 (기본 30)")
    p.add_argument("--team-size", type=int, default=6, help="동시 육성 팀 크기 (기본 6)")
    args = p.parse_args()

    print(f"# progression-sim — Lv1 → Lv{args.target_level} ({args.rarity})\n")
    print("## 입력 파라미터")
    print(f"- target_level: {args.target_level}")
    print(f"- rarity: {args.rarity} (보상 배율 {RARITY_MULT[args.rarity]}x)")
    print(f"- base_xp_per_kill: {args.base_xp} / base_candy_per_kill: {args.base_candy}")
    print(f"- avg_battle_sec: {args.avg_battle_sec}s / team_size: {args.team_size}")
    print()

    print("## 누적 곡선 (체크포인트)")
    print(render_curve_table(args.target_level))
    print()

    total_xp = total_xp_to_level(args.target_level)
    total_candy = total_candy_to_level(args.target_level)
    xp_battles = battles_required(args.target_level, args.rarity, args.base_xp)
    candy_battles = candy_battles_required(args.target_level, args.rarity, args.base_candy)
    est_hours = xp_battles * args.avg_battle_sec / 3600

    print("## 핵심 지표")
    print(f"- Lv{args.target_level} 도달 총 EXP 필요: **{total_xp:,}**")
    print(f"- Lv{args.target_level} 도달 총 캔디 필요: **{total_candy:,}**")
    print(f"- EXP 경로 필요 전투 수: **{xp_battles:,}회**")
    print(f"- 캔디 경로 필요 전투 수: **{candy_battles:,}회**")
    print(f"- 추정 플레이 시간 (EXP 경로): **{est_hours:.1f}h**")
    print(f"- 팀 {args.team_size}마리 동시 캔디 비용: **{total_candy * args.team_size:,}** 캔디")
    print()

    print("## 위험 신호 표")
    signals = evaluate_signals(args)
    print(render_signals(signals))
    print()

    print("## 가정 / 한계")
    print("- IV(개체값) 미적용 — 실제 전투 시간 ±15% 편차")
    print(f"- 평균 전투 시간 {args.avg_battle_sec}s 가정 (사용자 입력 의존)")
    print("- 사용자 행동 균등 가정 (실제는 초반 폭주 후반 정체)")
    print("- 이벤트/시즌 보상 미반영")
    print("- 텔레메트리 수집 후 재검증 필요")

    fail_count = sum(1 for s in signals if s[3] == "FAIL")
    return 1 if fail_count > 0 else 0


if __name__ == "__main__":
    sys.exit(main())
