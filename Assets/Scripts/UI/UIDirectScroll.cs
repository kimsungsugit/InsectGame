using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// IMGUI 목록을 스크롤바 화살표 대신 목록 위 휠과 포인터 드래그로 조작한다.
    /// 각 ScrollView마다 인스턴스 하나를 보유하고 BeginScrollView 전에 Handle을 호출한다.
    /// </summary>
    public sealed class UIDirectScroll
    {
        private const float DragThreshold = 8f;
        private const float DefaultWheelStep = 36f;

        private bool pointerDown;
        private bool dragging;
        private bool tapCancelled;
        private Vector2 pointerStart;
        private Vector2 lastPointer;
        private int touchFingerId = -1;
        private int lastTouchFrame = -1;
        private int suppressPointerFrame = -1;
        private Vector2 touchStart;
        private Vector2 lastTouch;

        public bool IsDragging => dragging;

        public void Reset()
        {
            pointerDown = false;
            dragging = false;
            tapCancelled = false;
            pointerStart = Vector2.zero;
            lastPointer = Vector2.zero;
            touchFingerId = -1;
            lastTouchFrame = -1;
            suppressPointerFrame = -1;
            touchStart = Vector2.zero;
            lastTouch = Vector2.zero;
        }

        /// <summary>
        /// 휠·드래그를 읽어 <paramref name="scrollPosition"/>을 갱신한다. 입력을 소비했으면 true.
        /// </summary>
        /// <param name="interactive">
        /// false면 위치 clamp와 제스처 초기화만 하고 입력에는 손대지 않는다.
        /// <b>위에 모달이 겹치는 배경 목록은 반드시 false를 넘긴다.</b> IMGUI는 먼저 그린 쪽이
        /// Handle도 먼저 부르므로, 그대로 두면 배경 목록이 모달 위에서 굴린 휠·드래그를
        /// <c>Event.Use()</c>로 가로채 모달이 전혀 스크롤되지 않는다. 터치 경로는 Event가 아니라
        /// <c>Input.GetTouch</c>를 직접 읽어 소비 개념이 없어서, 두 인스턴스가 같은 손가락을
        /// 동시에 붙잡고 배경과 모달이 함께 움직인다.
        /// </param>
        public bool Handle(
            ref Vector2 scrollPosition,
            Rect viewport,
            float contentHeight,
            float wheelStep = DefaultWheelStep,
            bool interactive = true)
        {
            // clamp는 비활성 구간에서도 해야 한다 — 모달이 열려 있는 사이 배경 목록의 내용이
            // 짧아지면(치료·판매 등) 범위 밖 스크롤이 그대로 남는다.
            scrollPosition.x = 0f;
            scrollPosition.y = ClampScrollY(scrollPosition.y, viewport.height, contentHeight);

            float maxScroll = Mathf.Max(0f, contentHeight - viewport.height);
            if (!interactive || maxScroll <= 0f)
            {
                Reset();
                return false;
            }

            Event current = Event.current;
            if (current == null)
                return false;

            bool hadTouchInput = Input.touchCount > 0 || touchFingerId >= 0;
            bool touchHandled = HandleTouch(
                ref scrollPosition,
                viewport,
                contentHeight);
            if (suppressPointerFrame == Time.frameCount
                && (current.type == EventType.MouseDrag
                    || current.type == EventType.MouseUp))
            {
                GUIUtility.hotControl = 0;
                current.Use();
                return true;
            }
            if (hadTouchInput || touchHandled || Input.touchCount > 0 || touchFingerId >= 0)
                return touchHandled;

            Vector2 pointer = UIScale.VirtualMousePosition;
            if (current.type == EventType.ScrollWheel && viewport.Contains(pointer))
            {
                scrollPosition.y = ClampScrollY(
                    scrollPosition.y + current.delta.y * Mathf.Max(1f, wheelStep),
                    viewport.height,
                    contentHeight);
                current.Use();
                return true;
            }

            if (current.type == EventType.MouseDown
                && current.button == 0
                && viewport.Contains(pointer))
            {
                pointerDown = true;
                dragging = false;
                tapCancelled = false;
                pointerStart = pointer;
                lastPointer = pointer;
                return false;
            }

            if (current.type == EventType.MouseDrag && pointerDown)
            {
                Vector2 delta = pointer - lastPointer;
                lastPointer = pointer;
                Vector2 displacement = pointer - pointerStart;
                if (!tapCancelled && IsGestureBeyondThreshold(displacement))
                {
                    tapCancelled = true;
                    GUIUtility.hotControl = 0;
                }
                if (!dragging && IsVerticalDrag(displacement))
                {
                    dragging = true;
                    // MouseDown을 받은 행/카드 버튼의 클릭 상태를 취소한다.
                    GUIUtility.hotControl = 0;
                }

                if (dragging)
                {
                    scrollPosition.y = ApplyDragDelta(
                        scrollPosition.y,
                        delta.y,
                        viewport.height,
                        contentHeight);
                    current.Use();
                    return true;
                }

                if (tapCancelled)
                {
                    current.Use();
                    return true;
                }
            }

            if (current.type == EventType.MouseUp && current.button == 0 && pointerDown)
            {
                bool consumed = tapCancelled;
                Reset();
                if (consumed)
                {
                    current.Use();
                    return true;
                }
            }
            else if (current.type == EventType.Repaint
                && pointerDown
                && !Input.GetMouseButton(0))
            {
                Reset();
            }

            return false;
        }

        private bool HandleTouch(
            ref Vector2 scrollPosition,
            Rect viewport,
            float contentHeight)
        {
            if (lastTouchFrame == Time.frameCount)
                return suppressPointerFrame == Time.frameCount;
            lastTouchFrame = Time.frameCount;

            if (touchFingerId < 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.phase != TouchPhase.Began)
                        continue;

                    Vector2 point = ToVirtualTouchPosition(touch.position);
                    if (!viewport.Contains(point))
                        continue;

                    touchFingerId = touch.fingerId;
                    tapCancelled = false;
                    dragging = false;
                    touchStart = point;
                    lastTouch = point;
                    return false;
                }
                return false;
            }

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.fingerId != touchFingerId)
                    continue;

                Vector2 point = ToVirtualTouchPosition(touch.position);
                if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    Vector2 delta = point - lastTouch;
                    lastTouch = point;
                    Vector2 displacement = point - touchStart;
                    if (!tapCancelled && IsGestureBeyondThreshold(displacement))
                    {
                        tapCancelled = true;
                        GUIUtility.hotControl = 0;
                    }
                    if (!dragging && IsVerticalDrag(displacement))
                    {
                        dragging = true;
                        GUIUtility.hotControl = 0;
                    }

                    if (!tapCancelled)
                        return false;

                    if (dragging)
                    {
                        scrollPosition.y = ApplyDragDelta(
                            scrollPosition.y,
                            delta.y,
                            viewport.height,
                            contentHeight);
                    }
                    suppressPointerFrame = Time.frameCount;
                    return true;
                }

                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    bool consumed = tapCancelled;
                    touchFingerId = -1;
                    dragging = false;
                    tapCancelled = false;
                    if (consumed)
                    {
                        suppressPointerFrame = Time.frameCount;
                        GUIUtility.hotControl = 0;
                    }
                    return consumed;
                }

                return false;
            }

            bool lostTouchWasGesture = tapCancelled;
            touchFingerId = -1;
            dragging = false;
            tapCancelled = false;
            if (lostTouchWasGesture)
            {
                suppressPointerFrame = Time.frameCount;
                GUIUtility.hotControl = 0;
            }
            return lostTouchWasGesture;
        }

        private static Vector2 ToVirtualTouchPosition(Vector2 screenPosition)
        {
            float scale = Mathf.Max(0.3f, UIScale.Scale);
            return new Vector2(
                screenPosition.x / scale,
                (Screen.height - screenPosition.y) / scale);
        }

        internal static bool IsVerticalDrag(Vector2 displacement)
        {
            float vertical = Mathf.Abs(displacement.y);
            float horizontal = Mathf.Abs(displacement.x);
            return vertical >= DragThreshold && vertical > horizontal;
        }

        internal static bool IsGestureBeyondThreshold(Vector2 displacement)
        {
            return displacement.sqrMagnitude >= DragThreshold * DragThreshold;
        }

        internal static float ApplyDragDelta(
            float currentY,
            float pointerDeltaY,
            float viewportHeight,
            float contentHeight)
        {
            return ClampScrollY(currentY - pointerDeltaY, viewportHeight, contentHeight);
        }

        internal static float ClampScrollY(float value, float viewportHeight, float contentHeight)
        {
            float maxScroll = Mathf.Max(0f, contentHeight - Mathf.Max(0f, viewportHeight));
            return Mathf.Clamp(value, 0f, maxScroll);
        }
    }
}
