#if UNITY_EDITOR
using NUnit.Framework;
using InsectGame.Core;

namespace InsectGame.Tests
{
    // 지속 HP(전투 간 유지)의 센티넬 마이그레이션·클램프 로직 검증.
    [TestFixture]
    public class PersistentHpTests
    {
        [Test]
        public void GetEffectiveHp_Uninitialized_ReturnsFull()
        {
            var pid = new PlayerInsectData();          // currentHp 기본 -1(미초기화)
            Assert.AreEqual(-1, pid.currentHp);
            Assert.AreEqual(100, pid.GetEffectiveHp(100));   // 구세이브 = 풀피
        }

        [Test]
        public void GetEffectiveHp_Initialized_ClampsToMax()
        {
            var pid = new PlayerInsectData { currentHp = 40 };
            Assert.AreEqual(40, pid.GetEffectiveHp(100));    // 저장된 40 유지
            Assert.AreEqual(40, pid.GetEffectiveHp(80));     // 40 <= 80
            pid.currentHp = 200;
            Assert.AreEqual(100, pid.GetEffectiveHp(100));   // 상한 클램프
        }

        [Test]
        public void EnsureHp_Uninitialized_FillsToMax()
        {
            var pid = new PlayerInsectData();          // -1
            pid.EnsureHp(90);
            Assert.AreEqual(90, pid.currentHp);        // 풀피 확정
            Assert.IsFalse(pid.IsFainted);
        }

        [Test]
        public void EnsureHp_Initialized_ClampsButKeeps()
        {
            var pid = new PlayerInsectData { currentHp = 25 };
            pid.EnsureHp(90);
            Assert.AreEqual(25, pid.currentHp);        // 기존값 보존(전투서 깎인 HP 유지)
        }

        [Test]
        public void IsFainted_OnlyWhenZero()
        {
            var faint = new PlayerInsectData { currentHp = 0 };
            var alive = new PlayerInsectData { currentHp = 1 };
            var fresh = new PlayerInsectData();        // -1 미초기화는 기절 아님
            Assert.IsTrue(faint.IsFainted);
            Assert.IsFalse(alive.IsFainted);
            Assert.IsFalse(fresh.IsFainted);
        }

        // 상태 기본값(마이그레이션 무해)
        [Test]
        public void StatusFlags_DefaultFalse()
        {
            var pid = new PlayerInsectData();
            Assert.IsFalse(pid.isPoisoned);
            Assert.IsFalse(pid.isParalyzed);
        }
    }
}
#endif
