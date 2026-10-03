#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 습격(<see cref="AmbushRules"/>) — 거절 조건 하나하나, 쿨다운 경계, 싸울 곤충, 접근 속도·길 고르기, 그리고 진짜 곤충 DB에서
    /// 습격형이 어느 시간·날씨에 깨는지.
    ///
    /// 실패가 조용한 계열이다: 거절 조건 하나가 빠지면 꿈속·동굴·대화 중에 곤충이 덮치고, 싸울 곤충 검사가 빠지면 첫 파트너가 없는
    /// 플레이어가 닫을 수 없는 창에 갇힌다. 예외도 경고도 없다.
    /// </summary>
    [TestFixture]
    public class AmbushRulesTests
    {
        private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly DayPhase[] Phases = (DayPhase[])System.Enum.GetValues(typeof(DayPhase));
        private static readonly WeatherType[] Weathers = (WeatherType[])System.Enum.GetValues(typeof(WeatherType));

        private readonly List<Object> shared = new List<Object>();
        private readonly List<Object> perTest = new List<Object>();
        private InsectDatabase db;

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in perTest) if (o != null) Object.DestroyImmediate(o);
            perTest.Clear();
        }

        [OneTimeTearDown]
        public void ReleaseSharedDatabase()
        {
            foreach (Object o in shared) if (o != null) Object.DestroyImmediate(o);
            shared.Clear();
            db = null;
        }

        private InsectDatabase RealDatabase()
        {
            if (db != null) return db;
            var host = new GameObject("AmbushRulesTests_RealDb");
            host.SetActive(false);   // Awake(월드 생성)를 막고 private 생성 함수만 부른다
            shared.Add(host);
            var bootstrap = host.AddComponent<PlaySceneBootstrap>();
            db = (InsectDatabase)typeof(PlaySceneBootstrap).GetMethod("EnsureExpandedDatabase", Inst).Invoke(bootstrap, null);
            Assert.IsNotNull(db);
            shared.Add(db);
            return db;
        }

        private static WorldState State(DayPhase phase, WeatherType weather)
            => new WorldState { DayPhase = phase, Weather = weather, Hour24 = 0 };

        /// <summary>밤에 깨는 습격형(비를 싫어함).</summary>
        private static readonly InsectHabit NightHunter =
            new InsectHabit(InsectActivity.Nocturnal, WeatherSet.None, WeatherSet.Rain, InsectTemperament.Ambusher);

        /// <summary>모든 칸이 습격을 허락하는 상태 — 각 테스트는 여기서 칸 하나만 바꾼다.</summary>
        private static AmbushRules.Context Ready()
        {
            return new AmbushRules.Context
            {
                Habit = NightHunter,
                State = State(DayPhase.Night, WeatherType.Clear),
                IsGuardian = false,
                IsEngaged = false,
                EntityCooldownLeft = 0f,
                SinceLastAmbushEnded = float.PositiveInfinity,
                SinceLastEncounterEnded = float.PositiveInfinity,
                PlayerInEncounter = false,
                DreamActive = false,
                InSubArea = false,
                PlayerFrozen = false,
                ModalOpen = false,
                CanFight = true
            };
        }

        // ── 거절 조건 하나씩 ──

        [Test]
        public void Check_EverythingAllows_ReturnsNone()
        {
            Assert.AreEqual(AmbushRefusal.None, AmbushRules.Check(Ready()));
            Assert.IsTrue(AmbushRules.CanAmbush(Ready()));
        }

        [Test]
        public void Check_DocileSpecies_IsNotAwake_AtAnyTime()
        {
            AmbushRules.Context c = Ready();
            c.Habit = new InsectHabit(InsectActivity.Nocturnal, WeatherSet.Fog, WeatherSet.None, InsectTemperament.Docile);
            foreach (DayPhase p in Phases)
                foreach (WeatherType w in Weathers)
                {
                    c.State = State(p, w);
                    Assert.AreEqual(AmbushRefusal.NotAwake, AmbushRules.Check(c), $"온순한 종이 {p}/{w}에 덤벼든다");
                }
        }

        [Test]
        public void Check_NocturnalAmbusherOutsideItsHours_IsNotAwake()
        {
            AmbushRules.Context c = Ready();
            c.State = State(DayPhase.Day, WeatherType.Clear);
            Assert.AreEqual(AmbushRefusal.NotAwake, AmbushRules.Check(c), "낮에 야행성 습격형이 덤벼든다");
            c.State = State(DayPhase.Morning, WeatherType.Clear);
            Assert.AreEqual(AmbushRefusal.NotAwake, AmbushRules.Check(c));
            c.State = State(DayPhase.Evening, WeatherType.Clear);
            Assert.AreEqual(AmbushRefusal.NotAwake, AmbushRules.Check(c), "어스름은 아직이다(좋아하는 날씨가 없다)");
        }

        [Test]
        public void Check_DislikedWeather_KeepsItQuietEvenAtNight()
        {
            AmbushRules.Context c = Ready();
            c.State = State(DayPhase.Night, WeatherType.Rain);
            Assert.AreEqual(AmbushRefusal.NotAwake, AmbushRules.Check(c));
        }

        [Test]
        public void Check_Guardian_NeverAmbushes()
        {
            AmbushRules.Context c = Ready();
            c.IsGuardian = true;
            Assert.AreEqual(AmbushRefusal.Guardian, AmbushRules.Check(c));
        }

        [Test]
        public void Check_AlreadyEngagedInsect_DoesNotAmbush()
        {
            AmbushRules.Context c = Ready();
            c.IsEngaged = true;
            Assert.AreEqual(AmbushRefusal.Engaged, AmbushRules.Check(c));
        }

        [Test]
        public void Check_DuringTheDream_DoesNotAmbush()
        {
            AmbushRules.Context c = Ready();
            c.DreamActive = true;
            Assert.AreEqual(AmbushRefusal.Dream, AmbushRules.Check(c));
        }

        [Test]
        public void Check_InsideASubArea_DoesNotAmbush()
        {
            AmbushRules.Context c = Ready();
            c.InSubArea = true;
            Assert.AreEqual(AmbushRefusal.SubArea, AmbushRules.Check(c));
        }

        [Test]
        public void Check_PlayerAlreadyInAnEncounter_DoesNotAmbush()
        {
            AmbushRules.Context c = Ready();
            c.PlayerInEncounter = true;
            Assert.AreEqual(AmbushRefusal.PlayerInEncounter, AmbushRules.Check(c));
        }

        [Test]
        public void Check_PlayerFrozen_DoesNotAmbush()
        {
            AmbushRules.Context c = Ready();
            c.PlayerFrozen = true;
            Assert.AreEqual(AmbushRefusal.PlayerFrozen, AmbushRules.Check(c));
        }

        [Test]
        public void Check_ModalOpen_DoesNotAmbush()
        {
            AmbushRules.Context c = Ready();
            c.ModalOpen = true;
            Assert.AreEqual(AmbushRefusal.ModalOpen, AmbushRules.Check(c));
        }

        [Test]
        public void Check_NoInsectToFightWith_DoesNotAmbush()
        {
            AmbushRules.Context c = Ready();
            // 튜토리얼 초반 — 배틀팀이 비었다. 습격 창엔 닫는 길이 없어 이 경우 열면 갇힌다.
            c.CanFight = AmbushRules.CanFight(false, 0, 0);
            Assert.AreEqual(AmbushRefusal.NoFighter, AmbushRules.Check(c));
            // 팀은 있지만 전원 기절.
            c.CanFight = AmbushRules.CanFight(false, 3, 0);
            Assert.AreEqual(AmbushRefusal.NoFighter, AmbushRules.Check(c));
        }

        // ── 쿨다운 경계 ──

        [Test]
        public void EntityCooldown_BlocksUntilItRunsOut()
        {
            AmbushRules.Context c = Ready();
            c.EntityCooldownLeft = 0.01f;
            Assert.AreEqual(AmbushRefusal.EntityCooldown, AmbushRules.Check(c));
            c.EntityCooldownLeft = 0f;
            Assert.AreEqual(AmbushRefusal.None, AmbushRules.Check(c));
            c.EntityCooldownLeft = -3f;
            Assert.AreEqual(AmbushRefusal.None, AmbushRules.Check(c));
        }

        [Test]
        public void GlobalCooldown_HoldsForItsFullLength_AfterAnAmbush()
        {
            AmbushRules.Context c = Ready();
            c.SinceLastAmbushEnded = 0f;
            Assert.AreEqual(AmbushRefusal.GlobalCooldown, AmbushRules.Check(c), "습격이 막 끝났는데 또 덤벼든다");
            c.SinceLastAmbushEnded = AmbushRules.GlobalCooldownSeconds - 0.01f;
            Assert.AreEqual(AmbushRefusal.GlobalCooldown, AmbushRules.Check(c));
            c.SinceLastAmbushEnded = AmbushRules.GlobalCooldownSeconds;
            Assert.AreEqual(AmbushRefusal.None, AmbushRules.Check(c));
        }

        [Test]
        public void AfterAnyEncounter_ThereIsAShortBreather()
        {
            AmbushRules.Context c = Ready();
            c.SinceLastEncounterEnded = AmbushRules.AfterEncounterGraceSeconds - 0.01f;
            Assert.AreEqual(AmbushRefusal.GlobalCooldown, AmbushRules.Check(c), "결과 창을 닫자마자 덤벼든다");
            c.SinceLastEncounterEnded = AmbushRules.AfterEncounterGraceSeconds;
            Assert.AreEqual(AmbushRefusal.None, AmbushRules.Check(c));
        }

        [Test]
        public void Cooldowns_AreInTheIntendedBand()
        {
            // 근거는 AmbushRules의 주석(밤에 걸으면 분당 약 1회 마주친다) — "60초 안팎"을 벗어나면 그 계산부터 다시 볼 것.
            Assert.That(AmbushRules.GlobalCooldownSeconds, Is.InRange(45f, 90f));
            Assert.Less(AmbushRules.AfterEncounterGraceSeconds, AmbushRules.GlobalCooldownSeconds);
            Assert.Greater(AmbushRules.EntityCooldownSeconds, 0f);
        }

        [Test]
        public void Remaining_CountsDownToZero_AndNeverGoesNegative()
        {
            Assert.AreEqual(2f, AmbushRules.Remaining(10f, 12f), 1e-5f);
            Assert.AreEqual(0f, AmbushRules.Remaining(12f, 10f), 1e-5f);
            Assert.AreEqual(0f, AmbushRules.Remaining(5f, 0f), 1e-5f);
        }

        // ── 싸울 수 있나 ──

        [Test]
        public void CanFight_OneOnOne_NeedsOneConsciousInsect()
        {
            Assert.IsFalse(AmbushRules.CanFight(false, 0, 0));
            Assert.IsFalse(AmbushRules.CanFight(false, 5, 0), "전원 기절인데 싸우라고 한다");
            Assert.IsTrue(AmbushRules.CanFight(false, 1, 1));
        }

        [Test]
        public void CanFight_RaidTarget_NeedsAFullTeamWithSomeoneStanding()
        {
            Assert.IsFalse(AmbushRules.CanFight(true, BattleTeamManager.MaxSlots - 1, BattleTeamManager.MaxSlots - 1),
                "5칸이 안 찼는데 레이드 습격이 열린다 — [레이드로 맞서기]가 막혀 창에 갇힌다");
            Assert.IsFalse(AmbushRules.CanFight(true, BattleTeamManager.MaxSlots, 0));
            Assert.IsTrue(AmbushRules.CanFight(true, BattleTeamManager.MaxSlots, 1));
        }

        [Test]
        public void RaidRarity_IsEpicAndLegendaryOnly()
        {
            // 습격 판정과 포획 선택 창이 같은 술어를 읽는다 — 어긋나면 습격 창이 막힌 버튼으로 뜬다.
            Assert.IsFalse(CaptureChoiceUI.IsRaidRarity(InsectRarity.Common));
            Assert.IsFalse(CaptureChoiceUI.IsRaidRarity(InsectRarity.Uncommon));
            Assert.IsFalse(CaptureChoiceUI.IsRaidRarity(InsectRarity.Rare));
            Assert.IsTrue(CaptureChoiceUI.IsRaidRarity(InsectRarity.Epic));
            Assert.IsTrue(CaptureChoiceUI.IsRaidRarity(InsectRarity.Legendary));
        }

        // ── 기다릴까 물러날까 ──

        [Test]
        public void IsPauseOnly_IsFrozenAndModal_AndNothingElse()
        {
            foreach (AmbushRefusal r in (AmbushRefusal[])System.Enum.GetValues(typeof(AmbushRefusal)))
            {
                bool expected = r == AmbushRefusal.PlayerFrozen || r == AmbushRefusal.ModalOpen;
                Assert.AreEqual(expected, AmbushRules.IsPauseOnly(r), r.ToString());
            }
        }

        [Test]
        public void Check_AnotherEncounterOutranksFrozenAndModal_SoOtherChasersRetreat()
        {
            // 습격 창·전투도 플레이어를 멈추고 모달을 연다. 그때 「멈춤」이 먼저 걸리면 다가오던 다른 곤충이 기다렸다가
            // 전투가 끝나자마자 덮친다 — 「교전 중」이 먼저 걸려 물러나야 한다.
            AmbushRules.Context c = Ready();
            c.PlayerInEncounter = true;
            c.PlayerFrozen = true;
            c.ModalOpen = true;
            AmbushRefusal r = AmbushRules.Check(c);
            Assert.AreEqual(AmbushRefusal.PlayerInEncounter, r);
            Assert.IsFalse(AmbushRules.IsPauseOnly(r));
        }

        // ── 거리·속도 ──

        private static float PlayerWalkSpeed()
        {
            // 진짜 기본 걸음 — 비활성 오브젝트에 붙여 Awake 없이 필드 초기값만 읽는다.
            var go = new GameObject("AmbushRulesTests_Player");
            go.SetActive(false);
            var pm = go.AddComponent<PlayerMovement>();
            float speed = (float)typeof(PlayerMovement).GetField("moveSpeed", Inst).GetValue(pm);
            Object.DestroyImmediate(go);
            return speed;
        }

        [Test]
        public void ApproachSpeed_IsBelowThePlayersWalk_ButStillAThreat()
        {
            float walk = PlayerWalkSpeed();
            Assert.Greater(walk, 0f);
            Assert.Less(AmbushRules.ApproachSpeed, walk, "걸음보다 빠르면 보고 달아나도 피할 수 없다");
            Assert.GreaterOrEqual(AmbushRules.ApproachSpeed, walk * 0.6f, "너무 느리면 위협이 안 된다");
        }

        [Test]
        public void StandingStill_GetsReached_BeforeTheChaseGivesUp()
        {
            float timeToReach = AmbushRules.HesitateSeconds
                                + (AmbushRules.NoticeRadius - AmbushRules.ReachDistance) / AmbushRules.ApproachSpeed;
            Assert.Less(timeToReach, AmbushRules.MaxChaseSeconds);
            Assert.Less(AmbushRules.ReachDistance, AmbushRules.NoticeRadius);
        }

        [Test]
        public void RunningAwayAtOnce_ShakesItOff_BeforeTheChaseLimit()
        {
            // 알아챈 순간 반대로 달리면 멈칫 동안 걸음만큼 벌어지고, 그 뒤 (걸음 − 접근 속도)씩 벌어진다.
            float walk = PlayerWalkSpeed();
            float gapAfterHesitation = AmbushRules.NoticeRadius + walk * AmbushRules.HesitateSeconds;
            float timeToLeash = Mathf.Max(0f, AmbushRules.LeashDistance - gapAfterHesitation)
                                / (walk - AmbushRules.ApproachSpeed);
            Assert.LessOrEqual(timeToLeash, AmbushRules.MaxChaseSeconds);
            Assert.Greater(AmbushRules.LeashDistance, AmbushRules.NoticeRadius, "경계 반경 언저리에서 들락날락한다");
            Assert.IsTrue(AmbushRules.ShouldGiveUp(AmbushRules.LeashDistance + 0.1f, 0f));
            Assert.IsTrue(AmbushRules.ShouldGiveUp(2f, AmbushRules.MaxChaseSeconds + 0.1f));
            Assert.IsFalse(AmbushRules.ShouldGiveUp(2f, 1f));
            Assert.IsTrue(AmbushRules.HasReached(AmbushRules.ReachDistance));
            Assert.IsFalse(AmbushRules.HasReached(AmbushRules.ReachDistance + 0.01f));
        }

        // ── 다가갈 길 ──

        [Test]
        public void ChooseApproach_OpenGround_GoesStraight_AndStopsShortOfThePlayer()
        {
            Vector3 dir = AmbushRules.ChooseApproach(Vector3.forward * 5f, 5f, 1f, _ => 100f, out float allowed);
            Assert.AreEqual(0f, Vector3.Angle(Vector3.forward, dir), 0.01f);
            Assert.AreEqual(AmbushRules.MaxLegDistance, allowed, 1e-4f, "한 걸음은 최대 길이로 자른다");

            AmbushRules.ChooseApproach(Vector3.forward * 2f, 2f, 1f, _ => 100f, out float close);
            Assert.AreEqual(2f - AmbushRules.ReachDistance * 0.5f, close, 1e-4f, "플레이어를 지나쳐 가면 안 된다");
            Assert.Less(2f - close, AmbushRules.ReachDistance, "걸음을 다 가면 닿는 거리 안이어야 한다");
        }

        [Test]
        public void ChooseApproach_WallAhead_GoesAroundIt_InsteadOfThrough()
        {
            // 정면 1m에 벽, 다른 방향은 뚫렸다.
            Vector3 toward = Vector3.forward;
            System.Func<Vector3, float> clearance = d => Vector3.Angle(toward, d) < 1f ? 1f : 100f;
            Vector3 one = AmbushRules.ChooseApproach(toward * 6f, 6f, 1f, clearance, out float allowed);
            float oneAngle = Vector3.SignedAngle(toward, one, Vector3.up);
            Assert.AreEqual(AmbushRules.ApproachAngles[1], Mathf.Abs(oneAngle), 0.5f, "가장 가까운 비낀 방향으로 돌아가야 한다");
            Assert.AreEqual(AmbushRules.MaxLegDistance, allowed, 1e-4f);

            Vector3 other = AmbushRules.ChooseApproach(toward * 6f, 6f, -1f, clearance, out _);
            float otherAngle = Vector3.SignedAngle(toward, other, Vector3.up);
            Assert.AreEqual(-Mathf.Sign(oneAngle), Mathf.Sign(otherAngle), "좌우 부호를 뒤집으면 반대쪽으로 돌아야 한다");
        }

        [Test]
        public void ChooseApproach_Boxed_StopsAtTheWall_NeverThroughIt()
        {
            Vector3 dir = AmbushRules.ChooseApproach(Vector3.forward * 6f, 6f, 1f, _ => 1.2f, out float allowed);
            Assert.AreEqual(1.2f - FleePath.WallMargin, allowed, 1e-4f, "벽 앞 여유만큼 앞에서 멈춰야 한다");
            Assert.AreEqual(1f, dir.magnitude, 1e-4f);

            AmbushRules.ChooseApproach(Vector3.forward * 6f, 6f, 1f, _ => 0f, out float none);
            Assert.AreEqual(0f, none, 1e-6f, "다 막혔으면 제자리 — 호출부가 잠시 뒤 쫓기를 접는다");
        }

        // ── 한 줄 문구 ──

        [Test]
        public void ReasonLine_SaysWhatWokeIt()
        {
            StringAssert.Contains("밤", AmbushRules.ReasonLine(NightHunter, State(DayPhase.Night, WeatherType.Clear)));
            var fogStalker = new InsectHabit(InsectActivity.Nocturnal, WeatherSet.Fog, WeatherSet.None, InsectTemperament.Ambusher);
            StringAssert.Contains("안개", AmbushRules.ReasonLine(fogStalker, State(DayPhase.Evening, WeatherType.Fog)));
            Assert.AreEqual(AmbushRules.FallbackReason,
                AmbushRules.ReasonLine(InsectHabit.Neutral, State(DayPhase.Day, WeatherType.Clear)));
        }

        [Test]
        public void ReasonLine_EveryAwakeMoment_HasItsOwnLine()
        {
            foreach (string id in InsectHabits.AllIds)
            {
                InsectHabit h = InsectHabits.Of(id);
                if (h.Temperament != InsectTemperament.Ambusher) continue;
                foreach (DayPhase p in Phases)
                    foreach (WeatherType w in Weathers)
                    {
                        if (!InsectHabits.IsAmbusher(h, State(p, w))) continue;
                        string line = AmbushRules.ReasonLine(h, State(p, w));
                        Assert.IsFalse(string.IsNullOrEmpty(line), $"{id} {p}/{w}");
                        Assert.AreNotEqual(AmbushRules.FallbackReason, line, $"{id} {p}/{w}: 깨어 있는데 까닭을 모른다");
                    }
            }
        }

        // ── 진짜 DB — 습격형이 언제 깨는가 ──

        /// <summary>
        /// 필드에 나오는 종(spawnWeight &gt; 0)으로 시간대 × 날씨마다 ①깨어 있는 습격형 종 수 ②필드 한 칸이 「깨어 있는 습격형」일 몫
        /// (전역 등급표 × 그 등급 안의 spawnWeight × 성향 배수 — 리전 풀은 무시한 전체 근사)을 잰다. 표는 로그의 <c>[Ambush]</c> 줄에 남는다.
        /// </summary>
        [Test]
        public void RealDb_AmbushersWakeAtNight_AndNeverByDay()
        {
            var field = new List<InsectData>();
            int ambushSpecies = 0;
            foreach (InsectData d in RealDatabase().insects)
            {
                if (d == null || d.spawnWeight <= 0f) continue;
                field.Add(d);
                if (InsectHabits.For(d).Temperament == InsectTemperament.Ambusher) ambushSpecies++;
            }
            Assert.Greater(ambushSpecies, 0, "필드에 습격형이 하나도 없다");

            var count = new Dictionary<(DayPhase, WeatherType), int>();
            var share = new Dictionary<(DayPhase, WeatherType), float>();
            var log = new StringBuilder();
            log.AppendLine($"[Ambush] 필드 습격형 {ambushSpecies}종 — 깨어 있는 종 수 / 필드 한 칸이 깨어 있는 습격형일 몫");
            foreach (DayPhase p in Phases)
            {
                log.Append("[Ambush] ").Append(p.ToString().PadRight(8));
                foreach (WeatherType w in Weathers)
                {
                    WorldState s = State(p, w);
                    int awake = 0;
                    float total = 0f;
                    for (int r = 0; r < FieldSpawnRules.RarityCount; r++)
                    {
                        float all = 0f, hunting = 0f;
                        foreach (InsectData d in field)
                        {
                            if ((int)d.rarity != r) continue;
                            InsectHabit h = InsectHabits.For(d);
                            float weight = d.spawnWeight * InsectHabits.SpawnWeightMultiplier(h, s);
                            all += weight;
                            if (InsectHabits.IsAmbusher(h, s)) hunting += weight;
                        }
                        if (all > 0f) total += FieldSpawnRules.BaseShare(r) * hunting / all;
                    }
                    foreach (InsectData d in field)
                        if (InsectHabits.IsAmbusher(InsectHabits.For(d), s)) awake++;
                    count[(p, w)] = awake;
                    share[(p, w)] = total;
                    log.Append($"  {w}:{awake,2}종/{total * 100f,4:0.0}%");
                }
                log.AppendLine();
            }
            Debug.Log(log.ToString());

            // 밤에는 어떤 날씨에도 덤벼드는 곤충이 있다.
            float nightSum = 0f;
            foreach (WeatherType w in Weathers)
            {
                Assert.Greater(count[(DayPhase.Night, w)], 0, $"밤 {w}에 깨는 습격형이 없다");
                Assert.GreaterOrEqual(share[(DayPhase.Night, w)], 0.02f, $"밤 {w}의 습격형 몫이 너무 적다");
                nightSum += share[(DayPhase.Night, w)];
            }
            float dayHoursSum = 0f;
            foreach (WeatherType w in Weathers)
                dayHoursSum += share[(DayPhase.Morning, w)] + share[(DayPhase.Day, w)];
            float nightAvg = nightSum / Weathers.Length;
            float dayAvg = dayHoursSum / (Weathers.Length * 2);

            // 아침·낮에는 아무도 싸움을 걸지 않는다 — 요청이 "밤에 싸움을 거는 곤충"이다(InsectHabits.IsAmbushHour).
            // 처음엔 주행성 말벌·사마귀가 한낮에 깨어 맑은 낮(4.6%)이 맑은 밤(4.1%)만큼 사나웠다.
            foreach (WeatherType w in Weathers)
            {
                Assert.AreEqual(0, count[(DayPhase.Morning, w)], $"아침 {w}에 깨는 습격형이 있다");
                Assert.AreEqual(0, count[(DayPhase.Day, w)], $"낮 {w}에 깨는 습격형이 있다");
            }
            Assert.AreEqual(0f, dayAvg, 1e-6f);
            // 밤이 저녁보다 사납다 — 저녁은 좋아하는 날씨일 때만 일찍 깬다.
            float eveningSum = 0f;
            foreach (WeatherType w in Weathers) eveningSum += share[(DayPhase.Evening, w)];
            Assert.Greater(nightAvg, eveningSum / Weathers.Length,
                $"밤(평균 {nightAvg * 100f:0.0}%)이 저녁보다 사납지 않다");
        }
    }
}
#endif
