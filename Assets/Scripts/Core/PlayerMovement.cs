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
        private bool frozen;
        private float frozenTimer;
        private float verticalVelocity;
        private bool isGrounded;
        private bool hasReceivedInput;
        private Vector3 clickTarget;
        private bool movingToClick;
        private string blockedRegionName;
        private float blockedMsgTimer;

        private bool guiKeyW, guiKeyA, guiKeyS, guiKeyD;
        private bool guiKeyUp, guiKeyDown, guiKeyLeft, guiKeyRight;
        private bool guiEscPressed;
        private bool guiClickRequest;
        private Vector2 guiClickPos;
        private bool guiUnstickPressed; // OnGUI Event 경로 F9 백업

        // 가상 조이스틱(터치) 입력 — VirtualJoystickUI(UI 계층)가 매 프레임 푸시. (UI→Core 허용)
        private Vector2 joystickInput;
        private bool joystickActive;

        // OnGUI 매 프레임 new GUIStyle 회귀 차단 — DexScreenUI/BattleScreenUI 패턴.
        private GUIStyle hintStyle, frozenStyle, blockStyle;
        private bool stylesInited;
        private static readonly Color HintBgCol = new Color(0f, 0f, 0f, 0.7f);
        private static readonly Color FrozenTextCol = new Color(1f, 1f, 0.5f, 0.7f);

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

        public bool IsFrozen => frozen;

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
                UnstickToSafePosition();
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

            float h = 0f;
            float v = 0f;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) || guiKeyA || guiKeyLeft) h = -1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) || guiKeyD || guiKeyRight) h = 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || guiKeyW || guiKeyUp) v = 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || guiKeyS || guiKeyDown) v = -1f;

            bool hasKeyboard = h != 0f || v != 0f;
            // 가상 조이스틱(터치) — 키보드 입력 없을 때 사용. 아날로그 크기 보존(부분 기울임=부분 속도).
            bool hasJoystick = !hasKeyboard && joystickActive && joystickInput.sqrMagnitude > 0.0004f;
            if (hasKeyboard || hasJoystick) hasReceivedInput = true;

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
                if (!pointerOverUI && !anyModalOpen)
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
                            hasReceivedInput = true;
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
                direction = new Vector3(h, 0f, v).normalized;
            }
            else if (hasJoystick)
            {
                movingToClick = false;
                Vector3 jd = new Vector3(joystickInput.x, 0f, joystickInput.y);
                if (jd.sqrMagnitude > 1f) jd.Normalize(); // 반경 초과만 클램프 — 아날로그 속도 유지
                direction = jd;
            }
            else if (movingToClick)
            {
                Vector3 toTarget = clickTarget - transform.position;
                toTarget.y = 0f;
                if (toTarget.magnitude < 0.5f)
                {
                    movingToClick = false;
                    direction = Vector3.zero;
                }
                else
                {
                    direction = toTarget.normalized;
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
                movingToClick = false;
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

            InitStyles();

            if (!hasReceivedInput && !frozen)
            {
                float w = 500f;
                float h = 80f;
                Rect rect = new Rect((Screen.width - w) / 2f, Screen.height * 0.4f, w, h);

                GUI.color = HintBgCol;
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(rect, "좌하단 조이스틱 또는 화면 터치로 이동하세요", hintStyle);
            }

            if (frozen)
            {
                GUI.Label(new Rect(0, Screen.height - 50, Screen.width, 30),
                    "ESC를 누르면 이동 잠금을 해제합니다", frozenStyle);
            }

            if (blockedMsgTimer > 0f)
            {
                float alpha = Mathf.Clamp01(blockedMsgTimer / 0.5f);
                // alpha 동적이라 textColor 매 프레임 갱신 (BattleScreenUI 패턴) — struct stack 할당.
                blockStyle.normal.textColor = new Color(1f, 0.4f, 0.3f, alpha);
                float bw = 400f;
                Rect bRect = new Rect((Screen.width - bw) / 2f, Screen.height * 0.6f, bw, 30);
                GUI.Label(bRect, $"{blockedRegionName} 진입에 레벨이 부족합니다!", blockStyle);
            }

        }

        private void InitStyles()
        {
            if (stylesInited) return;
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            hintStyle.normal.textColor = Color.white;

            frozenStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter
            };
            frozenStyle.normal.textColor = FrozenTextCol;

            blockStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            stylesInited = true;
        }

        private void AnimateWalk(bool walking)
        {
            float swing = walking ? Mathf.Sin(walkAnimTimer) : 0f;
            float swingDeg = swing * 25f;
            float bobY = walking ? Mathf.Abs(Mathf.Sin(walkAnimTimer * 2f)) * 0.06f : 0f;

            // 팔 흔들기 (좌우 반대) — PlayerVisualBuilder 노드 안정적이므로 lazy 캐싱
            // Z 회전 6°는 PlayerVisualBuilder의 ArmL/R 초기 각도와 동기 (14° V자 어색함 차단).
            if (cachedArmL == null) cachedArmL = transform.Find("ArmL");
            if (cachedArmR == null) cachedArmR = transform.Find("ArmR");
            // Z=0 수직 자세 — 옛 ±6° V자 회전이 사용자 보고 "어깨 언밸런스" 원인. swing은 X축만.
            if (cachedArmL != null) cachedArmL.localRotation = Quaternion.Euler(swingDeg, 0f, 0f);
            if (cachedArmR != null) cachedArmR.localRotation = Quaternion.Euler(-swingDeg, 0f, 0f);

            // 도구 = 오른팔과 동일 X swing. walking=false면 swing=0이라 ApplyToolShape이 갓 설정한
            // 좌표가 그대로 base로 캐싱됨 → 도구 변경 후에도 멈춤 1프레임에 자동 재동기.
            if (cachedNetHandle == null) cachedNetHandle = transform.Find("NetHandle");
            if (cachedNetRing == null) cachedNetRing = transform.Find("NetRing");
            if (!walking && cachedNetHandle != null && cachedNetRing != null)
            {
                netHandleBaseRot = cachedNetHandle.localRotation;
                netRingBaseRot = cachedNetRing.localRotation;
                toolBaseCached = true;
            }
            if (toolBaseCached)
            {
                if (cachedNetHandle != null)
                    cachedNetHandle.localRotation = Quaternion.Euler(-swingDeg, 0f, 0f) * netHandleBaseRot;
                if (cachedNetRing != null)
                    cachedNetRing.localRotation = Quaternion.Euler(-swingDeg, 0f, 0f) * netRingBaseRot;
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

            // 머리 약간 흔들림
            if (cachedHeadPivot == null) cachedHeadPivot = transform.Find("HeadPivot");
            if (cachedHeadPivot != null)
            {
                float headTilt = walking ? Mathf.Sin(walkAnimTimer * 0.5f) * 3f : 0f;
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
                        blockedRegionName = r.displayName;
                        blockedMsgTimer = 2f;
                        return true;
                    }
                }
            }

            // 지형 장애물 체크: 본인 CapsuleCollider 직접 참조로 확실히 제외.
            // (옛 IsChildOf만으로는 새 CapsuleCollider center 변경 이후 충돌 회귀가 발생)
            Collider[] hits = Physics.OverlapSphere(pos + Vector3.up * 1.4f, 0.4f);
            foreach (var h in hits)
            {
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

        // F11 unstick — 끼임 escape. SubArea 안이면 SubAreaWorldBuilder의 FindSafeSpawnPosition과
        // 동일 패턴으로 벽 없는 입구 좌표 탐색. 메인 월드면 (0, 0.1, 0) 초기 스폰 위치.
        private void UnstickToSafePosition()
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
                // 메인 월드 초기 스폰 위치
                target = FindClearSpot(new Vector3(0f, 0.1f, 0f), Vector3.zero);
            }
            transform.position = target;
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

        private bool IsClearAt(Vector3 pos)
        {
            Collider[] hits = Physics.OverlapSphere(pos + Vector3.up * 1.0f, 0.5f);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider h = hits[i];
                if (h == null || h.isTrigger) continue;
                if (h.gameObject == gameObject) continue;
                if (h.transform.IsChildOf(transform)) continue;
                if (h.attachedRigidbody != null && h.attachedRigidbody.gameObject == gameObject) continue;
                if (h.bounds.size.y < 0.5f) continue;
                return false;
            }
            return true;
        }

        public void AutoWire(RegionManager rm)
        {
            if (regionManager == null) regionManager = rm;
        }

        public void AutoWire(OutfitBonusProvider bonus)
        {
            if (outfitBonus == null) outfitBonus = bonus;
        }
    }
}
