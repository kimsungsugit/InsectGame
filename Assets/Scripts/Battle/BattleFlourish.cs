using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>
    /// 전투 체감 연출의 <b>순수 규칙</b> — 전용기 시각표, 승리 포즈·길이, 전투 진입 샷 길이, 대기 숨쉬기 곡선.
    /// 씬 없이 <c>BattleFlourishTests</c>가 고정한다. 그리기는 <c>BattleArenaController.Flourish</c>·<c>.Life</c>,
    /// 카메라 샷은 <see cref="BattleCameraDirector"/>가 이 표를 읽는다.
    ///
    /// <b>ui-dev가 맞출 값</b>(같은 시각에 화면 글자를 그린다):
    /// <list type="bullet">
    /// <item>전용기 기술 이름 컷인 — <see cref="SignatureCutInStart"/>~<see cref="SignatureCutInEnd"/>(스킬 연출 시작부터 연출 시계 초).
    /// 아레나의 <c>IsSignaturePlaying</c>·<c>IsSignatureCutIn</c>·<c>SignatureCutInProgress</c>가 지금 값을 준다.</item>
    /// <item>승리 — <see cref="VictorySeconds"/>(실제 초) 동안 내 곤충이 포즈를 잡고 카메라가 반 바퀴 돈다. 아레나의 <c>IsVictoryPlaying</c>·<c>VictoryFinished</c>.</item>
    /// <item>전투 진입 샷 — <see cref="OpeningShotSeconds"/>(실제 초). 1대1 진입 구간(<see cref="BattleReadPacing.EntryIntroSeconds"/>) 안에서 끝난다.</item>
    /// </list>
    /// </summary>
    public static class BattleFlourish
    {
        // ───────────── 전용기 ─────────────

        /// <summary>전용기 컷인(시전자 클로즈업 + 기술 이름) 시작 — 스킬 연출 첫 프레임(연출 시계 초).</summary>
        public const float SignatureCutInStart = 0f;
        /// <summary>
        /// 전용기 컷인 끝(연출 시계 초). 여기서 카메라가 대상 쪽으로 휘돌아 넘어가고 시전자가 달려든다. 타격이 이보다 이르면
        /// (짧은 연출) 타격 시각의 <see cref="SignatureCutInShare"/>까지로 줄인다(<see cref="SignatureCutInEndFor"/>).
        /// </summary>
        public const float SignatureCutInEnd = 0.5f;
        /// <summary>타격 시각 대비 컷인 비율의 상한 — 컷인 뒤에 달려들 시간이 남게.</summary>
        public const float SignatureCutInShare = 0.62f;
        /// <summary>컷인 → 타격까지의 상한(초) — 아이가 지루하지 않게 1초 안쪽. 스킬 연출(2.5초)의 타격 진행률 0.4 이하가 이걸 지킨다.</summary>
        public const float SignatureLeadMaxSeconds = 1.0f;
        /// <summary>타격 뒤에도 "전용기 연출 중"으로 남는 꼬리(초) — 큰 이펙트가 터지는 동안.</summary>
        public const float SignatureTailSeconds = 0.4f;
        /// <summary>시전자 클로즈업 — 기본 구도에서 시전자 쪽으로 다가가는 비율(컷인 동안 시작 → 끝).</summary>
        public const float SignatureCasterDollyStart = 0.4f;
        public const float SignatureCasterDollyEnd = 0.52f;
        /// <summary>타격 클로즈업 비율(시네마틱 0.32보다 붙는다).</summary>
        public const float SignatureTargetDolly = 0.4f;
        /// <summary>전용기 타격의 큰 이펙트 크기 배율(보통 속성 임팩트 대비).</summary>
        public const float SignatureBurstScale = 1.8f;

        /// <summary>이번 전용기 연출의 컷인 끝(초) — 타격 시각(<paramref name="impactSeconds"/>)을 넘지 않는다.</summary>
        public static float SignatureCutInEndFor(float impactSeconds)
        {
            return Mathf.Max(0.05f, Mathf.Min(SignatureCutInEnd, impactSeconds * SignatureCutInShare));
        }

        // ───────────── 승리 ─────────────

        /// <summary>
        /// 승리 연출 길이(실제 초 — 배속을 타지 않는다. 결과 화면의 <c>ResultShownSeconds</c>와 같은 시계). 1대1은 내 곤충의 포즈 + 카메라 반 바퀴,
        /// 레이드는 팀 전원 점프 + 팀을 담는 카메라. 끝나면 카메라는 그 자리에 머문다(결과 화면 뒤).
        /// </summary>
        public const float VictorySeconds = 2.0f;
        /// <summary>몸 포즈가 끝나 제자리로 돌아오는 시각(초) — <see cref="VictorySeconds"/>보다 앞.</summary>
        public const float VictoryPoseSeconds = 1.6f;
        /// <summary>
        /// 1대1 카메라가 내 곤충 둘레를 도는 각(도) — 얼굴 쪽으로 돈다. 배틀 구도는 내 곤충의 오른쪽 앞(얼굴에서 약 70°)에 있어서,
        /// 120°를 돌면 얼굴을 지나 반대편 3/4 앞(얼굴에서 약 50°)에 선다. 150°면 얼굴에서 80° 넘게 지나 옆모습이 됐다(수치 모의).
        /// </summary>
        public const float VictoryOrbitDegrees = 120f;
        /// <summary>레이드 — 팀 뒤에서 팀 앞(보스가 있던 쪽)으로 도는 각(도).</summary>
        public const float RaidVictoryOrbitDegrees = 140f;
        /// <summary>레이드 팀원이 차례로 뛰어오르는 간격(초).</summary>
        public const float RaidVictoryStagger = 0.08f;
        /// <summary>1대1 끝 구도 — 처음 거리의 이 비율까지 다가간다.</summary>
        public const float VictoryCloseRatio = 0.5f;
        public const float VictoryMinDistance = 2.2f;

        /// <summary>
        /// 승리 포즈(시각 초) — 계열마다 다르다. 0 이하·<see cref="VictoryPoseSeconds"/> 이상은 제자리.
        /// 딱정벌레: 두 번 깡충(정점에서 뿔을 쳐든다) · 사마귀: 앞다리를 쳐들고 서서 깡충 · 나는 종: 솟아올라 한 바퀴 돌며 떠 있다 내려온다 ·
        /// 벌: 솟아올라 윙윙 좌우로 몸을 흔든다 · 지네·거미: 몸을 세우고 깡충 · 그 밖: 크게 뛰며 한 바퀴.
        /// </summary>
        public static BattleMotion.Pose VictoryPose(BattleMotion.Family family, float seconds)
        {
            float s = seconds;
            if (s <= 0f || s >= VictoryPoseSeconds) return new BattleMotion.Pose(0f, 0f, 0f, 0f, 0f);
            switch (family)
            {
                case BattleMotion.Family.Beetle:
                {
                    float h1 = Hop(s, 0.12f, 0.58f, 0.38f);
                    float h2 = Hop(s, 0.68f, 1.12f, 0.3f);
                    float toss = BattleMotion.Bump(s, 0.18f, 0.55f) + BattleMotion.Bump(s, 0.74f, 1.1f);
                    float settle = BattleMotion.Bump(s, 1.12f, 1.5f);
                    return new BattleMotion.Pose(0f, h1 + h2, -16f * toss + 5f * settle, 0f, 0f);
                }
                case BattleMotion.Family.Mantis:
                {
                    float rear = Plateau(s, 0.08f, 0.4f, 1.15f, 1.5f);
                    float hop = Hop(s, 0.45f, 0.82f, 0.24f);
                    float sway = 6f * rear * Mathf.Sin(s * 9f);
                    return new BattleMotion.Pose(0f, hop + 0.08f * rear, -28f * rear, sway, 10f * BattleMotion.Bump(s, 0.85f, 1.3f));
                }
                case BattleMotion.Family.Flier:
                {
                    float up = Plateau(s, 0.08f, 0.5f, 1.1f, 1.55f);
                    float bob = 0.06f * up * Mathf.Sin(s * 8f);
                    float spin = 360f * Mathf.SmoothStep(0f, 1f, (s - 0.2f) / 0.9f);
                    return new BattleMotion.Pose(0f, 0.7f * up + bob, -12f * up, 10f * up * Mathf.Sin(s * 6f), spin);
                }
                case BattleMotion.Family.Bee:
                {
                    float up = Plateau(s, 0.08f, 0.36f, 1.15f, 1.55f);
                    float zip = up * Mathf.Sin(s * 22f);
                    return new BattleMotion.Pose(0f, 0.45f * up + 0.03f * Mathf.Sin(s * 40f) * up, -8f * up, 18f * zip, 25f * zip);
                }
                case BattleMotion.Family.Crawler:
                {
                    float rear = Plateau(s, 0.08f, 0.4f, 1.0f, 1.45f);
                    float hop = Hop(s, 0.48f, 0.9f, 0.3f);
                    return new BattleMotion.Pose(0f, 0.12f * rear + hop, -24f * rear, 0f, 0f);
                }
                default:
                {
                    float big = Hop(s, 0.14f, 0.76f, 0.5f);
                    float small = Hop(s, 0.86f, 1.2f, 0.18f);
                    float spin = 360f * Mathf.SmoothStep(0f, 1f, (s - 0.14f) / 0.62f);
                    return new BattleMotion.Pose(0f, big + small, -10f * BattleMotion.Bump(s, 0.14f, 0.76f), 0f, spin);
                }
            }
        }

        /// <summary>a~b 동안 꼭짓점 <paramref name="height"/>의 포물선 깡충.</summary>
        private static float Hop(float s, float a, float b, float height)
        {
            if (s <= a || s >= b) return 0f;
            float u = (s - a) / (b - a);
            return height * 4f * u * (1f - u);
        }

        /// <summary>a→b 동안 0→1로 오르고 c까지 머물다 d까지 0으로 내린다.</summary>
        private static float Plateau(float s, float a, float b, float c, float d)
        {
            if (s <= a || s >= d) return 0f;
            if (s < b) return Mathf.SmoothStep(0f, 1f, (s - a) / (b - a));
            if (s < c) return 1f;
            return 1f - Mathf.SmoothStep(0f, 1f, (s - c) / (d - c));
        }

        /// <summary>
        /// 무리 전체를 화면에 담는 카메라 거리 — 가로 반폭·세로 반높이(m)를 시야각·화면비에서 잰다(여유 <paramref name="margin"/>배).
        /// 레이드 승리 샷의 끝 거리가 쓴다(세로 화면은 가로 시야가 좁아 더 물러난다).
        /// </summary>
        public static float FitDistance(float halfWidth, float halfHeight, float verticalFov, float aspect, float margin = 1.15f)
        {
            float vHalf = Mathf.Max(1f, verticalFov) * 0.5f * Mathf.Deg2Rad;
            float hHalf = Mathf.Atan(Mathf.Tan(vHalf) * Mathf.Max(0.2f, aspect));
            float byWidth = Mathf.Max(0.1f, halfWidth) / Mathf.Tan(hHalf);
            float byHeight = Mathf.Max(0.1f, halfHeight) / Mathf.Tan(vHalf);
            return Mathf.Max(byWidth, byHeight) * Mathf.Max(1f, margin);
        }

        // ───────────── 전투 진입 샷 ─────────────

        /// <summary>
        /// 전투 진입 샷 길이(실제 초 — 배속을 타지 않는다). 카메라가 상대 바로 옆에서 크게 휘돌며 물러나 배틀 구도에 내려앉는다.
        /// 시계는 1대1 진입 구간과 같다(<c>BattleScreenUI.EntryIntroProgress</c> × <see cref="BattleReadPacing.EntryIntroSeconds"/> = 이 샷의 경과 초).
        /// 요구가 "1초 안쪽"이라 구간(실제 1.4초)의 앞 1초에 끝내고, 남은 0.4초는 ui-dev의 「야생 ○○이(가) 나타났다!」가 내려앉은 구도 위에서 읽힌다
        /// (<c>BattleFlourishTests</c>가 구간 안인지 본다). 레이드(수문장 아님)도 같은 길이 — 레이드 인트로도 실제 1.4초 하한이다.
        /// </summary>
        public const float OpeningShotSeconds = 1.0f;
        /// <summary>출발 각 — 배틀 구도에서 상대 쪽으로 이만큼 돌아간 자리에서 시작한다(도).</summary>
        public const float OpeningSwingDegrees = 75f;
        /// <summary>출발 거리 — 기본 구도 거리의 이 비율(상대에게 바짝).</summary>
        public const float OpeningStartDistance = 0.42f;
        /// <summary>출발 높이 — 기본 구도 높이의 이 비율.</summary>
        public const float OpeningStartHeight = 0.55f;

        // ───────────── 대기 숨쉬기 ─────────────

        /// <summary>숨 한 번의 길이(초). 보스는 <see cref="BossBreathPeriod"/>.</summary>
        public const float BreathPeriod = 1.9f;
        public const float BossBreathPeriod = 2.5f;
        /// <summary>몸통이 부푸는 몫(가로·앞뒤). 세로는 그 60%.</summary>
        public const float BreathAmplitude = 0.035f;

        /// <summary>숨 -1..1 — <paramref name="phase"/>(0..1)로 곤충마다 박자를 어긋낸다.</summary>
        public static float Breath(float seconds, float period, float phase)
        {
            return Mathf.Sin((seconds / Mathf.Max(0.1f, period) + phase) * Mathf.PI * 2f);
        }

        /// <summary>
        /// 더듬이 까딱임(도, 양수 = 앞으로 숙임) — 느린 흔들림(±4°)에 2.6초마다 한 번 빠르게 까딱(14°). 좌우는 <paramref name="phase"/>로 어긋낸다.
        /// </summary>
        public static float AntennaNod(float seconds, float phase)
        {
            float sway = 4f * Mathf.Sin((seconds / 1.7f + phase) * Mathf.PI * 2f);
            float c = Mathf.Repeat(seconds / 2.6f + phase * 1.3f, 1f);
            float flick = c < 0.14f ? Mathf.Sin(c / 0.14f * Mathf.PI) : 0f;
            return sway + 14f * flick;
        }
    }
}
