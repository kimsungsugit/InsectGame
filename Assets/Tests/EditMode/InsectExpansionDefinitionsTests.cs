#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using InsectGame.Core;
using InsectGame.Data;

namespace InsectGame.Tests
{
    /// <summary>
    /// 곤충 확장 64종 시드 데이터 무결성 테스트.
    /// InsectEntity.BuildModel의 ID contains 분기(부분문자열 오매칭)를 데이터 차원에서 방어한다.
    /// </summary>
    [TestFixture]
    public class InsectExpansionDefinitionsTests
    {
        [Test]
        public void CreateAll_Count_Is64()
        {
            Assert.AreEqual(64, InsectExpansionDefinitions.CreateAll().Length);
        }

        [Test]
        public void AllNewIds_Unique_NoDuplicates()
        {
            string[] ids = InsectExpansionDefinitions.AllNewIds();
            HashSet<string> unique = new HashSet<string>(ids);
            Assert.AreEqual(ids.Length, unique.Count, "중복 insectId 존재");
        }

        [Test]
        public void AllNewIds_AllSnakeCase()
        {
            // snake_case: 소문자/숫자/언더스코어만, 언더스코어로 시작·끝 금지
            Regex snakeCase = new Regex("^[a-z0-9]+(_[a-z0-9]+)*$");
            foreach (string id in InsectExpansionDefinitions.AllNewIds())
            {
                Assert.IsTrue(snakeCase.IsMatch(id), $"snake_case 위반: {id}");
            }
        }

        [Test]
        public void NewIds_EpicPlusCount_Is17()
        {
            int epicPlus = 0;
            foreach (InsectSeed seed in InsectExpansionDefinitions.CreateAll())
            {
                if (seed.rarity >= InsectRarity.Epic) epicPlus++;
            }
            Assert.AreEqual(17, epicPlus, "에픽+ 종 수(전용기 필요 수) 불일치");
        }

        [Test]
        public void NewSeeds_RarityDistribution_Matches()
        {
            Dictionary<InsectRarity, int> counts = new Dictionary<InsectRarity, int>();
            foreach (InsectSeed seed in InsectExpansionDefinitions.CreateAll())
            {
                counts.TryGetValue(seed.rarity, out int c);
                counts[seed.rarity] = c + 1;
            }

            Assert.AreEqual(16, counts[InsectRarity.Common], "Common 수 불일치");
            Assert.AreEqual(16, counts[InsectRarity.Uncommon], "Uncommon 수 불일치");
            Assert.AreEqual(15, counts[InsectRarity.Rare], "Rare 수 불일치");
            Assert.AreEqual(11, counts[InsectRarity.Epic], "Epic 수 불일치");
            Assert.AreEqual(6, counts[InsectRarity.Legendary], "Legendary 수 불일치");
        }

        [Test]
        public void NewSeeds_WeightPositive_DifficultyInRange()
        {
            foreach (InsectSeed seed in InsectExpansionDefinitions.CreateAll())
            {
                // 신규 64종은 전부 필드 스폰 대상 — weight 0(가챠 전용)은 없어야 함
                Assert.Greater(seed.weight, 0f, $"{seed.id}: spawnWeight는 양수여야 함");
                Assert.Greater(seed.difficulty, 0f, $"{seed.id}: 포획 난이도 하한 위반");
                Assert.Less(seed.difficulty, 1f, $"{seed.id}: 포획 난이도 상한 위반");
                Assert.IsFalse(string.IsNullOrEmpty(seed.name), $"{seed.id}: 이름 누락");
                Assert.IsFalse(string.IsNullOrEmpty(seed.desc), $"{seed.id}: 설명 누락");
                Assert.IsFalse(string.IsNullOrEmpty(seed.habitat), $"{seed.id}: habitat 누락");
            }
        }

