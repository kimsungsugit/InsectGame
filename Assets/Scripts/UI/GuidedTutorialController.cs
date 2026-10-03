using System.Collections.Generic;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    // 첫 몇 단계 '강제 가이드' 오버레이 — 지정된 튜토리얼 퀘스트가 활성화되면 코치 배너(지시)를 띄우고,
    // 그 퀘스트를 완료하기 전까지 튜토리얼 숨김을 억제한다(TutorialQuestUI가 IsGuiding 조회).
    // 시작 지시 순간엔 잠깐 이동 잠금 후 자동 해제. 새 퀘스트/게이팅 없음 — 기존 튜토리얼 체인 위
    // 얹는 안내/차단 레이어. 싱글턴 아님(AutoWire). UI 컴포넌트라 Core만 참조(UI→Core).
    public class GuidedTutorialController : MonoBehaviour
    {
        private TutorialQuestManager questManager;
        private PlayerMovement playerMovement;

        // 강제 가이드 대상 questId → 코치 지시문. 이 퀘스트들만 강제 유도(나머지는 기존 비블로킹 안내).
        private static readonly Dictionary<string, string> GuidedSteps = new Dictionary<string, string>
        {
            // q_approach("곤충에게 다가가 E 키로 포획해보세요!")는 사용자 요청으로 제거 — 그 강제 배너/프리즈 미표시.
            // 퀘스트 진행 자체는 TutorialQuestManager가 처리하므로 안내만 빠지고 흐름은 유지된다.
            { "q_battle", "야생 곤충에게 B 키로 배틀을 걸어보세요!" },
            // q_team(팀 편성)은 「둘러보기」 서브 과제로 옮겨졌다 — 서브는 활성화 이벤트가 없어 강제 가이드에
            // 걸리지도 않고, 걸려서도 안 된다(안 해도 되는 일로 화면을 막지 않는다).
        };

        private string activeGuidedQuestId;   // 현재 가이드 중인 questId (없으면 null)
        private string activeGuidedText;
        private float freezeTimer;            // 시작 지시 프리즈 남은 시간
        private bool weFroze;                 // 우리가 프리즈를 걸었는가 — 남의 프리즈 오해제 방지
        private bool subscribed;

        public bool IsGuiding => !string.IsNullOrEmpty(activeGuidedQuestId);

        private GUIStyle coachStyle;
        private GUIStyle hintStyle;
        private bool stylesInit;

        public void AutoWire(TutorialQuestManager quest, PlayerMovement movement)
        {
            if (questManager == null) questManager = quest;
            if (playerMovement == null) playerMovement = movement;
            Subscribe();
        }

        private void Start()
        {
            if (playerMovement == null) playerMovement = FindFirstObjectByType<PlayerMovement>();
            Subscribe();
            // 로드 직후 이미 활성 퀘스트가 가이드 대상이면(복귀 유저) 즉시 진입.
            if (questManager != null && questManager.ActiveQuest != null)
                TryEnterGuided(questManager.ActiveQuest);
        }

        private void Subscribe()
        {
            if (subscribed || questManager == null) return;
            subscribed = true;
            questManager.QuestActivated += OnQuestActivated;
            questManager.QuestCompleted += OnQuestCompleted;
        }

        private void OnDestroy()
        {
            if (questManager != null)
            {
                questManager.QuestActivated -= OnQuestActivated;
                questManager.QuestCompleted -= OnQuestCompleted;
            }
        }

        private void OnQuestActivated(TutorialQuest quest) { TryEnterGuided(quest); }

        private void OnQuestCompleted(TutorialQuest quest)
        {
            if (quest != null && quest.questId == activeGuidedQuestId) ExitGuided();
        }

        private void TryEnterGuided(TutorialQuest quest)
        {
            if (quest == null) return;
            if (!GuidedSteps.TryGetValue(quest.questId, out string text)) return;
            activeGuidedQuestId = quest.questId;
            activeGuidedText = text;
            // 시작 지시 순간 잠깐 이동 잠금(플레이어가 지시를 읽도록) — 모달 없을 때만, 짧게 후 자동 해제.
            if (playerMovement != null && !playerMovement.IsFrozen && !ModalUIRegistry.IsAnyOpen())
            {
                playerMovement.SetFrozen(true);
                weFroze = true;
                freezeTimer = 1.3f;
            }
        }

        private void ExitGuided()
        {
            activeGuidedQuestId = null;
            activeGuidedText = null;
            ReleaseOurFreeze();
        }

        // 우리가 건 시작 프리즈만 해제한다. 이미 만료됐거나(weFroze=false) 배틀 결과화면·모달이
        // 프리즈를 인계한 상태면 건드리지 않는다 — SetFrozen은 refcount 없는 단순 bool이라 남의
        // 프리즈를 풀면 배틀 결과 4초+·대사 도중 플레이어가 이동해 버린다(첫 배틀에서 실제 발생).
        private void ReleaseOurFreeze()
        {
            freezeTimer = 0f;
            if (!weFroze) return;
            weFroze = false;
            if (playerMovement != null && playerMovement.IsFrozen && !ModalUIRegistry.IsAnyOpen())
                playerMovement.SetFrozen(false);
        }

        private void Update()
        {
            if (freezeTimer > 0f)
            {
                freezeTimer -= Time.deltaTime;
                if (freezeTimer <= 0f) ReleaseOurFreeze();   // 시작 지시 프리즈 자동 해제(우리 것만)
            }
        }

        private void OnGUI()
        {
            if (!IsGuiding) return;
            if (ModalUIRegistry.IsAnyOpen()) return;   // 다른 모달 위로 안 겹치게
            if (DreamPrologueState.Active) return;     // 꿈 밖의 안내가 꿈속에 비치지 않게(다시 보기는 가이드 중에도 열린다)
            if (YieldsNow(HudFrame.Current)) return;

            UIScale.Begin();
            InitStyles();
            DrawCoach();
            UIScale.End();
        }

        /// <summary>
        /// 코치 배너가 지금 비켜서는가 — 가운데 무대에 선 카드와 겹치거나(<see cref="HudStage.OccupiedOver"/>), 섬 안내 배너가 같은 자리에
        /// 섰거나, 대화 버튼·동굴 입구 버튼이 이 배너와 겹치는 자리에 섰을 때. 배너는 행동 안내라 버튼·카드에 자리를 내준다
        /// (가이드는 퀘스트를 마칠 때까지 남아 있어 잠깐 비켜도 다시 뜬다).
        /// </summary>
        private static bool YieldsNow(HudFrame f)
        {
            Rect coach = CoachRect(f);
            if (HudStage.OccupiedOver(coach)) return true;
            if (HudPresence.IsShowing(HudPresenceItem.IslandGuide)) return true;
            if (HudPresence.IsShowing(HudPresenceItem.Talk) && coach.Overlaps(WorldInteractionController.TalkRect(f)))
                return true;
            if (HudPresence.IsShowing(HudPresenceItem.Nearby) && coach.Overlaps(WorldFieldMultiplayerUI.NearbyRect(f)))
                return true;
            return HudPresence.IsShowing(HudPresenceItem.Gate) && coach.Overlaps(SubAreaWorldBuilder.GateRect(f));
        }

        private void InitStyles()
        {
            if (stylesInit) return;
            stylesInit = true;
            coachStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            coachStyle.normal.textColor = new Color(1f, 0.95f, 0.6f);
            hintStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 21, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter };
            hintStyle.normal.textColor = new Color(0.8f, 0.9f, 1f);
        }

        /// <summary>코치 배너의 윗변 — 안전 영역 위에서 이만큼(상단 리전 배너·알림 아래).</summary>
        public const float CoachTopOffset = 150f;
        public const float CoachHeight = 100f;
        public const float CoachMaxWidth = 720f;

        /// <summary>
        /// 코치 배너의 자리 — <b>순수 계산</b>. <b>데스크톱</b>은 위쪽 가운데(리전 배너·내기 점수판 아래, ContentTop+150).
        /// <b>모바일</b>은 섬 안내 배너와 같은 가운데 줄(세로 59%·가로 64%, 캐릭터 발밑) — 위쪽은 세로 화면에서 미니맵·단축 바가
        /// 양옆을 차지해 폭 720 배너가 둘 다 덮었다. 가로 모바일의 퀘스트 칩(<see cref="QuestChipLayout"/>)은 데스크톱 띠 높이 아래에 선다.
        /// </summary>
        public static Rect CoachRect(HudFrame f)
        {
            if (!f.Mobile)
            {
                float availW = f.Width - f.SafeLeft - f.SafeRight;
                float w = Mathf.Min(CoachMaxWidth, availW - 24f);
                return new Rect(f.SafeLeft + (availW - w) * 0.5f, f.ContentTop + CoachTopOffset, w, CoachHeight);
            }
            float mw = Mathf.Min(CoachMaxWidth, f.ContentWidth);
            float y = Mathf.Clamp(f.Height * (f.Portrait ? 0.59f : 0.64f), f.ContentTop, f.ContentBottom - CoachHeight);
            return new Rect(f.ContentLeft + (f.ContentWidth - mw) * 0.5f, y, mw, CoachHeight);
        }

        private void DrawCoach()
        {
            // 코치 배너 — 펄스 강조. 가이드 중엔 숨길 수 없음(강제). 자리는 CoachRect(순수 계산).
            Rect coach = CoachRect(HudFrame.Current);
            float w = coach.width;
            float h = coach.height;
            float x = coach.x;
            float y = coach.y;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            GUI.color = new Color(0.1f, 0.08f, 0.02f, 0.9f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.85f, 0.3f, 0.55f + 0.4f * pulse);
            GUI.DrawTexture(new Rect(x, y, w, 4f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + h - 4f, w, 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 가이드 문구는 단계마다 길이가 달라 46px(≈한 줄)을 쉽게 넘긴다 — 폰트를 줄여 맞춘다.
            UIHelper.LabelFit(new Rect(x + 16f, y + 14f, w - 32f, 46f), activeGuidedText, coachStyle);
            GUI.Label(new Rect(x + 16f, y + 62f, w - 32f, 28f), "안내대로 진행하면 다음 단계로 넘어갑니다", hintStyle);
        }
    }
}
