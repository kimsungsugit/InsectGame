#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using InsectGame.Core;
using InsectGame.Story;
using UnityEngine;
using UnityEngine.Video;

namespace InsectGame.UI
{
    /// <summary>
    /// 스토리 영상 촬영 fixture(<c>-battleScenario story-video</c>) — 진짜 <see cref="StoryVideoDirector"/>가 진짜 디코더로
    /// 영상을 틀고, 자막·「건너뛰기」가 실제로 그려진 화면을 찍는다(IMGUI라 배치 캡처에는 안 잡힌다 — <c>rules/testing.md</c> 「한계 셋」).
    ///
    /// 편마다 첫 자막 0.5초 앞 한 장 + 자막마다 그 가운데 한 장. README에 실제로 재생됐는지·첫 프레임까지 걸린 시간·실제 재생 길이를 적는다.
    /// <c>-videoOnly ch6_wall,ch7_fence</c>로 일부만(앞의 <c>vid_</c>는 붙여도 빼도 된다).
    ///
    /// 앞머리에 저널 한 벌(<c>00-journal-*</c>): 본 비트에 「▶ 영상」이 선 화면 → 눌러서 영상이 저널을 덮은 화면 → ESC(PlayerMovement와 같은
    /// <see cref="ModalUIRegistry.HandleEscape"/>)로 영상만 닫히고 저널이 맨 위로 돌아온 화면. 그 판정도 README에 적는다.
    ///
    /// <b>저장을 건드리지 않는다</b> — 영상 지휘자는 AutoWire하지 않는다(스토리 구독·조작 잠금·카메라 없음. Awake가 플레이어·소리를 만들고,
    /// AutoWire의 세 참조는 전부 null 가드라 없어도 <see cref="StoryVideoDirector.PlayReplay"/>가 그대로 돈다).
    /// 저널의 열람 기록은 <b>꺼진</b> 오브젝트의 <see cref="StoryDirector"/>에 메모리로만 넣는다 — Awake(디스크 읽기)·Start(구독·발화)가 돌지 않고
    /// 저장 경로(Save)는 발화·완료에서만 불리는데 그 둘이 없다.
    ///
    /// 종료 코드: 0 전부 재생 · 3 하나라도 재생 실패(라이브러리에 없음·파일 없음·디코더 오류·준비 초과·도중 멈춤) · 5 영상은 됐지만 저널 ESC 판정 실패 ·
    /// 2 찍을 영상이 없음. 전부 재생하면 15편 × 10~15초라 3분을 넘는다 — 검수 하네스의 감시 시계(기본 120초)를 그만큼 늘린다.
    /// </summary>
    public static class StoryVideoVisualCapture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public const string OnlyArg = "-videoOnly";

        /// <summary>
        /// 이야기 순서. 라이브러리 상수(리플렉션)와 Story.json이 부르는 다른 ID는 뒤에 붙는다 — 새 영상을 여기 안 적어도 찍힌다.
        /// 여기 적었는데 라이브러리에 없으면 「라이브러리에 없음」으로 실패한다(아직 안 넣은 영상이 조용히 빠지지 않게).
        /// </summary>
        private static readonly string[] StoryOrder =
        {
            "vid_ch1_prologue", "vid_ch2_watchers", "vid_ch3_scholar", "vid_ch4_crates", "vid_ch5_summit",
            "vid_ch6_wall", "vid_ch7_fence", "vid_ch8_vault", "vid_ch9_archive", "vid_ch10_kiln",
            "vid_ch11_crown", "vid_ch12_ledger", "vid_fin_shadow", "vid_fin_return", "vid_fin_epilogue",
        };

        /// <summary>첫 자막보다 이만큼 앞에서 한 장 — 자막 없는 화면(그림만 + 건너뛰기).</summary>
        private const float PreCueLead = 0.5f;
        /// <summary>준비를 기다리는 상한. 지휘자 자신의 상한(5초)보다 길다 — 지휘자가 먼저 포기하면 그걸 그대로 적는다.</summary>
        private const float PrepareGiveUp = 10f;
        /// <summary>끝을 기다리는 여유. 지휘자의 종료 안전망(저작 길이 + 3초) 너머까지.</summary>
        private const float EndGrace = 6f;
        /// <summary>편 사이 쉼 — 앞 편의 Stop과 다음 편의 Play가 같은 프레임에 붙지 않게.</summary>
        private const float GapSeconds = 0.4f;

