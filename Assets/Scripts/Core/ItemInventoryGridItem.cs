using InsectGame.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace InsectGame.Core
{
    public class ItemInventoryGridItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Button button;
        [SerializeField] private Image borderImage;
        [SerializeField] private Image rarityIconImage;
        [SerializeField] private ItemRarityPalette rarityPalette;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseStrength = 0.15f;
        [SerializeField] private ParticleSystem rarityParticles;
        [Header("Rarity Particle Presets")]
        [SerializeField] private ParticleSystem commonPreset;
        [SerializeField] private ParticleSystem uncommonPreset;
        [SerializeField] private ParticleSystem rarePreset;
        [SerializeField] private ParticleSystem epicPreset;
        [SerializeField] private ParticleSystem legendaryPreset;
        [Header("Particle Playback")]
        [SerializeField] private bool playOnHoverOnly = true;
        [SerializeField] private bool playOnStartWhenNotHover = true;
        [SerializeField] private bool enableLongPress = true;
        [SerializeField] private float longPressSeconds = 0.35f;

        private float currentPulseStrength = 0.15f;
        private bool isHovered;
        private bool isPressed;
        private float pressedAt;

        private string itemId;

        public void Bind(ItemData data, int count, System.Action<string> onClick)
        {
            itemId = data != null ? data.itemId : string.Empty;
            if (iconImage != null)
            {
                iconImage.sprite = data != null ? data.icon : null;
                iconImage.enabled = iconImage.sprite != null;
            }

            if (nameText != null)
            {
                nameText.text = data != null ? data.displayName : itemId;
            }

            if (countText != null)
            {
                countText.text = $"x{count}";
            }

            if (borderImage != null)
            {
                borderImage.color = data != null ? GetRarityColor(data.rarity) : new Color(0.7f, 0.7f, 0.7f, 1f);
                borderImage.enabled = data != null;
            }

            if (rarityIconImage != null)
            {
                rarityIconImage.sprite = data != null ? data.rarityIcon : null;
                rarityIconImage.enabled = rarityIconImage.sprite != null;
            }

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick?.Invoke(itemId));
            }

            currentPulseStrength = data != null ? GetPulseStrength(data.rarity) : pulseStrength;
            UpdateParticles(data);
        }

        private void Update()
        {
            if (borderImage == null || !borderImage.enabled)
            {
                return;
            }

            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            Color baseColor = borderImage.color;
            Color pulseColor = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(baseColor.a + currentPulseStrength));
            borderImage.color = Color.Lerp(baseColor, pulseColor, t);

            if (enableLongPress && isPressed && !isHovered)
            {
                if (Time.unscaledTime - pressedAt >= longPressSeconds)
                {
                    isHovered = true;
                    ApplyPlaybackRule(rarityParticles);
                }
            }
        }

        private Color GetRarityColor(ItemRarity rarity)
        {
            if (rarityPalette != null)
            {
                return rarityPalette.GetColor(rarity);
            }

            switch (rarity)
            {
                case ItemRarity.Uncommon:
                    return new Color(0.5f, 0.9f, 0.5f, 1f);
                case ItemRarity.Rare:
                    return new Color(0.4f, 0.7f, 1f, 1f);
                case ItemRarity.Epic:
                    return new Color(0.8f, 0.4f, 1f, 1f);
                case ItemRarity.Legendary:
                    return new Color(1f, 0.75f, 0.2f, 1f);
                default:
                    return new Color(0.8f, 0.8f, 0.8f, 1f);
            }
        }

        private float GetPulseStrength(ItemRarity rarity)
        {
            if (rarityPalette != null)
            {
                return rarityPalette.GetPulseStrength(rarity);
            }

            return pulseStrength;
        }

        private void UpdateParticles(ItemData data)
        {
            if (rarityParticles == null)
            {
                return;
            }

            if (data == null)
            {
                rarityParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

            ParticleSystem preset = GetPreset(data.rarity);
            ParticleSystem target = EnsurePresetInstance(preset);
            if (target == null)
            {
                return;
            }

            Color color = GetRarityColor(data.rarity);
            float intensity = Mathf.Clamp01(GetPulseStrength(data.rarity) * 4f);

            var main = target.main;
            Gradient gradient = GetRarityGradient(data.rarity);
            main.startColor = gradient != null ? new ParticleSystem.MinMaxGradient(gradient) : new ParticleSystem.MinMaxGradient(color);
            main.startSizeMultiplier = Mathf.Lerp(0.6f, 1.3f, intensity);
            main.startSpeedMultiplier = Mathf.Lerp(0.5f, 1.6f, intensity);

            var emission = target.emission;
            emission.rateOverTime = Mathf.Lerp(2f, 18f, intensity);

            ApplyPlaybackRule(target);
        }

        private ParticleSystem GetPreset(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon:
                    return uncommonPreset != null ? uncommonPreset : commonPreset;
                case ItemRarity.Rare:
                    return rarePreset != null ? rarePreset : commonPreset;
                case ItemRarity.Epic:
                    return epicPreset != null ? epicPreset : commonPreset;
                case ItemRarity.Legendary:
                    return legendaryPreset != null ? legendaryPreset : commonPreset;
                default:
                    return commonPreset;
            }
        }

        private ParticleSystem EnsurePresetInstance(ParticleSystem preset)
        {
            if (preset == null)
            {
                return rarityParticles;
            }

            if (rarityParticles != null && rarityParticles.name == preset.name)
            {
                return rarityParticles;
            }

            if (rarityParticles != null)
            {
                Destroy(rarityParticles.gameObject);
            }

            rarityParticles = Instantiate(preset, transform);
            rarityParticles.name = preset.name;
            return rarityParticles;
        }

        private Gradient GetRarityGradient(ItemRarity rarity)
        {
            if (rarityPalette == null)
            {
                return null;
            }

            return rarityPalette.GetGradient(rarity);
        }

        private void ApplyPlaybackRule(ParticleSystem target)
        {
            if (playOnHoverOnly)
            {
                if (isHovered)
                {
                    if (!target.isPlaying)
                    {
                        target.Play();
                    }
                }
                else
                {
                    if (target.isPlaying)
                    {
                        target.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
                return;
            }

            if (playOnStartWhenNotHover && !target.isPlaying)
            {
                target.Play();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            if (rarityParticles != null)
            {
                ApplyPlaybackRule(rarityParticles);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            isPressed = false;
            if (rarityParticles != null)
            {
                ApplyPlaybackRule(rarityParticles);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!enableLongPress)
            {
                return;
            }

            isPressed = true;
            pressedAt = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!enableLongPress)
            {
                return;
            }

            isPressed = false;
            if (isHovered && playOnHoverOnly)
            {
                isHovered = false;
                if (rarityParticles != null)
                {
                    ApplyPlaybackRule(rarityParticles);
                }
            }
        }
    }
}
