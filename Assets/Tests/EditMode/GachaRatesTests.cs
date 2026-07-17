#if UNITY_EDITOR
using NUnit.Framework;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 가챠 박스 등급 확률 검증. 이 영역은 오랫동안 테스트가 0개였고, 그 사이 골드 상자
    /// 전설 확률이 오타로 45%가 되어(임계값 {5,10,23,55}, 전설이 커먼의 9배) 방치됐다.
    /// data_lint는 "임계값 단조증가"만 봐서 통과시켰다 — 숫자가 정렬돼 있으면 서열이
    /// 뒤집혀도 못 잡았다. 이 테스트는 그 종류의 회귀를 코드 레벨에서 막는다.
    /// </summary>
    [TestFixture]
    public class GachaRatesTests
    {
        private static readonly string[] Boxes = { "box_bronze", "box_silver", "box_gold" };

        private GameObject go;
        private GachaBoxManager mgr;

        [SetUp]
        public void SetUp()
        {
            // Awake는 Instance = this 만 한다(부작용 없음). GetRates는 static 임계값만 읽어
            // AutoWire 없이도 동작한다.
            go = new GameObject("GachaTest");
            mgr = go.AddComponent<GachaBoxManager>();
        }

        [TearDown]
        public void TearDown()
        {
            // 파괴하면 다음 SetUp의 Awake에서 Instance가 fake-null로 취급돼 새로 잡힌다.
            Object.DestroyImmediate(go);
        }

        private float Pct(string box, InsectRarity rarity)
        {
            foreach (var e in mgr.GetRates(box))
            {
                if (e.rarity == rarity) return e.percent;
            }
            Assert.Fail($"{box}에 {rarity} 등급 없음");
            return -1f;
        }

        [Test]
        public void GachaRates_EachBox_SumsTo100()
        {
            foreach (var box in Boxes)
            {
                float sum = 0f;
                foreach (var e in mgr.GetRates(box)) sum += e.percent;
                Assert.AreEqual(100f, sum, 0.01f, $"{box} 등급 확률 합이 100%가 아님");
            }
        }

        // 서열 규칙: 최고 레어도(전설)가 차상위(에픽)보다 흔하면 안 된다.
        // 봉우리형은 허용한다 — 실버는 Rare 봉우리(L8 ≤ E22)라 정상이다. 골드 오타
        // {5,10,23,55}는 L45 > E32라 이 검사에서 걸린다.
        [Test]
        public void GachaRates_Legendary_NotMoreCommonThanEpic()
        {
            foreach (var box in Boxes)
            {
                float legendary = Pct(box, InsectRarity.Legendary);
                float epic = Pct(box, InsectRarity.Epic);
                Assert.LessOrEqual(legendary, epic,
                    $"{box}: 전설({legendary}%)이 에픽({epic}%)보다 흔하다 — 등급 서열 역전");
            }
        }

        // 전설 확률은 박스 등급이 오를수록 높아져야 한다 (브론즈 < 실버 < 골드).
        [Test]
        public void GachaRates_LegendaryChance_IncreasesWithBoxTier()
        {
            float bronze = Pct("box_bronze", InsectRarity.Legendary);
            float silver = Pct("box_silver", InsectRarity.Legendary);
            float gold = Pct("box_gold", InsectRarity.Legendary);
            Assert.Less(bronze, silver, "브론즈 전설 확률이 실버 이상");
            Assert.Less(silver, gold, "실버 전설 확률이 골드 이상");
        }

        // 각 박스의 모든 등급 확률은 음수가 아니어야 한다 (임계값 단조증가의 다른 표현).
        [Test]
        public void GachaRates_AllPercents_NonNegative()
        {
            foreach (var box in Boxes)
            {
                foreach (var e in mgr.GetRates(box))
                {
                    Assert.GreaterOrEqual(e.percent, 0f,
                        $"{box}의 {e.rarity} 확률이 음수 — 임계값이 단조증가가 아님");
                }
            }
        }
    }
}
#endif
