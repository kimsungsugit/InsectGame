#if UNITY_EDITOR
using System.Reflection;
using System.Threading.Tasks;
using InsectGame.Core;
using InsectGame.Spawning;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InsectGame.Tests
{
    /// <summary>
    /// 실제 PlayScene에서 섬을 드나든다 — 진짜 <c>RegionManager</c>·<c>SubAreaWorldBuilder</c>·<c>InsectSpawner</c>·
    /// <c>PlayerMovement</c>·<c>CameraFollower</c>가 붙은 상태다.
    ///
    /// 섬은 서브에리어 상태를 빌려 타므로(<c>rules/island.md</c>) 결함이 이음매에서 조용히 난다:
    /// 서브에리어 빌더가 빈 방을 짓거나, 25m 자동 이탈이 돌아 들어가자마자 쫓겨나거나, 끼임 복구가
    /// (2000,·,2000) 허공으로 보내거나, 나온 뒤 리전 판정이 얼어 있거나. 검수 화면(<c>island-ui</c>)은
    /// 가짜 무대라 이걸 못 본다.
    ///
    /// <b>디스크에 쓰지 않는다</b> — 테스트는 개발자 PC의 실제 저장 폴더에서 돈다. 섬 매니저의 저장을 끄고
    /// 재화를 건드리는 동작(구매·수확)은 부르지 않는다.
    /// </summary>
    [TestFixture]
    public class IslandIntegrationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        [Timeout(180000)]
        public async Task PlayScene_EnterWalkAndLeaveIsland_KeepsWorldStateConsistent()
        {
            int previousSceneHandle = SceneManager.GetActiveScene().handle;
            SceneManager.LoadScene(GameConstants.Scenes.Play, LoadSceneMode.Single);
            await WaitForBootAsync(previousSceneHandle);

            IslandManager island = Object.FindFirstObjectByType<IslandManager>();
            IslandWorldBuilder world = Object.FindFirstObjectByType<IslandWorldBuilder>();
            RegionManager regions = Object.FindFirstObjectByType<RegionManager>();
            SubAreaWorldBuilder subAreaWorld = Object.FindFirstObjectByType<SubAreaWorldBuilder>();
            PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
            InsectSpawner spawner = Object.FindFirstObjectByType<InsectSpawner>();
            Assert.IsNotNull(island, "IslandManager가 Bootstrap에 등록되지 않았다");
            Assert.IsNotNull(world, "IslandWorldBuilder가 Bootstrap에 등록되지 않았다");
            Assert.IsNotNull(regions);
            Assert.IsNotNull(subAreaWorld);
            Assert.IsNotNull(player);

            // 저장을 끄고 메모리 섬을 넣는다 — 물건 둘(하나는 통행 차단), 방목 없음.
            island.PersistenceEnabled = false;
            var save = new IslandSave { starterGranted = true, guideDone = true };
            save.owned.Add(new IslandOwnedRecord { id = "t_rock", count = 2 });
            save.owned.Add(new IslandOwnedRecord { id = "f_bench", count = 1 });
            save.placed.Add(new IslandPlacedRecord { id = "t_rock", x = 2, z = 0 });
            island.LoadForCapture(save);

            // 본 마을 나루터가 섰는가 — 탐험 메뉴 말고 두 번째 입구.
            Assert.IsNotNull(GameObject.Find("IslandDock"), "본 마을 나루터가 지어지지 않았다");

            Invoke(regions, "Update");
            Assert.IsNotNull(regions.CurrentRegion, "시작 리전이 잡히지 않았다");
            string regionBefore = regions.CurrentRegion.regionId;
            Vector3 fieldPos = player.transform.position;

            // ── 진입 ──
            Assert.IsTrue(world.CanTravel(out string reason), reason);
            Assert.IsTrue(world.EnterOwnIsland());
            Assert.AreEqual(IslandMode.Own, world.Mode);
            Assert.IsNotNull(regions.CurrentSubArea);
            Assert.IsTrue(regions.CurrentSubArea.detached);
            Assert.AreEqual(GameConstants.Island.SubAreaId, regions.CurrentSubArea.subAreaId);
            Assert.IsTrue(regions.SubAreaSticky, "sticky가 꺼져 있으면 섬 좌표에서 리전이 null로 바뀌며 RegionChanged가 울린다");
            Assert.IsFalse(subAreaWorld.IsInSubArea, "서브에리어 빌더가 섬을 자기 방으로 취급했다(빈 방·25m 자동 이탈)");
            Assert.IsNull(GameObject.Find("SubArea_" + GameConstants.Island.SubAreaId), "서브에리어 빌더가 섬 id로 방을 지었다");
            Assert.IsNotNull(GameObject.Find("PlayerIsland"));
            Assert.IsNotNull(GameObject.Find("IslandTerrain"));
            Assert.IsNotNull(GameObject.Find("Placed_t_rock"), "놓인 물건이 세워지지 않았다");

            float half = IslandGrid.HalfExtent(island.SizeLevel);
            AssertOnIsland(player.transform.position, half, "진입 직후");

            // 리전은 떠나온 곳에 머문다(얼어 있다) — 그래야 나올 때 방문 퀘스트·BGM이 다시 울리지 않는다.
            Invoke(regions, "Update");
            Assert.AreEqual(regionBefore, regions.CurrentRegion.regionId);

            // 몇 프레임 굴린다 — 접지(섬 바닥 콜라이더), 서브에리어 빌더의 Update(자동 이탈이 돌면 안 된다), 스포너.
            for (int i = 0; i < 30; i++) await Task.Yield();
            Assert.AreEqual(IslandMode.Own, world.Mode, "몇 프레임 뒤 섬에서 쫓겨났다");
            Assert.IsNotNull(regions.CurrentSubArea);
            AssertOnIsland(player.transform.position, half, "30프레임 뒤");
            Assert.Greater(player.transform.position.y, IslandWorldBuilder.Origin.y - 0.5f, "섬 바닥을 뚫고 떨어지고 있다");
            Assert.Less(player.transform.position.y, IslandWorldBuilder.Origin.y + 1.0f, "섬 위에 떠 있다");
            if (spawner != null)
                Assert.AreEqual(0, spawner.ActiveInsects.Count, "섬에 있는 동안 야생 곤충의 몸이 남아 있다");

            // 끼임 복구가 섬 도착 칸으로 보내는가((2000,·,2000)이 아니라).
            player.transform.position = IslandWorldBuilder.Origin + new Vector3(3f, 0.5f, 0f);
            player.RecoverToSafePosition();
            AssertOnIsland(player.transform.position, half, "끼임 복구 뒤");

            // 배치가 바뀌면 월드가 따라오는가(보관함의 벤치를 놓는다 — 재화를 건드리지 않는다).
            Assert.AreEqual(IslandPlaceResult.Ok, island.TryPlace("f_bench", -3, 0, 0));
            Assert.IsNotNull(GameObject.Find("Placed_f_bench"), "놓은 물건이 월드에 생기지 않았다");
            Assert.AreEqual(IslandPlaceResult.Overlap, island.TryPlace("t_rock", -3, 0, 0));

            // 꾸미기 카메라 — 대상이 초점으로 갔다가 플레이어로 돌아오는가.
            Camera mainCamera = Camera.main;
            Assert.IsNotNull(mainCamera);
            CameraFollower follower = mainCamera.GetComponent<CameraFollower>();
            Assert.IsNotNull(follower);
            FieldInfo targetField = typeof(CameraFollower).GetField("target", Private);
            world.BeginEditCamera();
            Assert.AreNotSame(player.transform, targetField.GetValue(follower));
            world.EndEditCamera();
            Assert.AreSame(player.transform, targetField.GetValue(follower), "꾸미기를 닫았는데 카메라가 플레이어로 돌아오지 않았다");

            // ── 남의 섬 구경 → 내 섬으로 ──
            var snapshot = new IslandSnapshot { ownerName = "이웃", sizeLevel = 1 };
            snapshot.placed.Add(new IslandPlacedRecord { id = "f_fountain", x = 0, z = 0 });
            IslandSaveRules.SanitizeSnapshot(snapshot);
            Assert.IsTrue(world.EnterVisit(snapshot));
            Assert.AreEqual(IslandMode.Visit, world.Mode);
            Assert.AreEqual(1, world.CurrentSizeLevel, "구경 중에는 주인의 섬 크기를 써야 한다");
            Assert.IsNotNull(regions.CurrentSubArea, "구경으로 바꾸다가 서브에리어 상태를 잃었다");
            await Task.Yield();
            Assert.IsNotNull(GameObject.Find("Placed_f_fountain"));
            Assert.IsTrue(world.ReturnToOwnIsland());
            Assert.AreEqual(IslandMode.Own, world.Mode);
            Assert.AreEqual(0, world.CurrentSizeLevel);

            // ── 퇴장 ──
            world.ExitIsland();
            Assert.AreEqual(IslandMode.None, world.Mode);
            Assert.IsNull(regions.CurrentSubArea);
            Assert.IsFalse(regions.SubAreaSticky, "나왔는데 리전 판정이 얼어 있다 — 다음 리전 이동이 영영 성립하지 않는다");
            Vector3 back = player.transform.position;
            Assert.LessOrEqual(Vector2.Distance(new Vector2(back.x, back.z), new Vector2(fieldPos.x, fieldPos.z)), 0.5f,
                "들어오기 전 자리로 돌아오지 않았다");
            Assert.AreSame(player.transform, targetField.GetValue(follower));

            await Task.Yield();
            await Task.Yield();
            Assert.IsNull(GameObject.Find("PlayerIsland"), "섬 월드가 파기되지 않았다");
            Invoke(regions, "Update");
            Assert.IsNotNull(regions.CurrentRegion);
            Assert.AreEqual(regionBefore, regions.CurrentRegion.regionId);

            // 다시 들어갈 수 있는가(한 번 쓰고 망가지는 상태가 없는지).
            Assert.IsTrue(world.EnterOwnIsland());
            Assert.AreEqual(IslandMode.Own, world.Mode);
            world.ExitIsland();
            Assert.AreEqual(IslandMode.None, world.Mode);
        }

        private static void AssertOnIsland(Vector3 position, float half, string when)
        {
            Vector3 local = position - IslandWorldBuilder.Origin;
            Assert.LessOrEqual(Mathf.Abs(local.x), half + 0.01f, $"{when}: 섬 밖(x)");
            Assert.LessOrEqual(Mathf.Abs(local.z), half + 0.01f, $"{when}: 섬 밖(z)");
        }

        private static void Invoke(object target, string method)
        {
            MethodInfo m = target.GetType().GetMethod(method, Private);
            Assert.IsNotNull(m, method);
            m.Invoke(target, null);
        }

        private static async Task WaitForBootAsync(int previousSceneHandle)
        {
            const int maxFrames = 600;
            for (int frame = 0; frame < maxFrames; frame++)
            {
                Scene scene = SceneManager.GetSceneByName(GameConstants.Scenes.Play);
                PlaySceneBootstrap bootstrap = Object.FindFirstObjectByType<PlaySceneBootstrap>();
                if (scene.IsValid() && scene.isLoaded && scene.handle != previousSceneHandle
                    && bootstrap != null && bootstrap.gameObject.scene == scene
                    && Object.FindFirstObjectByType<IslandWorldBuilder>() != null)
                    return;
                await Task.Yield();
            }
            Assert.Fail("PlayScene Bootstrap 초기화가 제한 시간 안에 끝나지 않았습니다.");
        }
    }
}
#endif
