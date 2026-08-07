using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 프로시저럴 마을/전초기지 빌더.
    /// - 본 마을: meadow 서남서(202.5°) 0.45R 지점 — 광장/우물/집5채/상점/훈련소/가챠 오두막/장식
    /// - 전초기지: 나머지 6개 리전 0.4R 지점(서브에리어 게이트 방향 회피) — 테마 오두막 + 모닥불 + 통나무 의자
    /// 부트스트랩 EnsureGround 이후 호출. regions는 WorldScale 1.5가 이미 적용된 상태.
    /// 콜라이더는 건물 벽만 유지(통행 차단), 장식/소품은 전부 Destroy. 머티리얼은 색상당 1개 캐시 공유.
    /// </summary>
    public class VillageBuilder : MonoBehaviour
    {
        // ===== 배치 상수 =====
        private const float VillageAngleDeg = 202.5f;   // meadow 중심 기준 서남서
        private const float VillageDistFrac = 0.45f;    // 중심거리 0.45R (≈34m)
        private const float OutpostDistFrac = 0.4f;     // 전초기지 중심거리 0.4R
        private const float SubAreaSafeDist = 20f;      // 서브에리어 중심과 최소 이격
        private const float InteractionRadius = 3f;     // 상호작용 반경

        /// <summary>본 마을 부지 반경 — 외부 배치 로직(RegionTerrainBuilder 소품 등)의 회피 기준.</summary>
        public const float MainVillageFootprintRadius = 18f;

        /// <summary>본 마을 중심 월드좌표 — 배치 상수의 단일 출처 (meadow center/radius로 계산).</summary>
        public static Vector3 GetMainVillageCenter(Vector3 meadowCenter, float meadowRadius)
        {
            return Polar(meadowCenter, VillageAngleDeg, meadowRadius * VillageDistFrac);
        }

        /// <summary>
        /// 전초기지 방향(리전 중심 기준 각도). 서브에리어 게이트 + 리전 게이트웨이 방향을 피해 선정.
        /// pond 서브 315°/135° → 60°, forest 124°/315° → 200°, swamp 297°/153° → 45°,
        /// mountain 135°/315° → 225°, garden 45°/243°(게이트웨이 162°) → 110°, ruins 63°/207° → 330°.
        /// </summary>
        private static readonly Dictionary<string, float> OutpostAngles = new Dictionary<string, float>
        {
            { "pond", 60f },
            { "forest", 200f },
            { "swamp", 45f },
            { "mountain", 225f },
            { "garden", 110f },
            { "ruins", 330f },
            // 2막(ver2) — hollow 서브 122°/318° → 230°, dunes 서브 133°/319° → 40°.
            // 미등록이면 ChooseOutpostPosition이 기본 90°로 떨어져 서브에리어 입구와 겹칠 수 있다.
            { "hollow", 230f },
            { "dunes", 40f },
            // frostline 서브 135°/317° → 230°, emberfall 서브 132°/318° → 230°.
            { "frostline", 230f },
            { "emberfall", 230f },
            // canopy 서브 130°/319° → 230°, nameless 서브 135°/318° → 230°.
            { "canopy", 230f },
            { "nameless", 230f }
        };

        /// <summary>잡기 아이(CatcherKid) 배치 — 리전 중심 부근 개활지 (서브에리어/전초기지 회피 각도).</summary>
        private static readonly (string regionId, float angleDeg, float distFrac)[] KidSpots =
        {
            ("pond", 200f, 0.30f),
            ("forest", 20f, 0.30f),
            ("garden", 340f, 0.35f),
            ("swamp", 0f, 0.35f),
            ("mountain", 30f, 0.30f),
            // 2막(ver2) — 텅 빈 들·잿불 골짜기·이름 없는 자리에는 두지 않는다.
            // 곤충이 없는 땅, 라온이 다치는 곳, 최종 지역에 채집 아이가 서 있으면 톤이 무너진다.
            ("dunes", 200f, 0.34f),
            ("frostline", 200f, 0.34f),
            ("canopy", 200f, 0.34f)
        };

        // 머티리얼 캐시 — 색상당 1개 생성 공유
        private readonly Dictionary<Color, Material> materialCache = new Dictionary<Color, Material>();
        private Transform villageRoot;

        /// <summary>부트스트랩 EnsureGround 이후 호출. regions는 WorldScale 1.5가 이미 적용된 상태.</summary>
        public VillageBuildResult Build(Data.RegionData[] regions)
        {
            var result = new VillageBuildResult();
            if (regions == null || regions.Length == 0) return result;

            villageRoot = new GameObject("Village").transform;

            // ── 1) 본 마을 (meadow) — 주민 앵커 8개가 리스트 맨 앞 (우선 스폰) ──
            Data.RegionData meadow = FindRegion(regions, "meadow");
            if (meadow != null)
            {
                BuildMainVillage(meadow, result);

                // meadow 잡기 아이 2명 — 마을 밖 동/북동 개활지 (초원 동굴 59° 방향 회피)
                result.npcAnchors.Add(new NpcSpawnAnchor
                {
                    position = Polar(meadow.centerPosition, 5f, meadow.radius * 0.40f),
                    kind = NpcKind.CatcherKid,
                    regionId = "meadow",
                    wanderRadius = 25f
                });
                result.npcAnchors.Add(new NpcSpawnAnchor
                {
                    position = Polar(meadow.centerPosition, 32f, meadow.radius * 0.42f),
                    kind = NpcKind.CatcherKid,
                    regionId = "meadow",
                    wanderRadius = 25f
                });
            }

            // ── 2) 전초기지 (나머지 6개 리전) — 리전당 주민 1명 ──
            foreach (var region in regions)
            {
                if (region == null || region.regionId == "meadow") continue;
                BuildOutpost(region, result);
            }

            // ── 3) 리전 잡기 아이 — 우선순위 낮음 (리스트 뒤쪽) ──
            foreach (var (regionId, angleDeg, distFrac) in KidSpots)
            {
                Data.RegionData region = FindRegion(regions, regionId);
                if (region == null) continue;
                result.npcAnchors.Add(new NpcSpawnAnchor
                {
                    position = Polar(region.centerPosition, angleDeg, region.radius * distFrac),
                    kind = NpcKind.CatcherKid,
                    regionId = regionId,
                    wanderRadius = 25f
                });
            }

            // ── 4) 스토리 NPC (고정 배치, wanderRadius 0) — 다가가 대화하면 스토리 발동 ──
            if (meadow != null)
            {
                result.npcAnchors.Add(new NpcSpawnAnchor
                {
                    position = Polar(meadow.centerPosition, 200f, meadow.radius * 0.16f),
                    kind = NpcKind.StoryNpc, regionId = "meadow",
                    storyNpcId = "village_elder", wanderRadius = 0f
                });
                result.npcAnchors.Add(new NpcSpawnAnchor
                {
                    position = Polar(meadow.centerPosition, 75f, meadow.radius * 0.30f),
                    kind = NpcKind.StoryNpc, regionId = "meadow",
                    storyNpcId = "catcher_rival", wanderRadius = 0f
                });
            }
            Data.RegionData storyForest = FindRegion(regions, "forest");
            if (storyForest != null)
            {
                result.npcAnchors.Add(new NpcSpawnAnchor
                {
                    position = Polar(storyForest.centerPosition, 90f, storyForest.radius * 0.22f),
                    kind = NpcKind.StoryNpc, regionId = "forest",
                    storyNpcId = "ruins_scholar", wanderRadius = 0f
                });
            }

            // ── 2막(ver2) 동행자 배치 ──
            // 같은 storyNpcId를 여러 리전에 두어도 안전하다 — NpcManager.SpawnStoryNpc가
            // 앵커 index로 npcId를 구분하고, NpcTalk 트리거는 storyNpcId 하나만 보므로
            // 어느 개체에 말을 걸든 그 NPC의 비트 체인이 이어진다.
            // 배치 자체가 서사를 실어 나른다 — 라온은 잿불 골짜기에서 다쳐 이탈하므로
            // 그 뒤 리전에는 앵커를 두지 않고 최종 리전에서만 복귀한다.
            //
            // **헬퍼로 묶지 않고 리터럴을 반복하는 이유**: game_facts.story_npc_ids()가
            // `storyNpcId = "..."` 형태를 정규식으로 읽어 story_lint 검사 3(NpcTalk 대상 존재)을
            // 돌린다. 헬퍼 인자로 넘기면 그 추출기에 잡히지 않아 월드에 배치된 NPC가
            // "미배치"로 잘못 보고된다.
            Data.RegionData storyHollow = FindRegion(regions, "hollow");
            if (storyHollow != null)
            {
                result.npcAnchors.Add(new NpcSpawnAnchor
                {
                    // 20°/0.30R — 두 서브에리어(침묵의 자리 121°, 마른 굴 318°) 진입 반경 밖.
                    // 서브에리어 안에 서 있으면 말을 걸려다 구역에 빨려 들어간다.
                    position = Polar(storyHollow.centerPosition, 20f, storyHollow.radius * 0.30f),
                    kind = NpcKind.StoryNpc, regionId = "hollow",
                    storyNpcId = "ruins_scholar", wanderRadius = 0f
                });
            }
            Data.RegionData storyDunes = FindRegion(regions, "dunes");
            if (storyDunes != null)
            {
                result.npcAnchors.Add(new NpcSpawnAnchor
                {
                    position = Polar(storyDunes.centerPosition, 250f, storyDunes.radius * 0.26f),
                    kind = NpcKind.StoryNpc, regionId = "dunes",
                    storyNpcId = "catcher_rival", wanderRadius = 0f
                });
                // 집게 — 보스 대결 상대(NpcBossDuels). 라온 반대편에 세워 둘이 겹치지 않게 한다.
                result.npcAnchors.Add(new NpcSpawnAnchor
                {
                    // 60°/0.22R — 서브에리어(창고 133°, 구덩이 319°) 진입 반경 밖.
                    position = Polar(storyDunes.centerPosition, 60f, storyDunes.radius * 0.22f),
                    kind = NpcKind.StoryNpc, regionId = "dunes",
                    storyNpcId = "ledger_grip", wanderRadius = 0f
                });
            }
            Data.RegionData storyFrostline = FindRegion(regions, "frostline");
            if (storyFrostline != null)
            {
                result.npcAnchors.Add(new NpcSpawnAnchor
                {
                    position = Polar(storyFrostline.centerPosition, 20f, storyFrostline.radius * 0.30f),
                    kind = NpcKind.StoryNpc, regionId = "frostline",
                    storyNpcId = "ruins_scholar", wanderRadius = 0f
                });
                // 저울 — 보스 대결 상대. 세라와 마주 보게 반대편에 둔다(옛 동문의 대치).
                result.npcAnchors.Add(new NpcSpawnAnchor
                {
                    position = Polar(storyFrostline.centerPosition, 200f, storyFrostline.radius * 0.26f),
                    kind = NpcKind.StoryNpc, regionId = "frostline",
                    storyNpcId = "ledger_scale", wanderRadius = 0f
                });
            }
            Data.RegionData storyEmberfall = FindRegion(regions, "emberfall");
            if (storyEmberfall != null)
            {
                // 라온의 마지막 배치 — 여기서 다쳐 이탈하므로 canopy에는 두지 않는다.
                result.npcAnchors.Add(new NpcSpawnAnchor
                {
                    position = Polar(storyEmberfall.centerPosition, 250f, storyEmberfall.radius * 0.26f),
                    kind = NpcKind.StoryNpc, regionId = "emberfall",
                    storyNpcId = "catcher_rival", wanderRadius = 0f
                });
                // 먹은 여기서 이탈해 아군이 된다 — ch10_echo(NpcTalk ledger_ink)의 월드 배치.
                // 이 앵커가 없으면 story_lint 검사 3이 "월드 미배치"로 FAIL한다.
                result.npcAnchors.Add(new NpcSpawnAnchor
                {
                    position = Polar(storyEmberfall.centerPosition, 60f, storyEmberfall.radius * 0.28f),
                    kind = NpcKind.StoryNpc, regionId = "emberfall",
                    storyNpcId = "ledger_ink", wanderRadius = 0f
                });
            }
            Data.RegionData storyCanopy = FindRegion(regions, "canopy");
            if (storyCanopy != null)
            {
                // 라온은 여기 없다(잿불 골짜기에서 부상). 세라만 동행한다.
                result.npcAnchors.Add(new NpcSpawnAnchor
                {
                    position = Polar(storyCanopy.centerPosition, 20f, storyCanopy.radius * 0.30f),
                    kind = NpcKind.StoryNpc, regionId = "canopy",
                    storyNpcId = "ruins_scholar", wanderRadius = 0f
                });
            }
            Data.RegionData storyNameless = FindRegion(regions, "nameless");
            if (storyNameless != null)
            {
                result.npcAnchors.Add(new NpcSpawnAnchor
                {
                    position = Polar(storyNameless.centerPosition, 20f, storyNameless.radius * 0.30f),
                    kind = NpcKind.StoryNpc, regionId = "nameless",
                    storyNpcId = "ruins_scholar", wanderRadius = 0f
                });
                // 라온 복귀 — ch12_echo(NpcTalk catcher_rival)가 여기서 열린다.
                result.npcAnchors.Add(new NpcSpawnAnchor
                {
                    position = Polar(storyNameless.centerPosition, 250f, storyNameless.radius * 0.26f),
                    kind = NpcKind.StoryNpc, regionId = "nameless",
                    storyNpcId = "catcher_rival", wanderRadius = 0f
                });
                // 관장 하월 — 마지막 보스 대결 상대.
                // 70°/0.30R — 서브에리어 둘(장부의 방 135°, 빈칸 318°)의 진입 반경을 모두 피하고,
                // 세라(20°)·라온(250°)과도 10m 이상 떨어진다.
                result.npcAnchors.Add(new NpcSpawnAnchor
                {
                    position = Polar(storyNameless.centerPosition, 70f, storyNameless.radius * 0.30f),
                    kind = NpcKind.StoryNpc, regionId = "nameless",
                    storyNpcId = "ledger_chief", wanderRadius = 0f
                });
            }

            Debug.Log($"[VillageBuilder] 마을 생성 완료 — NPC 앵커 {result.npcAnchors.Count}개, 상호작용 {result.interactions.Count}개");
            return result;
        }

        // ================= 본 마을 =================

        private void BuildMainVillage(Data.RegionData meadow, VillageBuildResult result)
        {
            // 마을 중심: 서남서 0.45R — 플레이어 스타트(중심 ±10m)와 광장 가장자리(≈26m) 분리 보장
            Vector3 v = Polar(meadow.centerPosition, VillageAngleDeg, meadow.radius * VillageDistFrac);

            GameObject main = new GameObject("MainVillage");
            main.transform.SetParent(villageRoot, false);
            main.transform.position = v;
            Transform root = main.transform;

            BuildPlazaAndWell(root);
            BuildHouses(root, v);
            BuildShop(root, v, result);
            BuildTrainingHall(root, v, result);
            BuildGachaHut(root, v, result);
            BuildHospital(root, v, result);
            BuildVillageDecorations(root);
            AddVillageVillagers(v, result);
        }

        /// <summary>원형 광장(납작 실린더) + 중앙 우물(원통 벽 + 두레박 지붕).</summary>
        private void BuildPlazaAndWell(Transform root)
        {
            Color dirt = new Color(0.76f, 0.64f, 0.44f);
            Color stone = new Color(0.55f, 0.55f, 0.55f);
            Color darkWater = new Color(0.1f, 0.12f, 0.18f);
            Color wood = new Color(0.45f, 0.3f, 0.16f);
            Color roofRed = new Color(0.62f, 0.24f, 0.18f);

            // 광장 플레인 — 걸어다니는 바닥 장식이므로 콜라이더 제거.
            // y는 리전 평면(Region_{id}, PlaySceneBootstrap이 Y=0.08에 배치)보다 확실히 위여야 한다.
            // 옛 0.03은 실린더 반높이 0.05를 더해 상면이 정확히 0.08 — 리전 평면과 같은 평면이라
            // 지름 16m 원반 전체가 z-fighting하거나 뒤에 그려져 통째로 사라졌다.
            // RegionTerrainBuilder의 SteppingStone(상면 0.15)과 같은 여유를 준다.
            Prim(PrimitiveType.Cylinder, "Plaza", root,
                new Vector3(0f, 0.10f, 0f), Vector3.zero, new Vector3(16f, 0.05f, 16f), dirt);

            Transform well = Child(root, "Well", Vector3.zero);
            // 우물 벽 — 건물 벽이 아니므로 규칙에 따라 콜라이더 제거
            Prim(PrimitiveType.Cylinder, "WellWall", well,
                new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(2.0f, 0.5f, 2.0f), stone);
            Prim(PrimitiveType.Cylinder, "WellWater", well,
                new Vector3(0f, 0.72f, 0f), Vector3.zero, new Vector3(1.4f, 0.3f, 1.4f), darkWater);
            // 두레박 지붕 기둥 2개 + 경사 지붕 2장
            Prim(PrimitiveType.Cylinder, "WellPostL", well,
                new Vector3(-0.95f, 1.5f, 0f), Vector3.zero, new Vector3(0.12f, 1.0f, 0.12f), wood);
            Prim(PrimitiveType.Cylinder, "WellPostR", well,
                new Vector3(0.95f, 1.5f, 0f), Vector3.zero, new Vector3(0.12f, 1.0f, 0.12f), wood);
            Prim(PrimitiveType.Cube, "WellRoofF", well,
                new Vector3(0f, 2.3f, 0.42f), new Vector3(-35f, 0f, 0f), new Vector3(2.3f, 0.1f, 1.1f), roofRed);
            Prim(PrimitiveType.Cube, "WellRoofB", well,
                new Vector3(0f, 2.3f, -0.42f), new Vector3(35f, 0f, 0f), new Vector3(2.3f, 0.1f, 1.1f), roofRed);
            // 두레박 + 밧줄
            Prim(PrimitiveType.Cylinder, "WellRope", well,
                new Vector3(0f, 1.75f, 0f), Vector3.zero, new Vector3(0.03f, 0.35f, 0.03f), new Color(0.8f, 0.72f, 0.55f));
            Prim(PrimitiveType.Cylinder, "WellBucket", well,
                new Vector3(0f, 1.35f, 0f), Vector3.zero, new Vector3(0.28f, 0.15f, 0.28f), wood);
        }

        /// <summary>집 5채 — 벽/지붕 색 변주. 벽만 콜라이더 유지, 문/창/지붕은 장식.</summary>
        private void BuildHouses(Transform root, Vector3 villageCenter)
        {
            // (광장 기준 각도, 거리, 벽색, 지붕색)
            (float angle, float dist, Color wall, Color roof)[] houses =
            {
                (25f, 13f, new Color(0.85f, 0.72f, 0.55f), new Color(0.65f, 0.25f, 0.20f)),
                (55f, 14f, new Color(0.75f, 0.80f, 0.85f), new Color(0.30f, 0.40f, 0.55f)),
                (145f, 13f, new Color(0.80f, 0.75f, 0.60f), new Color(0.35f, 0.50f, 0.30f)),
                (215f, 13f, new Color(0.70f, 0.60f, 0.50f), new Color(0.50f, 0.30f, 0.35f)),
                (320f, 13f, new Color(0.88f, 0.85f, 0.75f), new Color(0.75f, 0.50f, 0.20f))
            };

            Color doorColor = new Color(0.25f, 0.16f, 0.10f);
            Color windowColor = new Color(0.95f, 0.90f, 0.60f);

            for (int i = 0; i < houses.Length; i++)
            {
                var h = houses[i];
                Vector3 pos = Polar(villageCenter, h.angle, h.dist);
                Transform house = FacingRoot($"House_{i + 1}", root, pos, villageCenter);

                // 벽 — 통행 차단용 콜라이더 유지
                Prim(PrimitiveType.Cube, "Wall", house,
                    new Vector3(0f, 1.4f, 0f), Vector3.zero, new Vector3(3.8f, 2.8f, 3.2f), h.wall, keepCollider: true);
                // 경사 지붕 2장 + 용마루 (Z축 회전 — 마루가 앞뒤 방향)
                Prim(PrimitiveType.Cube, "RoofR", house,
                    new Vector3(0.95f, 3.1f, 0f), new Vector3(0f, 0f, 35f), new Vector3(2.5f, 0.15f, 3.7f), h.roof);
                Prim(PrimitiveType.Cube, "RoofL", house,
                    new Vector3(-0.95f, 3.1f, 0f), new Vector3(0f, 0f, -35f), new Vector3(2.5f, 0.15f, 3.7f), h.roof);
                Prim(PrimitiveType.Cube, "RoofRidge", house,
                    new Vector3(0f, 3.85f, 0f), Vector3.zero, new Vector3(0.35f, 0.12f, 3.75f), h.roof);
                // 문/창 — 어두운 장식, 콜라이더 제거
                Prim(PrimitiveType.Cube, "Door", house,
                    new Vector3(0f, 0.95f, 1.63f), Vector3.zero, new Vector3(0.95f, 1.9f, 0.12f), doorColor);
                Prim(PrimitiveType.Cube, "WindowL", house,
                    new Vector3(-1.15f, 1.9f, 1.63f), Vector3.zero, new Vector3(0.65f, 0.65f, 0.10f), windowColor);
                Prim(PrimitiveType.Cube, "WindowR", house,
                    new Vector3(1.15f, 1.9f, 1.63f), Vector3.zero, new Vector3(0.65f, 0.65f, 0.10f), windowColor);
            }
        }

        /// <summary>상점 — 집보다 큰 건물 + 줄무늬 차양 + 간판. 문 앞 ItemShop 상호작용.</summary>
        private void BuildShop(Transform root, Vector3 villageCenter, VillageBuildResult result)
        {
            Vector3 pos = Polar(villageCenter, 180f, 12f);
            Transform shop = FacingRoot("Shop", root, pos, villageCenter);

            Color wall = new Color(0.82f, 0.70f, 0.50f);
            Color roof = new Color(0.62f, 0.24f, 0.18f);
            Color awningRed = new Color(0.85f, 0.20f, 0.20f);
            Color awningWhite = new Color(0.95f, 0.93f, 0.88f);

            Prim(PrimitiveType.Cube, "Wall", shop,
                new Vector3(0f, 1.9f, 0f), Vector3.zero, new Vector3(6f, 3.8f, 5f), wall, keepCollider: true);
            // 지붕 — 9개 건물 중 상점만 빠져 있었다. 벽 큐브 윗면이 그대로 노출되고,
            // 아래 간판(하단 4.10)이 벽 상단(3.80) 위 0.3m 허공에 떠 보였다.
            // 상단을 4.10으로 맞춰 간판과 밀착시킨다(훈련소의 Roof 상단 = 간판 하단 관례와 동일).
            Prim(PrimitiveType.Cube, "Roof", shop,
                new Vector3(0f, 3.95f, 0f), Vector3.zero, new Vector3(6.6f, 0.3f, 5.6f), roof);
            Prim(PrimitiveType.Cube, "Door", shop,
                new Vector3(0f, 1.1f, 2.56f), Vector3.zero, new Vector3(1.3f, 2.2f, 0.12f), new Color(0.25f, 0.16f, 0.10f));
            Prim(PrimitiveType.Cube, "WindowL", shop,
                new Vector3(-1.9f, 2.2f, 2.56f), Vector3.zero, new Vector3(0.9f, 0.9f, 0.10f), new Color(0.95f, 0.90f, 0.60f));
            Prim(PrimitiveType.Cube, "WindowR", shop,
                new Vector3(1.9f, 2.2f, 2.56f), Vector3.zero, new Vector3(0.9f, 0.9f, 0.10f), new Color(0.95f, 0.90f, 0.60f));

            // 줄무늬 차양 — 빨강/흰색 교차 6장, 앞으로 살짝 기울임
            for (int i = 0; i < 6; i++)
            {
                float x = -2.25f + 0.9f * i;
                Prim(PrimitiveType.Cube, $"Awning_{i}", shop,
                    new Vector3(x, 3.0f, 3.1f), new Vector3(18f, 0f, 0f), new Vector3(0.88f, 0.08f, 1.5f),
                    i % 2 == 0 ? awningRed : awningWhite);
            }

            CreateSign(shop, "상점", new Vector3(0f, 4.55f, 2.56f), 3.2f);

            result.interactions.Add(new InteractionPointDef
            {
                id = "village_shop",
                worldPosition = pos + DirTo(pos, villageCenter) * 4.0f, // 벽 절반(2.5) + 1.5m 앞
                radius = InteractionRadius,
                label = "상점",
                kind = InteractionKind.ItemShop
            });
        }

        /// <summary>병원 — 흰 벽 + 빨간 십자 간판. 곤충 HP·상태 치료(Hospital 상호작용). 빈 각도 315°.</summary>
        private void BuildHospital(Transform root, Vector3 villageCenter, VillageBuildResult result)
        {
            Vector3 pos = Polar(villageCenter, 315f, 12f);
            Transform hosp = FacingRoot("Hospital", root, pos, villageCenter);

            Color wall = new Color(0.95f, 0.95f, 0.97f);       // 흰 병원 벽
            Color roof = new Color(0.80f, 0.30f, 0.28f);       // 붉은 지붕
            Color cross = new Color(0.90f, 0.20f, 0.18f);      // 적십자

            Prim(PrimitiveType.Cube, "Wall", hosp,
                new Vector3(0f, 1.9f, 0f), Vector3.zero, new Vector3(6f, 3.8f, 5f), wall, keepCollider: true);
            Prim(PrimitiveType.Cube, "Roof", hosp,
                new Vector3(0f, 3.95f, 0f), Vector3.zero, new Vector3(6.6f, 0.3f, 5.6f), roof);
            Prim(PrimitiveType.Cube, "Door", hosp,
                new Vector3(0f, 1.1f, 2.56f), Vector3.zero, new Vector3(1.3f, 2.2f, 0.12f), new Color(0.6f, 0.75f, 0.85f));
            Prim(PrimitiveType.Cube, "WindowL", hosp,
                new Vector3(-1.9f, 2.2f, 2.56f), Vector3.zero, new Vector3(0.9f, 0.9f, 0.10f), new Color(0.85f, 0.92f, 1f));
            Prim(PrimitiveType.Cube, "WindowR", hosp,
                new Vector3(1.9f, 2.2f, 2.56f), Vector3.zero, new Vector3(0.9f, 0.9f, 0.10f), new Color(0.85f, 0.92f, 1f));

            // 적십자 마크(벽 정면) — 세로 + 가로 큐브
            Prim(PrimitiveType.Cube, "CrossV", hosp,
                new Vector3(0f, 2.9f, 2.60f), Vector3.zero, new Vector3(0.45f, 1.4f, 0.10f), cross);
            Prim(PrimitiveType.Cube, "CrossH", hosp,
                new Vector3(0f, 2.9f, 2.60f), Vector3.zero, new Vector3(1.4f, 0.45f, 0.10f), cross);

            CreateSign(hosp, "병원", new Vector3(0f, 4.55f, 2.56f), 3.2f);

            result.interactions.Add(new InteractionPointDef
            {
                id = "village_hospital",
                worldPosition = pos + DirTo(pos, villageCenter) * 4.0f,
                radius = InteractionRadius,
                label = "병원",
                kind = InteractionKind.Hospital
            });
        }

        /// <summary>훈련소 — 건물 + 마당 과녁 더미(원반 스택) 2개 + 간판. Training 상호작용.</summary>
        private void BuildTrainingHall(Transform root, Vector3 villageCenter, VillageBuildResult result)
        {
            Vector3 pos = Polar(villageCenter, 90f, 12f);
            Transform hall = FacingRoot("TrainingHall", root, pos, villageCenter);

            Color wall = new Color(0.60f, 0.55f, 0.48f);
            Color roof = new Color(0.35f, 0.32f, 0.30f);

            Prim(PrimitiveType.Cube, "Wall", hall,
                new Vector3(0f, 1.6f, 0f), Vector3.zero, new Vector3(5f, 3.2f, 4.2f), wall, keepCollider: true);
            Prim(PrimitiveType.Cube, "Roof", hall,
                new Vector3(0f, 3.4f, 0f), Vector3.zero, new Vector3(5.6f, 0.2f, 4.8f), roof);
            Prim(PrimitiveType.Cube, "Door", hall,
                new Vector3(0f, 1.0f, 2.16f), Vector3.zero, new Vector3(1.1f, 2.0f, 0.12f), new Color(0.25f, 0.16f, 0.10f));

            // 마당 과녁 더미 2개 — 원반 스택(흰/파랑/빨강), 광장 쪽을 향함
            BuildTargetDummy(hall, new Vector3(3.4f, 0f, 1.0f));
            BuildTargetDummy(hall, new Vector3(-3.4f, 0f, 1.4f));

            CreateSign(hall, "훈련소", new Vector3(0f, 3.95f, 2.16f), 3.0f);

            result.interactions.Add(new InteractionPointDef
            {
                id = "village_training",
                worldPosition = pos + DirTo(pos, villageCenter) * 3.6f, // 벽 절반(2.1) + 1.5m 앞
                radius = InteractionRadius,
                label = "훈련소",
                kind = InteractionKind.Training
            });
        }

        /// <summary>과녁 더미 — 기둥 + 지름이 줄어드는 원반 3장 스택.</summary>
        private void BuildTargetDummy(Transform parent, Vector3 localPos)
        {
            Transform dummy = Child(parent, "TargetDummy", localPos);
            Prim(PrimitiveType.Cylinder, "Post", dummy,
                new Vector3(0f, 0.6f, 0f), Vector3.zero, new Vector3(0.12f, 0.6f, 0.12f), new Color(0.4f, 0.28f, 0.16f));
            // 원반은 눕힌 실린더(90° X회전) — 평평한 면이 광장(+Z) 방향
            Prim(PrimitiveType.Cylinder, "DiscWhite", dummy,
                new Vector3(0f, 1.3f, 0f), new Vector3(90f, 0f, 0f), new Vector3(1.3f, 0.05f, 1.3f), new Color(0.92f, 0.92f, 0.88f));
            Prim(PrimitiveType.Cylinder, "DiscBlue", dummy,
                new Vector3(0f, 1.3f, 0.07f), new Vector3(90f, 0f, 0f), new Vector3(0.9f, 0.05f, 0.9f), new Color(0.25f, 0.45f, 0.85f));
            Prim(PrimitiveType.Cylinder, "DiscRed", dummy,
                new Vector3(0f, 1.3f, 0.14f), new Vector3(90f, 0f, 0f), new Vector3(0.5f, 0.05f, 0.5f), new Color(0.85f, 0.2f, 0.2f));
        }

        /// <summary>가챠 오두막 — 육각 벽 + 파고다 지붕 + 지붕 위 별 장식 + 간판. Gacha 상호작용.</summary>
        private void BuildGachaHut(Transform root, Vector3 villageCenter, VillageBuildResult result)
        {
            Vector3 pos = Polar(villageCenter, 270f, 11f);
            Transform hut = FacingRoot("GachaHut", root, pos, villageCenter);

            Color wall = new Color(0.55f, 0.35f, 0.55f);
            Color roof = new Color(0.35f, 0.22f, 0.40f);
            Color star = new Color(1f, 0.85f, 0.25f);

            // 육각 벽 — 60° 간격 6면 중 광장 방향(로컬 +Z, 90°) 1면은 문 개구부로 생략
            for (int k = 0; k < 6; k++)
            {
                float a = 60f * k + 30f;
                if (k == 1) continue; // 문 개구부
                float rad = a * Mathf.Deg2Rad;
                Vector3 wp = new Vector3(Mathf.Cos(rad) * 1.5f, 1.1f, Mathf.Sin(rad) * 1.5f);
                Prim(PrimitiveType.Cube, $"HexWall_{k}", hut,
                    wp, new Vector3(0f, -(a + 90f), 0f), new Vector3(1.7f, 2.2f, 0.25f), wall, keepCollider: true);
            }
            // 문 (개구부 안쪽 어두운 판)
            Prim(PrimitiveType.Cube, "Door", hut,
                new Vector3(0f, 1.0f, 1.35f), Vector3.zero, new Vector3(1.1f, 2.0f, 0.12f), new Color(0.15f, 0.10f, 0.08f));
            // 파고다 지붕 — 지름이 줄어드는 원반 3단
            Prim(PrimitiveType.Cylinder, "Roof1", hut,
                new Vector3(0f, 2.5f, 0f), Vector3.zero, new Vector3(4.2f, 0.15f, 4.2f), roof);
            Prim(PrimitiveType.Cylinder, "Roof2", hut,
                new Vector3(0f, 2.85f, 0f), Vector3.zero, new Vector3(3.0f, 0.18f, 3.0f), roof);
            Prim(PrimitiveType.Cylinder, "Roof3", hut,
                new Vector3(0f, 3.2f, 0f), Vector3.zero, new Vector3(1.8f, 0.18f, 1.8f), roof);
            // 지붕 위 별 장식 — 중심 구 + 방사형 스파이크 4개 (XY 평면 8각 별)
            Prim(PrimitiveType.Sphere, "StarCore", hut,
                new Vector3(0f, 4.0f, 0f), Vector3.zero, new Vector3(0.35f, 0.35f, 0.35f), star);
            for (int s = 0; s < 4; s++)
            {
                Prim(PrimitiveType.Cube, $"StarSpike_{s}", hut,
                    new Vector3(0f, 4.0f, 0f), new Vector3(0f, 0f, 45f * s), new Vector3(1.0f, 0.1f, 0.1f), star);
            }

            CreateSign(hut, "랜덤상자", new Vector3(0f, 2.1f, 1.7f), 2.4f);

            result.interactions.Add(new InteractionPointDef
            {
                id = "village_gacha",
                worldPosition = pos + DirTo(pos, villageCenter) * 3.0f, // 육각 반경(1.5) + 1.5m 앞
                radius = InteractionRadius,
                label = "랜덤상자",
                kind = InteractionKind.Gacha
            });
        }

        /// <summary>장식 — 가로등(기둥+밝은 구, Light 컴포넌트 없음), 꽃 화분, 울타리 조각.</summary>
        private void BuildVillageDecorations(Transform root)
        {
            Transform deco = Child(root, "Decorations", Vector3.zero);

            Color post = new Color(0.20f, 0.18f, 0.16f);
            Color lampGlow = new Color(1f, 0.95f, 0.60f);

            // 가로등 4개 — 광장 가장자리
            float[] lampAngles = { 45f, 135f, 225f, 315f };
            for (int i = 0; i < lampAngles.Length; i++)
            {
                float rad = lampAngles[i] * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(rad) * 8.8f, 0f, Mathf.Sin(rad) * 8.8f);
                Prim(PrimitiveType.Cylinder, $"LampPost_{i}", deco,
                    p + new Vector3(0f, 1.6f, 0f), Vector3.zero, new Vector3(0.15f, 1.6f, 0.15f), post);
                Prim(PrimitiveType.Sphere, $"LampGlow_{i}", deco,
                    p + new Vector3(0f, 3.35f, 0f), Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f), lampGlow);
            }

            // 꽃 화분 6개 — 집/건물 근처
            Color pot = new Color(0.65f, 0.40f, 0.25f);
            Color[] flowers =
            {
                new Color(1f, 0.55f, 0.70f), new Color(1f, 0.85f, 0.30f), new Color(0.55f, 0.60f, 1f),
                new Color(1f, 0.45f, 0.35f), new Color(0.85f, 0.55f, 1f), new Color(1f, 0.75f, 0.50f)
            };
            float[] potAngles = { 10f, 70f, 120f, 200f, 250f, 340f };
            for (int i = 0; i < potAngles.Length; i++)
            {
                float rad = potAngles[i] * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(rad) * 9.5f, 0f, Mathf.Sin(rad) * 9.5f);
                Prim(PrimitiveType.Cylinder, $"FlowerPot_{i}", deco,
                    p + new Vector3(0f, 0.25f, 0f), Vector3.zero, new Vector3(0.5f, 0.25f, 0.5f), pot);
                Prim(PrimitiveType.Sphere, $"Flower_{i}", deco,
                    p + new Vector3(0f, 0.65f, 0f), Vector3.zero, new Vector3(0.35f, 0.35f, 0.35f), flowers[i]);
            }

            // 울타리 조각 3개 — 건물 사이 빈 각도, 접선 방향 정렬 (장식이므로 콜라이더 제거)
            Color fence = new Color(0.55f, 0.45f, 0.30f);
            float[] fenceAngles = { 0f, 120f, 245f };
            for (int i = 0; i < fenceAngles.Length; i++)
            {
                float a = fenceAngles[i];
                float rad = a * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(rad) * 15.5f, 0f, Mathf.Sin(rad) * 15.5f);
                Transform piece = Child(deco, $"FencePiece_{i}", p);
                piece.localRotation = Quaternion.Euler(0f, -(a + 90f), 0f); // 접선 방향
                Prim(PrimitiveType.Cube, "PostL", piece,
                    new Vector3(-1.1f, 0.5f, 0f), Vector3.zero, new Vector3(0.15f, 1.0f, 0.15f), fence);
                Prim(PrimitiveType.Cube, "PostR", piece,
                    new Vector3(1.1f, 0.5f, 0f), Vector3.zero, new Vector3(0.15f, 1.0f, 0.15f), fence);
                Prim(PrimitiveType.Cube, "Rail", piece,
                    new Vector3(0f, 0.75f, 0f), Vector3.zero, new Vector3(2.5f, 0.12f, 0.1f), fence);
            }
        }

        /// <summary>본 마을 주민 앵커 8개 — 우물가 2, 광장 2, 집 앞 4. wanderRadius 6~10.</summary>
        private void AddVillageVillagers(Vector3 v, VillageBuildResult result)
        {
            (Vector3 pos, float wander)[] villagers =
            {
                (v + new Vector3(2.4f, 0f, 1.6f), 6f),    // 우물가
                (v + new Vector3(-2.0f, 0f, -2.4f), 6f),  // 우물가
                (v + new Vector3(5.5f, 0f, -4.0f), 8f),   // 광장
                (v + new Vector3(-5.0f, 0f, 4.5f), 8f),   // 광장
                (Polar(v, 25f, 9.5f), 7f),                // 집1 앞
                (Polar(v, 145f, 9.5f), 7f),               // 집3 앞
                (Polar(v, 215f, 9.5f), 9f),               // 집4 앞
                (Polar(v, 320f, 9.5f), 10f)               // 집5 앞
            };
            foreach (var (pos, wander) in villagers)
            {
                result.npcAnchors.Add(new NpcSpawnAnchor
                {
                    position = pos,
                    kind = NpcKind.Villager,
                    regionId = "meadow",
                    wanderRadius = wander
                });
            }
        }

        // ================= 전초기지 =================

        /// <summary>전초기지 — 테마 오두막 + 모닥불 + 통나무 의자 2개 + 주민 앵커 1개. 상호작용 없음.</summary>
        private void BuildOutpost(Data.RegionData region, VillageBuildResult result)
        {
            Vector3 pos = ChooseOutpostPosition(region);
            Transform outpost = FacingRoot($"Outpost_{region.regionId}", villageRoot, pos, region.centerPosition);

            // 테마 오두막 — 로컬 -Z(리전 중심 반대편) 배치, 모닥불이 중심 쪽
            switch (region.regionId)
            {
                case "pond": BuildPierHut(outpost); break;
                case "forest": BuildLogCabin(outpost); break;
                case "swamp": BuildStiltHut(outpost); break;
                case "mountain": BuildStoneHut(outpost); break;
                case "garden": BuildFlowerPergola(outpost); break;
                case "ruins": BuildTentCamp(outpost); break;
                default: BuildLogCabin(outpost); break;
            }

            BuildCampfire(outpost, new Vector3(0f, 0f, 2.5f));

            // 통나무 의자 2개 — 모닥불 좌우 (눕힌 실린더, 장식)
            Color log = new Color(0.40f, 0.28f, 0.16f);
            Prim(PrimitiveType.Cylinder, "LogBenchL", outpost,
                new Vector3(-1.8f, 0.18f, 2.5f), new Vector3(90f, 0f, 0f), new Vector3(0.35f, 0.9f, 0.35f), log);
            Prim(PrimitiveType.Cylinder, "LogBenchR", outpost,
                new Vector3(1.8f, 0.18f, 2.5f), new Vector3(90f, 0f, 0f), new Vector3(0.35f, 0.9f, 0.35f), log);

            // 주민 앵커 — 모닥불 옆
            result.npcAnchors.Add(new NpcSpawnAnchor
            {
                position = outpost.TransformPoint(new Vector3(1.5f, 0f, 3.2f)),
                kind = NpcKind.Villager,
                regionId = region.regionId,
                wanderRadius = 6f
            });
        }

        /// <summary>
        /// 전초기지 위치 선정 — 사전 계산 각도 우선, 서브에리어 중심과 20m 미만이면 ±30° 단위로 회피 탐색.
        /// </summary>
        private Vector3 ChooseOutpostPosition(Data.RegionData region)
        {
            float baseAngle = OutpostAngles.TryGetValue(region.regionId, out float a) ? a : 90f;
            float dist = region.radius * OutpostDistFrac;

            for (int attempt = 0; attempt < 12; attempt++)
            {
                // 0, +30, -30, +60, -60 ... 순으로 탐색
                float offset = (attempt + 1) / 2 * 30f * (attempt % 2 == 1 ? 1f : -1f);
                if (attempt == 0) offset = 0f;
                Vector3 candidate = Polar(region.centerPosition, baseAngle + offset, dist);
                if (IsFarFromSubAreas(region, candidate)) return candidate;
            }
            return Polar(region.centerPosition, baseAngle, dist);
        }

        private static bool IsFarFromSubAreas(Data.RegionData region, Vector3 pos)
        {
            if (region.subAreas == null) return true;
            foreach (var sub in region.subAreas)
            {
                if (sub == null) continue;
                Vector3 d = sub.centerPosition - pos;
                if (new Vector2(d.x, d.z).magnitude < SubAreaSafeDist) return false;
            }
            return true;
        }

        /// <summary>모닥불 — 돌 링 6개 + 교차 장작 + 불꽃색 구 2겹 (Light 컴포넌트 금지 준수).</summary>
        private void BuildCampfire(Transform parent, Vector3 localPos)
        {
            Transform fire = Child(parent, "Campfire", localPos);
            Color stone = new Color(0.45f, 0.45f, 0.45f);
            Color log = new Color(0.35f, 0.24f, 0.13f);

            for (int i = 0; i < 6; i++)
            {
                float rad = 60f * i * Mathf.Deg2Rad;
                Prim(PrimitiveType.Sphere, $"RingStone_{i}", fire,
                    new Vector3(Mathf.Cos(rad) * 1.0f, 0.2f, Mathf.Sin(rad) * 1.0f), Vector3.zero,
                    new Vector3(0.5f, 0.4f, 0.5f), stone);
            }
            Prim(PrimitiveType.Cylinder, "LogA", fire,
                new Vector3(0f, 0.15f, 0f), new Vector3(90f, 45f, 0f), new Vector3(0.18f, 0.5f, 0.18f), log);
            Prim(PrimitiveType.Cylinder, "LogB", fire,
                new Vector3(0f, 0.15f, 0f), new Vector3(90f, -45f, 0f), new Vector3(0.18f, 0.5f, 0.18f), log);
            Prim(PrimitiveType.Sphere, "FlameOuter", fire,
                new Vector3(0f, 0.5f, 0f), Vector3.zero, new Vector3(0.6f, 0.8f, 0.6f), new Color(1f, 0.50f, 0.12f));
            Prim(PrimitiveType.Sphere, "FlameInner", fire,
                new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(0.3f, 0.45f, 0.3f), new Color(1f, 0.85f, 0.30f));
        }

        /// <summary>pond — 부두 오두막: 짧은 말뚝 위 오두막 + 옆쪽 널판 부두.</summary>
        private void BuildPierHut(Transform outpost)
        {
            Transform hut = Child(outpost, "PierHut", new Vector3(0f, 0f, -2.5f));
            Color wood = new Color(0.55f, 0.50f, 0.42f);
            Color roof = new Color(0.35f, 0.45f, 0.50f);

            // 말뚝 4개
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -1.2f : 1.2f;
                float z = (i < 2) ? -1.1f : 1.1f;
                Prim(PrimitiveType.Cylinder, $"Stilt_{i}", hut,
                    new Vector3(x, 0.35f, z), Vector3.zero, new Vector3(0.2f, 0.35f, 0.2f), wood);
            }
            // 벽 — 콜라이더 유지
            Prim(PrimitiveType.Cube, "Wall", hut,
                new Vector3(0f, 1.8f, 0f), Vector3.zero, new Vector3(2.8f, 2.2f, 2.6f), wood, keepCollider: true);
            Prim(PrimitiveType.Cube, "Roof", hut,
                new Vector3(0f, 3.1f, 0f), new Vector3(8f, 0f, 0f), new Vector3(3.4f, 0.12f, 3.2f), roof);
            Prim(PrimitiveType.Cube, "Door", hut,
                new Vector3(0f, 1.5f, 1.33f), Vector3.zero, new Vector3(0.9f, 1.6f, 0.1f), new Color(0.25f, 0.20f, 0.15f));
            // 부두 널판 3장 — 오두막 옆에서 앞쪽으로
            for (int i = 0; i < 3; i++)
            {
                Prim(PrimitiveType.Cube, $"PierPlank_{i}", hut,
                    new Vector3(-2.6f, 0.15f, 0.4f + i * 2.1f), Vector3.zero, new Vector3(1.2f, 0.08f, 2.0f), wood);
            }
        }

        /// <summary>forest — 통나무집: 목재 벽 + 모서리 통나무 + 전면 가로 통나무 장식.</summary>
        private void BuildLogCabin(Transform outpost)
        {
            Transform hut = Child(outpost, "LogCabin", new Vector3(0f, 0f, -2.5f));
            Color wood = new Color(0.45f, 0.30f, 0.16f);
            Color logDark = new Color(0.36f, 0.24f, 0.12f);
            Color roof = new Color(0.20f, 0.32f, 0.15f);

            Prim(PrimitiveType.Cube, "Wall", hut,
                new Vector3(0f, 1.2f, 0f), Vector3.zero, new Vector3(3.0f, 2.4f, 2.8f), wood, keepCollider: true);
            // 모서리 통나무 4개
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -1.5f : 1.5f;
                float z = (i < 2) ? -1.4f : 1.4f;
                Prim(PrimitiveType.Cylinder, $"CornerLog_{i}", hut,
                    new Vector3(x, 1.25f, z), Vector3.zero, new Vector3(0.35f, 1.25f, 0.35f), logDark);
            }
            // 전면 가로 통나무 장식 3단 (눕힌 실린더)
            for (int i = 0; i < 3; i++)
            {
                Prim(PrimitiveType.Cylinder, $"FrontLog_{i}", hut,
                    new Vector3(0f, 0.5f + i * 0.6f, 1.45f), new Vector3(0f, 0f, 90f), new Vector3(0.22f, 1.55f, 0.22f), logDark);
            }
            // 경사 지붕 2장 (마루가 좌우 방향)
            Prim(PrimitiveType.Cube, "RoofF", hut,
                new Vector3(0f, 2.9f, 0.75f), new Vector3(32f, 0f, 0f), new Vector3(3.4f, 0.12f, 1.9f), roof);
            Prim(PrimitiveType.Cube, "RoofB", hut,
                new Vector3(0f, 2.9f, -0.75f), new Vector3(-32f, 0f, 0f), new Vector3(3.4f, 0.12f, 1.9f), roof);
            Prim(PrimitiveType.Cube, "Door", hut,
                new Vector3(0f, 0.85f, 1.43f), Vector3.zero, new Vector3(0.9f, 1.7f, 0.1f), new Color(0.2f, 0.13f, 0.08f));
        }

        /// <summary>swamp — 수상 오두막: 기둥 4개 위 마루 + 벽 + 사다리.</summary>
        private void BuildStiltHut(Transform outpost)
        {
            Transform hut = Child(outpost, "StiltHut", new Vector3(0f, 0f, -2.5f));
            Color wood = new Color(0.40f, 0.42f, 0.30f);
            Color roof = new Color(0.30f, 0.35f, 0.22f);

            // 기둥 4개 (높이 1.5m)
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -1.2f : 1.2f;
                float z = (i < 2) ? -1.1f : 1.1f;
                Prim(PrimitiveType.Cylinder, $"Pillar_{i}", hut,
                    new Vector3(x, 0.75f, z), Vector3.zero, new Vector3(0.22f, 0.75f, 0.22f), wood);
            }
            Prim(PrimitiveType.Cube, "Floor", hut,
                new Vector3(0f, 1.5f, 0f), Vector3.zero, new Vector3(3.2f, 0.15f, 3.0f), wood);
            // 벽 — 마루 위, 콜라이더 유지 (통행 차단)
            Prim(PrimitiveType.Cube, "Wall", hut,
                new Vector3(0f, 2.5f, 0f), Vector3.zero, new Vector3(2.6f, 1.9f, 2.4f), wood, keepCollider: true);
            Prim(PrimitiveType.Cube, "Roof", hut,
                new Vector3(0f, 3.6f, 0f), new Vector3(6f, 0f, 0f), new Vector3(3.2f, 0.1f, 3.0f), roof);
            // 사다리 — 레일 2개 + 가로대 3개
            Color rail = new Color(0.32f, 0.34f, 0.24f);
            Prim(PrimitiveType.Cylinder, "LadderRailL", hut,
                new Vector3(-0.35f, 0.8f, 1.55f), new Vector3(20f, 0f, 0f), new Vector3(0.08f, 0.85f, 0.08f), rail);
            Prim(PrimitiveType.Cylinder, "LadderRailR", hut,
                new Vector3(0.35f, 0.8f, 1.55f), new Vector3(20f, 0f, 0f), new Vector3(0.08f, 0.85f, 0.08f), rail);
            for (int i = 0; i < 3; i++)
            {
                Prim(PrimitiveType.Cube, $"LadderRung_{i}", hut,
                    new Vector3(0f, 0.4f + i * 0.45f, 1.7f - i * 0.16f), Vector3.zero, new Vector3(0.7f, 0.06f, 0.06f), rail);
            }
        }

        /// <summary>mountain — 돌집: 석재 벽 + 평평한 돌판 지붕 + 굴뚝 + 바위.</summary>
        private void BuildStoneHut(Transform outpost)
        {
            Transform hut = Child(outpost, "StoneHut", new Vector3(0f, 0f, -2.5f));
            Color stone = new Color(0.55f, 0.53f, 0.50f);
            Color slate = new Color(0.40f, 0.38f, 0.36f);

            Prim(PrimitiveType.Cube, "Wall", hut,
                new Vector3(0f, 1.15f, 0f), Vector3.zero, new Vector3(3.2f, 2.3f, 3.0f), stone, keepCollider: true);
            Prim(PrimitiveType.Cube, "Roof", hut,
                new Vector3(0f, 2.45f, 0f), Vector3.zero, new Vector3(3.8f, 0.25f, 3.6f), slate);
            Prim(PrimitiveType.Cube, "Chimney", hut,
                new Vector3(1.0f, 2.95f, -0.8f), Vector3.zero, new Vector3(0.5f, 0.9f, 0.5f), slate);
            Prim(PrimitiveType.Cube, "Door", hut,
                new Vector3(0f, 0.85f, 1.53f), Vector3.zero, new Vector3(0.9f, 1.7f, 0.1f), new Color(0.22f, 0.16f, 0.10f));
            // 입구 옆 바위 2개
            Prim(PrimitiveType.Sphere, "BoulderL", hut,
                new Vector3(-2.1f, 0.3f, 1.2f), Vector3.zero, new Vector3(0.8f, 0.6f, 0.8f), slate);
            Prim(PrimitiveType.Sphere, "BoulderR", hut,
                new Vector3(2.2f, 0.25f, 0.8f), Vector3.zero, new Vector3(0.6f, 0.5f, 0.6f), slate);
        }

        /// <summary>garden — 꽃 정자: 기둥 4개 + 지붕 2단 + 꽃 구 장식 (벽 없음 → 콜라이더 전부 제거).</summary>
        private void BuildFlowerPergola(Transform outpost)
        {
            Transform hut = Child(outpost, "FlowerPergola", new Vector3(0f, 0f, -2.5f));
            Color post = new Color(0.90f, 0.88f, 0.80f);
            Color roof = new Color(0.85f, 0.60f, 0.65f);
            Color flowerPink = new Color(1f, 0.55f, 0.70f);
            Color flowerYellow = new Color(1f, 0.85f, 0.40f);

            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -1.4f : 1.4f;
                float z = (i < 2) ? -1.4f : 1.4f;
                Prim(PrimitiveType.Cylinder, $"Post_{i}", hut,
                    new Vector3(x, 1.3f, z), Vector3.zero, new Vector3(0.18f, 1.3f, 0.18f), post);
                // 지붕 모서리 꽃 구
                Prim(PrimitiveType.Sphere, $"CornerFlower_{i}", hut,
                    new Vector3(x, 2.85f, z), Vector3.zero, new Vector3(0.45f, 0.45f, 0.45f), flowerPink);
            }
            Prim(PrimitiveType.Cube, "Roof1", hut,
                new Vector3(0f, 2.7f, 0f), Vector3.zero, new Vector3(3.6f, 0.12f, 3.6f), roof);
            Prim(PrimitiveType.Cube, "Roof2", hut,
                new Vector3(0f, 2.9f, 0f), Vector3.zero, new Vector3(2.8f, 0.10f, 2.8f), roof);
            Prim(PrimitiveType.Sphere, "TopFlower", hut,
                new Vector3(0f, 3.2f, 0f), Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f), flowerYellow);
        }

        /// <summary>ruins — 천막 캠프: A형 천막 + 마룻대 + 바닥 매트 + 상자 (천 재질 → 콜라이더 전부 제거).</summary>
        private void BuildTentCamp(Transform outpost)
        {
            Transform hut = Child(outpost, "TentCamp", new Vector3(0f, 0f, -2.5f));
            Color fabric = new Color(0.75f, 0.65f, 0.45f);
            Color pole = new Color(0.35f, 0.28f, 0.20f);

            // 마룻대 (좌우 방향으로 눕힌 실린더)
            Prim(PrimitiveType.Cylinder, "RidgePole", hut,
                new Vector3(0f, 2.1f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.1f, 1.7f, 0.1f), pole);
            // 천막 슬랩 2장 — 마룻대에서 앞뒤로 경사
            Prim(PrimitiveType.Cube, "TentF", hut,
                new Vector3(0f, 1.25f, 0.8f), new Vector3(52f, 0f, 0f), new Vector3(3.2f, 0.1f, 2.6f), fabric);
            Prim(PrimitiveType.Cube, "TentB", hut,
                new Vector3(0f, 1.25f, -0.8f), new Vector3(-52f, 0f, 0f), new Vector3(3.2f, 0.1f, 2.6f), fabric);
            // 말뚝 4개
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -1.7f : 1.7f;
                float z = (i < 2) ? -1.5f : 1.5f;
                Prim(PrimitiveType.Cube, $"Stake_{i}", hut,
                    new Vector3(x, 0.2f, z), new Vector3(0f, 0f, (i % 2 == 0) ? 15f : -15f), new Vector3(0.12f, 0.45f, 0.12f), pole);
            }
            // Plaza와 같은 이유로 y를 올린다 — 옛 0.03은 상면이 0.055라 리전 평면(Y=0.08)에
            // 완전히 묻혀 보이지 않았다.
            Prim(PrimitiveType.Cube, "GroundMat", hut,
                new Vector3(0f, 0.12f, 0f), Vector3.zero, new Vector3(2.8f, 0.05f, 2.2f), new Color(0.50f, 0.40f, 0.30f));
            Prim(PrimitiveType.Cube, "Crate", hut,
                new Vector3(2.4f, 0.35f, -0.6f), new Vector3(0f, 20f, 0f), new Vector3(0.7f, 0.7f, 0.7f), new Color(0.48f, 0.36f, 0.22f));
        }

        // ================= 공용 헬퍼 =================

        /// <summary>프리미티브 생성 + 로컬 배치 + 캐시 머티리얼 적용. 기본은 콜라이더 제거(장식), keepCollider=true는 건물 벽 전용.</summary>
        private GameObject Prim(PrimitiveType type, string name, Transform parent,
            Vector3 localPos, Vector3 localEuler, Vector3 localScale, Color color, bool keepCollider = false)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPos;
            obj.transform.localRotation = Quaternion.Euler(localEuler);
            obj.transform.localScale = localScale;

            MeshRenderer mr = obj.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = Mat(color);

            if (!keepCollider)
            {
                Collider col = obj.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
            return obj;
        }

        /// <summary>빈 자식 노드 생성 (로컬 좌표 지정).</summary>
        private static Transform Child(Transform parent, string name, Vector3 localPos)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPos;
            return obj.transform;
        }

        /// <summary>월드 위치에 두고 faceTarget(XZ)을 바라보는 루트 생성 — 건물 정면(+Z)이 광장/리전 중심을 향함.</summary>
        private Transform FacingRoot(string name, Transform parent, Vector3 worldPos, Vector3 faceTarget)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, true);
            obj.transform.position = worldPos;
            Vector3 dir = faceTarget - worldPos;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                obj.transform.rotation = Quaternion.LookRotation(dir.normalized);
            }
            return obj.transform;
        }

        /// <summary>
        /// 간판 — 어두운 판 + TextMesh (수문장 라벨 관례: characterSize/fontSize/anchor/alignment).
        /// TextMesh는 -Z 방향에서 읽히므로 로컬 yaw 180° — 건물 정면(+Z, 광장 방향)에서 정상 판독.
        /// </summary>
        private void CreateSign(Transform building, string label, Vector3 plateLocalPos, float plateWidth)
        {
            Prim(PrimitiveType.Cube, "SignPlate", building,
                plateLocalPos, Vector3.zero, new Vector3(plateWidth, 0.9f, 0.15f), new Color(0.28f, 0.18f, 0.10f));

            GameObject textObj = new GameObject("SignText");
            textObj.transform.SetParent(building, false);
            textObj.transform.localPosition = plateLocalPos + new Vector3(0f, 0f, 0.12f);
            textObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh text = textObj.AddComponent<TextMesh>();
            text.text = label;
            text.characterSize = 0.14f;
            text.fontSize = 48;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(1f, 0.95f, 0.80f);
        }

        /// <summary>색상당 1개 머티리얼 캐시 — RegionTerrainBuilder/부트스트랩과 동일한 셰이더 fallback 체인.</summary>
        private Material Mat(Color color)
        {
            if (materialCache.TryGetValue(color, out Material cached)) return cached;

            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material mat = shader != null ? new Material(shader) : new Material(Shader.Find("Hidden/InternalErrorShader"));
            mat.color = color;
            materialCache[color] = mat;
            return mat;
        }

        /// <summary>center에서 angleDeg(atan2(z,x) 기준) 방향으로 dist만큼 떨어진 XZ 좌표.</summary>
        private static Vector3 Polar(Vector3 center, float angleDeg, float dist)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Cos(rad) * dist, 0f, Mathf.Sin(rad) * dist);
        }

        /// <summary>from → to 수평 단위 벡터.</summary>
        private static Vector3 DirTo(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            return d.sqrMagnitude > 0.001f ? d.normalized : Vector3.forward;
        }

        private static Data.RegionData FindRegion(Data.RegionData[] regions, string regionId)
        {
            foreach (var r in regions)
            {
                if (r != null && r.regionId == regionId) return r;
            }
            return null;
        }
    }
}
