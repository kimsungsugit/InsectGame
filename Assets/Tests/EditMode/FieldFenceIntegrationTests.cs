#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InsectGame.Tests
{
    /// <summary>
    /// 실제 PlayScene에서 초원 울타리를 한 바퀴 돌며 <b>걸어서 빠져나갈 수 있는 자리</b>를 잰다.
    ///
    /// 초원은 목장 울타리(기둥 + 가로대 두 줄)로 둘러 그려지는데 가로대는 합친 메시라 콜라이더가 없었다 —
    /// 눈에는 막힌 울타리인데 기둥 사이(약 7.7m) 어디로든 걸어 나갔다. 겹친 습지 쪽은 기둥이 빠져서
    /// 울타리 끝과 습지 잠금 원 사이에 실제 틈도 있었다(2026-10-02 기기 보고).
    ///
    /// 판정은 게임의 진짜 이동 차단(<c>PlayerMovement.IsBlockedPosition</c>)을 그대로 부른다. 다른 리전의 원 안은
    /// <b>잠긴 것으로 친다</b> — 이 PC의 PlayerPrefs에 해금 기록이 남아 있어도 "첫 지역만 열린 새 게임"을 재도록.
    /// </summary>
    [TestFixture]
    public class FieldFenceIntegrationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>반지름 방향으로 훑는 구간(울타리 줄 기준 m)과 간격.</summary>
        private const float ProbeInside = 3f, ProbeOutside = 3f, ProbeStep = 0.15f;
        /// <summary>각도 간격(도). 울타리 줄에서 약 0.26m — 플레이어 검사 구(반경 0.4m)보다 촘촘하다.</summary>
        private const float AngleStep = 0.2f;

        [Test]
        [Timeout(180000)]
        public async Task Meadow_FenceRing_OpensOnlyAtTheGateway()
        {
            int previousSceneHandle = SceneManager.GetActiveScene().handle;
            SceneManager.LoadScene(GameConstants.Scenes.Play, LoadSceneMode.Single);
            await WaitForBootAsync(previousSceneHandle);

            RegionManager regionManager = Object.FindFirstObjectByType<RegionManager>();
            PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
            Assert.IsNotNull(regionManager);
            Assert.IsNotNull(player);
            RegionData[] regions = regionManager.Regions;
            RegionData meadow = WorldRouteLayout.Find(regions, "meadow");
            Assert.IsNotNull(meadow);

            // 지형은 부트 중에 지어졌다 — 물리 쪽 좌표를 맞춘 뒤 잰다.
            for (int i = 0; i < 5; i++) await Task.Yield();
            Physics.SyncTransforms();

            MethodInfo isBlocked = typeof(PlayerMovement).GetMethod("IsBlockedPosition", Private);
            Assert.IsNotNull(isBlocked, "IsBlockedPosition");
            var args = new object[1];

            float fenceRadius = meadow.radius - 1f;
            int steps = Mathf.RoundToInt(360f / AngleStep);
            var open = new bool[steps];
            for (int s = 0; s < steps; s++)
            {
                float rad = s * AngleStep * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
                bool blocked = false;
                for (float r = fenceRadius - ProbeInside; r <= fenceRadius + ProbeOutside && !blocked; r += ProbeStep)
                {
                    Vector3 p = meadow.centerPosition + dir * r;
                    p.y = FieldGround.SurfaceY(p.x, p.z);
                    if (InsideOtherRegion(regions, meadow, p)) { blocked = true; break; }
                    args[0] = p;
                    blocked = (bool)isBlocked.Invoke(player, args);
                }
                open[s] = !blocked;
            }

            // 열린 각도를 구간으로 묶는다(0°를 넘는 구간은 이어 붙인다).
            var spans = new List<(float from, float to)>();
            int start = -1;
            for (int s = 0; s < steps; s++)
            {
                if (open[s] && start < 0) start = s;
                if (!open[s] && start >= 0) { spans.Add((start * AngleStep, (s - 1) * AngleStep)); start = -1; }
            }
            if (start >= 0) spans.Add((start * AngleStep, (steps - 1) * AngleStep));
            if (spans.Count > 1 && open[0] && open[steps - 1])
            {
                var first = spans[0];
                var last = spans[spans.Count - 1];
                spans.RemoveAt(spans.Count - 1);
                spans[0] = (last.from - 360f, first.to);
            }

            Vector3 gatewayDir = WorldRouteLayout.GetGateway(meadow, regions) - meadow.centerPosition;
            float gatewayAngle = Mathf.Atan2(gatewayDir.z, gatewayDir.x) * Mathf.Rad2Deg;

            var report = new StringBuilder();
            report.Append($"[FENCE] meadow r={meadow.radius:F1} gateway={Mathf.Repeat(gatewayAngle, 360f):F1}° 열린 구간 {spans.Count}개");
            foreach (var span in spans)
            {
                float width = (span.to - span.from + AngleStep) * Mathf.Deg2Rad * fenceRadius;
                report.Append($"\n[FENCE]   {span.from:F1}°~{span.to:F1}° (폭 {width:F1}m)");
            }
            Debug.Log(report.ToString());

            Assert.AreEqual(1, spans.Count, "초원 울타리가 통로 말고도 열려 있다(또는 통로가 막혔다)\n" + report);
            float mid = (spans[0].from + spans[0].to) * 0.5f;
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(mid, gatewayAngle)), 8f, "열린 자리가 통로(길) 쪽이 아니다\n" + report);
            float gateWidth = (spans[0].to - spans[0].from + AngleStep) * Mathf.Deg2Rad * fenceRadius;
            Assert.GreaterOrEqual(gateWidth, 4f, "통로가 길 폭(2.5m)에 여유를 둔 것보다 좁다\n" + report);
        }

        private static bool InsideOtherRegion(RegionData[] regions, RegionData self, Vector3 p)
        {
            foreach (RegionData other in regions)
                if (other != null && other != self && other.ContainsPoint(p)) return true;
            return false;
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
