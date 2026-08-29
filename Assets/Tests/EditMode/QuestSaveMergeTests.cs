#if UNITY_EDITOR
using InsectGame.Core;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 퀘스트 세이브의 로컬↔클라우드 병합. <b>회귀 고정이 목적이다</b> — 예전 부트 로드는
    /// 클라우드 값을 그대로 덮어써서, 낡거나 빈 클라우드 문서 하나가 깬 퀘스트를 통째로
    /// 미완료로 되돌렸다(로그인할 때마다 반복됐고 예외도 경고도 없었다).
    /// </summary>
    [TestFixture]
    public class QuestSaveMergeTests
    {
        // ── 완료 목록(합집합) ──

        [Test]
        public void UnionCsv_CloudEmpty_KeepsLocal()
        {
            Assert.AreEqual("q_move,q_talk_elder",
                QuestSaveMerge.UnionCsv("q_move,q_talk_elder", ""));
        }

        [Test]
        public void UnionCsv_CloudNull_KeepsLocal()
        {
            Assert.AreEqual("q_move", QuestSaveMerge.UnionCsv("q_move", null));
        }

        [Test]
        public void UnionCsv_LocalEmpty_TakesCloud()
        {
            Assert.AreEqual("q_move,q_battle", QuestSaveMerge.UnionCsv("", "q_move,q_battle"));
        }

        [Test]
        public void UnionCsv_BothEmpty_ReturnsEmpty()
        {
            Assert.AreEqual("", QuestSaveMerge.UnionCsv("", ""));
        }

        [Test]
        public void UnionCsv_Overlapping_DeduplicatesKeepingLocalOrder()
        {
            Assert.AreEqual("q_move,q_talk_elder,q_battle",
                QuestSaveMerge.UnionCsv("q_move,q_talk_elder", "q_move,q_battle"));
        }

        [Test]
        public void UnionCsv_LocalAheadOfCloud_DoesNotLoseProgress()
        {
            // 로컬이 앞선 상태에서 낡은 클라우드가 와도 뒤엣것이 살아남는다(이 결함의 본체).
            string merged = QuestSaveMerge.UnionCsv(
                "q_move,q_talk_elder,q_approach,q_equip,q_battle", "q_move,q_talk_elder");
            StringAssert.Contains("q_battle", merged);
            StringAssert.Contains("q_equip", merged);
        }

        [Test]
        public void UnionCsv_BlankEntries_AreDropped()
        {
            Assert.AreEqual("q_move,q_battle", QuestSaveMerge.UnionCsv("q_move,,q_battle", ",,"));
        }

        [Test]
        public void UnionCsv_WhitespacePadding_IsTrimmed()
        {
            Assert.AreEqual("q_move,q_battle", QuestSaveMerge.UnionCsv(" q_move , q_battle ", " q_move "));
        }

        // ── 진행 카운트·반복 횟수(키별 최댓값) ──

        [Test]
        public void MaxIntDict_CloudEmpty_KeepsLocal()
        {
            Assert.AreEqual("q_battle3:2", QuestSaveMerge.MaxIntDict("q_battle3:2", ""));
        }

        [Test]
        public void MaxIntDict_LocalEmpty_TakesCloud()
        {
            Assert.AreEqual("q_battle3:2", QuestSaveMerge.MaxIntDict("", "q_battle3:2"));
        }

        [Test]
        public void MaxIntDict_SameKey_TakesHigher()
        {
            Assert.AreEqual("q_battle3:5", QuestSaveMerge.MaxIntDict("q_battle3:5", "q_battle3:1"));
            Assert.AreEqual("q_battle3:5", QuestSaveMerge.MaxIntDict("q_battle3:1", "q_battle3:5"));
        }

        [Test]
        public void MaxIntDict_DisjointKeys_KeepsBoth()
        {
            Assert.AreEqual("q_move:1,q_capture3:2",
                QuestSaveMerge.MaxIntDict("q_move:1", "q_capture3:2"));
        }

        [Test]
        public void MaxIntDict_MalformedEntries_AreIgnored()
        {
            Assert.AreEqual("q_move:1", QuestSaveMerge.MaxIntDict("q_move:1,broken,x:notanint", ""));
        }

        [Test]
        public void MaxIntDict_RepeatableResetToZero_KeepsHigherSide()
        {
            // 반복 서브가 한쪽에서 방금 완료돼 0으로 리셋됐다면 진행 중인 쪽(3)이 남는다.
            // 의도한 방향 — 덜 남은 진행이 사라진 진행보다 낫다.
            Assert.AreEqual("side_capture:3", QuestSaveMerge.MaxIntDict("side_capture:0", "side_capture:3"));
        }

        // ── 활성 퀘스트 ──

        [Test]
        public void PreferCloudActive_CloudEmpty_KeepsLocal()
        {
            Assert.AreEqual("q_battle", QuestSaveMerge.PreferCloudActive("q_battle", ""));
        }

        [Test]
        public void PreferCloudActive_CloudNull_KeepsLocal()
        {
            Assert.AreEqual("q_battle", QuestSaveMerge.PreferCloudActive("q_battle", null));
        }

        [Test]
        public void PreferCloudActive_CloudPresent_TakesCloud()
        {
            Assert.AreEqual("q_item", QuestSaveMerge.PreferCloudActive("q_battle", "q_item"));
        }

        [Test]
        public void PreferCloudActive_BothEmpty_ReturnsEmpty()
        {
            Assert.AreEqual("", QuestSaveMerge.PreferCloudActive(null, null));
        }
    }
}
#endif
