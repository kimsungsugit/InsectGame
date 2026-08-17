using InsectGame.Spawning;
using UnityEngine;
using UnityEngine.EventSystems;

namespace InsectGame.Core
{
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float rotationSpeed = 720f;
        [SerializeField] private float gravity = 20f;
        [SerializeField] private float groundCheckDistance = 2f;

        private RegionManager regionManager;
        private OutfitBonusProvider outfitBonus;

        // 장애물 검사 버퍼 — IsBlockedPosition이 이동 중 **매 프레임 2회** 불린다(다음 위치 차단
        // 판정 + 끼임 감지). Physics.OverlapSphere는 호출마다 Collider[]를 새로 할당하므로
        // 그대로 두면 걷는 내내 GC가 돈다. NonAlloc + 고정 버퍼로 바꾼다.
        // 16이면 반경 0.4~0.5m 구에 겹칠 콜라이더로 충분하고, 넘쳐도 잘릴 뿐 오판정은 없다
        // (버퍼가 찬다 = 이미 막을 것을 찾았다는 뜻이라 판정 결과가 바뀌지 않는다).
        private const int ObstacleBufferSize = 16;
        private readonly Collider[] obstacleBuffer = new Collider[ObstacleBufferSize];
        private PlayerStartPose mainWorldSafePose = PlayerStartPlacement.FallbackPose;
        private bool frozen;
        private float frozenTimer;
        private float verticalVelocity;
        private bool isGrounded;
        private Vector3 clickTarget;
        private bool movingToClick;
        // 진입 차단 안내 — 그리기는 PlayerHintOverlay(UI)가 한다. 문구는 설정 시 1회만 만든다.
        private string blockedMessage;
        private float blockedMsgTimer;

        // ── 자동 주행(메인퀘스트 목표로 이동) ──
        // 클릭 이동(clickTarget/movingToClick)을 그대로 재사용한다. 키보드·조이스틱 입력이
        // movingToClick을 끄는 기존 동작이 곧 "이동 중 조작하면 즉시 해제"라서 공짜로 따라온다.
        // 다른 점은 둘뿐 — 도착 반경이 넓고(대화 사거리), 막혔을 때 포기하지 않고 우회한다.
        private bool autoRunning;
        private float autoRunArriveRadius;
        // 우회가 계속 실패하면 영원히 벽을 밀게 된다 — 누적 시간이 넘으면 포기하고 알린다.
        private float autoRunBlockedTimer;
        private const float AutoRunGiveUpSeconds = 3f;
        // 사방이 막힌 건 아닌데 도착도 못 하는 경우(큰 바위를 빙빙 돎)를 잡는 정체 감지.
        // 목표까지 최단 거리가 갱신되면 리셋된다.
        private float autoRunStallTimer;
        private float autoRunBestDistance = float.MaxValue;
        private const float AutoRunStallSeconds = 5f;
        /// <summary>자동 주행이 우회에 실패해 스스로 멈췄다 — HUD가 안내 문구를 띄운다.</summary>
        public event System.Action AutoRunFailed;

        private bool guiKeyW, guiKeyA, guiKeyS, guiKeyD;
        private bool guiKeyUp, guiKeyDown, guiKeyLeft, guiKeyRight;
        private bool guiEscPressed;
        private bool guiClickRequest;
        private Vector2 guiClickPos;
        private bool guiUnstickPressed; // OnGUI Event 경로 F9 백업

        // 가상 조이스틱(터치) 입력 — VirtualJoystickUI(UI 계층)가 매 프레임 푸시. (UI→Core 허용)
        private Vector2 joystickInput;
        private bool joystickActive;


        private float walkAnimTimer;
        private bool isWalking;
        // AnimateWalk가 매 Update 프레임 호출되어 transform.Find 6번 발생. lazy 캐싱으로 차단.
        // LegPivot은 Leg+Boot 모두 자식으로 묶은 빈 컨테이너(PlayerVisualBuilder). Pivot 회전 시
        // Leg/Boot 둘 다 자동 전파 → 옛 cachedLegL/R 직접 회전(발 동기 누락) 회귀 차단.
        private Transform cachedArmL, cachedArmR, cachedLegPivotL, cachedLegPivotR, cachedBody, cachedHeadPivot;
        // 도구(NetHandle/NetRing)는 오른팔과 동기 회전 — 손과 분리되어 공중에 떠 있는 인상 차단.
        private Transform cachedNetHandle, cachedNetRing;
        private Vector3 netHandleBasePos, netRingBasePos;
        private Quaternion netHandleBaseRot, netRingBaseRot;
        private bool toolBaseCached;
        private float footstepTimer;
        private float bodyBaseY = float.NaN;
        // 잡기 액션 — 탭 시 오른팔/도구를 한 방향으로 크게 휘둘렀다 복귀(sin 아크).
        private float catchSwingTimer;
        private const float CatchSwingDuration = 0.42f;
        private const float CatchSwingMaxDeg = 72f;
        private float stuckTimer; // 끼임(embedded) 자동탈출용 — 이동 시도 중 콜라이더 박힘 지속시간

