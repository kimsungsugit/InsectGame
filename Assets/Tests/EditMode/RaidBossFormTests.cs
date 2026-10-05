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
    /// 체력에 따라 모습을 바꾸는 레이드 보스(<see cref="RaidBossForms"/> + <see cref="RaidBattleController"/>).
    ///
    /// 실패가 조용한 자리들: 래치가 없으면 회복할 때마다 모습이 되돌아가고, 정체(<c>Data</c>)를 갈아끼우면 이겨도 나비가
    /// 잡히고 최종장 비트가 사마귀를 못 알아보고, 상성을 정체로 재면 모습이 바뀌어도 약점이 그대로다(바뀐 건 그림뿐).
    /// 쓰러지는 일격에 변신이 끼면 시체가 나비로 일어난다.
    /// </summary>
    [TestFixture]
    public class RaidBossFormTests
    {
        private const string Boss = "mantis_unnamed";
        private const string Butterfly = "butterfly_swallowtail";
        private const string Firefly = "firefly_blue";

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        // ── 표(순수) ──

        [Test]
        public void Table_UnnamedMantis_BorrowsButterflyThenFirefly()
        {
            Assert.IsTrue(RaidBossForms.TryGetStages(Boss, out RaidBossForms.Stage[] stages));
            Assert.AreEqual(2, stages.Length);
            Assert.AreEqual(66, stages[0].HpPercent);
            Assert.AreEqual(Butterfly, stages[0].FormInsectId);
            Assert.AreEqual(33, stages[1].HpPercent);
            Assert.AreEqual(Firefly, stages[1].FormInsectId);
            Assert.AreEqual(Boss, RaidBossForms.FormInsectId(Boss, 0), "0번 모습은 자기 자신");
            Assert.IsFalse(RaidBossForms.HasForms("boss"), "표에 없는 보스는 변신하지 않는다");
        }

        [Test]
        public void Table_EveryStage_DescendsAndStaysInRange()
        {
            foreach (string bossId in RaidBossForms.BossIds)
            {
                RaidBossForms.TryGetStages(bossId, out RaidBossForms.Stage[] stages);
                int previous = 100;
                foreach (RaidBossForms.Stage s in stages)
                {
                    Assert.Less(s.HpPercent, previous, $"{bossId}: 임계가 내림차순이 아니다 — 뒤 단계가 먼저 열린다");
                    Assert.Greater(s.HpPercent, 0, $"{bossId}: 0% 임계는 쓰러진 뒤라 영영 안 열린다");
                    Assert.IsFalse(string.IsNullOrEmpty(s.LineFormat), $"{bossId}: 표시 한 줄이 비었다");
                    Assert.AreNotEqual(bossId, s.FormInsectId, $"{bossId}: 자기 모습으로 변신한다");
                    previous = s.HpPercent;
                }
            }
        }

        [TestCase(1000, 0)]
        [TestCase(661, 0)]
        [TestCase(660, 1)]
        [TestCase(331, 1)]
        [TestCase(330, 2)]
        [TestCase(1, 2)]
        public void FormIndexFor_UsesInclusiveThresholds(int hp, int expected)
        {
            Assert.AreEqual(expected, RaidBossForms.FormIndexFor(Boss, hp, 1000));
        }

        [Test]
        public void NextFormIndex_LatchesAndIgnoresAKillingBlow()
        {
            Assert.AreEqual(1, RaidBossForms.NextFormIndex(Boss, 1, 1000, 1000), "회복해도 돌아가지 않는다");
            Assert.AreEqual(2, RaidBossForms.NextFormIndex(Boss, 0, 300, 1000), "한 번에 둘을 넘으면 마지막 모습");
            Assert.AreEqual(0, RaidBossForms.NextFormIndex(Boss, 0, 0, 1000), "쓰러졌으면 변신하지 않는다");
            Assert.AreEqual(0, RaidBossForms.NextFormIndex("boss", 0, 1, 1000));
        }

        [Test]
        public void BuildLine_NamesTheBorrowedForm()
        {
            Assert.AreEqual("그림자가 호랑나비의 모습을 빌렸다!", RaidBossForms.BuildLine(Boss, 1, "호랑나비"));
            Assert.AreEqual("그림자가 파란반딧불이의 모습을 빌렸다!", RaidBossForms.BuildLine(Boss, 2, "파란반딧불이"));
            Assert.AreEqual(string.Empty, RaidBossForms.BuildLine(Boss, 0, "x"));
        }

        [Test]
        public void RealDatabase_HasEveryBossAndForm()
        {
            InsectDatabase db = RealDatabase();
            foreach (string bossId in RaidBossForms.BossIds)
            {
                Assert.IsNotNull(db.GetById(bossId), $"{bossId}: 보스가 곤충 DB에 없다");
                RaidBossForms.TryGetStages(bossId, out RaidBossForms.Stage[] stages);
                foreach (RaidBossForms.Stage s in stages)
                {
                    InsectData form = db.GetById(s.FormInsectId);
                    Assert.IsNotNull(form, $"{bossId}: 모습 {s.FormInsectId}가 곤충 DB에 없다 — 그 단계에서 변신이 조용히 빠진다");
                    Assert.IsFalse(string.IsNullOrEmpty(form.displayName), $"{s.FormInsectId}: 표시명이 없다");
                }
            }
        }

        // ── 컨트롤러 ──

        [Test]
        public void CrossingEachThreshold_ChangesFormOnce_AndLatches()
        {
            RaidBattleController raid = Raid(out List<RaidBossFormChange> changes);
            Assert.AreEqual(3, raid.BossFormCount);
            Assert.AreEqual(0, raid.BossFormIndex);

            DamageTo(raid, 65);
            ActOnce(raid);
            Assert.AreEqual(1, changes.Count, "66% 아래 — 한 번");
            Assert.AreEqual(1, raid.BossFormIndex);
            Assert.AreEqual(Butterfly, raid.BossFormData.insectId);

            ActOnce(raid);
            ActOnce(raid);
            Assert.AreEqual(1, changes.Count, "같은 구간에서 또 바뀌었다");

            DamageTo(raid, 32);
            ActOnce(raid);
            Assert.AreEqual(2, changes.Count, "33% 아래 — 한 번 더");
            Assert.AreEqual(2, raid.BossFormIndex);
            Assert.AreEqual(Firefly, raid.BossFormData.insectId);
            Assert.AreEqual(1, changes[1].FromIndex);
            Assert.AreEqual(2, changes[1].ToIndex);
            Assert.IsFalse(changes[1].SkippedStages);

            raid.BossStats.Heal(raid.BossStats.MaxHp);
            ActOnce(raid);
            Assert.AreEqual(2, raid.BossFormIndex, "회복했다고 모습이 돌아갔다");
            Assert.AreEqual(2, changes.Count);
        }

        [Test]
        public void FormChange_SwapsElementsAndSkills_KeepsIdentityHpLevelAndEnrage()
        {
            RaidBattleController raid = Raid(out List<RaidBossFormChange> changes);
            Assert.AreEqual("sig_mantis", raid.NextBossIntent.Skill.skillId, "전제: 원래 모습은 사마귀 전용기를 예고한다");

            DamageTo(raid, 40);
            RunRound(raid);                    // 이 라운드의 첫 행동이 변신을 부르고, 다음 라운드 시작이 격노를 켠다
            Assert.AreEqual(1, raid.BossFormIndex);
            Assert.IsTrue(raid.BossEnraged, "전제: 40%면 격노");
            int maxHp = raid.BossStats.MaxHp;
            int level = raid.BossStats.Level;

            Assert.AreEqual(Boss, raid.BossStats.Data.insectId, "정체가 바뀌었다 — 이기면 나비가 잡히고 최종장 비트가 사마귀를 못 알아본다");
            Assert.AreEqual(Butterfly, raid.BossStats.CombatData.insectId);
            Assert.AreEqual(InsectElement.Wind, raid.BossStats.CombatData.primaryType, "속성이 모습을 따라가지 않았다");
            Assert.AreEqual("sig_butterfly", raid.NextBossIntent.Skill.skillId, "기술이 모습을 따라가지 않았다");
            Assert.AreEqual(InsectElement.Wind, raid.NextBossIntent.Element);

            DamageTo(raid, 30);
            ActOnce(raid);
            Assert.AreEqual(2, raid.BossFormIndex);
            Assert.AreEqual(maxHp, raid.BossStats.MaxHp, "최대 HP가 바뀌었다");
            Assert.AreEqual(level, raid.BossStats.Level, "레벨이 바뀌었다");
            Assert.IsTrue(raid.BossEnraged, "변신이 격노를 풀었다");
            // 반딧불이 모습엔 전용기가 없다 — 그 모습이 아는 가장 센 피해기를 쓴다.
            Assert.AreEqual("glow_strong", raid.NextBossIntent.Skill.skillId, "전용기 없는 모습은 가장 센 피해기를 써야 한다");
            Assert.AreSame(raid.NextBossIntent, raid.CurrentRoundResult.BossIntent, "예고와 실행이 같은 객체여야 한다");

            // 이번 라운드 보스의 응답이 바뀐 기술로 나간다.
            while (raid.CanSubmitTeamCommand) raid.ResolveTeamCommand(0);
            RaidRoundResult round = raid.ResolveBossResponse();
            Assert.IsNotNull(round);
            Assert.AreEqual("glow_strong", round.BossAction.Skill.skillId);
        }

        [Test]
        public void FormChange_MovesTheTeamsTypeMatchup()
        {
            // 상성은 정체가 아니라 지금 모습으로 잰다 — 같은 공격이 모습에 따라 다른 피해를 낸다.
            InsectData mantis = Data(Boss, 40000, 20, 60, InsectElement.Leaf);
            InsectData butterfly = Data(Butterfly, 100, 10, 10, InsectElement.Wind);
            InsectElement attackElement = InsectElement.None;
            foreach (InsectElement e in System.Enum.GetValues(typeof(InsectElement)))
            {
                if (InsectTypeChart.GetEffectiveness(e, InsectElement.Leaf, InsectElement.None)
                    != InsectTypeChart.GetEffectiveness(e, InsectElement.Wind, InsectElement.None)) { attackElement = e; break; }
            }
            Assert.AreNotEqual(InsectElement.None, attackElement, "전제: 풀과 바람에 상성이 다른 속성이 있어야 한다");

            var asMantis = new RaidBossStats(mantis, 10, 100000, 100, 100);
            var asButterfly = new RaidBossStats(mantis, 10, 100000, 100, 100);
            asButterfly.ChangeForm(butterfly);
            InsectBattleStats attacker = new InsectBattleStats(Data("ally", 4000, 50, 50, InsectElement.None), 10);
            InsectSkill hit = Skill("hit", SkillEffectType.Damage, 60, attackElement);

            int vsMantis = RaidRoundResolver.ResolveLeaderSkill(0, 0, attacker, asMantis, new[] { attacker }, hit, null).Damage;
            int vsButterfly = RaidRoundResolver.ResolveLeaderSkill(0, 0, attacker, asButterfly, new[] { attacker }, hit, null).Damage;
            float effMantis = InsectTypeChart.GetEffectiveness(attackElement, InsectElement.Leaf, InsectElement.None);
            float effButterfly = InsectTypeChart.GetEffectiveness(attackElement, InsectElement.Wind, InsectElement.None);
            Assert.AreEqual(effButterfly > effMantis, vsButterfly > vsMantis,
                $"{attackElement} 공격 — 사마귀 {vsMantis}, 나비 모습 {vsButterfly}: 모습이 바뀌어도 약점이 그대로다");
            Assert.AreNotEqual(vsMantis, vsButterfly);
        }

        [Test]
        public void OneHitAcrossBothThresholds_FiresOneEventToTheLastForm()
        {
            RaidBattleController raid = Raid(out List<RaidBossFormChange> changes);
            DamageTo(raid, 20);
            ActOnce(raid);

            Assert.AreEqual(1, changes.Count, "임계 둘을 한 번에 넘으면 이벤트는 한 번");
            Assert.AreEqual(0, changes[0].FromIndex);
            Assert.AreEqual(2, changes[0].ToIndex);
            Assert.IsTrue(changes[0].SkippedStages);
            Assert.AreEqual(Firefly, changes[0].ToData.insectId);
            Assert.AreEqual(Boss, changes[0].FromData.insectId);
            Assert.AreEqual("그림자가 Blue Firefly의 모습을 빌렸다!", changes[0].Line);
            Assert.AreEqual(changes[0].Line, raid.BossFormLine);
        }

        [Test]
        public void FormChange_FiresBeforeTheMemberActionEvent()
        {
            // 행동을 받은 화면이 이미 바뀐 모습을 읽고, 그 공격 연출이 끝난 뒤 변신 단계를 끼울 수 있게 — 순서를 고정한다.
            RaidBattleController raid = Raid(out _);
            var order = new List<string>();
            raid.BossFormChanged += c => order.Add("form");
            raid.RaidMemberActionResolved += a => order.Add("action");
            DamageTo(raid, 50);
            ActOnce(raid);
            CollectionAssert.AreEqual(new[] { "form", "action" }, order);
        }

        [Test]
        public void KillingBlow_DoesNotTransform()
        {
            RaidBattleController raid = Raid(out List<RaidBossFormChange> changes, allyPower: 5000000);
            ActOnce(raid);   // 한 방에 쓰러진다 — 임계를 다 지나쳤어도 시체는 일어나지 않는다

            Assert.AreEqual(0, raid.BossStats.CurrentHp, "전제: 보스가 쓰러져야 한다");
            Assert.AreEqual(0, changes.Count);
            Assert.AreEqual(0, raid.BossFormIndex);
            Assert.AreEqual(Boss, raid.BossFormData.insectId);
        }

        [Test]
        public void BossWithoutForms_IsUntouched()
        {
            RaidBattleController raid = Raid(out List<RaidBossFormChange> changes, bossId: "boss");
            Assert.AreEqual(1, raid.BossFormCount);
            DamageTo(raid, 10);
            RunRound(raid);
            RunRound(raid);

            Assert.AreEqual(0, changes.Count);
            Assert.AreEqual(0, raid.BossFormIndex);
            Assert.AreSame(raid.BossStats.Data, raid.BossStats.CombatData);
            Assert.AreEqual("sig_mantis", raid.NextBossIntent.Skill.skillId, "변신 없는 보스의 기술 고르기가 바뀌었다");
            Assert.AreEqual(string.Empty, raid.BossFormLine);
        }

        [Test]
        public void MissingFormData_StaysInTheOriginalForm()
        {
            // DB에서 모습 곤충을 못 찾으면 바꾸지 않는다(경고 한 번) — 반쯤 바뀐 상태(번호만 오르고 속성은 그대로)가 되지 않게.
            RaidBattleController raid = Raid(out List<RaidBossFormChange> changes);
            raid.SetInsectLookup(id => null);
            DamageTo(raid, 50);

            // LogAssert(UnityEngine.TestTools)는 이 어셈블리에서 참조가 안 된다(asmdef 없음) — 로그 이벤트로 직접 센다.
            int warnings = 0;
            Application.LogCallback count = (message, stack, type) =>
            {
                if (type == LogType.Warning && message.StartsWith("[Raid]")) warnings++;
            };
            Application.logMessageReceived += count;
            try
            {
                ActOnce(raid);
                ActOnce(raid);
            }
            finally
            {
                Application.logMessageReceived -= count;
            }

            Assert.AreEqual(1, warnings, "모습을 못 찾았다는 경고는 한 번만 — 행동마다 찍으면 로그가 도배된다");
            Assert.AreEqual(0, changes.Count);
            Assert.AreEqual(0, raid.BossFormIndex);
            Assert.AreSame(raid.BossStats.Data, raid.BossStats.CombatData);
        }

        // ── 도우미 ──

        private InsectData mantisData;

        private RaidBattleController Raid(out List<RaidBossFormChange> changes, string bossId = Boss, int allyPower = 1)
        {
            mantisData = Data(bossId, 40000, 20, 60, InsectElement.Leaf);
            mantisData.learnset = new[] { Learn(Skill("sig_mantis", SkillEffectType.Damage, 4, InsectElement.Leaf, signature: true), 1) };

            InsectData butterfly = Data(Butterfly, 100, 10, 10, InsectElement.Wind);
            butterfly.displayName = "Swallowtail";
            butterfly.learnset = new[]
            {
                Learn(Skill("flutter", SkillEffectType.Damage, 30, InsectElement.Wind), 1),
                Learn(Skill("sig_butterfly", SkillEffectType.Damage, 5, InsectElement.Wind, signature: true), 1),
            };
            InsectData firefly = Data(Firefly, 100, 10, 10, InsectElement.Light);
            firefly.displayName = "Blue Firefly";
            firefly.learnset = new[]
            {
                Learn(Skill("glow_weak", SkillEffectType.Damage, 20, InsectElement.Light), 1),
                Learn(Skill("glow_strong", SkillEffectType.Damage, 50, InsectElement.Light), 1),
                Learn(Skill("glow_heal", SkillEffectType.Heal, 90, InsectElement.Light), 1),
                Learn(Skill("glow_late", SkillEffectType.Damage, 99, InsectElement.Light), 50),   // 아직 못 배웠다(레벨 10)
            };
            var forms = new Dictionary<string, InsectData> { [Butterfly] = butterfly, [Firefly] = firefly };

            GameObject bossObject = Track(new GameObject("FormTestBoss"));
            InsectEntity bossEntity = bossObject.AddComponent<InsectEntity>();
            SetField(bossEntity, "data", mantisData);
            SetField(bossEntity, "level", 10);

            GameObject controllerObject = Track(new GameObject("FormTestController"));
            RaidBattleController controller = controllerObject.AddComponent<RaidBattleController>();
            controller.SetRandomSource(new LowestSlotRandomSource());
            controller.SetCritSource(null);
            controller.SetInsectLookup(id => forms.TryGetValue(id, out InsectData d) ? d : null);
            var list = new List<RaidBossFormChange>();
            controller.BossFormChanged += c => list.Add(c);
            changes = list;

            InsectData[] team = new InsectData[5];
            int[] levels = new int[5];
            InsectSkill[][] skills = new InsectSkill[5][];
            InsectSkill poke = Skill("poke", SkillEffectType.Damage, allyPower, InsectElement.Bug);
            for (int i = 0; i < 5; i++)
            {
                team[i] = Data($"ally_{i}", 4000, 20, 200, InsectElement.Bug);
                levels[i] = 10;
                skills[i] = new[] { poke };
            }

            Assert.IsTrue(controller.StartRaid(bossEntity, team, levels, null, skills));
            return controller;
        }

        // HP를 최대의 percent%로 맞춘다(비율 보정 없는 ApplyDamage) — 변신은 다음 팀 행동이 부른다.
        private static void DamageTo(RaidBattleController raid, int percent)
        {
            int target = raid.BossStats.MaxHp * percent / 100;
            int cut = raid.BossStats.CurrentHp - target;
            if (cut > 0) raid.BossStats.ApplyDamage(cut);
        }

        // 팀원 한 마리가 행동한다. 팀 턴이 닫혀 있으면 보스 응답·라운드 마무리를 먼저 돌린다.
        private static void ActOnce(RaidBattleController raid)
        {
            if (!raid.CanSubmitTeamCommand)
            {
                raid.ResolveBossResponse();
                raid.CompleteRoundPresentation();
            }
            Assert.IsTrue(raid.CanSubmitTeamCommand, "팀이 행동할 차례가 와야 한다");
            Assert.IsNotNull(raid.ResolveTeamCommand(0));
        }

        private static void RunRound(RaidBattleController raid)
        {
            if (!raid.CanSubmitTeamCommand)
            {
                raid.ResolveBossResponse();
                raid.CompleteRoundPresentation();
            }
            int guard = raid.TeamStats.Length + 1;
            while (guard-- > 0 && raid.CanSubmitTeamCommand)
                Assert.IsNotNull(raid.ResolveTeamCommand(0));
            raid.ResolveBossResponse();
            Assert.IsTrue(raid.CompleteRoundPresentation());
        }

        private InsectData Data(string id, int hp, int attack, int defense, InsectElement element)
        {
            InsectData data = Track(ScriptableObject.CreateInstance<InsectData>());
            data.insectId = id;
            data.displayName = id;
            data.primaryType = element;
            data.secondaryType = InsectElement.None;
            data.baseHp = hp;
            data.baseAtk = attack;
            data.baseDef = defense;
            data.candyReward = 1;
            data.expReward = 1;
            return data;
        }

        private InsectSkill Skill(string id, SkillEffectType effectType, int power, InsectElement element,
            bool signature = false)
        {
            InsectSkill skill = Track(ScriptableObject.CreateInstance<InsectSkill>());
            skill.skillId = id;
            skill.displayName = id;
            skill.element = element;
            skill.effectType = effectType;
            skill.power = power;
            skill.cooldownTurns = 0;
            skill.accuracy = 1f;
            skill.effectValue = effectType == SkillEffectType.Heal ? 0.1f : 0f;
            skill.isSignatureSkill = signature;
            return skill;
        }

        private static InsectLearnableSkill Learn(InsectSkill skill, int level)
        {
            return new InsectLearnableSkill { skillId = skill.skillId, learnLevel = level, skill = skill };
        }

        private InsectDatabase RealDatabase()
        {
            var host = Track(new GameObject("RaidBossFormTests_RealDb"));
            host.SetActive(false);   // Awake(월드 생성)를 막고 private 생성 함수만 부른다
            var bootstrap = host.AddComponent<PlaySceneBootstrap>();
            var db = (InsectDatabase)typeof(PlaySceneBootstrap)
                .GetMethod("EnsureExpandedDatabase", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bootstrap, null);
            Assert.IsNotNull(db);
            Track(db);
            return db;
        }

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private sealed class LowestSlotRandomSource : IRaidRandomSource
        {
            public float Next01() => 0f;
            public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
        }
    }
}
#endif
