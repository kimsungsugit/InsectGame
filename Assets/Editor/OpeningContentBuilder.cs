using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InsectGame.Editor
{
    /// <summary>
    /// 오프닝 씬과 무샘플 절차 합성 테마를 재현 가능하게 생성합니다.
    /// </summary>
    public static class OpeningContentBuilder
    {
        private const string OpeningScenePath = "Assets/Scenes/OpeningScene.unity";
        private const string PlayScenePath = "Assets/Scenes/PlayScene.unity";
        private const string AudioAssetPath =
            "Assets/Resources/Audio/Opening/opening_theme.wav";

        private const int SampleRate = 44100;
        private const int ChannelCount = 2;
        private const int BitsPerSample = 16;
        private const int DurationSeconds = 10;
        private const int FrameCount = SampleRate * DurationSeconds;
        private const double TargetPeak = 0.68;
        private const double MaximumAllowedPeak = 0.7079457843841379; // -3 dBFS

        private static readonly double[] ArpeggioStarts =
        {
            5.20, 5.55, 5.90, 6.25, 6.60, 6.95, 7.30, 7.65
        };

        private static readonly double[] ArpeggioFrequencies =
        {
            293.6648, 369.9944, 440.0000, 493.8833,
            587.3295, 659.2551, 739.9888, 880.0000
        };

        [MenuItem("Insect Game/Opening/Build Opening Content")]
        public static void BuildAll()
        {
            BuildOpeningTheme();
            BuildOpeningScene();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log(
                "[OpeningContentBuilder] OpeningScene, opening_theme.wav, " +
                "and build settings generated successfully.");
        }

        private static void BuildOpeningTheme()
        {
            byte[] wavBytes = SynthesizeOpeningTheme(out double quantizedPeak);
            string absolutePath = GetAbsoluteAssetPath(AudioAssetPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Opening audio directory is invalid.");

            Directory.CreateDirectory(directory);
            bool changed = WriteIfDifferent(absolutePath, wavBytes);
            AssetDatabase.ImportAsset(
                AudioAssetPath,
                ImportAssetOptions.ForceSynchronousImport |
                (changed ? ImportAssetOptions.ForceUpdate : ImportAssetOptions.Default));

            ConfigureAudioImporter();

            double peakDb = 20.0 * Math.Log10(quantizedPeak);
            Debug.Log(
                $"[OpeningContentBuilder] Audio ready: {FrameCount} frames, " +
                $"{SampleRate} Hz, stereo PCM16, peak {quantizedPeak:F6} " +
                $"({peakDb:F2} dBFS). File changed: {changed}.");
        }

        private static byte[] SynthesizeOpeningTheme(out double quantizedPeak)
        {
            double[] left = new double[FrameCount];
            double[] right = new double[FrameCount];
            uint noiseState = 0x7A6D4E31u;
            double breezeLeft = 0.0;
            double breezeRight = 0.0;

            for (int frame = 0; frame < FrameCount; frame++)
            {
                double time = (double)frame / SampleRate;

                double whiteLeft = NextBipolar(ref noiseState);
                double whiteRight = NextBipolar(ref noiseState);
                breezeLeft = breezeLeft * 0.994 + whiteLeft * 0.006;
                breezeRight = breezeRight * 0.993 + whiteRight * 0.007;

                double padEnvelope = Smooth01(time / 0.85) *
                    (1.0 - Smooth01((time - 7.85) / 1.65));
                double slowSwell = 0.84 + 0.16 * Math.Sin(
                    2.0 * Math.PI * 0.105 * time - 0.7);

                // 초반 숲 패드: 순수 파형과 저역 통과된 결정론적 바람만 사용합니다.
                double padLeft =
                    0.105 * Math.Sin(Phase(146.8324, time) +
                        0.08 * Math.Sin(Phase(0.08, time))) +
                    0.070 * Math.Sin(Phase(220.0000, time) + 0.35) +
                    0.040 * Math.Sin(Phase(293.6648, time) + 1.10) +
                    breezeLeft * 0.075;
                double padRight =
                    0.102 * Math.Sin(Phase(146.8324, time) + 0.18 +
                        0.07 * Math.Sin(Phase(0.075, time))) +
                    0.072 * Math.Sin(Phase(220.0000, time) + 0.70) +
                    0.042 * Math.Sin(Phase(293.6648, time) + 1.45) +
                    breezeRight * 0.075;

                double sampleLeft = padLeft * padEnvelope * slowSwell;
                double sampleRight = padRight * padEnvelope * slowSwell;

                // 2.4초 부근 발견 차임: 비정수 배음의 작은 종 세 음입니다.
                AddPanned(
                    ref sampleLeft,
                    ref sampleRight,
                    Bell(time - 2.40, 587.3295, 1.75) * 0.215,
                    -0.42);
                AddPanned(
                    ref sampleLeft,
                    ref sampleRight,
                    Bell(time - 2.61, 739.9888, 1.55) * 0.185,
                    0.38);
                AddPanned(
                    ref sampleLeft,
                    ref sampleRight,
                    Bell(time - 2.86, 1108.7305, 1.35) * 0.155,
                    0.05);

                // 5.2~8.0초 상승 아르페지오.
                for (int note = 0; note < ArpeggioStarts.Length; note++)
                {
                    double pluck = Pluck(
                        time - ArpeggioStarts[note],
                        ArpeggioFrequencies[note]);
                    double pan = note % 2 == 0 ? -0.34 : 0.34;
                    AddPanned(
                        ref sampleLeft,
                        ref sampleRight,
                        pluck * (0.118 + note * 0.004),
                        pan);
                }

                // 6.2초 로고 히트가 저음 펀치로 시작해 후반의 밝은 화음으로 번집니다.
                double logoTime = time - 6.20;
                if (logoTime >= 0.0 && logoTime < 3.65)
                {
                    double attack = Smooth01(logoTime / 0.018);
                    double bodyEnvelope = attack * Math.Exp(-logoTime * 1.15);
                    double chord =
                        0.54 * Math.Sin(Phase(146.8324, logoTime)) +
                        0.34 * Math.Sin(Phase(220.0000, logoTime) + 0.12) +
                        0.24 * Math.Sin(Phase(369.9944, logoTime) + 0.28) +
                        0.18 * Math.Sin(Phase(440.0000, logoTime) + 0.43);
                    double downwardCycles = 78.0 * logoTime -
                        13.0 * logoTime * logoTime;
                    double sub = Math.Sin(2.0 * Math.PI * downwardCycles) *
                        Smooth01(logoTime / 0.006) * Math.Exp(-logoTime * 4.2);
                    double sheen = Math.Sin(
                        Phase(1174.659, logoTime) +
                        1.5 * Math.Exp(-logoTime * 5.0)) *
                        Math.Exp(-logoTime * 2.3);

                    sampleLeft += chord * bodyEnvelope * 0.155 +
                        sub * 0.205 + sheen * 0.033;
                    sampleRight += chord * bodyEnvelope * 0.158 +
                        sub * 0.205 - sheen * 0.031;
                }

                // 후반 마크 고정용 짧은 빛 번짐. 첫 로고 히트의 잔향과 연결됩니다.
                double resolveTime = time - 8.18;
                if (resolveTime >= 0.0 && resolveTime < 1.62)
                {
                    double resolveEnvelope = Smooth01(resolveTime / 0.025) *
                        Math.Exp(-resolveTime * 1.55);
                    double resolve =
                        Math.Sin(Phase(587.3295, resolveTime)) * 0.48 +
                        Math.Sin(Phase(739.9888, resolveTime) + 0.22) * 0.32 +
                        Math.Sin(Phase(880.0000, resolveTime) + 0.50) * 0.22;
                    sampleLeft += resolve * resolveEnvelope * 0.130;
                    sampleRight += resolve * resolveEnvelope * 0.130;
                }

                // 파일 경계와 마지막 잔향을 모두 무음으로 수렴시켜 클릭을 막습니다.
                double globalFade = Smooth01(time / 0.080) *
                    Smooth01((DurationSeconds - time) / 0.320);
                left[frame] = sampleLeft * globalFade;
                right[frame] = sampleRight * globalFade;
            }

            NormalizeStereo(left, right, TargetPeak);
            return EncodePcm16Wav(left, right, out quantizedPeak);
        }

        private static void ConfigureAudioImporter()
        {
            AudioImporter importer = AssetImporter.GetAtPath(AudioAssetPath) as AudioImporter;
            if (importer == null)
                throw new InvalidOperationException("Generated opening WAV could not be imported.");

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            bool changed = importer.forceToMono ||
                settings.loadType != AudioClipLoadType.DecompressOnLoad ||
                settings.compressionFormat != AudioCompressionFormat.PCM ||
                settings.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate ||
                !settings.preloadAudioData;

            if (!changed) return;

            importer.forceToMono = false;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        private static void BuildOpeningScene()
        {
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene generatedScene = default;
            bool preservePreviousScene = !Application.isBatchMode &&
                previousActiveScene.IsValid() &&
                previousActiveScene.isLoaded &&
                !string.IsNullOrEmpty(previousActiveScene.path) &&
                !string.Equals(
                    NormalizeAssetPath(previousActiveScene.path),
                    NormalizeAssetPath(OpeningScenePath),
                    StringComparison.OrdinalIgnoreCase);

            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException(
                    "OpeningScene generation was cancelled while saving open scenes.");
            }

            try
            {
                generatedScene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    preservePreviousScene ? NewSceneMode.Additive : NewSceneMode.Single);
                SceneManager.SetActiveScene(generatedScene);

                GameObject root = new GameObject("OpeningSceneController");
                root.AddComponent<InsectGame.Opening.OpeningSceneController>();

                // IMGUI 렌더링의 clear target이며, 런타임 AudioSource를 위한 유일한 listener입니다.
                // MainCamera 태그를 사용하지 않아 PlayScene 카메라 조회를 방해하지 않습니다.
                GameObject cameraObject = new GameObject("Opening Camera");
                cameraObject.tag = "Untagged";
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                Camera openingCamera = cameraObject.AddComponent<Camera>();
                openingCamera.enabled = true;
                openingCamera.clearFlags = CameraClearFlags.SolidColor;
                openingCamera.backgroundColor = new Color(0.008f, 0.014f, 0.018f, 1f);
                openingCamera.cullingMask = 0;
                openingCamera.depth = 100f;
                openingCamera.orthographic = true;
                openingCamera.allowHDR = false;
                openingCamera.allowMSAA = false;
                openingCamera.useOcclusionCulling = false;
                cameraObject.AddComponent<AudioListener>();

                if (!EditorSceneManager.SaveScene(generatedScene, OpeningScenePath))
                    throw new InvalidOperationException("OpeningScene could not be saved.");
            }
            finally
            {
                if (preservePreviousScene &&
                    previousActiveScene.IsValid() &&
                    previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                    if (generatedScene.IsValid() && generatedScene.isLoaded)
                        EditorSceneManager.CloseScene(generatedScene, true);
                }
                else if (!Application.isBatchMode &&
                    generatedScene.IsValid() &&
                    generatedScene.isLoaded &&
                    string.IsNullOrEmpty(previousActiveScene.path))
                {
                    EditorSceneManager.NewScene(
                        NewSceneSetup.DefaultGameObjects,
                        NewSceneMode.Single);
                }
            }

            AssetDatabase.ImportAsset(
                OpeningScenePath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
        }

        private static void ConfigureBuildSettings()
        {
            RequireSceneAsset(OpeningScenePath);
            RequireSceneAsset(PlayScenePath);

            List<EditorBuildSettingsScene> orderedScenes =
                new List<EditorBuildSettingsScene>
                {
                    new EditorBuildSettingsScene(OpeningScenePath, true),
                    new EditorBuildSettingsScene(PlayScenePath, true)
                };
            HashSet<string> seenPaths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
                {
                    NormalizeAssetPath(OpeningScenePath),
                    NormalizeAssetPath(PlayScenePath)
                };

            // 기존의 다른 활성 씬은 물론 비활성 항목도 순서와 상태를 보존합니다.
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                string normalizedPath = NormalizeAssetPath(scene.path);
                if (string.IsNullOrEmpty(normalizedPath) ||
                    !seenPaths.Add(normalizedPath))
                {
                    continue;
                }

                orderedScenes.Add(scene);
            }

            EditorBuildSettings.scenes = orderedScenes.ToArray();
            Debug.Log(
                $"[OpeningContentBuilder] Build settings now contain " +
                $"{orderedScenes.Count} scene(s); OpeningScene and PlayScene are indices 0 and 1.");
        }

        private static void RequireSceneAsset(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                throw new FileNotFoundException("Required scene asset is missing.", path);
        }

        private static byte[] EncodePcm16Wav(
            double[] left,
            double[] right,
            out double quantizedPeak)
        {
            int dataByteCount = FrameCount * ChannelCount * (BitsPerSample / 8);
            uint ditherState = 0x13579BDFu;
            quantizedPeak = 0.0;

            using (MemoryStream stream = new MemoryStream(44 + dataByteCount))
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataByteCount);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)ChannelCount);
                writer.Write(SampleRate);
                writer.Write(SampleRate * ChannelCount * (BitsPerSample / 8));
                writer.Write((short)(ChannelCount * (BitsPerSample / 8)));
                writer.Write((short)BitsPerSample);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataByteCount);

                for (int frame = 0; frame < FrameCount; frame++)
                {
                    WriteDitheredSample(writer, left[frame], ref ditherState, ref quantizedPeak);
                    WriteDitheredSample(writer, right[frame], ref ditherState, ref quantizedPeak);
                }

                writer.Flush();
                if (quantizedPeak > MaximumAllowedPeak)
                {
                    throw new InvalidOperationException(
                        $"Opening audio peak {quantizedPeak:F6} exceeds -3 dBFS.");
                }

                return stream.ToArray();
            }
        }

        private static void WriteDitheredSample(
            BinaryWriter writer,
            double sample,
            ref uint ditherState,
            ref double quantizedPeak)
        {
            // 0.5 LSB 피크의 결정론적 TPDF 디더.
            double dither = (NextUnit(ref ditherState) - NextUnit(ref ditherState)) *
                (0.5 / short.MaxValue);
            double withDither = Clamp(sample + dither, -1.0, 1.0);
            int quantized = (int)Math.Round(
                withDither * short.MaxValue,
                MidpointRounding.AwayFromZero);
            quantized = Math.Max(short.MinValue, Math.Min(short.MaxValue, quantized));
            writer.Write((short)quantized);

            double magnitude = Math.Abs((double)quantized / short.MaxValue);
            if (magnitude > quantizedPeak)
                quantizedPeak = magnitude;
        }

        private static void NormalizeStereo(double[] left, double[] right, double targetPeak)
        {
            double peak = 0.0;
            for (int frame = 0; frame < FrameCount; frame++)
            {
                peak = Math.Max(peak, Math.Abs(left[frame]));
                peak = Math.Max(peak, Math.Abs(right[frame]));
            }

            if (peak <= double.Epsilon)
                throw new InvalidOperationException("Opening audio synthesis produced silence.");

            double gain = targetPeak / peak;
            for (int frame = 0; frame < FrameCount; frame++)
            {
                left[frame] *= gain;
                right[frame] *= gain;
            }
        }

        private static double Bell(double localTime, double frequency, double decaySeconds)
        {
            if (localTime < 0.0 || localTime >= decaySeconds * 3.25)
                return 0.0;

            double envelope = Smooth01(localTime / 0.009) *
                Math.Exp(-localTime / decaySeconds);
            return envelope * (
                Math.Sin(Phase(frequency, localTime)) * 0.62 +
                Math.Sin(Phase(frequency * 2.013, localTime) + 0.31) * 0.24 +
                Math.Sin(Phase(frequency * 3.917, localTime) + 0.73) * 0.10 +
                Math.Sin(Phase(frequency * 5.431, localTime) + 1.08) * 0.04);
        }

        private static double Pluck(double localTime, double frequency)
        {
            if (localTime < 0.0 || localTime >= 1.18)
                return 0.0;

            double envelope = Smooth01(localTime / 0.012) *
                Math.Exp(-localTime * 3.05);
            double pulse = 0.88 + 0.12 * Math.Cos(Phase(4.2, localTime));
            return envelope * pulse * (
                Math.Sin(Phase(frequency, localTime)) * 0.72 +
                Math.Sin(Phase(frequency * 2.0, localTime) + 0.22) * 0.20 +
                Math.Sin(Phase(frequency * 3.0, localTime) + 0.47) * 0.08);
        }

        private static void AddPanned(
            ref double left,
            ref double right,
            double sample,
            double pan)
        {
            double clampedPan = Clamp(pan, -1.0, 1.0);
            double angle = (clampedPan + 1.0) * Math.PI * 0.25;
            left += sample * Math.Cos(angle);
            right += sample * Math.Sin(angle);
        }

        private static double Phase(double frequency, double time)
        {
            return 2.0 * Math.PI * frequency * time;
        }

        private static double Smooth01(double value)
        {
            double clamped = Clamp(value, 0.0, 1.0);
            return clamped * clamped * (3.0 - 2.0 * clamped);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return value < minimum ? minimum : value > maximum ? maximum : value;
        }

        private static double NextBipolar(ref uint state)
        {
            return NextUnit(ref state) * 2.0 - 1.0;
        }

        private static double NextUnit(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) / 16777216.0;
        }

        private static bool WriteIfDifferent(string path, byte[] content)
        {
            if (File.Exists(path))
            {
                byte[] existing = File.ReadAllBytes(path);
                if (existing.Length == content.Length)
                {
                    bool identical = true;
                    for (int index = 0; index < existing.Length; index++)
                    {
                        if (existing[index] == content[index]) continue;
                        identical = false;
                        break;
                    }

                    if (identical) return false;
                }
            }

            File.WriteAllBytes(path, content);
            return true;
        }

        private static string GetAbsoluteAssetPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("Unity project root could not be resolved.");

            return Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace('\\', '/').Trim();
        }
    }
}
