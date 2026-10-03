using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 날씨 예보의 <b>순수 규칙</b> — 다음 날씨·지속 시간·지역 기후. <see cref="WeatherSystem"/>은 이 답을 시계에 옮길 뿐이다.
    /// 난수는 밖에서 받아(<c>roll01</c>) 테스트가 분포를 고정한다.
    ///
    /// <b>날씨는 세계에 하나지만 지역마다 다르게 보인다</b>(<see cref="EffectiveIn"/>). 설산(frostline·mountain)에서는 비가
    /// 눈이 되고, 사막·화산(dunes·emberfall)에서는 눈이 센바람이 된다. 스폰·조명·전투가 지역 기준 날씨를 읽는다.
    /// </summary>
    public static class WeatherForecast
    {
        // ── 등급표 — 합이 1이 아니어도 된다(정규화) ──
        public const float ClearShare = 0.46f;
        public const float RainShare = 0.20f;
        public const float FogShare = 0.13f;
        public const float WindShare = 0.09f;
        public const float SnowShare = 0.12f;

        /// <summary>게임을 켠 뒤 처음 이 시간(초)은 맑다 — 튜토리얼 첫 장면이 비·안개에 묻히지 않게.</summary>
        public const float FirstClearSeconds = 180f;

        /// <summary>날씨가 바뀔 때 하늘·안개·입자가 옮겨 가는 시간(초).</summary>
        public const float TransitionSeconds = 10f;

        /// <summary>맑은 날씨의 지속(초, 무작위). 게임 하루가 12분이라 하루에 두세 번 바뀐다.</summary>
        public const float ClearDurationMin = 150f;
        public const float ClearDurationMax = 330f;

        /// <summary>궂은 날씨의 지속(초, 무작위) — 맑음보다 짧다.</summary>
        public const float BadDurationMin = 90f;
        public const float BadDurationMax = 200f;

        /// <summary>궂은 날씨 다음에 맑음이 나올 몫을 키우는 배수 — 궂은 날이 줄줄이 이어지지 않게.</summary>
        public const float ClearAfterBadBoost = 1.5f;

        internal const int WeatherCount = 5;

        internal static float Share(WeatherType weather)
        {
            switch (weather)
            {
                case WeatherType.Clear: return ClearShare;
                case WeatherType.Rain: return RainShare;
                case WeatherType.Fog: return FogShare;
                case WeatherType.Wind: return WindShare;
                case WeatherType.Snow: return SnowShare;
                default: return 0f;
            }
        }

        /// <summary>
        /// 다음 날씨. <b>같은 날씨가 연달아 나오지 않는다</b>(현재를 뺀 표에서 굴린다). 궂은 날씨 뒤에는 맑음의 몫이 커진다.
        /// </summary>
        public static WeatherType Next(WeatherType current, float roll01)
        {
            float total = 0f;
            for (int i = 0; i < WeatherCount; i++) total += NextShare(current, (WeatherType)i);
            if (total <= 0f) return WeatherType.Clear;

            float target = Mathf.Clamp01(roll01) * total;
            float acc = 0f;
            WeatherType last = WeatherType.Clear;
            for (int i = 0; i < WeatherCount; i++)
            {
                WeatherType w = (WeatherType)i;
                float s = NextShare(current, w);
                if (s <= 0f) continue;
                last = w;
                acc += s;
                if (target < acc) return w;
            }
            return last;
        }

        private static float NextShare(WeatherType current, WeatherType candidate)
        {
            if (candidate == current) return 0f;
            float s = Share(candidate);
            if (candidate == WeatherType.Clear && current != WeatherType.Clear) s *= ClearAfterBadBoost;
            return s;
        }

        /// <summary>이 날씨가 지속되는 시간(초).</summary>
        public static float RollDuration(WeatherType weather, float roll01)
        {
            float t = Mathf.Clamp01(roll01);
            return weather == WeatherType.Clear
                ? Mathf.Lerp(ClearDurationMin, ClearDurationMax, t)
                : Mathf.Lerp(BadDurationMin, BadDurationMax, t);
        }

        // ── 지역 기후 ──

        /// <summary>추운 지역 — 비가 눈이 된다.</summary>
        public static bool IsColdRegion(string regionId)
            => regionId == "frostline" || regionId == "mountain";

        /// <summary>더운 지역 — 눈이 오지 않는다(센바람으로 바뀐다).</summary>
        public static bool IsWarmRegion(string regionId)
            => regionId == "dunes" || regionId == "emberfall";

        /// <summary>
        /// 이 지역에서 실제로 보이는 날씨. 지역을 모르면(<c>null</c>·길 위) 세계 날씨 그대로다.
        /// 추운 곳 비 → 눈, 더운 곳 눈 → 센바람. 맑음·안개·바람은 어디서나 그대로다.
        /// </summary>
        public static WeatherType EffectiveIn(WeatherType global, string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return global;
            if (global == WeatherType.Rain && IsColdRegion(regionId)) return WeatherType.Snow;
            if (global == WeatherType.Snow && IsWarmRegion(regionId)) return WeatherType.Wind;
            return global;
        }

        /// <summary>화면에 쓰는 이름.</summary>
        public static string DisplayName(WeatherType weather)
        {
            switch (weather)
            {
                case WeatherType.Rain: return "비";
                case WeatherType.Fog: return "안개";
                case WeatherType.Wind: return "센바람";
                case WeatherType.Snow: return "눈";
                default: return "맑음";
            }
        }

        /// <summary>화면에 쓰는 시간대 이름.</summary>
        public static string DisplayName(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Morning: return "아침";
                case DayPhase.Day: return "낮";
                case DayPhase.Evening: return "저녁";
                default: return "밤";
            }
        }
    }
}
