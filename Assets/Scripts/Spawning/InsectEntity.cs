using System;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Spawning
{
    public class InsectEntity : MonoBehaviour
    {
        [SerializeField] private InsectData data;
        [SerializeField] private int level = 1;

        private Action<InsectEntity> onDespawn;
        // 소속 리전(필드 곤충). 서브에리어·배틀·수문장 개체는 빈 문자열.
        private string regionId = string.Empty;
        private float bobPhase;
        private Vector3 basePosition;
        // basePosition 자리의 둔덕 윗면(FieldGround.SurfaceY) — 이동 중 높이를 그 자리 지면만큼 오르내리는 기준(GroundRise)
        private float baseSurfaceY = Core.FieldGround.FloorY;
        private float wingPhase;
        private bool shiny;
        private bool erased;   // 「지워진 개체」 — IsErased 요약 참조
        private bool forBattle;
        private float shinySparkleTimer;
        private Transform cachedShinySparkle;
        private Transform cachedNameLabel;
        private float cachedShinyShift = -1f; // 이로치 종별 고정 색조 이동량 캐시(빌드마다 -1로 리셋 후 첫 Shinify에서 산출)
        private int cachedMoveStyle = -1;     // 0 일반/1 날개비행/2 점프 — 빌드마다 -1 리셋 후 첫 Update에서 산출
        private Transform cachedGroundMarker; // 지면 마커: 상하 이동 상쇄용(곤충이 떠도 마커는 지면 고정)
        private Transform cachedGrass;        // 풀숲 은신 더미(지상 곤충) — 곤충이 움직여도 제자리 고정
        // 날개 캐시 — WingL/R 탐색과 종별 날갯짓 파라미터를 빌드마다 한 번만 정한다.
        // 옛 AnimateWings는 **매 프레임** transform.Find 2회 + insectId.Contains 최대 10회를 다시 했다.
        // 특히 날개가 없는 종(기어다니는 곤충)은 그 Find가 영원히 실패해 매 프레임 자식 전체를 훑었다 —
        // 실패하는 Find가 가장 비싸다. 위 cachedMoveStyle/cachedGroundMarker와 같은 형태로 맞춘다.
        private Transform cachedWingL;
        private Transform cachedWingR;
        private float wingSpeed;
        private float wingAmplitude;
        private bool wingsResolved;
        // NameLabel은 **배틀 모델엔 아예 없다**(BuildForBattle이 CreateNameLabel을 부르지 않는다).
        // null 검사만으로 재시도하면 그 경우 매 프레임 Find가 영원히 실패하므로 찾았는지를 따로 든다.
        private bool nameLabelResolved;
        // 경계/도주(긴장감) 상태
        private int alertState;               // 0 평온 / 1 경계(주시·떨림) / 2 도주
        private float patience;               // 경계 인내심(0이면 도주)
        private float alertGraceTimer;        // 경계 직후 도주 유예(반응 시간 보장)
        private Vector3 fleeDir;
        private float fleeTimer;
        // 도주로 갈 수 있는 수평 거리와 지금까지 간 거리 — 장애물 앞에서 멈추게 한다(FleePath 주석).
        private float fleeAllowed;
        private float fleeTravelled;
        private bool engaged;                 // 포획 상호작용 중 — 절대 도주 안 함
        // 플레이어 추적(전 곤충 공유, 프레임당 1회 계산)
        private static Transform cachedPlayer;
        private static Vector3 lastPlayerPos;
        private static float playerSpeed;
        private static int playerTrackFrame = -1;
        // 아이템 도주 방지 확률 제공자 — 부트스트랩이 세팅(itemEffects.GetFleePreventChance). null이면 0(방지 없음).
        // InsectEntity는 풀링 객체라 AutoWire/provider 참조가 없어 static 훅으로 주입.
        public static System.Func<float> FleePreventChanceProvider;

        // ── 습격(AmbushRules) ──
        // 깨어 있는 습격형은 플레이어를 알아채면 달아나는 대신 멈칫 → 다가간다. 경계·도주와 같은 칸(alertState)을 쓴다 —
        // 3이 습격이고 IsAlerted가 참이라 스포너가 그동안 거두거나 바꾸지 않는다. CanBeEngaged의 뜻은 그대로다(다가오는 중에도 [E]로 말을 걸 수 있다).
        private const int AmbushState = 3;
        private bool ambusherSpecies;     // 이 종이 습격형인가 — 몸을 지을 때 한 번 정한다(온순한 종은 판정을 묻지도 않는다)
        private float ambushReadyTime;    // 이 몸이 다시 습격할 수 있는 시각(Time.time) — 개체 쿨다운(AmbushRules.EntityCooldownSeconds)
        private float ambushHesitate;     // 남은 멈칫(초). 0 이하면 다가가는 중
        private float ambushChaseTime;    // 다가간 시간(기다린 시간 제외)
        private float ambushReplanTimer;
        private float ambushStuckTime;
        private float ambushSide = 1f;
        private Vector3 ambushDir;
        private float ambushLegAllowed;
        private float ambushLegTravelled;
        private bool ambushAnnounced;
        // 풀 더미가 서 있는 자리 — 습격으로 몸이 자리를 옮겨도(RebaseHere) 풀은 처음 자리에 남는다.
        private Vector3 grassAnchor;

        // 도주가 나갈 수 없는 구역 — (출발점, 방향) → 그 방향으로 구역 안에서 갈 수 있는 거리(m). null이면 제약 없음(필드).
        // 섬 손님 곤충이 쓴다(SetFleeArea): 물리 측정은 벽·건물만 보고 "빈 칸"을 모르므로, 그대로 두면 꽃밭 너머·섬 가장자리 너머로 달아난다.
        private Func<Vector3, Vector3, float> fleeArea;

        /// <summary>
        /// 습격 판정 — 이 곤충이 지금 습격할 수 있는가(막혔다면 까닭). <c>CaptureInputController</c>가 세운다. 풀 객체라 AutoWire가 없어
        /// <see cref="FleePreventChanceProvider"/>와 같은 static 훅이다. <b>null이면 습격이 없다</b> — 모든 곤충이 예전처럼 달아나기만 한다.
        /// </summary>
        public static Func<InsectEntity, AmbushRefusal> AmbushGate;

        /// <summary>습격형이 멈칫을 끝내고 발을 뗐다 — 필드 경고 문구가 듣는다.</summary>
        public static event Action<InsectEntity> AmbushStarted;

        /// <summary>
        /// 습격형이 플레이어에게 닿았다 — 받는 쪽이 「습격!」 창을 연다(<c>SetEngaged(true)</c>). 아무도 안 열면 이 곤충은 맴돌지 않고 물러난다.
        /// </summary>
        public static event Action<InsectEntity> AmbushReached;

        private bool despawnedThisCycle; // Despawn 다중 호출 가드 (Battle/Capture 동시 호출 시 풀 중복 반환 차단)
        // 수문장 표식 — 기본은 빈 문자열(야생). 풀 재사용마다 반드시 지운다(GuardianRegionId 주석 참조).
        private string guardianRegionId = string.Empty;
        // 몸을 새로 지을 때마다 오르는 번호 — SpawnSerial 요약 참조.
        private int spawnSerial;
        private static int nextSpawnSerial;

        /// <summary>
        /// 필드 곤충이 「색다른 개체」로 나올 확률. 스포너가 슬롯에 개체를 들일 때 한 번 굴려 기록한다 —
        /// 몸을 다시 세울 때마다 굴리면 멀어졌다 돌아온 같은 곤충의 색이 바뀐다. (gacha_sim이 이 상수를 읽는다.)
        /// </summary>
        internal const float FieldShinyChance = 0.01f;

        // 도주 경로 측정 — 도주를 시작할 때만 쏜다(매 프레임이 아니다). 전 곤충이 한 스레드에서 번갈아 쓰므로 정적 버퍼 하나로 족하다.
        /// <summary>
        /// 이번 퇴장이 놓침 도주의 끝인가(<see cref="Despawn"/> 직전에 선다). 스포너가 본다 — 도주는 개체가 죽은 게 아니라
        /// 달아난 것이라, 자리를 비우고 1~2분 기다리는 대신 같은 개체를 플레이어 눈 밖 다른 자리로 옮긴다.
        /// </summary>
        internal bool Fled => fled;
        private bool fled;

        // NonAlloc 결과는 거리순이 아니다 — 버퍼가 차면 가장 가까운 벽이 빠졌을 수 있어 그 방향은 막힌 것으로 친다.
        private static readonly RaycastHit[] fleeProbeHits = new RaycastHit[32];
        private static Vector3 fleeProbeOrigin;
        private static Func<Vector3, float> fleeClearanceProbe;
        private static Func<Vector3, float> approachClearanceProbe;   // 습격 접근용(울타리 층 포함) — 한 번만 묶는다
        /// <summary>도주 경로 측정 높이(발 위 m)와 굵기 — 낮은 풀·돌턱은 넘고 벽·줄기·바위 옆면에는 걸린다.</summary>
        private const float FleeProbeHeight = 0.6f;
        private const float FleeProbeRadius = 0.3f;

        // Camera.main은 매 호출마다 FindGameObjectWithTag — 최대 20마리×매 프레임 핫패스 회피.
        private static Camera cachedMainCam;

        public InsectData Data => data;
        public int Level => level;
        public bool IsShiny => shiny;

        /// <summary>
        /// 「지워진 개체」 — 이름을 빼앗겨 검은 실루엣이 된 개체. 2막 리전에서만 나온다.
        ///
        /// <b>포획하면 보통 개체가 된다.</b> 이 플래그는 월드에 서 있는 동안의 외형과 이름표에만
        /// 걸리고 <c>PlayerInsectData</c>로 넘어가지 않는다 — 잡는 행위가 곧 이름을 되찾아주는
        /// 것이라는 게 2막 서사의 골자다. 그래서 세이브에 필드를 늘릴 필요도 없다.
        /// </summary>
        public bool IsErased => erased;

        /// <summary>
        /// 플레이어에게 보여줄 종명. <b>「지워진 개체」는 이름을 빼앗긴 상태</b>라 포획 전엔 밝히지 않는다.
        ///
        /// 월드 모델은 검은 실루엣 + <c>"??? Lv.N"</c> 이름표로 그리면서 정작 포획 선택창과 미니게임
        /// HUD가 본명을 그대로 띄우고 있었다 — 연출이 감춘 것을 UI가 바로 다음 화면에서 알려주는 셈이라
        /// <c>CaptureFeedbackController</c>의 "이름을 되찾아주었다" 회수 문구가 이미 아는 이름을
        /// 반복하는 말이 됐다. 표시명을 여기 한 곳으로 모아 그 루프를 닫는다.
        /// </summary>
        public string DisplayNameForPlayer =>
            (erased || Data == null) ? "???" : Data.displayName;
        /// <summary>이 개체가 수문장인가. <see cref="GuardianRegionId"/>가 곧 답이다.</summary>
        public bool IsGuardian => !string.IsNullOrEmpty(guardianRegionId);

        /// <summary>
        /// 플레이어가 다가가 걸 수 있는가.
        ///
        /// <b><c>forBattle</c>에 수문장 예외가 필요하다.</b> 그 플래그는 두 가지를 겸하는데
        /// ①배회·도주하지 않는 정적 개체 ②아레나 전시용이라 상호작용 금지 —
        /// 수문장은 ①만 필요하고 ②는 아니다. 예외 없이 두면 <b>수문장에게 말을 걸 수 없다</b>:
        /// 접근 판정 3곳(<c>CaptureInputController</c>·<c>WorldInteractionController</c>·
        /// <c>CatcherKidNpc</c>)이 전부 이 프로퍼티로 거른다.
        ///
        /// 실제로 그 상태였다. 수문장의 <c>BuildForBattle</c>은 최초 커밋부터 있었고
        /// <c>!forBattle</c> 조건이 <b>나중에</b> 들어오면서 수문장이 조용히 장식물이 됐다 —
        /// 예외도 경고도 없고, 격파 판정이 종·레벨만 봐서 <b>야생 동종을 이기면 리전이 열렸기 때문에
        /// 아무도 눈치채지 못했다</b>. 그 우회로를 막으려면(정체성 판정) 이 예외가 함께 있어야 한다.
        /// 없으면 진행이 영구 정지한다.
        /// </summary>
        public bool CanBeEngaged =>
            (!forBattle || IsGuardian) && !engaged && alertState != 2 && !despawnedThisCycle;
        /// <summary>소속 리전 ID(필드 곤충). 서브에리어·배틀·수문장 개체는 빈 문자열.</summary>
        public string RegionId => regionId;

        /// <summary>포획·전투에 붙잡혀 있는가. 스포너는 이 개체를 거두거나 바꾸지 않는다.</summary>
        public bool IsEngaged => engaged;

        /// <summary>플레이어를 알아챘거나(경계) 달아나는 중인가. 스포너가 수명 교체를 미룬다.</summary>
        public bool IsAlerted => alertState != 0;

        /// <summary>
        /// 습격 중인가(멈칫해 노려보거나 다가오는 중). 연출(경계 포즈·붉은 기운)이 읽을 자리다 — 모양·색은 visual-dev 영역이라 여기서 칠하지 않는다.
        /// </summary>
        public bool IsAmbushing => alertState == AmbushState;

        /// <summary>이 몸의 습격 쿨다운이 끝날 때까지 남은 시간(초, 0 이상).</summary>
        public float AmbushCooldownLeft => AmbushRules.Remaining(Time.time, ambushReadyTime);

        /// <summary>
        /// 몸을 지을 때마다(<see cref="Initialize(InsectData,int,string,Action{InsectEntity},bool,bool)"/>·
        /// <see cref="BuildForBattle"/>) 새로 붙는 번호. 풀 객체는 같은 참조가 다른 개체로 되살아나므로,
        /// 참조를 쥔 쪽(지도 레이드 마커)이 "아직 그 개체인가"를 이걸로 묻는다 — 활성 여부만 보면
        /// 한 프레임 안에 거뒀다 다시 꺼낸 몸을 옛 개체로 착각한다.
        /// </summary>
        public int SpawnSerial => spawnSerial;

        /// <summary>
        /// 이 개체가 <b>어느 리전의 수문장인가</b>. 수문장이 아니면 빈 문자열이다.
        ///
        /// <b>왜 좌표가 아니라 정체성인가.</b> 예전엔 격파 판정이 "수문장 자리에서 15m 안이었나"를
        /// 봤는데, 그 반경은 야생 스폰이 그대로 들어왔다 — 당시 스포너는 현재 리전 스폰 포인트를
        /// <b>플레이어로부터 10~43m</b> 나선 위로 끌어오고 거기서 다시 5m만큼 흩었다. 최근접 스폰이 플레이어에서 5m였다.
        /// 지금은 야생이 리전 원판 전체에 흩어져 기록되지만(<c>FieldPopulation</c>) 수문장 앞이라고 비켜 두지 않으니
        /// <b>야생이 반경 안에 들어오는 건 여전히 구조</b>고,
        /// 13곳 중 9곳은 수문장 종이 자기 리전 야생 풀에도 있어 종·레벨 조건까지 함께 맞는다.
        ///
        /// 그래서 "그 자리였나"가 아니라 <b>"바로 그 개체였나"</b>를 묻는다. 수문장은
        /// <c>PlaySceneBootstrap.SpawnGuardianInsect</c>가 <c>new GameObject</c>로 따로 세우는
        /// 단 하나의 개체라(풀에서 오지 않는다) 이 값이 곧 확정 답이다.
        /// </summary>
        public string GuardianRegionId => guardianRegionId;

        /// <summary>
        /// 수문장으로 표식한다. <c>BuildForBattle</c> <b>뒤에</b> 부를 것 — 그쪽이 표식을 지운다.
        /// </summary>
        public void MarkAsGuardian(string regionId)
        {
            guardianRegionId = string.IsNullOrEmpty(regionId) ? string.Empty : regionId;
        }

        /// <summary>
        /// 색다름·지워짐을 여기서 굴려 세운다 — 기록 없이 한 번 쓰고 마는 개체용(옛 진입점, 테스트).
        /// 필드 곤충은 스포너가 슬롯에 굴려 둔 값을 넘기는 아래 오버로드를 쓴다.
        /// </summary>
        public void Initialize(InsectData insectData, int insectLevel, SpawnPoint point,
            Action<InsectEntity> despawnCallback, float erasedChance = 0f)
        {
            bool rolledShiny = UnityEngine.Random.value < FieldShinyChance;
            // 지워진 개체 — 확률은 스폰너가 리전에서 정해 넘긴다(여기에 리전 목록을 두지 않는다).
            bool rolledErased = erasedChance > 0f && UnityEngine.Random.value < erasedChance;
            Initialize(insectData, insectLevel, point != null ? point.regionId : null, despawnCallback,
                rolledShiny, rolledErased);
        }

        /// <summary>
        /// <b>기록된 개체를 그대로</b> 세운다 — 종·레벨·색다름·지워짐을 굴리지 않는다. 스포너가 슬롯(<c>FieldSlot</c>)에
        /// 적어 둔 값을 넘기므로, 멀어졌다 돌아와 몸을 다시 세워도 같은 곤충이다. 자리는 호출 전에 옮겨 둔다
        /// (<c>transform.position</c>이 곧 배회·풀 더미의 기준 <c>basePosition</c>이 된다).
        /// </summary>
        public void Initialize(InsectData insectData, int insectLevel, string homeRegionId,
            Action<InsectEntity> despawnCallback, bool isShiny, bool isErased)
        {
            data = insectData;
            level = insectLevel;
            regionId = homeRegionId ?? string.Empty;
            onDespawn = despawnCallback;
            shiny = isShiny;
            erased = isErased;
            spawnSerial = ++nextSpawnSerial;
            // 풀 재사용 회귀 방지: BuildForBattle에서 true로 설정된 forBattle이 남아있으면
            // 다음 Update에서 회전 안 하는 정적 곤충이 됨. 매 Initialize마다 명시적 false.
            forBattle = false;
            // 풀 재사용 시 stale Transform 참조 회피 (ClearChildren 직후 cache 무효).
            cachedNameLabel = null;
            cachedShinySparkle = null;
            cachedShinyShift = -1f;
            cachedMoveStyle = -1;
            cachedGroundMarker = null;
            cachedGrass = null;
            cachedWingL = null;
            cachedWingR = null;
            wingsResolved = false;
            nameLabelResolved = false;
            alertState = 0;
            fleeTimer = 0f;
            fleeAllowed = 0f;
            fleeTravelled = 0f;
            engaged = false;
            despawnedThisCycle = false;
            fled = false;
            guardianRegionId = string.Empty;   // 풀에서 왔다면 직전 개체의 표식을 물려받지 않는다
            // 습격 — 풀에서 왔다면 직전 개체의 쿨다운·쫓기를 물려받지 않는다. 종이 습격형인지는 여기서 한 번만 본다.
            ResetAmbush();
            ambusherSpecies = data != null && InsectHabits.For(data).Temperament == InsectTemperament.Ambusher;
            fleeArea = null;                   // 풀 재사용 — 섬 손님이던 몸이 필드에서 섬 경계를 물려받지 않게

            ClearChildren();
            BuildModel();
            AddRarityEffects();
            CreateNameLabel();
            CreateGroundMarker();
            float scale = GetRarityScale();
            transform.localScale = Vector3.one * scale;
            basePosition = transform.position;
            grassAnchor = basePosition;
            baseSurfaceY = Core.FieldGround.SurfaceY(basePosition.x, basePosition.z);
            bobPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            wingPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        public void BuildForBattle(InsectData insectData, int insectLevel, bool shinyOverride,
            bool erasedOverride = false)
        {
            data = insectData;
            level = insectLevel;
            shiny = shinyOverride;
            // 풀 재사용 회귀 방지 — 명시하지 않으면 직전 개체의 erased가 남아 도감 프리뷰까지 검게 나온다.
            erased = erasedOverride;
            forBattle = true;
            regionId = string.Empty;
            spawnSerial = ++nextSpawnSerial;
            cachedNameLabel = null;
            cachedShinySparkle = null;
            cachedShinyShift = -1f;
            cachedMoveStyle = -1;
            cachedGroundMarker = null;
            cachedGrass = null;
            cachedWingL = null;
            cachedWingR = null;
            wingsResolved = false;
            nameLabelResolved = false;
            alertState = 0;
            fleeTimer = 0f;
            fleeAllowed = 0f;
            fleeTravelled = 0f;
            engaged = false;
            despawnedThisCycle = false;
            fled = false;
            guardianRegionId = string.Empty;   // 풀에서 왔다면 직전 개체의 표식을 물려받지 않는다
            ResetAmbush();
            ambusherSpecies = false;           // 아레나·전시·수문장 몸은 덤벼들지 않는다
            fleeArea = null;

            ClearChildren();
            BuildModel();
            basePosition = transform.position;
            grassAnchor = basePosition;
            bobPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            wingPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        }

        private void ClearChildren()
        {
            // 인스턴스 머티리얼 정리 — ApplyColorRaw가 파트마다 new Material을 .material로 할당하는데
            // GameObject 파괴로는 머티리얼이 자동 해제되지 않아(수동 Destroy 필요) 풀 재사용/리스폰마다
            // 수십 개씩 누수(장시간 탐험 시 모바일 OOM). .material 게터는 인스턴스만 반환/생성하므로
            // 공유 에셋 머티리얼은 건드리지 않아 안전.
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i].transform == transform) continue;
                if (renderers[i].sharedMaterial != null) DestroyImmediate(renderers[i].material);
            }
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
        }

        private void Update()
        {
            UpdateMovement();

            AnimateWings();
            if (shiny) AnimateShinySparkle();

            // Camera.main 매 프레임 FindGameObjectWithTag 회피 — static cache.
            if (cachedMainCam == null) cachedMainCam = Camera.main;
            if (cachedMainCam != null)
            {
                // 배틀 모델엔 NameLabel이 없다 — null 재시도로 두면 그 개체는 매 프레임 Find가
                // 영원히 실패한다(실패하는 Find가 자식 전체를 훑어 가장 비싸다). 1회만 찾는다.
                if (!nameLabelResolved)
                {
                    nameLabelResolved = true;
                    cachedNameLabel = transform.Find("NameLabel");
                }
                if (cachedNameLabel != null)
                    cachedNameLabel.rotation = cachedMainCam.transform.rotation;
            }
        }

        // 필드 곤충 이동 + 긴장감(경계/도주): 평소엔 풀숲에 낮게 숨어 배회/비행/점프, 플레이어가 다가오면
        // 경계(고개 들고 떨며 주시)하고, 무심코 빠르게 접근하면 도망쳐 사라진다.
        // 포획 중(engaged) 또는 플레이어 정지 시엔 도주하지 않음(SetFrozen으로 정지 → playerSpeed 0).
        private void UpdateMovement()
        {
            float t = Time.time;
            // The arena owns battle root translation and rotation (lunge, recoil,
            // grounded idle). Wing animation still runs separately in Update.
            if (forBattle) return;

            EnsureMoveStyle();
            UpdatePlayerTracking();
            float dt = Time.deltaTime;

            // ===== 도주 진행 (이동 방식별로 다른 도주 모션) =====
            // 도주는 1.1초에 최대 8m를 간다 — 높이를 스폰 자리(basePosition.y)에 묶어 두면 사구·재 더미를 지날 때 몸이
            // 둔덕 속에 묻히고, 둔덕 위에서 내려오면 허공에 뜬다. 세 모션 모두 지금 자리 지면만큼 오르내린다(GroundRise).
            // 수평으로는 도주를 시작할 때 잰 거리(fleeAllowed)까지만 간다 — 벽·건물·줄기를 뚫지 않고 그 앞에 멈춘다(BeginFlee).
            if (alertState == 2)
            {
                fleeTimer -= dt;
                float elapsed = 1.1f - fleeTimer; // 도주 경과 시간
                if (cachedMoveStyle == 1)
                {
                    // 비행: 날개로 날아오르며 멀어짐 — 점점 고도 상승(하늘로 사라짐)
                    Vector3 p = transform.position + FleeStep(7.5f * dt);
                    p.y = basePosition.y + GroundRise(baseSurfaceY, p.x, p.z) + 0.55f + elapsed * 2.8f;
                    transform.position = p;
                    FaceFlee(dt, 8f);
                }
                else if (cachedMoveStyle == 2)
                {
                    // 점프: 큰 포물선 도약으로 튀어 달아남 — 공중에 뜬 동안 더 멀리, 착지 땐 멈칫
                    float ph = (elapsed % 0.45f) / 0.45f;
                    float hop = Mathf.Sin(ph * Mathf.PI);
                    Vector3 p = transform.position + FleeStep(6.5f * (0.3f + hop) * dt);
                    p.y = basePosition.y + GroundRise(baseSurfaceY, p.x, p.z) + hop * 0.75f;
                    transform.position = p;
                    FaceFlee(dt, 11f);
                }
                else
                {
                    // 기어다님: 지면에 낮게 빠르게 허둥지둥
                    Vector3 p = transform.position + FleeStep(6.0f * dt);
                    p.y = basePosition.y + GroundRise(baseSurfaceY, p.x, p.z) + 0.05f + Mathf.Abs(Mathf.Sin(elapsed * 24f)) * 0.07f;
                    transform.position = p;
                    FaceFlee(dt, 12f);
                }
                AnchorGrass();
                if (fleeTimer <= 0f) { fled = true; Despawn(); } // 놓침 — 눈앞에서 사라짐(스포너가 개체를 다른 자리로 옮긴다)
                return;
            }

            // ===== 습격 진행 — 멈칫 → 다가가기(AmbushRules). 붙잡히면(engaged) 아래 경계 분기가 받는다 =====
            if (alertState == AmbushState && !engaged)
            {
                UpdateAmbush(dt, t);
                return;
            }

            float dist = cachedPlayer != null ? Vector3.Distance(transform.position, cachedPlayer.position) : 999f;
            float skit = Skittishness();
            float alertR = 6.5f + skit * 1.6f;   // 레어할수록 먼 거리에서 눈치챔
            float fleeR = 2.2f + skit * 0.8f;
            bool moving = playerSpeed > 1.5f;

            if (engaged)
            {
                alertState = 1; // 포획 중 — 경계 포즈 유지, 도주 분기 진입 안 함
            }
            else if (ambusherSpecies && dist < AmbushRules.NoticeRadius && TryBeginAmbush())
            {
                // 깨어 있는 습격형 — 달아나지 않고 덤벼든다. 판정이 막으면(잠잠·쿨다운·싸울 곤충 없음 등) 아래로 내려가 온순한 곤충처럼 군다.
                UpdateAmbush(dt, t);
                return;
            }
            else if (dist < alertR)
            {
                if (alertState != 1) { alertState = 1; patience = 2.6f - skit * 0.95f; alertGraceTimer = 0.5f; }
                alertGraceTimer -= dt;
                if (moving) patience -= dt * (dist < fleeR ? 3.0f : 1.2f);
                else patience -= dt * 0.2f; // 멈추면 거의 안 닳음(E 누를 시간 확보)
                bool burst = moving && dist < fleeR && playerSpeed > 4f; // 코앞으로 돌진하면 즉시
                if (alertGraceTimer <= 0f && (patience <= 0f || burst))
                {
                    // 아이템 도주 방지 확률 — 활성 시 확률적으로 도주 취소(patience 리셋으로 다시 버팀).
                    float fp = FleePreventChanceProvider != null ? FleePreventChanceProvider() : 0f;
                    if (fp > 0f && UnityEngine.Random.value < fp)
                    {
                        patience = 2.6f - skit * 0.95f;
                        alertGraceTimer = 0.5f;
                    }
                    else
                    {
                        BeginFlee(transform.position - cachedPlayer.position);
                        return;
                    }
                }
            }
            else
            {
                alertState = 0;
            }

            Vector3 offset;
            float rotSpeed;
            if (alertState == 1)
            {
                // 경계: 배회 정지 + 긴장 떨림 + 풀 위로 확실히 솟아 주시("들켰다", 종류 식별 가능)
                float tremble = Mathf.Sin(t * 27f) * 0.05f;
                float rise = (cachedMoveStyle == 1)
                    ? 0.55f + Mathf.Sin(t * 5f) * 0.18f
                    : 0.34f + Mathf.Abs(Mathf.Sin(t * 6f)) * 0.06f;
                offset = new Vector3(tremble, rise, 0f);
                rotSpeed = 0f;
                if (cachedPlayer != null)
                {
                    Vector3 look = cachedPlayer.position - transform.position; look.y = 0f;
                    if (look.sqrMagnitude > 0.01f)
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), dt * 6f);
                }
            }
            else if (cachedMoveStyle == 1)
            {
                // 날개: 공중 부유 + 좌우/앞뒤 드리프트(나는 느낌) + 빠른 회전
                float bob = Mathf.Sin(t * 4.5f + bobPhase) * 0.5f;
                float driftX = Mathf.Sin(t * 1.9f + wingPhase) * 0.55f;
                float driftZ = Mathf.Sin(t * 1.4f + wingPhase * 1.7f) * 0.4f;
                offset = new Vector3(driftX, 0.55f + bob, driftZ);
                rotSpeed = 36f;
            }
            else if (cachedMoveStyle == 2)
            {
                // 긴 다리: 점프하듯 — 주기적 포물선 도약 + 착지 사이 짧은 정지
                float cycle = 1.3f;
                float phase = ((t + bobPhase * 0.3f) % cycle) / cycle;
                float hop = phase < 0.55f ? Mathf.Sin(phase / 0.55f * Mathf.PI) * 0.85f : 0f;
                offset = new Vector3(0f, hop, 0f);
                rotSpeed = 12f;
            }
            else
            {
                // 일반(기어다님): 풀 위로 몸이 보이게 살짝 올라와 배회 + 느린 회전
                float bs = 1.6f + (bobPhase % 1.5f);
                offset = new Vector3(0f, 0.1f + Mathf.Sin(t * bs + bobPhase) * 0.12f, 0f);
                rotSpeed = 8f;
            }

            Vector3 pos = basePosition + offset;
            // 수평으로 떠도는 건 비행 드리프트(±0.55m)·경계 떨림뿐이다 — 재 더미 가장자리(턱 0.33m)에서는 그만큼으로도
            // 둔덕 속을 드나든다. 제자리 모션(기어다님·점프)은 xz가 0이라 둔덕 조회를 건너뛴다.
            if (offset.x != 0f || offset.z != 0f)
                pos.y += GroundRise(baseSurfaceY, pos.x, pos.z);
            transform.position = pos;
            if (rotSpeed > 0f)
                transform.Rotate(Vector3.up, rotSpeed * Time.deltaTime, Space.World);

            // 오르내린 지면만큼은 마커도 따라간다 — 상쇄는 모션 높이(offset.y)만(마커는 발밑 지면에 붙는다)
            AnchorGroundMarker(offset.y);
            AnchorGrass();
        }

        /// <summary>
        /// (x, z) 지면이 스폰 자리 지면(<paramref name="baseSurfaceY"/>)보다 얼마나 높은가(m, 음수면 낮다). 둔덕은 콜라이더가
        /// 없어 <see cref="Core.FieldGround"/>에 묻는다 — 스폰(<c>InsectSpawner.PickSpawnPosition</c>)과 같은 지면이다.
        /// 서브에리어는 (2000,·,2000) 너머라 둔덕이 없어 늘 0이다. 스폰 자리가 둔덕 밖 콜라이더(물가 바위 등) 위였다면
        /// 그 높이는 옛날처럼 이동 내내 유지된다 — 콜라이더를 매 프레임 쏘지 않는다.
        /// </summary>
        internal static float GroundRise(float baseSurfaceY, float x, float z)
            => Core.FieldGround.SurfaceY(x, z) - baseSurfaceY;

        // 이동 스타일 1회 판정(캐시): grasshopper/cricket/katydid=점프, WingL 보유=비행, 그 외=일반.
        // 지상 곤충(기어다님/점프)은 풀숲 은신 더미 생성(비행 곤충은 공중이라 제외).
        private void EnsureMoveStyle()
        {
            if (cachedMoveStyle >= 0) return;
            string id = data != null ? data.insectId ?? "" : "";
            if (id.Contains("grasshopper") || id.Contains("cricket") || id.Contains("katydid"))
                cachedMoveStyle = 2;
            else if (transform.Find("WingL") != null)
                cachedMoveStyle = 1;
            else
                cachedMoveStyle = 0;
            if (!forBattle && cachedMoveStyle != 1)
                BuildGrassTuft();
        }

        // 포획 상호작용 시작/종료 시 호출 — engaged면 절대 도주 안 함(경계 포즈만 유지).
        // 진입 시 인내심·유예 리셋 → 포획 취소 직후 즉시 도망가지 않게(관대).
        public void SetEngaged(bool value)
        {
            bool wasEngaged = engaged;
            // 다가오던 몸이 붙잡혔다(습격 창·[E]·아이 NPC) — 지금 자리를 기준으로 삼는다. 안 그러면 경계 포즈(basePosition 기준)가
            // 몸을 스폰 자리로 순간이동시킨다(창 뒤에서, 그리고 전투 아레나가 그 자리를 적 위치로 읽는다).
            if (value && alertState == AmbushState) RebaseHere();
            engaged = value;
            if (value) { alertState = 1; patience = 2.6f; alertGraceTimer = 0.6f; }
            // 풀려났다(포획 창 취소·습격 도망 등) — 한동안 덤벼들지 않는다. 창을 닫자마자 바로 옆에서 다시 닿지 않게.
            else if (wasEngaged) ambushReadyTime = Mathf.Max(ambushReadyTime, Time.time + AmbushRules.EntityCooldownSeconds);
        }

        public void ScareAway()
        {
            if (!CanBeEngaged) return;

            UpdatePlayerTracking();
            BeginFlee(cachedPlayer != null
                ? transform.position - cachedPlayer.position
                : transform.forward);
        }

        /// <summary>
        /// 도주를 시작한다 — 도주가 시작되는 두 곳(인내 소진·<see cref="ScareAway"/>)이 함께 쓴다.
        ///
        /// 플레이어 반대쪽부터 좌우로 벌려 가며 몸 높이에서 장애물을 재고(<see cref="FleePath.Choose"/>) 뚫린 방향을 고른다.
        /// 다 막혔으면 가장 멀리 가는 쪽으로 <b>장애물 앞까지만</b> 간다. 곤충은 몸 콜라이더가 없어 물리가 막아 주지
        /// 않으므로 여기서 재지 않으면 벽·건물·바위·나무 줄기, 서브에리어 방 벽을 그대로 뚫고 나간다.
        /// 측정은 도주 시작 때 한 번이다(최대 7방향) — 매 프레임 쏘지 않는다.
        /// </summary>
        private void BeginFlee(Vector3 away)
        {
            alertState = 2;
            fleeTimer = 1.1f;
            fleeTravelled = 0f;
            away.y = 0f;
            if (away.sqrMagnitude <= 0.01f) away = Vector3.forward;

            if (fleeClearanceProbe == null) fleeClearanceProbe = MeasureFleeClearance;
            fleeProbeOrigin = new Vector3(transform.position.x, basePosition.y + FleeProbeHeight, transform.position.z);
            float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            Func<Vector3, float> probe = fleeClearanceProbe;
            if (fleeArea != null)
            {
                // 구역이 정해진 몸(섬 손님) — 벽까지의 거리와 구역 끝까지의 거리 중 짧은 쪽. 도주를 시작할 때만 만든다.
                Func<Vector3, Vector3, float> area = fleeArea;
                Vector3 origin = fleeProbeOrigin;
                probe = dir => Mathf.Min(MeasureFleeClearance(dir), area(origin, dir));
            }
            fleeDir = FleePath.Choose(away, side, probe, out fleeAllowed);
        }

        /// <summary>
        /// 도주가 이 구역 밖으로 나가지 않게 한다 — <paramref name="areaRun"/>은 (출발점, 방향) → 그 방향으로 구역 안에서 갈 수 있는
        /// 거리(m). 나의 섬 손님 곤충이 "빈 칸 위에서만" 달아나게 쓴다(물리 측정은 벽·건물만 본다). <see cref="Initialize(InsectData,int,string,Action{InsectEntity},bool,bool)"/>와
        /// <see cref="BuildForBattle"/>이 지우므로 몸을 세운 <b>뒤에</b> 부를 것.
        /// </summary>
        public void SetFleeArea(Func<Vector3, Vector3, float> areaRun)
        {
            fleeArea = areaRun;
        }

        /// <summary>
        /// <c>fleeProbeOrigin</c>에서 <paramref name="dir"/>로 막히지 않고 갈 수 있는 거리(m). 트리거(NPC 몸통·줍기 구)는
        /// 장애물이 아니고, 곤충·플레이어 몸도 아니다. 위를 향한 면(바닥·바위 윗면)은 올라타는 곳이지 벽이 아니다.
        /// </summary>
        private static float MeasureFleeClearance(Vector3 dir) => MeasureClearance(dir, Physics.DefaultRaycastLayers);

        /// <summary>
        /// 습격 접근용 — 도주 측정과 같되 <b>Ignore Raycast 층도 본다</b>. 울타리 난간 차단(<c>RegionTerrainBuilder.AddRailBlocker</c>)이
        /// 카메라·탭 레이를 피하려고 그 층에 있어서, 기본 층만 쏘면 플레이어를 향해 울타리를 뚫고 온다. 플레이어 몸도 그 층이지만 아래에서 거른다.
        /// </summary>
        private static float MeasureApproachClearance(Vector3 dir) => MeasureClearance(dir, Physics.AllLayers);

        private static float MeasureClearance(Vector3 dir, int layerMask)
        {
            float length = FleePath.ProbeLength;
            int n = Physics.SphereCastNonAlloc(fleeProbeOrigin, FleeProbeRadius, dir, fleeProbeHits, length,
                layerMask, QueryTriggerInteraction.Ignore);
            if (n >= fleeProbeHits.Length) return 0f;   // 버퍼가 찼다 — 빠진 충돌 중 벽이 있을 수 있다
            float nearest = length;
            for (int i = 0; i < n; i++)
            {
                RaycastHit hit = fleeProbeHits[i];
                Collider c = hit.collider;
                if (c == null) continue;
                if (c.GetComponentInParent<InsectEntity>() != null) continue;
                if (c.GetComponentInParent<Core.PlayerMovement>() != null) continue;
                // 시작부터 겹친 콜라이더는 거리 0으로 온다 — 그 방향은 막힌 것이다(법선을 믿지 않는다).
                if (hit.distance > 0f && hit.normal.y > 0.7f) continue;
                if (hit.distance < nearest) nearest = hit.distance;
            }
            return nearest;
        }

        /// <summary>이번 프레임 도주 이동 — 시작 때 잰 허용 거리를 넘지 않는다(장애물 앞에서 멈춤).</summary>
        private Vector3 FleeStep(float wanted)
        {
            float step = FleePath.StepDistance(wanted, fleeAllowed, fleeTravelled);
            fleeTravelled += step;
            return fleeDir * step;
        }

        // ── 습격(AmbushRules) ──────────────────────────────────────────────

        private void ResetAmbush()
        {
            ambushReadyTime = 0f;
            ambushHesitate = 0f;
            ambushChaseTime = 0f;
            ambushReplanTimer = 0f;
            ambushStuckTime = 0f;
            ambushLegAllowed = 0f;
            ambushLegTravelled = 0f;
            ambushAnnounced = false;
        }

        /// <summary>
        /// 깨어 있는 습격형이 플레이어를 알아챘다 — 판정(<see cref="AmbushGate"/>)이 허가하면 습격을 시작한다(멈칫부터).
        /// 막히면 false — 호출부가 온순한 곤충의 경계·도주로 내려간다.
        /// </summary>
        private bool TryBeginAmbush()
        {
            if (forBattle || IsGuardian || despawnedThisCycle || cachedPlayer == null) return false;
            if (Time.time < ambushReadyTime) return false;   // 판정도 보지만, 묻기 전에 싸게 거른다
            if (AmbushGate == null || AmbushGate(this) != AmbushRefusal.None) return false;

            alertState = AmbushState;
            ambushHesitate = AmbushRules.HesitateSeconds;
            ambushChaseTime = 0f;
            ambushReplanTimer = 0f;
            ambushStuckTime = 0f;
            ambushLegAllowed = 0f;
            ambushLegTravelled = 0f;
            ambushAnnounced = false;
            // 한 습격 동안 같은 쪽으로 돌아간다 — 벽 앞에서 좌우로 흔들리지 않게(늘 같은 쪽이면 곤충들이 한쪽으로만 돈다).
            ambushSide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            return true;
        }

        /// <summary>
        /// 습격 한 프레임 — 판정을 다시 묻고(대화·메뉴면 기다리고, 그 밖에 막히면 물러난다) 멈칫 → 다가가기 → 닿기.
        /// 다가가는 길은 <see cref="AmbushRules.ReplanSeconds"/>마다 몸 높이에서 장애물을 재어 고른다 — 곤충은 몸 콜라이더가 없어
        /// 물리가 막아 주지 않으므로, 재지 않으면 벽·건물·바위·울타리를 그대로 뚫고 온다(도주와 같은 이유).
        /// </summary>
        private void UpdateAmbush(float dt, float t)
        {
            if (cachedPlayer == null) { GiveUpAmbush(); return; }
            AmbushRefusal gate = AmbushGate != null ? AmbushGate(this) : AmbushRefusal.NotAwake;
            if (gate != AmbushRefusal.None)
            {
                if (AmbushRules.IsPauseOnly(gate)) HoldAmbushPose(dt, t);   // 대화·메뉴 — 그 자리에서 노려보며 기다린다
                else GiveUpAmbush();
                return;
            }

            Vector3 toPlayer = cachedPlayer.position - transform.position;
            toPlayer.y = 0f;
            float planar = toPlayer.magnitude;

            if (ambushHesitate > 0f)
            {
                ambushHesitate -= dt;
                HoldAmbushPose(dt, t);
                if (AmbushRules.HasReached(planar)) ReachPlayer();   // 멈칫하는 사이 플레이어가 먼저 다가와 닿았다
                return;
            }

            if (!ambushAnnounced)
            {
                ambushAnnounced = true;
                RaiseAmbushEvent(AmbushStarted);
                if (engaged || alertState != AmbushState) return;    // 듣는 쪽이 이 곤충을 붙잡았다
            }

            if (AmbushRules.HasReached(planar)) { ReachPlayer(); return; }

            ambushChaseTime += dt;
            if (AmbushRules.ShouldGiveUp(planar, ambushChaseTime)) { GiveUpAmbush(); return; }

            // 길은 일정 간격으로만 다시 잰다. 걸음을 다 써도 바로 재지 않는다 — 막혀서 조금씩만 열리는 곳에서 매 프레임 쏘지 않게.
            ambushReplanTimer -= dt;
            if (ambushReplanTimer <= 0f)
            {
                PlanAmbushLeg(toPlayer, planar);
                ambushReplanTimer = AmbushRules.ReplanSeconds;
            }

            float step = FleePath.StepDistance(AmbushRules.ApproachSpeed * dt, ambushLegAllowed, ambushLegTravelled);
            ambushLegTravelled += step;
            if (step <= 1e-4f)
            {
                ambushStuckTime += dt;
                if (ambushStuckTime > AmbushRules.StuckGiveUpSeconds) { GiveUpAmbush(); return; }
            }
            else
            {
                ambushStuckTime = 0f;
            }

            Vector3 p = transform.position + ambushDir * step;
            float hover = AmbushMotionHeight(t, true);
            p.y = basePosition.y + GroundRise(baseSurfaceY, p.x, p.z) + hover;
            transform.position = p;
            if (ambushDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(ambushDir), dt * 10f);
            AnchorGroundMarker(hover);
            AnchorGrass();
        }

        /// <summary>이번 걸음의 방향과 거리를 잰다 — 몸 높이(지금 자리 지면 + <see cref="FleeProbeHeight"/>)에서 플레이어 쪽부터.</summary>
        private void PlanAmbushLeg(Vector3 toPlayer, float planar)
        {
            if (approachClearanceProbe == null) approachClearanceProbe = MeasureApproachClearance;
            Vector3 pos = transform.position;
            fleeProbeOrigin = new Vector3(pos.x, basePosition.y + GroundRise(baseSurfaceY, pos.x, pos.z) + FleeProbeHeight, pos.z);
            ambushDir = AmbushRules.ChooseApproach(toPlayer, planar, ambushSide, approachClearanceProbe, out ambushLegAllowed);
            ambushLegTravelled = 0f;
        }

        /// <summary>
        /// 멈칫·기다림 — 지금 자리에서 고개를 들고 떨며 플레이어를 노려본다. 높이는 경계 포즈와 같고, 기준은 스폰 자리가 아니라
        /// <b>지금 자리</b>다(다가오던 도중에 멈춰도 제자리로 튀지 않는다).
        /// </summary>
        private void HoldAmbushPose(float dt, float t)
        {
            Vector3 p = transform.position;
            float hover = AmbushMotionHeight(t, false);
            p.y = basePosition.y + GroundRise(baseSurfaceY, p.x, p.z) + hover + Mathf.Sin(t * 27f) * 0.02f;
            transform.position = p;
            if (cachedPlayer != null)
            {
                Vector3 look = cachedPlayer.position - p;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), dt * 8f);
            }
            AnchorGroundMarker(hover);
            AnchorGrass();
        }

        /// <summary>습격 중 몸 높이(지면 위 m). 노려볼 때는 경계 포즈 높이, 다가올 때는 날것은 떠서·뛰는 것은 뛰며·기는 것은 낮게 빠르게.</summary>
        private float AmbushMotionHeight(float t, bool approaching)
        {
            if (cachedMoveStyle == 1) return 0.55f + Mathf.Sin(t * 5f) * (approaching ? 0.12f : 0.18f);
            if (!approaching) return 0.34f + Mathf.Abs(Mathf.Sin(t * 6f)) * 0.06f;
            if (cachedMoveStyle == 2) return Mathf.Sin(((t % 0.45f) / 0.45f) * Mathf.PI) * 0.6f;
            return 0.06f + Mathf.Abs(Mathf.Sin(t * 20f)) * 0.06f;
        }

        /// <summary>
        /// 닿았다 — 듣는 쪽이 「습격!」 창을 열면 <see cref="SetEngaged"/>로 경계 상태가 된다. 아무도 안 열었으면(창이 다른 일로 막혔다)
        /// 닿은 채 매 프레임 다시 쏘지 않도록 물러난다.
        /// </summary>
        private void ReachPlayer()
        {
            RaiseAmbushEvent(AmbushReached);
            if (!engaged && alertState == AmbushState) GiveUpAmbush();
        }

        /// <summary>
        /// 쫓기를 접고 물러난다 — 놓침 도주와 같은 길이다(스포너가 같은 개체를 플레이어 눈 밖 다른 자리로 옮긴다). 다가오며 스폰 자리를
        /// 떠난 몸을 제자리로 순간이동시키지 않고 치우는 가장 단순한 길이다. 몸에는 쿨다운을 걸어 둔다.
        /// </summary>
        private void GiveUpAmbush()
        {
            ambushReadyTime = Mathf.Max(ambushReadyTime, Time.time + AmbushRules.EntityCooldownSeconds);
            alertState = 1;
            BeginFlee(cachedPlayer != null ? transform.position - cachedPlayer.position : transform.forward);
        }

        /// <summary>
        /// 지금 자리를 배회·경계 포즈의 기준으로 삼는다 — 습격으로 스폰 자리를 떠난 몸이 붙잡혔을 때. 높이 기준도 지금 자리 지면으로 옮긴다
        /// (<see cref="GroundRise"/>가 새 자리에서 0이 되게). 풀 더미는 처음 자리(<c>grassAnchor</c>)에 남는다.
        /// </summary>
        private void RebaseHere()
        {
            Vector3 p = transform.position;
            basePosition = new Vector3(p.x, basePosition.y + GroundRise(baseSurfaceY, p.x, p.z), p.z);
            baseSurfaceY = Core.FieldGround.SurfaceY(p.x, p.z);
        }

        private void RaiseAmbushEvent(Action<InsectEntity> handler)
        {
            if (handler == null) return;
            // 듣는 쪽이 던져도 이 곤충의 Update가 매 프레임 같은 예외로 멈추지 않게 — 닿기는 물러나기로 정리된다.
            try { handler(this); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        // 플레이어 위치/속도 추적 — 프레임당 1회만 계산(전 곤충 공유).
        private static void UpdatePlayerTracking()
        {
            if (playerTrackFrame == Time.frameCount) return;
            playerTrackFrame = Time.frameCount;
            if (cachedPlayer == null)
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p == null) p = GameObject.Find("Player");
                if (p != null) { cachedPlayer = p.transform; lastPlayerPos = cachedPlayer.position; playerSpeed = 0f; }
                return;
            }
            float dt = Time.deltaTime;
            if (dt > 0.0001f)
            {
                Vector3 cur = cachedPlayer.position;
                playerSpeed = (cur - lastPlayerPos).magnitude / dt;
                lastPlayerPos = cur;
            }
        }

        // 레어도별 예민함(0~1.5): 높을수록 멀리서 눈치채고 더 쉽게 도망 — 희귀 포획에 긴장감.
        private float Skittishness()
        {
            if (data == null) return 0f;
            switch (data.rarity)
            {
                case InsectRarity.Uncommon: return 0.3f;
                case InsectRarity.Rare: return 0.6f;
                case InsectRarity.Epic: return 1.0f;
                case InsectRarity.Legendary: return 1.5f;
                default: return 0f;
            }
        }

        // 지면 마커: 곤충이 떠도 항상 지면에 고정 — 부모 상하 이동량을 로컬에서 상쇄.
        private void AnchorGroundMarker(float offsetY)
        {
            if (cachedGroundMarker == null) cachedGroundMarker = transform.Find("GroundMarker");
            if (cachedGroundMarker == null) return;
            float s = transform.localScale.y;
            if (s < 0.0001f) s = 1f;
            Vector3 mlp = cachedGroundMarker.localPosition;
            mlp.y = -0.35f - offsetY / s;
            cachedGroundMarker.localPosition = mlp;
        }

        // 도주 방향을 향해 부드럽게 회전(머리부터 달아남).
        private void FaceFlee(float dt, float turnSpeed)
        {
            if (fleeDir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fleeDir), dt * turnSpeed);
        }

        // 풀 더미는 곤충이 움직여도 제자리(스폰 지점)에 고정 — 자식이지만 월드 좌표를 매 프레임 고정.
        private void AnchorGrass()
        {
            if (cachedGrass == null) return;
            cachedGrass.position = grassAnchor;
            cachedGrass.rotation = Quaternion.identity;
        }

        // 풀 더미: 스폰 지점 '바깥쪽'에 낮게 둘러 곤충을 프레이밍(가리지 않음). 곤충은 풀 위로 몸·특징이 보임.
        // 주의: Unity 캡슐 기본 높이=2유닛 → 실제 높이 = 2*half. half는 작게(0.16~0.28 → 실제 0.32~0.56).
        private void BuildGrassTuft()
        {
            GameObject tuft = new GameObject("GrassTuft");
            tuft.transform.SetParent(transform, false);
            cachedGrass = tuft.transform;
            Color g1 = new Color(0.20f, 0.46f, 0.15f);
            Color g2 = new Color(0.32f, 0.62f, 0.22f);
            const int blades = 7;
            for (int i = 0; i < blades; i++)
            {
                float ang = i * (360f / blades) + (i * 37 % 20);
                float rad = 0.34f + (i % 3) * 0.10f;          // 곤충 바깥쪽(몸을 안 가림)
                float bx = Mathf.Cos(ang * Mathf.Deg2Rad) * rad;
                float bz = Mathf.Sin(ang * Mathf.Deg2Rad) * rad;
                float half = 0.16f + (i % 4) * 0.04f;          // 실제 높이 0.32~0.56 (곤충보다 낮음)
                GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                blade.name = "Blade";
                blade.transform.SetParent(tuft.transform, false);
                blade.transform.localPosition = new Vector3(bx, half - 0.35f, bz); // 뿌리를 지면(-0.35)에
                blade.transform.localScale = new Vector3(0.045f, half, 0.045f);
                blade.transform.localRotation = Quaternion.Euler((i % 2 == 0) ? 20f : -16f, ang, (i % 3 - 1) * 20f);
                Collider c = blade.GetComponent<Collider>();
                if (c != null) Destroy(c);
                ApplyColorRaw(blade, (i % 2 == 0) ? g1 : g2);
            }
        }

        private void AnimateShinySparkle()
        {
            shinySparkleTimer += Time.deltaTime;
            if (cachedShinySparkle == null)
            {
                Transform existing = transform.Find("ShinySparkle");
                if (existing != null)
                {
                    cachedShinySparkle = existing;
                }
                else
                {
                    GameObject sparkleObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sparkleObj.name = "ShinySparkle";
                    sparkleObj.transform.SetParent(transform, false);
                    sparkleObj.transform.localScale = Vector3.one * 0.15f;
                    Collider sc = sparkleObj.GetComponent<Collider>();
                    if (sc != null) Destroy(sc);
                    ApplyColorRaw(sparkleObj, new Color(1f, 1f, 0.6f, 0.8f));
                    cachedShinySparkle = sparkleObj.transform;
                }
            }
            Transform sparkle = cachedShinySparkle;

            // 반짝임 원형 이동 + 크기 맥동
            float angle = shinySparkleTimer * 3f;
            float radius = 0.6f;
            float sparkY = 0.3f + Mathf.Sin(shinySparkleTimer * 2f) * 0.4f;
            sparkle.localPosition = new Vector3(Mathf.Cos(angle) * radius, sparkY, Mathf.Sin(angle) * radius);
            float pulse = 0.1f + Mathf.Abs(Mathf.Sin(shinySparkleTimer * 5f)) * 0.12f;
            sparkle.localScale = Vector3.one * pulse;
        }

        private void AnimateWings()
        {
            if (!wingsResolved) ResolveWings();
            if (cachedWingL == null || cachedWingR == null) return;

            float angle = Mathf.Sin(Time.time * wingSpeed + wingPhase) * wingAmplitude;
            cachedWingL.localRotation = Quaternion.Euler(0f, 0f, angle);
            cachedWingR.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        /// <summary>
        /// 날개 노드와 종별 날갯짓 파라미터를 빌드당 <b>한 번</b> 정한다. 모델은
        /// <c>Initialize</c>/<c>BuildForBattle</c>이 동기로 다 지은 뒤에야 첫 Update가 돌므로
        /// 여기서 못 찾았다면 이 개체엔 날개가 없는 것이다 — 그래서 실패해도 다시 찾지 않는다
        /// (`wingsResolved`를 맨 먼저 세운다). 풀 재사용 시엔 두 진입점이 함께 리셋한다.
        /// </summary>
        private void ResolveWings()
        {
            wingsResolved = true;
            cachedWingL = transform.Find("WingL");
            cachedWingR = transform.Find("WingR");
            if (cachedWingL == null || cachedWingR == null) return;

            string id = data != null ? data.insectId ?? "" : "";
            // 날갯짓 강화(빠르고 크게) — 정적이던 필드 곤충에 생동감.
            wingSpeed = 9f;
            wingAmplitude = 34f;
            if (id.Contains("butterfly") || id.Contains("moth") || id.Contains("luna") || id.Contains("atlas"))
            { wingSpeed = 5f; wingAmplitude = 48f; }
            else if (id.Contains("damselfly"))
            { wingSpeed = 7f; wingAmplitude = 42f; }
            else if (id.Contains("bee") || id.Contains("dragonfly"))
            { wingSpeed = 16f; wingAmplitude = 30f; }
            else if (id.Contains("wasp") || id.Contains("hornet"))
            { wingSpeed = 18f; wingAmplitude = 27f; }
            else if (id.Contains("mosquito") || id.Contains("fly"))
            { wingSpeed = 22f; wingAmplitude = 24f; }
        }

        private void BuildModel()
        {
            if (data == null) { BuildGenericBeetle(GetRarityColor(), Color.gray); return; }
            string id = data.insectId ?? "";
            Color col = GetInsectColor();
            // 음영색: 단순 절반(칙칙·무채색화)이 아니라 HSV로 채도 살짝 올리고 명도만 낮춰 색감 유지.
            Color.RGBToHSV(col, out float dh, out float ds, out float dv);
            Color dark = Color.HSVToRGB(dh, Mathf.Min(1f, ds * 1.1f), dv * 0.6f);

            // 순서 중요: 구체적인 ID를 먼저 체크 (antlion→ant 오매칭 방지 등)
            if (id.Contains("antlion"))
                BuildAntlion(col, dark);
            else if (id.Contains("aphid"))
                BuildAphid(col, dark);
            // 나비: alexandras(비단제비나비)는 진짜 나비라 포함. luna/atlas는 "moth" 포함이라
            // 아래 moth 분기로 자연 라우팅(나방인데 나비로 렌더되던 종 불일치 해소).
            else if (id.Contains("butterfly") || id.Contains("alexandras"))
                BuildButterfly(col, dark);
            else if (id.Contains("moth") || id.Contains("luna") || id.Contains("atlas"))
                BuildMoth(col, dark);
            else if (id.Contains("orchid"))
                BuildOrchidMantis(col, dark);
            else if (id.Contains("ghost"))
                BuildGhostMantis(col, dark);
            else if (id.Contains("mantis"))
                BuildMantis(col, dark);
            else if (id.Contains("damselfly"))
                BuildDamselfly(col, dark);
            // "ancient"를 dragonfly 별칭으로 두면 scarab_ancient(풍뎅이)가 잠자리로 오라우팅됨.
            // dragonfly_ancient는 이미 "dragonfly" 포함이라 별칭 불필요 → 제거.
            else if (id.Contains("dragonfly"))
                BuildDragonfly(col, dark);
            else if (id.Contains("firefly"))
                BuildFirefly(col, dark);
            // "bee"는 "beetle"의 부분문자열 → 가드 없으면 전 딱정벌레가 벌로 렌더됨(stag/rhinoceros 등).
            else if (id.Contains("bee") && !id.Contains("beetle"))
                BuildBee(col, dark);
            else if (id.Contains("hornet") || id.Contains("wasp"))
                BuildWasp(col, dark);
            else if (id.Contains("rhinoceros") || id.Contains("hercules"))
                BuildRhinocerosBeetle(col, dark);
            else if (id.Contains("stag") || id.Contains("golden_stag"))
                BuildStagBeetle(col, dark);
            else if (id.Contains("cicada"))
                BuildCicada(col, dark);
            else if (id.Contains("cricket") || id.Contains("katydid"))
                BuildCricket(col, dark);
            // "phantom"이 "ant"를 포함 → leaf_insect_phantom(대벌레)이 개미로 오라우팅되던 문제 가드.
            else if (id.Contains("ant") && !id.Contains("phantom"))
                BuildAnt(col, dark);
            else if (id.Contains("water_strider") || id.Contains("strider"))
                BuildWaterStrider(col, dark);
            else if (id.Contains("diving"))
                BuildDivingBeetle(col, dark);
            // diamond/celestial 가챠 딱정벌레는 보석곤충(무지갯빛 외골격)으로 — GenericBeetle 평범함 대신 프리미엄 외형.
            else if (id.Contains("scarab") || id.Contains("jewel") || id.Contains("diamond") || id.Contains("celestial"))
                BuildJewelBeetle(col, dark);
            else if (id.Contains("ladybug"))
                BuildLadybug(col, dark);
            else if (id.Contains("grasshopper"))
                BuildGrasshopper(col, dark);
            else if (id.Contains("spider"))
                BuildSpider(col, dark);
            else if (id.Contains("stick_insect") || id.Contains("leaf_insect"))
                BuildStickInsect(col, dark);
            else if (id.Contains("centipede"))
                BuildCentipede(col, dark);
            else if (id.Contains("pill_bug"))
                BuildPillBug(col, dark);
            else if (id.Contains("earwig"))
                BuildEarwig(col, dark);
            else if (id.Contains("longhorn"))
                BuildLonghornBeetle(col, dark);
            else if (id.Contains("caterpillar"))
                BuildCaterpillar(col, dark);
            else if (id.Contains("mosquito") || id.Contains("fly"))
                BuildFly(col, dark);
            else if (id.Contains("dung"))
                BuildDungBeetle(col, dark);
            else if (id.Contains("click"))
                BuildClickBeetle(col, dark);
            else
                BuildGenericBeetle(col, dark);

            BindWingSurfaces();
        }

        private void BuildGenericBeetle(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.72f, 0.46f, 0.86f), body);
            MakeTopGloss(Vector3.zero, new Vector3(0.72f, 0.46f, 0.86f));
            MakePart("ShellL", PrimitiveType.Sphere, new Vector3(-0.13f, 0.18f, -0.05f), new Vector3(0.32f, 0.18f, 0.75f), dark);
            MakePart("ShellR", PrimitiveType.Sphere, new Vector3(0.13f, 0.18f, -0.05f), new Vector3(0.32f, 0.18f, 0.75f), dark);
            MakePart("ShellLine", PrimitiveType.Cylinder, new Vector3(0f, 0.22f, -0.05f), new Vector3(0.02f, 0.01f, 0.7f), body);
            // 가슴마디(prothorax) — 머리·몸 연결 자연화(옛엔 머리가 몸에 바로 붙어 뭉툭)
            MakePart("Prothorax", PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0.32f), new Vector3(0.5f, 0.34f, 0.32f), dark);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.56f), new Vector3(0.46f, 0.42f, 0.42f), dark);
            MakeEyes(0.68f, 0.13f);
            MakeLegs(dark, 3, 0f);
            MakeAntennae(dark, 0.45f);
        }

        private void BuildHornBeetle(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.8f, 0.5f, 1.0f), body);
            MakePart("Shell", PrimitiveType.Sphere, new Vector3(0f, 0.15f, -0.1f), new Vector3(0.75f, 0.35f, 0.85f), dark);
            MakePart("ShellLineL", PrimitiveType.Cylinder, new Vector3(-0.15f, 0.25f, -0.1f), new Vector3(0.02f, 0.01f, 0.7f), body);
            MakePart("ShellLineR", PrimitiveType.Cylinder, new Vector3(0.15f, 0.25f, -0.1f), new Vector3(0.02f, 0.01f, 0.7f), body);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.1f, 0.55f), new Vector3(0.5f, 0.4f, 0.45f), dark);
            MakePart("HornBase", PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0.6f), new Vector3(0.1f, 0.2f, 0.1f), body,
                Quaternion.Euler(20f, 0f, 0f));
            MakePart("HornMid", PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0.75f), new Vector3(0.07f, 0.2f, 0.07f), body,
                Quaternion.Euler(35f, 0f, 0f));
            MakePart("HornTip", PrimitiveType.Sphere, new Vector3(0f, 0.65f, 0.9f), Vector3.one * 0.1f, body);
            MakePart("ClawL", PrimitiveType.Cube, new Vector3(-0.28f, -0.22f, 0.25f), new Vector3(0.05f, 0.08f, 0.1f), dark);
            MakePart("ClawR", PrimitiveType.Cube, new Vector3(0.28f, -0.22f, 0.25f), new Vector3(0.05f, 0.08f, 0.1f), dark);
            MakeEyes(0.7f, 0.14f, 0.2f);
            MakeLegs(dark, 3, 0f);
        }

        // 사슴벌레: 시그니처는 코뿔소 뿔이 아니라 앞으로 뻗은 큰 집게턱(mandible).
        // 좌우 한 쌍이 바깥으로 벌어졌다 끝이 안으로 굽는 사슴뿔 실루엣.
        private void BuildStagBeetle(Color body, Color dark)
        {
            Color jaw = new Color(dark.r * 0.85f + 0.04f, dark.g * 0.72f + 0.03f, dark.b * 0.6f + 0.03f);
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.78f, 0.46f, 1.0f), body);
            MakeTopGloss(Vector3.zero, new Vector3(0.78f, 0.46f, 1.0f), 0.12f);
            MakeSculpture("ShellL", InsectSculptureMeshes.Shape.ElytronLeft, body);
            MakeSculpture("ShellR", InsectSculptureMeshes.Shape.ElytronRight, body);
            // 각진 전흉(pronotum) — 사슴벌레 특유의 넓적한 가슴판
            MakePart("Pronotum", PrimitiveType.Sphere, new Vector3(0f, 0.14f, 0.42f), new Vector3(0.62f, 0.3f, 0.4f), dark);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.1f, 0.68f), new Vector3(0.42f, 0.32f, 0.4f), dark);
            MakeSculpture("MandBaseL", InsectSculptureMeshes.Shape.StagJawLeft, jaw);
            MakeSculpture("MandBaseR", InsectSculptureMeshes.Shape.StagJawRight, jaw);
            foreach (int sign in new[] { -1, 1 })
                MakeSegment("MandTooth" + (sign < 0 ? "L" : "R"), new Vector3(sign * .29f, .13f, 1.03f),
                    new Vector3(sign * .16f, .13f, 1.08f), .042f, jaw);
            // The short front tarsal claws begin at the front feet. The old
            // cubes floated beneath the face in the gallery's front view.
            MakeSegment("ClawL", new Vector3(-.46f, -.39f, .344f), new Vector3(-.48f, -.42f, .40f), .018f, dark);
            MakeSegment("ClawR", new Vector3(.46f, -.39f, .344f), new Vector3(.48f, -.42f, .40f), .018f, dark);
            MakeEyes(0.78f, 0.11f, 0.18f);
            MakeLegs(dark, 3, 0f);
        }

        private void BuildButterfly(Color body, Color dark)
        {
            string id = data != null ? data.insectId ?? "" : "";
            MakePart("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.18f, 0.34f, 0.18f), dark,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, .025f, .19f), new Vector3(.20f, .21f, .26f), dark);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.45f), new Vector3(0.3f, 0.3f, 0.28f), dark);
            float wingAlpha = id.Contains("glasswing") || id.Contains("snowveil") ? .38f : .90f;
            Color wingCol = new Color(body.r, body.g, body.b, wingAlpha);
            Color wingSpot = new Color(Mathf.Min(1, body.r + 0.3f), Mathf.Min(1, body.g + 0.3f), body.b * 0.5f);
            Color wingEdge = new Color(dark.r, dark.g, dark.b, 0.7f);
            MakeSculpture("WingL", InsectSculptureMeshes.Shape.ButterflyForewing, wingCol,
                new Vector3(-.49f, .10f, .04f), new Vector3(.90f, 1f, .72f));
            MakeSculpture("WingR", InsectSculptureMeshes.Shape.ButterflyForewing, wingCol,
                new Vector3(.49f, .10f, .04f), new Vector3(.90f, 1f, .72f));
            MakePart("SpotL1", PrimitiveType.Sphere, new Vector3(-0.45f, 0.12f, 0.1f), new Vector3(0.15f, 0.02f, 0.15f), wingSpot);
            MakePart("SpotR1", PrimitiveType.Sphere, new Vector3(0.45f, 0.12f, 0.1f), new Vector3(0.15f, 0.02f, 0.15f), wingSpot);
            MakePart("SpotL2", PrimitiveType.Sphere, new Vector3(-0.55f, 0.12f, 0f), new Vector3(0.12f, 0.02f, 0.12f), wingSpot);
            MakePart("SpotR2", PrimitiveType.Sphere, new Vector3(0.55f, 0.12f, 0f), new Vector3(0.12f, 0.02f, 0.12f), wingSpot);
            MakePart("SpotL3", PrimitiveType.Sphere, new Vector3(-0.4f, 0.12f, -0.1f), new Vector3(0.1f, 0.02f, 0.1f), wingSpot);
            MakePart("SpotR3", PrimitiveType.Sphere, new Vector3(0.4f, 0.12f, -0.1f), new Vector3(0.1f, 0.02f, 0.1f), wingSpot);
            MakePart("WingTipL", PrimitiveType.Sphere, new Vector3(-0.88f, 0.1f, 0.05f), new Vector3(0.18f, 0.025f, 0.24f), wingEdge);
            MakePart("WingTipR", PrimitiveType.Sphere, new Vector3(0.88f, 0.1f, 0.05f), new Vector3(0.18f, 0.025f, 0.24f), wingEdge);
            MakeSculpture("WingLB", InsectSculptureMeshes.Shape.ButterflyHindwing, wingCol,
                new Vector3(-.37f, .08f, -.18f), new Vector3(.65f, 1f, .55f));
            MakeSculpture("WingRB", InsectSculptureMeshes.Shape.ButterflyHindwing, wingCol,
                new Vector3(.37f, .08f, -.18f), new Vector3(.65f, 1f, .55f));
            MakeFlightLegs(dark);
            if (id.Contains("swallowtail") || id.Contains("birdwing"))
            {
                MakeSegment("HindTailL", new Vector3(-.58f, .07f, -.38f), new Vector3(-.68f, .05f, -.66f), .018f, wingEdge);
                MakeSegment("HindTailR", new Vector3(.58f, .07f, -.38f), new Vector3(.68f, .05f, -.66f), .018f, wingEdge);
            }
            MakeAntennae(dark, 0.35f);
            MakePart("AntBallL", PrimitiveType.Sphere, new Vector3(-0.15f, 0.42f, 0.57f), Vector3.one * 0.06f, dark);
            MakePart("AntBallR", PrimitiveType.Sphere, new Vector3(0.15f, 0.42f, 0.57f), Vector3.one * 0.06f, dark);
            MakeEyes(0.45f, 0.18f);
        }

        private void BuildMoth(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.2f, 0.35f, 0.2f), body,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Fur", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.15f), new Vector3(0.35f, 0.3f, 0.3f), body);
            MakePart("FurFluff", PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0.2f), new Vector3(0.28f, 0.22f, 0.25f),
                new Color(body.r * 1.1f, body.g * 1.1f, body.b * 1.0f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.4f), new Vector3(0.3f, 0.28f, 0.28f), dark);
            Color wingCol = new Color(body.r * 0.8f, body.g * 0.7f, body.b * 0.6f);
            Color eyeSpotCol = new Color(Mathf.Min(1, body.r + 0.1f), body.g * 0.4f, body.b * 0.3f);
            MakeSculpture("WingL", InsectSculptureMeshes.Shape.ButterflyForewing, wingCol,
                new Vector3(-.53f, .06f, .06f), new Vector3(.98f, 1f, .65f));
            MakeSculpture("WingR", InsectSculptureMeshes.Shape.ButterflyForewing, wingCol,
                new Vector3(.53f, .06f, .06f), new Vector3(.98f, 1f, .65f));
            MakeSculpture("WingLB", InsectSculptureMeshes.Shape.ButterflyHindwing, wingCol,
                new Vector3(-.38f, .04f, -.19f), new Vector3(.69f, 1f, .51f));
            MakeSculpture("WingRB", InsectSculptureMeshes.Shape.ButterflyHindwing, wingCol,
                new Vector3(.38f, .04f, -.19f), new Vector3(.69f, 1f, .51f));
            MakeFlightLegs(dark);
            MakePart("EyeSpotL", PrimitiveType.Sphere, new Vector3(-0.5f, 0.07f, 0.05f), new Vector3(0.18f, 0.02f, 0.18f), eyeSpotCol);
            MakePart("EyeSpotR", PrimitiveType.Sphere, new Vector3(0.5f, 0.07f, 0.05f), new Vector3(0.18f, 0.02f, 0.18f), eyeSpotCol);
            MakePart("EyeSpotCoreL", PrimitiveType.Sphere, new Vector3(-0.5f, 0.08f, 0.05f), new Vector3(0.08f, 0.02f, 0.08f), Color.black);
            MakePart("EyeSpotCoreR", PrimitiveType.Sphere, new Vector3(0.5f, 0.08f, 0.05f), new Vector3(0.08f, 0.02f, 0.08f), Color.black);
            MakeAntennae(dark, 0.4f, true);
            MakePart("FeatherL", PrimitiveType.Cube, new Vector3(-0.18f, 0.35f, 0.6f), new Vector3(0.1f, 0.02f, 0.06f), dark);
            MakePart("FeatherR", PrimitiveType.Cube, new Vector3(0.18f, 0.35f, 0.6f), new Vector3(0.1f, 0.02f, 0.06f), dark);
            MakeEyes(0.4f, 0.15f);

            string mid = data != null ? data.insectId ?? "" : "";
            if (mid.Contains("luna"))
            {
                // 루나나방 시그니처: 뒷날개에서 길게 뻗은 꼬리(스트리머)
                Color tailCol = new Color(body.r * 0.85f, Mathf.Min(1f, body.g * 1.0f), body.b * 0.7f, 0.9f);
                MakePart("HindTailL", PrimitiveType.Cube, new Vector3(-0.34f, 0.02f, -0.42f), new Vector3(0.13f, 0.02f, 0.5f),
                    tailCol, Quaternion.Euler(0f, 16f, 0f));
                MakePart("HindTailR", PrimitiveType.Cube, new Vector3(0.34f, 0.02f, -0.42f), new Vector3(0.13f, 0.02f, 0.5f),
                    tailCol, Quaternion.Euler(0f, -16f, 0f));
                MakePart("TailCurlL", PrimitiveType.Sphere, new Vector3(-0.4f, 0.02f, -0.68f), new Vector3(0.1f, 0.02f, 0.16f), tailCol);
                MakePart("TailCurlR", PrimitiveType.Sphere, new Vector3(0.4f, 0.02f, -0.68f), new Vector3(0.1f, 0.02f, 0.16f), tailCol);
            }
            else if (mid.Contains("atlas"))
            {
                // 아틀라스나방(세계 최대 나방) 시그니처: 앞날개 끝 뱀머리형 갈고리 + 투명창 무늬
                Color hookCol = new Color(Mathf.Min(1f, body.r + 0.18f), body.g * 0.78f, body.b * 0.55f);
                MakePart("WingHookL", PrimitiveType.Sphere, new Vector3(-0.82f, 0.06f, 0.3f), new Vector3(0.2f, 0.025f, 0.16f), hookCol);
                MakePart("WingHookR", PrimitiveType.Sphere, new Vector3(0.82f, 0.06f, 0.3f), new Vector3(0.2f, 0.025f, 0.16f), hookCol);
                MakePart("WingWindowL", PrimitiveType.Sphere, new Vector3(-0.55f, 0.07f, 0.12f), new Vector3(0.14f, 0.02f, 0.16f),
                    new Color(0.95f, 0.92f, 0.85f, 0.55f));
                MakePart("WingWindowR", PrimitiveType.Sphere, new Vector3(0.55f, 0.07f, 0.12f), new Vector3(0.14f, 0.02f, 0.16f),
                    new Color(0.95f, 0.92f, 0.85f, 0.55f));
            }
        }

        private void BuildMantis(Color body, Color dark)
        {
            Color leaf = Color.Lerp(body, new Color(.32f, .49f, .16f), .72f);
            Color vein = Color.Lerp(leaf, new Color(.14f, .24f, .07f), .50f);
            MakePart("Body", PrimitiveType.Sphere, new Vector3(0f, -.035f, -.30f), new Vector3(.24f, .15f, .68f), vein);
            MakeSculpture("Thorax", InsectSculptureMeshes.Shape.MantisThorax, leaf);
            MakeSculpture("Head", InsectSculptureMeshes.Shape.MantisHead, leaf);
            foreach (int sign in new[] { -1, 1 })
            {
                string side = sign < 0 ? "L" : "R";
                MakeSculpture("ArmUpper" + side, sign < 0 ? InsectSculptureMeshes.Shape.MantisFemurLeft : InsectSculptureMeshes.Shape.MantisFemurRight, leaf);
                MakeSculpture("Claw" + side, sign < 0 ? InsectSculptureMeshes.Shape.MantisBladeLeft : InsectSculptureMeshes.Shape.MantisBladeRight, vein);
                // Eyes sit on the corners of the triangular head, not below it.
                MakePart("Eye" + side, PrimitiveType.Sphere, new Vector3(sign * .18f, .38f, .33f), new Vector3(.13f, .12f, .12f), new Color(.51f, .64f, .24f));
                MakePart("EyeGlint" + side, PrimitiveType.Sphere, new Vector3(sign * .17f, .405f, .38f), Vector3.one * .023f, new Color(.94f, .96f, .80f));
                MakeSegment("Antenna" + side, new Vector3(sign * .09f, .38f, .28f), new Vector3(sign * .16f, .66f, .37f), .015f, vein);
            }
            // Folded opaque tegmina read as two tapered leaves; no transparent boards.
            MakeSculpture("WingFoldL", InsectSculptureMeshes.Shape.Wing, leaf,
                new Vector3(-.062f, .035f, -.25f), new Vector3(.55f, .040f, .18f), Quaternion.Euler(0f, 90f, 0f));
            MakeSculpture("WingFoldR", InsectSculptureMeshes.Shape.Wing, leaf,
                new Vector3(.062f, .035f, -.25f), new Vector3(.55f, .040f, .18f), Quaternion.Euler(0f, 90f, 0f));
            MakeLegs(vein, 2, -.15f);
        }

        private void BuildDragonfly(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Capsule, new Vector3(0f, 0f, -0.3f), new Vector3(0.12f, 0.6f, 0.12f), body,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("TailSeg1", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.65f), new Vector3(0.1f, 0.1f, 0.12f), body);
            MakePart("TailSeg2", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.78f), new Vector3(0.09f, 0.09f, 0.1f), dark);
            MakePart("TailSeg3", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.9f), new Vector3(0.08f, 0.08f, 0.09f), body);
            MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.2f), new Vector3(0.2f, 0.18f, 0.2f), dark);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.35f), new Vector3(0.35f, 0.25f, 0.3f), dark);
            Color wingCol = new Color(0.8f, 0.9f, 1f, 0.4f);
            Color veinCol = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            MakeSculpture("WingL", InsectSculptureMeshes.Shape.DragonflyWing, wingCol,
                new Vector3(-.43f, .12f, .25f), new Vector3(.76f, 1f, .85f));
            MakeSculpture("WingR", InsectSculptureMeshes.Shape.DragonflyWing, wingCol,
                new Vector3(.43f, .12f, .25f), new Vector3(.76f, 1f, .85f));
            MakeSegment("VeinFL", new Vector3(-.09f, .134f, .25f), new Vector3(-.77f, .134f, .25f), .008f, veinCol);
            MakeSegment("VeinFR", new Vector3(.09f, .134f, .25f), new Vector3(.77f, .134f, .25f), .008f, veinCol);
            MakeSculpture("WingLB", InsectSculptureMeshes.Shape.DragonflyWing, wingCol,
                new Vector3(-.40f, .10f, .08f), new Vector3(.72f, 1f, .92f));
            MakeSculpture("WingRB", InsectSculptureMeshes.Shape.DragonflyWing, wingCol,
                new Vector3(.40f, .10f, .08f), new Vector3(.72f, 1f, .92f));
            MakeSegment("VeinBL", new Vector3(-.09f, .114f, .08f), new Vector3(-.73f, .114f, .08f), .008f, veinCol);
            MakeSegment("VeinBR", new Vector3(.09f, .114f, .08f), new Vector3(.73f, .114f, .08f), .008f, veinCol);
            MakePart("EyeL", PrimitiveType.Sphere, new Vector3(-0.15f, 0.15f, 0.4f), Vector3.one * 0.16f, new Color(0.2f, 0.8f, 0.3f));
            MakePart("EyeR", PrimitiveType.Sphere, new Vector3(0.15f, 0.15f, 0.4f), Vector3.one * 0.16f, new Color(0.2f, 0.8f, 0.3f));
            MakeLegs(dark, 3, 0.18f);
        }

        private void BuildBee(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.55f, 0.45f, 0.7f), body);
            MakeTopGloss(Vector3.zero, new Vector3(0.55f, 0.45f, 0.7f), 0.1f);
            MakePart("Stripe1", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.15f), new Vector3(0.54f, 0.02f, 0.54f),
                Color.black, Quaternion.Euler(90f, 0f, 0f));
            MakePart("Stripe2", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(0.56f, 0.02f, 0.56f),
                Color.black, Quaternion.Euler(90f, 0f, 0f));
            MakePart("Stripe3", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.15f), new Vector3(0.52f, 0.02f, 0.52f),
                Color.black, Quaternion.Euler(90f, 0f, 0f));
            MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.22f), new Vector3(0.38f, 0.32f, 0.3f),
                new Color(body.r * 0.9f, body.g * 0.8f, body.b * 0.3f));
            MakePart("ThoraxFuzz", PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0.22f), new Vector3(0.32f, 0.25f, 0.25f),
                new Color(body.r, body.g * 0.9f, body.b * 0.4f, 0.7f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.4f), new Vector3(0.35f, 0.32f, 0.3f), dark);
            Color wingCol = new Color(1f, 1f, 1f, 0.35f);
            MakeWing("WingL", new Vector3(-0.3f, 0.25f, 0.05f), new Vector3(0.4f, 0.01f, 0.25f), wingCol);
            MakeWing("WingR", new Vector3(0.3f, 0.25f, 0.05f), new Vector3(0.4f, 0.01f, 0.25f), wingCol);
            MakePart("PollenL", PrimitiveType.Sphere, new Vector3(-0.22f, -0.18f, -0.05f), Vector3.one * 0.08f,
                new Color(1f, 0.85f, 0.2f));
            MakePart("PollenR", PrimitiveType.Sphere, new Vector3(0.22f, -0.18f, -0.05f), Vector3.one * 0.08f,
                new Color(1f, 0.85f, 0.2f));
            MakePart("Stinger", PrimitiveType.Capsule, new Vector3(0f, -0.05f, -0.45f), new Vector3(0.06f, 0.15f, 0.06f),
                dark, Quaternion.Euler(80f, 0f, 0f));
            MakeEyes(0.4f, 0.14f);
            MakeAntennae(dark, 0.35f);
            MakeLegs(dark, 3, 0f);
        }

        private void BuildFirefly(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.5f, 0.35f, 0.7f), dark);
            MakePart("LightOrgan", PrimitiveType.Sphere, new Vector3(0f, -0.05f, -0.3f), new Vector3(0.4f, 0.3f, 0.35f),
                new Color(0.9f, 1f, 0.3f, 0.9f));
            MakePart("GlowOuter", PrimitiveType.Sphere, new Vector3(0f, -0.05f, -0.3f), new Vector3(0.5f, 0.38f, 0.42f),
                new Color(0.95f, 1f, 0.5f, 0.3f));
            MakePart("GlowPulse", PrimitiveType.Sphere, new Vector3(0f, -0.02f, -0.32f), new Vector3(0.3f, 0.22f, 0.25f),
                new Color(1f, 1f, 0.8f, 0.5f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.4f), new Vector3(0.3f, 0.28f, 0.3f), dark);
            Color wingCol = new Color(body.r, body.g, body.b, 0.3f);
            MakeWing("WingL", new Vector3(-0.3f, 0.2f, 0f), new Vector3(0.35f, 0.01f, 0.3f), wingCol);
            MakeWing("WingR", new Vector3(0.3f, 0.2f, 0f), new Vector3(0.35f, 0.01f, 0.3f), wingCol);
            MakeEyes(0.4f, 0.13f);
            MakeAntennae(dark, 0.3f);
            MakeLegs(dark, 3, 0f);
        }

        private void BuildCicada(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.5f, 0.35f, 0.85f), body);
            MakePart("AbdSeg1", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.15f), new Vector3(0.48f, 0.02f, 0.48f),
                dark, Quaternion.Euler(90f, 0f, 0f));
            MakePart("AbdSeg2", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.28f), new Vector3(0.42f, 0.02f, 0.42f),
                dark, Quaternion.Euler(90f, 0f, 0f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.5f), new Vector3(0.4f, 0.35f, 0.35f), dark);
            Color wingCol = new Color(0.7f, 0.8f, 0.7f, 0.3f);
            Color wingVein = new Color(0.4f, 0.5f, 0.4f, 0.5f);
            MakeWing("WingL", new Vector3(-0.35f, 0.15f, -0.1f), new Vector3(0.5f, 0.01f, 0.55f), wingCol);
            MakeWing("WingR", new Vector3(0.35f, 0.15f, -0.1f), new Vector3(0.5f, 0.01f, 0.55f), wingCol);
            MakePart("WingVeinL", PrimitiveType.Cylinder, new Vector3(-0.35f, 0.16f, -0.1f), new Vector3(0.01f, 0.01f, 0.45f),
                wingVein, Quaternion.Euler(0f, 0f, 80f));
            MakePart("WingVeinR", PrimitiveType.Cylinder, new Vector3(0.35f, 0.16f, -0.1f), new Vector3(0.01f, 0.01f, 0.45f),
                wingVein, Quaternion.Euler(0f, 0f, -80f));
            MakePart("CompEyeL", PrimitiveType.Sphere, new Vector3(-0.18f, 0.15f, 0.5f), Vector3.one * 0.17f,
                new Color(0.6f, 0.2f, 0.2f));
            MakePart("CompEyeR", PrimitiveType.Sphere, new Vector3(0.18f, 0.15f, 0.5f), Vector3.one * 0.17f,
                new Color(0.6f, 0.2f, 0.2f));
            MakeLegs(dark, 3, 0.1f);
        }

        private void BuildCricket(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.3f, 0.4f, 0.3f), body,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.4f), new Vector3(0.3f, 0.28f, 0.28f), dark);
            MakePart("ThighBL", PrimitiveType.Sphere, new Vector3(-0.18f, -0.05f, -0.18f), new Vector3(0.12f, 0.08f, 0.18f), dark);
            MakePart("ThighBR", PrimitiveType.Sphere, new Vector3(0.18f, -0.05f, -0.18f), new Vector3(0.12f, 0.08f, 0.18f), dark);
            MakePart("LegBL", PrimitiveType.Capsule, new Vector3(-0.22f, -0.12f, -0.22f), new Vector3(0.06f, 0.4f, 0.06f),
                dark, Quaternion.Euler(-30f, 0f, 30f));
            MakePart("LegBR", PrimitiveType.Capsule, new Vector3(0.22f, -0.12f, -0.22f), new Vector3(0.06f, 0.4f, 0.06f),
                dark, Quaternion.Euler(-30f, 0f, -30f));
            Color wingFold = new Color(body.r * 0.7f, body.g * 0.7f, body.b * 0.6f, 0.6f);
            MakePart("WingFoldL", PrimitiveType.Cube, new Vector3(-0.06f, 0.1f, -0.1f), new Vector3(0.12f, 0.01f, 0.35f), wingFold);
            MakePart("WingFoldR", PrimitiveType.Cube, new Vector3(0.06f, 0.1f, -0.1f), new Vector3(0.12f, 0.01f, 0.35f), wingFold);
            MakeLegs(dark, 2, 0.1f);
            MakePart("LongAntL", PrimitiveType.Capsule, new Vector3(-0.1f, 0.2f, 0.55f), new Vector3(0.02f, 0.3f, 0.02f),
                dark, Quaternion.Euler(-25f, 0f, 10f));
            MakePart("LongAntR", PrimitiveType.Capsule, new Vector3(0.1f, 0.2f, 0.55f), new Vector3(0.02f, 0.3f, 0.02f),
                dark, Quaternion.Euler(-25f, 0f, -10f));
            MakeEyes(0.4f, 0.13f);
        }

        private void BuildAnt(Color body, Color dark)
        {
            MakePart("Abdomen", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.3f), new Vector3(0.35f, 0.3f, 0.4f), body);
            MakePart("Petiole", PrimitiveType.Capsule, new Vector3(0f, 0f, -0.1f), new Vector3(0.06f, 0.08f, 0.06f), dark,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Thorax", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.22f, 0.2f, 0.25f), dark);
            MakePart("Neck", PrimitiveType.Capsule, new Vector3(0f, 0.03f, 0.18f), new Vector3(0.06f, 0.06f, 0.06f), dark,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.3f), new Vector3(0.3f, 0.28f, 0.28f), dark);
            // Paired hooked mandibles emerge from the head and curve inward.
            // Keep the MandibleL/R node contract used by model inspections.
            foreach (int sign in new[] { -1, 1 })
            {
                string side = sign < 0 ? "L" : "R";
                Vector3 root = new Vector3(sign * .08f, -.025f, .40f);
                Vector3 bend = new Vector3(sign * .15f, -.07f, .51f);
                Vector3 tip = new Vector3(sign * .05f, -.065f, .56f);
                MakeSegment("Mandible" + side, root, bend, .027f, body);
                MakeSegment("MandibleTip" + side, bend, tip, .017f, body);
            }
            MakeLegs(dark, 3, -0.05f);
            foreach (int sign in new[] { -1, 1 })
            {
                string side = sign < 0 ? "L" : "R";
                Vector3 basePoint = new Vector3(sign * 0.09f, 0.13f, 0.38f);
                Vector3 elbow = new Vector3(sign * 0.18f, 0.29f, 0.51f);
                MakeSegment("ElbowAnt" + side, basePoint, elbow, 0.027f, dark);
                MakeSegment("ElbowAnt" + side + "2", elbow,
                    new Vector3(sign * 0.29f, 0.30f, 0.67f), 0.021f, dark);
            }
            MakeEyes(0.3f, 0.1f);
        }

        private void BuildWaterStrider(Color body, Color dark)
        {
            Color sheen = new Color(Mathf.Min(1, body.r + 0.2f), Mathf.Min(1, body.g + 0.2f), Mathf.Min(1, body.b + 0.25f));
            MakePart("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.12f, 0.35f, 0.12f), sheen,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.03f, 0.35f), new Vector3(0.18f, 0.16f, 0.18f), dark);
            MakePart("LegFL", PrimitiveType.Capsule, new Vector3(-0.4f, -0.05f, 0.2f), new Vector3(0.03f, 0.4f, 0.03f),
                dark, Quaternion.Euler(0f, 0f, 70f));
            MakePart("LegFR", PrimitiveType.Capsule, new Vector3(0.4f, -0.05f, 0.2f), new Vector3(0.03f, 0.4f, 0.03f),
                dark, Quaternion.Euler(0f, 0f, -70f));
            MakePart("LegML", PrimitiveType.Capsule, new Vector3(-0.5f, -0.05f, 0f), new Vector3(0.03f, 0.5f, 0.03f),
                dark, Quaternion.Euler(0f, 0f, 80f));
            MakePart("LegMR", PrimitiveType.Capsule, new Vector3(0.5f, -0.05f, 0f), new Vector3(0.03f, 0.5f, 0.03f),
                dark, Quaternion.Euler(0f, 0f, -80f));
            MakePart("LegBL", PrimitiveType.Capsule, new Vector3(-0.4f, -0.05f, -0.2f), new Vector3(0.03f, 0.45f, 0.03f),
                dark, Quaternion.Euler(0f, 0f, 75f));
            MakePart("LegBR", PrimitiveType.Capsule, new Vector3(0.4f, -0.05f, -0.2f), new Vector3(0.03f, 0.45f, 0.03f),
                dark, Quaternion.Euler(0f, 0f, -75f));
            Color ripple = new Color(0.7f, 0.85f, 1f, 0.4f);
            MakePart("RippleFL", PrimitiveType.Sphere, new Vector3(-0.55f, -0.12f, 0.2f), Vector3.one * 0.06f, ripple);
            MakePart("RippleFR", PrimitiveType.Sphere, new Vector3(0.55f, -0.12f, 0.2f), Vector3.one * 0.06f, ripple);
            MakePart("RippleML", PrimitiveType.Sphere, new Vector3(-0.7f, -0.12f, 0f), Vector3.one * 0.06f, ripple);
            MakePart("RippleMR", PrimitiveType.Sphere, new Vector3(0.7f, -0.12f, 0f), Vector3.one * 0.06f, ripple);
            MakePart("RippleBL", PrimitiveType.Sphere, new Vector3(-0.55f, -0.12f, -0.2f), Vector3.one * 0.06f, ripple);
            MakePart("RippleBR", PrimitiveType.Sphere, new Vector3(0.55f, -0.12f, -0.2f), Vector3.one * 0.06f, ripple);
            MakeEyes(0.35f, 0.08f);
        }

        private void BuildDivingBeetle(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.6f, 0.3f, 0.95f), body);
            MakePart("Shell", PrimitiveType.Sphere, new Vector3(0f, 0.1f, 0f), new Vector3(0.55f, 0.15f, 0.85f), dark);
            MakePart("Keel", PrimitiveType.Cube, new Vector3(0f, -0.12f, 0f), new Vector3(0.08f, 0.04f, 0.8f), dark);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.5f), new Vector3(0.38f, 0.25f, 0.32f), dark);
            MakePart("PaddleL", PrimitiveType.Cube, new Vector3(-0.35f, -0.1f, -0.2f), new Vector3(0.22f, 0.03f, 0.18f), dark);
            MakePart("PaddleR", PrimitiveType.Cube, new Vector3(0.35f, -0.1f, -0.2f), new Vector3(0.22f, 0.03f, 0.18f), dark);
            MakePart("PaddleFringeL", PrimitiveType.Cube, new Vector3(-0.45f, -0.1f, -0.2f), new Vector3(0.04f, 0.01f, 0.16f),
                new Color(dark.r, dark.g, dark.b, 0.6f));
            MakePart("PaddleFringeR", PrimitiveType.Cube, new Vector3(0.45f, -0.1f, -0.2f), new Vector3(0.04f, 0.01f, 0.16f),
                new Color(dark.r, dark.g, dark.b, 0.6f));
            Color bubbleCol = new Color(0.8f, 0.9f, 1f, 0.35f);
            MakePart("Bubble", PrimitiveType.Sphere, new Vector3(0f, -0.15f, -0.35f), Vector3.one * 0.15f, bubbleCol);
            MakeLegs(dark, 2, 0.15f);
            MakeEyes(0.5f, 0.12f);
        }

        private void BuildJewelBeetle(Color body, Color dark)
        {
            Color shimmer1 = new Color(Mathf.Min(1, body.r + 0.2f), Mathf.Min(1, body.g + 0.3f), Mathf.Min(1, body.b + 0.2f));
            Color shimmer2 = new Color(body.b * 0.5f, Mathf.Min(1, body.r + 0.3f), Mathf.Min(1, body.g + 0.2f));
            Color shimmer3 = new Color(Mathf.Min(1, body.g + 0.2f), body.r * 0.6f, Mathf.Min(1, body.b + 0.3f));
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.65f, 0.4f, 0.9f), body);
            MakePart("ShellLayer1", PrimitiveType.Sphere, new Vector3(0f, 0.18f, 0.1f), new Vector3(0.58f, 0.12f, 0.35f), shimmer1);
            MakePart("ShellLayer2", PrimitiveType.Sphere, new Vector3(0f, 0.17f, -0.05f), new Vector3(0.56f, 0.1f, 0.3f), shimmer2);
            MakePart("ShellLayer3", PrimitiveType.Sphere, new Vector3(0f, 0.16f, -0.2f), new Vector3(0.52f, 0.1f, 0.3f), shimmer3);
            MakePart("ShellL", PrimitiveType.Sphere, new Vector3(-0.12f, 0.15f, 0f), new Vector3(0.3f, 0.2f, 0.8f), shimmer1);
            MakePart("ShellR", PrimitiveType.Sphere, new Vector3(0.12f, 0.15f, 0f), new Vector3(0.3f, 0.2f, 0.8f), shimmer1);
            MakePart("ShellGloss", PrimitiveType.Sphere, new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.08f, 0.7f),
                new Color(1f, 1f, 1f, 0.15f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.5f), new Vector3(0.35f, 0.3f, 0.3f), dark);
            MakeLegs(dark, 3, 0f);
            MakeAntennae(dark, 0.35f);
            MakeEyes(0.5f, 0.14f);
        }

        private void BuildRhinocerosBeetle(Color body, Color dark)
        {
            Color chitin = Color.Lerp(body, new Color(.24f, .13f, .075f), .72f);
            Color underside = Color.Lerp(chitin, Color.black, .42f);
            MakePart("Body", PrimitiveType.Sphere, new Vector3(0f, -.045f, -.08f), new Vector3(.90f, .36f, .95f), underside);
            MakeSculpture("ShellL", InsectSculptureMeshes.Shape.ElytronLeft, chitin);
            MakeSculpture("ShellR", InsectSculptureMeshes.Shape.ElytronRight, chitin);
            Color forebody = Color.Lerp(chitin, underside, .22f);
            MakeSculpture("Pronotum", InsectSculptureMeshes.Shape.RhinoPronotum, forebody);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, .10f, .61f), new Vector3(.48f, .31f, .38f), forebody);
            MakeSculpture("HornMain", InsectSculptureMeshes.Shape.RhinoHorn, chitin);
            MakeSculpture("HornForkL", InsectSculptureMeshes.Shape.RhinoForkLeft, chitin);
            MakeSculpture("HornForkR", InsectSculptureMeshes.Shape.RhinoForkRight, chitin);
            MakeSculpture("HornSmall", InsectSculptureMeshes.Shape.ThoraxHorn, underside);
            MakeEyes(.73f, .09f, .19f);
            MakeLegs(underside, 3, 0f);
            // Keep the short clubbed antennae ahead of the head. The old tall
            // antenna roots crossed the horn and looked like a dark cut at its base.
            foreach (int sign in new[] { -1, 1 })
            {
                string side = sign < 0 ? "L" : "R";
                Vector3 elbow = new Vector3(sign * .28f, .11f, .79f);
                Vector3 tip = new Vector3(sign * .28f, .18f, .85f);
                MakeSegment("AntBase" + side, new Vector3(sign * .18f, .08f, .68f), elbow, .025f, underside);
                MakeSegment("AntMid" + side, elbow, tip, .02f, underside);
                MakePart("AntTip" + side, PrimitiveType.Sphere, tip, new Vector3(.055f, .03f, .045f), underside);
            }
        }

        private void BuildOrchidMantis(Color body, Color dark)
        {
            Color petal = new Color(1f, 0.75f, 0.8f);
            Color petalLight = new Color(1f, 0.9f, 0.92f);
            MakePart("Body", PrimitiveType.Capsule, new Vector3(0f, 0f, -0.12f), new Vector3(0.18f, 0.45f, 0.18f), petalLight,
                Quaternion.Euler(80f, 0f, 0f));
            MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.15f, 0.15f), new Vector3(0.25f, 0.2f, 0.22f), petal);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.3f, 0.25f), new Vector3(0.32f, 0.26f, 0.24f), petalLight);
            MakePart("HeadCrest", PrimitiveType.Cube, new Vector3(0f, 0.38f, 0.23f), new Vector3(0.1f, 0.06f, 0.1f), petal);
            MakePart("ArmL", PrimitiveType.Capsule, new Vector3(-0.22f, 0.15f, 0.3f), new Vector3(0.07f, 0.25f, 0.07f),
                petal, Quaternion.Euler(-15f, 0f, 18f));
            MakePart("ArmR", PrimitiveType.Capsule, new Vector3(0.22f, 0.15f, 0.3f), new Vector3(0.07f, 0.25f, 0.07f),
                petal, Quaternion.Euler(-15f, 0f, -18f));
            MakePart("ClawL", PrimitiveType.Cube, new Vector3(-0.28f, 0.35f, 0.48f), new Vector3(0.05f, 0.16f, 0.04f), dark);
            MakePart("ClawR", PrimitiveType.Cube, new Vector3(0.28f, 0.35f, 0.48f), new Vector3(0.05f, 0.16f, 0.04f), dark);
            MakePart("PetalLegFL", PrimitiveType.Sphere, new Vector3(-0.2f, -0.1f, 0.1f), new Vector3(0.15f, 0.04f, 0.12f), petal);
            MakePart("PetalLegFR", PrimitiveType.Sphere, new Vector3(0.2f, -0.1f, 0.1f), new Vector3(0.15f, 0.04f, 0.12f), petal);
            MakePart("PetalLegML", PrimitiveType.Sphere, new Vector3(-0.22f, -0.12f, -0.05f), new Vector3(0.16f, 0.04f, 0.13f), petalLight);
            MakePart("PetalLegMR", PrimitiveType.Sphere, new Vector3(0.22f, -0.12f, -0.05f), new Vector3(0.16f, 0.04f, 0.13f), petalLight);
            MakeLegs(petal, 2, -0.1f);
            MakeAntennae(petal, 0.3f);
            MakeEyes(0.3f, 0.2f);
        }

        private void BuildLadybug(Color body, Color dark)
        {
            // 금빛 무당벌레(가챠)는 황금 외피, 일반은 빨강. 검은 7점은 공통(칠성무당벌레 시그니처).
            string id = data != null ? data.insectId ?? "" : "";
            Color shell = id.Contains("golden") ? new Color(0.95f, 0.78f, 0.15f) : new Color(0.9f, 0.15f, 0.1f);
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.65f, 0.5f, 0.7f), shell);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.4f), new Vector3(0.28f, 0.25f, 0.25f), dark);
            MakePart("ShellLine", PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0f), new Vector3(0.02f, 0.01f, 0.6f), Color.black);
            MakePart("Spot1", PrimitiveType.Sphere, new Vector3(-0.15f, 0.26f, 0.1f), Vector3.one * 0.09f, Color.black);
            MakePart("Spot2", PrimitiveType.Sphere, new Vector3(0.15f, 0.26f, 0.1f), Vector3.one * 0.09f, Color.black);
            MakePart("Spot3", PrimitiveType.Sphere, new Vector3(-0.1f, 0.27f, -0.1f), Vector3.one * 0.1f, Color.black);
            MakePart("Spot4", PrimitiveType.Sphere, new Vector3(0.1f, 0.27f, -0.1f), Vector3.one * 0.1f, Color.black);
            MakePart("Spot5", PrimitiveType.Sphere, new Vector3(-0.18f, 0.24f, -0.2f), Vector3.one * 0.08f, Color.black);
            MakePart("Spot6", PrimitiveType.Sphere, new Vector3(0.18f, 0.24f, -0.2f), Vector3.one * 0.08f, Color.black);
            MakePart("Spot7", PrimitiveType.Sphere, new Vector3(0f, 0.27f, 0f), Vector3.one * 0.08f, Color.black);
            MakeLegs(dark, 3, 0.05f);
            MakeEyes(0.4f, 0.1f);
            MakeAntennae(dark, 0.3f);
        }

        private void BuildGrasshopper(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.25f, 0.5f, 0.25f), body,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Abdomen", PrimitiveType.Sphere, new Vector3(0f, -0.02f, -0.3f), new Vector3(0.22f, 0.2f, 0.25f), body);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.45f), new Vector3(0.3f, 0.28f, 0.28f), dark);
            MakePart("Jaw", PrimitiveType.Cube, new Vector3(0f, -0.02f, 0.58f), new Vector3(0.12f, 0.06f, 0.08f), dark);
            // 뒷다리: 허벅지를 위로 솟게 하고 무릎 관절 추가
            MakePart("ThighBL", PrimitiveType.Sphere, new Vector3(-0.18f, 0.12f, -0.18f), new Vector3(0.16f, 0.12f, 0.25f), dark);
            MakePart("ThighBR", PrimitiveType.Sphere, new Vector3(0.18f, 0.12f, -0.18f), new Vector3(0.16f, 0.12f, 0.25f), dark);
            MakePart("KneeBL", PrimitiveType.Sphere, new Vector3(-0.26f, 0.18f, -0.32f), Vector3.one * 0.07f, dark);
            MakePart("KneeBR", PrimitiveType.Sphere, new Vector3(0.26f, 0.18f, -0.32f), Vector3.one * 0.07f, dark);
            MakePart("ShinBL", PrimitiveType.Capsule, new Vector3(-0.3f, -0.08f, -0.42f), new Vector3(0.04f, 0.5f, 0.04f),
                dark, Quaternion.Euler(-50f, 0f, 15f));
            MakePart("ShinBR", PrimitiveType.Capsule, new Vector3(0.3f, -0.08f, -0.42f), new Vector3(0.04f, 0.5f, 0.04f),
                dark, Quaternion.Euler(-50f, 0f, -15f));
            MakePart("FootBL", PrimitiveType.Sphere, new Vector3(-0.32f, -0.35f, -0.55f), Vector3.one * 0.04f, dark);
            MakePart("FootBR", PrimitiveType.Sphere, new Vector3(0.32f, -0.35f, -0.55f), Vector3.one * 0.04f, dark);
            MakeLegs(dark, 2, 0.15f);
            Color wingFold = new Color(body.r * 0.7f, body.g * 0.8f, body.b * 0.6f, 0.5f);
            MakePart("WingFoldL", PrimitiveType.Cube, new Vector3(-0.06f, 0.1f, -0.1f), new Vector3(0.12f, 0.01f, 0.4f), wingFold);
            MakePart("WingFoldR", PrimitiveType.Cube, new Vector3(0.06f, 0.1f, -0.1f), new Vector3(0.12f, 0.01f, 0.4f), wingFold);
            MakeEyes(0.45f, 0.16f);
            MakeAntennae(dark, 0.35f);
        }

        private void BuildWasp(Color body, Color dark)
        {
            Color yellow = new Color(1f, 0.85f, 0.1f);
            MakePart("Abdomen", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.25f), new Vector3(0.4f, 0.35f, 0.55f), yellow);
            MakePart("AbdStripe1", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.15f), new Vector3(0.39f, 0.02f, 0.39f),
                Color.black, Quaternion.Euler(90f, 0f, 0f));
            MakePart("AbdStripe2", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.3f), new Vector3(0.36f, 0.02f, 0.36f),
                Color.black, Quaternion.Euler(90f, 0f, 0f));
            MakePart("AbdStripe3", PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.42f), new Vector3(0.3f, 0.02f, 0.3f),
                Color.black, Quaternion.Euler(90f, 0f, 0f));
            MakePart("Waist", PrimitiveType.Capsule, new Vector3(0f, 0f, 0.02f), new Vector3(0.06f, 0.1f, 0.06f), Color.black,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.18f), new Vector3(0.3f, 0.28f, 0.28f), Color.black);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.4f), new Vector3(0.3f, 0.28f, 0.28f), dark);
            MakePart("Stinger", PrimitiveType.Capsule, new Vector3(0f, -0.05f, -0.55f), new Vector3(0.05f, 0.2f, 0.05f),
                dark, Quaternion.Euler(85f, 0f, 0f));
            Color wingCol = new Color(1f, 1f, 1f, 0.3f);
            MakeWing("WingL", new Vector3(-0.28f, 0.22f, 0.05f), new Vector3(0.38f, 0.01f, 0.22f), wingCol);
            MakeWing("WingR", new Vector3(0.28f, 0.22f, 0.05f), new Vector3(0.38f, 0.01f, 0.22f), wingCol);
            MakeLegs(dark, 3, 0.05f);
            MakeEyes(0.4f, 0.13f);
            MakeAntennae(dark, 0.3f);
        }

        private void BuildSpider(Color body, Color dark)
        {
            MakePart("Abdomen", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.25f), new Vector3(0.55f, 0.5f, 0.6f), body);
            MakePart("Cephalothorax", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.15f), new Vector3(0.35f, 0.3f, 0.35f), dark);
            MakePart("FangL", PrimitiveType.Capsule, new Vector3(-0.06f, -0.05f, 0.35f), new Vector3(0.04f, 0.1f, 0.04f),
                dark, Quaternion.Euler(20f, 0f, 5f));
            MakePart("FangR", PrimitiveType.Capsule, new Vector3(0.06f, -0.05f, 0.35f), new Vector3(0.04f, 0.1f, 0.04f),
                dark, Quaternion.Euler(20f, 0f, -5f));
            float[] angles = { 30f, 55f, 110f, 140f };
            for (int i = 0; i < 4; i++)
            {
                float rad = angles[i] * Mathf.Deg2Rad;
                float x = Mathf.Cos(rad) * 0.45f;
                float z = Mathf.Sin(rad) * 0.15f - 0.05f;
                MakePart($"SpiderLegL{i}", PrimitiveType.Capsule, new Vector3(-Mathf.Abs(x), -0.08f, z),
                    new Vector3(0.04f, 0.35f, 0.04f), dark, Quaternion.Euler(0f, 0f, 55f + i * 5f));
                MakePart($"SpiderLegR{i}", PrimitiveType.Capsule, new Vector3(Mathf.Abs(x), -0.08f, z),
                    new Vector3(0.04f, 0.35f, 0.04f), dark, Quaternion.Euler(0f, 0f, -(55f + i * 5f)));
            }
            for (int i = 0; i < 4; i++)
            {
                float xOff = -0.06f + i * 0.04f;
                float size = (i < 2) ? 0.06f : 0.04f;
                MakePart($"SpEyeL{i}", PrimitiveType.Sphere, new Vector3(xOff - 0.02f, 0.15f + i * 0.02f, 0.3f),
                    Vector3.one * size, Color.black);
                MakePart($"SpEyeR{i}", PrimitiveType.Sphere, new Vector3(-xOff + 0.02f, 0.15f + i * 0.02f, 0.3f),
                    Vector3.one * size, Color.black);
            }
        }

        private void BuildStickInsect(Color body, Color dark)
        {
            string id = data != null ? data.insectId ?? "" : "";
            bool isLeaf = id.Contains("leaf_insect");
            Color col = isLeaf ? new Color(0.3f, 0.6f, 0.2f) : body;
            Color colDark = isLeaf ? new Color(0.2f, 0.4f, 0.12f) : dark;

            MakePart("BodySeg1", PrimitiveType.Capsule, new Vector3(0f, 0f, -0.25f), new Vector3(0.06f, 0.3f, 0.06f), col,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("BodySeg2", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.06f, 0.3f, 0.06f), colDark,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("BodySeg3", PrimitiveType.Capsule, new Vector3(0f, 0f, 0.25f), new Vector3(0.06f, 0.25f, 0.06f), col,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0.45f), new Vector3(0.12f, 0.1f, 0.12f), colDark);
            for (int i = 0; i < 3; i++)
            {
                float z = -0.2f + i * 0.2f;
                MakePart($"StickLegL{i}", PrimitiveType.Capsule, new Vector3(-0.35f, -0.08f, z),
                    new Vector3(0.03f, 0.35f, 0.03f), colDark, Quaternion.Euler(0f, 0f, 65f));
                MakePart($"StickLegR{i}", PrimitiveType.Capsule, new Vector3(0.35f, -0.08f, z),
                    new Vector3(0.03f, 0.35f, 0.03f), colDark, Quaternion.Euler(0f, 0f, -65f));
            }
            MakeAntennae(colDark, 0.35f);
            MakeEyes(0.45f, 0.06f);
            if (isLeaf)
            {
                MakePart("LeafWingL", PrimitiveType.Cube, new Vector3(-0.15f, 0.05f, 0f), new Vector3(0.25f, 0.02f, 0.4f), col);
                MakePart("LeafWingR", PrimitiveType.Cube, new Vector3(0.15f, 0.05f, 0f), new Vector3(0.25f, 0.02f, 0.4f), col);
                MakePart("LeafVeinL", PrimitiveType.Cylinder, new Vector3(-0.15f, 0.06f, 0f), new Vector3(0.01f, 0.01f, 0.35f),
                    colDark, Quaternion.Euler(0f, 0f, 85f));
                MakePart("LeafVeinR", PrimitiveType.Cylinder, new Vector3(0.15f, 0.06f, 0f), new Vector3(0.01f, 0.01f, 0.35f),
                    colDark, Quaternion.Euler(0f, 0f, -85f));
            }
        }

        private void BuildCentipede(Color body, Color dark)
        {
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0.4f), new Vector3(0.2f, 0.16f, 0.2f), dark);
            MakePart("FangL", PrimitiveType.Capsule, new Vector3(-0.08f, -0.02f, 0.52f), new Vector3(0.04f, 0.08f, 0.04f),
                new Color(0.6f, 0.1f, 0.1f), Quaternion.Euler(15f, 0f, 10f));
            MakePart("FangR", PrimitiveType.Capsule, new Vector3(0.08f, -0.02f, 0.52f), new Vector3(0.04f, 0.08f, 0.04f),
                new Color(0.6f, 0.1f, 0.1f), Quaternion.Euler(15f, 0f, -10f));
            for (int i = 0; i < 6; i++)
            {
                float z = 0.25f - i * 0.14f;
                float size = 0.16f - i * 0.005f;
                Color segCol = (i % 2 == 0) ? body : dark;
                MakePart($"Seg{i}", PrimitiveType.Sphere, new Vector3(0f, 0f, z), new Vector3(size, 0.1f, 0.13f), segCol);
                MakePart($"CentiLegL{i}", PrimitiveType.Capsule, new Vector3(-0.15f, -0.1f, z),
                    new Vector3(0.03f, 0.12f, 0.03f), dark, Quaternion.Euler(0f, 0f, 30f));
                MakePart($"CentiLegR{i}", PrimitiveType.Capsule, new Vector3(0.15f, -0.1f, z),
                    new Vector3(0.03f, 0.12f, 0.03f), dark, Quaternion.Euler(0f, 0f, -30f));
            }
            MakeAntennae(dark, 0.3f);
            MakeEyes(0.4f, 0.07f);
        }

        private void BuildPillBug(Color body, Color dark)
        {
            // 둥근 갑옷 느낌: 겹겹이 쌓인 마디 Sphere로 반구형 등 표현
            Color shell1 = body;
            Color shell2 = new Color(body.r * 0.85f, body.g * 0.85f, body.b * 0.85f);
            MakePart("Shell1", PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.18f), new Vector3(0.48f, 0.28f, 0.22f), shell1);
            MakePart("Shell2", PrimitiveType.Sphere, new Vector3(0f, 0.1f, 0.08f), new Vector3(0.55f, 0.32f, 0.2f), shell2);
            MakePart("Shell3", PrimitiveType.Sphere, new Vector3(0f, 0.11f, -0.02f), new Vector3(0.58f, 0.34f, 0.2f), shell1);
            MakePart("Shell4", PrimitiveType.Sphere, new Vector3(0f, 0.1f, -0.12f), new Vector3(0.56f, 0.32f, 0.2f), shell2);
            MakePart("Shell5", PrimitiveType.Sphere, new Vector3(0f, 0.08f, -0.22f), new Vector3(0.5f, 0.28f, 0.2f), shell1);
            MakePart("Shell6", PrimitiveType.Sphere, new Vector3(0f, 0.05f, -0.3f), new Vector3(0.4f, 0.22f, 0.18f), shell2);
            MakePart("ShellTail", PrimitiveType.Sphere, new Vector3(0f, 0.02f, -0.36f), new Vector3(0.28f, 0.15f, 0.12f), dark);
            // 마디 사이 홈(어두운 라인)
            for (int i = 0; i < 5; i++)
            {
                float z = 0.13f - i * 0.1f;
                MakePart($"SegLine{i}", PrimitiveType.Cylinder, new Vector3(0f, 0.14f, z), new Vector3(0.5f - i * 0.02f, 0.005f, 0.5f - i * 0.02f),
                    dark, Quaternion.Euler(90f, 0f, 0f));
            }
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0.3f), new Vector3(0.22f, 0.18f, 0.2f), dark);
            for (int i = 0; i < 7; i++)
            {
                float z = 0.15f - i * 0.07f;
                float legLen = 0.06f + (i < 3 ? 0f : 0.02f);
                MakePart($"PillLegL{i}", PrimitiveType.Capsule, new Vector3(-0.24f, -0.14f, z),
                    new Vector3(0.025f, legLen, 0.025f), dark, Quaternion.Euler(0f, 0f, 20f));
                MakePart($"PillLegR{i}", PrimitiveType.Capsule, new Vector3(0.24f, -0.14f, z),
                    new Vector3(0.025f, legLen, 0.025f), dark, Quaternion.Euler(0f, 0f, -20f));
            }
            MakeAntennae(dark, 0.2f);
            MakeEyes(0.3f, 0.05f);
        }

        private void BuildEarwig(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.22f, 0.45f, 0.22f), body,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0.2f), new Vector3(0.2f, 0.16f, 0.18f), dark);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.42f), new Vector3(0.25f, 0.22f, 0.25f), dark);
            // 집게: 크고 뚜렷한 V자 (각도 강화 + 끝 부분 두껍게)
            Color pincerCol = new Color(dark.r * 0.7f, dark.g * 0.5f, dark.b * 0.4f);
            MakePart("PincerBaseL", PrimitiveType.Capsule, new Vector3(-0.06f, -0.02f, -0.42f), new Vector3(0.055f, 0.12f, 0.055f),
                pincerCol, Quaternion.Euler(-45f, 0f, 25f));
            MakePart("PincerBaseR", PrimitiveType.Capsule, new Vector3(0.06f, -0.02f, -0.42f), new Vector3(0.055f, 0.12f, 0.055f),
                pincerCol, Quaternion.Euler(-45f, 0f, -25f));
            MakePart("PincerTipL", PrimitiveType.Capsule, new Vector3(-0.14f, -0.04f, -0.58f), new Vector3(0.045f, 0.14f, 0.045f),
                pincerCol, Quaternion.Euler(-70f, 0f, 10f));
            MakePart("PincerTipR", PrimitiveType.Capsule, new Vector3(0.14f, -0.04f, -0.58f), new Vector3(0.045f, 0.14f, 0.045f),
                pincerCol, Quaternion.Euler(-70f, 0f, -10f));
            MakePart("PincerEndL", PrimitiveType.Sphere, new Vector3(-0.15f, -0.08f, -0.7f), Vector3.one * 0.04f, pincerCol);
            MakePart("PincerEndR", PrimitiveType.Sphere, new Vector3(0.15f, -0.08f, -0.7f), Vector3.one * 0.04f, pincerCol);
            Color wingFold = new Color(body.r * 0.7f, body.g * 0.7f, body.b * 0.6f, 0.6f);
            MakePart("WingFoldL", PrimitiveType.Cube, new Vector3(-0.06f, 0.1f, -0.1f), new Vector3(0.1f, 0.01f, 0.25f), wingFold);
            MakePart("WingFoldR", PrimitiveType.Cube, new Vector3(0.06f, 0.1f, -0.1f), new Vector3(0.1f, 0.01f, 0.25f), wingFold);
            MakeLegs(dark, 3, 0f);
            MakeAntennae(dark, 0.32f);
            MakeEyes(0.42f, 0.1f);
        }

        private void BuildLonghornBeetle(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.65f, 0.4f, 0.85f), body);
            MakePart("ShellL", PrimitiveType.Sphere, new Vector3(-0.12f, 0.16f, -0.05f), new Vector3(0.3f, 0.18f, 0.72f), dark);
            MakePart("ShellR", PrimitiveType.Sphere, new Vector3(0.12f, 0.16f, -0.05f), new Vector3(0.3f, 0.18f, 0.72f), dark);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.48f), new Vector3(0.35f, 0.3f, 0.32f), dark);
            MakePart("LongAntL1", PrimitiveType.Capsule, new Vector3(-0.12f, 0.22f, 0.55f), new Vector3(0.03f, 0.25f, 0.03f),
                dark, Quaternion.Euler(-35f, 0f, 20f));
            MakePart("LongAntL2", PrimitiveType.Capsule, new Vector3(-0.22f, 0.45f, 0.7f), new Vector3(0.025f, 0.22f, 0.025f),
                dark, Quaternion.Euler(-15f, 0f, 10f));
            MakePart("LongAntL3", PrimitiveType.Capsule, new Vector3(-0.28f, 0.68f, 0.82f), new Vector3(0.02f, 0.2f, 0.02f),
                dark, Quaternion.Euler(-5f, 0f, 5f));
            MakePart("LongAntR1", PrimitiveType.Capsule, new Vector3(0.12f, 0.22f, 0.55f), new Vector3(0.03f, 0.25f, 0.03f),
                dark, Quaternion.Euler(-35f, 0f, -20f));
            MakePart("LongAntR2", PrimitiveType.Capsule, new Vector3(0.22f, 0.45f, 0.7f), new Vector3(0.025f, 0.22f, 0.025f),
                dark, Quaternion.Euler(-15f, 0f, -10f));
            MakePart("LongAntR3", PrimitiveType.Capsule, new Vector3(0.28f, 0.68f, 0.82f), new Vector3(0.02f, 0.2f, 0.02f),
                dark, Quaternion.Euler(-5f, 0f, -5f));
            MakeLegs(dark, 3, 0f);
            MakeEyes(0.48f, 0.12f);
        }

        private void BuildDamselfly(Color body, Color dark)
        {
            Color light = new Color(Mathf.Min(1, body.r + 0.15f), Mathf.Min(1, body.g + 0.15f), Mathf.Min(1, body.b + 0.2f));
            MakePart("Body", PrimitiveType.Capsule, new Vector3(0f, 0f, -0.3f), new Vector3(0.08f, 0.5f, 0.08f), light,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("TailSeg1", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.6f), new Vector3(0.07f, 0.07f, 0.08f), light);
            MakePart("TailSeg2", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.7f), new Vector3(0.06f, 0.06f, 0.07f), body);
            MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.04f, 0.15f), new Vector3(0.14f, 0.12f, 0.14f), dark);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.06f, 0.3f), new Vector3(0.25f, 0.2f, 0.22f), dark);
            Color wingCol = new Color(0.85f, 0.92f, 1f, 0.35f);
            MakeWing("WingL", new Vector3(-0.3f, 0.08f, 0.05f), new Vector3(0.5f, 0.01f, 0.1f), wingCol);
            MakeWing("WingR", new Vector3(0.3f, 0.08f, 0.05f), new Vector3(0.5f, 0.01f, 0.1f), wingCol);
            MakePart("WingLB", PrimitiveType.Sphere, new Vector3(-0.28f, 0.06f, -0.08f), new Vector3(0.45f, 0.01f, 0.08f), wingCol);
            MakePart("WingRB", PrimitiveType.Sphere, new Vector3(0.28f, 0.06f, -0.08f), new Vector3(0.45f, 0.01f, 0.08f), wingCol);
            MakePart("EyeL", PrimitiveType.Sphere, new Vector3(-0.1f, 0.12f, 0.35f), Vector3.one * 0.12f, new Color(0.3f, 0.7f, 0.9f));
            MakePart("EyeR", PrimitiveType.Sphere, new Vector3(0.1f, 0.12f, 0.35f), Vector3.one * 0.12f, new Color(0.3f, 0.7f, 0.9f));
            MakeLegs(dark, 3, 0.1f);
        }

        private void BuildCaterpillar(Color body, Color dark)
        {
            // 통통한 애벌레: 머리와 꼬리 마디가 크고 중간이 가장 뚱뚱
            float[] sizes = { 0.13f, 0.16f, 0.18f, 0.19f, 0.18f, 0.15f, 0.11f };
            float[] yOff  = { 0.02f, 0.01f, 0f,    0f,    0f,   0.01f, 0.02f };
            Color light = new Color(Mathf.Min(1, body.r + 0.15f), Mathf.Min(1, body.g + 0.15f), body.b * 0.8f);
            for (int i = 0; i < sizes.Length; i++)
            {
                float z = 0.36f - i * 0.12f;
                float s = sizes[i];
                Color segCol = (i % 2 == 0) ? body : light;
                MakePart($"CaterSeg{i}", PrimitiveType.Sphere, new Vector3(0f, yOff[i], z),
                    new Vector3(s, s * 0.85f, 0.11f), segCol);
                // 마디 위 등점 무늬 (작은 원형 장식)
                if (i > 0 && i < sizes.Length - 1)
                {
                    MakePart($"CaterDot{i}", PrimitiveType.Sphere, new Vector3(0f, s * 0.7f, z),
                        Vector3.one * 0.03f, dark);
                }
                // 다리: 진짜 다리(앞 3쌍) + 배다리(뒤 4쌍)
                if (i >= 1)
                {
                    float legSize = (i >= 4) ? 0.045f : 0.035f;
                    MakePart($"CaterLegL{i}", PrimitiveType.Sphere, new Vector3(-s * 0.55f, -s * 0.5f, z),
                        Vector3.one * legSize, dark);
                    MakePart($"CaterLegR{i}", PrimitiveType.Sphere, new Vector3(s * 0.55f, -s * 0.5f, z),
                        Vector3.one * legSize, dark);
                }
            }
            // 큰 둥근 머리
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.04f, 0.46f), new Vector3(0.16f, 0.15f, 0.14f), dark);
            // 큰 귀여운 눈
            MakePart("EyeL", PrimitiveType.Sphere, new Vector3(-0.06f, 0.08f, 0.5f), Vector3.one * 0.08f, Color.white);
            MakePart("EyeR", PrimitiveType.Sphere, new Vector3(0.06f, 0.08f, 0.5f), Vector3.one * 0.08f, Color.white);
            MakePart("PupilL", PrimitiveType.Sphere, new Vector3(-0.06f, 0.08f, 0.54f), Vector3.one * 0.05f, Color.black);
            MakePart("PupilR", PrimitiveType.Sphere, new Vector3(0.06f, 0.08f, 0.54f), Vector3.one * 0.05f, Color.black);
            // 짧고 귀여운 더듬이
            MakePart("AntStubL", PrimitiveType.Capsule, new Vector3(-0.05f, 0.12f, 0.5f), new Vector3(0.02f, 0.05f, 0.02f),
                dark, Quaternion.Euler(-25f, 0f, 20f));
            MakePart("AntStubR", PrimitiveType.Capsule, new Vector3(0.05f, 0.12f, 0.5f), new Vector3(0.02f, 0.05f, 0.02f),
                dark, Quaternion.Euler(-25f, 0f, -20f));
            // 꼬리 돌기
            MakePart("TailHorn", PrimitiveType.Capsule, new Vector3(0f, 0.05f, -0.48f), new Vector3(0.025f, 0.06f, 0.025f),
                body, Quaternion.Euler(-50f, 0f, 0f));
        }

        private void BuildGhostMantis(Color body, Color dark)
        {
            Color ghost = new Color(0.45f, 0.4f, 0.35f, 0.65f);
            Color ghostDark = new Color(0.3f, 0.28f, 0.25f, 0.6f);
            MakePart("Body", PrimitiveType.Capsule, new Vector3(0f, 0f, -0.15f), new Vector3(0.2f, 0.5f, 0.2f), ghost,
                Quaternion.Euler(80f, 0f, 0f));
            MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.15f, 0.15f), new Vector3(0.25f, 0.2f, 0.25f), ghost);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.3f, 0.25f), new Vector3(0.35f, 0.28f, 0.25f), ghostDark);
            MakePart("HeadCrest", PrimitiveType.Cube, new Vector3(0f, 0.42f, 0.22f), new Vector3(0.18f, 0.1f, 0.15f), ghost);
            MakePart("CrestTip", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0.2f), new Vector3(0.12f, 0.06f, 0.1f), ghostDark);
            MakePart("ArmUpperL", PrimitiveType.Capsule, new Vector3(-0.22f, 0.15f, 0.3f), new Vector3(0.08f, 0.2f, 0.08f),
                ghost, Quaternion.Euler(-10f, 0f, 20f));
            MakePart("ArmUpperR", PrimitiveType.Capsule, new Vector3(0.22f, 0.15f, 0.3f), new Vector3(0.08f, 0.2f, 0.08f),
                ghost, Quaternion.Euler(-10f, 0f, -20f));
            MakePart("ArmLowerL", PrimitiveType.Capsule, new Vector3(-0.28f, 0.28f, 0.42f), new Vector3(0.06f, 0.18f, 0.06f),
                ghost, Quaternion.Euler(-40f, 0f, 15f));
            MakePart("ArmLowerR", PrimitiveType.Capsule, new Vector3(0.28f, 0.28f, 0.42f), new Vector3(0.06f, 0.18f, 0.06f),
                ghost, Quaternion.Euler(-40f, 0f, -15f));
            MakePart("LeafDecorL", PrimitiveType.Cube, new Vector3(-0.3f, 0.1f, 0.05f), new Vector3(0.1f, 0.02f, 0.15f), ghost);
            MakePart("LeafDecorR", PrimitiveType.Cube, new Vector3(0.3f, 0.1f, 0.05f), new Vector3(0.1f, 0.02f, 0.15f), ghost);
            MakePart("LeafDecorLB", PrimitiveType.Cube, new Vector3(-0.25f, 0.08f, -0.15f), new Vector3(0.08f, 0.02f, 0.12f), ghostDark);
            MakePart("LeafDecorRB", PrimitiveType.Cube, new Vector3(0.25f, 0.08f, -0.15f), new Vector3(0.08f, 0.02f, 0.12f), ghostDark);
            MakeLegs(ghostDark, 2, -0.15f);
            MakeAntennae(ghostDark, 0.3f);
            MakeEyes(0.3f, 0.2f);
        }

        private void BuildFly(Color body, Color dark)
        {
            string id = data != null ? data.insectId ?? "" : "";
            bool isMosquito = id.Contains("mosquito");

            if (isMosquito)
            {
                // 모기: 가늘고 긴 몸, 긴 주둥이, 긴 다리
                MakePart("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.1f, 0.2f, 0.1f), dark,
                    Quaternion.Euler(90f, 0f, 0f));
                MakePart("Abdomen", PrimitiveType.Capsule, new Vector3(0f, -0.02f, -0.2f), new Vector3(0.08f, 0.18f, 0.08f),
                    new Color(0.4f, 0.15f, 0.12f), Quaternion.Euler(100f, 0f, 0f));
                MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.04f, 0.2f), new Vector3(0.14f, 0.13f, 0.14f), dark);
                Color eyeCol = new Color(0.2f, 0.2f, 0.2f);
                MakePart("BigEyeL", PrimitiveType.Sphere, new Vector3(-0.06f, 0.08f, 0.24f), Vector3.one * 0.08f, eyeCol);
                MakePart("BigEyeR", PrimitiveType.Sphere, new Vector3(0.06f, 0.08f, 0.24f), Vector3.one * 0.08f, eyeCol);
                MakePart("Proboscis", PrimitiveType.Capsule, new Vector3(0f, -0.02f, 0.32f), new Vector3(0.015f, 0.35f, 0.015f),
                    dark, Quaternion.Euler(75f, 0f, 0f));
                Color wingCol = new Color(1f, 1f, 1f, 0.25f);
                MakeWing("WingL", new Vector3(-0.18f, 0.12f, 0f), new Vector3(0.28f, 0.01f, 0.1f), wingCol);
                MakeWing("WingR", new Vector3(0.18f, 0.12f, 0f), new Vector3(0.28f, 0.01f, 0.1f), wingCol);
                // 모기 특유의 긴 다리 6개
                for (int i = 0; i < 3; i++)
                {
                    float z = 0.05f - i * 0.1f;
                    float spread = 30f + i * 12f;
                    MakePart($"MosqLegL{i}", PrimitiveType.Capsule, new Vector3(-0.2f, -0.15f, z),
                        new Vector3(0.02f, 0.3f, 0.02f), dark, Quaternion.Euler(-10f, 0f, spread));
                    MakePart($"MosqLegR{i}", PrimitiveType.Capsule, new Vector3(0.2f, -0.15f, z),
                        new Vector3(0.02f, 0.3f, 0.02f), dark, Quaternion.Euler(-10f, 0f, -spread));
                }
                MakeAntennae(dark, 0.12f, true);
            }
            else
            {
                // 파리: 통통한 몸, 거대한 빨간 복안, 짧은 주둥이
                MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.3f, 0.25f, 0.35f), dark);
                MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.03f, 0.15f), new Vector3(0.24f, 0.22f, 0.2f),
                    new Color(dark.r * 0.8f, dark.g * 0.8f, dark.b * 1.2f));
                MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.06f, 0.28f), new Vector3(0.26f, 0.24f, 0.24f), dark);
                Color eyeCol = new Color(0.75f, 0.12f, 0.08f);
                // 파리 복안: 머리의 대부분을 차지
                MakePart("BigEyeL", PrimitiveType.Sphere, new Vector3(-0.1f, 0.12f, 0.3f), Vector3.one * 0.17f, eyeCol);
                MakePart("BigEyeR", PrimitiveType.Sphere, new Vector3(0.1f, 0.12f, 0.3f), Vector3.one * 0.17f, eyeCol);
                MakePart("EyeHighlightL", PrimitiveType.Sphere, new Vector3(-0.08f, 0.16f, 0.34f), Vector3.one * 0.05f, Color.white);
                MakePart("EyeHighlightR", PrimitiveType.Sphere, new Vector3(0.08f, 0.16f, 0.34f), Vector3.one * 0.05f, Color.white);
                MakePart("Proboscis", PrimitiveType.Capsule, new Vector3(0f, -0.04f, 0.38f), new Vector3(0.04f, 0.08f, 0.04f),
                    dark, Quaternion.Euler(60f, 0f, 0f));
                Color wingCol = new Color(1f, 1f, 1f, 0.3f);
                MakeWing("WingL", new Vector3(-0.22f, 0.16f, 0.05f), new Vector3(0.35f, 0.01f, 0.18f), wingCol);
                MakeWing("WingR", new Vector3(0.22f, 0.16f, 0.05f), new Vector3(0.35f, 0.01f, 0.18f), wingCol);
                MakeLegs(dark, 3, 0.08f);
            }
        }

        private GameObject MakePart(string name, PrimitiveType type, Vector3 localPos, Vector3 localScale, Color color,
            Quaternion? rotation = null)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            if (rotation.HasValue)
                part.transform.localRotation = rotation.Value;

            Collider col = part.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);

            ApplyColor(part, color);
            return part;
        }

        private GameObject MakeSculpture(string name, InsectSculptureMeshes.Shape shape, Color color,
            Vector3? position = null, Vector3? scale = null, Quaternion? rotation = null)
        {
            var part = new GameObject(name);
            part.transform.SetParent(transform, false);
            part.transform.localPosition = position ?? Vector3.zero;
            part.transform.localScale = scale ?? Vector3.one;
            part.transform.localRotation = rotation ?? Quaternion.identity;
            part.AddComponent<MeshFilter>().sharedMesh = InsectSculptureMeshes.Get(shape);
            part.AddComponent<MeshRenderer>();
            ApplyColor(part, color);
            return part;
        }

        private void MakeWing(string name, Vector3 pos, Vector3 scale, Color color)
        {
            // An ellipsoid gives a tapered membrane rather than a rectangular plank.
            scale.x = Mathf.Max(scale.x, 2f * (Mathf.Abs(pos.x) - 0.06f));
            MakeSculpture(name, InsectSculptureMeshes.Shape.Wing, color, pos, scale);
        }

        private void BindWingSurfaces()
        {
            // Keep WingL/R as the animation contract, but rotate at the thorax.
            // Spots, veins and hindwings must follow the same hinge.
            foreach (string side in new[] { "L", "R" })
            {
                Transform surface = transform.Find("Wing" + side);
                if (surface == null) continue;
                Vector3 center = surface.localPosition;
                surface.name = "WingSurface" + side;
                Transform hinge = new GameObject("Wing" + side).transform;
                hinge.SetParent(transform, false);
                hinge.localPosition = new Vector3(side == "L" ? -0.07f : 0.07f, center.y, center.z);
                var attached = new System.Collections.Generic.List<Transform> { surface };
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    string n = child.name;
                    if (child == surface || child == hinge) continue;
                    bool decoration = n.StartsWith("Spot") || n.StartsWith("EyeSpot") || n.StartsWith("Vein")
                        || n.StartsWith("Wing") || n.StartsWith("HindTail") || n.StartsWith("TailCurl");
                    if (decoration && (side == "L" ? child.localPosition.x < 0f : child.localPosition.x > 0f))
                        attached.Add(child);
                }
                foreach (Transform child in attached) child.SetParent(hinge, true);
            }
        }

        private GameObject MakeSegment(string name, Vector3 from, Vector3 to, float width, Color color)
        {
            Vector3 delta = to - from;
            return MakePart(name, PrimitiveType.Capsule, (from + to) * 0.5f,
                new Vector3(width, delta.magnitude * 0.5f, width), color,
                Quaternion.FromToRotation(Vector3.up, delta.normalized));
        }

        private void MakeLegs(Color color, int pairs, float zOffset)
        {
            Color joint = Color.Lerp(color, Color.black, 0.22f);
            // Attach to the narrow thorax on slender species, the shell on beetles.
            Transform thorax = transform.Find("Thorax");
            MeshFilter thoraxMesh = thorax != null ? thorax.GetComponent<MeshFilter>() : null;
            float thoraxWidth = thoraxMesh != null && thoraxMesh.sharedMesh != null
                ? thoraxMesh.sharedMesh.bounds.size.x * thorax.localScale.x : .55f;
            float hipX = Mathf.Clamp(thoraxWidth * .4f, .07f, .22f);
            for (int i = 0; i < pairs; i++)
            {
                float z = zOffset + (i - (pairs - 1) * 0.5f) * 0.2f;
                float fan = (i - (pairs - 1) * 0.5f) * 0.09f;
                foreach (int sign in new[] { -1, 1 })
                {
                    string side = sign < 0 ? "L" : "R";
                    Vector3 hip = new Vector3(sign * hipX, -0.055f, z);
                    Vector3 knee = new Vector3(sign * (hipX + 0.18f), -0.16f, z + fan);
                    Vector3 foot = new Vector3(sign * (hipX + 0.24f), -0.39f, z + fan * 1.6f);
                    MakeSegment($"LegU{side}{i}", hip, knee, 0.055f, color);
                    MakePart($"Knee{side}{i}", PrimitiveType.Sphere, knee, Vector3.one * 0.056f, joint);
                    MakeSegment($"LegL{side}{i}", knee, foot, 0.035f, color);
                    MakePart($"Foot{side}{i}", PrimitiveType.Sphere, foot,
                        new Vector3(0.06f, 0.028f, 0.065f), joint);
                }
            }
        }

        private void MakeAntennae(Color color, float zBase, bool feathered = false)
        {
            // Explicit shared endpoints avoid the gaps made by independently
            // rotated capsules (most visible on the butterfly side portrait).
            for (int sign = -1; sign <= 1; sign += 2)
            {
                string side = sign < 0 ? "L" : "R";
                Vector3 root = new Vector3(sign * .085f, .145f, zBase + .08f);
                Vector3 elbow = new Vector3(sign * .145f, .30f, zBase + .17f);
                Vector3 tip = new Vector3(sign * .205f, .43f, zBase + .25f);
                MakeSegment("AntBase" + side, root, elbow, .022f, color);
                MakeSegment("AntMid" + side, elbow, tip, .016f, color);
                MakePart("AntTip" + side, PrimitiveType.Sphere, tip,
                    Vector3.one * (feathered ? .066f : .046f), color);
                if (feathered)
                    MakeSegment("AntFeather" + side, elbow + new Vector3(0f, .035f, 0f),
                        elbow + new Vector3(sign * .10f, .015f, .015f), .012f, color);
            }
        }

        private void MakeFlightLegs(Color color)
        {
            // Six narrow hanging legs on moths and butterflies. They remain
            // tucked beneath the thorax so wing portraits keep the focal shape.
            for (int i = 0; i < 3; i++)
            {
                float z = .17f - i * .12f;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    string side = sign < 0 ? "L" : "R";
                    Vector3 hip = new Vector3(sign * .075f, -.065f, z);
                    Vector3 knee = new Vector3(sign * .16f, -.17f, z - .025f);
                    Vector3 foot = new Vector3(sign * .20f, -.26f, z + .01f);
                    MakeSegment("LegU" + side + i, hip, knee, .017f, color);
                    MakeSegment("LegL" + side + i, knee, foot, .012f, color);
                }
            }
        }

        private void MakeEyes(float zPos, float size, float xSpread = 0.12f)
        {
            string id = data != null ? data.insectId ?? "" : "";
            bool armored = id.Contains("beetle") || id.Contains("stag") || id.StartsWith("ant") || id.Contains("_ant");
            Color eyeColor = armored ? new Color(0.055f, 0.075f, 0.09f) : new Color(0.76f, 0.87f, 0.67f);
            MakePart("EyeL", PrimitiveType.Sphere, new Vector3(-xSpread, 0.15f, zPos), Vector3.one * size, eyeColor);
            MakePart("EyeR", PrimitiveType.Sphere, new Vector3(xSpread, 0.15f, zPos), Vector3.one * size, eyeColor);
            // 큰 동공 (치비 톤: 64%)
            float pupilSize = size * 0.64f;
            MakePart("PupilL", PrimitiveType.Sphere, new Vector3(-xSpread, 0.15f, zPos + 0.04f), Vector3.one * pupilSize, new Color(0.05f, 0.05f, 0.08f));
            MakePart("PupilR", PrimitiveType.Sphere, new Vector3(xSpread, 0.15f, zPos + 0.04f), Vector3.one * pupilSize, new Color(0.05f, 0.05f, 0.08f));
            // 메인 하이라이트 (확대 — 촉촉한 큰 눈)
            float hlSize = size * 0.28f;
            MakePart("HighlightL", PrimitiveType.Sphere, new Vector3(-(xSpread - 0.02f), 0.19f, zPos + 0.05f), Vector3.one * hlSize, new Color(1f, 1f, 1f, 0.95f));
            MakePart("HighlightR", PrimitiveType.Sphere, new Vector3(xSpread - 0.02f, 0.19f, zPos + 0.05f), Vector3.one * hlSize, new Color(1f, 1f, 1f, 0.95f));
            // 서브 글린트 (동공 반대편 작은 반짝임 — 치비 캐릭터의 생기있는 눈 시그니처)
            float glintSize = size * 0.12f;
            MakePart("GlintL", PrimitiveType.Sphere, new Vector3(-(xSpread + 0.025f), 0.115f, zPos + 0.05f), Vector3.one * glintSize, new Color(1f, 1f, 1f, 0.8f));
            MakePart("GlintR", PrimitiveType.Sphere, new Vector3(xSpread - 0.025f, 0.115f, zPos + 0.05f), Vector3.one * glintSize, new Color(1f, 1f, 1f, 0.8f));
        }

        // Use the actual shell surface for gloss; no floating transparent white blob.
        private void MakeTopGloss(Vector3 bodyCenter, Vector3 bodyScale, float intensity = 0.14f)
        {
            Transform body = transform.Find("Body");
            Renderer renderer = body != null ? body.GetComponent<Renderer>() : null;
            Material mat = renderer != null ? renderer.sharedMaterial : null;
            if (mat == null) return;
            float smoothness = Mathf.Clamp01(0.48f + intensity);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        }

        // 모델 파츠 색칠 — shiny면 종별 색변환을 거쳐 전 파츠(하드코딩 색 포함)가 이로치 팔레트로 바뀜.
        private void ApplyColor(GameObject go, Color color)
        {
            // erased가 shiny를 이긴다 — 이름을 빼앗긴 개체에는 옮길 색조가 남아 있지 않다.
            if (erased) ApplyColorRaw(go, Erase(color));
            else ApplyColorRaw(go, shiny ? Shinify(color) : color);
        }

        /// <summary>
        /// 「지워진 개체」의 색 — 원색을 거의 잃은 검은 실루엣.
        ///
        /// 완전한 검정으로 뭉개지 않는다. 밝기 차이를 조금 남겨야 더듬이·다리·날개가 구분돼
        /// "무엇이었는지는 알겠는데 무엇인지는 모르겠는" 인상이 나온다 — 그게 이 개체의 요점이다.
        /// </summary>
        private static Color Erase(Color color)
        {
            float lum = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
            Color ink = new Color(0.055f, 0.05f, 0.07f);
            Color ghost = new Color(0.22f, 0.21f, 0.26f);
            return new Color(
                Mathf.Lerp(ink.r, ghost.r, lum),
                Mathf.Lerp(ink.g, ghost.g, lum),
                Mathf.Lerp(ink.b, ghost.b, lum),
                color.a);
        }

        // shiny 변환을 건너뛰는 원색 적용 — 반짝임/오라/바닥마커 등 효과 오버레이용(레어/금빛 고정색 보존).
        private void ApplyColorRaw(GameObject go, Color color)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r == null) return;
            // 셰이더 폴백 체인은 SceneryMaterials.LitShader가 단일 출처다. 광택·반투명은 공유하지 않는다 —
            // 아래 키틴·눈·막 광택이 곤충의 외형이고(무광 마감 ApplyFinish를 쓰면 전 종이 점토가 된다),
            // 반투명 경로엔 날개뿐 아니라 샤이니 반짝임·오라·바닥 마커 같은 이펙트가 섞여 있다.
            // 공유 체인의 마지막 방어선(에러 셰이더)은 칠하지 않는다 — 옛 체인은 Sprites/Default에서 멈추고
            // 못 찾으면 프리미티브의 기본 머티리얼을 그대로 두었다.
            Shader shader = InsectGame.Core.SceneryMaterials.LitShader;
            if (shader == null || shader.name == "Hidden/InternalErrorShader") return;
            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            // PBR 광택: 옛 ApplyColor는 색만 칠해 전 곤충이 무광 점토처럼 보였음(품질 저하 핵심).
            // Standard/URP Lit에서만 _Glossiness/_Metallic 설정(Unlit/Sprites fallback은 프로퍼티 없어 가드).
            bool pbr = shader.name == "Standard" || shader.name.Contains("Lit");
            if (color.a < 1f)
            {
                if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 3);
                if (mat.HasProperty("_Surface"))
                {
                    mat.SetFloat("_Surface", 1f);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                }
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                // 날개/반투명: 막·천 느낌(번들거림 억제)
                if (pbr)
                {
                    if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", .18f);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", .18f);
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
                }
            }
            else if (pbr)
            {
                // 외골격 키틴 광택 + 미세 금속감 — 전 34종 동시 개선
                bool eye = go.name.Contains("Eye") || go.name.Contains("Pupil");
                bool membrane = go.name.StartsWith("Wing") || go.name.StartsWith("Fur");
                bool chitin = go.name.StartsWith("Shell") || go.name.StartsWith("Horn") || go.name.StartsWith("Mand");
                float smoothness = eye ? .78f : membrane ? .18f : chitin ? .52f : .32f;
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", chitin ? .025f : 0f);
            }
            r.material = mat;
        }

        private Color GetInsectColor()
        {
            if (data == null) return Color.gray;
            string id = data.insectId ?? "";

            // 종 시그니처 색 우선(군주나비=주황, 모르포=파랑 등) — 해시색이 종 날개색을 무시하던 문제 해소.
            // shiny(이로치) 색변환은 ApplyColor에서 전 파츠에 일괄 적용하므로 여기선 항상 일반 베이스 반환.
            if (TryGetSpeciesColor(id, out Color signature))
                return Color.Lerp(signature, GetRarityColor(), 0.12f); // 시그니처 색은 약하게만 레어 틴트(식별성 유지)

            uint hash = 0;
            foreach (char c in id) hash = hash * 31 + c;
            float hue = (hash % 360) / 360f;
            float sat = 0.5f + (hash % 100) / 200f;
            float val = 0.6f + (hash % 80) / 200f;

            Color baseCol = Color.HSVToRGB(hue, sat, val);
            return Color.Lerp(baseCol, GetRarityColor(), 0.3f);
        }

        // 실제 곤충 외형에 맞춘 종 고유 시그니처 색. 없으면 false→해시 절차색 사용(변종 다양성 유지).
        private static bool TryGetSpeciesColor(string id, out Color color)
        {
            if (id.Contains("monarch"))     { color = new Color(0.95f, 0.45f, 0.05f); return true; } // 군주나비 주황
            if (id.Contains("morpho"))      { color = new Color(0.22f, 0.45f, 0.95f); return true; } // 모르포 이리데센트 블루
            if (id.Contains("cabbage"))     { color = new Color(0.93f, 0.93f, 0.86f); return true; } // 배추흰나비 흰/크림
            if (id.Contains("swallowtail")) { color = new Color(0.96f, 0.83f, 0.18f); return true; } // 호랑나비 노랑
            if (id.Contains("azure"))       { color = new Color(0.40f, 0.70f, 0.96f); return true; } // 푸른부전나비 하늘
            if (id.Contains("luna"))        { color = new Color(0.62f, 0.92f, 0.62f); return true; } // 루나나방 연두
            if (id.Contains("atlas"))       { color = new Color(0.62f, 0.36f, 0.20f); return true; } // 아틀라스나방 적갈
            if (id.Contains("alexandras"))  { color = new Color(0.10f, 0.62f, 0.50f); return true; } // 비단제비나비 청록
            if (id.Contains("rainbow"))     { color = new Color(0.85f, 0.30f, 0.65f); return true; } // 무지개나비(가챠) 마젠타
            color = default;
            return false;
        }

        // 이로치(색다른 곤충) 색 변환 — 종마다 고정 색조 이동(포켓몬식 일관 팔레트). 전 파츠 일괄 적용해
        // 하드코딩 색(무당벌레 빨강·말벌 노랑·벌 검정줄·사마귀 분홍)도 반드시 다른 색으로 바뀜.
        private Color Shinify(Color c)
        {
            if (c.a <= 0f) return c;
            Color.RGBToHSV(c, out float h, out float s, out float v);
            // 흰색·눈 하이라이트(저채도+고명도)는 유지 — 눈/광택 식별성 보존
            if (s < 0.12f && v > 0.78f) return c;

            if (cachedShinyShift < 0f)
            {
                // 종별 고정 색조 이동량(0.35~0.6): 같은 종 이로치는 항상 같은 색
                uint hash = 0;
                string id = data != null ? data.insectId ?? "" : "";
                foreach (char ch in id) hash = hash * 31 + ch;
                cachedShinyShift = 0.35f + (hash % 100) / 100f * 0.25f;
            }
            h = (h + cachedShinyShift) % 1f;

            if (s < 0.12f && v < 0.3f)
            {
                // 거의 검정(벌·말벌 줄무늬)은 색조만으론 안 보임 → 짙은 유채색 부여
                s = 0.55f; v = Mathf.Max(v, 0.32f);
            }
            else
            {
                s = Mathf.Min(1f, s * 1.08f + 0.05f);
                v = Mathf.Min(1f, v + 0.06f);
            }
            Color outC = Color.HSVToRGB(h, s, v);
            outC.a = c.a;
            return outC;
        }

        private void CreateNameLabel()
        {
            Transform existing = transform.Find("NameLabel");
            if (existing != null) DestroyImmediate(existing.gameObject);
            if (data == null) return;

            GameObject label = new GameObject("NameLabel");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = new Vector3(0f, 2.5f, 0f);

            TextMesh text = label.AddComponent<TextMesh>();
            if (erased)
            {
                // 빼앗긴 것이 바로 이름이다 — 종명 자리를 비워 둔다. 레벨은 남긴다(위험도는 보여야 한다).
                text.text = $"??? Lv.{level}";
            }
            else
            {
                string prefix = shiny ? "★ " : "";
                string suffix = shiny ? " ★" : "";
                text.text = $"{prefix}{data.displayName} Lv.{level}{suffix}";
            }
            text.characterSize = 0.2f;
            text.fontSize = 48;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = GetRarityColor();
        }

        private void CreateGroundMarker()
        {
            Transform existing = transform.Find("GroundMarker");
            if (existing != null) DestroyImmediate(existing.gameObject);

            Color color = GetRarityColor();
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "GroundMarker";
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            marker.transform.localScale = new Vector3(2f, 0.02f, 2f);

            Collider mc = marker.GetComponent<Collider>();
            if (mc != null) UnityEngine.Object.Destroy(mc);
            ApplyColorRaw(marker, new Color(color.r, color.g, color.b, 0.5f));
        }

        private Color GetRarityColor()
        {
            if (data == null) return Color.gray;
            switch (data.rarity)
            {
                case InsectRarity.Common:    return new Color(0.55f, 0.45f, 0.3f);
                case InsectRarity.Uncommon:  return new Color(0.3f, 0.7f, 0.3f);
                case InsectRarity.Rare:      return new Color(0.3f, 0.5f, 0.9f);
                case InsectRarity.Epic:      return new Color(0.7f, 0.3f, 0.9f);
                case InsectRarity.Legendary: return new Color(1f, 0.8f, 0.2f);
                default:                     return Color.gray;
            }
        }

        private void BuildAphid(Color body, Color dark)
        {
            // 진딧물: 아주 작고 둥글둥글, 긴 다리, 꿀관
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.35f, 0.3f, 0.4f), body);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.25f), new Vector3(0.2f, 0.18f, 0.2f), dark);
            // 꿀관 (뒤쪽 돌기 2개)
            MakePart("CornicleL", PrimitiveType.Capsule, new Vector3(-0.1f, 0.1f, -0.22f), new Vector3(0.03f, 0.1f, 0.03f),
                body, Quaternion.Euler(-20f, 0f, 10f));
            MakePart("CornicleR", PrimitiveType.Capsule, new Vector3(0.1f, 0.1f, -0.22f), new Vector3(0.03f, 0.1f, 0.03f),
                body, Quaternion.Euler(-20f, 0f, -10f));
            // 긴 가느다란 다리
            for (int i = 0; i < 3; i++)
            {
                float z = -0.05f + i * 0.12f;
                MakePart($"LegL{i}", PrimitiveType.Capsule, new Vector3(-0.18f, -0.15f, z),
                    new Vector3(0.02f, 0.18f, 0.02f), dark, Quaternion.Euler(0f, 0f, 20f));
                MakePart($"LegR{i}", PrimitiveType.Capsule, new Vector3(0.18f, -0.15f, z),
                    new Vector3(0.02f, 0.18f, 0.02f), dark, Quaternion.Euler(0f, 0f, -20f));
            }
            MakeAntennae(dark, 0.25f);
            MakeEyes(0.28f, 0.08f);
        }

        private void BuildAntlion(Color body, Color dark)
        {
            // 개미귀신: 큰 턱, 납작한 몸, 넓은 머리
            MakePart("Body", PrimitiveType.Sphere, new Vector3(0f, 0f, -0.1f), new Vector3(0.4f, 0.2f, 0.6f), body);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.3f), new Vector3(0.4f, 0.22f, 0.35f), dark);
            // 거대한 턱 (집게)
            MakePart("JawL", PrimitiveType.Capsule, new Vector3(-0.12f, 0f, 0.5f), new Vector3(0.05f, 0.2f, 0.05f),
                dark, Quaternion.Euler(-50f, 20f, 0f));
            MakePart("JawR", PrimitiveType.Capsule, new Vector3(0.12f, 0f, 0.5f), new Vector3(0.05f, 0.2f, 0.05f),
                dark, Quaternion.Euler(-50f, -20f, 0f));
            MakePart("JawTipL", PrimitiveType.Sphere, new Vector3(-0.18f, 0.05f, 0.65f), Vector3.one * 0.04f, body);
            MakePart("JawTipR", PrimitiveType.Sphere, new Vector3(0.18f, 0.05f, 0.65f), Vector3.one * 0.04f, body);
            MakeLegs(dark, 3, -0.05f);
            MakeEyes(0.35f, 0.12f);
        }

        private void BuildDungBeetle(Color body, Color dark)
        {
            // 쇠똥구리: 넓적한 몸, 삽 모양 머리, 굵은 앞다리
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.7f, 0.4f, 0.8f), body);
            MakePart("Shell", PrimitiveType.Sphere, new Vector3(0f, 0.15f, -0.05f), new Vector3(0.65f, 0.25f, 0.7f), dark);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.45f), new Vector3(0.45f, 0.25f, 0.3f), dark);
            // 삽 모양 머리 돌기
            MakePart("Shovel", PrimitiveType.Cube, new Vector3(0f, 0.12f, 0.55f), new Vector3(0.35f, 0.06f, 0.1f), dark);
            // 굵은 앞다리 (삽질용)
            MakePart("DigLegL", PrimitiveType.Capsule, new Vector3(-0.3f, -0.1f, 0.3f), new Vector3(0.1f, 0.2f, 0.1f),
                dark, Quaternion.Euler(0f, 0f, 35f));
            MakePart("DigLegR", PrimitiveType.Capsule, new Vector3(0.3f, -0.1f, 0.3f), new Vector3(0.1f, 0.2f, 0.1f),
                dark, Quaternion.Euler(0f, 0f, -35f));
            // 소똥 (옆에)
            MakePart("DungBall", PrimitiveType.Sphere, new Vector3(0.4f, -0.1f, -0.3f), Vector3.one * 0.25f,
                new Color(0.35f, 0.28f, 0.15f));
            MakeLegs(dark, 2, -0.1f);
            MakeEyes(0.45f, 0.1f);
        }

        private void BuildClickBeetle(Color body, Color dark)
        {
            // 방아벌레: 길쭉한 몸, 뾰족한 모서리, 도약 장치(전흉)
            MakePart("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.25f, 0.5f, 0.25f), body,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Shell", PrimitiveType.Cube, new Vector3(0f, 0.1f, -0.1f), new Vector3(0.22f, 0.08f, 0.6f), dark);
            MakePart("ShellLine", PrimitiveType.Cylinder, new Vector3(0f, 0.14f, -0.1f), new Vector3(0.01f, 0.01f, 0.55f), body);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.35f), new Vector3(0.22f, 0.18f, 0.2f), dark);
            // 전흉 (클릭 장치)
            MakePart("Pronotum", PrimitiveType.Cube, new Vector3(0f, 0.08f, 0.2f), new Vector3(0.24f, 0.1f, 0.15f), body);
            MakePart("ClickSpine", PrimitiveType.Capsule, new Vector3(0f, -0.02f, 0.15f), new Vector3(0.04f, 0.06f, 0.04f),
                body, Quaternion.Euler(90f, 0f, 0f));
            MakeLegs(dark, 3, 0f);
            MakeAntennae(dark, 0.3f);
            MakeEyes(0.38f, 0.08f);
        }

        private void AddRarityEffects()
        {
            if (data == null) return;

            if (data.rarity == InsectRarity.Epic)
            {
                // Epic: 은은한 보라 오라
                GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                aura.name = "EpicAura";
                aura.transform.SetParent(transform, false);
                aura.transform.localPosition = Vector3.zero;
                aura.transform.localScale = Vector3.one * 1.3f;
                Collider c = aura.GetComponent<Collider>();
                if (c != null) Destroy(c);
                ApplyColorRaw(aura, new Color(0.6f, 0.2f, 0.8f, 0.08f));
            }
            else if (data.rarity == InsectRarity.Legendary)
            {
                // Legendary: 금색 오라 + 빛나는 링
                GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                aura.name = "LegendaryAura";
                aura.transform.SetParent(transform, false);
                aura.transform.localPosition = Vector3.zero;
                aura.transform.localScale = Vector3.one * 1.5f;
                Collider c = aura.GetComponent<Collider>();
                if (c != null) Destroy(c);
                ApplyColorRaw(aura, new Color(1f, 0.85f, 0.2f, 0.1f));

                GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "LegendaryRing";
                ring.transform.SetParent(transform, false);
                ring.transform.localPosition = new Vector3(0f, -0.05f, 0f);
                ring.transform.localScale = new Vector3(1.2f, 0.01f, 1.2f);
                Collider rc = ring.GetComponent<Collider>();
                if (rc != null) Destroy(rc);
                ApplyColorRaw(ring, new Color(1f, 0.8f, 0.15f, 0.3f));
            }
        }

        private float GetRarityScale()
        {
            if (data == null) return 1.2f;
            switch (data.rarity)
            {
                case InsectRarity.Common:    return 1.0f;
                case InsectRarity.Uncommon:  return 1.2f;
                case InsectRarity.Rare:      return 1.4f;
                case InsectRarity.Epic:      return 1.6f;
                case InsectRarity.Legendary: return 1.9f;
                default:                     return 1.2f;
            }
        }

        public void Despawn()
        {
            // 수문장은 풀 객체가 아니다(onDespawn도 소속 리전도 없음) — 아래 래치를 걸면 아무것도
            // 반환·파괴되지 않은 채 CanBeEngaged만 영구 false가 돼, **한 번 지거나 도주하면 눈앞에
            // 서 있는 수문장에게 다시 말을 걸 수 없고 리전이 영영 잠긴다**(2026-09-09). 격파 시 실제
            // 제거는 PlaySceneBootstrap.RemoveGuardianSeal이 한다. 여기서는 교전만 풀어 준다.
            if (IsGuardian)
            {
                engaged = false;
                return;
            }

            // 다중 호출 가드 — Battle/Capture가 동시에 Despawn 호출 시 풀 중복 반환 차단.
            // 옛은 onDespawn 두 번 발화 → 풀이 같은 객체 두 번 Return → 다음 Get에서 같은 인스턴스 2번 회귀.
            if (despawnedThisCycle) return;
            despawnedThisCycle = true;

            // 풀 반환 전 진행 중 코루틴 정리 (다음 인스턴스 사용 시 잔존 영향 방지)
            StopAllCoroutines();
            onDespawn?.Invoke(this);
        }

        /// <summary>
        /// 스포너가 몸만 <b>조용히 거둔다</b>(플레이어가 멀어짐·서브에리어 전환) — 게임플레이 퇴장이 아니다.
        /// <see cref="Despawn"/>과 달리 콜백을 부르지 않는다: 콜백은 "그 자리가 비었다"(재생 지연 시작)는 뜻이라,
        /// 거리로 거둔 것까지 그 길로 보내면 멀어졌다 돌아올 때마다 새 곤충이 된다(옛 리전 이동 리롤).
        /// 다중 호출 가드는 같이 건다 — 이 참조를 쥔 쪽(아이 NPC 등)이 뒤늦게 <c>Despawn</c>을 불러도 no-op이다.
        /// 거뒀으면 true(호출부가 풀에 돌린다). 수문장·이미 퇴장한 몸은 false.
        /// </summary>
        internal bool Recall()
        {
            if (IsGuardian || despawnedThisCycle) return false;
            despawnedThisCycle = true;
            engaged = false;
            alertState = 0;
            StopAllCoroutines();
            return true;
        }
    }
}
