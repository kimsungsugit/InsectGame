#if UNITY_EDITOR
using NUnit.Framework;
using InsectGame.Battle;
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

        /// <summary>
        /// 서포트 배율과 보스 HP 배율은 <b>짝으로 움직인다</b> — 서포트가 세지면 라운드 수를 지키려고
        /// 보스 HP도 같이 올라간다. 둘 중 하나만 고치면 레이드 길이가 조용히 바뀌므로 여기 함께 고정한다.
        /// </summary>
        [Test]
        public void Battle_RaidSupportSkillPowerMultiplier_IsBelowLeader()
        {
            Assert.Greater(GameConstants.Battle.RaidSupportSkillPowerMultiplier, 0f);
            Assert.Less(GameConstants.Battle.RaidSupportSkillPowerMultiplier, 1f,
                "리더 우위(1.0)를 넘으면 리더를 고르는 의미가 사라진다");
            Assert.Greater(GameConstants.Battle.RaidSupportSkillPowerMultiplier,
                RaidRoundResolver.SupportAssistPowerMultiplier,
                "스킬 폴백(기본 지원 공격)보다는 세야 스킬을 쓰는 보람이 있다");
        }

        [Test]
        public void Battle_RaidBossHpMultiplier_RaisedWithTeamFirepower()
        {
            Assert.AreEqual(8.5f, GameConstants.Battle.RaidBossHpMultiplier);
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
