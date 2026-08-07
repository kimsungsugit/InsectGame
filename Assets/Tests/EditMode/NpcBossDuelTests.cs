#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.NPC;

namespace InsectGame.Tests
{
    /// <summary>
    /// 명부회 간부 보스 대결 표(<see cref="NpcBossDuels"/>)의 정합성.
    ///
    /// 이 표의 ID가 틀리면 런타임엔 아무 에러도 안 난다 — <c>CanBossDuel</c>이 조용히 false를
    /// 돌려 그 간부에게 영영 도전할 수 없게 될 뿐이다. 여기서 잡는다.
    /// </summary>
    [TestFixture]
    public class NpcBossDuelTests
    {
        private static HashSet<string> AllInsectIds()
        {
            HashSet<string> ids = new HashSet<string>();
            foreach (InsectSeed seed in InsectExpansionDefinitions.CreateAll()) ids.Add(seed.id);
            foreach (InsectSeed seed in InsectExpansion2Definitions.CreateAll()) ids.Add(seed.id);
            // 1막 64종은 PlaySceneBootstrap의 CreateStableInsect 호출부에 있어 씬 없이 못 읽는다.
            // 보스 곤충은 전부 2막 확장에서 고르므로 이 두 집합으로 충분하다.
            return ids;
        }

        [Test]
        public void Table_IsNotEmpty()
        {
            Assert.Greater(NpcBossDuels.All().Length, 0, "보스 대결 표가 비었다");
        }

        [Test]
        public void StoryNpcIds_AreUnique()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (NpcBossDuels.BossDuel d in NpcBossDuels.All())
            {
                Assert.IsTrue(seen.Add(d.storyNpcId), $"storyNpcId 중복: {d.storyNpcId}");
            }
        }

        [Test]
        public void EveryBoss_HasWorldAnchor()
        {
            // 앵커가 없으면 그 간부는 월드에 서 있지 않아 말을 걸 수조차 없다.
            // VillageBuilder의 storyNpcId 리터럴이 배치의 단일 출처다(story_lint도 거기서 읽는다).
            HashSet<string> placed = new HashSet<string>(VillageBuilderStoryNpcIds());

            foreach (NpcBossDuels.BossDuel d in NpcBossDuels.All())
            {
                Assert.IsTrue(placed.Contains(d.storyNpcId),
                    $"{d.storyNpcId}: 보스 표에는 있는데 월드에 배치되지 않았다");
            }
        }

        [Test]
        public void EveryBoss_InsectExistsInExpansion()
        {
            HashSet<string> ids = AllInsectIds();
            foreach (NpcBossDuels.BossDuel d in NpcBossDuels.All())
            {
                Assert.IsTrue(ids.Contains(d.insectId),
                    $"{d.storyNpcId}: 상대 곤충 {d.insectId}가 존재하지 않는다");
            }
        }

        [Test]
        public void EveryBoss_LevelWithinCap()
        {
            int cap = GameConstants.Leveling.FallbackMaxLevel;
            foreach (NpcBossDuels.BossDuel d in NpcBossDuels.All())
            {
                Assert.Greater(d.level, 0, $"{d.storyNpcId}: 레벨이 0 이하");
                Assert.LessOrEqual(d.level, cap, $"{d.storyNpcId}: 레벨 {d.level} > 상한 {cap}");
            }
        }

        [Test]
        public void EveryBoss_HasRewardAndDisplayName()
        {
            foreach (NpcBossDuels.BossDuel d in NpcBossDuels.All())
            {
                Assert.IsFalse(string.IsNullOrEmpty(d.displayName), $"{d.storyNpcId}: 표시명 누락");
                Assert.IsFalse(string.IsNullOrEmpty(d.rewardItemId), $"{d.storyNpcId}: 보상 아이템 누락");
                Assert.Greater(d.rewardCount, 0, $"{d.storyNpcId}: 보상 수량이 0 이하");
                Assert.Greater(d.retryCooldownSeconds, 0f, $"{d.storyNpcId}: 재도전 쿨다운이 0 이하");
            }
        }

        [Test]
        public void LedgerInk_IsNotABoss()
        {
            // 먹은 잿불 골짜기에서 이탈해 아군이 된다 — 싸울 상대가 아니다.
            Assert.IsFalse(NpcBossDuels.IsBoss("ledger_ink"),
                "먹은 아군화하므로 보스 표에 있으면 안 된다");
        }

        [Test]
        public void TryGet_UnknownId_ReturnsFalse()
        {
            Assert.IsFalse(NpcBossDuels.TryGet("village_elder", out _));
            Assert.IsFalse(NpcBossDuels.TryGet("", out _));
            Assert.IsFalse(NpcBossDuels.TryGet(null, out _));
        }

        // VillageBuilder는 MonoBehaviour라 씬 없이 Build를 못 돌린다 — 배치된 storyNpcId만
        // 소스에서 읽어 온다. story_lint(game_facts.story_npc_ids)와 같은 출처·같은 형태다.
        private static IEnumerable<string> VillageBuilderStoryNpcIds()
        {
            string path = System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "Scripts/Core/VillageBuilder.cs");
            Assert.IsTrue(System.IO.File.Exists(path),
                "VillageBuilder.cs를 못 찾았다 — 경로가 바뀌었으면 이 테스트도 함께 고칠 것");
            string src = System.IO.File.ReadAllText(path);
            var matches = System.Text.RegularExpressions.Regex.Matches(src, "storyNpcId = \"(\\w+)\"");
            Assert.Greater(matches.Count, 0,
                "VillageBuilder에서 storyNpcId를 하나도 못 읽었다 — 배치 형태가 바뀌었는가?");
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                yield return m.Groups[1].Value;
            }
        }
    }
}
#endif
