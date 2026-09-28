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

        [TestCase(-72f)]
        [TestCase(0f)]
        [TestCase(72f)]
        public void RotateAttachment_HandAndNet_PreserveGripDistance(float angle)
        {
            Vector3 pivot = new Vector3(0.29f, 0.78f, 0f);
            Vector3 hand = new Vector3(0.29f, 0.52f, 0f);
            Vector3 handle = new Vector3(0.29f, 0.74f, 0f);
            Vector3 movedHand = PlayerVisualBuilder.RotateAttachment(hand, pivot, angle);
            Vector3 movedHandle = PlayerVisualBuilder.RotateAttachment(handle, pivot, angle);
            Assert.AreEqual(Vector3.Distance(hand, handle), Vector3.Distance(movedHand, movedHandle), 0.0001f);
            Vector3 restored = PlayerVisualBuilder.RotateAttachment(movedHand, pivot, -angle);
            Assert.Less(Vector3.Distance(hand, restored), 0.0001f);
        }

        [Test]
        public void DefaultNet_HandlePassesThroughHand_AndRingMeetsTip()
        {
            Transform hand = player.transform.Find("HandR");
            Transform handle = player.transform.Find("NetHandle");
            Transform ring = player.transform.Find("NetRing");
            Vector3 tip = handle.localPosition + handle.localRotation * Vector3.up * handle.localScale.y;
            Assert.Less(Vector3.Distance(tip, ring.localPosition), 0.001f);
            Assert.AreEqual(hand.localPosition.x, handle.localPosition.x, 0.001f);
            Assert.AreEqual(hand.localPosition.z, handle.localPosition.z, 0.001f);
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
