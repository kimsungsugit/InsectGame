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
            if (File.Exists(scenePath))
            {
                // 빌드가 도중에 죽으면(메모리 부족 등) finally가 못 돌아 이 씬이 남는다. 이 빌더가 만든 모양 그대로면 치우고 계속한다.
                if (!IsLeftoverFromThisBuilder(scenePath))
                    throw new InvalidOperationException("Temporary scene already exists and was not made by this builder; inspect it before retrying.");
                Debug.Log("[QA] removing leftover " + scenePath + " from an interrupted build");
                AssetDatabase.DeleteAsset(scenePath);
            }
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

        // 빈 씬 + BattleVisualCapture 오브젝트 하나 — 위 Build가 저장하는 모양이다. 누가 손댄 씬이면 false.
        static bool IsLeftoverFromThisBuilder(string scenePath)
        {
            string text = File.ReadAllText(scenePath);
            int gameObjects = 0;
            for (int at = text.IndexOf("--- !u!1 &", StringComparison.Ordinal); at >= 0;
                 at = text.IndexOf("--- !u!1 &", at + 1, StringComparison.Ordinal))
                gameObjects++;
            return gameObjects == 1 && text.Contains("m_Name: BattleVisualCapture");
        }
    }
}
#endif
