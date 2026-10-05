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
        public void ForceExitSubArea_ClearsStaleEntranceBeforeAnotherInput()
        {
            var nearby = new SubAreaData { subAreaId = "test_exit" };
            typeof(RegionManager).GetField("nearbySubArea", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, nearby);
            manager.RequestEnterSubArea();
            manager.ForceExitSubArea();
            manager.RequestEnterSubArea();
            Assert.IsNull(manager.CurrentSubArea);
            Assert.IsNull(manager.NearbySubArea);
            Assert.IsFalse(manager.SubAreaSticky);
        }

        [Test]
        public void SafeSubAreaSpawn_BlockedEntrance_DoesNotChooseBeyondSmallestWalls()
        {
            Vector3 origin = new Vector3(4000f, 0f, 4000f);
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                blocker.transform.position = origin + new Vector3(0f, 1.5f, -5f);
                blocker.transform.localScale = new Vector3(9f, 4f, 7f);
                Physics.SyncTransforms();
                var method = typeof(SubAreaWorldBuilder).GetMethod("FindSafeSpawnPosition",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Vector3 spawn = (Vector3)method.Invoke(null, new object[] { origin });
                Assert.That(spawn.x - origin.x, Is.InRange(-8f, 8f));
                Assert.That(spawn.z - origin.z, Is.InRange(-8f, 8f));
                Assert.IsFalse(blocker.GetComponent<Collider>().bounds.Contains(spawn + Vector3.up));
            }
            finally { Object.DestroyImmediate(blocker); }
        }

        [Test]
        public void SubAreaDecoration_ColliderDisabledBeforeDeferredDestruction()
        {
            var builder = managerObject.AddComponent<SubAreaWorldBuilder>();
            var decoration = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Collider collider = decoration.GetComponent<Collider>();
                typeof(SubAreaWorldBuilder).GetMethod("NoCollider", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(builder, new object[] { decoration });
                Assert.IsFalse(collider.enabled);
            }
            finally { Object.DestroyImmediate(decoration); }
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
