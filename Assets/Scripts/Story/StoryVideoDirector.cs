using InsectGame.Core;
using InsectGame.UI;
using UnityEngine;
using UnityEngine.Video;

namespace InsectGame.Story
{
    /// <summary>
    /// 스토리 영상(mp4) 재생기 — <see cref="CutsceneDirector"/>의 영상판이다. 저쪽이 카메라를
    /// 뺏는다면 여기는 화면을 통째로 덮는다(카메라는 건드리지 않는다).
    ///
    /// <b>스토리 비트의 대사가 끝난 뒤</b>(<c>StoryBeatCompleted</c>) 재생한다. 파일은
    /// <c>Assets/StreamingAssets/Video/</c>에서 <c>VideoPlayer.url</c>로 스트리밍한다 —
    /// 임포터를 타지 않아 트랜스코딩·메모리 상주가 없고, Android의 <c>jar:file://</c> 경로도
    /// URL 재생은 그대로 지원한다.
    ///
    /// <b>복귀 보장이 급소다.</b> 재생 중 조작을 막고 모달 스택을 잡으므로 어떤 경로로 끝나든
    /// (정상 종료·건너뛰기·디코더 오류·파일 없음·비활성·씬 전환) <see cref="Stop"/> 하나로 되돌린다.
    /// 파일이 없거나 디코더가 실패해도 <b>진행은 잃지 않는다</b> — 비트는 이미 완료됐고,
    /// 여기서 멈추면 <c>DrainPendingTriggers</c>가 모달 가드에 막혀 다음 비트가 영영 안 온다.
    /// </summary>
    public class StoryVideoDirector : MonoBehaviour, IModalUI
    {
        private const string VideoFolder = "Video";
        /// <summary>준비(디코더 열기)가 이 시간을 넘기면 포기한다. 저사양 기기의 첫 프레임 지연보다 넉넉하게.</summary>
        private const float PrepareTimeoutSeconds = 5f;
        /// <summary>디코더가 <c>loopPointReached</c>를 안 주는 기기 대비 — 저작 길이보다 이만큼 지나면 끝낸다.</summary>
        private const float OverrunGraceSeconds = 3f;
        /// <summary>전투 화면 때문에 미룬 영상을 포기하는 시각(초). <see cref="CutsceneDirector"/>와 같다.</summary>
        private const float PendingGiveUpSeconds = 12f;

        private StoryDirector storyDirector;
        private CameraFollower cameraFollower;
        private PlayerMovement playerMovement;

        private VideoPlayer videoPlayer;
        private AudioSource audioSource;
        private RenderTexture renderTexture;

        private StoryVideoDefinition current;
        private float elapsed;
        private float prepareSeconds;
        private bool prepared;
        private bool playing;
        private bool restoreFrozen;

        private StoryVideoDefinition pending;
        private float pendingSeconds;

        private GUIStyle subtitleStyle;
        private bool stylesReady;

        // ── IModalUI ── ESC(Back키)로 건너뛴다. 등록하지 않으면 StoryDirector.ShouldDeferNow가
        // "화면에 아무것도 없다"고 판정해 영상 위로 다음 대사가 겹쳐 뜬다.
        public bool IsOpen => playing;
        public void CloseModal() => Stop();

        public bool IsPlaying => playing;

        public void AutoWire(StoryDirector director, CameraFollower follower, PlayerMovement movement)
        {
            if (storyDirector == null) storyDirector = director;
            if (cameraFollower == null) cameraFollower = follower;
            if (playerMovement == null) playerMovement = movement;
            Subscribe();
        }

        private void Awake()
        {
            // 컴포넌트를 코드로 붙인다 — 이 프로젝트엔 프리팹이 없고 Bootstrap이 전부 EnsureComponent로 만든다.
            videoPlayer = gameObject.GetComponent<VideoPlayer>();
            if (videoPlayer == null) videoPlayer = gameObject.AddComponent<VideoPlayer>();
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.ignoreListenerPause = true;   // 오프닝과 같은 이유 — 리스너 일시정지 중에도 들려야 한다

            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.source = VideoSource.Url;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.aspectRatio = VideoAspectRatio.FitInside;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.skipOnDrop = true;
            videoPlayer.waitForFirstFrame = true;

            videoPlayer.prepareCompleted += OnPrepared;
            videoPlayer.loopPointReached += OnReachedEnd;
            videoPlayer.errorReceived += OnVideoError;
        }

        // AutoWire와 OnEnable이 함께 부른다 — `-=` 뒤 `+=`라 중복 구독이 되지 않는다.
        private void Subscribe()
        {
            if (storyDirector == null) return;
            storyDirector.StoryBeatCompleted -= OnBeatCompleted;
            storyDirector.StoryBeatCompleted += OnBeatCompleted;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            if (storyDirector != null) storyDirector.StoryBeatCompleted -= OnBeatCompleted;
            pending = null;   // Update가 안 도는 동안 대기 시각도 안 흐른다 — 뒤늦게 튀어나오지 않게 버린다
            Stop();
        }

