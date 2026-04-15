using System.Collections.Generic;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Capture
{
    [RequireComponent(typeof(SphereCollider))]
    public class CaptureProximityTrigger : MonoBehaviour
    {
        [SerializeField] private CaptureMinigameController minigameController;
        [SerializeField] private bool autoStart;
        [SerializeField] private float fallbackRadius = 5f;

        private readonly List<InsectEntity> inRange = new List<InsectEntity>();

        private float GetRadius()
        {
            SphereCollider sphere = GetComponent<SphereCollider>();
            float r = sphere != null ? sphere.radius : fallbackRadius;
            return r < 1f ? fallbackRadius : r;
        }

        public void TryStartCapture()
        {
            if (minigameController == null)
            {
                Debug.Log("[Capture] minigameController is null");
                return;
            }

            float radius = GetRadius();
            RefreshNearby(radius);

            InsectEntity target = GetNearest();
            if (target != null)
            {
                Debug.Log($"[Capture] Found insect: {target.name} at distance {Vector3.Distance(transform.position, target.transform.position):F1}");
                minigameController.StartMinigame(target);
            }
            else
            {
                Debug.Log($"[Capture] No insect within {radius}m of {transform.position}. Total InsectEntities in scene: {FindObjectsByType<InsectEntity>(FindObjectsSortMode.None).Length}");
            }
        }

        private void RefreshNearby(float radius)
        {
            inRange.Clear();

            InsectEntity[] allInsects = FindObjectsByType<InsectEntity>(FindObjectsSortMode.None);
            Vector3 origin = transform.position;

            foreach (InsectEntity entity in allInsects)
            {
                if (entity == null || !entity.gameObject.activeInHierarchy) continue;

                float dist = Vector3.Distance(origin, entity.transform.position);
                if (dist <= radius)
                {
                    inRange.Add(entity);
                }
            }
        }

        private InsectEntity GetNearest()
        {
            InsectEntity best = null;
            float bestDistance = float.MaxValue;
            Vector3 origin = transform.position;

            for (int i = inRange.Count - 1; i >= 0; i--)
            {
                InsectEntity target = inRange[i];
                if (target == null)
                {
                    inRange.RemoveAt(i);
                    continue;
                }

                float distance = Vector3.Distance(origin, target.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = target;
                }
            }

            return best;
        }

        public void AutoWire(CaptureMinigameController minigame)
        {
            if (minigameController == null)
            {
                minigameController = minigame;
            }
        }
    }
}
