using InsectGame.Core;
using InsectGame.UI;
using UnityEngine;

namespace InsectGame.Story
{
    /// <summary>라온과의 포획 내기 — <b>순수</b> 규칙. 시간표와 보상을 여기 한곳에 둔다.</summary>
    public static class RivalRaceRules
    {
        /// <summary>먼저 이만큼 잡는 쪽이 이긴다.</summary>
        public const int Target = 3;

        // 라온이 한 마리씩 잡는 시각 — 플레이어가 **필드에서 자유롭게 움직인 시간**(초)으로 잰다.
        // 대사·메뉴·미니게임 중에는 흐르지 않으므로 창을 읽느라 지는 일은 없다.
        // 곤충 한 마리에 15초쯤 쓰면 45초에 끝나 라온은 한 마리, 30초씩 쓰면 90초에 따라잡힌다.
        private const float FirstCatch = 25f;
        private const float SecondCatch = 55f;
        private const float ThirdCatch = 90f;

        public const int WinCandy = 15;
        public const string WinItemId = "net_silver";
        public const int WinItemCount = 2;
        public const int LoseCandy = 5;

        /// <summary>필드 시간이 이만큼 흘렀을 때 라온이 잡은 수(0~<see cref="Target"/>).</summary>
        public static int RivalCountAt(float fieldSeconds)
        {
            if (fieldSeconds >= ThirdCatch) return 3;
            if (fieldSeconds >= SecondCatch) return 2;
            return fieldSeconds >= FirstCatch ? 1 : 0;
        }

        /// <summary>승부가 났는가. 같은 순간에 둘 다 채웠으면 플레이어가 이긴다(잡은 쪽이 직접 한 일이다).</summary>
        public static bool IsDecided(int playerCount, int rivalCount, out bool playerWon)
        {
            playerWon = playerCount >= Target;
            return playerWon || rivalCount >= Target;
        }
    }

    /// <summary>
    /// 라온과의 포획 내기. 라온이 "좋아, 승부다! 누가 더 많은 친구를 구해내는지"라고 말하는 비트
    /// (<c>ch1_rival_intro</c>)가 끝나면 시작한다 — 예전엔 그 대사가 말뿐이었고 그 뒤는 혼자 세 마리를 채우는
    /// 숙제였다. 먼저 세 마리를 잡는 쪽이 이기고, 라온은 시간이 흐르면 한 마리씩 잡는다.
    ///
    /// 퀘스트가 아니다. 지든 이기든 본편 진행과 무관하고(스토리는 이 결과를 읽지 않는다) 한 계정에 한 번이다.
    /// 화면(<c>FieldMomentsUI</c>)은 구독 없이 <see cref="IsActive"/>와 두 점수를 읽는다.
    /// </summary>
    public class RivalRaceController : MonoBehaviour
    {
        /// <summary>내기를 여는 비트. 저작 데이터의 ID라 여기 하나에만 둔다.</summary>
        public const string StartBeatId = "ch1_rival_intro";

        private const string StateActive = "active";
        private const string StateDone = "done";

        private StoryDirector storyDirector;
        private PlayerInsectCollection insectCollection;
        private PlayerCandyInventory candyInventory;
        private PlayerItemInventory itemInventory;
        private PlayerMovement playerMovement;
        private FieldMomentFeed momentFeed;

        private bool active;
        private float fieldSeconds;
        // 불러온 계정 스코프 키 — 로그인·계정 전환으로 키가 바뀌면 다시 읽는다(StoryObjectiveTracker와 같은 방식).
        private string prefsKey;
        private string savedState = string.Empty;

        public bool IsActive => active;
        public int PlayerCount { get; private set; }
        public int RivalCount { get; private set; }

        public void AutoWire(StoryDirector director, PlayerInsectCollection collection, PlayerCandyInventory candy,
            PlayerItemInventory items, PlayerMovement movement, FieldMomentFeed feed)
        {
            if (storyDirector == null) storyDirector = director;
            if (insectCollection == null) insectCollection = collection;
            if (candyInventory == null) candyInventory = candy;
            if (itemInventory == null) itemInventory = items;
            if (playerMovement == null) playerMovement = movement;
            if (momentFeed == null) momentFeed = feed;
            Subscribe();
        }

        private void Subscribe()
        {
            if (storyDirector != null)
            {
                storyDirector.StoryBeatCompleted -= OnStoryBeatCompleted;
                storyDirector.StoryBeatCompleted += OnStoryBeatCompleted;
            }
            if (insectCollection != null)
            {
                insectCollection.InsectCaptured -= OnInsectCaptured;
                insectCollection.InsectCaptured += OnInsectCaptured;
            }
        }

        private void OnDestroy()
        {
            if (storyDirector != null) storyDirector.StoryBeatCompleted -= OnStoryBeatCompleted;
            if (insectCollection != null) insectCollection.InsectCaptured -= OnInsectCaptured;
        }

        private void EnsureStateLoaded()
        {
            string key = AuthManager.ScopedKey(GameConstants.PrefsKeys.RivalRace);
            if (key == prefsKey) return;
            prefsKey = key;
            savedState = PlayerPrefs.GetString(key, string.Empty);
            // 계정이 바뀌었다 — 앞 계정의 내기를 들고 넘어가지 않는다.
            active = false;
        }

