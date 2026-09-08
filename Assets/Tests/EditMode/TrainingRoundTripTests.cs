#if UNITY_EDITOR
using System.IO;
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// <see cref="TrainingManager.TrainSkill"/> 왕복 — 실제 캔디·아이템 인벤토리를 붙여 돈다.
    ///
    /// <c>SkillLevelGateTests</c>는 목록 API만, <c>TrainingProgressTests</c>는 진척 카운터만 본다.
    /// 그래서 실제 우회 방어선(<c>IsSkillAllowed</c>의 레벨 검사)·회차별 캔디 차감·교체 실패 환불·
    /// 디스크 소모는 어느 테스트도 실행하지 않았다(2026-09-09 재감사). 여기서 그 넷을 고정한다.
    ///
    /// 파일 IO: 인벤토리 두 종이 <c>SaveScope</c> 경로에 세이브를 쓴다. <c>DexCoinRewardTests</c>와
    /// 같은 방식으로 SetUp에서 백업하고 TearDown에서 복원해 개발 세이브를 보호한다.
    /// </summary>
    [TestFixture]
    public class TrainingRoundTripTests
    {
        private static readonly string[] SaveFiles =
        {
            GameConstants.SaveFiles.PlayerCandies,
            GameConstants.SaveFiles.PlayerItems,
        };

        private GameObject host;
        private TrainingManager training;
        private PlayerCandyInventory candy;
        private PlayerItemInventory items;
        private ItemDatabase itemDb;
        private InsectSkill low;
        private InsectSkill high;
        private ItemData disc;
        private TrainingMethod extreme;
        private TrainingMethod discMethod;

        [SetUp]
        public void SetUp()
        {
            foreach (string f in SaveFiles)
            {
                string path = SaveScope.FilePath(f);
                if (File.Exists(path)) File.Move(path, path + ".testbak");
            }

            host = new GameObject("TrainingRoundTripTests");
            candy = host.AddComponent<PlayerCandyInventory>();
            items = host.AddComponent<PlayerItemInventory>();
            training = host.AddComponent<TrainingManager>();

            low = MakeSkill("tr_low", 10, 1);      // 필요 회차 1 + 10/12 = 1
            high = MakeSkill("tr_high", 40, 34);   // 필요 회차 1 + 40/12 = 4

            disc = ScriptableObject.CreateInstance<ItemData>();
            disc.itemId = "disc_low";
            disc.displayName = "기술 디스크: low";
            disc.teachSkillId = low.skillId;
            itemDb = ScriptableObject.CreateInstance<ItemDatabase>();
            itemDb.items.Add(disc);

            extreme = new TrainingMethod
            {
                methodId = "extreme", displayName = "극한 훈련", candyCost = 20, requiredLevel = 1,
                skillPool = new[] { low.skillId, high.skillId }
            };
            discMethod = new TrainingMethod
            {
                methodId = TrainingManager.DiscMethodId, displayName = "기술 디스크", candyCost = 1, requiredLevel = 1,
                skillPool = new string[0]
            };

            training.Initialize(new[] { extreme, discMethod }, new[] { low, high });
            training.AutoWire((PlayerInsectCollection)null, candy);
            training.AutoWire(items, itemDb);
            candy.SetCandies(100);
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (low != null) Object.DestroyImmediate(low);
            if (high != null) Object.DestroyImmediate(high);
            if (disc != null) Object.DestroyImmediate(disc);
            if (itemDb != null) Object.DestroyImmediate(itemDb);
            foreach (string f in SaveFiles)
            {
                string path = SaveScope.FilePath(f);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".testbak")) File.Move(path + ".testbak", path);
            }
        }

        private static InsectSkill MakeSkill(string id, int power, int requiredLevel)
        {
            InsectSkill s = ScriptableObject.CreateInstance<InsectSkill>();
            s.skillId = id;
            s.displayName = id;
            s.power = power;
            s.requiredLevel = requiredLevel;
            s.element = InsectElement.None;   // 속성 호환을 빼고 게이트·비용·소모만 본다
            return s;
        }

        private static PlayerInsectData InsectAtLevel(int level)
        {
            return new PlayerInsectData { instanceId = "t", insectId = "test_beetle", level = level };
        }

        [Test]
        public void TrainSkill_BelowRequiredLevel_DirectCallIsRefusedAndChargesNothing()
        {
            // 목록에서 빠지는 것과 별개로 **직접 호출**도 막혀야 UI 우회가 안 된다.
            PlayerInsectData insect = InsectAtLevel(6);

            Assert.IsFalse(training.TrainSkill(extreme, insect, high.skillId));
            Assert.AreEqual(100, candy.Candies, "거부된 훈련이 캔디를 가져가면 안 된다");
            Assert.AreEqual(0, insect.GetTrainingProgress(high.skillId));
        }

        [Test]
        public void TrainSkill_MultiSession_ChargesEverySessionAndLearnsOnLast()
        {
            PlayerInsectData insect = InsectAtLevel(40);
            int required = training.GetRequiredSessions(extreme, high);
            Assert.Greater(required, 1, "이 테스트는 누적 훈련 기술이어야 의미가 있다");

            for (int i = 1; i < required; i++)
            {
                Assert.IsTrue(training.TrainSkill(extreme, insect, high.skillId));
                Assert.IsFalse(training.LastTrainingLearned, $"{i}회차에 배우면 누적이 아니다");
                Assert.AreEqual(i, training.LastTrainingProgress);
            }
            Assert.IsTrue(training.TrainSkill(extreme, insect, high.skillId));
            Assert.IsTrue(training.LastTrainingLearned);
            Assert.IsTrue(insect.HasLearnedSkill(high.skillId));
            Assert.AreEqual(100 - 20 * required, candy.Candies, "캔디는 매 회차 나간다");
            Assert.AreEqual(0, insect.GetTrainingProgress(high.skillId), "다 배우면 진척 기록은 지운다");
        }

        [Test]
        public void TrainSkill_ReplaceFails_RefundsCandyAndRollsBackProgress()
        {
            PlayerInsectData insect = InsectAtLevel(40);
            for (int i = 0; i < PlayerInsectData.MaxLearnedSkills; i++)
                Assert.IsTrue(insect.LearnSkill("filler_" + i));
            Assert.IsTrue(insect.IsSkillsFull());

            // 마지막 회차(low는 1회차)인데 교체 대상이 배운 목록에 없다 → ReplaceSkill 실패.
            Assert.IsFalse(training.TrainSkill(extreme, insect, low.skillId, "not_learned"));

            Assert.AreEqual(100, candy.Candies, "교체 실패 회차의 캔디는 돌려줘야 한다");
            Assert.AreEqual(0, insect.GetTrainingProgress(low.skillId), "실패한 회차의 진척은 무른다");
            Assert.IsFalse(insect.HasLearnedSkill(low.skillId));
        }

        [Test]
        public void TrainSkill_DiscMethod_ConsumesExactlyOneDisc()
        {
            items.AddItem(disc.itemId, 2);
            PlayerInsectData insect = InsectAtLevel(5);
            Assert.AreEqual(2, training.GetDiscCount(low.skillId));

            Assert.IsTrue(training.TrainSkill(discMethod, insect, low.skillId));

            Assert.IsTrue(training.LastTrainingLearned, "디스크는 1회에 끝난다");
            Assert.IsTrue(insect.HasLearnedSkill(low.skillId));
            Assert.AreEqual(1, items.GetCount(disc.itemId), "디스크는 정확히 1장만 소모");
            Assert.AreEqual(1, training.GetDiscCount(low.skillId));
            Assert.AreEqual(99, candy.Candies, "디스크 방식은 방식 고정 비용(1)만 받는다");
        }

        [Test]
        public void TrainSkill_DiscMethod_ZeroCost_StillLearns()
        {
            // 비용 0은 SpendCandy가 거부하는 값이다 — 차감을 건너뛰어야 "버튼은 켜졌는데 무반응"이 안 된다.
            discMethod.candyCost = 0;
            items.AddItem(disc.itemId, 1);
            PlayerInsectData insect = InsectAtLevel(5);

            Assert.IsTrue(training.CanTrain(discMethod, insect, low.skillId));
            Assert.IsTrue(training.TrainSkill(discMethod, insect, low.skillId));
            Assert.IsTrue(insect.HasLearnedSkill(low.skillId));
            Assert.AreEqual(100, candy.Candies);
            Assert.AreEqual(0, items.GetCount(disc.itemId));
        }

        [Test]
        public void TrainSkill_DiscMethod_WithoutDisc_IsRefused()
        {
            PlayerInsectData insect = InsectAtLevel(5);

            Assert.IsFalse(training.TrainSkill(discMethod, insect, low.skillId),
                "디스크가 없으면 풀에 없으므로 배울 수 없다");
            Assert.AreEqual(100, candy.Candies);
        }
    }
}
#endif