        [Test]
        public void NewIds_FollowBeeAntFlyPrefixRules()
        {
            foreach (string id in InsectExpansionDefinitions.AllNewIds())
            {
                // 벌: "bee"는 "beetle"의 부분문자열 — 벌이면 bee_ 접두/_bee 접미/_bee_ 포함이어야 함
                if (id.Contains("bee") && !id.Contains("beetle"))
                {
                    Assert.IsTrue(
                        id.StartsWith("bee_") || id.EndsWith("_bee") || id.Contains("_bee_"),
                        $"{id}: 벌 ID 규칙 위반 (bee_ 접두/_bee 접미 필요)");
                }

                // 개미: "ant"는 mantis/antlion에 포함 — 그 외의 "ant"는 ant_ 접두만 허용
                //       (phantom/giant/elephant 같은 수식어는 BuildModel ant 분기 오매칭 위험)
                string stripped = id.Replace("mantis", "").Replace("antlion", "");
                if (stripped.Contains("ant"))
                {
                    Assert.IsTrue(id.StartsWith("ant_"),
                        $"{id}: 개미 외 종에 'ant' 부분문자열 포함 (BuildAnt 오라우팅 위험)");
                }

                // 파리: "fly"는 dragonfly/butterfly/firefly/damselfly에 포함 — 그 외에는 fly_ 접두만 허용
                if (id.Contains("fly")
                    && !id.Contains("dragonfly") && !id.Contains("butterfly")
                    && !id.Contains("firefly") && !id.Contains("damselfly"))
                {
                    Assert.IsTrue(id.StartsWith("fly_"),
                        $"{id}: 파리 ID 규칙 위반 (fly_ 접두 필요)");
                }
            }
        }

        [Test]
        public void NewIds_NoMantisOnlyModifiersOnOtherSpecies()
        {
            foreach (string id in InsectExpansionDefinitions.AllNewIds())
            {
                // ghost/orchid는 사마귀 전용 수식어 (BuildGhostMantis/BuildOrchidMantis 분기가
                // mantis 분기보다 앞서서 다른 종에 붙으면 사마귀로 렌더됨)
                if (id.Contains("ghost") || id.Contains("orchid"))
                {
                    Assert.IsTrue(id.Contains("mantis"),
                        $"{id}: ghost/orchid는 mantis ID에만 허용");
                }

                // luna/atlas는 moth 별칭 — 나방 외 종에 붙으면 나방으로 렌더됨
                if (id.Contains("luna") || id.Contains("atlas"))
                {
                    Assert.IsTrue(id.Contains("moth"),
                        $"{id}: luna/atlas는 moth ID에만 허용");
                }
            }
        }

        [Test]
        public void NewSeeds_HabitatTags_AreKnown()
        {
            // 신규 태그(Swamp/Mountain/Ruins)는 InferPrimaryType 폴백 확장과 짝 — 오타 방지
            HashSet<string> known = new HashSet<string>
            {
                "Meadow", "Pond", "Forest", "Garden", "Swamp", "Mountain", "Ruins"
            };
            foreach (InsectSeed seed in InsectExpansionDefinitions.CreateAll())
            {
                Assert.IsTrue(known.Contains(seed.habitat), $"{seed.id}: 미지의 habitat 태그 '{seed.habitat}'");
            }
        }

        [Test]
        public void AllNewIds_EachAssignedToAtLeastOnePool()
        {
            // 신규 64종 전부가 어느 리전 insectIds 또는 서브에리어 exclusiveInsectIds에든
            // 최소 1회 등장해야 함 (풀 미배정 = 영원히 스폰 불가)
            HashSet<string> pooled = new HashSet<string>();
            foreach (RegionData region in RegionDefinitions.CreateAll())
            {
                if (region.insectIds != null)
                {
                    foreach (string id in region.insectIds) pooled.Add(id);
                }
                if (region.subAreas == null) continue;
                foreach (SubAreaData sub in region.subAreas)
                {
                    if (sub.exclusiveInsectIds == null) continue;
                    foreach (string id in sub.exclusiveInsectIds) pooled.Add(id);
                }
            }

            foreach (string id in InsectExpansionDefinitions.AllNewIds())
            {
                Assert.IsTrue(pooled.Contains(id), $"{id}: 어떤 리전/서브에리어 풀에도 배정되지 않음");
            }
        }
    }
}
#endif
