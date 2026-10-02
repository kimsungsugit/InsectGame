using UnityEngine;

namespace InsectGame.Data
{
    [System.Serializable]
    public class SubAreaData
    {
        public string subAreaId;
        public string displayName;
        public string description;
        public Vector3 centerPosition;
        public float radius = 12f;
        public string[] exclusiveInsectIds;
        public int minLevel = 1;
        public int maxLevel = 10;
        public string environmentType;
        /// <summary>
        /// 리전에 속하지 않고 위치와 무관하게 드나드는 분리 구역(나의 섬). <c>RegionManager.EnterDetachedSubArea</c>로만
        /// 들어가고, <c>SubAreaWorldBuilder</c>는 방을 짓지 않는다 — 그 구역의 주인(<c>IslandWorldBuilder</c>)이 짓는다.
        /// "숨겨진 장소 방문" 퀘스트도 세지 않는다.
        /// </summary>
        public bool detached;

        public bool ContainsPoint(Vector3 point)
        {
            float dx = point.x - centerPosition.x;
            float dz = point.z - centerPosition.z;
            return dx * dx + dz * dz <= radius * radius;
        }
    }
}
