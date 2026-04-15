using InsectGame.Capture;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.UI
{
    public class CapturePopupUI : MonoBehaviour
    {
        [SerializeField] private CaptureController captureController;
        [SerializeField] private PlayerInsectCollection insectCollection;

        private float popupTimer;
        private float popupDuration = 4.5f;
        private bool wasSuccess;
        private string insectName;
        private string insectId;
        private InsectRarity insectRarity;
        private int insectLevel;
        private int candyReward;
        private int expReward;
        private float animTime;
        private IVGrade capturedGrade;
        private int capturedIvHp, capturedIvAtk, capturedIvDef;
        private float capturedIvPct;
        private string capturedInstanceId;

        private struct Star
        {
            public float angle, speed, dist, size;
        }
        private Star[] stars;

        private void OnEnable()
        {
            if (captureController != null)
                captureController.CaptureResolved += OnCaptureResolved;
        }

        private void OnDisable()
        {
            if (captureController != null)
                captureController.CaptureResolved -= OnCaptureResolved;
        }

        private void OnCaptureResolved(InsectEntity target, bool success)
        {
            wasSuccess = success;
            popupTimer = popupDuration;
            animTime = 0f;

            if (target != null && target.Data != null)
            {
                insectName = target.Data.displayName;
                insectId = target.Data.insectId;
                insectRarity = target.Data.rarity;
                insectLevel = target.Level;
                candyReward = InsectRewardCalculator.GetCandyReward(target.Data);
                expReward = InsectRewardCalculator.GetExpReward(target.Data);

                if (success && insectCollection != null)
                {
                    PlayerInsectData pid = insectCollection.GetLatestOwnedBySpecies(target.Data.insectId);
                    if (pid != null)
                    {
                        capturedInstanceId = pid.instanceId;
                        capturedGrade = pid.Grade;
                        capturedIvHp = pid.ivHp;
                        capturedIvAtk = pid.ivAtk;
                        capturedIvDef = pid.ivDef;
                        capturedIvPct = pid.IVPercent;
                    }
                }
            }
            else
            {
                insectName = "???";
                insectId = "";
                capturedInstanceId = null;
                insectRarity = InsectRarity.Common;
                insectLevel = 1;
                candyReward = 0;
                expReward = 0;
            }

            if (success)
            {
                int starCount = 8 + (int)insectRarity * 5;
                stars = new Star[starCount];
                for (int i = 0; i < starCount; i++)
                {
                    stars[i] = new Star
                    {
                        angle = Random.Range(0f, 360f),
                        speed = Random.Range(40f, 120f),
                        dist = Random.Range(20f, 70f),
                        size = Random.Range(8f, 18f)
                    };
                }
            }
        }

        private void Update()
        {
            if (popupTimer > 0f)
            {
                popupTimer -= Time.deltaTime;
                animTime += Time.deltaTime;
            }
        }

        private void OnGUI()
        {
            if (popupTimer <= 0f) return;

            float alpha = popupTimer < 0.5f ? popupTimer / 0.5f : Mathf.Clamp01(animTime / 0.3f);

            if (wasSuccess)
                DrawSuccessPopup(alpha);
            else
                DrawFailPopup(alpha);
        }

        private void DrawSuccessPopup(float alpha)
        {
            float cx = Screen.width / 2f;
            float cy = Screen.height * 0.38f;
            float panelW = 560f;
            float panelH = 540f;
            float px = cx - panelW / 2f;
            float py = cy - panelH / 2f;

            float slideIn = Mathf.Clamp01(animTime / 0.25f);
            py += (1f - slideIn) * 30f;

            Color rarityCol = GetRarityColor(insectRarity);

            GUI.color = new Color(0.03f, 0.05f, 0.1f, 0.94f * alpha);
            GUI.DrawTexture(new Rect(px, py, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            Rect panelRect = new Rect(px, py, panelW, panelH);
            int rarityTier = (int)insectRarity;

            // Epic/Legendary 글로우
            if (rarityTier >= 3)
            {
                float glowIntensity = rarityTier >= 4 ? 0.8f : 0.5f;
                UIHelper.DrawRarityGlow(panelRect, rarityCol, glowIntensity * alpha, animTime);
            }

            UIHelper.DrawRarityBorder(panelRect, rarityTier, animTime);

            if (stars != null)
            {
                foreach (Star s in stars)
                {
                    float d = s.dist + animTime * s.speed;
                    float rad = s.angle * Mathf.Deg2Rad + animTime * 0.5f;
                    float sx = cx + Mathf.Cos(rad) * d;
                    float sy = cy - 30 + Mathf.Sin(rad) * d * 0.6f;
                    float starAlpha = Mathf.Clamp01(1f - d / 220f) * alpha;
                    GUI.color = new Color(rarityCol.r, rarityCol.g, rarityCol.b, starAlpha);
                    float sz = s.size * (1f + Mathf.Sin(animTime * 5f + s.angle) * 0.3f);
                    GUI.DrawTexture(new Rect(sx - sz / 2, sy - sz / 2, sz, sz), Texture2D.whiteTexture);
                }
            }

            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            headerStyle.normal.textColor = new Color(1, 1, 1, alpha);
            GUI.color = Color.white;
            GUI.Label(new Rect(px, py + 14, panelW, 36), "포획 성공!", headerStyle);

            DrawTypedInsectPortrait(cx, py + 105, insectId, insectRarity, alpha);

            GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            nameStyle.normal.textColor = new Color(rarityCol.r, rarityCol.g, rarityCol.b, alpha);
            GUI.Label(new Rect(px, py + 175, panelW, 40), insectName, nameStyle);

            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            subStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f, alpha);
            GUI.Label(new Rect(px, py + 216, panelW, 28),
                $"Lv.{insectLevel}  |  {insectRarity}", subStyle);

            Color gc = GetGradeColor(capturedGrade);
            float gradeBoxY = py + 256;

            GUI.color = new Color(0.1f, 0.12f, 0.18f, 0.8f * alpha);
            GUI.DrawTexture(new Rect(px + 30, gradeBoxY, panelW - 60, 120), Texture2D.whiteTexture);
            GUI.color = new Color(gc.r, gc.g, gc.b, 0.6f * alpha);
            GUI.DrawTexture(new Rect(px + 30, gradeBoxY, 5, 120), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle gradeTitle = new GUIStyle(GUI.skin.label)
            { fontSize = 19, alignment = TextAnchor.MiddleLeft };
            gradeTitle.normal.textColor = new Color(0.6f, 0.6f, 0.6f, alpha);
            GUI.Label(new Rect(px + 44, gradeBoxY + 6, 120, 22), "개체값 감정", gradeTitle);

            GUIStyle gradeLbl = new GUIStyle(GUI.skin.label)
            { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            gradeLbl.normal.textColor = new Color(gc.r, gc.g, gc.b, alpha);
            GUI.Label(new Rect(px + 44, gradeBoxY + 26, 65, 56), GetGradeLabel(capturedGrade), gradeLbl);

            GUIStyle pctLbl = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            pctLbl.normal.textColor = new Color(gc.r, gc.g, gc.b, alpha * 0.8f);
            GUI.Label(new Rect(px + 102, gradeBoxY + 38, 100, 30), $"{capturedIvPct * 100:0}%", pctLbl);

            float ivX = px + 210;
            float ivW = panelW - 260;
            DrawMiniIVBar(ivX, gradeBoxY + 22, ivW, "HP", capturedIvHp, alpha);
            DrawMiniIVBar(ivX, gradeBoxY + 52, ivW, "ATK", capturedIvAtk, alpha);
            DrawMiniIVBar(ivX, gradeBoxY + 82, ivW, "DEF", capturedIvDef, alpha);

            float rewardY = gradeBoxY + 132;
            GUI.color = new Color(0.15f, 0.17f, 0.25f, 0.7f * alpha);
            GUI.DrawTexture(new Rect(px + 36, rewardY, panelW - 72, 68), Texture2D.whiteTexture);

            GUIStyle rewardLabel = new GUIStyle(GUI.skin.label)
            { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            rewardLabel.normal.textColor = new Color(0.6f, 0.6f, 0.6f, alpha);
            GUI.color = Color.white;
            GUI.Label(new Rect(px, rewardY + 4, panelW, 22), "보상", rewardLabel);

            GUIStyle rewardVal = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            rewardVal.normal.textColor = new Color(1f, 0.5f, 0.8f, alpha);
            GUI.Label(new Rect(px, rewardY + 32, panelW / 2f, 30), $"+{candyReward} 캔디", rewardVal);

            rewardVal.normal.textColor = new Color(0.4f, 0.8f, 1f, alpha);
            GUI.Label(new Rect(px + panelW / 2f, rewardY + 32, panelW / 2f, 30), $"+{expReward} XP", rewardVal);
        }

        private void DrawFailPopup(float alpha)
        {
            float cx = Screen.width / 2f;
            float cy = Screen.height * 0.35f;

            GUI.color = new Color(0, 0, 0, 0.6f * alpha);
            GUI.DrawTexture(new Rect(cx - 240, cy - 20, 480, 120), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle style = new GUIStyle(GUI.skin.label)
            { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(1f, 0.35f, 0.3f, alpha);
            GUI.Label(new Rect(cx - 240, cy - 10, 480, 55), "도망갔다...", style);

            GUIStyle sub = new GUIStyle(GUI.skin.label)
            { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            sub.normal.textColor = new Color(0.7f, 0.7f, 0.7f, alpha);
            GUI.Label(new Rect(cx - 240, cy + 45, 480, 32), $"{insectName}(이)가 도망쳤습니다!", sub);
        }

        public static void DrawTypedInsectPortrait(float cx, float cy, string id, InsectRarity rarity, float alpha)
        {
            Color col = GetInsectColor(id, rarity);
            Color bodyCol = new Color(col.r, col.g, col.b, alpha);
            Color darkCol = new Color(col.r * 0.45f, col.g * 0.45f, col.b * 0.45f, alpha);
            Color lightCol = new Color(
                Mathf.Min(1, col.r + 0.4f), Mathf.Min(1, col.g + 0.4f), Mathf.Min(1, col.b + 0.4f), alpha);
            Color accentCol = new Color(GetRarityColor(rarity).r, GetRarityColor(rarity).g, GetRarityColor(rarity).b, alpha);
            float s = 1.6f;

            if (string.IsNullOrEmpty(id)) { DrawInsectPortrait(cx, cy, rarity, alpha); return; }

            GUI.color = new Color(accentCol.r, accentCol.g, accentCol.b, 0.06f * alpha + (int)rarity * 0.02f);
            GUI.DrawTexture(new Rect(cx - 48, cy - 48, 96, 96), Texture2D.whiteTexture);

            if (id.Contains("butterfly") || id.Contains("luna") || id.Contains("atlas") || id.Contains("monarch") || id.Contains("morpho") || id.Contains("alexandras") || id.Contains("swallowtail") || id.Contains("cabbage"))
                PortraitButterfly(cx, cy, s, bodyCol, darkCol, lightCol, accentCol, alpha);
            else if (id.Contains("moth") || id.Contains("phantom"))
                PortraitMoth(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("orchid") || id.Contains("ghost"))
                PortraitMantis(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("mantis"))
                PortraitMantis(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("damselfly"))
                PortraitDragonfly(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("dragonfly") || id.Contains("ancient"))
                PortraitDragonfly(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("bee") || id.Contains("wasp") || id.Contains("hornet"))
                PortraitBee(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("firefly"))
                PortraitFirefly(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("stag") || id.Contains("rhinoceros") || id.Contains("hercules"))
                PortraitHornBeetle(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("cicada"))
                PortraitCicada(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("spider"))
                PortraitSpider(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("grasshopper") || id.Contains("katydid"))
                PortraitGrasshopper(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("ladybug"))
                PortraitLadybug(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("centipede"))
                PortraitCentipede(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("caterpillar") || id.Contains("aphid"))
                PortraitCaterpillar(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("ant"))
                PortraitAnt(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("stick") || id.Contains("leaf_insect"))
                PortraitStickInsect(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("mosquito") || id.Contains("fly"))
                PortraitFly(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("cricket") || id.Contains("earwig"))
                PortraitCricket(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("pill_bug"))
                PortraitPillBug(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("longhorn"))
                PortraitLonghorn(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("water") || id.Contains("strider"))
                PortraitWaterStrider(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("diving"))
                PortraitDiving(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else if (id.Contains("jewel") || id.Contains("scarab") || id.Contains("golden"))
                PortraitJewel(cx, cy, s, bodyCol, darkCol, lightCol, alpha);
            else
                PortraitGenericBeetle(cx, cy, s, bodyCol, darkCol, lightCol, alpha);

            GUI.color = Color.white;
        }

        private static void PortraitEyes(float cx, float cy, float s, float alpha)
        {
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.DrawTexture(new Rect(cx - 6 * s, cy, 5 * s, 5 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2 * s, cy, 5 * s, 5 * s), Texture2D.whiteTexture);
            GUI.color = new Color(0, 0, 0, alpha);
            GUI.DrawTexture(new Rect(cx - 4 * s, cy + 1.5f * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 4 * s, cy + 1.5f * s, 2 * s, 2 * s), Texture2D.whiteTexture);
        }

        private static void PortraitLegs(float cx, float cy, float s, Color dark, int pairs, float alpha)
        {
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            for (int i = 0; i < pairs; i++)
            {
                float lx = (i - (pairs - 1) * 0.5f) * 10 * s;
                GUI.DrawTexture(new Rect(cx + lx - 3 * s, cy + 12 * s, 6 * s, 14 * s), Texture2D.whiteTexture);
            }
        }

        private static void PortraitButterfly(float cx, float cy, float s, Color body, Color dark, Color light, Color accent, float a)
        {
            float wingFlap = Mathf.Sin(Time.time * 4f) * 3 * s;
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.7f * a);
            GUI.DrawTexture(new Rect(cx - 40 * s, cy - 24 * s + wingFlap, 30 * s, 36 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 10 * s, cy - 24 * s - wingFlap, 30 * s, 36 * s), Texture2D.whiteTexture);
            GUI.color = new Color(light.r, light.g, light.b, 0.55f * a);
            GUI.DrawTexture(new Rect(cx - 34 * s, cy - 16 * s + wingFlap, 18 * s, 20 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 16 * s, cy - 16 * s - wingFlap, 18 * s, 20 * s), Texture2D.whiteTexture);
            GUI.color = new Color(body.r, body.g, body.b, 0.55f * a);
            GUI.DrawTexture(new Rect(cx - 34 * s, cy + 6 * s + wingFlap, 22 * s, 22 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 12 * s, cy + 6 * s - wingFlap, 22 * s, 22 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 4 * s, cy - 20 * s, 8 * s, 38 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 3 * s, cy - 18 * s, 6 * s, 34 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 28 * s, 12 * s, 12 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 24 * s, s, a);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 44 * s, 2 * s, 18 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 44 * s, 2 * s, 18 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 46 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 7 * s, cy - 46 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
        }

        private static void PortraitMoth(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            Color dusty = new Color(body.r * 0.7f, body.g * 0.6f, body.b * 0.5f, 0.65f * a);
            GUI.color = dusty;
            GUI.DrawTexture(new Rect(cx - 38 * s, cy - 16 * s, 30 * s, 28 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8 * s, cy - 16 * s, 30 * s, 28 * s), Texture2D.whiteTexture);
            GUI.color = new Color(light.r, light.g, light.b, 0.2f * a);
            GUI.DrawTexture(new Rect(cx - 30 * s, cy - 8 * s, 14 * s, 12 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 16 * s, cy - 8 * s, 14 * s, 12 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 5 * s, cy - 14 * s, 10 * s, 28 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 4 * s, cy - 12 * s, 8 * s, 24 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 24 * s, 20 * s, 14 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 20 * s, 14 * s, 8 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 18 * s, s, a);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 38 * s, 2 * s, 16 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 4 * s, cy - 38 * s, 2 * s, 16 * s), Texture2D.whiteTexture);
            PortraitLegs(cx, cy + 4 * s, s, dark, 3, a);
        }

        private static void PortraitMantis(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 14 * s, 16 * s, 36 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 12 * s, 12 * s, 32 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 32 * s, 20 * s, 20 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 28 * s, 14 * s, 12 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 26 * s, s, a);
            float swing = Mathf.Sin(Time.time * 3f) * 4 * s;
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 26 * s, cy - 22 * s + swing, 18 * s, 5 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8 * s, cy - 22 * s - swing, 18 * s, 5 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 30 * s, cy - 28 * s + swing, 6 * s, 14 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 24 * s, cy - 28 * s - swing, 6 * s, 14 * s), Texture2D.whiteTexture);
            GUI.color = new Color(body.r, body.g, body.b, 0.25f * a);
            GUI.DrawTexture(new Rect(cx - 18 * s, cy - 8 * s, 12 * s, 24 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 8 * s, 12 * s, 24 * s), Texture2D.whiteTexture);
            PortraitLegs(cx, cy + 6 * s, s, dark, 2, a);
        }

        private static void PortraitDragonfly(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            float wingFlap = Mathf.Sin(Time.time * 6f) * 2 * s;
            GUI.color = new Color(light.r, light.g, light.b, 0.3f * a);
            GUI.DrawTexture(new Rect(cx - 38 * s, cy - 18 * s + wingFlap, 32 * s, 10 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 18 * s - wingFlap, 32 * s, 10 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 34 * s, cy - 6 * s - wingFlap, 28 * s, 8 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 6 * s + wingFlap, 28 * s, 8 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 4 * s, cy - 12 * s, 8 * s, 44 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 3 * s, cy - 10 * s, 6 * s, 40 * s), Texture2D.whiteTexture);
            for (int i = 0; i < 4; i++)
            {
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 3 * s, cy + 8 * s + i * 7 * s, 6 * s, 2 * s), Texture2D.whiteTexture);
            }
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 26 * s, 20 * s, 16 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 24 * s, 8 * s, 8 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2 * s, cy - 24 * s, 8 * s, 8 * s), Texture2D.whiteTexture);
            GUI.color = new Color(0, 0, 0, a);
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 22 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 4 * s, cy - 22 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
        }

        private static void PortraitBee(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.3f * a);
            GUI.DrawTexture(new Rect(cx - 28 * s, cy - 24 * s, 20 * s, 14 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8 * s, cy - 24 * s, 20 * s, 14 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 18 * s, cy - 14 * s, 36 * s, 26 * s), Texture2D.whiteTexture);
            GUI.color = new Color(0.1f, 0.1f, 0.05f, a);
            for (int i = 0; i < 3; i++)
                GUI.DrawTexture(new Rect(cx - 16 * s, cy - 10 * s + i * 8 * s, 32 * s, 3 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 26 * s, 16 * s, 14 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 5 * s, cy - 22 * s, 10 * s, 8 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 22 * s, s, a);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 5 * s, cy - 36 * s, 2 * s, 12 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 3 * s, cy - 36 * s, 2 * s, 12 * s), Texture2D.whiteTexture);
            GUI.color = new Color(0.2f, 0.15f, 0.1f, a);
            GUI.DrawTexture(new Rect(cx - 2 * s, cy + 12 * s, 4 * s, 10 * s), Texture2D.whiteTexture);
            PortraitLegs(cx, cy + 2 * s, s, dark, 3, a);
        }

        private static void PortraitFirefly(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            float glowPulse = 0.3f + Mathf.Sin(Time.time * 4f) * 0.2f;
            GUI.color = new Color(0.8f, 1f, 0.4f, glowPulse * a);
            GUI.DrawTexture(new Rect(cx - 30 * s, cy - 30 * s, 60 * s, 60 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 14 * s, cy - 14 * s, 28 * s, 26 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 11 * s, cy - 11 * s, 22 * s, 20 * s), Texture2D.whiteTexture);
            float glowIntensity = 0.6f + Mathf.Sin(Time.time * 4f) * 0.4f;
            GUI.color = new Color(0.9f, 1f, 0.3f, glowIntensity * a);
            GUI.DrawTexture(new Rect(cx - 10 * s, cy + 6 * s, 20 * s, 12 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 24 * s, 16 * s, 12 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 20 * s, s, a);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 5 * s, cy - 36 * s, 2 * s, 14 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 3 * s, cy - 36 * s, 2 * s, 14 * s), Texture2D.whiteTexture);
            GUI.color = new Color(body.r, body.g, body.b, 0.25f * a);
            GUI.DrawTexture(new Rect(cx - 22 * s, cy - 10 * s, 14 * s, 18 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8 * s, cy - 10 * s, 14 * s, 18 * s), Texture2D.whiteTexture);
            PortraitLegs(cx, cy + 4 * s, s, dark, 3, a);
        }

        private static void PortraitHornBeetle(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 26 * s, cy - 16 * s, 52 * s, 32 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 22 * s, cy - 13 * s, 44 * s, 26 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 1 * s, cy - 13 * s, 2 * s, 26 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 12 * s, cy - 32 * s, 24 * s, 20 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 9 * s, cy - 28 * s, 18 * s, 12 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 2 * s, cy - 52 * s, 4 * s, 24 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 54 * s, 5 * s, 5 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 14 * s, cy - 36 * s, 5 * s, 12 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 10 * s, cy - 36 * s, 5 * s, 12 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 24 * s, s, a);
            PortraitLegs(cx, cy, s, dark, 3, a);
        }

        private static void PortraitGenericBeetle(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 24 * s, cy - 18 * s, 48 * s, 30 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 20 * s, cy - 15 * s, 40 * s, 24 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 1 * s, cy - 15 * s, 2 * s, 24 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 30 * s, 20 * s, 16 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 27 * s, 14 * s, 10 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 24 * s, s, a);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 4 * s, cy - 42 * s, 2 * s, 14 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2 * s, cy - 42 * s, 2 * s, 14 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 5 * s, cy - 44 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2 * s, cy - 44 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            PortraitLegs(cx, cy, s, dark, 3, a);
        }

        private static void PortraitCicada(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = new Color(body.r, body.g, body.b, 0.3f * a);
            GUI.DrawTexture(new Rect(cx - 28 * s, cy - 6 * s, 22 * s, 24 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 6 * s, 22 * s, 24 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 18 * s, cy - 12 * s, 36 * s, 28 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 15 * s, cy - 10 * s, 30 * s, 24 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 24 * s, 20 * s, 14 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 20 * s, 14 * s, 8 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 18 * s, s, a);
            PortraitLegs(cx, cy + 4 * s, s, dark, 3, a);
        }

        private static void PortraitAnt(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 9 * s, cy + 0 * s, 18 * s, 22 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 7 * s, cy + 2 * s, 14 * s, 18 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 8 * s, 12 * s, 12 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 4 * s, cy - 6 * s, 8 * s, 8 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 26 * s, 20 * s, 20 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 22 * s, 14 * s, 12 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 2 * s, cy - 14 * s, 4 * s, 10 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 20 * s, s, a);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 40 * s, 2 * s, 16 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 4 * s, cy - 40 * s, 2 * s, 16 * s), Texture2D.whiteTexture);
            PortraitLegs(cx, cy + 8 * s, s, dark, 3, a);
        }

        private static void PortraitCricket(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 14 * s, cy - 10 * s, 28 * s, 26 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 12 * s, cy - 8 * s, 24 * s, 22 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 24 * s, 20 * s, 16 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 20 * s, 14 * s, 10 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 18 * s, s, a);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 26 * s, cy + 4 * s, 16 * s, 5 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 10 * s, cy + 4 * s, 16 * s, 5 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 30 * s, cy - 2 * s, 6 * s, 12 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 24 * s, cy - 2 * s, 6 * s, 12 * s), Texture2D.whiteTexture);
            PortraitLegs(cx, cy + 4 * s, s, dark, 3, a);
        }

        private static void PortraitWaterStrider(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 5 * s, cy - 14 * s, 10 * s, 32 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 4 * s, cy - 12 * s, 8 * s, 28 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 22 * s, 16 * s, 12 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 5 * s, cy - 18 * s, 10 * s, 6 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 18 * s, s, a);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 34 * s, cy - 4 * s, 28 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 4 * s, 28 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 30 * s, cy + 8 * s, 24 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy + 8 * s, 24 * s, 3 * s), Texture2D.whiteTexture);
            GUI.color = new Color(0.6f, 0.8f, 1f, 0.15f * a);
            GUI.DrawTexture(new Rect(cx - 38 * s, cy + 12 * s, 76 * s, 6 * s), Texture2D.whiteTexture);
        }

        private static void PortraitDiving(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 20 * s, cy - 12 * s, 40 * s, 26 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 17 * s, cy - 10 * s, 34 * s, 22 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 15 * s, cy - 6 * s, 30 * s, 10 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 24 * s, 20 * s, 14 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 20 * s, 14 * s, 8 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 18 * s, s, a);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 24 * s, cy + 6 * s, 14 * s, 8 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 10 * s, cy + 6 * s, 14 * s, 8 * s), Texture2D.whiteTexture);
            PortraitLegs(cx, cy + 2 * s, s, dark, 3, a);
        }

        private static void PortraitJewel(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 20 * s, cy - 12 * s, 40 * s, 28 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 17 * s, cy - 10 * s, 34 * s, 24 * s), Texture2D.whiteTexture);
            Color shimmer = new Color(Mathf.Min(1, body.r + 0.25f), Mathf.Min(1, body.g + 0.3f), Mathf.Min(1, body.b + 0.2f), a * 0.7f);
            float shimPulse = 0.4f + Mathf.Sin(Time.time * 3f) * 0.2f;
            GUI.color = new Color(shimmer.r, shimmer.g, shimmer.b, shimPulse * a);
            GUI.DrawTexture(new Rect(cx - 14 * s, cy - 8 * s, 13 * s, 18 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 1 * s, cy - 8 * s, 13 * s, 18 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 1 * s, cy - 10 * s, 2 * s, 24 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 26 * s, 20 * s, 16 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 22 * s, 14 * s, 10 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 20 * s, s, a);
            PortraitLegs(cx, cy + 4 * s, s, dark, 3, a);
        }

        private static void PortraitSpider(float cx, float cy, float s, Color body, Color dark, Color light, float alpha)
        {
            // 배 (큰 원)
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            GUI.DrawTexture(new Rect(cx - 16 * s, cy + 2 * s, 32 * s, 26 * s), Texture2D.whiteTexture);
            // 가슴 (작은 원)
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 10 * s, 16 * s, 14 * s), Texture2D.whiteTexture);
            GUI.color = new Color(light.r, light.g, light.b, alpha);
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 8 * s, 12 * s, 10 * s), Texture2D.whiteTexture);
            // 8개 다리 (좌우 4쌍)
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            for (int i = 0; i < 4; i++)
            {
                float ly = cy - 4 * s + i * 6 * s;
                GUI.DrawTexture(new Rect(cx - 36 * s + i * 4 * s, ly, 20 * s, 2 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 16 * s - i * 4 * s, ly, 20 * s, 2 * s), Texture2D.whiteTexture);
            }
            // 8눈 (2열)
            GUI.color = new Color(1f, 0f, 0f, 0.8f * alpha);
            GUI.DrawTexture(new Rect(cx - 5 * s, cy - 16 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2 * s, cy - 16 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 12 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 3 * s, cy - 12 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 1 * s, cy - 12 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 5 * s, cy - 12 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            // 배 무늬
            GUI.color = new Color(dark.r, dark.g, dark.b, 0.5f * alpha);
            GUI.DrawTexture(new Rect(cx - 10 * s, cy + 10 * s, 20 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 8 * s, cy + 18 * s, 16 * s, 3 * s), Texture2D.whiteTexture);
        }

        private static void PortraitGrasshopper(float cx, float cy, float s, Color body, Color dark, Color light, float alpha)
        {
            // 긴 몸통
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 14 * s, 20 * s, 36 * s), Texture2D.whiteTexture);
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 12 * s, 16 * s, 32 * s), Texture2D.whiteTexture);
            // 머리
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 28 * s, 20 * s, 16 * s), Texture2D.whiteTexture);
            GUI.color = new Color(light.r, light.g, light.b, alpha);
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 24 * s, 14 * s, 10 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 22 * s, s, alpha);
            // 큰 뒷다리 (V자)
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            GUI.DrawTexture(new Rect(cx - 28 * s, cy - 4 * s, 20 * s, 5 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8 * s, cy - 4 * s, 20 * s, 5 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 32 * s, cy + 2 * s, 6 * s, 18 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 26 * s, cy + 2 * s, 6 * s, 18 * s), Texture2D.whiteTexture);
            // 앞다리
            PortraitLegs(cx, cy + 6 * s, s, dark, 2, alpha);
            // 더듬이
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 42 * s, 2 * s, 16 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 4 * s, cy - 42 * s, 2 * s, 16 * s), Texture2D.whiteTexture);
        }

        private static void PortraitLadybug(float cx, float cy, float s, Color body, Color dark, Color light, float alpha)
        {
            // 빨간 반원 몸체
            Color red = new Color(0.9f, 0.15f, 0.1f, alpha);
            GUI.color = red;
            GUI.DrawTexture(new Rect(cx - 22 * s, cy - 10 * s, 44 * s, 28 * s), Texture2D.whiteTexture);
            // 중앙선
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            GUI.DrawTexture(new Rect(cx - 1 * s, cy - 10 * s, 2 * s, 28 * s), Texture2D.whiteTexture);
            // 검은 점 5개
            GUI.color = new Color(0.05f, 0.05f, 0.05f, alpha);
            GUI.DrawTexture(new Rect(cx - 14 * s, cy - 4 * s, 6 * s, 6 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8 * s, cy - 4 * s, 6 * s, 6 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 16 * s, cy + 8 * s, 6 * s, 6 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 10 * s, cy + 8 * s, 6 * s, 6 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 4 * s, cy + 4 * s, 8 * s, 6 * s), Texture2D.whiteTexture);
            // 검은 머리
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 24 * s, 20 * s, 16 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 20 * s, s, alpha);
            PortraitLegs(cx, cy + 6 * s, s, dark, 3, alpha);
        }

        private static void PortraitCentipede(float cx, float cy, float s, Color body, Color dark, Color light, float alpha)
        {
            // 긴 마디진 몸 (5마디)
            for (int i = 0; i < 5; i++)
            {
                float my = cy - 20 * s + i * 12 * s;
                GUI.color = (i % 2 == 0) ? new Color(body.r, body.g, body.b, alpha) : new Color(dark.r, dark.g, dark.b, alpha);
                GUI.DrawTexture(new Rect(cx - 8 * s, my, 16 * s, 10 * s), Texture2D.whiteTexture);
                // 각 마디에 짧은 다리
                GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
                GUI.DrawTexture(new Rect(cx - 18 * s, my + 2 * s, 10 * s, 3 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 8 * s, my + 2 * s, 10 * s, 3 * s), Texture2D.whiteTexture);
            }
            // 머리 + 집게
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 34 * s, 20 * s, 16 * s), Texture2D.whiteTexture);
            GUI.color = new Color(light.r, light.g, light.b, alpha);
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 30 * s, 14 * s, 10 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 28 * s, s, alpha);
            // 집게
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            GUI.DrawTexture(new Rect(cx - 12 * s, cy - 42 * s, 4 * s, 10 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8 * s, cy - 42 * s, 4 * s, 10 * s), Texture2D.whiteTexture);
        }

        private static void PortraitCaterpillar(float cx, float cy, float s, Color body, Color dark, Color light, float alpha)
        {
            // 통통한 원 5개 연속
            for (int i = 0; i < 5; i++)
            {
                float my = cy - 12 * s + i * 10 * s;
                float size = (i == 0) ? 14 * s : 12 * s;
                Color segCol = (i % 2 == 0) ? body : new Color(body.r * 0.8f, body.g * 1.1f, body.b * 0.8f, 1f);
                GUI.color = new Color(segCol.r, segCol.g, segCol.b, alpha);
                GUI.DrawTexture(new Rect(cx - size * 0.5f, my, size, size), Texture2D.whiteTexture);
                // 짧은 다리들
                GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
                GUI.DrawTexture(new Rect(cx - size * 0.5f - 4 * s, my + 3 * s, 4 * s, 3 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + size * 0.5f, my + 3 * s, 4 * s, 3 * s), Texture2D.whiteTexture);
            }
            // 큰 눈 (머리 위)
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 28 * s, 20 * s, 18 * s), Texture2D.whiteTexture);
            GUI.color = new Color(light.r, light.g, light.b, alpha);
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 24 * s, 14 * s, 12 * s), Texture2D.whiteTexture);
            // 큰 눈
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 24 * s, 6 * s, 8 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 1 * s, cy - 24 * s, 6 * s, 8 * s), Texture2D.whiteTexture);
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(cx - 4 * s, cy - 21 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 3 * s, cy - 21 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            // 더듬이
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 38 * s, 2 * s, 12 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 4 * s, cy - 38 * s, 2 * s, 12 * s), Texture2D.whiteTexture);
        }

        private static void PortraitStickInsect(float cx, float cy, float s, Color body, Color dark, Color light, float alpha)
        {
            // 매우 가느다란 직선 몸
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            GUI.DrawTexture(new Rect(cx - 3 * s, cy - 30 * s, 6 * s, 60 * s), Texture2D.whiteTexture);
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            GUI.DrawTexture(new Rect(cx - 2 * s, cy - 28 * s, 4 * s, 56 * s), Texture2D.whiteTexture);
            // 머리
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            GUI.DrawTexture(new Rect(cx - 5 * s, cy - 38 * s, 10 * s, 10 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 36 * s, s * 0.7f, alpha);
            // 가는 다리 3쌍
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            for (int i = 0; i < 3; i++)
            {
                float ly = cy - 16 * s + i * 16 * s;
                GUI.DrawTexture(new Rect(cx - 24 * s, ly, 20 * s, 2 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 4 * s, ly, 20 * s, 2 * s), Texture2D.whiteTexture);
            }
            // 더듬이
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            GUI.DrawTexture(new Rect(cx - 4 * s, cy - 50 * s, 2 * s, 14 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2 * s, cy - 50 * s, 2 * s, 14 * s), Texture2D.whiteTexture);
        }

        private static void PortraitFly(float cx, float cy, float s, Color body, Color dark, Color light, float alpha)
        {
            // 작은 몸
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 6 * s, 16 * s, 20 * s), Texture2D.whiteTexture);
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 4 * s, 12 * s, 16 * s), Texture2D.whiteTexture);
            // 거대한 빨간 눈 (원 2개)
            GUI.color = new Color(0.8f, 0.1f, 0.05f, alpha);
            GUI.DrawTexture(new Rect(cx - 16 * s, cy - 24 * s, 14 * s, 14 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2 * s, cy - 24 * s, 14 * s, 14 * s), Texture2D.whiteTexture);
            // 눈 하이라이트
            GUI.color = new Color(1f, 1f, 1f, 0.4f * alpha);
            GUI.DrawTexture(new Rect(cx - 12 * s, cy - 22 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 22 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            // 투명 날개 2장
            GUI.color = new Color(light.r, light.g, light.b, 0.25f * alpha);
            GUI.DrawTexture(new Rect(cx - 30 * s, cy - 14 * s, 22 * s, 16 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8 * s, cy - 14 * s, 22 * s, 16 * s), Texture2D.whiteTexture);
            PortraitLegs(cx, cy + 4 * s, s, dark, 3, alpha);
        }

        private static void PortraitPillBug(float cx, float cy, float s, Color body, Color dark, Color light, float alpha)
        {
            // 둥근 몸
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            GUI.DrawTexture(new Rect(cx - 20 * s, cy - 14 * s, 40 * s, 30 * s), Texture2D.whiteTexture);
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            GUI.DrawTexture(new Rect(cx - 17 * s, cy - 12 * s, 34 * s, 26 * s), Texture2D.whiteTexture);
            // 마디 줄 4개
            GUI.color = new Color(dark.r, dark.g, dark.b, 0.4f * alpha);
            for (int i = 0; i < 4; i++)
            {
                float ly = cy - 8 * s + i * 6 * s;
                GUI.DrawTexture(new Rect(cx - 15 * s, ly, 30 * s, 2 * s), Texture2D.whiteTexture);
            }
            // 머리
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 24 * s, 20 * s, 12 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 20 * s, s, alpha);
            // 작은 다리들
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            for (int i = 0; i < 5; i++)
            {
                float lx = cx - 14 * s + i * 7 * s;
                GUI.DrawTexture(new Rect(lx, cy + 16 * s, 4 * s, 6 * s), Texture2D.whiteTexture);
            }
            // 더듬이
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 34 * s, 2 * s, 12 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 4 * s, cy - 34 * s, 2 * s, 12 * s), Texture2D.whiteTexture);
        }

        private static void PortraitLonghorn(float cx, float cy, float s, Color body, Color dark, Color light, float alpha)
        {
            // 딱정벌레 몸체
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            GUI.DrawTexture(new Rect(cx - 20 * s, cy - 12 * s, 40 * s, 28 * s), Texture2D.whiteTexture);
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            GUI.DrawTexture(new Rect(cx - 17 * s, cy - 10 * s, 34 * s, 24 * s), Texture2D.whiteTexture);
            // 딱지날개 중앙선
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            GUI.DrawTexture(new Rect(cx - 1 * s, cy - 10 * s, 2 * s, 24 * s), Texture2D.whiteTexture);
            // 머리
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 26 * s, 20 * s, 16 * s), Texture2D.whiteTexture);
            GUI.color = new Color(light.r, light.g, light.b, alpha);
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 22 * s, 14 * s, 10 * s), Texture2D.whiteTexture);
            PortraitEyes(cx, cy - 20 * s, s, alpha);
            // 초장 더듬이 2개 (몸보다 긴 라인)
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 58 * s, 2 * s, 34 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 58 * s, 2 * s, 34 * s), Texture2D.whiteTexture);
            // 더듬이 끝 구부림
            GUI.DrawTexture(new Rect(cx - 14 * s, cy - 60 * s, 8 * s, 2 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 60 * s, 8 * s, 2 * s), Texture2D.whiteTexture);
            PortraitLegs(cx, cy + 4 * s, s, dark, 3, alpha);
        }

        private void DrawMiniIVBar(float x, float y, float w, string label, int iv, float alpha)
        {
            GUIStyle lbl = new GUIStyle(GUI.skin.label) { fontSize = 19 };
            lbl.normal.textColor = new Color(0.55f, 0.55f, 0.55f, alpha);
            GUI.Label(new Rect(x, y, 50, 22), label, lbl);

            float barX = x + 52;
            float barW = w - 90;
            float barH = 14f;

            GUI.color = new Color(0.15f, 0.15f, 0.2f, alpha);
            GUI.DrawTexture(new Rect(barX, y + 4, barW, barH), Texture2D.whiteTexture);

            float ratio = iv / (float)PlayerInsectData.MaxIV;
            Color bc = GetIVBarColor(iv);
            GUI.color = new Color(bc.r, bc.g, bc.b, alpha);
            GUI.DrawTexture(new Rect(barX, y + 4, barW * ratio, barH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle vs = new GUIStyle(GUI.skin.label)
            { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            vs.normal.textColor = new Color(bc.r, bc.g, bc.b, alpha);
            GUI.Label(new Rect(barX + barW + 6, y, 34, 22), $"{iv}", vs);
        }

        public static void DrawInsectPortrait(float cx, float cy, InsectRarity rarity, float alpha)
        {
            Color col = GetRarityColor(rarity);
            Color bodyCol = new Color(col.r, col.g, col.b, alpha);
            Color darkCol = new Color(col.r * 0.6f, col.g * 0.6f, col.b * 0.6f, alpha);
            Color lightCol = new Color(
                Mathf.Min(1, col.r + 0.3f), Mathf.Min(1, col.g + 0.3f), Mathf.Min(1, col.b + 0.3f), alpha);

            GUI.color = new Color(bodyCol.r, bodyCol.g, bodyCol.b, 0.15f * alpha);
            GUI.DrawTexture(new Rect(cx - 40, cy - 40, 80, 80), Texture2D.whiteTexture);

            GUI.color = darkCol;
            GUI.DrawTexture(new Rect(cx - 24, cy - 18, 48, 28), Texture2D.whiteTexture);
            GUI.color = bodyCol;
            GUI.DrawTexture(new Rect(cx - 20, cy - 15, 40, 22), Texture2D.whiteTexture);
            GUI.color = darkCol;
            GUI.DrawTexture(new Rect(cx - 10, cy - 30, 20, 16), Texture2D.whiteTexture);
            GUI.color = lightCol;
            GUI.DrawTexture(new Rect(cx - 7, cy - 27, 14, 10), Texture2D.whiteTexture);
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.DrawTexture(new Rect(cx - 6, cy - 24, 4, 4), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2, cy - 24, 4, 4), Texture2D.whiteTexture);
            GUI.color = bodyCol;
            GUI.DrawTexture(new Rect(cx - 2, cy - 42, 2, 14), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2, cy - 42, 2, 14), Texture2D.whiteTexture);
            GUI.color = lightCol;
            GUI.DrawTexture(new Rect(cx - 4, cy - 44, 3, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 3, cy - 44, 3, 3), Texture2D.whiteTexture);
            GUI.color = darkCol;
            GUI.DrawTexture(new Rect(cx - 18, cy + 10, 8, 12), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 6, cy + 10, 8, 14), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 4, cy + 10, 8, 14), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 12, cy + 10, 8, 12), Texture2D.whiteTexture);
            GUI.color = new Color(col.r, col.g, col.b, 0.3f * alpha);
            GUI.DrawTexture(new Rect(cx - 30, cy - 8, 6, 18), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 26, cy - 8, 6, 18), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        [System.Obsolete("Use UITheme.Instance.GetInsectRarityColor() instead")]
        public static Color GetRarityColor(InsectRarity rarity)
        {
            switch (rarity)
            {
                case InsectRarity.Common: return new Color(0.55f, 0.45f, 0.3f);
                case InsectRarity.Uncommon: return new Color(0.3f, 0.8f, 0.3f);
                case InsectRarity.Rare: return new Color(0.3f, 0.5f, 0.95f);
                case InsectRarity.Epic: return new Color(0.75f, 0.3f, 0.95f);
                case InsectRarity.Legendary: return new Color(1f, 0.8f, 0.2f);
                default: return Color.gray;
            }
        }

        public static Color GetInsectColor(string insectId, InsectRarity rarity)
        {
            if (string.IsNullOrEmpty(insectId)) return GetRarityColor(rarity);

            uint hash = 0;
            for (int i = 0; i < insectId.Length; i++)
                hash = hash * 31u + (uint)insectId[i];

            float hue = (hash % 360u) / 360f;
            float sat = 0.55f + (int)rarity * 0.06f;
            float val = 0.7f + (int)rarity * 0.06f;

            return Color.HSVToRGB(hue, Mathf.Clamp01(sat), Mathf.Clamp01(val));
        }

        public static Color GetGradeColor(IVGrade grade)
        {
            switch (grade)
            {
                case IVGrade.S: return new Color(1f, 0.8f, 0.15f);
                case IVGrade.A: return new Color(0.7f, 0.3f, 0.95f);
                case IVGrade.B: return new Color(0.3f, 0.55f, 1f);
                case IVGrade.C: return new Color(0.4f, 0.85f, 0.4f);
                default: return new Color(0.55f, 0.55f, 0.55f);
            }
        }

        public static string GetGradeLabel(IVGrade grade)
        {
            switch (grade)
            {
                case IVGrade.S: return "S";
                case IVGrade.A: return "A";
                case IVGrade.B: return "B";
                case IVGrade.C: return "C";
                default: return "D";
            }
        }

        public static Color GetIVBarColor(int iv)
        {
            float ratio = iv / (float)PlayerInsectData.MaxIV;
            if (ratio >= 0.9f) return new Color(1f, 0.8f, 0.15f);
            if (ratio >= 0.7f) return new Color(0.7f, 0.3f, 0.95f);
            if (ratio >= 0.5f) return new Color(0.3f, 0.55f, 1f);
            if (ratio >= 0.3f) return new Color(0.4f, 0.85f, 0.4f);
            return new Color(0.55f, 0.55f, 0.55f);
        }

        public void AutoWire(CaptureController controller)
        {
            if (captureController == null || captureController != controller)
            {
                if (captureController != null)
                    captureController.CaptureResolved -= OnCaptureResolved;
                captureController = controller;
                if (captureController != null)
                    captureController.CaptureResolved += OnCaptureResolved;
            }
        }

        public void AutoWire(PlayerInsectCollection col)
        {
            if (insectCollection == null)
                insectCollection = col;
        }
    }
}
