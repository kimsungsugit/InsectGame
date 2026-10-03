#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 곤충 시간·날씨 성향(<see cref="InsectHabits"/>) — 표의 정합, 분포 대역, 순수 규칙, 스폰 연동.
    ///
    /// 실패가 전부 조용한 계열이다: 표에서 종 하나가 빠지면 그 종만 조용히 성향이 없어지고(중립), 배수가 0이 되면 후보가
    /// 소리 없이 줄고, 한 날씨에 풀 대부분이 불리해져도 예외가 없다. 그래서 진짜 곤충 DB·진짜 리전 풀로 센다.
    /// </summary>
    [TestFixture]
    public class InsectHabitsTests
    {
        private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int Sweep = 4000;

        private static readonly DayPhase[] Phases = (DayPhase[])System.Enum.GetValues(typeof(DayPhase));
        private static readonly WeatherType[] Weathers = (WeatherType[])System.Enum.GetValues(typeof(WeatherType));

        // 진짜 DB는 한 번만 만든다(종 194개의 기술표까지 짓는 일이라 테스트마다 짓기엔 무겁다).
        private readonly List<Object> shared = new List<Object>();
        private readonly List<Object> perTest = new List<Object>();
        private InsectDatabase db;

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in perTest) if (o != null) Object.DestroyImmediate(o);
            perTest.Clear();
        }

        [OneTimeTearDown]
        public void ReleaseSharedDatabase()
        {
            foreach (Object o in shared) if (o != null) Object.DestroyImmediate(o);
            shared.Clear();
            db = null;
        }

        private InsectDatabase RealDatabase()
        {
            if (db != null) return db;
            var host = new GameObject("InsectHabitsTests_RealDb");
            host.SetActive(false);   // Awake(월드 생성)를 막고 private 생성 함수만 부른다
            shared.Add(host);
            var bootstrap = host.AddComponent<PlaySceneBootstrap>();
            db = (InsectDatabase)typeof(PlaySceneBootstrap).GetMethod("EnsureExpandedDatabase", Inst).Invoke(bootstrap, null);
            Assert.IsNotNull(db);
            shared.Add(db);
            return db;
        }

        private static InsectHabit Habit(InsectActivity activity, WeatherSet like = WeatherSet.None,
            WeatherSet dislike = WeatherSet.None, InsectTemperament temperament = InsectTemperament.Docile)
            => new InsectHabit(activity, like, dislike, temperament);

        private static WorldState State(DayPhase phase, WeatherType weather)
            => new WorldState { DayPhase = phase, Weather = weather, Hour24 = 12 };

        // ── 표 정합 ──

        [Test]
        public void Table_CoversEverySpeciesInTheRealDatabase_AndNothingElse()
        {
            var dbIds = new HashSet<string>();
            foreach (InsectData d in RealDatabase().insects) dbIds.Add(d.insectId);

            var missing = new List<string>();
            foreach (string id in dbIds) if (!InsectHabits.TryGet(id, out _)) missing.Add(id);
            var stale = new List<string>();
            foreach (string id in InsectHabits.AllIds) if (!dbIds.Contains(id)) stale.Add(id);

            Assert.AreEqual(0, missing.Count, "성향이 없는 종(조용히 중립이 된다): " + string.Join(", ", missing));
            Assert.AreEqual(0, stale.Count, "DB에 없는 종의 줄(ID 오타·삭제된 종): " + string.Join(", ", stale));
            Assert.AreEqual(dbIds.Count, InsectHabits.Count);
        }

        [Test]
        public void Table_HasNoDuplicateRows()
        {
            // 같은 종을 두 줄 적으면 뒤 줄이 앞 줄을 조용히 덮는다.
            Assert.AreEqual(InsectHabits.Count, InsectHabits.AuthoredRows, "같은 종이 두 줄 이상 적혔다");
        }

        [Test]
        public void SeedDefinitions_AllHaveHabits_WithoutTheDatabase()
        {
            // 진짜 DB 없이도 도는 빠른 검사 — 확장 시드(1막·2막)의 ID 전부.
            foreach (string id in InsectExpansionDefinitions.AllNewIds())
                Assert.IsTrue(InsectHabits.TryGet(id, out _), $"1막 확장 시드 '{id}'의 성향이 없다");
            foreach (string id in InsectExpansion2Definitions.AllNewIds())
                Assert.IsTrue(InsectHabits.TryGet(id, out _), $"2막 확장 시드 '{id}'의 성향이 없다");
        }

        [Test]
        public void UnknownSpecies_IsNeutral_AndNeverLosesItsSpawnWeight()
        {
            Assert.IsFalse(InsectHabits.TryGet("no_such_insect", out _));
            Assert.IsFalse(InsectHabits.TryGet(null, out _));
            InsectHabit h = InsectHabits.Of("no_such_insect");
            Assert.AreEqual(InsectActivity.Any, h.Activity);
            Assert.AreEqual(InsectTemperament.Docile, h.Temperament);
            foreach (DayPhase p in Phases)
                foreach (WeatherType w in Weathers)
                {
                    Assert.AreEqual(1f, InsectHabits.SpawnWeightMultiplier(h, State(p, w)), 1e-6f);
                    Assert.AreEqual(0, InsectHabits.Fit(h, State(p, w)));
                    Assert.AreEqual(1f, InsectHabits.BattleStatMultiplier(h, State(p, w)), 1e-6f);
                    Assert.IsFalse(InsectHabits.IsAmbusher(h, State(p, w)));
                }
        }

        [Test]
        public void Of_AndFor_AgreeWithTryGet()
        {
            foreach (string id in InsectHabits.AllIds)
            {
                Assert.IsTrue(InsectHabits.TryGet(id, out InsectHabit h));
                Assert.AreEqual(h.Activity, InsectHabits.Of(id).Activity, id);
                Assert.AreEqual(h.LikedWeather, InsectHabits.Of(id).LikedWeather, id);
            }
            Assert.AreEqual(InsectHabit.Neutral.Activity, InsectHabits.For(null).Activity);
        }

        // ── 분포 대역 ──

        [Test]
        public void ActivityShares_StayInBand()
        {
            int diurnal = 0, nocturnal = 0, any = 0, total = 0;
            foreach (InsectData d in RealDatabase().insects)
            {
                total++;
                switch (InsectHabits.For(d).Activity)
                {
                    case InsectActivity.Diurnal: diurnal++; break;
                    case InsectActivity.Nocturnal: nocturnal++; break;
                    default: any++; break;
                }
            }
            Assert.That(diurnal / (float)total, Is.InRange(0.30f, 0.45f), $"주행성 {diurnal}/{total}");
            Assert.That(nocturnal / (float)total, Is.InRange(0.25f, 0.40f), $"야행성 {nocturnal}/{total}");
            Assert.That(any / (float)total, Is.InRange(0.15f, 0.35f), $"시간 무관 {any}/{total}");
        }

        [Test]
        public void Ambushers_AreAboutOneInEight_AndGrowWithRarity()
        {
            int rarities = System.Enum.GetValues(typeof(InsectRarity)).Length;
            var total = new int[rarities];
            var ambush = new int[rarities];
            int allTotal = 0, allAmbush = 0, fieldTotal = 0, fieldAmbush = 0;
            foreach (InsectData d in RealDatabase().insects)
            {
                bool a = InsectHabits.For(d).Temperament == InsectTemperament.Ambusher;
                int r = (int)d.rarity;
                total[r]++;
                allTotal++;
                if (a) { ambush[r]++; allAmbush++; }
                if (d.spawnWeight > 0f) { fieldTotal++; if (a) fieldAmbush++; }
            }

            Assert.That(allAmbush / (float)allTotal, Is.InRange(0.10f, 0.15f), $"습격형 {allAmbush}/{allTotal}");
            Assert.That(fieldAmbush / (float)fieldTotal, Is.InRange(0.10f, 0.15f),
                $"필드에 나오는 종 중 습격형 {fieldAmbush}/{fieldTotal}");
            for (int r = 1; r < rarities; r++)
                Assert.GreaterOrEqual(ambush[r] / (float)total[r], ambush[r - 1] / (float)total[r - 1],
                    $"등급이 높을수록 습격형이 많아야 한다 — {(InsectRarity)r}가 {(InsectRarity)(r - 1)}보다 적다");
        }

        [Test]
        public void EveryAmbusher_HasAnActivityWindow_AndGachaOnlySpeciesAreDocile()
        {
            foreach (InsectData d in RealDatabase().insects)
            {
                InsectHabit h = InsectHabits.For(d);
                if (h.Temperament == InsectTemperament.Ambusher)
                    Assert.AreNotEqual(InsectActivity.Any, h.Activity,
                        $"{d.insectId}: 시간 무관 습격형은 깨는 때가 좋아하는 날씨뿐이다 — 활동 시간대를 정할 것");
                if (d.spawnWeight <= 0f)
                    Assert.AreEqual(InsectTemperament.Docile, h.Temperament, $"{d.insectId}: 필드에 안 나오는 종이 습격형이다");
            }
        }

        // ── 날씨 균형 ──

        [Test]
        public void LikedAndDislikedWeather_NeverOverlap()
        {
            foreach (string id in InsectHabits.AllIds)
            {
                InsectHabit h = InsectHabits.Of(id);
                Assert.AreEqual(WeatherSet.None, h.LikedWeather & h.DislikedWeather, $"{id}: 같은 날씨를 좋아하면서 싫어한다");
            }
        }

        [Test]
        public void NoWeather_IsUnfavorableToMoreThanAThirdOfAllSpecies_AndEachHasFriends()
        {
            int total = InsectHabits.Count;
            foreach (WeatherType w in Weathers)
            {
                int liked = 0, disliked = 0;
                foreach (string id in InsectHabits.AllIds)
                {
                    InsectHabit h = InsectHabits.Of(id);
                    if (InsectHabits.Contains(h.LikedWeather, w)) liked++;
                    if (InsectHabits.Contains(h.DislikedWeather, w)) disliked++;
                }
                Assert.LessOrEqual(disliked / (float)total, 1f / 3f, $"{w}: 싫어하는 종 {disliked}/{total}");
                Assert.GreaterOrEqual(liked / (float)total, 0.08f, $"{w}: 좋아하는 종 {liked}/{total} — 이 날씨가 아무에게도 좋지 않다");
            }
        }

        [Test]
        public void NoRegionPool_HasAWeatherThatHurtsMostOfIt()
        {
            // 전체로는 균형이어도 풀 단위로 쏠릴 수 있다 — 정원 풀(나비·벌 17종)은 처음에 15종이 비에 불리했다.
            foreach (RegionData region in RegionDefinitions.CreateAll())
            {
                if (region.insectIds == null || region.insectIds.Length == 0) continue;
                foreach (WeatherType w in Weathers)
                {
                    int disliked = 0;
                    foreach (string id in region.insectIds)
                        if (InsectHabits.Contains(InsectHabits.Of(id).DislikedWeather, w)) disliked++;
                    Assert.Less(disliked / (float)region.insectIds.Length, 0.5f,
                        $"{region.regionId} 풀: {w}이 {disliked}/{region.insectIds.Length}종에 불리하다(절반 이상)");
                }
            }
        }

        [Test]
        public void WaterTypesNeverDislikeRain_WindTypesLikeStrongWind()
        {
            // 사용자 근거 예: 물 속성은 비를 좋아하고 바람 속성은 센바람. 속성은 부트스트랩이 이름·서식지로 정한다 —
            // 속성이 바뀌거나 표가 어긋나면 "물 곤충이 비에 약해지는" 일이 조용히 생긴다.
            foreach (InsectData d in RealDatabase().insects)
            {
                InsectHabit h = InsectHabits.For(d);
                if (d.primaryType == InsectElement.Water)
                    Assert.IsFalse(InsectHabits.Contains(h.DislikedWeather, WeatherType.Rain), $"{d.insectId}: 물 속성이 비를 싫어한다");
                if (d.primaryType == InsectElement.Wind)
                {
                    Assert.IsTrue(InsectHabits.Contains(h.LikedWeather, WeatherType.Wind), $"{d.insectId}: 바람 속성이 센바람을 좋아하지 않는다");
                    Assert.IsFalse(InsectHabits.Contains(h.DislikedWeather, WeatherType.Wind), $"{d.insectId}: 바람 속성이 센바람을 싫어한다");
                }
            }
        }

        [Test]
        public void SnowHabitats_FrostlineAndMountain_LikeSnow()
        {
            foreach (string habitat in new[] { "Frostline", "Mountain" })
            {
                int species = 0, liking = 0;
                foreach (InsectData d in RealDatabase().insects)
                {
                    if (d.habitatHint != habitat) continue;
                    species++;
                    InsectHabit h = InsectHabits.For(d);
                    if (InsectHabits.Contains(h.LikedWeather, WeatherType.Snow)) liking++;
                    Assert.IsFalse(InsectHabits.Contains(h.DislikedWeather, WeatherType.Snow), $"{d.insectId}: 설산 종이 눈을 싫어한다");
                }
                Assert.Greater(species, 0, habitat);
                Assert.GreaterOrEqual(liking / (float)species, 0.7f, $"{habitat} 서식종 중 눈을 좋아하는 종 {liking}/{species}");
            }
        }

        // ── 순수 규칙 ──

        [Test]
        public void WeatherSet_BitsFollowWeatherTypeValues()
        {
            foreach (WeatherType w in Weathers)
            {
                Assert.AreEqual((WeatherSet)(1 << (int)w), InsectHabits.SetOf(w), $"{w}");
                Assert.IsTrue(InsectHabits.Contains(InsectHabits.SetOf(w), w));
                foreach (WeatherType other in Weathers)
                    if (other != w) Assert.IsFalse(InsectHabits.Contains(InsectHabits.SetOf(w), other));
            }
            // 날씨를 늘리면 집합 비트도 함께 — 정의된 비트 수가 날씨 수와 같아야 한다.
            int bits = 0;
            foreach (WeatherSet s in System.Enum.GetValues(typeof(WeatherSet))) if (s != WeatherSet.None) bits++;
            Assert.AreEqual(Weathers.Length, bits, "WeatherType과 WeatherSet이 어긋났다");
            Assert.AreEqual(WeatherSet.Clear, InsectHabits.SetOf(WeatherType.Clear));
            Assert.AreEqual(WeatherSet.Snow, InsectHabits.SetOf(WeatherType.Snow));
        }

        [Test]
        public void TimeMultiplier_Table_MatchesTheRequestedRanges()
        {
            Assert.That(InsectHabits.TimeMultiplier(InsectActivity.Nocturnal, DayPhase.Night), Is.InRange(2.0f, 2.5f));
            Assert.That(InsectHabits.TimeMultiplier(InsectActivity.Nocturnal, DayPhase.Day), Is.InRange(0.3f, 0.4f));
            Assert.Greater(InsectHabits.TimeMultiplier(InsectActivity.Diurnal, DayPhase.Day), 1f);
            Assert.Less(InsectHabits.TimeMultiplier(InsectActivity.Diurnal, DayPhase.Night), 1f);
            foreach (DayPhase p in Phases)
                Assert.AreEqual(1f, InsectHabits.TimeMultiplier(InsectActivity.Any, p), $"시간 무관이 {p}에 배수를 받는다");
            Assert.AreEqual(1.5f, InsectHabits.FavorableWeatherMultiplier);
            Assert.AreEqual(0.6f, InsectHabits.UnfavorableWeatherMultiplier);
        }

        [Test]
        public void SpawnWeightMultiplier_IsNeverZero_AndStaysInsideTheClamp()
        {
            foreach (string id in InsectHabits.AllIds)
                foreach (DayPhase p in Phases)
                    foreach (WeatherType w in Weathers)
                    {
                        float m = InsectHabits.SpawnWeightMultiplier(InsectHabits.Of(id), State(p, w));
                        Assert.Greater(m, 0f, $"{id} @ {p}/{w}: 배수가 0이다 — 후보에서 빠진다");
                        Assert.GreaterOrEqual(m, InsectHabits.MinSpawnMultiplier, $"{id} @ {p}/{w}");
                        Assert.LessOrEqual(m, InsectHabits.MaxSpawnMultiplier, $"{id} @ {p}/{w}");
                    }
            Assert.Greater(InsectHabits.MinSpawnMultiplier, 0f);
        }

        [Test]
        public void SpawnWeightMultiplier_NocturnalSurgesAtNightAndSleepsByDay_WeatherStacksOnTop()
        {
            InsectHabit moth = Habit(InsectActivity.Nocturnal, WeatherSet.Fog, WeatherSet.Rain);
            Assert.AreEqual(2.5f, InsectHabits.SpawnWeightMultiplier(moth, State(DayPhase.Night, WeatherType.Clear)), 1e-5f);
            Assert.AreEqual(0.35f, InsectHabits.SpawnWeightMultiplier(moth, State(DayPhase.Day, WeatherType.Clear)), 1e-5f);
            Assert.AreEqual(2.5f * 1.5f, InsectHabits.SpawnWeightMultiplier(moth, State(DayPhase.Night, WeatherType.Fog)), 1e-5f);
            Assert.AreEqual(0.35f * 0.6f, InsectHabits.SpawnWeightMultiplier(moth, State(DayPhase.Day, WeatherType.Rain)), 1e-5f);
            Assert.AreEqual(1.5f, InsectHabits.SpawnWeightMultiplier(Habit(InsectActivity.Any, WeatherSet.Fog),
                State(DayPhase.Day, WeatherType.Fog)), 1e-5f);
        }

        [Test]
        public void Fit_CombinesTimeAndWeather_IntoMinusOneZeroPlusOne()
        {
            InsectHabit moth = Habit(InsectActivity.Nocturnal, WeatherSet.Fog, WeatherSet.Rain);
            Assert.AreEqual(1, InsectHabits.Fit(moth, State(DayPhase.Night, WeatherType.Fog)), "제 시간 + 좋은 날씨");
            Assert.AreEqual(1, InsectHabits.Fit(moth, State(DayPhase.Night, WeatherType.Clear)), "제 시간 + 보통 날씨");
            Assert.AreEqual(0, InsectHabits.Fit(moth, State(DayPhase.Night, WeatherType.Rain)), "제 시간 + 싫은 날씨는 서로 지운다");
            Assert.AreEqual(0, InsectHabits.Fit(moth, State(DayPhase.Day, WeatherType.Fog)), "잘 시간 + 좋은 날씨는 서로 지운다");
            Assert.AreEqual(-1, InsectHabits.Fit(moth, State(DayPhase.Day, WeatherType.Rain)), "잘 시간 + 싫은 날씨");
            Assert.AreEqual(-1, InsectHabits.Fit(moth, State(DayPhase.Morning, WeatherType.Clear)), "잘 시간");
            Assert.AreEqual(0, InsectHabits.Fit(moth, State(DayPhase.Evening, WeatherType.Clear)), "어스름은 보통");
            Assert.AreEqual(1, InsectHabits.Fit(moth, State(DayPhase.Evening, WeatherType.Fog)), "어스름 + 좋은 날씨");
        }

        [Test]
        public void BattleStatMultiplier_IsPlusTenMinusTenOrOne()
        {
            InsectHabit moth = Habit(InsectActivity.Nocturnal, WeatherSet.Fog, WeatherSet.Rain);
            Assert.AreEqual(1.10f, InsectHabits.BattleStatMultiplier(moth, State(DayPhase.Night, WeatherType.Fog)), 1e-6f);
            Assert.AreEqual(0.90f, InsectHabits.BattleStatMultiplier(moth, State(DayPhase.Day, WeatherType.Rain)), 1e-6f);
            Assert.AreEqual(1f, InsectHabits.BattleStatMultiplier(moth, State(DayPhase.Night, WeatherType.Rain)), 1e-6f);
            Assert.AreEqual(1f, InsectHabits.BattleStatMultiplier(InsectHabit.Neutral, State(DayPhase.Night, WeatherType.Fog)), 1e-6f);
        }

        [Test]
        public void IsAmbusher_WakesOnlyAfterDark_AndStaysQuietInWeatherItHates()
        {
            InsectHabit nightHunter = Habit(InsectActivity.Nocturnal, WeatherSet.Fog, WeatherSet.Rain, InsectTemperament.Ambusher);
            Assert.IsTrue(InsectHabits.IsAmbusher(nightHunter, State(DayPhase.Night, WeatherType.Clear)));
            Assert.IsFalse(InsectHabits.IsAmbusher(nightHunter, State(DayPhase.Day, WeatherType.Clear)), "낮에 야행성 습격형이 깨어 있다");
            Assert.IsFalse(InsectHabits.IsAmbusher(nightHunter, State(DayPhase.Day, WeatherType.Fog)), "좋아하는 날씨여도 낮에는 덤비지 않는다");
            Assert.IsFalse(InsectHabits.IsAmbusher(nightHunter, State(DayPhase.Evening, WeatherType.Clear)), "어스름은 아직이다");
            Assert.IsTrue(InsectHabits.IsAmbusher(nightHunter, State(DayPhase.Evening, WeatherType.Fog)), "안개 낀 저녁에는 일찍 깬다");
            Assert.IsFalse(InsectHabits.IsAmbusher(nightHunter, State(DayPhase.Night, WeatherType.Rain)), "싫은 날씨엔 제 시간이어도 가만히 있는다");

            // 주행성 습격형(말벌류)은 한낮에 덤비지 않는다 — 해 질 녘에 좋아하는 날씨일 때만이다.
            InsectHabit dayHunter = Habit(InsectActivity.Diurnal, WeatherSet.Wind, WeatherSet.None, InsectTemperament.Ambusher);
            foreach (WeatherType w in Weathers)
            {
                Assert.IsFalse(InsectHabits.IsAmbusher(dayHunter, State(DayPhase.Morning, w)), $"아침 {w}에 싸움을 건다");
                Assert.IsFalse(InsectHabits.IsAmbusher(dayHunter, State(DayPhase.Day, w)), $"낮 {w}에 싸움을 건다");
                Assert.IsFalse(InsectHabits.IsAmbusher(dayHunter, State(DayPhase.Night, w)), $"잠든 밤 {w}에 싸움을 건다");
            }
            Assert.IsTrue(InsectHabits.IsAmbusher(dayHunter, State(DayPhase.Evening, WeatherType.Wind)));
            Assert.IsFalse(InsectHabits.IsAmbusher(dayHunter, State(DayPhase.Evening, WeatherType.Clear)));

            InsectHabit docile = Habit(InsectActivity.Nocturnal, WeatherSet.Fog);
            foreach (DayPhase p in Phases)
                foreach (WeatherType w in Weathers)
                    Assert.IsFalse(InsectHabits.IsAmbusher(docile, State(p, w)), "온순한 종이 습격한다");

            InsectHabit anyHunter = Habit(InsectActivity.Any, WeatherSet.Fog, WeatherSet.None, InsectTemperament.Ambusher);
            Assert.IsFalse(InsectHabits.IsAmbusher(anyHunter, State(DayPhase.Day, WeatherType.Fog)), "시간 무관이어도 낮에는 덤비지 않는다");
            Assert.IsTrue(InsectHabits.IsAmbusher(anyHunter, State(DayPhase.Night, WeatherType.Fog)), "시간 무관 습격형은 어두울 때 좋아하는 날씨에만 깬다");
            Assert.IsFalse(InsectHabits.IsAmbusher(anyHunter, State(DayPhase.Night, WeatherType.Clear)));
        }

        [Test]
        public void IsAmbushHour_IsEveningAndNightOnly()
        {
            Assert.IsFalse(InsectHabits.IsAmbushHour(DayPhase.Morning));
            Assert.IsFalse(InsectHabits.IsAmbushHour(DayPhase.Day));
            Assert.IsTrue(InsectHabits.IsAmbushHour(DayPhase.Evening));
            Assert.IsTrue(InsectHabits.IsAmbushHour(DayPhase.Night));
        }

        [Test]
        public void IsOutOfPhase_MarksOnlyTheSleepers()
        {
            InsectHabit night = Habit(InsectActivity.Nocturnal);
            InsectHabit day = Habit(InsectActivity.Diurnal);
            Assert.IsTrue(InsectHabits.IsOutOfPhase(night, DayPhase.Morning));
            Assert.IsTrue(InsectHabits.IsOutOfPhase(night, DayPhase.Day));
            Assert.IsFalse(InsectHabits.IsOutOfPhase(night, DayPhase.Evening));
            Assert.IsFalse(InsectHabits.IsOutOfPhase(night, DayPhase.Night));
            Assert.IsTrue(InsectHabits.IsOutOfPhase(day, DayPhase.Night));
            Assert.IsFalse(InsectHabits.IsOutOfPhase(day, DayPhase.Morning));
            Assert.IsFalse(InsectHabits.IsOutOfPhase(day, DayPhase.Day));
            Assert.IsFalse(InsectHabits.IsOutOfPhase(day, DayPhase.Evening));
            foreach (DayPhase p in Phases)
                Assert.IsFalse(InsectHabits.IsOutOfPhase(InsectHabit.Neutral, p), "시간 무관 종이 잘 시간이다");
        }

        // ── 스폰 구성 — 진짜 리전 풀 ──

        private List<InsectData> PoolOf(RegionData region)
        {
            var pool = new List<InsectData>();
            foreach (string id in region.insectIds)
            {
                InsectData d = RealDatabase().GetById(id);
                Assert.IsNotNull(d, $"{region.regionId} 풀의 '{id}'가 DB에 없다");
                pool.Add(d);
            }
            return pool;
        }

        [Test]
        public void EveryRegionPool_KeepsEveryCandidate_AtEveryTimeAndWeather()
        {
            // 어떤 리전 풀·어떤 시간·날씨에서도 후보가 비지 않고(성향 배수가 0이 아니다), 풀에 있는 모든 등급이 남는다.
            // 한 등급이 통째로 비면 등급 대체가 돌아 등급 분포가 시간대를 따라 움직인다.
            InsectDatabase database = RealDatabase();
            foreach (RegionData region in RegionDefinitions.CreateAll())
            {
                if (region.insectIds == null || region.insectIds.Length == 0) continue;
                List<InsectData> pool = PoolOf(region);
                foreach (DayPhase p in Phases)
                    foreach (WeatherType raw in Weathers)
                    {
                        // 스포너와 같다 — 그 리전에서 보이는 날씨(설산은 비가 눈, 사막은 눈이 센바람).
                        WorldState state = State(p, WeatherForecast.EffectiveIn(raw, region.regionId));
                        List<InsectData> stateCandidates = database.GetCandidates(state);
                        var rarityKept = new HashSet<InsectRarity>();
                        var rarityInPool = new HashSet<InsectRarity>();
                        int candidates = 0;
                        foreach (InsectData d in pool)
                        {
                            rarityInPool.Add(d.rarity);
                            if (!stateCandidates.Contains(d)) continue;
                            candidates++;
                            rarityKept.Add(d.rarity);
                            Assert.Greater(InsectHabits.SpawnWeightMultiplier(InsectHabits.For(d), state), 0f,
                                $"{region.regionId} {p}/{state.Weather}: {d.insectId}의 배수가 0이다");
                        }
                        Assert.Greater(candidates, 0, $"{region.regionId} {p}/{state.Weather}: 후보가 비었다");
                        Assert.AreEqual(rarityInPool.Count, rarityKept.Count, $"{region.regionId} {p}/{state.Weather}: 등급 하나가 통째로 빠졌다");
                    }
            }
        }

        /// <summary>스포너와 같은 순서 — 등급을 먼저 굴리고(전역 표), 그 등급 후보 안에서 spawnWeight × 성향 배수.</summary>
        private static InsectData PickLikeTheSpawner(List<InsectData> pool, WorldState state, bool[] available,
            float roll, List<InsectData> scratch, List<float> multipliers)
        {
            int rarity = FieldSpawnRules.PickRarity(roll, 1f, available);
            scratch.Clear();
            multipliers.Clear();
            foreach (InsectData d in pool)
            {
                if ((int)d.rarity != rarity) continue;
                scratch.Add(d);
                multipliers.Add(InsectHabits.SpawnWeightMultiplier(InsectHabits.For(d), state));
            }
            return FieldSpawnRules.PickWeighted(scratch, multipliers, Mathf.Repeat(roll * 7.31f, 1f));
        }

        [Test]
        public void RarityShares_DoNotMoveWithTimeOrWeather_WhenPickingLikeTheSpawner()
        {
            var scratch = new List<InsectData>();
            var multipliers = new List<float>();
            foreach (RegionData region in RegionDefinitions.CreateAll())
            {
                if (region.regionId != "forest" && region.regionId != "garden" && region.regionId != "swamp") continue;
                List<InsectData> pool = PoolOf(region);
                var available = new bool[FieldSpawnRules.RarityCount];
                foreach (InsectData d in pool) available[(int)d.rarity] = true;
                float[] expect = FieldSpawnRules.EffectiveShares(1f, available);

                foreach (DayPhase p in Phases)
                    foreach (WeatherType w in Weathers)
                    {
                        var counts = new float[FieldSpawnRules.RarityCount];
                        for (int i = 0; i < Sweep; i++)
                            counts[(int)PickLikeTheSpawner(pool, State(p, w), available, (i + 0.5f) / Sweep, scratch, multipliers).rarity]
                                += 1f / Sweep;
                        for (int r = 0; r < FieldSpawnRules.RarityCount; r++)
                            Assert.AreEqual(expect[r], counts[r], 0.002f, $"{region.regionId} {p}/{w}: {(InsectRarity)r}");
                    }
            }
        }

        private static float NocturnalPickShare(List<InsectData> pool, WorldState state)
        {
            var scratch = new List<InsectData>();
            var multipliers = new List<float>();
            var available = new bool[FieldSpawnRules.RarityCount];
            foreach (InsectData d in pool) available[(int)d.rarity] = true;
            int nocturnal = 0;
            for (int i = 0; i < Sweep; i++)
            {
                InsectData picked = PickLikeTheSpawner(pool, state, available, (i + 0.5f) / Sweep, scratch, multipliers);
                if (InsectHabits.For(picked).Activity == InsectActivity.Nocturnal) nocturnal++;
            }
            return nocturnal / (float)Sweep;
        }

        [Test]
        public void NightShiftsTheForestAndMeadowMix_TowardNocturnalSpecies()
        {
            foreach (RegionData region in RegionDefinitions.CreateAll())
            {
                if (region.regionId != "forest" && region.regionId != "meadow") continue;
                List<InsectData> pool = PoolOf(region);
                float night = NocturnalPickShare(pool, State(DayPhase.Night, WeatherType.Clear));
                float day = NocturnalPickShare(pool, State(DayPhase.Day, WeatherType.Clear));
                Assert.Greater(night - day, 0.3f, $"{region.regionId}: 야행성 비율이 밤 {night:P0} / 낮 {day:P0} — 시간을 못 탄다");
            }
        }

        // ── 스포너 연동 — 시계 신호 · 보너스 슬롯 ──

        private InsectSpawner NewSpawner(out GameClock clock, out WeatherSystem weather)
        {
            var world = new GameObject("InsectHabitsTests_World");
            perTest.Add(world);
            clock = world.AddComponent<GameClock>();
            weather = world.AddComponent<WeatherSystem>();
            var provider = world.AddComponent<WorldStateProvider>();
            provider.AutoWire(clock, weather);

            var host = new GameObject("InsectHabitsTests_Spawner");
            perTest.Add(host);
            InsectSpawner spawner = host.AddComponent<InsectSpawner>();
            spawner.AutoWire(null, provider, null);
            return spawner;
        }

        private static FieldPopulation PopulationOf(InsectSpawner spawner)
            => (FieldPopulation)typeof(InsectSpawner).GetField("population", Inst).GetValue(spawner);

        /// <summary>리전 표 한 줄(스포너 안쪽 클래스)을 만들어 스포너에 단다.</summary>
        private static object AddRegion(InsectSpawner spawner, string regionId, int baseSlots)
        {
            System.Type infoType = typeof(InsectSpawner).GetNestedType("RegionSpawnInfo", BindingFlags.NonPublic);
            object info = System.Activator.CreateInstance(infoType, true);
            infoType.GetField("RegionId").SetValue(info, regionId);
            infoType.GetField("BaseSlots").SetValue(info, baseSlots);
            ((System.Collections.IList)typeof(InsectSpawner).GetField("regionInfos", Inst).GetValue(spawner)).Add(info);
            return info;
        }

        private static int RegionCap(InsectSpawner spawner, object info)
            => (int)typeof(InsectSpawner).GetMethod("RegionCap", Inst).Invoke(spawner, new[] { info });

        [Test]
        public void Spawner_PullsOnlyOutOfPhaseSlotsForward_WhenTheClockChangesPhase()
        {
            InsectSpawner spawner = NewSpawner(out GameClock clock, out _);
            AddRegion(spawner, "meadow", 10);
            clock.SetTime01(0.5f, hold: true);   // 낮 — 슬롯을 채우기 전에 시각을 맞춘다(아침→낮 전환도 신호를 쏜다)

            FieldPopulation pop = PopulationOf(spawner);
            pop.EnsureSlotCount("meadow", 2, 0f, isSubArea: false);
            List<FieldSlot> slots = pop.SlotsOf("meadow");
            float far = Time.time + 1000f;
            FieldPopulation.Fill(slots[0], "bee_worker", null, 3, false, false, Vector3.zero, far);    // 주행성
            FieldPopulation.Fill(slots[1], "moth_night", null, 3, false, false, Vector3.zero, far);    // 야행성

            float t0 = Time.time;
            clock.SetTime01(0.95f, hold: true);   // 밤 — 주행성이 잠든다
            Assert.GreaterOrEqual(slots[0].ExpiresAt, t0 + FieldSpawnRules.PhaseSwapDelayMin, "5초보다 일찍 바뀐다");
            Assert.LessOrEqual(slots[0].ExpiresAt, t0 + FieldSpawnRules.PhaseSwapDelayMax + 0.01f, "60초 안에 안 바뀐다");
            Assert.AreEqual(far, slots[1].ExpiresAt, "밤에 깨어나는 야행성의 수명을 당겼다");
            Assert.IsTrue(slots[0].IsAlive && slots[1].IsAlive, "개체를 지우면 안 된다 — 만료만 당긴다");

            // 아침이 오면 거꾸로다 — 야행성이 잠들고 주행성은 그대로.
            FieldPopulation.Fill(slots[0], "bee_worker", null, 3, false, false, Vector3.zero, far);
            t0 = Time.time;
            clock.SetTime01(0.3f, hold: true);
            Assert.AreEqual(far, slots[0].ExpiresAt, "아침에 주행성을 당겼다");
            Assert.GreaterOrEqual(slots[1].ExpiresAt, t0 + FieldSpawnRules.PhaseSwapDelayMin);
            Assert.LessOrEqual(slots[1].ExpiresAt, t0 + FieldSpawnRules.PhaseSwapDelayMax + 0.01f);
        }

        [Test]
        public void Spawner_StopsListeningWhenDisabled_AndListensAgainWhenEnabled()
        {
            InsectSpawner spawner = NewSpawner(out GameClock clock, out _);
            AddRegion(spawner, "meadow", 10);
            clock.SetTime01(0.95f, hold: true);   // 밤
            FieldPopulation pop = PopulationOf(spawner);
            pop.EnsureSlotCount("meadow", 1, 0f, isSubArea: false);
            FieldSlot slot = pop.SlotsOf("meadow")[0];
            float far = Time.time + 1000f;
            FieldPopulation.Fill(slot, "moth_night", null, 3, false, false, Vector3.zero, far);   // 야행성

            spawner.enabled = false;
            clock.SetTime01(0.3f, hold: true);    // 아침 — 구독이 없으니 당기지 않는다
            Assert.AreEqual(far, slot.ExpiresAt, "꺼진 스포너가 시계 신호를 받았다(해지 누락)");

            spawner.enabled = true;               // OnEnable이 시계를 다시 구독한다
            clock.SetTime01(0.95f, hold: true);   // 밤
            clock.SetTime01(0.3f, hold: true);    // 아침
            Assert.Less(slot.ExpiresAt, far, "다시 켠 스포너가 시계 신호를 못 받는다(재구독 누락)");
        }

        [Test]
        public void Spawner_RegionCap_AddsBonusSlotsFromTheRegionsOwnWeather()
        {
            InsectSpawner spawner = NewSpawner(out GameClock clock, out WeatherSystem weather);
            object meadow = AddRegion(spawner, "meadow", 20);
            object frostline = AddRegion(spawner, "frostline", 20);

            clock.SetTime01(0.5f, hold: true);                       // 낮
            weather.SetWeather(WeatherType.Clear, hold: true, instant: true);
            Assert.AreEqual(20, RegionCap(spawner, meadow), "낮·맑음은 보너스가 없다");

            clock.SetTime01(0.95f, hold: true);                      // 밤
            Assert.AreEqual(21, RegionCap(spawner, meadow), "밤 +1");

            weather.SetWeather(WeatherType.Rain, hold: true, instant: true);
            Assert.AreEqual(22, RegionCap(spawner, meadow), "밤 + 비 = +2");
            Assert.AreEqual(21, RegionCap(spawner, frostline), "설산에서는 비가 눈으로 보인다 — 비 보너스는 없고 밤 +1만");

            clock.SetTime01(0.5f, hold: true);                       // 낮 + 비
            Assert.AreEqual(21, RegionCap(spawner, meadow), "비 +1");
            Assert.AreEqual(20, RegionCap(spawner, frostline), "설산의 낮은 눈이라 보너스가 없다");

            weather.SetWeather(WeatherType.Fog, hold: true, instant: true);
            Assert.AreEqual(21, RegionCap(spawner, meadow), "안개 +1");
            weather.SetWeather(WeatherType.Snow, hold: true, instant: true);
            Assert.AreEqual(20, RegionCap(spawner, meadow), "눈은 슬롯을 늘리지 않는다");
        }
    }
}
#endif
