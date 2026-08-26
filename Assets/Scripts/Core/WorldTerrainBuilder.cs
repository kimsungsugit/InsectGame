using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    public class WorldTerrainBuilder : MonoBehaviour
    {
        private RegionData[] cachedRegions;

        public void BuildTerrain(RegionData[] regions)
        {
            cachedRegions = regions;
            ApplyElevation(regions);
            BuildElevationSlopes(regions);
            BuildCliffs(regions);
            BuildRiver(regions);
            BuildBridges(regions);
            BuildMapBoundary();
            BuildExtraPaths(regions);
        }

        private void ApplyElevation(RegionData[] regions)
        {
            foreach (var r in regions)
            {
                float y = GetRegionElevation(r.regionId);
                r.centerPosition = new Vector3(r.centerPosition.x, y, r.centerPosition.z);

                if (r.subAreas != null)
                {
                    foreach (var sub in r.subAreas)
                        sub.centerPosition = new Vector3(sub.centerPosition.x, y, sub.centerPosition.z);
                }

                GameObject regionObj = GameObject.Find($"Region_{r.regionId}");
                if (regionObj != null)
                    regionObj.transform.position = r.centerPosition + new Vector3(0f, 0.08f, 0f);
            }
        }

        private float GetRegionElevation(string regionId)
        {
            // 전 리전 0으로 평탄화 — 복원 금지.
            // 상승 리전(forest/ruins/mountain, was 4/8/12): 불투명 Region_ 평면이 카메라와
            // 플레이어 사이를 가려 캐릭터가 안 보이는 회귀(사용자 반복 보고) + PlayerMovement
            // 2유닛 raycast가 단차를 못 올라 Y=0에 갇힘 → 평탄화가 근본 해결.
            // 함몰 리전(pond/swamp, was -3/-2): 베이스 Ground 평면(Y=0, 전역 불투명)이 위를
            // 덮어 리전 평면·호수·부두·갈대 등 모든 소품이 지면 아래 묻혀 비가시가 되고,
            // 플레이어는 Y=0 베이스 지면 위를 걸어 함몰 연출 자체가 성립하지 않았다.
            // 고도는 리전 판정(ContainsPoint=XZ만)·스폰(player.y 추종)에 영향 0인 순수
            // 장식이므로 동일하게 평탄화한다.
            switch (regionId)
            {
                case "meadow": return 0f;
                case "pond": return 0f;      // was -3f — 베이스 지면에 묻혀 소품 전체 비가시
                case "garden": return 0f;
                case "forest": return 0f;   // was 4f — 차폐 회귀로 평탄화
                case "swamp": return 0f;     // was -2f — 베이스 지면에 묻혀 소품 전체 비가시
                case "mountain": return 0f;  // was 12f — 차폐 회귀로 평탄화
                case "ruins": return 0f;     // was 8f — 차폐 회귀로 평탄화
                default: return 0f;
            }
        }

        private void BuildElevationSlopes(RegionData[] regions)
        {
            Material slopeMat = CreateMat(new Color(0.3f, 0.42f, 0.2f));

            // pond/swamp 평탄화(GetRegionElevation=0)에 맞춰 0→0. 옛 0→-3/-2 램프는 지면 아래로 꺼지는 잔재가 됨.
            CreateSlope("Slope_Meadow_Pond", GetCenter(regions, "meadow"), GetCenter(regions, "pond"), 0f, 0f, 5f, slopeMat);
            // forest 평탄화(GetRegionElevation=0)에 맞춰 0→0. 옛 0→4 램프는 허공으로 솟구치는 잔재가 됨.
            CreateSlope("Slope_Meadow_Forest", GetCenter(regions, "meadow"), GetCenter(regions, "forest"), 0f, 0f, 6f, slopeMat);

            Material swampSlope = CreateMat(new Color(0.25f, 0.35f, 0.18f));
            CreateSlope("Slope_Meadow_Swamp", GetCenter(regions, "meadow"), GetCenter(regions, "swamp"), 0f, 0f, 5f, swampSlope);

            Material stoneSlope = CreateMat(new Color(0.4f, 0.38f, 0.35f));
            // mountain·ruins 평탄화에 맞춰 0→0. 옛 12→8 램프는 평지 위 공중에 떠 있게 됨.
            CreateSlope("Slope_Mountain_Ruins", GetCenter(regions, "mountain"), GetCenter(regions, "ruins"), 0f, 0f, 4f, stoneSlope);
        }

        private void CreateSlope(string name, Vector3 from, Vector3 to, float fromY, float toY, float width, Material mat)
        {
            Vector3 flatFrom = new Vector3(from.x, 0f, from.z);
            Vector3 flatTo = new Vector3(to.x, 0f, to.z);
            Vector3 dir = flatTo - flatFrom;
            float dist = dir.magnitude;
            if (dist < 1f) return;

            Vector3 mid = Vector3.Lerp(flatFrom, flatTo, 0.5f);
            float slopeDist = dist * 0.4f;
            float midY = (fromY + toY) / 2f;

            float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float heightDiff = toY - fromY;
            float tiltAngle = Mathf.Atan2(heightDiff, slopeDist) * Mathf.Rad2Deg;

            GameObject slope = GameObject.CreatePrimitive(PrimitiveType.Plane);
            slope.name = name;
            slope.transform.position = new Vector3(mid.x, midY + 0.15f, mid.z);
            slope.transform.rotation = Quaternion.Euler(tiltAngle, angle, 0f);
            slope.transform.localScale = new Vector3(width / 10f, 1f, slopeDist / 10f);
            slope.GetComponent<MeshRenderer>().material = mat;
        }

        private void BuildCliffs(RegionData[] regions)
        {
            Material cliffMat = CreateMat(new Color(0.45f, 0.4f, 0.35f));
            Material darkCliffMat = CreateMat(new Color(0.35f, 0.32f, 0.28f));

            Vector3 forestCenter = GetCenter(regions, "forest");
            Vector3 mountainCenter = GetCenter(regions, "mountain");
            float forestRadius = GetRadius(regions, "forest");
            float mountainRadius = GetRadius(regions, "mountain");

            // forest→mountain 사이 절벽: 두 리전 경계 밖에만 배치
            Vector3 dir = (mountainCenter - forestCenter).normalized;
            Vector3 cliffStart = forestCenter + dir * (forestRadius + 2f);
            Vector3 cliffEnd = mountainCenter - dir * (mountainRadius + 2f);

            // 절벽 구간이 유효한 경우에만 생성
            float cliffDist = Vector3.Distance(cliffStart, cliffEnd);
            if (cliffDist > 5f)
            {
                int count = Mathf.Max(3, Mathf.RoundToInt(cliffDist / 8f));
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / (count - 1);
                    Vector3 pos = Vector3.Lerp(cliffStart, cliffEnd, t);
                    float arcOffset = Mathf.Sin(t * Mathf.PI) * 6f;
                    Vector3 perp = Vector3.Cross(dir, Vector3.up).normalized;
                    pos += perp * arcOffset;

                    GameObject cliff = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cliff.name = $"Cliff_ForestMountain_{i}";
                    cliff.transform.position = pos + new Vector3(0f, 3f, 0f);
                    cliff.transform.localScale = new Vector3(3f, 6f, 2f);
                    cliff.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, i * 8f, 0f);
                    cliff.GetComponent<MeshRenderer>().material = i % 2 == 0 ? cliffMat : darkCliffMat;
                }
            }

            // mountain 외곽 절벽 (ruins 방향 제외)
            Vector3 ruinsCenter = GetCenter(regions, "ruins");
            Vector3 toRuins = (ruinsCenter - mountainCenter).normalized;
            float mRad = mountainRadius + 3f;

            for (int i = 0; i < 8; i++)
            {
                float angle = Mathf.PI * 0.3f + i * Mathf.PI * 1.0f / 8f;
                Vector3 d = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                if (Vector3.Dot(d, toRuins) > 0.4f) continue;
                // forest 방향도 건너뛰기 (절벽이 이미 있음)
                if (Vector3.Dot(d, (forestCenter - mountainCenter).normalized) > 0.4f) continue;

                Vector3 pos = mountainCenter + d * mRad;
                pos.y = mountainCenter.y;

                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Cliff_Mountain_{i}";
                wall.transform.position = pos + new Vector3(0f, 4f, 0f);
                wall.transform.localScale = new Vector3(6f, 8f, 2f);
                wall.transform.rotation = Quaternion.LookRotation(d);
                wall.GetComponent<MeshRenderer>().material = cliffMat;
            }
        }

        private void BuildRiver(RegionData[] regions)
        {
            Vector3 pondCenter = GetCenter(regions, "pond");
            Vector3 meadowCenter = GetCenter(regions, "meadow");

            // 강: pond에서 meadow 방향으로 흐름 (pond 리전 가장자리)
            float pondRadius = GetRadius(regions, "pond");
            Vector3 riverDir = (meadowCenter - pondCenter).normalized;
            Vector3 riverStart = pondCenter + riverDir * (pondRadius * 0.5f);
            Vector3 riverEnd = pondCenter + riverDir * (pondRadius + 15f);

            Material waterMat = CreateMat(new Color(0.15f, 0.35f, 0.65f, 0.7f));
            SetTransparent(waterMat);
            Material bankMat = CreateMat(new Color(0.35f, 0.3f, 0.22f));

            int segments = 5;
            float riverWidth = 5f;
            float bridgeT = 0.5f;

            for (int i = 0; i < segments; i++)
            {
                float tStart = (float)i / segments;
                float tEnd = (float)(i + 1) / segments;
                float tMid = (tStart + tEnd) / 2f;

                if (Mathf.Abs(tMid - bridgeT) < 0.12f) continue;

                Vector3 from = Vector3.Lerp(riverStart, riverEnd, tStart);
                Vector3 to = Vector3.Lerp(riverStart, riverEnd, tEnd);
                Vector3 perp = Vector3.Cross(riverDir, Vector3.up).normalized;
                float wave = Mathf.Sin(tMid * Mathf.PI * 2f) * 3f;
                Vector3 mid = (from + to) / 2f + perp * wave;

                Vector3 segDir = to - from;
                float len = segDir.magnitude;
                float angle = Mathf.Atan2(segDir.x, segDir.z) * Mathf.Rad2Deg;
                float y = Mathf.Lerp(pondCenter.y, pondCenter.y + 1f, tMid);

                GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
                water.name = $"River_Water_{i}";
                water.transform.position = new Vector3(mid.x, y + 0.05f, mid.z);
                water.transform.rotation = Quaternion.Euler(0f, angle, 0f);
                water.transform.localScale = new Vector3(riverWidth / 10f, 1f, (len + 2f) / 10f);
                water.GetComponent<MeshRenderer>().material = waterMat;
                Object.Destroy(water.GetComponent<Collider>());

                GameObject blocker = new GameObject($"River_Blocker_{i}");
                blocker.transform.position = new Vector3(mid.x, y + 0.5f, mid.z);
                blocker.transform.rotation = Quaternion.Euler(0f, angle, 0f);
                BoxCollider box = blocker.AddComponent<BoxCollider>();
                box.size = new Vector3(riverWidth, 2f, len + 2f);
            }

            // 강둑 돌
            for (int i = 0; i < 14; i++)
            {
                float t = (float)i / 14f;
                Vector3 pos = Vector3.Lerp(riverStart, riverEnd, t);
                Vector3 perp = Vector3.Cross(riverDir, Vector3.up).normalized;
                float wave = Mathf.Sin(t * Mathf.PI * 2f) * 3f;
                pos += perp * wave;
                pos.y = Mathf.Lerp(pondCenter.y, pondCenter.y + 1f, t);

                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject bank = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    bank.name = $"River_Bank_{i}_{(side > 0 ? "R" : "L")}";
                    float s = Random.Range(0.4f, 0.7f);
                    bank.transform.position = pos + perp * (riverWidth * 0.5f + 0.5f) * side + new Vector3(0f, s * 0.15f, 0f);
                    bank.transform.localScale = new Vector3(s * 1.5f, s * 0.4f, s);
                    bank.GetComponent<MeshRenderer>().material = bankMat;
                    Object.Destroy(bank.GetComponent<Collider>());
                }
            }
        }

        private void BuildBridges(RegionData[] regions)
        {
            Material woodMat = CreateMat(new Color(0.5f, 0.35f, 0.15f));
            Material railMat = CreateMat(new Color(0.45f, 0.3f, 0.12f));
            Material plankMat = CreateMat(new Color(0.55f, 0.4f, 0.2f));

            // 다리 1: pond 강 위 (50% 지점)
            Vector3 pondCenter = GetCenter(regions, "pond");
            Vector3 meadowCenter = GetCenter(regions, "meadow");
            float pondRadius = GetRadius(regions, "pond");
            Vector3 riverDir = (meadowCenter - pondCenter).normalized;
            Vector3 riverStart = pondCenter + riverDir * (pondRadius * 0.5f);
            Vector3 riverEnd = pondCenter + riverDir * (pondRadius + 15f);
            Vector3 bridgePos = Vector3.Lerp(riverStart, riverEnd, 0.5f);
            bridgePos.y = pondCenter.y + 0.5f;
            Vector3 bridgeDir = Vector3.Cross(riverDir, Vector3.up).normalized;

            CreateBridge("Bridge_PondRiver", bridgePos, bridgeDir, 8f, 3f, woodMat, railMat, plankMat);

            // 다리 2: mountain-ruins 연결
            Material stoneBridgeMat = CreateMat(new Color(0.5f, 0.48f, 0.42f));
            Material stoneRailMat = CreateMat(new Color(0.45f, 0.43f, 0.38f));
            Vector3 mountainCenter = GetCenter(regions, "mountain");
            Vector3 ruinsCenter = GetCenter(regions, "ruins");
            Vector3 bridge2Dir = (ruinsCenter - mountainCenter).normalized;
            Vector3 bridge2Pos = Vector3.Lerp(mountainCenter, ruinsCenter, 0.5f);
            bridge2Pos.y = (mountainCenter.y + ruinsCenter.y) / 2f;

            CreateBridge("Bridge_MountainRuins", bridge2Pos, bridge2Dir, 12f, 3.5f, stoneBridgeMat, stoneRailMat, stoneBridgeMat);
        }

        private void CreateBridge(string name, Vector3 pos, Vector3 dir, float length, float width, Material floorMat, Material railMat, Material plankMat)
        {
            float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = $"{name}_Floor";
            floor.transform.position = pos + new Vector3(0f, 0.15f, 0f);
            floor.transform.rotation = Quaternion.Euler(0f, angle, 0f);
            floor.transform.localScale = new Vector3(width, 0.3f, length);
            floor.GetComponent<MeshRenderer>().material = floorMat;

            Vector3 perp = new Vector3(-dir.z, 0f, dir.x).normalized;
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = $"{name}_Rail_{(side > 0 ? "R" : "L")}";
                rail.transform.position = pos + perp * (width * 0.5f) * side + new Vector3(0f, 0.8f, 0f);
                rail.transform.rotation = Quaternion.Euler(0f, angle, 0f);
                rail.transform.localScale = new Vector3(0.15f, 1f, length);
                rail.GetComponent<MeshRenderer>().material = railMat;
                Object.Destroy(rail.GetComponent<Collider>());

                for (int p = 0; p < 4; p++)
                {
                    float t = (p + 0.5f) / 4f - 0.5f;
                    Vector3 postPos = pos + dir * (t * length) + perp * (width * 0.5f) * side;
                    GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    post.name = $"{name}_Post_{side}_{p}";
                    post.transform.position = postPos + new Vector3(0f, 0.5f, 0f);
                    post.transform.rotation = Quaternion.Euler(0f, angle, 0f);
                    post.transform.localScale = new Vector3(0.12f, 1f, 0.12f);
                    post.GetComponent<MeshRenderer>().material = plankMat;
                    Object.Destroy(post.GetComponent<Collider>());
                }
            }
        }

        private void BuildMapBoundary()
        {
            Material boundaryMat = CreateMat(new Color(0.3f, 0.35f, 0.25f));
            // 2막(ver2) 6지역 추가로 월드가 북·동으로 뻗었다. 최원점은 canopy(z=397.5, r=75)의
            // 472.5 — 옛 320이면 canopy/emberfall/frostline/nameless가 벽 **바깥**에 놓여
            // 도달 불가 지역이 된다. 여유 47.5를 두고 520.
            // **PlaySceneBootstrap.EnsureGround의 Ground Plane 스케일과 짝이다** —
            // 지면이 벽보다 좁으면 벽 앞이 무바닥(무한 낙하)이 된다.
            float mapSize = 520f;
            float wallHeight = 15f;

            string[] names = { "Boundary_N", "Boundary_S", "Boundary_E", "Boundary_W" };
            Vector3[] positions = {
                new Vector3(0f, wallHeight / 2f, mapSize),
                new Vector3(0f, wallHeight / 2f, -mapSize),
                new Vector3(mapSize, wallHeight / 2f, 0f),
                new Vector3(-mapSize, wallHeight / 2f, 0f)
            };
            Vector3[] scales = {
                new Vector3(mapSize * 2f, wallHeight, 3f),
                new Vector3(mapSize * 2f, wallHeight, 3f),
                new Vector3(3f, wallHeight, mapSize * 2f),
                new Vector3(3f, wallHeight, mapSize * 2f)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = names[i];
                wall.transform.position = positions[i];
                wall.transform.localScale = scales[i];
                wall.GetComponent<MeshRenderer>().material = boundaryMat;
            }
        }

        private void BuildExtraPaths(RegionData[] regions)
        {
            Material pathMat = CreateMat(new Color(0.55f, 0.48f, 0.32f));
            CreateSimplePath("Path_Meadow_Swamp", GetCenter(regions, "meadow"), GetCenter(regions, "swamp"), 2.5f, pathMat);
            CreateSimplePath("Path_Meadow_Garden", GetCenter(regions, "meadow"), GetCenter(regions, "garden"), 2.5f, pathMat);

            // ── 1막 진행 사슬 ── 2막 블록이 아래에서 하는 일을 1막에도 한다.
            // 해금은 meadow→pond→forest→swamp→mountain→ruins 한 줄로 도는데, 그중 땅에 길이
            // 깔린 건 mountain→ruins 하나뿐이었다. **`pond→forest`와 `swamp→mountain`은
            // 지도 간선조차 없었다** — 다음 목적지로 가는 길만 골라서 안 보였다.
            // (meadow→garden은 위에 이미 있다. 사슬이 아닌 공간 인접선은 지도에만 둔다 —
            //  길로 깔면 사슬과 구분이 안 돼 오히려 엉뚱한 쪽으로 이끈다.)
            CreateSimplePath("Path_Meadow_Pond", GetCenter(regions, "meadow"), GetCenter(regions, "pond"), 2.5f, pathMat);
            CreateSimplePath("Path_Pond_Forest", GetCenter(regions, "pond"), GetCenter(regions, "forest"), 2.5f, pathMat);
            CreateSimplePath("Path_Forest_Swamp", GetCenter(regions, "forest"), GetCenter(regions, "swamp"), 2.5f, pathMat);
            CreateSimplePath("Path_Swamp_Mountain", GetCenter(regions, "swamp"), GetCenter(regions, "mountain"), 2.5f, pathMat);

            Material stonePath = CreateMat(new Color(0.5f, 0.45f, 0.4f));
            CreateSimplePath("Path_Mountain_Ruins", GetCenter(regions, "mountain"), GetCenter(regions, "ruins"), 3f, stonePath);

            // ── 2막(ver2) 사슬 ── RegionMapUI.Connections와 같은 토폴로지를 월드에도 깐다.
            // 지도에는 길이 있는데 땅에는 없으면 어디로 가야 할지 알 수 없다.
            // 색을 바래게 둔 건 이 길들이 오래 방치된 땅으로 이어지기 때문이다.
            Material fadedPath = CreateMat(new Color(0.46f, 0.44f, 0.38f));
            string[,] act2Chain =
            {
                { "ruins", "hollow" }, { "hollow", "dunes" }, { "dunes", "frostline" },
                { "frostline", "emberfall" }, { "emberfall", "canopy" }, { "canopy", "nameless" }
            };
            for (int i = 0; i < act2Chain.GetLength(0); i++)
            {
                string a = act2Chain[i, 0], b = act2Chain[i, 1];
                CreateSimplePath($"Path_{a}_{b}", GetCenter(regions, a), GetCenter(regions, b), 2.5f, fadedPath);
            }
        }

        private void CreateSimplePath(string name, Vector3 from, Vector3 to, float width, Material mat)
        {
            Vector3 dir = to - from;
            float dist = new Vector3(dir.x, 0f, dir.z).magnitude;
            float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

            int segCount = Mathf.Max(2, Mathf.RoundToInt(dist / 30f));
            for (int i = 0; i < segCount; i++)
            {
                float tMid = ((float)i + 0.5f) / segCount;
                Vector3 segMid = Vector3.Lerp(from, to, tMid);
                float segLen = dist / segCount;

                GameObject path = GameObject.CreatePrimitive(PrimitiveType.Plane);
                path.name = $"{name}_{i}";
                // 지면 장식의 적층 순서 — 0.00 베이스 지면 / 0.08 리전 평면 / 0.15 경사면 / **0.18 길**.
                // 길이 맨 위여야 한다. 예전엔 0.12여서 **경사면(0.15) 아래에 깔렸고**, 경사면은
                // 두 리전 중심 사이 가운데 40%를 덮으므로 길이 **한복판에서 끊겨 보였다** —
                // 하필 길의 존재 이유(어디로 가는지)가 가장 필요한 구간이다.
                // meadow―swamp·mountain―ruins가 실제로 그 상태였고, 경사면은 지금 전부 0→0이라
                // (GetRegionElevation 평탄화) 가릴 높이차도 없는 장식이다.
                path.transform.position = segMid + new Vector3(0f, 0.18f, 0f);
                path.transform.rotation = Quaternion.Euler(0f, angle, 0f);
                path.transform.localScale = new Vector3(width / 10f, 1f, segLen / 10f);
                path.GetComponent<MeshRenderer>().material = mat;
                Object.Destroy(path.GetComponent<Collider>());
            }
        }

        private Vector3 GetCenter(RegionData[] regions, string id)
        {
            foreach (var r in regions)
                if (r.regionId == id) return r.centerPosition;
            return Vector3.zero;
        }

        private float GetRadius(RegionData[] regions, string id)
        {
            foreach (var r in regions)
                if (r.regionId == id) return r.radius;
            return 40f;
        }

        private Material CreateMat(Color color)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material mat = shader != null ? new Material(shader) : new Material(Shader.Find("Hidden/InternalErrorShader"));
            mat.color = color;
            return mat;
        }

        private void SetTransparent(Material mat)
        {
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
        }
    }
}
