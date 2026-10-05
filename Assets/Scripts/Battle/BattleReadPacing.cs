using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>
    /// 전투 화면의 <b>읽는 단계</b>가 배속을 덜 타게 하는 순수 규칙.
    ///
    /// 전투 연출의 시계는 전부 <c>BattlePresentation.DeltaTime</c>(배속 × 히트스톱)이라, 2배속을 켜면 상대 교체(1.2초)·그림자 변신(1.5초)·
    /// 수문장 등장(2.6초)이 절반으로 줄어 아이가 문구(「집게가 지네를 내보냈다!」·「그림자가 ○○의 모습을 빌렸다!」)를 못 읽었다.
    /// 그래서 이 단계들은 <b>배속과 무관한 최소 실제 시간</b>을 갖는다 — 단계 시계를 늦출 뿐 늘리지 않으므로 1배속은 한 프레임도 안 바뀐다.
    /// 공격 연출·턴 배너·HP 보간은 지금처럼 배속을 탄다.
    ///
    /// <b>쓰는 법</b>: 단계 시계(<c>phaseTimer</c>·<c>introTimer</c>)를 <c>DeltaTime × ClockScale(…)</c>로 올린다. 진행률(시계 ÷ 길이)은
    /// 그대로 0→1이라 문구의 페이드·박자가 늘어난 길이 전체에 고르게 퍼진다. 같은 실제 시간에 아레나 연출이 끝나게 하려면
    /// 연출 길이로 <see cref="StagingSeconds"/>를 넘긴다(연출 시계 초 — 아레나 코루틴도 배속 시계를 쓴다).
    ///
    /// 히트스톱(<c>BattlePresentation.TimeScale</c>)은 그대로 곱해진다 — 읽는 단계에는 타격이 없어 실제로는 1이다.
    /// </summary>
    public static class BattleReadPacing
    {
        /// <summary>
        /// 1대1 <b>진입 인트로</b>의 실제 초 — 아레나 진입 샷(visual-dev, 1초 안쪽) + 화면 쓸기 전환과 큰 「야생 ○○이(가) 나타났다!」
        /// (ui-dev)가 들어갈 자리. 이 구간은 <b>배속과 무관하게 실제 시간으로</b> 흐른다(<see cref="IntroClockScale"/>).
        /// 대결·수문장 컷인이 있으면 컷인은 이 구간 <b>뒤에</b> 시작한다(<see cref="IntroSeconds"/>).
        /// </summary>
        public const float EntryIntroSeconds = 1.4f;

        /// <summary>대결·수문장 컷인(1대1, 1배속 3.2초)의 최소 실제 초 — 상대의 첫마디가 타자로 다 찍힌다.</summary>
        public const float CutInMinSeconds = 2.0f;

        /// <summary>상대 교체 단계(<c>BattleScreenUI.EnemySwitchSeconds</c> 1.2초)의 최소 실제 초.</summary>
        public const float EnemySwitchMinSeconds = 1.0f;

        /// <summary>그림자 변신 단계(<c>RaidBattleUI.BossTransformDuration</c> 1.5초)의 최소 실제 초.</summary>
        public const float BossTransformMinSeconds = 1.0f;

        /// <summary>수문장 레이드 등장 인트로(<c>BattleStaging.GuardianIntroSeconds</c> 2.6초)의 최소 실제 초 — 별칭·등장 한 줄을 읽는다.</summary>
        public const float GuardianIntroMinSeconds = 2.0f;

        /// <summary>일반 레이드 인트로(RAID BOSS → 이름 → FIGHT!, 1배속 2초)의 최소 실제 초 — 1대1 진입과 같은 값.</summary>
        public const float RaidIntroMinSeconds = EntryIntroSeconds;

        /// <summary>
        /// 단계 시계 배율(0~1] — 연출 시계(<c>BattlePresentation.DeltaTime</c>)에 곱한다. 1이면 배속 그대로(1배속이거나 하한에 안 걸린다).
        /// <paramref name="nominalSeconds"/> 길이의 단계가 실제로 <paramref name="minRealSeconds"/>보다 빨리 끝나지 않을 만큼만 늦춘다.
        /// </summary>
        /// <param name="nominalSeconds">단계 길이(연출 시계 초 = 1배속 실제 초).</param>
        /// <param name="minRealSeconds">배속과 무관한 최소 실제 초.</param>
        /// <param name="speed">전투 배속(<c>BattlePresentation.Speed</c>, 1 또는 2). 1 미만은 1로 본다.</param>
        public static float ClockScale(float nominalSeconds, float minRealSeconds, float speed)
        {
            if (nominalSeconds <= 0f || minRealSeconds <= 0f) return 1f;
            float s = Mathf.Max(1f, speed);
            return Mathf.Clamp01(nominalSeconds / (minRealSeconds * s));
        }

        /// <summary>그 단계가 실제로 걸리는 초(히트스톱 없음) — <c>max(길이 ÷ 배속, 하한)</c>. 단, 길이가 하한보다 짧으면 하한.</summary>
        public static float RealSeconds(float nominalSeconds, float minRealSeconds, float speed)
        {
            if (nominalSeconds <= 0f) return 0f;
            float s = Mathf.Max(1f, speed);
            return nominalSeconds / (s * ClockScale(nominalSeconds, minRealSeconds, speed));
        }

        /// <summary>
        /// 아레나 연출(배속 시계로 도는 코루틴)에 넘길 길이 — 단계와 <b>같은 실제 시간</b>에 끝난다.
        /// 1배속이면 <paramref name="nominalSeconds"/> 그대로, 2배속이면 하한까지 늘어난 만큼(예: 교체 1.2 → 2.0).
        /// </summary>
        public static float StagingSeconds(float nominalSeconds, float minRealSeconds, float speed)
        {
            float scale = ClockScale(nominalSeconds, minRealSeconds, speed);
            return scale > 0f ? nominalSeconds / scale : nominalSeconds;
        }

        // ── 1대1 인트로 = 진입 구간(실제 시간) + [컷인 | 평범한 인트로의 나머지] ──

        /// <summary>
        /// 1대1 인트로 전체 길이(인트로 시계 초). 컷인이 있으면 진입 구간 <b>뒤에</b> 컷인이 이어지고(1.4 + 3.2),
        /// 없으면 평범한 인트로가 진입 구간을 품는다(더 짧으면 진입 구간 길이).
        /// </summary>
        /// <param name="cutInSeconds">컷인 길이. 컷인이 없는 전투(야생·아이 대결·꿈)면 0.</param>
        /// <param name="plainSeconds">평범한 인트로 길이(컷인이 없을 때).</param>
        public static float IntroSeconds(float cutInSeconds, float plainSeconds)
        {
            return cutInSeconds > 0f
                ? EntryIntroSeconds + cutInSeconds
                : Mathf.Max(EntryIntroSeconds, plainSeconds);
        }

        /// <summary>
        /// 인트로 시계 배율 — 진입 구간(<see cref="EntryIntroSeconds"/> 전)은 실제 시간(배속을 되돌린다), 컷인 구간은
        /// <see cref="CutInMinSeconds"/> 하한, 평범한 인트로의 나머지는 배속 그대로.
        /// </summary>
        /// <param name="elapsed">지금까지의 인트로 시계(초).</param>
        public static float IntroClockScale(float elapsed, float cutInSeconds, float speed)
        {
            if (elapsed < EntryIntroSeconds) return ClockScale(EntryIntroSeconds, EntryIntroSeconds, speed);
            return cutInSeconds > 0f ? ClockScale(cutInSeconds, CutInMinSeconds, speed) : 1f;
        }

        /// <summary>진입 구간 진행률 0~1(인트로 시계 기준 — 이 구간은 실제 시간과 같은 속도로 흐른다).</summary>
        public static float EntryProgress(float introElapsed) => Mathf.Clamp01(introElapsed / EntryIntroSeconds);

        /// <summary>컷인 시계 — 진입 구간이 끝난 뒤부터 0. 진입 구간 중에는 음수다(컷인을 아직 그리지 않는다).</summary>
        public static float CutInElapsed(float introElapsed) => introElapsed - EntryIntroSeconds;
    }

    /// <summary>
    /// 전투 결과 화면을 <b>닫는</b> 순수 규칙 — 1대1(<c>BattleScreenUI</c>)과 레이드(<c>RaidBattleUI</c>)가 함께 쓴다.
    ///
    /// 예전엔 결과 화면이 1대1 4초·레이드 5초 뒤 저절로 닫혀서, 보상·포획 결과·상대의 한마디를 다 읽기 전에 필드로 돌아갔다.
    /// 이제는 <b>탭·클릭·Space/Enter로만</b> 닫힌다. 처음 <see cref="InputLockSeconds"/>는 누름을 받지 않는다(쌓아 두지도 않는다) —
    /// 마지막 공격을 연타하던 손가락이 결과를 곧바로 닫지 않게.
    ///
    /// <b>받아들인 누름은 손을 뗄 때 닫는다</b>(<see cref="ReleaseComplete"/>). 누른 그 프레임에 닫으면 화면이 플레이어 프리즈를 풀고,
    /// 같은 프레임의 <c>PlayerMovement</c>가 그 탭(<c>GetMouseButtonDown</c>)을 클릭-이동으로 읽어 캐릭터가 탭한 자리로 걸어갔다
    /// (rules/ui-layout.md 「필드 위에 그리는 버튼」과 같은 계열). 뗀 프레임엔 그 판정이 거짓이다.
    ///
    /// <b>예외 하나 — 꿈 챔피언전(샌드박스)은 예전처럼 저절로 닫힌다.</b> 꿈 지휘자(<c>DreamPrologueDirector</c>)가 결과 화면이
    /// 닫히는 것(<c>IsBattleActive</c>)을 신호로 섬으로 넘어가는데, 처음 켠 사람은 "눌러서 닫기"를 아직 모르고, 그 지휘자의 상한
    /// (14초)이 먼저 오면 결과 화면이 떠 있는 채로 섬이 열린다. 탭하면 더 일찍 닫힌다.
    /// </summary>
    public static class BattleResultRules
    {
        /// <summary>결과가 뜬 뒤 이 실제 초 동안은 누름을 받지 않는다.</summary>
        public const float InputLockSeconds = 0.6f;

        /// <summary>받아들인 누름을 이만큼(실제 초) 계속 누르고 있으면 떼지 않아도 닫는다 — 포커스를 잃어 뗌을 못 받아도 갇히지 않게.</summary>
        public const float ReleaseGraceSeconds = 0.5f;

        /// <summary>꿈 챔피언전 결과가 저절로 닫히는 실제 초(예전 1대1 자동 닫힘과 같다).</summary>
        public const float SandboxAutoCloseSeconds = 4f;

        /// <summary>지금 누르면 받아들이는가(잠금이 끝났는가).</summary>
        public static bool CanClose(float shownSeconds) => shownSeconds >= InputLockSeconds;

        /// <summary>이번 프레임의 누름을 받아들이는가 — 잠금 중 누름은 버린다(잠금이 풀린 뒤 다시 눌러야 한다).</summary>
        public static bool AcceptsPress(float shownSeconds, bool pressed) => pressed && CanClose(shownSeconds);

        /// <summary>받아들인 누름 뒤 — 손을 뗐거나(<paramref name="inputHeld"/> 거짓) 충분히 오래 눌렀으면 닫는다.</summary>
        public static bool ReleaseComplete(bool inputHeld, float secondsSincePress)
            => !inputHeld || secondsSincePress >= ReleaseGraceSeconds;

        /// <summary>누르지 않아도 닫는가 — 꿈 챔피언전(샌드박스)만. 다른 전투는 몇 초가 지나도 false.</summary>
        public static bool ShouldAutoClose(float shownSeconds, bool sandbox)
            => sandbox && shownSeconds > SandboxAutoCloseSeconds;
    }
}
