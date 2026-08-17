using UnityEngine;

namespace InsectGame.Story
{
    /// <summary>조우 접근이 이번 판단에서 NPC에게 시킬 일.</summary>
    public enum ApproachAction
    {
        /// <summary>아무것도 하지 않는다.</summary>
        None,
        /// <summary>플레이어 쪽으로 걸어오게 한다.</summary>
        Walk,
        /// <summary>이미 대화 거리다 — 쳐다보고 손을 흔든다(1회).</summary>
        Greet,
        /// <summary>플레이어가 멀어졌다 — 앵커로 돌려보낸다.</summary>
        Return,
    }

    /// <summary>
    /// "지금 기다리는 목표가 ○○에게 말 걸기인데 플레이어가 근처에 왔다 →
    /// 그 NPC가 스스로 걸어와 인사한다"의 <b>순수</b> 판정부.
    ///
    /// MonoBehaviour와 떼어 놓아 PlayMode 테스트로 경계를 고정한다
    /// (<see cref="StoryObjectiveResolver"/>·<see cref="CutsceneTimeline"/>과 같은 성격).
    ///
    /// <b>반경 두 개가 히스테리시스를 만든다.</b> 시작(12m)과 해제(16m)를 같은 값으로 두면
    /// 경계에 선 플레이어 앞에서 NPC가 왔다 갔다 떨게 된다.
    /// </summary>
    public static class StoryNpcApproach
    {
        /// <summary>이 거리 안으로 들어오면 NPC가 걸어오기 시작한다.</summary>
        public const float TriggerRadius = 12f;
        /// <summary>이 거리를 넘어가면 접근을 접고 앵커로 돌아간다. 반드시 시작 반경보다 크다.</summary>
        public const float ReleaseRadius = 16f;

        /// <summary>
        /// 도착 판정 반경 — 대화 사거리보다 <b>안쪽</b>에서 멈춰야 확실히 말이 걸린다.
        /// 너무 붙으면 플레이어와 겹쳐 보이므로 0.8m 아래로는 내리지 않는다.
        /// </summary>
        public static float ArriveRadiusFor(float talkRadius)
        {
            return Mathf.Max(0.8f, talkRadius * 0.7f);
        }

        /// <summary>
        /// 이번 틱에 시킬 일 하나.
        /// </summary>
        /// <param name="distanceToPlayer">NPC와 플레이어의 수평 거리(m).</param>
        /// <param name="arriveRadius"><see cref="ArriveRadiusFor"/>가 준 값.</param>
        /// <param name="moving">이미 접근 명령을 받아 걸어오는 중인가.</param>
        /// <param name="greeted">이번 조우에서 이미 인사했는가(같은 손짓 반복 방지).</param>
        /// <param name="engaged">
        /// 이번 조우에서 <b>이미 다가오기 시작했는가</b>. 히스테리시스를 <b>시작에만</b> 걸기 위한 것이다.
        /// 없으면 12~16m가 죽은 구간이 된다 — NPC가 옛 목표 지점에 도착해 멈췄는데 그때 플레이어가
        /// 그 밴드 안에 있으면, 시작 반경(12m) 밖이라 다시 출발하지 못하고 <b>영영 멈춰 선다</b>
        /// (실측으로 12.54m에서 그대로 굳었다).
        /// </param>
        public static ApproachAction Decide(float distanceToPlayer, float arriveRadius,
            bool moving, bool greeted, bool engaged = false)
        {
            // 멀어졌으면 걷는 중이든 아니든 제자리로. 접근하다 만 채 들판에 서 있게 두지 않는다.
            if (distanceToPlayer > ReleaseRadius) return ApproachAction.Return;

            // 이미 대화 거리 — 걸어올 필요 없이 인사만(플레이어가 직접 찾아온 경우가 여기다).
            if (distanceToPlayer <= arriveRadius)
                return greeted ? ApproachAction.None : ApproachAction.Greet;

            // 걸어오는 중이면 도착까지 그대로 둔다.
            if (moving) return ApproachAction.None;

            // 한 번 인사했으면 더 쫓아가지 않는다. 이게 없으면 말을 걸 때까지 NPC가 플레이어를
            // 따라 12m 반경을 계속 따라다녀, 마을 어르신이 제 자리를 떠나 들판을 헤맨다.
            // (해제 반경 밖으로 나갔다 오면 호출부가 greeted를 지우므로 다시 다가온다.)
            if (greeted) return ApproachAction.None;

            // 시작은 12m 안에서만. 다만 **이미 출발한 조우라면** 해제 반경까지는 계속 따라간다 —
            // 그래야 옛 목표 지점에 도착해 멈춘 뒤에도 새 위치로 다시 걸어간다.
            return (engaged || distanceToPlayer <= TriggerRadius)
                ? ApproachAction.Walk
                : ApproachAction.None;
        }
    }
}
