#if UNITY_EDITOR
using System.Reflection;
using InsectGame.Core;
using InsectGame.Story;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 스토리 발화 관문(<c>StoryDirector.ShouldDeferNow</c>).
    ///
    /// 이 게이트가 막는 것 둘:
    /// <list type="number">
    /// <item><b>대화 중 끼어들기.</b> <c>NpcDialogueUI.ShowStory</c>는 열린 모달을
    /// <c>CloseModal</c>로 밀어낸다. 마을 사람과 이야기하던 중 레벨업·퀘스트 완료가 일어나면
    /// <b>읽던 대화가 통째로 사라지고</b> 스토리가 시작됐다.</item>
    /// <item><b>전투 결과 화면 덮기.</b> 전투 승리는 같은 프레임에 XP·도감·퀘스트를 함께
    /// 갱신하므로 <c>LevelReach</c>/<c>DexProgress</c>/<c>QuestComplete</c>가 즉시 발화해
    /// 보상 패널 위로 대사창이 열렸다 — <c>BattleWin</c>만 미루고 있었던 탓이다.</item>
    /// </list>
    ///
    /// private이라 리플렉션으로 부른다. 공개 API로 올리지 않는 이유: 이건 내부 판단이고,
    /// 밖에서 부를 일이 생기면 그때 <c>RouteTrigger</c>를 쓰는 게 맞다.
    /// </summary>
    [TestFixture]
    public class StoryDeferGateTests
    {
        /// <summary>테스트용 모달 — 스택에 올라가면 <c>IsAnyOpen</c>이 참이 된다.</summary>
        private sealed class FakeModal : IModalUI
        {
            public bool IsOpen { get; set; } = true;
            public void CloseModal() { IsOpen = false; }
        }

        private GameObject host;
        private StoryDirector director;
        private GameObject cameraObject;
        private CameraFollower follower;
        private MethodInfo shouldDefer;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("StoryDeferGateTests");
            director = host.AddComponent<StoryDirector>();

            cameraObject = new GameObject("StoryDeferGateTestsCamera");
            follower = cameraObject.AddComponent<CameraFollower>();

            shouldDefer = typeof(StoryDirector).GetMethod(
                "ShouldDeferNow", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(shouldDefer,
                "ShouldDeferNow가 사라졌다 — 관문을 걷어내면 대사가 대화·전투 화면을 다시 덮는다");
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
        }

        private bool Defers()
        {
            return (bool)shouldDefer.Invoke(director, null);
        }

        [Test]
        public void NothingOpen_DoesNotDefer()
        {
            Assert.IsFalse(Defers(), "아무것도 안 떠 있으면 그 자리에서 발화해야 한다");
        }

        [Test]
        public void ModalOpen_Defers()
        {
            FakeModal modal = new FakeModal();
            ModalUIRegistry.Register(modal);
            try
            {
                Assert.IsTrue(Defers(),
                    "대화·컷신이 떠 있는데 발화하면 읽던 대사가 밀려나고 보상까지 지급된다");
            }
            finally
            {
                ModalUIRegistry.Unregister(modal);
            }
        }

        [Test]
        public void ModalClosed_StopsDeferring()
        {
            FakeModal modal = new FakeModal();
            ModalUIRegistry.Register(modal);
            modal.CloseModal();     // IsOpen=false → IsAnyOpen이 죽은 참조로 정리한다

            Assert.IsFalse(Defers(), "대화가 끝났으면 미뤄 둔 편이 곧바로 이어져야 한다");
            ModalUIRegistry.Unregister(modal);
        }

        [Test]
        public void BattleCameraActive_Defers()
        {
            director.AutoWire(follower);
            follower.EnterBattleMode(Vector3.zero, Vector3.forward * 5f);
            try
            {
                Assert.IsTrue(Defers(),
                    "전투 화면이 떠 있는데 발화하면 대사창이 보상 패널을 덮는다");
            }
            finally
            {
                follower.ExitBattleMode();
            }
        }

        [Test]
        public void BattleCameraReleased_StopsDeferring()
        {
            director.AutoWire(follower);
            follower.EnterBattleMode(Vector3.zero, Vector3.forward * 5f);
            follower.ExitBattleMode();

            Assert.IsFalse(Defers(), "전투가 끝났으면 미뤄 둔 트리거가 이어져야 한다");
        }

        [Test]
        public void NoCameraWired_FallsBackToModalCheckOnly()
        {
            // 카메라 미배선은 옛 경로(전투 화면 종료 통지 + 12초 타이머)로 떨어진다.
            // 그때도 모달 판정은 살아 있어야 한다.
            Assert.IsFalse(Defers());

            FakeModal modal = new FakeModal();
            ModalUIRegistry.Register(modal);
            try { Assert.IsTrue(Defers()); }
            finally { ModalUIRegistry.Unregister(modal); }
        }
    }
}
#endif
