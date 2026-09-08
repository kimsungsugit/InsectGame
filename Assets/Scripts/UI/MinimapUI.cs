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
        /// <summary>미니맵 패널 한 변(가상 단위).</summary>
        public const float PanelSize = 220f;

        /// <summary>ContentTop에서 미니맵 상단까지 — 좌상단 HUD 닫힘 탭 아래.</summary>
        public const float TopOffset = 150f;

        /// <summary>
        /// 미니맵 아래에 붙는 좌측 HUD의 y. <see cref="TutorialQuestUI"/> 퀘스트 칩이 쓴다.
        ///
        /// 예전엔 그쪽이 <c>ContentTop + 380f</c>로 이 기하를 **베껴** 갖고 있었다.
        /// 380은 150+220+10을 손으로 더한 값이라, 미니맵 크기나 위치를 바꾸면
        /// 조용히 겹치거나 벌어진다. 값의 출처를 여기 하나로 둔다.
        /// </summary>
        public static float StackBelowY => UISafeLayout.ContentTop + TopOffset + PanelSize + UITheme.Space.S;

        /// <summary>미니맵 좌변 x. 아래에 붙는 HUD가 좌변을 맞추는 데 쓴다.</summary>
        public static float LeftX => UIScale.VirtualSafeLeft + 16f;

        /// <summary>
        /// 좌측 스택(미니맵·퀘스트 칩·목표 행)이 지금 가려져 있는가.
        /// <see cref="PlayerStatusHUD"/>의 펼침 패널이 이 영역을 통째로 덮으므로, 덮였으면
        /// 그리지 않는다 — 안 그러면 보이지도 않는 버튼이 클릭을 가로챈다.
        /// 미주입이면 false(가림 없음)라 패널이 없어도 정상 동작한다.
        /// </summary>
        public static bool LeftStackOccluded =>
            statusHud != null && statusHud.IsExpanded;

        private static PlayerStatusHUD statusHud;

        [SerializeField] private float worldRadius = 45f; // 미니맵이 커버하는 월드 반경(m)

        private Transform player;
        private InsectEntity[] insects;
        private float refreshTimer;

        // 메인퀘스트 목표 방향 쐐기 — 위치·이름은 전부 저쪽이 푼다(UI는 그리기만).
        private InsectGame.Story.StoryObjectiveTracker objectiveTracker;

        private GUIStyle labelStyle;
        private GUIStyle wedgeStyle;
        private Texture2D dotTex;
        private bool ready;

        /// <summary>메인퀘스트 목표 쐐기 소스. 미주입이면 쐐기만 안 그린다(미니맵 자체는 정상).</summary>
        public void AutoWire(InsectGame.Story.StoryObjectiveTracker tracker)
        {
            if (objectiveTracker == null) objectiveTracker = tracker;
        }

        /// <summary>
        /// 가림 판정 소스. static에 담는 것은 <see cref="LeftStackOccluded"/>를
        /// <see cref="TutorialQuestUI"/>도 봐야 하는데 그쪽에 AutoWire를 하나 더 늘리지 않기
        /// 위해서다 — 좌측 스택의 기하가 이미 이 클래스에 모여 있으므로 가림 여부도 같은 자리에 둔다.
        /// </summary>
        public void AutoWire(PlayerStatusHUD hud)
        {
            if (statusHud == null) statusHud = hud;
        }

        private void EnsureAssets()
        {
            if (ready) return;
            ready = true;
            labelStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            labelStyle.normal.textColor = new Color(0.7f, 0.85f, 1f);
            wedgeStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            wedgeStyle.normal.textColor = UITheme.Instance.accentAmber;
            // 소프트 디스크는 UIShapes가 소유한다 — 여기 있던 MakeDisc는 하드 엣지라
            // 확대 시 계단이 보였다(RegionMapUI 사본은 소프트였다). 공용판으로 통일.
            dotTex = UIShapes.Disc;
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
            // 좌상단 상태 패널이 펼쳐져 있으면 그 아래에 완전히 덮인다 — 그리지 않는다.
            if (LeftStackOccluded) return;

            EnsureAssets();
            UIScale.Begin();

            float size = PanelSize;
            float x = LeftX;
            float y = UISafeLayout.ContentTop + TopOffset; // 좌상단 HUD(ContentTop) 닫힘 탭 아래
            Rect rect = new Rect(x, y, size, size);
            float cx = x + size / 2f;
            float cy = y + size / 2f;
            float mapRadius = size / 2f - 12f;

            // 버튼은 없지만 **불투명 패널**이다 — 여기를 탭하면 뒤 월드로 레이가 나가
            // 캐릭터가 화면 좌상단 방향으로 걸어간다. 퀵액세스 바가 자기 배경까지 등록하는
            // 것과 같은 이유다(rules/ui-layout.md).
            FieldHudInput.RegisterBlockingRect(rect);

            // 배경 — 아래에 붙는 퀘스트 칩과 같은 서피스를 쓴다(각진 4줄 테두리였다).
            UISurface.HudCard(rect);

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 6f, size, 24f), "미니맵", labelStyle);

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

            // 메인퀘스트 목표 쐐기 — 곤충 점 위, 플레이어 아래에 그려 셋이 겹쳐도 읽힌다.
            DrawObjectiveWedge(cx, cy, mapRadius);

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

        /// <summary>
        /// 목표 방향 쐐기. 미니맵 반경(worldRadius) 안이면 실제 위치에, 밖이면 <b>테두리에 붙여</b>
        /// 방향만 알려 준다 — 밖에 있다고 안 그리면 "목표가 멀 때는 아무 안내도 없는" 상태가 된다.
        /// </summary>
        private void DrawObjectiveWedge(float cx, float cy, float mapRadius)
        {
            if (objectiveTracker == null || !objectiveTracker.HasObjective
                || !objectiveTracker.HasWorldTarget) return;

            Vector3 dir = objectiveTracker.DirectionToTarget;
            if (dir.sqrMagnitude < 0.0001f) return;

            // 월드 +Z가 미니맵 위쪽이므로 화면 벡터는 (x, -z)다.
            float dist = objectiveTracker.DistanceToTarget;
            float mapped = Mathf.Min(dist, worldRadius) / worldRadius * mapRadius;
            float wx = cx + dir.x * mapped;
            float wy = cy - dir.z * mapped;

            // 위를 향한 "▲"를 목표 쪽으로 돌린다. GUI 회전은 시계방향이 양수라 atan2(x, z)가 그대로 각도.
            float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, new Vector2(wx, wy));
            GUI.Label(new Rect(wx - 14f, wy - 14f, 28f, 28f), "▲", wedgeStyle);
            GUI.matrix = saved;
        }
    }
}
