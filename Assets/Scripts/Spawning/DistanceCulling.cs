using UnityEngine;

namespace InsectGame.Spawning
{
    public class DistanceCulling : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float disableDistance = 25f;
        [SerializeField] private float enableDistance = 20f;

        private Renderer[] cachedRenderers;

        private void Awake()
        {
            cachedRenderers = GetComponentsInChildren<Renderer>(true);
        }

        private void Update()
        {
            if (target == null)
            {
                return;
            }

            float distance = Vector3.Distance(transform.position, target.position);
            if (distance > disableDistance)
            {
                SetRenderers(false);
            }
            else if (distance < enableDistance)
            {
                SetRenderers(true);
            }
        }

        private void SetRenderers(bool enabled)
        {
            if (cachedRenderers == null)
            {
                return;
            }

            foreach (Renderer renderer in cachedRenderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = enabled;
                }
            }
        }
    }
}
