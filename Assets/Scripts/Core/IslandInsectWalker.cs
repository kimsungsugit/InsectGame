using System.Collections.Generic;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 섬에 풀어놓은 곤충의 느긋한 배회. 빈 칸 하나를 골라 걸어가고, 잠깐 쉬고, 또 고른다.
    ///
    /// 모델은 <c>InsectEntity.BuildForBattle</c>로 짓는다 — 그 경로는 배회·도주 AI가 꺼져 있고 포획 대상도
    /// 아니다(야생 스폰 경로로 지으면 섬 위에서 플레이어를 보고 달아나고 잡기 버튼이 뜬다).
    /// 그래서 몸을 옮기는 일은 이 컴포넌트가 맡는다. 날갯짓은 InsectEntity가 그대로 돌린다.
    ///
    /// <b>시간·날씨를 탄다</b>(<see cref="IslandInsectMood"/>) — 야행성은 밤에 활발하고 낮엔 대부분 쉰다(오래 쉬고 느리고,
    /// 나는 종은 땅에 낮게 내려앉는다). 주행성은 반대다. 월드 빌더가 상태가 바뀔 때 <see cref="ApplyWorld"/>로 알려 준다.
    /// </summary>
    public class IslandInsectWalker : MonoBehaviour
    {
        /// <summary>곤충 모델의 원점이 지면에서 뜨는 높이 — 배틀 아레나가 세우는 높이와 같다.</summary>
        internal const float GroundOffset = 0.45f;

        private const float FlyHeight = 0.9f;
        private const int PickAttempts = 6;
        private const int WanderCells = 4;
        /// <summary>나는 높이가 기분을 따라 바뀌는 속도(m/s) — 상태가 바뀐 순간 툭 떨어지지 않고 내려앉는다.</summary>
        private const float HoverChangeSpeed = 0.5f;

        private Vector3 islandOrigin;
        private IReadOnlyList<Vector2Int> freeCells;
        private HashSet<Vector2Int> freeSet;
        private Vector3 target;
        private bool moving;
        private float restTimer;
        private float speed;
        private bool flies;
        private float bobPhase;
        private InsectHabit habit = InsectHabit.Neutral;
        private IslandInsectMood.Mood mood = IslandInsectMood.Neutral;
        private float hover = FlyHeight;

        /// <summary>지금 기분(테스트·검수가 읽는다).</summary>
        public IslandInsectMood.Mood CurrentMood => mood;

        /// <summary>
        /// <paramref name="cells"/>는 지금 섬의 빈 칸(월드 빌더가 배치가 바뀔 때마다 다시 넘긴다).
        /// 목록을 복사하지 않는다 — 여러 마리가 같은 목록을 본다.
        /// </summary>
        public void Initialize(Vector3 origin, IReadOnlyList<Vector2Int> cells, HashSet<Vector2Int> cellSet, int seed)
        {
            islandOrigin = origin;
            freeCells = cells;
            freeSet = cellSet;
            flies = transform.Find("WingL") != null;
            InsectEntity entity = GetComponent<InsectEntity>();
            habit = entity != null ? InsectHabits.For(entity.Data) : InsectHabit.Neutral;
            mood = IslandInsectMood.Neutral;
            // 같은 프레임에 세워진 곤충들이 같은 박자로 움직이지 않게 개체마다 어긋나게 둔다.
            var rng = new System.Random(seed);
            speed = 0.55f + (float)rng.NextDouble() * 0.5f;
            restTimer = (float)rng.NextDouble() * 2.5f;
            bobPhase = (float)rng.NextDouble() * Mathf.PI * 2f;
            moving = false;
            hover = FlyHeight;
        }

        /// <summary>
        /// 지금 섬의 시간대·날씨를 알려 준다 — 기분을 다시 정한다. 쉬는 중이면 남은 쉬기를 새 기분의 범위로 가둔다
        /// (밤이 됐는데 낮에 잡은 12초 쉬기를 다 채우지 않게). 나는 높이는 <see cref="Update"/>가 천천히 따라간다.
        /// </summary>
        public void ApplyWorld(WorldState state)
        {
            mood = IslandInsectMood.For(habit, state);
            if (!moving) restTimer = Mathf.Min(restTimer, mood.RestMax);
        }

        private void Update()
        {
            if (freeCells == null || freeCells.Count == 0) return;
            float dt = Time.deltaTime;

            Vector3 pos = transform.position;
            if (flies) hover = Mathf.MoveTowards(hover, FlyHeight * mood.HoverMultiplier, HoverChangeSpeed * dt);
            float baseY = islandOrigin.y + GroundOffset * transform.localScale.y
                          + (flies ? hover + Mathf.Sin(Time.time * 1.7f + bobPhase) * 0.12f * mood.HoverMultiplier : 0f);

            if (!moving)
            {
                restTimer -= dt;
                if (restTimer <= 0f) PickTarget(pos);
                pos.y = baseY;
                transform.position = pos;
                return;
            }

            Vector3 to = target - pos;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.08f)
            {
                moving = false;
                restTimer = Random.Range(mood.RestMin, mood.RestMax);
            }
            else
            {
                Vector3 dir = to / dist;
                pos += dir * Mathf.Min(dist, speed * mood.SpeedMultiplier * dt);
                Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 240f * dt);
            }
            pos.y = baseY;
            transform.position = pos;
        }

        // 가까운 빈 칸 가운데 **가는 길이 전부 빈 칸**인 곳을 고른다 — 그러지 않으면 건물을 뚫고 지나간다.
        private void PickTarget(Vector3 from)
        {
            IslandGrid.CellAt(from - islandOrigin, out int cx, out int cz);
            for (int attempt = 0; attempt < PickAttempts; attempt++)
            {
                Vector2Int cell = freeCells[Random.Range(0, freeCells.Count)];
                if (Mathf.Abs(cell.x - cx) > WanderCells || Mathf.Abs(cell.y - cz) > WanderCells) continue;
                Vector3 candidate = islandOrigin + IslandGrid.FootprintCenter(cell.x, cell.y, 1, 1);
                if (!PathIsFree(from, candidate)) continue;
                target = candidate;
                moving = true;
                return;
            }
            // 못 찾았다(좁은 틈에 끼었거나 섬이 꽉 찼다) — 조금 쉬고 다시 고른다.
            restTimer = 1f;
        }

        private bool PathIsFree(Vector3 from, Vector3 to)
        {
            float cs = GameConstants.Island.CellSize;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / (cs * 0.5f)));
            for (int i = 1; i <= steps; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, i / (float)steps) - islandOrigin;
                IslandGrid.CellAt(p, out int x, out int z);
                if (!freeSet.Contains(new Vector2Int(x, z))) return false;
            }
            return true;
        }
    }
}
