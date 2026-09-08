using UnityEngine;

namespace InsectGame.UI
{
    public enum EaseType
    {
        Linear,
        SmoothStep,
        EaseOutBack,
        EaseOutBounce,
        EaseInOutQuad
    }

    [System.Serializable]
    public struct TweenHandle
    {
        public float from;
        public float to;
        public float duration;
        public float elapsed;
        public EaseType ease;
        public bool active;

        /// <summary>
        /// 마지막으로 시간을 전진시킨 프레임. <see cref="UITween.Evaluate"/>가 프레임당 한 번만
        /// 진행하도록 막는다 — 아래 Evaluate 주석 참조.
        /// </summary>
        public int lastFrame;
    }

    public static class UITween
    {
        public static TweenHandle Create(float from, float to, float duration, EaseType ease = EaseType.SmoothStep)
        {
            return new TweenHandle
            {
                from = from,
                to = to,
                duration = Mathf.Max(0.001f, duration),
                elapsed = 0f,
                ease = ease,
                active = true,
                // 생성 프레임에 바로 Evaluate가 불려도 시간이 앞서 가지 않게 현재 프레임을 찍어둔다.
                lastFrame = Time.frameCount
            };
        }

        /// <summary>
        /// 진행값을 읽는다. <b>프레임당 한 번만 시간이 흐른다.</b>
        ///
        /// 호출부가 `OnGUI`인데 OnGUI는 프레임당 여러 번 돈다 — Layout 1회 + Repaint 1회에
        /// 입력 이벤트마다 1회씩 더. 예전엔 호출할 때마다 `elapsed`를 더해서, 0.2초로 만든
        /// 페이드가 최소 2배 빨랐고 **마우스를 움직이면 더 빨라졌다**(MouseMove가 패스를 늘린다).
        /// 화면에선 사실상 페이드가 없는 것처럼 보였다.
        ///
        /// `Event.current.type == Repaint`로 거르는 IMGUI 관용구 대신 프레임 번호로 막는다 —
        /// 이 유틸이 OnGUI 밖에서도 쓰일 수 있어야 하고, Repaint 횟수는 보장되지 않는다.
        /// </summary>
        public static float Evaluate(ref TweenHandle h)
        {
            if (!h.active) return h.to;

            int frame = Time.frameCount;
            if (h.lastFrame != frame)
            {
                h.lastFrame = frame;
                h.elapsed += Time.unscaledDeltaTime;
            }

            // duration은 Create가 clamp하지만 TweenHandle은 public 필드를 가진 serializable struct라
            // 인스펙터·직접 생성으로 0이 들어올 수 있다. 0이면 0/0 = NaN이 되고, NaN은 `t >= 1f`가
            // 거짓이라 active가 영영 안 풀린 채 NaN을 계속 뱉는다.
            float t = Mathf.Clamp01(h.elapsed / Mathf.Max(0.001f, h.duration));
            float eased = Ease(t, h.ease);

            if (t >= 1f) h.active = false;

            return Mathf.LerpUnclamped(h.from, h.to, eased);
        }

        public static bool IsComplete(ref TweenHandle h)
        {
            return !h.active;
        }

        public static void Reset(ref TweenHandle h)
        {
            h.elapsed = 0f;
            h.active = true;
            h.lastFrame = Time.frameCount;
        }

        public static void Stop(ref TweenHandle h)
        {
            h.active = false;
        }

        /// <summary>
        /// 0~1 진행도를 이징 곡선에 통과시킨다. 순수 함수 — 시간 소스가 없어 테스트가 직접 부른다.
        /// **모든 EaseType은 0→0, 1→1을 지켜야 한다.** 안 지키면 트윈이 시작·끝에서 튄다.
        /// </summary>
        internal static float Ease(float t, EaseType ease)
        {
            switch (ease)
            {
                case EaseType.SmoothStep:
                    return t * t * (3f - 2f * t);

                case EaseType.EaseOutBack:
                    float c = 1.70158f;
                    float t1 = t - 1f;
                    return 1f + (c + 1f) * t1 * t1 * t1 + c * t1 * t1;

                case EaseType.EaseOutBounce:
                    if (t < 1f / 2.75f)
                        return 7.5625f * t * t;
                    if (t < 2f / 2.75f)
                    {
                        float tb = t - 1.5f / 2.75f;
                        return 7.5625f * tb * tb + 0.75f;
                    }
                    if (t < 2.5f / 2.75f)
                    {
                        float tb = t - 2.25f / 2.75f;
                        return 7.5625f * tb * tb + 0.9375f;
                    }
                    {
                        float tb = t - 2.625f / 2.75f;
                        return 7.5625f * tb * tb + 0.984375f;
                    }

                case EaseType.EaseInOutQuad:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

                default: // Linear
                    return t;
            }
        }
    }
}
