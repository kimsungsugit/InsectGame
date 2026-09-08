#if UNITY_EDITOR
using System.Reflection;
using InsectGame.Core;
using NUnit.Framework;

namespace InsectGame.Tests
{
    [TestFixture]
    public class SaveScopeTests
    {
        [Test]
        public void ScopedFiles_ContainsStoryProgress_ForMigrationAndCleanup()
        {
            FieldInfo field = typeof(SaveScope).GetField(
                "ScopedFiles",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(field);
            string[] scopedFiles = field.GetValue(null) as string[];
            Assert.IsNotNull(scopedFiles);
            CollectionAssert.Contains(scopedFiles, GameConstants.SaveFiles.StoryProgress);
        }

        [Test]
        public void MigrationVersion_StoryProgressAddition_IsAtLeastFour()
        {
            FieldInfo field = typeof(SaveScope).GetField(
                "MigrationVersion",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(field);
            int version = (int)field.GetRawConstantValue();
            Assert.GreaterOrEqual(version, 4);
        }
    }
}
#endif
