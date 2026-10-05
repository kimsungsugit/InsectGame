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
            // 컨트롤러가 굴린 진짜 치명타(LastPlayerHitCritical/LastEnemyHitCritical) — CRITICAL 글자와 같은 값이라
            // 화면 글자와 연출 세기가 따로 놀지 않는다. 예전엔 "최대 HP의 25% 이상"을 치명타로 쳐서 거의 매 타격이 무거웠다.
            bool critical = damage > 0 && (playerAction ? lastWasCritical : lastEnemyWasCritical);
            int hpAfter = battleController == null ? 1
                : playerAction ? battleController.EnemyHpAfterPlayerAction : battleController.PlayerHpAfterEnemyAction;
            bool finisher = damage > 0 && hpAfter <= 0;
            bool missed = effectType == SkillEffectType.Damage && damage <= 0;
            // 치명타 여부는 세기(평타 0.25~0.5 / 치명타 0.85~1)와 별개로 함께 넘긴다 — 아레나가 치명타만의 연출
            // (별 빛살·CriticalHit 소리·큰 비명)을 세기 문턱이 아니라 이 판정으로 고른다.
            // 전용기도 컨트롤러가 적어 둔 표지(SignatureSkills — 레이드와 같은 기준)를 그대로 옮긴다. 빗나가도 켜진다.
            bool signature = battleController != null
                && (playerAction ? battleController.LastPlayerSkillIsSignature : battleController.LastEnemySkillIsSignature);
            return new BattleArenaController.HitCue(skillName,
                BattleArenaController.HitCue.WeightFor(damage, maxHp, critical), finisher, missed, critical, signature);
        }
    }
}
