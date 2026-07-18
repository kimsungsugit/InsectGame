#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using UnityEngine;
using InsectGame.Core;
using InsectGame.Dex;

namespace InsectGame.Tests
{
    /// <summary>
    /// 코인 첫 발견 보상의 무한 지급 방지 회귀 테스트.
    ///
    /// 코인은 데드 화폐였다가 Dex 첫 발견 보상 + 배틀 트리클로 부활했다. 첫 발견 보상은
    /// GetOrCreateRecord의 lookup 미스(레코드 최초 생성)에만 지급돼야 한다 — 재발견이나
    /// 디스크 로드 때 재지급되면 무한 코인 인플레가 된다. 이 불변식을 코드 레벨에서 못박는다.
    ///
    /// 디바운스 우회: 같은 곤충을 RegisterEncounter로 연속 호출하면 디바운스(0.1s)가 차단해
    /// lookup까지 못 간다. 대신 encounter 후 RegisterCapture로 재호출한다 — 둘은 별도 타이머
    /// (lastEncounterTime/lastCaptureTime)라 시간 대기 없이 lookup 히트(재지급 방지)를 검증한다.
    /// [Test] 동기로 유지하는 이유: asmdef가 없어 [UnityTest](UnityEngine.TestRunner 의존)는
    /// Assembly-CSharp에서 참조 불가.
    ///
    /// 파일 IO: DexController/PlayerCurrencyWallet이 SaveScope 경로에 세이브를 쓴다.
    /// SetUp에서 실제 세이브를 백업하고 TearDown에서 복원해 개발 세이브를 보호한다.
    /// </summary>
    [TestFixture]
    public class DexCoinRewardTests
    {
        private static readonly string[] SaveFiles = { "player_currency.json", "dex_save.json" };
        private GameObject go;

        [SetUp]
        public void SetUp()
        {
            // 실제 세이브 보호 — 백업 후 격리된 빈 상태에서 테스트한다.
            foreach (string f in SaveFiles)
            {
                string path = SaveScope.FilePath(f);
                if (File.Exists(path)) File.Move(path, path + ".testbak");
            }
            go = new GameObject("DexCoinTest");
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) Object.DestroyImmediate(go);
            // 테스트가 만든 파일 삭제 + 백업 복원.
            foreach (string f in SaveFiles)
            {
                string path = SaveScope.FilePath(f);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".testbak")) File.Move(path + ".testbak", path);
            }
        }

        [Test]
        public void FirstDiscovery_GrantsCoinOnce_NotOnRediscoveryOrReload()
        {
            const string insectId = "beetle_basic"; // InsectLore.json: rewardCoins = 5
            const int reward = 5;

            PlayerCurrencyWallet wallet = go.AddComponent<PlayerCurrencyWallet>();
            DexController dex = go.AddComponent<DexController>();
            dex.AutoWire(wallet);

            int baseCoins = wallet.Coins;

            // 첫 발견(encounter) → 코인 1회 지급
            dex.RegisterEncounter(insectId);
            Assert.AreEqual(baseCoins + reward, wallet.Coins,
                "첫 발견 시 코인이 지급되지 않았다");

            // 재발견(capture — 별도 디바운스 타이머라 즉시 통과) → lookup 히트, 재지급 없음
            dex.RegisterCapture(insectId);
            Assert.AreEqual(baseCoins + reward, wallet.Coins,
                "재발견 시 코인이 또 지급됐다 — 무한 지급 버그");

            // 디스크 로드 → 재지급 없음 (records 순회로 lookup 채움, GetOrCreateRecord 우회)
            dex.ReloadFromDisk();
            Assert.AreEqual(baseCoins + reward, wallet.Coins,
                "ReloadFromDisk 시 코인이 재지급됐다");
        }

        [Test]
        public void DistinctInsects_EachGrantOnce()
        {
            PlayerCurrencyWallet wallet = go.AddComponent<PlayerCurrencyWallet>();
            DexController dex = go.AddComponent<DexController>();
            dex.AutoWire(wallet);

            int baseCoins = wallet.Coins;

            // 서로 다른 곤충은 lastEncounterTime이 별개라 디바운스 무관.
            dex.RegisterEncounter("beetle_basic");   // +5
            dex.RegisterEncounter("mantis_green");   // +12

            Assert.AreEqual(baseCoins + 5 + 12, wallet.Coins,
                "서로 다른 곤충의 첫 발견 보상이 각각 지급되지 않았다");
        }

        [Test]
        public void NoWallet_DoesNotThrow()
        {
            // wallet 미연결(AutoWire 안 함)이어도 GrantFirstDiscoveryReward의 null 가드로
            // 예외 없이 통과해야 한다.
            DexController dex = go.AddComponent<DexController>();
            dex.RegisterEncounter("beetle_basic");
            Assert.IsTrue(dex.IsDiscovered("beetle_basic"),
                "wallet 없이도 발견 기록은 남아야 한다");
        }
    }
}
#endif