        private void OnDestroy()
        {
            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= OnPrepared;
                videoPlayer.loopPointReached -= OnReachedEnd;
                videoPlayer.errorReceived -= OnVideoError;
            }
            ReleaseRenderTexture();
        }

        private void OnApplicationPause(bool paused)
        {
            if (!playing || videoPlayer == null || !prepared) return;
            if (paused) videoPlayer.Pause();
            else videoPlayer.Play();
        }

        private void OnBeatCompleted(StoryBeat beat)
        {
            if (beat == null || string.IsNullOrEmpty(beat.videoId)) return;
            if (!StoryVideoLibrary.TryGet(beat.videoId, out StoryVideoDefinition def))
            {
                // 오타를 조용히 넘기지 않는다 — 영상이 안 나오는 건 화면상 티가 안 난다.
                Debug.LogWarning($"[StoryVideo] 알 수 없는 videoId: '{beat.videoId}' (beat {beat.beatId})");
                return;
            }

            // 전투 화면이 아직 떠 있으면 미룬다(BattleWin 비트). 카메라의 배틀 모드가
            // "전투가 아직 화면에 있다"의 유일한 신뢰 신호다(BattleScreenUI는 IModalUI가 아니다).
            if (cameraFollower != null && cameraFollower.InBattleMode)
            {
                pending = def;
                pendingSeconds = 0f;
                return;
            }

            Play(def);
        }

        private void TickPending()
        {
            if (pending == null) return;

            if (cameraFollower == null || !cameraFollower.InBattleMode)
            {
                StoryVideoDefinition queued = pending;
                pending = null;
                Play(queued);
                return;
            }

            pendingSeconds += Time.unscaledDeltaTime;
            if (pendingSeconds < PendingGiveUpSeconds) return;

            Debug.LogWarning("[StoryVideo] 전투 화면이 닫히지 않아 영상을 건너뛴다");
            pending = null;
        }

        public void Play(StoryVideoDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.fileName)) return;
            if (videoPlayer == null) return;

            // 재진입 가드 — 없으면 restoreFrozen이 자기가 만든 frozen=true를 읽어 복구를 건너뛴다(영구 먹통).
            if (playing) Stop();

            current = definition;
            elapsed = 0f;
            prepareSeconds = 0f;
            prepared = false;
            playing = true;

            restoreFrozen = playerMovement != null && playerMovement.IsFrozen;
            if (playerMovement != null)
            {
                playerMovement.CancelAutoRun();
                playerMovement.SetFrozen(true);
            }
            ModalUIRegistry.Register(this);

            // 재생 중엔 설정 UI가 닫혀 있으니 시작 때 한 번만 읽는다(OpeningSceneController와 같다).
            audioSource.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(
                GameConstants.PrefsKeys.MasterVolume, GameConstants.Defaults.MasterVolume));

            string url = BuildUrl(definition.fileName);
#if UNITY_EDITOR || UNITY_STANDALONE
            // 에디터·데스크톱에서는 파일 유무를 미리 알 수 있다 — 디코더 오류보다 읽기 쉬운 경고를 남긴다.
            // Android는 StreamingAssets가 jar 안에 있어 File.Exists가 항상 false라 여기서 보지 않는다.
            if (!System.IO.File.Exists(url))
            {
                Debug.LogWarning($"[StoryVideo] 파일이 없다: {url} ({definition.videoId}) — 영상을 건너뛴다");
                Stop();
                return;
            }
