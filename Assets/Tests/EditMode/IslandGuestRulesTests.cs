#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 나의 섬 손님 곤충(<see cref="IslandGuestRules"/>) — 손님 수 표, 들어설 수 있는 곳, 종 고르기(해금 1곳에서도 비지 않음·등급 상한),
    /// 레벨 대역, 떠나는 규칙, 빈 칸 위 도주 거리, 들어설 칸.
    ///
    /// 실패가 조용한 계열이다: 해금 판정이 뒤집히면 잠긴 리전의 종이 섬으로 새고, 등급 상한이 빠지면 집에서 전설 레이드가 열리고,
    /// 빈 칸 거리가 틀리면 손님이 꽃밭 너머·바다로 달아난다. 예외도 경고도 없다.
    /// </summary>
    [TestFixture]
    public class IslandGuestRulesTests
    {
        private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly DayPhase[] Phases = (DayPhase[])System.Enum.GetValues(typeof(DayPhase));
        private static readonly WeatherType[] Weathers = (WeatherType[])System.Enum.GetValues(typeof(WeatherType));

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
            var host = new GameObject("IslandGuestRulesTests_RealDb");
            host.SetActive(false);   // Awake(월드 생성)를 막고 private 생성 함수만 부른다
            shared.Add(host);
            var bootstrap = host.AddComponent<PlaySceneBootstrap>();
            db = (InsectDatabase)typeof(PlaySceneBootstrap).GetMethod("EnsureExpandedDatabase", Inst).Invoke(bootstrap, null);
            Assert.IsNotNull(db);
            shared.Add(db);
            return db;
        }

        private static WorldState State(DayPhase phase, WeatherType weather, int hour = 12)
            => new WorldState { DayPhase = phase, Weather = weather, Hour24 = hour };

        private static int HourOf(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Morning: return 8;
                case DayPhase.Day: return 14;
                case DayPhase.Evening: return 19;
                default: return 23;
            }
        }

        /// <summary>진짜 리전 정의의 풀 — 레벨 대역은 리전의 requiredLevel부터(대역 폭은 이 테스트가 보지 않는다).</summary>
        private List<FieldRegionTable> RealTables()
        {
            var tables = new List<FieldRegionTable>();
            foreach (RegionData region in RegionDefinitions.CreateAll())
            {
                if (region.insectIds == null || region.insectIds.Length == 0) continue;
                var pool = new List<InsectData>();
                foreach (string id in region.insectIds)
                {
                    InsectData d = RealDatabase().GetById(id);
                    Assert.IsNotNull(d, $"{region.regionId} 풀의 '{id}'가 DB에 없다");
                    if (!pool.Contains(d)) pool.Add(d);
                }
                tables.Add(new FieldRegionTable(region.regionId, region.requiredLevel, region.requiredLevel + 9, pool));
            }
            return tables;
        }

        private InsectData Fake(string id, InsectRarity rarity, float weight = 1f)
        {
            var d = ScriptableObject.CreateInstance<InsectData>();
            d.insectId = id;
            d.rarity = rarity;
            d.spawnWeight = weight;
            perTest.Add(d);
            return d;
        }

        // ── 손님 수 ──

        [Test]
        public void WantedGuests_IsNightPlusWetWeather_CappedAtTwo()
        {
            foreach (DayPhase p in Phases)
                foreach (WeatherType w in Weathers)
                {
                    int expected = (p == DayPhase.Night ? 1 : 0)
                                   + (w == WeatherType.Rain || w == WeatherType.Fog ? 1 : 0);
                    Assert.AreEqual(expected, IslandGuestRules.WantedGuests(State(p, w)), $"{p}/{w}");
                }
            Assert.AreEqual(0, IslandGuestRules.WantedGuests(State(DayPhase.Day, WeatherType.Clear)), "맑은 낮엔 아무도 안 온다");
            Assert.AreEqual(2, IslandGuestRules.WantedGuests(State(DayPhase.Night, WeatherType.Fog)));
            Assert.AreEqual(2, IslandGuestRules.MaxGuests);
        }

        [Test]
        public void WantedGuests_IsTheFieldBonusSlotRule()
        {
            // 단일 출처 — 필드 보너스 슬롯과 갈라지면 섬만 다른 규칙이 된다.
            foreach (DayPhase p in Phases)
                foreach (WeatherType w in Weathers)
                    Assert.AreEqual(FieldSpawnRules.BonusSlots(State(p, w)), IslandGuestRules.WantedGuests(State(p, w)));
        }

        // ── 어디서 오나 ──

        [Test]
        public void CanHostGuests_OnlyOnMyOwnIsland_NotDreaming_NotDecorating()
        {
            Assert.IsTrue(IslandGuestRules.CanHostGuests(IslandMode.Own, false, false));
            Assert.IsFalse(IslandGuestRules.CanHostGuests(IslandMode.Visit, false, false), "남의 섬 구경에 손님이 온다");
            Assert.IsFalse(IslandGuestRules.CanHostGuests(IslandMode.None, false, false), "섬 밖에서 손님이 선다");
            Assert.IsFalse(IslandGuestRules.CanHostGuests(IslandMode.Own, true, false), "꿈속 섬에 손님이 온다");
            Assert.IsFalse(IslandGuestRules.CanHostGuests(IslandMode.Visit, true, false));
            Assert.IsFalse(IslandGuestRules.CanHostGuests(IslandMode.Own, false, true), "꾸미기 중에 손님이 들어선다");
        }

        // ── 종 고르기 ──

        [Test]
        public void PickSpecies_WithOnlyTheMeadowUnlocked_NeverComesUpEmpty()
        {
            List<FieldRegionTable> all = RealTables();
            var accessible = new List<FieldRegionTable>();
            IslandGuestRules.FilterAccessible(all, id => id == "meadow", accessible);
            Assert.AreEqual(1, accessible.Count, "초원만 해금인데 다른 리전 표가 남았다");
            var meadowPool = new HashSet<InsectData>(accessible[0].Pool);

            var candidates = new List<InsectData>();
            var scratch = new List<InsectData>();
            var multipliers = new List<float>();
            var available = new bool[FieldSpawnRules.RarityCount];
            float[] rolls = { 0f, 0.13f, 0.5f, 0.87f, 0.999f };
            foreach (DayPhase p in Phases)
                foreach (WeatherType w in Weathers)
                {
                    WorldState s = State(p, w, HourOf(p));
                    IslandGuestRules.CollectCandidates(accessible, s, candidates);
                    Assert.Greater(candidates.Count, 0, $"{p}/{w}: 초원 후보가 비었다");
                    foreach (float rr in rolls)
                        foreach (float sr in rolls)
                        {
                            InsectData d = IslandGuestRules.PickSpecies(candidates, s, rr, sr, scratch, multipliers, available);
                            Assert.IsNotNull(d, $"{p}/{w} 굴림 {rr}/{sr}: 손님 종이 없다");
                            Assert.IsTrue(meadowPool.Contains(d), $"{d.insectId}: 잠긴 리전의 종이 섬으로 샜다");
                            Assert.LessOrEqual(d.rarity, IslandGuestRules.MaxGuestRarity);
                            Assert.IsTrue(IslandGuestRules.TryLevelBand(d, accessible, out int lo, out int hi));
                            Assert.AreEqual(1, lo, "초원 손님은 초원 대역이다");
                            Assert.GreaterOrEqual(hi, lo);
                        }
                }
        }

        [Test]
        public void PickSpecies_EverythingUnlocked_NeverSendsARaidBoss()
        {
            List<FieldRegionTable> all = RealTables();
            var accessible = new List<FieldRegionTable>();
            IslandGuestRules.FilterAccessible(all, _ => true, accessible);
            var candidates = new List<InsectData>();
            var scratch = new List<InsectData>();
            var multipliers = new List<float>();
            var available = new bool[FieldSpawnRules.RarityCount];
            WorldState s = State(DayPhase.Night, WeatherType.Fog, 23);
            IslandGuestRules.CollectCandidates(accessible, s, candidates);

            // 등급표의 맨 끝(전설 몫)을 굴려도 희귀로 내려온다.
            for (int i = 0; i <= 200; i++)
            {
                InsectData d = IslandGuestRules.PickSpecies(candidates, s, i / 200f, 0.5f, scratch, multipliers, available);
                Assert.IsNotNull(d);
                Assert.IsFalse(CaptureChoiceUI.IsRaidRarity(d.rarity), $"{d.insectId}({d.rarity}): 섬에서 레이드 창이 열린다");
            }
        }

        [Test]
        public void GuestRarityCap_SitsBelowEveryRaidRarity_AndFoldsIntoRare()
        {
            for (int r = 0; r <= (int)IslandGuestRules.MaxGuestRarity; r++)
                Assert.IsFalse(CaptureChoiceUI.IsRaidRarity((InsectRarity)r), $"{(InsectRarity)r}: 손님 등급 안에 레이드 등급이 있다");

            // 영웅·전설 몫은 기존 대체 규칙(가까운 아래)대로 희귀로 내려온다 — 일반·고급 몫은 그대로다.
            var available = new bool[FieldSpawnRules.RarityCount];
            for (int r = 0; r <= (int)IslandGuestRules.MaxGuestRarity; r++) available[r] = true;
            float[] shares = FieldSpawnRules.EffectiveShares(1f, available);
            Assert.AreEqual(FieldSpawnRules.CommonShare, shares[(int)InsectRarity.Common], 1e-4f);
            Assert.AreEqual(FieldSpawnRules.UncommonShare, shares[(int)InsectRarity.Uncommon], 1e-4f);
            Assert.AreEqual(FieldSpawnRules.RareShare + FieldSpawnRules.EpicShare + FieldSpawnRules.LegendaryShare,
                shares[(int)InsectRarity.Rare], 1e-4f);
            Assert.AreEqual(0f, shares[(int)InsectRarity.Epic], 1e-6f);
            Assert.AreEqual(0f, shares[(int)InsectRarity.Legendary], 1e-6f);
        }

        [Test]
        public void CollectCandidates_IsAUnionWithoutDuplicates_AndFallsBackWhenNothingMatches()
        {
            InsectData shared1 = Fake("shared", InsectRarity.Common);
            InsectData onlyA = Fake("only_a", InsectRarity.Uncommon);
            InsectData nightOnly = Fake("night_only", InsectRarity.Common);
            nightOnly.spawnCondition = new InsectSpawnCondition
            {
                limitByDayPhase = true,
                allowedDayPhases = new[] { DayPhase.Night }
            };
            var a = new FieldRegionTable("a", 1, 10, new List<InsectData> { shared1, onlyA });
            var b = new FieldRegionTable("b", 6, 16, new List<InsectData> { shared1, nightOnly });
            var into = new List<InsectData>();

            IslandGuestRules.CollectCandidates(new List<FieldRegionTable> { a, b }, State(DayPhase.Day, WeatherType.Clear), into);
            CollectionAssert.AreEquivalent(new[] { shared1, onlyA }, into, "낮인데 밤에만 나오는 종이 끼었거나 겹친 종이 두 번 들었다");

            IslandGuestRules.CollectCandidates(new List<FieldRegionTable> { a, b }, State(DayPhase.Night, WeatherType.Clear, 23), into);
            CollectionAssert.AreEquivalent(new[] { shared1, onlyA, nightOnly }, into);

            // 지금 나올 수 있는 종이 하나도 없으면 합집합 전체로 물러난다(손님이 영영 안 오지 않게).
            var c = new FieldRegionTable("c", 1, 10, new List<InsectData> { nightOnly });
            IslandGuestRules.CollectCandidates(new List<FieldRegionTable> { c }, State(DayPhase.Day, WeatherType.Clear), into);
            CollectionAssert.AreEquivalent(new[] { nightOnly }, into);
        }

        // ── 레벨 대역 ──

        [Test]
        public void FilterAccessible_DropsLockedRegions_AndSortsByLevel()
        {
            InsectData x = Fake("x", InsectRarity.Common);
            var all = new List<FieldRegionTable>
            {
                new FieldRegionTable("ruins", 36, 50, new List<InsectData> { x }),
                new FieldRegionTable("meadow", 1, 10, new List<InsectData> { x }),
                new FieldRegionTable("pond", 6, 16, new List<InsectData> { x }),
                new FieldRegionTable("empty", 3, 9, new List<InsectData>()),
            };
            var into = new List<FieldRegionTable>();
            IslandGuestRules.FilterAccessible(all, id => id != "pond", into);
            Assert.AreEqual(2, into.Count, "잠긴 리전·빈 풀이 남았다");
            Assert.AreEqual("meadow", into[0].RegionId);
            Assert.AreEqual("ruins", into[1].RegionId);
        }

        [Test]
        public void TryLevelBand_UsesTheLowestUnlockedRegionThatHasTheSpecies()
        {
            InsectData dragonfly = Fake("dragonfly", InsectRarity.Rare);
            InsectData other = Fake("other", InsectRarity.Common);
            var pond = new FieldRegionTable("pond", 6, 16, new List<InsectData> { dragonfly, other });
            var swamp = new FieldRegionTable("swamp", 20, 32, new List<InsectData> { dragonfly });
            var ruins = new FieldRegionTable("ruins", 36, 50, new List<InsectData> { dragonfly });

            // 늪·유적까지 열려 있어도 연못 대역 — 가장 높은 해금 리전으로 올라가지 않는다(섬이 고레벨 사냥터가 되지 않게).
            Assert.IsTrue(IslandGuestRules.TryLevelBand(dragonfly, new List<FieldRegionTable> { pond, swamp, ruins }, out int lo, out int hi));
            Assert.AreEqual(6, lo);
            Assert.AreEqual(16, hi);

            // 연못이 잠겼으면 그 종이 사는 다음 리전 — 뒤 리전 종을 앞 리전 대역으로 거저 주지 않는다.
            Assert.IsTrue(IslandGuestRules.TryLevelBand(dragonfly, new List<FieldRegionTable> { swamp, ruins }, out lo, out hi));
            Assert.AreEqual(20, lo);
            Assert.AreEqual(32, hi);

            Assert.IsFalse(IslandGuestRules.TryLevelBand(other, new List<FieldRegionTable> { swamp, ruins }, out _, out _));
            Assert.IsFalse(IslandGuestRules.TryLevelBand(null, new List<FieldRegionTable> { pond }, out _, out _));
        }

        // ── 떠나기 ──

        [Test]
        public void Leaving_OnlyWhenSurplusOrExpired_AndOnlyOutOfSight()
        {
            Assert.IsFalse(IslandGuestRules.ShouldLeave(false, false));
            Assert.IsTrue(IslandGuestRules.ShouldLeave(true, false), "아침이 와 손님 수가 줄었다");
            Assert.IsTrue(IslandGuestRules.ShouldLeave(false, true), "수명이 다했다");

            Assert.IsTrue(IslandGuestRules.CanLeaveNow(false, false));
            Assert.IsFalse(IslandGuestRules.CanLeaveNow(false, true), "눈앞에서 사라진다");
            Assert.IsFalse(IslandGuestRules.CanLeaveNow(true, false), "포획·전투 중에 떠난다");
        }

        [Test]
        public void IsInView_ScreenAndMargin_AndNothingBehindTheCamera()
        {
            Assert.IsTrue(IslandGuestRules.IsInView(new Vector3(0.5f, 0.5f, 5f)));
            Assert.IsTrue(IslandGuestRules.IsInView(new Vector3(1.05f, 0.5f, 5f)), "가장자리에 걸친 몸도 보인다");
            Assert.IsFalse(IslandGuestRules.IsInView(new Vector3(1.2f, 0.5f, 5f)));
            Assert.IsFalse(IslandGuestRules.IsInView(new Vector3(0.5f, -0.2f, 5f)));
            Assert.IsFalse(IslandGuestRules.IsInView(new Vector3(0.5f, 0.5f, -1f)), "카메라 뒤");
        }

        // ── 빈 칸 위 도주 ──

        private static HashSet<Vector2Int> Block(int minX, int maxX, int minZ, int maxZ)
        {
            var set = new HashSet<Vector2Int>();
            for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                    set.Add(new Vector2Int(x, z));
            return set;
        }

        private static Vector3 CellCenter(int x, int z) => IslandGrid.FootprintCenter(x, z, 1, 1);

        [Test]
        public void FreeRun_StopsAtTheEdgeOfTheFreeCells()
        {
            float cs = GameConstants.Island.CellSize;
            HashSet<Vector2Int> free = Block(0, 2, 0, 0);   // 가로 세 칸
            float run = IslandGuestRules.FreeRun(CellCenter(0, 0), Vector3.right, free, 20f);
            // 첫 칸 가운데(0.5칸)에서 세 칸 끝(3칸)까지 = 2.5칸 — 한 걸음(0.25m) 오차 안.
            Assert.AreEqual(2.5f * cs, run, IslandGuestRules.FreeRunStep + 1e-3f);
            Assert.Less(run, 2.5f * cs + 1e-3f, "빈 칸 밖으로 한 걸음이라도 나갔다");

            float back = IslandGuestRules.FreeRun(CellCenter(0, 0), Vector3.left, free, 20f);
            Assert.Less(back, 0.5f * cs + 1e-3f, "왼쪽은 반 칸 뒤가 끝이다");
        }

        [Test]
        public void FreeRun_ZeroWhenStartingOffTheFreeCells_AndCappedByMaxDistance()
        {
            HashSet<Vector2Int> free = Block(0, 20, 0, 0);
            Assert.AreEqual(0f, IslandGuestRules.FreeRun(CellCenter(-3, 0), Vector3.right, free, 20f), 1e-6f);
            Assert.AreEqual(4f, IslandGuestRules.FreeRun(CellCenter(0, 0), Vector3.right, free, 4f), 1e-6f);
            Assert.AreEqual(0f, IslandGuestRules.FreeRun(CellCenter(0, 0), Vector3.zero, free, 4f), 1e-6f);
            Assert.AreEqual(0f, IslandGuestRules.FreeRun(CellCenter(0, 0), Vector3.right, null, 4f), 1e-6f);
        }

        [Test]
        public void FreeRun_BlockedCellInTheMiddle_IsAWall()
        {
            float cs = GameConstants.Island.CellSize;
            HashSet<Vector2Int> free = Block(0, 4, 0, 0);
            free.Remove(new Vector2Int(2, 0));   // 꽃밭이 아니라 막는 물건이 놓인 칸
            float run = IslandGuestRules.FreeRun(CellCenter(0, 0), Vector3.right, free, 20f);
            Assert.Less(run, 1.5f * cs + 1e-3f, "막힌 칸을 뛰어넘었다");
        }

        // ── 들어설 칸 ──

        [Test]
        public void PickArrivalCell_PrefersCellsOutOfSight_AwayFromThePlayer()
        {
            var free = new List<Vector2Int>();
            for (int x = -5; x < 5; x++) free.Add(new Vector2Int(x, 0));
            var player = new Vector2Int(0, 0);
            System.Func<Vector2Int, bool> inView = c => c.x >= -2 && c.x <= 2;   // 가운데 다섯 칸만 보인다

            for (int i = 0; i <= 20; i++)
            {
                Assert.IsTrue(IslandGuestRules.PickArrivalCell(free, inView, player, null, i / 20f, out Vector2Int cell));
                Assert.IsFalse(inView(cell), $"{cell}: 화면 안에 들어섰다");
                Assert.GreaterOrEqual(Mathf.Abs(cell.x), IslandGuestRules.ArrivalMinCells, $"{cell}: 플레이어 코앞이다");
            }
        }

        [Test]
        public void PickArrivalCell_EverythingVisible_TakesTheFarthestFreeCell_AndSkipsTakenOnes()
        {
            var free = new List<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(6, 0), new Vector2Int(5, 0) };
            System.Func<Vector2Int, bool> all = _ => true;
            Assert.IsTrue(IslandGuestRules.PickArrivalCell(free, all, Vector2Int.zero, null, 0.3f, out Vector2Int cell));
            Assert.AreEqual(new Vector2Int(6, 0), cell);

            var taken = new HashSet<Vector2Int> { new Vector2Int(6, 0) };
            Assert.IsTrue(IslandGuestRules.PickArrivalCell(free, all, Vector2Int.zero, taken, 0.3f, out cell));
            Assert.AreEqual(new Vector2Int(5, 0), cell, "다른 손님이 선 칸에 겹쳐 섰다");

            Assert.IsFalse(IslandGuestRules.PickArrivalCell(new List<Vector2Int>(), all, Vector2Int.zero, null, 0f, out _));
        }

        [Test]
        public void ArrivalCells_ExistOnTheSmallestIsland_AwayFromTheArrivalPoint()
        {
            // 가장 작은 섬(10칸)·빈 섬 — 도착 자리(남쪽 가운데)에서 최소 거리 밖 칸이 남는다.
            var free = new List<Vector2Int>();
            IslandGrid.CollectFreeCells(new List<IslandPlacedRecord>(), 0, free);
            Assert.Greater(free.Count, 0);
            IslandGrid.CellAt(IslandGrid.ArrivalPoint(0), out int ax, out int az);
            Assert.IsTrue(IslandGuestRules.PickArrivalCell(free, _ => false, new Vector2Int(ax, az), null, 0.5f, out Vector2Int cell));
            int dx = cell.x - ax, dz = cell.y - az;
            Assert.GreaterOrEqual(dx * dx + dz * dz, IslandGuestRules.ArrivalMinCells * IslandGuestRules.ArrivalMinCells);
        }
    }
}
#endif
