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

        public bool IsFrozen => frozen;

        public void SetFrozen(bool value)
        {
            frozen = value;
            if (value) frozenTimer = 0f;
        }

        private void Update()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                EventSystem.current.SetSelectedGameObject(null);

            if (frozen)
            {
                frozenTimer += Time.unscaledDeltaTime;
                if (frozenTimer > GameConstants.Player.AutoUnfreezeTime)
                {
                    frozen = false;
                }

                if (Input.GetKeyDown(KeyCode.Escape) || guiEscPressed)
                {
                    frozen = false;
                    guiEscPressed = false;
                }

                return;
            }

            float h = 0f;
            float v = 0f;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) || guiKeyA || guiKeyLeft) h = -1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) || guiKeyD || guiKeyRight) h = 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || guiKeyW || guiKeyUp) v = 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || guiKeyS || guiKeyDown) v = -1f;

            bool hasKeyboard = h != 0f || v != 0f;
            if (hasKeyboard) hasReceivedInput = true;

            bool wantClick = Input.GetMouseButtonDown(0) || guiClickRequest;
            guiClickRequest = false;

            if (wantClick && Camera.main != null)
            {
                Vector2 mousePos = guiClickRequest ? guiClickPos : (Vector2)Input.mousePosition;

                bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (!pointerOverUI)
                {
                    Ray ray = Camera.main.ScreenPointToRay(mousePos);

                    int playerLayer = gameObject.layer;
                    gameObject.layer = 2;
                    Collider[] childColliders = GetComponentsInChildren<Collider>();
                    int[] childLayers = new int[childColliders.Length];
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

                    gameObject.layer = playerLayer;
                    for (int i = 0; i < childColliders.Length; i++)
                    {
                        childColliders[i].gameObject.layer = childLayers[i];
                    }
                }
            }

            Vector3 direction;

            if (hasKeyboard)
            {
                movingToClick = false;
                direction = new Vector3(h, 0f, v).normalized;
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

            if (regionManager != null && IsBlockedPosition(nextPos))
            {
                move.x = 0f;
                move.z = 0f;
                movingToClick = false;
            }

            transform.position += move;

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

            if (!hasReceivedInput && !frozen)
            {
                GUIStyle bigStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 32,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                bigStyle.normal.textColor = Color.white;

                float w = 500f;
                float h = 80f;
                Rect rect = new Rect((Screen.width - w) / 2f, Screen.height * 0.4f, w, h);

                GUI.color = new Color(0, 0, 0, 0.7f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(rect, "WASD 또는 마우스 클릭으로 이동하세요", bigStyle);
            }

            if (frozen)
            {
                GUIStyle frozenStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24,
                    alignment = TextAnchor.MiddleCenter
                };
                frozenStyle.normal.textColor = new Color(1f, 1f, 0.5f, 0.7f);
                GUI.Label(new Rect(0, Screen.height - 50, Screen.width, 30),
                    "ESC를 누르면 이동 잠금을 해제합니다", frozenStyle);
            }

            if (blockedMsgTimer > 0f)
            {
                float alpha = Mathf.Clamp01(blockedMsgTimer / 0.5f);
                GUIStyle blockStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                blockStyle.normal.textColor = new Color(1f, 0.4f, 0.3f, alpha);
                float bw = 400f;
                Rect bRect = new Rect((Screen.width - bw) / 2f, Screen.height * 0.6f, bw, 30);
                GUI.Label(bRect, $"{blockedRegionName} 진입에 레벨이 부족합니다!", blockStyle);
            }
        }

        private bool IsBlockedPosition(Vector3 pos)
        {
            if (regionManager == null || regionManager.Regions == null) return false;
            foreach (var r in regionManager.Regions)
            {
                if (r.ContainsPoint(pos) && !regionManager.IsRegionAccessible(r))
                {
                    blockedRegionName = r.displayName;
                    blockedMsgTimer = 2f;
                    return true;
                }
            }
            return false;
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
