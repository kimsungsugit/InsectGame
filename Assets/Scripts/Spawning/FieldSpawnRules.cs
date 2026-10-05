using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Spawning
{
    /// <summary>
    /// 필드 곤충 개체군의 규칙 — <b>순수 계산부</b>. 등급표·등급 대체·레어 부스트·레벨·슬롯 수·재생 시간이
    /// 전부 여기 한 곳에 있다. <see cref="InsectSpawner"/>는 이 답을 월드에 옮길 뿐이다.
    ///
    /// <b>희귀도는 리전과 무관하고 레벨만 리전을 따른다</b>(사용자 결정 2026-09-29). 예전엔 리전 풀의 종 가중치를
    /// 그대로 굴려서 풀의 등급 구성이 곧 그 리전의 등급 분포였다 — 초원은 희귀 이상이 0%, 유적은 일반이 0%에
    /// 희귀 50%·전설 10%였다. 이제 등급을 먼저 전역 표로 굴리고, 종은 그 등급 안에서 리전 풀로 고른다.
    ///
    /// 등급표 다섯 값은 <c>progression_sim.py</c>가 이 파일에서 직접 읽는다(<c>game_facts.field_rarity_shares</c>) —
    /// 값을 바꿔도 시뮬 쪽에 사본이 없다. 이름(<c>…Share</c>)을 바꾸면 추출기가 ExtractorBroken으로 죽는다.
    /// </summary>
    public static class FieldSpawnRules
    {
        // ── 등급표 — 리전과 무관 ──
        //
        // 사용자 확정 값(2026-09-29). 바꿀 땐 이 다섯 줄만 고친다 — 합이 1이 아니어도 된다(정규화한다).
        // 모든 리전 풀이 다섯 등급을 다 갖도록 채우는 게 전제다(RegionDefinitions — game-designer 경계).
        // 아래 대체 규칙(Fallback)은 그 전제가 깨졌을 때(풀 편집 실수·시간대 필터)의 안전망이다.
        public const float CommonShare = 0.60f;
        public const float UncommonShare = 0.25f;
        public const float RareShare = 0.11f;
        public const float EpicShare = 0.035f;
        public const float LegendaryShare = 0.005f;

        internal const int RarityCount = 5;

        /// <summary>등급표의 원래 몫(정규화 전).</summary>
        internal static float BaseShare(int rarity)
        {
            switch ((InsectRarity)rarity)
            {
                case InsectRarity.Common: return CommonShare;
                case InsectRarity.Uncommon: return UncommonShare;
                case InsectRarity.Rare: return RareShare;
                case InsectRarity.Epic: return EpicShare;
                case InsectRarity.Legendary: return LegendaryShare;
                default: return 0f;
            }
        }

        /// <summary>
        /// 레어 부스트(아이템·의상 <c>GetRareSpawnMultiplier</c>)를 얹은 몫. <b>희귀 이상에만 곱한다</b> — 옛
        /// <c>GetWeightedRandomWithRareBoost</c>와 같은 의미다. 재정규화는 호출부가 합으로 나눠서 한다.
        ///
        /// (옛 경로는 부스트가 1을 넘는 순간 등급 기본 가중치 표 — 일반 1 / 고급 0.45 / 희귀 0.12 … — 까지 함께
        /// 곱해서, 배수 1.5짜리 부스터를 켜면 오히려 희귀가 <b>줄었다</b>. 부스트 없는 경로는 그 표를 안 썼다.)
        /// </summary>
        internal static float BoostedShare(int rarity, float rareBoost)
        {
            float share = BaseShare(rarity);
            if (rarity >= (int)InsectRarity.Rare) share *= Mathf.Max(1f, rareBoost);
            return share;
        }

        /// <summary>
        /// 원하는 등급이 후보에 없을 때 대신 쓸 등급. <b>가까운 아래 등급 → 위 등급</b> 순이다.
        /// 후보가 하나도 없으면 −1. 풀이 다섯 등급을 다 갖는 한 쓰이지 않는 안전망이다.
        ///
        /// 아래부터 보는 이유: 빠진 등급을 위로 올려 채우면 희귀가 흔해진다 — 일반·고급뿐인 풀에서
        /// 전설 0.5%는 고급으로 내려와야 등급표의 "귀함"이 유지된다. 위로 가는 건 아래가 통째로
        /// 없을 때(일반이 없는 풀)뿐이다.
        /// </summary>
        internal static int Fallback(int wanted, bool[] available)
        {
            if (available == null) return -1;
            if (wanted >= 0 && wanted < available.Length && available[wanted]) return wanted;
            for (int r = Mathf.Min(wanted, available.Length) - 1; r >= 0; r--)
                if (available[r]) return r;
            for (int r = Mathf.Max(wanted + 1, 0); r < available.Length; r++)
                if (available[r]) return r;
            return -1;
        }

        /// <summary>
        /// 등급 하나를 굴린다 — 부스트를 얹은 표에서 뽑고(<paramref name="roll01"/>) 후보에 없으면 <see cref="Fallback"/>.
        /// <paramref name="available"/>는 등급(enum 순서)별로 "이 리전·지금 시간대에 그 등급 종이 있는가".
        /// </summary>
        internal static int PickRarity(float roll01, float rareBoost, bool[] available)
        {
            float total = 0f;
            for (int r = 0; r < RarityCount; r++) total += BoostedShare(r, rareBoost);
            if (total <= 0f) return Fallback(0, available);

            float target = Mathf.Clamp01(roll01) * total;
            int wanted = RarityCount - 1;
            float acc = 0f;
            for (int r = 0; r < RarityCount; r++)
            {
                acc += BoostedShare(r, rareBoost);
                if (target < acc) { wanted = r; break; }
            }
            return Fallback(wanted, available);
        }

        /// <summary>
        /// 대체까지 반영한 최종 등급 분포(합 1, 후보가 없으면 전부 0). <see cref="PickRarity"/>가 따르는 분포와 같다 —
        /// 테스트와 <c>progression_sim</c>의 리전 수입 모델이 이 형태를 쓴다.
        /// </summary>
        internal static float[] EffectiveShares(float rareBoost, bool[] available)
        {
            var shares = new float[RarityCount];
            float total = 0f;
            for (int r = 0; r < RarityCount; r++) total += BoostedShare(r, rareBoost);
            if (total <= 0f) return shares;
            for (int r = 0; r < RarityCount; r++)
            {
                int to = Fallback(r, available);
                if (to >= 0) shares[to] += BoostedShare(r, rareBoost) / total;
            }
            return shares;
        }

        // ── 레벨 — 리전 대역만 본다 ──

        /// <summary>
        /// 메인 필드 레벨 분포의 지수. 1보다 크면 대역의 낮은 쪽이 더 흔하다 — 1.5면 대역 아래 1/3에 약 48%,
        /// 위 1/3에 약 24%가 떨어진다.
        ///
        /// 옛 규칙은 리전 대역을 버리고 <b>항상 Lv.1부터</b> 지수 3.5 + 등급 보정(전설 +1.5)으로 굴렸다 — 유적(Lv.36~50)에서도
        /// 절반 가까이가 Lv.10 아래였다. 이제 레벨은 리전만으로 정하고 등급은 보지 않는다.
        /// </summary>
        public const float FieldLevelPower = 1.5f;

        /// <summary>리전 대역 [<paramref name="min"/>, <paramref name="max"/>] 안의 레벨. 등급과 무관하다.</summary>
        internal static int RollFieldLevel(int min, int max, float roll01)
        {
            min = Mathf.Max(1, min);
            max = Mathf.Max(min, max);
            float weighted = Mathf.Pow(Mathf.Clamp01(roll01), FieldLevelPower);
            int level = min + Mathf.FloorToInt(weighted * (max - min + 1));
            return Mathf.Clamp(level, min, max);
        }

        // ── 개체군 크기 ──

        /// <summary>
        /// 곤충 한 마리가 차지하는 땅(㎡). 옛 스포너는 리전당 10마리를 플레이어 둘레 10~43m 고리(약 5,500㎡)에
        /// 몰아 두었다 — 그 체감 밀도(약 550㎡당 1마리)를 리전 전체로 편 값이다. 플레이어 45m 안(약 6,360㎡)에
        /// 평균 11~12마리가 선다.
        /// </summary>
        public const float SquareMetersPerInsect = 550f;
        public const int MinRegionSlots = 8;
        public const int MaxRegionSlots = 40;

        /// <summary>
        /// 리전 슬롯 수 — 곤충이 설 수 있는 땅 넓이(<paramref name="usableArea"/>, ㎡) × 밀도. 오염 거점의 감소는
        /// 여기가 아니라 <c>BlightPolicy.MaxActiveFor</c>가 이 값에 얹는다(<c>InsectSpawner.RegionCap</c>).
        /// </summary>
        internal static int SlotCountFor(float usableArea)
        {
            int n = Mathf.RoundToInt(Mathf.Max(0f, usableArea) / SquareMetersPerInsect);
            return Mathf.Clamp(n, MinRegionSlots, MaxRegionSlots);
        }

        // ── 시간·날씨에 따른 개체 수 ──

        /// <summary>밤이면 리전 슬롯이 이만큼 늘어난다 — 어둠 속에서 더 많은 곤충이 움직인다(사용자 요청).</summary>
        public const int NightBonusSlots = 1;

        /// <summary>비·안개면 리전 슬롯이 이만큼 늘어난다 — 습한 날 곤충이 더 나온다. 눈·센바람·맑음은 늘리지 않는다.</summary>
        public const int WetWeatherBonusSlots = 1;

        /// <summary>보너스의 상한 — 밤이면서 비·안개여도 둘이다("한두 개 정도 더").</summary>
        public const int MaxBonusSlots = 2;

        /// <summary>
        /// 이 상태(<b>그 리전에서 보이는</b> 날씨 — <c>WorldStateProvider.GetWorldState(regionId)</c>)에서 리전 슬롯에 더하는 수.
        /// 밤 +1, 비·안개 +1, 합 최대 <see cref="MaxBonusSlots"/>. 오염 상한과 합치는 순서는 <c>InsectSpawner.RegionCap</c>이 정한다
        /// (보너스를 더한 뒤 오염 감소를 곱하므로 황폐한 땅은 보너스도 같이 줄어든다).
        /// </summary>
        public static int BonusSlots(WorldState state)
        {
            int bonus = 0;
            if (state.IsNight) bonus += NightBonusSlots;
            if (state.Weather == WeatherType.Rain || state.Weather == WeatherType.Fog) bonus += WetWeatherBonusSlots;
            return Mathf.Clamp(bonus, 0, MaxBonusSlots);
        }

        // ── 시간대 전환 갈아입기 ──

        /// <summary>
        /// 시간대가 바뀌었을 때, 새 시간대에 어울리지 않는 개체(잠들 시간의 종)의 남은 수명을 이 범위(초)로 당긴다.
        /// 그 뒤의 교체는 평소 순환 그대로다 — 플레이어 <see cref="RotateMinPlayerDistance"/> 안이면 기다리고, 스토리 포획 목표종은 면제다.
        /// 한꺼번에 바꾸지 않고 5~60초에 흩어 필드가 한순간에 갈아엎이지 않게 한다.
        /// </summary>
        public const float PhaseSwapDelayMin = 5f;
        public const float PhaseSwapDelayMax = 60f;

        internal static float RollPhaseSwapDelay(float roll01)
            => Mathf.Lerp(PhaseSwapDelayMin, PhaseSwapDelayMax, Mathf.Clamp01(roll01));

        // ── 종 고르기 — 성향 배수 ──

        /// <summary><c>InsectDatabase.PickWeighted</c>와 같은 가중 하한 — 가챠 전용(0)이 섞여도 0으로 나누지 않는다.</summary>
        public const float MinSpawnWeight = 0.01f;

        /// <summary>
        /// 가중 선택 — <c>InsectDatabase.PickWeighted</c>와 같되 후보마다 <paramref name="multipliers"/>(시간·날씨 성향 배수,
        /// <see cref="InsectHabits.SpawnWeightMultiplier"/>)를 곱한다. <c>InsectData.spawnWeight</c>는 건드리지 않는다.
        /// 배수 목록이 null이거나 짧으면 모자란 쪽은 ×1이다. 배수가 전부 1이면 <c>InsectDatabase.PickWeighted</c>와 같은 답이다
        /// (테스트가 훑어서 고정한다 — 두 곳의 가중식이 갈라지지 않게).
        ///
        /// 등급은 이미 정해진 뒤다 — 이 함수는 <b>한 등급의 후보 안</b>에서만 부른다. 그래서 배수가 등급 분포를 못 건드린다.
        /// </summary>
        internal static InsectData PickWeighted(IReadOnlyList<InsectData> candidates, IReadOnlyList<float> multipliers,
            float roll01)
        {
            if (candidates == null || candidates.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] == null) continue;
                total += CandidateWeight(candidates[i], multipliers, i);
            }

            float roll = Mathf.Clamp01(roll01) * total;
            float cumulative = 0f;
            InsectData last = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                InsectData data = candidates[i];
                if (data == null) continue;
                last = data;
                cumulative += CandidateWeight(data, multipliers, i);
                if (roll <= cumulative) return data;
            }
            return last;
        }

        private static float CandidateWeight(InsectData data, IReadOnlyList<float> multipliers, int index)
        {
            float multiplier = multipliers != null && index < multipliers.Count ? multipliers[index] : 1f;
            return Mathf.Max(MinSpawnWeight, data.spawnWeight) * multiplier;
        }

        // ── 실체화 · 재생 · 순환 ──

        /// <summary>실체화·재생 판정 주기(초). 매 프레임 돌 일이 아니다 — 45m를 0.5초에 달려 들어올 수 없다.</summary>
        public const float TickSeconds = 0.5f;

        /// <summary>이 거리(m) 안의 살아 있는 개체를 월드에 세운다. 게임 카메라(0,9,−6 고각)가 보는 범위보다 넉넉히 밖이다.</summary>
        public const float MaterializeRadius = 45f;

        /// <summary>
        /// 이 거리(m) 밖이면 조용히 풀로 돌린다 — <b>기록은 남는다</b>. 실체화 반경과 10m를 벌려 경계에서 서성일 때
        /// 매 틱 세웠다 거뒀다 하지 않게 한다.
        /// </summary>
        public const float RecallRadius = 55f;

        /// <summary>포획·전투·도주로 빈 자리가 다시 차기까지(초, 무작위). 리전을 오가도 줄어들지 않는다.</summary>
        public const float RespawnDelayMin = 60f;
        public const float RespawnDelayMax = 120f;

        /// <summary>
        /// 살아 있는 개체의 수명(초, 무작위). 다 되면 그 시점의 시간대·날씨로 새 개체가 들어선다 — 게임 하루가
        /// 12분이라 한 시간대(약 3분)가 지날 때마다 필드의 일부가 바뀐다.
        /// </summary>
        public const float LifetimeMin = 240f;
        public const float LifetimeMax = 420f;

        /// <summary>
        /// 수명이 다한 개체를 바꿔도 되는 플레이어와의 거리(m). 그보다 가까우면 기다린다 — 눈앞에서 딴 곤충으로
        /// 바뀌면 "리전을 오가면 리롤"과 다를 게 없다.
        /// </summary>
        public const float RotateMinPlayerDistance = 25f;

        /// <summary>새로 굴린 개체가 플레이어 코앞에 갑자기 생기지 않게 두는 거리(m).</summary>
        public const float SpawnMinPlayerDistance = 20f;

        /// <summary>
        /// 시작 채우기의 거리(m). 장면이 막 떴을 때라 "갑자기 생김"이 없다 — 다만 시작 자리 바로 옆이면
        /// 첫 걸음에 놀라 달아난다.
        /// </summary>
        public const float InitialSpawnMinPlayerDistance = 8f;

        /// <summary>정화 직후 채우기의 거리(m). 돌아온 것이 보여야 하는 사건이라 평소보다 가깝게 둔다.</summary>
        public const float CleanseSpawnMinPlayerDistance = 10f;

        /// <summary>리전 가장자리 울타리 안쪽 여유(m).</summary>
        public const float RegionEdgeMargin = 3f;

        /// <summary>
        /// 서브에리어 게이트 원 바깥 여유(m). 원 안에 서면 잡기 E와 진입 E가 한 프레임에 겹친다 —
        /// 부트스트랩 <c>PushOutOfSubAreas</c>와 같은 마진이다.
        /// </summary>
        public const float SubAreaGateMargin = 9f;

        /// <summary>같은 리전 곤충끼리 최소 간격(m) — 풀 더미가 겹쳐 한 덩어리로 보이지 않게.</summary>
        public const float MinSlotSpacing = 1.5f;

        /// <summary>자리·종을 못 구했을 때 다시 시도하기까지(초).</summary>
        public const float RetrySeconds = 5f;

        internal static float RollRespawnDelay(float roll01)
            => Mathf.Lerp(RespawnDelayMin, RespawnDelayMax, Mathf.Clamp01(roll01));

        internal static float RollLifetime(float roll01)
            => Mathf.Lerp(LifetimeMin, LifetimeMax, Mathf.Clamp01(roll01));

        /// <summary>
        /// 시작 채우기의 수명 — 전부 4~7분짜리로 시작하면 4분쯤에 필드 전체가 한꺼번에 바뀐다. 첫 판은
        /// 1~7분에 흩어 둔다.
        /// </summary>
        internal static float RollInitialLifetime(float roll01)
            => Mathf.Lerp(LifetimeMin * 0.25f, LifetimeMax, Mathf.Clamp01(roll01));

        // ── 서브에리어 ──

        /// <summary>서브에리어 동시 개체 수 — 전용종 수 + 1과 튜닝 상한 중 작은 쪽(옛 규칙 그대로).</summary>
        internal static int SubAreaSlotCount(int exclusiveCount, int activeCount)
        {
            if (exclusiveCount <= 0) return 0;
            return Mathf.Min(exclusiveCount + 1, Mathf.Max(1, activeCount));
        }

        /// <summary>
        /// 서브에리어 슬롯의 종 — 첫 판은 옛 규칙(<c>ids[i % n]</c>)과 같고, 다시 찰 때마다 슬롯 수만큼 건너뛴다.
        ///
        /// 옛 규칙은 슬롯 번호만 봐서 <b>슬롯 수(2)보다 뒤의 전용종이 영영 안 나왔다</b> — 초원 동굴의 공벌레,
        /// 깊은 연못의 왕물방개, 사원의 황금 사마귀 등 9종이 그 서브에리어에서 한 번도 뜨지 않았다.
        /// </summary>
        internal static string SubAreaSpecies(string[] ids, int slotIndex, int slotCount, int generation)
        {
            if (ids == null || ids.Length == 0) return null;
            long k = (long)Mathf.Max(0, slotIndex) + (long)Mathf.Max(0, generation) * Mathf.Max(1, slotCount);
            return ids[(int)(k % ids.Length)];
        }

        // ── 스토리 포획 보조 ──

        /// <summary>
        /// 스토리가 지금 기다리는 종(예: 연못의 왕잠자리)이 그 리전에 살아 있지 않을 때, 새로 굴리는 자리가 그 종이 될 확률.
        ///
        /// 리전 이동 리롤이 사라져서 필요해졌다. 영웅 3.5%를 풀의 영웅 종 수로 나누면 특정 영웅 한 종은 수백 마리에
        /// 한 번이다 — 예전처럼 리전을 오가며 다시 굴릴 수도 없으니 본편이 거기서 멈춘 것처럼 느껴진다.
        /// </summary>
        public const float StoryAssistChance = 0.2f;

        /// <summary>
        /// 스토리 보조로 고를 종. <paramref name="wanted"/> 가운데 이번 후보(<paramref name="candidateIds"/>)에 있고
        /// 그 리전에 살아 있지 않은(<paramref name="aliveIds"/>) 종이 대상이다. <paramref name="chanceRoll"/>이
        /// <see cref="StoryAssistChance"/> 이상이거나 대상이 없으면 null(평소 규칙으로 굴린다).
        /// </summary>
        internal static string PickStoryTarget(IReadOnlyList<string> wanted, ICollection<string> candidateIds,
            ICollection<string> aliveIds, float chanceRoll, float pickRoll)
        {
            if (wanted == null || wanted.Count == 0 || chanceRoll >= StoryAssistChance) return null;

            int eligible = 0;
            for (int i = 0; i < wanted.Count; i++)
                if (IsStoryEligible(wanted[i], candidateIds, aliveIds)) eligible++;
            if (eligible == 0) return null;

            int pick = Mathf.Min(eligible - 1, Mathf.FloorToInt(Mathf.Clamp01(pickRoll) * eligible));
            for (int i = 0; i < wanted.Count; i++)
            {
                if (!IsStoryEligible(wanted[i], candidateIds, aliveIds)) continue;
                if (pick-- == 0) return wanted[i];
            }
            return null;
        }

        private static bool IsStoryEligible(string id, ICollection<string> candidateIds, ICollection<string> aliveIds)
            => !string.IsNullOrEmpty(id)
               && (candidateIds == null || candidateIds.Contains(id))
               && (aliveIds == null || !aliveIds.Contains(id));
    }
}
