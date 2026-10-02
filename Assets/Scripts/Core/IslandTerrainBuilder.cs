using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 섬의 땅·모래톱·바다·나루터 <b>모양만</b> 짓는다. root 원점 = 섬 중심·지면(y = 0).
    ///
    /// <b>콜라이더가 없다.</b> 밟는 땅과 경계 벽은 <c>IslandWorldBuilder.BuildColliders</c>가 따로 세운다 —
    /// 플레이어 접지와 클릭-이동이 콜라이더를 보므로 모래톱이나 바다에 콜라이더가 있으면 그리로 걸어 나간다.
    ///
    /// 잔디는 <b>윗면이 정확히 y = 0인 평면</b>이다. 물건이 그 위에 y = 0 기준으로 놓이고 화면 좌표 → 칸 변환
    /// (<c>IslandWorldBuilder.ScreenToCell</c>)도 그 평면을 쓴다. 칸이 은은히 읽히도록 두 톤 체커로 칠하되
    /// 칸마다 오브젝트를 두지 않고 <b>톤별로 한 메시</b>에 굽는다(가장 큰 섬 484칸이 렌더러 3개).
    /// </summary>
    public static class IslandTerrainBuilder
    {
        // 색은 감마 반사율이고 윗면은 약 1.25배로 나온다(IslandObjectBuilder 머리 주석). 두 잔디 톤의 차는 6%쯤 —
        // 더 벌리면 장기판이 되어 그 위 물건보다 바닥이 먼저 눈에 들어온다.
        private static readonly Color GrassLight = new Color(0.36f, 0.545f, 0.265f);
        private static readonly Color GrassDark = new Color(0.33f, 0.51f, 0.245f);
        // 도착 칸(나루터 앞 2×2) — 물건을 놓을 수 없는 자리임을 바닥색으로 미리 알린다.
        private static readonly Color ArrivalPad = new Color(0.64f, 0.56f, 0.40f);

        // 바다는 섬에서 멀어질수록 짙어지다가 맨 바깥 띠에서 다시 옅어진다. 맨 바깥 색은 안개(0.66, 0.84, 0.95 · Exp 0.006)를
        // 150m 거친 결과가 카메라 배경(0.56, 0.80, 0.96)에 닿도록 역산한 값이다 — 수평선에 금이 서지 않는다.
        private static readonly Color SeaFloor = new Color(0.455f, 0.69f, 0.63f);
        private static readonly Color SeaMid = new Color(0.29f, 0.575f, 0.69f);
        private static readonly Color SeaDeep = new Color(0.175f, 0.415f, 0.64f);
        private static readonly Color SeaFar = new Color(0.27f, 0.53f, 0.735f);
        private static readonly Color SeaWater = new Color(0.375f, 0.695f, 0.735f, 0.5f);

        /// <summary>수면 높이. 섬이 물 위로 이만큼 솟아 있다.</summary>
        private const float SeaLevel = -0.5f;

        // 모래톱 단면은 타원이다: 잔디 가장자리에서 SandTopY로 시작해 바깥으로 SandReach만큼 가며 SandDrop만큼 떨어진다.
        // 변(누운 원기둥)과 모서리(구)가 같은 단면이라 이음매 없이 둥글게 돈다. 수면과 만나는 자리가 잔디에서 약 2.8m다.
        private const float SandTopY = -0.05f;
        private const float SandReach = 3.6f;
        private const float SandDrop = 1.2f;

        // 반투명 수면 한 장은 섬 둘레에만 깐다(모래가 물밑으로 비치는 띠). 그 아래·바깥은 전부 불투명이다 —
        // 반투명을 겹겹이 깔면 정렬이 깨지고, 섬 위의 연못·온실 유리와도 순서가 얽힌다.
        private const float ShallowMargin = 3.5f;
        private const float ShallowFloorY = -0.97f;
        private const float MidBand = 12f;
        private const float DeepRadius = 72f;
        private const float FarRadius = 150f;

        // 0.02가 아니라 0.025인 건 꾸미기 격자선(IslandWorldBuilder, y = 0.02) 때문이다 — 같은 높이면 남쪽 가장자리 선이
        // 첫 널판과 겹쳐 지글거린다.
        private const float PierTopY = 0.025f;
        private const float PierWidth = 1.9f;
        private const int PierPlanks = 9;
        private const float PierPlankPitch = 0.54f;

        // 체커 메시는 프로세스 수명 캐시다(ProcMeshLibrary와 같은 이유 — 소유자가 섬 인스턴스가 아니라 프로세스).
        // 섬을 드나들 때마다 만들었다 지우면 "누가 아직 쓰는가"를 따져야 하는데, 조합이 크기 4단계 × 톤 3개로 끝난다.
        private static readonly Dictionary<int, Mesh> tileMeshes = new Dictionary<int, Mesh>();

        private const int ToneLight = 0;
        private const int ToneDark = 1;
        private const int ToneArrival = 2;

        /// <summary>섬의 땅·모래톱·바다·나루터 <b>모양만</b> 짓는다. root 원점 = 섬 중심·지면(y=0). 콜라이더 없음.</summary>
        public static GameObject Build(int sizeLevel, Transform parent, IslandMaterialCache materials)
        {
            GameObject root = new GameObject("IslandTerrain");
            root.transform.SetParent(parent, false);
            if (materials == null) return root;

            int gridSize = IslandGrid.GridSize(sizeLevel);
            float half = IslandGrid.HalfExtent(sizeLevel);

            BuildGrass(root.transform, gridSize, half, materials);
            BuildSand(root.transform, half, materials);
            BuildSea(root.transform, half, materials);
            BuildPier(root.transform, half, materials);
            BuildIslets(root.transform, materials);
            return root;
        }

        private static void BuildGrass(Transform root, int gridSize, float half, IslandMaterialCache materials)
        {
            AddTiles(root, "GrassTiles_Light", gridSize, ToneLight, materials.Get(GrassLight));
            AddTiles(root, "GrassTiles_Dark", gridSize, ToneDark, materials.Get(GrassDark));
            AddTiles(root, "ArrivalPad", gridSize, ToneArrival, materials.Get(ArrivalPad));

            // 잔디 판의 옆면(흙 단면). 윗면은 타일 메시 바로 밑에 숨기고, 모래톱 원기둥·구의 섬 안쪽 절반도 이 상자가 삼킨다.
            IslandObjectBuilder.NoShadow(IslandObjectBuilder.AddPiece(root, "GrassSkirt", IslandObjectBuilder.CubeMesh,
                materials.Get(IslandObjectBuilder.Soil), new Vector3(0f, -0.335f, 0f), Vector3.zero,
                new Vector3(half * 2f, 0.66f, half * 2f)));
        }

        private static void AddTiles(Transform root, string name, int gridSize, int tone, Material material)
        {
            // 평평한 바닥은 그림자를 드리울 데가 없다(받기만 한다).
            IslandObjectBuilder.NoShadow(IslandObjectBuilder.AddPiece(root, name, TileMesh(gridSize, tone), material,
                Vector3.zero, Vector3.zero, Vector3.one));
        }

        /// <summary>
        /// 그 톤에 해당하는 칸만 모은 메시(y = 0, 위를 본다). 체커는 <b>섬 중심 기준</b>이라 섬을 넓혀도
        /// 이미 놓인 물건 밑의 무늬가 바뀌지 않는다. 도착 칸 판정은 <see cref="IslandGrid.TouchesArrival"/>을 그대로 쓴다 —
        /// 여기서 좌표를 다시 적으면 규칙이 바뀔 때 바닥색만 옛 자리에 남는다.
        /// </summary>
        private static Mesh TileMesh(int gridSize, int tone)
        {
            int key = gridSize * 4 + tone;
            if (tileMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;

            int half = gridSize / 2;
            float cs = GameConstants.Island.CellSize;
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<int> tris = new List<int>();

            for (int z = -half; z < half; z++)
            {
                for (int x = -half; x < half; x++)
                {
                    int cellTone = IslandGrid.TouchesArrival(x, z, 1, 1, gridSize) ? ToneArrival
                        : ((x + z) & 1) == 0 ? ToneLight : ToneDark;
                    if (cellTone != tone) continue;

                    // 이웃 칸과 같은 식((x + 1) * cs)으로 모서리를 내야 좌표가 비트 단위로 같아 틈이 안 생긴다.
                    float x0 = x * cs, x1 = (x + 1) * cs;
                    float z0 = z * cs, z1 = (z + 1) * cs;
                    int v = verts.Count;
                    verts.Add(new Vector3(x0, 0f, z0));
                    verts.Add(new Vector3(x0, 0f, z1));
                    verts.Add(new Vector3(x1, 0f, z1));
                    verts.Add(new Vector3(x1, 0f, z0));
                    for (int i = 0; i < 4; i++) norms.Add(Vector3.up);
                    // cross(b − a, c − a)가 +Y인 순서 — 뒤집히면 위에서 통째로 안 보인다.
                    tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
                    tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
                }
            }

            Mesh mesh = new Mesh { name = "IslandTiles_" + gridSize + "_" + tone };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.HideAndDontSave;
            tileMeshes[key] = mesh;
            return mesh;
        }

        private static void BuildSand(Transform root, float half, IslandMaterialCache materials)
        {
            Material sand = materials.Get(IslandObjectBuilder.Sand);
            float centerY = SandTopY - SandDrop;
            float across = SandReach * 2f;
            float tall = SandDrop * 2f;

            // 변: 누운 원기둥의 위·바깥 4분면이 해변이 된다. 원기둥은 높이가 2라 스케일 y가 곧 반길이(half)다.
            // (0,0,90)은 축을 X로 눕힌다 — 로컬 x가 세로, z가 폭. (90,0,0)은 축을 Z로 눕힌다 — 로컬 x가 폭, z가 세로.
            Vector3 alongX = new Vector3(tall, half, across);
            Vector3 alongZ = new Vector3(across, half, tall);
            AddSand(root, "Sand_N", IslandObjectBuilder.CylinderMesh, sand, new Vector3(0f, centerY, half),
                new Vector3(0f, 0f, 90f), alongX);
            AddSand(root, "Sand_S", IslandObjectBuilder.CylinderMesh, sand, new Vector3(0f, centerY, -half),
                new Vector3(0f, 0f, 90f), alongX);
            AddSand(root, "Sand_E", IslandObjectBuilder.CylinderMesh, sand, new Vector3(half, centerY, 0f),
                new Vector3(90f, 0f, 0f), alongZ);
            AddSand(root, "Sand_W", IslandObjectBuilder.CylinderMesh, sand, new Vector3(-half, centerY, 0f),
                new Vector3(90f, 0f, 0f), alongZ);

            // 모서리: 같은 단면의 타원체 — 변의 원기둥 끝과 정확히 맞물려 모서리가 둥글게 돈다.
            Vector3 corner = new Vector3(across, tall, across);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -half : half;
                float z = (i < 2) ? -half : half;
                AddSand(root, "Sand_Corner", IslandObjectBuilder.SphereMesh, sand, new Vector3(x, centerY, z),
                    Vector3.zero, corner);
            }
        }

        private static void AddSand(Transform root, string name, Mesh mesh, Material sand, Vector3 pos, Vector3 euler,
            Vector3 scale)
        {
            IslandObjectBuilder.NoShadow(IslandObjectBuilder.AddPiece(root, name, mesh, sand, pos, euler, scale));
        }

        private static void BuildSea(Transform root, float half, IslandMaterialCache materials)
        {
            // 마른 모래의 폭 = 타원 단면이 수면과 만나는 자리. 섬이 네모라 모서리가 가장 멀리 나간다.
            float t = (SeaLevel - (SandTopY - SandDrop)) / SandDrop;
            float dryWidth = SandReach * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
            float shallow = half * Mathf.Sqrt(2f) + dryWidth + ShallowMargin;

            // 불투명 층은 바깥일수록 조금씩 낮다 — 같은 높이에 겹치면 z-fighting으로 띠 경계가 지글거린다.
            // 얕은 바닥이 수면보다 0.47m 낮은 건 모래 비탈이 물밑으로 0.7m쯤 더 비쳐 보이게 하려는 것이다.
            AddSeaDisc(root, "SeaFar", FarRadius, ShallowFloorY - 0.09f, materials.Get(SeaFar));
            AddSeaDisc(root, "SeaDeep", Mathf.Max(DeepRadius, shallow + MidBand + 20f), ShallowFloorY - 0.06f,
                materials.Get(SeaDeep));
            AddSeaDisc(root, "SeaMid", shallow + MidBand, ShallowFloorY - 0.03f, materials.Get(SeaMid));
            AddSeaDisc(root, "SeaShallowFloor", shallow + 0.6f, ShallowFloorY, materials.Get(SeaFloor));
            AddSeaDisc(root, "SeaShallowWater", shallow, SeaLevel, materials.GetFade(SeaWater));
        }

        private static void AddSeaDisc(Transform root, string name, float radius, float y, Material material)
        {
            MeshRenderer renderer = IslandObjectBuilder.AddFlatDisc(root, name, material, new Vector3(0f, y, 0f),
                radius * 2f, radius * 2f);
            // 바다는 섬 물건의 그림자 거리 밖이 대부분이다 — 받는 쪽도 꺼서 넓은 판의 그림자 샘플링을 아낀다.
            renderer.receiveShadows = false;
        }

        // 나루터는 남쪽(−Z) 가장자리 가운데. 도착 칸(IslandGrid.ArrivalPoint) 바로 남쪽에서 바다로 뻗는다.
        private static void BuildPier(Transform root, float half, IslandMaterialCache materials)
        {
            Transform pier = new GameObject("Pier").transform;
            pier.SetParent(root, false);
            pier.localPosition = new Vector3(0f, 0f, -half);

            Material light = materials.Get(IslandObjectBuilder.WoodLight);
            Material mid = materials.Get(IslandObjectBuilder.Wood);
            Material dark = materials.Get(IslandObjectBuilder.WoodDark);
            Mesh cube = IslandObjectBuilder.CubeMesh;
            Mesh cylinder = IslandObjectBuilder.CylinderMesh;

            // 널판은 잔디 위로 0.1m 걸쳐 시작한다 — 딱 가장자리에서 시작하면 잔디 옆면과 널판 사이로 모래가 실금처럼 보인다.
            // 두 톤을 번갈아 깔아 위에서 "판자를 이어 붙인 길"로 읽히게 한다.
            float length = PierPlanks * PierPlankPitch;
            for (int i = 0; i < PierPlanks; i++)
            {
                float z = 0.1f - PierPlankPitch * (i + 0.5f);
                IslandObjectBuilder.AddPiece(pier, "Pier_Plank", cube, i % 2 == 0 ? light : mid,
                    new Vector3(0f, PierTopY - 0.035f, z), Vector3.zero,
                    new Vector3(PierWidth, 0.07f, PierPlankPitch - 0.03f));
            }

            float centerZ = 0.1f - length * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                IslandObjectBuilder.NoShadow(IslandObjectBuilder.AddPiece(pier, "Pier_Beam", cube, dark,
                    new Vector3(side * 0.8f, PierTopY - 0.13f, centerZ), Vector3.zero,
                    new Vector3(0.14f, 0.12f, length)));

                // 말뚝은 모래·바닷속까지 박힌다. 맨 끝 한 쌍만 높여 배를 매는 자리로 둔다.
                for (int i = 0; i < 3; i++)
                {
                    bool mooring = i == 2;
                    float top = mooring ? 0.75f : 0.3f;
                    const float bottom = -1.3f;
                    IslandObjectBuilder.AddPiece(pier, "Pier_Post", cylinder, dark,
                        new Vector3(side * (PierWidth * 0.5f + 0.07f), (top + bottom) * 0.5f, -1.0f - i * 1.8f),
                        Vector3.zero, new Vector3(0.2f, (top - bottom) * 0.5f, 0.2f));
                }
            }

            BuildBoat(pier, light, mid, dark);
        }

        // 작은 배 — 위에서 뱃머리가 뾰족한 윤곽 + 짙은 안쪽 + 밝은 좌판 둘로 읽힌다. 뱃머리는 45° 돌린 상자의 모서리다.
        private static void BuildBoat(Transform pier, Material light, Material mid, Material dark)
        {
            Transform boat = new GameObject("Boat").transform;
            boat.SetParent(pier, false);
            boat.localPosition = new Vector3(2.15f, SeaLevel + 0.1f, -3.3f);
            boat.localRotation = Quaternion.Euler(0f, 8f, 0f);

            Mesh cube = IslandObjectBuilder.CubeMesh;
            Vector3 diagonal = new Vector3(0f, 45f, 0f);
            IslandObjectBuilder.AddPiece(boat, "Hull", cube, mid, Vector3.zero, Vector3.zero,
                new Vector3(1.0f, 0.36f, 1.7f));
            IslandObjectBuilder.AddPiece(boat, "Bow", cube, mid, new Vector3(0f, 0f, -0.85f), diagonal,
                new Vector3(0.707f, 0.36f, 0.707f));
            IslandObjectBuilder.NoShadow(IslandObjectBuilder.AddPiece(boat, "Inside", cube, dark,
                new Vector3(0f, 0.185f, 0.02f), Vector3.zero, new Vector3(0.78f, 0.03f, 1.5f)));
            IslandObjectBuilder.NoShadow(IslandObjectBuilder.AddPiece(boat, "InsideBow", cube, dark,
                new Vector3(0f, 0.185f, -0.75f), diagonal, new Vector3(0.55f, 0.03f, 0.55f)));
            IslandObjectBuilder.NoShadow(IslandObjectBuilder.AddPiece(boat, "SeatFront", cube, light,
                new Vector3(0f, 0.23f, -0.25f), Vector3.zero, new Vector3(0.9f, 0.05f, 0.2f)));
            IslandObjectBuilder.NoShadow(IslandObjectBuilder.AddPiece(boat, "SeatBack", cube, light,
                new Vector3(0f, 0.23f, 0.45f), Vector3.zero, new Vector3(0.9f, 0.05f, 0.2f)));
        }

        // 먼바다의 바위섬 — 수평선이 비어 보이지 않게. 위치는 섬 크기와 무관하게 고정이다(가장 큰 섬의 얕은 바다 밖).
        private static void BuildIslets(Transform root, IslandMaterialCache materials)
        {
            AddIslet(root, new Vector3(-40f, 0f, 52f), 1.0f, 20f, materials);
            AddIslet(root, new Vector3(56f, 0f, 38f), 0.7f, -35f, materials);
            AddIslet(root, new Vector3(34f, 0f, -52f), 1.25f, 70f, materials);
        }

        private static void AddIslet(Transform root, Vector3 position, float scale, float yaw,
            IslandMaterialCache materials)
        {
            Transform islet = new GameObject("Islet").transform;
            islet.SetParent(root, false);
            islet.localPosition = position;
            islet.localRotation = Quaternion.Euler(0f, yaw, 0f);
            islet.localScale = Vector3.one * scale;

            Mesh sphere = IslandObjectBuilder.SphereMesh;
            IslandObjectBuilder.NoShadow(IslandObjectBuilder.AddPiece(islet, "Rock", sphere,
                materials.Get(IslandObjectBuilder.Stone), new Vector3(0f, -0.4f, 0f), Vector3.zero,
                new Vector3(11f, 6f, 8.5f)));
            IslandObjectBuilder.NoShadow(IslandObjectBuilder.AddPiece(islet, "RockSide", sphere,
                materials.Get(IslandObjectBuilder.StoneDark), new Vector3(4.2f, -0.6f, 2.2f), Vector3.zero,
                new Vector3(6f, 3.6f, 5f)));
            IslandObjectBuilder.NoShadow(IslandObjectBuilder.AddPiece(islet, "Green", sphere,
                materials.Get(IslandObjectBuilder.Leaf), new Vector3(-0.6f, 2.25f, -0.2f), Vector3.zero,
                new Vector3(5.5f, 1.0f, 4.4f)));
        }
    }
}
