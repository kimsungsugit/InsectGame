#if UNITY_EDITOR
using InsectGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 오염 리전의 스폰 억제·지면 탈색 계산. 렌더 결과는 기기 확인 대상이고,
    /// 여기서는 <b>진행을 막지 않는가</b>와 <b>명암이 남는가</b>만 고정한다 —
    /// 그 둘이 어긋나면 조용히 캠페인이 멈추거나 땅이 통째로 안 보인다.
    /// </summary>
    [TestFixture]
    public class BlightPolicyTests
    {
        private const float Delta = 0.0001f;

        // ── 스폰 하한 — 여기가 급소다 ──

        /// <summary>
        /// 오염이 스폰을 0으로 만들면 그 리전의 포획·전투 비트가 영영 발화하지 못한다.
        /// <b>지키려는 건 "0이 아님"이고 <see cref="BlightPolicy.MinActive"/>는 그 수단이다.</b>
        ///
        /// 그 둘을 같은 것으로 적었다가 어긋났다. 예전 판은 어떤 기본값에서도 <c>MinActive</c>
        /// 이상을 요구했는데, <c>baseMax=1</c>이면 그건 <b>멀쩡한 리전(1)보다 오염 리전(2)에
        /// 곤충이 많다</b>는 뜻이 된다 — 줄이는 정책이 늘리는 정책이 되고, 그 상태로 통과했다.
        ///
        /// 하한은 리전이 허용하는 만큼까지다: <c>min(MinActive, baseMax)</c>.
        /// 절대 불변식(<b>1 이상</b>)은 그대로 못 박는다 — 캠페인 정지는 거기서 난다.
        /// 도달 가능한 값이라 1도 함께 본다(<c>GameplayTuningProfile</c>이 <c>[Range(1, 15)]</c>).
        /// </summary>
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(5)]
        [TestCase(10)]
        [TestCase(30)]
        public void MaxActiveFor_Blighted_NeverDropsBelowMinActive(int baseMax)
        {
            int actual = BlightPolicy.MaxActiveFor(true, baseMax);
            int floor = Mathf.Min(BlightPolicy.MinActive, baseMax);
            Assert.GreaterOrEqual(actual, floor,
                $"기본 {baseMax}에서 오염 상한이 {actual} — 하한 {floor} 미만이면 캠페인이 멈춘다");
            Assert.GreaterOrEqual(actual, 1,
                $"기본 {baseMax}에서 오염 상한이 {actual} — 0이면 그 리전 비트가 영영 발화 못 한다");
        }

        [Test]
        public void MinActive_IsAtLeastOne()
        {
            // 0이면 위 테스트가 통과하면서도 스폰이 완전히 멈춘다 — 하한 자체를 고정한다.
            Assert.GreaterOrEqual(BlightPolicy.MinActive, 1);
        }

        [Test]
        public void MaxActiveFor_Clean_ReturnsBaseUnchanged()
        {
            Assert.AreEqual(10, BlightPolicy.MaxActiveFor(false, 10));
            Assert.AreEqual(3, BlightPolicy.MaxActiveFor(false, 3));
        }

        [Test]
        public void MaxActiveFor_Blighted_IsScarcerThanClean()
        {
            // 줄어들지 않으면 오염이 게임플레이로 드러나지 않는다.
            Assert.Less(BlightPolicy.MaxActiveFor(true, 10), BlightPolicy.MaxActiveFor(false, 10));
        }

        /// <summary>
        /// <b>오염이 곤충을 늘려서는 안 된다 — 정의역 전체에서.</b>
        ///
        /// 이 검사가 <c>baseMax=10</c> 하나만 보던 동안 <c>baseMax=1</c>이 뒤집혀 있었다:
        /// <c>Mathf.Max(MinActive, 1/3)</c>이 2라, 오염된 리전이 멀쩡한 리전(1)보다
        /// **곤충이 많았다.** 하한을 보장 수량으로 쓴 탓이다.
        ///
        /// 범위는 <c>GameplayTuningProfile.maxActivePerRegion</c>의 <c>[Range(1, 15)]</c>다 —
        /// 인스펙터에서 넣을 수 있는 값 전부가 정의역이므로 전부 본다.
        /// </summary>
        [Test]
        public void MaxActiveFor_Blighted_NeverExceedsClean_AcrossTuningRange()
        {
            for (int baseMax = 1; baseMax <= 15; baseMax++)
            {
                int clean = BlightPolicy.MaxActiveFor(false, baseMax);
                int blighted = BlightPolicy.MaxActiveFor(true, baseMax);
                Assert.LessOrEqual(blighted, clean,
                    $"기본 {baseMax}에서 오염 상한이 {blighted} — 멀쩡한 리전({clean})보다 많다");
                Assert.GreaterOrEqual(blighted, 1,
                    $"기본 {baseMax}에서 오염 상한이 {blighted} — 0이면 그 리전 비트가 영영 발화 못 한다");
            }
        }

        /// <summary>
        /// 스포너는 maxActivePerRegion &lt;= 0을 "리전 상한 없음"으로 쓴다(InsectSpawner:409).
        /// 그 의미를 오염이 뒤집으면 안 된다 — 갑자기 상한 2가 생겨 전 리전이 조인다.
        /// </summary>
        [TestCase(0)]
        [TestCase(-1)]
        public void MaxActiveFor_NonPositiveBase_IsPassedThrough(int baseMax)
        {
            Assert.AreEqual(baseMax, BlightPolicy.MaxActiveFor(true, baseMax));
            Assert.AreEqual(baseMax, BlightPolicy.MaxActiveFor(false, baseMax));
        }

        // ── 지면 탈색 ──

        [Test]
        public void TintOf_ZeroAmount_LeavesColorUntouched()
        {
            Color src = new Color(0.5f, 0.45f, 0.4f);
            Color got = BlightPolicy.TintOf(src, 0f);
            Assert.AreEqual(src.r, got.r, Delta);
            Assert.AreEqual(src.g, got.g, Delta);
            Assert.AreEqual(src.b, got.b, Delta);
        }

        /// <summary>
        /// 밝은 땅과 어두운 땅이 탈색 후에도 밝기 순서를 유지해야 지형의 굴곡이 읽힌다.
        /// 한 색으로 뭉개면 "죽은 땅"이 아니라 "안 그려진 땅"이 된다.
        /// </summary>
        [Test]
        public void TintOf_PreservesRelativeLuminance()
        {
            Color bright = BlightPolicy.TintOf(new Color(0.8f, 0.85f, 0.7f));
            Color dark = BlightPolicy.TintOf(new Color(0.2f, 0.25f, 0.15f));
            Assert.Greater(Lum(bright), Lum(dark), "탈색 후 밝기 순서가 뒤집혔다");
        }

        /// <summary>탈색은 채도를 낮춘다 — 안 낮추면 화면에서 오염이 안 보인다.</summary>
        [Test]
        public void TintOf_ReducesSaturation()
        {
            Color src = new Color(0.15f, 0.75f, 0.25f);   // 짙은 초록 — 초원/숲 계열
            Color got = BlightPolicy.TintOf(src);
            Assert.Less(Spread(got), Spread(src), "탈색 후에도 색조가 그대로다");
        }

        /// <summary>
        /// **이미 무채색인 지면에서도 변화가 보여야 한다.**
        ///
        /// 파일럿 리전인 산의 지면이 (0.35, 0.325, 0.24)인 무채색 갈색이라, 채도만 빼던 옛
        /// 공식은 그걸 (0.340, 0.319, 0.267)로 만들었다 — 사실상 그대로였고 파랑은 오히려
        /// 올라갔다. 배치모드 캡처로 오염/정화를 나란히 찍었더니 두 장이 구분되지 않았다.
        /// 밝기 저하가 그 구멍을 막는다.
        /// </summary>
        [Test]
        public void TintOf_AlreadyDesaturatedGround_StillDarkensVisibly()
        {
            Color mountainGround = new Color(0.35f, 0.325f, 0.24f);   // 산 지면의 실제 색
            Color got = BlightPolicy.TintOf(mountainGround);

            Assert.Less(Lum(got), Lum(mountainGround) * 0.85f,
                $"탈색 후 밝기가 {Lum(got):F3} — 원본 {Lum(mountainGround):F3}의 85% 미만이어야 눈에 띈다");
            Assert.Less(got.b, mountainGround.b, "파랑이 올라가면 오히려 더 파래 보인다");
        }

        /// <summary>탈색은 밝기를 낮춘다 — 어느 원색에서든.</summary>
        [TestCase(0.35f, 0.325f, 0.24f)]   // 산 (무채색 갈색)
        [TestCase(0.2f, 0.4f, 0.18f)]      // 숲 (초록)
        [TestCase(0.3f, 0.275f, 0.2f)]     // 유적
        [TestCase(0.8f, 0.85f, 0.7f)]      // 밝은 지면
        public void TintOf_AlwaysDarkens(float r, float g, float b)
        {
            Color src = new Color(r, g, b);
            Assert.Less(Lum(BlightPolicy.TintOf(src)), Lum(src));
        }

        /// <summary>알파는 건드리지 않는다 — 반투명 지형/오버레이가 통째로 불투명해진다.</summary>
        [Test]
        public void TintOf_KeepsAlpha()
        {
            Color src = new Color(0.5f, 0.5f, 0.5f, 0.35f);
            Assert.AreEqual(0.35f, BlightPolicy.TintOf(src).a, Delta);
        }

        /// <summary>강도를 0~1 밖으로 줘도 안전해야 한다(호출부가 보간값을 그대로 넘긴다).</summary>
        [TestCase(-0.5f)]
        [TestCase(1.5f)]
        public void TintOf_AmountOutOfRange_IsClamped(float amount)
        {
            Color got = BlightPolicy.TintOf(new Color(0.5f, 0.45f, 0.4f), amount);
            Assert.IsTrue(got.r >= 0f && got.r <= 1f, $"r={got.r}");
            Assert.IsTrue(got.g >= 0f && got.g <= 1f, $"g={got.g}");
            Assert.IsTrue(got.b >= 0f && got.b <= 1f, $"b={got.b}");
        }

        private static float Lum(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;

        /// <summary>채도 대용 — 최대 채널과 최소 채널의 차.</summary>
        private static float Spread(Color c) =>
            Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b));
    }
}
#endif
