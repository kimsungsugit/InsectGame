#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Spawning;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 필드 개체군 기록부(<see cref="FieldPopulation"/>) — <b>기록이 곧 개체</b>라는 약속을 고정한다.
    ///
    /// 사라지는 방식이 둘이다: ①게임플레이 퇴장(포획·전투·도주) — 자리가 비고 재생 지연 뒤에 찬다.
    /// ②스포너가 거둠(멀어짐·서브에리어 전환) — 몸만 풀로 가고 개체는 남는다. 둘이 섞이면 "리전을 오가면 새 곤충"
    /// (옛 리롤)으로 돌아가거나 잡은 곤충이 영영 안 사라진다. 몸(<see cref="InsectEntity"/>)은 빈 GameObject에 붙여
    /// 세우기만 하고 모델은 짓지 않는다(Update는 프레임이 안 돌아 호출되지 않는다).
    /// </summary>
    [TestFixture]
    public class FieldPopulationTests
    {
        private readonly List<GameObject> bodies = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in bodies)
                if (go != null) Object.DestroyImmediate(go);
            bodies.Clear();
        }

        private InsectEntity Body()
        {
            var go = new GameObject("TestInsectBody");
            bodies.Add(go);
            return go.AddComponent<InsectEntity>();
        }

        private static FieldSlot FilledSlot(FieldPopulation pop, string key, float now, float expiresAt = 300f)
        {
            pop.EnsureSlotCount(key, 1, now, isSubArea: false);
            FieldSlot slot = pop.SlotsOf(key)[0];
            FieldPopulation.Fill(slot, "stag_beetle", null, 17, true, false, new Vector3(3f, 0.1f, 4f), expiresAt);
            return slot;
        }

        // ── 거둠 vs 퇴장 ──

        [Test]
        public void RelocateAfterFlee_KeepsIndividual_MovesItAndDropsTheBody()
        {
            // 도주는 개체가 죽은 게 아니라 달아난 것 — 자리를 비우면 멈춰 선 플레이어 둘레가 1~2분씩 빈 들판이 된다.
            var pop = new FieldPopulation();
            FieldSlot slot = FilledSlot(pop, "meadow", 0f);
            slot.Entity = Body();
            Vector3 away = new Vector3(40f, 0.1f, -25f);

            FieldPopulation.RelocateAfterFlee(slot, away);

            Assert.IsTrue(slot.IsAlive, "달아난 곤충의 자리가 비었다");
            Assert.IsFalse(slot.IsMaterialized, "몸이 그대로 붙어 있다");
            Assert.AreEqual("stag_beetle", slot.InsectId);
            Assert.AreEqual(17, slot.Level);
            Assert.IsTrue(slot.Shiny);
            Assert.AreEqual(away, slot.Position);
        }

        [Test]
        public void RelocateAfterFlee_OnEmptySlot_DoesNothing()
        {
            var pop = new FieldPopulation();
            pop.EnsureSlotCount("meadow", 1, 0f, isSubArea: false);
            FieldSlot slot = pop.SlotsOf("meadow")[0];
            Vector3 before = slot.Position;

            FieldPopulation.RelocateAfterFlee(slot, new Vector3(9f, 0f, 9f));

            Assert.IsFalse(slot.IsAlive, "빈 자리가 도주 이동으로 살아났다");
            Assert.AreEqual(before, slot.Position);
        }

        [Test]
        public void MarkRecalled_KeepsTheSameIndividual()
        {
            var pop = new FieldPopulation();
            FieldSlot slot = FilledSlot(pop, "forest", 0f);
            slot.Entity = Body();

            FieldPopulation.MarkRecalled(slot);

            Assert.IsFalse(slot.IsMaterialized, "몸이 떨어지지 않았다");
            Assert.IsTrue(slot.IsAlive, "멀어졌다고 자리가 비면 돌아올 때 새 곤충이다(옛 리롤)");
            Assert.AreEqual("stag_beetle", slot.InsectId);
            Assert.AreEqual(17, slot.Level);
            Assert.IsTrue(slot.Shiny, "색다름도 기록이다 — 다시 세울 때 다시 굴리지 않는다");
            Assert.AreEqual(new Vector3(3f, 0.1f, 4f), slot.Position);
            Assert.IsFalse(FieldPopulation.IsDueForRoll(slot, 10000f), "산 자리는 다시 굴리지 않는다");
        }

        [Test]
        public void MarkRemovedByGameplay_RefillsOnlyAfterTheDelay()
        {
            var pop = new FieldPopulation();
            FieldSlot slot = FilledSlot(pop, "pond", 0f);
            slot.Entity = Body();

            FieldPopulation.MarkRemovedByGameplay(slot, 100f, 90f);

            Assert.IsFalse(slot.IsAlive);
            Assert.IsFalse(slot.IsMaterialized);
            Assert.IsNull(slot.InsectId, "잡힌 개체의 기록이 남아 있다");
            Assert.IsFalse(FieldPopulation.IsDueForRoll(slot, 189.9f), "지연 전에 다시 찼다");
            Assert.IsTrue(FieldPopulation.IsDueForRoll(slot, 190f), "지연이 지나도 안 찬다");
        }

        [Test]
        public void Fill_RaisesGenerationEachNewIndividual()
        {
            var pop = new FieldPopulation();
            FieldSlot slot = FilledSlot(pop, "meadow", 0f);
            Assert.AreEqual(0, slot.Generation, "첫 개체는 0세대");
            FieldPopulation.MarkRemovedByGameplay(slot, 0f, 0f);
            FieldPopulation.Fill(slot, "ant", null, 2, false, false, Vector3.zero, 999f);
            Assert.AreEqual(1, slot.Generation);
        }

        // ── 수명 순환 ──

        [Test]
        public void CanRotate_ExpiredWithoutBody_True_NotExpired_False()
        {
            var pop = new FieldPopulation();
            FieldSlot slot = FilledSlot(pop, "swamp", 0f, expiresAt: 300f);
            Assert.IsFalse(FieldPopulation.CanRotate(slot, 299f, float.PositiveInfinity, false), "수명 전에 바뀐다");
            Assert.IsTrue(FieldPopulation.CanRotate(slot, 300f, 1f, false),
                "몸이 없으면(멀리 있으면) 플레이어 거리와 무관하게 바꾼다");
        }

        [Test]
        public void CanRotate_WithBodyNearPlayer_Waits()
        {
            var pop = new FieldPopulation();
            FieldSlot slot = FilledSlot(pop, "swamp", 0f, expiresAt: 300f);
            slot.Entity = Body();
            float near = FieldSpawnRules.RotateMinPlayerDistance - 1f;
            float far = FieldSpawnRules.RotateMinPlayerDistance + 1f;

            Assert.IsFalse(FieldPopulation.CanRotate(slot, 400f, near, false), "플레이어 눈앞에서 딴 곤충으로 바뀐다");
            Assert.IsTrue(FieldPopulation.CanRotate(slot, 400f, far, false));
            Assert.IsFalse(FieldPopulation.CanRotate(slot, 400f, far, true), "포획·경계·도주 중인 몸을 바꿨다");
        }

        // ── 실체화 히스테리시스 ──

        [Test]
        public void MaterializeAndRecall_UseSeparatedRadii()
        {
            var pop = new FieldPopulation();
            FieldSlot slot = FilledSlot(pop, "ruins", 0f);
            float between = (FieldSpawnRules.MaterializeRadius + FieldSpawnRules.RecallRadius) * 0.5f;

            Assert.IsTrue(FieldPopulation.ShouldMaterialize(slot, FieldSpawnRules.MaterializeRadius));
            Assert.IsFalse(FieldPopulation.ShouldMaterialize(slot, between), "경계 사이에서 세웠다");

            slot.Entity = Body();
            Assert.IsFalse(FieldPopulation.ShouldMaterialize(slot, 1f), "이미 선 몸을 또 세운다");
            Assert.IsFalse(FieldPopulation.ShouldRecall(slot, between, false), "경계 사이에서 거뒀다 — 서성이면 깜빡인다");
            Assert.IsTrue(FieldPopulation.ShouldRecall(slot, FieldSpawnRules.RecallRadius + 0.1f, false));
            Assert.IsFalse(FieldPopulation.ShouldRecall(slot, 999f, true), "전투 중인 몸을 거뒀다");
        }

        // ── 슬롯 수(오염 상한) ──

        [Test]
        public void EnsureSlotCount_Grow_AddsEmptySlotsDueNow()
        {
            var pop = new FieldPopulation();
            Assert.AreEqual(3, pop.EnsureSlotCount("forest", 3, 12f, isSubArea: false));
            foreach (FieldSlot s in pop.SlotsOf("forest"))
            {
                Assert.IsFalse(s.IsAlive);
                Assert.IsTrue(FieldPopulation.IsDueForRoll(s, 12f), "늘어난 자리를 바로 채우지 않는다(정화 직후 빈 들판)");
            }
        }

        [Test]
        public void EnsureSlotCount_Shrink_RemovesEmptyThenBodilessButNeverAStandingBody()
        {
            var pop = new FieldPopulation();
            pop.EnsureSlotCount("mountain", 4, 0f, isSubArea: false);
            List<FieldSlot> slots = pop.SlotsOf("mountain");
            FieldPopulation.Fill(slots[0], "a", null, 1, false, false, Vector3.zero, 999f);
            FieldPopulation.Fill(slots[1], "b", null, 1, false, false, Vector3.zero, 999f);
            FieldPopulation.Fill(slots[2], "c", null, 1, false, false, Vector3.zero, 999f);
            slots[0].Entity = Body();
            slots[1].Entity = Body();
            // slots[3]은 비었다

            Assert.AreEqual(-2, pop.EnsureSlotCount("mountain", 2, 0f, isSubArea: false));
            Assert.AreEqual(2, slots.Count);
            Assert.IsTrue(slots.TrueForAll(s => s.IsMaterialized), "눈앞에 선 곤충을 뺐다");

            // 몸이 선 자리는 이번엔 못 뺀다 — 거둔 뒤 다음 호출에서 빠진다.
            Assert.AreEqual(0, pop.EnsureSlotCount("mountain", 1, 0f, isSubArea: false));
            FieldPopulation.MarkRecalled(slots[1]);
            Assert.AreEqual(-1, pop.EnsureSlotCount("mountain", 1, 0f, isSubArea: false));
            Assert.AreEqual(1, slots.Count);
        }

        [Test]
        public void HasAliveAndCollectAliveIds_SeeRecordsNotBodies()
        {
            var pop = new FieldPopulation();
            FieldSlot slot = FilledSlot(pop, "pond", 0f);
            Assert.IsTrue(pop.HasAlive("pond", "stag_beetle"), "몸이 없어도 산 개체다");
            Assert.IsFalse(pop.HasAlive("forest", "stag_beetle"));
            var ids = new HashSet<string>();
            pop.CollectAliveIds("pond", ids);
            CollectionAssert.AreEquivalent(new[] { "stag_beetle" }, ids);

            FieldPopulation.MarkRemovedByGameplay(slot, 0f, 60f);
            Assert.IsFalse(pop.HasAlive("pond", "stag_beetle"));
        }

        // ── 서브에리어 재진입 ──

        /// <summary>
        /// 나갔다 다시 들어오면 <b>같은 개체가 같은 자리에</b> 있다. 옛 스포너는 들어올 때마다 새로 굴렸다.
        /// 잡힌 자리만 슬롯별 지연(서브에리어 45초) 뒤에 다음 차례 종으로 찬다.
        /// </summary>
        [Test]
        public void SubArea_ReEntry_KeepsIndividuals_CaughtSlotRefillsWithNextSpecies()
        {
            string[] ids = { "centipede_common", "earwig_common", "pill_bug_garden" };
            var pop = new FieldPopulation();
            int count = FieldSpawnRules.SubAreaSlotCount(ids.Length, 2);
            pop.EnsureSlotCount("sub:meadow_cave", count, 0f, isSubArea: true);
            List<FieldSlot> slots = pop.SlotsOf("sub:meadow_cave");
            for (int i = 0; i < slots.Count; i++)
            {
                string id = FieldSpawnRules.SubAreaSpecies(ids, slots[i].Index, count, slots[i].Generation + 1);
                FieldPopulation.Fill(slots[i], id, null, 5 + i, false, false, new Vector3(2000f + i, 0f, 2000f),
                    float.PositiveInfinity);
                slots[i].Entity = Body();
                Assert.IsTrue(slots[i].IsSubArea);
            }
            Assert.AreEqual("centipede_common", slots[0].InsectId);
            Assert.AreEqual("earwig_common", slots[1].InsectId);

            // 나간다 — 몸만 거둔다.
            foreach (FieldSlot s in slots) FieldPopulation.MarkRecalled(s);
            // 한참 뒤 다시 들어온다 — 새로 굴릴 자리가 없다(같은 개체).
            foreach (FieldSlot s in slots)
            {
                Assert.IsTrue(s.IsAlive);
                Assert.IsFalse(FieldPopulation.IsDueForRoll(s, 10000f));
                Assert.IsFalse(FieldPopulation.IsExpired(s, 10000f), "서브에리어 전용종은 수명이 없다");
            }
            Assert.AreEqual("earwig_common", slots[1].InsectId);
            Assert.AreEqual(6, slots[1].Level);
            Assert.AreEqual(new Vector3(2001f, 0f, 2000f), slots[1].Position);

            // 첫 자리의 곤충을 잡는다 — 45초 뒤 다음 차례(세 번째 전용종)로 찬다.
            slots[0].Entity = Body();
            FieldPopulation.MarkRemovedByGameplay(slots[0], 20000f, 45f);
            Assert.IsFalse(FieldPopulation.IsDueForRoll(slots[0], 20044f));
            Assert.IsTrue(FieldPopulation.IsDueForRoll(slots[0], 20045f));
            Assert.AreEqual("pill_bug_garden",
                FieldSpawnRules.SubAreaSpecies(ids, slots[0].Index, count, slots[0].Generation + 1),
                "옛 규칙에선 세 번째 전용종이 영영 안 나왔다");
        }

        // ── 몸 — 기록 그대로 세우기 · 조용히 거두기 ──

        [Test]
        public void Entity_InitializeRecorded_UsesGivenShinyAndErased_AndBumpsSerial()
        {
            InsectEntity body = Body();
            body.Initialize(null, 9, "hollow", null, true, true);
            int first = body.SpawnSerial;
            Assert.IsTrue(body.IsShiny);
            Assert.IsTrue(body.IsErased);
            Assert.AreEqual("hollow", body.RegionId);

            body.Initialize(null, 9, "hollow", null, false, false);
            Assert.IsFalse(body.IsShiny, "기록된 값 대신 다시 굴렸다");
            Assert.AreNotEqual(first, body.SpawnSerial, "다시 지은 몸이 같은 번호다 — 지도 레이드 표식이 옛 개체로 붙는다");
        }

        [Test]
        public void Entity_Recall_DoesNotReportGameplayRemoval_AndLatchesLateDespawn()
        {
            int despawnCalls = 0;
            InsectEntity body = Body();
            body.Initialize(null, 3, "meadow", _ => despawnCalls++, false, false);

            Assert.IsTrue(body.Recall());
            Assert.AreEqual(0, despawnCalls, "거리 회수가 '잡혔다'로 보고됐다 — 자리가 비어 돌아오면 새 곤충이 된다");
            body.Despawn();   // 참조를 쥔 쪽(아이 NPC 등)의 뒤늦은 호출
            Assert.AreEqual(0, despawnCalls, "거둔 몸의 뒤늦은 Despawn이 자리를 비웠다");
            Assert.IsFalse(body.Recall(), "두 번 거둬 풀에 두 번 들어간다");

            body.Initialize(null, 3, "meadow", _ => despawnCalls++, false, false);
            body.Despawn();
            Assert.AreEqual(1, despawnCalls, "게임플레이 퇴장은 한 번 보고돼야 한다");
        }
    }
}
#endif
