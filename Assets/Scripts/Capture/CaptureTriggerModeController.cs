using InsectGame.Core;
using UnityEngine;

namespace InsectGame.Capture
{
    public enum CaptureTriggerMode
    {
        Raycast,
        Proximity
    }

    public class CaptureTriggerModeController : MonoBehaviour
    {
        [SerializeField] private CaptureTriggerMode defaultMode = CaptureTriggerMode.Proximity;
        [SerializeField] private CaptureRaycastTrigger raycastTrigger;
        [SerializeField] private CaptureProximityTrigger proximityTrigger;

        private CaptureTriggerMode currentMode;

        private void Awake()
        {
            int saved = PlayerPrefs.GetInt(GameConstants.PrefsKeys.CaptureTriggerMode, (int)defaultMode);
            CaptureTriggerMode mode = (CaptureTriggerMode)Mathf.Clamp(saved, 0, System.Enum.GetValues(typeof(CaptureTriggerMode)).Length - 1);
            ApplyMode(mode);
        }

        public void ApplyMode(CaptureTriggerMode mode)
        {
            currentMode = mode;

            if (raycastTrigger != null)
            {
                raycastTrigger.enabled = mode == CaptureTriggerMode.Raycast;
            }

            if (proximityTrigger != null)
            {
                proximityTrigger.enabled = mode == CaptureTriggerMode.Proximity;
            }
        }

        public void SetMode(int mode)
        {
            CaptureTriggerMode next = (CaptureTriggerMode)Mathf.Clamp(mode, 0, System.Enum.GetValues(typeof(CaptureTriggerMode)).Length - 1);
            ApplyMode(next);
            SaveMode(next);
        }

        public CaptureTriggerMode GetMode()
        {
            return currentMode;
        }

        private void SaveMode(CaptureTriggerMode mode)
        {
            PlayerPrefs.SetInt(GameConstants.PrefsKeys.CaptureTriggerMode, (int)mode);
            PlayerPrefs.Save();
        }

        public void AutoWire(CaptureRaycastTrigger raycast, CaptureProximityTrigger proximity)
        {
            if (raycastTrigger == null)
            {
                raycastTrigger = raycast;
            }

            if (proximityTrigger == null)
            {
                proximityTrigger = proximity;
            }
        }
    }
}
