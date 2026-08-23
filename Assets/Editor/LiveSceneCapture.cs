#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace InsectGame.EditorTools
{
    /// <summary>
    /// 배치모드에서 <b>실제 게임 화면을 렌더해 PNG로 남기는</b> 도구. 3D 변경(모델·애니메이션·
    /// 지형·연출)을 코드만 보고 판단하지 않고 눈으로 확인하기 위한 것이다.
    ///
    /// <code>
    /// Unity.exe -batchmode -projectPath &lt;proj&gt; -logFile &lt;log&gt; \
    ///   -executeMethod InsectGame.EditorTools.LiveSceneCapture.Run \
    ///   -captureOut .claude/cache/capture -captureTimes 1.5,3.5,5.5 \
    ///   -captureSize 900x700 -captureOffset 1.5,1.15,1.9 -captureLook 0,0.8,0
    /// </code>
    ///
    /// <b>알아야 할 한계 — IMGUI는 안 찍힌다.</b> <c>OnGUI</c>는 카메라를 거치지 않고 화면 위에
    /// 직접 그리므로 여기서 잡히지 않는다(상점·대화창·배틀 UI·HUD). 그건 스탠드얼론 빌드로
    /// <c>ScreenCapture</c>를 써야 한다. 이 도구가 덮는 것은 <b>월드에 있는 것</b>이다.
    ///
    /// 구현상 함정 셋 — 전부 실측으로 확인했다:
    /// <list type="number">
    ///   <item><c>[UnityTest]</c> 코루틴 테스트로는 못 한다. asmdef가 없어 테스트가
    ///     <c>Assembly-CSharp</c>로 컴파일되는데 거기엔 <c>UnityEngine.TestTools</c> 참조가 없다.</item>
    ///   <item><c>ScreenCapture.CaptureScreenshot</c>은 배치모드에 게임뷰가 없어 <b>파일을 만들지 않는다</b>
    ///     (조용히 실패한다). 카메라를 <c>RenderTexture</c>로 직접 렌더해 픽셀을 읽어야 한다 —
    ///     배치모드에도 그래픽 디바이스는 있다(Direct3D11 확인).</item>
    ///   <item>플레이모드 진입은 도메인 리로드를 일으켜 정적 상태를 날린다. <c>SessionState</c>로
    ///     단계를 넘기고 <c>[InitializeOnLoadMethod]</c>가 이어받는다.</item>
    /// </list>
    ///
    /// <b>읽을 때 헷갈리는 것 둘.</b>
    /// <list type="bullet">
    ///   <item>월드 이름표(곤충 Lv. 등)가 <b>거울처럼 뒤집혀</b> 보일 수 있다. 버그가 아니다 —
    ///     이름표는 <c>Camera.main</c>을 향하는 빌보드인데(<c>InsectEntity</c>가 매 프레임 회전을 물린다)
    ///     이 도구는 별도 카메라로 다른 각도에서 찍으므로 뒷면을 보게 된다.</item>
    ///   <item>각도에 따라 캐릭터가 <b>새까만 실루엣</b>으로 나온다. 역광일 뿐이다 —
    ///     형태가 아니라 색·질감을 봐야 한다면 빛을 등지지 않는 오프셋을 골라야 한다
    ///     (기본값 <c>0,1.2,-2.6</c>은 밝게 나오는 각도다).</item>
    /// </list>
    /// </summary>
    public static class LiveSceneCapture
    {
        private const string StageKey = "InsectGame.LiveSceneCapture.Stage";
        private const string DefaultScene = "Assets/Scenes/PlayScene.unity";
        private const string DefaultOut = ".claude/cache/capture";
        /// <summary>플레이모드에 못 들어가거나 촬영이 끝나지 않을 때의 탈출구(초).</summary>
        private const float HardTimeoutSeconds = 180f;

        private static Settings settings;
        private static float startTime;
        private static int nextShot;
        private static int written;

        private struct Settings
        {
            public string scene;
            public string outDir;
            public float[] times;
            public int width, height;
            public Vector3 offset;   // 대상 기준 카메라 위치
            public Vector3 look;     // 대상 기준 시선 지점
            public string target;    // 따라갈 GameObject 이름(비면 Camera.main 구도 그대로)

            /// <summary>
            /// 촬영 전에 플레이어를 옮겨 둘 리전 ID(비면 이동 없음).
            ///
            /// 초원 밖의 것을 찍으려면 이게 필요하다 — 플레이어는 늘 초원에서 시작하고,
            /// 리전에 실제로 들어가야만 그 리전에 붙은 것들(수문장 봉인·오염 거점 구조물 등)이
            /// 지어진다. 좌표만 옮기면 <c>RegionManager.Update</c>가 리전 변경을 알아서 잡는다.
            /// </summary>
            public string region;

            /// <summary>리전으로 옮기는 시각(초). 부트스트랩이 끝난 뒤여야 한다.</summary>
            public float regionAt;

            /// <summary>
            /// 현재 리전의 오염 거점을 정화하는 시각(초). 음수면 하지 않는다.
            ///
            /// 오염과 정화를 <b>한 번의 실행에서</b> 찍기 위한 것이다 — 전/후를 따로 돌리면
            /// 조명·시각·플레이어 위치가 미묘하게 달라 비교가 흐려진다.
            /// 실제 승리 경로와 같은 <c>CleanseByBoss</c>를 부른다(디버그 전용 뒷문 금지).
            /// </summary>
            public float cleanseAt;

            /// <summary>촬영 전에 정화 기록을 지울 것인가.</summary>
            public bool resetBlight;
        }

        [MenuItem("InsectGame/Live Scene Capture")]
        public static void Run()
        {
            settings = ReadSettings();
            Directory.CreateDirectory(Path.GetFullPath(ProjectPath(settings.outDir)));


            // 빈 씬으로 들어가면 카메라조차 없다 — 실제 게임 씬을 연 뒤 플레이모드로 간다.
            // PlaySceneBootstrap이 카메라·플레이어·NPC·곤충을 전부 코드로 짓는다.
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                settings.scene, UnityEditor.SceneManagement.OpenSceneMode.Single);

            SessionState.SetString(StageKey, Serialize(settings));
            Log($"scene={settings.scene} times=[{string.Join(",", settings.times)}] " +
                $"size={settings.width}x{settings.height} out={settings.outDir}");
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            string stage = SessionState.GetString(StageKey, "");
            if (string.IsNullOrEmpty(stage)) return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return;

            settings = Deserialize(stage);
            startTime = Time.realtimeSinceStartup;
            nextShot = 0;
            written = 0;
            regionMoved = false;
            cleansed = false;
            EditorApplication.update += Tick;
            Log("플레이모드 진입 — 촬영 대기");
        }

        private static bool regionMoved;
        private static bool cleansed;

        /// <summary>
        /// 정화 기록을 지운다. 정화는 세이브에 남으므로 안 지우면 <b>두 번째 실행부터 거점이
        /// 아예 안 선다</b>(실제로 그렇게 빈 화면을 찍었다). 촬영은 반복 가능해야 비교가 된다.
        /// </summary>
        private static void ResetBlightRecord()
        {
            string key = InsectGame.Core.SaveScope.PrefsKey(
                InsectGame.Core.GameConstants.PrefsKeys.BlightCleansed);
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            var blight = UnityEngine.Object.FindFirstObjectByType<InsectGame.Core.RegionBlightManager>();
            if (blight != null) blight.ReloadFromDisk();   // 인메모리 캐시도 되돌린다
            Log($"정화 기록 초기화 ({key})");
        }

        /// <summary>현재 리전의 거점을 무너뜨린다 — 승리 경로와 같은 함수를 쓴다.</summary>
        private static void CleanseCurrentRegion()
        {
            var rm = UnityEngine.Object.FindFirstObjectByType<InsectGame.Core.RegionManager>();
            var blight = UnityEngine.Object.FindFirstObjectByType<InsectGame.Core.RegionBlightManager>();
            if (rm == null || blight == null || rm.CurrentRegion == null)
            {
                Log("정화 실패 — RegionManager/RegionBlightManager/현재 리전 없음");
                return;
            }
            InsectGame.Data.RegionData here = rm.CurrentRegion;
            bool ok = blight.CleanseByBoss(here.blightBossNpcId, here.regionId);
            Log($"정화 {(ok ? "성공" : "실패")} — {here.regionId}");
        }

        /// <summary>
        /// 플레이어를 리전 중심으로 옮긴다. 좌표만 바꾸면 <c>RegionManager.Update</c>가
        /// 리전 변경을 잡고, 그에 붙은 구독자(스포너·스토리·오염 거점 비주얼)가 따라 움직인다.
        ///
        /// y는 지형 높이를 모르니 조금 띄워 두고 중력에 맡긴다 — 파묻히면 아무것도 안 보인다.
        /// </summary>
        private static void MoveToRegion(string regionId)
        {
            var rm = UnityEngine.Object.FindFirstObjectByType<InsectGame.Core.RegionManager>();
            GameObject player = GameObject.Find("Player");
            if (rm == null || player == null)
            {
                Log($"리전 이동 실패 — RegionManager={(rm != null)} Player={(player != null)}");
                return;
            }
            InsectGame.Data.RegionData r = rm.GetRegionById(regionId);
            if (r == null)
            {
                Log($"리전 '{regionId}'를 못 찾았다");
                return;
            }
            // **지형 높이를 찾아 붙인다.** 그냥 현재 y로 옮기면 리전마다 지면 높이가 달라
            // 파묻히거나(카메라가 땅속에서 갈색 화면만 찍는다 — 실제로 겪었다) 공중에 뜬다.
            Vector3 p = r.centerPosition;
            if (Physics.Raycast(new Vector3(p.x, 300f, p.z), Vector3.down, out RaycastHit hit, 600f))
            {
                p.y = hit.point.y + 1.2f;
            }
            else
            {
                p.y = player.transform.position.y + 5f;
                Log($"지면을 못 찾았다 — 현재 높이 +5로 둔다 ({regionId})");
            }
            player.transform.position = p;
            Log($"리전 이동 → {regionId} {p}");
        }

        private static void Tick()
        {
            float elapsed = Time.realtimeSinceStartup - startTime;

            if (!regionMoved && !string.IsNullOrEmpty(settings.region) && elapsed >= settings.regionAt)
            {
                regionMoved = true;
                // **플레이 모드에서** 지운다. 에디터 모드에는 AuthManager가 없어 전역 키가
                // 잡히는데 실행 중에는 계정 스코프 키를 쓴다 — 엉뚱한 키를 지우고 "초기화했다"고
                // 로그만 남긴 채 거점이 안 서는 일을 실제로 겪었다.
                if (settings.resetBlight) ResetBlightRecord();
                MoveToRegion(settings.region);
            }

            if (!cleansed && settings.cleanseAt >= 0f && elapsed >= settings.cleanseAt)
            {
                cleansed = true;
                CleanseCurrentRegion();
            }

            if (nextShot < settings.times.Length && elapsed >= settings.times[nextShot])
            {
                string path = Path.GetFullPath(ProjectPath(Path.Combine(
                    settings.outDir, $"shot_{settings.times[nextShot].ToString("0.0", CultureInfo.InvariantCulture)}s.png")));
                if (Capture(path)) written++;
                nextShot++;
            }

            bool done = nextShot >= settings.times.Length;
            if (!done && elapsed < HardTimeoutSeconds) return;

            EditorApplication.update -= Tick;
            SessionState.SetString(StageKey, "");
            Log($"완료 written={written}/{settings.times.Length} elapsed={elapsed:F1}s");
            EditorApplication.Exit(written == settings.times.Length ? 0 : 3);
        }

        /// <summary>
        /// 한 장 촬영. <b>게임 카메라를 옮기지 않는다</b> — 전용 카메라를 새로 만들어 설정만 복사한다.
        /// <c>Camera.main</c>을 직접 옮기면 <c>CameraFollower</c>와 다투고, 월드 좌표 이름표
        /// (<c>InsectEntity</c>가 매 프레임 카메라 회전을 물린다)가 한 프레임 늦어 거울처럼 뒤집혀 찍힌다.
        /// </summary>
        private static bool Capture(string path)
        {
            Camera source = Camera.main;
            if (source == null) source = UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (source == null)
            {
                Log("카메라를 찾지 못했다 — 씬이 아직 안 지어졌는가?");
                return false;
            }

            var rigGo = new GameObject("~LiveSceneCaptureRig");
            var rig = rigGo.AddComponent<Camera>();
            rig.CopyFrom(source);
            rig.enabled = false;   // 자동 렌더 금지 — 아래에서 수동으로 한 번만 그린다

            if (string.IsNullOrEmpty(settings.target))
            {
                rig.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            }
            else
            {
                GameObject go = GameObject.Find(settings.target);
                if (go == null)
                {
                    Log($"대상 '{settings.target}'을 못 찾아 게임 카메라 구도를 그대로 쓴다");
                    rig.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                }
                else
                {
                    Vector3 p = go.transform.position;
                    rig.transform.position = p + settings.offset;
                    rig.transform.LookAt(p + settings.look);
                }
            }

            RenderTexture rt = null;
            Texture2D tex = null;
            RenderTexture prevActive = RenderTexture.active;
            try
            {
                rt = new RenderTexture(settings.width, settings.height, 24);
                rig.targetTexture = rt;
                rig.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(settings.width, settings.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, settings.width, settings.height), 0, 0);
                tex.Apply();

                File.WriteAllBytes(path, tex.EncodeToPNG());
                Log($"촬영 → {path}");
                return true;
            }
            catch (Exception e)
            {
                Log($"촬영 실패: {e.Message}");
                return false;
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (rig != null) rig.targetTexture = null;
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                UnityEngine.Object.DestroyImmediate(rigGo);
            }
        }

        // ── CLI 인자 ──

        private static Settings ReadSettings()
        {
            var s = new Settings
            {
                scene = Arg("-captureScene") ?? DefaultScene,
                outDir = Arg("-captureOut") ?? DefaultOut,
                times = Floats(Arg("-captureTimes"), new[] { 2f }),
                offset = Vec(Arg("-captureOffset"), new Vector3(0f, 1.2f, -2.6f)),
                look = Vec(Arg("-captureLook"), new Vector3(0f, 0.85f, 0f)),
                target = Arg("-captureTarget") ?? "Player",
                region = Arg("-captureRegion") ?? "",
                regionAt = Floats(Arg("-captureRegionAt"), new[] { 2f })[0],
                cleanseAt = Floats(Arg("-captureCleanseAt"), new[] { -1f })[0],
                resetBlight = Arg("-captureResetBlight") != null,
            };
            Size(Arg("-captureSize"), out s.width, out s.height);
            // "-captureTarget none"이면 게임 카메라 구도를 그대로 쓴다.
            if (s.target == "none") s.target = "";
            return s;
        }

        private static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        private static float[] Floats(string raw, float[] fallback)
        {
            if (string.IsNullOrEmpty(raw)) return fallback;
            var list = new List<float>();
            foreach (string part in raw.Split(','))
                if (float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                    list.Add(v);
            return list.Count > 0 ? list.ToArray() : fallback;
        }

        private static Vector3 Vec(string raw, Vector3 fallback)
        {
            float[] v = Floats(raw, null);
            return v != null && v.Length == 3 ? new Vector3(v[0], v[1], v[2]) : fallback;
        }

        private static void Size(string raw, out int w, out int h)
        {
            w = 900;
            h = 700;
            if (string.IsNullOrEmpty(raw)) return;
            string[] parts = raw.Split('x', 'X');
            if (parts.Length == 2
                && int.TryParse(parts[0], out int pw) && int.TryParse(parts[1], out int ph)
                && pw > 0 && ph > 0)
            {
                w = pw;
                h = ph;
            }
        }

        // 도메인 리로드를 건너 설정을 나른다 — SessionState는 문자열만 담는다.
        private static string Serialize(Settings s)
        {
            return string.Join("|",
                s.scene, s.outDir, string.Join(",", s.times),
                s.width.ToString(), s.height.ToString(),
                V(s.offset), V(s.look), s.target ?? "",
                s.region ?? "", s.regionAt.ToString(CultureInfo.InvariantCulture),
                s.cleanseAt.ToString(CultureInfo.InvariantCulture),
                s.resetBlight ? "1" : "0");
        }

        private static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture,
            "{0},{1},{2}", v.x, v.y, v.z);

        private static Settings Deserialize(string raw)
        {
            string[] p = raw.Split('|');
            return new Settings
            {
                scene = p[0],
                outDir = p[1],
                times = Floats(p[2], new[] { 2f }),
                width = int.Parse(p[3]),
                height = int.Parse(p[4]),
                offset = Vec(p[5], Vector3.zero),
                look = Vec(p[6], Vector3.zero),
                target = p.Length > 7 ? p[7] : "Player",
                region = p.Length > 8 ? p[8] : "",
                regionAt = p.Length > 9 ? Floats(p[9], new[] { 2f })[0] : 2f,
                cleanseAt = p.Length > 10 ? Floats(p[10], new[] { -1f })[0] : -1f,
                resetBlight = p.Length > 11 && p[11] == "1",
            };
        }

        private static string ProjectPath(string relative)
        {
            return Path.IsPathRooted(relative)
                ? relative
                : Path.Combine(Application.dataPath, "..", relative);
        }

        // 로그 접두사를 고정한다 — 호출부(CLI)가 grep 한 번으로 결과를 읽는다.
        private static void Log(string message) => Debug.Log($"[CAPTURE] {message}");
    }
}
#endif
