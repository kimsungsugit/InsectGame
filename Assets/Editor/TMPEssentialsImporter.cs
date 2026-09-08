using System.IO;
using UnityEditor;
using UnityEngine;

namespace InsectGame.Editor
{
    /// <summary>
    /// TextMeshPro "Essential Resources"(TMP Settings + 기본 폰트)를 임포트합니다.
    /// 이게 없으면 런타임에 TMP_Settings가 null이 되어 TextMeshProUGUI.Awake()에서
    /// NullReferenceException이 발생합니다(PlaySceneBootstrap.CreateTMPText 등).
    ///
    /// 커맨드라인 실행(-quit 없이! 임포트는 비동기라 콜백에서 종료):
    ///   Unity -batchmode -projectPath &lt;proj&gt; -executeMethod InsectGame.Editor.TMPEssentialsImporter.ImportFromCommandLine -logFile &lt;log&gt;
    /// </summary>
    public static class TMPEssentialsImporter
    {
        [MenuItem("Insect Game/Setup/Import TMP Essential Resources")]
        public static void ImportFromMenu()
        {
            Run(false);
        }

        public static void ImportFromCommandLine()
        {
            Run(true);
        }

        private static void Run(bool exitWhenDone)
        {
            if (Directory.Exists("Assets/TextMesh Pro/Resources"))
            {
                Debug.Log("[TMPEssentialsImporter] 이미 임포트되어 있습니다 — 스킵.");
                if (exitWhenDone) Exit(0);
                return;
            }

            string pkg = FindPackage();
            if (string.IsNullOrEmpty(pkg))
            {
                Debug.LogError("[TMPEssentialsImporter] 'TMP Essential Resources.unitypackage'를 찾지 못했습니다.");
                if (exitWhenDone) Exit(1);
                return;
            }

            if (exitWhenDone)
            {
                AssetDatabase.importPackageCompleted += OnCompleted;
                AssetDatabase.importPackageFailed += OnFailed;
                AssetDatabase.importPackageCancelled += OnCancelled;
            }

            Debug.Log($"[TMPEssentialsImporter] 임포트 시작: {pkg}");
            AssetDatabase.ImportPackage(pkg, interactive: false);
        }

        private static void OnCompleted(string packageName)
        {
            Debug.Log($"[TMPEssentialsImporter] 임포트 완료: {packageName}");
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            bool ok = Directory.Exists("Assets/TextMesh Pro/Resources");
            Debug.Log($"[TMPEssentialsImporter] TMP Resources 존재: {ok}");
            Exit(ok ? 0 : 1);
        }

        private static void OnFailed(string packageName, string error)
        {
            Debug.LogError($"[TMPEssentialsImporter] 임포트 실패: {packageName} — {error}");
            Exit(1);
        }

        private static void OnCancelled(string packageName)
        {
            Debug.LogError($"[TMPEssentialsImporter] 임포트 취소됨: {packageName}");
            Exit(1);
        }

        private static void Exit(int code)
        {
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        private static string FindPackage()
        {
            const string cache = "Library/PackageCache";
            if (Directory.Exists(cache))
            {
                foreach (string dir in Directory.GetDirectories(cache))
                {
                    string name = Path.GetFileName(dir);
                    if (name.StartsWith("com.unity.ugui") || name.StartsWith("com.unity.textmeshpro"))
                    {
                        string candidate = Path.Combine(dir, "Package Resources", "TMP Essential Resources.unitypackage");
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            return null;
        }
    }
}
