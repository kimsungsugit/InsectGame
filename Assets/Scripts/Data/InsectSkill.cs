using UnityEngine;

namespace InsectGame.Data
{
    public enum SkillEffectType
    {
        Damage,
        BuffAttack,
        DebuffAttack
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
    }
}
