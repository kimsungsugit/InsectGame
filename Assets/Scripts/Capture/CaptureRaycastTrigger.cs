using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Capture
{
    public class CaptureRaycastTrigger : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float maxDistance = 8f;
        [SerializeField] private LayerMask insectLayer = -1;
        [SerializeField] private CaptureMinigameController minigameController;

        public void TryStartCapture()
        {
            if (targetCamera == null || minigameController == null)
            {
                return;
            }

            Ray ray = targetCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, insectLayer, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            InsectEntity target = hit.collider.GetComponentInParent<InsectEntity>();
            if (target == null)
            {
                return;
            }

            minigameController.StartMinigame(target);
        }

        public void AutoWire(Camera camera, CaptureMinigameController minigame)
        {
            if (targetCamera == null)
            {
                targetCamera = camera;
            }

            if (minigameController == null)
            {
                minigameController = minigame;
            }
        }
    }
}