        public bool IsFrozen => frozen;
        public PlayerStartPose MainWorldSafePose => mainWorldSafePose;

        /// <summary>진입 차단 안내 문구. 없으면 null. 그리기는 <c>PlayerHintOverlay</c>(UI)가 한다.</summary>
        public string BlockedMessage => blockedMessage;

        /// <summary>차단 안내의 잔여 알파(0이면 표시 없음). 마지막 0.5초 동안 사라진다.</summary>
        public float BlockedMessageAlpha =>
            blockedMsgTimer > 0f ? Mathf.Clamp01(blockedMsgTimer / 0.5f) : 0f;

        // 잡기 버튼 탭 시 캐릭터가 도구를 휙 휘두르는 1회성 액션. CaptureInputController가 호출.
        public void PlayCatchSwing()
        {
            catchSwingTimer = CatchSwingDuration;
        }

        // 잡기 대상(곤충) 방향으로 캐릭터를 수평 회전(Y만) — 스윙이 곤충을 향하도록.
        public void FaceTowards(Vector3 worldPos)
        {
            Vector3 dir = worldPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0004f)
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        public void SetFrozen(bool value)
        {
            frozen = value;
            if (value) frozenTimer = 0f;
        }

        // 가상 조이스틱 입력 주입 — VirtualJoystickUI가 호출. dir: -1..1, active: 조작 중 여부.
        public void SetMoveInput(Vector2 dir, bool active)
        {
            joystickInput = dir;
            joystickActive = active;
        }

        private bool outfitSubscribed;

        private void OnEnable()
        {
            TrySubscribeOutfit();
        }

        private void Start()
        {
            TrySubscribeOutfit();
        }

        private void OnDisable()
        {
            if (outfitSubscribed && CharacterOutfitManager.Instance != null)
            {
                CharacterOutfitManager.Instance.OutfitChanged -= InvalidateToolBase;
            }
            outfitSubscribed = false;
        }

        private void TrySubscribeOutfit()
        {
            if (outfitSubscribed) return;
            CharacterOutfitManager mgr = CharacterOutfitManager.Instance;
            if (mgr == null) return;
            mgr.OutfitChanged += InvalidateToolBase;
            outfitSubscribed = true;
        }

        // 도구가 OutfitChanged로 좌표 재배치되면 다음 AnimateWalk가 새 base를 캐싱하도록 무효화.
        // walking 중에도 즉시 base 무효 — 이전엔 walking=false 1프레임까지 기다려 옛 base × swing 곱셈
        // 으로 도구가 잘못 회전됨.
        private void InvalidateToolBase()
        {
            toolBaseCached = false;
        }

        private void Update()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                EventSystem.current.SetSelectedGameObject(null);

            // F9: 끼임 escape — frozen 상태/모달 무관 항상 우선 처리.
            // OnGUI Event 경로(guiUnstickPressed)도 백업 (Unity Editor에서 F11 OS 인터셉트 우회).
            // 옛 F11은 Windows/Editor 풀스크린 토글로 잡혀 안 됐다는 사용자 보고 → F9로 변경.
            if (Input.GetKeyDown(KeyCode.F9) || guiUnstickPressed)
            {
                guiUnstickPressed = false;
                RecoverToSafePosition();
                if (frozen) frozen = false; // frozen 상태도 자동 해제 (끼임은 frozen 동반 가능)
                return;
            }

