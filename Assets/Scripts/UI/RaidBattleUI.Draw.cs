using System.Collections.Generic;
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// <see cref="RaidBattleUI"/>의 <b>렌더 절반</b> — GUIStyle 캐시와 InitStyles, 그리고 모든 Draw* 메서드.
    ///
    /// 상태기계(페이즈 전이·입력·컨트롤러 이벤트)는 <c>RaidBattleUI.cs</c>에 남는다. 한 파일일 때
    /// 3500줄 중 렌더가 2600줄을 차지해, <b>페이즈 타이밍을 바꾼 변경이 드로잉 타임라인과 어긋나도
    /// 같은 화면에 보이지 않았다</b> — 실제로 유나이트 종료 상한이 2.5s에서 1.15s로 바뀌었는데
    /// 2.5s 기준으로 쓰인 이 파일의 <c>DrawUniteAttackAnimation</c>이 그대로 남아 폭발·TOTAL이
    /// 통째로 렌더되지 않았다(2026-08-06 audit).
    ///
    /// 담당 경계는 <c>rules/agent-coordination.md</c>의 RaidBattleUI 행을 그대로 따른다 —
    /// 레이아웃·Rect는 ui-dev, AOE·유나이트 이펙트는 visual-dev.
    /// </summary>
    /// <summary>
    /// 레이드 팀 슬롯의 화면 x 좌표. 팀 러시 투사체·AOE 피해 팝업·단일 피격 연출 세 곳이 같은 식을
    /// 따로 갖고 있었고, 그중 <b>팀 러시만 1인 팀 중앙 정렬을 빠뜨려</b> 마지막 한 마리가 화면 왼쪽에
    /// 붙었다. 순수 계산이라 테스트로 고정한다.
    /// </summary>
    public static class RaidSlotLayout
    {
        public const float StartRatio = 0.15f;
        public const float SpanRatio = 0.7f;

        public static float AnchorX(int slot, int teamCount, float screenWidth)
        {
            if (teamCount <= 1) return screenWidth * 0.5f;
            int safeSlot = Mathf.Clamp(slot, 0, teamCount - 1);
            return screenWidth * StartRatio
                + safeSlot * (screenWidth * SpanRatio / (teamCount - 1));
        }
    }

    public partial class RaidBattleUI
    {
        // GUIStyle 캐싱 (OnGUI에서 매 프레임 할당 방지)
        private bool stylesInitialized;
        private GUIStyle raidTurnStyleCache;       // DrawOverlay (3D arena)
        private GUIStyle turnStyleCache;            // DrawOverlay (2D)
        private GUIStyle crownStyleCache;           // DrawBossSprite "BOSS"
        private GUIStyle bossNameStyleCache;        // DrawBossSprite displayName
        private GUIStyle teamNumStyleCache;         // DrawTeamField slot index
        private GUIStyle bossHpNameStyleCache;      // DrawBossHpBar name
        private GUIStyle bossHpLvStyleCache;        // DrawBossHpBar level
        private GUIStyle bossHpMiniStatStyleCache;  // DrawBossHpBar atk/def
        private GUIStyle bossHpTextStyleCache;      // DrawBossHpBar hp text
        private GUIStyle teamHpNameStyleCache;      // DrawTeamHpBars name
        private GUIStyle teamHpTextStyleCache;      // DrawTeamHpBars hp text
        private GUIStyle teamHpLvStyleCache;        // DrawTeamHpBars level
        private GUIStyle introBossNameStyleCache;   // DrawIntro boss name
        private GUIStyle introSubStyleCache;        // DrawIntro subtitle
        private GUIStyle insectSelHeaderStyleCache; // DrawInsectSelector header
        private GUIStyle insectSelKeyStyleCache;    // DrawInsectSelector key
        private GUIStyle insectSelNameStyleCache;   // DrawInsectSelector name
        private GUIStyle insectSelHpStyleCache;     // DrawInsectSelector hp
        private GUIStyle skillSelHeaderStyleCache;  // DrawSkillSelector header
        private GUIStyle skillSelKeyStyleCache;     // DrawSkillSelector key
        private GUIStyle skillSelNameStyleCache;    // DrawSkillSelector name
        private GUIStyle skillSelTypeStyleCache;    // DrawSkillSelector type
        private GUIStyle skillSelInfoStyleCache;    // DrawSkillSelector info
        private GUIStyle skillSelCdStyleCache;      // DrawSkillSelector cooldown active
        private GUIStyle skillSelCdInfoStyleCache;  // DrawSkillSelector cooldown info
        private GUIStyle skillSelNoSkillStyleCache; // DrawSkillSelector empty
        private GUIStyle attackSkillNameStyleCache; // DrawAttackEffects skill name
        private GUIStyle aoeLabelStyleCache;        // DrawAttackEffects AOE label
        private GUIStyle aoeDmgStyleCache;          // DrawAttackEffects AOE damage
        private GUIStyle aoeMemberDmgStyleCache;    // DrawAttackEffects per-member AOE dmg
        private GUIStyle attackDmg2StyleCache;      // DrawAttackEffects single-target dmg
        private GUIStyle actionTextStyleCache;      // DrawActionText
        private GUIStyle resultWinSubStyleCache;    // DrawResult win subtitle
        private GUIStyle resultValStyleCache;       // DrawResult value
        private GUIStyle resultBonusStyleCache;     // DrawResult bonus
        private GUIStyle resultWinHintStyleCache;   // DrawResult win hint
        private GUIStyle resultFailStyleCache;      // DrawResult fail title
        private GUIStyle resultFailSubStyleCache;   // DrawResult fail sub
        private GUIStyle resultFailHintStyleCache;  // DrawResult fail hint
        private GUIStyle uniteBtnLabelStyleCache;   // DrawUniteButton label
        private GUIStyle uniteBtnKeyHintStyleCache; // DrawUniteButton key hint
        private GUIStyle uniteGaugeLabelStyleCache; // DrawUniteGaugeBar label
        private GUIStyle uniteGaugeHintStyleCache;  // DrawUniteGaugeBar ready hint
        private GUIStyle uniteSlotDmgStyleCache;    // DrawUniteAttackAnimation slot dmg
        private GUIStyle buffArrowStyleCache;       // DrawBuffDebuffEffect (buff) arrow
        private GUIStyle buffTxtStyleCache;         // DrawBuffDebuffEffect (buff) text
        private GUIStyle debuffArrowStyleCache;     // DrawBuffDebuffEffect (debuff) arrow
        private GUIStyle debuffTxtStyleCache;       // DrawBuffDebuffEffect (debuff) text
        private GUIStyle introRaidBossStyleCache;   // DrawIntro "RAID BOSS"
        private GUIStyle introFightStyleCache;      // DrawIntro "FIGHT!"
        private GUIStyle bossDmgNumStyleCache;      // boss damage number (dynamic fontSize)
        private GUIStyle resultWinTitleStyleCache;  // DrawResult "RAID CLEAR!"
        private GUIStyle uniteLabelStyleCache;      // DrawUniteAttackAnimation "★ 합체공격! ★"
        private GUIStyle uniteTotalStyleCache;      // DrawUniteAttackAnimation total damage
        private GUIStyle bossIntentStyleCache;      // 다음 보스 행동 예고
        private GUIStyle comboStyleCache;           // 동시 팀 러시 콤보
        private GUIStyle slotContribStyleCache;     // 슬롯별 기여(피해/회복/MISS)
        private GUIStyle slotSkillNameStyleCache;   // 서포트가 쓴 스킬 이름

        /// <summary>
        /// 슬롯별 기여 문구를 라운드마다 <b>한 번만</b> 굽는다. OnGUI는 한 프레임에 여러 패스가 돌고
        /// 연출 구간엔 5슬롯이 동시에 그려지므로, 여기서 만들지 않으면 패스마다 문자열 5개가 새로 난다
        /// (도감·지역맵 라운드가 같은 형태를 P1으로 잡았다). 라운드 값은 확정 후 불변이라 캐시가 정확하다.
        /// </summary>
        private string[] slotContribText;

        /// <summary>
        /// 회복량 문구 — <b>받은 슬롯</b>으로 색인한다(시전자 슬롯이 아니다).
        /// 본문(<see cref="slotContribText"/>)과 배열을 나눈 이유: 회복을 받은 슬롯도 자기 행동
        /// (피해 등)이 있어 한 배열에 넣으면 서로 덮어쓴다.
        /// </summary>
        private string[] slotHealText;
        private void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            raidTurnStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            raidTurnStyleCache.normal.textColor = new Color(0.9f, 0.4f, 0.3f);

            turnStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            turnStyleCache.normal.textColor = new Color(1f, 0.6f, 0.2f);

            // DrawBossSprite scale은 항상 5.5f이므로 (int)(20 * 5.5f / 3.5f)=31, (int)(16 * 5.5f / 3f)=29
            crownStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = (int)(20 * 5.5f / 3.5f), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            bossNameStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = (int)(16 * 5.5f / 3f), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            teamNumStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            bossHpNameStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold };

            bossHpLvStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            bossHpLvStyleCache.normal.textColor = new Color(0.75f, 0.75f, 0.75f);

            bossHpMiniStatStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 17 };
            bossHpMiniStatStyleCache.normal.textColor = new Color(0.5f, 0.5f, 0.55f);

            bossHpTextStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bossHpTextStyleCache.normal.textColor = Color.white;

            teamHpNameStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 18, fontStyle = FontStyle.Bold };

            teamHpTextStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            teamHpTextStyleCache.normal.textColor = Color.white;

            teamHpLvStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            teamHpLvStyleCache.normal.textColor = new Color(0.6f, 0.6f, 0.6f);

            introBossNameStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            introSubStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 22, alignment = TextAnchor.MiddleCenter };

            insectSelHeaderStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold };
            insectSelHeaderStyleCache.normal.textColor = new Color(1f, 0.85f, 0.3f);

            insectSelKeyStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold };

            insectSelNameStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 22, fontStyle = FontStyle.Bold };

            insectSelHpStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 19, fontStyle = FontStyle.Bold };

            skillSelHeaderStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            skillSelHeaderStyleCache.normal.textColor = new Color(0.9f, 0.85f, 0.5f);

            skillSelKeyStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            skillSelNameStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 27,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip
            };

            skillSelTypeStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 19 };

            skillSelInfoStyleCache = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };

            skillSelCdStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            skillSelCdStyleCache.normal.textColor = new Color(1f, 0.4f, 0.3f);

            skillSelCdInfoStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 17, alignment = TextAnchor.MiddleRight };
            skillSelCdInfoStyleCache.normal.textColor = new Color(0.68f, 0.68f, 0.74f);

            skillSelNoSkillStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            skillSelNoSkillStyleCache.normal.textColor = new Color(0.5f, 0.5f, 0.5f);

            attackSkillNameStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            aoeLabelStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            aoeDmgStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            aoeMemberDmgStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            attackDmg2StyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            actionTextStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            resultWinSubStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 22, alignment = TextAnchor.MiddleCenter };

            resultValStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            resultBonusStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            resultWinHintStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 18, alignment = TextAnchor.MiddleCenter };

            resultFailStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 52, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            resultFailSubStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 22, alignment = TextAnchor.MiddleCenter };

            resultFailHintStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 18, alignment = TextAnchor.MiddleCenter };

            uniteBtnLabelStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            uniteBtnLabelStyleCache.normal.textColor = new Color(1f, 0.95f, 0.5f);

            uniteBtnKeyHintStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 16, alignment = TextAnchor.MiddleCenter };

            uniteGaugeLabelStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            uniteGaugeHintStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 16, alignment = TextAnchor.MiddleCenter };

            uniteSlotDmgStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            buffArrowStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            buffTxtStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            debuffArrowStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            debuffTxtStyleCache = new GUIStyle(GUI.skin.label)
            { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            // 동적 fontSize 캐시 (호출부에서 fontSize만 갱신)
            introRaidBossStyleCache = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            introFightStyleCache = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bossDmgNumStyleCache = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            resultWinTitleStyleCache = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            uniteLabelStyleCache = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            uniteTotalStyleCache = new GUIStyle(GUI.skin.label)
            { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            bossIntentStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };
            bossIntentStyleCache.normal.textColor = new Color(1f, 0.91f, 0.72f);

            comboStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            slotContribStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            slotSkillNameStyleCache = new GUIStyle(GUI.skin.label)
            {
                fontSize = 21,
                alignment = TextAnchor.MiddleCenter
            };
            comboStyleCache.normal.textColor = new Color(0.45f, 0.95f, 1f);
        }
        private void DrawOverlay()
        {
            float sw = UIScale.VirtualScreenWidth;
            float sh = UIScale.VirtualScreenHeight;

            // 3D 아레나 활성 시 2D 배경 스킵
            if (arena != null && arena.IsActive)
            {
                GUI.color = new Color(0.02f, 0.02f, 0.05f, 0.7f);
                GUI.DrawTexture(new Rect(0, 0, sw, sh * 0.06f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(0, 4, sw, 30), "RAID BATTLE", raidTurnStyleCache);
                return;
            }

            float arenaY = sh * 0.06f;
            float arenaH = sh * 0.49f;
            float horizon = arenaY + arenaH * 0.40f;
            float groundBot = arenaY + arenaH;

            GUI.color = new Color(0.02f, 0.02f, 0.05f, 0.95f);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);

            int skyBands = 6;
            float skyBandH = (horizon - arenaY) / skyBands;
            for (int i = 0; i < skyBands; i++)
            {
                float t = (float)i / skyBands;
                float r = Mathf.Lerp(0.04f, 0.10f, t);
                float g = Mathf.Lerp(0.02f, 0.06f, t);
                float b = Mathf.Lerp(0.08f, 0.18f, t);
                GUI.color = new Color(r, g, b, 1f);
                GUI.DrawTexture(new Rect(0, arenaY + i * skyBandH, sw, skyBandH + 1), Texture2D.whiteTexture);
            }

            int groundBands = 10;
            float groundH = groundBot - horizon;
            for (int i = 0; i < groundBands; i++)
            {
                float t = (float)i / groundBands;
                float bandY = horizon + t * groundH;
                float bandH2 = groundH / groundBands + 1;
                float r = Mathf.Lerp(0.06f, 0.12f, t);
                float g = Mathf.Lerp(0.04f, 0.08f, t);
                float b = Mathf.Lerp(0.06f, 0.10f, t);
                GUI.color = new Color(r, g, b, 1f);
                GUI.DrawTexture(new Rect(0, bandY, sw, bandH2), Texture2D.whiteTexture);

                if (i > 2 && i % 2 == 0)
                {
                    GUI.color = new Color(r + 0.03f, g + 0.02f, b + 0.02f, 0.25f);
                    GUI.DrawTexture(new Rect(0, bandY, sw, 1), Texture2D.whiteTexture);
                }
            }

            GUI.color = new Color(0.2f, 0.1f, 0.15f, 0.5f);
            GUI.DrawTexture(new Rect(0, horizon - 1, sw, 3), Texture2D.whiteTexture);

            DrawPlatformEllipse(sw * 0.5f, arenaY + arenaH * 0.28f, sw * 0.18f, 20f,
                new Color(0.15f, 0.08f, 0.12f, 0.6f), new Color(0.3f, 0.15f, 0.25f, 0.3f));

            float teamPlatY = arenaY + arenaH * 0.82f;
            DrawPlatformEllipse(sw * 0.5f, teamPlatY, sw * 0.35f, 18f,
                new Color(0.1f, 0.12f, 0.18f, 0.5f), new Color(0.2f, 0.25f, 0.35f, 0.3f));

            GUI.color = new Color(0.04f, 0.04f, 0.08f, 0.95f);
            GUI.DrawTexture(new Rect(0, 0, sw, arenaY), Texture2D.whiteTexture);

            float raidPulse = 0.3f + Mathf.Sin(Time.time * 2f) * 0.1f;
            GUI.color = new Color(1f, 0.3f, 0.15f, raidPulse);
            GUI.DrawTexture(new Rect(0, arenaY - 2, sw, 3), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(0, 4, sw, 32),
                $"RAID BATTLE  -  Turn {raidController.TurnNumber + 1}  |  생존: {raidController.AliveCount()}/{raidController.TeamStats.Length}", turnStyleCache);
        }
        private void DrawPlatformEllipse(float cx, float cy, float rx, float ry, Color fill, Color rim)
        {
            int segments = 16;
            for (int i = -segments; i <= segments; i++)
            {
                float t = (float)i / segments;
                float w = rx * 2f * Mathf.Sqrt(Mathf.Max(0, 1f - t * t));
                float h = ry / segments * 2f;
                float sy = cy + t * ry;
                GUI.color = fill;
                GUI.DrawTexture(new Rect(cx - w / 2f, sy, w, Mathf.Max(h, 1f)), Texture2D.whiteTexture);
            }
            for (int i = -segments; i <= segments; i++)
            {
                float t = (float)i / segments;
                float w = rx * 2f * Mathf.Sqrt(Mathf.Max(0, 1f - t * t));
                float sy = cy + t * ry;
                GUI.color = rim;
                GUI.DrawTexture(new Rect(cx - w / 2f, sy, w, 1), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }
        private void DrawBossField()
        {
            // 3D 아레나 활성 시 2D 보스 그리기 스킵
            if (arena != null && arena.IsActive)
                return;

            if (raidController.BossStats == null) return;

            float bx = UIScale.VirtualScreenWidth * 0.5f;
            float by = UIScale.VirtualScreenHeight * 0.12f;
            float breath = Mathf.Sin(Time.time * 1.5f) * 3f;
            by += breath;

            if (bossShake > 0)
            {
                bx += Mathf.Sin(Time.time * 55f) * 12f;
                by += Mathf.Cos(Time.time * 55f) * 8f;
            }

            Color bossGlow = UITheme.Instance.GetInsectColor(raidController.BossStats.Data.insectId, raidController.BossStats.Data.rarity);
            float glowPulse = 0.1f + Mathf.Sin(Time.time * 2.5f) * 0.05f;
            GUI.color = new Color(1f, 0.2f, 0.1f, glowPulse);
            GUI.DrawTexture(new Rect(bx - 120, by - 120, 240, 240), Texture2D.whiteTexture);
            GUI.color = new Color(bossGlow.r, bossGlow.g, bossGlow.b, glowPulse * 0.6f);
            GUI.DrawTexture(new Rect(bx - 100, by - 100, 200, 200), Texture2D.whiteTexture);

            DrawBossSprite(bx, by, raidController.BossStats.Data, 5.5f);
        }
        private void DrawBossSprite(float cx, float cy, InsectData data, float scale)
        {
            Color col = UITheme.Instance.GetInsectColor(data.insectId, data.rarity);
            Color darkCol = new Color(col.r * 0.4f, col.g * 0.4f, col.b * 0.4f);
            Color lightCol = new Color(
                Mathf.Min(1, col.r + 0.3f), Mathf.Min(1, col.g + 0.3f), Mathf.Min(1, col.b + 0.3f));
            Color accentCol = UITheme.Instance.GetInsectRarityColor(data.rarity);
            float s = scale;
            string id = data.insectId ?? "";

            // Boss aura - pulsing red/purple glow rings
            float auraPulse = 0.15f + Mathf.Sin(Time.time * 2.5f) * 0.08f;
            float auraSize2 = 70 * s + Mathf.Sin(Time.time * 1.8f) * 8 * s;
            GUI.color = new Color(0.8f, 0.1f, 0.3f, auraPulse * 0.4f);
            GUI.DrawTexture(new Rect(cx - auraSize2, cy - auraSize2 * 0.7f, auraSize2 * 2, auraSize2 * 1.4f), Texture2D.whiteTexture);
            float auraSize1 = 55 * s + Mathf.Sin(Time.time * 2.2f + 1f) * 6 * s;
            GUI.color = new Color(0.5f, 0.1f, 0.6f, auraPulse * 0.5f);
            GUI.DrawTexture(new Rect(cx - auraSize1, cy - auraSize1 * 0.7f, auraSize1 * 2, auraSize1 * 1.4f), Texture2D.whiteTexture);

            // Type-specific boss sprite
            if (id.Contains("butterfly") || id.Contains("moth") || id.Contains("luna") || id.Contains("atlas"))
                DrawBossButterfly(cx, cy, s, col, darkCol, lightCol, accentCol);
            else if (id.Contains("mantis") || id.Contains("orchid") || id.Contains("ghost"))
                DrawBossMantis(cx, cy, s, col, darkCol, lightCol);
            else if (id.Contains("dragonfly") || id.Contains("damselfly"))
                DrawBossDragonfly(cx, cy, s, col, darkCol, lightCol);
            // "beetle"이 "bee"를 품는다 — 가드가 없으면 아래 stag/rhinoceros/hercules 분기까지
            // 못 가고 딱정벌레가 전부 벌로 그려진다(InsectEntity.BuildModel의 같은 가드와 짝).
            else if ((id.Contains("bee") && !id.Contains("beetle")) || id.Contains("wasp") || id.Contains("hornet"))
                DrawBossBee(cx, cy, s, col, darkCol, lightCol);
            else if (id.Contains("stag") || id.Contains("rhinoceros") || id.Contains("hercules"))
                DrawBossHornBeetle(cx, cy, s, col, darkCol, lightCol);
            else if (id.Contains("spider"))
                DrawBossSpider(cx, cy, s, col, darkCol, lightCol);
            else
                DrawBossDefault(cx, cy, s, col, darkCol, lightCol);

            // Crown label
            float crownPulse = 0.8f + Mathf.Sin(Time.time * 3f) * 0.2f;
            crownStyleCache.normal.textColor = new Color(1f, 0.3f, 0.15f, crownPulse);
            GUI.color = Color.white;
            GUI.Label(new Rect(cx - 40, cy - 66 * s, 80, 24), "BOSS", crownStyleCache);

            bossNameStyleCache.normal.textColor = new Color(col.r, col.g, col.b, 0.9f);
            GUI.Label(new Rect(cx - 60, cy + 34 * s, 120, 22), data.displayName, bossNameStyleCache);

            GUI.color = Color.white;
        }
        private void DrawBossButterfly(float cx, float cy, float s, Color body, Color dark, Color light, Color accent)
        {
            float wingFlap = Mathf.Sin(Time.time * 3f) * 4 * s;
            // Large wings
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.75f);
            GUI.DrawTexture(new Rect(cx - 50 * s, cy - 30 * s + wingFlap, 38 * s, 46 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 12 * s, cy - 30 * s - wingFlap, 38 * s, 46 * s), Texture2D.whiteTexture);
            GUI.color = new Color(light.r, light.g, light.b, 0.55f);
            GUI.DrawTexture(new Rect(cx - 44 * s, cy - 20 * s + wingFlap, 22 * s, 26 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 22 * s, cy - 20 * s - wingFlap, 22 * s, 26 * s), Texture2D.whiteTexture);
            // Lower wings
            GUI.color = new Color(body.r, body.g, body.b, 0.6f);
            GUI.DrawTexture(new Rect(cx - 44 * s, cy + 8 * s + wingFlap, 28 * s, 28 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 16 * s, cy + 8 * s - wingFlap, 28 * s, 28 * s), Texture2D.whiteTexture);
            // Body
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 5 * s, cy - 24 * s, 10 * s, 46 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 4 * s, cy - 22 * s, 8 * s, 42 * s), Texture2D.whiteTexture);
            // Head
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 34 * s, 16 * s, 14 * s), Texture2D.whiteTexture);
            // Eyes
            GUI.color = new Color(1f, 0.1f, 0.1f);
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 30 * s, 5 * s, 5 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2 * s, cy - 30 * s, 5 * s, 5 * s), Texture2D.whiteTexture);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(cx - 4 * s, cy - 28 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 4 * s, cy - 28 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            // Antennae
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 52 * s, 2 * s, 20 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8 * s, cy - 52 * s, 2 * s, 20 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 12 * s, cy - 56 * s, 5 * s, 5 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8 * s, cy - 56 * s, 5 * s, 5 * s), Texture2D.whiteTexture);
            // Wing sparkle pattern
            float sparkle = Mathf.Sin(Time.time * 5f) * 0.3f + 0.4f;
            GUI.color = new Color(1f, 1f, 1f, sparkle);
            GUI.DrawTexture(new Rect(cx - 36 * s, cy - 12 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 32 * s, cy - 12 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        private void DrawBossMantis(float cx, float cy, float s, Color body, Color dark, Color light)
        {
            // Elongated body
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 18 * s, 20 * s, 44 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 16 * s, 16 * s, 40 * s), Texture2D.whiteTexture);
            // Triangular head (large)
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 14 * s, cy - 40 * s, 28 * s, 26 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 36 * s, 20 * s, 16 * s), Texture2D.whiteTexture);
            // Boss eyes (menacing red)
            GUI.color = new Color(1f, 0.1f, 0.1f);
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 34 * s, 6 * s, 6 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 5 * s, cy - 34 * s, 6 * s, 6 * s), Texture2D.whiteTexture);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 32 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 7 * s, cy - 32 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            // Large scythe arms with swing
            float swingAngle = Mathf.Sin(Time.time * 2.5f) * 5 * s;
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 36 * s, cy - 28 * s + swingAngle, 24 * s, 6 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 12 * s, cy - 28 * s - swingAngle, 24 * s, 6 * s), Texture2D.whiteTexture);
            // Scythe blades (bigger than normal)
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 42 * s, cy - 36 * s + swingAngle, 8 * s, 20 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 34 * s, cy - 36 * s - swingAngle, 8 * s, 20 * s), Texture2D.whiteTexture);
            // Blade edge highlights
            GUI.color = new Color(1f, 1f, 1f, 0.4f);
            GUI.DrawTexture(new Rect(cx - 42 * s, cy - 36 * s + swingAngle, 2 * s, 20 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 40 * s, cy - 36 * s - swingAngle, 2 * s, 20 * s), Texture2D.whiteTexture);
            // Legs
            GUI.color = dark;
            for (int i = 0; i < 2; i++)
            {
                float lx = (i - 0.5f) * 14 * s;
                GUI.DrawTexture(new Rect(cx + lx - 4 * s, cy + 22 * s, 8 * s, 18 * s), Texture2D.whiteTexture);
            }
            // Translucent wings
            GUI.color = new Color(body.r, body.g, body.b, 0.2f);
            GUI.DrawTexture(new Rect(cx - 22 * s, cy - 10 * s, 14 * s, 30 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8 * s, cy - 10 * s, 14 * s, 30 * s), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        private void DrawBossDragonfly(float cx, float cy, float s, Color body, Color dark, Color light)
        {
            float wingFlap = Mathf.Sin(Time.time * 5f) * 3 * s;
            // 4 transparent wings (large)
            GUI.color = new Color(light.r, light.g, light.b, 0.3f);
            GUI.DrawTexture(new Rect(cx - 48 * s, cy - 22 * s + wingFlap, 42 * s, 12 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 22 * s - wingFlap, 42 * s, 12 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 42 * s, cy - 8 * s - wingFlap, 36 * s, 10 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 8 * s + wingFlap, 36 * s, 10 * s), Texture2D.whiteTexture);
            // Wing veins
            GUI.color = new Color(light.r, light.g, light.b, 0.15f);
            GUI.DrawTexture(new Rect(cx - 38 * s, cy - 17 * s + wingFlap, 30 * s, 1), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 8 * s, cy - 17 * s - wingFlap, 30 * s, 1), Texture2D.whiteTexture);
            // Body (short thorax)
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 5 * s, cy - 14 * s, 10 * s, 18 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 4 * s, cy - 12 * s, 8 * s, 14 * s), Texture2D.whiteTexture);
            // Long tail (boss has extra-long segmented tail)
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 4 * s, cy + 4 * s, 8 * s, 52 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 3 * s, cy + 6 * s, 6 * s, 48 * s), Texture2D.whiteTexture);
            // Tail segments
            for (int i = 0; i < 6; i++)
            {
                GUI.color = dark;
                GUI.DrawTexture(new Rect(cx - 3 * s, cy + 10 * s + i * 8 * s, 6 * s, 2 * s), Texture2D.whiteTexture);
            }
            // Tail tip glow
            float tailGlow = 0.4f + Mathf.Sin(Time.time * 3f) * 0.2f;
            GUI.color = new Color(body.r, body.g, body.b, tailGlow);
            GUI.DrawTexture(new Rect(cx - 5 * s, cy + 50 * s, 10 * s, 8 * s), Texture2D.whiteTexture);
            // Large compound eyes head
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 14 * s, cy - 32 * s, 28 * s, 20 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 13 * s, cy - 30 * s, 12 * s, 10 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2 * s, cy - 30 * s, 12 * s, 10 * s), Texture2D.whiteTexture);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(cx - 9 * s, cy - 27 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 27 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        private void DrawBossBee(float cx, float cy, float s, Color body, Color dark, Color light)
        {
            // Translucent wings
            GUI.color = new Color(1f, 1f, 1f, 0.3f);
            GUI.DrawTexture(new Rect(cx - 36 * s, cy - 30 * s, 26 * s, 18 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 10 * s, cy - 30 * s, 26 * s, 18 * s), Texture2D.whiteTexture);
            // Large striped abdomen
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 22 * s, cy - 16 * s, 44 * s, 32 * s), Texture2D.whiteTexture);
            // Stripes
            GUI.color = new Color(0.1f, 0.1f, 0.05f);
            for (int i = 0; i < 4; i++)
                GUI.DrawTexture(new Rect(cx - 20 * s, cy - 12 * s + i * 8 * s, 40 * s, 3 * s), Texture2D.whiteTexture);
            // Head
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 30 * s, 20 * s, 16 * s), Texture2D.whiteTexture);
            // Eyes
            GUI.color = new Color(1f, 0.1f, 0.1f);
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 27 * s, 5 * s, 5 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 4 * s, cy - 27 * s, 5 * s, 5 * s), Texture2D.whiteTexture);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 25 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 25 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            // Antennae
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 42 * s, 2 * s, 14 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 5 * s, cy - 42 * s, 2 * s, 14 * s), Texture2D.whiteTexture);
            // Large stinger (boss-sized)
            float stingerPulse = 0.7f + Mathf.Sin(Time.time * 4f) * 0.3f;
            GUI.color = new Color(0.3f, 0.2f, 0.1f, stingerPulse);
            GUI.DrawTexture(new Rect(cx - 3 * s, cy + 16 * s, 6 * s, 16 * s), Texture2D.whiteTexture);
            GUI.color = new Color(0.6f, 0.4f, 0.1f);
            GUI.DrawTexture(new Rect(cx - 2 * s, cy + 28 * s, 4 * s, 8 * s), Texture2D.whiteTexture);
            // Stinger glow
            GUI.color = new Color(1f, 0.6f, 0.1f, 0.3f + Mathf.Sin(Time.time * 3f) * 0.15f);
            GUI.DrawTexture(new Rect(cx - 6 * s, cy + 30 * s, 12 * s, 8 * s), Texture2D.whiteTexture);
            // Legs
            GUI.color = dark;
            for (int i = 0; i < 3; i++)
            {
                float lx = (i - 1f) * 12 * s;
                GUI.DrawTexture(new Rect(cx + lx - 3 * s, cy + 14 * s, 6 * s, 14 * s), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }
        private void DrawBossHornBeetle(float cx, float cy, float s, Color body, Color dark, Color light)
        {
            // Large shell
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 30 * s, cy - 20 * s, 60 * s, 38 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 26 * s, cy - 17 * s, 52 * s, 32 * s), Texture2D.whiteTexture);
            // Shell seam
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 1 * s, cy - 17 * s, 2 * s, 32 * s), Texture2D.whiteTexture);
            // Shell highlight
            GUI.color = new Color(light.r, light.g, light.b, 0.25f);
            GUI.DrawTexture(new Rect(cx - 22 * s, cy - 14 * s, 18 * s, 6 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 14 * s, 18 * s, 6 * s), Texture2D.whiteTexture);
            // Head
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 14 * s, cy - 38 * s, 28 * s, 22 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 34 * s, 20 * s, 14 * s), Texture2D.whiteTexture);
            // Boss-sized horn (main horn)
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 3 * s, cy - 62 * s, 6 * s, 28 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 66 * s, 7 * s, 7 * s), Texture2D.whiteTexture);
            // Side horns
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 18 * s, cy - 44 * s, 6 * s, 14 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 12 * s, cy - 44 * s, 6 * s, 14 * s), Texture2D.whiteTexture);
            // Horn tip glow
            float hornGlow = 0.3f + Mathf.Sin(Time.time * 2f) * 0.2f;
            GUI.color = new Color(1f, 0.4f, 0.1f, hornGlow);
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 68 * s, 10 * s, 10 * s), Texture2D.whiteTexture);
            // Eyes
            GUI.color = new Color(1f, 0.1f, 0.1f);
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 32 * s, 5 * s, 5 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 4 * s, cy - 32 * s, 5 * s, 5 * s), Texture2D.whiteTexture);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 30 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 6 * s, cy - 30 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            // Legs
            GUI.color = dark;
            for (int leg = 0; leg < 3; leg++)
            {
                float lx = (leg - 1f) * 14 * s;
                GUI.DrawTexture(new Rect(cx + lx - 4 * s, cy + 18 * s, 8 * s, 18 * s), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }
        private void DrawBossSpider(float cx, float cy, float s, Color body, Color dark, Color light)
        {
            // Large abdomen
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 24 * s, cy - 6 * s, 48 * s, 36 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 20 * s, cy - 3 * s, 40 * s, 30 * s), Texture2D.whiteTexture);
            // Abdomen pattern
            GUI.color = new Color(dark.r, dark.g, dark.b, 0.5f);
            GUI.DrawTexture(new Rect(cx - 8 * s, cy + 2 * s, 16 * s, 8 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 6 * s, cy + 14 * s, 12 * s, 6 * s), Texture2D.whiteTexture);
            // Cephalothorax + head
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 12 * s, cy - 26 * s, 24 * s, 24 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 9 * s, cy - 22 * s, 18 * s, 16 * s), Texture2D.whiteTexture);
            // Multiple eyes (boss spider = 8 eyes visible)
            GUI.color = new Color(1f, 0.1f, 0.1f);
            GUI.DrawTexture(new Rect(cx - 7 * s, cy - 22 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 3 * s, cy - 22 * s, 4 * s, 4 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 4 * s, cy - 18 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 2 * s, cy - 18 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 21 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 5 * s, cy - 21 * s, 2 * s, 2 * s), Texture2D.whiteTexture);
            // 8 legs (4 per side)
            GUI.color = dark;
            float legWave = Mathf.Sin(Time.time * 2f) * 2 * s;
            for (int i = 0; i < 4; i++)
            {
                float angle = (i - 1.5f) * 0.5f;
                float legLen = 28 * s;
                float leftX = cx - 12 * s - Mathf.Cos(angle) * legLen;
                float leftY = cy - 6 * s + i * 8 * s + legWave * (i % 2 == 0 ? 1 : -1);
                GUI.DrawTexture(new Rect(Mathf.Min(leftX, cx - 12 * s), leftY, Mathf.Abs(cx - 12 * s - leftX) + 2, 3 * s), Texture2D.whiteTexture);
                // Leg joint
                GUI.DrawTexture(new Rect(leftX - 2 * s, leftY, 4 * s, (12 + i * 2) * s), Texture2D.whiteTexture);

                float rightX = cx + 12 * s + Mathf.Cos(angle) * legLen;
                float rightY = cy - 6 * s + i * 8 * s - legWave * (i % 2 == 0 ? 1 : -1);
                GUI.DrawTexture(new Rect(cx + 12 * s, rightY, Mathf.Abs(rightX - cx - 12 * s) + 2, 3 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rightX - 2 * s, rightY, 4 * s, (12 + i * 2) * s), Texture2D.whiteTexture);
            }
            // Fangs
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 5 * s, cy - 12 * s, 3 * s, 8 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 3 * s, cy - 12 * s, 3 * s, 8 * s), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        private void DrawBossDefault(float cx, float cy, float s, Color body, Color dark, Color light)
        {
            // Original boss sprite (default fallback)
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 28 * s, cy - 22 * s, 56 * s, 36 * s), Texture2D.whiteTexture);
            GUI.color = body;
            GUI.DrawTexture(new Rect(cx - 24 * s, cy - 18 * s, 48 * s, 28 * s), Texture2D.whiteTexture);
            GUI.color = dark;
            GUI.DrawTexture(new Rect(cx - 14 * s, cy - 38 * s, 28 * s, 20 * s), Texture2D.whiteTexture);
            GUI.color = light;
            GUI.DrawTexture(new Rect(cx - 10 * s, cy - 34 * s, 20 * s, 14 * s), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.1f, 0.1f);
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 30 * s, 6 * s, 6 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 3 * s, cy - 30 * s, 6 * s, 6 * s), Texture2D.whiteTexture);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(cx - 6 * s, cy - 28 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 5 * s, cy - 28 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            GUI.color = body;
            for (int i = -1; i <= 1; i++)
            {
                float hx = cx + i * 12 * s;
                GUI.DrawTexture(new Rect(hx - 2 * s, cy - 52 * s, 4 * s, 18 * s), Texture2D.whiteTexture);
                GUI.color = light;
                GUI.DrawTexture(new Rect(hx - 3 * s, cy - 56 * s, 6 * s, 6 * s), Texture2D.whiteTexture);
                GUI.color = body;
            }
            GUI.color = dark;
            for (int leg = 0; leg < 4; leg++)
            {
                float lx = (leg - 1.5f) * 10 * s;
                GUI.DrawTexture(new Rect(cx + lx - 4 * s, cy + 14 * s, 8 * s, 18 * s), Texture2D.whiteTexture);
            }
            GUI.color = new Color(body.r, body.g, body.b, 0.4f);
            GUI.DrawTexture(new Rect(cx - 38 * s, cy - 12 * s, 10 * s, 26 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 30 * s, cy - 12 * s, 10 * s, 26 * s), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        private void DrawTeamField()
        {
            // 3D 아레나 활성 시 2D 팀 그리기 스킵
            if (arena != null && arena.IsActive)
                return;

            if (raidController.TeamStats == null) return;
            int count = raidController.TeamStats.Length;
            float totalW = UIScale.VirtualScreenWidth * 0.7f;
            float startX = UIScale.VirtualScreenWidth * 0.15f;
            float y = UIScale.VirtualScreenHeight * 0.40f;
            float spacing = count > 1 ? totalW / (count - 1) : 0;

            for (int i = 0; i < count; i++)
            {
                var stats = raidController.TeamStats[i];
                if (stats == null) continue;

                float ix = count > 1 ? startX + i * spacing : UIScale.VirtualScreenWidth * 0.5f;
                float iy = y;
                float teamBreath = Mathf.Sin(Time.time * 2f + i * 0.8f) * 2f;
                iy += teamBreath;

                if (teamShake != null && i < teamShake.Length && teamShake[i] > 0)
                {
                    ix += Mathf.Sin(Time.time * 55f) * 7f;
                    iy += Mathf.Cos(Time.time * 55f) * 4f;
                }

                bool alive = stats.CurrentHp > 0;
                bool isSelected = (i == selectedSlot);
                float sc = isSelected ? 3.0f : 2.2f;
                float alpha = alive ? 1f : 0.3f;

                if (isSelected && alive)
                {
                    float selPulse = 0.2f + Mathf.Sin(Time.time * 4f) * 0.1f;
                    GUI.color = new Color(1f, 0.85f, 0.2f, selPulse);
                    GUI.DrawTexture(new Rect(ix - 30 * sc, iy - 30 * sc, 60 * sc, 60 * sc), Texture2D.whiteTexture);
                }

                DrawMiniInsect(ix, iy, stats.Data, sc, alpha);

                teamNumStyleCache.normal.textColor = isSelected ? new Color(1f, 0.85f, 0.2f) : new Color(0.7f, 0.7f, 0.7f, alpha);
                GUI.color = Color.white;
                GUI.Label(new Rect(ix - 14, iy + 26 * sc, 28, 24), $"{i + 1}", teamNumStyleCache);
            }
        }
        private void DrawMiniInsect(float cx, float cy, InsectData data, float scale, float alpha)
        {
            Color col = UITheme.Instance.GetInsectColor(data.insectId, data.rarity);
            Color darkCol = new Color(col.r * 0.5f, col.g * 0.5f, col.b * 0.5f, alpha);
            float s = scale;
            string id = data.insectId ?? "";

            // Base body (shared)
            GUI.color = new Color(darkCol.r, darkCol.g, darkCol.b, alpha);
            GUI.DrawTexture(new Rect(cx - 18 * s, cy - 12 * s, 36 * s, 20 * s), Texture2D.whiteTexture);
            GUI.color = new Color(col.r, col.g, col.b, alpha);
            GUI.DrawTexture(new Rect(cx - 15 * s, cy - 10 * s, 30 * s, 16 * s), Texture2D.whiteTexture);

            // Head
            GUI.color = new Color(darkCol.r, darkCol.g, darkCol.b, alpha);
            GUI.DrawTexture(new Rect(cx - 8 * s, cy - 22 * s, 16 * s, 12 * s), Texture2D.whiteTexture);

            // Eyes
            GUI.color = new Color(1, 1, 1, alpha);
            GUI.DrawTexture(new Rect(cx - 5 * s, cy - 18 * s, 3 * s, 3 * s), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 3 * s, cy - 18 * s, 3 * s, 3 * s), Texture2D.whiteTexture);

            // Type-specific accents
            if (id.Contains("butterfly") || id.Contains("moth") || id.Contains("luna") || id.Contains("atlas"))
            {
                // Wings extending to sides
                float wingFlap = Mathf.Sin(Time.time * 4f + cx * 0.1f) * 2 * s;
                GUI.color = new Color(col.r, col.g, col.b, alpha * 0.6f);
                GUI.DrawTexture(new Rect(cx - 28 * s, cy - 14 * s + wingFlap, 12 * s, 18 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 16 * s, cy - 14 * s - wingFlap, 12 * s, 18 * s), Texture2D.whiteTexture);
            }
            // "beetle"이 "bee"를 품는다 — 가드가 없으면 아래 stag/rhinoceros/hercules 분기까지
            // 못 가고 딱정벌레가 전부 벌로 그려진다(InsectEntity.BuildModel의 같은 가드와 짝).
            else if ((id.Contains("bee") && !id.Contains("beetle")) || id.Contains("wasp") || id.Contains("hornet"))
            {
                // Stripes on body
                GUI.color = new Color(0.1f, 0.1f, 0.05f, alpha * 0.7f);
                GUI.DrawTexture(new Rect(cx - 13 * s, cy - 8 * s, 26 * s, 2 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 13 * s, cy - 2 * s, 26 * s, 2 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 13 * s, cy + 4 * s, 26 * s, 2 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("stag") || id.Contains("rhinoceros") || id.Contains("hercules"))
            {
                // Shell line on body
                GUI.color = new Color(darkCol.r, darkCol.g, darkCol.b, alpha * 0.6f);
                GUI.DrawTexture(new Rect(cx - 1 * s, cy - 10 * s, 2 * s, 16 * s), Texture2D.whiteTexture);
                // Small horn
                GUI.DrawTexture(new Rect(cx - 2 * s, cy - 28 * s, 4 * s, 8 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("mantis") || id.Contains("orchid") || id.Contains("ghost"))
            {
                // Triangular head accent (pointy top)
                GUI.color = new Color(col.r, col.g, col.b, alpha * 0.7f);
                GUI.DrawTexture(new Rect(cx - 3 * s, cy - 28 * s, 6 * s, 8 * s), Texture2D.whiteTexture);
                // Small scythe arms
                GUI.DrawTexture(new Rect(cx - 24 * s, cy - 14 * s, 8 * s, 3 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 16 * s, cy - 14 * s, 8 * s, 3 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("dragonfly") || id.Contains("damselfly"))
            {
                // Transparent mini wings
                GUI.color = new Color(col.r, col.g, col.b, alpha * 0.25f);
                GUI.DrawTexture(new Rect(cx - 26 * s, cy - 16 * s, 14 * s, 6 * s), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 12 * s, cy - 16 * s, 14 * s, 6 * s), Texture2D.whiteTexture);
                // Tail extension
                GUI.color = new Color(darkCol.r, darkCol.g, darkCol.b, alpha);
                GUI.DrawTexture(new Rect(cx - 2 * s, cy + 8 * s, 4 * s, 14 * s), Texture2D.whiteTexture);
            }
            else if (id.Contains("spider"))
            {
                // Extra legs on sides
                GUI.color = new Color(darkCol.r, darkCol.g, darkCol.b, alpha);
                for (int i = 0; i < 3; i++)
                {
                    float ly = cy - 8 * s + i * 6 * s;
                    GUI.DrawTexture(new Rect(cx - 26 * s, ly, 10 * s, 2 * s), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(cx + 16 * s, ly, 10 * s, 2 * s), Texture2D.whiteTexture);
                }
            }

            GUI.color = Color.white;
        }
        private void DrawBossHpBar()
        {
            if (raidController.BossStats == null) return;
            var boss = raidController.BossStats;
            float w = Mathf.Min(700, UIScale.VirtualScreenWidth * 0.7f);
            float h = 100f;
            float x = (UIScale.VirtualScreenWidth - w) / 2f;
            float y = UISafeLayout.ContentTop; // 노치/상태바 + 세로 마진 아래로

            GUI.color = new Color(0.05f, 0.03f, 0.08f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

            Color rarityCol = UITheme.Instance.GetInsectRarityColor(boss.Data.rarity);
            GUI.color = new Color(0.9f, 0.15f, 0.1f);
            GUI.DrawTexture(new Rect(x, y, w, 4), Texture2D.whiteTexture);
            GUI.color = new Color(0.9f, 0.15f, 0.1f, 0.3f);
            GUI.DrawTexture(new Rect(x, y + h - 2, w, 2), Texture2D.whiteTexture);

            bossHpNameStyleCache.normal.textColor = rarityCol;
            GUI.color = Color.white;
            UIHelper.LabelFit(new Rect(x + 14, y + 8, w * 0.5f, 30),
                $"BOSS  {boss.Data.displayName}", bossHpNameStyleCache);

            GUI.Label(new Rect(x + w - 130, y + 8, 116, 26), $"Lv.{boss.Level}", bossHpLvStyleCache);

            GUI.Label(new Rect(x + 14, y + 38, w - 28, 20),
                $"ATK {boss.Attack}  DEF {boss.Defense}", bossHpMiniStatStyleCache);

            float barX = x + 14;
            float barY = y + 62;
            float barW = w - 28;
            float barH = 26f;

            GUI.color = new Color(0.12f, 0.08f, 0.12f);
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), Texture2D.whiteTexture);

            float hpRatio = boss.MaxHp > 0 ? displayBossHp / boss.MaxHp : 0;
            Color hpColor = hpRatio > 0.5f ? new Color(0.85f, 0.2f, 0.15f) :
                            hpRatio > 0.2f ? new Color(0.95f, 0.5f, 0.1f) :
                            new Color(0.95f, 0.85f, 0.15f);
            GUI.color = hpColor;
            GUI.DrawTexture(new Rect(barX, barY, barW * Mathf.Clamp01(hpRatio), barH), Texture2D.whiteTexture);
            GUI.color = new Color(hpColor.r + 0.15f, hpColor.g + 0.15f, hpColor.b + 0.15f, 0.35f);
            GUI.DrawTexture(new Rect(barX, barY, barW * Mathf.Clamp01(hpRatio), barH / 3f), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(barX, barY, barW, barH),
                $"{Mathf.CeilToInt(displayBossHp)} / {boss.MaxHp}", bossHpTextStyleCache);
        }
        private void DrawTeamHpBars()
        {
            if (raidController.TeamStats == null) return;
            int count = raidController.TeamStats.Length;
            float barW = 180f;
            float barH = 82f;
            float totalW2 = count * (barW + 10);
            float startX = (UIScale.VirtualScreenWidth - totalW2) / 2f;
            float y = UIScale.VirtualScreenHeight * 0.53f;

            for (int i = 0; i < count; i++)
            {
                var stats = raidController.TeamStats[i];
                if (stats == null) continue;
                bool alive = stats.CurrentHp > 0;
                bool sel = (i == selectedSlot);
                float bx = startX + i * (barW + 10);

                GUI.color = sel ? new Color(0.1f, 0.12f, 0.22f, 0.95f) : new Color(0.05f, 0.06f, 0.10f, alive ? 0.92f : 0.5f);
                GUI.DrawTexture(new Rect(bx, y, barW, barH), Texture2D.whiteTexture);

                Color rarityCol = UITheme.Instance.GetInsectRarityColor(stats.Data.rarity);
                GUI.color = sel ? new Color(1f, 0.85f, 0.2f) : (alive ? rarityCol : new Color(0.3f, 0.3f, 0.3f));
                GUI.DrawTexture(new Rect(bx, y, barW, 4), Texture2D.whiteTexture);

                teamHpNameStyleCache.normal.textColor = alive ? Color.white : new Color(0.4f, 0.4f, 0.4f);
                GUI.color = Color.white;
                UIHelper.LabelFit(new Rect(bx + 8, y + 6, barW - 16, 22),
                    $"{i + 1}. {stats.Data.displayName}", teamHpNameStyleCache);

                float hbX = bx + 8;
                float hbY = y + 32;
                float hbW = barW - 16;
                float hbH = 20;

                GUI.color = new Color(0.12f, 0.12f, 0.18f);
                GUI.DrawTexture(new Rect(hbX, hbY, hbW, hbH), Texture2D.whiteTexture);

                float dhp = displayTeamHp != null && i < displayTeamHp.Length ? displayTeamHp[i] : stats.CurrentHp;
                float ratio = stats.MaxHp > 0 ? dhp / stats.MaxHp : 0;
                Color hpCol = ratio > 0.5f ? new Color(0.3f, 0.85f, 0.35f) :
                              ratio > 0.2f ? new Color(0.95f, 0.8f, 0.2f) :
                              new Color(0.95f, 0.25f, 0.2f);
                GUI.color = alive ? hpCol : new Color(0.3f, 0.15f, 0.15f);
                GUI.DrawTexture(new Rect(hbX, hbY, hbW * Mathf.Clamp01(ratio), hbH), Texture2D.whiteTexture);

                GUI.color = Color.white;
                GUI.Label(new Rect(hbX, hbY, hbW, hbH),
                    alive ? $"{Mathf.CeilToInt(dhp)}/{stats.MaxHp}" : "KO", teamHpTextStyleCache);

                GUI.Label(new Rect(bx + 8, y + 58, barW - 16, 18), $"Lv.{stats.Level}", teamHpLvStyleCache);
            }
        }
        private void DrawIntro()
        {
            if (raidController.BossStats == null) return;

            float alpha = Mathf.Clamp01(introTimer / 0.6f);
            float cx = UIScale.VirtualScreenWidth / 2f;
            float cy = UIScale.VirtualScreenHeight * 0.30f;
            float sw = UIScale.VirtualScreenWidth;
            float sh = UIScale.VirtualScreenHeight;

            Color ec = UITheme.Instance.GetInsectRarityColor(raidController.BossStats.Data.rarity);
            int rarity = (int)raidController.BossStats.Data.rarity;

            // Background rarity effect (Epic=purple pulse, Legendary=gold pulse)
            if (rarity >= 3) // Epic+
            {
                Color bgPulseCol = rarity >= 4
                    ? new Color(1f, 0.8f, 0.2f, 0.06f + Mathf.Sin(Time.time * 2f) * 0.04f) // Legendary gold
                    : new Color(0.5f, 0.15f, 0.7f, 0.06f + Mathf.Sin(Time.time * 2f) * 0.04f); // Epic purple
                GUI.color = bgPulseCol;
                GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
            }

            if (introTimer < 0.7f)
            {
                // "RAID BOSS" text drops from above
                float dropT = Mathf.Clamp01(introTimer / 0.5f);
                float easeT = dropT * dropT * (3f - 2f * dropT); // smoothstep
                float textY = Mathf.Lerp(cy - 120, cy, easeT);
                float scaleAnim = 0.5f + easeT * 0.5f;
                int fontSize = (int)(60 * scaleAnim);

                // Impact flash when text lands
                if (dropT > 0.85f && dropT < 1f)
                {
                    float impactAlpha = (1f - (dropT - 0.85f) / 0.15f) * 0.4f;
                    GUI.color = new Color(1f, 0.3f, 0.15f, impactAlpha);
                    GUI.DrawTexture(new Rect(cx - 300, textY - 10, 600, 80), Texture2D.whiteTexture);
                }

                GUI.color = new Color(0, 0, 0, 0.7f * alpha);
                GUI.DrawTexture(new Rect(cx - 300, textY - 10, 600, 80), Texture2D.whiteTexture);

                // Red glow borders
                GUI.color = new Color(1f, 0.2f, 0.1f, alpha * 0.6f);
                GUI.DrawTexture(new Rect(cx - 300, textY - 10, 600, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 300, textY + 66, 600, 4), Texture2D.whiteTexture);

                introRaidBossStyleCache.fontSize = fontSize;
                introRaidBossStyleCache.normal.textColor = new Color(1f, 0.3f, 0.15f, alpha);
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - 300, textY, 600, 60), "RAID BOSS", introRaidBossStyleCache);
            }
            else if (introTimer < 1.6f)
            {
                // Boss name appears with burning red/rarity-colored effect
                float nameT = Mathf.Clamp01((introTimer - 0.7f) / 0.5f);
                float namePulse = 0.8f + Mathf.Sin(Time.time * 6f) * 0.2f;

                GUI.color = new Color(0, 0, 0, 0.7f);
                GUI.DrawTexture(new Rect(cx - 340, cy - 10, 680, 110), Texture2D.whiteTexture);

                // Rarity-colored border glow
                GUI.color = new Color(ec.r, ec.g, ec.b, 0.6f * namePulse);
                GUI.DrawTexture(new Rect(cx - 340, cy - 10, 680, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 340, cy + 96, 680, 4), Texture2D.whiteTexture);

                // Fire-like shimmer behind name
                float fireOffset1 = Mathf.Sin(Time.time * 8f) * 3f;
                float fireOffset2 = Mathf.Sin(Time.time * 10f + 2f) * 2f;
                GUI.color = new Color(1f, 0.2f, 0.1f, 0.15f * nameT);
                GUI.DrawTexture(new Rect(cx - 250, cy - 2 + fireOffset1, 500, 50), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 0.5f, 0.1f, 0.1f * nameT);
                GUI.DrawTexture(new Rect(cx - 200, cy + 2 + fireOffset2, 400, 40), Texture2D.whiteTexture);

                introBossNameStyleCache.normal.textColor = new Color(ec.r * namePulse, ec.g * namePulse, ec.b * namePulse, nameT);
                GUI.color = Color.white;
                UIHelper.LabelFit(new Rect(cx - 320, cy + 2, 640, 48),
                    $"{raidController.BossStats.Data.displayName}  Lv.{raidController.BossStats.Level}", introBossNameStyleCache);

                introSubStyleCache.normal.textColor = new Color(1f, 0.8f, 0.3f, nameT);
                GUI.Label(new Rect(cx - 250, cy + 56, 500, 30),
                    "5마리가 힘을 합쳐 쓰러뜨려라!", introSubStyleCache);
            }
            else
            {
                // "FIGHT!" text with punch effect
                float fightT = Mathf.Clamp01((introTimer - 1.6f) / 0.4f);
                float fightScale = 1f + Mathf.Max(0, 1f - fightT * 3f) * 0.6f;
                int fightSize = Mathf.RoundToInt(72 * fightScale);

                // Screen flash on FIGHT
                if (fightT < 0.3f)
                {
                    GUI.color = new Color(1f, 1f, 1f, (0.3f - fightT) * 0.5f);
                    GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
                }

                introFightStyleCache.fontSize = fightSize;
                float shakeX = fightT < 0.3f ? Mathf.Sin(Time.time * 60f) * 4f : 0;
                introFightStyleCache.normal.textColor = new Color(1f, 0.9f, 0.2f, Mathf.Clamp01(1f - (fightT - 0.5f) * 3f));
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - 200 + shakeX, cy + 10, 400, 80), "FIGHT!", introFightStyleCache);
            }
        }
        /// <summary>칩 순서 = 화면 배치 순서. 입력 히트테스트도 이 순서를 쓴다.</summary>
        internal static readonly RaidTeamStance[] StanceOrder =
        {
            RaidTeamStance.Assault, RaidTeamStance.Guard, RaidTeamStance.Support
        };

        private static readonly string[] StanceLabels = { "총공격", "수비", "지원" };

        /// <summary>
        /// 팀 스탠스 칩 3개 — 비-리더 AI의 성향을 <b>1탭</b>으로 바꾼다.
        /// 5슬롯 행동을 매 라운드 직접 지정하면 세로 모바일에서 라운드당 10탭이 되므로, 조작은
        /// 리더 선택만 남기고 나머지는 이 성향으로 조종한다(평소 추가 탭 0, 예고를 읽었을 때만 1탭).
        /// </summary>
        private void DrawStanceChips(float panelY, float panelW)
        {
            UITheme theme = UITheme.Instance;
            float chipW = Mathf.Min(116f, (panelW - 72f) / 3f);
            const float chipH = 34f;
            float totalW = chipW * 3f + UITheme.Space.XS * 2f;
            float x = panelW - 30f - totalW;
            float y = panelY + 8f;

            for (int i = 0; i < StanceOrder.Length; i++)
            {
                Rect r = new Rect(x + i * (chipW + UITheme.Space.XS), y, chipW, chipH);
                stanceRects[i] = r;

                bool active = raidController != null && raidController.TeamStance == StanceOrder[i];
                UISurface.Chip(
                    r,
                    StanceLabels[i],
                    active ? theme.accentAmber : theme.surfaceRaised,
                    active ? theme.surfaceBase : theme.textSecondary);
            }

            GUI.color = Color.white;
        }

        private void DrawInsectSelector()
        {
            bool mobile = UIScale.IsMobileLayout;
            bool portrait = UIScale.IsPortrait;   // 레이아웃 형태는 방향 기준 — 가로 모바일 세로형 패널의 곤충 가림 방지
            float panelW = UIScale.VirtualScreenWidth;
            float panelH = UISafeLayout.ClampHeight(portrait ? 380f : 200f);
            // 제스처바(하단 세이프 인셋) + 세로 마진 위로. 배경은 바닥까지 채워 빈틈 방지.
            float panelY = UISafeLayout.ContentBottom - panelH;

            GUI.color = new Color(0.04f, 0.05f, 0.10f, 0.97f);
            GUI.DrawTexture(new Rect(0, panelY, panelW, UIScale.VirtualScreenHeight - panelY), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.6f, 0.15f);
            GUI.DrawTexture(new Rect(0, panelY, panelW, 4), Texture2D.whiteTexture);
            GUI.color = Color.white;

            DrawStanceChips(panelY, panelW);
            // 헤더 폭은 스탠스 칩 앞까지만 — 겹치면 긴 문구가 칩 아래로 깔린다.
            float headerW = Mathf.Max(120f, stanceRects[0].x - 30f - UITheme.Space.S);
            UIHelper.LabelFit(new Rect(30, panelY + 10, headerW, 32),
                mobile ? "공격할 곤충을 선택하세요" : "공격할 곤충을 선택하세요 [1-5]:", insectSelHeaderStyleCache);

            int count = raidController.TeamStats != null ? raidController.TeamStats.Length : 0;
            float btnW = portrait ? (panelW - 84f) / 3f : Mathf.Min(240, (panelW - 60) / Mathf.Max(count, 1));
            float btnH = portrait ? 136f : 120f;
            float baseBtnY = panelY + 52f;
            float btnY = baseBtnY;
            float startX = 30;

            for (int i = 0; i < count; i++)
            {
                var stats = raidController.TeamStats[i];
                if (stats == null) continue;
                bool alive = stats.CurrentHp > 0;
                float bx = portrait
                    ? startX + (i % 3) * (btnW + 12f)
                    : startX + i * (btnW + 12f);
                if (portrait) btnY = baseBtnY + (i / 3) * (btnH + 12f);

                Color bgCol = alive ? new Color(0.08f, 0.10f, 0.20f) : new Color(0.06f, 0.04f, 0.04f);
                if (i == selectedSlot && alive) bgCol = new Color(0.12f, 0.15f, 0.30f);
                GUI.color = bgCol;
                GUI.DrawTexture(new Rect(bx, btnY, btnW, btnH), Texture2D.whiteTexture);

                Color rarityCol = UITheme.Instance.GetInsectRarityColor(stats.Data.rarity);
                GUI.color = alive ? rarityCol : new Color(0.3f, 0.3f, 0.3f);
                GUI.DrawTexture(new Rect(bx, btnY, btnW, 4), Texture2D.whiteTexture);
                if (alive)
                {
                    GUI.DrawTexture(new Rect(bx, btnY, 2, btnH), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(bx + btnW - 2, btnY, 2, btnH), Texture2D.whiteTexture);
                }

                if (!mobile)
                {
                    insectSelKeyStyleCache.normal.textColor = alive ? new Color(1f, 0.85f, 0.3f) : new Color(0.4f, 0.4f, 0.4f);
                    GUI.color = alive ? new Color(0.15f, 0.12f, 0.05f) : new Color(0.06f, 0.06f, 0.06f);
                    GUI.DrawTexture(new Rect(bx + 8, btnY + 8, 32, 32), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(bx + 8, btnY + 8, 32, 32), $"{i + 1}", insectSelKeyStyleCache);
                }

                insectSelNameStyleCache.normal.textColor = alive ? Color.white : new Color(0.35f, 0.35f, 0.35f);
                UIHelper.LabelFit(new Rect(bx + 8, btnY + 44, btnW - 16, 26), stats.Data.displayName, insectSelNameStyleCache);

                float hpRatio = stats.MaxHp > 0 ? (float)stats.CurrentHp / stats.MaxHp : 0;
                insectSelHpStyleCache.normal.textColor = alive ? (hpRatio > 0.5f ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.95f, 0.5f, 0.2f))
                    : new Color(0.5f, 0.2f, 0.2f);
                GUI.Label(new Rect(bx + 8, btnY + 76, btnW - 16, 24),
                    alive ? $"HP {stats.CurrentHp}/{stats.MaxHp}" : "KO", insectSelHpStyleCache);

                Vector2 mouseGui = UIScale.VirtualMousePosition;
                bool hovered = alive && new Rect(bx, btnY, btnW, btnH).Contains(mouseGui);
                if (hovered)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.06f);
                    GUI.DrawTexture(new Rect(bx, btnY, btnW, btnH), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                insectBtnRects[i] = new Rect(bx, btnY, btnW, btnH);
                insectBtnUsable[i] = alive;
            }
            insectBtnCount = count;

            float uniteX = portrait ? startX + 2f * (btnW + 12f) : startX + count * (btnW + 12f) + 20f;
            float uniteY = portrait ? baseBtnY + btnH + 12f : baseBtnY;
            DrawUniteButton(uniteX, uniteY, btnH);
        }
        private void DrawSkillSelector()
        {
            if (selectedSlot < 0 || raidController.TeamStats == null || selectedSlot >= raidController.TeamStats.Length) return;
            var stats = raidController.TeamStats[selectedSlot];
            var skills = raidController.TeamSkills != null && selectedSlot < raidController.TeamSkills.Length
                ? raidController.TeamSkills[selectedSlot] : null;
            var cooldowns = raidController.TeamCooldowns != null && selectedSlot < raidController.TeamCooldowns.Length
                ? raidController.TeamCooldowns[selectedSlot] : null;

            bool mobile = UIScale.IsMobileLayout;
            bool portrait = UIScale.IsPortrait;   // 레이아웃 형태는 방향 기준 — 가로 모바일 세로형 패널의 곤충 가림 방지
            float panelW = UIScale.VirtualScreenWidth;
            float panelH = UISafeLayout.ClampHeight(portrait ? 640f : 300f);
            // 제스처바(하단 세이프 인셋) + 세로 마진 위로. 배경은 바닥까지 채워 빈틈 방지.
            float panelY = UISafeLayout.ContentBottom - panelH;

            GUI.color = new Color(0.03f, 0.04f, 0.09f, 0.97f);
            GUI.DrawTexture(new Rect(0, panelY, panelW, UIScale.VirtualScreenHeight - panelY), Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.5f, 0.9f);
            GUI.DrawTexture(new Rect(0, panelY, panelW, 4), Texture2D.whiteTexture);
            GUI.color = Color.white;

            skillSelHeaderStyleCache.fontSize = mobile ? 27 : 26;
            GUI.Label(new Rect(30, panelY + 8, panelW - 60f, 48),
                mobile ? $"{stats.Data.displayName}의 기술을 선택하세요"
                    : $"{stats.Data.displayName}의 스킬 [Q/W/E/R]  |  ESC: 돌아가기", skillSelHeaderStyleCache);

            int count = skills != null ? Mathf.Min(skills.Length, 4) : 0;
            float btnW = portrait ? (panelW - 76f) * 0.5f : Mathf.Min(300, (panelW - 80) / Mathf.Max(count, 1));
            float btnH = 212f;
            float baseBtnY = panelY + 60f;
            float btnY = baseBtnY;
            float startX = 30;
            string[] keyLabels = { "Q", "W", "E", "R" };

            for (int i = 0; i < count; i++)
            {
                var skill = skills[i];
                if (skill == null) continue;

                float bx = portrait
                    ? startX + (i % 2) * (btnW + 14f)
                    : startX + i * (btnW + 14f);
                if (portrait) btnY = baseBtnY + (i / 2) * (btnH + 14f);
                int cd = cooldowns != null && i < cooldowns.Length ? cooldowns[i] : 0;
                bool canUse = cd <= 0;

                Vector2 mouseGui = UIScale.VirtualMousePosition;
                bool hovered = canUse && new Rect(bx, btnY, btnW, btnH).Contains(mouseGui);

                Color bgCol = hovered ? new Color(0.16f, 0.20f, 0.36f) :
                              canUse ? new Color(0.08f, 0.10f, 0.20f) : new Color(0.05f, 0.05f, 0.07f);
                GUI.color = bgCol;
                GUI.DrawTexture(new Rect(bx, btnY, btnW, btnH), Texture2D.whiteTexture);

                Color borderCol = GetSkillColor(skill.effectType);
                if (canUse)
                {
                    GUI.color = hovered ? Color.white : borderCol;
                    GUI.DrawTexture(new Rect(bx, btnY, btnW, 5), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(bx, btnY + btnH - 3, btnW, 3), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(bx, btnY, 2, btnH), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(bx + btnW - 2, btnY, 2, btnH), Texture2D.whiteTexture);
                }
                else
                {
                    GUI.color = new Color(0.25f, 0.25f, 0.25f);
                    GUI.DrawTexture(new Rect(bx, btnY, btnW, 2), Texture2D.whiteTexture);
                }

                Color skillCol = GetSkillColor(skill.effectType);
                float iconSize = 32f;
                GUI.color = new Color(skillCol.r, skillCol.g, skillCol.b, canUse ? 0.85f : 0.25f);
                GUI.DrawTexture(new Rect(bx + 12, btnY + 12, iconSize, iconSize), Texture2D.whiteTexture);

                if (!mobile)
                {
                    float pulse = 0.5f + Mathf.Sin(Time.time * 3f) * 0.15f;
                    skillSelKeyStyleCache.normal.textColor = canUse ? new Color(1f, 0.85f, 0.3f, pulse + 0.5f) : new Color(0.35f, 0.35f, 0.35f);
                    GUI.color = canUse ? new Color(0.15f, 0.12f, 0.05f) : new Color(0.06f, 0.06f, 0.06f);
                    GUI.DrawTexture(new Rect(bx + btnW - 46, btnY + 10, 36, 36), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(bx + btnW - 46, btnY + 10, 36, 36), keyLabels[i], skillSelKeyStyleCache);
                }

                Rect skillCardRect = new Rect(bx, btnY, btnW, btnH);
                SkillCardDetailRows detailRows = SkillUILayout.GetDetailRows(
                    skillCardRect, 136f, 12f, 22f, 2f);
                skillSelNameStyleCache.fontSize = mobile ? 29 : 27;
                skillSelNameStyleCache.normal.textColor = canUse ? Color.white : SkillUILayout.DisabledTextColor;
                GUI.Label(SkillUILayout.GetNameRect(skillCardRect, 50f, 12f, 56f),
                    skill.displayName, skillSelNameStyleCache);

                skillSelTypeStyleCache.normal.textColor = canUse
                    ? SkillUILayout.GetReadableAccent(skillCol)
                    : SkillUILayout.DisabledSecondaryTextColor;
                GUI.Label(new Rect(bx + 12, btnY + 110, btnW - 24, 24),
                    RaidSkillTypeLabel(skill.effectType), skillSelTypeStyleCache);

                // 상성 배지 — 보스에게 강/약(데미지 스킬만). InsectTypeChart는 이미 public.
                if (skill.effectType == SkillEffectType.Damage && raidController.BossStats != null && raidController.BossStats.Data != null)
                {
                    float eff = InsectTypeChart.GetEffectiveness(skill.element,
                        raidController.BossStats.Data.primaryType, raidController.BossStats.Data.secondaryType);
                    if (eff > 1.05f || eff < 0.95f)
                    {
                        bool strong = eff > 1.05f;
                        skillSelTypeStyleCache.normal.textColor = canUse
                            ? (strong ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.45f, 0.4f))
                            : SkillUILayout.DisabledSecondaryTextColor;
                        GUI.Label(detailRows.Effectiveness,
                            strong ? "효과적 ▲" : "비효과 ▼", skillSelTypeStyleCache);
                    }
                }

                skillSelInfoStyleCache.normal.textColor = canUse
                    ? new Color(0.96f, 0.91f, 0.72f)
                    : SkillUILayout.DisabledSecondaryTextColor;
                GUI.Label(detailRows.Power,
                    RaidSkillPowerLabel(skill), skillSelInfoStyleCache);

                if (cd > 0)
                {
                    GUI.Label(detailRows.Cooldown, $"쿨다운 {cd}턴", skillSelCdStyleCache);

                    GUI.color = new Color(1f, 0.3f, 0.2f, 0.12f);
                    GUI.DrawTexture(new Rect(bx, btnY, btnW, btnH), Texture2D.whiteTexture);
                }
                else if (skill.cooldownTurns > 0)
                {
                    GUI.Label(detailRows.Cooldown,
                        $"쿨다운: {skill.cooldownTurns}턴", skillSelCdInfoStyleCache);
                }

                if (hovered)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.06f);
                    GUI.DrawTexture(new Rect(bx, btnY, btnW, btnH), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }

                raidSkillRects[i] = new Rect(bx, btnY, btnW, btnH);
                raidSkillUsable[i] = canUse;
            }
            raidSkillCount = count;

            if (count == 0)
            {
                GUI.Label(new Rect(0, btnY, panelW, btnH), "스킬 없음", skillSelNoSkillStyleCache);
            }

            if (portrait)
                DrawUniteButton(startX, baseBtnY + 2f * (btnH + 14f), 90f);
            else
                DrawUniteButton(startX + count * (btnW + 14) + 20, baseBtnY, btnH);
        }
        // 스킬 효과 타입 라벨(신규 타입 포함).
        private static string RaidSkillTypeLabel(SkillEffectType t)
        {
            switch (t)
            {
                case SkillEffectType.BuffAttack: return "버프 스킬";
                case SkillEffectType.DebuffAttack: return "디버프 스킬";
                case SkillEffectType.Heal: return "회복 스킬";
                case SkillEffectType.PoisonDot: return "중독 스킬";
                case SkillEffectType.Stun: return "기절 스킬";
                case SkillEffectType.DefenseBuff: return "방어 스킬";
                default: return "공격 스킬";
            }
        }
        private static string RaidSkillPowerLabel(InsectSkill skill)
        {
            switch (skill.effectType)
            {
                case SkillEffectType.BuffAttack: return "ATK UP";
                case SkillEffectType.DebuffAttack: return "ATK DOWN";
                case SkillEffectType.Heal: return "HP 회복";
                case SkillEffectType.PoisonDot: return $"지속 피해 {skill.power}";
                case SkillEffectType.Stun: return "보스 기절";
                case SkillEffectType.DefenseBuff: return "DEF UP";
                default: return $"위력: {skill.power}";
            }
        }
        // 인터-턴 중앙 배너 — "팀의 턴". 페이드 인·아웃 + 슬라이드. introFightStyle 재사용.
        private void DrawTeamTurnAnnounce()
        {
            float sw = UIScale.VirtualScreenWidth;
            float cy = UIScale.VirtualScreenHeight * 0.30f;
            float t = 1f - Mathf.Clamp01(announceTimer / TeamTurnAnnounceDuration);
            float alpha = Mathf.Clamp01(Mathf.Clamp01(Mathf.Min(t * 4f, (1f - t) * 4f)) + 0.15f);
            float slide = (1f - Mathf.Clamp01(t * 3f)) * 40f;

            Color accent = new Color(0.4f, 0.9f, 1f);

            GUI.color = new Color(0.03f, 0.04f, 0.09f, 0.72f * alpha);
            GUI.DrawTexture(new Rect(0, cy - 12f, sw, 92f), Texture2D.whiteTexture);
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.85f * alpha);
            GUI.DrawTexture(new Rect(0, cy - 12f, sw, 4f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, cy + 76f, sw, 4f), Texture2D.whiteTexture);

            introFightStyleCache.fontSize = 58;
            introFightStyleCache.normal.textColor = new Color(accent.r, accent.g, accent.b, alpha);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, cy + slide, sw, 80f), "팀의 턴", introFightStyleCache);
        }
        private void DrawBossIntent()
        {
            if (phase == Phase.None || phase == Phase.Intro || phase == Phase.Result
                || phase == Phase.BossAttack
                || raidController == null) return;

            RaidBossIntent intent = activeRound != null
                ? activeRound.BossIntent
                : raidController.NextBossIntent;
            if (intent == null) return;

            float sw = UIScale.VirtualScreenWidth;
            float safeTop = SafeArea.Top / UIScale.Scale;
            float width = Mathf.Min(680f, sw - 48f);
            float x = (sw - width) * 0.5f;
            float y = safeTop + 142f;
            float height = UIScale.IsMobileLayout ? 64f : 54f;
            Color accent = intent.IsArea
                ? new Color(1f, 0.35f, 0.25f)
                : new Color(1f, 0.72f, 0.25f);

            string target = "";
            if (!intent.IsArea
                && intent.TargetSlot >= 0
                && raidController.TeamStats != null
                && intent.TargetSlot < raidController.TeamStats.Length
                && raidController.TeamStats[intent.TargetSlot] != null
                && raidController.TeamStats[intent.TargetSlot].Data != null)
            {
                target = $" → {raidController.TeamStats[intent.TargetSlot].Data.displayName}";
            }

            string name = !string.IsNullOrEmpty(intent.DisplayName)
                ? intent.DisplayName
                : intent.IsArea ? "전체 공격" : "강공격";
            string prefix = phase == Phase.BossTelegraph ? "CASTING" : "NEXT";
            string areaLabel = intent.IsArea ? " · 전원 대상" : target;

            GUI.color = new Color(0.035f, 0.035f, 0.075f, 0.94f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.9f);
            GUI.DrawTexture(new Rect(x, y, 6f, height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y, width, 3f), Texture2D.whiteTexture);

            bossIntentStyleCache.fontSize = UIScale.IsMobileLayout ? 25 : 23;
            bossIntentStyleCache.normal.textColor = new Color(1f, 0.92f, 0.78f);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 16f, y + 5f, width - 32f, height - 12f),
                $"⚠ {prefix} · {name}{areaLabel}", bossIntentStyleCache);

            float progress = phase == Phase.BossTelegraph
                ? Mathf.Clamp01(phaseTimer / BossTelegraphDuration)
                : 0f;
            GUI.color = new Color(0.12f, 0.12f, 0.18f, 0.95f);
            GUI.DrawTexture(new Rect(x + 8f, y + height - 7f, width - 16f, 4f), Texture2D.whiteTexture);
            if (progress > 0f)
            {
                GUI.color = accent;
                GUI.DrawTexture(new Rect(x + 8f, y + height - 7f, (width - 16f) * progress, 4f),
                    Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }
        private void DrawBossTelegraph()
        {
            RaidBossIntent intent = activeRound != null
                ? activeRound.BossIntent
                : raidController != null ? raidController.NextBossIntent : null;
            if (intent == null) return;

            float sw = UIScale.VirtualScreenWidth;
            float sh = UIScale.VirtualScreenHeight;
            float pulse = 0.7f + Mathf.Sin(Time.time * 14f) * 0.2f;
            Color accent = intent.IsArea
                ? new Color(1f, 0.22f, 0.16f)
                : new Color(1f, 0.58f, 0.18f);

            GUI.color = new Color(accent.r, accent.g, accent.b, 0.10f * pulse);
            GUI.DrawTexture(new Rect(0, sh * 0.20f, sw, sh * 0.28f), Texture2D.whiteTexture);
            GUI.color = new Color(0.025f, 0.02f, 0.05f, 0.74f);
            GUI.DrawTexture(new Rect(0, sh * 0.28f, sw, 104f), Texture2D.whiteTexture);
            GUI.color = new Color(accent.r, accent.g, accent.b, pulse);
            GUI.DrawTexture(new Rect(0, sh * 0.28f, sw, 4f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, sh * 0.28f + 100f, sw, 4f), Texture2D.whiteTexture);

            introFightStyleCache.fontSize = UIScale.IsMobileLayout ? 50 : 58;
            introFightStyleCache.normal.textColor = new Color(accent.r, accent.g, accent.b, 1f);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, sh * 0.28f + 4f, sw, 62f),
                intent.IsArea ? "⚠ 보스 전체 공격!" : "⚠ 보스 공격 준비!", introFightStyleCache);

            bossIntentStyleCache.normal.textColor = Color.white;
            GUI.Label(new Rect(0, sh * 0.28f + 60f, sw, 36f),
                intent.DisplayName, bossIntentStyleCache);
        }
        /// <summary>
        /// 라운드가 확정된 직후 슬롯별 기여 문구를 굽는다(상태기계 쪽에서 호출).
        /// <c>Healing</c>·<c>Missed</c>·<c>Capped</c>·<c>KnockedOut</c>은 모델에 원래 있었는데
        /// UI가 한 번도 읽지 않아, 화면엔 <b>합계 하나</b>만 뜨고 누가 무엇을 했는지 알 수 없었다.
        /// </summary>
        private void BuildSlotContributions(RaidRoundResult round)
        {
            int teamCount = raidController != null && raidController.TeamStats != null
                ? raidController.TeamStats.Length
                : 0;
            if (slotContribText == null || slotContribText.Length != teamCount)
                slotContribText = new string[teamCount];
            if (slotHealText == null || slotHealText.Length != teamCount)
                slotHealText = new string[teamCount];
            for (int i = 0; i < slotContribText.Length; i++)
            {
                slotContribText[i] = null;
                slotHealText[i] = null;
            }
            if (round == null) return;

            foreach (RaidActionResult action in round.TeamActions)
            {
                if (action == null || action.SourceSlot < 0
                    || action.SourceSlot >= slotContribText.Length)
                {
                    continue;
                }

                slotContribText[action.SourceSlot] = ContributionLabel(action);

                // 회복만 **받은 슬롯**에 따로 적는다. 시전자 슬롯에 적으면 멀쩡한 곤충 위에 +N이 뜨고
                // 정작 HP가 오른 쪽엔 아무것도 안 나온다 — Stage 4에서 회복 대상이 "생존 아군 중
                // 최저 HP"로 바뀌면서 시전자와 대상이 갈렸는데 표시가 따라가지 않았다.
                if (action.Healing > 0
                    && action.TargetSlot >= 0 && action.TargetSlot < slotHealText.Length)
                {
                    slotHealText[action.TargetSlot] = "+" + action.Healing;
                }
            }
        }

        private static string ContributionLabel(RaidActionResult action)
        {
            if (action.KnockedOut) return "FINISH!";
            if (action.Missed) return "MISS";
            if (action.Damage > 0) return $"-{action.Damage}";
            // 회복량은 여기서 내지 않는다 — 시전자가 아니라 **받은 슬롯** 위에 따로 띄운다.
            if (action.StunApplied) return "기절!";
            if (action.Capped) return "최대치";   // 상한에 걸려 턴만 소비 — 알려야 다음 선택이 달라진다
            return null;
        }

        private Color ContributionColor(RaidActionResult action)
        {
            UITheme theme = UITheme.Instance;
            if (action.Missed) return theme.textMuted;
            if (action.Healing > 0) return theme.accentMint;
            if (action.Capped) return theme.accentAmber;
            if (action.Damage > 0) return GetElementColor(action.Element);
            return theme.textSecondary;
        }

        /// <summary>
        /// 팀 러시 중 슬롯마다 자기 몫을 띄운다 — 피해·회복·MISS·최대치·FINISH,
        /// 그리고 <b>서포트가 무슨 스킬을 썼는지</b>. 후자가 없으면 비-리더가 자기 스킬을 쓰게 된 변화가
        /// 화면에 전혀 드러나지 않는다.
        /// </summary>
        private void DrawSlotContributions(float t)
        {
            if (activeRound == null || slotContribText == null) return;
            if (t < 0.3f) return;

            float sw = UIScale.VirtualScreenWidth;
            float sh = UIScale.VirtualScreenHeight;
            int teamCount = raidController.TeamStats != null
                ? raidController.TeamStats.Length
                : 0;
            if (teamCount <= 0) return;
            float rise = (t - 0.3f) / 0.7f;

            int order = 0;
            foreach (RaidActionResult action in activeRound.TeamActions)
            {
                if (action == null || action.SourceSlot < 0
                    || action.SourceSlot >= slotContribText.Length)
                {
                    continue;
                }

                string label = slotContribText[action.SourceSlot];
                float localT = Mathf.Clamp01(rise - order * 0.06f);
                order++;
                if (string.IsNullOrEmpty(label) || localT <= 0f) continue;

                float x = RaidSlotLayout.AnchorX(action.SourceSlot, teamCount, sw);
                float y = sh * 0.40f - 58f - localT * 44f;
                float alpha = Mathf.Clamp01(1f - localT * 0.7f);
                Color col = ContributionColor(action);

                slotContribStyleCache.normal.textColor =
                    new Color(col.r, col.g, col.b, alpha);
                GUI.color = Color.white;
                UIHelper.LabelFit(new Rect(x - 92f, y, 184f, 34f), label, slotContribStyleCache);

                // 서포트가 고른 스킬 이름 — 라운드마다 달라지는 걸 보여줘야 AI가 도는 게 보인다.
                if (action.Kind == RaidActionKind.SupportSkill
                    && !string.IsNullOrEmpty(action.DisplayName))
                {
                    slotSkillNameStyleCache.normal.textColor =
                        new Color(col.r, col.g, col.b, alpha * 0.9f);
                    UIHelper.LabelFit(new Rect(x - 92f, y + 30f, 184f, 26f),
                        action.DisplayName, slotSkillNameStyleCache);
                }
            }

            // 회복은 슬롯 색인이라 액션 루프와 따로 돈다 — 받은 쪽 위에, 본문보다 한 단 위에 띄운다.
            if (slotHealText != null)
            {
                UITheme theme = UITheme.Instance;
                for (int slot = 0; slot < slotHealText.Length; slot++)
                {
                    if (string.IsNullOrEmpty(slotHealText[slot])) continue;

                    float hx = RaidSlotLayout.AnchorX(slot, teamCount, sw);
                    float hy = sh * 0.40f - 96f - rise * 40f;
                    float hAlpha = Mathf.Clamp01(1f - rise * 0.7f);
                    slotContribStyleCache.normal.textColor = new Color(
                        theme.accentMint.r, theme.accentMint.g, theme.accentMint.b, hAlpha);
                    GUI.color = Color.white;
                    UIHelper.LabelFit(
                        new Rect(hx - 92f, hy, 184f, 34f), slotHealText[slot], slotContribStyleCache);
                }
            }

            GUI.color = Color.white;
        }

        private void DrawAttackEffects()
        {
            float t = Mathf.Clamp01(phaseTimer / 1f);
            float sw = UIScale.VirtualScreenWidth;
            float sh = UIScale.VirtualScreenHeight;

            // 합계(-N)와 별개로 슬롯마다 자기 몫을 띄운다. 아래 블록 밖에 두는 이유는
            // **피해 0인 라운드**(전원 버프·회복·기절)에도 뭘 했는지 보여야 하기 때문이다.
            if (phase == Phase.PlayerAttack)
                DrawSlotContributions(t);

            if (phase == Phase.PlayerAttack && lastDmgToBoss > 0)
            {
                float bossX = sw * 0.5f;
                float bossY = sh * 0.12f;

                // 실제 선택한 리더 스킬을 사용한다. 이전 구현은 항상 skills[0]을 읽어
                // 선택한 기술과 2D/3D 속성 이펙트가 어긋났다.
                Color skillColor = new Color(0.3f, 0.7f, 1f);
                InsectElement element = InsectElement.Bug;
                RaidActionResult leaderAction = FindLeaderAction(activeRound);
                if (leaderAction != null)
                {
                    skillColor = GetSkillColor(leaderAction.EffectType);
                    element = leaderAction.Element;
                }
                Color elemCol = GetElementColor(element);

                if (t < 0.35f)
                {
                    float projT = t / 0.35f;
                    float easeT = projT * projT * (3f - 2f * projT);
                    int teamCount = raidController.TeamStats != null ? raidController.TeamStats.Length : 1;
                    float atkY = sh * 0.40f;
                    if (activeRound != null)
                    {
                        int actionIndex = 0;
                        foreach (RaidActionResult action in activeRound.TeamActions)
                        {
                            if (action == null || action.SourceSlot < 0) continue;
                            if (action.Damage <= 0 && !action.IsSupport) continue;
                            float localT = Mathf.Clamp01((projT - actionIndex * 0.05f) / 0.82f);
                            float localEase = localT * localT * (3f - 2f * localT);
                            float atkX = RaidSlotLayout.AnchorX(action.SourceSlot, teamCount, sw);
                            float px = Mathf.Lerp(atkX, bossX, localEase);
                            float py = Mathf.Lerp(atkY, bossY, localEase)
                                - Mathf.Sin(localEase * Mathf.PI) * (62f + actionIndex * 8f);
                            Color actionColor = GetElementColor(action.Element);
                            float projSize = 12f + (action.IsLeader ? 7f : 2f);

                            GUI.color = new Color(actionColor.r, actionColor.g, actionColor.b, 0.16f);
                            GUI.DrawTexture(new Rect(px - projSize * 1.8f, py - projSize * 1.8f,
                                projSize * 3.6f, projSize * 3.6f), Texture2D.whiteTexture);
                            GUI.color = new Color(actionColor.r, actionColor.g, actionColor.b, 0.88f);
                            GUI.DrawTexture(new Rect(px - projSize / 2f, py - projSize / 2f,
                                projSize, projSize), Texture2D.whiteTexture);
                            actionIndex++;
                        }
                    }
                }

                if (t >= 0.3f && t < 0.7f)
                {
                    float impactT = (t - 0.3f) / 0.4f;
                    DrawElementImpact(bossX, bossY, impactT, element, elemCol);
                }

                if (t >= 0.3f)
                {
                    float dmgT = (t - 0.3f) / 0.7f;

                    // Skill name with colored background flash
                    if (!string.IsNullOrEmpty(lastSkillUsedName))
                    {
                        float skillAlpha = Mathf.Clamp01(1f - dmgT * 1.5f);

                        // Skill name background glow
                        GUI.color = new Color(skillColor.r, skillColor.g, skillColor.b, skillAlpha * 0.15f);
                        GUI.DrawTexture(new Rect(bossX - 170, bossY - 140 - dmgT * 20f, 340, 44), Texture2D.whiteTexture);

                        attackSkillNameStyleCache.normal.textColor = new Color(skillColor.r, skillColor.g, skillColor.b, skillAlpha);
                        GUI.color = Color.white;
                        GUI.Label(new Rect(bossX - 170, bossY - 136 - dmgT * 20f, 340, 36), lastSkillUsedName, attackSkillNameStyleCache);
                    }

                    // Damage number
                    float dmgAlpha = Mathf.Clamp01(1f - dmgT * 0.8f);
                    float dmgScale = 1f + Mathf.Sin(dmgT * Mathf.PI * 0.5f) * 0.3f;
                    bossDmgNumStyleCache.fontSize = (int)(48 * dmgScale);
                    bossDmgNumStyleCache.normal.textColor = new Color(1, 1, 0.3f, dmgAlpha);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(bossX - 70, bossY - 90 - dmgT * 50, 140, 56), $"-{lastDmgToBoss}", bossDmgNumStyleCache);

                    if (activeRound != null && activeRound.TeamActions.Count > 1)
                    {
                        float comboAlpha = Mathf.Clamp01(1f - dmgT * 0.65f);
                        comboStyleCache.normal.textColor = new Color(0.45f, 0.95f, 1f, comboAlpha);
                        GUI.Label(new Rect(bossX - 180, bossY - 32 - dmgT * 36f, 360, 44),
                            $"TEAM RUSH ×{activeRound.TeamActions.Count}", comboStyleCache);
                    }
                }
            }

            if (phase == Phase.PlayerAttack && lastDmgToBoss == 0 && !string.IsNullOrEmpty(lastSkillUsedName))
            {
                DrawBuffDebuffEffect(t);
            }

            if ((phase == Phase.BossAttack || phase == Phase.PlayerAttack) && lastDmgToTeam > 0)
            {
                if (lastAoe)
                {
                    // Full screen red flash for AOE
                    float aoeFlash = Mathf.Clamp01(1f - t * 3f) * 0.3f;
                    if (aoeFlash > 0)
                    {
                        GUI.color = new Color(1f, 0.1f, 0.05f, aoeFlash);
                        GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
                    }

                    // Red energy wash across team area
                    float aoeAlpha = (1f - t) * 0.4f;
                    GUI.color = new Color(0.9f, 0.15f, 0.2f, aoeAlpha);
                    GUI.DrawTexture(new Rect(0, sh * 0.30f, sw, sh * 0.26f), Texture2D.whiteTexture);

                    float wave = Mathf.Sin(t * Mathf.PI * 4f) * 0.15f;
                    GUI.color = new Color(1f, 0.3f, 0.2f, (1f - t) * 0.5f + wave);
                    GUI.DrawTexture(new Rect(0, sh * 0.30f, sw, 3), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(0, sh * 0.56f - 3, sw, 3), Texture2D.whiteTexture);

                    // "AOE Attack!" large text
                    float aoeTextAlpha = Mathf.Clamp01(1f - t * 1.2f);
                    aoeLabelStyleCache.normal.textColor = new Color(1f, 0.2f, 0.15f, aoeTextAlpha);
                    float shakeX = t < 0.5f ? Mathf.Sin(Time.time * 50f) * 5f : 0;
                    GUI.color = Color.white;
                    GUI.Label(new Rect(sw / 2 - 200 + shakeX, sh * 0.33f - t * 15f, 400, 52), "전체 공격!", aoeLabelStyleCache);

                    // Damage number below
                    aoeDmgStyleCache.normal.textColor = new Color(1, 0.3f, 0.3f, 1f - t * 0.6f);
                    GUI.Label(new Rect(sw / 2 - 130, sh * 0.38f - t * 30, 260, 44),
                        $"TOTAL -{lastDmgToTeam}", aoeDmgStyleCache);

                    // Per-team-member damage popup
                    if (raidController.TeamStats != null && t > 0.2f)
                    {
                        int teamCount = raidController.TeamStats.Length;
                        for (int i = 0; i < teamCount; i++)
                        {
                            if (raidController.TeamStats[i] == null) continue;
                            float mx = RaidSlotLayout.AnchorX(i, teamCount, sw);
                            float my = sh * 0.40f;
                            float memberT = Mathf.Clamp01((t - 0.2f - i * 0.05f) / 0.5f);
                            if (memberT > 0)
                            {
                                float mAlpha = Mathf.Clamp01(1f - memberT * 0.8f);
                                int memberDamage = activeRound != null
                                    && i < activeRound.BossDamageBySlot.Length
                                        ? activeRound.BossDamageBySlot[i]
                                        : 0;
                                if (memberDamage <= 0) continue;
                                aoeMemberDmgStyleCache.normal.textColor = new Color(1, 0.4f, 0.3f, mAlpha);
                                GUI.Label(new Rect(mx - 42, my - 40 - memberT * 30, 84, 30),
                                    $"-{memberDamage}", aoeMemberDmgStyleCache);
                            }
                        }
                    }
                }
                else if (lastHitSlot >= 0)
                {
                    int teamCount = raidController.TeamStats.Length;
                    float hx = RaidSlotLayout.AnchorX(lastHitSlot, teamCount, sw);
                    float hy = sh * 0.40f;

                    // Red energy projectile from boss to target
                    if (t < 0.4f)
                    {
                        float projT = t / 0.4f;
                        float easeT = projT * projT * (3f - 2f * projT);
                        float px = Mathf.Lerp(sw * 0.5f, hx, easeT);
                        float py = Mathf.Lerp(sh * 0.12f, hy, easeT) - Mathf.Sin(easeT * Mathf.PI) * 60f;
                        float projSize = 14f;

                        // Red energy glow
                        GUI.color = new Color(1f, 0.15f, 0.1f, 0.2f);
                        GUI.DrawTexture(new Rect(px - projSize * 2, py - projSize * 2, projSize * 4, projSize * 4), Texture2D.whiteTexture);
                        GUI.color = new Color(1f, 0.3f, 0.2f, 0.85f);
                        GUI.DrawTexture(new Rect(px - projSize / 2, py - projSize / 2, projSize, projSize), Texture2D.whiteTexture);
                        GUI.color = new Color(1f, 0.8f, 0.3f, 0.9f);
                        GUI.DrawTexture(new Rect(px - 4, py - 4, 8, 8), Texture2D.whiteTexture);

                        // Trail
                        for (int tr = 1; tr <= 3; tr++)
                        {
                            float trailT = Mathf.Max(0, projT - tr * 0.08f);
                            float trailEase = trailT * trailT * (3f - 2f * trailT);
                            float tx = Mathf.Lerp(sw * 0.5f, hx, trailEase);
                            float ty = Mathf.Lerp(sh * 0.12f, hy, trailEase) - Mathf.Sin(trailEase * Mathf.PI) * 60f;
                            GUI.color = new Color(1f, 0.3f, 0.2f, 0.25f - tr * 0.06f);
                            float ts = projSize * (1f - tr * 0.2f);
                            GUI.DrawTexture(new Rect(tx - ts / 2, ty - ts / 2, ts, ts), Texture2D.whiteTexture);
                        }
                    }

                    if (t >= 0.35f)
                    {
                        float impT = (t - 0.35f) / 0.4f;

                        // Impact flash on hit member
                        float flashAlpha = (1f - Mathf.Clamp01(impT)) * 0.5f;
                        GUI.color = new Color(0.9f, 0.2f, 0.3f, flashAlpha);
                        float fs = 60f + impT * 40f;
                        GUI.DrawTexture(new Rect(hx - fs / 2, hy - fs / 2, fs, fs), Texture2D.whiteTexture);

                        // Damage popup
                        float dmgAlpha2 = Mathf.Clamp01(1f - impT * 0.7f);
                        attackDmg2StyleCache.normal.textColor = new Color(1, 0.3f, 0.3f, dmgAlpha2);
                        GUI.color = Color.white;
                        float shakeX = impT < 0.5f ? Mathf.Sin(Time.time * 50f) * 4f : 0;
                        int actualDamage = activeRound != null
                            && lastHitSlot < activeRound.BossDamageBySlot.Length
                                ? activeRound.BossDamageBySlot[lastHitSlot]
                                : lastDmgToTeam;
                        GUI.Label(new Rect(hx - 48 + shakeX, hy - 60 - impT * 30, 96, 36),
                            $"-{actualDamage}", attackDmg2StyleCache);
                    }
                }
            }
            GUI.color = Color.white;
        }
        private void DrawActionText()
        {
            if (string.IsNullOrEmpty(actionText)) return;
            float alpha = Mathf.Clamp01(actionTimer / 0.5f);
            float cx = UIScale.VirtualScreenWidth / 2f;
            float cy = UIScale.VirtualScreenHeight * 0.50f;

            string[] lines = actionText.Split('\n');
            float lineH = 40f;
            float totalH = lines.Length * lineH;
            float bgW = 700;

            GUI.color = new Color(0, 0, 0, 0.75f * alpha);
            GUI.DrawTexture(new Rect(cx - bgW / 2, cy - 8, bgW, totalH + 18), Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.5f, 0.9f, 0.5f * alpha);
            GUI.DrawTexture(new Rect(cx - bgW / 2, cy - 8, bgW, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - bgW / 2, cy + totalH + 7, bgW, 3), Texture2D.whiteTexture);

            actionTextStyleCache.normal.textColor = new Color(1, 1, 1, alpha);
            GUI.color = Color.white;

            for (int i = 0; i < lines.Length; i++)
                GUI.Label(new Rect(cx - bgW / 2, cy + i * lineH, bgW, lineH), lines[i], actionTextStyleCache);
        }
        private void DrawResult()
        {
            float alpha = Mathf.Clamp01(resultTimer / 0.5f);
            float cx = UIScale.VirtualScreenWidth / 2f;
            float cy = UIScale.VirtualScreenHeight * 0.26f;
            float sw = UIScale.VirtualScreenWidth;
            float sh = UIScale.VirtualScreenHeight;

            bool won = raidController.PlayerWon;

            if (won)
            {
                // Victory: gold-toned overlay
                GUI.color = new Color(0f, 0f, 0f, 0.6f * alpha);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);

                // Gold pulsing border glow
                float goldPulse = 0.5f + Mathf.Sin(Time.time * 3f) * 0.15f;
                GUI.color = new Color(1f, 0.85f, 0.2f, 0.08f * goldPulse * alpha);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);

                // Panel
                GUI.color = new Color(0.02f, 0.02f, 0.05f, 0.85f * alpha);
                GUI.DrawTexture(new Rect(cx - 340, cy - 20, 680, 240), Texture2D.whiteTexture);

                // Gold borders
                GUI.color = new Color(1f, 0.85f, 0.2f, 0.7f * alpha);
                GUI.DrawTexture(new Rect(cx - 340, cy - 20, 680, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 340, cy + 216, 680, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 340, cy - 20, 4, 240), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx + 336, cy - 20, 4, 240), Texture2D.whiteTexture);
                GUI.color = Color.white;

                // "RAID CLEAR!" title with scale-in
                float titleScale = 1f + Mathf.Max(0, 1f - resultTimer * 3f) * 0.4f;
                int titleFs = Mathf.RoundToInt(56 * titleScale);
                resultWinTitleStyleCache.fontSize = titleFs;
                resultWinTitleStyleCache.normal.textColor = new Color(1f, 0.85f, 0.2f, alpha);
                GUI.Label(new Rect(cx - 300, cy, 600, 62), "RAID CLEAR!", resultWinTitleStyleCache);

                // Capture message
                resultWinSubStyleCache.normal.textColor = new Color(0.9f, 0.9f, 0.9f, alpha);
                UIHelper.LabelFit(new Rect(cx - 300, cy + 68, 600, 28),
                    $"보스 {raidController.BossStats.Data.displayName}을(를) 포획했다!", resultWinSubStyleCache);

                // Animated reward display
                float rewardDelay = 0.8f;
                float candyAlpha = Mathf.Clamp01((resultTimer - rewardDelay) * 3f);
                float xpAlpha = Mathf.Clamp01((resultTimer - rewardDelay - 0.2f) * 3f);
                float bonusAlpha = Mathf.Clamp01((resultTimer - rewardDelay - 0.4f) * 3f);

                // Candy reward with bounce
                float candyBounce = candyAlpha > 0 ? Mathf.Max(0, Mathf.Sin((resultTimer - rewardDelay) * 8f) * (1f - candyAlpha) * 10f) : 0;
                resultValStyleCache.normal.textColor = new Color(1f, 0.5f, 0.8f, candyAlpha);
                GUI.Label(new Rect(cx - 200, cy + 100 - candyBounce, 200, 28), $"+{raidController.RewardCandy} Candy (x3)", resultValStyleCache);

                // XP reward with bounce
                float xpBounce = xpAlpha > 0 ? Mathf.Max(0, Mathf.Sin((resultTimer - rewardDelay - 0.2f) * 8f) * (1f - xpAlpha) * 10f) : 0;
                resultValStyleCache.normal.textColor = new Color(0.4f, 0.8f, 1f, xpAlpha);
                GUI.Label(new Rect(cx, cy + 100 - xpBounce, 200, 28), $"+{raidController.RewardExp} XP (x3)", resultValStyleCache);

                // Bonus text
                resultBonusStyleCache.normal.textColor = new Color(0.6f, 1f, 0.6f, bonusAlpha);
                GUI.Label(new Rect(cx - 220, cy + 140, 440, 26), "레이드 보너스: 보상 x3!", resultBonusStyleCache);

                // Sparkle effects around rewards
                if (resultTimer > rewardDelay)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        float sparkT = (resultTimer - rewardDelay + i * 0.3f) % 1.5f;
                        float sparkAlpha2 = Mathf.Clamp01(1f - sparkT) * 0.6f;
                        float sparkX = cx - 200 + Mathf.Sin(i * 2.1f + Time.time * 2f) * 180;
                        float sparkY = cy + 100 + Mathf.Cos(i * 1.7f + Time.time * 1.5f) * 30;
                        GUI.color = new Color(1f, 0.9f, 0.4f, sparkAlpha2 * alpha);
                        GUI.DrawTexture(new Rect(sparkX, sparkY, 4, 4), Texture2D.whiteTexture);
                    }
                }

                // Timer hint
                float hintAlpha = Mathf.Clamp01(resultTimer - 2f);
                resultWinHintStyleCache.normal.textColor = new Color(0.6f, 0.6f, 0.6f, hintAlpha * 0.7f);
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - 150, cy + 180, 300, 24), "잠시 후 자동으로 돌아갑니다...", resultWinHintStyleCache);
            }
            else
            {
                // Defeat: dark red overlay
                GUI.color = new Color(0.05f, 0f, 0f, 0.75f * alpha);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);

                // Panel
                GUI.color = new Color(0.03f, 0.01f, 0.01f, 0.9f * alpha);
                GUI.DrawTexture(new Rect(cx - 320, cy - 15, 640, 160), Texture2D.whiteTexture);

                // Red borders
                GUI.color = new Color(1f, 0.2f, 0.15f, 0.5f * alpha);
                GUI.DrawTexture(new Rect(cx - 320, cy - 15, 640, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 320, cy + 141, 640, 4), Texture2D.whiteTexture);
                GUI.color = Color.white;

                // "RAID FAILED" with shake
                float failShake = resultTimer < 1f ? Mathf.Sin(Time.time * 30f) * 3f * (1f - resultTimer) : 0;
                resultFailStyleCache.normal.textColor = new Color(1f, 0.25f, 0.2f, alpha);
                GUI.Label(new Rect(cx - 300 + failShake, cy + 10, 600, 62), "RAID FAILED", resultFailStyleCache);

                // Subtitle
                resultFailSubStyleCache.normal.textColor = new Color(0.6f, 0.4f, 0.4f, alpha);
                GUI.Label(new Rect(cx - 250, cy + 80, 500, 28), "팀이 전멸했습니다...", resultFailSubStyleCache);

                // Timer hint
                float hintAlpha = Mathf.Clamp01(resultTimer - 2f);
                resultFailHintStyleCache.normal.textColor = new Color(0.5f, 0.4f, 0.4f, hintAlpha * 0.7f);
                GUI.Label(new Rect(cx - 150, cy + 116, 300, 24), "잠시 후 자동으로 돌아갑니다...", resultFailHintStyleCache);
            }
            GUI.color = Color.white;
        }
        private void DrawUniteButton(float x, float y, float h)
        {
            bool ready = raidController != null && raidController.CanUniteAttack;
            uniteBtnVisible = ready;
            if (!ready)
            {
                uniteBtnRect = Rect.zero;
                return;
            }

            bool mobile = UIScale.IsMobileLayout;
            float bw = mobile ? (UIScale.VirtualScreenWidth - 84f) / 3f : 160f;
            float bh = Mathf.Min(h, 120f);
            float by = y + (h - bh) / 2f;

            float pulse = Mathf.Sin(Time.time * 4f) * 0.15f + 0.85f;
            Color bgCol = new Color(0.8f * pulse, 0.55f * pulse, 0.05f, 0.95f);

            Vector2 mouseGui = UIScale.VirtualMousePosition;
            bool hovered = new Rect(x, by, bw, bh).Contains(mouseGui);
            if (hovered) bgCol = new Color(1f, 0.7f, 0.1f, 0.95f);

            GUI.color = bgCol;
            GUI.DrawTexture(new Rect(x, by, bw, bh), Texture2D.whiteTexture);

            GUI.color = new Color(1f, 0.9f, 0.3f);
            GUI.DrawTexture(new Rect(x, by, bw, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, by + bh - 3, bw, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, by, 3, bh), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + bw - 3, by, 3, bh), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(x, by + 8, bw, 28), "★ 합체공격 ★", uniteBtnLabelStyleCache);

            uniteBtnKeyHintStyleCache.normal.textColor = new Color(1f, 0.8f, 0.3f, pulse);
            GUI.Label(new Rect(x, by + 38, bw, 22), mobile ? "탭하여 사용" : "[F] 또는 클릭", uniteBtnKeyHintStyleCache);

            uniteBtnRect = new Rect(x, by, bw, bh);
        }
        private void DrawUniteGaugeBar()
        {
            if (raidController == null || !raidController.IsActive) return;
            if (phase == Phase.Intro || phase == Phase.Result) return;

            float gauge = raidController.UniteGauge;
            float max = RaidBattleController.UniteGaugeMax;
            float ratio = Mathf.Clamp01(gauge / max);
            bool ready = raidController.CanUniteAttack;

            float barW = 360f;
            float barH = 36f;
            float bx = (UIScale.VirtualScreenWidth - barW) / 2f;
            float by = UISafeLayout.ContentBottom - 160f; // 하단 패널(ContentBottom 기준)과 상대 위치 유지

            GUI.color = new Color(0.05f, 0.05f, 0.1f, 0.9f);
            GUI.DrawTexture(new Rect(bx - 4, by - 4, barW + 8, barH + 28), Texture2D.whiteTexture);

            Color borderCol = ready
                ? Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.5f, 0.1f), Mathf.Sin(Time.time * 6f) * 0.5f + 0.5f)
                : new Color(0.3f, 0.3f, 0.4f);
            GUI.color = borderCol;
            GUI.DrawTexture(new Rect(bx - 4, by - 4, barW + 8, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bx - 4, by + barH + 2, barW + 8, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bx - 4, by - 4, 2, barH + 8), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(bx + barW + 2, by - 4, 2, barH + 8), Texture2D.whiteTexture);

            GUI.color = new Color(0.1f, 0.1f, 0.15f);
            GUI.DrawTexture(new Rect(bx, by, barW, barH), Texture2D.whiteTexture);

            if (ratio > 0)
            {
                Color fillBot = ready ? new Color(1f, 0.7f, 0.1f) : new Color(0.2f, 0.5f, 0.9f);
                Color fillTop = ready ? new Color(1f, 0.9f, 0.3f) : new Color(0.4f, 0.7f, 1f);
                float halfH = barH / 2f;
                GUI.color = fillBot;
                GUI.DrawTexture(new Rect(bx, by + halfH, barW * ratio, halfH), Texture2D.whiteTexture);
                GUI.color = fillTop;
                GUI.DrawTexture(new Rect(bx, by, barW * ratio, halfH), Texture2D.whiteTexture);

                if (ready)
                {
                    float pulse = Mathf.Sin(Time.time * 4f) * 0.3f + 0.3f;
                    GUI.color = new Color(1f, 1f, 1f, pulse);
                    GUI.DrawTexture(new Rect(bx, by, barW * ratio, barH), Texture2D.whiteTexture);
                }
            }

            uniteGaugeLabelStyleCache.normal.textColor = ready ? new Color(1f, 0.95f, 0.4f) : new Color(0.7f, 0.7f, 0.8f);
            GUI.color = Color.white;
            GUI.Label(new Rect(bx, by, barW, barH),
                ready
                    ? (UIScale.IsMobileLayout ? "★ 합체공격 준비! ★" : "★ 합체공격 준비! [F] ★")
                    : $"합체 게이지  {Mathf.RoundToInt(gauge)}/{Mathf.RoundToInt(max)}",
                uniteGaugeLabelStyleCache);

            if (ready)
            {
                uniteGaugeHintStyleCache.normal.textColor = new Color(1f, 0.8f, 0.3f, Mathf.Sin(Time.time * 3f) * 0.3f + 0.7f);
                GUI.Label(new Rect(bx, by + barH + 2, barW, 22),
                    UIScale.IsMobileLayout ? "합체공격 버튼을 눌러 동시 공격!" : "F키를 눌러 전체 곤충이 동시 공격!",
                    uniteGaugeHintStyleCache);
            }
        }
        private void DrawUniteAttackAnimation()
        {
            float sw = UIScale.VirtualScreenWidth;
            float sh = UIScale.VirtualScreenHeight;
            float t = uniteAnimTimer;

            // Initial bright flash
            float flashAlpha = Mathf.Clamp01(1f - t * 2f) * 0.7f;
            if (flashAlpha > 0)
            {
                GUI.color = new Color(1f, 0.9f, 0.3f, flashAlpha);
                GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
            }

            float bossCx = sw * 0.5f;
            float bossCy = sh * 0.22f;

            // Sequential team member rush
            if (raidController.TeamStats != null)
            {
                int alive = raidController.AliveCount();
                int idx = 0;
                for (int i = 0; i < raidController.TeamStats.Length; i++)
                {
                    if (raidController.TeamStats[i] == null || raidController.TeamStats[i].CurrentHp <= 0) continue;

                    float memberDelay = idx * 0.2f; // Staggered timing per member
                    float startX = sw * 0.1f + idx * (sw * 0.16f);
                    float startY = sh * 0.55f;
                    float progress = Mathf.Clamp01((t - 0.3f - memberDelay) / 0.45f);
                    float cx = Mathf.Lerp(startX, bossCx, progress);
                    float cy = Mathf.Lerp(startY, bossCy, progress) - Mathf.Sin(progress * Mathf.PI) * 90f;

                    InsectData data = raidController.TeamData != null && i < raidController.TeamData.Length ? raidController.TeamData[i] : null;
                    Color memberCol = data != null ? UITheme.Instance.GetInsectColor(data.insectId, data.rarity) : new Color(1f, 0.8f, 0.3f);

                    // Colored energy trail
                    if (progress > 0 && progress < 1f)
                    {
                        for (int tr = 0; tr < 6; tr++)
                        {
                            float trP = Mathf.Max(0, progress - tr * 0.05f);
                            float trX = Mathf.Lerp(startX, bossCx, trP);
                            float trY = Mathf.Lerp(startY, bossCy, trP) - Mathf.Sin(trP * Mathf.PI) * 90f;
                            float trAlpha = (0.4f - tr * 0.06f) * Mathf.Clamp01(progress * 3f);
                            GUI.color = new Color(memberCol.r, memberCol.g, memberCol.b, trAlpha);
                            float trSize = 14 - tr * 1.5f;
                            GUI.DrawTexture(new Rect(trX - trSize, trY - trSize, trSize * 2, trSize * 2), Texture2D.whiteTexture);
                        }
                    }

                    // Member sprite while rushing
                    if (progress > 0 && progress < 1f)
                    {
                        float scale = 2.2f;
                        // Glow aura around rushing member
                        GUI.color = new Color(memberCol.r, memberCol.g, memberCol.b, 0.4f);
                        GUI.DrawTexture(new Rect(cx - scale * 16, cy - scale * 16, scale * 32, scale * 32), Texture2D.whiteTexture);
                        if (data != null)
                            DrawMiniInsect(cx, cy, data, scale, 1f);
                    }

                    // Per-slot hit flash on boss
                    float hitTime = 0.3f + memberDelay + 0.45f;
                    float hitT = t - hitTime;
                    if (hitT > 0 && hitT < 0.3f)
                    {
                        float hitAlpha = (0.3f - hitT) / 0.3f;
                        float hitSize = 50f + hitT * 120f;
                        GUI.color = new Color(memberCol.r, memberCol.g, memberCol.b, hitAlpha * 0.5f);
                        GUI.DrawTexture(new Rect(bossCx - hitSize / 2, bossCy - hitSize / 2, hitSize, hitSize), Texture2D.whiteTexture);
                    }

                    // Per-slot damage number popup
                    int dmg = raidController.UniteSlotDamages != null && i < raidController.UniteSlotDamages.Length
                        ? raidController.UniteSlotDamages[i] : 0;
                    if (hitT > 0 && hitT < 1.2f && dmg > 0)
                    {
                        float dmgAlpha = Mathf.Clamp01(1f - hitT * 0.8f);
                        float dmgY = bossCy - 30f - hitT * 45f + idx * 22f;
                        float dmgX = bossCx - 70 + idx * 28 - alive * 14;
                        uniteSlotDmgStyleCache.normal.textColor = new Color(memberCol.r, Mathf.Min(1, memberCol.g + 0.3f), memberCol.b, dmgAlpha);
                        GUI.color = Color.white;
                        GUI.Label(new Rect(dmgX, dmgY, 140, 40), $"-{dmg}", uniteSlotDmgStyleCache);
                    }
                    idx++;
                }
            }

            // Final combined impact explosion
            float impactT = t - 1.5f;
            if (impactT > 0 && impactT < 1.0f)
            {
                float impactAlpha = Mathf.Clamp01(1f - impactT / 1.0f);

                // Bright center flash
                if (impactT < 0.2f)
                {
                    float brightFlash = (0.2f - impactT) / 0.2f * 0.5f;
                    GUI.color = new Color(1f, 1f, 1f, brightFlash);
                    GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);
                }

                // Expanding shockwave ring (two concentric)
                float ringR1 = impactT * 300f;
                float ringR2 = Mathf.Max(0, impactT - 0.1f) * 280f;
                float ringThick = 6f * impactAlpha;
                GUI.color = new Color(1f, 0.85f, 0.2f, impactAlpha * 0.6f);
                // Outer ring approximation
                GUI.DrawTexture(new Rect(bossCx - ringR1, bossCy - 2, ringR1 * 2, ringThick), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(bossCx - 2, bossCy - ringR1, ringThick, ringR1 * 2), Texture2D.whiteTexture);
                // Diagonal lines
                for (int d = 0; d < 4; d++)
                {
                    float angle = d * 45f * Mathf.Deg2Rad + 22.5f * Mathf.Deg2Rad;
                    float dx = Mathf.Cos(angle) * ringR1;
                    float dy = Mathf.Sin(angle) * ringR1;
                    GUI.DrawTexture(new Rect(bossCx + Mathf.Min(0, dx), bossCy + Mathf.Min(0, dy),
                        Mathf.Abs(dx) + ringThick, ringThick), Texture2D.whiteTexture);
                }

                // Inner explosion sphere
                float sphereR = 30f + impactT * 180f;
                GUI.color = new Color(1f, 0.6f, 0.2f, impactAlpha * 0.4f);
                GUI.DrawTexture(new Rect(bossCx - sphereR, bossCy - sphereR, sphereR * 2, sphereR * 2), Texture2D.whiteTexture);

                // Radial sparks (16 directions)
                for (int sp = 0; sp < 16; sp++)
                {
                    float angle = sp * Mathf.PI * 2f / 16f + impactT * 1.5f;
                    float dist = impactT * 280f;
                    float spx = bossCx + Mathf.Cos(angle) * dist;
                    float spy = bossCy + Mathf.Sin(angle) * dist;
                    float spSize = 10f * impactAlpha;
                    GUI.color = new Color(1f, 0.9f, 0.4f, impactAlpha * 0.8f);
                    GUI.DrawTexture(new Rect(spx - spSize, spy - spSize, spSize * 2, spSize * 2), Texture2D.whiteTexture);
                }
            }

            // "Unite Attack" text with shake effect
            if (t > 0.4f)
            {
                float txtT = t - 0.4f;
                float txtAlpha = Mathf.Clamp01(txtT / 0.3f);
                float scaleVal = 1f + Mathf.Max(0, 1f - txtT * 2.5f) * 0.6f;
                int fs = Mathf.RoundToInt(48 * scaleVal);

                // Text shake during impact period
                float shakeX = 0;
                float shakeY = 0;
                if (t > 1.5f && t < 2.0f)
                {
                    shakeX = Mathf.Sin(Time.time * 45f) * 6f;
                    shakeY = Mathf.Cos(Time.time * 50f) * 3f;
                }

                // Background glow behind text
                GUI.color = new Color(1f, 0.7f, 0.1f, txtAlpha * 0.15f);
                GUI.DrawTexture(new Rect(sw * 0.2f, sh * 0.40f + shakeY, sw * 0.6f, 68), Texture2D.whiteTexture);

                uniteLabelStyleCache.fontSize = fs;
                uniteLabelStyleCache.normal.textColor = new Color(1f, 0.9f, 0.2f, txtAlpha);
                GUI.color = Color.white;
                GUI.Label(new Rect(shakeX, sh * 0.42f + shakeY, sw, 60), "★ 합체공격! ★", uniteLabelStyleCache);
            }

            // Total damage display
            if (lastDmgToBoss > 0 && t > 1.8f)
            {
                float totalT = t - 1.8f;
                float totalAlpha = Mathf.Clamp01(totalT / 0.3f);
                float totalScale = 1f + Mathf.Max(0, 1f - totalT * 2f) * 0.4f;
                int totalFs = Mathf.RoundToInt(52 * totalScale);

                // Red glow background
                GUI.color = new Color(1f, 0.1f, 0.05f, totalAlpha * 0.12f);
                GUI.DrawTexture(new Rect(sw * 0.25f, sh * 0.28f, sw * 0.5f, 68), Texture2D.whiteTexture);

                uniteTotalStyleCache.fontSize = totalFs;
                uniteTotalStyleCache.normal.textColor = new Color(1f, 0.2f, 0.1f, totalAlpha);
                GUI.color = Color.white;
                GUI.Label(new Rect(0, sh * 0.30f, sw, 60), $"TOTAL -{lastDmgToBoss}", uniteTotalStyleCache);
            }
        }
        private void DrawRotatedLine(float x1, float y1, float x2, float y2, float thickness, Color color)
        {
            Vector2 start = new Vector2(x1, y1);
            Vector2 end = new Vector2(x2, y2);
            float length = Vector2.Distance(start, end);
            float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
            Vector2 center = (start + end) / 2f;

            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, center);
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - length / 2f, center.y - thickness / 2f, length, thickness), Texture2D.whiteTexture);
            GUI.matrix = saved;
        }
        private Color GetElementColor(InsectElement element)
        {
            switch (element)
            {
                case InsectElement.Bug: return new Color(0.55f, 0.82f, 0.28f);
                case InsectElement.Poison: return new Color(0.6f, 0.2f, 0.8f);
                case InsectElement.Water: return new Color(0.2f, 0.5f, 1f);
                case InsectElement.Leaf: return new Color(0.2f, 0.85f, 0.3f);
                case InsectElement.Wind: return new Color(0.6f, 0.9f, 0.7f);
                case InsectElement.Electric: return new Color(1f, 0.95f, 0.2f);
                case InsectElement.Earth: return new Color(0.7f, 0.5f, 0.2f);
                case InsectElement.Light: return new Color(1f, 0.95f, 0.7f);
                case InsectElement.Dark: return new Color(0.4f, 0.15f, 0.5f);
                case InsectElement.Metal: return new Color(0.7f, 0.75f, 0.8f);
                default: return Color.white;
            }
        }
        private void DrawElementImpact(float tgtX, float tgtY, float impactT, InsectElement element, Color elemCol)
        {
            switch (element)
            {
                case InsectElement.Poison:
                    DrawPoisonImpact(tgtX, tgtY, impactT, elemCol);
                    break;
                case InsectElement.Water:
                    DrawWaterImpact(tgtX, tgtY, impactT, elemCol);
                    break;
                case InsectElement.Leaf:
                    DrawLeafImpact(tgtX, tgtY, impactT, elemCol);
                    break;
                case InsectElement.Wind:
                    DrawWindImpact(tgtX, tgtY, impactT, elemCol);
                    break;
                case InsectElement.Electric:
                    DrawElectricImpact(tgtX, tgtY, impactT, elemCol);
                    break;
                case InsectElement.Earth:
                    DrawEarthImpact(tgtX, tgtY, impactT, elemCol);
                    break;
                case InsectElement.Light:
                    DrawLightImpact(tgtX, tgtY, impactT, elemCol);
                    break;
                case InsectElement.Dark:
                    DrawDarkImpact(tgtX, tgtY, impactT, elemCol);
                    break;
                case InsectElement.Metal:
                    DrawMetalImpact(tgtX, tgtY, impactT, elemCol);
                    break;
                default:
                    DrawDefaultImpact(tgtX, tgtY, impactT);
                    break;
            }
        }
        // Poison: purple fog expansion + bubbles floating up
        private void DrawPoisonImpact(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            // Poison fog expanding
            float fogSize = 80f + impactT * 140f;
            float fogAlpha = (1f - impactT) * 0.45f;
            GUI.color = new Color(elemCol.r, elemCol.g, elemCol.b, fogAlpha);
            GUI.DrawTexture(new Rect(tgtX - fogSize / 2, tgtY - fogSize / 2, fogSize, fogSize), Texture2D.whiteTexture);

            // Inner fog layer
            float innerFog = 50f + impactT * 80f;
            GUI.color = new Color(elemCol.r * 0.7f, elemCol.g, elemCol.b * 1.2f, fogAlpha * 0.6f);
            GUI.DrawTexture(new Rect(tgtX - innerFog / 2, tgtY - innerFog / 2, innerFog, innerFog), Texture2D.whiteTexture);

            // Bubbles floating up
            for (int b = 0; b < 7; b++)
            {
                float bPhase = b * 0.9f + 1.3f;
                float bx = tgtX + Mathf.Sin(bPhase) * (30f + b * 12f);
                float by = tgtY - impactT * (60f + b * 20f);
                float bSize = (8f - b * 0.5f) * (1f - impactT * 0.5f);
                float bAlpha = (1f - impactT) * 0.7f;
                GUI.color = new Color(elemCol.r, elemCol.g, elemCol.b, bAlpha);
                GUI.DrawTexture(new Rect(bx - bSize / 2, by - bSize / 2, bSize, bSize), Texture2D.whiteTexture);
                // Bubble highlight
                GUI.color = new Color(1f, 1f, 1f, bAlpha * 0.4f);
                GUI.DrawTexture(new Rect(bx - bSize * 0.15f, by - bSize * 0.3f, bSize * 0.3f, bSize * 0.3f), Texture2D.whiteTexture);
            }
        }
        // Water: concentric ripple rings + radial droplets
        private void DrawWaterImpact(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            // Three concentric ripple rings
            for (int ring = 0; ring < 3; ring++)
            {
                float ringDelay = ring * 0.12f;
                float ringT = Mathf.Clamp01((impactT - ringDelay) / (1f - ringDelay));
                if (ringT <= 0f) continue;

                float radius = 30f + ringT * (80f + ring * 30f);
                float ringAlpha = (1f - ringT) * 0.6f;
                float thick = 4f * (1f - ringT * 0.5f);

                // Draw ring as 4 edge rects (top, bottom, left, right)
                GUI.color = new Color(elemCol.r, elemCol.g, elemCol.b, ringAlpha);
                GUI.DrawTexture(new Rect(tgtX - radius, tgtY - thick / 2, radius * 2, thick), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(tgtX - thick / 2, tgtY - radius, thick, radius * 2), Texture2D.whiteTexture);

                // Diagonal edges for rounder appearance
                float diag = radius * 0.707f;
                GUI.DrawTexture(new Rect(tgtX - diag - thick / 2, tgtY - diag - thick / 2, thick * 2, thick * 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(tgtX + diag - thick / 2, tgtY - diag - thick / 2, thick * 2, thick * 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(tgtX - diag - thick / 2, tgtY + diag - thick / 2, thick * 2, thick * 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(tgtX + diag - thick / 2, tgtY + diag - thick / 2, thick * 2, thick * 2), Texture2D.whiteTexture);
            }

            // 8 radial water droplets
            for (int d = 0; d < 8; d++)
            {
                float angle = d * 45f * Mathf.Deg2Rad;
                float dist = 20f + impactT * 100f;
                float dx = tgtX + Mathf.Cos(angle) * dist;
                float dy = tgtY + Mathf.Sin(angle) * dist;
                float dSize = 7f * (1f - impactT * 0.6f);
                float dAlpha = (1f - impactT) * 0.8f;
                GUI.color = new Color(elemCol.r, elemCol.g, elemCol.b, dAlpha);
                GUI.DrawTexture(new Rect(dx - dSize / 2, dy - dSize / 2, dSize, dSize), Texture2D.whiteTexture);
                // White specular highlight
                GUI.color = new Color(1f, 1f, 1f, dAlpha * 0.5f);
                GUI.DrawTexture(new Rect(dx - 2, dy - 2, 3, 3), Texture2D.whiteTexture);
            }

            // Central splash
            float splashAlpha = (1f - impactT) * 0.5f;
            float splashSize = 60f + impactT * 50f;
            GUI.color = new Color(elemCol.r, elemCol.g, elemCol.b, splashAlpha);
            GUI.DrawTexture(new Rect(tgtX - splashSize / 2, tgtY - splashSize / 2, splashSize, splashSize), Texture2D.whiteTexture);
        }
        // Leaf: X-shaped green slash + leaf fragments
        private void DrawLeafImpact(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            float slashLen = 60f + impactT * 40f;
            float slashAlpha = (1f - impactT) * 0.8f;
            float thick = 4f * (1f - impactT * 0.3f);
            Color slashCol = new Color(elemCol.r, elemCol.g, elemCol.b, slashAlpha);

            // X-shaped slash using DrawRotatedLine
            DrawRotatedLine(tgtX - slashLen, tgtY - slashLen, tgtX + slashLen, tgtY + slashLen, thick, slashCol);
            DrawRotatedLine(tgtX + slashLen, tgtY - slashLen, tgtX - slashLen, tgtY + slashLen, thick, slashCol);

            // Flash at center
            float flashAlpha = (1f - impactT) * 0.6f;
            float flashSize = 50f + impactT * 30f;
            GUI.color = new Color(elemCol.r, elemCol.g, elemCol.b, flashAlpha * 0.3f);
            GUI.DrawTexture(new Rect(tgtX - flashSize / 2, tgtY - flashSize / 2, flashSize, flashSize), Texture2D.whiteTexture);

            // 6 leaf fragments flying outward
            for (int lf = 0; lf < 6; lf++)
            {
                float angle = lf * 60f * Mathf.Deg2Rad + 0.3f;
                float dist = 25f + impactT * (70f + lf * 15f);
                float lx = tgtX + Mathf.Cos(angle) * dist;
                float ly = tgtY + Mathf.Sin(angle) * dist - impactT * 20f;
                float lfSize = 10f * (1f - impactT * 0.4f);
                float lfAlpha = (1f - impactT) * 0.7f;
                // Leaf shape: small rotated rect
                float lfAngle = angle * Mathf.Rad2Deg + impactT * 180f;
                Color lfCol = new Color(elemCol.r * (0.8f + lf * 0.05f), elemCol.g, elemCol.b * 0.7f, lfAlpha);
                DrawRotatedLine(lx - lfSize, ly, lx + lfSize, ly, 3f, lfCol);
            }
        }
        // Wind: arc-shaped expansions + speed lines
        private void DrawWindImpact(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            // 3 rotating arcs expanding outward
            for (int arc = 0; arc < 3; arc++)
            {
                float arcAngle = (arc * 120f + impactT * 200f) * Mathf.Deg2Rad;
                float radius = 40f + impactT * (60f + arc * 20f);
                float arcAlpha = (1f - impactT) * 0.6f;
                float arcLen = 30f + impactT * 15f;

                // Draw arc segment as a short thick line at angle
                float ax1 = tgtX + Mathf.Cos(arcAngle - 0.3f) * radius;
                float ay1 = tgtY + Mathf.Sin(arcAngle - 0.3f) * radius;
                float ax2 = tgtX + Mathf.Cos(arcAngle + 0.3f) * radius;
                float ay2 = tgtY + Mathf.Sin(arcAngle + 0.3f) * radius;
                Color arcCol = new Color(elemCol.r, elemCol.g, elemCol.b, arcAlpha);
                DrawRotatedLine(ax1, ay1, ax2, ay2, 3f, arcCol);
            }

            // Wind speed lines (horizontal streaks)
            for (int sl = 0; sl < 5; sl++)
            {
                float slY = tgtY - 40f + sl * 20f;
                float slX = tgtX - 60f + impactT * 80f + sl * 10f;
                float lineLen = 30f + sl * 8f;
                float slAlpha = (1f - impactT) * (0.5f - sl * 0.06f);
                Color slCol = new Color(elemCol.r, elemCol.g, elemCol.b, slAlpha);
                DrawRotatedLine(slX, slY, slX + lineLen, slY, 2f, slCol);
            }

            // Central swirl glow
            float swirlSize = 70f + impactT * 40f;
            float swirlAlpha = (1f - impactT) * 0.25f;
            GUI.color = new Color(elemCol.r, elemCol.g, elemCol.b, swirlAlpha);
            GUI.DrawTexture(new Rect(tgtX - swirlSize / 2, tgtY - swirlSize / 2, swirlSize, swirlSize), Texture2D.whiteTexture);
        }
        // Electric: zigzag lightning + yellow flash + sparks
        private void DrawElectricImpact(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            // Yellow flash
            float flashAlpha = (1f - impactT) * 0.8f;
            float flashSize = 90f + impactT * 60f;
            GUI.color = new Color(elemCol.r, elemCol.g, elemCol.b, flashAlpha * 0.35f);
            GUI.DrawTexture(new Rect(tgtX - flashSize / 2, tgtY - flashSize / 2, flashSize, flashSize), Texture2D.whiteTexture);

            // Inner bright flash
            float innerFlash = 40f * (1f - impactT);
            GUI.color = new Color(1f, 1f, 0.9f, flashAlpha * 0.5f);
            GUI.DrawTexture(new Rect(tgtX - innerFlash / 2, tgtY - innerFlash / 2, innerFlash, innerFlash), Texture2D.whiteTexture);

            // 3-4 zigzag lightning bolts
            for (int bolt = 0; bolt < 4; bolt++)
            {
                float boltAngle = bolt * 90f * Mathf.Deg2Rad + 0.4f;
                float boltAlpha = (1f - impactT) * 0.9f;
                Color boltCol = new Color(elemCol.r, elemCol.g, elemCol.b, boltAlpha);
                float reach = 50f + impactT * 40f;

                // Zigzag: 3 segments
                float cx = tgtX;
                float cy = tgtY;
                for (int seg = 0; seg < 3; seg++)
                {
                    float segLen = reach / 3f;
                    float zigOffset = (seg % 2 == 0 ? 12f : -12f);
                    float nx = cx + Mathf.Cos(boltAngle) * segLen + Mathf.Cos(boltAngle + Mathf.PI / 2) * zigOffset;
                    float ny = cy + Mathf.Sin(boltAngle) * segLen + Mathf.Sin(boltAngle + Mathf.PI / 2) * zigOffset;
                    DrawRotatedLine(cx, cy, nx, ny, 3f, boltCol);
                    cx = nx;
                    cy = ny;
                }
            }

            // Sparks
            for (int sp = 0; sp < 8; sp++)
            {
                float spAngle = sp * 45f * Mathf.Deg2Rad + impactT * 3f;
                float spDist = 30f + impactT * 80f;
                float spx = tgtX + Mathf.Cos(spAngle) * spDist;
                float spy = tgtY + Mathf.Sin(spAngle) * spDist;
                float spSize = 5f * (1f - impactT);
                GUI.color = new Color(1f, 1f, 0.6f, (1f - impactT) * 0.8f);
                GUI.DrawTexture(new Rect(spx - spSize / 2, spy - spSize / 2, spSize, spSize), Texture2D.whiteTexture);
            }
        }
        // Earth: rising pillars + dust particles
        private void DrawEarthImpact(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            // 4-5 pillars rising from below
            for (int p = 0; p < 5; p++)
            {
                float delay = p * 0.08f;
                float pT = Mathf.Clamp01((impactT - delay) / (1f - delay));
                if (pT <= 0f) continue;

                float px = tgtX - 50f + p * 25f;
                float pillarH = (60f + p * 15f) * Mathf.Min(pT * 2f, 1f);
                float pillarW = 14f - p * 1.5f;
                float pAlpha = (1f - pT * 0.7f) * 0.8f;

                // Pillar body
                GUI.color = new Color(elemCol.r, elemCol.g, elemCol.b, pAlpha);
                GUI.DrawTexture(new Rect(px - pillarW / 2, tgtY - pillarH, pillarW, pillarH), Texture2D.whiteTexture);

                // Pillar highlight edge
                GUI.color = new Color(elemCol.r + 0.2f, elemCol.g + 0.15f, elemCol.b, pAlpha * 0.5f);
                GUI.DrawTexture(new Rect(px - pillarW / 2, tgtY - pillarH, 3, pillarH), Texture2D.whiteTexture);
            }

            // Dust particles
            for (int d = 0; d < 8; d++)
            {
                float dAngle = d * 45f * Mathf.Deg2Rad + 0.2f;
                float dDist = 20f + impactT * 60f;
                float dx = tgtX + Mathf.Cos(dAngle) * dDist;
                float dy = tgtY + Mathf.Sin(dAngle) * dDist * 0.4f - impactT * 30f;
                float dSize = 6f * (1f - impactT * 0.5f);
                float dAlpha = (1f - impactT) * 0.5f;
                GUI.color = new Color(elemCol.r * 0.9f, elemCol.g * 0.8f, elemCol.b * 0.6f, dAlpha);
                GUI.DrawTexture(new Rect(dx - dSize / 2, dy - dSize / 2, dSize, dSize), Texture2D.whiteTexture);
            }

            // Ground crack line
            float crackLen = 80f * Mathf.Min(impactT * 3f, 1f);
            float crackAlpha = (1f - impactT) * 0.6f;
            GUI.color = new Color(elemCol.r * 0.6f, elemCol.g * 0.4f, elemCol.b * 0.3f, crackAlpha);
            GUI.DrawTexture(new Rect(tgtX - crackLen / 2, tgtY - 1.5f, crackLen, 3), Texture2D.whiteTexture);
        }
        // Light: descending light pillar + cross expansion + star sparkle
        private void DrawLightImpact(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            // Light pillar from above
            float pillarH = 200f * Mathf.Min(impactT * 3f, 1f);
            float pillarW = 24f * (1f - impactT * 0.3f);
            float pillarAlpha = (1f - impactT) * 0.6f;
            GUI.color = new Color(elemCol.r, elemCol.g, elemCol.b, pillarAlpha);
            GUI.DrawTexture(new Rect(tgtX - pillarW / 2, tgtY - pillarH, pillarW, pillarH), Texture2D.whiteTexture);

            // Wider faint pillar glow
            GUI.color = new Color(elemCol.r, elemCol.g, elemCol.b, pillarAlpha * 0.2f);
            GUI.DrawTexture(new Rect(tgtX - pillarW * 1.5f, tgtY - pillarH, pillarW * 3, pillarH), Texture2D.whiteTexture);

            // Cross expansion at impact point
            float crossLen = 40f + impactT * 60f;
            float crossThick = 4f * (1f - impactT * 0.4f);
            float crossAlpha = (1f - impactT) * 0.7f;
            GUI.color = new Color(1f, 1f, 0.9f, crossAlpha);
            GUI.DrawTexture(new Rect(tgtX - crossLen, tgtY - crossThick / 2, crossLen * 2, crossThick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(tgtX - crossThick / 2, tgtY - crossLen, crossThick, crossLen * 2), Texture2D.whiteTexture);

            // Star sparkles
            for (int s = 0; s < 6; s++)
            {
                float sAngle = s * 60f * Mathf.Deg2Rad + impactT * 2f;
                float sDist = 35f + impactT * 50f;
                float sx = tgtX + Mathf.Cos(sAngle) * sDist;
                float sy = tgtY + Mathf.Sin(sAngle) * sDist;
                float sSize = 6f * (0.5f + Mathf.Sin((impactT + s * 0.3f) * Mathf.PI * 2f) * 0.5f);
                float sAlpha = (1f - impactT) * 0.8f;
                // Star: small cross
                GUI.color = new Color(1f, 1f, 0.85f, sAlpha);
                GUI.DrawTexture(new Rect(sx - sSize, sy - 1, sSize * 2, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(sx - 1, sy - sSize, 2, sSize * 2), Texture2D.whiteTexture);
            }
        }
        // Dark: screen darken + shrinking purple circle + crack lines
        private void DrawDarkImpact(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            float sw = UIScale.VirtualScreenWidth;
            float sh = UIScale.VirtualScreenHeight;

            // Screen darkening
            float darkenAlpha = (1f - impactT) * 0.35f;
            GUI.color = new Color(0.05f, 0f, 0.1f, darkenAlpha);
            GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.whiteTexture);

            // Shrinking purple circle converging on target
            float circleSize = 120f * (1f - impactT * 0.7f);
            float circleAlpha = (1f - impactT) * 0.55f;
            GUI.color = new Color(elemCol.r, elemCol.g, elemCol.b, circleAlpha);
            // Draw as border rects (hollow circle approximation)
            float thick = 5f * (1f - impactT * 0.3f);
            GUI.DrawTexture(new Rect(tgtX - circleSize, tgtY - thick / 2, circleSize * 2, thick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(tgtX - thick / 2, tgtY - circleSize, thick, circleSize * 2), Texture2D.whiteTexture);
            float diag = circleSize * 0.707f;
            DrawRotatedLine(tgtX - diag, tgtY - diag, tgtX + diag, tgtY + diag, thick * 0.7f,
                new Color(elemCol.r, elemCol.g, elemCol.b, circleAlpha * 0.6f));
            DrawRotatedLine(tgtX + diag, tgtY - diag, tgtX - diag, tgtY + diag, thick * 0.7f,
                new Color(elemCol.r, elemCol.g, elemCol.b, circleAlpha * 0.6f));

            // Crack lines radiating from center
            for (int c = 0; c < 5; c++)
            {
                float cAngle = c * 72f * Mathf.Deg2Rad + 0.5f;
                float cLen = 30f + impactT * 50f;
                float cx = tgtX + Mathf.Cos(cAngle) * cLen;
                float cy = tgtY + Mathf.Sin(cAngle) * cLen;
                float cAlpha = (1f - impactT) * 0.7f;
                Color crackCol = new Color(elemCol.r * 1.3f, elemCol.g * 0.5f, elemCol.b * 1.2f, cAlpha);
                DrawRotatedLine(tgtX, tgtY, cx, cy, 2f, crackCol);
            }

            // Inner dark core
            float coreSize = 20f * (1f - impactT);
            GUI.color = new Color(0.1f, 0f, 0.15f, (1f - impactT) * 0.8f);
            GUI.DrawTexture(new Rect(tgtX - coreSize / 2, tgtY - coreSize / 2, coreSize, coreSize), Texture2D.whiteTexture);
        }
        // Metal: X-shaped sharp slash + metal shards + highlight
        private void DrawMetalImpact(float tgtX, float tgtY, float impactT, Color elemCol)
        {
            // X-shaped sharp slash
            float slashLen = 55f + impactT * 35f;
            float slashAlpha = (1f - impactT) * 0.85f;
            float thick = 3.5f * (1f - impactT * 0.3f);
            Color slashCol = new Color(0.9f, 0.9f, 0.95f, slashAlpha);
            DrawRotatedLine(tgtX - slashLen, tgtY - slashLen * 0.6f, tgtX + slashLen, tgtY + slashLen * 0.6f, thick, slashCol);
            DrawRotatedLine(tgtX + slashLen, tgtY - slashLen * 0.6f, tgtX - slashLen, tgtY + slashLen * 0.6f, thick, slashCol);

            // Bright highlight flash at intersection
            float hlSize = 30f * (1f - impactT);
            GUI.color = new Color(1f, 1f, 1f, slashAlpha * 0.7f);
            GUI.DrawTexture(new Rect(tgtX - hlSize / 2, tgtY - hlSize / 2, hlSize, hlSize), Texture2D.whiteTexture);

            // Metal shards flying outward
            for (int sh = 0; sh < 7; sh++)
            {
                float shAngle = sh * 51.4f * Mathf.Deg2Rad + impactT * 1.5f;
                float shDist = 20f + impactT * (60f + sh * 12f);
                float shx = tgtX + Mathf.Cos(shAngle) * shDist;
                float shy = tgtY + Mathf.Sin(shAngle) * shDist;
                float shSize = 8f * (1f - impactT * 0.5f);
                float shAlpha = (1f - impactT) * 0.7f;
                // Shard: small rotated line
                float shRot = shAngle + impactT * Mathf.PI;
                Color shardCol = new Color(elemCol.r, elemCol.g, elemCol.b, shAlpha);
                DrawRotatedLine(shx - Mathf.Cos(shRot) * shSize, shy - Mathf.Sin(shRot) * shSize,
                    shx + Mathf.Cos(shRot) * shSize, shy + Mathf.Sin(shRot) * shSize, 2f, shardCol);
            }

            // Edge highlight streaks
            GUI.color = new Color(1f, 1f, 1f, slashAlpha * 0.3f);
            DrawRotatedLine(tgtX - slashLen * 0.8f, tgtY - slashLen * 0.48f - 2f,
                tgtX + slashLen * 0.8f, tgtY + slashLen * 0.48f - 2f, 1.5f,
                new Color(1f, 1f, 1f, slashAlpha * 0.3f));
        }
        // Default (Bug/None): radial burst + shockwave
        private void DrawDefaultImpact(float tgtX, float tgtY, float impactT)
        {
            // Impact flash
            float flashAlpha = (1f - impactT) * 0.7f;
            float flashSize = 100f + impactT * 80f;
            GUI.color = new Color(1f, 0.5f, 0.2f, flashAlpha);
            GUI.DrawTexture(new Rect(tgtX - flashSize / 2, tgtY - flashSize / 2, flashSize, flashSize), Texture2D.whiteTexture);

            // Radial sparks
            for (int sp = 0; sp < 10; sp++)
            {
                float angle = sp * 36f * Mathf.Deg2Rad + impactT * 2f;
                float dist = 40f + impactT * 120f;
                float spx = tgtX + Mathf.Cos(angle) * dist;
                float spy = tgtY + Mathf.Sin(angle) * dist;
                float sparkSize = 10f * (1f - impactT);
                GUI.color = new Color(1f, 1f, 0.4f, (1f - impactT) * 0.8f);
                GUI.DrawTexture(new Rect(spx - sparkSize / 2, spy - sparkSize / 2, sparkSize, sparkSize), Texture2D.whiteTexture);
            }

            // Slash lines on boss
            for (int sl = 0; sl < 3; sl++)
            {
                float slashAngle = (sl - 1) * 30f * Mathf.Deg2Rad;
                float slashLen = 50f + impactT * 40f;
                float slashAlpha = (1f - impactT) * 0.5f;
                float sx1 = tgtX - Mathf.Cos(slashAngle) * slashLen;
                float sx2 = tgtX + Mathf.Cos(slashAngle) * slashLen;
                float sy1 = tgtY - Mathf.Sin(slashAngle) * slashLen;
                float slashW = 3f * (1f - impactT * 0.5f);
                GUI.color = new Color(1f, 1f, 1f, slashAlpha);
                GUI.DrawTexture(new Rect(Mathf.Min(sx1, sx2), sy1 - slashW / 2, Mathf.Abs(sx2 - sx1), slashW), Texture2D.whiteTexture);
            }
        }
        private void DrawBuffDebuffEffect(float t)
        {
            float sw = UIScale.VirtualScreenWidth;
            float sh = UIScale.VirtualScreenHeight;

            // Determine if buff or debuff from skill name / action text
            bool isBuff = false;
            bool isDebuff = false;
            string displayText = "";

            if (!string.IsNullOrEmpty(lastSkillUsedName))
            {
                string upper = lastSkillUsedName.ToUpper();
                if (upper.Contains("UP") || upper.Contains("BUFF") || upper.Contains("강화"))
                {
                    isBuff = true;
                    displayText = lastSkillUsedName;
                }
                else if (upper.Contains("DOWN") || upper.Contains("DEBUFF") || upper.Contains("약화"))
                {
                    isDebuff = true;
                    displayText = lastSkillUsedName;
                }
            }

            if (!string.IsNullOrEmpty(actionText) && !isBuff && !isDebuff)
            {
                string upper = actionText.ToUpper();
                if (upper.Contains("UP") || upper.Contains("강화"))
                    isBuff = true;
                else if (upper.Contains("DOWN") || upper.Contains("약화"))
                    isDebuff = true;
            }

            // Fallback: treat as buff if neither detected
            if (!isBuff && !isDebuff) isBuff = true;

            if (isBuff)
            {
                // Buff: effect on active team member position
                int teamCount = raidController.TeamStats != null ? raidController.TeamStats.Length : 1;
                float memberX = sw * 0.15f + (selectedSlot >= 0 ? selectedSlot : 0) * (sw * 0.7f / Mathf.Max(teamCount - 1, 1));
                float memberY = sh * 0.40f;

                // Pulsing aura circle
                float auraSize = 60f + Mathf.Sin(t * Mathf.PI * 3f) * 15f;
                float auraAlpha = (1f - t * 0.6f) * 0.3f;
                GUI.color = new Color(0.2f, 0.6f, 1f, auraAlpha);
                GUI.DrawTexture(new Rect(memberX - auraSize, memberY - auraSize, auraSize * 2, auraSize * 2), Texture2D.whiteTexture);

                // Inner bright aura
                float innerAura = auraSize * 0.6f;
                GUI.color = new Color(0.3f, 0.8f, 0.4f, auraAlpha * 1.5f);
                GUI.DrawTexture(new Rect(memberX - innerAura, memberY - innerAura, innerAura * 2, innerAura * 2), Texture2D.whiteTexture);

                // Rising arrows
                for (int a = 0; a < 3; a++)
                {
                    float arrowT = Mathf.Clamp01(t * 1.5f - a * 0.15f);
                    float arrowY = memberY - 20f - arrowT * 80f;
                    float arrowX = memberX - 15f + a * 15f;
                    float arrowAlpha = (1f - arrowT) * 0.9f;

                    buffArrowStyleCache.normal.textColor = new Color(0.3f, 0.85f, 0.5f, arrowAlpha);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(arrowX - 12, arrowY, 24, 28), "\u25b2", buffArrowStyleCache);
                }

                // Skill name text
                if (!string.IsNullOrEmpty(displayText))
                {
                    float textAlpha = Mathf.Clamp01(1f - t * 1.2f);
                    buffTxtStyleCache.normal.textColor = new Color(0.3f, 0.85f, 0.5f, textAlpha);
                    GUI.color = Color.white;

                    // Background glow
                    GUI.color = new Color(0.2f, 0.5f, 0.3f, textAlpha * 0.2f);
                    GUI.DrawTexture(new Rect(memberX - 110, memberY - 80 - t * 20f, 220, 38), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(memberX - 110, memberY - 78 - t * 20f, 220, 34), displayText, buffTxtStyleCache);
                }
            }
            else
            {
                // Debuff: effect on boss position
                float bossX = sw * 0.5f;
                float bossY = sh * 0.12f;

                // Dark aura around boss
                float auraSize = 70f + Mathf.Sin(t * Mathf.PI * 2.5f) * 12f;
                float auraAlpha = (1f - t * 0.6f) * 0.35f;
                GUI.color = new Color(0.5f, 0.1f, 0.1f, auraAlpha);
                GUI.DrawTexture(new Rect(bossX - auraSize, bossY - auraSize, auraSize * 2, auraSize * 2), Texture2D.whiteTexture);

                // Inner dark core
                float innerAura = auraSize * 0.5f;
                GUI.color = new Color(0.3f, 0f, 0.05f, auraAlpha * 1.2f);
                GUI.DrawTexture(new Rect(bossX - innerAura, bossY - innerAura, innerAura * 2, innerAura * 2), Texture2D.whiteTexture);

                // Descending arrows
                for (int a = 0; a < 3; a++)
                {
                    float arrowT = Mathf.Clamp01(t * 1.5f - a * 0.15f);
                    float arrowY = bossY + 20f + arrowT * 60f;
                    float arrowX = bossX - 15f + a * 15f;
                    float arrowAlpha = (1f - arrowT) * 0.9f;

                    debuffArrowStyleCache.normal.textColor = new Color(0.9f, 0.2f, 0.2f, arrowAlpha);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(arrowX - 12, arrowY, 24, 28), "\u25bc", debuffArrowStyleCache);
                }

                // Skill name text
                if (!string.IsNullOrEmpty(displayText))
                {
                    float textAlpha = Mathf.Clamp01(1f - t * 1.2f);
                    debuffTxtStyleCache.normal.textColor = new Color(0.9f, 0.25f, 0.2f, textAlpha);
                    GUI.color = Color.white;

                    // Background glow
                    GUI.color = new Color(0.4f, 0.05f, 0.05f, textAlpha * 0.2f);
                    GUI.DrawTexture(new Rect(bossX - 110, bossY + 80 + t * 15f, 220, 38), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(bossX - 110, bossY + 82 + t * 15f, 220, 34), displayText, debuffTxtStyleCache);
                }
            }
        }
        private Color GetSkillColor(SkillEffectType type)
        {
            switch (type)
            {
                case SkillEffectType.Damage: return new Color(0.9f, 0.35f, 0.3f);
                case SkillEffectType.BuffAttack: return new Color(0.3f, 0.8f, 0.4f);
                case SkillEffectType.DebuffAttack: return new Color(0.7f, 0.4f, 0.9f);
                case SkillEffectType.Heal: return new Color(0.35f, 0.92f, 0.62f);
                case SkillEffectType.DefenseBuff: return new Color(0.35f, 0.68f, 1f);
                case SkillEffectType.Stun: return new Color(1f, 0.86f, 0.25f);
                case SkillEffectType.PoisonDot: return new Color(0.68f, 0.35f, 0.88f);
                default: return Color.gray;
            }
        }
    }
}
