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

        // 명부회 오염 거점 — 이 리전에 거점이 서 있는가.
        //
        // 수문장과 같은 성격의 **정적 데이터**다. "어느 리전이 오염 대상인가"를 코드 어딘가의
        // 리전 ID 목록으로 판정하지 않는다 — RegionDefinitions가 그 방식을 명시적으로 금지하고
        // (하드코딩 리전 목록이 세 번 조용히 어긋난 전례), CreateAll()이 네 번 따로 불려도
        // 필드 값은 같으므로 사본이 갈리지 않는다.
        //
        // blightBossNpcId가 비어 있으면 그 리전에는 거점이 없다.
        // 런타임 가변 상태(정화 여부)는 여기가 아니라 RegionBlightManager가 든다.
        public string blightBossNpcId;
        public string blightSiteName;
        public string blightReturningInsectId;

        /// <summary>이 리전에 명부회 거점이 정의돼 있는가(정화 여부와 무관한 정적 판정).</summary>
        public bool HasBlightSite => !string.IsNullOrEmpty(blightBossNpcId);

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
