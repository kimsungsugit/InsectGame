#if UNITY_EDITOR
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 기술별 요구 레벨(<c>InsectSkill.requiredLevel</c>)과 누적 훈련 횟수.
    ///
    /// 옛 배치는 요구 레벨이 <b>훈련 방식 단위</b>뿐이라 "극한 훈련"이 곤충 Lv6에 위력
    /// 55·65·75를 한꺼번에 열었다. Lv6 야생 Common의 MaxHp가 62~73인데 <c>tr_doom_sting</c>
    /// 한 방이 (75 + 6×2) = <b>87</b>이라 상성·공방비를 곱하기도 전에 즉사였다.
    ///
    /// 여기서는 <c>collection</c>을 배선하지 않는다 — 속성이 <c>None</c>인 기술은
    /// <c>IsCompatibleWithInsect</c>가 곤충 데이터 없이도 통과시키므로, 레벨 게이트만
    /// 따로 떼어 볼 수 있다.
    /// </summary>
    [TestFixture]
    public class SkillLevelGateTests
    {
        private GameObject host;
        private TrainingManager training;
        private InsectSkill low;
        private InsectSkill high;
        private TrainingMethod method;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("SkillLevelGateTests");
            training = host.AddComponent<TrainingManager>();

            low = MakeSkill("tr_low", 10, 1);
            high = MakeSkill("tr_high", 40, 34);

            method = new TrainingMethod
            {
                methodId = "extreme",
                displayName = "극한 훈련",
                candyCost = 20,
                requiredLevel = 1,
                skillPool = new[] { low.skillId, high.skillId }
            };

            training.Initialize(new[] { method }, new[] { low, high });
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (low != null) Object.DestroyImmediate(low);
            if (high != null) Object.DestroyImmediate(high);
        }

        private static InsectSkill MakeSkill(string id, int power, int requiredLevel)
        {
            InsectSkill s = ScriptableObject.CreateInstance<InsectSkill>();
            s.skillId = id;
            s.displayName = id;
            s.power = power;
            s.requiredLevel = requiredLevel;
            s.element = InsectElement.None;   // 속성 호환을 빼고 레벨 게이트만 본다
            return s;
        }

        private static PlayerInsectData InsectAtLevel(int level)
        {
            return new PlayerInsectData { instanceId = "t", insectId = "test_beetle", level = level };
        }

        [Test]
        public void GetAvailableSkills_BelowRequiredLevel_ExcludesHighSkill()
        {
            InsectSkill[] available = training.GetAvailableSkills(method, InsectAtLevel(6));

            CollectionAssert.Contains(available, low);
            CollectionAssert.DoesNotContain(available, high,
                "Lv6에 위력 40 기술이 열리면 전투가 한 방에 끝난다 — 그게 옛 동작이었다");
        }

        [Test]
        public void GetAvailableSkills_AtRequiredLevel_IncludesHighSkill()
        {
            CollectionAssert.Contains(training.GetAvailableSkills(method, InsectAtLevel(34)), high);
        }

        [Test]
        public void GetAvailableSkillCount_MatchesGetAvailableSkills()
        {
            // 목록과 개수는 다른 메서드다(개수 쪽은 매 프레임 도는 무할당 경로).
            // 규칙이 갈리면 화면의 배지 숫자와 실제 목록이 어긋난다.
            foreach (int level in new[] { 1, 6, 33, 34, 50 })
            {
                PlayerInsectData insect = InsectAtLevel(level);
                Assert.AreEqual(training.GetAvailableSkills(method, insect).Length,
                    training.GetAvailableSkillCount(method, insect),
                    $"Lv{level}에서 목록과 개수가 다르다");
            }
        }

        // ── 누적 훈련 횟수 ──

        [Test]
        public void GetRequiredSessions_ScalesWithPower()
        {
            Assert.AreEqual(1, training.GetRequiredSessions(method, low),   // 1 + 10/12 = 1
                "기본기는 한 번에 배워도 된다");
            Assert.AreEqual(4, training.GetRequiredSessions(method, high)); // 1 + 40/12 = 4
        }

        [Test]
        public void GetRequiredSessions_IsCappedAtFive()
        {
            InsectSkill monster = MakeSkill("tr_monster", 999, 1);
            try
            {
                Assert.AreEqual(5, training.GetRequiredSessions(method, monster),
                    "상한이 없으면 고위력 기술이 사실상 습득 불가가 된다");
            }
            finally { Object.DestroyImmediate(monster); }
        }

        /// <summary>
        /// 기술 디스크는 <b>1회</b>다 — 사서 얻는 물건이라 즉시 습득이 그 값어치이고,
        /// 누적 훈련과 대비돼야 둘 다 존재 이유가 생긴다.
        /// </summary>
        [Test]
        public void GetRequiredSessions_DiscMethod_IsAlwaysOne()
        {
            TrainingMethod disc = new TrainingMethod
            {
                methodId = TrainingManager.DiscMethodId,
                displayName = "기술 디스크",
                candyCost = 1,
                requiredLevel = 1,
                skillPool = new string[0]
            };

            Assert.AreEqual(1, training.GetRequiredSessions(disc, high));
        }

        [Test]
        public void DiscMethod_WithoutInventory_OffersNothing()
        {
            TrainingMethod disc = new TrainingMethod
            {
                methodId = TrainingManager.DiscMethodId,
                skillPool = new string[0]
            };

            Assert.AreEqual(0, training.GetAvailableSkills(disc, InsectAtLevel(50)).Length,
                "디스크를 안 들고 있으면 목록이 비어야 한다(고정 풀을 쓰면 안 된다)");
        }
    }
}
#endif
