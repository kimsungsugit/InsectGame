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

        public bool IsOpen => isOpen;

        public void AutoWire(PlayerMovement player)
        {
            if (playerMovement == null) playerMovement = player;
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
        }

        private void OnDisable()
        {
            // 안전 해제 (CaptureChoiceUI 관례) — 파괴/비활성 시 스택 잔존 방지
            ModalUIRegistry.Unregister(this);
        }

        private void Update()
        {
            if (!isOpen) return;
            // 대화 상대가 사라짐(ApplyTuning 비활성화 등) — 안전 종료
            if (currentNpc == null || !currentNpc.gameObject.activeInHierarchy)
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

            // 이름 + 대사
            string npcName = currentNpc != null ? currentNpc.DisplayName : "주민";
            GUI.Label(new Rect(px + 28f, py + 16f, panelW - 56f, 34f), npcName, nameStyle);
            GUI.Label(new Rect(px + 28f, py + 56f, panelW - 56f, 88f),
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
