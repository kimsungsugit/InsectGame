#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Core;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 의상 spawn 파츠의 재질(광택) 판정 — <see cref="OutfitShapeLibrary.SurfaceOf"/>.
    ///
    /// 예외 표는 itemId 문자열과 색 역할로 적혀 있어서 틀려도 조용하다: 키에 오타가 나거나 레시피가 바뀌어
    /// 예외 역할이 사라지면 그 아이템은 예외도 경고도 없이 천(무광)으로 그려진다. 그 두 갈래를 여기서 막는다.
    /// </summary>
    [TestFixture]
    public class OutfitPartSurfaceTests
    {
        private static Dictionary<string, OutfitSlot> CatalogSlots()
        {
            Dictionary<string, OutfitSlot> map = new Dictionary<string, OutfitSlot>();
            OutfitItem[] catalog = CharacterOutfitManager.BuildCatalog();
            for (int i = 0; i < catalog.Length; i++) map[catalog[i].itemId] = catalog[i].slot;
            return map;
        }

        private static OutfitRecipe RecipeOf(string itemId)
        {
            Dictionary<string, OutfitSlot> slots = CatalogSlots();
            Assert.IsTrue(slots.TryGetValue(itemId, out OutfitSlot slot), itemId + "가 카탈로그에 없다");
            Assert.IsTrue(OutfitShapeLibrary.TryGet(slot, itemId, out OutfitRecipe recipe), itemId + " 레시피가 없다");
            return recipe;
        }

        // ── 예외 표 정합 ──

        [Test]
        public void SurfaceOverrides_EveryKey_IsExactRecipe()
        {
            HashSet<string> ids = new HashSet<string>(OutfitShapeLibrary.ExactRecipeIds());
            foreach (KeyValuePair<string, OutfitShapeLibrary.ItemSurface> e in OutfitShapeLibrary.SurfaceOverrideEntries())
                Assert.IsTrue(ids.Contains(e.Key), $"재질 예외 '{e.Key}'에 해당하는 레시피가 없다 — 오타면 조용히 천으로 그려진다");
        }

        /// <summary>
        /// 예외 역할이 레시피의 spawn 파츠에 실제로 있어야 하고, 기본 재질이 적용될 파츠도 남아 있어야 한다.
        /// 둘 중 하나가 비면 표가 말하는 것과 화면이 다르다(예: 왕관 보석을 기본색으로 바꾸면 보석이 금속이 된다).
        /// </summary>
        [Test]
        public void SurfaceOverrides_ExceptRole_SplitsRealSpawnParts()
        {
            foreach (KeyValuePair<string, OutfitShapeLibrary.ItemSurface> e in OutfitShapeLibrary.SurfaceOverrideEntries())
            {
                if (!e.Value.HasExcept) continue;

                OutfitRecipe recipe = RecipeOf(e.Key);
                bool exceptSeen = false, baseSeen = false;
                for (int i = 0; i < recipe.parts.Length; i++)
                {
                    if (recipe.parts[i].IsBind) continue;
                    if (recipe.parts[i].role == e.Value.ExceptRole) exceptSeen = true;
                    else baseSeen = true;
                }

                Assert.IsTrue(exceptSeen, $"{e.Key}: 예외 역할 {e.Value.ExceptRole}을 쓰는 spawn 파츠가 없다");
                Assert.IsTrue(baseSeen, $"{e.Key}: 기본 재질 {e.Value.Kind}이 적용될 파츠가 없다 — 예외만 남았다");
                Assert.AreNotEqual(e.Value.Kind, e.Value.ExceptKind, $"{e.Key}: 예외가 기본과 같은 재질이다");
            }
        }

        /// <summary>
        /// 역참조가 레시피마다 자기 id를 돌려줘야 한다. 두 아이템이 레시피 인스턴스를 나누면
        /// 한쪽의 재질 예외가 다른 쪽에 조용히 번진다.
        /// </summary>
        [Test]
        public void ItemIdOf_EveryExactRecipe_RoundTrips()
        {
            Dictionary<string, OutfitSlot> slots = CatalogSlots();
            foreach (string id in OutfitShapeLibrary.ExactRecipeIds())
            {
                OutfitSlot slot = slots.TryGetValue(id, out OutfitSlot s) ? s : OutfitSlot.Hat;
                Assert.IsTrue(OutfitShapeLibrary.TryGet(slot, id, out OutfitRecipe recipe), id);
                Assert.AreEqual(id, OutfitShapeLibrary.ItemIdOf(recipe), $"{id}의 레시피가 다른 id로 역참조된다");
            }
            Assert.IsNull(OutfitShapeLibrary.ItemIdOf(null));
        }

        // ── 판정 ──

        /// <summary>표에 없는 아이템은 슬롯 기본 — bind 노드(Cap=천, Backpack=가죽)와 같은 재질이다.</summary>
        [Test]
        public void SurfaceOf_UnlistedItem_FollowsSlotDefault()
        {
            Assert.AreEqual(SurfaceKind.Cloth, OutfitShapeLibrary.SurfaceOf(OutfitSlot.Hat, "hat_straw", PartColorRole.Primary));
            Assert.AreEqual(SurfaceKind.Cloth, OutfitShapeLibrary.SurfaceOf(OutfitSlot.Outerwear, "outer_legendary", PartColorRole.Secondary));
            Assert.AreEqual(SurfaceKind.Cloth, OutfitShapeLibrary.SurfaceOf(OutfitSlot.Accessory, "acc_scarf", PartColorRole.Primary));
            Assert.AreEqual(SurfaceKind.Leather, OutfitShapeLibrary.SurfaceOf(OutfitSlot.Backpack, "bag_dragon", PartColorRole.Secondary));
            Assert.AreEqual(SurfaceKind.Cloth, OutfitShapeLibrary.SurfaceOf(OutfitSlot.Hat, null, PartColorRole.Primary));
        }

        /// <summary>꽃 왕관의 금색 꽃술은 고정색(Gold)이지만 꽃이다 — 금색이라고 금속으로 가르지 않는다.</summary>
        [Test]
        public void SurfaceOf_FlowerCrownGoldStamen_StaysCloth()
        {
            Assert.AreEqual(SurfaceKind.Cloth, OutfitShapeLibrary.SurfaceOf(OutfitSlot.Hat, "hat_flower", PartColorRole.Fixed));
        }

        [Test]
        public void SurfaceOf_Crown_GoldIsMetal_GemIsGlossy()
        {
            Assert.AreEqual(SurfaceKind.Metal, OutfitShapeLibrary.SurfaceOf(OutfitSlot.Hat, "hat_crown", PartColorRole.Primary));
            Assert.AreEqual(SurfaceKind.Metal, OutfitShapeLibrary.SurfaceOf(OutfitSlot.Hat, "hat_crown", PartColorRole.Fixed));
            Assert.AreEqual(SurfaceKind.Wet, OutfitShapeLibrary.SurfaceOf(OutfitSlot.Hat, "hat_crown", PartColorRole.Secondary));
        }

        [Test]
        public void SurfaceOf_ScienceCase_ClaspsAreMetal_HandleIsLeather()
        {
            Assert.AreEqual(SurfaceKind.Metal, OutfitShapeLibrary.SurfaceOf(OutfitSlot.Backpack, "bag_science", PartColorRole.Secondary));
            Assert.AreEqual(SurfaceKind.Leather, OutfitShapeLibrary.SurfaceOf(OutfitSlot.Backpack, "bag_science", PartColorRole.PrimaryDark));
        }

        /// <summary>
        /// 슬롯 기본은 무광 계열이어야 한다 — 광택 통일의 요지다. 금속·유리 광택은 예외 표에서만 나온다.
        /// </summary>
        [Test]
        public void DefaultSurface_EverySlot_IsNonMetallicAndNotGlossierThanLeather()
        {
            CharacterPalette.SurfaceValues(SurfaceKind.Leather, out float leatherGloss, out _);
            foreach (OutfitSlot slot in System.Enum.GetValues(typeof(OutfitSlot)))
            {
                CharacterPalette.SurfaceValues(OutfitShapeLibrary.DefaultSurface(slot), out float gloss, out float metallic);
                Assert.AreEqual(0f, metallic, slot + ": 슬롯 기본이 금속이다");
                Assert.LessOrEqual(gloss, leatherGloss, slot + ": 슬롯 기본이 가죽보다 반짝인다");
            }
        }
    }
}
#endif
