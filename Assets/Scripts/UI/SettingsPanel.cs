using InsectGame.Core;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using System.Linq;

namespace InsectGame.UI
{
    /// <summary>
    /// 설정 패널. 볼륨과 그래픽 품질을 PlayerPrefs에 저장/로드합니다.
    /// OnEnable에서 로드, OnDisable에서 저장합니다.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Dropdown graphicsDropdown;
        [SerializeField] private Button closeButton;

        [Header("Audio")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private string masterVolumeParam = "MasterVolume";
        [SerializeField] private string sfxVolumeParam = "SfxVolume";

        private void OnEnable()
        {
            SetupGraphicsDropdown();
            LoadSettings();

            if (masterVolumeSlider != null)
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            if (graphicsDropdown != null)
                graphicsDropdown.onValueChanged.AddListener(OnGraphicsChanged);
            if (closeButton != null)
                closeButton.onClick.AddListener(OnClose);
        }

        private void OnDisable()
        {
            if (masterVolumeSlider != null)
                masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            if (graphicsDropdown != null)
                graphicsDropdown.onValueChanged.RemoveListener(OnGraphicsChanged);
            if (closeButton != null)
                closeButton.onClick.RemoveListener(OnClose);

            SaveSettings();
        }

        private void SetupGraphicsDropdown()
        {
            if (graphicsDropdown == null) return;
            graphicsDropdown.ClearOptions();
            graphicsDropdown.AddOptions(QualitySettings.names.ToList());
        }

        private void LoadSettings()
        {
            float masterVol = PlayerPrefs.GetFloat(
                GameConstants.PrefsKeys.MasterVolume,
                GameConstants.Defaults.MasterVolume);
            float sfxVol = PlayerPrefs.GetFloat(
                GameConstants.PrefsKeys.SfxVolume,
                GameConstants.Defaults.SfxVolume);
            int quality = PlayerPrefs.GetInt(
                GameConstants.PrefsKeys.GraphicsQuality,
                GameConstants.Defaults.GraphicsQuality);

            quality = Mathf.Clamp(quality, 0, QualitySettings.names.Length - 1);

            if (masterVolumeSlider != null) masterVolumeSlider.value = masterVol;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfxVol;
            if (graphicsDropdown != null) graphicsDropdown.value = quality;

            ApplyVolume(masterVolumeParam, masterVol);
            ApplyVolume(sfxVolumeParam, sfxVol);
            QualitySettings.SetQualityLevel(quality, true);
        }

        private void SaveSettings()
        {
            if (masterVolumeSlider != null)
                PlayerPrefs.SetFloat(GameConstants.PrefsKeys.MasterVolume, masterVolumeSlider.value);
            if (sfxVolumeSlider != null)
                PlayerPrefs.SetFloat(GameConstants.PrefsKeys.SfxVolume, sfxVolumeSlider.value);
            if (graphicsDropdown != null)
                PlayerPrefs.SetInt(GameConstants.PrefsKeys.GraphicsQuality, graphicsDropdown.value);
            PlayerPrefs.Save();
        }

        private void OnMasterVolumeChanged(float value)
        {
            ApplyVolume(masterVolumeParam, value);
        }

        private void OnSfxVolumeChanged(float value)
        {
            ApplyVolume(sfxVolumeParam, value);
        }

        private void OnGraphicsChanged(int index)
        {
            QualitySettings.SetQualityLevel(index, true);
        }

        private void ApplyVolume(string paramName, float linearValue)
        {
            if (audioMixer == null) return;
            // 선형(0~1) → 데시벨(dB) 변환
            float db = Mathf.Log10(Mathf.Max(linearValue, 0.0001f)) * 20f;
            audioMixer.SetFloat(paramName, db);
        }

        private void OnClose()
        {
            gameObject.SetActive(false);
        }
    }
}
