#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using InsectGame.Core;
using InsectGame.Story;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 첫 파트너 지급은 "포획"이 아니다.
    ///
    /// 어르신이 파트너를 건네는 순간(<c>ch1_intro</c> 보상) 컬렉션이 <c>InsectCaptured</c>를 울리고,
    /// 그게 그대로 <c>CaptureInsect</c> 트리거가 되면 다음 비트 <c>ch1_first_capture</c>
    /// ("훌륭해! 이 초원에서 곤충을 거둬 왔구나")가 <b>아무것도 잡기 전에</b> 뜬다. 정작 처음 잡았을 때는
    /// 그 대사가 이미 지나가 있다. 예외도 경고도 없고 대사는 멀쩡히 뜨므로 눈으로 순서를 따져야만 보인다.
    /// </summary>
    [TestFixture]
    public class StoryStarterGrantTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        private GameObject host;
        private StoryDirector director;
        private MethodInfo onInsectCaptured;
        private FieldInfo grantingStarter;
        private List<string> frameCaptures;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("StoryStarterGrantTests");
            director = host.AddComponent<StoryDirector>();
            onInsectCaptured = typeof(StoryDirector).GetMethod("OnInsectCaptured", Private);
            grantingStarter = typeof(StoryDirector).GetField("grantingStarter", Private);
            Assert.IsNotNull(onInsectCaptured);
            Assert.IsNotNull(grantingStarter, "첫 파트너 지급을 가리는 표지가 사라졌다");
            frameCaptures = (List<string>)typeof(StoryDirector).GetField("frameCaptures", Private).GetValue(director);
        }

        [TearDown]
        public void TearDown()
        {
            if (frameCaptures != null) frameCaptures.Clear();
            if (host != null) Object.DestroyImmediate(host);
        }

        private void Capture(string insectId)
        {
            onInsectCaptured.Invoke(director, new object[] { new PlayerInsectData { insectId = insectId } });
        }

        [Test]
        public void OrdinaryCapture_IsQueuedAsACaptureTrigger()
        {
            Capture("beetle_basic");
            CollectionAssert.AreEqual(new[] { "beetle_basic" }, frameCaptures);
        }

        [Test]
        public void StarterGrant_IsNotACapture()
        {
            grantingStarter.SetValue(director, true);
            Capture("longhorn_beetle");
            Assert.AreEqual(0, frameCaptures.Count, "첫 파트너를 받는 순간 '첫 포획 축하' 대사가 뜬다");
        }

        [Test]
        public void StarterBeat_IsTheOneThatHandsOverAnInsect()
        {
            // 가리는 조건이 beatId == StarterBeatId다. 그 비트가 곤충을 안 주게 바뀌면 이 장치가 헛돈다.
            Assert.IsTrue(StoryService.TryGetBeat(InsectGame.Data.StarterInsectCatalog.StarterBeatId, out StoryBeat beat));
            Assert.IsFalse(string.IsNullOrEmpty(beat.onComplete.rewardInsectId));
        }
    }
}
#endif
