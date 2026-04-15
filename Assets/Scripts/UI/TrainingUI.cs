using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    public class TrainingUI : MonoBehaviour
    {
        [SerializeField] private TrainingManager trainingManager;
        [SerializeField] private PlayerInsectCollection collection;
        [SerializeField] private PlayerCandyInventory candyInventory;

        private bool isOpen;

        private enum Page { InsectSelect, MethodSelect, SkillLearn, SkillEquip, SkillReplace }
        private Page page;
        private string selectedInstanceId;
        private int selectedMethodIndex = -1;
        private string pendingNewSkillId;
        private Vector2 scrollPos;
        private string feedbackMsg;
        private float feedbackTimer;

        public bool IsOpen => isOpen;
        public void Toggle()
        {
            isOpen = !isOpen;
            if (isOpen) { page = Page.InsectSelect; selectedInstanceId = null; selectedMethodIndex = -1; }
        }

        private void Update()
        {
            if (feedbackTimer > 0) feedbackTimer -= Time.deltaTime;
        }

        private void OnGUI()
        {
            if (!isOpen) return;

            switch (page)
            {
                case Page.InsectSelect: DrawInsectSelect(); break;
                case Page.MethodSelect: DrawMethodSelect(); break;
                case Page.SkillLearn: DrawSkillLearn(); break;
                case Page.SkillEquip: DrawSkillEquip(); break;
                case Page.SkillReplace: DrawSkillReplace(); break;
            }

            if (feedbackTimer > 0)
                DrawFeedback();
        }

        private void DrawPanel(string title, out float px, out float py, out float pw, out float ph)
        {
            pw = 1000f; ph = 900f;
            px = (Screen.width - pw) / 2f;
            py = (Screen.height - ph) / 2f;

            GUI.color = new Color(0.04f, 0.06f, 0.1f, 0.96f);
            GUI.DrawTexture(new Rect(px, py, pw, ph), Texture2D.whiteTexture);
            GUI.color = new Color(0.15f, 0.2f, 0.3f);
            GUI.DrawTexture(new Rect(px, py, pw, 70), Texture2D.whiteTexture);
            GUI.color = new Color(0.9f, 0.6f, 0.2f);
            GUI.DrawTexture(new Rect(px, py + 70, pw, 5), Texture2D.whiteTexture);

            GUIStyle ts = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            ts.normal.textColor = new Color(1f, 0.85f, 0.4f);
            GUI.color = Color.white;
            GUI.Label(new Rect(px + 120, py + 10, pw - 240, 50), title, ts);

            GUIStyle cls = new GUIStyle(GUI.skin.button) { fontSize = 28, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(px + pw - 60, py + 12, 50, 46), "X", cls))
                isOpen = false;
        }

        private bool DrawBackButton(float px, float py)
        {
            GUIStyle bs = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };
            return GUI.Button(new Rect(px + 12, py + 12, 110, 46), "< Back", bs);
        }

        private void DrawInsectSelect()
        {
            DrawPanel("TRAINING CENTER", out float px, out float py, out float pw, out float ph);

            if (collection == null) return;
            List<PlayerInsectData> owned = collection.GetAllOwned();

            GUIStyle sub = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            sub.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            int candy = candyInventory != null ? candyInventory.Candies : 0;
            GUI.Label(new Rect(px, py + 76, pw, 36), $"훈련할 곤충을 선택하세요  |  캔디: {candy}", sub);

            float listY = py + 120;
            float listH = ph - 130;
            float itemH = 108f;
            Rect area = new Rect(px + 10, listY, pw - 20, listH);
            Rect view = new Rect(0, 0, area.width - 20, owned.Count * itemH);
            scrollPos = GUI.BeginScrollView(area, scrollPos, view);

            for (int i = 0; i < owned.Count; i++)
            {
                PlayerInsectData pid = owned[i];
                InsectData data = collection.GetInsectData(pid.insectId);
                Rect r = new Rect(0, i * itemH, view.width, itemH - 3);

                Color rc = data != null ? UITheme.Instance.GetInsectRarityColor(data.rarity) : Color.gray;
                GUI.color = new Color(0.1f, 0.12f, 0.18f, 0.85f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = rc;
                GUI.DrawTexture(new Rect(r.x, r.y, 6, r.height), Texture2D.whiteTexture);

                if (data != null)
                    CapturePopupUI.DrawTypedInsectPortrait(r.x + 60, r.y + r.height / 2f, data.insectId, data.rarity, 1f);

                GUIStyle ns = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
                ns.normal.textColor = rc;
                GUI.color = Color.white;
                string name = data != null
                    ? $"{data.displayName} #{GetShortInstanceId(pid)}"
                    : $"{pid.insectId} #{GetShortInstanceId(pid)}";
                GUI.Label(new Rect(r.x + 100, r.y + 10, r.width - 250, 36), name, ns);

                GUIStyle info = new GUIStyle(GUI.skin.label) { fontSize = 24 };
                info.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                int learned = pid.learnedSkillIds != null ? pid.learnedSkillIds.Count : 0;
                GUI.Label(new Rect(r.x + 100, r.y + 46, r.width - 250, 30),
                    $"Lv.{pid.level}  |  스킬: {learned}/{PlayerInsectData.MaxLearnedSkills}  |  장착: {pid.EquippedCount()}/{PlayerInsectData.MaxEquipSlots}", info);

                GUIStyle btn = new GUIStyle(GUI.skin.button) { fontSize = 26 };
                GUI.backgroundColor = new Color(0.3f, 0.45f, 0.25f);
                if (GUI.Button(new Rect(r.x + r.width - 140, r.y + r.height / 2f - 24, 120, 48), "훈련", btn))
                {
                    selectedInstanceId = pid.instanceId;
                    page = Page.MethodSelect;
                    scrollPos = Vector2.zero;
                }
                GUI.backgroundColor = Color.white;
            }
            GUI.EndScrollView();
        }

        private void DrawMethodSelect()
        {
            DrawPanel("CHOOSE TRAINING", out float px, out float py, out float pw, out float ph);
            if (DrawBackButton(px, py)) { page = Page.InsectSelect; return; }

            PlayerInsectData pid = GetPid();
            InsectData data = pid != null ? collection.GetInsectData(pid.insectId) : null;
            if (pid == null) { page = Page.InsectSelect; return; }

            Color rc = data != null ? UITheme.Instance.GetInsectRarityColor(data.rarity) : Color.gray;
            GUIStyle nameS = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            nameS.normal.textColor = rc;
            GUI.Label(new Rect(px, py + 76, pw, 36), data != null ? $"{data.displayName} Lv.{pid.level}" : pid.insectId, nameS);

            GUIStyle equipBtn = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
            GUI.backgroundColor = new Color(0.2f, 0.35f, 0.55f);
            if (GUI.Button(new Rect(px + pw - 210, py + 76, 190, 44), "스킬 장착", equipBtn))
            {
                page = Page.SkillEquip;
                scrollPos = Vector2.zero;
            }
            GUI.backgroundColor = Color.white;

            if (trainingManager == null || trainingManager.Methods == null) return;

            float startY = py + 130;
            float cardH = 150f;
            TrainingMethod[] methods = trainingManager.Methods;

            for (int i = 0; i < methods.Length; i++)
            {
                var m = methods[i];
                float cy = startY + i * (cardH + 6);
                bool canTrain = trainingManager.CanTrain(m, pid);
                bool levelOk = pid.level >= m.requiredLevel;

                GUI.color = new Color(m.themeColor.r * 0.15f, m.themeColor.g * 0.15f, m.themeColor.b * 0.15f, 0.8f);
                GUI.DrawTexture(new Rect(px + 15, cy, pw - 30, cardH), Texture2D.whiteTexture);
                GUI.color = levelOk ? m.themeColor : new Color(0.3f, 0.3f, 0.3f);
                GUI.DrawTexture(new Rect(px + 15, cy, 6, cardH), Texture2D.whiteTexture);

                GUIStyle mName = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
                mName.normal.textColor = levelOk ? m.themeColor : new Color(0.4f, 0.4f, 0.4f);
                GUI.color = Color.white;
                GUI.Label(new Rect(px + 38, cy + 14, pw - 250, 36), m.displayName, mName);

                GUIStyle mDesc = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true };
                mDesc.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                GUI.Label(new Rect(px + 38, cy + 50, pw - 260, 60), m.description, mDesc);

                GUIStyle costS = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleRight };
                costS.normal.textColor = canTrain ? new Color(1f, 0.5f, 0.8f) : new Color(0.5f, 0.3f, 0.3f);
                GUI.Label(new Rect(px + pw - 250, cy + 14, 170, 30), $"비용: {m.candyCost}", costS);

                if (!levelOk)
                {
                    GUIStyle lockS = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleRight };
                    lockS.normal.textColor = new Color(1f, 0.4f, 0.3f);
                    GUI.Label(new Rect(px + pw - 250, cy + 50, 170, 30), $"Lv.{m.requiredLevel} 필요", lockS);
                }

                int skillCount = m.skillPool != null ? m.skillPool.Length : 0;
                GUIStyle countS = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleRight };
                countS.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
                GUI.Label(new Rect(px + pw - 250, cy + cardH - 40, 170, 28), $"스킬 {skillCount}개", countS);

                GUIStyle trainBtn = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
                GUI.backgroundColor = canTrain ? new Color(0.3f, 0.5f, 0.25f) : new Color(0.2f, 0.2f, 0.2f);
                GUI.enabled = canTrain;
                if (GUI.Button(new Rect(px + pw - 170, cy + cardH / 2f - 22, 130, 46), "시작", trainBtn))
                {
                    selectedMethodIndex = i;
                    page = Page.SkillLearn;
                    scrollPos = Vector2.zero;
                }
                GUI.enabled = true;
                GUI.backgroundColor = Color.white;
            }
        }

        private void DrawSkillLearn()
        {
            DrawPanel("LEARN SKILLS", out float px, out float py, out float pw, out float ph);
            if (DrawBackButton(px, py)) { page = Page.MethodSelect; return; }

            PlayerInsectData pid = GetPid();
            if (pid == null || trainingManager == null || selectedMethodIndex < 0) { page = Page.MethodSelect; return; }

            TrainingMethod method = trainingManager.Methods[selectedMethodIndex];
            InsectSkill[] skills = trainingManager.GetAvailableSkills(method, pid);

            GUIStyle header = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            header.normal.textColor = method.themeColor;
            GUI.Label(new Rect(px, py + 76, pw, 34), $"{method.displayName}  |  Cost: {method.candyCost} Candy", header);

            float listY = py + 120;
            float listH = ph - 130;
            float itemH = 130f;
            Rect area = new Rect(px + 10, listY, pw - 20, listH);
            Rect view = new Rect(0, 0, area.width - 20, skills.Length * itemH);
            scrollPos = GUI.BeginScrollView(area, scrollPos, view);

            for (int i = 0; i < skills.Length; i++)
            {
                InsectSkill skill = skills[i];
                if (skill == null) continue;
                Rect r = new Rect(0, i * itemH, view.width, itemH - 4);
                bool learned = pid.HasLearnedSkill(skill.skillId);

                DrawSkillCard(r, skill, learned, method.themeColor);

                if (!learned)
                {
                    bool canAfford = trainingManager.CanTrain(method, pid);
                    bool isFull = pid.IsSkillsFull();
                    string btnLabel = isFull ? "Replace" : "Learn";
                    GUIStyle learnBtn = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
                    GUI.backgroundColor = canAfford ? (isFull ? new Color(0.5f, 0.35f, 0.2f) : new Color(0.25f, 0.5f, 0.3f)) : new Color(0.2f, 0.2f, 0.2f);
                    GUI.enabled = canAfford;
                    if (GUI.Button(new Rect(r.x + r.width - 130, r.y + r.height / 2f - 23, 110, 46), btnLabel, learnBtn))
                    {
                        if (isFull)
                        {
                            pendingNewSkillId = skill.skillId;
                            page = Page.SkillReplace;
                            scrollPos = Vector2.zero;
                        }
                        else if (trainingManager.TrainSkill(method, pid, skill.skillId))
                        {
                            feedbackMsg = $"{skill.displayName} learned!";
                            feedbackTimer = 2f;
                        }
                    }
                    GUI.enabled = true;
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUIStyle tag = new GUIStyle(GUI.skin.label)
                    { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
                    tag.normal.textColor = new Color(0.3f, 0.9f, 0.5f);
                    GUI.Label(new Rect(r.x + r.width - 150, r.y + r.height / 2f - 17, 140, 34), "습득완료", tag);
                }
            }
            GUI.EndScrollView();
        }

        private void DrawSkillEquip()
        {
            DrawPanel("EQUIP SKILLS", out float px, out float py, out float pw, out float ph);
            if (DrawBackButton(px, py)) { page = Page.MethodSelect; return; }

            PlayerInsectData pid = GetPid();
            if (pid == null) { page = Page.InsectSelect; return; }

            InsectData data = collection.GetInsectData(pid.insectId);
            Color rc = data != null ? UITheme.Instance.GetInsectRarityColor(data.rarity) : Color.gray;

            GUIStyle nameS = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            nameS.normal.textColor = rc;
            GUI.Label(new Rect(px, py + 76, pw, 34),
                $"{(data != null ? data.displayName : pid.insectId)} - Skill Slots", nameS);

            float slotY = py + 120;
            float slotH = 90f;

            for (int i = 0; i < PlayerInsectData.MaxEquipSlots; i++)
            {
                float sy = slotY + i * (slotH + 4);
                string eqId = pid.GetEquippedSkill(i);
                InsectSkill eqSkill = eqId != null ? trainingManager.GetSkill(eqId) : null;

                GUI.color = new Color(0.1f, 0.12f, 0.18f, 0.85f);
                GUI.DrawTexture(new Rect(px + 15, sy, pw - 30, slotH), Texture2D.whiteTexture);

                GUIStyle numS = new GUIStyle(GUI.skin.label)
                { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                numS.normal.textColor = new Color(0.3f, 0.3f, 0.4f);
                GUI.color = Color.white;
                GUI.Label(new Rect(px + 15, sy, 52, slotH), $"{i + 1}", numS);

                if (eqSkill != null)
                {
                    Color sc = GetSkillColor(eqSkill.effectType);
                    GUI.color = sc;
                    GUI.DrawTexture(new Rect(px + 15, sy, 5, slotH), Texture2D.whiteTexture);
                    GUI.color = Color.white;

                    GUIStyle sn = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
                    sn.normal.textColor = sc;
                    GUI.Label(new Rect(px + 80, sy + 10, pw - 290, 32), eqSkill.displayName, sn);

                    GUIStyle si = new GUIStyle(GUI.skin.label) { fontSize = 24 };
                    si.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                    string typeStr = eqSkill.effectType == SkillEffectType.Damage ? $"DMG {eqSkill.power}" :
                                     eqSkill.effectType == SkillEffectType.BuffAttack ? $"ATK UP +{eqSkill.effectValue * 100:0}%" :
                                     $"ATK DOWN -{eqSkill.effectValue * 100:0}%";
                    GUI.Label(new Rect(px + 80, sy + 44, pw - 290, 30), $"{typeStr}  |  CD: {eqSkill.cooldownTurns}t", si);

                    GUIStyle remBtn = new GUIStyle(GUI.skin.button) { fontSize = 24 };
                    GUI.backgroundColor = new Color(0.4f, 0.2f, 0.2f);
                    if (GUI.Button(new Rect(px + pw - 150, sy + 10, 110, 38), "해제", remBtn))
                    {
                        pid.EquipSkill(null, i);
                        collection.ForceSave();
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUIStyle emptyS = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Italic };
                    emptyS.normal.textColor = new Color(0.35f, 0.35f, 0.4f);
                    GUI.Label(new Rect(px + 80, sy + slotH / 2f - 17, pw - 170, 34), "빈 슬롯", emptyS);
                }
            }

            float learnedY = slotY + PlayerInsectData.MaxEquipSlots * (slotH + 4) + 10;
            GUIStyle lh = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            lh.normal.textColor = new Color(0.7f, 0.75f, 1f);
            GUI.Label(new Rect(px + 15, learnedY, pw - 30, 34), "Learned Skills:", lh);

            float listY2 = learnedY + 40;
            float listH2 = ph - (listY2 - py) - 10;
            float itemH2 = 68f;

            List<string> learned = pid.learnedSkillIds ?? new List<string>();
            Rect area = new Rect(px + 15, listY2, pw - 30, listH2);
            Rect viewR = new Rect(0, 0, area.width - 20, learned.Count * itemH2);
            scrollPos = GUI.BeginScrollView(area, scrollPos, viewR);

            for (int i = 0; i < learned.Count; i++)
            {
                InsectSkill sk = trainingManager.GetSkill(learned[i]);
                if (sk == null) continue;

                Rect r = new Rect(0, i * itemH2, viewR.width, itemH2 - 3);
                bool isEquipped = IsEquipped(pid, sk.skillId);

                GUI.color = new Color(0.08f, 0.1f, 0.15f, 0.8f);
                GUI.DrawTexture(r, Texture2D.whiteTexture);

                Color sc2 = GetSkillColor(sk.effectType);
                GUI.color = sc2;
                GUI.DrawTexture(new Rect(r.x, r.y, 5, r.height), Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUIStyle sn2 = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
                sn2.normal.textColor = isEquipped ? new Color(0.5f, 0.5f, 0.5f) : sc2;
                GUI.Label(new Rect(r.x + 14, r.y + 6, r.width - 180, 32), sk.displayName, sn2);

                GUIStyle si2 = new GUIStyle(GUI.skin.label) { fontSize = 22 };
                si2.normal.textColor = new Color(0.45f, 0.45f, 0.45f);
                GUI.Label(new Rect(r.x + 14, r.y + 38, r.width - 180, 26),
                    sk.effectType == SkillEffectType.Damage ? $"DMG {sk.power}" : sk.effectType.ToString(), si2);

                if (!isEquipped && pid.EquippedCount() < PlayerInsectData.MaxEquipSlots)
                {
                    GUIStyle eqBtn = new GUIStyle(GUI.skin.button) { fontSize = 22 };
                    GUI.backgroundColor = new Color(0.2f, 0.4f, 0.3f);
                    if (GUI.Button(new Rect(r.x + r.width - 100, r.y + r.height / 2f - 18, 88, 36), "Equip", eqBtn))
                    {
                        for (int s = 0; s < PlayerInsectData.MaxEquipSlots; s++)
                        {
                            if (pid.GetEquippedSkill(s) == null)
                            {
                                pid.EquipSkill(sk.skillId, s);
                                collection.ForceSave();
                                break;
                            }
                        }
                    }
                    GUI.backgroundColor = Color.white;
                }
                else if (isEquipped)
                {
                    GUIStyle eqTag = new GUIStyle(GUI.skin.label)
                    { fontSize = 22, alignment = TextAnchor.MiddleRight };
                    eqTag.normal.textColor = new Color(0.4f, 0.7f, 0.4f);
                    GUI.Label(new Rect(r.x + r.width - 130, r.y + r.height / 2f - 15, 120, 30), "장착중", eqTag);
                }
            }
            GUI.EndScrollView();
        }

        private void DrawSkillReplace()
        {
            DrawPanel("REPLACE SKILL", out float px, out float py, out float pw, out float ph);
            if (DrawBackButton(px, py)) { page = Page.SkillLearn; pendingNewSkillId = null; return; }

            PlayerInsectData pid = GetPid();
            if (pid == null || trainingManager == null || string.IsNullOrEmpty(pendingNewSkillId))
            { page = Page.SkillLearn; return; }

            InsectSkill newSkill = trainingManager.GetSkill(pendingNewSkillId);
            if (newSkill == null) { page = Page.SkillLearn; return; }

            GUIStyle header = new GUIStyle(GUI.skin.label)
            { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            header.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            GUI.Label(new Rect(px, py + 76, pw, 36), $"스킬이 가득 찼습니다 ({PlayerInsectData.MaxLearnedSkills}/{PlayerInsectData.MaxLearnedSkills})! 잊을 스킬을 선택하세요:", header);

            Color nc = GetSkillColor(newSkill.effectType);
            GUIStyle newS = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            newS.normal.textColor = nc;
            string newInfo = newSkill.effectType == SkillEffectType.Damage ? $"DMG {newSkill.power}" :
                             newSkill.effectType == SkillEffectType.BuffAttack ? $"ATK UP +{newSkill.effectValue * 100:0}%" :
                             $"ATK DOWN -{newSkill.effectValue * 100:0}%";
            GUI.Label(new Rect(px, py + 116, pw, 34), $"New: {newSkill.displayName}  ({newInfo})", newS);

            GUI.color = nc;
            GUI.DrawTexture(new Rect(px + 100, py + 154, pw - 200, 2), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float startY = py + 170;
            float cardH = 120f;
            System.Collections.Generic.List<string> learned = pid.learnedSkillIds ?? new System.Collections.Generic.List<string>();

            TrainingMethod method = selectedMethodIndex >= 0 ? trainingManager.Methods[selectedMethodIndex] : null;

            for (int i = 0; i < learned.Count; i++)
            {
                InsectSkill old = trainingManager.GetSkill(learned[i]);
                if (old == null) continue;

                float cy = startY + i * (cardH + 4);
                Color oc = GetSkillColor(old.effectType);

                GUI.color = new Color(0.1f, 0.12f, 0.18f, 0.85f);
                GUI.DrawTexture(new Rect(px + 15, cy, pw - 30, cardH), Texture2D.whiteTexture);
                GUI.color = oc;
                GUI.DrawTexture(new Rect(px + 15, cy, 6, cardH), Texture2D.whiteTexture);
                GUI.color = Color.white;

                GUIStyle sn = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
                sn.normal.textColor = oc;
                GUI.Label(new Rect(px + 38, cy + 10, pw - 260, 36), old.displayName, sn);

                GUIStyle si = new GUIStyle(GUI.skin.label) { fontSize = 24 };
                si.normal.textColor = new Color(0.55f, 0.55f, 0.55f);
                string oldInfo = old.effectType == SkillEffectType.Damage ? $"DMG {old.power}" :
                                 old.effectType == SkillEffectType.BuffAttack ? $"ATK UP +{old.effectValue * 100:0}%" :
                                 $"ATK DOWN -{old.effectValue * 100:0}%";
                GUI.Label(new Rect(px + 38, cy + 46, pw - 260, 30), $"{oldInfo}  |  CD: {old.cooldownTurns}t", si);

                GUIStyle forgetBtn = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
                GUI.backgroundColor = new Color(0.55f, 0.2f, 0.2f);
                if (GUI.Button(new Rect(px + pw - 190, cy + cardH / 2f - 24, 140, 48), "잊기", forgetBtn))
                {
                    if (method != null && trainingManager.TrainSkill(method, pid, pendingNewSkillId, old.skillId))
                    {
                        feedbackMsg = $"Forgot {old.displayName}, learned {newSkill.displayName}!";
                        feedbackTimer = 2.5f;
                        pendingNewSkillId = null;
                        page = Page.SkillLearn;
                        scrollPos = Vector2.zero;
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            GUIStyle cancelBtn = new GUIStyle(GUI.skin.button) { fontSize = 24 };
            GUI.backgroundColor = new Color(0.3f, 0.3f, 0.35f);
            if (GUI.Button(new Rect(px + pw / 2f - 90, py + ph - 60, 180, 48), "Cancel", cancelBtn))
            {
                pendingNewSkillId = null;
                page = Page.SkillLearn;
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawSkillCard(Rect r, InsectSkill skill, bool learned, Color accent)
        {
            GUI.color = learned ? new Color(0.08f, 0.1f, 0.14f, 0.7f) : new Color(0.1f, 0.12f, 0.18f, 0.85f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);

            Color sc = GetSkillColor(skill.effectType);
            GUI.color = sc;
            GUI.DrawTexture(new Rect(r.x, r.y, 6, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle ns = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            ns.normal.textColor = learned ? new Color(0.45f, 0.45f, 0.45f) : sc;
            GUI.Label(new Rect(r.x + 16, r.y + 10, r.width - 160, 36), skill.displayName, ns);

            GUIStyle descS = new GUIStyle(GUI.skin.label) { fontSize = 24, wordWrap = true };
            descS.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            string typeStr = skill.effectType == SkillEffectType.Damage ? $"데미지: {skill.power}" :
                             skill.effectType == SkillEffectType.BuffAttack ? $"ATK UP +{skill.effectValue * 100:0}% ({skill.effectDurationTurns}t)" :
                             $"ATK DOWN -{skill.effectValue * 100:0}% ({skill.effectDurationTurns}t)";
            GUI.Label(new Rect(r.x + 16, r.y + 46, r.width - 160, 30), typeStr, descS);

            GUIStyle cdS = new GUIStyle(GUI.skin.label) { fontSize = 22 };
            cdS.normal.textColor = new Color(0.45f, 0.45f, 0.5f);
            GUI.Label(new Rect(r.x + 16, r.y + 76, r.width - 160, 28), $"쿨다운: {skill.cooldownTurns}턴", cdS);
        }

        private void DrawFeedback()
        {
            float alpha = Mathf.Clamp01(feedbackTimer / 0.5f);
            GUIStyle s = new GUIStyle(GUI.skin.label)
            { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            s.normal.textColor = new Color(0.3f, 1f, 0.5f, alpha);
            GUI.Label(new Rect(0, Screen.height * 0.15f, Screen.width, 30), feedbackMsg, s);
        }

        private PlayerInsectData GetPid()
        {
            if (collection == null || string.IsNullOrEmpty(selectedInstanceId)) return null;
            return collection.GetByInstanceId(selectedInstanceId);
        }

        private bool IsEquipped(PlayerInsectData pid, string skillId)
        {
            for (int i = 0; i < PlayerInsectData.MaxEquipSlots; i++)
                if (pid.GetEquippedSkill(i) == skillId) return true;
            return false;
        }

        private Color GetSkillColor(SkillEffectType type)
        {
            switch (type)
            {
                case SkillEffectType.Damage: return new Color(0.9f, 0.35f, 0.3f);
                case SkillEffectType.BuffAttack: return new Color(0.3f, 0.8f, 0.4f);
                case SkillEffectType.DebuffAttack: return new Color(0.7f, 0.4f, 0.9f);
                default: return Color.gray;
            }
        }

        public void AutoWire(TrainingManager tm, PlayerInsectCollection col, PlayerCandyInventory candy)
        {
            if (trainingManager == null) trainingManager = tm;
            if (collection == null) collection = col;
            if (candyInventory == null) candyInventory = candy;
        }

        private static string GetShortInstanceId(PlayerInsectData data)
        {
            if (data == null || string.IsNullOrEmpty(data.instanceId))
            {
                return "----";
            }

            return data.instanceId.Substring(0, Mathf.Min(6, data.instanceId.Length)).ToUpperInvariant();
        }
    }
}
