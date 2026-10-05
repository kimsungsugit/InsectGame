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

        /// <summary>
        /// 지면 위로 떠 있는 높이(m). 옛 절대 높이 0.8 = 둔덕 밖 지면(<see cref="FieldGround.FloorY"/> 0.1) + 0.7이라
        /// 평지에선 그대로고, 사구·재 더미 위에서는 그 윗면에서 0.7 뜬다.
        /// </summary>
        internal const float HoverHeight = 0.7f;

        /// <summary>플레이어에서 이만큼(m)은 떨어져 놓는다(옛 지역 변수 minDist 그대로).</summary>
        private const float MinSpawnDistance = 8f;

        private void SpawnRandomItem()
        {
            CaptureItemData chosen = PickWeighted();
            if (chosen == null) return;

            Vector3 center = playerTransform != null ? playerTransform.position : Vector3.zero;
            Vector3 pos = PickItemPosition(() =>
            {
                Vector2 off = Random.insideUnitCircle * spawnRadius;
                // 원점에서 normalized는 0이라 플레이어 발밑에 떨어졌다 — 방향이 없으면 한쪽으로 둔다
                if (off.magnitude < MinSpawnDistance)
                    off = (off.sqrMagnitude > 1e-8f ? off.normalized : Vector2.right) * MinSpawnDistance;
                return center + new Vector3(off.x, 0f, off.y);
            });

            GameObject go = new GameObject($"Pickup_{chosen.displayName}");
            go.transform.position = pos;
            go.layer = 0;

            CaptureItemPickup pickup = go.AddComponent<CaptureItemPickup>();
            pickup.Initialize(chosen, inventory);
            activePickups.Add(go);
        }

        /// <summary>
        /// 순수 판정 — 곤충 스폰과 <b>같은 자리 규칙</b>(<see cref="InsectSpawner.PickSpawnPosition"/>: 물 위 후보는 최대 8번
        /// 다시 굴리고, 끝까지 물이면 물가로 밀어내고, 그 자리 둔덕 윗면에 세운다)을 지난 뒤 <see cref="HoverHeight"/>만큼 띄운다.
        /// 옛 스폰은 반경 80m 어디든 놓아 연못 호수 한가운데에도 떴다.
        /// </summary>
        internal static Vector3 PickItemPosition(System.Func<Vector3> roll)
        {
            Vector3 p = InsectSpawner.PickSpawnPosition(roll);
            p.y += HoverHeight;
            return p;
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
