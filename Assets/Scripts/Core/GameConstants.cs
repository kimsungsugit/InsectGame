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
            // 서브 퀘스트 진행/반복횟수 — 로컬 전용(QuestUnseen처럼 클라우드 미동기).
            public const string QuestSideProgress = "InsectGame.QuestSideProgress";
            public const string QuestSideRepeat = "InsectGame.QuestSideRepeat";
            public const string TutorialHidden = "InsectGame.TutorialHidden";
            public const string LastSubAreaId = "InsectGame.SubArea.LastEntered";
        }

        // ── 플레이어 ──
        public static class Player
        {
            public const int MaxEquipSlots = 4;
            public const int MaxLearnedSkills = 4;
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
