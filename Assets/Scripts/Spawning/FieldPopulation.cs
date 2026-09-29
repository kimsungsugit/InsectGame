using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Spawning
{
    /// <summary>슬롯이 지금 개체를 들고 있는가.</summary>
    public enum FieldSlotState
    {
        /// <summary>비었다 — <see cref="FieldSlot.RespawnAt"/>가 되면 새 개체를 굴린다.</summary>
        Empty,
        /// <summary>개체가 산다 — 실체(월드의 곤충)가 있든 없든 기록은 그대로다.</summary>
        Alive
    }

    /// <summary>
    /// 필드의 곤충 한 자리. <b>기록이 곧 개체다</b> — 월드에 서 있는 <see cref="InsectEntity"/>는 플레이어가
    /// 가까이 있는 동안만 빌려 쓰는 몸이고, 멀어져 풀로 돌아가도 종·레벨·색다름·자리가 여기 남는다.
    /// 그래서 리전을 떠났다 돌아와도 같은 곤충이 같은 자리에 있다.
    /// </summary>
    public sealed class FieldSlot
    {
        /// <summary>소속 — 메인 필드면 리전 ID, 서브에리어면 <c>InsectSpawner</c>가 붙인 서브에리어 키.</summary>
        public readonly string Key;
        /// <summary>소속 안 번호. 서브에리어 종 순환(<see cref="FieldSpawnRules.SubAreaSpecies"/>)이 쓴다.</summary>
        public readonly int Index;
        public readonly bool IsSubArea;

        public FieldSlotState State = FieldSlotState.Empty;
        public string InsectId;
        /// <summary>종 데이터(조회 캐시). 테스트처럼 DB 없이 기록만 다룰 땐 null이어도 된다.</summary>
        public InsectData Data;
        public int Level;
        public bool Shiny;
        public bool Erased;
        public Vector3 Position;
        /// <summary>비었을 때 다시 굴릴 시각(<c>Time.time</c>).</summary>
        public float RespawnAt;
        /// <summary>살아 있을 때 수명이 다하는 시각. 서브에리어는 무한(전용종이라 시간대와 무관하다).</summary>
        public float ExpiresAt = float.PositiveInfinity;
        /// <summary>이 자리에 몇 번째 개체인가(첫 개체 0). 새로 굴릴 때마다 오른다.</summary>
        public int Generation = -1;
        /// <summary>지금 빌려 쓰는 몸. 없으면 null — Unity 파괴 객체도 null로 읽힌다.</summary>
        public InsectEntity Entity;

        /// <summary>실체화 정렬용 거리 캐시(스포너가 틱마다 쓴다).</summary>
        internal float SortKey;

        public FieldSlot(string key, int index, bool isSubArea)
        {
            Key = key ?? string.Empty;
            Index = index;
            IsSubArea = isSubArea;
        }

        public bool IsAlive => State == FieldSlotState.Alive;
        public bool IsMaterialized => Entity != null;
    }

    /// <summary>
    /// 필드 개체군 기록부 — 순수 C#이라 씬 없이 테스트한다. 슬롯의 상태 전이 규칙도 여기 모았다.
    ///
    /// <b>사라지는 방식이 둘이다.</b> ①게임플레이 퇴장(포획·전투·아이 NPC) — 그 자리는 비고 재생 지연 뒤에
    /// 새로 찬다. ②스포너가 거둠(멀어짐·서브에리어 전환·씬 정리) — 몸만 풀로 돌아가고 개체는 그대로다.
    /// 둘을 섞으면 "멀어졌다 돌아오면 새 곤충"(옛 리롤)으로 되돌아가거나, 잡은 곤충이 영영 안 사라진다.
    /// 필드의 놓침 도주는 셋째 길이다 — 개체는 그대로 두고 자리만 옮긴다(<see cref="RelocateAfterFlee"/>).
    /// </summary>
    public sealed class FieldPopulation
    {
        private readonly Dictionary<string, List<FieldSlot>> slotsByKey = new Dictionary<string, List<FieldSlot>>();
        private readonly List<string> keys = new List<string>();

        public IReadOnlyList<string> Keys => keys;

        public bool Contains(string key) => key != null && slotsByKey.ContainsKey(key);

        /// <summary>그 소속의 슬롯 목록(없으면 빈 목록을 만든다).</summary>
        public List<FieldSlot> SlotsOf(string key)
        {
            key = key ?? string.Empty;
            if (!slotsByKey.TryGetValue(key, out List<FieldSlot> slots))
            {
                slots = new List<FieldSlot>();
                slotsByKey[key] = slots;
                keys.Add(key);
            }
            return slots;
        }

        /// <summary>
        /// 슬롯 수를 <paramref name="desired"/>에 맞춘다. 모자라면 빈 슬롯을 지금 굴릴 차례로 더하고, 남으면
        /// <b>빈 것 → 몸 없는 산 것</b> 순으로 뺀다. 몸이 있는(플레이어 앞에 서 있는) 슬롯은 빼지 않는다 —
        /// 눈앞의 곤충이 이유 없이 사라지면 안 된다. 그 몫은 거둬진 뒤 다음 호출에서 빠진다.
        ///
        /// 오염 거점의 상한(<c>BlightPolicy.MaxActiveFor</c>)이 로그인·클라우드 재적재·정화로 바뀌는 걸 이 한 곳이 받는다.
        /// </summary>
        /// <returns>더한 수(양수) 또는 뺀 수(음수).</returns>
        public int EnsureSlotCount(string key, int desired, float now, bool isSubArea)
        {
            List<FieldSlot> slots = SlotsOf(key);
            desired = Mathf.Max(0, desired);
            int changed = 0;

            while (slots.Count < desired)
            {
                slots.Add(new FieldSlot(key, slots.Count, isSubArea) { State = FieldSlotState.Empty, RespawnAt = now });
                changed++;
            }

            for (int pass = 0; pass < 2 && slots.Count > desired; pass++)
            {
                for (int i = slots.Count - 1; i >= 0 && slots.Count > desired; i--)
                {
                    FieldSlot s = slots[i];
                    if (s.IsMaterialized) continue;
                    if (pass == 0 && s.IsAlive) continue;   // 첫 바퀴는 빈 것만
                    slots.RemoveAt(i);
                    changed--;
                }
            }
            return changed;
        }

        /// <summary>그 소속에 이 종이 살아 있는가(실체 유무와 무관).</summary>
        public bool HasAlive(string key, string insectId)
        {
            if (string.IsNullOrEmpty(insectId) || key == null || !slotsByKey.TryGetValue(key, out List<FieldSlot> slots))
                return false;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].IsAlive && slots[i].InsectId == insectId) return true;
            return false;
        }

        /// <summary>그 소속에 살아 있는 종 ID를 <paramref name="into"/>에 모은다(비우지 않는다).</summary>
        public void CollectAliveIds(string key, HashSet<string> into)
        {
            if (into == null || key == null || !slotsByKey.TryGetValue(key, out List<FieldSlot> slots)) return;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].IsAlive && !string.IsNullOrEmpty(slots[i].InsectId)) into.Add(slots[i].InsectId);
        }

        public void Clear()
        {
            slotsByKey.Clear();
            keys.Clear();
        }

        // ── 슬롯 상태 전이 ──

        /// <summary>새 개체를 들인다. 몸은 붙이지 않는다(실체화는 거리로 따로 정한다).</summary>
        public static void Fill(FieldSlot slot, string insectId, InsectData data, int level, bool shiny, bool erased,
            Vector3 position, float expiresAt)
        {
            slot.State = FieldSlotState.Alive;
            slot.InsectId = insectId;
            slot.Data = data;
            slot.Level = level;
            slot.Shiny = shiny;
            slot.Erased = erased;
            slot.Position = position;
            slot.ExpiresAt = expiresAt;
            slot.Generation++;
        }

        /// <summary>
        /// 게임플레이로 사라졌다(포획·전투·도주·아이 NPC) — 자리가 비고 <paramref name="delay"/>초 뒤에 다시 찬다.
        /// </summary>
        public static void MarkRemovedByGameplay(FieldSlot slot, float now, float delay)
        {
            Vacate(slot, now + Mathf.Max(0f, delay));
        }

        /// <summary>
        /// 스포너가 몸만 거뒀다(멀어짐·서브에리어 전환) — <b>개체는 그대로다.</b> 상태·종·레벨·자리·수명 어느 것도
        /// 건드리지 않는다. 다시 가까워지면 같은 곤충이 같은 자리에 선다.
        /// </summary>
        public static void MarkRecalled(FieldSlot slot)
        {
            slot.Entity = null;
        }

        /// <summary>
        /// 놓침 도주로 달아났다 — <b>개체는 그대로</b>(종·레벨·색다름·수명) 자리만 <paramref name="newPosition"/>으로 옮기고
        /// 몸을 뗀다. 포획·전투처럼 자리를 비우면 플레이어가 대화·전투로 잠깐 서 있기만 해도 주변 곤충이 달아나
        /// 1~2분씩 빈 들판이 남는다(멈춰 서도 인내심이 조금씩 닳는다 — <c>InsectEntity</c> 경계 로직).
        /// </summary>
        public static void RelocateAfterFlee(FieldSlot slot, Vector3 newPosition)
        {
            if (slot.State != FieldSlotState.Alive) return;
            slot.Position = newPosition;
            slot.Entity = null;
        }

        /// <summary>자리를 비운다 — <paramref name="respawnAt"/>에 다시 굴린다. 수명 순환·재시도도 이 길로 온다.</summary>
        public static void Vacate(FieldSlot slot, float respawnAt)
        {
            slot.State = FieldSlotState.Empty;
            slot.InsectId = null;
            slot.Data = null;
            slot.Entity = null;
            slot.ExpiresAt = float.PositiveInfinity;
            slot.RespawnAt = respawnAt;
        }

        /// <summary>빈 자리이고 다시 찰 시각이 됐는가.</summary>
        public static bool IsDueForRoll(FieldSlot slot, float now)
            => slot.State == FieldSlotState.Empty && now >= slot.RespawnAt;

        public static bool IsExpired(FieldSlot slot, float now)
            => slot.State == FieldSlotState.Alive && now >= slot.ExpiresAt;

        /// <summary>
        /// 수명이 다한 개체를 지금 새 개체로 바꿔도 되는가. 몸이 없으면(멀리 있으면) 언제든, 몸이 있으면
        /// 플레이어가 <see cref="FieldSpawnRules.RotateMinPlayerDistance"/> 밖이고 포획·경계·도주 중이 아닐 때만.
        /// </summary>
        public static bool CanRotate(FieldSlot slot, float now, float playerDistance, bool busy)
        {
            if (!IsExpired(slot, now)) return false;
            if (!slot.IsMaterialized) return true;
            return !busy && playerDistance >= FieldSpawnRules.RotateMinPlayerDistance;
        }

        /// <summary>살아 있고 몸이 없는데 플레이어 실체화 반경 안인가.</summary>
        public static bool ShouldMaterialize(FieldSlot slot, float playerDistance)
            => slot.IsAlive && !slot.IsMaterialized && playerDistance <= FieldSpawnRules.MaterializeRadius;

        /// <summary>몸이 있는데 거둠 반경 밖이고 붙잡혀 있지 않은가.</summary>
        public static bool ShouldRecall(FieldSlot slot, float playerDistance, bool busy)
            => slot.IsMaterialized && !busy && playerDistance > FieldSpawnRules.RecallRadius;
    }
}
