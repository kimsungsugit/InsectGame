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
        // 펼친 행에 [따라가기]가 붙는가 — DrawDetailPanel이 패스마다 한 번 정하고 DrawQuestRow가 읽는다.
        private bool expandedRowTrackable;

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
        // 목표가 다른 리전일 때 열어 줄 지도. 미주입이면 트래커가 안내 문구로 폴백한다.
        private RegionMapUI regionMapUi;
        // 칩(또는 숨김 버튼)이 끝나는 y — 목표 행이 그 아래에 붙는다. 칩 높이가 상태마다
        // 달라(완료/숨김/진행 중) 상수로 둘 수 없어, 그린 쪽이 실제 값을 남긴다.
        private float objectiveRowTop;
        private float objectiveRowLeft;
        private float objectiveRowWidth;
        private bool objectiveRowVisible;

        // 목표 행 높이 — 칩이 데스크톱에서 이만큼 자리를 비워 두고 위로 올라간다(그리기와 같은 값이어야 한다).
        private float ObjectiveRowHeight => QuestChipLayout.RowHeight;

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
        private GUIStyle detailSectionStyleCache;
        private bool detailStylesReady;

        // 퀘스트별 보상 요약 문자열 캐시. 보상은 불변이라 최초 1회만 조립하면 되고,
        // 목록이 매 프레임 그려지므로 캐시하지 않으면 행마다 문자열 할당이 쌓인다.
        private readonly Dictionary<string, string> rewardTextCache = new Dictionary<string, string>();

        private static Color DoneTextCol => UITheme.Instance.accentMint;
        private static Color QuestTitleCol => UITheme.Instance.accentAmber;
        private static Color QuestHintBaseCol => UITheme.Instance.textSecondary;
        private static Color CompHeaderBaseCol => UITheme.Instance.accentMint;
        private static Color CompTitleBaseCol => UITheme.Instance.textPrimary;
        private static Color RewardBaseCol => UITheme.Instance.accentAmber;
        private static Color NewQuestBaseCol => UITheme.Instance.accentCoral;
        private static Color NewQuestDescCol => UITheme.Instance.textSecondary;
        private static Color NewQuestPromptCol => UITheme.Instance.accentMint;
        private static Color RowCompletedCol => UITheme.Instance.accentMint;
        private static Color RowLockedCol => UITheme.Instance.textMuted;
        private static Color RowPendingCol => UITheme.Instance.textSecondary;
        private static Color StatusCompletedCol => UITheme.Instance.accentMint;
        private static Color StatusActiveCol => UITheme.Instance.accentAmber;

        private void InitQuestPanelStyles()
        {
            if (questPanelStylesReady) return;
            questPanelStylesReady = true;

            doneStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            doneStyleCache.normal.textColor = DoneTextCol;

            questTitleStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = QuestChipLayout.TitleFontSize, fontStyle = FontStyle.Bold };
            questTitleStyleCache.normal.textColor = QuestTitleCol;

            questDescStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, wordWrap = true };
            questDescStyleCache.normal.textColor = Color.white;

            questProgStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = QuestChipLayout.ProgressFontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            questProgStyleCache.normal.textColor = Color.white;

            questHintStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 25, fontStyle = FontStyle.Italic, wordWrap = true };
            // hintStyle.normal.textColor는 alpha 동적이라 매 호출 갱신 (BattleScreenUI 패턴).

            panelBtnStyleCache = new GUIStyle(GUI.skin.button)
            { fontSize = 22, fontStyle = FontStyle.Bold };

            // UISurface.Button 전용 — 라벨은 GUI.Label로 그려지므로 **label 파생**이어야 한다.
            // button 파생을 넘기면 style.normal.background(유니티 기본 회색 상자)가
            // 둥근 서피스 위에 겹쳐 그려져서 없애려던 옛날 버튼이 그대로 남는다.
            panelSurfaceBtnStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            panelSurfaceBtnStyleCache.normal.textColor = UITheme.Instance.textPrimary;

            objectiveStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = QuestChipLayout.ObjectiveFontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            objectiveStyleCache.normal.textColor = UITheme.Instance.textPrimary;

            objectiveStatusStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 23, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            objectiveStatusStyleCache.normal.textColor = UITheme.Instance.accentAmber;
        }

        /// <summary>
        /// 한 줄짜리 라벨이 <b>안 잘리는</b> 최소 상자 높이. 한글 줄높이는 대략
        /// <c>fontSize × 1.35</c>이라 그보다 낮은 Rect에 그리면 위아래가 깎인다.
        ///
        /// <b>상자 높이를 숫자로 적지 말 것.</b> 폰트를 키우는 순간 전부 어긋나는데
        /// 컴파일도 되고 예외도 없어서 조용히 잘린다 — 2026-08-08에 폰트를 1.25배 올리면서
        /// 실제로 10곳이 그렇게 깨졌다(퀘스트 완료 알림·상세 패널 헤더·보상 줄).
        /// </summary>
        private static float RowH(GUIStyle style)
        {
            return Mathf.Ceil(style.fontSize * 1.35f);
        }

        // 완료·다음 퀘스트 알림의 글자 크기 — 패널 높이가 여기서 나온다(아래 순수 배치와 스타일이 같은 값을 쓴다).
        private const int DoneHeaderFont = 40;
        private const int DoneTitleFont = 31;
        private const int DoneRewardFont = 28;
        private const int NextHeaderFont = 31;
        private const int NextDescFont = 26;
        private const int NextPromptFont = 24;
        /// <summary>알림 패널이 바라는 폭 — 무대(<see cref="HudStage.Area"/>)가 좁으면 그 폭으로 줄어든다.</summary>
        public const float NoticePanelWidth = 640f;

        private static float LineH(int fontSize) => Mathf.Ceil(fontSize * 1.35f);

        /// <summary>
        /// 퀘스트 완료 알림의 자리 — 가운데 무대의 차례 항목(<see cref="HudStageItem.QuestDone"/>). 예전엔 화면 위 가운데(ContentTop)라
        /// 리전 배너·내기 점수판·세로 화면의 단축 바와 미니맵을 덮었다.
        /// </summary>
        public static Rect QuestDoneRect(HudFrame f, bool hasReward)
        {
            float h = 12f + LineH(DoneHeaderFont) + 6f + LineH(DoneTitleFont)
                      + (hasReward ? 6f + LineH(DoneRewardFont) : 0f) + 14f;
            return HudStage.Place(f, HudStageItem.QuestDone, NoticePanelWidth, h);
        }

        /// <summary>다음 퀘스트 알림의 자리 — 가운데 무대의 차례 항목(<see cref="HudStageItem.QuestNext"/>).</summary>
        public static Rect QuestNextRect(HudFrame f, bool hasDesc)
        {
            float h = hasDesc
                ? 8f + LineH(NextHeaderFont) + 6f + LineH(NextDescFont) * 2f + 6f + LineH(NextPromptFont) + 10f
                : 8f + LineH(NextHeaderFont) + 10f;
            return HudStage.Place(f, HudStageItem.QuestNext, NoticePanelWidth, h);
        }

        private void InitNotifStyles()
        {
            if (notifStylesReady) return;
            notifStylesReady = true;

            compHeaderStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = DoneHeaderFont, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            compTitleStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = DoneTitleFont, alignment = TextAnchor.MiddleCenter };
            rewardStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = DoneRewardFont, alignment = TextAnchor.MiddleCenter };
            newQuestStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = NextHeaderFont, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            newQuestDescStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = NextDescFont, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            newQuestPromptStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = NextPromptFont, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter };
            // 모두 textColor는 alpha 동적이라 매 호출 갱신.
        }

        private void InitDetailStyles()
        {
            if (detailStylesReady) return;
            detailStylesReady = true;

            detailHeaderStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 37, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            detailHeaderStyleCache.normal.textColor = QuestTitleCol;

            detailCloseStyleCache = new GUIStyle(GUI.skin.button)
            { fontSize = 30, fontStyle = FontStyle.Bold };
            detailCloseStyleCache.normal.textColor = Color.white;

            detailRowStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, alignment = TextAnchor.MiddleLeft };
            // textColor는 4분기(완료/활성/잠금/대기) 동적 갱신.

            detailStatusStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 25, alignment = TextAnchor.MiddleRight };
            // textColor 4분기 동적 갱신.

            detailRewardStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 23,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            detailRewardStyleCache.normal.textColor = UITheme.Instance.accentAmber;

            detailDescStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 24, wordWrap = true, alignment = TextAnchor.UpperLeft };
            detailDescStyleCache.normal.textColor = UITheme.Instance.textSecondary;

            detailRewardLabelStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 23, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            detailRewardLabelStyleCache.normal.textColor = UITheme.Instance.textMuted;

            // 목록 안의 "★ 스토리" / "◆ 서브" 구분줄. **패널 헤더와 스타일을 공유하지 않는다** —
            // 예전엔 헤더 스타일의 fontSize를 21로 바꿔 그리고 상수 31로 되돌렸는데, 헤더가
            // 37로 커진 뒤에도 그 31이 그대로 남아 **패널 제목이 첫 프레임 이후 영구히 작아졌다**
            // (컴파일도 되고 예외도 없다. 이 파일의 RowH 주석이 경고하는 "숫자를 박아두면
            // 어긋난다"의 실례가 자기 안에 있었다).
            detailSectionStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 21, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            detailSectionStyleCache.normal.textColor = QuestTitleCol;
        }

        private void OnEnable()
        {
            SubscribeObjectiveEvents();
            if (questManager != null)
            {
                questManager.QuestActivated += OnQuestActivated;
                questManager.QuestProgressUpdated += OnQuestProgressUpdated;
                questManager.QuestCompleted += OnQuestCompleted;
            }
        }

        private void OnDisable()
        {
            if (objectiveTracker != null) objectiveTracker.MapRequested -= OnObjectiveMapRequested;
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

            // **모달이 열려 있는 동안에는 알림 수명을 태우지 않는다.**
            // 퀘스트는 전투·포획 중에 올라가는 일이 대부분인데, 완료 배너는 3초·새 퀘스트 배너는
            // 2초짜리다. 예전엔 전투 결과창을 보고 있는 사이에 그 3초가 그대로 흘러가, 창을 닫고
            // 필드로 돌아오면 **이미 사라진 뒤**였다 — "전투는 끝났는데 뭐가 진행됐는지 모르겠다"가
            // 그래서 생긴다. 타이머를 멈춰 두면 창을 닫는 순간부터 온전히 3초를 보게 된다.
            // (모달 위에 겹쳐 그리지 않는 이유는 이 파일에 GUI.depth가 없어 그리기 순서가
            //  불확정이고, 배너가 상점·도감 위를 덮는 건 더 나쁘기 때문이다.)
            if (ModalUIRegistry.IsAnyOpen()) return;
            // 꿈 동안은 알림을 그리지 않으므로(OnGUI) 수명도 멈춘다 — 무대를 보이지 않는 카드가 차지하지 않게.
            if (InsectGame.Core.DreamPrologueState.Active) return;

            // 두 알림은 가운데 무대(HudStage)에 선다 — 다른 카드(포획 결과·동굴 진입 알림 등)가 서 있으면 차례를 기다리고,
            // 기다리는 동안은 수명을 태우지 않는다(모달과 같은 이유 — 기다리다 사라지면 못 본다).
            bool doneShowing = completionAnimTimer > 0f && HudStage.Request(HudStageItem.QuestDone);
            if (doneShowing) completionAnimTimer -= Time.deltaTime;
            if (rewardAnimTimer > 0f) rewardAnimTimer -= Time.deltaTime;
            // 완료 알림이 끝나길 기다린 뒤(newQuestDelay) 새 퀘스트 배너 수명 소진. 완료 알림이 기다리는 동안은 지연도 멈춘다.
            if (newQuestDelay > 0f)
            {
                if (completionAnimTimer <= 0f || doneShowing) newQuestDelay -= Time.deltaTime;
            }
            else if (newQuestAnimTimer > 0f)
            {
                // 숨김 중엔 그리지 않으니 무대도 잡지 않고 수명만 흐른다(예전과 같다).
                if (tutorialHidden || HudStage.Request(HudStageItem.QuestNext)) newQuestAnimTimer -= Time.deltaTime;
            }
        }

        private void OnGUI()
        {
            // 「챔피언의 꿈」 동안은 퀘스트 칩·목표 행·알림을 모두 숨긴다 — 꿈 밖의 진행이 꿈속에 비치면 안 된다.
            if (InsectGame.Core.DreamPrologueState.Active) return;

            // 형제 HUD(PlayerStatusHUD 등)와 동일한 가상 캔버스(1920x1080 / 1080x1920)에서 그려
            // 고DPI 기기에서도 폰트·패널 크기가 일관되게 보이도록 한다. 내부 좌표는 모두 가상 단위.
            UIScale.Begin();
            if (detailOpen)
            {
                DrawDetailPanel();
                // **알림은 그리지 않는다.** 상단 알림(ContentTop 부근)과 중앙 상세 패널은 원래
                // 세로로 스쳤는데, 알림 높이를 폰트에서 파생시키며 커지자 상세 패널의 **제목 줄을
                // 덮었다**(본문은 그 아래라 멀쩡해서 "타이틀만 가려진다"로 보였다).
                // 상세 목록은 모달이므로 그 위에 무언가를 겹치는 것 자체가 맞지 않는다.
                // 타이머는 Update가 계속 줄이므로, 닫고 나면 남은 시간만큼 이어서 뜬다.
                UIScale.End();
                return;
            }

            DrawQuestPanel();
            DrawObjectiveRow();   // 칩 아래 — 상세 팝업보다 먼저 그려 팝업이 위에 오게 한다
            if (activeDetailOpen) DrawActiveQuestDetail();

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

            // 모바일에서만 칩이 좌측 스택(미니맵 아래)에 있다. 그 자리가 상태 패널에 덮이면
            // 그리지 않는다 — 안 그리면 보이지도 않는 목표 행 버튼이 클릭을 가로챈다.
            // 데스크톱 칩은 좌하단이라 패널과 무관하므로 조건에 IsMobileLayout이 붙는다.
            if (UIScale.IsMobileLayout && MinimapUI.LeftStackOccluded) return;

            UITheme theme = UITheme.Instance;
            bool guideLock = guided != null && guided.IsGuiding;
            // 자리는 QuestChipLayout(순수 계산)이 정한다 — 세로 모바일은 미니맵 아래, 가로 모바일은 미니맵 오른쪽(아래는 조이스틱
            // 시작 영역이다), 데스크톱은 좌하단에서 단축 바 왼쪽 끝까지만 쓰고 목표 행 자리까지 비워 두고 바닥에서 위로 쌓는다.
            HudFrame frame = HudFrame.Current;
            float stackW = QuestChipLayout.StackWidth(frame);
            float rowReserve = objectiveTracker != null && objectiveTracker.HasObjective ? ObjectiveRowHeight : 0f;

            // 숨김: 작은 복원 버튼만. 단 강제 가이드 중엔 숨김 무시(칩 강제 표시).
            if (tutorialHidden && !guideLock)
            {
                Rect restoreRect = QuestChipLayout.RestoreRect(frame, rowReserve);
                objectiveRowLeft = restoreRect.x;
                objectiveRowWidth = stackW;
                objectiveRowTop = QuestChipLayout.Row(restoreRect, stackW, rowReserve).y;
                objectiveRowVisible = true;
                FieldHudInput.RegisterBlockingRect(restoreRect);
                if (UISurface.Button(restoreRect, "▼ 퀘스트 보기", theme.surfaceRaised, panelSurfaceBtnStyleCache))
                    SetTutorialHidden(false);
                return;
            }

            TutorialQuest act = questManager.ActiveQuest;
            bool done = act == null && questManager.AllCompleted;
            if (act == null && !done) return;

            // 컴팩트 칩 — 제목+진행바만. 데스크톱 좌하단(조작법 제거로 빈 자리) / 모바일은 미니맵 곁(위 QuestChipLayout).
            float cpad = UITheme.Space.S;
            float ctitleH = RowH(questTitleStyleCache);
            float cbarH = done ? 0f : RowH(questProgStyleCache);
            float crowGap = done ? 0f : UITheme.Space.XS;
            float chipH = cpad + ctitleH + crowGap + cbarH + cpad;
            Rect chipRect = QuestChipLayout.ChipRect(frame, chipH, rowReserve);
            objectiveRowLeft = chipRect.x;
            objectiveRowWidth = stackW;
            objectiveRowTop = QuestChipLayout.Row(chipRect, stackW, rowReserve).y;
            objectiveRowVisible = true;

            // **필드 위에 겹쳐 그리는 버튼은 자기 영역을 매 프레임 등록한다.**
            // 안 하면 그 탭이 월드 클릭-이동으로 **새어** 캐릭터가 칩 아래 지점으로 걸어간다 —
            // `PlayerMovement`가 `Input.GetMouseButtonDown(0)`을 Update에서 따로 폴링하는데,
            // 그 시점엔 모달도 안 열려 있고 IMGUI는 EventSystem을 거치지 않아 방어선이 이것뿐이다
            // (rules/ui-layout.md). 칩은 눌러서 상세를 여는 버튼이고 안에 ✕도 있다.
            // 좌표는 `UIScale.Begin()` 안이라 가상 좌표 그대로 넘긴다.
            FieldHudInput.RegisterBlockingRect(chipRect);

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

            // **모달 위에는 그리지 않는다.** 형제 HUD(MinimapUI·KeyGuideHUD)와 같은 규칙이고,
            // 여기는 이유가 하나 더 있다 — 이 행이 지도를 여는 버튼이 되면서, 지도가 열린 뒤에도
            // 그 위에 남아 있으면 같은 탭이 지도 쪽 컨트롤과 함께 먹힌다(IMGUI는 z-order로
            // 히트 테스트를 가르지 않는다).
            if (ModalUIRegistry.IsAnyOpen()) return;

            UITheme theme = UITheme.Instance;
            // 자리·폭은 칩을 그릴 때 QuestChipLayout이 정해 둔 것을 쓴다(데스크톱은 칩이 이 행 높이만큼 올라가 있다).
            Rect row = new Rect(objectiveRowLeft, objectiveRowTop, objectiveRowWidth, ObjectiveRowHeight);

            // 칩과 같은 이유로 등록한다 — 여기는 더 나쁘다. 이 행을 누르면 자동 주행이 시작되는데
            // **같은 탭이 클릭-이동으로도 발화해** 목표로 달려가면서 동시에 탭 지점으로 걸어가려
            // 든다. 버튼이 아닌 안내 카드일 때도 생김새가 같아 눌리므로 함께 막는다.
            FieldHudInput.RegisterBlockingRect(row);

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

            // 의뢰를 따라가는 중이면 오른쪽 끝에 ✕(해제 → 본편 목표로 복귀)를 붙인다.
            // **✕를 먼저 그린다** — IMGUI는 먼저 처리된 버튼이 MouseDown을 가져가므로, 행 버튼이
            // 먼저면 ✕를 눌러도 자동 주행이 시작된다. 행은 ✕ 폭만큼 줄여 겹치지 않게 한다.
            Rect body = row;
            if (objectiveTracker.IsTrackingTale)
            {
                Rect closeRect = new Rect(row.xMax - row.height, row.y, row.height, row.height);
                body = new Rect(row.x, row.y, row.width - row.height - UITheme.Space.XS, row.height);
                if (UISurface.Button(closeRect, "✕", theme.surfaceRaised, panelSurfaceBtnStyleCache))
                {
                    objectiveTracker.StopTrackingTale();
                    return;   // 목표가 본편으로 바뀌었다 — 옛 라벨로 한 번 더 그리지 않는다
                }
            }

            if (canRun)
            {
                Color bg = running ? theme.accentCoral : theme.surfaceRaised;
                if (UISurface.Button(body, string.Empty, bg, panelSurfaceBtnStyleCache))
                    objectiveTracker.Toggle();
                // 라벨은 좌측 정렬이라 UISurface.Button의 중앙 정렬 스타일을 쓰지 않고 따로 그린다.
                UIHelper.LabelFit(
                    new Rect(body.x + UITheme.Space.M, body.y, body.width - UITheme.Space.M * 2f, body.height),
                    label, objectiveStyleCache);
            }
            else
            {
                UISurface.HudCard(body);
                UIHelper.LabelFit(
                    new Rect(body.x + UITheme.Space.M, body.y, body.width - UITheme.Space.M * 2f, body.height),
                    label, objectiveStyleCache);
            }

            // 일시 안내(길 막힘 / 다른 리전) — 행 아래 한 줄.
            string status = objectiveTracker.StatusMessage;
            if (!string.IsNullOrEmpty(status))
            {
                UIHelper.LabelFit(
                    new Rect(row.x + UITheme.Space.XS, row.yMax + 2f,
                        row.width - UITheme.Space.XS * 2f, RowH(objectiveStatusStyleCache)),
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
            // 폰트를 키운 만큼 폭도 넓힌다 — 폭을 그대로 두면 같은 문장이 줄 수만 늘어
            // 세로로 길어진다(한국어는 같은 뜻을 더 긴 글자수로 쓴다).
            float panelW = UIScale.IsMobileLayout
                ? Mathf.Min(760f, UIScale.VirtualScreenWidth - UIScale.VirtualSafeLeft - UIScale.VirtualSafeRight - 40f)
                : 700f;
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
            UIHelper.LabelFit(new Rect(x, y, w - (UIScale.IsMobileLayout ? 64f : 34f), 34f), "\u2605 \ud018\uc2a4\ud2b8: " + active.title, questTitleStyleCache);
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
            // 칩 쪽은 "100/100"이 안 들어가 84px로 넓혔는데 여기는 62px 그대로였다.
            // questProgStyle은 wordWrap이 꺼져 있어 넘치면 세로가 아니라 **가로**로 잘린다.
            UIHelper.LabelFit(new Rect(x + barW + 8f, y, 62f, barH + 8f),
                current + "/" + target, questProgStyleCache);
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
            if (!HudStage.Request(HudStageItem.QuestDone)) return;   // 무대에 다른 카드가 서 있다 — 차례를 기다린다

            // 나타날 때는 위에서 미끄러지지 않고 제자리에서 밝아진다 — 무대 밖(리전 배너·내기 점수판)으로 삐져나오지 않게.
            float alpha;
            if (completionAnimTimer > 2.5f)
                alpha = Mathf.Clamp01((3f - completionAnimTimer) / 0.5f);
            else if (completionAnimTimer < 0.5f)
                alpha = Mathf.Clamp01(completionAnimTimer / 0.5f);
            else
                alpha = 1f;

            InitNotifStyles();

            // \ud589 \ub192\uc774\u00b7\ud328\ub110 \ub192\uc774\ub97c **\ud3f0\ud2b8\uc5d0\uc11c \ud30c\uc0dd**\ud55c\ub2e4. \uace0\uc815 \uc22b\uc790\ub85c \ub450\uba74 \ud3f0\ud2b8\ub97c \ud0a4\uc6b0\ub294 \uc21c\uac04
            // \ud55c\uae00 \uae00\uc790(\uc904\ub192\uc774 \u2248 fontSize\u00d71.35)\uac00 \uc704\uc544\ub798\ub85c \uae4e\uc778\ub2e4 \u2014 \uc544\ub798 \ud018\uc2a4\ud2b8 \ubc30\ub108\uac00 \uc774\ubbf8
            // \uac19\uc740 \uc774\uc720\ub85c \ud30c\uc0dd\uc2dd\uc744 \uc4f0\uace0 \uc788\ub294\ub370 \uc774 \uc54c\ub9bc \ud328\ub110\ub9cc \uc0c1\uc218\ub85c \ub0a8\uc544 \uc2e4\uc81c\ub85c \uc798\ub838\ub2e4.
            float headH = RowH(compHeaderStyleCache);
            float titleH = RowH(compTitleStyleCache);
            float rewH = RowH(rewardStyleCache);
            bool hasReward = !string.IsNullOrEmpty(completedRewardText);

            Rect panel = QuestDoneRect(HudFrame.Current, hasReward);
            HudStage.Request(HudStageItem.QuestDone, panel);   // 무대 안의 가운데 것들이 겹치면 비켜서게 자리를 알린다
            float panelW = panel.width;
            float panelH = panel.height;
            float panelX = panel.x;
            float panelY = panel.y;

            // Background
            GUI.color = new Color(0.15f, 0.12f, 0.02f, 0.9f * alpha);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);

            // Gold border (top + bottom)
            GUI.color = new Color(0.9f, 0.75f, 0.2f, alpha);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panelX, panelY + panelH - 3f, panelW, 3f), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 1f, 1f, alpha);

            // "Quest Complete!" header \u2014 base \uce90\uc2dc + textColor alpha \ub3d9\uc801
            float rowY = panelY + 12f;
            compHeaderStyleCache.normal.textColor = new Color(CompHeaderBaseCol.r, CompHeaderBaseCol.g, CompHeaderBaseCol.b, alpha);
            GUI.Label(new Rect(panelX, rowY, panelW, headH), "\u2713 \ud034\uc2a4\ud2b8 \uc644\ub8cc!", compHeaderStyleCache);
            rowY += headH + 6f;

            // Quest title
            compTitleStyleCache.normal.textColor = new Color(CompTitleBaseCol.r, CompTitleBaseCol.g, CompTitleBaseCol.b, alpha);
            // 제목 길이는 데이터가 정한다 — 무대가 좁은 화면(가로 모바일 섬)에서 잘리지 않게 줄여 맞춘다.
            UIHelper.LabelFit(new Rect(panelX + 12f, rowY, panelW - 24f, titleH),
                "\"" + (completedQuestTitle ?? "") + "\"", compTitleStyleCache);
            rowY += titleH + 6f;

            // \ubcf4\uc0c1 \u2014 \uc870\ub9bd\uc740 OnQuestCompleted\uc5d0\uc11c QuestRewardFormatter\uac00 \uc774\ubbf8 \ub05d\ub0c8\ub2e4.
            if (hasReward)
            {
                rewardStyleCache.normal.textColor = new Color(RewardBaseCol.r, RewardBaseCol.g, RewardBaseCol.b, alpha);
                UIHelper.LabelFit(new Rect(panelX + 12f, rowY, panelW - 24f, rewH),
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
            if (!HudStage.Request(HudStageItem.QuestNext)) return;   // 무대 차례를 기다린다

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

            InitNotifStyles();

            bool hasDesc = !string.IsNullOrEmpty(newQuestDesc);

            // 높이는 전부 폰트에서 파생한다(RowH 주석 참조). 설명은 두 줄까지 잡는다 —
            // 길이가 데이터에서 오므로 wordWrap이 접히는 경우가 있다.
            float nqHeadH = RowH(newQuestStyleCache);
            float nqDescH = RowH(newQuestDescStyleCache) * 2f;
            float nqPromptH = RowH(newQuestPromptStyleCache);
            Rect panel = QuestNextRect(HudFrame.Current, hasDesc);
            HudStage.Request(HudStageItem.QuestNext, panel);
            float panelW = panel.width;
            float panelH = panel.height;
            float panelX = panel.x;
            float panelY = panel.y;

            GUI.color = new Color(0.08f, 0.15f, 0.3f, 0.92f * alpha);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = new Color(0.3f, 0.6f, 1f, alpha);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panelX, panelY + panelH - 2f, panelW, 2f), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 1f, 1f, alpha);

            // 헤더: 다음 단계가 "무엇"인지
            float nqY = panelY + 8f;
            newQuestStyleCache.normal.textColor = new Color(NewQuestBaseCol.r, NewQuestBaseCol.g, NewQuestBaseCol.b, alpha);
            UIHelper.LabelFit(new Rect(panelX + 12f, nqY, panelW - 24f, nqHeadH),
                "다음 단계 \u2192 \"" + (newQuestTitle ?? "") + "\"", newQuestStyleCache);
            nqY += nqHeadH + 6f;

            if (hasDesc)
            {
                // 무엇을 "해야 하는지"(설명) — 길이가 데이터에서 오므로 상자에 맞춰 줄인다.
                newQuestDescStyleCache.normal.textColor = new Color(NewQuestDescCol.r, NewQuestDescCol.g, NewQuestDescCol.b, alpha);
                UIHelper.LabelFit(new Rect(panelX + 12f, nqY, panelW - 24f, nqDescH),
                    newQuestDesc, newQuestDescStyleCache);
                nqY += nqDescH + 6f;

                // 진행 독려
                newQuestPromptStyleCache.normal.textColor = new Color(NewQuestPromptCol.r, NewQuestPromptCol.g, NewQuestPromptCol.b, alpha);
                GUI.Label(new Rect(panelX, nqY, panelW, nqPromptH),
                    "\u25b6 지금 진행해보세요!", newQuestPromptStyleCache);
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
            // [따라가기] 버튼이 칩과 같은 표면 스타일(panelSurfaceBtnStyleCache)을 쓴다 — 칩이 한 번도
            // 안 그려진 채(가이드 숨김 등) 목록부터 열면 null이라 여기서도 보장한다(가드가 있어 1회뿐).
            InitQuestPanelStyles();

            float panelW = UIScale.IsMobileLayout
                ? Mathf.Min(860f, UIScale.VirtualScreenWidth - UIScale.VirtualSafeLeft - UIScale.VirtualSafeRight - 32f)
                : 820f;
            float panelH = UISafeLayout.ClampHeight(UIScale.IsMobileLayout ? 900f : 680f);
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
            GUI.Label(new Rect(panelX + 18f, panelY + 12f, panelW - detailCloseSize - 40f,
                    RowH(detailHeaderStyleCache)),
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
            // [따라가기] 여부는 **이번 패스에 한 번만** 정한다 — 행 높이(DrawQuestRow)와 콘텐츠 높이가
            // 같은 답을 봐야 스크롤 끝이 안 잘린다. 판정이 스토리 진행을 훑어서 매 행마다 부를 값도 아니다.
            // Layout 패스에서만 정한다 — 판정이 이야기 비트 전체를 훑어서, Repaint·입력 패스마다 되풀이하면
            // 행을 펼쳐 둔 동안 프레임마다 두세 번 돈다. 같은 프레임의 뒤 패스는 이 값을 그대로 쓴다.
            if (Event.current.type == EventType.Layout)
                expandedRowTrackable = expandedCount > 0 && objectiveTracker != null
                    && (objectiveTracker.IsQuestTracked(expandedQuestId) || objectiveTracker.CanTrackQuestNow(expandedQuestId));
            float contentH = QuestListLayout.GetContentHeight(story.Count, side.Count, expandedCount,
                expandedRowTrackable ? 1 : 0);
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
                // \ub9c8\uc744 \uc758\ub8b0(1\ud68c)\uac00 \ud568\uaed8 \ub4e4\uc5b4\uc624\uba74\uc11c "\ubc18\ubcf5 \uc2dc \ubaa9\ud45c \uc0c1\uc2b9"\ub9cc\uc73c\ub85c\ub294 \uc124\uba85\uc774 \ubaa8\uc790\ub77c\ub2e4.
                DrawQuestSectionHeader(viewRect.width, ref ry, headH, "\u25c6 \uc11c\ube0c \u00b7 \uc678\uc804 \u00b7 \ub9c8\uc744 \uc758\ub8b0");
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
            GUI.Label(new Rect(10f, ry + 3f, width - 20f, headH - 4f), label, detailSectionStyleCache);
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
                // 선행 퀘스트 + (지역 의뢰면) 리전 해금 — 진행을 세는 쪽과 같은 판정을 쓴다.
                // prereq만 보면 잠긴 리전의 의뢰가 0/5 진행 중으로 떠 갈 수 없는 곳을 할 일처럼 보인다.
                // 완료를 먼저 본다 — 끝낸 의뢰는 나중에 리전 접근이 꺼져도(마스터 특권 해제 등) 완료다.
                tgt = questManager.EffectiveTarget(quest);
                if (!quest.repeatable && questManager.IsQuestCompleted(quest.questId))
                {
                    icon = "\u2713 "; titleCol = RowCompletedCol;
                    statusText = "\uc644\ub8cc"; statusCol = StatusCompletedCol;
                    cur = tgt;
                }
                else if (!questManager.IsSideUnlocked(quest))
                {
                    icon = "\ud83d\udd12 "; titleCol = RowLockedCol;
                    statusText = "\ubbf8\ud574\uae08"; statusCol = RowLockedCol;
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
            bool trackable = expanded && expandedRowTrackable;
            float totalH = QuestListLayout.GetRowHeight(expanded, trackable);
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
            // 마을 의뢰 제목("◆ [잿불 골짜기] 타지 않은 기억")은 28pt에서 이 상자를 넘는다 — 줄여 맞춘다.
            UIHelper.LabelFit(new Rect(10f, ry, width * 0.46f, rowH), icon + QuestTitle(quest), detailRowStyleCache);

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
                    UIHelper.LabelFit(new Rect(16f, ey, width - 32f, RowH(detailDescStyleCache) * 2f),
                        desc, detailDescStyleCache);
                }
                ey += RowH(detailDescStyleCache) * 2f + 6f;

                if (tgt > 0)
                {
                    float progLabelH = RowH(detailRewardLabelStyleCache);
                    GUI.Label(new Rect(16f, ey, 60f, progLabelH), "\uc9c4\ud589", detailRewardLabelStyleCache);
                    UIHelper.DrawProgressBar(
                        new Rect(80f, ey + 9f, width - 176f, 8f),
                        cur / (float)tgt,
                        t.surfaceBase,
                        t.accentMint);
                    detailStatusStyleCache.normal.textColor = statusCol;
                    GUI.Label(new Rect(width - 88f, ey, 72f, RowH(detailStatusStyleCache)),
                        cur + " / " + tgt, detailStatusStyleCache);
                }
                ey += RowH(detailRewardLabelStyleCache) + 8f;

                float rewardRowH = RowH(detailRewardStyleCache);
                GUI.Label(new Rect(16f, ey, 60f, rewardRowH), "\ubcf4\uc0c1", detailRewardLabelStyleCache);
                GUI.Label(new Rect(80f, ey, width - 96f, rewardRowH),
                    string.IsNullOrEmpty(rewardText) ? "\uc5c6\uc74c" : rewardText, detailRewardStyleCache);

                // \ub9c8\uc744 \uc758\ub8b0 \u2014 [\ub530\ub77c\uac00\uae30]. \ub204\ub974\uba74 \ubaa9\ud45c \ud589\u00b7\ubbf8\ub2c8\ub9f5\u00b7\uc9c0\ub3c4\uac00 \uadf8 \uc8fc\ubbfc\uc758 \uc774\uc57c\uae30\ub97c \uac00\ub9ac\ud0a8\ub2e4.
                // \ubc84\ud2bc \uc790\ub9ac\ub294 \ud589 \ub192\uc774 \uacc4\uc0b0(QuestListLayout.TrackButtonExtra)\uacfc \uac19\uc740 \ud310\uc815\uc73c\ub85c \ud655\ubcf4\ub3fc \uc788\ub2e4.
                if (trackable)
                {
                    Rect btn = new Rect(16f,
                        ry + QuestListLayout.RowHeight + QuestListLayout.ExpandedExtra + QuestListLayout.TrackButtonTopGap,
                        Mathf.Min(360f, width - 32f), QuestListLayout.TrackButtonHeight);
                    bool tracked = objectiveTracker.IsQuestTracked(quest.questId);
                    Color btnBg = tracked ? t.accentCoral : t.accentMint;
                    if (UISurface.Button(btn, tracked ? "\ub530\ub77c\uac00\uae30 \ud574\uc81c" : "\u25b6 \ub530\ub77c\uac00\uae30", btnBg, panelSurfaceBtnStyleCache)
                        && !detailDirectScroll.IsDragging)
                    {
                        if (tracked) objectiveTracker.StopTrackingTale();
                        else
                        {
                            objectiveTracker.TrackQuest(quest.questId);
                            // \ubaa9\ub85d\uc744 \ub2eb\uc544 \ubc14\ub85c \ud544\ub4dc\uc640 \ubaa9\ud45c \ud589\uc744 \ubcf4\uac8c \ud55c\ub2e4 \u2014 \ub530\ub77c\uac00\uae30\ub97c \ucf1c \ub193\uace0 \ubaa9\ub85d\uc5d0 \uba38\ubb3c \uc774\uc720\uac00 \uc5c6\ub2e4.
                            SetDetailOpen(false);
                        }
                    }
                }
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
            SubscribeObjectiveEvents();
        }

        /// <summary>
        /// 목표가 타 리전일 때 열어 줄 지도. 미주입이면 트래커가 안내 문구로 폴백하므로
        /// 기능이 사라지지 않고 한 단계 덜 편해질 뿐이다.
        /// </summary>
        public void AutoWire(RegionMapUI map)
        {
            if (regionMapUi == null) regionMapUi = map;
            SubscribeObjectiveEvents();   // 트래커가 먼저 들어왔다면 이 시점에 구독이 성립한다
        }

        // 구독을 메서드로 뺀 것은 OnEnable에서 되살리기 위해서다 — OpeningReplayCoordinator가
        // UI 루트를 통째로 껐다 켜는 경로에서 AutoWire는 다시 불리지 않는다(rules/ui-layout.md).
        // `-=` 뒤 `+=`라 중복 구독이 되지 않는다.
        private void SubscribeObjectiveEvents()
        {
            // **지도를 못 받았으면 구독하지 않는다.** 구독자가 하나라도 있으면 트래커는
            // 안내 문구 폴백을 건너뛰는데, 여기서 regionMapUi가 null이면 핸들러가 아무것도
            // 하지 않아 목표 행을 눌러도 **무반응**이 된다(문구조차 안 뜬다).
            // 구독을 지도 유무에 걸면 그 경우 구독자가 0이라 트래커가 문구로 폴백한다.
            if (objectiveTracker == null || regionMapUi == null) return;
            objectiveTracker.MapRequested -= OnObjectiveMapRequested;
            objectiveTracker.MapRequested += OnObjectiveMapRequested;
        }

        // 목표가 다른 리전이라 걸어갈 수 없다 — 지도를 그 리전이 선택된 채로 연다.
        // 실제 이동은 지도의 "이동" 버튼이 한다(해금·수문장 판정이 거기 있다).
        private void OnObjectiveMapRequested(string regionId)
        {
            if (regionMapUi != null) regionMapUi.OpenAt(regionId);
        }

        /// <summary>보상 아이템의 표시명 조회용. 미주입이면 목록·배너에 아이템 ID가 그대로 나온다.</summary>
        public void AutoWire(ItemDatabase database)
        {
            if (itemDatabase == null) itemDatabase = database;
            // AutoWire가 첫 렌더보다 늦게 올 수 있다. 그 사이 ID 원문으로 굳은 캐시를 버린다.
            rewardTextCache.Clear();
        }
    }

    /// <summary>퀘스트 칩 스택(칩·목표 행)이 서는 곳.</summary>
    public enum QuestStackPlace
    {
        /// <summary>모바일 세로 — 미니맵 아래에서 아래로.</summary>
        UnderMinimap,
        /// <summary>모바일 가로 — 미니맵 오른쪽, 위쪽 가운데 코치 배너 띠 아래.</summary>
        BesideMinimap,
        /// <summary>데스크톱 — 좌하단, 단축 바 왼쪽 끝까지, 바닥에서 위로.</summary>
        BottomLeft
    }

    /// <summary>
    /// 퀘스트 칩·숨김 복원 버튼·목표 행의 자리 — <b>순수 계산</b>. <see cref="TutorialQuestUI"/>가 이걸로 그리고
    /// <c>FieldHudInput</c>에 같은 Rect를 등록한다. 겹침 테스트(<c>WorldClockHudTests.QuestChip_*</c>)가 화면 크기별로 읽는다.
    ///
    /// <b>모바일 세로</b>: 미니맵 아래 좌측 스택(<c>MinimapUI.StackBelowY</c>)에서 아래로 — 칩, 그 아래 목표 행. 폭 최대 500.
    /// <b>모바일 가로</b>: 미니맵 <b>오른쪽</b>(x = 미니맵 오른쪽 끝 + 10), 위쪽 가운데 코치 배너 띠(<c>ContentTop + 150~250</c>,
    /// <see cref="GuidedTutorialController.CoachRect"/>) 바로 아래에서 아래로. 가로 화면은 미니맵 아래에 128px(412~540)밖에 안 남아
    /// 칩(108) + 목표 행(57)이 화면 가운데 줄 아래 — 가상 조이스틱의 시작 영역(좌하단 사분면) — 으로 43px 내려갔다. 미니맵 오른쪽에 두면
    /// 칩 아랫변이 미니맵 아랫변과 거의 맞고(400 vs 402) 목표 행이 y 463에서 끝난다. 미니맵 오른쪽 위 띠를 쓰지 않는 이유는 첫 가이드의
    /// 코치 배너(가운데 폭 720)가 거기 서기 때문이다 — 1920 폭에서 x 600부터라 폭 500 칩(246~746)과 겹친다.
    /// <b>데스크톱</b>: 좌하단, <b>단축 바 왼쪽 끝까지만</b> 쓴다(최대 400). 16:9(가상 폭 1920)에서 단축 바(폭 1200, 가운데)가
    /// x 360에서 시작해 폭 400 칩(x 16~416)과 56px 겹쳤다 — 칩은 334로 줄고, 21:9(2560)처럼 넓으면 400 그대로다.
    /// 그리고 <b>바닥에서 위로 쌓는다</b>: 목표 행이 뜰 거면 그 높이만큼 칩을 올린다. 예전엔 칩이 바닥에 붙고 목표 행이 그 <i>아래</i>
    /// (안전 영역 밖, 1080 화면에서 y 1054~1111)에 그려져 거의 보이지 않았다.
    /// 단축 바 옆에 280도 안 남는 화면(데스크톱 레이아웃의 세로에 가까운 창)에서는 단축 바 <i>위</i>에 쌓는다.
    /// </summary>
    public static class QuestChipLayout
    {
        public const float MobileMaxWidth = 500f;
        public const float DesktopMaxWidth = 400f;
        /// <summary>데스크톱에서 단축 바 옆에 칩을 둘 최소 폭 — 이보다 좁으면 단축 바 위로 올린다.</summary>
        public const float DesktopMinBesideWidth = 280f;
        public const float RestoreWidthMobile = 230f;
        public const float RestoreHeightMobile = 56f;
        public const float RestoreWidthDesktop = 190f;
        public const float RestoreHeightDesktop = 40f;
        /// <summary>칩 제목·진행·목표 행 글자 크기 — 칩과 목표 행의 높이가 여기서 나온다(한글 줄높이 ≈ 글자 × 1.35).</summary>
        public const int TitleFontSize = 34;
        public const int ProgressFontSize = 26;
        public const int ObjectiveFontSize = 27;

        private static float LineHeight(int fontSize) => Mathf.Ceil(fontSize * 1.35f);

        /// <summary>진행 중인 퀘스트 칩 높이 — 여백 + 제목 + 간격 + 진행 줄 + 여백(108).</summary>
        public static float ExpandedHeight => UITheme.Space.S + LineHeight(TitleFontSize) + UITheme.Space.XS
                                              + LineHeight(ProgressFontSize) + UITheme.Space.S;

        /// <summary>모두 끝난 칩 높이(진행 줄 없음, 66).</summary>
        public static float DoneHeight => UITheme.Space.S + LineHeight(TitleFontSize) + UITheme.Space.S;

        /// <summary>목표 행 높이(57).</summary>
        public static float RowHeight => LineHeight(ObjectiveFontSize) + UITheme.Space.S * 2f;

        /// <summary>
        /// 가로 모바일 스택의 윗변 — 안전 영역 위에서 위쪽 가운데 줄(리전 배너·내기 점수판, 데스크톱 코치 배너 띠 높이) 아래까지(260).
        /// 모바일 코치 배너는 이제 화면 가운데 줄에 서지만, 이 높이가 칩을 미니맵 아랫변과 맞추고 목표 행을 조이스틱 자리 위에서 끝낸다.
        /// </summary>
        public const float BesideMinimapTop =
            GuidedTutorialController.CoachTopOffset + GuidedTutorialController.CoachHeight + UITheme.Space.S;

        public static QuestStackPlace PlaceFor(bool mobileLayout, bool portrait)
        {
            if (!mobileLayout) return QuestStackPlace.BottomLeft;
            return portrait ? QuestStackPlace.UnderMinimap : QuestStackPlace.BesideMinimap;
        }

        /// <summary>스택의 왼쪽 끝. <paramref name="minimapLeft"/>는 <c>MinimapUI.LeftX</c>.</summary>
        public static float Left(QuestStackPlace place, float minimapLeft)
        {
            return place == QuestStackPlace.BesideMinimap ? minimapLeft + MinimapUI.PanelSize + UITheme.Space.S : minimapLeft;
        }

        /// <summary>
        /// 칩·목표 행의 폭. <paramref name="minimapLeft"/>는 <c>MinimapUI.LeftX</c>,
        /// <paramref name="quickBar"/>는 단축 바(<c>QuickAccessBarUI.ShortcutBarRect</c>).
        /// </summary>
        public static float Width(QuestStackPlace place, float screenWidth, float safeLeft, float safeRight, float minimapLeft,
            Rect quickBar)
        {
            float room = screenWidth - safeLeft - safeRight - 40f;
            if (place == QuestStackPlace.UnderMinimap) return Mathf.Min(MobileMaxWidth, room);
            if (place == QuestStackPlace.BesideMinimap)
                return Mathf.Min(MobileMaxWidth, Mathf.Max(1f, room - MinimapUI.PanelSize - UITheme.Space.S));
            float beside = BesideBar(minimapLeft, quickBar);
            return Mathf.Min(DesktopMaxWidth, beside >= DesktopMinBesideWidth ? beside : room);
        }

        /// <summary>모바일 스택의 윗변(데스크톱은 쓰지 않는다 — <see cref="DesktopBottom"/>에서 위로 쌓는다).</summary>
        public static float MobileTop(QuestStackPlace place, float contentTop)
        {
            return place == QuestStackPlace.BesideMinimap
                ? contentTop + BesideMinimapTop
                : contentTop + MinimapUI.TopOffset + MinimapUI.PanelSize + UITheme.Space.S;   // = MinimapUI.StackBelowY
        }

        /// <summary>데스크톱 스택(칩 + 목표 행)의 바닥 — 단축 바 옆에 서면 안전 영역 바닥, 아니면 단축 바 위.</summary>
        public static float DesktopBottom(float minimapLeft, Rect quickBar, float contentBottom)
        {
            return BesideBar(minimapLeft, quickBar) >= DesktopMinBesideWidth ? contentBottom : quickBar.y - UITheme.Space.S;
        }

        /// <summary>
        /// 칩(또는 숨김 복원 버튼). 모바일은 <paramref name="mobileTop"/>에서 아래로. <paramref name="rowHeight"/>가 0보다 크면
        /// 그 아래 목표 행 자리를 비워 둔다 — 데스크톱은 바닥(<paramref name="desktopBottom"/>)에서 위로 쌓으므로 칩이 그만큼 올라간다.
        /// </summary>
        public static Rect Chip(QuestStackPlace place, float x, float width, float height, float rowHeight, float mobileTop,
            float desktopBottom)
        {
            if (place != QuestStackPlace.BottomLeft) return new Rect(x, mobileTop, width, height);
            float stack = height + (rowHeight > 0f ? UITheme.Space.XS + rowHeight : 0f);
            return new Rect(x, desktopBottom - stack, width, height);
        }

        /// <summary>목표 행 — 칩(또는 복원 버튼) 바로 아래, 칩 폭(<see cref="Width"/>).</summary>
        public static Rect Row(Rect chip, float width, float rowHeight)
        {
            return new Rect(chip.x, chip.yMax + UITheme.Space.XS, width, rowHeight);
        }

        private static float BesideBar(float minimapLeft, Rect quickBar)
        {
            return quickBar.x - UITheme.Space.S - minimapLeft;
        }

        // ── 화면 한 장(HudFrame)에서 ──

        /// <summary>칩·목표 행 폭(화면 한 장 기준).</summary>
        public static float StackWidth(HudFrame f)
        {
            QuestStackPlace place = PlaceFor(f.Mobile, f.Portrait);
            return Width(place, f.Width, f.SafeLeft, f.SafeRight, f.SafeLeft + 16f, QuickAccessBarUI.ShortcutBarRectFor(f));
        }

        /// <summary>칩(높이 <paramref name="height"/>)의 자리 — <paramref name="rowHeight"/> &gt; 0이면 그 아래 목표 행 자리를 비운다.</summary>
        public static Rect ChipRect(HudFrame f, float height, float rowHeight)
        {
            return StackRect(f, StackWidth(f), height, rowHeight);
        }

        /// <summary>숨김 상태의 복원 버튼 자리.</summary>
        public static Rect RestoreRect(HudFrame f, float rowHeight)
        {
            float w = Mathf.Min(f.Mobile ? RestoreWidthMobile : RestoreWidthDesktop, StackWidth(f));
            return StackRect(f, w, f.Mobile ? RestoreHeightMobile : RestoreHeightDesktop, rowHeight);
        }

        private static Rect StackRect(HudFrame f, float width, float height, float rowHeight)
        {
            QuestStackPlace place = PlaceFor(f.Mobile, f.Portrait);
            float minimapLeft = f.SafeLeft + 16f;   // = MinimapUI.LeftX
            Rect bar = QuickAccessBarUI.ShortcutBarRectFor(f);
            return Chip(place, Left(place, minimapLeft), width, height, rowHeight, MobileTop(place, f.ContentTop),
                DesktopBottom(minimapLeft, bar, f.ContentBottom));
        }
    }
}