#endif
            videoPlayer.url = url;
            // URL 소스는 준비 전엔 트랙 수를 모른다 — 1로 못 박아야 SetTargetAudioSource가 받는다.
            videoPlayer.controlledAudioTrackCount = 1;
            videoPlayer.SetTargetAudioSource(0, audioSource);
            videoPlayer.Prepare();
        }

        private static string BuildUrl(string fileName)
        {
            // Path.Combine은 Windows에서 역슬래시를 넣는데 Android jar 경로는 '/'만 받는다.
            return Application.streamingAssetsPath + "/" + VideoFolder + "/" + fileName;
        }

        /// <summary>재생 종료 — <b>모든 종료 경로가 여기로 모인다.</b> 두 번 불려도 안전하다.</summary>
        public void Stop()
        {
            if (!playing)
            {
                ModalUIRegistry.Unregister(this);
                return;
            }

            playing = false;
            prepared = false;
            current = null;

            if (videoPlayer != null && (videoPlayer.isPlaying || videoPlayer.isPrepared))
                videoPlayer.Stop();

            if (playerMovement != null && !restoreFrozen) playerMovement.SetFrozen(false);
            ModalUIRegistry.Unregister(this);
        }

        private void OnPrepared(VideoPlayer source)
        {
            if (!playing) return;
            EnsureRenderTexture((int)source.width, (int)source.height);
            source.targetTexture = renderTexture;
            prepared = true;
            elapsed = 0f;
            source.Play();
        }

        private void OnReachedEnd(VideoPlayer source)
        {
            if (playing) Stop();
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            Debug.LogWarning($"[StoryVideo] 재생 실패({current?.videoId}): {message} — 영상을 건너뛴다");
            Stop();
        }

        private void EnsureRenderTexture(int width, int height)
        {
            if (width <= 0 || height <= 0) { width = 1280; height = 720; }
            if (renderTexture != null && renderTexture.width == width && renderTexture.height == height)
                return;
            ReleaseRenderTexture();
            renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            renderTexture.name = "StoryVideoRT";
            renderTexture.Create();
        }

        private void ReleaseRenderTexture()
        {
            if (renderTexture == null) return;
            if (videoPlayer != null) videoPlayer.targetTexture = null;
            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }

        private void Update()
        {
            if (!playing)
            {
                TickPending();
                return;
            }

            // 연출은 timeScale에 끌려다니면 안 된다(전투 슬로모션 직후 재생될 수 있다).
            float dt = Time.unscaledDeltaTime;

            if (!prepared)
            {
                prepareSeconds += dt;
                if (prepareSeconds > PrepareTimeoutSeconds)
                {
                    Debug.LogWarning($"[StoryVideo] 준비 시간 초과({current?.videoId}) — 영상을 건너뛴다");
                    Stop();
                }
                return;
            }

            elapsed += dt;
            if (current != null && elapsed > current.expectedDuration + OverrunGraceSeconds)
                Stop();
        }

        private void OnGUI()
        {
            if (!playing) return;

            EnsureStyles();
            UIScale.Begin();
            // 모든 HUD 위에 덮는다 — 오프닝(-10000)과 같은 계열.
            GUI.depth = -9000;

            Rect full = new Rect(0f, 0f, UIScale.VirtualScreenWidth, UIScale.VirtualScreenHeight);
            UISurface.Flat(full, Color.black);

            if (prepared && renderTexture != null)
            {
                // 16:9 마스터 하나로 세로 화면까지 덮는다 — 가운데를 잘라 채운다(cover).
                Rect uv = UIHelper.CalculateCoverUv(full, renderTexture.width, renderTexture.height);
                GUI.DrawTextureWithTexCoords(full, renderTexture, uv, false);
                DrawSubtitle();
            }
            else
            {
                // 첫 프레임 전 — 검정 위에 작은 점 하나. 멈춘 것처럼 보이지 않게만.
                float pulse = 0.35f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
                UISurface.Flat(new Rect(full.width * 0.5f - 5f, full.height * 0.5f - 5f, 10f, 10f),
                    new Color(1f, 1f, 1f, pulse));
            }

            // 건너뛰기 — ESC(ModalUIRegistry)와 이 탭. 모바일엔 Back키뿐이라 이 버튼이 실질 유일 수단이다.
            float skipW = 240f, skipH = 56f;
            Rect skip = new Rect(
                UIScale.VirtualScreenWidth - UIScale.VirtualSafeRight - skipW - 24f,
                UISafeLayout.BottomY(skipH),
                skipW, skipH);
            if (UISurface.Button(skip, "건너뛰기 ▶", new Color(1f, 1f, 1f, 0.22f), subtitleStyle))
                Stop();

            UIScale.End();
        }

        private void DrawSubtitle()
        {
            if (current == null || !StoryVideoTimeline.TryGetCue(current.cues, elapsed, out int index)) return;

            StoryVideoCue cue = current.cues[index];
            float alpha = CutsceneTimeline.SubtitleAlpha(cue.duration, StoryVideoTimeline.CueProgress(cue, elapsed));

            float w = Mathf.Min(1100f, UIScale.VirtualScreenWidth
                - UIScale.VirtualSafeLeft - UIScale.VirtualSafeRight - 80f);
            float x = UIScale.VirtualSafeLeft
                + (UIScale.VirtualScreenWidth - UIScale.VirtualSafeLeft - UIScale.VirtualSafeRight - w) * 0.5f;
            float y = UISafeLayout.BottomY(150f);

            UISurface.Flat(new Rect(x, y, w, 96f), new Color(0f, 0f, 0f, 0.55f * alpha));

            Color c = subtitleStyle.normal.textColor;
            subtitleStyle.normal.textColor = new Color(c.r, c.g, c.b, alpha);
            UIHelper.LabelFit(new Rect(x + 24f, y + 8f, w - 48f, 80f), cue.text, subtitleStyle);
            subtitleStyle.normal.textColor = c;
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            subtitleStyle.normal.textColor = new Color(0.96f, 0.95f, 0.90f);
        }
    }
}
