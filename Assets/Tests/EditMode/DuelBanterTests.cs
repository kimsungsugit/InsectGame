#if UNITY_EDITOR
using InsectGame.NPC;
using InsectGame.UI;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 대결 연출 대사 — 순간 판정(<see cref="DuelBanter.Next"/>)과 표의 무결성.
    /// 그리기(BattleScreenUI.Duel)는 IMGUI라 QA 캡처 빌드(<c>-battleScenario boss|guardian</c>)로 본다.
    /// </summary>
    [TestFixture]
    public class DuelBanterTests
    {
        [Test]
        public void Next_EnemyCrossesHalfThenCrisis_EachOnce()
        {
            var t = new DuelBanter.Tracker();
            Assert.AreEqual(DuelBanter.Moment.None, DuelBanter.Next(ref t, 0.8f, 1f));
            Assert.AreEqual(DuelBanter.Moment.Half, DuelBanter.Next(ref t, 0.45f, 1f));
            Assert.AreEqual(DuelBanter.Moment.None, DuelBanter.Next(ref t, 0.40f, 1f));
            Assert.AreEqual(DuelBanter.Moment.Crisis, DuelBanter.Next(ref t, 0.15f, 1f));
            Assert.AreEqual(DuelBanter.Moment.None, DuelBanter.Next(ref t, 0.05f, 1f));
        }

        [Test]
        public void Next_OneHitThroughBothLines_SaysCrisisOnly()
        {
            var t = new DuelBanter.Tracker();
            Assert.AreEqual(DuelBanter.Moment.Crisis, DuelBanter.Next(ref t, 0.1f, 1f));
            Assert.AreEqual(DuelBanter.Moment.None, DuelBanter.Next(ref t, 0.1f, 1f), "절반 대사가 뒤늦게 나오면 안 된다");
        }

        [Test]
        public void Next_KnockedOut_SaysNothing()
        {
            // 쓰러진 자리는 결과 한마디가 맡는다 — 여기서 "이럴 리가"가 나오면 결과 대사와 겹친다.
            var t = new DuelBanter.Tracker();
            Assert.AreEqual(DuelBanter.Moment.None, DuelBanter.Next(ref t, 0f, 1f));
        }

        [Test]
        public void Next_PlayerLow_PressingOnce_AfterEnemyMoment()
        {
            var t = new DuelBanter.Tracker();
            // 같은 교환에서 둘 다 넘으면 상대 쪽이 먼저, 내 쪽은 다음 호출에서.
            Assert.AreEqual(DuelBanter.Moment.Half, DuelBanter.Next(ref t, 0.4f, 0.2f));
            Assert.AreEqual(DuelBanter.Moment.Pressing, DuelBanter.Next(ref t, 0.4f, 0.2f));
            Assert.AreEqual(DuelBanter.Moment.None, DuelBanter.Next(ref t, 0.4f, 0.1f));
        }

        [Test]
        public void Next_PlayerFainted_NoPressing()
        {
            var t = new DuelBanter.Tracker();
            Assert.AreEqual(DuelBanter.Moment.None, DuelBanter.Next(ref t, 1f, 0f));
        }

        [Test]
        public void LineFor_MapsEachMoment()
        {
            Assert.IsTrue(DuelBanter.TryGet("ledger_grip", out DuelBanter.Lines l));
            Assert.AreEqual(l.half, DuelBanter.LineFor(l, DuelBanter.Moment.Half));
            Assert.AreEqual(l.crisis, DuelBanter.LineFor(l, DuelBanter.Moment.Crisis));
            Assert.AreEqual(l.pressing, DuelBanter.LineFor(l, DuelBanter.Moment.Pressing));
            Assert.IsNull(DuelBanter.LineFor(l, DuelBanter.Moment.None));
        }

        [Test]
        public void EveryBossDuel_HasCompleteBanter()
        {
            // 보스 표에 사람을 늘리고 대사를 안 쓰면 그 인물만 말없이 싸운다(예외도 경고도 없다).
            foreach (NpcBossDuels.BossDuel d in NpcBossDuels.All())
            {
                Assert.IsTrue(DuelBanter.TryGet(d.storyNpcId, out DuelBanter.Lines l), $"{d.storyNpcId}: 대결 대사 없음");
                Assert.AreEqual(d.storyNpcId, l.npcId, $"{d.storyNpcId}: 초상 인물이 다르다");
                Assert.AreEqual(d.displayName, l.name, $"{d.storyNpcId}: 컷인 이름이 보스 표와 다르다");
            }
        }

        [Test]
        public void EveryBanter_AllLinesPresent_NameMatchesDialogue_HasPortrait()
        {
            foreach (string id in DuelBanter.Ids)
            {
                DuelBanter.TryGet(id, out DuelBanter.Lines l);
                foreach (string line in new[] { l.title, l.intro, l.half, l.crisis, l.pressing, l.defeat, l.victory })
                {
                    Assert.IsFalse(string.IsNullOrEmpty(line), $"{id}: 빈 대사");
                    // 말풍선·컷인 판은 한두 줄 크기다 — 넘치면 LabelFit이 글자를 읽기 어렵게 줄인다.
                    Assert.LessOrEqual(line.Length, 40, $"{id}: 너무 긴 대사 — {line}");
                    Assert.IsFalse(line.Contains("무명"), $"{id}: 「무명」을 부르면 안 된다");
                }
                Assert.AreEqual(NpcDialogueDatabase.StorySpeakerName(l.npcId), l.name,
                    $"{id}: 대사창 이름표와 컷인 이름이 다르다");
                Assert.IsTrue(NpcDialogueUI.HasStoryPortrait(l.npcId), $"{id}: 초상 없음 — 컷인에 얼굴이 빈다");
            }
        }

        [Test]
        public void ActOneThugs_DoNotNameTheOrganization()
        {
            // 1막 하수는 정체가 밝혀지기 전이다(NpcDialogueDatabase.StorySpeakerName 주석) — 칭호로도 새면 안 된다.
            foreach (string id in new[] { "ledger_thug_pin", "ledger_thug_rule", "ledger_thug_cord" })
            {
                Assert.IsTrue(DuelBanter.TryGet(id, out DuelBanter.Lines l), id);
                foreach (string line in new[] { l.title, l.intro, l.half, l.crisis, l.pressing, l.defeat, l.victory })
                    Assert.IsFalse(line.Contains("명부회"), $"{id}: {line}");
            }
        }
    }
}
#endif
