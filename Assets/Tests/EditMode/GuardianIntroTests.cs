#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 수문장 등장 화면의 글(<see cref="GuardianIntros"/>) — 수문장이 있는 리전을 빠짐없이 덮는지, 아이가 읽을 길이인지.
    /// 빠진 리전은 예외 없이 별칭·등장 줄만 비어서 뜬다(그 수문장만 밋밋하다). 그리기는 ui-dev(레이드 시작 화면).
    /// </summary>
    [TestFixture]
    public class GuardianIntroTests
    {
        private static readonly string[] RetiredWords = { "무명", "봉인", "지워진 개체", "예비 울타리" };

        [Test]
        public void EveryGuardianRegion_HasAnIntro_AndNoStrays()
        {
            var guardianRegions = new HashSet<string>();
            foreach (RegionData r in RegionDefinitions.CreateAll())
                if (r != null && !string.IsNullOrEmpty(r.guardianInsectId)) guardianRegions.Add(r.regionId);
            Assume.That(guardianRegions.Count, Is.GreaterThan(0), "리전 정의를 못 읽었다");

            var covered = new HashSet<string>();
            foreach (GuardianIntros.Intro intro in GuardianIntros.All())
            {
                Assert.IsTrue(covered.Add(intro.regionId), $"{intro.regionId}: 두 번 적혔다");
                Assert.IsTrue(guardianRegions.Contains(intro.regionId), $"{intro.regionId}: 수문장이 없는 리전이다(오타?)");
            }
            CollectionAssert.AreEquivalent(guardianRegions, covered, "수문장 리전과 등장 글이 1:1이어야 한다");
        }

        [Test]
        public void Intros_AreShortAndPlain()
        {
            foreach (GuardianIntros.Intro intro in GuardianIntros.All())
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(intro.epithet), $"{intro.regionId}: 별칭 없음");
                Assert.IsFalse(string.IsNullOrWhiteSpace(intro.line), $"{intro.regionId}: 등장 줄 없음");
                Assert.LessOrEqual(intro.epithet.Length, GuardianIntros.MaxEpithetChars, $"{intro.regionId}: {intro.epithet}");
                Assert.LessOrEqual(intro.line.Length, GuardianIntros.MaxLineChars, $"{intro.regionId}: {intro.line}");
                foreach (string word in RetiredWords)
                {
                    StringAssert.DoesNotContain(word, intro.epithet, intro.regionId);
                    StringAssert.DoesNotContain(word, intro.line, intro.regionId);
                }
                // 「그림자」는 울타리 밖의 그것에만 쓴다(StoryBible 2장) — 수문장 묘사에 섞으면 아이가 적과 헷갈린다.
                StringAssert.DoesNotContain("그림자", intro.epithet + intro.line, intro.regionId);
            }
        }

        [Test]
        public void TryGet_KnownAndUnknown()
        {
            Assert.IsTrue(GuardianIntros.TryGet("meadow", out GuardianIntros.Intro meadow));
            Assert.AreEqual("meadow", meadow.regionId);
            Assert.IsFalse(GuardianIntros.TryGet("nowhere", out _));
            Assert.IsFalse(GuardianIntros.TryGet(null, out _));
        }
    }
}
#endif
