using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    public enum BgmType
    {
        Explore,
        Battle,
        RaidBattle,
        Victory,
        Defeat,
        Menu
    }

    public enum SfxType
    {
        Attack,
        SkillUse,
        Hit,
        CriticalHit,
        Capture,
        CaptureSuccess,
        CaptureFail,
        LevelUp,
        Evolve,
        ButtonClick,
        MenuOpen,
        MenuClose,
        Victory,
        Defeat,
        BossAppear,
        UniteAttack,
        BuffApply,
        DebuffApply,
        ItemPickup,
        ItemUse,
        Equip,
        SetComplete
    }

    /// <summary>
    /// BGM / SFX / 환경음을 관리하는 씬 단위 싱글톤.
    /// ProceduralAudioGenerator로 AudioClip을 절차적 생성하며 캐시합니다.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private AudioSource bgmSource;
        private AudioSource sfxSource;
        private AudioSource ambientSource;

        private float masterVolume;
        private float bgmVolume;
        private float sfxVolume;

        private BgmType? currentBgmType;
        private Coroutine crossfadeCoroutine;

        private readonly Dictionary<BgmType, AudioClip> bgmCache = new Dictionary<BgmType, AudioClip>();
        private readonly Dictionary<SfxType, AudioClip> sfxCache = new Dictionary<SfxType, AudioClip>();
        private readonly Dictionary<string, AudioClip> ambientCache = new Dictionary<string, AudioClip>();

        private bool initialized;

        private void Awake()
        {
            Instance = this;
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;

            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.loop = true;
            ambientSource.playOnAwake = false;

            masterVolume = PlayerPrefs.GetFloat(
                GameConstants.PrefsKeys.MasterVolume,
                GameConstants.Defaults.MasterVolume);
            sfxVolume = PlayerPrefs.GetFloat(
                GameConstants.PrefsKeys.SfxVolume,
                GameConstants.Defaults.SfxVolume);
            bgmVolume = 0.6f;

            ApplyVolumes();
        }

        // ── BGM ──

        public void PlayBGM(BgmType type)
        {
            EnsureInitialized();
            if (currentBgmType.HasValue && currentBgmType.Value == type && bgmSource.isPlaying)
                return;

            currentBgmType = type;
            StartCoroutine(PlayBGMDeferred(type));
        }

        private System.Collections.IEnumerator PlayBGMDeferred(BgmType type)
        {
            // 클립 생성을 다음 프레임으로 지연하여 프리즈 방지
            yield return null;

            AudioClip clip = GetOrCreateBgmClip(type);
            if (clip == null) yield break;

            if (crossfadeCoroutine != null)
                StopCoroutine(crossfadeCoroutine);

            crossfadeCoroutine = StartCoroutine(CrossfadeBGM(clip, 1f));
        }

        public void StopBGM(float fadeTime = 1f)
        {
            if (crossfadeCoroutine != null)
                StopCoroutine(crossfadeCoroutine);

            crossfadeCoroutine = StartCoroutine(FadeOutBGM(fadeTime));
            currentBgmType = null;
        }

        public void SetBGMVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            ApplyVolumes();
        }

        // ── SFX ──

        public void PlaySFX(SfxType type)
        {
            EnsureInitialized();
            AudioClip clip = GetOrCreateSfxClip(type);
            if (clip == null) return;

            sfxSource.PlayOneShot(clip, masterVolume * sfxVolume);
        }

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(GameConstants.PrefsKeys.SfxVolume, sfxVolume);
            ApplyVolumes();
        }

        // ── 환경음 ──

        public void PlayAmbient(string environmentId)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(environmentId)) return;

            StartCoroutine(PlayAmbientDeferred(environmentId));
        }

        private System.Collections.IEnumerator PlayAmbientDeferred(string environmentId)
        {
            yield return null;

            AudioClip clip = GetOrCreateAmbientClip(environmentId);
            if (clip == null) yield break;

            ambientSource.clip = clip;
            ambientSource.Play();
        }

        public void StopAmbient()
        {
            ambientSource.Stop();
        }

        // ── 마스터 볼륨 ──

        public void SetMasterVolume(float vol)
        {
            masterVolume = Mathf.Clamp01(vol);
            PlayerPrefs.SetFloat(GameConstants.PrefsKeys.MasterVolume, masterVolume);
            ApplyVolumes();
        }

        // ── 내부 ──

        private void ApplyVolumes()
        {
            if (bgmSource != null)
                bgmSource.volume = masterVolume * bgmVolume;
            if (sfxSource != null)
                sfxSource.volume = masterVolume * sfxVolume;
            if (ambientSource != null)
                ambientSource.volume = masterVolume * bgmVolume * 0.4f;
        }

        private IEnumerator CrossfadeBGM(AudioClip newClip, float duration)
        {
            float startVolume = bgmSource.volume;
            float elapsed = 0f;

            // 현재 곡 페이드 아웃
            while (elapsed < duration * 0.5f)
            {
                elapsed += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / (duration * 0.5f));
                yield return null;
            }

            // 새 곡 시작
            bgmSource.Stop();
            bgmSource.clip = newClip;
            bgmSource.Play();

            // 페이드 인
            float targetVolume = masterVolume * bgmVolume;
            elapsed = 0f;
            while (elapsed < duration * 0.5f)
            {
                elapsed += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / (duration * 0.5f));
                yield return null;
            }

            bgmSource.volume = targetVolume;
            crossfadeCoroutine = null;
        }

        private IEnumerator FadeOutBGM(float duration)
        {
            float startVolume = bgmSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
                yield return null;
            }

            bgmSource.Stop();
            bgmSource.volume = 0f;
            crossfadeCoroutine = null;
        }

        // ── 절차적 오디오 클립 생성 (캐시) ──

        private AudioClip GetOrCreateBgmClip(BgmType type)
        {
            if (bgmCache.TryGetValue(type, out AudioClip cached))
                return cached;

            string key = BgmTypeToString(type);
            AudioClip clip = ProceduralAudioGenerator.GetBGM(key);
            if (clip != null)
                bgmCache[type] = clip;
            return clip;
        }

        private AudioClip GetOrCreateSfxClip(SfxType type)
        {
            if (sfxCache.TryGetValue(type, out AudioClip cached))
                return cached;

            string key = SfxTypeToString(type);
            AudioClip clip = ProceduralAudioGenerator.GetSFX(key);
            if (clip != null)
                sfxCache[type] = clip;
            return clip;
        }

        private AudioClip GetOrCreateAmbientClip(string environmentId)
        {
            if (ambientCache.TryGetValue(environmentId, out AudioClip cached))
                return cached;

            AudioClip clip = ProceduralAudioGenerator.GetAmbient(environmentId);
            if (clip != null)
                ambientCache[environmentId] = clip;
            return clip;
        }

        private static string BgmTypeToString(BgmType type)
        {
            switch (type)
            {
                case BgmType.Explore: return "explore";
                case BgmType.Battle: return "battle";
                case BgmType.RaidBattle: return "raid";
                case BgmType.Victory: return "victory";
                case BgmType.Defeat: return "defeat";
                case BgmType.Menu: return "menu";
                default: return "explore";
            }
        }

        private static string SfxTypeToString(SfxType type)
        {
            switch (type)
            {
                case SfxType.Attack: return "attack";
                case SfxType.SkillUse: return "skill_use";
                case SfxType.Hit: return "hit";
                case SfxType.CriticalHit: return "critical";
                case SfxType.Capture: return "capture";
                case SfxType.CaptureSuccess: return "capture_success";
                case SfxType.CaptureFail: return "capture_fail";
                case SfxType.LevelUp: return "level_up";
                case SfxType.Evolve: return "level_up";
                case SfxType.ButtonClick: return "button_click";
                case SfxType.MenuOpen: return "menu_open";
                case SfxType.MenuClose: return "menu_close";
                case SfxType.Victory: return "victory";
                case SfxType.Defeat: return "defeat";
                case SfxType.BossAppear: return "boss_appear";
                case SfxType.UniteAttack: return "unite_attack";
                case SfxType.BuffApply: return "buff";
                case SfxType.DebuffApply: return "debuff";
                case SfxType.ItemPickup: return "item_pickup";
                case SfxType.ItemUse: return "item_use";
                case SfxType.Equip: return "equip";
                case SfxType.SetComplete: return "set_complete";
                default: return "hit";
            }
        }
    }

}
