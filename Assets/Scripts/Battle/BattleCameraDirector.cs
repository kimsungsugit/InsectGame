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
            return Evaluate(style, progress, durationSeconds, basePos, baseRot, attacker, target, impactWeight, support,
                BattleMotion.ImpactProgress);
        }

        /// <param name="impactProgress">이 몸짓의 타격 진행률(<see cref="BattleMotion.ImpactOf"/>) — 계열마다 다르다(찌르기 0.36).</param>
        public static Shot Evaluate(Style style, float progress, float durationSeconds,
            Vector3 basePos, Quaternion baseRot, Vector3 attacker, Vector3 target,
            float impactWeight, bool support, float impactProgress)
        {
            float p = Mathf.Clamp01(progress);
            float weight = Mathf.Clamp01(impactWeight);
            if (style == Style.Off || p >= ReleaseEnd) return Shot.None;
            float impactAt = Mathf.Clamp(impactProgress, WindupEnd + 0.02f, HoldEnd - 0.02f);

            float sinceImpact = (p - impactAt) * Mathf.Max(0.1f, durationSeconds);
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
            if (p < impactAt)
            {
                float k = Mathf.SmoothStep(0f, 1f, (p - WindupEnd) / (impactAt - WindupEnd));
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

        // ── 등장·변신 연출 샷 — 시각표는 BattleStaging ──
        // 셋 다 Style.Off면 걸지 않는다(구도 고정 비교용). 마지막엔 가중치가 0으로 내려가 배틀 구도가 그대로 돌아온다.

        /// <summary>
        /// 상대 교체 등장(1대1 팀 대결) — <b>앞 구도</b>(<paramref name="fromPos"/>, 교체 전 배틀 구도)에서 출발해 착지점 클로즈업으로
        /// 옮겨 가고, 착지에 위아래로 튀고, <b>새 구도</b>(<paramref name="basePos"/>, 새 곤충 크기로 다시 잡은 배틀 구도)로 풀린다.
        /// 첫 프레임이 앞 구도라서 새 곤충 크기에 맞춰 구도가 바뀌어도 화면이 튀지 않는다.
        /// </summary>
        /// <param name="progress">교체 단계 진행률 0~1.</param>
        /// <param name="durationSeconds">교체 단계 길이(초) — 착지 반동의 진동수를 초 단위로 맞춘다.</param>
        /// <param name="landing">새 곤충이 서는 자리의 몸 중심.</param>
        /// <param name="opponent">내 곤충 몸 중심 — 시선을 그쪽으로 조금 당겨 두 곤충의 관계를 남긴다.</param>
        public static Shot EvaluateEntrance(float progress, float durationSeconds, Vector3 fromPos, Quaternion fromRot,
            Vector3 basePos, Quaternion baseRot, Vector3 landing, Vector3 opponent)
        {
            if (Current == Style.Off) return Shot.None;
            float p = Mathf.Clamp01(progress);
            if (p >= BattleStaging.EntranceCamReleaseEnd) return Shot.None;

            Vector3 close = Vector3.Lerp(basePos, landing, BattleStaging.EntranceDolly) + Vector3.up * 0.1f;
            Quaternion closeRot = LookAt(close, Vector3.Lerp(landing, opponent, 0.2f), baseRot);
            float sinceLand = (p - BattleStaging.EntranceLand) * Mathf.Max(0.1f, durationSeconds);
            Vector3 kicked = close + Kick(Vector3.down, sinceLand, 0.1f);

            if (p < BattleStaging.EntranceCamIn)
            {
                float k = Mathf.SmoothStep(0f, 1f, p / BattleStaging.EntranceCamIn);
                return new Shot(Vector3.Lerp(fromPos, close, k), Quaternion.Slerp(fromRot, closeRot, k), 1f);
            }
            if (p < BattleStaging.EntranceCamHoldEnd) return new Shot(kicked, closeRot, 1f);
            float release = 1f - Mathf.SmoothStep(0f, 1f, (p - BattleStaging.EntranceCamHoldEnd)
                / (BattleStaging.EntranceCamReleaseEnd - BattleStaging.EntranceCamHoldEnd));
            return new Shot(kicked, closeRot, release);
        }

        /// <summary>
        /// 그림자 변신(레이드) — 연기가 덮는 동안 보스 쪽으로 천천히 붙고, 새 모습이 울부짖을 때 뒤로 튀었다가, 원래 구도로 풀린다.
        /// 레이드 기본 구도(팀 뒤 원경)에서 <see cref="BattleStaging.TransformDolly"/>만 붙는다 — 카메라가 팀 줄을 넘지 않는다.
        /// </summary>
        public static Shot EvaluateBossTransform(float progress, float durationSeconds, Vector3 basePos, Quaternion baseRot,
            Vector3 bossCenter)
        {
            if (Current == Style.Off) return Shot.None;
            float p = Mathf.Clamp01(progress);
            if (p >= BattleStaging.TransformCamReleaseEnd) return Shot.None;

            float push = Mathf.Lerp(BattleStaging.TransformDolly * 0.5f, BattleStaging.TransformDolly,
                Mathf.SmoothStep(0f, 1f, p / BattleStaging.TransformSwap));
            Vector3 pos = Vector3.Lerp(basePos, bossCenter, push);
            Quaternion rot = Quaternion.Slerp(baseRot, LookAt(pos, bossCenter, baseRot), 0.55f);
            Vector3 axis = bossCenter - basePos;
            axis.y = 0f;
            axis = axis.sqrMagnitude > 0.0001f ? axis.normalized : baseRot * Vector3.forward;
            // 새 모습이 울부짖는 순간 뒤로 밀린다(보스에게서 멀어지는 쪽).
            pos += Kick(-axis, (p - BattleStaging.TransformRoar) * Mathf.Max(0.1f, durationSeconds), 0.18f);

            float weight;
            if (p < BattleStaging.TransformCamIn) weight = Mathf.SmoothStep(0f, 1f, p / BattleStaging.TransformCamIn);
            else if (p < BattleStaging.TransformCamHoldEnd) weight = 1f;
            else weight = 1f - Mathf.SmoothStep(0f, 1f, (p - BattleStaging.TransformCamHoldEnd)
                / (BattleStaging.TransformCamReleaseEnd - BattleStaging.TransformCamHoldEnd));
            return new Shot(pos, rot, weight);
        }

        /// <summary>
        /// 수문장 등장(레이드 인트로, <b>초</b>) — <paramref name="startPos"/>(낮고 먼 곳)에서 <paramref name="endPos"/>(수문장이 화면 위쪽
        /// 창을 채우는 곳)로 감속하며 다가가고, 포효에 위로 튀고, 천천히 조금 더 붙었다가, 원래 구도로 풀린다. 회전은 내내
        /// <paramref name="rotation"/>(올려다보는 각) 하나다 — 그래서 다가가는 동안 수문장이 화면 위쪽 창에서 벗어나지 않는다.
        /// 첫 프레임 가중치가 1이라 인트로 첫 장면부터 이 샷이다.
        /// </summary>
        public static Shot EvaluateGuardianIntro(float seconds, Vector3 startPos, Vector3 endPos, Quaternion rotation)
        {
            if (Current == Style.Off || seconds < 0f || seconds >= BattleStaging.GuardianReleaseEnd) return Shot.None;
            float a = Mathf.Clamp01(seconds / BattleStaging.GuardianApproachEnd);
            float k = 1f - (1f - a) * (1f - a) * (1f - a);   // 감속 — 처음엔 성큼, 끝엔 조심스레
            Vector3 forward = rotation * Vector3.forward;
            float creep = Mathf.SmoothStep(0f, 1f, (seconds - BattleStaging.GuardianApproachEnd)
                / (BattleStaging.GuardianReleaseEnd - BattleStaging.GuardianApproachEnd));
            Vector3 pos = Vector3.Lerp(startPos, endPos, k) + forward * (BattleStaging.GuardianCreep * creep)
                + Kick(Vector3.up, seconds - BattleStaging.GuardianRoarAt, BattleStaging.GuardianRoarKick);
            float weight = seconds < BattleStaging.GuardianReleaseStart ? 1f
                : 1f - Mathf.SmoothStep(0f, 1f, (seconds - BattleStaging.GuardianReleaseStart)
                    / (BattleStaging.GuardianReleaseEnd - BattleStaging.GuardianReleaseStart));
            return new Shot(pos, rotation, weight);
        }

        // ── 전투 체감 샷 — 전용기·승리·진입. 시각표는 BattleFlourish ──

        /// <summary>
        /// 전용기 — 첫 프레임에 시전자 클로즈업으로 <b>끊어</b> 들어가(0.08초) 컷인 동안(<see cref="BattleFlourish.SignatureCutInEndFor"/>) 천천히 더 붙고
        /// 옆으로 조금 돈다 → 컷인이 끝나면 대상 쪽으로 휘돌아 넘어가 타격 순간 대상 클로즈업(시네마틱보다 가깝다) → 반동 → 원래 구도.
        /// 강화·회복 전용기는 시전자만 담고 풀린다. <see cref="Style.Off"/>면 걸지 않는다.
        /// </summary>
        /// <param name="progress">스킬 타임라인 0..1.</param>
        /// <param name="durationSeconds">타임라인 길이(연출 시계 초).</param>
        /// <param name="impactProgress">타격 진행률(<see cref="BattleMotion.ImpactOf"/>).</param>
        public static Shot EvaluateSignature(float progress, float durationSeconds, Vector3 basePos, Quaternion baseRot,
            Vector3 attacker, Vector3 target, float impactWeight, bool support, float impactProgress)
        {
            if (Current == Style.Off) return Shot.None;
            float p = Mathf.Clamp01(progress);
            if (p >= ReleaseEnd) return Shot.None;
            float dur = Mathf.Max(0.1f, durationSeconds);
            float impactAt = Mathf.Clamp(impactProgress, WindupEnd + 0.02f, HoldEnd - 0.02f);
            float seconds = p * dur;
            float impactSeconds = impactAt * dur;
            float cutEnd = BattleFlourish.SignatureCutInEndFor(impactSeconds);
            float weight = Mathf.Clamp01(impactWeight);

            Vector3 toCaster = attacker - basePos;
            toCaster.y = 0f;
            Vector3 side = toCaster.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, toCaster.normalized) : baseRot * Vector3.right;
            float c = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(seconds / cutEnd));
            Vector3 casterPos = Vector3.Lerp(Vector3.Lerp(basePos, attacker, BattleFlourish.SignatureCasterDollyStart),
                Vector3.Lerp(basePos, attacker, BattleFlourish.SignatureCasterDollyEnd), c) + Vector3.up * 0.12f + side * (0.3f * c);
            Quaternion casterRot = LookAt(casterPos, attacker + Vector3.up * 0.05f, baseRot);
            float snapIn = Mathf.SmoothStep(0f, 1f, seconds / 0.08f);

            if (support)
            {
                if (seconds < cutEnd) return new Shot(casterPos, casterRot, snapIn);
                float release = 1f - Mathf.SmoothStep(0f, 1f, (p - cutEnd / dur) / Mathf.Max(0.01f, HoldEnd - cutEnd / dur));
                return release <= 0f ? Shot.None : new Shot(casterPos, casterRot, release);
            }

            Vector3 hitDir = target - attacker;
            hitDir.y = 0f;
            hitDir = hitDir.sqrMagnitude > 0.0001f ? hitDir.normalized : baseRot * Vector3.right;
            Vector3 targetPos = Vector3.Lerp(basePos, target, BattleFlourish.SignatureTargetDolly + 0.06f * weight);
            Quaternion targetRot = LookAt(targetPos, Vector3.Lerp(target, attacker, 0.12f), baseRot);

            if (seconds < cutEnd) return new Shot(casterPos, casterRot, snapIn);
            if (p < impactAt)
            {
                float k = Mathf.SmoothStep(0f, 1f, (seconds - cutEnd) / Mathf.Max(0.01f, impactSeconds - cutEnd));
                return new Shot(Vector3.Lerp(casterPos, targetPos, k), Quaternion.Slerp(casterRot, targetRot, k), 1f);
            }
            Vector3 kicked = targetPos + Kick(hitDir, seconds - impactSeconds, 0.2f + 0.16f * weight);
            if (p < HoldEnd) return new Shot(kicked, targetRot, 1f);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, (p - HoldEnd) / (ReleaseEnd - HoldEnd));
            return new Shot(kicked, targetRot, fade);
        }

        /// <summary>
        /// 승리 — 주인공(<paramref name="pivot"/>, 1대1 내 곤충·레이드 팀 한가운데) 둘레를 <paramref name="orbitDegrees"/>만큼 돌며
        /// 다가가 <paramref name="endDistance"/>·<paramref name="endHeight"/>(주인공 중심 기준)에 선다. <b>얼굴 쪽</b>(<paramref name="face"/>)으로 돈다 —
        /// 1대1은 상대가 있던 쪽에서 내 곤충의 얼굴을 비스듬히 본다. 0.3초에 걸쳐 원래 구도에서 넘어오고, 다 돈 뒤엔 그 자리에 머문다
        /// (시각 <see cref="BattleFlourish.VictorySeconds"/>를 넘겨도 마지막 샷).
        /// </summary>
        public static Shot EvaluateVictoryOrbit(float seconds, Vector3 basePos, Quaternion baseRot, Vector3 pivot, Vector3 face,
            float orbitDegrees, float endDistance, float endHeight)
        {
            if (Current == Style.Off || seconds < 0f) return Shot.None;
            Vector3 off = basePos - pivot;
            Vector3 flat = new Vector3(off.x, 0f, off.z);
            float r0 = flat.magnitude;
            Vector3 d0 = r0 > 0.01f ? flat / r0 : FlatOr(baseRot * Vector3.back, Vector3.back);
            r0 = Mathf.Max(0.5f, r0);
            Vector3 faceFlat = FlatOr(face, d0);
            float sign = Mathf.Sign(Vector3.Cross(d0, faceFlat).y);
            if (Mathf.Abs(Vector3.Cross(d0, faceFlat).y) < 0.0001f) sign = 1f;

            float u = Mathf.Clamp01(seconds / BattleFlourish.VictorySeconds);
            float e = Mathf.SmoothStep(0f, 1f, u);
            Vector3 dir = Quaternion.AngleAxis(sign * orbitDegrees * e, Vector3.up) * d0;
            float r = Mathf.Lerp(r0, Mathf.Max(0.5f, endDistance), e);
            float h = Mathf.Lerp(off.y, endHeight, e);
            Vector3 pos = pivot + dir * r + Vector3.up * h;
            Quaternion rot = LookAt(pos, pivot + Vector3.up * 0.1f, baseRot);
            return new Shot(pos, rot, Mathf.SmoothStep(0f, 1f, seconds / 0.3f));
        }

        /// <summary>
        /// 전투 진입(<b>실제 초</b>) — 상대(<paramref name="subject"/>) 바로 옆 낮은 자리, 배틀 구도에서 상대 쪽으로 <see cref="BattleFlourish.OpeningSwingDegrees"/>
        /// 돌아간 곳에서 상대를 보며 시작해, 감속하며 크게 휘돌아 물러나 배틀 구도에 정확히 내려앉는다(<see cref="BattleFlourish.OpeningShotSeconds"/>).
        /// 첫 프레임 가중치 1 — 전투가 이 샷으로 열린다. 끝나면 <see cref="Shot.None"/>(같은 자리라 튀지 않는다).
        /// </summary>
        /// <param name="focus">배틀 구도가 담는 한가운데(두 곤충·팀과 보스의 경계 중심).</param>
        public static Shot EvaluateOpening(float seconds, Vector3 basePos, Quaternion baseRot, Vector3 focus, Vector3 subject)
        {
            if (Current == Style.Off || seconds < 0f || seconds >= BattleFlourish.OpeningShotSeconds) return Shot.None;
            float u = Mathf.Clamp01(seconds / BattleFlourish.OpeningShotSeconds);
            float e = 1f - (1f - u) * (1f - u) * (1f - u);   // 크게 휘돌다 감속하며 내려앉는다
            Vector3 off = basePos - focus;
            Vector3 flat = new Vector3(off.x, 0f, off.z);
            float r0 = flat.magnitude;
            Vector3 d0 = r0 > 0.01f ? flat / r0 : FlatOr(baseRot * Vector3.back, Vector3.back);
            r0 = Mathf.Max(0.5f, r0);
            Vector3 toSubject = FlatOr(subject - focus, -d0);
            float cross = Vector3.Cross(d0, toSubject).y;
            float sign = Mathf.Abs(cross) < 0.0001f ? 1f : Mathf.Sign(cross);

            Vector3 pivot = Vector3.Lerp(subject, focus, e);
            Vector3 dir = Quaternion.AngleAxis(sign * BattleFlourish.OpeningSwingDegrees * (1f - e), Vector3.up) * d0;
            float r = Mathf.Lerp(r0 * BattleFlourish.OpeningStartDistance, r0, e);
            float h = Mathf.Lerp(off.y * BattleFlourish.OpeningStartHeight, off.y, e);
            Vector3 pos = pivot + dir * r + Vector3.up * h;
            Quaternion look = LookAt(pos, Vector3.Lerp(subject, focus, e * e), baseRot);
            Quaternion rot = Quaternion.Slerp(look, baseRot, Mathf.SmoothStep(0f, 1f, (u - 0.45f) / 0.55f));
            return new Shot(pos, rot, 1f);
        }

        private static Vector3 FlatOr(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : fallback;
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
