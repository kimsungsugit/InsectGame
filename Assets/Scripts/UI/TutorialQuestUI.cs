using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    public class TutorialQuestUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private TutorialQuestManager questManager;

        private bool detailOpen;
        public bool IsOpen => detailOpen;
        public void Toggle() { SetDetailOpen(!detailOpen); }
        public void CloseModal() { SetDetailOpen(false); }

        // 퀘스트 목록 팝업만 모달 등록(상시 패널/알림은 비모달). 열려있는 동안 이동 차단 + ESC로 닫기.
        private void SetDetailOpen(bool v)
        {
            detailOpen = v;
            if (v) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }

        // 튜토리얼 표시 ON/OFF — 플레이 중 좌상단 패널·다음 단계 배너를 숨겨 방해 없이 진행.
        // 단 완료 알림(DrawCompletionNotification)은 숨김과 무관하게 항상 표시(보상 순간 유지).
        private bool tutorialHidden;

        private void Awake()
        {
            tutorialHidden = PlayerPrefs.GetInt(GameConstants.PrefsKeys.TutorialHidden, 0) == 1;
        }

        private void SetTutorialHidden(bool hidden)
        {
            tutorialHidden = hidden;
            PlayerPrefs.SetInt(GameConstants.PrefsKeys.TutorialHidden, hidden ? 1 : 0);
            PlayerPrefs.Save();
        }

        private float completionAnimTimer;
        private string completedQuestTitle;
        private float rewardAnimTimer;
        private int rewardCandy;
        private int rewardExp;
        private float hintPulse;

        private float newQuestAnimTimer;
        private string newQuestTitle;
        private string newQuestDesc;
        private float newQuestDelay; // 완료 알림이 끝난 뒤 이어서 표시하기 위한 지연

        private Vector2 detailScroll;

        // OnGUI 매 프레임 GUIStyle 생성 회귀 차단 — DrawQuestPanel은 매 프레임 호출되어 P0.
        // DrawCompletionNotification/DrawNewQuestNotification/DrawDetailPanel은 일시적 표시라 별도 라운드.
        private GUIStyle doneStyleCache;
        private GUIStyle questTitleStyleCache;
        private GUIStyle questDescStyleCache;
        private GUIStyle questProgStyleCache;
        private GUIStyle questHintStyleCache;
        private GUIStyle panelBtnStyleCache; // 숨기기/복원 작은 버튼 공용
        private bool questPanelStylesReady;

        // 알림(Notification) 캐시 - 일시 표시이나 OnGUI 매 호출 시 GC 차단
        private GUIStyle compHeaderStyleCache;
        private GUIStyle compTitleStyleCache;
        private GUIStyle rewardStyleCache;
        private GUIStyle newQuestStyleCache;
        private GUIStyle newQuestDescStyleCache;
        private GUIStyle newQuestPromptStyleCache;
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
        private static readonly Color NewQuestDescCol = new Color(0.85f, 0.92f, 1f);
        private static readonly Color NewQuestPromptCol = new Color(0.5f, 0.9f, 0.6f);
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

            panelBtnStyleCache = new GUIStyle(GUI.skin.button)
            { fontSize = 14, fontStyle = FontStyle.Bold };
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
            newQuestDescStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            newQuestPromptStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 15, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter };
            // 모두 textColor는 alpha 동적이라 매 호출 갱신.
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
            ModalUIRegistry.Unregister(this);
        }

        private void OnQuestActivated(TutorialQuest quest)
        {
            newQuestTitle = quest.title;
            newQuestDesc = quest.description;
            // 완료 알림과 겹치지 않게 그 뒤에 이어서 표시.
            // (옛: completionAnimTimer>0 동안 억제됐는데 완료=3s·새퀘=2s라 수명 내내 가려져
            //  게임 시작 첫 퀘스트를 제외한 모든 "다음 단계" 배너가 영구 미표시였음.)
            newQuestDelay = completionAnimTimer > 0f ? completionAnimTimer + 0.15f : 0f;
            newQuestAnimTimer = 3.5f;
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
            // 완료 알림이 끝나길 기다린 뒤(newQuestDelay) 새 퀘스트 배너 수명 소진.
            if (newQuestDelay > 0f) newQuestDelay -= Time.deltaTime;
            else if (newQuestAnimTimer > 0f) newQuestAnimTimer -= Time.deltaTime;
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

            // 숨김 상태: 작은 복원 버튼만 표시(게임 진행에 방해 없음). 완료 알림은 별도로 항상 표시됨.
            if (tutorialHidden)
            {
                if (GUI.Button(new Rect(20f + SafeArea.Left, 120f + SafeArea.Top, 130f, 30f), "▼ 퀘스트 보기", panelBtnStyleCache))
                    SetTutorialHidden(false);
                return;
            }

            Rect panelRect = new Rect(20f + SafeArea.Left, 120f + SafeArea.Top, 380f, 140f);

            // Background
            GUI.color = new Color(0.05f, 0.08f, 0.15f, 0.85f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);

            // Gold top bar
            GUI.color = new Color(0.9f, 0.75f, 0.2f, 1f);
            GUI.DrawTexture(new Rect(panelRect.x, panelRect.y, panelRect.width, 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 숨기기 버튼(우상단) — 누르면 패널·다음 단계 배너 끄고 방해 없이 플레이.
            if (GUI.Button(new Rect(panelRect.xMax - 30f, panelRect.y + 6f, 24f, 22f), "X", panelBtnStyleCache))
            {
                SetTutorialHidden(true);
                return;
            }

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

            // Title \u2014 \uc6b0\uc0c1\ub2e8 \uc228\uae30\uae30 \ubc84\ud2bc\uacfc \uacb9\uce58\uc9c0 \uc54a\uac8c \ub108\ube44 \ucd95\uc18c(-28).
            GUI.Label(new Rect(x, y, w - 28f, 28f), "\u2605 \ud018\uc2a4\ud2b8: " + active.title, questTitleStyleCache);
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
            float panelY = 30f + SafeArea.Top + slideOffset;

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
            if (tutorialHidden) return;           // \uc228\uae40 \uc911\uc5d4 \ub2e4\uc74c \ub2e8\uacc4 \uc548\ub0b4 \uc548 \ub744\uc6c0(\uc644\ub8cc \uc54c\ub9bc\uc740 \ubcc4\ub3c4 \uc720\uc9c0)
            if (newQuestDelay > 0f) return;       // \uc644\ub8cc \uc54c\ub9bc\uc774 \ub05d\ub0a0 \ub54c\uae4c\uc9c0 \ub300\uae30
            if (newQuestAnimTimer <= 0f) return;

            float alpha;
            if (newQuestAnimTimer > 3f)
            {
                float t = (3.5f - newQuestAnimTimer) / 0.5f; // \uc2ac\ub77c\uc774\ub4dc \uc778(0.5s)
                alpha = Mathf.Clamp01(t);
            }
            else if (newQuestAnimTimer < 0.4f)
            {
                alpha = Mathf.Clamp01(newQuestAnimTimer / 0.4f); // \ud398\uc774\ub4dc \uc544\uc6c3(0.4s)
            }
            else
            {
                alpha = 1f;
            }

            bool hasDesc = !string.IsNullOrEmpty(newQuestDesc);
            float panelW = 420f;
            float panelH = hasDesc ? 92f : 48f;
            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = 60f + SafeArea.Top;

            GUI.color = new Color(0.08f, 0.15f, 0.3f, 0.92f * alpha);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = new Color(0.3f, 0.6f, 1f, alpha);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panelX, panelY + panelH - 2f, panelW, 2f), Texture2D.whiteTexture);

            InitNotifStyles();
            GUI.color = new Color(1f, 1f, 1f, alpha);

            // \ud5e4\ub354: \ub2e4\uc74c \ub2e8\uacc4\uac00 "\ubb34\uc5c7"\uc778\uc9c0
            newQuestStyleCache.normal.textColor = new Color(NewQuestBaseCol.r, NewQuestBaseCol.g, NewQuestBaseCol.b, alpha);
            GUI.Label(new Rect(panelX, panelY + 6f, panelW, 28f),
                "\ub2e4\uc74c \ub2e8\uacc4 \u2192 \"" + (newQuestTitle ?? "") + "\"", newQuestStyleCache);

            if (hasDesc)
            {
                // \ubb34\uc5c7\uc744 "\ud574\uc57c \ud558\ub294\uc9c0"(\uc124\uba85)
                newQuestDescStyleCache.normal.textColor = new Color(NewQuestDescCol.r, NewQuestDescCol.g, NewQuestDescCol.b, alpha);
                GUI.Label(new Rect(panelX + 10f, panelY + 34f, panelW - 20f, 34f), newQuestDesc, newQuestDescStyleCache);

                // \uc9c4\ud589 \ub3c5\ub824
                newQuestPromptStyleCache.normal.textColor = new Color(NewQuestPromptCol.r, NewQuestPromptCol.g, NewQuestPromptCol.b, alpha);
                GUI.Label(new Rect(panelX, panelY + panelH - 24f, panelW, 20f),
                    "\u25b6 \uc9c0\uae08 \uc9c4\ud589\ud574\ubcf4\uc138\uc694!", newQuestPromptStyleCache);
            }

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
                SetDetailOpen(false);
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
