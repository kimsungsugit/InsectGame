using System;
using System.Collections.Generic;

namespace InsectGame.Core
{
    /// <summary>
    /// 전투 계열 곡(1대1·라온 대결·수문장·레이드·간부·최종전)의 작곡과 합성.
    ///
    /// <para><b>왜 따로 두나.</b> 예전 전투곡은 12~16초짜리 한 마디 패턴을 "샘플마다 모든 악기를 다시 계산"하는
    /// 방식이었다. 그대로 50초로 늘리면 생성 시간이 네 배가 되고(기기에서 수백 ms 프리즈), 마디 수가 정수가 아니라
    /// 루프 이음매에서 딸깍 소리가 났다(12초 = 150bpm 7.5마디). 여기는 <b>음표 사건</b>을 먼저 적고(악보) 음마다 한 번만
    /// 합성해 섞는다 — 같은 음·길이는 한 번만 만든다.</para>
    ///
    /// <para><b>루프 이음매.</b> 곡 끝을 넘는 소리(마지막 음의 여운·잔향)는 곡 첫머리에 <b>감아서</b> 더한다. 그래서 끝 →
    /// 처음이 곡 안의 아무 두 샘플 사이처럼 이어진다. 곡 길이는 언제나 정수 마디다.</para>
    ///
    /// <para><b>폰 스피커.</b> 300Hz 아래는 폰이 거의 못 낸다. 무게는 저음 북이 아니라 배음으로 낸다 — 베이스·금관은 톱니
    /// 배음, 북에는 때리는 소리(중역 노이즈)와 포화 배음을 붙였다. 음량은 300Hz 위 대역 RMS로 맞춘다(<see cref="MusicTargetBandRms"/>) —
    /// 저음이 많은 곡이 폰에서 작게 들리지 않게. 300Hz 위 에너지 비율은 새 곡 29~37%, 예전 보스 곡(리전 곡 생성기)은 약 8%였다.</para>
    ///
    /// <para><b>메모리.</b> 22.05kHz 모노다. 64초면 약 140만 샘플 — float로 5.6MB. 44.1kHz로 하면 곡마다 11MB다.
    /// 나이퀴스트 11kHz는 폰 스피커 대역(~6kHz)을 넉넉히 덮는다.</para>
    ///
    /// <para><b>스레드.</b> <see cref="RenderCombatSong"/>는 UnityEngine을 부르지 않는 순수 계산이라 작업 스레드에서 돌려도 된다
    /// (1대1 전투곡을 미리 굽는 <see cref="StartCombatPrewarm"/>이 그렇게 쓴다).</para>
    /// </summary>
    public static partial class ProceduralAudioGenerator
    {
        /// <summary>전투 계열 곡의 샘플레이트(모노).</summary>
        internal const int MusicRate = 22050;

        /// <summary>
        /// 300Hz 위 대역 RMS 목표(MasterGain을 곱하기 전, <see cref="Score.BandRms"/>의 어림으로). 이 값이면 최종 클립의
        /// 300Hz~6kHz 대역 RMS가 0.035~0.038(FFT 실측)로 탐험 곡(약 0.023)보다 4dB쯤 크다 — 전투가 한 단계 세게 들린다.
        /// </summary>
        internal const float MusicTargetBandRms = 0.075f;

        /// <summary>리미터의 무릎 — 이 위의 봉우리만 tanh로 둥글게 누른다. 출력은 1을 넘지 않는다.</summary>
        internal const float MusicLimiterKnee = 0.8f;

        /// <summary>새 렌더러가 맡는 BGM 키(GetBGM의 case와 같은 문자열).</summary>
        internal static readonly string[] CombatSongKeys = { "battle", "rival", "guardian", "raid", "boss_ledger", "boss_final" };

        internal static bool IsCombatSong(string key) => Array.IndexOf(CombatSongKeys, key) >= 0;

        /// <summary>곡의 꼴(템포·마디·샘플 수) — 합성하지 않고 악보만 적어서 낸다.</summary>
        internal struct SongShape
        {
            public double Bpm;
            public int Bars;
            public int Samples;
            public double Seconds => (double)Samples / MusicRate;
            public double SamplesPerBar => MusicRate * 60.0 / Bpm * Score.BeatsPerBar;
        }

        internal static SongShape GetCombatSongShape(string key)
        {
            Score s = ComposeCombatSong(key);
            return s == null ? default : new SongShape { Bpm = s.Bpm, Bars = s.Bars, Samples = s.Length };
        }

        /// <summary>곡을 합성한다(MasterGain 전, 피크 &lt; 1). 모르는 키면 null. 스레드 안전.</summary>
        internal static float[] RenderCombatSong(string key)
        {
            Score s = ComposeCombatSong(key);
            return s?.Render();
        }

        private static Score ComposeCombatSong(string key)
        {
            switch (key)
            {
                case "battle": return ComposeBattle();
                case "rival": return ComposeRival();
                case "guardian": return ComposeGuardian();
                case "raid": return ComposeRaid();
                case "boss_ledger": return ComposeLedger();
                case "boss_final": return ComposeFinal();
                default: return null;
            }
        }

        // ────────────────────────────────────────────
        //  미리 굽기 — 작업 스레드에서
        // ────────────────────────────────────────────

        // 곡 하나를 굽는 데 데스크톱에서 60~250ms 걸린다(수문장이 가장 무겁다). AudioManager는 BGM을 바꾸는 순간 한 프레임 뒤
        // 메인 스레드에서 GetBGM을 부르므로, 미리 굽지 않은 곡은 그만큼 프레임이 멈춘다. 미리 구운 곡은 AudioClip에 옮겨 담기만 한다.
        // 사전과 Task 시작·회수는 메인 스레드에서만 한다(작업 스레드는 RenderCombatSong만 돈다).
        private static readonly Dictionary<string, System.Threading.Tasks.Task<float[]>> prewarms =
            new Dictionary<string, System.Threading.Tasks.Task<float[]>>();

        /// <summary>
        /// 전투 계열 곡을 작업 스레드에서 미리 굽는다 — 곧 그 곡을 틀 게 확실할 때(예: 라온과의 대결·수문장 레이드 직전 대사)
        /// 부르면 전환 순간 프레임이 막히지 않는다. 구운 데이터는 GetBGM이 가져갈 때까지 곡당 4~5MB를 붙잡으므로
        /// "혹시 몰라서"는 부르지 말 것. 전투 계열이 아니거나 이미 만들었거나 굽는 중이면 아무것도 안 한다. 메인 스레드 전용.
        /// </summary>
        public static void PrewarmBGM(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return; // 스레드가 없다 — 필요할 때 그 자리에서 굽는다.
#else
            if (!IsCombatSong(key) || cache.ContainsKey("bgm_" + key) || prewarms.ContainsKey(key)) return;
            prewarms[key] = System.Threading.Tasks.Task.Run(() => RenderCombatSong(key));
#endif
        }

        /// <summary>
        /// 1대1 전투곡은 거의 모든 세션에서 쓰여서 첫 탐험 곡을 만들 때 미리 굽는다. 다른 전투곡은 쓰일지 모르는 채
        /// 메모리를 붙잡지 않도록 저절로 굽지 않는다(<see cref="PrewarmBGM"/>를 부르는 쪽이 정한다).
        /// </summary>
        private static void StartCombatPrewarm() => PrewarmBGM("battle");

        /// <summary>미리 구운 데이터가 있으면 넘겨받는다(아직 굽는 중이면 끝날 때까지 기다린다). 없거나 실패했으면 null.</summary>
        private static float[] TakePrewarmed(string key)
        {
            if (!prewarms.TryGetValue(key, out System.Threading.Tasks.Task<float[]> task)) return null;
            prewarms.Remove(key);
            try { return task.Result; }
            catch (Exception) { return null; }
        }

