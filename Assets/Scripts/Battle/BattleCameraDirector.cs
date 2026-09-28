using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>
    /// 전투 연출 카메라 — 스킬 한 번의 정규화 타임라인(0..1, <see cref="BattleMotion"/>과 같은 축)을
    /// <b>샷</b>(위치·회전·가중치)으로 바꾼다. 순수 계산이라 씬 없이 테스트로 고정한다.
    ///
    /// 배틀 구도(<c>BattleFraming</c>이 잡은 원거리 정면)는 건드리지 않는다. 샷은 그 위에 가중치로
    /// 얹히고(<c>CameraFollower.SetBattleShot</c>), 연출이 끝나면 가중치가 0으로 내려가 구도가 그대로
    /// 돌아온다. 예전엔 전투 내내 카메라가 한 자리에 서 있어서 스킬·피격·반격이 모두 같은 원경이었다.
    /// </summary>
    public static class BattleCameraDirector
    {
        public enum Style
        {
            /// <summary>카메라는 구도에 고정 — 흔들림만.</summary>
            Off,
            /// <summary>
            /// 구도 유지, 타격 순간에만 대상 쪽으로 짧게 튀었다 돌아온다. 1v1 기본값은 아니지만(2026-09-28
            /// 비교 후 시네마틱으로 결정) 레이드 팀원 공격처럼 한 라운드에 다섯 번 몰아치는 짧은 타격에 쓴다 —
            /// 0.7초짜리 공격마다 카메라가 시전자와 보스를 오가면 어지럽다.
            /// </summary>
            Punch,
            /// <summary>시전자 클로즈업 → 대상 쪽으로 따라가 타격 클로즈업 → 원래 구도.</summary>
            Cinematic
        }

        /// <summary>현재 연출 스타일. QA 캡처가 비교용으로 바꾼다.</summary>
        public static Style Current { get; set; } = Style.Cinematic;

        public readonly struct Shot
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly float Weight;
            public Shot(Vector3 position, Quaternion rotation, float weight)
            { Position = position; Rotation = rotation; Weight = weight; }
            public static readonly Shot None = new Shot(Vector3.zero, Quaternion.identity, 0f);
        }

        // 타임라인 구간 — BattleMotion의 준비(0~0.18)·돌진(~0.40 타격)·복귀(~0.84)에 맞춘다.
        private const float WindupEnd = 0.16f;
        private const float HoldEnd = 0.58f;
        private const float ReleaseEnd = 0.82f;

        /// <summary>시전자 클로즈업 — 기본 구도에서 시전자 쪽으로 다가가는 비율.</summary>
        public const float AttackerDolly = 0.30f;
        /// <summary>타격 클로즈업 — 기본 구도에서 대상 쪽으로 다가가는 비율(세기 1이면 더 붙는다).</summary>
        public const float TargetDolly = 0.32f;
        public const float TargetDollyHeavyBonus = 0.10f;
        /// <summary>펀치형 — 타격 순간 튀어 들어가는 비율.</summary>
        public const float PunchDolly = 0.11f;

        /// <param name="style">연출 스타일.</param>
        /// <param name="progress">스킬 타임라인 0..1(타격은 <see cref="BattleMotion.ImpactProgress"/>).</param>
        /// <param name="durationSeconds">타임라인 전체 길이(초) — 타격 후 반동의 진동수를 초 단위로 맞춘다.</param>
        /// <param name="basePos">배틀 구도 카메라 위치.</param>
        /// <param name="baseRot">배틀 구도 카메라 회전.</param>
        /// <param name="attacker">시전자 중심(월드).</param>
        /// <param name="target">대상 중심(월드). 자기 강화면 시전자와 같다.</param>
        /// <param name="impactWeight">타격 세기 0..1(치명타·큰 피해·마무리일수록 1).</param>
        /// <param name="support">강화·회복·약화 — 타격 클로즈업 없이 시전자만 살짝 본다.</param>
        public static Shot Evaluate(Style style, float progress, float durationSeconds,
            Vector3 basePos, Quaternion baseRot, Vector3 attacker, Vector3 target,
            float impactWeight, bool support)
        {
            float p = Mathf.Clamp01(progress);
            float weight = Mathf.Clamp01(impactWeight);
            if (style == Style.Off || p >= ReleaseEnd) return Shot.None;

            float sinceImpact = (p - BattleMotion.ImpactProgress) * Mathf.Max(0.1f, durationSeconds);
            Vector3 hitDir = target - attacker;
            hitDir.y = 0f;
            hitDir = hitDir.sqrMagnitude > 0.0001f ? hitDir.normalized : baseRot * Vector3.right;

            if (style == Style.Punch)
            {
                if (support || sinceImpact < 0f) return Shot.None;
                // 순간 튀어 들어갔다가 지수로 빠진다 — 0.35초 안에 거의 원위치.
                float w = Mathf.Exp(-sinceImpact * 9f);
                if (w < 0.01f) return Shot.None;
                Vector3 pos = Vector3.Lerp(basePos, target, PunchDolly + 0.05f * weight)
                    + Kick(hitDir, sinceImpact, 0.10f + 0.12f * weight);
                Quaternion rot = Quaternion.Slerp(baseRot, LookAt(pos, target, baseRot), 0.25f);
                return new Shot(pos, rot, w);
            }

            // ── 시네마틱 ──
            Vector3 attackerPos = Vector3.Lerp(basePos, attacker, AttackerDolly);
            // 시선은 시전자 쪽에 둔다 — 대상 쪽으로 28% 당겼을 때 세로 화면(가로 시야가 좁다)에서
            // 시전자가 화면 끝에 반쯤 잘렸다(720×1280 QA 캡처).
            Quaternion attackerRot = LookAt(attackerPos, Vector3.Lerp(attacker, target, 0.12f), baseRot);

            if (support)
            {
                // 강화는 몸을 부풀리는 순간이지 부딪치는 순간이 아니다 — 시전자만 가볍게 담는다.
                float sw = Bell(p, 0f, 0.30f, 0.72f) * 0.7f;
                if (sw <= 0f) return Shot.None;
                Vector3 selfPos = Vector3.Lerp(basePos, attacker, AttackerDolly * 0.8f);
                return new Shot(selfPos, LookAt(selfPos, attacker, baseRot), sw);
            }

            float dolly = TargetDolly + TargetDollyHeavyBonus * weight;
            Vector3 targetPos = Vector3.Lerp(basePos, target, dolly);
            Quaternion targetRot = LookAt(targetPos, Vector3.Lerp(target, attacker, 0.12f), baseRot);

            if (p < WindupEnd)
            {
                float w = Mathf.SmoothStep(0f, 1f, p / WindupEnd);
                return new Shot(attackerPos, attackerRot, w);
            }
            if (p < BattleMotion.ImpactProgress)
            {
                float k = Mathf.SmoothStep(0f, 1f, (p - WindupEnd) / (BattleMotion.ImpactProgress - WindupEnd));
                return new Shot(Vector3.Lerp(attackerPos, targetPos, k),
                    Quaternion.Slerp(attackerRot, targetRot, k), 1f);
            }
            Vector3 kicked = targetPos + Kick(hitDir, sinceImpact, 0.14f + 0.16f * weight);
            if (p < HoldEnd) return new Shot(kicked, targetRot, 1f);
            float release = 1f - Mathf.SmoothStep(0f, 1f, (p - HoldEnd) / (ReleaseEnd - HoldEnd));
            return new Shot(kicked, targetRot, release);
        }

        /// <summary>
        /// 레이드 합체공격 샷(<see cref="RaidUniteTimeline"/> 초 단위). 살짝 올라 팀 머리 위로 보스를 보고 →
        /// 팀원이 차례로 때리는 동안 보스 쪽으로 서서히 들어가다 → 합동 일격 직전에 몰아쳐 붙고 →
        /// 반동 뒤 원래 구도로 돌아온다. 레이드 기본 구도는 팀 뒤에서 보스를 정면으로 보므로 "보스 쪽"이 곧
        /// 카메라의 앞이다 — 붙는 비율(최대 0.38)은 카메라가 팀 줄(7.5m 앞)을 넘지 않게 잡았다.
        /// </summary>
        public static Shot EvaluateUnite(float seconds, Vector3 basePos, Quaternion baseRot,
            Vector3 teamCenter, Vector3 bossCenter)
        {
            if (seconds <= 0f || seconds >= RaidUniteTimeline.Total) return Shot.None;
            Vector3 axis = bossCenter - basePos;
            axis.y = 0f;
            axis = axis.sqrMagnitude > 0.0001f ? axis.normalized : baseRot * Vector3.forward;

            // 뒤로 물러나지 않는다 — 레이드 기본 구도가 이미 팀과 보스를 한 화면에 담는 원경이라,
            // 더 물러나면 팀원이 점처럼 작아졌다(QA 캡처). 살짝 올라 팀 머리 위로 보스를 본다.
            Vector3 wide = basePos + Vector3.up * 0.25f;
            Vector3 approach = Vector3.Lerp(basePos, bossCenter, 0.2f) + Vector3.up * 0.3f;
            Vector3 rush = Vector3.Lerp(basePos, bossCenter, 0.38f);
            Quaternion wideRot = LookAt(wide, Vector3.Lerp(teamCenter, bossCenter, 0.55f), baseRot);
            Quaternion bossRot = LookAt(rush, bossCenter, baseRot);

            float gather = RaidUniteTimeline.FinalStrike - 0.15f;
            if (seconds < RaidUniteTimeline.MemberStart)
                return new Shot(wide, wideRot, Mathf.SmoothStep(0f, 1f, seconds / RaidUniteTimeline.MemberStart));
            if (seconds < gather)
            {
                float k = Mathf.SmoothStep(0f, 1f, (seconds - RaidUniteTimeline.MemberStart) / (gather - RaidUniteTimeline.MemberStart));
                return new Shot(Vector3.Lerp(wide, approach, k), Quaternion.Slerp(wideRot, LookAt(approach, bossCenter, baseRot), k), 1f);
            }
            if (seconds < RaidUniteTimeline.FinalStrike)
            {
                float k = Mathf.SmoothStep(0f, 1f, (seconds - gather) / 0.15f);
                return new Shot(Vector3.Lerp(approach, rush, k), Quaternion.Slerp(LookAt(approach, bossCenter, baseRot), bossRot, k), 1f);
            }
            float since = seconds - RaidUniteTimeline.FinalStrike;
            Vector3 kicked = rush + Kick(axis, since, 0.35f);
            float releaseStart = 0.35f;
            float releaseSpan = RaidUniteTimeline.Total - RaidUniteTimeline.FinalStrike - releaseStart;
            float weight = since < releaseStart ? 1f
                : 1f - Mathf.SmoothStep(0f, 1f, (since - releaseStart) / Mathf.Max(0.01f, releaseSpan));
            return new Shot(kicked, bossRot, weight);
        }

        /// <summary>
        /// 타격 반동 — 맞은 방향으로 밀렸다가 감쇠 진동하며 돌아온다(무작위 흔들림과 달리 방향이 있다).
        /// </summary>
        public static Vector3 Kick(Vector3 direction, float secondsSinceImpact, float amplitude)
        {
            if (secondsSinceImpact < 0f || amplitude <= 0f) return Vector3.zero;
            float t = secondsSinceImpact;
            float wave = Mathf.Sin(t * 38f) * Mathf.Exp(-t * 9f);
            return direction * (amplitude * wave);
        }

        private static float Bell(float p, float start, float peak, float end)
        {
            if (p <= start || p >= end) return 0f;
            return p < peak
                ? Mathf.SmoothStep(0f, 1f, (p - start) / (peak - start))
                : 1f - Mathf.SmoothStep(0f, 1f, (p - peak) / (end - peak));
        }

        private static Quaternion LookAt(Vector3 from, Vector3 to, Quaternion fallback)
        {
            Vector3 d = to - from;
            return d.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(d) : fallback;
        }
    }
}
