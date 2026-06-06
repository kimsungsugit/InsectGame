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

        // 인접 리전 + gateway(통로) 정의 — null/empty면 옛 동작(자유 이동) 유지.
        // gatewayAngle: 자기 중심에서 gateway 방향 (0°=동, 90°=북, 180°=서, 270°=남, Unity Z axis +).
        // gatewayWidth: 통로 폭(m). fence가 외곽 원주에 생성될 때 이 각도 범위만 비워둠.
        public RegionConnection[] connections;

        public bool ContainsPoint(Vector3 point)
        {
            float dx = point.x - centerPosition.x;
            float dz = point.z - centerPosition.z;
            return dx * dx + dz * dz <= radius * radius;
        }
    }

    [System.Serializable]
    public class RegionConnection
    {
        public string targetRegionId;
        public float gatewayAngle;       // degrees (0~360, 0=+X 동쪽)
        public float gatewayWidth = 5f;  // meters
    }
}
