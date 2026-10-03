using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 섬 방목 곤충의 <b>기분</b> — 시간대·날씨 성향(<see cref="InsectHabits"/>)을 활동도 0~1로 바꾸고, 활동도를 배회의 숫자
    /// (쉬는 시간·걷는 속도·나는 높이)로 바꾸는 순수 규칙. <see cref="IslandInsectWalker"/>가 읽는다. 씬 없이 돌아 테스트가 고정한다.
    ///
    /// <b>몸의 위치·속도·쉬는 시간만</b> 바꾼다 — 날갯짓 같은 모델 애니메이션은 visual-dev 영역이라 건드리지 않는다.
    /// 섬은 지역이 없어 세계 날씨(<c>WorldStateProvider.GetWorldState(null)</c>)로 잰다. 꿈속 섬은 늘 맑은 한낮이라 자연히 낮의 기분이다.
    ///
    /// <b>섬 수입과는 무관하다</b>(사용자 결정 — 섬 생산 보너스 없음). 기분은 보이는 모습일 뿐 <c>IslandYield</c>를 건드리지 않는다.
    /// </summary>
    public static class IslandInsectMood
    {
        // ── 활동도 ──
        // 제 시간(야행성의 밤·주행성의 아침·낮)이면 활발, 잠들 시간이면 대부분 쉬고, 어스름·시간 무관이면 보통이다.
        // 날씨는 좋아하면 조금 오르고 싫어하면 더 내려간다 — "싫은 날씨엔 덜 움직인다"가 "좋은 날씨엔 활발"보다 눈에 잘 띄게.

        public const float LivelyActivity = 1.0f;
        public const float NeutralActivity = 0.6f;
        public const float SleepyActivity = 0.2f;
        public const float LikedWeatherBonus = 0.15f;
        public const float DislikedWeatherPenalty = 0.25f;

        // ── 활동도 → 배회. 세 기준점(졸림 0.2 · 보통 0.6 · 활발 1.0) 사이를 선형으로 잇는다 ──
        // 보통(0.6)의 값은 성향을 넣기 전의 배회 그대로다(쉬기 1.2~4.5초 · 속도 ×1 · 나는 높이 ×1) — 시간·날씨를 안 타는 종은 예전과 같다.

        public const float SleepyRestMin = 5f, NeutralRestMin = 1.2f, LivelyRestMin = 0.6f;
        public const float SleepyRestMax = 12f, NeutralRestMax = 4.5f, LivelyRestMax = 2f;
        public const float SleepySpeed = 0.55f, NeutralSpeed = 1f, LivelySpeed = 1.3f;

        /// <summary>나는 곤충의 높이 배수 — 졸리면 땅에 낮게 내려앉는다(0.25 × 0.9m ≈ 0.22m).</summary>
        public const float SleepyHover = 0.25f, NeutralHover = 1f, LivelyHover = 1.1f;

        /// <summary>배회의 숫자들.</summary>
        public readonly struct Mood
        {
            public readonly float Activity;
            public readonly float RestMin;
            public readonly float RestMax;
            public readonly float SpeedMultiplier;
            public readonly float HoverMultiplier;

            public Mood(float activity, float restMin, float restMax, float speed, float hover)
            {
                Activity = activity;
                RestMin = restMin;
                RestMax = restMax;
                SpeedMultiplier = speed;
                HoverMultiplier = hover;
            }
        }

        /// <summary>성향도 상태도 모를 때 — 예전 배회 그대로.</summary>
        public static Mood Neutral => FromActivity(NeutralActivity);

        /// <summary>이 종이 이 상태에서 얼마나 활발한가(0~1).</summary>
        public static float Activity(InsectHabit habit, WorldState state)
        {
            int timeFit = InsectHabits.TimeFit(habit, state.DayPhase);
            float a = timeFit > 0 ? LivelyActivity : timeFit < 0 ? SleepyActivity : NeutralActivity;
            int weatherFit = InsectHabits.WeatherFit(habit, state.Weather);
            if (weatherFit > 0) a += LikedWeatherBonus;
            else if (weatherFit < 0) a -= DislikedWeatherPenalty;
            return Mathf.Clamp01(a);
        }

        /// <summary>이 종이 이 상태에서 섬을 어떻게 돌아다니나.</summary>
        public static Mood For(InsectHabit habit, WorldState state) => FromActivity(Activity(habit, state));

        /// <summary>활동도 → 배회 숫자. 기준점 바깥(0.2 아래·1.0)은 끝값으로 가둔다.</summary>
        public static Mood FromActivity(float activity)
        {
            float a = Mathf.Clamp01(activity);
            return new Mood(a,
                Blend(a, SleepyRestMin, NeutralRestMin, LivelyRestMin),
                Blend(a, SleepyRestMax, NeutralRestMax, LivelyRestMax),
                Blend(a, SleepySpeed, NeutralSpeed, LivelySpeed),
                Blend(a, SleepyHover, NeutralHover, LivelyHover));
        }

        private static float Blend(float a, float sleepy, float neutral, float lively)
        {
            if (a <= NeutralActivity)
                return Mathf.Lerp(sleepy, neutral, Mathf.InverseLerp(SleepyActivity, NeutralActivity, a));
            return Mathf.Lerp(neutral, lively, Mathf.InverseLerp(NeutralActivity, LivelyActivity, a));
        }
    }
}
