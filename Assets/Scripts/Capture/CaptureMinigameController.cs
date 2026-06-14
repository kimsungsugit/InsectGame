using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;
using UnityEngine.UI;

namespace InsectGame.Capture
{
    public class CaptureMinigameController : MonoBehaviour
    {
        [SerializeField] private CaptureController captureController;
        [SerializeField] private Slider timingSlider;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private InsectGame.Core.PlayerMovement playerMovement;

        [Header("Minigame Tuning")]
        [SerializeField] private float baseSpeed = 1.4f;
        [SerializeField] private float speedPerRarity = 0.5f;
        [SerializeField] private float baseZoneSize = 0.35f;
        [SerializeField] private float zoneShrinkPerRarity = 0.05f;
        [SerializeField] private float timeLimit = 4f;

        private InsectEntity currentTarget;
        private float cursor;
        private float direction = 1f;
        private float currentSpeed;
        private float zoneCenter;
        private float zoneHalfSize;
        private float timer;
        private bool isActive;
        public bool IsActive => isActive;
        private float resultTimer;
        private string resultMessage;
        private bool resultSuccess;
        private int comboHits;
        private bool wantConfirm;
        private bool wantCancel;

        private enum Phase { Ready, Attempt1, Attempt2, Attempt3, Done }
        private Phase phase;
        private int hits;

        private float itemSpeedMult = 1f;
        private float itemZoneMult = 1f;
        private float itemTimeMult = 1f;
        private float itemCaptureBonus;

        // OnGUI 스타일 캐시 — 옛은 매 프레임 6개 new GUIStyle (라인 244/279/288/309/318/337).
        // 콤보 별은 loop 안이라 매 프레임 최대 3개 추가 → 60 FPS × 6~9 = 360~540회/초 회귀.
        // textColor가 동적인 스타일(title/phase/result)은 베이스만 캐시 후 textColor 매번 할당.
        private GUIStyle titleStyleCache;
        private GUIStyle phaseStyleCache;
        private GUIStyle starStyleCache;
        private GUIStyle captureBtnCache;
        private GUIStyle cancelBtnCache;
        private GUIStyle resultStyleCache;
        private bool stylesInitialized;

        private void InitMinigameStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            titleStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            phaseStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            starStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            starStyleCache.normal.textColor = Color.green;

            captureBtnCache = new GUIStyle(GUI.skin.button)
            { fontSize = 18, fontStyle = FontStyle.Bold };

            cancelBtnCache = new GUIStyle(GUI.skin.button)
            { fontSize = 16 };

            resultStyleCache = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        public void StartMinigame(InsectEntity target)
        {
            StartMinigame(target, 1f, 1f, 1f, 0f);
        }

        public void StartMinigame(InsectEntity target, float speedMult, float zoneMult, float timeMult, float captureBonus)
        {
            currentTarget = target;
            if (target != null) target.SetEngaged(true); // 미니게임 중 — 곤충 도주 방지
            isActive = true;
            if (playerMovement != null) playerMovement.SetFrozen(true);
            resultTimer = 0f;
            resultMessage = null;
            comboHits = 0;
            hits = 0;
            itemSpeedMult = speedMult;
            itemZoneMult = zoneMult;
            itemTimeMult = timeMult;
            itemCaptureBonus = captureBonus;

            int rarity = target != null && target.Data != null ? (int)target.Data.rarity : 0;
            currentSpeed = (baseSpeed + rarity * speedPerRarity) * itemSpeedMult;
            zoneHalfSize = Mathf.Max(0.08f, (baseZoneSize - rarity * zoneShrinkPerRarity) / 2f * itemZoneMult);

            BeginPhase(Phase.Attempt1);
        }

        private void BeginPhase(Phase p)
        {
            phase = p;
            cursor = 0f;
            direction = 1f;
            timer = timeLimit * itemTimeMult;

            float margin = zoneHalfSize + 0.05f;
            zoneCenter = Random.Range(margin, 1f - margin);

            if (p == Phase.Attempt2)
            {
                currentSpeed *= 1.15f;
                zoneHalfSize = Mathf.Max(0.06f, zoneHalfSize * 0.85f);
            }
            else if (p == Phase.Attempt3)
            {
                currentSpeed *= 1.15f;
                zoneHalfSize = Mathf.Max(0.05f, zoneHalfSize * 0.8f);
            }
        }

        private void Update()
        {
            if (resultTimer > 0f)
            {
                resultTimer -= Time.deltaTime;
                if (resultTimer <= 0f) resultMessage = null;
            }

            if (!isActive) return;

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Return))
                wantConfirm = true;
            if (Input.GetKeyDown(KeyCode.Escape))
                wantCancel = true;
            if (Input.GetMouseButtonDown(0))
                wantConfirm = true;

            if (wantConfirm) { wantConfirm = false; ConfirmCapture(); }
            if (wantCancel) { wantCancel = false; CancelCapture(); return; }

