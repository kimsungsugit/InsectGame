#if UNITY_EDITOR
using NUnit.Framework;
using InsectGame.Data;
using InsectGame.NPC;

namespace InsectGame.Tests
{
    [TestFixture]
    public class NpcCatchRulesTests
    {
        // 규칙 통과용 기준값: 플레이어가 곤충에서 충분히 먼 거리
        private const float FarFromPlayer = NpcCatchRules.PlayerClaimRadius + 5f;

        [Test]
        public void CanKidTarget_RareRarity_ReturnsFalse()
        {
            Assert.IsFalse(NpcCatchRules.CanKidTarget(
                InsectRarity.Rare, canBeEngaged: true, playerToInsectDistance: FarFromPlayer, reservedByOtherKid: false));
        }

        [Test]
        public void CanKidTarget_NotEngageable_ReturnsFalse()
        {
            Assert.IsFalse(NpcCatchRules.CanKidTarget(
                InsectRarity.Common, canBeEngaged: false, playerToInsectDistance: FarFromPlayer, reservedByOtherKid: false));
        }

        [Test]
        public void CanKidTarget_PlayerWithinClaimRadius_ReturnsFalse()
        {
            Assert.IsFalse(NpcCatchRules.CanKidTarget(
                InsectRarity.Common, canBeEngaged: true,
                playerToInsectDistance: NpcCatchRules.PlayerClaimRadius - 0.1f, reservedByOtherKid: false));
        }

        [Test]
        public void CanKidTarget_ReservedByOther_ReturnsFalse()
        {
            Assert.IsFalse(NpcCatchRules.CanKidTarget(
                InsectRarity.Common, canBeEngaged: true, playerToInsectDistance: FarFromPlayer, reservedByOtherKid: true));
        }

        [Test]
        public void CanKidTarget_CommonFreeInsect_ReturnsTrue()
        {
            Assert.IsTrue(NpcCatchRules.CanKidTarget(
                InsectRarity.Common, canBeEngaged: true, playerToInsectDistance: FarFromPlayer, reservedByOtherKid: false));
            Assert.IsTrue(NpcCatchRules.CanKidTarget(
                InsectRarity.Uncommon, canBeEngaged: true, playerToInsectDistance: FarFromPlayer, reservedByOtherKid: false));
        }

        [Test]
        public void ShouldWatchOnly_RarePlus_ReturnsTrue()
        {
            Assert.IsTrue(NpcCatchRules.ShouldWatchOnly(InsectRarity.Rare));
            Assert.IsTrue(NpcCatchRules.ShouldWatchOnly(InsectRarity.Epic));
            Assert.IsTrue(NpcCatchRules.ShouldWatchOnly(InsectRarity.Legendary));
        }

        [Test]
        public void ShouldWatchOnly_CommonUncommon_ReturnsFalse()
        {
            Assert.IsFalse(NpcCatchRules.ShouldWatchOnly(InsectRarity.Common));
            Assert.IsFalse(NpcCatchRules.ShouldWatchOnly(InsectRarity.Uncommon));
        }
    }
}
#endif
