using InsectGame.Core;
using InsectGame.UI;
using UnityEngine;

namespace InsectGame.Story
{
    /// <summary>
    /// 프로시저럴 컷신 재생기 — 카메라 워크 + 흔들림 + 딤 + 자막을 시간축으로 돌린다.
    /// 에셋을 쓰지 않는다(이 게임엔 영상도 컷신 스틸도 없다).
    ///
    /// <b>스토리 비트의 대사가 끝난 뒤</b>(<c>StoryBeatCompleted</c>) 재생한다. 발화 시점에 걸면
    /// 대사 모달과 화면을 다투게 되고, 읽는 순서도 "장면을 보고 나서 대사"가 되어 어색하다.
    ///
    /// <b>복귀 보장이 이 클래스의 급소다.</b> 재생 중에는 조작을 막고 카메라를 뺏으므로,
    /// 어떤 경로로 끝나든(정상 종료·ESC·컴포넌트 비활성·씬 전환) 반드시 되돌려야 한다.
    /// 그래서 <see cref="Stop"/> 하나에 복구를 모으고 <c>OnDisable</c>도 그걸 부른다 —
    /// 오프닝 다시보기가 UI 루트를 껐다 켜는 경로(<c>rules/ui-layout.md</c>의 구독 회귀 계열)와
    /// 같은 함정이 여기도 있다. 못 되돌리면 <b>영구히 움직일 수 없는 상태</b>가 된다.
    /// </summary>
    public class CutsceneDirector : MonoBehaviour, IModalUI
    {
        private StoryDirector storyDirector;
        private CameraFollower cameraFollower;
        private PlayerMovement playerMovement;
        private Transform playerTransform;

        private CutsceneShot[] shots;
        private float elapsed;
        private int lastShotIndex = -1;
        private bool playing;

        // 복귀는 **원래 상태로** 해야 한다 — 고정값으로 되돌리면 컷신 전에 다른 시스템이 걸어 둔
        // 상태를 지운다. `BattleWin` 트리거 비트(fin_seal 등)는 전투 직후에 발화하므로
        // 배틀 카메라가 아직 살아 있을 수 있고, 거기서 ExitBattleMode를 무조건 부르면
        // 전투 화면의 카메라가 컷신 때문에 풀린다.
        private bool restoreBattleMode;
        private bool restoreFrozen;
        private Vector3 restoreBattlePos;
        private Quaternion restoreBattleRot;

        private GUIStyle subtitleStyle;
        private bool stylesReady;

        // ── IModalUI ── 재생 중 ESC로 건너뛸 수 있어야 한다. 건너뛰기가 없으면
        // 연출이 길게 느껴질 때 갇힌 기분이 들고, 무엇보다 버그로 안 끝났을 때 탈출구가 없다.
        public bool IsOpen => playing;
        public void CloseModal() => Stop();

        public bool IsPlaying => playing;

        public void AutoWire(StoryDirector director, CameraFollower follower,
            PlayerMovement movement, Transform player)
        {
            if (storyDirector == null) storyDirector = director;
            if (cameraFollower == null) cameraFollower = follower;
            if (playerMovement == null) playerMovement = movement;
            if (playerTransform == null) playerTransform = player;
            Subscribe();
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
            // 재생 중 비활성화되면 카메라·조작이 뺏긴 채로 남는다 — 반드시 되돌린다.
            Stop();
        }

        private void OnBeatCompleted(StoryBeat beat)
        {
            if (beat == null || string.IsNullOrEmpty(beat.cutsceneId)) return;
            if (!CutsceneLibrary.TryGet(beat.cutsceneId, out CutsceneShot[] loaded))
            {
                // 오타를 조용히 넘기지 않는다 — 컷신이 안 나오는 건 화면상 티가 안 난다.
                Debug.LogWarning($"[Cutscene] 알 수 없는 cutsceneId: '{beat.cutsceneId}' (beat {beat.beatId})");
                return;
            }
            Play(loaded);
        }

        public void Play(CutsceneShot[] definition)
        {
            if (definition == null || definition.Length == 0) return;
            if (playerTransform == null || cameraFollower == null) return;

            // **재진입 가드 — 없으면 영구 먹통이 된다.**
            // 재생 중에 Play가 다시 불리면 아래의 "되돌릴 상태 기록"이 컷신 1번이 스스로 만든 값
            // (battleMode=true, frozen=true)을 읽는다. 그러면 Stop()이 복구를 **둘 다 건너뛰어**
            // 조작이 영영 안 돌아오고 카메라도 배틀 모드에 갇힌다.
            // 먼저 Stop()으로 1번을 정상 종료시켜 원래 상태를 되돌린 뒤 새로 시작한다
            // (겸사겸사 ModalUIRegistry 중복 등록도 사라진다).
            if (playing) Stop();

            shots = definition;
            elapsed = 0f;
            lastShotIndex = -1;
            playing = true;

            // 되돌릴 상태를 먼저 기록한다(아래에서 덮어쓰기 전에).
            restoreBattleMode = cameraFollower.InBattleMode;
            restoreFrozen = playerMovement != null && playerMovement.IsFrozen;
            // 배틀 구도도 함께 기억한다 — restoreBattleMode면 ExitBattleMode를 건너뛰는데,
            // 그때 battlePos/battleRot는 컷신 마지막 컷 값으로 남아 전투 카메라가 그 구도로 굳는다.
            cameraFollower.GetBattleFraming(out restoreBattlePos, out restoreBattleRot);

            if (playerMovement != null)
            {
                playerMovement.CancelAutoRun();   // 자동 주행 중이면 먼저 끊는다
                playerMovement.SetFrozen(true);
            }
            ModalUIRegistry.Register(this);
        }

