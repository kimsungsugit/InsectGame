using InsectGame.Capture;
using InsectGame.NPC;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Core
{
    public class GameplayTuningApplier : MonoBehaviour
    {
        [SerializeField] private GameplayTuningProfile profile;
        [SerializeField] private InsectSpawner spawner;
        [SerializeField] private CaptureController captureController;
        [SerializeField] private NpcManager npcManager;

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

            if (npcManager != null)
            {
                npcManager.ApplyTuning(profile);
            }
        }

        public void AutoWire(NpcManager targetNpcManager)
        {
            if (npcManager == null)
            {
                npcManager = targetNpcManager;
            }

            // 런타임 부트스트랩은 AddComponent(Awake) 뒤에 참조를 주입하므로 여기서 다시 적용한다.
            Apply();
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

            // 런타임 부트스트랩은 AddComponent(Awake) 뒤에 참조를 주입하므로 여기서 다시 적용한다.
            Apply();
        }
    }
}
