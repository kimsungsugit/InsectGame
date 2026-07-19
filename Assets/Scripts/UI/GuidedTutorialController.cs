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
            { "q_team", "T 키로 배틀 팀을 편성하세요!" },
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

            UIScale.Begin();
            InitStyles();
            DrawCoach();
            UIScale.End();
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

        private void DrawCoach()
        {
            // 상단 중앙 코치 배너 — 펄스 강조. 가이드 중엔 숨길 수 없음(강제).
            float availW = UIScale.VirtualScreenWidth - UIScale.VirtualSafeLeft - UIScale.VirtualSafeRight;
            float w = Mathf.Min(720f, availW - 24f);
            float h = 100f;
            float x = UIScale.VirtualSafeLeft + (availW - w) * 0.5f;
            float y = UIScale.VirtualSafeTop + 200f;   // 상단 리전 배너·알림 아래

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            GUI.color = new Color(0.1f, 0.08f, 0.02f, 0.9f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.85f, 0.3f, 0.55f + 0.4f * pulse);
            GUI.DrawTexture(new Rect(x, y, w, 4f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + h - 4f, w, 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 16f, y + 14f, w - 32f, 46f), activeGuidedText, coachStyle);
            GUI.Label(new Rect(x + 16f, y + 62f, w - 32f, 28f), "안내대로 진행하면 다음 단계로 넘어갑니다", hintStyle);
        }
    }
}
