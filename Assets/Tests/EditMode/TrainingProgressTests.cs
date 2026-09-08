#if UNITY_EDITOR
using InsectGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 누적 훈련 진척도.
    ///
    /// 훈련은 한 번에 기술을 주지 않는다 — 같은 기술을 필요 횟수만큼 훈련해야 습득된다.
    /// 진척도는 <c>player_insects.json</c>에 실려 클라우드까지 따라가므로 직렬화 왕복도 함께 본다
    /// (<c>rules/save-system.md</c>: PlayerInsectData 필드 추가는 DTO 4점 변경이 필요 없다).
    /// </summary>
    [TestFixture]
    public class TrainingProgressTests
    {
        private static PlayerInsectData NewInsect()
        {
            return new PlayerInsectData { instanceId = "test", insectId = "test_beetle", level = 10 };
        }

        [Test]
        public void GetTrainingProgress_Untrained_IsZero()
        {
            Assert.AreEqual(0, NewInsect().GetTrainingProgress("tr_charge"));
        }

        [Test]
        public void AddTrainingProgress_Accumulates()
        {
            PlayerInsectData insect = NewInsect();

            Assert.AreEqual(1, insect.AddTrainingProgress("tr_charge"));
            Assert.AreEqual(2, insect.AddTrainingProgress("tr_charge"));
            Assert.AreEqual(2, insect.GetTrainingProgress("tr_charge"));
        }

        [Test]
        public void AddTrainingProgress_SkillsAreIndependent()
        {
            PlayerInsectData insect = NewInsect();
            insect.AddTrainingProgress("tr_charge");
            insect.AddTrainingProgress("tr_charge");
            insect.AddTrainingProgress("tr_sting");

            Assert.AreEqual(2, insect.GetTrainingProgress("tr_charge"));
            Assert.AreEqual(1, insect.GetTrainingProgress("tr_sting"),
                "다른 기술의 진척이 섞이면 한 기술만 훈련해도 전부 배워진다");
        }

        [Test]
        public void PruneTrainingProgress_DropsLearnedAndUnknown_KeepsLive()
        {
            PlayerInsectData insect = NewInsect();
            insect.AddTrainingProgress("tr_charge");     // 살아 있는 진척
            insect.AddTrainingProgress("tr_sting");      // 이미 배움 → 죽은 항목
            insect.AddTrainingProgress("tr_removed");    // DB에서 사라짐 → 죽은 항목
            insect.LearnSkill("tr_sting");

            bool changed = insect.PruneTrainingProgress(id => id == "tr_charge" || id == "tr_sting");

            Assert.IsTrue(changed);
            Assert.AreEqual(1, insect.GetTrainingProgress("tr_charge"), "진행 중인 진척은 남는다");
            Assert.AreEqual(0, insect.GetTrainingProgress("tr_sting"));
            Assert.AreEqual(0, insect.GetTrainingProgress("tr_removed"));
            Assert.AreEqual(1, insect.trainingProgress.Count);
        }

        [Test]
        public void PruneTrainingProgress_NoRegistry_OnlyDropsLearned()
        {
            // 부트 로드에는 레지스트리가 아직 없다 — 미상 기술을 함부로 지우면 안 된다.
            PlayerInsectData insect = NewInsect();
            insect.AddTrainingProgress("tr_charge");
            insect.AddTrainingProgress("tr_sting");
            insect.LearnSkill("tr_sting");

            Assert.IsTrue(insect.PruneTrainingProgress(null));
            Assert.AreEqual(1, insect.GetTrainingProgress("tr_charge"));
            Assert.AreEqual(0, insect.GetTrainingProgress("tr_sting"));
            Assert.IsFalse(insect.PruneTrainingProgress(null), "두 번째는 바뀐 게 없어야 한다");
        }

        [Test]
        public void GetTrainingProgress_PrefixCollision_DoesNotLeak()
        {
            // "tr_sting"과 "tr_sting_plus"처럼 앞부분이 같은 id가 섞이면 안 된다.
            // 구분자 ':'를 붙여 비교하는 이유가 이것이다.
            PlayerInsectData insect = NewInsect();
            insect.AddTrainingProgress("tr_sting_plus");

            Assert.AreEqual(0, insect.GetTrainingProgress("tr_sting"),
                "접두사가 같은 다른 기술의 진척을 읽으면 엉뚱한 기술이 먼저 배워진다");
            Assert.AreEqual(1, insect.GetTrainingProgress("tr_sting_plus"));
        }

        /// <summary>
        /// 되돌리기는 <b>제자리</b>여야 한다. 옛 롤백은 "마지막 항목이 방금 올린 그것"이라고
        /// 가정하고 <c>trainingProgress[Count-1]</c>을 고쳤는데, <c>AddTrainingProgress</c>는
        /// 기존 항목을 제자리에서 갱신하므로 그 가정이 깨진다 — 엉뚱한 기술의 진척이 깎였다.
        /// </summary>
        [Test]
        public void RemoveTrainingProgress_TouchesOnlyThatSkill()
        {
            PlayerInsectData insect = NewInsect();
            insect.AddTrainingProgress("tr_charge");   // 먼저 들어가 앞자리를 차지한다
            insect.AddTrainingProgress("tr_sting");
            insect.AddTrainingProgress("tr_charge");   // 제자리 갱신 → 마지막 항목은 tr_sting이다

            insect.RemoveTrainingProgress("tr_charge");

            Assert.AreEqual(1, insect.GetTrainingProgress("tr_charge"));
            Assert.AreEqual(1, insect.GetTrainingProgress("tr_sting"),
                "되돌리기가 다른 기술의 진척을 깎으면 안 된다");
        }

        [Test]
        public void RemoveTrainingProgress_AtZero_DropsEntry()
        {
            PlayerInsectData insect = NewInsect();
            insect.AddTrainingProgress("tr_charge");
            insect.RemoveTrainingProgress("tr_charge");

            Assert.AreEqual(0, insect.GetTrainingProgress("tr_charge"));
            Assert.AreEqual(0, insect.trainingProgress.Count, "0이 된 항목은 남기지 않는다");

            insect.RemoveTrainingProgress("tr_charge");   // 없는 것을 또 물러도 안전해야 한다
            Assert.AreEqual(0, insect.GetTrainingProgress("tr_charge"));
        }

        [Test]
        public void ClearTrainingProgress_RemovesOnlyThatSkill()
        {
            PlayerInsectData insect = NewInsect();
            insect.AddTrainingProgress("tr_charge");
            insect.AddTrainingProgress("tr_sting");

            insect.ClearTrainingProgress("tr_charge");

            Assert.AreEqual(0, insect.GetTrainingProgress("tr_charge"));
            Assert.AreEqual(1, insect.GetTrainingProgress("tr_sting"));
        }

        [Test]
        public void TrainingProgress_SurvivesJsonRoundTrip()
        {
            PlayerInsectData insect = NewInsect();
            insect.AddTrainingProgress("tr_charge");
            insect.AddTrainingProgress("tr_charge");
            insect.AddTrainingProgress("tr_sting");

            PlayerInsectData loaded = JsonUtility.FromJson<PlayerInsectData>(JsonUtility.ToJson(insect));

            Assert.AreEqual(2, loaded.GetTrainingProgress("tr_charge"));
            Assert.AreEqual(1, loaded.GetTrainingProgress("tr_sting"));
        }

        /// <summary>
        /// 구세이브 호환 — 이 필드가 없는 JSON을 읽어도 빈 리스트로 남아야 한다.
        /// JsonUtility는 JSON에 없는 필드를 건드리지 않으므로 C# 초기값이 그대로 남는다.
        /// </summary>
        [Test]
        public void TrainingProgress_MissingInOldSave_IsEmptyNotNull()
        {
            const string legacy = "{\"instanceId\":\"old\",\"insectId\":\"test_beetle\",\"level\":5}";
            PlayerInsectData loaded = JsonUtility.FromJson<PlayerInsectData>(legacy);

            Assert.IsNotNull(loaded.trainingProgress, "null이면 첫 훈련에서 NRE가 난다");
            Assert.AreEqual(0, loaded.trainingProgress.Count);
            Assert.AreEqual(0, loaded.GetTrainingProgress("tr_charge"));
        }
    }
}
#endif
