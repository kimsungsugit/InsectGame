using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// PCM 데이터를 직접 생성하여 AudioClip을 만드는 절차적 오디오 생성기.
    /// MonoBehaviour 없이 static으로 동작하며 캐시를 통해 중복 생성을 방지합니다.
    /// </summary>
    public static class ProceduralAudioGenerator
    {
        private const int SampleRate = 44100;
        private const float MasterGain = 0.4f;

        private static readonly Dictionary<string, AudioClip> cache =
            new Dictionary<string, AudioClip>();

        // ────────────────────────────────────────────
        //  공개 API
        // ────────────────────────────────────────────

        /// <summary>BGM AudioClip을 반환합니다. 캐시에 없으면 생성합니다.</summary>
        public static AudioClip GetBGM(string type)
        {
            string key = "bgm_" + type;
            if (cache.TryGetValue(key, out AudioClip clip))
            {
                return clip;
            }

            switch (type)
            {
                case "explore": clip = GenerateExploreBGM(); break;
                case "battle":  clip = GenerateBattleBGM();  break;
                case "raid":    clip = GenerateRaidBattleBGM(); break;
                case "victory": clip = GenerateVictoryBGM(); break;
                case "defeat":  clip = GenerateDefeatBGM();  break;
                case "menu":    clip = GenerateMenuBGM();    break;
                default:
                    Debug.LogWarning($"[ProceduralAudio] Unknown BGM type: {type}");
                    return null;
            }

            cache[key] = clip;
            return clip;
        }

        /// <summary>SFX AudioClip을 반환합니다. 캐시에 없으면 생성합니다.</summary>
        public static AudioClip GetSFX(string type)
        {
            string key = "sfx_" + type;
            if (cache.TryGetValue(key, out AudioClip clip))
            {
                return clip;
            }

            switch (type)
            {
                case "attack":          clip = GenerateAttackSFX(); break;
                case "skill_use":       clip = GenerateSkillUseSFX(); break;
                case "hit":             clip = GenerateHitSFX(); break;
                case "critical":        clip = GenerateCriticalHitSFX(); break;
                case "capture":         clip = GenerateCaptureSFX(); break;
                case "capture_success": clip = GenerateCaptureSuccessSFX(); break;
                case "capture_fail":    clip = GenerateCaptureFailSFX(); break;
                case "level_up":        clip = GenerateLevelUpSFX(); break;
                case "button_click":    clip = GenerateButtonClickSFX(); break;
                case "menu_open":       clip = GenerateMenuOpenSFX(); break;
                case "menu_close":      clip = GenerateMenuCloseSFX(); break;
                case "victory":         clip = GenerateVictorySFX(); break;
                case "defeat":          clip = GenerateDefeatSFX(); break;
                case "boss_appear":     clip = GenerateBossAppearSFX(); break;
                case "unite_attack":    clip = GenerateUniteAttackSFX(); break;
                case "buff":            clip = GenerateBuffApplySFX(); break;
                case "debuff":          clip = GenerateDebuffApplySFX(); break;
                case "item_pickup":     clip = GenerateItemPickupSFX(); break;
                case "item_use":        clip = GenerateItemUseSFX(); break;
                case "equip":           clip = GenerateEquipSFX(); break;
                case "set_complete":    clip = GenerateSetCompleteSFX(); break;
                default:
                    Debug.LogWarning($"[ProceduralAudio] Unknown SFX type: {type}");
                    return null;
            }

            cache[key] = clip;
            return clip;
        }

        /// <summary>환경음 AudioClip을 반환합니다. 캐시에 없으면 생성합니다.</summary>
        public static AudioClip GetAmbient(string type)
        {
            string key = "ambient_" + type;
            if (cache.TryGetValue(key, out AudioClip clip))
            {
                return clip;
            }

            switch (type)
            {
                case "forest": clip = GenerateForestAmbient(); break;
                case "pond":   clip = GeneratePondAmbient();   break;
                case "night":  clip = GenerateNightAmbient();  break;
                default:
                    Debug.LogWarning($"[ProceduralAudio] Unknown Ambient type: {type}");
                    return null;
            }

            cache[key] = clip;
            return clip;
        }

        // ────────────────────────────────────────────
        //  헬퍼 — 파형 생성
        // ────────────────────────────────────────────

        private static float SinWave(float frequency, int sample)
        {
            return Mathf.Sin(2f * Mathf.PI * frequency * sample / SampleRate);
        }

        private static float TriWave(float frequency, int sample)
        {
            float t = (frequency * sample / SampleRate) % 1f;
            return 4f * Mathf.Abs(t - 0.5f) - 1f;
        }

        private static float SquareWave(float frequency, int sample)
        {
            float t = (frequency * sample / SampleRate) % 1f;
            return t < 0.5f ? 1f : -1f;
        }

        private static float Noise(System.Random rng)
        {
            return (float)(rng.NextDouble() * 2.0 - 1.0);
        }

        // ────────────────────────────────────────────
        //  헬퍼 — 엔벨로프
        // ────────────────────────────────────────────

        /// <summary>간소화된 ADSR 엔벨로프.</summary>
        private static float Envelope(
            int sample,
            int attackSamples,
            int decaySamples,
            float sustainLevel,
            int releaseSamples,
            int totalSamples)
        {
            int sustainEnd = totalSamples - releaseSamples;

            if (sample < attackSamples)
            {
                // Attack
                return (float)sample / attackSamples;
            }
            if (sample < attackSamples + decaySamples)
            {
                // Decay
                float t = (float)(sample - attackSamples) / decaySamples;
                return Mathf.Lerp(1f, sustainLevel, t);
            }
            if (sample < sustainEnd)
            {
                // Sustain
                return sustainLevel;
            }
            // Release
            float rt = (float)(sample - sustainEnd) / releaseSamples;
            return Mathf.Lerp(sustainLevel, 0f, Mathf.Clamp01(rt));
        }

        /// <summary>단순 감쇠 (exponential decay).</summary>
        private static float Decay(int sample, int totalSamples, float speed)
        {
            float t = (float)sample / totalSamples;
            return Mathf.Exp(-speed * t);
        }

        // ────────────────────────────────────────────
        //  헬퍼 — AudioClip 생성
        // ────────────────────────────────────────────

        private static AudioClip CreateClip(string name, float[] data, bool loop)
        {
            // 마스터 게인 적용 및 클리핑 방지
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = Mathf.Clamp(data[i] * MasterGain, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>BPM과 비트 수로 총 샘플 수 계산.</summary>
        private static int BeatsToSamples(float bpm, float beats)
        {
            float secondsPerBeat = 60f / bpm;
            return Mathf.RoundToInt(secondsPerBeat * beats * SampleRate);
        }

        /// <summary>초를 샘플 수로 변환.</summary>
        private static int SecondsToSamples(float seconds)
        {
            return Mathf.RoundToInt(seconds * SampleRate);
        }

        // ────────────────────────────────────────────
        //  음높이 상수 (Hz)
        // ────────────────────────────────────────────

        private const float D1 = 36.71f;
        private const float D2 = 73.42f;
        private const float A2 = 110.00f;
        private const float C3 = 130.81f;
        private const float E3 = 164.81f;
        private const float G3 = 196.00f;
        private const float B3 = 246.94f;
        private const float C4 = 261.63f;
        private const float D4 = 293.66f;
        private const float E4 = 329.63f;
        private const float F4 = 349.23f;
        private const float G4 = 392.00f;
        private const float A4 = 440.00f;
        private const float B4 = 493.88f;
        private const float C5 = 523.25f;
        private const float D5 = 587.33f;
        private const float E5 = 659.25f;
        private const float F5 = 698.46f;
        private const float G5 = 783.99f;
        private const float A5 = 880.00f;
        private const float C6 = 1046.50f;

        // ────────────────────────────────────────────
        //  BGM 생성
        // ────────────────────────────────────────────

        /// <summary>탐험 BGM: C메이저 기반, 밝고 평화로운 16초 루프.</summary>
        private static AudioClip GenerateExploreBGM()
        {
            const float bpm = 110f;
            const float durationSec = 16f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(42);

            float secPerBeat = 60f / bpm;
            int samplesPerBeat = SecondsToSamples(secPerBeat);
            int samplesPerEighth = samplesPerBeat / 2;

            // 베이스 패턴: C3, E3, G3 (4비트씩 반복)
            float[] bassNotes = { C3, E3, G3, C3 };

            // 멜로디 음계
            float[] melodyNotes = { C5, D5, E5, G5, A5 };

            // 아르페지오 패턴
            float[] arpNotes = { C4, E4, G4, C5 };

            // 멜로디 순서를 고정 시드로 생성
            int totalEighths = (int)(durationSec / (secPerBeat * 0.5f));
            int[] melodySequence = new int[totalEighths];
            for (int i = 0; i < totalEighths; i++)
            {
                melodySequence[i] = rng.Next(melodyNotes.Length);
            }

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float beatPos = t / secPerBeat;

                // 베이스: 4비트 단위 변경, 사인파
                int bassIndex = ((int)(beatPos / 4f)) % bassNotes.Length;
                float bass = SinWave(bassNotes[bassIndex], i) * 0.3f;

                // 멜로디: 8분음표 단위, 사인파
                int eighthIndex = ((int)(beatPos * 2f)) % melodySequence.Length;
                float melFreq = melodyNotes[melodySequence[eighthIndex]];
                int posInEighth = i % samplesPerEighth;
                float melEnv = Envelope(posInEighth, samplesPerEighth / 10, samplesPerEighth / 5,
                    0.6f, samplesPerEighth / 4, samplesPerEighth);
                float melody = SinWave(melFreq, i) * 0.25f * melEnv;

                // 아르페지오: 삼각파, 16분음표 단위 순환
                int arpIndex = ((int)(beatPos * 4f)) % arpNotes.Length;
                int posInSixteenth = i % (samplesPerEighth / 2);
                int sixteenthLen = samplesPerEighth / 2;
                float arpEnv = Envelope(posInSixteenth, sixteenthLen / 8, sixteenthLen / 6,
                    0.4f, sixteenthLen / 3, sixteenthLen);
                float arp = TriWave(arpNotes[arpIndex], i) * 0.15f * arpEnv;

                // 패드: 노이즈 + 로우패스 효과 (간이 필터)
                float pad = Noise(rng) * 0.02f;

                data[i] = bass + melody + arp + pad;
            }

            return CreateClip("BGM_Explore", data, true);
        }

        /// <summary>전투 BGM: A마이너 기반, 긴장감 있고 빠른 12초 루프.</summary>
        private static AudioClip GenerateBattleBGM()
        {
            const float bpm = 150f;
            const float durationSec = 12f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(77);

            float secPerBeat = 60f / bpm;
            int samplesPerBeat = SecondsToSamples(secPerBeat);
            int samplesPerEighth = samplesPerBeat / 2;

            // 코드 진행: Am(A2) -> F(F4*0.5) -> G(G3) -> Am(A2) — 각 2비트
            float[] chordRoots = { A2, F4 * 0.5f, G3, A2 };

            // 멜로디 패턴
            float[] melodyNotes = { A4, C5, D5, E5, G5 };
            int totalEighths = (int)(durationSec / (secPerBeat * 0.5f));
            int[] melodySeq = new int[totalEighths];
            for (int i = 0; i < totalEighths; i++)
            {
                melodySeq[i] = rng.Next(melodyNotes.Length);
            }

            // 드럼 패턴 (8비트 단위 1마디)
            // K=킥, S=스네어, H=하이햇
            // 비트:  1  &  2  &  3  &  4  &
            bool[] kick =   { true,  false, false, false, true,  false, false, false };
            bool[] snare =  { false, false, true,  false, false, false, true,  false };
            bool[] hihat =  { true,  true,  true,  true,  true,  true,  true,  true  };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float beatPos = t / secPerBeat;

                // 베이스: 사각파 스타카토 (8분음표)
                int chordIdx = ((int)(beatPos / 2f)) % chordRoots.Length;
                int posInEighth = i % samplesPerEighth;
                float bassEnv = Decay(posInEighth, samplesPerEighth, 5f);
                float bass = SquareWave(chordRoots[chordIdx], i) * 0.2f * bassEnv;

                // 드럼
                int eighthInBar = ((int)(beatPos * 2f)) % 8;
                float drum = 0f;

                if (kick[eighthInBar])
                {
                    int posInDrum = posInEighth;
                    float kickFreq = 80f * Mathf.Exp(-6f * (float)posInDrum / samplesPerEighth);
                    drum += SinWave(kickFreq, posInDrum) * Decay(posInDrum, samplesPerEighth, 8f) * 0.35f;
                }
                if (snare[eighthInBar])
                {
                    int posInDrum = posInEighth;
                    drum += Noise(rng) * Decay(posInDrum, samplesPerEighth, 12f) * 0.2f;
                }
                if (hihat[eighthInBar])
                {
                    int posInDrum = posInEighth;
                    drum += Noise(rng) * Decay(posInDrum, samplesPerEighth / 4, 20f) * 0.08f;
                }

                // 멜로디: 삼각파
                int melIdx = ((int)(beatPos * 2f)) % melodySeq.Length;
                float melFreq = melodyNotes[melodySeq[melIdx]];
                float melEnv = Envelope(posInEighth, samplesPerEighth / 12, samplesPerEighth / 6,
                    0.5f, samplesPerEighth / 4, samplesPerEighth);
                float melody = TriWave(melFreq, i) * 0.2f * melEnv;

                data[i] = bass + drum + melody;
            }

            return CreateClip("BGM_Battle", data, true);
        }

        /// <summary>레이드 전투 BGM: D마이너 기반, 웅장하고 위압적 16초 루프.</summary>
        private static AudioClip GenerateRaidBattleBGM()
        {
            const float bpm = 120f;
            const float durationSec = 16f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(99);

            float secPerBeat = 60f / bpm;
            int samplesPerBeat = SecondsToSamples(secPerBeat);
            int samplesPerEighth = samplesPerBeat / 2;

            // 멜로디: D4, F4, A4, D5 레가토
            float[] melodyNotes = { D4, F4, A4, D5 };

            // 드럼 패턴 (4비트 단위)
            bool[] kick  = { true,  false, false, false };
            bool[] snare = { false, false, true,  false };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float beatPos = t / secPerBeat;

                // 베이스: D2 + A2 옥타브 사각파
                int posInBeat = i % samplesPerBeat;
                float bassEnv = Decay(posInBeat, samplesPerBeat, 3f);
                float bass = (SquareWave(D2, i) * 0.15f + SquareWave(A2, i) * 0.1f) * bassEnv;

                // 서브베이스: D1 사인파 존재감
                float subBass = SinWave(D1, i) * 0.12f;

                // 드럼: 느린 킥 + 무거운 스네어 + 크래시
                int beatInBar = ((int)beatPos) % kick.Length;
                float drum = 0f;
                int posInEighth = i % samplesPerEighth;

                if (kick[beatInBar] && posInBeat < samplesPerBeat / 2)
                {
                    float kickFreq = 60f * Mathf.Exp(-4f * (float)posInBeat / samplesPerBeat);
                    drum += SinWave(kickFreq, posInBeat) * Decay(posInBeat, samplesPerBeat, 5f) * 0.4f;
                }
                if (snare[beatInBar] && posInBeat < samplesPerBeat / 2)
                {
                    drum += Noise(rng) * Decay(posInBeat, samplesPerBeat / 2, 8f) * 0.25f;
                }

                // 크래시: 매 4마디 시작
                int barIndex = (int)(beatPos / 4f);
                float barStart = barIndex * 4f * secPerBeat;
                float timeSinceBar = t - barStart;
                if (timeSinceBar < 0.5f)
                {
                    drum += Noise(rng) * Decay(SecondsToSamples(timeSinceBar),
                        SecondsToSamples(0.5f), 6f) * 0.1f;
                }

                // 멜로디: 사인파+삼각파 혼합, 느린 레가토 (1비트당 1음)
                int melIdx = ((int)beatPos) % melodyNotes.Length;
                float melFreq = melodyNotes[melIdx];
                float melEnv = Envelope(posInBeat, samplesPerBeat / 6, samplesPerBeat / 4,
                    0.7f, samplesPerBeat / 3, samplesPerBeat);
                float melody = (SinWave(melFreq, i) * 0.5f + TriWave(melFreq, i) * 0.5f)
                    * 0.2f * melEnv;

                data[i] = bass + subBass + drum + melody;
            }

            return CreateClip("BGM_Raid", data, true);
        }

        /// <summary>승리 BGM: C메이저 팡파레 6초 (루프 아님).</summary>
        private static AudioClip GenerateVictoryBGM()
        {
            const float durationSec = 6f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(123);

            // 팡파레: C5-E5-G5-C6 상승 아르페지오
            float[] fanfare = { C5, E5, G5, C6 };
            float noteLen = 0.3f;
            int noteSamples = SecondsToSamples(noteLen);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;

                // 팡파레 (0~1.2초)
                for (int n = 0; n < fanfare.Length; n++)
                {
                    float noteStart = n * noteLen;
                    float noteEnd = noteStart + noteLen * 2f; // 오버랩
                    if (t >= noteStart && t < noteEnd)
                    {
                        int posInNote = SecondsToSamples(t - noteStart);
                        int noteTotal = SecondsToSamples(noteLen * 2f);
                        float env = Envelope(posInNote, noteSamples / 8, noteSamples / 4,
                            0.7f, noteSamples, noteTotal);
                        sample += (SinWave(fanfare[n], i) * 0.6f +
                                   TriWave(fanfare[n], i) * 0.4f) * 0.25f * env;
                    }
                }

                // 화음: C메이저 풀 코드 (1.2초 이후 서서히 등장)
                if (t > 1.0f)
                {
                    float chordFade = Mathf.Clamp01((t - 1.0f) / 0.5f);
                    float chordDecay = Decay(SecondsToSamples(t - 1.0f),
                        SecondsToSamples(4.5f), 1.5f);
                    float chord = (SinWave(C4, i) + SinWave(E4, i) + SinWave(G4, i) +
                                   SinWave(C5, i)) * 0.08f * chordFade * chordDecay;
                    sample += chord;
                }

                // 스파클: 고주파 짧은 사인파 반짝임
                if (t > 0.5f && t < 4f)
                {
                    float sparkleFreq = 2000f + 1000f * SinWave(3f, i);
                    float sparkleEnv = Mathf.Abs(SinWave(6f, i)) * 0.03f;
                    sample += SinWave(sparkleFreq, i) * sparkleEnv;
                }

                data[i] = sample;
            }

            return CreateClip("BGM_Victory", data, false);
        }

        /// <summary>패배 BGM: A마이너 하강 6초.</summary>
        private static AudioClip GenerateDefeatBGM()
        {
            const float durationSec = 6f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];

            // 하강: A4-G4-F4-E4
            float[] descent = { A4, G4, F4, E4 };
            float noteLen = 1.2f;
            int noteSamples = SecondsToSamples(noteLen);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;

                // 하강 멜로디
                for (int n = 0; n < descent.Length; n++)
                {
                    float noteStart = n * noteLen;
                    float noteEnd = noteStart + noteLen;
                    if (t >= noteStart && t < noteEnd)
                    {
                        int posInNote = SecondsToSamples(t - noteStart);
                        float env = Envelope(posInNote, noteSamples / 6, noteSamples / 3,
                            0.5f, noteSamples / 2, noteSamples);
                        sample += SinWave(descent[n], i) * 0.3f * env;
                    }
                }

                // 패드: Am 코드 지속
                float padDecay = Decay(i, totalSamples, 1f);
                float pad = (SinWave(A2, i) + SinWave(C3, i) + SinWave(E3, i)) * 0.06f * padDecay;
                sample += pad;

                data[i] = sample;
            }

            return CreateClip("BGM_Defeat", data, false);
        }

        /// <summary>메뉴 BGM: Em 기반, 차분하고 신비로운 12초 루프.</summary>
        private static AudioClip GenerateMenuBGM()
        {
            const float bpm = 70f;
            const float durationSec = 12f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];

            float secPerBeat = 60f / bpm;
            int samplesPerBeat = SecondsToSamples(secPerBeat);

            // 아르페지오: E3-G3-B3-E4
            float[] arpNotes = { E3, G3, B3, E4 };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float beatPos = t / secPerBeat;

                // 패드: Em 코드 사인파
                float pad = (SinWave(E4, i) + SinWave(G4, i) + SinWave(B4, i)) * 0.07f;

                // 아르페지오: 느린 순환
                int arpIdx = ((int)(beatPos)) % arpNotes.Length;
                int posInBeat = i % samplesPerBeat;
                float arpEnv = Envelope(posInBeat, samplesPerBeat / 8, samplesPerBeat / 4,
                    0.5f, samplesPerBeat / 3, samplesPerBeat);
                float arp = SinWave(arpNotes[arpIdx], i) * 0.15f * arpEnv;

                data[i] = pad + arp;
            }

            return CreateClip("BGM_Menu", data, true);
        }

        // ────────────────────────────────────────────
        //  SFX 생성
        // ────────────────────────────────────────────

        /// <summary>공격 SFX: 빠른 스와이프 "쉭" (0.15초).</summary>
        private static AudioClip GenerateAttackSFX()
        {
            int totalSamples = SecondsToSamples(0.15f);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(10);

            for (int i = 0; i < totalSamples; i++)
            {
                float env = Decay(i, totalSamples, 10f);
                // 고주파에서 시작해서 하강하는 노이즈
                float freq = 4000f * Mathf.Exp(-5f * (float)i / totalSamples);
                data[i] = (Noise(rng) * 0.5f + SinWave(freq, i) * 0.3f) * env * 0.6f;
            }

            return CreateClip("SFX_Attack", data, false);
        }

        /// <summary>스킬 발동 SFX: "빙" 상승음 (0.3초).</summary>
        private static AudioClip GenerateSkillUseSFX()
        {
            int totalSamples = SecondsToSamples(0.3f);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(11);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float freq = Mathf.Lerp(500f, 1500f, t);
                float env = Envelope(i, totalSamples / 10, totalSamples / 5,
                    0.6f, totalSamples / 3, totalSamples);
                // 사인파 상승 + 반짝임
                float sparkle = SinWave(freq * 3f, i) * 0.1f * Mathf.Max(0, t - 0.5f) * 2f;
                data[i] = (SinWave(freq, i) * 0.6f + sparkle) * env;
            }

            return CreateClip("SFX_SkillUse", data, false);
        }

        /// <summary>타격 SFX: "탁" (0.1초).</summary>
        private static AudioClip GenerateHitSFX()
        {
            int totalSamples = SecondsToSamples(0.1f);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(12);

            for (int i = 0; i < totalSamples; i++)
            {
                float env = Decay(i, totalSamples, 15f);
                float lowPulse = SinWave(100f, i) * 0.5f;
                float noise = Noise(rng) * 0.3f;
                data[i] = (lowPulse + noise) * env * 0.7f;
            }

            return CreateClip("SFX_Hit", data, false);
        }

        /// <summary>크리티컬 타격 SFX: "퍽!" (0.2초).</summary>
        private static AudioClip GenerateCriticalHitSFX()
        {
            int totalSamples = SecondsToSamples(0.2f);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(13);

            for (int i = 0; i < totalSamples; i++)
            {
                float env = Decay(i, totalSamples, 10f);
                float lowPulse = SinWave(60f, i) * 0.5f;
                float noise = Noise(rng) * 0.4f;
                float highAccent = SinWave(1200f, i) * 0.15f * Decay(i, totalSamples, 20f);
                data[i] = (lowPulse + noise + highAccent) * env * 0.8f;
            }

            return CreateClip("SFX_CriticalHit", data, false);
        }

        /// <summary>포획 시도 SFX: "삐리리" 떨림 (0.5초).</summary>
        private static AudioClip GenerateCaptureSFX()
        {
            int totalSamples = SecondsToSamples(0.5f);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float env = Envelope(i, totalSamples / 10, totalSamples / 5,
                    0.6f, totalSamples / 3, totalSamples);
                // 트레몰로 효과: AM 변조
                float tremolo = 0.5f + 0.5f * SinWave(15f, i);
                float tone = SinWave(800f + 200f * SinWave(8f, i), i);
                data[i] = tone * tremolo * env * 0.5f;
            }

            return CreateClip("SFX_Capture", data, false);
        }

        /// <summary>포획 성공 SFX: "딩동!" C5-E5-G5 (0.4초).</summary>
        private static AudioClip GenerateCaptureSuccessSFX()
        {
            int totalSamples = SecondsToSamples(0.4f);
            float[] data = new float[totalSamples];

            float[] notes = { C5, E5, G5 };
            float noteLen = 0.4f / 3f;
            int noteSamples = SecondsToSamples(noteLen);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;

                for (int n = 0; n < notes.Length; n++)
                {
                    float noteStart = n * noteLen;
                    if (t >= noteStart)
                    {
                        int posInNote = SecondsToSamples(t - noteStart);
                        int remaining = totalSamples - SecondsToSamples(noteStart);
                        float env = Decay(posInNote, remaining, 6f);
                        sample += SinWave(notes[n], i) * 0.3f * env;
                    }
                }

                data[i] = sample;
            }

            return CreateClip("SFX_CaptureSuccess", data, false);
        }

        /// <summary>포획 실패 SFX: "부-" 하강 (0.3초).</summary>
        private static AudioClip GenerateCaptureFailSFX()
        {
            int totalSamples = SecondsToSamples(0.3f);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(14);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float freq = Mathf.Lerp(400f, 150f, t);
                float env = Decay(i, totalSamples, 5f);
                data[i] = (SinWave(freq, i) * 0.4f + Noise(rng) * 0.05f) * env * 0.6f;
            }

            return CreateClip("SFX_CaptureFail", data, false);
        }

        /// <summary>레벨업 SFX: "따다닷!" C5-E5-G5-C6 (0.5초).</summary>
        private static AudioClip GenerateLevelUpSFX()
        {
            int totalSamples = SecondsToSamples(0.5f);
            float[] data = new float[totalSamples];

            float[] notes = { C5, E5, G5, C6 };
            float noteLen = 0.5f / 4f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;

                for (int n = 0; n < notes.Length; n++)
                {
                    float noteStart = n * noteLen;
                    if (t >= noteStart)
                    {
                        int posInNote = SecondsToSamples(t - noteStart);
                        int remaining = totalSamples - SecondsToSamples(noteStart);
                        float env = Decay(posInNote, remaining, 5f);
                        sample += (SinWave(notes[n], i) * 0.5f +
                                   TriWave(notes[n], i) * 0.3f) * 0.25f * env;
                    }
                }

                data[i] = sample;
            }

            return CreateClip("SFX_LevelUp", data, false);
        }

        /// <summary>UI 클릭 SFX: "틱" (0.05초).</summary>
        private static AudioClip GenerateButtonClickSFX()
        {
            int totalSamples = SecondsToSamples(0.05f);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float env = Decay(i, totalSamples, 30f);
                data[i] = SinWave(1800f, i) * env * 0.5f;
            }

            return CreateClip("SFX_ButtonClick", data, false);
        }

        /// <summary>메뉴 열기 SFX: "슝" 상승 스윕 (0.2초).</summary>
        private static AudioClip GenerateMenuOpenSFX()
        {
            int totalSamples = SecondsToSamples(0.2f);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float freq = Mathf.Lerp(300f, 1200f, t);
                float env = Envelope(i, totalSamples / 8, totalSamples / 4,
                    0.5f, totalSamples / 3, totalSamples);
                data[i] = SinWave(freq, i) * env * 0.4f;
            }

            return CreateClip("SFX_MenuOpen", data, false);
        }

        /// <summary>메뉴 닫기 SFX: "슉" 하강 스윕 (0.15초).</summary>
        private static AudioClip GenerateMenuCloseSFX()
        {
            int totalSamples = SecondsToSamples(0.15f);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float freq = Mathf.Lerp(1200f, 300f, t);
                float env = Decay(i, totalSamples, 8f);
                data[i] = SinWave(freq, i) * env * 0.4f;
            }

            return CreateClip("SFX_MenuClose", data, false);
        }

        /// <summary>승리 효과음: 짧은 팡파레 (1초).</summary>
        private static AudioClip GenerateVictorySFX()
        {
            int totalSamples = SecondsToSamples(1f);
            float[] data = new float[totalSamples];

            float[] fanfare = { C5, E5, G5, C6 };
            float noteLen = 0.15f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;

                for (int n = 0; n < fanfare.Length; n++)
                {
                    float noteStart = n * noteLen;
                    if (t >= noteStart)
                    {
                        int posInNote = SecondsToSamples(t - noteStart);
                        int remaining = totalSamples - SecondsToSamples(noteStart);
                        float env = Decay(posInNote, remaining, 4f);
                        sample += (SinWave(fanfare[n], i) * 0.5f +
                                   TriWave(fanfare[n], i) * 0.3f) * 0.2f * env;
                    }
                }

                // 마지막 코드 지속
                if (t > 0.6f)
                {
                    float chordDecay = Decay(SecondsToSamples(t - 0.6f),
                        SecondsToSamples(0.4f), 3f);
                    sample += (SinWave(C5, i) + SinWave(E5, i) + SinWave(G5, i))
                        * 0.05f * chordDecay;
                }

                data[i] = sample;
            }

            return CreateClip("SFX_Victory", data, false);
        }

        /// <summary>패배 효과음: 하강 3음 (0.6초).</summary>
        private static AudioClip GenerateDefeatSFX()
        {
            int totalSamples = SecondsToSamples(0.6f);
            float[] data = new float[totalSamples];

            float[] notes = { A4, F4, D4 };
            float noteLen = 0.2f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;

                for (int n = 0; n < notes.Length; n++)
                {
                    float noteStart = n * noteLen;
                    if (t >= noteStart)
                    {
                        int posInNote = SecondsToSamples(t - noteStart);
                        int remaining = totalSamples - SecondsToSamples(noteStart);
                        float env = Decay(posInNote, remaining, 5f);
                        sample += SinWave(notes[n], i) * 0.3f * env;
                    }
                }

                data[i] = sample;
            }

            return CreateClip("SFX_Defeat", data, false);
        }

        /// <summary>보스 등장 SFX: "두두둥" 저주파 3타 (0.8초).</summary>
        private static AudioClip GenerateBossAppearSFX()
        {
            int totalSamples = SecondsToSamples(0.8f);
            float[] data = new float[totalSamples];

            // 3타 타이밍: 0, 0.25, 0.5초 (마지막이 가장 길게)
            float[] hitTimes = { 0f, 0.25f, 0.5f };
            float[] hitLengths = { 0.15f, 0.15f, 0.3f };
            float[] hitFreqs = { 70f, 60f, 45f };
            float[] hitVolumes = { 0.5f, 0.6f, 0.8f };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;

                for (int h = 0; h < hitTimes.Length; h++)
                {
                    if (t >= hitTimes[h] && t < hitTimes[h] + hitLengths[h])
                    {
                        float localT = t - hitTimes[h];
                        int localSample = SecondsToSamples(localT);
                        int hitSamples = SecondsToSamples(hitLengths[h]);
                        float env = Decay(localSample, hitSamples, 8f);
                        sample += SinWave(hitFreqs[h], i) * hitVolumes[h] * env;
                    }
                }

                data[i] = sample;
            }

            return CreateClip("SFX_BossAppear", data, false);
        }

        /// <summary>합체공격 SFX: "차차차챠!" 상승 화음 + 임팩트 (1초).</summary>
        private static AudioClip GenerateUniteAttackSFX()
        {
            int totalSamples = SecondsToSamples(1f);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(20);

            // 상승 단계: 0~0.6초 (3단계 상승)
            float[] riseFreqs = { C4, E4, G4 };
            float riseNoteLen = 0.2f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;

                // 상승 화음
                for (int n = 0; n < riseFreqs.Length; n++)
                {
                    float noteStart = n * riseNoteLen;
                    float noteEnd = noteStart + riseNoteLen;
                    if (t >= noteStart && t < noteEnd + 0.1f)
                    {
                        int posInNote = SecondsToSamples(t - noteStart);
                        int noteSamples = SecondsToSamples(riseNoteLen + 0.1f);
                        float env = Envelope(posInNote, noteSamples / 10, noteSamples / 5,
                            0.6f, noteSamples / 3, noteSamples);
                        sample += (SinWave(riseFreqs[n], i) + TriWave(riseFreqs[n], i))
                            * 0.15f * env;
                    }
                }

                // 최종 임팩트 (0.6~1.0초): C5 코드 + 노이즈 버스트
                if (t >= 0.6f)
                {
                    float localT = t - 0.6f;
                    int localSample = SecondsToSamples(localT);
                    int impactTotal = SecondsToSamples(0.4f);
                    float env = Decay(localSample, impactTotal, 5f);
                    sample += (SinWave(C5, i) + SinWave(E5, i) + SinWave(G5, i))
                        * 0.12f * env;
                    // 노이즈 버스트
                    float noiseEnv = Decay(localSample, impactTotal, 15f);
                    sample += Noise(rng) * 0.15f * noiseEnv;
                }

                data[i] = sample;
            }

            return CreateClip("SFX_UniteAttack", data, false);
        }

        /// <summary>버프 적용 SFX: "삐링" 상승 3음 (0.3초).</summary>
        private static AudioClip GenerateBuffApplySFX()
        {
            int totalSamples = SecondsToSamples(0.3f);
            float[] data = new float[totalSamples];

            float[] notes = { E5, G5, C6 };
            float noteLen = 0.1f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;

                for (int n = 0; n < notes.Length; n++)
                {
                    float noteStart = n * noteLen;
                    if (t >= noteStart)
                    {
                        int posInNote = SecondsToSamples(t - noteStart);
                        int remaining = totalSamples - SecondsToSamples(noteStart);
                        float env = Decay(posInNote, remaining, 8f);
                        sample += SinWave(notes[n], i) * 0.3f * env;
                    }
                }

                data[i] = sample;
            }

            return CreateClip("SFX_BuffApply", data, false);
        }

        /// <summary>디버프 적용 SFX: "부웅" 하강 2음 (0.3초).</summary>
        private static AudioClip GenerateDebuffApplySFX()
        {
            int totalSamples = SecondsToSamples(0.3f);
            float[] data = new float[totalSamples];

            float[] notes = { D4, A2 };
            float noteLen = 0.15f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;

                for (int n = 0; n < notes.Length; n++)
                {
                    float noteStart = n * noteLen;
                    if (t >= noteStart)
                    {
                        int posInNote = SecondsToSamples(t - noteStart);
                        int remaining = totalSamples - SecondsToSamples(noteStart);
                        float env = Decay(posInNote, remaining, 7f);
                        sample += SinWave(notes[n], i) * 0.35f * env;
                    }
                }

                data[i] = sample;
            }

            return CreateClip("SFX_DebuffApply", data, false);
        }

        /// <summary>아이템 획득 SFX: "팅" (0.1초).</summary>
        private static AudioClip GenerateItemPickupSFX()
        {
            int totalSamples = SecondsToSamples(0.1f);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float env = Decay(i, totalSamples, 15f);
                data[i] = SinWave(2200f, i) * env * 0.5f;
            }

            return CreateClip("SFX_ItemPickup", data, false);
        }

        /// <summary>아이템 사용 SFX: "퐁" (0.2초).</summary>
        private static AudioClip GenerateItemUseSFX()
        {
            int totalSamples = SecondsToSamples(0.2f);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float env = Envelope(i, totalSamples / 8, totalSamples / 4,
                    0.5f, totalSamples / 3, totalSamples);
                float tone = SinWave(600f, i) * 0.4f;
                // 반짝임
                float sparkle = SinWave(1800f, i) * 0.1f * Mathf.Max(0, t - 0.3f);
                data[i] = (tone + sparkle) * env * 0.6f;
            }

            return CreateClip("SFX_ItemUse", data, false);
        }

        private static AudioClip GenerateEquipSFX()
        {
            int totalSamples = SecondsToSamples(0.12f);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float env = Decay(i, totalSamples, 15f);
                float click = SinWave(2200f, i) * 0.4f * Decay(i, totalSamples, 40f);
                float whoosh = SinWave(400f + 800f * t, i) * 0.3f * env;
                data[i] = (click + whoosh) * 0.7f;
            }

            return CreateClip("SFX_Equip", data, false);
        }

        private static AudioClip GenerateSetCompleteSFX()
        {
            int totalSamples = SecondsToSamples(0.4f);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float env = Decay(i, totalSamples, 4f);
                float note1 = SinWave(523f, i) * Mathf.Max(0, 1f - t * 4f);
                float note2 = SinWave(659f, i) * Mathf.Clamp01((t - 0.15f) * 5f) * Mathf.Max(0, 1f - (t - 0.15f) * 3f);
                float note3 = SinWave(784f, i) * Mathf.Clamp01((t - 0.3f) * 6f);
                float shimmer = SinWave(1568f, i) * 0.08f * env;
                data[i] = (note1 * 0.3f + note2 * 0.35f + note3 * 0.35f + shimmer) * env * 0.65f;
            }

            return CreateClip("SFX_SetComplete", data, false);
        }

        // ────────────────────────────────────────────
        //  환경음 생성
        // ────────────────────────────────────────────

        /// <summary>숲 환경음: 낮은 노이즈 + 새소리 (20초 루프).</summary>
        private static AudioClip GenerateForestAmbient()
        {
            const float durationSec = 20f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(200);

            // 새소리 이벤트 미리 생성 (고정 시드)
            int birdEventCount = 12;
            float[] birdTimes = new float[birdEventCount];
            float[] birdFreqs = new float[birdEventCount];
            for (int b = 0; b < birdEventCount; b++)
            {
                birdTimes[b] = (float)(rng.NextDouble() * durationSec);
                birdFreqs[b] = 2000f + (float)(rng.NextDouble() * 2000f);
            }

            // 별도의 노이즈 RNG (매 샘플마다 호출하므로)
            System.Random noiseRng = new System.Random(201);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;

                // 배경 노이즈 (바람/나뭇잎)
                float bg = Noise(noiseRng) * 0.03f;

                // 새소리: chirp
                float bird = 0f;
                for (int b = 0; b < birdEventCount; b++)
                {
                    float chirpStart = birdTimes[b];
                    float chirpDur = 0.15f;
                    if (t >= chirpStart && t < chirpStart + chirpDur)
                    {
                        float localT = t - chirpStart;
                        int localSample = SecondsToSamples(localT);
                        int chirpSamples = SecondsToSamples(chirpDur);
                        float env = Decay(localSample, chirpSamples, 15f);
                        // 떨리는 새소리
                        float vibrato = 1f + 0.1f * SinWave(30f, i);
                        bird += SinWave(birdFreqs[b] * vibrato, i) * env * 0.08f;
                    }
                }

                data[i] = bg + bird;
            }

            return CreateClip("Ambient_Forest", data, true);
        }

        /// <summary>연못 환경음: 물소리 + 개구리 (20초 루프).</summary>
        private static AudioClip GeneratePondAmbient()
        {
            const float durationSec = 20f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(210);

            // 개구리 이벤트 미리 생성
            int frogEventCount = 8;
            float[] frogTimes = new float[frogEventCount];
            for (int f = 0; f < frogEventCount; f++)
            {
                frogTimes[f] = (float)(rng.NextDouble() * durationSec);
            }

            System.Random noiseRng = new System.Random(211);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;

                // 물소리: 필터드 노이즈 (저역 강조)
                float water = Noise(noiseRng) * 0.04f;
                // 간이 저역 필터: 이전 샘플과 블렌딩
                if (i > 0)
                {
                    water = data[i - 1] * 0.7f + water * 0.3f;
                }

                // 개구리: 낮은 주파수 간헐적
                float frog = 0f;
                for (int f = 0; f < frogEventCount; f++)
                {
                    float frogStart = frogTimes[f];
                    float frogDur = 0.3f;
                    if (t >= frogStart && t < frogStart + frogDur)
                    {
                        float localT = t - frogStart;
                        int localSample = SecondsToSamples(localT);
                        int frogSamples = SecondsToSamples(frogDur);
                        float env = Envelope(localSample, frogSamples / 10, frogSamples / 5,
                            0.5f, frogSamples / 2, frogSamples);
                        frog += SinWave(120f + 30f * SinWave(5f, i), i) * env * 0.1f;
                    }
                }

                data[i] = water + frog;
            }

            return CreateClip("Ambient_Pond", data, true);
        }

        /// <summary>밤 환경음: 귀뚜라미 + 바람 (20초 루프).</summary>
        private static AudioClip GenerateNightAmbient()
        {
            const float durationSec = 20f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random noiseRng = new System.Random(221);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;

                // 바람: 저주파 노이즈
                float wind = Noise(noiseRng) * 0.025f;
                if (i > 0)
                {
                    wind = data[i - 1] * 0.8f + wind * 0.2f;
                }

                // 귀뚜라미: 고주파 트릴 (간헐적 on/off)
                float cricketEnv = 0.5f + 0.5f * SinWave(0.3f, i); // 느린 on/off 사이클
                float cricket = SinWave(4200f, i) * 0.03f * cricketEnv;
                // 두 번째 귀뚜라미 (약간 다른 주파수, 다른 타이밍)
                float cricket2Env = 0.5f + 0.5f * SinWave(0.25f, i + SampleRate * 3);
                float cricket2 = SinWave(4800f, i) * 0.02f * cricket2Env;

                data[i] = wind + cricket + cricket2;
            }

            return CreateClip("Ambient_Night", data, true);
        }
    }
}
