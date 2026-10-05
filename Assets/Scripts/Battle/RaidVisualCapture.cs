#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using InsectGame.UI;
using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>
    /// Actual raid UI fixture; deliberately has no collection or reward persistence.
    ///
    /// 장면: <c>raid</c>(보통 레이드) · <c>raid-unite</c>(첫 차례 합체공격) · <c>raid-forms</c>(이름 없는 사마귀 — 수문장 등장 컷 + 66%·33%
    /// 그림자 변신 두 번 + 그림자 모습) · <c>guardian-intro</c>(숲의 수문장 헤라클레스 — 등장 컷만 보고 첫 차례 1초 뒤 끝낸다).
    /// 4단계 연출도 여기서 보인다 — <c>raid</c>의 첫 장면이 진입 샷(보스 옆에서 팀 뒤로, 1초), 끝 장면이 레이드 승리(팀 전원 점프 + 팀 앞으로
    /// 도는 카메라 — 결과 뒤 2.4초까지 찍힌다), 팀원 강화 스킬 뒤엔 몸의 불꽃결·육각 막.
    /// </summary>
    public static class RaidVisualCapture
    {
        /// <param name="unite">첫 차례에 게이지를 채워 합체공격을 쏜다 — 팀 전원 공격 연출 검수용.</param>
        public static IEnumerator Run(string output, CameraFollower follower, BattleArenaController arena, bool unite = false)
        {
            return Run(output, follower, arena, unite ? "raid-unite" : "raid");
        }

        public static IEnumerator Run(string output, CameraFollower follower, BattleArenaController arena, string scenario)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            bool unite = scenario == "raid-unite";
            bool forms = scenario == "raid-forms";
            bool guardianIntro = scenario == "guardian-intro";
            var controller = new GameObject("QARaidController").AddComponent<RaidBattleController>();
            var ui = new GameObject("QARaidUI").AddComponent<RaidBattleUI>();
            controller.AutoWire(arena);
            arena.AutoWire(controller);   // 몸 상태 표시(강화 스택·보스 기절)의 원천 — Life partial
            controller.SetRandomSeed(8173);
            ui.AutoWire(controller, follower, null);
            ui.AutoWire(arena);
            string[] ids = { "rhinoceros_beetle", "stag_beetle", "mantis", "dragonfly", "ant" };
            string[] names = { "장수풍뎅이", "사슴벌레", "사마귀", "잠자리", "개미" };
            var team = new InsectData[5];
            var levels = new int[5];
            var skills = new InsectSkill[5][];
            for (int i = 0; i < 5; i++)
            {
                team[i] = BattleVisualCapture.Fixture(ids[i], names[i]);
                levels[i] = 15;
                skills[i] = new[] { team[i].skills[0] };
            }
            InsectData boss;
            string guardianRegion = null;
            if (forms)
            {
                // 그림자 — 이름 없는 땅의 수문장이 66%에 호랑나비, 33%에 반딧불이의 모습을 빌린다(RaidBossForms).
                boss = BattleVisualCapture.Fixture("mantis_unnamed", "이름 없는 사마귀");
                boss.rarity = InsectRarity.Legendary;
                boss.primaryType = InsectElement.Leaf;
                InsectData swallowtail = BattleVisualCapture.Fixture("butterfly_swallowtail", "호랑나비");
                swallowtail.rarity = InsectRarity.Rare;
                swallowtail.primaryType = InsectElement.Wind;
                InsectData firefly = BattleVisualCapture.Fixture("firefly_blue", "푸른 반딧불이");
                firefly.rarity = InsectRarity.Epic;
                firefly.primaryType = InsectElement.Light;
                controller.SetInsectLookup(id => id == swallowtail.insectId ? swallowtail : id == firefly.insectId ? firefly : null);
                guardianRegion = "nameless";
            }
            else if (guardianIntro)
            {
                boss = BattleVisualCapture.Fixture("beetle_hercules", "숲의 문지기 헤라클레스");
                boss.rarity = InsectRarity.Epic;
                boss.primaryType = InsectElement.Earth;
                guardianRegion = "forest";
            }
            else boss = BattleVisualCapture.Fixture("rhinoceros_beetle", "숲의 수호자");
            boss.baseHp = 135;
            var entity = new GameObject("QAOriginalBoss").AddComponent<InsectEntity>();
            entity.BuildForBattle(boss, 15, false);
            // 수문장 표식은 BuildForBattle 뒤에 — 그쪽이 지운다. 표식이 있으면 인트로가 등장 컷 길이(BattleStaging.GuardianIntroSeconds)가 된다.
            if (guardianRegion != null) entity.MarkAsGuardian(guardianRegion);
            if (!controller.StartRaid(entity, team, levels, null, skills))
            { Debug.LogError("Raid fixture failed to start"); Application.Quit(3); yield break; }
            var rise = GameObject.Find("BossRise");
            if (rise != null)
            {
                Renderer renderer = rise.GetComponent<Renderer>();
                Debug.Log($"QA BossRise: position={rise.transform.position}, scale={rise.transform.lossyScale}, bounds={renderer.bounds}, shader={renderer.sharedMaterial.shader.name}, active={renderer.enabled}");
            }
            FieldInfo phaseField = typeof(RaidBattleUI).GetField("phase", flags);
            FieldInfo bossHp = typeof(RaidBattleUI).GetField("displayBossHp", flags);
            FieldInfo teamHp = typeof(RaidBattleUI).GetField("displayTeamHp", flags);
            FieldInfo introTimer = typeof(RaidBattleUI).GetField("introTimer", flags);
            MethodInfo auto = typeof(RaidBattleUI).GetMethod("TryAutoAll", flags);
            MethodInfo uniteAttack = typeof(RaidBattleUI).GetMethod("TryUnite", flags);
            PropertyInfo currentHp = typeof(InsectBattleStats).GetProperty("CurrentHp");
            bool uniteFired = false;
            float start = Time.realtimeSinceStartup, next = 0f, readyAt = 0f, resultAt = -1f, firstReadyAt = -1f;
            int frame = 0, transforms = 0;
            bool bossAttackSeen = false, aoeSeen = false;
            string previous = "";
            using (var csv = new StreamWriter(Path.Combine(output, "timeline.csv")))
            {
                csv.WriteLine("frame,elapsed,phase,actualPlayerHp,actualEnemyHp,displayPlayerHp,displayEnemyHp,impactRevealed,phaseTimer,width,height,activeSlot,round,bossAoe,bossForm,transformProgress,introTimer,introProgress,resultShown,resultCanClose");
                while (Time.realtimeSinceStartup - start < 105f)
                {
                    yield return new WaitForEndOfFrame();
                    float time = Time.realtimeSinceStartup - start;
                    string phase = phaseField.GetValue(ui).ToString();
                    bool changed = phase != previous;
                    if (changed && phase == "SelectSkill")
                    {
                        readyAt = time;
                        if (firstReadyAt < 0f) firstReadyAt = time;
                    }
                    if (changed && phase == "BossTransform") transforms++;
                    if (phase == "Result" && resultAt < 0f) resultAt = time;
                    bossAttackSeen |= phase == "BossAttack";
                    aoeSeen |= controller.BossUsedAoe;
                    if (changed || time >= next)
                    {
                        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                        File.WriteAllBytes(Path.Combine(output, $"frame-{frame:D5}.jpg"), shot.EncodeToJPG(85));
                        if (changed) File.WriteAllBytes(Path.Combine(output, $"state-{frame:D5}-{phase}.png"), shot.EncodeToPNG());
                        UnityEngine.Object.Destroy(shot);
                        float[] displayed = (float[])teamHp.GetValue(ui);
                        csv.WriteLine(FormattableString.Invariant($"{frame},{time:F3},{phase},{controller.TeamStats.Sum(s => s.CurrentHp)},{controller.BossStats.CurrentHp},{(displayed == null ? 0f : displayed.Sum())},{bossHp.GetValue(ui)},,,{Screen.width},{Screen.height},{controller.ActiveSlot},{controller.TurnNumber},{controller.BossUsedAoe},{controller.BossFormIndex},{ui.BossTransformProgress:F3},{introTimer.GetValue(ui)},{ui.IntroProgress:F3},{ui.ResultShownSeconds:F2},{ui.ResultCanClose}"));
                        csv.Flush(); frame++; next = time + BattleVisualCapture.CaptureInterval;
                    }
                    previous = phase;
                    // 등장 컷만 보는 장면 — 첫 차례가 열리고(원래 구도로 돌아온 뒤) 1초를 더 찍고 끝낸다.
                    if (guardianIntro && firstReadyAt >= 0f && time - firstReadyAt >= 1.0f) break;
                    if (phase == "SelectSkill" && time - readyAt >= 0.6f)
                    {
                        // 변신 검수 — 다음 임계 바로 위로 HP를 맞춰 두면 첫 일격이 그 임계를 넘는다(피해량과 무관하게 결정적).
                        // 66% 위 → 첫 변신, 33% 위 → 둘째 변신, 둘 다 봤으면 4%로 깎아 결과까지 빨리 간다.
                        if (forms && controller.BossStats != null)
                        {
                            int max = controller.BossStats.MaxHp;
                            int target = controller.BossFormIndex == 0 ? Mathf.CeilToInt(max * 0.67f)
                                : controller.BossFormIndex == 1 ? Mathf.CeilToInt(max * 0.34f)
                                : Mathf.Max(1, Mathf.CeilToInt(max * 0.04f));
                            if (controller.BossStats.CurrentHp > target) currentHp.SetValue(controller.BossStats, target);
                        }
                        if (unite && !uniteFired)
                        {
                            typeof(RaidBattleController).GetProperty("UniteGauge")
                                .SetValue(controller, RaidBattleController.UniteGaugeMax);
                            uniteAttack.Invoke(ui, null);
                            uniteFired = true;
                        }
                        else auto.Invoke(ui, null);
                        readyAt = time;
                    }
                    // 레이드 승리 연출(BattleFlourish.VictorySeconds 2초)이 끝까지 찍히게.
                    if (resultAt >= 0f && time - resultAt >= BattleFlourish.VictorySeconds + 0.4f) break;
                }
            }
            File.WriteAllText(Path.Combine(output, "manifest.txt"),
                $"Actual standalone IMGUI raid fixture\nScenario={scenario}\nGuardianRegion={guardianRegion ?? "-"}\nUnite={uniteFired}\nSeed=8173\nSize={Screen.width}x{Screen.height}\nFrames={frame}\nResultReached={resultAt >= 0f}\nBossAttackSeen={bossAttackSeen}\nAoeSeen={aoeSeen}\nBossTransforms={transforms}\nFinalBossForm={controller.BossFormIndex}\nIntroSeconds(firstSelectSkill)={(firstReadyAt >= 0f ? firstReadyAt.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "-")}\nNo audio/input hit-test/performance certification\n");
            bool ok = guardianIntro ? firstReadyAt >= 0f
                : forms ? resultAt >= 0f && transforms >= 2
                : resultAt >= 0f && bossAttackSeen;
            Application.Quit(ok ? 0 : 3);
        }
    }
}
#endif
