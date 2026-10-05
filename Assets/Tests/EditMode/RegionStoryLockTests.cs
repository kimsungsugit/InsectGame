#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.NPC;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 이야기 잠금 — 서릿길은 집게를, 잿불 골짜기는 저울을 이겨야 열린다(앞 리전 수문장에 <b>더해서</b>, 2026-10-04).
    ///
    /// 판정의 단일 출처는 <see cref="RegionManager.IsRegionAccessible"/>다(울타리 통행·지도·HUD·지역 의뢰·섬 손님이 전부 읽는다).
    /// 깨지면 조용하다: 간부를 이겼는데 영영 안 열리거나, 수문장만 이기면 열려 간부전이 필수가 아니게 되거나,
    /// 이미 열렸던 지역이 닫혀 그 안을 돌던 플레이어가 갇힌다. 안내 문구가 이미 이긴 수문장을 다시 가리키는 것도 여기서 본다.
    /// </summary>
    [TestFixture]
    public class RegionStoryLockTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        // ── 순수 판정 ──

        [Test]
        public void SavedUnlock_NeverCloses()
        {
            // 이야기 잠금이 생기기 전에 열린 서릿길 — 간부를 안 이겼어도 그대로 열려 있다.
            Assert.IsTrue(RegionManager.IsOpenByProgress("frostline", true, false, _ => false));
        }

        [Test]
        public void GuardianOnly_StoryLockedRegionStaysClosed()
        {
            Assert.IsFalse(RegionManager.IsOpenByProgress("frostline", false, true, _ => false));
            Assert.IsFalse(RegionManager.IsOpenByProgress("emberfall", false, true, id => id == "ledger_grip"),
                "잿불 골짜기의 열쇠는 저울이다 — 집게 승리로는 안 열린다");
        }

        [Test]
        public void GuardianAndBoss_Opens()
        {
            Assert.IsTrue(RegionManager.IsOpenByProgress("frostline", false, true, id => id == "ledger_grip"));
            Assert.IsTrue(RegionManager.IsOpenByProgress("emberfall", false, true, id => id == "ledger_scale"));
        }

        [Test]
        public void BossOnly_WithoutGuardian_StaysClosed()
        {
            Assert.IsFalse(RegionManager.IsOpenByProgress("frostline", false, false, _ => true),
                "간부만 이기고 앞 리전 수문장을 안 넘었으면 닫혀 있다 — 잠금은 더하기다");
        }

        [Test]
        public void OtherRegions_GuardianIsEnough_AndNoGateMeansOldBehavior()
        {
            Assert.IsTrue(RegionManager.IsOpenByProgress("dunes", false, true, _ => false));
            Assert.IsFalse(RegionManager.IsOpenByProgress("dunes", false, false, _ => true));
            // 판정 함수가 없으면(미배선) 옛 동작 — 수문장만으로 열린다. 배선이 빠져 영영 못 여는 쪽보다 낫다.
            Assert.IsTrue(RegionManager.IsOpenByProgress("frostline", false, true, null));
        }

        [Test]
        public void DescribeLockText_NamesTheRealKey()
        {
            Assert.AreEqual("서릿길 — 모래언덕의 장수말벌에게 이겨야 열립니다",
                RegionManager.DescribeLockText("서릿길", RegionManager.LockKind.Guardian, "모래언덕의 장수말벌", "집게"));
            Assert.AreEqual("서릿길 — 집게에게 이겨야 열립니다",
                RegionManager.DescribeLockText("서릿길", RegionManager.LockKind.StoryDuel, "모래언덕의 장수말벌", "집게"));
            Assert.AreEqual("서릿길 — 아직 갈 수 없습니다",
                RegionManager.DescribeLockText("서릿길", RegionManager.LockKind.Unknown, null, null), "원인을 모르면 지어내지 않는다");
            Assert.AreEqual(string.Empty, RegionManager.DescribeLockText("서릿길", RegionManager.LockKind.Open, null, null));
        }

        // ── 표 ──

        [Test]
        public void StoryLocks_AreGripAndScale_WithTheirDialogueNames()
        {
            var expected = new Dictionary<string, string> { ["frostline"] = "ledger_grip", ["emberfall"] = "ledger_scale" };
            var actual = new Dictionary<string, string>();
            var regions = new Dictionary<string, RegionData>();
            foreach (RegionData r in RegionDefinitions.CreateAll()) regions[r.regionId] = r;

            foreach (RegionManager.StoryLock l in RegionManager.AllStoryLocks())
            {
                actual[l.regionId] = l.storyNpcId;
                Assert.IsTrue(regions.ContainsKey(l.regionId), $"{l.regionId}: 없는 리전");
                Assert.IsTrue(NpcBossDuels.TryGet(l.storyNpcId, out NpcBossDuels.BossDuel duel), $"{l.storyNpcId}: 간부 대결 표에 없다");
                // 안내 문구의 이름이 대결 컷인·대사창 이름표와 같아야 한다.
                Assert.AreEqual(duel.displayName, l.displayName, l.regionId);
                Assert.AreEqual(NpcDialogueDatabase.StorySpeakerName(l.storyNpcId), l.displayName, l.regionId);
            }
            CollectionAssert.AreEquivalent(expected, actual);
        }

        // ── 매니저 — 실제 판정 경로 (저장은 부르지 않는다) ──

        // 마스터 계정 우회(AuthManager.MasterPrivilegesActive)는 모든 리전을 연다. 앞선 통합 테스트가 PlayScene을 띄우면
        // AuthManager가 남아 이 PC의 마스터 계정 표지를 들고 있을 수 있다 — 그러면 잠금을 잴 수 없어 예전엔 Inconclusive로
        // 건너뛰었다(2026-10-04 전체 실행에서 3건). 잠금 판정 테스트 동안만 Instance를 비우고 끝나면 되돌린다.
        private AuthManager authBeforeTest;
        private static readonly PropertyInfo AuthInstance = typeof(AuthManager).GetProperty("Instance");

        [SetUp]
        public void HideAuthManager()
        {
            authBeforeTest = AuthManager.Instance;
            AuthInstance.GetSetMethod(true).Invoke(null, new object[] { null });
        }

        [TearDown]
        public void RestoreAuthManager()
        {
            AuthInstance.GetSetMethod(true).Invoke(null, new object[] { authBeforeTest });
            authBeforeTest = null;
        }

        private static RegionManager NewManager(GameObject host, IEnumerable<string> unlocked, IEnumerable<string> guardians,
            System.Func<string, bool> duelWon)
        {
            var manager = host.AddComponent<RegionManager>();
            typeof(RegionManager).GetField("regions", Private).SetValue(manager, RegionDefinitions.CreateAll());
            typeof(RegionManager).GetField("unlockedRegions", Private).SetValue(manager, new HashSet<string>(unlocked));
            typeof(RegionManager).GetField("defeatedGuardians", Private).SetValue(manager, new HashSet<string>(guardians));
            manager.AutoWireDuelGate(duelWon);
            return manager;
        }

        [Test]
        public void Manager_GuardianBeaten_GripNot_FrostlineLocked_PointsAtGrip()
        {
            GameObject host = new GameObject("RegionStoryLockTests");
            host.SetActive(false);
            try
            {
                var beaten = new HashSet<string>();
                RegionManager rm = NewManager(host, new[] { "meadow", "pond", "dunes" }, new[] { "hollow", "dunes" }, beaten.Contains);
                RegionData frost = rm.GetRegionById("frostline");

                Assert.IsFalse(rm.IsRegionAccessible(frost), "수문장만 이겼다 — 집게가 남았다");
                Assert.AreEqual(RegionManager.LockKind.StoryDuel, rm.GetLockKind(frost, out RegionData gate, out RegionManager.StoryLock lk));
                Assert.AreEqual("dunes", gate.regionId);
                Assert.AreEqual("ledger_grip", lk.storyNpcId);
                Assert.AreEqual("서릿길 — 집게에게 이겨야 열립니다", rm.DescribeLock(frost));

                beaten.Add("ledger_grip");
                Assert.IsTrue(rm.IsRegionAccessible(frost), "집게를 이기는 순간 열린다(해금 기록 없이 두 격파 기록으로)");
                Assert.AreEqual(string.Empty, rm.DescribeLock(frost));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void Manager_GuardianNotBeaten_PointsAtGuardianFirst()
        {
            GameObject host = new GameObject("RegionStoryLockTestsGuardian");
            host.SetActive(false);
            try
            {
                RegionManager rm = NewManager(host, new[] { "meadow" }, new string[0], _ => false);
                RegionData frost = rm.GetRegionById("frostline");
                Assert.AreEqual(RegionManager.LockKind.Guardian, rm.GetLockKind(frost, out RegionData gate, out _));
                Assert.AreEqual("dunes", gate.regionId);
                StringAssert.Contains(gate.guardianDisplayName, rm.DescribeLock(frost));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void Manager_SavedUnlock_StaysOpen_EvenWithoutGrip()
        {
            GameObject host = new GameObject("RegionStoryLockTestsSaved");
            host.SetActive(false);
            try
            {
                RegionManager rm = NewManager(host, new[] { "meadow", "frostline" }, new[] { "dunes" }, _ => false);
                Assert.IsTrue(rm.IsRegionAccessible(rm.GetRegionById("frostline")),
                    "이야기 잠금 전에 열린 지역은 닫지 않는다 — 안을 돌던 플레이어를 가두지 않는다");
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void Manager_DefeatGuardian_HoldsTheUnlockRecordWhileTheBossRemains()
        {
            // DefeatGuardian 자체는 저장을 부르므로 여기선 그 분기 판정만 본다 — 해금 기록을 적으면 영영 열린다.
            GameObject host = new GameObject("RegionStoryLockTestsHold");
            host.SetActive(false);
            try
            {
                var beaten = new HashSet<string>();
                RegionManager rm = NewManager(host, new[] { "meadow" }, new string[0], beaten.Contains);
                MethodInfo held = typeof(RegionManager).GetMethod("IsHeldByStoryLock", Private);
                Assert.IsTrue((bool)held.Invoke(rm, new object[] { "frostline" }));
                Assert.IsFalse((bool)held.Invoke(rm, new object[] { "hollow" }), "이야기 잠금이 없는 리전은 바로 적는다");
                beaten.Add("ledger_grip");
                Assert.IsFalse((bool)held.Invoke(rm, new object[] { "frostline" }), "집게를 먼저 이겼으면 수문장 격파가 곧바로 연다");
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
#endif
