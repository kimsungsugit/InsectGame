using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    public class TutorialQuestUI : MonoBehaviour
    {
        [SerializeField] private TutorialQuestManager questManager;

        private bool detailOpen;
        public bool IsOpen => detailOpen;
        public void Toggle() { detailOpen = !detailOpen; }

        private float completionAnimTimer;
        private string completedQuestTitle;
        private float rewardAnimTimer;
        private int rewardCandy;
        private int rewardExp;
        private float hintPulse;

        private float newQuestAnimTimer;
        private string newQuestTitle;

        private Vector2 detailScroll;

        // OnGUI 매 프레임 GUIStyle 생성 회귀 차단 — DrawQuestPanel은 매 프레임 호출되어 P0.
        // DrawCompletionNotification/DrawNewQuestNotification/DrawDetailPanel은 일시적 표시라 별도 라운드.
        private GUIStyle doneStyleCache;
        private GUIStyle questTitleStyleCache;
        private GUIStyle questDescStyleCache;
        private GUIStyle questProgStyleCache;
        private GUIStyle questHintStyleCache;
        private bool questPanelStylesReady;

        // 알림(Notification) 캐시 - 일시 표시이나 OnGUI 매 호출 시 GC 차단
        private GUIStyle compHeaderStyleCache;
        private GUIStyle compTitleStyleCache;
        private GUIStyle rewardStyleCache;
        private GUIStyle newQuestStyleCache;
        private bool notifStylesReady;

        // 상세 패널 캐시
        private GUIStyle detailHeaderStyleCache;
        private GUIStyle detailCloseStyleCache;
        private GUIStyle detailRowStyleCache;
        private GUIStyle detailStatusStyleCache;
        private bool detailStylesReady;

        private static readonly Color DoneTextCol = new Color(0.9f, 0.75f, 0.2f);
        private static readonly Color QuestTitleCol = new Color(0.9f, 0.75f, 0.2f);
        private static readonly Color QuestHintBaseCol = new Color(0.65f, 0.65f, 0.65f);
        private static readonly Color CompHeaderBaseCol = new Color(0.2f, 1f, 0.4f);
        private static readonly Color CompTitleBaseCol = new Color(1f, 0.95f, 0.8f);
        private static readonly Color RewardBaseCol = new Color(1f, 0.85f, 0.5f);
        private static readonly Color NewQuestBaseCol = new Color(0.7f, 0.85f, 1f);
        private static readonly Color RowCompletedCol = new Color(0.5f, 0.8f, 0.5f);
        private static readonly Color RowLockedCol = new Color(0.45f, 0.45f, 0.5f);
        private static readonly Color RowPendingCol = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color StatusCompletedCol = new Color(0.4f, 0.75f, 0.4f);
        private static readonly Color StatusActiveCol = new Color(0.9f, 0.85f, 0.3f);

        private void InitQuestPanelStyles()
        {
            if (questPanelStylesReady) return;
            questPanelStylesReady = true;

            doneStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            doneStyleCache.normal.textColor = DoneTextCol;

            questTitleStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 22, fontStyle = FontStyle.Bold };
            questTitleStyleCache.normal.textColor = QuestTitleCol;

            questDescStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 18, wordWrap = true };
            questDescStyleCache.normal.textColor = Color.white;

            questProgStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            questProgStyleCache.normal.textColor = Color.white;

            questHintStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 16, fontStyle = FontStyle.Italic, wordWrap = true };
            // hintStyle.normal.textColor는 alpha 동적이라 매 호출 갱신 (BattleScreenUI 패턴).
        }

        private void InitNotifStyles()
        {
            if (notifStylesReady) return;
            notifStylesReady = true;

            compHeaderStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            compTitleStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            rewardStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            newQuestStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            // 4개 모두 textColor는 alpha 동적이라 매 호출 갱신.
        }

        private void InitDetailStyles()
        {
            if (detailStylesReady) return;
            detailStylesReady = true;

            detailHeaderStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            detailHeaderStyleCache.normal.textColor = QuestTitleCol;

            detailCloseStyleCache = new GUIStyle(GUI.skin.button)
            { fontSize = 20, fontStyle = FontStyle.Bold };
            detailCloseStyleCache.normal.textColor = Color.white;

            detailRowStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 18, alignment = TextAnchor.MiddleLeft };
            // textColor는 4분기(완료/활성/잠금/대기) 동적 갱신.

            detailStatusStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 16, alignment = TextAnchor.MiddleRight };
            // textColor 4분기 동적 갱신.
        }

        private void OnEnable()
        {
            if (questManager != null)
            {
                questManager.QuestActivated += OnQuestActivated;
                questManager.QuestProgressUpdated += OnQuestProgressUpdated;
                questManager.QuestCompleted += OnQuestCompleted;
            }
        }

        private void OnDisable()
        {
            if (questManager != null)
            {
                questManager.QuestActivated -= OnQuestActivated;
                questManager.QuestProgressUpdated -= OnQuestProgressUpdated;
                questManager.QuestCompleted -= OnQuestCompleted;
            }
        }

        private void OnQuestActivated(TutorialQuest quest)
        {
            newQuestTitle = quest.title;
            newQuestAnimTimer = 2f;
        }

        private void OnQuestProgressUpdated(TutorialQuest quest, int current, int target)
        {
            // progress is read live from questManager
        }

        private void OnQuestCompleted(TutorialQuest quest)
        {
            completedQuestTitle = quest.title;
            rewardCandy = quest.rewardCandy;
            rewardExp = quest.rewardExp;
            completionAnimTimer = 3f;
            rewardAnimTimer = 3f;
        }

        private void Update()
        {
            hintPulse += Time.deltaTime * 2.5f;
            if (completionAnimTimer > 0f) completionAnimTimer -= Time.deltaTime;
            if (rewardAnimTimer > 0f) rewardAnimTimer -= Time.deltaTime;
            if (newQuestAnimTimer > 0f) newQuestAnimTimer -= Time.deltaTime;
        }

        private void OnGUI()
        {
            if (detailOpen)
                DrawDetailPanel();
            else
                DrawQuestPanel();
            DrawCompletionNotification();
            DrawNewQuestNotification();
        }

        // ------------------------------------------------------------------
        // 1. Current Quest Panel (top-left, below PlayerStatusHUD)
        // ------------------------------------------------------------------
        private void DrawQuestPanel()
        {
            if (questManager == null) return;

            InitQuestPanelStyles();

            Rect panelRect = new Rect(20f, 120f, 380f, 140f);

            // Background
            GUI.color = new Color(0.05f, 0.08f, 0.15f, 0.85f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);

            // Gold top bar
            GUI.color = new Color(0.9f, 0.75f, 0.2f, 1f);
            GUI.DrawTexture(new Rect(panelRect.x, panelRect.y, panelRect.width, 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            TutorialQuest active = questManager.ActiveQuest;

            if (active == null && questManager.AllCompleted)
            {
                GUI.Label(panelRect, "\u2728 \ubaa8\ub4e0 \ud29c\ud1a0\ub9ac\uc5bc \uc644\ub8cc!", doneStyleCache);
                return;
            }

            if (active == null) return;

            float x = panelRect.x + 12f;
            float y = panelRect.y + 8f;
            float w = panelRect.width - 24f;

            // Title
            GUI.Label(new Rect(x, y, w, 28f), "\u2605 \ud034\uc2a4\ud2b8: " + active.title, questTitleStyleCache);
            y += 28f;

            // Description
            GUI.Label(new Rect(x, y, w, 36f), active.description, questDescStyleCache);
            y += 36f;

            // Progress bar
            int current = questManager.ActiveProgress;
            int target = active.targetCount;
            float ratio = target > 0 ? Mathf.Clamp01((float)current / target) : 0f;

            float barH = 16f;
            float barW = w - 60f;

            // Bar background
            GUI.color = new Color(0.12f, 0.12f, 0.18f, 1f);
            GUI.DrawTexture(new Rect(x, y + 2f, barW, barH), Texture2D.whiteTexture);

            // Bar fill
            if (ratio > 0f)
            {
                GUI.color = new Color(0.2f, 0.75f, 0.3f, 1f);
                GUI.DrawTexture(new Rect(x, y + 2f, barW * ratio, barH), Texture2D.whiteTexture);
            }

            // Progress text
            GUI.color = Color.white;
            GUI.Label(new Rect(x + barW + 6f, y, 54f, barH + 4f), current + "/" + target, questProgStyleCache);
            y += barH + 8f;

            // Hint with pulsing alpha \u2014 base style \uce90\uc2dc + textColor\ub9cc \ub3d9\uc801 \uac31\uc2e0 (BattleScreenUI \ud328\ud134).
            if (!string.IsNullOrEmpty(active.hint))
            {
                float hintAlpha = 0.4f + 0.4f * (0.5f + 0.5f * Mathf.Sin(hintPulse));
                questHintStyleCache.normal.textColor = new Color(QuestHintBaseCol.r, QuestHintBaseCol.g, QuestHintBaseCol.b, hintAlpha);
                GUI.Label(new Rect(x, y, w, 24f), "\ud83d\udca1 " + active.hint, questHintStyleCache);
            }
        }

        // ------------------------------------------------------------------
        // 2. Completion notification (top-center, 3 seconds)
        // ------------------------------------------------------------------
        private void DrawCompletionNotification()
        {
            if (completionAnimTimer <= 0f) return;

            float alpha;
            float slideOffset;

            if (completionAnimTimer > 2.5f)
            {
                // Slide in (0..0.5s)
                float t = (3f - completionAnimTimer) / 0.5f;
                alpha = Mathf.Clamp01(t);
                slideOffset = Mathf.Lerp(-60f, 0f, t);
            }
            else if (completionAnimTimer < 0.5f)
            {
                // Fade out
                alpha = Mathf.Clamp01(completionAnimTimer / 0.5f);
                slideOffset = 0f;
            }
            else
            {
                alpha = 1f;
                slideOffset = 0f;
            }

            float panelW = 420f;
            float panelH = 120f;
            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = 30f + slideOffset;

            // Background
            GUI.color = new Color(0.15f, 0.12f, 0.02f, 0.9f * alpha);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);

            // Gold border (top + bottom)
            GUI.color = new Color(0.9f, 0.75f, 0.2f, alpha);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panelX, panelY + panelH - 3f, panelW, 3f), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 1f, 1f, alpha);

            InitNotifStyles();

            // "Quest Complete!" header \u2014 base \uce90\uc2dc + textColor alpha \ub3d9\uc801
            compHeaderStyleCache.normal.textColor = new Color(CompHeaderBaseCol.r, CompHeaderBaseCol.g, CompHeaderBaseCol.b, alpha);
            GUI.Label(new Rect(panelX, panelY + 8f, panelW, 34f), "\u2713 \ud034\uc2a4\ud2b8 \uc644\ub8cc!", compHeaderStyleCache);

            // Quest title
            compTitleStyleCache.normal.textColor = new Color(CompTitleBaseCol.r, CompTitleBaseCol.g, CompTitleBaseCol.b, alpha);
            GUI.Label(new Rect(panelX, panelY + 42f, panelW, 26f),
                "\"" + (completedQuestTitle ?? "") + "\"", compTitleStyleCache);

            // Rewards
            string rewardText = "";
            if (rewardCandy > 0)
                rewardText += "\uce94\ub514 " + rewardCandy;
            if (rewardExp > 0)
            {
                if (rewardText.Length > 0) rewardText += " + ";
                rewardText += "\uacbd\ud5d8\uce58 " + rewardExp;
            }

            if (rewardText.Length > 0)
            {
                rewardStyleCache.normal.textColor = new Color(RewardBaseCol.r, RewardBaseCol.g, RewardBaseCol.b, alpha);
                GUI.Label(new Rect(panelX, panelY + 74f, panelW, 24f),
                    "\ubcf4\uc0c1: " + rewardText, rewardStyleCache);
            }

            GUI.color = Color.white;
        }

        // ------------------------------------------------------------------
        // 3. New quest notification (top-center, 2 seconds)
        // ------------------------------------------------------------------
        private void DrawNewQuestNotification()
        {
            if (newQuestAnimTimer <= 0f) return;
            if (completionAnimTimer > 0f) return; // don't overlap with completion

            float alpha;
            if (newQuestAnimTimer > 1.5f)
            {
                float t = (2f - newQuestAnimTimer) / 0.5f;
                alpha = Mathf.Clamp01(t);
            }
            else if (newQuestAnimTimer < 0.4f)
            {
                alpha = Mathf.Clamp01(newQuestAnimTimer / 0.4f);
            }
            else
            {
                alpha = 1f;
            }

            float panelW = 380f;
            float panelH = 44f;
            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = 60f;

            GUI.color = new Color(0.08f, 0.15f, 0.3f, 0.9f * alpha);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = new Color(0.3f, 0.6f, 1f, alpha);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panelX, panelY + panelH - 2f, panelW, 2f), Texture2D.whiteTexture);

            InitNotifStyles();
            newQuestStyleCache.normal.textColor = new Color(NewQuestBaseCol.r, NewQuestBaseCol.g, NewQuestBaseCol.b, alpha);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(panelX, panelY, panelW, panelH),
                "\uc0c8 \ud034\uc2a4\ud2b8! \u2192 \"" + (newQuestTitle ?? "") + "\"", newQuestStyleCache);

            GUI.color = Color.white;
        }

        // ------------------------------------------------------------------
        // 4. Detail panel (full quest list, toggled via QuickAccessBar)
        // ------------------------------------------------------------------
        private void DrawDetailPanel()
        {
            if (questManager == null) return;

            InitDetailStyles();

            float panelW = 440f;
            float panelH = 500f;
            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = (Screen.height - panelH) * 0.5f;

            // Background
            GUI.color = new Color(0.05f, 0.08f, 0.15f, 0.95f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);

            // Gold top bar
            GUI.color = new Color(0.9f, 0.75f, 0.2f, 1f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Header
            GUI.Label(new Rect(panelX + 16f, panelY + 8f, panelW - 80f, 36f),
                "\u2605 \ud034\uc2a4\ud2b8 \ubaa9\ub85d", detailHeaderStyleCache);

            // Close button [X]
            if (GUI.Button(new Rect(panelX + panelW - 50f, panelY + 8f, 38f, 32f), "X", detailCloseStyleCache))
            {
                detailOpen = false;
                return;
            }

            // Separator
            GUI.color = new Color(0.3f, 0.3f, 0.4f, 1f);
            GUI.DrawTexture(new Rect(panelX, panelY + 48f, panelW, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Quest list area
            float listX = panelX + 12f;
            float listY = panelY + 56f;
            float listW = panelW - 24f;
            float listH = panelH - 66f;

            TutorialQuest[] allQuests = questManager.GetAllQuests();
            if (allQuests == null || allQuests.Length == 0) return;

            float rowH = 40f;
            float contentH = allQuests.Length * rowH;
            Rect viewRect = new Rect(0, 0, listW - 20f, contentH);

            detailScroll = GUI.BeginScrollView(
                new Rect(listX, listY, listW, listH), detailScroll, viewRect);

            for (int i = 0; i < allQuests.Length; i++)
            {
                TutorialQuest quest = allQuests[i];
                float ry = i * rowH;
                bool isCompleted = questManager.IsQuestCompleted(quest.questId);
                bool isActive = questManager.ActiveQuest != null
                    && questManager.ActiveQuest.questId == quest.questId;
                bool isLocked = !isCompleted && !isActive
                    && !string.IsNullOrEmpty(quest.prerequisiteQuestId)
                    && !questManager.IsQuestCompleted(quest.prerequisiteQuestId);

                // Row background (alternating)
                if (i % 2 == 0)
                {
                    GUI.color = new Color(0.08f, 0.1f, 0.18f, 0.6f);
                    GUI.DrawTexture(new Rect(0, ry, viewRect.width, rowH), Texture2D.whiteTexture);
                }

                // Active highlight
                if (isActive)
                {
                    GUI.color = new Color(0.2f, 0.4f, 0.15f, 0.4f);
                    GUI.DrawTexture(new Rect(0, ry, viewRect.width, rowH), Texture2D.whiteTexture);
                }

                GUI.color = Color.white;

                // Status icon + title \u2014 base \uce90\uc2dc, textColor\ub9cc \ubd84\uae30 \ub3d9\uc801 \uac31\uc2e0
                string icon;
                if (isCompleted)
                {
                    icon = "\u2713 ";
                    detailRowStyleCache.normal.textColor = RowCompletedCol;
                }
                else if (isActive)
                {
                    icon = "\u25ba ";
                    detailRowStyleCache.normal.textColor = Color.white;
                }
                else if (isLocked)
                {
                    icon = "\ud83d\udd12 ";
                    detailRowStyleCache.normal.textColor = RowLockedCol;
                }
                else
                {
                    icon = "  ";
                    detailRowStyleCache.normal.textColor = RowPendingCol;
                }

                GUI.Label(new Rect(8f, ry, viewRect.width * 0.6f, rowH),
                    icon + quest.title, detailRowStyleCache);

                // Status text (right side) \u2014 base \uce90\uc2dc, textColor \ubd84\uae30 \ub3d9\uc801
                string statusText;
                if (isCompleted)
                {
                    statusText = "\uc644\ub8cc";
                    detailStatusStyleCache.normal.textColor = StatusCompletedCol;
                }
                else if (isActive)
                {
                    int cur = questManager.ActiveProgress;
                    int tgt = quest.targetCount;
                    statusText = cur + "/" + tgt;
                    detailStatusStyleCache.normal.textColor = StatusActiveCol;
                }
                else if (isLocked)
                {
                    statusText = "\ubbf8\ud574\uae08";
                    detailStatusStyleCache.normal.textColor = RowLockedCol;
                }
                else
                {
                    statusText = "\ub300\uae30";
                    detailStatusStyleCache.normal.textColor = RowPendingCol;
                }

                GUI.Label(new Rect(viewRect.width * 0.6f, ry, viewRect.width * 0.38f, rowH),
                    statusText, detailStatusStyleCache);
            }

            GUI.EndScrollView();
        }

        public void AutoWire(TutorialQuestManager manager)
        {
            if (questManager == null) questManager = manager;

            // Re-subscribe events in case AutoWire is called after OnEnable
            if (questManager != null)
            {
                questManager.QuestActivated -= OnQuestActivated;
                questManager.QuestProgressUpdated -= OnQuestProgressUpdated;
                questManager.QuestCompleted -= OnQuestCompleted;
                questManager.QuestActivated += OnQuestActivated;
                questManager.QuestProgressUpdated += OnQuestProgressUpdated;
                questManager.QuestCompleted += OnQuestCompleted;
            }
        }
    }
}
