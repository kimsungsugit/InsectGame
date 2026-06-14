using System;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 동기화 충돌(클라우드가 더 최신 + 로컬에 진행 데이터) 시 사용자에게 선택을 묻는 IMGUI 모달.
    /// CloudSaveManager.ConflictDetected를 구독해 로컬/클라우드 요약을 나란히 보여주고,
    /// 선택 결과를 ResolveConflict로 전달한다. 의존성 없음(CloudSaveManager.Instance 사용).
    /// </summary>
    public class SaveConflictUI : MonoBehaviour, IModalUI
    {
        private bool visible;
        private SaveConflictInfo info;

        // 모달로 등록해 뒤 화면 이동/클릭 차단. 단 충돌은 반드시 선택해야 하므로 ESC(CloseModal)는 무시.
        public bool IsOpen => visible;
        public void CloseModal() { /* 충돌은 명시적 선택 필요 — ESC로 닫지 않음 */ }

        private GUIStyle panelStyle, titleStyle, descStyle, cardTitleStyle, rowStyle, btnLocalStyle, btnCloudStyle;
        private bool stylesReady;

        private void OnEnable()
        {
            if (CloudSaveManager.Instance != null)
                CloudSaveManager.Instance.ConflictDetected += OnConflict;
        }

        private void OnDisable()
        {
            if (CloudSaveManager.Instance != null)
                CloudSaveManager.Instance.ConflictDetected -= OnConflict;
            ModalUIRegistry.Unregister(this);
        }

        private void OnConflict(SaveConflictInfo conflictInfo)
        {
            info = conflictInfo;
            visible = true;
            ModalUIRegistry.Register(this);
        }

        private void OnGUI()
        {
            if (!visible || info == null) return;

            GUI.depth = -50; // LoginUI 로딩 화면 위에 그림
            EnsureStyles();

            // 전체 딤
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float pw = Mathf.Min(880f, Screen.width * 0.92f);
            float ph = Mathf.Min(560f, Screen.height * 0.9f);
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

            GUI.Box(new Rect(px, py, pw, ph), "", panelStyle);

            GUI.Label(new Rect(px, py + 26f, pw, 50f), "동기화 충돌", titleStyle);
            GUI.Label(new Rect(px + 40f, py + 84f, pw - 80f, 56f),
                "이 기기와 클라우드의 저장 데이터가 다릅니다.\n어느 쪽 데이터로 이어서 플레이할지 선택하세요. (선택하지 않은 쪽은 덮어쓰여집니다)",
                descStyle);

            float cardW = (pw - 120f) * 0.5f;
            float cardH = 230f;
            float cardY = py + 156f;
            float leftX = px + 40f;
            float rightX = px + pw - 40f - cardW;

            DrawCard(new Rect(leftX, cardY, cardW, cardH), "이 기기", info.local, new Color(0.18f, 0.32f, 0.5f));
            DrawCard(new Rect(rightX, cardY, cardW, cardH), "클라우드", info.cloud, new Color(0.2f, 0.42f, 0.24f));

            float btnY = cardY + cardH + 24f;
            float btnH = 64f;
            if (GUI.Button(new Rect(leftX, btnY, cardW, btnH), "이 기기 데이터 사용", btnLocalStyle))
                Resolve(false);
            if (GUI.Button(new Rect(rightX, btnY, cardW, btnH), "클라우드 데이터 사용", btnCloudStyle))
                Resolve(true);
        }

        private void Resolve(bool useCloud)
        {
            visible = false;
            ModalUIRegistry.Unregister(this);
            if (CloudSaveManager.Instance != null)
                CloudSaveManager.Instance.ResolveConflict(useCloud);
        }

        private void DrawCard(Rect rect, string title, SaveSummary s, Color accent)
        {
            GUI.color = new Color(0.1f, 0.11f, 0.16f, 0.95f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float x = rect.x + 18f;
            float w = rect.width - 36f;
            float y = rect.y + 12f;

            cardTitleStyle.normal.textColor = new Color(accent.r + 0.4f, accent.g + 0.4f, accent.b + 0.4f);
            GUI.Label(new Rect(x, y, w, 32f), title, cardTitleStyle);
            y += 44f;

            DrawRow(x, ref y, w, "레벨", s.level.ToString());
            DrawRow(x, ref y, w, "곤충", s.insectCount + "마리");
            DrawRow(x, ref y, w, "캔디", s.candies.ToString());
            DrawRow(x, ref y, w, "코인", s.coins.ToString());
            DrawRow(x, ref y, w, "마지막 저장", FormatTime(s.lastSaveUnix));
        }

        private void DrawRow(float x, ref float y, float w, string label, string value)
        {
            GUI.Label(new Rect(x, y, w * 0.45f, 28f), label, rowStyle);
            GUIStyle valStyle = rowStyle;
            TextAnchor prev = valStyle.alignment;
            valStyle.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(x + w * 0.45f, y, w * 0.55f, 28f), value, valStyle);
            valStyle.alignment = prev;
            y += 32f;
        }

        private static string FormatTime(long unix)
        {
            if (unix <= 0) return "기록 없음";
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime.ToString("yyyy-MM-dd HH:mm");
            }
            catch
            {
                return "알 수 없음";
            }
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = MakeTex(new Color(0.07f, 0.08f, 0.12f, 0.98f));

            titleStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = new Color(1f, 0.7f, 0.3f);

            descStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 19, alignment = TextAnchor.UpperCenter, wordWrap = true };
            descStyle.normal.textColor = new Color(0.82f, 0.85f, 0.9f);

            cardTitleStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };

            rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 20 };
            rowStyle.normal.textColor = new Color(0.88f, 0.9f, 0.95f);

            btnLocalStyle = MakeButton(new Color(0.2f, 0.36f, 0.58f));
            btnCloudStyle = MakeButton(new Color(0.2f, 0.48f, 0.28f));
        }

        private static GUIStyle MakeButton(Color bg)
        {
            GUIStyle s = new GUIStyle(GUI.skin.button)
            { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            s.normal.background = MakeTex(bg);
            s.hover.background = MakeTex(bg * 1.15f);
            s.active.background = MakeTex(bg * 0.85f);
            s.normal.textColor = Color.white;
            s.hover.textColor = Color.white;
            s.active.textColor = Color.white;
            return s;
        }

        private static Texture2D MakeTex(Color col)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }
    }
}
