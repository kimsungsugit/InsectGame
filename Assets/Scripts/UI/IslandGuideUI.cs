using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 섬 안내 — 첫 방문 때의 단계형 코치 배너와, 언제든 다시 여는 도움말 창.
    ///
    /// 배너는 <b>실제 행동으로</b> 넘어간다(판정은 <see cref="IslandGuideSteps"/>, 진행 저장은 섬 세이브).
    /// 기존 <c>GuidedTutorialController</c>를 안 쓰는 이유는 그쪽이 Story 퀘스트의 활성 이벤트에 묶여 있어서다 —
    /// 섬 퀘스트는 Side라 그 이벤트가 울리지 않는다.
    ///
    /// 배너는 모달이 아니다. 「물건 놓기」 단계는 꾸미기 화면(모달) 위에서 떠야 하므로, 다른 HUD와 달리
    /// "모달이 하나라도 열렸으면 숨는다"가 아니라 <b>맨 위 모달이 꾸미기 화면일 때는 그린다</b>.
    /// </summary>
    public class IslandGuideUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private IslandManager island;
        [SerializeField] private IslandWorldBuilder world;
        [SerializeField] private IslandEditUI editUI;

        private bool helpOpen;
        private Vector2 helpScroll;
        private readonly UIDirectScroll directScroll = new UIDirectScroll();

        public bool IsOpen => helpOpen;

        public void AutoWire(IslandManager islandManager, IslandWorldBuilder worldBuilder, IslandEditUI edit)
        {
            if (island == null) island = islandManager;
            if (world == null) world = worldBuilder;
            if (editUI == null) editUI = edit;
        }

        public void OpenHelp()
        {
            if (helpOpen) return;
            helpOpen = true;
            helpScroll = Vector2.zero;
            directScroll.Reset();
            ModalUIRegistry.Register(this);
        }

        public void CloseModal()
        {
            helpOpen = false;
            directScroll.Reset();
            ModalUIRegistry.Unregister(this);
        }

        private void OnDisable() => CloseModal();

        private void OnGUI()
        {
            if (island == null || world == null) return;

            if (helpOpen)
            {
                if (!ReferenceEquals(ModalUIRegistry.TopModal, this)) return;
                UIScale.Begin();
                DrawHelp();
                UIScale.End();
                return;
            }

            if (world.Mode != IslandMode.Own || !island.GuideActive) return;
            // 꾸미기 화면 위에서는 그린다. 그 밖의 모달(상점·곤충·방문·대화) 위에서는 숨는다.
            IModalUI top = ModalUIRegistry.IsAnyOpen() ? ModalUIRegistry.TopModal : null;
            if (top != null && !ReferenceEquals(top, editUI)) return;

            UIScale.Begin();
            DrawCoach(top != null);
            UIScale.End();
        }

        private void DrawCoach(bool overEdit)
        {
            UITheme t = UITheme.Instance;
            IslandGuideStep step = island.GuideStep;
            bool replay = island.GuideIsReplay;
            bool needsButton = replay || step == IslandGuideStep.Finish;

            Rect banner;
            float h;
            if (UIScale.IsMobileLayout)
            {
                // 모바일: 우상단에 퀵바가 있고 그 아래로 섬 버튼 줄이 내려온다 — 그 왼쪽 빈 자리에 놓는다.
                // 가운데에 두면 퀵바와 겹치고, 아래로 내리면 좌하단 조이스틱 영역에 든다.
                Rect quick = QuickAccessBarUI.ShortcutBarRect;
                h = 200f;
                banner = new Rect(UISafeLayout.ContentLeft, quick.yMax + 10f,
                    Mathf.Max(320f, quick.x - UISafeLayout.ContentLeft - 12f), h);
            }
            else
            {
                h = 148f;
                banner = UISafeLayout.TopPanel(Mathf.Min(860f, UISafeLayout.ContentWidth), h);
                // 꾸미기 화면에서는 그 화면의 상단 바·안내 문구 아래로 내린다.
                banner.y += overEdit ? 160f : 150f;
            }
            // 배너 위 탭이 클릭-이동(필드)이나 칸 고르기(꾸미기)로 새지 않게.
            FieldHudInput.RegisterBlockingRect(banner);

            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3.5f);
            UISurface.Card(banner, new Color(t.surfaceCard.r, t.surfaceCard.g, t.surfaceCard.b, 0.96f),
                Color.Lerp(t.surfaceBorder, t.accentAmber, pulse));

            const float btnW = 150f;
            float textW = banner.width - 40f - (needsButton ? btnW + 12f : 0f);
            IslandUiKit.Label(new Rect(banner.x + 20f, banner.y + 10f, textW, 44f), IslandGuideSteps.Title(step),
                IslandUiKit.Title, t.accentAmber);
            IslandUiKit.Label(new Rect(banner.x + 20f, banner.y + 56f, textW, h - 68f), IslandGuideSteps.Body(step),
                IslandUiKit.BodyWrap, t.textPrimary);

            if (!needsButton) return;
            Rect btn = new Rect(banner.xMax - btnW - 14f, banner.y + (h - 68f) * 0.5f, btnW, 68f);
            string label = step == IslandGuideStep.Finish ? "확인" : "다음";
            if (!UISurface.Button(btn, label, t.accentMint, IslandUiKit.Button)) return;
            if (replay) island.AdvanceGuideReplay();
            else island.AcknowledgeGuideFinish();
        }

        private void DrawHelp()
        {
            UISurface.Dim(0.6f);
            UITheme t = UITheme.Instance;
            bool mobile = UIScale.IsMobileLayout;
            Rect panel = UISafeLayout.CenteredPanel(mobile ? 1000f : 920f, mobile ? 1400f : 900f);
            UISurface.Card(panel);
            if (UISurface.Header(new Rect(panel.x + 3f, panel.y + 3f, panel.width - 6f, 84f), "섬 도움말", string.Empty))
            {
                CloseModal();
                return;
            }

            Rect area = new Rect(panel.x + 20f, panel.y + 100f, panel.width - 40f,
                Mathf.Max(1f, panel.height - 100f - 100f));
            var topics = IslandGuideSteps.HelpTopics;
            float rowH = mobile ? 190f : 136f;
            const float gap = 10f;
            float contentH = topics.Length * (rowH + gap);
            Rect view = new Rect(0f, 0f, area.width - 16f, contentH);
            directScroll.Handle(ref helpScroll, area, contentH, rowH * 0.5f);
            helpScroll = GUI.BeginScrollView(area, helpScroll, view, GUIStyle.none, GUIStyle.none);
            for (int i = 0; i < topics.Length; i++)
            {
                Rect row = new Rect(0f, i * (rowH + gap), view.width, rowH);
                UISurface.Card(row, t.surfaceCard, t.surfaceBorder);
                IslandUiKit.Label(new Rect(row.x + 20f, row.y + 8f, row.width - 40f, 42f), topics[i].title,
                    IslandUiKit.Title, t.accentAmber);
                IslandUiKit.Label(new Rect(row.x + 20f, row.y + 52f, row.width - 40f, rowH - 62f), topics[i].body,
                    IslandUiKit.BodyWrap, t.textPrimary);
            }
            GUI.EndScrollView();
            UISurface.ScrollAffordance(area, helpScroll, contentH, t.accentMint);

            // 내 섬에 있을 때만 — 안내는 섬 위의 버튼을 가리키므로 다른 곳에서 다시 틀면 가리킬 것이 없다.
            Rect replayBtn = new Rect(panel.x + 20f, panel.yMax - 88f, panel.width - 40f, 68f);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && world.Mode == IslandMode.Own;
            if (UISurface.Button(replayBtn, "처음 안내 다시 보기", t.surfaceRaised, IslandUiKit.Button))
            {
                island.RestartGuide();
                CloseModal();
            }
            GUI.enabled = enabled;
        }
    }
}
