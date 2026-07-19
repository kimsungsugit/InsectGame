using UnityEngine;

namespace InsectGame.Data
{
    public enum SkillEffectType
    {
        Damage,
        BuffAttack,
        DebuffAttack,
        // ── P4 신규 효과 타입 ──
        Heal,          // 시전자 HP 회복 (effectValue = MaxHp 비율)
        PoisonDot,     // 대상에 지속 피해 부여 (power = 턴당 피해, effectDurationTurns 턴)
        Stun,          // 대상 다음 행동 1회 스킵
        DefenseBuff    // 시전자 방어 상승 (effectValue, effectDurationTurns 턴)
    }

    [CreateAssetMenu(menuName = "InsectGame/Insect Skill", fileName = "InsectSkill")]
    public class InsectSkill : ScriptableObject
    {
        public string skillId;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public InsectElement element;
        public Sprite icon;
        [Tooltip("비어있으면 Resources/SkillIcons/{skillId}.png 로 자동 로드")]
        public string iconResourcePath;
        [Range(1, 999)] public int power = 10;
        [Range(0, 10)] public int cooldownTurns = 2;
        public SkillEffectType effectType = SkillEffectType.Damage;
        [Range(0f, 1f)] public float effectValue = 0.2f;
        [Range(1, 5)] public int effectDurationTurns = 2;
        [Range(0, 999)] public int trainingCost;
        public bool isSignatureSkill;
    }
}
