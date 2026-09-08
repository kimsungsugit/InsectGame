#if UNITY_EDITOR
using System.Reflection;
using InsectGame.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace InsectGame.Tests
{
    [TestFixture]
    public class PlayerProgressUIControllerTests
    {
        private readonly System.Collections.Generic.List<GameObject> objects =
            new System.Collections.Generic.List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject obj in objects)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            objects.Clear();
        }

        [Test]
        public void Refresh_ViewsBoundAfterAutoWire_RendersCurrentValues()
        {
            PlayerProgressController progress = CreateComponent<PlayerProgressController>("Progress");
            SetField(progress, "data", new PlayerProgressData { level = 4, currentXp = 12 });

            PlayerCandyInventory candy = CreateComponent<PlayerCandyInventory>("Candy");
            SetField(candy, "data", new PlayerCandyData { candies = 23 });

            PlayerProgressUIController ui = CreateComponent<PlayerProgressUIController>("ProgressUI");
            ui.AutoWire(progress);
            ui.AutoWire(candy);

            Text levelText = CreateComponent<Text>("LevelText");
            Text xpText = CreateComponent<Text>("XpText");
            Text candyText = CreateComponent<Text>("CandyText");
            SetField(ui, "levelText", levelText);
            SetField(ui, "xpText", xpText);
            SetField(ui, "candyText", candyText);

            ui.Refresh();

            Assert.AreEqual("레벨 4 (12/95)", levelText.text);
            Assert.AreEqual("12/95", xpText.text);
            Assert.AreEqual("사탕 23", candyText.text);
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            GameObject obj = new GameObject(name);
            objects.Add(obj);
            return obj.AddComponent<T>();
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }
    }
}
#endif
