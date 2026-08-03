#if UNITY_EDITOR
using NUnit.Framework;
using InsectGame.Core;
using InsectGame.Data;

namespace InsectGame.Tests
{
    [TestFixture]
    public class GameConstantsTests
    {
        [Test]
        public void SceneNames_PlayScene_IsNotEmpty()
        {
            Assert.IsFalse(string.IsNullOrEmpty(GameConstants.Scenes.Play));
        }

        [Test]
        public void SceneNames_MainMenu_IsNotEmpty()
        {
            Assert.IsFalse(string.IsNullOrEmpty(GameConstants.Scenes.MainMenu));
        }

        [Test]
        public void SceneNames_OpeningScene_IsNotEmpty()
        {
            Assert.IsFalse(string.IsNullOrEmpty(GameConstants.Scenes.Opening));
        }

        [Test]
        public void SaveFiles_AllNotEmpty()
        {
            Assert.IsFalse(string.IsNullOrEmpty(GameConstants.SaveFiles.PlayerProgress));
            Assert.IsFalse(string.IsNullOrEmpty(GameConstants.SaveFiles.PlayerInsects));
            Assert.IsFalse(string.IsNullOrEmpty(GameConstants.SaveFiles.PlayerCandies));
            Assert.IsFalse(string.IsNullOrEmpty(GameConstants.SaveFiles.PlayerCurrency));
            Assert.IsFalse(string.IsNullOrEmpty(GameConstants.SaveFiles.PlayerItems));
            Assert.IsFalse(string.IsNullOrEmpty(GameConstants.SaveFiles.BattleTeam));
            Assert.IsFalse(string.IsNullOrEmpty(GameConstants.SaveFiles.DexSave));
            Assert.IsFalse(string.IsNullOrEmpty(GameConstants.SaveFiles.StoryProgress));
        }

        [Test]
        public void SaveFiles_AllEndWithJson()
        {
            Assert.IsTrue(GameConstants.SaveFiles.PlayerProgress.EndsWith(".json"));
            Assert.IsTrue(GameConstants.SaveFiles.PlayerInsects.EndsWith(".json"));
            Assert.IsTrue(GameConstants.SaveFiles.BattleTeam.EndsWith(".json"));
            Assert.IsTrue(GameConstants.SaveFiles.DexSave.EndsWith(".json"));
            Assert.IsTrue(GameConstants.SaveFiles.StoryProgress.EndsWith(".json"));
        }

        [Test]
        public void Player_MaxIV_Is15()
        {
            Assert.AreEqual(15, GameConstants.Player.MaxIV);
        }

        [Test]
        public void Player_MaxEquipSlots_Is4()
        {
            Assert.AreEqual(4, GameConstants.Player.MaxEquipSlots);
        }

        [Test]
        public void Player_MaxLearnedSkills_Is6()
        {
            Assert.AreEqual(6, GameConstants.Player.MaxLearnedSkills);
        }

        [Test]
        public void TypeChart_LeafIsStrongAgainstWater()
        {
            Assert.AreEqual(1.5f,
                InsectTypeChart.GetEffectiveness(InsectElement.Leaf, InsectElement.Water, InsectElement.None),
                0.001f);
        }

        [Test]
        public void TypeChart_WaterResistsMetalAttack()
        {
            Assert.Less(
                InsectTypeChart.GetEffectiveness(InsectElement.Metal, InsectElement.Water, InsectElement.None),
                1f);
        }

        [Test]
        public void PlayerInsect_SeventhSkillRequiresReplacement()
        {
            PlayerInsectData insect = new PlayerInsectData();
            Assert.IsTrue(insect.LearnSkill("s1"));
            Assert.IsTrue(insect.LearnSkill("s2"));
            Assert.IsTrue(insect.LearnSkill("s3"));
            Assert.IsTrue(insect.LearnSkill("s4"));
            Assert.IsTrue(insect.LearnSkill("s5"));
            Assert.IsTrue(insect.LearnSkill("s6"));
            Assert.IsFalse(insect.LearnSkill("s7"));
            Assert.IsTrue(insect.ReplaceSkill("s1", "s7"));
            Assert.IsTrue(insect.HasLearnedSkill("s7"));
            Assert.IsFalse(insect.HasLearnedSkill("s1"));
        }

        [Test]
        public void Battle_MaxTeamSlots_Is5()
        {
            Assert.AreEqual(5, GameConstants.Battle.MaxTeamSlots);
        }

        [Test]
        public void Battle_UniteGaugeMax_Is100()
        {
            Assert.AreEqual(100f, GameConstants.Battle.UniteGaugeMax);
        }

        [Test]
        public void Leveling_FallbackMaxLevel_IsPositive()
        {
            Assert.Greater(GameConstants.Leveling.FallbackMaxLevel, 0);
        }

        [Test]
        public void Defaults_MasterVolume_InRange()
        {
            Assert.GreaterOrEqual(GameConstants.Defaults.MasterVolume, 0f);
            Assert.LessOrEqual(GameConstants.Defaults.MasterVolume, 1f);
        }
    }
}
#endif
