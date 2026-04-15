using UnityEngine;

namespace InsectGame.Spawning
{
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private float radius = 5f;
        [SerializeField] private int maxLocalActive = 2;

        public string regionId;
        public string[] regionInsectIds;
        public int regionMinLevel = 1;
        public int regionMaxLevel = 5;

        private int localActiveCount;

        public bool CanSpawn => localActiveCount < maxLocalActive;

        public Vector3 GetRandomPosition()
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            return transform.position + new Vector3(offset.x, 0f, offset.y);
        }

        public void NotifySpawned()
        {
            localActiveCount = Mathf.Max(0, localActiveCount + 1);
        }

        public void NotifyDespawned()
        {
            localActiveCount = Mathf.Max(0, localActiveCount - 1);
        }

        public void ResetCount()
        {
            localActiveCount = 0;
        }
    }
}
