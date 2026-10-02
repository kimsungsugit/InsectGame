namespace InsectGame.Core
{
    /// <summary>
    /// 「챔피언의 꿈」 프롤로그가 도는 동안 켜지는 전역 표지. 그동안 <b>꿈 밖의 게임 상태는 하나도 바뀌지 않아야 한다.</b>
    ///
    /// 읽는 쪽이 여럿이라(퀘스트 진행·필드 HUD·자동 주행·섬 HUD) 이벤트가 아니라 정적 표지 하나로 둔다 —
    /// 꿈이 끝나는 길이 여럿이다(끝까지 봄·건너뜀·오류·씬 재로드). 켠 쪽이 <see cref="End"/>를 안 부르면
    /// 퀘스트가 영영 안 올라가므로 지휘자(<c>DreamPrologueDirector</c>)가 OnDisable/OnDestroy에서도 끈다.
    /// </summary>
    public static class DreamPrologueState
    {
        /// <summary>프롤로그가 도는 중인가.</summary>
        public static bool Active { get; private set; }

        public static void Begin() => Active = true;

        public static void End() => Active = false;
    }

    /// <summary>프롤로그를 <b>언제 시작하는가</b>의 순수 판정.</summary>
    public static class DreamPrologueRules
    {
        /// <summary>
        /// 첫 퀘스트. 이 퀘스트가 아직 진행 0으로 활성이라는 것은 "이제 막 시작한 새 계정"이라는 뜻이다 —
        /// 따로 "신규 계정" 플래그를 두지 않는다(기존 계정은 이 퀘스트를 이미 끝냈다).
        /// </summary>
        public const string StartQuestId = "q_move";

        /// <summary>계정별로 한 번 봤는가를 적는 PlayerPrefs 키(<c>SaveScope.PrefsKey</c>로 계정 스코프를 붙인다).</summary>
        public const string PlayedPrefsKey = "InsectGame.DreamPrologue.Played";

        /// <summary>조건이 이 시간 동안 <b>계속</b> 참이어야 시작한다 — 로그인·월드 선택 화면이 닫히는 몇 프레임을 건너뛴다.</summary>
        public const float StableSeconds = 1.5f;

        /// <param name="activeQuestId">지금 활성인 스토리 퀘스트(없으면 null).</param>
        /// <param name="activeProgress">그 퀘스트의 진행.</param>
        /// <param name="alreadyPlayed">이 계정이 이미 봤는가.</param>
        /// <param name="modalOpen">대화·메뉴·로그인 같은 모달이 떠 있는가.</param>
        /// <param name="playerFrozen">조작이 묶여 있는가(대화·연출 중).</param>
        /// <param name="inSubArea">서브에리어(동굴·섬) 안인가 — 섬에 들어가는 연출이라 이미 안에 있으면 못 한다.</param>
        public static bool ShouldAutoStart(string activeQuestId, int activeProgress, bool alreadyPlayed,
            bool modalOpen, bool playerFrozen, bool inSubArea)
        {
            if (alreadyPlayed) return false;
            if (activeQuestId != StartQuestId || activeProgress > 0) return false;
            return !modalOpen && !playerFrozen && !inSubArea;
        }

        /// <summary>설정 화면의 「다시 보기」가 눌러도 되는가 — 자동 시작과 같되 "이미 봤다"는 막지 않는다.</summary>
        public static bool CanReplay(bool running, bool modalOpen, bool playerFrozen, bool inSubArea)
        {
            return !running && !modalOpen && !playerFrozen && !inSubArea;
        }
    }
}
