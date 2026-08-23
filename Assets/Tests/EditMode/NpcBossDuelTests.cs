#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.NPC;
using UnityEngine;

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

            // 1막 64종은 PlaySceneBootstrap의 CreateStableInsect 호출부에 있어 C#에서 실행할 수
            // 없다(씬이 필요하다). **소스를 읽어 ID만 뽑는다** — 옛 주석은 "보스 곤충은 전부 2막
            // 확장에서 고르므로 두 집합으로 충분하다"고 했는데, 1막 하수가 흔한 1막 종을
            // 부리게 되면서 그 전제가 깨졌다(하수가 지역 고유종을 쓰면 "쓸어 담는다"는 태도가
            // 안 읽힌다 — 곤충 선택 자체가 서사라 종을 바꾸는 대신 검사를 넓혔다).
            string bootstrap = ReadRepoText("Assets/Scripts/Core/PlaySceneBootstrap.cs");
            foreach (Match m in Regex.Matches(bootstrap, @"CreateStableInsect\(""([a-z_0-9]+)"""))
                ids.Add(m.Groups[1].Value);

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

        [Test]
        public void Act1Thugs_AreWeakerThanAnyLedgerOfficer()
        {
            // 조직의 위계가 숫자로 읽혀야 한다. 하수가 간부에 가까우면 2막에서 급이 올라간
            // 느낌이 사라지고, 하수가 더 세면 1막에서 막힌다.
            int weakestOfficer = int.MaxValue;
            int strongestThug = 0;
            foreach (NpcBossDuels.BossDuel d in NpcBossDuels.All())
            {
                bool isThug = d.storyNpcId.StartsWith("ledger_thug_");
                if (isThug) strongestThug = Mathf.Max(strongestThug, d.level);
                else weakestOfficer = Mathf.Min(weakestOfficer, d.level);
            }

            Assert.Greater(strongestThug, 0, "1막 하수 대결이 표에 없다");
            Assert.Less(weakestOfficer, int.MaxValue, "간부 대결이 표에 없다");
            Assert.Less(strongestThug + 10, weakestOfficer,
                $"하수(Lv.{strongestThug})와 간부(Lv.{weakestOfficer})의 급 차이가 10 미만 — 위계가 안 읽힌다");
        }

        [Test]
        public void Act1Thugs_AreBeatableWithinFirstActLevelRange()
        {
            // 1막 마지막 리전(유적)의 수문장 레벨을 넘으면 그 자리에서 진행이 막힌다.
            int ruinsGuardian = 0;
            foreach (RegionData r in RegionDefinitions.CreateAll())
                if (r != null && r.regionId == "ruins") ruinsGuardian = r.guardianLevel;

            Assert.Greater(ruinsGuardian, 0, "유적 수문장 레벨을 못 읽었다");

            foreach (NpcBossDuels.BossDuel d in NpcBossDuels.All())
            {
                if (!d.storyNpcId.StartsWith("ledger_thug_")) continue;
                Assert.LessOrEqual(d.level, ruinsGuardian + 6,
                    $"{d.displayName} Lv.{d.level}이 유적 수문장 Lv.{ruinsGuardian}보다 너무 높다 — 1막에서 막힌다");
            }
        }

        [Test]
        public void EveryThug_HasWorldAnchorAndIntroBeat()
        {
            // 전투는 소개 비트를 본 뒤에만 열린다(WorldInteractionController). 소개 비트가
            // 없으면 그 하수와는 영영 싸울 수 없고, 월드 앵커가 없으면 만날 수도 없다.
            string village = ReadRepoText("Assets/Scripts/Core/VillageBuilder.cs");
            string story = ReadRepoText("Assets/Resources/Story.json");

            foreach (NpcBossDuels.BossDuel d in NpcBossDuels.All())
            {
                if (!d.storyNpcId.StartsWith("ledger_thug_")) continue;
                StringAssert.Contains($"storyNpcId = \"{d.storyNpcId}\"", village,
                    $"{d.storyNpcId}가 월드에 배치되지 않았다 — 만날 수 없다");
                StringAssert.Contains($"\"param\": \"{d.storyNpcId}\"", story,
                    $"{d.storyNpcId}에게 NpcTalk 소개 비트가 없다 — 대결이 열리지 않는다");
            }
        }

        /// <summary>
        /// <b>명부회 보스는 전원 장부를 든다.</b> 하나가 0이면 그 인물만 아무 압박 없이
        /// 싸워, 조직의 서명이던 것이 그 사람에게만 없는 상태가 된다 — 예외도 경고도
        /// 안 나고 그냥 밋밋한 전투가 된다.
        ///
        /// 하한도 함께 본다. <c>MinThreshold</c> 아래면 반복 한 번(+2)에 즉시 터져
        /// <b>피할 방법이 사라진다</b>(<c>LedgerPressure.IsActive</c>가 그때 장부를 통째로 끈다).
        /// </summary>
        [Test]
        public void EveryBoss_ArmsTheLedger()
        {
            foreach (NpcBossDuels.BossDuel d in NpcBossDuels.All())
            {
                Assert.GreaterOrEqual(d.ledgerThreshold, InsectGame.Battle.LedgerPressure.MinThreshold,
                    $"{d.storyNpcId}의 ledgerThreshold가 {d.ledgerThreshold} — 장부가 안 걸리거나 피할 수 없다");
            }
        }

        /// <summary>
        /// <b>임계는 곧 계급이다.</b> 하수가 간부보다 빨리 적으면 위계가 뒤집혀,
        /// 초반 하수전이 최종전보다 가혹해진다. 레벨과 임계가 같은 방향을 가리켜야 한다
        /// (레벨이 높을수록 임계는 낮다 = 빨리 적는다).
        /// </summary>
        [Test]
        public void Threshold_TightensWithRank()
        {
            NpcBossDuels.BossDuel[] all = NpcBossDuels.All();
            foreach (NpcBossDuels.BossDuel low in all)
            {
                foreach (NpcBossDuels.BossDuel high in all)
                {
                    if (low.level >= high.level) continue;
                    Assert.GreaterOrEqual(low.ledgerThreshold, high.ledgerThreshold,
                        $"{low.storyNpcId}(Lv.{low.level}, 임계 {low.ledgerThreshold})가 " +
                        $"{high.storyNpcId}(Lv.{high.level}, 임계 {high.ledgerThreshold})보다 빨리 적는다 — 위계가 뒤집혔다");
                }
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
