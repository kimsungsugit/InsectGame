using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Dex;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.UI
{
    // 월드 지도 모달 — 리전을 색상 원(disc)으로 그리는 지도 + 선택 리전 정보패널(가로 우측/세로 하단) + 도감 브라우저.
    // 렌더는 안티앨리어싱 disc 텍스처 1개 재사용(MinimapUI.MakeDisc 이식). 데이터·세이브 무변경(UI 전용).
    public class RegionMapUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private RegionManager regionManager;
        [SerializeField] private PlayerProgressController progress;
        [SerializeField] private DexController dex;
        [SerializeField] private InsectDatabase database;
        [SerializeField] private InsectSpawner spawner;
        // 스토리 목표 마커의 출처. 개체 선택 규칙(현재 리전 우선 → 최근접)과 라벨 조립이
        // 전부 저쪽에 있어서 여기는 좌표와 문구를 받아 그리기만 한다.
        private InsectGame.Story.StoryObjectiveTracker objectiveTracker;

        private InsectGame.NPC.NpcManager npcManager;
        public void AutoWire(InsectGame.NPC.NpcManager manager)
        {
            if (npcManager == null) npcManager = manager;
        }

        private bool isOpen;
        private string selectedRegionId;   // 지도에서 선택돼 정보패널에 표시되는 리전(도감 아님)
        private bool dexOpen;              // 도감 브라우저 열림(정보패널 [도감]이 켬)
        private Vector2 dexScroll;
        private readonly UIDirectScroll dexDirectScroll = new UIDirectScroll();
        private Transform playerTransform; // 최초 1회 탐색 후 캐시(GameObject.Find 매 프레임 회피)

        private readonly List<RaidBossMarker> raidMarkers = new List<RaidBossMarker>();
        private string errorMessage = "";
        private float errorTimer;

        // 도감 브라우저 행 텍스트 캐시 — 리전 곤충 수(최대 20여)만큼의 라벨을 **OnGUI 패스마다**
        // 새로 만들던 자리다(OnGUI는 프레임당 Layout+Repaint+입력마다 돈다).
        // 둘 다 종 데이터에서만 파생되므로 세션 내내 불변이다:
        //   - "등급 | CP n" : rarity와 minLevel 기준 CP는 안 바뀐다(보간 + enum 박싱 + CP 재계산이었다)
        //   - "???"        : displayName 길이에만 의존한다(`new string('?', n)`이었다)
        // 2026-05-27 캐시 라운드는 GUIStyle 28개를 다뤘고 문자열 할당은 범위 밖이었다.
        private readonly Dictionary<string, string> dexInfoCache = new Dictionary<string, string>();
        private readonly Dictionary<int, string> hiddenNameCache = new Dictionary<int, string>();

        private RegionData[] routeRegions;
        private readonly List<Vector3[]> mapRoutes = new List<Vector3[]>();

        private void EnsureMapRoutes(RegionData[] regions)
        {
            if (ReferenceEquals(routeRegions, regions)) return;
            routeRegions = regions;
            mapRoutes.Clear();
            foreach (WorldRouteEdge edge in WorldRouteLayout.FieldConnections)
                mapRoutes.Add(WorldRouteLayout.BuildRoute(regions, edge.FromRegionId, edge.ToRegionId));
        }

        private struct RaidBossMarker
        {
            public string name;
            public Vector3 worldPos;
            public InsectRarity rarity;
            public InsectEntity entity;
            // 표식을 단 순간의 몸 번호 — 풀 객체라 같은 참조가 다른 개체로 되살아난다(InsectEntity.SpawnSerial).
            public int serial;
        }

        public bool IsOpen => isOpen;
        public void Toggle()
        {
            isOpen = !isOpen;
            if (!isOpen)
            {
                selectedRegionId = null;
                dexOpen = false;
                dexDirectScroll.Reset();
            }
            else
            {
                dexScroll = Vector2.zero;
                dexDirectScroll.Reset();
            }
            if (isOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }
        public void CloseModal()
        {
            isOpen = false;
            selectedRegionId = null;
            dexOpen = false;
            dexDirectScroll.Reset();
            ModalUIRegistry.Unregister(this);
        }

        private void OnEnable()
        {
            // OnDisable이 해지한 구독을 되살린다 — 오프닝 다시보기(OpeningReplayCoordinator)가
            // UI 루트를 통째로 SetActive 토글하므로, 없으면 다시보기 한 번에 레이드 보스 마커가
            // 영구히 안 뜬다. 해지 뒤 구독이라 중복되지 않는다.
            if (spawner != null)
            {
                spawner.RaidBossSpawned -= OnRaidBossSpawned;
                spawner.RaidBossSpawned += OnRaidBossSpawned;
            }
        }

        private void OnDisable()
        {
            if (spawner != null) spawner.RaidBossSpawned -= OnRaidBossSpawned;
            // GO SetActive 토글 시 isOpen 잔존 + Registry 미등록 stale 모달 방지.
            isOpen = false;
            selectedRegionId = null;
            dexOpen = false;
            dexDirectScroll.Reset();
            ModalUIRegistry.Unregister(this);
        }

        // ─── 자산(GUIStyle + disc 텍스처) — mapStylesReady 가드로 1회 할당(프레임당 new 회피) ───
        private Texture2D discTex;
        private GUIStyle titleStyle, closeStyle, levelStyle, noDataStyle;
        private GUIStyle regionNameStyle, subNameStyle, raidLabelStyle, guardianStyle, mapErrStyle;
        private GUIStyle objectiveStyle, taleBadgeStyle;
        private GUIStyle infoTitleStyle, infoLineStyle, legendStyle, btnStyle, hintStyle;
        private GUIStyle detailTitleStyle, detailBtnStyle, detailDescStyle, detailSummaryStyle, detailNoInsectStyle;
        private GUIStyle dexNameStyle, dexInfoStyle, dexCheckStyle, dexUnknownStyle, dexHiddenStyle, dexNotCaughtStyle;
        private bool ready;

        private void EnsureAssets()
        {
            if (ready) return;
            ready = true;

            // 소프트 디스크는 UIShapes가 소유한다 — 예전엔 여기와 MinimapUI가 각각 private
            // 사본을 만들었다(이쪽은 소프트 엣지, 저쪽은 하드 엣지로 미묘하게 다르기까지 했다).
            discTex = UIShapes.Disc;

            titleStyle = Label(40, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            closeStyle = new GUIStyle(GUI.skin.button) { fontSize = 34, fontStyle = FontStyle.Bold };
            levelStyle = Label(26, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.7f, 0.85f, 1f));
            noDataStyle = Label(30, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.55f, 0.55f, 0.6f));

            regionNameStyle = Label(24, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            subNameStyle = Label(17, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.85f, 0.88f, 0.95f));
            raidLabelStyle = Label(17, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            guardianStyle = Label(16, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.75f, 0.55f));
            objectiveStyle = Label(19, FontStyle.Bold, TextAnchor.MiddleCenter, UITheme.Instance.accentMint);
            // 의뢰 주민 배지 안의 !/? — 밝은 원 위에 올라가므로 어두운 글자.
            taleBadgeStyle = Label(20, FontStyle.Bold, TextAnchor.MiddleCenter, UITheme.Instance.surfaceBase);
            mapErrStyle = Label(26, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.6f, 0.6f));

            infoTitleStyle = Label(34, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            infoLineStyle = Label(24, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.82f, 0.85f, 0.9f));
            legendStyle = Label(22, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.8f, 0.82f, 0.88f));
            hintStyle = Label(23, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.6f, 0.62f, 0.7f));
            btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 27, fontStyle = FontStyle.Bold };

            detailTitleStyle = Label(38, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            detailBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 30, fontStyle = FontStyle.Bold };
            detailDescStyle = Label(24, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.7f, 0.72f, 0.78f));
            detailSummaryStyle = Label(28, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.7f, 0.85f, 1f));
            detailNoInsectStyle = Label(26, FontStyle.Italic, TextAnchor.MiddleCenter, new Color(0.5f, 0.5f, 0.55f));

            dexNameStyle = Label(32, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            dexInfoStyle = Label(24, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.62f, 0.64f, 0.7f));
            dexCheckStyle = Label(38, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.3f, 1f, 0.5f));
            dexUnknownStyle = Label(46, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.35f, 0.35f, 0.4f));
            dexHiddenStyle = Label(32, FontStyle.Italic, TextAnchor.MiddleLeft, new Color(0.42f, 0.42f, 0.48f));
            dexNotCaughtStyle = Label(24, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.4f, 0.4f, 0.45f));
        }

        private static GUIStyle Label(int size, FontStyle fs, TextAnchor anchor, Color col)
        {
            // wordWrap=false — GUI.skin.label 기본값이 true라 좁은 rect에서 2줄로 접혀 잘림. 지도 라벨은 1줄 고정.
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, alignment = anchor, wordWrap = false };
            s.normal.textColor = col;
            return s;
        }


        private void OnRaidBossSpawned(InsectEntity entity)
        {
            if (entity == null || entity.Data == null) return;
            raidMarkers.Add(new RaidBossMarker
            {
                name = entity.Data.displayName,
                worldPos = entity.transform.position,
                rarity = entity.Data.rarity,
                entity = entity,
                serial = entity.SpawnSerial
            });
        }

        private void Update()
        {
            for (int i = raidMarkers.Count - 1; i >= 0; i--)
            {
                // 몸 번호가 바뀌었으면 풀로 돌아갔다가 다른 개체로 다시 선 것이다 — 활성 여부만으론 못 가린다.
                if (raidMarkers[i].entity == null || !raidMarkers[i].entity.gameObject.activeInHierarchy
                    || raidMarkers[i].entity.SpawnSerial != raidMarkers[i].serial)
                    raidMarkers.RemoveAt(i);
            }
            if (errorTimer > 0f) errorTimer -= Time.deltaTime;
        }

        private Transform GetPlayer()
        {
            if (playerTransform == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p == null) p = GameObject.Find("Player");
                if (p != null) playerTransform = p.transform;
            }
            return playerTransform;
        }

        private void OnGUI()
        {
            if (!isOpen) return;

            UIScale.Begin();
            EnsureAssets();
            if (dexOpen) DrawDexBrowser();
            else DrawMap();
            DrawErrorToast();
            UIScale.End();
        }

        // ─── 패널 지오메트리(세이프에어리어 + 세로 마진은 UISafeLayout이 처리) ───
        private void PanelRect(out float px, out float py, out float pw, out float ph)
        {
            Rect panel = UISafeLayout.CenteredPanel(1160f, 980f);
            px = panel.x;
            py = panel.y;
            pw = panel.width;
            ph = panel.height;
        }

        /// <param name="titleInset">
        /// 제목 왼쪽에 비워 둘 폭. 헤더 왼쪽에 버튼을 얹는 화면이 넘긴다 — <b>제목을 먼저
        /// 그리고 버튼을 나중에 그리므로 안 비우면 제목이 버튼 밑에 깔린다.</b> 실제로 도감
        /// 브라우저가 그랬다: 헤더가 "{리전} 도감"인데 왼쪽 150px을 뒤로 버튼이 덮어
        /// <b>리전 이름이 통째로 가려졌다</b>(어느 지역 도감인지가 그 제목에만 있다).
        /// </param>
        private void DrawPanelFrame(float px, float py, float pw, float ph, string title, Color headerAccent,
            float titleInset = 0f)
        {
            UITheme t = UITheme.Instance;
            // 이 화면의 모든 패널이 이 프레임 하나를 지나므로 여기만 바꾸면 지도·상세가 함께 둥글어진다.
            UISurface.Card(new Rect(px, py, pw, ph), t.panelBg, t.surfaceBorder);
            UISurface.Rounded(new Rect(px + 3f, py + 3f, pw - 6f, 84f), t.panelHeaderBg);
            GUI.color = headerAccent;
            GUI.DrawTexture(new Rect(px + 3f, py + 84f, pw - 6f, 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            UIHelper.LabelFit(new Rect(px + 26f + titleInset, py + 12f, pw - 200f - titleInset, 60f),
                title, titleStyle);
        }

        private void DrawMap()
        {
            PanelRect(out float px, out float py, out float pw, out float ph);
            DrawPanelFrame(px, py, pw, ph, "월드 지도", new Color(0.3f, 0.5f, 0.8f));

            int playerLv = progress != null ? progress.Level : 1;
            GUI.Label(new Rect(px + pw - 400f, py + 26f, 280f, 34f), $"플레이어 레벨 {playerLv}", levelStyle);

            if (GUI.Button(new Rect(px + pw - 76f, py + 12f, 60f, 60f), "X", closeStyle))
            {
                CloseModal();
                return;
            }

            if (regionManager == null || regionManager.Regions == null)
            {
                GUI.Label(new Rect(px, py + 84f, pw, ph - 84f), "등록된 지역 없음", noDataStyle);
                return;
            }

            // 콘텐츠 영역 → 지도(주역) + 정보패널(가로 우측 / 세로 하단)
            float cx = px + 20f, cy = py + 100f, cw = pw - 40f, ch = ph - 120f;
            Rect mapRect, infoRect;
            if (UIScale.IsPortrait)
            {
                float mapH = ch * 0.60f;
                mapRect = new Rect(cx, cy, cw, mapH);
                infoRect = new Rect(cx, cy + mapH + 10f, cw, ch - mapH - 10f);
            }
            else
            {
                float mapW = cw * 0.60f;
                mapRect = new Rect(cx, cy, mapW, ch);
                infoRect = new Rect(cx + mapW + 12f, cy, cw - mapW - 12f, ch);
            }

            DrawWorldMap(mapRect);
            DrawRegionInfoPanel(infoRect);
        }

        // ─── 월드→지도 좌표 변환(종횡비 보존 fit) 캐시 ───
        private float wmMinX, wmMinZ, wmW, wmH, wmOx, wmOy, wmDrawW, wmDrawH;

        private Vector2 WorldToMap(float wx, float wz)
        {
            float nx = (wx - wmMinX) / wmW;
            float nz = (wz - wmMinZ) / wmH;
            return new Vector2(wmOx + nx * wmDrawW, wmOy + (1f - nz) * wmDrawH);
        }

        private void DrawWorldMap(Rect area)
        {
            // 지도 배경
            GUI.color = new Color(0.06f, 0.08f, 0.13f, 0.9f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = new Color(0.2f, 0.24f, 0.34f, 0.8f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 2f), Texture2D.whiteTexture);

            RegionData[] regions = regionManager.Regions;
            if (regions == null || regions.Length == 0) return;
            EnsureMapRoutes(regions);

            // 월드 경계 계산
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var r in regions)
            {
                if (r == null) continue;
                minX = Mathf.Min(minX, r.centerPosition.x - r.radius);
                maxX = Mathf.Max(maxX, r.centerPosition.x + r.radius);
                minZ = Mathf.Min(minZ, r.centerPosition.z - r.radius);
                maxZ = Mathf.Max(maxZ, r.centerPosition.z + r.radius);
            }
            foreach (Vector3[] route in mapRoutes)
                foreach (Vector3 point in route)
                {
                    minX = Mathf.Min(minX, point.x); maxX = Mathf.Max(maxX, point.x);
                    minZ = Mathf.Min(minZ, point.z); maxZ = Mathf.Max(maxZ, point.z);
                }
            float pad = 40f;
            minX -= pad; maxX += pad; minZ -= pad; maxZ += pad;
            wmW = Mathf.Max(1f, maxX - minX);
            wmH = Mathf.Max(1f, maxZ - minZ);
            wmMinX = minX; wmMinZ = minZ;

            float fit = Mathf.Min(area.width / wmW, area.height / wmH) * 0.9f;
            wmDrawW = wmW * fit; wmDrawH = wmH * fit;
            wmOx = area.x + (area.width - wmDrawW) * 0.5f;
            wmOy = area.y + (area.height - wmDrawH) * 0.5f;
            float worldToPx = fit;

            // 0) 의뢰 주민 배지 탭 → 따라가기. **리전·서브에리어 버튼보다 먼저** 처리한다 —
            //    IMGUI는 먼저 호출된 GUI.Button이 MouseDown을 가져가는데, 배지는 리전 원 안에 있어
            //    뒤에 두면 배지를 눌러도 리전 선택만 된다. 좌표 변환(wmOx 등)이 위에서 끝난 뒤라야 한다.
            HandleTaleMarkerInput(area);

            // 1) 연결선(길) — 회전 quad로 깔끔하게
            Color pathCol = new Color(0.5f, 0.44f, 0.3f, 0.5f);
            foreach (Vector3[] route in mapRoutes)
                for (int i = 1; i < route.Length; i++)
                    DrawThickLine(WorldToMap(route[i - 1].x, route[i - 1].z),
                        WorldToMap(route[i].x, route[i].z), 3f, pathCol);

            // 2) 리전 원
            foreach (var r in regions)
            {
                if (r == null) continue;
                bool accessible = regionManager.IsRegionAccessible(r);
                bool current = regionManager.CurrentRegion == r;
                Vector2 c = WorldToMap(r.centerPosition.x, r.centerPosition.z);
                float cr = Mathf.Clamp(r.radius * worldToPx, 30f, 130f);

                Color theme = r.themeColor;
                Color fill = accessible ? theme : Desaturate(theme, 0.7f);
                float fillA = accessible ? (current ? 0.85f : 0.6f) : 0.32f;

                // 외곽 링(살짝 큰 disc) → fill disc 순서
                float pulse = current ? (0.75f + 0.25f * Mathf.Sin(Time.time * 3.5f)) : 1f;
                Color ring = current ? new Color(1f, 1f, 1f, 0.95f * pulse)
                                     : new Color(fill.r * 0.6f + 0.3f, fill.g * 0.6f + 0.3f, fill.b * 0.6f + 0.3f, accessible ? 0.8f : 0.5f);
                float ringPad = current ? 5f : 3f;
                GUI.color = ring;
                GUI.DrawTexture(new Rect(c.x - cr - ringPad, c.y - cr - ringPad, (cr + ringPad) * 2f, (cr + ringPad) * 2f), discTex);
                GUI.color = new Color(fill.r, fill.g, fill.b, fillA);
                GUI.DrawTexture(new Rect(c.x - cr, c.y - cr, cr * 2f, cr * 2f), discTex);

                // 이름
                regionNameStyle.normal.textColor = accessible ? Color.white : new Color(0.7f, 0.7f, 0.72f);
                GUI.color = Color.white;


                // 선택 강조 링
                if (selectedRegionId == r.regionId)
                {
                    GUI.color = new Color(1f, 0.9f, 0.4f, 0.9f);
                    float sp = cr + ringPad + 4f;
                    GUI.DrawTexture(new Rect(c.x - sp, c.y - sp, sp * 2f, sp * 2f), discTex);
                    GUI.color = new Color(fill.r, fill.g, fill.b, fillA);
                    GUI.DrawTexture(new Rect(c.x - cr - ringPad, c.y - cr - ringPad, (cr + ringPad) * 2f, (cr + ringPad) * 2f), discTex);
                    GUI.color = new Color(fill.r, fill.g, fill.b, fillA);
                    GUI.DrawTexture(new Rect(c.x - cr, c.y - cr, cr * 2f, cr * 2f), discTex);
                    regionNameStyle.normal.textColor = accessible ? Color.white : new Color(0.7f, 0.7f, 0.72f);
                    GUI.color = Color.white;

                }

                // 서브에리어 — 소형 disc(형태 통일) + 클릭 텔레포트
                if (r.subAreas != null)
                {
                    foreach (var sub in r.subAreas)
                    {
                        if (sub == null) continue;
                        Vector2 sc = WorldToMap(sub.centerPosition.x, sub.centerPosition.z);
                        float sr = Mathf.Clamp(cr * 0.22f, 8f, 20f);
                        Color subCol = accessible ? GetSubAreaColor(sub.environmentType) : new Color(0.4f, 0.4f, 0.42f);
                        GUI.color = new Color(subCol.r, subCol.g, subCol.b, 0.85f);
                        GUI.DrawTexture(new Rect(sc.x - sr, sc.y - sr, sr * 2f, sr * 2f), discTex);
                        GUI.color = new Color(subCol.r + 0.25f, subCol.g + 0.25f, subCol.b + 0.25f, 0.9f);
                        GUI.DrawTexture(new Rect(sc.x - sr - 1.5f, sc.y - sr - 1.5f, (sr + 1.5f) * 2f, (sr + 1.5f) * 2f), discTex);
                        GUI.color = new Color(subCol.r, subCol.g, subCol.b, 0.85f);
                        GUI.DrawTexture(new Rect(sc.x - sr, sc.y - sr, sr * 2f, sr * 2f), discTex);
                        GUI.color = Color.white;
                        if (selectedRegionId == r.regionId || new Rect(sc.x - sr, sc.y - sr, sr * 2f, sr * 2f).Contains(Event.current.mousePosition))
                            UIHelper.LabelFit(new Rect(sc.x - 85f, sc.y + sr + 1f, 170f, 22f), sub.displayName, subNameStyle);

                        float hot = Mathf.Max(sr, 14f);
                        if (GUI.Button(new Rect(sc.x - hot, sc.y - hot, hot * 2f, hot * 2f), "", GUIStyle.none))
                        {
                            if (accessible) TeleportToSubArea(sub);
                            else { errorMessage = $"먼저 {r.displayName}을(를) 해금하세요 (이전 지역의 수문장 격파)"; errorTimer = 3f; }
                        }
                    }
                }

                // 가디언 마커 — 미격파 가디언이 있으면 진입로(GetGuardianPosition)에 표시
                if (!string.IsNullOrEmpty(r.guardianInsectId) && !regionManager.IsGuardianDefeated(r.regionId))
                {
                    Vector3 gpos = regionManager.GetGuardianPosition(r);
                    Vector2 gc = WorldToMap(gpos.x, gpos.z);
                    GUI.color = new Color(0.95f, 0.35f, 0.25f, 0.95f);
                    GUI.DrawTexture(new Rect(gc.x - 9f, gc.y - 9f, 18f, 18f), discTex);
                    GUI.color = new Color(1f, 0.8f, 0.6f, 1f);
                    GUI.DrawTexture(new Rect(gc.x - 4f, gc.y - 4f, 8f, 8f), discTex);
                    GUI.color = Color.white;
                    if (new Rect(gc.x - 12f, gc.y - 12f, 24f, 24f).Contains(Event.current.mousePosition))
                        UIHelper.LabelFit(new Rect(gc.x - 60f, gc.y + 10f, 120f, 22f), $"수문장 Lv{r.guardianLevel}", guardianStyle);
                }

                regionNameStyle.wordWrap = true;
                UIHelper.LabelFit(new Rect(c.x - cr + 3f, c.y - 27f, cr * 2f - 6f, 54f),
                    r.displayName, regionNameStyle, 12);
                regionNameStyle.wordWrap = false;

                // 리전 클릭 버튼 — 서브에리어 버튼 '이후' 호출해야 서브 점 클릭이 리전에 먹히지 않음(IMGUI 이벤트 소비 순서).
                if (GUI.Button(new Rect(c.x - cr, c.y - cr, cr * 2f, cr * 2f), "", GUIStyle.none))
                {
                    selectedRegionId = r.regionId;
                    dexScroll = Vector2.zero;
                    dexDirectScroll.Reset();
                }
            }

            // 3) 레이드 보스 마커
            foreach (var m in raidMarkers)
            {
                // 스폰 좌표가 아니라 **지금 위치**를 쓴다 — 보스도 배회하므로 굳혀 두면
                // 지도가 가리키는 곳에 가도 아무것도 없다(Update가 이미 entity를 들고 있다).
                Vector3 mp = m.entity != null ? m.entity.transform.position : m.worldPos;
                Vector2 mc = WorldToMap(mp.x, mp.z);
                if (!area.Contains(mc)) continue;
                Color raidCol = m.rarity == InsectRarity.Legendary ? new Color(1f, 0.8f, 0.15f) : new Color(0.7f, 0.3f, 0.95f);
                float pulse = 0.7f + Mathf.Sin(Time.time * 4f) * 0.3f;
                GUI.color = new Color(raidCol.r, raidCol.g, raidCol.b, 0.3f * pulse);
                GUI.DrawTexture(new Rect(mc.x - 15f, mc.y - 15f, 30f, 30f), discTex);
                GUI.color = new Color(raidCol.r, raidCol.g, raidCol.b, pulse);
                GUI.DrawTexture(new Rect(mc.x - 7f, mc.y - 7f, 14f, 14f), discTex);
                raidLabelStyle.normal.textColor = raidCol;
                GUI.color = Color.white;
                GUI.Label(new Rect(mc.x - 90f, mc.y + 13f, 180f, 22f), m.name, raidLabelStyle);
            }

            // 3b) 스토리 목표 마커 — "지금 어디로 가야 하는가". 수문장(주황)·레이드(보라)와
            //     색으로 구분되게 민트다. 레이드 뒤, 플레이어 앞에 그려 플레이어를 가리지 않는다.
            DrawNpcMarkers(area);
            DrawStoryObjectiveMarker(area);

            // 4) 플레이어 마커 — disc + 진행방향
            Transform pl = GetPlayer();
            if (pl != null)
            {
                Vector3 mapPosition = MapMarkerProjection.OverworldPlayerPosition(pl.position,
                    regionManager != null ? regionManager.CurrentSubArea : null);
                Vector2 pc = WorldToMap(mapPosition.x, mapPosition.z);
                GUI.color = new Color(0.4f, 0.85f, 1f, 1f);
                GUI.DrawTexture(new Rect(pc.x - 8f, pc.y - 8f, 16f, 16f), discTex);
                if (regionManager.CurrentSubArea != null)
                    UIHelper.LabelFit(new Rect(pc.x - 90f, pc.y + 14f, 180f, 24f),
                        "서브지역 탐험 중", subNameStyle);
                Vector3 f = regionManager.CurrentSubArea != null ? Vector3.zero : pl.forward;
                Vector2 fd = new Vector2(f.x, -f.z);
                if (fd.sqrMagnitude > 0.001f)
                {
                    fd = fd.normalized * 18f;
                    GUI.color = new Color(0.75f, 0.95f, 1f, 1f);
                    GUI.DrawTexture(new Rect(pc.x + fd.x - 5f, pc.y + fd.y - 5f, 10f, 10f), discTex);
                }
            }
            GUI.color = Color.white;
        }

        /// <summary>
        /// 지금 이야기를 잇는 목표가 지도 어디에 있는가. <see cref="StoryObjectiveTracker"/>가
        /// 이미 좌표와 문구를 정해 두었으므로 여기서 다시 고르지 않는다.
        ///
        /// 미니맵 쐐기는 <b>방향</b>만 알려 준다(반경 밖이면 테두리에 붙는다). 목표가 다른
        /// 리전이면 방향만으로는 어디인지 알 수 없어서, 전체 지도에 실제 위치를 찍는다.
        /// </summary>
        private void DrawNpcMarkers(Rect area)
        {
            if (npcManager == null) return;
            foreach (var npc in npcManager.StoryNpcs)
            {
                if (npc == null || !npc.gameObject.activeInHierarchy) continue;
                Vector3 position = npc.transform.position;
                Vector2 marker = WorldToMap(position.x, position.z);
                if (!area.Contains(marker)) continue;

                InsectGame.NPC.QuestMark mark = objectiveTracker != null
                    ? objectiveTracker.TaleMarkOf(npc) : InsectGame.NPC.QuestMark.None;
                if (mark != InsectGame.NPC.QuestMark.None)
                {
                    DrawTaleBadge(marker, npc, mark, area);
                    continue;
                }

                GUI.color = UITheme.Instance.accentAmber;
                GUI.DrawTexture(new Rect(marker.x - 5f, marker.y - 5f, 10f, 10f), discTex);
                GUI.color = Color.white;
                if (new Rect(marker.x - 10f, marker.y - 10f, 20f, 20f).Contains(Event.current.mousePosition))
                    UIHelper.LabelFit(new Rect(Mathf.Clamp(marker.x - 70f, area.x, area.xMax - 140f),
                        Mathf.Clamp(marker.y + 8f, area.y, area.yMax - 24f), 140f, 24f), npc.DisplayName, subNameStyle);
            }
        }

        // ── 의뢰 주민 배지 ──
        // 반경은 손가락이 닿게 잡는다 — 지도 점(10px)과 같은 크기면 모바일에서 누를 수가 없다.
        private const float TaleBadgeRadius = 13f;
        private const float TaleBadgeHotPad = 8f;

        /// <summary>
        /// 지도에서 의뢰 주민 배지를 누르면 그 이야기를 따라간다. 지도는 닫지 않는다 — 목표 마커(민트)가
        /// 곧바로 그 자리로 옮겨 가는 것을 보여 주고, 다른 리전이면 이어서 [이동]을 누르게 한다.
        /// </summary>
        private void HandleTaleMarkerInput(Rect area)
        {
            Event e = Event.current;
            if (e == null || e.type != EventType.MouseDown || e.button != 0 || objectiveTracker == null) return;

            var marks = objectiveTracker.TaleMarkers;
            float hot = TaleBadgeRadius + TaleBadgeHotPad;
            for (int i = 0; i < marks.Count; i++)
            {
                InsectGame.NPC.VillagerNpc npc = marks[i].Npc;
                if (npc == null || !npc.gameObject.activeInHierarchy) continue;
                Vector2 m = WorldToMap(npc.transform.position.x, npc.transform.position.z);
                if (!area.Contains(m)) continue;
                if (!new Rect(m.x - hot, m.y - hot, hot * 2f, hot * 2f).Contains(e.mousePosition)) continue;

                selectedRegionId = npc.RegionId;   // 정보 패널도 그 리전으로 — [이동]이 바로 보인다
                objectiveTracker.TrackTale(npc.StoryNpcId);
                e.Use();
                return;
            }
        }

        private void DrawTaleBadge(Vector2 m, InsectGame.NPC.VillagerNpc npc, InsectGame.NPC.QuestMark mark, Rect area)
        {
            UITheme t = UITheme.Instance;
            bool report = mark == InsectGame.NPC.QuestMark.Report;
            // !·? 모두 호박색 — 민트는 이야기 목표 마커와 서브에리어 자리라, ?를 민트로 두면 셋이 한 색이 됐다.
            // 새 이야기와 보고는 기호로 가른다(머리 위 표식·미니맵과 같은 규칙).
            Color col = t.accentAmber;
            bool tracked = objectiveTracker != null && objectiveTracker.IsTaleTracked(npc.StoryNpcId);

            // 따라가는 주민은 흰 테두리로 한 번 더 두른다 — 여러 배지 중 어느 것을 골랐는지 보이게.
            float r = TaleBadgeRadius;
            GUI.color = tracked ? Color.white : new Color(t.surfaceShadow.r, t.surfaceShadow.g, t.surfaceShadow.b, 0.55f);
            GUI.DrawTexture(new Rect(m.x - r - 3f, m.y - r - 3f, (r + 3f) * 2f, (r + 3f) * 2f), discTex);
            GUI.color = col;
            GUI.DrawTexture(new Rect(m.x - r, m.y - r, r * 2f, r * 2f), discTex);
            GUI.color = Color.white;
            GUI.Label(new Rect(m.x - r, m.y - r, r * 2f, r * 2f), report ? "?" : "!", taleBadgeStyle);

            // 이름은 누르기 전에도 보인다 — 배지만으로는 누구의 의뢰인지 모른다. 다른 점과 달리 개수가 적다(주민 7명).
            float lw = 150f;
            UIHelper.LabelFit(new Rect(Mathf.Clamp(m.x - lw * 0.5f, area.x, area.xMax - lw),
                Mathf.Clamp(m.y + r + 2f, area.y, area.yMax - 24f), lw, 24f), npc.DisplayName, subNameStyle);
        }

        private void DrawStoryObjectiveMarker(Rect area)
        {
            if (objectiveTracker == null || !objectiveTracker.HasObjective
                || !objectiveTracker.HasWorldTarget) return;

            Vector3 wp = objectiveTracker.TargetPosition;
            Vector2 mc = WorldToMap(wp.x, wp.z);
            // **지도 경계는 리전 원들로만 잡는다** — 서브에리어도 NPC도 그 밖에 있을 수 있고,
            // 지도는 Group으로 클립되지 않아 나가면 정보 패널 위에 민트 점이 떠 버린다.
            // 나침반처럼 테두리에 붙인다(방향은 남고, 라벨이 바로 옆에 붙어 뭔지는 읽힌다).
            mc.x = Mathf.Clamp(mc.x, area.x + 10f, area.xMax - 10f);
            mc.y = Mathf.Clamp(mc.y, area.y + 10f, area.yMax - 10f);
            Color mint = UITheme.Instance.accentMint;

            // 맥동은 레이드 마커와 같은 관용구 — 두 마커가 서로 다른 박자로 뛰면 산만해진다.
            float pulse = 0.7f + Mathf.Sin(Time.time * 4f) * 0.3f;
            GUI.color = new Color(mint.r, mint.g, mint.b, 0.28f * pulse);
            GUI.DrawTexture(new Rect(mc.x - 17f, mc.y - 17f, 34f, 34f), discTex);
            GUI.color = new Color(mint.r, mint.g, mint.b, pulse);
            GUI.DrawTexture(new Rect(mc.x - 8f, mc.y - 8f, 16f, 16f), discTex);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(mc.x - 3f, mc.y - 3f, 6f, 6f), discTex);

            // 라벨을 지도 영역 안으로 가둔다. 목표가 지도 가장자리에 있으면 280px 라벨이
            // 패널 제목이나 정보 패널 위로 삐져나간다(지도는 Group으로 클립되지 않는다).
            // 위쪽에 자리가 없으면 마커 아래로 내린다.
            float lw = Mathf.Min(280f, area.width);
            float lx = Mathf.Clamp(mc.x - lw * 0.5f, area.x, area.xMax - lw);
            float ly = mc.y - 36f >= area.y ? mc.y - 36f : mc.y + 14f;
            ly = Mathf.Clamp(ly, area.y, area.yMax - 24f);

            // Label()이 wordWrap을 전역으로 꺼 두어 긴 목표명은 가로로 잘린다 — LabelFit이
            // 폭까지 보고 폰트를 줄인다(rules/ui-layout.md).
            if (new Rect(mc.x - 17f, mc.y - 17f, 34f, 34f).Contains(Event.current.mousePosition))
                UIHelper.LabelFit(new Rect(lx, ly, lw, 24f), objectiveTracker.Label, objectiveStyle);
            GUI.color = Color.white;
        }

        // 정보 패널 — 선택 리전 상세 or 범례
        private void DrawRegionInfoPanel(Rect area)
        {
            UITheme t = UITheme.Instance;
            GUI.color = new Color(t.panelHeaderBg.r, t.panelHeaderBg.g, t.panelHeaderBg.b, 0.65f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);

            RegionData region = selectedRegionId != null ? regionManager.GetRegionById(selectedRegionId) : null;
            float ix = area.x + 20f, iw = area.width - 40f;

            if (region == null)
            {
                // 범례 — 색상 원 스와치 + 한글(폰트 무관)
                GUI.color = Color.white;
                GUI.Label(new Rect(ix, area.y + 16f, iw, 34f), "범례", infoTitleStyle);
                float ly = area.y + 62f;

                // **행 간격을 남은 높이에서 낸다.** 세로 화면에서 이 패널은 콘텐츠 높이의 40%,
                // 대략 334px밖에 안 된다 — 고정 34px로 일곱 줄을 쌓으면 마지막 두 줄이 아래
                // 안내 문구를 뚫고 나간다. 범례는 한 항목만 빠져도 지도를 못 읽으므로
                // 줄을 버리는 대신 간격을 줄인다(rules/ui-layout.md의 "고정 개수 행" 처리).
                const int LegendRows = 8;
                float legendPitch = Mathf.Clamp(
                    (area.y + area.height - 66f - ly) / LegendRows, 22f, 34f);

                DrawLegendRow(ix, ref ly, iw, new Color(1f, 1f, 1f, 0.95f), "현재 위치 (밝은 테두리)", legendPitch);
                DrawLegendRow(ix, ref ly, iw, new Color(0.45f, 0.45f, 0.47f), "잠긴 지역 (회색)", legendPitch);
                DrawLegendRow(ix, ref ly, iw, new Color(0.95f, 0.35f, 0.25f), "수문장 (진입로 표시)", legendPitch);
                DrawLegendRow(ix, ref ly, iw, new Color(0.7f, 0.3f, 0.95f), "레이드 보스", legendPitch);
                DrawLegendRow(ix, ref ly, iw, new Color(0.4f, 0.85f, 1f), "내 위치", legendPitch);
                DrawLegendRow(ix, ref ly, iw, new Color(0.5f, 0.7f, 0.4f), "서브에리어 (소형 원, 눌러 이동)", legendPitch);
                DrawLegendRow(ix, ref ly, iw, UITheme.Instance.accentMint, "이야기 목표 (민트, 맥동)", legendPitch);
                DrawLegendRow(ix, ref ly, iw, UITheme.Instance.accentAmber, "마을 의뢰 (!·? 눌러 따라가기)", legendPitch);

                // 목표 문구 자체는 여기 두지 않는다 — 마커 바로 위에 이미 붙어 있고(지도),
                // 패널 밖 HUD 목표 행에도 있다. 세로에서는 넣을 자리도 없다.
                GUI.Label(new Rect(ix, area.y + area.height - 60f, iw, 40f), "지역을 눌러 정보를 확인하세요", hintStyle);
                return;
            }

            bool accessible = regionManager.IsRegionAccessible(region);
            bool current = regionManager.CurrentRegion == region;

            // 헤더 악센트
            GUI.color = new Color(region.themeColor.r, region.themeColor.g, region.themeColor.b, accessible ? 1f : 0.5f);
            GUI.DrawTexture(new Rect(area.x, area.y, area.width, 4f), Texture2D.whiteTexture);

            infoTitleStyle.normal.textColor = accessible ? region.themeColor : new Color(0.6f, 0.6f, 0.62f);
            GUI.color = Color.white;
            UIHelper.LabelFit(new Rect(ix, area.y + 16f, iw, 40f), region.displayName + (current ? " (현재)" : ""), infoTitleStyle);

            // **버튼 자리를 먼저 확보하고 남는 높이에 정보를 담는다.** 범례 쪽(위)과 같은
            // 이유다 — 세로 화면에서 이 패널은 콘텐츠 높이의 40%, 대략 334px밖에 안 된다.
            // 고정 36px로 정보 네 줄(수문장 있는 리전이 대부분이다)에 도감 진행바까지 쌓으면
            // 바가 [이동]/[도감] 버튼 위로 10px 파고든다. 지난 라운드에 범례 분기만 고치고
            // **이 분기를 빠뜨렸다** — 같은 패널, 같은 높이, 같은 결함이다.
            float btnH = UIScale.IsMobileLayout ? 64f : 52f;
            float btnY = area.y + area.height - btnH - 14f;

            float y = area.y + 64f;
            int infoRows = string.IsNullOrEmpty(region.guardianInsectId) ? 3 : 4;
            const float DexBlockH = 58f;   // 위 여백 8 + 라벨 34 + 바 16
            float linePitch = Mathf.Clamp((btnY - 12f - DexBlockH - y) / infoRows, 26f, 36f);

            DiffLabel(region.requiredLevel, out string diff, out Color diffCol);
            DrawInfoLine(ix, ref y, iw, "난이도", diff, diffCol, linePitch);
            // "요구"가 아니라 "권장"이다 — 진입 요건은 레벨이 아니라 앞 리전의 수문장 격파고
            // (RegionManager.IsRegionAccessible), requiredLevel은 이 리전의 스폰 레벨과 바로 위
            // 난이도 라벨을 정하는 값이다. "요구 레벨 Lv 6"은 필드 차단 배너가 하던 것과 똑같은
            // 거짓말이라, 레벨만 채우면 들어갈 수 있다고 읽힌다.
            DrawInfoLine(ix, ref y, iw, "권장 레벨", $"Lv {region.requiredLevel}", new Color(0.8f, 0.85f, 0.95f), linePitch);
            DrawInfoLine(ix, ref y, iw, "상태", accessible ? "해금됨" : "잠김",
                accessible ? new Color(0.4f, 0.9f, 0.5f) : new Color(0.9f, 0.45f, 0.4f), linePitch);

            if (!string.IsNullOrEmpty(region.guardianInsectId))
            {
                bool defeated = regionManager.IsGuardianDefeated(region.regionId);
                string gname = string.IsNullOrEmpty(region.guardianDisplayName) ? "수문장" : region.guardianDisplayName;
                DrawInfoLine(ix, ref y, iw, "수문장", $"{gname} Lv{region.guardianLevel}" + (defeated ? " (격파)" : ""),
                    defeated ? new Color(0.5f, 0.7f, 0.5f) : new Color(1f, 0.6f, 0.4f), linePitch);
            }

            // 도감 진행바
            int total = region.insectIds != null ? region.insectIds.Length : 0;
            int caught = CountCaught(region);
            y += 8f;
            infoLineStyle.normal.textColor = new Color(0.82f, 0.85f, 0.9f);
            UIHelper.LabelFit(new Rect(ix, y, iw, 30f), $"도감  {caught} / {total}", infoLineStyle);
            y += 34f;
            GUI.color = new Color(0.15f, 0.15f, 0.2f, 1f);
            GUI.DrawTexture(new Rect(ix, y, iw, 16f), Texture2D.whiteTexture);
            GUI.color = region.themeColor;
            GUI.DrawTexture(new Rect(ix, y, iw * (total > 0 ? (float)caught / total : 0f), 16f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float halfW = (iw - 12f) / 2f;
            GUI.backgroundColor = accessible ? UITheme.Instance.btnPrimary : UITheme.Instance.btnDisabled;
            if (GUI.Button(new Rect(ix, btnY, halfW, btnH), "이동", btnStyle))
            {
                if (accessible) { TeleportToRegion(region); CloseModal(); }
                else { errorMessage = "잠긴 지역입니다 (이전 지역의 수문장 격파)"; errorTimer = 3f; }
            }
            GUI.backgroundColor = UITheme.Instance.btnSecondary;
            if (GUI.Button(new Rect(ix + halfW + 12f, btnY, halfW, btnH), "도감", btnStyle))
            {
                dexOpen = true;
                dexScroll = Vector2.zero;
                dexDirectScroll.Reset();
            }
            GUI.backgroundColor = Color.white;
        }

        // pitch는 호출부가 남은 높이에서 계산해 넘긴다. 스와치와 라벨 상자를 그 안에 가둬야
        // 간격이 좁아졌을 때 위아래 행이 서로 겹치지 않는다.
        private void DrawLegendRow(float x, ref float y, float w, Color swatch, string label, float pitch)
        {
            float dot = Mathf.Min(22f, pitch - 6f);
            float rowH = Mathf.Min(28f, pitch - 2f);
            GUI.color = swatch;
            GUI.DrawTexture(new Rect(x, y + (pitch - dot) * 0.5f, dot, dot), discTex);
            GUI.color = Color.white;
            UIHelper.LabelFit(new Rect(x + 32f, y, w - 32f, rowH), label, legendStyle);
            y += pitch;
        }

        // pitch는 호출부가 버튼 위 남은 높이에서 계산해 넘긴다(DrawLegendRow와 같은 방식).
        // 행 높이도 그 안에 가둬야 간격이 좁아졌을 때 위아래 줄이 서로 먹지 않는다.
        private void DrawInfoLine(float x, ref float y, float w, string key, string val, Color valCol,
            float pitch = 36f)
        {
            float keyW = 116f;
            float rowH = Mathf.Min(30f, pitch - 2f);
            infoLineStyle.normal.textColor = new Color(0.62f, 0.64f, 0.72f);
            GUI.Label(new Rect(x, y, keyW, rowH), key, infoLineStyle);
            infoLineStyle.normal.textColor = valCol;
            // 값은 길이가 데이터에서 온다(수문장 이름 등) — 고정 상자라 LabelFit이 필요하다.
            UIHelper.LabelFit(new Rect(x + keyW, y, w - keyW, rowH), val, infoLineStyle);
            y += pitch;
        }

        private static void DiffLabel(int requiredLevel, out string label, out Color col)
        {
            if (requiredLevel <= 2) { label = "쉬움"; col = new Color(0.4f, 0.9f, 0.5f); }
            else if (requiredLevel <= 5) { label = "보통"; col = new Color(0.9f, 0.8f, 0.3f); }
            else { label = "어려움"; col = new Color(1f, 0.45f, 0.35f); }
        }

        private static Color Desaturate(Color c, float amount)
        {
            float g = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            return new Color(Mathf.Lerp(c.r, g, amount), Mathf.Lerp(c.g, g, amount), Mathf.Lerp(c.b, g, amount), c.a);
        }

        private void DrawThickLine(Vector2 a, Vector2 b, float thickness, Color col)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.5f) return;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Matrix4x4 saved = GUI.matrix;
            GUI.matrix = MapMarkerProjection.PivotMatrix(saved, a, ang);
            GUI.color = col;
            GUI.DrawTexture(new Rect(0f, -thickness * 0.5f, len, thickness), Texture2D.whiteTexture);
            GUI.matrix = saved;
            GUI.color = Color.white;
        }

        private void DrawErrorToast()
        {
            if (errorTimer <= 0f || string.IsNullOrEmpty(errorMessage)) return;
            float sw = UIScale.VirtualScreenWidth, sh = UIScale.VirtualScreenHeight;
            float bw = 900f, bh = 96f;
            float bx = (sw - bw) * 0.5f, by = sh * 0.72f;
            float alpha = Mathf.Clamp01(errorTimer);

            GUI.color = new Color(0f, 0f, 0f, 0.85f * alpha);
            GUI.DrawTexture(new Rect(bx, by, bw, bh), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.3f, 0.3f, alpha);
            GUI.DrawTexture(new Rect(bx, by, bw, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bx, by + bh - 3f, bw, 3f), Texture2D.whiteTexture);

            mapErrStyle.normal.textColor = new Color(1f, 0.6f, 0.6f, alpha);
            GUI.color = Color.white;
            // 문구 길이를 데이터가 정하고(리전 표시명이 들어간다) mapErrStyle은 wordWrap=false라
            // 폭을 넘으면 가로로 잘린다 — 가운데 정렬이면 앞뒤가 함께 잘려 더 나쁘다.
            UIHelper.LabelFit(new Rect(bx, by, bw, bh), errorMessage, mapErrStyle);
        }

        // ─── 도감 브라우저(정보패널 [도감]으로 진입) ───
        private void DrawDexBrowser()
        {
            RegionData region = regionManager != null ? regionManager.GetRegionById(selectedRegionId) : null;
            if (region == null)
            {
                dexOpen = false;
                dexDirectScroll.Reset();
                return;
            }

            PanelRect(out float px, out float py, out float pw, out float ph);
            // 왼쪽 150px + 여백은 뒤로 버튼 자리다.
            DrawPanelFrame(px, py, pw, ph, $"{region.displayName} 도감", region.themeColor, 162f);

            if (GUI.Button(new Rect(px + pw - 76f, py + 12f, 60f, 60f), "X", closeStyle)) { CloseModal(); return; }
            if (GUI.Button(new Rect(px + 20f, py + 14f, 150f, UIScale.IsMobileLayout ? 60f : 52f), "< 뒤로", detailBtnStyle))
            {
                dexOpen = false;
                dexDirectScroll.Reset();
                return;
            }

            if (!string.IsNullOrEmpty(region.description))
                UIHelper.LabelFit(new Rect(px + 30f, py + 96f, pw - 60f, 42f), region.description, detailDescStyle);

            int total = region.insectIds != null ? region.insectIds.Length : 0;
            int caught = CountCaught(region);
            GUI.Label(new Rect(px, py + 146f, pw, 40f), $"포획  {caught} / {total}", detailSummaryStyle);

            float barX = px + pw * 0.2f, barW = pw * 0.6f, barY = py + 196f;
            GUI.color = new Color(0.15f, 0.15f, 0.2f);
            GUI.DrawTexture(new Rect(barX, barY, barW, 18f), Texture2D.whiteTexture);
            GUI.color = region.themeColor;
            GUI.DrawTexture(new Rect(barX, barY, barW * (total > 0 ? (float)caught / total : 0f), 18f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            Rect listArea = new Rect(px + 15f, py + 232f, pw - 30f, ph - 244f);
            if (total == 0)
            {
                GUI.Label(listArea, "등록된 곤충 없음", detailNoInsectStyle);
                return;
            }

            float itemH = 140f;
            float contentH = region.insectIds.Length * itemH;
            Rect viewRect = new Rect(0, 0, listArea.width, contentH);
            dexDirectScroll.Handle(ref dexScroll, listArea, contentH, itemH * 0.4f);
            dexScroll = GUI.BeginScrollView(
                listArea,
                dexScroll,
                viewRect,
                GUIStyle.none,
                GUIStyle.none);
            // 화면에 걸치는 줄만 그린다 — DrawDexItem이 포획한 종마다 3D 썸네일을 요청하므로,
            // 컬링하지 않으면 한 뷰포트 분량짜리 캐시가 영구 스래싱한다(2026-08-06 audit).
            DexBrowseLayout.GetVisibleRowRange(
                dexScroll.y, listArea.height, itemH - 4f, 4f, region.insectIds.Length,
                out int firstVisible, out int lastVisible);

            for (int i = firstVisible; i <= lastVisible; i++)
                DrawDexItem(new Rect(0, i * itemH, viewRect.width, itemH - 4f), region.insectIds[i]);
            GUI.EndScrollView();
        }

        private void DrawDexItem(Rect rect, string insectId)
        {
            InsectData data = FindInsectData(insectId);
            bool isCaught = dex != null && dex.HasRecord(insectId);

            GUI.color = new Color(0.1f, 0.12f, 0.18f, 0.85f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            if (isCaught && data != null)
            {
                Color rarityCol = UITheme.Instance.GetInsectRarityColor(data.rarity);
                GUI.color = rarityCol;
                GUI.DrawTexture(new Rect(rect.x, rect.y, 6f, rect.height), Texture2D.whiteTexture);
                InsectVisual.Draw(rect.x + 62f, rect.y + rect.height / 2f, 96f, data, false, 1f);

                dexNameStyle.normal.textColor = rarityCol;
                GUI.color = Color.white;
                UIHelper.LabelFit(new Rect(rect.x + 120f, rect.y + 18f, rect.width - 200f, 46f), data.displayName, dexNameStyle);
                GUI.Label(new Rect(rect.x + 120f, rect.y + 68f, rect.width - 200f, 42f),
                    DexInfoLine(data), dexInfoStyle);
                GUI.Label(new Rect(rect.x + rect.width - 72f, rect.y + 22f, 60f, 60f), "V", dexCheckStyle);
            }
            else
            {
                GUI.color = new Color(0.3f, 0.3f, 0.3f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 6f, rect.height), Texture2D.whiteTexture);
                GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                GUI.DrawTexture(new Rect(rect.x + 24f, rect.y + rect.height / 2f - 34f, 68f, 68f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(rect.x + 24f, rect.y + rect.height / 2f - 34f, 68f, 68f), "?", dexUnknownStyle);
                string hint = HiddenName(data);
                GUI.Label(new Rect(rect.x + 120f, rect.y + 26f, rect.width - 200f, 46f), hint, dexHiddenStyle);
                GUI.Label(new Rect(rect.x + 120f, rect.y + 74f, rect.width - 200f, 42f), "아직 포획하지 않음", dexNotCaughtStyle);
            }
        }

        /// <summary>"등급  |  CP n" — 종 데이터에서만 파생되므로 insectId로 캐시한다.</summary>
        private string DexInfoLine(InsectData data)
        {
            if (data == null) return string.Empty;
            string key = data.insectId ?? data.displayName ?? string.Empty;
            if (dexInfoCache.TryGetValue(key, out string cached)) return cached;

            string line = $"{data.rarity}  |  CP {PlayerInsectCombatPower.CalculateBasePreview(data, data.minLevel)}";
            dexInfoCache[key] = line;
            return line;
        }

        /// <summary>미포획 곤충의 "???" — 이름 길이에만 의존하므로 길이로 캐시한다.</summary>
        private string HiddenName(InsectData data)
        {
            int len = data != null && data.displayName != null ? data.displayName.Length : 3;
            if (len <= 0) len = 3;
            if (hiddenNameCache.TryGetValue(len, out string cached)) return cached;

            string hint = new string('?', len);
            hiddenNameCache[len] = hint;
            return hint;
        }

        private int CountCaught(RegionData region)
        {
            if (region == null || region.insectIds == null || dex == null) return 0;
            int count = 0;
            foreach (string id in region.insectIds)
                if (dex.HasRecord(id)) count++;
            return count;
        }

        private InsectData FindInsectData(string insectId)
        {
            if (database == null || database.insects == null) return null;
            foreach (var d in database.insects)
                if (d != null && d.insectId == insectId) return d;
            return null;
        }

        private RegionData FindRegion(RegionData[] regions, string id)
        {
            foreach (var r in regions)
                if (r != null && r.regionId == id) return r;
            return null;
        }

        private void TeleportToSubArea(Data.SubAreaData sub)
        {
            if (sub == null) return;
            Transform pl = GetPlayer();
            if (pl == null) return;
            // Exit restores the main world synchronously before the new destination is assigned.
            if (regionManager != null) regionManager.ForceExitSubArea();
            Vector3 dest = sub.centerPosition;
            dest.y = pl.position.y;
            pl.position = dest;
            pl.GetComponent<PlayerMovement>()?.StopNavigation();
            // 맵은 닫지 않음 — 다음 Update에서 SubArea 진입 팝업 자동 발화.
        }

        private void TeleportToRegion(RegionData region)
        {
            if (region == null) return;
            Transform pl = GetPlayer();
            if (pl == null) return;
            if (regionManager != null) regionManager.ForceExitSubArea();
            Vector3 dest = region.centerPosition;
            dest.y = pl.position.y;
            pl.position = dest;
            pl.GetComponent<PlayerMovement>()?.StopNavigation();
        }

        private Color GetSubAreaColor(string envType)
        {
            switch (envType)
            {
                case "cave": return new Color(0.5f, 0.4f, 0.3f);
                case "deep_forest": return new Color(0.1f, 0.4f, 0.1f);
                case "underwater": return new Color(0.2f, 0.4f, 0.8f);
                case "pond": return new Color(0.3f, 0.5f, 0.7f);
                case "fog": return new Color(0.5f, 0.5f, 0.45f);
                case "reeds": return new Color(0.4f, 0.5f, 0.2f);
                case "peak": return new Color(0.7f, 0.7f, 0.8f);
                case "flower_maze": return new Color(0.9f, 0.5f, 0.6f);
                case "greenhouse": return new Color(0.5f, 0.8f, 0.4f);
                case "temple": return new Color(0.5f, 0.3f, 0.7f);
                case "underground": return new Color(0.3f, 0.25f, 0.2f);
                default: return new Color(0.5f, 0.5f, 0.5f);
            }
        }

        public void AutoWire(RegionManager rm, PlayerProgressController prog, DexController dexCtrl, InsectDatabase db)
        {
            if (regionManager == null) regionManager = rm;
            if (progress == null) progress = prog;
            if (dex == null) dex = dexCtrl;
            if (database == null) database = db;
        }

        /// <summary>
        /// 스토리 목표 마커의 출처. 없으면 마커만 안 그려질 뿐 지도는 그대로 동작한다.
        /// </summary>
        public void AutoWire(InsectGame.Story.StoryObjectiveTracker tracker)
        {
            if (objectiveTracker == null) objectiveTracker = tracker;
        }

        /// <summary>
        /// 지도를 <paramref name="regionId"/>가 선택된 채로 연다. 목표 행이 "다른 리전에 있다"고
        /// 판단했을 때 부른다 — 예전엔 안내 문구만 띄워서, 읽고 나서 지도를 직접 열고 그 지역을
        /// 눈으로 찾아야 했다. <b>이동은 여기서 하지 않는다</b>: 해금·수문장 판정이 아래
        /// "이동" 버튼 경로에 있고, 그걸 우회하면 잠긴 지역으로 들어간다.
        /// </summary>
        public void OpenAt(string regionId)
        {
            if (!isOpen) Toggle();
            if (!string.IsNullOrEmpty(regionId)) selectedRegionId = regionId;
            dexOpen = false;
            dexScroll = Vector2.zero;
            dexDirectScroll.Reset();
        }

        public void AutoWire(InsectSpawner sp)
        {
            if (spawner != null) spawner.RaidBossSpawned -= OnRaidBossSpawned;
            spawner = sp;
            if (spawner != null) spawner.RaidBossSpawned += OnRaidBossSpawned;
        }
    }
    internal static class MapMarkerProjection
    {
        internal static Matrix4x4 PivotMatrix(Matrix4x4 parent, Vector2 pivot, float angle)
        {
            return parent * Matrix4x4.TRS(new Vector3(pivot.x, pivot.y, 0f),
                Quaternion.Euler(0f, 0f, angle), Vector3.one);
        }

        internal static Vector3 OverworldPlayerPosition(Vector3 playerPosition, SubAreaData currentSubArea)
        {
            return currentSubArea != null ? currentSubArea.centerPosition : playerPosition;
        }

        internal static bool TryRadarOffset(Vector3 origin, Vector3 target, float worldRadius,
            float mapRadius, out Vector2 offset)
        {
            Vector3 delta = target - origin;
            offset = Vector2.zero;
            if (worldRadius <= 0f || mapRadius <= 0f) return false;
            Vector2 horizontal = new Vector2(delta.x, -delta.z);
            if (horizontal.sqrMagnitude > worldRadius * worldRadius) return false;
            offset = horizontal / worldRadius * mapRadius;
            return true;
        }
    }

}
