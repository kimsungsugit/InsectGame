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

        private bool isOpen;
        private string selectedRegionId;   // 지도에서 선택돼 정보패널에 표시되는 리전(도감 아님)
        private bool dexOpen;              // 도감 브라우저 열림(정보패널 [도감]이 켬)
        private Vector2 dexScroll;
        private Transform playerTransform; // 최초 1회 탐색 후 캐시(GameObject.Find 매 프레임 회피)

        private readonly List<RaidBossMarker> raidMarkers = new List<RaidBossMarker>();
        private string errorMessage = "";
        private float errorTimer;

        // 리전 간 공간 인접(길). RegionData.connections는 전부 null이라 여기서 토폴로지 유지. 정적이라 프레임당 할당 없음.
        private static readonly string[,] Connections = {
            {"meadow","pond"}, {"meadow","forest"}, {"meadow","swamp"}, {"meadow","garden"},
            {"mountain","ruins"}, {"forest","swamp"}, {"forest","mountain"}
        };

        private struct RaidBossMarker
        {
            public string name;
            public Vector3 worldPos;
            public InsectRarity rarity;
            public InsectEntity entity;
        }

        public bool IsOpen => isOpen;
        public void Toggle()
        {
            isOpen = !isOpen;
            if (!isOpen) { selectedRegionId = null; dexOpen = false; }
            if (isOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }
        public void CloseModal()
        {
            isOpen = false;
            selectedRegionId = null;
            dexOpen = false;
            ModalUIRegistry.Unregister(this);
        }

        private void OnDisable()
        {
            if (spawner != null) spawner.RaidBossSpawned -= OnRaidBossSpawned;
            // GO SetActive 토글 시 isOpen 잔존 + Registry 미등록 stale 모달 방지.
            isOpen = false;
            selectedRegionId = null;
            dexOpen = false;
            ModalUIRegistry.Unregister(this);
        }

        // ─── 자산(GUIStyle + disc 텍스처) — mapStylesReady 가드로 1회 할당(프레임당 new 회피) ───
        private Texture2D discTex;
        private GUIStyle titleStyle, closeStyle, levelStyle, noDataStyle;
        private GUIStyle regionNameStyle, subNameStyle, raidLabelStyle, guardianStyle, mapErrStyle;
        private GUIStyle infoTitleStyle, infoLineStyle, legendStyle, btnStyle, hintStyle;
        private GUIStyle detailTitleStyle, detailBtnStyle, detailDescStyle, detailSummaryStyle, detailNoInsectStyle;
        private GUIStyle dexNameStyle, dexInfoStyle, dexCheckStyle, dexUnknownStyle, dexHiddenStyle, dexNotCaughtStyle;
        private bool ready;

        private void EnsureAssets()
        {
            if (ready) return;
            ready = true;

            discTex = MakeSoftDisc(64);

            titleStyle = Label(40, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            closeStyle = new GUIStyle(GUI.skin.button) { fontSize = 34, fontStyle = FontStyle.Bold };
            levelStyle = Label(26, FontStyle.Bold, TextAnchor.MiddleRight, new Color(0.7f, 0.85f, 1f));
            noDataStyle = Label(30, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.55f, 0.55f, 0.6f));

            regionNameStyle = Label(24, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            subNameStyle = Label(17, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.85f, 0.88f, 0.95f));
            raidLabelStyle = Label(17, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            guardianStyle = Label(16, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.75f, 0.55f));
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

        // 안티앨리어싱 원형 텍스처 — 외곽 ~1.5px 소프트 엣지(MinimapUI.MakeDisc의 하드엣지판 개선).
        private static Texture2D MakeSoftDisc(int size)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) / 2f;
            float edge = 1.5f / c;
            for (int yy = 0; yy < size; yy++)
                for (int xx = 0; xx < size; xx++)
                {
                    float d = Mathf.Sqrt((xx - c) * (xx - c) + (yy - c) * (yy - c)) / c;
                    float a = Mathf.Clamp01((1f - d) / edge);
                    t.SetPixel(xx, yy, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return t;
        }

        private void OnRaidBossSpawned(InsectEntity entity)
        {
            if (entity == null || entity.Data == null) return;
            raidMarkers.Add(new RaidBossMarker
            {
                name = entity.Data.displayName,
                worldPos = entity.transform.position,
                rarity = entity.Data.rarity,
                entity = entity
            });
        }

        private void Update()
        {
            for (int i = raidMarkers.Count - 1; i >= 0; i--)
            {
                if (raidMarkers[i].entity == null || !raidMarkers[i].entity.gameObject.activeInHierarchy)
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

        // ─── 패널 지오메트리(세이프에어리어 노치 오프셋 반영) ───
        private void PanelRect(out float px, out float py, out float pw, out float ph)
        {
            pw = Mathf.Min(1160f, UIScale.ContentWidth(18f));
            float safeTop = UIScale.VirtualSafeTop;
            float safeBot = UIScale.VirtualSafeBottom;
            float availH = UIScale.VirtualScreenHeight - safeTop - safeBot;
            ph = Mathf.Min(980f, availH - 24f);
            px = (UIScale.VirtualScreenWidth - pw) / 2f;
            py = safeTop + (availH - ph) * 0.5f;   // 세이프에어리어 내 중앙(노치 침범 방지)
        }

        private void DrawPanelFrame(float px, float py, float pw, float ph, string title, Color headerAccent)
        {
            UITheme t = UITheme.Instance;
            GUI.color = t.panelBg;
            GUI.DrawTexture(new Rect(px, py, pw, ph), Texture2D.whiteTexture);
            GUI.color = t.panelHeaderBg;
            GUI.DrawTexture(new Rect(px, py, pw, 84f), Texture2D.whiteTexture);
            GUI.color = headerAccent;
            GUI.DrawTexture(new Rect(px, py + 84f, pw, 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(px + 26f, py + 12f, pw - 200f, 60f), title, titleStyle);
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

            // 1) 연결선(길) — 회전 quad로 깔끔하게
            Color pathCol = new Color(0.5f, 0.44f, 0.3f, 0.5f);
            for (int i = 0; i < Connections.GetLength(0); i++)
            {
                RegionData a = FindRegion(regions, Connections[i, 0]);
                RegionData b = FindRegion(regions, Connections[i, 1]);
                if (a == null || b == null) continue;
                DrawThickLine(WorldToMap(a.centerPosition.x, a.centerPosition.z),
                              WorldToMap(b.centerPosition.x, b.centerPosition.z), 3f, pathCol);
            }

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
                GUI.Label(new Rect(c.x - Mathf.Max(95f, cr + 10f), c.y - 18f, Mathf.Max(190f, (cr + 10f) * 2f), 36f), r.displayName, regionNameStyle);

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
                    GUI.Label(new Rect(c.x - Mathf.Max(95f, cr + 10f), c.y - 18f, Mathf.Max(190f, (cr + 10f) * 2f), 36f), r.displayName, regionNameStyle);
                }

                // 서브에리어 — 소형 disc(형태 통일) + 클릭 텔레포트
                if (r.subAreas != null)
                {
                    foreach (var sub in r.subAreas)
                    {
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
                        GUI.Label(new Rect(sc.x - 85f, sc.y + sr + 1f, 170f, 22f), sub.displayName, subNameStyle);

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
                    GUI.Label(new Rect(gc.x - 60f, gc.y + 10f, 120f, 22f), $"수문장 Lv{r.guardianLevel}", guardianStyle);
                }

                // 리전 클릭 버튼 — 서브에리어 버튼 '이후' 호출해야 서브 점 클릭이 리전에 먹히지 않음(IMGUI 이벤트 소비 순서).
                if (GUI.Button(new Rect(c.x - cr, c.y - cr, cr * 2f, cr * 2f), "", GUIStyle.none))
                    selectedRegionId = r.regionId;
            }

            // 3) 레이드 보스 마커
            foreach (var m in raidMarkers)
            {
                Vector2 mc = WorldToMap(m.worldPos.x, m.worldPos.z);
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

            // 4) 플레이어 마커 — disc + 진행방향
            Transform pl = GetPlayer();
            if (pl != null)
            {
                Vector2 pc = WorldToMap(pl.position.x, pl.position.z);
                GUI.color = new Color(0.4f, 0.85f, 1f, 1f);
                GUI.DrawTexture(new Rect(pc.x - 8f, pc.y - 8f, 16f, 16f), discTex);
                Vector3 f = pl.forward;
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
                DrawLegendRow(ix, ref ly, iw, new Color(1f, 1f, 1f, 0.95f), "현재 위치 (밝은 테두리)");
                DrawLegendRow(ix, ref ly, iw, new Color(0.45f, 0.45f, 0.47f), "잠긴 지역 (회색)");
                DrawLegendRow(ix, ref ly, iw, new Color(0.95f, 0.35f, 0.25f), "수문장 (진입로 표시)");
                DrawLegendRow(ix, ref ly, iw, new Color(0.7f, 0.3f, 0.95f), "레이드 보스");
                DrawLegendRow(ix, ref ly, iw, new Color(0.4f, 0.85f, 1f), "내 위치");
                DrawLegendRow(ix, ref ly, iw, new Color(0.5f, 0.7f, 0.4f), "서브에리어 (소형 원, 눌러 이동)");
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
            GUI.Label(new Rect(ix, area.y + 16f, iw, 40f), region.displayName + (current ? " (현재)" : ""), infoTitleStyle);

            float y = area.y + 64f;
            DiffLabel(region.requiredLevel, out string diff, out Color diffCol);
            DrawInfoLine(ix, ref y, iw, "난이도", diff, diffCol);
            DrawInfoLine(ix, ref y, iw, "요구 레벨", $"Lv {region.requiredLevel}", new Color(0.8f, 0.85f, 0.95f));
            DrawInfoLine(ix, ref y, iw, "상태", accessible ? "해금됨" : "잠김",
                accessible ? new Color(0.4f, 0.9f, 0.5f) : new Color(0.9f, 0.45f, 0.4f));

            if (!string.IsNullOrEmpty(region.guardianInsectId))
            {
                bool defeated = regionManager.IsGuardianDefeated(region.regionId);
                string gname = string.IsNullOrEmpty(region.guardianDisplayName) ? "수문장" : region.guardianDisplayName;
                DrawInfoLine(ix, ref y, iw, "수문장", $"{gname} Lv{region.guardianLevel}" + (defeated ? " (격파)" : ""),
                    defeated ? new Color(0.5f, 0.7f, 0.5f) : new Color(1f, 0.6f, 0.4f));
            }

            // 도감 진행바
            int total = region.insectIds != null ? region.insectIds.Length : 0;
            int caught = CountCaught(region);
            y += 8f;
            infoLineStyle.normal.textColor = new Color(0.82f, 0.85f, 0.9f);
            GUI.Label(new Rect(ix, y, iw, 30f), $"도감  {caught} / {total}", infoLineStyle);
            y += 34f;
            GUI.color = new Color(0.15f, 0.15f, 0.2f, 1f);
            GUI.DrawTexture(new Rect(ix, y, iw, 16f), Texture2D.whiteTexture);
            GUI.color = region.themeColor;
            GUI.DrawTexture(new Rect(ix, y, iw * (total > 0 ? (float)caught / total : 0f), 16f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 버튼 [이동] [도감]
            float btnH = UIScale.IsMobileLayout ? 64f : 52f;
            float btnY = area.y + area.height - btnH - 14f;
            float halfW = (iw - 12f) / 2f;
            GUI.backgroundColor = accessible ? UITheme.Instance.btnPrimary : UITheme.Instance.btnDisabled;
            if (GUI.Button(new Rect(ix, btnY, halfW, btnH), "이동", btnStyle))
            {
                if (accessible) { TeleportToRegion(region); CloseModal(); }
                else { errorMessage = "잠긴 지역입니다 (이전 지역의 수문장 격파)"; errorTimer = 3f; }
            }
            GUI.backgroundColor = UITheme.Instance.btnSecondary;
            if (GUI.Button(new Rect(ix + halfW + 12f, btnY, halfW, btnH), "도감", btnStyle))
                dexOpen = true;
            GUI.backgroundColor = Color.white;
        }

        private void DrawLegendRow(float x, ref float y, float w, Color swatch, string label)
        {
            GUI.color = swatch;
            GUI.DrawTexture(new Rect(x, y + 3f, 22f, 22f), discTex);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 32f, y, w - 32f, 28f), label, legendStyle);
            y += 34f;
        }

        private void DrawInfoLine(float x, ref float y, float w, string key, string val, Color valCol)
        {
            float keyW = 116f;
            infoLineStyle.normal.textColor = new Color(0.62f, 0.64f, 0.72f);
            GUI.Label(new Rect(x, y, keyW, 30f), key, infoLineStyle);
            infoLineStyle.normal.textColor = valCol;
            GUI.Label(new Rect(x + keyW, y, w - keyW, 30f), val, infoLineStyle);
            y += 36f;
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
            GUIUtility.RotateAroundPivot(ang, a);
            GUI.color = col;
            GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, len, thickness), Texture2D.whiteTexture);
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
            GUI.Label(new Rect(bx, by, bw, bh), errorMessage, mapErrStyle);
        }

        // ─── 도감 브라우저(정보패널 [도감]으로 진입) ───
        private void DrawDexBrowser()
        {
            RegionData region = regionManager != null ? regionManager.GetRegionById(selectedRegionId) : null;
            if (region == null) { dexOpen = false; return; }

            PanelRect(out float px, out float py, out float pw, out float ph);
            DrawPanelFrame(px, py, pw, ph, $"{region.displayName} 도감", region.themeColor);

            if (GUI.Button(new Rect(px + pw - 76f, py + 12f, 60f, 60f), "X", closeStyle)) { CloseModal(); return; }
            if (GUI.Button(new Rect(px + 20f, py + 14f, 150f, UIScale.IsMobileLayout ? 60f : 52f), "< 뒤로", detailBtnStyle))
            { dexOpen = false; return; }

            if (!string.IsNullOrEmpty(region.description))
                GUI.Label(new Rect(px + 30f, py + 96f, pw - 60f, 42f), region.description, detailDescStyle);

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
            Rect viewRect = new Rect(0, 0, listArea.width - 20f, region.insectIds.Length * itemH);
            dexScroll = GUI.BeginScrollView(listArea, dexScroll, viewRect);
            for (int i = 0; i < region.insectIds.Length; i++)
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
                CapturePopupUI.DrawTypedInsectPortrait(rect.x + 62f, rect.y + rect.height / 2f, data.insectId, data.rarity, 1f);

                dexNameStyle.normal.textColor = rarityCol;
                GUI.color = Color.white;
                GUI.Label(new Rect(rect.x + 120f, rect.y + 18f, rect.width - 200f, 46f), data.displayName, dexNameStyle);
                GUI.Label(new Rect(rect.x + 120f, rect.y + 68f, rect.width - 200f, 42f),
                    $"{data.rarity}  |  CP {PlayerInsectCombatPower.CalculateBasePreview(data, data.minLevel)}", dexInfoStyle);
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
                string hint = data != null ? new string('?', data.displayName.Length) : "???";
                GUI.Label(new Rect(rect.x + 120f, rect.y + 26f, rect.width - 200f, 46f), hint, dexHiddenStyle);
                GUI.Label(new Rect(rect.x + 120f, rect.y + 74f, rect.width - 200f, 42f), "아직 포획하지 않음", dexNotCaughtStyle);
            }
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
            Vector3 dest = sub.centerPosition;
            dest.y = pl.position.y;
            pl.position = dest;
            // 맵은 닫지 않음 — 다음 Update에서 SubArea 진입 팝업 자동 발화.
        }

        private void TeleportToRegion(RegionData region)
        {
            if (region == null) return;
            Transform pl = GetPlayer();
            if (pl == null) return;
            Vector3 dest = region.centerPosition;
            dest.y = pl.position.y;
            pl.position = dest;
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

        public void AutoWire(InsectSpawner sp)
        {
            if (spawner != null) spawner.RaidBossSpawned -= OnRaidBossSpawned;
            spawner = sp;
            if (spawner != null) spawner.RaidBossSpawned += OnRaidBossSpawned;
        }
    }
}
