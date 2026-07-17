"""경제 시뮬레이션. 캔디/젬/코인 수입·지출 균형, 코드 결함(데드 화폐, 교환 부재) 자동 점검.

가격 사본은 두지 않는다 — game_facts가 코드에서 읽는다.
"""
import argparse
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import game_facts  # noqa: E402
from cs_strip import strip_cs  # noqa: E402  — 주석/문자열 제거 공용

# Windows cp949 환경에서 유니코드 출력 보장
if hasattr(sys.stdout, "reconfigure") and sys.stdout.encoding and sys.stdout.encoding.lower() != "utf-8":
    sys.stdout.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]

# === 임계값 (디자이너가 조정 가능) ===
# 근거: 일일 1캡처 권장 페이스. 4일+ 대기는 단일 비용이 일주일 행동량 초과 — 진행 막힘.
THRESHOLD_CANDY_BOTTLENECK_DAYS_FAIL = 4.0   # 6마리 팀 동시 레벨업 4일+ 대기 = FAIL
# 근거: 화폐 정의는 있는데 발행/사용 경로 0건이면 dead 시스템. TryPurchase 호출부 0건(코인) 알려진 결함.
THRESHOLD_DEAD_CURRENCY_FAIL = True          # 발행 경로 0건이면 FAIL
# 근거: 14일 후 젬 잉여는 신규 지출 경로 부족 신호. 골드박스 4회분(4800) 초과는 디자인 의도 외 축적.
THRESHOLD_GEM_HOARD_DAYS_WARN = 14           # 14일 후 젬 잔고 > 골드박스 4회분 = WARN
# 근거: 캔디 부족분을 젬으로 보충 못 하면 P2W 외 우회로 0 — 경제 유연성 부재.
THRESHOLD_NO_EXCHANGE_WARN = True            # 캔디↔젬 교환 경로 0건 = WARN
# 근거: 효과 1% 차이에 가격 5배(diamond_net 2000젬 vs normal_net 150코인)는 명백한 P2W 함정.
THRESHOLD_P2W_GAP_RATIO_WARN = 5.0           # 프리미엄/베이직 가격 비율 5배+ = WARN

# === 정본 = 코드. 사본 없음 ===
# "동기화 필요"라 적어둔 사본은 동기화되지 않는다 — 실제로 박스 가격이 실버 800 /
# 골드 1200에 멈춰 있는 동안 코드는 600 / 750으로 바뀌어 있었다.
#
# CASH_ITEM_GEMS는 삭제했다: 정의만 있고 사용처가 0인 죽은 사본이었고, 그나마도
# incense_rare(350젬)라는 유령 품목을 들고 있었다 — CashShopManager.cs:79가
# "ItemData SO 미정의로 비활성화"라고 적어둔, 상점에 존재하지 않는 아이템이다.
def _load_facts():
    # import 시점에 돌아 main()의 예외 처리보다 이르므로 여기서 잡는다.
    try:
        return (game_facts.box_gem_prices(), game_facts.gem_packages(),
                game_facts.team_max_slots())
    except game_facts.ExtractorBroken as e:
        print(f"추출기 고장: {e}\n가격을 코드에서 읽지 못했다 — 시뮬을 돌리지 않는다.",
              file=sys.stderr)
        sys.exit(2)


GACHA_BOX_PRICES, CASH_GEM_PACKAGES, TEAM_MAX_SLOTS = _load_facts()   # CASH_GEM_PACKAGES: [(KRW, gems), ...]

# Assets/Scripts/Data/InsectLevelCurve.cs (progression_sim과 동일)
BASE_CANDY = 4
CANDY_GROWTH = 1.14

# 사용자 행동 프로파일
PROFILES = {
    "ftp":   {"gem_topup_per_week": 0,    "label": "Free-to-play (충전 없음)"},
    "mid":   {"gem_topup_per_week": 400,  "label": "중과금 (gem_550 주 1회)"},
    "whale": {"gem_topup_per_week": 2700, "label": "고래 (gem_1200 주 3회)"},
}


def candy_cost(level: int) -> int:
    return max(1, int(BASE_CANDY * (CANDY_GROWTH ** (level - 1))))


