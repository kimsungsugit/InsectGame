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
    public class NpcDuelController : MonoBehaviour
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

        // 진행 중인 대결의 상대 — DuelEnded에서 보상·쿨다운을 걸 대상.
        private CatcherKidNpc activeKid;
        private InsectRarity activeRarity = InsectRarity.Common;

        /// <summary>직전 대결 결과 문구 — WorldInteractionController가 잠깐 띄운다.</summary>
        public string LastResultText { get; private set; } = string.Empty;
        public float LastResultTime { get; private set; } = float.MinValue;

        public void AutoWire(InsectBattleController battle, BattleTeamManager team,
            PlayerInsectCollection col, InsectDatabase db, PlayerItemInventory inventory,
            ItemDatabase itemDb, RegionManager region)
        {
            if (itemDatabase == null) itemDatabase = itemDb;
            if (battleController == null && battle != null)
            {
                battleController = battle;
                battleController.DuelEnded += OnDuelEnded;
            }
            if (teamManager == null) teamManager = team;
            if (collection == null) collection = col;
            if (database == null) database = db;
            if (itemInventory == null) itemInventory = inventory;
            if (regionManager == null) regionManager = region;
        }

        private void OnDestroy()
        {
            if (battleController != null)
                battleController.DuelEnded -= OnDuelEnded;
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
            activeRarity = kid.DuelInsect.rarity;
            return true;
        }

        private void OnDuelEnded(bool playerWon)
        {
            CatcherKidNpc kid = activeKid;
            activeKid = null;
            if (kid != null) kid.MarkDuelFinished(Time.time, DuelCooldownSeconds);

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
            return item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : itemId;
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
            LastResultTime = Time.time;
        }
    }
}
