using UnityEngine;

namespace InsectGame.Data
{
    [System.Serializable]
    public class RegionData
    {
        public string regionId;
        public string displayName;
        public string description;
        public Color themeColor = Color.green;
        public Vector3 centerPosition;
        public float radius = 40f;
        public int requiredLevel = 1;
        public string[] insectIds;

        // 수문장 (지역 잠금)
        public string guardianInsectId;
        public string guardianDisplayName;
        public int guardianLevel = 5;

        // 서브 구역
        public SubAreaData[] subAreas;

        public bool ContainsPoint(Vector3 point)
        {
            float dx = point.x - centerPosition.x;
            float dz = point.z - centerPosition.z;
            return dx * dx + dz * dz <= radius * radius;
        }
    }
}
