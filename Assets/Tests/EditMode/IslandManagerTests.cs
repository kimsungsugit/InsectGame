#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// <see cref="IslandManager"/> — 정산·수확·방목·안내 진행이 맞물리는 순서.
    /// 순수 공식은 <see cref="IslandYieldTests"/>가 보고, 여기는 "변경 전에 정산했는가", "수확물의 소수점이 남는가",
    /// "안내의 첫 선물이 한 번만 들어가는가"처럼 상태가 얽힌 부분을 본다.
    ///
    /// 매니저는 <b>비활성 오브젝트</b>에 붙여 Awake(디스크 로드)를 막고, 저장을 끄고, 시계를 주입한다.
    /// 지갑·캔디는 붙이지 않는다 — 그 둘은 지급 즉시 파일에 쓰므로 개발자 PC의 진짜 세이브를 건드린다.
    /// </summary>
    [TestFixture]
    public class IslandManagerTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const long T0 = 1_760_000_000L;
        private const long Hour = 3600L;

        private GameObject holder;
        private InsectDatabase database;
        private IslandManager island;
        private long now;

        [SetUp]
        public void SetUp()
        {
            holder = new GameObject("IslandManagerTests");
            holder.SetActive(false);

            database = ScriptableObject.CreateInstance<InsectDatabase>();
            var owned = new List<PlayerInsectData>();
            for (int i = 0; i < 5; i++)
            {
                var species = ScriptableObject.CreateInstance<InsectData>();
                species.insectId = "common_" + i;
                species.displayName = "일반 " + i;
                species.rarity = InsectRarity.Common;
                database.insects.Add(species);
                owned.Add(new PlayerInsectData { instanceId = "inst_" + i, insectId = species.insectId, level = 5 });
            }

            var collection = holder.AddComponent<PlayerInsectCollection>();
            Set(collection, "database", database);
            Set(collection, "saveData", new PlayerInsectCollectionSave { insects = owned });
            var lookup = (Dictionary<string, PlayerInsectData>)Get(collection, "lookup");
            foreach (PlayerInsectData p in owned) lookup.Add(p.instanceId, p);

            island = holder.AddComponent<IslandManager>();
            island.PersistenceEnabled = false;
            now = T0;
            island.Clock = () => now;
            island.AutoWire(collection, null, null, database);
            island.LoadForCapture(new IslandSave());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (InsectData species in database.insects) Object.DestroyImmediate(species);
            Object.DestroyImmediate(database);
            Object.DestroyImmediate(holder);
        }

        private void ReleaseThree()
        {
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(IslandReleaseResult.Ok, island.TryRelease("inst_" + i));
        }

        // 안내의 첫 선물이 수확량 계산에 섞이지 않게, 안내가 끝난 섬으로 시작한다.
        private void StartWithGuideDone()
        {
            island.LoadForCapture(new IslandSave { guideDone = true, starterGranted = true });
            island.NotifyEnteredOwnIsland();   // 정산 기준 시각을 잡는다
        }

        [Test]
        public void FirstEntry_GrantsStarterKitOnce()
        {
            island.NotifyEnteredOwnIsland();
            Assert.IsTrue(island.HasEnteredOnce);
            Assert.AreEqual(1, island.GetStorageCount("f_bench"));
            Assert.AreEqual(2, island.GetStorageCount("f_flowerpot"));

            island.NotifyEnteredOwnIsland();
            Assert.AreEqual(1, island.GetStorageCount("f_bench"), "들어올 때마다 스타터 키트를 다시 준다");
        }

        [Test]
        public void Harvest_AfterFiveHours_GivesFlooredAmounts_AndKeepsTheFraction()
        {
            StartWithGuideDone();
            ReleaseThree();
            now += 5 * Hour;

            // 일반 3마리: 캔디 0.75/h → 3.75, 코인 0.45/h → 2.25
            Assert.IsTrue(island.Harvest(out int candy, out int coin));
            Assert.AreEqual(3, candy);
            Assert.AreEqual(2, coin);

            island.Peek(out float pendingCandy, out float pendingCoin, out float accrued, out _);
            Assert.AreEqual(0.75f, pendingCandy, 0.01f, "소수점 아래를 버리면 자주 수확할수록 손해를 본다");
            Assert.AreEqual(0.25f, pendingCoin, 0.01f);
            Assert.AreEqual(0f, accrued, 0.001f, "수확하면 쌓인 시간이 0으로 돌아가야 한다");
        }

        [Test]
        public void Harvest_NothingAccrued_ReturnsFalse()
        {
            StartWithGuideDone();
            ReleaseThree();
            now += 30 * 60;   // 30분 — 캔디 0.375, 코인 0.225
            Assert.IsFalse(island.Harvest(out int candy, out int coin));
            Assert.AreEqual(0, candy);
            Assert.AreEqual(0, coin);
            Assert.AreEqual(0, island.HarvestCount);
        }

        [Test]
        public void Harvest_LeftForDays_IsCappedAtCapHours()
        {
            StartWithGuideDone();
            ReleaseThree();
            now += 100 * Hour;

            Assert.IsTrue(island.IsHarvestFull);
            Assert.IsTrue(island.Harvest(out int candy, out _));
            // 상한 8시간 × 0.75/h = 6
            Assert.AreEqual(6, candy);
        }

        [Test]
        public void PlacingAYieldObject_DoesNotApplyToHoursAlreadyPassed()
        {
            StartWithGuideDone();
            ReleaseThree();
            island.GrantObject("o_feeder", 1);

            now += 4 * Hour;
            island.GetRates(out float before, out _);
            Assert.AreEqual(IslandPlaceResult.Ok, island.TryPlace("o_feeder", 2, 2, 0));
            island.GetRates(out float after, out _);
            Assert.Greater(after, before, "먹이통을 놓았는데 생산량이 그대로다(캐시가 안 풀렸다)");

            now += 2 * Hour;
            island.Peek(out float pending, out _, out _, out _);
            // 앞 4시간은 놓기 전 속도, 뒤 2시간만 놓은 뒤 속도.
            Assert.AreEqual(before * 4f + after * 2f, pending, 0.01f,
                "변경 전에 정산하지 않으면 방금 놓은 설비가 지난 시간에도 있었던 것처럼 계산된다");
        }

        [Test]
        public void Release_BeyondSlots_IsRejected_AndUnreleaseKeepsBond()
        {
            StartWithGuideDone();
            ReleaseThree();
            Assert.AreEqual(IslandReleaseResult.NoFreeSlot, island.TryRelease("inst_3"));
            Assert.AreEqual(IslandReleaseResult.AlreadyReleased, island.TryRelease("inst_0"));
            Assert.AreEqual(IslandReleaseResult.NoSuchInsect, island.TryRelease("nope"));

            now += 7 * Hour;
            island.Settle();
            Assert.AreEqual(1, island.GetBondLevel("inst_0"), "7시간이면 하트 하나(6시간)");

            Assert.IsTrue(island.Unrelease("inst_0"));
            Assert.IsFalse(island.IsReleased("inst_0"));
            Assert.AreEqual(1, island.GetBondLevel("inst_0"), "거두어도 친밀도는 남아야 한다");
            Assert.AreEqual(IslandReleaseResult.Ok, island.TryRelease("inst_3"));
        }

        [Test]
        public void Release_GhostInstanceIds_AreClearedWhenMakingRoom()
        {
            // 보유 목록에서 사라진 id가 슬롯을 차지한 채 남아 있으면 새 곤충을 풀 수 없다.
            var save = new IslandSave { guideDone = true, starterGranted = true };
            save.released.AddRange(new[] { "gone_a", "gone_b", "inst_0" });
            island.LoadForCapture(save);
            Assert.AreEqual(1, island.ReleasedInsects.Count, "사라진 곤충은 방목 목록에서 걸러져야 한다");
            Assert.AreEqual(IslandReleaseResult.Ok, island.TryRelease("inst_1"));
            Assert.AreEqual(2, island.ReleasedInsects.Count);
        }

        [Test]
        public void MoveAndStore_KeepInventoryConsistent()
        {
            StartWithGuideDone();
            island.GrantObject("f_bench", 2);
            Assert.AreEqual(IslandPlaceResult.Ok, island.TryPlace("f_bench", 0, 0, 0));
            Assert.AreEqual(1, island.GetStorageCount("f_bench"));
            Assert.AreEqual(IslandPlaceResult.Ok, island.TryPlace("f_bench", 0, 2, 0));
            Assert.AreEqual(IslandPlaceResult.NotOwned, island.TryPlace("f_bench", 0, 3, 0));

            Assert.AreEqual(IslandPlaceResult.Overlap, island.TryMove(1, 1, 0, 0));
            Assert.AreEqual(IslandPlaceResult.Ok, island.TryMove(1, 2, 2, 1));
            Assert.AreEqual(1, island.Placed[1].rot);

            Assert.IsTrue(island.Store(0));
            Assert.AreEqual(1, island.Placed.Count);
            Assert.AreEqual(1, island.GetStorageCount("f_bench"), "넣은 물건은 보관함으로 돌아와야 한다");
            Assert.AreEqual(2, island.GetOwnedCount("f_bench"));
        }

        [Test]
        public void Buy_WithoutWallet_IsRefused_AndGrantsNothing()
        {
            StartWithGuideDone();
            Assert.AreEqual(IslandBuyResult.NotEnoughCoins, island.TryBuy("f_bench"));
            Assert.AreEqual(IslandBuyResult.NotEnoughGems, island.TryBuy("b_greenhouse"));
            Assert.AreEqual(IslandBuyResult.Locked, island.TryBuy("b_lighthouse"));
            Assert.AreEqual(IslandBuyResult.Unknown, island.TryBuy("x_typo"));
            Assert.AreEqual(0, island.GetOwnedCount("f_bench"));
            Assert.AreEqual(IslandBuyResult.NotEnoughCoins, island.TryExpandSize(false));
            Assert.AreEqual(0, island.SizeLevel);
        }

        [Test]
        public void Guide_WalksThroughByActions_AndGivesTheFirstHarvestGiftOnce()
        {
            island.NotifyEnteredOwnIsland();
            Assert.IsTrue(island.GuideActive);
            Assert.AreEqual(IslandGuideStep.OpenEdit, island.GuideStep);

            island.ReportEditOpened();
            Assert.AreEqual(IslandGuideStep.PlaceFirst, island.GuideStep);

            Assert.AreEqual(IslandPlaceResult.Ok, island.TryPlace("f_bench", 0, 0, 0));
            Assert.AreEqual(IslandGuideStep.ReleaseInsect, island.GuideStep);

            Assert.AreEqual(IslandReleaseResult.Ok, island.TryRelease("inst_0"));
            Assert.AreEqual(IslandGuideStep.Harvest, island.GuideStep);

            // 기다리지 않고 바로 수확할 수 있어야 한다 — 그게 선물의 이유다.
            Assert.IsTrue(island.Harvest(out int candy, out int coin));
            Assert.AreEqual((int)IslandManager.GuideGiftCandy, candy);
            Assert.AreEqual((int)IslandManager.GuideGiftCoin, coin);
            Assert.AreEqual(IslandGuideStep.OpenShop, island.GuideStep);

            island.ReportShopOpened();
            Assert.AreEqual(IslandGuideStep.Finish, island.GuideStep);
            island.AcknowledgeGuideFinish();
            Assert.IsFalse(island.GuideActive);

            // 다시 보기는 선물을 다시 주지 않고, 이미 한 행동으로 건너뛰지도 않는다.
            island.RestartGuide();
            Assert.AreEqual(IslandGuideStep.OpenEdit, island.GuideStep);
            island.ReportEditOpened();
            Assert.AreEqual(IslandGuideStep.OpenEdit, island.GuideStep, "다시 보기는 [다음]으로만 넘어간다");
            for (int i = 0; i < (int)IslandGuideStep.Done; i++) island.AdvanceGuideReplay();
            Assert.IsFalse(island.GuideActive);
            Assert.IsFalse(island.Harvest(out _, out _), "다시 보기가 수확 선물을 또 넣었다");
        }

        [Test]
        public void Guide_EndReplay_ClosesOnlyTheReplay_NeverTheFirstRun()
        {
            // 첫 안내에서 배너를 닫는 건 화면에서 치우는 것뿐이다 — 단계가 넘어가면 안 된다.
            island.NotifyEnteredOwnIsland();
            island.ReportEditOpened();
            Assert.AreEqual(IslandGuideStep.PlaceFirst, island.GuideStep);
            island.EndGuideReplay();
            Assert.IsTrue(island.GuideActive, "첫 안내가 닫기로 끝나 버렸다");
            Assert.AreEqual(IslandGuideStep.PlaceFirst, island.GuideStep);

            // 다시 보기는 중간에 그만둘 수 있고, 수확 선물을 다시 넣지 않는다.
            StartWithGuideDone();
            island.RestartGuide();
            island.AdvanceGuideReplay();
            Assert.IsTrue(island.GuideIsReplay);
            island.EndGuideReplay();
            Assert.IsFalse(island.GuideActive);
            Assert.IsFalse(island.GuideIsReplay);
            Assert.IsFalse(island.Harvest(out _, out _), "다시 보기를 닫았더니 수확 선물이 들어갔다");
        }

        [Test]
        public void GuideBanner_HidesItselfAfterItsTime_ButNotDuringReplay()
        {
            Assert.Greater(InsectGame.UI.IslandGuideUI.CoachRemaining(0f, false), 0f);
            Assert.LessOrEqual(InsectGame.UI.IslandGuideUI.CoachRemaining(InsectGame.UI.IslandGuideUI.CoachSeconds, false), 0f);
            // 다시 보기는 [다음]으로 직접 넘긴다 — 읽는 도중에 사라지면 안 된다.
            Assert.Greater(InsectGame.UI.IslandGuideUI.CoachRemaining(999f, true), 0f);

            // 마지막 1초 동안만 옅어진다.
            Assert.AreEqual(1f, InsectGame.UI.IslandGuideUI.CoachAlpha(InsectGame.UI.IslandGuideUI.CoachSeconds), 0.0001f);
            Assert.AreEqual(1f, InsectGame.UI.IslandGuideUI.CoachAlpha(InsectGame.UI.IslandGuideUI.CoachFadeSeconds), 0.0001f);
            Assert.AreEqual(0.5f, InsectGame.UI.IslandGuideUI.CoachAlpha(InsectGame.UI.IslandGuideUI.CoachFadeSeconds * 0.5f), 0.0001f);
            Assert.AreEqual(0f, InsectGame.UI.IslandGuideUI.CoachAlpha(0f), 0.0001f);
        }

        [Test]
        public void Snapshot_CarriesLayoutAndSpecies_ButNoInstanceIds()
        {
            StartWithGuideDone();
            island.GrantObject("f_bench", 1);
            island.TryPlace("f_bench", -2, 1, 3);
            island.TryRelease("inst_2");

            IslandSnapshot snapshot = island.BuildSnapshot("탐험가");
            Assert.AreEqual("탐험가", snapshot.ownerName);
            Assert.AreEqual(1, snapshot.placed.Count);
            Assert.AreEqual(3, snapshot.placed[0].rot);
            Assert.AreEqual(1, snapshot.insects.Count);
            Assert.AreEqual("common_2", snapshot.insects[0].insectId);
            StringAssert.DoesNotContain("inst_2", JsonUtility.ToJson(snapshot), "공개 스냅샷에 instanceId가 실렸다");
        }

        private static object Get(object target, string name)
        {
            FieldInfo f = target.GetType().GetField(name, Private);
            Assert.IsNotNull(f, name);
            return f.GetValue(target);
        }

        private static void Set(object target, string name, object value)
        {
            FieldInfo f = target.GetType().GetField(name, Private);
            Assert.IsNotNull(f, name);
            f.SetValue(target, value);
        }
    }
}
#endif
