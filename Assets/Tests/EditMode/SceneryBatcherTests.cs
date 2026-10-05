#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace InsectGame.Tests
{
    /// <summary>
    /// 장식 배처(<see cref="SceneryBatcher"/>)와 색표(<see cref="SceneryPalette"/>)의 순수 규칙.
    ///
    /// 셋 다 <b>틀려도 조용하다</b>. UV가 칸 경계에 걸리면 이웃 색이 번지고, 팔레트가 넘치면 색별 머티리얼로
    /// 떨어져 드로우콜만 늘고, 인덱스 폭이 모자라면 삼각형이 엉뚱한 정점을 이어 조각이 튄다 — 예외는 없다.
    /// 메시·오브젝트를 만드는 합치기 자체는 <c>FieldDesignTour</c> 캡처로 본다.
    /// </summary>
    [TestFixture]
    public class SceneryBatcherTests
    {
        private static Color Distinct(int i) => new Color(i / 1024f, (i % 7) / 7f, 0.5f, 1f);

        [Test]
        public void PaletteUV_EveryIndex_SamplesCenterOfItsOwnTexel()
        {
            // 점 필터라도 UV가 칸 경계에 있으면 반올림 방향에 따라 이웃 칸을 읽는다 — 칸 중앙이어야 한다
            for (int i = 0; i < SceneryPalette.Size; i++)
            {
                Vector2 uv = SceneryPalette.UV(i);
                float texel = uv.x * SceneryPalette.Size;
                Assert.AreEqual(i, Mathf.FloorToInt(texel), $"칸 {i}");
                Assert.AreEqual(0.5f, texel - i, 1e-3f, $"칸 {i}: 중앙이 아니다");
                Assert.AreEqual(0.5f, uv.y, 1e-6f);
            }
        }

        [Test]
        public void PaletteTryIndex_SameColorTwice_ReturnsSameSlot()
        {
            var palette = new SceneryPalette();
            Assert.IsTrue(palette.TryIndex(Distinct(0), out int a));
            Assert.IsTrue(palette.TryIndex(Distinct(1), out int b));
            Assert.IsTrue(palette.TryIndex(Distinct(0), out int again));
            Assert.AreEqual(0, a);
            Assert.AreEqual(1, b);
            Assert.AreEqual(a, again, "같은 색이 칸을 두 개 먹으면 256칸이 금방 찬다");
        }

        [Test]
        public void PaletteTryIndex_OverCapacity_FallsBackAndWarnsOnce()
        {
            var palette = new SceneryPalette();
            for (int i = 0; i < SceneryPalette.Size; i++)
            {
                Assert.IsTrue(palette.TryIndex(Distinct(i), out int slot), $"칸 {i}");
                Assert.AreEqual(i, slot);
            }

            // LogAssert(UnityEngine.TestTools)는 이 어셈블리에서 참조가 안 된다(asmdef 없음) — 로그 이벤트로 직접 센다
            int warnings = 0;
            void Count(string message, string stack, LogType type)
            {
                if (type == LogType.Warning && message.Contains("[SceneryPalette]")) warnings++;
            }
            Application.logMessageReceived += Count;
            try
            {
                Assert.IsFalse(palette.TryIndex(Distinct(SceneryPalette.Size), out int overflow), "257번째 색은 색별 머티리얼로 떨어져야 한다");
                Assert.AreEqual(-1, overflow);
                Assert.IsFalse(palette.TryIndex(Distinct(SceneryPalette.Size + 1), out _));
                Assert.IsFalse(palette.TryIndex(Distinct(SceneryPalette.Size), out _));

                // 이미 든 색은 넘친 뒤에도 제 칸을 계속 받는다 — 넘친 색만 폴백한다
                Assert.IsTrue(palette.TryIndex(Distinct(3), out int kept));
                Assert.AreEqual(3, kept);
            }
            finally
            {
                Application.logMessageReceived -= Count;
            }

            // 넘친 걸 알려야 하지만 한 번만 — 소품마다 부르므로 매번 찍으면 로그가 수천 줄이 된다
            Assert.AreEqual(1, warnings);
        }

        [Test]
        public void IndexFormatFor_Over65000Vertices_Uses32Bit()
        {
            Assert.AreEqual(IndexFormat.UInt16, SceneryBatcher.IndexFormatFor(0));
            Assert.AreEqual(IndexFormat.UInt16, SceneryBatcher.IndexFormatFor(65000));
            Assert.AreEqual(IndexFormat.UInt32, SceneryBatcher.IndexFormatFor(65001));
            Assert.AreEqual(IndexFormat.UInt32, SceneryBatcher.IndexFormatFor(200000));
        }

        // ── 색별 머티리얼 캐시 키 ──
        // 옛 캐시는 색만 키로 써서, 같은 색이 한 배처에선 발광·다른 배처에선 광택으로 지정되면 먼저 만든 쪽으로
        // 합쳐졌다(한쪽이 엉뚱하게 빛나거나 빛을 잃는다 — 예외 없음). 아래 머티리얼 테스트는 씬 없이 Material만 만든다.

        private static readonly Color Shared = new Color(0.40f, 0.60f, 0.80f, 1f);
        private static readonly Color Ember = new Color(1.2f, 0.5f, 0.1f, 1f);

        [Test]
        public void KeyFor_SameColorDifferentSurface_Differs()
        {
            var plain = new SceneryBatcher(8f, null);
            var glowA = new SceneryBatcher(8f, null);
            glowA.MarkGlow(Shared, Ember);
            var glowB = new SceneryBatcher(8f, null);
            glowB.MarkGlow(Shared, Ember);
            var glowDim = new SceneryBatcher(8f, null);
            glowDim.MarkGlow(Shared, Ember * 0.5f);
            var wet = new SceneryBatcher(8f, null);
            wet.MarkWet(Shared);

            Assert.AreNotEqual(plain.KeyFor(Shared), glowA.KeyFor(Shared), "무광과 발광이 한 머티리얼로 합쳐진다");
            Assert.AreNotEqual(glowA.KeyFor(Shared), wet.KeyFor(Shared), "발광과 젖은 광택이 한 머티리얼로 합쳐진다");
            Assert.AreNotEqual(plain.KeyFor(Shared), wet.KeyFor(Shared), "무광과 젖은 광택이 한 머티리얼로 합쳐진다");
            Assert.AreNotEqual(glowA.KeyFor(Shared), glowDim.KeyFor(Shared), "발광 세기가 다른데 먼저 만든 쪽으로 합쳐진다");
            // 같은 표면은 여전히 하나로 모여야 한다 — 갈라지면 머티리얼·드로우콜이 배처 수만큼 는다
            Assert.AreEqual(glowA.KeyFor(Shared), glowB.KeyFor(Shared));
            Assert.AreEqual(glowA.KeyFor(Shared).GetHashCode(), glowB.KeyFor(Shared).GetHashCode());
            Assert.AreEqual(SceneryMaterialKey.Matte(Shared), plain.KeyFor(Shared));
        }

        [Test]
        public void ColorMaterial_SharedCache_SameColorDifferentSurface_GetsDifferentMaterial()
        {
            var cache = new Dictionary<SceneryMaterialKey, Material>();
            var owned = new List<Material>();
            var glow = new SceneryBatcher(8f, null);
            glow.MarkGlow(Shared, Ember);
            var glowToo = new SceneryBatcher(8f, null);
            glowToo.MarkGlow(Shared, Ember);
            var wet = new SceneryBatcher(8f, null);
            wet.MarkWet(Shared);
            try
            {
                Material glowing = glow.ColorMaterial(Shared, owned, cache);
                Material wetOne = wet.ColorMaterial(Shared, owned, cache);
                Assert.AreNotSame(glowing, wetOne, "같은 색이라도 표면이 다르면 머티리얼이 달라야 한다");
                Assert.AreSame(glowing, glowToo.ColorMaterial(Shared, owned, cache), "같은 색·같은 표면은 머티리얼 하나를 함께 쓴다");
                Assert.AreEqual(2, owned.Count, "회수 목록에는 만든 것만 한 번씩 오른다");
                if (glowing.HasProperty("_EmissionColor"))
                {
                    Assert.IsTrue(glowing.IsKeywordEnabled("_EMISSION"));
                    Assert.IsFalse(wetOne.IsKeywordEnabled("_EMISSION"), "젖은 쪽이 발광 머티리얼을 받았다");
                }
            }
            finally
            {
                foreach (Material m in owned) if (m != null) Object.DestroyImmediate(m);
            }
        }

        [Test]
        public void ColorMaterial_SharingScopeIsTheDictionaryInstance()
        {
            // 공유 범위는 넘긴 사전 인스턴스다 — 리전 장식(RegionDressingBuilder)과 울타리 장식(RegionTerrainBuilder)은
            // 각자 사전을 들고 각자 회수한다. 다른 사전끼리 머티리얼이 섞이면 한쪽 OnDestroy가 다른 쪽이 쓰는 걸 파괴한다.
            var cacheA = new Dictionary<SceneryMaterialKey, Material>();
            var cacheB = new Dictionary<SceneryMaterialKey, Material>();
            var ownedA = new List<Material>();
            var ownedB = new List<Material>();
            var first = new SceneryBatcher(8f, null);
            var second = new SceneryBatcher(8f, null);
            try
            {
                Material a = first.ColorMaterial(Shared, ownedA, cacheA);
                Assert.AreSame(a, second.ColorMaterial(Shared, ownedA, cacheA), "같은 사전을 넘긴 배처끼리는 같은 색·표면을 하나로 모아야 한다");
                Material b = second.ColorMaterial(Shared, ownedB, cacheB);
                Assert.AreNotSame(a, b, "다른 사전을 넘긴 배처가 남의 머티리얼을 받았다");
                Assert.AreEqual(1, ownedA.Count);
                Assert.AreEqual(1, ownedB.Count, "만든 머티리얼은 그 사전 소유자의 회수 목록에만 오른다");
                Assert.AreSame(a, cacheA[SceneryMaterialKey.Matte(Shared)], "캐시 키가 표면 키(무광)가 아니다");

                // 소유자가 파괴한 뒤 다시 빌드하면(씬 재로드 전 재빌드 등) 파괴된 항목을 새로 만든다
                Object.DestroyImmediate(a);
                Material again = first.ColorMaterial(Shared, ownedA, cacheA);
                Assert.IsTrue(again != null, "파괴된 캐시 항목을 그대로 돌려줬다");
            }
            finally
            {
                foreach (Material m in ownedA) if (m != null) Object.DestroyImmediate(m);
                foreach (Material m in ownedB) if (m != null) Object.DestroyImmediate(m);
            }
        }

        // ── 머티리얼 생성·반투명 (빌더 다섯 곳이 공유) ──

        [Test]
        public void Create_OpaqueIsMatte_TranslucentKeepsShaderGloss()
        {
            Material opaque = SceneryMaterials.Create(new Color(0.5f, 0.4f, 0.3f, 1f));
            Material glass = SceneryMaterials.Create(new Color(0.6f, 0.8f, 1f, 0.3f));
            try
            {
                Assert.AreSame(SceneryMaterials.LitShader, opaque.shader, "셰이더 캐시와 다른 셰이더로 만들었다");
                Assert.AreSame(SceneryMaterials.LitShader, SceneryMaterials.LitShader, "셰이더를 매번 다시 찾는다");
                if (opaque.HasProperty("_Glossiness"))
                {
                    Assert.AreEqual(SceneryMaterials.MatteGloss, opaque.GetFloat("_Glossiness"), 1e-5f, "불투명 소품이 무광이 아니다");
                    Assert.AreNotEqual(SceneryMaterials.MatteGloss, glass.GetFloat("_Glossiness"), "반투명은 셰이더 기본 광택을 유지해야 한다");
                }
                Assert.IsFalse(SceneryMaterials.IsTranslucent(new Color(0.5f, 0.4f, 0.3f, 1f)));
                Assert.IsTrue(SceneryMaterials.IsTranslucent(new Color(0.6f, 0.8f, 1f, 0.3f)));
            }
            finally
            {
                Object.DestroyImmediate(opaque);
                Object.DestroyImmediate(glass);
            }
        }

        [Test]
        public void MakeFade_SetsAlphaBlendTransparentQueueAndNoDepthWrite()
        {
            // Standard는 기본 Opaque라 알파를 넣어도 무시된다 — 이 넷이 함께 서야 실제로 비친다
            Material mat = SceneryMaterials.Create(new Color(0.2f, 0.4f, 0.7f, 0.5f));
            try
            {
                SceneryMaterials.MakeFade(mat);
                Assert.AreEqual((int)RenderQueue.Transparent, mat.renderQueue);
                Assert.IsTrue(mat.IsKeywordEnabled("_ALPHABLEND_ON"));
                Assert.IsFalse(mat.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"));
                Assert.IsFalse(mat.IsKeywordEnabled("_ALPHATEST_ON"));
                if (mat.HasProperty("_SrcBlend"))
                {
                    // Standard는 이 셋을 Float 프로퍼티로 선언한다 — 정수로 넣어도 실수로 읽힌다
                    Assert.AreEqual((float)BlendMode.SrcAlpha, mat.GetFloat("_SrcBlend"));
                    Assert.AreEqual((float)BlendMode.OneMinusSrcAlpha, mat.GetFloat("_DstBlend"));
                    Assert.AreEqual(0f, mat.GetFloat("_ZWrite"));
                }
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }
        }
    }
}
#endif
