#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
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

        // ── 부트스트랩과의 "짝" 규칙 ──
        //
        // 위 NewSeeds_HabitatTags_AreKnown은 **시드 쪽만** 자기 하드코딩 목록과 맞춰 본다.
        // 짝의 반대편(PlaySceneBootstrap)이 무너져도 통과하므로, 아래 둘이 그쪽을 본다.
        // 분기 사슬은 private 인스턴스 메서드라 여기서 실행할 수 없어 소스를 읽는다 —
        // InsectPortraitRoutingTests가 같은 이유로 같은 방식을 쓴다.

        private const string BootstrapSourcePath = "Scripts/Core/PlaySceneBootstrap.cs";

        private static string ReadBootstrapSource()
        {
            string path = Path.Combine(Application.dataPath, BootstrapSourcePath);
            Assert.IsTrue(File.Exists(path),
                $"{BootstrapSourcePath}: 파일을 못 찾았다 — 경로가 바뀌었으면 이 테스트도 고칠 것");
            return File.ReadAllText(path);
        }

        [Test]
        public void EveryHabitatTag_HasZoneFallback_InBootstrap()
        {
            // habitat이 InferPrimaryType의 zone 폴백에서 빠지면 그 리전 종이 **전부 조용히 Bug 속성**으로
            // 떨어진다(에러도 경고도 없다). 속성은 상성 계산의 입력이라 전투가 통째로 어긋난다.
            // 시드 파일 상단 주석이 "한쪽만 고치면"이라고 경고하는 바로 그 방향이다.
            string source = ReadBootstrapSource();

            HashSet<string> habitats = new HashSet<string>();
            foreach (InsectSeed seed in InsectExpansion2Definitions.CreateAll())
            {
                habitats.Add(seed.habitat);
            }

            Assert.Greater(habitats.Count, 0, "전제: 2막 시드가 habitat 태그를 갖는다");
            foreach (string habitat in habitats)
            {
                StringAssert.Contains(
                    $"zone == \"{habitat}\"",
                    source,
                    $"habitat '{habitat}'에 대응하는 zone 폴백이 InferPrimaryType에 없다");
            }
        }

        [Test]
        public void EveryEpicAndLegendary_HasSignatureSkillCase()
        {
            // Epic/Legendary는 전용기를 갖는다. 부트스트랩 switch에서 빠지면 default
            // "궁극 생태 해방"으로 조용히 떨어져 **상위 종의 전용기가 전부 같은 이름**이 된다.
            // 그 switch 위 주석이 경고하지만 지금까지 검사하는 곳이 없었다.
            string source = ReadBootstrapSource();

            List<string> missing = new List<string>();
            foreach (InsectSeed seed in InsectExpansion2Definitions.CreateAll())
            {
                if (seed.rarity != InsectRarity.Epic && seed.rarity != InsectRarity.Legendary) continue;
                if (!source.Contains($"case \"{seed.id}\":")) missing.Add(seed.id);
            }

            CollectionAssert.IsEmpty(missing,
                "전용기 case가 없는 Epic/Legendary — default 이름으로 떨어진다: "
                + string.Join(", ", missing));
        }
    }
}
#endif
