using UnityEngine;

namespace InsectGame.Spawning
{
    /// <summary>
    /// 도주 방향 고르기 — <b>순수 판정부</b>. 실제 장애물 측정(스피어캐스트)은 <see cref="InsectEntity"/>가 하고
    /// 여기는 "어느 방향을, 얼마나"만 답한다.
    ///
    /// 예전 도주는 플레이어 반대쪽으로 1.1초간 곧장 최대 8m를 갔다 — 벽·건물·바위·나무 줄기 콜라이더를
    /// 그대로 뚫었고 서브에리어 방 벽도 넘어 방 밖 허공으로 나갔다(곤충은 몸 콜라이더가 없어 물리가 막지 않는다).
    /// 이제 반대쪽부터 좌우로 벌려 가며 뚫린 방향을 찾고, 다 막혔으면 가장 멀리 갈 수 있는 쪽으로 장애물 앞까지만 간다.
    /// </summary>
    internal static class FleePath
    {
        /// <summary>
        /// 도주 1.1초의 최대 수평 이동(m). 가장 빠른 비행(7.5m/s × 1.1 ≈ 8.3m)을 덮는다 —
        /// 기어다님 6.6m, 점프 약 6.7m는 이보다 짧다.
        /// </summary>
        internal const float MaxDistance = 8.5f;

        /// <summary>장애물 앞에서 멈추는 여유(m) — 몸(등급 배율 최대 1.9)이 벽면에 파묻히지 않게.</summary>
        internal const float WallMargin = 0.6f;

        /// <summary>
        /// 후보 방향 — 플레이어 반대쪽(0°)에서 좌우로 벌린다. ±110°까지 가면 옆으로 비껴 달아나는 셈이다.
        /// 그보다 더 돌면 플레이어 쪽으로 뛰어드는 꼴이라 넣지 않는다.
        /// </summary>
        internal static readonly float[] CandidateAngles = { 0f, 35f, -35f, 70f, -70f, 110f, -110f };

        /// <summary>한 방향을 잴 때 쏘는 길이 — 끝까지 가도 여유만큼 남아야 뚫린 것으로 친다.</summary>
        internal static float ProbeLength => MaxDistance + WallMargin;

        /// <summary>
        /// <paramref name="index"/>번째 후보 방향. <paramref name="sideSign"/>이 −1이면 좌우를 뒤집는다 —
        /// 늘 같은 쪽부터 보면 벽 앞의 곤충들이 전부 한쪽으로만 비껴 달아난다.
        /// </summary>
        internal static Vector3 DirectionFor(Vector3 away, int index, float sideSign)
        {
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f) away = Vector3.forward;
            away.Normalize();
            float angle = CandidateAngles[Mathf.Clamp(index, 0, CandidateAngles.Length - 1)] * (sideSign < 0f ? -1f : 1f);
            return Quaternion.AngleAxis(angle, Vector3.up) * away;
        }

        /// <summary>그 방향으로 갈 수 있는 거리(<paramref name="clearance"/>)가 도주 전체를 덮는가.</summary>
        internal static bool IsClear(float clearance) => clearance >= ProbeLength - 1e-4f;

        /// <summary>장애물까지 <paramref name="clearance"/>m일 때 실제로 갈 거리 — 여유를 빼고 0~최대로 자른다.</summary>
        internal static float AllowedDistance(float clearance)
            => Mathf.Clamp(clearance - WallMargin, 0f, MaxDistance);

        /// <summary>
        /// 방향을 고른다. 후보를 순서대로 재서 <b>처음 뚫린 방향</b>을 쓰고, 전부 막혔으면 가장 멀리 가는 방향을 쓴다.
        /// <paramref name="clearance"/>는 방향 → 막히지 않고 갈 수 있는 거리(m, 막힘 없으면 <see cref="ProbeLength"/> 이상).
        /// </summary>
        internal static Vector3 Choose(Vector3 away, float sideSign, System.Func<Vector3, float> clearance,
            out float allowedDistance)
        {
            Vector3 best = DirectionFor(away, 0, sideSign);
            float bestClear = -1f;
            for (int i = 0; i < CandidateAngles.Length; i++)
            {
                Vector3 dir = DirectionFor(away, i, sideSign);
                float c = clearance != null ? clearance(dir) : ProbeLength;
                if (IsClear(c))
                {
                    allowedDistance = MaxDistance;
                    return dir;
                }
                if (c > bestClear)
                {
                    bestClear = c;
                    best = dir;
                }
            }
            allowedDistance = AllowedDistance(bestClear);
            return best;
        }

        /// <summary>
        /// 이번 프레임에 실제로 갈 수평 거리 — 남은 허용 거리(<paramref name="allowed"/> − <paramref name="travelled"/>)를
        /// 넘지 않는다. 다 쓰면 0이라 장애물 앞에 멈춘 채 타이머가 끝나 사라진다.
        /// </summary>
        internal static float StepDistance(float wanted, float allowed, float travelled)
            => Mathf.Clamp(wanted, 0f, Mathf.Max(0f, allowed - travelled));
    }
}
