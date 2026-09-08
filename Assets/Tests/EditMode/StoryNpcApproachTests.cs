#if UNITY_EDITOR
using InsectGame.Story;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 조우 접근(목표 NPC가 스스로 다가와 인사하는 규칙)의 경계.
    /// 실제 걷기·충돌 회피는 기기 확인 대상이고, 여기서 고정하는 건 두 가지다 —
    /// <b>히스테리시스</b>(경계에서 NPC가 떨지 않는가)와 <b>인사 1회</b>(계속 손을 흔들지 않는가).
    /// </summary>
    [TestFixture]
    public class StoryNpcApproachTests
    {
        // 대화 사거리 3m(WorldInteractionController.VillagerTalkRadius)에서 파생되는 값.
        private static float Arrive => StoryNpcApproach.ArriveRadiusFor(3f);

        [Test]
        public void Radii_ReleaseIsWiderThanTrigger()
        {
            // 같으면 경계에 선 플레이어 앞에서 접근/복귀가 매 틱 뒤집혀 NPC가 떤다.
            Assert.Greater(StoryNpcApproach.ReleaseRadius, StoryNpcApproach.TriggerRadius);
        }

        [Test]
        public void ArriveRadius_IsInsideTalkRange()
        {
            // 대화 사거리보다 안쪽에서 멈춰야 "도착했는데 말이 안 걸린다"가 안 생긴다.
            Assert.Less(Arrive, 3f);
            Assert.Less(Arrive, StoryNpcApproach.TriggerRadius);
        }

        [Test]
        public void ArriveRadius_HasFloor_SoNpcDoesNotOverlapPlayer()
        {
            // 대화 사거리를 아주 좁게 잡아도 플레이어와 겹칠 만큼 붙지는 않는다.
            Assert.GreaterOrEqual(StoryNpcApproach.ArriveRadiusFor(0.1f), 0.8f);
        }

        [Test]
        public void Decide_FarAway_ReturnsHome()
        {
            // 걷는 중이든 아니든 마찬가지 — 접근하다 만 채 들판에 두지 않는다.
            Assert.AreEqual(ApproachAction.Return,
                StoryNpcApproach.Decide(StoryNpcApproach.ReleaseRadius + 1f, Arrive, false, false));
            Assert.AreEqual(ApproachAction.Return,
                StoryNpcApproach.Decide(StoryNpcApproach.ReleaseRadius + 1f, Arrive, true, true));
        }

        [Test]
        public void Decide_InsideTrigger_StartsWalking()
        {
            Assert.AreEqual(ApproachAction.Walk,
                StoryNpcApproach.Decide(StoryNpcApproach.TriggerRadius - 0.1f, Arrive, false, false));
        }

        [Test]
        public void Decide_AtTriggerBoundary_StartsWalking()
        {
            // 경계값 포함 — "12m 안"의 안이 어디까지인지 고정한다.
            Assert.AreEqual(ApproachAction.Walk,
                StoryNpcApproach.Decide(StoryNpcApproach.TriggerRadius, Arrive, false, false));
        }

        [Test]
        public void Decide_InHysteresisBand_DoesNothing()
        {
            // 12~16m 사이는 시작도 복귀도 하지 않는 완충 구간이다.
            float band = (StoryNpcApproach.TriggerRadius + StoryNpcApproach.ReleaseRadius) * 0.5f;
            Assert.AreEqual(ApproachAction.None,
                StoryNpcApproach.Decide(band, Arrive, false, false));
        }

        [Test]
        public void Decide_WhileWalking_DoesNotReissue()
        {
            // 매 틱 다시 명령하면 이동 타임아웃이 영영 갱신돼 절대 만료되지 않는다.
            Assert.AreEqual(ApproachAction.None,
                StoryNpcApproach.Decide(StoryNpcApproach.TriggerRadius - 5f, Arrive, true, false));
        }

        [Test]
        public void Decide_WithinArriveRange_GreetsOnce()
        {
            Assert.AreEqual(ApproachAction.Greet,
                StoryNpcApproach.Decide(Arrive - 0.1f, Arrive, false, false));
            Assert.AreEqual(ApproachAction.None,
                StoryNpcApproach.Decide(Arrive - 0.1f, Arrive, false, true));
        }

        [Test]
        public void Decide_PlayerWalkedUpThemselves_StillGreets()
        {
            // 플레이어가 직접 찾아온 경우 — 걸어올 필요 없이 시선만 맞추고 인사한다.
            Assert.AreEqual(ApproachAction.Greet,
                StoryNpcApproach.Decide(0f, Arrive, false, false));
        }

        [Test]
        public void Decide_ArrivedWhileWalking_Greets()
        {
            // 도착 직후 아직 Scripted 정리가 안 됐어도 인사로 넘어간다.
            Assert.AreEqual(ApproachAction.Greet,
                StoryNpcApproach.Decide(Arrive - 0.05f, Arrive, true, false));
        }

        [Test]
        public void Decide_AfterGreeting_DoesNotChasePlayer()
        {
            // 인사하고 나서도 쫓아가면 마을 어르신이 제 자리를 떠나 들판을 헤맨다.
            Assert.AreEqual(ApproachAction.None,
                StoryNpcApproach.Decide(StoryNpcApproach.TriggerRadius - 1f, Arrive, false, true));
        }

        [Test]
        public void Decide_InHysteresisBand_ButAlreadyEngaged_KeepsWalking()
        {
            // **실제로 갇혔던 자리다.** NPC가 옛 목표 지점에 도착해 멈췄는데 그때 플레이어가
            // 12~16m 밴드 안이면, 시작 반경 밖이라 다시 출발하지 못하고 12.54m에서 영영 굳었다.
            // 히스테리시스는 조우를 **시작**할 때만 걸려야 한다.
            float band = (StoryNpcApproach.TriggerRadius + StoryNpcApproach.ReleaseRadius) * 0.5f;
            Assert.AreEqual(ApproachAction.None,
                StoryNpcApproach.Decide(band, Arrive, false, false, engaged: false));
            Assert.AreEqual(ApproachAction.Walk,
                StoryNpcApproach.Decide(band, Arrive, false, false, engaged: true));
        }

        [Test]
        public void Decide_Engaged_StillReleasesBeyondOuterRadius()
        {
            // 시작했다고 무한정 따라가지는 않는다 — 해제 반경은 여전히 유효하다.
            Assert.AreEqual(ApproachAction.Return,
                StoryNpcApproach.Decide(StoryNpcApproach.ReleaseRadius + 1f, Arrive, false, false, engaged: true));
        }

        [Test]
        public void Decide_Engaged_DoesNotOverrideGreetedOrArrival()
        {
            // engaged가 다른 규칙을 덮어쓰면 안 된다.
            Assert.AreEqual(ApproachAction.None,
                StoryNpcApproach.Decide(8f, Arrive, false, greeted: true, engaged: true));
            Assert.AreEqual(ApproachAction.Greet,
                StoryNpcApproach.Decide(Arrive - 0.1f, Arrive, false, greeted: false, engaged: true));
            Assert.AreEqual(ApproachAction.None,
                StoryNpcApproach.Decide(8f, Arrive, moving: true, greeted: false, engaged: true));
        }

        [Test]
        public void Decide_GreetedThenPlayerLeaves_ReturnsAndCanGreetAgain()
        {
            // 멀어지면 복귀. 호출부가 greeted를 리셋하므로 다시 다가오면 또 인사한다.
            Assert.AreEqual(ApproachAction.Return,
                StoryNpcApproach.Decide(StoryNpcApproach.ReleaseRadius + 5f, Arrive, false, true));
            Assert.AreEqual(ApproachAction.Walk,
                StoryNpcApproach.Decide(StoryNpcApproach.TriggerRadius - 1f, Arrive, false, false));
        }
    }
}
#endif
