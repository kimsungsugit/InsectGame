#if UNITY_EDITOR
using System.Collections.Generic;
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
    /// 낮·밤·날씨 전투 보정(<see cref="BattleEnvironment"/>)과 도주 규칙(<see cref="BattleEscapeRules"/>).
    ///
    /// 실패가 전부 조용한 계열이다: 보정이 대결·꿈 챔피언전에 새어도, 다음 전투가 이전 칩을 물려받아도, 교체한 곤충이
    /// 보정을 못 받아도 예외 하나 없이 숫자만 10%씩 어긋난다. 그래서 순수 규칙과 함께 <b>진짜 컨트롤러</b>를 진짜 시계·날씨
    /// (<see cref="GameClock"/>·<see cref="WeatherSystem"/>·<see cref="WorldStateProvider"/>)에 물려 돌린다.
    /// 성향은 진짜 표(<c>InsectHabitTable</c>)를 읽는다 — 아래 종 ID의 성향이 바뀌면 이 테스트도 같이 고친다.
    /// </summary>
    [TestFixture]
    public class BattleEnvironmentTests
    {
        private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.NonPublic;

        // 표에서 고른 종 — 성향이 이 주석대로여야 아래 기대값이 맞는다.
        private const string NocturnalFogLover = "stag_beetle";   // 야행성 · 안개 ↑ · 싫은 날씨 없음
        private const string DiurnalPlain = "hornet_asian";       // 주행성 · 좋은 날씨 없음 · 비 ↓
        private const string Neutral = "beetle_basic";            // 시간·날씨 무관
        private const string SnowLover = "beetle_rime";           // 시간 무관 · 눈 ↑

        private const int Level = 10;

        private static readonly DayPhase[] Phases = (DayPhase[])System.Enum.GetValues(typeof(DayPhase));
        private static readonly WeatherType[] Weathers = (WeatherType[])System.Enum.GetValues(typeof(WeatherType));

        private readonly List<Object> objects = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (Object o in objects) if (o != null) Object.DestroyImmediate(o);
            objects.Clear();
        }

        // ── 노트 계산(순수) ──

        [Test]
        public void Note_NocturnalAtNight_IsFavorable_AndNamesTheTime()
        {
            var habit = new InsectHabit(InsectActivity.Nocturnal, WeatherSet.None, WeatherSet.None, InsectTemperament.Docile);
            BattleEnvironmentNote note = BattleEnvironment.NoteFor(habit, State(DayPhase.Night, WeatherType.Clear));

            Assert.IsTrue(note.HasEffect);
            Assert.AreEqual(InsectHabits.FavorableBattleMultiplier, note.Multiplier, 1e-6f);
            Assert.AreEqual(10, note.Percent);
            Assert.AreEqual("밤 · 야행성", note.Reason);
        }

        [Test]
        public void Note_DislikedWeather_IsUnfavorable_AndNamesTheWeather()
        {
            var habit = new InsectHabit(InsectActivity.Any, WeatherSet.None, WeatherSet.Rain, InsectTemperament.Docile);
            BattleEnvironmentNote note = BattleEnvironment.NoteFor(habit, State(DayPhase.Day, WeatherType.Rain));

            Assert.IsTrue(note.HasEffect);
            Assert.AreEqual(InsectHabits.UnfavorableBattleMultiplier, note.Multiplier, 1e-6f);
            Assert.AreEqual(-10, note.Percent);
            Assert.AreEqual("비 · 비를 싫어함", note.Reason);
        }

        [Test]
        public void Note_TimeAndWeatherAgree_NamesBoth()
        {
            var habit = new InsectHabit(InsectActivity.Nocturnal, WeatherSet.Fog, WeatherSet.None, InsectTemperament.Docile);
            BattleEnvironmentNote note = BattleEnvironment.NoteFor(habit, State(DayPhase.Night, WeatherType.Fog));

            Assert.AreEqual(InsectHabits.FavorableBattleMultiplier, note.Multiplier, 1e-6f, "유리는 겹쳐도 +10%다(Fit이 −1~+1로 가둔다)");
            Assert.AreEqual("밤 · 야행성 · 안개를 좋아함", note.Reason);
        }

        [Test]
        public void Note_OnlyTheSideThatMadeTheMultiplier_IsNamed()
        {
            // 잠잘 시간(−1)에 좋아하는 날씨(+1)면 서로 지워 칩이 없다.
            var diurnalClear = new InsectHabit(InsectActivity.Diurnal, WeatherSet.Clear, WeatherSet.None, InsectTemperament.Docile);
            BattleEnvironmentNote cancelled = BattleEnvironment.NoteFor(diurnalClear, State(DayPhase.Night, WeatherType.Clear));
            Assert.IsFalse(cancelled.HasEffect);
            Assert.AreEqual(1f, cancelled.Multiplier);
            Assert.AreEqual(string.Empty, cancelled.Reason);

            // 저녁(시간 0)에 싫은 날씨(−1)면 날씨만 이유다 — "저녁 · 주행성"이라고 적으면 거짓이다.
            var diurnalRainHater = new InsectHabit(InsectActivity.Diurnal, WeatherSet.None, WeatherSet.Rain, InsectTemperament.Docile);
            BattleEnvironmentNote weatherOnly = BattleEnvironment.NoteFor(diurnalRainHater, State(DayPhase.Evening, WeatherType.Rain));
            Assert.AreEqual(InsectHabits.UnfavorableBattleMultiplier, weatherOnly.Multiplier, 1e-6f);
            Assert.AreEqual("비 · 비를 싫어함", weatherOnly.Reason);

            // 낮(+1)에 싫은 날씨(−1)는 0, 밤(−1)에 싫은 날씨(−1)는 둘 다 이유다.
            Assert.IsFalse(BattleEnvironment.NoteFor(diurnalRainHater, State(DayPhase.Day, WeatherType.Rain)).HasEffect);
            Assert.AreEqual("밤 · 주행성 · 비를 싫어함",
                BattleEnvironment.NoteFor(diurnalRainHater, State(DayPhase.Night, WeatherType.Rain)).Reason);
        }

        [Test]
        public void Note_NeutralHabit_IsNone_InEveryPhaseAndWeather()
        {
            foreach (DayPhase phase in Phases)
            foreach (WeatherType weather in Weathers)
            {
                BattleEnvironmentNote note = BattleEnvironment.NoteFor(InsectHabit.Neutral, State(phase, weather));
                Assert.IsFalse(note.HasEffect, $"{phase}/{weather}");
                Assert.AreEqual(1f, note.Multiplier, $"{phase}/{weather}");
                Assert.AreEqual(string.Empty, note.Reason, $"{phase}/{weather}");
            }
        }

        [Test]
        public void Note_FromInsectData_ReadsTheHabitTable_AndNullIsNone()
        {
            InsectData stag = Insect(NocturnalFogLover);
            BattleEnvironmentNote note = BattleEnvironment.NoteFor(stag, State(DayPhase.Night, WeatherType.Clear));
            Assert.AreEqual("밤 · 야행성", note.Reason);
            Assert.AreEqual(10, note.Percent);

            BattleEnvironmentNote none = BattleEnvironment.NoteFor((InsectData)null, State(DayPhase.Night, WeatherType.Fog));
            Assert.IsFalse(none.HasEffect);
            Assert.AreEqual(string.Empty, none.Reason);
        }

        [Test]
        public void Note_DefaultAndNone_HaveNoEffect()
        {
            // 아직 정하지 않은 표지(default — 배수 0)도 "영향 없음"으로 읽혀야 화면이 빈 칩을 안 그린다.
            Assert.IsFalse(default(BattleEnvironmentNote).HasEffect);
            Assert.IsFalse(BattleEnvironmentNote.None.HasEffect);
            Assert.AreEqual(0, BattleEnvironmentNote.None.Percent);
            Assert.AreEqual(string.Empty, BattleEnvironmentNote.None.Reason);
        }

        [Test]
        public void Note_EveryTableHabit_EveryPhaseAndWeather_HasAShortHonestReason()
        {
            string longest = string.Empty;
            int effects = 0;
            foreach (string id in InsectHabits.AllIds)
            foreach (DayPhase phase in Phases)
            foreach (WeatherType weather in Weathers)
            {
                InsectHabit habit = InsectHabits.Of(id);
                WorldState state = State(phase, weather);
                BattleEnvironmentNote note = BattleEnvironment.NoteFor(habit, state);
                string where = $"{id} {phase}/{weather}";

                Assert.AreEqual(InsectHabits.BattleStatMultiplier(habit, state), note.Multiplier, 1e-6f, where);
                Assert.AreEqual(note.HasEffect, !string.IsNullOrEmpty(note.Reason), $"효과와 문구가 어긋남: {where}");
                if (!note.HasEffect) continue;

                effects++;
                Assert.IsFalse(note.Reason.Contains("%") || note.Reason.Contains("+") || note.Reason.Contains("-"),
                    $"문구에 기호·퍼센트가 들어갔다(칩이 따로 붙인다): {where} \"{note.Reason}\"");
                // 유리한데 "싫어함", 불리한데 "좋아함"이면 이유가 거짓이다.
                if (note.Percent > 0) Assert.IsFalse(note.Reason.Contains("싫어함"), $"{where} \"{note.Reason}\"");
                else Assert.IsFalse(note.Reason.Contains("좋아함"), $"{where} \"{note.Reason}\"");
                if (note.Reason.Length > longest.Length) longest = note.Reason;
            }

            TestContext.WriteLine($"[BattleEnvironment] effects={effects}, longest=\"{longest}\" ({longest.Length}자)");
            Assert.Greater(effects, 0);
            Assert.LessOrEqual(longest.Length, 20, $"칩 문구가 너무 길다: \"{longest}\"");
        }

        // ── 적용 범위(순수) ──

        [Test]
        public void Applies_OnlyOrdinaryOutdoorWildBattles()
        {
            foreach (bool duel in new[] { false, true })
            foreach (bool sandbox in new[] { false, true })
            foreach (bool guardian in new[] { false, true })
            foreach (bool indoor in new[] { false, true })
            {
                bool expected = !duel && !sandbox && !guardian && !indoor;
                Assert.AreEqual(expected, BattleEnvironment.Applies(duel, sandbox, guardian, indoor),
                    $"duel={duel} sandbox={sandbox} guardian={guardian} indoor={indoor}");
            }
        }

        [Test]
        public void IsIndoor_CaveYes_IslandAndFieldNo()
        {
            Assert.IsFalse(BattleEnvironment.IsIndoor(null), "필드");
            Assert.IsTrue(BattleEnvironment.IsIndoor(new SubAreaData { subAreaId = "cave" }), "동굴");
            Assert.IsFalse(BattleEnvironment.IsIndoor(new SubAreaData { subAreaId = "player_island", detached = true }), "나의 섬");
        }

        [Test]
        public void WeatherRegionId_FieldUsesRegion_SubAreaUsesWorldWeather()
        {
            var frost = new RegionData { regionId = "frostline" };
            Assert.AreEqual("frostline", BattleEnvironment.WeatherRegionId(frost, null));
            Assert.IsNull(BattleEnvironment.WeatherRegionId(null, null), "리전 밖은 세계 날씨");
            // 섬에 있는 동안 CurrentRegion은 마지막 리전에 붙어 있다 — 그걸 읽으면 섬에 설산의 눈이 내린다.
            Assert.IsNull(BattleEnvironment.WeatherRegionId(frost, new SubAreaData { detached = true }));
        }

        // ── 능력치 보정(InsectBattleStats.ApplyEnvironment) ──

        [Test]
        public void ApplyEnvironment_ScalesAttackAndDefense_LeavesHpAlone()
        {
            InsectData data = Insect(Neutral);
            var reference = new InsectBattleStats(data, Level);
            var stats = new InsectBattleStats(data, Level);
            stats.ApplyDamage(25);
            int hp = stats.CurrentHp;

            stats.ApplyEnvironment(1.1f);

            Assert.AreEqual(Scaled(reference.Attack, 1.1f), stats.Attack);
            Assert.AreEqual(Scaled(reference.Defense, 1.1f), stats.Defense);
            Assert.AreEqual(reference.MaxHp, stats.MaxHp, "최대 HP가 바뀌면 HP바가 튄다");
            Assert.AreEqual(hp, stats.CurrentHp);
            Assert.AreEqual(1.1f, stats.EnvironmentMultiplier, 1e-6f);
        }

        [Test]
        public void ApplyEnvironment_CalledAgain_ReplacesInsteadOfCompounding()
        {
            InsectData data = Insect(Neutral);
            var reference = new InsectBattleStats(data, Level);
            var stats = new InsectBattleStats(data, Level);

            stats.ApplyEnvironment(1.1f);
            stats.ApplyEnvironment(1.1f);
            Assert.AreEqual(Scaled(reference.Attack, 1.1f), stats.Attack, "두 번 불러 1.21배가 됐다");

            stats.ApplyEnvironment(0.9f);
            Assert.AreEqual(Scaled(reference.Attack, 0.9f), stats.Attack);
            Assert.AreEqual(Scaled(reference.Defense, 0.9f), stats.Defense);

            stats.ApplyEnvironment(1f);
            Assert.AreEqual(reference.Attack, stats.Attack, "1을 걸면 원래 값으로 돌아와야 한다");
            Assert.AreEqual(reference.Defense, stats.Defense);
        }

        [Test]
        public void ApplyEnvironment_LeavesPerTurnBonusesAlone()
        {
            var stats = new InsectBattleStats(Insect(Neutral), Level) { AttackBonus = 0.3f, DefenseBonus = 0.2f };
            stats.ApplyEnvironment(0.9f);
            Assert.AreEqual(0.3f, stats.AttackBonus, 1e-6f, "의상·아이템·버프 경로는 매 턴 다시 계산된다 — 섞으면 지워진다");
            Assert.AreEqual(0.2f, stats.DefenseBonus, 1e-6f);
        }

        [Test]
        public void ApplyEnvironment_InvalidMultiplier_IsTreatedAsOne_AndNeverBelowOne()
        {
            InsectData data = Insect(Neutral);
            var reference = new InsectBattleStats(data, Level);
            foreach (float bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                var stats = new InsectBattleStats(data, Level);
                stats.ApplyEnvironment(bad);
                Assert.AreEqual(reference.Attack, stats.Attack, $"multiplier={bad}");
                Assert.AreEqual(reference.Defense, stats.Defense, $"multiplier={bad}");
            }

            InsectData weak = Insect(Neutral, atk: -1, def: -1);   // 공격·방어가 바닥(1)인 곤충
            var tiny = new InsectBattleStats(weak, 1);
            Assert.AreEqual(1, tiny.Attack);
            tiny.ApplyEnvironment(0.9f);
            Assert.AreEqual(1, tiny.Attack, "보정이 공격력을 0으로 만들면 안 된다");
            Assert.AreEqual(1, tiny.Defense);
        }

        // ── 컨트롤러 ──

        [Test]
        public void WildBattle_AtNight_EachSideUsesItsOwnHabit_BeforeTheFirstFrame()
        {
            Sky sky = NewSky(DayPhase.Night, WeatherType.Clear);
            InsectBattleController battle = NewController(sky);
            InsectData stag = Insect(NocturnalFogLover);
            InsectData hornet = Insect(DiurnalPlain);
            var playerRef = new InsectBattleStats(stag, Level);
            var enemyRef = new InsectBattleStats(hornet, Level);

            BattleEnvironmentNote seenOnStart = default;
            BattleEnvironmentNote seenOnUpdate = default;
            int seenAttack = 0;
            battle.BattleUpdated += (p, e) => { seenOnUpdate = battle.PlayerEnvironment; };
            battle.StartBattle(stag, Level, Wild(hornet, Level), (p, e) =>
            {
                seenOnStart = battle.PlayerEnvironment;
                seenAttack = p.Attack;
            });

            Assert.IsTrue(seenOnStart.HasEffect, "onStarted가 칩보다 먼저 울렸다");
            Assert.IsTrue(seenOnUpdate.HasEffect, "BattleUpdated가 칩보다 먼저 울렸다");
            Assert.AreEqual(Scaled(playerRef.Attack, InsectHabits.FavorableBattleMultiplier), seenAttack,
                "첫 프레임의 능력치가 보정 전 값이다");

            Assert.AreEqual(10, battle.PlayerEnvironment.Percent);
            Assert.AreEqual("밤 · 야행성", battle.PlayerEnvironment.Reason);
            Assert.AreEqual(-10, battle.EnemyEnvironment.Percent);
            Assert.AreEqual("밤 · 주행성", battle.EnemyEnvironment.Reason);

            InsectBattleStats player = PlayerStats(battle);
            InsectBattleStats enemy = EnemyStats(battle);
            Assert.AreEqual(Scaled(playerRef.Defense, InsectHabits.FavorableBattleMultiplier), player.Defense);
            Assert.AreEqual(Scaled(enemyRef.Attack, InsectHabits.UnfavorableBattleMultiplier), enemy.Attack);
            Assert.AreEqual(Scaled(enemyRef.Defense, InsectHabits.UnfavorableBattleMultiplier), enemy.Defense);
            Assert.AreEqual(playerRef.MaxHp, player.MaxHp, "HP는 보정하지 않는다");
            Assert.AreEqual(player.MaxHp, player.CurrentHp);
            Assert.AreEqual(enemyRef.MaxHp, enemy.MaxHp);
        }

        [Test]
        public void WildBattle_NeutralInsects_GetNoNote()
        {
            Sky sky = NewSky(DayPhase.Night, WeatherType.Fog);
            InsectBattleController battle = NewController(sky);
            InsectData neutral = Insect(Neutral);
            var reference = new InsectBattleStats(neutral, Level);

            battle.StartBattle(neutral, Level, Wild(Insect(Neutral), Level));

            Assert.IsFalse(battle.PlayerEnvironment.HasEffect);
            Assert.IsFalse(battle.EnemyEnvironment.HasEffect);
            Assert.AreEqual(string.Empty, battle.PlayerEnvironment.Reason);
            Assert.AreEqual(reference.Attack, PlayerStats(battle).Attack);
        }

        [Test]
        public void Duel_IsNeverAdjusted()
        {
            Sky sky = NewSky(DayPhase.Night, WeatherType.Fog);
            InsectBattleController battle = NewController(sky);
            InsectData stag = Insect(NocturnalFogLover);
            var reference = new InsectBattleStats(stag, Level);

            Assert.IsTrue(battle.StartDuel(stag, Level, Insect(DiurnalPlain), Level));

            AssertNoEnvironment(battle, reference);
        }

        [Test]
        public void Sandbox_IsNeverAdjusted()
        {
            Sky sky = NewSky(DayPhase.Night, WeatherType.Fog);
            InsectBattleController battle = NewController(sky);
            InsectData stag = Insect(NocturnalFogLover);
            var reference = new InsectBattleStats(stag, Level);

            Assert.IsTrue(battle.StartSandbox(stag, Level, Insect(DiurnalPlain), Level, null, null));

            AssertNoEnvironment(battle, reference);
        }

        [Test]
        public void Guardian_IsNeverAdjusted()
        {
            Sky sky = NewSky(DayPhase.Night, WeatherType.Fog);
            InsectBattleController battle = NewController(sky);
            InsectData stag = Insect(NocturnalFogLover);
            var reference = new InsectBattleStats(stag, Level);

            battle.StartBattle(stag, Level, Wild(Insect(DiurnalPlain), Level, guardianRegion: "meadow"));

            Assert.AreEqual("meadow", battle.EnemyGuardianRegionId);
            AssertNoEnvironment(battle, reference);
        }

        [Test]
        public void IndoorSubArea_IsNeverAdjusted()
        {
            Sky sky = NewSky(DayPhase.Night, WeatherType.Fog);
            SetField(sky.Regions, "currentSubArea", new SubAreaData { subAreaId = "cave" });
            InsectBattleController battle = NewController(sky);
            InsectData stag = Insect(NocturnalFogLover);
            var reference = new InsectBattleStats(stag, Level);

            battle.StartBattle(stag, Level, Wild(Insect(DiurnalPlain), Level));

            AssertNoEnvironment(battle, reference);
        }

        [Test]
        public void FieldBattle_ReadsTheRegionsVisibleWeather()
        {
            // 세계는 비지만 설산에서는 눈이다 — 눈을 좋아하는 종이 힘을 얻는다(인자 없는 GetWorldState였다면 "비"로 판정).
            Sky sky = NewSky(DayPhase.Day, WeatherType.Rain);
            SetField(sky.Regions, "currentRegion", new RegionData { regionId = "frostline" });
            InsectBattleController battle = NewController(sky);
            InsectData rime = Insect(SnowLover);

            battle.StartBattle(rime, Level, Wild(Insect(Neutral), Level));

            Assert.AreEqual(10, battle.PlayerEnvironment.Percent);
            Assert.AreEqual("눈 · 눈을 좋아함", battle.PlayerEnvironment.Reason);
        }

        [Test]
        public void Island_IsAdjusted_WithTheWorldWeather_NotTheLastRegions()
        {
            Sky sky = NewSky(DayPhase.Night, WeatherType.Rain);
            SetField(sky.Regions, "currentRegion", new RegionData { regionId = "frostline" });   // 섬에서도 붙어 있는 마지막 리전
            SetField(sky.Regions, "currentSubArea", new SubAreaData { subAreaId = "player_island", detached = true });
            InsectBattleController battle = NewController(sky);
            InsectData stag = Insect(NocturnalFogLover);

            battle.StartBattle(stag, Level, Wild(Insect(SnowLover), Level));

            Assert.AreEqual("밤 · 야행성", battle.PlayerEnvironment.Reason, "섬은 하늘 아래다 — 보정이 걸린다");
            Assert.IsFalse(battle.EnemyEnvironment.HasEffect, "섬에 설산의 눈이 내렸다(세계 날씨는 비)");
        }

        [Test]
        public void NextBattle_DoesNotInheritThePreviousAdjustment()
        {
            Sky sky = NewSky(DayPhase.Night, WeatherType.Clear);
            InsectBattleController battle = NewController(sky);
            InsectData stag = Insect(NocturnalFogLover);
            var reference = new InsectBattleStats(stag, Level);

            battle.StartBattle(stag, Level, Wild(Insect(DiurnalPlain), Level));
            Assert.IsTrue(battle.PlayerEnvironment.HasEffect);

            Assert.IsTrue(battle.StartDuel(stag, Level, Insect(DiurnalPlain), Level));
            AssertNoEnvironment(battle, reference);

            battle.StartBattle(stag, Level, Wild(Insect(DiurnalPlain), Level));
            Assert.IsTrue(battle.StartSandbox(stag, Level, Insect(DiurnalPlain), Level, null, null));
            AssertNoEnvironment(battle, reference);

            // 낮으로 바꾸고 다시 야생 — 이전 +10%에 겹치지 않고 새 하늘의 −10%만 걸린다.
            sky.Set(DayPhase.Day, WeatherType.Clear);
            battle.StartBattle(stag, Level, Wild(Insect(DiurnalPlain), Level));
            Assert.AreEqual(-10, battle.PlayerEnvironment.Percent);
            Assert.AreEqual("낮 · 야행성", battle.PlayerEnvironment.Reason);
            Assert.AreEqual(Scaled(reference.Attack, InsectHabits.UnfavorableBattleMultiplier), PlayerStats(battle).Attack);
        }

        [Test]
        public void Swap_AdjustsTheIncomingInsect_UnderTheBattlesStartingSky()
        {
            Sky sky = NewSky(DayPhase.Night, WeatherType.Clear);
            InsectBattleController battle = NewController(sky);
            InsectData stag = Insect(NocturnalFogLover);
            InsectData hornet = Insect(DiurnalPlain);
            var hornetRef = new InsectBattleStats(hornet, Level);

            battle.StartBattle(stag, Level, Wild(Insect(Neutral), Level));
            Assert.AreEqual("밤 · 야행성", battle.PlayerEnvironment.Reason);

            // 전투 도중 해가 떠도 이번 전투는 시작할 때의 하늘(밤)로 끝까지 잰다 — 상대 표지와 같은 하늘이다.
            sky.Set(DayPhase.Day, WeatherType.Clear);
            BattleEnvironmentNote seenOnUpdate = default;
            battle.BattleUpdated += (p, e) => seenOnUpdate = battle.PlayerEnvironment;

            battle.SwapPlayerInsect(hornet, Level);

            Assert.AreEqual(-10, battle.PlayerEnvironment.Percent);
            Assert.AreEqual("밤 · 주행성", battle.PlayerEnvironment.Reason);
            Assert.AreEqual("밤 · 주행성", seenOnUpdate.Reason, "교체의 BattleUpdated가 옛 곤충의 칩을 들고 울렸다");
            InsectBattleStats player = PlayerStats(battle);
            Assert.AreEqual(Scaled(hornetRef.Attack, InsectHabits.UnfavorableBattleMultiplier), player.Attack);
            Assert.AreEqual(hornetRef.MaxHp, player.MaxHp);
        }

        [Test]
        public void Swap_InADuel_StaysUnadjusted()
        {
            Sky sky = NewSky(DayPhase.Night, WeatherType.Clear);
            InsectBattleController battle = NewController(sky);
            InsectData stag = Insect(NocturnalFogLover);
            var reference = new InsectBattleStats(stag, Level);

            Assert.IsTrue(battle.StartDuel(Insect(Neutral), Level, Insect(Neutral), Level));
            battle.SwapPlayerInsect(stag, Level);

            AssertNoEnvironment(battle, reference);
        }

        [Test]
        public void NocturnalPlayer_HitsHarderAtNight_ThanByDay()
        {
            int Damage(DayPhase phase)
            {
                Sky sky = NewSky(phase, WeatherType.Clear);
                InsectBattleController battle = NewController(sky);
                battle.SetRandomSeed(7);
                // 상대 방어를 높여 공격/방어 비율이 밤·낮 모두 clamp(0.7~1.5) 안에 들게 한다 — 상한에 붙으면 차이가 묻힌다.
                battle.StartBattle(Insect(NocturnalFogLover), Level, Wild(Insect(Neutral, def: 60), Level));
                InsectBattleStats enemy = EnemyStats(battle);
                int before = enemy.CurrentHp;
                battle.UseBasicAttack();
                return before - battle.EnemyHpAfterPlayerAction;
            }

            int night = Damage(DayPhase.Night);
            int day = Damage(DayPhase.Day);
            TestContext.WriteLine($"[BattleEnvironment] basic attack night={night} day={day}");
            Assert.Greater(night, day);
        }

        // ── 도주 규칙 ──

        [Test]
        public void EscapeChance_SameLevel_IsHalf()
        {
            Assert.AreEqual(0.5f, BattleEscapeRules.Chance(15, 15), 1e-6f);
        }

        [Test]
        public void EscapeChance_HigherPlayerEscapesMore_AndIsClamped()
        {
            Assert.AreEqual(0.7f, BattleEscapeRules.Chance(14, 10), 1e-5f, "내가 4 높으면 +20%");
            Assert.AreEqual(0.3f, BattleEscapeRules.Chance(10, 14), 1e-5f, "내가 4 낮으면 −20%");
            Assert.AreEqual(0.9f, BattleEscapeRules.Chance(18, 10), 1e-5f, "+8은 정확히 상한");
            Assert.AreEqual(0.9f, BattleEscapeRules.Chance(60, 1), 1e-6f, "상한 90%");
            Assert.AreEqual(0.1f, BattleEscapeRules.Chance(2, 10), 1e-5f, "−8은 정확히 하한");
            Assert.AreEqual(0.1f, BattleEscapeRules.Chance(1, 60), 1e-6f, "하한 10%");
        }

        [Test]
        public void EscapeChance_MatchesTheFormerInlineFormulaExactly()
        {
            for (int player = 1; player <= 60; player++)
            for (int enemy = 1; enemy <= 60; enemy++)
            {
                float former = Mathf.Clamp(0.5f + (player - enemy) * 0.05f, 0.1f, 0.9f);
                Assert.AreEqual(former, BattleEscapeRules.Chance(player, enemy), $"player={player} enemy={enemy}");
            }
        }

        [Test]
        public void TryEscape_UsesTheEscapeRule()
        {
            // 첫 난수가 0.5~0.8 사이인 시드 — 같은 레벨(50%)이면 실패하고, 6 높으면(80%) 성공해야 한다.
            int seed = 0;
            for (; seed < 5000; seed++)
            {
                float roll = new BattleRandomSource(seed).Next01();
                if (roll >= 0.5f && roll < 0.8f) break;
            }
            Assert.Less(seed, 5000);

            InsectData data = Insect(Neutral);
            InsectBattleController even = NewController(null);
            Assert.IsTrue(even.StartDuel(data, 15, data, 15));
            even.SetRandomSeed(seed);
            Assert.IsFalse(even.TryEscape(), "같은 레벨인데 도망쳤다");

            InsectBattleController higher = NewController(null);
            Assert.IsTrue(higher.StartDuel(data, 21, data, 15));
            higher.SetRandomSeed(seed);
            Assert.IsTrue(higher.TryEscape(), "6 높은데 못 도망쳤다 — 레벨 차의 부호가 뒤집혔다");
        }

        // ── 헬퍼 ──

        private static WorldState State(DayPhase phase, WeatherType weather)
        {
            return new WorldState { DayPhase = phase, Weather = weather, Hour24 = HourOf(phase) };
        }

        private static int HourOf(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Morning: return 8;
                case DayPhase.Day: return 12;
                case DayPhase.Evening: return 19;
                default: return 0;
            }
        }

        private static int Scaled(int value, float multiplier)
        {
            return Mathf.Max(1, Mathf.RoundToInt(value * multiplier));
        }

        private void AssertNoEnvironment(InsectBattleController battle, InsectBattleStats reference)
        {
            Assert.IsFalse(battle.PlayerEnvironment.HasEffect, $"내 곤충 칩: \"{battle.PlayerEnvironment.Reason}\"");
            Assert.IsFalse(battle.EnemyEnvironment.HasEffect, $"상대 칩: \"{battle.EnemyEnvironment.Reason}\"");
            Assert.AreEqual(string.Empty, battle.PlayerEnvironment.Reason);
            Assert.AreEqual(string.Empty, battle.EnemyEnvironment.Reason);
            Assert.AreEqual(reference.Attack, PlayerStats(battle).Attack, "보정 없는 전투인데 공격력이 바뀌었다");
            Assert.AreEqual(reference.Defense, PlayerStats(battle).Defense);
        }

        private T Track<T>(T obj) where T : Object
        {
            objects.Add(obj);
            return obj;
        }

        private InsectData Insect(string id, int atk = 40, int def = 30)
        {
            InsectData data = Track(ScriptableObject.CreateInstance<InsectData>());
            data.insectId = id;
            data.displayName = id;
            data.baseHp = 4000;   // 몇 번 주고받아도 아무도 쓰러지지 않게(쓰러지면 보상·디스폰 경로가 돈다)
            data.baseAtk = atk;
            data.baseDef = def;
            data.primaryType = InsectElement.None;
            data.secondaryType = InsectElement.None;
            InsectSkill skill = Track(ScriptableObject.CreateInstance<InsectSkill>());
            skill.effectType = SkillEffectType.Damage;
            skill.element = InsectElement.None;
            skill.power = 20;
            skill.accuracy = 1f;
            skill.cooldownTurns = 1;
            data.skills = new[] { skill };
            return data;
        }

        private InsectEntity Wild(InsectData data, int level, string guardianRegion = null)
        {
            InsectEntity entity = Track(new GameObject("EnvironmentWildFixture")).AddComponent<InsectEntity>();
            SetField(entity, "data", data);
            SetField(entity, "level", level);
            if (guardianRegion != null) SetField(entity, "guardianRegionId", guardianRegion);
            return entity;
        }

        private InsectBattleController NewController(Sky sky)
        {
            InsectBattleController controller =
                Track(new GameObject("EnvironmentBattleFixture")).AddComponent<InsectBattleController>();
            controller.SetCritSource(null);   // 능력치 보정만 재도록 치명타는 끈다
            if (sky != null) controller.AutoWire(sky.Provider, sky.Regions);
            return controller;
        }

        private Sky NewSky(DayPhase phase, WeatherType weather)
        {
            GameObject go = Track(new GameObject("EnvironmentSkyFixture"));
            var sky = new Sky
            {
                Clock = go.AddComponent<GameClock>(),
                Weather = go.AddComponent<WeatherSystem>(),
                Provider = go.AddComponent<WorldStateProvider>(),
                Regions = go.AddComponent<RegionManager>()
            };
            sky.Provider.AutoWire(sky.Clock, sky.Weather);
            sky.Set(phase, weather);
            return sky;
        }

        private sealed class Sky
        {
            public GameClock Clock;
            public WeatherSystem Weather;
            public WorldStateProvider Provider;
            public RegionManager Regions;

            public void Set(DayPhase phase, WeatherType weather)
            {
                Clock.SetTime01(HourOf(phase) / 24f, hold: true);
                Weather.SetWeather(weather, hold: true, instant: true);
            }
        }

        private static InsectBattleStats PlayerStats(InsectBattleController battle)
        {
            return (InsectBattleStats)typeof(InsectBattleController).GetField("playerStats", Inst).GetValue(battle);
        }

        private static InsectBattleStats EnemyStats(InsectBattleController battle)
        {
            return (InsectBattleStats)typeof(InsectBattleController).GetField("enemyStats", Inst).GetValue(battle);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, Inst);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{name} 필드가 없다 — 테스트가 낡았다");
            field.SetValue(target, value);
        }
    }
}
#endif
