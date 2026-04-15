using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.UI
{
    public class RaidBattleUI : MonoBehaviour
    {
        [SerializeField] private RaidBattleController raidController;
        [SerializeField] private CameraFollower cameraFollower;
        [SerializeField] private PlayerMovement playerMovement;

        private enum Phase { None, Intro, SelectInsect, SelectSkill, PlayerAttack, BossAttack, UniteAttack, Result }

        private Phase phase = Phase.None;
        public bool IsRaidActive => phase != Phase.None;

        private float phaseTimer;
        private float introTimer;
        private float resultTimer;
        private bool resultShown;

        private string actionText;
        private float actionTimer;
        private int lastDmgToBoss;
        private int lastDmgToTeam;
        private bool lastAoe;
        private int lastHitSlot;
        private string lastSkillUsedName;

        private float displayBossHp;
        private float[] displayTeamHp;
        private float bossShake;
        private float[] teamShake;

        private int selectedSlot = -1;

        private Rect[] insectBtnRects = new Rect[5];
        private bool[] insectBtnUsable = new bool[5];
        private int insectBtnCount;
        private Rect[] raidSkillRects = new Rect[4];
        private bool[] raidSkillUsable = new bool[4];
        private int raidSkillCount;

        private bool wantInsect0, wantInsect1, wantInsect2, wantInsect3, wantInsect4;
        private bool wantSkillQ, wantSkillW, wantSkillE, wantSkillR;
        private bool wantEscape;
        private bool wantUnite;
        private bool wantMouseClick;
        private Vector2 guiMousePos;
        private float uniteAnimTimer;
        private Rect uniteBtnRect;
        private bool uniteBtnVisible;

        private void OnEnable()
        {
            if (raidController != null)
            {
                raidController.RaidUpdated += OnRaidUpdated;
                raidController.RaidEnded += OnRaidEnded;
            }
        }

        private void OnDisable()
        {
            if (raidController != null)
            {
                raidController.RaidUpdated -= OnRaidUpdated;
                raidController.RaidEnded -= OnRaidEnded;
            }
        }

        private void OnRaidUpdated()
        {
            if (!raidController.IsActive && phase != Phase.Result) return;

            if (phase == Phase.None)
            {
                int count = raidController.TeamStats.Length;
                displayTeamHp = new float[count];
                teamShake = new float[count];
                for (int i = 0; i < count; i++)
                    displayTeamHp[i] = raidController.TeamStats[i].CurrentHp;
                displayBossHp = raidController.BossStats.CurrentHp;
                bossShake = 0;
                selectedSlot = raidController.ActiveSlot;

                phase = Phase.Intro;
                introTimer = 0f;
                resultShown = false;
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayBGM(BgmType.RaidBattle);
                    AudioManager.Instance.PlaySFX(SfxType.BossAppear);
                }

                InsectEntity bossEnt = raidController.BossEntity;
                Vector3 bossWorldPos = bossEnt != null ? bossEnt.transform.position : Vector3.zero;

                // 3D 레이드 아레나 생성 (보스 월드 위치 전달)
                if (BattleArenaController.Instance != null)
                {
                    bool bossShiny3d = bossEnt != null && bossEnt.IsShiny;
                    InsectData bossData3d = bossEnt != null ? bossEnt.Data : null;
                    int bossLevel3d = bossEnt != null ? bossEnt.Level : 1;
                    InsectData[] teamData3d = raidController.TeamData;
                    int[] teamLevels3d = new int[count];
                    for (int tl = 0; tl < count; tl++)
                        teamLevels3d[tl] = raidController.TeamStats[tl] != null ? raidController.TeamStats[tl].Level : 1;
                    if (bossData3d != null)
                        BattleArenaController.Instance.SetupRaidBattle(bossData3d, bossLevel3d, bossShiny3d, teamData3d, teamLevels3d, bossWorldPos);
                }
                else if (cameraFollower != null && bossEnt != null)
                {
                    GameObject playerObj = GameObject.Find("Player");
                    Vector3 pPos = playerObj != null ? playerObj.transform.position : Vector3.zero;
                    cameraFollower.EnterBattleMode(pPos, bossWorldPos);
                }
                if (playerMovement != null) playerMovement.SetFrozen(true);
                return;
            }

            lastDmgToBoss = raidController.LastDamageToBoss;
            lastDmgToTeam = raidController.LastDamageToTeam;
            lastAoe = raidController.BossUsedAoe;
            lastHitSlot = raidController.LastHitSlot;
            actionText = raidController.LastActionText;
            actionTimer = 2f;

            if (lastDmgToBoss > 0) bossShake = 0.4f;
            if (lastAoe)
            {
                for (int i = 0; i < teamShake.Length; i++)
                    if (raidController.TeamStats[i].CurrentHp >= 0) teamShake[i] = 0.4f;
            }
            else if (lastHitSlot >= 0 && lastHitSlot < teamShake.Length)
            {
                teamShake[lastHitSlot] = 0.4f;
            }

            selectedSlot = raidController.ActiveSlot;

            if (raidController.LastWasUnite)
            {
                phase = Phase.UniteAttack;
                phaseTimer = 0f;
                uniteAnimTimer = 0f;
            }
            else
            {
                phase = Phase.PlayerAttack;
                phaseTimer = 0f;
            }
        }

        private void OnRaidEnded(bool playerWon)
        {
            resultShown = true;
            resultTimer = 0f;
            phase = Phase.Result;
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(playerWon ? SfxType.Victory : SfxType.Defeat);
                AudioManager.Instance.PlayBGM(playerWon ? BgmType.Victory : BgmType.Defeat);
            }
            if (playerWon && TutorialQuestManager.Instance != null)
                TutorialQuestManager.Instance.NotifyRaidCompleted();
        }

        private void Update()
        {
            if (phase == Phase.None) return;

            phaseTimer += Time.deltaTime;
            introTimer += Time.deltaTime;
            if (actionTimer > 0) actionTimer -= Time.deltaTime;
            if (bossShake > 0) bossShake -= Time.deltaTime;
            if (resultShown) resultTimer += Time.deltaTime;

            if (teamShake != null)
                for (int i = 0; i < teamShake.Length; i++)
                    if (teamShake[i] > 0) teamShake[i] -= Time.deltaTime;

            float hpSpeed = 80f * Time.deltaTime;
            if (raidController.BossStats != null)
                displayBossHp = Mathf.MoveTowards(displayBossHp, raidController.BossStats.CurrentHp, hpSpeed);
            if (raidController.TeamStats != null && displayTeamHp != null)
            {
                for (int i = 0; i < raidController.TeamStats.Length && i < displayTeamHp.Length; i++)
                    displayTeamHp[i] = Mathf.MoveTowards(displayTeamHp[i], raidController.TeamStats[i].CurrentHp, hpSpeed);
            }

            if (phase == Phase.Intro && introTimer > 2f)
            {
                phase = Phase.SelectInsect;
                phaseTimer = 0f;
            }

            if (phase == Phase.SelectInsect && raidController != null && raidController.IsActive)
            {
                int idx = -1;
                if (wantInsect0 || Input.GetKeyDown(KeyCode.Alpha1)) idx = 0;
                else if (wantInsect1 || Input.GetKeyDown(KeyCode.Alpha2)) idx = 1;
                else if (wantInsect2 || Input.GetKeyDown(KeyCode.Alpha3)) idx = 2;
                else if (wantInsect3 || Input.GetKeyDown(KeyCode.Alpha4)) idx = 3;
                else if (wantInsect4 || Input.GetKeyDown(KeyCode.Alpha5)) idx = 4;
                wantInsect0 = wantInsect1 = wantInsect2 = wantInsect3 = wantInsect4 = false;
                if (idx >= 0) TrySelectInsect(idx);

                if (wantUnite || Input.GetKeyDown(KeyCode.F))
                {
                    if (raidController.CanUniteAttack)
                    {
                        lastSkillUsedName = "합체공격";
                        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.UniteAttack);
                        raidController.UseUniteAttack();
                    }
                    wantUnite = false;
                }

                if (wantMouseClick || Input.GetMouseButtonDown(0))
                {
                    Vector2 mp = wantMouseClick ? guiMousePos :
                        new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);

                    if (uniteBtnVisible && uniteBtnRect.Contains(mp) && raidController.CanUniteAttack)
                    {
                        lastSkillUsedName = "합체공격";
                        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.UniteAttack);
                        raidController.UseUniteAttack();
                    }
                    else
                    {
                        for (int i = 0; i < insectBtnCount; i++)
                        {
                            if (insectBtnUsable[i] && insectBtnRects[i].Contains(mp))
                            {
                                TrySelectInsect(i);
                                break;
                            }
                        }
                    }
                    wantMouseClick = false;
                }
            }
            else if (phase == Phase.SelectSkill && raidController != null && raidController.IsActive)
            {
                if (wantSkillQ || Input.GetKeyDown(KeyCode.Q)) TryUseRaidSkill(0);
                else if (wantSkillW || Input.GetKeyDown(KeyCode.W)) TryUseRaidSkill(1);
                else if (wantSkillE || Input.GetKeyDown(KeyCode.E)) TryUseRaidSkill(2);
                else if (wantSkillR || Input.GetKeyDown(KeyCode.R)) TryUseRaidSkill(3);
                wantSkillQ = wantSkillW = wantSkillE = wantSkillR = false;

                if (wantUnite || Input.GetKeyDown(KeyCode.F))
                {
                    if (raidController.CanUniteAttack)
                    {
                        lastSkillUsedName = "합체공격";
                        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.UniteAttack);
                        raidController.UseUniteAttack();
                    }
                    wantUnite = false;
                }

                if (wantEscape || Input.GetKeyDown(KeyCode.Escape))
                {
                    phase = Phase.SelectInsect;
                    phaseTimer = 0f;
                    wantEscape = false;
                }

                if (wantMouseClick || Input.GetMouseButtonDown(0))
                {
                    Vector2 mp = wantMouseClick ? guiMousePos :
                        new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);

                    if (uniteBtnVisible && uniteBtnRect.Contains(mp) && raidController.CanUniteAttack)
                    {
                        lastSkillUsedName = "합체공격";
                        if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.UniteAttack);
                        raidController.UseUniteAttack();
                    }
                    else
                    {
                        for (int i = 0; i < raidSkillCount; i++)
                        {
                            if (raidSkillUsable[i] && raidSkillRects[i].Contains(mp))
                            {
                                TryUseRaidSkill(i);
                                break;
                            }
                        }
                    }
                    wantMouseClick = false;
                }
            }

            if (phase == Phase.UniteAttack)
            {
                uniteAnimTimer += Time.deltaTime;
                if (uniteAnimTimer > 2.5f)
                {
                    if (lastDmgToTeam > 0)
                    {
                        phase = Phase.BossAttack;
                        phaseTimer = 0f;
                    }
                    else if (!resultShown)
                    {
                        phase = Phase.SelectInsect;
                        phaseTimer = 0f;
                    }
                }
            }

            if (phase == Phase.PlayerAttack && phaseTimer > 1f)
            {
                if (lastDmgToTeam > 0)
                {
                    phase = Phase.BossAttack;
                    phaseTimer = 0f;
                }
                else if (!resultShown)
                {
                    phase = Phase.SelectInsect;
                    phaseTimer = 0f;
                }
            }

            if (phase == Phase.BossAttack && phaseTimer > 1f)
            {
                if (!resultShown)
                {
                    phase = Phase.SelectInsect;
                    phaseTimer = 0f;
                }
            }

            if (phase == Phase.Result && resultTimer > 5f)
                EndRaid();
        }

        private void TrySelectInsect(int index)
        {
            if (raidController.TeamStats != null && index < raidController.TeamStats.Length
                && raidController.TeamStats[index].CurrentHp > 0)
            {
                selectedSlot = index;
                raidController.SelectSlot(index);
                phase = Phase.SelectSkill;
                phaseTimer = 0f;
            }
        }

        private void TryUseRaidSkill(int index)
        {
            if (raidController.CanUseSkill(index))
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.SkillUse);
                var skills = raidController.TeamSkills != null && selectedSlot < raidController.TeamSkills.Length
                    ? raidController.TeamSkills[selectedSlot] : null;
                InsectSkill skill = skills != null && index < skills.Length ? skills[index] : null;
                lastSkillUsedName = skill != null ? skill.displayName : "공격";

                if (BattleArenaController.Instance != null && BattleArenaController.Instance.IsActive)
                {
                    BattleArenaController.Instance.SetSelectedTeamIndex(selectedSlot);
                    InsectElement elem = InsectElement.Bug;
                    if (raidController.TeamData != null && selectedSlot >= 0 && selectedSlot < raidController.TeamData.Length && raidController.TeamData[selectedSlot] != null)
                        elem = raidController.TeamData[selectedSlot].primaryType;
                    SkillEffectType effectType = (skill != null) ? skill.effectType : SkillEffectType.Damage;
                    BattleArenaController.Instance.PlaySkillEffect(true, elem, effectType);
                }

                raidController.UseSkill(index);
            }
        }

        private void OnGUI()
        {
            if (phase == Phase.None) return;

            Event evt = Event.current;
            if (evt != null && evt.type == EventType.KeyDown)
            {
                switch (evt.keyCode)
                {
                    case KeyCode.Alpha1: case KeyCode.Keypad1:
                        if (phase == Phase.SelectInsect) wantInsect0 = true;
                        evt.Use(); break;
                    case KeyCode.Alpha2: case KeyCode.Keypad2:
                        if (phase == Phase.SelectInsect) wantInsect1 = true;
                        evt.Use(); break;
                    case KeyCode.Alpha3: case KeyCode.Keypad3:
                        if (phase == Phase.SelectInsect) wantInsect2 = true;
                        evt.Use(); break;
                    case KeyCode.Alpha4: case KeyCode.Keypad4:
                        if (phase == Phase.SelectInsect) wantInsect3 = true;
                        evt.Use(); break;
                    case KeyCode.Alpha5: case KeyCode.Keypad5:
                        if (phase == Phase.SelectInsect) wantInsect4 = true;
                        evt.Use(); break;
                    case KeyCode.Q:
                        if (phase == Phase.SelectSkill) wantSkillQ = true;
                        evt.Use(); break;
                    case KeyCode.W:
                        if (phase == Phase.SelectSkill) wantSkillW = true;
                        evt.Use(); break;
                    case KeyCode.E:
                        if (phase == Phase.SelectSkill) wantSkillE = true;
                        evt.Use(); break;
                    case KeyCode.R:
                        if (phase == Phase.SelectSkill) wantSkillR = true;
                        evt.Use(); break;
                    case KeyCode.F:
                        if (phase == Phase.SelectSkill || phase == Phase.SelectInsect) wantUnite = true;
                        evt.Use(); break;
                    case KeyCode.Escape:
                        if (phase == Phase.SelectSkill) wantEscape = true;
                        evt.Use(); break;
                }
            }

            if (evt != null && evt.type == EventType.MouseDown && evt.button == 0)
            {
                wantMouseClick = true;
                guiMousePos = evt.mousePosition;
            }

            DrawOverlay();
            DrawBossField();
            DrawTeamField();
            DrawBossHpBar();
            DrawTeamHpBars();

            DrawUniteGaugeBar();

            if (phase == Phase.Intro)
                DrawIntro();
            else if (phase == Phase.SelectInsect)
                DrawInsectSelector();
            else if (phase == Phase.SelectSkill)
                DrawSkillSelector();
            else if (phase == Phase.UniteAttack)
                DrawUniteAttackAnimation();
            else if (phase == Phase.PlayerAttack || phase == Phase.BossAttack)
                DrawAttackEffects();

            if (actionTimer > 0)
                DrawActionText();

            if (resultShown)
                DrawResult();
        }

        private void DrawOverlay()
        {
            float sw = Screen.width;
            float sh = Screen.height;

            // 3D 아레나 활성 시 2D 배경 스킵
            if (BattleArenaController.Instance != null && BattleArenaController.Instance.IsActive)
            {
                GUI.color = new Color(0.02f, 0.02f, 0.05f, 0.7f);
                GUI.DrawTexture(new Rect(0, 0, sw, sh * 0.06f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUIStyle raidTurnStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                raidTurnStyle.normal.textColor = new Color(0.9f, 0.4f, 0.3f);
                GUI.Label(new Rect(0, 4, sw, 30), "RAID BATTLE", raidTurnStyle);
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

            GUIStyle turnStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            turnStyle.normal.textColor = new Color(1f, 0.6f, 0.2f);
            GUI.Label(new Rect(0, 4, sw, 32),
                $"RAID BATTLE  -  Turn {raidController.TurnNumber + 1}  |  생존: {raidController.AliveCount()}/{raidController.TeamStats.Length}", turnStyle);
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
            if (BattleArenaController.Instance != null && BattleArenaController.Instance.IsActive)
                return;

            if (raidController.BossStats == null) return;

            float bx = Screen.width * 0.5f;
            float by = Screen.height * 0.12f;
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
            else if (id.Contains("bee") || id.Contains("wasp") || id.Contains("hornet"))
                DrawBossBee(cx, cy, s, col, darkCol, lightCol);
            else if (id.Contains("stag") || id.Contains("rhinoceros") || id.Contains("hercules"))
                DrawBossHornBeetle(cx, cy, s, col, darkCol, lightCol);
            else if (id.Contains("spider"))
                DrawBossSpider(cx, cy, s, col, darkCol, lightCol);
            else
                DrawBossDefault(cx, cy, s, col, darkCol, lightCol);

            // Crown label
            float crownPulse = 0.8f + Mathf.Sin(Time.time * 3f) * 0.2f;
            GUIStyle crownStyle = new GUIStyle(GUI.skin.label)
            { fontSize = (int)(20 * s / 3.5f), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            crownStyle.normal.textColor = new Color(1f, 0.3f, 0.15f, crownPulse);
            GUI.color = Color.white;
            GUI.Label(new Rect(cx - 40, cy - 66 * s, 80, 24), "BOSS", crownStyle);

            GUIStyle bossNameStyle = new GUIStyle(GUI.skin.label)
            { fontSize = (int)(16 * s / 3f), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bossNameStyle.normal.textColor = new Color(col.r, col.g, col.b, 0.9f);
            GUI.Label(new Rect(cx - 60, cy + 34 * s, 120, 22), data.displayName, bossNameStyle);

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
            if (BattleArenaController.Instance != null && BattleArenaController.Instance.IsActive)
                return;

            if (raidController.TeamStats == null) return;
            int count = raidController.TeamStats.Length;
            float totalW = Screen.width * 0.7f;
            float startX = Screen.width * 0.15f;
            float y = Screen.height * 0.40f;
            float spacing = count > 1 ? totalW / (count - 1) : 0;

            for (int i = 0; i < count; i++)
            {
                var stats = raidController.TeamStats[i];
                if (stats == null) continue;

                float ix = count > 1 ? startX + i * spacing : Screen.width * 0.5f;
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

                GUIStyle numStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                numStyle.normal.textColor = isSelected ? new Color(1f, 0.85f, 0.2f) : new Color(0.7f, 0.7f, 0.7f, alpha);
                GUI.color = Color.white;
                GUI.Label(new Rect(ix - 14, iy + 26 * sc, 28, 24), $"{i + 1}", numStyle);
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
            else if (id.Contains("bee") || id.Contains("wasp") || id.Contains("hornet"))
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
            float w = Mathf.Min(700, Screen.width * 0.7f);
            float h = 100f;
            float x = (Screen.width - w) / 2f;
            float y = 36f;

            GUI.color = new Color(0.05f, 0.03f, 0.08f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

            Color rarityCol = UITheme.Instance.GetInsectRarityColor(boss.Data.rarity);
            GUI.color = new Color(0.9f, 0.15f, 0.1f);
            GUI.DrawTexture(new Rect(x, y, w, 4), Texture2D.whiteTexture);
            GUI.color = new Color(0.9f, 0.15f, 0.1f, 0.3f);
            GUI.DrawTexture(new Rect(x, y + h - 2, w, 2), Texture2D.whiteTexture);

            GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 24, fontStyle = FontStyle.Bold };
            nameStyle.normal.textColor = rarityCol;
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 14, y + 8, w * 0.5f, 30),
                $"BOSS  {boss.Data.displayName}", nameStyle);

            GUIStyle lvStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            lvStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
            GUI.Label(new Rect(x + w - 130, y + 8, 116, 26), $"Lv.{boss.Level}", lvStyle);

            GUIStyle miniStat = new GUIStyle(GUI.skin.label) { fontSize = 17 };
            miniStat.normal.textColor = new Color(0.5f, 0.5f, 0.55f);
            GUI.Label(new Rect(x + 14, y + 38, w - 28, 20),
                $"ATK {boss.Attack}  DEF {boss.Defense}", miniStat);

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

            GUIStyle hpText = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            hpText.normal.textColor = Color.white;
            GUI.color = Color.white;
            GUI.Label(new Rect(barX, barY, barW, barH),
                $"{Mathf.CeilToInt(displayBossHp)} / {boss.MaxHp}", hpText);
        }

        private void DrawTeamHpBars()
        {
            if (raidController.TeamStats == null) return;
            int count = raidController.TeamStats.Length;
            float barW = 180f;
            float barH = 82f;
            float totalW2 = count * (barW + 10);
            float startX = (Screen.width - totalW2) / 2f;
            float y = Screen.height * 0.53f;

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

                GUIStyle ns = new GUIStyle(GUI.skin.label)
                { fontSize = 18, fontStyle = FontStyle.Bold };
                ns.normal.textColor = alive ? Color.white : new Color(0.4f, 0.4f, 0.4f);
                GUI.color = Color.white;
                GUI.Label(new Rect(bx + 8, y + 6, barW - 16, 22),
                    $"{i + 1}. {stats.Data.displayName}", ns);

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

                GUIStyle hps = new GUIStyle(GUI.skin.label)
                { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                hps.normal.textColor = Color.white;
                GUI.color = Color.white;
                GUI.Label(new Rect(hbX, hbY, hbW, hbH),
                    alive ? $"{Mathf.CeilToInt(dhp)}/{stats.MaxHp}" : "KO", hps);

                GUIStyle lvs = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                lvs.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                GUI.Label(new Rect(bx + 8, y + 58, barW - 16, 18), $"Lv.{stats.Level}", lvs);
            }
        }

        private void DrawIntro()
        {
            if (raidController.BossStats == null) return;

            float alpha = Mathf.Clamp01(introTimer / 0.6f);
            float cx = Screen.width / 2f;
            float cy = Screen.height * 0.30f;
            float sw = Screen.width;
            float sh = Screen.height;

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

                GUIStyle style = new GUIStyle(GUI.skin.label)
                { fontSize = fontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                style.normal.textColor = new Color(1f, 0.3f, 0.15f, alpha);
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - 300, textY, 600, 60), "RAID BOSS", style);
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

                GUIStyle nameS = new GUIStyle(GUI.skin.label)
                { fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                nameS.normal.textColor = new Color(ec.r * namePulse, ec.g * namePulse, ec.b * namePulse, nameT);
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - 320, cy + 2, 640, 48),
                    $"{raidController.BossStats.Data.displayName}  Lv.{raidController.BossStats.Level}", nameS);

                GUIStyle sub = new GUIStyle(GUI.skin.label)
                { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                sub.normal.textColor = new Color(1f, 0.8f, 0.3f, nameT);
                GUI.Label(new Rect(cx - 250, cy + 56, 500, 30),
                    "5마리가 힘을 합쳐 쓰러뜨려라!", sub);
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

                GUIStyle fightStyle = new GUIStyle(GUI.skin.label)
                { fontSize = fightSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                float shakeX = fightT < 0.3f ? Mathf.Sin(Time.time * 60f) * 4f : 0;
                fightStyle.normal.textColor = new Color(1f, 0.9f, 0.2f, Mathf.Clamp01(1f - (fightT - 0.5f) * 3f));
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - 200 + shakeX, cy + 10, 400, 80), "FIGHT!", fightStyle);
            }
        }

        private void DrawInsectSelector()
        {
            float panelW = Screen.width;
            float panelH = 200f;
            float panelY = Screen.height - panelH;

            GUI.color = new Color(0.04f, 0.05f, 0.10f, 0.97f);
            GUI.DrawTexture(new Rect(0, panelY, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 0.6f, 0.15f);
            GUI.DrawTexture(new Rect(0, panelY, panelW, 4), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle header = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold };
            header.normal.textColor = new Color(1f, 0.85f, 0.3f);
            GUI.Label(new Rect(30, panelY + 10, 600, 32), "공격할 곤충을 선택하세요 [1-5]:", header);

            int count = raidController.TeamStats != null ? raidController.TeamStats.Length : 0;
            float btnW = Mathf.Min(240, (panelW - 60) / Mathf.Max(count, 1));
            float btnH = 120f;
            float btnY = panelY + 52;
            float startX = 30;

            for (int i = 0; i < count; i++)
            {
                var stats = raidController.TeamStats[i];
                if (stats == null) continue;
                bool alive = stats.CurrentHp > 0;
                float bx = startX + i * (btnW + 12);

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

                GUIStyle keyStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 24, fontStyle = FontStyle.Bold };
                keyStyle.normal.textColor = alive ? new Color(1f, 0.85f, 0.3f) : new Color(0.4f, 0.4f, 0.4f);
                GUI.color = alive ? new Color(0.15f, 0.12f, 0.05f) : new Color(0.06f, 0.06f, 0.06f);
                GUI.DrawTexture(new Rect(bx + 8, btnY + 8, 32, 32), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(bx + 8, btnY + 8, 32, 32), $"{i + 1}", keyStyle);

                GUIStyle nStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 22, fontStyle = FontStyle.Bold };
                nStyle.normal.textColor = alive ? Color.white : new Color(0.35f, 0.35f, 0.35f);
                GUI.Label(new Rect(bx + 8, btnY + 44, btnW - 16, 26), stats.Data.displayName, nStyle);

                float hpRatio = stats.MaxHp > 0 ? (float)stats.CurrentHp / stats.MaxHp : 0;
                GUIStyle hpStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold };
                hpStyle.normal.textColor = alive ? (hpRatio > 0.5f ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.95f, 0.5f, 0.2f))
                    : new Color(0.5f, 0.2f, 0.2f);
                GUI.Label(new Rect(bx + 8, btnY + 76, btnW - 16, 24),
                    alive ? $"HP {stats.CurrentHp}/{stats.MaxHp}" : "KO", hpStyle);

                Vector2 mouseGui = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
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

            DrawUniteButton(startX + count * (btnW + 12) + 20, btnY, btnH);
        }

        private void DrawSkillSelector()
        {
            if (selectedSlot < 0 || raidController.TeamStats == null || selectedSlot >= raidController.TeamStats.Length) return;
            var stats = raidController.TeamStats[selectedSlot];
            var skills = raidController.TeamSkills != null && selectedSlot < raidController.TeamSkills.Length
                ? raidController.TeamSkills[selectedSlot] : null;
            var cooldowns = raidController.TeamCooldowns != null && selectedSlot < raidController.TeamCooldowns.Length
                ? raidController.TeamCooldowns[selectedSlot] : null;

            float panelW = Screen.width;
            float panelH = 280f;
            float panelY = Screen.height - panelH;

            GUI.color = new Color(0.03f, 0.04f, 0.09f, 0.97f);
            GUI.DrawTexture(new Rect(0, panelY, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.5f, 0.9f);
            GUI.DrawTexture(new Rect(0, panelY, panelW, 4), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle header = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold };
            header.normal.textColor = new Color(0.9f, 0.85f, 0.5f);
            GUI.Label(new Rect(30, panelY + 10, 700, 32),
                $"{stats.Data.displayName}의 스킬 [Q/W/E/R]  |  ESC: 돌아가기", header);

            int count = skills != null ? Mathf.Min(skills.Length, 4) : 0;
            float btnW = Mathf.Min(300, (panelW - 80) / Mathf.Max(count, 1));
            float btnH = 180f;
            float btnY = panelY + 52;
            float startX = 30;
            string[] keyLabels = { "Q", "W", "E", "R" };

            for (int i = 0; i < count; i++)
            {
                var skill = skills[i];
                if (skill == null) continue;

                float bx = startX + i * (btnW + 14);
                int cd = cooldowns != null && i < cooldowns.Length ? cooldowns[i] : 0;
                bool canUse = cd <= 0;

                Vector2 mouseGui = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
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

                float pulse = 0.5f + Mathf.Sin(Time.time * 3f) * 0.15f;
                GUIStyle keyNum = new GUIStyle(GUI.skin.label)
                { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                keyNum.normal.textColor = canUse ? new Color(1f, 0.85f, 0.3f, pulse + 0.5f) : new Color(0.35f, 0.35f, 0.35f);
                GUI.color = canUse ? new Color(0.15f, 0.12f, 0.05f) : new Color(0.06f, 0.06f, 0.06f);
                GUI.DrawTexture(new Rect(bx + btnW - 46, btnY + 10, 36, 36), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(bx + btnW - 46, btnY + 10, 36, 36), keyLabels[i], keyNum);

                GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 24, fontStyle = FontStyle.Bold };
                nameStyle.normal.textColor = canUse ? Color.white : new Color(0.4f, 0.4f, 0.4f);
                GUI.Label(new Rect(bx + 12, btnY + 52, btnW - 24, 30), skill.displayName, nameStyle);

                GUIStyle typeLabel = new GUIStyle(GUI.skin.label) { fontSize = 19 };
                typeLabel.normal.textColor = canUse ? skillCol : new Color(0.3f, 0.3f, 0.3f);
                string typeStr = skill.effectType == SkillEffectType.Damage ? "공격 스킬" :
                                 skill.effectType == SkillEffectType.BuffAttack ? "버프 스킬" : "디버프 스킬";
                GUI.Label(new Rect(bx + 12, btnY + 86, btnW - 24, 24), typeStr, typeLabel);

                GUIStyle infoStyle = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold };
                infoStyle.normal.textColor = canUse ? new Color(0.9f, 0.85f, 0.65f) : new Color(0.3f, 0.3f, 0.3f);
                string pStr = skill.effectType == SkillEffectType.Damage ? $"위력: {skill.power}" :
                              skill.effectType == SkillEffectType.BuffAttack ? "ATK UP" : "ATK DOWN";
                GUI.Label(new Rect(bx + 12, btnY + 114, btnW - 24, 26), pStr, infoStyle);

                if (cd > 0)
                {
                    GUIStyle cdStyle = new GUIStyle(GUI.skin.label)
                    { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
                    cdStyle.normal.textColor = new Color(1f, 0.4f, 0.3f);
                    GUI.Label(new Rect(bx, btnY + 148, btnW - 14, 24), $"쿨다운 {cd}턴", cdStyle);

                    GUI.color = new Color(1f, 0.3f, 0.2f, 0.12f);
                    GUI.DrawTexture(new Rect(bx, btnY, btnW, btnH), Texture2D.whiteTexture);
                }
                else if (skill.cooldownTurns > 0)
                {
                    GUIStyle cdInfo = new GUIStyle(GUI.skin.label)
                    { fontSize = 17, alignment = TextAnchor.MiddleRight };
                    cdInfo.normal.textColor = new Color(0.45f, 0.45f, 0.5f);
                    GUI.Label(new Rect(bx, btnY + 150, btnW - 14, 22), $"쿨다운: {skill.cooldownTurns}턴", cdInfo);
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
                GUIStyle noSkill = new GUIStyle(GUI.skin.label)
                { fontSize = 24, alignment = TextAnchor.MiddleCenter };
                noSkill.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
                GUI.Label(new Rect(0, btnY, panelW, btnH), "스킬 없음", noSkill);
            }

            DrawUniteButton(startX + count * (btnW + 14) + 20, btnY, btnH);
        }

        private void DrawAttackEffects()
        {
            float t = Mathf.Clamp01(phaseTimer / 1f);
            float sw = Screen.width;
            float sh = Screen.height;

            if (phase == Phase.PlayerAttack && lastDmgToBoss > 0)
            {
                float bossX = sw * 0.5f;
                float bossY = sh * 0.12f;

                // Determine skill color based on skill type
                Color skillColor = new Color(0.3f, 0.7f, 1f);
                InsectElement element = InsectElement.Bug;
                if (raidController.TeamSkills != null && selectedSlot >= 0 && selectedSlot < raidController.TeamSkills.Length)
                {
                    var skills = raidController.TeamSkills[selectedSlot];
                    if (skills != null && skills.Length > 0 && skills[0] != null)
                    {
                        skillColor = GetSkillColor(skills[0].effectType);
                        element = skills[0].element;
                    }
                }
                Color elemCol = GetElementColor(element);

                if (t < 0.35f)
                {
                    float projT = t / 0.35f;
                    float easeT = projT * projT * (3f - 2f * projT);
                    int teamCount = raidController.TeamStats != null ? raidController.TeamStats.Length : 1;
                    float atkX = sw * 0.15f + (selectedSlot >= 0 ? selectedSlot : 0) * (sw * 0.7f / Mathf.Max(teamCount - 1, 1));
                    float atkY = sh * 0.40f;
                    float px = Mathf.Lerp(atkX, bossX, easeT);
                    float py = Mathf.Lerp(atkY, bossY, easeT) - Mathf.Sin(easeT * Mathf.PI) * 80f;

                    // Colored projectile based on skill type
                    float projSize = 16f + Mathf.Sin(projT * Mathf.PI * 3f) * 5f;
                    GUI.color = new Color(skillColor.r, skillColor.g, skillColor.b, 0.15f);
                    GUI.DrawTexture(new Rect(px - projSize * 2, py - projSize * 2, projSize * 4, projSize * 4), Texture2D.whiteTexture);
                    GUI.color = new Color(skillColor.r, skillColor.g, skillColor.b, 0.8f);
                    GUI.DrawTexture(new Rect(px - projSize / 2, py - projSize / 2, projSize, projSize), Texture2D.whiteTexture);
                    GUI.color = new Color(1, 1, 1, 0.9f);
                    GUI.DrawTexture(new Rect(px - 4, py - 4, 8, 8), Texture2D.whiteTexture);

                    for (int tr = 1; tr <= 4; tr++)
                    {
                        float trailT = Mathf.Max(0, projT - tr * 0.06f);
                        float trailEase = trailT * trailT * (3f - 2f * trailT);
                        float tx = Mathf.Lerp(atkX, bossX, trailEase);
                        float ty = Mathf.Lerp(atkY, bossY, trailEase) - Mathf.Sin(trailEase * Mathf.PI) * 80f;
                        GUI.color = new Color(skillColor.r, skillColor.g, skillColor.b, 0.3f - tr * 0.06f);
                        float ts = projSize * (1f - tr * 0.18f);
                        GUI.DrawTexture(new Rect(tx - ts / 2, ty - ts / 2, ts, ts), Texture2D.whiteTexture);
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

                        GUIStyle skillS = new GUIStyle(GUI.skin.label)
                        { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                        skillS.normal.textColor = new Color(skillColor.r, skillColor.g, skillColor.b, skillAlpha);
                        GUI.color = Color.white;
                        GUI.Label(new Rect(bossX - 170, bossY - 136 - dmgT * 20f, 340, 36), lastSkillUsedName, skillS);
                    }

                    // Damage number
                    float dmgAlpha = Mathf.Clamp01(1f - dmgT * 0.8f);
                    float dmgScale = 1f + Mathf.Sin(dmgT * Mathf.PI * 0.5f) * 0.3f;
                    GUIStyle dmg = new GUIStyle(GUI.skin.label)
                    { fontSize = (int)(48 * dmgScale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                    dmg.normal.textColor = new Color(1, 1, 0.3f, dmgAlpha);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(bossX - 70, bossY - 90 - dmgT * 50, 140, 56), $"-{lastDmgToBoss}", dmg);
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
                    GUIStyle aoeLabel = new GUIStyle(GUI.skin.label)
                    { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                    aoeLabel.normal.textColor = new Color(1f, 0.2f, 0.15f, aoeTextAlpha);
                    float shakeX = t < 0.5f ? Mathf.Sin(Time.time * 50f) * 5f : 0;
                    GUI.color = Color.white;
                    GUI.Label(new Rect(sw / 2 - 200 + shakeX, sh * 0.33f - t * 15f, 400, 52), "전체 공격!", aoeLabel);

                    // Damage number below
                    GUIStyle aoeDmg = new GUIStyle(GUI.skin.label)
                    { fontSize = 38, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                    aoeDmg.normal.textColor = new Color(1, 0.3f, 0.3f, 1f - t * 0.6f);
                    GUI.Label(new Rect(sw / 2 - 80, sh * 0.38f - t * 30, 160, 44), $"-{lastDmgToTeam}", aoeDmg);

                    // Per-team-member damage popup
                    if (raidController.TeamStats != null && t > 0.2f)
                    {
                        int teamCount = raidController.TeamStats.Length;
                        float totalW = sw * 0.7f;
                        float startX2 = sw * 0.15f;
                        float spacing = teamCount > 1 ? totalW / (teamCount - 1) : 0;
                        for (int i = 0; i < teamCount; i++)
                        {
                            if (raidController.TeamStats[i] == null) continue;
                            float mx = teamCount > 1 ? startX2 + i * spacing : sw * 0.5f;
                            float my = sh * 0.40f;
                            float memberT = Mathf.Clamp01((t - 0.2f - i * 0.05f) / 0.5f);
                            if (memberT > 0)
                            {
                                float mAlpha = Mathf.Clamp01(1f - memberT * 0.8f);
                                GUIStyle mDmg = new GUIStyle(GUI.skin.label)
                                { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                                mDmg.normal.textColor = new Color(1, 0.4f, 0.3f, mAlpha);
                                GUI.Label(new Rect(mx - 30, my - 40 - memberT * 30, 60, 30), $"-{lastDmgToTeam}", mDmg);
                            }
                        }
                    }
                }
                else if (lastHitSlot >= 0)
                {
                    int teamCount = raidController.TeamStats.Length;
                    float totalW = sw * 0.7f;
                    float startX2 = sw * 0.15f;
                    float spacing = teamCount > 1 ? totalW / (teamCount - 1) : 0;
                    float hx = teamCount > 1 ? startX2 + lastHitSlot * spacing : sw * 0.5f;
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
                        GUIStyle dmg2 = new GUIStyle(GUI.skin.label)
                        { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                        dmg2.normal.textColor = new Color(1, 0.3f, 0.3f, dmgAlpha2);
                        GUI.color = Color.white;
                        float shakeX = impT < 0.5f ? Mathf.Sin(Time.time * 50f) * 4f : 0;
                        GUI.Label(new Rect(hx - 40 + shakeX, hy - 60 - impT * 30, 80, 36), $"-{lastDmgToTeam}", dmg2);
                    }
                }
            }
            GUI.color = Color.white;
        }

        private void DrawActionText()
        {
            if (string.IsNullOrEmpty(actionText)) return;
            float alpha = Mathf.Clamp01(actionTimer / 0.5f);
            float cx = Screen.width / 2f;
            float cy = Screen.height * 0.50f;

            string[] lines = actionText.Split('\n');
            float lineH = 40f;
            float totalH = lines.Length * lineH;
            float bgW = 700;

            GUI.color = new Color(0, 0, 0, 0.75f * alpha);
            GUI.DrawTexture(new Rect(cx - bgW / 2, cy - 8, bgW, totalH + 18), Texture2D.whiteTexture);
            GUI.color = new Color(0.3f, 0.5f, 0.9f, 0.5f * alpha);
            GUI.DrawTexture(new Rect(cx - bgW / 2, cy - 8, bgW, 3), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - bgW / 2, cy + totalH + 7, bgW, 3), Texture2D.whiteTexture);

            GUIStyle style = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(1, 1, 1, alpha);
            GUI.color = Color.white;

            for (int i = 0; i < lines.Length; i++)
                GUI.Label(new Rect(cx - bgW / 2, cy + i * lineH, bgW, lineH), lines[i], style);
        }

        private void DrawResult()
        {
            float alpha = Mathf.Clamp01(resultTimer / 0.5f);
            float cx = Screen.width / 2f;
            float cy = Screen.height * 0.26f;
            float sw = Screen.width;
            float sh = Screen.height;

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
                GUIStyle resultStyle = new GUIStyle(GUI.skin.label)
                { fontSize = titleFs, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                resultStyle.normal.textColor = new Color(1f, 0.85f, 0.2f, alpha);
                GUI.Label(new Rect(cx - 300, cy, 600, 62), "RAID CLEAR!", resultStyle);

                // Capture message
                GUIStyle sub = new GUIStyle(GUI.skin.label)
                { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                sub.normal.textColor = new Color(0.9f, 0.9f, 0.9f, alpha);
                GUI.Label(new Rect(cx - 300, cy + 68, 600, 28),
                    $"보스 {raidController.BossStats.Data.displayName}을(를) 포획했다!", sub);

                // Animated reward display
                float rewardDelay = 0.8f;
                float candyAlpha = Mathf.Clamp01((resultTimer - rewardDelay) * 3f);
                float xpAlpha = Mathf.Clamp01((resultTimer - rewardDelay - 0.2f) * 3f);
                float bonusAlpha = Mathf.Clamp01((resultTimer - rewardDelay - 0.4f) * 3f);

                // Candy reward with bounce
                float candyBounce = candyAlpha > 0 ? Mathf.Max(0, Mathf.Sin((resultTimer - rewardDelay) * 8f) * (1f - candyAlpha) * 10f) : 0;
                GUIStyle valStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                valStyle.normal.textColor = new Color(1f, 0.5f, 0.8f, candyAlpha);
                GUI.Label(new Rect(cx - 200, cy + 100 - candyBounce, 200, 28), $"+{raidController.RewardCandy} Candy (x3)", valStyle);

                // XP reward with bounce
                float xpBounce = xpAlpha > 0 ? Mathf.Max(0, Mathf.Sin((resultTimer - rewardDelay - 0.2f) * 8f) * (1f - xpAlpha) * 10f) : 0;
                valStyle.normal.textColor = new Color(0.4f, 0.8f, 1f, xpAlpha);
                GUI.Label(new Rect(cx, cy + 100 - xpBounce, 200, 28), $"+{raidController.RewardExp} XP (x3)", valStyle);

                // Bonus text
                GUIStyle bonus = new GUIStyle(GUI.skin.label)
                { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                bonus.normal.textColor = new Color(0.6f, 1f, 0.6f, bonusAlpha);
                GUI.Label(new Rect(cx - 220, cy + 140, 440, 26), "레이드 보너스: 보상 x3!", bonus);

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
                GUIStyle hint = new GUIStyle(GUI.skin.label)
                { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                hint.normal.textColor = new Color(0.6f, 0.6f, 0.6f, hintAlpha * 0.7f);
                GUI.color = Color.white;
                GUI.Label(new Rect(cx - 150, cy + 180, 300, 24), "잠시 후 자동으로 돌아갑니다...", hint);
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
                GUIStyle resultStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 52, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                resultStyle.normal.textColor = new Color(1f, 0.25f, 0.2f, alpha);
                GUI.Label(new Rect(cx - 300 + failShake, cy + 10, 600, 62), "RAID FAILED", resultStyle);

                // Subtitle
                GUIStyle failSub = new GUIStyle(GUI.skin.label)
                { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                failSub.normal.textColor = new Color(0.6f, 0.4f, 0.4f, alpha);
                GUI.Label(new Rect(cx - 250, cy + 80, 500, 28), "팀이 전멸했습니다...", failSub);

                // Timer hint
                float hintAlpha = Mathf.Clamp01(resultTimer - 2f);
                GUIStyle hint = new GUIStyle(GUI.skin.label)
                { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                hint.normal.textColor = new Color(0.5f, 0.4f, 0.4f, hintAlpha * 0.7f);
                GUI.Label(new Rect(cx - 150, cy + 116, 300, 24), "잠시 후 자동으로 돌아갑니다...", hint);
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

            float bw = 160f;
            float bh = Mathf.Min(h, 120f);
            float by = y + (h - bh) / 2f;

            float pulse = Mathf.Sin(Time.time * 4f) * 0.15f + 0.85f;
            Color bgCol = new Color(0.8f * pulse, 0.55f * pulse, 0.05f, 0.95f);

            Vector2 mouseGui = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
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
            GUIStyle label = new GUIStyle(GUI.skin.label)
            { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            label.normal.textColor = new Color(1f, 0.95f, 0.5f);
            GUI.Label(new Rect(x, by + 8, bw, 28), "★ 합체공격 ★", label);

            GUIStyle keyHint = new GUIStyle(GUI.skin.label)
            { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            keyHint.normal.textColor = new Color(1f, 0.8f, 0.3f, pulse);
            GUI.Label(new Rect(x, by + 38, bw, 22), "[F] 또는 클릭", keyHint);

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
            float bx = (Screen.width - barW) / 2f;
            float by = Screen.height - 160f;

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

            GUIStyle labelS = new GUIStyle(GUI.skin.label)
            { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            labelS.normal.textColor = ready ? new Color(1f, 0.95f, 0.4f) : new Color(0.7f, 0.7f, 0.8f);
            GUI.color = Color.white;
            GUI.Label(new Rect(bx, by, barW, barH), ready ? "★ 합체공격 준비! [F] ★" : $"합체 게이지  {Mathf.RoundToInt(gauge)}/{Mathf.RoundToInt(max)}", labelS);

            if (ready)
            {
                GUIStyle hint = new GUIStyle(GUI.skin.label)
                { fontSize = 16, alignment = TextAnchor.MiddleCenter };
                hint.normal.textColor = new Color(1f, 0.8f, 0.3f, Mathf.Sin(Time.time * 3f) * 0.3f + 0.7f);
                GUI.Label(new Rect(bx, by + barH + 2, barW, 22), "F키를 눌러 전체 곤충이 동시 공격!", hint);
            }
        }

        private void DrawUniteAttackAnimation()
        {
            float sw = Screen.width;
            float sh = Screen.height;
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
                        GUIStyle dmgS = new GUIStyle(GUI.skin.label)
                        { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                        dmgS.normal.textColor = new Color(memberCol.r, Mathf.Min(1, memberCol.g + 0.3f), memberCol.b, dmgAlpha);
                        GUI.color = Color.white;
                        GUI.Label(new Rect(dmgX, dmgY, 140, 40), $"-{dmg}", dmgS);
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

                GUIStyle uniteLabel = new GUIStyle(GUI.skin.label)
                { fontSize = fs, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                uniteLabel.normal.textColor = new Color(1f, 0.9f, 0.2f, txtAlpha);
                GUI.color = Color.white;
                GUI.Label(new Rect(shakeX, sh * 0.42f + shakeY, sw, 60), "★ 합체공격! ★", uniteLabel);
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

                GUIStyle totalS = new GUIStyle(GUI.skin.label)
                { fontSize = totalFs, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                totalS.normal.textColor = new Color(1f, 0.2f, 0.1f, totalAlpha);
                GUI.color = Color.white;
                GUI.Label(new Rect(0, sh * 0.30f, sw, 60), $"TOTAL -{lastDmgToBoss}", totalS);
            }
        }

        private void EndRaid()
        {
            if (BattleArenaController.Instance != null)
                BattleArenaController.Instance.CleanupArena();

            if (AudioManager.Instance != null) AudioManager.Instance.PlayBGM(BgmType.Explore);
            phase = Phase.None;
            if (cameraFollower != null) cameraFollower.ExitBattleMode();
            if (playerMovement != null) playerMovement.SetFrozen(false);
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
            float sw = Screen.width;
            float sh = Screen.height;

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
            float sw = Screen.width;
            float sh = Screen.height;

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

                    GUIStyle arrowStyle = new GUIStyle(GUI.skin.label)
                    { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                    arrowStyle.normal.textColor = new Color(0.3f, 0.85f, 0.5f, arrowAlpha);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(arrowX - 12, arrowY, 24, 28), "\u25b2", arrowStyle);
                }

                // Skill name text
                if (!string.IsNullOrEmpty(displayText))
                {
                    float textAlpha = Mathf.Clamp01(1f - t * 1.2f);
                    GUIStyle txtStyle = new GUIStyle(GUI.skin.label)
                    { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                    txtStyle.normal.textColor = new Color(0.3f, 0.85f, 0.5f, textAlpha);
                    GUI.color = Color.white;

                    // Background glow
                    GUI.color = new Color(0.2f, 0.5f, 0.3f, textAlpha * 0.2f);
                    GUI.DrawTexture(new Rect(memberX - 110, memberY - 80 - t * 20f, 220, 38), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(memberX - 110, memberY - 78 - t * 20f, 220, 34), displayText, txtStyle);
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

                    GUIStyle arrowStyle = new GUIStyle(GUI.skin.label)
                    { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                    arrowStyle.normal.textColor = new Color(0.9f, 0.2f, 0.2f, arrowAlpha);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(arrowX - 12, arrowY, 24, 28), "\u25bc", arrowStyle);
                }

                // Skill name text
                if (!string.IsNullOrEmpty(displayText))
                {
                    float textAlpha = Mathf.Clamp01(1f - t * 1.2f);
                    GUIStyle txtStyle = new GUIStyle(GUI.skin.label)
                    { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                    txtStyle.normal.textColor = new Color(0.9f, 0.25f, 0.2f, textAlpha);
                    GUI.color = Color.white;

                    // Background glow
                    GUI.color = new Color(0.4f, 0.05f, 0.05f, textAlpha * 0.2f);
                    GUI.DrawTexture(new Rect(bossX - 110, bossY + 80 + t * 15f, 220, 38), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(bossX - 110, bossY + 82 + t * 15f, 220, 34), displayText, txtStyle);
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
                default: return Color.gray;
            }
        }

        public void AutoWire(RaidBattleController rc, CameraFollower cam, PlayerMovement pm = null)
        {
            if (raidController != null && raidController != rc)
            {
                raidController.RaidUpdated -= OnRaidUpdated;
                raidController.RaidEnded -= OnRaidEnded;
            }

            if (raidController == null || raidController != rc)
            {
                raidController = rc;
                if (raidController != null)
                {
                    raidController.RaidUpdated -= OnRaidUpdated;
                    raidController.RaidEnded -= OnRaidEnded;
                    raidController.RaidUpdated += OnRaidUpdated;
                    raidController.RaidEnded += OnRaidEnded;
                }
            }

            if (cameraFollower == null) cameraFollower = cam;
            if (playerMovement == null) playerMovement = pm;
        }
    }
}
