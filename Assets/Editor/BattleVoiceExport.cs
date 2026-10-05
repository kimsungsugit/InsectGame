#if UNITY_EDITOR
using System;
using System.IO;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.EditorTools
{
    /// <summary>
    /// 전투 목소리(울음·비명·타격음)를 WAV로 뽑는다 — 소리는 화면 캡처로 확인할 수 없어서
    /// 합성 결과를 귀로 비교하려면 파일이 필요하다. 합성기는 게임이 쓰는 그 코드
    /// (<c>ProceduralAudioGenerator.GetSFX</c>) 그대로다.
    ///
    /// <code>
    /// Unity.exe -batchmode -projectPath X:/ -executeMethod InsectGame.EditorTools.BattleVoiceExport.Run
    ///   -voiceOut .claude/cache/fx/voices -quit
    /// </code>
    /// </summary>
    public static class BattleVoiceExport
    {
        private static readonly string[] Keys =
        {
            "hit", "critical",
            "cry_beetle", "hurt_beetle", "cry_mantis", "hurt_mantis", "cry_flyer", "hurt_flyer",
            "cry_buzzer", "hurt_buzzer", "cry_chirper", "hurt_chirper", "cry_crawler", "hurt_crawler",
            "cry_boss", "hurt_boss"
        };

        public static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            int arg = Array.IndexOf(args, "-voiceOut");
            string output = Path.GetFullPath(arg >= 0 && arg + 1 < args.Length ? args[arg + 1] : ".claude/cache/fx/voices");
            Directory.CreateDirectory(output);
            int written = 0;
            foreach (string key in Keys)
            {
                AudioClip clip = ProceduralAudioGenerator.GetSFX(key);
                if (clip == null) { Debug.LogError($"[VOICE] missing {key}"); continue; }
                WriteWav(Path.Combine(output, key + ".wav"), clip);
                written++;
            }
            Debug.Log($"[VOICE] wrote {written}/{Keys.Length} to {output}");
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(written == Keys.Length ? 0 : 1);
        }

        private static void WriteWav(string path, AudioClip clip)
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            using (var stream = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(stream))
            {
                int byteCount = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + byteCount);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)clip.channels);
                w.Write(clip.frequency);
                w.Write(clip.frequency * clip.channels * 2);
                w.Write((short)(clip.channels * 2));
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(byteCount);
                foreach (float s in samples)
                    w.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
            }
        }
    }
}
#endif
