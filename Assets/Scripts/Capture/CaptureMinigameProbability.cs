using UnityEngine;

namespace InsectGame.Capture
{
    /// <summary>3단계 미니게임 결과를 일반 포획 공식의 타이밍값과 추가 보너스로 변환한다.</summary>
    internal static class CaptureMinigameProbability
    {
        internal const int MaxComboHits = 3;
        internal const float BonusPerHit = 0.05f;

        internal static float GetTiming01(int comboHits)
        {
            switch (ClampComboHits(comboHits))
            {
                case 3: return 0.5f;
                case 2: return 0.45f;
                case 1: return 0.3f;
                default: return 0.1f;
            }
        }

        internal static float GetComboBonus(int comboHits)
        {
            return ClampComboHits(comboHits) * BonusPerHit;
        }

        internal static float GetExtraBonus(int comboHits, float captureItemBonus)
        {
            return GetComboBonus(comboHits) + Mathf.Max(0f, captureItemBonus);
        }

        private static int ClampComboHits(int comboHits)
        {
            return Mathf.Clamp(comboHits, 0, MaxComboHits);
        }
    }
}
