using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>파츠 색을 아이템 데이터의 어느 값에서 가져올지.</summary>
    public enum PartColorRole
    {
        Primary,
        Secondary,
        PrimaryDark,
        SecondaryDark,
        Skin,
        Fixed,
    }

    /// <summary>
    /// 레시피가 붙는 기준 노드. <b>현행 노드의 실제 부모와 1:1로 맞춘다.</b>
    /// - Root: 플레이어 루트. NetHandle/NetRing/Backpack/Accessory 계열이 전부 루트 직속 자식이다
    ///   (PlayerVisualBuilder가 SetParent(transform)). 스케일 왜곡이 없어 좌표를 그대로 읽을 수 있다.
    /// - HatRoot: HeadPivot 자식(스케일 0.60). Cap/CapBrim과 같은 좌표계.
    /// 몸통(Body)·배낭(Backpack)은 비균일 스케일이라 자식 좌표가 찌그러진다 — 앵커로 쓰지 않고
    /// Root 좌표계에 직접 놓는다(BackpackStrap의 z=1.6 같은 값이 나오는 걸 피한다).
    /// </summary>
    public enum OutfitAnchor
    {
        Root,
        HatRoot,
    }

    /// <summary>
    /// 의상 파츠 하나. <paramref name="bindName"/>이 있으면 기존 노드를 재사용(bind),
    /// 비어 있으면 앵커 아래에 새로 만든다(spawn).
    /// </summary>
    public struct OutfitPart
    {
        public string bindName;
        public PrimitiveType prim;
        public Vector3 pos;      // 앵커 로컬
        public Vector3 scale;
        public Vector3 euler;
        public PartColorRole role;
        public Color fixedColor; // role == Fixed 일 때만 의미

        public bool IsBind => !string.IsNullOrEmpty(bindName);
    }

    /// <summary>itemId 하나의 형태 정의.</summary>
    public sealed class OutfitRecipe
    {
        public OutfitAnchor anchor;
        public OutfitPart[] parts;
        /// <summary>이 레시피가 켜지면 숨길 기존 노드(예: 왕관 → Cap, CapBrim).</summary>
        public string[] hideNodes;
    }

    /// <summary>
    /// 의상 형태의 <b>단일 출처</b>. 3D 캐릭터·3D 마네킹 프리뷰·2D 카드 아이콘이 전부 여기를 읽는다.
    ///
    /// 예전엔 형태 정의가 셋으로 갈라져 있었다 —
    /// CharacterOutfitManager.ApplyToolShape(도구 9분기) / PlayerVisualBuilder.ApplyAccessory(악세 3프리셋) /
    /// CharacterPortraitRenderer.DrawItemPreview(2D 카드의 또 다른 분기).
    /// 그래서 카드엔 목도리 분기가 있는데 3D엔 없어 "카드는 목도리, 캐릭터는 가슴 큐브"로 어긋났고,
    /// 2D 카드에는 실재하지 않는 itemId(hat_beanie) 분기가 죽은 채로 남아 있었다.
    ///
    /// <b>bind는 슬롯 커버리지가 전부일 때만 쓴다.</b> bind는 기존 노드의 mesh를 갈아끼우는데,
    /// 레시피가 없는 아이템으로 갈아입으면 색-only 폴백 경로가 mesh를 되돌리지 않기 때문이다.
    /// - Tool: 기본 잠자리채 레시피가 else 역할을 해 <b>전량 커버</b> → bind 사용 가능(필수이기도 하다,
    ///   PlayerMovement가 NetHandle/NetRing을 Find로 캐싱해 스윙을 돌리므로 파괴하면 안 된다).
    /// - Hat: 부분 커버 → <b>spawn + hideNodes만</b>. Cap/CapBrim의 mesh는 절대 건드리지 않는다.
    /// 이 불변식은 OutfitShapeLibraryTests가 고정한다.
    /// </summary>
    public static class OutfitShapeLibrary
    {
        /// <summary>spawn 파츠 컨테이너 이름 접두사. 어떤 transform.Find/FindDeep도 이 이름을 조회하지 않는다.</summary>
        public const string SpawnPrefix = "OP_";

        /// <summary>
        /// 슬롯별 스폰 컨테이너 이름을 미리 구워 둔다. <c>SpawnPrefix + slot</c>은 enum을 문자열로
        /// 바꾸며 할당이 나는데, <see cref="Apply"/>는 <b>슬롯마다</b> 불리고 그 호출부에는
        /// 프리뷰 렌더러가 있다(마네킹에 옷을 입히는 경로). 라운드마다 8~10개씩 나던 문자열을 없앤다.
        /// </summary>
        private static readonly string[] SpawnContainerNames = BuildSpawnContainerNames();

        private static string[] BuildSpawnContainerNames()
        {
            OutfitSlot[] slots = (OutfitSlot[])System.Enum.GetValues(typeof(OutfitSlot));
            int max = 0;
            for (int i = 0; i < slots.Length; i++)
                if ((int)slots[i] > max) max = (int)slots[i];

            string[] names = new string[max + 1];
            for (int i = 0; i < slots.Length; i++)
                names[(int)slots[i]] = SpawnPrefix + slots[i];
            return names;
        }

        /// <summary>슬롯의 스폰 컨테이너 이름(할당 없음). 범위 밖이면 예전처럼 즉석 조합으로 물러난다.</summary>
        internal static string SpawnContainerName(OutfitSlot slot)
        {
            int i = (int)slot;
            return i >= 0 && i < SpawnContainerNames.Length && SpawnContainerNames[i] != null
                ? SpawnContainerNames[i]
                : SpawnPrefix + slot;
        }

        private static readonly Color SkinColor = new Color(0.92f, 0.78f, 0.62f);

        // ── 조회 ──────────────────────────────────────────────

        /// <summary>
        /// 레시피를 찾는다. Tool은 기본 잠자리채가 else 역할을 해 <b>항상 true</b>다.
        /// 나머지 슬롯은 레시피가 없으면 false — 호출부가 기존 색-only 경로로 폴백한다.
        /// </summary>
        public static bool TryGet(OutfitSlot slot, string itemId, out OutfitRecipe recipe)
        {
            string id = itemId ?? "";
            if (slot == OutfitSlot.Tool)
            {
                recipe = ResolveTool(id);
                return recipe != null;
            }
            return ExactRecipes.TryGetValue(id, out recipe);
        }

        /// <summary>
        /// 도구 레시피 해석. 현행 ApplyToolShape의 <c>else if</c> 체인과 <b>같은 순서로</b> 평가한다 —
        /// 순서가 바뀌면 tool_tranq_gun처럼 두 키워드에 걸리는 id의 결과가 달라진다.
        /// </summary>
        internal static OutfitRecipe ResolveTool(string itemId)
        {
            string id = itemId ?? "";
            for (int i = 0; i < ToolTable.Length; i++)
            {
                string[] keys = ToolTable[i].keys;
                if (keys == null) return ToolTable[i].recipe;   // 기본 잠자리채(else)
                for (int k = 0; k < keys.Length; k++)
                {
                    if (id.Contains(keys[k])) return ToolTable[i].recipe;
                }
            }
            return null;
        }

        /// <summary>레시피에서 특정 bind 파츠를 꺼낸다(파리티 테스트용).</summary>
        internal static bool TryGetBoundPart(OutfitRecipe recipe, string bindName, out OutfitPart part)
        {
            part = default;
            if (recipe == null || recipe.parts == null) return false;
            for (int i = 0; i < recipe.parts.Length; i++)
            {
                if (recipe.parts[i].bindName == bindName) { part = recipe.parts[i]; return true; }
            }
            return false;
        }

        /// <summary>exact 매칭으로 등록된 모든 itemId(카탈로그 정합 테스트용).</summary>
        internal static IEnumerable<string> ExactRecipeIds()
        {
            return ExactRecipes.Keys;
        }

        internal static IEnumerable<OutfitRecipe> ExactRecipeValues()
        {
            return ExactRecipes.Values;
        }

        internal static ToolEntry[] ToolEntries => ToolTable;

        // ── 적용 ──────────────────────────────────────────────

        /// <summary>
        /// 레시피를 <paramref name="root"/>(플레이어 또는 마네킹)에 적용한다.
        /// <paramref name="recipe"/>가 null이면 이 슬롯이 남긴 spawn 파츠만 정리한다 —
        /// 레시피 있는 아이템 → 없는 아이템으로 갈아입을 때 잔상이 남지 않도록 <b>매번 불러야 한다</b>.
        /// </summary>
        public static void Apply(Transform root, OutfitSlot slot, OutfitRecipe recipe,
            Color primary, Color secondary)
        {
            if (root == null) return;

            int needSpawn = CountSpawnParts(recipe);
            string containerName = SpawnContainerName(slot);
            Transform container = FindDeep(root, containerName);

            // 도구처럼 bind만 쓰는 슬롯에 빈 컨테이너를 만들지 않는다 —
            // 모든 플레이어·마네킹마다 쓸모없는 GameObject가 하나씩 늘어난다.
            if (needSpawn > 0)
            {
                Transform anchor = ResolveAnchor(root, recipe.anchor);
                if (anchor == null) anchor = root;
                container = EnsureContainer(container, anchor, SpawnPrefix + slot);
            }

            int spawnIndex = 0;
            if (recipe != null && recipe.parts != null)
            {
                for (int i = 0; i < recipe.parts.Length; i++)
                {
                    OutfitPart p = recipe.parts[i];
                    Color c = ResolveColor(p, primary, secondary);
                    if (p.IsBind)
                    {
                        ApplyBound(root, p);
                    }
                    else
                    {
                        ApplySpawned(root, container, spawnIndex, p, c);
                        spawnIndex++;
                    }
                }

                if (recipe.hideNodes != null)
                {
                    for (int i = 0; i < recipe.hideNodes.Length; i++)
                    {
                        Transform t = FindDeep(root, recipe.hideNodes[i]);
                        if (t != null) t.gameObject.SetActive(false);
                    }
                }
            }

            if (container != null) TrimContainer(container, spawnIndex);
        }

        internal static int CountSpawnParts(OutfitRecipe recipe)
        {
            if (recipe == null || recipe.parts == null) return 0;
            int n = 0;
            for (int i = 0; i < recipe.parts.Length; i++)
                if (!recipe.parts[i].IsBind) n++;
            return n;
        }

        private static Transform ResolveAnchor(Transform root, OutfitAnchor anchor)
        {
            switch (anchor)
            {
                case OutfitAnchor.HatRoot: return FindDeep(root, "HatRoot");
                default: return root;
            }
        }

        private static Transform EnsureContainer(Transform existing, Transform anchor, string name)
        {
            Transform c = existing;
            if (c == null)
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(anchor, false);
                c = go.transform;
            }
            else if (c.parent != anchor)
            {
                // 같은 슬롯이 앵커를 바꾼 경우(현재는 없지만 확장 대비). 로컬 좌표를 보존하지 않는다 —
                // 파츠가 매번 다시 배치되므로 부모만 옮기면 된다.
                c.SetParent(anchor, false);
            }
            c.localPosition = Vector3.zero;
            c.localRotation = Quaternion.identity;
            c.localScale = Vector3.one;
            return c;
        }

        /// <summary>
        /// bind 파츠 — 기존 노드의 mesh/좌표만 갈아끼운다. <b>색은 건드리지 않는다.</b>
        /// bind 노드의 머티리얼은 PlayerVisualBuilder가 슬롯별로 쥐고 있고 ApplyPartColor가 칠하므로,
        /// 여기서 또 칠하면 소유자가 둘이 된다. 대신 파츠의 role이 ApplyPartColor가 넣는 색과
        /// 일치하는지를 OutfitShapeParityTests가 고정한다(NetHandle=Primary / NetRing=Secondary).
        /// SetActive(true)도 하지 않는다 — *_none의 알파 0 판정은 ApplyPartColor 한 곳에만 둔다.
        /// </summary>
        private static void ApplyBound(Transform root, OutfitPart p)
        {
            Transform t = FindDeep(root, p.bindName);
            if (t == null) return;

            MeshFilter mf = t.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = GetPrimMesh(p.prim);
            t.localPosition = p.pos;
            t.localScale = p.scale;
            t.localRotation = Quaternion.Euler(p.euler);
        }

        private static void ApplySpawned(Transform root, Transform container, int index, OutfitPart p, Color c)
        {
            Transform t = index < container.childCount ? container.GetChild(index) : null;
            if (t == null)
            {
                // CreatePrimitive를 쓰지 않는다 — 콜라이더가 생겼다 파괴되는 왕복을 피한다.
                GameObject go = new GameObject(SpawnPrefix + index);
                go.transform.SetParent(container, false);
                go.AddComponent<MeshFilter>();
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.sharedMaterial = CreatePartMaterial(root, c);
                t = go.transform;
            }

            t.gameObject.SetActive(true);
            MeshFilter mf = t.GetComponent<MeshFilter>();
            if (mf != null) mf.sharedMesh = GetPrimMesh(p.prim);
            t.localPosition = p.pos;
            t.localScale = p.scale;
            t.localRotation = Quaternion.Euler(p.euler);

            MeshRenderer mr = t.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null)
            {
                // spawn 파츠의 머티리얼은 자기 자신만 쓰는 인스턴스라 sharedMaterial 직접 수정이 안전하다.
                // renderer.material(getter)을 쓰면 파츠마다 인스턴스가 하나씩 더 생겨 샌다.
                mr.sharedMaterial.color = c;
                if (mr.sharedMaterial.HasProperty("_BaseColor")) mr.sharedMaterial.SetColor("_BaseColor", c);
            }
        }

        /// <summary>
        /// 이 루트가 들고 있는 <b>살아 있는</b> spawn 파츠의 머티리얼을 전부 파기한다.
        ///
        /// <see cref="TrimContainer"/>는 <b>남는</b> 파츠만 지우므로, 루트가 통째로 파괴될 때
        /// 마지막까지 쓰이던 파츠의 머티리얼은 아무도 지우지 않는다 — 마네킹은 <b>파괴가 정상 수명</b>이라
        /// (프리뷰가 각도를 바꿀 때마다 다시 짓는다) 그때마다 샌다.
        /// <c>PlayerVisualBuilder.OnDestroy</c>가 자기 <c>runtimeMaterials</c>만 도는 것과 같은
        /// 사각지대이고, 여기가 <b>그 목록에 없는 두 번째 생성 지점</b>이다(<see cref="CreatePartMaterial"/>).
        ///
        /// bind 파츠는 건드리지 않는다 — 그 노드의 머티리얼 소유자는 PlayerVisualBuilder다(이중 파기 방지).
        /// spawn 파츠만 <see cref="SpawnPrefix"/> 이름을 갖는다는 사실로 가른다.
        /// </summary>
        public static void DestroySpawnedMaterials(Transform root)
        {
            if (root == null) return;

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer r = renderers[i];
                if (r == null || r.sharedMaterial == null) continue;
                if (!r.gameObject.name.StartsWith(SpawnPrefix)) continue;
                Object.Destroy(r.sharedMaterial);
            }
        }

        /// <summary>남는 spawn 파츠를 파괴한다. 머티리얼은 GameObject와 함께 사라지지 않으므로 같이 지운다.</summary>
        private static void TrimContainer(Transform container, int used)
        {
            for (int i = container.childCount - 1; i >= used; i--)
            {
                Transform t = container.GetChild(i);
                MeshRenderer mr = t.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null) Object.Destroy(mr.sharedMaterial);
                Object.Destroy(t.gameObject);
            }
        }

        /// <summary>
        /// spawn 파츠용 머티리얼. 셰이더는 캐릭터가 이미 쓰고 있는 것을 그대로 빌린다 —
        /// Standard/URP/Unlit 중 어느 것이 잡혔든 자동으로 맞고, 파이프라인 판정이 한 곳에만 있게 된다
        /// (PlayerVisualBuilder.MakeMaterial의 fallback 체인이 그 한 곳이다).
        /// </summary>
        private static Material CreatePartMaterial(Transform root, Color c)
        {
            Shader sh = null;
            MeshRenderer any = root.GetComponentInChildren<MeshRenderer>(true);
            if (any != null && any.sharedMaterial != null) sh = any.sharedMaterial.shader;
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            if (sh == null) sh = Shader.Find("Sprites/Default");

            Material m = new Material(sh);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            return m;
        }

        internal static Color ResolveColor(OutfitPart p, Color primary, Color secondary)
        {
            switch (p.role)
            {
                case PartColorRole.Secondary: return secondary;
                case PartColorRole.PrimaryDark: return Darken(primary);
                case PartColorRole.SecondaryDark: return Darken(secondary);
                case PartColorRole.Skin: return SkinColor;
                case PartColorRole.Fixed: return p.fixedColor;
                default: return primary;
            }
        }

        /// <summary>2D 카드의 dark 규칙(CharacterPortraitRenderer)과 같은 계수 0.7 — 두 그림이 어긋나지 않게.</summary>
        internal static Color Darken(Color c)
        {
            return new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f, c.a);
        }

        // ── 프리미티브 메시 캐시 ──────────────────────────────
        //
        // PrimitiveType별 sharedMesh를 1회 추출해 재사용. CreatePrimitive를 매 적용마다 부르면
        // 콜라이더가 생겼다 파괴되는 왕복이 파츠 수만큼 반복된다.

        private static Dictionary<PrimitiveType, Mesh> primMeshCache;

        internal static Mesh GetPrimMesh(PrimitiveType type)
        {
            if (primMeshCache == null) primMeshCache = new Dictionary<PrimitiveType, Mesh>();
            if (!primMeshCache.TryGetValue(type, out Mesh m) || m == null)
            {
                GameObject temp = GameObject.CreatePrimitive(type);
                m = temp.GetComponent<MeshFilter>().sharedMesh;   // built-in 메시라 GO를 지워도 살아있다
                Object.Destroy(temp.GetComponent<Collider>());
                Object.Destroy(temp);
                primMeshCache[type] = m;
            }
            return m;
        }

        public static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindDeep(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // ── 레시피 테이블 ────────────────────────────────────

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        /// <summary>bind 파츠 — 기존 노드를 재사용한다.</summary>
        private static OutfitPart B(string bind, PrimitiveType prim, Vector3 pos, Vector3 scale,
            Vector3 euler, PartColorRole role)
        {
            return new OutfitPart { bindName = bind, prim = prim, pos = pos, scale = scale, euler = euler, role = role };
        }

        /// <summary>spawn 파츠.</summary>
        private static OutfitPart S(PrimitiveType prim, Vector3 pos, Vector3 scale, PartColorRole role)
        {
            return new OutfitPart { prim = prim, pos = pos, scale = scale, euler = Vector3.zero, role = role };
        }

        /// <summary>spawn 파츠 + 회전.</summary>
        private static OutfitPart SR(PrimitiveType prim, Vector3 pos, Vector3 scale, Vector3 euler, PartColorRole role)
        {
            return new OutfitPart { prim = prim, pos = pos, scale = scale, euler = euler, role = role };
        }

        /// <summary>spawn 파츠 + 고정색(아이템 색과 무관한 금장식·해골 등).</summary>
        private static OutfitPart SF(PrimitiveType prim, Vector3 pos, Vector3 scale, Color color)
        {
            return new OutfitPart
            {
                prim = prim, pos = pos, scale = scale, euler = Vector3.zero,
                role = PartColorRole.Fixed, fixedColor = color,
            };
        }

        private static readonly Color Gold = new Color(1f, 0.9f, 0.32f);
        private static readonly Color Bone = new Color(0.95f, 0.94f, 0.9f);

        // ── 도구(Tool) — 현행 ApplyToolShape 9분기의 파리티 이전 ──
        //
        // 손 기준 좌표. 현행 코드의 hx=0.29, hy=0.52를 전개해 절대값으로 적었다.
        // NetHandle/NetRing은 플레이어 루트 직속 자식이므로 이 값은 루트 로컬 좌표다
        // (주석이 "손 위치 기준"이라 적혀 있지만 HandR의 자식이 아니다 — 앵커를 손으로 옮기면 안 된다).
        // 모든 도구가 bind 2파츠다. spawn을 섞으면 PlayerMovement의 스윙 캐시가 깨진다.

        internal struct ToolEntry
        {
            public string[] keys;      // null이면 기본(else) 분기
            public OutfitRecipe recipe;
        }

        private static readonly ToolEntry[] ToolTable =
        {
            // 총: 박스형 본체 + 원통 총구
            new ToolEntry { keys = new[] { "gun", "blaster", "tranq" }, recipe = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    B("NetHandle", PrimitiveType.Cube,     V(0.29f, 0.52f, 0.18f), V(0.08f, 0.05f, 0.22f), Vector3.zero,        PartColorRole.Primary),
                    B("NetRing",   PrimitiveType.Cylinder, V(0.29f, 0.52f, 0.32f), V(0.06f, 0.06f, 0.04f), V(90f, 0f, 0f),      PartColorRole.Secondary),
                },
            }},

            // 지팡이: 가는 막대 + 구체 오브
            new ToolEntry { keys = new[] { "wand" }, recipe = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    B("NetHandle", PrimitiveType.Cylinder, V(0.29f, 0.70f, 0.05f), V(0.03f, 0.40f, 0.03f), V(10f, 0f, -15f),    PartColorRole.Primary),
                    B("NetRing",   PrimitiveType.Sphere,   V(0.37f, 1.10f, 0.05f), V(0.10f, 0.10f, 0.10f), Vector3.zero,        PartColorRole.Secondary),
                },
            }},

            // 올가미: 짧은 막대 + 고리(디스크). 고리의 X축 -20°는 부감 카메라에서 edge-on collapse를 막는다.
            new ToolEntry { keys = new[] { "lasso" }, recipe = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    B("NetHandle", PrimitiveType.Cylinder, V(0.29f, 0.65f, 0f),    V(0.04f, 0.25f, 0.04f), V(20f, 0f, -12f),    PartColorRole.Primary),
                    B("NetRing",   PrimitiveType.Cylinder, V(0.35f, 0.94f, 0.06f), V(0.28f, 0.02f, 0.28f), V(-20f, 0f, 0f),     PartColorRole.Secondary),
                },
            }},

            // 수리검: 납작한 별 — Cube 십자형
            new ToolEntry { keys = new[] { "shuriken" }, recipe = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    B("NetHandle", PrimitiveType.Cube, V(0.29f, 0.52f, 0.10f), V(0.18f, 0.02f, 0.05f), V(0f, 45f, 0f), PartColorRole.Primary),
                    B("NetRing",   PrimitiveType.Cube, V(0.29f, 0.52f, 0.10f), V(0.05f, 0.02f, 0.18f), V(0f, 45f, 0f), PartColorRole.Secondary),
                },
            }},

            // 검: 박스 손잡이 + 긴 박스 칼날
            new ToolEntry { keys = new[] { "cutlass", "sword" }, recipe = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    B("NetHandle", PrimitiveType.Cube, V(0.29f, 0.58f, 0.05f), V(0.05f, 0.10f, 0.05f), Vector3.zero, PartColorRole.Primary),
                    B("NetRing",   PrimitiveType.Cube, V(0.29f, 0.84f, 0.05f), V(0.04f, 0.40f, 0.10f), Vector3.zero, PartColorRole.Secondary),
                },
            }},

            // 발사기: 손목 박스 + 구체 발사구
            new ToolEntry { keys = new[] { "web_shooter" }, recipe = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    B("NetHandle", PrimitiveType.Cube,   V(0.29f, 0.60f, 0.05f), V(0.08f, 0.06f, 0.12f), Vector3.zero, PartColorRole.Primary),
                    B("NetRing",   PrimitiveType.Sphere, V(0.29f, 0.60f, 0.15f), V(0.04f, 0.04f, 0.04f), Vector3.zero, PartColorRole.Secondary),
                },
            }},

            // 돋보기: 가는 막대 + 렌즈(디스크)
            new ToolEntry { keys = new[] { "magnify" }, recipe = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    B("NetHandle", PrimitiveType.Cylinder, V(0.29f, 0.57f, 0.10f), V(0.03f, 0.18f, 0.03f), V(35f, 0f, 0f),  PartColorRole.Primary),
                    B("NetRing",   PrimitiveType.Cylinder, V(0.29f, 0.74f, 0.20f), V(0.16f, 0.02f, 0.16f), V(-20f, 0f, 0f), PartColorRole.Secondary),
                },
            }},

            // 카메라: 박스 본체 + 원통 렌즈
            new ToolEntry { keys = new[] { "camera" }, recipe = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    B("NetHandle", PrimitiveType.Cube,     V(0.29f, 0.57f, 0.18f), V(0.16f, 0.10f, 0.10f), Vector3.zero,   PartColorRole.Primary),
                    B("NetRing",   PrimitiveType.Cylinder, V(0.29f, 0.57f, 0.26f), V(0.07f, 0.07f, 0.06f), V(90f, 0f, 0f), PartColorRole.Secondary),
                },
            }},

            // 레이저 포인터: 펜형 본체 + 발광 팁.
            // 신규 — 옛 코드엔 분기가 없어 tool_laser가 else로 떨어져 잠자리채로 보였다.
            new ToolEntry { keys = new[] { "laser" }, recipe = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    B("NetHandle", PrimitiveType.Cube,   V(0.29f, 0.55f, 0.14f), V(0.04f, 0.04f, 0.22f), Vector3.zero, PartColorRole.Primary),
                    B("NetRing",   PrimitiveType.Sphere, V(0.29f, 0.55f, 0.27f), V(0.05f, 0.05f, 0.05f), Vector3.zero, PartColorRole.Secondary),
                },
            }},

            // 기본 잠자리채(else). 망 디스크의 X축 -20°는 절대 건드리지 않는다 — 옛 rot(0,0,90)은
            // 법선이 ±X라 부감 카메라에서 edge-on으로 collapse해 "망이 사라지고 손잡이만 남던" 회귀의 원인.
            new ToolEntry { keys = null, recipe = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    B("NetHandle", PrimitiveType.Cylinder, V(0.29f, 0.74f, 0.02f), V(0.04f, 0.40f, 0.04f), V(20f, 0f, -15f), PartColorRole.Primary),
                    B("NetRing",   PrimitiveType.Cylinder, V(0.34f, 1.14f, 0.06f), V(0.20f, 0.02f, 0.20f), V(-20f, 0f, 0f),  PartColorRole.Secondary),
                },
            }},
        };

        // ── 모자(Hat) — HatRoot 로컬. 전부 spawn + Cap/CapBrim 숨김 ──
        //
        // 좌표 감각: HeadPivot 스케일 0.60, 머리 구체 반지름 x 0.35 / y 0.34.
        // 기존 Cap은 y 0.18~0.42(지름 0.30), CapBrim은 z 0.28. 눈높이는 y ≈ -0.03, 얼굴 앞면은 z ≈ 0.30.
        // 띠(band)류는 그 높이의 머리 지름보다 커야 파묻히지 않는다 —
        // y 0.22에서 0.53 / y 0.24에서 0.50 / y 0.28에서 0.37 / y 0.29에서 0.365.

        private static readonly string[] HideCap = { "Cap", "CapBrim" };
        private static readonly string[] HideBackpack = { "Backpack" };

        private static readonly Dictionary<string, OutfitRecipe> ExactRecipes = new Dictionary<string, OutfitRecipe>
        {
            // 밀짚모자: 넓고 얇은 챙 + 낮은 돔 + 띠
            ["hat_straw"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCap,
                parts = new[]
                {
                    S(PrimitiveType.Cylinder, V(0f, 0.20f, 0f), V(0.68f, 0.015f, 0.68f), PartColorRole.Primary),
                    S(PrimitiveType.Cylinder, V(0f, 0.30f, 0f), V(0.36f, 0.10f, 0.36f),  PartColorRole.Primary),
                    S(PrimitiveType.Cylinder, V(0f, 0.245f, 0f), V(0.54f, 0.02f, 0.54f), PartColorRole.PrimaryDark),
                },
            },

            // 사파리 헬멧: 중간 챙 + 반구 돔 + 정수리 능선
            ["hat_safari"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCap,
                parts = new[]
                {
                    S(PrimitiveType.Cylinder, V(0f, 0.19f, 0.01f), V(0.62f, 0.022f, 0.60f), PartColorRole.Primary),
                    S(PrimitiveType.Sphere,   V(0f, 0.22f, 0f),    V(0.76f, 0.46f, 0.76f),  PartColorRole.Primary),
                    S(PrimitiveType.Cube,     V(0f, 0.42f, 0f),    V(0.04f, 0.06f, 0.44f),  PartColorRole.PrimaryDark),
                },
            },

            // 꽃 왕관: 줄기 링 + 꽃잎 4 + 꽃술
            ["hat_flower"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCap,
                parts = new[]
                {
                    S(PrimitiveType.Cylinder, V(0f, 0.22f, 0f),      V(0.58f, 0.022f, 0.58f), PartColorRole.PrimaryDark),
                    S(PrimitiveType.Sphere,   V(0f, 0.25f, 0.26f),   V(0.15f, 0.10f, 0.15f),  PartColorRole.Primary),
                    S(PrimitiveType.Sphere,   V(0f, 0.25f, -0.26f),  V(0.15f, 0.10f, 0.15f),  PartColorRole.Primary),
                    S(PrimitiveType.Sphere,   V(-0.26f, 0.25f, 0f),  V(0.15f, 0.10f, 0.15f),  PartColorRole.Primary),
                    S(PrimitiveType.Sphere,   V(0.26f, 0.25f, 0f),   V(0.15f, 0.10f, 0.15f),  PartColorRole.Primary),
                    SF(PrimitiveType.Sphere,  V(0f, 0.28f, 0.26f),   V(0.07f, 0.05f, 0.07f),  Gold),
                },
            },

            // 장수풍뎅이 투구: 반구 투구 + 테 + 앞으로 뻗은 뿔
            ["hat_beetle"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCap,
                parts = new[]
                {
                    S(PrimitiveType.Sphere,   V(0f, 0.18f, -0.01f), V(0.78f, 0.48f, 0.78f), PartColorRole.Primary),
                    S(PrimitiveType.Cylinder, V(0f, 0.10f, 0f),     V(0.80f, 0.025f, 0.80f), PartColorRole.PrimaryDark),
                    SR(PrimitiveType.Cube,    V(0f, 0.36f, 0.16f),  V(0.08f, 0.18f, 0.09f), V(35f, 0f, 0f),  PartColorRole.PrimaryDark),
                    SR(PrimitiveType.Cube,    V(0f, 0.50f, 0.31f),  V(0.055f, 0.20f, 0.06f), V(-28f, 0f, 0f), PartColorRole.PrimaryDark),
                },
            },

            // 곤충왕 왕관: 띠 + 뾰족 4 + 보석
            ["hat_crown"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCap,
                parts = new[]
                {
                    S(PrimitiveType.Cylinder, V(0f, 0.29f, 0f),     V(0.40f, 0.05f, 0.40f),  PartColorRole.Primary),
                    S(PrimitiveType.Cube,     V(0f, 0.40f, 0.17f),  V(0.07f, 0.15f, 0.05f),  PartColorRole.Primary),
                    S(PrimitiveType.Cube,     V(0f, 0.40f, -0.17f), V(0.07f, 0.15f, 0.05f),  PartColorRole.Primary),
                    S(PrimitiveType.Cube,     V(-0.17f, 0.40f, 0f), V(0.05f, 0.15f, 0.07f),  PartColorRole.Primary),
                    S(PrimitiveType.Cube,     V(0.17f, 0.40f, 0f),  V(0.05f, 0.15f, 0.07f),  PartColorRole.Primary),
                    S(PrimitiveType.Sphere,   V(0f, 0.30f, 0.20f),  V(0.09f, 0.09f, 0.06f),  PartColorRole.Secondary),
                    SF(PrimitiveType.Sphere,  V(0f, 0.49f, 0.17f),  V(0.055f, 0.055f, 0.055f), Gold),
                },
            },

            // 나비 날개 머리띠: 띠 + 위/아래 날개 + 더듬이
            ["hat_butterfly_wing"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCap,
                parts = new[]
                {
                    S(PrimitiveType.Cylinder, V(0f, 0.24f, 0f),       V(0.54f, 0.03f, 0.54f),  PartColorRole.Primary),
                    SR(PrimitiveType.Cube,    V(-0.26f, 0.44f, -0.03f), V(0.22f, 0.26f, 0.025f), V(0f, 0f, 22f),   PartColorRole.Primary),
                    SR(PrimitiveType.Cube,    V(0.26f, 0.44f, -0.03f),  V(0.22f, 0.26f, 0.025f), V(0f, 0f, -22f),  PartColorRole.Primary),
                    SR(PrimitiveType.Cube,    V(-0.21f, 0.28f, -0.03f), V(0.15f, 0.15f, 0.025f), V(0f, 0f, 22f),   PartColorRole.Secondary),
                    SR(PrimitiveType.Cube,    V(0.21f, 0.28f, -0.03f),  V(0.15f, 0.15f, 0.025f), V(0f, 0f, -22f),  PartColorRole.Secondary),
                    SR(PrimitiveType.Cube,    V(-0.08f, 0.40f, 0.10f),  V(0.016f, 0.22f, 0.016f), V(-16f, 0f, 16f),  PartColorRole.PrimaryDark),
                    SR(PrimitiveType.Cube,    V(0.08f, 0.40f, 0.10f),   V(0.016f, 0.22f, 0.016f), V(-16f, 0f, -16f), PartColorRole.PrimaryDark),
                },
            },

            // 카우보이 모자: 앞뒤로 좁은 넓은 챙 + 높은 크라운 + 정수리 홈 + 띠
            ["hat_cowboy"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCap,
                parts = new[]
                {
                    S(PrimitiveType.Cylinder, V(0f, 0.20f, 0f),  V(0.64f, 0.022f, 0.50f), PartColorRole.Primary),
                    S(PrimitiveType.Cylinder, V(0f, 0.33f, 0f),  V(0.34f, 0.16f, 0.34f),  PartColorRole.Primary),
                    S(PrimitiveType.Cube,     V(0f, 0.45f, 0f),  V(0.09f, 0.06f, 0.30f),  PartColorRole.PrimaryDark),
                    S(PrimitiveType.Cylinder, V(0f, 0.28f, 0f),  V(0.40f, 0.03f, 0.40f),  PartColorRole.Secondary),
                },
            },

            // 히어로 마스크: 눈 부위를 덮는 마스크. 모자가 아니라 얼굴 장비다(눈높이 y≈-0.03, 앞면 z≈0.30).
            ["hat_hero_mask"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCap,
                parts = new[]
                {
                    S(PrimitiveType.Cube,  V(0f, -0.02f, 0.27f),    V(0.50f, 0.17f, 0.10f), PartColorRole.Primary),
                    S(PrimitiveType.Cube,  V(-0.12f, -0.02f, 0.33f), V(0.15f, 0.07f, 0.03f), PartColorRole.Secondary),
                    S(PrimitiveType.Cube,  V(0.12f, -0.02f, 0.33f),  V(0.15f, 0.07f, 0.03f), PartColorRole.Secondary),
                    S(PrimitiveType.Cube,  V(-0.26f, -0.02f, 0.10f), V(0.06f, 0.11f, 0.34f), PartColorRole.Primary),
                    S(PrimitiveType.Cube,  V(0.26f, -0.02f, 0.10f),  V(0.06f, 0.11f, 0.34f), PartColorRole.Primary),
                    SR(PrimitiveType.Cube, V(0f, 0.24f, 0.16f),      V(0.05f, 0.16f, 0.14f), V(22f, 0f, 0f), PartColorRole.Secondary),
                },
            },

            // 닌자 두건: 정수리~뒤통수만 덮는 납작한 구(얼굴 z 0.30은 비워 둔다) + 입가리개 + 이마띠 + 꼬리
            ["hat_ninja"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCap,
                parts = new[]
                {
                    S(PrimitiveType.Sphere, V(0f, 0.14f, -0.04f),  V(0.76f, 0.56f, 0.72f), PartColorRole.Primary),
                    S(PrimitiveType.Cube,   V(0f, -0.15f, 0.20f),  V(0.52f, 0.17f, 0.24f), PartColorRole.Primary),
                    S(PrimitiveType.Cube,   V(0f, 0.06f, 0.28f),   V(0.54f, 0.09f, 0.12f), PartColorRole.Secondary),
                    SR(PrimitiveType.Cube,  V(0.05f, 0.10f, -0.36f), V(0.09f, 0.28f, 0.07f), V(22f, 0f, 8f), PartColorRole.Primary),
                },
            },

            // 해적 삼각모: 넓은 챙 + 낮은 크라운 + 세 방향 접힌 챙 + 해골
            ["hat_pirate"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCap,
                parts = new[]
                {
                    S(PrimitiveType.Cylinder, V(0f, 0.24f, 0f),        V(0.60f, 0.025f, 0.54f), PartColorRole.Primary),
                    S(PrimitiveType.Cylinder, V(0f, 0.30f, 0f),        V(0.32f, 0.10f, 0.32f),  PartColorRole.Primary),
                    SR(PrimitiveType.Cube,    V(0f, 0.33f, 0.27f),     V(0.38f, 0.17f, 0.04f),  V(18f, 0f, 0f),   PartColorRole.Primary),
                    SR(PrimitiveType.Cube,    V(-0.24f, 0.33f, -0.11f), V(0.32f, 0.17f, 0.04f), V(14f, 58f, 0f),  PartColorRole.Primary),
                    SR(PrimitiveType.Cube,    V(0.24f, 0.33f, -0.11f),  V(0.32f, 0.17f, 0.04f), V(14f, -58f, 0f), PartColorRole.Primary),
                    SF(PrimitiveType.Sphere,  V(0f, 0.35f, 0.30f),     V(0.10f, 0.10f, 0.04f),  Bone),
                    SF(PrimitiveType.Cube,    V(0f, 0.28f, 0.30f),     V(0.12f, 0.028f, 0.03f), Bone),
                },
            },

            // 사이버 바이저: 눈 앞 렌즈 + 프레임 + 관자놀이 암 + 안테나
            ["hat_cyber_visor"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCap,
                parts = new[]
                {
                    S(PrimitiveType.Cube,  V(0f, 0.07f, 0.25f),   V(0.54f, 0.10f, 0.12f), PartColorRole.Primary),
                    S(PrimitiveType.Cube,  V(0f, 0f, 0.29f),      V(0.50f, 0.12f, 0.07f), PartColorRole.Secondary),
                    S(PrimitiveType.Cube,  V(-0.27f, 0.05f, 0.06f), V(0.05f, 0.06f, 0.38f), PartColorRole.Primary),
                    S(PrimitiveType.Cube,  V(0.27f, 0.05f, 0.06f),  V(0.05f, 0.06f, 0.38f), PartColorRole.Primary),
                    SR(PrimitiveType.Cube, V(0.25f, 0.24f, 0.06f),  V(0.025f, 0.20f, 0.025f), V(0f, 0f, -12f), PartColorRole.Secondary),
                },
            },

            // 마법사 모자: 넓은 챙 + 4단으로 좁아지며 앞으로 휘는 원뿔 + 띠 + 별
            ["hat_wizard"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCap,
                parts = new[]
                {
                    S(PrimitiveType.Cylinder,  V(0f, 0.22f, 0f),    V(0.66f, 0.02f, 0.66f), PartColorRole.Primary),
                    S(PrimitiveType.Cylinder,  V(0f, 0.32f, 0f),    V(0.34f, 0.10f, 0.34f), PartColorRole.Primary),
                    SR(PrimitiveType.Cylinder, V(0f, 0.50f, 0.02f), V(0.23f, 0.09f, 0.23f), V(7f, 0f, 0f),  PartColorRole.Primary),
                    SR(PrimitiveType.Cylinder, V(0f, 0.66f, 0.06f), V(0.14f, 0.08f, 0.14f), V(14f, 0f, 0f), PartColorRole.Primary),
                    SR(PrimitiveType.Cylinder, V(0f, 0.78f, 0.11f), V(0.07f, 0.06f, 0.07f), V(20f, 0f, 0f), PartColorRole.Primary),
                    S(PrimitiveType.Cylinder,  V(0f, 0.27f, 0f),    V(0.42f, 0.035f, 0.42f), PartColorRole.Secondary),
                    SF(PrimitiveType.Sphere,   V(0f, 0.86f, 0.15f), V(0.10f, 0.10f, 0.10f), Gold),
                },
            },

            // 군용 헬멧: 챙 없는 돔 + 테 + 턱끈 + 위장 밴드
            ["hat_military"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCap,
                parts = new[]
                {
                    S(PrimitiveType.Sphere,   V(0f, 0.20f, -0.01f), V(0.78f, 0.50f, 0.80f),  PartColorRole.Primary),
                    S(PrimitiveType.Cylinder, V(0f, 0.10f, 0f),     V(0.82f, 0.025f, 0.82f), PartColorRole.PrimaryDark),
                    S(PrimitiveType.Cube,     V(0f, -0.08f, 0.22f), V(0.26f, 0.045f, 0.10f), PartColorRole.PrimaryDark),
                    S(PrimitiveType.Cylinder, V(0f, 0.20f, 0f),     V(0.80f, 0.02f, 0.82f),  PartColorRole.Secondary),
                },
            },

            // ── 악세서리(Accessory) — 루트 로컬. 전부 spawn ──
            //
            // 옛 ApplyAccessory는 미리 만든 4노드(AccGlassesL/R·AccNecklace·AccBadge) 중 하나만 켰다.
            // 15종 중 8종이 else로 떨어져 "곤충 날개 장식"·"신비의 오라"·"천사의 후광"이 전부
            // 가슴팍 큐브 하나로 보였다. 그 8종이 여기 있다.
            // 기준점: 목 y1.00 / 가슴 앞면 z0.20 / 눈 y1.20·z0.21 / 손 (±0.29, 0.52) / 머리 중심 y1.22·반지름 0.21.

            // 뿔테 안경 — 옛 AccGlassesL/R 좌표 그대로 + 콧대
            ["acc_glasses"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    S(PrimitiveType.Cube, V(-0.072f, 1.20f, 0.21f), V(0.10f, 0.09f, 0.02f), PartColorRole.Primary),
                    S(PrimitiveType.Cube, V(0.072f, 1.20f, 0.21f),  V(0.10f, 0.09f, 0.02f), PartColorRole.Primary),
                    S(PrimitiveType.Cube, V(0f, 1.20f, 0.21f),      V(0.05f, 0.02f, 0.02f), PartColorRole.Primary),
                },
            },

            // 해적 안대: 한쪽 눈만 가리고 머리끈이 사선으로 지난다(옛 코드는 안경 한쪽을 끄기만 했다)
            ["acc_eyepatch"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    S(PrimitiveType.Cube,  V(-0.072f, 1.20f, 0.21f), V(0.11f, 0.10f, 0.02f), PartColorRole.Primary),
                    SR(PrimitiveType.Cube, V(0f, 1.23f, 0.16f),      V(0.30f, 0.02f, 0.14f), V(0f, 0f, -8f), PartColorRole.PrimaryDark),
                },
            },

            // 곤충 펜던트 — 옛 AccNecklace 좌표 + 목줄
            ["acc_pendant"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    SR(PrimitiveType.Cube, V(-0.06f, 1.02f, 0.16f), V(0.018f, 0.11f, 0.018f), V(0f, 0f, 12f),  PartColorRole.Secondary),
                    SR(PrimitiveType.Cube, V(0.06f, 1.02f, 0.16f),  V(0.018f, 0.11f, 0.018f), V(0f, 0f, -12f), PartColorRole.Secondary),
                    S(PrimitiveType.Sphere, V(0f, 1.00f, 0.20f),    V(0.07f, 0.07f, 0.05f),   PartColorRole.Primary),
                },
            },

            // 수정구: 목에 매단 큰 구슬 + 받침
            ["acc_crystal_orb"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    SR(PrimitiveType.Cube, V(-0.06f, 1.03f, 0.16f), V(0.018f, 0.10f, 0.018f), V(0f, 0f, 12f),  PartColorRole.Secondary),
                    SR(PrimitiveType.Cube, V(0.06f, 1.03f, 0.16f),  V(0.018f, 0.10f, 0.018f), V(0f, 0f, -12f), PartColorRole.Secondary),
                    S(PrimitiveType.Sphere,   V(0f, 0.98f, 0.21f),  V(0.10f, 0.10f, 0.08f),   PartColorRole.Primary),
                    S(PrimitiveType.Cylinder, V(0f, 1.04f, 0.20f),  V(0.05f, 0.015f, 0.05f),  PartColorRole.Secondary),
                },
            },

            // 곤충박사 배지 — 옛 AccBadge 좌표 그대로 + 핀
            ["acc_badge"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    S(PrimitiveType.Cube, V(0f, 0.85f, 0.20f), V(0.10f, 0.10f, 0.04f), PartColorRole.Primary),
                    S(PrimitiveType.Cube, V(0f, 0.90f, 0.20f), V(0.02f, 0.04f, 0.02f), PartColorRole.Secondary),
                },
            },

            // 스카프: 목 두름 + 앞으로 늘어진 자락
            ["acc_scarf"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    S(PrimitiveType.Cylinder, V(0f, 1.00f, 0.02f),    V(0.26f, 0.05f, 0.24f), PartColorRole.Primary),
                    SR(PrimitiveType.Cube,    V(0.08f, 0.86f, 0.17f), V(0.10f, 0.26f, 0.05f), V(8f, 0f, -6f), PartColorRole.Primary),
                },
            },

            // 곤충 날개 장식: 등 뒤 상하 4장
            ["acc_wings"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    SR(PrimitiveType.Cube, V(-0.22f, 0.92f, -0.24f), V(0.30f, 0.34f, 0.02f), V(0f, -22f, 24f),  PartColorRole.Primary),
                    SR(PrimitiveType.Cube, V(0.22f, 0.92f, -0.24f),  V(0.30f, 0.34f, 0.02f), V(0f, 22f, -24f),  PartColorRole.Primary),
                    SR(PrimitiveType.Cube, V(-0.18f, 0.72f, -0.24f), V(0.20f, 0.22f, 0.02f), V(0f, -22f, 20f),  PartColorRole.Secondary),
                    SR(PrimitiveType.Cube, V(0.18f, 0.72f, -0.24f),  V(0.20f, 0.22f, 0.02f), V(0f, 22f, -20f),  PartColorRole.Secondary),
                },
            },

            // 신비의 오라: 몸을 감싸는 기울어진 고리 2개 + 떠 있는 구슬
            ["acc_aura"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    SR(PrimitiveType.Cylinder, V(0f, 0.80f, 0f),        V(0.62f, 0.012f, 0.62f), V(12f, 0f, 8f),  PartColorRole.Primary),
                    SR(PrimitiveType.Cylinder, V(0f, 1.02f, 0f),        V(0.50f, 0.012f, 0.50f), V(-10f, 0f, -6f), PartColorRole.Secondary),
                    S(PrimitiveType.Sphere,    V(0.30f, 0.95f, 0.10f),  V(0.06f, 0.06f, 0.06f),  PartColorRole.Secondary),
                    S(PrimitiveType.Sphere,    V(-0.28f, 0.70f, -0.08f), V(0.05f, 0.05f, 0.05f), PartColorRole.Secondary),
                },
            },

            // 천사의 후광: 머리 위 고리. 토러스 프리미티브가 없어 육각으로 근사한다 —
            // 원판 하나로 그리면 후광이 아니라 접시로 보인다.
            ["acc_halo"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    SR(PrimitiveType.Cube, V(0f, 1.52f, 0.15f),       V(0.16f, 0.022f, 0.045f), V(0f, 0f, 0f),    PartColorRole.Primary),
                    SR(PrimitiveType.Cube, V(0.130f, 1.52f, 0.075f),  V(0.16f, 0.022f, 0.045f), V(0f, 60f, 0f),   PartColorRole.Primary),
                    SR(PrimitiveType.Cube, V(0.130f, 1.52f, -0.075f), V(0.16f, 0.022f, 0.045f), V(0f, 120f, 0f),  PartColorRole.Primary),
                    SR(PrimitiveType.Cube, V(0f, 1.52f, -0.15f),      V(0.16f, 0.022f, 0.045f), V(0f, 180f, 0f),  PartColorRole.Primary),
                    SR(PrimitiveType.Cube, V(-0.130f, 1.52f, -0.075f), V(0.16f, 0.022f, 0.045f), V(0f, 240f, 0f), PartColorRole.Primary),
                    SR(PrimitiveType.Cube, V(-0.130f, 1.52f, 0.075f), V(0.16f, 0.022f, 0.045f), V(0f, 300f, 0f),  PartColorRole.Primary),
                },
            },

            // 빨간 반다나: 이마 띠 + 뒤통수 매듭 + 자락
            ["acc_bandana"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    S(PrimitiveType.Cylinder, V(0f, 1.30f, 0.02f),    V(0.44f, 0.035f, 0.44f), PartColorRole.Primary),
                    S(PrimitiveType.Sphere,   V(0f, 1.28f, -0.19f),   V(0.09f, 0.08f, 0.09f),  PartColorRole.PrimaryDark),
                    SR(PrimitiveType.Cube,    V(0.03f, 1.20f, -0.24f), V(0.06f, 0.16f, 0.03f), V(18f, 0f, 10f), PartColorRole.Primary),
                },
            },

            // 거미 엠블럼: 가슴팍 몸통·머리 + 다리 4
            ["acc_spider_emblem"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    S(PrimitiveType.Sphere, V(0f, 0.88f, 0.20f),     V(0.10f, 0.10f, 0.05f),   PartColorRole.Primary),
                    S(PrimitiveType.Sphere, V(0f, 0.94f, 0.20f),     V(0.06f, 0.06f, 0.04f),   PartColorRole.Primary),
                    SR(PrimitiveType.Cube,  V(-0.09f, 0.91f, 0.20f), V(0.14f, 0.018f, 0.018f), V(0f, 0f, 25f),  PartColorRole.Secondary),
                    SR(PrimitiveType.Cube,  V(0.09f, 0.91f, 0.20f),  V(0.14f, 0.018f, 0.018f), V(0f, 0f, -25f), PartColorRole.Secondary),
                    SR(PrimitiveType.Cube,  V(-0.09f, 0.85f, 0.20f), V(0.14f, 0.018f, 0.018f), V(0f, 0f, -25f), PartColorRole.Secondary),
                    SR(PrimitiveType.Cube,  V(0.09f, 0.85f, 0.20f),  V(0.14f, 0.018f, 0.018f), V(0f, 0f, 25f),  PartColorRole.Secondary),
                },
            },

            // 닌자 머플러: 목 두름 + 뒤로 길게 날리는 자락
            ["acc_ninja_scarf"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    S(PrimitiveType.Cylinder, V(0f, 1.00f, 0f),        V(0.26f, 0.05f, 0.24f), PartColorRole.Primary),
                    SR(PrimitiveType.Cube,    V(-0.06f, 0.92f, -0.22f), V(0.11f, 0.22f, 0.03f), V(-18f, 0f, 8f),  PartColorRole.Primary),
                    SR(PrimitiveType.Cube,    V(-0.10f, 0.76f, -0.34f), V(0.10f, 0.20f, 0.03f), V(-30f, 0f, 14f), PartColorRole.Secondary),
                },
            },

            // 네온 팔찌: 양 손목 밴드 + 발광 링
            ["acc_neon_ring"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    S(PrimitiveType.Cylinder, V(0.29f, 0.60f, 0f),  V(0.16f, 0.025f, 0.16f), PartColorRole.Primary),
                    S(PrimitiveType.Cylinder, V(-0.29f, 0.60f, 0f), V(0.16f, 0.025f, 0.16f), PartColorRole.Primary),
                    S(PrimitiveType.Cylinder, V(0.29f, 0.60f, 0f),  V(0.19f, 0.012f, 0.19f), PartColorRole.Secondary),
                    S(PrimitiveType.Cylinder, V(-0.29f, 0.60f, 0f), V(0.19f, 0.012f, 0.19f), PartColorRole.Secondary),
                },
            },

            // 군번줄: 목줄 2가닥 + 인식표 2장
            ["acc_dog_tag"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    SR(PrimitiveType.Cube, V(-0.07f, 1.00f, 0.16f), V(0.02f, 0.14f, 0.02f),  V(0f, 0f, 10f),  PartColorRole.Primary),
                    SR(PrimitiveType.Cube, V(0.07f, 1.00f, 0.16f),  V(0.02f, 0.14f, 0.02f),  V(0f, 0f, -10f), PartColorRole.Primary),
                    S(PrimitiveType.Cube,  V(0f, 0.90f, 0.19f),     V(0.07f, 0.10f, 0.015f), PartColorRole.Primary),
                    S(PrimitiveType.Cube,  V(0.03f, 0.87f, 0.19f),  V(0.06f, 0.09f, 0.012f), PartColorRole.Secondary),
                },
            },

            // ── 가방(Backpack) — 루트 로컬. Backpack 노드(0, 0.80, -0.22)에 파츠를 덧붙인다 ──

            // 드래곤 배낭: 기본 상자 + 박쥐 날개 + 등뼈 가시
            ["bag_dragon"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    SR(PrimitiveType.Cube, V(-0.26f, 0.98f, -0.28f), V(0.28f, 0.30f, 0.02f), V(0f, -25f, 28f), PartColorRole.Secondary),
                    SR(PrimitiveType.Cube, V(0.26f, 0.98f, -0.28f),  V(0.28f, 0.30f, 0.02f), V(0f, 25f, -28f), PartColorRole.Secondary),
                    SR(PrimitiveType.Cube, V(0f, 0.96f, -0.32f),     V(0.05f, 0.10f, 0.05f), V(20f, 0f, 0f),   PartColorRole.PrimaryDark),
                    SR(PrimitiveType.Cube, V(0f, 0.84f, -0.32f),     V(0.05f, 0.09f, 0.05f), V(20f, 0f, 0f),   PartColorRole.PrimaryDark),
                },
            },

            // 요정 날개 가방: 기본 상자 + 상하 요정 날개 4장
            ["bag_fairy"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    SR(PrimitiveType.Cube, V(-0.20f, 1.00f, -0.26f), V(0.24f, 0.30f, 0.015f), V(0f, -20f, 20f), PartColorRole.Secondary),
                    SR(PrimitiveType.Cube, V(0.20f, 1.00f, -0.26f),  V(0.24f, 0.30f, 0.015f), V(0f, 20f, -20f), PartColorRole.Secondary),
                    SR(PrimitiveType.Cube, V(-0.16f, 0.78f, -0.26f), V(0.16f, 0.20f, 0.015f), V(0f, -20f, 14f), PartColorRole.Secondary),
                    SR(PrimitiveType.Cube, V(0.16f, 0.78f, -0.26f),  V(0.16f, 0.20f, 0.015f), V(0f, 20f, -14f), PartColorRole.Secondary),
                },
            },

            // 어깨가방: 등 상자를 숨기고 어깨끈 + 옆구리 가방으로 바꾼다
            ["bag_satchel"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root, hideNodes = HideBackpack,
                parts = new[]
                {
                    SR(PrimitiveType.Cube, V(0f, 0.90f, 0.20f),     V(0.07f, 0.40f, 0.03f), V(0f, 0f, 24f),  PartColorRole.PrimaryDark),
                    SR(PrimitiveType.Cube, V(0f, 0.90f, -0.20f),    V(0.07f, 0.40f, 0.03f), V(0f, 0f, -24f), PartColorRole.PrimaryDark),
                    S(PrimitiveType.Cube,  V(-0.28f, 0.62f, -0.02f), V(0.16f, 0.20f, 0.24f), PartColorRole.Primary),
                    S(PrimitiveType.Cube,  V(-0.28f, 0.71f, -0.02f), V(0.17f, 0.05f, 0.25f), PartColorRole.PrimaryDark),
                },
            },

            // 연구 장비함: 기본 상자 + 잠금쇠 + 시료 탱크 + 손잡이
            ["bag_science"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    S(PrimitiveType.Cube,     V(0f, 0.90f, -0.31f),    V(0.16f, 0.03f, 0.02f), PartColorRole.Secondary),
                    S(PrimitiveType.Cube,     V(0f, 0.76f, -0.31f),    V(0.16f, 0.03f, 0.02f), PartColorRole.Secondary),
                    S(PrimitiveType.Cylinder, V(0.16f, 0.86f, -0.30f), V(0.09f, 0.13f, 0.09f), PartColorRole.Secondary),
                    S(PrimitiveType.Cube,     V(0f, 1.00f, -0.24f),    V(0.14f, 0.03f, 0.04f), PartColorRole.PrimaryDark),
                },
            },

            // ── 겉옷(Outerwear) — 루트 로컬. Body/ArmL/ArmR은 ApplyPartColor가 칠하고 여기선 덧붙인다 ──

            // 전설의 망토: 등 뒤로 흐르는 망토 + 어깨 깃 + 밑단
            ["outer_legendary"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    SR(PrimitiveType.Cube, V(0f, 0.72f, -0.22f), V(0.50f, 0.60f, 0.03f), V(-6f, 0f, 0f), PartColorRole.Primary),
                    S(PrimitiveType.Cube,  V(0f, 1.00f, -0.10f), V(0.40f, 0.10f, 0.16f), PartColorRole.Secondary),
                    S(PrimitiveType.Cube,  V(0f, 0.42f, -0.24f), V(0.52f, 0.08f, 0.04f), PartColorRole.Secondary),
                },
            },

            // 마법사 로브: 발목까지 퍼지는 치맛단 + 등 망토 + 금테
            ["outer_wizard"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    S(PrimitiveType.Cylinder, V(0f, 0.44f, 0f),     V(0.56f, 0.22f, 0.50f), PartColorRole.Primary),
                    S(PrimitiveType.Cube,     V(0f, 0.74f, -0.21f), V(0.46f, 0.52f, 0.03f), PartColorRole.Primary),
                    S(PrimitiveType.Cylinder, V(0f, 0.23f, 0f),     V(0.58f, 0.02f, 0.52f), PartColorRole.Secondary),
                },
            },

            // 그림자 코트: 갈라진 코트 자락 + 세운 깃 + 라펠
            ["outer_shadow"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    SR(PrimitiveType.Cube, V(-0.14f, 0.44f, -0.16f), V(0.20f, 0.42f, 0.06f), V(4f, 0f, 3f),   PartColorRole.Primary),
                    SR(PrimitiveType.Cube, V(0.14f, 0.44f, -0.16f),  V(0.20f, 0.42f, 0.06f), V(4f, 0f, -3f),  PartColorRole.Primary),
                    S(PrimitiveType.Cube,  V(0f, 1.00f, 0.06f),      V(0.42f, 0.12f, 0.26f), PartColorRole.PrimaryDark),
                    SR(PrimitiveType.Cube, V(-0.10f, 0.88f, 0.20f),  V(0.10f, 0.26f, 0.03f), V(0f, 0f, 10f),  PartColorRole.PrimaryDark),
                    SR(PrimitiveType.Cube, V(0.10f, 0.88f, 0.20f),   V(0.10f, 0.26f, 0.03f), V(0f, 0f, -10f), PartColorRole.PrimaryDark),
                },
            },

            // 연구원 코트: 앞자락 2장 + 뒷자락 + 주머니
            ["outer_labcoat"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    S(PrimitiveType.Cube, V(-0.13f, 0.66f, 0.20f), V(0.20f, 0.50f, 0.03f), PartColorRole.Primary),
                    S(PrimitiveType.Cube, V(0.13f, 0.66f, 0.20f),  V(0.20f, 0.50f, 0.03f), PartColorRole.Primary),
                    S(PrimitiveType.Cube, V(0f, 0.62f, -0.20f),    V(0.46f, 0.54f, 0.03f), PartColorRole.Primary),
                    S(PrimitiveType.Cube, V(-0.14f, 0.48f, 0.22f), V(0.12f, 0.09f, 0.02f), PartColorRole.PrimaryDark),
                },
            },
        };
    }
}
