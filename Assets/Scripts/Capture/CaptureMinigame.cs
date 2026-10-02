using UnityEngine;

namespace InsectGame.Capture
{
    /// <summary>포획 미니게임 종류. 포획마다 하나가 무작위로 걸린다(<see cref="CaptureMinigame.PickNext"/>).</summary>
    public enum CaptureMinigameKind
    {
        /// <summary>살금살금 — 누르고 있으면 다가가고, 곤충이 돌아보면 손을 뗀다.</summary>
        Sneak,
        /// <summary>가두기 — 날아다니는 곤충을 그물 원 안에 붙잡아 게이지를 채운다.</summary>
        Track,
        /// <summary>던지기 — 그물이 날아가는 시간이 있어 곤충이 갈 곳에 던진다.</summary>
        Toss,
    }

    /// <summary>
    /// 한 판의 조건 — 곤충 등급과 채집망 보정. 채집망의 세 배율은 예전 타이밍 바 시절 그대로 온다
    /// (<c>CaptureItemData</c>): 속도는 "곤충이 얼마나 재빠른가", 구간은 "판정이 얼마나 넉넉한가",
    /// 시간은 제한 시간이다. 세 게임이 같은 뜻으로 읽어야 은빛·황금 채집망이 어느 게임에서도 값을 한다.
    /// </summary>
    public readonly struct CaptureMinigameTuning
    {
        public readonly int Rarity;
        public readonly float SpeedMult;
        public readonly float ZoneMult;
        public readonly float TimeMult;

        public CaptureMinigameTuning(int rarity, float speedMult, float zoneMult, float timeMult)
        {
            Rarity = Mathf.Clamp(rarity, 0, 4);
            // 0 이하 배율은 "설정 안 함"으로 읽는다 — 나눗셈·제한 시간이 0이 되어 판이 시작하자마자 끝난다.
            SpeedMult = speedMult > 0.01f ? speedMult : 1f;
            ZoneMult = zoneMult > 0.01f ? zoneMult : 1f;
            TimeMult = timeMult > 0.01f ? timeMult : 1f;
        }
    }

    /// <summary>한 프레임의 조작. <see cref="Point"/>는 놀이판 좌표(<see cref="CaptureMinigame.BoardWidth"/> 기준).</summary>
    public struct CaptureMinigameInput
    {
        public bool Down;
        public bool Pressed;
        public Vector2 Point;
    }

    /// <summary>
    /// 포획 미니게임 한 판의 <b>순수</b> 규칙. 그리기·입력 수집·포획 판정은 <c>CaptureMinigameController</c>가 하고,
    /// 여기는 "무엇이 맞았는가"만 정한다 — 화면(IMGUI)은 테스트로 못 보므로 규칙을 떼어 고정한다.
    ///
    /// 결과는 셋 다 <see cref="Hits"/> 0~3이다. 그 값이 <c>CaptureMinigameProbability</c>로 들어가므로
    /// 게임을 바꿔도 포획 공식은 그대로다.
    /// </summary>
    public abstract class CaptureMinigame
    {
        /// <summary>놀이판의 논리 크기. 화면에서는 이 비율로 늘여 그린다.</summary>
        public const float BoardWidth = 360f;
        public const float BoardHeight = 280f;
        public const int MaxHits = 3;

        private readonly System.Random rng;

        protected CaptureMinigame(System.Random random, float timeLimit)
        {
            rng = random ?? new System.Random();
            TimeLimit = Mathf.Max(1f, timeLimit);
            TimeLeft = TimeLimit;
        }

        public abstract CaptureMinigameKind Kind { get; }
        public int Hits { get; protected set; }
        public bool Done { get; protected set; }
        public float TimeLimit { get; }
        public float TimeLeft { get; protected set; }
        public float TimeRatio => Mathf.Clamp01(TimeLeft / TimeLimit);

        public abstract void Tick(float dt, CaptureMinigameInput input);

        protected float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
        protected bool Chance(float probability) => rng.NextDouble() < probability;

        /// <summary>제한 시간을 깎는다. 다 됐으면 true — 그 시점까지의 <see cref="Hits"/>로 끝난다.</summary>
        protected bool RunOutOfTime(float dt)
        {
            TimeLeft -= dt;
            if (TimeLeft > 0f) return false;
            TimeLeft = 0f;
            Done = true;
            return true;
        }

        public static CaptureMinigame Create(CaptureMinigameKind kind, CaptureMinigameTuning tuning, System.Random random)
        {
            switch (kind)
            {
                case CaptureMinigameKind.Track: return new TrackMinigame(tuning, random);
                case CaptureMinigameKind.Toss: return new TossMinigame(tuning, random);
                default: return new SneakMinigame(tuning, random);
            }
        }

