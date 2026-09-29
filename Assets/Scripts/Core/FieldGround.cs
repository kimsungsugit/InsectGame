using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 콜라이더 없는 둔덕(사구·눈 언덕·능선·초원 언덕·재 더미·이끼 둔덕 …)의 지면 높이를 묻는 곳.
    ///
    /// 왜 필요한가: 둔덕은 <b>일부러 콜라이더가 없다</b>. <c>PlayerMovement</c>의 접지는 <c>Max(pos.y, hit.y)</c>라
    /// 올라가기만 하고 내려오지 않아서, 밟히는 콜라이더를 두면 둔덕을 지난 뒤에도 그 높이에 뜬 채 남는다. 그런데
    /// 둔덕이 지면 위로 드러나자(<c>RegionTerrainBuilder.PlaceMound</c>) 평면 y에 서는 것들이 그 속에 묻혔다 —
    /// 모래언덕 사구 꼭대기는 바닥 위 0.62m인데 기어다니는 곤충의 몸 꼭대기는 바닥 위 0.25~0.48m다(보통 등급).
    /// 레이캐스트로는 못 찾으니 둔덕을 지은 쪽이 모양을 여기 올리고, 서 있을 높이가 필요한 쪽이 좌표로 묻는다.
    ///
    /// 둔덕은 전부 <b>반쯤 묻힌 타원체</b>(내장 Sphere를 늘린 것)라 중심·반축·요(y축 회전)만 알면 윗면이 정해진다.
    /// 월드는 씬마다 새로 지어지므로 <see cref="Clear"/>는 짓는 쪽(<c>RegionTerrainBuilder</c>)이 빌드 시작과 파괴 때 부른다.
    /// 서브에리어는 (2000,·,2000) 너머의 다른 좌표라 여기 둔덕과 겹치지 않는다 — 거기서 물으면 0이 나온다.
    /// </summary>
    public static class FieldGround
    {
        /// <summary>
        /// 둔덕이 기준으로 삼는 리전 바닥 높이(월드 y). 실제 리전 평면은 <c>PlaySceneBootstrap</c>이 0.08 + 순번 × 0.001에
        /// 깔지만 둔덕·장식은 전부 이 값 위에 놓는다 — 1~2cm 차이는 눈에 안 띄고, 기준이 하나여야 높이가 서로 맞는다.
        /// </summary>
        public const float FloorY = 0.1f;

        private struct Dome
        {
            public Vector2 center;          // XZ
            public float centerY;
            public float halfX, halfY, halfZ;   // 반축(월드 m)
            public Quaternion inverseYaw;
            public float reach;             // 빠른 배제용 — 가장 긴 수평 반축
        }

        private static readonly List<Dome> domes = new List<Dome>();

        /// <summary>등록된 둔덕 수(테스트·진단용).</summary>
        public static int Count => domes.Count;

        public static void Clear() => domes.Clear();

        /// <summary>
        /// 타원체 둔덕 하나를 올린다. <paramref name="semiAxes"/>는 월드 반축(내장 Sphere면 <c>lossyScale × 0.5</c>),
        /// <paramref name="yawDeg"/>는 y축 회전(<c>Quaternion.Euler(0, yaw, 0)</c>과 같은 뜻)이다. 윗면이 <see cref="FloorY"/>를
        /// 넘지 못하는 것(완전히 묻힌 옛 둔덕)은 올려도 높이를 바꾸지 않는다.
        /// </summary>
        public static void AddDome(Vector3 center, Vector3 semiAxes, float yawDeg)
        {
            if (semiAxes.x <= 0f || semiAxes.y <= 0f || semiAxes.z <= 0f) return;
            if (center.y + semiAxes.y <= FloorY) return;
            domes.Add(new Dome
            {
                center = new Vector2(center.x, center.z),
                centerY = center.y,
                halfX = semiAxes.x,
                halfY = semiAxes.y,
                halfZ = semiAxes.z,
                inverseYaw = Quaternion.Inverse(Quaternion.Euler(0f, yawDeg, 0f)),
                reach = Mathf.Max(semiAxes.x, semiAxes.z),
            });
        }

        /// <summary>
        /// (x, z)에 선 것이 발을 둘 높이(월드 y) — 둔덕 밖이면 <see cref="FloorY"/>. 둔덕이 겹치면 높은 쪽.
        /// </summary>
        public static float SurfaceY(float x, float z)
        {
            float best = FloorY;
            for (int i = 0; i < domes.Count; i++)
            {
                Dome d = domes[i];
                float dx = x - d.center.x, dz = z - d.center.y;
                if (dx * dx + dz * dz >= d.reach * d.reach) continue;
                float y = DomeTop(d, dx, dz);
                if (y > best) best = y;
            }
            return best;
        }

        /// <summary>
        /// (x, z)에서 둔덕이 바닥(<see cref="FloorY"/>) 위로 올라온 높이(m, 0 이상). 평면 높이에 서 있던 것에 더하면
        /// 둔덕 윗면에 선다 — 스폰·이동 쪽은 이 값만 알면 된다.
        /// </summary>
        public static float LiftAt(float x, float z) => SurfaceY(x, z) - FloorY;

        /// <summary>타원체 윗면 y. 수평 투영 밖이면 음의 무한대.</summary>
        private static float DomeTop(in Dome d, float dx, float dz)
        {
            Vector3 local = d.inverseYaw * new Vector3(dx, 0f, dz);
            float u = local.x / d.halfX, v = local.z / d.halfZ;
            float rr = u * u + v * v;
            if (rr >= 1f) return float.NegativeInfinity;
            return d.centerY + d.halfY * Mathf.Sqrt(1f - rr);
        }
    }
}
