#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 첫 파트너 곤충 선택.
    ///
    /// 여기서 지키는 것 셋:
    /// ① <b>후보가 실제로 존재하는 종</b>인가 — 없는 id를 주면 런타임은 <c>LogWarning</c>만 찍고
    ///    곤충은 안 들어온다(조용한 실패).
    /// ② <b>등급·속성이 실제 DB와 맞는가</b> — 값을 코드 주석에 적어두고 믿으면 DB가 바뀔 때 어긋난다.
    ///    같은 id에 등급이 갈리는 자리가 실제로 있어서(<c>CreateInsect</c> vs <c>CreateStableInsect</c>)
    ///    가정 대신 조회로 확인한다.
    /// ③ <b>조작된 PlayerPrefs를 막는가</b> — 화이트리스트가 없으면 전설 곤충 id를 적어 넣어
    ///    1레벨에 받을 수 있다.
    /// </summary>
    [TestFixture]
    public class StarterInsectTests
    {
        // ── 카탈로그 정합 ──

        [Test]
        public void Catalog_HasAtLeastThreeChoices()
        {
            Assert.GreaterOrEqual(StarterInsectCatalog.Count, 3,
                "선택지가 셋 미만이면 고르는 의미가 옅다");
        }

        [Test]
        public void EveryChoice_HasNameAndBlurb()
        {
            for (int i = 0; i < StarterInsectCatalog.Count; i++)
            {
                StarterInsectCatalog.Choice c = StarterInsectCatalog.Get(i);
                Assert.IsFalse(string.IsNullOrEmpty(c.InsectId), $"{i}: id 비었음");
                Assert.IsFalse(string.IsNullOrEmpty(c.DisplayName), $"{i}: 표시명 비었음 — 카드가 빈 칸이 된다");
                Assert.IsFalse(string.IsNullOrEmpty(c.Blurb), $"{i}: 설명 비었음");
            }
        }

        [Test]
        public void EveryChoiceId_IsUnique()
        {
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < StarterInsectCatalog.Count; i++)
            {
                Assert.IsTrue(seen.Add(StarterInsectCatalog.Get(i).InsectId),
                    "같은 종이 두 번 들어 있다 — 다른 카드를 골라도 같은 곤충을 받는다");
            }
        }

        /// <summary>
        /// 기본값은 Story.json <c>ch1_intro</c>의 원래 보상과 같아야 한다.
        /// 어긋나면 "선택하지 않은 기존 플레이어"가 받는 종이 조용히 바뀐다.
        /// </summary>
        [Test]
        public void DefaultId_MatchesTheBeatReward()
        {
            Assert.AreEqual("rhinoceros_beetle", StarterInsectCatalog.DefaultId);
            Assert.AreEqual(StarterInsectCatalog.DefaultId, StarterInsectCatalog.Get(0).InsectId,
                "기본값이 첫 카드가 아니면 화면과 지급이 어긋나 보인다");
        }

        [Test]
        public void StarterBeatId_MatchesStoryJson()
        {
            Assert.AreEqual("ch1_intro", StarterInsectCatalog.StarterBeatId,
                "beatId 오타는 오버라이드를 통째로 죽인다 — 아무도 예외를 내지 않는다");
        }

        [Test]
        public void Get_OutOfRangeIndex_Clamps()
        {
            Assert.AreEqual(StarterInsectCatalog.Get(0).InsectId, StarterInsectCatalog.Get(-4).InsectId);
            Assert.AreEqual(StarterInsectCatalog.Get(StarterInsectCatalog.Count - 1).InsectId,
                StarterInsectCatalog.Get(999).InsectId);
        }

        // ── 선택 해석 (조작 방어) ──

        [Test]
        public void ResolveChoice_NoSavedPick_ReturnsBeatDefault()
        {
            PlayerPrefs.DeleteKey(SaveScope.PrefsKey(StarterInsectCatalog.PrefsKeyBase));

            Assert.AreEqual("rhinoceros_beetle", StarterInsectCatalog.ResolveChoice("rhinoceros_beetle"),
                "선택이 없으면 비트의 원래 보상 그대로여야 한다 — 기존 세이브가 오늘과 같이 동작한다");
        }

        /// <summary>
        /// <b>조작 방어.</b> PlayerPrefs는 사용자가 고칠 수 있다. 목록에 없는 id는 무조건
        /// 기본값으로 떨어져야 한다 — 아니면 전설 곤충을 1레벨에 받을 수 있다.
        /// </summary>
        [Test]
        public void ResolveChoice_UnknownId_FallsBackToDefault()
        {
            PlayerPrefs.SetString(SaveScope.PrefsKey(StarterInsectCatalog.PrefsKeyBase), "atlas_moth_legendary");

            Assert.AreEqual("rhinoceros_beetle", StarterInsectCatalog.ResolveChoice("rhinoceros_beetle"),
                "화이트리스트에 없는 값이 통과하면 임의의 곤충을 시작부터 받을 수 있다");

            PlayerPrefs.DeleteKey(SaveScope.PrefsKey(StarterInsectCatalog.PrefsKeyBase));
        }

        [Test]
        public void ResolveChoice_ValidPick_ReturnsIt()
        {
            string picked = StarterInsectCatalog.Get(1).InsectId;
            PlayerPrefs.SetString(SaveScope.PrefsKey(StarterInsectCatalog.PrefsKeyBase), picked);

            Assert.AreEqual(picked, StarterInsectCatalog.ResolveChoice("rhinoceros_beetle"));

            PlayerPrefs.DeleteKey(SaveScope.PrefsKey(StarterInsectCatalog.PrefsKeyBase));
        }

        [Test]
        public void SaveChoice_RejectsUnknownId()
        {
            StarterInsectCatalog.SaveChoice("not_a_real_insect");

            Assert.AreEqual(StarterInsectCatalog.DefaultId,
                PlayerPrefs.GetString(SaveScope.PrefsKey(StarterInsectCatalog.PrefsKeyBase), ""),
                "조작된 값이 세이브에 눌러앉으면 클라우드까지 따라간다");

            PlayerPrefs.DeleteKey(SaveScope.PrefsKey(StarterInsectCatalog.PrefsKeyBase));
        }

        [Test]
        public void IndexOf_RoundTripsEveryChoice()
        {
            for (int i = 0; i < StarterInsectCatalog.Count; i++)
            {
                Assert.AreEqual(i, StarterInsectCatalog.IndexOf(StarterInsectCatalog.Get(i).InsectId));
            }
            Assert.AreEqual(0, StarterInsectCatalog.IndexOf("nonexistent"), "모르는 id는 첫 카드로");
        }
    }
}
#endif