            if (frozen)
            {
                frozenTimer += Time.unscaledDeltaTime;
                if (frozenTimer > GameConstants.Player.AutoUnfreezeTime)
                {
                    frozen = false;
                }

                // **얼어 있어도 자세는 산다.** 예전엔 여기서 곧장 return해 AnimateWalk가 아예 안 불렸다
                // — 대화·컷신·연출이 전부 frozen이므로, 그때마다 플레이어가 **걷던 자세 그대로 굳었다**
                // (팔다리가 벌어진 채, 호흡도 없이). NPC는 숨을 쉬는데 플레이어만 마네킹이 된다.
                // 잡기 스윙 타이머도 여기서 줄인다 — 안 줄이면 스윙 도중 모달이 열렸을 때 팔이 든 채 남는다.
                // timeScale에 끌려다니면 안 되므로(컷신이 늦출 수 있다) unscaled를 쓴다.
                if (catchSwingTimer > 0f) catchSwingTimer -= Time.unscaledDeltaTime;
                walkAnimTimer = 0f;
                isWalking = false;
                footstepTimer = 0f;
                AnimateWalk(false);

                if (Input.GetKeyDown(KeyCode.Escape) || guiEscPressed)
                {
                    guiEscPressed = false;
                    // frozen + 모달 동시 상태: 모달 먼저 닫기 (모달 OnDisable이 SetFrozen(false) 호출).
                    // 옛은 frozen만 해제 → 모달 UI는 화면에 남고 PlayerMovement만 움직여 게임 깨짐.
                    if (InsectGame.UI.ModalUIRegistry.HandleEscape())
                    {
                        return;
                    }
                    frozen = false;
                }

                return;
            }

            // ESC: 활성 모달 우선 닫기 (한 번에 최상위 한 개만), 모달 없으면 기본 동작 (frozen 해제 등)
            if (Input.GetKeyDown(KeyCode.Escape) || guiEscPressed)
            {
                guiEscPressed = false;
                if (InsectGame.UI.ModalUIRegistry.HandleEscape())
                {
                    return; // 모달 닫음 — 이번 프레임 이동 처리 스킵
                }
            }

            // OnGUI KeyDown 래치(guiKey*)는 KeyUp 이벤트를 놓치면 켜진 채 stuck된다 — 에디터 Game뷰가
            // 포커스를 잃으면(다른 창 클릭 등) 키를 떼도 KeyUp이 안 와서 guiKeyW/guiKeyD 등이 true로 남아
            // 입력 없이 캐릭터가 자동 이동한다(사용자 보고: 오른쪽 위로 계속 감). 실제 물리 키가 안 눌린
            // 래치를 매 프레임 정리 — 포커스 복귀 시 Input.GetKey=false면 래치도 해제해 유령 이동을 차단.
            // (포커스가 있으면 Input.GetKey가 진실이라 백업 손실 없음. 포커스가 없으면 OnGUI 자체가 안 돎.)
            if (!Input.GetKey(KeyCode.W)) guiKeyW = false;
            if (!Input.GetKey(KeyCode.A)) guiKeyA = false;
            if (!Input.GetKey(KeyCode.S)) guiKeyS = false;
            if (!Input.GetKey(KeyCode.D)) guiKeyD = false;
            if (!Input.GetKey(KeyCode.UpArrow)) guiKeyUp = false;
            if (!Input.GetKey(KeyCode.DownArrow)) guiKeyDown = false;
            if (!Input.GetKey(KeyCode.LeftArrow)) guiKeyLeft = false;
            if (!Input.GetKey(KeyCode.RightArrow)) guiKeyRight = false;

