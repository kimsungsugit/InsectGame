namespace InsectGame.Story
{
    /// <summary>
    /// 전투 화면 뒤로 미룬 이야기(대사 트리거·컷신·영상)의 <b>대기 시계</b> — 순수 규칙. <see cref="StoryDirector"/>·
    /// <see cref="CutsceneDirector"/>·<see cref="StoryVideoDirector"/>가 함께 쓴다.
    ///
    /// <b>상한이 왜 있나.</b> 세 곳 모두 "전투 화면이 아직 떠 있으면 미룬다"를 카메라의 배틀 모드(<c>CameraFollower.InBattleMode</c>)로
    /// 판정한다(<c>BattleScreenUI</c>는 모달이 아니다). 그 신호가 어떤 이유로든 굳으면(컷신 복원 누락·<c>EndBattle</c> 중단·씬 교체)
    /// 미뤄 둔 것이 영원히 기다린다 — 상한은 그때를 위한 안전장치다. 컷신·영상은 연출이라 <b>버리고</b>(비트는 이미 완료됐다 — 억지로 틀면
    /// 카메라가 전투 구도에 갇힌다), 카메라가 배선되지 않은 대사 큐는 이야기의 진행이라 <b>그냥 쏜다</b>.
    ///
    /// <b>결과 화면 시간은 세지 않는다.</b> 2026-10-04부터 결과 화면은 눌러야 닫힌다(<c>BattleResultRules</c> — 예전엔 1대1 4초·레이드
    /// 5초 뒤 저절로). 보상을 천천히 보는 아이에게 결과 화면 30초는 정상이고, 언제든 누르면 닫히는 화면이라 굳은 것도 아니다.
    /// 그 시간을 셌다면 결과를 12초 넘게 본 사람의 미뤄 둔 컷신·영상이 조용히 사라지고, 카메라 없는 대사 큐는 결과 화면 위로 대사를 띄운다.
    /// 결과 화면이 <b>아닌데</b> 전투 카메라가 안 풀리는 것(진짜로 멈춘 화면)은 그대로 센다.
    ///
    /// <b>모달(대화·메뉴)도 세지 않는다</b>(<see cref="TickScene"/>) — 역시 플레이어가 닫는 화면이다. 예전 영상 대기는 이 시간을 함께 세어,
    /// 전투가 닫힌 뒤 이어진 대사를 12초 넘게 읽으면 영상이 사라졌다.
    ///
    /// 결과 화면 여부는 Story가 UI를 모르게 함수 하나(<c>Func&lt;bool&gt;</c>)로 받는다 — 부트스트랩이 <c>BattleScreenUI</c>·<c>RaidBattleUI</c>의
    /// <c>ResultShownSeconds</c>로 만들어 세 지휘자에게 <c>AutoWire</c>한다. 없으면(미배선·테스트) 예전처럼 결과 화면도 센다.
    /// </summary>
    public static class StoryBattleWait
    {
        /// <summary>미뤄 둔 컷신·영상을 포기하는 대기(초) — 결과 화면·모달을 뺀, 전투 카메라가 안 풀린 시간.</summary>
        public const float SceneGiveUpSeconds = 12f;

        /// <summary>
        /// 카메라 미배선 대사 큐가 전투 화면 종료 통지를 기다리는 상한(초) — 결과 화면을 뺀 시간. 넘으면 겹치더라도 쏜다
        /// (진행을 잃는 것보다 낫다). 카메라가 배선돼 있으면 쓰이지 않는다 — 그 경로는 전투 화면 위로 대사를 쏘지 않는다.
        /// </summary>
        public const float DialogueFallbackSeconds = 12f;

        /// <summary>대사 큐가 이만큼(결과 화면 제외) 기다린 뒤에야 흘렀으면 경고를 남긴다 — 진단용이다. 발화를 앞당기지 않는다.</summary>
        public const float DialogueLongWaitWarnSeconds = 60f;

        /// <summary>미뤄 둔 컷신·영상의 한 프레임 판정.</summary>
        public enum SceneStep
        {
            /// <summary>미뤄 둔 것이 없다.</summary>
            None,
            /// <summary>아직 막혀 있다 — 계속 들고 있는다.</summary>
            Wait,
            /// <summary>화면이 비었다 — 지금 튼다.</summary>
            Play,
            /// <summary>전투 화면이 상한 넘게 안 풀렸다 — 버린다.</summary>
            GiveUp
        }

        /// <summary>
        /// 대기 시계를 한 프레임 올린다. 결과 화면이 떠 있으면 그대로 둔다(멈춤). 음수·0 프레임도 그대로.
        /// </summary>
        public static float Advance(float waited, float deltaSeconds, bool resultScreenShowing)
        {
            if (resultScreenShowing || deltaSeconds <= 0f) return waited;
            return waited + deltaSeconds;
        }

        /// <summary>
        /// 미뤄 둔 컷신·영상의 한 프레임. 시계(<paramref name="waited"/>)는 <b>전투 카메라가 결과 화면 밖에서 안 풀린 시간만</b> 센다.
        /// </summary>
        /// <param name="waited">지금까지 센 대기(초) — 갱신된다.</param>
        /// <param name="deltaSeconds">이번 프레임의 실제 초(<c>Time.unscaledDeltaTime</c>).</param>
        /// <param name="battleOnScreen">전투 카메라가 아직 배틀 모드다(결과 화면 포함).</param>
        /// <param name="resultScreenShowing">1대1·레이드 결과 화면이 떠 있다.</param>
        /// <param name="modalOpen">다른 모달이 떠 있어 기다려야 한다(영상만 본다 — 컷신은 넘긴다 <c>false</c>).</param>
        public static SceneStep TickScene(ref float waited, float deltaSeconds,
            bool battleOnScreen, bool resultScreenShowing, bool modalOpen)
        {
            if (!battleOnScreen && !modalOpen) return SceneStep.Play;
            if (battleOnScreen) waited = Advance(waited, deltaSeconds, resultScreenShowing);
            return waited >= SceneGiveUpSeconds ? SceneStep.GiveUp : SceneStep.Wait;
        }

        /// <summary>결과 화면 탐침을 읽는다 — 없으면(미배선) 거짓. 탐침이 던져도 거짓(대기 규칙이 전투 화면 쪽 예외로 죽지 않게).</summary>
        public static bool ReadProbe(System.Func<bool> probe)
        {
            if (probe == null) return false;
            try { return probe(); }
            catch (System.Exception) { return false; }
        }
    }
}
