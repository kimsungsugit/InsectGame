#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using InsectGame.Core;
using InsectGame.Story;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 「챔피언의 꿈」 도입 영상(경기장 입장 8초) — <b>디코더 없이</b> 파일 헤더로 고정한다.
    ///
    /// 실제 재생은 배치모드·PlayMode 러너로 못 본다(<c>rules/testing.md</c>). 그래서 재생이 아니라 재생이 성립할 조건을 본다:
    /// 파일이 플레이어가 찾는 자리에 있는가, 길이가 게임의 시간표와 같은가, 기기 디코더가 받는 부호화인가.
    /// 어긋나면 전부 조용히 실패한다 — 영상이 안 뜨고 글자 카드로 넘어갈 뿐이라 화면으로는 티가 안 난다.
    /// </summary>
    [TestFixture]
    public class DreamIntroVideoTests
    {
        private static string VideoPath => Path.GetFullPath(Path.Combine(
            Application.dataPath, "StreamingAssets", "Video", DreamIntroVideo.FileName));

        private static string AudioPath => Path.GetFullPath(Path.Combine(
            Application.dataPath, "Resources", DreamIntroVideo.AudioResourcePath + ".wav"));

        private static string ScriptPath(string name) => Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "Tools", "Video", name));

        // ── mp4 상자 읽기(필요한 것만) ──

        private static int IndexOf(byte[] data, string tag, int from = 0)
        {
            byte[] needle = System.Text.Encoding.ASCII.GetBytes(tag);
            for (int i = from; i <= data.Length - needle.Length; i++)
            {
                bool hit = true;
                for (int k = 0; k < needle.Length && hit; k++) hit = data[i + k] == needle[k];
                if (hit) return i;
            }
            return -1;
        }

        private static uint U32(byte[] d, int at) => (uint)((d[at] << 24) | (d[at + 1] << 16) | (d[at + 2] << 8) | d[at + 3]);
        private static int U16(byte[] d, int at) => (d[at] << 8) | d[at + 1];

        // faststart라 moov가 맨 앞이다 — 첫 일치가 진짜 상자다.
        private static byte[] ReadVideo()
        {
            Assert.IsTrue(File.Exists(VideoPath), "도입 영상이 없다: " + VideoPath + " — Tools/Video/dream_arena.py");
            return File.ReadAllBytes(VideoPath);
        }

        [Test]
        public void Video_IsPlacedWhereThePlayerLooks_CaseExact()
        {
            // Android의 jar 경로는 대소문자를 구분한다 — Windows 파일시스템은 안 하므로 이름을 직접 비교한다.
            string[] names = Directory.GetFiles(Path.GetDirectoryName(VideoPath));
            bool exact = false;
            foreach (string n in names) if (Path.GetFileName(n) == DreamIntroVideo.FileName) exact = true;
            Assert.IsTrue(exact, DreamIntroVideo.FileName + "이 StreamingAssets/Video에 정확한 이름으로 있어야 한다");
        }

        [Test]
        public void Video_Duration_MatchesTheGameTimeline()
        {
            byte[] d = ReadVideo();
            int mvhd = IndexOf(d, "mvhd");
            Assert.GreaterOrEqual(mvhd, 0, "mvhd 상자를 못 찾았다(faststart가 아니거나 손상)");
            // 태그 뒤: version(1) flags(3) creation(4) modification(4) timescale(4) duration(4) — version 0
            Assert.AreEqual(0, d[mvhd + 4], "mvhd version 0만 읽는다");
            uint timescale = U32(d, mvhd + 16);
            uint duration = U32(d, mvhd + 20);
            float seconds = (float)duration / timescale;
            Assert.AreEqual(DreamPrologueData.IntroSeconds, seconds, 0.12f,
                "영상 길이가 DreamPrologueData.IntroSeconds와 다르다 — Tools/Video/dream_arena.py의 T_TOTAL과 함께 고칠 것");
        }

        [Test]
        public void Video_Encoding_FitsTheDeviceDecoder()
        {
            byte[] d = ReadVideo();
            Assert.LessOrEqual(d.Length, 4 * 1024 * 1024, "편당 4MB 이하(Docs/StoryVideos.md §1)");

            int avcC = IndexOf(d, "avcC");
            Assert.GreaterOrEqual(avcC, 0, "H.264(avcC)가 아니다");
            Assert.AreEqual(77, d[avcC + 5], "H.264 Main 프로파일(77)이어야 한다 — High는 minSdk 25 하드웨어 디코더가 못 받는 기기가 있다");

            // "avc1"은 ftyp의 호환 브랜드 목록에도 나온다 — 표본 설명(stsd) 뒤의 것이 진짜 상자다.
            int stsd = IndexOf(d, "stsd");
            Assert.GreaterOrEqual(stsd, 0);
            int avc1 = IndexOf(d, "avc1", stsd);
            Assert.GreaterOrEqual(avc1, 0);
            Assert.AreEqual(1280, U16(d, avc1 + 28), "가로 1280");
            Assert.AreEqual(720, U16(d, avc1 + 30), "세로 720 — 세로 화면은 이 마스터를 가운데 cover-crop한다");
        }

        [Test]
        public void Audio_MatchesTheVideoLength_AndIsStereo44k()
        {
            Assert.IsTrue(File.Exists(AudioPath), "도입 음원이 없다: " + AudioPath + " — Tools/Video/dream_arena_audio.py");
            using (var reader = new BinaryReader(File.OpenRead(AudioPath)))
            {
                Assert.AreEqual("RIFF", new string(reader.ReadChars(4)));
                reader.ReadInt32();
                Assert.AreEqual("WAVE", new string(reader.ReadChars(4)));

                int channels = 0, rate = 0, bits = 0;
                long dataBytes = 0;
                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    string id = new string(reader.ReadChars(4));
                    int size = reader.ReadInt32();
                    long next = reader.BaseStream.Position + size + (size & 1);
                    if (id == "fmt ")
                    {
                        reader.ReadInt16();   // 형식(PCM)
                        channels = reader.ReadInt16();
                        rate = reader.ReadInt32();
                        reader.ReadInt32();
                        reader.ReadInt16();
                        bits = reader.ReadInt16();
                    }
                    else if (id == "data") dataBytes = size;
                    reader.BaseStream.Position = next;
                }

                Assert.AreEqual(2, channels);
                Assert.AreEqual(44100, rate);
                Assert.AreEqual(16, bits);
                float seconds = (float)dataBytes / (channels * (bits / 8) * rate);
                Assert.AreEqual(DreamPrologueData.IntroSeconds, seconds, 0.05f, "음원 길이가 영상과 같아야 한다");
            }
        }

        [Test]
        public void Audio_LoadsFromResources()
        {
            // 경로 오타·임포트 누락은 "소리만 안 나는" 조용한 실패다 — 재생기는 클립이 없으면 무음으로 간다.
            AudioClip clip = Resources.Load<AudioClip>(DreamIntroVideo.AudioResourcePath);
            Assert.IsNotNull(clip, "Resources/" + DreamIntroVideo.AudioResourcePath);
            Assert.AreEqual(DreamPrologueData.IntroSeconds, clip.length, 0.05f);
        }

        [TestCase("dream_arena.py")]
        [TestCase("dream_arena_audio.py")]
        public void Scripts_ExistAndShareTheTimeline(string script)
        {
            Assert.IsTrue(File.Exists(ScriptPath(script)), script);
        }

        [Test]
        public void VideoScript_TotalSeconds_MatchesTheGameConstant()
        {
            // 시각표의 단일 출처는 스크립트다 — 게임 상수가 따로 놀면 카드 시점이 어긋난다.
            string source = File.ReadAllText(ScriptPath("dream_arena.py"));
            Match m = Regex.Match(source, @"^T_TOTAL\s*=\s*([0-9.]+)", RegexOptions.Multiline);
            Assert.IsTrue(m.Success, "T_TOTAL을 못 찾았다");
            Assert.AreEqual(DreamPrologueData.IntroSeconds, float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), 0.001f);
        }

        // ── 재생기 ──

        [Test]
        public void Player_WithoutPlay_IsInertAndSafeToStopAndDispose()
        {
            var host = new GameObject("DreamIntroTestHost");
            try
            {
                var video = new DreamIntroVideo(host);
                Assert.AreEqual(DreamIntroVideo.State.Idle, video.Current);
                Assert.IsFalse(video.IsActive);

                video.Tick(0.5f);
                video.Pause(true);
                video.Stop();
                video.Stop();   // 두 번 불려도 안전하다
                Assert.AreEqual(DreamIntroVideo.State.Idle, video.Current);

                video.Dispose();
                video.Dispose();
                Assert.AreEqual(DreamIntroVideo.State.Idle, video.Current);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
#endif
