using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>
    /// 이야기 전투의 등장·변신 연출 — <b>순수 규칙</b>(시각표·곡선·그림자 색). 씬 없이 <c>BattleStagingTests</c>가 고정한다.
    ///
    /// 셋이다.
    /// <list type="number">
    /// <item>상대 교체 등장(1대1 팀 대결) — 빛살이 착지점을 알리고, 새 곤충이 바깥 뒤쪽에서 뛰어들어 먼지를 일으키며 내려앉는다.
    /// 길이는 화면 단계 <c>BattleScreenUI.EnemySwitchSeconds</c>(1.2초)를 받아 쓴다 — 여기 시각은 그 길이의 진행률(0~1)이다.</item>
    /// <item>그림자 변신(레이드) — 검은 연기가 보스를 감싸고, 한가운데에서 모습이 바뀌고, 연기가 걷히며 빌린 모습이 드러난다.
    /// 길이는 <c>RaidBattleUI.BossTransformDuration</c>(1.5초)의 진행률이다.</item>
    /// <item>수문장 등장(레이드 인트로) — 낮은 곳에서 수문장을 올려다보며 다가가고, 포효에 흔들리고, 원래 구도로 돌아온다.
    /// 이건 <b>초</b>로 잰다 — 인트로 길이(<see cref="GuardianIntroSeconds"/>)를 이 표가 정하고 UI가 읽는다.</item>
    /// </list>
    /// 그리기는 <c>BattleArenaController.Staging</c>, 카메라 샷은 <see cref="BattleCameraDirector"/>가 이 표를 읽는다.
    /// </summary>
    public static class BattleStaging
    {
        // ───────────── ① 상대 교체 등장 (진행률 0..1) ─────────────

        /// <summary>착지점에 선 빛살이 사라지는 진행률 — 새 곤충이 "어디로" 오는지 먼저 알린다.</summary>
        public const float EntranceBeamEnd = 0.34f;
        /// <summary>도약 시작. 그 전엔 몸을 보이지 않는다(출발점이 가로 화면 가장자리에 걸릴 수 있다).</summary>
        public const float EntranceLeapStart = 0.12f;
        /// <summary>착지 — 먼지·땅 고리·빛살 퍼짐·흔들림·울음이 이 순간이다(1.2초면 0.62초).</summary>
        public const float EntranceLand = 0.52f;
        /// <summary>착지 반동(눌렸다 펴짐)이 가라앉는 진행률.</summary>
        public const float EntranceSettleEnd = 0.82f;
        /// <summary>도약 꼭짓점 높이(m). 출발·착지 높이를 잇는 선 위로 이만큼 솟는다.</summary>
        public const float EntranceApex = 1.9f;
        /// <summary>출발점 — 상대 자리에서 바깥(내 곤충 반대쪽)으로 이만큼(m).</summary>
        public const float EntranceLaunchOut = 3.0f;
        /// <summary>출발점 — 상대 자리에서 뒤(카메라 반대쪽)로 이만큼(m).</summary>
        public const float EntranceLaunchBack = 1.6f;

        /// <summary>카메라가 앞 구도에서 착지점 클로즈업으로 옮겨 가는 끝(진행률).</summary>
        public const float EntranceCamIn = 0.34f;
        /// <summary>착지 클로즈업을 붙드는 끝 — 여기서부터 새 구도로 풀린다.</summary>
        public const float EntranceCamHoldEnd = 0.72f;
        /// <summary>카메라가 새 구도로 완전히 돌아오는 진행률. <b>1보다 작아야</b> 「당신의 턴」이 원래 구도에서 뜬다.</summary>
        public const float EntranceCamReleaseEnd = 0.94f;
        /// <summary>착지 클로즈업 — 기본 구도에서 착지점 쪽으로 다가가는 비율(시네마틱 타격 클로즈업 0.32보다 조금 멀다).</summary>
        public const float EntranceDolly = 0.26f;

        /// <summary>
        /// 도약 궤적 — <paramref name="launch"/>에서 <paramref name="rest"/>까지 포물선. <paramref name="u"/>는 도약 구간 진행률(0~1).
        /// 수평은 끝으로 갈수록 살짝 감속하고(착지에 무게가 실리게) 수직은 꼭짓점 <paramref name="apex"/>의 포물선이다 — 끝점에서 정확히 <paramref name="rest"/>.
        /// </summary>
        public static Vector3 LeapPosition(Vector3 launch, Vector3 rest, float apex, float u)
        {
            u = Mathf.Clamp01(u);
            float h = Mathf.Lerp(u, 1f - (1f - u) * (1f - u), 0.35f);
            Vector3 p = Vector3.Lerp(launch, rest, h);
            p.y = Mathf.Lerp(launch.y, rest.y, u) + apex * 4f * u * (1f - u);
            return p;
        }

        /// <summary>도약 중 몸 기울기(도, 양수 = 머리 숙임) — 오를 땐 머리를 들고 내려올 땐 숙인다.</summary>
        public static float LeapPitch(float u)
        {
            return Mathf.Lerp(-20f, 12f, Mathf.Clamp01(u));
        }

        /// <summary>
        /// 착지 반동의 세로 배율 — <paramref name="k"/>는 착지 뒤 진행률(0~1). 0.78로 눌렸다가 1.1 가까이 튀고 1로 잦아든다
        /// (감쇠 진동, 끝점에서 정확히 1). 가로는 부피를 지키게 반대로 움직인다(<see cref="SquashWidth"/>).
        /// </summary>
        public static float LandingSquash(float k)
        {
            k = Mathf.Clamp01(k);
            return 1f - 0.22f * (1f - k) * (1f - k) * Mathf.Cos(3f * Mathf.PI * k);
        }

        /// <summary>세로 배율 <paramref name="squash"/>에 짝지은 가로 배율 — 눌리면 퍼지고 늘면 좁아진다.</summary>
        public static float SquashWidth(float squash)
        {
            return 1f - (squash - 1f) * 0.5f;
        }

        // ───────────── ② 그림자 변신 (진행률 0..1) ─────────────

        /// <summary>연기가 보스를 완전히 덮는 진행률.</summary>
        public const float TransformEngulfEnd = 0.44f;
        /// <summary>모델이 새 모습으로 바뀌는 순간 — 연기가 가장 짙은 한가운데(1.5초면 0.75초).</summary>
        public const float TransformSwap = 0.5f;
        /// <summary>새 모습이 연기를 뚫고 울부짖는 순간(울음·흔들림·눈빛).</summary>
        public const float TransformRoar = 0.6f;
        /// <summary>연기가 다 걷히는 진행률.</summary>
        public const float TransformRevealEnd = 0.86f;
        /// <summary>몸을 덮는 먹빛의 최대 세기(0~1) — 바꾸는 순간 실루엣만 남는다.</summary>
        public const float TransformVeilPeak = 0.9f;
        /// <summary>연기에 삼켜지며 움츠러드는 최소 배율.</summary>
        public const float TransformShrink = 0.86f;
        /// <summary>새 모습이 펴지며 넘치는 최대 배율.</summary>
        public const float TransformOvershoot = 1.07f;

        /// <summary>카메라가 보스 쪽으로 다가붙는 끝.</summary>
        public const float TransformCamIn = 0.2f;
        /// <summary>카메라를 붙드는 끝 — 여기서부터 원래 구도로 풀린다.</summary>
        public const float TransformCamHoldEnd = 0.72f;
        /// <summary>카메라가 원래 구도로 완전히 돌아오는 진행률(1보다 작게 — 다음 차례가 원래 구도에서 열린다).</summary>
        public const float TransformCamReleaseEnd = 0.94f;
        /// <summary>보스 쪽으로 다가붙는 비율(레이드 기본 구도는 팀 뒤 원경이라 조금만).</summary>
        public const float TransformDolly = 0.16f;

        /// <summary>연기 덮임 0~1 — 덮이고(~<see cref="TransformEngulfEnd"/>), 바꾸는 순간을 지나 잠깐 머물고, 걷힌다.</summary>
        public static float SmokeCover(float p)
        {
            p = Mathf.Clamp01(p);
            if (p < TransformEngulfEnd) return Mathf.SmoothStep(0f, 1f, p / TransformEngulfEnd);
            float holdEnd = TransformSwap + 0.06f;
            if (p < holdEnd) return 1f;
            return 1f - Mathf.SmoothStep(0f, 1f, (p - holdEnd) / (TransformRevealEnd - holdEnd));
        }

        /// <summary>몸을 덮는 먹빛 0~<see cref="TransformVeilPeak"/> — 바꾸는 순간에 가장 짙고 양 끝에서 0.</summary>
        public static float TransformVeil(float p)
        {
            p = Mathf.Clamp01(p);
            if (p < TransformSwap)
                return TransformVeilPeak * Mathf.SmoothStep(0f, 1f, (p - 0.08f) / (TransformSwap - 0.08f));
            return TransformVeilPeak * (1f - Mathf.SmoothStep(0f, 1f, (p - TransformSwap - 0.04f) / (TransformRevealEnd - TransformSwap - 0.04f)));
        }

        /// <summary>
        /// 몸 배율 — 삼켜지며 <see cref="TransformShrink"/>까지 움츠러들고, 바뀐 뒤 그 크기에서 <see cref="TransformOvershoot"/>로 펴졌다가
        /// 1로 돌아온다. 바꾸는 순간 앞뒤가 같은 값이라 모델이 갈아끼워져도 크기가 튀지 않는다.
        /// </summary>
        public static float TransformScale(float p)
        {
            p = Mathf.Clamp01(p);
            if (p < TransformSwap)
                return Mathf.Lerp(1f, TransformShrink, Mathf.SmoothStep(0f, 1f, (p - 0.12f) / (TransformSwap - 0.12f)));
            float peak = TransformRoar + 0.08f;
            if (p < peak)
                return Mathf.Lerp(TransformShrink, TransformOvershoot, Mathf.SmoothStep(0f, 1f, (p - TransformSwap) / (peak - TransformSwap)));
            return Mathf.Lerp(TransformOvershoot, 1f, Mathf.SmoothStep(0f, 1f, (p - peak) / (TransformRevealEnd - peak)));
        }

        // ───────────── ③ 수문장 등장 (초) ─────────────

        /// <summary>
        /// 수문장 레이드의 인트로 길이(연출 시계 초). 보통 레이드는 2초다(<c>RaidBattleUI</c>) — 수문장만 0.6초 길다.
        /// 등장 컷이 이 안에서 원래 구도로 돌아와야 첫 차례가 원래 구도에서 열린다(<see cref="GuardianReleaseEnd"/>).
        /// </summary>
        public const float GuardianIntroSeconds = 2.6f;
        /// <summary>올려다보며 다가가기가 끝나는 시각 — 그 뒤엔 천천히 조금 더 붙는다(<see cref="GuardianCreep"/>).</summary>
        public const float GuardianApproachEnd = 1.3f;
        /// <summary>포효 — 몸을 젖히며 솟고, 울고, 화면이 흔들리고, 받침대에서 먼지가 인다. ui-dev 배너의 "쾅"도 이 박자에 맞춘다.</summary>
        public const float GuardianRoarAt = 0.62f;
        /// <summary>원래 구도로 풀리기 시작하는 시각.</summary>
        public const float GuardianReleaseStart = 1.85f;
        /// <summary>원래 구도로 완전히 돌아오는 시각 — <see cref="GuardianIntroSeconds"/>보다 앞이어야 한다.</summary>
        public const float GuardianReleaseEnd = 2.5f;
        /// <summary>
        /// 올려다보는 각도(도)의 시작값. 수문장은 2m 넘는 받침대 위라, 화면 위쪽 창에 두면서 이 각을 지키려면 카메라가 땅속까지
        /// 내려가야 할 때가 많다 — 그러면 <see cref="GuardianPitchStep"/>씩 낮춘다(<see cref="GuardianEndShot"/>). 각이 낮아져도
        /// 카메라가 받침대 윗면보다 낮게 서서 수문장을 올려다보는 구도는 남는다.
        /// </summary>
        public const float GuardianLookUpPitch = 8f;
        /// <summary>각을 낮추는 한 걸음(도).</summary>
        public const float GuardianPitchStep = 2f;
        /// <summary>가장 낮춘 각(도, 음수 = 살짝 내려다봄). 넓은 날개의 세로 화면이 여기까지 온다.</summary>
        public const float GuardianMinPitch = -4f;
        /// <summary>카메라가 팀 줄(팀 호 중심) 뒤로 떨어지는 거리(m) — 다가간 뒤 천천히 더 붙는 몫(<see cref="GuardianCreep"/>)까지 넉넉히.</summary>
        public const float GuardianTeamClearance = 1.4f;
        /// <summary>출발점 — 끝 구도의 거리에서 이 비율만큼 더 물러난 데서 다가온다.</summary>
        public const float GuardianApproachTravel = 0.55f;
        /// <summary>다가간 뒤 천천히 더 붙는 거리(m).</summary>
        public const float GuardianCreep = 0.18f;
        /// <summary>포효 반동 세기(m) — 위로 튀었다 감쇠한다.</summary>
        public const float GuardianRoarKick = 0.1f;
        /// <summary>
        /// 수문장을 담는 화면 창(안전 영역 세로 비율). <b>아래 1/3은 ui-dev의 등장 배너 자리</b>라 비우고, 맨 위는 보스 HP 바 몫을 남긴다.
        /// 가로 1280×720·세로 720×1280 모두 이 창 안에 들어오는지 <c>BattleStagingTests</c>가 잰다.
        /// </summary>
        public const float GuardianWindowBottom = 0.38f;
        public const float GuardianWindowTop = 0.93f;
        /// <summary>카메라가 벽에서 떨어져 있어야 하는 거리(m) — 벽 밖에 서면 벽의 바깥 면이 화면을 막는다.</summary>
        public const float GuardianWallMargin = 1.2f;
        /// <summary>
        /// 카메라의 최저 높이(바닥에서, m) — 팀 곤충 머리 위. 더 낮으면 앞에 선 팀원이 수문장의 발과 받침대를 가린다
        /// (팀 머리는 화면 0.3~0.53에 걸리고 수문장은 0.48 위에 선다 — 2026-10-04 수치 모의).
        /// </summary>
        public const float GuardianMinCameraHeight = 1.2f;

        /// <summary>포효 자세 0~1 — 솟았다(0.22초) 버티고(0.28초) 내려온다(0.35초).</summary>
        public static float RoarPose(float seconds)
        {
            float s = seconds - GuardianRoarAt;
            if (s <= 0f) return 0f;
            if (s < 0.22f) return Mathf.SmoothStep(0f, 1f, s / 0.22f);
            if (s < 0.5f) return 1f;
            return 1f - Mathf.SmoothStep(0f, 1f, (s - 0.5f) / 0.35f);
        }

        /// <summary>
        /// 수문장 샷의 끝 자리 — 수문장 경계(<paramref name="fit"/>)가 화면 창(<paramref name="window"/>, 뷰포트 좌표)을 채우는 카메라.
        /// 올려다보는 각(<see cref="GuardianLookUpPitch"/>)에서 시작해, 카메라가 바닥에서 <see cref="GuardianMinCameraHeight"/> 위에 설 때까지
        /// 각을 낮춘다. 카메라는 팀 줄(<paramref name="teamCenter"/>) 뒤 <see cref="GuardianTeamClearance"/>에 선다 — 시선 축을 따라
        /// 물러나면 수문장은 화면 가운데 쪽으로 줄어들 뿐이라 창 아래(배너 자리)로 내려가지 않는다.
        /// </summary>
        public static void GuardianEndShot(Bounds fit, float aspect, float fieldOfView, float yaw, Rect window,
            Vector3 teamCenter, float floorY, out Vector3 position, out Vector3 target, out Quaternion rotation)
        {
            float pitch = GuardianLookUpPitch;
            while (true)
            {
                rotation = Quaternion.Euler(-pitch, yaw, 0f);
                BattleFraming.ComputeInWindow(fit, aspect, fieldOfView, rotation, window, out position, out target);
                Vector3 forward = rotation * Vector3.forward;
                Vector3 flat = new Vector3(forward.x, 0f, forward.z);
                flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward;
                float ahead = Vector3.Dot(position - teamCenter, flat) + GuardianTeamClearance;
                if (ahead > 0f)
                {
                    float distance = Vector3.Distance(position, target) + ahead / Mathf.Max(0.2f, Vector3.Dot(forward, flat));
                    position = target - forward * distance;
                }
                if (position.y >= floorY + GuardianMinCameraHeight || pitch <= GuardianMinPitch) break;
                pitch = Mathf.Max(GuardianMinPitch, pitch - GuardianPitchStep);
            }
            position.y = Mathf.Max(position.y, floorY + GuardianMinCameraHeight);
        }

        /// <summary>
        /// 다가가기 출발점 — 끝 구도(<paramref name="endPos"/>)에서 수평으로 물러난 자리. 벽 안쪽(<paramref name="wallSpan"/> −
        /// <see cref="GuardianWallMargin"/>)을 넘지 않게 물러나는 거리를 줄이고, 바닥에서 <see cref="GuardianMinCameraHeight"/> 위에 둔다.
        /// </summary>
        public static Vector3 GuardianApproachStart(Vector3 endPos, Quaternion rotation, float endDistance,
            Vector3 arenaCenter, float wallSpan, float floorY)
        {
            Vector3 flat = rotation * Vector3.forward;
            flat.y = 0f;
            flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward;
            float limit = Mathf.Max(0.5f, wallSpan - GuardianWallMargin);
            float back = Mathf.Max(0f, endDistance) * GuardianApproachTravel;
            Vector3 start = endPos - flat * back;
            for (int i = 0; i < 8 && !Inside(start, arenaCenter, limit); i++)
            {
                back *= 0.6f;
                start = endPos - flat * back;
            }
            if (!Inside(start, arenaCenter, limit)) start = endPos;
            start.y = Mathf.Max(start.y, floorY + GuardianMinCameraHeight);
            return start;
        }

        private static bool Inside(Vector3 p, Vector3 center, float limit)
        {
            return Mathf.Abs(p.x - center.x) <= limit && Mathf.Abs(p.z - center.z) <= limit;
        }

        // ───────────── 그림자 색 ─────────────
        // 모습을 빌리는 보스(지금은 이름 없는 사마귀 하나 — RaidBossForms)는 「그림자」다. 빌린 모습이 진짜 호랑나비·반딧불이와
        // 헷갈리지 않게, 그리고 원래 모습도 같은 존재로 읽히게 검보라 톤을 입힌다. 무늬(밝고 어두운 차)는 남긴다 —
        // 「지워진 개체」(InsectEntity.Erase)처럼 "무엇이었는지는 알겠는" 실루엣이 요점이다.

        /// <summary>그림자 톤의 가장 어두운 끝(원래 어두운 부위가 간다).</summary>
        public static readonly Color ShadowDeep = new Color(0.06f, 0.03f, 0.10f);
        /// <summary>그림자 톤의 밝은 끝(원래 밝은 부위가 간다) — 검보라.</summary>
        public static readonly Color ShadowLilac = new Color(0.32f, 0.21f, 0.48f);
        /// <summary>그림자 빛 — 눈·반딧불이 발광기관·테두리. 차가운 보랏빛.</summary>
        public static readonly Color ShadowGlow = new Color(0.74f, 0.50f, 1.00f);
        /// <summary>변신 순간 몸을 덮는 먹빛.</summary>
        public static readonly Color ShadowInk = new Color(0.035f, 0.02f, 0.06f);
        /// <summary>원래 모습(사마귀) — 같은 존재로 읽히되 사마귀였다는 건 보이게 조금 덜 덮는다.</summary>
        public const float ShadowOriginalStrength = 0.7f;
        /// <summary>빌린 모습 — 진짜 그 곤충과 확실히 갈리게 더 덮는다.</summary>
        public const float ShadowBorrowedStrength = 0.88f;

        /// <summary>
        /// 이 레이드 보스가 「그림자」인가 — 모습을 빌리는 보스(<see cref="RaidBossForms.HasForms"/>). 표에 다른 보스가 들어와
        /// 그림자가 아니게 되면 여기서 가른다(지금 표의 유일한 항목이 이름 없는 사마귀다).
        /// </summary>
        public static bool IsShadowBoss(string bossInsectId)
        {
            return RaidBossForms.HasForms(bossInsectId);
        }

        /// <summary>
        /// 그림자 톤 — 밝기를 검보라 띠(<see cref="ShadowDeep"/>~<see cref="ShadowLilac"/>)에 옮기고 원색과 <paramref name="strength"/>만큼 섞는다.
        /// 밝기 순서가 지켜져 무늬가 남는다. 알파는 그대로(날개 막은 막대로).
        /// </summary>
        public static Color ShadowTint(Color color, float strength)
        {
            float lum = Mathf.Clamp01(color.r * 0.299f + color.g * 0.587f + color.b * 0.114f);
            Color shade = Color.Lerp(ShadowDeep, ShadowLilac, lum);
            Color mixed = Color.Lerp(color, shade, Mathf.Clamp01(strength));
            mixed.a = color.a;
            return mixed;
        }

        /// <summary>발광 부위(반딧불이 발광기관 등)의 그림자 빛 — 어둡게 덮지 않고 보랏빛으로 바꾼다.</summary>
        public static Color ShadowGlowTint(Color color)
        {
            Color mixed = Color.Lerp(color, ShadowGlow, 0.85f);
            mixed.a = color.a;
            return mixed;
        }
    }
}
