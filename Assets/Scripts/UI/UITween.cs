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
                active = true
            };
        }

        public static float Evaluate(ref TweenHandle h)
        {
            if (!h.active) return h.to;

            h.elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(h.elapsed / h.duration);
            float eased = ApplyEase(t, h.ease);

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
        }

        public static void Stop(ref TweenHandle h)
        {
            h.active = false;
        }

        private static float ApplyEase(float t, EaseType ease)
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
