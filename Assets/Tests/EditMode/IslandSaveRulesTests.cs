#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// <see cref="IslandSaveRules"/>와 <see cref="IslandGuideSteps"/> — 섬 세이브의 자가 복구, 방문 스냅샷 정리, 안내 단계.
    /// </summary>
    [TestFixture]
    public class IslandSaveRulesTests
    {
        // ── 세이브 호환 ──

        [Test]
        public void EmptyJson_BecomesDefaultIsland()
        {
            // 구세이브(island.json 없음)·빈 블롭 — 필드 기본값만으로 성립해야 한다.
            IslandSave save = JsonUtility.FromJson<IslandSave>("{}");
            Assert.IsFalse(IslandSaveRules.Sanitize(save));
            Assert.AreEqual(0, save.sizeLevel);
            Assert.AreEqual(0, save.extraSlots);
            Assert.AreEqual(0, save.owned.Count);
            Assert.AreEqual(0, save.placed.Count);
            Assert.AreEqual(0, save.released.Count);
            Assert.AreEqual(0L, save.lastSettleUnix);
            Assert.IsFalse(save.guideDone);
            Assert.IsFalse(save.starterGranted);
            Assert.IsTrue(save.isPublic, "공개가 기본이어야 한다 — 필드가 없는 세이브가 비공개로 굳으면 안 된다");
        }

        [Test]
        public void RoundTrip_PreservesState()
        {
            var save = new IslandSave { sizeLevel = 2, extraSlots = 3, lastSettleUnix = 1_760_000_000L, accruedHours = 3.5f };
            save.owned.Add(new IslandOwnedRecord { id = "f_bench", count = 2 });
            save.placed.Add(new IslandPlacedRecord { id = "f_bench", x = -3, z = 4, rot = 1 });
            save.released.Add("abc");
            save.bonds.Add(new IslandBondRecord { instanceId = "abc", hours = 12.5f });

            IslandSave loaded = JsonUtility.FromJson<IslandSave>(JsonUtility.ToJson(save, false));
            Assert.IsFalse(IslandSaveRules.Sanitize(loaded));
            Assert.AreEqual(2, loaded.sizeLevel);
            Assert.AreEqual(3, loaded.extraSlots);
            Assert.AreEqual(-3, loaded.placed[0].x);
            Assert.AreEqual(1, loaded.placed[0].rot);
            Assert.AreEqual("abc", loaded.released[0]);
            Assert.AreEqual(12.5f, loaded.bonds[0].hours, 1e-4f);
            Assert.AreEqual(1_760_000_000L, loaded.lastSettleUnix);
        }

        [Test]
        public void Sanitize_NullLists_AreRecreated()
        {
            var save = new IslandSave { owned = null, placed = null, released = null, bonds = null };
            Assert.IsTrue(IslandSaveRules.Sanitize(save));
            Assert.IsNotNull(save.owned);
            Assert.IsNotNull(save.placed);
            Assert.IsNotNull(save.released);
            Assert.IsNotNull(save.bonds);
        }

        [Test]
        public void Sanitize_ClampsLevelsAndNumbers()
        {
            var save = new IslandSave
            {
                sizeLevel = 99, extraSlots = -4, pendingCandy = -5f, pendingCoin = float.NaN,
                accruedHours = 500f, harvestCount = -1, guideStep = 77, lastSettleUnix = -10,
            };
            Assert.IsTrue(IslandSaveRules.Sanitize(save));
            Assert.AreEqual(GameConstants.Island.MaxSizeLevel, save.sizeLevel);
            Assert.AreEqual(0, save.extraSlots);
            Assert.AreEqual(0f, save.pendingCandy);
            Assert.AreEqual(0f, save.pendingCoin);
            Assert.AreEqual(GameConstants.Island.MaxCapHours, save.accruedHours);
            Assert.AreEqual(0, save.harvestCount);
            Assert.AreEqual((int)IslandGuideStep.Done, save.guideStep);
            Assert.AreEqual(0L, save.lastSettleUnix);
        }

        [Test]
        public void Sanitize_MergesDuplicateOwned_AndDropsEmpty()
        {
            var save = new IslandSave();
            save.owned.Add(new IslandOwnedRecord { id = "f_bench", count = 1 });
            save.owned.Add(null);
            save.owned.Add(new IslandOwnedRecord { id = "", count = 5 });
            save.owned.Add(new IslandOwnedRecord { id = "f_bench", count = 2 });
            Assert.IsTrue(IslandSaveRules.Sanitize(save));
            Assert.AreEqual(1, save.owned.Count);
            Assert.AreEqual(3, save.owned[0].count);
        }

        [Test]
        public void Sanitize_PlacedMoreThanOwned_RaisesOwnedInsteadOfDeleting()
        {
            // 놓인 물건을 지우는 쪽이 아니라 보유 수를 올린다 — 섬 모양이 유지된다.
            var save = new IslandSave();
            save.owned.Add(new IslandOwnedRecord { id = "f_bench", count = 1 });
            save.placed.Add(new IslandPlacedRecord { id = "f_bench", x = 0, z = 0 });
            save.placed.Add(new IslandPlacedRecord { id = "f_bench", x = 3, z = 0 });
            save.placed.Add(new IslandPlacedRecord { id = "t_rock", x = 0, z = 3 });
            Assert.IsTrue(IslandSaveRules.Sanitize(save));
            Assert.AreEqual(3, save.placed.Count);
            Assert.AreEqual(2, IslandSaveRules.FindOwned(save, "f_bench").count);
            Assert.AreEqual(1, IslandSaveRules.FindOwned(save, "t_rock").count);
        }

        [Test]
        public void Sanitize_UnknownObjectIds_AreKept()
        {
            // 더 새 버전에서 산 물건을 구버전이 열었다고 날리면 안 된다.
            var save = new IslandSave();
            save.owned.Add(new IslandOwnedRecord { id = "x_from_future", count = 1 });
            save.placed.Add(new IslandPlacedRecord { id = "x_from_future", x = 0, z = 0 });
            IslandSaveRules.Sanitize(save);
            Assert.AreEqual(1, save.placed.Count);
            Assert.IsNotNull(IslandSaveRules.FindOwned(save, "x_from_future"));
        }

        [Test]
        public void Sanitize_NegativeRotation_IsNormalized()
        {
            var save = new IslandSave();
            save.owned.Add(new IslandOwnedRecord { id = "f_bench", count = 1 });
            save.placed.Add(new IslandPlacedRecord { id = "f_bench", rot = -1 });
            Assert.IsTrue(IslandSaveRules.Sanitize(save));
            Assert.AreEqual(3, save.placed[0].rot);
        }

        [Test]
        public void Sanitize_Released_DropsDuplicatesAndOverflow()
        {
            var save = new IslandSave();
            save.released.AddRange(new[] { "a", "", "a", "b", "c", "d", "e" });
            Assert.IsTrue(IslandSaveRules.Sanitize(save));
            // 기본 3칸 — 앞에서부터 남긴다.
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, save.released);
        }

        // ── 방문 스냅샷(남이 만든 데이터) ──

        [Test]
        public void SanitizeSnapshot_DropsUnknownOutOfBoundsAndOverlapping()
        {
            var snapshot = new IslandSnapshot { sizeLevel = 0, ownerName = null };
            snapshot.placed.Add(new IslandPlacedRecord { id = "b_cabin", x = 0, z = 0 });
            snapshot.placed.Add(new IslandPlacedRecord { id = "t_rock", x = 1, z = 1 });        // 오두막과 겹친다
            snapshot.placed.Add(new IslandPlacedRecord { id = "x_from_future", x = -4, z = 0 }); // 모르는 물건
            snapshot.placed.Add(new IslandPlacedRecord { id = "t_rock", x = 40, z = 0 });        // 섬 밖
            snapshot.placed.Add(new IslandPlacedRecord { id = "t_rock", x = 0, z = -5 });        // 도착 칸
            snapshot.placed.Add(new IslandPlacedRecord { id = "t_rock", x = -4, z = 2, rot = -3 });
            snapshot.placed.Add(null);

            IslandSaveRules.SanitizeSnapshot(snapshot);
            Assert.AreEqual(2, snapshot.placed.Count);
            Assert.AreEqual("b_cabin", snapshot.placed[0].id);
            Assert.AreEqual(1, snapshot.placed[1].rot);
            Assert.AreEqual("", snapshot.ownerName);
        }

        [Test]
        public void SanitizeSnapshot_ClampsInsectsAndSize()
        {
            var snapshot = new IslandSnapshot { sizeLevel = 50, ownerName = new string('가', 80) };
            for (int i = 0; i < 30; i++)
                snapshot.insects.Add(new IslandSnapshotInsect { insectId = "bug" + i, level = 9999 });
            snapshot.insects.Add(new IslandSnapshotInsect { insectId = "" });

            IslandSaveRules.SanitizeSnapshot(snapshot);
            Assert.AreEqual(GameConstants.Island.MaxSizeLevel, snapshot.sizeLevel);
            Assert.AreEqual(GameConstants.Island.MaxInsectSlots, snapshot.insects.Count);
            Assert.AreEqual(GameConstants.Leveling.FallbackMaxLevel, snapshot.insects[0].level);
            Assert.LessOrEqual(snapshot.ownerName.Length, 24);
        }

        [Test]
        public void Snapshot_JsonRoundTrip_KeepsPlacement()
        {
            var snapshot = new IslandSnapshot { sizeLevel = 1, comfort = 12, ownerName = "탐험가" };
            snapshot.placed.Add(new IslandPlacedRecord { id = "f_bench", x = -2, z = 3, rot = 2 });
            snapshot.insects.Add(new IslandSnapshotInsect { insectId = "stag", level = 7, shiny = true });
            IslandSnapshot loaded = JsonUtility.FromJson<IslandSnapshot>(JsonUtility.ToJson(snapshot));
            Assert.AreEqual(-2, loaded.placed[0].x);
            Assert.AreEqual(2, loaded.placed[0].rot);
            Assert.IsTrue(loaded.insects[0].shiny);
            Assert.AreEqual(12, loaded.comfort);
        }

        // ── 안내 단계 ──

        [Test]
        public void Guide_FreshIsland_StaysAtFirstStep()
        {
            var facts = new IslandGuideFacts { ownedInsectCount = 3 };
            Assert.AreEqual(IslandGuideStep.OpenEdit, IslandGuideSteps.Advance(IslandGuideStep.OpenEdit, facts));
        }

        [Test]
        public void Guide_AdvancesByAction_OneStepAtATime()
        {
            var facts = new IslandGuideFacts { ownedInsectCount = 3, editOpened = true };
            Assert.AreEqual(IslandGuideStep.PlaceFirst, IslandGuideSteps.Advance(IslandGuideStep.OpenEdit, facts));
            facts.placedCount = 1;
            Assert.AreEqual(IslandGuideStep.ReleaseInsect, IslandGuideSteps.Advance(IslandGuideStep.PlaceFirst, facts));
            facts.releasedCount = 1;
            Assert.AreEqual(IslandGuideStep.Harvest, IslandGuideSteps.Advance(IslandGuideStep.ReleaseInsect, facts));
            facts.harvestCount = 1;
            Assert.AreEqual(IslandGuideStep.OpenShop, IslandGuideSteps.Advance(IslandGuideStep.Harvest, facts));
            facts.shopOpened = true;
            Assert.AreEqual(IslandGuideStep.Finish, IslandGuideSteps.Advance(IslandGuideStep.OpenShop, facts));
            facts.finishAcknowledged = true;
            Assert.AreEqual(IslandGuideStep.Done, IslandGuideSteps.Advance(IslandGuideStep.Finish, facts));
        }

        [Test]
        public void Guide_AlreadyDecoratedSave_SkipsToTheFirstUndoneStep()
        {
            // 다른 기기에서 꾸민 섬을 받아 온 세이브가 "벤치를 놓아 보세요"에 멈추면 안 된다.
            var facts = new IslandGuideFacts { ownedInsectCount = 3, placedCount = 5, releasedCount = 2, harvestCount = 4 };
            Assert.AreEqual(IslandGuideStep.OpenShop, IslandGuideSteps.Advance(IslandGuideStep.OpenEdit, facts));
        }

        [Test]
        public void Guide_NoInsectsOwned_DoesNotBlockOnRelease()
        {
            var facts = new IslandGuideFacts { ownedInsectCount = 0, placedCount = 1 };
            Assert.AreEqual(IslandGuideStep.Harvest, IslandGuideSteps.Advance(IslandGuideStep.OpenEdit, facts));
        }

        [Test]
        public void Guide_EveryVisibleStep_HasText()
        {
            for (int i = 0; i < (int)IslandGuideStep.Done; i++)
            {
                var step = (IslandGuideStep)i;
                Assert.IsFalse(string.IsNullOrEmpty(IslandGuideSteps.Title(step)), step.ToString());
                Assert.IsFalse(string.IsNullOrEmpty(IslandGuideSteps.Body(step)), step.ToString());
            }
            Assert.Greater(IslandGuideSteps.HelpTopics.Length, 0);
        }
    }
}
#endif
