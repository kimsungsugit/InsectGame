using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    public class RegionManager : MonoBehaviour, ICloudReloadable
    {
        [SerializeField] private PlayerProgressController progress;

        private RegionData[] regions;
        private RegionData currentRegion;
        private SubAreaData currentSubArea;

        private HashSet<string> unlockedRegions = new HashSet<string>();
        private HashSet<string> defeatedGuardians = new HashSet<string>();
        private Transform cachedPlayerTransform; // Update 매 프레임 GameObject.Find 회피

        // SubArea 진입 시 SubAreaWorldBuilder가 플레이어를 (2000,0,2000)로 텔레포트하므로
        // Update의 위치 기반 SubArea 판정이 false가 되어 SubAreaChanged(null) 무한 토글이 발생.
        // sticky=true 동안 위치 판정 자체를 스킵 → SubAreaWorldBuilder가 명시적 Exit 트리거.
        private bool subAreaSticky;
        private string lastExitedSubAreaId;
        private float lastExitedAtTime;
        private const float SubAreaReentryCooldown = 1.5f;

        private const string UnlockKey = "InsectGame.UnlockedRegions";
        private const string GuardianKey = "InsectGame.DefeatedGuardians";

        public RegionData[] Regions => regions;
        public RegionData CurrentRegion => currentRegion;
        public SubAreaData CurrentSubArea => currentSubArea;
        public bool SubAreaSticky => subAreaSticky;

        // 사용자가 영역 안에 있지만 아직 진입 안 한 상태. SubAreaProximityChanged로 UI 표시.
        // 옛은 ContainsPoint 시 SubAreaChanged 자동 발화 → 자동 진입. 사용자 명시 요청: [E] 키 선택.
        private SubAreaData nearbySubArea;
        public SubAreaData NearbySubArea => nearbySubArea;

        public event System.Action<RegionData> RegionChanged;
        public event System.Action<SubAreaData> SubAreaChanged;
        public event System.Action<SubAreaData> SubAreaProximityChanged;

        public void SetSubAreaSticky(bool sticky, string exitedId = null)
        {
            subAreaSticky = sticky;
            if (!sticky && !string.IsNullOrEmpty(exitedId))
            {
                lastExitedSubAreaId = exitedId;
                lastExitedAtTime = Time.time;
            }
        }

        /// <summary>F2 또는 외부 트리거로 SubArea 강제 종료 — sticky 풀고 즉시 SubAreaChanged(null) 발화.</summary>
        public void ForceExitSubArea()
        {
            if (currentSubArea == null) return;
            string exitedId = currentSubArea.subAreaId;
            currentSubArea = null;
            subAreaSticky = false;
            lastExitedSubAreaId = exitedId;
            lastExitedAtTime = Time.time;
            SubAreaChanged?.Invoke(null);
        }

        public void Initialize(RegionData[] regionList)
        {
            regions = regionList;
            LoadUnlockState();
        }

        public void AutoWire(PlayerProgressController prog)
        {
            if (progress == null) progress = prog;
        }

        private void Update()
        {
            if (regions == null || regions.Length == 0) return;

            // 플레이어 transform 캐싱 (매 프레임 GameObject.Find 비용 회피)
            if (cachedPlayerTransform == null)
            {
                GameObject p = GameObject.Find("Player");
                if (p == null) return;
                cachedPlayerTransform = p.transform;
            }

            // SubArea sticky 모드: 텔레포트 좌표(2000,0,2000)에 있는 동안 위치 기반 판정 스킵
            // (SubAreaWorldBuilder가 명시적으로 SetSubAreaSticky(false)를 호출할 때까지 유지)
            if (subAreaSticky) return;

            Vector3 pos = cachedPlayerTransform.position;
            RegionData found = null;
            foreach (var r in regions)
            {
                if (r.ContainsPoint(pos))
                {
                    found = r;
                    break;
                }
            }

            if (found != currentRegion)
            {
                currentRegion = found;
                RegionChanged?.Invoke(currentRegion);
            }

            // 서브구역 감지
            SubAreaData foundSub = null;
            if (currentRegion != null && currentRegion.subAreas != null)
            {
                foreach (var sub in currentRegion.subAreas)
                {
                    if (sub.ContainsPoint(pos))
                    {
                        foundSub = sub;
                        break;
                    }
                }
            }

            // 방금 Exit한 SubArea로의 자동 재진입 차단 (쿨다운 1.5초)
            if (foundSub != null
                && foundSub.subAreaId == lastExitedSubAreaId
                && Time.time - lastExitedAtTime < SubAreaReentryCooldown)
            {
                foundSub = null;
            }

            // 옛은 currentSubArea를 자동 설정해 SubAreaChanged 발화 → 자동 진입.
            // 새는 nearbySubArea만 갱신, 사용자가 [E] 키로 RequestEnterSubArea() 호출해야 진입.
            // currentSubArea는 EnterSubArea/Exit 시점에만 변경됨.
            if (foundSub != nearbySubArea)
            {
                nearbySubArea = foundSub;
                SubAreaProximityChanged?.Invoke(nearbySubArea);
            }
        }

        /// <summary>사용자 [E] 키 또는 UI 버튼 트리거 — nearbySubArea로 명시적 진입.</summary>
        public void RequestEnterSubArea()
        {
            if (nearbySubArea == null || currentSubArea != null) return;
            currentSubArea = nearbySubArea;
            SubAreaChanged?.Invoke(currentSubArea);
            PlayerPrefs.SetString(GameConstants.PrefsKeys.LastSubAreaId, currentSubArea.subAreaId);
            PlayerPrefs.Save();
        }

        // --- SubArea 재진입 자동 복귀 ---

        public void RestoreLastSubArea(Transform playerTransform)
        {
            if (playerTransform == null || regions == null) return;
            string lastId = PlayerPrefs.GetString(GameConstants.PrefsKeys.LastSubAreaId, "");
            if (string.IsNullOrEmpty(lastId)) return;

            foreach (var r in regions)
            {
                if (r == null || r.subAreas == null) continue;
                foreach (var sub in r.subAreas)
                {
                    if (sub != null && sub.subAreaId == lastId)
                    {
                        Vector3 dest = sub.centerPosition;
                        dest.y = playerTransform.position.y;
                        playerTransform.position = dest;

                        // 첫 Update 전에 currentSubArea를 즉시 설정 → PlayerMovement의 region 가드 회귀 차단
                        currentRegion = r;
                        currentSubArea = sub;
                        SubAreaChanged?.Invoke(currentSubArea);
                        return;
                    }
                }
            }
        }

        // --- 지역 잠금 시스템 ---

        public bool IsRegionAccessible(RegionData region)
        {
            if (region == null) return false;
            // 마스터 계정은 모든 리전 우회 — AuthManager.ApplyMasterPrivileges가 PlayerPrefs를 갱신하지만
            // RegionManager.LoadUnlockState 이후 마스터 로그인 시 HashSet에 반영 안 되는 race 차단.
            if (AuthManager.Instance != null && AuthManager.Instance.IsMasterAccount) return true;
            if (region.regionId == "meadow") return true;
            return unlockedRegions.Contains(region.regionId);
        }

        public RegionData[] GetAccessibleRegions()
        {
            if (regions == null) return new RegionData[0];
            List<RegionData> result = new List<RegionData>();
            foreach (var r in regions)
            {
                if (IsRegionAccessible(r))
                    result.Add(r);
            }
            return result.ToArray();
        }

        public RegionData GetRegionById(string id)
        {
            if (regions == null) return null;
            foreach (var r in regions)
            {
                if (r.regionId == id) return r;
            }
            return null;
        }

        // --- 수문장 시스템 ---

        public bool IsGuardianDefeated(string regionId)
        {
            return defeatedGuardians.Contains(regionId);
        }

        public void DefeatGuardian(string regionId)
        {
            // 중복 격파 가드 — BattleScreenUI.CheckGuardianDefeat가 IsGuardianDefeated 가드 후 호출하지만
            // 명시적 idempotent 보장 + SaveUnlockState 중복 PlayerPrefs.Save 비용 차단.
            if (defeatedGuardians.Contains(regionId)) return;

            defeatedGuardians.Add(regionId);

            string nextRegion = GetNextRegionId(regionId);
            if (!string.IsNullOrEmpty(nextRegion))
            {
                unlockedRegions.Add(nextRegion);
            }

            // 초원 수문장 격파 시 꽃밭도 해금 (분기 경로)
            if (regionId == "meadow")
            {
                unlockedRegions.Add("garden");
            }

            SaveUnlockState();
        }

        public RegionData GetRegionWithGuardianNear(Vector3 position, float searchRadius = 15f)
        {
            if (regions == null) return null;
            foreach (var r in regions)
            {
                if (string.IsNullOrEmpty(r.guardianInsectId)) continue;
                if (IsGuardianDefeated(r.regionId)) continue;

                Vector3 guardianPos = GetGuardianPosition(r);
                if (Vector3.Distance(position, guardianPos) <= searchRadius)
                    return r;
            }
            return null;
        }

        public Vector3 GetGuardianPosition(RegionData region)
        {
            Vector3 fromCenter = Vector3.zero;
            string prevId = GetPreviousRegionId(region.regionId);
            if (prevId != null)
            {
                RegionData prev = GetRegionById(prevId);
                if (prev != null) fromCenter = prev.centerPosition;
            }
            return (fromCenter + region.centerPosition) / 2f;
        }

        // --- 지역 순서 매핑 ---

        private string GetNextRegionId(string currentRegionId)
        {
            switch (currentRegionId)
            {
                case "meadow": return "pond";      // + garden도 해금 (DefeatGuardian에서 별도 처리)
                case "pond": return "forest";
                case "forest": return "swamp";
                case "swamp": return "mountain";
                case "mountain": return "ruins";
                default: return null;
            }
        }

        private string GetPreviousRegionId(string regionId)
        {
            switch (regionId)
            {
                case "pond": return "meadow";
                case "forest": return "pond";
                case "swamp": return "forest";
                case "mountain": return "swamp";
                case "ruins": return "mountain";
                case "garden": return "meadow";
                default: return null;
            }
        }

        // --- 저장/로드 ---

        // 클라우드 로드 후 PlayerPrefs(지역 해금/수문장)를 다시 읽어 인메모리 갱신.
        // 지도 UI(IMGUI)는 매 프레임 IsRegionUnlocked로 읽어 자동 반영.
        public void ReloadFromDisk()
        {
            LoadUnlockState();
        }

        private void LoadUnlockState()
        {
            // RemoveEmptyEntries — 옛은 "meadow,," 같은 문자열에서 빈 항목이 HashSet에 누적되어
            // SaveUnlockState 시 string.Join이 ",,meadow,," 형태로 PlayerPrefs에 잔존.
            string saved = PlayerPrefs.GetString(UnlockKey, "meadow");
            unlockedRegions = new HashSet<string>(
                saved.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));

            string guardians = PlayerPrefs.GetString(GuardianKey, "");
            defeatedGuardians = new HashSet<string>(
                guardians.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
        }

        private void SaveUnlockState()
        {
            PlayerPrefs.SetString(UnlockKey, string.Join(",", unlockedRegions));
            PlayerPrefs.SetString(GuardianKey, string.Join(",", defeatedGuardians));
            PlayerPrefs.Save();
        }
    }
}
