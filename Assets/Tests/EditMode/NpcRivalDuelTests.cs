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
            // 엔딩 뒤 연못 — 연못 단계를 이미 이겼으면 재대결이 뜬다(각 단계는 독립이다).
            var seen = Seen("ch2_water", "post_rival_rematch");
            Assert.IsTrue(NpcRivalDuels.TrySelect("catcher_rival", "pond", seen, NoneDefeated, out NpcRivalDuels.Stage first));
            Assert.AreEqual("rival_pond", first.stageId);
            Assert.IsTrue(NpcRivalDuels.TrySelect("catcher_rival", "pond", seen, id => id == "rival_pond",
                out NpcRivalDuels.Stage next));
            Assert.AreEqual("rival_final", next.stageId);
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
