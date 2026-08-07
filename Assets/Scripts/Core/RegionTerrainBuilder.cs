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
        /// <summary>
        /// 지형 배치 난수의 고정 시드. <b>월드가 실행마다 같은 모양이 되게 한다.</b>
        ///
        /// 이 클래스는 <c>Random.Range</c>를 87번 부르는데 시드가 없으면 Unity 전역 난수의
        /// 시작 상태가 실행마다 달라, 나무·통나무·바위·죽은나무·생울타리·아치기둥·폐허 벽/기둥
        /// <b>9종이 매번 다른 자리에 선다</b>. 그 9종은 collider를 남기는 것들이라
        /// (<c>PlayerMovement.IsBlockedPosition</c>의 OverlapSphere가 막는다) 단순한 장식이 아니라
        /// <b>지나갈 수 있는 길 자체가 실행마다 바뀐다</b>. 어제 걷던 길이 오늘 막히고,
        /// 그런 종류의 버그는 재시작하면 사라져 재현조차 되지 않는다.
        /// </summary>
        private const int TerrainLayoutSeed = 20260807;

        public void BuildAllRegions(RegionData[] regions)
        {
            // 배치 난수만 시드로 가두고 끝나면 되돌린다. **전역 상태를 복원하지 않으면**
            // 스폰·IV·포획 판정까지 결정론이 되어 훨씬 나쁜 문제가 된다.
            // 빌드 도중 예외가 나도 반드시 되돌아가도록 finally에 둔다.
            Random.State prevRandomState = Random.state;
            Random.InitState(TerrainLayoutSeed);
            try
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
                        // ── 2막(ver2) ── 여기 case가 빠지면 지형이 안 그려져 평지로 뜬다.
                        case "hollow": BuildHollowTerrain(c, rad); break;
                        case "dunes": BuildDunesTerrain(c, rad); break;
                        case "frostline": BuildFrostlineTerrain(c, rad); break;
                        case "emberfall": BuildEmberfallTerrain(c, rad); break;
                        case "canopy": BuildCanopyTerrain(c, rad); break;
                        case "nameless": BuildNamelessTerrain(c, rad); break;
                    }
                }
                // 옛은 리전 경계가 빈 공간이라 어디서든 자유 이동 가능 + "네모 박스" 인상.
                // 각 리전 외곽에 fence + 인접 리전으로 향하는 gateway(좁은 통로) 생성.
                BuildBoundaries(regions);
            }
            finally
            {
                Random.state = prevRandomState;
            }
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

            // 완만한 언덕 (걸어 올라갈 수 있는 경사) — 면적 비례 개수 (설계 반경 50)
            int hillCount = ScaleCount(5, rad, 50f);
            for (int i = 0; i < hillCount; i++)
            {
                float a = i * Mathf.PI * 2f / hillCount + 0.3f;
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

            // 랜덤 소품의 본 마을 부지(서남서 0.45R, 반경 18m) 회피 기준 — 상수는 VillageBuilder가 단일 출처
            Vector3 villageCenter = VillageBuilder.GetMainVillageCenter(c, rad);
            float villageAvoidSq = VillageBuilder.MainVillageFootprintRadius * VillageBuilder.MainVillageFootprintRadius;

            // 건초더미 (원기둥 몸통 + 구 지붕) — 신규 장식
            Material hayMat = Mat(new Color(0.78f, 0.68f, 0.35f));
            int hayCount = ScaleCount(3, rad, 50f);
            for (int i = 0; i < hayCount; i++)
            {
                Vector3 pos = RandomSpotAvoiding(c, rad, villageCenter, villageAvoidSq);

                GameObject body = Prim(PrimitiveType.Cylinder, $"Scenery_MeadowHaystack_{i}");
                body.transform.position = pos + new Vector3(0f, 0.4f, 0f);
                body.transform.localScale = new Vector3(1.2f, 0.4f, 1.2f);
                Apply(body, hayMat);
                Destroy(body.GetComponent<Collider>());

                GameObject top = Prim(PrimitiveType.Sphere, $"Scenery_MeadowHaystackTop_{i}");
                top.transform.position = pos + new Vector3(0f, 0.9f, 0f);
                top.transform.localScale = new Vector3(1.2f, 0.7f, 1.2f);
                Apply(top, hayMat);
                Destroy(top.GetComponent<Collider>());
            }

            // 들꽃 무리 — 색색 작은 구 클러스터 (신규 장식)
            Material[] flowerMats =
            {
                Mat(new Color(0.95f, 0.4f, 0.5f)),
                Mat(new Color(0.95f, 0.85f, 0.3f)),
                Mat(new Color(0.6f, 0.5f, 0.9f)),
                Mat(new Color(0.95f, 0.95f, 0.9f))
            };
            int flowerClusterCount = ScaleCount(6, rad, 50f);
            for (int i = 0; i < flowerClusterCount; i++)
            {
                Vector3 clusterCenter = RandomSpotAvoiding(c, rad, villageCenter, villageAvoidSq);
                int blooms = Random.Range(4, 7);
                for (int j = 0; j < blooms; j++)
                {
                    Vector3 off = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                    GameObject bloom = Prim(PrimitiveType.Sphere, $"Scenery_MeadowFlower_{i}_{j}");
                    float fs = Random.Range(0.12f, 0.22f);
                    bloom.transform.position = clusterCenter + off + new Vector3(0f, fs * 0.5f + 0.05f, 0f);
                    bloom.transform.localScale = new Vector3(fs, fs, fs);
                    Apply(bloom, flowerMats[(i + j) % flowerMats.Length]);
                    Destroy(bloom.GetComponent<Collider>());
                }
            }
        }

        // 리전 내 랜덤 배치(0.2R~0.75R)에서 회피 원(마을 부지 등)을 피해 지점을 뽑는다.
        // 5회 리샘플 후에도 실패하면 회피 원 가장자리 바깥으로 밀어낸다.
        private static Vector3 RandomSpotAvoiding(Vector3 c, float rad, Vector3 avoidCenter, float avoidSq)
        {
            Vector3 pos = c;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.2f, 0.75f);
                pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                if ((pos - avoidCenter).sqrMagnitude >= avoidSq) return pos;
            }

            Vector3 away = pos - avoidCenter;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.right;
            return avoidCenter + away.normalized * (Mathf.Sqrt(avoidSq) + 2f);
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

            // 갈대 군락 — 면적 비례 개수 (설계 반경 45)
            int reedCount = ScaleCount(20, rad, 45f);
            for (int i = 0; i < reedCount; i++)
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

            // 징검다리 — 선형 소품이라 개수와 함께 span도 반경 비례로 연장 (겹침 방지)
            Material stepMat = Mat(new Color(0.5f, 0.48f, 0.42f));
            int stoneCount = ScaleCount(6, rad, 45f);
            float stoneSpan = 8f * (rad / 45f);
            for (int i = 0; i < stoneCount; i++)
            {
                float t = (float)i / stoneCount;
                Vector3 pos = c + new Vector3(3f + t * stoneSpan, 0.1f, 2f + Mathf.Sin(t * 3f) * 2f);
                GameObject step = Prim(PrimitiveType.Cylinder, $"Scenery_SteppingStone_{i}");
                step.transform.position = pos;
                step.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);
                Apply(step, stepMat);
                Destroy(step.GetComponent<Collider>());
            }

            // 수련잎 — 호수 수면 위 납작 원반 (신규 장식, 호수가 리전 중심이라 예외적으로 중심부 배치)
            Material lilyMat = Mat(new Color(0.2f, 0.55f, 0.25f));
            int lilyCount = ScaleCount(5, rad, 45f);
            float lakeR = rad * 0.05f; // Scenery_Lake 반경 (scale x = rad*0.1 = 지름)
            for (int i = 0; i < lilyCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = Random.Range(lakeR * 0.2f, lakeR * 0.85f);
                Vector3 pos = c + new Vector3(3f + Mathf.Cos(a) * d, 0.07f, 2f + Mathf.Sin(a) * d);
                GameObject pad = Prim(PrimitiveType.Cylinder, $"Scenery_LilyPad_{i}");
                pad.transform.position = pos;
                float ls = Random.Range(0.5f, 0.9f);
                pad.transform.localScale = new Vector3(ls, 0.02f, ls);
                Apply(pad, lilyMat);
                Destroy(pad.GetComponent<Collider>());
            }

            // 부들 — 가는 줄기 + 갈색 이삭 (신규 장식)
            Material cattailStemMat = Mat(new Color(0.35f, 0.45f, 0.2f));
            Material cattailHeadMat = Mat(new Color(0.4f, 0.25f, 0.12f));
            int cattailCount = ScaleCount(8, rad, 45f);
            for (int i = 0; i < cattailCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.2f, 0.75f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);

                GameObject stem = Prim(PrimitiveType.Cylinder, $"Scenery_Cattail_{i}");
                stem.transform.position = pos + new Vector3(0f, 0.8f, 0f);
                stem.transform.localScale = new Vector3(0.05f, 0.8f, 0.05f);
                stem.transform.rotation = Quaternion.Euler(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
                Apply(stem, cattailStemMat);
                Destroy(stem.GetComponent<Collider>());

                GameObject head = Prim(PrimitiveType.Cylinder, $"Scenery_CattailHead_{i}");
                head.transform.position = pos + new Vector3(0f, 1.5f, 0f);
                head.transform.localScale = new Vector3(0.12f, 0.25f, 0.12f);
                Apply(head, cattailHeadMat);
                Destroy(head.GetComponent<Collider>());
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
            // 트리가 region 전역에 흩어져 모이면 "위쪽 layer" 인상 회피.
            // 면적 비례 개수 (설계 반경 55, 기준 25그루)
            int treeCount = ScaleCount(25, rad, 55f);
            for (int i = 0; i < treeCount; i++)
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

            // 쓰러진 통나무 (장애물 + 분위기) — 면적 비례
            int logCount = ScaleCount(4, rad, 55f);
            for (int i = 0; i < logCount; i++)
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

            // 이끼 낀 바위 — 면적 비례
            int forestRockCount = ScaleCount(6, rad, 55f);
            for (int i = 0; i < forestRockCount; i++)
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

            // 버섯 링 — 갓+기둥 조합 원형 배치 (신규 장식)
            Material mushStemMat = Mat(new Color(0.85f, 0.8f, 0.7f));
            Material mushCapMat = Mat(new Color(0.7f, 0.25f, 0.2f));
            int ringCount = ScaleCount(2, rad, 55f);
            for (int i = 0; i < ringCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.2f, 0.75f);
                Vector3 ringCenter = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                int mushrooms = 6;
                float ringR = Random.Range(1.2f, 1.8f);
                for (int j = 0; j < mushrooms; j++)
                {
                    float ma = j * Mathf.PI * 2f / mushrooms;
                    Vector3 mpos = ringCenter + new Vector3(Mathf.Cos(ma) * ringR, 0f, Mathf.Sin(ma) * ringR);

                    GameObject mstem = Prim(PrimitiveType.Cylinder, $"Scenery_MushroomStem_{i}_{j}");
                    mstem.transform.position = mpos + new Vector3(0f, 0.12f, 0f);
                    mstem.transform.localScale = new Vector3(0.08f, 0.12f, 0.08f);
                    Apply(mstem, mushStemMat);
                    Destroy(mstem.GetComponent<Collider>());

                    GameObject mcap = Prim(PrimitiveType.Sphere, $"Scenery_MushroomCap_{i}_{j}");
                    mcap.transform.position = mpos + new Vector3(0f, 0.26f, 0f);
                    mcap.transform.localScale = new Vector3(0.28f, 0.16f, 0.28f);
                    Apply(mcap, mushCapMat);
                    Destroy(mcap.GetComponent<Collider>());
                }
            }

            // 그루터기 (신규 장식)
            Material stumpMat = Mat(new Color(0.4f, 0.28f, 0.15f));
            int stumpCount = ScaleCount(4, rad, 55f);
            for (int i = 0; i < stumpCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.2f, 0.75f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);

                GameObject stump = Prim(PrimitiveType.Cylinder, $"Scenery_Stump_{i}");
                float sh = Random.Range(0.25f, 0.45f);
                stump.transform.position = pos + new Vector3(0f, sh * 0.5f, 0f);
                stump.transform.localScale = new Vector3(0.5f, sh * 0.5f, 0.5f);
                Apply(stump, stumpMat);
                Destroy(stump.GetComponent<Collider>());
            }
        }

        // ======= 늪: 수렁 + 고목 + 안개 =======
        private void BuildSwampTerrain(Vector3 c, float rad)
        {
            Material mudMat = Mat(new Color(0.2f, 0.22f, 0.12f));
            Material waterMat = Mat(new Color(0.12f, 0.2f, 0.15f, 0.5f));
            SetTransparent(waterMat);
            Material deadWoodMat = Mat(new Color(0.3f, 0.22f, 0.15f));

            // 수렁 (진흙 + 물 웅덩이) — 면적 비례 개수 (설계 반경 45)
            int poolCount = ScaleCount(8, rad, 45f);
            for (int i = 0; i < poolCount; i++)
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

            // 고목 (죽은 나무) — 면적 비례
            int deadTreeCount = ScaleCount(8, rad, 45f);
            for (int i = 0; i < deadTreeCount; i++)
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
            // 시작/끝이 rad 비례라 span은 자동 연장 — 개수만 면적 비례로 보강
            int stepCount = ScaleCount(10, rad, 45f);
            for (int i = 0; i < stepCount; i++)
            {
                float t = (float)i / stepCount;
                Vector3 pos = Vector3.Lerp(pathStart, pathEnd, t);
                pos.z += Mathf.Sin(t * Mathf.PI * 2f) * 3f;

                GameObject step = Prim(PrimitiveType.Cylinder, $"Scenery_SwampStep_{i}");
                step.transform.position = pos + new Vector3(0f, 0.1f, 0f);
                step.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);
                Apply(step, stepMat);
                Destroy(step.GetComponent<Collider>());
            }

            // 맹그로브 뿌리 — 바깥으로 기울인 원기둥 다발 (신규 장식)
            Material rootMat = Mat(new Color(0.28f, 0.2f, 0.12f));
            int mangroveCount = ScaleCount(4, rad, 45f);
            for (int i = 0; i < mangroveCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.2f, 0.75f);
                Vector3 clusterCenter = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                int roots = 5;
                for (int j = 0; j < roots; j++)
                {
                    float ra = j * Mathf.PI * 2f / roots + Random.Range(-0.3f, 0.3f);
                    GameObject root = Prim(PrimitiveType.Cylinder, $"Scenery_MangroveRoot_{i}_{j}");
                    float rh = Random.Range(0.6f, 1.1f);
                    root.transform.position = clusterCenter + new Vector3(Mathf.Cos(ra) * 0.4f, rh * 0.4f, Mathf.Sin(ra) * 0.4f);
                    root.transform.localScale = new Vector3(0.08f, rh * 0.5f, 0.08f);
                    // 다발 중심에서 바깥 방향으로 기울임
                    root.transform.rotation = Quaternion.Euler(Mathf.Sin(ra) * 25f, 0f, -Mathf.Cos(ra) * 25f);
                    Apply(root, rootMat);
                    Destroy(root.GetComponent<Collider>());
                }
            }

            // 도깨비불 — 에미시브 느낌 밝은 구, 실시간 Light 컴포넌트 없음 (신규 장식)
            Material wispMat = Mat(new Color(0.55f, 0.95f, 0.7f));
            SetEmissive(wispMat, new Color(0.35f, 0.9f, 0.55f));
            int wispCount = ScaleCount(5, rad, 45f);
            for (int i = 0; i < wispCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.2f, 0.75f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, Random.Range(0.8f, 1.6f), Mathf.Sin(a) * d);
                GameObject wisp = Prim(PrimitiveType.Sphere, $"Scenery_Wisp_{i}");
                wisp.transform.position = pos;
                float ws = Random.Range(0.15f, 0.25f);
                wisp.transform.localScale = new Vector3(ws, ws, ws);
                Apply(wisp, wispMat);
                Destroy(wisp.GetComponent<Collider>());
            }
        }

        // ======= 산: 바위 절벽 + 계단 길 + 눈 =======
        private void BuildMountainTerrain(Vector3 c, float rad)
        {
            Material rockMat = Mat(new Color(0.45f, 0.42f, 0.38f));
            Material snowMat = Mat(new Color(0.88f, 0.9f, 0.95f));
            Material pathMat = Mat(new Color(0.42f, 0.38f, 0.32f));

            // 바위 절벽 (큰 바위) — 면적 비례 개수 (설계 반경 50)
            int mtRockCount = ScaleCount(10, rad, 50f);
            for (int i = 0; i < mtRockCount; i++)
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

            // 눈 패치 — 면적 비례
            int snowCount = ScaleCount(6, rad, 50f);
            for (int i = 0; i < snowCount; i++)
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

            // 침엽수 — 원기둥 tier 스택으로 원뿔 실루엣 (Unity에 Cone 프리미티브 없음, 신규 장식)
            // 총 높이 ~1.6m로 캐릭터 머리(2.2) 아래 유지 → "위쪽 layer" 인상 회피.
            Material pineLeafMat = Mat(new Color(0.1f, 0.3f, 0.15f));
            Material pineTrunkMat = Mat(new Color(0.3f, 0.2f, 0.1f));
            int pineCount = ScaleCount(6, rad, 50f);
            for (int i = 0; i < pineCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.2f, 0.75f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);

                GameObject pineTrunk = Prim(PrimitiveType.Cylinder, $"Scenery_Pine_{i}");
                pineTrunk.transform.position = pos + new Vector3(0f, 0.25f, 0f);
                pineTrunk.transform.localScale = new Vector3(0.15f, 0.25f, 0.15f);
                Apply(pineTrunk, pineTrunkMat);
                Destroy(pineTrunk.GetComponent<Collider>());

                for (int j = 0; j < 3; j++)
                {
                    GameObject tier = Prim(PrimitiveType.Cylinder, $"Scenery_PineTier_{i}_{j}");
                    float tw = 1.1f - j * 0.3f; // 위로 갈수록 지름 축소
                    tier.transform.position = pos + new Vector3(0f, 0.6f + j * 0.4f, 0f);
                    tier.transform.localScale = new Vector3(tw, 0.22f, tw);
                    Apply(tier, pineLeafMat);
                    Destroy(tier.GetComponent<Collider>());
                }
            }

            // 낙석 더미 — 크기 다른 큐브/구 무더기 (신규 장식)
            Material rubbleMat = Mat(new Color(0.5f, 0.47f, 0.43f));
            int rockfallCount = ScaleCount(4, rad, 50f);
            for (int i = 0; i < rockfallCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.2f, 0.75f);
                Vector3 pileCenter = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                int pieces = Random.Range(4, 7);
                for (int j = 0; j < pieces; j++)
                {
                    Vector3 off = new Vector3(Random.Range(-0.9f, 0.9f), 0f, Random.Range(-0.9f, 0.9f));
                    float ps = Random.Range(0.25f, 0.7f);
                    GameObject piece = Prim(j % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere, $"Scenery_Rockfall_{i}_{j}");
                    piece.transform.position = pileCenter + off + new Vector3(0f, ps * 0.35f, 0f);
                    piece.transform.localScale = new Vector3(ps, ps * 0.7f, ps);
                    piece.transform.rotation = Quaternion.Euler(Random.Range(0f, 25f), Random.Range(0f, 360f), Random.Range(0f, 25f));
                    Apply(piece, rubbleMat);
                    Destroy(piece.GetComponent<Collider>());
                }
            }
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

            // 꽃 아치 — 반원 궤적 큐브 + 꽃 구 (신규 장식, 설계 반경 40 기준 면적 비례)
            Material archFrameMat = Mat(new Color(0.75f, 0.72f, 0.68f));
            Material archBloomMat = Mat(new Color(0.9f, 0.45f, 0.6f));
            int flowerArchCount = ScaleCount(2, rad, 40f);
            for (int i = 0; i < flowerArchCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.3f, 0.7f);
                Vector3 basePos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                Quaternion yawRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                int segs = 7;
                float archR = 1.5f;
                for (int j = 0; j < segs; j++)
                {
                    float t = (float)j / (segs - 1) * Mathf.PI; // 0~180° 반원
                    Vector3 local = new Vector3(Mathf.Cos(t) * archR, Mathf.Sin(t) * archR, 0f);
                    Vector3 pos = basePos + yawRot * local + new Vector3(0f, 0.15f, 0f);

                    GameObject seg = Prim(PrimitiveType.Cube, $"Scenery_FlowerArch_{i}_{j}");
                    seg.transform.position = pos;
                    seg.transform.localScale = new Vector3(0.18f, 0.55f, 0.18f);
                    seg.transform.rotation = yawRot * Quaternion.Euler(0f, 0f, t * Mathf.Rad2Deg); // 호 접선 방향
                    Apply(seg, archFrameMat);
                    Destroy(seg.GetComponent<Collider>());

                    if (j % 2 == 1)
                    {
                        GameObject archBloom = Prim(PrimitiveType.Sphere, $"Scenery_FlowerArchBloom_{i}_{j}");
                        archBloom.transform.position = pos + new Vector3(0f, 0.15f, 0f);
                        archBloom.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
                        Apply(archBloom, archBloomMat);
                        Destroy(archBloom.GetComponent<Collider>());
                    }
                }
            }

            // 화단 상자 — 테두리 큐브 + 흙 + 꽃 구 (신규 장식)
            Material bedFrameMat = Mat(new Color(0.5f, 0.35f, 0.2f));
            Material bedSoilMat = Mat(new Color(0.32f, 0.24f, 0.16f));
            Material[] bedBloomMats =
            {
                Mat(new Color(0.95f, 0.5f, 0.55f)),
                Mat(new Color(0.95f, 0.9f, 0.35f)),
                Mat(new Color(0.95f, 0.95f, 0.95f))
            };
            int bedCount = ScaleCount(3, rad, 40f);
            for (int i = 0; i < bedCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.2f, 0.75f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                Quaternion bedRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                GameObject soil = Prim(PrimitiveType.Cube, $"Scenery_FlowerBed_{i}");
                soil.transform.position = pos + new Vector3(0f, 0.12f, 0f);
                soil.transform.localScale = new Vector3(2.4f, 0.24f, 1.2f);
                soil.transform.rotation = bedRot;
                Apply(soil, bedSoilMat);
                Destroy(soil.GetComponent<Collider>());

                for (int side = 0; side < 4; side++)
                {
                    GameObject edge = Prim(PrimitiveType.Cube, $"Scenery_FlowerBedEdge_{i}_{side}");
                    bool longSide = side < 2;
                    Vector3 local = longSide
                        ? new Vector3(0f, 0.18f, side == 0 ? 0.66f : -0.66f)
                        : new Vector3(side == 2 ? 1.26f : -1.26f, 0.18f, 0f);
                    edge.transform.position = pos + bedRot * local;
                    edge.transform.localScale = longSide
                        ? new Vector3(2.64f, 0.36f, 0.12f)
                        : new Vector3(0.12f, 0.36f, 1.2f);
                    edge.transform.rotation = bedRot;
                    Apply(edge, bedFrameMat);
                    Destroy(edge.GetComponent<Collider>());
                }

                for (int j = 0; j < 6; j++)
                {
                    Vector3 local = new Vector3(Random.Range(-1f, 1f), 0.3f, Random.Range(-0.4f, 0.4f));
                    GameObject bedBloom = Prim(PrimitiveType.Sphere, $"Scenery_FlowerBedBloom_{i}_{j}");
                    float fs = Random.Range(0.14f, 0.22f);
                    bedBloom.transform.position = pos + bedRot * local;
                    bedBloom.transform.localScale = new Vector3(fs, fs, fs);
                    Apply(bedBloom, bedBloomMats[(i + j) % bedBloomMats.Length]);
                    Destroy(bedBloom.GetComponent<Collider>());
                }
            }
        }

        // ======= 유적: 무너진 벽 + 기둥 + 계단 =======
        private void BuildRuinsTerrain(Vector3 c, float rad)
        {
            Material stoneMat = Mat(new Color(0.4f, 0.38f, 0.32f));
            Material darkStoneMat = Mat(new Color(0.3f, 0.28f, 0.24f));
            Material mossRock = Mat(new Color(0.3f, 0.35f, 0.25f));
            Material pathMat = Mat(new Color(0.38f, 0.35f, 0.3f));

            // 무너진 벽 — 사용자 추가 보고 "layer 층 전체 위에 떠있음". wh 0.3~0.8 잔해 수준.
            // 면적 비례 개수 (설계 반경 45)
            int wallCount = ScaleCount(6, rad, 45f);
            for (int i = 0; i < wallCount; i++)
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
            // 원형 배치라 개수 늘려도 각도 균등 분배로 자연 분포
            int pillarCount = ScaleCount(8, rad, 45f);
            for (int i = 0; i < pillarCount; i++)
            {
                float a = i * Mathf.PI * 2f / pillarCount;
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

            // 이끼 바위 — 면적 비례
            int mossRockCount = ScaleCount(5, rad, 45f);
            for (int i = 0; i < mossRockCount; i++)
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

            // 부서진 오벨리스크 — 기울어진 큐브 스택 (신규 장식, 총 높이 ~1.35m로 layer 인상 회피)
            Material obeliskMat = Mat(new Color(0.42f, 0.4f, 0.36f));
            int obeliskCount = ScaleCount(3, rad, 45f);
            for (int i = 0; i < obeliskCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.2f, 0.75f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float lean = Random.Range(4f, 12f);
                float leanDir = Random.Range(0f, 360f);
                for (int j = 0; j < 3; j++)
                {
                    GameObject block = Prim(PrimitiveType.Cube, $"Scenery_Obelisk_{i}_{j}");
                    float bw = 0.8f - j * 0.2f; // 위로 갈수록 좁아짐
                    const float bh = 0.45f;
                    // 위 tier일수록 lean 방향으로 밀려 기운 실루엣
                    Vector3 leanOff = Quaternion.Euler(0f, leanDir, 0f)
                        * new Vector3(Mathf.Tan(lean * Mathf.Deg2Rad) * bh * j, 0f, 0f);
                    block.transform.position = pos + leanOff + new Vector3(0f, bh * 0.5f + j * bh, 0f);
                    block.transform.localScale = new Vector3(bw, bh, bw);
                    block.transform.rotation = Quaternion.Euler(0f, leanDir, -lean);
                    Apply(block, obeliskMat);
                    Destroy(block.GetComponent<Collider>());
                }
            }

            // 이끼 낀 석상 — 큐브 대좌 + 구 몸통/머리 추상 조형 (신규 장식)
            Material statueMat = Mat(new Color(0.45f, 0.43f, 0.38f));
            Material statueMossMat = Mat(new Color(0.32f, 0.42f, 0.25f));
            int statueCount = ScaleCount(2, rad, 45f);
            for (int i = 0; i < statueCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.25f, 0.7f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);

                GameObject pedestal = Prim(PrimitiveType.Cube, $"Scenery_Statue_{i}");
                pedestal.transform.position = pos + new Vector3(0f, 0.25f, 0f);
                pedestal.transform.localScale = new Vector3(0.9f, 0.5f, 0.9f);
                Apply(pedestal, statueMat);
                Destroy(pedestal.GetComponent<Collider>());

                GameObject body = Prim(PrimitiveType.Sphere, $"Scenery_StatueBody_{i}");
                body.transform.position = pos + new Vector3(0f, 0.85f, 0f);
                body.transform.localScale = new Vector3(0.55f, 0.7f, 0.55f);
                Apply(body, i % 2 == 0 ? statueMossMat : statueMat);
                Destroy(body.GetComponent<Collider>());

                GameObject head = Prim(PrimitiveType.Sphere, $"Scenery_StatueHead_{i}");
                head.transform.position = pos + new Vector3(0f, 1.35f, 0f);
                head.transform.localScale = new Vector3(0.32f, 0.32f, 0.32f);
                Apply(head, statueMossMat);
                Destroy(head.GetComponent<Collider>());
            }
        }

        // ======= 텅 빈 들: 색이 빠진 폐허 초원 + 쓰러진 울타리 + 빈 표석 =======
        // 초원(BuildMeadowTerrain)과 같은 소품군을 쓰되 채도를 걷어내고 개수를 줄인다 —
        // "다른 땅"이 아니라 "초원이 죽은 모습"이어야 한다.
        private void BuildHollowTerrain(Vector3 c, float rad)
        {
            Material dirtMat = Mat(new Color(0.46f, 0.44f, 0.38f));
            Material deadWoodMat = Mat(new Color(0.38f, 0.34f, 0.30f));
            Material paleGrassMat = Mat(new Color(0.52f, 0.54f, 0.44f));
            Material slabMat = Mat(new Color(0.58f, 0.57f, 0.55f));

            // 낮게 눌린 마른 언덕 — 초원의 절반 높이(hs * 0.5f → 0.28f)
            int hillCount = ScaleCount(4, rad, 45f);
            for (int i = 0; i < hillCount; i++)
            {
                float a = i * Mathf.PI * 2f / hillCount + 0.6f;
                float d = rad * Random.Range(0.25f, 0.6f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float hs = Random.Range(4f, 7f);

                GameObject hill = Prim(PrimitiveType.Sphere, $"Scenery_HollowRidge_{i}");
                hill.transform.position = pos + new Vector3(0f, -hs * 0.3f, 0f);
                hill.transform.localScale = new Vector3(hs * 2.2f, hs * 0.28f, hs * 2.2f);
                Apply(hill, dirtMat);
                Destroy(hill.GetComponent<Collider>());
            }

            // 죽은 그루터기 — 가지 없는 몸통만
            int stumpCount = ScaleCount(7, rad, 45f);
            for (int i = 0; i < stumpCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.2f, 0.8f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float h = Random.Range(0.7f, 1.6f);

                GameObject stump = Prim(PrimitiveType.Cylinder, $"Scenery_HollowStump_{i}");
                stump.transform.position = pos + new Vector3(0f, h * 0.5f, 0f);
                stump.transform.localScale = new Vector3(0.5f, h * 0.5f, 0.5f);
                stump.transform.rotation = Quaternion.Euler(Random.Range(-9f, 9f), 0f, Random.Range(-9f, 9f));
                Apply(stump, deadWoodMat);
                Destroy(stump.GetComponent<Collider>());
            }

            // 쓰러진 울타리 — 눕힌 각재 2~3개가 한 무리
            int fenceCount = ScaleCount(5, rad, 45f);
            for (int i = 0; i < fenceCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.3f, 0.75f);
                Vector3 spot = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float yaw = Random.Range(0f, 180f);
                int rails = Random.Range(2, 4);
                for (int j = 0; j < rails; j++)
                {
                    GameObject rail = Prim(PrimitiveType.Cube, $"Scenery_HollowFence_{i}_{j}");
                    rail.transform.position = spot + new Vector3(Random.Range(-1.2f, 1.2f), 0.12f, Random.Range(-1.2f, 1.2f));
                    rail.transform.localScale = new Vector3(2.6f, 0.16f, 0.28f);
                    rail.transform.rotation = Quaternion.Euler(0f, yaw + Random.Range(-25f, 25f), Random.Range(-6f, 6f));
                    Apply(rail, deadWoodMat);
                    Destroy(rail.GetComponent<Collider>());
                }
            }

            // 빛바랜 풀 무리 — 초원 들꽃의 자리에 색 없는 낮은 덩어리만 남는다
            int tuftCount = ScaleCount(8, rad, 45f);
            for (int i = 0; i < tuftCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.15f, 0.8f);
                Vector3 clusterCenter = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                int blades = Random.Range(3, 6);
                for (int j = 0; j < blades; j++)
                {
                    GameObject tuft = Prim(PrimitiveType.Sphere, $"Scenery_HollowTuft_{i}_{j}");
                    float ts = Random.Range(0.25f, 0.45f);
                    tuft.transform.position = clusterCenter
                        + new Vector3(Random.Range(-1f, 1f), ts * 0.3f, Random.Range(-1f, 1f));
                    tuft.transform.localScale = new Vector3(ts, ts * 0.55f, ts);
                    Apply(tuft, paleGrassMat);
                    Destroy(tuft.GetComponent<Collider>());
                }
            }

            // 빈 표석 — 아무것도 새겨지지 않은 판석. 이 리전의 상징물(빈칸)
            int slabCount = ScaleCount(3, rad, 45f);
            for (int i = 0; i < slabCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.3f, 0.65f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);

                GameObject slab = Prim(PrimitiveType.Cube, $"Scenery_HollowSlab_{i}");
                slab.transform.position = pos + new Vector3(0f, 0.75f, 0f);
                slab.transform.localScale = new Vector3(1.1f, 1.5f, 0.22f);
                slab.transform.rotation = Quaternion.Euler(Random.Range(-7f, 7f), Random.Range(0f, 360f), Random.Range(-5f, 5f));
                Apply(slab, slabMat);
                Destroy(slab.GetComponent<Collider>());
            }
        }

        // ======= 모래언덕: 사구 능선 + 바위 노두 + 마른 관목 + 명부회 화물 =======
        private void BuildDunesTerrain(Vector3 c, float rad)
        {
            Material sandMat = Mat(new Color(0.86f, 0.74f, 0.46f));
            Material sandShadeMat = Mat(new Color(0.72f, 0.60f, 0.36f));
            Material rockMat = Mat(new Color(0.60f, 0.50f, 0.40f));
            Material shrubMat = Mat(new Color(0.48f, 0.46f, 0.30f));
            Material crateMat = Mat(new Color(0.55f, 0.42f, 0.26f));

            // 사구 능선 — 길게 늘인 낮은 구를 같은 방향으로 눕혀 바람 결을 만든다
            int duneCount = ScaleCount(6, rad, 48f);
            for (int i = 0; i < duneCount; i++)
            {
                float a = i * Mathf.PI * 2f / duneCount + 0.2f;
                float d = rad * Random.Range(0.2f, 0.65f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float ds = Random.Range(6f, 11f);

                GameObject dune = Prim(PrimitiveType.Sphere, $"Scenery_Dune_{i}");
                dune.transform.position = pos + new Vector3(0f, -ds * 0.32f, 0f);
                dune.transform.localScale = new Vector3(ds * 3f, ds * 0.42f, ds * 1.4f);
                dune.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
                Apply(dune, i % 3 == 0 ? sandShadeMat : sandMat);
                Destroy(dune.GetComponent<Collider>());
            }

            // 바위 노두 — 모래 위로 솟은 각진 덩어리
            int rockCount = ScaleCount(6, rad, 48f);
            for (int i = 0; i < rockCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.25f, 0.8f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float rs = Random.Range(1.4f, 3.2f);

                GameObject rock = Prim(PrimitiveType.Cube, $"Scenery_DuneRock_{i}");
                rock.transform.position = pos + new Vector3(0f, rs * 0.35f, 0f);
                rock.transform.localScale = new Vector3(rs * 1.4f, rs * 0.8f, rs);
                rock.transform.rotation = Quaternion.Euler(Random.Range(-12f, 12f), Random.Range(0f, 360f), Random.Range(-12f, 12f));
                Apply(rock, rockMat);
                Destroy(rock.GetComponent<Collider>());
            }

            // 마른 관목 — 가느다란 가지 몇 개가 한 포기
            int shrubCount = ScaleCount(8, rad, 48f);
            for (int i = 0; i < shrubCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.2f, 0.8f);
                Vector3 spot = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                int twigs = Random.Range(3, 6);
                for (int j = 0; j < twigs; j++)
                {
                    GameObject twig = Prim(PrimitiveType.Cylinder, $"Scenery_DuneShrub_{i}_{j}");
                    float th = Random.Range(0.35f, 0.75f);
                    twig.transform.position = spot + new Vector3(Random.Range(-0.4f, 0.4f), th * 0.5f, Random.Range(-0.4f, 0.4f));
                    twig.transform.localScale = new Vector3(0.07f, th * 0.5f, 0.07f);
                    twig.transform.rotation = Quaternion.Euler(Random.Range(-28f, 28f), Random.Range(0f, 360f), Random.Range(-28f, 28f));
                    Apply(twig, shrubMat);
                    Destroy(twig.GetComponent<Collider>());
                }
            }

            // 반쯤 묻힌 화물 상자 — 명부회가 곤충을 실어 나른 자국. 이 리전의 서사 소품
            int crateCount = ScaleCount(4, rad, 48f);
            for (int i = 0; i < crateCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.3f, 0.7f);
                Vector3 spot = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                int stack = Random.Range(2, 4);
                float yaw = Random.Range(0f, 360f);
                for (int j = 0; j < stack; j++)
                {
                    GameObject crate = Prim(PrimitiveType.Cube, $"Scenery_DuneCrate_{i}_{j}");
                    // 아래 상자일수록 모래에 깊이 잠긴다(y가 음수에서 올라온다)
                    crate.transform.position = spot
                        + new Vector3(Random.Range(-0.5f, 0.5f), -0.35f + j * 0.85f, Random.Range(-0.5f, 0.5f));
                    crate.transform.localScale = new Vector3(1.1f, 0.8f, 1.1f);
                    crate.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), yaw + Random.Range(-20f, 20f), Random.Range(-8f, 8f));
                    Apply(crate, crateMat);
                    Destroy(crate.GetComponent<Collider>());
                }
            }
        }

        // ======= 서릿길: 눈 언덕 + 얼음 기둥 + 언 나무 =======
        private void BuildFrostlineTerrain(Vector3 c, float rad)
        {
            Material snowMat = Mat(new Color(0.90f, 0.93f, 0.96f));
            Material iceMat = Mat(new Color(0.66f, 0.84f, 0.92f, 0.72f));
            SetTransparent(iceMat);
            Material rockMat = Mat(new Color(0.52f, 0.56f, 0.60f));
            Material frozenWoodMat = Mat(new Color(0.44f, 0.48f, 0.52f));

            // 눈 언덕 — 초원 언덕과 같은 형태를 흰색으로. 완만해 걸어 오를 수 있다
            int driftCount = ScaleCount(5, rad, 45f);
            for (int i = 0; i < driftCount; i++)
            {
                float a = i * Mathf.PI * 2f / driftCount + 0.4f;
                float d = rad * Random.Range(0.22f, 0.6f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float ds = Random.Range(5f, 9f);

                GameObject drift = Prim(PrimitiveType.Sphere, $"Scenery_SnowDrift_{i}");
                drift.transform.position = pos + new Vector3(0f, -ds * 0.28f, 0f);
                drift.transform.localScale = new Vector3(ds * 2.4f, ds * 0.45f, ds * 2f);
                Apply(drift, snowMat);
                Destroy(drift.GetComponent<Collider>());
            }

            // 얼음 기둥 — 위로 갈수록 가늘어지는 반투명 기둥. 이 리전의 상징물(얼어붙은 기록)
            int pillarCount = ScaleCount(7, rad, 45f);
            for (int i = 0; i < pillarCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.2f, 0.78f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float h = Random.Range(2.2f, 4.6f);

                GameObject shaft = Prim(PrimitiveType.Cylinder, $"Scenery_IcePillar_{i}");
                shaft.transform.position = pos + new Vector3(0f, h * 0.5f, 0f);
                shaft.transform.localScale = new Vector3(0.8f, h * 0.5f, 0.8f);
                shaft.transform.rotation = Quaternion.Euler(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
                Apply(shaft, iceMat);
                Destroy(shaft.GetComponent<Collider>());

                GameObject cap = Prim(PrimitiveType.Sphere, $"Scenery_IcePillarTip_{i}");
                cap.transform.position = pos + new Vector3(0f, h + 0.15f, 0f);
                cap.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
                Apply(cap, iceMat);
                Destroy(cap.GetComponent<Collider>());
            }

            // 언 나무 — 잎 없이 몸통과 가지 두어 개만
            int treeCount = ScaleCount(6, rad, 45f);
            for (int i = 0; i < treeCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.25f, 0.8f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float h = Random.Range(2.4f, 4f);

                GameObject trunk = Prim(PrimitiveType.Cylinder, $"Scenery_FrozenTree_{i}");
                trunk.transform.position = pos + new Vector3(0f, h * 0.5f, 0f);
                trunk.transform.localScale = new Vector3(0.32f, h * 0.5f, 0.32f);
                Apply(trunk, frozenWoodMat);
                Destroy(trunk.GetComponent<Collider>());

                int branches = Random.Range(2, 4);
                for (int j = 0; j < branches; j++)
                {
                    GameObject branch = Prim(PrimitiveType.Cylinder, $"Scenery_FrozenBranch_{i}_{j}");
                    branch.transform.position = pos + new Vector3(0f, h * Random.Range(0.55f, 0.9f), 0f);
                    branch.transform.localScale = new Vector3(0.14f, Random.Range(0.5f, 0.9f), 0.14f);
                    branch.transform.rotation = Quaternion.Euler(Random.Range(55f, 82f), Random.Range(0f, 360f), 0f);
                    Apply(branch, frozenWoodMat);
                    Destroy(branch.GetComponent<Collider>());
                }
            }

            // 눈 덮인 바위 — 각진 덩어리 위에 흰 뚜껑
            int rockCount = ScaleCount(5, rad, 45f);
            for (int i = 0; i < rockCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.2f, 0.75f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float rs = Random.Range(1.2f, 2.6f);

                GameObject rock = Prim(PrimitiveType.Cube, $"Scenery_FrostRock_{i}");
                rock.transform.position = pos + new Vector3(0f, rs * 0.35f, 0f);
                rock.transform.localScale = new Vector3(rs * 1.3f, rs * 0.7f, rs);
                rock.transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
                Apply(rock, rockMat);
                Destroy(rock.GetComponent<Collider>());

                GameObject cap = Prim(PrimitiveType.Sphere, $"Scenery_FrostRockCap_{i}");
                cap.transform.position = pos + new Vector3(0f, rs * 0.68f, 0f);
                cap.transform.localScale = new Vector3(rs * 1.25f, rs * 0.28f, rs * 0.95f);
                Apply(cap, snowMat);
                Destroy(cap.GetComponent<Collider>());
            }
        }

        // ======= 잿불 골짜기: 굳은 용암 바위 + 재 더미 + 불탄 나무 =======
        private void BuildEmberfallTerrain(Vector3 c, float rad)
        {
            Material basaltMat = Mat(new Color(0.20f, 0.18f, 0.18f));
            Material ashMat = Mat(new Color(0.44f, 0.41f, 0.39f));
            Material charMat = Mat(new Color(0.16f, 0.13f, 0.12f));
            Material emberMat = Mat(new Color(0.85f, 0.34f, 0.14f));
            SetEmissive(emberMat, new Color(0.85f, 0.30f, 0.08f));

            // 굳은 용암 지대 — 낮고 넓은 검은 판
            int flowCount = ScaleCount(5, rad, 48f);
            for (int i = 0; i < flowCount; i++)
            {
                float a = i * Mathf.PI * 2f / flowCount + 0.5f;
                float d = rad * Random.Range(0.2f, 0.6f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float fs = Random.Range(6f, 10f);

                GameObject flow = Prim(PrimitiveType.Sphere, $"Scenery_LavaFlow_{i}");
                flow.transform.position = pos + new Vector3(0f, -fs * 0.34f, 0f);
                flow.transform.localScale = new Vector3(fs * 2.6f, fs * 0.38f, fs * 1.8f);
                flow.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                Apply(flow, basaltMat);
                Destroy(flow.GetComponent<Collider>());
            }

            // 잿불 틈 — 굳은 용암 사이로 보이는 자체 발광 이음매
            int seamCount = ScaleCount(6, rad, 48f);
            for (int i = 0; i < seamCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.2f, 0.7f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0.04f, Mathf.Sin(a) * d);

                GameObject seam = Prim(PrimitiveType.Cube, $"Scenery_EmberSeam_{i}");
                seam.transform.position = pos;
                seam.transform.localScale = new Vector3(Random.Range(2.5f, 5.5f), 0.06f, Random.Range(0.2f, 0.45f));
                seam.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                Apply(seam, emberMat);
                Destroy(seam.GetComponent<Collider>());
            }

            // 재 더미 — 회색 원뿔 대용(눌린 구)
            int moundCount = ScaleCount(7, rad, 48f);
            for (int i = 0; i < moundCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.25f, 0.8f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float ms = Random.Range(1.2f, 2.4f);

                GameObject mound = Prim(PrimitiveType.Sphere, $"Scenery_AshMound_{i}");
                mound.transform.position = pos + new Vector3(0f, ms * 0.18f, 0f);
                mound.transform.localScale = new Vector3(ms * 1.8f, ms * 0.6f, ms * 1.8f);
                Apply(mound, ashMat);
                Destroy(mound.GetComponent<Collider>());
            }

            // 불탄 나무 — 위가 부러진 검은 몸통. 가지는 없다
            int snagCount = ScaleCount(8, rad, 48f);
            for (int i = 0; i < snagCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.2f, 0.82f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float h = Random.Range(1.6f, 3.8f);

                GameObject snag = Prim(PrimitiveType.Cylinder, $"Scenery_BurntSnag_{i}");
                snag.transform.position = pos + new Vector3(0f, h * 0.5f, 0f);
                snag.transform.localScale = new Vector3(0.34f, h * 0.5f, 0.34f);
                snag.transform.rotation = Quaternion.Euler(Random.Range(-11f, 11f), 0f, Random.Range(-11f, 11f));
                Apply(snag, charMat);
                Destroy(snag.GetComponent<Collider>());
            }
        }

        // ======= 우듬지: 거대수 기둥 + 잎 덮개 + 공중 뿌리 =======
        // 2막에서 유일하게 "살아 있는" 리전 — 채도와 소품 밀도를 다른 2막 리전보다 높게 둔다.
        private void BuildCanopyTerrain(Vector3 c, float rad)
        {
            Material barkMat = Mat(new Color(0.36f, 0.28f, 0.20f));
            Material leafMat = Mat(new Color(0.24f, 0.58f, 0.28f));
            Material leafLightMat = Mat(new Color(0.42f, 0.74f, 0.36f));
            Material mossMat = Mat(new Color(0.32f, 0.48f, 0.26f));

            // 거대수 기둥 — 아주 굵은 원기둥. 이 리전의 뼈대
            int trunkCount = ScaleCount(5, rad, 50f);
            for (int i = 0; i < trunkCount; i++)
            {
                float a = i * Mathf.PI * 2f / trunkCount + 0.35f;
                float d = rad * Random.Range(0.25f, 0.7f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float h = Random.Range(9f, 15f);
                float w = Random.Range(2.2f, 3.4f);

                GameObject trunk = Prim(PrimitiveType.Cylinder, $"Scenery_GreatTrunk_{i}");
                trunk.transform.position = pos + new Vector3(0f, h * 0.5f, 0f);
                trunk.transform.localScale = new Vector3(w, h * 0.5f, w);
                Apply(trunk, barkMat);
                Destroy(trunk.GetComponent<Collider>());

                // 잎 덮개 — 위로 갈수록 작아지는 구 3겹
                for (int j = 0; j < 3; j++)
                {
                    GameObject crown = Prim(PrimitiveType.Sphere, $"Scenery_GreatCrown_{i}_{j}");
                    float cs = (w * 3.6f) * (1f - j * 0.22f);
                    crown.transform.position = pos + new Vector3(
                        Random.Range(-0.8f, 0.8f), h * (0.78f + j * 0.14f), Random.Range(-0.8f, 0.8f));
                    crown.transform.localScale = new Vector3(cs, cs * 0.6f, cs);
                    Apply(crown, j == 2 ? leafLightMat : leafMat);
                    Destroy(crown.GetComponent<Collider>());
                }

                // 공중 뿌리 — 기둥에서 비스듬히 땅으로 내려오는 가는 기둥
                int roots = Random.Range(3, 6);
                for (int j = 0; j < roots; j++)
                {
                    GameObject root = Prim(PrimitiveType.Cylinder, $"Scenery_AerialRoot_{i}_{j}");
                    float rh = Random.Range(2.5f, 4.5f);
                    float ra = Random.Range(0f, Mathf.PI * 2f);
                    root.transform.position = pos + new Vector3(
                        Mathf.Cos(ra) * w * 1.1f, rh * 0.5f, Mathf.Sin(ra) * w * 1.1f);
                    root.transform.localScale = new Vector3(0.16f, rh * 0.5f, 0.16f);
                    root.transform.rotation = Quaternion.Euler(Random.Range(-18f, 18f), 0f, Random.Range(-18f, 18f));
                    Apply(root, barkMat);
                    Destroy(root.GetComponent<Collider>());
                }
            }

            // 이끼 둔덕 — 바닥을 초록으로 채워 "살아 있는 땅"임을 알린다
            int mossCount = ScaleCount(9, rad, 50f);
            for (int i = 0; i < mossCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.15f, 0.82f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float ms = Random.Range(1.4f, 3f);

                GameObject mound = Prim(PrimitiveType.Sphere, $"Scenery_MossMound_{i}");
                mound.transform.position = pos + new Vector3(0f, ms * 0.12f, 0f);
                mound.transform.localScale = new Vector3(ms * 2f, ms * 0.42f, ms * 2f);
                Apply(mound, mossMat);
                Destroy(mound.GetComponent<Collider>());
            }
        }

        // ======= 이름 없는 자리: 빈 석판 원형 배치 + 색이 빠진 땅 =======
        // 소품을 일부러 적게 둔다 — 채워지지 않은 자리가 이 리전의 주제다.
        private void BuildNamelessTerrain(Vector3 c, float rad)
        {
            Material voidGroundMat = Mat(new Color(0.30f, 0.29f, 0.33f));
            Material slabMat = Mat(new Color(0.46f, 0.45f, 0.50f));
            Material palePillarMat = Mat(new Color(0.58f, 0.57f, 0.62f));

            // 색이 빠진 지반 — 넓고 아주 낮은 판 몇 장
            int plateCount = ScaleCount(4, rad, 42f);
            for (int i = 0; i < plateCount; i++)
            {
                float a = i * Mathf.PI * 2f / plateCount;
                float d = rad * Random.Range(0.2f, 0.55f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float ps = Random.Range(7f, 11f);

                GameObject plate = Prim(PrimitiveType.Sphere, $"Scenery_VoidPlate_{i}");
                plate.transform.position = pos + new Vector3(0f, -ps * 0.36f, 0f);
                plate.transform.localScale = new Vector3(ps * 2.6f, ps * 0.4f, ps * 2.4f);
                Apply(plate, voidGroundMat);
                Destroy(plate.GetComponent<Collider>());
            }

            // 빈 석판 원형 배치 — 유적 신전 벽의 이곳 판본. 전부 아무것도 새겨져 있지 않다
            int ringCount = 12;
            float ringRadius = rad * 0.42f;
            for (int i = 0; i < ringCount; i++)
            {
                float a = i * Mathf.PI * 2f / ringCount;
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * ringRadius, 0f, Mathf.Sin(a) * ringRadius);
                float h = Random.Range(2.4f, 3.6f);

                GameObject slab = Prim(PrimitiveType.Cube, $"Scenery_BlankSlab_{i}");
                slab.transform.position = pos + new Vector3(0f, h * 0.5f, 0f);
                slab.transform.localScale = new Vector3(1.5f, h, 0.3f);
                // 원 중심을 바라보게 세운다 — 무언가를 둘러싸고 있던 배치
                slab.transform.rotation = Quaternion.Euler(
                    Random.Range(-4f, 4f), -a * Mathf.Rad2Deg, Random.Range(-3f, 3f));
                Apply(slab, slabMat);
                Destroy(slab.GetComponent<Collider>());
            }

            // 창백한 기둥 — 원 안쪽에 드문드문. 무엇을 받치던 것인지 알 수 없다
            int pillarCount = ScaleCount(3, rad, 42f);
            for (int i = 0; i < pillarCount; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float d = rad * Random.Range(0.1f, 0.3f);
                Vector3 pos = c + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                float h = Random.Range(4f, 6.5f);

                GameObject pillar = Prim(PrimitiveType.Cylinder, $"Scenery_PalePillar_{i}");
                pillar.transform.position = pos + new Vector3(0f, h * 0.5f, 0f);
                pillar.transform.localScale = new Vector3(0.9f, h * 0.5f, 0.9f);
                pillar.transform.rotation = Quaternion.Euler(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));
                Apply(pillar, palePillarMat);
                Destroy(pillar.GetComponent<Collider>());
            }
        }

        // ======= 공통 유틸 =======

        // 소품 개수를 면적 비례로 유지 — designRadius는 소품 수치를 설계했던 스케일 1.0 기준 반경.
        // WorldScale 확장으로 radius가 커져도 밀도(개수/면적)가 희석되지 않도록 보정.
        private static int ScaleCount(int baseCount, float radius, float designRadius)
        {
            return Mathf.Max(baseCount, Mathf.RoundToInt(baseCount * (radius * radius) / (designRadius * designRadius)));
        }

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

        // 에미시브 느낌 — 실시간 Light 컴포넌트 없이 자체 발광 색상만 부여 (빌드 타임 1회).
        private void SetEmissive(Material mat, Color emission)
        {
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);
        }
    }
}
