using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// uGUI 패널용 세이프 에어리어 적용기. RectTransform 앵커를 Screen.safeArea에 맞춰 자동 인셋한다.
    /// IMGUI가 아닌 uGUI 요소(통화 표시 TMP 캔버스 등)에 에디터에서 부착해 노치/제스처바를 피한다.
    ///
    /// 사용: 안전 영역 안에 두고 싶은 uGUI 패널의 RectTransform에 이 컴포넌트를 추가.
    /// 화면 회전/해상도 변경 시 자동 갱신.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaPanel : MonoBehaviour
    {
        private RectTransform rt;
        private Rect lastSafe = new Rect(0, 0, 0, 0);
        private Vector2Int lastScreen;

        private void Awake()
        {
            rt = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != lastSafe
                || Screen.width != lastScreen.x || Screen.height != lastScreen.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (rt == null) rt = GetComponent<RectTransform>();
            if (rt == null || Screen.width == 0 || Screen.height == 0) return;

            lastSafe = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);

            Rect sa = Screen.safeArea;
            Vector2 anchorMin = sa.position;
            Vector2 anchorMax = sa.position + sa.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
