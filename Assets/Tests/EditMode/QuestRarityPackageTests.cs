#if UNITY_EDITOR
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 등급 패키지 포획 퀘스트(QuestType.CaptureRarity)의 매칭 규칙.
    /// Capture(전체)·CaptureRare(Uncommon+)와 달리 등급 하나만 정확히 집어야 한다 —
    /// 여기가 어긋나면 일반 곤충을 아무리 잡아도 전설 패키지가 함께 차오른다.
    /// </summary>
    [TestFixture]
    public class QuestRarityPackageTests
    {
        private static TutorialQuest MakePackage(InsectRarity rarity)
        {
            return new TutorialQuest
            {
                questId = "s_pack_test",
                type = QuestType.CaptureRarity,
                requiredRarity = rarity,
                targetCount = 3,
                targetIncrement = 2,
                category = QuestCategory.Side,
                repeatable = true
            };
        }

        // TutorialQuestManager.ProgressSideCapture / NotifyCapture와 같은 판정식.
        private static bool Matches(TutorialQuest quest, InsectRarity captured)
        {
            switch (quest.type)
            {
                case QuestType.Capture: return true;
                case QuestType.CaptureRare: return captured >= InsectRarity.Uncommon;
                case QuestType.CaptureRarity: return captured == quest.requiredRarity;
                default: return false;
            }
        }

        [Test]
        public void CaptureRarity_ExactRarityOnly_Matches()
        {
            TutorialQuest rare = MakePackage(InsectRarity.Rare);
            Assert.IsTrue(Matches(rare, InsectRarity.Rare));
            Assert.IsFalse(Matches(rare, InsectRarity.Common));
            Assert.IsFalse(Matches(rare, InsectRarity.Uncommon));
            Assert.IsFalse(Matches(rare, InsectRarity.Epic), "상위 등급이 하위 패키지를 채우면 안 된다");
            Assert.IsFalse(Matches(rare, InsectRarity.Legendary));
        }

        [Test]
        public void CaptureRarity_CommonPackage_IgnoresHigherRarities()
        {
            TutorialQuest common = MakePackage(InsectRarity.Common);
            Assert.IsTrue(Matches(common, InsectRarity.Common));
            foreach (InsectRarity r in new[]
                     {
                         InsectRarity.Uncommon, InsectRarity.Rare,
                         InsectRarity.Epic, InsectRarity.Legendary
                     })
            {
                Assert.IsFalse(Matches(common, r), $"{r}이 일반 패키지를 채웠다");
            }
        }

        [Test]
        public void RequiredRarity_DefaultsToCommon_SoLegacyQuestsAreUnaffected()
        {
            // 기존 퀘스트는 이 필드를 지정하지 않는다 — enum 0(Common)이라 세이브·정의 호환이 유지된다.
            TutorialQuest legacy = new TutorialQuest { questId = "q_capture3", type = QuestType.Capture };
            Assert.AreEqual(InsectRarity.Common, legacy.requiredRarity);
            // Capture 타입이면 requiredRarity와 무관하게 모든 포획이 진행된다.
            Assert.IsTrue(Matches(legacy, InsectRarity.Legendary));
        }

        [Test]
        public void CaptureRare_StillMatchesUncommonAndAbove()
        {
            TutorialQuest rareOrBetter = new TutorialQuest
            {
                questId = "q_capture_rare",
                type = QuestType.CaptureRare
            };
            Assert.IsFalse(Matches(rareOrBetter, InsectRarity.Common));
            Assert.IsTrue(Matches(rareOrBetter, InsectRarity.Uncommon));
            Assert.IsTrue(Matches(rareOrBetter, InsectRarity.Legendary));
        }

        [Test]
        public void EveryRarity_HasExactlyOnePackageThatMatchesIt()
        {
            // 5등급 각각에 패키지가 하나씩 있고 서로 겹치지 않는다(등록된 s_pack_* 5종과 같은 구성).
            InsectRarity[] all =
            {
                InsectRarity.Common, InsectRarity.Uncommon, InsectRarity.Rare,
                InsectRarity.Epic, InsectRarity.Legendary
            };

            foreach (InsectRarity captured in all)
            {
                int matched = 0;
                foreach (InsectRarity packageRarity in all)
                {
                    if (Matches(MakePackage(packageRarity), captured)) matched++;
                }
                Assert.AreEqual(1, matched, $"{captured} 포획이 패키지 {matched}개를 동시에 채웠다");
            }
        }
    }
}
#endif
