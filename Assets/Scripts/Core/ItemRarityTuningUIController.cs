using TMPro;
using UnityEngine;
using UnityEngine.UI;
using InsectGame.Data;

namespace InsectGame.Core
{
    public class ItemRarityTuningUIController : MonoBehaviour
    {
        [SerializeField] private ItemRarityPalette palette;
        [SerializeField] private Slider commonSlider;
        [SerializeField] private Slider uncommonSlider;
        [SerializeField] private Slider rareSlider;
        [SerializeField] private Slider epicSlider;
        [SerializeField] private Slider legendarySlider;
        [SerializeField] private TMP_Text commonLabel;
        [SerializeField] private TMP_Text uncommonLabel;
        [SerializeField] private TMP_Text rareLabel;
        [SerializeField] private TMP_Text epicLabel;
        [SerializeField] private TMP_Text legendaryLabel;

        private void Start()
        {
            if (palette == null)
            {
                return;
            }

            BindSlider(commonSlider, v => palette.commonPulse = v);
            BindSlider(uncommonSlider, v => palette.uncommonPulse = v);
            BindSlider(rareSlider, v => palette.rarePulse = v);
            BindSlider(epicSlider, v => palette.epicPulse = v);
            BindSlider(legendarySlider, v => palette.legendaryPulse = v);

            SetSliderValue(commonSlider, palette.commonPulse);
            SetSliderValue(uncommonSlider, palette.uncommonPulse);
            SetSliderValue(rareSlider, palette.rarePulse);
            SetSliderValue(epicSlider, palette.epicPulse);
            SetSliderValue(legendarySlider, palette.legendaryPulse);

            UpdateLabels();
        }

        private void BindSlider(Slider slider, UnityEngine.Events.UnityAction<float> onChanged)
        {
            if (slider == null)
            {
                return;
            }

            slider.minValue = 0f;
            slider.maxValue = 0.5f;
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(value =>
            {
                onChanged?.Invoke(value);
                UpdateLabels();
            });
        }

        private void SetSliderValue(Slider slider, float value)
        {
            if (slider != null)
            {
                slider.value = value;
            }
        }

        private void UpdateLabels()
        {
            if (commonLabel != null)
            {
                commonLabel.text = $"Common {palette.commonPulse:0.00}";
            }
            if (uncommonLabel != null)
            {
                uncommonLabel.text = $"Uncommon {palette.uncommonPulse:0.00}";
            }
            if (rareLabel != null)
            {
                rareLabel.text = $"Rare {palette.rarePulse:0.00}";
            }
            if (epicLabel != null)
            {
                epicLabel.text = $"Epic {palette.epicPulse:0.00}";
            }
            if (legendaryLabel != null)
            {
                legendaryLabel.text = $"Legendary {palette.legendaryPulse:0.00}";
            }
        }

        public void AutoWire(ItemRarityPalette paletteRef)
        {
            if (palette == null)
            {
                palette = paletteRef;
            }
        }
    }
}
