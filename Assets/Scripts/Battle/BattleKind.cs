using InsectGame.Core;

namespace InsectGame.Battle
{
    /// <summary>
    /// 1대1 전투의 종류 — 화면 문구(「야생 ○○이(가) 나타났다!」·「집게가 승부를 걸어왔다!」)와 연출을 고르는 값.
    /// 화면에서는 <c>BattleScreenUI.CurrentBattleKind</c>로 읽는다(라온 단계 표를 아는 쪽이 UI라서 거기서 가른다).
    /// 레이드는 이 값을 쓰지 않는다 — 수문장 레이드인지는 <c>RaidBattleController.BossGuardianRegionId</c>로 안다.
    /// </summary>
    public enum BattleKind
    {
        /// <summary>필드의 야생 곤충(습격·나의 섬 손님 포함).</summary>
        Wild = 0,
        /// <summary>리전 수문장(1대1 — 영웅·전설 수문장은 레이드다).</summary>
        Guardian,
        /// <summary>곤충잡이 아이와의 대결.</summary>
        KidDuel,
        /// <summary>명부회 간부·하수와의 대결(장부가 걸린다 — 팀 대결 포함).</summary>
        BossDuel,
        /// <summary>라온 라이벌 대결.</summary>
        RivalDuel,
        /// <summary>「챔피언의 꿈」 챔피언전(샌드박스 — 보상·도감·포획 없음).</summary>
        Sandbox
    }

    /// <summary>
    /// <see cref="BattleKind"/> 판정 — 순수. 컨트롤러의 시작 시점 표지만 받는다(<c>IsSandbox</c>·<c>EnemyGuardianRegionId</c>·
    /// <c>IsDuel</c>·<c>DuelOpponentId</c>). 우선순위: 샌드박스 → 수문장 → 야생 → 아이(상대 표지 없음) → 라온(라이벌 단계) → 간부.
    ///
    /// <b>상대 표지는 시작 함수 다음에 선다</b>(<c>StartDuel</c> → <c>SetDuelOpponent</c>) — 시작 콜백(<c>BattleUpdated</c>) 안에서 물으면
    /// 간부·라온도 아이로 보인다. 화면은 매 프레임 묻는다(첫 OnGUI 때는 이미 섰다).
    /// </summary>
    public static class BattleKinds
    {
        public static BattleKind Classify(bool sandbox, bool guardian, bool duel, bool hasOpponent, bool rivalStage)
        {
            if (sandbox) return BattleKind.Sandbox;
            if (guardian) return BattleKind.Guardian;
            if (!duel) return BattleKind.Wild;
            if (!hasOpponent) return BattleKind.KidDuel;
            return rivalStage ? BattleKind.RivalDuel : BattleKind.BossDuel;
        }

        /// <summary>사람이 거는 대결인가(아이·간부·라온) — 「○○이(가) 승부를 걸어왔다!」 쪽 문구.</summary>
        public static bool IsTrainerDuel(BattleKind kind)
            => kind == BattleKind.KidDuel || kind == BattleKind.BossDuel || kind == BattleKind.RivalDuel;
    }

    /// <summary>
    /// 전투 곡 고르기 — 순수. 전환은 전투를 연 쪽이 한다: 1대1 시작 신호에 화면이 <see cref="BgmType.Battle"/>을 먼저 걸고,
    /// 대결을 연 <c>NpcDuelController</c>가 같은 프레임에 간부·라온 곡으로 덮는다(<c>AudioManager.PlayBGM</c>은 한 프레임 늦게 틀어
    /// 마지막 요청만 남는다). 레이드는 화면(<c>RaidBattleUI</c>)의 시작 핸들러가 한 번에 고른다.
    /// 새 곡을 늘리면 <c>AudioManager</c>의 등록 4지점(문자열·전환 호출부·생성기·<c>IsCombatBgm</c>)은 visual-dev 몫이다.
    /// </summary>
    public static class BattleMusic
    {
        /// <summary>1대1 — 수문장(초원 사마귀처럼 1대1로 맞서는 수문장)이면 수문장 곡, 아니면 전투 곡. 대결은 NpcDuelController가 덮는다.</summary>
        public static BgmType OneVsOne(string guardianRegionId)
            => string.IsNullOrEmpty(guardianRegionId) ? BgmType.Battle : BgmType.Guardian;

        /// <summary>레이드 — 수문장 레이드면 수문장 곡, 아니면 레이드 곡.</summary>
        public static BgmType Raid(string guardianRegionId)
            => string.IsNullOrEmpty(guardianRegionId) ? BgmType.RaidBattle : BgmType.Guardian;

        /// <summary>명부회 대결 — 최종전(관장 하월·무명)과 간부전을 가른다.</summary>
        public static BgmType BossDuel(bool isFinal) => isFinal ? BgmType.BossFinal : BgmType.BossLedger;

        /// <summary>라온 라이벌 대결.</summary>
        public static BgmType RivalDuel => BgmType.Rival;
    }
}
