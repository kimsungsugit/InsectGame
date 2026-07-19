using UnityEngine;

namespace InsectGame.Data
{
    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    [CreateAssetMenu(menuName = "InsectGame/Item Data", fileName = "ItemData")]
    public class ItemData : ScriptableObject
    {
        public string itemId;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;
        public ItemRarity rarity = ItemRarity.Common;
        public Sprite rarityIcon;

        [Header("Capture Effects")]
        [Range(0f, 0.5f)] public float captureChanceBonus = 0.1f;
        [Range(1f, 3f)] public float expMultiplier = 1.0f;
        [Range(1f, 3f)] public float candyMultiplier = 1.0f;
        [Range(1f, 3f)] public float rareSpawnMultiplier = 1.0f;

        [Header("Battle & Flee Effects")]
        // 아주 좋은(Epic/Legendary) 아이템 전용 강화 — 활성 중 전투 공격/방어 배율 가산 + 야생 곤충 도주 방지 확률.
        [Range(0f, 1f)] public float atkBonus = 0f;          // 전투 공격력 배율 가산 (GetDamage: 1+AttackBonus)
        [Range(0f, 1f)] public float defBonus = 0f;          // 유효 방어 배율 가산 (ApplyDamage: def*(1+DefenseBonus))
        [Range(0f, 1f)] public float fleePreventChance = 0f; // 야생 곤충 도주 방지 확률 (patience 소진 시 롤)

        [Header("Duration")]
        [Range(30f, 3600f)] public float durationSeconds = 600f;

        [Header("Treatment (대상지정 치료 아이템)")]
        // isTargetedUse=true면 부스터가 아니라 곤충을 지정해 즉시 사용(병원 선택기 경유). HP 회복·상태 해제.
        public bool isTargetedUse = false;
        [Range(0, 9999)] public int healAmount = 0;   // HP 회복량(0=회복 없음). 9999=전액 취급.
        public bool curePoison = false;
        public bool cureParalysis = false;
    }
}
