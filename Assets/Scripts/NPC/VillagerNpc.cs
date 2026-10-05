using InsectGame.Core;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.NPC
{
    /// <summary>
    /// 머리 위 표식 — 마을 이야기 주민과, 본편이 지금 가리키는 <b>한 사람</b>에게 붙는다
    /// (<c>StoryObjectiveTracker</c>가 정한다).
    /// </summary>
    public enum QuestMark
    {
        None,
        /// <summary>말을 걸면 새 이야기가 나온다(<c>!</c>).</summary>
        New,
        /// <summary>의뢰를 끝냈으니 알리러 오면 된다(<c>?</c>).</summary>
        Report,
        /// <summary>본편이 지금 이 사람에게 말을 걸라고 한다(<c>!</c>, 민트) — 한 번에 한 명뿐이다.</summary>
        Main,
    }

    /// <summary>
    /// 마을 주민 NPC — Idle(2~6s) ⇄ Wander(앵커 wanderRadius, 속도 1.8) + 대화/연출 상태.
    /// 개별 Update 없음: NpcManager가 TickAI(라운드로빈)/TickMovement(40m 이내 매 프레임)를 호출.
    ///
    /// <b>Scripted</b>는 스토리가 이 NPC를 배우로 부리는 상태다(조우 접근·등장·퇴장).
    /// 이동·지면 샘플·벽 판정·회전이 전부 여기 이미 있어서 상태 하나만 늘렸다 —
    /// 별도 컴포넌트로 복제하면 두 벌이 조용히 어긋난다.
    /// </summary>
    public class VillagerNpc : MonoBehaviour
    {
        private enum State { Idle, Wander, Talking, Scripted }

        /// <summary>이동 시도의 결과 — Wander와 Scripted가 같은 이동 헬퍼를 공유한다.</summary>
        private enum MoveResult { Moving, Arrived, Blocked }

        private const float MoveSpeed = 1.8f;
        /// <summary>연출 이동 속도 — 배회보다 빠르다. 다가오는 인상은 속도가 만든다.</summary>
        private const float ScriptedMoveSpeed = 3.2f;
        /// <summary>
        /// 연출 이동 하드 타임아웃(초). <b>이게 없으면 스토리가 영구 정지한다</b> —
        /// 벽에 갇히거나 목표가 닿을 수 없는 곳이면 도착 판정이 영영 안 오고,
        /// onArrive를 기다리는 연출은 다음 스텝으로 못 넘어간다.
        /// </summary>
        private const float ScriptedTimeoutSeconds = 8f;
        private const float AiInterval = 0.3f;
        private const float ArriveDistance = 0.3f;
        private const float TurnSpeed = 540f;
        // 지면 스텝 클램프 — 이보다 큰 Y 급변(건물 지붕/상판)은 지면으로 인정하지 않음
        private const float MaxGroundStep = 0.75f;

        private string npcId;
        private string displayName;
        private string storyNpcId;   // 비어있으면 일반 주민, 채워지면 스토리 NPC(대화 시 스토리 발동)
        private string regionId;
        private Vector3 anchorPosition;
        private float wanderRadius = 8f;

        private State state = State.Idle;
        private float stateEndTime;
        private Vector3 wanderTarget;
        private float groundY;
        private float lastAiTime = float.MinValue;
        private System.Random rng;
        private NpcWalkAnimator animator;

        // ── 연출 이동(Scripted) ──
        private Vector3 scriptedTarget;
        private float scriptedArriveRadius;
        private System.Action scriptedOnArrive;
        private float scriptedDeadline;

        public string NpcId => npcId;
        public string DisplayName => displayName;
        public string RegionId => regionId;
        public bool IsTalking => state == State.Talking;
        /// <summary>스토리 연출이 이 NPC를 움직이는 중인가.</summary>
        public bool IsScripted => state == State.Scripted;
        /// <summary>스폰 앵커 위치 — 연출이 끝난 뒤 제자리로 돌려보낼 때 쓴다.</summary>
        public Vector3 AnchorPosition => anchorPosition;

        /// <summary>스토리 NPC 식별자(village_elder 등). 일반 주민이면 빈 문자열.</summary>
        public string StoryNpcId => storyNpcId;
        public bool IsStoryNpc => !string.IsNullOrEmpty(storyNpcId);

        /// <summary>대화 가능 여부 — 이미 대화 중이 아니고 활성 상태일 때.</summary>
        public bool CanTalk => state != State.Talking && isActiveAndEnabled;

        // ── 머리 위 의뢰 표식 ──
        // 월드 오브젝트라 UITheme(UI 모듈)를 끌어오지 않는다 — 색은 지도·미니맵 배지와 같은 계열
        // (호박=새 이야기, 민트=보고)로 맞춘 리터럴이다.
        private const float QuestMarkHeight = 2.35f;
        // !·? 한 색 — 지도·미니맵에서 ?를 민트로 두었더니 이야기 목표·서브에리어 입구(민트)와 섞였다.
        // 세 곳(머리 위·지도·미니맵)이 같은 색·같은 기호를 쓰고, 새 이야기와 보고는 기호로 가른다.
        private static readonly Color QuestMarkColor = new Color(1f, 0.8f, 0.25f);
        // 본편 표식은 지도의 이야기 목표 마커와 같은 민트 계열이다. 지도 토큰(#4FC98A) 그대로면 초원 풀색과
        // 붙어 버려서 흰 쪽으로 많이 띄웠고, 의뢰 표식보다 크다 — "지금 가야 할 한 사람"이 여러 !들 사이에서
        // 먼저 보여야 한다.
        private static readonly Color MainQuestMarkColor = new Color(0.72f, 1f, 0.86f);
        private const float QuestMarkSize = 0.12f;
        private const float MainQuestMarkSize = 0.16f;
        // Camera.main은 호출마다 태그 검색이다 — InsectEntity의 이름표와 같은 이유로 정적 캐시.
        private static Camera questMarkCamera;
        private QuestMark questMark = QuestMark.None;
        private TextMesh questMarkText;
        // 표식은 **몸이 보일 때만** 보인다. DistanceCulling은 Awake에 몸 렌더러만 캐시해서 나중에 만든
        // 표식을 모른다 — 그대로 두면 컬링된(보이지 않는) NPC 위에 !만 떠 있고, TickMovement 밖(40m+)에선
        // 카메라를 향하는 갱신도 멈춰 옆으로 누운 글자가 된다. 그래서 몸 렌더러 하나를 따라 켜고 끈다.
        private Renderer questMarkRenderer;
        private Renderer questMarkBodyRenderer;

        /// <summary>
        /// 머리 위 표식을 바꾼다. 같은 값이면 아무것도 안 한다(트래커가 0.5초마다 부른다).
        /// 표식은 처음 필요할 때 만든다 — 일반 주민·동행자에게는 끝내 생기지 않는다.
        /// </summary>
        public void SetQuestMark(QuestMark mark)
        {
            if (mark == questMark) return;
            questMark = mark;

            if (mark == QuestMark.None)
            {
                if (questMarkText != null) questMarkText.gameObject.SetActive(false);
                return;
            }

            if (questMarkText == null) questMarkText = CreateQuestMark();
            bool main = mark == QuestMark.Main;
            questMarkText.text = mark == QuestMark.Report ? "?" : "!";
            questMarkText.color = main ? MainQuestMarkColor : QuestMarkColor;
            questMarkText.characterSize = main ? MainQuestMarkSize : QuestMarkSize;
            questMarkText.gameObject.SetActive(true);
            SyncQuestMarkVisibility();   // 멀리서(컬링 중) 켜질 수 있다 — 틱을 기다리지 않고 바로 맞춘다
        }

        private void SyncQuestMarkVisibility()
        {
            if (questMarkRenderer == null) return;
            questMarkRenderer.enabled = questMarkBodyRenderer == null || questMarkBodyRenderer.enabled;
        }

        private TextMesh CreateQuestMark()
        {
            // 표식보다 **먼저** 몸 렌더러를 잡는다 — 뒤에 잡으면 표식 자신이 걸린다.
            questMarkBodyRenderer = GetComponentInChildren<Renderer>();
            GameObject go = new GameObject("QuestMark");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, QuestMarkHeight, 0f);
            TextMesh text = go.AddComponent<TextMesh>();
            text.characterSize = QuestMarkSize;
            text.fontSize = 96;
            text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            questMarkRenderer = go.GetComponent<Renderer>();   // TextMesh가 MeshRenderer를 함께 붙인다
            return text;
        }

        // 카메라를 향하고 살짝 떠오르내린다. TickMovement(플레이어 40m 이내)에서만 불리므로
        // 멀리 있는 표식은 갱신하지 않는다 — 어차피 안 보인다.
        private void TickQuestMark(float time)
        {
            if (questMarkText == null || questMark == QuestMark.None) return;
            SyncQuestMarkVisibility();
            if (questMarkCamera == null) questMarkCamera = Camera.main;
            Transform mt = questMarkText.transform;
            if (questMarkCamera != null) mt.rotation = questMarkCamera.transform.rotation;
            mt.localPosition = new Vector3(0f, QuestMarkHeight + Mathf.Sin(time * 3f) * 0.08f, 0f);
        }

        /// <summary>NpcManager가 스폰 직후 호출. 시각 모델은 NpcVisualBuilder.Build로 이미 생성된 상태.</summary>
        public void Initialize(NpcSpawnAnchor anchor, string id, string name, int seed, string storyId = null)
        {
            npcId = id;
            displayName = name;
            storyNpcId = storyId;
            regionId = anchor != null ? anchor.regionId : string.Empty;
            anchorPosition = anchor != null ? anchor.position : transform.position;
            wanderRadius = anchor != null ? anchor.wanderRadius : 8f;
            rng = new System.Random(seed);
            animator = new NpcWalkAnimator(transform);
            groundY = transform.position.y;
            // 고정 배치(wanderRadius 0 — 스토리 NPC·전초기지)는 한 번도 걷지 않아 이 자리 높이가 끝까지 간다
            StandOnGround();
            state = State.Idle;
            stateEndTime = 0f; // 첫 TickAI에서 즉시 새 Idle 타이머 시작
        }

        /// <summary>
        /// NPC 발 높이(월드 y) — 레이가 맞은 콜라이더 윗면(<paramref name="colliderY"/>, <c>groundY</c>)에 둔덕을 얹는다.
        /// 둔덕(사구·재 더미·초원 언덕 …)은 콜라이더가 없어 레이로 못 보므로 <see cref="FieldGround"/>에 묻는다.
        ///
        /// <c>Max(콜라이더, min(콜라이더, 바닥) + 둔덕 높이)</c>인 이유:
        /// <list type="bullet">
        ///   <item><b>둔덕이 없으면(LiftAt 0) 콜라이더 그대로</b> — 마을·서브에리어·평지는 옛 높이에서 한 치도 안 바뀐다
        ///     (플레이어처럼 <c>Max(콜라이더, SurfaceY)</c>로 두면 리전 평면 0.08에 서 있던 NPC가 전부 2cm 오른다).</item>
        ///   <item>리전 평면(바닥 이하) 위라면 평면 + 둔덕 높이 — 둔덕 윗면을 따라 오르내린다.</item>
        ///   <item>바닥보다 높은 콜라이더(바위·다리) 위라면 그 윗면과 둔덕 윗면 중 높은 쪽 — 둔덕 위 바위에서 둔덕 높이를
        ///     한 번 더 얹어 뜨지 않는다(플레이어 접지 <c>PlayerMovement.GroundHeight</c>와 같은 답).</item>
        /// </list>
        /// 잡기 아이(<see cref="CatcherKidNpc"/>)도 이 식을 쓴다 — 사구 위 곤충을 쫓아 올라간다.
        /// </summary>
        internal static float StandHeight(float colliderY, float x, float z)
            => Mathf.Max(colliderY, Mathf.Min(colliderY, FieldGround.FloorY) + FieldGround.LiftAt(x, z));

        private void StandOnGround()
        {
            Vector3 pos = transform.position;
            pos.y = StandHeight(groundY, pos.x, pos.z);
            transform.position = pos;
        }

        /// <summary>대화 시작 — 정지 + 플레이어 방향 바라봄. NpcDialogueUI.Show가 호출.</summary>
        public void BeginTalk(Transform player)
        {
            // 걸어오는 도중에도 말을 걸 수 있다. 연출 콜백을 먼저 소진해야(삼키면) 그 연출이
            // 다음 스텝으로 못 넘어가 멈춘다 — 이동을 중단하되 약속은 지킨다.
            if (state == State.Scripted) CompleteScripted();
            state = State.Talking;
            if (player != null) FaceTowards(player.position);
        }

        /// <summary>대화 종료 — Idle 복귀. NpcDialogueUI.CloseModal이 호출.</summary>
        public void EndTalk()
        {
            if (state != State.Talking) return;
            state = State.Idle;
            stateEndTime = Time.time + RandomRange(2f, 6f);
        }

        /// <summary>플레이어를 향해 돌아봄(상태 변경 없음) — 스토리 NPC 조우 연출용.
        /// 스토리 발동은 모달만 뜨고 Talking 상태로 안 넣으므로, 최소한 시선은 맞춘다.</summary>
        public void FacePlayer(Transform player)
        {
            if (player != null) FaceTowards(player.position);
        }

        // ── 연출 이동 API (StoryStageDirector가 호출) ──

        /// <summary>
        /// 지정 좌표까지 걸어간다. 도착하거나 <see cref="ScriptedTimeoutSeconds"/>가 지나면
        /// <paramref name="onArrive"/>를 <b>정확히 한 번</b> 부른다.
        ///
        /// <b>콜백은 어떤 경로로든 반드시 불린다</b> — 대화 중이라 못 움직여도, 이전 명령을
        /// 덮어써도, 벽에 막혀도. 호출부(연출 재생기)가 이걸 기다리므로 삼키면 그 자리에서 멈춘다.
        /// </summary>
        public void BeginScriptedMove(Vector3 worldTarget, float arriveRadius, System.Action onArrive = null)
        {
            // 앞선 명령이 남아 있으면 그 약속부터 지운다(콜백 소진).
            CompleteScripted();

            if (state == State.Talking)
            {
                // 대화 중엔 움직이지 않는다. 그래도 연출은 흘러가야 한다.
                onArrive?.Invoke();
                return;
            }

            scriptedTarget = worldTarget;
            scriptedArriveRadius = Mathf.Max(0.2f, arriveRadius);
            scriptedOnArrive = onArrive;
            scriptedDeadline = Time.time + ScriptedTimeoutSeconds;
            state = State.Scripted;
        }

        /// <summary>스폰 앵커로 되돌아간다 — 연출이 끝난 NPC가 플레이어를 따라 떠돌지 않게.</summary>
        public void BeginScriptedReturn(System.Action onArrive = null)
        {
            BeginScriptedMove(anchorPosition, 0.4f, onArrive);
        }

        /// <summary>
        /// 즉시 배치 — 등장 연출이 배우를 무대 밖에 세울 때, 그리고 건너뛰기가 최종 자리로
        /// 보낼 때 쓴다. <b>지면을 다시 잡는다</b>: 평소의 <see cref="SampleGround"/>는 지붕 오인을
        /// 막으려 이전 groundY에서 0.75m 이상 벗어난 값을 거부하는데, 먼 곳으로 옮긴 직후엔
        /// 그 이전 값이 무의미해서 그대로 두면 NPC가 공중이나 땅속에 박힌다.
        /// </summary>
        public void WarpTo(Vector3 worldPosition, Transform lookAt = null)
        {
            StopScripted();

            // **지붕 가드는 여기서도 필요하다.** 위 주석대로 이전 groundY 기준 클램프는 쓸 수 없지만,
            // 그렇다고 첫 히트를 무조건 받으면 안 된다 — 집·상점·병원·회관 몸통은 콜라이더를 남기므로
            // (`VillageBuilder`, 지붕만 제거) 목적지가 건물 발자국 안이면 상판 y=2.8~3.8m가 잡힌다.
            // 그 뒤로는 `SampleGround`의 ±0.75m 클램프가 **지상 복귀를 영구히 거부**해서 자가 회복이
            // 불가능하다(라온의 등장 워프 좌표가 실제로 그 입력을 만든다).
            // 기준은 요청 좌표의 y다 — 호출부가 주는 오프셋은 지면 높이를 뜻한다.
            if (Physics.Raycast(worldPosition + Vector3.up * 5f, Vector3.down,
                    out RaycastHit hit, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(transform)
                && hit.point.y <= worldPosition.y + MaxGroundStep)
            {
                groundY = hit.point.y;
            }
            else
            {
                groundY = worldPosition.y;
            }

            // 레이는 둔덕을 못 본다(콜라이더 없음) — 콜라이더 높이에 둔덕을 얹는다(StandHeight). groundY는 콜라이더 값으로
            // 남겨 둔다: SampleGround의 ±0.75m 클램프가 그걸 기준으로 다음 레이를 받는다.
            transform.position = new Vector3(worldPosition.x, StandHeight(groundY, worldPosition.x, worldPosition.z), worldPosition.z);
            if (lookAt != null) FaceTowards(lookAt.position);
        }

        /// <summary>연출 이동을 즉시 끝낸다(건너뛰기 등). 대기 중인 콜백은 그대로 불린다.</summary>
        public void StopScripted()
        {
            if (state != State.Scripted) return;
            CompleteScripted();
        }

        /// <summary>몸짓 1회 재생 — 애니메이터로 위임.</summary>
        public void PlayGesture(NpcGesture gesture)
        {
            if (animator != null) animator.PlayGesture(gesture);
        }

        /// <summary>몸짓 재생 중인가 — 연출이 다음 스텝으로 넘어갈 시점 판단.</summary>
        public bool IsGesturing => animator != null && animator.IsGesturing;

        /// <summary>
        /// 대기 중인 연출 콜백을 <b>한 번만</b> 부르고 Idle로 되돌린다. 두 번 불려도 안전하다.
        /// 콜백을 먼저 비우는 이유는 재진입 방어다 — 콜백 안에서 다시 BeginScriptedMove가
        /// 불려도 방금 지운 약속이 두 번 불리지 않는다.
        /// </summary>
        private void CompleteScripted()
        {
            System.Action callback = scriptedOnArrive;
            scriptedOnArrive = null;
            if (state == State.Scripted)
            {
                state = State.Idle;
                stateEndTime = Time.time + RandomRange(2f, 6f);
            }
            callback?.Invoke();
        }

        /// <summary>상태 결정 틱 — NpcManager 라운드로빈(프레임당 최대 3명). 내부 0.3s 주기 자체 스로틀.</summary>
        public void TickAI(float time)
        {
            if (time - lastAiTime < AiInterval) return;
            lastAiTime = time;
            if (rng == null) return; // Initialize 전 방어

            // 지면 Y 샘플 — tick당 1회 (필드 평탄이라 실패 시 기존 값 유지)
            SampleGround();

            switch (state)
            {
                case State.Idle:
                    // wanderRadius 0은 고정 배치(스토리 NPC·전초기지)다. 배회로 전이해 봐야
                    // 목적지가 제자리라 즉시 되돌아오므로 아예 들어가지 않는다.
                    if (time >= stateEndTime && wanderRadius > 0.1f)
                    {
                        wanderTarget = PickWanderTarget();
                        state = State.Wander;
                        stateEndTime = time + 10f; // Wander 안전 타임아웃 (40m 밖 미이동 NPC 영구 Wander 방지)
                    }
                    break;

                case State.Wander:
                    if (time >= stateEndTime)
                    {
                        state = State.Idle;
                        stateEndTime = time + RandomRange(2f, 6f);
                    }
                    break;

                case State.Talking:
                    // NpcDialogueUI가 EndTalk로 해제 — 여기선 대기만
                    break;

                case State.Scripted:
                    // **타임아웃을 여기서 본다.** TickMovement는 플레이어 40m 이내에서만 도는데,
                    // 연출 이동 중에 플레이어가 멀어지면 도착 판정이 영영 안 온다. TickAI는
                    // 거리와 무관하게 라운드로빈으로 돌므로 약속을 지킬 수 있는 유일한 자리다.
                    if (time >= scriptedDeadline) CompleteScripted();
                    break;
            }
        }

        /// <summary>이동/애니 틱 — 플레이어 40m 이내에서만 매 프레임 호출.</summary>
        public void TickMovement(float dt, float time)
        {
            TickQuestMark(time);
            if (animator == null) return;

            bool walking = false;
            if (state == State.Wander)
            {
                MoveResult result = MoveTowards(wanderTarget, ArriveDistance, MoveSpeed, dt);
                // 도착했거나 건물 벽 등에 막혔으면 목적지를 포기하고 Idle 복귀(다음 배회 때 재추첨).
                if (result == MoveResult.Moving) walking = true;
                else
                {
                    state = State.Idle;
                    stateEndTime = time + RandomRange(2f, 6f);
                }
            }
            else if (state == State.Scripted)
            {
                MoveResult result = MoveTowards(scriptedTarget, scriptedArriveRadius, ScriptedMoveSpeed, dt);
                if (result == MoveResult.Arrived) CompleteScripted();
                else walking = true;
                // Blocked여도 포기하지 않는다 — 배회와 다른 점이다. 사람이나 곤충이 잠깐 앞을
                // 막았을 수 있어 계속 밀어 본다. 정말 못 가면 TickAI의 타임아웃이 끝낸다.
            }

            animator.Tick(time, dt, walking);
        }

        /// <summary>
        /// 목표 쪽으로 한 스텝. Wander와 Scripted가 공유한다 — 지면 고정·벽 판정·회전이
        /// 두 벌로 갈라지면 한쪽만 고쳐져 조용히 어긋난다.
        /// </summary>
        private MoveResult MoveTowards(Vector3 target, float arriveDistance, float speed, float dt)
        {
            Vector3 to = target - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist <= arriveDistance) return MoveResult.Arrived;

            Vector3 dir = to / dist;
            float step = speed * dt;
            if (IsBlockedAhead(dir, step)) return MoveResult.Blocked;

            Vector3 pos = transform.position + dir * step;
            pos.y = StandHeight(groundY, pos.x, pos.z);   // groundY(콜라이더)는 0.3초마다, 둔덕은 매 스텝 — 사구를 따라 걷는다
            transform.position = pos;
            RotateTowards(dir, dt);
            return MoveResult.Moving;
        }

        // 물을 피해 다시 뽑는 횟수 — 다 실패하면 제자리(앵커)로 돌아간다.
        private const int WanderWaterRetries = 6;

        private Vector3 PickWanderTarget()
        {
            // 연못 호수를 키운 뒤로 물가 주민의 배회 원이 물에 걸친다 — 목적지도, 가는 길도 물이면 다시 뽑는다
            // (물 판정은 RegionDressingBuilder가 실제로 그린 물이 단일 출처. 곤충 스폰과 같은 판정이다).
            for (int attempt = 0; attempt < WanderWaterRetries; attempt++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                float radius = (float)(rng.NextDouble()) * wanderRadius;
                var target = new Vector3(
                    anchorPosition.x + Mathf.Sin(angle) * radius,
                    groundY,
                    anchorPosition.z + Mathf.Cos(angle) * radius);
                if (!PathTouchesWater(transform.position, target)) return target;
            }
            return new Vector3(anchorPosition.x, groundY, anchorPosition.z);
        }

        // 곧게 걸어가는 길을 네 토막으로 짚는다 — 배회 반경(8m 안팎)에선 2m 간격이면 웅덩이를 건너뛰지 않는다.
        private static bool PathTouchesWater(Vector3 from, Vector3 to)
        {
            for (int i = 1; i <= 4; i++)
                if (RegionDressingBuilder.IsOnWater(Vector3.Lerp(from, to, i / 4f), 0.3f)) return true;
            return false;
        }

        private void SampleGround()
        {
            // 본인 콜라이더는 트리거(NpcVisualBuilder)라 Ignore로 자동 제외
            if (Physics.Raycast(transform.position + Vector3.up * 3f, Vector3.down,
                    out RaycastHit hit, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                // 스텝 클램프: 건물 지붕/상판(급격한 Y 상승)을 지면으로 오인하면 NPC가
                // 지붕 위로 워프한다 — 정상 지형 경사(틱당 이동량 이내)만 수용.
                if (!hit.transform.IsChildOf(transform)
                    && Mathf.Abs(hit.point.y - groundY) <= MaxGroundStep)
                    groundY = hit.point.y;
            }
        }

        /// <summary>진행 방향에 통행 불가 콜라이더(건물 벽 등)가 있는지 — 벽 관통 방지.</summary>
        private bool IsBlockedAhead(Vector3 dir, float step)
        {
            if (!Physics.Raycast(transform.position + Vector3.up * 0.5f, dir,
                    out RaycastHit hit, step + 0.35f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;
            if (hit.transform.IsChildOf(transform)) return false;
            // 곤충 엔티티는 통과 허용 (PlayerMovement.IsBlockedPosition 관례)
            if (hit.collider.GetComponentInParent<InsectEntity>() != null) return false;
            return true;
        }

        private void FaceTowards(Vector3 worldPos)
        {
            Vector3 dir = worldPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0004f)
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        private void RotateTowards(Vector3 dir, float dt)
        {
            Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, TurnSpeed * dt);
        }

        private float RandomRange(float min, float max)
        {
            // Initialize 전에도 불릴 수 있다(연출 콜백 경로) — rng 없으면 하한으로 떨어진다.
            if (rng == null) return min;
            return min + (float)rng.NextDouble() * (max - min);
        }

        private void OnDestroy()
        {
            // NpcVisualBuilder.Build가 만든 인스턴스 머티리얼 정리 (누수 방지)
            NpcVisualBuilder.CleanupMaterials(transform);
        }
    }
}
