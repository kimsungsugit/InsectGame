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
                case "explore_meadow":   clip = GenerateRegionBGM("Meadow", 110, new[] { C4, D4, E4, G4, A4, C5 }, new[] { C3, F4 * 0.5f, G3, C3 }, 42); break;
                case "explore_forest":   clip = GenerateRegionBGM("Forest", 95,  new[] { D4, E4, G4, A4, B4, D5 }, new[] { D2, A2, G3, D2 }, 88); break;
                case "explore_pond":     clip = GenerateRegionBGM("Pond", 100, new[] { F4, G4, A4, C5, D5, F5 }, new[] { F4 * 0.5f, C3, G3, F4 * 0.5f }, 134); break;
                case "explore_swamp":    clip = GenerateRegionBGM("Swamp", 75,  new[] { E4, G4, A4, B4, D5, E5 }, new[] { E3, A2, B3, E3 }, 56); break;
                case "explore_mountain": clip = GenerateRegionBGM("Mountain", 105, new[] { G4, A4, C5, D5, E5, G5 }, new[] { G3, D4, A4, G3 }, 167); break;
                case "explore_garden":   clip = GenerateRegionBGM("Garden", 120, new[] { C5, D5, E5, G5, A5, C6 }, new[] { F4 * 0.5f, A2, G3, C3 }, 73); break;
                case "explore_ruins":    clip = GenerateRegionBGM("Ruins", 80,  new[] { D4, F4, G4, A4, C5, D5 }, new[] { D2, A2, F4 * 0.5f, D2 }, 199); break;
                // ── 2막(ver2) ── 여기 case가 없으면 LogWarning + null이라 그 리전만 무음이 된다.
                // 텅 빈 들은 느리고 베이스가 거의 움직이지 않는다(정체). 모래언덕은 건조한 고음 위주.
                case "explore_hollow":   clip = GenerateRegionBGM("Hollow", 68, new[] { A2 * 2f, C4, D4, E4, G4, A4 }, new[] { A2, A2, G3, A2 }, 211); break;
                case "explore_dunes":    clip = GenerateRegionBGM("Dunes", 92,  new[] { D4, E4, G4, A4, C5, D5 }, new[] { D2, D2, A2, G3 }, 233); break;
                case "explore_frostline": clip = GenerateRegionBGM("Frostline", 62, new[] { E4, G4, B4, D5, E5, G5 }, new[] { E3, E3, B3, E3 }, 251); break;
                case "explore_emberfall": clip = GenerateRegionBGM("Emberfall", 88, new[] { C4, D4, F4, G4, C5, D5 }, new[] { D1 * 2f, C3, G3, C3 }, 277); break;
                case "explore_canopy":   clip = GenerateRegionBGM("Canopy", 112, new[] { G4, A4, B4, D5, E5, G5 }, new[] { G3, D4, E4, C3 }, 307); break;
                case "explore_nameless": clip = GenerateRegionBGM("Nameless", 58, new[] { C4, D4, F4, G4, B4, C5 }, new[] { D1, D1, C3, D1 }, 331); break;
                // ── 2막 보스 테마 ── 리전 곡보다 빠르고 베이스가 한 음에 눌러앉는다(압박).
                // 간부전은 명부회의 사무적인 냉정함, 최종전은 반음 충돌을 섞어 불안정하게.
                case "boss_ledger": clip = GenerateRegionBGM("BossLedger", 138, new[] { D4, F4, G4, A4, C5, D5 }, new[] { D2, D2, D2, A2 }, 353); break;
                case "boss_final":  clip = GenerateRegionBGM("BossFinal", 152, new[] { C4, D4, E4, G4, B4, C5 }, new[] { D1, D1, C3, D1 }, 379); break;
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
                case "footstep":        clip = GenerateFootstepSFX(); break;
                case "level_up_gain":   clip = GenerateLevelUpGainSFX(); break;
                case "menu_hover":      clip = GenerateMenuHoverSFX(); break;
                case "purchase":        clip = GeneratePurchaseSFX(); break;
                case "error":           clip = GenerateErrorSFX(); break;
                case "skill_bug":       clip = GenerateElementSkillSFX(0); break;
                case "skill_poison":    clip = GenerateElementSkillSFX(1); break;
                case "skill_water":     clip = GenerateElementSkillSFX(2); break;
                case "skill_leaf":      clip = GenerateElementSkillSFX(3); break;
                case "skill_wind":      clip = GenerateElementSkillSFX(4); break;
                case "skill_electric":  clip = GenerateElementSkillSFX(5); break;
                case "skill_earth":     clip = GenerateElementSkillSFX(6); break;
                case "skill_light":     clip = GenerateElementSkillSFX(7); break;
                case "skill_dark":      clip = GenerateElementSkillSFX(8); break;
                case "skill_metal":     clip = GenerateElementSkillSFX(9); break;
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
                case "cave":         clip = GenerateCaveAmbient(); break;
                case "underground":  clip = GenerateUndergroundAmbient(); break;
                case "deep_forest":  clip = GenerateDeepForestAmbient(); break;
                case "underwater":   clip = GenerateUnderwaterAmbient(); break;
                case "fog":          clip = GenerateFogAmbient(); break;
                case "reeds":        clip = GenerateReedsAmbient(); break;
                case "peak":         clip = GeneratePeakAmbient(); break;
                case "flower_maze":  clip = GenerateFlowerMazeAmbient(); break;
                case "greenhouse":   clip = GenerateGreenhouseAmbient(); break;
                case "temple":       clip = GenerateTempleAmbient(); break;
                case "day":          clip = GenerateDayAmbient(); break;
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

        /// <summary>배음 합산 (1f, 0.5f, 0.33f, 0.25f 비율로 부분배음 추가) — 풍부한 음색.</summary>
        private static float Harmonic(float freq, int sample, float[] partials = null)
        {
            float[] amps = partials ?? new[] { 1f, 0.5f, 0.33f, 0.25f };
            float sum = 0f;
            float total = 0f;
            for (int p = 0; p < amps.Length; p++)
            {
                sum += SinWave(freq * (p + 1), sample) * amps[p];
                total += amps[p];
            }
            return total > 0f ? sum / total : 0f;
        }

        /// <summary>간이 reverb: comb filter 4탭 합산 in-place.</summary>
        private static void ApplySimpleReverb(float[] data, float decay = 0.4f, int delayMs = 80)
        {
            int delaySamples = SampleRate * delayMs / 1000;
            int[] delays = { delaySamples, delaySamples * 2, (int)(delaySamples * 1.5f), (int)(delaySamples * 2.7f) };
            float[] gains = { decay, decay * 0.7f, decay * 0.5f, decay * 0.35f };

            float[] buffer = new float[data.Length];
            System.Array.Copy(data, buffer, data.Length);

            for (int i = 0; i < data.Length; i++)
            {
                float wet = 0f;
                for (int t = 0; t < delays.Length; t++)
                {
                    int srcIdx = i - delays[t];
                    if (srcIdx >= 0) wet += buffer[srcIdx] * gains[t];
                }
                data[i] = Mathf.Clamp(buffer[i] + wet * 0.5f, -1f, 1f);
            }
        }

        /// <summary>간이 echo: 단일 탭 + 피드백.</summary>
        private static void ApplyEcho(float[] data, int delayMs = 200, float feedback = 0.4f, float wetMix = 0.3f)
        {
            int delaySamples = SampleRate * delayMs / 1000;
            float[] buffer = new float[data.Length];
            System.Array.Copy(data, buffer, data.Length);

            for (int i = 0; i < data.Length; i++)
            {
                float echo = 0f;
                int srcIdx = i - delaySamples;
                if (srcIdx >= 0) echo = buffer[srcIdx] * feedback;
                int srcIdx2 = i - delaySamples * 2;
                if (srcIdx2 >= 0) echo += buffer[srcIdx2] * feedback * 0.5f;
                data[i] = Mathf.Clamp(buffer[i] + echo * wetMix, -1f, 1f);
            }
        }

        /// <summary>모티프 기반 멜로디 시퀀스 생성. motif는 음계 인덱스 배열, bars만큼 반복하며 변조.</summary>
        private static int[] GenerateMotifMelody(int[] motif, int scaleSize, int bars, int notesPerBar, int seed)
        {
            System.Random rng = new System.Random(seed);
            int[] sequence = new int[bars * notesPerBar];

            for (int b = 0; b < bars; b++)
            {
                int variation = rng.Next(4); // 0=원본, 1=transpose, 2=invert, 3=retrograde
                int transpose = rng.Next(-2, 3);

                for (int n = 0; n < notesPerBar; n++)
                {
                    int motifIdx = n % motif.Length;
                    int note;
                    switch (variation)
                    {
                        case 1: note = motif[motifIdx] + transpose; break;
                        case 2: note = (scaleSize - 1) - motif[motifIdx]; break; // invert
                        case 3: note = motif[(motif.Length - 1) - motifIdx]; break; // retrograde
                        default: note = motif[motifIdx]; break;
                    }
                    sequence[b * notesPerBar + n] = ((note % scaleSize) + scaleSize) % scaleSize;
                }
            }

            return sequence;
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

        // ────────────────────────────────────────────
        //  신규 BGM (리전별)
        // ────────────────────────────────────────────

        /// <summary>리전별 탐험 BGM 공통 생성기 — 음계/베이스/BPM/시드만 다름.</summary>
        private static AudioClip GenerateRegionBGM(string label, float bpm, float[] melodyScale, float[] bassNotes, int seed)
        {
            const float durationSec = 16f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(seed);

            float secPerBeat = 60f / bpm;
            int samplesPerBeat = SecondsToSamples(secPerBeat);
            int samplesPerEighth = samplesPerBeat / 2;

            int totalEighths = (int)(durationSec / (secPerBeat * 0.5f));
            // 모티프 기반 멜로디: 4음 모티프 + 변조
            int[] motif = { 0, 2, 4, 3 };
            int notesPerBar = 8;
            int bars = totalEighths / notesPerBar;
            int[] melodySequence = GenerateMotifMelody(motif, melodyScale.Length, bars, notesPerBar, seed);

            float[] arpNotes = { melodyScale[0], melodyScale[2], melodyScale[4], melodyScale[5] };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float beatPos = t / secPerBeat;

                int bassIndex = ((int)(beatPos / 4f)) % bassNotes.Length;
                float bass = SinWave(bassNotes[bassIndex], i) * 0.3f;

                int eighthIndex = ((int)(beatPos * 2f)) % melodySequence.Length;
                float melFreq = melodyScale[melodySequence[eighthIndex]];
                int posInEighth = i % samplesPerEighth;
                float melEnv = Envelope(posInEighth, samplesPerEighth / 10, samplesPerEighth / 5,
                    0.6f, samplesPerEighth / 4, samplesPerEighth);
                // 배음 합산으로 더 풍부한 음색
                float melody = Harmonic(melFreq, i) * 0.22f * melEnv;

                int arpIndex = ((int)(beatPos * 4f)) % arpNotes.Length;
                int posInSixteenth = i % (samplesPerEighth / 2);
                int sixteenthLen = samplesPerEighth / 2;
                float arpEnv = Envelope(posInSixteenth, sixteenthLen / 8, sixteenthLen / 6,
                    0.4f, sixteenthLen / 3, sixteenthLen);
                float arp = TriWave(arpNotes[arpIndex], i) * 0.13f * arpEnv;

                float pad = Noise(rng) * 0.015f;

                data[i] = bass + melody + arp + pad;
            }

            return CreateClip("BGM_" + label, data, true);
        }

        // ────────────────────────────────────────────
        //  신규 SFX
        // ────────────────────────────────────────────

        /// <summary>발소리: 짧은 노이즈 버스트 (0.08초).</summary>
        private static AudioClip GenerateFootstepSFX()
        {
            int totalSamples = SecondsToSamples(0.08f);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(30);

            for (int i = 0; i < totalSamples; i++)
            {
                float env = Decay(i, totalSamples, 25f);
                float thump = SinWave(120f, i) * 0.3f * env;
                float scuff = Noise(rng) * 0.15f * env;
                data[i] = (thump + scuff) * 0.5f;
            }

            return CreateClip("SFX_Footstep", data, false);
        }

        /// <summary>레벨업 획득 SFX: 부드러운 상승 (0.25초).</summary>
        private static AudioClip GenerateLevelUpGainSFX()
        {
            int totalSamples = SecondsToSamples(0.25f);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float freq = Mathf.Lerp(440f, 880f, t);
                float env = Envelope(i, totalSamples / 10, totalSamples / 5,
                    0.7f, totalSamples / 3, totalSamples);
                float sparkle = SinWave(freq * 2f, i) * 0.1f * t;
                data[i] = (SinWave(freq, i) * 0.4f + sparkle) * env;
            }

            return CreateClip("SFX_LevelUpGain", data, false);
        }

        /// <summary>메뉴 호버 SFX: 짧은 사인 (0.04초).</summary>
        private static AudioClip GenerateMenuHoverSFX()
        {
            int totalSamples = SecondsToSamples(0.04f);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float env = Decay(i, totalSamples, 35f);
                data[i] = SinWave(1800f, i) * env * 0.3f;
            }

            return CreateClip("SFX_MenuHover", data, false);
        }

        /// <summary>구매 SFX: 코인 짤랑 (0.3초).</summary>
        private static AudioClip GeneratePurchaseSFX()
        {
            int totalSamples = SecondsToSamples(0.3f);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(31);

            float[] notes = { C5, E5, G5 };
            float noteLen = 0.08f;
            int noteSamples = SecondsToSamples(noteLen);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;

                for (int n = 0; n < notes.Length; n++)
                {
                    float noteStart = n * noteLen * 0.6f;
                    if (t >= noteStart && t < noteStart + noteLen)
                    {
                        int posInNote = SecondsToSamples(t - noteStart);
                        float env = Decay(posInNote, noteSamples, 12f);
                        sample += SinWave(notes[n], i) * 0.35f * env;
                    }
                }

                // 짤랑 노이즈
                if (t < 0.15f)
                {
                    float chimeEnv = Decay(i, SecondsToSamples(0.15f), 18f);
                    sample += Noise(rng) * 0.05f * chimeEnv;
                }

                data[i] = sample;
            }

            return CreateClip("SFX_Purchase", data, false);
        }

        /// <summary>오류 SFX: 하강 2음 (0.25초).</summary>
        private static AudioClip GenerateErrorSFX()
        {
            int totalSamples = SecondsToSamples(0.25f);
            float[] data = new float[totalSamples];

            float[] notes = { D4, A2 };
            float noteLen = 0.12f;

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
                        sample += SquareWave(notes[n], i) * 0.25f * env;
                    }
                }

                data[i] = sample;
            }

            return CreateClip("SFX_Error", data, false);
        }

        /// <summary>속성별 스킬 효과음 (0.3초). element index: 0=Bug, 1=Poison, 2=Water, ...</summary>
        private static AudioClip GenerateElementSkillSFX(int elementIndex)
        {
            int totalSamples = SecondsToSamples(0.3f);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(40 + elementIndex);

            // 속성별 파라미터: (베이스 주파수, 파형 종류 0=Sin/1=Tri/2=Sqr, 노이즈 비율, 변조 깊이)
            float baseFreq = 600f;
            int waveType = 0;
            float noiseAmt = 0.1f;
            float modDepth = 200f;
            switch (elementIndex)
            {
                case 0: baseFreq = 800f; waveType = 0; noiseAmt = 0.2f; modDepth = 100f; break; // Bug: 가벼운 윙
                case 1: baseFreq = 350f; waveType = 1; noiseAmt = 0.3f; modDepth = 150f; break; // Poison: 부글부글
                case 2: baseFreq = 500f; waveType = 0; noiseAmt = 0.4f; modDepth = 300f; break; // Water: 물 튀김
                case 3: baseFreq = 700f; waveType = 1; noiseAmt = 0.15f; modDepth = 200f; break; // Leaf: 나뭇잎 스침
                case 4: baseFreq = 1000f; waveType = 0; noiseAmt = 0.5f; modDepth = 400f; break; // Wind: 휘이잉
                case 5: baseFreq = 1500f; waveType = 2; noiseAmt = 0.3f; modDepth = 600f; break; // Electric: 지지직
                case 6: baseFreq = 200f; waveType = 2; noiseAmt = 0.4f; modDepth = 80f; break;  // Earth: 쿵
                case 7: baseFreq = 1200f; waveType = 0; noiseAmt = 0.05f; modDepth = 500f; break; // Light: 반짝
                case 8: baseFreq = 280f; waveType = 1; noiseAmt = 0.25f; modDepth = 120f; break; // Dark: 어두운 윙
                case 9: baseFreq = 900f; waveType = 2; noiseAmt = 0.2f; modDepth = 50f; break;   // Metal: 챙
            }

            for (int i = 0; i < totalSamples; i++)
            {
                float env = Decay(i, totalSamples, 6f);
                float modFreq = baseFreq + modDepth * SinWave(8f, i);

                float wave = waveType == 0 ? SinWave(modFreq, i)
                           : waveType == 1 ? TriWave(modFreq, i)
                           : SquareWave(modFreq, i);

                float noise = Noise(rng) * noiseAmt;
                data[i] = (wave * 0.4f + noise) * env * 0.6f;
            }

            return CreateClip($"SFX_Skill_{elementIndex}", data, false);
        }

        // ────────────────────────────────────────────
        //  신규 환경음 (서브에리어)
        // ────────────────────────────────────────────

        /// <summary>동굴 환경음: 물방울 + 낮은 윙 + 메아리 노이즈 (20초).</summary>
        private static AudioClip GenerateCaveAmbient()
        {
            const float durationSec = 20f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(300);

            int dripCount = 8;
            float[] dripTimes = new float[dripCount];
            for (int d = 0; d < dripCount; d++)
                dripTimes[d] = (float)(rng.NextDouble() * durationSec);

            System.Random noiseRng = new System.Random(301);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;

                // 낮은 동굴 윙 (저주파 드론)
                float drone = SinWave(60f, i) * 0.06f + SinWave(90f, i) * 0.04f;

                // 메아리 노이즈
                float echo = Noise(noiseRng) * 0.015f;

                // 물방울
                float drip = 0f;
                for (int d = 0; d < dripCount; d++)
                {
                    float dripStart = dripTimes[d];
                    float dripDur = 0.2f;
                    if (t >= dripStart && t < dripStart + dripDur)
                    {
                        float localT = t - dripStart;
                        int localSample = SecondsToSamples(localT);
                        int dripSamples = SecondsToSamples(dripDur);
                        float env = Decay(localSample, dripSamples, 18f);
                        drip += SinWave(900f - localT * 600f, i) * env * 0.1f;
                    }
                }

                data[i] = drone + echo + drip;
            }

            ApplySimpleReverb(data, 0.5f, 120);
            return CreateClip("Ambient_Cave", data, true);
        }

        /// <summary>지하 환경음: 더 깊은 동굴, 미세한 바람 (20초).</summary>
        private static AudioClip GenerateUndergroundAmbient()
        {
            const float durationSec = 20f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random noiseRng = new System.Random(311);

            for (int i = 0; i < totalSamples; i++)
            {
                float drone = SinWave(40f, i) * 0.07f + SinWave(55f, i) * 0.04f;
                float wind = Noise(noiseRng) * 0.02f;
                if (i > 0) wind = data[i - 1] * 0.85f + wind * 0.15f;
                data[i] = drone + wind;
            }

            ApplySimpleReverb(data, 0.6f, 180);
            return CreateClip("Ambient_Underground", data, true);
        }

        /// <summary>깊은 숲: 더 어두운 노이즈 + 멀리서 새 (20초).</summary>
        private static AudioClip GenerateDeepForestAmbient()
        {
            const float durationSec = 20f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(320);
            System.Random noiseRng = new System.Random(321);

            int birdCount = 5;
            float[] birdTimes = new float[birdCount];
            for (int b = 0; b < birdCount; b++)
                birdTimes[b] = (float)(rng.NextDouble() * durationSec);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float bg = Noise(noiseRng) * 0.04f;

                float bird = 0f;
                for (int b = 0; b < birdCount; b++)
                {
                    if (t >= birdTimes[b] && t < birdTimes[b] + 0.2f)
                    {
                        float localT = t - birdTimes[b];
                        int localSample = SecondsToSamples(localT);
                        float env = Decay(localSample, SecondsToSamples(0.2f), 12f);
                        bird += SinWave(1500f + 300f * SinWave(20f, i), i) * env * 0.04f;
                    }
                }

                data[i] = bg + bird;
            }

            return CreateClip("Ambient_DeepForest", data, true);
        }

        /// <summary>수중 환경음: 거품 + 저음 드론 (20초).</summary>
        private static AudioClip GenerateUnderwaterAmbient()
        {
            const float durationSec = 20f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(330);
            System.Random noiseRng = new System.Random(331);

            int bubbleCount = 15;
            float[] bubbleTimes = new float[bubbleCount];
            for (int b = 0; b < bubbleCount; b++)
                bubbleTimes[b] = (float)(rng.NextDouble() * durationSec);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float drone = SinWave(80f, i) * 0.06f;
                float water = Noise(noiseRng) * 0.025f;
                if (i > 0) water = data[i - 1] * 0.9f + water * 0.1f;

                float bubble = 0f;
                for (int b = 0; b < bubbleCount; b++)
                {
                    if (t >= bubbleTimes[b] && t < bubbleTimes[b] + 0.15f)
                    {
                        float localT = t - bubbleTimes[b];
                        int localSample = SecondsToSamples(localT);
                        float env = Decay(localSample, SecondsToSamples(0.15f), 20f);
                        bubble += SinWave(400f + localT * 400f, i) * env * 0.05f;
                    }
                }

                data[i] = drone + water + bubble;
            }

            return CreateClip("Ambient_Underwater", data, true);
        }

        /// <summary>안개 늪: 미스터리한 윙윙 + 물 (20초).</summary>
        private static AudioClip GenerateFogAmbient()
        {
            const float durationSec = 20f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random noiseRng = new System.Random(341);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float mistDrone = SinWave(110f + 30f * SinWave(0.2f, i), i) * 0.05f;
                float wisp = SinWave(220f, i) * 0.03f * (0.5f + 0.5f * SinWave(0.3f, i));
                float bg = Noise(noiseRng) * 0.025f;
                data[i] = mistDrone + wisp + bg;
            }

            return CreateClip("Ambient_Fog", data, true);
        }

        /// <summary>갈대밭: 바람에 흔들리는 갈대 (20초).</summary>
        private static AudioClip GenerateReedsAmbient()
        {
            const float durationSec = 20f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random noiseRng = new System.Random(351);

            for (int i = 0; i < totalSamples; i++)
            {
                float windEnv = 0.3f + 0.7f * (0.5f + 0.5f * SinWave(0.4f, i));
                float rustleNoise = Noise(noiseRng) * 0.05f * windEnv;
                if (i > 0) rustleNoise = data[i - 1] * 0.7f + rustleNoise * 0.3f;
                data[i] = rustleNoise;
            }

            return CreateClip("Ambient_Reeds", data, true);
        }

        /// <summary>산 정상: 바람 휘이잉 (20초).</summary>
        private static AudioClip GeneratePeakAmbient()
        {
            const float durationSec = 20f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random noiseRng = new System.Random(361);

            for (int i = 0; i < totalSamples; i++)
            {
                float windPower = 0.5f + 0.5f * SinWave(0.15f, i);
                float wind = Noise(noiseRng) * 0.06f * windPower;
                if (i > 0) wind = data[i - 1] * 0.75f + wind * 0.25f;
                float whistle = SinWave(280f + 80f * SinWave(0.5f, i), i) * 0.02f * windPower;
                data[i] = wind + whistle;
            }

            return CreateClip("Ambient_Peak", data, true);
        }

        /// <summary>꽃밭: 평화로운 새소리 + 벌 윙윙 (20초).</summary>
        private static AudioClip GenerateFlowerMazeAmbient()
        {
            const float durationSec = 20f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(370);
            System.Random noiseRng = new System.Random(371);

            int birdCount = 14;
            float[] birdTimes = new float[birdCount];
            float[] birdFreqs = new float[birdCount];
            for (int b = 0; b < birdCount; b++)
            {
                birdTimes[b] = (float)(rng.NextDouble() * durationSec);
                birdFreqs[b] = 2200f + (float)(rng.NextDouble() * 1500f);
            }

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float bg = Noise(noiseRng) * 0.02f;
                float beeBuzz = SinWave(220f, i) * 0.015f * (0.5f + 0.5f * SinWave(0.7f, i));

                float bird = 0f;
                for (int b = 0; b < birdCount; b++)
                {
                    if (t >= birdTimes[b] && t < birdTimes[b] + 0.12f)
                    {
                        float localT = t - birdTimes[b];
                        int localSample = SecondsToSamples(localT);
                        float env = Decay(localSample, SecondsToSamples(0.12f), 18f);
                        bird += SinWave(birdFreqs[b] * (1f + 0.05f * SinWave(40f, i)), i) * env * 0.06f;
                    }
                }

                data[i] = bg + bird + beeBuzz;
            }

            return CreateClip("Ambient_FlowerMaze", data, true);
        }

        /// <summary>온실: 잔잔한 물 + 환풍 (20초).</summary>
        private static AudioClip GenerateGreenhouseAmbient()
        {
            const float durationSec = 20f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random noiseRng = new System.Random(381);

            for (int i = 0; i < totalSamples; i++)
            {
                float fanHum = SinWave(120f, i) * 0.04f + SinWave(180f, i) * 0.02f;
                float bg = Noise(noiseRng) * 0.02f;
                if (i > 0) bg = data[i - 1] * 0.8f + bg * 0.2f;
                data[i] = fanHum + bg;
            }

            return CreateClip("Ambient_Greenhouse", data, true);
        }

        /// <summary>사원: 신비로운 메아리 + 종소리 (20초).</summary>
        private static AudioClip GenerateTempleAmbient()
        {
            const float durationSec = 20f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(390);

            int bellCount = 4;
            float[] bellTimes = new float[bellCount];
            for (int b = 0; b < bellCount; b++)
                bellTimes[b] = (float)(rng.NextDouble() * durationSec);

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;

                // 신비한 패드
                float pad = (SinWave(165f, i) + SinWave(220f, i) + SinWave(330f, i)) * 0.025f;

                // 메아리 종
                float bell = 0f;
                for (int b = 0; b < bellCount; b++)
                {
                    if (t >= bellTimes[b])
                    {
                        float localT = t - bellTimes[b];
                        if (localT < 3f)
                        {
                            int localSample = SecondsToSamples(localT);
                            float env = Decay(localSample, SecondsToSamples(3f), 1.5f);
                            bell += (SinWave(523f, i) + SinWave(659f, i) * 0.6f) * 0.08f * env;
                        }
                    }
                }

                data[i] = pad + bell;
            }

            ApplySimpleReverb(data, 0.55f, 150);
            return CreateClip("Ambient_Temple", data, true);
        }

        /// <summary>낮 환경음: 밝은 새소리 + 부드러운 바람 (20초).</summary>
        private static AudioClip GenerateDayAmbient()
        {
            const float durationSec = 20f;
            int totalSamples = SecondsToSamples(durationSec);
            float[] data = new float[totalSamples];
            System.Random rng = new System.Random(400);
            System.Random noiseRng = new System.Random(401);

            int birdCount = 10;
            float[] birdTimes = new float[birdCount];
            float[] birdFreqs = new float[birdCount];
            for (int b = 0; b < birdCount; b++)
            {
                birdTimes[b] = (float)(rng.NextDouble() * durationSec);
                birdFreqs[b] = 2500f + (float)(rng.NextDouble() * 1500f);
            }

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / SampleRate;
                float breeze = Noise(noiseRng) * 0.025f;
                if (i > 0) breeze = data[i - 1] * 0.6f + breeze * 0.4f;

                float bird = 0f;
                for (int b = 0; b < birdCount; b++)
                {
                    if (t >= birdTimes[b] && t < birdTimes[b] + 0.18f)
                    {
                        float localT = t - birdTimes[b];
                        int localSample = SecondsToSamples(localT);
                        float env = Decay(localSample, SecondsToSamples(0.18f), 14f);
                        float vib = 1f + 0.08f * SinWave(35f, i);
                        bird += SinWave(birdFreqs[b] * vib, i) * env * 0.07f;
                    }
                }

                data[i] = breeze + bird;
            }

            return CreateClip("Ambient_Day", data, true);
        }
    }
}
