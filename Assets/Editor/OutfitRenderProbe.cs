#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using InsectGame.Core;
using UnityEditor;
using UnityEngine;

namespace InsectGame.EditorTools
{
    /// <summary>
    /// 의상을 실제로 입힌 캐릭터를 <b>그려서</b> 확인한다 — 왕관·망토·날개 같은 spawn 파츠가
    /// 정말 보이는지, 도구 10종이 형태를 바꾸는지.
    ///
    /// 왜 필요한가: 의상 형태는 <c>OutfitShapeLibraryTests</c>·<c>OutfitShapeParityTests</c>가
    /// <b>좌표와 스키마</b>를 고정하지만, "그 좌표에 실제로 메시가 그려지는가"는 아무도 안 본다.
    /// 면이 뒤집히거나 레이어가 어긋나면 <b>예외 없이</b> 안 보이는데, 그건 이번 라운드에
    /// <c>ProcMeshLibrary.Disc</c>에서 실제로 났던 종류의 결함이다.
    ///
    /// <b>IMGUI를 거치지 않는다.</b> 의상 화면은 OnGUI라 배치 캡처가 불가능하지만,
    /// 그 화면이 보여주는 <b>마네킹은 3D 리그</b>다(<c>CharacterModelPreviewRenderer</c>가
    /// 전용 레이어·카메라로 RenderTexture에 찍는다). 여기서는 같은 방식으로 직접 리그를 세워
    /// PNG로 남긴다 — 스탠드얼론 빌드가 필요 없다.
    ///
    /// <code>
    /// "$UNITY_EDITOR_PATH" -batchmode -projectPath "X:/" \
    ///   -executeMethod InsectGame.EditorTools.OutfitRenderProbe.Run \
    ///   -outfitOut .claude/cache/outfit -logFile .claude/cache/outfit-probe.log
    /// </code>
    ///
    /// 종료 코드는 <b>모든 대상이 화면에 실제 픽셀을 남겼을 때만</b> 0이다.
    /// </summary>
    public static class OutfitRenderProbe
    {
        private const string ScenePath = "Assets/Scenes/PlayScene.unity";
        private const string StageKey = "InsectGame.OutfitRenderProbe.Stage";

        /// <summary>곤충 프리뷰 30 · 캐릭터 프리뷰 29 · SubArea 31과 겹치지 않는 레이어.</summary>
        private const int ProbeLayer = 28;
        private static readonly Vector3 RigOrigin = new Vector3(0f, -6400f, 0f);
        private const int ShotW = 320;
        private const int ShotH = 420;

        private static string outDir = ".claude/cache/outfit";
        private static readonly StringBuilder Report = new StringBuilder();
        private static int failures;
        private static int shots;

        private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        private static GameObject mannequin;
        private static Camera cam;

        public static void Run()
        {
            ParseArgs();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                ScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            SessionState.SetString(StageKey, outDir);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            string stage = SessionState.GetString(StageKey, "");
            if (string.IsNullOrEmpty(stage)) return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return;

            SessionState.EraseString(StageKey);
            outDir = stage;
            failures = 0;
            shots = 0;
            Report.Length = 0;
            stack.Clear();
            stack.Push(Drive());
            EditorApplication.update += Pump;
        }

        /// <summary>수동 MoveNext는 중첩 코루틴을 안 펼친다 — StarterInsectProbe와 같은 스택 방식.</summary>
        private static void Pump()
        {
            if (stack.Count == 0) return;
            try
            {
                IEnumerator top = stack.Peek();
                if (!top.MoveNext())
                {
                    stack.Pop();
                    if (stack.Count == 0) { EditorApplication.update -= Pump; Finish(); }
                    return;
                }
                if (top.Current is IEnumerator nested) stack.Push(nested);
            }
            catch (Exception e)
            {
                Log("예외: " + e);
                failures++;
                stack.Clear();
                EditorApplication.update -= Pump;
                Finish();
            }
        }

        private static IEnumerator Drive()
        {
            float waited = 0f;
            while (CharacterOutfitManager.Instance == null && waited < 25f)
            {
                waited += 0.1f;
                yield return null;
            }
            if (CharacterOutfitManager.Instance == null)
            {
                Log("부트스트랩 실패 — CharacterOutfitManager가 서지 않았다");
                failures++;
                yield break;
            }

            Directory.CreateDirectory(Path.GetFullPath(outDir));
            BuildRig();
            yield return null;

            Report.AppendLine("# 의상 렌더 확인 — 마네킹 3D 리그 직접 촬영");
            Report.AppendLine();
            Report.AppendLine("각 행은 그 아이템만 입힌 마네킹이다. `기본 대비 변화`는 아무것도 안 입은");
            Report.AppendLine("상태와의 픽셀 차이 — **0이면 그 의상이 화면에 아무것도 그리지 않았다는 뜻**이다.");
            Report.AppendLine();
            Report.AppendLine("| 슬롯 | itemId | 그려진 픽셀 | 기본 대비 변화 | 결과 |");
            Report.AppendLine("|---|---|---|---|---|");

            // 기준: 아무 의상도 없는 상태
            OutfitLoadout bare = new OutfitLoadout();
            byte[] baseline = null;
            yield return Shoot(bare, "00_bare", v => baseline = v);

            // 레시피가 있는 아이템 — 형태가 실제로 나타나야 하는 것들
            foreach (string id in Targets())
            {
                yield return ProbeItem(id, baseline);
            }

            CleanupRig();
        }

