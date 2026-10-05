using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.NPC;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 1대1 전투 화면의 <b>흐름</b> — 결과 화면 닫기, 읽는 단계의 배속 하한, 진입 인트로의 길이, 전투 종류.
    /// 규칙은 순수부(<see cref="BattleResultRules"/>·<see cref="BattleReadPacing"/>·<see cref="BattleKinds"/>)가 들고, 여기는 화면 상태에
    /// 잇기만 한다. 모놀리스 본체를 키우지 않으려고 partial로 뗐다 — 본체는 <c>Update</c>(시계 배율·결과 닫기)·<c>OnGUI</c>(Space/Enter)·
    /// <c>EnterResult</c>/<c>EndBattle</c>(초기화) 네 군데서 부른다.
    ///
    /// <b>ui-dev·visual-dev가 읽는 공개 값</b>: <see cref="ResultCanClose"/>·<see cref="ResultShownSeconds"/>(결과 화면 안내·승리 연출 대기),
    /// <see cref="IsIntroPlaying"/>·<see cref="IntroProgress"/>·<see cref="EntryIntroProgress"/>(진입 샷·화면 쓸기·「야생 ○○이(가) 나타났다!」),
    /// <see cref="CurrentBattleKind"/>(문구 고르기), <see cref="EnemySwitchStagingSeconds"/>(교체 등장 연출 길이).
    /// </summary>
    public partial class BattleScreenUI
    {
        // ── 결과 화면 닫기(BattleResultRules) ──

        private bool wantResultClose;     // OnGUI Space/Enter — 결과 화면에서만 선다
        private bool resultCloseArmed;    // 받아들인 누름 — 손을 떼면 닫는다
        private float resultArmedAt;

        /// <summary>
        /// 결과 화면이 지금 누름을 받는가(처음 <see cref="BattleResultRules.InputLockSeconds"/>초가 지났다). ui-dev가 「눌러서 계속」 안내를
        /// 이때부터 보인다. 결과 화면이 아니면 false. 꿈 챔피언전도 누르면 닫힌다(그리고 4초 뒤 저절로 닫힌다).
        /// </summary>
        public bool ResultCanClose => phase == Phase.Result && BattleResultRules.CanClose(resultTimer);

        /// <summary>결과 화면이 뜬 뒤 지난 <b>실제</b> 초(배속·히트스톱·일시정지와 무관). 결과 화면이 아니면 0. 승리 연출 대기에 쓴다.</summary>
        public float ResultShownSeconds => phase == Phase.Result ? resultTimer : 0f;

        /// <summary>결과 화면을 띄운 쪽의 공통 초기화 — <c>EnterResult</c>와 <c>EndBattle</c>가 부른다.</summary>
        private void ResetResultClose()
        {
            wantResultClose = false;
            resultCloseArmed = false;
            resultArmedAt = 0f;
        }

        /// <summary>
        /// 결과 화면의 한 프레임 — 탭·클릭·Space/Enter를 받아(잠금 뒤) 손을 뗄 때 닫는다. 자동으로는 꿈 챔피언전만 닫힌다.
        /// <c>wantMouseClick</c>은 Update 말미가 비우므로 여기서 먼저 읽는다.
        /// </summary>
        private void TickResultClose()
        {
            // 결과 화면은 이제 플레이어가 닫을 때까지 서 있다 — 그동안 플레이어를 묶어 둔다. PlayerMovement는 20초 넘게 묶이면
            // 스스로 풀고(AutoUnfreezeTime) ESC로도 풀리는데, 풀린 채로 이 화면을 탭하면 그 탭이 필드의 클릭-이동으로 샌다.
            if (playerMovement != null && !playerMovement.IsFrozen) playerMovement.SetFrozen(true);

            bool pressed = wantResultClose || wantMouseClick || BattleResultInput.PressedThisFrame();
            wantResultClose = false;
            if (!resultCloseArmed && BattleResultRules.AcceptsPress(resultTimer, pressed))
            {
                resultCloseArmed = true;
                resultArmedAt = resultTimer;
            }

            bool sandbox = battleController != null && battleController.IsSandbox;
            bool released = resultCloseArmed
                && BattleResultRules.ReleaseComplete(BattleResultInput.Held(), resultTimer - resultArmedAt);
            if (released || BattleResultRules.ShouldAutoClose(resultTimer, sandbox))
                EndBattle();
        }

        /// <summary>OnGUI의 KeyDown — 결과 화면에서 Space/Enter를 받는다(다른 단계의 키는 건드리지 않는다).</summary>
        private void CaptureResultKey(Event evt)
        {
            if (phase != Phase.Result || evt == null || evt.type != EventType.KeyDown) return;
            if (!BattleResultInput.IsCloseKey(evt.keyCode)) return;
            wantResultClose = true;
            evt.Use();
        }

        // ── 읽는 단계의 배속 하한(BattleReadPacing) ──

        /// <summary>단계 시계(<c>phaseTimer</c>) 배율 — 상대 교체 단계만 <see cref="BattleReadPacing.EnemySwitchMinSeconds"/> 하한을 둔다.</summary>
        private float PhaseClockScale => phase == Phase.EnemySwitch
            ? BattleReadPacing.ClockScale(EnemySwitchSeconds, BattleReadPacing.EnemySwitchMinSeconds, BattlePresentation.Speed)
            : 1f;

        /// <summary>인트로 시계(<c>introTimer</c>) 배율 — 진입 구간은 실제 시간, 컷인은 <see cref="BattleReadPacing.CutInMinSeconds"/> 하한.</summary>
        private float IntroClockScale => phase == Phase.Intro
            ? BattleReadPacing.IntroClockScale(introTimer, IntroCutInSeconds, BattlePresentation.Speed)
            : 1f;

        /// <summary>
        /// 상대 교체 등장 연출(아레나 <c>PlayEnemySwitchIn</c>)에 넘길 길이(연출 시계 초) — 교체 단계와 <b>같은 실제 시간</b>에 끝난다.
        /// 1배속 1.2초, 2배속 2.0초(실제 1.0초). <see cref="EnemySwitchSeconds"/>를 넘기면 2배속에서 연출이 0.6초에 끝나고 0.4초 서 있는다.
        /// </summary>
        public float EnemySwitchStagingSeconds =>
            BattleReadPacing.StagingSeconds(EnemySwitchSeconds, BattleReadPacing.EnemySwitchMinSeconds, BattlePresentation.Speed);

        // ── 진입 인트로 ──

        /// <summary>이 전투의 컷인 길이 — 대결(한마디 표가 있는 상대)·수문장이면 <c>CutInSeconds</c>, 아니면 0.</summary>
        private float IntroCutInSeconds => HasCutIn ? CutInSeconds : 0f;

        /// <summary>인트로 단계인가(입력 없음 — 배속 버튼도 숨는다).</summary>
        public bool IsIntroPlaying => phase == Phase.Intro;

        /// <summary>
        /// 인트로 전체 진행률 0~1 — 진입 구간(<see cref="BattleReadPacing.EntryIntroSeconds"/>) + [컷인 | 평범한 인트로의 나머지].
        /// 전투 밖이면 0, 인트로가 끝난 뒤면 1. 구간마다 시계 배율이 달라 실제 시간에 정비례하지는 않는다 — 진입 연출은 <see cref="EntryIntroProgress"/>.
        /// </summary>
        public float IntroProgress => phase == Phase.None ? 0f
            : phase == Phase.Intro ? Mathf.Clamp01(introTimer / IntroSeconds) : 1f;

        /// <summary>
        /// 진입 구간 진행률 0~1 — 진입 샷·화면 쓸기·「야생 ○○이(가) 나타났다!」가 이 값으로 그린다. 이 구간은 배속과 무관하게 실제 시간으로
        /// <see cref="BattleReadPacing.EntryIntroSeconds"/>(1.4초) 동안 흐른다. 컷인이 있는 전투는 이 구간이 끝난 뒤 컷인이 시작한다.
        /// </summary>
        public float EntryIntroProgress => phase == Phase.None ? 0f
            : phase == Phase.Intro ? BattleReadPacing.EntryProgress(introTimer) : 1f;

        /// <summary>컷인 시계 — 진입 구간 뒤부터 0(그 전엔 음수). <c>DrawDuelCutIn</c>이 읽는다.</summary>
        private float CutInClock => BattleReadPacing.CutInElapsed(introTimer);

        // ── 전투 종류 ──

        /// <summary>
        /// 지금(또는 방금 끝난) 전투의 종류 — 야생·수문장·아이 대결·간부 대결·라온 대결·꿈 챔피언전. 문구 고르기용(ui-dev).
        /// 상대 표지가 시작 함수 <b>다음에</b> 서므로(<see cref="BattleKinds"/>) 시작 콜백 안에서는 대결이 아이로 보일 수 있다 — 그리는 쪽은 OnGUI에서 읽는다.
        /// 사람 이름(「집게」)은 한마디 표(<c>DuelBanter</c> — <c>duelLines.name</c>)에 있다. 아이 대결은 이름 표가 없다.
        /// </summary>
        public BattleKind CurrentBattleKind
        {
            get
            {
                if (battleController == null) return BattleKind.Wild;
                string opponent = battleController.DuelOpponentId ?? string.Empty;
                return BattleKinds.Classify(
                    battleController.IsSandbox,
                    !string.IsNullOrEmpty(battleController.EnemyGuardianRegionId),
                    battleController.IsDuel,
                    opponent.Length > 0,
                    opponent.Length > 0 && NpcRivalDuels.TryGetStage(opponent, out _));
            }
        }
    }

    /// <summary>결과 화면을 닫는 입력 — 1대1·레이드가 함께 쓴다(Unity <c>Input</c>을 읽는 자리라 순수부와 나눴다).</summary>
    internal static class BattleResultInput
    {
        internal static bool IsCloseKey(KeyCode key)
            => key == KeyCode.Space || key == KeyCode.Return || key == KeyCode.KeypadEnter;

        /// <summary>이번 프레임에 눌렀다 — 탭·클릭·Space·Enter.</summary>
        internal static bool PressedThisFrame()
        {
            return Input.GetMouseButtonDown(0)
                || Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter);
        }

        /// <summary>
        /// 아직 누르고 있다 — 손을 뗄 때 닫아야 그 탭이 필드의 클릭-이동으로 새지 않는다(<see cref="BattleResultRules"/> 주석).
        /// </summary>
        internal static bool Held()
        {
            return Input.GetMouseButton(0)
                || Input.touchCount > 0
                || Input.GetKey(KeyCode.Space)
                || Input.GetKey(KeyCode.Return)
                || Input.GetKey(KeyCode.KeypadEnter);
        }
    }
}
