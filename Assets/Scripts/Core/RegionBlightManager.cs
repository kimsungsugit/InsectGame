using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 명부회 오염 거점의 <b>런타임 상태</b> — 어느 리전이 아직 오염돼 있는가.
    ///
    /// 거점이 어디에 있는지(정적)는 <see cref="RegionData.blightBossNpcId"/>가 답하고,
    /// 여기는 <b>정화 여부만</b> 든다. 정적/동적을 가르는 이유는 <c>RegionDefinitions.CreateAll()</c>이
    /// 호출마다 새 인스턴스를 만들고 부트스트랩이 그걸 <b>네 번 따로</b> 만들기 때문이다 —
    /// 런타임 상태를 RegionData에 얹으면 스포너가 보는 사본과 매니저가 보는 사본이 갈라진다.
    ///
    /// <see cref="RegionManager"/>와 같은 계열이다: 싱글턴이 아니고, AutoWire로 의존을 받고,
    /// 계정 스코프 PlayerPrefs CSV에 저장하고, <c>ICloudReloadable</c>로 클라우드 로드에 붙는다.
    ///
    /// <b>정화 상태를 간부 격파 기록에서 파생하지 않는다.</b> 파생하면 코드는 줄지만, 두 하수를
    /// 이미 이긴 세이브(2막 진행자 대부분)는 산·유적이 처음부터 정화 상태라 오염을 한 번도 보지
    /// 못한다. 대신 <c>NpcDuelController.CanBossDuel</c>이 "오염 리전 안에서는 이긴 상대에게도
    /// 다시 도전할 수 있다"는 예외를 두어, 그 세이브도 지금 여기서 정화를 겪게 한다.
    /// </summary>
    public class RegionBlightManager : MonoBehaviour, ICloudReloadable
    {
        private static string CleansedKey => SaveScope.PrefsKey(GameConstants.PrefsKeys.BlightCleansed);

        private RegionManager regionManager;
        private readonly HashSet<string> cleansedRegions = new HashSet<string>();
        private bool loaded;

        /// <summary>
        /// 리전이 방금 정화됐다 — 리전당 <b>일생 한 번만</b> 울린다(<see cref="CleanseByBoss"/>의
        /// idempotent 가드). 스폰 복구·연출·스토리 트리거가 이걸 듣는다.
        /// 부팅이나 클라우드 로드로 상태를 맞출 때는 울리지 않는다 — 앱을 켤 때마다
        /// 이미 정화한 리전에서 정화 컷신이 다시 도는 것을 막는다.
        /// </summary>
        public event System.Action<string> RegionCleansed;

        public void AutoWire(RegionManager region)
        {
            if (regionManager == null) regionManager = region;
        }

        // ── 조회 ──

        /// <summary>이 리전에 명부회 거점이 살아 있는가(정의돼 있고 아직 정화 안 됨).</summary>
        public bool IsBlighted(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return false;
            RegionData region = regionManager != null ? regionManager.GetRegionById(regionId) : null;
            if (region == null || !region.HasBlightSite) return false;
            EnsureLoaded();
            return !cleansedRegions.Contains(regionId);
        }

        public bool IsCleansed(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return false;
            EnsureLoaded();
            return cleansedRegions.Contains(regionId);
        }

        /// <summary>
        /// 이 인물이 맡은 거점이 <b>지금 서 있는 리전</b>에서 아직 살아 있는가.
        ///
        /// <c>CanBossDuel</c>의 오염 예외가 쓴다. 리전을 함께 보는 것이 핵심이다 —
        /// 하수 둘은 거점이 없는 리전(숲·연못·습지)에도 서 있는데, 거기서까지 재도전을 열면
        /// 이미 이긴 상대와 무한히 다시 싸우게 된다.
        /// </summary>
        public bool IsBlightBossHere(string storyNpcId, string regionId)
        {
            if (string.IsNullOrEmpty(storyNpcId) || string.IsNullOrEmpty(regionId)) return false;
            RegionData region = regionManager != null ? regionManager.GetRegionById(regionId) : null;
            if (region == null || !region.HasBlightSite) return false;
            if (region.blightBossNpcId != storyNpcId) return false;
            EnsureLoaded();
            return !cleansedRegions.Contains(regionId);
        }

        // ── 변경 ──

        /// <summary>
        /// 이 인물을 꺾어 <paramref name="regionId"/>의 거점을 무너뜨린다.
        ///
        /// <b>보스와 리전을 함께 검증한다.</b> 승리 시점에 호출부가 아는 것은 "누구를 이겼나"와
        /// "지금 어디에 서 있나" 둘뿐이라, 리전만 보면 거점이 있는 리전에서 <b>다른</b> 인물을
        /// 이겨도 정화된다. 하수 둘이 여러 리전에 서 있어서 실제로 일어날 수 있는 조합이다.
        ///
        /// 이미 정화됐으면 조용히 반환한다 — <c>RegionManager.DefeatGuardian</c>과 같은
        /// idempotent 가드이고, 이게 있어야 재도전 승리에서 정화 연출이 두 번 돌지 않는다.
        /// </summary>
        /// <returns>이번 호출로 실제 정화됐으면 true.</returns>
        public bool CleanseByBoss(string storyNpcId, string regionId)
        {
            if (string.IsNullOrEmpty(storyNpcId) || string.IsNullOrEmpty(regionId)) return false;
            RegionData region = regionManager != null ? regionManager.GetRegionById(regionId) : null;
            if (region == null || !region.HasBlightSite) return false;
            if (region.blightBossNpcId != storyNpcId) return false;

            EnsureLoaded();
            if (!cleansedRegions.Add(regionId)) return false;
            Save();
            RegionCleansed?.Invoke(regionId);
            return true;
        }

        // ── 세이브 ──

        private void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            Load();
        }

        private void Load()
        {
            cleansedRegions.Clear();
            string csv = PlayerPrefs.GetString(CleansedKey, string.Empty);
            if (string.IsNullOrEmpty(csv)) return;
            // RemoveEmptyEntries — "mountain,," 같은 잔여 문자열이 빈 항목으로 누적되면
            // 다음 Save가 ",,mountain,,"을 써서 눈덩이가 된다(RegionManager가 겪은 형태).
            foreach (string id in csv.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = id.Trim();
                if (trimmed.Length > 0) cleansedRegions.Add(trimmed);
            }
        }

        private void Save()
        {
            PlayerPrefs.SetString(CleansedKey, string.Join(",", cleansedRegions));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 클라우드 로드가 PlayerPrefs를 갈아끼운 뒤 인메모리 캐시를 다시 읽는다.
        /// 없으면 다른 기기에서 정화한 리전이 이 기기에선 계속 오염으로 남는다
        /// (RegionManager의 해금 상태·NpcDuelController의 격파 기록과 같은 이유).
        ///
        /// <b>여기서 RegionCleansed를 울리지 않는다</b> — 로그인 직후 정화 컷신이 쏟아진다.
        /// </summary>
        public void ReloadFromDisk()
        {
            loaded = false;
            EnsureLoaded();
        }
    }
}