        private sealed class Result
        {
            public string Id;
            public bool Known;
            public string File = "-";
            public bool FileExists;
            public float Expected;
            public int CueCount;
            public bool ReplayAccepted;
            public bool Prepared;
            public float PrepareSeconds = -1f;
            public float FirstFrameSeconds = -1f;
            public float PlayedSeconds = -1f;
            public double FileLength = -1d;
            public uint Width, Height;
            public int ShotsPlanned;
            public int ShotsTaken;
            public bool QaTimeout;
            /// <summary>재생기가 공개로 알린 상태가 틀렸다 — 다른 영상 ID를 틀었거나, 다시보기가 대사 앞 영상(콜백 있음)으로 돌았다.</summary>
            public bool WrongState;
            public string Ended = "-";
            public string Note = "";
            public bool Played => Known && Prepared && ShotsPlanned > 0 && ShotsTaken == ShotsPlanned && !QaTimeout && !WrongState;
        }

        private sealed class JournalReport
        {
            public bool Ran;
            public string Skipped = "";
            public string BeatId = "-";
            public string Videos = "-";
            public bool ButtonShown;
            public bool VideoStarted;
            public bool VideoOnTop;
            public bool JournalOpenUnder;
            public bool SecondVideoChained;
            public bool VideoClosed;
            public bool JournalStillOpen;
            public bool JournalBackOnTop;
            public readonly List<string> Shots = new List<string>();
            public bool Passed => Ran && ButtonShown && VideoStarted && VideoOnTop && JournalOpenUnder
                                  && VideoClosed && JournalStillOpen && JournalBackOnTop;
        }

        private struct Shot
        {
            public string Name;
            public float At;
        }

        public static IEnumerator Run(string output, Camera camera)
        {
            var made = new List<GameObject>();
            var results = new List<Result>();
            var journal = new JournalReport();
            var notes = new StringBuilder();

            List<string> selected = Select(notes, out bool filtered);
            if (selected.Count == 0)
            {
                File.WriteAllText(Path.Combine(output, "README.txt"), "No story video selected.\n" + notes.ToString());
                Application.Quit(2);
                yield break;
            }

            // 영상 뒤 월드는 비어 있다 — 카메라는 검은 바탕만. 소리는 실제처럼 들리게 리스너를 세운다(검수 장면엔 없다).
            CameraFollower follower = camera != null ? camera.GetComponent<CameraFollower>() : null;
            if (follower != null) follower.enabled = false;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
            }

            var directorGo = new GameObject("StoryVideoQADirector");
            made.Add(directorGo);
            StoryVideoDirector director = directorGo.AddComponent<StoryVideoDirector>();
            VideoPlayer player = directorGo.GetComponent<VideoPlayer>();

            ExtendWatchdog(selected, notes);

            yield return JournalShots(output, director, player, journal, made);

            for (int i = 0; i < selected.Count; i++)
            {
                var r = new Result { Id = selected[i] };
                results.Add(r);
                yield return PlayOne(output, i, r, director, player);
                Debug.Log("[StoryVideoQA] " + Line(r));
                if (director.IsPlaying) director.Stop();
                yield return Wait(GapSeconds);
            }

            foreach (GameObject go in made)
                if (go != null) UnityEngine.Object.Destroy(go);

            int played = 0;
            foreach (Result r in results) if (r.Played) played++;
            bool videosOk = played == results.Count;
            bool journalOk = !journal.Ran || journal.Passed;
            int exit = !videosOk ? 3 : !journalOk ? 5 : 0;

