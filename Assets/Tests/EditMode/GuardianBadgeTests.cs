#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 수문장 배지 — 표의 무결성(리전·그림·보상 아이템), 이정표 계산, 그리고 서비스가
    /// <b>첫 격파에서만</b> 배지를 알리고 이정표를 한 번만 주는지.
    ///
    /// 배지 표가 리전과 어긋나도 런타임엔 아무 말이 없다 — 그 리전 수문장을 이겨도 연출이 안 뜨고,
    /// 케이스엔 영영 빈 칸이 남는다. 그림이 빠져도 원판으로 대신 그려질 뿐 경고 한 줄이다.
    /// </summary>
    [TestFixture]
    public class GuardianBadgeTests
    {
        private const string UnlockKey = "InsectGame.UnlockedRegions";
        private const string GuardianKey = "InsectGame.DefeatedGuardians";

        // ── 표 ──

        [Test]
        public void Table_OneBadgePerGuardianRegion_InLevelOrder()
        {
            var guarded = new Dictionary<string, RegionData>();
            foreach (RegionData r in RegionDefinitions.CreateAll())
                if (!string.IsNullOrEmpty(r.guardianInsectId)) guarded[r.regionId] = r;

            GuardianBadges.Badge[] all = GuardianBadges.All();
            Assert.AreEqual(guarded.Count, all.Length, "수문장이 있는 리전마다 배지가 하나");
            var seen = new HashSet<string>();
            int prevLevel = 0;
            foreach (GuardianBadges.Badge b in all)
            {
                Assert.IsTrue(seen.Add(b.regionId), $"중복 {b.regionId}");
                Assert.IsTrue(guarded.TryGetValue(b.regionId, out RegionData r), $"{b.regionId}: 수문장 리전 아님");
                Assert.GreaterOrEqual(r.requiredLevel, prevLevel, $"{b.regionId}: 표시 순서는 리전 요구 레벨 순서");
                prevLevel = r.requiredLevel;
            }
        }

        [Test]
        public void Table_NamesAndFlavor_ShortAndClean()
        {
            var names = new HashSet<string>();
            foreach (GuardianBadges.Badge b in GuardianBadges.All())
            {
                Assert.IsFalse(string.IsNullOrEmpty(b.name), b.regionId);
                Assert.IsTrue(b.name.EndsWith("배지"), b.name);
                Assert.LessOrEqual(b.name.Length, 8, b.name);
                Assert.IsTrue(names.Add(b.name), $"이름 중복 {b.name}");
                Assert.IsFalse(string.IsNullOrEmpty(b.flavor), b.regionId);
                Assert.LessOrEqual(b.flavor.Length, 24, $"{b.regionId}: 새김글은 한 줄에 들어가야 한다");
                // 「무명」은 이름으로 부르지 않는다(StoryBible 2장).
                StringAssert.DoesNotContain("무명", b.name + b.flavor);
            }
        }

        [Test]
        public void Art_EveryBadgeAndEffectTextureExists()
        {
            string dir = Path.Combine(Application.dataPath, "Resources", GuardianBadges.ArtFolder);
            foreach (GuardianBadges.Badge b in GuardianBadges.All())
                Assert.IsTrue(File.Exists(Path.Combine(dir, "badge_" + b.regionId + ".png")),
                    $"그림 없음: {b.regionId} — Tools/Badges/guardian_badges.py를 돌릴 것");
            foreach (string fx in new[] { "badge_rays", "badge_glow", "badge_sparkle" })
                Assert.IsTrue(File.Exists(Path.Combine(dir, fx + ".png")), fx);
        }

        [Test]
        public void Milestones_AscendReachTotal_RewardsExist()
        {
            ItemDatabase items = ItemDatabase.CreateRuntimeDefault();
            try
            {
                int prev = 0;
                GuardianBadges.Milestone[] all = GuardianBadges.AllMilestones();
                Assert.AreEqual(all.Length, GuardianBadges.MilestoneCount);
                foreach (GuardianBadges.Milestone m in all)
                {
                    Assert.Greater(m.count, prev, "이정표는 오름차순");
                    Assert.LessOrEqual(m.count, GuardianBadges.Total);
                    Assert.IsFalse(string.IsNullOrEmpty(m.title), m.count.ToString());
                    Assert.IsNotNull(m.rewards);
                    Assert.Greater(m.rewards.Length, 0, m.count.ToString());
                    foreach (GuardianBadges.Reward r in m.rewards)
                    {
                        Assert.IsNotNull(items.FindById(r.itemId), $"{m.count}: 아이템 {r.itemId} 없음");
                        Assert.Greater(r.count, 0);
                    }
                    prev = m.count;
                }
                Assert.AreEqual(GuardianBadges.Total, prev, "마지막 이정표는 전부 모으기");
            }
            finally { Object.DestroyImmediate(items); }
        }

        // ── 계산 ──

        [Test]
        public void Claimable_OnlyReachedAndUnclaimed_Ascending()
        {
            CollectionAssert.IsEmpty(GuardianBadges.Claimable(3, new HashSet<int>()));
            CollectionAssert.AreEqual(new[] { 4 }, GuardianBadges.Claimable(4, new HashSet<int>()));
            CollectionAssert.AreEqual(new[] { 8 }, GuardianBadges.Claimable(9, new HashSet<int> { 4 }));
            CollectionAssert.AreEqual(new[] { 4, 8, 13 }, GuardianBadges.Claimable(13, null));
            CollectionAssert.IsEmpty(GuardianBadges.Claimable(13, new HashSet<int> { 4, 8, 13 }));
        }

        [Test]
        public void ParseClaimed_DropsGarbage_FormatSorts()
        {
            HashSet<int> set = GuardianBadges.ParseClaimed(" 8, 4,x,5,,13,4");
            CollectionAssert.AreEquivalent(new[] { 4, 8, 13 }, set);
            Assert.AreEqual("4,8,13", GuardianBadges.FormatClaimed(set));
            CollectionAssert.IsEmpty(GuardianBadges.ParseClaimed(null));
        }

        [Test]
        public void CountEarned_CountsOnlyTableRegions()
        {
            Assert.AreEqual(GuardianBadges.Total, GuardianBadges.CountEarned(_ => true));
            Assert.AreEqual(1, GuardianBadges.CountEarned(id => id == "pond" || id == "not_a_region"));
            Assert.AreEqual(0, GuardianBadges.CountEarned(null));
        }

        // ── 저장 경로 ──

        [Test]
        public void CloudDto_CarriesClaimedField_WithEmptyDefault()
        {
            FieldInfo f = typeof(GameSaveData).GetField("badgeMilestonesClaimed");
            Assert.IsNotNull(f, "클라우드에 안 오르면 기기를 바꿀 때마다 같은 보상을 다시 받는다");
            Assert.AreEqual("", f.GetValue(new GameSaveData()));

            FieldInfo keys = typeof(SaveScope).GetField("ScopedStringPrefsKeys", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(keys);
            CollectionAssert.Contains((string[])keys.GetValue(null), GameConstants.PrefsKeys.BadgeMilestonesClaimed,
                "계정 삭제 뒤 같은 uid로 돌아오면 수령 기록이 부활한다");
        }

        // ── 서비스 ──

        [Test]
        public void Service_AwardsOnFirstDefeatOnly_AndGrantsMilestoneOnce()
        {
            string[] keys = { UnlockKey, GuardianKey, GameConstants.PrefsKeys.BadgeMilestonesClaimed };
            var backup = new Dictionary<string, string>();
            foreach (string k in keys)
            {
                string scoped = SaveScope.PrefsKey(k);
                if (PlayerPrefs.HasKey(scoped)) backup[scoped] = PlayerPrefs.GetString(scoped);
                PlayerPrefs.DeleteKey(scoped);
            }

            var host = new GameObject("GuardianBadgeTests");
            try
            {
                RegionManager regions = host.AddComponent<RegionManager>();
                regions.Initialize(RegionDefinitions.CreateAll());
                GuardianBadgeService service = host.AddComponent<GuardianBadgeService>();
                service.AutoWire(regions, null);

                var awards = new List<GuardianBadgeService.Award>();
                service.BadgeAwarded += awards.Add;

                foreach (string id in new[] { "meadow", "pond", "forest" }) regions.DefeatGuardian(id);
                Assert.AreEqual(3, awards.Count);
                Assert.AreEqual(3, awards[2].earned);
                CollectionAssert.IsEmpty(awards[2].milestones);

                regions.DefeatGuardian("garden");
                Assert.AreEqual(4, awards.Count);
                CollectionAssert.AreEqual(new[] { 4 }, awards[3].milestones, "4번째 배지와 함께 이정표를 준다");
                Assert.IsTrue(service.IsClaimed(4));
                Assert.IsFalse(service.TryClaim(4), "한 번만 받는다");

                // 이미 깬 수문장을 필드에서 다시 이긴 경우 — 봉인만 걷고 배지는 다시 안 뜬다.
                Assert.IsFalse(regions.TryDefeatGuardian("meadow", "test"));
                regions.DefeatGuardian("meadow");
                Assert.AreEqual(4, awards.Count);
                Assert.AreEqual(4, service.EarnedCount);
                Assert.AreEqual(0, service.ClaimableCount);

                // 수령 기록은 다시 읽어도 남는다(클라우드 적용 뒤 경로).
                service.ReloadFromDisk();
                Assert.IsTrue(service.IsClaimed(4));
            }
            finally
            {
                Object.DestroyImmediate(host);
                foreach (string k in keys)
                {
                    string scoped = SaveScope.PrefsKey(k);
                    if (backup.TryGetValue(scoped, out string v)) PlayerPrefs.SetString(scoped, v);
                    else PlayerPrefs.DeleteKey(scoped);
                }
                PlayerPrefs.Save();
            }
        }
    }
}
#endif
