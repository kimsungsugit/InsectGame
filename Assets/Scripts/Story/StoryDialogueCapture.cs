#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using System.Reflection;
using InsectGame.Core;
using InsectGame.UI;
using UnityEngine;

namespace InsectGame.Story
{
    /// <summary>Actual IMGUI dialogue preview, isolated from story progress and save services.</summary>
    public static class StoryDialogueCapture
    {
        public static IEnumerator Run(string output, Camera camera)
        {
            var regions = RegionDefinitions.CreateAll();
            new GameObject("VillageQA").AddComponent<VillageBuilder>().Build(regions);
            Vector3 center = GameObject.Find("MainVillage").transform.position;
            camera.transform.position = center + new Vector3(0, 14, 23);
            camera.transform.LookAt(center + Vector3.up);
            Light light = new GameObject("StoryQAKey").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(48, -30, 0);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.position = center;
            ground.transform.localScale = Vector3.one * 20;
            ground.GetComponent<Renderer>().material.color = new Color(.30f, .40f, .29f);
            NpcDialogueUI ui = new GameObject("DialogueQA").AddComponent<NpcDialogueUI>();
            // No StoryDirector is created or injected: replay cannot grant rewards or write progress.
            FieldInfo index = typeof(NpcDialogueUI).GetField("lineIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            // 타자 효과 — 줄을 다 드러낸 상태로 찍는다(반쯤 나온 글자는 레이아웃 검수에 방해만 된다).
            FieldInfo revealAll = typeof(NpcDialogueUI).GetField("lineRevealAll", BindingFlags.Instance | BindingFlags.NonPublic);
            int captures = 0;
            using (var manifest = new StreamWriter(Path.Combine(output, "dialogue.tsv")))
            {
                manifest.WriteLine("beat\tline\tspeaker\ttext");
                // 무대(좌우 인물·이름표·지문·번쩍임) 검수용 — 셋이 주고받는 장면, 지문과 연출이 많은 클라이맥스,
                // 2막 대치(셋째 화자 교대), 2막 절정(외침·붕괴), 최종장(지문 목소리·마지막 줄 선택지).
                foreach (string id in new[] { "ch4_reach_swamp", "ch6_secret", "ch8_confront", "ch10_confront", "fin_unnamed" })
                {
                    if (!StoryService.TryGetBeat(id, out StoryBeat beat))
                    { Debug.LogError("Missing dialogue fixture " + id); Application.Quit(3); yield break; }
                    ui.ShowStoryReplay(beat);
                    for (int line = 0; line < beat.lines.Count; line++)
                    {
                        index.SetValue(ui, line);
                        if (revealAll != null) revealAll.SetValue(ui, true);
                        yield return new WaitForSecondsRealtime(.8f);
                        yield return new WaitForEndOfFrame();
                        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                        File.WriteAllBytes(Path.Combine(output, id + "-" + line + ".png"), shot.EncodeToPNG());
                        UnityEngine.Object.Destroy(shot);
                        manifest.WriteLine($"{id}\t{line}\t{beat.lines[line].speaker}\t{beat.lines[line].text}");
                        captures++;
                    }
                    ui.CloseModal();
                }
            }
            var npc = new GameObject("AmbientQA").AddComponent<InsectGame.NPC.VillagerNpc>();
            npc.Initialize(new NpcSpawnAnchor { regionId = "meadow" }, "qa_resident", "마을 주민", 8173);
            var tracker = new GameObject("ObjectiveLayoutQA").AddComponent<StoryObjectiveTracker>();
            tracker.enabled = false;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(StoryObjectiveTracker).GetField("hasObjective", flags).SetValue(tracker, true);
            typeof(StoryObjectiveTracker).GetField("label", flags).SetValue(tracker, "마을 어르신에게 말 걸기");
            ui.AutoWire(tracker);
            ui.Show(npc);
            yield return new WaitForSecondsRealtime(.8f);
            yield return new WaitForEndOfFrame();
            Texture2D ambientShot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, "ambient-objective.png"), ambientShot.EncodeToPNG());
            UnityEngine.Object.Destroy(ambientShot);
            ui.CloseModal();
            File.WriteAllText(Path.Combine(output, "README.txt"),
                $"Actual standalone IMGUI. {captures} lines from real story data. Replay with no StoryDirector/save services. Ambient goal row uses an injected layout sample, not live objective selection. Village background is a staging fixture, not the authored chapter location. Does not validate campaign triggering or physical input.");
            Application.Quit(captures > 0 ? 0 : 3);
        }
    }
}
#endif
