#if UNITY_EDITOR
using InsectGame.Core;
using InsectGame.Data;
using UnityEditor;
using UnityEngine;

namespace InsectGame.EditorTools
{
    /// <summary>
    /// 오염 거점을 <b>눈으로 확인하기 위한</b> 에디터 전용 메뉴.
    ///
    /// 왜 필요한가: 거점은 숲(Lv.12)·산(Lv.28)·유적(Lv.36)에 있는데 플레이어는 늘 초원에서 시작한다.
    /// 그리고 오염/정화는 <b>3D 월드 연출이라 테스트로 검증할 수 없다</b> — IMGUI는 배치모드
    /// 캡처에 안 잡히고, 3D는 잡히지만 캡처 도구가 플레이어를 따라가므로 초원 밖으로 못 간다
    /// (그래서 <see cref="LiveSceneCapture"/>에 <c>-captureRegion</c>을 붙였다).
    /// 사람이 직접 보려면 결국 그 리전까지 가야 하는데, 정상 플레이로는 수십 분이 걸린다.
    ///
    /// <b>플레이 모드에서만 동작한다.</b> 월드가 지어져 있어야 의미가 있다.
    /// 에디터 폴더라 플레이어 빌드에는 들어가지 않는다.
    /// </summary>
    public static class BlightSiteDebugMenu
    {
        private const string Root = "InsectGame/오염 거점/";

        // MenuItem 경로는 컴파일 상수여야 해서 거점마다 한 줄씩 손으로 는다 —
        // 여기만 데이터 주도가 안 된다. 거점을 늘리면 이 셋도 함께 늘릴 것.
        [MenuItem(Root + "숲으로 이동", false, 100)]
        private static void GoForest() => TeleportTo("forest");

        [MenuItem(Root + "산으로 이동", false, 101)]
        private static void GoMountain() => TeleportTo("mountain");

        [MenuItem(Root + "유적으로 이동", false, 102)]
        private static void GoRuins() => TeleportTo("ruins");

        [MenuItem(Root + "숲으로 이동", true)]
        [MenuItem(Root + "산으로 이동", true)]
        [MenuItem(Root + "유적으로 이동", true)]
        private static bool ValidateTeleport() => Application.isPlaying;

        [MenuItem(Root + "현재 리전 정화 (연출 재생)", false, 200)]
        private static void CleanseHere()
        {
            RegionManager region = Object.FindFirstObjectByType<RegionManager>();
            RegionBlightManager blight = Object.FindFirstObjectByType<RegionBlightManager>();
            if (region == null || blight == null)
            {
                Debug.LogWarning("[BlightDebug] RegionManager/RegionBlightManager를 못 찾았다 — 플레이 중인가?");
                return;
            }

            RegionData here = region.CurrentRegion;
            if (here == null || !here.HasBlightSite)
            {
                Debug.LogWarning("[BlightDebug] 지금 리전에는 거점이 없다 — 먼저 숲·산·유적 중 한 곳으로 이동할 것");
                return;
            }
            if (blight.IsCleansed(here.regionId))
            {
                Debug.Log("[BlightDebug] 이미 정화된 리전이다 — '정화 기록 초기화' 후 다시 들어오면 오염 상태로 돌아온다");
                return;
            }

            // 실제 승리 경로와 같은 함수를 부른다(보스·리전 검증 + idempotent 가드 포함) —
            // 디버그 전용 뒷문을 따로 두면 그 길만 동작하고 진짜 경로는 안 볼 수 있다.
            bool ok = blight.CleanseByBoss(here.blightBossNpcId, here.regionId);
            Debug.Log($"[BlightDebug] 정화 {(ok ? "성공" : "실패")} — {here.displayName}");
        }

        [MenuItem(Root + "현재 리전 정화 (연출 재생)", true)]
        private static bool ValidateCleanse() => Application.isPlaying;

        /// <summary>
        /// 정화 기록을 지운다. 상태가 <c>defeatedBosses</c>가 아니라 별도 키에 있어서
        /// 이것만 지우면 간부 격파 기록은 남고 거점만 다시 선다 — 재도전 예외 경로
        /// (<c>CanBossDuel</c>의 오염 예외)를 확인하기에 딱 맞는 상태다.
        /// </summary>
        [MenuItem(Root + "정화 기록 초기화 (다시 오염)", false, 300)]
        private static void ResetBlight()
        {
            string key = SaveScope.PrefsKey(GameConstants.PrefsKeys.BlightCleansed);
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();

            RegionBlightManager blight = Object.FindFirstObjectByType<RegionBlightManager>();
            if (blight != null) blight.ReloadFromDisk();   // 인메모리 캐시도 함께 되돌린다
            Debug.Log($"[BlightDebug] 정화 기록 초기화 ({key}) — 리전을 나갔다 들어오면 거점이 다시 선다");
        }

        private static void TeleportTo(string regionId)
        {
            RegionManager region = Object.FindFirstObjectByType<RegionManager>();
            GameObject player = GameObject.Find("Player");
            if (region == null || player == null)
            {
                Debug.LogWarning("[BlightDebug] RegionManager/Player를 못 찾았다 — 플레이 중인가?");
                return;
            }

            RegionData target = region.GetRegionById(regionId);
            if (target == null)
            {
                Debug.LogWarning($"[BlightDebug] 리전 '{regionId}'가 없다");
                return;
            }

            // 좌표만 옮기면 RegionManager.Update가 리전 변경을 잡고, 구독자(스포너·스토리·
            // 거점 비주얼)가 알아서 따라온다. y는 지형 높이를 모르니 띄워 두고 중력에 맡긴다.
            Vector3 p = target.centerPosition;
            p.y = player.transform.position.y + 5f;
            player.transform.position = p;
            Debug.Log($"[BlightDebug] {target.displayName}(으)로 이동 — 거점이 서기까지 1~2초 걸린다");
        }
    }
}
#endif
