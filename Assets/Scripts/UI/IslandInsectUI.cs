using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Dex;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 섬 곤충 창 — 보유 곤충을 섬에 풀어놓거나 거둔다. 풀어놓은 곤충은 보유 목록에 그대로 있고
    /// 전투·훈련에도 쓸 수 있다(배틀팀처럼 표시만 붙는다).
    /// </summary>
    public class IslandInsectUI : MonoBehaviour, IModalUI
    {
        private const float FeedbackSeconds = 2.4f;

        // 한 줄에 보여 줄 문구를 목록을 만들 때 한 번만 잇는다 — 행마다 매 프레임 이으면 목록이 길수록 GC가 는다.
        private class Row
        {
            public PlayerInsectData insect;
            public InsectData data;
            public bool released;
            public string title;
            public string detail;
            public string hearts;
        }

        [SerializeField] private IslandManager island;
        [SerializeField] private PlayerInsectCollection insects;

        private bool isOpen;
        private Vector2 scroll;
        private readonly UIDirectScroll directScroll = new UIDirectScroll();
        private readonly List<Row> rows = new List<Row>();
        private bool rowsDirty = true;
        private string summary = string.Empty;
        private string feedback;
        private float feedbackUntil;

        public bool IsOpen => isOpen;

        public void AutoWire(IslandManager islandManager, PlayerInsectCollection collection)
        {
            if (island == null) island = islandManager;
            if (insects == null) insects = collection;
        }

        public void Toggle()
        {
            if (isOpen) { CloseModal(); return; }
            if (island == null || insects == null) return;
            isOpen = true;
            scroll = Vector2.zero;
            rowsDirty = true;
            directScroll.Reset();
            // 창을 열 때 밀린 시간을 정산한다 — 친밀도와 시간당 생산이 지금 값으로 보인다.
            island.Settle();
            ModalUIRegistry.Register(this);
        }

        public void CloseModal()
        {
            isOpen = false;
            directScroll.Reset();
            ModalUIRegistry.Unregister(this);
        }

        private void OnDisable() => CloseModal();

        private void RebuildRows()
        {
            rows.Clear();
            IslandEffects effects = island.Effects;
            List<PlayerInsectData> owned = insects.GetAllOwned();
            for (int i = 0; i < owned.Count; i++)
            {
                PlayerInsectData p = owned[i];
                InsectData data = insects.GetInsectData(p.insectId);
                if (data == null) continue;
                bool released = island.IsReleased(p.instanceId);
                int bond = island.GetBondLevel(p.instanceId);
                string detail;
                if (released)
                {
                    detail = "시간당 캔디 " + IslandYield.CandyPerHour(data.rarity, bond, effects).ToString("0.00")
                             + " · 코인 " + IslandYield.CoinPerHour(bond, effects).ToString("0.00");
                }
                else
                {
                    detail = "풀어놓으면 시간당 캔디 "
                             + IslandYield.CandyPerHour(data.rarity, bond, effects).ToString("0.00");
                }
                rows.Add(new Row
                {
                    insect = p,
                    data = data,
                    released = released,
                    title = (p.isShiny ? "★ " : "") + data.displayName + "  Lv." + p.level,
                    detail = detail,
                    hearts = IslandUiKit.Hearts(bond),
                });
            }
            // 섬에 있는 곤충을 위로, 그다음 레벨 높은 순.
            rows.Sort((a, b) =>
            {
                if (a.released != b.released) return a.released ? -1 : 1;
                return b.insect.level.CompareTo(a.insect.level);
            });

            island.GetRates(out float candyRate, out float coinRate);
            summary = "섬에 있는 곤충 " + island.ReleasedInsects.Count + " / " + island.InsectSlots
                      + "  ·  시간당 캔디 " + candyRate.ToString("0.0") + " · 코인 " + coinRate.ToString("0.0");
            rowsDirty = false;
        }

        private void OnGUI()
        {
            if (!isOpen || island == null || insects == null) return;
            if (!ReferenceEquals(ModalUIRegistry.TopModal, this)) return;
            if (rowsDirty) RebuildRows();

            UIScale.Begin();
            UISurface.Dim(0.6f);
            UITheme t = UITheme.Instance;
            bool mobile = UIScale.IsMobileLayout;
            Rect panel = UISafeLayout.CenteredPanel(mobile ? 1000f : 980f, mobile ? 1500f : 940f);
            UISurface.Card(panel);
            if (UISurface.Header(new Rect(panel.x + 3f, panel.y + 3f, panel.width - 6f, 84f), "섬 곤충", string.Empty))
            {
                CloseModal();
                UIScale.End();
                return;
            }

            IslandUiKit.Label(new Rect(panel.x + 24f, panel.y + 98f, panel.width - 48f, 40f), summary,
                IslandUiKit.Body, t.accentAmber);

            Rect area = new Rect(panel.x + 20f, panel.y + 148f, panel.width - 40f,
                Mathf.Max(1f, panel.height - 148f - 72f));
            if (rows.Count == 0)
            {
                IslandUiKit.Label(area, "풀어놓을 곤충이 없습니다. 필드에서 곤충을 잡아 오세요.",
                    IslandUiKit.BodyCenter, t.textSecondary);
            }
            else
            {
                DrawList(area, t, mobile);
            }

            if (!string.IsNullOrEmpty(feedback) && Time.unscaledTime < feedbackUntil)
                IslandUiKit.Label(new Rect(panel.x + 20f, panel.yMax - 62f, panel.width - 40f, 48f), feedback,
                    IslandUiKit.BodyCenter, t.accentAmber);
            UIScale.End();
        }

        private void DrawList(Rect area, UITheme t, bool mobile)
        {
            float rowH = mobile ? 132f : 112f;
            const float gap = 8f;
            float contentH = rows.Count * (rowH + gap);
            Rect view = new Rect(0f, 0f, area.width - 16f, contentH);
            directScroll.Handle(ref scroll, area, contentH, rowH * 0.5f);
            scroll = GUI.BeginScrollView(area, scroll, view, GUIStyle.none, GUIStyle.none);
            // 뷰포트 컬링이 꼭 필요하다 — 곤충 썸네일 캐시가 24칸이라 보이지 않는 행까지 그리면 캐시가 매 프레임 밀린다.
            DexBrowseLayout.GetVisibleRowRange(scroll.y, area.height, rowH, gap, rows.Count, out int first, out int last);
            for (int i = first; i <= last; i++)
                DrawRow(new Rect(0f, i * (rowH + gap), view.width, rowH), rows[i], t);
            GUI.EndScrollView();
            UISurface.ScrollAffordance(area, scroll, contentH, t.accentMint);
        }

        private void DrawRow(Rect r, Row row, UITheme t)
        {
            UISurface.Card(r, row.released ? t.surfaceRaised : t.surfaceCard,
                row.released ? t.accentMint : t.surfaceBorder);
            UISurface.Flat(new Rect(r.x + 3f, r.y + UITheme.Radius.Card, 6f, r.height - UITheme.Radius.Card * 2f),
                t.GetInsectRarityColor(row.data.rarity));

            float portrait = r.height - 16f;
            InsectVisual.Draw(new Rect(r.x + 16f, r.y + 8f, portrait, portrait), row.data, row.insect.isShiny, 1f);

            const float btnW = 190f;
            float textX = r.x + 16f + portrait + 14f;
            float textW = r.xMax - btnW - 24f - textX;
            IslandUiKit.Label(new Rect(textX, r.y + 8f, textW * 0.68f, 40f), row.title, IslandUiKit.Title, t.textPrimary);
            IslandUiKit.Label(new Rect(textX + textW * 0.68f, r.y + 8f, textW * 0.32f, 40f), row.hearts,
                IslandUiKit.Body, t.accentCoral);
            IslandUiKit.Label(new Rect(textX, r.y + 50f, textW, 34f), row.detail, IslandUiKit.Small, t.textSecondary);

            Rect btn = new Rect(r.xMax - btnW - 12f, r.y + (r.height - 64f) * 0.5f, btnW, 64f);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !directScroll.IsDragging;
            if (row.released)
            {
                if (UISurface.Button(btn, "거두기", t.surfaceBase, IslandUiKit.Button)) Unrelease(row);
            }
            else if (UISurface.Button(btn, "풀어놓기", t.accentMint, IslandUiKit.Button))
            {
                Release(row);
            }
            GUI.enabled = enabled;
        }

        private void Release(Row row)
        {
            IslandReleaseResult result = island.TryRelease(row.insect.instanceId);
            ShowFeedback(result == IslandReleaseResult.Ok
                ? row.data.displayName + " — " + IslandUiKit.ReleaseResultText(result)
                : IslandUiKit.ReleaseResultText(result));
            rowsDirty = true;
        }

        private void Unrelease(Row row)
        {
            if (island.Unrelease(row.insect.instanceId))
                ShowFeedback(row.data.displayName + " — 섬에서 거두었습니다. 친밀도는 남아 있어요.");
            rowsDirty = true;
        }

        private void ShowFeedback(string message)
        {
            feedback = message;
            feedbackUntil = Time.unscaledTime + FeedbackSeconds;
        }
    }
}
