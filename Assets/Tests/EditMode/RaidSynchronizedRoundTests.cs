#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 레이드 라운드 파이프라인 — <b>팀 5마리가 하나씩 차례로</b> 행동하고, 전원이 끝나야
    /// 보스가 한 번 반격한다. 예전엔 리더 한 마리의 스킬 선택으로 팀 전원이 같은 순간에
    /// 행동했다(팀 러시). 아래 테스트가 그 순차성과 라운드 경계를 고정한다.
    /// </summary>
    [TestFixture]
    public class RaidSynchronizedRoundTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                    Object.DestroyImmediate(createdObjects[i]);
            }
            createdObjects.Clear();
        }

        // ── 순차 팀 턴 ──

        [Test]
        public void SequentialTurn_OneCommand_MovesTurnToNextSlotOnly()
        {
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 8, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 5000, 30, 80),
                CreateTeam(5, 300, 50, 60),
                new[] { skill });

            Assert.AreEqual(0, raid.ActiveSlot, "라운드는 첫 생존 슬롯에서 시작한다");

            RaidRoundResult round = raid.ResolveTeamCommand(0);

            Assert.AreEqual(1, round.TeamActions.Count, "한 번의 커맨드는 한 마리만 행동시킨다");
            Assert.AreEqual(0, round.TeamActions[0].SourceSlot);
            Assert.AreEqual(1, raid.ActiveSlot, "차례가 다음 슬롯으로 넘어가야 한다");
            Assert.IsTrue(raid.CanSubmitTeamCommand, "아직 팀 턴이다");
            Assert.IsFalse(raid.IsAwaitingBossResponse, "전원이 끝나기 전엔 보스 차례가 아니다");
        }

        [Test]
        public void SequentialTurn_AllFiveAct_ThenBossResponds()
        {
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 8, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 5000, 30, 80),
                CreateTeam(5, 300, 50, 60),
                new[] { skill });

            RaidRoundResult round = ResolveWholeTeamTurn(raid, 0);

            Assert.AreEqual(5, round.TeamActions.Count);
            CollectionAssert.AreEqual(
                new[] { 0, 1, 2, 3, 4 },
                round.TeamActions.Select(action => action.SourceSlot).ToArray(),
                "행동 순서는 슬롯 순서 그대로여야 한다(결정론)");
            Assert.AreEqual(-1, raid.ActiveSlot, "전원이 끝나면 차례가 비어야 한다");
            Assert.IsFalse(raid.CanSubmitTeamCommand);
            Assert.IsTrue(raid.IsAwaitingBossResponse);
        }

        [Test]
        public void SequentialTurn_MemberEventFiresOncePerMember()
        {
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 8, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 5000, 30, 80),
                CreateTeam(5, 300, 50, 60),
                new[] { skill });
            List<int> actedSlots = new List<int>();
            int teamPhaseClosed = 0;
            raid.RaidMemberActionResolved += action => actedSlots.Add(action.SourceSlot);
            raid.RaidTeamRushResolved += _ => teamPhaseClosed++;

            ResolveWholeTeamTurn(raid, 0);

            // UI는 이 이벤트 하나에 곤충 한 마리의 연출을 건다 — 수가 어긋나면 연출이 겹치거나 빠진다.
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, actedSlots);
            Assert.AreEqual(1, teamPhaseClosed, "팀 턴 종료 이벤트는 라운드당 한 번");
        }

        [Test]
        public void SequentialTurn_DeadMembers_AreSkipped()
        {
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 8, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 5000, 30, 80),
                CreateTeam(5, 300, 50, 60),
                new[] { skill });
            raid.TeamStats[2].ApplyDamage(999999);
            raid.TeamStats[4].ApplyDamage(999999);

            RaidRoundResult round = ResolveWholeTeamTurn(raid, 0);

            CollectionAssert.AreEqual(
                new[] { 0, 1, 3 },
                round.TeamActions.Select(action => action.SourceSlot).ToArray());
            Assert.AreEqual(3, raid.RoundActorCount);
        }

        [Test]
        public void SequentialTurn_SameSlotCannotActTwiceInOneRound()
        {
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 8, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 5000, 30, 80),
                CreateTeam(5, 300, 50, 60),
                new[] { skill });

            raid.ResolveTeamCommand(0);
            raid.SelectSlot(0);   // 이미 행동한 슬롯으로 되돌리려는 시도

            Assert.AreEqual(1, raid.ActiveSlot, "행동을 마친 슬롯으로는 차례가 돌아가지 않는다");
            Assert.IsTrue(raid.HasActedThisRound(0));
            Assert.IsFalse(raid.HasActedThisRound(1));
        }

        [Test]
        public void SequentialTurn_NewRound_ClearsActedMarks()
        {
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 8, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 500000, 30, 80),
                CreateTeam(5, 3000, 50, 60),
                new[] { skill });

            ResolveWholeTeamTurn(raid, 0);
            raid.ResolveBossResponse();
            Assert.IsTrue(raid.CompleteRoundPresentation());

            Assert.AreEqual(0, raid.ActedThisRound, "새 라운드는 행동 기록이 비어 있어야 한다");
            Assert.AreEqual(0, raid.ActiveSlot);
            Assert.IsTrue(raid.CanSubmitTeamCommand);
            for (int slot = 0; slot < raid.TeamStats.Length; slot++)
                Assert.IsFalse(raid.HasActedThisRound(slot), $"slot {slot}");
        }

        [Test]
        public void SequentialTurn_KnockoutMidRound_EndsTeamPhaseImmediately()
        {
            InsectSkill skill = CreateSkill("finisher", SkillEffectType.Damage, 9999, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 10, 10, 5),
                CreateTeam(5, 500, 100, 80),
                new[] { skill });

            RaidRoundResult round = raid.ResolveTeamCommand(0);

            Assert.AreEqual(RaidRoundEndState.Victory, round.EndState);
            Assert.AreEqual(1, round.TeamActions.Count,
                "보스가 쓰러지면 남은 팀원은 헛되이 때리지 않는다");
            Assert.IsFalse(raid.CanSubmitTeamCommand);
            Assert.IsNull(raid.ResolveBossResponse());
            Assert.IsTrue(raid.IsAwaitingPresentationCompletion);
        }

        // ── 자동 위임 ──

        [Test]
        public void ResolveAutoCommand_ConsumesExactlyOneSlot()
        {
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 8, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 5000, 30, 80),
                CreateTeam(5, 300, 50, 60),
                new[] { skill });

            RaidRoundResult round = raid.ResolveAutoCommand();

            Assert.AreEqual(1, round.TeamActions.Count);
            Assert.AreEqual(RaidActionKind.SupportSkill, round.TeamActions[0].Kind,
                "위임한 행동은 서포트 위력으로 계산된다(직접 고를 때보다 약하다)");
            Assert.AreEqual(1, raid.ActiveSlot);
        }

        [Test]
        public void ResolveAutoRemaining_ClosesTeamPhaseInOneCall()
        {
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 8, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 500000, 30, 80),
                CreateTeam(5, 3000, 50, 60),
                new[] { skill });

            raid.ResolveTeamCommand(0);           // 첫 마리만 직접
            RaidRoundResult round = raid.ResolveAutoRemaining();

            Assert.AreEqual(5, round.TeamActions.Count);
            Assert.AreEqual(RaidActionKind.LeaderSkill, round.TeamActions[0].Kind);
            Assert.AreEqual(4, round.TeamActions.Count(a => a.Kind == RaidActionKind.SupportSkill));
            Assert.IsTrue(raid.IsAwaitingBossResponse);
        }

        [Test]
        public void ResolveAutoCommand_WithoutSkills_FallsBackToAssist()
        {
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 40, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 1000000, 40, 100),
                CreateTeam(5, 3000, 60, 80),
                new[] { skill },
                supportsHaveSkills: false);

            raid.ResolveTeamCommand(0);
            RaidRoundResult round = raid.ResolveAutoRemaining();

            Assert.AreEqual(
                4,
                round.TeamActions.Count(action => action.Kind == RaidActionKind.SupportAssist),
                "쓸 스킬이 없으면 차례를 비우지 않고 기본 지원 공격으로 폴백해야 한다");
        }

        [Test]
        public void DirectCommand_HitsHarderThanDelegatedOne()
        {
            // 직접 조작이 자동 위임보다 유리해야 위임이 "조작 절약"으로 남고 늘 옳은 답이 되지 않는다.
            int direct = MeasureFirstActionDamage(autoDelegate: false);
            int delegated = MeasureFirstActionDamage(autoDelegate: true);

            Assert.Greater(direct, delegated);
        }

        // ── 보스 반격 ──

        [Test]
        public void ResolveBossResponse_CalledTwice_DealsDamageOnlyOnce()
        {
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 4, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 5000, 80, 80),
                CreateTeam(5, 500, 40, 70),
                new[] { skill });
            ResolveWholeTeamTurn(raid, 0);

            int hpBefore = raid.TeamStats.Sum(stats => stats.CurrentHp);
            RaidRoundResult first = raid.ResolveBossResponse();
            int hpAfterFirst = raid.TeamStats.Sum(stats => stats.CurrentHp);
            RaidRoundResult second = raid.ResolveBossResponse();

            Assert.IsNotNull(first);
            Assert.IsNull(second);
            Assert.Less(hpAfterFirst, hpBefore);
            Assert.AreEqual(hpAfterFirst, raid.TeamStats.Sum(stats => stats.CurrentHp));
            Assert.IsTrue(first.BossResponseResolved);
        }

        [Test]
        public void BossIntent_IsNeverAreaAttack_WhileDisabled()
        {
            // 사용자 요청으로 보스의 전체공격을 껐다. 한 라운드에 팀 전원을 깎는 유일한 수단이라
            // 순차 턴에서는 라운드 한 번에 팀이 무너졌다.
            Assert.IsFalse(GameConstants.Battle.RaidBossUsesAreaAttack,
                "전제: 전체공격 스위치가 꺼져 있다");

            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 1, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 500000, 100, 100),
                CreateTeam(5, 5000, 20, 60),
                new[] { skill });

            for (int round = 0; round < 6; round++)
            {
                Assert.AreNotEqual(RaidBossIntentKind.AreaAttack, raid.NextBossIntent.Kind,
                    $"라운드 {round + 1}");
                Assert.IsFalse(raid.NextBossIntent.IsArea);
                ResolveWholeTeamTurn(raid, 0);
                raid.ResolveBossResponse();
                Assert.IsFalse(raid.BossUsedAoe, $"라운드 {round + 1} 실행");
                raid.CompleteRoundPresentation();
            }
        }

        [Test]
        public void BossSingleAttack_HitsOneSlotOnly()
        {
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 1, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 500000, 120, 100),
                CreateTeam(5, 5000, 20, 60),
                new[] { skill });
            ResolveWholeTeamTurn(raid, 0);

            RaidRoundResult round = raid.ResolveBossResponse();

            Assert.AreEqual(RaidActionKind.BossSingle, round.BossAction.Kind);
            Assert.AreEqual(
                1,
                round.BossDamageBySlot.Count(damage => damage > 0),
                "단일 공격은 슬롯 하나만 깎아야 한다");
        }

        /// <summary>
        /// 전체공격 <b>실행</b> 경로는 그대로 살아 있다 — 스위치를 되돌리면 예전 동작이다.
        /// 그래서 여기서는 리졸버를 직접 불러 슬롯별 피해 기록을 고정한다.
        /// </summary>
        [Test]
        public void ResolveBossIntent_AreaIntent_ReportsActualDamagePerSlot()
        {
            InsectBattleStats boss = new InsectBattleStats(
                CreateData("boss", 5000, 100, 100), 10);
            InsectBattleStats[] team =
            {
                new InsectBattleStats(CreateData("team_0", 1000, 20, 10), 10),
                new InsectBattleStats(CreateData("team_1", 1000, 20, 30), 10),
                new InsectBattleStats(CreateData("team_2", 1000, 20, 60), 10),
                new InsectBattleStats(CreateData("team_3", 1000, 20, 120), 10),
                new InsectBattleStats(CreateData("team_4", 1000, 20, 200), 10)
            };
            RaidBossIntent intent = RaidRoundResolver.CreateBossIntent(
                1, boss, team, 0, null, new FixedRaidRandomSource(), allowAreaAttack: true);
            Assert.AreEqual(RaidBossIntentKind.AreaAttack, intent.Kind, "전제: 전체공격 의도");

            int[] hpBefore = team.Select(stats => stats.CurrentHp).ToArray();
            int[] damageBySlot = new int[team.Length];
            RaidActionResult action = RaidRoundResolver.ResolveBossIntent(
                intent, boss, team, damageBySlot);

            int expectedTotal = 0;
            for (int i = 0; i < team.Length; i++)
            {
                int actual = hpBefore[i] - team[i].CurrentHp;
                Assert.AreEqual(actual, damageBySlot[i], $"slot {i}");
                expectedTotal += actual;
            }
            Assert.AreEqual(expectedTotal, action.Damage);
            Assert.Greater(damageBySlot[0], damageBySlot[4], "방어가 낮은 쪽이 더 아파야 한다");
        }

        [Test]
        public void CompleteRoundPresentation_TicksCooldownExactlyOnce()
        {
            InsectSkill skill = CreateSkill("cooldown_hit", SkillEffectType.Damage, 4, 3);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 5000, 40, 100),
                CreateTeam(5, 500, 40, 80),
                new[] { skill });

            ResolveWholeTeamTurn(raid, 0);
            Assert.AreEqual(3, raid.TeamCooldowns[0][0]);
            raid.ResolveBossResponse();
            Assert.AreEqual(3, raid.TeamCooldowns[0][0]);

            Assert.IsTrue(raid.CompleteRoundPresentation());
            Assert.AreEqual(2, raid.TeamCooldowns[0][0]);
            Assert.IsFalse(raid.CompleteRoundPresentation());
            Assert.AreEqual(2, raid.TeamCooldowns[0][0]);
            Assert.AreEqual(1, raid.TurnNumber);
        }

        [Test]
        public void EverySlotSetsItsOwnCooldown()
        {
            InsectSkill skill = CreateSkill("cd_hit", SkillEffectType.Damage, 40, 3);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 1000000, 40, 100),
                CreateTeam(5, 3000, 60, 80),
                new[] { skill });

            ResolveWholeTeamTurn(raid, 0);

            for (int slot = 0; slot < raid.TeamCooldowns.Length; slot++)
                Assert.AreEqual(3, raid.TeamCooldowns[slot][0], $"slot {slot}");

            raid.ResolveBossResponse();
            Assert.IsTrue(raid.CompleteRoundPresentation());

            for (int slot = 0; slot < raid.TeamCooldowns.Length; slot++)
                Assert.AreEqual(2, raid.TeamCooldowns[slot][0], $"slot {slot} 틱");
        }

        // ── 합체공격 ──

        [Test]
        public void ResolveUniteCommand_TotalMatchesContributionsAndSlots()
        {
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 1, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 10000, 30, 100),
                CreateTeam(5, 500, 60, 80),
                new[] { skill });
            SetUniteGauge(raid, RaidBattleController.UniteGaugeMax);

            RaidRoundResult result = raid.ResolveUniteCommand();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsUnite);
            Assert.AreEqual(5, result.TeamActions.Count);
            Assert.AreEqual(
                result.TeamActions.Sum(action => action.Damage),
                result.TotalDamageToBoss);
            Assert.AreEqual(result.UniteSlotDamages.Sum(), result.TotalDamageToBoss);
            CollectionAssert.AreEqual(result.UniteSlotDamages, raid.UniteSlotDamages);
            Assert.AreEqual(0f, raid.UniteGauge);
            Assert.IsTrue(raid.IsAwaitingBossResponse);
        }

        [Test]
        public void ResolveUniteCommand_MidRound_SkipsSlotsThatAlreadyActed()
        {
            // 게이지는 슬롯마다 차오르므로 팀 턴 **도중에** 100을 넘을 수 있고,
            // CanUniteAttack은 ActiveSlot을 보지 않는다. 그때 이미 행동한 슬롯까지
            // 합체에 참여시키면 한 라운드에 두 번 때린다.
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 1, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 10000, 30, 100),
                CreateTeam(5, 500, 60, 80),
                new[] { skill });

            raid.ResolveTeamCommand(0);
            raid.ResolveTeamCommand(0);
            Assert.AreEqual(2, raid.ActiveSlot, "전제: 슬롯 0·1이 이미 행동했다");

            SetUniteGauge(raid, RaidBattleController.UniteGaugeMax);
            RaidRoundResult result = raid.ResolveUniteCommand();

            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.TeamActions.Count,
                "이미 행동한 슬롯 0·1은 합체에 참여하지 않는다(참여하면 한 라운드 2회 공격)");
            foreach (RaidActionResult action in result.TeamActions)
            {
                Assert.GreaterOrEqual(action.SourceSlot, 2,
                    $"슬롯 {action.SourceSlot}은 이미 행동했는데 합체에 또 들어갔다");
            }
        }

        [Test]
        public void CanUniteAttack_CountsRemainingActors_NotAliveCount()
        {
            // 게이트와 참가 규칙이 같은 수를 봐야 한다. 살아있는 마릿수로 판정하면
            // 마지막 슬롯 차례에도 버튼이 열려 게이지 100을 **1마리분**에 태운다
            // (슬롯 0에서 쓰면 5마리분 — 같은 게이지가 5배 값을 한다).
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 1, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 10000, 30, 100),
                CreateTeam(5, 500, 60, 80),
                new[] { skill });

            for (int i = 0; i < 4; i++) raid.ResolveTeamCommand(0);
            Assert.AreEqual(4, raid.ActiveSlot, "전제: 슬롯 0~3이 행동했고 마지막 한 마리만 남았다");
            Assert.AreEqual(5, raid.AliveCount(), "전제: 팀 턴 중엔 아무도 죽지 않는다");
            Assert.AreEqual(1, raid.RemainingActors);

            SetUniteGauge(raid, RaidBattleController.UniteGaugeMax);

            Assert.IsFalse(raid.CanUniteAttack,
                "참가자가 1마리뿐이면 합체가 열려선 안 된다(게이지만 태우고 평범한 스킬보다 약하다)");
        }

        [Test]
        public void CanUniteAttack_StillOpens_WhenTwoActorsRemain()
        {
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 1, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 10000, 30, 100),
                CreateTeam(5, 500, 60, 80),
                new[] { skill });

            for (int i = 0; i < 3; i++) raid.ResolveTeamCommand(0);
            Assert.AreEqual(2, raid.RemainingActors, "전제: 두 마리가 남았다");

            SetUniteGauge(raid, RaidBattleController.UniteGaugeMax);

            Assert.IsTrue(raid.CanUniteAttack, "2마리 이상 남으면 여전히 발동할 수 있어야 한다");
            Assert.AreEqual(2, raid.ResolveUniteCommand().TeamActions.Count);
        }

        [Test]
        public void CanUseSkill_RejectsSlotThatAlreadyActed()
        {
            // RaidMemberActionResolved 발화 시점에는 ActiveSlot이 아직 방금 행동한 슬롯이고
            // roundStage도 Ready라 CanSubmitTeamCommand가 true다. 그 창에서 커맨드를 받으면
            // 한 곤충이 라운드에 두 번 때린다 — ResolveUniteCommand는 막고 있던 위험이다.
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 1, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 10000, 30, 100),
                CreateTeam(5, 500, 60, 80),
                new[] { skill });

            bool checkedInsideHandler = false;
            raid.RaidMemberActionResolved += _ =>
            {
                // 이 순간 ActiveSlot은 아직 0(방금 행동한 슬롯)이다.
                checkedInsideHandler = true;
                Assert.IsTrue(raid.HasActedThisRound(raid.ActiveSlot),
                    "전제: 이벤트 시점의 ActiveSlot은 이미 행동을 마쳤다");
                Assert.IsFalse(raid.CanUseSkill(0),
                    "이미 행동한 슬롯으로 스킬을 또 쓸 수 있으면 한 라운드에 두 번 때린다");
            };

            raid.ResolveTeamCommand(0);

            Assert.IsTrue(checkedInsideHandler, "핸들러가 실제로 불렸어야 검사가 의미를 갖는다");
            Assert.IsTrue(raid.CanUseSkill(0), "차례가 넘어간 뒤에는 다시 사용 가능해야 한다");
        }

        [Test]
        public void ResolveUniteCommand_MidRound_CarriesOverDamageAlreadyDealt()
        {
            // 합체는 roundInProgress를 새 결과로 갈아끼운다 — 이월하지 않으면 합체 이전
            // 슬롯들의 피해가 사라져 라운드 집계와 UI 기여도가 과소 표시된다.
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 1, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 10000, 30, 100),
                CreateTeam(5, 500, 60, 80),
                new[] { skill });

            RaidRoundResult first = raid.ResolveTeamCommand(0);
            int dealtBefore = first.TotalDamageToBoss;
            Assert.Greater(dealtBefore, 0, "전제: 첫 슬롯이 실제로 피해를 줬다");

            SetUniteGauge(raid, RaidBattleController.UniteGaugeMax);
            RaidRoundResult result = raid.ResolveUniteCommand();

            Assert.AreEqual(
                dealtBefore + result.TeamActions.Sum(action => action.Damage),
                result.TotalDamageToBoss,
                "합체 이전 피해가 라운드 집계에서 사라졌다");
        }

        [Test]
        public void Unite_MidRound_ConsumesEveryRemainingSlot()
        {
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 1, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 1000000, 30, 100),
                CreateTeam(5, 3000, 60, 80),
                new[] { skill });

            raid.ResolveTeamCommand(0);   // 슬롯 0만 행동한 상태에서 합체
            SetUniteGauge(raid, RaidBattleController.UniteGaugeMax);
            raid.ResolveUniteCommand();

            // 합체는 팀 턴을 통째로 소비한다 — 남은 슬롯이 한 라운드에 두 번 때리면 안 된다.
            Assert.IsFalse(raid.CanSubmitTeamCommand);
            Assert.AreEqual(-1, raid.ActiveSlot);
            Assert.IsTrue(raid.IsAwaitingBossResponse);
        }

        // ── 기절: 명중 판정 + 연속 잠금 방지 ──

        [Test]
        public void Stun_SkipsExactlyOneBossResponse()
        {
            InsectSkill stun = CreateSkill("stun", SkillEffectType.Stun, 1, 0);
            InsectSkill hit = CreateSkill("hit", SkillEffectType.Damage, 1, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 100000, 80, 100),
                CreateTeam(5, 3000, 40, 80),
                new[] { stun, hit });
            int hpBefore = raid.TeamStats.Sum(stats => stats.CurrentHp);

            ResolveWholeTeamTurn(raid, 0);   // 전원이 기절기를 시도
            RaidRoundResult stunnedRound = raid.ResolveBossResponse();

            Assert.IsTrue(stunnedRound.BossResponseSkipped);
            Assert.AreEqual(hpBefore, raid.TeamStats.Sum(stats => stats.CurrentHp));
            Assert.IsTrue(raid.CompleteRoundPresentation());

            ResolveWholeTeamTurn(raid, 1);   // 다음 라운드는 평범한 공격
            RaidRoundResult followingRound = raid.ResolveBossResponse();

            Assert.IsFalse(followingRound.BossResponseSkipped);
            Assert.Less(raid.TeamStats.Sum(stats => stats.CurrentHp), hpBefore);
        }

        [Test]
        public void Stun_TwoRoundsInARow_SecondIsResisted()
        {
            // 팀 5마리가 각자 기절기를 들면 매 라운드 재시도해 보스를 영구히 묶을 수 있다.
            // 기절로 한 턴을 건너뛴 다음 라운드는 면역이다.
            InsectSkill stun = CreateSkill("stun", SkillEffectType.Stun, 1, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 100000, 60, 100),
                CreateTeam(5, 3000, 40, 80),
                new[] { stun });

            ResolveWholeTeamTurn(raid, 0);
            raid.ResolveBossResponse();
            raid.CompleteRoundPresentation();
            ResolveWholeTeamTurn(raid, 0);

            StringAssert.Contains("저항", raid.LastActionText,
                "면역으로 막힌 기절은 차례를 소비하므로 플레이어에게 알려야 한다");
        }

        [Test]
        public void Stun_OneLandingIsEnough_ForTheWholeTeam()
        {
            // 기절 판정은 라운드당 한 번이다. 다섯 마리가 각자 굴려 다섯 번 걸리는 게 아니다.
            InsectSkill stun = CreateSkill("stun", SkillEffectType.Stun, 1, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 100000, 80, 100),
                CreateTeam(5, 3000, 40, 80),
                new[] { stun });

            ResolveWholeTeamTurn(raid, 0);
            Assert.IsTrue(raid.ResolveBossResponse().BossResponseSkipped);
            Assert.IsTrue(raid.CompleteRoundPresentation());

            ResolveWholeTeamTurn(raid, 0);
            Assert.IsFalse(raid.ResolveBossResponse().BossResponseSkipped,
                "연속 기절은 면역으로 막혀야 한다");
        }

        [Test]
        public void Stun_WhenRollMisses_BossActsNormally()
        {
            InsectSkill stun = CreateSkill("stun", SkillEffectType.Stun, 1, 0);
            stun.accuracy = 0.6f;   // 1미만이어야 명중을 굴린다
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 100000, 120, 100),
                CreateTeam(5, 3000, 40, 80),
                new[] { stun },
                new MissingRaidRandomSource());
            int hpBefore = raid.TeamStats.Sum(stats => stats.CurrentHp);

            RaidRoundResult teamRound = ResolveWholeTeamTurn(raid, 0);
            RaidRoundResult bossRound = raid.ResolveBossResponse();

            Assert.IsTrue(teamRound.TeamActions[0].Missed);
            Assert.IsFalse(teamRound.TeamActions[0].StunApplied);
            Assert.IsFalse(bossRound.BossResponseSkipped, "빗나간 기절로 보스 턴이 사라지면 안 된다");
            Assert.Less(raid.TeamStats.Sum(stats => stats.CurrentHp), hpBefore);
        }

        // ── 상성·자속이 팀 피해 전 경로에 적용되는가 ──

        [Test]
        public void SupportAssist_SuperEffective_ExceedsNeutral()
        {
            Assert.Greater(
                InsectTypeChart.GetEffectiveness(InsectElement.Leaf, InsectElement.Water, InsectElement.None),
                InsectTypeChart.GetEffectiveness(InsectElement.Leaf, InsectElement.Bug, InsectElement.None),
                "전제: 풀→물이 풀→벌레보다 효과적이어야 이 비교가 의미를 갖는다");

            int neutral = MeasureSupportAssistDamage(InsectElement.Leaf, InsectElement.Bug);
            int strong = MeasureSupportAssistDamage(InsectElement.Leaf, InsectElement.Water);

            Assert.Greater(strong, neutral);
        }

        [Test]
        public void UniteContribution_AppliesTypeChart()
        {
            int neutral = MeasureUniteDamage(InsectElement.Leaf, InsectElement.Bug);
            int strong = MeasureUniteDamage(InsectElement.Leaf, InsectElement.Water);

            Assert.Greater(strong, neutral);
        }

        // ── 헬퍼 ──

        /// <summary>
        /// 팀 전원이 차례로 행동해 팀 턴을 닫는다 — 순차 턴에서 한 라운드의 팀 페이즈 전체다.
        /// 해당 칸이 쿨다운이면 자동 위임으로 흘려보낸다(차례를 비우지 않기 위해서다).
        /// </summary>
        private static RaidRoundResult ResolveWholeTeamTurn(RaidBattleController raid, int skillIndex)
        {
            RaidRoundResult last = null;
            int guard = raid.TeamStats.Length + 1;
            while (guard-- > 0 && raid.CanSubmitTeamCommand)
            {
                RaidRoundResult resolved = raid.CanUseSkill(skillIndex)
                    ? raid.ResolveTeamCommand(skillIndex)
                    : raid.ResolveAutoCommand();
                if (resolved == null) break;
                last = resolved;
            }
            return last;
        }

        private int MeasureFirstActionDamage(bool autoDelegate)
        {
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 60, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 1000000, 30, 60),
                CreateTeam(5, 3000, 200, 80),
                new[] { skill });

            RaidRoundResult round = autoDelegate
                ? raid.ResolveAutoCommand()
                : raid.ResolveTeamCommand(0);
            return round.TeamActions[0].Damage;
        }

        private int MeasureSupportAssistDamage(InsectElement teamElement, InsectElement bossElement)
        {
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 1, 0);
            // 서포트에게 스킬을 주지 않아 **기본 지원 공격 폴백**을 타게 한다 — 그 경로의 상성을 잰다.
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 1000000, 30, 60, bossElement),
                CreateTeam(5, 3000, 200, 80, teamElement),
                new[] { skill },
                supportsHaveSkills: false);

            raid.ResolveTeamCommand(0);
            RaidRoundResult result = raid.ResolveAutoRemaining();
            return result.TeamActions
                .First(action => action.Kind == RaidActionKind.SupportAssist)
                .Damage;
        }

        private int MeasureUniteDamage(InsectElement teamElement, InsectElement bossElement)
        {
            InsectSkill skill = CreateSkill("hit", SkillEffectType.Damage, 1, 0);
            RaidBattleController raid = CreateRaid(
                CreateData("boss", 1000000, 30, 60, bossElement),
                CreateTeam(5, 3000, 200, 80, teamElement),
                new[] { skill });
            SetUniteGauge(raid, RaidBattleController.UniteGaugeMax);

            return raid.ResolveUniteCommand().UniteSlotDamages.Sum();
        }

        private RaidBattleController CreateRaid(InsectData bossData,
            InsectData[] teamData, InsectSkill[] equippedSkills,
            IRaidRandomSource random = null, bool supportsHaveSkills = true)
        {
            GameObject bossObject = Track(new GameObject("RaidTestBoss"));
            InsectEntity bossEntity = bossObject.AddComponent<InsectEntity>();
            SetField(bossEntity, "data", bossData);
            SetField(bossEntity, "level", 10);

            GameObject controllerObject = Track(new GameObject("RaidTestController"));
            RaidBattleController controller =
                controllerObject.AddComponent<RaidBattleController>();
            controller.SetRandomSource(random ?? new FixedRaidRandomSource());

            int[] levels = Enumerable.Repeat(10, teamData.Length).ToArray();
            InsectSkill[][] skills = new InsectSkill[teamData.Length][];
            for (int i = 0; i < skills.Length; i++)
            {
                // 슬롯 0이 첫 행동자. supportsHaveSkills=false면 나머지는 빈 손패라
                // 플래너가 -1을 돌려주고 기본 지원 공격 폴백을 탄다.
                skills[i] = i == 0 || supportsHaveSkills
                    ? equippedSkills
                    : new InsectSkill[0];
            }

            controller.StartRaid(
                bossEntity,
                teamData,
                levels,
                null,
                skills);
            return controller;
        }

        private InsectData[] CreateTeam(int count, int hp, int attack, int defense,
            InsectElement primary = InsectElement.Bug)
        {
            InsectData[] result = new InsectData[count];
            for (int i = 0; i < count; i++)
                result[i] = CreateData($"team_{i}", hp, attack, defense, primary);
            return result;
        }

        private InsectData CreateData(string id, int hp, int attack, int defense,
            InsectElement primary = InsectElement.Bug)
        {
            InsectData data = Track(ScriptableObject.CreateInstance<InsectData>());
            data.insectId = id;
            data.displayName = id;
            data.primaryType = primary;
            data.secondaryType = InsectElement.None;
            data.baseHp = hp;
            data.baseAtk = attack;
            data.baseDef = defense;
            data.candyReward = 1;
            data.expReward = 1;
            return data;
        }

        private InsectSkill CreateSkill(string id, SkillEffectType effectType,
            int power, int cooldown)
        {
            InsectSkill skill = Track(ScriptableObject.CreateInstance<InsectSkill>());
            skill.skillId = id;
            skill.displayName = id;
            skill.element = InsectElement.Bug;
            skill.effectType = effectType;
            skill.power = power;
            skill.cooldownTurns = cooldown;
            skill.accuracy = 1f;
            return skill;
        }

        private T Track<T>(T obj) where T : Object
        {
            createdObjects.Add(obj);
            return obj;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static void SetUniteGauge(RaidBattleController controller, float value)
        {
            PropertyInfo property = typeof(RaidBattleController).GetProperty(
                nameof(RaidBattleController.UniteGauge),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(property);
            MethodInfo setter = property.GetSetMethod(true);
            Assert.IsNotNull(setter);
            setter.Invoke(controller, new object[] { value });
        }

        /// <summary>모든 명중이 성공한다(<c>RollHit</c>는 <c>roll &lt; hitChance</c>).</summary>
        private sealed class FixedRaidRandomSource : IRaidRandomSource
        {
            public float Next01()
            {
                return 0f;
            }

            public int NextInt(int minInclusive, int maxExclusive)
            {
                return minInclusive;
            }
        }

        /// <summary>
        /// 모든 명중이 실패한다. <c>hitChance</c>는 0.3~1로 clamp되므로 0.99면 어떤 명중률에도 빗나간다.
        /// </summary>
        private sealed class MissingRaidRandomSource : IRaidRandomSource
        {
            public float Next01()
            {
                return 0.99f;
            }

            public int NextInt(int minInclusive, int maxExclusive)
            {
                return minInclusive;
            }
        }
    }
}
#endif
