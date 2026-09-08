using System;
using InsectGame.Core;
using InsectGame.UI;
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

        /// <summary>
        /// 오프닝 내레이션 — <b>이 게임이 무슨 이야기인지</b>를 처음 켠 사람에게 알린다.
        /// 예전엔 부제 한 줄("발견하고, 성장시키고")뿐이라 서사가 전혀 전달되지 않았다.
        ///
        /// 세 줄로 1막의 전제를 세운다: 사라짐 → 그것을 노리는 자들 → 플레이어가 할 일.
        /// 답을 주지 않고 질문만 남긴다 — 답은 마을 어르신이 한다.
        /// 순서·시각은 <see cref="OpeningSequenceState"/>의 Narration* 상수가 정한다.
        /// </summary>
        private static readonly string[] NarrationLines =
        {
            "곤충이 사라지고 있다.",
            "그리고 그것을 남김없이 거두려는 자들이 있다.",
            "사라지는 이름을 기록하는 일 — 거기서부터 시작된다.",
        };

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
        private GUIStyle narrationStyle;
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

            // 비네트 → 곤충 → 빛 순서. 곤충은 배경 앞을 지나는 그림자라 빛보다 뒤에 있어야
            // 빛이 곤충 위로 떠오르는 것처럼 보인다.
            DrawVignette(screenRect, 1f - sequence.FadeAlpha);
            DrawDriftingInsects(screenRect, sequence.Elapsed, 1f - sequence.FadeAlpha);
            DrawFloatingLights(screenRect, sequence.Elapsed, 1f - sequence.FadeAlpha);

            float titleAlpha = sequence.TitleAlpha * (1f - sequence.FadeAlpha);
            Rect safeRect = GetGuiSafeArea(screenRect);
            if (titleAlpha > 0f)
                DrawTitle(safeRect, titleAlpha);

            sequence.GetNarration(out int narrationIndex, out float narrationAlpha);
            if (narrationIndex >= 0 && narrationIndex < NarrationLines.Length)
                DrawNarration(safeRect, NarrationLines[narrationIndex],
                    narrationAlpha * (1f - sequence.FadeAlpha));

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

        /// <summary>
        /// 스토리 내레이션 — 화면 아래쪽 1/3에 한 줄. 타이틀은 위쪽 7.5%라 겹치지 않는다.
        /// 건너뛰기 알약(하단 중앙)보다 위에 두어 서로 침범하지 않게 한다.
        /// </summary>
        private void DrawNarration(Rect safeRect, string text, float alpha)
        {
            if (alpha <= 0f || string.IsNullOrEmpty(text)) return;

            EnsureStyles();
            float shortSide = Mathf.Min(safeRect.width, safeRect.height);
            narrationStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(shortSide * 0.038f, 20f, 44f));

            float width = safeRect.width - 64f;
            float height = narrationStyle.fontSize * 2.6f;
            // 건너뛰기 알약이 하단 마진 위에 서므로 그보다 충분히 위에 둔다.
            // **알파에 따라 아래에서 떠오른다** — 제자리에서 밝아지기만 하면 슬라이드처럼 보인다.
            float rise = (1f - alpha) * height * 0.45f;
            float y = safeRect.yMax - safeRect.height * 0.30f - height * 0.5f + rise;
            Rect rect = new Rect(safeRect.x + 32f, y, width, height);

            Color previous = GUI.color;

            // 검은 띠 대신 **글자 뒤에만 번지는 발광**을 깐다. 각진 띠는 자막을 자막처럼
            // 보이게 하는데, 여기서 원하는 건 화면에 스며든 문장이다.
            // 비네트가 이미 아래를 눌러 뒀으므로 이 정도로도 밝은 배경에서 읽힌다.
            float glowH = height * 1.15f;
            UIShapes.Ellipse(
                new Rect(rect.center.x - width * 0.42f, rect.center.y - glowH * 0.5f, width * 0.84f, glowH),
                new Color(0f, 0f, 0f, 0.5f * alpha));

            GUI.color = new Color(0f, 0f, 0f, 0.9f * alpha);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 3f, rect.width, rect.height), text, narrationStyle);
            GUI.color = new Color(1f, 0.98f, 0.92f, alpha);
            GUI.Label(rect, text, narrationStyle);
            GUI.color = previous;
        }

        /// <summary>
        /// 하단 중앙 반투명 알약. 예전엔 우상단에 <b>금색 사각형 + 2px 안쪽 어두운 사각형</b>을
        /// 겹쳐 그린 각진 이중 테두리였다 — 그 베벨이 "옛날 느낌"의 정체였다.
        ///
        /// 지금은 영상이 비치는 유리판 위에 글자를 얹고, 남은 재생 시간을 얇은 앰버 바로
        /// 보여준다. 위치를 하단 중앙으로 내린 것은 타이틀(상단 7.5%)과 아트워크를 가리지
        /// 않으면서도 엄지가 닿는 자리이기 때문이다.
        /// </summary>
        private void DrawSkipButton(Rect safeRect, float alpha)
        {
            EnsureStyles();
            float shortSide = Mathf.Min(safeRect.width, safeRect.height);
            Rect buttonRect = CalculateSkipButtonRect(safeRect);
            float radius = buttonRect.height * 0.5f;   // 완전한 알약

            skipButtonStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(shortSide * 0.026f, 18f, 32f));

            Event current = Event.current;
            bool hovered = current != null && buttonRect.Contains(current.mousePosition);

            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            // 유리판 — 바깥 테두리(밝은 반투명) 위에 본체(어두운 반투명)를 1.5px 물려 얹는다.
            UISurface.Rounded(buttonRect, new Color(1f, 1f, 1f, hovered ? 0.34f : 0.22f), radius);
            UISurface.Rounded(
                new Rect(buttonRect.x + 1.5f, buttonRect.y + 1.5f, buttonRect.width - 3f, buttonRect.height - 3f),
                new Color(0.02f, 0.05f, 0.07f, hovered ? 0.58f : 0.46f),
                radius);

            // 남은 재생 시간 — 알약 안쪽 아래에 얇게. 얇은 것은 각진 채로 두고(Flat),
            // 둥근 모서리를 뚫지 않게 긴 축을 반경만큼 물린다(rules/ui-layout.md).
            float trackInset = radius * 0.7f;
            float trackW = buttonRect.width - trackInset * 2f;
            if (trackW > 0f && sequence != null)
            {
                float progress = Mathf.Clamp01(sequence.Elapsed / OpeningSequenceState.Duration);
                Rect track = new Rect(trackInset + buttonRect.x, buttonRect.yMax - 9f, trackW, 3f);
                DrawSolid(track, new Color(1f, 1f, 1f, alpha * 0.18f));
                DrawSolid(
                    new Rect(track.x, track.y, track.width * progress, track.height),
                    new Color(1f, 0.82f, 0.36f, alpha * (hovered ? 0.95f : 0.8f)));
            }

            // 글자는 진행바를 피해 살짝 위로.
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(buttonRect.x, buttonRect.y - 3f, buttonRect.width, buttonRect.height), "건너뛰기 ▶", skipButtonStyle);
            bool clicked = GUI.Button(buttonRect, GUIContent.none, GUIStyle.none);
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

            narrationStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Normal,
                wordWrap = true
            };
            narrationStyle.normal.textColor = Color.white;
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

        /// <summary>
        /// 가장자리를 눌러 시선을 가운데로 모은다. 정지 이미지는 네 귀퉁이가 다 보여서
        /// 평평해 보이는데, 비네트 하나로 깊이가 생긴다. 위아래를 더 어둡게 해
        /// 타이틀·자막이 얹힐 자리를 미리 만든다.
        /// </summary>
        private static void DrawVignette(Rect screenRect, float alpha)
        {
            if (alpha <= 0f) return;

            const int Bands = 10;
            float bandH = screenRect.height * 0.22f / Bands;
            for (int i = 0; i < Bands; i++)
            {
                float t = 1f - i / (float)(Bands - 1);   // 가장자리에서 가장 진하다
                Color col = new Color(0f, 0f, 0f, 0.38f * t * t * alpha);
                DrawSolid(new Rect(screenRect.x, screenRect.y + bandH * i, screenRect.width, bandH + 1f), col);
                DrawSolid(new Rect(screenRect.x, screenRect.yMax - bandH * (i + 1), screenRect.width, bandH + 1f), col);
            }
        }

        /// <summary>
        /// 떠다니는 빛. 예전엔 <c>DrawSolid</c>(흰 텍스처)라 <b>각진 사각형 24개</b>였다 —
        /// 반딧불이라기보다 픽셀 덩어리로 보였다. <see cref="UIShapes.Disc"/>는 가장자리가
        /// 부드러운 원이라 같은 코드량으로 훨씬 빛처럼 보인다.
        /// </summary>
        private static void DrawFloatingLights(Rect screenRect, float elapsed, float overallAlpha)
        {
            if (overallAlpha <= 0f)
                return;

            for (int i = 0; i < 18; i++)
            {
                float phase = i * 2.173f;
                float baseX = ((i * 0.6180339f + 0.09f) % 1f) * screenRect.width;
                float baseY = ((i * 0.371f + 0.12f) % 1f) * screenRect.height;
                // 위로 천천히 떠오르며 좌우로 흔들린다 — 제자리 진동보다 살아 있어 보인다.
                float rise = ((elapsed * 0.045f + i * 0.137f) % 1f) * screenRect.height;
                float x = screenRect.x + baseX + Mathf.Sin(elapsed * 0.62f + phase) * screenRect.width * 0.02f;
                float y = screenRect.y + Mathf.Repeat(baseY - rise, screenRect.height);
                float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 1.6f + phase);
                float size = 3f + (i % 4) * 1.4f + pulse * 2.5f;
                float alpha = overallAlpha * (0.10f + pulse * 0.24f);

                // 넓은 헤일로 + 밝은 심지 — 두 겹이라야 발광체로 읽힌다.
                UIShapes.Ellipse(
                    new Rect(x - size * 2.2f, y - size * 2.2f, size * 4.4f, size * 4.4f),
                    new Color(1f, 0.72f, 0.24f, alpha * 0.22f));
                UIShapes.Ellipse(
                    new Rect(x - size * 0.5f, y - size * 0.5f, size, size),
                    new Color(1f, 0.94f, 0.62f, alpha));
            }
        }

        /// <summary>
        /// 화면을 가로지르는 곤충 실루엣.
        ///
        /// <b>곤충 게임 오프닝인데 곤충이 한 마리도 없었다.</b> 정지 이미지와 자막만으로는
        /// 무엇에 관한 게임인지 그림으로 전달되지 않는다. 3D 썸네일(<c>InsectVisual</c>)은
        /// <c>InsectDatabase</c>가 필요해 오프닝 씬에서 쓸 수 없으므로, <c>UIShapes</c>로
        /// 몸통·날개·더듬이를 직접 그린다 — 배경 앞을 지나는 그림자라 형태만 있으면 된다.
        ///
        /// 날갯짓은 세로 스케일을 흔들어 표현한다(가로를 흔들면 앞뒤로 기우는 것처럼 보인다).
        /// </summary>
        private static void DrawDriftingInsects(Rect screenRect, float elapsed, float overallAlpha)
        {
            if (overallAlpha <= 0f) return;

            for (int i = 0; i < 5; i++)
            {
                // 저마다 다른 속도·높이·크기로 지나간다. 같은 속도면 벽지처럼 보인다.
                float speed = 0.055f + i * 0.021f;
                float t = (elapsed * speed + i * 0.31f) % 1.35f;   // 1.0을 넘겨 화면 밖 여백을 둔다
                if (t > 1.15f) continue;                            // 잠깐 비는 구간 — 줄줄이 지나가지 않게

                bool leftward = i % 2 == 1;
                float px = leftward
                    ? screenRect.xMax - t * (screenRect.width + 160f) + 80f
                    : screenRect.x + t * (screenRect.width + 160f) - 80f;
                float py = screenRect.y + screenRect.height * (0.22f + i * 0.13f)
                    + Mathf.Sin(elapsed * 1.3f + i * 1.7f) * screenRect.height * 0.035f;

                float scale = Mathf.Min(screenRect.width, screenRect.height) * (0.020f + (i % 3) * 0.008f);
                float alpha = overallAlpha * 0.34f;
                Color body = new Color(0.05f, 0.06f, 0.09f, alpha);

                // 날갯짓 — 위아래로 눌렸다 펴진다.
                float flap = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(elapsed * 11f + i * 2.1f));
                float wingW = scale * 1.5f;
                float wingH = scale * 0.85f * flap;
                float dir = leftward ? -1f : 1f;

                UIShapes.Ellipse(
                    new Rect(px - wingW * 0.5f - dir * scale * 0.2f, py - wingH, wingW, wingH),
                    new Color(body.r, body.g, body.b, alpha * 0.55f));
                UIShapes.Ellipse(
                    new Rect(px - wingW * 0.5f - dir * scale * 0.2f, py, wingW, wingH),
                    new Color(body.r, body.g, body.b, alpha * 0.4f));

                // 몸통 — 진행 방향으로 길다.
                UIShapes.Ellipse(
                    new Rect(px - scale * 0.9f, py - scale * 0.22f, scale * 1.8f, scale * 0.44f), body);
                // 머리 + 더듬이
                float headX = px + dir * scale * 0.85f;
                UIShapes.Ellipse(
                    new Rect(headX - scale * 0.24f, py - scale * 0.24f, scale * 0.48f, scale * 0.48f), body);
                UIShapes.Capsule(
                    new Vector2(headX, py - scale * 0.1f),
                    new Vector2(headX + dir * scale * 0.6f, py - scale * 0.5f),
                    Mathf.Max(1f, scale * 0.09f), body);
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

        /// <summary>
        /// 스킵 알약의 자리 — <b>하단 중앙</b>. 예전엔 우상단이었는데, 그 위치는
        /// 타이틀·아트워크와 시선을 다투면서도 한 손 조작에서는 가장 먼 모서리였다.
        ///
        /// 세로 마진이 가로보다 큰 것은 제스처바(safeArea가 걷어내지 못하는 기기가 있다)와
        /// 겹치지 않게 하기 위해서다. 높이 하한 56은 터치 타깃 최소치로
        /// <c>OpeningSequenceTests</c>가 고정한다.
        /// </summary>
        internal static Rect CalculateSkipButtonRect(Rect safeRect)
        {
            float shortSide = Mathf.Min(safeRect.width, safeRect.height);
            float margin = Mathf.Round(Mathf.Clamp(shortSide * 0.045f, 20f, 56f));
            float width = Mathf.Round(Mathf.Clamp(shortSide * 0.30f, 200f, 300f));
            float height = Mathf.Round(Mathf.Clamp(shortSide * 0.062f, 56f, 76f));
            width = Mathf.Min(width, Mathf.Max(1f, safeRect.width - margin * 2f));
            height = Mathf.Min(height, Mathf.Max(1f, safeRect.height - margin * 2f));
            return new Rect(
                safeRect.x + (safeRect.width - width) * 0.5f,
                safeRect.yMax - margin - height,
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
