using System.Collections.Generic;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    public class RegionManager : MonoBehaviour, ICloudReloadable
    {
        [SerializeField] private PlayerProgressController progress;

        private RegionData[] regions;
        private RegionData currentRegion;
        private SubAreaData currentSubArea;

        private HashSet<string> unlockedRegions = new HashSet<string>();
        private HashSet<string> defeatedGuardians = new HashSet<string>();
        private Transform cachedPlayerTransform; // Update 매 프레임 GameObject.Find 회피

        // SubArea 진입 시 SubAreaWorldBuilder가 플레이어를 (2000,0,2000)로 텔레포트하므로
        // Update의 위치 기반 SubArea 판정이 false가 되어 SubAreaChanged(null) 무한 토글이 발생.
        // sticky=true 동안 위치 판정 자체를 스킵 → SubAreaWorldBuilder가 명시적 Exit 트리거.
        private bool subAreaSticky;
        private string lastExitedSubAreaId;
        private float lastExitedAtTime;
        private const float SubAreaReentryCooldown = 1.5f;

        private static string UnlockKey => SaveScope.PrefsKey("InsectGame.UnlockedRegions");
        private static string GuardianKey => SaveScope.PrefsKey("InsectGame.DefeatedGuardians");

        public RegionData[] Regions => regions;
        public RegionData CurrentRegion => currentRegion;
        public SubAreaData CurrentSubArea => currentSubArea;
        public bool SubAreaSticky => subAreaSticky;

        /// <summary>
        /// 행동(포획·전투·대화)이 <b>어느 리전에서</b> 일어났는가 — 지역 의뢰(<c>QuestRegionGate</c>)와 스토리 리전 게이트가 읽는다.
        /// 분리 구역(나의 섬) 안이면 null이다. 섬은 어느 리전도 아닌데 <see cref="CurrentRegion"/>은 섬에 있는 동안에도 떠나기 전
        /// 리전으로 남아서(sticky — <see cref="EnterDetachedSubArea"/>), 섬의 손님 곤충을 잡으면 그 리전의 의뢰·스토리 포획으로 셌다.
        /// 동굴 같은 보통 서브에리어는 그 리전 안이다(지역 의뢰는 서브에리어 안의 행동도 센다 — rules/quest-system.md).
        /// </summary>
        public string ActionRegionId => ActionRegionIdOf(currentRegion, currentSubArea);

        /// <summary><see cref="ActionRegionId"/>의 순수 판정 — 테스트가 매니저 없이 본다.</summary>
        public static string ActionRegionIdOf(RegionData region, SubAreaData subArea)
        {
            if (subArea != null && subArea.detached) return null;
            return region != null ? region.regionId : null;
        }

        // 사용자가 영역 안에 있지만 아직 진입 안 한 상태. SubAreaProximityChanged로 UI 표시.
        // 옛은 ContainsPoint 시 SubAreaChanged 자동 발화 → 자동 진입. 사용자 명시 요청: [E] 키 선택.
        private SubAreaData nearbySubArea;
        public SubAreaData NearbySubArea => nearbySubArea;

        public event System.Action<RegionData> RegionChanged;
        public event System.Action<SubAreaData> SubAreaChanged;
        public event System.Action<SubAreaData> SubAreaProximityChanged;

        /// <summary>
        /// 수문장을 처음 쓰러뜨렸을 때 그 regionId로 발화. StoryDirector의 GuardianDefeat 트리거 소스.
        ///
        /// <b>일생에 리전당 딱 한 번만 울린다</b> — <see cref="DefeatGuardian"/>이 idempotent 가드로
        /// 중복 격파를 무시하기 때문이다. 그래서 이 트리거를 쓰는 스토리 비트는 **leaf 전용**이다:
        /// 발화 순간 prereq가 미충족이면 그 비트는 영영 열리지 않고, 뒤 비트가 그걸 prereq로 삼고
        /// 있으면 캠페인이 거기서 영구 정지한다(QuestComplete와 정확히 같은 함정).
        /// 스파인은 RegionEnter/SubAreaEnter 같은 재발화 트리거에 건다.
        /// </summary>
        public event System.Action<string> GuardianDefeated;

        /// <summary>
        /// 수문장 배지를 새로 얻었다 — <see cref="DefeatGuardian"/>의 <b>첫 격파에서만</b>, 리전당 일생 한 번.
        /// <see cref="GuardianDefeated"/>와 나눈 이유: 그쪽은 이미 깬 수문장이 필드에 남아 있을 때 봉인을 걷으려고
        /// <see cref="TryDefeatGuardian"/>이 <b>다시</b> 울린다. 배지 연출이 그걸 들으면 같은 배지가 두 번 뜬다.
        /// </summary>
        public event System.Action<string> GuardianBadgeEarned;

        public void SetSubAreaSticky(bool sticky, string exitedId = null)
        {
            subAreaSticky = sticky;
            if (!sticky && !string.IsNullOrEmpty(exitedId))
            {
                lastExitedSubAreaId = exitedId;
                lastExitedAtTime = Time.time;
            }
        }

        /// <summary>F2 또는 외부 트리거로 SubArea 강제 종료 — sticky 풀고 즉시 SubAreaChanged(null) 발화.</summary>
        public void ForceExitSubArea()
        {
            if (currentSubArea == null) return;
            string exitedId = currentSubArea.subAreaId;
            currentSubArea = null;
            subAreaSticky = false;
            nearbySubArea = null;
            SubAreaProximityChanged?.Invoke(null);
            lastExitedSubAreaId = exitedId;
            lastExitedAtTime = Time.time;
            SubAreaChanged?.Invoke(null);
        }

        public void Initialize(RegionData[] regionList)
        {
            regions = regionList;

            // 이전 버전은 마지막으로 진입한 SubArea를 전역 PlayerPrefs에 남겨 다음 실행 때
            // 자동 복귀했다. 이제 플레이어는 항상 마을에서 시작하므로 레거시 키를 1회 정리한다.
            if (PlayerPrefs.HasKey(GameConstants.PrefsKeys.LastSubAreaId))
            {
                PlayerPrefs.DeleteKey(GameConstants.PrefsKeys.LastSubAreaId);
                PlayerPrefs.Save();
            }

            LoadUnlockState();
        }

        public void AutoWire(PlayerProgressController prog)
        {
            if (progress == null) progress = prog;
        }

        private void Update()
        {
            if (regions == null || regions.Length == 0) return;

            // 플레이어 transform 캐싱 (매 프레임 GameObject.Find 비용 회피)
            if (cachedPlayerTransform == null)
            {
                GameObject p = GameObject.Find("Player");
                if (p == null) return;
                cachedPlayerTransform = p.transform;
            }

            // SubArea sticky 모드: 텔레포트 좌표(2000,0,2000)에 있는 동안 위치 기반 판정 스킵
            // (SubAreaWorldBuilder가 명시적으로 SetSubAreaSticky(false)를 호출할 때까지 유지)
            if (subAreaSticky) return;

            Vector3 pos = cachedPlayerTransform.position;
            RegionData found = null;
            foreach (var r in regions)
            {
                if (r.ContainsPoint(pos))
                {
                    found = r;
                    break;
                }
            }

            if (found != currentRegion)
            {
                currentRegion = found;
                RegionChanged?.Invoke(currentRegion);
            }

            // 서브구역 감지
            SubAreaData foundSub = null;
            if (currentRegion != null && currentRegion.subAreas != null)
            {
                foreach (var sub in currentRegion.subAreas)
                {
                    if (sub.ContainsPoint(pos))
                    {
                        foundSub = sub;
                        break;
                    }
                }
            }

            // 방금 Exit한 SubArea로의 자동 재진입 차단 (쿨다운 1.5초)
            if (foundSub != null
                && foundSub.subAreaId == lastExitedSubAreaId
                && Time.time - lastExitedAtTime < SubAreaReentryCooldown)
            {
                foundSub = null;
            }

            // 옛은 currentSubArea를 자동 설정해 SubAreaChanged 발화 → 자동 진입.
            // 새는 nearbySubArea만 갱신, 사용자가 [E] 키로 RequestEnterSubArea() 호출해야 진입.
            // currentSubArea는 EnterSubArea/Exit 시점에만 변경됨.
            if (foundSub != nearbySubArea)
            {
                nearbySubArea = foundSub;
                SubAreaProximityChanged?.Invoke(nearbySubArea);
            }
        }

        /// <summary>사용자 [E] 키 또는 UI 버튼 트리거 — nearbySubArea로 명시적 진입.</summary>
        public void RequestEnterSubArea()
        {
            if (nearbySubArea == null || currentSubArea != null) return;
            currentSubArea = nearbySubArea;
            SubAreaChanged?.Invoke(currentSubArea);
        }

        /// <summary>
        /// <b>위치와 무관하게</b> 분리 구역(나의 섬)으로 들어간다 — <see cref="SubAreaData.detached"/>인 구역 전용.
        ///
        /// 섬을 서브에리어 상태로 태우는 이유: "분리된 공간에 있다"는 판정이 전부
        /// <see cref="CurrentSubArea"/> != null에 걸려 있다(플레이어 접지·끼임 복구, 스포너 필드 틱, 미니맵·지도, 환경광).
        /// 섬을 별개 상태로 두면 그 전부를 따로 고쳐야 하고, 리전 판정이 멈추지 않아 섬을 오갈 때마다
        /// <see cref="RegionChanged"/>가 다시 울린다(방문 퀘스트·스토리 트리거·BGM이 왕복마다 재발화한다).
        ///
        /// 들어가는 순간 sticky를 켠다 — 섬 좌표에는 리전이 없어 Update의 위치 판정이 리전을 null로 바꾸기 때문이다.
        /// 나올 때는 <see cref="ForceExitSubArea"/>.
        /// </summary>
        public bool EnterDetachedSubArea(SubAreaData area)
        {
            if (area == null || !area.detached || currentSubArea != null) return false;
            subAreaSticky = true;
            // 근접 진입 대상이 남아 있으면 섬 위에서도 "○○ 들어가기" 버튼과 [E] 진입이 살아 있다.
            if (nearbySubArea != null)
            {
                nearbySubArea = null;
                SubAreaProximityChanged?.Invoke(null);
            }
            currentSubArea = area;
            SubAreaChanged?.Invoke(currentSubArea);
            return true;
        }

        // --- 지역 잠금 시스템 ---

        /// <summary>
        /// <b>이야기 잠금</b> — 앞 리전 수문장에 <b>더해</b> 이 사람을 이겨야 열리는 리전(2026-10-04 사용자 결정:
        /// 명부회 간부전은 본편 필수, 이겨야 다음 지역이 열린다). 하월은 같은 리전 안의 최종장이라 리전 잠금이 아니라
        /// 스토리 선행(<c>fin_unnamed</c> ← <c>duel_chief_win</c>)으로 막는다.
        ///
        /// 인물 ID를 문자열로 둔다 — Core가 NPC 모듈(<c>NpcBossDuels</c>)을 참조하지 않게 하려는 것이고, 이긴 기록은
        /// 부트스트랩이 넘기는 조회 함수(<see cref="AutoWireDuelGate"/>)로만 읽는다. 표시명이 대결 표·대사창과 같은지는
        /// <c>RegionStoryLockTests</c>가 본다(어긋나면 "집게에게 이겨야"가 다른 이름으로 뜬다).
        /// </summary>
        public struct StoryLock
        {
            public string regionId;
            /// <summary>이겨야 하는 상대 — 간부 대결 표의 storyNpcId(격파 기록 ID와 같다).</summary>
            public string storyNpcId;
            /// <summary>안내 문구에 쓰는 이름 — 대사창 이름표와 같다.</summary>
            public string displayName;
        }

        private static readonly StoryLock[] StoryLocks =
        {
            // 서릿길 — 모래언덕 창고의 집게. 창고(ch8_confront) 직후 대결이 열리고, 이기면 서릿길 도착(ch9_arrive)이 이어진다.
            new StoryLock { regionId = "frostline", storyNpcId = "ledger_grip", displayName = "집게" },
            // 잿불 골짜기 — 얼음 서고의 저울. 서고(ch9_confront)의 대답 뒤 대결, 이기면 잿불 도착(ch10_arrive).
            new StoryLock { regionId = "emberfall", storyNpcId = "ledger_scale", displayName = "저울" },
        };

        /// <summary>이야기 잠금 표 전체 — 테스트·검사용(값 복사).</summary>
        public static StoryLock[] AllStoryLocks()
        {
            StoryLock[] copy = new StoryLock[StoryLocks.Length];
            System.Array.Copy(StoryLocks, copy, StoryLocks.Length);
            return copy;
        }

        public static bool TryGetStoryLock(string regionId, out StoryLock storyLock)
        {
            for (int i = 0; i < StoryLocks.Length; i++)
            {
                if (StoryLocks[i].regionId == regionId)
                {
                    storyLock = StoryLocks[i];
                    return true;
                }
            }
            storyLock = default;
            return false;
        }

        /// <summary>
        /// 간부를 이겼는가 — <c>NpcDuelController.IsBossDefeated</c>. 부트스트랩이 넘긴다.
        /// <b>null이면 이야기 잠금이 없는 옛 동작이다</b>(수문장만으로 열린다) — 배선이 빠져 영영 못 여는 것보다 낫다.
        /// </summary>
        private System.Func<string, bool> storyDuelWon;

        /// <summary>이야기 잠금의 판정 함수 — 간부 격파 기록 조회. 클라우드 재적재는 그쪽 컨트롤러가 다시 읽는다.</summary>
        public void AutoWireDuelGate(System.Func<string, bool> duelWon)
        {
            if (storyDuelWon == null) storyDuelWon = duelWon;
        }

        /// <summary>
        /// 리전이 열려 있는가 — <b>순수 판정</b>(테스트가 매니저 없이 본다). 마스터·시작 리전 우회는 호출부가 먼저 한다.
        ///
        /// <list type="number">
        /// <item><b>저장된 해금 기록은 닫지 않는다.</b> 이야기 잠금이 생기기 전에 열린 서릿길·잿불 골짜기는 그대로 열려 있다 —
        /// 그 안을 돌던 플레이어를 가두거나 밖으로 밀어내지 않는다. 진행은 스토리 선행(<c>ch9_arrive</c> ← 집게 승리)이
        /// 따로 막고, HUD가 간부에게 안내한다.</item>
        /// <item>그 밖에는 <b>앞 리전 수문장 격파</b>가 열쇠이고, 이야기 잠금이 있으면 <b>그 간부 승리</b>까지 함께 본다.
        /// 수문장을 먼저 이기면 해금 기록을 미뤄 두고(<see cref="DefeatGuardian"/>), 간부를 이기는 순간 여기서 열린다 —
        /// 두 기록이 모두 저장·클라우드 동기라 기기를 바꿔도 같다.</item>
        /// </list>
        /// </summary>
        /// <param name="duelWon">null이면 이야기 잠금을 보지 않는다(옛 동작).</param>
        public static bool IsOpenByProgress(string regionId, bool savedUnlock, bool gateGuardianDefeated,
            System.Func<string, bool> duelWon)
        {
            if (savedUnlock) return true;
            if (!gateGuardianDefeated) return false;
            if (duelWon == null || !TryGetStoryLock(regionId, out StoryLock storyLock)) return true;
            return duelWon(storyLock.storyNpcId);
        }

        public bool IsRegionAccessible(RegionData region)
        {
            if (region == null) return false;
            // 마스터 계정은 모든 리전 우회 — AuthManager.ApplyMasterPrivileges가 PlayerPrefs를 갱신하지만
            // RegionManager.LoadUnlockState 이후 마스터 로그인 시 HashSet에 반영 안 되는 race 차단.
            // **"특권 없이" 모드면 우회하지 않는다** — 그 모드의 요점이 지역 게이트를 살리는 것이다.
            if (AuthManager.Instance != null && AuthManager.Instance.MasterPrivilegesActive) return true;
            if (region.regionId == "meadow") return true;
            return IsOpenByProgress(region.regionId, unlockedRegions.Contains(region.regionId),
                IsGatekeeperGuardianDefeated(region.regionId), storyDuelWon);
        }

        // 앞 리전(열쇠를 쥔 리전)의 수문장을 쓰러뜨렸는가. 앞이 없는 리전(시작 리전)은 false — 저장 기록으로만 열린다.
        private bool IsGatekeeperGuardianDefeated(string regionId)
        {
            string prevId = GetPreviousRegionId(regionId);
            return !string.IsNullOrEmpty(prevId) && defeatedGuardians.Contains(prevId);
        }

        /// <summary>잠긴 리전의 열쇠 종류.</summary>
        public enum LockKind
        {
            /// <summary>열려 있다.</summary>
            Open,
            /// <summary>앞 리전 수문장을 아직 못 이겼다.</summary>
            Guardian,
            /// <summary>수문장은 이겼고 이야기 대결(간부)이 남았다.</summary>
            StoryDuel,
            /// <summary>열쇠를 모른다(앞 리전 없음·데이터 누락).</summary>
            Unknown,
        }

        /// <summary>
        /// 잠긴 이유 — 안내 문구(필드 차단·지도·HUD 목표)가 같은 답을 내도록 여기 한 곳에서 판정한다.
        /// <paramref name="gate"/>는 열쇠를 쥔 앞 리전, <paramref name="storyLock"/>은 이야기 잠금(있을 때).
        /// </summary>
        public LockKind GetLockKind(RegionData region, out RegionData gate, out StoryLock storyLock)
        {
            gate = null;
            storyLock = default;
            if (region == null) return LockKind.Unknown;
            if (IsRegionAccessible(region)) return LockKind.Open;

            gate = GetGatekeeperRegion(region.regionId);
            bool hasStoryLock = TryGetStoryLock(region.regionId, out storyLock);
            if (gate != null && !IsGuardianDefeated(gate.regionId)) return LockKind.Guardian;
            if (hasStoryLock && storyDuelWon != null && !storyDuelWon(storyLock.storyNpcId)) return LockKind.StoryDuel;
            return LockKind.Unknown;
        }

        /// <summary>
        /// 잠긴 리전 앞에서 띄울 한 줄 — "서릿길 — 집게에게 이겨야 열립니다". 필드 차단 배너(<c>PlayerMovement</c>)가 쓰고,
        /// 지도(<c>RegionMapUI</c>)의 잠김 안내도 이걸 쓰면 두 화면이 같은 이유를 댄다.
        /// 열려 있으면 빈 문자열.
        /// </summary>
        public string DescribeLock(RegionData region)
        {
            if (region == null) return string.Empty;
            LockKind kind = GetLockKind(region, out RegionData gate, out StoryLock storyLock);
            return DescribeLockText(region.displayName, kind,
                gate != null ? gate.guardianDisplayName : null, storyLock.displayName);
        }

        /// <summary>
        /// <see cref="DescribeLock"/>의 순수 문구부. 조사는 "…에게"로 잇는다 — 수문장 이름이 데이터에서 오는데 받침이
        /// 갈려서("사마귀" / "장수말벌") 을/를을 어느 쪽으로 고정해도 절반은 어색해진다. 원인을 모르면 지어내지 않는다.
        /// </summary>
        public static string DescribeLockText(string regionName, LockKind kind, string gateGuardianName, string duelOpponentName)
        {
            switch (kind)
            {
                case LockKind.Open: return string.Empty;
                case LockKind.Guardian:
                    if (!string.IsNullOrEmpty(gateGuardianName)) return $"{regionName} — {gateGuardianName}에게 이겨야 열립니다";
                    break;
                case LockKind.StoryDuel:
                    if (!string.IsNullOrEmpty(duelOpponentName)) return $"{regionName} — {duelOpponentName}에게 이겨야 열립니다";
                    break;
            }
            return $"{regionName} — 아직 갈 수 없습니다";
        }

        public RegionData[] GetAccessibleRegions()
        {
            if (regions == null) return new RegionData[0];
            List<RegionData> result = new List<RegionData>();
            foreach (var r in regions)
            {
                if (IsRegionAccessible(r))
                    result.Add(r);
            }
            return result.ToArray();
        }

        public RegionData GetRegionById(string id)
        {
            if (regions == null) return null;
            foreach (var r in regions)
            {
                if (r.regionId == id) return r;
            }
            return null;
        }

        // --- 수문장 시스템 ---

        public bool IsGuardianDefeated(string regionId)
        {
            return defeatedGuardians.Contains(regionId);
        }

        /// <summary>
        /// 전투 승리 뒤 격파 확정 — 1v1(<c>BattleScreenUI</c>)과 레이드(<c>RaidBattleUI</c>)가
        /// 같은 한 줄을 부른다. 빈 ID(야생)·이미 깬 수문장이면 false. 두 UI에 복제돼 있던 로직을
        /// 여기로 모았다(한쪽만 고치면 어긋난다).
        /// </summary>
        public bool TryDefeatGuardian(string regionId, string via)
        {
            if (string.IsNullOrEmpty(regionId)) return false;   // 수문장이 아니라 야생이었다
            if (IsGuardianDefeated(regionId))
            {
                // 이미 깬 수문장인데 필드에 서 있었다(클라우드 로드 전 선전투 등). 격파 처리는 다시 안 하지만
                // 봉인은 걷어야 한다 — 수문장의 Despawn은 no-op이라 그냥 두면 무한히 다시 싸울 수 있다.
                GuardianDefeated?.Invoke(regionId);
                return false;
            }
            DefeatGuardian(regionId);
            RegionData region = GetRegionById(regionId);
            string next = GetNextRegionId(regionId);
            Debug.Log($"[Guardian] {(region != null ? region.displayName : regionId)} 수문장 격파({via})! "
                + (next != null && IsHeldByStoryLock(next) ? "다음 지역은 이야기 대결이 남았다" : "다음 지역 해금됨"));
            return true;
        }

        // 이 리전이 아직 이야기 대결 때문에 닫혀 있어야 하는가. 판정 함수가 없으면(미배선) 잠그지 않는다 — 옛 동작.
        private bool IsHeldByStoryLock(string regionId)
        {
            return storyDuelWon != null && TryGetStoryLock(regionId, out StoryLock storyLock)
                && !storyDuelWon(storyLock.storyNpcId);
        }

        public void DefeatGuardian(string regionId)
        {
            // 중복 격파 가드 — BattleScreenUI.CheckGuardianDefeat가 IsGuardianDefeated 가드 후 호출하지만
            // 명시적 idempotent 보장 + SaveUnlockState 중복 PlayerPrefs.Save 비용 차단.
            if (defeatedGuardians.Contains(regionId)) return;

            defeatedGuardians.Add(regionId);

            string nextRegion = GetNextRegionId(regionId);
            // **이야기 잠금이 남았으면 해금 기록을 미룬다.** 저장된 해금은 "닫지 않는다"는 약속이라(IsOpenByProgress),
            // 여기서 적어 버리면 간부를 이기기 전에 영영 열린다. 간부를 이기는 순간 IsRegionAccessible이 두 기록으로 연다.
            if (!string.IsNullOrEmpty(nextRegion) && !IsHeldByStoryLock(nextRegion))
            {
                unlockedRegions.Add(nextRegion);
            }

            // 초원 수문장 격파 시 꽃밭도 해금 (분기 경로)
            if (regionId == "meadow")
            {
                unlockedRegions.Add("garden");
            }

            SaveUnlockState();

            // 해금·저장이 끝난 뒤에 알린다 — 구독자(StoryDirector)가 발화 시점에
            // IsRegionAccessible 같은 상태를 읽어도 이미 갱신된 값을 보게 한다.
            // 위 idempotent 가드 덕에 리전당 정확히 1회만 울린다.
            // 배지가 먼저다 — 배지 서비스가 이정표 보상까지 지급해 둔 뒤에 스토리·봉인이 움직인다.
            GuardianBadgeEarned?.Invoke(regionId);
            GuardianDefeated?.Invoke(regionId);
        }

        // GetRegionWithGuardianNear(위치, 반경)는 제거했다 — **좌표로는 수문장을 판별할 수 없다.**
        // 당시 야생 스폰 링이 플레이어를 따라왔으므로(10~43m 나선 + 5m 산포) 수문장 앞에 선 순간 야생이
        // 반경 5m까지 들어왔다. 지금은 야생이 리전 원판 전체에 기록돼 흩어지지만(InsectSpawner) 수문장 앞을
        // 비켜 두지 않으니 같다 — 어떤 반경을 골라도 야생 조우가 격파로 잡히는 구조라, 격파 판정은 개체 표식
        // (InsectEntity.GuardianRegionId)으로 옮겼다. 이름이 그럴듯해 다시 불려 나가지 않도록 함수째 지운다.

        /// <summary>
        /// 수문장이 서는 자리 — 이전 리전에서 오는 <b>길목</b>, 리전 경계 안쪽이다.
        ///
        /// 예전엔 두 리전 중심의 <b>중점</b>이었다. 그건 리전이 서로 겹칠 때만 경계가 되는데
        /// 이 월드의 리전은 떨어져 있어서, 13개 중 <b>9개가 어느 리전에도 속하지 않는 허공</b>에
        /// 섰다(hollow는 자기 중심에서 77m 밖). 전역 Ground 위라 떨어지지는 않지만 리전 안을
        /// 아무리 둘러봐도 수문장이 안 보였고, 지도 마커도 리전 밖을 가리켰다.
        ///
        /// 반경의 72%에 두면 항상 리전 안이면서 중심보다 바깥이라 "길을 막는" 그림이 유지된다.
        /// <c>RegionMapUI</c> 마커와 <c>StoryObjectiveTracker</c> 목표가 같은 함수를 쓰므로
        /// 실물·표시·안내가 함께 움직인다.
        /// </summary>
        public Vector3 GetGuardianPosition(RegionData region)
        {
            if (region == null) return Vector3.zero;

            Vector3 fromCenter = Vector3.zero;
            string prevId = GetPreviousRegionId(region.regionId);
            if (prevId != null)
            {
                RegionData prev = GetRegionById(prevId);
                if (prev != null) fromCenter = prev.centerPosition;
            }

            Vector3 toPrev = fromCenter - region.centerPosition;
            toPrev.y = 0f;

            // **시작 리전(meadow)은 이전 리전이 없다 — 대신 '나가는 길'을 본다.**
            // 예전엔 중심에 뒀는데, 첫 리전만 배치 규칙이 예외라 플레이어가 시작 지점에서
            // 수문장을 아예 못 보고 "수문장이 없다"고 느꼈다. 수문장의 역할은 어차피 길목을
            // 지키는 것이므로, 이전 리전이 없으면 **다음 리전 방향**의 같은 반경에 세운다.
            // 그러면 13개 리전이 모두 "경계에 선다"는 한 가지 규칙으로 통일된다.
            if (toPrev.sqrMagnitude < 0.01f)
            {
                string nextId = GetNextRegionId(region.regionId);
                RegionData next = nextId != null ? GetRegionById(nextId) : null;
                if (next != null)
                {
                    Vector3 toNext = next.centerPosition - region.centerPosition;
                    toNext.y = 0f;
                    if (toNext.sqrMagnitude >= 0.01f)
                        return region.centerPosition + toNext.normalized * (region.radius * GuardianEdgeRatio);
                }
                return region.centerPosition;   // 앞뒤 어느 쪽도 없는 리전(있다면) — 종전대로
            }

            return region.centerPosition + toPrev.normalized * (region.radius * GuardianEdgeRatio);
        }

        /// <summary>수문장을 리전 반경의 몇 %에 세울지. 1.0이면 경계 밖으로 새어 나간다.</summary>
        private const float GuardianEdgeRatio = 0.72f;

        // --- 지역 순서 매핑 ---

        private string GetNextRegionId(string currentRegionId)
        {
            switch (currentRegionId)
            {
                case "meadow": return "pond";      // + garden도 해금 (DefeatGuardian에서 별도 처리)
                case "pond": return "forest";
                case "forest": return "swamp";
                case "swamp": return "mountain";
                case "mountain": return "ruins";
                // ── 2막(ver2) ── 유적 수문장 격파가 '봉인이 열린 날'이자 2막의 문이다.
                // 1막에서는 ruins가 종착지라 여기 case가 없었다.
                case "ruins": return "hollow";
                case "hollow": return "dunes";
                case "dunes": return "frostline";
                case "frostline": return "emberfall";
                case "emberfall": return "canopy";
                case "canopy": return "nameless";
                // nameless는 종착지 — ver3를 붙일 때 여기 case가 생긴다.
                default: return null;
            }
        }

        private string GetPreviousRegionId(string regionId)
        {
            switch (regionId)
            {
                case "pond": return "meadow";
                case "forest": return "pond";
                case "swamp": return "forest";
                case "mountain": return "swamp";
                case "ruins": return "mountain";
                case "garden": return "meadow";
                // ── 2막(ver2) ── 빠뜨리면 GetGuardianPosition의 fromCenter가 원점(0,0,0)이 되어
                // 수문장이 맵 한복판과 리전 사이 엉뚱한 자리에 스폰된다(add-region 시나리오 E).
                case "hollow": return "ruins";
                case "dunes": return "hollow";
                case "frostline": return "dunes";
                case "emberfall": return "frostline";
                case "canopy": return "emberfall";
                case "nameless": return "canopy";
                default: return null;
            }
        }

        /// <summary>
        /// 이 리전을 여는 열쇠 — <b>어느 리전의 수문장</b>을 쓰러뜨려야 하는가.
        ///
        /// 해금은 <see cref="DefeatGuardian"/>이 <c>GetNextRegionId</c>로 <b>다음</b> 리전을
        /// 여는 구조라, 잠긴 리전의 열쇠는 그 리전 안이 아니라 <b>바로 앞 리전</b>에 있다.
        /// 안내 문구가 이걸 모르면 플레이어를 들어가지도 못하는 리전 쪽으로 되돌려 보낸다.
        ///
        /// 꽃밭도 규칙이 같다 — 앞 리전이 초원이고 초원 수문장 격파가 함께 열어 준다.
        /// 시작 리전(초원)처럼 앞이 없는 곳은 null을 준다(애초에 잠기지 않는다).
        ///
        /// <c>GetPreviousRegionId</c>를 public으로 바꾸지 않고 감싸는 이유:
        /// <c>RegionProgressionTests</c>가 그 <b>시그니처 문자열</b>로 본문을 찾아
        /// 진행 체인을 검사한다(`private string GetPreviousRegionId`).
        /// </summary>
        public RegionData GetGatekeeperRegion(string regionId)
        {
            string prevId = GetPreviousRegionId(regionId);
            return string.IsNullOrEmpty(prevId) ? null : GetRegionById(prevId);
        }

        // --- 저장/로드 ---

        // 클라우드 로드 후 PlayerPrefs(지역 해금/수문장)를 다시 읽어 인메모리 갱신.
        // 지도 UI(IMGUI)는 매 프레임 IsRegionUnlocked로 읽어 자동 반영.
        public void ReloadFromDisk()
        {
            LoadUnlockState();
        }

        private void LoadUnlockState()
        {
            // RemoveEmptyEntries — 옛은 "meadow,," 같은 문자열에서 빈 항목이 HashSet에 누적되어
            // SaveUnlockState 시 string.Join이 ",,meadow,," 형태로 PlayerPrefs에 잔존.
            string saved = PlayerPrefs.GetString(UnlockKey, "meadow");
            unlockedRegions = new HashSet<string>(
                saved.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));

            string guardians = PlayerPrefs.GetString(GuardianKey, "");
            defeatedGuardians = new HashSet<string>(
                guardians.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
        }

        private void SaveUnlockState()
        {
            PlayerPrefs.SetString(UnlockKey, string.Join(",", unlockedRegions));
            PlayerPrefs.SetString(GuardianKey, string.Join(",", defeatedGuardians));
            PlayerPrefs.Save();
        }
    }
}
