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

        // 패널 페이드 상태(UIHelper.AnimatePanelOpen이 소유). CharacterOutfitUI와 같은 관례.
        private TweenHandle openFade;
        private bool wasOpen;

        // 스토리 비트 렌더 — StoryDirector.StoryBeatTriggered 구독. 기존 대화 모달 렌더 재사용.
        private InsectGame.Story.StoryDirector storyDirector;
        private InsectGame.Story.StoryBeat currentBeat;
        private InsectGame.Story.StoryLine[] storyLines;
        private bool storyMode;
        // 다시보기 — 저널에서 이미 열람한 비트를 다시 읽는 중. storyMode는 켠 채로 두어
        // 화자명·초상 렌더를 그대로 쓰되, 닫을 때 CompleteBeat만 건너뛴다.
        // **이 플래그가 없으면 다시 읽을 때마다 onComplete 보상이 재지급된다.**
        private bool storyReplay;

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

        // 대사 앞 연출 게이트(옵션). 배선되지 않으면 지금까지처럼 곧바로 대사를 띄운다.
        private InsectGame.Story.IStoryStagePrelude stagePrelude;

        /// <summary>
        /// NPC 등장 연출 주입 — 비트에 <c>stageEnterId</c>가 있으면 <b>대사보다 먼저</b> 돌린다.
        /// "라온이 뛰어 들어오고 나서 말한다"의 순서가 여기서 갈린다.
        /// </summary>
        public void AutoWire(InsectGame.Story.IStoryStagePrelude prelude)
        {
            if (stagePrelude == null) stagePrelude = prelude;
        }

        private void OnStoryBeatTriggered(InsectGame.Story.StoryBeat beat)
        {
            // 연출이 있으면 그것이 끝나며 ShowStory를 부른다. 연출 쪽은 어떤 경로로 끝나든
            // (도착·건너뛰기·타임아웃) 콜백을 반드시 부르기로 계약돼 있다 — 안 그러면 이 비트가
            // pendingBeatId에 갇혀 캠페인이 멈춘다.
            if (stagePrelude != null && stagePrelude.TryPlayPrelude(beat, () => ShowStory(beat))) return;
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
            storyReplay = false;
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

        /// <summary>
        /// 이미 열람한 비트를 저널에서 다시 읽는다 — <see cref="ShowStory"/>와 렌더는 같고
        /// 닫을 때 <c>CompleteBeat</c>만 부르지 않는다(보상 재지급·seen 재기록 방지).
        /// <b>미열람 비트에는 절대 쓰지 말 것</b> — 대사를 보여주면서 seen 마킹은 안 되므로
        /// 나중에 정상 트리거로 한 번 더 뜬다. 호출부(StoryJournalUI)가 열람 여부를 걸러야 한다.
        /// </summary>
        public void ShowStoryReplay(InsectGame.Story.StoryBeat beat)
        {
            if (beat == null || beat.lines == null || beat.lines.Count == 0) return;
            ShowStory(beat);
            storyReplay = true;   // ShowStory가 false로 돌려놓은 뒤에 켠다
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
            // 페이드 상태를 되돌린다 — OnGUI가 `!isOpen`에서 곧바로 return하므로 닫힘 전이가
            // AnimatePanelOpen에 전달되지 않는다. 그대로 두면 wasOpen이 true로 굳어
            // **두 번째 열림부터 페이드가 사라진다**(다음 열림에서 전이가 감지되지 않는다).
            wasOpen = false;
            ModalUIRegistry.Unregister(this);
            if (playerMovement != null) playerMovement.SetFrozen(false);
            if (currentNpc != null) currentNpc.EndTalk();
            currentNpc = null;
            lines = null;

            // 스토리 비트였으면 완료 콜백(보상/seen). 상태를 먼저 비워 CompleteBeat 재진입에 안전.
            // 저널 다시보기(storyReplay)는 이미 열람·보상 완료된 비트라 콜백을 건너뛴다.
            if (storyMode)
            {
                InsectGame.Story.StoryBeat done = currentBeat;
                bool replay = storyReplay;
                storyMode = false;
                storyReplay = false;
                currentBeat = null;
                storyLines = null;
                if (!replay && storyDirector != null && done != null)
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
            // 스토리는 글씨도 크다 — 스타일은 1회 캐시라 크기만 매 프레임 지정한다
            // (LabelFit이 넘칠 때 줄였다가 원복하므로 여기서 기준값을 다시 세워야 한다).
            nameStyle.fontSize = storyMode ? 38 : 26;
            lineStyle.fontSize = storyMode ? 34 : 24;
            UIScale.Begin();

            // 패널 페이드 — 대사창이 툭 튀어나오면 NPC가 다가와 인사하는 흐름이 거기서 끊긴다.
            // **열릴 때만** 페이드한다: 닫을 때는 CloseModal이 lines/storyLines/currentBeat을
            // 그 자리에서 비우고 보상까지 지급하므로(CompleteBeat), 사라지는 동안 그릴 내용이
            // 남아 있지 않다. 내용을 살려 두려면 비트 완료 시점을 미뤄야 하는데 그건 이 저장소가
            // 영구 정지를 겪은 자리라 건드리지 않는다.
            float panelAlpha = UIHelper.AnimatePanelOpen(ref openFade, isOpen, ref wasOpen);
            GUI.color = new Color(1f, 1f, 1f, panelAlpha);

            // **스토리는 화면 가운데 크게, 일반 대화는 하단에.**
            // 둘을 같은 하단 띠에 그리면 지금 보고 있는 것이 이야기인지 잡담인지 구분되지 않는다.
            // 딤은 뒤 월드를 눌러 시선을 대사로 모으되, 완전히 가리지는 않는다 —
            // "지금 어디서 듣고 있는지"가 보여야 장면이 이어진다.
            Rect panel = storyMode
                ? UISafeLayout.CenteredPanel(1180f, 560f)
                : UISafeLayout.BottomPanel(920f, 210f);
            float panelW = panel.width;
            float panelH = panel.height;
            float px = panel.x;
            float py = panel.y;

            if (storyMode)
            {
                UISurface.Dim(0.68f);
                UISurface.Card(new Rect(px, py, panelW, panelH),
                    new Color(0.04f, 0.05f, 0.10f, 0.97f), UITheme.Instance.accentAmber);
                // 상단 액센트 — 둥근 모서리를 뚫지 않게 긴 축을 반경만큼 물린다(rules/ui-layout.md).
                UISurface.Flat(
                    new Rect(px + UITheme.Radius.Card, py + 3f,
                        panelW - UITheme.Radius.Card * 2f, 5f),
                    UITheme.Instance.accentAmber);
            }
            else
            {
                // 페이드 알파를 곱해 넣고, 복구도 흰색이 아니라 그 알파로 되돌린다 —
                // 흰색으로 되돌리면 이 뒤의 이름·대사·버튼이 페이드에서 빠진다.
                GUI.color = new Color(0f, 0f, 0f, 0.82f * panelAlpha);
                GUI.DrawTexture(new Rect(px, py, panelW, panelH), panelTex);
                GUI.color = new Color(1f, 1f, 1f, panelAlpha);
            }

            // 이름 + 대사 — 스토리 모드는 라인별 speaker(없으면 비트 speakerNpcId), 아니면 NPC 이름.
            string npcName;
            if (storyMode)
            {
                InsectGame.Story.StoryLine sl = (storyLines != null
                    && lineIndex >= 0 && lineIndex < storyLines.Length) ? storyLines[lineIndex] : null;
                if (sl != null && !string.IsNullOrEmpty(sl.speaker)) npcName = sl.speaker;
                else if (currentBeat != null && !string.IsNullOrEmpty(currentBeat.speakerNpcId)) npcName = currentBeat.speakerNpcId;
                else npcName = "???";
                // 직접 다가가 말을 건 조우(NpcTalk)면 화자명에 플로리시 — 만남을 강조.
                if (currentBeat != null && currentBeat.trigger != null && currentBeat.trigger.type == "NpcTalk")
                    npcName = "✦ " + npcName;
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
            // 중앙 패널은 훨씬 크므로 고정 오프셋을 그대로 쓰면 아래가 텅 빈다 — 높이에서 파생한다.
            float nameH = storyMode ? 52f : 34f;
            float nameY = py + (storyMode ? 30f : 16f);
            float lineY = nameY + nameH + (storyMode ? 18f : 6f);
            float btnBandH = storyMode ? 96f : 70f;
            float lineH = Mathf.Max(40f, py + panelH - btnBandH - lineY);

            GUI.Label(new Rect(textX, nameY, textW, nameH), npcName, nameStyle);
            // 대사 길이는 데이터가 정한다 — 상자에 안 들어가면 폰트를 줄여 맞춘다.
            // 초상화가 붙는 스토리 대사는 textW까지 좁아져 더 쉽게 넘친다.
            UIHelper.LabelFit(new Rect(textX, lineY, textW, lineH),
                lines[Mathf.Clamp(lineIndex, 0, lines.Length - 1)], lineStyle);

            // 진행 표시 (n/총)
            GUI.Label(new Rect(px + panelW - 120f, nameY, 92f, 30f),
                $"{lineIndex + 1}/{lines.Length}", nameStyle);

            // 버튼 — 마지막 줄이면 [닫기]만, 아니면 [다음]/[닫기]
            float btnW = storyMode ? 220f : 170f;
            float btnH = storyMode ? 72f : 56f;
            float btnY = py + panelH - btnH - (storyMode ? 24f : 14f);
            bool isLast = lineIndex >= lines.Length - 1;

            if (!isLast)
            {
                if (GUI.Button(new Rect(px + panelW - btnW * 2f - 40f, btnY, btnW, btnH), "다음", buttonStyle))
                    lineIndex++;
            }
            if (GUI.Button(new Rect(px + panelW - btnW - 24f, btnY, btnW, btnH), "닫기", buttonStyle))
                CloseModal();

            // 페이드 알파를 남기지 않는다 — GUI.color는 전역이라 다음 컴포넌트의 OnGUI까지 물든다.
            GUI.color = Color.white;
            UIScale.End();
        }

        // 스토리 화자(어르신/라온/세라) 좌측 포트레이트 — CharacterPortraitRenderer 재사용.
        // 반환: 그린 포트레이트 폭 오프셋(0이면 아는 화자 아님 → 포트레이트 없음).
        private float DrawStoryPortrait(float px, float py, float panelH, string speakerNpcId)
        {
            if (!GetStoryPortrait(speakerNpcId, out int gender, out int skinIdx, out int hairIdx,
                    out int hairStyle, out int faceType, out Color top, out Color hat))
                return 0f;

            // 중앙 패널은 세로로 길다 — panelH를 그대로 쓰면 초상화가 패널을 통째로 채운다.
            // 정사각 상자를 상단에 붙이고 남는 세로는 대사가 쓴다.
            float box = storyMode
                ? Mathf.Min(300f, panelH * 0.52f)
                : panelH - 24f;
            float boxX = px + (storyMode ? 26f : 14f);
            float boxY = py + (storyMode ? 26f : 12f);

            // 주변색(패널 페이드 알파)을 곱해 넣고 끝에 되돌린다 — 이 메서드는 호출부의
            // panelAlpha를 인자로 받지 않으므로, UISurface와 같은 방식으로 GUI.color를 보존한다.
            Color ambient = GUI.color;
            GUI.color = new Color(0.12f, 0.1f, 0.06f, 0.9f) * ambient;
            GUI.DrawTexture(new Rect(boxX, boxY, box, box), panelTex);
            GUI.color = new Color(1f, 0.85f, 0.45f, 0.5f) * ambient;
            GUI.DrawTexture(new Rect(boxX, boxY, box, 3f), panelTex);
            GUI.color = ambient;

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
                // 1막 하수 2인 — 검은 상의로 통일한다. 이름 대신 그 색이 이들의 정체다.
                // 2막 간부와 같은 계열이라 나중에 "그때 그 옷"으로 회수된다.
                case "ledger_thug_cord": // 끈 — 챙 깊은 모자로 얼굴을 가린다
                    gender = 0; skinIdx = 2; hairIdx = 0; hairStyle = 0; faceType = 1;
                    top = new Color(0.16f, 0.16f, 0.20f); hat = new Color(0.10f, 0.10f, 0.13f); return true;
                case "ledger_thug_rule": // 자 — 모자 없이 묶은 머리
                    gender = 1; skinIdx = 1; hairIdx = 3; hairStyle = 3; faceType = 1;
                    top = new Color(0.16f, 0.16f, 0.20f); hat = new Color(0f, 0f, 0f, 0f); return true;
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
