using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 곤충 개체의 몸길이·무게 계산. 종 기준값(<see cref="InsectData.baseSizeMm"/>)에
    /// 개체 롤(<see cref="PlayerInsectData.sizeRoll"/>)을 곱한다.
    ///
    /// 전투에는 관여하지 않는다 — IV가 전투 축이라면 이쪽은 수집·주간 대결 축이다.
    /// 순수 정적이라 씬 없이 테스트된다.
    /// </summary>
    public static class InsectSizeCalculator
    {
        public const int MinRoll = 0;
        public const int MaxRoll = 100;

        // 종 기준 대비 몸길이 배율 범위. ±25%면 눈으로 "크다/작다"가 분명하면서
        // 종 사이의 구분(사슴벌레 vs 개미)을 흐리지 않는다.
        public const float MinScale = 0.75f;
        public const float MaxScale = 1.25f;

        /// <summary>롤(0~100) → 몸길이 배율(0.75~1.25). 범위 밖 값은 clamp한다.</summary>
        public static float ScaleFor(int sizeRoll)
        {
            float t = Mathf.Clamp01((float)(sizeRoll - MinRoll) / (MaxRoll - MinRoll));
            return Mathf.Lerp(MinScale, MaxScale, t);
        }

        /// <summary>
        /// 저장된 롤. <c>-1</c>(구세이브 미초기화)이면 instanceId 해시로 되살린다 —
        /// 결정적이라 같은 개체는 언제 봐도 같은 크기다.
        /// </summary>
        public static int EffectiveRoll(PlayerInsectData pid)
        {
            if (pid == null) return MaxRoll / 2;
            if (pid.sizeRoll >= MinRoll) return Mathf.Min(pid.sizeRoll, MaxRoll);
            return RollFromInstanceId(pid.instanceId);
        }

        /// <summary>
        /// instanceId → 0~100 롤. FNV-1a를 쓴다 — <c>string.GetHashCode</c>는 런타임마다
        /// 값이 달라 재실행할 때마다 크기가 바뀐다(NpcDialogueDatabase.StableHash와 같은 이유).
        /// </summary>
        public static int RollFromInstanceId(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return MaxRoll / 2;

            unchecked
            {
                int hash = (int)2166136261;
                for (int i = 0; i < instanceId.Length; i++)
                    hash = (hash ^ instanceId[i]) * 16777619;
                // 음수 방어: Abs(int.MinValue)는 그 자신이라 오버플로한다.
                long positive = hash < 0 ? -(long)hash : hash;
                return (int)(positive % (MaxRoll + 1));
            }
        }

        /// <summary>이 개체의 몸길이(mm). 데이터가 없으면 0.</summary>
        public static float SizeMm(InsectData data, PlayerInsectData pid)
        {
            if (data == null) return 0f;
            return Mathf.Max(0.1f, data.baseSizeMm) * ScaleFor(EffectiveRoll(pid));
        }

        /// <summary>
        /// 이 개체의 무게(g). <b>길이 배율의 세제곱</b>에 비례한다 — 부피가 길이³로 커지므로,
        /// 그래야 "조금 큰데 훨씬 묵직하다"가 자연스럽다(선형이면 크기 차이가 밋밋해진다).
        /// </summary>
        public static float WeightG(InsectData data, PlayerInsectData pid)
        {
            if (data == null) return 0f;
            float scale = ScaleFor(EffectiveRoll(pid));
            return Mathf.Max(0.001f, data.baseWeightG) * scale * scale * scale;
        }

        /// <summary>종 기준 대비 몸길이 비율. 주간 대결 티어 판정이 이 값을 쓴다.</summary>
        public static float SizeRatio(InsectData data, PlayerInsectData pid)
        {
            if (data == null || data.baseSizeMm <= 0f) return 1f;
            return SizeMm(data, pid) / data.baseSizeMm;
        }

        public static string SizeLabel(float mm)
        {
            return mm >= 100f ? mm.ToString("0") + "mm" : mm.ToString("0.0") + "mm";
        }

        public static string WeightLabel(float grams)
        {
            if (grams >= 1000f) return (grams / 1000f).ToString("0.00") + "kg";
            return grams >= 10f ? grams.ToString("0.0") + "g" : grams.ToString("0.00") + "g";
        }

        /// <summary>"71.8mm · 4.20g" 한 줄 요약.</summary>
        public static string Summary(InsectData data, PlayerInsectData pid)
        {
            if (data == null) return string.Empty;
            return SizeLabel(SizeMm(data, pid)) + " · " + WeightLabel(WeightG(data, pid));
        }
    }
}
