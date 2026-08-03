using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 좌상단 소형 레이더 미니맵. 플레이어를 중심에 두고 주변 곤충 위치를 레어도색 점으로 표시한다.
    /// 전체 지도(RegionMapUI)를 열지 않아도 내 위치·곤충 위치를 항상 파악할 수 있게 한다.
    /// 자기 충족형 — 플레이어/곤충을 스스로 탐색(주기 캐싱)하므로 AutoWire 불필요.
    /// </summary>
    public class MinimapUI : MonoBehaviour
    {
        [SerializeField] private float worldRadius = 45f; // 미니맵이 커버하는 월드 반경(m)

        private Transform player;
        private InsectEntity[] insects;
        private float refreshTimer;

        private GUIStyle labelStyle;
        private Texture2D dotTex;
        private bool ready;

        private void EnsureAssets()
        {
            if (ready) return;
            ready = true;
            labelStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            labelStyle.normal.textColor = new Color(0.7f, 0.85f, 1f);
            dotTex = MakeDisc(32);
        }

        private void Update()
        {
            refreshTimer -= Time.deltaTime;
            if (refreshTimer > 0f) return;
            refreshTimer = 0.3f;

            if (player == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p == null) p = GameObject.Find("Player");
                if (p != null) player = p.transform;
            }
            insects = FindObjectsByType<InsectEntity>(FindObjectsSortMode.None);
        }

        private void OnGUI()
        {
            if (player == null) return;
            // 전체화면 모달(도감/배틀/포획선택 등)이 열려 있으면 숨김 — 필드 탐험 중에만.
            if (ModalUIRegistry.IsAnyOpen()) return;

            EnsureAssets();
            UIScale.Begin();

            float size = 220f;
            float x = UIScale.VirtualSafeLeft + 16f;
            float y = UISafeLayout.ContentTop + 150f; // 좌상단 HUD(ContentTop) 닫힘 탭 아래
            Rect rect = new Rect(x, y, size, size);
            float cx = x + size / 2f;
            float cy = y + size / 2f;
            float mapRadius = size / 2f - 12f;

            // 배경 + 테두리
            GUI.color = new Color(0.04f, 0.06f, 0.1f, 0.78f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.5f, 0.8f, 0.9f);
            GUI.DrawTexture(new Rect(x, y, size, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + size - 3f, size, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y, 3f, size), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + size - 3f, y, 3f, size), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 4f, size, 22f), "미니맵", labelStyle);

            // 곤충 점 (월드 +Z = 미니맵 위쪽)
            Vector3 pp = player.position;
            if (insects != null)
            {
                foreach (var e in insects)
                {
                    if (e == null || !e.gameObject.activeInHierarchy || e.Data == null) continue;
                    Vector3 d = e.transform.position - pp;
                    float dist = new Vector2(d.x, d.z).magnitude;
                    if (dist > worldRadius) continue;
                    float mx = cx + (d.x / worldRadius) * mapRadius;
                    float my = cy - (d.z / worldRadius) * mapRadius;
                    GUI.color = UITheme.Instance.GetInsectRarityColor(e.Data.rarity);
                    GUI.DrawTexture(new Rect(mx - 6f, my - 6f, 12f, 12f), dotTex);
                }
            }

            // 플레이어(중심) + 진행방향 점
            GUI.color = new Color(0.4f, 0.85f, 1f, 1f);
            GUI.DrawTexture(new Rect(cx - 7f, cy - 7f, 14f, 14f), dotTex);
            Vector3 f = player.forward;
            Vector2 fdir = new Vector2(f.x, -f.z);
            if (fdir.sqrMagnitude > 0.001f)
            {
                fdir = fdir.normalized * 17f;
                GUI.color = new Color(0.75f, 0.95f, 1f, 1f);
                GUI.DrawTexture(new Rect(cx + fdir.x - 4f, cy + fdir.y - 4f, 8f, 8f), dotTex);
            }

            GUI.color = Color.white;
            UIScale.End();
        }

        private static Texture2D MakeDisc(int size)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            float c = (size - 1) / 2f;
            for (int yy = 0; yy < size; yy++)
                for (int xx = 0; xx < size; xx++)
                {
                    float d = Mathf.Sqrt((xx - c) * (xx - c) + (yy - c) * (yy - c)) / c;
                    t.SetPixel(xx, yy, new Color(1f, 1f, 1f, d <= 1f ? 1f : 0f));
                }
            t.Apply();
            return t;
        }
    }
}
