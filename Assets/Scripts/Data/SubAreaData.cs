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

        public bool ContainsPoint(Vector3 point)
        {
            float dx = point.x - centerPosition.x;
            float dz = point.z - centerPosition.z;
            return dx * dx + dz * dz <= radius * radius;
        }
    }
}
