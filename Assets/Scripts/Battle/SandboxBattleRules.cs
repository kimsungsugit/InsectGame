using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>
    /// 샌드박스 전투(「챔피언의 꿈」)의 <b>순수</b> 규칙 — 피해 배율과 길이.
    ///
    /// 이 전투는 처음 켠 사람에게 "이 게임의 전투가 이렇게 시원하다"를 보여 주는 자리라 두 가지를 보장한다.
    /// ①<b>반드시 이긴다</b>(내 곤충은 쓰러지지 않는다) ②<b>3~4턴 안에 끝난다</b>(너무 길면 지루하고 한 턴에
    /// 끝나면 스킬을 눌러 볼 틈이 없다). 둘 다 확률이 아니라 규칙으로 지킨다 — 데미지 공식은 곤충 종·레벨에 따라
    /// 실제 전투 길이가 크게 달라서(balance.md 「전투 길이 3종」), 수치를 맞춰 놓고 믿으면 DB가 바뀔 때 깨진다.
    ///
    /// 실제 데미지 공식은 건드리지 않는다 — 공식이 낸 값에 <b>배율과 상·하한만</b> 건다.
    /// </summary>
    public static class SandboxBattleRules
    {
        /// <summary>내 곤충이 주는 피해 배율 — 챔피언다운 시원한 숫자.</summary>
        public const float PlayerDamageScale = 2.5f;

        /// <summary>적이 주는 피해 배율 — 맞기는 하되 위협이 되지 않는다.</summary>
        public const float EnemyDamageScale = 0.4f;

        /// <summary>내 곤충 HP가 이 비율 아래로는 내려가지 않는다.</summary>
        public const float PlayerHpFloorRatio = 0.30f;

        /// <summary>이 번째 행동 전에는 적이 쓰러지지 않는다 — 스킬을 몇 개는 눌러 보게 한다.</summary>
        public const int MinPlayerActions = 3;

        /// <summary>이 번째 행동에서 반드시 끝난다.</summary>
        public const int MaxPlayerActions = 4;

        /// <summary>
        /// 내가 때릴 때의 최종 피해.
        /// </summary>
        /// <param name="damage">공식이 낸 피해(방어 비율까지 반영된 값).</param>
        /// <param name="enemyCurrentHp">맞기 전 적 HP.</param>
        /// <param name="actionNumber">이번이 내 몇 번째 행동인가(1부터).</param>
        public static int PlayerDamage(int damage, int enemyCurrentHp, int actionNumber)
        {
            int scaled = Mathf.Max(1, Mathf.RoundToInt(damage * PlayerDamageScale));
            if (enemyCurrentHp <= 0) return 0;
            if (actionNumber >= MaxPlayerActions) return Mathf.Max(scaled, enemyCurrentHp);   // 마지막 일격
            if (actionNumber < MinPlayerActions) return Mathf.Max(0, Mathf.Min(scaled, enemyCurrentHp - 1));
            return scaled;
        }

        /// <summary>
        /// 적이 때릴 때의 최종 피해 — 배율을 걸고, 내 HP가 하한 밑으로 내려가지 않게 자른다.
        /// 0이면 아무 피해도 주지 않는다(호출부가 적용을 건너뛴다).
        /// </summary>
        public static int EnemyDamage(int damage, int playerCurrentHp, int playerMaxHp)
        {
            int scaled = Mathf.Max(1, Mathf.RoundToInt(damage * EnemyDamageScale));
            return Mathf.Clamp(playerCurrentHp - HpFloor(playerMaxHp), 0, scaled);
        }

        /// <summary>내 곤충 HP의 하한(최대 HP의 <see cref="PlayerHpFloorRatio"/>).</summary>
        public static int HpFloor(int playerMaxHp)
        {
            return Mathf.Max(1, Mathf.CeilToInt(playerMaxHp * PlayerHpFloorRatio));
        }
    }
}
