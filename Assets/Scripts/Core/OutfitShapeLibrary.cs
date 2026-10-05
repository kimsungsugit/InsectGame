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
        /// <summary>
        /// 몸통(Body). 걷기에서 **몸통만 위아래로 튄다**(PlayerMovement bob ±6cm) — 옷깃·망토·멜빵처럼 몸에 붙은 것은
        /// 여기 붙어야 함께 움직인다. Body는 크기를 메시에 구운 노드라(스케일 1) 자식 좌표가 찌그러지지 않는다.
        /// 좌표는 몸통 중심(루트 y 0.77) 기준이다.
        /// </summary>
        Body,
        /// <summary>왼다리 관절(LegLPivot, 루트 (−0.13, 0.48, 0)). 반바지·장화·박차처럼 다리와 함께 흔들려야 하는 것.</summary>
        LegL,
        /// <summary>오른다리 관절(LegRPivot).</summary>
        LegR,
    }

    /// <summary>상의 소매. Short면 어깨 캡만 상의 색(반소매), Long이면 팔 전체, None(민소매)이면 어깨까지 피부.</summary>
    public enum SleeveLength { Short, Long, None }

    /// <summary>
    /// 겉옷이 몸통을 어떻게 덮는가. OpenJacket = 몸통·팔은 겉옷, 앞섶 사이로 상의(셔츠 판)와 옷깃이 보인다.
    /// Robe = 앞까지 여민 옷이라 상의가 안 보인다. Cape = 몸통은 상의 그대로 두고 등 뒤에 망토만 단다.
    /// </summary>
    public enum OuterForm { OpenJacket, Robe, Cape }

    /// <summary>하의 길이. Shorts면 다리 노드를 피부로 칠하고 허벅지 천은 레시피가 단다.</summary>
    public enum LegForm { Long, Shorts }

    /// <summary>신발 형태. Sandal이면 발 노드를 피부로 칠하고 밑창·끈은 레시피가 단다.</summary>
    public enum FootForm { Shoe, Sandal }

    /// <summary>
    /// spawn 파츠의 메시 모양. 기본은 <see cref="Primitive"/>(내장 프리미티브, <see cref="OutfitPart.prim"/>)이라
    /// 이 필드가 생기기 전의 레시피는 그대로다. 나머지는 <see cref="ProcMeshLibrary"/> 생성기이고 인자는
    /// <see cref="OutfitPart.shapeArgs"/>에 담는다.
    /// </summary>
    public enum PartShape
    {
        Primitive,
        /// <summary>둥근 상자. 크기(scale)를 메시에 **굽고** 노드 스케일은 1로 둔다(모서리 왜곡 방지). args.x = 모서리 반경.</summary>
        RoundedBox,
        /// <summary>테이퍼 캡슐(높이 2·반지름 0.5 규약). args.x = 아래 반지름 비율.</summary>
        Capsule,
        /// <summary>늘어진 천 — <see cref="ProcMeshLibrary.DrapeShell"/>. args = (감싸는 각도, 밑단 퍼짐, 주름 수, 주름 깊이). 원점 = 윗변 중앙, 아래로 늘어진다.</summary>
        Drape,
        /// <summary>고리 — <see cref="ProcMeshLibrary.Torus"/>. args.x = 관 반지름(단위 큰 반지름 0.5 기준).</summary>
        Torus,
        /// <summary>보석(팔면체) — <see cref="ProcMeshLibrary.Diamond"/>. 단위 반지름 0.5, 위 0.7 / 아래 0.5.</summary>
        Gem,
        /// <summary>잠자리채 머리(세운 테 + 뒤로 늘어진 그물) — <see cref="ProcMeshLibrary.NetHead"/>. 원점 = 테 아래 끝. args = (관 반지름, 주머니 깊이).</summary>
        NetHead,
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

        /// <summary>메시 모양. 기본 Primitive면 <see cref="prim"/>을 쓴다(2D 카드 투영도 prim을 근사로 쓴다).</summary>
        public PartShape shape;
        public Vector4 shapeArgs;

        /// <summary>
        /// 파츠별 앵커. <see cref="hasAnchor"/>가 false면 레시피 앵커를 따른다 — enum 기본값(Root)과 "지정 안 함"을
        /// 가르기 위해 플래그를 따로 둔다(모자 레시피의 파츠가 조용히 루트로 떨어지는 걸 막는다).
        /// 레시피 앵커와 다르면 <c>OP_{슬롯}_{앵커}</c> 컨테이너에 들어간다.
        /// </summary>
        public bool hasAnchor;
        public OutfitAnchor anchor;

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

        /// <summary>파츠가 레시피와 다른 앵커를 지정했을 때의 컨테이너 이름(OP_{슬롯}_{앵커}). 이름도 미리 굽는다.</summary>
        private static readonly Dictionary<int, string> ExtraContainerNames = new Dictionary<int, string>();

        internal static string SpawnContainerName(OutfitSlot slot, OutfitAnchor anchor)
        {
            int key = (int)slot * 64 + (int)anchor;
            if (!ExtraContainerNames.TryGetValue(key, out string name))
            {
                name = SpawnPrefix + slot + "_" + anchor;
                ExtraContainerNames[key] = name;
            }
            return name;
        }

        /// <summary>
        /// <see cref="PartColorRole.Skin"/> 파츠가 쓰는 색. <b>지금 이 role을 쓰는 레시피는 없다</b>
        /// (전부 Primary/Secondary/Fixed 계열) — 그래서 외형별 피부색을 여기까지 끌고 오지 않고
        /// 팔레트 기본값을 가리키기만 한다. 값이 갈라지는 것만 막는 게 목적이다.
        /// role을 실제로 쓰는 레시피가 생기면 그때 <c>Apply</c>가 피부색을 인자로 받아야 한다.
        /// </summary>
        private static readonly Color SkinColor = CharacterPalette.DefaultSkin;

        /// <summary>
        /// 의상 spawn 파츠(왕관·망토·날개 등)가 그림자를 드리우는가.
        ///
        /// 오래 <c>Off</c>였다 — 캐릭터 본체는 그림자를 드리우는데 덧붙인 의상만 안 드리워서
        /// 큰 파츠일수록 붕 떠 보였다. 켜는 비용은 캐릭터당 그림자 패스 렌더러 0~6개다.
        /// 모바일에서 문제가 되면 이 상수 하나만 되돌리면 된다.
        /// </summary>
        private const UnityEngine.Rendering.ShadowCastingMode PartShadowMode =
            UnityEngine.Rendering.ShadowCastingMode.On;

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

            OutfitAnchor recipeAnchor = recipe != null ? recipe.anchor : OutfitAnchor.Root;
            string itemId = CountSpawnParts(recipe) > 0 ? ItemIdOf(recipe) : null;

            // spawn 파츠는 앵커별 컨테이너로 간다. 기본 컨테이너(OP_{슬롯})는 레시피 앵커에,
            // 파츠가 앵커를 따로 지정하면 OP_{슬롯}_{앵커}에. **모든 앵커를 매번 훑는다** — 반바지(다리) →
            // 긴바지(파츠 없음)로 갈아입을 때 다리 컨테이너를 비워야 잔상이 안 남는다.
            ApplySpawnGroup(root, slot, recipe, primary, secondary, itemId, recipeAnchor, true);
            for (int a = 0; a < AllAnchors.Length; a++)
                ApplySpawnGroup(root, slot, recipe, primary, secondary, itemId, AllAnchors[a], false);

            if (recipe != null && recipe.parts != null)
            {
                for (int i = 0; i < recipe.parts.Length; i++)
                    if (recipe.parts[i].IsBind) ApplyBound(root, recipe.parts[i]);

                if (recipe.hideNodes != null)
                {
                    for (int i = 0; i < recipe.hideNodes.Length; i++)
                    {
                        Transform t = FindDeep(root, recipe.hideNodes[i]);
                        if (t != null) t.gameObject.SetActive(false);
                    }
                }
            }
        }

        private static readonly OutfitAnchor[] AllAnchors = (OutfitAnchor[])System.Enum.GetValues(typeof(OutfitAnchor));

        /// <summary>파츠가 이 컨테이너 몫인가. 기본 컨테이너는 앵커 미지정 + 레시피 앵커와 같은 지정을 함께 받는다.</summary>
        internal static bool BelongsTo(OutfitPart p, OutfitAnchor recipeAnchor, OutfitAnchor anchor, bool isDefault)
        {
            if (p.IsBind) return false;
            bool own = !p.hasAnchor || p.anchor == recipeAnchor;
            return isDefault ? own : (!own && p.anchor == anchor);
        }

        private static void ApplySpawnGroup(Transform root, OutfitSlot slot, OutfitRecipe recipe, Color primary, Color secondary,
            string itemId, OutfitAnchor anchor, bool isDefault)
        {
            OutfitAnchor recipeAnchor = recipe != null ? recipe.anchor : OutfitAnchor.Root;
            string name = isDefault ? SpawnContainerName(slot) : SpawnContainerName(slot, anchor);

            int need = 0;
            if (recipe != null && recipe.parts != null)
                for (int i = 0; i < recipe.parts.Length; i++)
                    if (BelongsTo(recipe.parts[i], recipeAnchor, anchor, isDefault)) need++;

            Transform container = FindDeep(root, name);
            // 도구처럼 bind만 쓰는 슬롯에 빈 컨테이너를 만들지 않는다 —
            // 모든 플레이어·마네킹마다 쓸모없는 GameObject가 하나씩 늘어난다.
            if (need == 0 && container == null) return;
            if (need > 0)
            {
                Transform parent = ResolveAnchor(root, anchor);
                if (parent == null) parent = root;
                container = EnsureContainer(container, parent, name);
            }

            int spawnIndex = 0;
            if (need > 0)
            {
                for (int i = 0; i < recipe.parts.Length; i++)
                {
                    OutfitPart p = recipe.parts[i];
                    if (!BelongsTo(p, recipeAnchor, anchor, isDefault)) continue;
                    ApplySpawned(root, container, spawnIndex, p, ResolveColor(p, primary, secondary), SurfaceOf(slot, itemId, p.role));
                    spawnIndex++;
                }
            }
            TrimContainer(container, spawnIndex);
        }

        /// <summary>파츠 모양의 메시. <paramref name="bakedSize"/>면 크기가 메시에 구워져 노드 스케일은 1이어야 한다.</summary>
        internal static Mesh GetPartMesh(OutfitPart p, out bool bakedSize)
        {
            bakedSize = false;
            Vector4 a = p.shapeArgs;
            switch (p.shape)
            {
                case PartShape.RoundedBox:
                    bakedSize = true;
                    return ProcMeshLibrary.RoundedBox(p.scale, a.x, 2);
                case PartShape.Capsule:
                {
                    float taper = a.x > 0f ? a.x : 1f;
                    float rTop = 0.5f, rBottom = 0.5f * taper;
                    return ProcMeshLibrary.TaperedCapsule(rTop, rBottom, 2f - rTop - rBottom, 8, 10);
                }
                case PartShape.Drape:
                    return ProcMeshLibrary.DrapeShell(a.x > 0f ? a.x : 180f, a.y > 0f ? a.y : 1f, Mathf.RoundToInt(a.z), a.w);
                case PartShape.Torus:
                    return ProcMeshLibrary.Torus(a.x > 0f ? a.x : 0.06f, 24, 8);
                case PartShape.Gem:
                    return ProcMeshLibrary.Diamond(0.5f, 0.7f, 0.5f);
                case PartShape.NetHead:
                    return ProcMeshLibrary.NetHead(a.x > 0f ? a.x : 0.05f, a.y > 0f ? a.y : 0.8f);
                default:
                    return GetPrimMesh(p.prim);
            }
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
                case OutfitAnchor.Body: return FindDeep(root, "Body");
                case OutfitAnchor.LegL: return FindDeep(root, "LegLPivot");
                case OutfitAnchor.LegR: return FindDeep(root, "LegRPivot");
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
            Mesh mesh = GetPartMesh(p, out bool baked);
            if (mf != null) mf.sharedMesh = mesh;
            t.localPosition = p.pos;
            t.localScale = baked ? Vector3.one : p.scale;
            t.localRotation = Quaternion.Euler(p.euler);

            // 예외 하나: 도구 머리(NetRing)의 **재질**. 빌더는 금속으로 짓는다(총구·렌즈·칼날·오브).
            // 그런데 잠자리채 그물까지 금속이면 주변을 반사해 그물 안쪽이 새까맣게 보인다 —
            // 그물 머리일 때만 천, 나머지는 금속으로 되돌린다(색은 여전히 ApplyPartColor 몫이다).
            if (p.bindName == "NetRing")
            {
                MeshRenderer r = t.GetComponent<MeshRenderer>();
                if (r != null && r.sharedMaterial != null)
                    CharacterPalette.ApplySurface(r.sharedMaterial, p.shape == PartShape.NetHead ? SurfaceKind.Cloth : SurfaceKind.Metal);
            }
        }

        private static void ApplySpawned(Transform root, Transform container, int index, OutfitPart p, Color c,
            SurfaceKind surface)
        {
            Transform t = index < container.childCount ? container.GetChild(index) : null;
            if (t == null)
            {
                // CreatePrimitive를 쓰지 않는다 — 콜라이더가 생겼다 파괴되는 왕복을 피한다.
                GameObject go = new GameObject(SpawnPrefix + index);
                go.transform.SetParent(container, false);
                go.AddComponent<MeshFilter>();
                MeshRenderer r = go.AddComponent<MeshRenderer>();
                r.shadowCastingMode = PartShadowMode;
                r.sharedMaterial = CreatePartMaterial(root, c);
                t = go.transform;
            }

            t.gameObject.SetActive(true);
            MeshFilter mf = t.GetComponent<MeshFilter>();
            Mesh mesh = GetPartMesh(p, out bool baked);
            if (mf != null) mf.sharedMesh = mesh;
            t.localPosition = p.pos;
            t.localScale = baked ? Vector3.one : p.scale;
            t.localRotation = Quaternion.Euler(p.euler);

            MeshRenderer mr = t.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null)
            {
                // spawn 파츠의 머티리얼은 자기 자신만 쓰는 인스턴스라 sharedMaterial 직접 수정이 안전하다.
                // renderer.material(getter)을 쓰면 파츠마다 인스턴스가 하나씩 더 생겨 샌다.
                mr.sharedMaterial.color = c;
                if (mr.sharedMaterial.HasProperty("_BaseColor")) mr.sharedMaterial.SetColor("_BaseColor", c);
                // 재질도 색처럼 매번 다시 준다 — 같은 자식이 왕관(금속)에서 밀짚모자(천)로 재사용된다.
                // 반투명 색에는 걸지 않는다(SceneryMaterials.ApplyFinish와 같은 규칙). 지금 레시피엔 그런 색이 없다.
                if (!SceneryMaterials.IsTranslucent(c)) CharacterPalette.ApplySurface(mr.sharedMaterial, surface);
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
        /// (<see cref="SceneryMaterials.LitShader"/>의 폴백 체인이 그 한 곳이다 — PlayerVisualBuilder.MakeMaterial도 거기서 받는다).
        /// 재질(광택)은 여기서 주지 않는다 — 파츠가 재사용될 때마다 바뀌므로 <see cref="ApplySpawned"/>가 매번 준다
        /// (<see cref="SurfaceOf"/>).
        /// </summary>
        private static Material CreatePartMaterial(Transform root, Color c)
        {
            Shader sh = null;
            MeshRenderer any = root.GetComponentInChildren<MeshRenderer>(true);
            if (any != null && any.sharedMaterial != null) sh = any.sharedMaterial.shader;
            if (sh == null) sh = SceneryMaterials.LitShader;

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

        // ── 파츠 재질(광택) ──────────────────────────────────
        //
        // spawn 파츠는 오래 셰이더 기본 광택(0.5)으로 그려졌다 — 밀짚모자·망토·스카프가 전부 플라스틱처럼 반짝였고,
        // 바로 옆 bind 노드(Cap·Backpack·Body)는 PlayerVisualBuilder.MakeMaterial이 부위 재질을 줘서 한 옷이 두 재료로
        // 갈라져 보였다. 수치는 CharacterPalette 재질표를 그대로 쓴다(사본 없음).
        //
        // OutfitPart에 재질 필드를 두지 않는다(스키마는 data-architect 경계). 슬롯 기본값 + 아이템별 예외 표로 가른다.
        // spawn 파츠엔 이름이 없어(bindName은 bind 전용) 파츠 단위가 아니라 **색 역할** 단위로 가른다 —
        // 같은 역할이면 같은 색이고, 같은 색이면 대개 같은 재료다.

        /// <summary>
        /// 아이템 하나의 재질 — 한 재질에 한 색 역할만 다를 수 있다(왕관의 보석, 수정구의 구슬).
        /// 지금 레시피엔 예외 역할이 둘 이상 필요한 아이템이 없다.
        /// </summary>
        internal readonly struct ItemSurface
        {
            public SurfaceKind Kind { get; }
            public bool HasExcept { get; }
            public PartColorRole ExceptRole { get; }
            public SurfaceKind ExceptKind { get; }

            public ItemSurface(SurfaceKind kind)
            {
                Kind = kind;
                HasExcept = false;
                ExceptRole = default;
                ExceptKind = kind;
            }

            public ItemSurface(SurfaceKind kind, PartColorRole exceptRole, SurfaceKind exceptKind)
            {
                Kind = kind;
                HasExcept = true;
                ExceptRole = exceptRole;
                ExceptKind = exceptKind;
            }

            public SurfaceKind For(PartColorRole role) => HasExcept && role == ExceptRole ? ExceptKind : Kind;
        }

        /// <summary>
        /// 광택이 제 질감인 아이템만 적는다 — 나머지는 <see cref="DefaultSurface"/>(천·가죽)라 무광 계열이다.
        /// 키가 실재하는 레시피인지, 예외 역할이 그 레시피에 실제로 있는지는 <c>OutfitPartSurfaceTests</c>가 고정한다
        /// (오타면 예외도 경고도 없이 조용히 천으로 그려진다).
        ///
        /// 일부러 뺀 것: 꽃 왕관의 금색 꽃술(꽃이다), 마법사 모자 끝 금색 구슬(수놓은 별 모자다), 히어로 마스크 눈
        /// (보조색 역할이 이마 장식과 같다), 오라·후광·네온 팔찌(빛이지 반사가 아니다 — 광택으로는 안 산다).
        /// </summary>
        private static readonly Dictionary<string, ItemSurface> SurfaceOverrides = new Dictionary<string, ItemSurface>
        {
            // 금속 — 왕관 금테·꼭지 장식, 배지, 군번줄. 왕관의 보석(보조색)만 유리 광택.
            ["hat_crown"] = new ItemSurface(SurfaceKind.Metal, PartColorRole.Secondary, SurfaceKind.Wet),
            ["acc_badge"] = new ItemSurface(SurfaceKind.Metal),
            ["acc_dog_tag"] = new ItemSurface(SurfaceKind.Metal),

            // 줄·받침은 금속, 알(수정구·펜던트, 기본색)은 유리 광택.
            ["acc_crystal_orb"] = new ItemSurface(SurfaceKind.Metal, PartColorRole.Primary, SurfaceKind.Wet),
            ["acc_pendant"] = new ItemSurface(SurfaceKind.Metal, PartColorRole.Primary, SurfaceKind.Wet),

            // 렌즈 — 바이저 렌즈(와 같은 색의 안테나)만 유리 광택, 프레임은 모자 기본(천).
            ["hat_cyber_visor"] = new ItemSurface(SurfaceKind.Cloth, PartColorRole.Secondary, SurfaceKind.Wet),

            // 잠금쇠·시료 탱크(보조색)는 금속, 손잡이는 가방 기본(가죽).
            ["bag_science"] = new ItemSurface(SurfaceKind.Leather, PartColorRole.Secondary, SurfaceKind.Metal),

            // 가죽 — 카탈로그 설명이 가죽인 모자("가죽 모자"), 안대, 뿔테.
            ["hat_cowboy"] = new ItemSurface(SurfaceKind.Leather),
            ["bot_cowboy"] = new ItemSurface(SurfaceKind.Leather),     // 레시피가 가죽 챕스뿐이다(청바지는 텍스처)
            ["shoe_crystal"] = new ItemSurface(SurfaceKind.Wet),       // 레시피가 발등 보석뿐이다
            ["outer_crystal"] = new ItemSurface(SurfaceKind.Wet),      // 레시피가 어깨 결정뿐이다
            ["acc_eyepatch"] = new ItemSurface(SurfaceKind.Leather),
            ["acc_glasses"] = new ItemSurface(SurfaceKind.Leather),
        };

        internal static IEnumerable<KeyValuePair<string, ItemSurface>> SurfaceOverrideEntries() => SurfaceOverrides;

        /// <summary>
        /// 슬롯 기본 재질 — bind 노드가 <c>PlayerVisualBuilder</c>에서 받는 재질에 맞춘다(모자 Cap·옷 = 천,
        /// 부츠·배낭 = 가죽). 덧붙인 파츠가 그 노드와 다르면 한 아이템이 두 재료로 갈라져 보인다.
        /// 도구는 전부 bind라(손잡이 가죽·망 금속은 빌더 몫) 여기 올 일이 없다.
        /// </summary>
        // ── 몸 전체 형태(스타일) ─────────────────────────────
        //
        // 레시피가 "덧붙이는 것"을 정한다면, 스타일은 **기존 노드를 누가 칠하는지**를 정한다 —
        // 소매 길이(팔을 피부로 둘지 상의 색으로 둘지), 겉옷 형태(몸통을 겉옷이 덮는지·앞이 열렸는지·망토라 안 덮는지),
        // 반바지(다리를 피부로), 샌들(발을 피부로). 판정은 CharacterOutfitManager.ApplyToCharacter가 한다.
        // 표에 없는 아이템은 기본값(반소매·열린 자켓·긴 바지·신발)이다.

        private static readonly Dictionary<string, SleeveLength> TopSleeves = new Dictionary<string, SleeveLength>
        {
            ["top_lab"] = SleeveLength.Long,
            ["top_cowboy"] = SleeveLength.Long,      // 조끼 속 체크 셔츠
            ["top_hero_suit"] = SleeveLength.Long,
            ["top_ninja"] = SleeveLength.Long,
            ["top_pirate"] = SleeveLength.Long,
            ["top_cyber"] = SleeveLength.Long,
            ["top_military"] = SleeveLength.Long,
        };

        /// <summary>소매가 상의의 **보조색**인 옷 — 조끼는 소매가 속셔츠(흰색)다.</summary>
        private static readonly HashSet<string> SleeveUsesSecondary = new HashSet<string> { "top_vest" };

        private static readonly Dictionary<string, OuterForm> OuterForms = new Dictionary<string, OuterForm>
        {
            ["outer_legendary"] = OuterForm.Cape,
            ["outer_wizard"] = OuterForm.Robe,
        };

        private static readonly Dictionary<string, LegForm> LegForms = new Dictionary<string, LegForm>
        {
            ["bot_shorts"] = LegForm.Shorts,
        };

        private static readonly Dictionary<string, FootForm> FootForms = new Dictionary<string, FootForm>
        {
            ["shoe_sandals"] = FootForm.Sandal,
        };

        public static SleeveLength SleeveOf(string topId) =>
            topId != null && TopSleeves.TryGetValue(topId, out SleeveLength v) ? v : SleeveLength.Short;

        public static bool SleeveIsSecondary(string topId) => topId != null && SleeveUsesSecondary.Contains(topId);

        public static OuterForm OuterFormOf(string outerId) =>
            outerId != null && OuterForms.TryGetValue(outerId, out OuterForm v) ? v : OuterForm.OpenJacket;

        public static LegForm LegFormOf(string bottomId) =>
            bottomId != null && LegForms.TryGetValue(bottomId, out LegForm v) ? v : LegForm.Long;

        public static FootForm FootFormOf(string shoeId) =>
            shoeId != null && FootForms.TryGetValue(shoeId, out FootForm v) ? v : FootForm.Shoe;

        internal static IEnumerable<string> StyledItemIds()
        {
            foreach (string k in TopSleeves.Keys) yield return k;
            foreach (string k in SleeveUsesSecondary) yield return k;
            foreach (string k in OuterForms.Keys) yield return k;
            foreach (string k in LegForms.Keys) yield return k;
            foreach (string k in FootForms.Keys) yield return k;
        }

        internal static SurfaceKind DefaultSurface(OutfitSlot slot)
        {
            switch (slot)
            {
                case OutfitSlot.Shoes:
                case OutfitSlot.Backpack:
                    return SurfaceKind.Leather;
                default:
                    return SurfaceKind.Cloth;
            }
        }

        /// <summary>spawn 파츠 하나의 재질. 순수 함수라 셰이더 없이 테스트한다.</summary>
        internal static SurfaceKind SurfaceOf(OutfitSlot slot, string itemId, PartColorRole role)
        {
            if (itemId != null && SurfaceOverrides.TryGetValue(itemId, out ItemSurface s)) return s.For(role);
            return DefaultSurface(slot);
        }

        /// <summary>
        /// 레시피 → itemId 역참조. <see cref="Apply"/>는 itemId를 받지 않는다 — <see cref="ExactRecipes"/>의 값이
        /// 아이템마다 고유 인스턴스라(테스트가 고정) 레시피가 곧 아이템이다. 호출부에 인자를 늘리면 레시피와 id가
        /// 어긋나게 넘어올 자리가 생긴다. 도구 레시피는 여러 id가 한 인스턴스를 나누지만 전부 bind라 재질을 묻지 않는다.
        /// </summary>
        private static Dictionary<OutfitRecipe, string> recipeItemIds;

        internal static string ItemIdOf(OutfitRecipe recipe)
        {
            if (recipe == null) return null;
            if (recipeItemIds == null)
            {
                Dictionary<OutfitRecipe, string> map = new Dictionary<OutfitRecipe, string>();
                foreach (KeyValuePair<string, OutfitRecipe> kv in ExactRecipes) map[kv.Value] = kv.Key;
                recipeItemIds = map;
            }
            return recipeItemIds.TryGetValue(recipe, out string id) ? id : null;
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

        // 고정색은 레시피 표(ExactRecipes)보다 **먼저** 선언한다 — 정적 초기화는 적힌 순서라 뒤에 두면 표가 검정(0,0,0)을 읽는다.
        private static readonly Color Gold = new Color(1f, 0.9f, 0.32f);
        private static readonly Color Bone = new Color(0.95f, 0.94f, 0.9f);
        private static readonly Color SoleWhite = new Color(0.95f, 0.95f, 0.94f);
        private static readonly Color Steel = new Color(0.6f, 0.62f, 0.66f);

        /// <summary>spawn 파츠 + 고정색 + 회전.</summary>
        private static OutfitPart SF(PrimitiveType prim, Vector3 pos, Vector3 scale, Color color, Vector3 euler)
        {
            OutfitPart p = SF(prim, pos, scale, color);
            p.euler = euler;
            return p;
        }

        /// <summary>파츠에 모양을 준다(bind 파츠에도 쓴다 — 잠자리채 머리처럼 노드 메시를 통째로 바꿀 때).</summary>
        private static OutfitPart WithShape(OutfitPart p, PartShape shape, Vector4 args)
        {
            p.shape = shape;
            p.shapeArgs = args;
            return p;
        }

        /// <summary>보석(팔면체) — 크리스털 결정·왕관 뾰족 장식.</summary>
        private static OutfitPart GemPart(Vector3 pos, Vector3 scale, Vector3 euler, PartColorRole role)
        {
            return new OutfitPart { prim = PrimitiveType.Sphere, shape = PartShape.Gem, pos = pos, scale = scale, euler = euler, role = role };
        }

        /// <summary>모양 헬퍼로 만든 파츠에 고정색을 준다.</summary>
        private static OutfitPart Fixed(OutfitPart p, Color color)
        {
            p.role = PartColorRole.Fixed;
            p.fixedColor = color;
            return p;
        }

        // ── 모양·앵커 헬퍼(2026-09-30) ──
        // prim은 2D 카드 투영(CharacterPortraitRenderer)이 근사로 쓰므로 모양에 가까운 것을 채워 둔다.

        /// <summary>파츠에 앵커를 준다 — 레시피 앵커와 다르면 OP_{슬롯}_{앵커} 컨테이너로 간다.</summary>
        private static OutfitPart At(OutfitAnchor anchor, OutfitPart p)
        {
            p.hasAnchor = true;
            p.anchor = anchor;
            return p;
        }

        /// <summary>둥근 상자 — 크기를 메시에 굽는다(노드 스케일 1).</summary>
        private static OutfitPart RB(Vector3 pos, Vector3 size, float radius, Vector3 euler, PartColorRole role)
        {
            return new OutfitPart
            {
                prim = PrimitiveType.Cube, shape = PartShape.RoundedBox, shapeArgs = new Vector4(radius, 0f, 0f, 0f),
                pos = pos, scale = size, euler = euler, role = role,
            };
        }

        /// <summary>테이퍼 캡슐(높이 2·반지름 0.5 규약 × scale). taper = 아래 반지름 비율.</summary>
        private static OutfitPart Cap(Vector3 pos, Vector3 scale, float taper, Vector3 euler, PartColorRole role)
        {
            return new OutfitPart
            {
                prim = PrimitiveType.Capsule, shape = PartShape.Capsule, shapeArgs = new Vector4(taper, 0f, 0f, 0f),
                pos = pos, scale = scale, euler = euler, role = role,
            };
        }

        /// <summary>늘어진 천 — 원점 = 윗변 중앙, 뒤(−Z)를 감싸며 아래로. scale = (지름 x, 길이, 지름 z).</summary>
        private static OutfitPart Drape(Vector3 pos, Vector3 scale, float arcDeg, float flare, int folds, float foldDepth,
            Vector3 euler, PartColorRole role)
        {
            return new OutfitPart
            {
                prim = PrimitiveType.Cube, shape = PartShape.Drape, shapeArgs = new Vector4(arcDeg, flare, folds, foldDepth),
                pos = pos, scale = scale, euler = euler, role = role,
            };
        }

        /// <summary>고리(토러스) — scale = (바깥 지름 근사 x, 관 두께 배율 y, 지름 z). tube = 관 반지름(단위 큰 반지름 0.5 기준).</summary>
        private static OutfitPart Ring(Vector3 pos, Vector3 scale, float tube, Vector3 euler, PartColorRole role)
        {
            return new OutfitPart
            {
                prim = PrimitiveType.Cylinder, shape = PartShape.Torus, shapeArgs = new Vector4(tube, 0f, 0f, 0f),
                pos = pos, scale = scale, euler = euler, role = role,
            };
        }

        /// <summary>좌우 거울(x·Y회전·Z회전 반전).</summary>
        internal static OutfitPart Mirror(OutfitPart p)
        {
            p.pos.x = -p.pos.x;
            p.euler.y = -p.euler.y;
            p.euler.z = -p.euler.z;
            return p;
        }

        /// <summary>
        /// 양다리. <b>오른다리 관절 기준 좌표</b>(다리 중심 x = 0, 바깥쪽 = +X)로 적으면 왼다리는 거울로 만든다.
        /// 다리 관절 로컬: 무릎 ≈ y −0.14, 발목 ≈ y −0.3, 부츠 중심 (0, −0.36, 0.07).
        /// </summary>
        private static OutfitPart[] Legs(params OutfitPart[] rightLeg)
        {
            OutfitPart[] all = new OutfitPart[rightLeg.Length * 2];
            for (int i = 0; i < rightLeg.Length; i++)
            {
                all[i * 2] = At(OutfitAnchor.LegR, rightLeg[i]);
                all[i * 2 + 1] = At(OutfitAnchor.LegL, Mirror(rightLeg[i]));
            }
            return all;
        }

        private static OutfitPart[] Concat(params OutfitPart[][] groups)
        {
            List<OutfitPart> all = new List<OutfitPart>();
            for (int i = 0; i < groups.Length; i++) all.AddRange(groups[i]);
            return all.ToArray();
        }

        // 몸통 앞면(몸통 로컬 z). 남 0.19 / 여 0.17 — 레시피는 성별을 모르므로 그 사이에 둔다(여자에선 0.01 뜬다).
        private const float TorsoFront = 0.18f;
        // 몸통 밑단(몸통 로컬 y). 몸통 높이 0.46의 절반.
        private const float TorsoHem = -0.23f;

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
                    // 고리는 토러스(밧줄 고리) — 원기둥 판이면 구멍이 막혀 올가미로 안 읽힌다.
                    WithShape(B("NetRing", PrimitiveType.Cylinder, V(0.35f, 0.94f, 0.06f), V(0.28f, 0.28f, 0.28f), V(-20f, 0f, 0f), PartColorRole.Secondary),
                        PartShape.Torus, new Vector4(0.07f, 0f, 0f, 0f)),
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

            // 기본 잠자리채(else). 머리는 **세운 테 + 뒤로 늘어진 그물**(ProcMeshLibrary.NetHead, 원점 = 테 아래 끝).
            // 옛 모양은 막힌 원판이라 자루와 합쳐 망치로 보였다(2026-09-30 전수 캡처). 테가 XY 평면이라
            // 뒤에서 내려다보는 필드 카메라에 정면으로 잡힌다 — 옛 원판이 edge-on으로 사라지던 회귀(rot(0,0,90))와 반대 방향이다.
            new ToolEntry { keys = null, recipe = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    B("NetHandle", PrimitiveType.Cylinder, V(0.29f, 0.74f, 0f), V(0.04f, 0.40f, 0.04f), Vector3.zero, PartColorRole.Primary),
                    WithShape(B("NetRing", PrimitiveType.Cylinder, V(0.29f, 1.14f, 0f), V(0.22f, 0.22f, 0.22f), V(-25f, 0f, 0f), PartColorRole.Secondary),
                        PartShape.NetHead, new Vector4(0.05f, 0.85f, 0f, 0f)),
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

        /// <summary>
        /// 머리카락 중 모자 선 위로 솟는 부분(올림머리 스파이크·번)을 담는 <c>PlayerVisualBuilder</c>의 컨테이너.
        /// 정수리를 덮는 모자는 이것까지 숨긴다. 머리띠·마스크·바이저처럼 정수리가 열린 모자는 <see cref="HideCap"/>만 쓴다
        /// (<c>OutfitShapeLibraryTests</c>가 "덮는 모자는 숨기고 열린 모자 목록은 명시"를 고정한다).
        /// 레시피가 없는 모자(기본 캡 계열)는 <c>CharacterOutfitManager</c>가 Cap이 보일 때 숨긴다.
        /// </summary>
        public const string HairCrownNode = "HairCrown";

        private static readonly string[] HideCapAndCrown = { "Cap", "CapBrim", HairCrownNode };
        private static readonly string[] HideBackpack = { "Backpack" };

        private static readonly Dictionary<string, OutfitRecipe> ExactRecipes = new Dictionary<string, OutfitRecipe>
        {
            // 밀짚모자: 머리의 1.5배 넓은 챙 + 낮은 돔 + 띠. 옛 챙(지름 0.68)은 머리(0.70)보다 좁아 띠처럼 보였다.
            ["hat_straw"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCapAndCrown,
                parts = new[]
                {
                    S(PrimitiveType.Cylinder, V(0f, 0.20f, 0f), V(1.04f, 0.016f, 1.04f), PartColorRole.Primary),
                    S(PrimitiveType.Sphere,   V(0f, 0.27f, 0f), V(0.66f, 0.34f, 0.66f), PartColorRole.Primary),
                    S(PrimitiveType.Cylinder, V(0f, 0.24f, 0f), V(0.67f, 0.035f, 0.67f), PartColorRole.PrimaryDark),
                    Ring(V(0f, 0.205f, 0f), V(1.04f, 0.2f, 1.04f), 0.02f, Vector3.zero, PartColorRole.PrimaryDark),
                },
            },

            // 사파리 헬멧: 중간 챙 + 반구 돔 + 정수리 능선
            ["hat_safari"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCapAndCrown,
                parts = new[]
                {
                    S(PrimitiveType.Cylinder, V(0f, 0.19f, 0.01f), V(0.62f, 0.022f, 0.60f), PartColorRole.Primary),
                    S(PrimitiveType.Sphere,   V(0f, 0.22f, 0f),    V(0.76f, 0.46f, 0.76f),  PartColorRole.Primary),
                    S(PrimitiveType.Cube,     V(0f, 0.42f, 0f),    V(0.04f, 0.06f, 0.44f),  PartColorRole.PrimaryDark),
                },
            },

            // 프로 탐험가 모자: 앞뒤로 긴 챙 + 앞이 눌린 크라운 + 금색 띠(보조색). 오래 레시피가 없어 캡 색만 바뀌었다.
            ["hat_explorer_pro"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCapAndCrown,
                parts = new[]
                {
                    SR(PrimitiveType.Cylinder, V(0f, 0.2f, 0.02f), V(0.86f, 0.02f, 0.94f), V(-4f, 0f, 0f), PartColorRole.Primary),
                    S(PrimitiveType.Sphere,    V(0f, 0.3f, 0f),    V(0.62f, 0.4f, 0.62f),  PartColorRole.Primary),
                    S(PrimitiveType.Cube,      V(0f, 0.44f, 0.04f), V(0.08f, 0.05f, 0.3f), PartColorRole.PrimaryDark),
                    S(PrimitiveType.Cylinder,  V(0f, 0.25f, 0f),   V(0.63f, 0.045f, 0.63f), PartColorRole.Secondary),
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
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCapAndCrown,
                parts = new[]
                {
                    S(PrimitiveType.Sphere,   V(0f, 0.18f, -0.01f), V(0.78f, 0.48f, 0.78f), PartColorRole.Primary),
                    S(PrimitiveType.Cylinder, V(0f, 0.10f, 0f),     V(0.80f, 0.025f, 0.80f), PartColorRole.PrimaryDark),
                    SR(PrimitiveType.Cube,    V(0f, 0.36f, 0.16f),  V(0.08f, 0.18f, 0.09f), V(35f, 0f, 0f),  PartColorRole.PrimaryDark),
                    SR(PrimitiveType.Cube,    V(0f, 0.50f, 0.31f),  V(0.055f, 0.20f, 0.06f), V(-28f, 0f, 0f), PartColorRole.PrimaryDark),
                },
            },

            // 곤충왕 왕관: 머리(머리카락) 위에 얹히는 굵은 띠 + 보석 뾰족 장식 5 + 앞 보석
            ["hat_crown"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCapAndCrown,
                parts = new[]
                {
                    S(PrimitiveType.Cylinder, V(0f, 0.31f, 0f), V(0.6f, 0.07f, 0.6f), PartColorRole.Primary),
                    Ring(V(0f, 0.25f, 0f), V(0.62f, 0.3f, 0.62f), 0.05f, Vector3.zero, PartColorRole.Primary),
                    GemPart(V(0f, 0.44f, 0.27f),     V(0.1f, 0.2f, 0.1f),  Vector3.zero, PartColorRole.Primary),
                    GemPart(V(0.26f, 0.43f, 0.08f),  V(0.09f, 0.17f, 0.09f), Vector3.zero, PartColorRole.Primary),
                    GemPart(V(-0.26f, 0.43f, 0.08f), V(0.09f, 0.17f, 0.09f), Vector3.zero, PartColorRole.Primary),
                    GemPart(V(0.16f, 0.43f, -0.22f), V(0.09f, 0.17f, 0.09f), Vector3.zero, PartColorRole.Primary),
                    GemPart(V(-0.16f, 0.43f, -0.22f), V(0.09f, 0.17f, 0.09f), Vector3.zero, PartColorRole.Primary),
                    S(PrimitiveType.Sphere,   V(0f, 0.31f, 0.3f), V(0.1f, 0.1f, 0.06f), PartColorRole.Secondary),
                    SF(PrimitiveType.Sphere,  V(0f, 0.55f, 0.27f), V(0.05f, 0.05f, 0.05f), Gold),
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

            // 카우보이 모자: 넓은 챙(양옆이 말려 올라감) + 높은 크라운 + 정수리 홈 + 띠
            ["hat_cowboy"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCapAndCrown,
                parts = new[]
                {
                    S(PrimitiveType.Cylinder, V(0f, 0.20f, 0f),      V(0.62f, 0.02f, 0.92f), PartColorRole.Primary),
                    SR(PrimitiveType.Cylinder, V(-0.33f, 0.25f, 0f), V(0.32f, 0.02f, 0.78f), V(0f, 0f, -32f), PartColorRole.Primary),
                    SR(PrimitiveType.Cylinder, V(0.33f, 0.25f, 0f),  V(0.32f, 0.02f, 0.78f), V(0f, 0f, 32f),  PartColorRole.Primary),
                    S(PrimitiveType.Cylinder, V(0f, 0.34f, 0f),      V(0.52f, 0.15f, 0.56f), PartColorRole.Primary),
                    S(PrimitiveType.Cube,     V(0f, 0.48f, 0f),      V(0.10f, 0.05f, 0.34f), PartColorRole.PrimaryDark),
                    S(PrimitiveType.Cylinder, V(0f, 0.25f, 0f),      V(0.54f, 0.035f, 0.58f), PartColorRole.Secondary),
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
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCapAndCrown,
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
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCapAndCrown,
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
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCapAndCrown,
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
                anchor = OutfitAnchor.HatRoot, hideNodes = HideCapAndCrown,
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
                    // 테(위·아래·바깥·안쪽 막대)만 — 렌즈 자리는 비운다. 예전엔 z 0.21의 속이 찬 판이라
                    // 눈이 z 0.22~0.25(눈꺼풀 포함)로 앞에 나온 뒤로는 판이 눈 **뒤**에 묻혀 거의 안 보였다.
                    // 눈 중심 월드 (±0.072, 1.202), 크기 0.09×0.10 → 테는 한 치수 크게, 눈꺼풀(0.249) 앞 z 0.26.
                    S(PrimitiveType.Cube, V(-0.072f, 1.255f, 0.26f), V(0.115f, 0.014f, 0.012f), PartColorRole.Primary),
                    S(PrimitiveType.Cube, V(-0.072f, 1.150f, 0.26f), V(0.115f, 0.014f, 0.012f), PartColorRole.Primary),
                    S(PrimitiveType.Cube, V(-0.128f, 1.202f, 0.26f), V(0.014f, 0.118f, 0.012f), PartColorRole.Primary),
                    S(PrimitiveType.Cube, V(-0.016f, 1.202f, 0.26f), V(0.014f, 0.118f, 0.012f), PartColorRole.Primary),
                    S(PrimitiveType.Cube, V(0.072f, 1.255f, 0.26f),  V(0.115f, 0.014f, 0.012f), PartColorRole.Primary),
                    S(PrimitiveType.Cube, V(0.072f, 1.150f, 0.26f),  V(0.115f, 0.014f, 0.012f), PartColorRole.Primary),
                    S(PrimitiveType.Cube, V(0.128f, 1.202f, 0.26f),  V(0.014f, 0.118f, 0.012f), PartColorRole.Primary),
                    S(PrimitiveType.Cube, V(0.016f, 1.202f, 0.26f),  V(0.014f, 0.118f, 0.012f), PartColorRole.Primary),
                    S(PrimitiveType.Cube, V(0f, 1.215f, 0.262f),     V(0.022f, 0.012f, 0.012f), PartColorRole.Primary),
                },
            },

            // 해적 안대: 한쪽 눈만 가리고 머리끈이 사선으로 지난다(옛 코드는 안경 한쪽을 끄기만 했다)
            ["acc_eyepatch"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = new[]
                {
                    // 눈(눈꺼풀 포함 z ≤ 0.25) **앞**에 둔다 — 뒤에 두면 가려야 할 눈이 안대를 뚫고 보인다.
                    S(PrimitiveType.Cube,  V(-0.072f, 1.20f, 0.258f), V(0.12f, 0.115f, 0.02f), PartColorRole.Primary),
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

            // ── 가방(Backpack) — **몸통 로컬**(몸통 중심 = 루트 y 0.77). Backpack 노드가 몸통 자식(0, 0.03, −0.22)이라
            // 걷기에서 몸통과 함께 튄다 — 레시피도 같은 앵커여야 날개·가시가 가방에서 떨어지지 않는다(2026-09-30 루트 → 몸통).

            // 드래곤 배낭: 기본 상자 + 박쥐 날개 + 등뼈 가시
            ["bag_dragon"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Body,
                parts = new[]
                {
                    SR(PrimitiveType.Cube, V(-0.26f, 0.21f, -0.28f), V(0.28f, 0.30f, 0.02f), V(0f, -25f, 28f), PartColorRole.Secondary),
                    SR(PrimitiveType.Cube, V(0.26f, 0.21f, -0.28f),  V(0.28f, 0.30f, 0.02f), V(0f, 25f, -28f), PartColorRole.Secondary),
                    SR(PrimitiveType.Cube, V(0f, 0.19f, -0.32f),     V(0.05f, 0.10f, 0.05f), V(20f, 0f, 0f),   PartColorRole.PrimaryDark),
                    SR(PrimitiveType.Cube, V(0f, 0.07f, -0.32f),     V(0.05f, 0.09f, 0.05f), V(20f, 0f, 0f),   PartColorRole.PrimaryDark),
                },
            },

            // 요정 날개 가방: 기본 상자 + 상하 요정 날개 4장
            ["bag_fairy"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Body,
                parts = new[]
                {
                    SR(PrimitiveType.Cube, V(-0.20f, 0.23f, -0.26f), V(0.24f, 0.30f, 0.015f), V(0f, -20f, 20f), PartColorRole.Secondary),
                    SR(PrimitiveType.Cube, V(0.20f, 0.23f, -0.26f),  V(0.24f, 0.30f, 0.015f), V(0f, 20f, -20f), PartColorRole.Secondary),
                    SR(PrimitiveType.Cube, V(-0.16f, 0.01f, -0.26f), V(0.16f, 0.20f, 0.015f), V(0f, -20f, 14f), PartColorRole.Secondary),
                    SR(PrimitiveType.Cube, V(0.16f, 0.01f, -0.26f),  V(0.16f, 0.20f, 0.015f), V(0f, 20f, -14f), PartColorRole.Secondary),
                },
            },

            // 어깨가방: 등 상자를 숨기고 어깨끈 + 옆구리 가방으로 바꾼다
            ["bag_satchel"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Body, hideNodes = HideBackpack,
                parts = new[]
                {
                    SR(PrimitiveType.Cube, V(0f, 0.13f, 0.20f),     V(0.07f, 0.40f, 0.03f), V(0f, 0f, 24f),  PartColorRole.PrimaryDark),
                    SR(PrimitiveType.Cube, V(0f, 0.13f, -0.20f),    V(0.07f, 0.40f, 0.03f), V(0f, 0f, -24f), PartColorRole.PrimaryDark),
                    S(PrimitiveType.Cube,  V(-0.28f, -0.15f, -0.02f), V(0.16f, 0.20f, 0.24f), PartColorRole.Primary),
                    S(PrimitiveType.Cube,  V(-0.28f, -0.06f, -0.02f), V(0.17f, 0.05f, 0.25f), PartColorRole.PrimaryDark),
                },
            },

            // 연구 장비함: 기본 상자 + 잠금쇠 + 시료 탱크 + 손잡이
            ["bag_science"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Body,
                parts = new[]
                {
                    S(PrimitiveType.Cube,     V(0f, 0.13f, -0.31f),    V(0.16f, 0.03f, 0.02f), PartColorRole.Secondary),
                    S(PrimitiveType.Cube,     V(0f, -0.01f, -0.31f),    V(0.16f, 0.03f, 0.02f), PartColorRole.Secondary),
                    S(PrimitiveType.Cylinder, V(0.16f, 0.09f, -0.30f), V(0.09f, 0.13f, 0.09f), PartColorRole.Secondary),
                    S(PrimitiveType.Cube,     V(0f, 0.23f, -0.24f),    V(0.14f, 0.03f, 0.04f), PartColorRole.PrimaryDark),
                },
            },

            // ── 상의(Top) — 몸통 로컬. 무늬는 OutfitPatternLibrary(텍스처), 여기는 **실루엣이 바뀌는 것**만 ──
            // 상의 레시피는 상의가 보일 때만 적용된다(겉옷을 벗었거나 망토일 때) — CharacterOutfitManager 참고.

            // 카우보이 조끼: 조끼 앞자락 밑단의 가죽 술(가운데 속셔츠 부분은 비운다)
            ["top_cowboy"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Body,
                parts = new[]
                {
                    Cap(V(-0.20f, TorsoHem - 0.03f, TorsoFront + 0.004f), V(0.024f, 0.035f, 0.014f), 0.4f, V(0f, 0f, -4f), PartColorRole.Primary),
                    Cap(V(-0.16f, TorsoHem - 0.035f, TorsoFront + 0.006f), V(0.024f, 0.04f, 0.014f), 0.4f, Vector3.zero, PartColorRole.Primary),
                    Cap(V(-0.12f, TorsoHem - 0.03f, TorsoFront + 0.008f), V(0.024f, 0.035f, 0.014f), 0.4f, V(0f, 0f, 4f), PartColorRole.Primary),
                    Cap(V(0.12f, TorsoHem - 0.03f, TorsoFront + 0.008f), V(0.024f, 0.035f, 0.014f), 0.4f, V(0f, 0f, -4f), PartColorRole.Primary),
                    Cap(V(0.16f, TorsoHem - 0.035f, TorsoFront + 0.006f), V(0.024f, 0.04f, 0.014f), 0.4f, Vector3.zero, PartColorRole.Primary),
                    Cap(V(0.20f, TorsoHem - 0.03f, TorsoFront + 0.004f), V(0.024f, 0.035f, 0.014f), 0.4f, V(0f, 0f, 4f), PartColorRole.Primary),
                },
            },

            // ── 하의(Bottom) — 다리 관절 로컬(오른다리 기준, Legs가 왼다리를 거울로 만든다) + 몸통 로컬 ──
            // 무늬(청바지·위장·줄무늬 등)는 텍스처. 여기는 다리 모양이 바뀌는 것만.

            // 반바지: 다리는 피부(LegForm.Shorts), 허벅지만 감싸는 천 + 밑단 접힘
            ["bot_shorts"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = Legs(
                    Cap(V(0f, -0.03f, 0f), V(0.235f, 0.075f, 0.235f), 0.95f, Vector3.zero, PartColorRole.Primary),
                    Ring(V(0f, -0.1f, 0f), V(0.245f, 0.3f, 0.245f), 0.06f, Vector3.zero, PartColorRole.PrimaryDark)),
            },

            // 카고 팬츠: 허벅지 바깥 입체 주머니(덮개 포함)
            ["bot_cargo"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = Legs(
                    RB(V(0.1f, -0.13f, 0.005f), V(0.03f, 0.1f, 0.1f), 0.008f, Vector3.zero, PartColorRole.Primary),
                    RB(V(0.106f, -0.085f, 0.005f), V(0.028f, 0.025f, 0.105f), 0.006f, Vector3.zero, PartColorRole.PrimaryDark)),
            },

            // 멜빵바지: 가슴판 + 어깨끈(앞·어깨 위·등) + 금단추 + 허리 — 몸통 로컬. 자켓을 입으면 앞섶 사이로 보인다.
            ["bot_overalls"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Body,
                parts = new[]
                {
                    RB(V(0f, -0.02f, TorsoFront + 0.034f), V(0.21f, 0.2f, 0.016f), 0.01f, Vector3.zero, PartColorRole.Primary),
                    RB(V(0f, 0.065f, TorsoFront + 0.04f), V(0.09f, 0.06f, 0.008f), 0.006f, Vector3.zero, PartColorRole.PrimaryDark),   // 가슴 주머니
                    RB(V(-0.08f, 0.165f, TorsoFront + 0.03f), V(0.04f, 0.13f, 0.012f), 0.005f, Vector3.zero, PartColorRole.Primary),
                    RB(V(0.08f, 0.165f, TorsoFront + 0.03f), V(0.04f, 0.13f, 0.012f), 0.005f, Vector3.zero, PartColorRole.Primary),
                    RB(V(-0.08f, 0.236f, 0f), V(0.04f, 0.012f, 0.4f), 0.005f, Vector3.zero, PartColorRole.Primary),
                    RB(V(0.08f, 0.236f, 0f), V(0.04f, 0.012f, 0.4f), 0.005f, Vector3.zero, PartColorRole.Primary),
                    RB(V(-0.07f, 0.06f, -TorsoFront - 0.022f), V(0.04f, 0.33f, 0.012f), 0.005f, V(0f, 0f, 8f), PartColorRole.Primary),
                    RB(V(0.07f, 0.06f, -TorsoFront - 0.022f), V(0.04f, 0.33f, 0.012f), 0.005f, V(0f, 0f, -8f), PartColorRole.Primary),
                    RB(V(0f, -0.2f, 0f), V(0.47f, 0.065f, 0.4f), 0.02f, Vector3.zero, PartColorRole.Primary),                          // 허리
                    SF(PrimitiveType.Sphere, V(-0.08f, 0.1f, TorsoFront + 0.044f), V(0.028f, 0.028f, 0.014f), Gold),
                    SF(PrimitiveType.Sphere, V(0.08f, 0.1f, TorsoFront + 0.044f), V(0.028f, 0.028f, 0.014f), Gold),
                },
            },

            // 카우보이 팬츠: 청바지(텍스처) 위에 앞·바깥을 감싸는 가죽 챕스
            ["bot_cowboy"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = Legs(
                    Drape(V(0f, 0.05f, 0f), V(0.25f, 0.34f, 0.25f), 170f, 1.06f, 0, 0f, V(0f, 200f, 0f), PartColorRole.Secondary)),
            },

            // ── 신발(Shoes) — 다리 관절 로컬(오른다리 기준). 부츠 노드 중심 (0, −0.36, 0.07), 크기 0.21×0.15×0.30 ──

            // 운동화: 두툼한 흰 밑창
            ["shoe_sneakers"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = Legs(
                    Fixed(RB(V(0f, -0.428f, 0.072f), V(0.228f, 0.042f, 0.325f), 0.016f, Vector3.zero, PartColorRole.Fixed), SoleWhite)),
            },

            // 샌들: 발은 피부(FootForm.Sandal) + 가죽 밑창 + 발등 끈 둘 + 발목 끈
            ["shoe_sandals"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = Legs(
                    RB(V(0f, -0.43f, 0.07f), V(0.23f, 0.03f, 0.32f), 0.01f, Vector3.zero, PartColorRole.Primary),
                    RB(V(0f, -0.283f, 0.14f), V(0.225f, 0.022f, 0.045f), 0.008f, Vector3.zero, PartColorRole.PrimaryDark),
                    RB(V(0f, -0.283f, 0.03f), V(0.225f, 0.022f, 0.045f), 0.008f, Vector3.zero, PartColorRole.PrimaryDark),
                    Ring(V(0f, -0.27f, 0f), V(0.185f, 0.25f, 0.185f), 0.07f, Vector3.zero, PartColorRole.PrimaryDark)),
            },

            // 장화: 무릎 아래까지 오는 목 + 입구 테
            ["shoe_waders"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = Legs(
                    Cap(V(0f, -0.2f, 0f), V(0.235f, 0.12f, 0.235f), 0.95f, Vector3.zero, PartColorRole.Primary),
                    Ring(V(0f, -0.075f, 0f), V(0.25f, 0.3f, 0.25f), 0.07f, Vector3.zero, PartColorRole.PrimaryDark)),
            },

            // 로켓 부츠: 뒤꿈치 분사구 + 불꽃 + 바깥 날개
            ["shoe_rocket"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = Legs(
                    SF(PrimitiveType.Cylinder, V(0f, -0.35f, -0.1f), V(0.1f, 0.03f, 0.1f), Steel, V(90f, 0f, 0f)),
                    Cap(V(0f, -0.35f, -0.16f), V(0.075f, 0.06f, 0.075f), 0.2f, V(90f, 0f, 0f), PartColorRole.Secondary),
                    RB(V(0.11f, -0.33f, -0.05f), V(0.014f, 0.09f, 0.09f), 0.004f, V(0f, 0f, -12f), PartColorRole.Secondary)),
            },

            // 크리스털 구두: 발등 보석
            ["shoe_crystal"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = Legs(
                    SR(PrimitiveType.Sphere, V(0f, -0.29f, 0.17f), V(0.06f, 0.045f, 0.06f), V(0f, 45f, 0f), PartColorRole.Secondary)),
            },

            // 카우보이 부츠: 굽 + 뾰족한 앞코 + 박차(고리 + 축)
            ["shoe_cowboy"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Root,
                parts = Legs(
                    RB(V(0f, -0.445f, -0.035f), V(0.12f, 0.05f, 0.1f), 0.008f, Vector3.zero, PartColorRole.PrimaryDark),
                    Cap(V(0f, -0.385f, 0.2f), V(0.15f, 0.07f, 0.1f), 0.35f, V(-90f, 0f, 0f), PartColorRole.Primary),
                    SR(PrimitiveType.Cube, V(0f, -0.39f, -0.1f), V(0.012f, 0.012f, 0.04f), Vector3.zero, PartColorRole.Secondary),
                    Ring(V(0f, -0.39f, -0.125f), V(0.05f, 0.3f, 0.05f), 0.14f, V(0f, 0f, 90f), PartColorRole.Secondary)),
            },

            // ── 겉옷(Outerwear) — **몸통 로컬**(2026-09-30 루트 → 몸통: 걷기에서 몸통과 함께 튄다).
            // 몸통·팔·셔츠 판을 누가 칠할지는 겉옷 형태(OuterForm)가 정하고 ApplyToCharacter가 칠한다. 여기선 덧붙인다 ──

            // 전설의 망토(망토 형태 — 몸통은 상의 그대로): 어깨에서 무릎까지 퍼지며 주름진 망토 + 선 깃 + 금 브로치
            ["outer_legendary"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Body,
                parts = new[]
                {
                    Drape(V(0f, 0.2f, -0.02f), V(0.56f, 0.64f, 0.5f), 200f, 1.4f, 4, 0.025f, Vector3.zero, PartColorRole.Primary),
                    Drape(V(0f, 0.31f, -0.01f), V(0.38f, 0.11f, 0.34f), 230f, 1.3f, 0, 0f, Vector3.zero, PartColorRole.Secondary),
                    SF(PrimitiveType.Sphere, V(0f, 0.2f, TorsoFront + 0.02f), V(0.06f, 0.06f, 0.03f), Gold),
                    SR(PrimitiveType.Cube, V(-0.1f, 0.21f, TorsoFront - 0.03f), V(0.14f, 0.02f, 0.02f), V(0f, 20f, -12f), PartColorRole.Secondary),
                    SR(PrimitiveType.Cube, V(0.1f, 0.21f, TorsoFront - 0.03f),  V(0.14f, 0.02f, 0.02f), V(0f, -20f, 12f), PartColorRole.Secondary),
                },
            },

            // 마법사 로브(로브 형태 — 앞까지 여민다): 발목까지 퍼지는 치마 + 금테 + 목 뒤 두건
            ["outer_wizard"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Body,
                parts = new[]
                {
                    Drape(V(0f, TorsoHem + 0.03f, 0f), V(0.48f, 0.37f, 0.42f), 360f, 1.38f, 6, 0.012f, Vector3.zero, PartColorRole.Primary),
                    Ring(V(0f, TorsoHem + 0.03f - 0.37f, 0f), V(0.66f, 0.3f, 0.58f), 0.035f, Vector3.zero, PartColorRole.Secondary),
                    Drape(V(0f, 0.3f, -0.03f), V(0.42f, 0.2f, 0.4f), 200f, 1.2f, 2, 0.01f, V(-10f, 0f, 0f), PartColorRole.Primary),
                    Ring(V(0f, 0.25f, 0f), V(0.34f, 0.35f, 0.3f), 0.08f, Vector3.zero, PartColorRole.Secondary),
                },
            },

            // 그림자 코트(열린 코트): 정강이까지 내려오는 뒤트임 자락 + 세운 깃
            ["outer_shadow"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Body,
                parts = new[]
                {
                    Drape(V(0f, TorsoHem + 0.03f, 0f), V(0.5f, 0.42f, 0.44f), 290f, 1.18f, 3, 0.01f, Vector3.zero, PartColorRole.Primary),
                    Drape(V(0f, 0.34f, -0.01f), V(0.36f, 0.14f, 0.33f), 240f, 1.18f, 0, 0f, Vector3.zero, PartColorRole.PrimaryDark),
                    Ring(V(0f, TorsoHem + 0.03f - 0.42f, 0f), V(0.59f, 0.25f, 0.52f), 0.02f, Vector3.zero, PartColorRole.Secondary),
                },
            },

            // 연구원 코트(열린 코트): 무릎까지 내려오는 앞트임 자락(주머니·펜은 텍스처)
            ["outer_labcoat"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Body,
                parts = new[]
                {
                    Drape(V(0f, TorsoHem + 0.03f, 0f), V(0.5f, 0.3f, 0.44f), 300f, 1.12f, 2, 0.008f, Vector3.zero, PartColorRole.Primary),
                },
            },

            // 비옷: 목 뒤로 넘긴 두건 + 무릎 위까지 오는 자락
            ["outer_raincoat"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Body,
                parts = new[]
                {
                    SR(PrimitiveType.Sphere, V(0f, 0.28f, -0.15f), V(0.36f, 0.24f, 0.24f), V(-15f, 0f, 0f), PartColorRole.Primary),
                    Drape(V(0f, TorsoHem + 0.03f, 0f), V(0.5f, 0.16f, 0.44f), 300f, 1.12f, 0, 0f, Vector3.zero, PartColorRole.Primary),
                },
            },

            // 바람막이: 목을 감싸 세운 깃(토러스)
            ["outer_windbreaker"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Body,
                parts = new[]
                {
                    Ring(V(0f, 0.25f, 0f), V(0.3f, 0.5f, 0.28f), 0.13f, Vector3.zero, PartColorRole.Primary),
                },
            },

            // 크리스털 자켓: 어깨 위로 솟은 결정(보석)
            ["outer_crystal"] = new OutfitRecipe
            {
                anchor = OutfitAnchor.Body,
                parts = new[]
                {
                    GemPart(V(-0.19f, 0.25f, 0.02f), V(0.06f, 0.13f, 0.06f), V(0f, 20f, 18f),  PartColorRole.Secondary),
                    GemPart(V(-0.23f, 0.22f, -0.05f), V(0.045f, 0.09f, 0.045f), V(10f, 0f, 35f), PartColorRole.Secondary),
                    GemPart(V(0.19f, 0.25f, 0.02f),  V(0.06f, 0.13f, 0.06f), V(0f, -20f, -18f), PartColorRole.Secondary),
                    GemPart(V(0.23f, 0.22f, -0.05f), V(0.045f, 0.09f, 0.045f), V(10f, 0f, -35f), PartColorRole.Secondary),
                },
            },
        };
    }
}
