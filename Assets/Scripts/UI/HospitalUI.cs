using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 병원 치료 모달 — 보유 곤충의 지속 HP·상태(독/마비)를 재화로 치료.
    /// 코인/캔디 토글 결제(전투로 얻는 재화) + 젬 버튼(전액+상태 즉시치료). TrainingUI 패턴.
    /// 무료 전체치료 폐지(P1)의 짝 — 여기서만 회복 가능.
    /// </summary>
    public class HospitalUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private PlayerInsectCollection collection;
        [SerializeField] private InsectDatabase database;
        [SerializeField] private PlayerCurrencyWallet wallet;
        [SerializeField] private PlayerCandyInventory candyInventory;

        // 밸런스: 잃은 HP 1당 코인 0.5(올림)·캔디 0.4(올림), 상태 치료 정액, 젬 전액.
        private const float CoinPerHp = 0.5f;
        private const float CandyPerHp = 0.4f;
        private const int CurePoisonCoin = 15;
        private const int CureParalysisCoin = 15;
        private const int FullHealGems = 5;

        private bool isOpen;
        private string selectedInstanceId;
        private bool payWithCoins = true;   // true=코인, false=캔디
        // 대상지정 치료 아이템 모드 — 인벤토리에서 상처약/해독제 사용 시 곤충 선택기로 이 UI를 연다.
        private ItemData pendingItem;
        private PlayerItemInventory pendingInv;
        private Vector2 scroll;
        private string feedback = "";
        private float feedbackTimer;

        private List<PlayerInsectData> ownedCache;
        private bool ownedDirty = true;

        private bool stylesReady;
        private GUIStyle titleStyle, closeStyle, rowNameStyle, rowInfoStyle, btnStyle, toggleStyle, feedbackStyle, hintStyle, hpTextStyle;

        public void AutoWire(PlayerInsectCollection col, InsectDatabase db, PlayerCurrencyWallet w, PlayerCandyInventory candy)
        {
            if (collection == null) collection = col;
            if (database == null) database = db;
            if (wallet == null) wallet = w;
            if (candyInventory == null) candyInventory = candy;
            if (collection != null) { collection.InsectUpdated -= OnInsectUpdated; collection.InsectUpdated += OnInsectUpdated; }
            ownedDirty = true;
        }

        public bool IsOpen => isOpen;
        public void Toggle()
        {
            isOpen = !isOpen;
            // 월드 병원 버튼 = 일반 치료 방문 → 아이템 모드 잔존 클리어(stale pendingItem로 열리는 것 방지).
            if (isOpen) { selectedInstanceId = null; ownedDirty = true; pendingItem = null; pendingInv = null; }
            if (isOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }
        public void CloseModal()
        {
            isOpen = false;
            pendingItem = null;
            pendingInv = null;
            ModalUIRegistry.Unregister(this);
        }

        /// <summary>인벤토리에서 대상지정 치료 아이템 사용 — 이 UI를 곤충 선택기로 연다(선택 시 소비+적용).</summary>
        public void UseTreatmentItem(ItemData item, PlayerItemInventory inv)
        {
            if (item == null || inv == null) return;
            pendingItem = item;
            pendingInv = inv;
            selectedInstanceId = null;
            ownedDirty = true;
            if (!isOpen) { isOpen = true; ModalUIRegistry.Register(this); }
        }
        private void OnEnable()
        {
            // OnDisable이 InsectUpdated를 해지하므로 재활성 시 재구독한다. 첫 활성 땐 collection이
            // 아직 AutoWire 전이라 null → no-op(AutoWire가 최초 구독을 건다). 재활성 시 목록도 갱신.
            if (collection != null) { collection.InsectUpdated -= OnInsectUpdated; collection.InsectUpdated += OnInsectUpdated; }
            ownedDirty = true;
        }
        private void OnDisable()
        {
            isOpen = false;
            ModalUIRegistry.Unregister(this);
            if (collection != null) collection.InsectUpdated -= OnInsectUpdated;
        }

        private void OnInsectUpdated(PlayerInsectData _) { ownedDirty = true; }

        private void Update()
        {
            if (feedbackTimer > 0f) feedbackTimer -= Time.deltaTime;
        }

        private List<PlayerInsectData> Owned()
        {
            if (ownedDirty || ownedCache == null)
            {
                ownedCache = collection != null ? collection.GetAllOwned() : new List<PlayerInsectData>();
                ownedDirty = false;
            }
            return ownedCache;
        }

        private int MaxHpOf(PlayerInsectData pid)
        {
            InsectData data = database != null && pid != null ? database.GetById(pid.insectId) : null;
            int baseHp = data != null ? data.baseHp : 50;
            return pid != null ? pid.GetTotalHp(baseHp) : baseHp;
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
            float pw = Mathf.Min(920f, UIScale.ContentWidth(18f));
            float availH = UIScale.VirtualScreenHeight - UIScale.VirtualSafeTop - UIScale.VirtualSafeBottom;
            float ph = Mathf.Min(940f, availH - 24f);
            float px = (UIScale.VirtualScreenWidth - pw) / 2f;
            float py = UIScale.VirtualSafeTop + (availH - ph) * 0.5f;

            GUI.color = t.panelBg;
            GUI.DrawTexture(new Rect(px, py, pw, ph), Texture2D.whiteTexture);
            GUI.color = new Color(0.85f, 0.25f, 0.22f);   // 병원 적십자 악센트
            GUI.DrawTexture(new Rect(px, py, pw, 6f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            bool itemMode = pendingItem != null;
            string title = itemMode ? $"{pendingItem.displayName} 사용 — 곤충 선택" : "병원 — 곤충 치료";
            GUI.Label(new Rect(px + 26f, py + 14f, pw - 200f, 50f), title, titleStyle);
            if (GUI.Button(new Rect(px + pw - 74f, py + 14f, 58f, 58f), "X", closeStyle)) { CloseModal(); return; }

            float ty = py + 78f;
            if (itemMode)
            {
                GUI.Label(new Rect(px + 26f, ty, pw - 52f, 44f), pendingItem.description + "  (X로 취소)", hintStyle);
            }
            else
            {
                // 결제 수단 토글(코인/캔디) + 잔액
                int coins = wallet != null ? wallet.Coins : 0;
                int candies = candyInventory != null ? candyInventory.Candies : 0;
                int gems = wallet != null ? wallet.Gems : 0;
                GUI.backgroundColor = payWithCoins ? UITheme.Instance.tabSelected : UITheme.Instance.tabNormal;
                if (GUI.Button(new Rect(px + 26f, ty, 150f, 44f), $"코인 {coins}", toggleStyle)) payWithCoins = true;
                GUI.backgroundColor = !payWithCoins ? UITheme.Instance.tabSelected : UITheme.Instance.tabNormal;
                if (GUI.Button(new Rect(px + 184f, ty, 150f, 44f), $"캔디 {candies}", toggleStyle)) payWithCoins = false;
                GUI.backgroundColor = Color.white;
                GUI.Label(new Rect(px + 350f, ty, pw - 380f, 44f), $"젬 {gems} · 젬 {FullHealGems}로 선택 곤충 전액+상태 즉시치료", hintStyle);
            }

            // 곤충 목록
            float listY = ty + 58f;
            float listH = ph - (listY - py) - 20f;
            Rect listArea = new Rect(px + 20f, listY, pw - 40f, listH);
            List<PlayerInsectData> owned = Owned();
            float rowH = 118f;
            Rect view = new Rect(0, 0, listArea.width - 20f, owned.Count * (rowH + 8f));
            scroll = GUI.BeginScrollView(listArea, scroll, view);
            for (int i = 0; i < owned.Count; i++)
                DrawInsectRow(new Rect(0, i * (rowH + 8f), view.width, rowH), owned[i]);
            GUI.EndScrollView();

            if (feedbackTimer > 0f && !string.IsNullOrEmpty(feedback))
            {
                feedbackStyle.normal.textColor = new Color(0.5f, 1f, 0.6f, Mathf.Clamp01(feedbackTimer));
                GUI.Label(new Rect(px, py + ph - 44f, pw, 32f), feedback, feedbackStyle);
            }
        }

        private void DrawInsectRow(Rect rect, PlayerInsectData pid)
        {
            if (pid == null) return;
            InsectData data = database != null ? database.GetById(pid.insectId) : null;
            int maxHp = MaxHpOf(pid);
            int curHp = pid.currentHp < 0 ? maxHp : pid.currentHp;
            bool needsHeal = curHp < maxHp || pid.isPoisoned || pid.isParalyzed;
            bool selected = pid.instanceId == selectedInstanceId;

            GUI.color = selected ? new Color(0.18f, 0.22f, 0.34f, 0.95f) : new Color(0.10f, 0.12f, 0.18f, 0.9f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            Color rarityCol = data != null ? UITheme.Instance.GetInsectRarityColor(data.rarity) : Color.gray;
            GUI.color = rarityCol;
            GUI.DrawTexture(new Rect(rect.x, rect.y, 5f, rect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (data != null)
                CapturePopupUI.DrawTypedInsectPortrait(rect.x + 58f, rect.y + rect.height / 2f, data.insectId, data.rarity, 1f);

            string name = data != null ? data.displayName : pid.insectId;
            rowNameStyle.normal.textColor = pid.IsFainted ? new Color(0.9f, 0.4f, 0.4f) : Color.white;
            GUI.Label(new Rect(rect.x + 116f, rect.y + 12f, rect.width - 300f, 34f), $"{name}  Lv.{pid.level}", rowNameStyle);

            // HP바
            float barX = rect.x + 116f, barY = rect.y + 52f, barW = rect.width - 320f;
            GUI.color = new Color(0.15f, 0.15f, 0.2f);
            GUI.DrawTexture(new Rect(barX, barY, barW, 16f), Texture2D.whiteTexture);
            float ratio = maxHp > 0 ? Mathf.Clamp01((float)curHp / maxHp) : 0f;
            GUI.color = ratio > 0.5f ? new Color(0.3f, 0.85f, 0.35f) : ratio > 0.2f ? new Color(0.95f, 0.8f, 0.2f) : new Color(0.95f, 0.3f, 0.2f);
            GUI.DrawTexture(new Rect(barX, barY, barW * ratio, 16f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(barX, barY - 2f, barW, 20f), $"{curHp}/{maxHp}", hpTextStyle);

            // 상태 태그
            string status = "";
            if (pid.IsFainted) status += "기절 ";
            if (pid.isPoisoned) status += "독 ";
            if (pid.isParalyzed) status += "마비 ";
            if (status.Length > 0)
            {
                rowInfoStyle.normal.textColor = new Color(1f, 0.6f, 0.4f);
                GUI.Label(new Rect(barX, barY + 22f, barW, 24f), status.Trim(), rowInfoStyle);
            }
            else if (!needsHeal)
            {
                rowInfoStyle.normal.textColor = new Color(0.5f, 0.8f, 0.5f);
                GUI.Label(new Rect(barX, barY + 22f, barW, 24f), "정상", rowInfoStyle);
            }

            // 치료 버튼 영역(우측)
            float bx = rect.x + rect.width - 190f;
            if (pendingItem != null)
            {
                // 아이템 사용 모드 — 이 곤충에 적용 가능하면 '사용' 버튼.
                bool applicable = (pendingItem.healAmount > 0 && curHp < maxHp)
                    || (pendingItem.curePoison && pid.isPoisoned)
                    || (pendingItem.cureParalysis && pid.isParalyzed);
                GUI.backgroundColor = applicable ? UITheme.Instance.btnPrimary : UITheme.Instance.btnDisabled;
                if (GUI.Button(new Rect(bx, rect.y + 34f, 176f, 48f), applicable ? "사용" : "대상 아님", btnStyle) && applicable)
                    ApplyTreatmentItem(pid);
                GUI.backgroundColor = Color.white;
            }
            else if (needsHeal)
            {
                int lostHp = maxHp - curHp;
                int cost = HealCost(lostHp, pid);
                string curLabel = payWithCoins ? "코인" : "캔디";
                GUI.backgroundColor = UITheme.Instance.btnPrimary;
                if (GUI.Button(new Rect(bx, rect.y + 14f, 176f, 44f), $"치료 {cost} {curLabel}", btnStyle))
                    TryHeal(pid, cost);
                GUI.backgroundColor = new Color(0.55f, 0.35f, 0.85f);
                if (GUI.Button(new Rect(bx, rect.y + 62f, 176f, 42f), $"젬 {FullHealGems} 즉시", btnStyle))
                    TryGemHeal(pid);
                GUI.backgroundColor = Color.white;
            }
        }

        private int HealCost(int lostHp, PlayerInsectData pid)
        {
            int hpCost = Mathf.CeilToInt(lostHp * (payWithCoins ? CoinPerHp : CandyPerHp));
            int statusCost = 0;
            if (pid.isPoisoned) statusCost += CurePoisonCoin;
            if (pid.isParalyzed) statusCost += CureParalysisCoin;
            return Mathf.Max(1, hpCost + statusCost);
        }

        private void TryHeal(PlayerInsectData pid, int cost)
        {
            bool paid = payWithCoins
                ? (wallet != null && wallet.SpendCoins(cost))
                : (candyInventory != null && candyInventory.SpendCandy(cost));
            if (!paid) { Feedback("재화가 부족합니다"); return; }

            int maxHp = MaxHpOf(pid);
            collection.HealInsect(pid, maxHp);   // 전액 회복
            collection.CurePoison(pid);
            collection.CureParalysis(pid);
            Feedback($"{DisplayName(pid)} 치료 완료!");
        }

        // 대상지정 치료 아이템 적용 — 인벤 소비 후 HP 회복·상태 해제. 소진되면 모드 종료.
        private void ApplyTreatmentItem(PlayerInsectData pid)
        {
            if (pendingItem == null || pendingInv == null) return;
            if (!pendingInv.UseItem(pendingItem.itemId, 1)) { Feedback("아이템이 없습니다"); pendingItem = null; pendingInv = null; return; }

            if (pendingItem.healAmount > 0)
                collection.HealInsect(pid, pendingItem.healAmount);   // 9999=전액(상한 클램프)
            if (pendingItem.curePoison) collection.CurePoison(pid);
            if (pendingItem.cureParalysis) collection.CureParalysis(pid);
            Feedback($"{DisplayName(pid)}에게 {pendingItem.displayName} 사용!");

            // 같은 아이템이 더 있으면 모드 유지(연속 사용), 없으면 종료.
            if (pendingInv.GetCount(pendingItem.itemId) <= 0) { pendingItem = null; pendingInv = null; }
            ownedDirty = true;
        }

        private void TryGemHeal(PlayerInsectData pid)
        {
            if (wallet == null || !wallet.SpendGems(FullHealGems)) { Feedback("젬이 부족합니다"); return; }
            collection.FullHeal(pid);
            Feedback($"{DisplayName(pid)} 전액+상태 즉시치료!");
        }

        private string DisplayName(PlayerInsectData pid)
        {
            InsectData data = database != null ? database.GetById(pid.insectId) : null;
            return data != null ? data.displayName : pid.insectId;
        }

        private void Feedback(string msg) { feedback = msg; feedbackTimer = 2f; }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            titleStyle = Label(36, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            closeStyle = new GUIStyle(GUI.skin.button) { fontSize = 30, fontStyle = FontStyle.Bold };
            rowNameStyle = Label(26, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            rowInfoStyle = Label(20, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.6f, 0.4f));
            btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
            toggleStyle = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold };
            feedbackStyle = Label(26, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.5f, 1f, 0.6f));
            hintStyle = Label(19, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.7f, 0.72f, 0.8f));
            hpTextStyle = Label(15, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        }

        private static GUIStyle Label(int size, FontStyle fs, TextAnchor anchor, Color col)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, alignment = anchor, wordWrap = false };
            s.normal.textColor = col;
            return s;
        }
    }
}
