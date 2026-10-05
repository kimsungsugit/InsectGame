using InsectGame.Core;
using UnityEngine;

namespace InsectGame.Spawning
{
    /// <summary>첫 색다른 조우의 <b>순수</b> 판정 — 언제 일어나는가.</summary>
    public static class FirstShinyRules
    {
        /// <summary>이 퀘스트를 하는 동안 일어난다 — 「곤충 3마리 포획」.</summary>
        public const string QuestId = "q_capture3";
        /// <summary>그 퀘스트의 진행이 이만큼 됐을 때. 2면 마지막 한 마리를 남긴 시점이다.</summary>
        public const int AtProgress = 2;

        /// <summary>
        /// 지금이 그 순간인가. 세 번째 포획을 앞둔 때로 잡은 것은, 포획에 익숙해진 직후이면서
        /// 라온이 내기를 건 바로 그때라서다 — "마지막 한 마리"가 평범한 숙제에서 반짝이는 표적으로 바뀐다.
        /// 그 퀘스트를 이미 끝낸 세이브에서는 일어나지 않는다(도입부 연출이다).
        /// </summary>
        public static bool ShouldTrigger(string activeQuestId, int activeProgress)
        {
            return activeQuestId == QuestId && activeProgress >= AtProgress;
        }
    }

    /// <summary>
    /// 도입부의 확정 조우 — 세 번째 포획을 앞두고 근처 곤충 한 마리가 색다른 개체로 바뀐다. 한 계정에 한 번.
    ///
    /// 색다른 개체는 필드에서 1%라 첫 10분 안에 볼 일이 거의 없다. 이 게임의 "특별한 것을 만났다"는 감각이
    /// 처음 생기는 자리를 운에 맡기지 않는다. 실제 전환은 <see cref="InsectSpawner.TryMakeNearbyShiny"/>가 한다 —
    /// 새로 스폰하지 않고 이미 서 있는 개체를 바꾸므로 슬롯·등급표·리전 풀에 영향이 없다.
    /// </summary>
    public class FirstShinyEncounter : MonoBehaviour
    {
        private const float CheckInterval = 2f;
        // 너무 가까우면 눈앞에서 색이 바뀌는 게 보이고, 너무 멀면 못 찾는다.
        private const float MinDistance = 7f;
        private const float MaxDistance = 26f;

        private InsectSpawner spawner;
        private TutorialQuestManager questManager;
        private RegionManager regionManager;
        private PlayerMovement playerMovement;
        private Transform playerTransform;
        private FieldMomentFeed momentFeed;

        private float timer;
        // 불러온 계정 스코프 키 — 로그인·계정 전환으로 키가 바뀌면 다시 읽는다.
        private string prefsKey;
        private bool given;

        public void AutoWire(InsectSpawner insectSpawner, TutorialQuestManager quests, RegionManager region,
            PlayerMovement movement, Transform player, FieldMomentFeed feed)
        {
            if (spawner == null) spawner = insectSpawner;
            if (questManager == null) questManager = quests;
            if (regionManager == null) regionManager = region;
            if (playerMovement == null) playerMovement = movement;
            if (playerTransform == null) playerTransform = player;
            if (momentFeed == null) momentFeed = feed;
        }

        private void EnsureStateLoaded()
        {
            string key = AuthManager.ScopedKey(GameConstants.PrefsKeys.FirstShinyGiven);
            if (key == prefsKey) return;
            prefsKey = key;
            given = PlayerPrefs.GetInt(key, 0) != 0;
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = CheckInterval;

            EnsureStateLoaded();
            if (given || spawner == null || questManager == null || playerTransform == null) return;

            TutorialQuest active = questManager.ActiveQuest;
            if (active == null || !FirstShinyRules.ShouldTrigger(active.questId, questManager.ActiveProgress)) return;
            // 대사·포획 창이 떠 있거나 서브에리어(동굴·섬) 안이면 기다린다 — 필드를 걷고 있을 때 일어나야 보인다.
            if (playerMovement != null && playerMovement.IsFrozen) return;
            if (regionManager != null && regionManager.CurrentSubArea != null) return;

            InsectEntity entity = spawner.TryMakeNearbyShiny(playerTransform.position, MinDistance, MaxDistance);
            if (entity == null) return;   // 근처에 맞는 곤충이 없다 — 다음 확인에서 다시 본다

            given = true;
            PlayerPrefs.SetInt(prefsKey, 1);
            PlayerPrefs.Save();

            if (momentFeed != null)
            {
                string name = entity.Data != null ? entity.Data.displayName : "곤충";
                momentFeed.Push(FieldMomentKind.Discovery, "색다른 곤충이 나타났습니다!",
                    $"근처 어딘가에 반짝이는 {name}이(가) 있습니다 — 찾아서 잡아 보세요");
            }
        }
    }
}
