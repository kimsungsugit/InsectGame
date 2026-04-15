using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    public class RegionManager : MonoBehaviour
    {
        [SerializeField] private PlayerProgressController progress;

        private RegionData[] regions;
        private RegionData currentRegion;
        private SubAreaData currentSubArea;

        private HashSet<string> unlockedRegions = new HashSet<string>();
        private HashSet<string> defeatedGuardians = new HashSet<string>();

        private const string UnlockKey = "InsectGame.UnlockedRegions";
        private const string GuardianKey = "InsectGame.DefeatedGuardians";

        public RegionData[] Regions => regions;
        public RegionData CurrentRegion => currentRegion;
        public SubAreaData CurrentSubArea => currentSubArea;

        public event System.Action<RegionData> RegionChanged;
        public event System.Action<SubAreaData> SubAreaChanged;

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

            GameObject player = GameObject.Find("Player");
            if (player == null) return;

            Vector3 pos = player.transform.position;
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

            if (foundSub != currentSubArea)
            {
                currentSubArea = foundSub;
                SubAreaChanged?.Invoke(currentSubArea);
            }
        }

        // --- 지역 잠금 시스템 ---

        public bool IsRegionAccessible(RegionData region)
        {
            if (region == null) return false;
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

        private void LoadUnlockState()
        {
            string saved = PlayerPrefs.GetString(UnlockKey, "meadow");
            unlockedRegions = new HashSet<string>(saved.Split(','));

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
