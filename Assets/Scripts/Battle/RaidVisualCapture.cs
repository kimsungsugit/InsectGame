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
    /// <summary>Actual raid UI fixture; deliberately has no collection or reward persistence.</summary>
    public static class RaidVisualCapture
    {
        /// <param name="unite">첫 차례에 게이지를 채워 합체공격을 쏜다 — 팀 전원 공격 연출 검수용.</param>
        public static IEnumerator Run(string output, CameraFollower follower, BattleArenaController arena, bool unite = false)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var controller = new GameObject("QARaidController").AddComponent<RaidBattleController>();
            var ui = new GameObject("QARaidUI").AddComponent<RaidBattleUI>();
            controller.AutoWire(arena);
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
            InsectData boss = BattleVisualCapture.Fixture("rhinoceros_beetle", "숲의 수호자");
            boss.baseHp = 135;
            var entity = new GameObject("QAOriginalBoss").AddComponent<InsectEntity>();
            entity.BuildForBattle(boss, 15, false);
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
            MethodInfo auto = typeof(RaidBattleUI).GetMethod("TryAutoAll", flags);
            MethodInfo uniteAttack = typeof(RaidBattleUI).GetMethod("TryUnite", flags);
            bool uniteFired = false;
            float start = Time.realtimeSinceStartup, next = 0f, readyAt = 0f, resultAt = -1f;
            int frame = 0;
            bool bossAttackSeen = false, aoeSeen = false;
            string previous = "";
            using (var csv = new StreamWriter(Path.Combine(output, "timeline.csv")))
            {
                csv.WriteLine("frame,elapsed,phase,actualPlayerHp,actualEnemyHp,displayPlayerHp,displayEnemyHp,impactRevealed,phaseTimer,width,height,activeSlot,round,bossAoe");
                while (Time.realtimeSinceStartup - start < 105f)
                {
                    yield return new WaitForEndOfFrame();
                    float time = Time.realtimeSinceStartup - start;
                    string phase = phaseField.GetValue(ui).ToString();
                    bool changed = phase != previous;
                    if (changed && phase == "SelectSkill") readyAt = time;
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
                        csv.WriteLine(FormattableString.Invariant($"{frame},{time:F3},{phase},{controller.TeamStats.Sum(s => s.CurrentHp)},{controller.BossStats.CurrentHp},{(displayed == null ? 0f : displayed.Sum())},{bossHp.GetValue(ui)},,,{Screen.width},{Screen.height},{controller.ActiveSlot},{controller.TurnNumber},{controller.BossUsedAoe}"));
                        csv.Flush(); frame++; next = time + BattleVisualCapture.CaptureInterval;
                    }
                    previous = phase;
                    if (phase == "SelectSkill" && time - readyAt >= 0.6f)
                    {
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
                    if (resultAt >= 0f && time - resultAt >= 1.8f) break;
                }
            }
            File.WriteAllText(Path.Combine(output, "manifest.txt"),
                $"Actual standalone IMGUI raid fixture\nUnite={uniteFired}\nSeed=8173\nSize={Screen.width}x{Screen.height}\nFrames={frame}\nResultReached={resultAt >= 0f}\nBossAttackSeen={bossAttackSeen}\nAoeSeen={aoeSeen}\nNo audio/input hit-test/performance certification\n");
            Application.Quit(resultAt >= 0f && bossAttackSeen ? 0 : 3);
        }
    }
}
#endif