            WriteReport(output, results, journal, notes, filtered, played, exit);
            Application.Quit(exit);
        }

        // ── 고르기 ──

        private static List<string> Select(StringBuilder notes, out bool filtered)
        {
            List<string> all = CandidateIds();
            string[] args = Environment.GetCommandLineArgs();
            int a = Array.IndexOf(args, OnlyArg);
            filtered = a >= 0 && a + 1 < args.Length;
            if (!filtered) return all;

            var picked = new List<string>();
            foreach (string raw in args[a + 1].Split(','))
            {
                string token = raw.Trim();
                if (token.Length == 0) continue;
                string id = all.Find(x => x == token || x == "vid_" + token);
                if (id == null)
                {
                    // 모르는 ID도 그대로 넣는다 — 「라이브러리에 없음」으로 실패해 오타가 조용히 빠지지 않는다.
                    id = token.StartsWith("vid_", StringComparison.Ordinal) ? token : "vid_" + token;
                    notes.AppendLine($"-videoOnly '{token}' is not a known video id (tried {id}).");
                }
                if (!picked.Contains(id)) picked.Add(id);
            }
            return picked;
        }

        private static List<string> CandidateIds()
        {
            var ids = new List<string>();
            void Add(string id)
            {
                if (!string.IsNullOrWhiteSpace(id) && !ids.Contains(id)) ids.Add(id);
            }
            foreach (string id in StoryOrder) Add(id);
            foreach (FieldInfo f in typeof(StoryVideoLibrary).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (!f.IsLiteral || f.FieldType != typeof(string)) continue;
                string value = f.GetRawConstantValue() as string;
                if (value != null && value.StartsWith("vid_", StringComparison.Ordinal)) Add(value);
            }
            foreach (StoryBeat beat in StoryService.AllBeats())
            {
                if (beat == null) continue;
                Add(beat.introVideoId);
                Add(beat.videoId);
            }
            return ids;
        }

        // ── 한 편 ──

        private static IEnumerator PlayOne(string output, int index, Result r, StoryVideoDirector director, VideoPlayer player)
        {
            if (!StoryVideoLibrary.TryGet(r.Id, out StoryVideoDefinition def) || def == null)
            {
                r.Note = "not in StoryVideoLibrary";
                yield break;
            }
            r.Known = true;
            r.File = def.fileName;
            r.Expected = def.expectedDuration;
            r.CueCount = def.cues != null ? def.cues.Length : 0;
            // 데스크톱 QA 빌드라 파일 유무를 미리 볼 수 있다(지휘자도 같은 경로를 본다).
            r.FileExists = File.Exists(Application.streamingAssetsPath + "/Video/" + def.fileName);

            List<Shot> shots = ShotsFor(def);
            r.ShotsPlanned = shots.Count;
            string prefix = $"{index + 1:D2}-{Short(r.Id)}";

            float t0 = Time.realtimeSinceStartup;
            if (!director.PlayReplay(r.Id))
            {
                // 데스크톱은 지휘자가 파일 유무를 먼저 보고 시작도 않고 끝낸다 — 그때도 false다.
                r.Note = r.FileExists ? "PlayReplay returned false" : "file missing in StreamingAssets/Video (PlayReplay returned false)";
                yield break;
            }
            r.ReplayAccepted = true;
            // 공개 상태로 맞춰 본다 — 다시보기는 그 영상을, 콜백 없이(대사를 열지 않고) 틀어야 한다.
            if (director.CurrentVideoId != r.Id || director.IsPlayingPrelude)
            {
                r.WrongState = true;
                r.Note = $"state: CurrentVideoId={director.CurrentVideoId ?? "null"} IsPlayingPrelude={director.IsPlayingPrelude}. ";
            }

            while (director.IsPlaying && !IsPrepared(director, player) && Time.realtimeSinceStartup - t0 < PrepareGiveUp)
            {
                TrackFirstFrame(r, player, t0);
                yield return null;
            }
            float waited = Time.realtimeSinceStartup - t0;
            if (!director.IsPlaying)
            {
                r.Note += r.FileExists
                    ? $"director stopped before the first frame after {waited:F1}s (decoder error or its own prepare timeout)"
                    : "file missing in StreamingAssets/Video — director skipped it";
                yield break;
            }
            if (!IsPrepared(director, player))
            {
                r.Note += $"QA gave up waiting for prepare after {waited:F1}s";
                director.Stop();
                yield break;
            }
            r.Prepared = true;
            r.PrepareSeconds = waited;
            float playStart = Time.realtimeSinceStartup;
            r.FileLength = player != null ? player.length : -1d;
            if (player != null)
            {
                r.Width = player.width;
                r.Height = player.height;
            }

            for (int s = 0; s < shots.Count; s++)
            {
                while (director.IsPlaying && Clock(director, player) < shots[s].At)
                {
                    TrackFirstFrame(r, player, t0);
                    yield return null;
                }
                if (!director.IsPlaying)
                {
                    r.Note += $"stopped at {Time.realtimeSinceStartup - playStart:F1}s before shot '{shots[s].Name}' (t={shots[s].At:F1}s)";
                    break;
                }
                yield return Capture(output, $"{prefix}-{s}-{shots[s].Name}");
                r.ShotsTaken++;
            }

            float giveUp = Time.realtimeSinceStartup + Mathf.Max(0f, r.Expected - Clock(director, player)) + EndGrace;
            while (director.IsPlaying && Time.realtimeSinceStartup < giveUp)
            {
                TrackFirstFrame(r, player, t0);
                yield return null;
            }
            r.PlayedSeconds = Time.realtimeSinceStartup - playStart;
            if (director.IsPlaying)
            {
                r.QaTimeout = true;
                r.Ended = "QA timeout — the director never stopped";
                director.Stop();
            }
            else r.Ended = ClassifyEnd(r);
        }

        /// <summary>첫 자막 0.5초 앞 + 자막마다 가운데. 자막이 없는 편은 0.5초와 한가운데.</summary>
        private static List<Shot> ShotsFor(StoryVideoDefinition def)
        {
            var shots = new List<Shot>();
            if (def.cues != null && def.cues.Length > 0)
            {
                var cues = new List<StoryVideoCue>(def.cues);
                cues.Sort((x, y) => x.at.CompareTo(y.at));
                shots.Add(new Shot { Name = "pre", At = Mathf.Max(0f, cues[0].at - PreCueLead) });
                for (int i = 0; i < cues.Count; i++)
                    shots.Add(new Shot { Name = "cue" + (i + 1), At = cues[i].at + cues[i].duration * 0.5f });
            }
            else
            {
                shots.Add(new Shot { Name = "start", At = 0.5f });
                shots.Add(new Shot { Name = "mid", At = def.expectedDuration * 0.5f });
            }
            return shots;
        }

        private static string ClassifyEnd(Result r)
        {
            if (r.FileLength <= 0d) return "stopped (file length unknown)";
            if (r.PlayedSeconds < r.FileLength - 0.75d)
                return r.PlayedSeconds >= r.Expected + 2.5f
                    ? "cut by the director's overrun guard (file longer than authored length + 3s)"
                    : "stopped before the end of the file (decoder error?)";
            if (r.PlayedSeconds > r.FileLength + 1.5d) return "ended by the overrun guard (no loopPointReached?)";
            return "end of file";
        }

        // ── 저널 ──

        private static IEnumerator JournalShots(string output, StoryVideoDirector director, VideoPlayer player,
            JournalReport report, List<GameObject> made)
        {
            StoryBeat beat = PickJournalBeat();
            if (beat == null)
            {
                report.Skipped = "no beat in Story.json has a video known to StoryVideoLibrary";
                yield break;
            }
            report.BeatId = beat.beatId;
            StoryJournalVideo.Pair pair = StoryJournalVideo.For(beat, IsKnown);
            report.Videos = pair.Then != null ? pair.First + " → " + pair.Then : pair.First;

            StoryDirector story = MemoryStoryDirector(beat, made, out string why);
            if (story == null)
            {
                report.Skipped = why;
                yield break;
            }

            var journalGo = new GameObject("StoryVideoQAJournal");
            made.Add(journalGo);
            StoryJournalUI ui = journalGo.AddComponent<StoryJournalUI>();
            ui.AutoWire(story, null);   // 대화창 없음 — 「다시 읽기」는 눌러도 아무것도 안 연다
            ui.AutoWire(director);
            ui.OpenForCapture(beat.chapterId, beat.beatId);
            report.Ran = true;

            yield return Wait(0.6f);
            report.ButtonShown = ui.ShowsVideoButtonForCapture(beat.beatId);
            yield return Capture(output, "00-journal-1-video-button");
            report.Shots.Add("00-journal-1-video-button");

            // 「▶ 영상」과 같은 길로 튼다.
            report.VideoStarted = ui.PressVideoForCapture(beat.beatId)
                                  && director.CurrentVideoId == pair.First && !director.IsPlayingPrelude;
            if (report.VideoStarted)
            {
                yield return WaitForClock(director, player, 1.2f, 8f);
                report.VideoOnTop = ReferenceEquals(ModalUIRegistry.TopModal, director);
                report.JournalOpenUnder = ui.IsOpen;
                yield return Capture(output, "00-journal-2-video-over");
                report.Shots.Add("00-journal-2-video-over");

                // ESC — PlayerMovement가 부르는 것과 같은 한 줄. 최상위 모달 하나만 닫는다.
                ModalUIRegistry.HandleEscape();
                yield return null;   // 저널 Update가 이어 틀 뒤 영상을 집는 프레임
                yield return null;
                if (director.IsPlaying)
                {
                    // 대사 앞·뒤 영상이 둘 다 있는 비트 — 앞 영상을 건너뛰면 뒤 영상이 이어진다.
                    // 지금 데이터에선 안 생긴다(story_lint 검사 13이 한 비트의 두 영상을 금지) — 저널의 방어 경로를 남겨 둔 만큼 여기도 둔다.
                    report.SecondVideoChained = true;
                    yield return WaitForClock(director, player, 1.2f, 8f);
                    yield return Capture(output, "00-journal-3-second-video");
                    report.Shots.Add("00-journal-3-second-video");
                    ModalUIRegistry.HandleEscape();
                    yield return null;
                }
                yield return Wait(0.4f);
                report.VideoClosed = !director.IsPlaying && !ui.HasQueuedVideoForCapture;
                report.JournalStillOpen = ui.IsOpen;
                report.JournalBackOnTop = ReferenceEquals(ModalUIRegistry.TopModal, ui);
                yield return Capture(output, "00-journal-4-after-esc");
                report.Shots.Add("00-journal-4-after-esc");
            }

            ui.CloseModal();
            if (director.IsPlaying) director.Stop();
            UnityEngine.Object.Destroy(journalGo);
            yield return Wait(GapSeconds);
        }

        /// <summary>저널에 세울 비트 — 대사 앞·뒤 영상이 둘 다 있는 것 → 앞 영상 → 뒤 영상 순으로, 그 안에서는 이야기 앞쪽.</summary>
        private static StoryBeat PickJournalBeat()
        {
            StoryBeat best = null;
            int bestKind = int.MaxValue, bestChapter = int.MaxValue, bestOrder = int.MaxValue;
            foreach (StoryBeat beat in StoryService.AllBeats())
            {
                if (beat == null || string.IsNullOrEmpty(beat.beatId)) continue;
                StoryJournalVideo.Pair pair = StoryJournalVideo.For(beat, IsKnown);
                if (!pair.HasAny) continue;
                bool intro = !string.IsNullOrWhiteSpace(beat.introVideoId) && IsKnown(beat.introVideoId);
                int kind = pair.Then != null ? 0 : intro ? 1 : 2;
                int chapter = ChapterRank(beat.chapterId);
                bool better = kind < bestKind
                              || kind == bestKind && chapter < bestChapter
                              || kind == bestKind && chapter == bestChapter && beat.order < bestOrder
                              || kind == bestKind && chapter == bestChapter && beat.order == bestOrder
                                 && string.CompareOrdinal(beat.beatId, best.beatId) < 0;
                if (!better) continue;
                best = beat;
                bestKind = kind;
                bestChapter = chapter;
                bestOrder = beat.order;
            }
            return best;
        }

        private static int ChapterRank(string chapterId)
        {
            if (string.IsNullOrEmpty(chapterId)) return 999;
            if (chapterId == "fin") return 100;
            if (chapterId.StartsWith("ch", StringComparison.Ordinal)
                && int.TryParse(chapterId.Substring(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) return n;
            return 500;
        }

        /// <summary>
        /// 꺼진 오브젝트에 세운 이야기 지휘자 — 그 장에서 이 비트까지(선택지 결과 제외)를 본 것으로 메모리에만 적는다.
        /// 꺼져 있어 Awake(디스크 읽기)·Start(구독·즉시 발화)·Update가 돌지 않는다.
        /// </summary>
        private static StoryDirector MemoryStoryDirector(StoryBeat target, List<GameObject> made, out string why)
        {
            why = "";
            var go = new GameObject("StoryVideoQAStory");
            made.Add(go);
            go.SetActive(false);
            StoryDirector story = go.AddComponent<StoryDirector>();
            FieldInfo field = typeof(StoryDirector).GetField("progress", Private);
            if (field == null || field.FieldType != typeof(StoryProgressData))
            {
                why = "StoryDirector.progress field not found — journal shot skipped";
                return null;
            }
            var progress = new StoryProgressData { activeChapterId = target.chapterId };
            foreach (StoryBeat beat in StoryService.AllBeats())
            {
                if (beat == null || string.IsNullOrEmpty(beat.beatId) || beat.chapterId != target.chapterId) continue;
                if (beat.beatId != target.beatId && (beat.order > target.order || story.IsChoiceTarget(beat.beatId))) continue;
                progress.seenBeatIds.Add(beat.beatId);
            }
            if (!progress.seenBeatIds.Contains(target.beatId)) progress.seenBeatIds.Add(target.beatId);
            try
            {
                field.SetValue(story, progress);
            }
            catch (Exception e)
            {
                why = "could not inject seen beats: " + e.Message;
                return null;
            }
            return story;
        }

        private static bool IsKnown(string videoId) => StoryVideoLibrary.TryGet(videoId, out _);

        // ── 재생 상태 읽기 ──
        // 어떤 영상인지·대사 앞 영상인지는 공개 값(CurrentVideoId·IsPlayingPrelude)으로 본다. 준비 완료(prepared)와 자막 시계(elapsed)는
        // 공개 값이 없어 안쪽 필드를 읽는다 — 대사 앞 영상이 들어온 뒤(2026-10-05)에도 같은 이름·형이다. 이름이 바뀌면 VideoPlayer로 물러선다.

        private static readonly FieldInfo PreparedField = typeof(StoryVideoDirector).GetField("prepared", Private);
        private static readonly FieldInfo ElapsedField = typeof(StoryVideoDirector).GetField("elapsed", Private);

        private static bool IsPrepared(StoryVideoDirector director, VideoPlayer player)
        {
            if (PreparedField != null && PreparedField.FieldType == typeof(bool)) return (bool)PreparedField.GetValue(director);
            return player != null && player.isPrepared && player.isPlaying;
        }

        /// <summary>자막을 고르는 시계 — 지휘자의 <c>elapsed</c>(자막이 이걸 본다). 없으면 영상 시계.</summary>
        private static float Clock(StoryVideoDirector director, VideoPlayer player)
        {
            if (ElapsedField != null && ElapsedField.FieldType == typeof(float)) return (float)ElapsedField.GetValue(director);
            return player != null ? (float)player.time : 0f;
        }

        private static void TrackFirstFrame(Result r, VideoPlayer player, float t0)
        {
            if (r.FirstFrameSeconds >= 0f || player == null) return;
            if (player.isPlaying && player.frame >= 0) r.FirstFrameSeconds = Time.realtimeSinceStartup - t0;
        }

        private static IEnumerator WaitForClock(StoryVideoDirector director, VideoPlayer player, float clock, float giveUpSeconds)
        {
            float giveUp = Time.realtimeSinceStartup + giveUpSeconds;
            while (director.IsPlaying && Time.realtimeSinceStartup < giveUp
                   && (!IsPrepared(director, player) || Clock(director, player) < clock))
                yield return null;
        }

        /// <summary>
        /// 검수 하네스의 감시 시계(<c>BattleVisualCapture.watchdogSeconds</c>, 기본 120초)를 이 장면 길이만큼 늘린다.
        /// 못 찾으면 README에 적는다 — 그때는 120초를 넘는 실행이 종료 코드 4로 끊긴다(<c>-videoOnly</c>로 나눠 찍을 것).
        /// </summary>
        private static void ExtendWatchdog(List<string> selected, StringBuilder notes)
        {
            float budget = 60f;   // 저널 한 벌 + 여유
            foreach (string id in selected)
            {
                float expected = StoryVideoLibrary.TryGet(id, out StoryVideoDefinition def) && def != null ? def.expectedDuration : 0f;
                budget += expected + 5f + 3f + EndGrace + GapSeconds;
            }
            var harness = UnityEngine.Object.FindFirstObjectByType<InsectGame.Battle.BattleVisualCapture>();
            FieldInfo field = typeof(InsectGame.Battle.BattleVisualCapture).GetField("watchdogSeconds", Private);
            if (harness == null || field == null || field.FieldType != typeof(float))
            {
                notes.AppendLine("Watchdog NOT extended (harness field not found) — runs over 120s end with exit 4; split with -videoOnly.");
                return;
            }
            // 감시는 (지금 − 시작) > 한도로 끊는다. 시작 시각 ≥ 0이라 '지금 + 예산'이면 이 장면이 끝날 때까지 안전하다.
            float need = Time.realtimeSinceStartup + budget;
            if ((float)field.GetValue(harness) < need) field.SetValue(harness, need);
            notes.AppendLine($"Watchdog extended to {need:F0}s for {selected.Count} video(s).");
        }

        // ── 보고 ──

        private static string Short(string id) => id != null && id.StartsWith("vid_", StringComparison.Ordinal) ? id.Substring(4) : id;

        private static string Secs(float s) => s < 0f ? "-" : s.ToString("F2", CultureInfo.InvariantCulture) + "s";

        private static string Line(Result r)
        {
            string length = r.FileLength > 0d ? r.FileLength.ToString("F2", CultureInfo.InvariantCulture) + "s" : "-";
            string drift = r.FileLength > 0d && Math.Abs(r.FileLength - r.Expected) > 0.5d ? "  [file length differs from authored]" : "";
            return $"{(r.Played ? "PLAYED " : "FAILED ")} {r.Id,-20} file={r.File} exists={(r.FileExists ? "yes" : "no")}" +
                   $"  prepare={Secs(r.PrepareSeconds)} firstFrame={Secs(r.FirstFrameSeconds)}" +
                   $"  played={Secs(r.PlayedSeconds)} (file {length}, authored {r.Expected.ToString("F1", CultureInfo.InvariantCulture)}s){drift}" +
                   $"  size={(r.Width > 0 ? r.Width + "x" + r.Height : "-")}  cues={r.CueCount} shots={r.ShotsTaken}/{r.ShotsPlanned}" +
                   $"  ended={r.Ended}" + (string.IsNullOrEmpty(r.Note) ? "" : "  note=" + r.Note);
        }

        private static void WriteReport(string output, List<Result> results, JournalReport journal, StringBuilder notes,
            bool filtered, int played, int exit)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Story videos: {played}/{results.Count} played for real (exit {exit}).");
            sb.AppendLine($"Actual standalone VideoPlayer + IMGUI (subtitles, 「건너뛰기」) via StoryVideoDirector.PlayReplay. " +
                          $"{Screen.width}x{Screen.height} (mobile layout={UIScale.IsMobileLayout}, portrait={UIScale.IsPortrait}).");
            sb.AppendLine(filtered ? "Filter: " + OnlyArg + " (subset only)." : "Filter: none (every known video).");
            sb.AppendLine("Shots per video: NN-<id>-0-pre (first cue - 0.5s, no subtitle) and NN-<id>-k-cueK (middle of each cue).");
            sb.AppendLine("prepare = PlayReplay → the director starts drawing the video (prepareCompleted). firstFrame = PlayReplay → VideoPlayer.frame >= 0 while playing.");
            sb.AppendLine("played = first drawn frame → the director closed the video (wall clock). file = VideoPlayer.length from the decoder; authored = StoryVideoLibrary.expectedDuration.");
            sb.AppendLine("In-memory fixture: the video director is not AutoWired (no story subscription, freeze or camera); nothing is saved.");
            sb.Append(notes.ToString());
            sb.AppendLine();
            foreach (Result r in results) sb.AppendLine(Line(r));
            sb.AppendLine();
            if (!journal.Ran)
            {
                sb.AppendLine("Journal: skipped — " + journal.Skipped);
            }
            else
            {
                sb.AppendLine($"Journal ▶ 영상 / ESC: {(journal.Passed ? "PASS" : "FAIL")}  beat={journal.BeatId}  videos={journal.Videos}");
                sb.AppendLine($"  button shown on a seen row={journal.ButtonShown}  video started={journal.VideoStarted}" +
                              $"  video on top of the modal stack={journal.VideoOnTop}  journal still open under it={journal.JournalOpenUnder}");
                sb.AppendLine($"  after ESC: video closed={journal.VideoClosed}  journal still open={journal.JournalStillOpen}" +
                              $"  journal back on top={journal.JournalBackOnTop}  second video chained={journal.SecondVideoChained}");
                sb.AppendLine("  seen beats: that chapter up to this beat (choice outcomes excluded), injected into an inactive StoryDirector.");
                sb.AppendLine("  shots: " + string.Join(", ", journal.Shots));
            }
            File.WriteAllText(Path.Combine(output, "README.txt"), sb.ToString());
        }

        // ── 공용 ──

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private static IEnumerator Capture(string output, string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), shot.EncodeToPNG());
            UnityEngine.Object.Destroy(shot);
        }
    }
}
#endif
