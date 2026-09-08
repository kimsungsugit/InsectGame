#if UNITY_EDITOR
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 수문장 격파 판정의 <b>정체성 조건</b>.
    ///
    /// 회귀 고정 — 이 판정은 두 번 틀렸다.
    /// <list type="number">
    /// <item><b>종 ID + 레벨</b>: 리전 13곳 중 9곳은 수문장 종이 자기 리전 야생 풀에도 있고,
    /// 그중 8곳은 야생 스폰 상한이 옛 임계(<c>guardianLevel - 2</c>)를 넘었다. 야생을 이기면
    /// 리전이 열렸다.</item>
    /// <item><b>수문장 자리 15m 반경</b>: <c>InsectSpawner.RelocateSpawnPoints</c>가 현재 리전
    /// 스폰포인트를 플레이어로부터 10~43m 나선 위로 끌어오고 거기서 <c>SpawnPoint.radius</c>(5m)만큼
    /// 흩어지므로, 수문장 앞에 선 순간 야생이 <b>5m</b>까지 들어온다. 반경을 좁혀도 스폰 링이
    /// 플레이어를 따라오는 한 같은 결함이 남는다.</item>
    /// </list>
    ///
    /// 그래서 판정 근거를 <b>개체 표식</b>(<see cref="InsectEntity.GuardianRegionId"/>)으로 옮겼다.
    /// 실제 판정은 <c>BattleScreenUI</c>/<c>RaidBattleUI</c>가 하지만 둘 다 OnGUI 컴포넌트라 씬 없이
    /// 못 돈다 — 그 둘이 공유하는 급소인 <b>표식의 생성·전파·초기화</b>를 여기서 고정한다.
    /// </summary>
    [TestFixture]
    public class GuardianLocationTests
    {
        private const string UnlockKey = "InsectGame.UnlockedRegions";
        private const string GuardianKey = "InsectGame.DefeatedGuardians";

        private GameObject host;
        private RegionManager manager;
        private RegionData ruins;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(SaveScope.PrefsKey(UnlockKey));
            PlayerPrefs.DeleteKey(SaveScope.PrefsKey(GuardianKey));
            PlayerPrefs.DeleteKey(UnlockKey);
            PlayerPrefs.DeleteKey(GuardianKey);
            PlayerPrefs.Save();

            host = new GameObject("GuardianLocationTests");
            manager = host.AddComponent<RegionManager>();

            // 유적을 본떴다 — 야생 상한 50, 수문장 Lv42(옛 임계 40)라 겹침이 실제로 일어나는 리전.
            ruins = new RegionData();
            ruins.regionId = "ruins";
            ruins.displayName = "잊힌 유적";
            ruins.centerPosition = new Vector3(300f, 0f, 300f);
            ruins.radius = 40f;
            ruins.guardianInsectId = "scarab_pharaoh";
            ruins.guardianDisplayName = "유적의 파수꾼";
            ruins.guardianLevel = 42;

            manager.Initialize(new[] { ruins });
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            ruins = null;   // RegionData는 일반 클래스라 파기 대상이 아니다
            PlayerPrefs.DeleteKey(SaveScope.PrefsKey(UnlockKey));
            PlayerPrefs.DeleteKey(SaveScope.PrefsKey(GuardianKey));
            PlayerPrefs.Save();
        }

        private static InsectEntity NewEntity(string name)
        {
            return new GameObject(name).AddComponent<InsectEntity>();
        }

        [Test]
        public void GuardianSpot_IsInsideTheRegion()
        {
            Assert.LessOrEqual(
                Vector3.Distance(manager.GetGuardianPosition(ruins), ruins.centerPosition), ruins.radius,
                "수문장이 리전 밖에 서면 아무리 둘러봐도 안 보인다");
        }

        /// <summary>
        /// <b>이 테스트가 결함의 핵심이다.</b> 야생 개체는 수문장 자리 바로 옆에 스폰돼도
        /// 표식이 없다 — 좌표가 아니라 표식이 답한다.
        /// </summary>
        [Test]
        public void WildEntity_AtTheGuardianSpot_IsNotMarked()
        {
            InsectEntity wild = NewEntity("Wild");
            try
            {
                // 스폰 링이 플레이어를 따라와 실제로 일어나는 배치 — 수문장 자리와 겹쳐 세운다.
                wild.transform.position = manager.GetGuardianPosition(ruins);

                Assert.IsTrue(string.IsNullOrEmpty(wild.GuardianRegionId),
                    "야생이 수문장으로 잡히면 곤충을 잡다가 리전이 줄줄이 열린다");
            }
            finally { Object.DestroyImmediate(wild.gameObject); }
        }

        [Test]
        public void MarkedEntity_CarriesTheRegionId()
        {
            InsectEntity guardian = NewEntity("Guardian");
            try
            {
                guardian.MarkAsGuardian(ruins.regionId);

                Assert.AreEqual(ruins.regionId, guardian.GuardianRegionId,
                    "표식이 안 붙으면 진짜 수문장을 이겨도 리전이 안 열린다");
            }
            finally { Object.DestroyImmediate(guardian.gameObject); }
        }

        /// <summary>
        /// 표식이 <b>풀 재사용으로 새지 않는지</b>. <c>Initialize</c>는 야생 스폰 경로다 —
        /// 직전 개체의 표식을 물려받으면 고치려던 결함이 그대로 돌아온다.
        /// </summary>
        [Test]
        public void Initialize_ClearsTheGuardianMark()
        {
            InsectEntity reused = NewEntity("Reused");
            try
            {
                reused.MarkAsGuardian(ruins.regionId);
                reused.Initialize(null, 7, null, null);

                Assert.IsTrue(string.IsNullOrEmpty(reused.GuardianRegionId),
                    "풀에서 나온 개체가 표식을 물려받으면 야생 승리가 리전을 연다");
            }
            finally { Object.DestroyImmediate(reused.gameObject); }
        }

        /// <summary>
        /// <c>BuildForBattle</c>도 표식을 지운다 — 그래서 <c>SpawnGuardianInsect</c>는
        /// <b>빌드 뒤에</b> 표식을 붙인다. 순서가 뒤집히면 조용히 안 붙는다.
        /// </summary>
        [Test]
        public void BuildForBattle_ClearsTheGuardianMark()
        {
            InsectEntity preview = NewEntity("Preview");
            try
            {
                preview.MarkAsGuardian(ruins.regionId);
                preview.BuildForBattle(null, 42, false);

                Assert.IsTrue(string.IsNullOrEmpty(preview.GuardianRegionId),
                    "도감 프리뷰 등 전투용 빌드가 표식을 들고 있으면 안 된다");
            }
            finally { Object.DestroyImmediate(preview.gameObject); }
        }

        /// <summary>
        /// <b>표식 없이는 격파가 성립하지 않으므로, 수문장은 반드시 걸 수 있어야 한다.</b>
        /// <c>forBattle</c>(정적 개체 표시)이 <c>CanBeEngaged</c>를 막아서 수문장이 조용히
        /// 장식물이 돼 있었다 — 접근 판정 3곳이 전부 이 프로퍼티로 거른다. 그 상태로 격파
        /// 판정만 정체성으로 바꾸면 <b>진행이 영구 정지한다</b>.
        /// </summary>
        [Test]
        public void Guardian_BuiltForBattle_IsStillEngageable()
        {
            InsectEntity guardian = NewEntity("Guardian");
            try
            {
                guardian.BuildForBattle(null, 42, false);   // forBattle = true
                guardian.MarkAsGuardian(ruins.regionId);

                Assert.IsTrue(guardian.CanBeEngaged,
                    "수문장에게 말을 걸 수 없으면 정체성 판정이 영영 안 선다 — 진행 정지");
            }
            finally { Object.DestroyImmediate(guardian.gameObject); }
        }

        /// <summary>
        /// 반대쪽도 지킨다 — 아레나 전시용 개체(표식 없는 <c>forBattle</c>)는 여전히 못 건다.
        /// </summary>
        [Test]
        public void ArenaDisplay_WithoutMark_StaysUnengageable()
        {
            InsectEntity display = NewEntity("ArenaDisplay");
            try
            {
                display.BuildForBattle(null, 10, false);

                Assert.IsFalse(display.CanBeEngaged,
                    "전투 아레나 전시 개체까지 걸리면 엉뚱한 상대와 전투가 시작된다");
            }
            finally { Object.DestroyImmediate(display.gameObject); }
        }

        /// <summary>
        /// 수문장은 풀 객체가 아니라 <c>Despawn</c>이 아무것도 반환·파괴하지 못한다. 옛은 그런데도
        /// <c>despawnedThisCycle</c> 래치만 걸려, **한 번 지거나 도주하면** 눈앞의 수문장에게 다시
        /// 말을 걸 수 없어 리전이 영영 잠겼다(2026-09-09). 전투 종료가 부르는 그 순서 그대로 검증한다.
        /// </summary>
        [Test]
        public void Guardian_AfterLossAndDespawn_IsEngageableAgain()
        {
            InsectEntity guardian = NewEntity("Guardian");
            try
            {
                guardian.BuildForBattle(null, 42, false);
                guardian.MarkAsGuardian(ruins.regionId);
                guardian.SetEngaged(true);      // StartBattle
                guardian.SetEngaged(false);     // ReleaseEnemyAfterBattle
                guardian.Despawn();

                Assert.IsTrue(guardian.CanBeEngaged, "패배 뒤 재도전이 막히면 리전이 영영 잠긴다");
                Assert.IsTrue(guardian.IsGuardian, "표식은 격파 전까지 남아야 한다");
            }
            finally { Object.DestroyImmediate(guardian.gameObject); }
        }

        [Test]
        public void TryDefeatGuardian_WildOrRepeated_ReturnsFalse()
        {
            Assert.IsFalse(manager.TryDefeatGuardian("", "test"), "빈 ID는 야생이다");
            Assert.IsTrue(manager.TryDefeatGuardian(ruins.regionId, "test"));
            Assert.IsFalse(manager.TryDefeatGuardian(ruins.regionId, "test"), "이미 깬 수문장은 두 번 처리하지 않는다");
        }

        [Test]
        public void DefeatGuardian_IsIdempotent()
        {
            manager.DefeatGuardian(ruins.regionId);
            Assert.IsTrue(manager.IsGuardianDefeated(ruins.regionId));

            // 판정부가 IsGuardianDefeated로 먼저 걸러내지만, 매니저 쪽도 스스로 막아야 한다.
            manager.DefeatGuardian(ruins.regionId);
            Assert.IsTrue(manager.IsGuardianDefeated(ruins.regionId),
                "격파가 두 번 처리되면 해금·저장이 중복된다");
        }
    }
}
#endif
