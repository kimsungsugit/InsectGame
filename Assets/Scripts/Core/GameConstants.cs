namespace InsectGame.Core
{
    /// <summary>
    /// 게임 전역 상수 중앙 관리.
    /// 기존 코드에 흩어진 매직넘버를 여기에 모읍니다.
    /// </summary>
    public static class GameConstants
    {
        // ── 씬 이름 ──
        public static class Scenes
        {
            public const string Play = "PlayScene";
            public const string MainMenu = "MainMenu";
            public const string Opening = "OpeningScene";
        }

        // ── 저장 파일명 ──
        public static class SaveFiles
        {
            public const string PlayerProgress = "player_progress.json";
            public const string PlayerInsects = "player_insects.json";
            public const string PlayerCandies = "player_candies.json";
            public const string PlayerCurrency = "player_currency.json";
            public const string PlayerItems = "player_items.json";
            public const string BattleTeam = "battle_team.json";
            public const string DexSave = "dex_save.json";
            public const string StoryProgress = "story_progress.json";
            // 나의 섬 — 보관함·배치·방목·누적 수확. 클라우드는 GameSaveData.islandData 블롭.
            public const string Island = "island.json";
        }

        // ── PlayerPrefs 키 ──
        public static class PrefsKeys
        {
            public const string CaptureTriggerMode = "InsectGame.CaptureTriggerMode";
            public const string DexSortMode = "InsectGame.DexSortMode";
            public const string DexFilterMode = "InsectGame.DexFilterMode";
            public const string MasterVolume = "InsectGame.MasterVolume";
            public const string SfxVolume = "InsectGame.SfxVolume";
            public const string GraphicsQuality = "InsectGame.GraphicsQuality";
            public const string QuestProgress = "InsectGame.QuestProgress";
            public const string QuestCompleted = "InsectGame.QuestCompleted";
            public const string ActiveQuest = "InsectGame.ActiveQuest";
            // 완료됐지만 아직 퀘스트 창에서 확인 안 한 퀘스트 id 목록 — 퀵바 배지 카운터용.
            public const string QuestUnseen = "InsectGame.QuestUnseen";
            // 서브 퀘스트 진행/반복횟수 — 클라우드 동기(CloudSaveManager DTO questSideProgress/questSideRepeat).
            public const string QuestSideProgress = "InsectGame.QuestSideProgress";
            public const string QuestSideRepeat = "InsectGame.QuestSideRepeat";
            public const string TutorialHidden = "InsectGame.TutorialHidden";
            public const string LastSubAreaId = "InsectGame.SubArea.LastEntered";
            // 주간 크기 대결 보상 수령 상태 "주차:등급". 주차가 바뀌면 값이 안 맞아 자동 미수령.
            // 기록 자체는 저장하지 않는다 — player_insects.json의 capturedUnix로 파생한다.
            public const string WeeklyContestClaimed = "InsectGame.WeeklyContest.Claimed";
            // 정화한 명부회 오염 거점의 리전 ID CSV — 클라우드 동기(DTO blightCleansed).
            // 간부 격파 기록(DefeatedLedgerBosses)에서 파생하지 않는 이유는 RegionBlightManager 참조.
            public const string BlightCleansed = "InsectGame.BlightCleansed";
            // 수문장 배지 이정표 보상 수령 상태("4,8") — 클라우드 동기(DTO badgeMilestonesClaimed).
            // 배지 자체는 저장하지 않는다(DefeatedGuardians에서 파생) — GuardianBadges 참조.
            public const string BadgeMilestonesClaimed = "InsectGame.BadgeMilestonesClaimed";
            // 따라가는 마을 이야기(주민 storyNpcId) — **로컬 전용**(QuestUnseen과 같은 편의 상태).
            // 기기마다 달라도 진행에 영향이 없고, 가리키던 이야기가 끝나면 트래커가 스스로 지운다.
            public const string TrackedTale = "InsectGame.TrackedTale";
            // 마지막으로 쓴 채집망 itemId — **로컬 전용** 편의 상태. 포획 선택 창이 이걸로 바로 미니게임을 연다.
            // 그 채집망이 떨어졌으면 가진 것 중 맨 앞(가장 흔한 것)으로 물러난다.
            public const string LastCaptureNet = "InsectGame.LastCaptureNet";
            // 라온과의 포획 내기 상태("active"/"done") — **로컬 전용**. 한 계정에 한 번뿐인 짧은 내기라
            // 점수는 저장하지 않는다(도중에 닫으면 0:0에서 다시 붙는다).
            public const string RivalRace = "InsectGame.RivalRace";
            // 첫 색다른 조우를 이미 줬는가("1") — **로컬 전용**. 한 번만 일어나야 하는 연출이다.
            public const string FirstShinyGiven = "InsectGame.FirstShinyGiven";
        }

        // ── 플레이어 ──
        public static class Player
        {
            public const int MaxEquipSlots = 4;
            // 습득 풀은 learnset 전체(최대 6: Epic+ jab/boost/trait/burst/storm/signature)를 담는다. 옛 4는 자동학습이
            // 초반 4개(jab/boost/trait/burst)로 차서 storm(L17)·signature가 영구 미습득 → 자연 성장으로 최강기 도달 불가였다.
            // 전투 장착 슬롯(MaxEquipSlots)은 4 유지 — 풀에서 4개를 골라 장착(플레이어 선택). 세이브 호환(리스트, 상한만 상승).
            public const int MaxLearnedSkills = 6;
            public const int MaxIV = 15;
            public const float AutoUnfreezeTime = 20f;
        }

        // ── 레벨링 ──
        // 이 Fallback 3종이 **실제로 도는 경로**다 — InsectLevelCurve(.asset)가 프로젝트에 없어
        // PlayerInsectCollection의 curve가 늘 null이고, SO의 지수 곡선은 미사용이다.
        // 캔디 비용은 선형(4 + 2×(L-1))이라 Lv.80까지 누적 약 6,600으로 완만하다.
        public static class Leveling
        {
            // 2막(ver2) 6지역이 Lv.42~70 구간을 쓴다. PlayerProgressController.maxLevel과 같은 값.
            public const int FallbackMaxLevel = 80;
            public const int FallbackBaseCandyCost = 4;
            public const int FallbackCandyCostGrowth = 2;
        }

        // ── 캐릭터(트레이너) 레벨 차 ──
        // 공식은 TrainerLevelGap이 단일 출처이고 여기는 그 계수다. 실측 근거는 rules/balance.md
        // 「포획 기준점」·「캐릭터 EXP 기준점」. 레벨 차는 늘 <c>곤충 레벨 − 캐릭터 레벨</c>이다(양수 = 곤충이 높다).
        public static class TrainerLevel
        {
            /// <summary>
            /// 포획 제한이 시작되는 레벨 차. 이 차까지는 포획 공식의 덧셈 보정(-3%/Lv, ±5 clamp)만 걸린다.
            /// 그 상한(<c>MaximumLevelDelta</c>)과 같은 값이라야 +5 너머가 평평하지 않다 — 예전엔
            /// +5와 +20이 같은 확률이라 20레벨 높은 전설도 퍼펙트 미니게임이면 34%로 잡혔다.
            /// </summary>
            public const int CaptureGraceLevels = 5;

            /// <summary>유예를 넘긴 1레벨마다 최종 포획 확률에서 깎는 비율 — +10이면 ×0.5, +14면 ×0.1.</summary>
            public const float CaptureDropPerLevel = 0.10f;

            /// <summary>포획 배율의 하한 — +15 이상은 아이템·미니게임 보너스를 다 얹어도 2~3%다.</summary>
            public const float MinCaptureMultiplier = 0.05f;

            /// <summary>
            /// 곤충 레벨 1당 EXP 증가 — <c>1 + (Lv − 1) × 이 값</c>(Lv21 ×2, Lv61 ×4). 캐릭터 레벨업 필요량이
            /// 선형(50 + 15×(Lv−1))이라 같은 레벨 사냥의 레벨당 조우 수가 후반까지 20~27회로 거의 일정하다.
            /// 이게 없던 때는 등급만 봐서 Lv60 일반 곤충도 Lv1과 같은 5였고, 레벨당 조우가 Lv5 13회 → Lv60 107회로 불었다.
            /// </summary>
            public const float ExpPerInsectLevel = 0.05f;

            /// <summary>곤충이 캐릭터보다 높을 때 1레벨당 EXP 가산 — 최대 <see cref="ExpHigherCapLevels"/>레벨까지(×2.0).</summary>
            public const float ExpHigherBonusPerLevel = 0.10f;
            public const int ExpHigherCapLevels = 10;

            /// <summary>
            /// 곤충이 캐릭터보다 낮을 때 1레벨당 EXP 감산 — 하한 <see cref="ExpLowerMinFactor"/>(−8레벨부터).
            /// 약한 곤충 반복 사냥으로 캐릭터만 앞서 나가지 않게 하는 조절기다. 0.05/×0.5로는 사냥량 3배에서
            /// 캐릭터가 리전보다 20레벨 넘게 앞섰다(progression_sim 「캐릭터 EXP」 절).
            /// </summary>
            public const float ExpLowerPenaltyPerLevel = 0.10f;
            public const float ExpLowerMinFactor = 0.2f;
        }

        // ── 훈련 비용 ──
        // 공식은 TrainingPricing이 단일 출처이고 여기는 그 계수다. 실측 근거는 rules/balance.md 「훈련 기준점」.
        public static class Training
        {
            /// <summary>
            /// 상태기(공격 상승·하락, 방어 상승)를 위력으로 환산하는 기준 위력 — <c>effectValue × 지속턴 × 이 값</c>.
            /// 그 턴 동안 중위권 기술(위력 30 안팎)을 칠 때마다 얹히는 몫이다. 이게 없던 때는 상태기의 위력 칸(1)이
            /// 가격이 되어 <b>광폭화(공격 +60%)가 26캔디 1회</b>로 끝나고 파멸의 독침은 216캔디였다.
            /// </summary>
            public const float StatusRefPower = 30f;

            /// <summary>회복기 환산 — <c>회복 비율(MaxHp 대비) × 이 값</c>. 30% 회복 ≈ 위력 30.</summary>
            public const float HealRefPower = 100f;

            /// <summary>기절기 환산 — 상대 행동 1회를 지운다 ≈ 기준 위력 1타.</summary>
            public const float StunRefPower = 30f;

            /// <summary>필요 훈련 횟수 = 1 + 가치 / 이 값(최대 <see cref="MaxSessions"/>). 옛 <c>1 + power / 12</c>와 같은 눈금이다.</summary>
            public const int SessionValueStep = 12;
            public const int MaxSessions = 5;

            /// <summary>회당 최소 캔디. 옛 범용기 공식의 하한과 같다.</summary>
            public const int MinSessionCost = 5;

            /// <summary>
            /// 능력치(개체값) +1의 기준 비용과 복리 — <c>이 값 × 성장률^현재 개체값 × 등급 배율</c>.
            /// 평균 일반 개체(5/5/5)를 S급(14/14/13)으로: 약 2,900캔디(전투 ~950회) — Lv1→50 레벨업 전부(2,548)보다 조금 크다.
            /// </summary>
            public const int StatBaseCost = 20;
            public const float StatCostGrowth = 1.2f;
        }

        // ── 나의 섬 ──
        // 공식은 IslandYield·IslandGrid가 단일 출처이고 여기는 그 계수다. 근거는 rules/balance.md 「섬 기준점」.
        public static class Island
        {
            /// <summary>섬이 타는 합성 서브에리어의 id — <c>RegionManager.EnterDetachedSubArea</c>로만 들어간다.</summary>
            public const string SubAreaId = "player_island";

            /// <summary>섬을 열어 주는 스토리 퀘스트. 풀어놓을 곤충이 생긴 시점이다.</summary>
            public const string UnlockQuestId = "q_capture3";

            /// <summary>격자 한 칸의 한 변(m).</summary>
            public const float CellSize = 1.5f;

            /// <summary>섬 한 변의 칸 수 = <c>BaseGridSize + sizeLevel × GridSizeStep</c>. 늘 짝수다(중심이 칸 경계).</summary>
            public const int BaseGridSize = 10;
            public const int GridSizeStep = 4;
            public const int MaxSizeLevel = 3;

            public const int BaseInsectSlots = 3;
            public const int MaxInsectSlots = 10;

            /// <summary>
            /// 방목 곤충 1마리의 시간당 캔디(일반 등급 기준). 기본 섬(일반 3마리)이 하루 18 —
            /// 평소 활동 수입(economy_sim 기준 하루 약 156)의 12%다. 섬만 돌려도 되는 게임이 되면 안 된다.
            /// </summary>
            public const float CandyPerInsectHour = 0.25f;

            /// <summary>방목 곤충 1마리의 시간당 코인. 등급을 보지 않는다 — 코인은 꾸미기 재화라 수집 깊이와 묶지 않는다.</summary>
            public const float CoinPerInsectHour = 0.15f;

            /// <summary>수확 없이 쌓이는 시간의 상한(시간). 설비(창고·바구니)가 <see cref="MaxCapHours"/>까지 늘린다.</summary>
            public const float BaseCapHours = 8f;
            public const float MaxCapHours = 16f;

            /// <summary>쾌적도가 주는 생산 보너스의 상한과, 그 상한에 닿는 쾌적도.</summary>
            public const float MaxComfortBonus = 0.30f;
            public const int ComfortForMaxBonus = 100;

            /// <summary>친밀도 하트 수와 하트 하나당 생산 보너스(5단계 = +20%).</summary>
            public const int MaxBondLevel = 5;
            public const float BondBonusPerLevel = 0.04f;

            /// <summary>
            /// 친밀도 단계 경계(누적 방목 시간) = <c>이 값 × L × (L+1) / 2</c> — 6·18·36·60·90시간.
            /// 상한 8시간짜리 섬을 꼬박꼬박 수확해도 다섯 하트까지 나흘 넘게 걸린다.
            /// </summary>
            public const float BondHoursUnit = 6f;

            /// <summary>
            /// 기기 시계가 이만큼 넘게 뒤로 갔으면 마지막 정산 시각을 지금으로 당긴다(초).
            /// 그보다 작게 되돌린 건 정산 시각을 그대로 둔다 — 시계를 앞뒤로 흔들어 상한분을 반복해 받는 걸 막는다.
            /// </summary>
            public const long ClockRollbackResetSeconds = 7L * 24L * 3600L;
        }

        // ── 전투 ──
        public static class Battle
        {
            public const int MaxTeamSlots = 5;
            /// <summary>Ordinary wild direct-hit pacing; excludes saved stats, DOT, duels and raids.</summary>
            public const float WildDamageMultiplier = 0.7f;
            public const float UniteGaugeMax = 100f;

            /// <summary>
            /// 공격력 상승/하락·방어 상승이 같은 방향으로 쌓일 수 있는 최대 횟수.
            /// 1v1은 지속턴이 만료시켜 자연히 줄지만 그 안에서 연타하면 무한히 쌓였고,
            /// 레이드는 만료 자체가 없어 전투 내내 남았다(break 3회면 보스 공격이 하한 고정).
            /// 두 모드가 같은 상한을 공유한다.
            /// </summary>
            public const int MaxBuffStacks = 3;

            // ── 치명타 ──────────────────────────────────────────────────────────
            //
            // 피해기(스킬 피해·기본 공격·레이드 팀원 스킬/지원 공격·보스 공격)에만 굴린다.
            // 버프·회복·독·기절·빗나감, 샌드박스(꿈 챔피언전), 레이드 합체공격, 피해 추정치(서포트 AI)는 굴리지 않는다.
            // 기대 피해는 1 + (1.5 − 1) / 16 = **+3.1%**뿐이다 — 전투 길이를 바꾸려는 값이 아니라
            // "가끔 크게 들어가는 한 방"이라는 체감용 변동이다. 예전 화면은 "최대 HP의 25% 이상"을
            // CRITICAL로 **표시만** 했는데 전투가 3~4라운드라 거의 매 타격이 그 선을 넘었다.
            // 판정은 명중과 **다른 난수 줄기**로 한다(InsectBattleController/RaidBattleController.SetCritSource) —
            // 같은 줄기면 명중 롤 순서가 밀려 시드 고정 시나리오가 전부 바뀐다.

            /// <summary>피해기 한 번이 치명타일 확률(1/16). 롤이 이 값 <b>미만</b>이면 치명타다.</summary>
            public const float CritChance = 1f / 16f;

            /// <summary>치명타 피해 배율 — 공방 비율·야생 페이싱을 거치기 전 피해량에 곱한다.</summary>
            public const float CritMultiplier = 1.5f;

            // ── 전투 길이 ────────────────────────────────────────────────────────
            //
            // 옛 값(레벨항 ×2 · 공방비 0.5~2.5 · HP +3/Lv)은 **양쪽이 서로를 한두 턴에
            // 지우는** 구간을 만들었다. 데미지가 (power + Lv×2) × 최대 2.5인데 HP는
            // base + Lv×3뿐이라 레벨이 오를수록 데미지가 HP를 앞질렀다.
            // 실측(Uncommon 플레이어 vs 동레벨 Epic): Lv22·Lv28·Lv42 전부 **내가 2턴에
            // 잡고 1턴에 죽는** 결과였고, 수문장전도 같았다.
            //
            // 세 값은 **함께 움직여야 한다** — 하나만 바꾸면 다른 쪽이 곧바로 지배한다.
            // 조정 후 같은 구간이 킬 3턴 / 생존 2~3턴이 된다.

            /// <summary>데미지의 레벨 가산분(스킬 위력에 <c>Level × 이 값</c>을 더한다).</summary>
            public const int LevelDamageScale = 1;

            /// <summary>레벨당 최대 HP 증가분. 데미지의 레벨항보다 커야 전투가 길어진다.</summary>
            public const int HpPerLevel = 4;

            /// <summary>
            /// 공격력/유효방어력 비의 하한·상한. 상한이 곧 "스탯 차이로 낼 수 있는 최대 배율"이라
            /// 여기가 넓으면 위력을 아무리 낮춰도 한 방에 끝난다(옛 2.5가 그랬다).
            /// </summary>
            public const float MinAtkDefRatio = 0.7f;

            public const float MaxAtkDefRatio = 1.5f;

            /// <summary>
            /// 레이드에서 <b>리더가 아닌</b> 팀원이 자기 스킬을 쓸 때의 위력 배율.
            /// 피해와 회복량에만 곱한다 — 버프·디버프·기절은 스택/불리언이라 배율이 의미가 없고,
            /// 스택 상한(<see cref="MaxBuffStacks"/>)이 이미 총량을 가둔다.
            ///
            /// 리더 우위는 유지하되(1.0 대 0.6), 예전의 고정 지원 공격
            /// (<c>RaidRoundResolver.SupportAssistPowerMultiplier</c> = 0.25, 상성·자속도 없었다)보다는
            /// 확실히 세다. 스킬이 없거나 전부 쿨다운이면 그 고정 지원 공격으로 폴백한다.
            /// </summary>
            public const float RaidSupportSkillPowerMultiplier = 0.6f;

            /// <summary>
            /// 레이드 보스 HP 배율(일반 개체 대비). ATK ×1.5 · DEF ×1.3은
            /// <c>RaidBattleController.StartRaid</c>가 정수 연산으로 직접 곱한다.
            ///
            /// <b>왜 5가 아니라 8.5인가</b>: 비-리더 4마리가 <c>ATK × 0.25</c> 고정 지원 공격에서
            /// 자기 스킬(<see cref="RaidSupportSkillPowerMultiplier"/>, 상성·자속 적용)로 바뀌면서
            /// 팀 화력이 올랐다. 실측 공식으로 두 구간을 계산하면
            /// Lv20 Epic 보스 <b>1.62배</b>, Lv40 Legendary 보스 <b>1.78배</b>다
            /// (리더분은 그대로이고 서포트분만 오르므로 라운드 총합 기준). 평균 ~1.7을 5에 곱해 8.5 —
            /// <b>전투 길이(라운드 수)를 개편 전과 같게 두려는 값</b>이지 난이도를 올리려는 값이 아니다.
            /// 서포트 배율을 건드리면 이 값도 같이 계산해야 한다.
            ///
            /// <b>주의 — 위 산출 근거는 순차 행동 전환(2026-08-08) 이후 더는 성립하지 않는다.</b>
            /// 8.5는 "리더 1×1.0 + 서포트 4×0.6 = 3.4유닛"을 전제로 뽑은 값인데, 지금은
            /// <c>ResolveTeamCommand</c>가 <b>모든 슬롯</b>을 <c>ResolveLeaderSkill</c>(배율 1.0)로
            /// 태우므로 플레이어가 5슬롯을 직접 조작하면 라운드 화력이 <b>5.0유닛(+47%)</b>이다.
            /// 동시에 <see cref="RaidBossUsesAreaAttack"/>가 꺼지며 단일의 2.17배였던 보스 주력기도
            /// 사라졌다. 즉 지금 8.5는 <b>의도한 전투 길이가 아니라 그보다 짧은 전투</b>를 낸다.
            /// 값을 그대로 둔 것은 의도적이다 — 사용자가 "레이드가 너무 세다"고 해서 AOE를 껐고,
            /// 여기서 HP를 올리면 그 요청을 되돌리는 셈이 된다. 난이도를 다시 조일 때는 이 숫자가
            /// 아니라 <b>서포트/리더 배율 구분</b>부터 재설계할 것(전 슬롯 1.0이 근본 원인이다).
            ///
            /// <b>8.5 → 4.5로 재산출했다(전투 길이 조정과 짝).</b> <see cref="LevelDamageScale"/>·
            /// <see cref="MaxAtkDefRatio"/>·<see cref="HpPerLevel"/>을 함께 바꾸면서 팀 화력이
            /// 절반 아래로 내려갔고, 보스 HP도 <c>HpPerLevel</c>만큼 함께 올랐다. 8.5를 그대로 두면
            /// 같은 구간이 <b>6/5/4턴 → 10/9/8턴</b>이 된다(리더 1.0 + 서포트 0.6×4 기준 실측).
            /// 4.5는 그 길이를 <b>6/5/5턴</b>으로 되돌리는 값이다 — 위 문단과 같은 취지로
            /// <b>난이도를 올리지도 내리지도 않으려는</b> 값이지 새 밸런스가 아니다.
            /// 위 세 상수 중 하나라도 건드리면 이 값을 다시 계산할 것.
            ///
            /// <b>4.5 → 4.65 (2026-10-04, 치명타 도입과 짝).</b> <see cref="CritChance"/>·<see cref="CritMultiplier"/>가
            /// 팀 화력 기대값을 ×1.031 올려 6턴 구간(Epic Lv20 vs Uncommon)이 중앙값 5턴으로 내려왔다 — 그 구간은
            /// 원래 5.024턴(5턴 뒤 남는 HP 0.5%)으로 아슬아슬했다. 4.5 × 1.031을 올림한 4.65가 6/5/5를 되돌린다(4.6은 부족).
            /// 치명타 확률·배율을 바꿔도 이 값을 다시 계산할 것.
            /// </summary>
            public const float RaidBossHpMultiplier = 4.65f;

            /// <summary>보스 HP가 이 비율 이하로 떨어지면 격노(1회 래치, 회복해도 풀리지 않는다).</summary>
            public const float RaidBossEnrageHpRatio = 0.5f;

            /// <summary>
            /// 격노 시 <b>단일 대상</b> 피해 배율. 전체공격은 배율 없이 <b>간격만</b> 짧아진다
            /// (<see cref="RaidBossEnragedAreaInterval"/>) — 둘 다 세지면 격노 진입이 곧 전멸이다.
            /// 레이드엔 부활·교체·아이템이 없어 되돌릴 수단이 없기 때문이다.
            /// </summary>
            public const float RaidBossEnragedDamageMultiplier = 1.15f;

            /// <summary>전체공격 사이에 끼는 단일 턴 수. 평소 2, 격노 시 1.</summary>
            public const int RaidBossAreaInterval = 2;
            public const int RaidBossEnragedAreaInterval = 1;

            /// <summary>
            /// 보스가 <b>전체공격(AOE)</b>을 예고하는가. <c>false</c>면 항상 단일 대상 하나만 노린다
            /// (위 두 간격 상수와 <c>bossCooldown</c>은 그대로 도는데 의도 생성에서만 걸러진다).
            ///
            /// 왜 껐나: 팀 턴이 <b>곤충 5마리 순차 행동</b>으로 바뀌면서 라운드가 길어졌는데,
            /// 전체공격은 라운드 한 번에 5마리 전원을 깎아 평균 팀 피해가 단일의 <b>2.17배</b>였다
            /// (2라운드마다 5명×2/3위력). 레이드엔 부활·교체·아이템이 없어 되돌릴 수단도 없다.
            /// AOE 코드 경로(<c>RaidRoundResolver.ResolveBossIntent</c>의 <c>IsArea</c> 분기)는
            /// 그대로 살아 있다 — 여기만 <c>true</c>로 되돌리면 예전 동작이다.
            /// </summary>
            public const bool RaidBossUsesAreaAttack = false;
        }

        // ── 기본 설정값 ──
        public static class Defaults
        {
            public const float MasterVolume = 1.0f;
            public const float SfxVolume = 0.8f;
            public const int GraphicsQuality = 2;
        }
    }
}
