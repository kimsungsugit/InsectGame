#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.UI;

namespace InsectGame.Tests
{
    /// <summary>
    /// 잠긴 의상 카드의 해금 조건 문구.
    ///
    /// <c>OutfitItem.unlockCondition</c>은 <c>"region_garden"</c>·<c>"level_15"</c> 같은 원문
    /// 토큰인데, 한때 그 값이 카드에 **그대로** 그려져 한국어 게임에 영문 식별자가 노출됐다.
    /// <see cref="CharacterOutfitUI.DescribeUnlockCondition"/>이 문장으로 바꾼다.
    ///
    /// 해금 <b>판정</b>은 여기 범위가 아니다 — 저장소에 조건을 평가해 소유를 부여하는 코드가
    /// 아직 없다(별건). 이 테스트는 표시만 본다.
    /// </summary>
    [TestFixture]
    public class OutfitUnlockConditionTextTests
    {
        private static readonly Regex Ascii = new Regex("^[a-z0-9_]+$");

        [Test]
        public void EveryAuthoredCondition_TranslatesToKorean()
        {
            int checkedCount = 0;
            foreach (OutfitItem item in CharacterOutfitManager.BuildCatalog())
            {
                if (string.IsNullOrEmpty(item.unlockCondition)) continue;
                checkedCount++;

                string text = CharacterOutfitUI.DescribeUnlockCondition(item.unlockCondition);
                Assert.IsFalse(string.IsNullOrEmpty(text), $"{item.itemId}: 문구가 비었다");
                Assert.AreNotEqual(item.unlockCondition, text,
                    $"{item.itemId}: 원문 토큰 '{item.unlockCondition}'이 그대로 표시된다");
                Assert.IsFalse(Ascii.IsMatch(text),
                    $"{item.itemId}: 변환 결과가 여전히 영문 식별자다 — '{text}'");
            }

            Assert.Greater(checkedCount, 0,
                "unlockCondition을 가진 의상이 하나도 없다 — 카탈로그가 바뀌었으면 이 테스트도 확인할 것");
        }

        [Test]
        public void RegionCondition_UsesRegionDisplayName()
        {
            // 지역명을 문자열로 박으면 리전을 고칠 때 낡는다 — RegionDefinitions에서 파생돼야 한다.
            Dictionary<string, string> names = new Dictionary<string, string>();
            foreach (RegionData r in RegionDefinitions.CreateAll())
            {
                if (r != null && !string.IsNullOrEmpty(r.regionId)) names[r.regionId] = r.displayName;
            }

            foreach (KeyValuePair<string, string> pair in names)
            {
                string text = CharacterOutfitUI.DescribeUnlockCondition("region_" + pair.Key);
                StringAssert.Contains(pair.Value, text,
                    $"region_{pair.Key}: 표시명 '{pair.Value}'이 문구에 없다");
            }
        }

        [Test]
        public void LevelCondition_ShowsTheNumber()
        {
            StringAssert.Contains("15", CharacterOutfitUI.DescribeUnlockCondition("level_15"));
            StringAssert.Contains("Lv.", CharacterOutfitUI.DescribeUnlockCondition("level_15"));
        }

        [Test]
        public void UnknownForms_FallBackToToken_NotEmpty()
        {
            // 새 조건 형식을 추가했는데 분기를 안 넣으면, 빈 칸이 아니라 토큰이라도 보여야 한다.
            Assert.AreEqual("", CharacterOutfitUI.DescribeUnlockCondition(""));
            Assert.AreEqual("", CharacterOutfitUI.DescribeUnlockCondition(null));
            Assert.AreEqual("wat_unknown", CharacterOutfitUI.DescribeUnlockCondition("wat_unknown"));
            // 형식은 맞는데 숫자가 아니면 토큰 그대로(조용히 "Lv.0"으로 거짓말하지 않는다).
            Assert.AreEqual("level_abc", CharacterOutfitUI.DescribeUnlockCondition("level_abc"));
        }

        [Test]
        public void UnknownRegionId_FallsBackToIdNotCrash()
        {
            string text = CharacterOutfitUI.DescribeUnlockCondition("region_nowhere");
            StringAssert.Contains("nowhere", text);
        }
    }
}
#endif