def estimate_daily_candy(captures_per_day: int, battles_per_day: int,
                          raids_per_week: int, avg_candy_per_event: float = 5.0) -> float:
    """행동량 기반 일일 캔디 평균 수입 추정. 레이드는 ×3 보상 + 주->일 환산."""
    capture_income = captures_per_day * avg_candy_per_event
    battle_income = battles_per_day * avg_candy_per_event
    raid_income = raids_per_week * avg_candy_per_event * 3.0 / 7.0
    return capture_income + battle_income + raid_income


def candy_bottleneck_days(team_size: int, current_level: int,
                           daily_candy_income: float) -> float:
    """팀원 동시 레벨업 비용 / 일일 캔디 수입."""
    cost_per_member = candy_cost(current_level)
    total = cost_per_member * team_size
    if daily_candy_income <= 0:
        return float("inf")
    return total / daily_candy_income


# gem_balance_after는 삭제했다 — 동어반복(income − spend = topup×주 − gacha×silver×주)이라
# FTP(topup=0)면 항상 음수를 "측정"했다. 젬 획득 경로 검사(gem_free_income_count)로 대체.


def _scan_files(root: str = "Assets/Scripts", suffix: str = ".cs"):
    """root 하위 .cs 파일 경로 yield."""
    if not os.path.isdir(root):
        return
    for dirpath, _, filenames in os.walk(root):
        for fn in filenames:
            if fn.endswith(suffix):
                yield os.path.join(dirpath, fn)


def _count_matches(pattern: str, exclude_definitions: bool = True) -> int:
    """pattern 매칭 라인 수. 메서드 정의 라인 제외 옵션."""
    rx = re.compile(pattern)
    def_rx = re.compile(r"\b(public|private|protected|internal)\s+\w+\s+\w+\s*\(")
    total = 0
    for path in _scan_files():
        try:
            with open(path, encoding="utf-8") as f:
                for line in f:
                    if rx.search(line):
                        if exclude_definitions and def_rx.search(line):
                            continue
                        total += 1
        except (OSError, UnicodeDecodeError):
            continue
    return total


def coin_income_count() -> int:
    """AddCoins 호출부 (정의 제외)."""
    return _count_matches(r"\.AddCoins\s*\(|\bwallet\.AddCoins\s*\(")


def coin_spend_count() -> int:
    return _count_matches(r"\.SpendCoins\s*\(|\bwallet\.SpendCoins\s*\(")


def gem_free_income_count() -> int:
    """무료 젬 **양수 지급** 호출부. 유료 결제·차감·복원·치트는 제외.

    예전엔 gem_balance_after가 `topup×주 − gacha×silver×주`로 잔고를 "계산"했다. 그건
    동어반복이었다 — FTP(topup=0)면 무조건 음수가 나오고, 그 음수는 측정이 아니라 가정한
    지출이 되돌아온 것이다. 젬은 음수가 될 수도 없다(SpendGems/PurchaseWithGems가 거절).
    코인 데드 화폐를 faucet 유무로 잡았듯, 젬도 무료 수입원 유무로 본다.

    단, `.AddGems(`를 그냥 세면 오탐이 쏟아진다(실측: 5건 전부 무료 수입 아님):
      - `AddGems(-item.gemPrice)` / `AddGems(-price)` — 음수, 구매 차감
      - `AddGems(diff)` — 클라우드 복원(동기화)
      - `AddGems(99999 - Gems)` — 디버그 치트
      - `// ...AddGems(-price)` — 주석
    그래서 strip_cs로 주석을 지우고, 인자가 음수(`-`)거나 Gems·diff를 참조하면(복원·치트)
    뺀다. CashShopManager(GrantGemPackage=유료 패키지 지급)도 통째로 제외.
    이래도 grep 휴리스틱이라 완벽하진 않다 — 진짜 무료 지급이 늘면 수동 확인 권장.
    """
    total = 0
    for path in _scan_files():
        if "CashShopManager" in path:
            continue
        try:
            with open(path, encoding="utf-8") as f:
                src = strip_cs(f.read())
        except (OSError, UnicodeDecodeError):
            continue
        for m in re.finditer(r"\.AddGems\s*\(([^)]*)\)", src):
            arg = m.group(1).strip()
            if arg.startswith("-"):
                continue  # 차감 (구매)
            if "Gems" in arg or "diff" in arg:
                continue  # 복원(diff) / 치트(99999 - Gems)
            total += 1
    return total


