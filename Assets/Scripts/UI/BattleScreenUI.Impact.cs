using InsectGame.Battle;
using InsectGame.Data;

namespace InsectGame.UI
{
    /// <summary>
    /// 1v1 연출에 넘길 타격 정보(<see cref="BattleArenaController.HitCue"/>)를 만든다.
    /// 전투는 스킬 버튼을 누른 그 프레임에 동기로 해결되고 <c>OnBattleUpdated</c>가 피해량을 먼저
    /// 적어 두므로, 연출을 시작하는 시점엔 결과가 이미 나와 있다 — 연출은 그걸 얼마나 세게
    /// 보여줄지만 고른다.
    /// </summary>
    public partial class BattleScreenUI
    {
        private BattleArenaController.HitCue BuildHitCue(bool playerAction, string skillName, SkillEffectType effectType)
        {
            InsectBattleStats target = playerAction ? enemyStats : playerStats;
            int damage = playerAction ? lastDamageToEnemy : lastDamageToPlayer;
            int maxHp = target != null ? target.MaxHp : 0;
            // CRITICAL 표시와 같은 판정선(최대 HP의 25%) — 화면 글자와 연출 세기가 따로 놀지 않게.
            bool critical = damage > 0 && maxHp > 0 && damage >= maxHp * 0.25f;
            int hpAfter = battleController == null ? 1
                : playerAction ? battleController.EnemyHpAfterPlayerAction : battleController.PlayerHpAfterEnemyAction;
            bool finisher = damage > 0 && hpAfter <= 0;
            bool missed = effectType == SkillEffectType.Damage && damage <= 0;
            return new BattleArenaController.HitCue(skillName,
                BattleArenaController.HitCue.WeightFor(damage, maxHp, critical), finisher, missed);
        }
    }
}
