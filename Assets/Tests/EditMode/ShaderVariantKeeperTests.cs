#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using InsectGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 런타임에 켜는 Standard 키워드 조합이 전부 빌드에 남는가(<see cref="SceneryMaterials.BuildKeepers"/>).
    /// 빠지면 빌드는 성공하는데 기기에서만 물·얼음이 불투명, 등불이 무발광으로 그려진다 — 에디터로는 절대 안 보인다.
    /// </summary>
    [TestFixture]
    public class ShaderVariantKeeperTests
    {
        // 변형 선택에 쓰이는 Standard shader_feature 중 이 저장소가 런타임에 만지는 것.
        private static readonly string[] Tracked = { "_ALPHABLEND_ON", "_ALPHATEST_ON", "_ALPHAPREMULTIPLY_ON", "_EMISSION" };

        private readonly List<Material> made = new List<Material>();

        [TearDown]
        public void TearDown()
        {
            foreach (Material m in made) Object.DestroyImmediate(m);
            made.Clear();
        }

        [Test]
        public void BuildKeepers_CoverEveryKeywordComboTheSceneryPathsEnable()
        {
            Color opaque = new Color(0.4f, 0.5f, 0.3f);
            Color glass = new Color(0.6f, 0.8f, 0.9f, 0.4f);
            Color glow = new Color(1f, 0.8f, 0.3f);

            Material fade = Track(SceneryMaterials.Create(glass));
            SceneryMaterials.MakeFade(fade);
            Material emission = Track(SceneryMaterials.Create(opaque));
            SceneryMaterials.SetEmission(emission, glow);
            Material both = Track(SceneryMaterials.Create(glass));   // 연기 덩이 발광(SubAreaGateBuilder)의 순서
            SceneryMaterials.SetEmission(both, glow);
            SceneryMaterials.MakeFade(both);
            Material plain = Track(SceneryMaterials.Create(opaque));

            if (plain.shader.name != "Standard") Assert.Ignore("Standard 없음 — 폴백 셰이더에선 키워드가 의미 없다");

            var keeperSets = SceneryMaterials.BuildKeepers.Select(k => Key(k.keywords)).ToList();
            foreach (Material m in new[] { fade, emission, both })
            {
                string combo = Key(Tracked.Where(k => m.IsKeywordEnabled(k)));
                Assert.IsTrue(keeperSets.Contains(combo), $"런타임 조합 '{combo}'가 BuildKeepers에 없다 — 빌드에서 빠진다");
            }
            Assert.AreEqual(string.Empty, Key(Tracked.Where(k => plain.IsKeywordEnabled(k))), "불투명 기본은 키워드가 없어야 한다");
        }

        [Test]
        public void BuildKeepers_ExistInResources_WithExactlyTheirKeywords()
        {
            foreach ((string name, string[] keywords) in SceneryMaterials.BuildKeepers)
            {
                Material m = Resources.Load<Material>(SceneryMaterials.BuildKeeperFolder + "/" + name);
                Assert.IsNotNull(m, $"Resources/{SceneryMaterials.BuildKeeperFolder}/{name}.mat 없음 — " +
                                    "InsectGame.EditorTools.ShaderVariantKeepers.Ensure로 만든다");
                Assert.AreEqual("Standard", m.shader.name, name);
                Assert.AreEqual(Key(keywords), Key(Tracked.Where(k => m.IsKeywordEnabled(k))), $"{name}: 키워드");
            }
        }

        private Material Track(Material m)
        {
            made.Add(m);
            return m;
        }

        private static string Key(IEnumerable<string> keywords) => string.Join("+", keywords.OrderBy(k => k));
    }
}
#endif
