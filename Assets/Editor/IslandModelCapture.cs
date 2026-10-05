using System;
using System.Globalization;
using System.IO;
using InsectGame.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace InsectGame.EditorTools
{
    /// <summary>
    /// 섬 지형과 물건 28종을 <b>PlayScene 없이</b> 빈 씬에 세워 찍는 도구. 모양(형태·색 대비·차지 칸 넘침)을 눈으로 본다.
    ///
    /// <code>
    /// Unity.exe -batchmode -projectPath &lt;proj&gt; -logFile &lt;log&gt; \
    ///   -executeMethod InsectGame.EditorTools.IslandModelCapture.Run -islandOut .claude/cache/island
    /// </code>
    ///
    /// 찍는 것:
    /// <list type="bullet">
    ///   <item><c>terrain_L{0,2,3}_game/arrival/aerial/horizon</c> — 섬 크기별 지형. game·arrival은 게임 카메라 구도
    ///     (대상 + (0,9,−6), 대상 + 0.85를 본다), aerial은 섬 전체 조감(안개 끔), horizon은 바다가 하늘로 녹는지.</item>
    ///   <item><c>category_{building,furniture,terrain,tool}</c> — 가장 큰 섬 위에 분류별로 줄지어 놓은 조감.</item>
    ///   <item><c>{id}</c> — 물건 하나를 게임 카메라 각도로 당겨 찍은 것(위에서 무엇으로 읽히는가).
    ///     <c>{id}_front</c>는 정면(+Z) 쪽 낮은 각도 — 게임 카메라는 회전 0의 물건을 뒤에서 보므로 문·날개가 여기서만 보인다.</item>
    /// </list>
    /// 물건 밑의 반투명 흰 판이 차지 칸이다. 넘침은 로그에도 수치로 남는다(<c>[CAPTURE] {id} parts=… spill=…</c>) —
    /// spill은 렌더러 경계 기준이라 돌려 놓은 조각에서는 실제보다 조금 크게 나온다.
    ///
    /// <b>플레이 모드에 들어가지 않는다.</b> 빌더가 정적 함수라 씬·부트스트랩·세이브가 필요 없고, 도메인 리로드를 건너
    /// 상태를 나를 일도 없다. 대신 플레이 모드 밖이라 <c>Object.Destroy</c>가 안 먹는다 — 빌더 쪽이
    /// <c>IslandObjectBuilder.DestroySafe</c>로 가른다.
    ///
    /// 조명·안개는 <c>SubAreaEnvironment</c>의 섬 프로필을 손으로 옮긴 값이다(그쪽을 바꾸면 여기도 바꿀 것).
    /// IMGUI는 안 찍힌다. 배치모드 전용이다 — 열린 에디터에서 돌리면 씬을 묻지 않고 갈아 끼우고 에디터째 닫는다.
    /// 종료 코드: 한 장이라도 찍었으면 0, 한 장도 못 찍었으면 3, 예외는 4.
    /// </summary>
    public static class IslandModelCapture
    {
        private const string DefaultOut = ".claude/cache/island";
        private const int Width = 1280;
        private const int Height = 720;
        private const float Fov = 40f;

        private static readonly Vector3 GameOffset = new Vector3(0f, 9f, -6f);
        private const float GameLookHeight = 0.85f;

        private static string outDir;
        private static Camera camera;
        private static int written;

        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Island model capture requires batch mode to protect the open scene.");

            try
            {
                written = 0;
                outDir = Path.GetFullPath(ProjectPath(Arg("-islandOut") ?? DefaultOut));
                Directory.CreateDirectory(outDir);
                Log("시작 out=" + outDir);

                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                BuildStage();

                IslandMaterialCache materials = new IslandMaterialCache();
                ShootTerrains(materials);
                ShootCatalog(materials);

                Log("완료 written=" + written);
                EditorApplication.Exit(written > 0 ? 0 : 3);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Log("예외로 중단 written=" + written + " — " + e.Message);
                EditorApplication.Exit(4);
            }
        }

        private static void BuildStage()
        {
            Light sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.97f, 0.88f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(52f, 35f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.40f, 0.44f, 0.50f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.fogColor = new Color(0.66f, 0.84f, 0.95f);
            DynamicGI.UpdateEnvironment();

            camera = new GameObject("CaptureCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.56f, 0.80f, 0.96f);
            camera.fieldOfView = Fov;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            camera.enabled = false;   // 자동 렌더 금지 — 장면마다 수동으로 한 번씩만 그린다
        }

        // ── 1. 지형 ──

        private static void ShootTerrains(IslandMaterialCache materials)
        {
            foreach (int level in new[] { 0, 2, 3 })
            {
                GameObject terrain = IslandTerrainBuilder.Build(level, null, materials);
                float half = IslandGrid.HalfExtent(level);
                string prefix = "terrain_L" + level + "_";

                ShotGame(prefix + "game", Vector3.zero);
                // 섬 중심에서는 모래톱·나루터가 화면 밖이다 — 도착 자리에서 한 장 더.
                ShotGame(prefix + "arrival", IslandGrid.ArrivalPoint(level));

                // 조감은 배치 확인용이라 안개를 끈다(80m 위에서는 연무가 40%를 덮는다).
                float h = AerialHeight(half + 9f, half + 9f);
                RenderSettings.fog = false;
                Shot(prefix + "aerial", new Vector3(0f, h, -h * 0.3f), Vector3.zero);
                RenderSettings.fog = true;

                Shot(prefix + "horizon", new Vector3(0f, 5f, -half - 16f), new Vector3(0f, 0.5f, 0f));

                UnityEngine.Object.DestroyImmediate(terrain);
            }
        }

        // ── 2. 물건 ──

        private static void ShootCatalog(IslandMaterialCache materials)
        {
            const int level = GameConstants.Island.MaxSizeLevel;
            IslandTerrainBuilder.Build(level, null, materials);

            int half = IslandGrid.GridSize(level) / 2;
            float cs = GameConstants.Island.CellSize;
            Material pad = materials.GetFade(new Color(1f, 1f, 1f, 0.35f));
            Transform stage = new GameObject("CatalogStage").transform;

            // 북쪽 끝에서부터 분류마다 새 줄로 흘려 놓는다. 물건 사이·줄 사이는 한 칸씩 비운다.
            int left = -half + 1;
            int right = half - 1;
            int rowTop = half - 1;
            int rowDepth = 0;
            int cursor = left;

            foreach (IslandObjectCategory category in Enum.GetValues(typeof(IslandObjectCategory)))
            {
                if (rowDepth > 0)
                {
                    rowTop -= rowDepth + 1;
                    rowDepth = 0;
                    cursor = left;
                }

                bool any = false;
                Bounds area = default;
                foreach (IslandObjectDef def in IslandCatalog.All)
                {
                    if (def.category != category) continue;
                    if (cursor + def.width > right && cursor > left)
                    {
                        rowTop -= rowDepth + 1;
                        rowDepth = 0;
                        cursor = left;
                    }

                    int x = cursor;
                    int z = rowTop - def.depth;
                    cursor += def.width + 1;
                    rowDepth = Mathf.Max(rowDepth, def.depth);
                    if (z < -half) Log("경고: " + def.id + "가 섬 남쪽 밖에 놓였다(z=" + z + ")");

                    Vector3 center = IslandGrid.FootprintCenter(x, z, def.width, def.depth);
                    Transform holder = new GameObject("Placed_" + def.id).transform;
                    holder.SetParent(stage, false);
                    holder.localPosition = center;

                    GameObject model = IslandObjectBuilder.Build(def, holder, materials);
                    AddPad(holder, def.width * cs, def.depth * cs, pad);

                    Bounds foot = new Bounds(center, new Vector3(def.width * cs, 0.1f, def.depth * cs));
                    if (any) area.Encapsulate(foot);
                    else area = foot;
                    any = true;

                    Bounds body = Inspect(def, model, foot);
                    float dist = Mathf.Max(4f, body.extents.magnitude * 2.9f);
                    Shot(def.id, body.center + GameOffset.normalized * dist, body.center);
                    Shot(def.id + "_front", body.center + new Vector3(0.45f, 0.55f, 0.75f).normalized * dist, body.center);
                }

                if (!any) continue;
                float h = AerialHeight(area.extents.x + 1.5f, area.extents.z + 2.5f);
                Shot("category_" + category.ToString().ToLowerInvariant(),
                    area.center + new Vector3(0f, h, -h * 0.45f), area.center);
            }
        }

        /// <summary>
        /// 조각 수·높이·차지 칸 넘침을 로그에 남기고 모델의 경계를 돌려준다.
        /// spill은 차지 칸 밖으로 나간 거리(음수면 그만큼 안쪽). 규칙은 −0.1 이하, 나무 수관만 +0.3까지다.
        /// </summary>
        private static Bounds Inspect(IslandObjectDef def, GameObject model, Bounds foot)
        {
            MeshRenderer[] renderers = model != null ? model.GetComponentsInChildren<MeshRenderer>() : new MeshRenderer[0];
            if (renderers.Length == 0)
            {
                Log("경고: " + def.id + " 렌더러가 없다");
                return foot;
            }

            Bounds body = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) body.Encapsulate(renderers[i].bounds);

            float spill = Mathf.Max(
                Mathf.Max(body.max.x - foot.max.x, foot.min.x - body.min.x),
                Mathf.Max(body.max.z - foot.max.z, foot.min.z - body.min.z));
            string flag = spill > 0.3f ? " OVER" : spill > -0.1f ? " (여유 0.1 미만)" : "";
            string fallback = IslandObjectBuilder.HasModel(def.id) ? "" : " FALLBACK(전용 모델 없음)";
            Log(string.Format(CultureInfo.InvariantCulture, "{0} parts={1} height={2:F2} spill={3:F2}{4}{5}",
                def.id, renderers.Length, body.max.y, spill, flag, fallback));
            return body;
        }

        private static void AddPad(Transform holder, float sizeX, float sizeZ, Material material)
        {
            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            pad.name = "FootprintPad";
            UnityEngine.Object.DestroyImmediate(pad.GetComponent<Collider>());
            pad.transform.SetParent(holder, false);
            pad.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            pad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // Quad는 −Z를 본다 — X로 90° 눕히면 위를 본다
            pad.transform.localScale = new Vector3(sizeX, sizeZ, 1f);
            MeshRenderer renderer = pad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // ── 촬영 ──

        private static void ShotGame(string name, Vector3 target)
        {
            Shot(name, target + GameOffset, target + Vector3.up * GameLookHeight);
        }

        /// <summary>그 넓이가 화면에 다 들어오는 카메라 높이(세로·가로 화각 중 빠듯한 쪽 + 여유 20%).</summary>
        private static float AerialHeight(float halfWidth, float halfDepth)
        {
            float tanV = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * Width / Height;
            return Mathf.Max(halfDepth / tanV, halfWidth / tanH) * 1.2f;
        }

        // ScreenCapture는 배치모드에 게임뷰가 없어 조용히 실패한다 — 카메라 → RenderTexture → ReadPixels로 읽는다
        // (LiveSceneCapture와 같은 경로). 실패는 삼키지 않고 올려 보낸다: 종료 코드 4가 "몇 장 빠졌다"보다 분명하다.
        private static void Shot(string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.position = position;
            camera.transform.LookAt(lookAt);

            RenderTexture rt = null;
            Texture2D tex = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                rt = new RenderTexture(Width, Height, 24);
                camera.targetTexture = rt;
                camera.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();

                string path = Path.Combine(outDir, name + ".png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                written++;
                Log("촬영 → " + path);
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                if (rt != null)
                {
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                }
            }
        }

        // ── CLI ──

        private static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        private static string ProjectPath(string relative)
        {
            return Path.IsPathRooted(relative)
                ? relative
                : Path.Combine(Application.dataPath, "..", relative);
        }

        // 로그 접두사를 고정한다 — 호출부(CLI)가 grep 한 번으로 결과를 읽는다.
        private static void Log(string message) => Debug.Log("[CAPTURE] " + message);
    }
}
