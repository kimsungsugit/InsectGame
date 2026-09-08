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

        /// <summary>
        /// 보스 HP 배율은 <b>팀 화력에서 파생된 값</b>이지 독립 난이도 손잡이가 아니다.
        /// 전투 길이 3종(<c>LevelDamageScale</c>·<c>MaxAtkDefRatio</c>·<c>HpPerLevel</c>)을
        /// 바꾸면 이 값도 함께 다시 계산해야 한다 — 그 사실을 여기 고정한다.
        /// 8.5는 옛 수식(레벨항 ×2 · 공방비 2.5 · HP +3)의 값이었고, 지금 수식에서
        /// 그대로 두면 레이드가 두 배로 길어진다(6/5/4턴 → 10/9/8턴).
        /// </summary>
        [Test]
        public void Battle_RaidBossHpMultiplier_MatchesCurrentDamageMath()
        {
            Assert.AreEqual(4.5f, GameConstants.Battle.RaidBossHpMultiplier);
        }

        // ── 전투 길이 3종 ──
        //
        // 세 값은 서로를 상쇄하므로 **함께** 고정한다. 하나만 되돌리면 다른 쪽이 곧바로
        // 지배해 "양쪽이 한두 턴에 서로를 지우는" 옛 상태로 돌아간다.

        [Test]
        public void Battle_LevelDamageScale_IsBelowHpPerLevel()
        {
            Assert.Less(GameConstants.Battle.LevelDamageScale, GameConstants.Battle.HpPerLevel,
                "레벨당 데미지 증가가 HP 증가를 따라잡으면 레벨이 오를수록 전투가 짧아진다");
            Assert.GreaterOrEqual(GameConstants.Battle.LevelDamageScale, 1,
                "0이면 레벨을 올려도 기술 위력이 그대로라 성장이 안 느껴진다");
        }

        [Test]
        public void Battle_AtkDefRatio_BracketsOne()
        {
            Assert.Less(GameConstants.Battle.MinAtkDefRatio, 1f);
            Assert.Greater(GameConstants.Battle.MaxAtkDefRatio, 1f);
            Assert.LessOrEqual(GameConstants.Battle.MaxAtkDefRatio, 2f,
                "상한이 넓으면 스킬 위력을 아무리 낮춰도 스탯 차이만으로 한 방에 끝난다(옛 2.5)");
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
