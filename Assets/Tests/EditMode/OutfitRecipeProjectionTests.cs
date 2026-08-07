#if UNITY_EDITOR
using InsectGame.Core;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 3D 파츠 레시피를 2D 카드 아이콘으로 옮기는 정사영 계산부.
    /// 실제 드로잉(GUI.DrawTexture)은 `rules/testing.md`상 테스트 제외이고, 여기서는 좌표만 본다.
    /// </summary>
    [TestFixture]
    public class OutfitRecipeProjectionTests
    {
        private static OutfitPart Part(Vector3 scale, Vector3 euler)
        {
            return new OutfitPart { prim = PrimitiveType.Cube, pos = Vector3.zero, scale = scale, euler = euler };
        }

        [Test]
        public void ProjectedSize_NoRotation_EqualsScaleXY()
        {
            Vector2 s = CharacterPortraitRenderer.ProjectedSize(Part(new Vector3(0.3f, 0.7f, 0.2f), Vector3.zero));

            Assert.AreEqual(0.3f, s.x, 0.0001f);
            Assert.AreEqual(0.7f, s.y, 0.0001f);
        }

        /// <summary>
        /// 잠자리채 망: 두께 0.02짜리 디스크를 X축 -20°로 눕힌 것. 두께만 보면 화면 높이가 0.02라
        /// 실선으로 사라진다. 눕은 만큼 지름(Z)이 화면으로 넘어와야 타원으로 보인다 —
        /// 3D에서 edge-on collapse를 막은 것과 같은 문제를 2D에서도 처리한다.
        /// </summary>
        [Test]
        public void ProjectedSize_TiltedDisc_BorrowsDepthAsHeight()
        {
            OutfitPart ring = Part(new Vector3(0.20f, 0.02f, 0.20f), new Vector3(-20f, 0f, 0f));

            Vector2 s = CharacterPortraitRenderer.ProjectedSize(ring);

            Assert.AreEqual(0.20f, s.x, 0.0001f, "가로는 그대로");
            Assert.Greater(s.y, 0.05f, "눕힌 디스크가 실선으로 무너졌다");
            Assert.AreEqual(0.0188f + 0.0684f, s.y, 0.002f);
        }

        [Test]
        public void ProjectedSize_YawedBox_BorrowsDepthAsWidth()
        {
            Vector2 s = CharacterPortraitRenderer.ProjectedSize(
                Part(new Vector3(0.30f, 0.10f, 0.40f), new Vector3(0f, 90f, 0f)));

            Assert.AreEqual(0.40f, s.x, 0.001f, "90° 돌면 깊이가 그대로 가로가 된다");
            Assert.AreEqual(0.10f, s.y, 0.001f);
        }

        [Test]
        public void RotatedExtent_ZeroRotation_IsHalfOfProjectedSize()
        {
            OutfitPart p = Part(new Vector3(0.4f, 0.2f, 0f), Vector3.zero);

            Vector2 e = CharacterPortraitRenderer.RotatedExtent(p);

            Assert.AreEqual(0.2f, e.x, 0.0001f);
            Assert.AreEqual(0.1f, e.y, 0.0001f);
        }

        [Test]
        public void RotatedExtent_45Degrees_GrowsBothAxes()
        {
            OutfitPart p = Part(new Vector3(0.4f, 0.1f, 0f), new Vector3(0f, 0f, 45f));

            Vector2 e = CharacterPortraitRenderer.RotatedExtent(p);

            Assert.Greater(e.y, 0.05f, "기울면 세로 AABB가 커진다");
            Assert.AreEqual((0.4f + 0.1f) * 0.5f * Mathf.Sqrt(0.5f), e.x, 0.001f);
        }

        [Test]
        public void RecipeBounds_EncompassesEveryPart()
        {
            OutfitPart[] parts =
            {
                new OutfitPart { prim = PrimitiveType.Cube, pos = new Vector3(0f, 0.5f, 0f),  scale = new Vector3(0.2f, 0.2f, 0.2f) },
                new OutfitPart { prim = PrimitiveType.Cube, pos = new Vector3(-0.4f, 0f, 0f), scale = new Vector3(0.2f, 0.2f, 0.2f) },
            };

            Rect b = CharacterPortraitRenderer.RecipeBounds(parts);

            Assert.AreEqual(-0.5f, b.xMin, 0.0001f);
            Assert.AreEqual(0.1f, b.xMax, 0.0001f);
            Assert.AreEqual(-0.1f, b.yMin, 0.0001f);
            Assert.AreEqual(0.6f, b.yMax, 0.0001f);
        }

        /// <summary>0으로 나누는 프레이밍을 막는다 — 폭 0인 바운드가 나오면 스케일이 무한이 된다.</summary>
        [Test]
        public void RecipeBounds_DegenerateInput_StaysPositive()
        {
            Assert.Greater(CharacterPortraitRenderer.RecipeBounds(null).width, 0f);
            Assert.Greater(CharacterPortraitRenderer.RecipeBounds(new OutfitPart[0]).height, 0f);

            OutfitPart[] flat = { new OutfitPart { prim = PrimitiveType.Cube, pos = Vector3.zero, scale = Vector3.zero } };
            Rect b = CharacterPortraitRenderer.RecipeBounds(flat);
            Assert.Greater(b.width, 0f);
            Assert.Greater(b.height, 0f);
        }

        [Test]
        public void RecipeRoundness_CubeIsAngular_SphereIsRound()
        {
            OutfitPart cube = new OutfitPart { prim = PrimitiveType.Cube, scale = Vector3.one };
            OutfitPart sphere = new OutfitPart { prim = PrimitiveType.Sphere, scale = Vector3.one };
            OutfitPart disc = new OutfitPart { prim = PrimitiveType.Cylinder, scale = new Vector3(0.2f, 0.02f, 0.2f) };
            OutfitPart rod = new OutfitPart { prim = PrimitiveType.Cylinder, scale = new Vector3(0.04f, 0.4f, 0.04f) };

            Assert.Less(CharacterPortraitRenderer.RecipeRoundness(cube), 0.3f);
            Assert.AreEqual(1f, CharacterPortraitRenderer.RecipeRoundness(sphere), 0.0001f);
            Assert.AreEqual(1f, CharacterPortraitRenderer.RecipeRoundness(disc), 0.0001f, "눕힌 디스크는 타원");
            Assert.Less(CharacterPortraitRenderer.RecipeRoundness(rod), 1f, "세운 원통은 완전한 타원이 아니다");
        }
    }
}
#endif
