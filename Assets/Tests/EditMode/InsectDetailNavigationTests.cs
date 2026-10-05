#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using InsectGame.Core;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    [TestFixture]
    public class InsectDetailNavigationTests
    {
        private GameObject host;
        private PlayerInsectCollection insects;
        private CollectionUI collectionUI;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("InsectDetailNavigationTests");
            host.SetActive(false); // 세이브 로드를 실행하지 않는다.
            insects = host.AddComponent<PlayerInsectCollection>();
            collectionUI = host.AddComponent<CollectionUI>();
            collectionUI.AutoWire(insects, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (collectionUI != null) collectionUI.CloseModal();
            if (host != null) Object.DestroyImmediate(host);
        }

        [Test]
        public void OpenDetailForInstance_UnknownId_DoesNotOpenCollection()
        {
            Assert.IsFalse(collectionUI.OpenDetailForInstance("missing"));
            Assert.IsFalse(collectionUI.IsOpen);
        }

        [Test]
        public void OpenDetailForInstance_SameSpecies_SelectsExactOwnedInstance()
        {
            var first = new PlayerInsectData { instanceId = "owned-first", insectId = "beetle" };
            var second = new PlayerInsectData { instanceId = "owned-second", insectId = "beetle" };
            FieldInfo lookupField = typeof(PlayerInsectCollection).GetField(
                "lookup", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(lookupField);
            var lookup = (Dictionary<string, PlayerInsectData>)lookupField.GetValue(insects);
            lookup.Add(first.instanceId, first);
            lookup.Add(second.instanceId, second);

            Assert.IsTrue(collectionUI.OpenDetailForInstance(second.instanceId));
            Assert.IsTrue(collectionUI.IsOpen);
            Assert.AreSame(collectionUI, ModalUIRegistry.TopModal);
            FieldInfo selectedField = typeof(CollectionUI).GetField(
                "selectedInstanceId", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(selectedField);
            Assert.AreEqual(second.instanceId, selectedField.GetValue(collectionUI));

            Assert.IsTrue(collectionUI.OpenDetailForInstance(first.instanceId));
            Assert.AreEqual(first.instanceId, selectedField.GetValue(collectionUI));
        }
    }
}
#endif
