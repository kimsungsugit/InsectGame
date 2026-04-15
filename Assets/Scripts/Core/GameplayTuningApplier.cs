using InsectGame.Capture;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Core
{
    public class GameplayTuningApplier : MonoBehaviour
    {
        [SerializeField] private GameplayTuningProfile profile;
        [SerializeField] private InsectSpawner spawner;
        [SerializeField] private CaptureController captureController;

        private void Awake()
        {
            if (profile == null)
            {
                profile = Resources.Load<GameplayTuningProfile>("GameplayTuningProfile");
            }

            Apply();
        }

        public void Apply()
        {
            if (profile == null)
            {
                return;
            }

            if (spawner != null)
            {
                spawner.ApplyTuning(profile);
            }

            if (captureController != null)
            {
                captureController.ApplyTuning(profile);
            }
        }

        public void AutoWire(InsectSpawner targetSpawner, CaptureController targetCapture)
        {
            if (spawner == null)
            {
                spawner = targetSpawner;
            }

            if (captureController == null)
            {
                captureController = targetCapture;
            }
        }
    }
}
