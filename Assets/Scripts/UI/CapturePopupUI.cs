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

        // OnGUI 매 프레임(popupTimer>0 동안 5초) new GUIStyle 12회 회귀 차단.
        // 모든 textColor는 alpha/rarity 동적이라 base 캐시 + textColor 매 호출 갱신 (BattleScreenUI 패턴).
        private GUIStyle headerStyleCache;
        private GUIStyle nameStyleCache;
        private GUIStyle subStyleCache;
        private GUIStyle gradeTitleStyleCache;
        private GUIStyle gradeLblStyleCache;
        private GUIStyle pctLblStyleCache;
        private GUIStyle rewardLabelStyleCache;
        private GUIStyle rewardValStyleCache;
        private GUIStyle failStyleCache;
        private GUIStyle failSubStyleCache;
        private GUIStyle ivLblStyleCache;
        private GUIStyle ivVsStyleCache;
        private bool popupStylesReady;

        private static readonly Color SubGrayBase = new Color(0.8f, 0.8f, 0.8f);
        private static readonly Color GradeTitleGrayBase = new Color(0.6f, 0.6f, 0.6f);
        private static readonly Color RewardCandyBase = new Color(1f, 0.5f, 0.8f);
        private static readonly Color RewardExpBase = new Color(0.4f, 0.8f, 1f);
        private static readonly Color FailMsgBase = new Color(1f, 0.35f, 0.3f);
        private static readonly Color FailSubBase = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color IvLblBase = new Color(0.55f, 0.55f, 0.55f);

        private void InitPopupStyles()
        {
            if (popupStylesReady) return;
            popupStylesReady = true;

            headerStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            nameStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            subStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            gradeTitleStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 19, alignment = TextAnchor.MiddleLeft };
            gradeLblStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            pctLblStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            rewardLabelStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            rewardValStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            failStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            failSubStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            ivLblStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 19 };
            ivVsStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        }

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

            InitPopupStyles();

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
            float panelH = UISafeLayout.Px.ClampHeight(540f);
            float px = cx - panelW / 2f;
            // 화면 38% 지점 중심 — 단 세이프에어리어 + 세로 마진 밖으로는 나가지 않는다.
            float py = Mathf.Clamp(
                cy - panelH / 2f,
                UISafeLayout.Px.ContentTop,
                Mathf.Max(UISafeLayout.Px.ContentTop, UISafeLayout.Px.ContentBottom - panelH));

            float slideIn = Mathf.Clamp01(animTime / 0.25f);
            py += (1f - slideIn) * 30f;

            Color rarityCol = UITheme.Instance.GetInsectRarityColor(insectRarity);

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

            headerStyleCache.normal.textColor = new Color(1, 1, 1, alpha);
            GUI.color = Color.white;
            GUI.Label(new Rect(px, py + 14, panelW, 36), "포획 성공!", headerStyleCache);

            DrawTypedInsectPortrait(cx, py + 105, insectId, insectRarity, alpha);

            nameStyleCache.normal.textColor = new Color(rarityCol.r, rarityCol.g, rarityCol.b, alpha);
            GUI.Label(new Rect(px, py + 175, panelW, 40), insectName, nameStyleCache);

            subStyleCache.normal.textColor = new Color(SubGrayBase.r, SubGrayBase.g, SubGrayBase.b, alpha);
            GUI.Label(new Rect(px, py + 216, panelW, 28),
                $"Lv.{insectLevel}  |  {insectRarity}", subStyleCache);

            Color gc = GetGradeColor(capturedGrade);
            float gradeBoxY = py + 256;

            GUI.color = new Color(0.1f, 0.12f, 0.18f, 0.8f * alpha);
            GUI.DrawTexture(new Rect(px + 30, gradeBoxY, panelW - 60, 120), Texture2D.whiteTexture);
            GUI.color = new Color(gc.r, gc.g, gc.b, 0.6f * alpha);
            GUI.DrawTexture(new Rect(px + 30, gradeBoxY, 5, 120), Texture2D.whiteTexture);
            GUI.color = Color.white;

            gradeTitleStyleCache.normal.textColor = new Color(GradeTitleGrayBase.r, GradeTitleGrayBase.g, GradeTitleGrayBase.b, alpha);
            GUI.Label(new Rect(px + 44, gradeBoxY + 6, 120, 22), "개체값 감정", gradeTitleStyleCache);

            gradeLblStyleCache.normal.textColor = new Color(gc.r, gc.g, gc.b, alpha);
            GUI.Label(new Rect(px + 44, gradeBoxY + 26, 65, 56), GetGradeLabel(capturedGrade), gradeLblStyleCache);

            pctLblStyleCache.normal.textColor = new Color(gc.r, gc.g, gc.b, alpha * 0.8f);
            GUI.Label(new Rect(px + 102, gradeBoxY + 38, 100, 30), $"{capturedIvPct * 100:0}%", pctLblStyleCache);

            float ivX = px + 210;
            float ivW = panelW - 260;
            DrawMiniIVBar(ivX, gradeBoxY + 22, ivW, "HP", capturedIvHp, alpha);
            DrawMiniIVBar(ivX, gradeBoxY + 52, ivW, "ATK", capturedIvAtk, alpha);
            DrawMiniIVBar(ivX, gradeBoxY + 82, ivW, "DEF", capturedIvDef, alpha);

            float rewardY = gradeBoxY + 132;
            GUI.color = new Color(0.15f, 0.17f, 0.25f, 0.7f * alpha);
            GUI.DrawTexture(new Rect(px + 36, rewardY, panelW - 72, 68), Texture2D.whiteTexture);

            rewardLabelStyleCache.normal.textColor = new Color(GradeTitleGrayBase.r, GradeTitleGrayBase.g, GradeTitleGrayBase.b, alpha);
            GUI.color = Color.white;
            GUI.Label(new Rect(px, rewardY + 4, panelW, 22), "보상", rewardLabelStyleCache);

            rewardValStyleCache.normal.textColor = new Color(RewardCandyBase.r, RewardCandyBase.g, RewardCandyBase.b, alpha);
            GUI.Label(new Rect(px, rewardY + 32, panelW / 2f, 30), $"+{candyReward} 캔디", rewardValStyleCache);

            rewardValStyleCache.normal.textColor = new Color(RewardExpBase.r, RewardExpBase.g, RewardExpBase.b, alpha);
            GUI.Label(new Rect(px + panelW / 2f, rewardY + 32, panelW / 2f, 30), $"+{expReward} XP", rewardValStyleCache);
        }

        private void DrawFailPopup(float alpha)
        {
            float cx = Screen.width / 2f;
            float cy = Screen.height * 0.35f;

            GUI.color = new Color(0, 0, 0, 0.6f * alpha);
            GUI.DrawTexture(new Rect(cx - 240, cy - 20, 480, 120), Texture2D.whiteTexture);
            GUI.color = Color.white;

            failStyleCache.normal.textColor = new Color(FailMsgBase.r, FailMsgBase.g, FailMsgBase.b, alpha);
            GUI.Label(new Rect(cx - 240, cy - 10, 480, 55), "도망갔다...", failStyleCache);

            failSubStyleCache.normal.textColor = new Color(FailSubBase.r, FailSubBase.g, FailSubBase.b, alpha);
            GUI.Label(new Rect(cx - 240, cy + 45, 480, 32), $"{insectName}(이)가 도망쳤습니다!", failSubStyleCache);
        }

        public static void DrawTypedInsectPortrait(float cx, float cy, string id, InsectRarity rarity, float alpha)
        {
            Color col = GetInsectColor(id, rarity);
            Color bodyCol = new Color(col.r, col.g, col.b, alpha);
            Color darkCol = new Color(col.r * 0.45f, col.g * 0.45f, col.b * 0.45f, alpha);
            Color lightCol = new Color(
                Mathf.Min(1, col.r + 0.4f), Mathf.Min(1, col.g + 0.4f), Mathf.Min(1, col.b + 0.4f), alpha);
            Color rarityBase = UITheme.Instance.GetInsectRarityColor(rarity);
            Color accentCol = new Color(rarityBase.r, rarityBase.g, rarityBase.b, alpha);
            float s = 1.6f;

            if (string.IsNullOrEmpty(id)) { DrawInsectPortrait(cx, cy, rarity, alpha); return; }

            // 등급 후광 — 예전엔 각진 96×96 사각형이라 뒤에 네모난 판이 깔린 것처럼 보였다.
            // 이제 원이라 곤충 주변으로 부드럽게 번진다(UIShapes.Part의 기본 roundness=1).
            UIShapes.Part(new Rect(cx - 48, cy - 48, 96, 96),
                new Color(accentCol.r, accentCol.g, accentCol.b, 0.06f * alpha + (int)rarity * 0.02f));

            // 접지 그림자 — 곤충이 배경에 떠 있지 않고 놓여 있게 보이게 한다.
            // 파트별 실루엣은 25개 함수를 전부 고쳐야 하므로, 발밑 타원 하나로 형체를 잡는다.
            UIShapes.Ellipse(new Rect(cx - 26f, cy + 26f, 52f, 13f), new Color(0f, 0f, 0f, 0.20f * alpha));

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
            // **"beetle"이 "bee"를 품는다** — 가드가 없으면 사슴벌레·장수풍뎅이·헤라클레스를 포함한
            // 딱정벌레 31종이 전부 벌로 그려진다(포획 직후 이 팝업이 잡은 곤충을 보여주는 자리다).
            // InsectEntity.BuildModel은 같은 가드를 갖고 있었는데 이쪽만 빠져 있었다.
            else if ((id.Contains("bee") && !id.Contains("beetle")) || id.Contains("wasp") || id.Contains("hornet"))
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
            UIShapes.Part(new Rect(cx - 6 * s, cy, 5 * s, 5 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 2 * s, cy, 5 * s, 5 * s), GUI.color);
            GUI.color = new Color(0, 0, 0, alpha);
            UIShapes.Part(new Rect(cx - 4 * s, cy + 1.5f * s, 2 * s, 2 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 4 * s, cy + 1.5f * s, 2 * s, 2 * s), GUI.color);
        }

        /// <summary>
        /// 다리 — 몸통 아래에서 바깥·아래로 벌어지는 두 마디 캡슐.
        ///
        /// 예전엔 몸 밑에 세로 직사각형을 나란히 붙였다. 축 정렬 사각형으로는 대각선을 그릴 수
        /// 없어 다리가 "말뚝"처럼 보였고, 곤충이 네모나 보이는 가장 큰 이유였다.
        /// 바깥쪽 다리일수록 더 벌어지게 해서 실루엣을 잡는다.
        /// </summary>
        private static void PortraitLegs(float cx, float cy, float s, Color dark, int pairs, float alpha)
        {
            Color legCol = new Color(dark.r, dark.g, dark.b, alpha);
            float thickness = 2.6f * s;
            for (int i = 0; i < pairs; i++)
            {
                // -1(왼쪽 끝) ~ +1(오른쪽 끝). 한 쌍이면 0(정중앙)이라 0으로 나누지 않는다.
                float t = pairs > 1 ? (i / (float)(pairs - 1)) * 2f - 1f : 0f;
                float hipX = cx + t * 9f * s;
                float hipY = cy + 10f * s;
                // 무릎에서 한 번 꺾어 두 마디로 — 직선 한 줄보다 곤충 다리처럼 보인다.
                float kneeX = hipX + Mathf.Sign(t == 0f ? 1f : t) * (5f + Mathf.Abs(t) * 4f) * s;
                float kneeY = hipY + 7f * s;
                float footX = kneeX + Mathf.Sign(t == 0f ? 1f : t) * (3f + Mathf.Abs(t) * 3f) * s;
                float footY = kneeY + 8f * s;

                UIShapes.Capsule(new Vector2(hipX, hipY), new Vector2(kneeX, kneeY), thickness, legCol);
                UIShapes.Capsule(new Vector2(kneeX, kneeY), new Vector2(footX, footY), thickness * 0.8f, legCol);
            }
        }

        private static void PortraitButterfly(float cx, float cy, float s, Color body, Color dark, Color light, Color accent, float a)
        {
            float wingFlap = Mathf.Sin(Time.time * 4f) * 3 * s;
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.7f * a);
            UIShapes.Part(new Rect(cx - 40 * s, cy - 24 * s + wingFlap, 30 * s, 36 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 10 * s, cy - 24 * s - wingFlap, 30 * s, 36 * s), GUI.color);
            GUI.color = new Color(light.r, light.g, light.b, 0.55f * a);
            UIShapes.Part(new Rect(cx - 34 * s, cy - 16 * s + wingFlap, 18 * s, 20 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 16 * s, cy - 16 * s - wingFlap, 18 * s, 20 * s), GUI.color);
            GUI.color = new Color(body.r, body.g, body.b, 0.55f * a);
            UIShapes.Part(new Rect(cx - 34 * s, cy + 6 * s + wingFlap, 22 * s, 22 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 12 * s, cy + 6 * s - wingFlap, 22 * s, 22 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 4 * s, cy - 20 * s, 8 * s, 38 * s), GUI.color);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 3 * s, cy - 18 * s, 6 * s, 34 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 6 * s, cy - 28 * s, 12 * s, 12 * s), GUI.color);
            PortraitEyes(cx, cy - 24 * s, s, a);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 8 * s, cy - 44 * s, 2 * s, 18 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 6 * s, cy - 44 * s, 2 * s, 18 * s), GUI.color);
            GUI.color = light;
            UIShapes.Part(new Rect(cx - 10 * s, cy - 46 * s, 4 * s, 4 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 7 * s, cy - 46 * s, 4 * s, 4 * s), GUI.color);
        }

        private static void PortraitMoth(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            Color dusty = new Color(body.r * 0.7f, body.g * 0.6f, body.b * 0.5f, 0.65f * a);
            GUI.color = dusty;
            UIShapes.Part(new Rect(cx - 38 * s, cy - 16 * s, 30 * s, 28 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 8 * s, cy - 16 * s, 30 * s, 28 * s), GUI.color);
            GUI.color = new Color(light.r, light.g, light.b, 0.2f * a);
            UIShapes.Part(new Rect(cx - 30 * s, cy - 8 * s, 14 * s, 12 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 16 * s, cy - 8 * s, 14 * s, 12 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 5 * s, cy - 14 * s, 10 * s, 28 * s), GUI.color);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 4 * s, cy - 12 * s, 8 * s, 24 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 10 * s, cy - 24 * s, 20 * s, 14 * s), GUI.color);
            GUI.color = light;
            UIShapes.Part(new Rect(cx - 7 * s, cy - 20 * s, 14 * s, 8 * s), GUI.color);
            PortraitEyes(cx, cy - 18 * s, s, a);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 6 * s, cy - 38 * s, 2 * s, 16 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 4 * s, cy - 38 * s, 2 * s, 16 * s), GUI.color);
            PortraitLegs(cx, cy + 4 * s, s, dark, 3, a);
        }

        private static void PortraitMantis(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 8 * s, cy - 14 * s, 16 * s, 36 * s), GUI.color);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 6 * s, cy - 12 * s, 12 * s, 32 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 10 * s, cy - 32 * s, 20 * s, 20 * s), GUI.color);
            GUI.color = light;
            UIShapes.Part(new Rect(cx - 7 * s, cy - 28 * s, 14 * s, 12 * s), GUI.color);
            PortraitEyes(cx, cy - 26 * s, s, a);
            float swing = Mathf.Sin(Time.time * 3f) * 4 * s;
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 26 * s, cy - 22 * s + swing, 18 * s, 5 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 8 * s, cy - 22 * s - swing, 18 * s, 5 * s), GUI.color);
            GUI.color = light;
            UIShapes.Part(new Rect(cx - 30 * s, cy - 28 * s + swing, 6 * s, 14 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 24 * s, cy - 28 * s - swing, 6 * s, 14 * s), GUI.color);
            GUI.color = new Color(body.r, body.g, body.b, 0.25f * a);
            UIShapes.Part(new Rect(cx - 18 * s, cy - 8 * s, 12 * s, 24 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 6 * s, cy - 8 * s, 12 * s, 24 * s), GUI.color);
            PortraitLegs(cx, cy + 6 * s, s, dark, 2, a);
        }

        private static void PortraitDragonfly(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            float wingFlap = Mathf.Sin(Time.time * 6f) * 2 * s;
            GUI.color = new Color(light.r, light.g, light.b, 0.3f * a);
            UIShapes.Part(new Rect(cx - 38 * s, cy - 18 * s + wingFlap, 32 * s, 10 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 6 * s, cy - 18 * s - wingFlap, 32 * s, 10 * s), GUI.color);
            UIShapes.Part(new Rect(cx - 34 * s, cy - 6 * s - wingFlap, 28 * s, 8 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 6 * s, cy - 6 * s + wingFlap, 28 * s, 8 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 4 * s, cy - 12 * s, 8 * s, 44 * s), GUI.color);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 3 * s, cy - 10 * s, 6 * s, 40 * s), GUI.color);
            for (int i = 0; i < 4; i++)
            {
                GUI.color = dark;
                UIShapes.Part(new Rect(cx - 3 * s, cy + 8 * s + i * 7 * s, 6 * s, 2 * s), GUI.color);
            }
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 10 * s, cy - 26 * s, 20 * s, 16 * s), GUI.color);
            GUI.color = light;
            UIShapes.Part(new Rect(cx - 10 * s, cy - 24 * s, 8 * s, 8 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 2 * s, cy - 24 * s, 8 * s, 8 * s), GUI.color);
            GUI.color = new Color(0, 0, 0, a);
            UIShapes.Part(new Rect(cx - 7 * s, cy - 22 * s, 3 * s, 3 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 4 * s, cy - 22 * s, 3 * s, 3 * s), GUI.color);
        }

        private static void PortraitBee(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.3f * a);
            UIShapes.Part(new Rect(cx - 28 * s, cy - 24 * s, 20 * s, 14 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 8 * s, cy - 24 * s, 20 * s, 14 * s), GUI.color);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 18 * s, cy - 14 * s, 36 * s, 26 * s), GUI.color);
            GUI.color = new Color(0.1f, 0.1f, 0.05f, a);
            for (int i = 0; i < 3; i++)
                UIShapes.Part(new Rect(cx - 16 * s, cy - 10 * s + i * 8 * s, 32 * s, 3 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 8 * s, cy - 26 * s, 16 * s, 14 * s), GUI.color);
            GUI.color = light;
            UIShapes.Part(new Rect(cx - 5 * s, cy - 22 * s, 10 * s, 8 * s), GUI.color);
            PortraitEyes(cx, cy - 22 * s, s, a);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 5 * s, cy - 36 * s, 2 * s, 12 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 3 * s, cy - 36 * s, 2 * s, 12 * s), GUI.color);
            GUI.color = new Color(0.2f, 0.15f, 0.1f, a);
            UIShapes.Part(new Rect(cx - 2 * s, cy + 12 * s, 4 * s, 10 * s), GUI.color);
            PortraitLegs(cx, cy + 2 * s, s, dark, 3, a);
        }

        private static void PortraitFirefly(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            float glowPulse = 0.3f + Mathf.Sin(Time.time * 4f) * 0.2f;
            GUI.color = new Color(0.8f, 1f, 0.4f, glowPulse * a);
            UIShapes.Part(new Rect(cx - 30 * s, cy - 30 * s, 60 * s, 60 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 14 * s, cy - 14 * s, 28 * s, 26 * s), GUI.color);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 11 * s, cy - 11 * s, 22 * s, 20 * s), GUI.color);
            float glowIntensity = 0.6f + Mathf.Sin(Time.time * 4f) * 0.4f;
            GUI.color = new Color(0.9f, 1f, 0.3f, glowIntensity * a);
            UIShapes.Part(new Rect(cx - 10 * s, cy + 6 * s, 20 * s, 12 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 8 * s, cy - 24 * s, 16 * s, 12 * s), GUI.color);
            PortraitEyes(cx, cy - 20 * s, s, a);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 5 * s, cy - 36 * s, 2 * s, 14 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 3 * s, cy - 36 * s, 2 * s, 14 * s), GUI.color);
            GUI.color = new Color(body.r, body.g, body.b, 0.25f * a);
            UIShapes.Part(new Rect(cx - 22 * s, cy - 10 * s, 14 * s, 18 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 8 * s, cy - 10 * s, 14 * s, 18 * s), GUI.color);
            PortraitLegs(cx, cy + 4 * s, s, dark, 3, a);
        }

        private static void PortraitHornBeetle(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 26 * s, cy - 16 * s, 52 * s, 32 * s), GUI.color);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 22 * s, cy - 13 * s, 44 * s, 26 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 1 * s, cy - 13 * s, 2 * s, 26 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 12 * s, cy - 32 * s, 24 * s, 20 * s), GUI.color);
            GUI.color = light;
            UIShapes.Part(new Rect(cx - 9 * s, cy - 28 * s, 18 * s, 12 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 2 * s, cy - 52 * s, 4 * s, 24 * s), GUI.color);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 6 * s, cy - 54 * s, 5 * s, 5 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 14 * s, cy - 36 * s, 5 * s, 12 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 10 * s, cy - 36 * s, 5 * s, 12 * s), GUI.color);
            PortraitEyes(cx, cy - 24 * s, s, a);
            PortraitLegs(cx, cy, s, dark, 3, a);
        }

        private static void PortraitGenericBeetle(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 24 * s, cy - 18 * s, 48 * s, 30 * s), GUI.color);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 20 * s, cy - 15 * s, 40 * s, 24 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 1 * s, cy - 15 * s, 2 * s, 24 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 10 * s, cy - 30 * s, 20 * s, 16 * s), GUI.color);
            GUI.color = light;
            UIShapes.Part(new Rect(cx - 7 * s, cy - 27 * s, 14 * s, 10 * s), GUI.color);
            PortraitEyes(cx, cy - 24 * s, s, a);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 4 * s, cy - 42 * s, 2 * s, 14 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 2 * s, cy - 42 * s, 2 * s, 14 * s), GUI.color);
            GUI.color = light;
            UIShapes.Part(new Rect(cx - 5 * s, cy - 44 * s, 4 * s, 4 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 2 * s, cy - 44 * s, 4 * s, 4 * s), GUI.color);
            PortraitLegs(cx, cy, s, dark, 3, a);
        }

        private static void PortraitCicada(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = new Color(body.r, body.g, body.b, 0.3f * a);
            UIShapes.Part(new Rect(cx - 28 * s, cy - 6 * s, 22 * s, 24 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 6 * s, cy - 6 * s, 22 * s, 24 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 18 * s, cy - 12 * s, 36 * s, 28 * s), GUI.color);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 15 * s, cy - 10 * s, 30 * s, 24 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 10 * s, cy - 24 * s, 20 * s, 14 * s), GUI.color);
            GUI.color = light;
            UIShapes.Part(new Rect(cx - 7 * s, cy - 20 * s, 14 * s, 8 * s), GUI.color);
            PortraitEyes(cx, cy - 18 * s, s, a);
            PortraitLegs(cx, cy + 4 * s, s, dark, 3, a);
        }

        private static void PortraitAnt(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 9 * s, cy + 0 * s, 18 * s, 22 * s), GUI.color);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 7 * s, cy + 2 * s, 14 * s, 18 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 6 * s, cy - 8 * s, 12 * s, 12 * s), GUI.color);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 4 * s, cy - 6 * s, 8 * s, 8 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 10 * s, cy - 26 * s, 20 * s, 20 * s), GUI.color);
            GUI.color = light;
            UIShapes.Part(new Rect(cx - 7 * s, cy - 22 * s, 14 * s, 12 * s), GUI.color);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 2 * s, cy - 14 * s, 4 * s, 10 * s), GUI.color);
            PortraitEyes(cx, cy - 20 * s, s, a);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 6 * s, cy - 40 * s, 2 * s, 16 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 4 * s, cy - 40 * s, 2 * s, 16 * s), GUI.color);
            PortraitLegs(cx, cy + 8 * s, s, dark, 3, a);
        }

        private static void PortraitCricket(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 14 * s, cy - 10 * s, 28 * s, 26 * s), GUI.color);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 12 * s, cy - 8 * s, 24 * s, 22 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 10 * s, cy - 24 * s, 20 * s, 16 * s), GUI.color);
            GUI.color = light;
            UIShapes.Part(new Rect(cx - 7 * s, cy - 20 * s, 14 * s, 10 * s), GUI.color);
            PortraitEyes(cx, cy - 18 * s, s, a);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 26 * s, cy + 4 * s, 16 * s, 5 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 10 * s, cy + 4 * s, 16 * s, 5 * s), GUI.color);
            UIShapes.Part(new Rect(cx - 30 * s, cy - 2 * s, 6 * s, 12 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 24 * s, cy - 2 * s, 6 * s, 12 * s), GUI.color);
            PortraitLegs(cx, cy + 4 * s, s, dark, 3, a);
        }

        private static void PortraitWaterStrider(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 5 * s, cy - 14 * s, 10 * s, 32 * s), GUI.color);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 4 * s, cy - 12 * s, 8 * s, 28 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 8 * s, cy - 22 * s, 16 * s, 12 * s), GUI.color);
            GUI.color = light;
            UIShapes.Part(new Rect(cx - 5 * s, cy - 18 * s, 10 * s, 6 * s), GUI.color);
            PortraitEyes(cx, cy - 18 * s, s, a);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 34 * s, cy - 4 * s, 28 * s, 3 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 6 * s, cy - 4 * s, 28 * s, 3 * s), GUI.color);
            UIShapes.Part(new Rect(cx - 30 * s, cy + 8 * s, 24 * s, 3 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 6 * s, cy + 8 * s, 24 * s, 3 * s), GUI.color);
            GUI.color = new Color(0.6f, 0.8f, 1f, 0.15f * a);
            UIShapes.Part(new Rect(cx - 38 * s, cy + 12 * s, 76 * s, 6 * s), GUI.color);
        }

        private static void PortraitDiving(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 20 * s, cy - 12 * s, 40 * s, 26 * s), GUI.color);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 17 * s, cy - 10 * s, 34 * s, 22 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 15 * s, cy - 6 * s, 30 * s, 10 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 10 * s, cy - 24 * s, 20 * s, 14 * s), GUI.color);
            GUI.color = light;
            UIShapes.Part(new Rect(cx - 7 * s, cy - 20 * s, 14 * s, 8 * s), GUI.color);
            PortraitEyes(cx, cy - 18 * s, s, a);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 24 * s, cy + 6 * s, 14 * s, 8 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 10 * s, cy + 6 * s, 14 * s, 8 * s), GUI.color);
            PortraitLegs(cx, cy + 2 * s, s, dark, 3, a);
        }

        private static void PortraitJewel(float cx, float cy, float s, Color body, Color dark, Color light, float a)
        {
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 20 * s, cy - 12 * s, 40 * s, 28 * s), GUI.color);
            GUI.color = body;
            UIShapes.Part(new Rect(cx - 17 * s, cy - 10 * s, 34 * s, 24 * s), GUI.color);
            Color shimmer = new Color(Mathf.Min(1, body.r + 0.25f), Mathf.Min(1, body.g + 0.3f), Mathf.Min(1, body.b + 0.2f), a * 0.7f);
            float shimPulse = 0.4f + Mathf.Sin(Time.time * 3f) * 0.2f;
            GUI.color = new Color(shimmer.r, shimmer.g, shimmer.b, shimPulse * a);
            UIShapes.Part(new Rect(cx - 14 * s, cy - 8 * s, 13 * s, 18 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 1 * s, cy - 8 * s, 13 * s, 18 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 1 * s, cy - 10 * s, 2 * s, 24 * s), GUI.color);
            GUI.color = dark;
            UIShapes.Part(new Rect(cx - 10 * s, cy - 26 * s, 20 * s, 16 * s), GUI.color);
            GUI.color = light;
            UIShapes.Part(new Rect(cx - 7 * s, cy - 22 * s, 14 * s, 10 * s), GUI.color);
            PortraitEyes(cx, cy - 20 * s, s, a);
            PortraitLegs(cx, cy + 4 * s, s, dark, 3, a);
        }

        private static void PortraitSpider(float cx, float cy, float s, Color body, Color dark, Color light, float alpha)
        {
            // 배 (큰 원)
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            UIShapes.Part(new Rect(cx - 16 * s, cy + 2 * s, 32 * s, 26 * s), GUI.color);
            // 가슴 (작은 원)
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            UIShapes.Part(new Rect(cx - 8 * s, cy - 10 * s, 16 * s, 14 * s), GUI.color);
            GUI.color = new Color(light.r, light.g, light.b, alpha);
            UIShapes.Part(new Rect(cx - 6 * s, cy - 8 * s, 12 * s, 10 * s), GUI.color);
            // 8개 다리 (좌우 4쌍)
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            for (int i = 0; i < 4; i++)
            {
                float ly = cy - 4 * s + i * 6 * s;
                UIShapes.Part(new Rect(cx - 36 * s + i * 4 * s, ly, 20 * s, 2 * s), GUI.color);
                UIShapes.Part(new Rect(cx + 16 * s - i * 4 * s, ly, 20 * s, 2 * s), GUI.color);
            }
            // 8눈 (2열)
            GUI.color = new Color(1f, 0f, 0f, 0.8f * alpha);
            UIShapes.Part(new Rect(cx - 5 * s, cy - 16 * s, 3 * s, 3 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 2 * s, cy - 16 * s, 3 * s, 3 * s), GUI.color);
            UIShapes.Part(new Rect(cx - 7 * s, cy - 12 * s, 2 * s, 2 * s), GUI.color);
            UIShapes.Part(new Rect(cx - 3 * s, cy - 12 * s, 2 * s, 2 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 1 * s, cy - 12 * s, 2 * s, 2 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 5 * s, cy - 12 * s, 2 * s, 2 * s), GUI.color);
            // 배 무늬
            GUI.color = new Color(dark.r, dark.g, dark.b, 0.5f * alpha);
            UIShapes.Part(new Rect(cx - 10 * s, cy + 10 * s, 20 * s, 3 * s), GUI.color);
            UIShapes.Part(new Rect(cx - 8 * s, cy + 18 * s, 16 * s, 3 * s), GUI.color);
        }

        private static void PortraitGrasshopper(float cx, float cy, float s, Color body, Color dark, Color light, float alpha)
        {
            // 긴 몸통
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            UIShapes.Part(new Rect(cx - 10 * s, cy - 14 * s, 20 * s, 36 * s), GUI.color);
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            UIShapes.Part(new Rect(cx - 8 * s, cy - 12 * s, 16 * s, 32 * s), GUI.color);
            // 머리
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            UIShapes.Part(new Rect(cx - 10 * s, cy - 28 * s, 20 * s, 16 * s), GUI.color);
            GUI.color = new Color(light.r, light.g, light.b, alpha);
            UIShapes.Part(new Rect(cx - 7 * s, cy - 24 * s, 14 * s, 10 * s), GUI.color);
            PortraitEyes(cx, cy - 22 * s, s, alpha);
            // 큰 뒷다리 (V자)
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            UIShapes.Part(new Rect(cx - 28 * s, cy - 4 * s, 20 * s, 5 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 8 * s, cy - 4 * s, 20 * s, 5 * s), GUI.color);
            UIShapes.Part(new Rect(cx - 32 * s, cy + 2 * s, 6 * s, 18 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 26 * s, cy + 2 * s, 6 * s, 18 * s), GUI.color);
            // 앞다리
            PortraitLegs(cx, cy + 6 * s, s, dark, 2, alpha);
            // 더듬이
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            UIShapes.Part(new Rect(cx - 6 * s, cy - 42 * s, 2 * s, 16 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 4 * s, cy - 42 * s, 2 * s, 16 * s), GUI.color);
        }

        private static void PortraitLadybug(float cx, float cy, float s, Color body, Color dark, Color light, float alpha)
        {
            // 빨간 반원 몸체
            Color red = new Color(0.9f, 0.15f, 0.1f, alpha);
            GUI.color = red;
            UIShapes.Part(new Rect(cx - 22 * s, cy - 10 * s, 44 * s, 28 * s), GUI.color);
            // 중앙선
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            UIShapes.Part(new Rect(cx - 1 * s, cy - 10 * s, 2 * s, 28 * s), GUI.color);
            // 검은 점 5개
            GUI.color = new Color(0.05f, 0.05f, 0.05f, alpha);
            UIShapes.Part(new Rect(cx - 14 * s, cy - 4 * s, 6 * s, 6 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 8 * s, cy - 4 * s, 6 * s, 6 * s), GUI.color);
            UIShapes.Part(new Rect(cx - 16 * s, cy + 8 * s, 6 * s, 6 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 10 * s, cy + 8 * s, 6 * s, 6 * s), GUI.color);
            UIShapes.Part(new Rect(cx - 4 * s, cy + 4 * s, 8 * s, 6 * s), GUI.color);
            // 검은 머리
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            UIShapes.Part(new Rect(cx - 10 * s, cy - 24 * s, 20 * s, 16 * s), GUI.color);
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
                UIShapes.Part(new Rect(cx - 8 * s, my, 16 * s, 10 * s), GUI.color);
                // 각 마디에 짧은 다리
                GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
                UIShapes.Part(new Rect(cx - 18 * s, my + 2 * s, 10 * s, 3 * s), GUI.color);
                UIShapes.Part(new Rect(cx + 8 * s, my + 2 * s, 10 * s, 3 * s), GUI.color);
            }
            // 머리 + 집게
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            UIShapes.Part(new Rect(cx - 10 * s, cy - 34 * s, 20 * s, 16 * s), GUI.color);
            GUI.color = new Color(light.r, light.g, light.b, alpha);
            UIShapes.Part(new Rect(cx - 7 * s, cy - 30 * s, 14 * s, 10 * s), GUI.color);
            PortraitEyes(cx, cy - 28 * s, s, alpha);
            // 집게
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            UIShapes.Part(new Rect(cx - 12 * s, cy - 42 * s, 4 * s, 10 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 8 * s, cy - 42 * s, 4 * s, 10 * s), GUI.color);
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
                UIShapes.Part(new Rect(cx - size * 0.5f, my, size, size), GUI.color);
                // 짧은 다리들
                GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
                UIShapes.Part(new Rect(cx - size * 0.5f - 4 * s, my + 3 * s, 4 * s, 3 * s), GUI.color);
                UIShapes.Part(new Rect(cx + size * 0.5f, my + 3 * s, 4 * s, 3 * s), GUI.color);
            }
            // 큰 눈 (머리 위)
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            UIShapes.Part(new Rect(cx - 10 * s, cy - 28 * s, 20 * s, 18 * s), GUI.color);
            GUI.color = new Color(light.r, light.g, light.b, alpha);
            UIShapes.Part(new Rect(cx - 7 * s, cy - 24 * s, 14 * s, 12 * s), GUI.color);
            // 큰 눈
            GUI.color = new Color(1f, 1f, 1f, alpha);
            UIShapes.Part(new Rect(cx - 7 * s, cy - 24 * s, 6 * s, 8 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 1 * s, cy - 24 * s, 6 * s, 8 * s), GUI.color);
            GUI.color = new Color(0f, 0f, 0f, alpha);
            UIShapes.Part(new Rect(cx - 4 * s, cy - 21 * s, 3 * s, 3 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 3 * s, cy - 21 * s, 3 * s, 3 * s), GUI.color);
            // 더듬이
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            UIShapes.Part(new Rect(cx - 6 * s, cy - 38 * s, 2 * s, 12 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 4 * s, cy - 38 * s, 2 * s, 12 * s), GUI.color);
        }

        private static void PortraitStickInsect(float cx, float cy, float s, Color body, Color dark, Color light, float alpha)
        {
            // 매우 가느다란 직선 몸
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            UIShapes.Part(new Rect(cx - 3 * s, cy - 30 * s, 6 * s, 60 * s), GUI.color);
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            UIShapes.Part(new Rect(cx - 2 * s, cy - 28 * s, 4 * s, 56 * s), GUI.color);
            // 머리
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            UIShapes.Part(new Rect(cx - 5 * s, cy - 38 * s, 10 * s, 10 * s), GUI.color);
            PortraitEyes(cx, cy - 36 * s, s * 0.7f, alpha);
            // 가는 다리 3쌍
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            for (int i = 0; i < 3; i++)
            {
                float ly = cy - 16 * s + i * 16 * s;
                UIShapes.Part(new Rect(cx - 24 * s, ly, 20 * s, 2 * s), GUI.color);
                UIShapes.Part(new Rect(cx + 4 * s, ly, 20 * s, 2 * s), GUI.color);
            }
            // 더듬이
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            UIShapes.Part(new Rect(cx - 4 * s, cy - 50 * s, 2 * s, 14 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 2 * s, cy - 50 * s, 2 * s, 14 * s), GUI.color);
        }

        private static void PortraitFly(float cx, float cy, float s, Color body, Color dark, Color light, float alpha)
        {
            // 작은 몸
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            UIShapes.Part(new Rect(cx - 8 * s, cy - 6 * s, 16 * s, 20 * s), GUI.color);
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            UIShapes.Part(new Rect(cx - 6 * s, cy - 4 * s, 12 * s, 16 * s), GUI.color);
            // 거대한 빨간 눈 (원 2개)
            GUI.color = new Color(0.8f, 0.1f, 0.05f, alpha);
            UIShapes.Part(new Rect(cx - 16 * s, cy - 24 * s, 14 * s, 14 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 2 * s, cy - 24 * s, 14 * s, 14 * s), GUI.color);
            // 눈 하이라이트
            GUI.color = new Color(1f, 1f, 1f, 0.4f * alpha);
            UIShapes.Part(new Rect(cx - 12 * s, cy - 22 * s, 4 * s, 4 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 6 * s, cy - 22 * s, 4 * s, 4 * s), GUI.color);
            // 투명 날개 2장
            GUI.color = new Color(light.r, light.g, light.b, 0.25f * alpha);
            UIShapes.Part(new Rect(cx - 30 * s, cy - 14 * s, 22 * s, 16 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 8 * s, cy - 14 * s, 22 * s, 16 * s), GUI.color);
            PortraitLegs(cx, cy + 4 * s, s, dark, 3, alpha);
        }

        private static void PortraitPillBug(float cx, float cy, float s, Color body, Color dark, Color light, float alpha)
        {
            // 둥근 몸
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            UIShapes.Part(new Rect(cx - 20 * s, cy - 14 * s, 40 * s, 30 * s), GUI.color);
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            UIShapes.Part(new Rect(cx - 17 * s, cy - 12 * s, 34 * s, 26 * s), GUI.color);
            // 마디 줄 4개
            GUI.color = new Color(dark.r, dark.g, dark.b, 0.4f * alpha);
            for (int i = 0; i < 4; i++)
            {
                float ly = cy - 8 * s + i * 6 * s;
                UIShapes.Part(new Rect(cx - 15 * s, ly, 30 * s, 2 * s), GUI.color);
            }
            // 머리
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            UIShapes.Part(new Rect(cx - 10 * s, cy - 24 * s, 20 * s, 12 * s), GUI.color);
            PortraitEyes(cx, cy - 20 * s, s, alpha);
            // 작은 다리들
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            for (int i = 0; i < 5; i++)
            {
                float lx = cx - 14 * s + i * 7 * s;
                UIShapes.Part(new Rect(lx, cy + 16 * s, 4 * s, 6 * s), GUI.color);
            }
            // 더듬이
            UIShapes.Part(new Rect(cx - 6 * s, cy - 34 * s, 2 * s, 12 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 4 * s, cy - 34 * s, 2 * s, 12 * s), GUI.color);
        }

        private static void PortraitLonghorn(float cx, float cy, float s, Color body, Color dark, Color light, float alpha)
        {
            // 딱정벌레 몸체
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            UIShapes.Part(new Rect(cx - 20 * s, cy - 12 * s, 40 * s, 28 * s), GUI.color);
            GUI.color = new Color(body.r, body.g, body.b, alpha);
            UIShapes.Part(new Rect(cx - 17 * s, cy - 10 * s, 34 * s, 24 * s), GUI.color);
            // 딱지날개 중앙선
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            UIShapes.Part(new Rect(cx - 1 * s, cy - 10 * s, 2 * s, 24 * s), GUI.color);
            // 머리
            UIShapes.Part(new Rect(cx - 10 * s, cy - 26 * s, 20 * s, 16 * s), GUI.color);
            GUI.color = new Color(light.r, light.g, light.b, alpha);
            UIShapes.Part(new Rect(cx - 7 * s, cy - 22 * s, 14 * s, 10 * s), GUI.color);
            PortraitEyes(cx, cy - 20 * s, s, alpha);
            // 초장 더듬이 2개 (몸보다 긴 라인)
            GUI.color = new Color(dark.r, dark.g, dark.b, alpha);
            UIShapes.Part(new Rect(cx - 8 * s, cy - 58 * s, 2 * s, 34 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 6 * s, cy - 58 * s, 2 * s, 34 * s), GUI.color);
            // 더듬이 끝 구부림
            UIShapes.Part(new Rect(cx - 14 * s, cy - 60 * s, 8 * s, 2 * s), GUI.color);
            UIShapes.Part(new Rect(cx + 6 * s, cy - 60 * s, 8 * s, 2 * s), GUI.color);
            PortraitLegs(cx, cy + 4 * s, s, dark, 3, alpha);
        }

        private void DrawMiniIVBar(float x, float y, float w, string label, int iv, float alpha)
        {
            ivLblStyleCache.normal.textColor = new Color(IvLblBase.r, IvLblBase.g, IvLblBase.b, alpha);
            GUI.Label(new Rect(x, y, 50, 22), label, ivLblStyleCache);

            float barX = x + 52;
            float barW = w - 90;
            float barH = 14f;

            GUI.color = new Color(0.15f, 0.15f, 0.2f, alpha);
            UIShapes.Part(new Rect(barX, y + 4, barW, barH), GUI.color);

            float ratio = iv / (float)PlayerInsectData.MaxIV;
            Color bc = GetIVBarColor(iv);
            GUI.color = new Color(bc.r, bc.g, bc.b, alpha);
            UIShapes.Part(new Rect(barX, y + 4, barW * ratio, barH), GUI.color);
            GUI.color = Color.white;

            ivVsStyleCache.normal.textColor = new Color(bc.r, bc.g, bc.b, alpha);
            GUI.Label(new Rect(barX + barW + 6, y, 34, 22), $"{iv}", ivVsStyleCache);
        }

        public static void DrawInsectPortrait(float cx, float cy, InsectRarity rarity, float alpha)
        {
            Color col = UITheme.Instance.GetInsectRarityColor(rarity);
            Color bodyCol = new Color(col.r, col.g, col.b, alpha);
            Color darkCol = new Color(col.r * 0.6f, col.g * 0.6f, col.b * 0.6f, alpha);
            Color lightCol = new Color(
                Mathf.Min(1, col.r + 0.3f), Mathf.Min(1, col.g + 0.3f), Mathf.Min(1, col.b + 0.3f), alpha);

            GUI.color = new Color(bodyCol.r, bodyCol.g, bodyCol.b, 0.15f * alpha);
            UIShapes.Part(new Rect(cx - 40, cy - 40, 80, 80), GUI.color);

            GUI.color = darkCol;
            UIShapes.Part(new Rect(cx - 24, cy - 18, 48, 28), GUI.color);
            GUI.color = bodyCol;
            UIShapes.Part(new Rect(cx - 20, cy - 15, 40, 22), GUI.color);
            GUI.color = darkCol;
            UIShapes.Part(new Rect(cx - 10, cy - 30, 20, 16), GUI.color);
            GUI.color = lightCol;
            UIShapes.Part(new Rect(cx - 7, cy - 27, 14, 10), GUI.color);
            GUI.color = new Color(1, 1, 1, alpha);
            UIShapes.Part(new Rect(cx - 6, cy - 24, 4, 4), GUI.color);
            UIShapes.Part(new Rect(cx + 2, cy - 24, 4, 4), GUI.color);
            GUI.color = bodyCol;
            UIShapes.Part(new Rect(cx - 2, cy - 42, 2, 14), GUI.color);
            UIShapes.Part(new Rect(cx + 2, cy - 42, 2, 14), GUI.color);
            GUI.color = lightCol;
            UIShapes.Part(new Rect(cx - 4, cy - 44, 3, 3), GUI.color);
            UIShapes.Part(new Rect(cx + 3, cy - 44, 3, 3), GUI.color);
            GUI.color = darkCol;
            UIShapes.Part(new Rect(cx - 18, cy + 10, 8, 12), GUI.color);
            UIShapes.Part(new Rect(cx - 6, cy + 10, 8, 14), GUI.color);
            UIShapes.Part(new Rect(cx + 4, cy + 10, 8, 14), GUI.color);
            UIShapes.Part(new Rect(cx + 12, cy + 10, 8, 12), GUI.color);
            GUI.color = new Color(col.r, col.g, col.b, 0.3f * alpha);
            UIShapes.Part(new Rect(cx - 30, cy - 8, 6, 18), GUI.color);
            UIShapes.Part(new Rect(cx + 26, cy - 8, 6, 18), GUI.color);
            GUI.color = Color.white;
        }

        public static Color GetInsectColor(string insectId, InsectRarity rarity)
        {
            if (string.IsNullOrEmpty(insectId)) return UITheme.Instance.GetInsectRarityColor(rarity);

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
