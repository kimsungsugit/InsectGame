using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Dex
{
    public class RarityIconProvider : MonoBehaviour
    {
        [SerializeField] private Sprite common;
        [SerializeField] private Sprite uncommon;
        [SerializeField] private Sprite rare;
        [SerializeField] private Sprite epic;
        [SerializeField] private Sprite legendary;

        public Sprite GetIcon(InsectRarity rarity)
        {
            switch (rarity)
            {
                case InsectRarity.Common:
                    return common;
                case InsectRarity.Uncommon:
                    return uncommon;
                case InsectRarity.Rare:
                    return rare;
                case InsectRarity.Epic:
                    return epic;
                case InsectRarity.Legendary:
                    return legendary;
                default:
                    return null;
            }
        }
    }
}
