using System;
using System.Collections;
using InsectGame.Capture;
using InsectGame.Core;
using InsectGame.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InsectGame.Opening
{
    /// <summary>
    /// PlayScene 상태를 스냅샷한 뒤 OpeningScene을 additive로 재생하고 정확히 복원한다.
    /// UI root가 이 컴포넌트의 조상이면 비활성화와 함께 coordinator도 멈추므로 replay를 거부한다.
    /// </summary>
    public class OpeningReplayCoordinator : MonoBehaviour, IOpeningReplayService, IModalUI
    {
        private enum ReplayState
        {
            Idle,
            Loading,
            Playing,
            Cleaning
        }

        private struct GameplaySnapshot
        {
            public bool UiRootActiveSelf;
            public bool PlayerMovementEnabled;
            public float TimeScale;
            public bool AudioListenerPaused;
            public bool CameraEnabled;
            public bool ListenerEnabled;
        }

        [Header("Replay Dependencies")]
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private GameObject playUiRoot;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private AudioListener gameplayListener;
        [SerializeField] private BattleScreenUI battleScreen;
        [SerializeField] private RaidBattleUI raidScreen;
        [SerializeField] private CaptureMinigameController captureMinigame;

        private ReplayState replayState;
        private GameplaySnapshot snapshot;
        private bool snapshotCaptured;
        private AsyncOperation loadOperation;
        private Scene openingScene;
        private OpeningSceneController openingController;

        public bool IsOpen => replayState != ReplayState.Idle;

        public bool CanReplay
        {
            get
            {
                if (!isActiveAndEnabled || replayState != ReplayState.Idle)
                    return false;
                if (!DependenciesReady() || IsCoordinatorUnderUiRoot())
                    return false;
                if (SceneManager.GetActiveScene().name != GameConstants.Scenes.Play)
                    return false;

                Scene existingOpening = SceneManager.GetSceneByName(GameConstants.Scenes.Opening);
                if (existingOpening.IsValid() && existingOpening.isLoaded)
                    return false;
                if (battleScreen.IsBattleActive || raidScreen.IsRaidActive || captureMinigame.IsActive)
                    return false;
                return true;
            }
        }

        public void AutoWire(
            PlayerMovement movement,
            GameObject uiRoot,
            Camera camera,
            AudioListener listener,
            BattleScreenUI battle,
            RaidBattleUI raid,
            CaptureMinigameController minigame)
        {
            playerMovement = movement;
            playUiRoot = uiRoot;
            gameplayCamera = camera;
            gameplayListener = listener;
            battleScreen = battle;
            raidScreen = raid;
            captureMinigame = minigame;
        }

        public bool TryReplay()
        {
            if (!CanReplay)
                return false;

            CaptureSnapshot();
            replayState = ReplayState.Loading;
            ApplyReplayBlock();
            ModalUIRegistry.Register(this);
            StartCoroutine(LoadAndStartReplay());
            return true;
        }

        public void CloseModal()
        {
            if (replayState == ReplayState.Playing && openingController != null)
                openingController.TrySkip();
        }

        private void OnDisable()
        {
            if (replayState != ReplayState.Idle)
                BeginCleanup(true);
        }

        private void OnDestroy()
        {
            if (replayState != ReplayState.Idle || snapshotCaptured)
                BeginCleanup(true);
            ModalUIRegistry.Unregister(this);
        }

        private bool DependenciesReady()
        {
            return playerMovement != null
                && playUiRoot != null
                && gameplayCamera != null
                && gameplayListener != null
                && battleScreen != null
                && raidScreen != null
                && captureMinigame != null;
        }

        private bool IsCoordinatorUnderUiRoot()
        {
            return playUiRoot != null && transform.IsChildOf(playUiRoot.transform);
        }

        private void CaptureSnapshot()
        {
            snapshot = new GameplaySnapshot
            {
                UiRootActiveSelf = playUiRoot.activeSelf,
                PlayerMovementEnabled = playerMovement.enabled,
                TimeScale = Time.timeScale,
                AudioListenerPaused = AudioListener.pause,
                CameraEnabled = gameplayCamera.enabled,
                ListenerEnabled = gameplayListener.enabled
            };
            snapshotCaptured = true;
        }

        /// <summary>
        /// 재생 중 게임플레이 차단. <b>순서가 의미를 갖는다 — 재배열 금지.</b>
        ///
        /// `playUiRoot.SetActive(false)`는 UI 루트 아래 40여 개 컴포넌트의 OnDisable을 발화시키고,
        /// 그중 `BattleScreenUI`/`RaidBattleUI`의 OnDisable에는 `if (Time.timeScale &lt; 0.99f)
        /// Time.timeScale = 1f`라는 슬로우모션 안전 복구가 들어 있다. `Time.timeScale = 0f`를
        /// 먼저 두면 그 복구가 즉시 1로 되돌려 **오프닝 뒤에서 게임 월드가 계속 돌아간다.**
        /// 그래서 UI를 먼저 끄고 timeScale은 마지막에 0으로 만든다.
        /// </summary>
        private void ApplyReplayBlock()
        {
            playUiRoot.SetActive(false);
            playerMovement.enabled = false;
            gameplayCamera.enabled = false;
            gameplayListener.enabled = false;
            AudioListener.pause = true;
            Time.timeScale = 0f;   // 반드시 마지막
        }

        private IEnumerator LoadAndStartReplay()
        {
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(
                    GameConstants.Scenes.Opening,
                    LoadSceneMode.Additive);
            }
            catch (Exception e)
            {
                FailReplay($"OpeningScene additive 로드 시작 실패: {e.Message}");
                yield break;
            }

            if (loadOperation == null)
            {
                FailReplay("OpeningScene additive 로드 작업이 생성되지 않았습니다.");
                yield break;
            }

            yield return loadOperation;
            loadOperation = null;

            if (replayState != ReplayState.Loading)
                yield break;

            openingScene = SceneManager.GetSceneByName(GameConstants.Scenes.Opening);
            if (!openingScene.IsValid() || !openingScene.isLoaded)
            {
                FailReplay("OpeningScene additive 로드 완료 후 씬을 찾지 못했습니다.");
                yield break;
            }

            openingController = FindControllerInLoadedScene(openingScene);
            if (openingController == null)
            {
                FailReplay("OpeningScene 루트에서 OpeningSceneController를 찾지 못했습니다.");
                yield break;
            }

            openingController.PlaybackEnded += OnPlaybackEnded;
            replayState = ReplayState.Playing;
            if (!openingController.StartReplay())
                FailReplay("OpeningSceneController가 replay 시작을 거부했습니다.");
        }

        private static OpeningSceneController FindControllerInLoadedScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                OpeningSceneController controller = roots[i].GetComponentInChildren<OpeningSceneController>(true);
                if (controller != null)
                    return controller;
            }
            return null;
        }

        private void OnPlaybackEnded(OpeningPlaybackResult result)
        {
            BeginCleanup(false);
        }

        private void FailReplay(string message)
        {
            Debug.LogError($"[OpeningReplayCoordinator] {message}");
            BeginCleanup(false);
        }

        private void BeginCleanup(bool immediate)
        {
            if (!immediate && (replayState == ReplayState.Idle || replayState == ReplayState.Cleaning))
                return;
            if (immediate && replayState == ReplayState.Idle && !snapshotCaptured)
                return;

            replayState = ReplayState.Cleaning;
            UnsubscribeController();
            if (immediate)
            {
                CleanupImmediately();
                return;
            }
            StartCoroutine(UnloadThenRestore());
        }

        private IEnumerator UnloadThenRestore()
        {
            Scene scene = ResolveOpeningScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                AsyncOperation unloadOperation = null;
                try
                {
                    unloadOperation = SceneManager.UnloadSceneAsync(scene);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[OpeningReplayCoordinator] OpeningScene unload 실패: {e.Message}");
                }

                if (unloadOperation != null)
                    yield return unloadOperation;
            }

            RestoreSnapshotOnce();
            FinishCleanup();
        }

        private Scene ResolveOpeningScene()
        {
            if (openingScene.IsValid() && openingScene.isLoaded)
                return openingScene;
            return SceneManager.GetSceneByName(GameConstants.Scenes.Opening);
        }

        private void UnsubscribeController()
        {
            if (openingController != null)
                openingController.PlaybackEnded -= OnPlaybackEnded;
            openingController = null;
        }

        private void RestoreSnapshotOnce()
        {
            if (!snapshotCaptured)
                return;

            // 각 값은 현재값을 추측하지 않고 replay 직전 값으로 정확히 되돌린다.
            if (playUiRoot != null) playUiRoot.SetActive(snapshot.UiRootActiveSelf);
            if (playerMovement != null) playerMovement.enabled = snapshot.PlayerMovementEnabled;
            if (gameplayCamera != null) gameplayCamera.enabled = snapshot.CameraEnabled;
            if (gameplayListener != null) gameplayListener.enabled = snapshot.ListenerEnabled;
            AudioListener.pause = snapshot.AudioListenerPaused;
            Time.timeScale = snapshot.TimeScale;
            snapshotCaptured = false;
        }

        private void FinishCleanup()
        {
            ModalUIRegistry.Unregister(this);
            openingScene = default(Scene);
            loadOperation = null;
            replayState = ReplayState.Idle;
        }

        private void CleanupImmediately()
        {
            if (loadOperation != null && !loadOperation.isDone)
                loadOperation.completed += UnloadOpeningWhenLoadCompletes;

            UnsubscribeController();
            Scene scene = ResolveOpeningScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                try
                {
                    SceneManager.UnloadSceneAsync(scene);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[OpeningReplayCoordinator] 파괴 중 OpeningScene unload 실패: {e.Message}");
                }
            }

            // 파괴/비활성화 중에는 coroutine 완료를 기다릴 수 없어 unload 요청 직후 즉시 복원한다.
            RestoreSnapshotOnce();
            FinishCleanup();
        }

        private static void UnloadOpeningWhenLoadCompletes(AsyncOperation operation)
        {
            Scene scene = SceneManager.GetSceneByName(GameConstants.Scenes.Opening);
            if (scene.IsValid() && scene.isLoaded)
                SceneManager.UnloadSceneAsync(scene);
        }
    }
}
