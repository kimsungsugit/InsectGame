using InsectGame.Core;
using InsectGame.UI;
using UnityEngine;
using UnityEngine.Video;

namespace InsectGame.Story
{
    /// <summary>
    /// 「챔피언의 꿈」 도입 영상(경기장 입장 8초) 재생기. <b>무음 영상 + 별도 음원</b>을 한 시계로 튼다 —
    /// <c>OpeningSceneController</c>와 같은 방식이라 건너뛰기·일시정지가 시계 하나로 끝나고, 영상이 못 떠도
    /// 호출부(<see cref="DreamPrologueDirector"/>)가 글자 카드로 이어 간다.
    ///
    /// 상태는 <see cref="State"/> 하나다: 준비 중(검은 화면) → 재생 → 끝남. <b>못 튼 모든 길</b>(파일 없음·디코더 오류·
    /// 준비 3초 초과·재생 중 멈춤)이 <see cref="State.Failed"/>/<see cref="State.Ended"/>로 모이므로 호출부는 둘만 보면 된다.
    /// 영상 파일은 <c>StreamingAssets/Video/dream_arena.mp4</c>, 음원은 <c>Resources/Audio/Dream/dream_arena</c>이며
    /// 둘 다 <c>Tools/Video/dream_arena*.py</c>가 만든다(시각표의 단일 출처는 그 스크립트).
    /// </summary>
    public sealed class DreamIntroVideo
    {
        public enum State { Idle, Preparing, Playing, Ended, Failed }

        public const string FileName = "dream_arena.mp4";
        public const string AudioResourcePath = "Audio/Dream/dream_arena";
        private const string VideoFolder = "Video";
        // 처음 여는 디코더는 느릴 수 있다 — Windows QA 빌드에서 3.8초가 걸렸다(StoryVideoDirector와 같은 5초).
        private const float PrepareTimeoutSeconds = 5f;
        // 긴 프레임(씬 로딩 직후의 끊김)이 준비 시간을 한꺼번에 먹지 않게 프레임당 상한을 둔다(OpeningPlaybackClock과 같다).
        private const float MaxFrameDelta = 0.1f;
        // 소리와 영상이 이만큼 벌어지면 소리를 영상 시각에 맞춘다(디코더가 첫 프레임을 늦게 내는 기기).
        private const float AudioDriftSeconds = 0.25f;
        private const float StallSeconds = 1.2f;
        private const float OverrunGraceSeconds = 1f;
        // 영상 음성(환호)과 월드 BGM이 8초 내내 겹치지 않게 — StoryVideoDirector와 같은 계열.
        private const float BgmDuckFactor = 0.12f;

        private readonly GameObject host;
        private VideoPlayer player;
        private AudioSource audioSource;
        private RenderTexture texture;
        private AudioClip clip;
        private bool componentsReady;
        private bool ducked;
        private float prepareSeconds;
        private float clock;
        private float lastVideoTime;
        private float stalledSeconds;

        public DreamIntroVideo(GameObject host) { this.host = host; }

        public State Current { get; private set; } = State.Idle;

        /// <summary>재생 시작(첫 프레임)부터 흐른 시간(초). 재생 중에만 의미가 있다.</summary>
        public float Clock => clock;

        public bool IsActive => Current == State.Preparing || Current == State.Playing;

        private static string BuildUrl() =>
            // Path.Combine은 Windows에서 역슬래시를 넣는데 Android jar 경로는 '/'만 받는다.
            Application.streamingAssetsPath + "/" + VideoFolder + "/" + FileName;

        private void EnsureComponents()
        {
            if (componentsReady || host == null) return;
            componentsReady = true;

            audioSource = host.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;

            player = host.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = false;
            player.source = VideoSource.Url;
            player.renderMode = VideoRenderMode.RenderTexture;
            // 영상은 무음이다 — 소리는 위 AudioSource가 같은 시계로 튼다(두 트랙이 어긋나지 않게).
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.skipOnDrop = true;
            player.waitForFirstFrame = true;
            player.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            player.prepareCompleted += OnPrepared;
            player.loopPointReached += OnReachedEnd;
            player.errorReceived += OnError;
        }

