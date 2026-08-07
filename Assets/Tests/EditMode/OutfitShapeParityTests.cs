#if UNITY_EDITOR
using InsectGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 도구 형태를 <c>CharacterOutfitManager.ApplyToolShape</c>의 9분기 switch에서
    /// <see cref="OutfitShapeLibrary"/> 레시피로 옮긴 것이 <b>픽셀 단위로 같은 결과</b>인지 고정한다.
    ///
    /// 여기 적힌 좌표가 정답이다 — 옛 코드의 <c>hx=0.29f, hy=0.52f</c> 상대 표기를 전개한 절대값이라
    /// 레시피를 잘못 옮겨 적으면 이 테스트가 먼저 깨진다. 값을 "고치기" 전에 왜 달라졌는지 볼 것.
    /// </summary>
    [TestFixture]
    public class OutfitShapeParityTests
    {
        // ── 도구 9분기 × 2노드 = 18 골든 ──
        //
        // 총(gun/blaster/tranq)
        [TestCase("tool_tranq_gun", "NetHandle", PrimitiveType.Cube, 0.29f, 0.52f, 0.18f, 0.08f, 0.05f, 0.22f, 0f, 0f, 0f)]
        [TestCase("tool_tranq_gun", "NetRing", PrimitiveType.Cylinder, 0.29f, 0.52f, 0.32f, 0.06f, 0.06f, 0.04f, 90f, 0f, 0f)]
        // 마법 지팡이
        [TestCase("tool_wand", "NetHandle", PrimitiveType.Cylinder, 0.29f, 0.70f, 0.05f, 0.03f, 0.40f, 0.03f, 10f, 0f, -15f)]
        [TestCase("tool_wand", "NetRing", PrimitiveType.Sphere, 0.37f, 1.10f, 0.05f, 0.10f, 0.10f, 0.10f, 0f, 0f, 0f)]
        // 올가미 — 고리의 X축 -20°는 edge-on collapse 방지값이라 특히 중요하다
        [TestCase("tool_lasso", "NetHandle", PrimitiveType.Cylinder, 0.29f, 0.65f, 0f, 0.04f, 0.25f, 0.04f, 20f, 0f, -12f)]
        [TestCase("tool_lasso", "NetRing", PrimitiveType.Cylinder, 0.35f, 0.94f, 0.06f, 0.28f, 0.02f, 0.28f, -20f, 0f, 0f)]
        // 수리검
        [TestCase("tool_shuriken", "NetHandle", PrimitiveType.Cube, 0.29f, 0.52f, 0.10f, 0.18f, 0.02f, 0.05f, 0f, 45f, 0f)]
        [TestCase("tool_shuriken", "NetRing", PrimitiveType.Cube, 0.29f, 0.52f, 0.10f, 0.05f, 0.02f, 0.18f, 0f, 45f, 0f)]
        // 해적 곡도
        [TestCase("tool_cutlass", "NetHandle", PrimitiveType.Cube, 0.29f, 0.58f, 0.05f, 0.05f, 0.10f, 0.05f, 0f, 0f, 0f)]
        [TestCase("tool_cutlass", "NetRing", PrimitiveType.Cube, 0.29f, 0.84f, 0.05f, 0.04f, 0.40f, 0.10f, 0f, 0f, 0f)]
        // 거미줄 발사기
        [TestCase("tool_web_shooter", "NetHandle", PrimitiveType.Cube, 0.29f, 0.60f, 0.05f, 0.08f, 0.06f, 0.12f, 0f, 0f, 0f)]
        [TestCase("tool_web_shooter", "NetRing", PrimitiveType.Sphere, 0.29f, 0.60f, 0.15f, 0.04f, 0.04f, 0.04f, 0f, 0f, 0f)]
        // 돋보기 — 렌즈 X축 -20° 동일 이유
        [TestCase("tool_magnify", "NetHandle", PrimitiveType.Cylinder, 0.29f, 0.57f, 0.10f, 0.03f, 0.18f, 0.03f, 35f, 0f, 0f)]
        [TestCase("tool_magnify", "NetRing", PrimitiveType.Cylinder, 0.29f, 0.74f, 0.20f, 0.16f, 0.02f, 0.16f, -20f, 0f, 0f)]
        // 관찰 카메라
        [TestCase("tool_camera", "NetHandle", PrimitiveType.Cube, 0.29f, 0.57f, 0.18f, 0.16f, 0.10f, 0.10f, 0f, 0f, 0f)]
        [TestCase("tool_camera", "NetRing", PrimitiveType.Cylinder, 0.29f, 0.57f, 0.26f, 0.07f, 0.07f, 0.06f, 90f, 0f, 0f)]
        // 기본 잠자리채(else) — PlayerVisualBuilder의 NetHandle/NetRing 초기 좌표와도 일치해야 한다
        [TestCase("tool_net", "NetHandle", PrimitiveType.Cylinder, 0.29f, 0.74f, 0.02f, 0.04f, 0.40f, 0.04f, 20f, 0f, -15f)]
        [TestCase("tool_net", "NetRing", PrimitiveType.Cylinder, 0.34f, 1.14f, 0.06f, 0.20f, 0.02f, 0.20f, -20f, 0f, 0f)]
        public void ToolRecipe_Branch_MatchesLegacyTransform(
            string itemId, string bindName, PrimitiveType prim,
            float px, float py, float pz, float sx, float sy, float sz, float ex, float ey, float ez)
        {
            OutfitRecipe recipe = OutfitShapeLibrary.ResolveTool(itemId);
            Assert.IsNotNull(recipe, $"{itemId}의 도구 레시피가 없다 — 기본 잠자리채(else)조차 못 찾았다는 뜻");

            Assert.IsTrue(OutfitShapeLibrary.TryGetBoundPart(recipe, bindName, out OutfitPart p),
                $"{itemId} 레시피에 {bindName} bind 파츠가 없다");

            Assert.AreEqual(prim, p.prim, $"{itemId}/{bindName} 메시 종류");
            AssertV3(new Vector3(px, py, pz), p.pos, $"{itemId}/{bindName} 위치");
            AssertV3(new Vector3(sx, sy, sz), p.scale, $"{itemId}/{bindName} 크기");
            AssertV3(new Vector3(ex, ey, ez), p.euler, $"{itemId}/{bindName} 회전");
        }

        // ── 분기 선택 순서 ──
        //
        // 옛 else-if 체인과 같은 분기를 고르는지. 레시피 객체를 참조 비교해 "같은 분기"를 판정한다.

        [TestCase("tool_golden_net", "tool_net", TestName = "황금 잠자리채는 기본 잠자리채와 같은 형태")]
        [TestCase("tool_diamond_net", "tool_net", TestName = "다이아 잠자리채는 기본 잠자리채와 같은 형태")]
        [TestCase("tool_none", "tool_net", TestName = "도구 없음도 else 분기")]
        [TestCase("tool_blaster", "tool_tranq_gun", TestName = "블래스터는 총 분기")]
        public void ResolveTool_SameBranch_ReturnsSameRecipe(string a, string b)
        {
            Assert.AreSame(OutfitShapeLibrary.ResolveTool(b), OutfitShapeLibrary.ResolveTool(a));
        }

        /// <summary>
        /// tool_laser는 옛 체인에 분기가 없어 else(잠자리채)로 떨어져 "레이저 포인터인데 잠자리채"였다.
        /// 신규 분기이므로 <b>기본 잠자리채와 달라야</b> 한다 — 파리티가 아니라 의도된 변경이다.
        /// </summary>
        [Test]
        public void ResolveTool_Laser_IsNotTheDefaultNet()
        {
            Assert.AreNotSame(
                OutfitShapeLibrary.ResolveTool("tool_net"),
                OutfitShapeLibrary.ResolveTool("tool_laser"),
                "레이저 포인터가 기본 잠자리채 형태로 떨어지고 있다");
        }

        /// <summary>
        /// 앞 분기가 뒤 분기를 가려 도달 불가능한 항목이 없는지. 카탈로그의 실제 도구 id로만 판정한다 —
        /// 어떤 아이템도 고르지 못하는 분기는 곧 죽은 코드다(2D 카드의 hat_beanie가 그랬다).
        /// </summary>
        [Test]
        public void ToolEntries_EveryBranch_IsReachableByARealItem()
        {
            OutfitItem[] catalog = CharacterOutfitManager.BuildCatalog();
            OutfitShapeLibrary.ToolEntry[] entries = OutfitShapeLibrary.ToolEntries;

            for (int i = 0; i < entries.Length; i++)
            {
                bool reached = false;
                for (int c = 0; c < catalog.Length && !reached; c++)
                {
                    if (catalog[c].slot != OutfitSlot.Tool) continue;
                    if (ReferenceEquals(OutfitShapeLibrary.ResolveTool(catalog[c].itemId), entries[i].recipe))
                        reached = true;
                }

                string keys = entries[i].keys == null ? "(기본/else)" : string.Join(",", entries[i].keys);
                Assert.IsTrue(reached,
                    $"도구 레시피 [{i}] {keys} 를 고르는 아이템이 카탈로그에 없다 — 앞 분기에 가려졌거나 오타다");
            }
        }

        private static void AssertV3(Vector3 expected, Vector3 actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, 0.0001f, what + " x");
            Assert.AreEqual(expected.y, actual.y, 0.0001f, what + " y");
            Assert.AreEqual(expected.z, actual.z, 0.0001f, what + " z");
        }
    }
}
#endif