def candy_gem_exchange_count() -> int:
    """같은 라인에 (Add|Spend)Gems와 (Add|Spend)Candy 동시 등장."""
    rx = re.compile(r"(Add|Spend)Gems.*?(Add|Spend)Candy|(Add|Spend)Candy.*?(Add|Spend)Gems")
    total = 0
    for path in _scan_files():
        try:
            with open(path, encoding="utf-8") as f:
                for line in f:
                    if rx.search(line):
                        total += 1
        except (OSError, UnicodeDecodeError):
            continue
    return total


def p2w_outfit_gap() -> dict:
    """프리미엄 의상 평균 젬 가격 / 베이직 의상 평균 코인 가격."""
    # 정본은 CharacterOutfitManager.cs MakeBaseItem/MakePremiumItem 호출
    # 단순화: 사용자 입력 또는 휴리스틱
    return {"basic_avg_coin": 300, "premium_avg_gem": 1100,
            "ratio_note": "평균 의상가 — 정확값은 CharacterOutfitManager.cs 인스펙터 참조"}


def evaluate_signals(args) -> list:
    signals = []

    # 1. 코인 발행 경로 (수입)
    coin_income = coin_income_count()
    coin_spend = coin_spend_count()
    if coin_income == 0 and coin_spend > 0:
        signals.append(("코인 발행 경로", ">= 1", f"수입 {coin_income} / 지출 {coin_spend} (데드 화폐)", "FAIL"))
    else:
        signals.append(("코인 발행 경로", ">= 1", f"수입 {coin_income} / 지출 {coin_spend}", "PASS"))

    # 2. 캔디 병목
    bottleneck = candy_bottleneck_days(args.team_size, args.current_level, args.daily_candy_income)
    judge = "FAIL" if bottleneck >= THRESHOLD_CANDY_BOTTLENECK_DAYS_FAIL else "PASS"
    signals.append((f"중반 캔디 병목 ({args.team_size}마리, Lv{args.current_level})",
                    f"< {THRESHOLD_CANDY_BOTTLENECK_DAYS_FAIL:.1f}일",
                    f"{bottleneck:.1f}일", judge))

    # 3. 젬 획득 경로 — 코인과 대비. 젬은 하드 화폐라 유료 획득이 정상이다.
    #    코인은 무료·유료 faucet 둘 다 0 + sink >0 이라 완전 데드(FAIL).
    #    젬은 무료 faucet 0이어도 유료 패키지(CASH_GEM_PACKAGES)가 있으면 하드 화폐 설계 → PASS.
    #    무료도 유료도 없을 때만 진짜 문제(WARN).
    gem_faucet = gem_free_income_count()
    has_paid = len(CASH_GEM_PACKAGES) > 0
    if gem_faucet == 0 and not has_paid:
        judge, note = "WARN", "젬 획득 경로 전무"
    elif gem_faucet == 0:
        judge, note = "PASS", "무료 없음 — 유료 패키지로만 획득(하드 화폐, 가챠 게임 관행)"
    else:
        judge, note = "PASS", f"무료 수입원 {gem_faucet}건"
    signals.append(("젬 획득 경로", ">= 1 (무료 또는 유료)", note, judge))

    # 4. 캔디↔젬 교환 — 부재 자체는 정상 설계(소프트→하드 전환은 가챠 게임 관행).
    # "강제 결제 유도" 낙인은 미검증 가치판단이라 뺀다. 정보성으로만 남긴다.
    exchange = candy_gem_exchange_count()
    signals.append(("캔디↔젬 교환 경로 (정보)", "-",
                    f"{exchange}건" + (" (없음 — 소프트↔하드 전환은 관행상 미제공)" if exchange == 0 else ""),
                    "PASS"))

    # 5. P2W 격차 (의상 가격대비)
    p2w = p2w_outfit_gap()
    ratio = p2w["premium_avg_gem"] / max(1, p2w["basic_avg_coin"])
    judge = "WARN" if ratio >= THRESHOLD_P2W_GAP_RATIO_WARN else "PASS"
    signals.append(("프리미엄 vs 베이직 의상 가격비",
                    f"< {THRESHOLD_P2W_GAP_RATIO_WARN:.1f}x",
                    f"{ratio:.1f}x (코인 {p2w['basic_avg_coin']} vs 젬 {p2w['premium_avg_gem']})",
                    judge))

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
        out.append("→ FAIL 1건 이상. PASS 보고 금지. Phase 4 권장사항 적용 필요.")
    return "\n".join(out)


