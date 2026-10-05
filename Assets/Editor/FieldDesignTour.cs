#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.NPC;
using UnityEditor;
using UnityEngine;

namespace InsectGame.EditorTools
{
    /// <summary>
    /// 필드 디자인 순회 캡처 — 실제 PlayScene을 띄워 <b>한 번의 실행으로</b> 본 마을·리전 13곳·
    /// 전초기지·서브에리어 전부·NPC 전원을 찍는다. 테마(건물·지형지물·NPC 옷) 개선의 전/후 기준이다.
    ///
    /// <code>
    /// Unity.exe -batchmode -projectPath &lt;proj&gt; -logFile &lt;log&gt; \
    ///   -executeMethod InsectGame.EditorTools.FieldDesignTour.Run \
    ///   -tourOut .claude/cache/tour [-tourOnly village,regions,outposts,subareas,npcs,population] [-tourFilter dunes,canopy]
    ///   (population은 사진이 아니라 판정이다 — 리전을 떠났다 돌아와도 같은 곤충인지. 보고서에 줄이 남는다)
    /// </code>
    ///
    /// 장소마다 세 구도를 남긴다 — <c>game</c>(실제 게임 카메라 그대로, 플레이어가 보는 것),
    /// <c>wide</c>(고각 조감 — 배치·밀도), <c>eye</c>(눈높이 — 건물·지형지물의 실루엣).
    /// 게임 카메라는 (0,9,-6) 고각이라 <b>세로로 선 것의 형태가 거의 안 보인다</b> — eye가 그걸 메운다.
    ///
    /// <b>NPC는 제자리에서 찍지 않는다.</b> 컬링(<c>DistanceCulling</c>)이 렌더러를 끄고 주변 소품이 가린다.
    /// 같은 프레임 안에서 무대(먼 좌표의 바닥판)로 옮겨 찍고 되돌린다 — Update가 끼지 않으니 배회가 안 섞인다.
    /// 실제 스폰 경로(<c>NpcManager</c>)가 만든 개체를 찍으므로 외형 규칙이 바뀌면 그대로 따라온다.
    ///
    /// IMGUI는 안 찍힌다(<c>LiveSceneCapture</c>와 같은 한계). 에디터를 열어 두면 프로젝트 잠금으로 못 돈다.
    ///
    /// <b>배치모드 전용이다</b>(<c>ModelDesignCapture</c>·<c>VillageDesignCapture</c>와 같은 가드). 열린 에디터에서 돌리면
    /// 저장 안 된 씬을 묻지도 않고 PlayScene으로 갈아 끼우고, 끝나면 <c>EditorApplication.Exit</c>로 에디터째 닫는다.
    /// 그래서 메뉴 항목도 두지 않는다.
    ///
    /// 종료 코드는 실패(<see cref="Fail"/>)가 한 건도 없을 때만 0이고, 있으면 3이다(<c>LiveSceneCapture</c> 관례).
    /// 예전엔 실패가 보고서 줄로만 쌓이고 늘 0으로 끝나 "다 찍었다"로 읽혔다.
    /// </summary>
    public static class FieldDesignTour
    {
        private const string StageKey = "InsectGame.FieldDesignTour.Stage";
        private const string Scene = "Assets/Scenes/PlayScene.unity";
        private const float BootWait = 7f;
        private const float HardTimeoutSeconds = 900f;
        private static readonly Vector3 NpcStage = new Vector3(4000f, 0f, 4000f);

        private struct Step
        {
            public string label;
            public Action act;
            public float wait;
            public Action shoot;
        }

        private static string outDir;
        private static HashSet<string> only;
        private static HashSet<string> filter;
        private static List<Step> steps;
        private static int index;
        private static bool acted;
        private static float startTime;
        private static float stepStart;
        private static int written;
        private static int failures;
        private static readonly StringBuilder report = new StringBuilder();

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Field design tour requires batch mode to protect the open scene.");
            string o = Arg("-tourOut") ?? ".claude/cache/tour";
            string stage = string.Join("|", o, Arg("-tourOnly") ?? "", Arg("-tourFilter") ?? "");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                Scene, UnityEditor.SceneManagement.OpenSceneMode.Single);
            SessionState.SetString(StageKey, stage);
            Log($"시작 {stage}");
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            string stage = SessionState.GetString(StageKey, "");
            if (string.IsNullOrEmpty(stage)) return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return;

