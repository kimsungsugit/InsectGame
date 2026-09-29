using System.Collections.Generic;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.Data
{
    [CreateAssetMenu(menuName = "InsectGame/Insect Database", fileName = "InsectDatabase")]
    public class InsectDatabase : ScriptableObject
    {
        public List<InsectData> insects = new List<InsectData>();

        public InsectData GetById(string insectId)
        {
            if (string.IsNullOrEmpty(insectId)) return null;
            foreach (InsectData data in insects)
            {
                if (data != null && data.insectId == insectId)
                    return data;
            }
            return null;
        }

        public List<InsectData> GetCandidates(WorldState state)
        {
            List<InsectData> results = new List<InsectData>();
            foreach (InsectData data in insects)
            {
                if (data != null && data.Matches(state))
                {
                    results.Add(data);
                }
            }
            return results;
        }

        public InsectData GetWeightedRandom(List<InsectData> candidates)
        {
            return PickWeighted(candidates, Random.value);
        }

        /// <summary>
        /// <c>spawnWeight</c> 가중 선택의 <b>단일 출처</b> — <paramref name="roll01"/>(0~1)로 고른다. 난수를 밖에서 받아
        /// 테스트가 분포를 고정할 수 있다. 필드 스폰은 등급을 먼저 굴린 뒤 그 등급 후보 안에서 이걸 부른다
        /// (<c>FieldSpawnRules</c>). 가중치는 0.01 아래로 내리지 않는다 — 가챠 전용(0)이 섞여도 0으로 나누지 않는다.
        ///
        /// (여기 있던 <c>GetWeightedRandomWithRareBoost</c>·<c>GetRarityWeight</c>는 지웠다. 필드 스폰만 부르던 것인데
        /// 부스트가 1을 넘는 순간 등급 기본 가중치 표까지 함께 곱해 희귀가 오히려 줄었다 — 레어 부스트는 이제
        /// <c>FieldSpawnRules.BoostedShare</c>가 등급표에 건다.)
        /// </summary>
        public static InsectData PickWeighted(IReadOnlyList<InsectData> candidates, float roll01)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            float total = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                // null 가드 — 외부에서 직접 candidates 전달 시 NRE 차단(GetCandidates 외 경로).
                InsectData data = candidates[i];
                if (data == null) continue;
                total += Mathf.Max(0.01f, data.spawnWeight);
            }

            float roll = Mathf.Clamp01(roll01) * total;
            float cumulative = 0f;
            InsectData last = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                InsectData data = candidates[i];
                if (data == null) continue;
                last = data;
                cumulative += Mathf.Max(0.01f, data.spawnWeight);
                if (roll <= cumulative)
                {
                    return data;
                }
            }

            return last;
        }
    }
}
