using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.NPC
{
    /// <summary>
    /// 곤충 잡는 아이와의 1v1 대결 조율자.
    ///
    /// 아이가 방금 잡은 곤충이 그대로 상대가 된다(<see cref="CatcherKidNpc.DuelInsect"/>).
    /// 아직 아무것도 못 잡은 아이는 현재 리전 풀에서 한 마리를 배정받는다 — 그래야 게임을
    /// 시작하자마자 만난 아이도 도전 대상이 된다.
    ///
    /// 대결은 야생 전투가 아니므로 <see cref="InsectBattleController.StartDuel"/>로 들어간다
    /// (포획 롤·야생 아이템 드랍 없음). 승리 아이템은 여기서 준다.
    /// </summary>
    public class NpcDuelController : MonoBehaviour, ICloudReloadable
    {
        // 결과와 무관하게 같은 아이에게 다시 도전하기까지의 대기 시간. 연속 파밍 차단.
        private const float DuelCooldownSeconds = 90f;
        // 상대 레벨은 플레이어 대표 곤충 레벨 ±LevelSpread 범위에서 정해진다 — 일방적 승부 방지.
        private const int LevelSpread = 2;

        [SerializeField] private InsectBattleController battleController;
        [SerializeField] private BattleTeamManager teamManager;
        [SerializeField] private PlayerInsectCollection collection;
        [SerializeField] private InsectDatabase database;
        [SerializeField] private PlayerItemInventory itemInventory;
        [SerializeField] private ItemDatabase itemDatabase;
        [SerializeField] private RegionManager regionManager;
        [SerializeField] private RegionBlightManager blight;

        // 진행 중인 대결의 상대 — DuelEnded에서 보상·쿨다운을 걸 대상.
        private CatcherKidNpc activeKid;
        private InsectRarity activeRarity = InsectRarity.Common;

        // 명부회 간부 보스 대결 — 아이 대결과 완료 처리가 달라 별도 상태로 둔다.
        // 비어 있지 않으면 진행 중인 대결이 보스전이라는 뜻이다(activeKid는 그때 null).
        private string activeBossId = string.Empty;
        private readonly System.Collections.Generic.HashSet<string> defeatedBosses =
            new System.Collections.Generic.HashSet<string>();
        // 패배 후 재도전 대기 — 보스별 해제 시각(Time.time 기준).
        private readonly System.Collections.Generic.Dictionary<string, float> bossRetryAt =
            new System.Collections.Generic.Dictionary<string, float>();
        private bool bossStateLoaded;

        private static string DefeatedBossKey => SaveScope.PrefsKey("InsectGame.DefeatedLedgerBosses");

        /// <summary>직전 대결 결과 문구 — <see cref="TryConsumeResult"/>로 꺼내 간다.</summary>
        public string LastResultText { get; private set; } = string.Empty;
        private bool hasPendingResult;

        public void AutoWire(InsectBattleController battle, BattleTeamManager team,
            PlayerInsectCollection col, InsectDatabase db, PlayerItemInventory inventory,
            ItemDatabase itemDb, RegionManager region)
        {
            if (itemDatabase == null) itemDatabase = itemDb;
            if (battleController == null && battle != null) battleController = battle;
            // **구독을 대입 분기 밖에 둔다.** 안에 있으면 인스펙터로 필드를 채운 인스턴스나
            // 두 번째 AutoWire(다른 battleController)는 영영 구독되지 않아 대결이 끝나도
            // 보상·쿨다운이 돌지 않는다. `-=` 뒤 `+=`라 중복 구독은 되지 않는다.
            if (battleController != null)
            {
                battleController.DuelEnded -= OnDuelEnded;
                battleController.DuelEnded += OnDuelEnded;
            }
            if (teamManager == null) teamManager = team;
            if (collection == null) collection = col;
            if (database == null) database = db;
            if (itemInventory == null) itemInventory = inventory;
            if (regionManager == null) regionManager = region;
        }

        /// <summary>
        /// 오염 거점 상태. 별도 오버로드로 둔 것은 위 AutoWire가 이미 인자 7개라 더 늘리면
        /// 호출부에서 순서를 틀리기 쉽기 때문이다(TutorialQuestManager가 같은 방식을 쓴다).
        /// </summary>
        public void AutoWire(RegionBlightManager blightManager)
        {
            if (blight == null) blight = blightManager;
        }

        private void OnDestroy()
        {
            if (battleController != null)
                battleController.DuelEnded -= OnDuelEnded;
        }

        // ── 명부회 간부 보스 대결 ──

        private void EnsureBossState()
        {
            if (bossStateLoaded) return;
            bossStateLoaded = true;
            string csv = PlayerPrefs.GetString(DefeatedBossKey, string.Empty);
            if (string.IsNullOrEmpty(csv)) return;
            foreach (string id in csv.Split(','))
            {
                string trimmed = id.Trim();
                if (trimmed.Length > 0) defeatedBosses.Add(trimmed);
            }
        }

        private void SaveBossState()
        {
            PlayerPrefs.SetString(DefeatedBossKey, string.Join(",", defeatedBosses));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 클라우드 로드가 PlayerPrefs를 갈아끼운 뒤 인메모리 격파 기록을 다시 읽는다.
        /// 이게 없으면 다른 기기에서 이긴 간부가 이 기기에선 여전히 미격파로 남아
        /// 같은 보스와 다시 싸우고 보상도 다시 받는다(RegionManager의 해금 상태와 같은 이유).
        /// </summary>
        public void ReloadFromDisk()
        {
            defeatedBosses.Clear();
            bossRetryAt.Clear();
            bossStateLoaded = false;
            EnsureBossState();
        }

        /// <summary>이 간부를 이미 이겼는가 — 이겼으면 다시 도전할 수 없다(대사만 남는다).</summary>
        public bool IsBossDefeated(string storyNpcId)
        {
            EnsureBossState();
            return !string.IsNullOrEmpty(storyNpcId) && defeatedBosses.Contains(storyNpcId);
        }

        /// <summary>
        /// 이 인물이 맡은 거점이 <b>지금 서 있는 리전</b>에서 아직 살아 있는가 — 재도전 예외의 조건.
        /// </summary>
        private bool CanRematchForBlight(string storyNpcId)
        {
            if (blight == null || regionManager == null) return false;
            RegionData here = regionManager.CurrentRegion;
            return here != null && blight.IsBlightBossHere(storyNpcId, here.regionId);
        }

        /// <summary>
        /// 지금 이 간부에게 도전할 수 있는가. 표에 없거나 이미 이겼거나 재도전 쿨다운 중이면 false.
        /// WorldInteractionController가 프롬프트 표시 여부 판정에도 그대로 쓴다.
        /// </summary>
        public bool CanBossDuel(string storyNpcId, float time)
        {
            if (battleController == null || database == null) return false;
            if (!NpcBossDuels.TryGet(storyNpcId, out NpcBossDuels.BossDuel duel)) return false;
            // 이미 이긴 상대는 원칙적으로 다시 못 붙는다 — 단 **그자가 맡은 오염 거점이 아직
            // 살아 있는 리전에 서 있다면** 예외다. 이 예외가 없으면 두 하수를 이미 이긴 세이브
            // (2막 진행자 대부분)는 산·유적의 거점을 영영 부수지 못해 기능 자체를 못 본다.
            // 거점이 없는 리전(숲·연못·습지)에서는 예외가 서지 않으므로 옛 동작 그대로다.
            if (IsBossDefeated(storyNpcId) && !CanRematchForBlight(storyNpcId)) return false;
            if (bossRetryAt.TryGetValue(storyNpcId, out float readyAt) && time < readyAt) return false;
            // 상대 곤충이 DB에 없으면(데이터 오타) 프롬프트를 띄우지 않는다 — 눌러도 안 열리는 버튼 방지.
            if (database.GetById(duel.insectId) == null) return false;
            return FindPlayerLeader() != null;
        }

        /// <summary>간부 대결 시작. 성공하면 true — 이후 흐름은 기존 배틀 화면이 처리한다.</summary>
        public bool TryStartBossDuel(string storyNpcId, float time)
        {
            if (!CanBossDuel(storyNpcId, time)) return false;
            NpcBossDuels.TryGet(storyNpcId, out NpcBossDuels.BossDuel duel);

            PlayerInsectData leader = FindPlayerLeader();
            InsectData leaderData = leader != null ? database.GetById(leader.insectId) : null;
            InsectData enemyData = database.GetById(duel.insectId);
            if (leaderData == null || enemyData == null) return false;

            InsectSkill[] equipped = collection != null ? collection.GetEquippedSkills(leader) : null;

            // 아이 대결과 달리 레벨을 플레이어에 맞추지 않는다 — 고정 레벨이라야 벽으로 기능한다.
            if (!battleController.StartDuel(
                    leaderData, leader.level, enemyData, duel.level,
                    equippedSkills: equipped, playerPid: leader))
            {
                return false;
            }

            // 장부를 건다 — 이 압박은 명부회 보스전에만 붙는다(아이 대결·야생 전투엔 없다).
            battleController.ArmLedger(duel.ledgerThreshold);

            activeKid = null;
            activeBossId = storyNpcId;
            activeRarity = enemyData.rarity;

            // 보스 테마로 전환 — 간부전과 최종전을 가른다. 전투 종료 시 배틀 화면이
            // 기존 경로로 탐험 BGM을 되돌리므로 여기서 복구를 따로 하지 않는다.
            if (Core.AudioManager.Instance != null)
            {
                Core.AudioManager.Instance.PlayBGM(
                    duel.isFinal ? Core.BgmType.BossFinal : Core.BgmType.BossLedger);
            }
            return true;
        }

        /// <summary>
        /// 이 아이에게 도전할 수 있는지. 아직 곤충이 없으면 배정까지 시도하므로
        /// 스캔에서 그대로 물어봐도 된다(프롬프트 표시 여부 판정 겸용).
        /// </summary>
        public bool CanDuel(CatcherKidNpc kid, float time)
        {
            if (kid == null || battleController == null) return false;
            if (kid.DuelInsect == null) EnsureDuelInsect(kid);
            return kid.CanChallenge(time) && FindPlayerLeader() != null;
        }

        /// <summary>대결 시작. 성공하면 true — 이후 흐름은 기존 배틀 화면이 그대로 처리한다.</summary>
        public bool TryStartDuel(CatcherKidNpc kid, float time)
        {
            if (!CanDuel(kid, time)) return false;

            PlayerInsectData leader = FindPlayerLeader();
            InsectData leaderData = database != null && leader != null
                ? database.GetById(leader.insectId)
                : null;
            if (leaderData == null) return false;

            InsectSkill[] equipped = collection != null ? collection.GetEquippedSkills(leader) : null;
            int enemyLevel = ResolveEnemyLevel(leader.level, kid.DuelLevel);

            if (!battleController.StartDuel(
                    leaderData, leader.level, kid.DuelInsect, enemyLevel,
                    equippedSkills: equipped, playerPid: leader))
            {
                return false;
            }

            activeKid = kid;
            // 보스 ID를 비우는 건 `TryStartBossDuel`이 `activeKid = null`로 하는 것과 대칭이다.
            // 대칭이 깨져 있으면 남은 보스 ID가 `OnDuelEnded`의 분기를 가로채, 아이 대결에 이겼을 뿐인데
            // **간부를 격파한 것으로 영구 기록**되고 보상까지 나간다(PlayerPrefs 저장 + 클라우드 동기).
            // 위쪽 `ConcludeDefeatWithoutSwap`이 상태 누출 자체를 막지만, 방어선을 한 겹 더 둔다 —
            // `DuelEnded`가 어떤 이유로든 빠지면 그 값이 그대로 다음 대결로 흘러가는 구조라서다.
            activeBossId = string.Empty;
            activeRarity = kid.DuelInsect.rarity;
            return true;
        }

        private void OnDuelEnded(bool playerWon)
        {
            // 보스전은 완료 처리가 다르다 — 먼저 갈라내고 아이 대결 경로는 그대로 둔다.
            if (!string.IsNullOrEmpty(activeBossId))
            {
                OnBossDuelEnded(playerWon);
                return;
            }

            CatcherKidNpc kid = activeKid;
            activeKid = null;

            // 이 컨트롤러가 시작하지 않은 듀얼이면 아무것도 하지 않는다. 옛 코드는 그대로
            // 진행해 **초기화된 적 없는 activeRarity**(기본 Common)로 보상을 주고
            // NotifyNpcDuelWon까지 올렸다 — 남이 연 듀얼에 이쪽이 상을 준 셈이다.
            if (kid == null) return;
            kid.MarkDuelFinished(Time.time, DuelCooldownSeconds);

            if (!playerWon)
            {
                SetResult("아이에게 졌다… 다시 도전해 보자");
                return;
            }

            string itemId = RewardItemFor(activeRarity);
            int count = RewardCountFor(activeRarity);
            if (!string.IsNullOrEmpty(itemId) && count > 0 && itemInventory != null)
                itemInventory.AddItem(itemId, count);

            TutorialQuestManager.Instance?.NotifyNpcDuelWon();

            string itemName = ResolveItemName(itemId);
            SetResult(string.IsNullOrEmpty(itemName)
                ? "대결 승리!"
                : $"대결 승리! {itemName} ×{count} 획득");
        }

        private void OnBossDuelEnded(bool playerWon)
        {
            string bossId = activeBossId;
            activeBossId = string.Empty;
            if (!NpcBossDuels.TryGet(bossId, out NpcBossDuels.BossDuel duel)) return;

            if (!playerWon)
            {
                // 패배해도 영구 차단은 하지 않는다 — 쿨다운 뒤 재도전. 벽이지 막다른 길이 아니다.
                bossRetryAt[bossId] = Time.time + duel.retryCooldownSeconds;
                SetResult($"{duel.displayName}에게 밀렸다… 다시 준비하자");
                return;
            }

            EnsureBossState();
            // **첫 승리인지를 기억해 둔다.** 오염 거점 재도전으로 다시 이길 수 있게 되었으므로
            // 이 값으로 보상을 가르지 않으면 같은 아이템을 두 번 준다.
            bool firstWin = defeatedBosses.Add(bossId);
            if (firstWin) SaveBossState();
            bossRetryAt.Remove(bossId);

            if (firstWin && !string.IsNullOrEmpty(duel.rewardItemId) && duel.rewardCount > 0
                && itemInventory != null)
                itemInventory.AddItem(duel.rewardItemId, duel.rewardCount);

            // 간부전도 '동네 최강자' 서브 퀘스트에 센다 — 아이 대결과 같은 1v1 듀얼이다.
            //
            // **여기는 firstWin으로 가르지 않는다**(보상과 비대칭인 게 의도다). 저 퀘스트가 세는
            // 것은 "이긴 상대 수"가 아니라 "이긴 대결 수"이고, 아이 대결도 같은 상대에게 쿨다운
            // 뒤 다시 이기면 다시 센다. 파밍 우려도 없다 — 오염 재도전은 거점당 한 번뿐이고
            // 정화되는 순간 CanBossDuel이 다시 닫힌다.
            TutorialQuestManager.Instance?.NotifyNpcDuelWon();

            // 오염 거점 정화 — **지금 서 있는 리전의 거점을 이 인물이 맡고 있을 때만.**
            // 하수 둘은 거점이 없는 리전에도 서 있어서, 리전만 보거나 인물만 보면 엉뚱한
            // 곳이 정화된다. CleanseByBoss가 둘을 함께 검증하고 idempotent 가드도 든다.
            bool cleansed = blight != null && regionManager != null && regionManager.CurrentRegion != null
                && blight.CleanseByBoss(bossId, regionManager.CurrentRegion.regionId);

            // 보상 문구와 정화 문구를 **함께** 낸다. 나눠서 정화만 띄우면 첫 승리에서
            // 아이템은 실제로 들어왔는데 그 사실을 아무 데서도 알리지 않는다
            // (AddItem은 토스트를 안 띄운다) — 받은 줄도 모르는 보상이 된다.
            string itemName = ResolveItemName(duel.rewardItemId);
            string gained = firstWin && !string.IsNullOrEmpty(itemName)
                ? $" {itemName} ×{duel.rewardCount} 획득"
                : string.Empty;

            if (cleansed)
            {
                RegionData here = regionManager.CurrentRegion;
                string siteName = string.IsNullOrEmpty(here.blightSiteName) ? "거점" : here.blightSiteName;
                SetResult($"{duel.displayName}을(를) 이겼다!{gained} · {siteName}이(가) 무너진다");
                return;
            }

            SetResult($"{duel.displayName}을(를) 이겼다!{gained}");
        }

        // ── 보상 ──
        // "약간의" 보상이므로 소모품 한 줌 수준. 상대 등급이 높을수록 조금 나은 것을 준다.
        // 표는 순수 함수라 테스트가 직접 검증한다(NpcDuelRewardTests).
        public static string RewardItemFor(InsectRarity rarity)
        {
            switch (rarity)
            {
                case InsectRarity.Uncommon: return "net_basic";
                case InsectRarity.Rare: return "net_silver";
                case InsectRarity.Epic:
                case InsectRarity.Legendary: return "net_gold";
                default: return "wound_salve";
            }
        }

        public static int RewardCountFor(InsectRarity rarity)
        {
            switch (rarity)
            {
                case InsectRarity.Common: return 2;
                case InsectRarity.Uncommon: return 2;
                default: return 1;
            }
        }

        private string ResolveItemName(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return string.Empty;
            ItemData item = itemDatabase != null ? itemDatabase.FindById(itemId) : null;
            // **폴백으로 raw ID를 내보내지 않는다.** 표에 없는 ID면 화면에 "net_basic ×2 획득"이
            // 그대로 뜬다. 호출부는 빈 문자열을 "아이템 문구 생략"으로 처리하므로,
            // 이름을 모르면 조용히 빼는 쪽이 맞다(아이템 자체는 인벤토리에 들어간다).
            return item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : string.Empty;
        }

        // ── 대상 선정 ──

        // 팀 슬롯 순서대로 첫 번째 출전 가능(기절 아님) 곤충. 팀이 비었으면 보유 목록에서 찾는다.
        private PlayerInsectData FindPlayerLeader()
        {
            if (collection == null) return null;

            if (teamManager != null)
            {
                for (int i = 0; i < BattleTeamManager.MaxSlots; i++)
                {
                    string id = teamManager.GetSlot(i);
                    if (string.IsNullOrEmpty(id)) continue;
                    PlayerInsectData pid = collection.GetByInstanceId(id);
                    if (pid != null && !pid.IsFainted) return pid;
                }
            }

            System.Collections.Generic.List<PlayerInsectData> owned = collection.GetAllOwned();
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] != null && !owned[i].IsFainted) return owned[i];
            }
            return null;
        }

        // 아직 잡은 게 없는 아이에게 현재 리전 풀에서 한 마리를 배정한다.
        // 아이 인스턴스 ID로 결정적으로 고르므로 같은 아이는 항상 같은 곤충을 들고 있다.
        private void EnsureDuelInsect(CatcherKidNpc kid)
        {
            if (kid == null || kid.DuelInsect != null || database == null) return;

            string[] pool = regionManager != null && regionManager.CurrentRegion != null
                ? regionManager.CurrentRegion.insectIds
                : null;
            if (pool == null || pool.Length == 0) return;

            for (int attempt = 0; attempt < pool.Length; attempt++)
            {
                InsectData data = database.GetById(pool[PoolIndexFor(kid.NpcId, pool.Length, attempt)]);
                if (data == null) continue;
                PlayerInsectData leader = FindPlayerLeader();
                kid.SetDuelInsect(data, leader != null ? leader.level : 1);
                return;
            }
        }

        /// <summary>
        /// 상대 레벨 — 플레이어 대표 곤충 기준 ±<see cref="LevelSpread"/> 안으로 좁힌다.
        /// 아이가 잡은 곤충이 너무 약하거나(초반 필드) 세도 승부가 나게 한다.
        /// </summary>
        public static int ResolveEnemyLevel(int playerLevel, int caughtLevel)
        {
            int lo = Mathf.Max(1, playerLevel - LevelSpread);
            int hi = Mathf.Max(lo, playerLevel + LevelSpread);
            return Mathf.Clamp(caughtLevel, lo, hi);
        }

        /// <summary>
        /// 아이 ID로 결정적으로 고른 리전 풀 인덱스. 항상 <c>[0, poolLength)</c> 안이다.
        ///
        /// <c>Mathf.Abs(int.MinValue)</c>는 그 자신(음수)이라 오버플로한다 — 그대로 쓰면
        /// <c>pool[음수]</c>로 즉사하므로 long으로 받아 양수화한다.
        /// <c>InsectSizeCalculator.RollFromInstanceId</c>·<c>NpcDialogueDatabase.Mod</c>가
        /// 같은 자리에서 이미 쓰는 방어이고, 여기만 빠져 있었다.
        /// </summary>
        public static int PoolIndexFor(string npcId, int poolLength, int attempt)
        {
            if (poolLength <= 0) return 0;
            int hash = StableHash(npcId);
            long seed = hash < 0 ? -(long)hash : hash;
            return (int)((seed + Mathf.Max(0, attempt)) % poolLength);
        }

        // string.GetHashCode는 런타임마다 값이 달라 배정이 세션마다 바뀐다(FNV-1a로 고정).
        private static int StableHash(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            unchecked
            {
                int hash = (int)2166136261;
                for (int i = 0; i < value.Length; i++)
                    hash = (hash ^ value[i]) * 16777619;
                return hash;
            }
        }

        private void SetResult(string text)
        {
            LastResultText = text;
            hasPendingResult = true;
        }

        /// <summary>
        /// 결과 문구를 <b>한 번만</b> 꺼내 간다 — 화면에 올리는 쪽이 자기 타이머로 표시한다.
        ///
        /// 옛 구현은 <c>SetResult</c> 시각을 찍고 소비자가 3.5초 창을 봤는데, 그 시각은
        /// <c>DuelEnded</c> 시점(= 전투 결과 화면이 열리는 프레임)이다. 결과 화면은 4초를
        /// 채운 뒤에야 닫히고(<c>BattleScreenUI</c>의 <c>resultTimer &gt; 4f</c>) 그동안은
        /// 모달이라 필드가 그리지도 않는다 — **토스트는 필드가 보이기 0.5초 전에 이미 죽었다.**
        /// 승리 아이템도, 거점이 무너졌다는 문구도 플레이어에게 도달한 적이 없다.
        /// </summary>
        public bool TryConsumeResult(out string text)
        {
            text = LastResultText;
            if (!hasPendingResult || string.IsNullOrEmpty(text)) return false;
            hasPendingResult = false;
            return true;
        }
    }
}
