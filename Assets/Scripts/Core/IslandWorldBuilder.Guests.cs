using System;
using System.Collections.Generic;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 나의 섬 — <b>방목 곤충의 시간·날씨 기분</b>과 <b>손님 곤충</b>(규칙은 <see cref="IslandGuestRules"/>, 문서는 rules/island.md 「손님 곤충」).
    ///
    /// 손님은 <b>진짜 야생 개체</b>다 — <c>InsectEntity.Initialize</c>(야생 경로)로 세우므로 잡기 버튼·포획 선택 창·미니게임·전투·포획 보상·
    /// 도감·퀘스트 통지가 필드와 같은 길을 탄다. 다른 점은 셋뿐이다: ①도주가 섬의 빈 칸을 벗어나지 않는다(<c>SetFleeArea</c>)
    /// ②기록(칸)이 섬 밖으로 나가도 남는다(돌아오면 같은 손님 — 섬을 드나들어 다시 굴리지 못한다) ③세션 간에는 저장하지 않는다.
    ///
    /// 손님 몸은 섬 <c>root</c> 밖(이 컴포넌트 아래)의 작은 풀에 둔다 — 섬을 다시 지을 때(<c>DestroyWorld</c>) 같이 부서지면 붙잡혀 있던
    /// 전투·포획 쪽 참조가 끊긴다. 섬을 허물 때는 몸만 거두고(<c>Recall</c> — 게임플레이 퇴장이 아니다) 기록은 남긴다.
    /// </summary>
    public partial class IslandWorldBuilder
    {
        [Header("Guests & Mood")]
        [SerializeField] private InsectSpawner insectSpawner;
        [SerializeField] private WorldStateProvider worldStateProvider;

        /// <summary>손님 한 칸 — 기록이 곧 개체이고 몸은 섬에 있을 때만 빌려 쓴다(필드 슬롯과 같은 원칙).</summary>
        private sealed class GuestSlot
        {
            public bool Alive;
            public InsectData Data;
            public int Level;
            public bool Shiny;
            /// <summary>비어 있을 때 — 다음 손님이 올 수 있는 시각.</summary>
            public float ReadyAt;
            /// <summary>와 있을 때 — 수명이 다하는 시각.</summary>
            public float ExpiresAt;
            public InsectEntity Body;
            public Vector2Int Cell;
        }

        private readonly GuestSlot[] guestSlots = CreateGuestSlots();
        private readonly List<FieldRegionTable> guestTablesAll = new List<FieldRegionTable>();
        private readonly List<FieldRegionTable> guestTables = new List<FieldRegionTable>();
        private readonly List<InsectData> guestCandidates = new List<InsectData>();
        private readonly List<InsectData> guestScratch = new List<InsectData>();
        private readonly List<float> guestScratchMultipliers = new List<float>();
        private readonly bool[] guestRarityAvailable = new bool[FieldSpawnRules.RarityCount];
        private readonly List<GameObject> guestBodyPool = new List<GameObject>();
        private readonly HashSet<Vector2Int> guestTakenCells = new HashSet<Vector2Int>();
        private Transform guestBodyHolder;
        private float guestNextTick;
        private int guestLastWanted = -1;
        private Func<string, bool> guestAccessibleProbe;
        private Func<Vector2Int, bool> guestCellInView;
        private Func<Vector3, Vector3, float> guestFleeArea;
        private Action<InsectEntity> guestDespawned;
        private Camera guestCamera;

        // 방목 곤충 기분 — 섬은 지역이 없어 세계 날씨다. 시간대·날씨가 바뀔 때만 곤충들에게 알린다.
        private const float MoodCheckSeconds = 1f;
        private float moodNextCheck;
        private bool moodStateKnown;
        private WorldState moodState;

        private static GuestSlot[] CreateGuestSlots()
        {
            var slots = new GuestSlot[IslandGuestRules.MaxGuests];
            for (int i = 0; i < slots.Length; i++) slots[i] = new GuestSlot();
            return slots;
        }

        /// <summary>
        /// 손님·기분이 읽는 것 — 필드 리전 표(풀·레벨 대역, <see cref="InsectSpawner.CopyFieldRegionTables"/>)와 세계 시간·날씨.
        /// 빠지면 손님이 오지 않고 방목 곤충은 예전처럼(시간·날씨 없이) 돌아다닌다.
        /// </summary>
        public void AutoWire(InsectSpawner spawner, WorldStateProvider worldState)
        {
            if (insectSpawner == null) insectSpawner = spawner;
            if (worldStateProvider == null) worldStateProvider = worldState;
        }

        /// <summary>지금 섬에 몸이 선 손님 수(검수·테스트용).</summary>
        public int ActiveGuestCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < guestSlots.Length; i++)
                    if (guestSlots[i].Alive && guestSlots[i].Body != null) n++;
                return n;
            }
        }

        // ── 방목 곤충의 기분 ──

        private void TickWorldMood()
        {
            if (worldStateProvider == null || Time.time < moodNextCheck) return;
            moodNextCheck = Time.time + MoodCheckSeconds;
            WorldState state = worldStateProvider.GetWorldState(null);
            if (moodStateKnown && state.DayPhase == moodState.DayPhase && state.Weather == moodState.Weather) return;
            moodStateKnown = true;
            moodState = state;
            if (insectsRoot == null) return;
            for (int i = 0; i < insectsRoot.transform.childCount; i++)
            {
                IslandInsectWalker walker = insectsRoot.transform.GetChild(i).GetComponent<IslandInsectWalker>();
                if (walker != null) walker.ApplyWorld(state);
            }
        }

        // ── 손님 곤충 ──

        /// <summary>
        /// 섬에 막 들어섰다 — 떠나 있는 동안 조건이 끝났거나 수명이 다한 손님은 <b>이미 떠난 것</b>으로 친다(아무도 못 봤다).
        /// 들어서는 순간엔 도착을 흩지 않는다 — 밤에 섬에 오면 손님이 이미 와 있다.
        /// </summary>
        private void OnIslandEnteredForGuests()
        {
            guestNextTick = 0f;
            if (mode != IslandMode.Own || DreamMode || worldStateProvider == null) return;
            float now = Time.time;
            int wanted = IslandGuestRules.WantedGuests(worldStateProvider.GetWorldState(null));
            guestLastWanted = wanted;
            int alive = 0;
            for (int i = 0; i < guestSlots.Length; i++)
            {
                GuestSlot slot = guestSlots[i];
                if (!slot.Alive) continue;
                if (alive >= wanted || now >= slot.ExpiresAt)
                {
                    slot.Alive = false;
                    slot.Data = null;
                    slot.ReadyAt = now;
                    continue;
                }
                alive++;
            }
        }

        private void TickGuests()
        {
            if (Time.time < guestNextTick) return;
            guestNextTick = Time.time + IslandGuestRules.TickSeconds;
            // 남의 섬·꿈속 섬에서는 내 섬의 손님 기록을 건드리지 않는다(세우지도, 떠나보내지도).
            if (mode != IslandMode.Own || DreamMode || worldStateProvider == null || root == null) return;

            float now = Time.time;
            WorldState state = worldStateProvider.GetWorldState(null);
            int wanted = IslandGuestRules.WantedGuests(state);

            // 조건이 막 시작됐다(밤이 왔다·비가 온다) — 비어 있던 칸의 도착을 5~60초에 흩는다. 한꺼번에 툭 나타나지 않게.
            if (guestLastWanted >= 0 && wanted > guestLastWanted)
            {
                for (int i = 0; i < guestSlots.Length; i++)
                    if (!guestSlots[i].Alive)
                        guestSlots[i].ReadyAt = Mathf.Max(guestSlots[i].ReadyAt,
                            now + FieldSpawnRules.RollPhaseSwapDelay(UnityEngine.Random.value));
            }
            guestLastWanted = wanted;

            // 떠나기 — 조건이 끝나 남거나 수명이 다한 손님은, 붙잡히지·놀라지 않았고 화면 밖일 때 조용히 떠난다.
            int alive = CountAliveGuests();
            for (int i = 0; i < guestSlots.Length; i++)
            {
                GuestSlot slot = guestSlots[i];
                if (!slot.Alive) continue;
                if (!IslandGuestRules.ShouldLeave(alive > wanted, now >= slot.ExpiresAt)) continue;
                if (slot.Body != null)
                {
                    bool busy = slot.Body.IsEngaged || slot.Body.IsAlerted;
                    if (!IslandGuestRules.CanLeaveNow(busy, IsWorldPointInView(slot.Body.transform.position))) continue;
                    RecallGuestBody(slot);
                }
                slot.Alive = false;
                slot.Data = null;
                slot.ReadyAt = now + FieldSpawnRules.RollRespawnDelay(UnityEngine.Random.value);
                alive--;
            }

            // 들어서기 — 꾸미기 중에는 새로 들이지 않는다(꾸미기의 지면 탭과 곤충 탭이 겹치지 않게).
            if (!IslandGuestRules.CanHostGuests(mode, DreamMode, editCameraActive)) return;
            for (int i = 0; i < guestSlots.Length && alive < wanted; i++)
            {
                GuestSlot slot = guestSlots[i];
                if (slot.Alive || now < slot.ReadyAt) continue;
                if (!RollGuest(slot, state))
                {
                    slot.ReadyAt = now + FieldSpawnRules.RetrySeconds;
                    continue;
                }
                slot.Alive = true;
                slot.ExpiresAt = now + FieldSpawnRules.RollLifetime(UnityEngine.Random.value);
                alive++;
            }

            for (int i = 0; i < guestSlots.Length; i++)
                if (guestSlots[i].Alive && guestSlots[i].Body == null) MaterializeGuest(guestSlots[i]);
        }

        private int CountAliveGuests()
        {
            int n = 0;
            for (int i = 0; i < guestSlots.Length; i++) if (guestSlots[i].Alive) n++;
            return n;
        }

        /// <summary>새 손님의 종·레벨·색다름을 굴린다 — 해금된 리전 풀만 본다. 표가 없거나 후보가 비면 false(잠시 뒤 다시).</summary>
        private bool RollGuest(GuestSlot slot, WorldState state)
        {
            if (insectSpawner == null || regionManager == null) return false;
            insectSpawner.CopyFieldRegionTables(guestTablesAll);
            if (guestAccessibleProbe == null) guestAccessibleProbe = IsRegionIdAccessible;
            IslandGuestRules.FilterAccessible(guestTablesAll, guestAccessibleProbe, guestTables);
            if (guestTables.Count == 0) return false;

            IslandGuestRules.CollectCandidates(guestTables, state, guestCandidates);
            InsectData data = IslandGuestRules.PickSpecies(guestCandidates, state, UnityEngine.Random.value,
                UnityEngine.Random.value, guestScratch, guestScratchMultipliers, guestRarityAvailable);
            if (data == null || !IslandGuestRules.TryLevelBand(data, guestTables, out int minLevel, out int maxLevel))
                return false;

            slot.Data = data;
            slot.Level = FieldSpawnRules.RollFieldLevel(minLevel, maxLevel, UnityEngine.Random.value);
            // 색다름은 필드와 같은 확률로 기록에 한 번 굴린다(몸을 다시 세울 때마다 굴리지 않는다). 「지워진 개체」는 섬에 오지 않는다.
            slot.Shiny = UnityEngine.Random.value < InsectEntity.FieldShinyChance;
            return true;
        }

        private bool IsRegionIdAccessible(string regionId)
        {
            if (regionManager == null || string.IsNullOrEmpty(regionId)) return false;
            RegionData region = regionManager.GetRegionById(regionId);
            return region != null && regionManager.IsRegionAccessible(region);
        }

        /// <summary>손님 몸을 세운다 — 화면 밖 빈 칸(다 보이면 플레이어에서 가장 먼 칸)에, 야생 경로(<c>Initialize</c>)로.</summary>
        private void MaterializeGuest(GuestSlot slot)
        {
            if (root == null || slot.Data == null || freeCells.Count == 0 || playerMovement == null) return;

            guestTakenCells.Clear();
            for (int i = 0; i < guestSlots.Length; i++)
                if (guestSlots[i] != slot && guestSlots[i].Body != null) guestTakenCells.Add(guestSlots[i].Cell);
            IslandGrid.CellAt(playerMovement.transform.position - Origin, out int px, out int pz);
            if (guestCellInView == null) guestCellInView = IsCellInView;
            if (!IslandGuestRules.PickArrivalCell(freeCells, guestCellInView, new Vector2Int(px, pz), guestTakenCells,
                    UnityEngine.Random.value, out Vector2Int cell))
                return;

            GameObject go = TakeGuestBody();
            // Initialize 전이어야 한다 — 지금 자리가 배회·풀 더미의 기준(basePosition)이 된다. 섬 땅 윗면은 원점 높이다.
            go.transform.SetPositionAndRotation(Origin + IslandGrid.FootprintCenter(cell.x, cell.y, 1, 1),
                Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            go.SetActive(true);
            InsectEntity entity = go.GetComponent<InsectEntity>();
            if (entity == null) entity = go.AddComponent<InsectEntity>();
            if (guestDespawned == null) guestDespawned = OnGuestDespawned;
            entity.Initialize(slot.Data, slot.Level, string.Empty, guestDespawned, slot.Shiny, false);
            if (guestFleeArea == null) guestFleeArea = GuestFreeRun;
            entity.SetFleeArea(guestFleeArea);

            slot.Body = entity;
            slot.Cell = cell;
        }

        /// <summary>
        /// 게임플레이 퇴장(잡힘·전투 승패·놓침 도주) — 손님은 떠났고 다음 손님은 재생 지연(필드와 같은 60~120초) 뒤다.
        /// 놓침도 같다 — 필드의 "같은 개체를 눈 밖 다른 자리로"는 섬이 작아 옮길 눈 밖이 없어서 사라지는 쪽을 택했다.
        /// </summary>
        private void OnGuestDespawned(InsectEntity entity)
        {
            if (ReferenceEquals(entity, null)) return;
            for (int i = 0; i < guestSlots.Length; i++)
            {
                GuestSlot slot = guestSlots[i];
                if (!ReferenceEquals(slot.Body, entity)) continue;
                slot.Body = null;
                slot.Alive = false;
                slot.Data = null;
                slot.ReadyAt = Time.time + FieldSpawnRules.RollRespawnDelay(UnityEngine.Random.value);
                break;
            }
            if (entity != null) ReturnGuestBody(entity.gameObject);
        }

        /// <summary>배치가 바뀌었다 — 손님이 선 칸이 막혔으면 그 몸만 거둔다(기록은 남아 다음 틱에 다른 빈 칸에 선다).</summary>
        private void RevalidateGuestCells()
        {
            for (int i = 0; i < guestSlots.Length; i++)
            {
                GuestSlot slot = guestSlots[i];
                if (slot.Body == null || freeCellSet.Contains(slot.Cell)) continue;
                if (slot.Body.IsEngaged) continue;   // 포획·전투 중인 몸은 건드리지 않는다
                RecallGuestBody(slot);
            }
        }

        /// <summary>섬을 허문다(나가기·다시 짓기·남의 섬으로) — 몸만 거둔다. 게임플레이 퇴장이 아니라 콜백을 부르지 않는다.</summary>
        private void RecallAllGuestBodies()
        {
            for (int i = 0; i < guestSlots.Length; i++)
                if (guestSlots[i].Body != null) RecallGuestBody(guestSlots[i]);
        }

        private void RecallGuestBody(GuestSlot slot)
        {
            InsectEntity body = slot.Body;
            slot.Body = null;
            if (body == null) return;
            body.Recall();   // 다중 호출 가드를 건다 — 이 몸을 쥔 쪽이 뒤늦게 Despawn을 불러도 콜백이 돌지 않는다
            ReturnGuestBody(body.gameObject);
        }

        private GameObject TakeGuestBody()
        {
            for (int i = guestBodyPool.Count - 1; i >= 0; i--)
            {
                GameObject pooled = guestBodyPool[i];
                guestBodyPool.RemoveAt(i);
                if (pooled != null) return pooled;
            }
            if (guestBodyHolder == null)
            {
                // 이름 접두어 주의 — Region_/Scenery_/SubArea_ 등은 서브에리어 진입 때 꺼진다(rules/island.md).
                var holder = new GameObject("IslandGuestBodies");
                holder.transform.SetParent(transform, false);
                guestBodyHolder = holder.transform;
            }
            var go = new GameObject("IslandGuest");
            go.transform.SetParent(guestBodyHolder, false);
            return go;
        }

        private void ReturnGuestBody(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            if (!guestBodyPool.Contains(go)) guestBodyPool.Add(go);
        }

        /// <summary>손님 도주 구역 — 섬의 빈 칸만 밟고 갈 수 있는 거리(<see cref="IslandGuestRules.FreeRun"/>).</summary>
        private float GuestFreeRun(Vector3 worldStart, Vector3 direction)
            => IslandGuestRules.FreeRun(worldStart - Origin, direction, freeCellSet, FleePath.ProbeLength);

        private bool IsCellInView(Vector2Int cell)
            => IsWorldPointInView(Origin + IslandGrid.FootprintCenter(cell.x, cell.y, 1, 1));

        /// <summary>그 자리(몸 높이)가 지금 카메라 화면 안인가. 카메라가 없으면 아무도 못 본다.</summary>
        private bool IsWorldPointInView(Vector3 world)
        {
            if (guestCamera == null) guestCamera = Camera.main;
            if (guestCamera == null) return false;
            return IslandGuestRules.IsInView(guestCamera.WorldToViewportPoint(world + Vector3.up * 0.4f));
        }
    }
}
