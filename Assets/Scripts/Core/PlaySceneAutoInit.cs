using UnityEngine;
using UnityEngine.SceneManagement;

namespace InsectGame.Core
{
    public static class PlaySceneAutoInit
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return;
            }

            if (Object.FindFirstObjectByType<PlaySceneBootstrap>() != null)
            {
                return;
            }

            GameObject bootstrapObj = new GameObject("PlaySceneBootstrap");
            bootstrapObj.AddComponent<PlaySceneBootstrap>();
        }
    }
}
