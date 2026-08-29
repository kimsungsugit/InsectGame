using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Dex;   // DexBrowseLayout — 목록 뷰포트 컬링 계산 공유
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 가방(인벤토리) 모달 — 보유 아이템을 보고 사용한다. HospitalUI 패턴.
    ///
    /// <b>이 화면이 없어서 아이템 시스템 전체가 잠겨 있었다.</b> 아이템을 여는 유일한 창구가
    /// uGUI <c>PlayerItemInventoryGridUIController</c>였는데, Bootstrap이 만들기만 하고
    /// <b>저장소 어디에서도 열지 않는다</b> — <c>Toggle</c>도 <c>SetActive</c>도 없고
    /// 퀵액세스 바 항목에도 없었다. 그래서 상점에서 산 부스터·치료제는 영영 쓸 수 없었고,
    /// "인벤토리에서 아이템을 선택해 사용하세요"라는 q_item 힌트는 가리킬 화면이 없었다.
    ///
    /// <b>종류마다 소비 경로가 다르다</b>(같은 "사용" 버튼이 세 갈래로 갈린다):
    /// <list type="bullet">
    /// <item>시간제 부스터 — 여기서 즉시 소비하고 <see cref="ItemEffectManager.ActivateItem"/>로 건다.</item>
    /// <item>대상지정 치료(<c>isTargetedUse</c>) — <see cref="HospitalUI.UseTreatmentItem"/>로
    ///       곤충 선택기를 연다. <b>소비는 거기서</b> 일어나므로 여기서 차감하면 이중 소모다.</item>
    /// <item>포획 전용 아이템 — 가방에서 못 쓴다. 야생 곤충을 만났을 때
    ///       <see cref="CaptureChoiceUI"/>가 미니게임 난이도 보정으로 소비한다.</item>
    /// </list>
    ///
    /// <b>채집망은 세 갈래 중 어디에 속하는지가 ID마다 다르다.</b> <c>net_silver</c>·<c>net_gold</c>는
    /// <see cref="ItemDatabase"/>에 시간제 부스터로도 정의돼 있어 가방에서 쓰면 10분 효과로 타 없어지고,
    /// <c>net_basic</c>은 DB에 없어 포획 전용이다. 그래서 DB 조회만으로 목록을 만들면
    /// <b>보유 중인데 가방에 안 보이는 아이템</b>이 생긴다 — 포획 아이템 표를 폴백으로 함께 읽고,
    /// 양쪽에 다 있는 ID는 어느 쪽으로 쓰는 것인지 버튼·설명에 적는다(<c>alsoFieldItem</c>).
    /// </summary>
    public class InventoryUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private PlayerItemInventory inventory;
        [SerializeField] private ItemDatabase itemDatabase;
        [SerializeField] private ItemEffectManager effectManager;
        [SerializeField] private HospitalUI hospital;

        /// <summary>
        /// 포획 전용 아이템 표. <see cref="ItemDatabase"/>에 없는 채집망의 이름·설명 폴백이자
        /// "필드 전용" 판정의 근거다(여기 있고 DB엔 없으면 가방에서 쓸 수 없다).
        /// </summary>
        private CaptureItemData[] captureItems;

        private bool isOpen;
        private Vector2 scroll;
        private readonly UIDirectScroll directScroll = new UIDirectScroll();
        private string feedback = "";
        private float feedbackTimer;

        /// <summary>표시 한 줄. DB 아이템과 포획 전용 아이템을 같은 모양으로 다룬다.</summary>
        private struct Entry
        {
            public string itemId;
            public string displayName;
            public string description;
            public int count;
            public ItemRarity rarity;
            public ItemData data;        // null이면 포획 전용(가방에서 사용 불가)
            public bool targeted;        // 대상지정 치료 — 병원 선택기 경유
            /// <summary>
            /// 포획 아이템 표에도 있는 ID(<c>net_silver</c>·<c>net_gold</c>). 여기서 쓰면
            /// <b>시간제 부스터로 타 없어지고 미니게임 재고가 준다</b> — 같은 아이템이 두 시스템에서
            /// 다르게 소비되므로, 어느 쪽으로 쓰는 것인지 버튼과 설명에 분명히 적는다.
            /// </summary>
            public bool alsoFieldItem;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private bool entriesDirty = true;

        private bool stylesReady;
        private GUIStyle titleStyle, closeStyle, nameStyle, descStyle, countStyle,
            btnStyle, bannerStyle, feedbackStyle, emptyStyle, iconStyle;

        public void AutoWire(PlayerItemInventory inv, ItemDatabase db, ItemEffectManager effects)
        {
            if (inventory == null || inventory != inv)
            {
                if (inventory != null) inventory.ItemsChanged -= OnItemsChanged;
                inventory = inv;
                if (inventory != null) inventory.ItemsChanged += OnItemsChanged;
            }
            if (itemDatabase == null) itemDatabase = db;
            if (effectManager == null) effectManager = effects;
            entriesDirty = true;
        }

        public void AutoWire(HospitalUI hospitalUi)
        {
            if (hospital == null) hospital = hospitalUi;
        }

        public void AutoWire(CaptureItemData[] items)
        {
            if (captureItems == null) captureItems = items;
            entriesDirty = true;
        }

        public bool IsOpen => isOpen;

        public void Toggle()
        {
            isOpen = !isOpen;
            if (isOpen)
            {
                entriesDirty = true;
                scroll = Vector2.zero;
                feedbackTimer = 0f;
            }
            directScroll.Reset();
            if (isOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }

        public void CloseModal()
        {
            isOpen = false;
            directScroll.Reset();
            ModalUIRegistry.Unregister(this);
        }

        private void OnEnable()
        {
            // OnDisable이 ItemsChanged를 해지하므로 재활성 시 되살린다 — AutoWire는 Bootstrap에서
            // 한 번만 불리고, 오프닝 다시보기가 UI 루트를 통째로 껐다 켠다(rules/ui-layout.md).
            if (inventory != null) { inventory.ItemsChanged -= OnItemsChanged; inventory.ItemsChanged += OnItemsChanged; }
            entriesDirty = true;
        }

        private void OnDisable()
        {
            isOpen = false;
            directScroll.Reset();
            ModalUIRegistry.Unregister(this);
            if (inventory != null) inventory.ItemsChanged -= OnItemsChanged;
        }

        private void OnItemsChanged(PlayerItemSave _) { entriesDirty = true; }

        private void Update()
        {
            if (feedbackTimer > 0f) feedbackTimer -= Time.deltaTime;
        }

        private List<Entry> Entries()
        {
            // entries.Count를 조건에 넣지 않는다 — 보유가 0일 때 매 OnGUI마다 재계산되고
            // GetSnapshot()은 호출마다 PlayerItemSave + List를 새로 만든다(얕은 복사).
            if (!entriesDirty) return entries;
            entriesDirty = false;
            entries.Clear();
            if (inventory == null) return entries;

            PlayerItemSave snapshot = inventory.GetSnapshot();
            if (snapshot == null || snapshot.items == null) return entries;

            foreach (PlayerItemRecord record in snapshot.items)
            {
                if (record == null || string.IsNullOrEmpty(record.itemId) || record.count <= 0) continue;

                ItemData data = itemDatabase != null ? itemDatabase.FindById(record.itemId) : null;
                if (data != null)
                {
                    entries.Add(new Entry
                    {
                        itemId = record.itemId,
                        displayName = string.IsNullOrEmpty(data.displayName) ? record.itemId : data.displayName,
                        description = data.description,
                        count = record.count,
                        rarity = data.rarity,
                        data = data,
                        targeted = data.isTargetedUse,
                        alsoFieldItem = FindCaptureItem(record.itemId) != null
                    });
                    continue;
                }

                // DB에 없는 것 — 포획 전용 표에서 이름·설명을 빌린다. 그것도 없으면 ID를 그대로
                // 보여준다(조용히 감추면 "산 아이템이 사라졌다"로 읽힌다).
                CaptureItemData capture = FindCaptureItem(record.itemId);
                entries.Add(new Entry
                {
                    itemId = record.itemId,
                    displayName = capture != null && !string.IsNullOrEmpty(capture.displayName)
                        ? capture.displayName : record.itemId,
                    description = capture != null ? capture.description : "",
                    count = record.count,
                    rarity = ItemRarity.Common,
                    data = null,
                    targeted = false,
                    alsoFieldItem = capture != null
                });
            }

            // 지금 쓸 수 있는 것을 위로 — 부스터 → 치료 → 필드 전용. 같은 묶음 안에서는 등급 내림차순.
            entries.Sort((a, b) =>
            {
                int ga = GroupOf(a), gb = GroupOf(b);
                if (ga != gb) return ga.CompareTo(gb);
                int r = ((int)b.rarity).CompareTo((int)a.rarity);
                if (r != 0) return r;
                return string.CompareOrdinal(a.displayName, b.displayName);
            });
            return entries;
        }

        private static int GroupOf(Entry e)
        {
            if (e.data == null) return 2;
            return e.targeted ? 1 : 0;
        }

        private CaptureItemData FindCaptureItem(string itemId)
        {
            if (captureItems == null) return null;
            foreach (CaptureItemData item in captureItems)
                if (item != null && item.itemId == itemId) return item;
            return null;
        }

        private void OnGUI()
        {
            if (!isOpen) return;
            UIScale.Begin();
            EnsureStyles();
            DrawPanel();
            UIScale.End();
        }

        private void DrawPanel()
        {
            UITheme t = UITheme.Instance;
            Rect panel = UISafeLayout.CenteredPanel(920f, 900f);
            float px = panel.x, py = panel.y, pw = panel.width, ph = panel.height;

            UISurface.Card(new Rect(px, py, pw, ph), t.panelBg, t.surfaceBorder);
            // 가방 악센트 스트라이프 — 8px라 각진 채로 두고(둥근 9-slice는 테두리 폭이 높이를
            // 넘겨 뭉개진다) x를 카드 반경만큼 물려 둥근 모서리를 뚫지 않게 한다.
            UISurface.Flat(
                new Rect(px + UITheme.Radius.Card, py + 3f, pw - UITheme.Radius.Card * 2f, 8f),
                t.accentAmber);
            GUI.color = Color.white;

            GUI.Label(new Rect(px + 26f, py + 14f, pw - 200f, 50f), "가방 — 아이템", titleStyle);
            if (GUI.Button(new Rect(px + pw - 74f, py + 14f, 58f, 58f), "X", closeStyle)) { CloseModal(); return; }

            float ty = py + 78f;
            float bannerH = UIScale.IsMobileLayout ? UIScale.MinTouchHeight : 44f;
            DrawActiveBanner(new Rect(px + 20f, ty, pw - 40f, bannerH));

            float listY = ty + bannerH + 12f;
            float listH = ph - (listY - py) - 20f;
            Rect listArea = new Rect(px + 20f, listY, pw - 40f, listH);

            List<Entry> rows = Entries();
            if (rows.Count == 0)
            {
                GUI.Label(listArea, "가진 아이템이 없습니다.\n상점에서 사거나 필드에서 주울 수 있습니다.", emptyStyle);
                DrawFeedback(px, py, pw, ph);
                return;
            }

            float rowH = UIScale.IsMobileLayout ? 156f : 132f;
            const float gap = 8f;
            float contentH = rows.Count * (rowH + gap);
            Rect view = new Rect(0f, 0f, listArea.width, contentH);
            directScroll.Handle(ref scroll, listArea, contentH, rowH * 0.45f);
            scroll = GUI.BeginScrollView(listArea, scroll, view, GUIStyle.none, GUIStyle.none);
            DexBrowseLayout.GetVisibleRowRange(
                scroll.y, listArea.height, rowH, gap, rows.Count,
                out int firstVisible, out int lastVisible);
            for (int i = firstVisible; i <= lastVisible; i++)
                DrawItemRow(new Rect(0f, i * (rowH + gap), view.width, rowH), rows[i]);
            GUI.EndScrollView();
            UISurface.ScrollAffordance(listArea, scroll, contentH, UITheme.Instance.accentAmber);

            DrawFeedback(px, py, pw, ph);
        }

        /// <summary>지금 걸려 있는 시간제 효과와 남은 시간. 없으면 무엇이 되는지 안내한다.</summary>
        private void DrawActiveBanner(Rect rect)
        {
            ItemData active = effectManager != null ? effectManager.ActiveItem : null;
            if (active == null)
            {
                UISurface.Card(rect, UITheme.Instance.surfaceCard, UITheme.Instance.surfaceBorder);
                bannerStyle.normal.textColor = UITheme.Instance.textMuted;
                UIHelper.LabelFit(new Rect(rect.x + 14f, rect.y, rect.width - 28f, rect.height),
                    "사용 중인 효과 없음 — 부스터를 쓰면 여기에 남은 시간이 표시됩니다", bannerStyle);
                return;
            }

            float remaining = effectManager.RemainingSeconds;
            float total = Mathf.Max(1f, active.durationSeconds);
            UISurface.Card(rect, new Color(0.12f, 0.18f, 0.16f, 0.95f), UITheme.Instance.accentMint);
            // 남은 시간 게이지 — 6px 얇은 바라 Flat(각짐)이고, 긴 축을 카드 반경만큼 물린다.
            float barY = rect.yMax - 9f;
            float barX = rect.x + UITheme.Radius.Card;
            float barW = rect.width - UITheme.Radius.Card * 2f;
            UISurface.Flat(new Rect(barX, barY, barW, 6f), new Color(0.16f, 0.2f, 0.24f));
            UISurface.Flat(new Rect(barX, barY, barW * Mathf.Clamp01(remaining / total), 6f),
                UITheme.Instance.accentMint);
            GUI.color = Color.white;

            bannerStyle.normal.textColor = UITheme.Instance.accentMint;
            int mm = Mathf.FloorToInt(remaining / 60f);
            int ss = Mathf.FloorToInt(remaining % 60f);
            UIHelper.LabelFit(new Rect(rect.x + 14f, rect.y, rect.width - 28f, rect.height - 8f),
                $"사용 중: {active.displayName}   {mm:00}:{ss:00} 남음", bannerStyle);
        }

        private void DrawItemRow(Rect rect, Entry entry)
        {
            UITheme t = UITheme.Instance;
            Color rarityCol = entry.data != null ? t.GetItemRarityColor(entry.rarity) : t.textMuted;

            UISurface.Card(rect, new Color(0.10f, 0.12f, 0.18f, 0.9f), t.surfaceBorder);
            // 등급 레일 — 5px라 각진 채로, 세로를 카드 반경만큼 물려 둥근 모서리 안쪽에 둔다.
            UISurface.Flat(
                new Rect(rect.x + 3f, rect.y + 3f + UITheme.Radius.Card, 5f,
                    Mathf.Max(4f, rect.height - 6f - UITheme.Radius.Card * 2f)),
                rarityCol);
            GUI.color = Color.white;

            // 아이콘 — 스프라이트가 있으면 그대로, 없으면 등급색 타일 + 이름 첫 글자.
            Rect icon = new Rect(rect.x + 20f, rect.y + (rect.height - 76f) * 0.5f, 76f, 76f);
            if (entry.data != null && entry.data.icon != null && entry.data.icon.texture != null)
            {
                GUI.DrawTexture(icon, entry.data.icon.texture, ScaleMode.ScaleToFit);
            }
            else
            {
                UISurface.Rounded(icon, new Color(rarityCol.r * 0.35f, rarityCol.g * 0.35f, rarityCol.b * 0.35f, 0.95f),
                    UITheme.Radius.Chip);
                iconStyle.normal.textColor = rarityCol;
                GUI.Label(icon, string.IsNullOrEmpty(entry.displayName) ? "?" : entry.displayName.Substring(0, 1), iconStyle);
            }

            float btnW = 186f;
            float textX = icon.xMax + 16f;
            float textW = rect.width - (textX - rect.x) - btnW - 32f;

            nameStyle.normal.textColor = entry.data != null ? Color.white : t.textSecondary;
            UIHelper.LabelFit(new Rect(textX, rect.y + 12f, textW - 96f, 36f), entry.displayName, nameStyle);

            countStyle.normal.textColor = t.accentAmber;
            GUI.Label(new Rect(textX + textW - 96f, rect.y + 12f, 92f, 36f), $"x{entry.count}", countStyle);

            descStyle.normal.textColor = t.textSecondary;
            string desc = string.IsNullOrEmpty(entry.description) ? "—" : entry.description;
            if (entry.alsoFieldItem && entry.data != null)
                desc += "  (야생 곤충 앞에서 쓰면 대신 미니게임이 쉬워집니다)";
            UIHelper.LabelFit(new Rect(textX, rect.y + 52f, textW, 40f), desc, descStyle);

            UISurface.Chip(new Rect(textX, rect.y + rect.height - 38f, 96f, 28f),
                entry.data != null ? RarityLabel(entry.rarity) : "필드 전용",
                new Color(rarityCol.r * 0.28f, rarityCol.g * 0.28f, rarityCol.b * 0.28f, 0.95f), rarityCol);

            // 사용 버튼 — 세 갈래(부스터/치료/필드 전용) 중 어디로 가는지 라벨로 먼저 알린다.
            float actionH = UIScale.IsMobileLayout ? UIScale.MinTouchHeight : 56f;
            Rect btn = new Rect(rect.xMax - btnW - 16f, rect.y + (rect.height - actionH) * 0.5f, btnW, actionH);
            bool usable = entry.data != null;
            string label = entry.data == null ? "필드에서 사용"
                : entry.targeted ? "곤충에게 사용"
                : entry.alsoFieldItem ? "부스터로 사용"
                : "사용";

            GUI.backgroundColor = usable ? t.btnPrimary : t.btnDisabled;
            GUI.enabled = usable;
            if (GUI.Button(btn, label, btnStyle)) TryUse(entry);
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;
        }

        private void DrawFeedback(float px, float py, float pw, float ph)
        {
            if (feedbackTimer <= 0f || string.IsNullOrEmpty(feedback)) return;
            // 목록 위에 겹치는 자리라 배경 없이는 카드에 묻혀 읽히지 않는다(HospitalUI와 같은 이유).
            float alpha = Mathf.Clamp01(feedbackTimer);
            Rect fr = new Rect(px + 20f, py + ph - 52f, pw - 40f, 40f);
            UISurface.Card(fr, new Color(0.06f, 0.08f, 0.12f, alpha * 0.94f), UITheme.Instance.surfaceBorder);
            GUI.color = Color.white;
            feedbackStyle.normal.textColor = new Color(0.5f, 1f, 0.6f, alpha);
            GUI.Label(fr, feedback, feedbackStyle);
        }

        private void TryUse(Entry entry)
        {
            if (inventory == null || entry.data == null) return;

            // 대상지정 치료 — 병원 선택기가 곤충을 고를 때 소비한다. 여기서 차감하면 이중 소모다.
            if (entry.targeted)
            {
                if (hospital == null) { Feedback("병원 화면을 찾을 수 없습니다"); return; }
                CloseModal();
                hospital.UseTreatmentItem(entry.data, inventory);
                return;
            }

            if (effectManager == null) { Feedback("효과 매니저를 찾을 수 없습니다"); return; }
            ItemData previous = effectManager.ActiveItem;
            if (!inventory.UseItem(entry.itemId, 1)) { Feedback("아이템이 없습니다"); return; }
            effectManager.ActivateItem(entry.data);
            entriesDirty = true;
            // 동시 활성은 하나뿐이다(ItemEffectManager.activeItem 단일) — 덮어썼다면 그 사실을 말한다.
            Feedback(previous != null && previous.itemId != entry.itemId
                ? $"{entry.data.displayName} 사용 — {previous.displayName} 효과를 대체했습니다"
                : $"{entry.data.displayName} 사용!");
        }

        private void Feedback(string message)
        {
            feedback = message;
            feedbackTimer = 2.5f;
        }

        private static string RarityLabel(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return "고급";
                case ItemRarity.Rare: return "희귀";
                case ItemRarity.Epic: return "영웅";
                case ItemRarity.Legendary: return "전설";
                default: return "일반";
            }
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;

            titleStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            titleStyle.normal.textColor = UITheme.Instance.titleColor;

            closeStyle = new GUIStyle(GUI.skin.button)
            { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            nameStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };

            descStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 20, wordWrap = true, alignment = TextAnchor.UpperLeft };

            countStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };

            btnStyle = new GUIStyle(GUI.skin.button)
            { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            bannerStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 22, alignment = TextAnchor.MiddleLeft };

            feedbackStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            emptyStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 24, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            emptyStyle.normal.textColor = UITheme.Instance.textMuted;

            iconStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }
    }
}
