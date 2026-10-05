#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Story;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InsectGame.Tests
{
    /// <summary>
    /// 「챔피언의 꿈」을 <b>실제 PlayScene</b>에서 끝까지 돌린다 — 부트스트랩 배선, 진짜 전투 화면, 진짜 섬(방문 모드),
    /// 깨어남까지. 단위 테스트는 규칙을 고정하지만 "지휘자가 실제로 이어 붙는가"와 "꿈 밖이 정말 그대로인가"는
    /// 진짜 씬에서만 보인다 — 배선 하나(예: 부트스트랩의 AutoWire)만 빠져도 꿈이 조용히 안 뜬다.
    ///
    /// 약속은 하나다: <b>꿈이 끝났을 때 이 계정의 어떤 기록도 시작 전과 같다.</b>
    /// </summary>
    [TestFixture]
    public class DreamPrologueIntegrationTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        [Timeout(300000)]
        public async Task FullDream_RunsThroughTheRealScene_AndLeavesTheGameUntouched()
        {
            int previousSceneHandle = SceneManager.GetActiveScene().handle;
            SceneManager.LoadScene(GameConstants.Scenes.Play, LoadSceneMode.Single);
            await WaitForBootAsync(previousSceneHandle);
            for (int i = 0; i < 10; i++) await Task.Yield();

            var director = Object.FindFirstObjectByType<DreamPrologueDirector>();
            var battle = Object.FindFirstObjectByType<InsectBattleController>();
            var battleScreen = Object.FindFirstObjectByType<BattleScreenUI>();
            var island = Object.FindFirstObjectByType<IslandWorldBuilder>();
            var player = Object.FindFirstObjectByType<PlayerMovement>();
            var quests = Object.FindFirstObjectByType<TutorialQuestManager>();
            var candy = Object.FindFirstObjectByType<PlayerCandyInventory>();
            var progress = Object.FindFirstObjectByType<PlayerProgressController>();
            var wallet = Object.FindFirstObjectByType<PlayerCurrencyWallet>();
            var collection = Object.FindFirstObjectByType<PlayerInsectCollection>();
            var story = Object.FindFirstObjectByType<StoryDirector>();
            Assert.IsNotNull(director, "부트스트랩이 DreamPrologueDirector를 만들지 않았다 — 꿈이 영영 안 뜬다");
            Assert.IsNotNull(battle);
            Assert.IsNotNull(battleScreen);
            Assert.IsNotNull(island);
            Assert.IsNotNull(player);

            // ── 시작 전 상태 ──
            string playedKey = SaveScope.PrefsKey(DreamPrologueRules.PlayedPrefsKey);
            int playedBefore = PlayerPrefs.GetInt(playedKey, 0);
            int candyBefore = candy != null ? candy.Candies : 0;
            int levelBefore = progress != null ? progress.Level : 0;
            int xpBefore = progress != null ? progress.CurrentXp : 0;
            int coinsBefore = wallet != null ? wallet.Coins : 0;
            int ownedBefore = collection != null ? collection.GetAllOwned().Count : 0;
            string questProgressBefore = QuestProgressSnapshot(quests);
            Vector3 positionBefore = player.transform.position;
            Assert.IsFalse(DreamPrologueState.Active);

            try
            {
                // 이 PC의 로그인 화면·월드 선택이 모달/프리즈를 쥐고 있을 수 있다 — 꿈을 시작할 수 있는 상태로 만든다.
                ClearBlockers(player);
                Assert.IsTrue(director.CanReplay, "다시 보기가 막혀 있다(모달·프리즈·서브에리어)");
                Assert.IsTrue(director.TryReplay());
                Assert.IsTrue(DreamPrologueState.Active, "꿈이 시작됐는데 표지가 안 켜졌다 — 퀘스트가 꿈속 행동을 센다");
                Assert.IsTrue(director.IsRunning);

                // ── 챔피언전 ──
                bool sawBattle = false;
                int actions = 0;
                float limit = Time.realtimeSinceStartup + 90f;
                while (Phase(director) != "Island" && Time.realtimeSinceStartup < limit)
                {
                    await Task.Yield();
                    if (Phase(director) == "Battle") sawBattle = true;
                    if (BattlePhase(battleScreen) != "PlayerTurn") continue;
                    for (int i = 0; i < 4; i++)
                    {
                        if (!battle.CanUseSkill(i)) continue;
                        typeof(BattleScreenUI).GetMethod("TryUseSkill", Private).Invoke(battleScreen, new object[] { i });
                        actions++;
                        break;
                    }
                }
                Assert.IsTrue(sawBattle, "챔피언전 단계가 없었다");
                Assert.IsTrue(battle.IsSandbox, "방금 끝난 전투가 샌드박스가 아니다");
                Assert.AreEqual("Island", Phase(director), "챔피언전 뒤 섬으로 넘어가지 못했다");
                Assert.GreaterOrEqual(actions, SandboxBattleRules.MinPlayerActions);
                Assert.LessOrEqual(actions, SandboxBattleRules.MaxPlayerActions + 2);

                // ── 챔피언의 섬 ──
                Assert.IsTrue(island.IsOnIsland, "섬에 들어가지 못했다");
                Assert.IsTrue(island.DreamMode);
                Assert.IsTrue(island.IsVisiting, "꾸며진 섬은 방문 모드의 스냅샷으로 열린다");

                // 걷기 단계를 건너뛰고 곤충 → 분수 순서로 걸어간다(순간이동은 걸음으로 세지 않으므로 몇 걸음에 나눈다).
                await WalkTo(director, island, player, target: null);
                Assert.AreEqual("Fountain", Step(director), "곤충에게 다가가는 단계가 끝나지 않았다");
                await WalkTo(director, island, player, target: DreamPrologueData.FountainWorldPosition());

                // ── 깨어남 ──
                limit = Time.realtimeSinceStartup + 40f;
                while (director.IsRunning && Time.realtimeSinceStartup < limit) await Task.Yield();
                Assert.IsFalse(director.IsRunning, "꿈이 끝나지 않았다");
            }
            finally
            {
                // 이 PC의 기록을 돌려 둔다 — 다시 보기도 "봤다"를 적는다.
                if (playedBefore == 0) PlayerPrefs.DeleteKey(playedKey);
                else PlayerPrefs.SetInt(playedKey, playedBefore);
            }

            // ── 끝난 뒤 ──
            Assert.IsFalse(DreamPrologueState.Active, "꿈이 끝났는데 표지가 켜진 채다 — 이후 퀘스트가 영영 안 올라간다");
            Assert.IsFalse(island.IsOnIsland, "섬에 갇혔다");
            Assert.IsFalse(island.DreamMode);
            Assert.IsFalse(player.IsFrozen, "플레이어가 얼어 있다");
            Assert.Less(Vector3.Distance(Planar(player.transform.position), Planar(positionBefore)), 3f, "원래 자리로 돌아오지 못했다");

            if (candy != null) Assert.AreEqual(candyBefore, candy.Candies, "캔디가 바뀌었다");
            if (progress != null)
            {
                Assert.AreEqual(levelBefore, progress.Level, "레벨이 바뀌었다");
                Assert.AreEqual(xpBefore, progress.CurrentXp, "경험치가 바뀌었다");
            }
            if (wallet != null) Assert.AreEqual(coinsBefore, wallet.Coins, "코인이 바뀌었다");
            if (collection != null) Assert.AreEqual(ownedBefore, collection.GetAllOwned().Count, "곤충이 컬렉션에 들어갔다");
            Assert.AreEqual(questProgressBefore, QuestProgressSnapshot(quests), "꿈속의 행동이 퀘스트에 들어갔다");
            if (story != null) Assert.IsFalse(story.HasSeen("ch1_first_battle"), "꿈속의 승리가 1막의 '첫 전투' 비트를 열었다");
        }

        // ── 도우미 ──

        private static string Phase(DreamPrologueDirector d)
            => typeof(DreamPrologueDirector).GetField("phase", Private).GetValue(d).ToString();

        private static string Step(DreamPrologueDirector d)
            => typeof(DreamPrologueDirector).GetField("step", Private).GetValue(d).ToString();

        private static string BattlePhase(BattleScreenUI ui)
            => typeof(BattleScreenUI).GetField("phase", Private).GetValue(ui).ToString();

        private static Vector3 Planar(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static string QuestProgressSnapshot(TutorialQuestManager quests)
        {
            if (quests == null) return string.Empty;
            var progress = (Dictionary<string, int>)typeof(TutorialQuestManager).GetField("questProgress", Private).GetValue(quests);
            var side = (Dictionary<string, int>)typeof(TutorialQuestManager).GetField("sideProgress", Private).GetValue(quests);
            var keys = new List<string>();
            foreach (var kv in progress) keys.Add($"{kv.Key}={kv.Value}");
            foreach (var kv in side) keys.Add($"s:{kv.Key}={kv.Value}");
            keys.Sort();
            return string.Join(",", keys) + "|" + string.Join(",", new List<string>(quests.CompletedQuestIds));
        }

        private static void ClearBlockers(PlayerMovement player)
        {
            // 로그인·월드 선택 화면이 남긴 모달 — 테스트 PC에는 로그인 세션이 없다.
            var stack = (System.Collections.IList)typeof(ModalUIRegistry)
                .GetField("stack", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            stack.Clear();
            player.SetFrozen(false);
        }

        // 목표(null이면 가장 가까운 곤충)까지 몇 걸음에 나눠 걸어간다 — 한 프레임 5m 이상은 순간이동으로 쳐서 세지 않는다.
        private static async Task WalkTo(DreamPrologueDirector director, IslandWorldBuilder island, PlayerMovement player, Vector3? target)
        {
            string phaseWanted = target.HasValue ? "Linger" : "Fountain";
            float limit = Time.realtimeSinceStartup + 40f;
            // 먼저 걷기 단계(8m)를 채운다 — 입구에서 같은 자리를 오가며 걷는다.
            while (Step(director) == "Move" && Time.realtimeSinceStartup < limit)
            {
                float swing = Mathf.Sin(Time.realtimeSinceStartup * 6f) * 1.5f;
                Vector3 p = player.transform.position;
                player.transform.position = new Vector3(p.x, p.y, p.z + (swing > 0 ? 0.6f : -0.6f));
                await Task.Yield();
            }

            while (Step(director) != phaseWanted && Time.realtimeSinceStartup < limit)
            {
                Vector3 goal;
                if (target.HasValue) goal = target.Value;
                else if (!island.TryGetNearestInsect(player.transform.position, out goal, out _, out _)) { await Task.Yield(); continue; }

                Vector3 from = player.transform.position;
                Vector3 toward = goal - from;
                toward.y = 0f;
                // 도착 반경 안쪽까지 들어간다(곤충은 움직이므로 매 프레임 다시 읽는다).
                Vector3 next = toward.magnitude <= 1.2f ? from : from + toward.normalized * Mathf.Min(1.5f, toward.magnitude - 1.0f);
                player.transform.position = new Vector3(next.x, from.y, next.z);
                await Task.Yield();
            }
            Assert.AreEqual(phaseWanted, Step(director), "목표 지점에 닿지 못했다");
        }

        private static async Task WaitForBootAsync(int previousSceneHandle)
        {
            const int maxFrames = 900;
            for (int frame = 0; frame < maxFrames; frame++)
            {
                Scene scene = SceneManager.GetSceneByName(GameConstants.Scenes.Play);
                PlaySceneBootstrap bootstrap = Object.FindFirstObjectByType<PlaySceneBootstrap>();
                if (scene.IsValid() && scene.isLoaded && scene.handle != previousSceneHandle
                    && bootstrap != null && bootstrap.gameObject.scene == scene
                    && Object.FindFirstObjectByType<IslandWorldBuilder>() != null
                    && Object.FindFirstObjectByType<DreamPrologueDirector>() != null)
                    return;
                await Task.Yield();
            }
            Assert.Fail("PlayScene Bootstrap 초기화가 제한 시간 안에 끝나지 않았습니다(또는 DreamPrologueDirector가 안 만들어졌습니다).");
        }
    }
}
#endif