            timer -= Time.deltaTime;
            if (timer <= 0f) { FinishCapture(); return; }

            float speedMod = 1f + Mathf.Abs(cursor - 0.5f) * 0.6f;
            cursor += direction * currentSpeed * speedMod * Time.deltaTime;

            if (cursor >= 1f) { cursor = 1f; direction = -1f; }
            else if (cursor <= 0f) { cursor = 0f; direction = 1f; }

            if (timingSlider != null) timingSlider.value = cursor;
        }

        public void ConfirmCapture()
        {
            if (!isActive || currentTarget == null) return;

            hits++;
            bool inZone = Mathf.Abs(cursor - zoneCenter) <= zoneHalfSize;

            if (inZone)
            {
                comboHits++;
                if (phase == Phase.Attempt1)
                    BeginPhase(Phase.Attempt2);
                else if (phase == Phase.Attempt2)
                    BeginPhase(Phase.Attempt3);
                else
                    FinishCapture();
            }
            else
            {
                FinishCapture();
            }
        }

        private void FinishCapture()
        {
            float timing01 = comboHits >= 3 ? 0.5f :
                             comboHits >= 2 ? 0.45f :
                             comboHits >= 1 ? 0.3f : 0.1f;

            if (captureController != null && currentTarget != null)
            {
                captureController.AttemptCapture(currentTarget, timing01, itemCaptureBonus);

                if (comboHits >= 3)
                    ShowResult("PERFECT!", true);
                else if (comboHits >= 2)
                    ShowResult("GREAT!", true);
                else if (comboHits >= 1)
                    ShowResult("GOOD", false);
                else
                    ShowResult("MISS...", false);
            }

            StopMinigame();
        }

        private void ShowResult(string msg, bool success)
        {
            resultMessage = msg;
            resultSuccess = success;
            resultTimer = 1.5f;
        }

        public void CancelCapture()
        {
            StopMinigame();
        }

        private void StopMinigame()
        {
            isActive = false;
            phase = Phase.Done;
            hits = 0;
            if (currentTarget != null) currentTarget.SetEngaged(false); // 미니게임 종료 — 도주 가능 상태 복귀
            currentTarget = null;
            if (panelRoot != null) panelRoot.SetActive(false);
            if (playerMovement != null) playerMovement.SetFrozen(false);
        }

        private void OnDisable()
        {
            // 외부에서 컴포넌트 disable되어도 isActive/playerMovement.frozen이 잔존하지 않도록 보장.
            // 옛은 OnDisable 없음 → 씬 전환 시 player가 영구 멈춤 + 다음 활성화 시 OnGUI 미니게임 잔존.
            if (isActive) StopMinigame();
        }

        private void OnGUI()
        {
            if (isActive)
            {
                Event evt = Event.current;
                if (evt != null && evt.type == EventType.KeyDown)
                {
                    if (evt.keyCode == KeyCode.Space || evt.keyCode == KeyCode.E
                        || evt.keyCode == KeyCode.Tab || evt.keyCode == KeyCode.Return)
                    {
                        wantConfirm = true;
                        evt.Use();
                    }
                    else if (evt.keyCode == KeyCode.Escape)
                    {
                        wantCancel = true;
                        evt.Use();
                    }
                }

                if (evt != null && evt.type == EventType.MouseDown && evt.button == 0)
                {
                    wantConfirm = true;
                }
            }

            if (resultTimer > 0f && resultMessage != null)
                DrawResult();

            if (!isActive) return;
            DrawMinigame();
        }

        private void DrawMinigame()
        {
            float panelW = 500f;
            float panelH = 220f;
            float x = (Screen.width - panelW) / 2f;
            float y = Screen.height * 0.28f;

            GUI.color = new Color(0, 0, 0, 0.88f);
            GUI.DrawTexture(new Rect(x, y, panelW, panelH), Texture2D.whiteTexture);

            string targetName = currentTarget != null && currentTarget.Data != null
                ? currentTarget.Data.displayName : "???";
            string rarityName = currentTarget != null && currentTarget.Data != null
                ? currentTarget.Data.rarity.ToString() : "";

            InitMinigameStyles();
            titleStyleCache.normal.textColor = GetRarityGUIColor();
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 8, panelW, 30), $"{targetName} [{rarityName}]", titleStyleCache);

            float barX = x + 30;
            float barY = y + 48;
            float barW = panelW - 60;
            float barH = 40;

            GUI.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);

            float zoneLeft = barX + (zoneCenter - zoneHalfSize) * barW;
            float zoneWidth = zoneHalfSize * 2f * barW;
            GUI.color = new Color(0.1f, 0.7f, 0.2f, 0.6f);
            GUI.DrawTexture(new Rect(zoneLeft, barY, zoneWidth, barH), Texture2D.whiteTexture);

            float outerLeft = barX + (zoneCenter - zoneHalfSize * 1.8f) * barW;
            float outerWidth = zoneHalfSize * 3.6f * barW;
            GUI.color = new Color(0.9f, 0.8f, 0.1f, 0.3f);
            GUI.DrawTexture(new Rect(outerLeft, barY, (zoneLeft - outerLeft), barH), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(zoneLeft + zoneWidth, barY,
                (outerLeft + outerWidth) - (zoneLeft + zoneWidth), barH), Texture2D.whiteTexture);

            float dist = Mathf.Abs(cursor - zoneCenter);
            Color cursorColor = dist <= zoneHalfSize ? Color.green :
                               dist <= zoneHalfSize * 1.8f ? Color.yellow : Color.red;

            float cursorX = barX + cursor * barW - 3f;
            GUI.color = cursorColor;
            GUI.DrawTexture(new Rect(cursorX, barY - 4, 6, barH + 8), Texture2D.whiteTexture);

            GUI.color = Color.white;
            string phaseLabel = phase == Phase.Attempt1 ? "1st" :
                               phase == Phase.Attempt2 ? "2nd - Faster!" : "FINAL!";
            phaseStyleCache.normal.textColor = phase == Phase.Attempt3 ? Color.yellow : Color.white;
            GUI.Label(new Rect(x, barY + barH + 4, panelW, 22), phaseLabel, phaseStyleCache);

            for (int i = 0; i < comboHits && i < 3; i++)
            {
                float starX = x + panelW / 2f - 45 + i * 30;
                GUI.Label(new Rect(starX, barY + barH + 22, 28, 28), "*", starStyleCache);
            }

            float timerRatio = Mathf.Clamp01(timer / timeLimit);
            float timerBarY = y + panelH - 60;
            GUI.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            GUI.DrawTexture(new Rect(barX, timerBarY, barW, 8), Texture2D.whiteTexture);
            Color timerColor = timerRatio > 0.4f ? new Color(0.2f, 0.7f, 1f) :
                              timerRatio > 0.2f ? Color.yellow : Color.red;
            GUI.color = timerColor;
            GUI.DrawTexture(new Rect(barX, timerBarY, barW * timerRatio, 8), Texture2D.whiteTexture);

            GUI.color = Color.white;
            float btnY = y + panelH - 42;
            float btnW = 140f;
            float btnH = 34f;

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
            if (GUI.Button(new Rect(x + panelW / 2f - btnW - 10, btnY, btnW, btnH), "포획! [Space/클릭]", captureBtnCache))
            {
                wantConfirm = true;
            }

            GUI.backgroundColor = new Color(0.6f, 0.2f, 0.2f);
            if (GUI.Button(new Rect(x + panelW / 2f + 10, btnY, btnW, btnH), "취소 [ESC]", cancelBtnCache))
            {
                wantCancel = true;
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawResult()
        {
            float alpha = Mathf.Clamp01(resultTimer / 0.3f);
            float cx = Screen.width / 2f;
            float baseY = Screen.height * 0.18f;

            float progress = 1f - (resultTimer / 1.5f);
            float bounce = 1f + Mathf.Sin(progress * Mathf.PI) * 0.15f;
            int fontSize = (int)(42 * bounce);

            InitMinigameStyles();
            resultStyleCache.fontSize = fontSize; // fontSize는 bounce에 따라 동적
            resultStyleCache.normal.textColor = resultSuccess
                ? new Color(0.3f, 1f, 0.5f, alpha)
                : new Color(1f, 0.4f, 0.3f, alpha);

            float w = 400;
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.Label(new Rect(cx - w / 2f, baseY, w, 60), resultMessage, resultStyleCache);

            if (resultSuccess)
            {
                float glowSize = 120f + progress * 60f;
                Color glowCol = resultStyleCache.normal.textColor;
                GUI.color = new Color(glowCol.r, glowCol.g, glowCol.b, 0.08f * alpha);
                GUI.DrawTexture(new Rect(cx - glowSize / 2, baseY + 30 - glowSize / 2, glowSize, glowSize), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
        }

        private Color GetRarityGUIColor()
        {
            if (currentTarget == null || currentTarget.Data == null) return Color.white;
            switch (currentTarget.Data.rarity)
            {
                case InsectRarity.Common: return new Color(0.7f, 0.7f, 0.7f);
                case InsectRarity.Uncommon: return new Color(0.4f, 0.9f, 0.4f);
                case InsectRarity.Rare: return new Color(0.4f, 0.6f, 1f);
                case InsectRarity.Epic: return new Color(0.8f, 0.4f, 1f);
                case InsectRarity.Legendary: return new Color(1f, 0.8f, 0.2f);
                default: return Color.white;
            }
        }

        public void AutoWire(CaptureController controller)
        {
            if (captureController == null)
                captureController = controller;
        }

        public void AutoWire(InsectGame.Core.PlayerMovement pm)
        {
            if (playerMovement == null) playerMovement = pm;
        }
    }
}
