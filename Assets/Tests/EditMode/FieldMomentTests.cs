#if UNITY_EDITOR
using InsectGame.Core;
using InsectGame.Spawning;
using InsectGame.Story;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 도입부의 "터지는 순간" 장치들 — 소식 대기열, 라온과의 내기, 첫 색다른 조우, 이야기 보상 문구.
    /// 화면(IMGUI)은 테스트로 못 보므로 규칙만 고정한다.
    /// </summary>
    [TestFixture]
    public class FieldMomentTests
    {
        // ── 소식 대기열 ──

        private static FieldMomentFeed NewFeed(out GameObject host)
        {
            host = new GameObject("FieldMomentTests");
            return host.AddComponent<FieldMomentFeed>();
        }

        [Test]
        public void Feed_Dequeues_InTheOrderPushed()
        {
            FieldMomentFeed feed = NewFeed(out GameObject host);
            try
            {
                feed.Push(FieldMomentKind.Reward, "하나", "a");
                feed.Push(FieldMomentKind.Rival, "둘");

                Assert.IsTrue(feed.TryDequeue(out FieldMoment first));
                Assert.AreEqual("하나", first.Title);
                Assert.AreEqual("a", first.Body);
                Assert.IsTrue(feed.TryDequeue(out FieldMoment second));
                Assert.AreEqual(FieldMomentKind.Rival, second.Kind);
                Assert.AreEqual(string.Empty, second.Body, "본문이 없으면 빈 문자열 — 화면이 null을 그리지 않는다");
                Assert.IsFalse(feed.TryDequeue(out _));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void Feed_Overflow_DropsTheOldest()
        {
            // 대사·영상에 오래 가려진 뒤 한참 지난 소식이 줄줄이 뜨면 안 된다.
            FieldMomentFeed feed = NewFeed(out GameObject host);
            try
            {
                for (int i = 0; i < FieldMomentFeed.MaxPending + 3; i++)
                    feed.Push(FieldMomentKind.Reward, "소식 " + i);

                Assert.AreEqual(FieldMomentFeed.MaxPending, feed.PendingCount);
                Assert.IsTrue(feed.TryDequeue(out FieldMoment oldest));
                Assert.AreEqual("소식 3", oldest.Title);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void Feed_EmptyTitle_IsIgnored()
        {
            FieldMomentFeed feed = NewFeed(out GameObject host);
            try
            {
                feed.Push(FieldMomentKind.Reward, string.Empty, "본문만");
                feed.Push(FieldMomentKind.Reward, null);
                Assert.AreEqual(0, feed.PendingCount);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void Feed_ItemName_FallsBackToId_WhenDatabaseIsMissing()
        {
            FieldMomentFeed feed = NewFeed(out GameObject host);
            try
            {
                Assert.AreEqual("net_silver", feed.ItemName("net_silver"));
                Assert.AreEqual(string.Empty, feed.ItemName(null));
            }
            finally { Object.DestroyImmediate(host); }
        }

        // ── 라온과의 내기 ──

        [TestCase(0f, 0)]
        [TestCase(24.9f, 0)]
        [TestCase(25f, 1)]
        [TestCase(54.9f, 1)]
        [TestCase(55f, 2)]
        [TestCase(89.9f, 2)]
        [TestCase(90f, 3)]
        [TestCase(600f, 3)]
        public void RivalCountAt_FollowsTheSchedule(float fieldSeconds, int expected)
        {
            Assert.AreEqual(expected, RivalRaceRules.RivalCountAt(fieldSeconds));
        }

        [Test]
        public void RivalRace_IsWinnableAtAnUnhurriedPace()
        {
            // 곤충 한 마리에 필드 시간 25초를 써도(찾아 걷는 시간만 센다) 세 번째를 잡을 때 라온은 아직 둘이다.
            // 이 여유가 사라지면 처음 하는 사람이 첫 내기를 거의 반드시 진다.
            float thirdCaptureAt = 25f * RivalRaceRules.Target;
            Assert.Less(RivalRaceRules.RivalCountAt(thirdCaptureAt - 0.1f), RivalRaceRules.Target);
        }

        [Test]
        public void IsDecided_PlayerReachingTarget_Wins_EvenOnATie()
        {
            Assert.IsTrue(RivalRaceRules.IsDecided(RivalRaceRules.Target, RivalRaceRules.Target, out bool won));
            Assert.IsTrue(won, "같은 순간이면 직접 잡은 쪽이 이긴다");
        }

        [Test]
        public void IsDecided_RivalReachingTargetFirst_PlayerLoses()
        {
            Assert.IsTrue(RivalRaceRules.IsDecided(2, RivalRaceRules.Target, out bool won));
            Assert.IsFalse(won);
        }

        [Test]
        public void IsDecided_NeitherAtTarget_IsOpen()
        {
            Assert.IsFalse(RivalRaceRules.IsDecided(2, 2, out _));
        }

        [Test]
        public void RivalRace_WinningPaysMoreThanLosing_AndNeverDiamonds()
        {
            // 다이아는 보상으로 줄 수 없다(서버 규칙 — rules/island.md). 보상은 캔디와 채집망뿐이다.
            Assert.Greater(RivalRaceRules.WinCandy, RivalRaceRules.LoseCandy);
            Assert.Greater(RivalRaceRules.LoseCandy, 0, "져도 빈손은 아니다");
            StringAssert.StartsWith("net_", RivalRaceRules.WinItemId);
        }

        [Test]
        public void RivalRace_StartBeat_ExistsInTheStory()
        {
            // 내기를 여는 비트 ID가 저작 데이터에서 사라지면 내기가 영영 안 열린다(예외도 경고도 없다).
            Assert.IsTrue(StoryService.TryGetBeat(RivalRaceController.StartBeatId, out _));
        }

        // ── 첫 색다른 조우 ──

        [TestCase("q_capture3", 2, true)]
        [TestCase("q_capture3", 3, true)]
        [TestCase("q_capture3", 1, false)]
        [TestCase("q_approach", 5, false)]
        [TestCase("q_battle", 2, false)]
        [TestCase(null, 2, false)]
        public void FirstShiny_TriggersOnlyBeforeTheThirdCapture(string activeQuestId, int progress, bool expected)
        {
            Assert.AreEqual(expected, FirstShinyRules.ShouldTrigger(activeQuestId, progress));
        }

        // ── 이야기 보상 문구 ──

        [Test]
        public void StoryRewardText_ListsEverythingGranted_WithTheActualInsectName()
        {
            var reward = new StoryReward
            {
                rewardCandy = 5, rewardItemId = "net_silver", rewardItemCount = 2,
                rewardInsectId = "longhorn_beetle", rewardInsectDisplayName = "첫 파트너", rewardInsectLevel = 6
            };

            // 첫 파트너는 고른 종으로 바뀐다 — 데이터의 이름("첫 파트너")이 아니라 실제로 준 이름이 떠야 한다.
            string text = StoryRewardText.Format(reward, id => id == "net_silver" ? "은빛 채집망" : null, "호수 잠자리");
            Assert.AreEqual("캔디 +5 · 은빛 채집망 ×2 · 호수 잠자리 Lv.6", text);
        }

        [Test]
        public void StoryRewardText_NothingGranted_IsEmpty()
        {
            Assert.AreEqual(string.Empty, StoryRewardText.Format(new StoryReward(), null, null));
            Assert.AreEqual(string.Empty, StoryRewardText.Format(null, null, null));
        }

        [Test]
        public void StoryRewardText_ItemWithZeroCount_AndInsectThatWasNotGranted_AreLeftOut()
        {
            // 지급부와 같은 조건이어야 한다 — 수량 0인 아이템·컬렉션이 없어 못 준 곤충은 "받았다"고 뜨면 안 된다.
            var reward = new StoryReward
            {
                rewardExp = 15, rewardItemId = "net_gold", rewardItemCount = 0, rewardInsectId = "stag_beetle"
            };
            Assert.AreEqual("경험치 +15", StoryRewardText.Format(reward, null, null));
        }

        [Test]
        public void StoryRewardText_UnknownItemName_FallsBackToId()
        {
            var reward = new StoryReward { rewardItemId = "mystery_box", rewardItemCount = 1 };
            Assert.AreEqual("mystery_box ×1", StoryRewardText.Format(reward, id => null, null));
        }
    }
}
#endif