        /// <summary>
        /// 준비를 시작한다. 파일이 없다는 걸 미리 알 수 있을 때(에디터·데스크톱)만 false — 그 밖의 실패는
        /// <see cref="Tick"/>이 <see cref="State.Failed"/>로 알린다(Android는 StreamingAssets가 jar 안이라 미리 볼 수 없다).
        /// </summary>
        public bool Play()
        {
            Stop();
            EnsureComponents();
            if (player == null) { Current = State.Failed; return false; }

            string url = BuildUrl();
#if UNITY_EDITOR || UNITY_STANDALONE
            if (!System.IO.File.Exists(url))
            {
                Debug.LogWarning($"[DreamIntro] 파일이 없다: {url} — 글자 카드로 대신한다");
                Current = State.Failed;
                return false;
            }
#endif
            clip = Resources.Load<AudioClip>(AudioResourcePath);   // 없으면 무음으로 간다
            prepareSeconds = 0f;
            clock = 0f;
            lastVideoTime = 0f;
            stalledSeconds = 0f;
            Current = State.Preparing;
            ApplyDuck(true);
            player.url = url;
            player.Prepare();
            return true;
        }

        /// <summary>매 프레임(비스케일 시간). 시간 초과·멈춤·끝을 상태로 바꾼다.</summary>
        public void Tick(float dt)
        {
            switch (Current)
            {
                case State.Preparing:
                    prepareSeconds += Mathf.Min(dt, MaxFrameDelta);
                    if (prepareSeconds > PrepareTimeoutSeconds) Fail("준비 시간 초과");
                    break;

                case State.Playing:
                    // 앱을 백그라운드에서 되돌릴 때의 큰 dt가 시계를 한꺼번에 밀어 영상이 끝난 것으로 오판하지 않게 한다.
                    clock += Mathf.Min(dt, MaxFrameDelta);
                    // 디코더가 한 프레임에 서 버리면 오래 붙들지 않는다 — 영상 시각이 안 흐르는 시간을 센다.
                    // 첫 프레임 전(time == 0)은 세지 않는다.
                    float videoTime = (float)player.time;
                    if (videoTime > lastVideoTime + 0.001f) { lastVideoTime = videoTime; stalledSeconds = 0f; }
                    else if (lastVideoTime > 0f) stalledSeconds += Mathf.Min(dt, MaxFrameDelta);   // 끊긴 한 프레임을 멈춤으로 오인하지 않게

                    // 영상 시각이 기준이다 — 건너뛰기·되감기가 아니라 시작 지연이 만드는 어긋남만 바로잡는다.
                    if (clip != null && audioSource != null && audioSource.isPlaying && videoTime > 0f)
                    {
                        float want = Mathf.Min(videoTime, clip.length - 0.01f);
                        if (Mathf.Abs(audioSource.time - want) > AudioDriftSeconds) audioSource.time = want;
                    }

                    if (stalledSeconds > StallSeconds)
                    {
                        Debug.LogWarning("[DreamIntro] 재생이 멈춰 카드로 넘어간다");
                        Conclude(State.Ended);
                    }
                    else if (clock > DreamPrologueData.IntroSeconds + OverrunGraceSeconds) Conclude(State.Ended);
                    break;
            }
        }

