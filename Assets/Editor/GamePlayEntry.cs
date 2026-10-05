using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InsectGame.EditorTools
{
    /// <summary>Keeps normal editor Play on the shipping entry scene, independently of QA scenes.</summary>
    [InitializeOnLoad]
    public static class GamePlayEntry
    {
        private const string OpeningScene = "Assets/Scenes/OpeningScene.unity";
        private const string CurrentScenePreference = "InsectGame.PlayCurrentScene.";

        static GamePlayEntry()
        {
            if (!Application.isBatchMode) EditorApplication.delayCall += Configure;
        }

        private static void Configure()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Configure;
                return;
            }
            bool currentScene = EditorPrefs.GetBool(CurrentScenePreference + Application.dataPath, false);
            EditorSceneManager.playModeStartScene = currentScene ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(OpeningScene);
            Scene active = SceneManager.GetActiveScene();
            // Never replace a saved or edited scene. An empty scene left by a batch QA run has no useful content.
            if (!currentScene && active.IsValid() && string.IsNullOrEmpty(active.path)
                && !active.isDirty && active.rootCount == 0 && SceneManager.sceneCount == 1)
                EditorSceneManager.OpenScene(OpeningScene);
        }

        [MenuItem("Insect Game/Play/Game from Opening")]
        public static void PlayGame()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorPrefs.SetBool(CurrentScenePreference + Application.dataPath, false);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(OpeningScene);
            if (EditorSceneManager.playModeStartScene == null)
            {
                Debug.LogError("OpeningScene is missing. Restore Assets/Scenes/OpeningScene.unity before playing.");
                return;
            }
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Insect Game/Play/Current Scene (development)")]
        public static void PlayCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorPrefs.SetBool(CurrentScenePreference + Application.dataPath, true);
            EditorSceneManager.playModeStartScene = null;
            EditorApplication.isPlaying = true;
        }
    }
}