        /// <summary>
        /// 레시피를 가진 아이템 중 슬롯을 골고루 덮는 표본.
        /// 45개 전부를 찍으면 프로세스가 길어지고 보고서도 읽기 어려워, spawn·bind·hideNodes
        /// 세 경로를 각각 대표하는 것으로 고른다.
        /// </summary>
        private static IEnumerable<string> Targets()
        {
            // spawn + hideNodes (모자를 숨기고 새 형태를 세운다)
            yield return "hat_crown";
            yield return "hat_wizard";
            // spawn (악세서리 — 레시피가 형태의 유일한 경로다)
            yield return "acc_wings";
            yield return "acc_halo";
            // spawn (겉옷 자락)
            yield return "outer_wizard";
            // spawn (가방)
            yield return "bag_dragon";
            // bind (도구 — 기존 노드의 mesh를 갈아끼운다. ToolTable은 id 부분일치로 고른다)
            yield return "tool_magnify";
            yield return "tool_net";
            // spawn (얼굴 — 정면으로 돌려야 보이는 자리)
            yield return "acc_glasses";
            yield return "acc_eyepatch";
            // 레시피 없는 색-only (대조군: 변화가 작아야 정상)
            yield return "top_polo";
        }

        private static IEnumerator ProbeItem(string itemId, byte[] baseline)
        {
            CharacterOutfitManager mgr = CharacterOutfitManager.Instance;
            OutfitItem item = mgr != null ? mgr.FindItem(itemId) : null;
            if (item == null)
            {
                Report.AppendLine("| ? | `" + itemId + "` | — | — | **카탈로그에 없음** |");
                failures++;
                yield break;
            }

            OutfitLoadout lo = new OutfitLoadout();
            lo.Set(item.slot, itemId);

            byte[] shot = null;
            yield return Shoot(lo, itemId, v => shot = v);

            int drawn = CountOpaque(shot);
            int diff = baseline != null ? CountDiff(baseline, shot) : -1;

            bool hasRecipe = OutfitShapeLibrary.TryGet(item.slot, itemId, out _);
            // 레시피가 있으면 형태가 바뀌어야 한다. 색만 바뀌는 아이템은 변화가 작아도 정상.
            bool ok = drawn > 0 && (!hasRecipe || diff > 200);
            if (!ok) failures++;

            Report.AppendLine(string.Format("| {0} | `{1}` | {2} | {3} | {4} |",
                item.slot, itemId, drawn, diff < 0 ? "—" : diff.ToString(),
                ok ? "OK" : (hasRecipe ? "**형태 변화 없음**" : "**아무것도 안 그림**")));

            Log(string.Format("{0} ({1}) — 픽셀={2} 변화={3} 레시피={4}",
                itemId, item.slot, drawn, diff, hasRecipe));
        }

        // ── 리그 ──

        private static void BuildRig()
        {
            AppearanceSpec spec = AppearanceSpec.FromPlayerPrefs();

            GameObject go = new GameObject("OutfitProbeMannequin");
            go.SetActive(false);                       // Awake 억제 — PlayerPrefs 외형으로 먼저 지어지지 않게
            go.transform.position = RigOrigin;
            go.AddComponent<PlayerVisualBuilder>().BuildForPreview(spec);
            go.SetActive(true);
            mannequin = go;

            GameObject camGo = new GameObject("OutfitProbeCam");
            camGo.transform.rotation = Quaternion.Euler(6f, 0f, 0f);
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.cullingMask = 1 << ProbeLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f, 1f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 40f;
            cam.enabled = false;                       // 수동 Render만

            GameObject lightGo = new GameObject("OutfitProbeLight");
            lightGo.transform.rotation = Quaternion.Euler(32f, -25f, 0f);
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            l.cullingMask = 1 << ProbeLayer;           // 월드 이중 조명 방지
            l.intensity = 1.15f;
        }

        private static void CleanupRig()
        {
            if (mannequin != null) UnityEngine.Object.Destroy(mannequin);
            if (cam != null) UnityEngine.Object.Destroy(cam.gameObject);
            mannequin = null;
            cam = null;
        }

