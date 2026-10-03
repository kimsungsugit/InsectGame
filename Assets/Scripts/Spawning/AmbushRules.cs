using System;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Spawning
{
    /// <summary>
    /// 습격을 막은 까닭 — <see cref="AmbushRules.Check"/>가 <b>처음 걸린 하나</b>를 돌려준다(순서가 곧 우선순위다).
    /// 다가오던 곤충은 이 값을 보고 기다릴지(<see cref="AmbushRules.IsPauseOnly"/>) 물러날지 정한다.
    /// </summary>
    public enum AmbushRefusal : byte
    {
        None = 0,
        /// <summary>습격형이 아니거나, 습격형이어도 지금 시간·날씨에는 잠잠하다(<see cref="InsectHabits.IsAmbusher"/>).</summary>
        NotAwake,
        /// <summary>수문장 — 길목에서 기다리는 상대지 덤벼드는 상대가 아니다.</summary>
        Guardian,
        /// <summary>이 개체가 이미 포획·전투·아이 NPC에게 붙잡혀 있다.</summary>
        Engaged,
        /// <summary>「챔피언의 꿈」 도중 — 꿈 밖 기록을 건드리지 않는다(rules/dream-prologue.md).</summary>
        Dream,
        /// <summary>
        /// 서브에리어 안 — 동굴·방은 실내라 시간·날씨를 보지 않고(서브에리어 스폰도 그렇다), 나의 섬(분리 구역)은 집이다 — 밤·비에
        /// 찾아오는 손님 곤충(진짜 야생 개체)도 덤벼들지 않는다. 전투 보정의 실내 판정(<c>BattleEnvironment.IsIndoor</c>)보다 넓게 잡았다.
        /// </summary>
        SubArea,
        /// <summary>플레이어가 다른 교전(선택 창·미니게임·전투·레이드) 중이다.</summary>
        PlayerInEncounter,
        /// <summary>이 몸이 얼마 전에 붙잡혔다 풀려났거나 습격을 접었다.</summary>
        EntityCooldown,
        /// <summary>얼마 전에 습격(또는 다른 교전)이 끝났다 — 연달아 덤벼들지 않는다.</summary>
        GlobalCooldown,
        /// <summary>이 곤충과 싸울 수단이 없다(기절 안 한 출전 곤충 · 레이드면 5마리 팀).</summary>
        NoFighter,
        /// <summary>플레이어가 멈춰 있다(대화·연출) — 다가오던 곤충은 기다린다.</summary>
        PlayerFrozen,
        /// <summary>메뉴·창이 열려 있다 — 다가오던 곤충은 기다린다.</summary>
        ModalOpen
    }

    /// <summary>
    /// 습격 — 깨어 있는 습격형 곤충이 도망치는 대신 플레이어에게 다가와 싸움을 거는 판정의 <b>순수 규칙</b>.
    /// 씬 없이 도는 정적 클래스라 거절 조건·쿨다운 경계를 테스트(<c>AmbushRulesTests</c>)가 전부 고정한다.
    ///
    /// <b>흐름.</b> <c>InsectEntity</c>가 플레이어를 <see cref="NoticeRadius"/> 안에서 알아채면 판정(<c>InsectEntity.AmbushGate</c> —
    /// <c>CaptureInputController</c>가 세운다)을 묻고, 허가되면 <see cref="HesitateSeconds"/> 멈칫한 뒤 <see cref="ApproachSpeed"/>로 다가간다.
    /// <see cref="ReachDistance"/> 안에 닿으면 「습격!」 창(<c>CaptureChoiceUI.ShowAmbush</c>)이 뜨고 [싸우기]/[도망치기]를 고른다.
    /// 도망은 전투의 도주 확률(<c>BattleEscapeRules.Chance</c> — 단일 출처는 battle-dev 쪽)을 그대로 쓰고, 실패하면 싸운다.
    ///
    /// <b>성향은 매 판정 시점의 상태로 본다</b> — 플레이어가 있는 리전에서 보이는 날씨(<c>WorldStateProvider.GetWorldState(regionId)</c>)다.
    /// 스폰 때 굴린 값이 아니므로 밤이 되면 이미 서 있던 사마귀가 그 자리에서 사나워진다.
    /// </summary>
    public static class AmbushRules
    {
        // ── 쿨다운 ──
        //
        // 전역 60초의 근거(표·로스터·밀도로 잰 값 — 기기 실측이 아니다. 기기에서 밤 한 번을 걸어 보고 조정할 것):
        //  · 필드 밀도는 설 수 있는 땅 550㎡당 1칸(FieldSpawnRules). 걸음 8m/s로 알아채는 반경 8m 띠를 훑으면 분당 약 7,700㎡ ≈ 14칸을 지난다.
        //  · 그 칸이 「지금 깨어 있는 습격형」일 몫 — 진짜 성향 표 × 진짜 필드 로스터 × 전역 등급표로 재면 전체 평균이 밤 맑음 4.1% · 밤 비 10.8% ·
        //    밤 안개 10.1% · 밤 센바람 6.8% · 밤 눈 8.1%(밤 평균 약 8%), 아침·낮은 2.5~4.6%다(AmbushRulesTests가 같은 표를 로그로 남긴다).
        //    리전마다는 크게 갈린다 — swamp 밤 15~41%(흔한 지네·거미가 습격형), garden 낮 12~17%·forest 낮 10~14%(말벌·사마귀), meadow·hollow·frostline·canopy는 0%.
        //  · 그러면 밤에 쉬지 않고 걸을 때 습격형과 마주치는 빈도는 평균 분당 약 1회, swamp의 비·안개 밤에는 분당 5회 남짓이다. 쿨다운이 없으면
        //    전투(1~2분)가 끝나자마자 다음 습격이 붙는다 — 「연달아 습격당한다」가 바로 그 모습이고, 늪에서는 쿨다운이 곧 습격 간격이 된다.
        //  · 습격 교전이 끝난 뒤 60초를 두면 습격 사이가 「전투 + 60초」 ≈ 2~3분이 되어, 현실 4.5분짜리 밤(하루 12분의 9/24)에 가장 사나운
        //    늪에서도 1~2번이다. 위협은 되고 사냥을 못 할 만큼 잦지는 않은 자리로 잡았다.

        /// <summary>습격 교전(창 → 전투)이 끝난 뒤 다음 습격까지(초).</summary>
        public const float GlobalCooldownSeconds = 60f;

        /// <summary>
        /// 다른 교전(포획 선택 창·미니게임·전투·레이드)이 끝난 직후 숨 돌릴 틈(초). 결과 창을 닫자마자 옆의 사마귀가 덮치지 않게 한다.
        /// 씬이 막 열렸을 때도 같은 틈을 둔다(로드 직후 바로 덤벼들지 않게).
        /// </summary>
        public const float AfterEncounterGraceSeconds = 8f;

        /// <summary>
        /// 한 몸이 다시 습격할 수 있기까지(초) — 붙잡혔다 풀려났거나(포획 창 취소 등) 습격을 접은 몸. 그 사이에는 온순한 곤충처럼 행동한다
        /// (빠르게 다가오면 달아난다). 몸에 붙은 값이라 풀로 돌아갔다 다시 서면 지워진다 — 그 틈은 전역 쿨다운이 덮는다.
        /// </summary>
        public const float EntityCooldownSeconds = 45f;

        // ── 거리·속도 ──

        /// <summary>
        /// 깨어 있는 습격형이 플레이어를 알아채는 반경(m). 온순한 곤충의 경계 반경(6.5~8.9m, 등급이 높을수록 멀다)과 비슷하게 두어
        /// 고레어가 덜 예민해지지 않게 했다 — 게임 카메라(뒤 6m·위 9m)에 이 거리는 화면 안이다.
        /// </summary>
        public const float NoticeRadius = 8f;

        /// <summary>닿은 것으로 치는 수평 거리(m) — 몸(등급 배율 최대 1.9)과 플레이어 캡슐이 맞닿는 정도.</summary>
        public const float ReachDistance = 1.3f;

        /// <summary>
        /// 다가오는 속도(m/s). 플레이어 기본 걸음(<c>PlayerMovement.moveSpeed</c> 8m/s)의 75%다 — 보고 달아나면 떼어 낼 수 있고
        /// (초당 2m씩 벌어진다), 가만히 있거나 못 보면 닿는다. 더 빠르면 피할 수 없고, 더 느리면 위협이 안 된다(테스트가 걸음보다 느린지 본다).
        /// </summary>
        public const float ApproachSpeed = 6f;

        /// <summary>알아챈 뒤 발을 떼기 전의 멈칫(초) — 고개를 들고 노려보는 틈. 이 사이에 [E]로 먼저 말을 걸거나 돌아서 달아날 수 있다.</summary>
        public const float HesitateSeconds = 0.7f;

        /// <summary>이만큼 멀어지면(수평 m) 쫓기를 접는다 — 알아채는 반경보다 넉넉히 커서 경계에서 들락날락하지 않는다.</summary>
        public const float LeashDistance = 14f;

        /// <summary>쫓는 시간 상한(초, 기다린 시간은 빼고) — 장애물을 빙빙 돌며 끝없이 쫓지 않는다.</summary>
        public const float MaxChaseSeconds = 8f;

        /// <summary>다가오는 길을 다시 재는 간격(초). 재기는 그때만 한다(최대 7방향 스피어캐스트) — 매 프레임 쏘지 않는다.</summary>
        public const float ReplanSeconds = 0.3f;

        /// <summary>한 번 잰 길로 가는 최대 거리(m) — 다음 재기까지(<see cref="ReplanSeconds"/> × 속도)보다 넉넉하다.</summary>
        public const float MaxLegDistance = 3f;

        /// <summary>앞이 막혀 한 걸음도 못 나간 채 이만큼(초) 지나면 쫓기를 접는다.</summary>
        public const float StuckGiveUpSeconds = 0.8f;

        // ── 창 ──

        /// <summary>
        /// 습격 창이 뜬 뒤 입력을 받기까지(초). 창은 플레이어가 고르지 않은 순간에 뜬다 — 걷던 손가락·누르던 키가 그대로
        /// [싸우기]/[도망치기]를 누르지 않게 한다.
        /// </summary>
        public const float InputDelaySeconds = 0.45f;

        /// <summary>도망에 실패한 뒤 「도망치지 못했다!」를 보여 주고 싸움으로 넘어가기까지(초).</summary>
        public const float EscapeFailPauseSeconds = 1.1f;

        /// <summary>
        /// 다가오는 방향 후보 — 플레이어 쪽(0°)에서 좌우로 벌린다. ±90°까지는 벽을 끼고 돌아가는 길이고, 그보다 더 돌면
        /// 물러나는 꼴이라 넣지 않는다(도주의 <see cref="FleePath.CandidateAngles"/>와 같은 형태, 기준만 반대다).
        /// </summary>
        internal static readonly float[] ApproachAngles = { 0f, 30f, -30f, 60f, -60f, 90f, -90f };

        /// <summary>판정에 들어가는 것 전부. 호출부(<c>CaptureInputController</c>)가 프레임마다 한 번 채우고 개체 칸만 바꿔 쓴다.</summary>
        public struct Context
        {
            /// <summary>이 종의 성향(<see cref="InsectHabits.For"/>).</summary>
            public InsectHabit Habit;
            /// <summary>지금 플레이어가 있는 리전에서 보이는 상태(<c>GetWorldState(regionId)</c>).</summary>
            public WorldState State;
            public bool IsGuardian;
            /// <summary>이 개체가 포획·전투·아이 NPC에게 붙잡혀 있다(<c>InsectEntity.IsEngaged</c>).</summary>
            public bool IsEngaged;
            /// <summary>이 몸의 남은 쿨다운(초). 0 이하면 끝났다.</summary>
            public float EntityCooldownLeft;
            /// <summary>마지막 습격 교전이 끝난 뒤 흐른 시간(초). 한 번도 없었으면 아주 큰 값.</summary>
            public float SinceLastAmbushEnded;
            /// <summary>마지막 교전(습격 포함)이 끝난 뒤 흐른 시간(초). 씬이 열린 뒤 처음이면 씬이 열린 뒤 흐른 시간.</summary>
            public float SinceLastEncounterEnded;
            /// <summary>플레이어가 다른 교전(선택 창·미니게임·전투·레이드) 중이다.</summary>
            public bool PlayerInEncounter;
            public bool DreamActive;
            public bool InSubArea;
            public bool PlayerFrozen;
            public bool ModalOpen;
            /// <summary>이 곤충과 싸울 수 있다(<see cref="CanFight"/>).</summary>
            public bool CanFight;
        }

        /// <summary>
        /// 지금 이 곤충이 습격할 수 있는가 — 막혔다면 <b>처음 걸린 까닭</b>을 돌려준다. 순서가 우선순위다:
        /// 습격형이 깨어 있나 → 수문장 → 붙잡힘 → 꿈 → 서브에리어 → 플레이어 교전 중 → 개체 쿨다운 → 전역 쿨다운 → 싸울 곤충 →
        /// 멈춤 → 창. <b>멈춤·창이 맨 뒤인 이유</b>: 다가오던 곤충은 그 둘에서만 기다리고(<see cref="IsPauseOnly"/>) 나머지에서는 물러난다.
        /// 습격 창·전투도 플레이어를 멈추고 모달을 여는데, 그때는 「교전 중」이 먼저 걸려 다른 곤충이 기다렸다 덮치지 않는다.
        /// </summary>
        public static AmbushRefusal Check(in Context c)
        {
            if (!InsectHabits.IsAmbusher(c.Habit, c.State)) return AmbushRefusal.NotAwake;
            if (c.IsGuardian) return AmbushRefusal.Guardian;
            if (c.IsEngaged) return AmbushRefusal.Engaged;
            if (c.DreamActive) return AmbushRefusal.Dream;
            if (c.InSubArea) return AmbushRefusal.SubArea;
            if (c.PlayerInEncounter) return AmbushRefusal.PlayerInEncounter;
            if (c.EntityCooldownLeft > 0f) return AmbushRefusal.EntityCooldown;
            if (c.SinceLastAmbushEnded < GlobalCooldownSeconds) return AmbushRefusal.GlobalCooldown;
            if (c.SinceLastEncounterEnded < AfterEncounterGraceSeconds) return AmbushRefusal.GlobalCooldown;
            if (!c.CanFight) return AmbushRefusal.NoFighter;
            if (c.PlayerFrozen) return AmbushRefusal.PlayerFrozen;
            if (c.ModalOpen) return AmbushRefusal.ModalOpen;
            return AmbushRefusal.None;
        }

        /// <summary><see cref="Check"/>가 막지 않았는가.</summary>
        public static bool CanAmbush(in Context c) => Check(c) == AmbushRefusal.None;

        /// <summary>
        /// 다가오던 곤충이 <b>기다리기만</b> 하는 까닭인가 — 플레이어가 대화·메뉴로 잠시 멈춘 것은 지나가므로 그 자리에서 노려본다.
        /// 나머지(잠잠해짐·쿨다운·다른 교전·서브에리어 등)는 쫓기를 접고 물러난다.
        /// </summary>
        public static bool IsPauseOnly(AmbushRefusal refusal)
            => refusal == AmbushRefusal.PlayerFrozen || refusal == AmbushRefusal.ModalOpen;

        /// <summary>
        /// 이 곤충과 싸울 수 있는가 — 1v1이면 기절 안 한 출전 곤충이 하나라도, 레이드 대상(영웅·전설)이면 5칸이 다 찼고 그중
        /// 하나라도 싸울 수 있어야 한다(<c>CaptureChoiceUI</c>의 레이드 버튼과 같은 조건). <b>싸울 수 없으면 습격하지 않는다</b> —
        /// 습격 창엔 그냥 닫는 길이 없으므로(도망에 실패하면 싸운다) 싸울 수 없는 창은 갇힌 창이 된다. 튜토리얼 초반 첫 파트너가 없을 때가 그 경우다.
        /// </summary>
        public static bool CanFight(bool raidTarget, int filledSlots, int readySlots)
        {
            if (readySlots <= 0) return false;
            return !raidTarget || filledSlots >= BattleTeamManager.MaxSlots;
        }

        /// <summary>쿨다운이 끝날 때까지 남은 시간(초, 0 이상).</summary>
        public static float Remaining(float now, float readyAt) => Mathf.Max(0f, readyAt - now);

        /// <summary>닿았는가(수평 거리).</summary>
        public static bool HasReached(float planarDistance) => planarDistance <= ReachDistance;

        /// <summary>쫓기를 접을 때인가 — 너무 멀어졌거나 너무 오래 쫓았다.</summary>
        public static bool ShouldGiveUp(float planarDistance, float chaseSeconds)
            => planarDistance > LeashDistance || chaseSeconds > MaxChaseSeconds;

        /// <summary>
        /// 다가갈 방향을 고른다 — 플레이어 쪽부터 좌우로 벌려 가며 이번 걸음(<paramref name="distanceToTarget"/>에서 닿는 거리의 절반을 뺀 만큼,
        /// 최대 <see cref="MaxLegDistance"/>)이 장애물 앞 여유(<see cref="FleePath.WallMargin"/>)까지 비어 있는 <b>처음 방향</b>을 쓴다.
        /// 다 막혔으면 가장 멀리 가는 방향으로 장애물 앞까지만 간다(0이면 제자리 — 호출부가 <see cref="StuckGiveUpSeconds"/> 뒤에 접는다).
        /// 도주(<see cref="FleePath.Choose"/>)와 달리 "다 비었다"의 기준이 이번 걸음 길이다 — 플레이어 뒤의 벽까지 비어 있을 필요는 없다.
        /// </summary>
        /// <param name="sideSign">−1이면 좌우를 뒤집는다(한 습격 동안 같은 쪽으로 돈다 — 벽 앞에서 좌우로 흔들리지 않게).</param>
        /// <param name="clearance">방향 → 막히지 않고 갈 수 있는 거리(m).</param>
        internal static Vector3 ChooseApproach(Vector3 toward, float distanceToTarget, float sideSign,
            Func<Vector3, float> clearance, out float allowedDistance)
        {
            toward.y = 0f;
            if (toward.sqrMagnitude < 1e-4f) toward = Vector3.forward;
            toward.Normalize();
            float want = Mathf.Clamp(distanceToTarget - ReachDistance * 0.5f, 0f, MaxLegDistance);
            float sign = sideSign < 0f ? -1f : 1f;

            Vector3 best = toward;
            float bestClear = -1f;
            for (int i = 0; i < ApproachAngles.Length; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(ApproachAngles[i] * sign, Vector3.up) * toward;
                float c = clearance != null ? clearance(dir) : float.PositiveInfinity;
                if (c >= want + FleePath.WallMargin)
                {
                    allowedDistance = want;
                    return dir;
                }
                if (c > bestClear)
                {
                    bestClear = c;
                    best = dir;
                }
            }
            allowedDistance = Mathf.Clamp(bestClear - FleePath.WallMargin, 0f, want);
            return best;
        }

        /// <summary>창에 띄우는 "왜 덤벼들었나" 한 줄 — 시간대가 깨운 것이면 시간대를, 아니면(어스름) 좋아하는 날씨를 말한다.</summary>
        public static string ReasonLine(InsectHabit habit, WorldState state)
        {
            if (InsectHabits.TimeFit(habit, state.DayPhase) > 0)
            {
                switch (state.DayPhase)
                {
                    case DayPhase.Night: return "밤이 되자 사나워졌다";
                    case DayPhase.Morning: return "아침 사냥에 나서 있었다";
                    case DayPhase.Day: return "한낮이라 사냥에 한창이다";
                    default: return "저물녘에 사나워졌다";
                }
            }
            if (InsectHabits.WeatherFit(habit, state.Weather) > 0)
            {
                switch (state.Weather)
                {
                    case WeatherType.Fog: return "안개를 틈타 덮쳐 왔다";
                    case WeatherType.Rain: return "비가 오자 사나워졌다";
                    case WeatherType.Wind: return "센바람을 타고 덤벼들었다";
                    case WeatherType.Snow: return "눈발 속에서 굶주려 있었다";
                    default: return "맑은 날씨에 힘이 넘친다";
                }
            }
            return FallbackReason;
        }

        /// <summary>까닭을 모를 때(상태를 못 읽었을 때)의 한 줄.</summary>
        public const string FallbackReason = "갑자기 덤벼들었다";
    }
}
