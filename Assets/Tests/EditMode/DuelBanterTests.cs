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
        public void TeamBosses_HaveSendOutLines()
        {
            // 간부는 여럿을 데리고 싸운다(집게·저울 셋, 하월 다섯) — 교체 순간 한마디가 없으면 말없이 다음 곤충이 나온다.
            var expectedAtLeast = new System.Collections.Generic.Dictionary<string, int>
            {
                ["ledger_grip"] = 2, ["ledger_scale"] = 2, ["ledger_chief"] = 4,
            };
            foreach (var pair in expectedAtLeast)
            {
                Assert.IsTrue(DuelBanter.TryGet(pair.Key, out DuelBanter.Lines l), pair.Key);
                Assert.IsNotNull(l.sendOut, $"{pair.Key}: 교체 한마디 없음");
                Assert.GreaterOrEqual(l.sendOut.Length, pair.Value, pair.Key);
                foreach (string line in l.sendOut)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(line), $"{pair.Key}: 빈 교체 한마디");
                    Assert.LessOrEqual(line.Length, 40, $"{pair.Key}: 너무 긴 교체 한마디 — {line}");
                    Assert.IsFalse(line.Contains("무명"), pair.Key);
                }
            }
        }

        [Test]
        public void SendOutLine_FirstInsectSilent_ThenInOrder_ThenRepeatsLast()
        {
            Assert.IsTrue(DuelBanter.TryGet("ledger_grip", out DuelBanter.Lines grip));
            Assert.IsNull(DuelBanter.SendOutLine(grip, 0), "첫 곤충의 말은 컷인 도발이 맡는다");
            Assert.AreEqual(grip.sendOut[0], DuelBanter.SendOutLine(grip, 1));
            Assert.AreEqual(grip.sendOut[1], DuelBanter.SendOutLine(grip, 2));
            Assert.AreEqual(grip.sendOut[grip.sendOut.Length - 1], DuelBanter.SendOutLine(grip, 9),
                "팀이 준비한 줄보다 길면 마지막 줄을 되풀이한다");

            Assert.IsTrue(DuelBanter.TryGet("rival_meadow", out DuelBanter.Lines raon));
            Assert.IsNull(DuelBanter.SendOutLine(raon, 1), "한 마리만 내는 상대는 교체 한마디가 없다");
        }

        [TestCase(1, 0, true)]     // 한 마리 대결 — 늘 말한다
        [TestCase(0, 0, true)]
        [TestCase(3, 0, false)]    // 집게·저울의 첫 곤충
        [TestCase(3, 1, false)]
        [TestCase(3, 2, true)]     // 에이스
        [TestCase(5, 3, false)]
        [TestCase(5, 4, true)]     // 하월의 이름 잃은 나방
        public void EnemyMomentsAllowed_OnlyTheAceInTeamDuels(int size, int index, bool expected)
        {
            Assert.AreEqual(expected, DuelBanter.EnemyMomentsAllowed(size, index));
        }

        [Test]
        public void Next_TeamDuel_FirstInsectsStayQuiet_AceStartsFresh()
        {
            var t = new DuelBanter.Tracker();
            // 첫 곤충이 위기까지 떨어져도 상대는 무너지는 소리를 하지 않는다 — 아직 둘이 남았다.
            Assert.AreEqual(DuelBanter.Moment.None, DuelBanter.Next(ref t, 0.1f, 1f, 3, 0));
            // 내 곤충이 몰리는 순간은 팀과 무관하다.
            Assert.AreEqual(DuelBanter.Moment.Pressing, DuelBanter.Next(ref t, 0.1f, 0.2f, 3, 1));
            // 에이스가 나오면 처음부터 센다 — 절반, 그다음 위기.
            Assert.AreEqual(DuelBanter.Moment.None, DuelBanter.Next(ref t, 1f, 0.2f, 3, 2));
            Assert.AreEqual(DuelBanter.Moment.Half, DuelBanter.Next(ref t, 0.45f, 0.2f, 3, 2));
            Assert.AreEqual(DuelBanter.Moment.Crisis, DuelBanter.Next(ref t, 0.15f, 0.2f, 3, 2));
        }

        [Test]
        public void TeamBosses_SendOutLinesCoverTheirTeams()
        {
            // 교체 한마디가 팀의 교체 횟수만큼 있어야 말이 되풀이되지 않는다(모자라면 마지막 줄을 되풀이한다).
            foreach (NpcBossDuels.BossDuel d in NpcBossDuels.All())
            {
                if (d.teamInsectIds == null || d.teamInsectIds.Length <= 1) continue;
                Assert.IsTrue(DuelBanter.TryGet(d.storyNpcId, out DuelBanter.Lines l), d.storyNpcId);
                Assert.IsNotNull(l.sendOut, $"{d.storyNpcId}: 팀 대결인데 교체 한마디가 없다");
                Assert.GreaterOrEqual(l.sendOut.Length, d.teamInsectIds.Length - 1,
                    $"{d.storyNpcId}: 곤충 {d.teamInsectIds.Length}마리 — 교체 {d.teamInsectIds.Length - 1}번");
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
