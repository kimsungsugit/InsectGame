#if UNITY_EDITOR
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 개체 크기·무게 계산. 수집·주간 대결 전용 축이라 전투 스탯과 독립이다.
    /// 구세이브(sizeRoll -1) backfill이 결정적인지가 핵심 — 볼 때마다 크기가 바뀌면 안 된다.
    /// </summary>
    [TestFixture]
    public class InsectSizeCalculatorTests
    {
        private InsectData species;

        [SetUp]
        public void SetUp()
        {
            species = ScriptableObject.CreateInstance<InsectData>();
            species.insectId = "test_beetle";
            species.displayName = "테스트 사슴벌레";
            species.baseSizeMm = 40f;
            species.baseWeightG = 5f;
        }

        [TearDown]
        public void TearDown()
        {
            if (species != null) Object.DestroyImmediate(species);
        }

        private static PlayerInsectData Pid(int roll, string instanceId = "abc123")
        {
            return new PlayerInsectData { instanceId = instanceId, insectId = "test_beetle", sizeRoll = roll };
        }

        [Test]
        public void ScaleFor_RollBoundaries_MapToScaleRange()
        {
            Assert.AreEqual(InsectSizeCalculator.MinScale,
                InsectSizeCalculator.ScaleFor(InsectSizeCalculator.MinRoll), 0.0001f);
            Assert.AreEqual(InsectSizeCalculator.MaxScale,
                InsectSizeCalculator.ScaleFor(InsectSizeCalculator.MaxRoll), 0.0001f);
            Assert.AreEqual(1f, InsectSizeCalculator.ScaleFor(50), 0.0001f);
        }

        [Test]
        public void ScaleFor_OutOfRangeRoll_Clamps()
        {
            Assert.AreEqual(InsectSizeCalculator.MinScale, InsectSizeCalculator.ScaleFor(-500), 0.0001f);
            Assert.AreEqual(InsectSizeCalculator.MaxScale, InsectSizeCalculator.ScaleFor(9999), 0.0001f);
        }

        [Test]
        public void SizeMm_MidRoll_EqualsSpeciesBase()
        {
            Assert.AreEqual(40f, InsectSizeCalculator.SizeMm(species, Pid(50)), 0.0001f);
        }

        [Test]
        public void SizeMm_IsMonotonicInRoll()
        {
            float previous = float.MinValue;
            for (int roll = 0; roll <= 100; roll += 10)
            {
                float mm = InsectSizeCalculator.SizeMm(species, Pid(roll));
                Assert.Greater(mm, previous, $"roll {roll}에서 크기가 줄었다");
                previous = mm;
            }
        }

        [Test]
        public void WeightG_GrowsFasterThanLength()
        {
            // 부피는 길이의 세제곱 — 길이가 1.25배면 무게는 약 1.95배여야 한다.
            float lengthRatio = InsectSizeCalculator.SizeMm(species, Pid(100))
                              / InsectSizeCalculator.SizeMm(species, Pid(50));
            float weightRatio = InsectSizeCalculator.WeightG(species, Pid(100))
                              / InsectSizeCalculator.WeightG(species, Pid(50));

            Assert.AreEqual(lengthRatio * lengthRatio * lengthRatio, weightRatio, 0.001f);
            Assert.Greater(weightRatio, lengthRatio, "무게가 길이보다 빠르게 늘어야 한다");
        }

        [Test]
        public void EffectiveRoll_LegacySaveSentinel_BackfillsDeterministically()
        {
            // ★ 회귀 고정 — sizeRoll -1을 그대로 쓰면 구세이브 곤충이 전부 최소 크기가 되고,
            // 매번 새로 롤하면 볼 때마다 크기가 바뀐다.
            PlayerInsectData legacy = Pid(-1, "9f3a1c7d55");

            int first = InsectSizeCalculator.EffectiveRoll(legacy);
            int second = InsectSizeCalculator.EffectiveRoll(legacy);

            Assert.AreEqual(first, second, "같은 개체가 호출마다 다른 크기를 냈다");
            Assert.GreaterOrEqual(first, InsectSizeCalculator.MinRoll);
            Assert.LessOrEqual(first, InsectSizeCalculator.MaxRoll);
        }

        [Test]
        public void RollFromInstanceId_DifferentIds_SpreadAcrossRange()
        {
            // 해시가 한쪽으로 몰리면 구세이브 곤충이 전부 비슷한 크기가 된다.
            int min = int.MaxValue, max = int.MinValue;
            for (int i = 0; i < 200; i++)
            {
                int roll = InsectSizeCalculator.RollFromInstanceId("instance_" + i);
                Assert.GreaterOrEqual(roll, InsectSizeCalculator.MinRoll);
                Assert.LessOrEqual(roll, InsectSizeCalculator.MaxRoll);
                if (roll < min) min = roll;
                if (roll > max) max = roll;
            }

            Assert.Less(min, 20, "낮은 롤이 하나도 안 나왔다");
            Assert.Greater(max, 80, "높은 롤이 하나도 안 나왔다");
        }

        [Test]
        public void RollFromInstanceId_EmptyOrNull_ReturnsMidpoint()
        {
            Assert.AreEqual(50, InsectSizeCalculator.RollFromInstanceId(null));
            Assert.AreEqual(50, InsectSizeCalculator.RollFromInstanceId(string.Empty));
        }

        [Test]
        public void SizeRatio_TracksScale()
        {
            Assert.AreEqual(InsectSizeCalculator.MinScale,
                InsectSizeCalculator.SizeRatio(species, Pid(0)), 0.0001f);
            Assert.AreEqual(1f, InsectSizeCalculator.SizeRatio(species, Pid(50)), 0.0001f);
            Assert.AreEqual(InsectSizeCalculator.MaxScale,
                InsectSizeCalculator.SizeRatio(species, Pid(100)), 0.0001f);
        }

        [Test]
        public void NullData_ReturnsZeroWithoutThrowing()
        {
            Assert.AreEqual(0f, InsectSizeCalculator.SizeMm(null, Pid(50)));
            Assert.AreEqual(0f, InsectSizeCalculator.WeightG(null, Pid(50)));
            Assert.AreEqual(string.Empty, InsectSizeCalculator.Summary(null, Pid(50)));
        }

        [Test]
        public void NullPid_UsesMidpointRoll()
        {
            // 야생 개체 미리보기처럼 pid가 없는 경로도 종 표준값을 그대로 보여줘야 한다.
            Assert.AreEqual(40f, InsectSizeCalculator.SizeMm(species, null), 0.0001f);
        }

        [Test]
        public void Labels_FormatByMagnitude()
        {
            Assert.AreEqual("71.8mm", InsectSizeCalculator.SizeLabel(71.84f));
            Assert.AreEqual("120mm", InsectSizeCalculator.SizeLabel(120.4f));
            Assert.AreEqual("4.20g", InsectSizeCalculator.WeightLabel(4.2f));
            Assert.AreEqual("42.0g", InsectSizeCalculator.WeightLabel(42f));
            Assert.AreEqual("1.50kg", InsectSizeCalculator.WeightLabel(1500f));
        }

        [Test]
        public void EnsureSize_FillsSentinelOnce()
        {
            PlayerInsectData legacy = Pid(-1, "deadbeef");
            legacy.EnsureSize();

            int filled = legacy.sizeRoll;
            Assert.GreaterOrEqual(filled, InsectSizeCalculator.MinRoll);

            legacy.EnsureSize();
            Assert.AreEqual(filled, legacy.sizeRoll, "이미 채워진 롤을 다시 굴렸다");
        }
    }
}
#endif
