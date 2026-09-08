#if UNITY_EDITOR
using InsectGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// <see cref="PlayerVisualBuilder.RebuildFromPrefs"/> — 캐릭터 생성 뒤 몸을 다시 짓는다.
    ///
    /// 플레이어 밑에는 빌더가 만든 노드만 있는 게 아니다 — <c>CaptureProximityTrigger</c>가
    /// 자식으로 붙는다. 첫 구현이 자식 전체를 파괴해 **생성 직후 포획이 죽었다**(2026-09-09 자체 검토).
    /// </summary>
    [TestFixture]
    public class PlayerVisualRebuildTests
    {
        private GameObject player;

        [SetUp]
        public void SetUp()
        {
            player = new GameObject("PlayerVisualRebuildTests");
            new GameObject("ProximityTrigger").transform.SetParent(player.transform, false);
            player.AddComponent<PlayerVisualBuilder>();   // Awake → BuildAll
        }

        [TearDown]
        public void TearDown()
        {
            if (player != null) Object.DestroyImmediate(player);
        }

        [Test]
        public void RebuildFromPrefs_KeepsForeignChildren_ReplacesBuiltNodes()
        {
            Transform bodyBefore = player.transform.Find("Body");
            Assert.IsNotNull(bodyBefore, "빌더가 Body를 짓지 않았다 — 테스트 전제가 깨졌다");

            player.GetComponent<PlayerVisualBuilder>().RebuildFromPrefs();

            Assert.IsNotNull(player.transform.Find("ProximityTrigger"), "남이 붙인 자식을 파괴하면 포획이 죽는다");
            Transform bodyAfter = player.transform.Find("Body");
            Assert.IsNotNull(bodyAfter, "재빌드 뒤 몸이 없다");
            Assert.AreNotSame(bodyBefore, bodyAfter, "옛 노드가 그대로면 외형이 안 바뀐다");
        }
    }
}
#endif
