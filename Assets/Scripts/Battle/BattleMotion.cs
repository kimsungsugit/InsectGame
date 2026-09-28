using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>Pure, normalized choreography. It never resolves damage or advances turns.</summary>
    public static class BattleMotion
    {
        public const float ImpactProgress = 0.4f;
        public enum Kind { Charge, Slash, Flight, Projectile, Support }
        public readonly struct Pose
        {
            public readonly float Travel;
            public readonly float Lift;
            public readonly float Pitch;
            public readonly float Roll;
            public Pose(float travel, float lift, float pitch, float roll)
            { Travel = travel; Lift = lift; Pitch = pitch; Roll = roll; }
        }

        public static Kind Resolve(string speciesId, bool melee, bool support)
        {
            if (support) return Kind.Support;
            if (!melee) return Kind.Projectile;
            string id = (speciesId ?? string.Empty).ToLowerInvariant();
            if (id.Contains("mantis")) return Kind.Slash;
            bool bee = id == "bee" || id.StartsWith("bee_") || id.EndsWith("_bee");
            if (id.Contains("dragonfly") || id.Contains("butterfly") || id.Contains("moth") || bee) return Kind.Flight;
            return Kind.Charge;
        }

        public static Pose Evaluate(Kind kind, float normalizedProgress)
        {
            float t = Mathf.Clamp01(normalizedProgress);
            if (t >= 0.84f || t <= 0f || kind == Kind.Support) return new Pose(0f, 0f, 0f, 0f);
            float windup = Mathf.Sin(Mathf.Clamp01(t / 0.18f) * Mathf.PI * 0.5f);
            float outbound = Mathf.SmoothStep(0f, 1f, (t - 0.18f) / 0.22f);
            float recover = 1f - Mathf.SmoothStep(0f, 1f, (t - 0.55f) / 0.29f);
            float reach = t < 0.18f ? -0.065f * windup : Mathf.Lerp(-0.065f, 1f, outbound) * recover;
            float tension = t < 0.18f ? windup : (1f - outbound) * recover;
            switch (kind)
            {
                case Kind.Slash:
                    return new Pose(reach * 0.63f, Mathf.Max(0f, reach) * 0.09f,
                        -11f * tension + 7f * outbound * recover, 14f * tension - 10f * outbound * recover);
                case Kind.Flight:
                    return new Pose(reach, Mathf.Sin(t / 0.84f * Mathf.PI) * 0.55f,
                        -9f * tension + 6f * outbound * recover, -8f * Mathf.Sin(t / 0.84f * Mathf.PI * 2f));
                case Kind.Projectile:
                    return new Pose(0f, 0f, -7f * tension + 4f * outbound * recover, 0f);
                default:
                    return new Pose(reach, 0f, 7f * tension - 3f * outbound * recover, 0f);
            }
        }
    }
}
