#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.NPC;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 라온 라이벌 대결 — 단계 선택(<see cref="NpcRivalDuels.TrySelect"/>)과 표의 무결성.
    /// 단계 ID·비트·리전이 틀려도 런타임엔 아무 말이 없다 — [승부하기] 버튼이 그냥 안 뜬다.
    /// </summary>
    [TestFixture]
    public class NpcRivalDuelTests
    {
        private static System.Func<string, bool> Seen(params string[] ids)
        {
            var set = new HashSet<string>(ids);
            return set.Contains;
        }

        private static readonly System.Func<string, bool> NoneDefeated = _ => false;

        [Test]
        public void TrySelect_BeforeOpeningBeat_Nothing()
        {
            Assert.IsFalse(NpcRivalDuels.TrySelect("catcher_rival", "pond", Seen(), NoneDefeated, out _));
        }

        [Test]
        public void TrySelect_OpenedAndInRegion_PicksThatStage()
        {
            Assert.IsTrue(NpcRivalDuels.TrySelect("catcher_rival", "pond", Seen("ch2_water"), NoneDefeated,
                out NpcRivalDuels.Stage s));
            Assert.AreEqual("rival_pond", s.stageId);
        }

        [Test]
        public void TrySelect_WrongRegion_Nothing()
        {
            Assert.IsFalse(NpcRivalDuels.TrySelect("catcher_rival", "meadow", Seen("ch2_water"), NoneDefeated, out _));
        }

        [Test]
        public void TrySelect_FrostStage_ClosesWhenRaonIsHurt()
        {
            Assert.IsTrue(NpcRivalDuels.TrySelect("catcher_rival", "frostline", Seen("ch9_arrive"), NoneDefeated,
                out NpcRivalDuels.Stage s));
            Assert.AreEqual("rival_frost", s.stageId);
            Assert.IsFalse(NpcRivalDuels.TrySelect("catcher_rival", "frostline",
                Seen("ch9_arrive", "ch10_confront"), NoneDefeated, out _), "갱도에서 다친 뒤엔 싸우지 않는다");
        }

        [Test]
        public void TrySelect_Defeated_SkipsToNextAvailable()
        {
            // 표 순서대로 첫 단계를 고른다 — 이긴 단계는 건너뛴다(각 단계는 독립이다). 표지만 세운 합성 상황이다.
            var seen = Seen("ch2_water", "post_rival_rematch");
            Assert.IsTrue(NpcRivalDuels.TrySelect("catcher_rival", "pond", seen, NoneDefeated, out NpcRivalDuels.Stage first));
            Assert.AreEqual("rival_pond", first.stageId);
            Assert.IsTrue(NpcRivalDuels.TrySelect("catcher_rival", "pond", seen, id => id == "rival_pond",
                out NpcRivalDuels.Stage next));
            Assert.AreEqual("rival_final", next.stageId);
        }

        [Test]
        public void TrySelect_AfterTheEnding_PreInjuryStagesAreClosed_OnlyTheRematch()
        {
            // 실제 엔딩 세이브는 갱도(ch10_confront)를 지나왔다 — 다치기 전 단계는 전부 닫히고 어디서든 재대결만 뜬다.
            var seen = Seen("ch1_rival_intro", "ch2_water", "ch4_reach_swamp", "ch7_arrive", "ch8_arrive",
                "ch9_arrive", "ch10_arrive", NpcRivalDuels.InjuryBeatId, "post_rival_rematch");
            foreach (string region in new[] { "meadow", "pond", "swamp", "hollow", "dunes", "frostline", "emberfall", "nameless" })
            {
                Assert.IsTrue(NpcRivalDuels.TrySelect("catcher_rival", region, seen, NoneDefeated, out NpcRivalDuels.Stage s), region);
                Assert.AreEqual("rival_final", s.stageId, region);
            }
        }

        [Test]
        public void TrySelect_MeadowOpensAtTheFirstMeeting()
        {
            // 첫 만남 대사가 "승부다!"로 끝나면 곧바로 이 단계가 열린다(StoryBeat.duelAfter → StoryDuelLauncher).
            Assert.IsFalse(NpcRivalDuels.TrySelect("catcher_rival", "meadow", Seen("ch1_intro"), NoneDefeated, out _));
            Assert.IsTrue(NpcRivalDuels.TrySelect("catcher_rival", "meadow", Seen("ch1_rival_intro"), NoneDefeated,
                out NpcRivalDuels.Stage s));
            Assert.AreEqual("rival_meadow", s.stageId);
            Assert.Less(s.level, 6, "첫 파트너(Lv.6)보다 낮다 — 첫 전투에서 막히면 이야기가 시작도 전에 선다");
        }

        [Test]
        public void TrySelect_EmberStage_OnlyBetweenArrivalAndTheKiln()
        {
            Assert.IsTrue(NpcRivalDuels.TrySelect("catcher_rival", "emberfall", Seen("ch10_arrive"), NoneDefeated,
                out NpcRivalDuels.Stage s));
            Assert.AreEqual("rival_ember", s.stageId);
            Assert.IsFalse(NpcRivalDuels.TrySelect("catcher_rival", "emberfall",
                Seen("ch10_arrive", "ch10_confront"), NoneDefeated, out _), "갱도에서 다친 뒤엔 싸우지 않는다");
        }

        [Test]
        public void EveryPreInjuryStage_ClosesAtTheKiln()
        {
            foreach (NpcRivalDuels.Stage s in NpcRivalDuels.All())
            {
                if (string.IsNullOrEmpty(s.regionId)) continue;   // 엔딩 뒤 재대결 — 다 나은 뒤다
                Assert.AreEqual(NpcRivalDuels.InjuryBeatId, s.closeBeatId, $"{s.stageId}: 다친 라온과 싸우게 된다");
            }
        }

        [Test]
        public void EveryChapterWhereRaonStands_HasAStage()
        {
            // 「장마다 한 번」 — 라온 앵커가 있는 리전마다 단계가 있다. 이름 없는 자리는 복귀(ch12_echo) 뒤라 엔딩 재대결이 맡는다.
            string village = ReadRepoText("Assets/Scripts/Core/VillageBuilder.cs");
            var staged = new HashSet<string>();
            foreach (NpcRivalDuels.Stage s in NpcRivalDuels.All())
                if (!string.IsNullOrEmpty(s.regionId)) staged.Add(s.regionId);
            foreach (Match m in Regex.Matches(village,
                         @"regionId\s*=\s*""([a-z_]+)"",\s*storyNpcId\s*=\s*""catcher_rival"""))
            {
                string region = m.Groups[1].Value;
                if (region == "nameless") continue;
                Assert.IsTrue(staged.Contains(region), $"{region}: 라온이 서 있는데 겨룰 단계가 없다");
            }
        }

        [Test]
        public void IsRival_OnlyRaon()
        {
            Assert.IsTrue(NpcRivalDuels.IsRival("catcher_rival"));
            Assert.IsFalse(NpcRivalDuels.IsRival("ledger_grip"));
            Assert.IsFalse(NpcRivalDuels.IsRival(null));
        }

        [Test]
        public void TrySelect_FinalRematch_AnyRegion()
        {
            Assert.IsTrue(NpcRivalDuels.TrySelect("catcher_rival", "nameless", Seen("post_rival_rematch"), NoneDefeated,
                out NpcRivalDuels.Stage s));
            Assert.AreEqual("rival_final", s.stageId);
        }

        [Test]
        public void TrySelect_OtherNpc_Nothing()
        {
            Assert.IsFalse(NpcRivalDuels.TrySelect("ruins_scholar", "pond", Seen("ch2_water"), NoneDefeated, out _));
        }

        [Test]
        public void Stages_UniquePrefixedIds_NotBossIds()
        {
            var ids = new HashSet<string>();
            foreach (NpcRivalDuels.Stage s in NpcRivalDuels.All())
            {
                Assert.IsTrue(s.stageId.StartsWith(NpcRivalDuels.StagePrefix), s.stageId);
                Assert.IsTrue(ids.Add(s.stageId), $"중복 단계 {s.stageId}");
                // 격파 기록을 간부와 같은 집합에 적는다 — 인물 ID와 겹치면 서로의 기록을 먹는다.
                Assert.IsFalse(NpcBossDuels.IsBoss(s.stageId), s.stageId);
            }
        }

        [Test]
        public void Stages_HaveBanter_ForTheRightFace()
        {
            foreach (NpcRivalDuels.Stage s in NpcRivalDuels.All())
            {
                Assert.IsTrue(DuelBanter.TryGet(s.stageId, out DuelBanter.Lines l), $"{s.stageId}: 대결 대사 없음");
                Assert.AreEqual(s.storyNpcId, l.npcId, s.stageId);
            }
        }

        [Test]
        public void Stages_GateBeatsExistInStory()
        {
            string story = ReadRepoText("Assets/Resources/Story.json");
            foreach (NpcRivalDuels.Stage s in NpcRivalDuels.All())
            {
                StringAssert.Contains($"\"beatId\": \"{s.openBeatId}\"", story, $"{s.stageId}: 여는 비트 없음");
                if (!string.IsNullOrEmpty(s.closeBeatId))
                    StringAssert.Contains($"\"beatId\": \"{s.closeBeatId}\"", story, $"{s.stageId}: 닫는 비트 없음");
            }
        }

        [Test]
        public void RegionalStages_RaonStandsThere_LevelWithinRegionBand()
        {
            // 리전 단계는 라온 앵커가 그 리전에 있어야 말을 걸 수 있다. 레벨은 그 리전 구간(요구~수문장) 안.
            string village = ReadRepoText("Assets/Scripts/Core/VillageBuilder.cs");
            var anchored = new HashSet<string>();
            foreach (Match m in Regex.Matches(village,
                         @"regionId\s*=\s*""([a-z_]+)"",\s*storyNpcId\s*=\s*""catcher_rival"""))
                anchored.Add(m.Groups[1].Value);
            var regions = new Dictionary<string, RegionData>();
            foreach (RegionData r in RegionDefinitions.CreateAll()) regions[r.regionId] = r;

            foreach (NpcRivalDuels.Stage s in NpcRivalDuels.All())
            {
                if (string.IsNullOrEmpty(s.regionId)) continue;
                Assert.IsTrue(anchored.Contains(s.regionId), $"{s.stageId}: {s.regionId}에 라온 앵커 없음");
                Assert.IsTrue(regions.TryGetValue(s.regionId, out RegionData r), s.regionId);
                Assert.That(s.level, Is.InRange(r.requiredLevel, r.guardianLevel), $"{s.stageId} Lv.{s.level}");
            }
        }

        [Test]
        public void Stages_InsectsExist_LevelsRise_RewardsSet()
        {
            var ids = new HashSet<string>();
            foreach (InsectSeed seed in InsectExpansionDefinitions.CreateAll()) ids.Add(seed.id);
            foreach (InsectSeed seed in InsectExpansion2Definitions.CreateAll()) ids.Add(seed.id);
            foreach (Match m in Regex.Matches(ReadRepoText("Assets/Scripts/Core/PlaySceneBootstrap.cs"),
                         @"CreateStableInsect\(""([a-z_0-9]+)"""))
                ids.Add(m.Groups[1].Value);

            int previous = 0;
            foreach (NpcRivalDuels.Stage s in NpcRivalDuels.All())
            {
                Assert.IsTrue(ids.Contains(s.insectId), $"{s.stageId}: 곤충 {s.insectId} 없음");
                Assert.Greater(s.level, previous, $"{s.stageId}: 라온은 단계마다 강해진다");
                Assert.LessOrEqual(s.level, 80, "곤충 레벨 상한");
                Assert.IsFalse(string.IsNullOrEmpty(s.rewardItemId), s.stageId);
                Assert.Greater(s.rewardCount, 0, s.stageId);
                previous = s.level;
            }
        }

        private static string ReadRepoText(string relativePath)
        {
            string full = System.IO.Path.Combine(Application.dataPath, "..", relativePath);
            Assert.IsTrue(System.IO.File.Exists(full), $"파일 없음: {relativePath}");
            return System.IO.File.ReadAllText(full);
        }
    }
}
#endif
