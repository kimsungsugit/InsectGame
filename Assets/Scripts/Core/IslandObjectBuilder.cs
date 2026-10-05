using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace InsectGame.Core
{
    /// <summary>
    /// 섬 물건의 3D 모양 — <see cref="IslandCatalog"/>의 id마다 모델 하나.
    ///
    /// <b>좌표 규약</b>: root 원점 = 차지 영역의 중심·지면(y = 0), +Z가 정면(회전 0).
    /// 모델은 차지 칸(<c>width × depth</c>, 한 칸 <see cref="GameConstants.Island.CellSize"/>) 안쪽 0.1m 여유 안에 든다 —
    /// 붙여 놓은 두 물건이 서로 파고들지 않게. 나무 수관만 위에서 0.3m까지 넘친다.
    ///
    /// <b>카메라는 (0, 9, −6) 고각이다.</b> 세로로 선 디테일은 거의 안 보이므로 지붕 모양·윗면 색 대비·실루엣으로 읽히게 짓는다
    /// (먹이통과 급수대는 옆모습이 아니라 윗면이 과일 접시냐 물이냐로 갈린다).
    ///
    /// <b>콜라이더가 없다.</b> 조각을 <c>CreatePrimitive</c>로 만들지 않고 내장 메시만 빌려 쓴다 — 콜라이더가 생겼다 지워지는
    /// 왕복이 없고(<c>Destroy</c>는 프레임 끝까지 미뤄져 그 사이 레이캐스트에 걸린다), 섬 하나에 조각이 수천 개까지 간다.
    /// 통행 차단은 호출부(<c>IslandWorldBuilder.BuildPlaced</c>)가 BoxCollider 하나로 따로 단다.
    ///
    /// 머티리얼은 <see cref="IslandMaterialCache"/>로만 받는다(같은 색 = 같은 머티리얼). 그래서 <b>색을 여기 상수로 모아
    /// 물건끼리 나눠 쓴다</b> — 물건마다 살짝 다른 갈색을 쓰면 섬 전체 머티리얼 수가 곧 물건 수가 된다.
    /// </summary>
    public static class IslandObjectBuilder
    {
        // ── 섬 공용 색 ──
        // 값은 감마 공간 반사율이다. 섬 조명(햇빛 1.2 × NdotL 0.79 + 평면 환경광 0.68)에 Standard의 비금속 확산 계수(0.78)를
        // 곱하면 윗면은 여기 적은 값의 약 1.25배로 나온다(RegionPalette의 필드 1.3배와 같은 계산).
        // 0.8을 넘기면 윗면이 하얗게 날아가 형태 음영이 사라진다 — 흰색도 0.79에서 멈춘다.

        internal static readonly Color WoodDark = new Color(0.30f, 0.21f, 0.13f);
        internal static readonly Color Wood = new Color(0.46f, 0.33f, 0.19f);
        internal static readonly Color WoodLight = new Color(0.64f, 0.51f, 0.34f);
        internal static readonly Color Stone = new Color(0.56f, 0.56f, 0.545f);
        internal static readonly Color StoneDark = new Color(0.385f, 0.40f, 0.415f);
        // 잎은 잔디(IslandTerrainBuilder)보다 어두워야 한다 — 같은 밝기면 위에서 본 수관이 잔디에 묻힌다.
        internal static readonly Color Leaf = new Color(0.24f, 0.465f, 0.21f);
        internal static readonly Color LeafDark = new Color(0.16f, 0.35f, 0.175f);
        internal static readonly Color LeafLight = new Color(0.465f, 0.64f, 0.255f);
        internal static readonly Color Red = new Color(0.70f, 0.24f, 0.19f);
        internal static readonly Color Cream = new Color(0.77f, 0.72f, 0.61f);
        internal static readonly Color White = new Color(0.79f, 0.79f, 0.77f);
        internal static readonly Color Yellow = new Color(0.80f, 0.64f, 0.20f);
        internal static readonly Color Pink = new Color(0.785f, 0.53f, 0.61f);
        internal static readonly Color Clay = new Color(0.655f, 0.40f, 0.255f);
        internal static readonly Color Soil = new Color(0.34f, 0.24f, 0.16f);
        internal static readonly Color Teal = new Color(0.225f, 0.48f, 0.53f);
        internal static readonly Color Sand = new Color(0.76f, 0.70f, 0.53f);

        // 반투명 — 알파가 곧 비침 정도다. 불투명 바닥(연못 바닥·분수 수반) 위에 얹어 깊이감을 낸다.
        private static readonly Color WaterTint = new Color(0.335f, 0.61f, 0.785f, 0.78f);
        private static readonly Color GlassTint = new Color(0.68f, 0.78f, 0.80f, 0.32f);

        // 발광 — 한낮 조명에선 "더 밝은 노랑"일 뿐이지만 그늘 면에서도 꺼지지 않아 불빛으로 읽힌다.
        private static readonly Color LampTint = new Color(0.80f, 0.68f, 0.36f);
        private static readonly Color LampEmission = new Color(0.45f, 0.34f, 0.10f);
        private static readonly Color FlameTint = new Color(0.80f, 0.40f, 0.12f);
        private static readonly Color FlameEmission = new Color(0.55f, 0.20f, 0.03f);

        /// <summary>물건 하나의 모델. 반환 root: 원점 = 차지 영역의 중심·지면(y=0), +Z가 정면(회전 0).</summary>
        public static GameObject Build(IslandObjectDef def, Transform parent, IslandMaterialCache materials)
        {
            if (def == null || materials == null) return null;

            GameObject root = new GameObject("IslandObj_" + def.id);
            root.transform.SetParent(parent, false);

            Kit kit = new Kit(root.transform, materials);
            Action<Kit> build = Resolve(def.id);
            if (build != null) build(kit);
            else Fallback(kit, def);
            return root;
        }

        /// <summary>그 id의 전용 모델이 있는가. 폴백 상자는 false.</summary>
        public static bool HasModel(string id)
        {
            return Resolve(id) != null;
        }

        // id → 모델. Build와 HasModel이 이 switch 하나를 읽는다 — 목록을 둘로 두면 "있다고 답했는데 폴백 상자가 뜨는"
        // 어긋남이 테스트(HasModel만 본다)를 통과한 채 생긴다.
        private static Action<Kit> Resolve(string id)
        {
            switch (id)
            {
                case "b_cabin": return Cabin;
                case "b_storage": return Storage;
                case "b_greenhouse": return Greenhouse;
                case "b_windmill": return Windmill;
                case "b_lighthouse": return Lighthouse;

                case "f_bench": return Bench;
                case "f_table": return Table;
                case "f_lantern": return Lantern;
                case "f_fence": return Fence;
                case "f_flowerpot": return Flowerpot;
                case "f_mailbox": return Mailbox;
                case "f_sign": return Sign;
                case "f_hammock": return Hammock;
                case "f_campfire": return Campfire;
                case "f_fountain": return Fountain;

                case "t_sapling": return Sapling;
                case "t_oak": return Oak;
                case "t_blossom": return Blossom;
                case "t_rock": return Rock;
                case "t_boulder": return Boulder;
                case "t_pond": return Pond;
                case "t_flowerbed": return Flowerbed;
                case "t_bush": return Bush;

                case "o_feeder": return Feeder;
                case "o_water": return WaterStand;
                case "o_basket": return Basket;
                case "o_honeypot": return Honeypot;
                case "o_sprinkler": return Sprinkler;

                default: return null;
            }
        }

        // 더 새 버전에서 산 물건이 구버전에 내려와도 섬은 떠야 한다 — 자리만 차지하는 상자로 둔다.
        private static void Fallback(Kit k, IslandObjectDef def)
        {
            float cs = GameConstants.Island.CellSize;
            float w = Mathf.Max(1, def.width) * cs - 0.2f;
            float d = Mathf.Max(1, def.depth) * cs - 0.2f;
            k.Box("Placeholder", V(0f, 0.5f, 0f), V(w, 1f, d), Stone);
        }

        // ── 건물 ──

        // 3×3. 마루가 앞뒤로 뻗은 붉은 박공지붕 + 돌 굴뚝 — 위에서 "집"으로 읽히는 건 지붕 두 면의 명암과 굴뚝이다.
        private static void Cabin(Kit k)
        {
            k.Box("Wall", V(0f, 1.0f, -0.1f), V(3.3f, 2.0f, 3.0f), Wood);
            k.Roof("Roof", V(0f, 1.9f, -0.1f), 4.0f, 1.2f, 3.6f, Red);
            k.Box("Ridge", V(0f, 3.1f, -0.1f), V(0.3f, 0.12f, 3.7f), WoodDark);
            k.Box("Chimney", V(0.95f, 2.75f, -0.9f), V(0.5f, 1.3f, 0.5f), Stone);
            k.Box("ChimneyCap", V(0.95f, 3.44f, -0.9f), V(0.64f, 0.12f, 0.64f), StoneDark);
            k.Box("Door", V(0f, 0.75f, 1.42f), V(0.75f, 1.5f, 0.08f), WoodDark);
            k.Box("WindowFront", V(1.0f, 1.25f, 1.42f), V(0.55f, 0.5f, 0.06f), Yellow);
            // 기본 방향에선 카메라가 뒷벽을 본다 — 뒤에도 창을 내야 통나무 벽 한 장으로 보이지 않는다.
            k.Box("WindowBack", V(-0.7f, 1.25f, -1.62f), V(0.55f, 0.5f, 0.06f), Yellow);
            k.Box("Porch", V(0f, 0.06f, 1.75f), V(1.7f, 0.12f, 0.66f), WoodLight);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -1.65f : 1.65f;
                float z = (i < 2) ? -1.6f : 1.4f;
                k.Cyl("CornerLog", V(x, 1.0f, z), 0.3f, 2.0f, WoodDark);
            }
        }

        // 3×2. 마루가 좌우로 뻗은 청록 지붕 — 오두막과 색·마루 방향이 둘 다 달라 위에서 바로 갈린다. 문 앞 상자·통·자루가 창고임을 말한다.
        private static void Storage(Kit k)
        {
            k.Box("Wall", V(0f, 0.9f, -0.15f), V(3.9f, 1.8f, 2.0f), WoodLight);
            k.Roof("Roof", V(0f, 1.7f, -0.15f), 2.5f, 1.0f, 4.2f, Teal, 90f);
            k.Box("Ridge", V(0f, 2.7f, -0.15f), V(4.3f, 0.12f, 0.28f), WoodDark);
            k.Box("Door", V(0f, 0.7f, 0.87f), V(1.5f, 1.4f, 0.08f), WoodDark);
            NoShadow(k.Box("BraceA", V(0f, 0.7f, 0.92f), V(1.7f, 0.08f, 0.03f), Cream, V(0f, 0f, 43f)));
            NoShadow(k.Box("BraceB", V(0f, 0.7f, 0.92f), V(1.7f, 0.08f, 0.03f), Cream, V(0f, 0f, -43f)));
            k.Box("Crate", V(1.35f, 0.3f, 1.08f), V(0.55f, 0.6f, 0.48f), Wood, V(0f, 12f, 0f));
            k.Box("CrateTop", V(1.3f, 0.76f, 1.05f), V(0.36f, 0.32f, 0.34f), WoodLight, V(0f, -10f, 0f));
            k.Cyl("Barrel", V(-1.45f, 0.35f, 1.08f), 0.56f, 0.7f, Wood);
            NoShadow(k.Cyl("BarrelGrain", V(-1.45f, 0.706f, 1.08f), 0.44f, 0.012f, Yellow));
            k.Ball("Sack", V(-0.85f, 0.2f, 1.12f), V(0.5f, 0.4f, 0.42f), Cream);
        }

        // 3×3. 유리 지붕 너머로 흙 두둑과 풀이 비친다 — 흰 뼈대(마루·처마·박공 보)가 위에서 "유리 집"의 윤곽선이 된다.
        private static void Greenhouse(Kit k)
        {
            Material glass = k.GlassMat();
            k.Box("Base", V(0f, 0.2f, 0f), V(4.0f, 0.4f, 4.0f), White);
            k.Box("BedL", V(-0.95f, 0.44f, 0f), V(1.3f, 0.1f, 3.2f), Soil);
            k.Box("BedR", V(0.95f, 0.44f, 0f), V(1.3f, 0.1f, 3.2f), Soil);
            k.Ball("PlantA", V(-0.95f, 0.75f, -0.9f), V(0.9f, 0.7f, 0.9f), Leaf);
            k.Ball("PlantB", V(-0.95f, 0.7f, 0.6f), V(0.8f, 0.6f, 0.9f), LeafLight);
            k.Ball("PlantC", V(0.95f, 0.75f, 0.9f), V(0.9f, 0.7f, 0.9f), Leaf);
            k.Ball("PlantD", V(0.95f, 0.68f, -0.6f), V(0.7f, 0.55f, 0.8f), LeafLight);
            // 유리는 그림자를 끈다 — Standard Fade의 그림자는 디더 점묘라 바닥에 모래알 같은 얼룩이 진다.
            NoShadow(k.Box("GlassWall", V(0f, 1.1f, 0f), V(3.8f, 1.4f, 3.8f), glass));
            NoShadow(k.Roof("GlassRoof", V(0f, 1.8f, 0f), 4.0f, 1.0f, 4.0f, glass));
            k.Box("FrameRidge", V(0f, 2.8f, 0f), V(0.12f, 0.12f, 4.1f), White);
            k.Box("FrameEaveL", V(-1.94f, 1.8f, 0f), V(0.12f, 0.12f, 4.0f), White);
            k.Box("FrameEaveR", V(1.94f, 1.8f, 0f), V(0.12f, 0.12f, 4.0f), White);
            k.Box("FrameFront", V(0f, 1.8f, 1.94f), V(4.0f, 0.12f, 0.12f), White);
            k.Box("FrameBack", V(0f, 1.8f, -1.94f), V(4.0f, 0.12f, 0.12f), White);
        }

        // 3×3. 둥근 탑 + 붉은 원뿔 지붕 + 정면의 X자 날개. 날개는 세로 면이지만 고각 카메라에서도 높이의 절반쯤은 보인다.
        private static void Windmill(Kit k)
        {
            k.Cyl("Base", V(0f, 0.25f, -0.3f), 2.6f, 0.5f, Stone);
            k.Cyl("Tower", V(0f, 1.9f, -0.3f), 2.0f, 2.8f, Cream);
            k.Cyl("Gallery", V(0f, 1.7f, -0.3f), 2.5f, 0.12f, WoodLight);
            k.Cone("Cap", V(0f, 3.3f, -0.3f), 2.4f, 1.2f, Red);
            k.Cyl("Hub", V(0f, 2.75f, 0.9f), 0.3f, 0.6f, WoodDark, V(90f, 0f, 0f));
            k.Box("SailA", V(0f, 2.75f, 1.08f), V(3.8f, 0.5f, 0.05f), Cream, V(0f, 0f, 45f));
            k.Box("SailB", V(0f, 2.75f, 1.08f), V(3.8f, 0.5f, 0.05f), Cream, V(0f, 0f, -45f));
            NoShadow(k.Box("SparA", V(0f, 2.75f, 1.13f), V(3.9f, 0.1f, 0.06f), WoodDark, V(0f, 0f, 45f)));
            NoShadow(k.Box("SparB", V(0f, 2.75f, 1.13f), V(3.9f, 0.1f, 0.06f), WoodDark, V(0f, 0f, -45f)));
            k.Box("Door", V(0f, 0.95f, 0.69f), V(0.6f, 1.0f, 0.1f), WoodDark);
            k.Box("Window", V(0f, 2.3f, -1.29f), V(0.4f, 0.45f, 0.08f), Yellow);
        }

        // 2×2. 흰·빨강 띠를 두른 탑이 위로 갈수록 가늘어진다 — 위에서는 동심원(돌 기단 > 흰 탑 > 난간 > 붉은 지붕)으로 읽힌다.
        private static void Lighthouse(Kit k)
        {
            k.Cyl("Base", V(0f, 0.18f, 0f), 2.5f, 0.36f, Stone);
            k.Cyl("TowerLow", V(0f, 1.0f, 0f), 1.9f, 1.3f, White);
            k.Cyl("TowerMid", V(0f, 2.25f, 0f), 1.7f, 1.2f, Red);
            k.Cyl("TowerTop", V(0f, 3.45f, 0f), 1.5f, 1.2f, White);
            k.Cyl("Gallery", V(0f, 4.12f, 0f), 2.0f, 0.14f, StoneDark);
            k.Cyl("Lamp", V(0f, 4.54f, 0f), 1.0f, 0.7f, k.LampMat());
            k.Cone("Roof", V(0f, 4.89f, 0f), 1.5f, 0.75f, Red);
            k.Ball("Finial", V(0f, 5.68f, 0f), V(0.18f, 0.18f, 0.18f), Yellow);
            k.Box("Door", V(0f, 0.85f, 0.93f), V(0.5f, 1.0f, 0.1f), WoodDark);
        }

        // ── 가구 ──

        // 2×1. 밝은 좌판 두 장 + 뒤쪽의 짙은 등받이 — 위에서 긴 직사각형에 한쪽 띠가 진 모양.
        private static void Bench(Kit k)
        {
            k.Box("SeatFront", V(0f, 0.45f, 0.2f), V(2.3f, 0.08f, 0.26f), WoodLight);
            k.Box("SeatBack", V(0f, 0.45f, -0.08f), V(2.3f, 0.08f, 0.26f), WoodLight);
            k.Box("Backrest", V(0f, 0.85f, -0.3f), V(2.3f, 0.4f, 0.1f), Wood);
            k.Box("LegL", V(-1.0f, 0.22f, 0.02f), V(0.12f, 0.44f, 0.62f), WoodDark);
            k.Box("LegR", V(1.0f, 0.22f, 0.02f), V(0.12f, 0.44f, 0.62f), WoodDark);
            k.Box("ArmL", V(-1.0f, 0.66f, 0.02f), V(0.14f, 0.08f, 0.7f), WoodDark);
            k.Box("ArmR", V(1.0f, 0.66f, 0.02f), V(0.14f, 0.08f, 0.7f), WoodDark);
        }

        // 2×2. 나이테가 보이는 둥근 상판 + 대각선 네 귀의 그루터기 의자.
        private static void Table(Kit k)
        {
            k.Cyl("Trunk", V(0f, 0.33f, 0f), 0.6f, 0.66f, Wood);
            k.Cyl("Top", V(0f, 0.73f, 0f), 1.5f, 0.14f, WoodLight);
            NoShadow(k.Cyl("RingOuter", V(0f, 0.805f, 0f), 1.05f, 0.012f, Wood));
            NoShadow(k.Cyl("RingInner", V(0f, 0.812f, 0f), 0.5f, 0.012f, WoodLight));
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -0.82f : 0.82f;
                float z = (i < 2) ? -0.82f : 0.82f;
                k.Cyl("Stool", V(x, 0.2f, z), 0.5f, 0.4f, Wood);
                NoShadow(k.Cyl("StoolTop", V(x, 0.405f, z), 0.38f, 0.012f, WoodLight));
            }
        }

        // 1×1. 갓이 등보다 작다 — 위에서 봐도 갓 둘레로 불빛이 고리처럼 남는다.
        private static void Lantern(Kit k)
        {
            k.Cyl("Foot", V(0f, 0.06f, 0f), 0.4f, 0.12f, Stone);
            k.Cyl("Post", V(0f, 0.75f, 0f), 0.1f, 1.3f, WoodDark);
            k.Cyl("Plate", V(0f, 1.42f, 0f), 0.34f, 0.05f, WoodDark);
            k.Ball("Light", V(0f, 1.66f, 0f), V(0.46f, 0.46f, 0.46f), k.LampMat());
            k.Cone("Cap", V(0f, 1.84f, 0f), 0.34f, 0.2f, WoodDark);
        }

        // 2×1. 기둥 셋 + 가로대 둘. 양 끝 기둥이 칸 가장자리 바로 안쪽이라 이어 놓으면 한 줄로 이어진다.
        private static void Fence(Kit k)
        {
            for (int i = -1; i <= 1; i++)
                k.Box("Post", V(i * 1.3f, 0.45f, 0f), V(0.16f, 0.9f, 0.16f), Wood);
            k.Box("RailHigh", V(0f, 0.68f, 0f), V(2.76f, 0.1f, 0.08f), WoodLight);
            k.Box("RailLow", V(0f, 0.36f, 0f), V(2.76f, 0.1f, 0.08f), WoodLight);
        }

        // 1×1. 토분 + 잎 뭉치 + 꽃 세 송이.
        private static void Flowerpot(Kit k)
        {
            k.Cyl("Pot", V(0f, 0.17f, 0f), 0.46f, 0.34f, Clay);
            k.Cyl("Rim", V(0f, 0.4f, 0f), 0.6f, 0.12f, Clay);
            NoShadow(k.Cyl("Soil", V(0f, 0.465f, 0f), 0.5f, 0.012f, Soil));
            k.Ball("Leaves", V(0f, 0.62f, 0f), V(0.62f, 0.4f, 0.62f), Leaf);
            NoShadow(k.Ball("FlowerA", V(0.12f, 0.84f, 0.08f), V(0.22f, 0.2f, 0.22f), Pink));
            NoShadow(k.Ball("FlowerB", V(-0.16f, 0.8f, -0.04f), V(0.2f, 0.18f, 0.2f), Yellow));
            NoShadow(k.Ball("FlowerC", V(0.02f, 0.8f, -0.18f), V(0.18f, 0.16f, 0.18f), Pink));
        }

        // 1×1. 둥근 지붕의 빨간 통 — 섬에서 빨강이 이만큼 뭉친 작은 물건은 이것뿐이라 색으로 읽힌다.
        private static void Mailbox(Kit k)
        {
            k.Box("Post", V(0f, 0.5f, 0f), V(0.12f, 1.0f, 0.12f), Wood);
            k.Box("Body", V(0f, 1.1f, 0f), V(0.44f, 0.24f, 0.62f), Red);
            k.Cyl("Top", V(0f, 1.22f, 0f), 0.44f, 0.62f, Red, V(90f, 0f, 0f));
            NoShadow(k.Box("Lid", V(0f, 1.16f, 0.32f), V(0.34f, 0.3f, 0.03f), White));
            NoShadow(k.Box("Flag", V(0.25f, 1.32f, 0.05f), V(0.04f, 0.26f, 0.14f), Yellow));
        }

        // 1×1. 판자 위에 짙은 갓을 얹었다 — 위에서는 갓이 가로 막대 하나로 보인다.
        private static void Sign(Kit k)
        {
            k.Box("Post", V(0f, 0.45f, 0f), V(0.1f, 0.9f, 0.1f), Wood);
            k.Box("Board", V(0f, 1.0f, 0.04f), V(1.0f, 0.56f, 0.08f), WoodLight);
            k.Box("Cap", V(0f, 1.31f, 0.04f), V(1.12f, 0.07f, 0.22f), WoodDark);
            NoShadow(k.Box("LineA", V(0f, 1.08f, 0.09f), V(0.66f, 0.06f, 0.02f), WoodDark));
            NoShadow(k.Box("LineB", V(-0.1f, 0.92f, 0.09f), V(0.46f, 0.06f, 0.02f), WoodDark));
        }

        // 3×1. 두 기둥 사이에 처진 천 세 토막(가운데 평평, 양 끝 오르막) + 빨간 줄무늬 + 베개.
        private static void Hammock(Kit k)
        {
            k.Cyl("PostL", V(-1.85f, 0.65f, 0f), 0.16f, 1.3f, Wood);
            k.Cyl("PostR", V(1.85f, 0.65f, 0f), 0.16f, 1.3f, Wood);
            k.Box("Cloth", V(0f, 0.55f, 0f), V(1.84f, 0.06f, 0.8f), Cream);
            k.Box("ClothL", V(-1.3f, 0.775f, 0f), V(0.93f, 0.05f, 0.72f), Cream, V(0f, 0f, -29.4f));
            k.Box("ClothR", V(1.3f, 0.775f, 0f), V(0.93f, 0.05f, 0.72f), Cream, V(0f, 0f, 29.4f));
            NoShadow(k.Box("StripeA", V(-0.4f, 0.585f, 0f), V(0.18f, 0.02f, 0.8f), Red));
            NoShadow(k.Box("StripeB", V(0.4f, 0.585f, 0f), V(0.18f, 0.02f, 0.8f), Red));
            k.Ball("Pillow", V(0.68f, 0.64f, 0f), V(0.36f, 0.14f, 0.52f), White);
        }

        // 2×2. 돌 고리 안의 불꽃 + 양옆 통나무 의자.
        private static void Campfire(Kit k)
        {
            NoShadow(k.Cyl("Ash", V(0f, 0.02f, 0f), 1.5f, 0.04f, Soil));
            for (int i = 0; i < 6; i++)
            {
                float a = (60f * i + 30f) * Mathf.Deg2Rad;
                k.Ball("RingStone", V(Mathf.Cos(a) * 0.72f, 0.13f, Mathf.Sin(a) * 0.72f), V(0.44f, 0.3f, 0.4f),
                    i % 2 == 0 ? Stone : StoneDark);
            }
            k.Cyl("LogA", V(0f, 0.14f, 0f), 0.16f, 0.95f, WoodDark, V(90f, 45f, 0f));
            k.Cyl("LogB", V(0f, 0.2f, 0f), 0.16f, 0.95f, WoodDark, V(90f, -45f, 0f));
            NoShadow(k.Cone("Flame", V(0f, 0.18f, 0f), 0.56f, 0.85f, k.FlameMat()));
            // 속불은 겉불 안에 넣으면 가려진다 — 옆으로 빼서 두 갈래 불꽃으로 둔다.
            NoShadow(k.Cone("FlameSmall", V(0.17f, 0.18f, 0.1f), 0.3f, 0.55f, k.LampMat()));
            k.Cyl("SeatL", V(-1.15f, 0.17f, 0f), 0.34f, 1.2f, Wood, V(90f, 0f, 0f));
            k.Cyl("SeatR", V(1.15f, 0.17f, 0f), 0.34f, 1.2f, Wood, V(90f, 0f, 0f));
        }

        // 3×3. 돌 테 안에 물이 찬 큰 원 + 가운데 2단 수반. 물은 짙은 수반 바닥 위에 얹은 반투명 원판이다.
        private static void Fountain(Kit k)
        {
            Material water = k.WaterMat();
            k.Cyl("Basin", V(0f, 0.2f, 0f), 4.0f, 0.4f, StoneDark);
            k.Ring("Rim", V(0f, 0.42f, 0f), 3.8f, 0.3f, Stone);
            k.Disc("Water", V(0f, 0.47f, 0f), 3.5f, 3.5f, water);
            k.Cyl("Pedestal", V(0f, 0.75f, 0f), 0.6f, 1.1f, Stone);
            k.Cyl("Bowl", V(0f, 1.3f, 0f), 1.5f, 0.16f, Stone);
            k.Disc("BowlWater", V(0f, 1.39f, 0f), 1.25f, 1.25f, water);
            k.Cyl("Spout", V(0f, 1.6f, 0f), 0.26f, 0.5f, StoneDark);
            NoShadow(k.Ball("Jet", V(0f, 1.98f, 0f), V(0.5f, 0.55f, 0.5f), water));
        }

        // ── 지형지물 ──

        // 1×1. 연두 수관 + 지지대 — 참나무와는 크기와 잎색(연두)으로 갈린다.
        private static void Sapling(Kit k)
        {
            NoShadow(k.Cyl("Mound", V(0f, 0.03f, 0f), 0.62f, 0.06f, Soil));
            k.Cyl("Trunk", V(0f, 0.5f, 0f), 0.1f, 1.0f, Wood);
            k.Ball("Crown", V(0f, 1.12f, 0f), V(0.74f, 0.66f, 0.74f), LeafLight);
            k.Ball("CrownTop", V(0.1f, 1.45f, 0.04f), V(0.44f, 0.42f, 0.44f), Leaf);
            k.Box("Stake", V(0.2f, 0.42f, 0.08f), V(0.05f, 0.84f, 0.05f), WoodLight);
        }

        // 2×2. 세 톤의 잎 뭉치 넷 — 수관만 칸 밖으로 0.2m까지 넘친다(그늘이 넓다는 설명대로).
        private static void Oak(Kit k)
        {
            k.Cyl("Roots", V(0f, 0.1f, 0f), 0.95f, 0.2f, WoodDark);
            k.Cyl("Trunk", V(0f, 0.9f, 0f), 0.6f, 1.8f, Wood);
            k.Ball("Crown", V(0f, 2.6f, 0f), V(2.7f, 2.0f, 2.7f), Leaf);
            k.Ball("CrownSideA", V(-0.75f, 2.15f, 0.55f), V(1.6f, 1.3f, 1.6f), LeafDark);
            k.Ball("CrownSideB", V(0.85f, 2.3f, -0.5f), V(1.5f, 1.2f, 1.5f), LeafDark);
            k.Ball("CrownTop", V(-0.3f, 3.35f, -0.3f), V(1.5f, 1.0f, 1.5f), LeafLight);
        }

        // 2×2. 분홍 수관에 흰 꽃 뭉치, 발치에 떨어진 꽃잎 — 섬에서 분홍 덩어리는 이 나무뿐이다.
        private static void Blossom(Kit k)
        {
            Material petal = k.Solid(Pink);
            k.Cyl("Trunk", V(0f, 0.8f, 0f), 0.42f, 1.6f, WoodDark);
            k.Cyl("Branch", V(0.45f, 1.75f, 0.1f), 0.2f, 1.0f, WoodDark, V(0f, 0f, -40f));
            k.Ball("Crown", V(0f, 2.45f, 0f), V(2.5f, 1.7f, 2.5f), Pink);
            k.Ball("CrownSide", V(0.85f, 2.1f, 0.45f), V(1.5f, 1.15f, 1.5f), Pink);
            k.Ball("CrownLight", V(-0.55f, 3.0f, -0.35f), V(1.4f, 0.95f, 1.4f), White);
            k.Ball("CrownLow", V(-0.8f, 2.0f, 0.5f), V(1.3f, 1.0f, 1.3f), White);
            k.Disc("PetalA", V(1.0f, 0.012f, 0.9f), 0.24f, 0.2f, petal);
            k.Disc("PetalB", V(-1.1f, 0.012f, 0.3f), 0.2f, 0.24f, petal);
            k.Disc("PetalC", V(0.5f, 0.012f, -1.15f), 0.22f, 0.22f, petal);
            k.Disc("PetalD", V(-0.6f, 0.012f, 1.1f), 0.24f, 0.18f, petal);
        }

        // 1×1. 납작한 돌 둘 + 윗면의 이끼.
        private static void Rock(Kit k)
        {
            k.Ball("Rock", V(0f, 0.24f, 0f), V(0.95f, 0.6f, 0.8f), Stone, V(0f, 20f, 0f));
            k.Ball("RockSmall", V(0.32f, 0.13f, 0.26f), V(0.46f, 0.34f, 0.42f), StoneDark);
            NoShadow(k.Ball("Moss", V(-0.08f, 0.5f, -0.02f), V(0.56f, 0.14f, 0.46f), Leaf));
        }

        // 2×2. 큰 덩어리 하나에 기대 선 작은 돌들.
        private static void Boulder(Kit k)
        {
            k.Ball("Rock", V(-0.1f, 0.62f, -0.1f), V(2.2f, 1.5f, 1.85f), Stone, V(0f, 25f, 0f));
            k.Ball("RockSide", V(0.72f, 0.4f, 0.6f), V(1.25f, 0.95f, 1.15f), StoneDark);
            k.Ball("RockLow", V(-0.85f, 0.28f, 0.7f), V(0.9f, 0.66f, 0.85f), Stone);
            NoShadow(k.Ball("Moss", V(-0.2f, 1.3f, -0.15f), V(1.2f, 0.22f, 0.95f), Leaf));
            k.Ball("Pebble", V(0.35f, 0.12f, 1.2f), V(0.34f, 0.24f, 0.3f), StoneDark);
        }

        // 3×3. 땅을 팔 수 없으니(잔디가 y=0의 한 장이다) 납작한 원판 세 겹으로 깊이를 낸다: 모래 물가 > 짙은 바닥 > 반투명 수면.
        private static void Pond(Kit k)
        {
            k.Disc("Shore", V(0f, 0.015f, 0f), 4.2f, 4.0f, k.Solid(Sand));
            k.Disc("Bed", V(0f, 0.03f, 0f), 3.7f, 3.5f, k.Solid(Teal));
            k.Disc("WaterSurface", V(0f, 0.05f, 0f), 3.7f, 3.5f, k.WaterMat());
            float[] stoneAngles = { 20f, 110f, 200f, 290f };
            for (int i = 0; i < stoneAngles.Length; i++)
            {
                float a = stoneAngles[i] * Mathf.Deg2Rad;
                k.Ball("ShoreStone", V(Mathf.Cos(a) * 1.85f, 0.12f, Mathf.Sin(a) * 1.75f), V(0.5f, 0.3f, 0.44f),
                    i % 2 == 0 ? Stone : StoneDark);
            }
            k.Disc("LilyPadA", V(0.5f, 0.065f, 0.4f), 0.5f, 0.5f, k.Solid(LeafLight));
            k.Disc("LilyPadB", V(-0.7f, 0.065f, -0.3f), 0.42f, 0.42f, k.Solid(LeafLight));
            NoShadow(k.Ball("Lotus", V(0.5f, 0.12f, 0.4f), V(0.2f, 0.14f, 0.2f), Pink));
            NoShadow(k.Cyl("ReedA", V(-1.45f, 0.45f, 0.95f), 0.06f, 0.9f, LeafDark));
            NoShadow(k.Cyl("ReedB", V(-1.3f, 0.38f, 1.1f), 0.06f, 0.76f, LeafDark));
            NoShadow(k.Cyl("ReedHeadA", V(-1.45f, 0.95f, 0.95f), 0.12f, 0.24f, WoodDark));
            NoShadow(k.Cyl("ReedHeadB", V(-1.3f, 0.8f, 1.1f), 0.12f, 0.22f, WoodDark));
        }

        // 2×2, 지나다닐 수 있다 — 캐릭터 무릎 아래(0.45m)로 낮게. 흙 판 위 잎 세 이랑 + 네 색 꽃.
        private static void Flowerbed(Kit k)
        {
            Color[] blooms = { Pink, Yellow, White, Red };
            NoShadow(k.Box("Soil", V(0f, 0.035f, 0f), V(2.7f, 0.07f, 2.7f), Soil));
            for (int row = -1; row <= 1; row++)
            {
                float z = row * 0.85f;
                NoShadow(k.Ball("Leaves", V(0f, 0.16f, z), V(2.4f, 0.24f, 0.5f), Leaf));
                for (int col = -1; col <= 1; col++)
                {
                    int index = (row + 1) * 3 + (col + 1);
                    // 이랑마다 반 칸씩 어긋나게 — 격자로 맞추면 꽃밭이 아니라 단추 판으로 보인다.
                    float x = col * 0.8f + (row == 0 ? 0.2f : -0.1f);
                    NoShadow(k.Ball("Flower", V(x, 0.32f, z), V(0.3f, 0.24f, 0.3f), blooms[index % blooms.Length]));
                }
            }
        }

        // 1×1, 지나다닐 수 있다 — 허리 아래로 낮게. 세 톤 잎 뭉치 + 빨간 열매.
        private static void Bush(Kit k)
        {
            k.Ball("Bush", V(0f, 0.27f, 0f), V(1.1f, 0.62f, 1.0f), Leaf);
            k.Ball("BushSide", V(0.28f, 0.22f, 0.24f), V(0.62f, 0.46f, 0.6f), LeafDark);
            k.Ball("BushTop", V(-0.2f, 0.46f, -0.12f), V(0.52f, 0.34f, 0.5f), LeafLight);
            NoShadow(k.Ball("BerryA", V(0.15f, 0.56f, 0.2f), V(0.1f, 0.1f, 0.1f), Red));
            NoShadow(k.Ball("BerryB", V(-0.3f, 0.4f, 0.36f), V(0.1f, 0.1f, 0.1f), Red));
            NoShadow(k.Ball("BerryC", V(0.38f, 0.42f, -0.12f), V(0.1f, 0.1f, 0.1f), Red));
        }

        // ── 도구(설비) ──

        // 1×1. 기둥 위 나무 접시에 과일 조각 — 지붕을 씌우지 않는다(위에서 과일이 안 보이면 급수대·바구니와 구별이 안 된다).
        private static void Feeder(Kit k)
        {
            k.Cyl("Foot", V(0f, 0.03f, 0f), 0.5f, 0.06f, WoodDark);
            k.Cyl("Post", V(0f, 0.25f, 0f), 0.16f, 0.5f, Wood);
            k.Cyl("Tray", V(0f, 0.55f, 0f), 1.0f, 0.1f, WoodLight);
            k.Ring("Rim", V(0f, 0.62f, 0f), 1.0f, 0.1f, Wood);
            NoShadow(k.Ball("FruitA", V(0.14f, 0.69f, 0.1f), V(0.3f, 0.2f, 0.3f), Red));
            NoShadow(k.Ball("FruitB", V(-0.2f, 0.68f, -0.02f), V(0.28f, 0.18f, 0.26f), Yellow));
            NoShadow(k.Ball("FruitC", V(0.02f, 0.67f, -0.24f), V(0.22f, 0.16f, 0.22f), Pink));
        }

        // 1×1. 돌 수반에 고인 물 + 뒤에서 내려오는 대나무 물길.
        private static void WaterStand(Kit k)
        {
            k.Cyl("Foot", V(0f, 0.06f, 0f), 0.8f, 0.12f, StoneDark);
            k.Cyl("Basin", V(0f, 0.32f, 0f), 1.0f, 0.4f, Stone);
            k.Ring("Rim", V(0f, 0.54f, 0f), 0.96f, 0.12f, StoneDark);
            k.Disc("Water", V(0f, 0.56f, 0f), 0.88f, 0.88f, k.WaterMat());
            k.Box("SpoutPost", V(0f, 0.5f, -0.56f), V(0.1f, 1.0f, 0.1f), Wood);
            NoShadow(k.Cyl("Spout", V(0f, 0.92f, -0.3f), 0.09f, 0.56f, LeafLight, V(107f, 0f, 0f)));
        }

        // 1×1. 손잡이가 위로 걸친 바구니에 수확물(노랑·빨강·초록).
        private static void Basket(Kit k)
        {
            k.Cyl("Body", V(0f, 0.25f, 0f), 0.9f, 0.5f, WoodLight);
            k.Cyl("Band", V(0f, 0.25f, 0f), 0.94f, 0.1f, Wood);
            k.Ring("Rim", V(0f, 0.5f, 0f), 0.92f, 0.1f, Wood);
            // 세운 고리 — 아래 절반은 몸통에 묻히고 위 절반이 손잡이가 된다.
            k.Ring("Handle", V(0f, 0.5f, 0f), 0.9f, 0.09f, Wood, V(0f, 0f, 90f));
            NoShadow(k.Ball("CropA", V(0.12f, 0.56f, 0.1f), V(0.34f, 0.26f, 0.34f), Yellow));
            NoShadow(k.Ball("CropB", V(-0.16f, 0.55f, -0.1f), V(0.3f, 0.24f, 0.3f), Yellow));
            NoShadow(k.Ball("CropC", V(0.02f, 0.57f, -0.22f), V(0.28f, 0.24f, 0.28f), Red));
            NoShadow(k.Ball("CropD", V(-0.18f, 0.56f, 0.14f), V(0.3f, 0.2f, 0.3f), Leaf));
        }

        // 1×1. 둥근 토기 단지 주둥이에 찬 금빛 꿀 + 옆에 내려놓은 뚜껑.
        private static void Honeypot(Kit k)
        {
            k.Ball("Pot", V(0f, 0.38f, 0f), V(0.84f, 0.76f, 0.84f), Clay);
            k.Cyl("Neck", V(0f, 0.76f, 0f), 0.52f, 0.14f, Clay);
            NoShadow(k.Cyl("Honey", V(0f, 0.835f, 0f), 0.42f, 0.012f, Yellow));
            NoShadow(k.Ball("Drip", V(0.2f, 0.66f, 0.2f), V(0.18f, 0.3f, 0.18f), Yellow));
            NoShadow(k.Cyl("Dipper", V(0.1f, 0.98f, -0.04f), 0.05f, 0.5f, Wood, V(0f, 0f, -25f)));
            k.Cyl("Lid", V(-0.36f, 0.04f, 0.32f), 0.44f, 0.08f, WoodLight);
        }

        // 2×1. 왼쪽 물통 → 관 → 오른쪽 살수기와 물 우산. "물을 뿌리는 설비"라 물뿌리개(손 도구)가 아니라 고정 설비로 지었다.
        private static void Sprinkler(Kit k)
        {
            Material water = k.WaterMat();
            k.Cyl("Tank", V(-0.8f, 0.42f, 0f), 0.95f, 0.84f, Wood);
            k.Cyl("BandLow", V(-0.8f, 0.2f, 0f), 0.99f, 0.07f, Teal);
            k.Cyl("BandHigh", V(-0.8f, 0.64f, 0f), 0.99f, 0.07f, Teal);
            k.Ring("TankRim", V(-0.8f, 0.84f, 0f), 0.9f, 0.09f, WoodDark);
            k.Disc("TankWater", V(-0.8f, 0.86f, 0f), 0.84f, 0.84f, water);
            k.Cyl("Pipe", V(0.02f, 0.1f, 0f), 0.09f, 1.3f, Teal, V(0f, 0f, 90f));
            k.Cyl("Stand", V(0.72f, 0.3f, 0f), 0.1f, 0.6f, Teal);
            k.Ball("Head", V(0.72f, 0.64f, 0f), V(0.22f, 0.22f, 0.22f), Teal);
            NoShadow(k.Cone("Spray", V(0.72f, 0.3f, 0f), 1.2f, 0.4f, water));
        }

        // ── 조립 도구 ──

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        /// <summary>
        /// 물건 하나를 짓는 동안의 문맥(root + 머티리얼). 치수는 <b>실제 크기(m)</b>로 받는다 —
        /// 내장 원기둥은 높이가 2라 스케일 y가 반높이인데, 그 환산을 호출부마다 하면 반드시 한 곳은 틀린다.
        /// </summary>
        private readonly struct Kit
        {
            private readonly Transform root;
            private readonly IslandMaterialCache mats;

            public Kit(Transform root, IslandMaterialCache mats)
            {
                this.root = root;
                this.mats = mats;
            }

            public Material Solid(Color color) => mats.Get(color);
            public Material WaterMat() => mats.GetFade(WaterTint);
            public Material GlassMat() => mats.GetFade(GlassTint);
            public Material LampMat() => mats.GetGlow(LampTint, LampEmission);
            public Material FlameMat() => mats.GetGlow(FlameTint, FlameEmission);

            public MeshRenderer Box(string name, Vector3 center, Vector3 size, Color color, Vector3 euler = default)
                => Box(name, center, size, mats.Get(color), euler);

            public MeshRenderer Box(string name, Vector3 center, Vector3 size, Material material, Vector3 euler = default)
                => AddPiece(root, name, CubeMesh, material, center, euler, size);

            /// <summary>세운 원기둥(축 = Y). 눕히려면 euler (0,0,90) = X축, (90,0,0) = Z축.</summary>
            public MeshRenderer Cyl(string name, Vector3 center, float diameter, float height, Color color,
                Vector3 euler = default)
                => Cyl(name, center, diameter, height, mats.Get(color), euler);

            public MeshRenderer Cyl(string name, Vector3 center, float diameter, float height, Material material,
                Vector3 euler = default)
                => AddPiece(root, name, CylinderMesh, material, center, euler,
                    new Vector3(diameter, height * 0.5f, diameter));

            /// <summary>타원체. <paramref name="size"/>는 축별 지름.</summary>
            public MeshRenderer Ball(string name, Vector3 center, Vector3 size, Color color, Vector3 euler = default)
                => Ball(name, center, size, mats.Get(color), euler);

            public MeshRenderer Ball(string name, Vector3 center, Vector3 size, Material material, Vector3 euler = default)
                => AddPiece(root, name, SphereMesh, material, center, euler, size);

            /// <summary>
            /// 박공지붕(속이 찬 삼각기둥). <paramref name="basePos"/>는 밑면 중심, 마루는 Z로 뻗는다.
            /// <paramref name="yaw"/> 90이면 마루가 X로 눕고 그때 <paramref name="span"/>이 Z 폭, <paramref name="length"/>가 X 길이다.
            /// </summary>
            public MeshRenderer Roof(string name, Vector3 basePos, float span, float height, float length, Color color,
                float yaw = 0f)
                => Roof(name, basePos, span, height, length, mats.Get(color), yaw);

            public MeshRenderer Roof(string name, Vector3 basePos, float span, float height, float length,
                Material material, float yaw = 0f)
                => AddPiece(root, name, PrismMesh, material, basePos, new Vector3(0f, yaw, 0f),
                    new Vector3(span, height, length));

            /// <summary>원뿔. <paramref name="basePos"/>는 밑면 중심.</summary>
            public MeshRenderer Cone(string name, Vector3 basePos, float diameter, float height, Color color)
                => Cone(name, basePos, diameter, height, mats.Get(color));

            public MeshRenderer Cone(string name, Vector3 basePos, float diameter, float height, Material material)
                => AddPiece(root, name, ConeMesh, material, basePos, Vector3.zero,
                    new Vector3(diameter, height, diameter));

            /// <summary>
            /// 고리(누운 토러스). <paramref name="diameter"/>는 관 중심선의 지름이라 바깥지름은 그 1.1배다.
            /// <paramref name="thickness"/>는 관의 세로 두께.
            /// </summary>
            public MeshRenderer Ring(string name, Vector3 center, float diameter, float thickness, Color color,
                Vector3 euler = default)
                => AddPiece(root, name, TorusMesh, mats.Get(color), center, euler,
                    new Vector3(diameter, thickness / (TorusTube * 2f), diameter));

            /// <summary>위를 보는 납작한 원판(두께 0). center.y가 곧 윗면이다. 그림자는 드리우지 않는다.</summary>
            public MeshRenderer Disc(string name, Vector3 center, float sizeX, float sizeZ, Material material)
                => AddFlatDisc(root, name, material, center, sizeX, sizeZ);
        }

        // ── 메시·조각(IslandTerrainBuilder와 공유) ──

        private const float TorusTube = 0.05f;
        private const int ConeSides = 12;

        // 내장 메시와 ProcMeshLibrary 캐시는 프로세스 수명이라 정적으로 쥐어도 된다. 그래도 Unity의 == null(파괴 검사)로
        // 다시 찾게 해 둔다(SceneryMaterials.LitShader와 같은 방어). ProcMeshLibrary를 조각마다 부르지 않는 건
        // 캐시 적중에도 람다 클로저가 하나씩 할당되기 때문이다.
        private static Mesh cubeMesh;
        private static Mesh cylinderMesh;
        private static Mesh sphereMesh;
        private static Mesh discMesh;
        private static Mesh torusMesh;
        private static Mesh prismMesh;
        private static Mesh coneMesh;

        internal static Mesh CubeMesh
        {
            get
            {
                if (cubeMesh == null) cubeMesh = ExtractPrimitive(PrimitiveType.Cube);
                return cubeMesh;
            }
        }

        internal static Mesh CylinderMesh
        {
            get
            {
                if (cylinderMesh == null) cylinderMesh = ExtractPrimitive(PrimitiveType.Cylinder);
                return cylinderMesh;
            }
        }

        /// <summary>지름 1의 구. 내장 Sphere(515정점) 대신 187정점짜리를 쓴다 — 나무 한 그루가 구 네 개다.</summary>
        internal static Mesh SphereMesh
        {
            get
            {
                if (sphereMesh == null) sphereMesh = ProcMeshLibrary.LowSphere(0.5f, 0.5f, 0.5f, 10, 16);
                return sphereMesh;
            }
        }

        /// <summary>지름 1의 원판(+Z를 본다). 눕혀 쓰려면 <see cref="AddFlatDisc"/>.</summary>
        internal static Mesh DiscMesh
        {
            get
            {
                if (discMesh == null) discMesh = ProcMeshLibrary.Disc(0.5f, 0.5f, 0f, 40);
                return discMesh;
            }
        }

        private static Mesh TorusMesh
        {
            get
            {
                if (torusMesh == null) torusMesh = ProcMeshLibrary.Torus(TorusTube, 24, 6);
                return torusMesh;
            }
        }

        private static Mesh PrismMesh
        {
            get
            {
                if (prismMesh == null) prismMesh = BuildPrism();
                return prismMesh;
            }
        }

        private static Mesh ConeMesh
        {
            get
            {
                if (coneMesh == null) coneMesh = BuildCone();
                return coneMesh;
            }
        }

        /// <summary>
        /// 콜라이더 없는 조각 하나. <c>sharedMaterial</c>로 넣는다 — <c>.material</c>은 인스턴스를 복제해
        /// 캐시가 무의미해지고, 복제본은 GameObject를 지워도 남는다.
        /// </summary>
        internal static MeshRenderer AddPiece(Transform parent, string name, Mesh mesh, Material material,
            Vector3 localPos, Vector3 localEuler, Vector3 localScale)
        {
            GameObject go = new GameObject(name);
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = Quaternion.Euler(localEuler);
            t.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        /// <summary>위를 보는 원판. Disc 메시는 +Z를 보므로 X로 −90° 눕힌다(그러면 스케일 y가 Z 폭이 된다).</summary>
        internal static MeshRenderer AddFlatDisc(Transform parent, string name, Material material, Vector3 localPos,
            float sizeX, float sizeZ)
        {
            return NoShadow(AddPiece(parent, name, DiscMesh, material, localPos, new Vector3(-90f, 0f, 0f),
                new Vector3(sizeX, sizeZ, 1f)));
        }

        /// <summary>작은 조각·납작한 판·반투명은 그림자를 끈다 — 섬에 물건이 수백 개라 그림자 드로우콜이 그만큼 준다.</summary>
        internal static MeshRenderer NoShadow(MeshRenderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        /// <summary>
        /// 플레이 중이면 <c>Destroy</c>, 아니면 <c>DestroyImmediate</c>. 배치 캡처(<c>IslandModelCapture</c>)는
        /// 플레이 모드 밖에서 빌더를 부르는데 거기선 <c>Destroy</c>가 오류만 찍고 아무것도 지우지 않는다.
        /// </summary>
        internal static void DestroySafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }

        // 내장 메시만 꺼내고 프리미티브는 버린다. 끄고 나서 지우는 건 Destroy가 프레임 끝까지 미뤄지는 동안
        // 원점에 콜라이더가 남지 않게 하려는 것이다.
        private static Mesh ExtractPrimitive(PrimitiveType type)
        {
            GameObject temp = GameObject.CreatePrimitive(type);
            Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            temp.SetActive(false);
            DestroySafe(temp);
            return mesh;
        }

        // 단위 삼각기둥: x ∈ [−0.5, 0.5], y ∈ [0, 1], z ∈ [−0.5, 0.5], 마루는 (0, 1, z).
        // 면마다 정점을 따로 둔다 — 공유하면 RecalculateNormals가 마루에서 노멀을 평균 내 지붕 두 면의 명암 차가 사라진다.
        private static Mesh BuildPrism()
        {
            Vector3 a0 = new Vector3(-0.5f, 0f, -0.5f), b0 = new Vector3(0.5f, 0f, -0.5f), c0 = new Vector3(0f, 1f, -0.5f);
            Vector3 a1 = new Vector3(-0.5f, 0f, 0.5f), b1 = new Vector3(0.5f, 0f, 0.5f), c1 = new Vector3(0f, 1f, 0.5f);

            Mesh mesh = new Mesh { name = "IslandPrism" };
            mesh.vertices = new[]
            {
                a0, c0, b0,          // 뒤 박공
                a1, b1, c1,          // 앞 박공
                a0, a1, c1, c0,      // 왼 지붕면
                b0, c0, c1, b1,      // 오른 지붕면
                a0, b0, b1, a1,      // 밑면
            };
            // 와인딩은 cross(b − a, c − a)가 바깥을 향하는 순서(ProcMeshLibrary와 같은 규약). 뒤집히면 예외 없이 안 보인다.
            mesh.triangles = new[]
            {
                0, 1, 2,
                3, 4, 5,
                6, 7, 8, 6, 8, 9,
                10, 11, 12, 10, 12, 13,
                14, 15, 16, 14, 16, 17,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.HideAndDontSave;   // 프로세스 수명 — 씬 저장·언로드 대상에서 뺀다
            return mesh;
        }

        // 단위 원뿔: 밑면 지름 1(y = 0), 꼭짓점 (0, 1, 0). 옆면은 면마다 정점을 따로 둬 각진 저폴리로 둔다.
        private static Mesh BuildCone()
        {
            Vector3[] verts = new Vector3[ConeSides * 3 + ConeSides + 1];
            int[] tris = new int[ConeSides * 6];

            for (int i = 0; i < ConeSides; i++)
            {
                int s = i * 3;
                verts[s] = ConeRim(i);
                verts[s + 1] = Vector3.up;
                verts[s + 2] = ConeRim(i + 1);
                tris[s] = s;
                tris[s + 1] = s + 1;
                tris[s + 2] = s + 2;
            }

            int center = ConeSides * 3;
            verts[center] = Vector3.zero;
            for (int i = 0; i < ConeSides; i++) verts[center + 1 + i] = ConeRim(i);
            for (int i = 0; i < ConeSides; i++)
            {
                int t = ConeSides * 3 + i * 3;
                tris[t] = center;
                tris[t + 1] = center + 1 + i;
                tris[t + 2] = center + 1 + (i + 1) % ConeSides;
            }

            Mesh mesh = new Mesh { name = "IslandCone" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.HideAndDontSave;
            return mesh;
        }

        private static Vector3 ConeRim(int index)
        {
            float a = (index % ConeSides) / (float)ConeSides * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f);
        }
    }
}
