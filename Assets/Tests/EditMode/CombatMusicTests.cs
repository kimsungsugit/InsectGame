#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using InsectGame.Core;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 전투 계열 곡(<c>ProceduralAudioGenerator.Music.cs</c>) — 등록 4지점, 길이·정수 마디, 클리핑, 폰 스피커 대역 에너지,
    /// 루프 이음매, 작업 스레드 합성.
    ///
    /// 소리가 좋은지는 귀로 검수한다(.claude/cache/stage4-audio/의 WAV). 여기서는 <b>조용히 깨지는 것</b>만 잡는다 —
    /// 전투 계열인데 긴장 램프가 빠지거나(2026-08-08에 보스 2종이 그렇게 샜다), 곡이 다시 짧아지거나, 루프가 마디 중간에서
    /// 끊기거나(예전 1대1 곡은 12초 = 150bpm 7.5마디였다), 에너지가 다시 저음으로 몰려 폰에서 안 들리게 되는 것.
    /// </summary>
    [TestFixture]
    public class CombatMusicTests
    {
        private static readonly BgmType[] CombatTypes =
        {
            BgmType.Battle, BgmType.RaidBattle, BgmType.BossLedger, BgmType.BossFinal, BgmType.Guardian, BgmType.Rival
        };

        private static readonly BgmType[] NonCombatTypes =
        {
            BgmType.Explore, BgmType.Victory, BgmType.Defeat, BgmType.Menu, BgmType.ExploreMeadow
        };

        private static string KeyOf(BgmType type)
        {
            MethodInfo m = typeof(AudioManager).GetMethod("BgmTypeToString", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(m, "AudioManager.BgmTypeToString을 못 찾음 — 이 테스트가 무의미해졌다");
            return (string)m.Invoke(null, new object[] { type });
        }

        private static bool IsCombat(BgmType type)
        {
            MethodInfo m = typeof(AudioManager).GetMethod("IsCombatBgm", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(m, "AudioManager.IsCombatBgm을 못 찾음 — 이 테스트가 무의미해졌다");
            return (bool)m.Invoke(null, new object[] { type });
        }

        [Test]
        public void NewThemes_HaveTheirOwnKeys()
        {
            // default로 떨어지면 "explore"가 나와 수문장전·라온 대결에서 탐험 곡이 흐른다(예외 없이).
            Assert.AreEqual("guardian", KeyOf(BgmType.Guardian));
            Assert.AreEqual("rival", KeyOf(BgmType.Rival));
        }

        [Test]
        public void IsCombatBgm_CombatThemes_GetIntensityRamp()
        {
            foreach (BgmType t in CombatTypes)
                Assert.IsTrue(IsCombat(t), $"{t}이 IsCombatBgm에 없다 — 체력이 낮을 때 긴장 램프(피치)가 안 걸린다");
            foreach (BgmType t in NonCombatTypes)
                Assert.IsFalse(IsCombat(t), $"{t}은 전투 곡이 아닌데 긴장 램프가 걸린다");
        }

        [Test]
        public void EveryCombatBgm_IsRenderedByTheCombatComposer()
        {
            foreach (BgmType t in CombatTypes)
                Assert.IsTrue(ProceduralAudioGenerator.IsCombatSong(KeyOf(t)),
                    $"{t}({KeyOf(t)})이 전투 곡 작곡기에 없다 — 예전 16초 생성기로 떨어지거나 무음이 된다");
        }

        [Test]
        public void CombatSongKeys_EachHasACaseInGetBgm()
        {
            // 키 목록이 둘이다(GetBGM의 case와 Music.cs의 CombatSongKeys). 한쪽만 늘리면 무음이거나 영영 안 불린다.
            string path = Path.Combine(Application.dataPath, "Scripts/Core/ProceduralAudioGenerator.cs");
            Assert.IsTrue(File.Exists(path), "소스를 못 찾음: " + path);
            string src = File.ReadAllText(path);
            src = Regex.Replace(src, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\n]*", string.Empty);
            foreach (string key in ProceduralAudioGenerator.CombatSongKeys)
            {
                StringAssert.Contains($"case \"{key}\":", src, $"GetBGM에 \"{key}\" case가 없다");
                Assert.Greater(ProceduralAudioGenerator.GetCombatSongShape(key).Bars, 0, $"\"{key}\"를 작곡하지 못한다");
            }
        }

        [TestCase("battle")]
        [TestCase("rival")]
        [TestCase("guardian")]
        [TestCase("raid")]
        [TestCase("boss_ledger")]
        [TestCase("boss_final")]
        public void CombatSong_Shape_IsWholeBarsBetween45And64Seconds(string key)
        {
            ProceduralAudioGenerator.SongShape shape = ProceduralAudioGenerator.GetCombatSongShape(key);
            Assert.AreEqual((int)Math.Round(shape.Bars * shape.SamplesPerBar), shape.Samples,
                "곡 길이가 정수 마디가 아니다 — 루프가 마디 중간에서 끊긴다");
            Assert.GreaterOrEqual(shape.Seconds, 45.0, "전투곡이 다시 짧아졌다(예전 12~16초 반복은 금방 질렸다)");
            Assert.LessOrEqual(shape.Seconds, 64.5, "곡이 길수록 클립 메모리가 커진다(22.05kHz 모노 64초 ≈ 5.6MB)");
        }

        [TestCase("battle")]
        [TestCase("rival")]
        [TestCase("guardian")]
        [TestCase("raid")]
        [TestCase("boss_ledger")]
        [TestCase("boss_final")]
        public void GetBGM_CombatKey_ReturnsAudibleMonoClipWithoutClipping(string key)
        {
            AudioClip clip = ProceduralAudioGenerator.GetBGM(key);
            Assert.IsNotNull(clip, $"GetBGM(\"{key}\")이 null — 그 전투는 무음이다");
            Assert.AreEqual(1, clip.channels);
            Assert.AreEqual(ProceduralAudioGenerator.MusicRate, clip.frequency);
            Assert.GreaterOrEqual(clip.length, 45f);

            float[] data = new float[clip.samples];
            Assert.IsTrue(clip.GetData(data, 0), "클립 데이터를 읽지 못했다");
            double sum = 0;
            int pinned = 0;
            foreach (float x in data)
            {
                sum += x * x;
                if (Math.Abs(x) >= 0.999f) pinned++;
            }
            double rms = Math.Sqrt(sum / data.Length);
            Assert.Greater(rms, 0.02, "클립이 거의 비어 있다");
            Assert.AreEqual(0, pinned, "±1에 붙은 샘플이 있다 — 잘려서(클리핑) 찢어지는 소리가 난다");
        }

        [TestCase("battle")]
        [TestCase("rival")]
        [TestCase("guardian")]
        [TestCase("raid")]
        [TestCase("boss_ledger")]
        [TestCase("boss_final")]
        public void RenderCombatSong_FullSong_NoClipPhoneBandAndSeamlessLoop(string key)
        {
            var sw = Stopwatch.StartNew();
            float[] d = ProceduralAudioGenerator.RenderCombatSong(key);
            sw.Stop();
            UnityEngine.Debug.Log($"[Music] {key}: {d.Length / (double)ProceduralAudioGenerator.MusicRate:F1}s, 합성 {sw.ElapsedMilliseconds}ms");

            double total = 0, band = 0, maxJump = 0;
            float peak = 0f;
            int overKnee = 0;
            // 폰 스피커 대역 — 300Hz 1차 고역통과 두 번(엔진의 음량 측정과 같은 어림)
            double hp = Math.Exp(-2.0 * Math.PI * 300.0 / ProceduralAudioGenerator.MusicRate);
            double h1 = 0, h1x = 0, h2 = 0, h2x = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float x = d[i];
                Assert.IsFalse(float.IsNaN(x) || float.IsInfinity(x), $"{i}번째 샘플이 NaN/무한대");
                total += x * x;
                peak = Math.Max(peak, Math.Abs(x));
                if (Math.Abs(x) > ProceduralAudioGenerator.MusicLimiterKnee) overKnee++;
                h1 = hp * (h1 + x - h1x); h1x = x;
                h2 = hp * (h2 + h1 - h2x); h2x = h1;
                band += h2 * h2;
                if (i > 0) maxJump = Math.Max(maxJump, Math.Abs(d[i] - d[i - 1]));
            }

            Assert.Less(peak, 1f, "합성 결과가 1을 넘었다 — MasterGain 전 단계에서 이미 잘린다");
            Assert.Less(overKnee / (double)d.Length, 0.005,
                "리미터 무릎을 넘는 샘플이 0.5%를 넘는다 — 봉우리가 눌려 북이 뭉개진다(믹스 음량을 낮출 것)");
            // 실측(2026-10-04): 새 곡 29~37%, 예전 보스 곡(리전 곡 생성기) 약 8%.
            Assert.GreaterOrEqual(band / total, 0.2,
                "에너지가 300Hz 아래로 몰렸다 — 폰 스피커에서는 거의 안 들린다(배음으로 무게를 낼 것)");

            // 끝 → 처음이 곡 안의 어떤 두 샘플 사이보다 크게 튀면 루프마다 딸깍 소리가 난다.
            double seam = Math.Abs(d[0] - d[d.Length - 1]);
            Assert.LessOrEqual(seam, maxJump, "루프 이음매가 곡 안의 어떤 순간보다도 크게 튄다");
        }

        [Test]
        public void RenderCombatSong_OnWorkerThread_MatchesMainThread()
        {
            // 1대1 전투곡은 작업 스레드에서 미리 굽는다(StartCombatPrewarm). UnityEngine을 부르거나 공유 상태를 건드리면
            // 여기서 예외가 나거나 결과가 갈린다.
            Task<float[]> worker = Task.Run(() => ProceduralAudioGenerator.RenderCombatSong("rival"));
            float[] main = ProceduralAudioGenerator.RenderCombatSong("rival");
            float[] off = worker.Result;
            Assert.AreEqual(main.Length, off.Length);
            for (int i = 0; i < main.Length; i++)
                if (main[i] != off[i]) Assert.Fail($"{i}번째 샘플이 다르다({main[i]} vs {off[i]}) — 합성이 결정적이지 않다");
        }
    }
}
#endif
