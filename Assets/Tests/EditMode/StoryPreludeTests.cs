#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using InsectGame.Story;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 대사 앞 영상(<c>StoryBeat.introVideoId</c>)과 연출 고리(<see cref="StoryPreludeChain"/>), 저널 다시보기(<c>PlayReplay</c>).
    ///
    /// 급소 하나: <b>대사 앞 연출이 콜백을 못 부르면 그 비트가 <c>pendingBeatId</c>에 갇혀 캠페인이 멈추고, 두 번 부르면 대사가
    /// 두 번 열린다.</b> 그래서 종료 경로마다 "정확히 한 번"을 센다 — 끝·건너뛰기·ESC·디코더 오류·준비 시간 초과·재생 초과·
    /// 비활성·재진입, 그리고 시작도 못 한 경우(파일 없음·모르는 ID·꺼짐)는 "한 번도 안 부르고 false"(호출부가 대사를 띄운다).
    ///
    /// 영상 지휘자는 <b>디코더를 열지 않는다</b>(<c>DecoderEnabled = false</c> — 배치·PlayMode 러너엔 디코더가 없다, rules/testing.md).
    /// 재생 상태(모달·콜백)만 세우고, 디코더 이벤트(끝·오류)와 시간(준비·초과)은 private 진입점을 직접 두드린다.
    /// LogAssert는 이 어셈블리에서 참조가 안 된다(asmdef 없음) — 일부러 던지는 테스트는 로거를 잠깐 끈다.
    /// </summary>
    [TestFixture]
    public class StoryPreludeTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
                if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        // ── 영상 지휘자: 대사 앞 영상의 종료 경로 ─────────────────────────────

        [Test]
        public void Prelude_SkipButton_CallsDoneExactlyOnce()
        {
            StoryVideoDirector video = NewVideo();
            int done = 0;
            Assert.IsTrue(video.TryPlayPrelude(IntroBeat(StoryVideoLibrary.Ch6Wall), () => done++), "영상이 있으면 게이트를 건다");
            Assert.IsTrue(video.IsPlayingPrelude);
            Assert.AreEqual(0, done, "영상이 끝나기 전에는 대사를 열지 않는다");

            video.Stop();   // 「건너뛰기」 버튼이 부르는 그것
            Assert.AreEqual(1, done);
            video.Stop();
            video.CloseModal();
            Assert.AreEqual(1, done, "두 번 닫아도 대사는 한 번만 열린다");
            Assert.IsFalse(video.IsPlaying);
        }

        [Test]
        public void Prelude_Escape_ClosesVideoAndOpensDialogueOnce()
        {
            Assume.That(!ModalUIRegistry.IsAnyOpen(), "다른 테스트가 모달을 남겼다");
            StoryVideoDirector video = NewVideo();
            int done = 0;
            Assert.IsTrue(video.TryPlayPrelude(IntroBeat(StoryVideoLibrary.Ch7Fence), () => done++));
            Assert.AreSame(video, ModalUIRegistry.TopModal, "재생 중엔 영상이 모달 맨 위다 — ESC·뒤로가기가 영상을 닫는다");

            Assert.IsTrue(ModalUIRegistry.HandleEscape());
            Assert.AreEqual(1, done);
            Assert.IsFalse(ModalUIRegistry.IsAnyOpen(), "영상이 모달을 남기지 않는다");
        }

        [Test]
        public void Prelude_NormalEnd_DecoderError_PrepareTimeout_Overrun_EachCallDoneOnce()
        {
            StoryVideoDirector video = NewVideo();

            int ended = 0;
            Assert.IsTrue(video.PlayIntro(Def("qa_end.mp4"), () => ended++));
            Invoke(video, "OnReachedEnd", new object[] { null });   // loopPointReached
            Assert.AreEqual(1, ended, "끝까지 보면 대사가 열린다");

            int errored = 0;
            Assert.IsTrue(video.PlayIntro(Def("qa_error.mp4"), () => errored++));
            Invoke(video, "OnVideoError", null, "qa decoder");      // errorReceived — Android의 파일 없음도 이 길로 온다
            Assert.AreEqual(1, errored, "디코더가 실패해도 대사는 연다");

            int prepare = 0;
            Assert.IsTrue(video.PlayIntro(Def("qa_prepare.mp4"), () => prepare++));
            Invoke(video, "TickPlayback", 4.9f);
            Assert.AreEqual(0, prepare, "준비 시간 안에는 기다린다");
            Invoke(video, "TickPlayback", 0.2f);
            Assert.AreEqual(1, prepare, "디코더가 끝내 준비되지 않으면 시간 초과로 대사를 연다");

            int overrun = 0;
            StoryVideoDefinition longDef = Def("qa_overrun.mp4");
            Assert.IsTrue(video.PlayIntro(longDef, () => overrun++));
            SetField(video, "prepared", true);                       // 첫 프레임이 떴다고 친다
            Invoke(video, "TickPlayback", longDef.expectedDuration + 2.9f);
            Assert.AreEqual(0, overrun);
            Invoke(video, "TickPlayback", 0.2f);
            Assert.AreEqual(1, overrun, "종료 이벤트가 안 오는 기기에서도 길이 + 여유 뒤에 대사를 연다");

            Assert.IsFalse(video.IsPlaying);
            // 앞 경로의 콜백이 뒤 경로(같은 지휘자의 다음 영상)에서 다시 불리지 않는다.
            Assert.AreEqual(1, ended);
            Assert.AreEqual(1, errored);
            Assert.AreEqual(1, prepare);
        }

        [Test]
        public void Prelude_DirectorDisabled_OpensDialogueOnce()
        {
            StoryVideoDirector video = NewVideo();
            int done = 0;
            Assert.IsTrue(video.TryPlayPrelude(IntroBeat(StoryVideoLibrary.FinShadow), () => done++));

            video.gameObject.SetActive(false);   // OnDisable — 꺼진 채 기다리면 시간 초과가 영영 안 온다
            Assert.AreEqual(1, done);
            video.gameObject.SetActive(true);
            Assert.AreEqual(1, done, "다시 켜도 또 부르지 않는다");
            Assert.IsFalse(video.IsPlaying);
        }

        [Test]
        public void Prelude_ReentrantPlay_FinishesTheFirstOnceThenTheSecondOnce()
        {
            StoryVideoDirector video = NewVideo();
            int first = 0, second = 0;
            Assert.IsTrue(video.PlayIntro(Def("qa_first.mp4"), () => first++));
            Assert.IsTrue(video.PlayIntro(Def("qa_second.mp4"), () => second++));
            Assert.AreEqual(1, first, "재진입은 앞 영상의 종료 경로다 — 앞 비트의 대사를 연다");
            Assert.AreEqual(0, second, "새 콜백을 재진입 Stop이 불러 버리면 안 된다(재생이 시작된 뒤에 건다)");

            video.Stop();
            Assert.AreEqual(1, first);
            Assert.AreEqual(1, second);
        }

        [Test]
        public void Prelude_PlainPlayOverAPrelude_StillOpensItsDialogue()
        {
            // 대사 뒤 영상(Play)이 어떤 이유로든 대사 앞 영상을 끊어도 그 비트의 대사는 열린다.
            StoryVideoDirector video = NewVideo();
            int done = 0;
            Assert.IsTrue(video.PlayIntro(Def("qa_intro.mp4"), () => done++));
            video.Play(Def("qa_outro.mp4"));
            Assert.AreEqual(1, done);
            Assert.IsTrue(video.IsPlaying);
            Assert.IsFalse(video.IsPlayingPrelude, "새 영상에는 콜백이 없다");
            video.Stop();
            Assert.AreEqual(1, done);
        }

        [Test]
        public void Prelude_MissingFile_ReturnsFalseWithoutCallback()
        {
            Assume.That(!ModalUIRegistry.IsAnyOpen(), "다른 테스트가 모달을 남겼다");
            StoryVideoDirector video = NewVideo();
            video.DecoderEnabled = true;   // 파일 확인까지는 진짜로 간다 — 파일이 없어 디코더는 열리지 않는다
            int done = 0;
            int warned = CountWarnings("[StoryVideo]", () =>
                Assert.IsFalse(video.PlayIntro(Def("qa_missing_intro_video.mp4"), () => done++),
                    "시작도 못 했다 — false면 호출부가 곧바로 대사를 띄운다"));
            Assert.AreEqual(0, done, "false를 돌려주며 콜백까지 부르면 대사가 두 번 열린다");
            Assert.AreEqual(1, warned, "파일이 없다고 한 번 말한다");
            Assert.IsFalse(video.IsPlaying);
            Assert.IsFalse(ModalUIRegistry.IsAnyOpen(), "조작·모달을 남기지 않는다");
        }

        [Test]
        public void Prelude_NothingToPlay_ReturnsFalse()
        {
            StoryVideoDirector video = NewVideo();
            int done = 0;
            Assert.IsFalse(video.TryPlayPrelude(new StoryBeat { beatId = "qa_plain", lines = Lines(3) }, () => done++), "영상 없는 비트");
            Assert.IsFalse(video.TryPlayPrelude(null, () => done++));
            Assert.IsFalse(video.TryPlayPrelude(IntroBeat(StoryVideoLibrary.Ch6Wall), null));
            int warned = CountWarnings("[StoryVideo]", () =>
                Assert.IsFalse(video.TryPlayPrelude(IntroBeat("vid_qa_typo"), () => done++), "모르는 ID"));
            Assert.AreEqual(1, warned, "오타는 조용히 넘기지 않는다");
            Assert.AreEqual(0, done);
            Assert.IsFalse(video.IsPlaying);
        }

        [Test]
        public void Prelude_InactiveDirector_DoesNotGate()
        {
            // 꺼진 채 true를 돌려주면 Update가 안 돌아 시간 초과가 영영 안 온다 → 그 비트가 갇힌다.
            GameObject go = new GameObject("StoryPreludeTestsInactiveVideo");
            go.SetActive(false);
            spawned.Add(go);
            StoryVideoDirector video = go.AddComponent<StoryVideoDirector>();
            int done = 0;
            Assert.IsFalse(video.TryPlayPrelude(IntroBeat(StoryVideoLibrary.Ch6Wall), () => done++));
            Assert.AreEqual(0, done);
        }

        [Test]
        public void Prelude_CallbackRunsAfterTheDirectorIsClean()
        {
            // 콜백(대사 열기)이 다시 조작을 잠그고 모달을 건다 — 그때 영상은 이미 내려가 있어야 한다.
            Assume.That(!ModalUIRegistry.IsAnyOpen(), "다른 테스트가 모달을 남겼다");
            StoryVideoDirector video = NewVideo();
            bool sawPlaying = true, sawModal = true;
            Assert.IsTrue(video.PlayIntro(Def("qa_clean.mp4"), () =>
            {
                sawPlaying = video.IsPlaying;
                sawModal = ModalUIRegistry.IsAnyOpen();
            }));
            video.Stop();
            Assert.IsFalse(sawPlaying, "콜백이 불릴 때 영상은 이미 멈춰 있다");
            Assert.IsFalse(sawModal, "콜백이 불릴 때 영상은 모달에서 이미 빠져 있다");
        }

        [Test]
        public void Prelude_CallbackThrows_DirectorStaysUsable()
        {
            Assume.That(!ModalUIRegistry.IsAnyOpen(), "다른 테스트가 모달을 남겼다");
            StoryVideoDirector video = NewVideo();
            Assert.IsTrue(video.PlayIntro(Def("qa_throw.mp4"), () => throw new System.InvalidOperationException("qa")));
            Quietly(() => Assert.DoesNotThrow(video.Stop, "콜백의 예외가 건너뛰기(OnGUI)·재진입 Play를 끊지 않는다"));
            Assert.IsFalse(video.IsPlaying);
            Assert.IsFalse(ModalUIRegistry.IsAnyOpen());

            int next = 0;
            Assert.IsTrue(video.PlayIntro(Def("qa_after.mp4"), () => next++), "다음 영상은 평소대로 돈다");
            video.Stop();
            Assert.AreEqual(1, next);
        }

        // ── 다시보기 ─────────────────────────────────────────────

        [Test]
        public void PlayReplay_UnknownOrEmptyId_IsFalse()
        {
            StoryVideoDirector video = NewVideo();
            Assert.IsFalse(video.PlayReplay("vid_nope"));
            Assert.IsFalse(video.PlayReplay(null));
            Assert.IsFalse(video.PlayReplay(""));
            Assert.IsFalse(video.IsPlaying);
        }

        [Test]
        public void PlayReplay_KnownId_PlaysWithoutStorySideEffects()
        {
            StoryVideoDirector video = NewVideo();
            Assert.IsTrue(video.PlayReplay(StoryVideoLibrary.Ch12Ledger));
            Assert.IsTrue(video.IsPlaying);
            Assert.AreEqual(StoryVideoLibrary.Ch12Ledger, video.CurrentVideoId);
            Assert.IsFalse(video.IsPlayingPrelude, "다시보기는 대사를 열지 않는다");
            Assert.IsFalse(video.PlayReplay(StoryVideoLibrary.Ch1Prologue), "재생 중이면 끊지 않는다");
            Assert.AreEqual(StoryVideoLibrary.Ch12Ledger, video.CurrentVideoId);
            video.Stop();
            Assert.IsFalse(video.IsPlaying);
        }

        [Test]
        public void PlayReplay_DuringPrelude_DoesNotStealTheBeat()
        {
            StoryVideoDirector video = NewVideo();
            int done = 0;
            Assert.IsTrue(video.TryPlayPrelude(IntroBeat(StoryVideoLibrary.FinReturn), () => done++));
            Assert.IsFalse(video.PlayReplay(StoryVideoLibrary.Ch1Prologue), "대사 앞 영상을 끊으면 그 대사가 다시보기 밑에서 열린다");
            Assert.AreEqual(0, done);
            Assert.IsTrue(video.IsPlayingPrelude);
            video.Stop();
            Assert.AreEqual(1, done);
        }

        [Test]
        public void PlayReplay_InactiveDirector_IsFalse()
        {
            GameObject go = new GameObject("StoryPreludeTestsInactiveReplay");
            go.SetActive(false);
            spawned.Add(go);
            StoryVideoDirector video = go.AddComponent<StoryVideoDirector>();
            Assert.IsFalse(video.PlayReplay(StoryVideoLibrary.Ch1Prologue));
        }

        // ── 고리: 영상 → NPC 등장 연출 ─────────────────────────────────

        private sealed class FakePrelude : IStoryStagePrelude
        {
            public readonly string name;
            public readonly List<string> log;
            public bool hasContent = true;
            public bool completeSynchronously;
            public bool throwBeforeStart;
            public bool fireTwice;
            public bool lieAndFireAfterFalse;
            public System.Action pending;
            public int calls;

            public FakePrelude(string name, List<string> log)
            {
                this.name = name;
                this.log = log;
            }

            public bool TryPlayPrelude(StoryBeat beat, System.Action onDone)
            {
                calls++;
                if (throwBeforeStart) throw new System.InvalidOperationException("qa " + name);
                if (lieAndFireAfterFalse) { pending = onDone; return false; }
                if (!hasContent) return false;
                log.Add(name + ":start");
                if (completeSynchronously)
                {
                    log.Add(name + ":end");
                    onDone();
                    if (fireTwice) onDone();
                    return true;
                }
                pending = onDone;
                return true;
            }

            public void Finish()
            {
                log.Add(name + ":end");
                System.Action done = pending;
                pending = null;
                done?.Invoke();
                if (fireTwice) done?.Invoke();
            }
        }

        [Test]
        public void Chain_RunsVideoThenStageThenDialogue()
        {
            var log = new List<string>();
            var video = new FakePrelude("video", log);
            var stage = new FakePrelude("stage", log);
            var chain = new StoryPreludeChain(video, stage);
            int done = 0;

            Assert.IsTrue(chain.TryPlayPrelude(new StoryBeat { beatId = "qa" }, () => { done++; log.Add("dialogue"); }));
            CollectionAssert.AreEqual(new[] { "video:start" }, log, "영상이 먼저 — 그동안 인물은 들어오지 않는다");

            video.Finish();
            CollectionAssert.AreEqual(new[] { "video:start", "video:end", "stage:start" }, log);
            Assert.AreEqual(0, done);

            stage.Finish();
            CollectionAssert.AreEqual(new[] { "video:start", "video:end", "stage:start", "stage:end", "dialogue" }, log);
            Assert.AreEqual(1, done);
        }

        [Test]
        public void Chain_NoLinkHasContent_ReturnsFalseAndNeverCalls()
        {
            var log = new List<string>();
            var video = new FakePrelude("video", log) { hasContent = false };
            var stage = new FakePrelude("stage", log) { hasContent = false };
            int done = 0;
            Assert.IsFalse(new StoryPreludeChain(video, stage).TryPlayPrelude(new StoryBeat { beatId = "qa" }, () => done++),
                "아무 연출도 없으면 false — 대화창이 곧바로 대사를 띄운다");
            Assert.AreEqual(0, done, "false인데 부르면 대사가 두 번 열린다");
            Assert.AreEqual(1, video.calls);
            Assert.AreEqual(1, stage.calls);
        }

        [Test]
        public void Chain_OnlyOneLinkHasContent_RunsJustThatOne()
        {
            var log = new List<string>();
            var video = new FakePrelude("video", log) { hasContent = false };
            var stage = new FakePrelude("stage", log);
            int done = 0;
            Assert.IsTrue(new StoryPreludeChain(video, stage).TryPlayPrelude(new StoryBeat { beatId = "qa" }, () => done++));
            stage.Finish();
            Assert.AreEqual(1, done, "영상 없는 비트는 예전처럼 등장 연출 → 대사");

            log.Clear();
            var video2 = new FakePrelude("video", log);
            var stage2 = new FakePrelude("stage", log) { hasContent = false };
            int done2 = 0;
            Assert.IsTrue(new StoryPreludeChain(video2, stage2).TryPlayPrelude(new StoryBeat { beatId = "qa" }, () => done2++));
            video2.Finish();
            Assert.AreEqual(1, done2, "등장 연출이 없으면 영상 → 대사");
            Assert.AreEqual(1, stage2.calls, "영상이 끝난 뒤에야 등장 연출에 묻는다");
        }

        [Test]
        public void Chain_SynchronousAndDoubleCallbacks_OpenDialogueOnce()
        {
            var log = new List<string>();
            var video = new FakePrelude("video", log) { completeSynchronously = true, fireTwice = true };
            var stage = new FakePrelude("stage", log) { hasContent = false };
            int done = 0;
            Assert.IsTrue(new StoryPreludeChain(video, stage).TryPlayPrelude(new StoryBeat { beatId = "qa" }, () => done++),
                "이미 끝났어도 true — 대사는 콜백이 열었다");
            Assert.AreEqual(1, done, "같은 콜백이 두 번 와도 대사는 한 번");
        }

        [Test]
        public void Chain_LinkThatLiesThenFiresLate_IsIgnored()
        {
            var log = new List<string>();
            var liar = new FakePrelude("video", log) { lieAndFireAfterFalse = true };
            var stage = new FakePrelude("stage", log) { hasContent = false };
            int done = 0;
            Assert.IsFalse(new StoryPreludeChain(liar, stage).TryPlayPrelude(new StoryBeat { beatId = "qa" }, () => done++));
            liar.pending?.Invoke();   // false를 돌려 놓고 나중에 부른다 — 이미 호출부가 대사를 띄웠다
            Assert.AreEqual(0, done, "버린 고리의 늦은 콜백은 대사를 다시 열지 않는다");
        }

        [Test]
        public void Chain_FirstLinkThrows_SkipsToTheNext()
        {
            var log = new List<string>();
            var video = new FakePrelude("video", log) { throwBeforeStart = true };
            var stage = new FakePrelude("stage", log);
            int done = 0;
            bool started = false;
            Quietly(() => started = new StoryPreludeChain(video, stage).TryPlayPrelude(new StoryBeat { beatId = "qa" }, () => done++));
            Assert.IsTrue(started, "영상이 던져도 등장 연출은 돈다");
            stage.Finish();
            Assert.AreEqual(1, done);
        }

        [Test]
        public void Chain_StageThrowsAfterVideo_StillOpensDialogueOnce()
        {
            // 영상이 끝난 뒤 콜백 안에서 등장 연출이 던진다 — 대화창의 try/catch는 첫 호출만 감싸서 이건 고리만 받을 수 있다.
            var log = new List<string>();
            var video = new FakePrelude("video", log);
            var stage = new FakePrelude("stage", log) { throwBeforeStart = true };
            int done = 0;
            Assert.IsTrue(new StoryPreludeChain(video, stage).TryPlayPrelude(new StoryBeat { beatId = "qa" }, () => done++));
            Quietly(video.Finish);
            Assert.AreEqual(1, done, "연출을 잃어도 진행(대사)은 잃지 않는다");
        }

        [Test]
        public void Chain_DestroyedUnityLink_IsSkipped()
        {
            GameObject go = new GameObject("StoryPreludeTestsDestroyedVideo");
            StoryVideoDirector destroyed = go.AddComponent<StoryVideoDirector>();
            Object.DestroyImmediate(go);

            var log = new List<string>();
            var stage = new FakePrelude("stage", log);
            int done = 0;
            Assert.IsTrue(new StoryPreludeChain(destroyed, stage).TryPlayPrelude(IntroBeat(StoryVideoLibrary.Ch6Wall), () => done++),
                "인터페이스 참조의 null 비교는 파괴를 못 본다 — 고리가 Unity의 ==로 다시 본다");
            stage.Finish();
            Assert.AreEqual(1, done);
        }

        [Test]
        public void Chain_WithRealVideoDirector_VideoThenStageThenDialogue()
        {
            StoryVideoDirector video = NewVideo();
            var log = new List<string>();
            var stage = new FakePrelude("stage", log);
            int done = 0;
            var chain = new StoryPreludeChain(video, stage);

            Assert.IsTrue(chain.TryPlayPrelude(IntroBeat(StoryVideoLibrary.Ch6Wall), () => done++));
            Assert.IsTrue(video.IsPlayingPrelude);
            Assert.AreEqual(0, stage.calls, "영상이 도는 동안 인물은 들어오지 않는다");

            video.CloseModal();   // 아이가 「건너뛰기」를 눌렀다
            Assert.IsFalse(video.IsPlaying);
            Assert.AreEqual(1, stage.calls);
            Assert.AreEqual(0, done);
            stage.Finish();
            Assert.AreEqual(1, done);

            // 영상이 없는 비트는 영상 지휘자를 건너 등장 연출로 곧장 간다.
            int plain = 0;
            Assert.IsTrue(chain.TryPlayPrelude(new StoryBeat { beatId = "qa_plain", lines = Lines(2) }, () => plain++));
            Assert.IsFalse(video.IsPlaying);
            stage.Finish();
            Assert.AreEqual(1, plain);
        }

        // ── 배선 ─────────────────────────────────────────────────

        [Test]
        public void Bootstrap_WiresTheChainAndTheJournalReplay()
        {
            // 빠지면 예외도 경고도 없이 대사 앞 영상이 안 나오거나(체인 대신 연출만 넘김) 저널 「▶ 영상」이 안 선다.
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Core/PlaySceneBootstrap.cs"));
            StringAssert.Contains("npcDialogue.AutoWire(new InsectGame.Story.StoryPreludeChain(storyVideo, stageDirector))", source,
                "순서는 영상 → 등장 연출이다");
            StringAssert.DoesNotContain("npcDialogue.AutoWire(stageDirector)", source, "연출만 넘기면 대사 앞 영상이 안 나온다");

            int video = source.IndexOf("EnsureComponent<InsectGame.Story.StoryVideoDirector>", System.StringComparison.Ordinal);
            int journal = source.IndexOf("storyJournal.AutoWire(storyVideo)", System.StringComparison.Ordinal);
            Assert.Greater(video, 0);
            Assert.Greater(journal, video, "저널 배선은 영상 지휘자를 만든 뒤에");
        }

        // ── 도움 ─────────────────────────────────────────────────

        private StoryVideoDirector NewVideo()
        {
            GameObject go = new GameObject("StoryPreludeTestsVideo");
            spawned.Add(go);
            StoryVideoDirector video = go.AddComponent<StoryVideoDirector>();   // 활성 — Awake가 VideoPlayer를 붙인다(열지는 않는다)
            video.DecoderEnabled = false;
            return video;
        }

        private static StoryBeat IntroBeat(string introVideoId)
            => new StoryBeat { beatId = "qa_intro", introVideoId = introVideoId, lines = Lines(5) };

        private static List<StoryLine> Lines(int count)
        {
            var lines = new List<StoryLine>();
            for (int i = 0; i < count; i++) lines.Add(new StoryLine { speaker = "세라", text = "qa " + i });
            return lines;
        }

        private static StoryVideoDefinition Def(string fileName)
            => new StoryVideoDefinition("vid_qa", fileName, 12f, new[] { new StoryVideoCue(1f, 2f, "qa") });

        private static object Invoke(object target, string method, params object[] args)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"{target.GetType().Name}.{method}가 사라졌다");
            return info.Invoke(target, args);
        }

        private static void SetField(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"{target.GetType().Name}.{field}가 사라졌다");
            info.SetValue(target, value);
        }

        private static int CountWarnings(string prefix, System.Action action)
        {
            int warned = 0;
            Application.LogCallback count = (message, _, type) =>
            {
                if (type == LogType.Warning && message.StartsWith(prefix)) warned++;
            };
            Application.logMessageReceived += count;
            try { action(); }
            finally { Application.logMessageReceived -= count; }
            return warned;
        }

        /// <summary>
        /// 일부러 던지는 테스트 — 지휘자·고리가 남기는 LogError가 러너의 "예상 못 한 오류 로그" 실패로 번지지 않게
        /// 로거를 잠깐 끈다(LogAssert는 asmdef가 없어 참조가 안 된다).
        /// </summary>
        private static void Quietly(System.Action action)
        {
            bool was = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            try { action(); }
            finally { Debug.unityLogger.logEnabled = was; }
        }
    }
}
#endif
