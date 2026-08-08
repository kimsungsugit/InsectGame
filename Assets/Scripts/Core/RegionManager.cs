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

        private static string UnlockKey => SaveScope.PrefsKey("InsectGame.UnlockedRegions");
        private static string GuardianKey => SaveScope.PrefsKey("InsectGame.DefeatedGuardians");

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

        /// <summary>
        /// 수문장을 처음 쓰러뜨렸을 때 그 regionId로 발화. StoryDirector의 GuardianDefeat 트리거 소스.
        ///
        /// <b>일생에 리전당 딱 한 번만 울린다</b> — <see cref="DefeatGuardian"/>이 idempotent 가드로
        /// 중복 격파를 무시하기 때문이다. 그래서 이 트리거를 쓰는 스토리 비트는 **leaf 전용**이다:
        /// 발화 순간 prereq가 미충족이면 그 비트는 영영 열리지 않고, 뒤 비트가 그걸 prereq로 삼고
        /// 있으면 캠페인이 거기서 영구 정지한다(QuestComplete와 정확히 같은 함정).
        /// 스파인은 RegionEnter/SubAreaEnter 같은 재발화 트리거에 건다.
        /// </summary>
        public event System.Action<string> GuardianDefeated;

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

            // 이전 버전은 마지막으로 진입한 SubArea를 전역 PlayerPrefs에 남겨 다음 실행 때
            // 자동 복귀했다. 이제 플레이어는 항상 마을에서 시작하므로 레거시 키를 1회 정리한다.
            if (PlayerPrefs.HasKey(GameConstants.PrefsKeys.LastSubAreaId))
            {
                PlayerPrefs.DeleteKey(GameConstants.PrefsKeys.LastSubAreaId);
                PlayerPrefs.Save();
            }

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

            // 해금·저장이 끝난 뒤에 알린다 — 구독자(StoryDirector)가 발화 시점에
            // IsRegionAccessible 같은 상태를 읽어도 이미 갱신된 값을 보게 한다.
            // 위 idempotent 가드 덕에 리전당 정확히 1회만 울린다.
            GuardianDefeated?.Invoke(regionId);
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

        /// <summary>
        /// 수문장이 서는 자리 — 이전 리전에서 오는 <b>길목</b>, 리전 경계 안쪽이다.
        ///
        /// 예전엔 두 리전 중심의 <b>중점</b>이었다. 그건 리전이 서로 겹칠 때만 경계가 되는데
        /// 이 월드의 리전은 떨어져 있어서, 13개 중 <b>9개가 어느 리전에도 속하지 않는 허공</b>에
        /// 섰다(hollow는 자기 중심에서 77m 밖). 전역 Ground 위라 떨어지지는 않지만 리전 안을
        /// 아무리 둘러봐도 수문장이 안 보였고, 지도 마커도 리전 밖을 가리켰다.
        ///
        /// 반경의 72%에 두면 항상 리전 안이면서 중심보다 바깥이라 "길을 막는" 그림이 유지된다.
        /// <c>RegionMapUI</c> 마커와 <c>StoryObjectiveTracker</c> 목표가 같은 함수를 쓰므로
        /// 실물·표시·안내가 함께 움직인다.
        /// </summary>
        public Vector3 GetGuardianPosition(RegionData region)
        {
            if (region == null) return Vector3.zero;

            Vector3 fromCenter = Vector3.zero;
            string prevId = GetPreviousRegionId(region.regionId);
            if (prevId != null)
            {
                RegionData prev = GetRegionById(prevId);
                if (prev != null) fromCenter = prev.centerPosition;
            }

            Vector3 toPrev = fromCenter - region.centerPosition;
            toPrev.y = 0f;
            // 시작 리전(meadow)은 이전 리전이 없다 — 중심에 둔다.
            if (toPrev.sqrMagnitude < 0.01f) return region.centerPosition;

            return region.centerPosition + toPrev.normalized * (region.radius * GuardianEdgeRatio);
        }

        /// <summary>수문장을 리전 반경의 몇 %에 세울지. 1.0이면 경계 밖으로 새어 나간다.</summary>
        private const float GuardianEdgeRatio = 0.72f;

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
                // ── 2막(ver2) ── 유적 수문장 격파가 '봉인이 열린 날'이자 2막의 문이다.
                // 1막에서는 ruins가 종착지라 여기 case가 없었다.
                case "ruins": return "hollow";
                case "hollow": return "dunes";
                case "dunes": return "frostline";
                case "frostline": return "emberfall";
                case "emberfall": return "canopy";
                case "canopy": return "nameless";
                // nameless는 종착지 — ver3를 붙일 때 여기 case가 생긴다.
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
                // ── 2막(ver2) ── 빠뜨리면 GetGuardianPosition의 fromCenter가 원점(0,0,0)이 되어
                // 수문장이 맵 한복판과 리전 사이 엉뚱한 자리에 스폰된다(add-region 시나리오 E).
                case "hollow": return "ruins";
                case "dunes": return "hollow";
                case "frostline": return "dunes";
                case "emberfall": return "frostline";
                case "canopy": return "emberfall";
                case "nameless": return "canopy";
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
