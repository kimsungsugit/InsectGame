using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>
    /// 전투에서 도망칠 확률의 <b>순수 규칙</b> — 50%에서 레벨 차 1마다 5%씩 오르내리고 10%~90%로 가둔다.
    /// 1v1 전투의 <c>InsectBattleController.TryEscape</c>와 습격 곤충에게서 도망치기가 같은 답을 읽는다(두 곳이 따로 계산하면 어긋난다).
    /// </summary>
    public static class BattleEscapeRules
    {
        public const float BaseChance = 0.5f;
        public const float ChancePerLevel = 0.05f;
        public const float MinChance = 0.1f;
        public const float MaxChance = 0.9f;

        /// <summary>
        /// 도망칠 확률(0.1~0.9). 레벨 차는 <b>내 곤충 − 상대</b>다 — 내가 높으면 잘 도망친다.
        /// </summary>
        public static float Chance(int playerLevel, int enemyLevel)
        {
            int levelDiff = playerLevel - enemyLevel;
            return Mathf.Clamp(BaseChance + levelDiff * ChancePerLevel, MinChance, MaxChance);
        }
    }
}
