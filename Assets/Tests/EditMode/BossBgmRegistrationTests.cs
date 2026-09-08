#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using InsectGame.NPC;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// BGM은 3지점(BgmType enum / BgmTypeToString / ProceduralAudioGenerator.GetBGM)을
    /// 함께 등록해야 소리가 난다. enum에만 넣으면 <c>BgmTypeToString</c>의 default가
    /// "explore"를 돌려주거나 생성기가 LogWarning + null을 내고 <b>조용히 무음</b>이 된다.
    ///
    /// 런타임 호출로는 잡히지 않는다(경고만 찍고 넘어간다) — 그래서 소스를 읽어 확인한다.
    /// </summary>
    [TestFixture]
    public class BossBgmRegistrationTests
    {
        private const string AudioManagerPath = "Assets/Scripts/Core/AudioManager.cs";
        private const string GeneratorPath = "Assets/Scripts/Core/ProceduralAudioGenerator.cs";

        private static string Read(string relativePath)
        {
            string full = Path.Combine(UnityEngine.Application.dataPath, "..", relativePath);
            Assert.IsTrue(File.Exists(full), $"소스를 못 찾음: {relativePath}");
            return File.ReadAllText(full);
        }

        /// <summary>주석을 걷어낸 소스 — 주석에 적힌 예시가 등록으로 오인되지 않게 한다.</summary>
        private static string ReadCode(string relativePath)
        {
            string src = Read(relativePath);
            src = Regex.Replace(src, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\n]*", string.Empty);
            return src;
        }

        [TestCase("BossLedger", "boss_ledger")]
        [TestCase("BossFinal", "boss_final")]
        public void BossBgm_RegisteredAtAllThreePoints(string enumName, string key)
        {
            string audio = ReadCode(AudioManagerPath);
            string generator = ReadCode(GeneratorPath);

            // enum 마지막 항목엔 쉼표가 없다 — 블록을 잘라 항목 단위로 본다.
            Match block = Regex.Match(audio, @"enum BgmType\s*\{(.*?)\}", RegexOptions.Singleline);
            Assert.IsTrue(block.Success, "BgmType enum 블록 파싱 실패 — 이 테스트가 무의미해졌다");
            var members = new HashSet<string>();
            foreach (string raw in block.Groups[1].Value.Split(','))
                members.Add(raw.Trim());
            Assert.IsTrue(members.Contains(enumName), $"BgmType enum에 {enumName}이 없다");
            StringAssert.Contains($"case BgmType.{enumName}: return \"{key}\"", audio,
                $"BgmTypeToString에 {enumName} case가 없다 — default로 떨어져 탐험 곡이 나온다");
            StringAssert.Contains($"case \"{key}\"", generator,
                $"GetBGM에 \"{key}\" case가 없다 — LogWarning + null로 무음이 된다");
        }

        [Test]
        public void EveryBgmTypeCase_HasAGeneratorCase()
        {
            // BgmTypeToString이 돌려주는 키 전부가 생성기에 있어야 한다.
            string audio = ReadCode(AudioManagerPath);
            string generator = ReadCode(GeneratorPath);

            var keys = new List<string>();
            foreach (Match m in Regex.Matches(audio, @"case BgmType\.\w+:\s*return\s+""([a-z_]+)"""))
                keys.Add(m.Groups[1].Value);

            Assert.Greater(keys.Count, 0, "BgmTypeToString 파싱 실패 — 이 테스트가 무의미해졌다");

            var missing = new List<string>();
            foreach (string key in keys)
                if (!generator.Contains($"case \"{key}\"")) missing.Add(key);

            CollectionAssert.IsEmpty(missing, $"생성기에 없는 BGM 키(무음): {string.Join(", ", missing)}");
        }

        [Test]
        public void EveryCombatBgm_IsCoveredByIntensityRamp()
        {
            // 등록 4번째 지점. 앞의 셋(enum/문자열/생성기)만 하면 소리는 나지만 긴장 램프가 빠진다 —
            // 보스 2종이 실제로 그렇게 샜다. 전투 계열 곡 이름을 규약으로 삼아 대조한다.
            string audio = ReadCode(AudioManagerPath);

            Match combat = Regex.Match(audio, @"IsCombatBgm\(BgmType type\)(.*?);", RegexOptions.Singleline);
            Assert.IsTrue(combat.Success, "IsCombatBgm 파싱 실패 — 이 테스트가 무의미해졌다");

            Match block = Regex.Match(audio, @"enum BgmType\s*\{(.*?)\}", RegexOptions.Singleline);
            Assert.IsTrue(block.Success, "BgmType enum 블록 파싱 실패");

            var missing = new List<string>();
            foreach (string raw in block.Groups[1].Value.Split(','))
            {
                string name = raw.Trim();
                // 전투 계열의 규약: Battle로 끝나거나 Boss로 시작한다.
                bool isCombat = name == "Battle" || name.EndsWith("Battle") || name.StartsWith("Boss");
                if (isCombat && !combat.Groups[1].Value.Contains($"BgmType.{name}")) missing.Add(name);
            }

            CollectionAssert.IsEmpty(missing,
                $"전투 BGM인데 IsCombatBgm에 없다(긴장 램프 누락): {string.Join(", ", missing)}");
        }

        [Test]
        public void BossTable_HasExactlyOneFinalDuel()
        {
            // 최종전 플래그가 둘이면 간부전이 최종 테마로 나오고, 없으면 최종전이 간부 테마로 나온다.
            int finals = 0;
            foreach (NpcBossDuels.BossDuel duel in NpcBossDuels.All())
                if (duel.isFinal) finals++;

            Assert.AreEqual(1, finals, "isFinal이 정확히 하나여야 한다");
        }

        [Test]
        public void FinalDuel_IsTheHighestLevelBoss()
        {
            NpcBossDuels.BossDuel final = default;
            int highest = 0;
            foreach (NpcBossDuels.BossDuel duel in NpcBossDuels.All())
            {
                if (duel.isFinal) final = duel;
                if (duel.level > highest) highest = duel.level;
            }

            Assert.AreEqual(highest, final.level,
                "최종전이 가장 높은 레벨의 보스가 아니다 — 표와 플래그가 어긋났다");
        }
    }
}
#endif
