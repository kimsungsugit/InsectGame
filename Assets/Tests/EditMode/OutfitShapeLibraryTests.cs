#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// <see cref="OutfitShapeLibrary"/>가 지켜야 할 불변식들. 형태 정의가 3벌로 갈라져 있던 시절의
    /// 결함(존재하지 않는 itemId 분기, 카드와 캐릭터의 모양 불일치)이 다시 생기지 않게 구조로 막는다.
    /// </summary>
    [TestFixture]
    public class OutfitShapeLibraryTests
    {
        /// <summary>PlayerVisualBuilder가 만드는 노드 중 레시피가 bind해도 되는 이름.</summary>
        private static readonly HashSet<string> KnownNodes = new HashSet<string>
        {
            "NetHandle", "NetRing",
            "Cap", "CapBrim",
            "Backpack", "BackpackStrap",
            "AccGlassesL", "AccGlassesR", "AccNecklace", "AccBadge",
        };

        private static Dictionary<string, OutfitSlot> CatalogSlots()
        {
            Dictionary<string, OutfitSlot> map = new Dictionary<string, OutfitSlot>();
            OutfitItem[] catalog = CharacterOutfitManager.BuildCatalog();
            for (int i = 0; i < catalog.Length; i++) map[catalog[i].itemId] = catalog[i].slot;
            return map;
        }

        private static List<OutfitRecipe> RecipesForSlot(OutfitSlot slot)
        {
            Dictionary<string, OutfitSlot> slots = CatalogSlots();
            List<OutfitRecipe> list = new List<OutfitRecipe>();
            foreach (string id in OutfitShapeLibrary.ExactRecipeIds())
            {
                if (slots.TryGetValue(id, out OutfitSlot s) && s == slot
                    && OutfitShapeLibrary.TryGet(slot, id, out OutfitRecipe r))
                    list.Add(r);
            }
            return list;
        }

        // ── 카탈로그 정합 ──

        /// <summary>
        /// 레시피가 물고 있는 itemId가 전부 실재해야 한다. 옛 2D 카드에는 카탈로그에 없는
        /// "hat_beanie" 분기가 두 곳에 죽은 채로 남아 있었다 — 그 형태를 구조적으로 차단한다.
        /// </summary>
        /// <summary>
        /// 스폰 컨테이너 이름은 미리 구워 둔다 — <c>SpawnPrefix + slot</c>은 enum을 문자열로 바꾸며
        /// 할당이 나는데 <c>Apply</c>가 슬롯마다 불리고, 그 호출부에 프리뷰 렌더러가 있다.
        /// 굽는 과정이 조용히 어긋나면(새 슬롯 추가 등) 컨테이너를 못 찾아 파츠가 매번 새로 생긴다.
        /// </summary>
        [Test]
        public void SpawnContainerName_MatchesLiteralConcatForEverySlot()
        {
            foreach (OutfitSlot slot in System.Enum.GetValues(typeof(OutfitSlot)))
            {
                Assert.AreEqual(
                    OutfitShapeLibrary.SpawnPrefix + slot,
                    OutfitShapeLibrary.SpawnContainerName(slot),
                    $"{slot} 컨테이너 이름");
            }
        }

        [Test]
        public void SpawnContainerName_SameSlotTwice_ReturnsSameInstance()
        {
            // 같은 인스턴스여야 캐시가 실제로 동작하는 것이다(매번 새 문자열이면 최적화가 사라진다).
            Assert.AreSame(
                OutfitShapeLibrary.SpawnContainerName(OutfitSlot.Hat),
                OutfitShapeLibrary.SpawnContainerName(OutfitSlot.Hat));
        }

        [Test]
        public void ExactRecipeIds_AllExistInCatalog()
        {
            Dictionary<string, OutfitSlot> slots = CatalogSlots();
            foreach (string id in OutfitShapeLibrary.ExactRecipeIds())
            {
                Assert.IsTrue(slots.ContainsKey(id),
                    $"레시피 '{id}'에 대응하는 의상이 카탈로그에 없다 — 오타거나 삭제된 아이템이다");
            }
        }

        /// <summary>
        /// exact 레시피 조회는 슬롯을 보지 않고 itemId만 쓴다. itemId가 슬롯을 넘어 중복되면
        /// 상의에 모자 레시피가 붙을 수 있으므로, 전역 유일성이 그 설계의 전제다.
        /// </summary>
        [Test]
        public void CatalogItemIds_AreGloballyUnique()
        {
            OutfitItem[] catalog = CharacterOutfitManager.BuildCatalog();
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < catalog.Length; i++)
            {
                Assert.IsTrue(seen.Add(catalog[i].itemId), $"itemId 중복: {catalog[i].itemId}");
            }
        }

        [Test]
        public void TryGet_UnknownItemId_ReturnsFalse()
        {
            Assert.IsFalse(OutfitShapeLibrary.TryGet(OutfitSlot.Hat, "hat_does_not_exist", out _),
                "모르는 id는 false여야 호출부가 기존 색-only 경로로 폴백한다");
            Assert.IsFalse(OutfitShapeLibrary.TryGet(OutfitSlot.Top, "", out _));
            Assert.IsFalse(OutfitShapeLibrary.TryGet(OutfitSlot.Top, null, out _));
        }

        /// <summary>도구는 기본 잠자리채가 else 역할을 하므로 어떤 id를 넣어도 반드시 잡힌다.</summary>
        [Test]
        public void TryGet_EveryCatalogTool_Resolves()
        {
            OutfitItem[] catalog = CharacterOutfitManager.BuildCatalog();
            for (int i = 0; i < catalog.Length; i++)
            {
                if (catalog[i].slot != OutfitSlot.Tool) continue;
                Assert.IsTrue(OutfitShapeLibrary.TryGet(OutfitSlot.Tool, catalog[i].itemId, out OutfitRecipe r),
                    $"{catalog[i].itemId} 레시피 미해결");
                Assert.IsNotNull(r);
            }
        }

        /// <summary>
        /// 악세서리는 레시피가 형태의 <b>유일한</b> 경로다 — PlayerVisualBuilder의 4노드 프리셋
        /// (AccGlassesL/R·AccNecklace·AccBadge)을 걷어냈기 때문에, 레시피가 없는 악세서리는
        /// 장착해도 아무것도 안 보인다. acc_none(알파 0)만 예외다.
        /// </summary>
        [Test]
        public void EveryAccessory_ExceptNone_HasRecipe()
        {
            OutfitItem[] catalog = CharacterOutfitManager.BuildCatalog();
            for (int i = 0; i < catalog.Length; i++)
            {
                if (catalog[i].slot != OutfitSlot.Accessory) continue;
                if (catalog[i].primaryColor.a < 0.01f) continue;   // acc_none

                Assert.IsTrue(OutfitShapeLibrary.TryGet(OutfitSlot.Accessory, catalog[i].itemId, out _),
                    $"{catalog[i].itemId}({catalog[i].displayName})에 레시피가 없다 — 장착해도 아무것도 안 보인다");
            }
        }

        // ── bind / spawn 불변식 ──

        /// <summary>
        /// 도구는 <b>bind만</b> 쓴다. PlayerMovement가 NetHandle/NetRing을 transform.Find로 캐싱해
        /// 스윙 애니메이션을 돌리므로(PlayerMovement.cs의 cachedNetHandle/cachedNetRing),
        /// spawn으로 바꿔 파괴·재생성하면 캐시가 죽은 Transform을 가리켜 도구 스윙이 멈춘다.
        /// </summary>
        [Test]
        public void ToolRecipes_UseBindModeOnly()
        {
            OutfitShapeLibrary.ToolEntry[] entries = OutfitShapeLibrary.ToolEntries;
            for (int i = 0; i < entries.Length; i++)
            {
                OutfitPart[] parts = entries[i].recipe.parts;
                for (int p = 0; p < parts.Length; p++)
                {
                    Assert.IsTrue(parts[p].IsBind,
                        $"도구 레시피 [{i}]의 파츠 {p}가 spawn이다 — 도구 스윙이 죽는다");
                }
            }
        }

        /// <summary>
        /// 모자는 <b>spawn만</b> 쓴다. bind는 기존 노드의 mesh를 갈아끼우는데, 모자는 레시피 커버리지가
        /// 부분적이라 레시피 없는 모자로 갈아입으면 색-only 폴백이 mesh를 되돌리지 못한다
        /// (왕관을 쓴 뒤 탐험가 캡을 쓰면 캡이 왕관 모양으로 남는다).
        /// </summary>
        [Test]
        public void HatRecipes_UseSpawnModeOnly()
        {
            foreach (OutfitRecipe r in RecipesForSlot(OutfitSlot.Hat))
            {
                for (int p = 0; p < r.parts.Length; p++)
                {
                    Assert.IsFalse(r.parts[p].IsBind,
                        $"모자 레시피가 '{r.parts[p].bindName}'을 bind한다 — 벗을 때 mesh가 안 돌아온다");
                }
            }
        }

        /// <summary>
        /// 모자 레시피는 기본 캡(Cap/CapBrim)을 반드시 숨긴다. 안 숨기면 왕관 속에 야구모자가 겹쳐 보인다.
        /// </summary>
        [Test]
        public void HatRecipes_HideBaseCapNodes()
        {
            foreach (OutfitRecipe r in RecipesForSlot(OutfitSlot.Hat))
            {
                Assert.IsNotNull(r.hideNodes, "모자 레시피에 hideNodes가 없다");
                CollectionAssert.Contains(r.hideNodes, "Cap");
                CollectionAssert.Contains(r.hideNodes, "CapBrim");
            }
        }

        /// <summary>
        /// 정수리가 **열린** 모자(머리띠·마스크·바이저·꽃관). 여기 없는 모자 레시피는 전부 올림머리(HairCrown)를
        /// 숨겨야 한다 — 안 숨기면 스파이크·번이 헬멧·챙을 뚫고 나온다. 새 모자를 만들면 둘 중 하나를 **고르게** 한다.
        /// </summary>
        private static readonly HashSet<string> OpenTopHats = new HashSet<string>
        {
            "hat_flower", "hat_butterfly_wing", "hat_hero_mask", "hat_cyber_visor",
        };

        [Test]
        public void HatRecipes_EitherHideTheHairCrownOrAreListedOpenTop()
        {
            Dictionary<string, OutfitSlot> slots = CatalogSlots();
            int hats = 0;
            foreach (string id in OutfitShapeLibrary.ExactRecipeIds())
            {
                if (!slots.TryGetValue(id, out OutfitSlot slot) || slot != OutfitSlot.Hat) continue;
                hats++;
                Assert.IsTrue(OutfitShapeLibrary.TryGet(OutfitSlot.Hat, id, out OutfitRecipe r));
                bool hides = r.hideNodes != null && System.Array.IndexOf(r.hideNodes, OutfitShapeLibrary.HairCrownNode) >= 0;
                if (OpenTopHats.Contains(id))
                    Assert.IsFalse(hides, id + ": 정수리가 열린 모자인데 올림머리를 숨긴다");
                else
                    Assert.IsTrue(hides, id + ": 정수리를 덮는 모자인데 올림머리(HairCrown)를 안 숨긴다 — 머리가 모자를 뚫는다");
            }
            Assert.Greater(hats, OpenTopHats.Count, "모자 레시피가 사라졌다");
        }

        // ── 몸 전체 형태(스타일) 표 ──

        /// <summary>
        /// 스타일 표의 키는 실재하는 아이템이고, **그 표가 다루는 슬롯**이어야 한다. 오타면 반바지가 긴바지로,
        /// 망토가 자켓으로 조용히 돌아간다(예외 없음).
        /// </summary>
        [Test]
        public void StyleTables_NameRealItemsOfTheRightSlot()
        {
            Dictionary<string, OutfitSlot> slots = CatalogSlots();
            foreach (string id in OutfitShapeLibrary.StyledItemIds())
                Assert.IsTrue(slots.ContainsKey(id), $"스타일 표의 '{id}'가 카탈로그에 없다");

            foreach (KeyValuePair<string, OutfitSlot> e in slots)
            {
                string id = e.Key;
                if (OutfitShapeLibrary.SleeveOf(id) != SleeveLength.Short || OutfitShapeLibrary.SleeveIsSecondary(id))
                    Assert.AreEqual(OutfitSlot.Top, e.Value, id + ": 소매 표는 상의 전용");
                if (OutfitShapeLibrary.OuterFormOf(id) != OuterForm.OpenJacket)
                    Assert.AreEqual(OutfitSlot.Outerwear, e.Value, id + ": 겉옷 형태 표는 겉옷 전용");
                if (OutfitShapeLibrary.LegFormOf(id) != LegForm.Long)
                    Assert.AreEqual(OutfitSlot.Bottom, e.Value, id + ": 하의 길이 표는 하의 전용");
                if (OutfitShapeLibrary.FootFormOf(id) != FootForm.Shoe)
                    Assert.AreEqual(OutfitSlot.Shoes, e.Value, id + ": 신발 형태 표는 신발 전용");
            }
        }

        /// <summary>
        /// 반바지·샌들은 다리·발을 피부로 칠하므로 **덧붙일 천·밑창 레시피가 반드시 있어야** 한다 —
        /// 없으면 맨다리·맨발로 걷는다.
        /// </summary>
        [Test]
        public void SkinRevealingForms_HaveTheirClothRecipes()
        {
            foreach (KeyValuePair<string, OutfitSlot> e in CatalogSlots())
            {
                if (OutfitShapeLibrary.LegFormOf(e.Key) == LegForm.Shorts)
                    Assert.IsTrue(OutfitShapeLibrary.TryGet(OutfitSlot.Bottom, e.Key, out _), e.Key + ": 반바지인데 천 레시피가 없다");
                if (OutfitShapeLibrary.FootFormOf(e.Key) == FootForm.Sandal)
                    Assert.IsTrue(OutfitShapeLibrary.TryGet(OutfitSlot.Shoes, e.Key, out _), e.Key + ": 샌들인데 밑창 레시피가 없다");
            }
        }

        // ── 파츠별 앵커 ──

        [Test]
        public void BelongsTo_SplitsDefaultAndExplicitAnchors()
        {
            OutfitPart plain = new OutfitPart { prim = PrimitiveType.Cube };
            OutfitPart sameAsRecipe = new OutfitPart { prim = PrimitiveType.Cube, hasAnchor = true, anchor = OutfitAnchor.Body };
            OutfitPart onLeg = new OutfitPart { prim = PrimitiveType.Cube, hasAnchor = true, anchor = OutfitAnchor.LegL };
            OutfitPart bind = new OutfitPart { bindName = "NetRing" };

            Assert.IsTrue(OutfitShapeLibrary.BelongsTo(plain, OutfitAnchor.Body, OutfitAnchor.Body, true));
            Assert.IsTrue(OutfitShapeLibrary.BelongsTo(sameAsRecipe, OutfitAnchor.Body, OutfitAnchor.Body, true), "레시피 앵커와 같은 지정은 기본 컨테이너로");
            Assert.IsFalse(OutfitShapeLibrary.BelongsTo(sameAsRecipe, OutfitAnchor.Body, OutfitAnchor.Body, false), "같은 파츠가 두 번 만들어지면 안 된다");
            Assert.IsTrue(OutfitShapeLibrary.BelongsTo(onLeg, OutfitAnchor.Body, OutfitAnchor.LegL, false));
            Assert.IsFalse(OutfitShapeLibrary.BelongsTo(onLeg, OutfitAnchor.Body, OutfitAnchor.LegR, false));
            Assert.IsFalse(OutfitShapeLibrary.BelongsTo(onLeg, OutfitAnchor.Body, OutfitAnchor.Body, true));
            Assert.IsFalse(OutfitShapeLibrary.BelongsTo(bind, OutfitAnchor.Root, OutfitAnchor.Root, true), "bind는 컨테이너에 들어가지 않는다");
        }

        [Test]
        public void Mirror_FlipsXAndSideRotations()
        {
            OutfitPart p = new OutfitPart { pos = new Vector3(0.1f, -0.2f, 0.05f), euler = new Vector3(10f, 20f, -30f) };
            OutfitPart m = OutfitShapeLibrary.Mirror(p);
            Assert.AreEqual(new Vector3(-0.1f, -0.2f, 0.05f), m.pos);
            Assert.AreEqual(new Vector3(10f, -20f, 30f), m.euler);
        }

        /// <summary>
        /// 다리에 다는 파츠는 **다리 관절 아래**에 가야 걸을 때 함께 흔들린다. 반바지를 가짜 계층에 입혀
        /// 양다리 컨테이너가 각 관절 아래 생기고, 왼다리 파츠가 거울 좌표인지 본다.
        /// </summary>
        [Test]
        public void LegParts_SpawnUnderEachLegPivot_Mirrored()
        {
            GameObject root = new GameObject("AnchorProbe");
            try
            {
                GameObject body = new GameObject("Body"); body.transform.SetParent(root.transform, false);
                GameObject legL = new GameObject("LegLPivot"); legL.transform.SetParent(root.transform, false);
                GameObject legR = new GameObject("LegRPivot"); legR.transform.SetParent(root.transform, false);

                Assert.IsTrue(OutfitShapeLibrary.TryGet(OutfitSlot.Bottom, "bot_cargo", out OutfitRecipe cargo));
                OutfitShapeLibrary.Apply(root.transform, OutfitSlot.Bottom, cargo, Color.yellow, Color.white);

                Transform cL = legL.transform.Find(OutfitShapeLibrary.SpawnContainerName(OutfitSlot.Bottom, OutfitAnchor.LegL));
                Transform cR = legR.transform.Find(OutfitShapeLibrary.SpawnContainerName(OutfitSlot.Bottom, OutfitAnchor.LegR));
                Assert.IsNotNull(cL, "왼다리 관절 아래 컨테이너가 없다");
                Assert.IsNotNull(cR, "오른다리 관절 아래 컨테이너가 없다");
                Assert.AreEqual(cR.childCount, cL.childCount);
                Assert.Greater(cR.childCount, 0);
                Assert.AreEqual(-cR.GetChild(0).localPosition.x, cL.GetChild(0).localPosition.x, 1e-5f, "왼다리가 거울 좌표가 아니다");
                Assert.Greater(cR.GetChild(0).localPosition.x, 0f, "오른다리 기준 좌표는 바깥이 +X다");
                Assert.IsNull(root.transform.Find(OutfitShapeLibrary.SpawnContainerName(OutfitSlot.Bottom)),
                    "다리 파츠만 있는 레시피가 루트에 빈 컨테이너를 만들었다");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HatRecipes_AnchorToHatRoot()
        {
            foreach (OutfitRecipe r in RecipesForSlot(OutfitSlot.Hat))
                Assert.AreEqual(OutfitAnchor.HatRoot, r.anchor);
        }

        [Test]
        public void BindNames_AreKnownPlayerVisualNodes()
        {
            foreach (OutfitShapeLibrary.ToolEntry e in OutfitShapeLibrary.ToolEntries)
                AssertBindNamesKnown(e.recipe);
            foreach (OutfitRecipe r in OutfitShapeLibrary.ExactRecipeValues())
                AssertBindNamesKnown(r);
        }

        [Test]
        public void BindNames_NeverStartWithSpawnPrefix()
        {
            // `DestroySpawnedMaterials`는 노드 이름의 `OP_` 접두 하나로 "누가 만든 머티리얼인가"를
            // 가른다 — spawn 파츠는 OutfitShapeLibrary가, bind 노드는 PlayerVisualBuilder가 소유한다.
            // bind 노드가 그 접두를 쓰면 **남의 머티리얼을 이중으로 파기**해 캐릭터가 분홍색이 된다
            // (PlayerVisualBuilder.OnDestroy 주석이 적어 둔 검정/마젠타 회귀와 같은 자리).
            foreach (OutfitShapeLibrary.ToolEntry e in OutfitShapeLibrary.ToolEntries)
                AssertBindNamesAreNotSpawnPrefixed(e.recipe);
            foreach (OutfitRecipe r in OutfitShapeLibrary.ExactRecipeValues())
                AssertBindNamesAreNotSpawnPrefixed(r);
        }

        private static void AssertBindNamesAreNotSpawnPrefixed(OutfitRecipe recipe)
        {
            if (recipe == null || recipe.parts == null) return;
            for (int i = 0; i < recipe.parts.Length; i++)
            {
                OutfitPart p = recipe.parts[i];
                if (!p.IsBind) continue;
                Assert.IsFalse(
                    p.bindName.StartsWith(OutfitShapeLibrary.SpawnPrefix),
                    $"bind 노드 '{p.bindName}'가 spawn 접두({OutfitShapeLibrary.SpawnPrefix})를 쓴다");
            }
        }

        /// <summary>
        /// bind 파츠의 색 역할은 ApplyToCharacter가 ApplyPartColor로 넣는 색과 일치해야 한다 —
        /// bind 경로는 형태만 담당하고 색은 안 건드리므로, role이 어긋나면 조용히 무시된다.
        /// ApplyToCharacter: NetHandle ← primaryColor, NetRing ← secondaryColor.
        /// </summary>
        [Test]
        public void ToolBindRoles_MatchApplyPartColorAssignment()
        {
            foreach (OutfitShapeLibrary.ToolEntry e in OutfitShapeLibrary.ToolEntries)
            {
                Assert.IsTrue(OutfitShapeLibrary.TryGetBoundPart(e.recipe, "NetHandle", out OutfitPart h));
                Assert.AreEqual(PartColorRole.Primary, h.role, "NetHandle은 primaryColor로 칠해진다");

                Assert.IsTrue(OutfitShapeLibrary.TryGetBoundPart(e.recipe, "NetRing", out OutfitPart ring));
                Assert.AreEqual(PartColorRole.Secondary, ring.role, "NetRing은 secondaryColor로 칠해진다");
            }
        }

        [Test]
        public void AllRecipes_HaveAtLeastOnePart()
        {
            foreach (OutfitRecipe r in OutfitShapeLibrary.ExactRecipeValues())
            {
                Assert.IsNotNull(r.parts);
                Assert.Greater(r.parts.Length, 0, "파츠가 없는 레시피는 아이템을 통째로 사라지게 한다");
            }
        }

        /// <summary>role=Fixed인데 알파가 0이면 그 파츠는 영영 안 보인다.</summary>
        [Test]
        public void FixedColorParts_HaveVisibleAlpha()
        {
            foreach (OutfitRecipe r in OutfitShapeLibrary.ExactRecipeValues())
            {
                for (int p = 0; p < r.parts.Length; p++)
                {
                    if (r.parts[p].role != PartColorRole.Fixed) continue;
                    Assert.Greater(r.parts[p].fixedColor.a, 0.01f, "Fixed 파츠의 알파가 0이다");
                }
            }
        }

        // ── 순수 계산부 ──

        [Test]
        public void CountSpawnParts_CountsOnlyNonBind()
        {
            Assert.AreEqual(0, OutfitShapeLibrary.CountSpawnParts(null));
            foreach (OutfitShapeLibrary.ToolEntry e in OutfitShapeLibrary.ToolEntries)
                Assert.AreEqual(0, OutfitShapeLibrary.CountSpawnParts(e.recipe), "도구는 전부 bind다");

            Assert.IsTrue(OutfitShapeLibrary.TryGet(OutfitSlot.Hat, "hat_wizard", out OutfitRecipe wizard));
            Assert.AreEqual(wizard.parts.Length, OutfitShapeLibrary.CountSpawnParts(wizard), "모자는 전부 spawn이다");
        }

        /// <summary>2D 카드의 dark 규칙과 같은 계수여야 두 그림이 어긋나지 않는다.</summary>
        [Test]
        public void Darken_ScalesRgbBy07_AndKeepsAlpha()
        {
            Color d = OutfitShapeLibrary.Darken(new Color(1f, 0.5f, 0.2f, 0.8f));

            Assert.AreEqual(0.7f, d.r, 0.0001f);
            Assert.AreEqual(0.35f, d.g, 0.0001f);
            Assert.AreEqual(0.14f, d.b, 0.0001f);
            Assert.AreEqual(0.8f, d.a, 0.0001f, "알파는 보존해야 *_none 판정이 흐트러지지 않는다");
        }

        [Test]
        public void ResolveColor_EachRole_PicksExpectedSource()
        {
            Color primary = new Color(0.8f, 0.2f, 0.1f, 1f);
            Color secondary = new Color(0.1f, 0.3f, 0.9f, 1f);
            Color fixedCol = new Color(1f, 0.9f, 0.3f, 1f);

            Assert.AreEqual(primary, Resolve(PartColorRole.Primary, fixedCol, primary, secondary));
            Assert.AreEqual(secondary, Resolve(PartColorRole.Secondary, fixedCol, primary, secondary));
            Assert.AreEqual(OutfitShapeLibrary.Darken(primary), Resolve(PartColorRole.PrimaryDark, fixedCol, primary, secondary));
            Assert.AreEqual(OutfitShapeLibrary.Darken(secondary), Resolve(PartColorRole.SecondaryDark, fixedCol, primary, secondary));
            Assert.AreEqual(fixedCol, Resolve(PartColorRole.Fixed, fixedCol, primary, secondary));
        }

        private static Color Resolve(PartColorRole role, Color fixedCol, Color primary, Color secondary)
        {
            OutfitPart p = new OutfitPart { role = role, fixedColor = fixedCol };
            return OutfitShapeLibrary.ResolveColor(p, primary, secondary);
        }

        private static void AssertBindNamesKnown(OutfitRecipe r)
        {
            if (r == null || r.parts == null) return;
            for (int p = 0; p < r.parts.Length; p++)
            {
                if (!r.parts[p].IsBind) continue;
                Assert.IsTrue(KnownNodes.Contains(r.parts[p].bindName),
                    $"'{r.parts[p].bindName}'은 PlayerVisualBuilder가 만드는 노드가 아니다 — 오타면 조용히 무시된다");
            }
        }
    }
}
#endif
