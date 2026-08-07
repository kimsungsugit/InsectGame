using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    public class TutorialQuestUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private TutorialQuestManager questManager;
        // 보상 아이템 ID를 표시명으로 바꾸는 데만 쓴다. 미주입이면 ID를 그대로 보여준다.
        [SerializeField] private ItemDatabase itemDatabase;
        private GuidedTutorialController guided;   // 강제 가이드 상태 조회(가이드 중 숨김 억제)

        private bool detailOpen;
        private bool activeDetailOpen;   // 칩 클릭 시 뜨는 활성 퀘스트 상세 팝업(중앙)
        public bool IsOpen => detailOpen || activeDetailOpen;
        public void Toggle() { SetDetailOpen(!detailOpen); }
        public void CloseModal()
        {
            detailOpen = false;
            activeDetailOpen = false;
            detailDirectScroll.Reset();
            UpdateModalRegistration();
        }

        // 모달 등록 갱신 — 목록/활성 팝업 중 하나라도 열리면 등록(이동 차단 + ESC로 닫기).
        private void UpdateModalRegistration()
        {
            if (detailOpen || activeDetailOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }

        // 칩 클릭 → 활성 퀘스트 상세 팝업(중앙). 목록 팝업과 상호 배타.
        private void SetActiveDetailOpen(bool v)
        {
            activeDetailOpen = v;
            if (v) detailOpen = false;
            UpdateModalRegistration();
        }

        // 퀘스트 목록 팝업(퀵바). 열려있는 동안 이동 차단 + ESC로 닫기.
        private void SetDetailOpen(bool v)
        {
            detailOpen = v;
            detailScroll = Vector2.zero;
            detailDirectScroll.Reset();
            if (v)
            {
                activeDetailOpen = false;
                // 완료 목록을 확인하는 순간 퀵바의 미확인 완료 배지를 0으로 리셋.
                if (questManager != null) questManager.MarkQuestsSeen();
            }
            UpdateModalRegistration();
        }

        // 튜토리얼 표시 ON/OFF — 플레이 중 좌상단 패널·다음 단계 배너를 숨겨 방해 없이 진행.
        // 단 완료 알림(DrawCompletionNotification)은 숨김과 무관하게 항상 표시(보상 순간 유지).
        private bool tutorialHidden;

        private string TutorialHiddenKey
        {
            get
            {
                if (AuthManager.Instance != null
                    && AuthManager.Instance.IsLoggedIn
                    && !string.IsNullOrEmpty(AuthManager.Instance.UserId))
                {
                    return GameConstants.PrefsKeys.TutorialHidden + "." + AuthManager.Instance.UserId;
                }

                return GameConstants.PrefsKeys.TutorialHidden;
            }
        }

        private void Awake()
        {
            tutorialHidden = PlayerPrefs.GetInt(TutorialHiddenKey, 0) == 1;
        }

        private void SetTutorialHidden(bool hidden)
        {
            tutorialHidden = hidden;
            PlayerPrefs.SetInt(TutorialHiddenKey, hidden ? 1 : 0);
            PlayerPrefs.Save();
        }

        private float completionAnimTimer;
        private string completedQuestTitle;
        private float rewardAnimTimer;
        // 완료 시점에 QuestRewardFormatter로 한 번 조립해 둔다. 옛 버전은 캔디/경험치/곤충을
        // 배너에서 직접 이어 붙이면서 아이템 보상을 통째로 빠뜨렸다.
        private string completedRewardText;
        private float hintPulse;

        // 목록 행의 보상 칩용 재사용 버퍼 — OnGUI 매 프레임 할당 방지.
        private readonly List<QuestRewardEntry> rewardChipBuffer = new List<QuestRewardEntry>(4);
        // 아코디언으로 펼쳐진 퀘스트. 빈 문자열이면 모두 접힌 상태.
        private string expandedQuestId = string.Empty;

        private float newQuestAnimTimer;
        private string newQuestTitle;
        private string newQuestDesc;
        private float newQuestDelay; // 완료 알림이 끝난 뒤 이어서 표시하기 위한 지연

        private Vector2 detailScroll;
        private readonly UIDirectScroll detailDirectScroll = new UIDirectScroll();

        // 설명/힌트 동적 높이(CalcHeight) 계산용 — OnGUI 매 프레임 new GUIContent 회피.
        private readonly GUIContent descContentCache = new GUIContent();
        private readonly GUIContent hintContentCache = new GUIContent();

        // OnGUI 매 프레임 GUIStyle 생성 회귀 차단 — DrawQuestPanel은 매 프레임 호출되어 P0.
        // DrawCompletionNotification/DrawNewQuestNotification/DrawDetailPanel은 일시적 표시라 별도 라운드.
        private GUIStyle doneStyleCache;
        private GUIStyle questTitleStyleCache;
        private GUIStyle questDescStyleCache;
        private GUIStyle questProgStyleCache;
        private GUIStyle questHintStyleCache;
        private GUIStyle panelBtnStyleCache;        // 상세 팝업의 GUI.Button용 (button 파생)
        private GUIStyle panelSurfaceBtnStyleCache; // 칩의 UISurface.Button용 (label 파생)
        private GUIStyle objectiveStyleCache;       // 목표 행 (label 파생 — UISurface.Button에 넘긴다)
        private GUIStyle objectiveStatusStyleCache; // 목표 행 아래 일시 안내

        // 메인퀘스트 목표 행. 위치·이름·거리는 전부 트래커가 풀어 준다(UI는 그리기만).
        private InsectGame.Story.StoryObjectiveTracker objectiveTracker;
        // 칩(또는 숨김 버튼)이 끝나는 y — 목표 행이 그 아래에 붙는다. 칩 높이가 상태마다
        // 달라(완료/숨김/진행 중) 상수로 둘 수 없어, 그린 쪽이 실제 값을 남긴다.
        private float objectiveRowTop;
        private bool objectiveRowVisible;
        // 목표 행 문자열 캐시 — OnGUI 매 프레임 보간 문자열 할당 차단.
        private string objectiveLabelCache;
        private string objectiveLabelSource;
        private int objectiveLabelDistance = int.MinValue;
        private bool objectiveLabelRunning;
        private bool objectiveLabelCanRun;
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
        private GUIStyle detailRewardStyleCache;
        private GUIStyle detailRewardLabelStyleCache;
        private GUIStyle detailDescStyleCache;
        private bool detailStylesReady;

        // 퀘스트별 보상 요약 문자열 캐시. 보상은 불변이라 최초 1회만 조립하면 되고,
        // 목록이 매 프레임 그려지므로 캐시하지 않으면 행마다 문자열 할당이 쌓인다.
        private readonly Dictionary<string, string> rewardTextCache = new Dictionary<string, string>();

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
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            doneStyleCache.normal.textColor = DoneTextCol;

            questTitleStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold };
            questTitleStyleCache.normal.textColor = QuestTitleCol;

            questDescStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 23, wordWrap = true };
            questDescStyleCache.normal.textColor = Color.white;

            questProgStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 21, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            questProgStyleCache.normal.textColor = Color.white;

            questHintStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 21, fontStyle = FontStyle.Italic, wordWrap = true };
            // hintStyle.normal.textColor는 alpha 동적이라 매 호출 갱신 (BattleScreenUI 패턴).

            panelBtnStyleCache = new GUIStyle(GUI.skin.button)
            { fontSize = 18, fontStyle = FontStyle.Bold };

            // UISurface.Button 전용 — 라벨은 GUI.Label로 그려지므로 **label 파생**이어야 한다.
            // button 파생을 넘기면 style.normal.background(유니티 기본 회색 상자)가
            // 둥근 서피스 위에 겹쳐 그려져서 없애려던 옛날 버튼이 그대로 남는다.
            panelSurfaceBtnStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            panelSurfaceBtnStyleCache.normal.textColor = UITheme.Instance.textPrimary;

            objectiveStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            objectiveStyleCache.normal.textColor = UITheme.Instance.textPrimary;

            objectiveStatusStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 19, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            objectiveStatusStyleCache.normal.textColor = UITheme.Instance.accentAmber;
        }

        private void InitNotifStyles()
        {
            if (notifStylesReady) return;
            notifStylesReady = true;

            compHeaderStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            compTitleStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            rewardStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 23, alignment = TextAnchor.MiddleCenter };
            newQuestStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            newQuestDescStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 21, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            newQuestPromptStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter };
            // 모두 textColor는 alpha 동적이라 매 호출 갱신.
        }

        private void InitDetailStyles()
        {
            if (detailStylesReady) return;
            detailStylesReady = true;

            detailHeaderStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 31, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            detailHeaderStyleCache.normal.textColor = QuestTitleCol;

            detailCloseStyleCache = new GUIStyle(GUI.skin.button)
            { fontSize = 26, fontStyle = FontStyle.Bold };
            detailCloseStyleCache.normal.textColor = Color.white;

            detailRowStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 23, alignment = TextAnchor.MiddleLeft };
            // textColor는 4분기(완료/활성/잠금/대기) 동적 갱신.

            detailStatusStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 21, alignment = TextAnchor.MiddleRight };
            // textColor 4분기 동적 갱신.

            detailRewardStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            detailRewardStyleCache.normal.textColor = UITheme.Instance.accentAmber;

            detailDescStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, wordWrap = true, alignment = TextAnchor.UpperLeft };
            detailDescStyleCache.normal.textColor = UITheme.Instance.textSecondary;

            detailRewardLabelStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            detailRewardLabelStyleCache.normal.textColor = UITheme.Instance.textMuted;
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
            detailOpen = false;
            activeDetailOpen = false;
            detailDirectScroll.Reset();
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
            // Awake는 로그인 전 실행될 수 있으므로 현재 계정 키에서 다시 읽는다.
            tutorialHidden = PlayerPrefs.GetInt(TutorialHiddenKey, 0) == 1;
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
            // 지급 조건과 같은 규칙으로 4종(캔디·경험치·아이템·곤충)을 전부 모은다.
            completedRewardText = QuestRewardFormatter.Format(quest, ResolveItemName);
            completionAnimTimer = 3f;
            rewardAnimTimer = 3f;
        }

        // ── 주간 크기 대결 문구 ──
        // 정의 배열의 제목·설명은 틀이고, 실제 문구는 이번 주 대상 종에 따라 달라진다.
        // 보상처럼 questId로 캐시할 수 없어(주차마다 바뀐다) 대상 종 ID를 키로 잡는다 —
        // OnGUI는 프레임당 여러 번 도므로 매 패스 문자열 보간을 돌리면 그대로 프레임 할당이다.
        private string contestCacheTargetId = string.Empty;
        private string contestTitleCache = string.Empty;
        private string contestDescCache = string.Empty;

        private void EnsureContestText(InsectData target)
        {
            if (target == null || contestCacheTargetId == target.insectId) return;

            contestCacheTargetId = target.insectId;
            contestTitleCache = "주간 크기 대결 — " + target.displayName;

            float bronze = WeeklyContestSchedule.RequiredMm(target, ContestTier.Bronze);
            float gold = WeeklyContestSchedule.RequiredMm(target, ContestTier.Gold);
            contestDescCache = $"이번 주는 {target.displayName}입니다. "
                + $"{InsectSizeCalculator.SizeLabel(bronze)} 이상이면 동, "
                + $"{InsectSizeCalculator.SizeLabel(gold)} 이상이면 금.";
        }

        private string QuestTitle(TutorialQuest quest)
        {
            if (quest == null) return string.Empty;
            if (quest.type != QuestType.SizeContest) return quest.title;

            InsectData target = questManager != null ? questManager.WeeklyContestTarget : null;
            if (target == null) return quest.title;
            EnsureContestText(target);
            return contestTitleCache;
        }

        private string QuestDescription(TutorialQuest quest)
        {
            if (quest == null) return string.Empty;
            if (quest.type != QuestType.SizeContest) return quest.description;

            InsectData target = questManager != null ? questManager.WeeklyContestTarget : null;
            if (target == null) return quest.description;
            EnsureContestText(target);
            return contestDescCache;
        }

        /// <summary>퀘스트별 보상 요약. 보상은 불변이라 최초 1회만 조립한다.</summary>
        private string GetRewardText(TutorialQuest quest)
        {
            if (quest == null || string.IsNullOrEmpty(quest.questId)) return string.Empty;
            if (rewardTextCache.TryGetValue(quest.questId, out string cached)) return cached;

            string text = QuestRewardFormatter.Format(quest, ResolveItemName);
            rewardTextCache[quest.questId] = text;
            return text;
        }

        /// <summary>보상 아이템 ID → 표시명. DB 미주입이면 ID 원문을 돌려준다(표시 누락 방지).</summary>
        private string ResolveItemName(string itemId)
        {
            if (itemDatabase == null || string.IsNullOrEmpty(itemId)) return itemId;
            ItemData data = itemDatabase.FindById(itemId);
            return data != null && !string.IsNullOrEmpty(data.displayName) ? data.displayName : itemId;
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
            // 형제 HUD(PlayerStatusHUD 등)와 동일한 가상 캔버스(1920x1080 / 1080x1920)에서 그려
            // 고DPI 기기에서도 폰트·패널 크기가 일관되게 보이도록 한다. 내부 좌표는 모두 가상 단위.
            UIScale.Begin();
            if (detailOpen)
                DrawDetailPanel();
            else
            {
                DrawQuestPanel();
                DrawObjectiveRow();   // 칩 아래 — 상세 팝업보다 먼저 그려 팝업이 위에 오게 한다
                if (activeDetailOpen) DrawActiveQuestDetail();
            }
            DrawCompletionNotification();
            DrawNewQuestNotification();
            UIScale.End();
        }

        // ------------------------------------------------------------------
        // 1. Active Quest Chip (compact — 제목+진행바만, 클릭하면 중앙 상세 팝업)
        // ------------------------------------------------------------------
        private void DrawQuestPanel()
        {
            if (questManager == null) return;

            InitQuestPanelStyles();

            // 칩이 실제로 그려진 경로에서만 다시 켠다 — 활성 퀘스트도 완료도 없으면 칩 자체가
            // 없으므로 목표 행이 허공에 뜨면 안 된다.
            objectiveRowVisible = false;

            UITheme theme = UITheme.Instance;
            bool guideLock = guided != null && guided.IsGuiding;
            // 미니맵과 좌변을 맞춘다 — 예전엔 20 vs 16으로 4px 어긋나 있었다.
            float chipX = MinimapUI.LeftX;

            // 숨김: 작은 복원 버튼만. 단 강제 가이드 중엔 숨김 무시(칩 강제 표시).
            if (tutorialHidden && !guideLock)
            {
                float rW = UIScale.IsMobileLayout ? 230f : 190f;
                float rH = UIScale.IsMobileLayout ? 56f : 40f;
                float rY = UIScale.IsMobileLayout
                    ? MinimapUI.StackBelowY        // 모바일: 미니맵 아래
                    : UISafeLayout.BottomY(rH);    // 데스크톱: 좌하단
                objectiveRowTop = rY + rH + UITheme.Space.XS;
                objectiveRowVisible = true;
                if (UISurface.Button(new Rect(chipX, rY, rW, rH), "▼ 퀘스트 보기", theme.surfaceRaised, panelSurfaceBtnStyleCache))
                    SetTutorialHidden(false);
                return;
            }

            TutorialQuest act = questManager.ActiveQuest;
            bool done = act == null && questManager.AllCompleted;
            if (act == null && !done) return;

            // 컴팩트 칩 — 제목+진행바만. 좌하단(조작법 제거로 빈 자리)/모바일은 미니맵 아래.
            float chipW = UIScale.IsMobileLayout
                ? Mathf.Min(500f, UIScale.VirtualScreenWidth - UIScale.VirtualSafeLeft - UIScale.VirtualSafeRight - 40f)
                : 400f;
            float cpad = UITheme.Space.S;
            // 행 높이는 폰트에서 파생한다. 예전엔 fontSize 28을 30px 상자에 그려서
            // 한글 글자(줄높이 ≈ fontSize×1.35)가 위아래로 깎여 나갔다 — "짤려 보임"의 정체.
            float ctitleH = Mathf.Ceil(questTitleStyleCache.fontSize * 1.35f);
            float cbarH = done ? 0f : Mathf.Ceil(questProgStyleCache.fontSize * 1.35f);
            float crowGap = done ? 0f : UITheme.Space.XS;
            float chipH = cpad + ctitleH + crowGap + cbarH + cpad;
            float chipY = UIScale.IsMobileLayout
                ? MinimapUI.StackBelowY            // 모바일: 미니맵 아래
                : UISafeLayout.BottomY(chipH);     // 데스크톱: 좌하단
            Rect chipRect = new Rect(chipX, chipY, chipW, chipH);
            objectiveRowTop = chipRect.yMax + UITheme.Space.XS;
            objectiveRowVisible = true;

            // 배경 — 미니맵과 같은 반투명 서피스. 각진 사각형 직접 칠하기는 금지(rules/ui-layout.md).
            UISurface.HudCard(chipRect);
            // 앰버 액센트 — 둥근 모서리를 뚫지 않게 긴 축을 반경만큼 물린다.
            UISurface.Flat(
                new Rect(chipRect.x + UITheme.Radius.Card, chipRect.y + 3f, chipRect.width - UITheme.Radius.Card * 2f, 4f),
                theme.accentAmber);

            // 숨기기 버튼(우상단) — 강제 가이드 중엔 숨김 불가(버튼 미표시).
            float cClose = UIScale.IsMobileLayout ? 44f : 30f;
            Rect cxRect = new Rect(chipRect.xMax - cClose - 8f, chipRect.y + 8f, cClose, cClose);
            if (!guideLock && UISurface.Button(cxRect, "✕", theme.surfaceRaised, panelSurfaceBtnStyleCache))
            {
                SetTutorialHidden(true);
                return;
            }

            if (done)
            {
                UIHelper.LabelFit(
                    new Rect(chipRect.x + cpad, chipRect.y + cpad, chipRect.width - cpad * 2f - cClose, ctitleH),
                    "✨ 모든 튜토리얼 완료!", doneStyleCache);
                return;
            }

            float ax = chipRect.x + cpad + 2f;
            float ay = chipRect.y + cpad;
            float aw = chipRect.width - (cpad + 2f) * 2f;

            // 제목 (X 버튼 폭 확보) — 누르면 중앙 상세 팝업.
            // 퀘스트 제목은 데이터가 길이를 정하는데 상자는 고정이다 → LabelFit으로 줄여 맞춘다.
            // questTitleStyle은 wordWrap이 꺼져 있어 넘치면 세로가 아니라 **가로**로 잘렸다.
            UIHelper.LabelFit(new Rect(ax, ay, aw - cClose - 8f, ctitleH), "★ " + act.title, questTitleStyleCache);
            ay += ctitleH + crowGap;

            // 진행 바 — 진행바는 얇으므로 Flat(각진 채움)이 맞다.
            int ccur = questManager.ActiveProgress;
            int ctgt = act.targetCount;
            float cratio = ctgt > 0 ? Mathf.Clamp01((float)ccur / ctgt) : 0f;
            float ccountW = 84f;   // "100/100"이 21px에서 ≈74px — 예전 60px는 카운트 자체가 잘렸다
            float cbarW = aw - ccountW - UITheme.Space.S;
            float cbarThick = 10f;
            float cbarY = ay + (cbarH - cbarThick) * 0.5f;
            UISurface.Flat(new Rect(ax, cbarY, cbarW, cbarThick), theme.surfaceBase);
            if (cratio > 0f)
                UISurface.Flat(new Rect(ax, cbarY, cbarW * cratio, cbarThick), theme.accentMint);
            UIHelper.LabelFit(new Rect(ax + cbarW + UITheme.Space.S, ay, ccountW, cbarH), ccur + "/" + ctgt, questProgStyleCache);

            // 칩 클릭(우상단 X 제외) → 중앙 상세 팝업 열기
            Event ce = Event.current;
            if (ce != null && ce.type == EventType.MouseDown && ce.button == 0
                && chipRect.Contains(ce.mousePosition) && !cxRect.Contains(ce.mousePosition))
            {
                SetActiveDetailOpen(true);
                ce.Use();
            }
        }

        // ------------------------------------------------------------------
        // 1a-2. 메인퀘스트 목표 행 — 칩 바로 아래. 누르면 자동 주행 시작/취소.
        // ------------------------------------------------------------------
        private void DrawObjectiveRow()
        {
            if (!objectiveRowVisible || objectiveTracker == null || !objectiveTracker.HasObjective) return;

            UITheme theme = UITheme.Instance;
            float x = MinimapUI.LeftX;
            float w = UIScale.IsMobileLayout
                ? Mathf.Min(500f, UIScale.VirtualScreenWidth - UIScale.VirtualSafeLeft - UIScale.VirtualSafeRight - 40f)
                : 400f;
            float h = Mathf.Ceil(objectiveStyleCache.fontSize * 1.35f) + UITheme.Space.S * 2f;
            Rect row = new Rect(x, objectiveRowTop, w, h);

            bool running = objectiveTracker.IsRunning;
            bool canRun = objectiveTracker.HasWorldTarget;

            // 문자열 조립은 매 프레임 할당이다. 거리는 반올림해 표시하므로(소수점이 떨리면 못 읽는다)
            // 실제로 바뀌는 건 1초에 몇 번뿐 — 그 값이 바뀔 때만 다시 만든다.
            int shownDistance = canRun && !running ? Mathf.RoundToInt(objectiveTracker.DistanceToTarget) : -1;
            string trackerLabel = objectiveTracker.Label;
            if (objectiveLabelCache == null
                || shownDistance != objectiveLabelDistance
                || running != objectiveLabelRunning
                || canRun != objectiveLabelCanRun
                || !ReferenceEquals(trackerLabel, objectiveLabelSource))
            {
                objectiveLabelDistance = shownDistance;
                objectiveLabelRunning = running;
                objectiveLabelCanRun = canRun;
                objectiveLabelSource = trackerLabel;
                objectiveLabelCache =
                    !canRun ? "◈ " + trackerLabel          // 갈 곳이 없는 목표 — 안내만, 버튼 아님
                    : running ? "■ 이동 취소"
                    : $"▶ {trackerLabel} · {shownDistance}m";
            }
            string label = objectiveLabelCache;

            if (canRun)
            {
                Color bg = running ? theme.accentCoral : theme.surfaceRaised;
                if (UISurface.Button(row, string.Empty, bg, panelSurfaceBtnStyleCache))
                    objectiveTracker.Toggle();
                // 라벨은 좌측 정렬이라 UISurface.Button의 중앙 정렬 스타일을 쓰지 않고 따로 그린다.
                UIHelper.LabelFit(
                    new Rect(row.x + UITheme.Space.M, row.y, row.width - UITheme.Space.M * 2f, row.height),
                    label, objectiveStyleCache);
            }
            else
            {
                UISurface.HudCard(row);
                UIHelper.LabelFit(
                    new Rect(row.x + UITheme.Space.M, row.y, row.width - UITheme.Space.M * 2f, row.height),
                    label, objectiveStyleCache);
            }

            // 일시 안내(길 막힘 / 다른 리전) — 행 아래 한 줄.
            string status = objectiveTracker.StatusMessage;
            if (!string.IsNullOrEmpty(status))
            {
                UIHelper.LabelFit(
                    new Rect(row.x + UITheme.Space.XS, row.yMax + 2f, row.width - UITheme.Space.XS * 2f, 30f),
                    status, objectiveStatusStyleCache);
            }
        }

        // ------------------------------------------------------------------
        // 1b. Active Quest Detail popup (center — 칩 클릭 시 열림)
        // ------------------------------------------------------------------
        private void DrawActiveQuestDetail()
        {
            if (questManager == null) return;

            InitQuestPanelStyles();

            TutorialQuest active = questManager.ActiveQuest;
            bool allCompleted = active == null && questManager.AllCompleted;
            if (active == null && !allCompleted) { SetActiveDetailOpen(false); return; }

            // 전체 화면 딤 (팝업 강조)
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0f, 0f, UIScale.VirtualScreenWidth, UIScale.VirtualScreenHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 중앙 배치 + 콘텐츠 높이 동적(설명/힌트 잘림 방지)
            float panelW = UIScale.IsMobileLayout
                ? Mathf.Min(600f, UIScale.VirtualScreenWidth - UIScale.VirtualSafeLeft - UIScale.VirtualSafeRight - 40f)
                : 520f;
            float pad = 16f;
            float wq = panelW - 32f;
            float titleH = 40f;
            float descH2 = 44f;
            float barBlockH = 0f;
            float hintH = 0f;
            if (!allCompleted)
            {
                descContentCache.text = active.description ?? "";
                descH2 = Mathf.Max(30f, questDescStyleCache.CalcHeight(descContentCache, wq));
                barBlockH = 34f;
                if (!string.IsNullOrEmpty(active.hint))
                {
                    hintContentCache.text = active.hint;
                    hintH = questHintStyleCache.CalcHeight(hintContentCache, wq - 28f) + 8f;
                }
            }
            // 내용 길이에 따라 자라는 높이 — 안전 영역을 넘으면 clamp된다.
            float panelH = UISafeLayout.ClampHeight(pad + titleH + descH2 + barBlockH + hintH + pad);
            float panelX = (UIScale.VirtualScreenWidth - panelW) * 0.5f;
            float panelY = UISafeLayout.CenteredY(panelH);
            Rect panelRect = new Rect(panelX, panelY, panelW, panelH);

            // Background
            GUI.color = new Color(0.05f, 0.08f, 0.15f, 0.85f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);

            // Gold top bar
            GUI.color = new Color(0.9f, 0.75f, 0.2f, 1f);
            GUI.DrawTexture(new Rect(panelRect.x, panelRect.y, panelRect.width, 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 닫기 버튼(우상단) — 팝업만 닫음(칩은 그대로 유지).
            float closeSize = UIScale.IsMobileLayout ? 48f : 34f;
            if (GUI.Button(new Rect(panelRect.xMax - closeSize - 6f, panelRect.y + 6f, closeSize, closeSize), "X", panelBtnStyleCache))
            {
                SetActiveDetailOpen(false);
                return;
            }

            if (allCompleted)
            {
                GUI.Label(panelRect, "\u2728 \ubaa8\ub4e0 \ud29c\ud1a0\ub9ac\uc5bc \uc644\ub8cc!", doneStyleCache);
                return;
            }

            float x = panelRect.x + 12f;
            float y = panelRect.y + pad;
            float w = panelRect.width - 24f;

            // Title \u2014 \uc6b0\uc0c1\ub2e8 \uc228\uae30\uae30 \ubc84\ud2bc\uacfc \uacb9\uce58\uc9c0 \uc54a\uac8c \ub108\ube44 \ucd95\uc18c.
            GUI.Label(new Rect(x, y, w - (UIScale.IsMobileLayout ? 64f : 34f), 34f), "\u2605 \ud018\uc2a4\ud2b8: " + active.title, questTitleStyleCache);
            y += 34f;

            // Description (동적 높이 — 긴 설명 잘림 방지)
            GUI.Label(new Rect(x, y, w, descH2), active.description, questDescStyleCache);
            y += descH2;

            // Progress bar
            int current = questManager.ActiveProgress;
            int target = active.targetCount;
            float ratio = target > 0 ? Mathf.Clamp01((float)current / target) : 0f;

            float barH = 20f;
            float barW = w - 72f;

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
            GUI.Label(new Rect(x + barW + 8f, y, 62f, barH + 8f), current + "/" + target, questProgStyleCache);
            y += barBlockH;

            // Hint with pulsing alpha \u2014 base style \uce90\uc2dc + textColor\ub9cc \ub3d9\uc801 \uac31\uc2e0 (BattleScreenUI \ud328\ud134).
            if (!string.IsNullOrEmpty(active.hint))
            {
                float hintAlpha = 0.4f + 0.4f * (0.5f + 0.5f * Mathf.Sin(hintPulse));
                questHintStyleCache.normal.textColor = new Color(QuestHintBaseCol.r, QuestHintBaseCol.g, QuestHintBaseCol.b, hintAlpha);
                GUI.Label(new Rect(x, y, w, hintH), "\ud83d\udca1 " + active.hint, questHintStyleCache);
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

            float availW = UIScale.VirtualScreenWidth - UIScale.VirtualSafeLeft - UIScale.VirtualSafeRight;
            float panelW = Mathf.Min(520f, availW - 24f);
            float panelH = UISafeLayout.ClampHeight(150f);
            float panelX = UIScale.VirtualSafeLeft + (availW - panelW) * 0.5f;
            float panelY = UISafeLayout.ContentTop + slideOffset;

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
            GUI.Label(new Rect(panelX, panelY + 12f, panelW, 44f), "\u2713 \ud034\uc2a4\ud2b8 \uc644\ub8cc!", compHeaderStyleCache);

            // Quest title
            compTitleStyleCache.normal.textColor = new Color(CompTitleBaseCol.r, CompTitleBaseCol.g, CompTitleBaseCol.b, alpha);
            GUI.Label(new Rect(panelX, panelY + 58f, panelW, 34f),
                "\"" + (completedQuestTitle ?? "") + "\"", compTitleStyleCache);

            // \ubcf4\uc0c1 \u2014 \uc870\ub9bd\uc740 OnQuestCompleted\uc5d0\uc11c QuestRewardFormatter\uac00 \uc774\ubbf8 \ub05d\ub0c8\ub2e4.
            if (!string.IsNullOrEmpty(completedRewardText))
            {
                rewardStyleCache.normal.textColor = new Color(RewardBaseCol.r, RewardBaseCol.g, RewardBaseCol.b, alpha);
                GUI.Label(new Rect(panelX, panelY + 96f, panelW, 32f),
                    "\ubcf4\uc0c1: " + completedRewardText, rewardStyleCache);
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
            float availW = UIScale.VirtualScreenWidth - UIScale.VirtualSafeLeft - UIScale.VirtualSafeRight;
            float panelW = Mathf.Min(520f, availW - 24f);
            float panelH = UISafeLayout.ClampHeight(hasDesc ? 132f : 54f);
            float panelX = UIScale.VirtualSafeLeft + (availW - panelW) * 0.5f;
            float panelY = UISafeLayout.ContentTop + 30f;   // 퀘스트 토스트(ContentTop) 아래로

            GUI.color = new Color(0.08f, 0.15f, 0.3f, 0.92f * alpha);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = new Color(0.3f, 0.6f, 1f, alpha);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panelX, panelY + panelH - 2f, panelW, 2f), Texture2D.whiteTexture);

            InitNotifStyles();
            GUI.color = new Color(1f, 1f, 1f, alpha);

            // \ud5e4\ub354: \ub2e4\uc74c \ub2e8\uacc4\uac00 "\ubb34\uc5c7"\uc778\uc9c0
            newQuestStyleCache.normal.textColor = new Color(NewQuestBaseCol.r, NewQuestBaseCol.g, NewQuestBaseCol.b, alpha);
            GUI.Label(new Rect(panelX, panelY + 8f, panelW, 36f),
                "\ub2e4\uc74c \ub2e8\uacc4 \u2192 \"" + (newQuestTitle ?? "") + "\"", newQuestStyleCache);

            if (hasDesc)
            {
                // \ubb34\uc5c7\uc744 "\ud574\uc57c \ud558\ub294\uc9c0"(\uc124\uba85)
                newQuestDescStyleCache.normal.textColor = new Color(NewQuestDescCol.r, NewQuestDescCol.g, NewQuestDescCol.b, alpha);
                GUI.Label(new Rect(panelX + 12f, panelY + 48f, panelW - 24f, 44f), newQuestDesc, newQuestDescStyleCache);

                // \uc9c4\ud589 \ub3c5\ub824
                newQuestPromptStyleCache.normal.textColor = new Color(NewQuestPromptCol.r, NewQuestPromptCol.g, NewQuestPromptCol.b, alpha);
                GUI.Label(new Rect(panelX, panelY + panelH - 34f, panelW, 28f),
                    "\u25b6 \uc9c0\uae08 \uc9c4\ud589\ud574\ubcf4\uc138\uc694!", newQuestPromptStyleCache);
            }

            GUI.color = Color.white;
        }

        // ------------------------------------------------------------------
        // 4. Detail panel (full quest list, toggled via QuickAccessBar)
        // ------------------------------------------------------------------
        // 스토리/서브 분리 결과. 퀘스트 배열은 `TutorialQuestManager.Initialize()`가 코드로 만드는
        // 고정 31개라 분류가 세션 내내 불변인데, 예전엔 상세 패널이 열려 있는 동안 **OnGUI 패스마다**
        // List 2개를 새로 만들고 31개를 다시 갈랐다(IMGUI는 한 프레임에 Layout·Repaint·입력마다 패스가 돈다).
        // 무효화 키는 원본 배열의 참조 자체 — 매니저가 다시 초기화되면 배열이 바뀌므로 자동으로 다시 갈린다.
        private List<TutorialQuest> storyQuestCache;
        private List<TutorialQuest> sideQuestCache;
        private TutorialQuest[] questPartitionSource;

        private void EnsureQuestPartition(TutorialQuest[] allQuests)
        {
            if (ReferenceEquals(questPartitionSource, allQuests)
                && storyQuestCache != null && sideQuestCache != null)
            {
                return;
            }

            if (storyQuestCache == null) storyQuestCache = new List<TutorialQuest>();
            if (sideQuestCache == null) sideQuestCache = new List<TutorialQuest>();
            storyQuestCache.Clear();
            sideQuestCache.Clear();

            foreach (TutorialQuest q in allQuests)
            {
                if (q == null) continue;
                if (q.category == QuestCategory.Side) sideQuestCache.Add(q);
                else storyQuestCache.Add(q);
            }

            questPartitionSource = allQuests;
        }

        private void DrawDetailPanel()
        {
            if (questManager == null) return;

            InitDetailStyles();

            float panelW = UIScale.IsMobileLayout
                ? Mathf.Min(600f, UIScale.VirtualScreenWidth - UIScale.VirtualSafeLeft - UIScale.VirtualSafeRight - 32f)
                : 540f;
            float panelH = UISafeLayout.ClampHeight(UIScale.IsMobileLayout ? 760f : 560f);
            float panelX = (UIScale.VirtualScreenWidth - panelW) * 0.5f;
            float panelY = UISafeLayout.CenteredY(panelH);

            // Background
            GUI.color = new Color(0.05f, 0.08f, 0.15f, 0.95f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);

            // Gold top bar
            GUI.color = new Color(0.9f, 0.75f, 0.2f, 1f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Close button size — defined before header so header width can clear it
            float detailCloseSize = UIScale.IsMobileLayout ? 56f : 48f;

            // Header
            GUI.Label(new Rect(panelX + 18f, panelY + 12f, panelW - detailCloseSize - 40f, 44f),
                "\u2605 \ud034\uc2a4\ud2b8 \ubaa9\ub85d", detailHeaderStyleCache);

            // Close button [X]
            if (GUI.Button(new Rect(panelX + panelW - detailCloseSize - 12f, panelY + 10f, detailCloseSize, detailCloseSize), "X", detailCloseStyleCache))
            {
                SetDetailOpen(false);
                return;
            }

            // Separator
            GUI.color = new Color(0.3f, 0.3f, 0.4f, 1f);
            GUI.DrawTexture(new Rect(panelX, panelY + 60f, panelW, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Quest list area
            float listX = panelX + 12f;
            float listY = panelY + 68f;
            float listW = panelW - 24f;
            float listH = panelH - 78f;

            TutorialQuest[] allQuests = questManager.GetAllQuests();
            if (allQuests == null || allQuests.Length == 0) return;

            // \uc2a4\ud1a0\ub9ac/\uc11c\ube0c \ubd84\ub9ac \u2014 \uc11c\ube0c\ub294 \ubc30\uc5f4 \ub4a4\ucabd\uc5d0 \uc815\uc758\ub428. \uacb0\uacfc\ub294 \uce90\uc2dc\ub41c\ub2e4(\uc544\ub798 \ucc38\uc870).
            EnsureQuestPartition(allQuests);
            List<TutorialQuest> story = storyQuestCache;
            List<TutorialQuest> side = sideQuestCache;

            float rowH = QuestListLayout.RowHeight;
            float headH = QuestListLayout.SectionHeaderHeight;
            // 펼쳐진 행이 있으면 그만큼 콘텐츠가 길어진다(한 번에 하나만 펼친다).
            int expandedCount = string.IsNullOrEmpty(expandedQuestId) ? 0 : 1;
            float contentH = QuestListLayout.GetContentHeight(story.Count, side.Count, expandedCount);
            Rect listArea = new Rect(listX, listY, listW, listH);
            Rect viewRect = new Rect(0, 0, listW, contentH);
            detailDirectScroll.Handle(ref detailScroll, listArea, contentH, rowH);

            detailScroll = GUI.BeginScrollView(
                listArea,
                detailScroll,
                viewRect,
                GUIStyle.none,
                GUIStyle.none);

            float ry = 0f;
            DrawQuestSectionHeader(viewRect.width, ref ry, headH, "\u2605 \uc2a4\ud1a0\ub9ac");
            for (int i = 0; i < story.Count; i++)
                DrawQuestRow(story[i], viewRect.width, ref ry, rowH, i);

            if (side.Count > 0)
            {
                DrawQuestSectionHeader(viewRect.width, ref ry, headH, "\u25c6 \uc11c\ube0c (\ubc18\ubcf5 \uc2dc \ubaa9\ud45c \uc0c1\uc2b9)");
                for (int i = 0; i < side.Count; i++)
                    DrawQuestRow(side[i], viewRect.width, ref ry, rowH, i);
            }

            GUI.EndScrollView();
        }

        private void DrawQuestSectionHeader(float width, ref float ry, float headH, string label)
        {
            GUI.color = new Color(0.9f, 0.75f, 0.2f, 0.16f);
            GUI.DrawTexture(new Rect(0, ry, width, headH), Texture2D.whiteTexture);
            GUI.color = Color.white;
            detailHeaderStyleCache.fontSize = 21;
            GUI.Label(new Rect(10f, ry + 3f, width - 20f, headH - 4f), label, detailHeaderStyleCache);
            detailHeaderStyleCache.fontSize = 31;   // \ud328\ub110 \ud5e4\ub354\uc6a9\uc73c\ub85c \ubcf5\uc6d0
            ry += headH;
        }

        // \ud55c \ud018\uc2a4\ud2b8 \ud589 \ub80c\ub354 \u2014 \uc2a4\ud1a0\ub9ac/\uc11c\ube0c \uacf5\uc6a9. \uc11c\ube0c\ub294 \ubc18\ubcf5 \uc0c1\uc2b9 \uc9c4\ud589(Lv \ud2f0\uc5b4) \ud45c\uc2dc.
        // \ud589\uc744 \ub204\ub974\uba74 \uadf8 \uc790\ub9ac\uc5d0\uc11c \ud3bc\uccd0\uc838 \uc124\uba85\u00b7\uc9c4\ud589\u00b7\uc804\uccb4 \ubcf4\uc0c1\uc744 \ubcf4\uc5ec\uc900\ub2e4(\uc544\ucf54\ub514\uc5b8).
        private void DrawQuestRow(TutorialQuest quest, float width, ref float ry, float rowH, int idx)
        {
            bool isSide = quest.category == QuestCategory.Side;
            bool isActiveStory = false;
            string icon;
            string statusText;
            Color statusCol;
            Color titleCol;
            int cur = 0;
            int tgt = Mathf.Max(0, quest.targetCount);
            bool showBar = false;

            if (isSide)
            {
                bool unlocked = string.IsNullOrEmpty(quest.prerequisiteQuestId)
                    || questManager.IsQuestCompleted(quest.prerequisiteQuestId);
                tgt = questManager.EffectiveTarget(quest);
                if (!unlocked)
                {
                    icon = "\ud83d\udd12 "; titleCol = RowLockedCol;
                    statusText = "\ubbf8\ud574\uae08"; statusCol = RowLockedCol;
                }
                else if (!quest.repeatable && questManager.IsQuestCompleted(quest.questId))
                {
                    icon = "\u2713 "; titleCol = RowCompletedCol;
                    statusText = "\uc644\ub8cc"; statusCol = StatusCompletedCol;
                    cur = tgt;
                }
                else
                {
                    icon = "\u25c6 "; titleCol = Color.white;
                    cur = questManager.GetSideProgress(quest.questId);
                    statusText = quest.repeatable
                        ? cur + "/" + tgt + "  Lv" + (questManager.GetSideRepeatCount(quest.questId) + 1)
                        : cur + "/" + tgt;
                    statusCol = StatusActiveCol;
                    showBar = true;
                }
            }
            else
            {
                bool isCompleted = questManager.IsQuestCompleted(quest.questId);
                isActiveStory = questManager.ActiveQuest != null
                    && questManager.ActiveQuest.questId == quest.questId;
                bool isLocked = !isCompleted && !isActiveStory
                    && !string.IsNullOrEmpty(quest.prerequisiteQuestId)
                    && !questManager.IsQuestCompleted(quest.prerequisiteQuestId);

                if (isCompleted)
                {
                    icon = "\u2713 "; titleCol = RowCompletedCol;
                    statusText = "\uc644\ub8cc"; statusCol = StatusCompletedCol;
                    cur = tgt;
                }
                else if (isActiveStory)
                {
                    icon = "\u25b6 "; titleCol = Color.white;
                    cur = questManager.ActiveProgress;
                    statusText = cur + "/" + tgt;
                    statusCol = StatusActiveCol;
                    showBar = true;
                }
                else if (isLocked)
                {
                    icon = "\ud83d\udd12 "; titleCol = RowLockedCol;
                    statusText = "\ubbf8\ud574\uae08"; statusCol = RowLockedCol;
                }
                else
                {
                    icon = "  "; titleCol = RowPendingCol;
                    statusText = "\ub300\uae30"; statusCol = RowPendingCol;
                }
            }

            bool expanded = !string.IsNullOrEmpty(quest.questId) && quest.questId == expandedQuestId;
            float totalH = QuestListLayout.GetRowHeight(expanded);
            UITheme t = UITheme.Instance;

            // \ubc30\uacbd(\uad50\ub300) + \ud65c\uc131 \uc2a4\ud1a0\ub9ac \ud558\uc774\ub77c\uc774\ud2b8 \u2014 \ud3bc\uce5c \uc601\uc5ed\uae4c\uc9c0 \ud568\uaed8 \uce60\ud55c\ub2e4.
            if (idx % 2 == 0)
            {
                GUI.color = new Color(0.08f, 0.1f, 0.18f, 0.6f);
                GUI.DrawTexture(new Rect(0, ry, width, totalH), Texture2D.whiteTexture);
            }
            if (isActiveStory)
            {
                GUI.color = new Color(0.2f, 0.4f, 0.15f, 0.4f);
                GUI.DrawTexture(new Rect(0, ry, width, totalH), Texture2D.whiteTexture);
            }
            if (expanded)
            {
                GUI.color = new Color(t.accentAmber.r, t.accentAmber.g, t.accentAmber.b, 0.14f);
                GUI.DrawTexture(new Rect(0, ry, width, totalH), Texture2D.whiteTexture);
                GUI.color = t.accentAmber;
                GUI.DrawTexture(new Rect(0, ry, 3f, totalH), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;

            detailRowStyleCache.normal.textColor = titleCol;
            GUI.Label(new Rect(10f, ry, width * 0.46f, rowH), icon + QuestTitle(quest), detailRowStyleCache);

            // \ubcf4\uc0c1 \uc694\uc57d \u2014 \ubaa9\ub85d\uc5d0\uc11c "\uc774\uac70 \uae68\uba74 \ubb58 \uc8fc\ub098"\uac00 \ubc14\ub85c \ubcf4\uc774\uac8c.
            string rewardText = GetRewardText(quest);
            if (!string.IsNullOrEmpty(rewardText))
            {
                GUI.Label(new Rect(width * 0.47f, ry, width * 0.32f, rowH), rewardText, detailRewardStyleCache);
            }

            detailStatusStyleCache.normal.textColor = statusCol;
            GUI.Label(new Rect(width * 0.80f, ry, width * 0.18f, rowH), statusText, detailStatusStyleCache);

            if (showBar && tgt > 0)
            {
                UIHelper.DrawProgressBar(
                    new Rect(10f, ry + rowH - 9f, width - 20f, 4f),
                    cur / (float)tgt,
                    t.surfaceBase,
                    t.accentMint);
            }

            // \ud074\ub9ad \ud310\uc815\uc740 \ud5e4\ub354 \ud589\uc5d0\ub9cc \u2014 \ud3bc\uce5c \ub0b4\uc6a9\uc744 \ub204\ub97c \ub54c \uc811\ud788\uc9c0 \uc54a\uac8c \ud55c\ub2e4.
            if (GUI.Button(new Rect(0f, ry, width, rowH), string.Empty, GUIStyle.none)
                && !detailDirectScroll.IsDragging)
            {
                expandedQuestId = expanded ? string.Empty : quest.questId;
            }

            if (expanded)
            {
                float ey = ry + rowH + 6f;
                string desc = QuestDescription(quest);
                if (!string.IsNullOrEmpty(desc))
                {
                    GUI.Label(new Rect(16f, ey, width - 32f, 52f), desc, detailDescStyleCache);
                }
                ey += 56f;

                if (tgt > 0)
                {
                    GUI.Label(new Rect(16f, ey, 60f, 26f), "\uc9c4\ud589", detailRewardLabelStyleCache);
                    UIHelper.DrawProgressBar(
                        new Rect(80f, ey + 9f, width - 176f, 8f),
                        cur / (float)tgt,
                        t.surfaceBase,
                        t.accentMint);
                    detailStatusStyleCache.normal.textColor = statusCol;
                    GUI.Label(new Rect(width - 88f, ey, 72f, 26f), cur + " / " + tgt, detailStatusStyleCache);
                }
                ey += 34f;

                GUI.Label(new Rect(16f, ey, 60f, 26f), "\ubcf4\uc0c1", detailRewardLabelStyleCache);
                GUI.Label(new Rect(80f, ey, width - 96f, 26f),
                    string.IsNullOrEmpty(rewardText) ? "\uc5c6\uc74c" : rewardText, detailRewardStyleCache);
            }

            ry += totalH;
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

        public void AutoWire(GuidedTutorialController guidedController)
        {
            if (guided == null) guided = guidedController;
        }

        /// <summary>
        /// 메인퀘스트 목표 행 소스. 미주입이면 행만 안 그린다(퀘스트 칩은 정상 동작).
        /// </summary>
        public void AutoWire(InsectGame.Story.StoryObjectiveTracker tracker)
        {
            if (objectiveTracker == null) objectiveTracker = tracker;
        }

        /// <summary>보상 아이템의 표시명 조회용. 미주입이면 목록·배너에 아이템 ID가 그대로 나온다.</summary>
        public void AutoWire(ItemDatabase database)
        {
            if (itemDatabase == null) itemDatabase = database;
            // AutoWire가 첫 렌더보다 늦게 올 수 있다. 그 사이 ID 원문으로 굳은 캐시를 버린다.
            rewardTextCache.Clear();
        }
    }
}
