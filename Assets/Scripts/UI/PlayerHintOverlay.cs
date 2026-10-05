using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 필드 안내 문구 두 줄 — "이동 잠금 해제(ESC)"와 "잠긴 리전 진입 차단".
    /// 상태의 주인은 <see cref="PlayerMovement"/>(Core)이고 여기선 그리기만 한다.
    ///
    /// <b>왜 UI로 옮겨왔나.</b> 예전엔 <c>PlayerMovement</c>가 자기 <c>OnGUI</c>에서 직접 그렸는데,
    /// 거긴 <see cref="UIScale"/> 밖이라 <b>실제 픽셀 좌표</b>였다. 나머지 UI는 전부 가상 캔버스
    /// (가로 1920×1080 / 세로 1080×1920) 안에서 그려지므로 스케일이 1이 아닌 화면에서는 이 두 문구만
    /// 혼자 어긋났다 — 1440×3200 폰이나 2560×1440 데스크톱은 스케일이 1.333이라 같은 글자가
    /// 다른 라벨보다 25% 작게 찍히고, <c>Screen.height - 50</c>은 세이프에어리어를 무시해
    /// 제스처바 아래로 들어갔다. 1920×1080에서만 우연히 맞아 Game View로는 안 보인다.
    /// (<c>BattleArenaController</c> → <c>BattleEffectTextOverlay</c>와 같은 이유·같은 형태다.)
    ///
    /// Core가 <see cref="UIScale"/>를 직접 부를 수는 없다 — UI가 이미 Core를 참조하므로 순환이
    /// 된다(<c>rules/architecture.md</c>). 그리는 쪽을 UI로 옮기는 것이 방향에 맞다.
    /// </summary>
    public class PlayerHintOverlay : MonoBehaviour
    {
        private PlayerMovement playerMovement;

        private GUIStyle frozenStyle;
        private GUIStyle blockStyle;
        private bool stylesReady;

        private static readonly Color FrozenTextCol = new Color(1f, 1f, 0.5f, 0.7f);
        private static readonly Color BlockTextCol = new Color(1f, 0.4f, 0.3f);

        public void AutoWire(PlayerMovement movement)
        {
            if (playerMovement == null) playerMovement = movement;
        }

        private void OnGUI()
        {
            if (playerMovement == null) return;

            // **모달이 열려 있으면 잠금 안내를 띄우지 않는다.** 대화·컷신·NPC 연출도 전부 frozen인데,
            // 그때 ESC는 모달을 닫거나 컷신을 건너뛰는 키다 — "이동 잠금을 해제합니다"는 틀린 안내이고,
            // 스토리를 읽는 내내 화면 아래에 남아 방해가 된다. 이 문구가 필요한 건 모달 없이
            // frozen만 남은 상태(진짜로 갇힌 경우)뿐이다.
            // 두 문구 모두 모달 뒤에 숨는다. 잠금 안내는 위 이유로, 차단 문구는 **대화창·컷신 위에
            // 겹쳐 뜨기 때문**이다(스토리 NPC가 걸어와 말을 거는 지금 구조에선 리전에서 튕긴 직후
            // 2초 창이 자주 열린다). 타이머 자체는 `PlayerMovement`의 frozen 분기가 줄인다.
            // 「챔피언의 꿈」에서는 둘 다 띄우지 않는다 — 꿈의 잠금은 연출이라 ESC 안내가 틀리고, 꿈속엔 리전 경계가 없다.
            if (DreamPrologueState.Active) return;
            bool modalOpen = ModalUIRegistry.IsAnyOpen();
            bool showFrozen = playerMovement.IsFrozen && !modalOpen;
            float blockedAlpha = playerMovement.BlockedMessageAlpha;
            // 차단 문구는 가운데 무대(HudStage)의 마지막 차례다. 시간은 PlayerMovement가 쥐고 있어 기다리게 할 수 없으니
            // 앞 차례(포획 결과·퀘스트 알림 등)가 서 있으면 이번엔 건너뛴다 — 다시 부딪히면 또 뜬다.
            bool showBlocked = blockedAlpha > 0f && !modalOpen
                               && !string.IsNullOrEmpty(playerMovement.BlockedMessage)
                               && HudStage.Request(HudStageItem.RegionLock);
            if (!showFrozen && !showBlocked) return;

            EnsureStyles();
            UIScale.Begin();
            HudFrame frame = HudFrame.Current;

            if (showFrozen)
            {
                UIHelper.LabelFit(FrozenRect(frame), "ESC를 누르면 이동 잠금을 해제합니다", frozenStyle);
            }

            if (showBlocked)
            {
                Color col = BlockTextCol;
                col.a = blockedAlpha;
                blockStyle.normal.textColor = col;   // 알파가 매 프레임 바뀐다(struct라 할당 아님)
                // 리전 이름 + 수문장 이름이 길이를 정하는데 상자는 고정이다(최장 30자쯤:
                // "이름 없는 자리 — 우듬지의 세계수나비에게 이겨야 열립니다"). 넘치면 글자를 줄여 맞춘다.
                Rect blocked = BlockedRect(frame);
                HudStage.Request(HudStageItem.RegionLock, blocked);
                UIHelper.LabelFit(blocked, playerMovement.BlockedMessage, blockStyle);
            }

            UIScale.End();
        }

        public const float LineHeight = 40f;
        public const float BlockedWidth = 900f;

        /// <summary>
        /// 이동 잠금 안내의 자리 — 순수 계산. 동굴 입구 버튼 자리의 아랫줄이다: 잠긴 동안은 그 버튼도, 단축 바·잡기 버튼도
        /// 숨으니(모두 IsFrozen에서 물러난다) 비어 있는 자리다. 예전엔 바 높이만큼 위(BottomY−바)였는데, 바 높이가 바뀌자
        /// 데스크톱 퀘스트 칩·동굴 입구 버튼과 같은 줄이 됐다.
        /// </summary>
        public static Rect FrozenRect(HudFrame f)
        {
            Rect gate = SubAreaWorldBuilder.GateRect(f);
            return new Rect(gate.x, gate.yMax - LineHeight, gate.width, LineHeight);
        }

        /// <summary>잠긴 리전 차단 문구 — 가운데 무대의 마지막 차례(<see cref="HudStageItem.RegionLock"/>).</summary>
        public static Rect BlockedRect(HudFrame f)
        {
            return HudStage.Place(f, HudStageItem.RegionLock, BlockedWidth, LineHeight);
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;

            frozenStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter
            };
            frozenStyle.normal.textColor = FrozenTextCol;

            blockStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
