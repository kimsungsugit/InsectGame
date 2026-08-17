using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 필드 안내 문구 두 줄 — "이동 잠금 해제(ESC)"와 "리전 진입 레벨 부족".
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
            bool showFrozen = playerMovement.IsFrozen && !ModalUIRegistry.IsAnyOpen();
            float blockedAlpha = playerMovement.BlockedMessageAlpha;
            bool showBlocked = blockedAlpha > 0f && !string.IsNullOrEmpty(playerMovement.BlockedMessage);
            if (!showFrozen && !showBlocked) return;

            EnsureStyles();
            UIScale.Begin();

            if (showFrozen)
            {
                const float h = 40f;
                GUI.Label(
                    new Rect(UIScale.VirtualSafeLeft, UISafeLayout.BottomY(h),
                        UISafeLayout.ContentWidth, h),
                    "ESC를 누르면 이동 잠금을 해제합니다", frozenStyle);
            }

            if (showBlocked)
            {
                // 화면 아래쪽 6할 — 비율 배치는 금지가 아니지만(rules/ui-layout.md) 고정 높이
                // 상자를 놓으므로 안전 영역 안으로 가둔다.
                const float h = 40f;
                float y = Mathf.Clamp(UIScale.VirtualScreenHeight * 0.6f,
                    UISafeLayout.ContentTop, UISafeLayout.ContentBottom - h);

                Color col = BlockTextCol;
                col.a = blockedAlpha;
                blockStyle.normal.textColor = col;   // 알파가 매 프레임 바뀐다(struct라 할당 아님)
                // 리전 이름이 길이를 정하는데 상자는 고정이다 — 넘치면 글자를 줄여 맞춘다.
                UIHelper.LabelFit(
                    new Rect(UISafeLayout.ContentLeft, y, UISafeLayout.ContentWidth, h),
                    playerMovement.BlockedMessage, blockStyle);
            }

            UIScale.End();
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
