#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 의상 창·2D 초상의 순수 계산부(2026-09-30 4단계). 그리기 자체(IMGUI)는 테스트 제외라
    /// 검수 빌드 <c>-battleScenario outfit</c> 캡처로 본다.
    /// </summary>
    [TestFixture]
    public class OutfitWindowTests
    {
        /// <summary>
        /// 상세 패널은 보너스를 **전부** 보여 준다. 예전 카드는 대표 1개만, 상세 툴팁은 이미 가진 옷에만 떴다 —
        /// 사기 전에 무엇이 좋은지 알 수 없었다.
        /// </summary>
        [Test]
        public void FullBonusText_ListsEveryNonZeroBonus()
        {
            OutfitStatBonus b = new OutfitStatBonus { captureChanceBonus = 0.02f, expMultiplier = 0.03f, defBonus = 0.01f };
            string text = CharacterOutfitUI.FullBonusText(b);
            StringAssert.Contains("포획 +2%", text);
            StringAssert.Contains("DEF +1%", text);
            StringAssert.Contains("경험치 +3%", text);
            StringAssert.DoesNotContain("ATK", text, "0인 보너스를 적었다");
            Assert.AreEqual("보너스 없음", CharacterOutfitUI.FullBonusText(default));
        }

        [Test]
        public void Filter_SplitsOwnedAndNotOwned()
        {
            Assert.IsTrue(CharacterOutfitUI.PassesFilter(CharacterOutfitUI.ItemFilter.All, true));
            Assert.IsTrue(CharacterOutfitUI.PassesFilter(CharacterOutfitUI.ItemFilter.All, false));
            Assert.IsTrue(CharacterOutfitUI.PassesFilter(CharacterOutfitUI.ItemFilter.Owned, true));
            Assert.IsFalse(CharacterOutfitUI.PassesFilter(CharacterOutfitUI.ItemFilter.Owned, false));
            Assert.IsTrue(CharacterOutfitUI.PassesFilter(CharacterOutfitUI.ItemFilter.NotOwned, false));
            Assert.IsFalse(CharacterOutfitUI.PassesFilter(CharacterOutfitUI.ItemFilter.NotOwned, true));
        }

        /// <summary>
        /// 모든 세트 구성품이 카드에서 세트를 찾아야 한다 — 예전 세트 점은 이미 한 벌 이상 입은 세트에만 찍혀,
        /// 시작하지 않은 세트는 무엇이 한 벌인지 알 수 없었다. 세트 구성품 id는 실재해야 한다(오타면 표시가 조용히 빠진다).
        /// </summary>
        [Test]
        public void EverySetMember_IsFoundByTheCard_AndExists()
        {
            HashSet<string> catalog = new HashSet<string>();
            foreach (OutfitItem item in CharacterOutfitManager.BuildCatalog()) catalog.Add(item.itemId);

            foreach (OutfitSetDefinition set in OutfitSetCatalog.GetAllSets())
            {
                foreach (string id in set.requiredItemIds)
                {
                    Assert.IsTrue(catalog.Contains(id), $"{set.setId}: 구성품 '{id}'가 카탈로그에 없다");
                    Assert.IsNotNull(CharacterOutfitUI.SetOf(id), $"{id}: 세트 표시가 안 뜬다");
                }
            }
            Assert.IsNull(CharacterOutfitUI.SetOf("top_shirt"), "세트가 아닌 옷에 세트 표시가 뜬다");
            Assert.IsNull(CharacterOutfitUI.SetOf(null));
        }

        /// <summary>
        /// 2D 초상(상점 폴백·NPC 대사)의 입은 3D와 **같은 표정 모양**이어야 한다. 예전 2D는 폭이 표정 번호에 비례해
        /// 무표정이 가장 컸고(3D와 반대), 모양 차이는 없었다.
        /// </summary>
        [Test]
        public void MouthShapes_2DMatches3D()
        {
            for (int f = 0; f < 4; f++)
            {
                PlayerVisualBuilder.MouthShape(f, out PlayerVisualBuilder.MouthKind k3, out Vector2 size3, out _);
                CharacterPortraitRenderer.MouthShape2D(f, out CharacterPortraitRenderer.Mouth2D k2, out float w2, out _);
                Assert.AreEqual(k3.ToString(), k2.ToString(), $"표정 {f}: 모양이 다르다");
            }

            // 폭 순서: 활짝 ≥ 미소 > 차분 > 무표정 — 3D와 2D가 같은 순서
            float[] w3 = new float[4], w2d = new float[4];
            for (int f = 0; f < 4; f++)
            {
                PlayerVisualBuilder.MouthShape(f, out _, out Vector2 s, out _);
                CharacterPortraitRenderer.MouthShape2D(f, out _, out float w, out _);
                w3[f] = s.x; w2d[f] = w;
            }
            for (int a = 0; a < 4; a++)
                for (int b = 0; b < 4; b++)
                    if (w3[a] > w3[b] + 1e-4f) Assert.Greater(w2d[a], w2d[b], $"표정 {a}·{b}: 2D 입 크기 순서가 3D와 반대다");
        }

        /// <summary>데스크톱 미리보기 칸(560×730)은 예전 비율 그대로다 — 그림 514 · 상세 200.</summary>
        [Test]
        public void PreviewStage_Desktop_KeepsOldSplit()
        {
            float stage = CharacterOutfitUI.PreviewStageHeight(560f, 730f, false);
            Assert.AreEqual(514f, stage, 0.01f);
            Assert.AreEqual(200f, 730f - 16f - stage, 0.01f, "상세 높이가 바뀌었다");
        }

        /// <summary>
        /// 세로 화면의 긴 칸(460×1258)에서 그림은 텍스처 비율까지만 커지고 나머지는 상세가 받는다.
        /// 옛 레이아웃은 창이 820 고정이라 세로 화면(1920)의 가운데 43%만 썼다(2026-09-30 검수 캡처).
        /// </summary>
        [Test]
        public void PreviewStage_TallColumn_CapsPictureAtTextureAspect()
        {
            float w = 460f, h = 1258f;
            float stage = CharacterOutfitUI.PreviewStageHeight(w, h, true);
            Assert.AreEqual((w - 16f) * CharacterModelPreviewRenderer.PreviewHeightPerWidth, stage, 0.01f,
                "그림이 텍스처 비율보다 길면 위아래가 빈 여백이다");
            Assert.Greater(h - 16f - stage, 150f + 110f, "상세가 설명(110)까지 받을 높이를 못 얻었다");
        }

        [Test]
        public void PreviewStage_ShortColumn_KeepsDetailMinimum()
        {
            float stage = CharacterOutfitUI.PreviewStageHeight(460f, 400f, true);
            Assert.AreEqual(400f - 16f - 150f, stage, 0.01f);
            Assert.AreEqual(1f, CharacterOutfitUI.PreviewStageHeight(460f, 50f, true), "음수 높이");
        }

        /// <summary>
        /// 데스크톱 캐시샵의 오른쪽 콘텐츠(패널 − 캐릭터 칸 − 세로 스크롤바)에 상자 세 장이 들어가야 한다.
        /// 패널 1200이던 때 812뿐이라 골드 상자가 잘리고 가로 스크롤이 생겼다(2026-09-30 검수 캡처).
        /// </summary>
        [Test]
        public void CashShopDesktop_ThreeCardsFitWithoutSideScroll()
        {
            const float VerticalScrollbar = 20f;
            float content = CashShopUI.DesktopPanelWidth - CashShopUI.DesktopCharColumnWidth - VerticalScrollbar;
            Assert.GreaterOrEqual(content, CashShopUI.DesktopBoxTabMinWidth, "상자 세 장이 안 들어간다");
            Assert.GreaterOrEqual(content, CashShopUI.DesktopCardTabMinWidth, "아이템 카드 세 장이 안 들어간다");
            Assert.LessOrEqual(CashShopUI.DesktopPanelWidth, UIScale.ReferenceWidth - 48f, "패널이 데스크톱 화면보다 넓다");
        }

        /// <summary>하단 "장비 보너스" 줄은 모든 보너스를 적고, 장착이 같으면 문자열을 다시 만들지 않는다.</summary>
        [Test]
        public void EquippedSummary_ListsBonuses_AndIsCachedUntilChanged()
        {
            GameObject go = new GameObject("OutfitUITest");
            try
            {
                CharacterOutfitUI ui = go.AddComponent<CharacterOutfitUI>();
                OutfitStatBonus a = new OutfitStatBonus { captureChanceBonus = 0.02f, rareSpawnBonus = 0.05f };
                string first = ui.EquippedSummaryText(a);
                StringAssert.Contains("포획+2%", first);
                StringAssert.Contains("레어+5%", first);
                Assert.AreSame(first, ui.EquippedSummaryText(a), "같은 보너스인데 문자열을 다시 만들었다");

                OutfitStatBonus b = a;
                b.defBonus = 0.01f;
                string second = ui.EquippedSummaryText(b);
                StringAssert.Contains("DEF+1%", second, "보너스가 바뀌었는데 옛 문자열이 남았다");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// 넘치는 요약 줄은 항목 경계(공백)에서 나눈다 — IMGUI 줄바꿈은 한글 사이 아무 데서나 끊어
        /// "캔디 / +2%"처럼 항목을 갈랐다(검수 캡처).
        /// </summary>
        [Test]
        public void SplitAtMiddleSpace_BreaksBetweenEntries()
        {
            string text = "장비 보너스: 포획+2% ATK+2% DEF+2% 이속+5% 캔디+2%";
            string split = CharacterOutfitUI.SplitAtMiddleSpace(text);
            string[] lines = split.Split('\n');
            Assert.AreEqual(2, lines.Length);
            foreach (string entry in new[] { "포획+2%", "ATK+2%", "DEF+2%", "이속+5%", "캔디+2%" })
                Assert.IsTrue(lines[0].Contains(entry) || lines[1].Contains(entry), $"'{entry}'가 두 줄에 갈렸다");
            Assert.AreEqual(text, split.Replace('\n', ' '), "공백 하나만 줄바꿈으로 바뀌어야 한다");
            Assert.AreEqual("공백없음", CharacterOutfitUI.SplitAtMiddleSpace("공백없음"));
        }

        /// <summary>
        /// 재화·가격 라벨에 보조 평면 이모지(💎·🪙 등)를 쓰지 않는다 — 스탠드얼론·기기 기본 폰트에 없어 □로 깨졌다
        /// (2026-09-30 검수 빌드 캡처). 주석의 이모지는 괜찮다.
        /// </summary>
        [TestCase("Assets/Scripts/UI/CashShopUI.cs")]
        [TestCase("Assets/Scripts/UI/CharacterOutfitUI.cs")]
        public void ShopLabels_HaveNoSupplementaryEmoji(string relativePath)
        {
            string full = System.IO.Path.Combine(Application.dataPath, "..", relativePath);
            Assert.IsTrue(System.IO.File.Exists(full), $"소스를 못 찾음: {relativePath}");
            string[] lines = System.IO.File.ReadAllLines(full);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int comment = line.IndexOf("//", System.StringComparison.Ordinal);
                string code = comment >= 0 ? line.Substring(0, comment) : line;
                foreach (char c in code)
                    Assert.IsFalse(char.IsSurrogate(c), $"{relativePath}:{i + 1} 코드에 이모지가 있다 — □로 깨진다: {line.Trim()}");
            }
        }
    }
}
#endif
