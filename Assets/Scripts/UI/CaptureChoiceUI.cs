using InsectGame.Battle;
using InsectGame.Capture;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.UI
{
    public class CaptureChoiceUI : MonoBehaviour, IModalUI
    {
        [SerializeField] private CaptureMinigameController minigame;
        [SerializeField] private InsectBattleController battleController;
        [SerializeField] private InsectBattleUIController battleUi;
        [SerializeField] private BattleTeamManager teamManager;
        [SerializeField] private PlayerInsectCollection collection;
        [SerializeField] private CaptureProximityTrigger proximityTrigger;
        [SerializeField] private CaptureController captureController;
        [SerializeField] private Dex.DexController dexController;
        [SerializeField] private TrainingManager trainingManager;
        [SerializeField] private PlayerItemInventory itemInventory;
        [SerializeField] private RaidBattleController raidController;
        [SerializeField] private PlayerMovement playerMovement;

        private CaptureItemData[] captureItems;

        private bool isOpen;
        public bool IsOpen => isOpen;
        public void CloseModal() { Hide(); }
        private InsectEntity targetInsect;
#pragma warning disable 0414
        private int selectedTeamSlot = -1;
#pragma warning restore 0414
        private bool showTeamSelect;
        private bool showItemSelect;

        public bool IsChoiceOpen => isOpen;

        public void SetCaptureItems(CaptureItemData[] items)
        {
            captureItems = items;
        }

        public void ShowChoice(InsectEntity target)
        {
            if (target == null || target.Data == null) return;
            targetInsect = target;
            target.SetEngaged(true); // 포획 상호작용 중 — 곤충 도주 방지
            isOpen = true;
            selectedTeamSlot = -1;
            showTeamSelect = false;
            showItemSelect = false;
            ModalUIRegistry.Register(this);
            if (playerMovement != null) playerMovement.SetFrozen(true);
        }

        public void Hide()
        {
            isOpen = false;
            if (targetInsect != null) targetInsect.SetEngaged(false); // 포획 취소 — 곤충 정상 행동 복귀
            targetInsect = null;
            showTeamSelect = false;
            showItemSelect = false;
            ModalUIRegistry.Unregister(this);
            if (playerMovement != null) playerMovement.SetFrozen(false);
        }

        private void OnDisable() { ModalUIRegistry.Unregister(this); }

        private void Update()
        {
            if (!isOpen) return;

            if (targetInsect == null || !targetInsect.gameObject.activeInHierarchy)
            {
                Hide();
                return;
            }

            HandleInputUpdate();
        }

        private void HandleInput(KeyCode key)
        {
            if (!isOpen) return;

            if (showTeamSelect)
            {
                if (key == KeyCode.Escape || key == KeyCode.Backspace)
                    showTeamSelect = false;
            }
            else if (showItemSelect)
            {
                if (key == KeyCode.Escape || key == KeyCode.Backspace)
                    showItemSelect = false;
            }
            else
            {
                bool isRaid = IsRaidTarget();
                if (!isRaid && (key == KeyCode.E || key == KeyCode.Alpha1))
                {
                    if (HasAnyCaptureItem())
                        showItemSelect = true;
                }
                if (!isRaid && (key == KeyCode.B || key == KeyCode.Alpha2))
                {
                    bool hasTeam = teamManager != null && teamManager.HasAnyInsect();
                    if (hasTeam)
                        showTeamSelect = true;
                }
                if (isRaid && (key == KeyCode.R || key == KeyCode.Alpha1 || key == KeyCode.Alpha3))
                {
                    bool hasFullTeam = teamManager != null && teamManager.FilledSlots >= 5;
                    if (hasFullTeam)
                        StartRaidBattle();
                }
                if (key == KeyCode.Escape)
                    Hide();
            }
        }

        private void HandleInputUpdate()
        {
            KeyCode[] keys = { KeyCode.E, KeyCode.B, KeyCode.R, KeyCode.Escape, KeyCode.Backspace,
                               KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };
            foreach (var k in keys)
            {
                if (Input.GetKeyDown(k))
                    HandleInput(k);
            }
        }

        private void OnGUI()
        {
            if (!isOpen || targetInsect == null) return;

            Event evt = Event.current;
            if (evt != null && evt.type == EventType.KeyDown && evt.keyCode != KeyCode.None)
            {
                HandleInput(evt.keyCode);
                evt.Use();
            }

            if (showTeamSelect)
                DrawTeamSelect();
            else if (showItemSelect)
                DrawItemSelect();
            else
                DrawChoice();
        }

        private bool IsRaidTarget()
        {
            return targetInsect != null && targetInsect.Data != null &&
                (targetInsect.Data.rarity == InsectRarity.Epic || targetInsect.Data.rarity == InsectRarity.Legendary);
        }

        private void DrawChoice()
        {
            if (targetInsect == null || targetInsect.Data == null) return;

            bool isRaid = IsRaidTarget();
            float panelW = isRaid ? 760f : 760f;
            float panelH = isRaid ? 560f : 480f;
            float px = (Screen.width - panelW) / 2f;
            float py = (Screen.height - panelH) / 2f;

            GUI.color = new Color(0.04f, 0.06f, 0.1f, 0.95f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);

            Color rarityCol = UITheme.Instance.GetInsectRarityColor(targetInsect.Data.rarity);
            GUI.color = rarityCol;
            GUI.DrawTexture(new Rect(px, py, panelW, 4), Texture2D.whiteTexture);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = Color.white;
            GUI.color = Color.white;

            string titleText = isRaid ? "레이드 보스 발견!" : "어떻게 포획할까요?";
            GUI.Label(new Rect(px, py + 20, panelW, 48), titleText, titleStyle);

            CapturePopupUI.DrawTypedInsectPortrait(px + panelW / 2f, py + 120, targetInsect.Data.insectId, targetInsect.Data.rarity, 1f);

            GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            nameStyle.normal.textColor = rarityCol;
            GUI.Label(new Rect(px, py + 160, panelW, 48),
                $"{targetInsect.Data.displayName} Lv.{targetInsect.Level}", nameStyle);

            if (isRaid)
            {
                GUIStyle raidHint = new GUIStyle(GUI.skin.label)
                { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                raidHint.normal.textColor = new Color(1f, 0.4f, 0.15f);
                GUI.Label(new Rect(px, py + 210, panelW, 36),
                    "RAID BOSS - 레이드로만 포획 가능!", raidHint);

                GUIStyle raidDesc = new GUIStyle(GUI.skin.label)
                { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                raidDesc.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                GUI.Label(new Rect(px, py + 244, panelW, 30),
                    "5마리 팀 전체가 협력하여 보스에 도전합니다", raidDesc);
            }

            GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
            { fontSize = 32, fontStyle = FontStyle.Bold };

            if (isRaid)
            {
                float raidBtnW = 340f;
                float raidBtnH = 90f;
                float raidBtnX = px + (panelW - raidBtnW) / 2f;
                float raidBtnY = py + 290;

                bool hasFullTeam = teamManager != null && teamManager.FilledSlots >= 5;
                GUI.backgroundColor = hasFullTeam ? new Color(0.7f, 0.2f, 0.05f) : new Color(0.3f, 0.3f, 0.3f);
                GUI.enabled = hasFullTeam;
                if (GUI.Button(new Rect(raidBtnX, raidBtnY, raidBtnW, raidBtnH), "레이드 시작 [R]", btnStyle))
                {
                    StartRaidBattle();
                }
                GUI.enabled = true;

                if (!hasFullTeam)
                {
                    GUIStyle raidReq = new GUIStyle(GUI.skin.label)
                    { fontSize = 26, alignment = TextAnchor.MiddleCenter };
                    raidReq.normal.textColor = new Color(1f, 0.4f, 0.15f);
                    int filled = teamManager != null ? teamManager.FilledSlots : 0;
                    GUI.Label(new Rect(raidBtnX, raidBtnY + raidBtnH + 8, raidBtnW, 34),
                        $"팀 편성 필요 ({filled}/5)  T키로 편성", raidReq);
                }
            }
            else
            {
                float btnY = py + 230;
                float btnW = 280f;
                float btnH = 80f;
                float gap = 24f;
                float leftX = px + panelW / 2f - btnW - gap / 2f;

                bool hasAnyNet = HasAnyCaptureItem();
                GUI.backgroundColor = hasAnyNet ? new Color(0.2f, 0.5f, 0.3f) : new Color(0.3f, 0.3f, 0.3f);
                GUI.enabled = hasAnyNet;
                if (GUI.Button(new Rect(leftX, btnY, btnW, btnH), "미니게임 [E]", btnStyle))
                {
                    showItemSelect = true;
                }
                GUI.enabled = true;

                if (!hasAnyNet)
                {
                    GUIStyle noNet = new GUIStyle(GUI.skin.label)
                    { fontSize = 24, alignment = TextAnchor.MiddleCenter };
                    noNet.normal.textColor = new Color(1f, 0.4f, 0.3f);
                    GUI.Label(new Rect(leftX, btnY + btnH + 6, btnW, 30),
                        "포획 아이템 없음!", noNet);
                }

                bool hasTeam = teamManager != null && teamManager.HasAnyInsect();
                GUI.backgroundColor = hasTeam ? new Color(0.5f, 0.2f, 0.2f) : new Color(0.3f, 0.3f, 0.3f);
                GUI.enabled = hasTeam;
                if (GUI.Button(new Rect(leftX + btnW + gap, btnY, btnW, btnH), "배틀 [B]", btnStyle))
                {
                    showTeamSelect = true;
                }
                GUI.enabled = true;

                if (!hasTeam)
                {
                    GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
                    { fontSize = 24, alignment = TextAnchor.MiddleCenter };
                    hintStyle.normal.textColor = new Color(0.5f, 0.4f, 0.3f);
                    GUI.Label(new Rect(leftX + btnW + gap, btnY + btnH + 6, btnW, 30),
                        "T키로 팀 편성", hintStyle);
                }
            }

            GUI.backgroundColor = new Color(0.3f, 0.3f, 0.35f);
            GUIStyle cancelStyle = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(px + panelW / 2f - 70, py + panelH - 70, 140, 50), "취소 [ESC]", cancelStyle))
                Hide();
            GUI.backgroundColor = Color.white;
        }

        private void DrawItemSelect()
        {
            if (captureItems == null || captureItems.Length == 0) { showItemSelect = false; return; }

            float panelW = 800f;
            float panelH = 120 + captureItems.Length * 160;
            float px = (Screen.width - panelW) / 2f;
            float py = (Screen.height - panelH) / 2f;

            GUI.color = new Color(0.04f, 0.06f, 0.1f, 0.95f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.8f, 0.4f);
            GUI.DrawTexture(new Rect(px, py, panelW, 4), Texture2D.whiteTexture);

            GUIStyle title = new GUIStyle(GUI.skin.label)
            { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(0.3f, 1f, 0.5f);
            GUI.color = Color.white;
            GUI.Label(new Rect(px, py + 16, panelW, 48), "포획 아이템 선택", title);

            float startY = py + 76;

            for (int i = 0; i < captureItems.Length; i++)
            {
                CaptureItemData item = captureItems[i];
                int count = itemInventory != null ? itemInventory.GetCount(item.itemId) : 0;
                float cy = startY + i * 160;
                bool hasItem = count > 0;

                GUI.color = new Color(0.1f, 0.12f, 0.18f, 0.85f);
                GUI.DrawTexture(new Rect(px + 20, cy, panelW - 40, 148), Texture2D.whiteTexture);
                GUI.color = item.themeColor;
                GUI.DrawTexture(new Rect(px + 20, cy, 6, 148), Texture2D.whiteTexture);

                float diamondX = px + 56;
                float diamondY = cy + 74;
                float ds = 24f;
                GUI.DrawTexture(new Rect(diamondX - ds / 2, diamondY - ds, ds, ds * 2), Texture2D.whiteTexture);

                GUI.color = Color.white;
                GUIStyle ns = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold };
                ns.normal.textColor = hasItem ? item.themeColor : new Color(0.4f, 0.4f, 0.4f);
                GUI.Label(new Rect(px + 90, cy + 10, panelW - 280, 42), $"[{i + 1}] {item.displayName}", ns);

                GUIStyle ds2 = new GUIStyle(GUI.skin.label) { fontSize = 24 };
                ds2.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                GUI.Label(new Rect(px + 90, cy + 52, panelW - 280, 32), item.description, ds2);

                string difficulty;
                if (item.speedMultiplier <= 0.6f) difficulty = "매우 쉬움";
                else if (item.speedMultiplier <= 0.8f) difficulty = "쉬움";
                else difficulty = "보통";

                GUIStyle diffS = new GUIStyle(GUI.skin.label) { fontSize = 24 };
                diffS.normal.textColor = item.speedMultiplier <= 0.6f ? new Color(0.3f, 1f, 0.5f) :
                    item.speedMultiplier <= 0.8f ? new Color(0.9f, 0.8f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
                GUI.Label(new Rect(px + 90, cy + 90, panelW - 280, 32), $"난이도: {difficulty}", diffS);

                GUIStyle countS = new GUIStyle(GUI.skin.label)
                { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
                countS.normal.textColor = hasItem ? new Color(1f, 0.85f, 0.3f) : new Color(0.4f, 0.3f, 0.3f);
                GUI.Label(new Rect(px + panelW - 220, cy + 14, 80, 36), $"x{count}", countS);

                GUIStyle useBtn = new GUIStyle(GUI.skin.button) { fontSize = 28, fontStyle = FontStyle.Bold };
                GUI.backgroundColor = hasItem ? new Color(0.25f, 0.5f, 0.3f) : new Color(0.2f, 0.2f, 0.2f);
                GUI.enabled = hasItem;
                if (GUI.Button(new Rect(px + panelW - 160, cy + 56, 120, 56), "사용", useBtn))
                {
                    if (itemInventory != null && itemInventory.UseItem(item.itemId, 1))
                    {
                        InsectEntity savedTarget = targetInsect;
                        Hide();
                        if (minigame != null && savedTarget != null)
                            minigame.StartMinigame(savedTarget,
                                item.speedMultiplier, item.zoneSizeMultiplier,
                                item.timeLimitMultiplier, item.captureBonus);
                    }
                }
                GUI.enabled = true;
                GUI.backgroundColor = Color.white;
            }

            GUIStyle backBtn = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(px + panelW / 2f - 70, py + panelH - 60, 140, 46), "< 뒤로", backBtn))
                showItemSelect = false;
        }

        private void TryItemSelectByKey()
        {
            if (captureItems == null || itemInventory == null) return;
            int keyIndex = -1;
            if (Input.GetKeyDown(KeyCode.Alpha1)) keyIndex = 0;
            else if (Input.GetKeyDown(KeyCode.Alpha2)) keyIndex = 1;
            else if (Input.GetKeyDown(KeyCode.Alpha3)) keyIndex = 2;
            else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                for (int i = 0; i < captureItems.Length; i++)
                {
                    if (itemInventory.GetCount(captureItems[i].itemId) > 0) { keyIndex = i; break; }
                }
            }
            if (keyIndex < 0 || keyIndex >= captureItems.Length) return;

            CaptureItemData item = captureItems[keyIndex];
            if (itemInventory.GetCount(item.itemId) <= 0) return;
            if (!itemInventory.UseItem(item.itemId, 1)) return;

            InsectEntity savedTarget = targetInsect;
            Hide();
            if (minigame != null && savedTarget != null)
                minigame.StartMinigame(savedTarget,
                    item.speedMultiplier, item.zoneSizeMultiplier,
                    item.timeLimitMultiplier, item.captureBonus);
        }

        private bool HasAnyCaptureItem()
        {
            if (captureItems == null || itemInventory == null) return false;
            foreach (var item in captureItems)
                if (itemInventory.GetCount(item.itemId) > 0) return true;
            return false;
        }

        private void DrawTeamSelect()
        {
            float panelW = 820f;
            float panelH = 720f;
            float px = (Screen.width - panelW) / 2f;
            float py = (Screen.height - panelH) / 2f;

            GUI.color = new Color(0.04f, 0.06f, 0.1f, 0.95f);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 0.5f, 0.2f);
            GUI.DrawTexture(new Rect(px, py, panelW, 4), Texture2D.whiteTexture);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = new Color(1f, 0.8f, 0.3f);
            GUI.color = Color.white;
            GUI.Label(new Rect(px, py + 18, panelW, 44), "곤충을 선택하세요", titleStyle);

            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            subStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            GUI.Label(new Rect(px, py + 62, panelW, 36),
                $"vs {targetInsect.Data.displayName} Lv.{targetInsect.Level}", subStyle);

            float slotY = py + 110;
            float slotH = 100f;

            for (int i = 0; i < BattleTeamManager.MaxSlots; i++)
            {
                string instanceId = teamManager != null ? teamManager.GetSlot(i) : null;
                DrawTeamSlotChoice(px + 24, slotY + i * (slotH + 6), panelW - 48, slotH, i, instanceId);
            }

            GUIStyle backStyle = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(px + panelW / 2f - 70, py + panelH - 60, 140, 46), "< 뒤로", backStyle))
                showTeamSelect = false;
        }

        private void DrawTeamSlotChoice(float x, float y, float w, float h, int index, string instanceId)
        {
            bool empty = string.IsNullOrEmpty(instanceId);

            GUI.color = empty
                ? new Color(0.08f, 0.08f, 0.12f, 0.5f)
                : new Color(0.1f, 0.12f, 0.18f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

            GUIStyle numStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            numStyle.normal.textColor = new Color(0.3f, 0.3f, 0.35f);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, 56, h), $"{index + 1}", numStyle);

            if (empty)
            {
                GUIStyle emptyStyle = new GUIStyle(GUI.skin.label) { fontSize = 28 };
                emptyStyle.normal.textColor = new Color(0.3f, 0.3f, 0.35f);
                GUI.Label(new Rect(x + 64, y + h / 2f - 18, w - 70, 36), "비어있음", emptyStyle);
                return;
            }

            PlayerInsectData pid = collection != null ? collection.GetByInstanceId(instanceId) : null;
            InsectData data = pid != null && collection != null ? collection.GetInsectData(pid.insectId) : null;

            if (data != null)
            {
                Color rarityCol = UITheme.Instance.GetInsectRarityColor(data.rarity);
                GUI.color = rarityCol;
                GUI.DrawTexture(new Rect(x, y, 5, h), Texture2D.whiteTexture);

                CapturePopupUI.DrawTypedInsectPortrait(x + 80, y + h / 2f, data.insectId, data.rarity, 1f);

                GUIStyle ns = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
                ns.normal.textColor = rarityCol;
                GUI.color = Color.white;
                int lv = pid != null ? pid.level : 1;
                int cp = PlayerInsectCombatPower.Calculate(data, pid);
                GUI.Label(new Rect(x + 120, y + 12, w - 280, 38), GetOwnedDisplayName(pid, data), ns);

                GUIStyle info = new GUIStyle(GUI.skin.label) { fontSize = 24 };
                info.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                GUI.Label(new Rect(x + 120, y + 52, w - 280, 32), $"Lv.{lv}  |  CP {cp}", info);

                GUIStyle fightBtn = new GUIStyle(GUI.skin.button) { fontSize = 28, fontStyle = FontStyle.Bold };
                GUI.backgroundColor = new Color(0.6f, 0.2f, 0.15f);
                if (GUI.Button(new Rect(x + w - 150, y + h / 2f - 28, 130, 56), "출격!", fightBtn))
                {
                    StartBattleCapture(pid, data, lv);
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void StartBattleCapture(PlayerInsectData playerPid, InsectData playerInsect, int playerLevel)
        {
            InsectEntity savedTarget = targetInsect;
            Hide();
            if (battleController == null || savedTarget == null) return;

            InsectSkill[] equippedSkills = null;
            if (playerPid != null && collection != null)
                equippedSkills = collection.GetEquippedSkills(playerPid);

            battleController.StartBattle(playerInsect, playerLevel, savedTarget, equippedSkills: equippedSkills, playerPid: playerPid);
        }

        private void StartRaidBattle()
        {
            InsectEntity savedTarget = targetInsect;
            Hide();
            if (raidController == null || savedTarget == null || teamManager == null || collection == null) return;

            int count = 0;
            InsectData[] teamInsects = new InsectData[BattleTeamManager.MaxSlots];
            int[] teamLevels = new int[BattleTeamManager.MaxSlots];
            PlayerInsectData[] teamPids = new PlayerInsectData[BattleTeamManager.MaxSlots];
            InsectSkill[][] teamSkills = new InsectSkill[BattleTeamManager.MaxSlots][];

            for (int i = 0; i < BattleTeamManager.MaxSlots; i++)
            {
                string instanceId = teamManager.GetSlot(i);
                if (string.IsNullOrEmpty(instanceId)) continue;

                PlayerInsectData pid = collection.GetByInstanceId(instanceId);
                InsectData data = pid != null ? collection.GetInsectData(pid.insectId) : null;
                if (data == null) continue;

                teamInsects[i] = data;
                teamLevels[i] = pid != null ? pid.level : 1;
                teamPids[i] = pid;

                teamSkills[i] = pid != null && collection != null ? collection.GetEquippedSkills(pid) : data.skills;

                count++;
            }

            if (count < BattleTeamManager.MaxSlots) return;

            raidController.StartRaid(savedTarget, teamInsects, teamLevels, teamPids, teamSkills);
        }

        private static string GetOwnedDisplayName(PlayerInsectData pid, InsectData data)
        {
            string baseName = data != null ? data.displayName : (pid != null ? pid.insectId : "Unknown");
            string shortId = pid == null || string.IsNullOrEmpty(pid.instanceId)
                ? "----"
                : pid.instanceId.Substring(0, Mathf.Min(6, pid.instanceId.Length)).ToUpperInvariant();
            return $"{baseName} #{shortId}";
        }

        public void AutoWire(
            CaptureMinigameController mg,
            InsectBattleController bc,
            InsectBattleUIController bui,
            BattleTeamManager tm,
            PlayerInsectCollection col,
            CaptureProximityTrigger prox,
            CaptureController cc,
            Dex.DexController dex,
            TrainingManager trm = null,
            PlayerItemInventory items = null,
            RaidBattleController raid = null)
        {
            if (minigame == null) minigame = mg;
            if (battleController == null) battleController = bc;
            if (battleUi == null) battleUi = bui;
            if (teamManager == null) teamManager = tm;
            if (collection == null) collection = col;
            if (proximityTrigger == null) proximityTrigger = prox;
            if (captureController == null) captureController = cc;
            if (dexController == null) dexController = dex;
            if (trainingManager == null) trainingManager = trm;
            if (itemInventory == null) itemInventory = items;
            if (raidController == null) raidController = raid;
        }

        public void AutoWire(PlayerMovement pm)
        {
            if (playerMovement == null) playerMovement = pm;
        }
    }
}