        /// <summary>
        /// 화면 전체를 영상으로 덮는다(cover — 세로 화면은 가운데를 잘라 채운다). 준비 중에는 검은 화면 위에 점 하나만 —
        /// 멈춘 것처럼 보이지 않게(StoryVideoDirector와 같다).
        /// </summary>
        public void Draw(Rect full)
        {
            if (Current == State.Preparing)
            {
                float pulse = 0.35f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
                Color previous = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, pulse);
                GUI.DrawTexture(new Rect(full.width * 0.5f - 5f, full.height * 0.5f - 5f, 10f, 10f), Texture2D.whiteTexture);
                GUI.color = previous;
                return;
            }
            if (Current != State.Playing || texture == null) return;
            Rect uv = UIHelper.CalculateCoverUv(full, texture.width, texture.height);
            GUI.DrawTextureWithTexCoords(full, texture, uv, false);
        }

        /// <summary>앱이 백그라운드로 가면 영상과 소리를 함께 멈춘다.</summary>
        public void Pause(bool paused)
        {
            if (Current != State.Playing || player == null) return;
            if (paused) { player.Pause(); if (audioSource != null) audioSource.Pause(); }
            else { player.Play(); if (audioSource != null) audioSource.UnPause(); }
        }

        /// <summary>재생을 접고 처음 상태(Idle)로 돌아간다 — 두 번 불려도 안전하다.</summary>
        public void Stop()
        {
            if (player != null && IsActive) player.Stop();   // 준비 중이어도 부른다 — 진행 중인 Prepare를 취소해 디코더를 닫는다
            if (audioSource != null) audioSource.Stop();
            ApplyDuck(false);
            Current = State.Idle;
        }

        /// <summary>호스트가 사라질 때 — 구독과 렌더 텍스처를 풀어 준다.</summary>
        public void Dispose()
        {
            Stop();
            if (player != null)
            {
                player.prepareCompleted -= OnPrepared;
                player.loopPointReached -= OnReachedEnd;
                player.errorReceived -= OnError;
                player.targetTexture = null;
            }
            if (texture != null)
            {
                texture.Release();
                Object.Destroy(texture);
                texture = null;
            }
        }

        private void OnPrepared(VideoPlayer source)
        {
            if (Current != State.Preparing) return;
            Debug.Log($"[DreamIntro] 준비 완료 {prepareSeconds:0.00}초 ({source.width}x{source.height}, {source.frameRate:0.#}fps)");
            EnsureTexture((int)source.width, (int)source.height);
            source.targetTexture = texture;
            Current = State.Playing;
            clock = 0f;
            lastVideoTime = 0f;
            stalledSeconds = 0f;
            source.Play();
            if (clip != null && audioSource != null)
            {
                audioSource.clip = clip;
                audioSource.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(
                    GameConstants.PrefsKeys.MasterVolume, GameConstants.Defaults.MasterVolume));
                audioSource.time = 0f;
                audioSource.Play();
            }
        }

        private void OnReachedEnd(VideoPlayer source)
        {
            if (Current == State.Playing) Conclude(State.Ended);
        }

        private void OnError(VideoPlayer source, string message) => Fail(message);

        private void Fail(string reason)
        {
            Debug.LogWarning($"[DreamIntro] 재생 실패: {reason} — 글자 카드로 대신한다");
            Conclude(State.Failed);
        }

        // 끝낸 뒤에도 상태(Ended/Failed)는 호출부가 읽을 때까지 남긴다. 디코더·소리·BGM 더킹은 여기서 놓는다.
        private void Conclude(State result)
        {
            if (player != null) player.Stop();
            if (audioSource != null) audioSource.Stop();
            ApplyDuck(false);
            Current = result;
        }

        private void ApplyDuck(bool on)
        {
            if (ducked == on) return;
            ducked = on;
            if (AudioManager.Instance != null) AudioManager.Instance.SetBgmDuck(on ? BgmDuckFactor : 1f);
        }

        private void EnsureTexture(int width, int height)
        {
            if (width <= 0 || height <= 0) { width = 1280; height = 720; }
            if (texture != null && texture.width == width && texture.height == height) return;
            if (texture != null)
            {
                if (player != null) player.targetTexture = null;
                texture.Release();
                Object.Destroy(texture);
            }
            texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = "DreamIntroRT" };
            texture.Create();
        }
    }
}