        private void SaveState(string state)
        {
            savedState = state;
            if (string.IsNullOrEmpty(prefsKey)) return;
            PlayerPrefs.SetString(prefsKey, state);
            PlayerPrefs.Save();
        }

        private void OnStoryBeatCompleted(StoryBeat beat)
        {
            if (beat == null || beat.beatId != StartBeatId) return;
            EnsureStateLoaded();
            if (active || savedState == StateDone) return;
            Begin("라온과의 내기 시작!");
        }

        private void Begin(string title)
        {
            active = true;
            PlayerCount = 0;
            RivalCount = 0;
            fieldSeconds = 0f;
            SaveState(StateActive);
            if (momentFeed != null)
                momentFeed.Push(FieldMomentKind.Rival, title, $"먼저 곤충 {RivalRaceRules.Target}마리를 잡는 쪽이 이깁니다");
        }

        private void OnInsectCaptured(PlayerInsectData insect)
        {
            if (!active) return;
            PlayerCount++;
            Decide();
        }

        // 계정 키 확인 주기. 키 문자열은 호출마다 새로 만들어지므로(AuthManager.ScopedKey) 매 프레임 묻지 않는다 —
        // 로그인·계정 전환을 1초 안에 알아채면 충분하다.
        private const float StateCheckInterval = 1f;
        private float stateCheckTimer;

        private void Update()
        {
            stateCheckTimer -= Time.deltaTime;
            if (stateCheckTimer <= 0f)
            {
                stateCheckTimer = StateCheckInterval;
                EnsureStateLoaded();
            }

            // 내기 도중에 앱을 닫았다 — 0:0에서 다시 붙는다(점수는 저장하지 않는다. 한 번뿐인 짧은 내기다).
            if (!active && savedState == StateActive && CanResume())
                Begin("라온과의 내기가 이어집니다");
            if (!active) return;

            // 라온의 시계는 **플레이어가 필드에서 움직일 수 있을 때만** 간다.
            if (ModalUIRegistry.IsAnyOpen() || (playerMovement != null && playerMovement.IsFrozen)) return;

            fieldSeconds += Time.deltaTime;
            int rival = RivalRaceRules.RivalCountAt(fieldSeconds);
            if (rival <= RivalCount) return;

            RivalCount = rival;
            if (momentFeed != null && rival < RivalRaceRules.Target)
                momentFeed.Push(FieldMomentKind.Rival, $"라온이 {rival}마리째를 잡았습니다",
                    $"나 {PlayerCount} : {rival} 라온");
            Decide();
        }

        // 이어 붙이는 것은 조작이 풀린 필드에서만 — 로그인 직후의 로비·대사 위에 소식을 얹지 않는다.
        private bool CanResume()
        {
            return storyDirector != null && storyDirector.HasSeen(StartBeatId)
                && !ModalUIRegistry.IsAnyOpen()
                && playerMovement != null && !playerMovement.IsFrozen;
        }

        private void Decide()
        {
            if (!RivalRaceRules.IsDecided(PlayerCount, RivalCount, out bool playerWon)) return;

            active = false;
            // **기록을 먼저 닫고 지급한다** — 지급 뒤 저장 전에 죽으면 다음 실행에서 내기가 다시 열려 보상을 또 받는다.
            SaveState(StateDone);

            int candy = playerWon ? RivalRaceRules.WinCandy : RivalRaceRules.LoseCandy;
            if (candyInventory != null) candyInventory.AddCandy(candy);
            else Debug.LogWarning($"[RivalRace] candyInventory null — 캔디 보상 손실 (+{candy})");

            string rewardText = $"캔디 +{candy}";
            if (playerWon)
            {
                if (itemInventory != null) itemInventory.AddItem(RivalRaceRules.WinItemId, RivalRaceRules.WinItemCount);
                else Debug.LogWarning($"[RivalRace] itemInventory null — 아이템 보상 손실 {RivalRaceRules.WinItemId}");
                string itemName = momentFeed != null ? momentFeed.ItemName(RivalRaceRules.WinItemId) : RivalRaceRules.WinItemId;
                rewardText += $" · {itemName} ×{RivalRaceRules.WinItemCount}";
            }

            if (momentFeed != null)
            {
                if (playerWon)
                    momentFeed.Push(FieldMomentKind.Rival, "라온과의 내기에서 이겼습니다!",
                        $"라온: \"…졌다. 다음엔 안 봐줘!\"   {rewardText}");
                else
                    momentFeed.Push(FieldMomentKind.Rival, "라온이 먼저 세 마리를 잡았습니다",
                        $"라온: \"내가 이겼지! 다음엔 더 서둘러.\"   {rewardText}");
            }

            // 한 번뿐인 보상이라 자동저장(120초)을 기다리지 않는다(퀘스트 완료와 같은 취급).
            if (CloudSaveManager.Instance != null) CloudSaveManager.Instance.SaveToCloud();
        }
    }
}
