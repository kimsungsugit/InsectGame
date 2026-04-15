using UnityEngine;
using UnityEngine.UI;

namespace InsectGame.Capture
{
    public class CaptureTriggerOptionsUI : MonoBehaviour
    {
        [SerializeField] private CaptureTriggerModeController modeController;
        [SerializeField] private Dropdown modeDropdown;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Image modeIcon;
        [SerializeField] private Sprite raycastIcon;
        [SerializeField] private Sprite proximityIcon;
        [TextArea(2, 4)] [SerializeField] private string raycastDescription = "중앙 조준으로 곤충을 선택해 포획합니다.";
        [TextArea(2, 4)] [SerializeField] private string proximityDescription = "근처에 있는 곤충을 우선 포획합니다.";
        [Header("Preview")]
        [SerializeField] private Image previewIconImage;
        [SerializeField] private RectTransform previewRangeRing;
        [SerializeField] private float raycastRangeRadius = 80f;
        [SerializeField] private float proximityRangeRadius = 140f;
        [SerializeField] private Color raycastRingColor = new Color(0.2f, 0.7f, 1f, 0.6f);
        [SerializeField] private Color proximityRingColor = new Color(1f, 0.6f, 0.2f, 0.6f);
        [SerializeField] private Image previewRingImage;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseScale = 0.08f;
        [Header("Color Pulse Per Mode")]
        [SerializeField] private float raycastColorPulseSpeed = 2.2f;
        [SerializeField] private float proximityColorPulseSpeed = 1.6f;
        [SerializeField] private float raycastColorPulseStrength = 0.18f;
        [SerializeField] private float proximityColorPulseStrength = 0.28f;

        private CaptureTriggerMode currentMode;

        private void Start()
        {
            if (modeController == null)
            {
                return;
            }

            if (modeDropdown != null)
            {
                modeDropdown.onValueChanged.RemoveListener(HandleChanged);
                modeDropdown.onValueChanged.AddListener(HandleChanged);
                modeDropdown.value = (int)modeController.GetMode();
                modeDropdown.RefreshShownValue();
            }

            UpdateUI(modeController.GetMode());
        }

        private void Update()
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseScale;
            if (previewRangeRing != null)
            {
                previewRangeRing.localScale = Vector3.one * pulse;
            }

            if (previewIconImage != null)
            {
                previewIconImage.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * pulseSpeed) * 4f);
            }

            if (previewRingImage != null)
            {
                float speed = currentMode == CaptureTriggerMode.Raycast ? raycastColorPulseSpeed : proximityColorPulseSpeed;
                float strength = currentMode == CaptureTriggerMode.Raycast ? raycastColorPulseStrength : proximityColorPulseStrength;
                float t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
                Color baseColor = currentMode == CaptureTriggerMode.Raycast ? raycastRingColor : proximityRingColor;
                Color bright = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(baseColor.a + strength));
                previewRingImage.color = Color.Lerp(baseColor, bright, t);
            }
        }

        public void HandleChanged(int mode)
        {
            if (modeController != null)
            {
                modeController.SetMode(mode);
            }

            UpdateUI((CaptureTriggerMode)mode);
        }

        private void UpdateUI(CaptureTriggerMode mode)
        {
            currentMode = mode;
            if (descriptionText != null)
            {
                descriptionText.text = mode == CaptureTriggerMode.Raycast ? raycastDescription : proximityDescription;
            }

            if (modeIcon != null)
            {
                modeIcon.sprite = mode == CaptureTriggerMode.Raycast ? raycastIcon : proximityIcon;
                modeIcon.enabled = modeIcon.sprite != null;
            }

            if (previewIconImage != null)
            {
                previewIconImage.sprite = mode == CaptureTriggerMode.Raycast ? raycastIcon : proximityIcon;
                previewIconImage.enabled = previewIconImage.sprite != null;
            }

            if (previewRangeRing != null)
            {
                float radius = mode == CaptureTriggerMode.Raycast ? raycastRangeRadius : proximityRangeRadius;
                previewRangeRing.sizeDelta = new Vector2(radius, radius);
            }

            if (previewRingImage != null)
            {
                previewRingImage.color = mode == CaptureTriggerMode.Raycast ? raycastRingColor : proximityRingColor;
            }
        }

        public void AutoWire(CaptureTriggerModeController controller)
        {
            if (modeController == null)
            {
                modeController = controller;
            }
        }
    }
}
