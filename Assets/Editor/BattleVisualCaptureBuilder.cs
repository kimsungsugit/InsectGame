#if UNITY_EDITOR
using System;
using System.IO;
using InsectGame.Battle;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace InsectGame.EditorTools
{
    public static class BattleVisualCaptureBuilder
    {
        public static void Build()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use batch mode to protect the open scene.");
            const string scenePath = "Assets/BattleVisualCaptureGenerated.unity";
            if (File.Exists(scenePath)) throw new InvalidOperationException("Temporary scene already exists; inspect it before retrying.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("BattleVisualCapture").AddComponent<BattleVisualCapture>();
            try
            {
                EditorSceneManager.SaveScene(scene, scenePath);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { scenePath },
                    locationPathName = "Builds/Windows/BattleVisualQA/BattleVisualQA.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(report.summary.result.ToString());
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }
    }
}
#endif
