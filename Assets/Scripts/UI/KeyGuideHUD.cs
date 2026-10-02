using InsectGame.Battle;
using InsectGame.Capture;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    public class KeyGuideHUD : MonoBehaviour
    {
        [SerializeField] private CaptureMinigameController minigame;
        [SerializeField] private InsectBattleController battleController;
        [SerializeField] private InsectBattleUIController battleUi;
        [SerializeField] private RegionManager regionManager;
        [SerializeField] private PlayerItemInventory itemInventory;

        private bool battleActive;

        // GUIStyle 캐싱
        private GUIStyle headerStyle;
        private GUIStyle keyStyle;
        private GUIStyle descStyle;
        private GUIStyle centeredKeyStyle;
        private GUIStyle hintStyle;
        private GUIStyle titleStyle;
        private GUIStyle itemNameStyle;
        private GUIStyle itemCountStyle;
        private bool stylesInit;

        private void InitStyles()
        {
            if (stylesInit) return;
            stylesInit = true;

            headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 36, fontStyle = FontStyle.Bold };
            headerStyle.normal.textColor = new Color(0.95f, 0.88f, 0.5f);

            keyStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold };
            keyStyle.normal.textColor = Color.white;

            descStyle = new GUIStyle(GUI.skin.label) { fontSize = 32 };
            descStyle.normal.textColor = new Color(0.78f, 0.78f, 0.78f);

            centeredKeyStyle = new GUIStyle(keyStyle) { alignment = TextAnchor.MiddleCenter };

            hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold };

            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold };

            itemNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 30 };

            itemCountStyle = new GUIStyle(GUI.skin.label) { fontSize = 30 };
        }

        private void OnEnable()
        {
            if (battleController != null)
            {
                battleController.BattleUpdated += OnBattleUpdated;
                battleController.BattleEnded += OnBattleEnded;
            }
        }

        private void OnDisable()
        {
            if (battleController != null)
            {
                battleController.BattleUpdated -= OnBattleUpdated;
                battleController.BattleEnded -= OnBattleEnded;
            }
        }

        private void OnBattleUpdated(InsectBattleStats p, InsectBattleStats e) { battleActive = true; }
        private void OnBattleEnded(bool won) { battleActive = false; }

        private void OnGUI()
        {
            // 모달이 열려 있으면 HUD를 숨긴다. depth를 안 거는 전체화면 모달(CollectionUI,
            // TrainingUI, RegionMapUI 등)과 렌더 순서가 미정의라 패널 위로 튀어나올 수 있다.
            // UIScale.Begin() 전에 return해야 Begin/End 균형이 유지된다(MinimapUI:52 관례).
            if (ModalUIRegistry.IsAnyOpen()) return;

            UIScale.Begin();
            InitStyles();
            // 조작법(키 안내표) 제거 — 사용자 요청으로 미표시. 퀘스트 추적은 TutorialQuestUI가 담당.
            // (DrawKeyGuide/DrawKeyRow는 더 이상 호출하지 않음 — 후속 정리 대상, 참조는 유지돼 경고 없음.)
            DrawCurrentRegion();
            if (!UIScale.IsMobileLayout) DrawCaptureItems();
            UIScale.End();
        }

        private void DrawKeyGuide()
        {
            float x = 20f;
            float lineH = 62f;
            int rowCount = 8;   // WASD·E·T·G·I·N·C·M — 행을 늘리면 여기도 함께(안 맞으면 패널 밖으로 넘친다)
            bool inMinigame = minigame != null && minigame.IsActive;
            if (inMinigame) rowCount++;
            if (battleActive) rowCount++;
            float bgH = (rowCount + 1) * lineH + 20;
            float y = UISafeLayout.BottomY(bgH);

            GUI.color = new Color(0, 0, 0, 0.6f);
            GUI.DrawTexture(new Rect(x - 8, y - 8, 560, bgH), Texture2D.whiteTexture);
            GUI.color = new Color(0.4f, 0.5f, 0.8f, 0.6f);
            GUI.DrawTexture(new Rect(x - 8, y - 8, 560, 4), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 6, y, 460, lineH), "조작법", headerStyle);
            y += lineH + 2;

            DrawKeyRow(x, ref y, lineH, "WASD", "이동", keyStyle, descStyle);
            DrawKeyRow(x, ref y, lineH, "E", inMinigame ? "타이밍 확인" : "포획", keyStyle, descStyle);

            if (inMinigame)
                DrawKeyRow(x, ref y, lineH, "ESC", "포획 취소", keyStyle, descStyle);

            if (battleActive)
                DrawKeyRow(x, ref y, lineH, "1~4", "스킬 사용", keyStyle, descStyle);

            DrawKeyRow(x, ref y, lineH, "T", "배틀 팀", keyStyle, descStyle);
            DrawKeyRow(x, ref y, lineH, "G", "훈련", keyStyle, descStyle);
            DrawKeyRow(x, ref y, lineH, "I", "가방", keyStyle, descStyle);
            DrawKeyRow(x, ref y, lineH, "N", "도감", keyStyle, descStyle);
            // 컬렉션 실제 바인딩은 C다(`QuickAccessBarUI.buttons[]`의 key가 단일 출처다 —
            // 예전 주석이 적어 둔 줄 번호는 이미 어긋나 있었다). TAB은 이 게임에서
            // 미니게임 확인/로비 오버레이 토글이고 IMGUI에선 포커스 이동 키다 —
            // 안내대로 누르면 컬렉션이 아니라 엉뚱한 동작을 했다.
            //
            // **이 목록 자체가 그 배열의 사본이다** — 6개만 적혀 있고 Q/P/F4/F6/J는 빠졌다.
            // 바인딩을 바꾸면 여기도 손으로 따라가야 한다(감사 P2로 남겼다).
            DrawKeyRow(x, ref y, lineH, "C", "컬렉션", keyStyle, descStyle);
            DrawKeyRow(x, ref y, lineH, "M", "지도", keyStyle, descStyle);
        }

        private void DrawKeyRow(float x, ref float y, float h, string key, string desc, GUIStyle ks, GUIStyle ds)
        {
            float keyW = 120f;
            GUI.color = new Color(0.2f, 0.25f, 0.42f, 0.85f);
            GUI.DrawTexture(new Rect(x, y + 3, keyW, h - 6), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(x, y, keyW, h), key, centeredKeyStyle);
            GUI.Label(new Rect(x + keyW + 16, y, 400, h), desc, ds);
            y += h;
        }

        private void DrawCurrentRegion()
        {
            if (regionManager == null) return;
            UITheme t = UITheme.Instance;

            RegionData current = regionManager.CurrentRegion;
            string regionName = current != null ? current.displayName : "야외";
            Color regionCol = current != null ? current.themeColor : t.textSecondary;
            // 섬(분리 구역)에 있는 동안은 리전 판정이 얼어 있어 CurrentRegion이 떠나온 리전에 머문다 —
            // 그대로 두면 섬 위에서 배너가 "초원"이라고 말한다.
            SubAreaData detached = regionManager.CurrentSubArea;
            if (detached != null && detached.detached)
            {
                regionName = detached.displayName;
                regionCol = t.accentMint;
            }

            bool mobile = UIScale.IsMobileLayout;
            float w = mobile ? 430f : 520f;
            float h = mobile ? 68f : 80f;
            // 진짜 화면 중앙이 아니라 '세이프 에어리어 중앙'으로 — 가로 비대칭 노치 보정.
            float left = UIScale.VirtualSafeLeft;
            float right = UIScale.VirtualScreenWidth - UIScale.VirtualSafeRight;
            if (mobile)
            {
                // 세로 화면은 좌상단 상태 탭과 우상단 단축 바 사이에 둔다. 화면 중앙에 두면
                // 단축 바(폭 324) 밑으로 20px 넘게 파고들었다(2026-09-30 검수 캡처).
                left = PlayerStatusHUD.CollapsedTabRight + UITheme.Space.S;
                right = QuickAccessBarUI.ShortcutBarRect.x - UITheme.Space.S;
                w = Mathf.Min(w, Mathf.Max(1f, right - left));
            }
            Rect banner = new Rect(left + (right - left - w) / 2f, UISafeLayout.ContentTop, w, h);

            // 미니맵·상태 패널과 같은 HUD 카드 + 리전 색 밑줄(얇아서 각진 채, 반경만큼 물린다).
            UISurface.HudCard(banner);
            UISurface.Flat(new Rect(banner.x + UITheme.Radius.Card, banner.yMax - 9f,
                banner.width - UITheme.Radius.Card * 2f, 4f), regionCol);

            hintStyle.fontSize = mobile ? 32 : 40;
            hintStyle.alignment = TextAnchor.MiddleCenter;
            hintStyle.normal.textColor = Color.Lerp(regionCol, t.textPrimary, 0.25f);
            GUI.color = Color.white;
            UIHelper.LabelFit(new Rect(banner.x + 16f, banner.y, banner.width - 32f, banner.height - 8f), regionName, hintStyle);
        }

        private void DrawCaptureItems()
        {
            if (itemInventory == null) return;
            UITheme t = UITheme.Instance;

            float w = 440f;
            float h = 226f;
            Rect panel = new Rect(UIScale.VirtualScreenWidth - UIScale.VirtualSafeRight - w - 20f,
                UISafeLayout.ContentTop, w, h);

            UISurface.HudCard(panel);
            UISurface.Flat(new Rect(panel.x + UITheme.Radius.Card, panel.y + 3f,
                panel.width - UITheme.Radius.Card * 2f, 3f), t.accentMint);

            titleStyle.normal.textColor = t.textSecondary;
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x + 20f, panel.y + 10f, w - 40f, 44f), "포획 아이템", titleStyle);

            // 이름은 아이템 DB·상점·포획 창과 같은 "은빛/황금"이다 — 여기만 "실버/골드"라 같은 그물이 둘로 보였다.
            float iy = panel.y + 60f;
            DrawItemCount(panel.x + 20f, iy, w - 40f, "기본 채집망", itemInventory.GetCount("net_basic"), t.itemCommon);
            DrawItemCount(panel.x + 20f, iy + 52f, w - 40f, "은빛 채집망", itemInventory.GetCount("net_silver"), t.itemRare);
            DrawItemCount(panel.x + 20f, iy + 104f, w - 40f, "황금 채집망", itemInventory.GetCount("net_gold"), t.itemLegendary);
        }

        private void DrawItemCount(float x, float y, float w, string label, int count, Color col)
        {
            UITheme t = UITheme.Instance;
            bool has = count > 0;
            UISurface.Rounded(new Rect(x, y + 10f, 24f, 24f), has ? col : t.surfaceRaised, 6f);

            itemNameStyle.normal.textColor = has ? t.textPrimary : t.textMuted;
            GUI.Label(new Rect(x + 36f, y, w - 150f, 44f), label, itemNameStyle);

            itemCountStyle.fontStyle = FontStyle.Bold;
            itemCountStyle.fontSize = 32;
            itemCountStyle.alignment = TextAnchor.MiddleRight;
            itemCountStyle.normal.textColor = has ? t.accentAmber : t.textMuted;
            GUI.Label(new Rect(x + w - 120f, y, 120f, 44f), $"×{count}", itemCountStyle);
        }

        public void AutoWire(CaptureMinigameController mg, InsectBattleController bc, InsectBattleUIController bui)
        {
            if (minigame == null) minigame = mg;
            if (battleController == null || battleController != bc)
            {
                if (battleController != null)
                {
                    battleController.BattleUpdated -= OnBattleUpdated;
                    battleController.BattleEnded -= OnBattleEnded;
                }
                battleController = bc;
                if (battleController != null)
                {
                    battleController.BattleUpdated += OnBattleUpdated;
                    battleController.BattleEnded += OnBattleEnded;
                }
            }
            if (battleUi == null) battleUi = bui;
        }

        public void AutoWire(RegionManager rm)
        {
            if (regionManager == null) regionManager = rm;
        }

        public void AutoWire(PlayerItemInventory inv)
        {
            if (itemInventory == null) itemInventory = inv;
        }
    }
}
