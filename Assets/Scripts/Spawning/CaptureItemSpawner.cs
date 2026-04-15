using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Spawning
{
    public class CaptureItemSpawner : MonoBehaviour
    {
        [SerializeField] private PlayerItemInventory inventory;
        [SerializeField] private float spawnInterval = 15f;
        [SerializeField] private int maxFieldItems = 8;
        [SerializeField] private float spawnRadius = 80f;

        private CaptureItemData[] itemDefs;
        private float totalWeight;
        private float timer;
        private Transform playerTransform;
        private readonly List<GameObject> activePickups = new List<GameObject>();

        public void Initialize(CaptureItemData[] items, Transform player)
        {
            itemDefs = items;
            playerTransform = player;
            totalWeight = 0f;
            if (items != null)
            {
                foreach (var d in items)
                    totalWeight += d.spawnWeight;
            }
        }

        private void Update()
        {
            if (itemDefs == null || itemDefs.Length == 0 || inventory == null) return;

            activePickups.RemoveAll(go => go == null);

            timer += Time.deltaTime;
            if (timer >= spawnInterval && activePickups.Count < maxFieldItems)
            {
                timer = 0f;
                SpawnRandomItem();
            }
        }

        private void SpawnRandomItem()
        {
            CaptureItemData chosen = PickWeighted();
            if (chosen == null) return;

            Vector3 center = playerTransform != null ? playerTransform.position : Vector3.zero;
            Vector2 off = Random.insideUnitCircle * spawnRadius;
            float minDist = 8f;
            if (off.magnitude < minDist) off = off.normalized * minDist;
            Vector3 pos = center + new Vector3(off.x, 0.8f, off.y);

            GameObject go = new GameObject($"Pickup_{chosen.displayName}");
            go.transform.position = pos;
            go.layer = 0;

            CaptureItemPickup pickup = go.AddComponent<CaptureItemPickup>();
            pickup.Initialize(chosen, inventory);
            activePickups.Add(go);
        }

        private CaptureItemData PickWeighted()
        {
            float roll = Random.value * totalWeight;
            float running = 0f;
            foreach (var d in itemDefs)
            {
                running += d.spawnWeight;
                if (roll <= running) return d;
            }
            return itemDefs[itemDefs.Length - 1];
        }

        public void AutoWire(PlayerItemInventory inv)
        {
            if (inventory == null) inventory = inv;
        }
    }
}
