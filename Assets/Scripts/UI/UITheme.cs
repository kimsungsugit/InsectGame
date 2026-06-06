using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    [CreateAssetMenu(menuName = "InsectGame/UI Theme", fileName = "UITheme")]
    public class UITheme : ScriptableObject
    {
        private static UITheme instance;
        public static UITheme Instance
        {
            get
            {
                if (instance == null)
                    instance = Resources.Load<UITheme>("UITheme");
                if (instance == null)
                    instance = CreateInstance<UITheme>();
                return instance;
            }
        }

        [Header("Panel")]
        public Color panelBg = new Color(0.08f, 0.1f, 0.2f, 0.92f);
        public Color panelHeaderBg = new Color(0.12f, 0.14f, 0.25f, 1f);
        public Color dimOverlay = new Color(0f, 0f, 0f, 0.6f);

        [Header("Tab")]
        public Color tabNormal = new Color(0.25f, 0.28f, 0.35f, 1f);
        public Color tabSelected = new Color(0.3f, 0.5f, 0.9f, 1f);

        [Header("Button")]
        public Color btnPrimary = new Color(0.2f, 0.5f, 0.2f, 1f);
        public Color btnSecondary = new Color(0.25f, 0.3f, 0.45f, 1f);
        public Color btnDanger = new Color(0.7f, 0.15f, 0.15f, 1f);
        public Color btnDisabled = new Color(0.25f, 0.25f, 0.25f, 1f);

        [Header("Text")]
        public Color titleColor = Color.white;
        public Color labelColor = Color.white;
        public Color coinColor = new Color(1f, 0.84f, 0f, 1f);
        public Color accentColor = new Color(0.3f, 0.6f, 1f, 1f);
        public Color bonusColor = new Color(0.4f, 0.9f, 0.4f, 1f);

        [Header("Insect Rarity")]
        public Color insectCommon = new Color(0.55f, 0.45f, 0.3f);
        public Color insectUncommon = new Color(0.3f, 0.8f, 0.3f);
        public Color insectRare = new Color(0.3f, 0.5f, 0.95f);
        public Color insectEpic = new Color(0.75f, 0.3f, 0.95f);
        public Color insectLegendary = new Color(1f, 0.8f, 0.2f);

        [Header("Item Rarity")]
        public Color itemCommon = new Color(0.8f, 0.8f, 0.8f);
        public Color itemUncommon = new Color(0.5f, 0.9f, 0.5f);
        public Color itemRare = new Color(0.4f, 0.7f, 1f);
        public Color itemEpic = new Color(0.8f, 0.4f, 1f);
        public Color itemLegendary = new Color(1f, 0.75f, 0.2f);

        [Header("IV Grade")]
        public Color gradeS = new Color(1f, 0.8f, 0.2f);
        public Color gradeA = new Color(0.75f, 0.3f, 0.95f);
        public Color gradeB = new Color(0.3f, 0.5f, 0.95f);
        public Color gradeC = new Color(0.3f, 0.8f, 0.3f);
        public Color gradeD = new Color(0.6f, 0.6f, 0.6f);

        public Color GetInsectRarityColor(InsectRarity rarity)
        {
            switch (rarity)
            {
                case InsectRarity.Uncommon: return insectUncommon;
                case InsectRarity.Rare: return insectRare;
                case InsectRarity.Epic: return insectEpic;
                case InsectRarity.Legendary: return insectLegendary;
                default: return insectCommon;
            }
        }

        public Color GetItemRarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return itemUncommon;
                case ItemRarity.Rare: return itemRare;
                case ItemRarity.Epic: return itemEpic;
                case ItemRarity.Legendary: return itemLegendary;
                default: return itemCommon;
            }
        }

        public Color GetRarityColor(int tierIndex)
        {
            switch (tierIndex)
            {
                case 1: return insectUncommon;
                case 2: return insectRare;
                case 3: return insectEpic;
                case 4: return insectLegendary;
                default: return insectCommon;
            }
        }

        public Color GetInsectColor(string insectId, InsectRarity rarity)
        {
            if (string.IsNullOrEmpty(insectId)) return GetInsectRarityColor(rarity);

            uint hash = 0;
            for (int i = 0; i < insectId.Length; i++)
                hash = hash * 31u + (uint)insectId[i];

            float hue = (hash % 360u) / 360f;
            float sat = 0.55f + (int)rarity * 0.06f;
            float val = 0.7f + (int)rarity * 0.06f;

            return Color.HSVToRGB(hue, Mathf.Clamp01(sat), Mathf.Clamp01(val));
        }

        public Color GetGradeColor(IVGrade grade)
        {
            switch (grade)
            {
                case IVGrade.S: return gradeS;
                case IVGrade.A: return gradeA;
                case IVGrade.B: return gradeB;
                case IVGrade.C: return gradeC;
                default: return gradeD;
            }
        }
    }
}
