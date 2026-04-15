using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InsectGame.Dex
{
    public class DexListUIPresetController : MonoBehaviour
    {
        [SerializeField] private DexListUIController listController;

        [Header("Sort Buttons (0:이름, 1:등급, 2:포획수)")]
        [SerializeField] private Button[] sortButtons;
        [SerializeField] private GameObject[] sortIndicators;
        [SerializeField] private Text sortStatusText;
        [SerializeField] private TMP_Text sortStatusTextTmp;
        [SerializeField] private string[] sortStatusLabels = { "이름순", "등급순", "포획수순" };
        [SerializeField] private Color sortStatusColor = new Color(0.2f, 0.7f, 1f, 1f);
        [SerializeField] private Image sortStatusIconImage;
        [SerializeField] private Sprite[] sortStatusIcons;

        [Header("Filter Buttons (0:전체, 1:발견, 2:미발견)")]
        [SerializeField] private Button[] filterButtons;
        [SerializeField] private GameObject[] filterIndicators;
        [SerializeField] private Text filterStatusText;
        [SerializeField] private TMP_Text filterStatusTextTmp;
        [SerializeField] private string[] filterStatusLabels = { "전체", "발견", "미발견" };
        [SerializeField] private Color filterStatusColor = new Color(1f, 0.75f, 0.2f, 1f);
        [SerializeField] private Image filterStatusIconImage;
        [SerializeField] private Sprite[] filterStatusIcons;

        private void Start()
        {
            HookButtons();
            ApplySavedSelection();
        }

        private void OnEnable()
        {
            ApplySavedSelection();
        }

        public void SelectSort(int mode)
        {
            if (listController == null)
            {
                return;
            }

            listController.SetSortMode(mode);
            SetIndicators(sortIndicators, mode);
            UpdateStatusText(sortStatusText, sortStatusTextTmp, sortStatusIconImage, sortStatusLabels, sortStatusIcons, sortStatusColor, mode);
        }

        public void SelectFilter(int mode)
        {
            if (listController == null)
            {
                return;
            }

            listController.SetFilterMode(mode);
            SetIndicators(filterIndicators, mode);
            UpdateStatusText(filterStatusText, filterStatusTextTmp, filterStatusIconImage, filterStatusLabels, filterStatusIcons, filterStatusColor, mode);
        }

        private void HookButtons()
        {
            if (sortButtons != null)
            {
                for (int i = 0; i < sortButtons.Length; i++)
                {
                    int index = i;
                    if (sortButtons[i] != null)
                    {
                        sortButtons[i].onClick.RemoveAllListeners();
                        sortButtons[i].onClick.AddListener(() => SelectSort(index));
                    }
                }
            }

            if (filterButtons != null)
            {
                for (int i = 0; i < filterButtons.Length; i++)
                {
                    int index = i;
                    if (filterButtons[i] != null)
                    {
                        filterButtons[i].onClick.RemoveAllListeners();
                        filterButtons[i].onClick.AddListener(() => SelectFilter(index));
                    }
                }
            }
        }

        private void SyncIndicators()
        {
            if (listController == null)
            {
                return;
            }

            int sortIndex = (int)listController.GetSortMode();
            int filterIndex = (int)listController.GetFilterMode();

            SetIndicators(sortIndicators, sortIndex);
            SetIndicators(filterIndicators, filterIndex);
            UpdateStatusText(sortStatusText, sortStatusTextTmp, sortStatusIconImage, sortStatusLabels, sortStatusIcons, sortStatusColor, sortIndex);
            UpdateStatusText(filterStatusText, filterStatusTextTmp, filterStatusIconImage, filterStatusLabels, filterStatusIcons, filterStatusColor, filterIndex);
        }

        public void RefreshFromController()
        {
            ApplySavedSelection();
        }

        private void ApplySavedSelection()
        {
            if (listController == null)
            {
                return;
            }

            int sortIndex = (int)listController.GetSortMode();
            int filterIndex = (int)listController.GetFilterMode();

            SelectSort(sortIndex);
            SelectFilter(filterIndex);
        }

        private void SetIndicators(GameObject[] indicators, int activeIndex)
        {
            if (indicators == null)
            {
                return;
            }

            for (int i = 0; i < indicators.Length; i++)
            {
                if (indicators[i] != null)
                {
                    indicators[i].SetActive(i == activeIndex);
                }
            }
        }

        private void UpdateStatusText(Text text, TMP_Text textTmp, Image iconImage, string[] labels, Sprite[] icons, Color color, int index)
        {
            if ((text == null && textTmp == null) || labels == null || labels.Length == 0)
            {
                return;
            }

            int clamped = Mathf.Clamp(index, 0, labels.Length - 1);
            string label = labels[clamped];
            if (text != null)
            {
                text.text = label;
                text.color = color;
            }

            if (textTmp != null)
            {
                textTmp.text = label;
                textTmp.color = color;
            }

            if (iconImage != null)
            {
                Sprite icon = icons != null && icons.Length > 0 ? icons[Mathf.Clamp(clamped, 0, icons.Length - 1)] : null;
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
                iconImage.color = color;
            }
        }
    }
}
