#if UNITY_EDITOR
using InsectGame.UI;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// <c>ModalUIRegistry.IsAnyOpenExcept</c> — 모달 안의 버튼이 "다른 연출이 도는 중인가"를 물을 때
    /// 자기 자신은 빼고 본다. 오프닝 다시보기(<c>OpeningReplayCoordinator.CanReplay</c>)가 쓴다:
    /// 설정 화면은 당연히 열려 있고, 그 위에 컷신·영상이 있으면 시작하면 안 된다.
    /// </summary>
    [TestFixture]
    public class ModalUIRegistryExceptTests
    {
        private sealed class SettingsLike : IModalUI
        {
            public bool IsOpen { get; set; } = true;
            public void CloseModal() { IsOpen = false; }
        }

        private sealed class OverlayLike : IModalUI
        {
            public bool IsOpen { get; set; } = true;
            public void CloseModal() { IsOpen = false; }
        }

        private SettingsLike settings;
        private OverlayLike overlay;

        [SetUp]
        public void SetUp()
        {
            settings = new SettingsLike();
            overlay = new OverlayLike();
        }

        [TearDown]
        public void TearDown()
        {
            ModalUIRegistry.Unregister(settings);
            ModalUIRegistry.Unregister(overlay);
        }

        [Test]
        public void IsAnyOpenExcept_OnlySelfOpen_IsFalse()
        {
            ModalUIRegistry.Register(settings);
            Assert.IsFalse(ModalUIRegistry.IsAnyOpenExcept(typeof(SettingsLike)));
            Assert.IsTrue(ModalUIRegistry.IsAnyOpen(), "자기 자신은 여전히 열려 있어야 한다");
        }

        [Test]
        public void IsAnyOpenExcept_OtherModalOnTop_IsTrue()
        {
            ModalUIRegistry.Register(settings);
            ModalUIRegistry.Register(overlay);
            Assert.IsTrue(ModalUIRegistry.IsAnyOpenExcept(typeof(SettingsLike)));
        }

        [Test]
        public void IsAnyOpenExcept_ClosedOther_IsPruned()
        {
            ModalUIRegistry.Register(settings);
            ModalUIRegistry.Register(overlay);
            overlay.IsOpen = false;   // 닫혔지만 Unregister를 안 부른 잔재
            Assert.IsFalse(ModalUIRegistry.IsAnyOpenExcept(typeof(SettingsLike)));
        }

        [Test]
        public void IsAnyOpenExcept_NullType_MatchesIsAnyOpen()
        {
            ModalUIRegistry.Register(settings);
            Assert.AreEqual(ModalUIRegistry.IsAnyOpen(), ModalUIRegistry.IsAnyOpenExcept(null));
        }
    }
}
#endif