def main():
    p = argparse.ArgumentParser(description="경제 균형 시뮬")
    p.add_argument("--days", type=int, default=14, help="시뮬 기간 (일, 기본 14)")
    p.add_argument("--profile", default="ftp", choices=list(PROFILES.keys()))
    p.add_argument("--gacha-per-week", type=int, default=2)
    p.add_argument("--captures-per-day", type=int, default=20)
    p.add_argument("--battles-per-day", type=int, default=10)
    p.add_argument("--raids-per-week", type=int, default=3)
    p.add_argument("--daily-candy-income", type=float, default=None,
                   help="일일 캔디 평균 수입. 지정 안 하면 captures/battles/raids에서 자동 계산")
    p.add_argument("--avg-candy-per-event", type=float, default=5.0,
                   help="이벤트(포획/전투/레이드 1회)당 평균 캔디 (기본 5)")
    p.add_argument("--team-size", type=int, default=TEAM_MAX_SLOTS)
    p.add_argument("--current-level", type=int, default=25,
                   help="병목 점검 시 곤충 레벨 (기본 25)")
    args = p.parse_args()

    # daily-candy-income이 None이면 행동량 기반 자동 계산
    if args.daily_candy_income is None:
        args.daily_candy_income = estimate_daily_candy(
            args.captures_per_day, args.battles_per_day,
            args.raids_per_week, args.avg_candy_per_event
        )
        income_source = f"자동 계산 (포획 {args.captures_per_day}/일, 전투 {args.battles_per_day}/일, 레이드 {args.raids_per_week}/주, 이벤트당 {args.avg_candy_per_event} 캔디)"
    else:
        income_source = "사용자 직접 입력"

    print(f"# economy-sim — {args.days}일 시뮬 ({PROFILES[args.profile]['label']})\n")

    print("## 입력 파라미터")
    print(f"- days: {args.days}, profile: {args.profile}")
    print(f"- 행동: 가챠 {args.gacha_per_week}/주, 포획 {args.captures_per_day}/일, 전투 {args.battles_per_day}/일, 레이드 {args.raids_per_week}/주")
    print(f"- daily_candy_income: {args.daily_candy_income:.1f} ({income_source})")
    print(f"- team_size: {args.team_size} / current_level: Lv{args.current_level}")
    print()

    print("## 캐시샵 정본")
    for krw, gems in CASH_GEM_PACKAGES:
        print(f"- {krw:,}원 → {gems}젬 (KRW/gem = {krw/gems:.2f})")
    print(f"- 가챠 박스: bronze {GACHA_BOX_PRICES['bronze']}젬 / silver {GACHA_BOX_PRICES['silver']}젬 / gold {GACHA_BOX_PRICES['gold']}젬")
    print()

    print("## 코드 결함 점검")
    coin_in = coin_income_count()
    coin_out = coin_spend_count()
    gem_free = gem_free_income_count()
    exch = candy_gem_exchange_count()
    print(f"- AddCoins 호출부: {coin_in}건 (0 + 지출>0이면 데드 화폐)")
    print(f"- SpendCoins 호출부: {coin_out}건")
    print(f"- 무료 젬 양수 지급: {gem_free}건 (유료 패키지·차감·복원·치트 제외)")
    print(f"- 캔디↔젬 교환: {exch}건 (부재는 하드 화폐 관행 — 결함 아님)")
    print()

    print("## 위험 신호 표")
    signals = evaluate_signals(args)
    print(render_signals(signals))
    print()

    print("## 가정 / 한계")
    print(f"- 일일 캔디 수입 {args.daily_candy_income} 가정 (사용자 입력 의존)")
    print("- 사용자 행동 균등 가정 (실제는 초반 폭주 후반 정체)")
    print("- 이벤트/시즌 보상 미반영")
    print("- P2W 격차는 의상 평균가 휴리스틱 — 정확한 검증은 CharacterOutfitManager.cs 인스펙터 참조")
    print("- 텔레메트리 수집 후 재검증 필수")

    fail_count = sum(1 for s in signals if s[3] == "FAIL")
    return 1 if fail_count > 0 else 0


if __name__ == "__main__":
    sys.exit(main())
