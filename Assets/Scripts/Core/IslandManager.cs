using System;
using System.Collections.Generic;
using System.IO;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>구매·확장이 거부된 이유 — UI가 문구로 바꾼다.</summary>
    public enum IslandBuyResult
    {
        Ok,
        Unknown,
        NotEnoughCoins,
        NotEnoughGems,
        Maxed,
        Locked,
    }

    /// <summary>방목이 거부된 이유.</summary>
    public enum IslandReleaseResult
    {
        Ok,
        NoSuchInsect,
        AlreadyReleased,
        NoFreeSlot,
    }

    /// <summary>
    /// 섬 세이브의 <b>순수</b> 정리 규칙 — 손상·구버전·신버전 혼용에서 스스로 낫는다.
    /// MonoBehaviour와 떼어 놓아 테스트로 고정한다.
    /// </summary>
    public static class IslandSaveRules
    {
        /// <summary>
        /// null 목록·빈 기록·중복을 정리하고 범위를 가둔다. 고친 게 있으면 true.
        ///
        /// <b>카탈로그가 모르는 id는 지우지 않는다</b> — 새 버전에서 산 물건을 구버전이 열었다고 날리면 안 된다.
        /// 그리기·효과·겹침 검사가 그 id를 건너뛸 뿐이다.
        /// </summary>
        public static bool Sanitize(IslandSave save)
        {
            if (save == null) return false;
            bool dirty = false;

            if (save.owned == null) { save.owned = new List<IslandOwnedRecord>(); dirty = true; }
            if (save.placed == null) { save.placed = new List<IslandPlacedRecord>(); dirty = true; }
            if (save.released == null) { save.released = new List<string>(); dirty = true; }
            if (save.bonds == null) { save.bonds = new List<IslandBondRecord>(); dirty = true; }

            int maxSize = GameConstants.Island.MaxSizeLevel;
            int clampedSize = Mathf.Clamp(save.sizeLevel, 0, maxSize);
            if (clampedSize != save.sizeLevel) { save.sizeLevel = clampedSize; dirty = true; }

            int maxExtra = GameConstants.Island.MaxInsectSlots - GameConstants.Island.BaseInsectSlots;
            int clampedExtra = Mathf.Clamp(save.extraSlots, 0, maxExtra);
            if (clampedExtra != save.extraSlots) { save.extraSlots = clampedExtra; dirty = true; }

            // 보유 기록 — 빈 id 제거, 같은 id 합치기, 음수 막기.
            for (int i = save.owned.Count - 1; i >= 0; i--)
            {
                IslandOwnedRecord r = save.owned[i];
                if (r == null || string.IsNullOrEmpty(r.id)) { save.owned.RemoveAt(i); dirty = true; continue; }
                if (r.count < 0) { r.count = 0; dirty = true; }
                for (int j = 0; j < i; j++)
                {
                    IslandOwnedRecord first = save.owned[j];
                    if (first == null || first.id != r.id) continue;
                    first.count += r.count;
                    save.owned.RemoveAt(i);
                    dirty = true;
                    break;
                }
            }

            // 배치 기록 — 빈 id 제거, 회전 정규화.
            for (int i = save.placed.Count - 1; i >= 0; i--)
            {
                IslandPlacedRecord p = save.placed[i];
                if (p == null || string.IsNullOrEmpty(p.id)) { save.placed.RemoveAt(i); dirty = true; continue; }
                int rot = ((p.rot % 4) + 4) % 4;
                if (rot != p.rot) { p.rot = rot; dirty = true; }
            }

            // 놓인 수가 보유 수를 넘으면 보유 수를 올린다 — 놓인 물건을 지우는 쪽보다 안전하다(섬 모양이 유지된다).
            for (int i = 0; i < save.placed.Count; i++)
            {
                string id = save.placed[i].id;
                int placedCount = CountPlaced(save, id);
                IslandOwnedRecord owned = FindOwned(save, id);
                if (owned == null)
                {
                    save.owned.Add(new IslandOwnedRecord { id = id, count = placedCount });
                    dirty = true;
                }
                else if (owned.count < placedCount)
                {
                    owned.count = placedCount;
                    dirty = true;
                }
            }

            // 방목 목록 — 빈 값·중복 제거, 슬롯 수를 넘는 뒤쪽은 버린다.
            for (int i = save.released.Count - 1; i >= 0; i--)
            {
                string id = save.released[i];
                if (string.IsNullOrEmpty(id) || save.released.IndexOf(id) != i) { save.released.RemoveAt(i); dirty = true; }
            }
            int slots = GameConstants.Island.BaseInsectSlots + save.extraSlots;
            if (save.released.Count > slots)
            {
                save.released.RemoveRange(slots, save.released.Count - slots);
                dirty = true;
            }

            for (int i = save.bonds.Count - 1; i >= 0; i--)
            {
                IslandBondRecord b = save.bonds[i];
                if (b == null || string.IsNullOrEmpty(b.instanceId)) { save.bonds.RemoveAt(i); dirty = true; continue; }
                if (b.hours < 0f || float.IsNaN(b.hours)) { b.hours = 0f; dirty = true; }
            }

            // 수치 — NaN·음수는 0으로. 누적 시간은 최대 상한을 넘지 않는다.
            if (!(save.pendingCandy >= 0f)) { save.pendingCandy = 0f; dirty = true; }
            if (!(save.pendingCoin >= 0f)) { save.pendingCoin = 0f; dirty = true; }
            if (!(save.accruedHours >= 0f)) { save.accruedHours = 0f; dirty = true; }
            if (save.accruedHours > GameConstants.Island.MaxCapHours)
            {
                save.accruedHours = GameConstants.Island.MaxCapHours;
                dirty = true;
            }
            if (save.harvestCount < 0) { save.harvestCount = 0; dirty = true; }
            if (save.lastSettleUnix < 0) { save.lastSettleUnix = 0; dirty = true; }
            int maxStep = (int)IslandGuideStep.Done;
            int clampedStep = Mathf.Clamp(save.guideStep, 0, maxStep);
            if (clampedStep != save.guideStep) { save.guideStep = clampedStep; dirty = true; }

            return dirty;
        }

        public static IslandOwnedRecord FindOwned(IslandSave save, string id)
        {
            if (save == null || save.owned == null) return null;
            for (int i = 0; i < save.owned.Count; i++)
                if (save.owned[i] != null && save.owned[i].id == id) return save.owned[i];
            return null;
        }

        public static int CountPlaced(IslandSave save, string id)
        {
            if (save == null || save.placed == null) return 0;
            int n = 0;
            for (int i = 0; i < save.placed.Count; i++)
                if (save.placed[i] != null && save.placed[i].id == id) n++;
            return n;
        }

        /// <summary>
        /// 공개용 축약본을 방문자 쪽에서 받았을 때의 정리 — <b>남이 만든 데이터라 믿지 않는다.</b>
        /// 모르는 id·경계 밖·겹치는 배치와 상한을 넘는 곤충을 버린다.
        /// </summary>
        public static void SanitizeSnapshot(IslandSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (snapshot.placed == null) snapshot.placed = new List<IslandPlacedRecord>();
            if (snapshot.insects == null) snapshot.insects = new List<IslandSnapshotInsect>();
            if (snapshot.ownerName == null) snapshot.ownerName = "";
            if (snapshot.ownerName.Length > 24) snapshot.ownerName = snapshot.ownerName.Substring(0, 24);
            snapshot.sizeLevel = Mathf.Clamp(snapshot.sizeLevel, 0, GameConstants.Island.MaxSizeLevel);

            var accepted = new List<IslandPlacedRecord>();
            for (int i = 0; i < snapshot.placed.Count; i++)
            {
                IslandPlacedRecord p = snapshot.placed[i];
                if (p == null) continue;
                p.rot = ((p.rot % 4) + 4) % 4;
                IslandObjectDef def = IslandCatalog.Get(p.id);
                if (IslandGrid.CanPlace(accepted, -1, def, p.x, p.z, p.rot, snapshot.sizeLevel) != IslandPlaceResult.Ok)
                    continue;
                accepted.Add(p);
            }
            snapshot.placed = accepted;

            for (int i = snapshot.insects.Count - 1; i >= 0; i--)
            {
                IslandSnapshotInsect s = snapshot.insects[i];
                if (s == null || string.IsNullOrEmpty(s.insectId)) { snapshot.insects.RemoveAt(i); continue; }
                s.level = Mathf.Clamp(s.level, 1, GameConstants.Leveling.FallbackMaxLevel);
            }
            if (snapshot.insects.Count > GameConstants.Island.MaxInsectSlots)
                snapshot.insects.RemoveRange(GameConstants.Island.MaxInsectSlots,
                    snapshot.insects.Count - GameConstants.Island.MaxInsectSlots);
        }
    }

    /// <summary>
    /// 나의 섬의 상태와 규칙 — 보관함·배치·방목·수확·확장·가이드 진행. 세이브는 island.json.
    /// 3D는 <c>IslandWorldBuilder</c>가, 화면은 <c>UI/Island*</c>가 이 컴포넌트의 이벤트를 보고 그린다.
    /// 싱글턴이 아니다(AutoWire).
    /// </summary>
    public class IslandManager : MonoBehaviour, ICloudReloadable
    {
        [SerializeField] private PlayerInsectCollection insects;
        [SerializeField] private PlayerCurrencyWallet wallet;
        [SerializeField] private PlayerCandyInventory candy;
        [SerializeField] private InsectDatabase database;

        /// <summary>가이드가 수확 단계에 들어설 때 넣어 주는 첫 선물 — 기다리지 않고 수확을 해 보게 한다.</summary>
        internal const float GuideGiftCandy = 10f;
        internal const float GuideGiftCoin = 30f;

        private IslandSave save = new IslandSave();
        private IslandEffects effects;
        private bool effectsDirty = true;
        private readonly List<PlayerInsectData> releasedCache = new List<PlayerInsectData>();
        private bool releasedDirty = true;
        // 시간당 생산량 — HUD가 OnGUI 패스마다(프레임당 2회 이상) 읽는다. 매번 다시 세면 곤충마다 종 DB를
        // 선형으로 훑게 되므로(GetById), 구성·배치·친밀도가 바뀔 때만 다시 계산한다.
        private float cachedCandyRate;
        private float cachedCoinRate;
        private bool ratesDirty = true;

        // 놓인 물건이 바뀌었다 — 효과(쾌적도·설비)와 그걸 곱하는 생산량이 낡았다.
        private void MarkLayoutDirty()
        {
            effectsDirty = true;
            ratesDirty = true;
        }

        // 방목 구성이나 곤충 목록이 바뀌었다 — 방목 캐시와 생산량이 낡았다.
        private void MarkInsectsDirty()
        {
            releasedDirty = true;
            ratesDirty = true;
        }

        // 가이드의 일회성 신호 — 저장하지 않는다(단계 번호만 저장한다).
        private bool guideEditOpened;
        private bool guideShopOpened;
        private bool guideFinishAcknowledged;

        /// <summary>시계. 테스트가 바꿔 끼운다.</summary>
        internal Func<long> Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>
        /// false면 디스크에 쓰지 않는다 — 검수 캡처·테스트 전용. 검수 빌드는 실제 게임과 같은 저장 폴더를 써서,
        /// 끄지 않으면 메모리 fixture의 섬이 이 PC의 진짜 island.json을 덮는다.
        /// </summary>
        internal bool PersistenceEnabled = true;

        /// <summary>검수 캡처·테스트 전용 — 디스크를 거치지 않고 세이브를 통째로 넣는다.</summary>
        internal void LoadForCapture(IslandSave fixture)
        {
            save = fixture ?? new IslandSave();
            IslandSaveRules.Sanitize(save);
            MarkLayoutDirty();
            MarkInsectsDirty();
        }

        /// <summary>무엇이든 바뀌었다 — 화면이 다시 읽는다.</summary>
        public event Action Changed;
        /// <summary>놓인 물건이나 섬 크기가 바뀌었다 — 월드를 다시 짓는다.</summary>
        public event Action LayoutChanged;
        /// <summary>방목 곤충 구성이 바뀌었다 — 돌아다니는 곤충을 다시 세운다.</summary>
        public event Action InsectsChanged;
        /// <summary>수확했다(캔디, 코인).</summary>
        public event Action<int, int> Harvested;

        public void AutoWire(PlayerInsectCollection collection, PlayerCurrencyWallet currencyWallet,
            PlayerCandyInventory candyInventory, InsectDatabase insectDatabase)
        {
            if (insects != null) insects.InsectUpdated -= OnInsectUpdated;
            if (insects == null) insects = collection;
            if (wallet == null) wallet = currencyWallet;
            if (candy == null) candy = candyInventory;
            if (database == null) database = insectDatabase;
            if (insects != null) insects.InsectUpdated += OnInsectUpdated;
            MarkInsectsDirty();
        }

        private void Awake()
        {
            // 읽기만 한다. 로그인 전이라 전역 경로를 읽을 수 있는데, 여기서 저장까지 하면 그 전역 파일을
            // 만들어 버린다 — 진짜 값은 로그인 뒤 ReloadFromDisk가 계정 경로에서 다시 읽는다.
            save = Load();
            IslandSaveRules.Sanitize(save);
        }

        private void OnEnable()
        {
            if (insects == null) return;
            insects.InsectUpdated -= OnInsectUpdated;
            insects.InsectUpdated += OnInsectUpdated;
        }

        private void OnDisable()
        {
            if (insects != null) insects.InsectUpdated -= OnInsectUpdated;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SettleAndSave();
        }

        private void OnApplicationQuit()
        {
            SettleAndSave();
        }

        // 곤충 목록이 통째로 바뀌면(클라우드 리로드) 방목 캐시가 낡는다. 개별 갱신(레벨업 등)은 등급이 안 바뀌어 무시.
        private void OnInsectUpdated(PlayerInsectData data)
        {
            if (data != null) return;
            MarkInsectsDirty();
            InsectsChanged?.Invoke();
            Changed?.Invoke();
        }

        public void ReloadFromDisk()
        {
            save = Load();
            IslandSaveRules.Sanitize(save);
            MarkLayoutDirty();
            MarkInsectsDirty();
            LayoutChanged?.Invoke();
            InsectsChanged?.Invoke();
            Changed?.Invoke();
        }

        // ── 조회 ──

        /// <summary>섬이 열렸는가 — 「곤충 수집가」 완료 뒤. 마스터 계정은 바로 연다.</summary>
        public bool IsUnlocked
        {
            get
            {
                if (IsMaster) return true;
                TutorialQuestManager quests = TutorialQuestManager.Instance;
                return quests != null && quests.IsQuestCompleted(GameConstants.Island.UnlockQuestId);
            }
        }

        private static bool IsMaster =>
            AuthManager.Instance != null && AuthManager.Instance.MasterPrivilegesActive;

        public int SizeLevel => save.sizeLevel;
        public int GridSize => IslandGrid.GridSize(save.sizeLevel);
        public int InsectSlots => GameConstants.Island.BaseInsectSlots + save.extraSlots;
        public int ExtraSlots => save.extraSlots;
        public IReadOnlyList<IslandPlacedRecord> Placed => save.placed;
        public int HarvestCount => save.harvestCount;
        public bool IsPublic => save.isPublic;

        public IslandEffects Effects
        {
            get
            {
                if (effectsDirty)
                {
                    effects = IslandYield.CollectEffects(save.placed);
                    effectsDirty = false;
                }
                return effects;
            }
        }

        public float CapHours => IslandYield.CapHours(Effects);

        /// <summary>섬에 한 번이라도 들어가 봤는가(스타터 키트를 받은 시점).</summary>
        public bool HasEnteredOnce => save.starterGranted;

        /// <summary>수확물이 상한까지 찼는가 — 곤충이 한 마리도 없으면 찰 것이 없으니 false.</summary>
        public bool IsHarvestFull
        {
            get
            {
                if (ReleasedInsects.Count == 0) return false;
                float cap = CapHours;
                float dt = IslandYield.SettleHours(save.lastSettleUnix, Clock(), save.accruedHours, cap, out _);
                return save.accruedHours + dt >= cap - 0.01f;
            }
        }

        public int GetOwnedCount(string id)
        {
            IslandOwnedRecord r = IslandSaveRules.FindOwned(save, id);
            return r != null ? r.count : 0;
        }

        public int GetPlacedCount(string id) => IslandSaveRules.CountPlaced(save, id);

        /// <summary>보관함에 남은 수 — 보유 총수에서 놓인 수를 뺀 값.</summary>
        public int GetStorageCount(string id) => Mathf.Max(0, GetOwnedCount(id) - GetPlacedCount(id));

        /// <summary>방목 중이면서 지금도 보유한 곤충. 보유 목록에서 사라진 id는 여기서 걸러진다(세이브에서 지우지는 않는다).</summary>
        public IReadOnlyList<PlayerInsectData> ReleasedInsects
        {
            get
            {
                if (releasedDirty)
                {
                    releasedCache.Clear();
                    if (insects != null)
                    {
                        for (int i = 0; i < save.released.Count; i++)
                        {
                            PlayerInsectData data = insects.GetByInstanceId(save.released[i]);
                            if (data != null) releasedCache.Add(data);
                        }
                    }
                    releasedDirty = false;
                }
                return releasedCache;
            }
        }

        public bool IsReleased(string instanceId)
            => !string.IsNullOrEmpty(instanceId) && save.released.Contains(instanceId);

        public float GetBondHours(string instanceId)
        {
            IslandBondRecord b = FindBond(instanceId);
            return b != null ? b.hours : 0f;
        }

        public int GetBondLevel(string instanceId) => IslandYield.BondLevel(GetBondHours(instanceId));

        /// <summary>지금 구성의 시간당 생산량.</summary>
        public void GetRates(out float candyPerHour, out float coinPerHour)
        {
            IReadOnlyList<PlayerInsectData> list = ReleasedInsects;
            if (ratesDirty)
            {
                cachedCandyRate = 0f;
                cachedCoinRate = 0f;
                IslandEffects e = Effects;
                for (int i = 0; i < list.Count; i++)
                {
                    int bond = GetBondLevel(list[i].instanceId);
                    cachedCandyRate += IslandYield.CandyPerHour(RarityOf(list[i]), bond, e);
                    cachedCoinRate += IslandYield.CoinPerHour(bond, e);
                }
                ratesDirty = false;
            }
            candyPerHour = cachedCandyRate;
            coinPerHour = cachedCoinRate;
        }

        /// <summary>
        /// 지금 수확하면 받을 양을 <b>저장을 건드리지 않고</b> 계산한다 — HUD가 매 프레임 읽는다.
        /// </summary>
        public void Peek(out float pendingCandy, out float pendingCoin, out float accruedHours, out float capHours)
        {
            capHours = CapHours;
            float dt = IslandYield.SettleHours(save.lastSettleUnix, Clock(), save.accruedHours, capHours, out _);
            GetRates(out float candyRate, out float coinRate);
            pendingCandy = save.pendingCandy + candyRate * dt;
            pendingCoin = save.pendingCoin + coinRate * dt;
            accruedHours = save.accruedHours + dt;
        }

        private InsectRarity RarityOf(PlayerInsectData data)
        {
            InsectData def = database != null && data != null ? database.GetById(data.insectId) : null;
            return def != null ? def.rarity : InsectRarity.Common;
        }

        // ── 정산 ──

        /// <summary>
        /// 흐른 시간만큼 수확물과 친밀도를 쌓는다. <b>생산에 영향을 주는 변경 전에 반드시 먼저 부른다</b> —
        /// 안 그러면 방금 놓은 온실이 지난 여덟 시간에도 있었던 것처럼 계산된다.
        /// </summary>
        public void Settle()
        {
            float cap = CapHours;
            float dt = IslandYield.SettleHours(save.lastSettleUnix, Clock(), save.accruedHours, cap, out long next);
            save.lastSettleUnix = next;
            if (dt <= 0f) return;

            GetRates(out float candyRate, out float coinRate);
            save.pendingCandy += candyRate * dt;
            save.pendingCoin += coinRate * dt;
            save.accruedHours += dt;

            float bondGain = dt * (1f + Mathf.Max(0f, Effects.bondSpeed));
            IReadOnlyList<PlayerInsectData> list = ReleasedInsects;
            for (int i = 0; i < list.Count; i++)
                GetOrCreateBond(list[i].instanceId).hours += bondGain;
            // 친밀도가 올랐으면 하트 단계가 바뀌었을 수 있다 — 생산량을 다시 센다.
            ratesDirty = true;
        }

        private void SettleAndSave()
        {
            if (!HasAnythingToPersist()) return;
            Settle();
            Save();
        }

        // 한 번도 섬에 들어간 적 없는 세이브에 빈 island.json을 만들지 않는다.
        private bool HasAnythingToPersist()
        {
            return save.starterGranted || save.owned.Count > 0 || save.released.Count > 0 || save.lastSettleUnix > 0;
        }

        /// <summary>쌓인 수확물을 받는다. 캔디·코인 어느 쪽도 1에 못 미치면 false.</summary>
        public bool Harvest(out int candyGain, out int coinGain)
        {
            Settle();
            candyGain = Mathf.FloorToInt(save.pendingCandy);
            coinGain = Mathf.FloorToInt(save.pendingCoin);
            if (candyGain <= 0 && coinGain <= 0) return false;

            // 소수점 아래는 남긴다 — 버리면 자주 수확할수록 손해를 본다.
            save.pendingCandy -= candyGain;
            save.pendingCoin -= coinGain;
            save.accruedHours = 0f;
            save.harvestCount++;
            // 지급보다 먼저 저장한다 — 지급 뒤 저장 전에 죽으면 같은 수확물을 다시 받는다.
            Save();

            // 지급처가 없으면 수확물이 사라진다 — 배선 누락을 조용히 넘기지 않는다(퀘스트 보상 지급과 같은 방식).
            if (candyGain > 0)
            {
                if (candy != null) candy.AddCandy(candyGain);
                else Debug.LogWarning($"[Island] candy null — 수확 캔디 손실 (+{candyGain})");
            }
            if (coinGain > 0)
            {
                if (wallet != null) wallet.AddCoins(coinGain);
                else Debug.LogWarning($"[Island] wallet null — 수확 코인 손실 (+{coinGain})");
            }

            TutorialQuestManager quests = TutorialQuestManager.Instance;
            if (quests != null) quests.NotifyIslandHarvested();

            Harvested?.Invoke(candyGain, coinGain);
            AdvanceGuide();
            Changed?.Invoke();
            return true;
        }

        // ── 구매 ──

        /// <summary>섬 크기 단계가 모자라 아직 상점에 안 뜨는 물건인가.</summary>
        public bool IsLockedBySize(IslandObjectDef def) => def != null && def.requiredSizeLevel > save.sizeLevel;

        public IslandBuyResult TryBuy(string id)
        {
            IslandObjectDef def = IslandCatalog.Get(id);
            if (def == null) return IslandBuyResult.Unknown;
            if (IsLockedBySize(def)) return IslandBuyResult.Locked;

            IslandBuyResult paid = Pay(def.coinPrice, def.gemPrice, def.IsPremium);
            if (paid != IslandBuyResult.Ok) return paid;

            AddOwned(id, 1);
            Save();
            ConfirmGemSpend(def.IsPremium);

            TutorialQuestManager quests = TutorialQuestManager.Instance;
            if (quests != null) quests.NotifyIslandPurchase();

            Changed?.Invoke();
            return IslandBuyResult.Ok;
        }

        public IslandBuyResult TryExpandSize(bool useGems)
        {
            if (!IslandCatalog.SizePrice(save.sizeLevel, out int coins, out int gems)) return IslandBuyResult.Maxed;
            IslandBuyResult paid = Pay(coins, gems, useGems);
            if (paid != IslandBuyResult.Ok) return paid;

            save.sizeLevel++;
            Save();
            ConfirmGemSpend(useGems);
            LayoutChanged?.Invoke();
            Changed?.Invoke();
            return IslandBuyResult.Ok;
        }

        public IslandBuyResult TryBuySlot(bool useGems)
        {
            if (!IslandCatalog.SlotPrice(save.extraSlots, out int coins, out int gems)) return IslandBuyResult.Maxed;
            IslandBuyResult paid = Pay(coins, gems, useGems);
            if (paid != IslandBuyResult.Ok) return paid;

            save.extraSlots++;
            Save();
            ConfirmGemSpend(useGems);
            Changed?.Invoke();
            return IslandBuyResult.Ok;
        }

        private IslandBuyResult Pay(int coins, int gems, bool useGems)
        {
            if (IsMaster) return IslandBuyResult.Ok;
            if (wallet == null) return useGems ? IslandBuyResult.NotEnoughGems : IslandBuyResult.NotEnoughCoins;
            if (useGems)
                return gems > 0 && wallet.SpendGems(gems) ? IslandBuyResult.Ok : IslandBuyResult.NotEnoughGems;
            return coins > 0 && wallet.SpendCoins(coins) ? IslandBuyResult.Ok : IslandBuyResult.NotEnoughCoins;
        }

        // SpendGems가 부른 클라우드 저장은 소유권을 적기 **전**의 스냅샷이다. 적은 뒤 한 번 더 올려야
        // "다이아는 빠졌는데 물건은 없는" 문서가 클라우드에 남지 않는다(연속 호출은 pendingSave가 합친다).
        private static void ConfirmGemSpend(bool usedGems)
        {
            if (!usedGems || IsMaster) return;
            CloudSaveManager cloud = CloudSaveManager.Instance;
            if (cloud != null) cloud.SaveToCloud();
        }

        /// <summary>물건을 그냥 준다 — 퀘스트 보상·스타터 키트.</summary>
        public void GrantObject(string id, int count)
        {
            if (count <= 0 || IslandCatalog.Get(id) == null) return;
            AddOwned(id, count);
            Save();
            Changed?.Invoke();
        }

        private void AddOwned(string id, int count)
        {
            IslandOwnedRecord r = IslandSaveRules.FindOwned(save, id);
            if (r == null)
            {
                r = new IslandOwnedRecord { id = id, count = 0 };
                save.owned.Add(r);
            }
            r.count += count;
        }

        // ── 배치 ──

        public IslandPlaceResult CanPlace(string id, int x, int z, int rot, int ignoreIndex = -1)
        {
            return IslandGrid.CanPlace(save.placed, ignoreIndex, IslandCatalog.Get(id), x, z, rot, save.sizeLevel);
        }

        /// <summary>보관함의 물건을 놓는다.</summary>
        public IslandPlaceResult TryPlace(string id, int x, int z, int rot)
        {
            if (GetStorageCount(id) <= 0) return IslandPlaceResult.NotOwned;
            IslandPlaceResult result = CanPlace(id, x, z, rot);
            if (result != IslandPlaceResult.Ok) return result;

            Settle();
            save.placed.Add(new IslandPlacedRecord { id = id, x = x, z = z, rot = ((rot % 4) + 4) % 4 });
            MarkLayoutDirty();
            Save();

            TutorialQuestManager quests = TutorialQuestManager.Instance;
            if (quests != null) quests.NotifyIslandObjectPlaced();

            LayoutChanged?.Invoke();
            AdvanceGuide();
            Changed?.Invoke();
            return IslandPlaceResult.Ok;
        }

        /// <summary>놓인 물건을 옮기거나 돌린다. 퀘스트의 "놓기"로 세지 않는다(옮기기를 반복해 채우지 못하게).</summary>
        public IslandPlaceResult TryMove(int index, int x, int z, int rot)
        {
            if (index < 0 || index >= save.placed.Count) return IslandPlaceResult.UnknownObject;
            IslandPlacedRecord p = save.placed[index];
            IslandPlaceResult result = CanPlace(p.id, x, z, rot, index);
            if (result != IslandPlaceResult.Ok) return result;

            p.x = x;
            p.z = z;
            p.rot = ((rot % 4) + 4) % 4;
            Save();
            LayoutChanged?.Invoke();
            Changed?.Invoke();
            return IslandPlaceResult.Ok;
        }

        /// <summary>놓인 물건을 보관함에 넣는다. 되팔기는 없다 — 다이아 환급은 서버 규칙이 막는다.</summary>
        public bool Store(int index)
        {
            if (index < 0 || index >= save.placed.Count) return false;
            Settle();
            save.placed.RemoveAt(index);
            MarkLayoutDirty();
            Save();
            LayoutChanged?.Invoke();
            Changed?.Invoke();
            return true;
        }

        // ── 방목 ──

        public IslandReleaseResult TryRelease(string instanceId)
        {
            if (insects == null || insects.GetByInstanceId(instanceId) == null) return IslandReleaseResult.NoSuchInsect;
            if (save.released.Contains(instanceId)) return IslandReleaseResult.AlreadyReleased;

            // 보유 목록에서 사라진 id가 슬롯을 차지하고 있으면 지금 치운다 — 사용자가 창을 열어 조작하는
            // 시점이라 곤충 목록이 확실히 로드돼 있다(부트 중에 치우면 로그인 전 빈 목록을 보고 전부 지운다).
            for (int i = save.released.Count - 1; i >= 0; i--)
                if (insects.GetByInstanceId(save.released[i]) == null) save.released.RemoveAt(i);

            if (save.released.Count >= InsectSlots) return IslandReleaseResult.NoFreeSlot;

            Settle();
            save.released.Add(instanceId);
            MarkInsectsDirty();
            Save();

            TutorialQuestManager quests = TutorialQuestManager.Instance;
            if (quests != null) quests.NotifyIslandInsectReleased();

            InsectsChanged?.Invoke();
            AdvanceGuide();
            Changed?.Invoke();
            return IslandReleaseResult.Ok;
        }

        /// <summary>곤충을 섬에서 거둔다. 친밀도는 남는다 — 다시 풀면 이어서 쌓인다.</summary>
        public bool Unrelease(string instanceId)
        {
            if (!save.released.Contains(instanceId)) return false;
            Settle();
            save.released.Remove(instanceId);
            MarkInsectsDirty();
            Save();
            InsectsChanged?.Invoke();
            Changed?.Invoke();
            return true;
        }

        private IslandBondRecord FindBond(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return null;
            for (int i = 0; i < save.bonds.Count; i++)
                if (save.bonds[i].instanceId == instanceId) return save.bonds[i];
            return null;
        }

        private IslandBondRecord GetOrCreateBond(string instanceId)
        {
            IslandBondRecord b = FindBond(instanceId);
            if (b != null) return b;
            b = new IslandBondRecord { instanceId = instanceId, hours = 0f };
            save.bonds.Add(b);
            return b;
        }

        // ── 드나들기 ──

        /// <summary>내 섬에 들어왔다 — 스타터 키트 지급(1회), 밀린 시간 정산, 퀘스트 통지.</summary>
        public void NotifyEnteredOwnIsland()
        {
            if (!save.starterGranted)
            {
                save.starterGranted = true;
                for (int i = 0; i < IslandCatalog.StarterKit.Length; i++)
                    AddOwned(IslandCatalog.StarterKit[i].id, IslandCatalog.StarterKit[i].count);
            }
            Settle();
            Save();

            TutorialQuestManager quests = TutorialQuestManager.Instance;
            if (quests != null) quests.NotifyIslandVisited();

            AdvanceGuide();
            Changed?.Invoke();
        }

        /// <summary>다른 사람의 섬을 구경했다.</summary>
        public void NotifyVisitedFriendIsland()
        {
            TutorialQuestManager quests = TutorialQuestManager.Instance;
            if (quests != null) quests.NotifyFriendIslandVisited();
        }

        public void SetPublic(bool isPublic)
        {
            if (save.isPublic == isPublic) return;
            save.isPublic = isPublic;
            Save();
            Changed?.Invoke();
        }

        // ── 가이드 ──

        public bool GuideActive => !save.guideDone;
        public IslandGuideStep GuideStep => save.guideDone ? IslandGuideStep.Done : (IslandGuideStep)save.guideStep;

        public void ReportEditOpened() { guideEditOpened = true; AdvanceGuide(); }
        public void ReportShopOpened() { guideShopOpened = true; AdvanceGuide(); }
        public void AcknowledgeGuideFinish() { guideFinishAcknowledged = true; AdvanceGuide(); }

        // 다시 보기 중인가(저장하지 않는다). 다시 보기에서는 이미 한 행동으로 단계를 건너뛰지 않고
        // [다음]으로만 넘긴다 — 건너뛰면 다시 보기를 눌러도 끝 단계만 뜬다.
        private bool guideReplay;

        /// <summary>안내를 처음부터 다시 본다(도움말의 "안내 다시 보기"). 선물은 다시 주지 않는다.</summary>
        public void RestartGuide()
        {
            guideEditOpened = guideShopOpened = guideFinishAcknowledged = false;
            save.guideDone = false;
            save.guideStep = (int)IslandGuideStep.OpenEdit;
            guideReplay = true;
            Save();
            Changed?.Invoke();
        }

        private void AdvanceGuide()
        {
            if (save.guideDone) return;
            var facts = new IslandGuideFacts
            {
                editOpened = guideEditOpened,
                shopOpened = guideShopOpened,
                finishAcknowledged = guideFinishAcknowledged,
                placedCount = guideReplay ? 0 : save.placed.Count,
                releasedCount = guideReplay ? 0 : ReleasedInsects.Count,
                harvestCount = guideReplay ? 0 : save.harvestCount,
                ownedInsectCount = insects != null ? insects.OwnedView.Count : 0,
            };
            // 다시 보기에서는 [다음]으로만 넘긴다(AdvanceGuideReplay).
            IslandGuideStep before = (IslandGuideStep)save.guideStep;
            IslandGuideStep after = guideReplay ? before : IslandGuideSteps.Advance(before, facts);
            ApplyGuideStep(before, after);
        }

        /// <summary>다시 보기에서 [다음]을 눌렀다 — 한 단계 넘긴다.</summary>
        public void AdvanceGuideReplay()
        {
            if (save.guideDone || !guideReplay) return;
            IslandGuideStep before = (IslandGuideStep)save.guideStep;
            ApplyGuideStep(before, (IslandGuideStep)Mathf.Min((int)IslandGuideStep.Done, (int)before + 1));
        }

        /// <summary>
        /// 다시 보기를 중간에 그만둔다(안내 배너의 ✕). 첫 안내에서는 아무것도 하지 않는다 — 그쪽 단계는 실제 행동으로만
        /// 넘어가고, 배너를 닫는 건 화면에서 치우는 것뿐이다.
        /// </summary>
        public void EndGuideReplay()
        {
            if (save.guideDone || !guideReplay) return;
            ApplyGuideStep((IslandGuideStep)save.guideStep, IslandGuideStep.Done);
        }

        public bool GuideIsReplay => guideReplay;

        private void ApplyGuideStep(IslandGuideStep before, IslandGuideStep after)
        {
            if (after == before) return;

            // 수확 단계에 처음 들어설 때 선물을 넣어 둔다 — 곤충 한 마리로는 캔디 1개에 네 시간이 걸려
            // "수확을 눌러 보세요"가 성립하지 않는다. 다시 보기에서는 주지 않는다.
            if (!guideReplay && before < IslandGuideStep.Harvest && after >= IslandGuideStep.Harvest
                && save.harvestCount == 0)
            {
                save.pendingCandy += GuideGiftCandy;
                save.pendingCoin += GuideGiftCoin;
            }

            save.guideStep = (int)after;
            if (after == IslandGuideStep.Done)
            {
                save.guideDone = true;
                guideReplay = false;
            }
            Save();
            Changed?.Invoke();
        }

        // ── 공유 ──

        /// <summary>공개용 축약본. instanceId·재화는 싣지 않는다.</summary>
        public IslandSnapshot BuildSnapshot(string ownerName)
        {
            var snapshot = new IslandSnapshot
            {
                ownerName = ownerName ?? "",
                sizeLevel = save.sizeLevel,
                comfort = Effects.comfort,
            };
            for (int i = 0; i < save.placed.Count; i++)
            {
                IslandPlacedRecord p = save.placed[i];
                if (IslandCatalog.Get(p.id) == null) continue;
                snapshot.placed.Add(new IslandPlacedRecord { id = p.id, x = p.x, z = p.z, rot = p.rot });
            }
            IReadOnlyList<PlayerInsectData> list = ReleasedInsects;
            for (int i = 0; i < list.Count; i++)
            {
                snapshot.insects.Add(new IslandSnapshotInsect
                {
                    insectId = list[i].insectId,
                    level = list[i].level,
                    shiny = list[i].isShiny,
                });
            }
            return snapshot;
        }

        // ── 저장 ──

        private IslandSave Load()
        {
            string path = GetPath();
            if (!File.Exists(path)) return new IslandSave();
            try
            {
                return JsonUtility.FromJson<IslandSave>(File.ReadAllText(path)) ?? new IslandSave();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Island] 손상된 세이브 — 기본 섬으로 시작: {e.Message}");
                return new IslandSave();
            }
        }

        private void Save()
        {
            if (save == null || !PersistenceEnabled) return;
            // 컴팩트 JSON — 이 파일이 통째로 Firestore 문서의 문자열 필드 하나가 된다(문서 한도 1MiB를 곤충 블롭과 나눠 쓴다).
            AtomicFileWriter.WriteAllText(GetPath(), JsonUtility.ToJson(save, false));
        }

        private static string GetPath() => SaveScope.FilePath(GameConstants.SaveFiles.Island);
    }
}