        /// <summary>
        /// 다음 판의 게임. 무작위지만 <b>직전과 같은 게임은 연달아 주지 않는다</b> — 균등 추첨이면
        /// 세 번에 한 번은 같은 게임이 반복돼 "랜덤인데 또 이거"가 된다.
        /// </summary>
        public static CaptureMinigameKind PickNext(CaptureMinigameKind? last, System.Random random)
        {
            const int count = 3;
            if (random == null) random = new System.Random();
            if (!last.HasValue) return (CaptureMinigameKind)random.Next(count);
            return (CaptureMinigameKind)(((int)last.Value + 1 + random.Next(count - 1)) % count);
        }
    }

    /// <summary>
    /// 살금살금. 누르고 있으면 다가간다. 곤충은 더듬이를 떨어 예고한 뒤 돌아보고, 그때 움직이면 들킨다.
    /// 세 구간을 <b>들키지 않고</b> 지날 때마다 1점이다. 두 번 들키면 달아난다.
    /// </summary>
    public sealed class SneakMinigame : CaptureMinigame
    {
        public enum Watch { Calm, Twitch, Look }

        public const int MaxSpots = 2;
        public const float FirstFlag = 2f / 3f;
        public const float SecondFlag = 1f / 3f;
        private const float SpotFreezeSeconds = 0.7f;
        private const float SpotPushBack = 0.07f;
        private const float SwoopSeconds = 0.45f;
        private const float FleeSeconds = 0.7f;

        private readonly float creepPerSecond;
        private readonly float telegraphSeconds;
        private readonly float calmScale;
        private readonly float lookBonus;

        private bool clean = true;
        // 판이 열릴 때 이미 누르고 있었으면(채집망을 고른 그 손가락) 한 번 떼야 움직인다.
        private bool needRelease = true;
        private float nervous = 1f;
        private float stateLeft;
        private float swoopTime = -1f;
        private float fleeTime;

        public SneakMinigame(CaptureMinigameTuning tuning, System.Random random)
            // 등급이 오르면 걸음이 느려지고 돌아보는 시간이 길어진다 — 그만큼 제한 시간도 늘려야
            // 한 번도 안 들키고도 시간이 모자라 끝나는 판이 안 생긴다.
            : base(random, (14f + tuning.Rarity * 1.5f) * tuning.TimeMult)
        {
            creepPerSecond = 0.30f - tuning.Rarity * 0.03f;
            telegraphSeconds = Mathf.Max(0.28f, 0.55f - tuning.Rarity * 0.05f) * tuning.ZoneMult;
            calmScale = (1f - tuning.Rarity * 0.07f) / tuning.SpeedMult;
            lookBonus = tuning.Rarity * 0.06f;
            State = Watch.Calm;
            stateLeft = Range(1.2f, 2.2f) * calmScale;
        }

        public override CaptureMinigameKind Kind => CaptureMinigameKind.Sneak;

        /// <summary>곤충까지 남은 거리. 1에서 출발해 0이면 덮친다.</summary>
        public float Distance { get; private set; } = 1f;
        public Watch State { get; private set; }
        public int Spots { get; private set; }
        public bool Fled { get; private set; }
        /// <summary>방금 들켜 굳어 있는 시간(초). 0보다 크면 조작을 받지 않는다.</summary>
        public float FreezeLeft { get; private set; }
        /// <summary>덮치는 연출 진행(0~1). 덮치기 전이면 음수.</summary>
        public float Swoop01 => swoopTime < 0f ? -1f : Mathf.Clamp01(swoopTime / SwoopSeconds);
        public float Flee01 => Mathf.Clamp01(fleeTime / FleeSeconds);

        public override void Tick(float dt, CaptureMinigameInput input)
        {
            if (Done || dt <= 0f) return;

            if (swoopTime >= 0f)
            {
                swoopTime += dt;
                if (swoopTime >= SwoopSeconds) Done = true;
                return;
            }
            if (Fled)
            {
                fleeTime += dt;
                if (fleeTime >= FleeSeconds) Done = true;
                return;
            }
            if (RunOutOfTime(dt)) return;

            stateLeft -= dt;
            if (stateLeft <= 0f) AdvanceWatch();

            if (FreezeLeft > 0f) { FreezeLeft -= dt; return; }
            if (!input.Down) { needRelease = false; return; }
            if (needRelease) return;

            if (State == Watch.Look) { GetSpotted(); return; }

            float before = Distance;
            Distance -= creepPerSecond * dt;
            CrossFlag(before, FirstFlag);
            CrossFlag(before, SecondFlag);
            if (Distance <= 0f)
            {
                Distance = 0f;
                if (clean) Hits = Mathf.Min(MaxHits, Hits + 1);
                swoopTime = 0f;
            }
        }

