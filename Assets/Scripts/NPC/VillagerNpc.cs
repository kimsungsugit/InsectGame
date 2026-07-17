using InsectGame.Core;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.NPC
{
    /// <summary>
    /// 마을 주민 NPC — Idle(2~6s) ⇄ Wander(앵커 wanderRadius, 속도 1.8) + 대화 상태.
    /// 개별 Update 없음: NpcManager가 TickAI(라운드로빈)/TickMovement(40m 이내 매 프레임)를 호출.
    /// </summary>
    public class VillagerNpc : MonoBehaviour
    {
        private enum State { Idle, Wander, Talking }

        private const float MoveSpeed = 1.8f;
        private const float AiInterval = 0.3f;
        private const float ArriveDistance = 0.3f;
        private const float TurnSpeed = 540f;
        // 지면 스텝 클램프 — 이보다 큰 Y 급변(건물 지붕/상판)은 지면으로 인정하지 않음
        private const float MaxGroundStep = 0.75f;

        private string npcId;
        private string displayName;
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

        public string NpcId => npcId;
        public string DisplayName => displayName;
        public string RegionId => regionId;
        public bool IsTalking => state == State.Talking;

        /// <summary>대화 가능 여부 — 이미 대화 중이 아니고 활성 상태일 때.</summary>
        public bool CanTalk => state != State.Talking && isActiveAndEnabled;

        /// <summary>NpcManager가 스폰 직후 호출. 시각 모델은 NpcVisualBuilder.Build로 이미 생성된 상태.</summary>
        public void Initialize(NpcSpawnAnchor anchor, string id, string name, int seed)
        {
            npcId = id;
            displayName = name;
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
                    if (time >= stateEndTime)
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
            }
        }

        /// <summary>이동/애니 틱 — 플레이어 40m 이내에서만 매 프레임 호출.</summary>
        public void TickMovement(float dt, float time)
        {
            if (animator == null) return;

            bool walking = false;
            if (state == State.Wander)
            {
                Vector3 to = wanderTarget - transform.position;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist <= ArriveDistance)
                {
                    state = State.Idle;
                    stateEndTime = time + RandomRange(2f, 6f);
                }
                else
                {
                    Vector3 dir = to / dist;
                    if (IsBlockedAhead(dir, MoveSpeed * dt))
                    {
                        // 건물 벽 등 통행 불가 — 목적지 포기하고 Idle 복귀 (다음 배회 때 재추첨)
                        state = State.Idle;
                        stateEndTime = time + RandomRange(2f, 6f);
                    }
                    else
                    {
                        Vector3 pos = transform.position + dir * (MoveSpeed * dt);
                        pos.y = groundY;
                        transform.position = pos;
                        RotateTowards(dir, dt);
                        walking = true;
                    }
                }
            }

            animator.Tick(time, dt, walking);
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
            return min + (float)rng.NextDouble() * (max - min);
        }

        private void OnDestroy()
        {
            // NpcVisualBuilder.Build가 만든 인스턴스 머티리얼 정리 (누수 방지)
            NpcVisualBuilder.CleanupMaterials(transform);
        }
    }
}
