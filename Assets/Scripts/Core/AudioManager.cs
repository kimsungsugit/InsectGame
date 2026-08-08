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
        Menu,
        // 리전별 탐험 BGM
        ExploreMeadow,
        ExploreForest,
        ExplorePond,
        ExploreSwamp,
        ExploreMountain,
        ExploreGarden,
        ExploreRuins,
        // 2막(ver2) 리전 BGM — BgmTypeToString / RegionIdToBgmType /
        // ProceduralAudioGenerator.GetBGM 세 곳을 함께 등록해야 소리가 난다.
        ExploreHollow,
        ExploreDunes,
        ExploreFrostline,
        ExploreEmberfall,
        ExploreCanopy,
        ExploreNameless,
        // 2막 보스 테마 — 명부회 간부(집게·저울)와 최종전(관장 하월·무명).
        // 위 리전 BGM과 똑같이 3지점(BgmTypeToString / 아래 전환 호출부 /
        // ProceduralAudioGenerator.GetBGM)을 함께 등록해야 소리가 난다.
        BossLedger,
        BossFinal
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
        SetComplete,
        // 신규
        Footstep,
        LevelUpGain,
        MenuHover,
        Purchase,
        Error
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
        private AudioSource ambientSource2; // 크로스페이드용

        private float masterVolume;
        private float bgmVolume;
        private float sfxVolume;

        private BgmType? currentBgmType;
        private Coroutine crossfadeCoroutine;
        private Coroutine ambientCrossfadeCoroutine;

        private float battleIntensity; // 0~1, HP 낮을수록 높음
        private string currentAmbientId;
        private bool useSecondaryAmbient;

        private GameClock gameClock;
        private float dayNightCheckTimer;
        private bool inSubArea; // 서브에리어 진입 시 GameClock 폴링 차단

        private readonly Dictionary<BgmType, AudioClip> bgmCache = new Dictionary<BgmType, AudioClip>();
        private readonly Dictionary<SfxType, AudioClip> sfxCache = new Dictionary<SfxType, AudioClip>();
        private readonly Dictionary<string, AudioClip> ambientCache = new Dictionary<string, AudioClip>();

        private bool initialized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            EnsureInitialized();
        }

        // 모바일 백그라운드 진입 시 BGM/Ambient 음소거 (배터리 절감 + UX)
        private void OnApplicationPause(bool pauseStatus)
        {
            if (bgmSource != null) bgmSource.mute = pauseStatus;
            if (ambientSource != null) ambientSource.mute = pauseStatus;
            if (ambientSource2 != null) ambientSource2.mute = pauseStatus;
        }

        // 다른 앱에서 복귀 시 음소거 해제
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) return;
            if (bgmSource != null) bgmSource.mute = false;
            if (ambientSource != null) ambientSource.mute = false;
            if (ambientSource2 != null) ambientSource2.mute = false;
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

            ambientSource2 = gameObject.AddComponent<AudioSource>();
            ambientSource2.loop = true;
            ambientSource2.playOnAwake = false;

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

        private Coroutine deferredBgmCoroutine;

        public void PlayBGM(BgmType type)
        {
            EnsureInitialized();
            if (currentBgmType.HasValue && currentBgmType.Value == type && bgmSource.isPlaying)
                return;

            currentBgmType = type;
            // 이전 deferred 코루틴이 진행 중이면 중단 (BGM 변경 race 방지)
            if (deferredBgmCoroutine != null)
                StopCoroutine(deferredBgmCoroutine);
            deferredBgmCoroutine = StartCoroutine(PlayBGMDeferred(type));
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
            deferredBgmCoroutine = null;
        }

        public void StopBGM(float fadeTime = 1f)
        {
            // deferred BGM 진행 중이면 함께 중단 — 옛은 PlayBGM 직후 StopBGM 시
            // deferredBgm이 다음 프레임에 CrossfadeBGM 시작하여 StopBGM 의도 깨짐.
            if (deferredBgmCoroutine != null)
            {
                StopCoroutine(deferredBgmCoroutine);
                deferredBgmCoroutine = null;
            }

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
            if (currentAmbientId == environmentId) return;

            currentAmbientId = environmentId;
            if (ambientCrossfadeCoroutine != null) StopCoroutine(ambientCrossfadeCoroutine);
            ambientCrossfadeCoroutine = StartCoroutine(CrossfadeAmbient(environmentId, 0.8f));
        }

        private System.Collections.IEnumerator CrossfadeAmbient(string environmentId, float duration)
        {
            yield return null;
            AudioClip clip = GetOrCreateAmbientClip(environmentId);
            if (clip == null) yield break;

            // 새 클립을 보조 소스에 로드
            AudioSource newSrc = useSecondaryAmbient ? ambientSource : ambientSource2;
            AudioSource oldSrc = useSecondaryAmbient ? ambientSource2 : ambientSource;
            useSecondaryAmbient = !useSecondaryAmbient;

            float targetVol = masterVolume * bgmVolume * 0.4f;
            newSrc.clip = clip;
            newSrc.volume = 0f;
            newSrc.Play();

            float startOldVol = oldSrc.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                newSrc.volume = Mathf.Lerp(0f, targetVol, t);
                oldSrc.volume = Mathf.Lerp(startOldVol, 0f, t);
                yield return null;
            }

            newSrc.volume = targetVol;
            oldSrc.Stop();
            oldSrc.volume = 0f;
            ambientCrossfadeCoroutine = null;
        }

        public void StopAmbient()
        {
            ambientSource.Stop();
            if (ambientSource2 != null) ambientSource2.Stop();
            currentAmbientId = null;
        }

        /// <summary>배틀 인텐시티 (0~1). HP 낮을수록 1에 가까워짐 → BGM pitch/volume 상승.</summary>
        public void SetBattleIntensity(float intensity01)
        {
            battleIntensity = Mathf.Clamp01(intensity01);
        }

        public void ClearBattleIntensity()
        {
            battleIntensity = 0f;
            if (bgmSource != null) bgmSource.pitch = 1f;
        }

        private void Update()
        {
            if (bgmSource == null) return;

            // 배틀 BGM 인텐시티
            if (bgmSource.isPlaying)
            {
                if (currentBgmType == BgmType.Battle || currentBgmType == BgmType.RaidBattle)
                {
                    float targetPitch = Mathf.Lerp(1.0f, 1.10f, battleIntensity);
                    bgmSource.pitch = Mathf.MoveTowards(bgmSource.pitch, targetPitch, Time.deltaTime * 0.3f);
                }
                else
                {
                    bgmSource.pitch = Mathf.MoveTowards(bgmSource.pitch, 1f, Time.deltaTime * 0.5f);
                }
            }

            // 낮/밤 환경음 자동 전환 (1초마다 체크, 서브에리어 진입 중에는 스킵)
            dayNightCheckTimer += Time.deltaTime;
            if (dayNightCheckTimer >= 1f && !inSubArea)
            {
                dayNightCheckTimer = 0f;
                if (gameClock == null) gameClock = FindFirstObjectByType<GameClock>();
                if (gameClock != null)
                {
                    DayPhase phase = gameClock.GetDayPhase();
                    string targetAmbient = (phase == DayPhase.Night || phase == DayPhase.Evening) ? "night" : "day";
                    if (currentAmbientId != targetAmbient)
                        PlayAmbient(targetAmbient);
                }
            }
        }

        public void SetSubAreaActive(bool active)
        {
            bool wasInSubArea = inSubArea;
            inSubArea = active;

            // SubArea 이탈 시: 환경음을 day/night으로 즉시 복원 (그렇지 않으면 cave 등 ambient가 계속 재생됨)
            if (wasInSubArea && !active)
            {
                if (gameClock == null) gameClock = FindFirstObjectByType<GameClock>();
                if (gameClock != null)
                {
                    DayPhase phase = gameClock.GetDayPhase();
                    string targetAmbient = (phase == DayPhase.Night || phase == DayPhase.Evening) ? "night" : "day";
                    PlayAmbient(targetAmbient);
                }
                else
                {
                    StopAmbient();
                }
                // 다음 dayNightCheck가 즉시 동작하도록 타이머 강제 트리거
                dayNightCheckTimer = 1f;
            }
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
            if (ambientSource2 != null)
                ambientSource2.volume = masterVolume * bgmVolume * 0.4f;
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
                case BgmType.ExploreMeadow: return "explore_meadow";
                case BgmType.ExploreForest: return "explore_forest";
                case BgmType.ExplorePond: return "explore_pond";
                case BgmType.ExploreSwamp: return "explore_swamp";
                case BgmType.ExploreMountain: return "explore_mountain";
                case BgmType.ExploreGarden: return "explore_garden";
                case BgmType.ExploreRuins: return "explore_ruins";
                case BgmType.ExploreHollow: return "explore_hollow";
                case BgmType.ExploreDunes: return "explore_dunes";
                case BgmType.ExploreFrostline: return "explore_frostline";
                case BgmType.ExploreEmberfall: return "explore_emberfall";
                case BgmType.ExploreCanopy: return "explore_canopy";
                case BgmType.ExploreNameless: return "explore_nameless";
                case BgmType.BossLedger: return "boss_ledger";
                case BgmType.BossFinal: return "boss_final";
                default: return "explore";
            }
        }

        // 마지막으로 재생한 리전 탐험 BGM. 전투·레이드가 끝난 뒤 되돌릴 곡이다.
        private BgmType lastExploreBgm = BgmType.Explore;

        public void PlayBGMForRegion(string regionId)
        {
            BgmType type = RegionIdToBgmType(regionId);
            lastExploreBgm = type;
            PlayBGM(type);
        }

        /// <summary>
        /// 전투·레이드·보스전이 끝난 뒤 탐험 BGM으로 복귀. <b>있던 리전의 곡으로</b> 돌아간다.
        ///
        /// 예전엔 세 호출부가 전부 <c>PlayBGM(BgmType.Explore)</c>였다 — 리전 곡 13개를 만들어
        /// 놓고 첫 전투가 끝나는 순간 범용 곡으로 떨어져서, 리전을 다시 갈아타기 전까지
        /// 그 상태로 남았다. 어느 곡으로 돌아갈지는 오디오 시스템이 알아야 할 일이라
        /// 호출부마다 RegionManager를 뒤지게 하지 않고 여기서 기억한다.
        /// </summary>
        public void RestoreExploreBGM()
        {
            PlayBGM(lastExploreBgm);
        }

        private static BgmType RegionIdToBgmType(string regionId)
        {
            switch (regionId)
            {
                case "meadow": return BgmType.ExploreMeadow;
                case "forest": return BgmType.ExploreForest;
                case "pond": return BgmType.ExplorePond;
                case "swamp": return BgmType.ExploreSwamp;
                case "mountain": return BgmType.ExploreMountain;
                case "garden": return BgmType.ExploreGarden;
                case "ruins": return BgmType.ExploreRuins;
                case "hollow": return BgmType.ExploreHollow;
                case "dunes": return BgmType.ExploreDunes;
                case "frostline": return BgmType.ExploreFrostline;
                case "emberfall": return BgmType.ExploreEmberfall;
                case "canopy": return BgmType.ExploreCanopy;
                case "nameless": return BgmType.ExploreNameless;
                default: return BgmType.Explore;
            }
        }

        public void PlaySkillSFX(InsectGame.Data.InsectElement element)
        {
            EnsureInitialized();
            string key = "skill_" + ElementToString(element);
            AudioClip clip = ProceduralAudioGenerator.GetSFX(key);
            if (clip == null) clip = GetOrCreateSfxClip(SfxType.SkillUse);
            if (clip != null)
                sfxSource.PlayOneShot(clip, masterVolume * sfxVolume);
        }

        private static string ElementToString(InsectGame.Data.InsectElement element)
        {
            switch (element)
            {
                case InsectGame.Data.InsectElement.Bug: return "bug";
                case InsectGame.Data.InsectElement.Poison: return "poison";
                case InsectGame.Data.InsectElement.Water: return "water";
                case InsectGame.Data.InsectElement.Leaf: return "leaf";
                case InsectGame.Data.InsectElement.Wind: return "wind";
                case InsectGame.Data.InsectElement.Electric: return "electric";
                case InsectGame.Data.InsectElement.Earth: return "earth";
                case InsectGame.Data.InsectElement.Light: return "light";
                case InsectGame.Data.InsectElement.Dark: return "dark";
                case InsectGame.Data.InsectElement.Metal: return "metal";
                default: return "bug";
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
                case SfxType.Footstep: return "footstep";
                case SfxType.LevelUpGain: return "level_up_gain";
                case SfxType.MenuHover: return "menu_hover";
                case SfxType.Purchase: return "purchase";
                case SfxType.Error: return "error";
                default: return "hit";
            }
        }
    }

}
