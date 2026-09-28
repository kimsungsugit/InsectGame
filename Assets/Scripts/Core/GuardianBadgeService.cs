using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 수문장 배지의 런타임 — 새 배지를 알리고 이정표 보상(<see cref="GuardianBadges.Milestone"/>)을 지급한다.
    ///
    /// 배지 자체는 <see cref="RegionManager"/>의 격파 기록에서 파생한다(<see cref="GuardianBadges"/> 참조).
    /// 여기가 저장하는 건 <b>어느 이정표 보상을 받았는가</b> 하나다 — 계정 스코프 PlayerPrefs CSV
    /// (<c>BadgeMilestonesClaimed</c>)이고 클라우드 DTO <c>badgeMilestonesClaimed</c>로 기기를 따라간다.
    /// 빠뜨리면 기기를 바꿀 때마다 같은 보상을 다시 받는다(<c>weeklyContestClaimed</c>가 겪은 결함).
    ///
    /// <b>새 배지 신호는 <c>RegionManager.GuardianBadgeEarned</c>다</b> — <c>GuardianDefeated</c>가 아니다.
    /// 후자는 이미 깬 수문장이 필드에 남아 있을 때 봉인을 걷으려고 <b>다시</b> 울리므로(<c>TryDefeatGuardian</c>),
    /// 그걸 들으면 같은 배지 연출이 두 번 뜬다.
    ///
    /// 싱글턴이 아니다. <see cref="RegionBlightManager"/>와 같은 계열(AutoWire + PlayerPrefs CSV + ICloudReloadable).
    /// </summary>
    public class GuardianBadgeService : MonoBehaviour, ICloudReloadable
    {
        /// <summary>배지 한 개를 얻은 순간 — 연출(<c>BadgeCeremonyUI</c>)이 듣는다.</summary>
        public struct Award
        {
            public string regionId;
            /// <summary>이 배지를 포함해 모은 수.</summary>
            public int earned;
            /// <summary>이 순간 함께 지급한 이정표(오름차순). 대개 비어 있다.</summary>
            public int[] milestones;
        }

        private static string ClaimedKey => SaveScope.PrefsKey(GameConstants.PrefsKeys.BadgeMilestonesClaimed);

        private RegionManager regionManager;
        private PlayerItemInventory itemInventory;
        private HashSet<int> claimed = new HashSet<int>();
        private bool loaded;
        private bool subscribed;

        /// <summary>새 배지 — 일생 리전당 한 번(격파 기록의 idempotent 가드를 그대로 따른다).</summary>
        public event System.Action<Award> BadgeAwarded;

        /// <summary>이정표 보상을 받았다(연출 중 자동 지급·케이스에서 수동 수령 모두).</summary>
        public event System.Action<int> MilestoneClaimed;

        public void AutoWire(RegionManager region, PlayerItemInventory items)
        {
            if (regionManager == null) regionManager = region;
            if (itemInventory == null) itemInventory = items;
            Subscribe();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            if (regionManager != null) regionManager.GuardianBadgeEarned -= OnBadgeEarned;
            subscribed = false;
        }

        private void Subscribe()
        {
            if (regionManager == null || !isActiveAndEnabled) return;
            regionManager.GuardianBadgeEarned -= OnBadgeEarned;
            regionManager.GuardianBadgeEarned += OnBadgeEarned;
            subscribed = true;
        }

        // ── 조회 ──

        public bool IsEarned(string regionId) =>
            regionManager != null && GuardianBadges.IndexOf(regionId) >= 0 && regionManager.IsGuardianDefeated(regionId);

        public int EarnedCount => regionManager != null ? GuardianBadges.CountEarned(regionManager.IsGuardianDefeated) : 0;

        public bool IsClaimed(int milestone)
        {
            EnsureLoaded();
            return claimed.Contains(milestone);
        }

        public bool CanClaim(int milestone) =>
            GuardianBadges.TryGetMilestone(milestone, out _) && EarnedCount >= milestone && !IsClaimed(milestone);

        /// <summary>도달했지만 아직 안 받은 이정표 수 — 퀵메뉴 알림용이라 할당하지 않는다.</summary>
        public int ClaimableCount
        {
            get
            {
                int earned = EarnedCount;
                int n = 0;
                for (int i = 0; i < GuardianBadges.MilestoneCount; i++)
                {
                    int c = GuardianBadges.MilestoneAt(i).count;
                    if (earned >= c && !IsClaimed(c)) n++;
                }
                return n;
            }
        }

        /// <summary>
        /// 이정표 보상을 받는다 — 도달했고 아직 안 받았을 때만. 케이스 화면의 [받기]가 부른다
        /// (이 기능 이전에 배지를 모은 세이브는 새 배지를 더 얻지 않으므로 연출의 자동 지급을 못 탄다).
        /// </summary>
        public bool TryClaim(int milestone)
        {
            if (!CanClaim(milestone)) return false;
            GuardianBadges.TryGetMilestone(milestone, out GuardianBadges.Milestone m);
            claimed.Add(milestone);
            Save();
            // 기록을 먼저 남기고 지급한다 — 지급 중 예외가 나도 두 번 받는 쪽으로 새지 않는다.
            if (itemInventory != null && m.rewards != null)
            {
                foreach (GuardianBadges.Reward r in m.rewards)
                    if (!string.IsNullOrEmpty(r.itemId) && r.count > 0) itemInventory.AddItem(r.itemId, r.count);
            }
            MilestoneClaimed?.Invoke(milestone);
            return true;
        }

        // ── 새 배지 ──

        private void OnBadgeEarned(string regionId)
        {
            if (GuardianBadges.IndexOf(regionId) < 0) return;
            EnsureLoaded();
            int earned = EarnedCount;
            // 도달한 이정표는 연출과 함께 바로 준다 — 앞서 못 받은 것(옛 세이브·다른 기기)까지 같이.
            List<int> granted = new List<int>();
            foreach (int m in GuardianBadges.Claimable(earned, claimed))
                if (TryClaim(m)) granted.Add(m);
            BadgeAwarded?.Invoke(new Award { regionId = regionId, earned = earned, milestones = granted.ToArray() });
        }

        // ── 저장 ──

        private void EnsureLoaded()
        {
            if (loaded) return;
            claimed = GuardianBadges.ParseClaimed(PlayerPrefs.GetString(ClaimedKey, ""));
            loaded = true;
        }

        private void Save()
        {
            PlayerPrefs.SetString(ClaimedKey, GuardianBadges.FormatClaimed(claimed));
            PlayerPrefs.Save();
        }

        /// <summary>클라우드 적용 뒤 — 수령 상태를 다시 읽는다. 연출은 내지 않는다(배지는 이미 격파 기록에 있다).</summary>
        public void ReloadFromDisk()
        {
            loaded = false;
            EnsureLoaded();
            if (!subscribed) Subscribe();
        }
    }
}
