#if UNITY_EDITOR
using System.Reflection;
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    [TestFixture]
    public class RegionManagerStartPolicyTests
    {
        private string legacyKey;
        private bool legacyKeyExisted;
        private string legacyValue;
        private GameObject managerObject;
        private RegionManager manager;

        [SetUp]
        public void SetUp()
        {
            legacyKey = GameConstants.PrefsKeys.LastSubAreaId;
            legacyKeyExisted = PlayerPrefs.HasKey(legacyKey);
            legacyValue = PlayerPrefs.GetString(legacyKey, string.Empty);

            PlayerPrefs.DeleteKey(legacyKey);
            PlayerPrefs.Save();

            managerObject = new GameObject("RegionManagerStartPolicyTests");
            manager = managerObject.AddComponent<RegionManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (managerObject != null)
            {
                Object.DestroyImmediate(managerObject);
            }

            if (legacyKeyExisted)
            {
                PlayerPrefs.SetString(legacyKey, legacyValue);
            }
            else
            {
                PlayerPrefs.DeleteKey(legacyKey);
            }
            PlayerPrefs.Save();
        }

        [Test]
        public void Initialize_LegacyLastSubAreaIdExists_DeletesLegacyKey()
        {
            PlayerPrefs.SetString(legacyKey, "meadow_cave");
            PlayerPrefs.Save();

            manager.Initialize(new RegionData[0]);

            Assert.IsFalse(PlayerPrefs.HasKey(legacyKey));
        }

        [Test]
        public void RequestEnterSubArea_NearbySubAreaExists_EntersWithoutPersistingLegacyKey()
        {
            SubAreaData nearby = new SubAreaData
            {
                subAreaId = "test_sub_area",
                displayName = "Test SubArea"
            };
            manager.Initialize(new RegionData[0]);

            FieldInfo nearbyField = typeof(RegionManager).GetField(
                "nearbySubArea",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(nearbyField);
            nearbyField.SetValue(manager, nearby);

            SubAreaData entered = null;
            manager.SubAreaChanged += subArea => entered = subArea;

            manager.RequestEnterSubArea();

            Assert.AreSame(nearby, manager.CurrentSubArea);
            Assert.AreSame(nearby, entered);
            Assert.IsFalse(PlayerPrefs.HasKey(legacyKey));
        }
    }
}
#endif