            string[] p = stage.Split('|');
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", p[0]));
            only = Set(p.Length > 1 ? p[1] : "");
            filter = Set(p.Length > 2 ? p[2] : "");
            Directory.CreateDirectory(outDir);
            steps = null;
            index = 0;
            acted = false;
            written = 0;
            failures = 0;
            report.Length = 0;
            startTime = Time.realtimeSinceStartup;
            EditorApplication.update += Tick;
            Log("플레이모드 진입 — 부트스트랩 대기");
        }

        private static HashSet<string> Set(string raw)
        {
            var s = new HashSet<string>();
            foreach (string part in raw.Split(','))
                if (!string.IsNullOrWhiteSpace(part)) s.Add(part.Trim());
            return s;
        }

        private static bool Wants(string group) => only.Count == 0 || only.Contains(group);
        private static bool Passes(string regionId) => filter.Count == 0 || filter.Contains(regionId);

        /// <summary>
        /// 시계·날씨를 정오·맑음으로 붙잡는다. 게임 시계는 새벽 6시에 시작해 돌고(하루 12분) 날씨도 스스로 바뀌므로, 3분짜리 투어 370장이
        /// 서로 다른 조명으로 찍힌다 — 전후 비교는 같은 조건이어야 한다. <c>-tourHour 0~24</c>·<c>-tourWeather clear|rain|fog|wind|snow</c>로
        /// 다른 조건을 고를 수 있다(<c>natural</c>이면 게임 그대로).
        /// </summary>
        private static void PinSky()
        {
            string hourArg = Arg("-tourHour");
            string weatherArg = Arg("-tourWeather");
            var provider = UnityEngine.Object.FindFirstObjectByType<InsectGame.Core.WorldStateProvider>();
            if (provider == null)
            {
                Log("시각·날씨 공급자를 못 찾아 하늘을 고정하지 못했다");
                return;
            }

            if (!string.Equals(hourArg, "natural", StringComparison.OrdinalIgnoreCase) && provider.Clock != null)
            {
                float hour = 12f;
                if (!string.IsNullOrEmpty(hourArg)) float.TryParse(hourArg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out hour);
                provider.Clock.SetTime01(hour / 24f, true);
            }

            if (!string.Equals(weatherArg, "natural", StringComparison.OrdinalIgnoreCase) && provider.Weather != null)
            {
                InsectGame.Core.WeatherType weather = InsectGame.Core.WeatherType.Clear;
                switch ((weatherArg ?? "").Trim().ToLowerInvariant())
                {
                    case "rain": weather = InsectGame.Core.WeatherType.Rain; break;
                    case "fog": weather = InsectGame.Core.WeatherType.Fog; break;
                    case "wind": weather = InsectGame.Core.WeatherType.Wind; break;
                    case "snow": weather = InsectGame.Core.WeatherType.Snow; break;
                }
                provider.Weather.SetWeather(weather, true, true);
            }
            Log($"하늘 고정 — hour={hourArg ?? "12"} weather={weatherArg ?? "clear"}");
        }

        private static void Tick()
        {
            float now = Time.realtimeSinceStartup;
            float elapsed = now - startTime;
            if (elapsed > HardTimeoutSeconds)
            {
                Finish($"시간 초과 {elapsed:F0}s — {index}/{steps?.Count ?? 0}단계에서 멈춤", 3);
                return;
            }
            if (elapsed < BootWait) return;

            try
            {
                if (steps == null)
                {
                    PinSky();
                    steps = BuildSteps();
                    Log($"단계 {steps.Count}개");
                }
                if (index >= steps.Count)
                {
                    Finish($"완료 written={written} failures={failures} elapsed={elapsed:F0}s", failures == 0 ? 0 : 3);
                    return;
                }

                Step s = steps[index];
                if (!acted)
                {
                    acted = true;
                    stepStart = now;
                    s.act?.Invoke();
                    return;
                }
                if (now - stepStart < s.wait) return;
                s.shoot?.Invoke();
                index++;
                acted = false;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish($"예외로 중단 — {e.Message}", 3);
            }
        }

        private static void Finish(string message, int code)
        {
            EditorApplication.update -= Tick;
            SessionState.SetString(StageKey, "");
            report.AppendLine(message);   // 보고서만 봐도 실패 수·종료 사유가 보이게
            try { File.WriteAllText(Path.Combine(outDir, "tour-report.txt"), report.ToString()); }
            catch (Exception e) { Log($"보고서 기록 실패: {e.Message}"); }
            Log(message);
            EditorApplication.Exit(code);
        }

        // ── 단계 구성 ──

        private static List<Step> BuildSteps()
        {
            var list = new List<Step>();
            var rm = UnityEngine.Object.FindFirstObjectByType<RegionManager>();
            if (rm == null || rm.Regions == null) throw new InvalidOperationException("RegionManager 없음");
            RegionData[] regions = rm.Regions;

            RegionData meadow = null;
            foreach (RegionData r in regions) if (r != null && r.regionId == "meadow") meadow = r;

            if (Wants("sky")) AuditSky();

            if (Wants("world"))
            {
                Vector3 o = Vector3.zero;
                foreach (RegionData r in regions) if (r != null) o += r.centerPosition;
                o /= regions.Length;
                list.Add(new Step
                {
                    label = "world",
                    act = null,
                    wait = 0.1f,
                    shoot = () =>
                    {
                        Shot("world_s", o + new Vector3(0f, 230f, -420f), o, 1200, 760);
                        Shot("world_n", o + new Vector3(0f, 230f, 420f), o, 1200, 760);
                        Shot("world_e", o + new Vector3(420f, 230f, 0f), o, 1200, 760);
                        Shot("world_w", o + new Vector3(-420f, 230f, 0f), o, 1200, 760);
                        // 필드 안에서 지평선 쪽을 보는 구도 — 원경 산맥·구름이 실제로 어떻게 걸리는가
                        foreach (RegionData r in regions)
                        {
                            if (r == null || (r.regionId != "meadow" && r.regionId != "frostline" && r.regionId != "dunes")) continue;
                            Vector3 away = (r.centerPosition - o).normalized;
                            Vector3 eye = Ground(r.centerPosition + away * r.radius * 0.5f) + Vector3.up * 2.2f;
                            Shot($"horizon_{r.regionId}", eye, eye + away * 100f + Vector3.up * 14f, 1000, 560);
                        }
                    },
                });
            }

            if (Wants("village") && meadow != null && Passes("meadow"))
            {
                Vector3 v = VillageBuilder.GetMainVillageCenter(meadow.centerPosition, meadow.radius);
                list.Add(new Step
                {
                    label = "village",
                    act = () => MovePlayer(v + new Vector3(0f, 0f, -4f)),
                    wait = 1.6f,
                    shoot = () =>
                    {
                        GameShot("village_game");
                        Vector3 g = Ground(v);
                        Shot("village_wide_s", g + new Vector3(0f, 16f, -26f), g);
                        Shot("village_wide_n", g + new Vector3(0f, 16f, 26f), g);
                        Shot("village_wide_e", g + new Vector3(26f, 16f, 0f), g);
                        Shot("village_wide_w", g + new Vector3(-26f, 16f, 0f), g);
                        Shot("village_eye", g + new Vector3(-9f, 2.2f, -13f), g + new Vector3(0f, 2.5f, 0f));
                    },
                });
            }

            if (Wants("regions"))
            {
                foreach (RegionData r in regions)
                {
                    if (r == null || !Passes(r.regionId)) continue;
                    RegionData region = r;
                    list.Add(new Step
                    {
                        label = "region " + region.regionId,
                        act = () => MovePlayer(region.centerPosition),
                        wait = 1.6f,
                        shoot = () =>
                        {
                            GameShot($"region_{region.regionId}_game");
                            Vector3 c = Ground(region.centerPosition);
                            float R = region.radius;
                            Shot($"region_{region.regionId}_wide", c + new Vector3(0f, 0.85f * R, -1.1f * R), c);
                            Shot($"region_{region.regionId}_wide2", c + new Vector3(1.0f * R, 0.7f * R, 0.6f * R), c);
                            Vector3 edge = Ground(c + new Vector3(-0.55f * R, 0f, -0.55f * R));
                            Shot($"region_{region.regionId}_eye", edge + Vector3.up * 3f, c + Vector3.up * 2.5f);
                        },
                    });
                }
            }

            if (Wants("outposts"))
            {
                foreach (RegionData r in regions)
                {
                    if (r == null || r.regionId == "meadow" || !Passes(r.regionId)) continue;
                    string id = r.regionId;
                    list.Add(new Step
                    {
                        label = "outpost " + id,
                        act = () =>
                        {
                            GameObject op = GameObject.Find("Outpost_" + id);
                            if (op != null) MovePlayer(op.transform.TransformPoint(new Vector3(0f, 0f, 6f)));
                            else Fail($"outpost {id}: 오브젝트 없음");
                        },
                        wait = 1.3f,
                        shoot = () =>
                        {
                            GameObject op = GameObject.Find("Outpost_" + id);
                            if (op == null) return;
                            Transform t = op.transform;
                            GameShot($"outpost_{id}_game");
                            Vector3 focus = t.position + Vector3.up * 1.4f;
                            Shot($"outpost_{id}_wide", t.TransformPoint(new Vector3(4f, 10f, 15f)), focus);
                            Shot($"outpost_{id}_eye", t.TransformPoint(new Vector3(-5.5f, 1.7f, 9.5f)), focus);
                        },
                    });
                }
            }

            if (Wants("subareas"))
            {
                foreach (RegionData r in regions)
                {
                    if (r == null || r.subAreas == null || !Passes(r.regionId)) continue;
                    foreach (SubAreaData s in r.subAreas)
                    {
                        if (s == null) continue;
                        SubAreaData sub = s;
                        // 진입 요청이 나갔는가 — 서브에리어마다 새 변수라 enter 단계의 act·shoot 람다가 이 서브에리어 것만 공유한다
                        bool requested = false;
                        list.Add(new Step
                        {
                            label = "near " + sub.subAreaId,
                            act = () => MovePlayer(sub.centerPosition + new Vector3(0f, 0f, -3f)),
                            wait = 0.9f,
                            // 필드 쪽 입구 — 무엇이 여기가 입구라고 알려 주는가
                            shoot = () =>
                            {
                                GameShot($"gate_{sub.subAreaId}_game");
                                Vector3 g = Ground(sub.centerPosition);
                                Shot($"gate_{sub.subAreaId}_eye", g + new Vector3(-4f, 2.4f, -9f), g + Vector3.up * 1.2f);
                            },
                        });
                        list.Add(new Step
                        {
                            label = "enter " + sub.subAreaId,
                            act = () =>
                            {
                                requested = false;
                                var m = UnityEngine.Object.FindFirstObjectByType<RegionManager>();
                                if (m == null || m.NearbySubArea == null || m.NearbySubArea.subAreaId != sub.subAreaId)
                                {
                                    Fail($"sub {sub.subAreaId}: 근접 판정 실패 (nearby={m?.NearbySubArea?.subAreaId ?? "없음"})");
                                    return;
                                }
                                m.RequestEnterSubArea();
                                requested = true;
                            },
                            wait = 2.2f,
                            shoot = () =>
                            {
                                // **진입이 안 됐으면 찍지 않는다.** 찍으면 메인 필드 사진이 sub_* 이름으로 저장돼
                                // 전후 비교에서 "서브에리어가 필드처럼 생겼다"로 읽힌다(실패 원인이 사진 뒤로 숨는다).
                                if (!requested) return;   // 근접 실패는 act에서 이미 셌다
                                var m = UnityEngine.Object.FindFirstObjectByType<RegionManager>();
                                string cur = m?.CurrentSubArea?.subAreaId ?? "없음";
                                if (cur != sub.subAreaId)
                                {
                                    Fail($"sub {sub.subAreaId}: 진입 요청 후에도 current={cur} — 촬영 건너뜀");
                                    return;
                                }
                                Note($"sub {sub.subAreaId}: env={sub.environmentType} current={cur}");
                                GameShot($"sub_{sub.subAreaId}_game");
                                GameObject root = GameObject.Find("SubArea_" + sub.subAreaId);
                                Vector3 o = root != null ? root.transform.position : new Vector3(2000f, 0f, 2000f);
                                Shot($"sub_{sub.subAreaId}_wide", o + new Vector3(0f, 17f, -21f), o);
                                GameObject player = GameObject.Find("Player");
                                Vector3 pp = player != null ? player.transform.position : o;
                                Shot($"sub_{sub.subAreaId}_eye", pp + new Vector3(0f, 1.9f, -3f), o + new Vector3(0f, 1.6f, 3f));
                            },
                        });
                        list.Add(new Step
                        {
                            label = "exit " + sub.subAreaId,
                            act = () =>
                            {
                                var b = UnityEngine.Object.FindFirstObjectByType<SubAreaWorldBuilder>();
                                if (b != null) b.RequestExit();
                                else UnityEngine.Object.FindFirstObjectByType<RegionManager>()?.ForceExitSubArea();
                            },
                            wait = 1.0f,
                        });
                    }
                }
            }

            if (Wants("npcs"))
            {
                list.Add(new Step { label = "npcs", act = null, wait = 0.2f, shoot = ShootNpcs });
            }
            if (Wants("population") && meadow != null) AddPopulationSteps(list, regions, meadow);
            return list;
        }

        // ── 필드 개체군 영속 — 리전을 떠났다 돌아와도 같은 곤충인가 ──
        //
        // 스포너는 개체를 슬롯 기록으로 들고 플레이어 45m 안에서만 몸을 세운다(InsectSpawner 주석). 옛 스포너는 리전을
        // 옮길 때마다 새로 굴려서, 돌아오면 다른 곤충이었다. 초원 한 자리 → 가장 먼 리전 → 같은 자리로 돌아와 종·레벨·자리가
        // 같은지 본다. 곤충은 제자리에서 조금씩 배회하므로 자리는 4m 안이면 같다고 친다. 첫 채움의 수명(1~7분)이 자리를
        // 비운 몇 초 사이에 끝나 교체되는 슬롯이 드물게 있어, 80% 이상 같으면 통과다.
        private static List<(string id, int level, Vector3 pos)> populationBefore;

        private static void AddPopulationSteps(List<Step> list, RegionData[] regions, RegionData meadow)
        {
            RegionData far = null;
            float farSq = -1f;
            foreach (RegionData r in regions)
            {
                if (r == null) continue;
                float d = (r.centerPosition - meadow.centerPosition).sqrMagnitude;
                if (d > farSq) { farSq = d; far = r; }
            }
            Vector3 home = meadow.centerPosition + new Vector3(0f, 0f, -meadow.radius * 0.45f);
            Vector3 away = far.centerPosition;
            list.Add(new Step
            {
                label = "population_home",
                act = () => MovePlayer(home),
                wait = 2.5f,
                shoot = () =>
                {
                    populationBefore = Nearby(home);
                    report.AppendLine($"개체군 초원 첫 방문: 45m 안 {populationBefore.Count}마리 — {Describe(populationBefore)}");
                    if (populationBefore.Count == 0) Fail("개체군: 초원에 곤충이 하나도 안 섰다");
                },
            });
            list.Add(new Step
            {
                label = "population_away",
                act = () => MovePlayer(away),
                wait = 2.5f,
                shoot = () =>
                {
                    var there = Nearby(away);
                    report.AppendLine($"개체군 {far.regionId}: 45m 안 {there.Count}마리");
                    foreach (var e in Nearby(home))
                        Fail($"개체군: 초원을 떠났는데 몸이 남아 있다 {e.id}");
                },
            });
            list.Add(new Step
            {
                label = "population_return",
                act = () => MovePlayer(home),
                wait = 2.5f,
                shoot = () =>
                {
                    var after = Nearby(home);
                    var pool = new List<(string id, int level, Vector3 pos)>(after);
                    int same = 0;
                    foreach (var b in populationBefore)
                    {
                        int hit = pool.FindIndex(a => a.id == b.id && a.level == b.level
                            && (new Vector2(a.pos.x - b.pos.x, a.pos.z - b.pos.z)).sqrMagnitude < 16f);
                        if (hit < 0) continue;
                        same++;
                        pool.RemoveAt(hit);
                    }
                    int total = Mathf.Max(1, populationBefore.Count);
                    report.AppendLine($"개체군 초원 재방문: 45m 안 {after.Count}마리, 첫 방문과 같은 개체 {same}/{populationBefore.Count}");
                    if (same < total * 0.8f)
                        Fail($"개체군: 돌아오니 다른 곤충이다 — 같은 개체 {same}/{populationBefore.Count}");
                },
            });
        }

        private static List<(string id, int level, Vector3 pos)> Nearby(Vector3 at)
        {
            var list = new List<(string id, int level, Vector3 pos)>();
            var spawner = UnityEngine.Object.FindFirstObjectByType<InsectGame.Spawning.InsectSpawner>();
            if (spawner == null) { Fail("InsectSpawner 없음"); return list; }
            foreach (var e in spawner.ActiveInsects)
            {
                if (e == null || e.Data == null || !e.gameObject.activeInHierarchy) continue;
                Vector3 p = e.transform.position;
                if (new Vector2(p.x - at.x, p.z - at.z).sqrMagnitude > 45f * 45f) continue;
                list.Add((e.Data.insectId, e.Level, p));
            }
            return list;
        }

        private static string Describe(List<(string id, int level, Vector3 pos)> list)
        {
            var sb = new StringBuilder();
            foreach (var e in list) sb.Append(e.id).Append(" Lv").Append(e.level).Append(", ");
            return sb.ToString();
        }

        /// <summary>
        /// 공중(바닥 8m 위)에 떠 있는 렌더러를 루트별로 센다. 눈높이 샷의 하늘에 정체불명의 원반이
        /// 찍혀서 만든 진단이다 — 서브에리어(2000,·,2000)와 NPC 무대는 뺀다.
        /// </summary>
        private static void AuditSky()
        {
            var counts = new Dictionary<string, int>();
            var sample = new Dictionary<string, string>();
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                Bounds b = r.bounds;
                if (b.min.y < 8f || b.center.x > 1500f) continue;
                string root = r.transform.root.name;
                string key = root + "/" + r.gameObject.name.Split('_')[0];
                counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
                if (!sample.ContainsKey(key))
                    sample[key] = $"{r.gameObject.name} c={b.center} size={b.size}";
            }
            foreach (var kv in counts) Note($"sky {kv.Key} x{kv.Value} — {sample[kv.Key]}");
            if (counts.Count == 0) Note("sky: 8m 위 렌더러 없음");

            // 지면 위로 솟은 거대 덩어리(폭 14m↑·높이 0.8m↑). 리전 한가운데를 덮는 돔을 찾으려고 둔다.
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                Bounds b = r.bounds;
                if (b.center.x > 1500f || b.max.y < 0.8f) continue;
                if (Mathf.Max(b.size.x, b.size.z) < 14f || b.size.y > 30f) continue;
                if (r.gameObject.name.StartsWith("Region_") || r.gameObject.name == "Ground") continue;
                Note($"big {r.gameObject.name} c={b.center} size={b.size} top={b.max.y:F2}");
            }
        }

        // ── 동작 ──

        private static void MovePlayer(Vector3 target)
        {
            GameObject player = GameObject.Find("Player");
            if (player == null) { Fail("Player 없음"); return; }
            Vector3 p = Ground(target) + Vector3.up * 1.2f;
            player.transform.position = p;
            player.GetComponent<PlayerMovement>()?.StopNavigation();
            Physics.SyncTransforms();
        }

        private static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, 300f, p.z), Vector3.down, out RaycastHit hit, 600f,
                    ~0, QueryTriggerInteraction.Ignore))
                return new Vector3(p.x, hit.point.y, p.z);
            return new Vector3(p.x, 0f, p.z);
        }

        /// <summary>
        /// NPC 전원을 무대로 옮겨 한 명씩 찍는다. 같은 호출 안에서 되돌리므로 게임 상태가 안 바뀐다.
        /// 렌더러는 컬링이 꺼 뒀을 수 있어 강제로 켜고 원래 값으로 복구한다.
        /// </summary>
        private static void ShootNpcs()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "~TourNpcStage";
            floor.transform.position = NpcStage;
            floor.transform.localScale = Vector3.one * 2f;
            // SceneryMaterials.Create로 바꾸지 않는다 — 무광 마감이 붙어 무대 바닥의 광택(Standard 기본 0.5)이 바뀌면
            // 이전 순회의 NPC 컷과 전후 비교가 어긋난다. 에디터 전용이라 Standard가 늘 있어 폴백도 필요 없다.
            var floorMat = new Material(Shader.Find("Standard")) { color = new Color(0.46f, 0.50f, 0.44f) };
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            var npcs = new List<Transform>();
            foreach (VillagerNpc v in UnityEngine.Object.FindObjectsByType<VillagerNpc>(FindObjectsSortMode.None))
                npcs.Add(v.transform);
            foreach (CatcherKidNpc k in UnityEngine.Object.FindObjectsByType<CatcherKidNpc>(FindObjectsSortMode.None))
                npcs.Add(k.transform);
            npcs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            foreach (Transform t in npcs)
            {
                Vector3 pos = t.position;
                Quaternion rot = t.rotation;
                Renderer[] rs = t.GetComponentsInChildren<Renderer>(true);
                bool[] was = new bool[rs.Length];
                for (int i = 0; i < rs.Length; i++) { was[i] = rs[i].enabled; rs[i].enabled = true; }
                try
                {
                    t.SetPositionAndRotation(NpcStage, Quaternion.identity);
                    Vector3 look = NpcStage + Vector3.up * 0.78f * t.localScale.y;
                    float dist = 2.4f * Mathf.Max(0.75f, t.localScale.y);
                    Shot("npc_" + t.name.Replace("Npc_", ""), look + new Vector3(0.9f, 0.35f, dist), look, 360, 440, 40f);
                    Shot("npcback_" + t.name.Replace("Npc_", ""), look + new Vector3(-1.2f, 0.45f, -dist), look, 360, 440, 40f);
                }
                finally
                {
                    t.SetPositionAndRotation(pos, rot);
                    for (int i = 0; i < rs.Length; i++) rs[i].enabled = was[i];
                }
            }
            Note($"npcs: {npcs.Count}명");
            UnityEngine.Object.DestroyImmediate(floor);
            UnityEngine.Object.DestroyImmediate(floorMat);
        }

        // ── 촬영 ──

        private static void GameShot(string name)
        {
            Camera src = Camera.main;
            if (src == null) { Fail(name + ": Camera.main 없음"); return; }
            Render(name, src, src.transform.position, src.transform.rotation, 800, 560, -1f);
        }

        private static void Shot(string name, Vector3 pos, Vector3 look, int w = 800, int h = 560, float fov = -1f)
        {
            Camera src = Camera.main;
            if (src == null) { Fail(name + ": Camera.main 없음"); return; }
            Render(name, src, pos, Quaternion.LookRotation(look - pos), w, h, fov);
        }

        private static void Render(string name, Camera src, Vector3 pos, Quaternion rot, int w, int h, float fov)
        {
            var rigGo = new GameObject("~FieldTourRig");
            var rig = rigGo.AddComponent<Camera>();
            rig.CopyFrom(src);
            rig.enabled = false;
            rig.farClipPlane = Mathf.Max(src.farClipPlane, 600f);
            if (fov > 0f) rig.fieldOfView = fov;
            rig.transform.SetPositionAndRotation(pos, rot);

            RenderTexture rt = null;
            Texture2D tex = null;
            RenderTexture prev = RenderTexture.active;
            try
            {
                rt = new RenderTexture(w, h, 24);
                rig.targetTexture = rt;
                rig.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
                written++;
            }
            catch (Exception e)
            {
                Fail($"{name}: 촬영 실패 {e.Message}");
            }
            finally
            {
                RenderTexture.active = prev;
                rig.targetTexture = null;
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                UnityEngine.Object.DestroyImmediate(rigGo);
            }
        }

        private static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        private static void Note(string line)
        {
            report.AppendLine(line);
            Log(line);
        }

        /// <summary>
        /// 실패 한 건 — 보고서에 "FAIL"로 남기고 종료 코드를 3으로 만든다. 정보성 줄(sky·npcs 수 등)은 <see cref="Note"/>.
        /// 실패를 Note로만 적으면 수백 장 중 몇 장이 빠졌는지 로그를 다 읽기 전엔 모른다.
        /// </summary>
        private static void Fail(string line)
        {
            failures++;
            Note("FAIL " + line);
        }

        private static void Log(string message) =>
            Debug.Log("[TOUR] " + message.ToString(CultureInfo.InvariantCulture));
    }
}
#endif
