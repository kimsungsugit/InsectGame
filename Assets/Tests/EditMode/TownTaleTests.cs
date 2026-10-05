#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using InsectGame.Core;
using InsectGame.NPC;
using InsectGame.Story;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 마을 이야기(Story.json <c>town</c> 챕터) + 지역 의뢰(<c>s_town_*</c>) + 따라가기 판정.
    ///
    /// 셋 다 실패가 조용하다 — 의뢰 리전이 틀리면 진행이 안 세어지고, 매듭 비트의 퀘스트 게이트가
    /// 반복 퀘스트를 물면 영영 안 열리고, 따라가기 판정이 발화 게이트와 어긋나면 <c>!</c>를 따라가
    /// 말을 걸어도 아무 일도 없다. 순수부는 합성 비트로, 저작은 실제 데이터로 고정한다.
    /// </summary>
    [TestFixture]
    public class TownTaleTests
    {
        private const string Npc = "town_qa";

        private static StoryBeat Talk(string id, int order, string prereq = null,
            string gate = null, string quest = null, string npc = Npc, string chapter = "town")
        {
            return new StoryBeat
            {
                beatId = id,
                chapterId = chapter,
                order = order,
                prerequisiteBeatId = prereq,
                requiredBeatId = gate,
                requiredQuestId = quest,
                trigger = new StoryTrigger { type = StoryDirector.TriggerNpcTalk, param = npc }
            };
        }

        // 만남(도착 게이트) → 징후(포획, 따라가지 않음) → 매듭(의뢰 게이트) → 후일담(본편 게이트)
        private static List<StoryBeat> Tale()
        {
            return new List<StoryBeat>
            {
                Talk("qa_meet", 1, gate: "qa_arrive"),
                new StoryBeat
                {
                    beatId = "qa_sign", chapterId = "town", order = 2, prerequisiteBeatId = "qa_meet",
                    requiredRegionId = "pond",
                    trigger = new StoryTrigger { type = StoryDirector.TriggerCaptureInsect, param = "water_strider_pond" }
                },
                Talk("qa_close", 3, prereq: "qa_meet", quest: "s_qa"),
                Talk("qa_after", 4, prereq: "qa_close", gate: "qa_later"),
            };
        }

        private static System.Func<string, bool> Set(params string[] ids)
        {
            var set = new HashSet<string>(ids);
            return id => set.Contains(id);
        }

        private static TaleStepKind Step(System.Func<string, bool> seen, System.Func<string, bool> questDone,
            out StoryBeat beat, out string questId)
        {
            return StoryTaleResolver.ResolveStep(Tale(), Npc, seen, questDone, out beat, out questId);
        }

        // ── QuestRegionGate ──

        [Test]
        public void QuestRegionGate_NoRegion_CountsEverywhere()
        {
            Assert.IsTrue(QuestRegionGate.Counts(null, "pond"));
            Assert.IsTrue(QuestRegionGate.Counts(string.Empty, null));
        }

        [Test]
        public void QuestRegionGate_Region_CountsOnlyInsideThatRegion()
        {
            Assert.IsTrue(QuestRegionGate.Counts("pond", "pond"));
            Assert.IsFalse(QuestRegionGate.Counts("pond", "meadow"));
            // 리전 사이 빈 땅(CurrentRegion null)에서 한 행동은 세지 않는다.
            Assert.IsFalse(QuestRegionGate.Counts("pond", null));
        }

        [Test]
        public void QuestRegionGate_IsOpen_FollowsAccessProbe_AndClosedWithoutOne()
        {
            Assert.IsTrue(QuestRegionGate.IsOpen(null, null), "리전 한정이 없으면 늘 열려 있다");
            Assert.IsFalse(QuestRegionGate.IsOpen("pond", null), "판정기가 없으면 닫힌 쪽으로 둔다");
            Assert.IsTrue(QuestRegionGate.IsOpen("pond", id => id == "pond"));
            Assert.IsFalse(QuestRegionGate.IsOpen("ruins", id => id == "pond"));
        }

        // ── 따라가기 단계 판정 ──

        [Test]
        public void ResolveStep_BeforeRegionArrival_IsWaiting()
        {
            Assert.AreEqual(TaleStepKind.Waiting, Step(Set(), Set(), out _, out _));
        }

        [Test]
        public void ResolveStep_AfterArrival_IsTalkToMeet()
        {
            Assert.AreEqual(TaleStepKind.Talk, Step(Set("qa_arrive"), Set(), out StoryBeat beat, out _));
            Assert.AreEqual("qa_meet", beat.beatId);
        }

        [Test]
        public void ResolveStep_MetButErrandUnfinished_IsErrandWithQuest_IgnoringCaptureFlavor()
        {
            // 포획 징후 비트(qa_sign)가 미열람이어도 할 일은 의뢰다 — 징후는 곁들임이라 따라가지 않는다.
            TaleStepKind step = Step(Set("qa_arrive", "qa_meet"), Set(), out StoryBeat beat, out string quest);
            Assert.AreEqual(TaleStepKind.Errand, step);
            Assert.AreEqual("qa_close", beat.beatId);
            Assert.AreEqual("s_qa", quest);
        }

        [Test]
        public void ResolveStep_ErrandDone_IsReport()
        {
            TaleStepKind step = Step(Set("qa_arrive", "qa_meet"), Set("s_qa"), out StoryBeat beat, out _);
            Assert.AreEqual(TaleStepKind.Report, step);
            Assert.AreEqual("qa_close", beat.beatId);
        }

        [Test]
        public void ResolveStep_ClosedButMainStoryBehind_IsWaiting_ThenTalkWhenItCatchesUp()
        {
            Assert.AreEqual(TaleStepKind.Waiting,
                Step(Set("qa_arrive", "qa_meet", "qa_close"), Set("s_qa"), out _, out _));
            Assert.AreEqual(TaleStepKind.Talk,
                Step(Set("qa_arrive", "qa_meet", "qa_close", "qa_later"), Set("s_qa"), out StoryBeat beat, out _));
            Assert.AreEqual("qa_after", beat.beatId);
        }

        [Test]
        public void ResolveStep_AllTalksSeen_IsDone_AndUnknownNpcIsNone()
        {
            var all = Set("qa_arrive", "qa_meet", "qa_close", "qa_later", "qa_after");
            Assert.AreEqual(TaleStepKind.Done, Step(all, Set("s_qa"), out _, out _));
            Assert.AreEqual(TaleStepKind.None,
                StoryTaleResolver.ResolveStep(Tale(), "nobody", all, Set(), out _, out _));
        }

        [Test]
        public void CollectTaleNpcIds_OnlyTownChapter_InAuthoringOrder()
        {
            var beats = new List<StoryBeat>
            {
                Talk("b_meet", 20, npc: "town_b"),
                Talk("a_meet", 10, npc: "town_a"),
                Talk("a_close", 11, npc: "town_a"),
                Talk("echo", 1, npc: "ruins_scholar", chapter: "ch3"),   // 동행자 여운 — 표식 대상 아님
            };
            CollectionAssert.AreEqual(new[] { "town_a", "town_b" }, StoryTaleResolver.CollectTaleNpcIds(beats));
        }

        [Test]
        public void FindNpcForQuest_MapsErrandToItsResident()
        {
            Assert.AreEqual(Npc, StoryTaleResolver.FindNpcForQuest(Tale(), "s_qa"));
            Assert.IsNull(StoryTaleResolver.FindNpcForQuest(Tale(), "q_move"));
        }

        [Test]
        public void FindNpcForQuest_MainStoryQuestGate_IsNotAnErrand()
        {
            // 본편도 퀘스트 게이트를 쓴다(ch1_intro ← q_move). 거르지 않으면 튜토리얼 행에 [따라가기]가 뜬다.
            var beats = Tale();
            beats.Add(Talk("ch1_intro", 1, quest: "q_move", npc: "village_elder", chapter: "ch1"));
            Assert.IsNull(StoryTaleResolver.FindNpcForQuest(beats, "q_move"));
            Assert.IsNull(StoryTaleResolver.FindNpcForQuest(StoryService.AllBeats(), "q_move"),
                "실제 저작에서 튜토리얼 퀘스트가 마을 의뢰로 잡힌다");
        }

        [Test]
        public void DescribeErrand_NamesRegionOnlyFromOutside_AndClampsProgress()
        {
            Assert.AreEqual("연못에서 곤충 포획 3/5",
                StoryTaleResolver.DescribeErrand(QuestType.Capture, "연못", false, 3, 5));
            Assert.AreEqual("전투 승리 1/3",
                StoryTaleResolver.DescribeErrand(QuestType.Battle, "숲", true, 1, 3));
            Assert.AreEqual("희귀 곤충 포획 3/3",
                StoryTaleResolver.DescribeErrand(QuestType.CaptureRare, "고대 유적", true, 9, 3));
        }

        // ── 저작 데이터 ──

        private static TutorialQuest[] LoadQuests()
        {
            GameObject host = new GameObject("TownTaleQuestFixture");
            host.SetActive(false);   // Awake(싱글턴 등록)·Start(세이브 로드)를 돌리지 않는다
            try
            {
                var mgr = host.AddComponent<TutorialQuestManager>();
                typeof(TutorialQuestManager).GetMethod("Initialize", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(mgr, null);
                return mgr.GetAllQuests();
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void EveryTownTale_ReportWaitsOnOneShotRegionErrand_InTheSameRegionAsItsSign()
        {
            var beats = new List<StoryBeat>(StoryService.AllBeats());
            List<string> residents = StoryTaleResolver.CollectTaleNpcIds(beats);
            Assert.GreaterOrEqual(residents.Count, 12, "1막 일곱 + 2막 다섯 마을");

            var quests = new Dictionary<string, TutorialQuest>();
            foreach (TutorialQuest q in LoadQuests()) quests[q.questId] = q;

            foreach (string npc in residents)
            {
                StoryBeat meet = null, report = null;
                foreach (StoryBeat b in beats)
                {
                    if (b.chapterId != StoryTaleResolver.TownChapterId || b.trigger == null) continue;
                    if (b.trigger.type != StoryDirector.TriggerNpcTalk || b.trigger.param != npc) continue;
                    // 표식 판정(ResolveStep)은 리전 게이트를 보지 않는다 — 대화 비트에 리전을 달면
                    // 리전 밖에서 !가 떠 있는데 말을 걸어도 아무 일이 없다.
                    Assert.IsTrue(string.IsNullOrEmpty(b.requiredRegionId),
                        $"{b.beatId}: 마을 이야기 대화 비트에 requiredRegionId — 표식과 발화가 갈린다");
                    if (!string.IsNullOrEmpty(b.requiredQuestId)) { Assert.IsNull(report, $"{npc}: 의뢰 보고가 둘"); report = b; }
                    else if (string.IsNullOrEmpty(b.prerequisiteBeatId)) meet = b;
                }
                Assert.IsNotNull(meet, $"{npc}: 만남 비트가 없다");
                Assert.IsFalse(string.IsNullOrEmpty(meet.requiredBeatId),
                    $"{npc}: 만남이 도착 게이트 없이 열린다 — 캠페인 시작부터 !가 뜬다");
                Assert.IsNotNull(report, $"{npc}: 의뢰를 기다리는 매듭 비트가 없다");

                Assert.IsTrue(quests.TryGetValue(report.requiredQuestId, out TutorialQuest q),
                    $"{npc}: 매듭이 무는 {report.requiredQuestId}가 퀘스트 배열에 없다");
                Assert.AreEqual(QuestCategory.Side, q.category, $"{q.questId}: 서브 퀘스트여야 한다");
                Assert.IsFalse(q.repeatable, $"{q.questId}: 반복 퀘스트는 완료 기록이 안 남아 매듭이 영영 안 열린다");
                Assert.IsFalse(string.IsNullOrEmpty(q.requiredRegionId), $"{q.questId}: 지역 의뢰여야 한다");

                // 징후(포획) 비트의 리전 = 의뢰 리전 — 주민이 선 마을과 일을 시키는 곳이 같아야 한다.
                foreach (StoryBeat b in beats)
                {
                    if (b.prerequisiteBeatId != meet.beatId || b.trigger == null
                        || b.trigger.type != StoryDirector.TriggerCaptureInsect) continue;
                    Assert.AreEqual(q.requiredRegionId, b.requiredRegionId, $"{b.beatId}: 의뢰와 다른 리전");
                }
            }
        }

        [Test]
        public void EveryTownResident_HasOwnNamePortraitLinesAndAppearance()
        {
            NpcAppearance elder = NpcVisualBuilder.StoryNpcAppearance("village_elder");
            foreach (string npc in StoryTaleResolver.CollectTaleNpcIds(StoryService.AllBeats()))
            {
                Assert.AreNotEqual(npc, NpcDialogueDatabase.StorySpeakerName(npc), $"{npc}: 표시명 미등록(ID가 그대로 뜬다)");
                // 초상은 검사기가 안 본다 — 빠지면 대사창에 얼굴 없이 이름표만 뜬다(2막 간부가 그랬다).
                Assert.IsTrue(NpcDialogueUI.HasStoryPortrait(npc), $"{npc}: 대사창 초상 미등록");
                Assert.IsTrue(NpcDialogueDatabase.TryGetStoryNpcLines(npc, out string[] lines) && lines.Length > 0,
                    $"{npc}: 전용 잡담 없음");
                NpcAppearance a = NpcVisualBuilder.StoryNpcAppearance(npc);
                Assert.IsFalse(a.top == elder.top && a.hat == elder.hat && a.hairStyle == elder.hairStyle,
                    $"{npc}: 외형 switch 미등록(마을 어르신 얼굴로 선다)");
            }
        }

        [Test]
        public void TownSignBeats_NeverSpokenByRaon()
        {
            // 징후는 포획 트리거라 만남 뒤 **언제든** 뜬다 — 라온 이탈 구간(ch10 대치 ~ ch12 복귀)에도.
            // 그 사이 라온의 말을 두지 않는다(StoryBible 4장·13장). 1막 초원·연못은 세라를 만나기 전일 수
            // 있어 지문이, 그 뒤 마을은 세라가 맡는다.
            foreach (StoryBeat b in StoryService.AllBeats())
            {
                if (b.chapterId != StoryTaleResolver.TownChapterId || b.trigger == null
                    || b.trigger.type != StoryDirector.TriggerCaptureInsect) continue;
                Assert.AreNotEqual("catcher_rival", b.speakerNpcId, $"{b.beatId}: 라온이 화자");
                if (b.lines == null) continue;
                foreach (StoryLine line in b.lines)
                    Assert.AreNotEqual("라온", line.speaker, $"{b.beatId}: 라온의 대사 — 이탈 구간에 뜰 수 있다");
            }
        }

        [Test]
        public void EveryStoryPortrait_WearsSameTorsoAndHatAsWorld()
        {
            // 초상 표와 월드 외형 표가 따로 있다 — 물결 할머니 밀짚모자가 초상에선 주황, 잎새의 두건은 초상에 아예
            // 없었고, 명부회 일곱은 전원이 달랐다(집게의 붉은 모자가 대사창에선 맨머리). 마을 주민만 보던 이 검사를
            // 초상이 있는 스토리 인물 전원으로 넓힌다. 몸통은 월드에서 **실제로 보이는** 색 — 셔츠 또는 겉옷.
            var ids = new HashSet<string>(StoryTaleResolver.CollectTaleNpcIds(StoryService.AllBeats()));
            foreach (StoryBeat b in StoryService.AllBeats())
            {
                if (!string.IsNullOrEmpty(b.speakerNpcId)) ids.Add(b.speakerNpcId);
                if (b.trigger != null && b.trigger.type == StoryDirector.TriggerNpcTalk && !string.IsNullOrEmpty(b.trigger.param))
                    ids.Add(b.trigger.param);
            }
            int checkedCount = 0;
            foreach (string npc in ids)
            {
                if (!NpcDialogueUI.TryGetStoryPortraitColors(npc, out Color top, out Color hat)) continue;
                checkedCount++;
                NpcAppearance world = NpcVisualBuilder.StoryNpcAppearance(npc);
                Color torso = NpcDialogueUI.VisibleTorsoColor(world);
                Assert.IsTrue(SameRgb(top, world.top) || SameRgb(top, torso),
                    $"{npc}: 몸통 — 초상 {top} / 월드 셔츠 {world.top}·겉옷 {torso}");
                Assert.AreEqual(world.hasHat, hat.a > 0f, $"{npc}: 모자 유무가 다르다");
                if (world.hasHat)
                    Assert.IsTrue(SameRgb(hat, world.hat), $"{npc}: 모자색 — 초상 {hat} / 월드 {world.hat}");
            }
            Assert.GreaterOrEqual(checkedCount, 12 + 7 + 3, "마을 주민 12 · 명부회 7 · 동행자 3");
        }

        [Test]
        public void EveryStoryPortrait_HasWorldSkinHairAndHairStyle_IncludingGreyTheCharacterPaletteLacks()
        {
            // 두 표가 같은 번호를 서로 다른 팔레트로 읽어 "자"의 회색 머리가 대사창에서 빨강, 세라의 검은 머리가 보라,
            // 어르신·물결 할머니·너울의 백발이 금발이 됐다. 초상이 있는 인물 전원이 월드 외형에서 받는지 본다.
            int checkedCount = 0;
            foreach (string npc in PortraitCandidates())
            {
                if (!NpcDialogueUI.TryGetStoryPortraitFace(npc, out Color skin, out Color hair, out int hairStyle)) continue;
                checkedCount++;
                NpcAppearance world = NpcVisualBuilder.StoryNpcAppearance(npc);
                Assert.IsTrue(SameRgb(skin, world.skin), $"{npc}: 피부 — 초상 {skin} / 월드 {world.skin}");
                Assert.IsTrue(SameRgb(hair, world.hair), $"{npc}: 머리색 — 초상 {hair} / 월드 {world.hair}");
                // 월드 0 짧은·1 중간·2 올림 ↔ 초상 0 짧은·1 중간·2 긴·3 올림
                Assert.AreEqual(world.hairStyle == 2 ? 3 : world.hairStyle, hairStyle, $"{npc}: 머리 모양");
            }
            Assert.GreaterOrEqual(checkedCount, 12 + 7 + 3, "마을 주민 12 · 명부회 7 · 동행자 3");
        }

        [Test]
        public void CompanionPortraits_MatchWorldHair_SeraBlackElderGrey()
        {
            // 사용자가 짚은 두 사람 — 세라(필드 검은 머리가 대사창 보라)와 어르신(필드 백발이 대사창 금발).
            foreach (string npc in new[] { "ruins_scholar", "village_elder", "catcher_rival" })
            {
                Assert.IsTrue(NpcDialogueUI.TryGetStoryPortraitFace(npc, out _, out Color hair, out _), $"{npc}: 초상 없음");
                Assert.IsTrue(SameRgb(hair, NpcVisualBuilder.StoryNpcAppearance(npc).hair), $"{npc}: 머리색 — 초상 {hair}");
            }
        }

        // ── 표식 판정 ──
        // 의뢰 표식은 눌러서 따라가는 손잡이다 — 본편 대상이 됐다고 민트 !로 덮이면 그 주민의 의뢰가
        // 지도에서 사라진다. 할 의뢰가 없는 주민일 때만 본편 표식이 붙는다.

        [TestCase(TaleStepKind.Talk, false, QuestMark.New)]
        [TestCase(TaleStepKind.Talk, true, QuestMark.New)]
        [TestCase(TaleStepKind.Report, false, QuestMark.Report)]
        [TestCase(TaleStepKind.Report, true, QuestMark.Report)]
        [TestCase(TaleStepKind.Errand, true, QuestMark.Main)]
        [TestCase(TaleStepKind.Waiting, true, QuestMark.Main)]
        [TestCase(TaleStepKind.Done, true, QuestMark.Main)]
        [TestCase(TaleStepKind.None, true, QuestMark.Main)]
        [TestCase(TaleStepKind.Errand, false, QuestMark.None)]
        [TestCase(TaleStepKind.Waiting, false, QuestMark.None)]
        [TestCase(TaleStepKind.Done, false, QuestMark.None)]
        public void MarkFor_TaleMarkWinsOverMain_MainOnlyWhenNoTaleToTell(
            TaleStepKind step, bool isMainTarget, QuestMark expected)
        {
            Assert.AreEqual(expected, StoryTaleResolver.MarkFor(step, isMainTarget));
        }

        private static HashSet<string> PortraitCandidates()
        {
            var ids = new HashSet<string>(StoryTaleResolver.CollectTaleNpcIds(StoryService.AllBeats()));
            foreach (StoryBeat b in StoryService.AllBeats())
            {
                if (!string.IsNullOrEmpty(b.speakerNpcId)) ids.Add(b.speakerNpcId);
                if (b.trigger != null && b.trigger.type == StoryDirector.TriggerNpcTalk && !string.IsNullOrEmpty(b.trigger.param))
                    ids.Add(b.trigger.param);
            }
            return ids;
        }

        private static bool SameRgb(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;
        }
    }
}
#endif
