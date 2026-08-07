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
        public static class Leveling
        {
            public const int FallbackMaxLevel = 50;
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
