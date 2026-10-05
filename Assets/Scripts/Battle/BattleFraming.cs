using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>Fits combatant bounds above the action panel. Coordinates are viewport coordinates.</summary>
    public static class BattleFraming
    {
        public static float DuelHalfSeparation(Bounds ally, Bounds opponent)
        {
            // Keep small combatants readable while leaving room for wide wings/horns.
            return Mathf.Max(1.55f, (ally.extents.x + opponent.extents.x + 0.9f) * 0.5f);
        }
        public static void Compute(Bounds bounds, float aspect, float fieldOfView,
            out Vector3 position, out Vector3 target)
        {
            ComputeDuel(bounds, aspect, fieldOfView, Quaternion.Euler(12f, -12f, 0f),
                new Rect(0f, 0f, 1f, 1f), out position, out target);
        }

        public static void Compute(Bounds bounds, float aspect, float fieldOfView, Quaternion rotation,
            Rect safeViewport, out Vector3 position, out Vector3 target)
        {
            ComputeWindow(bounds, aspect, fieldOfView, rotation, safeViewport, 0.40f, 0.84f, out position, out target);
        }

        /// <summary>
        /// <paramref name="window"/>(뷰포트 좌표의 창) 안에 경계를 맞춘다 — 위의 둘과 달리 HUD 여백 비율(아래 패널 몫)을 따로 두지 않고
        /// 창을 그대로 쓴다(가로만 창 폭의 8%씩 안쪽). 수문장 등장 컷이 "화면 위쪽 2/3"을 창으로 넘긴다(<see cref="BattleStaging"/>).
        /// </summary>
        public static void ComputeInWindow(Bounds bounds, float aspect, float fieldOfView, Quaternion rotation,
            Rect window, out Vector3 position, out Vector3 target)
        {
            ComputeWindow(bounds, aspect, fieldOfView, rotation, window, 0f, 1f, out position, out target);
        }

        public static void ComputeDuel(Bounds bounds, float aspect, float fieldOfView, Quaternion rotation,
            Rect safeViewport, out Vector3 position, out Vector3 target)
        {
            ComputeWindow(bounds, aspect, fieldOfView, rotation, safeViewport, 0.34f, 0.77f, out position, out target);
        }

        private static void ComputeWindow(Bounds bounds, float aspect, float fieldOfView, Quaternion rotation,
            Rect safeViewport, float lower, float upper, out Vector3 position, out Vector3 target)
        {
            Quaternion inverse = Quaternion.Inverse(rotation);
            float tanV = Mathf.Tan(Mathf.Clamp(fieldOfView, 20f, 100f) * Mathf.Deg2Rad * 0.5f);
            float tanH = tanV * Mathf.Max(0.5f, aspect);
            float left = Mathf.Lerp(safeViewport.xMin, safeViewport.xMax, 0.08f) * 2f - 1f;
            float right = Mathf.Lerp(safeViewport.xMin, safeViewport.xMax, 0.92f) * 2f - 1f;
            float bottom = Mathf.Lerp(safeViewport.yMin, safeViewport.yMax, lower) * 2f - 1f;
            float top = Mathf.Lerp(safeViewport.yMin, safeViewport.yMax, upper) * 2f - 1f;
            float centerX = (left + right) * 0.5f;
            float centerY = (bottom + top) * 0.5f;
            float halfX = Mathf.Max(0.05f, (right - left) * 0.5f);
            float halfY = Mathf.Max(0.05f, (top - bottom) * 0.5f);
            float distance = 3f;
            // Fit the requested HUD-free window, preserving the raid's separate layout.
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = Vector3.Scale(bounds.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 p = inverse * corner;
                distance = Mathf.Max(distance, (p.x - right * tanH * p.z) / (halfX * tanH));
                distance = Mathf.Max(distance, (-p.x + left * tanH * p.z) / (halfX * tanH));
                distance = Mathf.Max(distance, (p.y - top * tanV * p.z) / (halfY * tanV));
                distance = Mathf.Max(distance, (-p.y + bottom * tanV * p.z) / (halfY * tanV));
            }
            distance += 0.35f;
            target = bounds.center - rotation * new Vector3(distance * tanH * centerX, distance * tanV * centerY, 0f);
            position = target - rotation * Vector3.forward * distance;
        }

        public static Bounds ModelBounds(GameObject model)
        {
            if (model == null) return new Bounds(Vector3.zero, Vector3.one);
            Bounds result = new Bounds(model.transform.position, Vector3.one * 0.2f);
            bool found = false;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer is ParticleSystemRenderer
                    || renderer.name == "BossAura" || renderer.name.Contains("Glow")) continue;
                if (!found) { result = renderer.bounds; found = true; }
                else result.Encapsulate(renderer.bounds);
            }
            return result;
        }
    }
}
