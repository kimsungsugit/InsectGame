#if UNITY_EDITOR
using System;
using InsectGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 날씨 예보(<see cref="WeatherForecast"/>)와 시계 시간대(<see cref="GameClock.PhaseOfHour"/>) — 순수 규칙.
    /// 난수는 0~1을 고르게 훑어 넣는다(결정적).
    /// </summary>
    [TestFixture]
    public class WeatherForecastTests
    {
        private const int Sweep = 20000;

        private static WeatherType[] AllWeather()
            => (WeatherType[])Enum.GetValues(typeof(WeatherType));

        [Test]
        public void WeatherType_Snow_IsAppendedAfterTheOriginalFour()
        {
            // InsectSpawnCondition.allowedWeather가 정수로 직렬화된다 — 앞 값이 밀리면 저장된 조건이 다른 날씨를 가리킨다.
            Assert.AreEqual(0, (int)WeatherType.Clear);
            Assert.AreEqual(1, (int)WeatherType.Rain);
            Assert.AreEqual(2, (int)WeatherType.Fog);
            Assert.AreEqual(3, (int)WeatherType.Wind);
            Assert.AreEqual(4, (int)WeatherType.Snow);
            Assert.AreEqual(WeatherForecast.WeatherCount, AllWeather().Length, "예보 표가 모든 날씨를 센다");
        }

        [Test]
        public void Next_NeverRepeatsTheCurrentWeather()
        {
            foreach (WeatherType current in AllWeather())
                for (int i = 0; i < Sweep; i++)
                    Assert.AreNotEqual(current, WeatherForecast.Next(current, (i + 0.5f) / Sweep),
                        $"{current} 다음에 같은 날씨");
        }

        [Test]
        public void Next_ReachesEveryOtherWeather()
        {
            foreach (WeatherType current in AllWeather())
            {
                var seen = new bool[WeatherForecast.WeatherCount];
                for (int i = 0; i < Sweep; i++) seen[(int)WeatherForecast.Next(current, (i + 0.5f) / Sweep)] = true;
                foreach (WeatherType w in AllWeather())
                    if (w != current) Assert.IsTrue(seen[(int)w], $"{current} 다음에 {w}가 영영 안 나온다");
            }
        }

        [Test]
        public void Next_AfterBadWeather_FavorsClear()
        {
            // 궂은 날씨 뒤의 맑음 몫이 키워져 있어야 줄줄이 이어지지 않는다 — 같은 표로 맑음을 키우지 않은 값보다 크다.
            int clear = 0;
            for (int i = 0; i < Sweep; i++)
                if (WeatherForecast.Next(WeatherType.Rain, (i + 0.5f) / Sweep) == WeatherType.Clear) clear++;

            float others = WeatherForecast.FogShare + WeatherForecast.WindShare + WeatherForecast.SnowShare;
            float plain = WeatherForecast.ClearShare / (WeatherForecast.ClearShare + others);
            Assert.Greater(clear / (float)Sweep, plain + 0.02f);
        }

        [Test]
        public void Next_RollExtremes_StayInsideTheTable()
        {
            foreach (WeatherType current in AllWeather())
            {
                Assert.AreNotEqual(current, WeatherForecast.Next(current, 0f));
                Assert.AreNotEqual(current, WeatherForecast.Next(current, 1f));
                Assert.AreNotEqual(current, WeatherForecast.Next(current, -3f), "범위 밖 난수는 clamp");
                Assert.AreNotEqual(current, WeatherForecast.Next(current, 9f));
            }
        }

        [Test]
        public void RollDuration_ClearIsLongerThanBadWeather_AndStaysInBand()
        {
            Assert.AreEqual(WeatherForecast.ClearDurationMin, WeatherForecast.RollDuration(WeatherType.Clear, 0f), 0.001f);
            Assert.AreEqual(WeatherForecast.ClearDurationMax, WeatherForecast.RollDuration(WeatherType.Clear, 1f), 0.001f);
            foreach (WeatherType w in AllWeather())
            {
                if (w == WeatherType.Clear) continue;
                Assert.AreEqual(WeatherForecast.BadDurationMin, WeatherForecast.RollDuration(w, 0f), 0.001f);
                Assert.AreEqual(WeatherForecast.BadDurationMax, WeatherForecast.RollDuration(w, 1f), 0.001f);
            }
            Assert.Less(WeatherForecast.BadDurationMax, WeatherForecast.ClearDurationMax, "궂은 날씨가 맑음보다 짧다");
        }

        [Test]
        public void Transition_FitsInsideTheShortestWeather()
        {
            // 전환이 끝나기 전에 다음 날씨로 넘어가면 하늘이 한 번도 제 모습이 못 된다.
            Assert.Less(WeatherForecast.TransitionSeconds * 3f, WeatherForecast.BadDurationMin);
        }

        [TestCase("frostline")]
        [TestCase("mountain")]
        public void EffectiveIn_ColdRegion_TurnsRainIntoSnow(string region)
        {
            Assert.AreEqual(WeatherType.Snow, WeatherForecast.EffectiveIn(WeatherType.Rain, region));
            Assert.AreEqual(WeatherType.Snow, WeatherForecast.EffectiveIn(WeatherType.Snow, region));
            Assert.AreEqual(WeatherType.Clear, WeatherForecast.EffectiveIn(WeatherType.Clear, region));
            Assert.AreEqual(WeatherType.Fog, WeatherForecast.EffectiveIn(WeatherType.Fog, region));
            Assert.AreEqual(WeatherType.Wind, WeatherForecast.EffectiveIn(WeatherType.Wind, region));
        }

        [TestCase("dunes")]
        [TestCase("emberfall")]
        public void EffectiveIn_WarmRegion_NeverSnows(string region)
        {
            Assert.AreEqual(WeatherType.Wind, WeatherForecast.EffectiveIn(WeatherType.Snow, region));
            Assert.AreEqual(WeatherType.Rain, WeatherForecast.EffectiveIn(WeatherType.Rain, region), "비는 그대로");
        }

        [TestCase("meadow")]
        [TestCase("pond")]
        [TestCase("forest")]
        [TestCase("swamp")]
        [TestCase("garden")]
        [TestCase("ruins")]
        [TestCase("hollow")]
        [TestCase("canopy")]
        [TestCase("nameless")]
        [TestCase(null)]
        [TestCase("")]
        public void EffectiveIn_TemperateOrUnknown_KeepsTheWorldWeather(string region)
        {
            foreach (WeatherType w in AllWeather())
                Assert.AreEqual(w, WeatherForecast.EffectiveIn(w, region), $"{region ?? "null"}에서 {w}");
        }

        [Test]
        public void EffectiveIn_OnlyColdAndWarmRegions_AreClimateRegions()
        {
            // 기후 표의 지역 ID가 실제 리전 정의에 있어야 한다 — 오타면 그 지역은 조용히 온대가 된다.
            string defs = System.IO.File.ReadAllText(
                System.IO.Path.Combine(Application.dataPath, "Scripts/Core/RegionDefinitions.cs"));
            foreach (string id in new[] { "frostline", "mountain", "dunes", "emberfall" })
                StringAssert.Contains($"regionId = \"{id}\"", defs, $"기후 표의 {id}가 리전 정의에 없다");
        }

        [Test]
        public void DisplayName_CoversEveryWeatherAndPhase()
        {
            foreach (WeatherType w in AllWeather())
            {
                string name = WeatherForecast.DisplayName(w);
                Assert.IsFalse(string.IsNullOrEmpty(name), $"{w} 이름");
            }
            Assert.AreEqual("눈", WeatherForecast.DisplayName(WeatherType.Snow));
            foreach (DayPhase p in Enum.GetValues(typeof(DayPhase)))
                Assert.IsFalse(string.IsNullOrEmpty(WeatherForecast.DisplayName(p)), $"{p} 이름");
        }

        // ── 시계 ──

        [TestCase(0f, DayPhase.Night)]
        [TestCase(5.99f, DayPhase.Night)]
        [TestCase(6f, DayPhase.Morning)]
        [TestCase(10.99f, DayPhase.Morning)]
        [TestCase(11f, DayPhase.Day)]
        [TestCase(16.99f, DayPhase.Day)]
        [TestCase(17f, DayPhase.Evening)]
        [TestCase(20.99f, DayPhase.Evening)]
        [TestCase(21f, DayPhase.Night)]
        [TestCase(23.99f, DayPhase.Night)]
        public void PhaseOfHour_Boundaries(float hour, DayPhase expected)
        {
            Assert.AreEqual(expected, GameClock.PhaseOfHour(hour));
        }

        [Test]
        public void Night_IsAboutAThirdOfTheDay()
        {
            // 밤이 너무 길면 낮 곤충을 못 만나는 시간이 길어진다 — 9시간(37.5%)이 기준이다.
            int night = 0;
            for (int i = 0; i < Sweep; i++)
                if (GameClock.PhaseOfHour(i / (float)Sweep * 24f) == DayPhase.Night) night++;
            Assert.AreEqual(0.375f, night / (float)Sweep, 0.01f);
        }

        [Test]
        public void GameClock_SetTime01_ChangesPhaseAndNotifies()
        {
            var go = new GameObject("ClockTest");
            try
            {
                var clock = go.AddComponent<GameClock>();
                var seen = new System.Collections.Generic.List<DayPhase>();
                clock.DayPhaseChanged += seen.Add;

                clock.SetTime01(0.5f, hold: true);   // 정오 — 처음(아침)과 다르다
                Assert.AreEqual(DayPhase.Day, clock.GetDayPhase());
                clock.SetTime01(0.5f, hold: true);   // 같은 시간대 — 알리지 않는다
                clock.SetTime01(23f / 24f, hold: true);
                clock.SetTime01(1.25f, hold: true);  // 범위 밖은 감아서 06시
                Assert.AreEqual(DayPhase.Morning, clock.GetDayPhase());

                CollectionAssert.AreEqual(new[] { DayPhase.Day, DayPhase.Night, DayPhase.Morning }, seen);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void WorldStateProvider_RegionalState_UsesTheRegionClimate()
        {
            var go = new GameObject("WorldStateTest");
            try
            {
                var weather = go.AddComponent<WeatherSystem>();
                var clock = go.AddComponent<GameClock>();
                var provider = go.AddComponent<WorldStateProvider>();
                provider.AutoWire(clock, weather);

                weather.SetWeather(WeatherType.Rain, hold: true, instant: true);
                Assert.AreEqual(WeatherType.Rain, provider.GetWorldState().Weather, "지역을 모르면 세계 날씨");
                Assert.AreEqual(WeatherType.Rain, provider.GetWorldState("meadow").Weather);
                Assert.AreEqual(WeatherType.Snow, provider.GetWorldState("frostline").Weather, "설산에서는 비가 눈");

                weather.SetWeather(WeatherType.Snow, hold: true, instant: true);
                Assert.AreEqual(WeatherType.Wind, provider.GetWorldState("dunes").Weather, "사막에는 눈이 안 온다");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void WeatherSystem_SetWeather_HoldsAndReportsTransitions()
        {
            var go = new GameObject("WeatherTest");
            try
            {
                var weather = go.AddComponent<WeatherSystem>();
                var changes = new System.Collections.Generic.List<(WeatherType, WeatherType)>();
                weather.WeatherChanged += (a, b) => changes.Add((a, b));

                Assert.AreEqual(WeatherType.Clear, weather.CurrentWeather, "처음은 맑음");
                Assert.AreEqual(1f, weather.Blend01, 0.0001f);

                weather.SetWeather(WeatherType.Fog, hold: true);
                Assert.AreEqual(WeatherType.Fog, weather.CurrentWeather, "스폰·전투는 전환을 기다리지 않는다");
                Assert.AreEqual(WeatherType.Clear, weather.PreviousWeather);
                Assert.AreEqual(0f, weather.Blend01, 0.0001f, "하늘은 이제 옮겨 간다");

                weather.SetWeather(WeatherType.Fog, hold: true, instant: true);   // 같은 날씨 — 알리지 않고 전환만 끝낸다
                Assert.AreEqual(1f, weather.Blend01, 0.0001f);

                weather.SetWeather(WeatherType.Snow, hold: true, instant: true);
                Assert.AreEqual(1f, weather.Blend01, 0.0001f);

                CollectionAssert.AreEqual(new[] { (WeatherType.Clear, WeatherType.Fog), (WeatherType.Fog, WeatherType.Snow) }, changes);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
#endif
