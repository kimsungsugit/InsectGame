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

            // **비용은 컬렉션에서 받는다**(`CollectionUI`가 쓰는 정본 API와 같은 것).
            // 예전엔 여기서 `insect.levelCurve`를 직접 보고 null이면 0으로 뒀는데, 이 프로젝트엔
            // `InsectLevelCurve` 에셋이 **하나도 없고** `InsectData`는 런타임 생성이라 `levelCurve`에
            // 대입하는 코드도 없다 — 즉 늘 null이라 화면은 항상 "사탕 0"이었다. 반면 결제 쪽
            // `GetCandyCostForLevel`은 폴백 곡선(4 + (Lv-1)×2)으로 **실제로 캔디를 뺐다.**
            // 재화 화면에서 표시와 차감이 갈린 셈이다.
            int cost = collection.GetCandyCostForLevel(current.insectId, current.level);
            int maxLv = collection.GetMaxLevel(current.insectId);
            bool atMax = current.level >= maxLv;

            // 만렙에서도 비용을 띄우고 버튼을 살려 두면, 눌렀을 때 `TryLevelUpWithCandy`가
            // 만렙 가드로 false를 내는데 UI는 그 false를 무조건 "사탕 부족"으로 보여준다 —
            // 캔디가 충분한 사람이 부족한 줄 알고 더 사게 된다.
            SetTexts(name, $"Lv {current.level}", atMax ? "MAX" : $"사탕 {cost}");
            if (levelUpButton != null) levelUpButton.interactable = !atMax;
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
