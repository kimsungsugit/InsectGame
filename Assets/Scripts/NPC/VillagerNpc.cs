using InsectGame.Core;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.NPC
{
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
            state = State.Idle;
            stateEndTime = 0f; // 첫 TickAI에서 즉시 새 Idle 타이머 시작
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

            if (Physics.Raycast(worldPosition + Vector3.up * 5f, Vector3.down,
                    out RaycastHit hit, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(transform))
            {
                groundY = hit.point.y;
            }
            else
            {
                groundY = worldPosition.y;
            }

            transform.position = new Vector3(worldPosition.x, groundY, worldPosition.z);
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
            pos.y = groundY;
            transform.position = pos;
            RotateTowards(dir, dt);
            return MoveResult.Moving;
        }

        private Vector3 PickWanderTarget()
        {
            float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float radius = (float)(rng.NextDouble()) * wanderRadius;
            return new Vector3(
                anchorPosition.x + Mathf.Sin(angle) * radius,
                groundY,
                anchorPosition.z + Mathf.Cos(angle) * radius);
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
