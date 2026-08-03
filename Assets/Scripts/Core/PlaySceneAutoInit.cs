using UnityEngine;
using UnityEngine.SceneManagement;

namespace InsectGame.Core
{
    public static class PlaySceneAutoInit
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            // Domain Reload를 끈 에디터 재생에서도 중복 콜백이 쌓이지 않게 멱등 구독한다.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // OpeningScene을 additive로 올릴 때 sceneLoaded가 다시 호출되므로, 이름으로 Play 씬만 허용한다.
            if (!scene.IsValid() || !scene.isLoaded || scene.name != GameConstants.Scenes.Play)
            {
                return;
            }

            // 다른 additive 씬의 Bootstrap을 오인하거나 그 씬을 검색하지 않도록 전달받은 씬 루트만 본다.
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].GetComponentInChildren<PlaySceneBootstrap>(true) != null)
                {
                    return;
                }
            }

            GameObject bootstrapObj = new GameObject("PlaySceneBootstrap");
            SceneManager.MoveGameObjectToScene(bootstrapObj, scene);
            bootstrapObj.AddComponent<PlaySceneBootstrap>();
        }
    }
}
