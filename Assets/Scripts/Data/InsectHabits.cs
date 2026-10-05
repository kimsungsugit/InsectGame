using System;
using System.Collections.Generic;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.Data
{
    /// <summary>활동 시간대 — 언제 깨어 있고 언제 잠드는가.</summary>
    public enum InsectActivity : byte
    {
        /// <summary>시간을 안 탄다(저녁·새벽에 도는 종, 땅속·물속 종, 멈춘 땅의 종).</summary>
        Any = 0,
        /// <summary>주행성 — 아침·낮에 많고 밤에 잠든다.</summary>
        Diurnal = 1,
        /// <summary>야행성 — 밤에 많고 낮에 잠든다.</summary>
        Nocturnal = 2
    }

    /// <summary>기질 — 사람을 보고 달아나기만 하는가, 먼저 싸움을 거는가.</summary>
    public enum InsectTemperament : byte
    {
        Docile = 0,
        /// <summary>습격형 — 깨어 있는 때(<see cref="InsectHabits.IsAmbusher"/>) 먼저 싸움을 건다.</summary>
        Ambusher = 1
    }

    /// <summary>
    /// 날씨 집합 — 비트 하나가 <see cref="WeatherType"/> 값 하나다(<c>1 &lt;&lt; (int)weather</c>).
    /// <see cref="WeatherType"/>은 뒤에만 덧붙이므로(눈이 그랬다) 이 비트도 밀리지 않는다 — 테스트가 정렬을 고정한다.
    /// </summary>
    [Flags]
    public enum WeatherSet : byte
    {
        None = 0,
        Clear = 1 << 0,
        Rain = 1 << 1,
        Fog = 1 << 2,
        Wind = 1 << 3,
        Snow = 1 << 4
    }

    /// <summary>한 종의 성향 — 시간대·날씨·기질. 값 타입이라 조회에 할당이 없다.</summary>
    public readonly struct InsectHabit
    {
        public readonly InsectActivity Activity;
        public readonly WeatherSet LikedWeather;
        public readonly WeatherSet DislikedWeather;
        public readonly InsectTemperament Temperament;

        public InsectHabit(InsectActivity activity, WeatherSet likedWeather, WeatherSet dislikedWeather,
            InsectTemperament temperament)
        {
            Activity = activity;
            LikedWeather = likedWeather;
            DislikedWeather = dislikedWeather;
            Temperament = temperament;
        }

        /// <summary>시간도 날씨도 안 타는 온순한 종 — 표에 없는 종(앞으로 늘어날 종)의 기본값이다.</summary>
        public static readonly InsectHabit Neutral =
            new InsectHabit(InsectActivity.Any, WeatherSet.None, WeatherSet.None, InsectTemperament.Docile);
    }

    /// <summary>
    /// 곤충별 시간·날씨 성향의 <b>순수 규칙</b> — 표(<c>InsectHabitTable.cs</c>)와 그 표를 읽는 함수가 여기 한 곳이다.
    /// 스폰(<c>InsectSpawner</c>)·전투 보정·습격이 같은 답을 읽는다. 씬 없이 도는 정적 클래스라 테스트가 전부 고정한다.
    ///
    /// <b>세 축이다.</b> ①활동 시간대(주행성·야행성·무관) ②날씨 취향(좋아하는 날씨 집합·싫어하는 날씨 집합)
    /// ③기질(온순·습격형). 시간과 날씨를 합친 <see cref="Fit"/>(−1 불리 / 0 보통 / +1 유리)이 전투 보정과 습격 판정을,
    /// 곱 배수(<see cref="SpawnWeightMultiplier"/>)가 필드 종 고르기를 정한다.
    ///
    /// <b>스폰에서의 자리.</b> 등급은 전역 표로 먼저 굴린다(<c>FieldSpawnRules.PickRarity</c>) — 이 배수는 그 등급 안에서
    /// 종을 고를 때만 곱해진다. 그래서 등급 분포는 시간·날씨와 무관하고, <b>배수는 0이 되지 않아</b>(<see cref="MinSpawnMultiplier"/>)
    /// 풀의 어떤 종도 어떤 시간·날씨에서 후보에서 빠지지 않는다. 표에 없는 종은 <see cref="InsectHabit.Neutral"/>이다.
    ///
    /// 시간대 경계는 <see cref="GameClock.PhaseOfHour"/>다 — 아침 6~11 · 낮 11~17 · 저녁 17~21 · 밤 21~6.
    /// </summary>
    public static partial class InsectHabits
    {
        // ── 시간대 배수 — 필드 종 고르기 가중에 곱한다 ──
        //
        // 야행성은 밤에 ×2.5, 낮에 ×0.35(사용자 요청: 밤 ×2~2.5 · 낮 ×0.3~0.4). 아침은 잠들러 가는 때라 ×0.6, 저녁은 깨어나기 전이라 ×1.
        // 주행성은 그 거울이다 — 낮 ×1.8 · 아침 ×1.6 · 저녁 ×1 · 밤 ×0.4. 밤이 하루의 3/8(9시간)이라 주행성의 밤 배수를
        // 야행성의 낮 배수만큼 깎지는 않았다(주행성 종이 밤의 3/8 동안 거의 사라지면 필드가 휑해진다).
        public const float NocturnalNightMultiplier = 2.5f;
        public const float NocturnalDayMultiplier = 0.35f;
        public const float NocturnalMorningMultiplier = 0.6f;
        public const float NocturnalEveningMultiplier = 1.0f;
        public const float DiurnalDayMultiplier = 1.8f;
        public const float DiurnalMorningMultiplier = 1.6f;
        public const float DiurnalEveningMultiplier = 1.0f;
        public const float DiurnalNightMultiplier = 0.4f;

        // ── 날씨 배수 ──

        /// <summary>좋아하는 날씨의 종 가중 배수.</summary>
        public const float FavorableWeatherMultiplier = 1.5f;

        /// <summary>싫어하는 날씨의 종 가중 배수.</summary>
        public const float UnfavorableWeatherMultiplier = 0.6f;

        // ── 합친 배수의 범위 ──
        //
        // 시간 × 날씨의 이론 범위는 0.21(야행성의 낮 + 싫은 날씨) ~ 3.75(야행성의 밤 + 좋은 날씨)다. 아래 상·하한은
        // 상수를 고치다 범위를 넘는 걸 막는 안전망이다 — <b>하한은 0이 아니다</b>. 0이면 그 종이 후보에서 빠지고, 풀의 한 등급이
        // 통째로 비면 등급 대체가 돌아 등급 분포가 시간대를 따라 움직인다(전역 등급표가 깨진다).
        public const float MinSpawnMultiplier = 0.15f;
        public const float MaxSpawnMultiplier = 4.0f;

        // ── 전투 보정(다음 단계가 소비) ──

        /// <summary>유리한 상태(<see cref="Fit"/> +1)의 능력치 배수 — +10%.</summary>
        public const float FavorableBattleMultiplier = 1.10f;

        /// <summary>불리한 상태(<see cref="Fit"/> −1)의 능력치 배수 — −10%.</summary>
        public const float UnfavorableBattleMultiplier = 0.90f;

        private const float FitEpsilon = 1e-4f;

        // 표는 InsectHabitTable.cs가 채운다(저작 데이터와 규칙을 갈라 두었다).
        private static readonly Dictionary<string, InsectHabit> table = BuildTable();

        // 표에 적은 줄 수 — 같은 종을 두 줄 적으면 뒤 줄이 앞 줄을 덮는다(조용히). 테스트가 Count와 비교한다.
        private static int authoredRows;

        /// <summary>표에 성향이 적힌 종의 수.</summary>
        public static int Count => table.Count;

        /// <summary>표에 적은 줄 수 — <see cref="Count"/>보다 크면 같은 종이 두 줄 적혔다.</summary>
        internal static int AuthoredRows => authoredRows;

        /// <summary>표에 성향이 적힌 종 ID 전부(테스트·검증용).</summary>
        public static IEnumerable<string> AllIds => table.Keys;

        /// <summary>표에 있으면 그 종의 성향.</summary>
        public static bool TryGet(string insectId, out InsectHabit habit)
        {
            habit = InsectHabit.Neutral;
            return !string.IsNullOrEmpty(insectId) && table.TryGetValue(insectId, out habit);
        }

        /// <summary>종 ID의 성향. 표에 없는 종은 <see cref="InsectHabit.Neutral"/>이다 — 새로 늘린 종이 필드에서 사라지지 않는다.</summary>
        public static InsectHabit Of(string insectId)
        {
            return TryGet(insectId, out InsectHabit habit) ? habit : InsectHabit.Neutral;
        }

        /// <summary>종 데이터의 성향(null이면 중립).</summary>
        public static InsectHabit For(InsectData data)
        {
            return data != null ? Of(data.insectId) : InsectHabit.Neutral;
        }

        /// <summary>이 날씨가 날씨 집합에 들어 있는가.</summary>
        public static bool Contains(WeatherSet set, WeatherType weather)
        {
            return (set & SetOf(weather)) != 0;
        }

        /// <summary>날씨 하나만 든 집합 — 비트 위치가 <see cref="WeatherType"/> 값이다.</summary>
        public static WeatherSet SetOf(WeatherType weather)
        {
            int bit = (int)weather;
            return bit >= 0 && bit < 8 ? (WeatherSet)(1 << bit) : WeatherSet.None;
        }

        // ── 시간대 ──

        /// <summary>시간대가 이 활동형 종의 가중에 곱하는 배수(0.35~2.5).</summary>
        public static float TimeMultiplier(InsectActivity activity, DayPhase phase)
        {
            switch (activity)
            {
                case InsectActivity.Nocturnal:
                    switch (phase)
                    {
                        case DayPhase.Night: return NocturnalNightMultiplier;
                        case DayPhase.Morning: return NocturnalMorningMultiplier;
                        case DayPhase.Day: return NocturnalDayMultiplier;
                        default: return NocturnalEveningMultiplier;
                    }
                case InsectActivity.Diurnal:
                    switch (phase)
                    {
                        case DayPhase.Day: return DiurnalDayMultiplier;
                        case DayPhase.Morning: return DiurnalMorningMultiplier;
                        case DayPhase.Evening: return DiurnalEveningMultiplier;
                        default: return DiurnalNightMultiplier;
                    }
                default:
                    return 1f;
            }
        }

        /// <summary>시간대 궁합 — +1(제 시간) / 0(어스름·시간 무관) / −1(잠잘 때). 배수가 1보다 큰가 작은가로 정한다.</summary>
        public static int TimeFit(InsectHabit habit, DayPhase phase)
        {
            float m = TimeMultiplier(habit.Activity, phase);
            if (m > 1f + FitEpsilon) return 1;
            if (m < 1f - FitEpsilon) return -1;
            return 0;
        }

        /// <summary>
        /// 새 시간대가 이 종에게 <b>어울리지 않는가</b>(잠들 시간) — 시간대가 바뀔 때 스포너가 이런 개체의 만료를 앞당겨
        /// 새 시간대에 맞는 종으로 갈아입힌다(<c>InsectSpawner.OnDayPhaseChanged</c>). 어스름·시간 무관은 해당 없다.
        /// </summary>
        public static bool IsOutOfPhase(InsectHabit habit, DayPhase phase)
        {
            return TimeFit(habit, phase) < 0;
        }

        // ── 날씨 ──

        /// <summary>날씨 궁합 — +1(좋아함) / −1(싫어함) / 0. 두 집합에 다 들어 있으면 서로 지워 0이다(표는 겹치지 않게 적는다).</summary>
        public static int WeatherFit(InsectHabit habit, WeatherType weather)
        {
            int fit = 0;
            if (Contains(habit.LikedWeather, weather)) fit++;
            if (Contains(habit.DislikedWeather, weather)) fit--;
            return fit;
        }

        /// <summary>날씨가 이 종의 가중에 곱하는 배수 — 좋아하면 ×1.5, 싫어하면 ×0.6.</summary>
        public static float WeatherMultiplier(InsectHabit habit, WeatherType weather)
        {
            int fit = WeatherFit(habit, weather);
            if (fit > 0) return FavorableWeatherMultiplier;
            if (fit < 0) return UnfavorableWeatherMultiplier;
            return 1f;
        }

        // ── 합친 판정 ──

        /// <summary>
        /// 시간대와 날씨를 합친 궁합 — −1 불리 / 0 보통 / +1 유리. 둘을 더해 −1~+1로 가둔다: 제 시간에 싫은 날씨를 만나면
        /// 0(보통), 둘 다 좋으면 +1, 둘 다 나쁘면 −1이다. 전투 보정과 습격 판정이 이 값을 읽는다.
        /// </summary>
        public static int Fit(InsectHabit habit, WorldState state)
        {
            return Mathf.Clamp(TimeFit(habit, state.DayPhase) + WeatherFit(habit, state.Weather), -1, 1);
        }

        /// <summary>
        /// 필드 종 고르기의 가중 배수 — 시간대 배수 × 날씨 배수를 [<see cref="MinSpawnMultiplier"/>, <see cref="MaxSpawnMultiplier"/>]로 가둔다.
        /// <b>절대 0이 되지 않는다.</b> 같은 등급 안의 상대 비율만 바꾸고 등급표는 건드리지 않는다.
        /// </summary>
        public static float SpawnWeightMultiplier(InsectHabit habit, WorldState state)
        {
            float m = TimeMultiplier(habit.Activity, state.DayPhase) * WeatherMultiplier(habit, state.Weather);
            return Mathf.Clamp(m, MinSpawnMultiplier, MaxSpawnMultiplier);
        }

        /// <summary>
        /// 전투 능력치 배수(<c>BattleEnvironment</c>가 소비) — 유리하면 +10%, 불리하면 −10%.
        /// 시간과 날씨가 서로 지울 수 있어(<see cref="Fit"/>) 어중간한 상태에서는 1이다.
        /// </summary>
        public static float BattleStatMultiplier(InsectHabit habit, WorldState state)
        {
            int fit = Fit(habit, state);
            if (fit > 0) return FavorableBattleMultiplier;
            if (fit < 0) return UnfavorableBattleMultiplier;
            return 1f;
        }

        /// <summary>
        /// 이 상태에서 습격형이 <b>깨어 있는가</b> — 습격(<c>AmbushRules</c>)이 읽는다. <b>어두울 때만</b>(<see cref="IsAmbushHour"/>),
        /// 그리고 궁합이 유리할 때다. 그래서 야행성 습격형은 밤에(싫어하는 날씨가 아니면), 저녁에는 좋아하는 날씨일 때만 덤벼들고,
        /// 주행성 습격형(말벌류)은 해 질 녘에 좋아하는 날씨일 때만 덤벼든다. 아침·낮에는 아무도 싸움을 걸지 않는다.
        ///
        /// 사용자 요청이 "<b>밤에</b> 싸움을 거는 곤충"이다. 처음엔 시간대 제한 없이 궁합만 봐서 주행성 말벌·사마귀가 한낮에 깨었고,
        /// 맑은 낮(필드 한 칸의 4.6%)이 맑은 밤(4.1%)만큼 사나웠다 — 정원·숲은 오히려 낮이 더 사나웠다(2026-10-03).
        /// 온순한 종은 언제나 false다.
        /// </summary>
        public static bool IsAmbusher(InsectHabit habit, WorldState state)
        {
            return habit.Temperament == InsectTemperament.Ambusher && IsAmbushHour(state.DayPhase) && Fit(habit, state) > 0;
        }

        /// <summary>습격이 일어날 수 있는 시간대 — 저녁(17~21시)과 밤(21~6시).</summary>
        public static bool IsAmbushHour(DayPhase phase) => phase == DayPhase.Evening || phase == DayPhase.Night;
    }
}
