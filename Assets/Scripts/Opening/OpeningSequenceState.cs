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
    /// </summary>
    public sealed class OpeningSequenceState
    {
        public const float GlowCrossFadeStart = 2.4f;
        public const float HorizonStart = 3f;
        public const float HorizonCrossFadeStart = 5.2f;
        public const float GatheringStart = 5.8f;
        public const float TitleStart = 6.2f;
        public const float TitleHoldStart = 8f;
        public const float FinalFadeStart = 9.2f;
        public const float Duration = 10f;
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
