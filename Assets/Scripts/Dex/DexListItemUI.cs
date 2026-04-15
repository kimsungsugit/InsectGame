using InsectGame.Data;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InsectGame.Dex
{
    public class DexListItemUI : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Text statusText;
        [SerializeField] private TMP_Text nameTextTmp;
        [SerializeField] private TMP_Text statusTextTmp;
        [SerializeField] private Button selectButton;

        private string insectId;

        public void Initialize(InsectData data, bool discovered, System.Action<string> onSelected)
        {
            insectId = data != null ? data.insectId : string.Empty;

            if (nameText != null)
            {
                nameText.text = discovered ? data.displayName : "???";
            }
            if (nameTextTmp != null)
            {
                nameTextTmp.text = discovered ? data.displayName : "???";
            }

            if (statusText != null)
            {
                statusText.text = discovered ? "발견됨" : "미발견";
            }
            if (statusTextTmp != null)
            {
                statusTextTmp.text = discovered ? "발견됨" : "미발견";
            }

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(() => onSelected?.Invoke(insectId));
                selectButton.interactable = discovered;
            }
        }
    }
}
