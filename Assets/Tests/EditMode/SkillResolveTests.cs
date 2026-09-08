#if UNITY_EDITOR
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// <b>훈련·기술 디스크로 배운 기술이 전투에 실제로 나오는가.</b>
    ///
    /// 회귀 고정: <c>PlayerInsectCollection.ResolveSkill</c>은 곤충의 <c>learnset</c>과
    /// <c>skills</c>만 뒤졌는데 그 둘은 같은 집합이다(<c>skills = ExtractUniqueSkills(learnset)</c>).
    /// 그래서 <c>tr_*</c> 범용기는 어느 종의 learnset에도 없어 <b>항상 null</b>이 됐고,
    /// 전투 슬롯이 빈칸이 되어 <c>CanUseSkill</c>이 false를 냈다 — <b>배운 기술이 전투에
    /// 아예 안 나왔다.</b> 훈련 화면은 <c>TrainingManager</c>의 자체 lookup을 쓰므로 멀쩡히
    /// 장착돼 보였고, 그래서 증상이 조용했다.
    /// </summary>
    [TestFixture]
    public class SkillResolveTests
    {
        private GameObject host;
        private PlayerInsectCollection collection;
        private InsectData species;
        private InsectSkill nativeSkill;
        private InsectSkill trainedSkill;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("SkillResolveTests");
            collection = host.AddComponent<PlayerInsectCollection>();

            nativeSkill = MakeSkill("metal_jab", 12);
            trainedSkill = MakeSkill("tr_charge", 20);

            species = ScriptableObject.CreateInstance<InsectData>();
            species.insectId = "test_beetle";
            species.learnset = new[]
            {
                new InsectLearnableSkill { skillId = nativeSkill.skillId, learnLevel = 1, skill = nativeSkill }
            };
            species.skills = new[] { nativeSkill };
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (species != null) Object.DestroyImmediate(species);
            if (nativeSkill != null) Object.DestroyImmediate(nativeSkill);
            if (trainedSkill != null) Object.DestroyImmediate(trainedSkill);
        }

        private static InsectSkill MakeSkill(string id, int power)
        {
            InsectSkill s = ScriptableObject.CreateInstance<InsectSkill>();
            s.skillId = id;
            s.displayName = id;
            s.power = power;
            return s;
        }

        [Test]
        public void ResolveSkill_SpeciesSkill_ResolvesWithoutRegistry()
        {
            Assert.AreSame(nativeSkill, collection.ResolveSkill(species, "metal_jab"),
                "종족 learnset의 기술은 레지스트리 없이도 풀려야 한다");
        }

        [Test]
        public void ResolveSkill_NoSpeciesData_StillResolvesFromRegistry()
        {
            // 구 ID·DB 미등록 개체 — 종 데이터가 null이어도 배운 범용기는 나와야 기본 공격만 하지 않는다.
            collection.AutoWire(new[] { trainedSkill });

            Assert.AreSame(trainedSkill, collection.ResolveSkill(null, "tr_charge"));
            Assert.IsNull(collection.ResolveSkill(null, ""));
        }

        [Test]
        public void ResolveSkill_TrainedSkill_WithoutRegistry_IsNull()
        {
            // 이 null이 바로 결함의 정체였다 — 여기가 null이면 전투 슬롯이 빈칸이 된다.
            Assert.IsNull(collection.ResolveSkill(species, "tr_charge"),
                "레지스트리가 없으면 종족 밖 기술은 못 찾는다(이 상태가 옛 동작이다)");
        }

        [Test]
        public void ResolveSkill_TrainedSkill_WithRegistry_Resolves()
        {
            collection.AutoWire(new[] { nativeSkill, trainedSkill });

            Assert.AreSame(trainedSkill, collection.ResolveSkill(species, "tr_charge"),
                "훈련·디스크로 배운 기술이 전투에서 풀리지 않으면 슬롯이 빈칸이 된다");
        }

        [Test]
        public void ResolveSkill_SpeciesInstanceWins_OverRegistry()
        {
            // 전용기는 종별 인스턴스다. 같은 id가 레지스트리에도 있으면 **종족 쪽이 이겨야** 한다.
            InsectSkill impostor = MakeSkill("metal_jab", 999);
            try
            {
                collection.AutoWire(new[] { impostor });
                Assert.AreSame(nativeSkill, collection.ResolveSkill(species, "metal_jab"),
                    "레지스트리가 종족 인스턴스를 밀어내면 전용기가 엉뚱한 것으로 바뀐다");
            }
            finally
            {
                Object.DestroyImmediate(impostor);
            }
        }

        [Test]
        public void FindSkill_UnknownId_IsNull()
        {
            collection.AutoWire(new[] { trainedSkill });
            Assert.IsNull(collection.FindSkill("no_such_skill"));
            Assert.IsNull(collection.FindSkill(null));
        }
    }
}
#endif