        private void CrossFlag(float before, float flag)
        {
            if (before <= flag || Distance > flag) return;
            if (clean) Hits = Mathf.Min(MaxHits, Hits + 1);
            clean = true;
        }

        private void AdvanceWatch()
        {
            switch (State)
            {
                case Watch.Calm:
                    State = Watch.Twitch;
                    stateLeft = telegraphSeconds;
                    break;
                case Watch.Twitch:
                    State = Watch.Look;
                    stateLeft = Range(0.7f, 1.2f) + lookBonus;
                    break;
                default:
                    State = Watch.Calm;
                    stateLeft = Range(1.3f, 2.6f) * nervous * calmScale;
                    break;
            }
        }

        private void GetSpotted()
        {
            Spots++;
            clean = false;
            nervous *= 0.75f;
            FreezeLeft = SpotFreezeSeconds;
            needRelease = true;
            // 밀려나되 **그 구간 안에서만**. 깃발을 되넘으면 다시 지날 때 구간이 새로 깨끗해져 벌점이 사라진다.
            float top = Distance > FirstFlag ? 1f : (Distance > SecondFlag ? FirstFlag : SecondFlag);
            Distance = Mathf.Min(top - 0.005f, Distance + SpotPushBack);
            if (Spots >= MaxSpots) Fled = true;
        }
    }

    /// <summary>
    /// 가두기. 곤충이 놀이판을 날아다니고, 누른 채 끄는 그물 원 안에 곤충이 있는 동안 게이지가 찬다.
    /// 게이지의 3분의 1마다 1점이고, 한 번 얻은 점수는 놓쳐도 깎이지 않는다.
    /// </summary>
    public sealed class TrackMinigame : CaptureMinigame
    {
        private const float Margin = 34f;
        private const float DrainPerSecond = 0.12f;
        private const float NetFollow = 14f;

        private readonly float speed;
        private readonly float fillSeconds;
        private readonly float retargetScale;
        private readonly float dartChance;

        private Vector2 target;
        private float retargetLeft;
        private float dartLeft;

        public TrackMinigame(CaptureMinigameTuning tuning, System.Random random)
            : base(random, 9f * tuning.TimeMult)
        {
            speed = (105f + tuning.Rarity * 26f) * tuning.SpeedMult;
            NetRadius = (48f - tuning.Rarity * 4f) * tuning.ZoneMult;
            fillSeconds = 1.8f + tuning.Rarity * 0.25f;
            retargetScale = 1f - tuning.Rarity * 0.08f;
            dartChance = 0.3f + tuning.Rarity * 0.08f;
            Insect = new Vector2(BoardWidth * 0.5f, BoardHeight * 0.4f);
            Net = new Vector2(BoardWidth * 0.5f, BoardHeight * 0.8f);
            target = Insect;
        }

        public override CaptureMinigameKind Kind => CaptureMinigameKind.Track;

        public Vector2 Insect { get; private set; }
        public Vector2 Net { get; private set; }
        public float NetRadius { get; }
        public float Gauge { get; private set; }
        public bool Inside { get; private set; }

        public override void Tick(float dt, CaptureMinigameInput input)
        {
            if (Done || dt <= 0f) return;
            if (RunOutOfTime(dt)) return;

            retargetLeft -= dt;
            if (retargetLeft <= 0f)
            {
                retargetLeft = Range(0.45f, 1.1f) * retargetScale;
                target = new Vector2(Range(Margin, BoardWidth - Margin), Range(Margin, BoardHeight - Margin));
                if (Chance(dartChance)) dartLeft = 0.25f;
            }
            dartLeft -= dt;

            Vector2 delta = target - Insect;
            float distance = delta.magnitude;
            if (distance > 4f)
            {
                float step = speed * (dartLeft > 0f ? 2.3f : 1f) * dt;
                Insect += delta / distance * Mathf.Min(step, distance);
            }

            if (input.Down)
            {
                Vector2 goal = new Vector2(
                    Mathf.Clamp(input.Point.x, 0f, BoardWidth), Mathf.Clamp(input.Point.y, 0f, BoardHeight));
                Net = Vector2.Lerp(Net, goal, Mathf.Min(1f, dt * NetFollow));
            }

            // 누르고 있을 때만 센다 — 놓아둔 그물로 곤충이 제 발로 들어와 별이 생기면 안 된다.
            Inside = input.Down && (Insect - Net).sqrMagnitude <= NetRadius * NetRadius;
            Gauge = Mathf.Clamp01(Gauge + (Inside ? dt / fillSeconds : -dt * DrainPerSecond));
            Hits = Mathf.Max(Hits, HitsForGauge(Gauge));
            if (Gauge >= 1f) Done = true;
        }