            float h = 0f;
            float v = 0f;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) || guiKeyA || guiKeyLeft) h = -1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) || guiKeyD || guiKeyRight) h = 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || guiKeyW || guiKeyUp) v = 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || guiKeyS || guiKeyDown) v = -1f;

            bool hasKeyboard = h != 0f || v != 0f;
            // 가상 조이스틱(터치) — 키보드 입력 없을 때 사용. 아날로그 크기 보존(부분 기울임=부분 속도).
            bool hasJoystick = !hasKeyboard && joystickActive && joystickInput.sqrMagnitude > 0.0004f;

            // 옛: line 145에서 guiClickRequest=false 즉시 reset → line 149 ternary 항상 false.
            // guiClickPos 분기가 dead branch였음. 로컬에 보존 후 reset.
            bool useGuiClick = guiClickRequest;
            guiClickRequest = false;
            bool wantClick = Input.GetMouseButtonDown(0) || useGuiClick;

            if (wantClick && Camera.main != null)
            {
                Vector2 mousePos = useGuiClick ? guiClickPos : (Vector2)Input.mousePosition;

                bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                // OnGUI 모달 열려있으면 마우스 클릭 흡수 (캐릭터 이동 차단)
                bool anyModalOpen = InsectGame.UI.ModalUIRegistry.IsAnyOpen();
                // IMGUI 필드 버튼(잡기 등) 위 탭은 월드 클릭-이동으로 오인하지 않는다 — uGUI EventSystem은
                // IMGUI 버튼을 모르므로 pointerOverUI가 false라, 버튼 탭이 캐릭터를 화면 밖으로 걷게 했음.
                bool overFieldHud = InsectGame.UI.FieldHudInput.IsScreenPointOverHud(Input.mousePosition);
                if (!pointerOverUI && !anyModalOpen && !overFieldHud)
                {
                    Ray ray = Camera.main.ScreenPointToRay(mousePos);

                    int playerLayer = gameObject.layer;
                    Collider[] childColliders = GetComponentsInChildren<Collider>();
                    int[] childLayers = new int[childColliders.Length];
                    // try-finally: Physics.Raycast 예외 시 player layer 복원 누락으로 본인 raycast 통과 회귀 차단.
                    try
                    {
                        gameObject.layer = 2;
                        for (int i = 0; i < childColliders.Length; i++)
                        {
                            childLayers[i] = childColliders[i].gameObject.layer;
                            childColliders[i].gameObject.layer = 2;
                        }

                        if (Physics.Raycast(ray, out RaycastHit clickHit, 200f))
                        {
                            clickTarget = clickHit.point;
                            clickTarget.y = transform.position.y;
                            movingToClick = true;
                        }
                    }
                    finally
                    {
                        gameObject.layer = playerLayer;
                        for (int i = 0; i < childColliders.Length; i++)
                        {
                            if (childColliders[i] != null)
                                childColliders[i].gameObject.layer = childLayers[i];
                        }
                    }
                }
            }

            Vector3 direction;

            if (hasKeyboard)
            {
                movingToClick = false;
                if (autoRunning) EndAutoRun();   // 조작하면 자동 주행 즉시 해제
                direction = new Vector3(h, 0f, v).normalized;
            }
            else if (hasJoystick)
            {
                movingToClick = false;
                if (autoRunning) EndAutoRun();
                Vector3 jd = new Vector3(joystickInput.x, 0f, joystickInput.y);
                if (jd.sqrMagnitude > 1f) jd.Normalize(); // 반경 초과만 클램프 — 아날로그 속도 유지
                direction = jd;
            }
            else if (movingToClick)
            {
                Vector3 toTarget = clickTarget - transform.position;
                toTarget.y = 0f;
                float remaining = toTarget.magnitude;
                // 자동 주행은 대화 사거리에서 멈춘다. 클릭 이동의 0.5m를 그대로 쓰면 NPC 콜라이더에
                // 코를 박고서야 도착 판정이 나 "다 왔는데 말이 안 걸린다"로 보인다.
                if (remaining < (autoRunning ? autoRunArriveRadius : 0.5f))
                {
                    movingToClick = false;
                    if (autoRunning) EndAutoRun();
                    direction = Vector3.zero;
                }
                else
                {
                    direction = toTarget.normalized;
                    if (autoRunning) direction = SteerAroundObstacles(direction, remaining);
                }
            }
            else
            {
                direction = Vector3.zero;
            }

            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            float speedMul = Mathf.Clamp(outfitBonus != null ? outfitBonus.GetMoveSpeedMultiplier() : 1f, 0.5f, 2f);
            Vector3 move = direction * moveSpeed * speedMul * Time.deltaTime;

            isGrounded = Physics.Raycast(
                transform.position + Vector3.up * 0.5f,
                Vector3.down, out RaycastHit hit, groundCheckDistance);

            if (isGrounded)
            {
                verticalVelocity = 0f;
                Vector3 pos = transform.position;
                pos.y = Mathf.Max(pos.y, hit.point.y);
                transform.position = pos;
            }
            else
            {
                verticalVelocity -= gravity * Time.deltaTime;
            }

            move.y = verticalVelocity * Time.deltaTime;
            Vector3 nextPos = transform.position + move;

            bool hasMovement = move.x != 0f || move.z != 0f;
            if (hasMovement && regionManager != null && IsBlockedPosition(nextPos))
            {
                move.x = 0f;
                move.z = 0f;
                // 클릭 이동은 여기서 포기한다. 자동 주행은 포기하지 않는다 — SteerAroundObstacles가
                // 다음 프레임에 우회로를 찾고, 정말 사방이 막혔을 때만 스스로 멈춘다.
                if (!autoRunning) movingToClick = false;
            }

            // 모바일 끼임 안전망 — 현재 위치 자체가 콜라이더 안(embedded)인데 이동 시도가 계속되면 자동 탈출.
            // (벽을 향해 걷는 경우는 현재 위치가 clear라 트리거 안 됨 — 박힘만 감지.) 모바일엔 F9 키가 없어
            // SubArea 이탈 박힘 등에서 복구수단이 없던 것을 보완(IsBlockedPosition은 본인 콜라이더 제외).
            if (hasMovement && regionManager != null && IsBlockedPosition(transform.position))
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= 1.5f)
                {
                    stuckTimer = 0f;
                    RecoverToSafePosition();
                    move = Vector3.zero; // 텔레포트했으니 이번 프레임 이동 적용 안 함
                }
            }
            else
            {
                stuckTimer = 0f;
            }

            transform.position += move;

            // 걷기 애니메이션
            bool walking = direction.sqrMagnitude > 0.01f;
            if (walking)
            {
                walkAnimTimer += Time.deltaTime * 8f;
                isWalking = true;
                footstepTimer += Time.deltaTime;
                if (footstepTimer >= 0.4f)
                {
                    footstepTimer = 0f;
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.Footstep);
                }
            }
            else if (isWalking)
            {
                walkAnimTimer = 0f;
                isWalking = false;
                footstepTimer = 0f;
            }
            AnimateWalk(walking);

            if (blockedMsgTimer > 0f)
                blockedMsgTimer -= Time.deltaTime;
            if (catchSwingTimer > 0f)
                catchSwingTimer -= Time.deltaTime;
        }

        private void OnGUI()
        {
            Event e = Event.current;
            if (e != null)
            {
                if (e.type == EventType.KeyDown)
                {
                    switch (e.keyCode)
                    {
                        case KeyCode.W: guiKeyW = true; break;
                        case KeyCode.A: guiKeyA = true; break;
                        case KeyCode.S: guiKeyS = true; break;
                        case KeyCode.D: guiKeyD = true; break;
                        case KeyCode.UpArrow: guiKeyUp = true; break;
                        case KeyCode.DownArrow: guiKeyDown = true; break;
                        case KeyCode.LeftArrow: guiKeyLeft = true; break;
                        case KeyCode.RightArrow: guiKeyRight = true; break;
                        case KeyCode.Escape: guiEscPressed = true; break;
                        case KeyCode.F9: guiUnstickPressed = true; break;
                    }
                }
                else if (e.type == EventType.KeyUp)
                {
                    switch (e.keyCode)
                    {
                        case KeyCode.W: guiKeyW = false; break;
                        case KeyCode.A: guiKeyA = false; break;
                        case KeyCode.S: guiKeyS = false; break;
                        case KeyCode.D: guiKeyD = false; break;
                        case KeyCode.UpArrow: guiKeyUp = false; break;
                        case KeyCode.DownArrow: guiKeyDown = false; break;
                        case KeyCode.LeftArrow: guiKeyLeft = false; break;
                        case KeyCode.RightArrow: guiKeyRight = false; break;
                    }
                }
            }


            // **화면 문구는 여기서 그리지 않는다.** 이 컴포넌트의 OnGUI는 UIScale 밖이라 실제
            // 픽셀 좌표인데, 나머지 UI는 전부 가상 캔버스 안에서 그려진다 — 스케일이 1이 아닌
            // 기기(1.333 등)에서 이 문구만 다른 크기로 찍히고 `Screen.height - 50`은 제스처바
            // 아래로 들어간다. `BattleArenaController`가 같은 이유로 `BattleEffectTextOverlay`로
            // 옮겨졌고, 여기도 같은 형태로 `PlayerHintOverlay`(UI)가 대신 그린다.
            // 이 OnGUI에 남은 것은 키 래치·이벤트 처리뿐이다(그리기 아님).
        }

        private void AnimateWalk(bool walking)
        {
            // idle 호흡 파형(-1..1) — 멈춰 있을 때 완전 정지(조각상)를 막는다.
            // NpcWalkAnimator가 주민에게 쓰는 것과 **같은 파형·진폭**이다(1.5rad/s, 팔 ±1.2°,
            // 몸통 ±1.8cm, 고개 ±4.5°). 예전엔 플레이어만 이게 없어서, 숨 쉬는 주민 옆에
            // 마네킹처럼 서 있었다 — 대화 중에는 카메라가 둘을 나란히 잡으므로 특히 티가 났다.
            float idle = walking ? 0f : Mathf.Sin(Time.time * 1.5f);
            float idleArm = idle * 1.2f;

            float swing = walking ? Mathf.Sin(walkAnimTimer) : 0f;
            float swingDeg = swing * 25f;
            float bobY = walking
                ? Mathf.Abs(Mathf.Sin(walkAnimTimer * 2f)) * 0.06f
                : idle * 0.018f;

            // 팔 흔들기 (좌우 반대) — PlayerVisualBuilder 노드 안정적이므로 lazy 캐싱
            // Z 회전 6°는 PlayerVisualBuilder의 ArmL/R 초기 각도와 동기 (14° V자 어색함 차단).
            if (cachedArmL == null) cachedArmL = transform.Find("ArmL");
            if (cachedArmR == null) cachedArmR = transform.Find("ArmR");
            // 오른팔 X 회전 — 기본은 walk swing(-swingDeg), 잡기 액션 중엔 큰 sin 아크로 오버라이드.
            //
            // **도구용 각도(toolDeg)를 팔 각도와 따로 둔다.** 도구는 idle 미세 흔들림을 따라가면
            // 안 된다 — 아래 base 재캐싱이 `!walking && 스윙 없음`일 때 매 프레임 도는데, 그때
            // 도구가 idleArm만큼 돌아가 있으면 그 회전이 base로 누적돼 도구가 조금씩 드리프트한다
            // (잡기 스윙 중 재캐싱을 막아 둔 것과 정확히 같은 함정이다).
            // idle에서 toolDeg는 0이라 재캐싱이 무해한 항등이 된다.
            float toolDeg = -swingDeg;
            float rightArmDeg = toolDeg + idleArm;
            if (catchSwingTimer > 0f)
            {
                float cp = 1f - Mathf.Clamp01(catchSwingTimer / CatchSwingDuration); // 0→1
                toolDeg = Mathf.Sin(cp * Mathf.PI) * CatchSwingMaxDeg;               // 0→peak→0, 휙 휘두름
                rightArmDeg = toolDeg;   // 크게 휘두르는 중엔 호흡을 섞지 않는다
            }
            // Z=0 수직 자세 — 옛 ±6° V자 회전이 사용자 보고 "어깨 언밸런스" 원인. swing은 X축만.
            if (cachedArmL != null) cachedArmL.localRotation = Quaternion.Euler(swingDeg + idleArm, 0f, 0f);
            if (cachedArmR != null) cachedArmR.localRotation = Quaternion.Euler(rightArmDeg, 0f, 0f);

            // 도구 = 오른팔과 동일 X swing. walking=false면 swing=0이라 도구 레시피
            // (OutfitShapeLibrary)가 갓 설정한 좌표가 그대로 base로 캐싱됨 →
            // 도구 변경 후에도 멈춤 1프레임에 자동 재동기.
            // 이 두 노드를 캐싱하기 때문에 도구 레시피는 반드시 bind 모드여야 한다 —
            // 파괴·재생성하면 캐시가 파괴된 Transform을 가리켜 스윙이 죽는다.
            if (cachedNetHandle == null) cachedNetHandle = transform.Find("NetHandle");
            if (cachedNetRing == null) cachedNetRing = transform.Find("NetRing");
            // 잡기 스윙 중엔 base 재캐싱 금지 — 안 그러면 스윙된 회전이 base로 누적돼 도구가 드리프트.
            if (!walking && catchSwingTimer <= 0f && cachedNetHandle != null && cachedNetRing != null)
            {
                netHandleBaseRot = cachedNetHandle.localRotation;
                netRingBaseRot = cachedNetRing.localRotation;
                toolBaseCached = true;
            }
            if (toolBaseCached)
            {
                // idle 호흡이 빠진 toolDeg를 쓴다 — 위 재캐싱이 매 idle 프레임 도므로,
                // 여기에 idleArm을 섞으면 그 회전이 base로 누적돼 도구가 드리프트한다.
                if (cachedNetHandle != null)
                    cachedNetHandle.localRotation = Quaternion.Euler(toolDeg, 0f, 0f) * netHandleBaseRot;
                if (cachedNetRing != null)
                    cachedNetRing.localRotation = Quaternion.Euler(toolDeg, 0f, 0f) * netRingBaseRot;
            }

            // 다리 흔들기 (팔과 반대) — LegPivot 회전 시 Leg+Boot 모두 함께 전파.
            // 옛 LegL/R 직접 회전은 Boot이 Player 직접 자식이라 발 동기 누락 회귀 (사용자 보고).
            if (cachedLegPivotL == null) cachedLegPivotL = transform.Find("LegLPivot");
            if (cachedLegPivotR == null) cachedLegPivotR = transform.Find("LegRPivot");
            if (cachedLegPivotL != null) cachedLegPivotL.localRotation = Quaternion.Euler(-swingDeg * 0.8f, 0f, 0f);
            if (cachedLegPivotR != null) cachedLegPivotR.localRotation = Quaternion.Euler(swingDeg * 0.8f, 0f, 0f);

            // 몸통 미세 상하 바운스 (BuildPlayerVisual에서 정한 초기 Y를 baseline으로 사용)
            if (cachedBody == null) cachedBody = transform.Find("Body");
            if (cachedBody != null)
            {
                Vector3 bp = cachedBody.localPosition;
                if (float.IsNaN(bodyBaseY)) bodyBaseY = bp.y;
                bp.y = bodyBaseY + bobY;
                cachedBody.localPosition = bp;
            }

            // 머리 흔들림 — 걷기: 좌우 흔들. idle: 아주 느린 고개 스윙(~14s 주기, ±4.5°)으로
            // 주변을 둘러보는 인상을 준다(NpcWalkAnimator와 같은 값).
            if (cachedHeadPivot == null) cachedHeadPivot = transform.Find("HeadPivot");
            if (cachedHeadPivot != null)
            {
                float headTilt = walking
                    ? Mathf.Sin(walkAnimTimer * 0.5f) * 3f
                    : Mathf.Sin(Time.time * 0.45f) * 4.5f;
                cachedHeadPivot.localRotation = Quaternion.Euler(0f, headTilt, 0f);
            }
        }

        private bool IsBlockedPosition(Vector3 pos)
        {
            if (regionManager == null || regionManager.Regions == null) return false;

            // SubArea 안: region 가드는 무의미(다른 좌표계). collider 가드는 유지(벽 차단을 위해)
            bool inSubArea = regionManager.CurrentSubArea != null;
            if (!inSubArea)
            {
                foreach (var r in regionManager.Regions)
                {
                    if (r.ContainsPoint(pos) && !regionManager.IsRegionAccessible(r))
                    {
                        blockedMessage = $"{r.displayName} 진입에 레벨이 부족합니다!";
                        blockedMsgTimer = 2f;
                        return true;
                    }
                }
            }

            // 지형 장애물 체크: 본인 CapsuleCollider 직접 참조로 확실히 제외.
            // (옛 IsChildOf만으로는 새 CapsuleCollider center 변경 이후 충돌 회귀가 발생)
            int hitCount = Physics.OverlapSphereNonAlloc(pos + Vector3.up * 1.4f, 0.4f, obstacleBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                Collider h = obstacleBuffer[i];
                if (h == null || h.isTrigger) continue;
                if (h.gameObject == gameObject) continue;
                if (h.transform.IsChildOf(transform)) continue;
                if (h.attachedRigidbody != null && h.attachedRigidbody.gameObject == gameObject) continue;

                GameObject go = h.gameObject;

                // 곤충 엔티티는 부모 체인 어디에 있어도 통과 (BuildForBattle 구조 모델은 손자 단위 가능)
                if (go.GetComponentInParent<InsectEntity>() != null) continue;

                // Guardian 정적 장식(플랫폼/기둥/사인/오라)은 이름으로 통과 — 시각용일 뿐 차단 의도 없음
                if (go.name.StartsWith("Guardian_")) continue;

                if (h.bounds.size.y < 0.25f) continue;

                return true;
            }

            return false;
        }

        // F9 unstick — 끼임 escape. SubArea 안이면 SubAreaWorldBuilder의 FindSafeSpawnPosition과
        // 동일 패턴으로 벽 없는 입구 좌표 탐색. 메인 월드면 주입된 안전 시작 포즈를 기준으로 복구.
        internal void RecoverToSafePosition()
        {
            bool inSubArea = regionManager != null && regionManager.CurrentSubArea != null;
            Vector3 target;
            if (inSubArea)
            {
                // SubArea 안 — Origin 부근 spiral 탐색
                Vector3 origin = new Vector3(2000f, 0f, 2000f);
                target = FindClearSpot(origin + new Vector3(0f, 0.5f, -8f), origin);
            }
            else
            {
                // 메인 월드 안전 시작 위치
                target = FindClearSpot(mainWorldSafePose.Position, mainWorldSafePose.Position);
            }
            transform.position = target;
            if (!inSubArea)
                transform.rotation = mainWorldSafePose.Rotation;
            verticalVelocity = 0f;
            movingToClick = false;
        }

        private Vector3 FindClearSpot(Vector3 preferred, Vector3 fallbackCenter)
        {
            if (IsClearAt(preferred)) return preferred;
            float[] radii = { 3f, 5f, 7f };
            for (int r = 0; r < radii.Length; r++)
            {
                for (int dir = 0; dir < 8; dir++)
                {
                    float angle = dir * 45f * Mathf.Deg2Rad;
                    Vector3 candidate = preferred + new Vector3(
                        Mathf.Sin(angle) * radii[r], 0f, Mathf.Cos(angle) * radii[r]);
                    if (IsClearAt(candidate)) return candidate;
                }
            }
            return fallbackCenter + new Vector3(0f, 0.5f, 0f);
        }

        // 안전 위치 탐색용 — 후보를 여러 개 훑으므로 한 번의 복구에서 여러 번 불린다.
        // IsBlockedPosition과 같은 버퍼를 쓴다: 둘은 호출이 겹치지 않는다(끼임 판정이 끝난 뒤에야
        // RecoverToSafePosition이 불린다). 중첩 호출을 만들면 서로의 결과를 덮으니 주의.
        private bool IsClearAt(Vector3 pos)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(pos + Vector3.up * 1.0f, 0.5f, obstacleBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                Collider h = obstacleBuffer[i];
                if (h == null || h.isTrigger) continue;
                if (h.gameObject == gameObject) continue;
                if (h.transform.IsChildOf(transform)) continue;
                if (h.attachedRigidbody != null && h.attachedRigidbody.gameObject == gameObject) continue;
                if (h.bounds.size.y < 0.5f) continue;
                return false;
            }
            return true;
        }

        // ================= 자동 주행 =================

        /// <summary>자동 주행 중인가 — HUD가 버튼 라벨을 "이동 취소"로 바꾼다.</summary>
        public bool IsAutoRunning => autoRunning;

        /// <summary>
        /// 목표 지점까지 자동으로 걸어간다. <paramref name="arriveRadius"/>는 도착 판정 반경으로,
        /// 대화가 걸리는 거리를 넘겨야 "도착했는데 말이 안 걸린다"가 안 생긴다.
        /// frozen(모달·대화 중)이면 시작하지 않는다.
        /// </summary>
        public void BeginAutoRun(Vector3 worldTarget, float arriveRadius)
        {
            if (frozen) return;
            clickTarget = new Vector3(worldTarget.x, transform.position.y, worldTarget.z);
            movingToClick = true;
            autoRunning = true;
            autoRunArriveRadius = Mathf.Max(0.5f, arriveRadius);
            autoRunBlockedTimer = 0f;
            autoRunStallTimer = 0f;
            autoRunBestDistance = float.MaxValue;
        }

        /// <summary>사용자가 취소하거나 목표가 사라졌을 때. 이벤트는 쏘지 않는다.</summary>
        public void CancelAutoRun()
        {
            if (!autoRunning) return;
            movingToClick = false;
            EndAutoRun();
        }

        // 주행 상태만 정리한다. movingToClick은 호출부 사정에 따라 다르므로 건드리지 않는다
        // (도착 시엔 이미 꺼져 있고, 입력 취소 경로에선 그쪽이 먼저 껐다).
        private void EndAutoRun()
        {
            autoRunning = false;
            autoRunBlockedTimer = 0f;
            autoRunStallTimer = 0f;
        }

        // 목표 방향이 막혔으면 좌우로 틀어 우회로를 찾는다. 0°(직진)부터 시도하므로
        // 열려 있으면 그대로 직진한다. 전부 막히면 잠시 밀어 보다가 포기한다.
        private static readonly float[] AutoRunSteerAngles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f };

        private Vector3 SteerAroundObstacles(Vector3 desired, float remainingDistance)
        {
            // 목표에 가까워지고 있으면 정체 타이머를 되돌린다. 이게 없으면 큰 바위를 빙 도는 동안
            // 매 방향이 열려 있어 blockedTimer는 0인데 영영 도착하지 못하는 경우를 못 잡는다.
            if (remainingDistance < autoRunBestDistance - 0.5f)
            {
                autoRunBestDistance = remainingDistance;
                autoRunStallTimer = 0f;
            }
            else
            {
                autoRunStallTimer += Time.deltaTime;
                if (autoRunStallTimer >= AutoRunStallSeconds)
                {
                    GiveUpAutoRun();
                    return desired;
                }
            }

            float probe = Mathf.Max(1.2f, moveSpeed * 0.35f);
            for (int i = 0; i < AutoRunSteerAngles.Length; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(AutoRunSteerAngles[i], Vector3.up) * desired;
                // IsClearAt은 IsBlockedPosition과 버퍼를 공유하지만 둘은 **순차** 호출이라 안전하다
                // (여기서 다 쓴 뒤에야 아래쪽 이동 적용부가 IsBlockedPosition을 부른다). 중첩만 금물.
                if (IsClearAt(transform.position + dir * probe))
                {
                    autoRunBlockedTimer = 0f;
                    return dir;
                }
            }

            autoRunBlockedTimer += Time.deltaTime;
            if (autoRunBlockedTimer >= AutoRunGiveUpSeconds) GiveUpAutoRun();
            return desired;
        }

        private void GiveUpAutoRun()
        {
            movingToClick = false;
            EndAutoRun();
            AutoRunFailed?.Invoke();
        }

        public void AutoWire(RegionManager rm)
        {
            if (regionManager == null) regionManager = rm;
        }

        public void AutoWire(PlayerStartPose pose)
        {
            mainWorldSafePose = pose;
        }

        public void AutoWire(OutfitBonusProvider bonus)
        {
            if (outfitBonus == null) outfitBonus = bonus;
        }
    }
}
