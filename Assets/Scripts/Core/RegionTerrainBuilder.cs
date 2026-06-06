using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 각 리전에 게임 필드 느낌의 지형을 생성합니다.
    /// 언덕/절벽/길/바위/나무/물 등으로 각 리전을 구분되는 공간으로 만듭니다.
    /// EnsureGround() 뒤에 호출됩니다.
    /// </summary>
    public class RegionTerrainBuilder : MonoBehaviour
    {
        public void BuildAllRegions(RegionData[] regions)
        {
            foreach (var r in regions)
            {
                Vector3 c = r.centerPosition;
                float rad = r.radius;
                switch (r.regionId)
                {
                    case "meadow": BuildMeadowTerrain(c, rad); break;
                    case "pond": BuildPondTerrain(c, rad); break;
                    case "forest": BuildForestTerrain(c, rad); break;
                    case "swamp": BuildSwampTerrain(c, rad); break;
                    case "mountain": BuildMountainTerrain(c, rad); break;
                    case "garden": BuildGardenTerrain(c, rad); break;
                    case "ruins": BuildRuinsTerrain(c, rad); break;
                }
            }
            // 옛은 리전 경계가 빈 공간이라 어디서든 자유 이동 가능 + "네모 박스" 인상.
            // 각 리전 외곽에 fence + 인접 리전으로 향하는 gateway(좁은 통로) 생성.
            BuildBoundaries(regions);
        }

        // ======= 리전 경계 fence + gateway =======
        // 각 리전 외곽 원주에 환경별 fence(돌담/나무/바위 등). 인접 리전(중심거리 ≤ r1+r2+30m)
        // 방향의 gateway angle에는 fence 빠뜨려 좁은 통로 형성. collider 보존으로 통과 차단.
        public void BuildBoundaries(RegionData[] regions)
        {
            if (regions == null || regions.Length == 0) return;
            const float NeighborThresholdExtra = 30f;
            const float DefaultGatewayWidthDeg = 22f; // 약 5m gateway (r=50 기준 호 길이)
            for (int i = 0; i < regions.Length; i++)
            {
                RegionData r = regions[i];
                if (r == null) continue;

                // 인접 리전 자동 검출 → gateway angle 목록
                var gateways = new System.Collections.Generic.List<(float angleDeg, float widthDeg)>();
                // RegionData.connections가 명시되면 우선 사용 (수동 통제)
                if (r.connections != null)
                {
                    foreach (var conn in r.connections)
                    {
                        if (conn == null || string.IsNullOrEmpty(conn.targetRegionId)) continue;
                        // gatewayWidth(m) → 각도 변환: 호 = width, 반지름 = r.radius → arc deg = width/radius * Rad2Deg
                        float widthDeg = Mathf.Min(60f, conn.gatewayWidth / Mathf.Max(1f, r.radius) * Mathf.Rad2Deg);
                        gateways.Add((NormalizeAngle(conn.gatewayAngle), Mathf.Max(8f, widthDeg)));
                    }
                }
                else
                {
                    // 자동 검출 — 가장 가까운 인접 리전 1곳만 gateway (사용자 명시 요청: 각 리전 외부 입구 1곳).
                    // 옛은 N개 인접 모두 gateway → "어디든 들어갈 수 있다" 인상.
                    RegionData closest = null;
                    float closestDist = float.MaxValue;
                    for (int j = 0; j < regions.Length; j++)
                    {
                        if (i == j) continue;
                        RegionData other = regions[j];
                        if (other == null) continue;
                        Vector3 d = other.centerPosition - r.centerPosition;
                        float dist = new Vector2(d.x, d.z).magnitude;
                        if (dist > r.radius + other.radius + NeighborThresholdExtra) continue;
                        if (dist < closestDist) { closest = other; closestDist = dist; }
                    }
                    if (closest != null)
                    {
                        Vector3 dc = closest.centerPosition - r.centerPosition;
                        float angle = Mathf.Atan2(dc.z, dc.x) * Mathf.Rad2Deg;
                        gateways.Add((NormalizeAngle(angle), DefaultGatewayWidthDeg));
                    }
                }

                BuildFenceArc(r, gateways);
            }
        }

        private static float NormalizeAngle(float deg)
        {
            float a = deg % 360f;
            if (a < 0f) a += 360f;
            return a;
        }

        private static bool IsInGateway(float angleDeg,
            System.Collections.Generic.List<(float angleDeg, float widthDeg)> gateways)
        {
            for (int g = 0; g < gateways.Count; g++)
            {
                float diff = Mathf.Abs(Mathf.DeltaAngle(angleDeg, gateways[g].angleDeg));
                if (diff <= gateways[g].widthDeg * 0.5f) return true;
            }
            return false;
        }

        // 리전 외곽 원주 fence — 각도 sample마다 fence post 배치. gateway angle 범위는 빈 공간.
        private void BuildFenceArc(RegionData r,
            System.Collections.Generic.List<(float angleDeg, float widthDeg)> gateways)
        {
            Material fenceMat = GetFenceMaterial(r.regionId);
            Material gatewayMarkerMat = Mat(new Color(1f, 0.85f, 0.3f));
            float fenceR = r.radius - 1f; // 외곽 살짝 안쪽
            int segments = 60; // 6° 간격 (60 × 6 = 360°)
            for (int s = 0; s < segments; s++)
            {
                float angDeg = (360f / segments) * s;
                if (IsInGateway(angDeg, gateways)) continue;
                float rad = angDeg * Mathf.Deg2Rad;
                Vector3 pos = r.centerPosition + new Vector3(Mathf.Cos(rad) * fenceR, 0f, Mathf.Sin(rad) * fenceR);
                BuildFencePost(pos, angDeg, fenceMat, r.regionId);
            }
            // gateway 위치에 노란 표지등(시각 anchor) — 통과 가능 시각 신호
            for (int g = 0; g < gateways.Count; g++)
            {
                float rad = gateways[g].angleDeg * Mathf.Deg2Rad;
                Vector3 pos = r.centerPosition + new Vector3(Mathf.Cos(rad) * fenceR, 0.5f, Mathf.Sin(rad) * fenceR);
                GameObject marker = Prim(PrimitiveType.Cylinder, $"GatewayMarker_{r.regionId}_{g}");
                marker.transform.position = pos;
                marker.transform.localScale = new Vector3(0.4f, 1.2f, 0.4f);
                Apply(marker, gatewayMarkerMat);
                Destroy(marker.GetComponent<Collider>());
            }
        }

        private Material GetFenceMaterial(string regionId)
        {
            switch (regionId)
            {
                case "meadow": return Mat(new Color(0.55f, 0.45f, 0.3f));  // 돌담
                case "pond": return Mat(new Color(0.35f, 0.5f, 0.3f));      // 갈대
                case "forest": return Mat(new Color(0.25f, 0.18f, 0.1f));   // 나무 wall
                case "swamp": return Mat(new Color(0.3f, 0.25f, 0.15f));    // 진흙 둔덕
                case "mountain": return Mat(new Color(0.45f, 0.45f, 0.45f));// 바위 벽
                case "garden": return Mat(new Color(0.3f, 0.6f, 0.3f));     // 생울타리
                case "ruins": return Mat(new Color(0.55f, 0.5f, 0.45f));    // 폐허 벽
                default: return Mat(new Color(0.5f, 0.45f, 0.35f));
            }
        }

        private void BuildFencePost(Vector3 pos, float facingDeg, Material mat, string regionId)
        {
            // 환경에 따라 fence shape 변경: forest는 나무(Cylinder 큼), garden은 생울타리(Cube 낮음), 등
            GameObject post;
            Vector3 scale;
            switch (regionId)
            {
                case "forest":
                    // 옛 (0.5, 2.5, 0.5) 60개는 시야를 막음 — 짧게 변경 (카메라 위로 비침)
                    post = Prim(PrimitiveType.Cylinder, "FenceTree");
                    scale = new Vector3(0.5f, 1.5f, 0.5f);
                    break;
                case "mountain":
                case "ruins":
                    // 옛 scale (1.6, 2, 1.6) 60개는 region 외곽 둘러싸서 카메라 부감 시점에 큰 사각형
                    // layer가 캐릭터 위로 떠 보이는 회귀(사용자 명시 보고, ruins 스크린샷 확인).
                    // (0.8, 0.8, 0.8) 작은 돌무더기 → 외곽 경계 시각 유지하며 layer 인상 제거.
                    post = Prim(PrimitiveType.Cube, "FenceRock");
                    scale = new Vector3(0.8f, 0.8f, 0.8f);
                    break;
                case "garden":
                    post = Prim(PrimitiveType.Cube, "FenceHedge");
                    scale = new Vector3(1.5f, 1.5f, 1.5f);
                    break;
                case "pond":
                    post = Prim(PrimitiveType.Cylinder, "FenceReed");
                    scale = new Vector3(0.15f, 2.5f, 0.15f);
                    break;
                case "swamp":
                    post = Prim(PrimitiveType.Cube, "FenceMound");
                    scale = new Vector3(1.4f, 1.2f, 1.4f);
                    break;
                default:
                    post = Prim(PrimitiveType.Cube, "FencePost");
                    scale = new Vector3(0.4f, 1.5f, 1.2f);
                    break;
            }
            post.transform.position = pos + new Vector3(0f, scale.y * 0.5f, 0f);
            post.transform.localScale = scale;
            post.transform.rotation = Quaternion.Euler(0f, -facingDeg + 90f, 0f); // 원 접선 방향
            Apply(post, mat);
            // collider 보존 — PlayerMovement.IsBlockedPosition OverlapSphere가 통과 차단
        }


        // ======= 초원: 완만한 언덕 + 개울 + 울타리 길 =======
        private void BuildMeadowTerrain(Vector3 c, float rad)
        {
            Material hillMat = Mat(new Color(0.32f, 0.52f, 0.22f));
            Material pathMat = Mat(new Color(0.55f, 0.48f, 0.32f));
            Material stoneMat = Mat(new Color(0.5f, 0.48f, 0.42f));

            // 완만한 언덕 5개 (걸어 올라갈 수 있는 경사)
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f + 0.3f;
                float d = rad * Random.Range(0.25f, 0.55f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float hs = Random.Range(4f, 8f);

                GameObject hill = Prim(PrimitiveType.Sphere, $"Scenery_MeadowHill_{i}");
                hill.transform.position = pos + new Vector3(0f, -hs * 0.25f, 0f);
                hill.transform.localScale = new Vector3(hs * 2f, hs * 0.5f, hs * 2f);
                Apply(hill, hillMat);
                Destroy(hill.GetComponent<Collider>());
            }

            // 개울 (얇은 물길)
            Material waterMat = Mat(new Color(0.3f, 0.5f, 0.7f, 0.5f));
            SetTransparent(waterMat);
            for (int i = 0; i < 4; i++)
            {
                float t = (float)i / 4f;
                Vector3 from = c + new Vector3(-rad * 0.4f, 0.03f, -rad * 0.3f + t * rad * 0.6f);
                GameObject creek = Prim(PrimitiveType.Plane, $"Scenery_Creek_{i}");
                creek.transform.position = from;
                creek.transform.localScale = new Vector3(0.2f, 1f, 0.15f);
                creek.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
                Apply(creek, waterMat);
                Destroy(creek.GetComponent<Collider>());
            }

            // 돌담길 (리전 내 메인 경로)
            CreateInternalPath(c, c + new Vector3(rad * 0.7f, 0f, 0f), 2f, pathMat, stoneMat);
            CreateInternalPath(c, c + new Vector3(0f, 0f, rad * 0.7f), 2f, pathMat, stoneMat);
        }

        // ======= 연못: 큰 호수 + 데크/부두 + 갈대 =======
        private void BuildPondTerrain(Vector3 c, float rad)
        {
            Material waterMat = Mat(new Color(0.15f, 0.35f, 0.6f, 0.6f));
            SetTransparent(waterMat);
            Material deckMat = Mat(new Color(0.45f, 0.32f, 0.15f));
            Material reedMat = Mat(new Color(0.4f, 0.5f, 0.2f));

            // 큰 호수 (리전 중심)
            GameObject lake = Prim(PrimitiveType.Cylinder, "Scenery_Lake");
            lake.transform.position = c + new Vector3(3f,0.04f, 2f);
            lake.transform.localScale = new Vector3(rad * 0.5f / 5f, 0.02f, rad * 0.5f / 5f);
            Apply(lake, waterMat);
            Destroy(lake.GetComponent<Collider>());

            // 부두/데크
            GameObject deck = Prim(PrimitiveType.Cube, "Scenery_Dock");
            deck.transform.position = c + new Vector3(-rad * 0.15f,0.2f, -rad * 0.2f);
            deck.transform.localScale = new Vector3(2f, 0.15f, 6f);
            Apply(deck, deckMat);
            Destroy(deck.GetComponent<Collider>());
            // 부두 기둥
            for (int i = 0; i < 4; i++)
            {
                GameObject post = Prim(PrimitiveType.Cylinder, $"Scenery_DockPost_{i}");
                float z = -rad * 0.2f - 2f + i * 1.8f;
                post.transform.position = c + new Vector3(-rad * 0.15f + (i % 2 == 0 ? -0.8f : 0.8f), -0.5f, z);
                post.transform.localScale = new Vector3(0.1f, 0.7f, 0.1f);
                Apply(post, deckMat);
                Destroy(post.GetComponent<Collider>());
            }

            // 갈대 군락
            for (int i = 0; i < 20; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.2f, 0.35f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d + 3f, 0f,Mathf.Sin(a) * d + 2f);

                GameObject reed = Prim(PrimitiveType.Cylinder, $"Scenery_PondReed_{i}");
                reed.transform.position = pos + new Vector3(0f, 1f, 0f);
                reed.transform.localScale = new Vector3(0.06f, 1f, 0.06f);
                reed.transform.rotation = Quaternion.Euler(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));
                Apply(reed, reedMat);
                Destroy(reed.GetComponent<Collider>());
            }

            // 징검다리
            Material stepMat = Mat(new Color(0.5f, 0.48f, 0.42f));
            for (int i = 0; i < 6; i++)
            {
                float t = (float)i / 6f;
                Vector3 pos = c + new Vector3(3f + t * 8f,0.1f, 2f + Mathf.Sin(t * 3f) * 2f);
                GameObject step = Prim(PrimitiveType.Cylinder, $"Scenery_SteppingStone_{i}");
                step.transform.position = pos;
                step.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);
                Apply(step, stepMat);
                Destroy(step.GetComponent<Collider>());
            }
        }

        // ======= 숲: 나무 군락 + 오솔길 + 통나무 =======
        private void BuildForestTerrain(Vector3 c, float rad)
        {
            Material trunkMat = Mat(new Color(0.25f, 0.15f, 0.08f));
            Material leafMat = Mat(new Color(0.1f, 0.35f, 0.08f));
            Material darkLeafMat = Mat(new Color(0.06f, 0.22f, 0.04f));
            Material logMat = Mat(new Color(0.35f, 0.22f, 0.1f));
            Material pathMat = Mat(new Color(0.35f, 0.28f, 0.18f));
            Material mossMat = Mat(new Color(0.2f, 0.35f, 0.12f));

            // 나무 군락 — 사용자 추가 보고 "layer 층 전체가 위에 떠있어 캐릭터/곤충이 가려짐".
            // 이전 1.2~2.5 축소로도 부족 → treeH 0.8~1.5(낮은 관목)로 캐릭터 머리(2.2) 한참 아래로 강제.
            // 25개 트리가 region 전역에 흩어져 모이면 "위쪽 layer" 인상 회피.
            for (int i = 0; i < 25; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(5f, rad * 0.65f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f,Mathf.Sin(a) * d);

                float treeH = Random.Range(0.8f, 1.5f);
                GameObject trunk = Prim(PrimitiveType.Cylinder, $"Scenery_ForestTree_{i}");
                trunk.transform.position = pos + new Vector3(0f, treeH * 0.5f, 0f);
                trunk.transform.localScale = new Vector3(0.35f, treeH * 0.5f, 0.35f);
                Apply(trunk, trunkMat);

                // 잎사귀 leafS 1~1.8로 더 작게 → 잎사귀 Y 범위 약 1~2.5m로 캐릭터 머리 이하/근처.
                // ShadowsOnly 제거 유지 — 정상 렌더링이라 mesh 보이지만 작아서 카메라 시야 차단 적음.
                float leafS = Random.Range(1f, 1.8f);
                GameObject leaf = Prim(PrimitiveType.Sphere, $"Scenery_ForestLeaf_{i}");
                leaf.transform.position = pos + new Vector3(0f, treeH + leafS * 0.3f, 0f);
                leaf.transform.localScale = new Vector3(leafS, leafS * 0.7f, leafS);
                Apply(leaf, i % 3 == 0 ? darkLeafMat : leafMat);
                Destroy(leaf.GetComponent<Collider>());
            }

            // 쓰러진 통나무 (장애물 + 분위기)
            for (int i = 0; i < 4; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(8f, rad * 0.5f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d,0.25f, Mathf.Sin(a) * d);

                GameObject log = Prim(PrimitiveType.Cylinder, $"Scenery_Log_{i}");
                log.transform.position = pos;
                log.transform.localScale = new Vector3(0.25f, 2f, 0.25f);
                log.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 90f);
                Apply(log, logMat);
            }

            // 이끼 낀 바위
            for (int i = 0; i < 6; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(5f, rad * 0.6f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f,Mathf.Sin(a) * d);
                float rs = Random.Range(0.6f, 1.5f);

                GameObject rock = Prim(PrimitiveType.Sphere, $"Scenery_ForestRock_{i}");
                rock.transform.position = pos + new Vector3(0f, rs * 0.2f, 0f);
                rock.transform.localScale = new Vector3(rs * 1.5f, rs * 0.5f, rs);
                Apply(rock, mossMat);
            }

            // 오솔길
            CreateInternalPath(c + new Vector3(-rad * 0.5f, 0f, 0f), c + new Vector3(rad * 0.5f, 0f, 0f), 1.5f, pathMat, logMat);
        }

        // ======= 늪: 수렁 + 고목 + 안개 =======
        private void BuildSwampTerrain(Vector3 c, float rad)
        {
            Material mudMat = Mat(new Color(0.2f, 0.22f, 0.12f));
            Material waterMat = Mat(new Color(0.12f, 0.2f, 0.15f, 0.5f));
            SetTransparent(waterMat);
            Material deadWoodMat = Mat(new Color(0.3f, 0.22f, 0.15f));

            // 수렁 (진흙 + 물 웅덩이)
            for (int i = 0; i < 8; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(3f, rad * 0.55f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d,0.02f, Mathf.Sin(a) * d);
                float ps = Random.Range(2f, 4f);

                GameObject pool = Prim(PrimitiveType.Cylinder, $"Scenery_SwampPool_{i}");
                pool.transform.position = pos;
                pool.transform.localScale = new Vector3(ps / 5f, 0.01f, ps / 5f);
                Apply(pool, waterMat);
                Destroy(pool.GetComponent<Collider>());
            }

            // 고목 (죽은 나무)
            for (int i = 0; i < 8; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(5f, rad * 0.6f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f,Mathf.Sin(a) * d);

                GameObject trunk = Prim(PrimitiveType.Cylinder, $"Scenery_DeadTree_{i}");
                float h = Random.Range(2f, 4f);
                trunk.transform.position = pos + new Vector3(0f, h * 0.5f, 0f);
                trunk.transform.localScale = new Vector3(0.2f, h * 0.5f, 0.2f);
                trunk.transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f));
                Apply(trunk, deadWoodMat);
            }

            // 징검다리 길 (안전 경로)
            Material stepMat = Mat(new Color(0.45f, 0.4f, 0.35f));
            Vector3 pathStart = c + new Vector3(-rad * 0.4f, 0f,0f);
            Vector3 pathEnd = c + new Vector3(rad * 0.4f, 0f,0f);
            for (int i = 0; i < 10; i++)
            {
                float t = (float)i / 10f;
                Vector3 pos = Vector3.Lerp(pathStart, pathEnd, t);
                pos.z += Mathf.Sin(t * Mathf.PI * 2f) * 3f;

                GameObject step = Prim(PrimitiveType.Cylinder, $"Scenery_SwampStep_{i}");
                step.transform.position = pos + new Vector3(0f, 0.1f, 0f);
                step.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);
                Apply(step, stepMat);
                Destroy(step.GetComponent<Collider>());
            }
        }

        // ======= 산: 바위 절벽 + 계단 길 + 눈 =======
        private void BuildMountainTerrain(Vector3 c, float rad)
        {
            Material rockMat = Mat(new Color(0.45f, 0.42f, 0.38f));
            Material snowMat = Mat(new Color(0.88f, 0.9f, 0.95f));
            Material pathMat = Mat(new Color(0.42f, 0.38f, 0.32f));

            // 바위 절벽 (큰 바위)
            for (int i = 0; i < 10; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(8f, rad * 0.6f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f,Mathf.Sin(a) * d);
                float rs = Random.Range(1.5f, 4f);

                GameObject rock = Prim(PrimitiveType.Sphere, $"Scenery_MountainRock_{i}");
                rock.transform.position = pos + new Vector3(0f, rs * 0.3f, 0f);
                rock.transform.localScale = new Vector3(rs * 1.8f, rs * 0.8f, rs * 1.2f);
                Apply(rock, rockMat);
            }

            // 눈 패치
            for (int i = 0; i < 6; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(5f, rad * 0.5f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d,0.06f, Mathf.Sin(a) * d);

                GameObject snow = Prim(PrimitiveType.Plane, $"Scenery_Snow_{i}");
                snow.transform.position = pos;
                float ss = Random.Range(0.3f, 0.6f);
                snow.transform.localScale = new Vector3(ss, 1f, ss);
                Apply(snow, snowMat);
                Destroy(snow.GetComponent<Collider>());
            }

            // 계단길 (중심에서 바깥으로)
            CreateStairPath(c, c + new Vector3(rad * 0.5f, 0f, rad * 0.3f), 8, pathMat);
        }

        // ======= 꽃밭: 화단 + 아치 + 나비길 =======
        private void BuildGardenTerrain(Vector3 c, float rad)
        {
            Material hedgeMat = Mat(new Color(0.18f, 0.45f, 0.12f));
            Material archMat = Mat(new Color(0.7f, 0.7f, 0.7f));
            Material pathMat = Mat(new Color(0.6f, 0.55f, 0.45f));
            Material stoneMat = Mat(new Color(0.55f, 0.52f, 0.48f));

            // 생울타리 구역 나눔
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f;
                float d = rad * 0.35f;
                Vector3 pos = c + new Vector3(Mathf.Cos(angle) * d,0.6f, Mathf.Sin(angle) * d);

                GameObject hedge = Prim(PrimitiveType.Cube, $"Scenery_GardenHedge_{i}");
                hedge.transform.position = pos;
                hedge.transform.localScale = new Vector3(rad * 0.35f, 1.2f, 0.5f);
                hedge.transform.rotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
                Apply(hedge, hedgeMat);
            }

            // 아치 입구
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject archPillar = Prim(PrimitiveType.Cylinder, $"Scenery_Arch_{side}");
                archPillar.transform.position = c + new Vector3(side * 2f,2f, -rad * 0.35f);
                archPillar.transform.localScale = new Vector3(0.2f, 2f, 0.2f);
                Apply(archPillar, archMat);

                GameObject archTop = Prim(PrimitiveType.Cube, $"Scenery_ArchTop_{side}");
                archTop.transform.position = c + new Vector3(0f,4.2f, -rad * 0.35f);
                archTop.transform.localScale = new Vector3(5f, 0.3f, 0.5f);
                Apply(archTop, archMat);
                Destroy(archTop.GetComponent<Collider>());
            }

            // 정원 길
            CreateInternalPath(c + new Vector3(0f, 0f, -rad * 0.5f), c + new Vector3(0f, 0f, rad * 0.5f), 2f, pathMat, stoneMat);
        }

        // ======= 유적: 무너진 벽 + 기둥 + 계단 =======
        private void BuildRuinsTerrain(Vector3 c, float rad)
        {
            Material stoneMat = Mat(new Color(0.4f, 0.38f, 0.32f));
            Material darkStoneMat = Mat(new Color(0.3f, 0.28f, 0.24f));
            Material mossRock = Mat(new Color(0.3f, 0.35f, 0.25f));
            Material pathMat = Mat(new Color(0.38f, 0.35f, 0.3f));

            // 무너진 벽 — 사용자 추가 보고 "layer 층 전체 위에 떠있음". wh 0.3~0.8 잔해 수준.
            for (int i = 0; i < 6; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(8f, rad * 0.5f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f,Mathf.Sin(a) * d);

                float wh = Random.Range(0.3f, 0.8f);
                float ww = Random.Range(3f, 6f);
                GameObject wall = Prim(PrimitiveType.Cube, $"Scenery_RuinWall_{i}");
                wall.transform.position = pos + new Vector3(0f, wh * 0.5f, 0f);
                wall.transform.localScale = new Vector3(ww, wh, 0.5f);
                wall.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-5f, 5f));
                Apply(wall, i % 2 == 0 ? stoneMat : darkStoneMat);
            }

            // 기둥 (일부 무너진) — ph 0.7~1.3로 캐릭터 어깨 아래까지만. 옛 1~2도 layer 인상.
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                float d = rad * 0.3f;
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f,Mathf.Sin(a) * d);
                bool fallen = Random.value > 0.6f;

                GameObject pillar = Prim(PrimitiveType.Cylinder, $"Scenery_RuinPillar_{i}");
                if (fallen)
                {
                    pillar.transform.position = pos + new Vector3(0f, 0.3f, 0f);
                    pillar.transform.localScale = new Vector3(0.4f, 1.5f, 0.4f);
                    pillar.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 85f);
                }
                else
                {
                    float ph = Random.Range(0.7f, 1.3f);
                    pillar.transform.position = pos + new Vector3(0f, ph * 0.5f, 0f);
                    pillar.transform.localScale = new Vector3(0.4f, ph * 0.5f, 0.4f);
                }
                Apply(pillar, stoneMat);
            }

            // 유적 계단
            CreateStairPath(c + new Vector3(0f, 0f, -rad * 0.3f), c, 6, pathMat);

            // 이끼 바위
            for (int i = 0; i < 5; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(4f, rad * 0.5f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f,Mathf.Sin(a) * d);
                float rs = Random.Range(0.5f, 1.2f);

                GameObject moss = Prim(PrimitiveType.Sphere, $"Scenery_MossRock_{i}");
                moss.transform.position = pos + new Vector3(0f, rs * 0.2f, 0f);
                moss.transform.localScale = new Vector3(rs * 1.3f, rs * 0.4f, rs);
                Apply(moss, mossRock);
                Destroy(moss.GetComponent<Collider>());
            }
        }

        // ======= 공통 유틸 =======

        private void CreateInternalPath(Vector3 from, Vector3 to, float width, Material pathMat, Material edgeMat)
        {
            Vector3 dir = to - from;
            float dist = new Vector3(dir.x, 0f, dir.z).magnitude;
            if (dist < 1f) return;
            float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

            int segCount = Mathf.Max(2, Mathf.RoundToInt(dist / 8f));
            for (int i = 0; i < segCount; i++)
            {
                float t = ((float)i + 0.5f) / segCount;
                Vector3 mid = Vector3.Lerp(from, to, t);
                float segLen = dist / segCount;

                GameObject path = Prim(PrimitiveType.Plane, $"Scenery_InPath_{i}");
                path.transform.position = mid + new Vector3(0f, 0.06f, 0f);
                path.transform.rotation = Quaternion.Euler(0f, angle, 0f);
                path.transform.localScale = new Vector3(width / 10f, 1f, segLen / 10f);
                Apply(path, pathMat);
                Destroy(path.GetComponent<Collider>());
            }

            // 길 가장자리 돌
            Vector3 perp = new Vector3(-dir.normalized.z, 0f, dir.normalized.x);
            for (int i = 0; i < Mathf.RoundToInt(dist / 4f); i++)
            {
                float t = (float)i / Mathf.Max(1, Mathf.RoundToInt(dist / 4f));
                Vector3 pos = Vector3.Lerp(from, to, t);
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject stone = Prim(PrimitiveType.Sphere, $"Scenery_PathEdge_{i}_{side}");
                    float ss = Random.Range(0.15f, 0.3f);
                    stone.transform.position = pos + perp * (width * 0.55f) * side + new Vector3(0f, ss * 0.15f, 0f);
                    stone.transform.localScale = new Vector3(ss * 1.3f, ss * 0.3f, ss);
                    Apply(stone, edgeMat);
                    Destroy(stone.GetComponent<Collider>());
                }
            }
        }

        private void CreateStairPath(Vector3 from, Vector3 to, int stepCount, Material mat)
        {
            Vector3 dir = to - from;
            float yDiff = dir.y;
            for (int i = 0; i < stepCount; i++)
            {
                float t = (float)i / stepCount;
                Vector3 pos = Vector3.Lerp(from, to, t);

                GameObject step = Prim(PrimitiveType.Cube, $"Scenery_Stair_{i}");
                step.transform.position = pos + new Vector3(0f, 0.1f, 0f);
                step.transform.localScale = new Vector3(3f, 0.2f, 1.5f);
                float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                step.transform.rotation = Quaternion.Euler(0f, angle, 0f);
                Apply(step, mat);
                Destroy(step.GetComponent<Collider>());
            }
        }

        private GameObject Prim(PrimitiveType type, string name)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            return obj;
        }

        private void Apply(GameObject obj, Material mat)
        {
            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = mat;
        }

        private Material Mat(Color color)
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