        /// <summary>의상을 입히고 한 장 찍어 PNG로 남긴다. 픽셀 배열도 돌려줘 비교에 쓴다.</summary>
        private static IEnumerator Shoot(OutfitLoadout loadout, string name, Action<byte[]> onDone)
        {
            CharacterOutfitManager mgr = CharacterOutfitManager.Instance;
            if (mgr != null) mgr.ApplyToCharacter(mannequin, loadout);

            // 회전 0°는 **뒤통수**다(캐릭터가 +Z를 향하고 카메라는 −Z에 있다).
            // 안경·아이패치 같은 얼굴 파츠는 그 각도에서 보이지 않으므로 정면으로 돌린다 —
            // CharacterModelPreviewRenderer가 같은 이유로 FrontYaw를 둔다.
            mannequin.transform.rotation =
                Quaternion.Euler(0f, CharacterModelPreviewRenderer.FrontYaw, 0f);

            // 레시피가 새로 만든 spawn 파츠는 레이어가 0이라 여기서 다시 칠하지 않으면 안 잡힌다
            // (CharacterModelPreviewRenderer가 같은 이유로 같은 일을 한다).
            SetLayerRecursive(mannequin, ProbeLayer);
            yield return null;

            // 마네킹 전체가 프레임에 들어오게
            Bounds b = new Bounds(mannequin.transform.position, Vector3.one);
            bool any = false;
            foreach (Renderer r in mannequin.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }

            float aspect = (float)ShotW / ShotH;
            cam.orthographicSize = Mathf.Max(0.05f,
                Mathf.Max(b.size.y * 0.5f, b.size.x * 0.5f / Mathf.Max(0.01f, aspect)) / 0.82f);
            cam.transform.position = b.center - cam.transform.forward * 8f;

            byte[] pixels = null;
            RenderTexture rt = null;
            RenderTexture prev = RenderTexture.active;
            Texture2D tex = null;
            try
            {
                rt = new RenderTexture(ShotW, ShotH, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;

                tex = new Texture2D(ShotW, ShotH, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0);
                tex.Apply();

                pixels = tex.GetRawTextureData();
                File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
                shots++;
            }
            finally
            {
                RenderTexture.active = prev;
                if (cam != null) cam.targetTexture = null;
                if (rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
                if (tex != null) UnityEngine.Object.Destroy(tex);
            }

            onDone(pixels);
        }

        // ── 픽셀 판정 ──

        /// <summary>배경이 아닌 픽셀 수 — 0이면 마네킹 자체가 안 잡힌 것이다.</summary>
        private static int CountOpaque(byte[] rgb)
        {
            if (rgb == null) return 0;
            int n = 0;
            for (int i = 0; i + 2 < rgb.Length; i += 3)
            {
                // 배경 (0.10,0.12,0.16) ≈ (26,31,41). 여유를 두고 판정한다.
                if (Mathf.Abs(rgb[i] - 26) > 12 || Mathf.Abs(rgb[i + 1] - 31) > 12 || Mathf.Abs(rgb[i + 2] - 41) > 12) n++;
            }
            return n;
        }

        /// <summary>기준 컷과 다른 픽셀 수 — 의상이 실제로 뭔가 바꿨는지 본다.</summary>
        private static int CountDiff(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return -1;
            int n = 0;
            for (int i = 0; i + 2 < a.Length; i += 3)
            {
                if (Mathf.Abs(a[i] - b[i]) > 10 || Mathf.Abs(a[i + 1] - b[i + 1]) > 10 || Mathf.Abs(a[i + 2] - b[i + 2]) > 10) n++;
            }
            return n;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }

        // ── 출력 ──

        private static void ParseArgs()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-outfitOut") outDir = args[i + 1];
        }

        private static void Log(string msg) { Debug.Log("[OUTFIT-PROBE] " + msg); }

        private static void Finish()
        {
            if (shots == 0)
            {
                failures++;
                Report.AppendLine();
                Report.AppendLine("> **찍은 장면이 0건이다 — 프로브가 돌지 않았다.**");
            }

            Report.AppendLine();
            Report.AppendLine(failures == 0
                ? "결과: **PASS** (" + shots + "장)"
                : "결과: **FAIL** (" + failures + "건 / " + shots + "장)");

            try
            {
                Directory.CreateDirectory(Path.GetFullPath(outDir));
                File.WriteAllText(Path.Combine(outDir, "report.md"), Report.ToString(), new UTF8Encoding(false));
                Log("보고서 → " + Path.Combine(outDir, "report.md"));
            }
            catch (Exception e) { Log("보고서 쓰기 실패: " + e.Message); }

            EditorApplication.ExitPlaymode();
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
    }
}
#endif
