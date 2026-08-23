using System.Collections;
using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 명부회 오염 거점의 <b>보이는 부분</b> — 구조물·오염 안개·지면 탈색, 그리고 정화 시 붕괴.
    ///
    /// 에셋을 하나도 쓰지 않는다. 이 저장소에는 <c>.anim</c>도 <c>.controller</c>도 없고
    /// 파티클 프리팹도 배선돼 있지 않다 — 전부 프리미티브 조립 + 코루틴 보간이다
    /// (<c>BattleArenaController</c>의 임팩트 연출과 <c>PlaySceneBootstrap.BuildGuardianSeal</c>의
    /// 상주 오브젝트 생명주기가 그 본보기다).
    ///
    /// <b>RenderSettings를 절대 만지지 않는다.</b> 안개를 전역 fog로 만들면 서브에리어에 한 번
    /// 들어갔다 나오는 순간 조용히 사라진다 — <see cref="SubAreaEnvironment"/>가 Start에서
    /// 기본값을 <b>1회</b> 스냅샷하고 매 프레임 <c>fog</c>/<c>ambientLight</c>/카메라 배경을
    /// 덮어쓰는데, 나중에 켠 오염 안개는 그 스냅샷에 없기 때문이다. 게다가 fog는 전역이라
    /// 리전 하나만 흐리게 할 수도 없다. 그래서 안개도 <b>리전 안에 놓는 반투명 물체</b>다.
    /// <c>blight_lint</c>가 이 파일에 <c>RenderSettings.</c>가 없는지 검사한다.
    ///
    /// 지형 소품(<c>Scenery_*</c>)은 씬 루트에 평면 배치되고 이름에 리전 ID가 없어 골라낼 수
    /// 없다. 리전을 특정할 수 있는 오브젝트는 <c>Region_{regionId}</c> 지면 하나뿐이라,
    /// 탈색 대상도 그 하나로 좁힌다.
    /// </summary>
    public class BlightVfx : MonoBehaviour
    {
        // ── 배치 ──
        /// <summary>거점 구조물을 하수 뒤 몇 m에 세우는가(플레이어가 그 사람 너머로 보게 된다).</summary>
        private const float SiteBehindNpc = 5.5f;
        /// <summary>NPC를 아직 못 찾았을 때 다시 시도하기까지의 간격(초).</summary>
        private const float RetrySeconds = 1.5f;

        // ── 오염 안개 ──
        // 배치모드 캡처로 실제 화면을 보고 잡은 값이다. 처음엔 7개 × 지름 2.5 × 알파 0.30이었는데
        // **안개가 아니라 창백한 풍선처럼 보였다** — 구체가 크면 조명이 표면에 얹혀 덩어리로 읽히고,
        // 구조물을 가려 거점이 무엇인지도 안 보였다. 작고 옅게, 대신 수를 늘린다.
        private const int HazeCount = 11;
        private const float HazeRadius = 4.6f;
        private const float HazeRiseSeconds = 5.5f;
        private const float HazeMinScale = 0.42f;
        private const float HazeGrowth = 0.75f;

        // ── 정화 연출 ──
        private const float CollapseSeconds = 1.5f;
        private const float ShockwaveSeconds = 0.9f;
        private const float GroundRestoreSeconds = 2.2f;

        private RegionManager regionManager;
        private RegionBlightManager blight;
        private NPC.NpcManager npcManager;

        private sealed class Site
        {
            public GameObject root;
            public Renderer ground;
            public Color groundOriginal;
            public bool groundTinted;
            public Coroutine haze;
            /// <summary>붕괴가 시작됐다 — 완주 전에 또 시작하지 않기 위한 가드.</summary>
            public bool collapsing;

            /// <summary>
            /// 이 거점이 만든 런타임 머티리얼. <b>GameObject를 지워도 머티리얼은 안 지워진다</b> —
            /// <c>SubAreaWorldBuilder</c>가 정확히 같은 결함을 한 번 고쳤고(그 파일의
            /// <c>DestroySubAreaWorld</c> 주석), 텍스처 쪽도 세 파일에서 같은 이유로 고쳤다.
            /// 로그아웃·계정삭제가 씬을 재로드해도 런타임 에셋은 남는다.
            /// </summary>
            public readonly List<Material> materials = new List<Material>();
        }

        private readonly Dictionary<string, Site> sites = new Dictionary<string, Site>();

        /// <summary>보스를 못 찾았다고 이미 경고한 리전 — 매 주기 같은 경고를 쏟지 않는다.</summary>
        private readonly HashSet<string> warnedMissingBoss = new HashSet<string>();
        private float retryTimer;

        public void AutoWire(RegionManager region, RegionBlightManager blightManager,
            NPC.NpcManager npcs)
        {
            if (regionManager == null) regionManager = region;
            if (npcManager == null) npcManager = npcs;

            if (blight != blightManager)
            {
                if (blight != null) blight.RegionCleansed -= OnRegionCleansed;
                blight = blightManager;
                if (blight != null) blight.RegionCleansed += OnRegionCleansed;
            }
        }

        private void OnDestroy()
        {
            if (blight != null) blight.RegionCleansed -= OnRegionCleansed;

            // 정화되지 않은 채 씬이 내려가는 거점 — GameObject는 씬과 함께 사라지지만
            // 런타임 머티리얼은 남는다. 지면 머티리얼은 이쪽 소유가 아니므로 색만 되돌린다.
            foreach (Site site in sites.Values)
            {
                RestoreGround(site);
                ReleaseMaterials(site.materials);
            }
            sites.Clear();
        }

        /// <summary>런타임 머티리얼 일괄 파기 — <c>SubAreaWorldBuilder.DestroySubAreaWorld</c>와 같은 형태.</summary>
        private static void ReleaseMaterials(List<Material> bag)
        {
            if (bag == null) return;
            for (int i = 0; i < bag.Count; i++)
                if (bag[i] != null) Destroy(bag[i]);
            bag.Clear();
        }

        private void Update()
        {
            if (blight == null || regionManager == null) return;

            // 매 프레임 훑지 않는다 — 거점은 둘뿐이고 NPC가 뜨기를 기다리는 것이 전부다.
            retryTimer -= Time.deltaTime;
            if (retryTimer > 0f) return;
            retryTimer = RetrySeconds;

            // 서브에리어에 있는 동안에는 세우지 않는다. **CurrentRegion은 그때도 부모 리전을
            // 그대로 가리키므로** 이 가드가 없으면 동굴 안에서 거점이 지어지는데, 그 시점엔
            // SubAreaWorldBuilder.HideMainWorld가 `Region_*` 지면을 SetActive(false)로 꺼 둔 상태다.
            // GameObject.Find는 비활성 오브젝트를 못 찾으므로 TintGround가 조용히 실패하고,
            // sites에 이미 등록돼 다시 시도하지도 않는다 — **그 세션 내내 그 리전만 탈색이 빠진다.**
            if (regionManager.CurrentSubArea != null) return;

            RegionData here = regionManager.CurrentRegion;
            if (here == null || !here.HasBlightSite) return;

            if (sites.TryGetValue(here.regionId, out Site standing))
            {
                // 이미 세운 거점이 있다 — 그 사이에 정화 상태가 **밖에서** 뒤집혔을 수 있다.
                // RegionBlightManager.ReloadFromDisk는 로그인 직후 컷신이 쏟아지지 않게
                // 의도적으로 RegionCleansed를 울리지 않으므로(그쪽 주석), 다른 기기에서 정화한
                // 진행이 클라우드로 들어오면 여기서만 따라잡을 수 있다.
                if (!standing.collapsing && !blight.IsBlighted(here.regionId))
                    BeginCollapse(here.regionId, standing);
                return;
            }

            if (!blight.IsBlighted(here.regionId)) return;
            BuildSite(here);
        }

        // ── 거점 세우기 ──

        /// <summary>
        /// 거점을 <b>플레이어가 그 리전에 들어왔을 때</b> 세운다.
        ///
        /// 부팅 시 전부 세우지 않는 이유는 둘이다: NPC가 아직 안 떠 있어 위치를 못 잡고,
        /// 어차피 보이지도 않는 곳에 프리미티브를 쌓아 둘 이유가 없다. 그리고 <b>정화 상태는
        /// 매번 다시 확인한다</b> — 다른 기기에서 정화한 뒤 이 기기로 들어올 수 있다.
        /// </summary>
        private void BuildSite(RegionData region)
        {
            if (!TryResolveSitePosition(region, out Vector3 pos))
            {
                // NPC가 아직 안 떴을 수 있으니 매번 경고하지는 않는다. 다만 계속 못 찾으면
                // 그 리전은 거점이 영영 안 서는데 아무 흔적도 안 남는다 — 한 번은 말한다.
                if (warnedMissingBoss.Add(region.regionId))
                {
                    Debug.LogWarning($"[BlightVfx] {region.regionId}의 거점 보스"
                        + $" '{region.blightBossNpcId}'를 월드에서 못 찾아 거점을 세우지 못한다"
                        + $" (스토리 NPC {(npcManager != null ? npcManager.StoryNpcs.Count : -1)}명 조회)"
                        + " — VillageBuilder 앵커의 regionId/storyNpcId를 확인할 것");
                }
                return;
            }

            Site site = new Site();
            // **`Scenery_` 프리픽스가 기능이다.** SubAreaWorldBuilder.HideMainWorld가 메인월드를
            // 이름 프리픽스로 골라 끄고 되살리는데(Region_/Scenery_/Path_ 등), 여기서 빠지면
            // 서브에리어 진입 중 혼자 살아남는 메인월드 소품이 된다. 지금은 서브에리어 원점이
            // 2km 밖이라 화면에 안 잡히지만, 그 거리에 기대는 것보다 기존 경로에 얹는 게 맞다.
            site.root = new GameObject("Scenery_BlightSite_" + region.regionId);
            site.root.transform.position = pos;
            // 리전 중심을 등지게 세운다 — 플레이어는 대개 중심 쪽에서 다가온다.
            Vector3 outward = pos - region.centerPosition;
            outward.y = 0f;
            if (outward.sqrMagnitude > 0.01f)
                site.root.transform.rotation = Quaternion.LookRotation(outward.normalized);

            BuildStructure(site.root.transform, region.regionId, site.materials);
            TintGround(region, site);
            site.haze = StartCoroutine(HazeLoop(site.root.transform, site.materials, region.regionId));

            sites[region.regionId] = site;
        }

        /// <summary>
        /// 거점 좌표 — 그 리전에 선 명부회 하수의 <b>뒤쪽</b>.
        ///
        /// 좌표를 여기 박지 않는다. <c>VillageBuilder</c>가 하수 앵커를 극좌표로 잡는데,
        /// 그 각도를 여기 베껴 두면 저쪽이 바뀔 때 거점만 엉뚱한 데 남는다(이 저장소에서
        /// 하드코딩 좌표 사본이 실제로 어긋난 적이 있다 — 수문장이 리전 밖에 섰다).
        /// 대신 실물 NPC를 찾아 그 뒤에 세운다.
        /// </summary>
        private bool TryResolveSitePosition(RegionData region, out Vector3 pos)
        {
            pos = Vector3.zero;
            if (npcManager == null) return false;

            var list = npcManager.StoryNpcs;
            for (int i = 0; i < list.Count; i++)
            {
                NPC.VillagerNpc npc = list[i];
                if (npc == null) continue;
                if (npc.StoryNpcId != region.blightBossNpcId) continue;
                if (npc.RegionId != region.regionId) continue;

                Vector3 outward = npc.transform.position - region.centerPosition;
                outward.y = 0f;
                if (outward.sqrMagnitude < 0.01f) outward = Vector3.forward;
                pos = npc.transform.position + outward.normalized * SiteBehindNpc;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 집하장 — 기둥에 걸린 그물과 쌓아 둔 상자. <b>거점 전부가 같은 형태를 쓴다</b>:
        /// 숲은 치는 현장, 산은 걷는 현장, 유적은 쌓는 현장이지만 <b>같은 조직의 같은 장비</b>라는
        /// 인상이 더 중요하다(하수 셋에게 같은 상의를 입힌 것과 같은 이유).
        /// 리전 ID로 난수를 고정해 곳마다 상자 배치만 달라진다.
        /// </summary>
        private void BuildStructure(Transform root, string regionId, List<Material> bag)
        {
            Color crateCol = new Color(0.34f, 0.29f, 0.22f);
            Color netCol = new Color(0.12f, 0.12f, 0.14f, 0.55f);

            // 색이 같은 파츠는 머티리얼 하나를 나눠 쓴다 — 기둥 4개·상자 대여섯 개에 각각
            // 새 머티리얼을 만들면 거점 하나에 20개 넘게 쌓인다. 색이 개별로 변하는 것은
            // 안개 구체뿐이라(HazeLoop) 나머지는 공유해도 서로 간섭하지 않는다.
            Material woodMat = Track(bag, Mat(new Color(0.20f, 0.19f, 0.21f)));
            Material crateMat = Track(bag, Mat(crateCol));
            Material netMat = Track(bag, Transparent(netCol));

            // 기둥 4개 + 가로대 — 그물을 거는 틀.
            float halfW = 2.6f, halfD = 1.4f, postH = 3.1f;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -halfW : halfW;
                float sz = (i < 2) ? -halfD : halfD;
                GameObject post = Prim(PrimitiveType.Cylinder, "Post", root, woodMat);
                post.transform.localPosition = new Vector3(sx, postH * 0.5f, sz);
                post.transform.localScale = new Vector3(0.16f, postH * 0.5f, 0.16f);
            }
            GameObject beam = Prim(PrimitiveType.Cube, "Beam", root, woodMat);
            beam.transform.localPosition = new Vector3(0f, postH, 0f);
            beam.transform.localScale = new Vector3(halfW * 2f + 0.3f, 0.18f, 0.18f);

            // 그물 — 반투명 판 두 장을 살짝 기울여 늘어진 인상을 만든다.
            for (int i = 0; i < 2; i++)
            {
                GameObject net = Prim(PrimitiveType.Cube, "Net", root, netMat);
                net.transform.localPosition = new Vector3(i == 0 ? -1.3f : 1.3f, postH * 0.55f, 0f);
                net.transform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 7f : -7f);
                net.transform.localScale = new Vector3(2.3f, postH * 0.9f, 0.05f);
            }

            // 상자 — 리전마다 다르게 쌓는다. 전역 난수를 건드리지 않으려고 자체 난수를 쓴다
            // (RegionTerrainBuilder가 Random.state를 저장·복원하는 것과 같은 이유 —
            //  여기서 전역 시드를 흔들면 스폰·IV·포획 판정이 함께 흔들린다).
            System.Random rng = new System.Random(StableSeed(regionId));
            int crates = 5 + rng.Next(0, 4);
            for (int i = 0; i < crates; i++)
            {
                GameObject crate = Prim(PrimitiveType.Cube, "Crate", root, crateMat);
                float cx = -2.2f + (float)rng.NextDouble() * 4.4f;
                float cz = 1.9f + (float)rng.NextDouble() * 1.6f;
                float cy = 0.36f + (i % 3) * 0.72f;
                crate.transform.localPosition = new Vector3(cx, cy, cz);
                crate.transform.localScale = new Vector3(0.72f, 0.7f, 0.72f);
                crate.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 40f - 20f, 0f);
            }

            // 장부대 — 상자 옆의 낮은 받침. 여기가 "이름표를 붙이는 자리"다.
            GameObject desk = Prim(PrimitiveType.Cube, "LedgerDesk", root,
                Track(bag, Mat(new Color(0.28f, 0.24f, 0.20f))));
            desk.transform.localPosition = new Vector3(2.9f, 0.5f, 0.2f);
            desk.transform.localScale = new Vector3(1.1f, 0.1f, 0.7f);
        }

        // ── 지면 탈색 ──

        /// <summary>
        /// <c>Region_{regionId}</c> 지면의 색을 뺀다. 원본 색을 반드시 캐시한다 —
        /// 지면 머티리얼은 부트스트랩이 <c>themeColor</c>에서 즉석으로 만든 것이라
        /// 되돌릴 원본이 씬 어디에도 남아 있지 않다.
        /// </summary>
        private void TintGround(RegionData region, Site site)
        {
            GameObject go = GameObject.Find("Region_" + region.regionId);
            Renderer r = go != null ? go.GetComponent<Renderer>() : null;
            if (r == null || r.material == null)
            {
                // 조용히 넘어가면 그 리전만 탈색이 빠진 채로 남고 아무도 이유를 모른다.
                // 구조물과 안개는 그대로 세운다 — 거점이 있다는 것 자체는 여전히 보여야 한다.
                Debug.LogWarning("[BlightVfx] Region_" + region.regionId
                    + " 지면을 찾지 못해 탈색을 건너뛴다 — 부트스트랩이 리전 지면을 만들었는가?");
                return;
            }

            site.ground = r;
            site.groundOriginal = r.material.color;
            site.groundTinted = true;
            r.material.color = BlightPolicy.TintOf(site.groundOriginal);
        }

        // ── 오염 안개 ──

        /// <summary>
        /// 거점 주위를 도는 반투명 구체 — 올라가며 옅어지고 다시 바닥에서 시작한다.
        /// <c>BattleArenaController.PoisonImpact3D</c>의 형태를 상주형으로 바꾼 것이다.
        /// 거점 root의 자식이라 정화 때 구조물과 함께 사라진다.
        /// </summary>
        private IEnumerator HazeLoop(Transform root, List<Material> bag, string regionId)
        {
            // 다른 리전에 있는 동안에는 쉰다. 거점은 세션 내내 서 있으므로 게이트가 없으면
            // 방문한 거점 수만큼의 안개가 끝까지 매 프레임 돈다 — 보이지도 않는 채로.
            WaitForSeconds idle = new WaitForSeconds(0.5f);
            Color hazeCol = new Color(0.42f, 0.44f, 0.33f, 0.16f);
            GameObject[] puffs = new GameObject[HazeCount];
            Renderer[] rends = new Renderer[HazeCount];
            float[] phase = new float[HazeCount];

            for (int i = 0; i < HazeCount; i++)
            {
                // 알파가 개체마다 따로 움직여야 해서 여기만 공유하지 않는다.
                puffs[i] = Prim(PrimitiveType.Sphere, "Haze", root, Track(bag, Transparent(hazeCol)));
                rends[i] = puffs[i].GetComponent<Renderer>();
                phase[i] = i / (float)HazeCount;   // 고르게 흩어 놓아 한꺼번에 튀지 않게 한다
            }

            while (root != null)
            {
                bool here = regionManager != null && regionManager.CurrentRegion != null
                    && regionManager.CurrentRegion.regionId == regionId;
                if (!here) { yield return idle; continue; }

                for (int i = 0; i < HazeCount; i++)
                {
                    if (puffs[i] == null) yield break;

                    phase[i] += Time.deltaTime / HazeRiseSeconds;
                    if (phase[i] > 1f) phase[i] -= 1f;

                    float t = phase[i];
                    float angle = (i * Mathf.PI * 2f / HazeCount) + t * 1.4f;
                    float radius = HazeRadius * (0.3f + t * 0.7f);
                    puffs[i].transform.localPosition = new Vector3(
                        Mathf.Cos(angle) * radius, 0.25f + t * 2.6f, Mathf.Sin(angle) * radius);
                    puffs[i].transform.localScale = Vector3.one * (HazeMinScale + t * HazeGrowth);

                    // 바닥에서 피어나 꼭대기에서 사라진다 — 양 끝에서 알파가 0이라 튀지 않는다.
                    float alpha = hazeCol.a * Mathf.Sin(t * Mathf.PI);
                    if (rends[i] != null && rends[i].material != null)
                        rends[i].material.color = new Color(hazeCol.r, hazeCol.g, hazeCol.b, alpha);
                }
                yield return null;
            }
        }

        // ── 정화 ──

        private void OnRegionCleansed(string regionId)
        {
            if (!sites.TryGetValue(regionId, out Site site) || site.collapsing) return;
            BeginCollapse(regionId, site);
        }

        /// <summary>
        /// 붕괴를 시작한다. <b>여기서 dict에서 빼지 않는다</b> — 코루틴이 완주하지 못하면
        /// (컴포넌트 비활성·파기) 구조물은 선 채 지면은 탈색된 채 남는데, 그 시점엔
        /// <c>IsBlighted</c>가 이미 false라 <c>Update</c>의 재건 경로도 닫혀 있어
        /// **되돌릴 방법이 없다.** 제거는 <see cref="FinishCollapse"/>가 맡는다.
        /// </summary>
        private void BeginCollapse(string regionId, Site site)
        {
            site.collapsing = true;

            // 비활성 상태에서는 StartCoroutine이 예외를 던진다(형제 구독자
            // InsectSpawner.OnRegionCleansed가 같은 가드를 둔다). 연출을 포기하고
            // 상태만 즉시 정리한다 — 남겨 두는 쪽이 훨씬 나쁘다.
            if (!isActiveAndEnabled)
            {
                FinishCollapse(regionId, site);
                return;
            }
            StartCoroutine(CollapseSite(regionId, site));
        }

        /// <summary>붕괴 마무리 — 지면 복원·오브젝트 파기·머티리얼 회수·등록 해제를 한자리에 모은다.</summary>
        private void FinishCollapse(string regionId, Site site)
        {
            RestoreGround(site);
            if (site.root != null) Destroy(site.root);
            ReleaseMaterials(site.materials);
            sites.Remove(regionId);
        }

        /// <summary>탈색해 둔 지면을 원래 색으로 되돌린다(보간 오차를 남기지 않는다).</summary>
        private static void RestoreGround(Site site)
        {
            if (!site.groundTinted || site.ground == null || site.ground.material == null) return;
            site.ground.material.color = site.groundOriginal;
            site.groundTinted = false;
        }

        /// <summary>
        /// 거점이 무너진다 — 충격파 링이 퍼지고 구조물이 주저앉으며 지면 색이 돌아온다.
        ///
        /// 세 가지가 <b>같은 시간축에서</b> 일어나야 한 사건으로 읽힌다. 순서대로 재생하면
        /// 링이 끝나고 나서 땅이 밝아져 인과가 끊긴다.
        /// </summary>
        private IEnumerator CollapseSite(string regionId, Site site)
        {
            if (site == null) yield break;

            if (site.haze != null) StopCoroutine(site.haze);

            Transform root = site.root != null ? site.root.transform : null;
            if (root != null) StartCoroutine(Shockwave(root.position));

            Vector3 baseScale = root != null ? root.localScale : Vector3.one;
            float t = 0f;
            while (t < CollapseSeconds)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / CollapseSeconds);

                if (root != null)
                {
                    // 세로로 주저앉으면서 살짝 벌어진다 — 무너지는 인상.
                    float squash = Mathf.SmoothStep(1f, 0.05f, p);
                    root.localScale = new Vector3(
                        baseScale.x * Mathf.Lerp(1f, 1.18f, p), baseScale.y * squash,
                        baseScale.z * Mathf.Lerp(1f, 1.18f, p));
                    root.Rotate(0f, 26f * Time.deltaTime, 0f, Space.Self);
                }

                if (site.groundTinted && site.ground != null && site.ground.material != null)
                {
                    // 지면은 조금 더 느리게 돌아온다 — 무너진 다음에도 땅은 잠깐 잿빛이다.
                    float g = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / GroundRestoreSeconds));
                    site.ground.material.color = Color.Lerp(
                        BlightPolicy.TintOf(site.groundOriginal), site.groundOriginal, g);
                }
                yield return null;
            }

            // 지면 복원은 붕괴보다 길 수 있어 남은 구간을 마저 돌린다.
            while (t < GroundRestoreSeconds)
            {
                t += Time.deltaTime;
                if (site.groundTinted && site.ground != null && site.ground.material != null)
                {
                    float g = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / GroundRestoreSeconds));
                    site.ground.material.color = Color.Lerp(
                        BlightPolicy.TintOf(site.groundOriginal), site.groundOriginal, g);
                }
                yield return null;
            }

            // 지면 복원·파기·회수·등록 해제를 한자리에 모아 둔다(비활성 경로와 같은 코드).
            // GameObject만 지우면 머티리얼이 남는다 — 그게 SubAreaWorldBuilder가 겪은 결함이다.
            FinishCollapse(regionId, site);
        }

        /// <summary>
        /// 바닥을 훑고 퍼지는 링 + 흩어지는 조각.
        /// <c>BattleArenaController.ImpactEffectCoroutine</c>과 같은 형태다.
        /// </summary>
        private IEnumerator Shockwave(Vector3 center)
        {
            List<Material> bag = new List<Material>();
            Color ringCol = new Color(0.85f, 0.93f, 0.72f, 0.65f);
            GameObject ring = Prim(PrimitiveType.Cylinder, "CleanseRing", null,
                Track(bag, Transparent(ringCol)));
            ring.transform.position = center + new Vector3(0f, 0.06f, 0f);

            // 조각 10개는 색이 안 변하니 머티리얼 하나를 나눠 쓴다(링만 알파가 줄어든다).
            const int Shards = 10;
            Material shardMat = Track(bag, Mat(new Color(0.30f, 0.28f, 0.24f)));
            GameObject[] shards = new GameObject[Shards];
            for (int i = 0; i < Shards; i++)
            {
                shards[i] = Prim(PrimitiveType.Cube, "Shard", null, shardMat);
                shards[i].transform.position = center + new Vector3(0f, 0.5f, 0f);
            }

            Renderer ringRend = ring.GetComponent<Renderer>();
            float t = 0f;
            while (t < ShockwaveSeconds)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / ShockwaveSeconds);

                float size = 1.2f + p * 11f;
                ring.transform.localScale = new Vector3(size, 0.02f * (1f - p), size);
                if (ringRend != null && ringRend.material != null)
                    ringRend.material.color = new Color(ringCol.r, ringCol.g, ringCol.b, ringCol.a * (1f - p));

                for (int i = 0; i < Shards; i++)
                {
                    if (shards[i] == null) continue;
                    float a = i * Mathf.PI * 2f / Shards;
                    float dist = p * 5.5f;
                    float y = 0.5f + Mathf.Sin(p * Mathf.PI) * 2.4f;   // 던져 올렸다 떨어진다
                    shards[i].transform.position = center + new Vector3(Mathf.Cos(a) * dist, y, Mathf.Sin(a) * dist);
                    shards[i].transform.localScale = Vector3.one * (0.34f * (1f - p));
                    shards[i].transform.Rotate(160f * Time.deltaTime, 120f * Time.deltaTime, 0f);
                }
                yield return null;
            }

            if (ring != null) Destroy(ring);
            for (int i = 0; i < Shards; i++)
                if (shards[i] != null) Destroy(shards[i]);
            ReleaseMaterials(bag);
        }

        // ── 프리미티브 헬퍼 ──

        /// <summary>머티리얼을 회수 목록에 올리고 그대로 돌려준다 — 등록을 빠뜨릴 자리를 없앤다.</summary>
        private static Material Track(List<Material> bag, Material m)
        {
            if (bag != null && m != null) bag.Add(m);
            return m;
        }

        private static GameObject Prim(PrimitiveType type, string name, Transform parent, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.material = mat;
            // 거점은 장식이다 — 콜라이더를 남기면 플레이어가 그물에 끼고 클릭-이동이 막힌다.
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            return go;
        }

        /// <summary>
        /// 셰이더 폴백 체인 — 이 저장소의 다른 빌더들과 같은 순서다.
        /// Built-in 파이프라인이라 Standard가 정상 해석되고 나머지는 방어선이다.
        /// </summary>
        private static Material Mat(Color color)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material m = new Material(shader);
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            return m;
        }

        private static Material Transparent(Color color)
        {
            Material m = Mat(color);
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = 3000;
            return m;
        }

        /// <summary>리전 ID에서 안정적인 난수 시드 — 실행마다 같은 상자 배치가 나온다.</summary>
        private static int StableSeed(string id)
        {
            int h = 17;
            for (int i = 0; i < id.Length; i++) h = h * 31 + id[i];
            return h;
        }
    }
}
