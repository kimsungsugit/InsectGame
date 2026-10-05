using InsectGame.Battle;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 레이드 화면의 <b>흐름</b> — 결과 화면 닫기와 읽는 단계(인트로·그림자 변신)의 배속 하한. 1대1(<c>BattleScreenUI.Flow</c>)과
    /// 같은 순수 규칙(<see cref="BattleResultRules"/>·<see cref="BattleReadPacing"/>)을 쓴다. 모놀리스 본체를 키우지 않으려고 partial로 뗐다 —
    /// 본체는 <c>Update</c>(시계 배율·결과 닫기)·<c>OnGUI</c>(Space/Enter)·<c>OnRaidEnded</c>/<c>EndRaid</c>(초기화)에서 부른다.
    ///
    /// <b>결과 화면의 두 시계</b>: 그리기용 <c>resultTimer</c>는 예전처럼 연출 시계(배속)다 — 보상 숫자가 튀어나오는 박자가 그대로다.
    /// 닫기 판정은 <b>실제 시간</b>(<c>resultRealTimer</c>)으로 잰다 — 2배속이라고 잠금이 0.3초가 되지 않게.
    /// </summary>
    public partial class RaidBattleUI
    {
        // ── 결과 화면 닫기 ──

        private float resultRealTimer;
        private bool wantResultClose;
        private bool resultCloseArmed;
        private float resultArmedAt;

        /// <summary>
        /// 결과 화면(RAID CLEAR/FAILED)이 지금 누름을 받는가(처음 <see cref="BattleResultRules.InputLockSeconds"/>초가 지났다).
        /// ui-dev가 「잠시 후 자동으로 돌아갑니다...」 대신 「눌러서 계속」을 이때부터 보인다. 결과 화면이 아니면 false.
        /// </summary>
        public bool ResultCanClose => phase == Phase.Result && BattleResultRules.CanClose(resultRealTimer);

        /// <summary>결과 화면이 뜬 뒤 지난 <b>실제</b> 초. 결과 화면이 아니면 0.</summary>
        public float ResultShownSeconds => phase == Phase.Result ? resultRealTimer : 0f;

        private void ResetResultClose()
        {
            resultRealTimer = 0f;
            wantResultClose = false;
            resultCloseArmed = false;
            resultArmedAt = 0f;
        }

        /// <summary>
        /// 결과 화면의 한 프레임 — 탭·클릭·Space/Enter를 받아(잠금 뒤) 손을 뗄 때 닫는다. 레이드에는 저절로 닫히는 길이 없다
        /// (꿈 챔피언전은 1대1이다). <c>wantMouseClick</c>은 Update 말미가 비우므로 여기서 먼저 읽는다.
        /// </summary>
        private void TickResultClose()
        {
            // 플레이어가 닫을 때까지 묶어 둔다 — 1대1과 같은 까닭(PlayerMovement의 20초 자동 해제·ESC 해제 뒤 탭이 클릭-이동으로 샌다).
            if (playerMovement != null && !playerMovement.IsFrozen) playerMovement.SetFrozen(true);
            resultRealTimer += Time.unscaledDeltaTime;
            bool pressed = wantResultClose || wantMouseClick || BattleResultInput.PressedThisFrame();
            wantResultClose = false;
            if (!resultCloseArmed && BattleResultRules.AcceptsPress(resultRealTimer, pressed))
            {
                resultCloseArmed = true;
                resultArmedAt = resultRealTimer;
            }

            bool released = resultCloseArmed
                && BattleResultRules.ReleaseComplete(BattleResultInput.Held(), resultRealTimer - resultArmedAt);
            if (released || BattleResultRules.ShouldAutoClose(resultRealTimer, sandbox: false))
                EndRaid();
        }

        /// <summary>OnGUI의 KeyDown — 결과 화면에서 Space/Enter를 받는다(스킬 키 Q·W·E·R 등은 건드리지 않는다).</summary>
        private void CaptureResultKey(Event evt)
        {
            if (phase != Phase.Result || evt == null || evt.type != EventType.KeyDown) return;
            if (!BattleResultInput.IsCloseKey(evt.keyCode)) return;
            wantResultClose = true;
            evt.Use();
        }

        // ── 읽는 단계의 배속 하한 ──

        private bool IsGuardianRaid => raidController != null && !string.IsNullOrEmpty(raidController.BossGuardianRegionId);

        /// <summary>단계 시계(<c>phaseTimer</c>) 배율 — 그림자 변신만 <see cref="BattleReadPacing.BossTransformMinSeconds"/> 하한을 둔다.</summary>
        private float PhaseClockScale => phase == Phase.BossTransform
            ? BattleReadPacing.ClockScale(BossTransformDuration, BattleReadPacing.BossTransformMinSeconds, BattlePresentation.Speed)
            : 1f;

        /// <summary>
        /// 인트로 시계(<c>introTimer</c>) 배율 — 수문장 등장(2.6초)은 <see cref="BattleReadPacing.GuardianIntroMinSeconds"/>,
        /// 일반 레이드 인트로(2초)는 <see cref="BattleReadPacing.RaidIntroMinSeconds"/> 하한.
        /// </summary>
        private float IntroClockScale => phase == Phase.Intro
            ? BattleReadPacing.ClockScale(IntroSeconds,
                IsGuardianRaid ? BattleReadPacing.GuardianIntroMinSeconds : BattleReadPacing.RaidIntroMinSeconds,
                BattlePresentation.Speed)
            : 1f;

        /// <summary>
        /// 그림자 변신 연출(아레나 <c>PlayBossTransform</c>)에 넘길 길이(연출 시계 초) — 변신 단계와 <b>같은 실제 시간</b>에 끝난다.
        /// 1배속 1.5초, 2배속 2.0초(실제 1.0초). <see cref="BossTransformDuration"/>을 넘기면 2배속에서 연출이 0.75초에 끝나
        /// 「쿵」 문구(<see cref="BossTransformProgress"/> 0.6)가 울부짖음보다 0.15초 늦다.
        /// </summary>
        public float BossTransformStagingSeconds =>
            BattleReadPacing.StagingSeconds(BossTransformDuration, BattleReadPacing.BossTransformMinSeconds, BattlePresentation.Speed);

        /// <summary>
        /// 수문장 등장 컷(아레나 <c>PlayGuardianIntro</c> — 초 단위 시각표)이 화면 인트로와 같은 박자로 돌려면 연출 시계에 곱할 배율.
        /// 1배속 1, 2배속 0.65(2.6초 → 실제 2.0초). 일반 레이드면 1. 인트로 밖에서도 같은 값을 준다(시작 프레임에 읽어 가도 되게).
        /// </summary>
        public float GuardianIntroClockScale => IsGuardianRaid
            ? BattleReadPacing.ClockScale(BattleStaging.GuardianIntroSeconds, BattleReadPacing.GuardianIntroMinSeconds, BattlePresentation.Speed)
            : 1f;

        /// <summary>인트로 진행률 0~1(레이드 밖이면 0, 인트로가 끝났으면 1).</summary>
        public float IntroProgress => phase == Phase.None ? 0f
            : phase == Phase.Intro ? Mathf.Clamp01(introTimer / IntroSeconds) : 1f;
    }
}
