using InsectGame.Data;
using InsectGame.Spawning;
using InsectGame.UI;
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

            int titleSize = UIScale.IsMobileLayout ? 30 : 20;
            int phaseSize = UIScale.IsMobileLayout ? 24 : 16;
            int buttonSize = UIScale.IsMobileLayout ? 25 : 18;
            titleStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = titleSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            phaseStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = phaseSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            starStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            starStyleCache.normal.textColor = Color.green;

            captureBtnCache = new GUIStyle(GUI.skin.button)
            { fontSize = buttonSize, fontStyle = FontStyle.Bold };

            cancelBtnCache = new GUIStyle(GUI.skin.button)
            { fontSize = UIScale.IsMobileLayout ? 23 : 16 };

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

            // 입력은 OnGUI 이벤트 패스(KeyDown/MouseDown)에서만 wantConfirm/wantCancel를 세팅한다.
            // 옛은 여기서 Input.GetKeyDown/GetMouseButtonDown 폴링으로도 세팅 → 같은 누름을 Update(폴링)+
            // OnGUI(이벤트)가 이중 큐잉, 프레임당 1회만 소비돼 누름 1회당 ConfirmCapture가 2회 실행됨.
            // 두 번째가 BeginPhase 직후 cursor=0에서 항상 miss→FinishCapture로 즉시 종료 → 3단계 콤보·
            // 퍼펙트 타이밍 보너스 영구 불가였음. 단일 입력 소스(OnGUI)로 통일.
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
            float timing01 = CaptureMinigameProbability.GetTiming01(comboHits);
            float extraBonus = CaptureMinigameProbability.GetExtraBonus(comboHits, itemCaptureBonus);

            if (captureController != null && currentTarget != null)
            {
                captureController.AttemptCapture(currentTarget, timing01, extraBonus);

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
                    // 어디든 탭=확정(누름 기반이라 반응성 좋음). 단 취소 버튼 영역은 제외(취소만 발화).
                    // 캡처 버튼의 MouseUp은 더 이상 확정을 세팅하지 않아(시각 전용) 단일 탭당 ConfirmCapture
                    // 가 정확히 1회 — 옛은 MouseDown(누름)+버튼 MouseUp(뗌)이 이중확정돼 페이즈 직후 cursor≈0
                    // 에서 MISS로 즉시 종료, 3단계 콤보·퍼펙트 보너스 영구 불가였음. (취소 rect는 가상 좌표,
                    // evt.mousePosition은 Begin 전이라 raw → Scale로 나눠 가상좌표로 변환.)
                    Vector2 vp = evt.mousePosition / Mathf.Max(0.3f, UIScale.Scale);
                    if (!cancelButtonRect.Contains(vp))
                        wantConfirm = true;
                }
            }

            if (resultTimer <= 0f && !isActive) return;
            UIScale.Begin();
            if (resultTimer > 0f && resultMessage != null) DrawResult();
            if (isActive) DrawMinigame();
            UIScale.End();
        }

        // 취소 버튼 가상 rect — 전역 MouseDown 확정에서 취소 영역을 제외하기 위해 직전 DrawMinigame에서 갱신.
        private Rect cancelButtonRect;

        private void DrawMinigame()
        {
            bool mobile = UIScale.IsMobileLayout;
            float panelW = mobile ? Mathf.Min(900f, UIScale.ContentWidth(28f)) : 500f;
            float panelH = UISafeLayout.ClampHeight(mobile ? 340f : 220f);
            float x = (UIScale.VirtualScreenWidth - panelW) / 2f;
            // 화면 상단 1/4 근처 비율 배치 — 단 안전 영역 밖으로는 나가지 않는다.
            float y = Mathf.Clamp(
                UIScale.VirtualScreenHeight * (mobile ? 0.24f : 0.28f),
                UISafeLayout.ContentTop,
                Mathf.Max(UISafeLayout.ContentTop, UISafeLayout.ContentBottom - panelH));

            GUI.color = new Color(0, 0, 0, 0.88f);
            GUI.DrawTexture(new Rect(x, y, panelW, panelH), Texture2D.whiteTexture);

            string targetName = currentTarget != null && currentTarget.Data != null
                ? currentTarget.Data.displayName : "???";
            string rarityName = currentTarget != null && currentTarget.Data != null
                ? currentTarget.Data.rarity.ToString() : "";

            InitMinigameStyles();
            titleStyleCache.normal.textColor = GetRarityGUIColor();
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + (mobile ? 14f : 8f), panelW, mobile ? 44f : 30f),
                $"{targetName} [{rarityName}]", titleStyleCache);

            float barX = x + 30;
            float barY = y + (mobile ? 72f : 48f);
            float barW = panelW - 60;
            float barH = mobile ? 64f : 40f;

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
            GUI.Label(new Rect(x, barY + barH + 4, panelW, mobile ? 34f : 22f), phaseLabel, phaseStyleCache);

            for (int i = 0; i < comboHits && i < 3; i++)
            {
                float starX = x + panelW / 2f - 45 + i * 30;
                GUI.Label(new Rect(starX, barY + barH + 22, 28, 28), "*", starStyleCache);
            }

            float timerRatio = Mathf.Clamp01(timer / timeLimit);
            float timerBarY = y + panelH - (mobile ? 100f : 60f);
            GUI.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            GUI.DrawTexture(new Rect(barX, timerBarY, barW, 8), Texture2D.whiteTexture);
            Color timerColor = timerRatio > 0.4f ? new Color(0.2f, 0.7f, 1f) :
                              timerRatio > 0.2f ? Color.yellow : Color.red;
            GUI.color = timerColor;
            GUI.DrawTexture(new Rect(barX, timerBarY, barW * timerRatio, 8), Texture2D.whiteTexture);

            GUI.color = Color.white;
            float btnY = y + panelH - (mobile ? 80f : 42f);
            float btnW = mobile ? (panelW - 90f) * 0.5f : 140f;
            float btnH = mobile ? 64f : 34f;

            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
            string captureText = mobile ? "지금 포획!" : "포획! [Space/클릭]";
            // 시각 전용 — 확정은 전역 MouseDown(누름)이 처리(버튼 MouseUp 이중확정 차단).
            GUI.Button(new Rect(x + panelW / 2f - btnW - 10, btnY, btnW, btnH), captureText, captureBtnCache);

            GUI.backgroundColor = new Color(0.6f, 0.2f, 0.2f);
            string cancelText = mobile ? "취소" : "취소 [ESC]";
            cancelButtonRect = new Rect(x + panelW / 2f + 10, btnY, btnW, btnH);
            if (GUI.Button(cancelButtonRect, cancelText, cancelBtnCache))
            {
                wantCancel = true;
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawResult()
        {
            float alpha = Mathf.Clamp01(resultTimer / 0.3f);
            float cx = UIScale.VirtualScreenWidth / 2f;
            float baseY = UIScale.VirtualScreenHeight * 0.18f;

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