        private static UnityEngine.AudioClip CreateMusicClip(string name, float[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                float v = data[i] * MasterGain;
                data[i] = v > 1f ? 1f : (v < -1f ? -1f : v);
            }
            UnityEngine.AudioClip clip = UnityEngine.AudioClip.Create(name, data.Length, 1, MusicRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ════════════════════════════════════════════
        //  곡
        // ════════════════════════════════════════════

        /// <summary>
        /// 1대1 전투 — A단조 150bpm, 36마디(57.6초). 예전 곡의 조성·템포를 잇는다.
        /// A(주제) → A'(주제 변주·대선율 16분 아르페지오·하이햇 16분) → B(F장조 쪽으로 올라서는 영웅적 대목, 앞 4마디 하프타임)
        /// → 브리지(D단조 저음 금관 리프·탐탐, 스네어 롤로 끌어올림) → A2(주제 + 한 옥타브 아래 금관 겹침, 절정) → 처음으로.
        /// </summary>
        private static Score ComposeBattle()
        {
            var s = new Score(150, 36, 77);
            Inst lead = s.Add(new Inst { Name = "lead", Wave = Wave.Square, MaxPartials = 14, Attack = 0.005, Decay = 0.22, Sustain = 0.62, Release = 0.07, Gate = 0.92, Gain = 0.26, Send = 0.22, VibratoDepth = 0.16, VibratoDelay = 0.16, Cutoff = 4200 });
            Inst brass = s.Add(new Inst { Name = "brass", Wave = Wave.Brass, MaxPartials = 28, Attack = 0.03, Decay = 0.3, Sustain = 0.75, Release = 0.12, Gate = 0.95, Gain = 0.30, Send = 0.18, Cutoff = 3600, DarkCutoff = 700, BrightBase = 0.55, BrightDecay = 0.25, Unison = 2, DetuneCents = 7 });
            Inst bass = s.Add(new Inst { Name = "bass", Wave = Wave.Saw, MaxPartials = 30, Attack = 0.004, Decay = 0.16, Sustain = 0.5, Release = 0.05, Gate = 0.85, Gain = 0.45, Send = 0.0, Cutoff = 2600, DarkCutoff = 450, BrightBase = 0.3, BrightDecay = 0.07 });
            Inst pad = s.Add(new Inst { Name = "pad", Wave = Wave.Saw, MaxPartials = 24, Attack = 0.12, Decay = 0.5, Sustain = 0.8, Release = 0.3, Gate = 1.0, Gain = 0.09, Send = 0.35, Cutoff = 2200, Unison = 2, DetuneCents = 9 });
            Inst pluck = s.Add(new Inst { Name = "arp", Wave = Wave.Triangle, MaxPartials = 12, Attack = 0.002, Decay = 0.09, Sustain = 0.0, Release = 0.04, Gate = 1.0, Gain = 0.20, Send = 0.2, Cutoff = 5000 });

            Chord[] a = Prog("Am F G Am Am F G E");
            Chord[] b = Prog("F G Em Am F G E E");
            Chord[] br = Prog("Dm Dm E E");

            const string ThemeA =
                "A4:.5 C5 E5 A5:1 G5:.5 E5 C5 | D5:.75 C5 A4:.5 C5:1 r:.5 A4 | B4:.5 D5 G5:1 F5:.5 E5 D5:1 | E5:1.5 C5:.5 A4:2 |" +
                "A4:.5 C5 E5 A5:1 B5:.5 C6:1 | A5:.75 G5 F5:.5 E5:1 C5 | D5:.5 E5 F5 G5 B5:1 G5 | G#5:1.5 E5:.5 B4:1 G#4";
            const string ThemeA2 =
                "A4:.5 C5 E5 A5:1 G5:.5 E5 C5 | D5:.75 C5 A4:.5 C5 D5 C5 A4 | B4:.5 D5 G5:1 F5:.5 E5 D5:1 | E5:.5 D5 C5 B4 C5:1 E5 |" +
                "A4:.5 C5 E5 A5:1 B5:.5 C6:1 | A5:.75 G5 F5:.5 E5:1 C5 | D5:.5 E5 F5 G5 B5:1 G5 | G#5:.5 B5 G#5 E5 B4 D5 E5:1";
            const string ThemeB =
                "C5:1.5 F5:.5 F5:1 E5:.5 F5 | G5:1.5 D5:.5 B4:1 D5 | E5:1.5 G5:.5 B5:1 A5:.5 G5 | A5:2 E5:1 C5 |" +
                "F5:1.5 A5:.5 C6:1 B5:.5 A5 | B5:1.5 G5:.5 D5:1 G5 | G#5:1 B5 D6 B5 | B5:1.5 G#5:.5 E5:1 r";
            const string Riff =
                "D4:.75 D4 F4:.5 A4:1 G4:.5 F4 | E4:.75 F4 E4:.5 D4:1 A3 | E4:.75 E4 G#4:.5 B4:1 A4:.5 G#4 | E4:.5 F4 G#4 A4 B4 C5 D5 E5";

            // A (0~7)
            s.Line(lead, 0, ThemeA);
            s.Pad(pad, 0, a, 57, 0.8f);
            s.Bass(bass, 0, a, 40, "R R O R R R O R");
            s.Drum(Kit.Kick, 0, 8, "X.......X.x.....");
            s.Drum(Kit.Snare, 0, 8, "....X.......X...");
            s.Drum(Kit.HatClosed, 0, 8, "x.x.x.x.x.x.x.x.");
            s.Drum(Kit.Crash, 0, 1, "X...............");
            s.Roll(Kit.Snare, s.BeatOf(7) + 3, 1, 0.25, 0.45f, 0.9f);

            // A' (8~15)
            s.Line(lead, 8, ThemeA2);
            s.Pad(pad, 8, a, 57, 0.8f);
            s.Arp(pluck, 8, a, 64, "0123210123213210");
            s.Bass(bass, 8, a, 40, "R O R O R O R O");
            s.Drum(Kit.Kick, 8, 8, "X.....x.X.x...x.");
            s.Drum(Kit.Snare, 8, 8, "....X.......X...");
            s.Drum(Kit.HatClosed, 8, 8, "xoxoxoxoxoxoxo..");
            s.Drum(Kit.HatOpen, 11, 1, "..............x.");
            s.Drum(Kit.HatOpen, 15, 1, "..............x.");
            s.Drum(Kit.Crash, 8, 1, "X...............");
            s.Roll(Kit.Snare, s.BeatOf(15) + 3, 1, 0.25, 0.5f, 1f);

            // B (16~23) — 앞 4마디 하프타임
            s.Line(lead, 16, ThemeB);
            s.Line(brass, 16, ThemeB, 0.55f, -12);
            s.Pad(pad, 16, b, 57, 1f);
            s.Bass(bass, 16, Slice(b, 0, 4), 40, "R - - - R - O -");
            s.Bass(bass, 20, Slice(b, 4, 4), 40, "R R O R R R O R");
            s.Drum(Kit.Kick, 16, 4, "X.........x.....");
            s.Drum(Kit.Snare, 16, 4, "........X.......");
            s.Drum(Kit.Kick, 20, 3, "X.......X.x.....");
            s.Drum(Kit.Snare, 20, 3, "....X.......X...");
            s.Drum(Kit.HatClosed, 16, 7, "x.x.x.x.x.x.x.x.");
            s.Drum(Kit.Crash, 16, 1, "X...............");
            s.Drum(Kit.Kick, 23, 1, "X...............");
            s.Drum(Kit.Snare, 23, 1, "....X...........");
            s.Drum(Kit.TomHi, 23, 1, "........XxXx....");
            s.Drum(Kit.TomLo, 23, 1, "............XxXx");

            // 브리지 (24~27) — 저음 금관 리프, 탐탐 3-3-2, 스네어 롤로 끌어올린다
            s.Line(brass, 24, Riff, 1f);
            s.Bass(bass, 24, Slice(br, 0, 2), 40, "R - - R - - R -");
            s.Bass(bass, 26, Slice(br, 2, 2), 40, "R R R R R R R R");
            s.Pad(pad, 26, Slice(br, 2, 2), 57, 0.7f);
            s.Drum(Kit.TomLo, 24, 2, "X.....X.....X...");
            s.Drum(Kit.TomHi, 24, 2, "...o.....o....o.");
            s.Drum(Kit.Kick, 26, 2, "X...X...X...X...");
            s.Roll(Kit.Snare, s.BeatOf(26), 4, 0.5, 0.3f, 0.6f);
            s.Roll(Kit.Snare, s.BeatOf(27), 4, 0.25, 0.6f, 1f);
            s.Riser(s.BeatOf(26), 8, 0.5f);

            // A2 (28~35) — 절정: 주제 + 한 옥타브 아래 금관
            s.Line(lead, 28, ThemeA);
            s.Line(brass, 28, ThemeA, 0.7f, -12);
            s.Pad(pad, 28, a, 57, 1f);
            s.Arp(pluck, 28, a, 64, "0123210123213210");
            s.Bass(bass, 28, a, 40, "R O R O R O R O");
            s.Drum(Kit.Kick, 28, 8, "X.....x.X.x...x.");
            s.Drum(Kit.Snare, 28, 8, "....X.......X...");
            s.Drum(Kit.HatClosed, 28, 8, "xoxoxoxoxoxoxoxo");
            s.Drum(Kit.Crash, 28, 1, "X...............");
            s.Drum(Kit.Crash, 32, 1, "X...............");
            s.Roll(Kit.Snare, s.BeatOf(35) + 3, 1, 0.25, 0.5f, 1f);
            return s;
        }

        /// <summary>
        /// 라온 대결 — G장조 160bpm, 가벼운 셔플, 36마디(54초). 주제는 아이들이 놀릴 때 부르는 "솔솔미라솔미" 꼴을 그대로 빌렸다
        /// ("라온 노래"로 기억되게). 25% 펄스(장난감 같은 리드) + 마림바 대답 + 뒷박 코드 찹 + 옥타브 튀는 베이스.
        /// A(주제·마림바가 쉼표에서 낄낄) → A'(변주) → B(쫓아가는 마림바 8분, 나무토막 똑딱) → C(멈춤 박자 — 베이스가 놀림 주제를 받아친다,
        /// 스네어로 끌어올림) → A2(주제를 3도 화음으로) → 처음으로.
        /// </summary>
        private static Score ComposeRival()
        {
            var s = new Score(160, 36, 211) { Swing = 0.07 };
            Inst lead = s.Add(new Inst { Name = "lead", Wave = Wave.Pulse25, MaxPartials = 16, Attack = 0.004, Decay = 0.14, Sustain = 0.55, Release = 0.05, Gate = 0.82, Gain = 0.30, Send = 0.18, VibratoDepth = 0.12, VibratoRate = 6.5, VibratoDelay = 0.12, Cutoff = 5200 });
            Inst harmony = s.Add(new Inst { Name = "harmony", Wave = Wave.Square, MaxPartials = 12, Attack = 0.004, Decay = 0.14, Sustain = 0.5, Release = 0.05, Gate = 0.82, Gain = 0.13, Send = 0.18, Cutoff = 4000 });
            Inst marimba = s.Add(new Inst { Name = "marimba", Mallet = true, Ratios = new[] { 1.0, 4.0, 10.0 }, Amps = new[] { 1.0, 0.35, 0.12 }, Decays = new[] { 0.45, 0.12, 0.04 }, Release = 0.06, Gate = 1.0, Gain = 0.26, Send = 0.22 });
            Inst organ = s.Add(new Inst { Name = "organ", Wave = Wave.Organ, MaxPartials = 8, Attack = 0.01, Decay = 0.2, Sustain = 0.8, Release = 0.06, Gate = 0.7, Gain = 0.11, Send = 0.15, Cutoff = 5000 });
            Inst chop = s.Add(new Inst { Name = "chop", Wave = Wave.Saw, MaxPartials = 18, Attack = 0.003, Decay = 0.07, Sustain = 0.0, Release = 0.03, Gate = 1.0, Gain = 0.20, Send = 0.12, Cutoff = 3800 });
            Inst bass = s.Add(new Inst { Name = "bass", Wave = Wave.Saw, MaxPartials = 30, Attack = 0.003, Decay = 0.12, Sustain = 0.35, Release = 0.04, Gate = 0.7, Gain = 0.36, Send = 0.0, Cutoff = 3000, DarkCutoff = 500, BrightBase = 0.25, BrightDecay = 0.05 });

            Chord[] a = Prog("G C G D G C D G");
            Chord[] b = Prog("Em C Am D Em C Am D7");
            Chord[] c = Prog("G C D D");
            int[] gMajor = Scale("G A B C D E F#");

            const string Hook =
                "G5:.5 G5 E5 A5 G5 E5 r:1 | G5:.5 G5 E5 A5 G5 E5 C5:1 | D5:.5 E5 G5 B5:1 A5:.5 G5 E5 | F#5:1 A5 D5 r |" +
                "G5:.5 G5 E5 A5 G5 E5 r:1 | G5:.5 G5 E5 A5 G5 E5 C6:1 | B5:.5 A5 G5 F#5 E5 F#5 A5:1 | G5:1.5 D5:.5 G4:1 r";
            const string Hook2 =
                "G5:.5 G5 E5 A5 G5 E5 r:1 | G5:.5 G5 E5 A5 G5 E5 C5:1 | D5:.5 E5 G5 B5:1 A5:.5 G5 E5 | F#5:.5 G5 A5 F#5 D5:1 r |" +
                "G5:.5 G5 E5 A5 G5 E5 r:1 | G5:.5 G5 E5 A5 G5 E5 C6:1 | B5:.5 A5 G5 F#5 E5 F#5 A5:1 | G5:.5 B5 D6 B5 G5:1 r";
            const string Chase =
                "B4:.5 E5 G5 E5 B5 G5 E5 G5 | C5:.5 E5 G5 E5 C6:1 B5:.5 A5 | A5:.5 G5 E5 C5 A4 C5 E5 A5 | F#5:1.5 E5:.5 D5:1 A4 |" +
                "B4:.5 E5 G5 E5 B5 G5 E5 G5 | C5:.5 E5 G5 E5 C6:1 D6:.5 E6 | C6:.5 B5 A5 G5 E5 G5 A5 C6 | D6:1 C6:.5 A5 F#5:1 D5";
            const string Taunt =
                "G3:.5 G3 E3 A3 G3 E3 r:1 | G3:.5 G3 E3 A3 G3 E3 C3:1 | D3:.5 D3 F#3 A3 D4 A3 F#3 A3 | D4:.5 r D4 r D4:.25 D4 D4 D4 r:1";

            // A (0~7) — 쉼표마다 마림바가 낄낄
            s.Line(lead, 0, Hook);
            s.Phrase(marimba, s.BeatOf(0) + 3, "D6:.25 B5 G5:.5", 0.9f);
            s.Phrase(marimba, s.BeatOf(4) + 3, "D6:.25 B5 G5:.5", 0.9f);
            s.Stabs(chop, 0, a, 59, "..x...x...x...x.");
            s.Bass(bass, 0, a, 40, "R O R O R O R O");
            s.Drum(Kit.Kick, 0, 8, "X.....x.X.......");
            s.Drum(Kit.Clap, 0, 8, "....X.......X...");
            s.Drum(Kit.HatClosed, 0, 8, "x.x.x.x.x.x.x.x.");
            s.Drum(Kit.Crash, 0, 1, "X...............");

            // A' (8~15)
            s.Line(lead, 8, Hook2);
            s.Phrase(marimba, s.BeatOf(8) + 3, "B5:.25 C6 D6:.5", 0.9f);
            s.Phrase(marimba, s.BeatOf(12) + 3, "G6:.25 E6 D6:.5", 0.9f);
            s.Stabs(chop, 8, a, 59, "..x...x...x...x.");
            s.Bass(bass, 8, a, 40, "R O F O R O F O");
            s.Drum(Kit.Kick, 8, 8, "X.....x.X.x.....");
            s.Drum(Kit.Clap, 8, 8, "....X.......X...");
            s.Drum(Kit.Snare, 8, 8, "...............o");
            s.Drum(Kit.HatClosed, 8, 8, "xoxoxoxoxoxoxo..");
            s.Drum(Kit.HatOpen, 8, 8, "..............x.");

            // B (16~23) — 쫓아가기: 마림바 + 오르간 겹침, 나무토막 똑딱
            s.Line(marimba, 16, Chase);
            s.Line(organ, 16, Chase, 0.9f);
            s.Stabs(chop, 16, b, 59, "....x.......x...");
            s.Bass(bass, 16, b, 40, "R . O R . R O .");
            s.Drum(Kit.Kick, 16, 8, "X.....x.X.....x.");
            s.Drum(Kit.Snare, 16, 8, "....X.......X...");
            s.Drum(Kit.HatClosed, 16, 8, "x.x.x.x.x.x.x.x.");
            s.Drum(Kit.WoodHi, 16, 8, "..x.......x.....");
            s.Drum(Kit.WoodLo, 16, 8, "......x.......x.");
            s.Drum(Kit.Crash, 16, 1, "X...............");

            // C (24~27) — 멈춤 박자: 다 같이 "빰 빰!" 하고 베이스가 놀림 주제를 받아친다
            s.Line(bass, 24, Taunt, 1.1f);
            s.Stabs(chop, 24, Slice(c, 0, 2), 59, "X.....X.........", 1.3f);
            s.Drum(Kit.Kick, 24, 2, "X.....X.........");
            s.Drum(Kit.Clap, 24, 2, "X.....X.........");
            s.Drum(Kit.Crash, 24, 1, "X...............");
            s.Drum(Kit.Kick, 26, 2, "X...X...X...X...");
            s.Roll(Kit.Snare, s.BeatOf(26), 4, 0.5, 0.35f, 0.65f);
            s.Roll(Kit.Snare, s.BeatOf(27), 3, 0.25, 0.65f, 1f);
            s.Riser(s.BeatOf(26), 7, 0.45f);

            // A2 (28~35) — 주제를 3도 아래 화음으로
            s.Line(lead, 28, Hook);
            s.Line(harmony, 28, Hook, 1f, 0, gMajor, -2);
            s.Arp(marimba, 28, a, 67, "0.1.2.3.2.1.2.3.", 0.55f);
            s.Stabs(chop, 28, a, 59, "..x...x...x...x.");
            s.Bass(bass, 28, a, 40, "R O F O R O F O");
            s.Drum(Kit.Kick, 28, 8, "X.....x.X.x.....");
            s.Drum(Kit.Clap, 28, 8, "....X.......X...");
            s.Drum(Kit.HatClosed, 28, 8, "xoxoxoxoxoxoxoxo");
            s.Drum(Kit.Crash, 28, 1, "X...............");
            s.Drum(Kit.Crash, 32, 1, "X...............");
            return s;
        }

        /// <summary>
        /// 수문장 레이드 — C단조 96bpm, 24마디(60초). 레이드 곡(D단조 120bpm, 곧은 8분)과 일부러 다르게:
        /// 느리고 무겁다. 큰북(타이코)이 3-3-2로 땅을 울리고, 낮은 트롬본이 동기를, 호른이 주제를 분다. 첫 박엔 징.
        /// A(타이코 + 저음 현 8분 + 트롬본 동기, i–VI–iv–V) → B(호른 주제·합창, 행진 스네어) → C(D♭→G 나폴리 화음, 팀파니 롤)
        /// → A2(주제 머리를 호른·트럼펫 겹침으로, 절정) → 처음으로.
        /// </summary>
        private static Score ComposeGuardian()
        {
            var s = new Score(96, 24, 401);
            Inst trombone = s.Add(new Inst { Name = "trombone", Wave = Wave.Brass, MaxPartials = 36, Attack = 0.05, Decay = 0.4, Sustain = 0.8, Release = 0.18, Gate = 0.96, Gain = 0.30, Send = 0.22, Cutoff = 3200, DarkCutoff = 500, BrightBase = 0.5, BrightDecay = 0.35, Unison = 2, DetuneCents = 6 });
            Inst tuba = s.Add(new Inst { Name = "tuba", Wave = Wave.Brass, MaxPartials = 30, Attack = 0.06, Decay = 0.4, Sustain = 0.8, Release = 0.2, Gate = 0.96, Gain = 0.18, Send = 0.12, Cutoff = 1800, DarkCutoff = 350, BrightBase = 0.4, BrightDecay = 0.3 });
            Inst horn = s.Add(new Inst { Name = "horn", Wave = Wave.Brass, MaxPartials = 24, Attack = 0.06, Decay = 0.5, Sustain = 0.85, Release = 0.2, Gate = 0.97, Gain = 0.30, Send = 0.3, Cutoff = 2600, DarkCutoff = 650, BrightBase = 0.55, BrightDecay = 0.4, Unison = 2, DetuneCents = 5, VibratoDepth = 0.08, VibratoDelay = 0.3, VibratoRate = 5 });
            Inst trumpet = s.Add(new Inst { Name = "trumpet", Wave = Wave.Brass, MaxPartials = 20, Attack = 0.03, Decay = 0.4, Sustain = 0.8, Release = 0.15, Gate = 0.95, Gain = 0.20, Send = 0.28, Cutoff = 4200, DarkCutoff = 900, BrightBase = 0.6, BrightDecay = 0.3, VibratoDepth = 0.1, VibratoDelay = 0.25 });
            Inst brassPad = s.Add(new Inst { Name = "brassPad", Wave = Wave.Brass, MaxPartials = 30, Attack = 0.25, Decay = 0.8, Sustain = 0.9, Release = 0.4, Gate = 1.0, Gain = 0.10, Send = 0.3, Cutoff = 2400, DarkCutoff = 500, BrightBase = 0.5, BrightDecay = 0.8, Unison = 2, DetuneCents = 8 });
            Inst lowStrings = s.Add(new Inst { Name = "strings", Wave = Wave.Saw, MaxPartials = 40, Attack = 0.01, Decay = 0.12, Sustain = 0.3, Release = 0.06, Gate = 0.8, Gain = 0.38, Send = 0.1, Cutoff = 2400, Unison = 2, DetuneCents = 6 });
            Inst choir = s.Add(new Inst { Name = "choir", Wave = Wave.Choir, MaxPartials = 40, Attack = 0.35, Decay = 0.8, Sustain = 0.9, Release = 0.5, Gate = 1.0, Gain = 0.065, Send = 0.45, Cutoff = 3500, Unison = 2, DetuneCents = 10 });
            Inst stringPad = s.Add(new Inst { Name = "stringPad", Wave = Wave.Saw, MaxPartials = 24, Attack = 0.3, Decay = 0.8, Sustain = 0.85, Release = 0.4, Gate = 1.0, Gain = 0.045, Send = 0.4, Cutoff = 2000, Unison = 2, DetuneCents = 10 });

            Chord[] a = Prog("Cm Cm Ab Ab Fm Fm G G");
            Chord[] b = Prog("Cm Ab Eb Bb Cm Ab Fm G");
            Chord[] c = Prog("Db Db G G");
            Chord[] a2 = Prog("Cm Ab Fm G");

            const string Motif =
                "C3:1.5 C3:.5 Eb3:1 G3 | F3:1.5 Eb3:.5 D3:2 | C3:1.5 C3:.5 Eb3:1 Ab3 | G3:1.5 F3:.5 Eb3:2 |" +
                "F3:1.5 F3:.5 Ab3:1 C4 | Bb3:1.5 Ab3:.5 G3:2 | G3:.75 G3 G3:.5 B3:1 D4 | G3:.75 G3 G3:.5 r:2";
            const string Theme =
                "G4:1.5 C5:.5 C5:1 D5:.5 Eb5 | Eb5:1.5 D5:.5 C5:1 Ab4 | G4:1.5 Bb4:.5 Eb5:1 D5:.5 C5 | D5:2.5 C5:.5 Bb4:1 |" +
                "G4:1.5 C5:.5 C5:1 D5:.5 Eb5 | F5:1.5 Eb5:.5 D5:1 C5 | Ab5:1.5 G5:.5 F5:1 Eb5:.5 D5 | D5:2 B4";
            const string Bridge = "Ab4:1.5 F4:.5 Db5:2 | C5:1.5 Ab4:.5 F4:2 | G4:1.5 B4:.5 D5:2 | F5:2 D5:1 B4";
            const string Climax = "G4:1.5 C5:.5 C5:1 D5:.5 Eb5 | Eb5:1.5 D5:.5 C5:1 Ab4 | Ab5:1.5 G5:.5 F5:1 Eb5:.5 D5 | D5:2 B4";
            const string ClimaxLow = "C3:1.5 C3:.5 Eb3:1 G3 | Ab2:1.5 Ab2:.5 C3:1 Eb3 | F3:1.5 F3:.5 Ab3:1 C4 | G3:.75 G3 G3:.5 B3:1 D4";

            // A (0~7)
            s.Line(trombone, 0, Motif);
            s.Line(tuba, 0, Motif, 1f, -12);
            s.Pad(stringPad, 0, a, 55, 1f);
            s.Bass(lowStrings, 0, a, 36, "R r r R r r R r", 12);
            s.Drum(Kit.Taiko, 0, 6, "X.....x.....x...");
            s.Drum(Kit.Taiko, 6, 2, "X.....x.....x.x.");
            s.Drum(Kit.TaikoHi, 0, 8, "...o.....o....o.");
            s.Drum(Kit.Gong, 0, 1, "X...............");
            for (int bar = 0; bar < 6; bar++) s.Timpani(s.BeatOf(bar), a[bar], 0.8f);
            s.TimpaniRoll(s.BeatOf(7), 4, a[7], 0.3f, 0.9f);

            // B (8~15) — 호른 주제
            s.Line(horn, 8, Theme);
            s.Pad(brassPad, 8, b, 48, 1f);
            s.Pad(choir, 8, b, 60, 1f);
            s.Bass(lowStrings, 8, b, 36, "R r r R r r R r", 12);
            s.Drum(Kit.Taiko, 8, 8, "X.....x.....x...");
            s.Drum(Kit.TaikoHi, 8, 8, "..o.o...o.o...xx");
            s.Drum(Kit.Snare, 8, 8, "o.o.X.o.o.o.X.o.", 0.55f);
            s.Drum(Kit.Crash, 8, 1, "X...............");
            s.Drum(Kit.Crash, 12, 1, "X...............", 0.7f);

            // C (16~19) — D♭ → G, 팀파니 롤
            s.Line(horn, 16, Bridge);
            s.Pad(brassPad, 16, c, 48, 1.2f);
            s.Pad(choir, 16, c, 60, 1.1f);
            s.Bass(lowStrings, 16, c, 36, "R - - - - - - -", 12);
            s.Drum(Kit.Gong, 16, 1, "X...............");
            s.Drum(Kit.Taiko, 16, 2, "X.......X.......");
            s.Drum(Kit.Taiko, 18, 1, "X.....x.....x...");
            for (int i = 0; i < 8; i++) s.Timpani(s.BeatOf(18) + i * 0.5, c[2], 0.5f + i * 0.04f);
            s.TimpaniRoll(s.BeatOf(19), 4, c[3], 0.4f, 1f);
            s.Roll(Kit.Snare, s.BeatOf(19), 4, 0.125, 0.15f, 0.7f);
            s.Riser(s.BeatOf(18), 8, 0.5f);

            // A2 (20~23) — 절정
            s.Line(horn, 20, Climax);
            s.Line(trumpet, 20, Climax, 0.9f);
            s.Line(trombone, 20, ClimaxLow);
            s.Line(tuba, 20, ClimaxLow, 1f, -12);
            s.Pad(choir, 20, a2, 60, 1.1f);
            s.Bass(lowStrings, 20, a2, 36, "R r r R r r R r", 12);
            s.Drum(Kit.Taiko, 20, 4, "X.....x.....x...");
            s.Drum(Kit.TaikoHi, 20, 4, "..o.o...o.o...xx");
            s.Drum(Kit.Snare, 20, 4, "o.o.X.o.o.o.X.o.", 0.6f);
            s.Drum(Kit.Crash, 20, 1, "X...............");
            for (int bar = 20; bar < 24; bar++) s.Timpani(s.BeatOf(bar), a2[bar - 20], 0.9f);
            return s;
        }

        /// <summary>
        /// 레이드 — D단조 120bpm, 24마디(48초). 예전 곡의 조성·템포·"웅장하고 위압적"을 잇되, 다섯이 함께 덤비는 전투라
        /// 16분 현 오스티나토가 끝없이 몰아친다. A(하프타임·금관 리드 호출) → A'(주제·합창) → B(F–C로 열렸다가 E♭–A로 조여 드는 대목) → 처음으로.
        /// </summary>
        private static Score ComposeRaid()
        {
            var s = new Score(120, 24, 99);
            Inst lead = s.Add(new Inst { Name = "lead", Wave = Wave.Brass, MaxPartials = 24, Attack = 0.025, Decay = 0.35, Sustain = 0.8, Release = 0.12, Gate = 0.95, Gain = 0.26, Send = 0.28, Cutoff = 4200, DarkCutoff = 900, BrightBase = 0.6, BrightDecay = 0.3, Unison = 2, DetuneCents = 6, VibratoDepth = 0.12, VibratoDelay = 0.22 });
            Inst ostinato = s.Add(new Inst { Name = "ostinato", Wave = Wave.Saw, MaxPartials = 20, Attack = 0.003, Decay = 0.08, Sustain = 0.15, Release = 0.03, Gate = 0.7, Gain = 0.30, Send = 0.18, Cutoff = 3400, Unison = 2, DetuneCents = 5 });
            Inst bass = s.Add(new Inst { Name = "bass", Wave = Wave.Saw, MaxPartials = 30, Attack = 0.004, Decay = 0.2, Sustain = 0.55, Release = 0.06, Gate = 0.85, Gain = 0.45, Send = 0.0, Cutoff = 2400, DarkCutoff = 400, BrightBase = 0.3, BrightDecay = 0.09 });
            Inst strings = s.Add(new Inst { Name = "strings", Wave = Wave.Saw, MaxPartials = 24, Attack = 0.25, Decay = 0.8, Sustain = 0.85, Release = 0.35, Gate = 1.0, Gain = 0.065, Send = 0.4, Cutoff = 2200, Unison = 2, DetuneCents = 10 });
            Inst choir = s.Add(new Inst { Name = "choir", Wave = Wave.Choir, MaxPartials = 40, Attack = 0.3, Decay = 0.8, Sustain = 0.9, Release = 0.45, Gate = 1.0, Gain = 0.085, Send = 0.45, Cutoff = 3500, Unison = 2, DetuneCents = 10 });

            Chord[] a = Prog("Dm Dm Bb Bb Gm Gm A A");
            Chord[] a2 = Prog("Dm Bb Gm A Dm Bb Gm A");
            Chord[] b = Prog("F C Dm Bb Gm Dm Eb A");

            const string Call =
                "r:4 | r:2 A4:.5 D5 F5 A5 | Bb5:3 A5:.5 G5 | F5:2 D5 |" +
                "G5:1.5 Bb5:.5 D6:1 C6:.5 Bb5 | A5:2 G5:1 F5 | E5:1.5 F5:.5 G5:1 A5 | C#6:2 A5:1 E5";
            const string Theme =
                "D5:1 F5:.5 A5 D6:1 C6:.5 A5 | Bb5:1.5 A5:.5 G5:1 F5 | G5:1 Bb5:.5 D6 G5:1 F5:.5 E5 | E5:1.5 C#5:.5 A4:2 |" +
                "D5:1 F5:.5 A5 D6:1 C6:.5 D6 | Bb5:1.5 C6:.5 D6:1 Bb5 | A5:1 G5:.5 F5 G5:1 A5 | A5:1.5 G5:.5 F5:1 E5";
            const string Lift =
                "A5:2 C6:1 A5 | G5:2 E5:1 C5 | F5:1.5 E5:.5 D5:1 F5 | D5:2 F5:1 Bb5 |" +
                "Bb5:2 A5:1 G5 | A5:1.5 F5:.5 D5:2 | G5:1.5 Bb5:.5 Eb6:1 D6:.5 C6 | C#6:1 A5 E5 C#5";
            const string Spiccato = "0020102000201032";
            const string Accent = "X..X..X.X..X..X.";

            // A (0~7) — 하프타임
            s.Line(lead, 0, Call);
            s.Arp(ostinato, 0, a, 62, Spiccato, 1f, Accent);
            s.Pad(strings, 0, a, 57, 1f);
            s.Bass(bass, 0, a, 38, "R r r R r r R r");
            s.Drum(Kit.Kick, 0, 8, "X.......X.x.....");
            s.Drum(Kit.Snare, 0, 8, "........X.......");
            s.Drum(Kit.HatClosed, 0, 8, "x.x.x.x.x.x.x.x.");
            s.Drum(Kit.Crash, 0, 1, "X...............");
            s.Drum(Kit.Crash, 4, 1, "X...............", 0.7f);
            s.Drum(Kit.TomLo, 7, 1, "........X.X.XxXx");

            // A' (8~15) — 주제
            s.Line(lead, 8, Theme);
            s.Arp(ostinato, 8, a2, 62, Spiccato, 1f, Accent);
            s.Pad(choir, 8, a2, 57, 1f);
            s.Pad(strings, 8, a2, 57, 0.8f);
            s.Bass(bass, 8, a2, 38, "R r O R r O R r");
            s.Drum(Kit.Kick, 8, 8, "X.....x.X.x...x.");
            s.Drum(Kit.Snare, 8, 8, "....X.......X...");
            s.Drum(Kit.HatClosed, 8, 8, "xoxoxoxoxoxoxoxo");
            s.Drum(Kit.Crash, 8, 1, "X...............");
            s.Drum(Kit.Crash, 12, 1, "X...............", 0.7f);
            s.Drum(Kit.TomHi, 15, 1, "........XxXx....");
            s.Drum(Kit.TomLo, 15, 1, "............XxXx");

            // B (16~23)
            s.Line(lead, 16, Lift);
            s.Arp(ostinato, 16, b, 62, Spiccato, 0.9f, Accent);
            s.Pad(choir, 16, b, 57, 1.1f);
            s.Pad(strings, 16, b, 57, 1f);
            s.Bass(bass, 16, b, 38, "R - - R - - R -");
            s.Drum(Kit.Kick, 16, 8, "X.......X.......");
            s.Drum(Kit.Snare, 16, 7, "....X.......X...");
            s.Drum(Kit.TomLo, 16, 7, "X.....X.....X...", 0.7f);
            s.Drum(Kit.HatClosed, 16, 8, "x.x.x.x.x.x.x...");
            s.Drum(Kit.HatOpen, 16, 8, "..............x.");
            s.Drum(Kit.Crash, 16, 1, "X...............");
            s.Drum(Kit.Crash, 20, 1, "X...............", 0.7f);
            s.Drum(Kit.Snare, 23, 1, "....X...........");
            s.Roll(Kit.Snare, s.BeatOf(23) + 2, 2, 0.25, 0.5f, 1f);
            return s;
        }

        /// <summary>
        /// 명부회 간부전 — D단조 138bpm, 28마디(약 48.7초). "사무적인 냉정함": 시계 똑딱(나무토막), 타자기 같은 16분 하이햇,
        /// 차가운 종(글로켄)과 비브라토 없는 사각파, D 페달 위의 내려가는 아르페지오. 감정이 없는 정확함.
        /// A(D 페달) → B(D–C–B♭–A 하행 라멘토, 금관 도장 찍기) → A'(완전4도 평행 — 기계적) → C(E♭→A, 3-3-2 도장) → 처음으로.
        /// </summary>
        private static Score ComposeLedger()
        {
            var s = new Score(138, 28, 353);
            Inst bell = s.Add(new Inst { Name = "bell", Mallet = true, Ratios = new[] { 1.0, 2.76, 5.40, 8.93 }, Amps = new[] { 1.0, 0.45, 0.22, 0.1 }, Decays = new[] { 0.9, 0.35, 0.15, 0.06 }, Release = 0.1, Gate = 1.0, Gain = 0.28, Send = 0.3 });
            Inst lead = s.Add(new Inst { Name = "lead", Wave = Wave.Square, MaxPartials = 12, Attack = 0.004, Decay = 0.25, Sustain = 0.6, Release = 0.06, Gate = 0.88, Gain = 0.17, Send = 0.2, Cutoff = 3600 });
            Inst fourth = s.Add(new Inst { Name = "fourth", Wave = Wave.Square, MaxPartials = 10, Attack = 0.004, Decay = 0.25, Sustain = 0.6, Release = 0.06, Gate = 0.88, Gain = 0.11, Send = 0.2, Cutoff = 3000 });
            Inst stamp = s.Add(new Inst { Name = "stamp", Wave = Wave.Brass, MaxPartials = 28, Attack = 0.012, Decay = 0.12, Sustain = 0.3, Release = 0.06, Gate = 0.6, Gain = 0.2, Send = 0.2, Cutoff = 3600, DarkCutoff = 800, BrightBase = 0.4, BrightDecay = 0.08, Unison = 2, DetuneCents = 6 });
            Inst pluck = s.Add(new Inst { Name = "pluck", Wave = Wave.Triangle, MaxPartials = 12, Attack = 0.002, Decay = 0.08, Sustain = 0.0, Release = 0.03, Gate = 1.0, Gain = 0.26, Send = 0.15, Cutoff = 5000 });
            Inst bass = s.Add(new Inst { Name = "bass", Wave = Wave.Square, MaxPartials = 25, Attack = 0.003, Decay = 0.1, Sustain = 0.4, Release = 0.04, Gate = 0.6, Gain = 0.36, Send = 0.0, Cutoff = 2400, DarkCutoff = 500, BrightBase = 0.35, BrightDecay = 0.06 });
            Inst pad = s.Add(new Inst { Name = "pad", Wave = Wave.Saw, MaxPartials = 20, Attack = 0.2, Decay = 0.6, Sustain = 0.8, Release = 0.3, Gate = 1.0, Gain = 0.05, Send = 0.35, Cutoff = 1800, Unison = 2, DetuneCents = 7 });

            Chord[] a = Prog("Dm Gm/D Dm Gm/D Bb C A A");
            Chord[] b = Prog("Dm C Bb A Dm C Bb A");
            Chord[] c = Prog("Eb Eb A A");

            const string Ledger =
                "D5:1.5 E5:.5 F5:1 E5 | D5:1.5 C5:.5 Bb4:2 | D5:1.5 E5:.5 F5:1 G5:.5 A5 | G5:1.5 F5:.5 E5:2 |" +
                "D6:1.5 C6:.5 Bb5:1 F5 | E5:1.5 G5:.5 C6:2 | C#6:1 E5 A5 G5 | F5:1 E5 C#5 A4";
            const string Ledger2 =
                "D5:1.5 E5:.5 F5:1 E5 | D5:1.5 C5:.5 Bb4:2 | D5:1.5 E5:.5 F5:1 G5:.5 A5 | G5:1.5 F5:.5 E5:2 |" +
                "D6:1.5 C6:.5 Bb5:1 F5 | E5:1.5 G5:.5 C6:2 | C#6:1 E5 A5 G5 | A5:.5 G5 F5 E5 D5 C#5 Bb4 A4";
            const string Lament =
                "A5:1 A5 A5:.5 G5 F5:1 | G5:1 G5 G5:.5 F5 E5:1 | F5:1 F5 F5:.5 E5 D5:1 | E5:2 C#5 |" +
                "A5:1 A5 A5:.5 Bb5 C6:1 | G5:1 G5 G5:.5 A5 Bb5:1 | F5:1 D5 Bb4 D5 | C#5:1 E5 A5:2";
            const string Seal = "Bb4:1.5 Eb5:.5 G5:2 | F5:1.5 Eb5:.5 D5:2 | C#5:1.5 E5:.5 A5:2 | G5:1 F5 E5 C#5";

            // 시계는 처음부터 끝까지 — 장부를 넘기는 소리
            s.Drum(Kit.WoodHi, 0, 28, "X.......X.......", 0.75f);
            s.Drum(Kit.WoodLo, 0, 28, "....x.......x...", 0.75f);

            // A (0~7) — D 페달
            s.Line(bell, 0, Ledger);
            s.Line(lead, 0, Ledger, 0.9f);
            s.Arp(pluck, 0, a, 62, "3212321232123212", 1f, "X...X...X...X...");
            s.Pad(pad, 0, a, 57, 1f);
            s.Bass(bass, 0, a, 38, "R O R O R O R O");
            s.Drum(Kit.Kick, 0, 8, "X.......X.......");
            s.Drum(Kit.Snare, 0, 8, "....X.......X...");
            s.Drum(Kit.HatClosed, 0, 8, "xoxoxxoxoxoxxoxo");
            s.Drum(Kit.Crash, 0, 1, "X...............");

            // B (8~15) — 하행 라멘토, 금관 도장
            s.Line(bell, 8, Lament, 0.8f);
            s.Line(stamp, 8, Lament, 1f);
            s.Arp(pluck, 8, b, 62, "3212321232123212", 0.8f, "X...X...X...X...");
            s.Pad(pad, 8, b, 57, 1f);
            s.Bass(bass, 8, b, 38, "R R R R R R R R");
            s.Drum(Kit.Kick, 8, 8, "X...X...X...X...");
            s.Drum(Kit.Snare, 8, 8, "....X.......X...");
            s.Drum(Kit.Clap, 8, 8, "....X.......X...", 0.6f);
            s.Drum(Kit.HatClosed, 8, 8, "x.x.x.x.x.x.x.x.");
            s.Drum(Kit.Crash, 8, 1, "X...............");

            // A' (16~23) — 완전4도 평행
            s.Line(bell, 16, Ledger2);
            s.Line(lead, 16, Ledger2, 0.9f);
            s.Line(fourth, 16, Ledger2, 1f, -5);
            s.Arp(pluck, 16, a, 62, "3212321232123212", 1f, "X...X...X...X...");
            s.Pad(pad, 16, a, 57, 1f);
            s.Bass(bass, 16, a, 38, "R O R O R O R O");
            s.Drum(Kit.Kick, 16, 8, "X...X...X...X...");
            s.Drum(Kit.Snare, 16, 8, "....X.......X..x");
            s.Drum(Kit.HatClosed, 16, 8, "xoxoxxoxoxoxxoxo");
            s.Drum(Kit.Crash, 16, 1, "X...............");
            s.Drum(Kit.Crash, 20, 1, "X...............", 0.7f);

            // C (24~27) — 도장 3-3-2
            s.Line(bell, 24, Seal);
            s.Stabs(stamp, 24, c, 51, "X.....X.....X...", 1.2f);
            s.Pad(pad, 24, c, 57, 1.2f);
            s.Bass(bass, 24, c, 38, "R - - R - - R -");
            s.Drum(Kit.Kick, 24, 4, "X.....X.....X...");
            s.Drum(Kit.Snare, 24, 3, "X.....X.....X...", 0.85f);
            s.Drum(Kit.Crash, 24, 1, "X...............");
            s.Drum(Kit.Snare, 27, 1, "X.....X.........");
            s.Roll(Kit.Snare, s.BeatOf(27) + 2, 2, 0.25, 0.45f, 1f);
            return s;
        }

        /// <summary>
        /// 최종전(관장 하월·무명) — C단조 152bpm, 32마디(약 50.5초). 반음 충돌(C–D♭–C–B)을 섞은 16분 오스티나토가 끝없이 흔들고,
        /// 그 위로 어두운 찬가가 선다. A(i–♭II 흔들림) → A'(합창 겹침·킥 8분) → B(찬가) → C(심장 박동과 종소리만 — 숨 고르기)
        /// → D(G 페달 위 롤·금관 8분으로 끌어올림) → 처음으로.
        /// </summary>
        private static Score ComposeFinal()
        {
            var s = new Score(152, 32, 379);
            Inst lead = s.Add(new Inst { Name = "lead", Wave = Wave.Brass, MaxPartials = 24, Attack = 0.02, Decay = 0.3, Sustain = 0.8, Release = 0.1, Gate = 0.94, Gain = 0.26, Send = 0.28, Cutoff = 4400, DarkCutoff = 900, BrightBase = 0.6, BrightDecay = 0.25, Unison = 2, DetuneCents = 8, VibratoDepth = 0.14, VibratoDelay = 0.2 });
            Inst ostinato = s.Add(new Inst { Name = "ostinato", Wave = Wave.Saw, MaxPartials = 22, Attack = 0.002, Decay = 0.07, Sustain = 0.1, Release = 0.03, Gate = 0.75, Gain = 0.26, Send = 0.15, Cutoff = 3600, Drive = 1.8 });
            Inst bass = s.Add(new Inst { Name = "bass", Wave = Wave.Saw, MaxPartials = 30, Attack = 0.003, Decay = 0.18, Sustain = 0.6, Release = 0.05, Gate = 0.85, Gain = 0.38, Send = 0.0, Cutoff = 2200, DarkCutoff = 500, BrightBase = 0.35, BrightDecay = 0.08, Drive = 2.2 });
            Inst choir = s.Add(new Inst { Name = "choir", Wave = Wave.Choir, MaxPartials = 40, Attack = 0.3, Decay = 0.8, Sustain = 0.9, Release = 0.45, Gate = 1.0, Gain = 0.10, Send = 0.45, Cutoff = 3500, Unison = 2, DetuneCents = 12 });
            Inst strings = s.Add(new Inst { Name = "strings", Wave = Wave.Saw, MaxPartials = 24, Attack = 0.2, Decay = 0.8, Sustain = 0.85, Release = 0.3, Gate = 1.0, Gain = 0.06, Send = 0.35, Cutoff = 2200, Unison = 2, DetuneCents = 10 });
            Inst stab = s.Add(new Inst { Name = "stab", Wave = Wave.Brass, MaxPartials = 28, Attack = 0.01, Decay = 0.12, Sustain = 0.3, Release = 0.06, Gate = 0.7, Gain = 0.24, Send = 0.2, Cutoff = 3800, DarkCutoff = 800, BrightBase = 0.4, BrightDecay = 0.08, Unison = 2, DetuneCents = 8 });
            Inst toll = s.Add(new Inst { Name = "toll", Mallet = true, Ratios = new[] { 1.0, 2.0, 3.01, 4.17, 5.43 }, Amps = new[] { 1.0, 0.6, 0.35, 0.25, 0.12 }, Decays = new[] { 2.2, 1.6, 0.9, 0.6, 0.35 }, Release = 0.3, Gate = 1.0, Gain = 0.18, Send = 0.4 });

            Chord[] a = Prog("Cm Db Cm Db Ab Fm G G");
            Chord[] b = Prog("Ab Eb Fm Db Ab Eb G G");
            Chord[] c = Prog("Db Db Cm Cm");
            Chord[] d = Prog("G G G G");
            int[] shake = { 0, 1, 0, -1, 0, 1, 0, -5 };

            const string Hymn =
                "C5:1.5 Eb5:.5 G5:1 F#5 | F5:1.5 Ab5:.5 Db6:1 C6 | G5:1.5 Eb5:.5 C5:1 B4 | Db5:2 C5 |" +
                "Eb5:1.5 Ab5:.5 C6:1 Bb5:.5 Ab5 | F5:1.5 Ab5:.5 C6:1 Db6 | B5:1.5 Ab5:.5 G5:1 F5 | D5:1 F5 Ab5 B5";
            const string Hymn2 =
                "C5:1.5 Eb5:.5 G5:1 F#5 | F5:1.5 Ab5:.5 Db6:1 C6 | G5:1.5 Eb5:.5 C5:1 B4 | Db5:2 C5 |" +
                "Eb5:1.5 Ab5:.5 C6:1 Bb5:.5 Ab5 | F5:1.5 Ab5:.5 C6:1 Db6 | B5:1.5 Ab5:.5 G5:1 F5 | B5:1 Ab5 F5 D5";
            const string Anthem =
                "C6:2 Bb5:1 Ab5 | G5:2 Bb5:1 Eb6 | Db6:1.5 C6:.5 Ab5:1 F5 | Ab5:3 F5:1 |" +
                "C6:2 Bb5:1 Ab5 | G5:2 Bb5:1 Eb6 | D6:1.5 C6:.5 B5:1 Ab5 | G5:2 r";

            // A (0~7)
            s.Line(lead, 0, Hymn);
            s.Figure(ostinato, 0, a, 60, shake);
            s.Pad(strings, 0, a, 55, 1f);
            s.Bass(bass, 0, a, 36, "R r R r R r R r");
            s.Drum(Kit.Kick, 0, 8, "X.....x.X.x...x.");
            s.Drum(Kit.Snare, 0, 8, "....X.......X...");
            s.Drum(Kit.HatClosed, 0, 8, "xoxoxoxoxoxoxoxo");
            s.Drum(Kit.Crash, 0, 1, "X...............");
            s.Drum(Kit.Crash, 4, 1, "X...............", 0.7f);

            // A' (8~15) — 합창 겹침, 킥 8분
            s.Line(lead, 8, Hymn2);
            s.Line(choir, 8, Hymn2, 0.9f, -12);
            s.Figure(ostinato, 8, a, 60, shake);
            s.Pad(strings, 8, a, 55, 1f);
            s.Bass(bass, 8, a, 36, "R r O r R r O r");
            s.Drum(Kit.Kick, 8, 8, "X.x.X.x.X.x.X.x.");
            s.Drum(Kit.Snare, 8, 8, "....X.......X...");
            s.Drum(Kit.HatClosed, 8, 8, "x.x.x.x.x.x.x.x.");
            s.Drum(Kit.Crash, 8, 1, "X...............");
            s.Drum(Kit.Crash, 12, 1, "X...............", 0.7f);
            s.Drum(Kit.TomHi, 15, 1, "........XxXx....");
            s.Drum(Kit.TomLo, 15, 1, "............XxXx");

            // B (16~23) — 찬가
            s.Line(lead, 16, Anthem);
            s.Pad(choir, 16, b, 57, 1.1f);
            s.Pad(strings, 16, b, 55, 1f);
            s.Figure(ostinato, 16, b, 60, shake, 0.8f);
            s.Bass(bass, 16, b, 36, "R - R O R - R O");
            s.Drum(Kit.Kick, 16, 8, "X.......X.x.....");
            s.Drum(Kit.Snare, 16, 8, "....X.......X...");
            s.Drum(Kit.TomLo, 16, 8, "X.....X.....X...", 0.6f);
            s.Drum(Kit.HatClosed, 16, 8, "x.x.x.x.x.x.x.x.");
            s.Drum(Kit.Crash, 16, 1, "X...............");
            s.Drum(Kit.Crash, 20, 1, "X...............", 0.8f);

            // C (24~27) — 심장 박동과 종
            s.Pad(choir, 24, c, 57, 1.1f);
            s.Bass(bass, 24, c, 36, "R - - - - - - -", 0, 0.7f);
            s.Line(toll, 24, "Db5:4 | Db5:4 | C5:4 | C5:4", 1f);
            s.Drum(Kit.Kick, 24, 4, "X..x............");
            s.Drum(Kit.Kick, 26, 2, "........X..x....", 0.7f);

            // D (28~31) — G 페달, 끌어올리기
            s.Figure(ostinato, 28, d, 60, shake, 1.1f);
            s.Pad(strings, 28, d, 55, 1.2f);
            s.Bass(bass, 28, d, 36, "R R R R R R R R");
            s.Stabs(stab, 30, Slice(d, 2, 2), 55, "x.x.x.x.x.x.x.x.", 1f);
            s.Drum(Kit.Kick, 28, 4, "X...X...X...X...");
            s.Roll(Kit.Snare, s.BeatOf(28), 8, 0.5, 0.3f, 0.55f);
            s.Roll(Kit.Snare, s.BeatOf(30), 4, 0.25, 0.55f, 0.8f);
            s.Roll(Kit.Snare, s.BeatOf(31), 4, 0.125, 0.8f, 1f);
            s.Drum(Kit.TomLo, 31, 1, "X...X...X.X.XXXX", 0.8f);
            s.Riser(s.BeatOf(29), 12, 0.55f);
            return s;
        }

        // ════════════════════════════════════════════
        //  악보 — 음표 사건을 모아 두었다가 한 번에 합성한다
        // ════════════════════════════════════════════

        internal enum Wave { Saw, Square, Pulse25, Triangle, Brass, Choir, Organ }

        internal enum Kit { Kick, Snare, Clap, HatClosed, HatOpen, Crash, TomHi, TomLo, Taiko, TaikoHi, WoodHi, WoodLo, Gong, Timpani }

        /// <summary>악기 한 대. 음색표(배음 진폭) + 엔벨로프 + 비브라토 + 밝기 엔벨로프(금관의 "빠앙").</summary>
        internal sealed class Inst
        {
            public string Name;
            public int Id;
            public Wave Wave = Wave.Saw;
            public int MaxPartials = 20;
            public double Attack = 0.005, Decay = 0.2, Sustain = 0.7, Release = 0.08;
            /// <summary>음 길이 중 실제로 누르는 비율 — 1 미만이면 스타카토.</summary>
            public double Gate = 1.0;
            public double Gain = 0.25;
            /// <summary>잔향으로 보내는 비율.</summary>
            public double Send = 0.15;
            public double VibratoDepth, VibratoRate = 5.5, VibratoDelay = 0.15; // 깊이: 반음
            public int Unison = 1;
            public double DetuneCents;
            /// <summary>밝은 음색표의 저역통과(Hz).</summary>
            public double Cutoff = 6000;
            /// <summary>0보다 크면 어두운 음색표를 하나 더 만들어 소리 크기를 따라 섞는다(Hz).</summary>
            public double DarkCutoff;
            public double BrightBase = 0.5, BrightDecay = 0.2;
            /// <summary>포화 — 1보다 크면 tanh로 배음을 더한다.</summary>
            public double Drive;
            /// <summary>말렛·종 — 비정수배 배음(<see cref="Ratios"/>)을 각자 다른 빠르기로 감쇠.</summary>
            public bool Mallet;
            public double[] Ratios, Amps, Decays;
        }

        internal struct Chord
        {
            public int RootPc;
            public int BassPc;
            public int[] Intervals;
        }

        private struct NoteEvent { public Inst Inst; public int Start; public int Hold; public int Midi; public float Vel; }
        private struct HitEvent { public Kit Kit; public int Start; public float Vel; public int Midi; }
        private struct RiserEvent { public int Start; public int Length; public float Vel; }

        internal sealed class Score
        {
            public const int BeatsPerBar = 4;
            /// <summary>음색표·사인표 한 주기의 칸 수 — 가장 높은 배음(40번째)도 한 주기에 25칸이 넘는다.</summary>
            internal const int TableSize = 1024;

            public readonly double Bpm;
            public readonly int Bars;
            public readonly int Length;
            public double Swing;
            private readonly double samplesPerBeat;
            private readonly int seed;
            private readonly List<Inst> insts = new List<Inst>();
            private readonly List<NoteEvent> notes = new List<NoteEvent>();
            private readonly List<HitEvent> hits = new List<HitEvent>();
            private readonly List<RiserEvent> risers = new List<RiserEvent>();

            public Score(double bpm, int bars, int seed)
            {
                Bpm = bpm;
                Bars = bars;
                this.seed = seed;
                samplesPerBeat = MusicRate * 60.0 / bpm;
                Length = (int)Math.Round(bars * BeatsPerBar * samplesPerBeat);
            }

            public Inst Add(Inst inst)
            {
                inst.Id = insts.Count;
                insts.Add(inst);
                return inst;
            }

            public double BeatOf(int bar) => bar * (double)BeatsPerBar;

            /// <summary>박 → 샘플. 셔플이면 뒷박(반 박)을 늦춘다 — 박 안을 두 구간으로 나눠 늘이고 줄인다.</summary>
            public int At(double beat)
            {
                if (Swing > 0)
                {
                    double whole = Math.Floor(beat);
                    double f = beat - whole;
                    double split = 0.5 + Swing;
                    f = f <= 0.5 ? f * split / 0.5 : split + (f - 0.5) * (1.0 - split) / 0.5;
                    beat = whole + f;
                }
                return (int)Math.Round(beat * samplesPerBeat);
            }

            public void Note(Inst inst, double beat, double beats, int midi, float vel)
            {
                int start = At(beat);
                int hold = (int)Math.Round((At(beat + beats) - start) * inst.Gate);
                if (hold < 32) hold = 32;
                // 작은 셈여림 차이 — 같은 음이 기계처럼 똑같지 않게(결정적)
                uint h = (uint)(start * 2654435761u) ^ (uint)(midi * 40503) ^ (uint)seed;
                float jitter = 0.95f + (h % 1000u) / 10000f;
                notes.Add(new NoteEvent { Inst = inst, Start = start, Hold = hold, Midi = midi, Vel = vel * jitter });
            }

            /// <summary>
            /// 선율 한 줄. "A4:.5 C5 E5 A5:1 | …" — 음이름:박(생략하면 앞 음과 같은 길이), r은 쉼표, |는 마디 줄.
            /// <b>마디마다 4박이 맞는지 검사한다</b> — 틀리면 예외(테스트가 곡을 전부 합성하므로 저작 실수가 바로 잡힌다).
            /// </summary>
            public void Line(Inst inst, int bar, string text, float vel = 1f, int transpose = 0, int[] scale = null, int diatonicShift = 0)
            {
                double beat = BeatOf(bar);
                double barStart = beat;
                double dur = 1;
                string[] tokens = text.Replace("|", " | ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string token in tokens)
                {
                    if (token == "|")
                    {
                        CheckBar(beat - barStart, text);
                        barStart = beat;
                        continue;
                    }
                    ParseToken(token, ref dur, out int midi);
                    if (midi >= 0)
                    {
                        int m = midi + transpose;
                        if (scale != null && diatonicShift != 0) m = DiatonicShift(m, scale, diatonicShift);
                        double frac = beat - Math.Floor(beat);
                        float accent = frac < 1e-6 ? 1f : 0.9f;
                        Note(inst, beat, dur, m, vel * accent);
                    }
                    beat += dur;
                }
                CheckBar(beat - barStart, text);
            }

            /// <summary>마디 검사 없는 짧은 악구(대답·장식).</summary>
            public void Phrase(Inst inst, double beat, string text, float vel = 1f)
            {
                double dur = 1;
                foreach (string token in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    ParseToken(token, ref dur, out int midi);
                    if (midi >= 0) Note(inst, beat, dur, midi, vel);
                    beat += dur;
                }
            }

            private static void CheckBar(double beats, string text)
            {
                if (Math.Abs(beats - BeatsPerBar) > 1e-6)
                    throw new InvalidOperationException($"마디 길이가 {beats}박이다(4박이어야 한다): {text}");
            }

            /// <summary>코드를 한 마디씩 길게(패드). 음은 lowMidi부터 한 옥타브 안에 모아 성부 진행을 부드럽게.</summary>
            public void Pad(Inst inst, int bar, Chord[] chords, int lowMidi, float vel)
            {
                for (int i = 0; i < chords.Length; i++)
                    foreach (int m in Voicing(chords[i], lowMidi))
                        Note(inst, BeatOf(bar + i), BeatsPerBar, m, vel);
            }

            /// <summary>코드 찹 — 16분 칸 패턴(x/X)마다 짧게.</summary>
            public void Stabs(Inst inst, int bar, Chord[] chords, int lowMidi, string pattern, float vel = 1f)
            {
                pattern = pattern.Replace(" ", "");
                for (int i = 0; i < chords.Length; i++)
                {
                    int[] tones = Voicing(chords[i], lowMidi);
                    for (int step = 0; step < 16; step++)
                    {
                        float v = StepVel(pattern[step % pattern.Length]);
                        if (v <= 0f) continue;
                        foreach (int m in tones) Note(inst, BeatOf(bar + i) + step * 0.25, 0.25, m, vel * v);
                    }
                }
            }

            /// <summary>
            /// 베이스 — 8분 칸마다 한 글자: R 근음, O 옥타브 위, F 5도, T 3도(대문자 = 강세, 소문자 = 여리게),
            /// '-' 앞 음 늘이기, '.' 쉼. <paramref name="doubleUp"/>이 0이 아니면 그만큼 위 음을 함께 낸다(폰에서 들리게).
            /// </summary>
            public void Bass(Inst inst, int bar, Chord[] chords, int lowMidi, string pattern, int doubleUp = 0, float vel = 1f)
            {
                string[] cells = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (cells.Length != 8) throw new InvalidOperationException("베이스 패턴은 8칸이어야 한다: " + pattern);
                for (int i = 0; i < chords.Length; i++)
                {
                    int root = PlaceAbove(chords[i].BassPc, lowMidi);
                    int third = root + chords[i].Intervals[1];
                    for (int c = 0; c < 8; c++)
                    {
                        char ch = cells[c][0];
                        if (ch == '-' || ch == '.') continue;
                        int len = 1;
                        while (c + len < 8 && cells[c + len][0] == '-') len++;
                        int m;
                        switch (char.ToUpperInvariant(ch))
                        {
                            case 'O': m = root + 12; break;
                            case 'F': m = root + 7; break;
                            case 'T': m = third; break;
                            default: m = root; break;
                        }
                        float v = char.IsUpper(ch) ? 1f : 0.62f;
                        double beat = BeatOf(bar + i) + c * 0.5;
                        Note(inst, beat, len * 0.5, m, vel * v);
                        if (doubleUp != 0) Note(inst, beat, len * 0.5, m + doubleUp, vel * v * 0.6f);
                    }
                }
            }

            /// <summary>
            /// 아르페지오 — 16칸(16분), 숫자는 코드음 번호(0 = 가장 낮은 음, 화음 수 = 맨 아래 음의 옥타브 위), '.' 쉼.
            /// <paramref name="accent"/>가 있으면 그 칸 'X'만 세게, 나머지는 0.7.
            /// </summary>
            public void Arp(Inst inst, int bar, Chord[] chords, int lowMidi, string pattern, float vel = 1f, string accent = null)
            {
                for (int i = 0; i < chords.Length; i++)
                {
                    int[] v = Voicing(chords[i], lowMidi);
                    int[] tones = new int[v.Length + 1];
                    Array.Copy(v, tones, v.Length);
                    tones[v.Length] = v[0] + 12;
                    for (int step = 0; step < 16; step++)
                    {
                        char ch = pattern[step % pattern.Length];
                        if (ch < '0' || ch > '9') continue;
                        int idx = Math.Min(ch - '0', tones.Length - 1);
                        float a = accent == null ? 1f : (accent[step % accent.Length] == 'X' ? 1f : 0.7f);
                        Note(inst, BeatOf(bar + i) + step * 0.25, 0.25, tones[idx], vel * a);
                    }
                }
            }

            /// <summary>근음 기준 반음 수 무늬를 16분으로 두 번씩(최종전의 흔들림 C–D♭–C–B).</summary>
            public void Figure(Inst inst, int bar, Chord[] chords, int lowMidi, int[] semis, float vel = 1f)
            {
                for (int i = 0; i < chords.Length; i++)
                {
                    int root = PlaceAbove(chords[i].RootPc, lowMidi);
                    for (int step = 0; step < 16; step++)
                    {
                        float a = (step % 8 == 0) ? 1f : 0.72f;
                        Note(inst, BeatOf(bar + i) + step * 0.25, 0.25, root + semis[step % semis.Length], vel * a);
                    }
                }
            }

            /// <summary>북 — 16칸(16분) 패턴: X 세게, x 보통, o 여리게, '.' 없음.</summary>
            public void Drum(Kit kit, int bar, int bars, string pattern, float vel = 1f, int midi = 0)
            {
                pattern = pattern.Replace(" ", "");
                if (pattern.Length != 16) throw new InvalidOperationException("북 패턴은 16칸이어야 한다: " + pattern);
                for (int b = 0; b < bars; b++)
                    for (int step = 0; step < 16; step++)
                    {
                        float v = StepVel(pattern[step]);
                        if (v > 0f) Hit(kit, BeatOf(bar + b) + step * 0.25, vel * v, midi);
                    }
            }

            public void Hit(Kit kit, double beat, float vel, int midi = 0)
            {
                hits.Add(new HitEvent { Kit = kit, Start = At(beat), Vel = vel, Midi = midi });
            }

            /// <summary>롤 — 일정 간격으로 세기를 키우며.</summary>
            public void Roll(Kit kit, double beat, double beats, double step, float from, float to, int midi = 0)
            {
                int n = Math.Max(1, (int)Math.Round(beats / step));
                for (int i = 0; i < n; i++)
                    Hit(kit, beat + i * step, from + (to - from) * i / Math.Max(1, n - 1), midi);
            }

            public void Timpani(double beat, Chord chord, float vel) => Hit(Kit.Timpani, beat, vel, PlaceAbove(chord.RootPc, 36));

            public void TimpaniRoll(double beat, double beats, Chord chord, float from, float to) =>
                Roll(Kit.Timpani, beat, beats, 0.125, from, to, PlaceAbove(chord.RootPc, 36));

            /// <summary>솟구치는 노이즈 — 대목 앞에서 끌어올린다.</summary>
            public void Riser(double beat, double beats, float vel)
            {
                int start = At(beat);
                risers.Add(new RiserEvent { Start = start, Length = At(beat + beats) - start, Vel = vel });
            }

            // ── 합성 ──

            public float[] Render()
            {
                float[] dry = new float[Length];
                float[] wet = new float[Length];
                var tables = new Dictionary<long, float[]>();

                // 같은 (악기, 음, 길이)는 한 번만 합성해 그 자리들에 모두 섞고 버린다 — 합성한 음을 쌓아 두면
                // 수문장 곡에서 수십 MB가 한꺼번에 살아 있었다(큰 객체 힙 할당이 GC를 부른다).
                var noteGroups = new Dictionary<long, List<int>>();
                for (int e = 0; e < notes.Count; e++)
                {
                    NoteEvent n = notes[e];
                    long key = ((long)n.Inst.Id << 48) | ((long)(n.Midi & 0xFF) << 32) | (uint)n.Hold;
                    if (!noteGroups.TryGetValue(key, out List<int> list)) noteGroups[key] = list = new List<int>();
                    list.Add(e);
                }
                foreach (List<int> group in noteGroups.Values)
                {
                    NoteEvent first = notes[group[0]];
                    int len = RenderNote(first.Inst, first.Midi, first.Hold, tables);
                    foreach (int e in group)
                    {
                        NoteEvent n = notes[e];
                        MixWrapped(dry, wet, scratchNote, len, n.Start, (float)(n.Vel * n.Inst.Gain), (float)n.Inst.Send);
                    }
                }

                // 북도 같은 방식 — 노이즈 북은 변형 몇 벌을 돌려 써서 기관총처럼 똑같이 들리지 않게 한다.
                var hitGroups = new Dictionary<long, List<int>>();
                int hitIndex = 0;
                for (int e = 0; e < hits.Count; e++)
                {
                    HitEvent h = hits[e];
                    int variants = NoiseVariants(h.Kit);
                    int variant = variants > 1 ? hitIndex++ % variants : 0;
                    long key = ((long)h.Kit << 40) | ((long)variant << 32) | (uint)h.Midi;
                    if (!hitGroups.TryGetValue(key, out List<int> list)) hitGroups[key] = list = new List<int>();
                    list.Add(e);
                }
                foreach (KeyValuePair<long, List<int>> group in hitGroups)
                {
                    HitEvent first = hits[group.Value[0]];
                    int variant = (int)((group.Key >> 32) & 0xFF);
                    float[] buf = DrumSynth.Render(first.Kit, first.Midi, seed * 31 + variant * 7 + (int)first.Kit);
                    DrumSynth.Levels(first.Kit, out float gain, out float send);
                    foreach (int e in group.Value)
                        MixWrapped(dry, wet, buf, buf.Length, hits[e].Start, gain * hits[e].Vel, send);
                }

                foreach (RiserEvent r in risers)
                {
                    float[] buf = DrumSynth.Riser(r.Length, seed + r.Start);
                    MixWrapped(dry, wet, buf, buf.Length, r.Start, 0.22f * r.Vel, 0.6f);
                }

                AddReverb(wet, dry);
                Master(dry);
                return dry;
            }

            private static int NoiseVariants(Kit kit)
            {
                switch (kit)
                {
                    case Kit.Snare: case Kit.HatClosed: case Kit.Clap: return 3;
                    default: return 1;
                }
            }

            /// <summary>곡 끝을 넘는 부분은 첫머리에 감아서 더한다 — 루프 이음매가 곡 안의 아무 두 샘플 사이처럼 이어진다.</summary>
            private static void MixWrapped(float[] dry, float[] wet, float[] src, int srcLength, int start, float gain, float send)
            {
                int len = dry.Length;
                int idx = start % len;
                if (idx < 0) idx += len;
                int n = Math.Min(srcLength, len);
                int first = Math.Min(n, len - idx);
                for (int j = 0; j < first; j++) dry[idx + j] += src[j] * gain;
                for (int j = first; j < n; j++) dry[j - first] += src[j] * gain;
                float sendGain = gain * send;
                if (sendGain == 0f) return;
                for (int j = 0; j < first; j++) wet[idx + j] += src[j] * sendGain;
                for (int j = first; j < n; j++) wet[j - first] += src[j] * sendGain;
            }

            // 음 하나를 합성할 때 쓰는 작업 버퍼 — 음마다 새로 잡지 않는다(악보 하나가 스레드 하나라 공유해도 안전).
            private float[] scratchEnv = new float[0], scratchMix = new float[0], scratchNote = new float[0];

            private static float[] Grow(float[] a, int n) => a.Length >= n ? a : new float[n];

            /// <summary>
            /// 음 하나를 <see cref="scratchNote"/>에 합성하고 길이를 돌려준다 — 엔벨로프·밝기를 먼저 한 줄로 깔고,
            /// 유니즌 목소리마다 표를 훑어 더한다(목소리 단위 반복이 빠르다).
            /// </summary>
            private int RenderNote(Inst inst, int midi, int hold, Dictionary<long, float[]> tables)
            {
                double f0 = 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);
                int release = Math.Max(16, (int)(inst.Release * MusicRate));
                int total = hold + release;
                scratchNote = Grow(scratchNote, total);
                float[] buf = scratchNote;
                Array.Clear(buf, 0, total);
                if (inst.Mallet)
                {
                    RenderMallet(inst, f0, hold, total, buf);
                    return total;
                }

                float[] bright = Table(inst, midi, f0, inst.Cutoff, 0, tables);
                float[] dark = inst.DarkCutoff > 0 ? Table(inst, midi, f0, inst.DarkCutoff, 1, tables) : null;

                // 엔벨로프(선형 어택 → 지수 감쇠 → 서스테인, 놓으면 제곱 꼴로 0) + 소리 크기를 따라가는 밝기
                scratchEnv = Grow(scratchEnv, total);
                float[] env = scratchEnv;
                float[] mix = null;
                if (dark != null) { scratchMix = Grow(scratchMix, total); mix = scratchMix; }
                int attack = Math.Max(8, (int)(inst.Attack * MusicRate));
                double decayMul = Math.Exp(-1.0 / Math.Max(1.0, inst.Decay * MusicRate));
                double brightMul = Math.Exp(-1.0 / Math.Max(1.0, inst.BrightDecay * MusicRate));
                double decayPart = 1.0, brightPart = 1.0, level = 0, holdLevel = 0;
                for (int j = 0; j < total; j++)
                {
                    if (j < hold)
                    {
                        if (j < attack) level = (double)j / attack;
                        else
                        {
                            decayPart *= decayMul;
                            level = inst.Sustain + (1.0 - inst.Sustain) * decayPart;
                        }
                        holdLevel = level;
                    }
                    else
                    {
                        double r = 1.0 - (double)(j - hold) / release;
                        level = holdLevel * r * r;
                    }
                    env[j] = (float)level;
                    if (mix != null)
                    {
                        brightPart *= brightMul;
                        double m = level * (inst.BrightBase + (1.0 - inst.BrightBase) * brightPart);
                        mix[j] = (float)(m > 1 ? 1 : m);
                    }
                }

                // 비브라토는 목소리 고리 안에서 바로 굴린다 — 샘플별 배율을 큰 배열에 깔아 두고 읽으면 이 고리가 4배 느려졌다(실측).
                double vibAmt = inst.VibratoDepth * 0.05776; // 반음 → 주파수 비(2^(1/12) - 1)
                double vibInc = inst.VibratoRate / MusicRate;
                int vibDelay = vibAmt > 0 ? (int)(inst.VibratoDelay * MusicRate) : int.MaxValue;
                double rampLen = 0.25 * MusicRate;
                float[] sine = SineTable; // 정적 필드는 고리 밖에서 한 번만 읽는다
                int voices = Math.Max(1, inst.Unison);
                for (int v = 0; v < voices; v++)
                {
                    double cents = voices == 1 ? 0 : inst.DetuneCents * (2.0 * v / (voices - 1) - 1.0);
                    double inc = f0 / MusicRate * Math.Pow(2.0, cents / 1200.0);
                    double p = (double)v / voices, vibPhase = 0;
                    for (int j = 0; j < total; j++)
                    {
                        if (j > vibDelay)
                        {
                            vibPhase += vibInc;
                            if (vibPhase >= 1) vibPhase -= 1;
                            int k = j - vibDelay;
                            double ramp = k < rampLen ? k / rampLen : 1.0;
                            p += inc * (1.0 + vibAmt * ramp * Lookup(sine, vibPhase));
                        }
                        else p += inc;
                        if (p >= 1) p -= 1;
                        double x = p * TableSize;
                        int i = (int)x;
                        float fr = (float)(x - i);
                        float sb = bright[i] + (bright[i + 1] - bright[i]) * fr;
                        if (mix != null)
                        {
                            float sd = dark[i] + (dark[i + 1] - dark[i]) * fr;
                            sb = sd + (sb - sd) * mix[j];
                        }
                        buf[j] += sb;
                    }
                }

                float voiceNorm = (float)(1.0 / Math.Sqrt(voices));
                if (inst.Drive > 1.0)
                {
                    float drive = (float)inst.Drive, driveNorm = 1f / SoftClip(drive);
                    for (int j = 0; j < total; j++) buf[j] = SoftClip(buf[j] * voiceNorm * drive) * driveNorm * env[j];
                }
                else
                {
                    for (int j = 0; j < total; j++) buf[j] *= voiceNorm * env[j];
                }
                return total;
            }

            /// <summary>tanh 근사(±3 밖은 ±1) — 포화에 쓴다. Math.Tanh를 샘플마다 부르지 않는다.</summary>
            internal static float SoftClip(float x)
            {
                if (x > 3f) return 1f;
                if (x < -3f) return -1f;
                float x2 = x * x;
                return x * (27f + x2) / (27f + 9f * x2);
            }

            /// <summary>말렛·종 — 비정수배 배음을 각자 감쇠. 나이퀴스트 근처 배음은 뺀다.</summary>
            private static void RenderMallet(Inst inst, double f0, int hold, int total, float[] buf)
            {
                int release = total - hold;
                float[] sine = SineTable;
                for (int p = 0; p < inst.Ratios.Length; p++)
                {
                    double f = f0 * inst.Ratios[p];
                    if (f > MusicRate * 0.42) continue;
                    double inc = f / MusicRate;
                    double phase = 0;
                    // 높은 음일수록 빨리 사그라진다
                    double decaySec = inst.Decays[p] * Math.Min(1.6, Math.Max(0.45, Math.Sqrt(523.25 / f0)));
                    double mul = Math.Exp(-1.0 / (decaySec * MusicRate));
                    double env = inst.Amps[p];
                    for (int j = 0; j < total; j++)
                    {
                        phase += inc;
                        if (phase >= 1) phase -= 1;
                        double g = env;
                        if (j < 24) g *= j / 24.0;
                        else if (j >= hold) { double u = 1.0 - (double)(j - hold) / release; g *= u * u; }
                        buf[j] += (float)(Lookup(sine, phase) * g);
                        env *= mul;
                    }
                }
            }

            internal static double Lookup(float[] table, double phase)
            {
                double x = phase * TableSize;
                int i = (int)x;
                if (i >= TableSize) i = TableSize - 1;
                double fr = x - i;
                return table[i] + (table[i + 1] - table[i]) * fr;
            }

            /// <summary>
            /// 음색표 한 주기 — 배음 진폭 × 저역통과, 나이퀴스트(0.45 × 레이트) 위 배음은 뺀다(에일리어싱 방지).
            /// sin(kθ)는 체비쇼프 점화식으로 굴린다(배음마다 sin을 부르지 않는다).
            /// </summary>
            private static float[] Table(Inst inst, int midi, double f0, double cutoff, int variant, Dictionary<long, float[]> cacheByNote)
            {
                long key = ((long)inst.Id << 16) | ((long)(midi & 0xFF) << 4) | (uint)variant;
                if (cacheByNote.TryGetValue(key, out float[] cached)) return cached;

                int k = Math.Max(1, Math.Min(inst.MaxPartials, (int)(MusicRate * 0.45 / f0)));
                double[] amp = new double[k + 1];
                for (int p = 1; p <= k; p++)
                {
                    double fp = p * f0;
                    double lp = 1.0 / Math.Sqrt(1.0 + Math.Pow(fp / cutoff, 4));
                    amp[p] = SpectrumAmp(inst.Wave, p, fp) * lp;
                }
                float[] table = new float[TableSize + 1];
                double peak = 0;
                for (int i = 0; i < TableSize; i++)
                {
                    double theta = 2.0 * Math.PI * i / TableSize;
                    double c2 = 2.0 * Math.Cos(theta);
                    double prev = 0, cur = Math.Sin(theta), sum = 0;
                    for (int p = 1; p <= k; p++)
                    {
                        sum += amp[p] * cur;
                        double next = c2 * cur - prev;
                        prev = cur;
                        cur = next;
                    }
                    table[i] = (float)sum;
                    peak = Math.Max(peak, Math.Abs(sum));
                }
                if (peak > 1e-9)
                    for (int i = 0; i < TableSize; i++) table[i] = (float)(table[i] / peak);
                table[TableSize] = table[0];
                cacheByNote[key] = table;
                return table;
            }

            private static double SpectrumAmp(Wave wave, int k, double fk)
            {
                switch (wave)
                {
                    case Wave.Square: return (k & 1) == 1 ? 1.0 / k : 0;
                    case Wave.Pulse25: return Math.Sin(Math.PI * k * 0.25) / k;
                    case Wave.Triangle: return (k & 1) == 1 ? ((((k - 1) / 2) & 1) == 0 ? 1.0 : -1.0) / ((double)k * k) : 0;
                    case Wave.Brass:
                    {
                        // 금관 — 1~1.3kHz 부근이 솟는 포먼트(폰 스피커의 단 대역에 그대로 얹힌다)
                        double l = Math.Log(fk / 1150.0);
                        return Math.Pow(k, -0.85) * (0.55 + 1.6 * Math.Exp(-l * l / 0.5));
                    }
                    case Wave.Choir:
                    {
                        // "아" 모음 포먼트 650 / 1080 / 2650Hz
                        double f1 = (fk - 650) / 110, f2 = (fk - 1080) / 160, f3 = (fk - 2650) / 250;
                        double formant = 1.0 / (1 + f1 * f1) + 0.5 / (1 + f2 * f2) + 0.25 / (1 + f3 * f3) + 0.03;
                        return Math.Pow(k, -0.5) * formant;
                    }
                    case Wave.Organ:
                        switch (k)
                        {
                            case 1: return 1;
                            case 2: return 0.55;
                            case 3: return 0.32;
                            case 4: return 0.18;
                            case 6: return 0.1;
                            case 8: return 0.06;
                            default: return 0;
                        }
                    default: return 1.0 / k; // Saw
                }
            }

            // ── 잔향·마스터 ──

            /// <summary>
            /// Schroeder 잔향(콤 4 + 올패스 2)을 <paramref name="output"/>에 더한다. 곡이 고리이므로 끝 3초를 먼저 흘려 상태를 데운 뒤 처음부터 쓴다 —
            /// 루프 첫머리에도 앞(=곡 끝)의 잔향이 그대로 남는다.
            /// </summary>
            private static void AddReverb(float[] input, float[] output)
            {
                int len = input.Length;
                float[] c0 = new float[557], c1 = new float[593], c2 = new float[641], c3 = new float[677];
                float[] a0 = new float[225], a1 = new float[341];
                int i0 = 0, i1 = 0, i2 = 0, i3 = 0, j0 = 0, j1 = 0;
                float s0 = 0, s1 = 0, s2 = 0, s3 = 0;
                const float feedback = 0.8f, damp = 0.3f, keep = 1f - damp, apFeedback = 0.5f;

                int warm = Math.Min(len, MusicRate * 3);
                for (int pass = 0; pass < 2; pass++)
                {
                    int from = pass == 0 ? len - warm : 0;
                    for (int i = from; i < len; i++)
                    {
                        float x = input[i];
                        float o0 = c0[i0], o1 = c1[i1], o2 = c2[i2], o3 = c3[i3];
                        s0 = o0 * keep + s0 * damp; c0[i0] = x + s0 * feedback; if (++i0 == c0.Length) i0 = 0;
                        s1 = o1 * keep + s1 * damp; c1[i1] = x + s1 * feedback; if (++i1 == c1.Length) i1 = 0;
                        s2 = o2 * keep + s2 * damp; c2[i2] = x + s2 * feedback; if (++i2 == c2.Length) i2 = 0;
                        s3 = o3 * keep + s3 * damp; c3[i3] = x + s3 * feedback; if (++i3 == c3.Length) i3 = 0;
                        float y = o0 + o1 + o2 + o3;
                        float b0 = a0[j0]; a0[j0] = y + b0 * apFeedback; if (++j0 == a0.Length) j0 = 0; y = b0 - y;
                        float b1 = a1[j1]; a1[j1] = y + b1 * apFeedback; if (++j1 == a1.Length) j1 = 0; y = b1 - y;
                        if (pass == 1) output[i] += y * 0.22f;
                    }
                }
            }

            /// <summary>
            /// 마스터 — ①30Hz 아래를 걷고(헤드룸만 먹는다) ②300Hz~6kHz 대역 RMS를 목표에 맞추고
            /// ③무릎 위 봉우리만 tanh로 눌러 1을 넘지 않게 한다.
            /// </summary>
            private static void Master(float[] d)
            {
                HighPassLoop(d, 30.0);
                double band = BandRms(d, MusicRate);
                if (band > 1e-6)
                {
                    float gain = (float)(MusicTargetBandRms / band);
                    for (int i = 0; i < d.Length; i++) d[i] *= gain;
                }
                const float knee = MusicLimiterKnee;
                for (int i = 0; i < d.Length; i++)
                {
                    float x = d[i];
                    float ax = x < 0 ? -x : x;
                    if (ax <= knee) continue;
                    float y = knee + (1f - knee) * (float)Math.Tanh((ax - knee) / (1f - knee));
                    d[i] = x < 0 ? -y : y;
                }
            }

            private static void HighPassLoop(float[] d, double cutoff)
            {
                double a = Math.Exp(-2.0 * Math.PI * cutoff / MusicRate);
                int len = d.Length;
                int warm = Math.Min(len, MusicRate);
                double y = 0, xPrev = d[len - warm];
                for (int i = len - warm + 1; i < len; i++) { y = a * (y + d[i] - xPrev); xPrev = d[i]; }
                for (int i = 0; i < len; i++)
                {
                    double x = d[i];
                    y = a * (y + x - xPrev);
                    xPrev = x;
                    d[i] = (float)y;
                }
            }

            /// <summary>
            /// 폰 스피커 대역 RMS — 300Hz 1차 고역통과 두 번으로 어림한다. 6kHz 위 에너지는 이 곡들에서 1% 남짓이라
            /// 저역통과는 생략한다(필터 사슬이 짧을수록 빠르다).
            /// </summary>
            internal static double BandRms(float[] d, int rate)
            {
                float hp = (float)Math.Exp(-2.0 * Math.PI * 300.0 / rate);
                float h1 = 0, h1x = 0, h2 = 0, h2x = 0;
                double sum = 0;
                for (int i = 0; i < d.Length; i++)
                {
                    float x = d[i];
                    h1 = hp * (h1 + x - h1x); h1x = x;
                    h2 = hp * (h2 + h1 - h2x); h2x = h1;
                    sum += h2 * h2;
                }
                return Math.Sqrt(sum / Math.Max(1, d.Length));
            }
        }

        // ── 악보 도우미 ──

        private static readonly float[] SineTable = BuildSineTable();

        private static float[] BuildSineTable()
        {
            const int size = Score.TableSize;
            var t = new float[size + 1];
            for (int i = 0; i <= size; i++) t[i] = (float)Math.Sin(2.0 * Math.PI * i / size);
            return t;
        }

        private static float StepVel(char c)
        {
            switch (c)
            {
                case 'X': return 1f;
                case 'x': return 0.75f;
                case 'o': return 0.45f;
                default: return 0f;
            }
        }

        private static Chord[] Slice(Chord[] chords, int from, int count)
        {
            var r = new Chord[count];
            Array.Copy(chords, from, r, 0, count);
            return r;
        }

        /// <summary>"Am F G7 Gm/D" — 마디마다 코드 하나.</summary>
        internal static Chord[] Prog(string text)
        {
            string[] parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var chords = new Chord[parts.Length];
            for (int i = 0; i < parts.Length; i++) chords[i] = ParseChord(parts[i]);
            return chords;
        }

        private static Chord ParseChord(string sym)
        {
            string body = sym;
            int bassPc = -1;
            int slash = sym.IndexOf('/');
            if (slash > 0)
            {
                body = sym.Substring(0, slash);
                bassPc = ParsePitchClass(sym.Substring(slash + 1), out _);
            }
            int root = ParsePitchClass(body, out int used);
            string q = body.Substring(used);
            int[] iv;
            switch (q)
            {
                case "": iv = new[] { 0, 4, 7 }; break;
                case "m": iv = new[] { 0, 3, 7 }; break;
                case "7": iv = new[] { 0, 4, 7, 10 }; break;
                case "m7": iv = new[] { 0, 3, 7, 10 }; break;
                case "dim": iv = new[] { 0, 3, 6 }; break;
                case "sus4": iv = new[] { 0, 5, 7 }; break;
                default: throw new InvalidOperationException("모르는 코드: " + sym);
            }
            return new Chord { RootPc = root, BassPc = bassPc >= 0 ? bassPc : root, Intervals = iv };
        }

        private static int ParsePitchClass(string s, out int used)
        {
            int pc;
            switch (s[0])
            {
                case 'C': pc = 0; break;
                case 'D': pc = 2; break;
                case 'E': pc = 4; break;
                case 'F': pc = 5; break;
                case 'G': pc = 7; break;
                case 'A': pc = 9; break;
                case 'B': pc = 11; break;
                default: throw new InvalidOperationException("음이름이 아니다: " + s);
            }
            used = 1;
            if (s.Length > 1 && s[1] == '#') { pc++; used = 2; }
            else if (s.Length > 1 && s[1] == 'b') { pc--; used = 2; }
            return (pc + 12) % 12;
        }

        /// <summary>"C#5:.75" → 미디 번호와 길이. 길이를 생략하면 앞 음 길이. 쉼표면 midi = -1.</summary>
        private static void ParseToken(string token, ref double dur, out int midi)
        {
            int colon = token.IndexOf(':');
            string name = colon >= 0 ? token.Substring(0, colon) : token;
            if (colon >= 0)
                dur = double.Parse(token.Substring(colon + 1), System.Globalization.CultureInfo.InvariantCulture);
            if (name == "r") { midi = -1; return; }
            int pc = ParsePitchClass(name, out int used);
            int octave = int.Parse(name.Substring(used), System.Globalization.CultureInfo.InvariantCulture);
            // B#·Cb 같은 옥타브 경계는 쓰지 않는다 — 음이름 그대로 옥타브를 붙인다
            int natural = name[0] == 'C' && pc == 11 ? -1 : (name[0] == 'B' && pc == 0 ? 1 : 0);
            midi = (octave + 1 + natural) * 12 + pc;
        }

        private static int[] Voicing(Chord chord, int lowMidi)
        {
            var tones = new int[chord.Intervals.Length];
            for (int i = 0; i < tones.Length; i++)
                tones[i] = PlaceAbove((chord.RootPc + chord.Intervals[i]) % 12, lowMidi);
            Array.Sort(tones);
            return tones;
        }

        /// <summary>음높이 종류 pc를 lowMidi 이상 한 옥타브 안에 놓는다.</summary>
        private static int PlaceAbove(int pc, int lowMidi) => lowMidi + ((pc - lowMidi % 12) % 12 + 12) % 12;

        private static int[] Scale(string text)
        {
            string[] parts = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var pcs = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++) pcs[i] = ParsePitchClass(parts[i], out _);
            return pcs;
        }

        /// <summary>음계 안에서 steps도 옮긴다(-2 = 3도 아래). 음계 밖 음은 바로 아래 음계음 기준.</summary>
        private static int DiatonicShift(int midi, int[] scale, int steps)
        {
            int m = midi;
            while (Array.IndexOf(scale, ((m % 12) + 12) % 12) < 0) m--;
            int dir = Math.Sign(steps);
            for (int i = 0; i < Math.Abs(steps); i++)
            {
                do { m += dir; } while (Array.IndexOf(scale, ((m % 12) + 12) % 12) < 0);
            }
            return m;
        }

        // ════════════════════════════════════════════
        //  북 합성 — 한 번 만들어 두고 여러 번 섞는다
        // ════════════════════════════════════════════

        private static class DrumSynth
        {
            /// <summary>
            /// 북 음량·잔향. 폰 대역(300Hz 위)에서 들리는 건 킥 몸통이 아니라 스네어·하이햇·박수·가죽 소리라
            /// 그쪽을 넉넉히 둔다 — 처음 값으로는 곡마다 북이 대역 기준 13~15dB 아래 묻혔다(마디별 실측).
            /// </summary>
            public static void Levels(Kit kit, out float gain, out float send)
            {
                switch (kit)
                {
                    case Kit.Kick: gain = 0.6f; send = 0.02f; break;
                    case Kit.Snare: gain = 0.62f; send = 0.18f; break;
                    case Kit.Clap: gain = 0.55f; send = 0.22f; break;
                    case Kit.HatClosed: gain = 0.16f; send = 0.05f; break;
                    case Kit.HatOpen: gain = 0.14f; send = 0.1f; break;
                    case Kit.Crash: gain = 0.24f; send = 0.25f; break;
                    case Kit.TomHi: case Kit.TomLo: gain = 0.55f; send = 0.18f; break;
                    case Kit.Taiko: gain = 0.8f; send = 0.25f; break;
                    case Kit.TaikoHi: gain = 0.45f; send = 0.2f; break;
                    case Kit.WoodHi: case Kit.WoodLo: gain = 0.24f; send = 0.12f; break;
                    case Kit.Gong: gain = 0.45f; send = 0.35f; break;
                    case Kit.Timpani: gain = 0.55f; send = 0.25f; break;
                    default: gain = 0.3f; send = 0.1f; break;
                }
            }

            public static float[] Render(Kit kit, int midi, int seed)
            {
                var rng = new Noise((uint)seed * 2654435761u + 12345u);
                switch (kit)
                {
                    case Kit.Kick: return Kick(ref rng);
                    case Kit.Snare: return Snare(ref rng);
                    case Kit.Clap: return Clap(ref rng);
                    case Kit.HatClosed: return Hat(ref rng, 0.07, 0.016);
                    case Kit.HatOpen: return Hat(ref rng, 0.38, 0.12);
                    case Kit.Crash: return Crash(ref rng);
                    case Kit.TomHi: return Tom(ref rng, 196.0);
                    case Kit.TomLo: return Tom(ref rng, 123.5);
                    case Kit.Taiko: return Taiko(ref rng, 68.0, 0.42, 1.4);
                    case Kit.TaikoHi: return Taiko(ref rng, 190.0, 0.12, 1.6);
                    case Kit.WoodHi: return Wood(ref rng, 1318.5);
                    case Kit.WoodLo: return Wood(ref rng, 880.0);
                    case Kit.Gong: return Gong(ref rng);
                    case Kit.Timpani: return Timpani(ref rng, 440.0 * Math.Pow(2.0, ((midi > 0 ? midi : 36) - 69) / 12.0));
                    default: return new float[1];
                }
            }

            /// <summary>xorshift32 — System.Random보다 몇 배 빠르고 결정적이다(같은 시드 = 같은 북).</summary>
            private struct Noise
            {
                private uint state;
                public Noise(uint seed) { state = seed == 0 ? 0x9E3779B9u : seed; }
                public float Next()
                {
                    uint x = state;
                    x ^= x << 13;
                    x ^= x >> 17;
                    x ^= x << 5;
                    state = x;
                    return (int)x * (1f / 2147483648f);
                }
            }

            /// <summary>1차 저역통과 계수.</summary>
            private static double Coef(double hz) => 1.0 - Math.Exp(-2.0 * Math.PI * Math.Min(hz, MusicRate * 0.45) / MusicRate);
            /// <summary>시간 상수(초)만큼 지나면 1/e가 되는 샘플당 곱 — 샘플마다 Math.Exp를 부르지 않는다.</summary>
            private static double Mul(double seconds) => Math.Exp(-1.0 / (seconds * MusicRate));
            private static double Sin(float[] sine, double phase) => Score.Lookup(sine, phase);
            private static double Step(double phase, double hz)
            {
                phase += hz / MusicRate;
                return phase >= 1 ? phase - Math.Floor(phase) : phase;
            }

            /// <summary>킥 — 196→46Hz 내려가는 몸통 + 포화 배음(폰에서 "퉁"이 들리게) + 2~5kHz 딸깍.</summary>
            private static float[] Kick(ref Noise rng)
            {
                int n = (int)(0.38 * MusicRate);
                var d = new float[n];
                float[] sine = SineTable;
                double phase = 0, phase2 = 0, bend = 1, body = 1, knock = 0.3, click = 1.6, hiA = 0, hiB = 0;
                double mBend = Mul(0.03), mBody = Mul(0.17), mKnock = Mul(0.045), mClick = Mul(0.004);
                double ca = Coef(5000), cb = Coef(1800);
                for (int j = 0; j < n; j++)
                {
                    double f = 46 + 150 * bend;
                    phase = Step(phase, f);
                    phase2 = Step(phase2, 2 * f);
                    double w = rng.Next();
                    hiA += (w - hiA) * ca;
                    hiB += (hiA - hiB) * cb;
                    float tone = (float)(1.8 * (Sin(sine, phase) * body + Sin(sine, phase2) * knock));
                    d[j] = Score.SoftClip(tone) + (float)((hiA - hiB) * click);
                    bend *= mBend; body *= mBody; knock *= mKnock; click *= mClick;
                }
                return Normalize(d);
            }

            /// <summary>스네어 — 190/330Hz 몸통 + 1.5~8kHz 노이즈.</summary>
            private static float[] Snare(ref Noise rng)
            {
                int n = (int)(0.28 * MusicRate);
                var d = new float[n];
                float[] sine = SineTable;
                double pA = 0, pB = 0, bend = 0.3, tone = 0.55, noise = 2.2, hA = 0, hB = 0;
                double mBend = Mul(0.01), mTone = Mul(0.045), mNoise = Mul(0.085);
                double ca = Coef(8000), cb = Coef(1500);
                for (int j = 0; j < n; j++)
                {
                    double k = 1 + bend;
                    pA = Step(pA, 190 * k);
                    pB = Step(pB, 330 * k);
                    double w = rng.Next();
                    hA += (w - hA) * ca;
                    hB += (hA - hB) * cb;
                    d[j] = (float)((Sin(sine, pA) * 0.6 + Sin(sine, pB) * 0.35) * tone + (hA - hB) * noise);
                    bend *= mBend; tone *= mTone; noise *= mNoise;
                }
                return Normalize(d);
            }

            /// <summary>박수 — 11ms 간격 세 번 + 꼬리, 0.9~3.5kHz.</summary>
            private static float[] Clap(ref Noise rng)
            {
                int n = (int)(0.25 * MusicRate);
                var d = new float[n];
                double hA = 0, hB = 0, ca = Coef(3500), cb = Coef(900);
                for (int j = 0; j < n; j++)
                {
                    double t = (double)j / MusicRate;
                    double env = 0;
                    for (int k = 0; k < 3; k++)
                    {
                        double dt = t - k * 0.011;
                        if (dt >= 0 && dt < 0.04) env = Math.Max(env, Math.Exp(-dt / 0.006));
                    }
                    if (t > 0.03) env = Math.Max(env, 0.7 * Math.Exp(-(t - 0.03) / 0.07));
                    double w = rng.Next();
                    hA += (w - hA) * ca;
                    hB += (hA - hB) * cb;
                    d[j] = (float)((hA - hB) * env);
                }
                return Normalize(d);
            }

            private static float[] Hat(ref Noise rng, double seconds, double decay)
            {
                int n = (int)(seconds * MusicRate);
                var d = new float[n];
                double l1 = 0, l2 = 0, c = Coef(5500), env = 1, m = Mul(decay);
                for (int j = 0; j < n; j++)
                {
                    double w = rng.Next();
                    l1 += (w - l1) * c;
                    l2 += (l1 - l2) * c;
                    d[j] = (float)((w - l2) * env);
                    env *= m;
                }
                return Normalize(d);
            }

            private static float[] Crash(ref Noise rng)
            {
                int n = (int)(1.8 * MusicRate);
                var d = new float[n];
                float[] sine = SineTable;
                double l1 = 0, l2 = 0, c = Coef(2600), env = 1, m = Mul(0.55);
                double[] f = { 3150, 4720, 6080, 7390 };
                double[] ph = { 0, 0.16, 0.32, 0.48 };
                for (int j = 0; j < n; j++)
                {
                    double w = rng.Next();
                    l1 += (w - l1) * c;
                    l2 += (l1 - l2) * c;
                    double ring = 0;
                    for (int p = 0; p < f.Length; p++)
                    {
                        ph[p] = Step(ph[p], f[p]);
                        ring += Sin(sine, ph[p]) * 0.15;
                    }
                    double attack = j < 40 ? j / 40.0 : 1.0;
                    d[j] = (float)(((w - l2) + ring) * env * attack);
                    env *= m;
                }
                return Normalize(d);
            }

            /// <summary>탐탐 — 내려앉는 음높이 + 1.5배 모드 + 두드림.</summary>
            private static float[] Tom(ref Noise rng, double f0)
            {
                int n = (int)(0.45 * MusicRate);
                var d = new float[n];
                float[] sine = SineTable;
                double p1 = 0, p2 = 0, l1 = 0, c = Coef(3000);
                double bend = 0.6, e1 = 1, e2 = 0.3, slap = 1.2;
                double mBend = Mul(0.02), m1 = Mul(0.22), m2 = Mul(0.12), mSlap = Mul(0.01);
                for (int j = 0; j < n; j++)
                {
                    double f = f0 * (1 + bend);
                    p1 = Step(p1, f);
                    p2 = Step(p2, 1.5 * f);
                    double body = Sin(sine, p1) * e1 + Sin(sine, p2) * e2;
                    double w = rng.Next();
                    l1 += (w - l1) * c;
                    d[j] = Score.SoftClip((float)(1.4 * body)) + (float)(l1 * slap);
                    bend *= mBend; e1 *= m1; e2 *= m2; slap *= mSlap;
                }
                return Normalize(d);
            }

            /// <summary>
            /// 타이코 — 몸통(낮은 사인 + 1.6·2.3배 모드)을 세게 포화해 배음을 만들고, 200Hz~1.5kHz "딱" 하는 가죽 소리를 얹는다.
            /// 폰에서 북이 들리는 건 몸통이 아니라 이 둘이다.
            /// </summary>
            private static float[] Taiko(ref Noise rng, double f0, double decay, double slapAmt)
            {
                int n = (int)((decay * 2.4 + 0.1) * MusicRate);
                var d = new float[n];
                float[] sine = SineTable;
                double p1 = 0, p2 = 0, p3 = 0, hA = 0, hB = 0, ca = Coef(1500), cb = Coef(220);
                double bend = 0.9, e1 = 1, e2 = 0.35, e3 = 0.2, slap = 2.5 * slapAmt;
                double mBend = Mul(0.025), m1 = Mul(decay), m2 = Mul(decay * 0.45), m3 = Mul(decay * 0.3), mSlap = Mul(0.022);
                for (int j = 0; j < n; j++)
                {
                    double f = f0 * (1 + bend);
                    p1 = Step(p1, f);
                    p2 = Step(p2, 1.6 * f);
                    p3 = Step(p3, 2.3 * f);
                    double body = Sin(sine, p1) * e1 + Sin(sine, p2) * e2 + Sin(sine, p3) * e3;
                    double w = rng.Next();
                    hA += (w - hA) * ca;
                    hB += (hA - hB) * cb;
                    d[j] = Score.SoftClip((float)(2.2 * body)) + (float)((hA - hB) * slap);
                    bend *= mBend; e1 *= m1; e2 *= m2; e3 *= m3; slap *= mSlap;
                }
                return Normalize(d);
            }

            private static float[] Wood(ref Noise rng, double f0)
            {
                int n = (int)(0.12 * MusicRate);
                var d = new float[n];
                float[] sine = SineTable;
                double pA = 0, pB = 0, eA = 1, eB = 0.35, mA = Mul(0.03), mB = Mul(0.012);
                for (int j = 0; j < n; j++)
                {
                    pA = Step(pA, f0);
                    pB = Step(pB, f0 * 2.7);
                    double s = Sin(sine, pA) * eA + Sin(sine, pB) * eB;
                    if (j < 40) s += rng.Next() * 0.3 * (1 - j / 40.0);
                    d[j] = (float)s;
                    eA *= mA; eB *= mB;
                }
                return Normalize(d);
            }

            /// <summary>징 — 비정수배 부분음이 천천히 피어올랐다 오래 남는다(높은 부분음 300~600Hz가 폰에서 들린다).</summary>
            private static float[] Gong(ref Noise rng)
            {
                int n = (int)(3.2 * MusicRate);
                var d = new float[n];
                float[] sine = SineTable;
                double[] f = { 72, 113, 158, 203, 271, 349, 455, 612, 790 };
                double[] env = { 1.0, 0.7, 0.6, 0.55, 0.5, 0.45, 0.35, 0.25, 0.15 };
                double[] dec = { 2.6, 2.2, 2.0, 1.8, 1.6, 1.4, 1.1, 0.8, 0.6 };
                int count = f.Length;
                var ph = new double[count];
                var m = new double[count];
                var bloom = new double[count];
                var mBloom = new double[count];
                for (int p = 0; p < count; p++)
                {
                    ph[p] = p * 0.7 / (2 * Math.PI);
                    m[p] = Mul(dec[p]);
                    bloom[p] = p < 3 ? 0 : 1;
                    mBloom[p] = Mul(0.05 + 0.02 * p);
                }
                double l1 = 0, c = Coef(900), hit = 1.5, mHit = Mul(0.04);
                for (int j = 0; j < n; j++)
                {
                    double s = 0;
                    for (int p = 0; p < count; p++)
                    {
                        ph[p] = Step(ph[p], f[p]);
                        s += Sin(sine, ph[p]) * env[p] * (1 - bloom[p]);
                        env[p] *= m[p];
                        bloom[p] *= mBloom[p];
                    }
                    double w = rng.Next();
                    l1 += (w - l1) * c;
                    s += l1 * hit;
                    hit *= mHit;
                    d[j] = (float)s;
                }
                return Normalize(d);
            }

            /// <summary>팀파니 — 원형 막의 모드비(1, 1.5, 1.74, 2, 2.24, 2.49) + 말렛 두드림.</summary>
            private static float[] Timpani(ref Noise rng, double f0)
            {
                int n = (int)(1.5 * MusicRate);
                var d = new float[n];
                float[] sine = SineTable;
                double[] r = { 1.0, 1.504, 1.742, 2.0, 2.245, 2.494 };
                double[] env = { 1.0, 0.6, 0.35, 0.3, 0.2, 0.15 };
                double[] dec = { 0.9, 0.6, 0.5, 0.45, 0.35, 0.3 };
                var ph = new double[r.Length];
                var m = new double[r.Length];
                for (int p = 0; p < r.Length; p++) m[p] = Mul(dec[p]);
                double l1 = 0, c = Coef(1200), bend = 0.015, mBend = Mul(0.05), thump = 0.9, mThump = Mul(0.012);
                for (int j = 0; j < n; j++)
                {
                    double f = f0 * (1 + bend);
                    double s = 0;
                    for (int p = 0; p < r.Length; p++)
                    {
                        ph[p] = Step(ph[p], f * r[p]);
                        s += Sin(sine, ph[p]) * env[p];
                        env[p] *= m[p];
                    }
                    double w = rng.Next();
                    l1 += (w - l1) * c;
                    s += l1 * thump;
                    d[j] = Score.SoftClip((float)(1.3 * s));
                    bend *= mBend; thump *= mThump;
                }
                return Normalize(d);
            }

            /// <summary>솟구침 — 대역통과 중심이 400Hz→6kHz로 올라가며 커지는 노이즈. 끝에서 짧게 닫는다.</summary>
            public static float[] Riser(int length, int seed)
            {
                var rng = new Noise((uint)seed * 2246822519u + 7u);
                var d = new float[Math.Max(1, length)];
                double lA = 0, lB = 0, ca = 0, cb = 0;
                for (int j = 0; j < d.Length; j++)
                {
                    double u = (double)j / d.Length;
                    if ((j & 31) == 0)
                    {
                        double center = 400 * Math.Pow(15, u);
                        ca = Coef(center * 1.6);
                        cb = Coef(center * 0.6);
                    }
                    double w = rng.Next();
                    lA += (w - lA) * ca;
                    lB += (lA - lB) * cb;
                    double env = u * u * Math.Min(1.0, (1 - u) * 40);
                    d[j] = (float)((lA - lB) * 3.0 * env);
                }
                return d;
            }

            private static float[] Normalize(float[] d)
            {
                float peak = 0f;
                for (int i = 0; i < d.Length; i++) peak = Math.Max(peak, Math.Abs(d[i]));
                if (peak > 1e-6f) for (int i = 0; i < d.Length; i++) d[i] /= peak;
                // 끝을 4ms에 걸쳐 닫는다 — 잘린 꼬리가 딸깍거리지 않게
                int fade = Math.Min(d.Length, 88);
                for (int i = 0; i < fade; i++) d[d.Length - 1 - i] *= (float)i / fade;
                return d;
            }
        }
    }
}
