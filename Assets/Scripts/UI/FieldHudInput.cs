using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 필드 HUD의 IMGUI 버튼(잡기 등) 영역을 공유 등록해 두 가지 IMGUI 한계를 보완한다.
    ///
    /// 1) 멀티터치: IMGUI GUI.Button은 합성 마우스(= primary 터치)로만 동작한다. 가상 조이스틱이
    ///    첫 손가락(finger 0)을 점유하면 두 번째 손가락의 버튼 탭이 GUI.Button에 전달되지 않는다.
    ///    <see cref="TryGetTapInVirtualRect"/>로 raw 터치를 직접 히트테스트해 우회한다.
    /// 2) 클릭-이동 오발: PlayerMovement는 Input.GetMouseButtonDown(0)을 별도로 폴링하므로,
    ///    IMGUI 버튼 위 탭이 버튼 액션과 함께 월드 클릭-이동으로도 발화된다(uGUI EventSystem은
    ///    IMGUI 버튼을 모름). <see cref="IsScreenPointOverHud"/>로 그 영역 탭을 이동에서 제외한다.
    ///
    /// 좌표계: HUD는 <see cref="UIScale"/> 가상 캔버스(순수 스케일 매트릭스)에서 그려지므로
    /// 화면 좌표 → 가상 좌표는 (x/Scale, (Screen.height - y)/Scale)로 변환한다.
    /// </summary>
    public static class FieldHudInput
    {
        private static readonly List<Rect> blockingVirtualRects = new List<Rect>();
        private static int lastRegisterFrame = -1;

        /// <summary>매 OnGUI 프레임 HUD가 자기 버튼의 가상 rect를 등록. 프레임이 바뀌면 자동으로 비운다.</summary>
        public static void RegisterBlockingRect(Rect virtualRect)
        {
            int f = Time.frameCount;
            if (f != lastRegisterFrame)
            {
                blockingVirtualRects.Clear();
                lastRegisterFrame = f;
            }
            blockingVirtualRects.Add(virtualRect);
        }

        /// <summary>화면 좌표(Input.mousePosition / Touch.position, bottom-up)가 등록된 HUD 버튼 위인지.
        /// 정적 버튼이라 1프레임 stale(직전 OnGUI 등록)까지 유효로 본다.</summary>
        public static bool IsScreenPointOverHud(Vector2 screenPos)
        {
            if (lastRegisterFrame < 0 || Time.frameCount - lastRegisterFrame > 1) return false;
            float s = UIScale.Scale;
            if (s <= 0f) return false;
            Vector2 v = new Vector2(screenPos.x / s, (Screen.height - screenPos.y) / s);
            for (int i = 0; i < blockingVirtualRects.Count; i++)
                if (blockingVirtualRects[i].Contains(v)) return true;
            return false;
        }

        /// <summary>이번 프레임 Began된 터치 중 주어진 가상 rect 안에 들어온 것이 있으면 true.
        /// 조이스틱이 점유한 손가락과 무관하게 모든 손가락을 검사(멀티터치 버튼 탭 감지).</summary>
        public static bool TryGetTapInVirtualRect(Rect virtualRect)
        {
            int count = Input.touchCount;
            if (count == 0) return false;
            float s = UIScale.Scale;
            if (s <= 0f) return false;
            for (int i = 0; i < count; i++)
            {
                Touch t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Began) continue;
                Vector2 v = new Vector2(t.position.x / s, (Screen.height - t.position.y) / s);
                if (virtualRect.Contains(v)) return true;
            }
            return false;
        }
    }
}
