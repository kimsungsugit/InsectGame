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
        private SpawnPoint ownerPoint;
        private float bobPhase;
        private Vector3 basePosition;
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
        private bool engaged;                 // 포획 상호작용 중 — 절대 도주 안 함
        // 플레이어 추적(전 곤충 공유, 프레임당 1회 계산)
        private static Transform cachedPlayer;
        private static Vector3 lastPlayerPos;
        private static float playerSpeed;
        private static int playerTrackFrame = -1;
        // 아이템 도주 방지 확률 제공자 — 부트스트랩이 세팅(itemEffects.GetFleePreventChance). null이면 0(방지 없음).
        // InsectEntity는 풀링 객체라 AutoWire/provider 참조가 없어 static 훅으로 주입.
        public static System.Func<float> FleePreventChanceProvider;
        private bool despawnedThisCycle; // Despawn 다중 호출 가드 (Battle/Capture 동시 호출 시 풀 중복 반환 차단)

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
        public bool CanBeEngaged => !forBattle && !engaged && alertState != 2 && !despawnedThisCycle;
        public SpawnPoint OwnerPoint => ownerPoint;
        public string RegionId => ownerPoint != null ? ownerPoint.regionId : string.Empty;

        public void Initialize(InsectData insectData, int insectLevel, SpawnPoint point,
            Action<InsectEntity> despawnCallback, float erasedChance = 0f)
        {
            data = insectData;
            level = insectLevel;
            ownerPoint = point;
            onDespawn = despawnCallback;
            shiny = UnityEngine.Random.value < 0.01f; // 1% 확률 색다른 곤충
            // 지워진 개체 — 확률은 스폰너가 리전에서 정해 넘긴다(여기에 리전 목록을 두지 않는다).
            erased = erasedChance > 0f && UnityEngine.Random.value < erasedChance;
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
            engaged = false;
            despawnedThisCycle = false;

            ClearChildren();
            BuildModel();
            AddRarityEffects();
            CreateNameLabel();
            CreateGroundMarker();
            float scale = GetRarityScale();
            transform.localScale = Vector3.one * scale;
            basePosition = transform.position;
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
            engaged = false;
            despawnedThisCycle = false;

            ClearChildren();
            BuildModel();
            basePosition = transform.position;
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
            if (forBattle)
            {
                // 배틀: 전투 포즈 유지 — 가벼운 상하만(회전·드리프트·경계 없음)
                float bs = 1.6f + (bobPhase % 1.5f);
                transform.position = basePosition + new Vector3(0f, Mathf.Sin(t * bs + bobPhase) * 0.25f, 0f);
                return;
            }

            EnsureMoveStyle();
            UpdatePlayerTracking();
            float dt = Time.deltaTime;

            // ===== 도주 진행 (이동 방식별로 다른 도주 모션) =====
            if (alertState == 2)
            {
                fleeTimer -= dt;
                float elapsed = 1.1f - fleeTimer; // 도주 경과 시간
                if (cachedMoveStyle == 1)
                {
                    // 비행: 날개로 날아오르며 멀어짐 — 점점 고도 상승(하늘로 사라짐)
                    Vector3 p = transform.position + fleeDir * 7.5f * dt;
                    p.y = basePosition.y + 0.55f + elapsed * 2.8f;
                    transform.position = p;
                    FaceFlee(dt, 8f);
                }
                else if (cachedMoveStyle == 2)
                {
                    // 점프: 큰 포물선 도약으로 튀어 달아남 — 공중에 뜬 동안 더 멀리, 착지 땐 멈칫
                    float ph = (elapsed % 0.45f) / 0.45f;
                    float hop = Mathf.Sin(ph * Mathf.PI);
                    Vector3 p = transform.position + fleeDir * (6.5f * (0.3f + hop)) * dt;
                    p.y = basePosition.y + hop * 0.75f;
                    transform.position = p;
                    FaceFlee(dt, 11f);
                }
                else
                {
                    // 기어다님: 지면에 낮게 빠르게 허둥지둥
                    Vector3 p = transform.position + fleeDir * 6.0f * dt;
                    p.y = basePosition.y + 0.05f + Mathf.Abs(Mathf.Sin(elapsed * 24f)) * 0.07f;
                    transform.position = p;
                    FaceFlee(dt, 12f);
                }
                AnchorGrass();
                if (fleeTimer <= 0f) Despawn(); // 놓침 — 사라짐
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
                        alertState = 2;
                        Vector3 away = transform.position - cachedPlayer.position; away.y = 0f;
                        fleeDir = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
                        fleeTimer = 1.1f;
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

            transform.position = basePosition + offset;
            if (rotSpeed > 0f)
                transform.Rotate(Vector3.up, rotSpeed * Time.deltaTime, Space.World);

            AnchorGroundMarker(offset.y);
            AnchorGrass();
        }

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
            engaged = value;
            if (value) { alertState = 1; patience = 2.6f; alertGraceTimer = 0.6f; }
        }

        public void ScareAway()
        {
            if (!CanBeEngaged) return;

            UpdatePlayerTracking();
            alertState = 2;
            Vector3 away = cachedPlayer != null
                ? transform.position - cachedPlayer.position
                : transform.forward;
            away.y = 0f;
            fleeDir = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
            fleeTimer = 1.1f;
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
            cachedGrass.position = basePosition;
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
            MakePart("FrontLegL", PrimitiveType.Capsule, new Vector3(-0.28f, -0.15f, 0.3f), new Vector3(0.06f, 0.22f, 0.06f),
                dark, Quaternion.Euler(0f, 0f, 25f));
            MakePart("FrontLegR", PrimitiveType.Capsule, new Vector3(0.28f, -0.15f, 0.3f), new Vector3(0.06f, 0.22f, 0.06f),
                dark, Quaternion.Euler(0f, 0f, -25f));
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
            MakePart("Shell", PrimitiveType.Sphere, new Vector3(0f, 0.18f, -0.08f), new Vector3(0.72f, 0.3f, 0.86f), dark);
            MakePart("ShellLineL", PrimitiveType.Cylinder, new Vector3(-0.14f, 0.26f, -0.08f), new Vector3(0.015f, 0.01f, 0.7f), body);
            MakePart("ShellLineR", PrimitiveType.Cylinder, new Vector3(0.14f, 0.26f, -0.08f), new Vector3(0.015f, 0.01f, 0.7f), body);
            // 각진 전흉(pronotum) — 사슴벌레 특유의 넓적한 가슴판
            MakePart("Pronotum", PrimitiveType.Sphere, new Vector3(0f, 0.14f, 0.42f), new Vector3(0.62f, 0.3f, 0.4f), dark);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.1f, 0.68f), new Vector3(0.42f, 0.32f, 0.4f), dark);
            // === 큰 집게턱 (좌우 대칭, 3분절 곡선) ===
            MakePart("MandBaseL", PrimitiveType.Capsule, new Vector3(-0.17f, 0.12f, 0.86f), new Vector3(0.06f, 0.17f, 0.06f),
                jaw, Quaternion.Euler(72f, 0f, 26f));
            MakePart("MandBaseR", PrimitiveType.Capsule, new Vector3(0.17f, 0.12f, 0.86f), new Vector3(0.06f, 0.17f, 0.06f),
                jaw, Quaternion.Euler(72f, 0f, -26f));
            MakePart("MandMidL", PrimitiveType.Capsule, new Vector3(-0.29f, 0.13f, 1.06f), new Vector3(0.05f, 0.15f, 0.05f),
                jaw, Quaternion.Euler(82f, 0f, 44f));
            MakePart("MandMidR", PrimitiveType.Capsule, new Vector3(0.29f, 0.13f, 1.06f), new Vector3(0.05f, 0.15f, 0.05f),
                jaw, Quaternion.Euler(82f, 0f, -44f));
            // 안쪽 돌기(이빨) — 사슴벌레 턱 안쪽의 톱니
            MakePart("MandToothL", PrimitiveType.Capsule, new Vector3(-0.2f, 0.13f, 1.12f), new Vector3(0.03f, 0.08f, 0.03f),
                jaw, Quaternion.Euler(90f, 0f, -54f));
            MakePart("MandToothR", PrimitiveType.Capsule, new Vector3(0.2f, 0.13f, 1.12f), new Vector3(0.03f, 0.08f, 0.03f),
                jaw, Quaternion.Euler(90f, 0f, 54f));
            // 끝 — 안쪽으로 굽어 마주봄
            MakePart("MandTipL", PrimitiveType.Capsule, new Vector3(-0.16f, 0.14f, 1.26f), new Vector3(0.04f, 0.13f, 0.04f),
                jaw, Quaternion.Euler(96f, 0f, 72f));
            MakePart("MandTipR", PrimitiveType.Capsule, new Vector3(0.16f, 0.14f, 1.26f), new Vector3(0.04f, 0.13f, 0.04f),
                jaw, Quaternion.Euler(96f, 0f, -72f));
            MakePart("MandPointL", PrimitiveType.Sphere, new Vector3(-0.07f, 0.14f, 1.34f), Vector3.one * 0.04f, jaw);
            MakePart("MandPointR", PrimitiveType.Sphere, new Vector3(0.07f, 0.14f, 1.34f), Vector3.one * 0.04f, jaw);
            MakePart("ClawL", PrimitiveType.Cube, new Vector3(-0.28f, -0.24f, 0.28f), new Vector3(0.05f, 0.08f, 0.11f), dark);
            MakePart("ClawR", PrimitiveType.Cube, new Vector3(0.28f, -0.24f, 0.28f), new Vector3(0.05f, 0.08f, 0.11f), dark);
            MakeEyes(0.78f, 0.11f, 0.18f);
            MakeLegs(dark, 3, 0f);
        }

        private void BuildButterfly(Color body, Color dark)
        {
            MakePart("Body", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.18f, 0.34f, 0.18f), dark,
                Quaternion.Euler(90f, 0f, 0f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.45f), new Vector3(0.3f, 0.3f, 0.28f), dark);
            Color wingCol = new Color(body.r, body.g, body.b, 0.85f);
            Color wingSpot = new Color(Mathf.Min(1, body.r + 0.3f), Mathf.Min(1, body.g + 0.3f), body.b * 0.5f);
            Color wingEdge = new Color(dark.r, dark.g, dark.b, 0.7f);
            MakeWing("WingL", new Vector3(-0.5f, 0.1f, 0f), new Vector3(0.7f, 0.02f, 0.6f), wingCol);
            MakeWing("WingR", new Vector3(0.5f, 0.1f, 0f), new Vector3(0.7f, 0.02f, 0.6f), wingCol);
            MakePart("SpotL1", PrimitiveType.Sphere, new Vector3(-0.45f, 0.12f, 0.1f), new Vector3(0.15f, 0.02f, 0.15f), wingSpot);
            MakePart("SpotR1", PrimitiveType.Sphere, new Vector3(0.45f, 0.12f, 0.1f), new Vector3(0.15f, 0.02f, 0.15f), wingSpot);
            MakePart("SpotL2", PrimitiveType.Sphere, new Vector3(-0.55f, 0.12f, 0f), new Vector3(0.12f, 0.02f, 0.12f), wingSpot);
            MakePart("SpotR2", PrimitiveType.Sphere, new Vector3(0.55f, 0.12f, 0f), new Vector3(0.12f, 0.02f, 0.12f), wingSpot);
            MakePart("SpotL3", PrimitiveType.Sphere, new Vector3(-0.4f, 0.12f, -0.1f), new Vector3(0.1f, 0.02f, 0.1f), wingSpot);
            MakePart("SpotR3", PrimitiveType.Sphere, new Vector3(0.4f, 0.12f, -0.1f), new Vector3(0.1f, 0.02f, 0.1f), wingSpot);
            MakePart("WingTipL", PrimitiveType.Sphere, new Vector3(-0.88f, 0.1f, 0.05f), new Vector3(0.18f, 0.025f, 0.24f), wingEdge);
            MakePart("WingTipR", PrimitiveType.Sphere, new Vector3(0.88f, 0.1f, 0.05f), new Vector3(0.18f, 0.025f, 0.24f), wingEdge);
            MakePart("WingLB", PrimitiveType.Sphere, new Vector3(-0.35f, 0.08f, -0.25f), new Vector3(0.45f, 0.02f, 0.4f), wingCol);
            MakePart("WingRB", PrimitiveType.Sphere, new Vector3(0.35f, 0.08f, -0.25f), new Vector3(0.45f, 0.02f, 0.4f), wingCol);
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
            MakeWing("WingL", new Vector3(-0.55f, 0.05f, 0.05f), new Vector3(0.8f, 0.02f, 0.7f), wingCol);
            MakeWing("WingR", new Vector3(0.55f, 0.05f, 0.05f), new Vector3(0.8f, 0.02f, 0.7f), wingCol);
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
            MakePart("Body", PrimitiveType.Capsule, new Vector3(0f, 0f, -0.15f), new Vector3(0.2f, 0.5f, 0.2f), body,
                Quaternion.Euler(80f, 0f, 0f));
            MakePart("Thorax", PrimitiveType.Sphere, new Vector3(0f, 0.15f, 0.15f), new Vector3(0.25f, 0.2f, 0.25f), body);
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.3f, 0.25f), new Vector3(0.38f, 0.28f, 0.25f), dark);
            MakePart("HeadCrest", PrimitiveType.Cube, new Vector3(0f, 0.38f, 0.22f), new Vector3(0.12f, 0.06f, 0.12f), dark);
            MakePart("ArmUpperL", PrimitiveType.Capsule, new Vector3(-0.22f, 0.15f, 0.3f), new Vector3(0.08f, 0.2f, 0.08f),
                body, Quaternion.Euler(-10f, 0f, 20f));
            MakePart("ArmUpperR", PrimitiveType.Capsule, new Vector3(0.22f, 0.15f, 0.3f), new Vector3(0.08f, 0.2f, 0.08f),
                body, Quaternion.Euler(-10f, 0f, -20f));
            MakePart("ArmLowerL", PrimitiveType.Capsule, new Vector3(-0.28f, 0.28f, 0.42f), new Vector3(0.06f, 0.18f, 0.06f),
                body, Quaternion.Euler(-40f, 0f, 15f));
            MakePart("ArmLowerR", PrimitiveType.Capsule, new Vector3(0.28f, 0.28f, 0.42f), new Vector3(0.06f, 0.18f, 0.06f),
                body, Quaternion.Euler(-40f, 0f, -15f));
            MakePart("ClawL", PrimitiveType.Cube, new Vector3(-0.3f, 0.38f, 0.55f), new Vector3(0.05f, 0.18f, 0.04f), dark);
            MakePart("ClawR", PrimitiveType.Cube, new Vector3(0.3f, 0.38f, 0.55f), new Vector3(0.05f, 0.18f, 0.04f), dark);
            Color wingFold = new Color(body.r * 0.7f, body.g * 0.8f, body.b * 0.6f, 0.5f);
            MakePart("WingFoldL", PrimitiveType.Cube, new Vector3(-0.08f, 0.12f, -0.2f), new Vector3(0.15f, 0.01f, 0.4f), wingFold);
            MakePart("WingFoldR", PrimitiveType.Cube, new Vector3(0.08f, 0.12f, -0.2f), new Vector3(0.15f, 0.01f, 0.4f), wingFold);
            MakeLegs(dark, 2, -0.15f);
            MakeAntennae(dark, 0.3f);
            MakeEyes(0.3f, 0.22f);
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
            MakeWing("WingL", new Vector3(-0.45f, 0.1f, 0.15f), new Vector3(0.7f, 0.01f, 0.15f), wingCol);
            MakeWing("WingR", new Vector3(0.45f, 0.1f, 0.15f), new Vector3(0.7f, 0.01f, 0.15f), wingCol);
            MakePart("VeinFL", PrimitiveType.Cylinder, new Vector3(-0.45f, 0.11f, 0.15f), new Vector3(0.01f, 0.01f, 0.13f), veinCol,
                Quaternion.Euler(0f, 0f, 85f));
            MakePart("VeinFR", PrimitiveType.Cylinder, new Vector3(0.45f, 0.11f, 0.15f), new Vector3(0.01f, 0.01f, 0.13f), veinCol,
                Quaternion.Euler(0f, 0f, -85f));
            MakePart("WingLB", PrimitiveType.Cube, new Vector3(-0.4f, 0.08f, -0.05f), new Vector3(0.6f, 0.01f, 0.13f), wingCol);
            MakePart("WingRB", PrimitiveType.Cube, new Vector3(0.4f, 0.08f, -0.05f), new Vector3(0.6f, 0.01f, 0.13f), wingCol);
            MakePart("VeinBL", PrimitiveType.Cylinder, new Vector3(-0.4f, 0.09f, -0.05f), new Vector3(0.01f, 0.01f, 0.11f), veinCol,
                Quaternion.Euler(0f, 0f, 85f));
            MakePart("VeinBR", PrimitiveType.Cylinder, new Vector3(0.4f, 0.09f, -0.05f), new Vector3(0.01f, 0.01f, 0.11f), veinCol,
                Quaternion.Euler(0f, 0f, -85f));
            MakePart("EyeL", PrimitiveType.Sphere, new Vector3(-0.15f, 0.15f, 0.4f), Vector3.one * 0.16f, new Color(0.2f, 0.8f, 0.3f));
            MakePart("EyeR", PrimitiveType.Sphere, new Vector3(0.15f, 0.15f, 0.4f), Vector3.one * 0.16f, new Color(0.2f, 0.8f, 0.3f));
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
            MakePart("MandibleL", PrimitiveType.Cube, new Vector3(-0.08f, 0f, 0.45f), new Vector3(0.06f, 0.04f, 0.1f), body);
            MakePart("MandibleR", PrimitiveType.Cube, new Vector3(0.08f, 0f, 0.45f), new Vector3(0.06f, 0.04f, 0.1f), body);
            MakeLegs(dark, 3, -0.05f);
            MakePart("ElbowAntL", PrimitiveType.Capsule, new Vector3(-0.1f, 0.2f, 0.45f), new Vector3(0.03f, 0.15f, 0.03f),
                dark, Quaternion.Euler(-50f, 0f, 15f));
            MakePart("ElbowAntR", PrimitiveType.Capsule, new Vector3(0.1f, 0.2f, 0.45f), new Vector3(0.03f, 0.15f, 0.03f),
                dark, Quaternion.Euler(-50f, 0f, -15f));
            MakePart("ElbowAntL2", PrimitiveType.Capsule, new Vector3(-0.14f, 0.35f, 0.52f), new Vector3(0.025f, 0.12f, 0.025f),
                dark, Quaternion.Euler(-10f, 0f, 5f));
            MakePart("ElbowAntR2", PrimitiveType.Capsule, new Vector3(0.14f, 0.35f, 0.52f), new Vector3(0.025f, 0.12f, 0.025f),
                dark, Quaternion.Euler(-10f, 0f, -5f));
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
            Color gloss = new Color(Mathf.Min(1, body.r + 0.15f), Mathf.Min(1, body.g + 0.1f), body.b * 0.8f);
            MakePart("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.9f, 0.55f, 1.1f), body);
            MakePart("Shell", PrimitiveType.Sphere, new Vector3(0f, 0.2f, -0.05f), new Vector3(0.82f, 0.3f, 0.95f), dark);
            MakePart("ShellGloss", PrimitiveType.Sphere, new Vector3(0f, 0.25f, -0.05f), new Vector3(0.7f, 0.12f, 0.8f),
                new Color(1f, 1f, 1f, 0.12f));
            MakePart("Head", PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0.6f), new Vector3(0.55f, 0.45f, 0.5f), dark);
            MakePart("HornMain", PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0.7f), new Vector3(0.1f, 0.35f, 0.1f), body,
                Quaternion.Euler(25f, 0f, 0f));
            // 3분절 곡선으로 뿔이 부드럽게 휨(옛 직선 실린더 2개 = 뚝뚝 끊김)
            MakePart("HornCurve", PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0.82f), new Vector3(0.08f, 0.18f, 0.08f), body,
                Quaternion.Euler(40f, 0f, 0f));
            MakePart("HornMid", PrimitiveType.Sphere, new Vector3(0f, 0.62f, 0.88f), Vector3.one * 0.09f, body);
            MakePart("HornTip", PrimitiveType.Cylinder, new Vector3(0f, 0.72f, 0.96f), new Vector3(0.06f, 0.13f, 0.06f), gloss,
                Quaternion.Euler(52f, 0f, 0f));
            // 끝 분기(Y자 뿔) — 장수풍뎅이 시그니처 실루엣
            MakePart("HornForkL", PrimitiveType.Cylinder, new Vector3(-0.05f, 0.78f, 1.0f), new Vector3(0.04f, 0.1f, 0.04f), gloss,
                Quaternion.Euler(50f, 0f, 12f));
            MakePart("HornForkR", PrimitiveType.Cylinder, new Vector3(0.05f, 0.78f, 1.0f), new Vector3(0.04f, 0.1f, 0.04f), gloss,
                Quaternion.Euler(50f, 0f, -12f));
            MakePart("HornSmall", PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0.55f), new Vector3(0.07f, 0.15f, 0.07f), dark,
                Quaternion.Euler(15f, 0f, 0f));
            MakePart("ClawL", PrimitiveType.Cube, new Vector3(-0.3f, -0.25f, 0.3f), new Vector3(0.06f, 0.08f, 0.12f), dark);
            MakePart("ClawR", PrimitiveType.Cube, new Vector3(0.3f, -0.25f, 0.3f), new Vector3(0.06f, 0.08f, 0.12f), dark);
            MakeEyes(0.78f, 0.14f, 0.22f);
            MakeLegs(dark, 3, 0f);
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
            MakePart("WingLB", PrimitiveType.Cube, new Vector3(-0.28f, 0.06f, -0.08f), new Vector3(0.45f, 0.01f, 0.08f), wingCol);
            MakePart("WingRB", PrimitiveType.Cube, new Vector3(0.28f, 0.06f, -0.08f), new Vector3(0.45f, 0.01f, 0.08f), wingCol);
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

        private void MakeWing(string name, Vector3 pos, Vector3 scale, Color color)
        {
            GameObject wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wing.name = name;
            wing.transform.SetParent(transform, false);
            wing.transform.localPosition = pos;
            wing.transform.localScale = scale;
            Collider col = wing.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            ApplyColor(wing, color);
        }

        private void MakeLegs(Color color, int pairs, float zOffset)
        {
            Color joint = new Color(color.r * 0.7f + 0.03f, color.g * 0.7f + 0.03f, color.b * 0.7f + 0.03f);
            for (int i = 0; i < pairs; i++)
            {
                float z = zOffset + (i - (pairs - 1) * 0.5f) * 0.2f;
                // 앞·중·뒷다리 각도 변주(기계적 동일각 해소) + z 부채꼴 펼침
                float zSpread = (i - (pairs - 1) * 0.5f) * 0.04f;
                float upAng = 26f + i * 4f;
                float loAng = 8f + i * 3f;
                // 대퇴 (상단)
                MakePart($"LegUL{i}", PrimitiveType.Capsule, new Vector3(-0.22f, -0.1f, z + zSpread),
                    new Vector3(0.055f, 0.12f, 0.055f), color, Quaternion.Euler(0f, 0f, upAng));
                MakePart($"LegUR{i}", PrimitiveType.Capsule, new Vector3(0.22f, -0.1f, z + zSpread),
                    new Vector3(0.055f, 0.12f, 0.055f), color, Quaternion.Euler(0f, 0f, -upAng));
                // 관절
                MakePart($"KneeL{i}", PrimitiveType.Sphere, new Vector3(-0.3f, -0.2f, z + zSpread),
                    Vector3.one * 0.05f, joint);
                MakePart($"KneeR{i}", PrimitiveType.Sphere, new Vector3(0.3f, -0.2f, z + zSpread),
                    Vector3.one * 0.05f, joint);
                // 경절 (하단)
                MakePart($"LegLL{i}", PrimitiveType.Capsule, new Vector3(-0.32f, -0.3f, z + zSpread),
                    new Vector3(0.038f, 0.12f, 0.038f), color, Quaternion.Euler(0f, 0f, loAng));
                MakePart($"LegLR{i}", PrimitiveType.Capsule, new Vector3(0.32f, -0.3f, z + zSpread),
                    new Vector3(0.038f, 0.12f, 0.038f), color, Quaternion.Euler(0f, 0f, -loAng));
                // 발끝(tarsus) — 접지감(옛엔 발끝 없어 공중에 뜬 느낌)
                MakePart($"FootL{i}", PrimitiveType.Sphere, new Vector3(-0.345f, -0.4f, z + zSpread),
                    Vector3.one * 0.03f, joint);
                MakePart($"FootR{i}", PrimitiveType.Sphere, new Vector3(0.345f, -0.4f, z + zSpread),
                    Vector3.one * 0.03f, joint);
            }
        }

        private void MakeAntennae(Color color, float zBase, bool feathered = false)
        {
            // 2분절 굴절로 부드러운 S곡선(옛 직선 캡슐 1개 = 막대기 느낌 해소).
            MakePart("AntBaseL", PrimitiveType.Capsule, new Vector3(-0.1f, 0.18f, zBase + 0.13f),
                new Vector3(0.03f, 0.14f, 0.03f), color, Quaternion.Euler(-38f, 0f, 16f));
            MakePart("AntBaseR", PrimitiveType.Capsule, new Vector3(0.1f, 0.18f, zBase + 0.13f),
                new Vector3(0.03f, 0.14f, 0.03f), color, Quaternion.Euler(-38f, 0f, -16f));
            MakePart("AntMidL", PrimitiveType.Capsule, new Vector3(-0.15f, 0.36f, zBase + 0.2f),
                new Vector3(0.025f, 0.12f, 0.025f), color, Quaternion.Euler(-10f, 0f, 8f));
            MakePart("AntMidR", PrimitiveType.Capsule, new Vector3(0.15f, 0.36f, zBase + 0.2f),
                new Vector3(0.025f, 0.12f, 0.025f), color, Quaternion.Euler(-10f, 0f, -8f));
            float tipScale = feathered ? 0.08f : 0.055f;
            MakePart("AntTipL", PrimitiveType.Sphere, new Vector3(-0.17f, 0.46f, zBase + 0.23f), Vector3.one * tipScale, color);
            MakePart("AntTipR", PrimitiveType.Sphere, new Vector3(0.17f, 0.46f, zBase + 0.23f), Vector3.one * tipScale, color);
            if (feathered)
            {
                // 나방/모기 깃털 더듬이 — 끝에 양옆 작은 깃
                MakePart("AntFeatherL", PrimitiveType.Cube, new Vector3(-0.16f, 0.40f, zBase + 0.22f),
                    new Vector3(0.07f, 0.012f, 0.03f), color, Quaternion.Euler(0f, 0f, 20f));
                MakePart("AntFeatherR", PrimitiveType.Cube, new Vector3(0.16f, 0.40f, zBase + 0.22f),
                    new Vector3(0.07f, 0.012f, 0.03f), color, Quaternion.Euler(0f, 0f, -20f));
            }
        }

        private void MakeEyes(float zPos, float size, float xSpread = 0.12f)
        {
            MakePart("EyeL", PrimitiveType.Sphere, new Vector3(-xSpread, 0.15f, zPos), Vector3.one * size, Color.white);
            MakePart("EyeR", PrimitiveType.Sphere, new Vector3(xSpread, 0.15f, zPos), Vector3.one * size, Color.white);
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

        // 곤충 등껍질 상단 흰색 반투명 글로스 — 입체 광택(딱정벌레/풍뎅이류 1줄 호출).
        private void MakeTopGloss(Vector3 bodyCenter, Vector3 bodyScale, float intensity = 0.14f)
        {
            MakePart("TopGloss", PrimitiveType.Sphere,
                bodyCenter + new Vector3(0f, bodyScale.y * 0.35f, bodyScale.z * 0.05f),
                new Vector3(bodyScale.x * 0.7f, bodyScale.y * 0.18f, bodyScale.z * 0.75f),
                new Color(1f, 1f, 1f, intensity));
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
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            Material mat = new Material(shader);
            mat.color = color;
            // PBR 광택: 옛 ApplyColor는 색만 칠해 전 곤충이 무광 점토처럼 보였음(품질 저하 핵심).
            // Standard/URP Lit에서만 _Glossiness/_Metallic 설정(Unlit/Sprites fallback은 프로퍼티 없어 가드).
            bool pbr = shader.name == "Standard" || shader.name.Contains("Lit");
            if (color.a < 1f)
            {
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                // 날개/반투명: 막·천 느낌(번들거림 억제)
                if (pbr) { mat.SetFloat("_Glossiness", 0.2f); mat.SetFloat("_Smoothness", 0.2f); mat.SetFloat("_Metallic", 0f); }
            }
            else if (pbr)
            {
                // 외골격 키틴 광택 + 미세 금속감 — 전 34종 동시 개선
                mat.SetFloat("_Glossiness", 0.55f);
                mat.SetFloat("_Smoothness", 0.55f);
                mat.SetFloat("_Metallic", 0.15f);
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
            // 다중 호출 가드 — Battle/Capture가 동시에 Despawn 호출 시 풀 중복 반환 차단.
            // 옛은 onDespawn 두 번 발화 → 풀이 같은 객체 두 번 Return → 다음 Get에서 같은 인스턴스 2번 회귀.
            if (despawnedThisCycle) return;
            despawnedThisCycle = true;

            // 풀 반환 전 진행 중 코루틴 정리 (다음 인스턴스 사용 시 잔존 영향 방지)
            StopAllCoroutines();
            if (ownerPoint != null)
                ownerPoint.NotifyDespawned();
            onDespawn?.Invoke(this);
        }
    }
}
