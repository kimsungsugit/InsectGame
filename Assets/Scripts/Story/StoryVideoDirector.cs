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
    /// 세 길로 튼다:
    /// <list type="number">
    /// <item><b>대사 뒤</b>(<c>StoryBeat.videoId</c>) — 대사를 닫은 뒤(<c>StoryBeatCompleted</c>). 전투 화면이 떠 있으면 미룬다.</item>
    /// <item><b>대사 앞</b>(<c>StoryBeat.introVideoId</c>) — <see cref="IStoryStagePrelude"/>로 대화창이 열리기 <b>전에</b>
    ///   튼다(<see cref="TryPlayPrelude"/>). 설명은 영상이 맡고 대사는 짧게 간다. 대화창의 연출 게이트는 하나라
    ///   <see cref="StoryPreludeChain"/>이 이 영상 → NPC 등장 연출 차례로 잇는다.</item>
    /// <item><b>다시보기</b>(<see cref="PlayReplay"/>) — 저널의 「▶ 영상」. 스토리 부수효과가 없다.</item>
    /// </list>
    /// 파일은 <c>Assets/StreamingAssets/Video/</c>에서 <c>VideoPlayer.url</c>로 스트리밍한다 —
    /// 임포터를 타지 않아 트랜스코딩·메모리 상주가 없고, Android의 <c>jar:file://</c> 경로도
    /// URL 재생은 그대로 지원한다. 소리는 mp4 안의 AAC를 <c>AudioSource</c>로 보낸다.
    ///
    /// <b>복귀 보장이 급소다.</b> 재생 중 조작을 막고 모달 스택을 잡으므로 어떤 경로로 끝나든
    /// (정상 종료·건너뛰기·디코더 오류·파일 없음·준비 시간 초과·재생 초과·비활성·재진입) <see cref="Stop"/> 하나로 되돌린다.
    /// 파일이 없거나 디코더가 실패해도 <b>진행은 잃지 않는다</b> — 대사 뒤 영상은 비트가 이미 완료됐고,
    /// 대사 앞 영상은 <see cref="Stop"/>이 마지막에 대사를 연다. 여기서 멈추면 <c>DrainPendingTriggers</c>가
    /// 모달 가드에 막혀 다음 비트가 영영 안 오고, 대사 앞 영상이면 그 비트가 <c>pendingBeatId</c>에 갇힌다.
    /// </summary>
    public class StoryVideoDirector : MonoBehaviour, IModalUI, IStoryStagePrelude
    {
        private const string VideoFolder = "Video";
        /// <summary>준비(디코더 열기)가 이 시간을 넘기면 포기한다. 저사양 기기의 첫 프레임 지연보다 넉넉하게.</summary>
        private const float PrepareTimeoutSeconds = 5f;
        /// <summary>디코더가 <c>loopPointReached</c>를 안 주는 기기 대비 — 저작 길이보다 이만큼 지나면 끝낸다.</summary>
        private const float OverrunGraceSeconds = 3f;
        /// <summary>재생 중 월드 BGM·환경음 배율.</summary>
        private const float BgmDuckFactor = 0.2f;

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
        // 포기 시계 — 전투 카메라가 결과 화면 밖에서 안 풀린 시간만 센다(StoryBattleWait, CutsceneDirector와 같다).
        // 결과 화면(이제 눌러야 닫힌다)과 모달(대사·메뉴)은 플레이어가 닫는 화면이라 세지 않는다.
        private float pendingSeconds;
        // 1대1·레이드 결과 화면이 떠 있는가 — 부트스트랩이 넘긴다(Story가 UI를 모르게 함수 하나).
        private System.Func<bool> battleResultShowing;

        // 대사 앞 영상이 끝나면 부를 것 — 대사를 연다(StoryPreludeChain이 그 사이에 NPC 등장 연출을 끼운다).
        // **재생이 실제로 시작된 뒤에만 건다**(PlayIntro). 그래서 Play 안의 재진입 Stop이 새 콜백을 부르는 일이 없고,
        // 시작 전에 끝난 영상(파일 없음)은 콜백 없이 false를 돌려 호출부가 곧바로 대사를 띄운다.
        // 건 뒤에는 Stop이 상태를 다 비운 **다음** 정확히 한 번 부른다(FinishPrelude).
        private System.Action onPreludeDone;

        /// <summary>
        /// 테스트 전용 — false면 재생 상태(모달·조작 잠금·콜백)만 세우고 파일 확인·디코더를 건너뛴다.
        /// PlayMode·배치 러너엔 디코더가 없어서(rules/testing.md) 종료 경로를 디코더 없이 두드리려고 둔다.
        /// 게임 코드는 건드리지 않는다(<c>IslandManager.PersistenceEnabled</c>와 같은 성격).
        /// </summary>
        internal bool DecoderEnabled = true;

        private GUIStyle subtitleStyle;
        private GUIStyle skipStyle;
        private bool stylesReady;

        // ── IModalUI ── ESC(Back키)로 건너뛴다. 등록하지 않으면 StoryDirector.ShouldDeferNow가
        // "화면에 아무것도 없다"고 판정해 영상 위로 다음 대사가 겹쳐 뜬다.
        public bool IsOpen => playing;
        public void CloseModal() => Stop();

        public bool IsPlaying => playing;

        /// <summary>지금 틀고 있는 영상 ID(안 틀면 null) — 배치 걸음(<c>StoryBeatWalkthrough</c>)의 보고서용.</summary>
        public string CurrentVideoId => playing && current != null ? current.videoId : null;

        /// <summary>대사 앞 영상을 틀고 있다 — 끝나면 대사가 열린다.</summary>
        public bool IsPlayingPrelude => playing && onPreludeDone != null;

        public void AutoWire(StoryDirector director, CameraFollower follower, PlayerMovement movement)
        {
            if (storyDirector == null) storyDirector = director;
            if (cameraFollower == null) cameraFollower = follower;
            if (playerMovement == null) playerMovement = movement;
            Subscribe();
        }

        /// <summary>
        /// 결과 화면 탐침. 결과 화면은 눌러야 닫혀서(<c>BattleResultRules</c>) 그 시간을 "굳은 전투 화면"으로 세면
        /// 보상을 오래 본 사람의 영상이 사라진다(<see cref="StoryBattleWait"/>). 없으면 예전처럼 결과 화면도 센다.
        /// </summary>
        public void AutoWire(System.Func<bool> resultShowing)
        {
            if (battleResultShowing == null) battleResultShowing = resultShowing;
        }

        private void Awake()
        {
            // 컴포넌트를 코드로 붙인다 — 이 프로젝트엔 프리팹이 없고 Bootstrap이 전부 EnsureComponent로 만든다.
            videoPlayer = gameObject.GetComponent<VideoPlayer>();
            if (videoPlayer == null) videoPlayer = gameObject.AddComponent<VideoPlayer>();
            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            // 오프닝과 달리 리스너 일시정지를 뚫지 않는다 — PlayScene에서 리스너를 멈추는 주체는 오프닝
            // 다시보기뿐이고, 그동안 이 영상이 들려서는 안 된다.
            audioSource.ignoreListenerPause = false;

            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.source = VideoSource.Url;
            // RenderTexture 모드에선 aspectRatio가 무시된다 — 화면 맞춤은 OnGUI의 CalculateCoverUv가 한다.
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
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
            // 대사 앞 영상 중이었다면 Stop이 대사를 연다 — 꺼진 채 기다리면 시간 초과가 영영 안 와 그 비트가 갇힌다.
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

        /// <summary>미뤄 둔 영상의 한 프레임. 무엇을 했는지 돌려준다(테스트가 본다).</summary>
        private StoryBattleWait.SceneStep TickPending(float deltaSeconds)
        {
            if (pending == null) return StoryBattleWait.SceneStep.None;

            // 전투 화면이 닫혔고 다른 모달(대사·컷신)도 없을 때만 튼다 — 모달 위로 시작하면 서로의 복구를 덮는다.
            // 상한은 결과 화면 밖에서 전투 카메라가 굳은 시간에만 걸린다 — 결과 화면을 오래 보거나 이어진 대사를 오래 읽어도 안 버린다.
            StoryBattleWait.SceneStep step = StoryBattleWait.TickScene(ref pendingSeconds, deltaSeconds,
                cameraFollower != null && cameraFollower.InBattleMode,
                StoryBattleWait.ReadProbe(battleResultShowing),
                ModalUIRegistry.IsAnyOpen());

            if (step == StoryBattleWait.SceneStep.Play)
            {
                StoryVideoDefinition queued = pending;
                pending = null;
                Play(queued);
            }
            else if (step == StoryBattleWait.SceneStep.GiveUp)
            {
                Debug.LogWarning("[StoryVideo] 전투 화면(결과 화면 밖)이 닫히지 않아 영상을 건너뛴다");
                pending = null;
            }
            return step;
        }

        // ==================== 대사 앞 영상 ====================

        /// <summary>
        /// 대사 <b>앞</b> 영상(<c>StoryBeat.introVideoId</c>). <see cref="InsectGame.UI.NpcDialogueUI"/>가 비트를 렌더하기 전에
        /// (<see cref="StoryPreludeChain"/>을 거쳐) 부른다.
        ///
        /// 계약: <b>true면 <paramref name="onDone"/>을 나중에 정확히 한 번 부른다</b>(끝·건너뛰기·ESC·디코더 오류·준비 시간 초과·
        /// 재생 초과·비활성·재진입 — 전부 <see cref="Stop"/>을 지난다). <b>false면 절대 안 부른다</b> — 호출부가 곧바로 대사를 띄운다.
        /// 영상이 없거나·모르는 ID거나·지휘자가 꺼져 있거나·시작도 못 하고 끝났으면(에디터·데스크톱의 파일 없음) false다.
        /// Android는 파일 유무를 미리 못 봐서(jar 경로) 파일 없음이 디코더 오류로 오고, 그건 true 쪽(나중에 한 번)이다.
        /// </summary>
        public bool TryPlayPrelude(StoryBeat beat, System.Action onDone)
        {
            if (beat == null || onDone == null) return false;
            if (string.IsNullOrEmpty(beat.introVideoId)) return false;

            // **죽었거나 꺼져 있으면 게이트를 걸지 않는다** — StoryStageDirector.TryPlayPrelude와 같은 함정이다.
            // 호출부의 null 검사는 인터페이스 참조 비교라 Unity의 파괴 검사가 안 걸리고, 이 컴포넌트는 World/ 아래라
            // 대화창(UI/)과 다른 루트다. 꺼진 채 true를 돌려주면 Update가 안 돌아 시간 초과가 영영 안 오고
            // onDone 미호출 → 그 비트가 pendingBeatId에 갇혀 캠페인이 멈춘다. Awake 전이면 VideoPlayer도 없다.
            if (!isActiveAndEnabled || videoPlayer == null) return false;

            if (!StoryVideoLibrary.TryGet(beat.introVideoId, out StoryVideoDefinition def))
            {
                // 오타를 조용히 넘기지 않는다 — 영상이 안 나오는 건 화면상 티가 안 난다(story_lint 검사 25가 먼저 잡는다).
                Debug.LogWarning($"[StoryVideo] 알 수 없는 introVideoId: '{beat.introVideoId}' (beat {beat.beatId}) — 대사로 넘어간다");
                return false;
            }
            return PlayIntro(def, onDone);
        }

        /// <summary>
        /// 대사 앞 영상을 틀고 콜백을 건다. <b>재생이 실제로 시작된 뒤에만 건다</b> — 먼저 걸면 <see cref="Play"/> 안의
        /// 재진입 <see cref="Stop"/>이 방금 건 콜백을 불러 버린다. 테스트는 정의를 직접 넘겨 이 경로를 두드린다.
        /// </summary>
        internal bool PlayIntro(StoryVideoDefinition definition, System.Action onDone)
        {
            if (definition == null || onDone == null) return false;
            try
            {
                Play(definition);
            }
            catch (System.Exception e)
            {
                // 재생 상태를 세우다 던졌다 — 콜백은 아직 안 걸었다. 화면·조작을 되돌리고 대사로 넘긴다.
                // (호출부가 예외를 받아 대사를 띄운 뒤 남은 재생이 시간 초과로 끝나며 콜백을 또 부르면 대사가 두 번 열린다.)
                Debug.LogError($"[StoryVideo] 대사 앞 영상 시작 실패({definition.videoId}) — 대사로 넘어간다: {e}");
                Stop();
                return false;
            }

            // 시작도 못 하고 끝났다(파일 없음 등) — 콜백 없이 false. 호출부가 곧바로 대사를 띄운다.
            if (!playing) return false;
            onPreludeDone = onDone;
            return true;
        }

        /// <summary>
        /// 대사 앞 영상의 콜백을 <b>상태를 다 비운 뒤</b> 한 번 부른다. 콜백(대사 열기·NPC 등장 연출)이 다시 조작을 잠그고
        /// 모달을 걸 수 있으므로 우리 복구가 먼저 끝나 있어야 한다. 콜백이 던져도 지휘자 상태는 이미 깨끗하고,
        /// 부른 쪽(OnGUI의 건너뛰기·재진입 Play)이 중간에 끊기지 않게 여기서 받는다.
        /// </summary>
        private void FinishPrelude()
        {
            System.Action done = onPreludeDone;
            onPreludeDone = null;
            if (done == null) return;
            try
            {
                done();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[StoryVideo] 대사 앞 영상 뒤 대사 열기 실패: {e}");
            }
        }

        // ==================== 다시보기 ====================

        /// <summary>
        /// 저널의 「▶ 영상」 — 이미 본 비트의 영상을 다시 튼다. <b>스토리 부수효과가 없다</b>(콜백·대결·완료 처리 없음).
        /// 건너뛰기·ESC는 평소와 같다. 재생을 시작했으면 true.
        /// false: 라이브러리에 없는 ID · 다른 영상이 재생 중(대사 앞 영상을 끊으면 그 대사가 다시보기 밑에서 열린다) ·
        /// 지휘자가 꺼져 있음 · 전투 화면 · 시작도 못 하고 끝남(에디터·데스크톱의 파일 없음).
        /// </summary>
        public bool PlayReplay(string videoId)
        {
            if (string.IsNullOrEmpty(videoId) || !StoryVideoLibrary.TryGet(videoId, out StoryVideoDefinition def))
                return false;
            if (playing) return false;
            if (!isActiveAndEnabled || videoPlayer == null) return false;
            if (cameraFollower != null && cameraFollower.InBattleMode) return false;

            Play(def);
            return playing;
        }

        // ==================== 재생 ====================

        public void Play(StoryVideoDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.fileName)) return;
            if (videoPlayer == null) return;

            // 재진입 가드 — 없으면 restoreFrozen이 자기가 만든 frozen=true를 읽어 복구를 건너뛴다(영구 먹통).
            // 대사 앞 영상이 걸려 있었다면 이 Stop이 그 대사를 연다(콜백은 한 번 — 재진입도 종료 경로다).
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
            // 월드 BGM·환경음을 낮춘다 — 영상 음성과 12~15초 내내 겹치지 않게. Stop()이 원복한다.
            if (AudioManager.Instance != null) AudioManager.Instance.SetBgmDuck(BgmDuckFactor);

            // 재생 중엔 설정 UI가 닫혀 있으니 시작 때 한 번만 읽는다(OpeningSceneController와 같다).
            audioSource.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(
                GameConstants.PrefsKeys.MasterVolume, GameConstants.Defaults.MasterVolume));

            if (!DecoderEnabled) return;   // 테스트 — 재생 상태만 세운다(디코더 없음)

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

        /// <summary>
        /// 재생 종료 — <b>모든 종료 경로가 여기로 모인다.</b> 두 번 불려도 안전하다.
        /// 대사 앞 영상이면 복구를 다 마친 뒤 <b>마지막에</b> 대사를 연다(<see cref="FinishPrelude"/> — 한 번만).
        /// </summary>
        public void Stop()
        {
            if (!playing)
            {
                ModalUIRegistry.Unregister(this);
                // 방어 — 정상 경로에선 늘 비어 있다(재생이 시작된 뒤에만 건다). 그래도 걸려 있으면 지금 풀어 대사를 연다.
                FinishPrelude();
                return;
            }

            playing = false;
            prepared = false;
            current = null;

            // 준비 중이어도 부른다 — 진행 중인 Prepare를 취소해 디코더를 닫는다(idle이면 no-op).
            if (videoPlayer != null) videoPlayer.Stop();

            if (AudioManager.Instance != null) AudioManager.Instance.SetBgmDuck(1f);
            if (playerMovement != null && !restoreFrozen) playerMovement.SetFrozen(false);
            ModalUIRegistry.Unregister(this);

            // **마지막에** — 콜백(대사 열기)이 다시 조작을 잠그고 모달을 건다. 우리 복구가 먼저 끝나 있어야 한다.
            FinishPrelude();
        }

        private void OnPrepared(VideoPlayer source)
        {
            if (!playing) return;
            EnsureRenderTexture((int)source.width, (int)source.height);
            source.targetTexture = renderTexture;
            prepared = true;
            elapsed = 0f;
            // 프리즈 타이머를 **실제 재생 시작** 기준으로 다시 감는다. Play()의 SetFrozen은 Prepare 시작
            // 시각이라, 준비 5초 + 영상 15초 + 여유 3초 = 23초가 AutoUnfreezeTime(20)을 넘어 검은 화면
            // 뒤에서 조작이 살아날 수 있었다. 재장전하면 최악 15 + 3 = 18초다.
            if (playerMovement != null) playerMovement.SetFrozen(true);
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
            // 연출은 timeScale에 끌려다니면 안 된다(전투 슬로모션 직후 재생될 수 있다).
            float dt = Time.unscaledDeltaTime;
            if (!playing)
            {
                TickPending(dt);
                return;
            }
            TickPlayback(dt);
        }

        /// <summary>재생 중의 한 프레임 — 준비 시간 초과·재생 초과 안전망(테스트는 시간을 넣어 직접 부른다).</summary>
        private void TickPlayback(float dt)
        {
            if (!playing) return;

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
            // 자막 띠·건너뛰기 자리는 순수 계산(StoryVideoScreenLayout) — 세로 화면에서 둘이 겹치지 않는 것을 테스트가 본다.
            HudFrame frame = HudFrame.Current;

            if (prepared && renderTexture != null)
            {
                // 16:9 마스터 하나로 세로 화면까지 덮는다 — 가운데를 잘라 채운다(cover).
                Rect uv = UIHelper.CalculateCoverUv(full, renderTexture.width, renderTexture.height);
                GUI.DrawTextureWithTexCoords(full, renderTexture, uv, false);
                DrawSubtitle(frame);
            }
            else
            {
                // 첫 프레임 전 — 검정 위에 작은 점 하나. 멈춘 것처럼 보이지 않게만.
                float pulse = 0.35f + 0.35f * Mathf.Sin(Time.unscaledTime * 4f);
                UISurface.Flat(new Rect(full.width * 0.5f - 5f, full.height * 0.5f - 5f, 10f, 10f),
                    new Color(1f, 1f, 1f, pulse));
            }

            // 건너뛰기 — ESC(ModalUIRegistry)와 이 탭. 모바일엔 Back키뿐이라 이 버튼이 실질 유일 수단이다.
            // 가로는 오른쪽 아래, 세로는 오른쪽 위(세로에선 자막 띠가 폭을 거의 다 써서 아래에 두면 겹친다).
            // 바탕은 어둡게 — 그림책 화풍의 밝은 장면 위에서는 흰 반투명 바탕(예전 0.22)이 밝은 글자와 함께 묻힌다.
            Rect skip = StoryVideoScreenLayout.Skip(frame);
            if (UISurface.Button(skip, "건너뛰기 ▶", new Color(0f, 0f, 0f, 0.55f), skipStyle))
                Stop();

            UIScale.End();
        }

        private void DrawSubtitle(HudFrame frame)
        {
            if (current == null || !StoryVideoTimeline.TryGetCue(current.cues, elapsed, out int index)) return;

            StoryVideoCue cue = current.cues[index];
            float alpha = CutsceneTimeline.SubtitleAlpha(cue.duration, StoryVideoTimeline.CueProgress(cue, elapsed));

            // 띠는 둥근 판(높이 120px대라 각지게 두지 않는다). 페이드는 GUI.color 알파로 곱한다 — 판 색을 알파마다 새로 구우면
            // 둥근 텍스처 캐시가 프레임마다 는다.
            Rect band = StoryVideoScreenLayout.SubtitleBand(frame);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            UISurface.Rounded(band, new Color(0f, 0f, 0f, StoryVideoScreenLayout.SubtitleBandAlpha));
            GUI.color = previous;

            // 글자 — 두 줄 칸. 길면 LabelFit이 줄인다. 밝은 장면에서 띠가 옅어지는 페이드 구간에도 읽히게 그림자 한 겹을 먼저 깐다.
            Rect text = StoryVideoScreenLayout.SubtitleText(band);
            Color c = subtitleStyle.normal.textColor;
            subtitleStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f * alpha);
            UIHelper.LabelFit(new Rect(text.x + 2f, text.y + 2f, text.width, text.height), cue.text, subtitleStyle);
            subtitleStyle.normal.textColor = new Color(c.r, c.g, c.b, alpha);
            UIHelper.LabelFit(text, cue.text, subtitleStyle);
            subtitleStyle.normal.textColor = c;
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            // 아이가 읽는 자막 — 36pt 굵게, 두 줄까지(StoryVideoScreenLayout이 칸을 잡는다).
            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = StoryVideoScreenLayout.SubtitleFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            subtitleStyle.normal.textColor = new Color(0.96f, 0.95f, 0.90f);
            // 건너뛰기는 자막과 따로 — 자막을 키워도 버튼(240×56) 안에 남는다.
            skipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = StoryVideoScreenLayout.SkipFontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false
            };
            skipStyle.normal.textColor = new Color(0.96f, 0.95f, 0.90f);
        }
    }
}
