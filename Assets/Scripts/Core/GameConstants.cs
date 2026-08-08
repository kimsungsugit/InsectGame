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

        // ── 전투 ──
        public static class Battle
        {
            public const int MaxTeamSlots = 5;
            public const float UniteGaugeMax = 100f;

            /// <summary>
            /// 공격력 상승/하락·방어 상승이 같은 방향으로 쌓일 수 있는 최대 횟수.
            /// 1v1은 지속턴이 만료시켜 자연히 줄지만 그 안에서 연타하면 무한히 쌓였고,
            /// 레이드는 만료 자체가 없어 전투 내내 남았다(break 3회면 보스 공격이 하한 고정).
            /// 두 모드가 같은 상한을 공유한다.
            /// </summary>
            public const int MaxBuffStacks = 3;

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
            /// </summary>
            public const float RaidBossHpMultiplier = 8.5f;

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
