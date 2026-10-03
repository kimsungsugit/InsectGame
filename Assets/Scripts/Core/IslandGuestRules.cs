using System;
using System.Collections.Generic;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 나의 섬 <b>손님 곤충</b>의 순수 규칙 — 밤이거나 비·안개일 때 야생 곤충이 1~2마리 찾아와 잡을 수 있다.
    /// 런타임(기록·몸)은 <c>IslandWorldBuilder.Guests.cs</c>가 든다. 규칙은 <b>필드 스폰의 순수 함수를 그대로 다시 쓴다</b> —
    /// 등급표·레벨·재생 지연·보너스 수를 여기서 새로 만들지 않는다(<see cref="FieldSpawnRules"/>).
    ///
    /// <code>
    /// 수       = FieldSpawnRules.BonusSlots(세계 상태)        밤 +1 · 비·안개 +1 · 최대 2 (필드 보너스 슬롯과 같은 모양)
    /// 후보     = 해금된 리전 풀의 합집합 ∩ 지금 시간·날씨에 나오는 종(InsectData.Matches) — 비면 합집합 전체
    /// 등급     = FieldSpawnRules.PickRarity(전역 표, 부스트 없음) — 영웅·전설은 후보에서 뺀다(아래 MaxGuestRarity)
    /// 종       = 그 등급 안에서 spawnWeight × InsectHabits.SpawnWeightMultiplier(세계 상태)  (FieldSpawnRules.PickWeighted)
    /// 레벨     = 그 종이 사는 해금 리전 중 <b>가장 낮은</b> 리전의 대역 → FieldSpawnRules.RollFieldLevel
    /// 재생     = 잡기·이기기·놓침 뒤 FieldSpawnRules.RespawnDelay(60~120초) · 수명 FieldSpawnRules.Lifetime(4~7분)
    /// 도착     = 조건이 막 시작되면 FieldSpawnRules.PhaseSwapDelay(5~60초)로 흩어 온다
    /// 떠남     = 조건이 끝났거나 수명이 다했고, 붙잡히지·놀라지 않았고, <b>화면 밖</b>일 때
    /// </code>
    /// </summary>
    public static class IslandGuestRules
    {
        /// <summary>
        /// 손님 등급 상한 — <b>희귀까지</b>. 영웅·전설은 레이드로만 맞설 수 있는데(<c>CaptureChoiceUI.IsRaidRarity</c>), 섬 손님으로 오면
        /// ①집에서 이동 없이 영웅·전설 레이드(이기면 확정 포획)를 밤마다 받는 길이 생기고 ②섬은 곤충을 1배로 두는 곳인데 전설 야생 몸은
        /// 1.9배라 가구 사이에서 어색하고 ③섬에서 레이드 아레나로 갔다 돌아오는 길을 검증하지 못했다(Unity 미실행).
        /// 빠진 두 등급의 몫(4%)은 등급표의 기존 대체 규칙(<see cref="FieldSpawnRules.Fallback"/> — 가까운 아래)대로 희귀로 내려온다 —
        /// 섬 손님의 희귀 몫은 11% → 15%다.
        /// </summary>
        public const InsectRarity MaxGuestRarity = InsectRarity.Rare;

        /// <summary>화면 밖 판정의 여유(뷰포트 비율) — 화면 가장자리에 걸친 곤충도 "보인다"로 친다.</summary>
        public const float ViewportMargin = 0.08f;

        /// <summary>섬 빈 칸을 따라 잴 때 한 걸음(m) — 칸(1.5m)의 6분의 1이라 모서리를 건너뛰지 않는다.</summary>
        public const float FreeRunStep = 0.25f;

        /// <summary>판정 주기(초) — 필드 스포너 틱과 같다.</summary>
        public const float TickSeconds = FieldSpawnRules.TickSeconds;

        /// <summary>최대 손님 수(<see cref="FieldSpawnRules.MaxBonusSlots"/>).</summary>
        public const int MaxGuests = FieldSpawnRules.MaxBonusSlots;

        /// <summary>지금 세계 상태에서 섬에 와 있을 손님 수 — 필드 보너스 슬롯과 같은 식이다(단일 출처).</summary>
        public static int WantedGuests(WorldState state) => FieldSpawnRules.BonusSlots(state);

        /// <summary>
        /// 지금 손님이 섬에 <b>들어설</b> 수 있는가 — 내 섬이고(남의 섬 구경 아님), 꿈이 아니고, 꾸미기 중이 아닐 때.
        /// 꾸미기 화면은 지면 탭이 「칸 고르기」라 곤충 탭과 겹치면 안 된다(이미 와 있는 손님은 그대로 두고 새로 들이지만 않는다).
        /// </summary>
        public static bool CanHostGuests(IslandMode mode, bool dreamMode, bool editing)
            => mode == IslandMode.Own && !dreamMode && !editing;

        /// <summary>떠날 차례인가 — 조건이 끝나 수가 줄었거나(<paramref name="surplus"/>) 수명이 다했다.</summary>
        public static bool ShouldLeave(bool surplus, bool expired) => surplus || expired;

        /// <summary>
        /// 지금 떠나도 되는가 — 붙잡히거나(포획·전투) 놀라 있지 않고(경계·도주), <b>플레이어 눈 밖</b>일 때. 필드의 "25m 밖에서만 바꾼다"와
        /// 같은 약속이지만 섬은 한 변이 15~33m라 거리로는 못 재서 화면으로 잰다.
        /// </summary>
        public static bool CanLeaveNow(bool busy, bool inView) => !busy && !inView;

        /// <summary>뷰포트 좌표(<c>Camera.WorldToViewportPoint</c>)가 화면 안(여유 포함)인가. 카메라 뒤는 안 보인다.</summary>
        public static bool IsInView(Vector3 viewport, float margin = ViewportMargin)
        {
            return viewport.z > 0f
                   && viewport.x >= -margin && viewport.x <= 1f + margin
                   && viewport.y >= -margin && viewport.y <= 1f + margin;
        }

        /// <summary>
        /// 해금된 리전 표만 남긴다(<paramref name="isAccessible"/> — <c>RegionManager.IsRegionAccessible</c>). 레벨 대역 고르기가
        /// "가장 낮은 리전"을 쓰므로 <b>최소 레벨 오름차순</b>으로 정렬해 둔다.
        /// </summary>
        public static void FilterAccessible(IReadOnlyList<FieldRegionTable> all, Func<string, bool> isAccessible,
            List<FieldRegionTable> into)
        {
            into.Clear();
            if (all == null) return;
            for (int i = 0; i < all.Count; i++)
            {
                FieldRegionTable t = all[i];
                if (t.Pool == null || t.Pool.Count == 0) continue;
                if (isAccessible != null && !isAccessible(t.RegionId)) continue;
                into.Add(t);
            }
            into.Sort((a, b) => a.MinLevel.CompareTo(b.MinLevel));
        }

        /// <summary>
        /// 손님 후보 — 해금된 풀들의 합집합 가운데 지금 시간·날씨에 나오는 종(<see cref="InsectData.Matches"/> — 필드 후보와 같은 거르개).
        /// 그렇게 걸러 아무것도 안 남으면 합집합 전체다(필드도 그렇게 물러난다 — 손님이 영영 안 오지 않게).
        /// </summary>
        public static void CollectCandidates(IReadOnlyList<FieldRegionTable> accessible, WorldState state, List<InsectData> into)
        {
            into.Clear();
            if (accessible == null) return;
            for (int t = 0; t < accessible.Count; t++)
            {
                IReadOnlyList<InsectData> pool = accessible[t].Pool;
                if (pool == null) continue;
                for (int i = 0; i < pool.Count; i++)
                {
                    InsectData d = pool[i];
                    if (d == null || !d.Matches(state) || into.Contains(d)) continue;
                    into.Add(d);
                }
            }
            if (into.Count > 0) return;
            for (int t = 0; t < accessible.Count; t++)
            {
                IReadOnlyList<InsectData> pool = accessible[t].Pool;
                if (pool == null) continue;
                for (int i = 0; i < pool.Count; i++)
                    if (pool[i] != null && !into.Contains(pool[i])) into.Add(pool[i]);
            }
        }

        /// <summary>
        /// 손님 종 하나 — 등급을 전역 표로 먼저 굴리고(<see cref="FieldSpawnRules.PickRarity"/>, <see cref="MaxGuestRarity"/> 위는 후보에서 뺀다)
        /// 그 등급 안에서 <c>spawnWeight</c> × 성향 배수로 고른다(<see cref="FieldSpawnRules.PickWeighted"/>). 후보가 없으면 null.
        /// 스크래치 목록·배열은 호출부가 들고 있다가 넘긴다(굴릴 때마다 할당하지 않게).
        /// </summary>
        public static InsectData PickSpecies(IReadOnlyList<InsectData> candidates, WorldState state, float rarityRoll,
            float speciesRoll, List<InsectData> scratch, List<float> scratchMultipliers, bool[] available)
        {
            if (candidates == null || candidates.Count == 0 || scratch == null || scratchMultipliers == null
                || available == null || available.Length < FieldSpawnRules.RarityCount) return null;

            for (int r = 0; r < available.Length; r++) available[r] = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                InsectData d = candidates[i];
                if (d == null || d.rarity > MaxGuestRarity) continue;
                int r = (int)d.rarity;
                if (r >= 0 && r < available.Length) available[r] = true;
            }
            int rarity = FieldSpawnRules.PickRarity(rarityRoll, 1f, available);
            if (rarity < 0) return null;

            scratch.Clear();
            scratchMultipliers.Clear();
            for (int i = 0; i < candidates.Count; i++)
            {
                InsectData d = candidates[i];
                if (d == null || (int)d.rarity != rarity) continue;
                scratch.Add(d);
                scratchMultipliers.Add(InsectHabits.SpawnWeightMultiplier(InsectHabits.For(d), state));
            }
            return FieldSpawnRules.PickWeighted(scratch, scratchMultipliers, speciesRoll);
        }

        /// <summary>
        /// 손님 레벨의 대역 — <b>그 종이 사는 해금 리전 가운데 가장 낮은 리전</b>의 대역(<paramref name="accessibleByLevel"/>는 최소 레벨
        /// 오름차순). 그 종을 필드에서 처음 만나는 곳과 같은 레벨이라 섬이 고레벨 사냥터(가장 높은 해금 리전의 대역)도,
        /// 뒤 리전 종을 낮은 레벨로 거저 주는 곳(가장 낮은 리전의 대역)도 되지 않는다. 어느 풀에도 없으면(있을 수 없다) false.
        /// </summary>
        public static bool TryLevelBand(InsectData species, IReadOnlyList<FieldRegionTable> accessibleByLevel,
            out int minLevel, out int maxLevel)
        {
            minLevel = maxLevel = 1;
            if (species == null || accessibleByLevel == null) return false;
            for (int t = 0; t < accessibleByLevel.Count; t++)
            {
                IReadOnlyList<InsectData> pool = accessibleByLevel[t].Pool;
                if (pool == null) continue;
                for (int i = 0; i < pool.Count; i++)
                {
                    if (!ReferenceEquals(pool[i], species)) continue;
                    minLevel = accessibleByLevel[t].MinLevel;
                    maxLevel = accessibleByLevel[t].MaxLevel;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 섬 원점 기준 <paramref name="localStart"/>에서 <paramref name="direction"/>으로 <b>빈 칸만 밟고</b> 갈 수 있는 거리(m, 최대
        /// <paramref name="maxDistance"/>). 손님의 도주가 이 거리를 넘지 않는다(<c>InsectEntity.SetFleeArea</c>) — 섬 밖·물·건물 칸으로 달아나지 않는다.
        /// 출발 칸부터 빈 칸이 아니면 0.
        /// </summary>
        public static float FreeRun(Vector3 localStart, Vector3 direction, ICollection<Vector2Int> freeCells,
            float maxDistance, float step = FreeRunStep)
        {
            if (freeCells == null || maxDistance <= 0f) return 0f;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) return 0f;
            direction.Normalize();
            step = Mathf.Max(0.05f, step);

            IslandGrid.CellAt(localStart, out int sx, out int sz);
            if (!freeCells.Contains(new Vector2Int(sx, sz))) return 0f;

            float run = 0f;
            while (run + step <= maxDistance)
            {
                Vector3 p = localStart + direction * (run + step);
                IslandGrid.CellAt(p, out int x, out int z);
                if (!freeCells.Contains(new Vector2Int(x, z))) return run;
                run += step;
            }
            return maxDistance;
        }

        /// <summary>
        /// 손님이 들어설 때 플레이어와 떨어뜨릴 최소 거리(칸, 1칸 1.5m). 전투 중에는 카메라가 아레나에 가 있어 섬 전체가 "화면 밖"이다 —
        /// 그때 들어선 손님이 플레이어 바로 옆이면 돌아오자마자 놀라 달아난다. 가장 작은 섬(10칸)에서도 도착 자리에서 이만큼 떨어진 칸이 남는다.
        /// </summary>
        public const int ArrivalMinCells = 3;

        /// <summary>
        /// 손님이 들어설 칸 — 플레이어에서 <see cref="ArrivalMinCells"/> 이상 떨어진 <b>화면 밖 칸</b> 가운데 하나(<paramref name="roll01"/>로),
        /// 그런 칸이 없으면 플레이어에서 가장 먼 칸. 다른 손님이 선 칸(<paramref name="taken"/>)은 고르지 않는다. 고를 칸이 없으면 false.
        /// 눈앞에 갑자기 생기지 않게 하는 필드의 "새 개체는 20m 밖"과 같은 약속이다(섬은 작아 거리 대신 화면으로 잰다).
        /// </summary>
        public static bool PickArrivalCell(IReadOnlyList<Vector2Int> freeCells, Func<Vector2Int, bool> inView,
            Vector2Int playerCell, ICollection<Vector2Int> taken, float roll01, out Vector2Int cell)
        {
            cell = default;
            if (freeCells == null || freeCells.Count == 0) return false;

            int hidden = 0;
            for (int i = 0; i < freeCells.Count; i++)
            {
                Vector2Int c = freeCells[i];
                if (!IsArrivalCandidate(c, playerCell, taken)) continue;
                if (inView == null || !inView(c)) hidden++;
            }
            if (hidden > 0)
            {
                int pick = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(roll01) * hidden), 0, hidden - 1);
                for (int i = 0; i < freeCells.Count; i++)
                {
                    Vector2Int c = freeCells[i];
                    if (!IsArrivalCandidate(c, playerCell, taken)) continue;
                    if (inView != null && inView(c)) continue;
                    if (pick-- == 0)
                    {
                        cell = c;
                        return true;
                    }
                }
            }

            int best = -1;
            int bestDistance = -1;
            for (int i = 0; i < freeCells.Count; i++)
            {
                Vector2Int c = freeCells[i];
                if (taken != null && taken.Contains(c)) continue;
                int dx = c.x - playerCell.x, dz = c.y - playerCell.y;
                int d = dx * dx + dz * dz;
                if (d > bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            if (best < 0) return false;
            cell = freeCells[best];
            return true;
        }

        private static bool IsArrivalCandidate(Vector2Int c, Vector2Int playerCell, ICollection<Vector2Int> taken)
        {
            if (taken != null && taken.Contains(c)) return false;
            int dx = c.x - playerCell.x, dz = c.y - playerCell.y;
            return dx * dx + dz * dz >= ArrivalMinCells * ArrivalMinCells;
        }
    }
}
