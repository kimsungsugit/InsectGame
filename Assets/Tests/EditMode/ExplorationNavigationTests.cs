#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using InsectGame.UI;
using InsectGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    [TestFixture]
    public class ExplorationNavigationTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void StopNavigation_DiscardsClickAndAutomaticDestination(bool automatic)
        {
            var host = new GameObject("Navigation reset fixture");
            try
            {
                var movement = host.AddComponent<PlayerMovement>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(PlayerMovement).GetField("movingToClick", flags).SetValue(movement, true);
                typeof(PlayerMovement).GetField("autoRunning", flags).SetValue(movement, automatic);
                movement.StopNavigation();
                Assert.IsFalse((bool)typeof(PlayerMovement).GetField("movingToClick", flags).GetValue(movement));
                Assert.IsFalse(movement.IsAutoRunning);
            }
            finally { Object.DestroyImmediate(host); }
        }
        [Test]
        public void Movement_OpenMenu_CancelsLatchedMovementWithoutFreezingMenu()
        {
            var go = new GameObject("Modal movement test");
            var movement = go.AddComponent<PlayerMovement>();
            var menu = go.AddComponent<QuickAccessBarUI>();
            try
            {
                typeof(QuickAccessBarUI).GetMethod("TryNavigate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(menu, new object[] { QuickAccessBarUI.Destination.Menu });
                foreach (string field in new[] { "movingToClick", "autoRunning", "joystickActive", "guiKeyW", "guiClickRequest" })
                    typeof(PlayerMovement).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(movement, true);
                typeof(PlayerMovement).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(movement, null);
                foreach (string field in new[] { "movingToClick", "autoRunning", "joystickActive", "guiKeyW", "guiClickRequest" })
                    Assert.IsFalse((bool)typeof(PlayerMovement).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(movement));
                Assert.IsTrue(menu.IsOpen);
                Assert.IsFalse(movement.IsFrozen);
            }
            finally { menu.CloseModal(); Object.DestroyImmediate(go); }
        }

        [Test]
        public void Shortcuts_Field_HasSevenPrimaryDestinations()
        {
            CollectionAssert.AreEqual(new[] { QuickAccessBarUI.Destination.Dex, QuickAccessBarUI.Destination.Collection,
                QuickAccessBarUI.Destination.Team, QuickAccessBarUI.Destination.Inventory,
                QuickAccessBarUI.Destination.Quest, QuickAccessBarUI.Destination.Map,
                QuickAccessBarUI.Destination.Menu }, QuickAccessBarUI.Shortcuts);
        }

        [TestCase(QuickAccessBarUI.Destination.Dex, KeyCode.N)]
        [TestCase(QuickAccessBarUI.Destination.Team, KeyCode.T)]
        [TestCase(QuickAccessBarUI.Destination.Training, KeyCode.G)]
        [TestCase(QuickAccessBarUI.Destination.Collection, KeyCode.C)]
        [TestCase(QuickAccessBarUI.Destination.Quest, KeyCode.Q)]
        [TestCase(QuickAccessBarUI.Destination.Map, KeyCode.M)]
        [TestCase(QuickAccessBarUI.Destination.Outfit, KeyCode.P)]
        [TestCase(QuickAccessBarUI.Destination.Shop, KeyCode.F4)]
        [TestCase(QuickAccessBarUI.Destination.Pvp, KeyCode.F6)]
        [TestCase(QuickAccessBarUI.Destination.Story, KeyCode.J)]
        [TestCase(QuickAccessBarUI.Destination.Inventory, KeyCode.I)]
        [TestCase(QuickAccessBarUI.Destination.Badges, KeyCode.K)]
        [TestCase(QuickAccessBarUI.Destination.Island, KeyCode.H)]
        public void Hotkey_ExistingDestination_PreservesBinding(QuickAccessBarUI.Destination id, KeyCode expected)
        {
            Assert.AreEqual(expected, QuickAccessBarUI.GetHotkey(id));
            CollectionAssert.Contains(QuickAccessBarUI.Destinations, id);
        }

        [Test]
        public void Destinations_Menu_HasUniqueCompleteRoutes()
        {
            var ids = new HashSet<QuickAccessBarUI.Destination>(QuickAccessBarUI.Destinations);
            Assert.AreEqual(14, ids.Count);
            foreach (QuickAccessBarUI.Destination id in System.Enum.GetValues(typeof(QuickAccessBarUI.Destination)))
                if (id != QuickAccessBarUI.Destination.Menu) Assert.IsTrue(ids.Contains(id));
        }

        [Test]
        public void Navigation_DuplicateInputSameFrame_OpensMenuOnlyOnce()
        {
            var go = new GameObject("Navigation debounce test");
            var bar = go.AddComponent<QuickAccessBarUI>();
            MethodInfo navigate = typeof(QuickAccessBarUI).GetMethod("TryNavigate", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                navigate.Invoke(bar, new object[] { QuickAccessBarUI.Destination.Menu });
                navigate.Invoke(bar, new object[] { QuickAccessBarUI.Destination.Menu });
                Assert.IsTrue(bar.IsOpen, "Update and OnGUI must not cancel each other.");
                Assert.AreSame(bar, ModalUIRegistry.TopModal);
                typeof(QuickAccessBarUI).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bar, null);
                Assert.IsTrue(bar.IsOpen, "The menu registry must not count as a battle freeze.");
                bar.enabled = false;
                Assert.IsFalse(bar.IsOpen);
                Assert.AreNotSame(bar, ModalUIRegistry.TopModal);
            }
            finally
            {
                bar.CloseModal();
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Navigation_MenuToSettings_ReplacesModalAndBlocksUnrelatedModal()
        {
            var go = new GameObject("Navigation test");
            var bar = go.AddComponent<QuickAccessBarUI>();
            var settings = go.AddComponent<AccountSettingsUI>();
            bar.AutoWire(settings);
            MethodInfo navigate = typeof(QuickAccessBarUI).GetMethod("TryNavigate", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo frame = typeof(QuickAccessBarUI).GetField("lastToggleFrame", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                Assert.IsTrue((bool)navigate.Invoke(bar, new object[] { QuickAccessBarUI.Destination.Menu }));
                Assert.AreSame(bar, ModalUIRegistry.TopModal);
                frame.SetValue(bar, -1);
                Assert.IsTrue((bool)navigate.Invoke(bar, new object[] { QuickAccessBarUI.Destination.Settings }));
                Assert.IsFalse(bar.IsOpen);
                Assert.IsTrue(settings.IsOpen);
                Assert.AreSame(settings, ModalUIRegistry.TopModal);
                frame.SetValue(bar, -1);
                Assert.IsFalse((bool)navigate.Invoke(bar, new object[] { QuickAccessBarUI.Destination.Menu }));
                Assert.IsFalse(bar.IsOpen);
            }
            finally
            {
                bar.CloseModal();
                settings.CloseModal();
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
