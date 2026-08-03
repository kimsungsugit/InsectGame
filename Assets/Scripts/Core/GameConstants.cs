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
