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
                GUIStyle doneStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
                doneStyle.normal.textColor = new Color(0.9f, 0.75f, 0.2f);
                GUI.Label(panelRect, "\u2728 \ubaa8\ub4e0 \ud29c\ud1a0\ub9ac\uc5bc \uc644\ub8cc!", doneStyle);
                return;
            }

            if (active == null) return;

            float x = panelRect.x + 12f;
            float y = panelRect.y + 8f;
            float w = panelRect.width - 24f;

            // Title
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(0.9f, 0.75f, 0.2f);
            GUI.Label(new Rect(x, y, w, 28f), "\u2605 \ud034\uc2a4\ud2b8: " + active.title, titleStyle);
            y += 28f;

            // Description
            GUIStyle descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                wordWrap = true
            };
            descStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y, w, 36f), active.description, descStyle);
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
            GUIStyle progStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            progStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x + barW + 6f, y, 54f, barH + 4f), current + "/" + target, progStyle);
            y += barH + 8f;

            // Hint with pulsing alpha
            if (!string.IsNullOrEmpty(active.hint))
            {
                float hintAlpha = 0.4f + 0.4f * (0.5f + 0.5f * Mathf.Sin(hintPulse));
                GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Italic,
                    wordWrap = true
                };
                hintStyle.normal.textColor = new Color(0.65f, 0.65f, 0.65f, hintAlpha);
                GUI.Label(new Rect(x, y, w, 24f), "\ud83d\udca1 " + active.hint, hintStyle);
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

            // "Quest Complete!" header
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = new Color(0.2f, 1f, 0.4f, alpha);
            GUI.Label(new Rect(panelX, panelY + 8f, panelW, 34f), "\u2713 \ud034\uc2a4\ud2b8 \uc644\ub8cc!", headerStyle);

            // Quest title
            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = new Color(1f, 0.95f, 0.8f, alpha);
            GUI.Label(new Rect(panelX, panelY + 42f, panelW, 26f),
                "\"" + (completedQuestTitle ?? "") + "\"", titleStyle);

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
                GUIStyle rewardStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter
                };
                rewardStyle.normal.textColor = new Color(1f, 0.85f, 0.5f, alpha);
                GUI.Label(new Rect(panelX, panelY + 74f, panelW, 24f),
                    "\ubcf4\uc0c1: " + rewardText, rewardStyle);
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

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = new Color(0.7f, 0.85f, 1f, alpha);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(panelX, panelY, panelW, panelH),
                "\uc0c8 \ud034\uc2a4\ud2b8! \u2192 \"" + (newQuestTitle ?? "") + "\"", style);

            GUI.color = Color.white;
        }

        // ------------------------------------------------------------------
        // 4. Detail panel (full quest list, toggled via QuickAccessBar)
        // ------------------------------------------------------------------
        private void DrawDetailPanel()
        {
            if (questManager == null) return;

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
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            headerStyle.normal.textColor = new Color(0.9f, 0.75f, 0.2f);
            GUI.Label(new Rect(panelX + 16f, panelY + 8f, panelW - 80f, 36f),
                "\u2605 \ud034\uc2a4\ud2b8 \ubaa9\ub85d", headerStyle);

            // Close button [X]
            GUIStyle closeStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };
            closeStyle.normal.textColor = Color.white;
            if (GUI.Button(new Rect(panelX + panelW - 50f, panelY + 8f, 38f, 32f), "X", closeStyle))
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

                // Status icon + title
                GUIStyle rowStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleLeft
                };

                string icon;
                if (isCompleted)
                {
                    icon = "\u2713 ";
                    rowStyle.normal.textColor = new Color(0.5f, 0.8f, 0.5f);
                }
                else if (isActive)
                {
                    icon = "\u25ba ";
                    rowStyle.normal.textColor = Color.white;
                }
                else if (isLocked)
                {
                    icon = "\ud83d\udd12 ";
                    rowStyle.normal.textColor = new Color(0.45f, 0.45f, 0.5f);
                }
                else
                {
                    icon = "  ";
                    rowStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                }

                GUI.Label(new Rect(8f, ry, viewRect.width * 0.6f, rowH),
                    icon + quest.title, rowStyle);

                // Status text (right side)
                GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleRight
                };

                string statusText;
                if (isCompleted)
                {
                    statusText = "\uc644\ub8cc";
                    statusStyle.normal.textColor = new Color(0.4f, 0.75f, 0.4f);
                }
                else if (isActive)
                {
                    int cur = questManager.ActiveProgress;
                    int tgt = quest.targetCount;
                    statusText = cur + "/" + tgt;
                    statusStyle.normal.textColor = new Color(0.9f, 0.85f, 0.3f);
                }
                else if (isLocked)
                {
                    statusText = "\ubbf8\ud574\uae08";
                    statusStyle.normal.textColor = new Color(0.45f, 0.45f, 0.5f);
                }
                else
                {
                    statusText = "\ub300\uae30";
                    statusStyle.normal.textColor = new Color(0.6f, 0.6f, 0.65f);
                }

                GUI.Label(new Rect(viewRect.width * 0.6f, ry, viewRect.width * 0.38f, rowH),
                    statusText, statusStyle);
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
