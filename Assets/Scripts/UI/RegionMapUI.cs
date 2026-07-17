using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Dex;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.UI
{
    public class RegionMapUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private RegionManager regionManager;
        [SerializeField] private PlayerProgressController progress;
        [SerializeField] private DexController dex;
        [SerializeField] private InsectDatabase database;
        [SerializeField] private InsectSpawner spawner;

        private bool isOpen;
        private string selectedRegionId;
        private Vector2 dexScroll;

        private readonly List<RaidBossMarker> raidMarkers = new List<RaidBossMarker>();
        private string errorMessage = "";
        private float errorTimer;

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
            if (!isOpen) selectedRegionId = null;
            if (isOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }
        public void CloseModal()
        {
            isOpen = false;
            selectedRegionId = null;
            ModalUIRegistry.Unregister(this);
        }

        private void OnDisable()
        {
            if (spawner != null) spawner.RaidBossSpawned -= OnRaidBossSpawned;
            // CharacterOutfitUI 동일 P1: GO SetActive 토글 시 isOpen 잔존 + Registry 미등록 → stale 모달
            isOpen = false;
            selectedRegionId = null;
            ModalUIRegistry.Unregister(this);
        }

        // OnGUI 매 OnGUI(모달 토글 시 매 프레임) 28 new GUIStyle 회귀 차단 — InitMapStyles에서 1회 할당.
        // 일부 textColor는 동적(분기/alpha)이라 base 캐시 + 호출 시 갱신 (BattleScreenUI 패턴).
        private GUIStyle mapErrStyleCache;
        private GUIStyle mapTitleStyleCache;
        private GUIStyle mapCloseStyleCache;
        private GUIStyle mapNoDataStyleCache;
        private GUIStyle mapLvStyleCache;
        private GUIStyle regionNameStyleCache;
        private GUIStyle regionDiffStyleCache;
        private GUIStyle regionSubNameStyleCache;
        private GUIStyle regionInvisibleStyleCache;
        private GUIStyle regionRaidLabelStyleCache;
        private GUIStyle nsStyleCache;
        private GUIStyle curStyleCache;
        private GUIStyle diffSStyleCache;
        private GUIStyle csStyleCache;
        private GUIStyle btnStyleCache;
        private GUIStyle detailTitleStyleCache;
        private GUIStyle detailBackStyleCache;
        private GUIStyle detailCloseStyleCache;
        private GUIStyle detailDescStyleCache;
        private GUIStyle detailSummaryStyleCache;
        private GUIStyle detailNoInsectStyleCache;
        private GUIStyle dexNameStyleCache;
        private GUIStyle dexInfoStyleCache;
        private GUIStyle dexCheckStyleCache;
        private GUIStyle dexUnknownStyleCache;
        private GUIStyle dexHiddenNameStyleCache;
        private GUIStyle dexNotCaughtStyleCache;
        private GUIStyle symStyleCache;
        private bool mapStylesReady;

        private static readonly Color MapErrTextCol = new Color(1f, 0.55f, 0.55f);
        private static readonly Color MapNoDataCol = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color MapLvCol = new Color(0.7f, 0.85f, 1f);

        private void InitMapStyles()
        {
            if (mapStylesReady) return;
            mapStylesReady = true;

            mapErrStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            mapTitleStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            mapTitleStyleCache.normal.textColor = Color.white;

            mapCloseStyleCache = new GUIStyle(GUI.skin.button) { fontSize = 36, fontStyle = FontStyle.Bold };

            mapNoDataStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 36, alignment = TextAnchor.MiddleCenter };
            mapNoDataStyleCache.normal.textColor = MapNoDataCol;

            mapLvStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 34, alignment = TextAnchor.MiddleCenter };
            mapLvStyleCache.normal.textColor = MapLvCol;

            // 리전 마커/디테일 — textColor 분기/alpha 동적이라 매 호출 갱신
            regionNameStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            regionDiffStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 23, alignment = TextAnchor.MiddleCenter };
            regionSubNameStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            regionInvisibleStyleCache = new GUIStyle();
            regionRaidLabelStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            nsStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            curStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 31, alignment = TextAnchor.MiddleCenter };
            diffSStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            csStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            btnStyleCache = new GUIStyle(GUI.skin.button) { fontSize = 31 };

            detailTitleStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 41, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            detailTitleStyleCache.normal.textColor = Color.white;
            detailBackStyleCache = new GUIStyle(GUI.skin.button) { fontSize = 34, fontStyle = FontStyle.Bold };
            detailCloseStyleCache = new GUIStyle(GUI.skin.button) { fontSize = 36, fontStyle = FontStyle.Bold };
            detailDescStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            detailSummaryStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 31, alignment = TextAnchor.MiddleCenter };
            detailNoInsectStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 31, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter };

            dexNameStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            dexInfoStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 34 };
            dexCheckStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 31, alignment = TextAnchor.MiddleRight };
            dexUnknownStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            dexHiddenNameStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 34, alignment = TextAnchor.MiddleLeft };
            dexNotCaughtStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 34 };

            symStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 18, alignment = TextAnchor.MiddleCenter };
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
            // 죽거나 디스폰된 레이드 보스 마커 제거
            for (int i = raidMarkers.Count - 1; i >= 0; i--)
            {
                if (raidMarkers[i].entity == null || !raidMarkers[i].entity.gameObject.activeInHierarchy)
                    raidMarkers.RemoveAt(i);
            }

            if (errorTimer > 0f) errorTimer -= Time.deltaTime;
        }

        private void OnGUI()
        {
            if (!isOpen) return;

            UIScale.Begin();
            if (selectedRegionId != null)
                DrawRegionDetail();
            else
                DrawMap();
            DrawErrorToast();
            UIScale.End();
        }

        private void DrawErrorToast()
        {
            if (errorTimer <= 0f || string.IsNullOrEmpty(errorMessage)) return;

            float sw = UIScale.VirtualScreenWidth;
            float sh = UIScale.VirtualScreenHeight;
            float bw = 860f;
            float bh = 96f;
            float bx = (sw - bw) * 0.5f;
            float by = sh * 0.7f;

            float alpha = Mathf.Clamp01(errorTimer);

            GUI.color = new Color(0f, 0f, 0f, 0.85f * alpha);
            GUI.DrawTexture(new Rect(bx, by, bw, bh), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 0.3f, 0.3f, alpha);
            GUI.DrawTexture(new Rect(bx, by, bw, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bx, by + bh - 3, bw, 3), Texture2D.whiteTexture);

            InitMapStyles();
            mapErrStyleCache.normal.textColor = new Color(MapErrTextCol.r, MapErrTextCol.g, MapErrTextCol.b, alpha);
            GUI.color = Color.white;
            GUI.Label(new Rect(bx, by, bw, bh), errorMessage, mapErrStyleCache);
        }

        private void DrawMap()
        {
            InitMapStyles();

            float panelW = Mathf.Min(1060f, UIScale.ContentWidth(18f));
            float panelH = Mathf.Min(960f,
                UIScale.VirtualScreenHeight - UIScale.VirtualSafeTop - UIScale.VirtualSafeBottom - 36f);
            float px = (UIScale.VirtualScreenWidth - panelW) / 2f;
            float py = (UIScale.VirtualScreenHeight - panelH) / 2f;

            GUI.color = new Color(0.04f, 0.06f, 0.1f, 0.95f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = new Color(0.15f, 0.18f, 0.25f);
            GUI.DrawTexture(new Rect(px, py, panelW, 84), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(px, py + 14, panelW - 72, 58), "WORLD MAP", mapTitleStyleCache);

            if (GUI.Button(new Rect(px + panelW - 72, py + 12, 60, 60), "X", mapCloseStyleCache))
            {
                CloseModal();
            }

            if (regionManager == null || regionManager.Regions == null)
            {
                GUI.Label(new Rect(px, py + 84, panelW, panelH - 84), "No regions available", mapNoDataStyleCache);
                return;
            }

            int playerLv = progress != null ? progress.Level : 1;
            GUI.Label(new Rect(px, py + 92, panelW, 44), $"Player Level: {playerLv}", mapLvStyleCache);

            float mapX = px + 30;
            float mapY = py + 144;
            float mapW = panelW - 60;
            float mapH = panelH - 154;

            GUI.color = new Color(0.08f, 0.1f, 0.15f, 0.8f);
            GUI.DrawTexture(new Rect(mapX, mapY, mapW, mapH), Texture2D.whiteTexture);

            DrawMiniMap(mapX, mapY, mapW, mapH);
            DrawRegionCards(mapX, mapY, mapW, mapH);
        }

        // 월드→미니맵 좌표 변환용 캐시
        private float wmMinX, wmMinZ, wmW, wmH, wmMiniX, wmMiniY, wmMiniW, wmMiniH;

        private Vector2 WorldToMini(float wx, float wz)
        {
            float nx = (wx - wmMinX) / wmW;
            float nz = (wz - wmMinZ) / wmH;
            return new Vector2(wmMiniX + nx * wmMiniW, wmMiniY + (1f - nz) * wmMiniH);
        }

        private void DrawMiniMap(float mx, float my, float mw, float mh)
        {
            if (regionManager.Regions == null) return;

            float worldMinX = float.MaxValue, worldMaxX = float.MinValue;
            float worldMinZ = float.MaxValue, worldMaxZ = float.MinValue;
            foreach (var r in regionManager.Regions)
            {
                float left = r.centerPosition.x - r.radius;
                float right = r.centerPosition.x + r.radius;
                float bottom = r.centerPosition.z - r.radius;
                float top = r.centerPosition.z + r.radius;
                if (left < worldMinX) worldMinX = left;
                if (right > worldMaxX) worldMaxX = right;
                if (bottom < worldMinZ) worldMinZ = bottom;
                if (top > worldMaxZ) worldMaxZ = top;
            }

            float padding = 30f;
            worldMinX -= padding; worldMaxX += padding;
            worldMinZ -= padding; worldMaxZ += padding;

            float worldW = worldMaxX - worldMinX;
            float worldH = worldMaxZ - worldMinZ;
            if (worldW < 1) worldW = 1;
            if (worldH < 1) worldH = 1;

            float miniH = mh * 0.45f;
            float miniW = mw;
            float miniX = mx;
            float miniY = my;

            // 변환 캐시 저장
            wmMinX = worldMinX; wmMinZ = worldMinZ; wmW = worldW; wmH = worldH;
            wmMiniX = miniX; wmMiniY = miniY; wmMiniW = miniW; wmMiniH = miniH;

            // ─── 지형지물 먼저 그리기 (리전 아래 레이어) ───
            DrawMapTerrain(worldMinX, worldMinZ, worldW, worldH, miniX, miniY, miniW, miniH);

            foreach (var r in regionManager.Regions)
            {
                bool accessible = regionManager.IsRegionAccessible(r);
                bool isCurrent = regionManager.CurrentRegion == r;

                float nx = (r.centerPosition.x - worldMinX) / worldW;
                float nz = (r.centerPosition.z - worldMinZ) / worldH;
                float nr = r.radius / Mathf.Max(worldW, worldH);

                float cx = miniX + nx * miniW;
                float cy = miniY + (1f - nz) * miniH;
                float cr = nr * Mathf.Min(miniW, miniH);
                cr = Mathf.Max(cr, 20f);

                Color col = r.themeColor;
                // 원형 fill — 동심원 8 rings × 32 samples = 256 small samples.
                // 옛 사각 DrawTexture(cx-cr, cy-cr, 2cr, 2cr)는 "네모 박스" 인상의 원인 (사용자 명시 요청).
                GUI.color = new Color(col.r, col.g, col.b, isCurrent ? 0.5f : 0.25f);
                int fillRings = 8;
                int fillSamples = 32;
                float dotSize = Mathf.Max(3f, cr / fillRings * 1.4f); // 동심원 간 빈틈 없도록 약간 큰 점
                for (int fr = 0; fr <= fillRings; fr++)
                {
                    float ringR = cr * fr / fillRings;
                    if (fr == 0)
                    {
                        // 중앙 1개 점
                        GUI.DrawTexture(new Rect(cx - dotSize * 0.5f, cy - dotSize * 0.5f, dotSize, dotSize), Texture2D.whiteTexture);
                        continue;
                    }
                    for (int fs = 0; fs < fillSamples; fs++)
                    {
                        float fa = (Mathf.PI * 2f / fillSamples) * fs;
                        float fx = cx + Mathf.Cos(fa) * ringR - dotSize * 0.5f;
                        float fy = cy + Mathf.Sin(fa) * ringR - dotSize * 0.5f;
                        GUI.DrawTexture(new Rect(fx, fy, dotSize, dotSize), Texture2D.whiteTexture);
                    }
                }

                // 리전 테두리(원형 ring) — 32 sample 점으로 ring 근사. 옛 사각 테두리는 "네모 박스" 인상의 원인.
                int ringSamples = 32;
                float pulse = isCurrent ? (0.7f + 0.3f * Mathf.Sin(Time.time * 3.5f)) : 1f;
                Color ringCol = new Color(col.r * 0.6f + 0.2f, col.g * 0.6f + 0.2f, col.b * 0.6f + 0.2f,
                    (isCurrent ? 0.95f : 0.7f) * pulse);
                GUI.color = ringCol;
                float ringSize = isCurrent ? 4f : 2.5f;
                for (int rs = 0; rs < ringSamples; rs++)
                {
                    float ang = (Mathf.PI * 2f / ringSamples) * rs;
                    float rx = cx + Mathf.Cos(ang) * cr - ringSize * 0.5f;
                    float ry = cy + Mathf.Sin(ang) * cr - ringSize * 0.5f;
                    GUI.DrawTexture(new Rect(rx, ry, ringSize, ringSize), Texture2D.whiteTexture);
                }

                // gateway 점 — 자동 검출 인접 리전 angle에 노란 점 표시 (통과 가능 지점 시각화)
                Color gatewayCol = new Color(1f, 0.85f, 0.3f, 0.95f);
                GUI.color = gatewayCol;
                if (regionManager.Regions != null)
                {
                    foreach (RegionData other in regionManager.Regions)
                    {
                        if (other == null || other == r) continue;
                        Vector3 d = other.centerPosition - r.centerPosition;
                        float dist = new Vector2(d.x, d.z).magnitude;
                        if (dist > r.radius + other.radius + 30f) continue;
                        float ang = Mathf.Atan2(d.z, d.x);
                        float gx = cx + Mathf.Cos(ang) * cr - 4f;
                        // miniY 축 inverted (1f - nz), z+는 화면 위. atan2 sin은 그대로 사용 후 Y 부호 반전
                        float gy = cy - Mathf.Sin(ang) * cr - 4f;
                        GUI.DrawTexture(new Rect(gx, gy, 8f, 8f), Texture2D.whiteTexture);
                    }
                }
                GUI.color = Color.white;

                regionNameStyleCache.normal.textColor = Color.white;
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - 110, cy - 19, 220, 38), r.displayName, regionNameStyleCache);

                string diffLabel;
                Color diffColor;
                if (r.requiredLevel <= 2) { diffLabel = "쉬움"; diffColor = new Color(0.4f, 0.9f, 0.5f); }
                else if (r.requiredLevel <= 5) { diffLabel = "보통"; diffColor = new Color(0.9f, 0.8f, 0.3f); }
                else { diffLabel = "어려움"; diffColor = new Color(1f, 0.4f, 0.3f); }

                regionDiffStyleCache.normal.textColor = diffColor;
                GUI.Label(new Rect(cx - 80, cy + 22, 160, 34), $"난이도: {diffLabel}", regionDiffStyleCache);

                // 서브에리어 점 표시 + 클릭 시 텔레포트
                if (r.subAreas != null)
                {
                    foreach (var sub in r.subAreas)
                    {
                        float snx = (sub.centerPosition.x - worldMinX) / worldW;
                        float snz = (sub.centerPosition.z - worldMinZ) / worldH;
                        float scx = miniX + snx * miniW;
                        float scy = miniY + (1f - snz) * miniH;
                        float sCr = Mathf.Max(5f, nr * Mathf.Min(miniW, miniH) * 0.25f);

                        Color subCol = GetSubAreaColor(sub.environmentType);
                        bool regionAccessible = regionManager == null || regionManager.IsRegionAccessible(r);

                        // 닫힌 상위 리전 SubArea는 회색
                        if (!regionAccessible)
                            subCol = new Color(0.4f, 0.4f, 0.4f);

                        GUI.color = new Color(subCol.r, subCol.g, subCol.b, 0.7f);
                        GUI.DrawTexture(new Rect(scx - sCr, scy - sCr, sCr * 2, sCr * 2), Texture2D.whiteTexture);
                        // 테두리
                        GUI.color = new Color(subCol.r + 0.2f, subCol.g + 0.2f, subCol.b + 0.2f, 0.9f);
                        GUI.DrawTexture(new Rect(scx - sCr, scy - sCr, sCr * 2, 1), Texture2D.whiteTexture);
                        GUI.DrawTexture(new Rect(scx - sCr, scy + sCr - 1, sCr * 2, 1), Texture2D.whiteTexture);

                        regionSubNameStyleCache.fontSize = 18;
                        regionSubNameStyleCache.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 0.8f);
                        GUI.color = Color.white;
                        GUI.Label(new Rect(scx - 60, scy + sCr + 2, 120, 24), sub.displayName, regionSubNameStyleCache);

                        // 클릭 핫존: 점 위 투명 버튼으로 텔레포트 트리거
                        float hot = Mathf.Max(sCr, 14f);
                        Rect hotRect = new Rect(scx - hot, scy - hot, hot * 2, hot * 2);
                        if (GUI.Button(hotRect, "", regionInvisibleStyleCache))
                        {
                            if (regionAccessible)
                            {
                                TeleportToSubArea(sub);
                            }
                            else
                            {
                                // 잠금은 unlockedRegions HashSet 기반 — 이전 region의 수문장을 격파해야 해금됨
                                errorMessage = $"먼저 {r.displayName}을(를) 해금해야 합니다 (이전 지역의 수문장을 격파하세요)";
                                errorTimer = 3f;
                            }
                        }
                    }
                }
            }

            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                float pnx = (player.transform.position.x - worldMinX) / worldW;
                float pnz = (player.transform.position.z - worldMinZ) / worldH;
                float ppx = miniX + pnx * miniW;
                float ppy = miniY + (1f - pnz) * miniH;
                GUI.color = Color.yellow;
                GUI.DrawTexture(new Rect(ppx - 8, ppy - 8, 16, 16), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // 레이드 보스 마커
            foreach (var marker in raidMarkers)
            {
                float rnx = (marker.worldPos.x - worldMinX) / worldW;
                float rnz = (marker.worldPos.z - worldMinZ) / worldH;
                float rpx = miniX + rnx * miniW;
                float rpy = miniY + (1f - rnz) * miniH;

                Color raidCol = marker.rarity == InsectRarity.Legendary
                    ? new Color(1f, 0.8f, 0.15f) : new Color(0.7f, 0.3f, 0.95f);
                float pulse = 0.7f + Mathf.Sin(Time.time * 4f) * 0.3f;

                // 외곽 링
                GUI.color = new Color(raidCol.r, raidCol.g, raidCol.b, 0.3f * pulse);
                GUI.DrawTexture(new Rect(rpx - 14, rpy - 14, 28, 28), Texture2D.whiteTexture);
                // 내부 점
                GUI.color = new Color(raidCol.r, raidCol.g, raidCol.b, pulse);
                GUI.DrawTexture(new Rect(rpx - 6, rpy - 6, 12, 12), Texture2D.whiteTexture);
                // 이름
                regionRaidLabelStyleCache.fontSize = 18;
                regionRaidLabelStyleCache.normal.textColor = raidCol;
                GUI.color = Color.white;
                GUI.Label(new Rect(rpx - 60, rpy + 12, 120, 24), marker.name, regionRaidLabelStyleCache);
            }

            float cardY = miniY + miniH + 10;
            float cardH = mh - miniH - 15;
            DrawRegionList(mx, cardY, mw, cardH);
        }

        private void DrawRegionList(float x, float y, float w, float h)
        {
            RegionData[] accessible = regionManager.GetAccessibleRegions();
            if (accessible.Length == 0) return;

            float cardW = (w - 10 * (accessible.Length - 1)) / Mathf.Max(accessible.Length, 1);
            cardW = Mathf.Min(cardW, 320);

            for (int i = 0; i < accessible.Length; i++)
            {
                var r = accessible[i];
                float cx = x + i * (cardW + 10);
                bool isCurrent = regionManager.CurrentRegion == r;

                GUI.color = new Color(r.themeColor.r * 0.2f, r.themeColor.g * 0.2f, r.themeColor.b * 0.2f, 0.7f);
                GUI.DrawTexture(new Rect(cx, y, cardW, h), Texture2D.whiteTexture);

                GUI.color = r.themeColor;
                GUI.DrawTexture(new Rect(cx, y, cardW, 3), Texture2D.whiteTexture);

                if (isCurrent)
                {
                    GUI.color = new Color(r.themeColor.r, r.themeColor.g, r.themeColor.b, 0.15f);
                    GUI.DrawTexture(new Rect(cx, y, cardW, h), Texture2D.whiteTexture);
                }

                nsStyleCache.normal.textColor = r.themeColor;
                GUI.color = Color.white;
                GUI.Label(new Rect(cx, y + 14, cardW, 46), r.displayName, nsStyleCache);

                if (isCurrent)
                {
                    curStyleCache.normal.textColor = new Color(0.5f, 1f, 0.5f);
                    GUI.Label(new Rect(cx, y + 66, cardW, 40), "현재 위치", curStyleCache);
                }

                string diffLabel;
                Color diffColor;
                if (r.requiredLevel <= 2) { diffLabel = "쉬움"; diffColor = new Color(0.4f, 0.9f, 0.5f); }
                else if (r.requiredLevel <= 5) { diffLabel = "보통"; diffColor = new Color(0.9f, 0.8f, 0.3f); }
                else { diffLabel = "어려움"; diffColor = new Color(1f, 0.4f, 0.3f); }
                diffSStyleCache.normal.textColor = diffColor;
                float diffY = isCurrent ? y + 112 : y + 66;
                GUI.Label(new Rect(cx, diffY, cardW, 36), $"난이도: {diffLabel}", diffSStyleCache);

                int total = r.insectIds != null ? r.insectIds.Length : 0;
                int caught = CountCaught(r);

                float dexButtonH = UIScale.IsMobileLayout ? 64f : 50f;
                float dexBtnTop = y + h - dexButtonH - 12f;
                float barW = cardW - 30;
                float barY = dexBtnTop - 24f;

                csStyleCache.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                GUI.Label(new Rect(cx, barY - 46f, cardW, 40), $"{caught}/{total}", csStyleCache);

                float bar = total > 0 ? (float)caught / total : 0;
                GUI.color = new Color(0.15f, 0.15f, 0.2f);
                GUI.DrawTexture(new Rect(cx + 10, barY, barW, 14), Texture2D.whiteTexture);
                GUI.color = new Color(0.3f, 0.8f, 0.3f);
                GUI.DrawTexture(new Rect(cx + 10, barY, barW * bar, 14), Texture2D.whiteTexture);

                GUI.backgroundColor = new Color(0.2f, 0.3f, 0.5f);
                GUI.color = Color.white;
                if (GUI.Button(new Rect(cx + 5, dexBtnTop, cardW - 10, dexButtonH), "도감", btnStyleCache))
                    selectedRegionId = r.regionId;
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawRegionCards(float mx, float my, float mw, float mh) { }

        private void DrawRegionDetail()
        {
            InitMapStyles();

            RegionData region = regionManager.GetRegionById(selectedRegionId);
            if (region == null) { selectedRegionId = null; return; }

            float panelW = Mathf.Min(1060f, UIScale.ContentWidth(18f));
            float panelH = Mathf.Min(960f,
                UIScale.VirtualScreenHeight - UIScale.VirtualSafeTop - UIScale.VirtualSafeBottom - 36f);
            float px = (UIScale.VirtualScreenWidth - panelW) / 2f;
            float py = (UIScale.VirtualScreenHeight - panelH) / 2f;

            GUI.color = new Color(0.04f, 0.06f, 0.1f, 0.95f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = new Color(region.themeColor.r * 0.3f, region.themeColor.g * 0.3f, region.themeColor.b * 0.3f, 1f);
            GUI.DrawTexture(new Rect(px, py, panelW, 84), Texture2D.whiteTexture);
            GUI.color = region.themeColor;
            GUI.DrawTexture(new Rect(px, py + 84, panelW, 5), Texture2D.whiteTexture);

            detailTitleStyleCache.normal.textColor = region.themeColor;
            GUI.color = Color.white;
            GUI.Label(new Rect(px + 150, py + 14, panelW - 300, 58), $"{region.displayName} Dex", detailTitleStyleCache);

            if (GUI.Button(new Rect(px + 14, py + 10f, 160f, UIScale.IsMobileLayout ? 64f : 52f), "< Back", detailBackStyleCache))
                selectedRegionId = null;

            if (GUI.Button(new Rect(px + panelW - 76f, py + 10f, 64f, UIScale.IsMobileLayout ? 64f : 52f), "X", detailCloseStyleCache))
            {
                CloseModal();
            }

            if (!string.IsNullOrEmpty(region.description))
            {
                detailDescStyleCache.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                GUI.Label(new Rect(px + 30, py + 96, panelW - 60, 42), region.description, detailDescStyleCache);
            }

            int total = region.insectIds != null ? region.insectIds.Length : 0;
            int caught = CountCaught(region);
            detailSummaryStyleCache.normal.textColor = new Color(0.7f, 0.85f, 1f);
            GUI.Label(new Rect(px, py + 146, panelW, 44), $"Captured: {caught} / {total}", detailSummaryStyleCache);

            float barX = px + panelW * 0.2f;
            float barW = panelW * 0.6f;
            float barY = py + 198;
            GUI.color = new Color(0.15f, 0.15f, 0.2f);
            GUI.DrawTexture(new Rect(barX, barY, barW, 18), Texture2D.whiteTexture);
            float fill = total > 0 ? (float)caught / total : 0;
            GUI.color = region.themeColor;
            GUI.DrawTexture(new Rect(barX, barY, barW * fill, 18), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float listY = py + 232;
            float listH = panelH - 242;
            Rect listArea = new Rect(px + 15, listY, panelW - 30, listH);

            if (region.insectIds == null || region.insectIds.Length == 0)
            {
                detailNoInsectStyleCache.fontSize = 20;
                detailNoInsectStyleCache.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
                GUI.Label(listArea, "No insects registered", detailNoInsectStyleCache);
                return;
            }

            float itemH = 140f;
            float totalListH = region.insectIds.Length * itemH;
            Rect viewRect = new Rect(0, 0, listArea.width - 20, totalListH);

            dexScroll = GUI.BeginScrollView(listArea, dexScroll, viewRect);
            for (int i = 0; i < region.insectIds.Length; i++)
            {
                DrawDexItem(new Rect(0, i * itemH, viewRect.width, itemH - 4), region.insectIds[i], region.themeColor);
            }
            GUI.EndScrollView();
        }

        private void DrawDexItem(Rect rect, string insectId, Color themeCol)
        {
            InsectData data = FindInsectData(insectId);
            bool isCaught = dex != null && dex.HasRecord(insectId);

            GUI.color = new Color(0.1f, 0.12f, 0.18f, 0.85f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            if (isCaught && data != null)
            {
                Color rarityCol = UITheme.Instance.GetInsectRarityColor(data.rarity);
                GUI.color = rarityCol;
                GUI.DrawTexture(new Rect(rect.x, rect.y, 6, rect.height), Texture2D.whiteTexture);

                CapturePopupUI.DrawTypedInsectPortrait(rect.x + 62, rect.y + rect.height / 2f, data.insectId, data.rarity, 1f);

                dexNameStyleCache.normal.textColor = rarityCol;
                GUI.color = Color.white;
                GUI.Label(new Rect(rect.x + 120, rect.y + 18, rect.width - 200, 46), data.displayName, dexNameStyleCache);

                dexInfoStyleCache.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                GUI.Label(new Rect(rect.x + 120, rect.y + 68, rect.width - 200, 42),
                    $"{data.rarity}  |  CP {PlayerInsectCombatPower.CalculateBasePreview(data, data.minLevel)}", dexInfoStyleCache);

                dexCheckStyleCache.fontSize = 41;
                dexCheckStyleCache.fontStyle = FontStyle.Bold;
                dexCheckStyleCache.normal.textColor = new Color(0.3f, 1f, 0.5f);
                GUI.Label(new Rect(rect.x + rect.width - 72, rect.y + 22, 60, 60), "V", dexCheckStyleCache);
            }
            else
            {
                GUI.color = new Color(0.3f, 0.3f, 0.3f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 6, rect.height), Texture2D.whiteTexture);

                GUI.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                GUI.DrawTexture(new Rect(rect.x + 24, rect.y + rect.height / 2f - 34, 68, 68), Texture2D.whiteTexture);

                dexUnknownStyleCache.fontSize = 49;
                dexUnknownStyleCache.alignment = TextAnchor.MiddleCenter;
                dexUnknownStyleCache.normal.textColor = new Color(0.35f, 0.35f, 0.35f);
                GUI.color = Color.white;
                GUI.Label(new Rect(rect.x + 24, rect.y + rect.height / 2f - 34, 68, 68), "?", dexUnknownStyleCache);

                dexHiddenNameStyleCache.fontStyle = FontStyle.Italic;
                dexHiddenNameStyleCache.normal.textColor = new Color(0.4f, 0.4f, 0.4f);
                string displayHint = data != null ? new string('?', data.displayName.Length) : "???";
                GUI.Label(new Rect(rect.x + 120, rect.y + 26, rect.width - 200, 46), displayHint, dexHiddenNameStyleCache);

                dexNotCaughtStyleCache.normal.textColor = new Color(0.35f, 0.35f, 0.35f);
                GUI.Label(new Rect(rect.x + 120, rect.y + 74, rect.width - 200, 42), "아직 포획하지 않음", dexNotCaughtStyleCache);
            }
        }

        private int CountCaught(RegionData region)
        {
            if (region == null || region.insectIds == null || dex == null) return 0;
            int count = 0;
            foreach (string id in region.insectIds)
            {
                if (dex.HasRecord(id)) count++;
            }
            return count;
        }

        private InsectData FindInsectData(string insectId)
        {
            if (database == null || database.insects == null) return null;
            foreach (var d in database.insects)
            {
                if (d != null && d.insectId == insectId) return d;
            }
            return null;
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

        private void DrawMapTerrain(float wMinX, float wMinZ, float wW, float wH, float mX, float mY, float mW, float mH)
        {
            if (regionManager.Regions == null) return;

            // ─── 리전 간 길 ───
            Color pathCol = new Color(0.55f, 0.48f, 0.32f, 0.6f);
            RegionData[] regions = regionManager.Regions;

            // 연결 관계: meadow↔pond, meadow↔forest, meadow↔swamp, meadow↔garden, mountain↔ruins, forest↔swamp
            string[,] connections = {
                {"meadow","pond"}, {"meadow","forest"}, {"meadow","swamp"}, {"meadow","garden"},
                {"mountain","ruins"}, {"forest","swamp"}
            };

            for (int i = 0; i < connections.GetLength(0); i++)
            {
                RegionData a = FindRegion(regions, connections[i, 0]);
                RegionData b = FindRegion(regions, connections[i, 1]);
                if (a == null || b == null) continue;

                Vector2 pa = WorldToMini(a.centerPosition.x, a.centerPosition.z);
                Vector2 pb = WorldToMini(b.centerPosition.x, b.centerPosition.z);
                DrawMapLine(pa, pb, 3f, pathCol);
            }

            // ─── 강 (pond 근처) ───
            RegionData pond = FindRegion(regions, "pond");
            if (pond != null)
            {
                Vector2 riverStart = WorldToMini(pond.centerPosition.x, pond.centerPosition.z);
                Vector2 riverEnd = WorldToMini(pond.centerPosition.x - pond.radius * 0.8f, pond.centerPosition.z);
                DrawMapLine(riverStart, riverEnd, 4f, new Color(0.2f, 0.45f, 0.7f, 0.7f));

                // 다리 표시
                Vector2 bridgePos = Vector2.Lerp(riverStart, riverEnd, 0.5f);
                GUI.color = new Color(0.5f, 0.35f, 0.15f, 0.9f);
                GUI.DrawTexture(new Rect(bridgePos.x - 6, bridgePos.y - 3, 12, 6), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // ─── 절벽 (forest↔mountain) ───
            RegionData forest = FindRegion(regions, "forest");
            RegionData mountain = FindRegion(regions, "mountain");
            if (forest != null && mountain != null)
            {
                Vector2 cliffA = WorldToMini(forest.centerPosition.x, forest.centerPosition.z);
                Vector2 cliffB = WorldToMini(mountain.centerPosition.x, mountain.centerPosition.z);
                Vector2 mid = (cliffA + cliffB) * 0.5f;
                Vector2 dir = (cliffB - cliffA).normalized;
                Vector2 perp = new Vector2(-dir.y, dir.x);

                // 절벽 = 두꺼운 갈색 선 + 지그재그
                for (int j = 0; j < 5; j++)
                {
                    float t = (j + 0.5f) / 5f;
                    Vector2 p = Vector2.Lerp(cliffA, cliffB, t);
                    p += perp * Mathf.Sin(t * Mathf.PI * 2f) * 4f;
                    float zag = (j % 2 == 0) ? 2f : -2f;
                    GUI.color = new Color(0.45f, 0.38f, 0.3f, 0.8f);
                    GUI.DrawTexture(new Rect(p.x - 4 + zag, p.y - 4, 8, 8), Texture2D.whiteTexture);
                }

                // mountain↔ruins 다리
                RegionData ruins = FindRegion(regions, "ruins");
                if (ruins != null)
                {
                    Vector2 bStart = WorldToMini(mountain.centerPosition.x, mountain.centerPosition.z);
                    Vector2 bEnd = WorldToMini(ruins.centerPosition.x, ruins.centerPosition.z);
                    Vector2 bMid = Vector2.Lerp(bStart, bEnd, 0.5f);
                    GUI.color = new Color(0.5f, 0.45f, 0.38f, 0.9f);
                    GUI.DrawTexture(new Rect(bMid.x - 8, bMid.y - 3, 16, 6), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
            }

            // ─── 리전 내 지형 심볼 ───
            foreach (var r in regions)
            {
                Vector2 rc = WorldToMini(r.centerPosition.x, r.centerPosition.z);
                float rr = (r.radius / Mathf.Max(wW, wH)) * Mathf.Min(mW, mH);
                rr = Mathf.Max(rr, 20f);

                symStyleCache.fontSize = 18;
                symStyleCache.normal.textColor = new Color(1f, 1f, 1f, 0.4f);
                GUI.color = Color.white;

                string sym = GetRegionSymbol(r.regionId);
                if (!string.IsNullOrEmpty(sym))
                    GUI.Label(new Rect(rc.x - 40, rc.y + rr * 0.3f, 80, 24), sym, symStyleCache);
            }

            GUI.color = Color.white;
        }

        private void DrawMapLine(Vector2 a, Vector2 b, float thickness, Color col)
        {
            Vector2 dir = b - a;
            float len = dir.magnitude;
            if (len < 1f) return;
            int segments = Mathf.Max(2, Mathf.RoundToInt(len / 6f));
            float segLen = len / segments;

            GUI.color = col;
            for (int i = 0; i < segments; i++)
            {
                float t = ((float)i + 0.5f) / segments;
                Vector2 p = Vector2.Lerp(a, b, t);
                Vector2 d = dir.normalized;
                // 방향에 따라 가로/세로 선택
                if (Mathf.Abs(d.x) > Mathf.Abs(d.y))
                    GUI.DrawTexture(new Rect(p.x - segLen * 0.5f, p.y - thickness * 0.5f, segLen, thickness), Texture2D.whiteTexture);
                else
                    GUI.DrawTexture(new Rect(p.x - thickness * 0.5f, p.y - segLen * 0.5f, thickness, segLen), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        private RegionData FindRegion(RegionData[] regions, string id)
        {
            foreach (var r in regions)
                if (r.regionId == id) return r;
            return null;
        }

        private string GetRegionSymbol(string regionId)
        {
            switch (regionId)
            {
                case "meadow": return "~ ~ ~";
                case "pond": return "~~~";
                case "forest": return "TTT";
                case "swamp": return "...";
                case "mountain": return "/\\";
                case "garden": return "***";
                case "ruins": return "|||";
                default: return null;
            }
        }

        private void TeleportToSubArea(Data.SubAreaData sub)
        {
            if (sub == null) return;
            GameObject player = GameObject.Find("Player");
            if (player == null) return;
            // SubArea 중심점 위에 배치 — RegionManager.Update가 ContainsPoint 감지 → 자동 진입
            Vector3 dest = sub.centerPosition;
            dest.y = player.transform.position.y;
            player.transform.position = dest;
            // 맵 UI는 닫지 않음 — 사용자가 닫음. 다음 Update에서 SubArea 진입 알림 팝업이 자동 발화.
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
    }
}
