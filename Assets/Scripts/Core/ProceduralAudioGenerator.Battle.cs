using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 전투 목소리 — 곤충 계열별 울음(<c>cry_*</c>)·비명(<c>hurt_*</c>)과 층을 쌓은 타격음.
    ///
    /// 예전 타격음은 100Hz 사인 + 노이즈 0.1초 한 겹이라 "톡"에 가까웠고, 곤충은 기술을 쓰든
    /// 맞든 소리를 내지 않았다. 계열 분류는 전투 쪽(<c>BattleShout.CryFor</c>)이 하고 여기선
    /// 키 문자열만 받는다 — Core가 Battle을 참조하지 않게.
    /// </summary>
    public static partial class ProceduralAudioGenerator
    {
        /// <summary>울음 한 소리의 설계값. 음높이는 시작→정점→끝 세 점을 잇는다.</summary>
        private struct VoiceSpec
        {
            public float Seconds;
            public float PitchStart, PitchPeak, PitchEnd;
            public float PeakAt;          // 0..1 — 정점 위치
            public float Saw;             // 0=사인, 1=톱니(거친 배음)
            public float VibratoHz, VibratoDepth;
            public float RoughHz, RoughDepth;   // 진폭 떨림 — 으르렁·붕붕의 "결"
            public float Noise;           // 노이즈 비율
            public float NoiseTone;       // 0=어두운(저역) … 1=밝은(치찰음)
            public float Brightness;      // 톤 저역통과 — 낮을수록 둥글다
            public int Pulses;            // >0이면 끊어 우는 소리(귀뚜라미·딸깍)
            public float PulseDuty;
            public float Attack, Release; // 초
            public float Drive;           // 포화 — 클수록 거칠다
            public float Sub;             // 한 옥타브 아래 사인(보스의 무게)
            public int Seed;
        }

        private static bool TryGenerateBattleVoice(string type, out AudioClip clip)
        {
            clip = null;
            switch (type)
            {
                // 딱정벌레류 — 낮게 으르렁 "그르옷"
                case "cry_beetle": clip = Voice("cry_beetle", new VoiceSpec { Seconds = 0.42f, PitchStart = 130f, PitchPeak = 210f, PitchEnd = 165f, PeakAt = 0.55f, Saw = 0.9f, VibratoHz = 7f, VibratoDepth = 0.03f, RoughHz = 28f, RoughDepth = 0.55f, Noise = 0.2f, NoiseTone = 0.4f, Brightness = 0.45f, Attack = 0.02f, Release = 0.12f, Drive = 2.6f, Sub = 0.12f, Seed = 101 }); break;
                case "hurt_beetle": clip = Voice("hurt_beetle", new VoiceSpec { Seconds = 0.24f, PitchStart = 230f, PitchPeak = 240f, PitchEnd = 110f, PeakAt = 0.1f, Saw = 0.9f, VibratoHz = 9f, VibratoDepth = 0.04f, RoughHz = 34f, RoughDepth = 0.6f, Noise = 0.24f, NoiseTone = 0.45f, Brightness = 0.45f, Attack = 0.005f, Release = 0.1f, Drive = 3f, Sub = 0.1f, Seed = 102 }); break;
                // 사마귀 — 쉭 하는 치찰음 위로 날카로운 끽
                case "cry_mantis": clip = Voice("cry_mantis", new VoiceSpec { Seconds = 0.36f, PitchStart = 1700f, PitchPeak = 2600f, PitchEnd = 2200f, PeakAt = 0.35f, Saw = 0.55f, VibratoHz = 22f, VibratoDepth = 0.05f, RoughHz = 0f, Noise = 0.62f, NoiseTone = 0.9f, Brightness = 0.55f, Attack = 0.006f, Release = 0.14f, Drive = 1.6f, Seed = 111 }); break;
                case "hurt_mantis": clip = Voice("hurt_mantis", new VoiceSpec { Seconds = 0.2f, PitchStart = 2700f, PitchPeak = 2800f, PitchEnd = 1500f, PeakAt = 0.1f, Saw = 0.6f, VibratoHz = 30f, VibratoDepth = 0.06f, Noise = 0.5f, NoiseTone = 0.85f, Brightness = 0.5f, Attack = 0.003f, Release = 0.09f, Drive = 1.8f, Seed = 112 }); break;
                // 잠자리·나비·나방 — 날갯짓 파닥임
                case "cry_flyer": clip = Voice("cry_flyer", new VoiceSpec { Seconds = 0.4f, PitchStart = 520f, PitchPeak = 800f, PitchEnd = 680f, PeakAt = 0.5f, Saw = 0.2f, VibratoHz = 5f, VibratoDepth = 0.04f, Noise = 0.62f, NoiseTone = 0.45f, Brightness = 0.4f, Pulses = 14, PulseDuty = 0.6f, Attack = 0.02f, Release = 0.14f, Drive = 1.2f, Seed = 121 }); break;
                case "hurt_flyer": clip = Voice("hurt_flyer", new VoiceSpec { Seconds = 0.22f, PitchStart = 820f, PitchPeak = 840f, PitchEnd = 420f, PeakAt = 0.1f, Saw = 0.25f, VibratoHz = 8f, VibratoDepth = 0.05f, Noise = 0.55f, NoiseTone = 0.5f, Brightness = 0.4f, Pulses = 7, PulseDuty = 0.55f, Attack = 0.004f, Release = 0.1f, Drive = 1.4f, Seed = 122 }); break;
                // 벌·말벌·파리 — 붕붕
                case "cry_buzzer": clip = Voice("cry_buzzer", new VoiceSpec { Seconds = 0.46f, PitchStart = 185f, PitchPeak = 255f, PitchEnd = 235f, PeakAt = 0.6f, Saw = 1f, VibratoHz = 11f, VibratoDepth = 0.05f, RoughHz = 90f, RoughDepth = 0.3f, Noise = 0.08f, NoiseTone = 0.5f, Brightness = 0.6f, Attack = 0.03f, Release = 0.12f, Drive = 1.8f, Seed = 131 }); break;
                case "hurt_buzzer": clip = Voice("hurt_buzzer", new VoiceSpec { Seconds = 0.24f, PitchStart = 270f, PitchPeak = 280f, PitchEnd = 140f, PeakAt = 0.1f, Saw = 1f, VibratoHz = 16f, VibratoDepth = 0.08f, RoughHz = 70f, RoughDepth = 0.35f, Noise = 0.1f, NoiseTone = 0.5f, Brightness = 0.6f, Attack = 0.004f, Release = 0.1f, Drive = 2f, Seed = 132 }); break;
                // 귀뚜라미·메뚜기·매미 — 끊어 우는 찌르르
                case "cry_chirper": clip = Voice("cry_chirper", new VoiceSpec { Seconds = 0.34f, PitchStart = 4000f, PitchPeak = 4400f, PitchEnd = 4300f, PeakAt = 0.4f, Saw = 0.1f, VibratoHz = 0f, Noise = 0.05f, NoiseTone = 0.9f, Brightness = 0.8f, Pulses = 5, PulseDuty = 0.55f, Attack = 0.01f, Release = 0.08f, Drive = 1.1f, Seed = 141 }); break;
                case "hurt_chirper": clip = Voice("hurt_chirper", new VoiceSpec { Seconds = 0.18f, PitchStart = 4600f, PitchPeak = 4600f, PitchEnd = 3100f, PeakAt = 0.05f, Saw = 0.15f, Noise = 0.1f, NoiseTone = 0.9f, Brightness = 0.8f, Pulses = 3, PulseDuty = 0.6f, Attack = 0.003f, Release = 0.06f, Drive = 1.2f, Seed = 142 }); break;
                // 거미·지네·개미 — 딸깍거리는 턱 소리
                case "cry_crawler": clip = Voice("cry_crawler", new VoiceSpec { Seconds = 0.32f, PitchStart = 1300f, PitchPeak = 1600f, PitchEnd = 1100f, PeakAt = 0.5f, Saw = 0.4f, Noise = 0.82f, NoiseTone = 0.75f, Brightness = 0.6f, Pulses = 9, PulseDuty = 0.22f, Attack = 0.004f, Release = 0.06f, Drive = 1.5f, Seed = 151 }); break;
                case "hurt_crawler": clip = Voice("hurt_crawler", new VoiceSpec { Seconds = 0.18f, PitchStart = 1700f, PitchPeak = 1700f, PitchEnd = 900f, PeakAt = 0.05f, Saw = 0.45f, Noise = 0.75f, NoiseTone = 0.7f, Brightness = 0.6f, Pulses = 5, PulseDuty = 0.25f, Attack = 0.003f, Release = 0.05f, Drive = 1.6f, Seed = 152 }); break;
                // 레이드 보스 — 깊고 긴 포효
                case "cry_boss": clip = Voice("cry_boss", new VoiceSpec { Seconds = 0.95f, PitchStart = 88f, PitchPeak = 132f, PitchEnd = 74f, PeakAt = 0.35f, Saw = 1f, VibratoHz = 5f, VibratoDepth = 0.04f, RoughHz = 32f, RoughDepth = 0.6f, Noise = 0.38f, NoiseTone = 0.35f, Brightness = 0.42f, Attack = 0.05f, Release = 0.35f, Drive = 3.4f, Sub = 0.3f, Seed = 161 }, true); break;
                case "hurt_boss": clip = Voice("hurt_boss", new VoiceSpec { Seconds = 0.45f, PitchStart = 140f, PitchPeak = 146f, PitchEnd = 70f, PeakAt = 0.08f, Saw = 1f, VibratoHz = 7f, VibratoDepth = 0.05f, RoughHz = 36f, RoughDepth = 0.6f, Noise = 0.34f, NoiseTone = 0.4f, Brightness = 0.42f, Attack = 0.008f, Release = 0.2f, Drive = 3.4f, Sub = 0.25f, Seed = 162 }, true); break;
                default:
                    return false;
            }
            return clip != null;
        }

        /// <summary>
        /// 울음 합성 — 음높이를 위상 누적으로 굴린다(<c>SinWave(freq, sample)</c>처럼 주파수×시각으로 계산하면
        /// 음높이가 움직일 때 위상이 튀어 딸깍 소리가 난다).
        /// </summary>
        private static AudioClip Voice(string name, VoiceSpec v, bool reverb = false)
        {
            int total = SecondsToSamples(v.Seconds);
            float[] data = new float[total];
            System.Random rng = new System.Random(v.Seed);
            float phase = 0f, subPhase = 0f;
            float toneLp = 0f, noiseHi = 0f, noiseLo = 0f;
            float toneCoef = Mathf.Lerp(0.04f, 0.6f, Mathf.Clamp01(v.Brightness));
            // 노이즈는 대역통과로 — 중심 300Hz(어두움)~5kHz(치찰음), 위아래 한 옥타브씩.
            // 1차 미분으로 밝히면 에너지가 14~22kHz로 몰려 폰 스피커에선 안 들리고 귀만 찌른다(실측).
            float noiseCenter = Mathf.Lerp(300f, 5000f, Mathf.Clamp01(v.NoiseTone));
            float noiseCoefHi = OnePoleCoef(noiseCenter * 2f);
            float noiseCoefLo = OnePoleCoef(noiseCenter * 0.5f);
            int attack = Mathf.Max(1, SecondsToSamples(v.Attack));
            int release = Mathf.Max(1, SecondsToSamples(v.Release));
            float drive = Mathf.Max(1f, v.Drive);
            float driveNorm = 1f / (float)System.Math.Tanh(drive);

            for (int i = 0; i < total; i++)
            {
                float t = (float)i / SampleRate;
                float u = (float)i / total;
                float pitch = u < v.PeakAt
                    ? Mathf.Lerp(v.PitchStart, v.PitchPeak, Mathf.SmoothStep(0f, 1f, u / Mathf.Max(0.001f, v.PeakAt)))
                    : Mathf.Lerp(v.PitchPeak, v.PitchEnd, Mathf.SmoothStep(0f, 1f, (u - v.PeakAt) / Mathf.Max(0.001f, 1f - v.PeakAt)));
                pitch *= 1f + v.VibratoDepth * Mathf.Sin(2f * Mathf.PI * v.VibratoHz * t);
                phase += pitch / SampleRate;
                phase -= Mathf.Floor(phase);
                subPhase += pitch * 0.5f / SampleRate;
                subPhase -= Mathf.Floor(subPhase);

                float saw = 2f * phase - 1f;
                float sine = Mathf.Sin(2f * Mathf.PI * phase);
                float tone = Mathf.Lerp(sine, saw, v.Saw);
                toneLp += (tone - toneLp) * toneCoef;

                float white = Noise(rng);
                noiseHi += (white - noiseHi) * noiseCoefHi;
                noiseLo += (noiseHi - noiseLo) * noiseCoefLo;
                float noise = (noiseHi - noiseLo) * 2.5f;

                float s = toneLp * (1f - v.Noise) * 1.6f + noise * v.Noise;
                if (v.Sub > 0f) s += Mathf.Sin(2f * Mathf.PI * subPhase) * v.Sub;
                if (v.RoughDepth > 0f)
                    s *= 1f - v.RoughDepth * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * v.RoughHz * t));
                if (v.Pulses > 0)
                {
                    float cell = u * v.Pulses;
                    float local = cell - Mathf.Floor(cell);
                    float gate = local < v.PulseDuty
                        ? Mathf.Sin(Mathf.PI * local / Mathf.Max(0.01f, v.PulseDuty))
                        : 0f;
                    s *= gate;
                }

                float env = i < attack ? (float)i / attack
                    : i > total - release ? (float)(total - i) / release
                    : 1f;
                data[i] = (float)System.Math.Tanh(s * env * drive) * driveNorm;
            }
            // 끝단 4단 저역통과(6.5kHz) — 1차 필터 한 겹으론 톱니 배음·치찰 노이즈의 40%가 10kHz 위에
            // 남았다(실측). 그 대역은 폰 스피커가 못 내고 이어폰에선 귀만 찌른다.
            LowPass(data, 6500f, 4);
            if (reverb) ApplySimpleReverb(data, 0.35f, 70);
            Normalize(data, 0.95f);
            return CreateClip("SFX_" + name, data, false);
        }

        /// <summary>
        /// 타격음 — 딸깍(고역 순간음) + 퍽(중역 노이즈 덩어리) + 쿵(내려가는 저음) + 부서짐을 겹친다.
        /// 치명타는 꼬리가 길고 금속성 울림과 잔향이 붙는다.
        ///
        /// <b>중역 "퍽"이 주인공이다.</b> 폰 스피커는 300Hz 아래를 거의 못 낸다. 저음 쿵이 주인공이던
        /// 첫 설계는 300Hz~6kHz 에너지가 2~3%뿐이라 기기에서 "틱"으로 들릴 판이었다(기존 타격음도 100Hz
        /// 사인이라 같은 문제였다). 쿵은 이어폰·PC용 무게로만 남긴다.
        /// </summary>
        private static AudioClip GenerateImpactSFX(bool heavy)
        {
            float seconds = heavy ? 0.42f : 0.2f;
            int total = SecondsToSamples(seconds);
            float[] data = new float[total];
            System.Random rng = new System.Random(heavy ? 13 : 12);
            float phase = 0f, crunchLp = 0f;
            float smackHi = 0f, smackLo = 0f, clickHi = 0f, clickLo = 0f;
            float smackCoefHi = OnePoleCoef(heavy ? 2600f : 3200f), smackCoefLo = OnePoleCoef(heavy ? 500f : 700f);
            float clickCoefHi = OnePoleCoef(7000f), clickCoefLo = OnePoleCoef(2000f);
            for (int i = 0; i < total; i++)
            {
                float t = (float)i / SampleRate;
                float white = Noise(rng);

                // 딸깍 — 2~7kHz 대역 노이즈 6ms
                clickHi += (white - clickHi) * clickCoefHi;
                clickLo += (clickHi - clickLo) * clickCoefLo;
                float click = (clickHi - clickLo) * Mathf.Exp(-t * 500f);

                // 퍽 — 0.5~3kHz 대역 노이즈 덩어리
                smackHi += (white - smackHi) * smackCoefHi;
                smackLo += (smackHi - smackLo) * smackCoefLo;
                float smack = (smackHi - smackLo) * Mathf.Exp(-t * (heavy ? 22f : 38f));

                // 쿵 — 몸통이 눌리는 저음(220→70Hz, 치명타 180→48Hz)
                float f = Mathf.Lerp(heavy ? 180f : 220f, heavy ? 48f : 70f, 1f - Mathf.Exp(-t * 26f));
                phase += f / SampleRate;
                phase -= Mathf.Floor(phase);
                float thump = Mathf.Sin(2f * Mathf.PI * phase) * Mathf.Exp(-t * (heavy ? 9f : 16f));

                // 부서짐 — 어두운 노이즈 짧게
                crunchLp += (white - crunchLp) * 0.28f;
                float crunch = crunchLp * Mathf.Exp(-t * (heavy ? 16f : 30f));

                float s = click * 2.2f + smack * 3.2f + thump * 0.55f + crunch * 0.5f;
                if (heavy)
                {
                    // 금속성 울림 — 비정수배 배음
                    float ring = (Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.5f
                        + Mathf.Sin(2f * Mathf.PI * 1873f * t) * 0.35f
                        + Mathf.Sin(2f * Mathf.PI * 2761f * t) * 0.25f) * Mathf.Exp(-t * 11f);
                    s += ring * 0.45f;
                }
                data[i] = (float)System.Math.Tanh(s * 1.6f);
            }
            if (heavy) ApplySimpleReverb(data, 0.3f, 45);
            Normalize(data, 0.97f);
            return CreateClip(heavy ? "SFX_CriticalHit" : "SFX_Hit", data, false);
        }

        /// <summary>1차 저역통과 계수 — 차단 주파수(Hz)에서.</summary>
        private static float OnePoleCoef(float cutoffHz)
        {
            return 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Min(cutoffHz, SampleRate * 0.45f) / SampleRate);
        }

        /// <summary>1차 저역통과를 <paramref name="poles"/>번 겹쳐 건다(단마다 -6dB/옥타브).</summary>
        private static void LowPass(float[] data, float cutoffHz, int poles)
        {
            float coef = OnePoleCoef(cutoffHz);
            for (int p = 0; p < poles; p++)
            {
                float y = 0f;
                for (int i = 0; i < data.Length; i++)
                {
                    y += (data[i] - y) * coef;
                    data[i] = y;
                }
            }
        }

        private static void Normalize(float[] data, float peak)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            if (max < 0.0001f) return;
            float gain = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
        }
    }
}
