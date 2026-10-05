#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using InsectGame.Data;
using InsectGame.UI;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 화면 간 표기·색 기준이 한 출처에서 나오는지 고정한다(2026-09-30 도감·필드·배틀팀 품질 맞춤).
    /// 배틀팀·보유 곤충·포획 창이 enum 이름("Rare")을 그대로 찍어 도감의 "희귀"와 섞여 나왔다.
    /// </summary>
    [TestFixture]
    public class UIParityTests
    {
        [Test]
        public void InsectRarityKorean_EveryRarity_HasDistinctHangulLabel()
        {
            var seen = new HashSet<string>();
            foreach (InsectRarity rarity in Enum.GetValues(typeof(InsectRarity)))
            {
                string label = rarity.Korean();
                Assert.IsFalse(string.IsNullOrEmpty(label), rarity.ToString());
                foreach (char c in label)
                    Assert.IsTrue(c >= '가' && c <= '힣', $"{rarity} → \"{label}\"에 한글 아닌 글자");
                Assert.IsTrue(seen.Add(label), $"{rarity} 표기 \"{label}\"가 다른 등급과 겹친다");
            }
        }

        [Test]
        public void InsectRarityKorean_MatchesDexVocabulary()
        {
            // 도감이 원래 쓰던 표 — 이 단어들이 게임 전체의 표기다.
            Assert.AreEqual("일반", InsectRarity.Common.Korean());
            Assert.AreEqual("고급", InsectRarity.Uncommon.Korean());
            Assert.AreEqual("희귀", InsectRarity.Rare.Korean());
            Assert.AreEqual("영웅", InsectRarity.Epic.Korean());
            Assert.AreEqual("전설", InsectRarity.Legendary.Korean());
        }

        [Test]
        public void GetHpColor_Thresholds_MintAmberCoral()
        {
            UITheme t = UITheme.Instance;
            Assert.AreEqual(t.accentMint, t.GetHpColor(1f));
            Assert.AreEqual(t.accentMint, t.GetHpColor(0.51f));
            Assert.AreEqual(t.accentAmber, t.GetHpColor(0.5f));
            Assert.AreEqual(t.accentAmber, t.GetHpColor(0.21f));
            Assert.AreEqual(t.accentCoral, t.GetHpColor(0.2f));
            Assert.AreEqual(t.accentCoral, t.GetHpColor(0f));
        }
    }
}
#endif
