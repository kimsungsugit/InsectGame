using System;

namespace InsectGame.Opening
{
    /// <summary>
    /// 실시간 시계를 오프닝 진행용 delta로 변환한다.
    /// 로딩 등으로 프레임이 오래 멈춰도 한 프레임에 최대 0.1초만 진행한다.
    /// </summary>
    public sealed class OpeningPlaybackClock
    {
        public const float MaxFrameDelta = 0.1f;

        private double lastRealtime;
        private bool initialized;

        public void Reset(double now)
        {
            if (!IsValidRealtime(now))
            {
                initialized = false;
                lastRealtime = 0d;
                return;
            }

            lastRealtime = now;
            initialized = true;
        }

        public float Consume(double now)
        {
            if (!initialized || !IsValidRealtime(now))
                return 0f;

            if (now < lastRealtime)
            {
                lastRealtime = now;
                return 0f;
            }

            double delta = now - lastRealtime;
            lastRealtime = now;
            if (delta <= 0d)
                return 0f;

            return (float)Math.Min(MaxFrameDelta, delta);
        }

        private static bool IsValidRealtime(double value)
        {
            return value >= 0d && !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    /// <summary>
    /// 시작/복귀 입력이 스킵으로 오인되지 않도록 입력 잠금 해제 뒤 중립 프레임을 요구한다.
    /// </summary>
    public sealed class OpeningSkipInputGate
    {
        private bool armed;

        public bool IsArmed => armed;

        public void Reset()
        {
            armed = false;
        }

        public bool ShouldSkip(bool canSkip, bool isInputHeld, bool inputBegan)
        {
            if (!canSkip)
            {
                armed = false;
                return false;
            }

            if (!armed)
            {
                if (!isInputHeld && !inputBegan)
                    armed = true;

                return false;
            }

            return inputBegan;
        }
    }

    public enum OpeningVisualPhase
    {
        Glow,
        GlowCrossFade,
        Horizon,
        HorizonCrossFade,
        Gathering,
        TitleReveal,
        TitleHold,
        FinalFade,
        Completed
    }

    /// <summary>
    /// Unity 생명주기와 분리된 오프닝 타임라인. 호출자가 unscaled delta를 공급한다.
    ///
    /// <b>20초 프롤로그 영상과 맞물린다.</b> 그림은 영상(<c>StreamingAssets/Video/opening_prologue_*.mp4</c>)이
    /// 그리고, 내레이션·타이틀·건너뛰기는 여기 시각대로 IMGUI가 얹는다. 영상은
    /// <c>Tools/Video/opening_prologue.py</c>, 소리는 <c>Tools/Video/opening_audio.py</c>가 만드는데
    /// 두 스크립트의 사건 시각이 아래 상수와 같다 — 한쪽을 바꾸면 셋을 함께 고친다.
    ///
    /// <code>
    ///  0.0 ~  9.6  그림1 밤숲 길 — 빛이 꺼지고 길 끝에 그물을 든 검은 코트. 8.3~9.4 암전
    /// 10.0         테마곡 시작(음원 안에서 앞 10초는 같은 D조 전주)
    ///  9.8 ~ 15.0  그림2 파트너 — 꺼졌던 빛이 돌아온다. 14.0~15.0 그림3으로 맞춤 컷
    /// 16.0         곡의 강세 = 타이틀
    /// 19.2 ~ 20.0  페이드아웃
    /// </code>
    ///
    /// 영상을 못 틀면(파일 없음·디코더 실패) 같은 시계로 정지 그림 3장을 넘긴다 — 아래 이미지 구간 상수는
    /// 그 대체 경로의 것이다.
    /// </summary>
    public sealed class OpeningSequenceState
    {
        public const float GlowCrossFadeStart = 9.0f;
        public const float HorizonStart = 10f;
        public const float HorizonCrossFadeStart = 14f;
        public const float GatheringStart = 15f;
        public const float TitleStart = 16f;
        public const float TitleHoldStart = 17.4f;
        public const float FinalFadeStart = 19.2f;
        public const float Duration = 20f;

        /// <summary>영상 속 암전 시작 — 마지막 빛이 그물에 거둬지고 화면이 검게 가라앉는다.</summary>
        public const float BlackoutStart = 8.3f;
        /// <summary>암전 뒤 파트너가 떠올라 화면이 다 밝아지는 시각.</summary>
        public const float PartnerRevealEnd = 10.9f;
        public const float SkipUnlockTime = 1f;
        public const float SkipFadeDuration = 0.25f;

        private float elapsed;
        private float skipFadeElapsed;
        private float skipFadeStartAlpha;
        private bool skipping;
        private bool completed;

        public event Action Completed;

        public float Elapsed => elapsed;
        public bool CanSkip => !completed && !skipping && elapsed >= SkipUnlockTime;
        public bool IsSkipping => skipping;
        public bool IsCompleted => completed;
        public bool WasSkipped { get; private set; }

        public OpeningVisualPhase Phase
        {
            get
            {
                if (completed) return OpeningVisualPhase.Completed;
                if (elapsed < GlowCrossFadeStart) return OpeningVisualPhase.Glow;
                if (elapsed < HorizonStart) return OpeningVisualPhase.GlowCrossFade;
                if (elapsed < HorizonCrossFadeStart) return OpeningVisualPhase.Horizon;
                if (elapsed < GatheringStart) return OpeningVisualPhase.HorizonCrossFade;
                if (elapsed < TitleStart) return OpeningVisualPhase.Gathering;
                if (elapsed < TitleHoldStart) return OpeningVisualPhase.TitleReveal;
                if (elapsed < FinalFadeStart) return OpeningVisualPhase.TitleHold;
                return OpeningVisualPhase.FinalFade;
            }
        }

        public int CurrentImageIndex
        {
            get
            {
                if (elapsed < HorizonStart) return 0;
                if (elapsed < GatheringStart) return 1;
                return 2;
            }
        }

        public int NextImageIndex
        {
            get
            {
                if (elapsed >= GlowCrossFadeStart && elapsed < HorizonStart) return 1;
                if (elapsed >= HorizonCrossFadeStart && elapsed < GatheringStart) return 2;
                return -1;
            }
        }

        public float ImageBlend
        {
            get
            {
                if (elapsed >= GlowCrossFadeStart && elapsed < HorizonStart)
                    return InverseLerp(GlowCrossFadeStart, HorizonStart, elapsed);
                if (elapsed >= HorizonCrossFadeStart && elapsed < GatheringStart)
                    return InverseLerp(HorizonCrossFadeStart, GatheringStart, elapsed);
                return 0f;
            }
        }

        public float TitleAlpha
        {
            get
            {
                float value = InverseLerp(TitleStart, TitleHoldStart, elapsed);
                return value * value * (3f - 2f * value);
            }
        }

        /// <summary>
        /// 스토리 내레이션 — 오프닝이 게임 소개("발견하고, 성장시키고")만 하고 <b>무슨 이야기인지는
        /// 한 마디도 안 했다.</b> 처음 켠 사람이 몰입할 근거가 없었다.
        ///
        /// 네 줄이 영상의 네 장면에 하나씩 붙는다: 빛이 꺼진다 → 길 끝의 그물 → 파트너 → 타이틀.
        /// 두 번째 줄은 암전(<see cref="BlackoutStart"/>) 전에 끝나고 세 번째 줄은 파트너가 다 떠오른 뒤
        /// 뜬다 — 검은 화면에 글자만 떠 있으면 영상이 멈춘 것처럼 보인다. 마지막 줄만 타이틀이
        /// 자리잡은 뒤 얹는다(게임의 목적을 말하는 줄이라 타이틀과 함께 남는다).
        /// </summary>
        public const float Narration1Start = 1.0f;
        public const float Narration1End = 4.3f;
        public const float Narration2Start = 4.8f;
        public const float Narration2End = 8.2f;
        public const float Narration3Start = 10.9f;
        public const float Narration3End = 13.9f;
        public const float Narration4Start = 17.6f;
        public const float Narration4End = 19.5f;
        /// <summary>내레이션 줄 수 — <c>OpeningSceneController</c>의 문장 배열 길이와 같아야 한다(테스트가 본다).</summary>
        public const int NarrationCount = 4;
        private const float NarrationFade = 0.45f;

        /// <summary>
        /// 지금 보여줄 내레이션 줄(0~3)과 그 알파. 없으면 index가 -1.
        /// 순수 계산이라 <c>OpeningSequenceTests</c>가 씬 없이 고정한다.
        /// </summary>
        public void GetNarration(out int index, out float alpha)
        {
            if (TryNarration(0, Narration1Start, Narration1End, out index, out alpha)) return;
            if (TryNarration(1, Narration2Start, Narration2End, out index, out alpha)) return;
            if (TryNarration(2, Narration3Start, Narration3End, out index, out alpha)) return;
            if (TryNarration(3, Narration4Start, Narration4End, out index, out alpha)) return;
            index = -1;
            alpha = 0f;
        }

        private bool TryNarration(int slot, float start, float end, out int index, out float alpha)
        {
            index = -1;
            alpha = 0f;
            if (elapsed < start || elapsed >= end) return false;

            float sinceStart = elapsed - start;
            float untilEnd = end - elapsed;
            index = slot;
            alpha = Clamp01(Min(sinceStart, untilEnd) / NarrationFade);
            return true;
        }

        private static float Min(float a, float b) => a < b ? a : b;

        public float FadeAlpha
        {
            get
            {
                if (skipping)
                {
                    float progress = Clamp01(skipFadeElapsed / SkipFadeDuration);
                    return skipFadeStartAlpha + (1f - skipFadeStartAlpha) * progress;
                }
                return InverseLerp(FinalFadeStart, Duration, elapsed);
            }
        }

        public void Advance(float unscaledDeltaTime)
        {
            if (completed || unscaledDeltaTime <= 0f)
                return;

            if (skipping)
            {
                skipFadeElapsed = Math.Min(SkipFadeDuration, skipFadeElapsed + unscaledDeltaTime);
                if (skipFadeElapsed >= SkipFadeDuration)
                    Complete(true);
                return;
            }

            elapsed = Math.Min(Duration, elapsed + unscaledDeltaTime);
            if (elapsed >= Duration)
                Complete(false);
        }

        public bool TrySkip()
        {
            if (!CanSkip)
                return false;

            // 자연 페이드 중 스킵해도 화면과 음량이 다시 밝아지지 않도록
            // 현재 페이드 값을 이어받아 남은 구간만 빠르게 마무리한다.
            skipFadeStartAlpha = InverseLerp(FinalFadeStart, Duration, elapsed);
            skipping = true;
            skipFadeElapsed = 0f;
            return true;
        }

        private void Complete(bool wasSkipped)
        {
            if (completed)
                return;

            completed = true;
            WasSkipped = wasSkipped;
            Action handler = Completed;
            if (handler != null) handler();
        }

        private static float InverseLerp(float from, float to, float value)
        {
            if (to <= from) return 0f;
            return Clamp01((value - from) / (to - from));
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
