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
    ///
    /// 배너는 ✕로 닫거나 <see cref="CoachSeconds"/>가 지나면 사라진다. <b>닫는 것은 표시만이다</b> — 단계는 실제 행동으로만
    /// 넘어가고, 다음 단계에 가거나 섬에 다시 들어오면 그 단계 안내가 다시 뜬다. 전부 다시 보려면 도움말의 「처음 안내 다시 보기」.
    /// </summary>
    public class IslandGuideUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private IslandManager island;
        [SerializeField] private IslandWorldBuilder world;
        [SerializeField] private IslandEditUI editUI;

        /// <summary>단계 안내가 떠 있는 시간(초). 지나면 스스로 사라지고, 다음 단계에 가면 그 단계 안내가 다시 뜬다.</summary>
        internal const float CoachSeconds = 12f;
        internal const float CoachFadeSeconds = 1f;

        // 안내 배너의 표시 상태 — 저장하지 않는다(단계 진행은 섬 세이브가 들고 있다).
        private bool coachArmed;                                     // 지금 단계의 시간을 재기 시작했는가
        private IslandGuideStep coachStep = IslandGuideStep.Done;
        private bool coachReplay;
        private float coachShown;                                    // 이 단계 안내가 화면에 떠 있던 누적 시간
        private bool coachDismissed;                                 // ✕나 시간 경과로 닫았다

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

            if (world.Mode != IslandMode.Own || !island.GuideActive)
            {
                // 섬을 나갔다 다시 들어오면 지금 단계 안내를 처음부터 다시 보여 준다.
                coachArmed = false;
                return;
            }
            // 꾸미기 화면 위에서는 그린다. 그 밖의 모달(상점·곤충·방문·대화) 위에서는 숨는다(시간도 멈춘다).
            IModalUI top = ModalUIRegistry.IsAnyOpen() ? ModalUIRegistry.TopModal : null;
            if (top != null && !ReferenceEquals(top, editUI)) return;

            IslandGuideStep step = island.GuideStep;
            bool replay = island.GuideIsReplay;
            if (!coachArmed || step != coachStep || replay != coachReplay)
            {
                coachArmed = true;
                coachStep = step;
                coachReplay = replay;
                coachShown = 0f;
                coachDismissed = false;
            }
            if (coachDismissed) return;
            // 가운데 무대에 선 카드(퀘스트 완료·섬 토스트 등)와 겹치면 비켜선다 — 그리지 않으므로 표시 시간도 흐르지 않는다.
            if (HudStage.OccupiedOver(CoachRect(HudFrame.Current, top != null))) return;

            float remaining = CoachRemaining(coachShown, replay);
            bool repaint = Event.current != null && Event.current.type == EventType.Repaint;
            if (remaining <= 0f)
            {
                if (repaint) DismissCoach(step, replay);
                return;
            }
            // OnGUI는 한 프레임에 여러 번 온다(Layout·입력·Repaint) — 그리는 이벤트에서만 센다.
            if (repaint) coachShown += Time.unscaledDeltaTime;

            UIScale.Begin();
            DrawCoach(top != null, step, replay, remaining);
            UIScale.End();
        }

        /// <summary>
        /// 이 단계 안내가 더 떠 있을 시간(초). 0 이하면 사라진다. 다시 보기는 [다음]으로 직접 넘기므로 시간으로 닫지 않는다.
        /// </summary>
        internal static float CoachRemaining(float shownSeconds, bool replay)
            => replay ? CoachSeconds : CoachSeconds - shownSeconds;

        /// <summary>마지막 <see cref="CoachFadeSeconds"/> 동안 서서히 옅어진다.</summary>
        internal static float CoachAlpha(float remaining)
            => Mathf.Clamp01(remaining / CoachFadeSeconds);

        /// <summary>
        /// 안내를 닫는다(✕ 또는 시간 경과). <b>단계는 그대로다</b> — 실제 행동으로 다음 단계에 가면 그 단계 안내가 다시 뜬다.
        /// 마지막 인사는 닫는 것이 곧 확인이고, 다시 보기는 거기서 끝낸다.
        /// </summary>
        private void DismissCoach(IslandGuideStep step, bool replay)
        {
            coachDismissed = true;
            if (replay) island.EndGuideReplay();
            else if (step == IslandGuideStep.Finish) island.AcknowledgeGuideFinish();
        }

        /// <summary>
        /// 배너 자리(가상 좌표).
        ///
        /// <b>모바일 필드에서는 화면 가운데 줄, 캐릭터 발 아래.</b> 위쪽은 왼편에 미니맵·퀘스트 칩·목표 행이, 오른편에 퀵바와
        /// 섬 버튼 줄이 내려와 있어 빈 자리가 없다 — 예전엔 퀵바 왼쪽(좌상단)에 놓아서 미니맵·퀘스트 칩과 한 자리에 겹쳤고,
        /// OnGUI 그리기 순서가 정해져 있지 않아 안내가 그 밑에 깔렸다(2026-10-02 기기 보고).
        /// 꾸미기 화면 위에서는 다른 HUD가 전부 숨으므로 그 화면의 상단 바 아래에 둔다(가운데는 칸을 고르는 자리다).
        /// </summary>
        public static Rect CoachRect(HudFrame f, bool overEdit)
        {
            bool mobile = f.Mobile;
            if (mobile && !overEdit)
            {
                bool portrait = f.Portrait;
                float mh = portrait ? 210f : 170f;
                float w = Mathf.Min(portrait ? 900f : 760f, f.ContentWidth);
                float y = Mathf.Clamp(f.Height * (portrait ? 0.59f : 0.64f), f.ContentTop, f.ContentBottom - mh);
                return new Rect(f.ContentLeft + (f.ContentWidth - w) * 0.5f, y, w, mh);
            }

            float h = mobile ? 200f : 148f;
            Rect banner = f.TopPanel(Mathf.Min(mobile ? 900f : 860f, f.ContentWidth), h);
            // 꾸미기 화면에서는 그 화면의 상단 바·안내 문구 아래로 내린다.
            banner.y += overEdit ? 160f : 150f;
            return banner;
        }

        private void DrawCoach(bool overEdit, IslandGuideStep step, bool replay, float remaining)
        {
            UITheme t = UITheme.Instance;
            bool needsButton = replay || step == IslandGuideStep.Finish;

            Rect banner = CoachRect(HudFrame.Current, overEdit);
            float h = banner.height;
            HudPresence.Mark(HudPresenceItem.IslandGuide);   // 코치 배너(GuidedTutorialController)가 같은 자리를 비켜 준다
            // 배너 위 탭이 클릭-이동(필드)이나 칸 고르기(꾸미기)로 새지 않게.
            FieldHudInput.RegisterBlockingRect(banner);

            Color previous = GUI.color;
            GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * CoachAlpha(remaining));

            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3.5f);
            UISurface.Card(banner, new Color(t.surfaceCard.r, t.surfaceCard.g, t.surfaceCard.b, 0.96f),
                Color.Lerp(t.surfaceBorder, t.accentAmber, pulse));

            // 닫기 — 우상단. 모바일은 손가락 크기로.
            float closeSize = UIScale.IsMobileLayout ? 60f : 44f;
            Rect close = new Rect(banner.xMax - closeSize - 10f, banner.y + 8f, closeSize, closeSize);

            const float btnW = 150f;
            const float btnH = 68f;
            float bodyTop = banner.y + 58f;
            float bodyH = banner.yMax - 14f - bodyTop;
            float titleW = close.x - 12f - (banner.x + 20f);
            float bodyW = banner.width - 40f - (needsButton ? btnW + 12f : 0f);
            IslandUiKit.Label(new Rect(banner.x + 20f, banner.y + 10f, titleW, 44f), IslandGuideSteps.Title(step),
                IslandUiKit.Title, t.accentAmber);
            IslandUiKit.Label(new Rect(banner.x + 20f, bodyTop, bodyW, bodyH), IslandGuideSteps.Body(step),
                IslandUiKit.BodyWrap, t.textPrimary);

            // 남은 시간 — 스스로 사라진다는 걸 알려 주는 얇은 줄(둥근 모서리를 뚫지 않게 긴 축을 반경만큼 물린다).
            if (!replay)
            {
                float track = banner.width - UITheme.Radius.Card * 2f;
                UISurface.Flat(new Rect(banner.x + UITheme.Radius.Card, banner.yMax - 7f,
                    track * Mathf.Clamp01(remaining / CoachSeconds), 4f), t.accentAmber);
            }

            bool closed = UISurface.Button(close, "✕", t.surfaceRaised, IslandUiKit.Button);
            bool acted = false;
            if (needsButton)
            {
                Rect btn = new Rect(banner.xMax - btnW - 14f, banner.yMax - btnH - 16f, btnW, btnH);
                acted = UISurface.Button(btn, step == IslandGuideStep.Finish ? "확인" : "다음", t.accentMint,
                    IslandUiKit.Button);
            }
            GUI.color = previous;

            if (closed) DismissCoach(step, replay);
            else if (acted && replay) island.AdvanceGuideReplay();
            else if (acted) island.AcknowledgeGuideFinish();
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
