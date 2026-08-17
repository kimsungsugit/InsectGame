#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.UI;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 보유 곤충 목록 정렬(<see cref="InsectBrowseSort"/>). 배틀팀 피커와 컬렉션 화면이 같은
    /// 것을 쓰므로 여기가 깨지면 두 화면이 동시에 어긋난다.
    ///
    /// <see cref="PlayerInsectCollection"/>은 MonoBehaviour라 씬 없이 만들 수 없어 null을 넘긴다.
    /// 등급·CP는 그 조회에 기대므로 여기서는 <b>레벨·최근·팀먼저·안정성</b>만 본다 —
    /// 등급/CP 경로는 조회가 없을 때 전부 동점이 되어 instanceId 타이브레이커로 떨어지고,
    /// 그 동작 자체를 <c>RarityMode_WithoutCollection_FallsBackToStableOrder</c>가 고정한다.
    /// </summary>
    [TestFixture]
    public class InsectBrowseSortTests
    {
        private static PlayerInsectData Make(string id, int level = 1, long captured = 0)
        {
            return new PlayerInsectData { instanceId = id, insectId = "test_beetle", level = level, capturedUnix = captured };
        }

        private static List<PlayerInsectData> Run(
            IReadOnlyList<PlayerInsectData> source, InsectSortMode mode,
            System.Func<PlayerInsectData, bool> isInTeam = null, bool teamFirst = false)
        {
            List<PlayerInsectData> target = new List<PlayerInsectData>();
            InsectBrowseSort.Sort(source, null, mode, target, isInTeam, teamFirst);
            return target;
        }

        [Test]
        public void LevelMode_SortsDescending()
        {
            List<PlayerInsectData> src = new List<PlayerInsectData> { Make("a", 3), Make("b", 9), Make("c", 5) };

            List<PlayerInsectData> result = Run(src, InsectSortMode.Level);

            Assert.AreEqual("b", result[0].instanceId);
            Assert.AreEqual("c", result[1].instanceId);
            Assert.AreEqual("a", result[2].instanceId);
        }

        [Test]
        public void RecentMode_SortsNewestFirst_UnknownCaptureTimeLast()
        {
            // capturedUnix 0은 구세이브의 '미상'이다 — 최신으로 올라오면 안 된다.
            List<PlayerInsectData> src = new List<PlayerInsectData>
            {
                Make("old", 1, 100), Make("unknown", 1, 0), Make("new", 1, 500)
            };

            List<PlayerInsectData> result = Run(src, InsectSortMode.Recent);

            Assert.AreEqual("new", result[0].instanceId);
            Assert.AreEqual("old", result[1].instanceId);
            Assert.AreEqual("unknown", result[2].instanceId);
        }

        [Test]
        public void TeamFirst_LiftsTeamMembers_AndKeepsInnerOrder()
        {
            List<PlayerInsectData> src = new List<PlayerInsectData>
            {
                Make("hi_lv_bench", 20), Make("lo_lv_team", 2), Make("mid_lv_team", 10)
            };

            List<PlayerInsectData> result = Run(src, InsectSortMode.Level,
                pid => pid.instanceId.EndsWith("_team"), teamFirst: true);

            // 팀이 먼저 — 레벨 20짜리 벤치보다 레벨 2 팀원이 위에 온다.
            Assert.AreEqual("mid_lv_team", result[0].instanceId);
            Assert.AreEqual("lo_lv_team", result[1].instanceId);
            Assert.AreEqual("hi_lv_bench", result[2].instanceId);
        }

        [Test]
        public void TeamFirst_Disabled_IgnoresTeamPredicate()
        {
            List<PlayerInsectData> src = new List<PlayerInsectData> { Make("bench", 20), Make("team", 2) };

            List<PlayerInsectData> result = Run(src, InsectSortMode.Level,
                pid => pid.instanceId == "team", teamFirst: false);

            Assert.AreEqual("bench", result[0].instanceId);
        }

        [Test]
        public void Ties_BreakByInstanceId_SoOrderNeverJitters()
        {
            // 같은 레벨 3마리. 동점 처리가 없으면 List.Sort가 불안정해 패스마다 순서가 흔들린다.
            List<PlayerInsectData> src = new List<PlayerInsectData> { Make("c", 7), Make("a", 7), Make("b", 7) };

            List<PlayerInsectData> first = Run(src, InsectSortMode.Level);
            List<PlayerInsectData> second = Run(first, InsectSortMode.Level);

            Assert.AreEqual("a", first[0].instanceId);
            Assert.AreEqual("b", first[1].instanceId);
            Assert.AreEqual("c", first[2].instanceId);
            for (int i = 0; i < first.Count; i++)
                Assert.AreEqual(first[i].instanceId, second[i].instanceId, "재정렬해도 순서가 같아야 한다");
        }

        [Test]
        public void RarityMode_WithoutCollection_FallsBackToStableOrder()
        {
            // 종 데이터를 못 찾으면 등급·CP가 전부 동점이 된다. 예외로 죽지 않고
            // instanceId 순서로 떨어지는 것이 이 경로의 계약이다.
            List<PlayerInsectData> src = new List<PlayerInsectData> { Make("z"), Make("m"), Make("a") };

            List<PlayerInsectData> result = Run(src, InsectSortMode.Rarity);

            Assert.AreEqual("a", result[0].instanceId);
            Assert.AreEqual("m", result[1].instanceId);
            Assert.AreEqual("z", result[2].instanceId);
        }

        [Test]
        public void Sort_ClearsTarget_AndDropsNullEntries()
        {
            List<PlayerInsectData> target = new List<PlayerInsectData> { Make("stale") };
            List<PlayerInsectData> src = new List<PlayerInsectData> { Make("a", 2), null, Make("b", 5) };

            InsectBrowseSort.Sort(src, null, InsectSortMode.Level, target);

            Assert.AreEqual(2, target.Count, "이전 내용이 남거나 null이 섞이면 안 된다");
            Assert.AreEqual("b", target[0].instanceId);
            Assert.AreEqual("a", target[1].instanceId);
        }

        [Test]
        public void Sort_NullSource_EmptiesTargetInsteadOfThrowing()
        {
            List<PlayerInsectData> target = new List<PlayerInsectData> { Make("stale") };

            InsectBrowseSort.Sort(null, null, InsectSortMode.Level, target);

            Assert.AreEqual(0, target.Count);
        }

        [Test]
        public void Order_CoversEveryMode_SoChipsCannotMissOne()
        {
            // UI 두 곳이 Order를 돌아 칩을 그린다 — enum에 값을 늘리고 Order를 안 고치면
            // 그 기준은 화면에 영영 안 나온다(선택할 방법이 없다).
            System.Array modes = System.Enum.GetValues(typeof(InsectSortMode));
            Assert.AreEqual(modes.Length, InsectBrowseSort.Order.Length);
            foreach (InsectSortMode mode in modes)
                Assert.Contains(mode, InsectBrowseSort.Order, $"{mode}가 Order에 없다");
        }

        [Test]
        public void Label_IsNeverEmpty_ForAnyMode()
        {
            foreach (InsectSortMode mode in System.Enum.GetValues(typeof(InsectSortMode)))
                Assert.IsFalse(string.IsNullOrEmpty(InsectBrowseSort.Label(mode)), $"{mode} 라벨 없음");
        }

        /// <summary>
        /// 라벨이 서로 달라야 한다. "비지 않았나"만 보면 <c>default</c>가 아무 값이나 돌려줘도
        /// 통과한다 — 실제로 <c>Label</c>의 default가 "등급"이라, 모드를 하나 더 늘리면 칩 두 개가
        /// 나란히 "등급"으로 뜨면서 정렬은 조용히 Rarity로 동작했다. 그 상태를 여기서 잡는다.
        /// </summary>
        [Test]
        public void Label_IsDistinct_ForEveryMode()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (InsectSortMode mode in System.Enum.GetValues(typeof(InsectSortMode)))
            {
                string label = InsectBrowseSort.Label(mode);
                Assert.IsTrue(seen.Add(label),
                    $"{mode}의 라벨 \"{label}\"이 다른 모드와 겹친다 — switch에 case를 빠뜨렸다");
            }
        }
    }
}
#endif
