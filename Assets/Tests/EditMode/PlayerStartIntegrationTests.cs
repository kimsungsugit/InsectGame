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
    [TestFixture]
    public class PlayerStartIntegrationTests
    {
        private const float HorizontalTolerance = 0.15f;

        [Test]
        [Timeout(120000)]
        public async Task PlayScene_LoadedTwice_AlwaysStartsAtVillageEntrance()
        {
            string legacyKey = GameConstants.PrefsKeys.LastSubAreaId;
            bool legacyKeyExisted = PlayerPrefs.HasKey(legacyKey);
            string legacyValue = PlayerPrefs.GetString(legacyKey, string.Empty);
            PlayerStartPose expected = PlayerStartPlacement.ResolveMainVillageEntrance(
                RegionDefinitions.CreateAll());

            try
            {
                for (int loadIndex = 0; loadIndex < 2; loadIndex++)
                {
                    PlayerPrefs.SetString(legacyKey, "meadow_hidden_grove");
                    PlayerPrefs.Save();

                    int previousSceneHandle = SceneManager.GetActiveScene().handle;
                    SceneManager.LoadScene(GameConstants.Scenes.Play, LoadSceneMode.Single);
                    await WaitForPlaySceneBuildAsync(previousSceneHandle);

                    AssertSceneStart(expected, loadIndex + 1);
                    Assert.IsFalse(PlayerPrefs.HasKey(legacyKey),
                        "레거시 SubArea 복귀 키가 시작 후 남아 있습니다.");
                }
            }
            finally
            {
                if (legacyKeyExisted)
                    PlayerPrefs.SetString(legacyKey, legacyValue);
                else
                    PlayerPrefs.DeleteKey(legacyKey);
                PlayerPrefs.Save();
            }
        }

        private static async Task WaitForPlaySceneBuildAsync(int previousSceneHandle)
        {
            const int maxFrames = 600;
            for (int frame = 0; frame < maxFrames; frame++)
            {
                Scene scene = SceneManager.GetSceneByName(GameConstants.Scenes.Play);
                PlaySceneBootstrap bootstrap = Object.FindFirstObjectByType<PlaySceneBootstrap>();
                PlayerMovement[] players = Object.FindObjectsByType<PlayerMovement>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (scene.IsValid() && scene.isLoaded && scene.handle != previousSceneHandle &&
                    bootstrap != null && bootstrap.gameObject.scene == scene &&
                    players.Length == 1)
                {
                    return;
                }

                await Task.Yield();
            }

            Assert.Fail("PlayScene Bootstrap 초기화가 제한 시간 안에 끝나지 않았습니다.");
        }

        private static void AssertSceneStart(PlayerStartPose expected, int loadNumber)
        {
            PlayerMovement[] players = Object.FindObjectsByType<PlayerMovement>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, players.Length, $"{loadNumber}번째 로드의 Player 수");

            PlayerMovement player = players[0];
            Vector2 actualXZ = new Vector2(player.transform.position.x, player.transform.position.z);
            Vector2 expectedXZ = new Vector2(expected.Position.x, expected.Position.z);
            Assert.LessOrEqual(Vector2.Distance(actualXZ, expectedXZ), HorizontalTolerance,
                $"{loadNumber}번째 로드의 시작 위치");
            Assert.Greater(Vector3.Dot(
                    player.transform.forward,
                    expected.Rotation * Vector3.forward),
                0.999f,
                $"{loadNumber}번째 로드의 시작 방향");
            Assert.AreEqual(expected.Position, player.MainWorldSafePose.Position);

            RegionManager regionManager = Object.FindFirstObjectByType<RegionManager>();
            Assert.IsNotNull(regionManager);
            MethodInfo regionUpdate = typeof(RegionManager).GetMethod(
                "Update", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(regionUpdate);
            regionUpdate.Invoke(regionManager, null);
            Assert.IsNotNull(regionManager.CurrentRegion);
            Assert.AreEqual("meadow", regionManager.CurrentRegion.regionId);
            Assert.IsNull(regionManager.CurrentSubArea);

            Camera mainCamera = Camera.main;
            Assert.IsNotNull(mainCamera);
            CameraFollower cameraFollower = mainCamera.GetComponent<CameraFollower>();
            Assert.IsNotNull(cameraFollower);
            FieldInfo targetField = typeof(CameraFollower).GetField(
                "target", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(targetField);
            Assert.AreSame(player.transform, targetField.GetValue(cameraFollower));

            SpawnPoint[] spawnPoints = Object.FindObjectsByType<SpawnPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(spawnPoints.Length, 0, "시작 위치 주변 스폰 포인트가 없습니다.");

            player.transform.position = Vector3.zero;
            player.transform.rotation = Quaternion.identity;
            player.RecoverToSafePosition();
            Vector2 recoveredXZ = new Vector2(
                player.transform.position.x, player.transform.position.z);
            Assert.LessOrEqual(Vector2.Distance(recoveredXZ, expectedXZ), HorizontalTolerance,
                "메인 월드 끼임 복구가 마을 입구를 사용하지 않습니다.");
            Assert.Greater(Vector3.Dot(
                    player.transform.forward,
                    expected.Rotation * Vector3.forward),
                0.999f,
                "메인 월드 끼임 복구 후 방향이 마을을 바라보지 않습니다.");
        }
    }
}
#endif
