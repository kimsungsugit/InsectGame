#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 조건부 퀘스트(<see cref="QuestType.CaptureTrait"/>·<see cref="QuestType.BattleFeat"/>) —
    /// 순수 판정, 실제 퀘스트 표의 불변식, 매니저를 실제로 굴린 진행, 실제 곤충 DB로 잰 달성 확률.
    ///
    /// 실패가 전부 조용한 계열이다: 조건이 안 먹으면 퀘스트가 그냥 안 차거나(너무 어렵다) 너무 쉽게 찬다.
    /// 예외도 경고도 없다.
    /// </summary>
    [TestFixture]
    public class QuestTraitTests
    {
        private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly List<Object> objects = new List<Object>();
        private readonly Dictionary<string, string> savedPrefs = new Dictionary<string, string>();

        private static readonly string[] PrefBaseKeys =
        {
            GameConstants.PrefsKeys.QuestProgress, GameConstants.PrefsKeys.QuestCompleted,
            GameConstants.PrefsKeys.ActiveQuest, GameConstants.PrefsKeys.QuestUnseen,
            GameConstants.PrefsKeys.QuestSideProgress, GameConstants.PrefsKeys.QuestSideRepeat,
        };

        [SetUp]
        public void SetUp()
        {
            // 매니저가 진행을 PlayerPrefs에 쓴다 — 이 PC의 진짜 퀘스트 진행을 덮지 않게 백업해 둔다.
            savedPrefs.Clear();
            foreach (string baseKey in PrefBaseKeys)
            {
                string key = AuthManager.ScopedKey(baseKey);
                savedPrefs[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;
            }
            DreamPrologueState.End();
        }

        [TearDown]
        public void TearDown()
        {
            DreamPrologueState.End();
            foreach (var kv in savedPrefs)
            {
                if (kv.Value == null) PlayerPrefs.DeleteKey(kv.Key);
                else PlayerPrefs.SetString(kv.Key, kv.Value);
            }
            PlayerPrefs.Save();
            foreach (Object o in objects) if (o != null) Object.DestroyImmediate(o);
            objects.Clear();
        }

        // ── 입력 만들기 ──

        private static CaptureFacts Cap(InsectRarity rarity = InsectRarity.Common, float mm = 30f, float ratio = 1f,
            InsectElement primary = InsectElement.Bug, InsectElement secondary = InsectElement.None,
            bool shiny = false, bool sizeKnown = true)
        {
            return new CaptureFacts
            {
                rarity = rarity, sizeMm = mm, sizeRatio = ratio, primary = primary, secondary = secondary,
                shiny = shiny, sizeKnown = sizeKnown,
            };
        }

        private static BattleFacts Win(InsectRarity rarity = InsectRarity.Common, int enemyLevel = 5, int playerLevel = 5,
            int actions = 4, float hp = 0.5f,
            InsectElement primary = InsectElement.Bug, InsectElement secondary = InsectElement.None)
        {
            return new BattleFacts
            {
                enemyRarity = rarity, enemyLevel = enemyLevel, playerLevel = playerLevel,
                playerActions = actions, playerHpRatio = hp, enemyPrimary = primary, enemySecondary = secondary,
            };
        }

        private static TutorialQuest CaptureQuest(float minMm = 0f, float maxMm = 0f, float minRatio = 0f, float maxRatio = 0f,
            InsectElement element = InsectElement.None, bool shiny = false, InsectRarity minRarity = InsectRarity.Common)
        {
            return new TutorialQuest
            {
                questId = "t_cap", type = QuestType.CaptureTrait, category = QuestCategory.Side,
                minSizeMm = minMm, maxSizeMm = maxMm, minSizeRatio = minRatio, maxSizeRatio = maxRatio,
                requiredElement = element, requireShiny = shiny, minRarity = minRarity,
            };
        }

        private static TutorialQuest BattleQuest(int levelEdge = 0, int maxTurns = 0, int hpPercent = 0,
            InsectElement element = InsectElement.None, InsectRarity minRarity = InsectRarity.Common)
        {
            return new TutorialQuest
            {
                questId = "t_bat", type = QuestType.BattleFeat, category = QuestCategory.Side,
                minLevelEdge = levelEdge, maxTurns = maxTurns, minHpPercent = hpPercent,
                requiredElement = element, minRarity = minRarity,
            };
        }

        // ── 포획 판정: 몸길이 ──

        [Test]
        public void CaptureSize_MinAndMax_AreInclusive()
        {
            TutorialQuest big = CaptureQuest(minMm: 40f);
            Assert.IsTrue(QuestTraitRules.Matches(big, Cap(mm: 40f)));
            Assert.IsTrue(QuestTraitRules.Matches(big, Cap(mm: 75f)));
            Assert.IsFalse(QuestTraitRules.Matches(big, Cap(mm: 39.9f)));

            TutorialQuest small = CaptureQuest(maxMm: 20f);
            Assert.IsTrue(QuestTraitRules.Matches(small, Cap(mm: 20f)));
            Assert.IsFalse(QuestTraitRules.Matches(small, Cap(mm: 20.1f)));
        }

        [Test]
        public void CaptureSize_IsJudgedByTheRoundedValueThePlayerSees()
        {
            // 화면은 소수 첫째 자리다(SizeLabel). 39.96은 "40.0mm"로 보이는데 40 이상 퀘스트에 안 세어지면 버그로 느낀다.
            Assert.AreEqual("40.0mm", InsectSizeCalculator.SizeLabel(39.96f));
            Assert.IsTrue(QuestTraitRules.Matches(CaptureQuest(minMm: 40f), Cap(mm: 39.96f)));
            Assert.IsTrue(QuestTraitRules.Matches(CaptureQuest(maxMm: 20f), Cap(mm: 20.04f)),
                "20.04는 20.0mm로 보인다");
            Assert.IsFalse(QuestTraitRules.Matches(CaptureQuest(minMm: 40f), Cap(mm: 39.94f)), "39.9mm로 보인다");
        }

        [Test]
        public void CaptureSize_Band_NeedsBothBounds()
        {
            TutorialQuest band = CaptureQuest(minMm: 25f, maxMm: 30f);
            Assert.IsTrue(QuestTraitRules.Matches(band, Cap(mm: 25f)));
            Assert.IsTrue(QuestTraitRules.Matches(band, Cap(mm: 27.5f)));
            Assert.IsTrue(QuestTraitRules.Matches(band, Cap(mm: 30f)));
            Assert.IsFalse(QuestTraitRules.Matches(band, Cap(mm: 24.9f)));
            Assert.IsFalse(QuestTraitRules.Matches(band, Cap(mm: 30.1f)));
        }

        [Test]
        public void CaptureRatio_BoundaryRolls_AreNotLostToFloatNoise()
        {
            // 배율은 정수 롤(0~100)의 Lerp라 롤 90이 정확히 1.2가 아닐 수 있다 — 경계 롤이 떨어지면 안 된다.
            TutorialQuest big = CaptureQuest(minRatio: 1.2f);
            Assert.IsTrue(QuestTraitRules.Matches(big, Cap(ratio: InsectSizeCalculator.ScaleFor(90))), "롤 90 = 1.20배");
            Assert.IsFalse(QuestTraitRules.Matches(big, Cap(ratio: InsectSizeCalculator.ScaleFor(89))));

            TutorialQuest small = CaptureQuest(maxRatio: 0.8f);
            Assert.IsTrue(QuestTraitRules.Matches(small, Cap(ratio: InsectSizeCalculator.ScaleFor(10))), "롤 10 = 0.80배");
            Assert.IsFalse(QuestTraitRules.Matches(small, Cap(ratio: InsectSizeCalculator.ScaleFor(11))));
        }

        [Test]
        public void CaptureSize_UnknownSize_NeverFillsASizeCondition()
        {
            // 개체 저장에 실패한 포획은 크기를 모른다 — 중간값으로 크기 조건을 채우면 안 된다.
            CaptureFacts unknown = Cap(mm: 27f, ratio: 1f, sizeKnown: false);
            Assert.IsFalse(QuestTraitRules.Matches(CaptureQuest(minMm: 25f, maxMm: 30f), unknown));
            Assert.IsFalse(QuestTraitRules.Matches(CaptureQuest(maxRatio: 1.0f), unknown));
            // 크기를 안 보는 조건은 그대로 센다.
            Assert.IsTrue(QuestTraitRules.Matches(CaptureQuest(minRarity: InsectRarity.Common), unknown));
        }

        // ── 포획 판정: 속성·이로치·등급·결합 ──

        [Test]
        public void CaptureElement_MatchesPrimaryOrSecondary()
        {
            TutorialQuest water = CaptureQuest(element: InsectElement.Water);
            Assert.IsTrue(QuestTraitRules.Matches(water, Cap(primary: InsectElement.Water)));
            Assert.IsTrue(QuestTraitRules.Matches(water, Cap(primary: InsectElement.Wind, secondary: InsectElement.Water)),
                "부속성도 그 속성이다");
            Assert.IsFalse(QuestTraitRules.Matches(water, Cap(primary: InsectElement.Wind, secondary: InsectElement.Leaf)));
        }

        [Test]
        public void CaptureShiny_OnlyShinyCounts()
        {
            TutorialQuest shiny = CaptureQuest(shiny: true);
            Assert.IsTrue(QuestTraitRules.Matches(shiny, Cap(shiny: true)));
            Assert.IsFalse(QuestTraitRules.Matches(shiny, Cap(shiny: false)));
        }

        [Test]
        public void Capture_MinRarity_IsAtLeast_NotExactly()
        {
            TutorialQuest rare = CaptureQuest(minRarity: InsectRarity.Rare, minMm: 1f);
            Assert.IsFalse(QuestTraitRules.Matches(rare, Cap(InsectRarity.Uncommon)));
            Assert.IsTrue(QuestTraitRules.Matches(rare, Cap(InsectRarity.Rare)));
            Assert.IsTrue(QuestTraitRules.Matches(rare, Cap(InsectRarity.Legendary)));
        }

        [Test]
        public void Capture_Conditions_AreAllRequired()
        {
            TutorialQuest q = CaptureQuest(minMm: 40f, element: InsectElement.Water, shiny: true);
            Assert.IsTrue(QuestTraitRules.Matches(q, Cap(mm: 50f, primary: InsectElement.Water, shiny: true)));
            Assert.IsFalse(QuestTraitRules.Matches(q, Cap(mm: 30f, primary: InsectElement.Water, shiny: true)), "크기 부족");
            Assert.IsFalse(QuestTraitRules.Matches(q, Cap(mm: 50f, primary: InsectElement.Wind, shiny: true)), "속성 다름");
            Assert.IsFalse(QuestTraitRules.Matches(q, Cap(mm: 50f, primary: InsectElement.Water, shiny: false)), "이로치 아님");
        }

        // ── 전투 판정 ──

        [Test]
        public void BattleLevelEdge_IsEnemyMinusMine()
        {
            TutorialQuest upset = BattleQuest(levelEdge: 3);
            Assert.IsTrue(QuestTraitRules.Matches(upset, Win(enemyLevel: 13, playerLevel: 10)));
            Assert.IsFalse(QuestTraitRules.Matches(upset, Win(enemyLevel: 12, playerLevel: 10)));
            Assert.IsFalse(QuestTraitRules.Matches(upset, Win(enemyLevel: 5, playerLevel: 20)), "내가 더 높으면 하극상이 아니다");
        }

        [Test]
        public void BattleMaxTurns_CountsMyActions_Inclusive()
        {
            TutorialQuest swift = BattleQuest(maxTurns: 3);
            Assert.IsTrue(QuestTraitRules.Matches(swift, Win(actions: 1)));
            Assert.IsTrue(QuestTraitRules.Matches(swift, Win(actions: 3)));
            Assert.IsFalse(QuestTraitRules.Matches(swift, Win(actions: 4)));
        }

        [Test]
        public void BattleHpPercent_IsComparedAsWholePercent()
        {
            TutorialQuest clean = BattleQuest(hpPercent: 70);
            Assert.IsTrue(QuestTraitRules.Matches(clean, Win(hp: 0.70f)));
            Assert.IsTrue(QuestTraitRules.Matches(clean, Win(hp: 0.699f)), "69.9%는 70%로 보인다");
            Assert.IsFalse(QuestTraitRules.Matches(clean, Win(hp: 0.69f)));
            Assert.IsTrue(QuestTraitRules.Matches(clean, Win(hp: 1f)));
        }

        [Test]
        public void BattleEnemyRarityAndElement_AreChecked()
        {
            TutorialQuest elite = BattleQuest(minRarity: InsectRarity.Rare);
            Assert.IsFalse(QuestTraitRules.Matches(elite, Win(InsectRarity.Uncommon)));
            Assert.IsTrue(QuestTraitRules.Matches(elite, Win(InsectRarity.Epic)));

            TutorialQuest wind = BattleQuest(element: InsectElement.Wind);
            Assert.IsTrue(QuestTraitRules.Matches(wind, Win(primary: InsectElement.Wind)));
            Assert.IsTrue(QuestTraitRules.Matches(wind, Win(primary: InsectElement.Bug, secondary: InsectElement.Wind)));
            Assert.IsFalse(QuestTraitRules.Matches(wind, Win(primary: InsectElement.Water)));
        }

        [Test]
        public void BattleCombined_AceNeedsRarityAndSpeed()
        {
            TutorialQuest ace = BattleQuest(maxTurns: 3, minRarity: InsectRarity.Rare);
            Assert.IsTrue(QuestTraitRules.Matches(ace, Win(InsectRarity.Rare, actions: 2)));
            Assert.IsFalse(QuestTraitRules.Matches(ace, Win(InsectRarity.Rare, actions: 5)), "느리다");
            Assert.IsFalse(QuestTraitRules.Matches(ace, Win(InsectRarity.Common, actions: 2)), "약한 상대");
        }

        [Test]
        public void Matches_WrongQuestType_IsAlwaysFalse()
        {
            Assert.IsFalse(QuestTraitRules.Matches(BattleQuest(maxTurns: 3), Cap()), "전투 퀘스트에 포획 입력");
            Assert.IsFalse(QuestTraitRules.Matches(CaptureQuest(minMm: 1f), Win()), "포획 퀘스트에 전투 입력");
            Assert.IsFalse(QuestTraitRules.Matches(null, Cap()));
            var plain = new TutorialQuest { questId = "plain", type = QuestType.Capture };
            Assert.IsFalse(QuestTraitRules.Matches(plain, Cap()), "일반 포획은 이 규칙의 대상이 아니다");
        }

        [Test]
        public void HasCondition_FalseOnlyForAnUnconditionedQuest()
        {
            Assert.IsFalse(QuestTraitRules.HasCondition(new TutorialQuest { type = QuestType.CaptureTrait }));
            Assert.IsFalse(QuestTraitRules.HasCondition(null));
            Assert.IsTrue(QuestTraitRules.HasCondition(CaptureQuest(minMm: 1f)));
            Assert.IsTrue(QuestTraitRules.HasCondition(CaptureQuest(shiny: true)));
            Assert.IsTrue(QuestTraitRules.HasCondition(BattleQuest(maxTurns: 1)));
            Assert.IsTrue(QuestTraitRules.HasCondition(new TutorialQuest { type = QuestType.BattleFeat, resetOnLoss = true }));
            Assert.IsTrue(QuestTraitRules.HasCondition(BattleQuest(minRarity: InsectRarity.Rare)));
        }

        // ── 입력 만들기(From) ──

        private static InsectData Species(float baseMm, InsectRarity rarity = InsectRarity.Uncommon,
            InsectElement primary = InsectElement.Leaf, InsectElement secondary = InsectElement.Bug)
        {
            var data = ScriptableObject.CreateInstance<InsectData>();
            data.insectId = "trait_probe";
            data.baseSizeMm = baseMm;
            data.rarity = rarity;
            data.primaryType = primary;
            data.secondaryType = secondary;
            return data;
        }

        [Test]
        public void CaptureFacts_From_ReadsTheSavedIndividual()
        {
            InsectData data = Species(40f);
            objects.Add(data);
            var captured = new PlayerInsectData { instanceId = "abc", insectId = "trait_probe", sizeRoll = 100, isShiny = true };

            CaptureFacts f = CaptureFacts.From(data, captured);

            Assert.IsTrue(f.sizeKnown);
            Assert.AreEqual(50f, f.sizeMm, 0.01f, "40mm × 1.25");
            Assert.AreEqual(1.25f, f.sizeRatio, 1e-4f);
            Assert.IsTrue(f.shiny, "이로치는 저장된 개체의 값이다");
            Assert.AreEqual(InsectRarity.Uncommon, f.rarity);
            Assert.AreEqual(InsectElement.Leaf, f.primary);
            Assert.AreEqual(InsectElement.Bug, f.secondary);
        }

        [Test]
        public void CaptureFacts_From_NullIndividual_DoesNotKnowTheSize()
        {
            InsectData data = Species(40f);
            objects.Add(data);

            CaptureFacts f = CaptureFacts.From(data, null, shinyFallback: true);

            Assert.IsFalse(f.sizeKnown);
            Assert.IsTrue(f.shiny, "저장 실패 때는 필드에서 본 이로치 표지를 쓴다");
            Assert.IsFalse(CaptureFacts.From(null, null).sizeKnown);
        }

        [Test]
        public void BattleFacts_From_ClampsHpAndSurvivesZeroMax()
        {
            InsectData enemy = Species(30f, InsectRarity.Epic, InsectElement.Wind, InsectElement.None);
            objects.Add(enemy);

            BattleFacts f = BattleFacts.From(enemy, 12, 9, 3, 45, 90);
            Assert.AreEqual(0.5f, f.playerHpRatio, 1e-4f);
            Assert.AreEqual(12, f.enemyLevel);
            Assert.AreEqual(9, f.playerLevel);
            Assert.AreEqual(3, f.playerActions);
            Assert.AreEqual(InsectRarity.Epic, f.enemyRarity);
            Assert.AreEqual(InsectElement.Wind, f.enemyPrimary);

            Assert.AreEqual(1f, BattleFacts.From(enemy, 1, 1, 1, 200, 90).playerHpRatio, "넘치는 HP는 1로");
            Assert.AreEqual(0f, BattleFacts.From(enemy, 1, 1, 1, 10, 0).playerHpRatio, "최대 HP 0이어도 0으로 나눠 터지지 않는다");
        }

        // ── 실제 퀘스트 표 ──

        private static TutorialQuest[] RealQuests()
        {
            var host = new GameObject("QuestTraitProbe");
            host.SetActive(false);   // Awake(싱글턴)·Start(세이브 로드)를 돌리지 않는다
            try
            {
                var mgr = host.AddComponent<TutorialQuestManager>();
                typeof(TutorialQuestManager).GetMethod("Initialize", Inst).Invoke(mgr, null);
                return mgr.GetAllQuests();
            }
            finally { Object.DestroyImmediate(host); }
        }

        private static bool IsTrait(TutorialQuest q) =>
            q.type == QuestType.CaptureTrait || q.type == QuestType.BattleFeat;

        [Test]
        public void RealTable_TraitQuests_AreSideOnly_AndCarryACondition()
        {
            int count = 0;
            foreach (TutorialQuest q in RealQuests())
            {
                if (!IsTrait(q)) continue;
                count++;
                Assert.AreEqual(QuestCategory.Side, q.category,
                    $"{q.questId}: 조건부 타입을 Story에 달면 선형 체인이 세지 않아 영구 정지한다");
                Assert.IsTrue(QuestTraitRules.HasCondition(q),
                    $"{q.questId}: 조건이 없으면 무엇이든 센다 — 기본 Capture/Battle로 쓸 것");
            }
            Assert.GreaterOrEqual(count, 25, "조건부 퀘스트가 표에서 빠졌다");
        }

        [Test]
        public void RealTable_NoQuestRequiresARepeatableOne()
        {
            // 반복 서브는 completedQuests에 들어가지 않고 목표만 올려 재시작한다(CompleteSideQuest).
            // 그걸 선행으로 물면 그 퀘스트는 영영 안 열린다.
            TutorialQuest[] all = RealQuests();
            var byId = new Dictionary<string, TutorialQuest>();
            foreach (TutorialQuest q in all) byId[q.questId] = q;

            foreach (TutorialQuest q in all)
            {
                if (string.IsNullOrEmpty(q.prerequisiteQuestId)) continue;
                Assert.IsTrue(byId.TryGetValue(q.prerequisiteQuestId, out TutorialQuest pre), $"{q.questId}의 선행이 없다");
                Assert.IsFalse(pre.repeatable, $"{q.questId}의 선행 {pre.questId}가 반복형이라 영영 완료로 안 잡힌다");
            }
        }

        [Test]
        public void RealTable_TaleChains_AreOneShot_AndEachEpisodeRequiresThePreviousOne()
        {
            var tales = new List<TutorialQuest>();
            foreach (TutorialQuest q in RealQuests())
                if (q.questId.StartsWith("s_tale_")) tales.Add(q);
            Assert.AreEqual(11, tales.Count, "외전: 일기 4 + 라온 4 + 세라 3");

            foreach (TutorialQuest q in tales)
            {
                Assert.IsFalse(q.repeatable, $"{q.questId}: 외전은 한 번 읽는 이야기다");
                Assert.IsTrue(q.title.StartsWith("[외전]"), $"{q.questId}: 외전 표지가 목록에서 구분해 준다");
            }

            // 연작은 번호 순으로 사슬이다(1화만 본편 퀘스트를 선행으로 문다).
            foreach (string series in new[] { "s_tale_diary", "s_tale_raon", "s_tale_sera" })
            {
                int episode = 1;
                var chain = tales.FindAll(t => t.questId.StartsWith(series));
                Assert.GreaterOrEqual(chain.Count, 3, series);
                foreach (TutorialQuest ep in chain)
                {
                    if (episode == 1)
                        Assert.IsFalse(ep.prerequisiteQuestId.StartsWith("s_tale_"), $"{ep.questId}: 1화는 본편 퀘스트를 선행으로 문다");
                    else
                        Assert.AreEqual(series + (episode - 1), ep.prerequisiteQuestId,
                            $"{ep.questId}: 앞 화를 선행으로 물어야 차례로 열린다");
                    episode++;
                }
            }
        }

        // ── 매니저를 실제로 굴린다 ──

        private TutorialQuestManager NewManager(params string[] completed)
        {
            var host = new GameObject("QuestTraitManager");
            host.SetActive(false);   // Awake(싱글턴 등록)·Start(세이브 로드)를 건너뛴다 — 상태는 아래에서 직접 채운다
            objects.Add(host);
            var mgr = host.AddComponent<TutorialQuestManager>();
            typeof(TutorialQuestManager).GetMethod("Initialize", Inst).Invoke(mgr, null);
            typeof(TutorialQuestManager).GetField("tutorialSessionStarted", Inst).SetValue(mgr, true);
            var done = (HashSet<string>)typeof(TutorialQuestManager).GetField("completedQuests", Inst).GetValue(mgr);
            done.Clear();
            foreach (string id in completed) done.Add(id);
            return mgr;
        }

        private static void BattleEnded(TutorialQuestManager mgr, bool playerWon) =>
            typeof(TutorialQuestManager).GetMethod("OnBattleEnded", Inst).Invoke(mgr, new object[] { playerWon });

        [Test]
        public void OneCapture_AdvancesEveryMatchingQuest_InParallel()
        {
            // 영웅 등급 82mm 물 곤충 한 마리 = 큰 곤충 셋 + 물가 + 영웅 채집단이 함께 오른다.
            TutorialQuestManager mgr = NewManager("q_capture3", "q_capture10");

            mgr.NotifyCapture(Cap(InsectRarity.Epic, mm: 82f, ratio: 1.1f, primary: InsectElement.Water));

            Assert.AreEqual(1, mgr.GetSideProgress("s_size_big40"));
            Assert.AreEqual(1, mgr.GetSideRepeatCount("s_size_big60"), "목표 1이라 바로 한 단계 끝났다");
            Assert.IsTrue(mgr.IsQuestCompleted("s_size_big80"), "80mm 이상 1회형은 완료");
            Assert.AreEqual(1, mgr.GetSideProgress("s_trait_water"));
            Assert.AreEqual(1, mgr.GetSideProgress("s_pack_epic"), "등급 기반 기존 서브도 같은 포획으로 오른다");
            // 안 맞는 조건은 건드리지 않는다.
            Assert.AreEqual(0, mgr.GetSideProgress("s_size_small20"));
            Assert.AreEqual(0, mgr.GetSideProgress("s_trait_light"));
            Assert.AreEqual(0, mgr.GetSideProgress("s_size_ratio_big"), "1.1배는 20% 미만");
        }

        [Test]
        public void LockedQuests_DoNotCount()
        {
            TutorialQuestManager mgr = NewManager();   // 선행을 하나도 안 깼다

            mgr.NotifyCapture(Cap(InsectRarity.Epic, mm: 82f, primary: InsectElement.Water));
            mgr.NotifyBattleFeat(Win(InsectRarity.Epic, actions: 1, hp: 1f));

            Assert.AreEqual(0, mgr.GetSideProgress("s_size_big40"));
            Assert.AreEqual(0, mgr.GetSideProgress("s_trait_water"));
            Assert.AreEqual(0, mgr.GetSideProgress("s_feat_swift"));
            Assert.IsFalse(mgr.IsQuestCompleted("s_size_big80"));
        }

        [Test]
        public void OneWin_CannotAdvanceTwoChaptersOfTheSameTale()
        {
            // 빠르고 깔끔한 승리는 라온 1화(3번 이내)와 2화(HP 70%)를 둘 다 만족한다. 1화가 끝나면 2화가 열리지만
            // 같은 승리가 2화까지 채우면 연작이 한 걸음씩이 아니게 된다.
            TutorialQuestManager mgr = NewManager("q_battle");

            mgr.NotifyBattleFeat(Win(actions: 2, hp: 1f));

            Assert.IsTrue(mgr.IsQuestCompleted("s_tale_raon1"));
            Assert.AreEqual(0, mgr.GetSideProgress("s_tale_raon2"), "방금 열린 2화에 같은 승리가 들어갔다");

            mgr.NotifyBattleFeat(Win(actions: 2, hp: 1f));
            Assert.AreEqual(1, mgr.GetSideProgress("s_tale_raon2"), "다음 승리부터 센다");
        }

        [Test]
        public void ParallelBattleFeats_ProgressTogether()
        {
            TutorialQuestManager mgr = NewManager("q_battle", "q_battle10", "q_battle3");

            // 희귀 · 바람 · 2번 행동 · 풀 HP · 내 곤충보다 4레벨 위
            mgr.NotifyBattleFeat(Win(InsectRarity.Rare, enemyLevel: 14, playerLevel: 10, actions: 2, hp: 1f,
                primary: InsectElement.Wind));

            Assert.AreEqual(1, mgr.GetSideProgress("s_feat_elite"));
            Assert.AreEqual(1, mgr.GetSideProgress("s_feat_upset"));
            Assert.AreEqual(1, mgr.GetSideProgress("s_feat_swift"));
            Assert.AreEqual(1, mgr.GetSideProgress("s_feat_clean"));
            Assert.AreEqual(1, mgr.GetSideProgress("s_feat_wind"));
            Assert.AreEqual(1, mgr.GetSideProgress("s_feat_streak"));
            Assert.AreEqual(0, mgr.GetSideProgress("s_feat_epic"), "희귀는 영웅이 아니다");
            Assert.AreEqual(1, mgr.GetSideRepeatCount("s_feat_ace"), "희귀 이상을 2번 행동 — 목표 1이라 한 단계 끝");
        }

        [Test]
        public void Streak_BreaksOnLoss_AndANonMatchingWinLeavesItAlone()
        {
            TutorialQuestManager mgr = NewManager("q_battle", "q_battle3");

            mgr.NotifyBattleFeat(Win());
            mgr.NotifyBattleFeat(Win());
            Assert.AreEqual(2, mgr.GetSideProgress("s_feat_streak"));

            BattleEnded(mgr, true);   // 이긴 신호는 연승을 끊지 않는다
            Assert.AreEqual(2, mgr.GetSideProgress("s_feat_streak"));

            BattleEnded(mgr, false);  // 지거나 도망쳤다
            Assert.AreEqual(0, mgr.GetSideProgress("s_feat_streak"));
            Assert.AreEqual(0, mgr.GetSideRepeatCount("s_feat_streak"), "반복 횟수(목표 상승)는 건드리지 않는다");

            mgr.NotifyBattleFeat(Win());
            Assert.AreEqual(1, mgr.GetSideProgress("s_feat_streak"), "끊긴 뒤 처음부터 다시 센다");
        }

        [Test]
        public void Streak_LossDoesNotTouchOrdinaryFeats()
        {
            TutorialQuestManager mgr = NewManager("q_battle", "q_battle3");
            mgr.NotifyBattleFeat(Win(actions: 2, hp: 1f));
            Assert.AreEqual(1, mgr.GetSideProgress("s_feat_swift"));

            BattleEnded(mgr, false);

            Assert.AreEqual(1, mgr.GetSideProgress("s_feat_swift"), "연승이 아닌 퀘스트의 진행은 지는 것으로 줄지 않는다");
            Assert.AreEqual(1, mgr.GetSideProgress("s_feat_clean"));
        }

        [Test]
        public void OneShotStreak_OfTheTale_CompletesAndOnlyResetsWhileOpen()
        {
            TutorialQuestManager mgr = NewManager("q_battle", "s_tale_raon1", "s_tale_raon2");

            mgr.NotifyBattleFeat(Win());
            mgr.NotifyBattleFeat(Win());
            BattleEnded(mgr, false);
            Assert.AreEqual(0, mgr.GetSideProgress("s_tale_raon3"));

            mgr.NotifyBattleFeat(Win());
            mgr.NotifyBattleFeat(Win());
            mgr.NotifyBattleFeat(Win());
            Assert.IsTrue(mgr.IsQuestCompleted("s_tale_raon3"), "세 번 연달아 이기면 도전장 3이 끝난다");

            BattleEnded(mgr, false);   // 끝난 뒤의 패배는 아무것도 되돌리지 않는다
            Assert.IsTrue(mgr.IsQuestCompleted("s_tale_raon3"));
        }

        [Test]
        public void RepeatableTrait_RaisesItsTarget_AfterEachRound()
        {
            TutorialQuestManager mgr = NewManager("q_capture3");
            TutorialQuest q = mgr.FindQuest("s_size_small20");
            Assert.AreEqual(4, mgr.EffectiveTarget(q));

            for (int i = 0; i < 4; i++) mgr.NotifyCapture(Cap(mm: 18f));

            Assert.AreEqual(1, mgr.GetSideRepeatCount("s_size_small20"));
            Assert.AreEqual(0, mgr.GetSideProgress("s_size_small20"), "다음 단계는 0에서 시작");
            Assert.AreEqual(7, mgr.EffectiveTarget(q), "4 + 3");
        }

        [Test]
        public void DreamPrologue_FreezesTraitQuests()
        {
            TutorialQuestManager mgr = NewManager("q_capture3", "q_battle", "q_battle3");

            DreamPrologueState.Begin();
            mgr.NotifyCapture(Cap(mm: 18f));
            mgr.NotifyBattleFeat(Win(actions: 1, hp: 1f));
            BattleEnded(mgr, false);
            DreamPrologueState.End();

            Assert.AreEqual(0, mgr.GetSideProgress("s_size_small20"), "꿈속의 곤충이 꿈 밖 퀘스트를 채웠다");
            Assert.AreEqual(0, mgr.GetSideProgress("s_feat_swift"), "꿈속의 승리가 꿈 밖 퀘스트를 채웠다");

            mgr.NotifyCapture(Cap(mm: 18f));
            Assert.AreEqual(1, mgr.GetSideProgress("s_size_small20"), "꿈이 끝나면 다시 센다");
        }

        [Test]
        public void SessionNotStarted_CountsNothing()
        {
            TutorialQuestManager mgr = NewManager("q_capture3");
            typeof(TutorialQuestManager).GetField("tutorialSessionStarted", Inst).SetValue(mgr, false);

            mgr.NotifyCapture(Cap(mm: 18f));

            Assert.AreEqual(0, mgr.GetSideProgress("s_size_small20"));
        }

        // ── 실제 곤충 DB로 달성 가능성을 잰다 ──
        //
        // 기준값(40·60·80mm, 16·20mm, 속성)을 손으로 정하면 "분포 밖의 값"이 조용히 들어온다 — 어떤 곤충도 80mm가
        // 안 되거나, 어떤 속성이 필드에 한 종뿐이거나. 부트스트랩이 만드는 진짜 DB와 진짜 판정(QuestTraitRules)으로
        // 포획 한 번당 확률을 재서 "채우려면 평균 몇 번 만나야 하는가"를 본다.

        private InsectDatabase RealDatabase()
        {
            var host = new GameObject("QuestTraitTests_RealDb");
            host.SetActive(false);   // Awake(월드 생성)를 막고 private 생성 함수만 부른다
            objects.Add(host);
            var bootstrap = host.AddComponent<PlaySceneBootstrap>();
            var db = (InsectDatabase)typeof(PlaySceneBootstrap).GetMethod("EnsureExpandedDatabase", Inst).Invoke(bootstrap, null);
            Assert.IsNotNull(db);
            objects.Add(db);
            return db;
        }

        private static float Share(InsectRarity r)
        {
            switch (r)
            {
                case InsectRarity.Common: return FieldSpawnRules.CommonShare;
                case InsectRarity.Uncommon: return FieldSpawnRules.UncommonShare;
                case InsectRarity.Rare: return FieldSpawnRules.RareShare;
                case InsectRarity.Epic: return FieldSpawnRules.EpicShare;
                default: return FieldSpawnRules.LegendaryShare;
            }
        }

        /// <summary>
        /// 필드에서 한 번 잡았을 때 이 조건부 포획이 세어질 확률 — 등급은 전역 등급표로, 종은 등급 안에서 균등,
        /// 개체 크기는 롤 0~100 균등으로 가정한다(실제 스폰 가중치·리전 풀은 무시한 근사). 판정은 진짜 규칙을 쓴다.
        /// </summary>
        private static double CaptureOdds(InsectDatabase db, TutorialQuest quest)
        {
            double total = 0;
            foreach (InsectRarity rarity in System.Enum.GetValues(typeof(InsectRarity)))
            {
                var species = db.insects.FindAll(d => d != null && d.rarity == rarity);
                if (species.Count == 0) continue;

                double within = 0;
                foreach (InsectData d in species)
                {
                    int hits = 0;
                    for (int roll = InsectSizeCalculator.MinRoll; roll <= InsectSizeCalculator.MaxRoll; roll++)
                    {
                        float scale = InsectSizeCalculator.ScaleFor(roll);
                        var facts = new CaptureFacts
                        {
                            rarity = d.rarity, primary = d.primaryType, secondary = d.secondaryType,
                            sizeMm = d.baseSizeMm * scale, sizeRatio = scale, sizeKnown = true, shiny = quest.requireShiny,
                        };
                        if (QuestTraitRules.Matches(quest, facts)) hits++;
                    }
                    within += hits / (double)(InsectSizeCalculator.MaxRoll - InsectSizeCalculator.MinRoll + 1);
                }
                total += Share(rarity) * within / species.Count;
            }
            return total;
        }

        /// <summary>전투는 등급·속성만 DB로 잴 수 있다(행동 수·HP·레벨 차는 전투가 정한다) — 상한 근사다.</summary>
        private static double EnemyOdds(InsectDatabase db, TutorialQuest quest)
        {
            double total = 0;
            foreach (InsectRarity rarity in System.Enum.GetValues(typeof(InsectRarity)))
            {
                var species = db.insects.FindAll(d => d != null && d.rarity == rarity);
                if (species.Count == 0) continue;
                int hits = 0;
                foreach (InsectData d in species)
                {
                    var facts = new BattleFacts
                    {
                        enemyRarity = d.rarity, enemyPrimary = d.primaryType, enemySecondary = d.secondaryType,
                        enemyLevel = 99, playerLevel = 1, playerActions = 1, playerHpRatio = 1f,
                    };
                    if (QuestTraitRules.Matches(quest, facts)) hits++;
                }
                total += Share(rarity) * hits / species.Count;
            }
            return total;
        }

        [Test]
        public void RealDb_SizeProfileIsApplied()
        {
            InsectDatabase db = RealDatabase();
            var distinct = new HashSet<int>();
            foreach (InsectData d in db.insects) if (d != null) distinct.Add(Mathf.RoundToInt(d.baseSizeMm));
            Assert.Greater(distinct.Count, 20, "종 몸길이가 한 값으로 몰려 있으면 크기 퀘스트의 확률 계산이 무의미하다");
        }

        [Test]
        public void RealDb_EveryTraitQuest_IsReachableAtSensibleOdds()
        {
            InsectDatabase db = RealDatabase();
            var report = new System.Text.StringBuilder();
            int checkedCount = 0;

            foreach (TutorialQuest q in RealQuests())
            {
                if (!IsTrait(q)) continue;

                double odds;
                if (q.type == QuestType.CaptureTrait)
                {
                    if (q.requireShiny) continue;   // 이로치는 종 데이터가 아니라 굴림(필드 1%)이다
                    odds = CaptureOdds(db, q);
                }
                else
                {
                    // 등급·속성 조건이 없으면 DB로 낼 수 있는 숫자가 없다(행동 수·HP·레벨 차는 전투가 정한다).
                    if (q.minRarity == InsectRarity.Common && q.requiredElement == InsectElement.None) continue;
                    odds = EnemyOdds(db, q);
                }

                checkedCount++;
                double encounters = odds > 0 ? q.targetCount / odds : double.PositiveInfinity;
                report.AppendLine($"{q.questId,-20} 한 번당 {odds * 100:0.0}%  → 목표 {q.targetCount}에 평균 {encounters:0}번");

                Assert.Greater(odds, 0.0, $"{q.questId}: 어떤 곤충도 이 조건을 못 채운다 — 영원히 안 찬다");
                Assert.LessOrEqual(encounters, 450.0,
                    $"{q.questId}: 평균 {encounters:0}번을 만나야 찬다 — 한 번당 {odds * 100:0.00}%");
            }

            Debug.Log("[QuestTrait] 달성 확률(포획·조우 한 번당)\n" + report);
            Assert.GreaterOrEqual(checkedCount, 20, "검사 대상이 너무 적다 — 판정 분기가 바뀌었는지 확인");
        }

        [Test]
        public void RealDb_EveryElementUsedByAQuest_HasSeveralSpecies()
        {
            InsectDatabase db = RealDatabase();
            foreach (TutorialQuest q in RealQuests())
            {
                if (!IsTrait(q) || q.requiredElement == InsectElement.None) continue;
                int species = db.insects.FindAll(d => d != null
                    && (d.primaryType == q.requiredElement || d.secondaryType == q.requiredElement)).Count;
                Assert.GreaterOrEqual(species, 5,
                    $"{q.questId}: {q.requiredElement} 속성 곤충이 {species}종뿐이라 목표를 채우기 전에 같은 종만 마주친다");
            }
        }
    }
}
#endif
