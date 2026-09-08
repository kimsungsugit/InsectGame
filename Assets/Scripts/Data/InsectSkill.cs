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
        [Range(0f, 1f)] public float accuracy = 1f;   // 명중률(1=항상 명중). 저명중 스킬은 빗나갈 수 있음.
        [Range(0, 999)] public int trainingCost;
        public bool isSignatureSkill;

        /// <summary>
        /// 이 기술을 배울 수 있는 최소 <b>곤충 레벨</b>. 훈련·기술 디스크 양쪽에 걸린다.
        ///
        /// 예전엔 요구 레벨이 <c>TrainingMethod</c> 단위뿐이었다. 그래서 "극한 훈련"이
        /// <b>곤충 Lv6에 위력 55·65·75를 한꺼번에</b> 열었고, Lv6 야생 HP가 62~73인데
        /// 데미지가 87이라 전투가 <b>한 방에 끝났다</b>. 방식은 "무엇을 배우는 곳인가"만 정하고
        /// 실제 게이트는 여기가 맡는다.
        ///
        /// 종족 기술(learnset)은 <c>InsectLearnableSkill.learnLevel</c>이 이미 같은 일을 하므로
        /// 그쪽 경로에서는 보지 않는다 — 두 값이 갈리면 어느 쪽이 옳은지 알 수 없어진다.
        /// </summary>
        [Range(1, 100)] public int requiredLevel = 1;
    }
}