        public static int HitsForGauge(float gauge)
        {
            if (gauge >= 1f) return 3;
            if (gauge >= 2f / 3f) return 2;
            return gauge >= 1f / 3f ? 1 : 0;
        }
    }

    /// <summary>
    /// 던지기. 누른 자리로 그물이 날아가 <see cref="FlightSeconds"/> 뒤에 떨어진다. 그 순간 곤충이 그물 원 안이면
    /// 1점. 그물은 세 개다. 곤충이 지금 있는 곳이 아니라 <b>갈 곳</b>에 던져야 맞는다.
    /// </summary>
    public sealed class TossMinigame : CaptureMinigame
    {
        public const int Throws = 3;
        public const float FlightSeconds = 0.45f;
        private const float StunSeconds = 0.5f;
        private const float EndSeconds = 0.6f;
        private const float LandShowSeconds = 0.3f;

        private float pathTime;
        private float pathSpeed;
        private float direction = 1f;
        private float flipLeft;
        private float flightTime;
        private float endTime = -1f;

        public TossMinigame(CaptureMinigameTuning tuning, System.Random random)
            : base(random, 10f * tuning.TimeMult)
        {
            pathSpeed = (1.1f + tuning.Rarity * 0.22f) * tuning.SpeedMult;
            NetRadius = (40f - tuning.Rarity * 3f) * tuning.ZoneMult;
            pathTime = Range(0f, 6f);
            flipLeft = Range(1f, 2f);
            ThrowsLeft = Throws;
            Insect = PathPoint(pathTime);
        }

        public override CaptureMinigameKind Kind => CaptureMinigameKind.Toss;

        public Vector2 Insect { get; private set; }
        public float NetRadius { get; }
        public int ThrowsLeft { get; private set; }
        public bool InFlight { get; private set; }
        public Vector2 Target { get; private set; }
        public float Flight01 => InFlight ? Mathf.Clamp01(flightTime / FlightSeconds) : 0f;
        public float StunLeft { get; private set; }
        /// <summary>방금 떨어진 그물을 보여 줄 남은 시간(초). 0이면 안 그린다.</summary>
        public float LandShowLeft { get; private set; }
        public Vector2 LastLanding { get; private set; }
        public bool LastHit { get; private set; }

        /// <summary>그물이 떠나는 자리(놀이판 아래 가운데).</summary>
        public static Vector2 Launcher => new Vector2(BoardWidth * 0.5f, BoardHeight - 18f);

        public static Vector2 PathPoint(float t)
        {
            return new Vector2(
                BoardWidth * 0.5f + 125f * Mathf.Sin(t * 1.3f),
                BoardHeight * 0.42f + 62f * Mathf.Sin(t * 2.1f + 0.7f));
        }

        public override void Tick(float dt, CaptureMinigameInput input)
        {
            if (Done || dt <= 0f) return;

            if (LandShowLeft > 0f) LandShowLeft -= dt;

            if (endTime >= 0f)
            {
                endTime += dt;
                if (endTime >= EndSeconds) Done = true;
                return;
            }
            if (RunOutOfTime(dt)) return;

            if (StunLeft > 0f)
            {
                StunLeft -= dt;
            }
            else
            {
                flipLeft -= dt;
                if (flipLeft <= 0f) { flipLeft = Range(0.9f, 2.2f); direction = -direction; }
                pathTime += dt * pathSpeed * direction;
                Insect = PathPoint(pathTime);
            }

            if (InFlight)
            {
                flightTime += dt;
                if (flightTime >= FlightSeconds) Land();
            }
            else if (input.Pressed && ThrowsLeft > 0)
            {
                ThrowsLeft--;
                InFlight = true;
                flightTime = 0f;
                Target = new Vector2(
                    Mathf.Clamp(input.Point.x, 0f, BoardWidth), Mathf.Clamp(input.Point.y, 0f, BoardHeight));
            }
        }

        private void Land()
        {
            InFlight = false;
            LastLanding = Target;
            LandShowLeft = LandShowSeconds;
            LastHit = (Insect - Target).sqrMagnitude <= NetRadius * NetRadius;
            if (LastHit)
            {
                Hits = Mathf.Min(MaxHits, Hits + 1);
                StunLeft = StunSeconds;
                pathSpeed *= 1.22f;   // 한 번 놀란 곤충은 더 빨리 난다
            }
            if (ThrowsLeft <= 0) endTime = 0f;
        }
    }
}
