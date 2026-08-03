using InsectGame.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InsectGame.Core
{
    public class PlayerInsectLevelUpUIController : MonoBehaviour
    {
        [SerializeField] private PlayerInsectCollection collection;
        [SerializeField] private TMP_Text insectNameText;
        [SerializeField] private TMP_Text insectLevelText;
        [SerializeField] private TMP_Text candyCostText;
        [SerializeField] private Button levelUpButton;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Text insectNameTextLegacy;
        [SerializeField] private Text insectLevelTextLegacy;
        [SerializeField] private Text candyCostTextLegacy;
        [SerializeField] private Text resultTextLegacy;
        [SerializeField] private string successMessage = "레벨 업!";
        [SerializeField] private string failMessage = "사탕 부족";

        private PlayerInsectData current;
        private string selectedInstanceId;
        private bool subscribed; // OnEnable/AutoWire 이중 구독 차단

        private void Start()
        {
            if (levelUpButton != null)
            {
                levelUpButton.onClick.RemoveAllListeners();
                levelUpButton.onClick.AddListener(LevelUpCurrent);
            }

            Refresh();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed || collection == null) return;
            collection.InsectUpdated += HandleInsectUpdated;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || collection == null) return;
            collection.InsectUpdated -= HandleInsectUpdated;
            subscribed = false;
        }

        private void HandleInsectUpdated(PlayerInsectData data)
        {
            Refresh();
        }

        public void Refresh()
        {
            if (collection == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(selectedInstanceId))
            {
                current = collection.GetByInstanceId(selectedInstanceId);
            }
            else if (!collection.TryGetAnyOwned(out current))
            {
                SetTexts("-", "-", "-");
                return;
            }

            // selectedInstanceId가 stale(곤충 삭제/강화 후 변경)이면 GetByInstanceId가 null 반환 → NRE 차단.
            if (current == null)
            {
                SetTexts("-", "-", "-");
                return;
            }

            InsectData insect = collection.GetInsectData(current.insectId);
            // #코드 미표시 — 의미 없는 GUID 조각이라 이름만 보여준다.
            string name = insect != null ? insect.displayName : current.insectId;
            int cost = 0;
            if (insect != null)
            {
                InsectLevelCurve curve = insect.levelCurve;
                if (curve != null)
                {
                    cost = curve.GetCandyCost(current.level);
                }
            }

            SetTexts(name, $"Lv {current.level}", $"사탕 {cost}");
        }

        public void SetSelectedInsect(string instanceId)
        {
            selectedInstanceId = instanceId;
            Refresh();
        }

        public void LevelUpCurrent()
        {
            if (collection == null || current == null)
            {
                return;
            }

            bool success = collection.TryLevelUpWithCandyByInstance(current.instanceId);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(success ? SfxType.LevelUpGain : SfxType.Error);
            if (resultText != null)
            {
                resultText.text = success ? successMessage : failMessage;
            }
            if (resultTextLegacy != null)
            {
                resultTextLegacy.text = success ? successMessage : failMessage;
            }

            Refresh();
        }

        private void SetTexts(string name, string level, string cost)
        {
            if (insectNameText != null)
            {
                insectNameText.text = name;
            }
            if (insectNameTextLegacy != null)
            {
                insectNameTextLegacy.text = name;
            }

            if (insectLevelText != null)
            {
                insectLevelText.text = level;
            }
            if (insectLevelTextLegacy != null)
            {
                insectLevelTextLegacy.text = level;
            }

            if (candyCostText != null)
            {
                candyCostText.text = cost;
            }
            if (candyCostTextLegacy != null)
            {
                candyCostTextLegacy.text = cost;
            }
        }

        public void AutoWire(PlayerInsectCollection playerCollection)
        {
            if (collection == playerCollection) return;
            Unsubscribe();
            collection = playerCollection;
            // 컴포넌트가 enable 상태일 때만 구독 — OnDisable 동안 구독 잔존 차단.
            if (isActiveAndEnabled) Subscribe();
        }

    }
}
