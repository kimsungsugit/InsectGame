using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 리전 표면 장식 — 바닥 얼룩, 발밑 디테일(풀·꽃·조약돌·낙엽·눈덩이·재…), 물(호수·웅덩이),
    /// 그리고 울타리 <b>바깥</b> 테두리(사구·설원 둑·숲 가장자리·암벽).
    ///
    /// 왜 필요한가: 게임 카메라는 (0,9,-6) 고각이라 화면의 대부분이 <b>발밑 15×10m</b>다. 그런데 그 자리가
    /// 단색 바닥 + 길 하나뿐이었다(배치 캡처). 큰 소품을 더 세우면 캐릭터를 가리므로(숲 나무를 관목 키로
    /// 줄인 이력이 있다) 여기선 <b>무릎 아래 디테일</b>과 <b>밖에 두르는 테두리</b>로 채운다.
    ///
    /// 규칙:
    /// <list type="bullet">
    ///   <item><b>콜라이더 없음.</b> 전부 장식이다. 플레이어 접지는 올라가기만 하고 내려오지 않으므로
    ///     (<c>PlayerMovement</c>의 <c>Max</c>) 밟히는 콜라이더를 두면 공중에 뜬다.</item>
    ///   <item><b>길·마을·전초기지·물·제단을 피한다</b> — 필드 도로(<see cref="WorldRouteLayout"/>), 리전 안 길
    ///     (<see cref="RegionTerrainBuilder.InternalPaths"/>), 본 마을, 전초기지, 연못 강, 수문장 제단.</item>
    ///   <item><b>남쪽 테두리는 낮게</b>. 카메라가 늘 플레이어 남쪽 위에 있어서, 남쪽 울타리 밖의 큰 나무는
    ///     가장자리에 선 플레이어를 가린다(카메라 차폐 검사는 콜라이더만 본다). 북쪽이 카메라가 보는 배경이다.</item>
    ///   <item><b>칸 × 색으로 합친다</b>(<see cref="SceneryBatcher"/>) — 풀포기 수천 개가 드로우콜 수십 번.</item>
    ///   <item>배치는 시드 고정(리전마다 따로 — <see cref="RegionSeed"/>), 전역 난수 상태는 끝나면 되돌린다.</item>
    /// </list>
    ///
    /// 마을·전초기지·수문장 제단이 <c>BuildSystems</c>에서 지어지므로(지형보다 뒤) 실제 빌드는 <c>Start</c>에서 한다.
    /// 리전 루트 이름이 <c>Scenery_</c>로 시작하는 건 서브에리어 진입 때 <c>HideMainWorld</c>가 접두어로 끄기 때문이다.
    /// 그 아래 칸 오브젝트에는 접두어를 달지 않는다(<see cref="SceneryBatcher.Build"/>) — 루트만 꺼도 따라 꺼진다.
    /// </summary>
    public class RegionDressingBuilder : MonoBehaviour
    {
        private const int LayoutSeed = 20260929;
        private const float CellSize = 32f;
        private const float FloorY = FieldGround.FloorY;

        private RegionData[] regions;
        private RegionTerrainBuilder terrain;
        private bool built;

        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly List<Texture2D> ownedTextures = new List<Texture2D>();
        // 색 + 표면(발광·광택) 키 — 리전 13곳의 flat·rim 배처가 전부 이 사전 하나를 넘겨 같은 표면끼리 머티리얼을 나눠 쓴다
        private readonly Dictionary<SceneryMaterialKey, Material> materialCache = new Dictionary<SceneryMaterialKey, Material>();
        private readonly SceneryPalette palette = new SceneryPalette();

        /// <summary>
        /// 방향 있는 사각형 회피 — 물길(연못 강)·부두 널판용. 원으로 덮으면 26×5m 띠의 옆이 크게 부풀고, 도로처럼
        /// 선분(<see cref="Lane"/>)으로 두면 <c>avoidPaths: false</c> 소품(조약돌·얼룩)이 물 위에 앉는다.
        /// </summary>
        private struct KeepStrip
        {
            public Vector3 center;
            public Vector3 axis;          // 길이 방향(XZ 단위 벡터)
            public float halfLength;
            public float halfWidth;
        }

        // 회피 — XZ 선분(반폭 포함), 원, 방향 있는 사각형, 불규칙 원반(호수 윤곽 × 배율)
        private readonly List<Vector3> laneA = new List<Vector3>();
        private readonly List<Vector3> laneB = new List<Vector3>();
        private readonly List<float> laneHalf = new List<float>();
        private readonly List<Vector4> keepOut = new List<Vector4>();   // xz + 반경(w)
        private readonly List<KeepStrip> strips = new List<KeepStrip>();
        private readonly List<(DiscPlacement disc, float fraction)> discKeepOut = new List<(DiscPlacement disc, float fraction)>();
        // 지금 짓는 리전 근처의 것만 — 소품 수만 개 × 전 월드 도로 선분을 매번 훑지 않게
        private readonly List<int> nearLanes = new List<int>();
        private readonly List<int> nearKeep = new List<int>();

        // 자체 메시(파괴 대상) — ProcMeshLibrary 것은 프로세스 캐시라 파괴하지 않는다
        private Mesh tuftMesh, fernMesh, patchMesh, lakeMesh, coneMesh, bladeMesh, stalkMesh, bloomMesh;
        private Mesh sphereMesh, pebbleMesh, blockMesh, stemMesh, leafMesh;

        public void Configure(RegionData[] regionDefs, RegionTerrainBuilder regionTerrain)
        {
            regions = regionDefs;
            terrain = regionTerrain;
        }

        // 물 기록은 정적이라 씬을 넘어 남는다 — 새 씬의 곤충 스포너가 옛 씬의 기록을 "다 됐다"로 읽지 않게, 어느 Start보다
        // 먼저 도는 여기서 비운다(부트스트랩이 Awake에서 이 컴포넌트를 붙인다). 물 판정 절 참조.
        private void Awake()
        {
            ClearWater();
        }

        private void Start()
        {
            Build();
        }

        public void Build()
        {
            if (built || regions == null) return;
            built = true;

            Random.State prev = Random.state;
            Random.InitState(LayoutSeed);
            try
            {
                ClearWater();
                CreateMeshes();
                CollectAvoidance();
                foreach (RegionData r in regions)
                {
                    if (r == null) continue;
                    BuildRegion(r);
                }
                // 난수를 쓰지 않는다 — 지어진 물을 읽기만 한다. 끝까지 지었을 때만 "기록 완료"로 둔다: 도중에 예외가 나면
                // 호수를 못 적었을 수 있어, 기록 전 판정(순수 윤곽의 호수)이 반쪽 기록보다 낫다.
                RecordSceneWater();
                waterRecorded = true;
            }
            finally
            {
                Random.state = prev;
                // 빌드 도중 예외가 나도 캐시는 비운다 — 원본 배열 캐시가 이 빌더의 메시(파괴 대상)를 키로 쥔 채
                // 남으면 다음 씬에서 인스턴스 ID가 재사용될 때 엉뚱한 배열이 나온다(ClearSourceCache 주석).
                palette.Apply();                  // 마지막 리전까지 모인 색을 텍스처에 올린다
                SceneryBatcher.ClearSourceCache();
            }

            // 모바일 예산 확인용 — 합친 메시 수와 정점 총량(정점당 32바이트, CPU 사본은 버렸다)
            long verts = 0;
            foreach (Mesh m in ownedMeshes) if (m != null) verts += m.vertexCount;
            Debug.Log($"[RegionDressingBuilder] 메시 {ownedMeshes.Count}개 · 정점 {verts:N0} · 머티리얼 {ownedMaterials.Count}개");
            // 2026-09-28 실측: 색별 머티리얼·캡슐 줄기 시절엔 메시 3,407개 · 정점 101만 · 머티리얼 201개였다 → 팔레트 텍스처로 교체
        }

        // ======= 리전 =======

        /// <summary>
        /// 리전마다 난수 줄기를 새로 시작한다. 한 줄기로 이어 뽑으면 앞 리전에서 호출 수가 하나만 바뀌어도
        /// (회피 원을 하나 더해 <see cref="TryInside"/>가 한 번 더 구르기만 해도) 뒤 리전의 배치가 통째로 밀려,
        /// 연못 하나를 고쳤는데 모래언덕 캡처까지 달라진다. <c>string.GetHashCode</c>는 런타임마다 달라질 수 있어 직접 섞는다.
        /// </summary>
        private static int RegionSeed(string regionId)
        {
            unchecked
            {
                int h = LayoutSeed;
                for (int i = 0; i < regionId.Length; i++) h = h * 31 + regionId[i];
                return h;
            }
        }

        private void BuildRegion(RegionData r)
        {
            Random.InitState(RegionSeed(r.regionId));   // Build의 finally가 전역 상태를 되돌린다
            SelectNearby(r);
            var root = new GameObject($"Scenery_Dressing_{r.regionId}").transform;
            root.SetParent(transform, false);

            // 울타리 안(바닥 얼룩 + 발밑 소품)은 배처 하나다 — 둘 다 그림자를 안 던진다(모바일). 울타리 밖만 그림자를 던진다.
            // 예전엔 같은 배처를 ground·detail 두 이름으로 넘겨서 발광·광택 표시(MarkGlow/MarkWet)가 한쪽에만 걸리는 것처럼
            // 읽혔다 — 실제로는 한 객체라 어느 이름으로 걸어도 안쪽 전부에 걸린다. 이름을 하나(b)로 합쳤다.
            var flat = new SceneryBatcher(CellSize, palette);
            var rim = new SceneryBatcher(CellSize, palette);

            RegionPalette.PatchTones(r.regionId, r.themeColor, out Color light, out Color dark);
            Patches(r, flat, light, dark);

            switch (r.regionId)
            {
                case "meadow": Meadow(r, flat, rim); break;
                case "pond": Pond(r, flat, rim); break;
                case "forest": Forest(r, flat, rim); break;
                case "swamp": Swamp(r, flat, rim); break;
                case "mountain": Mountain(r, flat, rim); break;
                case "garden": Garden(r, flat, rim); break;
                case "ruins": Ruins(r, flat, rim); break;
                case "hollow": Hollow(r, flat, rim); break;
                case "dunes": Dunes(r, flat, rim); break;
                case "frostline": Frostline(r, flat, rim); break;
                case "emberfall": Emberfall(r, flat, rim); break;
                case "canopy": Canopy(r, flat, rim); break;
                case "nameless": Nameless(r, flat, rim); break;
            }

            // 칸 이름엔 Scenery_를 안 단다 — HideMainWorld가 위 root 하나만 끄면 된다(SceneryBatcher.Build 주석)
            flat.Build(root, $"Detail_{r.regionId}", false, ownedMeshes, ownedMaterials, ownedTextures, materialCache);
            rim.Build(root, $"Rim_{r.regionId}", true, ownedMeshes, ownedMaterials, ownedTextures, materialCache);
        }

        /// <summary>바닥 얼룩 — 밝은·짙은 두 톤의 불규칙 원반. 단색 판 느낌을 깬다.</summary>
        private void Patches(RegionData r, SceneryBatcher b, Color light, Color dark)
        {
            int n = Scale(34, r);
            for (int i = 0; i < n; i++)
            {
                if (!TryInside(r, 0.92f, 1.5f, out Vector3 p, avoidPaths: false)) continue;
                float s = Random.Range(3f, 8.5f);
                b.Add(patchMesh, p + Vector3.up * (0.004f + (i % 4) * 0.002f), Yaw(),
                    new Vector3(s * Random.Range(0.8f, 1.5f), 1f, s), i % 2 == 0 ? light : dark);
            }
        }

        // ---------------- 1막 ----------------

        private void Meadow(RegionData r, SceneryBatcher b, SceneryBatcher rim)
        {
            Tufts(r, b, Scale(420, r), 0.34f, 0.6f, 0.9f, C(0.34f, 0.58f, 0.24f), C(0.44f, 0.66f, 0.28f), C(0.52f, 0.64f, 0.26f));
            Flowers(r, b, Scale(150, r), C(0.98f, 0.86f, 0.30f), C(0.98f, 0.98f, 0.94f), C(0.95f, 0.50f, 0.62f), C(0.62f, 0.52f, 0.92f));
            Pebbles(r, b, Scale(60, r), 0.12f, 0.3f, C(0.62f, 0.60f, 0.55f), C(0.52f, 0.50f, 0.46f));
            Clover(r, b, Scale(40, r), C(0.30f, 0.52f, 0.22f));
            RimBushes(r, rim, 0.9f, C(0.30f, 0.50f, 0.22f), C(0.36f, 0.56f, 0.26f));
            RimTrees(r, rim, 0.35f, false, C(0.40f, 0.28f, 0.17f), C(0.28f, 0.50f, 0.22f), C(0.34f, 0.56f, 0.25f));
        }

        private void Pond(RegionData r, SceneryBatcher b, SceneryBatcher rim)
        {
            // 호수 — 옛날엔 리전 바닥 전체가 파란색이라 "물"을 흉내 냈다. 바닥을 물가 풀밭으로 바꾸고 진흙 + 여울 + 깊은 물
            // 세 겹 원반을 깐다. 윤곽·자리는 PondLake가 단일 출처다(부두·NPC·입구와의 여유가 거기 적혀 있다).
            PondLakeLayout lake = PondLake(r.centerPosition, r.radius);
            Color shallow = C(0.42f, 0.64f, 0.66f), deep = C(0.22f, 0.46f, 0.62f), mud = C(0.42f, 0.38f, 0.28f);
            b.MarkWet(shallow);
            b.MarkWet(deep);
            b.Add(lakeMesh, lake.mud.center + Vector3.up * 0.014f, lake.mud.Rotation, lake.mud.Scale, mud);
            b.Add(lakeMesh, lake.water.center + Vector3.up * 0.02f, lake.water.Rotation, lake.water.Scale, shallow);
            b.Add(lakeMesh, lake.deep.center + Vector3.up * 0.026f, lake.deep.Rotation, lake.deep.Scale, deep);
            AddWaterDisc(lake.water);   // 깊은 물은 여울 안쪽 2m 이상에 머문다(PondLake 주석) — 여울 윤곽이 곧 물가다

            // 나루터 오두막(VillageBuilder.BuildPierHut) 밑의 물가 — 옛날엔 리전 전체가 파란 바닥이라 말뚝 오두막과
            // 부두 널판이 물 위에 선 것처럼 보였다. 바닥이 풀밭이 되자 부두가 맨땅으로 뻗었다. 오두막(로컬 z -2.5)과
            // 널판(x -2.6) 쪽에 작은 물가를 깐다 — 모닥불(0,2.5)·주민 자리(1.5,3.2)는 비껴간다.
            // 큰 호수와는 진흙끼리 6.4m 떨어져 있어 두 물이 이어 붙은 이상한 모양이 되지 않는다(PondLake 주석).
            GameObject pier = GameObject.Find("Outpost_pond");
            if (pier != null)
            {
                Vector3 cove = Floor(pier.transform.TransformPoint(new Vector3(-3.2f, 0f, -2.5f)));
                Quaternion yaw = pier.transform.rotation;
                b.Add(patchMesh, cove + Vector3.up * 0.014f, yaw, new Vector3(10.8f, 1f, 9.6f), mud);
                b.Add(patchMesh, cove + Vector3.up * 0.02f, yaw * Quaternion.Euler(0f, 30f, 0f), new Vector3(9.4f, 1f, 8.4f), shallow);
                b.Add(patchMesh, cove + Vector3.up * 0.026f, yaw * Quaternion.Euler(0f, 75f, 0f), new Vector3(7.6f, 1f, 6.8f), deep);
                // 물은 여울·깊은 물 두 장 — 요가 달라(30°·75°) 깊은 물이 여울 밖으로 삐져나오는 방향이 있다. 진흙은 뭍이다.
                // 인자는 위 b.Add와 글자 그대로 같아야 한다(FieldGroundPlacementTests가 소스에서 대조한다).
                RecordWaterPatch(cove, yaw * Quaternion.Euler(0f, 30f, 0f), new Vector3(9.4f, 1f, 8.4f));
                RecordWaterPatch(cove, yaw * Quaternion.Euler(0f, 75f, 0f), new Vector3(7.6f, 1f, 6.8f));
                for (int i = 0; i < 18; i++)
                {
                    Vector3 q = cove + Polar(i * 20f + 10f, Random.Range(4.3f, 5.2f));
                    if (Vector3.Distance(q, Floor(pier.transform.TransformPoint(new Vector3(0f, 0f, 2.5f)))) < 3f) continue;
                    b.Add(bladeMesh, q, Yaw(), new Vector3(0.5f, Random.Range(0.9f, 1.5f), 0.5f), C(0.40f, 0.54f, 0.24f));
                }
            }

            // 부두 널판(RegionTerrainBuilder)은 콜라이더가 없어 레이캐스트로 못 가린다 — 자리를 직접 받아 갈대·풀이
            // 널판을 뚫고 솟지 않게 막는다(널판 윗면 0.28m, 풀포기는 0.9m까지 솟는다).
            RegionTerrainBuilder.PondDockFootprint(r.centerPosition, r.radius, out Vector3 dock, out Vector2 dockSize);
            strips.Add(new KeepStrip
            {
                center = dock,
                axis = Vector3.forward,
                halfLength = dockSize.y * 0.5f + 0.2f,
                halfWidth = dockSize.x * 0.5f + 0.2f,
            });

            // 수련잎 — 수면 위. 물 윤곽을 각도마다 정확히 재서(PatchEdgeRadius) 가장자리의 88% 안쪽에만 띄운다 —
            // 가장 좁은 쪽(짧은 축 × 가장 들어간 요철, 물가까지 약 13.5m)에서도 잎 가장자리가 물가에서 1m 넘게 안쪽이다.
            for (int i = 0; i < Scale(26, r); i++)
            {
                Vector3 p = lake.water.EdgePoint(Random.Range(0f, Mathf.PI * 2f), 0.88f * Mathf.Sqrt(Random.Range(0.03f, 1f)));
                float s = Random.Range(0.5f, 1.1f);
                b.Add(patchMesh, Floor(p) + Vector3.up * 0.04f, Yaw(), new Vector3(s, 1f, s), i % 3 == 0 ? C(0.34f, 0.60f, 0.26f) : C(0.26f, 0.52f, 0.22f));
                if (i % 4 == 0)
                    b.Add(bloomMesh, Floor(p) + Vector3.up * 0.06f, Quaternion.identity, new Vector3(0.3f, 1f, 0.3f), C(0.98f, 0.78f, 0.86f));
            }
            // 물가 갈대 고리 — 물 가장자리의 94%(여울 안쪽 약 1m)부터 109%(진흙 띠 안, 진흙은 110%)까지. 옛 식은 원형 평균 반경을
            // 기준으로 삼아 요철이 들어간 쪽에선 풀밭에, 나온 쪽에선 물 한가운데에 섰다. 호수 회피는 이 고리를 놓은 <b>뒤에</b>
            // 건다(먼저 걸면 갈대가 제 호수에 걸려 버려진다). 도로·입구·부두 회피는 그대로 본다.
            for (int i = 0; i < Scale(90, r); i++)
            {
                Vector3 p = lake.water.EdgePoint(Random.Range(0f, Mathf.PI * 2f), Random.Range(0.94f, 1.09f));
                if (Blocked(p, 0.4f)) continue;
                float h = Random.Range(1.0f, 1.8f);
                b.Add(bladeMesh, Floor(p), Yaw(), new Vector3(0.5f, h, 0.5f), i % 2 == 0 ? C(0.40f, 0.54f, 0.24f) : C(0.50f, 0.58f, 0.28f));
                if (i % 5 == 0)
                    b.Add(pebbleMesh, Floor(p) + Vector3.up * h, Quaternion.identity, new Vector3(0.09f, 0.3f, 0.09f), C(0.42f, 0.27f, 0.15f));
            }
            // 풀·꽃·조약돌은 물에 안 들어가게 — 물 윤곽의 105%(진흙 띠의 안쪽 절반, 약 1m)까지 막는다. 진흙 바깥 절반엔 풀이 난다.
            // 옛 원형 회피(여울 요철의 최대 반경 17.7m)는 요철이 들어간 쪽에서 진흙 끝과 풀 사이에 최대 3.7m 맨땅을 남겼다.
            discKeepOut.Add((lake.water, LakeClearFraction));

            Tufts(r, b, Scale(300, r), 0.34f, 0.6f, 1f, C(0.36f, 0.58f, 0.30f), C(0.44f, 0.64f, 0.32f));
            Flowers(r, b, Scale(50, r), C(0.64f, 0.70f, 0.98f), C(0.98f, 0.98f, 0.94f));
            Pebbles(r, b, Scale(50, r), 0.12f, 0.35f, C(0.56f, 0.56f, 0.52f), C(0.46f, 0.47f, 0.45f));
            RimReeds(r, rim, C(0.40f, 0.54f, 0.24f), C(0.50f, 0.58f, 0.30f));
            RimBushes(r, rim, 0.5f, C(0.28f, 0.48f, 0.24f), C(0.34f, 0.54f, 0.28f));
            RimTrees(r, rim, 0.25f, false, C(0.40f, 0.30f, 0.18f), C(0.30f, 0.52f, 0.30f), C(0.36f, 0.58f, 0.32f));
        }

        private void Forest(RegionData r, SceneryBatcher b, SceneryBatcher rim)
        {
            Leaves(r, b, Scale(700, r), C(0.52f, 0.36f, 0.18f), C(0.62f, 0.46f, 0.20f), C(0.40f, 0.44f, 0.18f), C(0.70f, 0.38f, 0.16f));
            Ferns(r, b, Scale(130, r), C(0.22f, 0.46f, 0.18f), C(0.28f, 0.52f, 0.20f));
            Tufts(r, b, Scale(220, r), 0.3f, 0.5f, 0.8f, C(0.26f, 0.44f, 0.18f), C(0.32f, 0.50f, 0.20f));
            Mushrooms(r, b, Scale(45, r), C(0.80f, 0.24f, 0.18f), C(0.72f, 0.56f, 0.36f), C(0.92f, 0.86f, 0.74f));
            Twigs(r, b, Scale(80, r), C(0.36f, 0.26f, 0.16f));
            Clover(r, b, Scale(45, r), C(0.24f, 0.44f, 0.20f));   // 이끼
            RimTrees(r, rim, 1.6f, true, C(0.34f, 0.24f, 0.15f), C(0.16f, 0.36f, 0.18f), C(0.20f, 0.42f, 0.20f));
            RimTrees(r, rim, 0.8f, false, C(0.38f, 0.27f, 0.16f), C(0.22f, 0.44f, 0.18f), C(0.28f, 0.50f, 0.20f));
            RimBushes(r, rim, 0.7f, C(0.20f, 0.40f, 0.18f), C(0.26f, 0.46f, 0.20f));
        }

        private void Swamp(RegionData r, SceneryBatcher b, SceneryBatcher rim)
        {
            // 탁한 웅덩이 — 광택으로 젖은 느낌
            Color pool = C(0.20f, 0.25f, 0.19f), poolEdge = C(0.27f, 0.25f, 0.18f);
            b.MarkWet(pool);
            for (int i = 0; i < Scale(12, r); i++)
            {
                if (!TryInside(r, 0.85f, 5f, out Vector3 p, avoidPaths: true)) continue;
                float s = Random.Range(3f, 6.5f);
                Quaternion y = Yaw();
                b.Add(patchMesh, p + Vector3.up * 0.014f, y, new Vector3(s * 1.25f, 1f, s * 1.1f), poolEdge);
                b.Add(patchMesh, p + Vector3.up * 0.02f, y, new Vector3(s, 1f, s * 0.85f), pool);
                RecordWaterPatch(p, y, new Vector3(s, 1f, s * 0.85f));   // 위 b.Add와 같은 인자 — 가장자리(poolEdge)는 진흙이라 뭍
                for (int k = 0; k < 6; k++)
                {
                    Vector3 q = p + Polar(Random.Range(0f, 360f), s * 0.55f * Random.Range(0.8f, 1.1f));
                    b.Add(bladeMesh, Floor(q), Yaw(), new Vector3(0.45f, Random.Range(0.8f, 1.4f), 0.45f), C(0.34f, 0.42f, 0.20f));
                }
            }
            Tufts(r, b, Scale(320, r), 0.32f, 0.55f, 1.1f, C(0.30f, 0.40f, 0.20f), C(0.36f, 0.44f, 0.22f), C(0.40f, 0.42f, 0.24f));
            Mushrooms(r, b, Scale(40, r), C(0.52f, 0.30f, 0.62f), C(0.44f, 0.52f, 0.30f), C(0.80f, 0.78f, 0.66f));
            Clover(r, b, Scale(50, r), C(0.26f, 0.34f, 0.18f));
            Twigs(r, b, Scale(60, r), C(0.28f, 0.24f, 0.18f));
            RimReeds(r, rim, C(0.36f, 0.44f, 0.22f), C(0.44f, 0.46f, 0.26f));
            RimDeadTrees(r, rim, 0.6f, C(0.26f, 0.23f, 0.19f));
            RimBushes(r, rim, 0.6f, C(0.22f, 0.30f, 0.17f), C(0.28f, 0.34f, 0.19f));
        }

        private void Mountain(RegionData r, SceneryBatcher b, SceneryBatcher rim)
        {
            Pebbles(r, b, Scale(260, r), 0.12f, 0.45f, C(0.58f, 0.57f, 0.54f), C(0.48f, 0.47f, 0.45f), C(0.66f, 0.64f, 0.60f));
            Tufts(r, b, Scale(180, r), 0.3f, 0.5f, 0.7f, C(0.52f, 0.56f, 0.28f), C(0.46f, 0.52f, 0.26f));
            Flowers(r, b, Scale(40, r), C(0.98f, 0.98f, 0.94f), C(0.70f, 0.60f, 0.95f));   // 에델바이스·용담
            SnowPatches(r, b, Scale(10, r), C(0.93f, 0.95f, 0.97f));
            RimRocks(r, rim, 1.2f, C(0.50f, 0.49f, 0.46f), C(0.42f, 0.41f, 0.39f), 3.2f);
            RimTrees(r, rim, 0.5f, true, C(0.32f, 0.24f, 0.16f), C(0.18f, 0.34f, 0.22f), C(0.22f, 0.40f, 0.24f));
        }

        private void Garden(RegionData r, SceneryBatcher b, SceneryBatcher rim)
        {
            // 꽃밭 — 분홍 바닥 대신 꽃 자체가 색을 낸다. 무리 지어 심어 화단처럼
            Color[] beds = { C(0.98f, 0.52f, 0.66f), C(0.98f, 0.84f, 0.30f), C(0.96f, 0.96f, 0.96f), C(0.72f, 0.52f, 0.96f), C(0.98f, 0.40f, 0.34f) };
            for (int i = 0; i < Scale(26, r); i++)
            {
                if (!TryInside(r, 0.88f, 3f, out Vector3 center, avoidPaths: true)) continue;
                Color col = beds[i % beds.Length];
                float spread = Random.Range(1.6f, 3.2f);
                for (int k = 0; k < 26; k++)
                {
                    Vector3 p = center + Polar(Random.Range(0f, 360f), spread * Mathf.Sqrt(Random.value));
                    if (Blocked(p, 0.2f)) continue;
                    Flower(b, p, col, Random.Range(0.28f, 0.5f));
                }
            }
            Flowers(r, b, Scale(200, r), beds);
            Tufts(r, b, Scale(300, r), 0.3f, 0.5f, 0.8f, C(0.40f, 0.64f, 0.30f), C(0.48f, 0.70f, 0.32f));
            Petals(r, b, Scale(120, r), C(0.98f, 0.70f, 0.80f), C(0.98f, 0.92f, 0.94f));
            RimHedges(r, rim, C(0.26f, 0.50f, 0.24f), beds);
            RimTrees(r, rim, 0.45f, false, C(0.44f, 0.32f, 0.22f), C(0.96f, 0.72f, 0.80f), C(0.98f, 0.82f, 0.86f));   // 꽃나무
        }

        private void Ruins(RegionData r, SceneryBatcher b, SceneryBatcher rim)
        {
            Rubble(r, b, Scale(150, r), C(0.64f, 0.60f, 0.52f), C(0.56f, 0.52f, 0.45f));
            Tufts(r, b, Scale(240, r), 0.3f, 0.5f, 0.8f, C(0.44f, 0.50f, 0.28f), C(0.50f, 0.54f, 0.30f));
            Clover(r, b, Scale(40, r), C(0.40f, 0.48f, 0.28f));   // 이끼 낀 판석 틈
            Tiles(r, b, Scale(40, r), C(0.62f, 0.58f, 0.50f));
            RimWalls(r, rim, C(0.60f, 0.56f, 0.48f), C(0.52f, 0.48f, 0.41f));
            RimBushes(r, rim, 0.5f, C(0.34f, 0.44f, 0.24f), C(0.40f, 0.48f, 0.26f));
        }

        // ---------------- 2막 ----------------

        private void Hollow(RegionData r, SceneryBatcher b, SceneryBatcher rim)
        {
            // 곤충이 떠난 들 — 색이 빠진 풀, 갈라진 흙. 꽃은 없다
            Tufts(r, b, Scale(300, r), 0.32f, 0.55f, 1.1f, C(0.70f, 0.66f, 0.50f), C(0.62f, 0.58f, 0.44f), C(0.76f, 0.72f, 0.58f));
            Pebbles(r, b, Scale(70, r), 0.12f, 0.3f, C(0.72f, 0.70f, 0.64f), C(0.62f, 0.60f, 0.55f));
            Cracks(r, b, Scale(60, r), C(0.46f, 0.44f, 0.36f));
            Twigs(r, b, Scale(50, r), C(0.56f, 0.52f, 0.44f));
            RimMounds(r, rim, 0.9f, C(0.64f, 0.62f, 0.52f), C(0.58f, 0.56f, 0.47f), 1.6f);
            RimDeadTrees(r, rim, 0.45f, C(0.56f, 0.53f, 0.46f));
        }

        private void Dunes(RegionData r, SceneryBatcher b, SceneryBatcher rim)
        {
            // 모래 물결 — 같은 방향으로 눕힌 가늘고 긴 둔덕. 사구 능선(RegionTerrainBuilder)과 같은 바람 결(35°)
            Color crest = C(0.93f, 0.82f, 0.60f), trough = C(0.80f, 0.66f, 0.45f);
            for (int i = 0; i < Scale(160, r); i++)
            {
                if (!TryInside(r, 0.92f, 1.5f, out Vector3 p, avoidPaths: true)) continue;
                float len = Random.Range(2.5f, 6f);
                // 납작한 줄무늬 — 저정점 구를 길게 늘이면 끝이 뾰족하게 반짝이는 바늘이 된다(실제로 그랬다)
                b.Add(patchMesh, p + Vector3.up * 0.011f, Quaternion.Euler(0f, 35f + Random.Range(-8f, 8f), 0f),
                    new Vector3(len, 1f, 0.5f), i % 3 == 0 ? trough : crest);
            }
            Shrubs(r, b, Scale(70, r), C(0.52f, 0.44f, 0.28f), C(0.44f, 0.46f, 0.30f));
            Pebbles(r, b, Scale(60, r), 0.15f, 0.5f, C(0.72f, 0.56f, 0.40f), C(0.62f, 0.48f, 0.34f));
            RimMounds(r, rim, 1.2f, C(0.90f, 0.77f, 0.54f), C(0.84f, 0.70f, 0.48f), 3.4f);
            RimRocks(r, rim, 0.4f, C(0.74f, 0.56f, 0.38f), C(0.66f, 0.50f, 0.34f), 2.6f);
        }

        private void Frostline(RegionData r, SceneryBatcher b, SceneryBatcher rim)
        {
            Color snow = C(0.95f, 0.97f, 0.99f), snowShade = C(0.84f, 0.89f, 0.95f), ice = C(0.66f, 0.84f, 0.95f);
            // 얼음은 속에서 빛나는 듯 — 그늘에서도 푸르게. 발광 표시는 배처마다 따로라 울타리 밖 얼음 가시(RimIce)에도
            // 건다 — 안쪽에만 걸면 같은 ice 색이 테두리에선 팔레트(무광)로 떨어져 울타리 안팎 얼음이 다른 재질로 보인다.
            Color iceGlow = C(0.10f, 0.16f, 0.22f);
            b.MarkGlow(ice, iceGlow);
            rim.MarkGlow(ice, iceGlow);
            for (int i = 0; i < Scale(200, r); i++)
            {
                if (!TryInside(r, 0.92f, 1f, out Vector3 p, avoidPaths: true)) continue;
                float s = Random.Range(0.25f, 0.8f);
                b.Add(pebbleMesh, Grounded(p), Yaw(), new Vector3(s * 1.4f, s * 0.45f, s), i % 3 == 0 ? snowShade : snow);
            }
            for (int i = 0; i < Scale(55, r); i++)
            {
                if (!TryInside(r, 0.9f, 2f, out Vector3 p, avoidPaths: true)) continue;
                int shards = Random.Range(2, 5);
                for (int k = 0; k < shards; k++)
                {
                    float h = Random.Range(0.35f, 0.9f);
                    b.Add(coneMesh, Grounded(p + Polar(Random.Range(0f, 360f), Random.Range(0f, 0.35f))),
                        Quaternion.Euler(Random.Range(-18f, 18f), Random.Range(0f, 360f), Random.Range(-18f, 18f)),
                        new Vector3(h * 0.35f, h, h * 0.35f), ice);
                }
            }
            Tufts(r, b, Scale(120, r), 0.3f, 0.5f, 0.8f, C(0.72f, 0.80f, 0.84f), C(0.62f, 0.72f, 0.78f));   // 서리 앉은 풀
            Pebbles(r, b, Scale(50, r), 0.12f, 0.35f, C(0.56f, 0.62f, 0.68f), C(0.48f, 0.53f, 0.60f));
            RimMounds(r, rim, 1.1f, snow, snowShade, 2.8f);
            RimIce(r, rim, ice);
            RimTrees(r, rim, 0.55f, true, C(0.30f, 0.26f, 0.22f), C(0.26f, 0.40f, 0.36f), C(0.90f, 0.94f, 0.98f));   // 눈 얹힌 침엽수
        }

        private void Emberfall(RegionData r, SceneryBatcher b, SceneryBatcher rim)
        {
            Color ember = C(1f, 0.42f, 0.12f);
            b.MarkGlow(ember, C(1.6f, 0.55f, 0.12f));
            Color crack = C(0.95f, 0.36f, 0.10f);
            b.MarkGlow(crack, C(1.2f, 0.36f, 0.08f));

            // 재 더미 얼룩 + 굳은 용암 틈(발광 선)
            for (int i = 0; i < Scale(50, r); i++)
            {
                if (!TryInside(r, 0.9f, 2f, out Vector3 p, avoidPaths: true)) continue;
                float s = Random.Range(1.5f, 4f);
                b.Add(patchMesh, p + Vector3.up * 0.012f, Yaw(), new Vector3(s, 1f, s * 0.7f), i % 2 == 0 ? C(0.46f, 0.44f, 0.42f) : C(0.38f, 0.36f, 0.35f));
            }
            for (int i = 0; i < Scale(40, r); i++)
            {
                if (!TryInside(r, 0.88f, 2f, out Vector3 p, avoidPaths: true)) continue;
                Quaternion y = Yaw();
                int segs = Random.Range(2, 4);
                for (int k = 0; k < segs; k++)
                {
                    Vector3 q = p + y * new Vector3(0f, 0f, k * 0.9f);
                    b.Add(blockMesh, q + Vector3.up * 0.01f, y * Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f),
                        new Vector3(0.1f, 0.03f, 1.1f), crack);
                }
            }
            Pebbles(r, b, Scale(180, r), 0.12f, 0.5f, C(0.16f, 0.14f, 0.14f), C(0.24f, 0.21f, 0.20f));
            for (int i = 0; i < Scale(60, r); i++)
            {
                if (!TryInside(r, 0.9f, 1f, out Vector3 p, avoidPaths: true)) continue;
                float s = Random.Range(0.08f, 0.16f);
                b.Add(pebbleMesh, Grounded(p) + Vector3.up * s * 0.3f, Quaternion.identity, Vector3.one * s, ember);
            }
            Shrubs(r, b, Scale(40, r), C(0.20f, 0.17f, 0.15f), C(0.28f, 0.24f, 0.20f));   // 타 버린 덤불
            RimRocks(r, rim, 1.2f, C(0.20f, 0.18f, 0.18f), C(0.27f, 0.24f, 0.23f), 3f);
            RimMounds(r, rim, 0.5f, C(0.44f, 0.42f, 0.40f), C(0.36f, 0.34f, 0.33f), 1.8f);   // 재 둔덕
            RimDeadTrees(r, rim, 0.5f, C(0.12f, 0.11f, 0.11f));
        }

        private void Canopy(RegionData r, SceneryBatcher b, SceneryBatcher rim)
        {
            // 2막에서 유일하게 살아 있는 리전 — 밀도와 채도를 가장 높게
            Leaves(r, b, Scale(600, r), C(0.36f, 0.56f, 0.22f), C(0.56f, 0.62f, 0.24f), C(0.70f, 0.64f, 0.26f));
            Ferns(r, b, Scale(200, r), C(0.26f, 0.56f, 0.22f), C(0.34f, 0.62f, 0.26f));
            Tufts(r, b, Scale(260, r), 0.32f, 0.55f, 0.9f, C(0.32f, 0.56f, 0.26f), C(0.40f, 0.62f, 0.30f));
            Mushrooms(r, b, Scale(50, r), C(0.96f, 0.62f, 0.20f), C(0.86f, 0.30f, 0.30f), C(0.94f, 0.92f, 0.84f));
            Flowers(r, b, Scale(70, r), C(0.98f, 0.60f, 0.24f), C(0.96f, 0.36f, 0.52f), C(0.98f, 0.94f, 0.50f));
            Clover(r, b, Scale(60, r), C(0.26f, 0.48f, 0.24f));
            RimTrees(r, rim, 1.2f, false, C(0.36f, 0.26f, 0.16f), C(0.22f, 0.48f, 0.22f), C(0.30f, 0.56f, 0.26f));
            RimBushes(r, rim, 1.1f, C(0.24f, 0.50f, 0.22f), C(0.32f, 0.58f, 0.26f));
        }

        private void Nameless(RegionData r, SceneryBatcher b, SceneryBatcher rim)
        {
            // 채워지지 않은 자리 — 소품을 일부러 적게. 대신 빛바랜 반딧불 같은 점이 떠 있다
            Color mote = C(0.78f, 0.74f, 0.92f);
            b.MarkGlow(mote, C(0.55f, 0.50f, 0.72f));
            Pebbles(r, b, Scale(80, r), 0.12f, 0.35f, C(0.60f, 0.59f, 0.62f), C(0.50f, 0.49f, 0.53f));
            Tufts(r, b, Scale(90, r), 0.28f, 0.45f, 0.8f, C(0.54f, 0.54f, 0.54f), C(0.60f, 0.59f, 0.60f));
            for (int i = 0; i < Scale(50, r); i++)
            {
                if (!TryInside(r, 0.9f, 1f, out Vector3 p, avoidPaths: false)) continue;
                b.Add(pebbleMesh, Grounded(p) + Vector3.up * Random.Range(0.6f, 2.2f), Quaternion.identity, Vector3.one * Random.Range(0.06f, 0.11f), mote);
            }
            RimMonoliths(r, rim, C(0.26f, 0.25f, 0.30f), C(0.34f, 0.33f, 0.38f));
        }

        // ======= 발밑 소품 =======

        /// <summary>
        /// 밀도 배율. 처음 값(초원 945포기/15,300㎡ ≈ 0.06/㎡)으로는 게임 카메라가 보는 15×10m에 풀포기가
        /// 9개뿐이라 화면이 여전히 맨바닥이었다. 호출부의 개수는 상대 비율로 두고 여기서 한 번에 올린다.
        /// </summary>
        private const float TuftDensity = 2f, FlowerDensity = 2f, LitterDensity = 1.5f;

        private void Tufts(RegionData r, SceneryBatcher b, int count, float minS, float maxS, float height, params Color[] colors)
        {
            count = Mathf.RoundToInt(count * TuftDensity);
            for (int i = 0; i < count; i++)
            {
                if (!TryInside(r, 0.93f, 0.5f, out Vector3 p, avoidPaths: true)) continue;
                float s = Random.Range(minS, maxS) * 1.5f;
                b.Add(tuftMesh, Grounded(p), Yaw(), new Vector3(s, s * 0.8f * height * Random.Range(0.8f, 1.25f), s), colors[i % colors.Length]);
            }
        }

        private void Ferns(RegionData r, SceneryBatcher b, int count, params Color[] colors)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TryInside(r, 0.92f, 0.8f, out Vector3 p, avoidPaths: true)) continue;
                float s = Random.Range(0.55f, 1.0f);
                b.Add(fernMesh, Grounded(p), Yaw(), new Vector3(s, s * 0.7f, s), colors[i % colors.Length]);
            }
        }

        private void Flowers(RegionData r, SceneryBatcher b, int count, params Color[] heads)
        {
            count = Mathf.RoundToInt(count * FlowerDensity);
            for (int i = 0; i < count; i++)
            {
                if (!TryInside(r, 0.92f, 0.4f, out Vector3 p, avoidPaths: true)) continue;
                Flower(b, p, heads[i % heads.Length], Random.Range(0.25f, 0.45f));
            }
        }

        private void Flower(SceneryBatcher b, Vector3 p, Color head, float h)
        {
            p = Grounded(p);   // 꽃밭 화단(Garden)도 이 경로라 한 번에 둔덕 위로 오른다
            b.Add(stalkMesh, p, Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f)),
                new Vector3(0.6f, h, 0.6f), C(0.24f, 0.42f, 0.18f));
            // 꽃송이는 고각 카메라에서 점 하나라도 읽혀야 한다 — 13cm는 안 보였다
            b.Add(bloomMesh, p + Vector3.up * h, Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), 0f), new Vector3(0.24f, 1f, 0.24f), head);
        }

        private void Pebbles(RegionData r, SceneryBatcher b, int count, float minS, float maxS, params Color[] colors)
        {
            count = Mathf.RoundToInt(count * LitterDensity);
            for (int i = 0; i < count; i++)
            {
                if (!TryInside(r, 0.93f, 0.4f, out Vector3 p, avoidPaths: false)) continue;
                float s = Random.Range(minS, maxS);
                b.Add(pebbleMesh, Grounded(p) + Vector3.up * s * 0.12f, Yaw(), new Vector3(s * 1.3f, s * 0.55f, s), colors[i % colors.Length]);
            }
        }

        private void Leaves(RegionData r, SceneryBatcher b, int count, params Color[] colors)
        {
            count = Mathf.RoundToInt(count * LitterDensity);
            for (int i = 0; i < count; i++)
            {
                if (!TryInside(r, 0.93f, 0.2f, out Vector3 p, avoidPaths: false)) continue;
                float s = Random.Range(0.14f, 0.26f);
                b.Add(leafMesh, p + Vector3.up * (0.006f + (i % 4) * 0.002f), Yaw(), new Vector3(s, 1f, s * 0.6f), colors[i % colors.Length]);
            }
        }

        private void Petals(RegionData r, SceneryBatcher b, int count, params Color[] colors) => Leaves(r, b, count, colors);

        private void Clover(RegionData r, SceneryBatcher b, int count, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TryInside(r, 0.9f, 0.8f, out Vector3 p, avoidPaths: true)) continue;
                float s = Random.Range(0.8f, 1.8f);
                b.Add(patchMesh, p + Vector3.up * 0.009f, Yaw(), new Vector3(s, 1f, s * 0.8f), color);
            }
        }

        private void Cracks(RegionData r, SceneryBatcher b, int count, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TryInside(r, 0.9f, 1f, out Vector3 p, avoidPaths: true)) continue;
                Quaternion y = Yaw();
                for (int k = 0; k < 3; k++)
                    b.Add(blockMesh, p + y * new Vector3(0f, 0.01f, k * 0.7f), y * Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f),
                        new Vector3(0.05f, 0.02f, 0.8f), color);
            }
        }

        private void Tiles(RegionData r, SceneryBatcher b, int count, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TryInside(r, 0.88f, 1f, out Vector3 p, avoidPaths: false)) continue;
                float s = Random.Range(0.6f, 1.2f);
                b.Add(blockMesh, p + Vector3.up * 0.01f, Yaw(), new Vector3(s, 0.04f, s * Random.Range(0.6f, 1f)), color);
            }
        }

        private void SnowPatches(RegionData r, SceneryBatcher b, int count, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TryInside(r, 0.9f, 2f, out Vector3 p, avoidPaths: true)) continue;
                float s = Random.Range(1.2f, 3f);
                b.Add(patchMesh, p + Vector3.up * 0.013f, Yaw(), new Vector3(s * 1.3f, 1f, s), color);
            }
        }

        private void Mushrooms(RegionData r, SceneryBatcher b, int count, params Color[] caps)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TryInside(r, 0.9f, 0.6f, out Vector3 center, avoidPaths: true)) continue;
                int cluster = Random.Range(1, 4);
                for (int k = 0; k < cluster; k++)
                {
                    Vector3 p = Grounded(center + Polar(Random.Range(0f, 360f), Random.Range(0f, 0.3f)));
                    float h = Random.Range(0.12f, 0.28f);
                    b.Add(stalkMesh, p, Yaw(), new Vector3(1.2f, h, 1.2f), C(0.92f, 0.88f, 0.78f));
                    b.Add(pebbleMesh, p + Vector3.up * h, Quaternion.identity, new Vector3(h * 0.9f, h * 0.45f, h * 0.9f), caps[i % caps.Length]);
                }
            }
        }

        private void Twigs(RegionData r, SceneryBatcher b, int count, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TryInside(r, 0.9f, 0.4f, out Vector3 p, avoidPaths: false)) continue;
                b.Add(blockMesh, Grounded(p) + Vector3.up * 0.03f, Quaternion.Euler(0f, Random.Range(0f, 180f), Random.Range(-6f, 6f)),
                    new Vector3(Random.Range(0.4f, 0.9f), 0.04f, 0.04f), color);
            }
        }

        private void Shrubs(RegionData r, SceneryBatcher b, int count, params Color[] colors)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TryInside(r, 0.9f, 0.8f, out Vector3 p, avoidPaths: true)) continue;
                p = Grounded(p);
                int twigs = Random.Range(4, 7);
                for (int k = 0; k < twigs; k++)
                {
                    float h = Random.Range(0.3f, 0.7f);
                    b.Add(stalkMesh, p,
                        Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(0f, 360f), Random.Range(-30f, 30f)),
                        new Vector3(0.8f, h, 0.8f), colors[k % colors.Length]);
                }
            }
        }

        private void Rubble(RegionData r, SceneryBatcher b, int count, params Color[] colors)
        {
            for (int i = 0; i < count; i++)
            {
                if (!TryInside(r, 0.9f, 0.6f, out Vector3 p, avoidPaths: true)) continue;
                float s = Random.Range(0.18f, 0.55f);
                b.Add(blockMesh, Grounded(p) + Vector3.up * s * 0.3f,
                    Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f)),
                    new Vector3(s * 1.3f, s * 0.7f, s), colors[i % colors.Length]);
            }
        }

        // ======= 울타리 밖 테두리 =======

        /// <summary>
        /// 울타리(R-1) 바깥 고리에서 자리를 뽑는다. 도로·통로는 피하고, 남쪽 반원에서는 <paramref name="maxHeightSouth"/>로
        /// 키를 제한한다 — 카메라가 플레이어 남쪽 위에 있어서 남쪽의 큰 것은 가장자리의 플레이어를 가린다.
        /// </summary>
        private bool TryRim(RegionData r, float minOut, float maxOut, float footprint, out Vector3 p, out float heightCap)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float a = Random.Range(0f, 360f);
                float dist = r.radius + Random.Range(minOut, maxOut);
                p = Floor(r.centerPosition + Polar(a, dist));
                if (Blocked(p, footprint + 1.5f)) continue;
                if (InsideOtherRegion(r, p, footprint)) continue;
                float south = -Mathf.Sin(a * Mathf.Deg2Rad);   // 1 = 정남
                heightCap = south > 0.25f ? 2.2f : float.MaxValue;
                return true;
            }
            p = Vector3.zero;
            heightCap = 0f;
            return false;
        }

        private void RimTrees(RegionData r, SceneryBatcher b, float density, bool conifer, Color trunk, Color leaf, Color leafLight)
        {
            int n = RimCount(r, 40f * density);
            for (int i = 0; i < n; i++)
            {
                if (!TryRim(r, 2.5f, 13f, 2.5f, out Vector3 p, out float cap)) continue;
                float h = Random.Range(4.5f, 8.5f);
                if (h > cap) continue;   // 남쪽엔 큰 나무를 두지 않는다
                if (conifer)
                {
                    b.Add(stemMesh, p + Vector3.up * h * 0.2f, Quaternion.identity, new Vector3(0.28f, h * 0.2f, 0.28f), trunk);
                    for (int k = 0; k < 3; k++)
                    {
                        float t = k / 3f;
                        float w = (1.9f - t * 1.1f) * h / 7f;
                        b.Add(coneMesh, p + Vector3.up * (h * (0.22f + t * 0.24f)), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                            new Vector3(w * 2f, h * 0.42f, w * 2f), k == 2 ? leafLight : leaf);
                    }
                }
                else
                {
                    b.Add(stemMesh, p + Vector3.up * h * 0.3f, Quaternion.Euler(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f)),
                        new Vector3(0.32f, h * 0.3f, 0.32f), trunk);
                    int puffs = Random.Range(3, 5);
                    for (int k = 0; k < puffs; k++)
                    {
                        float s = h * Random.Range(0.28f, 0.4f);
                        Vector3 o = new Vector3(Random.Range(-0.8f, 0.8f), h * Random.Range(0.62f, 0.85f), Random.Range(-0.8f, 0.8f));
                        b.Add(sphereMesh, p + o, Yaw(), new Vector3(s * 1.2f, s, s * 1.2f), k % 2 == 0 ? leaf : leafLight);
                    }
                }
            }
        }

        private void RimBushes(RegionData r, SceneryBatcher b, float density, Color a, Color c)
        {
            int n = RimCount(r, 60f * density);
            for (int i = 0; i < n; i++)
            {
                if (!TryRim(r, 1f, 7f, 1.2f, out Vector3 p, out _)) continue;
                int puffs = Random.Range(2, 4);
                for (int k = 0; k < puffs; k++)
                {
                    float s = Random.Range(1.0f, 2.1f);
                    b.Add(sphereMesh, p + new Vector3(Random.Range(-0.7f, 0.7f), s * 0.3f, Random.Range(-0.7f, 0.7f)), Yaw(),
                        new Vector3(s * 1.2f, s * 0.8f, s), k % 2 == 0 ? a : c);
                }
            }
        }

        private void RimHedges(RegionData r, SceneryBatcher b, Color hedge, Color[] blossoms)
        {
            int n = RimCount(r, 70f);
            for (int i = 0; i < n; i++)
            {
                if (!TryRim(r, 1.2f, 6f, 1.5f, out Vector3 p, out _)) continue;
                Vector3 outward = (p - r.centerPosition);
                outward.y = 0f;
                Quaternion along = Quaternion.LookRotation(Vector3.Cross(Vector3.up, outward.normalized), Vector3.up);
                float len = Random.Range(2.5f, 4.5f);
                b.Add(blockMesh, p + Vector3.up * 0.7f, along, new Vector3(1.1f, 1.4f, len), hedge);
                for (int k = 0; k < 7; k++)
                {
                    Vector3 o = along * new Vector3(Random.Range(-0.56f, 0.56f), Random.Range(0.4f, 1.42f), Random.Range(-len * 0.45f, len * 0.45f));
                    b.Add(pebbleMesh, p + o, Quaternion.identity, Vector3.one * Random.Range(0.18f, 0.28f), blossoms[(i + k) % blossoms.Length]);
                }
            }
        }

        private void RimReeds(RegionData r, SceneryBatcher b, Color a, Color c)
        {
            int n = RimCount(r, 90f);
            for (int i = 0; i < n; i++)
            {
                if (!TryRim(r, 0.8f, 6f, 0.8f, out Vector3 p, out _)) continue;
                for (int k = 0; k < 4; k++)
                {
                    float h = Random.Range(1.2f, 2.1f);
                    b.Add(bladeMesh, p + Polar(Random.Range(0f, 360f), Random.Range(0f, 0.5f)), Yaw(), new Vector3(0.6f, h, 0.6f), k % 2 == 0 ? a : c);
                }
            }
        }

        private void RimDeadTrees(RegionData r, SceneryBatcher b, float density, Color wood)
        {
            int n = RimCount(r, 30f * density);
            for (int i = 0; i < n; i++)
            {
                if (!TryRim(r, 2f, 11f, 1.5f, out Vector3 p, out float cap)) continue;
                float h = Random.Range(3f, 6f);
                if (h > cap) h = cap;
                b.Add(stemMesh, p + Vector3.up * h * 0.5f, Quaternion.Euler(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f)),
                    new Vector3(0.24f, h * 0.5f, 0.24f), wood);
                int branches = Random.Range(2, 4);
                for (int k = 0; k < branches; k++)
                {
                    float bl = Random.Range(0.8f, 1.6f);
                    Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Quaternion.Euler(Random.Range(35f, 60f), 0f, 0f);
                    b.Add(stemMesh, p + Vector3.up * h * Random.Range(0.55f, 0.85f) + rot * new Vector3(0f, bl * 0.5f, 0f), rot,
                        new Vector3(0.1f, bl * 0.5f, 0.1f), wood);
                }
            }
        }

        private void RimRocks(RegionData r, SceneryBatcher b, float density, Color a, Color c, float maxSize)
        {
            int n = RimCount(r, 45f * density);
            for (int i = 0; i < n; i++)
            {
                if (!TryRim(r, 1.5f, 11f, 2f, out Vector3 p, out float cap)) continue;
                float s = Random.Range(1.2f, maxSize);
                float h = Mathf.Min(s * Random.Range(0.7f, 1.4f), cap);
                b.Add(blockMesh, p + Vector3.up * h * 0.35f,
                    Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f)),
                    new Vector3(s * Random.Range(1f, 1.6f), h, s), i % 2 == 0 ? a : c);
            }
        }

        private void RimMounds(RegionData r, SceneryBatcher b, float density, Color a, Color c, float maxRise)
        {
            int n = RimCount(r, 40f * density);
            for (int i = 0; i < n; i++)
            {
                if (!TryRim(r, 3f, 14f, 4f, out Vector3 p, out float cap)) continue;
                float rise = Mathf.Min(Random.Range(maxRise * 0.45f, maxRise), cap);
                float w = Random.Range(6f, 13f);
                Vector3 outward = p - r.centerPosition;
                outward.y = 0f;
                Quaternion along = Quaternion.LookRotation(Vector3.Cross(Vector3.up, outward.normalized), Vector3.up);
                // PlaceMound와 같은 식 — 구의 위 40%만 드러낸다
                float bAxis = rise / 0.4f;
                b.Add(sphereMesh, new Vector3(p.x, FloorY + rise - bAxis, p.z), along * Quaternion.Euler(0f, Random.Range(-20f, 20f), 0f),
                    new Vector3(w * 0.55f / 0.8f, bAxis * 2f, w / 0.8f), i % 3 == 0 ? c : a);
            }
        }

        private void RimIce(RegionData r, SceneryBatcher b, Color ice)
        {
            int n = RimCount(r, 35f);
            for (int i = 0; i < n; i++)
            {
                if (!TryRim(r, 1.5f, 10f, 1.5f, out Vector3 p, out float cap)) continue;
                int spikes = Random.Range(2, 5);
                for (int k = 0; k < spikes; k++)
                {
                    float h = Mathf.Min(Random.Range(1.2f, 3.4f), cap);
                    b.Add(coneMesh, p + Polar(Random.Range(0f, 360f), Random.Range(0f, 0.8f)),
                        Quaternion.Euler(Random.Range(-14f, 14f), Random.Range(0f, 360f), Random.Range(-14f, 14f)),
                        new Vector3(h * 0.4f, h, h * 0.4f), ice);
                }
            }
        }

        private void RimWalls(RegionData r, SceneryBatcher b, Color a, Color c)
        {
            int n = RimCount(r, 30f);
            for (int i = 0; i < n; i++)
            {
                if (!TryRim(r, 1.5f, 9f, 2.5f, out Vector3 p, out float cap)) continue;
                Vector3 outward = p - r.centerPosition;
                outward.y = 0f;
                Quaternion along = Quaternion.LookRotation(Vector3.Cross(Vector3.up, outward.normalized), Vector3.up);
                float h = Mathf.Min(Random.Range(1.2f, 3.4f), cap);
                float len = Random.Range(2.5f, 5f);
                b.Add(blockMesh, p + Vector3.up * h * 0.5f, along * Quaternion.Euler(0f, 0f, Random.Range(-4f, 4f)),
                    new Vector3(0.6f, h, len), i % 2 == 0 ? a : c);
                if (i % 3 == 0)   // 쓰러진 기둥 토막
                    b.Add(stemMesh, p + outward.normalized * 1.4f + Vector3.up * 0.35f, along * Quaternion.Euler(90f, 0f, 0f),
                        new Vector3(0.7f, 1.1f, 0.7f), c);
            }
        }

        private void RimMonoliths(RegionData r, SceneryBatcher b, Color a, Color c)
        {
            int n = RimCount(r, 16f);
            for (int i = 0; i < n; i++)
            {
                if (!TryRim(r, 2f, 12f, 1.5f, out Vector3 p, out float cap)) continue;
                float h = Mathf.Min(Random.Range(1.8f, 4.2f), cap);
                b.Add(blockMesh, p + Vector3.up * h * 0.45f, Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(0f, 360f), Random.Range(-6f, 6f)),
                    new Vector3(Random.Range(0.8f, 1.4f), h, 0.35f), i % 2 == 0 ? a : c);
            }
        }

        // ======= 자리 판정 =======

        /// <summary>리전 원 안(반경 비율 maxFrac)에서 회피 규칙을 통과하는 자리를 뽑는다. 6회 실패하면 포기.</summary>
        private bool TryInside(RegionData r, float maxFrac, float footprint, out Vector3 p, bool avoidPaths)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float a = Random.Range(0f, 360f);
                float d = r.radius * maxFrac * Mathf.Sqrt(Random.value);
                p = Floor(r.centerPosition + Polar(a, d));
                if (InKeepOut(p, footprint)) continue;
                if (avoidPaths && OnLane(p, footprint)) continue;
                return true;
            }
            p = Vector3.zero;
            return false;
        }

        private bool Blocked(Vector3 p, float footprint) => InKeepOut(p, footprint) || OnLane(p, footprint);

        private bool InKeepOut(Vector3 p, float footprint)
        {
            for (int n = 0; n < nearKeep.Count; n++)
            {
                Vector4 k = keepOut[nearKeep[n]];
                float dx = p.x - k.x, dz = p.z - k.z;
                float rr = k.w + footprint;
                if (dx * dx + dz * dz < rr * rr) return true;
            }
            // 물길·부두·호수는 월드에 몇 개뿐이라 근처 추리기 없이 전부 본다(원반은 외접원으로 먼저 거른다)
            for (int n = 0; n < strips.Count; n++)
            {
                KeepStrip s = strips[n];
                float dx = p.x - s.center.x, dz = p.z - s.center.z;
                float along = dx * s.axis.x + dz * s.axis.z;
                float side = dx * s.axis.z - dz * s.axis.x;
                if (Mathf.Abs(along) < s.halfLength + footprint && Mathf.Abs(side) < s.halfWidth + footprint) return true;
            }
            for (int n = 0; n < discKeepOut.Count; n++)
                if (discKeepOut[n].disc.Contains(p, discKeepOut[n].fraction, footprint)) return true;
            return false;
        }

        private bool OnLane(Vector3 p, float footprint)
        {
            for (int n = 0; n < nearLanes.Count; n++)
            {
                int i = nearLanes[n];
                if (WorldRouteLayout.DistanceToSegment(p, laneA[i], laneB[i]) < laneHalf[i] + footprint * 0.5f) return true;
            }
            return false;
        }

        /// <summary>리전 원 + 테두리 폭(20m) 안에 걸치는 도로·회피원만 추린다.</summary>
        private void SelectNearby(RegionData r)
        {
            nearLanes.Clear();
            nearKeep.Clear();
            float reach = r.radius + 20f;
            for (int i = 0; i < laneA.Count; i++)
                if (WorldRouteLayout.DistanceToSegment(r.centerPosition, laneA[i], laneB[i]) < reach + laneHalf[i]) nearLanes.Add(i);
            for (int i = 0; i < keepOut.Count; i++)
            {
                Vector4 k = keepOut[i];
                float dx = r.centerPosition.x - k.x, dz = r.centerPosition.z - k.z;
                float rr = reach + k.w;
                if (dx * dx + dz * dz < rr * rr) nearKeep.Add(i);
            }
        }

        /// <summary>다른 리전 원 안으로 파고드는 테두리 소품은 버린다(리전끼리 가까운 곳).</summary>
        private bool InsideOtherRegion(RegionData self, Vector3 p, float footprint)
        {
            foreach (RegionData o in regions)
            {
                if (o == null || o == self) continue;
                float dx = p.x - o.centerPosition.x, dz = p.z - o.centerPosition.z;
                float rr = o.radius + footprint + 1f;
                if (dx * dx + dz * dz < rr * rr) return true;
            }
            return false;
        }

        private void CollectAvoidance()
        {
            laneA.Clear(); laneB.Clear(); laneHalf.Clear(); keepOut.Clear(); strips.Clear(); discKeepOut.Clear();

            foreach (WorldRouteEdge edge in WorldRouteLayout.FieldConnections)
            {
                Vector3[] route = WorldRouteLayout.BuildRoute(regions, edge.FromRegionId, edge.ToRegionId);
                for (int i = 1; i < route.Length; i++) Lane(route[i - 1], route[i], WorldRouteLayout.RoadWidth * 0.5f + 0.8f);
            }

            RegionData meadow = WorldRouteLayout.Find(regions, "meadow");
            if (meadow != null)
            {
                Vector3 main = VillageBuilder.GetMainVillageCenter(meadow.centerPosition, meadow.radius);
                keepOut.Add(new Vector4(main.x, 0f, main.z, VillageBuilder.MainVillageFootprintRadius + 2f));
                Lane(meadow.centerPosition, PlayerStartPlacement.ResolveMainVillageEntrance(regions).Position,
                    WorldRouteLayout.RoadWidth * 0.5f + 0.8f);
            }

            if (terrain != null)
                foreach (RegionTerrainBuilder.PathSegment s in terrain.InternalPaths)
                    Lane(s.from, s.to, s.width * 0.5f + 0.5f);

            // 전초기지(VillageBuilder가 BuildSystems에서 짓는다 — 그래서 Start에서 모은다)
            GameObject village = GameObject.Find("Village");
            if (village != null)
            {
                foreach (Transform child in village.transform)
                    if (child.name.StartsWith("Outpost_"))
                        keepOut.Add(new Vector4(child.position.x, 0f, child.position.z, 9.5f));
            }

            // 서브에리어 입구 자리 — 입구 구조물과 겹치지 않게
            foreach (RegionData r in regions)
            {
                if (r?.subAreas == null) continue;
                foreach (SubAreaData sub in r.subAreas)
                    if (sub != null) keepOut.Add(new Vector4(sub.centerPosition.x, 0f, sub.centerPosition.z, 3.5f));
            }

            // 수문장 제단(PlaySceneBootstrap.CreateGuardians — 지름 4m, 윗면 0.3m) — 안 피하면 풀포기(최고 ~0.9m)가
            // 판을 뚫고 솟는다. 자리는 RegionManager.GetGuardianPosition이 단일 출처라 사본을 두지 않고 지어진 판을 읽는다.
            // 격파 뒤에도 판은 남으므로 늘 피한다. 기둥(±3m)은 밑동에 풀이 나도 자연스러워 판만 덮는다.
            foreach (RegionData r in regions)
            {
                if (r == null) continue;
                GameObject platform = GameObject.Find($"Guardian_{r.regionId}_Platform");
                if (platform == null) continue;
                Vector3 c = platform.transform.position;
                keepOut.Add(new Vector4(c.x, 0f, c.z, platform.transform.lossyScale.x * 0.5f + 0.3f));
            }

            // 연못 강(WorldTerrainBuilder.BuildRiver) — 안 피하면 반투명 수면 위로 풀·꽃이 솟는다. 강 자리는 연못 입구 방향에서
            // 파생되므로 좌표를 여기 다시 적지 않고 지어진 수면을 읽는다. 내장 Plane은 10×10이라 반폭·반길이는
            // 5 × 스케일이고, 둑 돌(River_Bank — 중심선에서 2.8m, 지름 0.7)까지 폭을 0.7m 더 막는다.
            for (int i = 0; ; i++)
            {
                GameObject water = GameObject.Find($"River_Water_{i}");
                if (water == null) break;
                Transform t = water.transform;
                Vector3 axis = new Vector3(t.forward.x, 0f, t.forward.z);
                if (axis.sqrMagnitude < 0.0001f) continue;
                strips.Add(new KeepStrip
                {
                    center = t.position,
                    axis = axis.normalized,
                    halfLength = 5f * t.lossyScale.z,
                    halfWidth = 5f * t.lossyScale.x + 0.7f,
                });
            }
        }

        private void Lane(Vector3 a, Vector3 b, float half)
        {
            laneA.Add(a);
            laneB.Add(b);
            laneHalf.Add(half);
        }

        // ======= 메시 =======

        /// <summary>patchMesh(<see cref="IrregularDisc"/>)의 반지름 — 배율 s의 원반은 평균 반경 PatchRadius·s다.</summary>
        internal const float PatchRadius = 0.5f;

        /// <summary>얼룩·잎·꽃송이 원반의 분할 수.</summary>
        internal const int PatchSegments = 14;

        /// <summary>
        /// 호수 원반의 분할 수. 얼룩(14분할)을 지름 40m로 늘리면 테두리 한 변이 9m라 각진 다각형으로 보였다 —
        /// 요철식은 같고 분할만 두 배다(요철 범위도 14분할의 0.79~1.21에서 연속식의 0.76~1.24에 가까워진다).
        /// </summary>
        internal const int LakeSegments = 28;

        /// <summary>풀·꽃·조약돌이 호수에서 비키는 범위 — 물 윤곽의 이 배율(진흙 띠 110%의 안쪽 절반)까지.</summary>
        private const float LakeClearFraction = 1.05f;

        /// <summary>
        /// 원반 테두리의 요철(평균 반경 대비 배율). 메시(<see cref="IrregularDisc"/>)와 윤곽 계산(<see cref="PatchEdgeRadius"/>)이
        /// 같은 식을 써야 소품이 실제 물가를 따라 선다 — 옛날엔 물 반경을 원으로 어림해 갈대가 물 한가운데·풀밭에 흩어졌다.
        /// </summary>
        internal static float PatchWobble(float angle) =>
            1f + 0.16f * Mathf.Sin(angle * 3f + 0.7f) + 0.09f * Mathf.Sin(angle * 5f + 2.1f);

        /// <summary>
        /// 배율 1의 원반(반지름 <see cref="PatchRadius"/>)에서 로컬 각 <paramref name="angle"/>(라디안, x축에서 z축으로) 방향으로
        /// 테두리까지의 거리 — 꼭짓점 사이는 메시와 똑같이 직선(현)이다.
        /// </summary>
        internal static float PatchEdgeRadius(float angle, int segments)
        {
            float step = Mathf.PI * 2f / segments;
            float a = Mathf.Repeat(angle, Mathf.PI * 2f);
            int i = Mathf.Min(segments - 1, Mathf.FloorToInt(a / step));
            float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
            Vector2 p0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * (PatchRadius * PatchWobble(a0));
            Vector2 p1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * (PatchRadius * PatchWobble(a1));
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 e = p1 - p0;
            float cross = dir.x * e.y - dir.y * e.x;
            if (Mathf.Abs(cross) < 1e-6f) return p0.magnitude;
            return (p0.x * e.y - p0.y * e.x) / cross;   // 반직선 t·dir이 현 p0→p1과 만나는 t
        }

        /// <summary>
        /// 불규칙 원반 한 장의 월드 배치(배처에 넘기는 TRS와 같은 값). 소품 배치·회피·테스트가 메시와 같은 윤곽을 본다.
        /// 원반 중심에서 보면 테두리가 한 번만 나오는 별 모양이라, 반직선 하나로 안팎을 가를 수 있다.
        /// </summary>
        internal readonly struct DiscPlacement
        {
            public readonly Vector3 center;
            public readonly float scaleX, scaleZ, yaw;
            public readonly int segments;

            public DiscPlacement(Vector3 center, float scaleX, float scaleZ, float yaw, int segments)
            {
                this.center = center;
                this.scaleX = scaleX;
                this.scaleZ = scaleZ;
                this.yaw = yaw;
                this.segments = segments;
            }

            public Quaternion Rotation => Quaternion.Euler(0f, yaw, 0f);
            public Vector3 Scale => new Vector3(scaleX, 1f, scaleZ);

            /// <summary>로컬 각 방향 테두리의 <paramref name="fraction"/>배 자리(월드, y는 중심과 같다).</summary>
            public Vector3 EdgePoint(float localAngle, float fraction)
            {
                float r = PatchEdgeRadius(localAngle, segments) * fraction;
                return center + Rotation * new Vector3(Mathf.Cos(localAngle) * r * scaleX, 0f, Mathf.Sin(localAngle) * r * scaleZ);
            }

            /// <summary>
            /// p의 로컬 각 — <see cref="EdgePoint"/>에 넘기면 중심에서 p로 가는 <b>같은 반직선</b> 위의 테두리가 나온다
            /// (배율로 늘어난 타원이라 월드 방위각을 그대로 넘기면 반직선이 어긋난다).
            /// </summary>
            public float LocalAngleOf(Vector3 p)
            {
                Vector3 local = Quaternion.Inverse(Rotation) * new Vector3(p.x - center.x, 0f, p.z - center.z);
                return Mathf.Atan2(local.z / scaleZ, local.x / scaleX);
            }

            /// <summary>
            /// p가 윤곽을 <paramref name="fraction"/>배 한 도형 안인가 — <paramref name="grow"/>m만큼 바깥까지 안으로 친다
            /// (중심에서 p를 잇는 반직선을 따라 잰 거리라 테두리 법선 거리보다 약간 후하다).
            /// </summary>
            public bool Contains(Vector3 p, float fraction, float grow)
            {
                float dx = p.x - center.x, dz = p.z - center.z;
                float dist2 = dx * dx + dz * dz;
                float reach = PatchRadius * 1.25f * Mathf.Max(scaleX, scaleZ) * fraction + grow;   // 요철 연속식 최대 1.2498
                if (dist2 > reach * reach) return false;
                Vector3 local = Quaternion.Inverse(Rotation) * new Vector3(dx, 0f, dz);
                float u = local.x / scaleX, v = local.z / scaleZ;
                float rho = Mathf.Sqrt(u * u + v * v);
                if (rho < 1e-5f) return true;
                float edge = PatchEdgeRadius(Mathf.Atan2(v, u), segments) * fraction;
                float dist = Mathf.Sqrt(dist2);
                return dist < dist * (edge / rho) + grow;
            }
        }

        internal struct PondLakeLayout
        {
            public DiscPlacement mud, water, deep;
        }

        /// <summary>
        /// 연못 호수의 윤곽 — 진흙·여울·깊은 물 세 장. 리전 중심·반경의 배율로만 적어 월드 배율(WorldScale)이 바뀌어도
        /// 부두·NPC·입구(전부 반경 배율로 놓인다)와의 관계가 그대로다.
        ///
        /// <b>왜 이 자리·이 모양인가</b>(반경 67.5m 리전, 2026-09-29 검산). 옛 호수는 평균 물 반경 14.6m 원이었고
        /// 부두(<see cref="RegionTerrainBuilder.PondDockFootprint"/>, 옛 호수 중심에서 18~23m)는 물 밖 풀밭에 섰다.
        /// 그런데 호수를 제자리에서 고르게 키울 수는 없다 — 옛 중심에서 20~24m 고리에 NPC가 둘러서 있다
        /// (북동 64° 전초기지 모닥불 21.3m·주민 20.8m, 동 18° 라온 22.2m, 북서 140° 갈대 밀림 입구 20.4m,
        /// 서남서 202° 검은 옷 끈 22.4m·잡기 아이 23.8m). 비어 있는 쪽은 남동(연못 깊은 곳 입구 방향)뿐이라
        /// 중심을 남동으로 (0.11R, −0.08R) 옮기고, 평균 물 반경 0.29R(19.6m)·가로세로 1.1배 타원을 40° 돌려 앉혔다.
        /// 물 면적 655 → 1,203㎡(1.84배), 진흙까지 830 → 1,456㎡.
        ///
        /// 결과: 부두 중심선의 북쪽 3.1m(길이 6m 중)가 여울 위, 나머지가 진흙이고 남쪽 끝 너머 1m까지 진흙이다.
        /// 진흙 가장자리까지의 여유(발자리 반경을 뺀 값) — 모닥불 돌 3.8m · 의자 3.7m · 주민 3.7m · 마을 이야기 주민 5.9m ·
        /// 라온 4.3m · 검은 옷 3.2m · 잡기 아이 4.5m · 갈대 밀림 아치 7.0m · 버드나무 8.5m · 전초기지 물가 진흙과 6.4m.
        /// 연못 깊은 곳 입구(돌 고리 2.45m)는 물가에서 10.7m 안쪽이다. 필드 길은 리전 중심에서 시작하므로 여전히 물을
        /// 건너 나온다(물 위 11.3 → 13m). 수문장 제단 33m·강 32m·울타리 29m 밖.
        /// 진흙·여울은 요를 맞춰 진흙 띠가 고른 폭(물 반경의 10%)이고, 깊은 물은 요를 225° 돌려(요철이 다르게) 0.7배로
        /// 두어 어느 방향에서도 여울 안쪽 2m 이상에 머문다. 위 숫자는 <c>FieldThemeTests.PondLake_*</c>가 고정한다.
        /// </summary>
        internal static PondLakeLayout PondLake(Vector3 regionCenter, float regionRadius)
        {
            var center = new Vector3(regionCenter.x + regionRadius * 0.11f, FloorY, regionCenter.z - regionRadius * 0.08f);
            float s = regionRadius * 0.29f / PatchRadius;   // 평균 물 반경 0.29R
            const float aspect = 1.1f, yaw = 40f;
            return new PondLakeLayout
            {
                water = new DiscPlacement(center, s * aspect, s / aspect, yaw, LakeSegments),
                mud = new DiscPlacement(center, s * aspect * 1.1f, s / aspect * 1.1f, yaw, LakeSegments),
                deep = new DiscPlacement(center, s * aspect * 0.7f, s / aspect * 0.7f, yaw + 225f, LakeSegments),
            };
        }

        /// <summary>
        /// 연못 물가 소품의 자리 — 호수 물(여울 윤곽)에 몸이 닿으면 <b>같은 방위</b>의 진흙 띠 바깥 가장자리로 내민다.
        /// 물에 안 닿으면(진흙 위·풀밭) 그대로 돌려준다. y는 건드리지 않는다. 난수를 쓰지 않는다.
        ///
        /// 쓰는 곳: 부트스트랩 물가 바위(<c>PlaySceneBootstrap.AddPondScenery</c>의 <c>Pond_ShoreRock_</c>). 옛 호수 중심에서
        /// 0.35R 고리에 60° 간격으로 서는데, 호수가 남동으로 옮겨 커지자(<see cref="PondLake"/>) 동쪽(0°)·남동쪽(300°) 두 개가
        /// 난수에 따라 물 한가운데에 섰다(물가에서 최대 약 12m 안쪽). 호출부는 이 결과만 바꾸고 난수는 그대로 뽑는다.
        ///
        /// 진흙 띠는 물 윤곽의 110%라 같은 반직선에서 물 가장자리보다 물 반경의 10%(가장 좁은 쪽 약 1.5m) 바깥이다 —
        /// 바위 반경(최대 0.54m)을 넉넉히 넘는다. 진흙 가장자리는 연못 NPC·모닥불·입구가 2m 넘게 비켜 있는 선이라
        /// (<c>FieldThemeTests.PondLake_*</c>) 옮긴 바위가 그들 발자리에 들지 않는다.
        /// </summary>
        internal static Vector3 PondShoreSpot(Vector3 regionCenter, float regionRadius, Vector3 p, float footprint)
        {
            PondLakeLayout lake = PondLake(regionCenter, regionRadius);
            if (!lake.water.Contains(p, 1f, footprint)) return p;
            Vector3 shore = lake.mud.EdgePoint(lake.mud.LocalAngleOf(p), 1f);
            return new Vector3(shore.x, p.y, shore.z);
        }

        // ======= 물 — 공개 판정 =======
        //
        // 필드에서 물로 칠해진 곳을 한 곳에서 묻는다: 연못 호수, 나루터 오두막 밑 물가(Pond의 원반 두 장), 습지 웅덩이(Swamp),
        // 그리고 다른 빌더가 깐 물 — 연못 강(WorldTerrainBuilder.BuildRiver), 부트스트랩의 원기둥 물(Pond_Water·Meadow_Puddle·
        // Swamp_Puddle_*), 수렁 웅덩이(RegionTerrainBuilder, Scenery_SwampPool_*). 곤충 스폰(InsectSpawner)과 필드 아이템
        // (CaptureItemSpawner)이 이걸로 물 위를 피한다.
        //
        // 습지 웅덩이 자리는 리전 난수(RegionSeed)로 정해진다. 밖에서 같은 난수를 재현하면 앞선 호출이 하나만 늘어도 조용히
        // 다른 자리를 가리키므로, <b>빌드가 실제로 놓은 원반을 그 자리에서 기록</b>한다(RecordWaterPatch). 다른 빌더의 물은
        // 지어진 오브젝트를 읽는다 — 강 회피(CollectAvoidance)가 이미 그렇게 한다.
        //
        // <b>기록 전</b>(이 빌더의 Start 전, 또는 장식 빌더가 없는 씬)에는 호수만 판정한다. 호수 윤곽은 리전 데이터의 순수 함수
        // (<see cref="PondLake"/>)라 빌드 없이도 같은 답이 나온다. 나머지는 놓이기 전엔 없으므로 물로 치지 않는다 — 대신
        // 곤충 초기 채우기가 기록을 기다린다(InsectSpawner.Start). 기록 뒤에는 기록만 본다(호수도 Pond가 적는다).
        // 월드는 씬마다 새로 지어지므로 기록은 이 빌더의 Awake·빌드 시작·파괴 때 비운다(FieldGround와 같은 수명).

        private const string PondRegionId = "pond";

        private static readonly List<DiscPlacement> waterDiscs = new List<DiscPlacement>();
        private static readonly List<Vector4> waterCircles = new List<Vector4>();   // xz + 반경(w)
        private static readonly List<KeepStrip> waterStrips = new List<KeepStrip>();
        private static bool waterRecorded;

        // 기록 전 판정용 호수 — 리전 데이터가 씬 안에서 안 바뀌므로 한 번만 구한다
        private static bool fallbackLakeResolved;
        private static bool hasFallbackLake;
        private static DiscPlacement fallbackLake;

        /// <summary>빌드가 물을 다 기록했는가. 기록 전에는 <see cref="IsOnWater"/>가 호수만 본다.</summary>
        internal static bool WaterRecorded => waterRecorded;

        /// <summary>기록된 물 도형 수(테스트·진단용).</summary>
        internal static int WaterShapeCount => waterDiscs.Count + waterCircles.Count + waterStrips.Count;

        /// <summary>기록을 비우고 "기록 전"으로 되돌린다.</summary>
        internal static void ClearWater()
        {
            waterDiscs.Clear();
            waterCircles.Clear();
            waterStrips.Clear();
            waterRecorded = false;
        }

        /// <summary>불규칙 원반 물 한 장(호수·물가·웅덩이).</summary>
        internal static void AddWaterDisc(DiscPlacement disc)
        {
            if (disc.scaleX <= 0f || disc.scaleZ <= 0f) return;
            waterDiscs.Add(disc);
        }

        /// <summary>원형 물(내장 원기둥을 납작하게 깐 것) — 중심과 반경(m).</summary>
        internal static void AddWaterCircle(Vector3 center, float radius)
        {
            if (radius <= 0f) return;
            waterCircles.Add(new Vector4(center.x, 0f, center.z, radius));
        }

        /// <summary>방향 있는 사각형 물(강) — <paramref name="axis"/>는 길이 방향 XZ 단위 벡터.</summary>
        internal static void AddWaterStrip(Vector3 center, Vector3 axis, float halfLength, float halfWidth)
        {
            axis.y = 0f;
            if (axis.sqrMagnitude < 1e-6f || halfLength <= 0f || halfWidth <= 0f) return;
            waterStrips.Add(new KeepStrip { center = center, axis = axis.normalized, halfLength = halfLength, halfWidth = halfWidth });
        }

        /// <summary>기록을 끝낸 것으로 둔다 — 빌드는 스스로 부른다. 테스트가 기록 뒤 동작을 볼 때 쓴다.</summary>
        internal static void MarkWaterRecorded() => waterRecorded = true;

        /// <summary>
        /// (x, z)가 물 위인가 — 물가에서 <paramref name="margin"/>(m) 바깥까지 물로 친다. y는 보지 않는다.
        /// 기록 전에는 호수만 판정한다(이 절 머리 주석).
        /// </summary>
        internal static bool IsOnWater(Vector3 p, float margin) => TryFindWater(p, margin, out _);

        /// <summary>
        /// 물 밖(여유 포함)까지 한 방향으로 민다. 방향은 처음 p를 덮은 물 도형에서 멀어지는 쪽 — 원반·원은 중심에서 p로 가는
        /// 반직선(원반은 중심에서 보면 테두리가 한 번만 나오는 별 모양이라 곧게 나가면 물가를 한 번만 넘는다), 강은 가까운 둑 쪽
        /// 수직이다. 도중에 다른 물을 만나도 <b>방향을 바꾸지 않는다</b> — 겹친 두 웅덩이 사이에서 번갈아 밀리면 제자리를 돈다.
        /// 물이 아니면 그대로 돌려준다. y는 건드리지 않는다.
        /// </summary>
        internal static Vector3 PushOutOfWater(Vector3 p, float margin, float step, int maxSteps)
        {
            if (!TryFindWater(p, margin, out Vector3 away)) return p;
            for (int i = 0; i < maxSteps && TryFindWater(p, margin, out _); i++)
                p += away * step;
            return p;
        }

        /// <summary>p를 덮는 첫 물 도형과 거기서 멀어지는 XZ 단위 방향.</summary>
        private static bool TryFindWater(Vector3 p, float margin, out Vector3 away)
        {
            if (!waterRecorded)
            {
                if (TryFallbackLake(out DiscPlacement lake) && lake.Contains(p, 1f, margin))
                {
                    away = AwayFrom(p, lake.center);
                    return true;
                }
                away = Vector3.zero;
                return false;
            }

            for (int i = 0; i < waterDiscs.Count; i++)
            {
                if (!waterDiscs[i].Contains(p, 1f, margin)) continue;
                away = AwayFrom(p, waterDiscs[i].center);
                return true;
            }
            for (int i = 0; i < waterCircles.Count; i++)
            {
                Vector4 c = waterCircles[i];
                float dx = p.x - c.x, dz = p.z - c.z, rr = c.w + margin;
                if (dx * dx + dz * dz >= rr * rr) continue;
                away = AwayFrom(p, new Vector3(c.x, 0f, c.z));
                return true;
            }
            for (int i = 0; i < waterStrips.Count; i++)
            {
                KeepStrip s = waterStrips[i];
                float dx = p.x - s.center.x, dz = p.z - s.center.z;
                float along = dx * s.axis.x + dz * s.axis.z;
                float side = dx * s.axis.z - dz * s.axis.x;   // InKeepOut과 같은 부호 — 옆 방향 (axis.z, 0, -axis.x)
                if (Mathf.Abs(along) >= s.halfLength + margin || Mathf.Abs(side) >= s.halfWidth + margin) continue;
                away = new Vector3(s.axis.z, 0f, -s.axis.x) * (side < 0f ? -1f : 1f);
                return true;
            }
            away = Vector3.zero;
            return false;
        }

        private static Vector3 AwayFrom(Vector3 p, Vector3 center)
        {
            var d = new Vector3(p.x - center.x, 0f, p.z - center.z);
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        }

        private static bool TryFallbackLake(out DiscPlacement lake)
        {
            if (!fallbackLakeResolved)
            {
                fallbackLakeResolved = true;
                foreach (RegionData r in RegionDefinitions.CreateAll())
                {
                    if (r == null || r.regionId != PondRegionId) continue;
                    fallbackLake = PondLake(r.centerPosition, r.radius).water;
                    hasFallbackLake = true;
                    break;
                }
            }
            lake = fallbackLake;
            return hasFallbackLake;
        }

        /// <summary>
        /// 패치 원반 물 한 장을 적는다 — 인자는 그 원반을 그린 <c>b.Add(patchMesh, center + 높이, rotation, scale, …)</c>와 같다
        /// (배처의 TRS와 <see cref="DiscPlacement"/>는 같은 뜻이다: 요 = rotation의 y, 배율 = scale의 x·z).
        /// </summary>
        private static void RecordWaterPatch(Vector3 center, Quaternion rotation, Vector3 scale)
        {
            AddWaterDisc(new DiscPlacement(center, scale.x, scale.z, rotation.eulerAngles.y, PatchSegments));
        }

        /// <summary>
        /// 다른 빌더가 깐 물을 지어진 오브젝트에서 읽는다(좌표를 여기 다시 적지 않는다). 강은 내장 Plane(10×10)이라 반폭·반길이가
        /// 5 × 스케일(<see cref="CollectAvoidance"/>의 강과 같은 식, 둑 돌 여유는 뺀다), 원기둥은 지름 1이라 반경이 스케일의 절반이다.
        /// 번호 붙은 것은 0부터 빈 번호가 나올 때까지 — 짓는 쪽이 전부 0부터 빠짐없이 매긴다.
        /// </summary>
        private static void RecordSceneWater()
        {
            for (int i = 0; ; i++)
            {
                GameObject water = GameObject.Find($"River_Water_{i}");
                if (water == null) break;
                Transform t = water.transform;
                AddWaterStrip(t.position, new Vector3(t.forward.x, 0f, t.forward.z), 5f * t.lossyScale.z, 5f * t.lossyScale.x);
            }
            RecordCylinderWater(GameObject.Find("Pond_Water"));
            RecordCylinderWater(GameObject.Find("Meadow_Puddle"));
            RecordCylinderSeries("Swamp_Puddle_");
            RecordCylinderSeries("Scenery_SwampPool_");
        }

        private static void RecordCylinderSeries(string prefix)
        {
            for (int i = 0; ; i++)
            {
                GameObject g = GameObject.Find(prefix + i);
                if (g == null) break;
                RecordCylinderWater(g);
            }
        }

        private static void RecordCylinderWater(GameObject g)
        {
            if (g == null) return;
            Vector3 s = g.transform.lossyScale;
            AddWaterCircle(g.transform.position, 0.5f * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z)));
        }

        private void CreateMeshes()
        {
            // 잎 폭은 고각 카메라(약 11m)에서 한 픽셀 넘게 잡히도록 — 처음의 9cm×크기(3~5cm)는 안 보였다
            tuftMesh = Own(BladeCluster("DressTuft", 5, 0.2f, 1f, 30f));
            fernMesh = Own(BladeCluster("DressFern", 6, 0.24f, 1f, 55f));
            bladeMesh = Own(BladeCluster("DressReed", 3, 0.05f, 1f, 8f));
            stalkMesh = Own(BladeCluster("DressStalk", 1, 0.05f, 1f, 0f));   // 꽃·버섯 줄기 — 캡슐(수십 정점) 대신 잎 한 장(3정점)
            bloomMesh = Own(IrregularDisc("DressBloom", 6, 0.5f));           // 꽃송이 — 고각 카메라엔 위에서 본 원반이 곧 꽃이다
            patchMesh = Own(IrregularDisc("DressPatch", PatchSegments, PatchRadius));
            lakeMesh = Own(IrregularDisc("DressLake", LakeSegments, PatchRadius));
            leafMesh = Own(IrregularDisc("DressLeaf", 6, 0.5f));
            coneMesh = Own(Cone("DressCone", 6));
            // 정점 예산: 합친 장식 전체가 한 번에 메모리에 올라간다. 구 하나 54 → 40, 조약돌 28 → 18정점.
            sphereMesh = ProcMeshLibrary.LowSphere(0.5f, 0.5f, 0.5f, 4, 7);
            pebbleMesh = ProcMeshLibrary.LowSphere(0.5f, 0.5f, 0.5f, 2, 5);
            blockMesh = ProcMeshLibrary.RoundedBox(Vector3.one, 0.12f, 1);
            stemMesh = Own(Prism("DressTrunk", 6));   // 줄기·기둥 — 내장 Capsule처럼 높이 2·중심 원점, 옆면만(14정점)
        }

        private Mesh Own(Mesh m)
        {
            ownedMeshes.Add(m);
            return m;
        }

        /// <summary>
        /// 풀잎 다발 — 가는 삼각형 잎을 방사형으로 기울여 세운다(양면). 법선을 전부 위로 둬서
        /// 잎이 바닥과 같은 밝기로 비친다(얇은 잎을 면 법선으로 비추면 반은 검게 죽는다).
        /// </summary>
        private static Mesh BladeCluster(string name, int blades, float width, float height, float lean)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < blades; i++)
            {
                float yaw = i * 360f / blades + (i % 2) * 17f;
                Quaternion q = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(lean * (0.6f + 0.4f * ((i * 7) % 5) / 4f), 0f, 0f);
                float h = height * (0.7f + 0.3f * ((i * 3) % 4) / 3f);
                Vector3 l = q * new Vector3(-width, 0f, 0f);
                Vector3 r = q * new Vector3(width, 0f, 0f);
                Vector3 tip = q * new Vector3(0f, h, 0f);
                int b = v.Count;
                v.Add(l); v.Add(r); v.Add(tip);
                t.Add(b); t.Add(b + 2); t.Add(b + 1);   // 앞
                t.Add(b); t.Add(b + 1); t.Add(b + 2);   // 뒤
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            var n = new Vector3[v.Count];
            for (int i = 0; i < n.Length; i++) n[i] = Vector3.up;
            mesh.normals = n;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>윗면만 있는 불규칙한 원반(지름 1). 테두리 반경을 정해진 패턴(<see cref="PatchWobble"/>)으로 흔들어 유기적인 윤곽을 낸다.</summary>
        internal static Mesh IrregularDisc(string name, int segments, float radius)
        {
            var v = new Vector3[segments + 1];
            var tri = new int[segments * 3];
            v[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float k = PatchWobble(a);
                v[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * k;
                tri[i * 3] = 0;
                tri[i * 3 + 1] = (i + 1) % segments + 1;
                tri[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = name, vertices = v, triangles = tri };
            var n = new Vector3[v.Length];
            for (int i = 0; i < n.Length; i++) n[i] = Vector3.up;
            mesh.normals = n;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// 옆면만 있는 각기둥(높이 2, 지름 1, 중심 원점 — 내장 Capsule과 같은 크기 규약). 나무줄기·쓰러진 기둥.
        /// 위아래 뚜껑은 고각 카메라에서 거의 안 보이므로 뺀다.
        /// </summary>
        private static Mesh Prism(string name, int segments)
        {
            var v = new Vector3[(segments + 1) * 2];
            var n = new Vector3[v.Length];
            var t = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v[i * 2] = dir * 0.5f + Vector3.down;
                v[i * 2 + 1] = dir * 0.44f + Vector3.up;   // 위로 살짝 가늘게
                n[i * 2] = dir;
                n[i * 2 + 1] = dir;
            }
            for (int i = 0; i < segments; i++)
            {
                int b = i * 2;
                t[i * 6] = b; t[i * 6 + 1] = b + 1; t[i * 6 + 2] = b + 2;
                t[i * 6 + 3] = b + 1; t[i * 6 + 4] = b + 3; t[i * 6 + 5] = b + 2;
            }
            var mesh = new Mesh { name = name, vertices = v, normals = n, triangles = t };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>밑면 없는 각뿔(높이 1, 밑지름 1, 밑면이 y=0). 얼음 조각·침엽수 층·가시.</summary>
        private static Mesh Cone(string name, int segments)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            Vector3 tip = new Vector3(0f, 1f, 0f);
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * 0.5f;
                Vector3 p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * 0.5f;
                int b = v.Count;
                v.Add(p0); v.Add(tip); v.Add(p1);
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ======= 유틸 =======

        /// <summary>
        /// 이 파일의 색은 옛 노출(환경광 0) 기준으로 골랐다. Trilight 환경광을 켠 뒤 화면이 하얗게 날아가서
        /// <see cref="RegionPalette"/>와 함께 한 번에 누른다 — 발광색은 Emission이 따로 받으므로 영향이 없다.
        /// </summary>
        private const float Tone = 0.84f;

        private static Color C(float r, float g, float b) => new Color(r * Tone, g * Tone, b * Tone, 1f);

        private static Quaternion Yaw() => Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        private static Vector3 Polar(float deg, float dist)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * dist, 0f, Mathf.Sin(a) * dist);
        }

        private static Vector3 Floor(Vector3 p) => new Vector3(p.x, FloorY, p.z);

        /// <summary>
        /// 선 소품(풀포기·꽃·조약돌·버섯·잔가지·관목·돌조각·눈덩이·얼음 조각·불씨)을 둔덕 윗면에 올린다. 둔덕은 콜라이더가
        /// 없어(<see cref="RegionTerrainBuilder.PlaceMound"/>) 바닥 높이에 두면 그 속에 묻힌다 — 초원 언덕·사구·눈 언덕 위가
        /// 소품 없는 민둥 무늬로 보였다. 모양은 지형 빌더가 <see cref="FieldGround"/>에 올려 두었다(지형이 먼저, 장식은 Start).
        /// 납작한 무늬(얼룩·잎·금·모래 물결·웅덩이)는 올리지 않는다 — 둥근 윗면에 평판을 띄우면 한쪽 끝이 공중에 뜨고
        /// 반대쪽이 묻힌다. 둔덕 속에 그대로 두면 둔덕이 가려 줄 뿐이다. 난수를 쓰지 않는다.
        /// </summary>
        private static Vector3 Grounded(Vector3 p) => new Vector3(p.x, p.y + FieldGround.LiftAt(p.x, p.z), p.z);

        /// <summary>개수를 면적 비례로(설계 반경 50m 기준).</summary>
        private static int Scale(int baseCount, RegionData r)
        {
            float k = (r.radius / 50f) * (r.radius / 50f);
            return Mathf.Max(1, Mathf.RoundToInt(baseCount * k));
        }

        /// <summary>테두리 개수를 둘레 비례로(설계 반경 50m 기준).</summary>
        private static int RimCount(RegionData r, float baseCount)
        {
            return Mathf.Max(1, Mathf.RoundToInt(baseCount * r.radius / 50f));
        }

        private void OnDestroy()
        {
            foreach (Mesh m in ownedMeshes) if (m != null) Destroy(m);
            foreach (Material m in ownedMaterials) if (m != null) Destroy(m);
            foreach (Texture2D t in ownedTextures) if (t != null) Destroy(t);
            ownedMeshes.Clear();
            ownedMaterials.Clear();
            ownedTextures.Clear();
            materialCache.Clear();
            ClearWater();   // 물 기록도 이 월드의 것이다 — 다음 씬이 옛 물을 보지 않게
        }
    }
}
