using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>
    /// 전투 몸짓 — 정규화 타임라인(0..1)의 몸 자세. <b>순수 계산</b>이라 피해·턴을 정하지 않는다(<c>BattleMotionTests</c>·<c>BattleFlourishTests</c>).
    ///
    /// <b>계열</b>(<see cref="Family"/>)이 몸짓을 고른다 — 모델 빌더(<c>InsectEntity.BuildModel</c>)가 그 ID로 어떤 몸을 짓는지와 같은 순서로 가른다.
    /// 사마귀는 앞다리로 <b>두 번 벤다</b>(<see cref="Kind.Slash"/>), 딱정벌레·풍뎅이는 <b>머리를 숙이고 돌진</b>(<see cref="Kind.Charge"/>),
    /// 나비·나방·잠자리 같은 나는 종은 <b>날아올라 내리꽂고</b>(<see cref="Kind.Flight"/>), 벌·말벌은 <b>찌르기 돌진</b>(<see cref="Kind.Sting"/>),
    /// 지네·거미·개미귀신은 <b>덮친다</b>(<see cref="Kind.Pounce"/>), 그 밖(개미·귀뚜라미·메뚜기·대벌레…)은 예전의 가벼운 돌진(<see cref="Kind.Lunge"/>).
    /// 원거리 속성 기술(<see cref="Kind.Projectile"/>)도 계열마다 시전 자세가 다르다 — 나는 종은 떠오르며, 사마귀는 베어 날리며 쏜다.
    ///
    /// <b>타격 순간</b>은 <see cref="ImpactOf"/>가 계열마다 낸다(찌르기만 0.36, 나머지 0.40). 1대1은 아레나가 그 순간에 <c>onImpact</c>를 불러
    /// 피해 숫자·HP 감소가 따라온다 — 숫자가 따로 시각을 세지 않는다. 카메라 샷도 같은 값을 받는다(<c>BattleCameraDirector.Evaluate</c>).
    /// 레이드 팀원 공격은 길이가 정해진 볼리(0.46초 끝에 타격 — <c>RaidBattleUI</c>의 숫자 시각이 그걸 가정한다) 안에 이 곡선을 눌러 담는다.
    /// </summary>
    public static class BattleMotion
    {
        /// <summary>공용 타격 진행률 — 돌진·베기(두 번째)·내리꽂기·덮치기·원거리 발사.</summary>
        public const float ImpactProgress = 0.4f;
        /// <summary>벌의 찌르기 — 짧게 띄웠다가 곧게 쏘아 들어가서 조금 이르다.</summary>
        public const float StingImpactProgress = 0.36f;
        /// <summary>사마귀의 첫 번째 베기 — 다가가며 한 번(가벼운 움찔), 타격 순간(<see cref="ImpactProgress"/>)에 두 번째.</summary>
        public const float SlashFirstSwing = 0.29f;
        /// <summary>이 진행률부터 몸이 제자리다(모든 몸짓의 끝).</summary>
        public const float RestProgress = 0.84f;

        private const float Windup = 0.18f;
        private const float RecoverStart = 0.55f;

        public enum Kind { Charge, Slash, Flight, Projectile, Support, Sting, Pounce, Lunge }

        /// <summary>몸 모양 계열 — 모델 빌더의 분기와 같은 기준이다.</summary>
        public enum Family { Beetle, Mantis, Flier, Bee, Crawler, Other }

        public readonly struct Pose
        {
            public readonly float Travel;
            public readonly float Lift;
            public readonly float Pitch;
            public readonly float Roll;
            /// <summary>몸 돌리기(도) — 사마귀 베기의 좌우 휘두름, 승리 포즈의 제자리 돌기.</summary>
            public readonly float Yaw;
            public Pose(float travel, float lift, float pitch, float roll)
            { Travel = travel; Lift = lift; Pitch = pitch; Roll = roll; Yaw = 0f; }
            public Pose(float travel, float lift, float pitch, float roll, float yaw)
            { Travel = travel; Lift = lift; Pitch = pitch; Roll = roll; Yaw = yaw; }

            public Pose Scaled(float k) => new Pose(Travel * k, Lift * k, Pitch * k, Roll * k, Yaw * k);
        }

        private static readonly Pose Rest = new Pose(0f, 0f, 0f, 0f, 0f);

        /// <summary>
        /// 곤충 ID → 몸 계열. <c>InsectEntity.BuildModel</c>의 분기 순서를 그대로 따른다 — 구체적인 것을 먼저 본다
        /// (antlion을 ant보다, beetle을 bee보다, dragonfly·butterfly·firefly를 fly보다 먼저). 모르는 ID는 그 빌더처럼 딱정벌레다.
        /// </summary>
        public static Family FamilyOf(string speciesId)
        {
            string id = (speciesId ?? string.Empty).ToLowerInvariant();
            if (id.Length == 0) return Family.Beetle;
            if (id.Contains("antlion")) return Family.Crawler;
            if (id.Contains("aphid")) return Family.Other;
            if (id.Contains("butterfly") || id.Contains("alexandras")) return Family.Flier;
            if (id.Contains("moth") || id.Contains("luna") || id.Contains("atlas")) return Family.Flier;
            if (id.Contains("orchid") || id.Contains("ghost") || id.Contains("mantis")) return Family.Mantis;
            if (id.Contains("damselfly") || id.Contains("dragonfly")) return Family.Flier;
            if (id.Contains("firefly")) return Family.Flier;
            if (id.Contains("bee") && !id.Contains("beetle")) return Family.Bee;
            if (id.Contains("hornet") || id.Contains("wasp")) return Family.Bee;
            if (id.Contains("rhinoceros") || id.Contains("hercules") || id.Contains("stag")) return Family.Beetle;
            if (id.Contains("cicada")) return Family.Flier;
            if (id.Contains("cricket") || id.Contains("katydid")) return Family.Other;
            if (id.Contains("ant") && !id.Contains("phantom")) return Family.Other;
            if (id.Contains("strider")) return Family.Other;
            if (id.Contains("diving")) return Family.Beetle;
            if (id.Contains("scarab") || id.Contains("jewel") || id.Contains("diamond") || id.Contains("celestial")) return Family.Beetle;
            if (id.Contains("ladybug")) return Family.Beetle;
            if (id.Contains("grasshopper") || id.Contains("locust")) return Family.Other;
            if (id.Contains("spider")) return Family.Crawler;
            if (id.Contains("stick_insect") || id.Contains("leaf_insect")) return Family.Other;
            if (id.Contains("centipede")) return Family.Crawler;
            if (id.Contains("pill_bug")) return Family.Beetle;
            if (id.Contains("earwig")) return Family.Other;
            if (id.Contains("longhorn")) return Family.Beetle;
            if (id.Contains("caterpillar")) return Family.Other;
            if (id.Contains("mosquito") || id.Contains("fly")) return Family.Flier;
            return Family.Beetle;   // dung·click·beetle_*·이름 모를 종 — 빌더의 GenericBeetle
        }

        /// <summary>계열의 근접 몸짓.</summary>
        public static Kind MeleeKindOf(Family family)
        {
            switch (family)
            {
                case Family.Mantis: return Kind.Slash;
                case Family.Flier: return Kind.Flight;
                case Family.Bee: return Kind.Sting;
                case Family.Crawler: return Kind.Pounce;
                case Family.Other: return Kind.Lunge;
                default: return Kind.Charge;
            }
        }

        public static Kind Resolve(string speciesId, bool melee, bool support)
        {
            if (support) return Kind.Support;
            if (!melee) return Kind.Projectile;
            return MeleeKindOf(FamilyOf(speciesId));
        }

        /// <summary>그 몸짓의 타격 진행률(0..1). 1대1 <c>onImpact</c>·카메라 샷·전용기 시각표가 이 값을 함께 읽는다.</summary>
        public static float ImpactOf(Kind kind) => kind == Kind.Sting ? StingImpactProgress : ImpactProgress;

        /// <summary>타격 전에 한 번 더 휘두르는 몸짓인가(사마귀 베기 — <see cref="SlashFirstSwing"/>).</summary>
        public static bool HasPreStrike(Kind kind) => kind == Kind.Slash;

        /// <summary>예전 시그니처 — 원거리 시전 자세는 계열 없는 기본값(<see cref="Family.Other"/>).</summary>
        public static Pose Evaluate(Kind kind, float normalizedProgress) => Evaluate(kind, Family.Other, normalizedProgress);

        /// <summary>몸짓 한 프레임. 0 이하·<see cref="RestProgress"/> 이상은 제자리, 지원 몸짓은 늘 제자리.</summary>
        public static Pose Evaluate(Kind kind, Family family, float normalizedProgress)
        {
            float t = Mathf.Clamp01(normalizedProgress);
            if (t >= RestProgress || t <= 0f || kind == Kind.Support) return Rest;
            switch (kind)
            {
                case Kind.Slash: return Slash(t);
                case Kind.Flight: return Flight(t);
                case Kind.Sting: return Sting(t);
                case Kind.Pounce: return Pounce(t);
                case Kind.Lunge: return Lunge(t);
                case Kind.Projectile: return Cast(family, t);
                default: return Charge(t);
            }
        }

        // ───────────── 곡선 부품 ─────────────

        private static float Wind(float t) => Mathf.Sin(Mathf.Clamp01(t / Windup) * Mathf.PI * 0.5f);
        private static float Outbound(float t, float impact) => Mathf.SmoothStep(0f, 1f, (t - Windup) / (impact - Windup));
        private static float Recover(float t) => 1f - Mathf.SmoothStep(0f, 1f, (t - RecoverStart) / (RestProgress - RecoverStart));

        /// <summary>a~b에서 0 → 1 → 0으로 솟는 반쪽 사인. 구간 밖은 0.</summary>
        public static float Bump(float t, float a, float b)
        {
            if (t <= a || t >= b) return 0f;
            return Mathf.Sin((t - a) / (b - a) * Mathf.PI);
        }

        /// <summary>
        /// 딱정벌레 — 뒤로 물러서며 머리를 숙이고(16°), 숙인 채 들이받고, 받는 순간 뿔을 쳐올린다(−14°). 낮게 간다(뜨지 않는다).
        /// </summary>
        private static Pose Charge(float t)
        {
            const float I = ImpactProgress;
            float w = Wind(t);
            float o = Outbound(t, I);
            float r = Recover(t);
            float reach = t < Windup ? -0.08f * w
                : (Mathf.Lerp(-0.08f, 1f, o) + 0.06f * Bump(t, I, I + 0.14f)) * r;
            float headDown = t < Windup ? w : t < I ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (t - I) / 0.08f);
            float pitch = 16f * headDown - 14f * Bump(t, I + 0.02f, I + 0.18f);
            return new Pose(reach, 0f, pitch, 0f, 0f);
        }

        /// <summary>그 밖 — 예전 공용 돌진(가볍게 숙였다 든다)에 작은 깡충을 얹었다.</summary>
        private static Pose Lunge(float t)
        {
            float w = Wind(t);
            float o = Outbound(t, ImpactProgress);
            float r = Recover(t);
            float reach = t < Windup ? -0.065f * w : Mathf.Lerp(-0.065f, 1f, o) * r;
            float tension = t < Windup ? w : (1f - o) * r;
            return new Pose(reach, 0.12f * Bump(t, Windup, ImpactProgress), 7f * tension - 3f * o * r, 0f, 0f);
        }

        /// <summary>
        /// 사마귀 — 앞다리를 쳐들고(몸을 −16° 세운다) 다가가며 한 번(<see cref="SlashFirstSwing"/>), 붙어서 반대 대각선으로 한 번 더 벤다.
        /// 휘두를 때마다 몸이 기울고(±22°) 돌아간다(±14°). 근접 거리가 돌진보다 짧다(0.63).
        /// </summary>
        private static Pose Slash(float t)
        {
            const float I = ImpactProgress;
            float w = Wind(t);
            float o = Outbound(t, I);
            float r = Recover(t);
            float reach = t < Windup ? -0.065f * w : Mathf.Lerp(-0.065f, 1f, o) * r;
            float rear = t < Windup ? w : t < I ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (t - I) / (RecoverStart - I));
            float s1 = Bump(t, SlashFirstSwing - 0.06f, SlashFirstSwing + 0.06f);
            float s2 = Bump(t, I - 0.06f, I + 0.08f);
            return new Pose(reach * 0.63f, Mathf.Max(0f, reach) * 0.09f,
                -16f * rear + 13f * s1 + 15f * s2, 22f * s1 - 22f * s2, 14f * s1 - 14f * s2);
        }

        /// <summary>
        /// 나는 종 — 살짝 웅크렸다가 높이 솟아올라(머리를 들고, ~1m) 가속하며 내리꽂고(머리를 34° 숙인다), 맞힌 뒤 튀어 올라 날아 돌아온다.
        /// 날갯짓처럼 좌우로 흔들린다.
        /// </summary>
        private static Pose Flight(float t)
        {
            const float I = ImpactProgress;
            const float RiseEnd = 0.27f;
            float roll = -8f * Mathf.Sin(t / RestProgress * Mathf.PI * 2f);
            if (t < Windup)
            {
                float w = Wind(t);
                return new Pose(-0.06f * w, 0.12f * w, -6f * w, roll, 0f);
            }
            if (t < RiseEnd)
            {
                float u = Mathf.SmoothStep(0f, 1f, (t - Windup) / (RiseEnd - Windup));
                return new Pose(Mathf.Lerp(-0.06f, 0.28f, u), Mathf.Lerp(0.12f, 1f, u), Mathf.Lerp(-6f, -22f, u), roll, 0f);
            }
            if (t < I)
            {
                float u = (t - RiseEnd) / (I - RiseEnd);
                float e = u * u;   // 가속하며 내리꽂는다
                return new Pose(Mathf.Lerp(0.28f, 1f, e), Mathf.Lerp(1f, 0.06f, e),
                    Mathf.Lerp(-22f, 34f, Mathf.SmoothStep(0f, 1f, u)), roll, 0f);
            }
            float r = Recover(t);
            float lift = 0.06f * (1f - Mathf.SmoothStep(0f, 1f, (t - I) / 0.1f)) + 0.32f * Bump(t, I + 0.02f, RestProgress);
            float pitch = 34f * (1f - Mathf.SmoothStep(0f, 1f, (t - I) / 0.14f)) - 8f * Bump(t, I + 0.1f, RestProgress);
            return new Pose(r, lift, pitch, roll, 0f);
        }

        /// <summary>
        /// 벌·말벌 — 뒤로 물러나 떠서(배를 치켜든다) 윙윙 떨며 노리다가, 곧은 선으로 쏘아 들어가 찌르고(앞으로 18° 기운다) 튕겨 물러난다.
        /// </summary>
        private static Pose Sting(float t)
        {
            const float Cock = 0.16f;
            const float Hover = 0.27f;
            const float I = StingImpactProgress;
            const float Bounce = 0.46f;
            if (t < Cock)
            {
                float w = Mathf.Sin(Mathf.Clamp01(t / Cock) * Mathf.PI * 0.5f);
                return new Pose(-0.12f * w, 0.24f * w, -10f * w, 0f, 0f);
            }
            if (t < Hover)
            {
                float buzz = 0.025f * Bump(t, Cock, Hover) * Mathf.Sin((t - Cock) * 160f);
                return new Pose(-0.12f, 0.24f + buzz, -10f, 0f, 0f);
            }
            if (t < I)
            {
                float u = (t - Hover) / (I - Hover);
                float e = u * u;
                return new Pose(Mathf.Lerp(-0.12f, 1.08f, e), Mathf.Lerp(0.24f, 0.08f, e), Mathf.Lerp(-10f, 18f, u), 0f, 0f);
            }
            if (t < Bounce)
            {
                float u = (t - I) / (Bounce - I);
                return new Pose(Mathf.Lerp(1.08f, 0.72f, Mathf.SmoothStep(0f, 1f, u)), 0.08f + 0.14f * Mathf.Sin(u * Mathf.PI * 0.5f),
                    Mathf.Lerp(18f, 0f, u), 0f, 0f);
            }
            float k = Mathf.SmoothStep(0f, 1f, (t - Bounce) / (RestProgress - Bounce));
            return new Pose(Mathf.Lerp(0.72f, 0f, k), Mathf.Lerp(0.22f, 0f, k), 0f, 0f, 0f);
        }

        /// <summary>
        /// 지네·거미·개미귀신 — 낮게 웅크렸다가(머리를 숙인다) 포물선으로 뛰어올라(몸을 세웠다가) 상대 위로 덮치고, 누른 채 버둥거리다 물러난다.
        /// </summary>
        private static Pose Pounce(float t)
        {
            const float I = ImpactProgress;
            if (t < Windup)
            {
                float w = Wind(t);
                return new Pose(-0.05f * w, -0.03f * w, 10f * w, 0f, 0f);
            }
            if (t < I)
            {
                float u = (t - Windup) / (I - Windup);
                float pitch = u < 0.5f ? Mathf.Lerp(10f, -18f, u * 2f) : Mathf.Lerp(-18f, 16f, (u - 0.5f) * 2f);
                return new Pose(Mathf.Lerp(-0.05f, 1.02f, Mathf.SmoothStep(0f, 1f, u)),
                    0.8f * 4f * u * (1f - u) + Mathf.Lerp(-0.03f, 0f, u), pitch, 0f, 0f);
            }
            if (t < RecoverStart)
            {
                float pin = 16f * (1f - Mathf.SmoothStep(0f, 1f, (t - I) / 0.1f));
                float wrestle = 5f * Bump(t, I, RecoverStart) * Mathf.Sin((t - I) * 70f);
                return new Pose(1.02f, 0f, pin, wrestle, 0f);
            }
            float k = Mathf.SmoothStep(0f, 1f, (t - RecoverStart) / (RestProgress - RecoverStart));
            return new Pose(Mathf.Lerp(1.02f, 0f, k), 0.16f * Bump(t, RecoverStart, RestProgress), 0f, 0f, 0f);
        }

        /// <summary>
        /// 원거리 시전 — 몸은 제자리, 계열마다 쏘는 몸짓이 다르다. 나는 종은 떠오르며, 딱정벌레는 머리를 쳐올리며, 사마귀는 베어 날리며,
        /// 벌은 앞으로 찌르며, 지네·거미는 몸을 세우며 쏜다. 그 밖은 예전 기본 자세.
        /// </summary>
        private static Pose Cast(Family family, float t)
        {
            const float I = ImpactProgress;
            float w = Wind(t);
            float o = Outbound(t, I);
            float r = Recover(t);
            float tension = t < Windup ? w : (1f - o) * r;
            switch (family)
            {
                case Family.Flier:
                    return new Pose(0f, 0.32f * Bump(t, 0f, 0.62f), -8f * tension + 5f * o * r,
                        -6f * Mathf.Sin(t / RestProgress * Mathf.PI * 2f), 0f);
                case Family.Beetle:
                    return new Pose(0f, 0f, 12f * tension - 14f * Bump(t, Windup, I + 0.1f), 0f, 0f);
                case Family.Mantis:
                {
                    float s = Bump(t, Windup - 0.02f, I + 0.04f);
                    return new Pose(0f, 0f, -12f * tension + 10f * s, 24f * s, 12f * s);
                }
                case Family.Bee:
                {
                    float jab = Bump(t, Windup, I + 0.08f);
                    return new Pose(0.1f * jab, 0.12f * Bump(t, 0f, 0.62f), 14f * jab - 6f * tension, 0f, 0f);
                }
                case Family.Crawler:
                    return new Pose(0f, 0f, -20f * tension + 6f * o * r, 0f, 0f);
                default:
                    return new Pose(0f, 0f, -7f * tension + 4f * o * r, 0f, 0f);
            }
        }
    }
}
