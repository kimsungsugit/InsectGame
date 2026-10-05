#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 낮·밤·날씨 하늘의 순수 규칙(<see cref="WorldSkyRules"/>) 불변식.
    ///
    /// 전부 <b>증상이 조용한</b> 것들이다. 밤이 너무 어두워도 예외는 없고 게임이 그냥 안 보이며, 안개 날씨가 짙어도
    /// 캐릭터가 뿌옇게 묻힐 뿐이고, 시간대 경계에서 조명이 뚝 끊겨도 에러가 없다. 눈으로 보는 건
    /// <c>LiveSceneCapture -captureHour -captureWeather</c>가 한다.
    ///
    /// 기본 상태(보정 전)는 <c>SubAreaEnvironment</c>가 실제로 만드는 값과 같게 적었다 — 필드는 리전 대기 표
    /// (<see cref="RegionAtmosphere"/>)와 <c>PlaySceneBootstrap.EnsureLight</c>의 라이트·Trilight 환경광, 섬은 섬 프로필(0.40/0.44/0.50 환경광 등).
    /// </summary>
    [TestFixture]
    public class WorldSkyRulesTests
    {
        private static readonly WeatherType[] AllWeathers = (WeatherType[])Enum.GetValues(typeof(WeatherType));

        // ── 기본 상태 ──

        private static WorldSkyRules.LightingState FieldBase(RegionAtmosphere.Profile p)
        {
            return new WorldSkyRules.LightingState
            {
                lightColor = p.light,
                lightIntensity = 1.2f * p.intensity,
                lightRotation = Quaternion.Euler(50f, 30f, 0f),
                shadowStrength = 0.5f,
                ambientSky = p.ambientSky,
                ambientEquator = new Color(0.42f, 0.43f, 0.41f),
                ambientGround = new Color(0.26f, 0.24f, 0.21f),
                fogEnabled = true,
                fogColor = p.fog,
                fogDensity = Mathf.Min(p.fogDensity, RegionAtmosphere.MaxFogDensity),
                background = new Color(0.5f, 0.8f, 1f),
            };
        }

        private static WorldSkyRules.LightingState IslandBase()
        {
            var amb = new Color(0.40f, 0.44f, 0.50f);
            return new WorldSkyRules.LightingState
            {
                lightColor = new Color(1f, 0.97f, 0.88f),
                lightIntensity = 1.1f,
                lightRotation = Quaternion.Euler(52f, 35f, 0f),
                shadowStrength = 0.5f,
                ambientSky = amb,
                ambientEquator = amb,
                ambientGround = amb,
                fogEnabled = true,
                fogColor = new Color(0.66f, 0.84f, 0.95f),
                fogDensity = 0.006f,
                background = new Color(0.56f, 0.80f, 0.96f),
            };
        }

        private static List<KeyValuePair<string, WorldSkyRules.LightingState>> AllFieldBases()
        {
            var list = new List<KeyValuePair<string, WorldSkyRules.LightingState>>();
            foreach (InsectGame.Data.RegionData r in RegionDefinitions.CreateAll())
            {
                Assert.IsTrue(RegionAtmosphere.TryGet(r.regionId, out RegionAtmosphere.Profile p), r.regionId);
                list.Add(new KeyValuePair<string, WorldSkyRules.LightingState>(r.regionId, FieldBase(p)));
            }
            Assert.GreaterOrEqual(list.Count, 10, "리전 표를 못 읽었다 — 이 검사가 무의미해졌다");
            return list;
        }

        private static WorldSkyRules.LightingState Meadow()
        {
            Assert.IsTrue(RegionAtmosphere.TryGet("meadow", out RegionAtmosphere.Profile p));
            return FieldBase(p);
        }

        private static float SunElevation01(WorldSkyRules.LightingState s) => Mathf.Max(0f, -(s.lightRotation * Vector3.forward).y);

        private static void AssertColor(Color expected, Color actual, float tol, string message)
        {
            Assert.AreEqual(expected.r, actual.r, tol, message + " (r)");
            Assert.AreEqual(expected.g, actual.g, tol, message + " (g)");
            Assert.AreEqual(expected.b, actual.b, tol, message + " (b)");
        }

        // ── 정오 맑음은 기준이다 ──

        [Test]
        public void Apply_NoonClear_ReturnsTheBaseStateUnchanged()
        {
            // 기존 한낮 모습이 곧 기준이다 — 이 규칙이 생겼다고 한낮의 리전 색·세기가 달라지면 안 된다
            foreach (KeyValuePair<string, WorldSkyRules.LightingState> kv in AllFieldBases())
            {
                WorldSkyRules.LightingState b = kv.Value;
                WorldSkyRules.LightingState o = WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(12f, WeatherType.Clear), true);
                AssertColor(b.lightColor, o.lightColor, 0.002f, kv.Key + " 햇빛색");
                Assert.AreEqual(b.lightIntensity, o.lightIntensity, 0.002f, kv.Key + " 햇빛 세기");
                Assert.Less(Quaternion.Angle(b.lightRotation, o.lightRotation), 0.5f, kv.Key + " 햇빛 방향");
                AssertColor(b.ambientSky, o.ambientSky, 0.002f, kv.Key + " 위 환경광");
                AssertColor(b.ambientEquator, o.ambientEquator, 0.002f, kv.Key + " 옆 환경광");
                AssertColor(b.ambientGround, o.ambientGround, 0.002f, kv.Key + " 아래 환경광");
                AssertColor(b.fogColor, o.fogColor, 0.002f, kv.Key + " 안개색");
                AssertColor(b.background, o.background, 0.002f, kv.Key + " 배경");
                Assert.AreEqual(b.fogDensity, o.fogDensity, 1e-6f, kv.Key + " 안개 밀도");
                Assert.AreEqual(b.shadowStrength, o.shadowStrength, 0.002f, kv.Key + " 그림자");
            }

            WorldSkyRules.LightingState island = IslandBase();
            WorldSkyRules.LightingState oi = WorldSkyRules.Apply(island, WorldSkyRules.Evaluate(12f, WeatherType.Clear), false);
            AssertColor(island.ambientSky, oi.ambientSky, 0.002f, "섬 환경광");
            AssertColor(island.background, oi.background, 0.002f, "섬 하늘");
            Assert.AreEqual(island.fogDensity, oi.fogDensity, 1e-6f, "섬 안개 밀도");
        }

        [Test]
        public void Apply_ClearWeather_NeverTouchesTheFogDensity()
        {
            // 날씨 없이 시각만으로는 안개 밀도가 달라지지 않는다 — 밤에 안개가 끼는 건 날씨의 몫이다
            foreach (KeyValuePair<string, WorldSkyRules.LightingState> kv in AllFieldBases())
                for (float h = 0f; h < 24f; h += 0.5f)
                    Assert.AreEqual(kv.Value.fogDensity,
                        WorldSkyRules.Apply(kv.Value, WorldSkyRules.Evaluate(h, WeatherType.Clear), true).fogDensity, 1e-6f,
                        $"{kv.Key} {h}시");
        }

        // ── 낮 밝기 곡선 ──

        [Test]
        public void Daylight_PeaksAtNoon()
        {
            foreach (KeyValuePair<string, WorldSkyRules.LightingState> kv in AllFieldBases())
            {
                float noon = WorldSkyRules.UpLuminance(WorldSkyRules.Apply(kv.Value, WorldSkyRules.Evaluate(12f, WeatherType.Clear), true));
                for (float h = 0f; h < 24f; h += 0.05f)
                {
                    float lum = WorldSkyRules.UpLuminance(WorldSkyRules.Apply(kv.Value, WorldSkyRules.Evaluate(h, WeatherType.Clear), true));
                    Assert.LessOrEqual(lum, noon + 1e-4f, $"{kv.Key}: {h:F2}시가 정오보다 밝다");
                }
                float before = WorldSkyRules.UpLuminance(WorldSkyRules.Apply(kv.Value, WorldSkyRules.Evaluate(11f, WeatherType.Clear), true));
                float after = WorldSkyRules.UpLuminance(WorldSkyRules.Apply(kv.Value, WorldSkyRules.Evaluate(13f, WeatherType.Clear), true));
                Assert.Greater(noon, before, kv.Key + ": 정오가 11시와 같다 — 최대가 평평하다");
                Assert.Greater(noon, after, kv.Key + ": 정오가 13시와 같다 — 최대가 평평하다");
            }
        }

        [Test]
        public void SunHeight_RisesAtSixPeaksAtNoonAndSetsAtNineteen()
        {
            Assert.AreEqual(0f, WorldSkyRules.SunHeight01(5f), 1e-6f);
            Assert.AreEqual(0f, WorldSkyRules.SunHeight01(WorldSkyRules.SunriseHour), 1e-5f);
            Assert.AreEqual(1f, WorldSkyRules.SunHeight01(WorldSkyRules.NoonHour), 1e-5f);
            Assert.AreEqual(0f, WorldSkyRules.SunHeight01(WorldSkyRules.SunsetHour), 1e-5f);
            Assert.AreEqual(0f, WorldSkyRules.SunHeight01(23f), 1e-6f);
            for (float h = 6f; h < 12f; h += 0.25f)
                Assert.Less(WorldSkyRules.SunHeight01(h), WorldSkyRules.SunHeight01(h + 0.25f), $"{h}시 오르막");
            for (float h = 12f; h < 19f; h += 0.25f)
                Assert.Greater(WorldSkyRules.SunHeight01(h), WorldSkyRules.SunHeight01(h + 0.25f), $"{h}시 내리막");
            Assert.AreEqual(-1f, WorldSkyRules.SunAzimuth(3f), 1e-6f);
            Assert.AreEqual(0f, WorldSkyRules.SunAzimuth(12f), 1e-6f);
            Assert.AreEqual(1f, WorldSkyRules.SunAzimuth(22f), 1e-6f);
        }

        [Test]
        public void Sun_MovesAcrossTheSkyThroughTheDay()
        {
            WorldSkyRules.LightingState b = Meadow();
            WorldSkyRules.LightingState morning = WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(8f, WeatherType.Clear), true);
            WorldSkyRules.LightingState noon = WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(12f, WeatherType.Clear), true);
            WorldSkyRules.LightingState evening = WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(16f, WeatherType.Clear), true);
            Assert.Greater(SunElevation01(noon), SunElevation01(morning), "정오 해가 아침 해보다 높아야 한다");
            Assert.Greater(SunElevation01(noon), SunElevation01(evening), "정오 해가 오후 해보다 높아야 한다");
            Assert.Greater(Quaternion.Angle(morning.lightRotation, evening.lightRotation), 30f, "그림자 방향이 하루 동안 돌아야 한다");
        }

        [Test]
        public void SunriseAndSunset_AreWarmerThanNoon()
        {
            WorldSkyRules.LightingState b = Meadow();
            float noon = RedMinusBlue(WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(12f, WeatherType.Clear), true).lightColor);
            Assert.Greater(RedMinusBlue(WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(6f, WeatherType.Clear), true).lightColor), noon + 0.1f, "일출 빛이 주황빛이 아니다");
            Assert.Greater(RedMinusBlue(WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(18.5f, WeatherType.Clear), true).lightColor), noon + 0.1f, "노을 빛이 주황빛이 아니다");
        }

        private static float RedMinusBlue(Color c) => c.r - c.b;

        // ── 연속성 ──

        [Test]
        public void Apply_AcrossTheDay_ChangesSmoothly()
        {
            // 시간대 경계(5·6·7·17·19·20·21·22시)에서 뚝 끊기면 한 틱(0.02시 = 0.6초)에 조도가 튄다
            var bases = new List<WorldSkyRules.LightingState> { Meadow(), IslandBase() };
            foreach (KeyValuePair<string, WorldSkyRules.LightingState> kv in AllFieldBases())
                if (kv.Key == "emberfall") bases.Add(kv.Value);

            foreach (WorldSkyRules.LightingState b in bases)
            {
                foreach (WeatherType w in AllWeathers)
                {
                    for (int i = 0; i < 1200; i++)
                    {
                        float h = i * 0.02f;
                        WorldSkyRules.LightingState a = WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(h, w), true);
                        WorldSkyRules.LightingState c = WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(h + 0.02f, w), true);   // 마지막 틱은 24시 = 0시로 이어진다
                        Assert.LessOrEqual(Mathf.Abs(WorldSkyRules.UpLuminance(a) - WorldSkyRules.UpLuminance(c)), 0.03f, $"{w} {h:F2}시 위쪽 조도가 튄다");
                        Assert.LessOrEqual(Mathf.Abs(WorldSkyRules.SideLuminance(a) - WorldSkyRules.SideLuminance(c)), 0.03f, $"{w} {h:F2}시 옆쪽 조도가 튄다");
                        Assert.LessOrEqual(Mathf.Abs(a.lightIntensity - c.lightIntensity), 0.05f, $"{w} {h:F2}시 햇빛 세기가 튄다");
                        Assert.LessOrEqual(ChannelDelta(a.fogColor, c.fogColor), 0.06f, $"{w} {h:F2}시 안개색이 튄다");
                        Assert.LessOrEqual(ChannelDelta(a.background, c.background), 0.06f, $"{w} {h:F2}시 하늘색이 튄다");
                        Assert.LessOrEqual(Quaternion.Angle(a.lightRotation, c.lightRotation), 6f, $"{w} {h:F2}시 햇빛 방향이 튄다");
                    }
                }
            }
        }

        private static float ChannelDelta(Color a, Color b)
        {
            return Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));
        }

        [Test]
        public void Evaluate_HoursWrapAroundTheDay()
        {
            Assert.AreEqual(WorldSkyRules.Evaluate(23f, WeatherType.Rain).sunFactor, WorldSkyRules.Evaluate(-1f, WeatherType.Rain).sunFactor, 1e-5f);
            Assert.AreEqual(WorldSkyRules.Evaluate(1f, WeatherType.Clear).moonFactor, WorldSkyRules.Evaluate(25f, WeatherType.Clear).moonFactor, 1e-5f);
            Assert.AreEqual(WorldSkyRules.Evaluate(0f, WeatherType.Clear).starAlpha, WorldSkyRules.Evaluate(24f, WeatherType.Clear).starAlpha, 1e-5f);
        }

        // ── 밤은 어둡되 보인다 ──

        [Test]
        public void Night_StaysReadable_ForEveryRegionWeatherAndHour()
        {
            // 환경광 바닥이 구조적 보장이고(어떤 조합이어도 그 밑으로 못 내려간다), 조도 하한은 실측 여유를 둔 값이다 —
            // 기본 필드 정오 맑음의 위쪽 조도가 약 1.43이고 가장 어두운 조합(어두운 리전·한낮 아닌 해 질 녘의 비)이 약 0.44다.
            const float MinUp = 0.40f;
            const float MinSide = 0.33f;
            var bases = AllFieldBases();
            bases.Add(new KeyValuePair<string, WorldSkyRules.LightingState>("island", IslandBase()));

            foreach (KeyValuePair<string, WorldSkyRules.LightingState> kv in bases)
            {
                bool field = kv.Key != "island";
                foreach (WeatherType w in AllWeathers)
                {
                    for (float h = 0f; h < 24f; h += 0.25f)
                    {
                        WorldSkyRules.LightingState s = WorldSkyRules.Apply(kv.Value, WorldSkyRules.Evaluate(h, w), field);
                        string tag = $"{kv.Key} {w} {h:F2}시";
                        Assert.GreaterOrEqual(WorldSkyRules.Luminance(s.ambientSky), WorldSkyRules.AmbientFloorSky - 1e-4f, tag + " 위 환경광이 바닥 아래");
                        Assert.GreaterOrEqual(WorldSkyRules.Luminance(s.ambientEquator), WorldSkyRules.AmbientFloorEquator - 1e-4f, tag + " 옆 환경광이 바닥 아래");
                        Assert.GreaterOrEqual(WorldSkyRules.Luminance(s.ambientGround), WorldSkyRules.AmbientFloorGround - 1e-4f, tag + " 아래 환경광이 바닥 아래");
                        Assert.GreaterOrEqual(WorldSkyRules.UpLuminance(s), MinUp, tag + " 위를 향한 면이 너무 어둡다");
                        Assert.GreaterOrEqual(WorldSkyRules.SideLuminance(s), MinSide, tag + " 캐릭터 옆면이 너무 어둡다");
                    }
                }
            }
        }

        [Test]
        public void Night_UsesColdDimMoonlightFromHighUp()
        {
            WorldSkyRules.LightingState b = Meadow();
            foreach (float h in new[] { 0f, 2f, 23f })
            {
                WorldSkyRules.LightingState s = WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(h, WeatherType.Clear), true);
                Assert.AreEqual(WorldSkyRules.MoonIntensity, s.lightIntensity, 0.01f, $"{h}시 달빛 세기");
                Assert.Less(s.lightIntensity, b.lightIntensity * 0.5f, $"{h}시 달빛이 낮 햇빛의 절반 아래여야 한다");
                Assert.Greater(s.lightColor.b, s.lightColor.r + 0.2f, $"{h}시 달빛이 차갑고 푸르지 않다");
                Assert.Greater(SunElevation01(s), 0.6f, $"{h}시 달이 낮게 떠 있다 — 그림자가 길어진다");
            }
        }

        [Test]
        public void Moonlight_TakesOverBeforeDuskIsDarkerThanNight()
        {
            // 달빛이 20시에야 켜지면 해가 약해진 19시가 한밤보다 어두운 홈이 생긴다(실측 74%) — 18~22시가 한밤 아래로 꺼지지 않는다
            WorldSkyRules.LightingState b = Meadow();
            float night = WorldSkyRules.UpLuminance(WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(0f, WeatherType.Clear), true));
            for (float h = 17f; h <= 22f; h += 0.1f)
            {
                float lum = WorldSkyRules.UpLuminance(WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(h, WeatherType.Clear), true));
                Assert.GreaterOrEqual(lum, night * 0.95f, $"{h:F1}시가 한밤보다 눈에 띄게 어둡다 ({lum:F3} < {night:F3})");
            }
            float dawnNight = WorldSkyRules.UpLuminance(WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(4f, WeatherType.Clear), true));
            for (float h = 4.5f; h <= 7f; h += 0.1f)
            {
                float lum = WorldSkyRules.UpLuminance(WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(h, WeatherType.Clear), true));
                Assert.GreaterOrEqual(lum, dawnNight * 0.95f, $"{h:F1}시 새벽이 한밤보다 눈에 띄게 어둡다");
            }
        }

        [Test]
        public void Night_IsDarkerThanDayButNotBlack()
        {
            WorldSkyRules.LightingState b = Meadow();
            float noon = WorldSkyRules.UpLuminance(WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(12f, WeatherType.Clear), true));
            float night = WorldSkyRules.UpLuminance(WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(0f, WeatherType.Clear), true));
            Assert.Less(night, noon * 0.6f, "밤이 낮만큼 밝으면 밤이 아니다");
            Assert.Greater(night, noon * 0.3f, "밤이 낮의 30%도 안 되면 곤충·캐릭터가 안 보인다");
        }

        [Test]
        public void Stars_ShowAtNightAndHideInDaylightAndUnderClouds()
        {
            Assert.AreEqual(1f, WorldSkyRules.Evaluate(0f, WeatherType.Clear).starAlpha, 1e-4f);
            Assert.AreEqual(0f, WorldSkyRules.Evaluate(12f, WeatherType.Clear).starAlpha, 1e-4f);
            Assert.Less(WorldSkyRules.Evaluate(0f, WeatherType.Rain).starAlpha, 0.3f, "비 오는 밤에도 별이 환하다");
            Assert.Less(WorldSkyRules.Evaluate(0f, WeatherType.Rain).starAlpha, WorldSkyRules.Evaluate(0f, WeatherType.Wind).starAlpha);
        }

        // ── 날씨 ──

        [Test]
        public void WeatherType_EveryNonClearWeatherDimsTheSun()
        {
            // 새 날씨를 enum에 늘리고 GradeOf에 칸을 안 만들면 조용히 맑음으로 떨어진다 — 정오에서 맑음과 달라야 한다
            Assert.AreEqual(5, AllWeathers.Length, "WeatherType이 늘었다 — WorldSkyRules.GradeOf·WeatherMix·WeatherEffects에 칸을 만들 것");
            float clear = WorldSkyRules.Evaluate(12f, WeatherType.Clear).sunFactor;
            foreach (WeatherType w in AllWeathers)
            {
                if (w == WeatherType.Clear) continue;
                Assert.Less(WorldSkyRules.Evaluate(12f, w).sunFactor, clear, w + "이 맑음과 같은 빛이다");
                Assert.Greater(WorldSkyRules.Evaluate(12f, w).overcast, 0f, w + "이 흐림 0이다");
            }
        }

        [Test]
        public void Weather_RainIsDarkerThanClearAndFogIsThickerThanRain()
        {
            WorldSkyRules.LightingState b = Meadow();
            WorldSkyRules.LightingState clear = WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(12f, WeatherType.Clear), true);
            WorldSkyRules.LightingState rain = WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(12f, WeatherType.Rain), true);
            WorldSkyRules.LightingState fog = WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(12f, WeatherType.Fog), true);
            Assert.Less(WorldSkyRules.UpLuminance(rain), WorldSkyRules.UpLuminance(clear) * 0.7f, "비 오는 낮이 맑은 낮만큼 밝다");
            Assert.Greater(fog.fogDensity, rain.fogDensity, "안개 날씨가 비보다 옅다");
            Assert.Greater(rain.fogDensity, clear.fogDensity, "비 오는 날 연무가 맑은 날과 같다");
            Assert.Less(rain.shadowStrength, clear.shadowStrength * 0.5f, "흐린 날 그림자가 맑은 날만큼 진하다");
        }

        [Test]
        public void FogWeather_KeepsThePlayerReadable_InTheField()
        {
            // 안개 낀 날이라도 카메라(11m)에서 캐릭터가 읽혀야 한다 — 같은 식(FogTransmittance)·같은 모드 상수로 모든 리전·시각·전환을 훑는다
            float dist = RegionAtmosphere.PlayerCameraDistance;
            float worst = 1f;
            foreach (KeyValuePair<string, WorldSkyRules.LightingState> kv in AllFieldBases())
            {
                foreach (WeatherType from in AllWeathers)
                {
                    foreach (WeatherType to in AllWeathers)
                    {
                        for (float blend = 0f; blend <= 1f; blend += 0.25f)
                        {
                            WorldSkyRules.WeatherMix mix = WorldSkyRules.WeatherMix.Blend(from, to, blend);
                            for (float h = 0f; h < 24f; h += 3f)
                            {
                                WorldSkyRules.LightingState s = WorldSkyRules.Apply(kv.Value, WorldSkyRules.Evaluate(h, mix), true);
                                Assert.LessOrEqual(s.fogDensity, RegionAtmosphere.MaxWeatherFogDensity + 1e-6f, $"{kv.Key} {from}→{to} {blend}");
                                float t = RegionAtmosphere.FogTransmittance(RegionAtmosphere.FieldFogMode, s.fogDensity, dist);
                                worst = Mathf.Min(worst, t);
                                Assert.GreaterOrEqual(t, RegionAtmosphere.MinWeatherTransmittance,
                                    $"{kv.Key} {from}→{to} {blend:F2}: {RegionAtmosphere.FieldFogMode} {dist}m 투과율 {t:F3}");
                            }
                        }
                    }
                }
            }
            // 상한 자체도 같은 식으로 통과해야 한다 — 상수를 올리다 하한과 어긋나면 위 검사가 못 잡는 조합이 생긴다
            Assert.GreaterOrEqual(RegionAtmosphere.FogTransmittance(RegionAtmosphere.FieldFogMode, RegionAtmosphere.MaxWeatherFogDensity, dist),
                RegionAtmosphere.MinWeatherTransmittance, "안개 날씨 밀도 상한의 11m 투과율이 하한 아래다");
            // 안개 날씨가 실제로 안개로 보여야 한다 — 하한만 지키려고 가산을 0으로 두면 이 날씨가 조용히 사라진다
            Assert.Less(worst, 0.95f, "안개 날씨의 최소 11m 투과율이 95%를 넘는다 — 안개가 안 보인다");
        }

        [Test]
        public void FogWeather_ThickensFarFogMoreThanTheRegionHaze()
        {
            WorldSkyRules.LightingState b = Meadow();
            float baseFar = RegionAtmosphere.FogTransmittance(RegionAtmosphere.FieldFogMode, b.fogDensity, 40f);
            WorldSkyRules.LightingState fog = WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(12f, WeatherType.Fog), true);
            float fogFar = RegionAtmosphere.FogTransmittance(RegionAtmosphere.FieldFogMode, fog.fogDensity, 40f);
            Assert.Less(fogFar, 0.35f, "안개 날씨에 40m 밖 풍경이 안 묻힌다");
            Assert.Greater(baseFar, 0.8f, "리전 연무가 40m에서 이미 짙다 — 이 비교가 의미를 잃었다");
        }

        [Test]
        public void FogWeather_KeepsThePlayerReadable_OnTheIsland()
        {
            // 섬은 분리 서브에리어라 안개가 지수(Exp)다 — 환산 계수와 상한이 11m 투과율을 지킨다
            float dist = RegionAtmosphere.PlayerCameraDistance;
            WorldSkyRules.LightingState island = IslandBase();
            foreach (WeatherType from in AllWeathers)
            {
                foreach (WeatherType to in AllWeathers)
                {
                    for (float blend = 0f; blend <= 1f; blend += 0.25f)
                    {
                        WorldSkyRules.LightingState s = WorldSkyRules.Apply(island,
                            WorldSkyRules.Evaluate(12f, WorldSkyRules.WeatherMix.Blend(from, to, blend)), false);
                        Assert.LessOrEqual(s.fogDensity, RegionAtmosphere.MaxWeatherFogDensitySubArea + 1e-6f, $"{from}→{to} {blend}");
                        float t = RegionAtmosphere.FogTransmittance(RegionAtmosphere.SubAreaFogMode, s.fogDensity, dist);
                        Assert.GreaterOrEqual(t, RegionAtmosphere.MinWeatherTransmittanceSubArea, $"섬 {from}→{to} {blend:F2}: 투과율 {t:F3}");
                    }
                }
            }
            Assert.GreaterOrEqual(
                RegionAtmosphere.FogTransmittance(RegionAtmosphere.SubAreaFogMode, RegionAtmosphere.MaxWeatherFogDensitySubArea, dist),
                RegionAtmosphere.MinWeatherTransmittanceSubArea, "섬 안개 밀도 상한의 11m 투과율이 하한 아래다");
            WorldSkyRules.LightingState fog = WorldSkyRules.Apply(island, WorldSkyRules.Evaluate(12f, WeatherType.Fog), false);
            Assert.Greater(fog.fogDensity, island.fogDensity * 1.5f, "섬에서 안개 날씨가 안 보인다");
        }

        [Test]
        public void FogWeather_TurnsOnFogWhereTheBaseHasNone()
        {
            // 길 위(리전 밖)는 기본 안개가 꺼져 있다 — 안개 날씨가 오면 켜져야 하고, 날씨가 없으면 꺼진 채여야 한다
            WorldSkyRules.LightingState b = Meadow();
            b.fogEnabled = false;
            b.fogDensity = 0f;
            Assert.IsFalse(WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(12f, WeatherType.Clear), true).fogEnabled);
            WorldSkyRules.LightingState fog = WorldSkyRules.Apply(b, WorldSkyRules.Evaluate(12f, WeatherType.Fog), true);
            Assert.IsTrue(fog.fogEnabled);
            Assert.Greater(fog.fogDensity, 0.01f);
        }

        [Test]
        public void WeatherMix_BlendEndsAreExactAndMiddleIsBetween()
        {
            WorldSkyRules.WeatherMix start = WorldSkyRules.WeatherMix.Blend(WeatherType.Rain, WeatherType.Clear, 0f);
            Assert.AreEqual(1f, start.rain, 1e-6f);
            Assert.AreEqual(0f, start.clear, 1e-6f);
            WorldSkyRules.WeatherMix end = WorldSkyRules.WeatherMix.Blend(WeatherType.Rain, WeatherType.Clear, 1f);
            Assert.AreEqual(0f, end.rain, 1e-6f);
            Assert.AreEqual(1f, end.clear, 1e-6f);
            WorldSkyRules.WeatherMix mid = WorldSkyRules.WeatherMix.Blend(WeatherType.Rain, WeatherType.Clear, 0.5f);
            Assert.AreEqual(1f, mid.Total, 1e-5f, "가중치 합이 1이 아니다");
            Assert.AreEqual(0.5f, mid.rain, 1e-5f);

            // 전환 양끝의 하늘이 각 날씨 하나만 평가한 것과 같다
            foreach (WeatherType from in AllWeathers)
            {
                foreach (WeatherType to in AllWeathers)
                {
                    WorldSkyRules.SkyFrame a0 = WorldSkyRules.Evaluate(15f, WorldSkyRules.WeatherMix.Blend(from, to, 0f));
                    WorldSkyRules.SkyFrame a1 = WorldSkyRules.Evaluate(15f, from);
                    Assert.AreEqual(a1.sunFactor, a0.sunFactor, 1e-5f, $"{from}→{to} 시작");
                    Assert.AreEqual(a1.fogAdd, a0.fogAdd, 1e-6f, $"{from}→{to} 시작 안개");
                    WorldSkyRules.SkyFrame b0 = WorldSkyRules.Evaluate(15f, WorldSkyRules.WeatherMix.Blend(from, to, 1f));
                    WorldSkyRules.SkyFrame b1 = WorldSkyRules.Evaluate(15f, to);
                    Assert.AreEqual(b1.sunFactor, b0.sunFactor, 1e-5f, $"{from}→{to} 끝");
                    Assert.AreEqual(b1.overcast, b0.overcast, 1e-6f, $"{from}→{to} 끝 흐림");
                }
            }

            WorldSkyRules.SkyFrame clear = WorldSkyRules.Evaluate(12f, WeatherType.Clear);
            WorldSkyRules.SkyFrame rain = WorldSkyRules.Evaluate(12f, WeatherType.Rain);
            WorldSkyRules.SkyFrame between = WorldSkyRules.Evaluate(12f, WorldSkyRules.WeatherMix.Blend(WeatherType.Clear, WeatherType.Rain, 0.5f));
            Assert.Less(between.sunFactor, clear.sunFactor);
            Assert.Greater(between.sunFactor, rain.sunFactor);
        }

        [Test]
        public void WeatherMix_SameWeatherBlendIsThatWeatherAtAnyProgress()
        {
            foreach (WeatherType w in AllWeathers)
                for (float blend = 0f; blend <= 1f; blend += 0.25f)
                    Assert.AreEqual(1f, WorldSkyRules.WeatherMix.Blend(w, w, blend).Get(w), 1e-5f, $"{w} {blend}");
        }

        [Test]
        public void WeatherMix_FromZeroWeights_FallsBackToClear()
        {
            // 가중치가 전부 0인 입력(방어)이 NaN을 만들지 않고 맑음으로 평가된다
            WorldSkyRules.SkyFrame f = WorldSkyRules.Evaluate(12f, default(WorldSkyRules.WeatherMix));
            Assert.AreEqual(WorldSkyRules.Evaluate(12f, WeatherType.Clear).sunFactor, f.sunFactor, 1e-6f);
            Assert.IsFalse(float.IsNaN(f.overcast));
        }

        [Test]
        public void WeatherMix_FromRegionClimate_FollowsForecastEffectiveWeather()
        {
            // 입자·조명은 지역 기준 날씨를 읽는다 — 설산의 비는 눈, 사막의 눈은 센바람
            var go = new GameObject("WorldSkyRulesTestWeather");
            try
            {
                WeatherSystem ws = go.AddComponent<WeatherSystem>();
                ws.SetWeather(WeatherType.Rain, hold: true, instant: true);
                Assert.AreEqual(1f, WorldSkyRules.WeatherMix.From(ws, "meadow").rain, 1e-5f, "초원의 비는 비");
                Assert.AreEqual(1f, WorldSkyRules.WeatherMix.From(ws, "frostline").snow, 1e-5f, "설산의 비는 눈");
                Assert.AreEqual(0f, WorldSkyRules.WeatherMix.From(ws, "frostline").rain, 1e-5f);
                Assert.AreEqual(1f, WorldSkyRules.WeatherMix.From(ws, null).rain, 1e-5f, "섬·길 위는 세계 날씨 그대로");
                ws.SetWeather(WeatherType.Snow, hold: true, instant: true);
                Assert.AreEqual(1f, WorldSkyRules.WeatherMix.From(ws, "dunes").wind, 1e-5f, "사막의 눈은 센바람");
                Assert.AreEqual(1f, WorldSkyRules.WeatherMix.From(ws, "meadow").snow, 1e-5f);
                Assert.AreEqual(1f, WorldSkyRules.WeatherMix.From(null, "meadow").clear, 1e-5f, "시스템이 없으면 맑음");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ── 판정 ──

        [Test]
        public void Applies_OnlyOnTheFieldAndTheIsland_NeverInTheDream()
        {
            Assert.IsTrue(WorldSkyRules.Applies(false, null, false), "메인 필드");
            Assert.IsTrue(WorldSkyRules.Applies(true, GameConstants.Island.SubAreaId, false), "나의 섬");
            Assert.IsFalse(WorldSkyRules.Applies(true, "mountain_cave", false), "동굴은 실내");
            Assert.IsFalse(WorldSkyRules.Applies(true, "greenhouse", false), "방은 실내");
            Assert.IsFalse(WorldSkyRules.Applies(false, null, true), "꿈속 필드는 늘 맑은 한낮");
            Assert.IsFalse(WorldSkyRules.Applies(true, GameConstants.Island.SubAreaId, true), "꿈속 섬은 늘 맑은 한낮");
        }

        // ── 보간 ──

        [Test]
        public void LightingState_LerpEndsAreExactAndFogOffSideCountsAsZeroDensity()
        {
            WorldSkyRules.LightingState a = Meadow();
            WorldSkyRules.LightingState b = WorldSkyRules.Apply(a, WorldSkyRules.Evaluate(0f, WeatherType.Rain), true);
            WorldSkyRules.LightingState at0 = WorldSkyRules.LightingState.Lerp(a, b, 0f);
            WorldSkyRules.LightingState at1 = WorldSkyRules.LightingState.Lerp(a, b, 1f);
            Assert.AreEqual(a.lightIntensity, at0.lightIntensity, 1e-6f);
            Assert.AreEqual(b.lightIntensity, at1.lightIntensity, 1e-6f);
            AssertColor(a.ambientSky, at0.ambientSky, 1e-6f, "t=0 환경광");
            AssertColor(b.ambientSky, at1.ambientSky, 1e-6f, "t=1 환경광");
            Assert.Less(Quaternion.Angle(a.lightRotation, at0.lightRotation), 0.01f);
            Assert.Less(Quaternion.Angle(b.lightRotation, at1.lightRotation), 0.01f);

            // 꺼진 쪽의 밀도 찌꺼기가 번지지 않는다
            a.fogEnabled = false;
            a.fogDensity = 0.5f;
            b.fogEnabled = true;
            b.fogDensity = 0.01f;
            Assert.AreEqual(0.005f, WorldSkyRules.LightingState.Lerp(a, b, 0.5f).fogDensity, 1e-6f);
            Assert.IsTrue(WorldSkyRules.LightingState.Lerp(a, b, 0.5f).fogEnabled);
        }

        [Test]
        public void SkyFrame_LightLevelStaysBetweenNightAndNoon()
        {
            foreach (WeatherType w in AllWeathers)
            {
                for (float h = 0f; h < 24f; h += 0.5f)
                {
                    float level = WorldSkyRules.Evaluate(h, w).lightLevel;
                    Assert.GreaterOrEqual(level, 0.4f - 1e-5f, $"{w} {h}시");
                    Assert.LessOrEqual(level, 1f + 1e-5f, $"{w} {h}시");
                }
            }
            Assert.AreEqual(1f, WorldSkyRules.Evaluate(12f, WeatherType.Clear).lightLevel, 1e-5f);
            Assert.AreEqual(0.4f, WorldSkyRules.Evaluate(0f, WeatherType.Clear).lightLevel, 1e-5f);
        }

        // ── 흐린 하늘의 연무 막 ──

        private static readonly WeatherType[] OvercastWeathers = { WeatherType.Rain, WeatherType.Fog, WeatherType.Snow };

        private static float Saturation(Color c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b));

        [Test]
        public void SkyHaze_IsZeroOnClearDays_SoTheVeilCostsNothing()
        {
            // 맑은 하늘은 지금 그대로여야 한다 — 짙기가 0이면 연무 돔이 GameObject째 꺼진다
            for (float h = 0f; h < 24f; h += 0.25f)
                Assert.AreEqual(0f, WorldSkyRules.Evaluate(h, WeatherType.Clear).skyHaze, 1e-6f, $"{h}시");
        }

        [Test]
        public void SkyHaze_FullyVeilsTheHorizon_OnOvercastDays()
        {
            // 비·눈·안개는 지평선이 안개색으로 완전히 덮여야 산이 하늘로 녹는다. 센바람은 먼지 낀 맑은 날이라 옅다.
            for (float h = 0f; h < 24f; h += 0.5f)
            {
                foreach (WeatherType w in OvercastWeathers)
                    Assert.AreEqual(1f, WorldSkyRules.Evaluate(h, w).skyHaze, 1e-5f, $"{w} {h}시");
                float wind = WorldSkyRules.Evaluate(h, WeatherType.Wind).skyHaze;
                Assert.Greater(wind, 0f, $"센바람 {h}시 — 먼지 낀 지평선이 사라졌다");
                Assert.Less(wind, 0.3f, $"센바람 {h}시 — 맑은 날인데 하늘이 덮였다");
            }
        }

        [Test]
        public void SkyHaze_RisesSmoothlyThroughAWeatherTransition()
        {
            // 10초 날씨 전환 동안 연무가 뚝 켜지면 하늘이 한 프레임에 회색으로 바뀐다
            foreach (WeatherType w in OvercastWeathers)
            {
                float prev = WorldSkyRules.Evaluate(12f, WorldSkyRules.WeatherMix.Blend(WeatherType.Clear, w, 0f)).skyHaze;
                Assert.AreEqual(0f, prev, 1e-6f, $"맑음→{w} 시작");
                for (int i = 1; i <= 100; i++)
                {
                    float haze = WorldSkyRules.Evaluate(12f, WorldSkyRules.WeatherMix.Blend(WeatherType.Clear, w, i / 100f)).skyHaze;
                    Assert.GreaterOrEqual(haze, prev - 1e-6f, $"맑음→{w} {i}% 연무가 줄었다");
                    Assert.LessOrEqual(haze - prev, 0.05f, $"맑음→{w} {i}% 연무가 튄다");
                    prev = haze;
                }
                Assert.AreEqual(1f, prev, 1e-5f, $"맑음→{w} 끝");
            }
        }

        [Test]
        public void VeilProfile_IsDensestAtTheHorizonAndThinsUpward()
        {
            Assert.AreEqual(1f, WorldSkyRules.VeilProfile(-90f), 1e-6f, "땅 쪽");
            Assert.AreEqual(1f, WorldSkyRules.VeilProfile(0f), 1e-6f, "지평선");
            Assert.AreEqual(1f, WorldSkyRules.VeilProfile(WorldSkyRules.VeilBandDegrees), 1e-6f, "지평선 띠");
            Assert.AreEqual(WorldSkyRules.VeilZenithFactor, WorldSkyRules.VeilProfile(90f), 1e-6f, "천정");
            Assert.GreaterOrEqual(WorldSkyRules.VeilZenithFactor, 0.7f, "천정이 너무 맑다 — 흐린 날 위쪽이 파랗게 남는다");
            Assert.Less(WorldSkyRules.VeilZenithFactor, 1f, "세로 분포가 없다 — 지평선과 천정이 같은 짙기다");
            float prev = 1f;
            for (float e = -90f; e <= 90f; e += 0.5f)
            {
                float v = WorldSkyRules.VeilProfile(e);
                Assert.LessOrEqual(v, prev + 1e-6f, $"{e}°에서 위로 갈수록 짙어졌다");
                Assert.LessOrEqual(prev - v, 0.01f, $"{e}°에서 짙기가 튄다");
                prev = v;
            }
        }

        [Test]
        public void OvercastSky_IsGreyerThanClear_AndNeverWarmer()
        {
            // 흐린 날 하늘(연무 막 = 안개색, 섬은 배경색도)이 맑은 날보다 채도가 낮아야 회색으로 읽힌다. 그리고 맑은 날보다
            // 따뜻(붉음)해지면 안 된다 — 2026-10-03 캡처에서 흐린 정오의 지평선이 해 질 녘처럼 주황으로 찍혔다.
            var bases = AllFieldBases();
            bases.Add(new KeyValuePair<string, WorldSkyRules.LightingState>("island", IslandBase()));
            foreach (KeyValuePair<string, WorldSkyRules.LightingState> kv in bases)
            {
                bool field = kv.Key != "island";
                foreach (float h in new[] { 0f, 6.5f, 12f, 15f, 19f })
                {
                    WorldSkyRules.LightingState clear = WorldSkyRules.Apply(kv.Value, WorldSkyRules.Evaluate(h, WeatherType.Clear), field);
                    foreach (WeatherType w in OvercastWeathers)
                    {
                        WorldSkyRules.LightingState o = WorldSkyRules.Apply(kv.Value, WorldSkyRules.Evaluate(h, w), field);
                        string tag = $"{kv.Key} {w} {h}시";
                        Assert.LessOrEqual(Saturation(o.fogColor), Saturation(clear.fogColor) * 0.5f + 0.01f, tag + " 하늘(안개색)이 회색이 아니다");
                        Assert.LessOrEqual(Saturation(o.background), Saturation(clear.background) * 0.5f + 0.01f, tag + " 배경 하늘이 회색이 아니다");
                        Assert.LessOrEqual(o.fogColor.r - o.fogColor.b, Mathf.Max(0f, clear.fogColor.r - clear.fogColor.b) + 1e-4f,
                            tag + " 흐린 하늘이 맑은 하늘보다 붉다");
                        Assert.LessOrEqual(WorldSkyRules.Luminance(o.fogColor), WorldSkyRules.Luminance(clear.fogColor) + 1e-4f,
                            tag + " 흐린 하늘이 맑은 하늘보다 밝다");
                    }
                }
            }
        }

        [Test]
        public void SkyVisuals_NeverThickensTheProceduralAtmosphere()
        {
            // 절차적 스카이박스의 대기 두께를 올리면 뿌예지는 게 아니라 지평선이 주황이 된다(레일리 계수가 두께의 2.5제곱).
            // 흐림은 연무 돔이 맡는다 — 두께 속성을 다시 만지면 이 검사가 잡는다.
            string src = File.ReadAllText(Path.Combine(Application.dataPath, "..", "Assets/Scripts/Core/WorldSkyVisuals.cs"));
            StringAssert.DoesNotContain("\"_AtmosphereThickness\"", src, "스카이박스 대기 두께를 바꾸고 있다 — 흐린 날 지평선이 노을처럼 붉어진다");
            StringAssert.Contains("WorldSkyRules.VeilProfile", src, "연무 돔이 규칙의 세로 분포를 쓰지 않는다");
        }
    }
}
#endif
