using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace InsectGame.Editor
{
    /// <summary>
    /// Google Play용 서명 AAB를 재현 가능하게 생성합니다.
    /// 비밀번호는 프로젝트에 저장하지 않고 환경변수로만 주입합니다.
    /// </summary>
    public static class AndroidReleaseBuilder
    {
        private const string IconAssetPath = "Assets/AppIcon/insect-game-icon.png";
        private const string ApplicationId = "com.insectexploration.game";
        private const string DefaultOutputPath = "Builds/Android/insect-game.aab";
        private const string DefaultDeviceApkPath = "Builds/Android/insect-game-dev.apk";

        [MenuItem("Insect Game/Release/Apply Android Settings")]
        public static void ApplyAndroidSettings()
        {
            PlayerSettings.companyName = "Insect Exploration";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Android,
                ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = true;

            ApplyVersionFromEnvironment();
            ApplyLauncherIcon();
            AssetDatabase.SaveAssets();

            Debug.Log("[AndroidReleaseBuilder] Android 릴리스 설정 적용 완료 (ARM64, IL2CPP, AAB).");
        }

        [MenuItem("Insect Game/Release/Build Signed AAB")]
        public static void BuildSignedAab()
        {
            ApplyAndroidSettings();
            ApplySigningFromEnvironment();

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("빌드에 포함된 활성 Scene이 없습니다.");

            string outputPath = Environment.GetEnvironmentVariable("INSECTGAME_AAB_PATH");
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = DefaultOutputPath;
            outputPath = Path.GetFullPath(outputPath);

            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(outputDirectory))
                throw new InvalidOperationException("AAB 출력 경로가 올바르지 않습니다.");
            Directory.CreateDirectory(outputDirectory);

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.CleanBuildCache
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Android AAB 빌드 실패: {report.summary.result}, 오류 {report.summary.totalErrors}개");
            }

            Debug.Log(
                $"[AndroidReleaseBuilder] AAB 생성 완료: {outputPath} " +
                $"({report.summary.totalSize} bytes)");
        }

        /// <summary>
        /// Play Console과 별개로 실제 Android 기기에 설치할 개발용 APK를 생성합니다.
        /// Unity 디버그 키로 서명하므로 별도 릴리스 키스토어가 필요하지 않습니다.
        /// </summary>
        [MenuItem("Insect Game/Release/Build Device APK")]
        public static void BuildDeviceApk()
        {
            ApplyAndroidSettings();
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.Android.useCustomKeystore = false;

            string[] scenes = GetEnabledScenes();
            string outputPath = Environment.GetEnvironmentVariable("INSECTGAME_APK_PATH");
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = DefaultDeviceApkPath;
            outputPath = Path.GetFullPath(outputPath);

            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(outputDirectory))
                throw new InvalidOperationException("APK 출력 경로가 올바르지 않습니다.");
            Directory.CreateDirectory(outputDirectory);

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                // 기기 반복 테스트는 IL2CPP/Gradle 캐시를 재사용해 빌드 시간을 줄입니다.
                options = BuildOptions.Development
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Android APK 빌드 실패: {report.summary.result}, 오류 {report.summary.totalErrors}개");
            }

            Debug.Log(
                $"[AndroidReleaseBuilder] 기기 테스트 APK 생성 완료: {outputPath} " +
                $"({report.summary.totalSize} bytes)");
        }

        // Unity -executeMethod InsectGame.Editor.AndroidReleaseBuilder.BuildFromCommandLine
        public static void BuildFromCommandLine()
        {
            BuildSignedAab();
        }

        // Unity -executeMethod InsectGame.Editor.AndroidReleaseBuilder.BuildDeviceApkFromCommandLine
        public static void BuildDeviceApkFromCommandLine()
        {
            BuildDeviceApk();
        }

        private static string[] GetEnabledScenes()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("빌드에 포함된 활성 Scene이 없습니다.");
            return scenes;
        }

        private static void ApplyVersionFromEnvironment()
        {
            string versionName = Environment.GetEnvironmentVariable("INSECTGAME_VERSION_NAME");
            if (!string.IsNullOrWhiteSpace(versionName))
                PlayerSettings.bundleVersion = versionName.Trim();

            string versionCode = Environment.GetEnvironmentVariable("INSECTGAME_VERSION_CODE");
            if (string.IsNullOrWhiteSpace(versionCode)) return;

            if (!int.TryParse(versionCode, out int parsed) || parsed < 1)
                throw new InvalidOperationException("INSECTGAME_VERSION_CODE는 1 이상의 정수여야 합니다.");
            PlayerSettings.Android.bundleVersionCode = parsed;
        }

        private static void ApplyLauncherIcon()
        {
            TextureImporter importer = AssetImporter.GetAtPath(IconAssetPath) as TextureImporter;
            if (importer == null)
                throw new FileNotFoundException("앱 아이콘을 찾을 수 없습니다.", IconAssetPath);

            bool reimport = importer.textureType != TextureImporterType.Default
                || importer.mipmapEnabled
                || importer.maxTextureSize < 2048;
            if (reimport)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconAssetPath);
            if (icon == null)
                throw new InvalidOperationException("앱 아이콘 Texture2D 로드에 실패했습니다.");

            // 단일 레이어(legacy/round) 슬롯에 모두 적용합니다. Adaptive 아이콘은
            // 전경/배경 분리 에셋이 준비되기 전까지 legacy 아이콘으로 안전하게 폴백합니다.
            foreach (PlatformIconKind kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                PlatformIcon[] slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                bool changed = false;
                foreach (PlatformIcon slot in slots)
                {
                    if (slot.layerCount != 1) continue;
                    slot.SetTexture(icon, 0);
                    changed = true;
                }

                if (changed)
                    PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
            }
        }

        private static void ApplySigningFromEnvironment()
        {
            string keystorePath = RequireEnvironment("INSECTGAME_KEYSTORE_PATH");
            string keystorePassword = RequireEnvironment("INSECTGAME_KEYSTORE_PASS");
            string keyAlias = RequireEnvironment("INSECTGAME_KEYALIAS_NAME");
            string keyAliasPassword = RequireEnvironment("INSECTGAME_KEYALIAS_PASS");

            keystorePath = Path.GetFullPath(keystorePath);
            if (!File.Exists(keystorePath))
                throw new FileNotFoundException("Android 업로드 키스토어를 찾을 수 없습니다.", keystorePath);

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystorePath;
            PlayerSettings.Android.keystorePass = keystorePassword;
            PlayerSettings.Android.keyaliasName = keyAlias;
            PlayerSettings.Android.keyaliasPass = keyAliasPassword;
        }

        private static string RequireEnvironment(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"필수 환경변수 {name}가 설정되지 않았습니다.");
            return value;
        }
    }
}
