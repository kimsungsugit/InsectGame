#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using InsectGame.Core;
using InsectGame.Story;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 전투 화면 뒤로 미룬 이야기의 대기 시계(<see cref="StoryBattleWait"/>)와 그것을 쓰는 세 지휘자.
    ///
    /// 결과 화면은 2026-10-04부터 눌러야 닫힌다(<c>BattleResultRules</c> — 예전엔 4초 뒤 저절로). 세 곳의 "굳은 전투 화면" 상한
    /// (컷신·영상 12초 포기, 카메라 없는 대사 큐 12초 폴백)이 결과 화면 시간을 세면, 보상을 천천히 보는 아이의 미뤄 둔 컷신·영상이
    /// 조용히 사라지거나 대사가 결과 화면 위로 뜬다. 여기서 고정하는 것 둘:
    /// <list type="number">
    /// <item>결과 화면에 30초 머문 뒤 닫아도 미뤄 둔 컷신·영상·대사가 나온다.</item>
    /// <item>결과 화면이 <b>아닌데</b> 전투 카메라가 안 풀리면(진짜로 멈춘 화면) 여전히 상한에 걸린다.</item>
    /// </list>
    ///
    /// 지휘자는 비활성 오브젝트에 붙인다 — Update·Awake가 돌지 않아 시계는 테스트가 넣는 시간으로만 흐르고, 영상 지휘자는
    /// <c>VideoPlayer</c>를 만들지 않아 디코더를 열지 않는다(배치·PlayMode 러너엔 디코더가 없다 — rules/testing.md).
    /// LogAssert는 이 어셈블리에서 참조가 안 된다(asmdef 없음) — 경고는 로그 이벤트로 직접 센다.
    /// </summary>
    [TestFixture]
    public class StoryBattleWaitTests
    {
        private const float Tick = 0.1f;
        private const int ThirtySeconds = 300;   // Tick × 300

        private sealed class FakeModal : IModalUI
        {
            public bool IsOpen { get; set; } = true;
            public void CloseModal() { IsOpen = false; }
        }

        private readonly List<GameObject> spawned = new List<GameObject>();
        private CameraFollower follower;

        [SetUp]
        public void SetUp()
        {
            GameObject cameraObject = new GameObject("StoryBattleWaitTestsCamera");
            spawned.Add(cameraObject);
            follower = cameraObject.AddComponent<CameraFollower>();
        }

        [TearDown]
        public void TearDown()
        {
            if (follower != null) follower.ExitBattleMode();
            foreach (GameObject go in spawned)
                if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        // ── 순수 규칙 ───────────────────────────────────────────────

        [Test]
        public void Advance_ResultScreenShowing_ClockStandsStill()
        {
            Assert.AreEqual(5f, StoryBattleWait.Advance(5f, 1f, resultScreenShowing: true));
        }

        [Test]
        public void Advance_OutsideResultScreen_CountsRealSeconds()
        {
            Assert.AreEqual(6f, StoryBattleWait.Advance(5f, 1f, resultScreenShowing: false), 1e-5f);
            Assert.AreEqual(5f, StoryBattleWait.Advance(5f, -1f, resultScreenShowing: false), "되감기는 프레임은 시계를 줄이지 않는다");
        }

        [Test]
        public void TickScene_ThirtySecondsOnResultScreen_ThenClosed_Plays()
        {
            float waited = 0f;
            for (int i = 0; i < ThirtySeconds; i++)
                Assert.AreEqual(StoryBattleWait.SceneStep.Wait,
                    StoryBattleWait.TickScene(ref waited, Tick, battleOnScreen: true, resultScreenShowing: true, modalOpen: false),
                    $"결과 화면 {i * Tick:0.0}초 — 보상을 보는 시간에 컷신을 버리면 안 된다");
            Assert.AreEqual(0f, waited, "결과 화면 시간은 대기로 세지 않는다");

            Assert.AreEqual(StoryBattleWait.SceneStep.Play,
                StoryBattleWait.TickScene(ref waited, Tick, battleOnScreen: false, resultScreenShowing: false, modalOpen: false));
        }

        [Test]
        public void TickScene_StuckBattleCameraOutsideResult_GivesUpAtLimit()
        {
            float waited = 0f;
            int ticks = 0;
            StoryBattleWait.SceneStep step;
            do
            {
                step = StoryBattleWait.TickScene(ref waited, Tick, battleOnScreen: true, resultScreenShowing: false, modalOpen: false);
                ticks++;
            } while (step == StoryBattleWait.SceneStep.Wait && ticks < 1000);

            Assert.AreEqual(StoryBattleWait.SceneStep.GiveUp, step, "결과 화면이 아닌데 안 풀리는 화면은 영원히 기다리지 않는다");
            Assert.AreEqual(StoryBattleWait.SceneGiveUpSeconds, ticks * Tick, Tick + 1e-3f, "상한은 그대로다");
        }

        [Test]
        public void TickScene_ResultTimeThenStuck_CountsOnlyTheStuckPart()
        {
            float waited = 0f;
            for (int i = 0; i < ThirtySeconds; i++)
                StoryBattleWait.TickScene(ref waited, Tick, true, true, false);

            // 결과 화면이 끝났는데(단계가 바뀌었는데) 카메라가 안 풀린다 — 여기서부터 센다.
            float stuck = StoryBattleWait.SceneGiveUpSeconds - 1f;
            Assert.AreEqual(StoryBattleWait.SceneStep.Wait, StoryBattleWait.TickScene(ref waited, stuck, true, false, false),
                "앞의 결과 화면 30초가 상한에 보태지면 굳은 지 11초 만에 버린다");
            Assert.AreEqual(StoryBattleWait.SceneStep.GiveUp, StoryBattleWait.TickScene(ref waited, 1.1f, true, false, false));
        }

        [Test]
        public void TickScene_ModalOnlyAfterBattle_NeverGivesUp()
        {
            // 전투가 닫힌 뒤 이어진 대사(모달)를 오래 읽는다 — 영상은 그 뒤에 나와야 한다.
            float waited = 0f;
            for (int i = 0; i < ThirtySeconds * 2; i++)
                Assert.AreEqual(StoryBattleWait.SceneStep.Wait,
                    StoryBattleWait.TickScene(ref waited, Tick, battleOnScreen: false, resultScreenShowing: false, modalOpen: true));
            Assert.AreEqual(StoryBattleWait.SceneStep.Play,
                StoryBattleWait.TickScene(ref waited, Tick, false, false, false));
        }

        [Test]
        public void ReadProbe_MissingOrThrowing_IsFalse()
        {
            Assert.IsFalse(StoryBattleWait.ReadProbe(null), "미배선이면 예전처럼 결과 화면도 센다");
            Assert.IsFalse(StoryBattleWait.ReadProbe(() => throw new System.InvalidOperationException("qa")),
                "전투 화면 쪽 예외로 대기 규칙이 죽지 않는다");
            Assert.IsTrue(StoryBattleWait.ReadProbe(() => true));
        }

        // ── 컷신 ─────────────────────────────────────────────────

        [Test]
        public void Cutscene_ResultScreenThirtySeconds_ThenClosed_PlaysDeferredCutscene()
        {
            bool resultShowing = true;
            CutsceneDirector cutscene = NewCutscene(() => resultShowing);
            try
            {
                follower.EnterBattleMode(Vector3.zero, Vector3.forward * 5f);
                Invoke(cutscene, "OnBeatCompleted", new StoryBeat { beatId = "qa_cleanse", cutsceneId = CutsceneLibrary.BlightCleanse });
                Assert.IsFalse(cutscene.IsPlaying, "전투 화면이 떠 있으면 미룬다");

                for (int i = 0; i < ThirtySeconds; i++)
                    Assert.AreEqual(StoryBattleWait.SceneStep.Wait, Step(cutscene, Tick),
                        $"결과 화면 {i * Tick:0.0}초 — 거점 정화 컷신을 버리면 안 된다");

                // 플레이어가 결과 화면을 닫았다 — EndBattle이 카메라를 푼다.
                resultShowing = false;
                follower.ExitBattleMode();
                Assert.AreEqual(StoryBattleWait.SceneStep.Play, Step(cutscene, Tick));
                Assert.IsTrue(cutscene.IsPlaying, "결과 화면을 30초 본 뒤에도 컷신이 나온다");
            }
            finally { cutscene.Stop(); }
        }

        [Test]
        public void Cutscene_StuckBattleCameraOutsideResult_StillDroppedAtLimit()
        {
            CutsceneDirector cutscene = NewCutscene(() => false);
            int warned = CountWarnings("[Cutscene]", () =>
            {
                follower.EnterBattleMode(Vector3.zero, Vector3.forward * 5f);
                Invoke(cutscene, "OnBeatCompleted", new StoryBeat { beatId = "qa_cleanse", cutsceneId = CutsceneLibrary.BlightCleanse });

                Assert.AreEqual(StoryBattleWait.SceneStep.Wait, Step(cutscene, StoryBattleWait.SceneGiveUpSeconds - 0.5f));
                Assert.AreEqual(StoryBattleWait.SceneStep.GiveUp, Step(cutscene, 1f),
                    "결과 화면이 아닌데 카메라가 안 풀리면 상한에서 버린다 — 억지로 틀면 전투 구도에 갇힌다");

                follower.ExitBattleMode();
                Assert.AreEqual(StoryBattleWait.SceneStep.None, Step(cutscene, Tick), "버린 컷신이 뒤늦게 튀어나오지 않는다");
                Assert.IsFalse(cutscene.IsPlaying);
            });
            cutscene.Stop();
            Assert.AreEqual(1, warned, "버릴 때 한 번 말한다 — 컷신이 안 나오는 건 화면상 티가 안 난다");
        }

        // ── 영상 ─────────────────────────────────────────────────

        [Test]
        public void Video_ResultScreenThirtySeconds_ThenClosed_PlaysDeferredVideo()
        {
            Assume.That(!ModalUIRegistry.IsAnyOpen(), "다른 테스트가 모달을 남겼다");
            bool resultShowing = true;
            StoryVideoDirector video = NewVideo(() => resultShowing);

            follower.EnterBattleMode(Vector3.zero, Vector3.forward * 5f);
            Invoke(video, "OnBeatCompleted", new StoryBeat { beatId = "qa_chief", videoId = StoryVideoLibrary.Ch12Ledger });
            Assert.IsNotNull(Pending(video), "전투 화면이 떠 있으면 미룬다");

            for (int i = 0; i < ThirtySeconds; i++)
                Assert.AreEqual(StoryBattleWait.SceneStep.Wait, Step(video, Tick),
                    $"결과 화면 {i * Tick:0.0}초 — 간부전 승리 영상을 버리면 안 된다");

            resultShowing = false;
            follower.ExitBattleMode();
            Assert.AreEqual(StoryBattleWait.SceneStep.Play, Step(video, Tick), "결과 화면을 30초 본 뒤에도 영상이 나온다");
            Assert.IsNull(Pending(video));
        }

        [Test]
        public void Video_LongDialogueAfterBattle_DoesNotDropDeferredVideo()
        {
            Assume.That(!ModalUIRegistry.IsAnyOpen(), "다른 테스트가 모달을 남겼다");
            StoryVideoDirector video = NewVideo(() => false);
            follower.EnterBattleMode(Vector3.zero, Vector3.forward * 5f);
            Invoke(video, "OnBeatCompleted", new StoryBeat { beatId = "qa_chief", videoId = StoryVideoLibrary.Ch12Ledger });
            follower.ExitBattleMode();

            // 전투가 닫히자 미뤄 둔 다른 대사가 먼저 열렸다 — 아이가 30초 읽는다.
            FakeModal dialogue = new FakeModal();
            ModalUIRegistry.Register(dialogue);
            try
            {
                for (int i = 0; i < ThirtySeconds; i++)
                    Assert.AreEqual(StoryBattleWait.SceneStep.Wait, Step(video, Tick),
                        "대사를 읽는 시간은 굳은 화면이 아니다 — 영상을 버리면 안 된다");
            }
            finally
            {
                dialogue.CloseModal();
                ModalUIRegistry.Unregister(dialogue);
            }
            Assert.AreEqual(StoryBattleWait.SceneStep.Play, Step(video, Tick));
        }

        [Test]
        public void Video_StuckBattleCameraOutsideResult_StillDroppedAtLimit()
        {
            Assume.That(!ModalUIRegistry.IsAnyOpen(), "다른 테스트가 모달을 남겼다");
            StoryVideoDirector video = NewVideo(() => false);
            int warned = CountWarnings("[StoryVideo]", () =>
            {
                follower.EnterBattleMode(Vector3.zero, Vector3.forward * 5f);
                Invoke(video, "OnBeatCompleted", new StoryBeat { beatId = "qa_chief", videoId = StoryVideoLibrary.Ch12Ledger });
                Assert.AreEqual(StoryBattleWait.SceneStep.Wait, Step(video, StoryBattleWait.SceneGiveUpSeconds - 0.5f));
                Assert.AreEqual(StoryBattleWait.SceneStep.GiveUp, Step(video, 1f));
                Assert.IsNull(Pending(video));
            });
            Assert.AreEqual(1, warned);
        }

        // ── 대사 큐(StoryDirector) ───────────────────────────────────

        [Test]
        public void Story_NoCamera_ResultScreenThirtySeconds_DoesNotFireOverIt_ThenFiresOnClose()
        {
            Assume.That(!ModalUIRegistry.IsAnyOpen(), "다른 테스트가 모달을 남겼다");
            bool resultShowing = true;
            StoryDirector director = NewStory(() => resultShowing);
            WithBattleWinBeat(director, () =>
            {
                string fired = null;
                director.StoryBeatTriggered += beat => fired = beat.beatId;
                Invoke(director, "DeferTrigger", "BattleWin", "qa_bug");

                Assert.IsTrue((bool)Invoke(director, "ShouldDeferNow"), "결과 화면은 카메라 없이도 전투 화면으로 친다");
                for (int i = 0; i < ThirtySeconds; i++) Invoke(director, "TickPendingTriggers", Tick);
                Assert.IsNull(fired, "카메라가 없을 때의 12초 폴백이 결과 화면 위로 대사를 띄우면 보상 패널을 덮는다");

                resultShowing = false;
                director.NotifyBattlePresentationClosed();
                Assert.AreEqual("qa_win", fired, "결과 화면을 30초 본 뒤 닫아도 미뤄 둔 대사가 나온다");
            });
        }

        [Test]
        public void Story_NoCamera_StuckNonResultScreen_StillFiresAtFallback()
        {
            Assume.That(!ModalUIRegistry.IsAnyOpen(), "다른 테스트가 모달을 남겼다");
            StoryDirector director = NewStory(() => false);
            WithBattleWinBeat(director, () =>
            {
                string fired = null;
                director.StoryBeatTriggered += beat => fired = beat.beatId;
                int warned = CountWarnings("[Story]", () =>
                {
                    Invoke(director, "DeferTrigger", "BattleWin", "qa_bug");
                    Invoke(director, "TickPendingTriggers", StoryBattleWait.DialogueFallbackSeconds - 0.5f);
                    Assert.IsNull(fired, "통지를 기다리는 동안은 쏘지 않는다");
                    Invoke(director, "TickPendingTriggers", 1f);
                });
                Assert.AreEqual("qa_win", fired, "종료 통지가 끝내 안 오면 상한에서 그냥 쏜다 — 진행을 잃는 것보다 낫다");
                Assert.AreEqual(1, warned);
            });
        }

        [Test]
        public void Story_CameraWired_LongResultScreen_FiresAfterCloseWithoutStuckWarning()
        {
            Assume.That(!ModalUIRegistry.IsAnyOpen(), "다른 테스트가 모달을 남겼다");
            bool resultShowing = true;
            StoryDirector director = NewStory(() => resultShowing);
            director.AutoWire(follower);
            WithBattleWinBeat(director, () =>
            {
                string fired = null;
                director.StoryBeatTriggered += beat => fired = beat.beatId;
                int warned = CountWarnings("[Story]", () =>
                {
                    follower.EnterBattleMode(Vector3.zero, Vector3.forward * 5f);
                    Invoke(director, "DeferTrigger", "BattleWin", "qa_bug");
                    for (int i = 0; i < ThirtySeconds * 3; i++) Invoke(director, "TickPendingTriggers", Tick);   // 90초
                    Assert.IsNull(fired, "전투 화면 위로 대사를 쏘지 않는다");

                    // 닫았다 — 이번엔 통지 대신 다음 프레임이 흘린다(통지 경로는 위 테스트가 본다).
                    resultShowing = false;
                    follower.ExitBattleMode();
                    Invoke(director, "TickPendingTriggers", Tick);
                });
                Assert.AreEqual("qa_win", fired);
                Assert.AreEqual(0, warned, "결과 화면을 오래 본 것은 '화면이 안 닫혔다'가 아니다 — 거짓 경고를 남기지 않는다");
            });
        }

        // ── 배선 ─────────────────────────────────────────────────

        [Test]
        public void Bootstrap_WiresTheResultProbeIntoAllThreeDirectors()
        {
            // 빠지면 예외도 경고도 없이 예전 동작(결과 화면도 센다)으로 돌아간다 — 소스에서 확인한다.
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Core/PlaySceneBootstrap.cs"));
            StringAssert.Contains("storyDirector.AutoWire(battleResultShowing)", source);
            StringAssert.Contains("cutscene.AutoWire(battleResultShowing)", source);
            StringAssert.Contains("storyVideo.AutoWire(battleResultShowing)", source);
            StringAssert.Contains("battleScreen.ResultShownSeconds", source, "1대1 결과 화면");
            StringAssert.Contains("raidBattleUi.ResultShownSeconds", source, "레이드 결과 화면 — fin_seal(전설)은 레이드로만 이긴다");
        }

        // ── 도움 ─────────────────────────────────────────────────

        private GameObject Inactive(string name)
        {
            GameObject go = new GameObject(name);
            go.SetActive(false);   // Awake·Update가 돌지 않는다 — 시계는 테스트가 넣는 시간으로만 흐른다
            spawned.Add(go);
            return go;
        }

        private CutsceneDirector NewCutscene(System.Func<bool> resultShowing)
        {
            GameObject player = new GameObject("StoryBattleWaitTestsPlayer");
            spawned.Add(player);
            CutsceneDirector cutscene = Inactive("StoryBattleWaitTestsCutscene").AddComponent<CutsceneDirector>();
            cutscene.AutoWire(null, follower, null, player.transform);
            cutscene.AutoWire(resultShowing);
            return cutscene;
        }

        private StoryVideoDirector NewVideo(System.Func<bool> resultShowing)
        {
            StoryVideoDirector video = Inactive("StoryBattleWaitTestsVideo").AddComponent<StoryVideoDirector>();
            video.AutoWire(null, follower, null);
            video.AutoWire(resultShowing);
            return video;
        }

        private StoryDirector NewStory(System.Func<bool> resultShowing)
        {
            StoryDirector director = Inactive("StoryBattleWaitTestsStory").AddComponent<StoryDirector>();
            director.AutoWire(resultShowing);
            return director;
        }

        private static StoryBattleWait.SceneStep Step(Object director, float deltaSeconds)
            => (StoryBattleWait.SceneStep)Invoke(director, "TickPending", deltaSeconds);

        private static object Pending(StoryVideoDirector video) => typeof(StoryVideoDirector)
            .GetField("pending", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(video);

        private static object Invoke(object target, string method, params object[] args)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"{target.GetType().Name}.{method}가 사라졌다");
            return info.Invoke(target, args);
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

        /// <summary>아무 승리에나 뜨는 BattleWin 비트 하나만 둔 이야기로 돌린다 — 실제 계정 진행·저장과 무관하다(CompleteBeat는 부르지 않는다).</summary>
        private static void WithBattleWinBeat(StoryDirector director, System.Action action)
        {
            FieldInfo cache = typeof(StoryService).GetField("cache", BindingFlags.Static | BindingFlags.NonPublic);
            object previous = cache.GetValue(null);
            cache.SetValue(null, new Dictionary<string, StoryBeat>
            {
                ["qa_win"] = new StoryBeat { beatId = "qa_win", trigger = new StoryTrigger { type = "BattleWin", param = "" } }
            });
            typeof(StoryDirector).GetField("progress", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(director, new StoryProgressData());
            try { action(); }
            finally { cache.SetValue(null, previous); }
        }
    }
}
#endif
