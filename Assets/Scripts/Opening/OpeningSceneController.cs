using System;
using InsectGame.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InsectGame.Opening
{
    public enum OpeningPlaybackResult
    {
        Completed,
        Skipped,
        Error,
        Destroyed
    }

    /// <summary>
    /// OpeningScene의 코드 기반 렌더러. standalone에서는 최초 cold start를 처리하고,
    /// additive에서는 <see cref="StartReplay"/>가 호출될 때까지 아무것도 시작하지 않는다.
    /// </summary>
    public class OpeningSceneController : MonoBehaviour
    {
        private const string GlowPath = "UI/Opening/opening_01_";
        private const string HorizonPath = "UI/Opening/opening_02_";
        private const string GatheringPath = "UI/Opening/opening_03_";
        private const string FallbackIconPath = "UI/insect-game-icon";
        private const string ThemePath = "Audio/Opening/opening_theme";
        private const string EnglishTitleText = "INSECT EXPLORATION";
        private const string KoreanTitleText = "곤충탐험";
        private const string SubtitleText = "발견하고, 성장시키고, 함께 모험하세요";

        private static readonly string[] ImagePaths =
        {
            GlowPath,
            HorizonPath,
            GatheringPath
        };

        // GUIContent는 struct가 아니라 class다 — CalcSize에 매번 새로 넘기면
        // 타이틀 구간 내내 OnGUI 패스마다 GC 할당이 발생한다. 문구가 상수라 1회면 충분하다.
        private static readonly GUIContent EnglishTitleContent = new GUIContent(EnglishTitleText);
        private static readonly GUIContent KoreanTitleContent = new GUIContent(KoreanTitleText);
        private static readonly GUIContent SubtitleContent = new GUIContent(SubtitleText);

        private readonly Texture2D[] openingImages = new Texture2D[3];
        private readonly OpeningPlaybackClock playbackClock = new OpeningPlaybackClock();
        private readonly OpeningSkipInputGate skipInputGate = new OpeningSkipInputGate();

        private OpeningSequenceState sequence;
        private AudioSource openingAudioSource;
        private AudioClip openingTheme;
        private Texture2D fallbackIcon;
        private bool orientationLoaded;
        private bool loadedPortrait;
        private bool applicationPaused;
        private bool audioWasPlayingBeforePause;
        private bool manualReplay;
        private bool resultReported;
        private bool loadingPlayScene;
        private string loadError;
        private float cachedMasterVolume = 1f;
        private float titleFitMinSide = -1f;
        private float titleFitAvailableWidth = -1f;
        private int fittedEnglishSize;
        private int fittedKoreanSize;
        private int fittedSubtitleSize;
        private GUIStyle titleStyle;
        private GUIStyle koreanTitleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle skipButtonStyle;
        private GUIStyle errorStyle;
        private GUIStyle retryStyle;

        public event Action<OpeningPlaybackResult> PlaybackEnded;

        public bool IsPlaying => sequence != null && !sequence.IsCompleted;
        public string LastError => loadError;

        private void Awake()
        {
            openingAudioSource = gameObject.AddComponent<AudioSource>();
            openingAudioSource.playOnAwake = false;
            openingAudioSource.loop = false;
            openingAudioSource.spatialBlend = 0f;
            openingAudioSource.ignoreListenerPause = true;
        }

        private void Start()
        {
            // Additive 로드는 PlayScene이 계속 active이므로 자동 시작하지 않는다.
            if (SceneManager.sceneCount == 1
                && gameObject.scene.IsValid()
                && SceneManager.GetActiveScene() == gameObject.scene)
                StartColdOpening();
        }

        private void Update()
        {
            if (sequence == null || sequence.IsCompleted || applicationPaused)
                return;

            bool portrait = Screen.height > Screen.width;
            if (!orientationLoaded || portrait != loadedPortrait)
                ReloadOrientationResourcesDuringPlayback(portrait);

            float playbackDelta = playbackClock.Consume(Time.realtimeSinceStartupAsDouble);
            sequence.Advance(playbackDelta);
            SynchronizeOpeningAudio();
            UpdateAudioVolume();

            if (sequence.IsCompleted)
                return;

            SkipInputSnapshot input = ReadSkipInput();
            if (skipInputGate.ShouldSkip(sequence.CanSkip, input.IsHeld, input.Began)
                && sequence.TrySkip())
            {
                Debug.Log(
                    $"[OpeningSceneController] 새 스킵 입력 수락: {input.Source}, "
                    + $"elapsed={sequence.Elapsed:F2}s");
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            applicationPaused = pauseStatus;
            playbackClock.Reset(Time.realtimeSinceStartupAsDouble);
            skipInputGate.Reset();
            if (openingAudioSource == null)
                return;

            if (pauseStatus)
            {
                audioWasPlayingBeforePause = openingAudioSource.isPlaying;
                if (audioWasPlayingBeforePause) openingAudioSource.Pause();
            }
            else if (audioWasPlayingBeforePause && sequence != null && !sequence.IsCompleted)
            {
                openingAudioSource.UnPause();
                audioWasPlayingBeforePause = false;
            }
        }

        private void OnDestroy()
        {
            if (sequence != null)
                sequence.Completed -= OnSequenceCompleted;

            // 클립을 언로드하기 전에 재생을 멈춘다. replay가 중단(Destroyed)되면
            // OnSequenceCompleted가 돌지 않아 AudioSource가 아직 openingTheme을 물고 있다.
            StopOpeningAudio();
            UnloadOrientationResources();
            // fallback icon은 다른 UI도 쓰는 공유 Resources 자산이라 강제 unload하지 않는다.
            fallbackIcon = null;
            if (openingTheme != null) Resources.UnloadAsset(openingTheme);

            if (manualReplay && sequence != null && !sequence.IsCompleted && !resultReported)
                ReportReplayResult(OpeningPlaybackResult.Destroyed);
        }

        /// <summary>additive replay 전용 진입점. PlayerPrefs 소비 키는 변경하지 않는다.</summary>
        public bool StartReplay()
        {
            if (sequence != null && !sequence.IsCompleted)
                return false;

            OpeningAutoPlayPolicy policy = new OpeningAutoPlayPolicy();
            if (!policy.TryBegin(OpeningPlaybackRequest.ManualReplay))
                return false;

            manualReplay = true;
            resultReported = false;
            return BeginPlayback();
        }

        public bool TrySkip()
        {
            return sequence != null && sequence.TrySkip();
        }

        private void StartColdOpening()
        {
            manualReplay = false;
            OpeningAutoPlayPolicy policy = new OpeningAutoPlayPolicy();
            if (!policy.TryBegin(OpeningPlaybackRequest.ColdStart))
            {
                SetLoadError("오프닝 재생 정책이 시작을 거부했습니다.");
                return;
            }

            if (!BeginPlayback())
                SetLoadError("오프닝을 시작하지 못했습니다. 게임으로 이동하려면 재시도하세요.");
        }

        private bool BeginPlayback()
        {
            try
            {
                loadError = null;
                sequence = new OpeningSequenceState();
                sequence.Completed += OnSequenceCompleted;
                LoadOrientationResources(Screen.height > Screen.width);
                RefreshMasterVolume();
                StartOpeningAudio();
                // Resources.Load에 걸린 시간은 시네마틱 타임라인에 포함하지 않는다.
                playbackClock.Reset(Time.realtimeSinceStartupAsDouble);
                skipInputGate.Reset();
                Debug.Log("[OpeningSceneController] 10초 오프닝 재생을 시작합니다.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[OpeningSceneController] 오프닝 시작 실패: {e.Message}");
                if (sequence != null) sequence.Completed -= OnSequenceCompleted;
                sequence = null;
                StopOpeningAudio();
                if (manualReplay) ReportReplayResult(OpeningPlaybackResult.Error);
                return false;
            }
        }

        private void OnSequenceCompleted()
        {
            StopOpeningAudio();
            if (manualReplay)
            {
                OpeningPlaybackResult result = sequence.WasSkipped
                    ? OpeningPlaybackResult.Skipped
                    : OpeningPlaybackResult.Completed;
                ReportReplayResult(result);
            }
            else
            {
                resultReported = true;
                TryLoadPlayScene();
            }
        }

        private void ReportReplayResult(OpeningPlaybackResult result)
        {
            if (resultReported)
                return;

            resultReported = true;
            Action<OpeningPlaybackResult> handler = PlaybackEnded;
            if (handler != null) handler(result);
        }

        private void StartOpeningAudio()
        {
            if (openingAudioSource == null)
                return;

            openingTheme = Resources.Load<AudioClip>(ThemePath);
            if (openingTheme == null)
                return;

            openingAudioSource.clip = openingTheme;
            UpdateAudioVolume();
            openingAudioSource.Play();
        }

        private void StopOpeningAudio()
        {
            if (openingAudioSource == null)
                return;

            openingAudioSource.Stop();
            openingAudioSource.clip = null;
        }

        private void SynchronizeOpeningAudio()
        {
            if (sequence == null
                || sequence.IsSkipping
                || openingAudioSource == null
                || openingAudioSource.clip == null
                || !openingAudioSource.isPlaying)
            {
                return;
            }

            float clipEnd = Mathf.Max(0f, openingAudioSource.clip.length - 0.01f);
            float expectedTime = Mathf.Clamp(sequence.Elapsed, 0f, clipEnd);
            if (Mathf.Abs(openingAudioSource.time - expectedTime) <= 0.25f)
                return;

            // 렌더/로딩 stall 동안 오디오 스레드만 앞서간 경우 영상 타임라인에 다시 맞춘다.
            openingAudioSource.time = expectedTime;
            Debug.Log(
                $"[OpeningSceneController] 프레임 지연 후 오디오 재동기화: {expectedTime:F2}s");
        }

        /// <summary>
        /// 재생 중에는 마스터 볼륨이 바뀔 수 없다 — cold start엔 설정 UI 자체가 없고,
        /// additive replay 중에는 PlayScene UI 루트가 비활성이다. 그래서 재생 시작 때만 읽는다.
        /// </summary>
        private void RefreshMasterVolume()
        {
            cachedMasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(
                GameConstants.PrefsKeys.MasterVolume,
                GameConstants.Defaults.MasterVolume));
        }

        private void UpdateAudioVolume()
        {
            if (openingAudioSource == null)
                return;

            float fade = sequence != null ? sequence.FadeAlpha : 0f;
            openingAudioSource.volume = cachedMasterVolume * (1f - fade);
        }

        private struct SkipInputSnapshot
        {
            public bool IsHeld;
            public bool Began;
            public string Source;
        }

        private static SkipInputSnapshot ReadSkipInput()
        {
            SkipInputSnapshot result = new SkipInputSnapshot
            {
                Source = "Unknown"
            };

            // 터치와 마우스는 IMGUI 스킵 버튼에서만 처리한다.
            // 모바일 물리 Back과 데스크톱 키보드는 접근성 단축 입력으로 유지한다.
            if (Application.isMobilePlatform)
            {
                result.IsHeld = Input.GetKey(KeyCode.Escape);
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    result.Began = true;
                    result.Source = "Back";
                }
            }
            else
            {
                bool mouseHeld = Input.GetMouseButton(0)
                    || Input.GetMouseButton(1)
                    || Input.GetMouseButton(2);
                bool mouseBegan = Input.GetMouseButtonDown(0)
                    || Input.GetMouseButtonDown(1)
                    || Input.GetMouseButtonDown(2);
                result.IsHeld = Input.anyKey && !mouseHeld;
                if (Input.anyKeyDown && !mouseBegan)
                {
                    result.Began = true;
                    result.Source = "Key";
                }
            }

            return result;
        }

        private void ReloadOrientationResourcesDuringPlayback(bool portrait)
        {
            bool resumeAudio = openingAudioSource != null && openingAudioSource.isPlaying;
            if (resumeAudio)
                openingAudioSource.Pause();

            LoadOrientationResources(portrait);

            // 방향 전환 중 동기 texture decode에 걸린 시간도 재생 시간에서 제외한다.
            playbackClock.Reset(Time.realtimeSinceStartupAsDouble);
            skipInputGate.Reset();
            if (resumeAudio && !applicationPaused)
                openingAudioSource.UnPause();
        }

        private void LoadOrientationResources(bool portrait)
        {
            UnloadOrientationResources();
            loadedPortrait = portrait;
            orientationLoaded = true;
            string suffix = portrait ? "portrait" : "landscape";

            bool anyMissing = false;
            for (int i = 0; i < ImagePaths.Length; i++)
            {
                string path = ImagePaths[i] + suffix;
                openingImages[i] = Resources.Load<Texture2D>(path);
                if (openingImages[i] == null)
                {
                    anyMissing = true;
                    Debug.LogWarning($"[OpeningSceneController] 이미지 누락, fallback 사용: Resources/{path}");
                }
            }

            if (anyMissing && fallbackIcon == null)
                fallbackIcon = Resources.Load<Texture2D>(FallbackIconPath);
        }

        private void UnloadOrientationResources()
        {
            for (int i = 0; i < openingImages.Length; i++)
            {
                if (openingImages[i] != null)
                    Resources.UnloadAsset(openingImages[i]);
                openingImages[i] = null;
            }
            orientationLoaded = false;
        }

        private void TryLoadPlayScene()
        {
            if (loadingPlayScene)
                return;

            loadError = null;
            try
            {
                AsyncOperation operation = SceneManager.LoadSceneAsync(
                    GameConstants.Scenes.Play,
                    LoadSceneMode.Single);
                if (operation == null)
                {
                    SetLoadError("PlayScene 로드를 시작하지 못했습니다.");
                    return;
                }
                loadingPlayScene = true;
            }
            catch (Exception e)
            {
                SetLoadError($"PlayScene 로드 실패: {e.Message}");
            }
        }

        private void SetLoadError(string message)
        {
            loadingPlayScene = false;
            loadError = message;
            Debug.LogError($"[OpeningSceneController] {message}");
        }

        private void OnGUI()
        {
            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            GUI.depth = -10000;
            GUI.color = Color.white;

            Rect screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
            if (sequence != null)
            {
                DrawSequence(screenRect);
            }
            else
            {
                DrawSolid(screenRect, Color.black);
            }

            if (!string.IsNullOrEmpty(loadError))
                DrawRetryPanel(screenRect);

            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        private void DrawSequence(Rect screenRect)
        {
            int current = sequence.CurrentImageIndex;
            DrawImageOrFallback(screenRect, openingImages[current], current, 1f);

            int next = sequence.NextImageIndex;
            float blend = sequence.ImageBlend;
            if (next >= 0 && blend > 0f)
                DrawImageOrFallback(screenRect, openingImages[next], next, blend);

            DrawFloatingLights(screenRect, sequence.Elapsed, 1f - sequence.FadeAlpha);

            float titleAlpha = sequence.TitleAlpha * (1f - sequence.FadeAlpha);
            Rect safeRect = GetGuiSafeArea(screenRect);
            if (titleAlpha > 0f)
                DrawTitle(safeRect, titleAlpha);

            if (sequence.CanSkip)
                DrawSkipButton(safeRect, 1f - sequence.FadeAlpha);

            if (sequence.FadeAlpha > 0f)
                DrawSolid(screenRect, new Color(0f, 0f, 0f, sequence.FadeAlpha));
        }

        private void DrawImageOrFallback(Rect target, Texture2D texture, int imageIndex, float alpha)
        {
            if (texture == null)
            {
                DrawFallback(target, alpha);
                return;
            }

            Rect uv = CalculateCoverUv(target, texture.width, texture.height);
            uv = ApplyKenBurns(uv, imageIndex, sequence != null ? sequence.Elapsed : 0f);
            Color previous = GUI.color;
            GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * alpha);
            GUI.DrawTextureWithTexCoords(target, texture, uv, true);
            GUI.color = previous;
        }

        private void DrawFallback(Rect target, float alpha)
        {
            const int BandCount = 24;
            Color top = new Color(0.035f, 0.16f, 0.18f, alpha);
            Color bottom = new Color(0.015f, 0.025f, 0.035f, alpha);
            float bandHeight = target.height / BandCount;
            for (int i = 0; i < BandCount; i++)
            {
                float t = i / (float)(BandCount - 1);
                Color color = Color.Lerp(top, bottom, t);
                DrawSolid(new Rect(target.x, target.y + bandHeight * i, target.width, bandHeight + 1f), color);
            }

            if (fallbackIcon == null)
                return;

            float size = Mathf.Min(target.width, target.height) * 0.28f;
            Rect iconRect = new Rect(
                target.center.x - size * 0.5f,
                target.center.y - size * 0.5f,
                size,
                size);
            Color previous = GUI.color;
            GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * alpha);
            GUI.DrawTexture(iconRect, fallbackIcon, ScaleMode.ScaleToFit, true);
            GUI.color = previous;
        }

        private void DrawTitle(Rect safeRect, float alpha)
        {
            EnsureStyles();
            float minSide = Mathf.Min(safeRect.width, safeRect.height);
            float availableWidth = Mathf.Max(1f, safeRect.width - 32f);

            // CalcSize는 텍스트를 실제로 측정한다. 화면 크기가 그대로면 결과도 같으므로
            // 타이틀 구간(6.2~10초) 내내 OnGUI 패스마다 3회씩 반복 측정하지 않는다.
            if (minSide != titleFitMinSide || availableWidth != titleFitAvailableWidth)
            {
                titleFitMinSide = minSide;
                titleFitAvailableWidth = availableWidth;
                fittedEnglishSize = FitFontSize(
                    titleStyle,
                    EnglishTitleContent,
                    Mathf.RoundToInt(Mathf.Clamp(minSide * 0.060f, 30f, 72f)),
                    20,
                    availableWidth);
                fittedKoreanSize = FitFontSize(
                    koreanTitleStyle,
                    KoreanTitleContent,
                    Mathf.RoundToInt(Mathf.Clamp(minSide * 0.080f, 40f, 94f)),
                    28,
                    availableWidth);
                fittedSubtitleSize = FitFontSize(
                    subtitleStyle,
                    SubtitleContent,
                    Mathf.RoundToInt(Mathf.Clamp(minSide * 0.027f, 17f, 34f)),
                    13,
                    availableWidth);
            }

            titleStyle.fontSize = fittedEnglishSize;
            koreanTitleStyle.fontSize = fittedKoreanSize;
            subtitleStyle.fontSize = fittedSubtitleSize;

            float englishHeight = titleStyle.fontSize * 1.35f;
            float koreanHeight = koreanTitleStyle.fontSize * 1.30f;
            float subtitleHeight = subtitleStyle.fontSize * 1.8f;
            float top = safeRect.y + safeRect.height * 0.075f;
            Rect englishRect = new Rect(safeRect.x + 16f, top, safeRect.width - 32f, englishHeight);
            Rect koreanRect = new Rect(safeRect.x + 16f, englishRect.yMax - 2f, safeRect.width - 32f, koreanHeight);
            Rect subtitleRect = new Rect(safeRect.x + 16f, koreanRect.yMax + 2f, safeRect.width - 32f, subtitleHeight);

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, alpha * 0.8f);
            GUI.Label(new Rect(englishRect.x + 2f, englishRect.y + 3f, englishRect.width, englishRect.height), EnglishTitleText, titleStyle);
            GUI.Label(new Rect(koreanRect.x + 3f, koreanRect.y + 4f, koreanRect.width, koreanRect.height), KoreanTitleText, koreanTitleStyle);
            GUI.Label(new Rect(subtitleRect.x + 2f, subtitleRect.y + 2f, subtitleRect.width, subtitleRect.height), SubtitleText, subtitleStyle);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(englishRect, EnglishTitleText, titleStyle);
            GUI.Label(koreanRect, KoreanTitleText, koreanTitleStyle);
            GUI.Label(subtitleRect, SubtitleText, subtitleStyle);
            GUI.color = previous;
        }

        private void DrawSkipButton(Rect safeRect, float alpha)
        {
            EnsureStyles();
            float shortSide = Mathf.Min(safeRect.width, safeRect.height);
            Rect buttonRect = CalculateSkipButtonRect(safeRect);

            skipButtonStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(shortSide * 0.023f, 18f, 28f));
            Color previous = GUI.color;
            DrawSolid(buttonRect, new Color(0.92f, 0.7f, 0.22f, alpha * 0.92f));
            DrawSolid(
                new Rect(buttonRect.x + 2f, buttonRect.y + 2f, buttonRect.width - 4f, buttonRect.height - 4f),
                new Color(0.025f, 0.18f, 0.19f, alpha * 0.92f));
            GUI.color = new Color(1f, 1f, 1f, alpha);
            bool clicked = GUI.Button(buttonRect, "건너뛰기  ▶", skipButtonStyle);
            GUI.color = previous;

            if (clicked && sequence != null && sequence.TrySkip())
            {
                Debug.Log(
                    $"[OpeningSceneController] 스킵 버튼 입력 수락: elapsed={sequence.Elapsed:F2}s");
            }
        }

        private void DrawRetryPanel(Rect screenRect)
        {
            EnsureStyles();
            DrawSolid(screenRect, new Color(0f, 0f, 0f, 0.86f));
            Rect safeRect = GetGuiSafeArea(screenRect);
            float width = Mathf.Min(680f, safeRect.width - 48f);
            float height = Mathf.Min(240f, safeRect.height - 32f);
            Rect panel = new Rect(
                safeRect.x + (safeRect.width - width) * 0.5f,
                safeRect.y + (safeRect.height - height) * 0.5f,
                width,
                height);
            DrawSolid(panel, new Color(0.06f, 0.09f, 0.11f, 0.98f));
            GUI.Label(new Rect(panel.x + 24f, panel.y + 24f, panel.width - 48f, 112f), loadError, errorStyle);
            Rect retryRect = new Rect(panel.x + panel.width * 0.25f, panel.yMax - 76f, panel.width * 0.5f, 52f);
            if (GUI.Button(retryRect, "다시 시도", retryStyle))
                TryLoadPlayScene();
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = Color.white;

            koreanTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            koreanTitleStyle.normal.textColor = new Color(1f, 0.88f, 0.44f);

            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Normal
            };
            subtitleStyle.normal.textColor = new Color(0.86f, 0.96f, 0.9f);

            skipButtonStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(18, 18, 8, 8)
            };
            skipButtonStyle.normal.textColor = Color.white;
            skipButtonStyle.hover.textColor = new Color(1f, 0.9f, 0.5f);
            skipButtonStyle.active.textColor = new Color(1f, 0.8f, 0.25f);

            errorStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                wordWrap = true
            };
            errorStyle.normal.textColor = new Color(1f, 0.82f, 0.72f);

            retryStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };
        }

        private static Rect CalculateCoverUv(Rect target, int textureWidth, int textureHeight)
        {
            if (target.width <= 0f || target.height <= 0f || textureWidth <= 0 || textureHeight <= 0)
                return new Rect(0f, 0f, 1f, 1f);

            float targetAspect = target.width / target.height;
            float textureAspect = textureWidth / (float)textureHeight;
            if (textureAspect > targetAspect)
            {
                float visibleWidth = targetAspect / textureAspect;
                return new Rect((1f - visibleWidth) * 0.5f, 0f, visibleWidth, 1f);
            }

            float visibleHeight = textureAspect / targetAspect;
            return new Rect(0f, (1f - visibleHeight) * 0.5f, 1f, visibleHeight);
        }

        private static Rect ApplyKenBurns(Rect coverUv, int imageIndex, float elapsed)
        {
            float phase = imageIndex * 1.73f;
            float zoom = 1.018f + 0.008f * (0.5f + 0.5f * Mathf.Sin(elapsed * 0.42f + phase));
            float width = coverUv.width / zoom;
            float height = coverUv.height / zoom;
            float spareX = coverUv.width - width;
            float spareY = coverUv.height - height;
            float panX = 0.5f + 0.35f * Mathf.Sin(elapsed * 0.19f + phase);
            float panY = 0.5f + 0.30f * Mathf.Sin(elapsed * 0.16f + phase * 1.31f);
            return new Rect(
                coverUv.x + spareX * panX,
                coverUv.y + spareY * panY,
                width,
                height);
        }

        private static void DrawFloatingLights(Rect screenRect, float elapsed, float overallAlpha)
        {
            if (overallAlpha <= 0f)
                return;

            for (int i = 0; i < 12; i++)
            {
                float phase = i * 2.173f;
                float baseX = ((i * 0.6180339f + 0.09f) % 1f) * screenRect.width;
                float baseY = ((i * 0.371f + 0.12f) % 1f) * screenRect.height;
                float x = screenRect.x + baseX + Mathf.Sin(elapsed * 0.31f + phase) * screenRect.width * 0.018f;
                float y = screenRect.y + baseY + Mathf.Sin(elapsed * 0.23f + phase * 0.77f) * screenRect.height * 0.026f;
                float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 1.1f + phase);
                float size = 2.5f + (i % 4) * 1.2f + pulse * 2f;
                float alpha = overallAlpha * (0.08f + pulse * 0.18f);

                DrawSolid(
                    new Rect(x - size, y - size, size * 2f, size * 2f),
                    new Color(1f, 0.67f, 0.18f, alpha * 0.28f));
                DrawSolid(
                    new Rect(x - size * 0.32f, y - size * 0.32f, size * 0.64f, size * 0.64f),
                    new Color(1f, 0.9f, 0.48f, alpha));
            }
        }

        private static Rect GetGuiSafeArea(Rect screenRect)
        {
            Rect safeArea = Screen.safeArea;
            if (safeArea.width <= 0f || safeArea.height <= 0f)
                return screenRect;

            // Screen.safeArea는 좌하단 원점, IMGUI는 좌상단 원점이다.
            return new Rect(
                safeArea.x,
                Screen.height - safeArea.yMax,
                safeArea.width,
                safeArea.height);
        }

        internal static Rect CalculateSkipButtonRect(Rect safeRect)
        {
            float shortSide = Mathf.Min(safeRect.width, safeRect.height);
            float margin = Mathf.Round(Mathf.Clamp(shortSide * 0.024f, 16f, 28f));
            float width = Mathf.Round(Mathf.Clamp(shortSide * 0.16f, 144f, 192f));
            float height = Mathf.Round(Mathf.Clamp(shortSide * 0.06f, 56f, 72f));
            width = Mathf.Min(width, Mathf.Max(1f, safeRect.width - margin * 2f));
            height = Mathf.Min(height, Mathf.Max(1f, safeRect.height - margin * 2f));
            return new Rect(
                safeRect.xMax - margin - width,
                safeRect.y + margin,
                width,
                height);
        }

        private static int FitFontSize(
            GUIStyle style,
            GUIContent content,
            int preferredSize,
            int minimumSize,
            float availableWidth)
        {
            int fittedSize = Mathf.Max(minimumSize, preferredSize);
            style.fontSize = fittedSize;
            float measuredWidth = style.CalcSize(content).x;
            if (measuredWidth > availableWidth && measuredWidth > 0f)
            {
                fittedSize = Mathf.Max(
                    minimumSize,
                    Mathf.FloorToInt(fittedSize * availableWidth / measuredWidth));
            }
            return fittedSize;
        }

        private static void DrawSolid(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = previous * color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
