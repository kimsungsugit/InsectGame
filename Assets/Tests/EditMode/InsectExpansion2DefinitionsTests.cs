#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using InsectGame.Core;
using InsectGame.Data;

namespace InsectGame.Tests
{
    /// <summary>
    /// 2막(ver2) 곤충 확장 54종 시드 데이터 무결성 테스트.
    /// InsectExpansionDefinitionsTests의 형제 — 같은 불변식을 2막 시드에 적용한다.
    /// 특히 InsectEntity.BuildModel의 ID contains 분기(부분문자열 오매칭)를 데이터 차원에서 막고,
    /// 1막 확장 64종과 ID가 겹치지 않는지(DB 중복 등록) 확인한다.
    /// </summary>
    [TestFixture]
    public class InsectExpansion2DefinitionsTests
    {
        [Test]
        public void CreateAll_Count_Is54()
        {
            Assert.AreEqual(54, InsectExpansion2Definitions.CreateAll().Length);
        }

        [Test]
        public void AllNewIds_Unique_NoDuplicates()
        {
            string[] ids = InsectExpansion2Definitions.AllNewIds();
            HashSet<string> unique = new HashSet<string>(ids);
            Assert.AreEqual(ids.Length, unique.Count, "중복 insectId 존재");
        }

        [Test]
        public void AllNewIds_DoNotCollideWithFirstExpansion()
        {
            // 부트스트랩이 두 파일을 연달아 소비하므로 ID가 겹치면 InsectDatabase에 같은 종이
            // 두 번 등록되고, GetById는 앞쪽만 돌려줘 뒤 정의가 조용히 죽는다.
            HashSet<string> first = new HashSet<string>(InsectExpansionDefinitions.AllNewIds());
            foreach (string id in InsectExpansion2Definitions.AllNewIds())
            {
                Assert.IsFalse(first.Contains(id), $"{id}: 1막 확장 64종과 ID 충돌");
            }
        }

        [Test]
        public void AllNewIds_AllSnakeCase()
        {
            Regex snakeCase = new Regex("^[a-z][a-z0-9_]*$");
            foreach (string id in InsectExpansion2Definitions.AllNewIds())
            {
                Assert.IsTrue(snakeCase.IsMatch(id), $"snake_case 위반: {id}");
            }
        }

        [Test]
        public void NewSeeds_RarityDistribution_Matches()
        {
            Dictionary<InsectRarity, int> counts = new Dictionary<InsectRarity, int>();
            foreach (InsectSeed seed in InsectExpansion2Definitions.CreateAll())
            {
                counts.TryGetValue(seed.rarity, out int n);
                counts[seed.rarity] = n + 1;
            }

            Assert.AreEqual(18, counts[InsectRarity.Common], "Common 수 불일치");
            Assert.AreEqual(12, counts[InsectRarity.Uncommon], "Uncommon 수 불일치");
            Assert.AreEqual(12, counts[InsectRarity.Rare], "Rare 수 불일치");
            Assert.AreEqual(10, counts[InsectRarity.Epic], "Epic 수 불일치");
            Assert.AreEqual(2, counts[InsectRarity.Legendary], "Legendary 수 불일치");
        }

        [Test]
        public void NewSeeds_WeightPositive_DifficultyInRange()
        {
            foreach (InsectSeed seed in InsectExpansion2Definitions.CreateAll())
            {
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
            foreach (string id in InsectExpansion2Definitions.AllNewIds())
            {
                if (id.Contains("bee") && !id.Contains("beetle"))
                {
                    Assert.IsTrue(
                        id.StartsWith("bee_") || id.EndsWith("_bee") || id.Contains("_bee_"),
                        $"{id}: 벌 ID 규칙 위반 (bee_ 접두/_bee 접미 필요)");
                }

                string stripped = id.Replace("mantis", "").Replace("antlion", "");
                if (stripped.Contains("ant"))
                {
                    Assert.IsTrue(id.StartsWith("ant_"),
                        $"{id}: 개미 외 종에 'ant' 부분문자열 포함 (BuildAnt 오라우팅 위험)");
                }

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
            foreach (string id in InsectExpansion2Definitions.AllNewIds())
            {
                if (id.Contains("ghost") || id.Contains("orchid"))
                {
                    Assert.IsTrue(id.Contains("mantis"),
                        $"{id}: ghost/orchid는 mantis ID에만 허용");
                }

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
            // 2막 태그는 PlaySceneBootstrap.InferPrimaryType의 zone 폴백과 짝이다.
            // 여기 없는 태그를 쓰면 그 종의 속성이 조용히 Bug로 떨어진다.
            HashSet<string> known = new HashSet<string>
            {
                "Hollow", "Dunes", "Frostline", "Emberfall", "Canopy", "Nameless"
            };
            foreach (InsectSeed seed in InsectExpansion2Definitions.CreateAll())
            {
                Assert.IsTrue(known.Contains(seed.habitat), $"{seed.id}: 미지의 habitat 태그 '{seed.habitat}'");
            }
        }

        [Test]
        public void AllNewIds_EachAssignedToAtLeastOnePool()
        {
            // 풀 미배정 = 영원히 스폰 불가. 리전 insectIds 또는 서브에리어 exclusiveInsectIds에 있어야 한다.
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

            foreach (string id in InsectExpansion2Definitions.AllNewIds())
            {
                Assert.IsTrue(pooled.Contains(id), $"{id}: 어떤 리전/서브에리어 풀에도 배정되지 않음");
            }
        }
    }
}
#endif