        /// <summary>
        /// 재생 종료 — <b>모든 종료 경로가 여기로 모인다.</b> 두 번 불려도 안전하다.
        /// </summary>
        public void Stop()
        {
            if (!playing)
            {
                // 재생 중이 아니어도 레지스트리 정리는 해 둔다(중복 등록 잔재 방지).
                ModalUIRegistry.Unregister(this);
                return;
            }

            playing = false;
            shots = null;
            lastShotIndex = -1;

            // 원래 상태로 되돌린다 — 컷신이 시작될 때 이미 배틀 카메라였거나 이미 frozen이었다면
            // 그건 다른 시스템이 걸어 둔 것이라 여기서 풀면 안 된다.
            if (cameraFollower != null)
            {
                if (restoreBattleMode)
                {
                    // 배틀 카메라를 유지하되 **구도는 원래대로** 돌려놓는다. 안 그러면 전투 화면이
                    // 컷신 마지막 컷의 구도로 굳는다(LateUpdate가 battlePos를 매 프레임 적용한다).
                    cameraFollower.EnterBattleModeFramed(
                        restoreBattlePos, restoreBattlePos + restoreBattleRot * Vector3.forward);
                }
                else
                {
                    cameraFollower.ExitBattleMode();
                }
            }
            if (playerMovement != null && !restoreFrozen) playerMovement.SetFrozen(false);
            ModalUIRegistry.Unregister(this);
        }

        private void Update()
        {
            if (!playing) return;

            // 컷신은 연출이라 timeScale에 끌려다니면 안 된다(전투 슬로모션 직후 재생될 수 있다).
            elapsed += Time.unscaledDeltaTime;

            if (!CutsceneTimeline.TryGetShot(shots, elapsed, out int index, out float t))
            {
                Stop();
                return;
            }

            CutsceneShot shot = shots[index];

            // 컷이 바뀌는 순간 1회만 터뜨린다 — Update에서 매 프레임 부르면 흔들림이 안 멈춘다.
            if (index != lastShotIndex)
            {
                lastShotIndex = index;
                if (shot.shake > 0f && cameraFollower != null)
                    cameraFollower.Shake(shot.shake, Mathf.Min(0.6f, shot.duration));
            }

            // 좌표는 플레이어 기준 상대 — 유적 지하처럼 원점이 다른 서브에리어에서도 맞는다.
            Vector3 origin = playerTransform.position;
            Vector3 camPos = origin + CutsceneTimeline.CameraOffsetAt(shot, t);
            Vector3 lookAt = origin + shot.lookAt;

            // 배틀 카메라 경로를 재사용한다 — LateUpdate가 매 프레임 battlePos를 적용하므로
            // 매 프레임 갱신하면 그대로 카메라 워크가 된다(그 경로만이 팔로우를 이긴다).
            if (cameraFollower != null) cameraFollower.EnterBattleModeFramed(camPos, lookAt);
        }

        private void OnGUI()
        {
            if (!playing || shots == null) return;
            if (!CutsceneTimeline.TryGetShot(shots, elapsed, out int index, out float t)) return;

            EnsureStyles();
            UIScale.Begin();

            CutsceneShot shot = shots[index];

            if (shot.dim > 0f) UISurface.Dim(shot.dim);

            if (!string.IsNullOrEmpty(shot.subtitle))
            {
                float alpha = CutsceneTimeline.SubtitleAlpha(shot.duration, t);
                float w = Mathf.Min(1100f, UIScale.VirtualScreenWidth
                    - UIScale.VirtualSafeLeft - UIScale.VirtualSafeRight - 80f);
                float x = UIScale.VirtualSafeLeft
                    + (UIScale.VirtualScreenWidth - UIScale.VirtualSafeLeft - UIScale.VirtualSafeRight - w) * 0.5f;
                // 하단 앵커 — 화면 가운데를 비워 연출을 가리지 않는다.
                float y = UISafeLayout.BottomY(150f);

                // 자막 밑 그림자 띠 — 밝은 배경에서도 글자가 읽히게.
                UISurface.Flat(new Rect(x, y, w, 96f), new Color(0f, 0f, 0f, 0.55f * alpha));

                Color c = subtitleStyle.normal.textColor;
                subtitleStyle.normal.textColor = new Color(c.r, c.g, c.b, alpha);
                // 자막 길이는 저작이 정하고 상자는 고정이라 LabelFit으로 줄여 맞춘다(rules/ui-layout.md).
                UIHelper.LabelFit(new Rect(x + 24f, y + 8f, w - 48f, 80f), shot.subtitle, subtitleStyle);
                subtitleStyle.normal.textColor = c;
            }

            // 건너뛰기 안내 — 갇힌 기분을 막는다. 실제 처리는 ESC(ModalUIRegistry)와 이 탭이다.
            float skipW = 240f, skipH = 56f;
            Rect skip = new Rect(
                UIScale.VirtualScreenWidth - UIScale.VirtualSafeRight - skipW - 24f,
                UISafeLayout.BottomY(skipH),
                skipW, skipH);
            if (UISurface.Button(skip, "건너뛰기 ▶", new Color(1f, 1f, 1f, 0.22f), subtitleStyle))
                Stop();

            UIScale.End();
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
