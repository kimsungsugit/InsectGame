using InsectGame.Core;
using InsectGame.NPC;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 주민 대화 모달 — 하단 대화 패널(이름 + 대사, [다음]/[닫기]).
    /// Show: Register + SetFrozen(true) + npc.BeginTalk / CloseModal: 역순 해제 (CaptureChoiceUI 관례).
    /// GUI.Button은 터치 합성 클릭으로 동작 — 대화 중 SetFrozen이라 조이스틱 점유 문제 없음.
    /// </summary>
    public class NpcDialogueUI : MonoBehaviour, IModalUI
    {
        private PlayerMovement playerMovement;

        private VillagerNpc currentNpc;
        private string[] lines;
        private int lineIndex;
        private bool isOpen;
        private int openedFrame; // 여는 터치가 같은 프레임의 버튼을 누르는 것 방지용

        // GUIStyle 1회 캐시 (OnGUI 매 프레임 new 금지)
        private GUIStyle nameStyle;
        private GUIStyle lineStyle;
        private GUIStyle buttonStyle;
        private Texture2D panelTex;
        private bool stylesInited;

        // 스토리 비트 렌더 — StoryDirector.StoryBeatTriggered 구독. 기존 대화 모달 렌더 재사용.
        private InsectGame.Story.StoryDirector storyDirector;
        private InsectGame.Story.StoryBeat currentBeat;
        private InsectGame.Story.StoryLine[] storyLines;
        private bool storyMode;

        public bool IsOpen => isOpen;

        public void AutoWire(PlayerMovement player)
        {
            if (playerMovement == null) playerMovement = player;
        }

        // 스토리 지휘자 주입 — 비트 발화 구독. Bootstrap이 호출.
        public void AutoWire(InsectGame.Story.StoryDirector director)
        {
            if (storyDirector == null && director != null)
            {
                storyDirector = director;
                storyDirector.StoryBeatTriggered += OnStoryBeatTriggered;
            }
        }

        private void OnDestroy()
        {
            if (storyDirector != null)
                storyDirector.StoryBeatTriggered -= OnStoryBeatTriggered;
        }

        private void OnStoryBeatTriggered(InsectGame.Story.StoryBeat beat)
        {
            ShowStory(beat);
        }

        // 스토리 비트를 대화 모달로 렌더 — lines[]를 순차 표시(speaker는 라인별). 닫으면 CompleteBeat 콜백.
        public void ShowStory(InsectGame.Story.StoryBeat beat)
        {
            if (beat == null) return;

            // 대사 없음 — 표시 없이 즉시 완료(보상/seen 처리).
            if (beat.lines == null || beat.lines.Count == 0)
            {
                if (storyDirector != null) storyDirector.CompleteBeat(beat.beatId);
                return;
            }

            // 다른 모달(주민 대화)이 열려 있으면 정리 후 스토리로 전환.
            if (isOpen) CloseModal();

            currentBeat = beat;
            storyMode = true;
            storyLines = beat.lines.ToArray();
            lines = new string[storyLines.Length];
            for (int i = 0; i < storyLines.Length; i++)
                lines[i] = storyLines[i] != null ? storyLines[i].text : "";
            lineIndex = 0;
            isOpen = true;
            openedFrame = Time.frameCount;
            ModalUIRegistry.Register(this);
            if (playerMovement != null) playerMovement.SetFrozen(true);
        }

        /// <summary>대화 시작 — WorldInteractionController가 호출.</summary>
        public void Show(VillagerNpc npc)
        {
            if (npc == null) return;
            currentNpc = npc;
            lines = NpcDialogueDatabase.GetLines(npc.NpcId, npc.RegionId);
            lineIndex = 0;
            isOpen = true;
            openedFrame = Time.frameCount;
            ModalUIRegistry.Register(this);
            if (playerMovement != null)
            {
                playerMovement.SetFrozen(true);
                npc.BeginTalk(playerMovement.transform);
            }
            else
            {
                npc.BeginTalk(null);
            }
        }

        public void CloseModal()
        {
            if (!isOpen) return;
            isOpen = false;
            ModalUIRegistry.Unregister(this);
            if (playerMovement != null) playerMovement.SetFrozen(false);
            if (currentNpc != null) currentNpc.EndTalk();
            currentNpc = null;
            lines = null;

            // 스토리 비트였으면 완료 콜백(보상/seen). 상태를 먼저 비워 CompleteBeat 재진입에 안전.
            if (storyMode)
            {
                InsectGame.Story.StoryBeat done = currentBeat;
                storyMode = false;
                currentBeat = null;
                storyLines = null;
                if (storyDirector != null && done != null)
                    storyDirector.CompleteBeat(done.beatId);
            }
        }

        private void OnDisable()
        {
            // Unregister만 하면 isOpen이 true로 남아 다시 켰을 때 "열린 것으로 아는데
            // 레지스트리엔 없는" 상태가 된다. 그러면 (a) HandleEscape가 이 모달을 무시해
            // ESC가 frozen만 풀고 Update가 즉시 재프리즈 → ESC 영구 무력화, (b)
            // IsAnyOpen()이 false라 WorldInteractionController의 재진입 가드가 뚫려
            // 이전 currentNpc를 EndTalk 없이 덮어써 그 주민이 Talking에 갇힌다
            // (VillagerNpc.CanTalk가 영구 false → 다시는 대화 불가).
            //
            // 옛 주석은 CaptureChoiceUI 관례를 인용했으나 그건 이 프로젝트가 이미 P1으로
            // 두 번 폐기한 방식이다(CharacterOutfitUI/RegionMapUI 라운드). 현재 표준은
            // 상태까지 되돌리는 것 — CloseModal이 그 일을 전부 한다.
            CloseModal();
        }

        private void Update()
        {
            if (!isOpen) return;
            // 대화 상대가 사라짐(ApplyTuning 비활성화 등) — 안전 종료. 스토리 모드는 NPC가 없으므로 스킵.
            if (!storyMode && (currentNpc == null || !currentNpc.gameObject.activeInHierarchy))
            {
                CloseModal();
                return;
            }

            // PlayerMovement의 AutoUnfreeze(20s)가 대화를 길게 읽는 동안 프리즈를 풀면
            // 모달이 열린 채 이동 가능해진다 — 열려 있는 동안 프리즈를 재적용(타이머 리셋).
            if (playerMovement != null && !playerMovement.IsFrozen)
                playerMovement.SetFrozen(true);
        }

        private void OnGUI()
        {
            if (!isOpen || lines == null || lines.Length == 0) return;

            // 대화를 연 바로 그 터치(합성 마우스)가 같은 자리의 [다음]/[닫기]를 즉시 누르는
            // 것 방지 — 세로 고해상 기기에서 상호작용 원버튼과 대화 버튼의 y밴드가 겹친다.
            Event evt = Event.current;
            if (evt != null && Time.frameCount <= openedFrame + 1
                && (evt.type == EventType.MouseDown || evt.type == EventType.MouseUp))
            {
                evt.Use();
                return;
            }

            EnsureStyles();
            UIScale.Begin();

            float vw = UIScale.VirtualScreenWidth;
            float vh = UIScale.VirtualScreenHeight;
            float safeB = UIScale.VirtualSafeBottom;

            float panelW = Mathf.Min(920f, vw - 48f);
            float panelH = 210f;
            float px = (vw - panelW) / 2f;
            float py = vh - safeB - panelH - 28f;

            // 패널 배경
            GUI.color = new Color(0f, 0f, 0f, 0.82f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), panelTex);
            GUI.color = Color.white;

            // 이름 + 대사 — 스토리 모드는 라인별 speaker(없으면 비트 speakerNpcId), 아니면 NPC 이름.
            string npcName;
            if (storyMode)
            {
                InsectGame.Story.StoryLine sl = (storyLines != null
                    && lineIndex >= 0 && lineIndex < storyLines.Length) ? storyLines[lineIndex] : null;
                if (sl != null && !string.IsNullOrEmpty(sl.speaker)) npcName = sl.speaker;
                else if (currentBeat != null && !string.IsNullOrEmpty(currentBeat.speakerNpcId)) npcName = currentBeat.speakerNpcId;
                else npcName = "???";
            }
            else
            {
                npcName = currentNpc != null ? currentNpc.DisplayName : "주민";
            }
            // 스토리 모드 + 아는 화자면 좌측에 포트레이트, 텍스트는 그만큼 우측으로 민다.
            float textX = px + 28f;
            float textW = panelW - 56f;
            if (storyMode && currentBeat != null)
            {
                float off = DrawStoryPortrait(px, py, panelH, currentBeat.speakerNpcId);
                textX += off;
                textW -= off;
            }
            GUI.Label(new Rect(textX, py + 16f, textW, 34f), npcName, nameStyle);
            GUI.Label(new Rect(textX, py + 56f, textW, 88f),
                lines[Mathf.Clamp(lineIndex, 0, lines.Length - 1)], lineStyle);

            // 진행 표시 (n/총)
            GUI.Label(new Rect(px + panelW - 120f, py + 16f, 92f, 30f),
                $"{lineIndex + 1}/{lines.Length}", nameStyle);

            // 버튼 — 마지막 줄이면 [닫기]만, 아니면 [다음]/[닫기]
            float btnW = 170f;
            float btnH = 56f;
            float btnY = py + panelH - btnH - 14f;
            bool isLast = lineIndex >= lines.Length - 1;

            if (!isLast)
            {
                if (GUI.Button(new Rect(px + panelW - btnW * 2f - 40f, btnY, btnW, btnH), "다음", buttonStyle))
                    lineIndex++;
            }
            if (GUI.Button(new Rect(px + panelW - btnW - 24f, btnY, btnW, btnH), "닫기", buttonStyle))
                CloseModal();

            UIScale.End();
        }

        // 스토리 화자(어르신/라온/세라) 좌측 포트레이트 — CharacterPortraitRenderer 재사용.
        // 반환: 그린 포트레이트 폭 오프셋(0이면 아는 화자 아님 → 포트레이트 없음).
        private float DrawStoryPortrait(float px, float py, float panelH, string speakerNpcId)
        {
            if (!GetStoryPortrait(speakerNpcId, out int gender, out int skinIdx, out int hairIdx,
                    out int hairStyle, out int faceType, out Color top, out Color hat))
                return 0f;

            float box = panelH - 24f;
            float boxX = px + 14f;
            float boxY = py + 12f;

            GUI.color = new Color(0.12f, 0.1f, 0.06f, 0.9f);
            GUI.DrawTexture(new Rect(boxX, boxY, box, box), panelTex);
            GUI.color = new Color(1f, 0.85f, 0.45f, 0.5f);
            GUI.DrawTexture(new Rect(boxX, boxY, box, 3f), panelTex);
            GUI.color = Color.white;

            float scale = box / 150f;
            Color bottom = new Color(0.18f, 0.22f, 0.28f);
            Color shoe = new Color(0.2f, 0.12f, 0.06f);
            CharacterPortraitRenderer.Draw(boxX + box * 0.5f, boxY + box * 0.52f, scale,
                gender, skinIdx, hairIdx, hairStyle, faceType, top, bottom, shoe, hat, 0f, false);

            return box + 22f;
        }

        private static bool GetStoryPortrait(string id, out int gender, out int skinIdx, out int hairIdx,
            out int hairStyle, out int faceType, out Color top, out Color hat)
        {
            switch (id)
            {
                case "catcher_rival": // 라온 — 밝은 주황 상의, 캡, 미소
                    gender = 0; skinIdx = 2; hairIdx = 1; hairStyle = 0; faceType = 1;
                    top = new Color(1f, 0.55f, 0.3f); hat = new Color(1f, 0.65f, 0.2f); return true;
                case "ruins_scholar": // 세라 — 보라 상의, 올림머리, 모자 없음
                    gender = 1; skinIdx = 0; hairIdx = 4; hairStyle = 3; faceType = 0;
                    top = new Color(0.6f, 0.45f, 0.7f); hat = new Color(0f, 0f, 0f, 0f); return true;
                case "village_elder": // 마을 어르신 — 따뜻한 상의, 모자, 밝은 머리
                    gender = 0; skinIdx = 1; hairIdx = 2; hairStyle = 0; faceType = 0;
                    top = new Color(0.85f, 0.7f, 0.4f); hat = new Color(0.55f, 0.35f, 0.25f); return true;
                default:
                    gender = 0; skinIdx = 0; hairIdx = 0; hairStyle = 0; faceType = 0;
                    top = Color.white; hat = new Color(0f, 0f, 0f, 0f); return false;
            }
        }

        private void EnsureStyles()
        {
            if (stylesInited) return;

            nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            nameStyle.normal.textColor = new Color(1f, 0.85f, 0.45f);

            lineStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };
            lineStyle.normal.textColor = Color.white;

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            panelTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            panelTex.SetPixel(0, 0, Color.white);
            panelTex.Apply();

            stylesInited = true;
        }
    }
}
