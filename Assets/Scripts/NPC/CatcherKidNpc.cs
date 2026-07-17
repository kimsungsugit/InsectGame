using InsectGame.Core;
using InsectGame.Data;
using InsectGame.Spawning;
using UnityEngine;

namespace InsectGame.NPC
{
    /// <summary>
    /// 곤충 잡는 아이 NPC 상태머신.
    /// Idle(1~3s) → Wander(속도 2.5) → 스캔(KidSpotRadius):
    ///   Rare+ → Watch(2~4s 정지, 곤충 주시) / Common·Uncommon → 예약 성공 시 Approach(속도 3.5, 5s 타임아웃)
    /// → 거리 1.2m → CatchSwing(0.6s) → 성공 시 SetEngaged(true)+Despawn() → Celebrate(1.5s 점프)
    /// → 쿨다운(kidCatchCooldownSeconds) → Idle. 실패/재검증 탈락 → GiveUp(1s 두리번) → Idle.
    /// 개별 Update 없음 — NpcManager가 TickAI(0.25s 자체 스로틀)/TickMovement를 호출.
    /// </summary>
    public class CatcherKidNpc : MonoBehaviour
    {
        private enum State { Idle, Wander, Watch, Approach, CatchSwing, Celebrate, GiveUp }

        private const float AiInterval = 0.25f;
        private const float WanderSpeed = 2.5f;
        private const float ApproachSpeed = 3.5f;
        private const float ApproachTimeout = 5f;
        private const float CatchDistance = 1.2f;
        private const float SwingDuration = 0.6f;
        private const float CelebrateDuration = 1.5f;
        private const float GiveUpDuration = 1f;
        private const float ArriveDistance = 0.3f;
        private const float TurnSpeed = 620f;
        // 지면 스텝 클램프 — 이보다 큰 Y 급변(건물 지붕/상판)은 지면으로 인정하지 않음
        private const float MaxGroundStep = 0.75f;

        private NpcManager manager;
        private string npcId;
        private Vector3 anchorPosition;
        private float wanderRadius = 8f;

        private State state = State.Idle;
        private float stateEndTime;
        private Vector3 wanderTarget;
        private InsectEntity targetInsect;    // Approach/CatchSwing 대상 (예약 보유)
        private InsectEntity watchInsect;     // Watch 대상 (예약 없음 — 구경만)
        private float cooldownUntilTime;
        private float groundY;
        private float baseYaw;                // GiveUp 두리번 기준 방향
        private float lastAiTime = float.MinValue;
        private System.Random rng;
        private NpcWalkAnimator animator;

        public string NpcId => npcId;

        /// <summary>NpcManager가 스폰 직후 호출.</summary>
        public void Initialize(NpcManager owner, NpcSpawnAnchor anchor, string id, int seed)
        {
            manager = owner;
            npcId = id;
            anchorPosition = anchor != null ? anchor.position : transform.position;
            wanderRadius = anchor != null ? anchor.wanderRadius : 8f;
            rng = new System.Random(seed);
            animator = new NpcWalkAnimator(transform);
            groundY = transform.position.y;
            state = State.Idle;
            stateEndTime = 0f;
            cooldownUntilTime = 0f;
        }

        /// <summary>상태 결정 틱 — 0.25s 주기 자체 스로틀. NpcManager 라운드로빈이 호출.</summary>
        public void TickAI(float time)
        {
            if (time - lastAiTime < AiInterval) return;
            lastAiTime = time;
            if (rng == null || manager == null) return;

            SampleGround();

            switch (state)
            {
                case State.Idle:
                    if (TryScanForInsect(time)) break;
                    if (time >= stateEndTime)
                    {
                        wanderTarget = PickWanderTarget();
                        state = State.Wander;
                        stateEndTime = time + 8f; // Wander 안전 타임아웃
                    }
                    break;

                case State.Wander:
                    if (TryScanForInsect(time)) break;
                    if (time >= stateEndTime)
                        EnterIdle(time);
                    break;

                case State.Watch:
                    // 구경 중 — 대상이 사라지면 조기 복귀
                    if (watchInsect == null || !watchInsect.gameObject.activeInHierarchy || time >= stateEndTime)
                    {
                        watchInsect = null;
                        EnterIdle(time);
                    }
                    break;

                case State.Approach:
                    TickApproach(time);
                    break;

                case State.CatchSwing:
                    if (time >= stateEndTime)
                        ResolveCatch(time);
                    break;

                case State.Celebrate:
                    if (time >= stateEndTime)
                        EnterIdle(time);
                    break;

                case State.GiveUp:
                    if (time >= stateEndTime)
                        EnterIdle(time);
                    break;
            }
        }

        /// <summary>이동/애니 틱 — 플레이어 40m 이내에서만 매 프레임 호출.</summary>
        public void TickMovement(float dt, float time)
        {
            if (animator == null) return;

            bool walking = false;
            switch (state)
            {
                case State.Wander:
                    walking = MoveTowards(wanderTarget, WanderSpeed, dt);
                    if (!walking) EnterIdle(time);
                    break;

                case State.Approach:
                    if (targetInsect != null)
                        walking = MoveTowards(targetInsect.transform.position, ApproachSpeed, dt);
                    break;

                case State.Watch:
                    if (watchInsect != null) FaceTowards(watchInsect.transform.position);
                    break;

                case State.Celebrate:
                {
                    // 점프 모션 — 남은 시간 기반 sin 바운스
                    float elapsed = CelebrateDuration - (stateEndTime - time);
                    Vector3 pos = transform.position;
                    pos.y = groundY + Mathf.Abs(Mathf.Sin(elapsed * 8f)) * 0.3f;
                    transform.position = pos;
                    break;
                }

                case State.GiveUp:
                {
                    // 두리번 — 기준 방향 좌우로 고개(몸) 회전
                    float yaw = baseYaw + Mathf.Sin(time * 5f) * 55f;
                    transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    break;
                }
            }

            animator.Tick(time, dt, walking);
        }

        // ── 스캔: 반경 내 최근접 곤충 기준 Watch(Rare+) / Approach(Common·Uncommon) 분기 ──
        private bool TryScanForInsect(float time)
        {
            if (time < cooldownUntilTime) return false;

            var insects = manager.ActiveInsects;
            if (insects == null) return false;

            InsectEntity best = null;
            float bestSq = NpcCatchRules.KidSpotRadius * NpcCatchRules.KidSpotRadius;
            Vector3 myPos = transform.position;

            for (int i = 0; i < insects.Count; i++)
            {
                InsectEntity e = insects[i];
                if (e == null || !e.gameObject.activeInHierarchy || e.Data == null) continue;
                float sq = (e.transform.position - myPos).sqrMagnitude;
                if (sq >= bestSq) continue;
                // 잡기 후보는 규칙 통과 필요, Rare+는 구경 후보라 통과 없이 인정
                if (!NpcCatchRules.ShouldWatchOnly(e.Data.rarity)
                    && !NpcCatchRules.CanKidTarget(e.Data.rarity, e.CanBeEngaged,
                        manager.DistanceFromPlayer(e.transform.position), manager.IsReserved(e)))
                    continue;
                bestSq = sq;
                best = e;
            }

            if (best == null) return false;

            if (NpcCatchRules.ShouldWatchOnly(best.Data.rarity))
            {
                // Rare 이상 — 정지하고 구경만 (2~4s)
                watchInsect = best;
                state = State.Watch;
                stateEndTime = time + RandomRange(2f, 4f);
                return true;
            }

            if (manager.TryReserveInsect(best))
            {
                targetInsect = best;
                state = State.Approach;
                stateEndTime = time + ApproachTimeout;
                return true;
            }
            return false;
        }

        // ── Approach: 매 TickAI 재검증 (스펙: !CanBeEngaged 또는 플레이어 근접 시 포기) ──
        private void TickApproach(float time)
        {
            if (targetInsect == null || !targetInsect.gameObject.activeInHierarchy
                || !targetInsect.CanBeEngaged
                || manager.DistanceFromPlayer(targetInsect.transform.position) < NpcCatchRules.PlayerClaimRadius
                || time >= stateEndTime)
            {
                EnterGiveUp(time);
                return;
            }

            Vector3 to = targetInsect.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude <= CatchDistance * CatchDistance)
            {
                FaceTowards(targetInsect.transform.position);
                if (animator != null) animator.PlaySwing();
                state = State.CatchSwing;
                stateEndTime = time + SwingDuration;
            }
        }

        // ── 스윙 종료: CanBeEngaged 최종 확인 → SetEngaged(true) 직후 Despawn() ──
        private void ResolveCatch(float time)
        {
            InsectEntity caught = targetInsect;
            bool success = caught != null && caught.gameObject.activeInHierarchy && caught.CanBeEngaged;

            // ABA 방어: 스윙(0.6s) 중 대상이 외부 요인(서브에리어 진입/원거리 디스폰)으로 풀에
            // 반환되고 같은 인스턴스가 '다른 곤충'으로 재초기화되면 위 두 체크를 통과한다.
            // 재활용 개체는 새 스폰 지점으로 순간이동해 있으므로 거리 재확인이 이를 차단하고,
            // 레어도/플레이어 근접 재확인으로 스윙 창 동안의 규칙 위반도 막는다.
            if (success)
            {
                Vector3 to = caught.transform.position - transform.position;
                to.y = 0f;
                float maxSq = CatchDistance * 1.5f * (CatchDistance * 1.5f);
                success = to.sqrMagnitude <= maxSq
                    && caught.Data != null
                    && !NpcCatchRules.ShouldWatchOnly(caught.Data.rarity)
                    && manager.DistanceFromPlayer(caught.transform.position) >= NpcCatchRules.PlayerClaimRadius;
            }

            if (success)
            {
                caught.SetEngaged(true);   // 도주 차단 상태로 고정 후
                caught.Despawn();          // 스포너 알림 + 풀 반환 (다중 호출 가드 내장)
                manager.ReleaseInsect(caught);
                targetInsect = null;
                state = State.Celebrate;
                stateEndTime = time + CelebrateDuration;
                cooldownUntilTime = time + manager.KidCatchCooldownSeconds;
            }
            else
            {
                EnterGiveUp(time);
            }
        }

        private void EnterGiveUp(float time)
        {
            if (targetInsect != null)
            {
                manager.ReleaseInsect(targetInsect);
                targetInsect = null;
            }
            baseYaw = transform.eulerAngles.y;
            state = State.GiveUp;
            stateEndTime = time + GiveUpDuration;
        }

        private void EnterIdle(float time)
        {
            // Celebrate 점프 잔여 Y 복귀
            Vector3 pos = transform.position;
            pos.y = groundY;
            transform.position = pos;
            watchInsect = null;
            state = State.Idle;
            stateEndTime = time + RandomRange(1f, 3f);
        }

        /// <summary>목표 지점으로 XZ 이동. 도착/통행 불가면 false(=걷기 종료) 반환.</summary>
        private bool MoveTowards(Vector3 target, float speed, float dt)
        {
            Vector3 to = target - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist <= ArriveDistance) return false;

            Vector3 dir = to / dist;
            if (IsBlockedAhead(dir, speed * dt)) return false; // 벽 관통 방지 — Wander는 Idle 복귀, Approach는 타임아웃→GiveUp

            Vector3 pos = transform.position + dir * (speed * dt);
            pos.y = groundY;
            transform.position = pos;
            RotateTowards(dir, dt);
            return true;
        }

        /// <summary>진행 방향에 통행 불가 콜라이더(건물 벽 등)가 있는지 — 벽 관통 방지.</summary>
        private bool IsBlockedAhead(Vector3 dir, float step)
        {
            if (!Physics.Raycast(transform.position + Vector3.up * 0.5f, dir,
                    out RaycastHit hit, step + 0.35f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;
            if (hit.transform.IsChildOf(transform)) return false;
            // 곤충 엔티티는 통과 허용 (PlayerMovement.IsBlockedPosition 관례) — 접근/포획 대상이므로
            if (hit.collider.GetComponentInParent<InsectEntity>() != null) return false;
            return true;
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
            // 본인 콜라이더는 트리거(NpcVisualBuilder)라 Ignore로 자동 제외 — raycast 1회/tick
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

        private void OnDisable()
        {
            // ApplyTuning 축소로 비활성화되거나 파괴될 때 예약 잔존 방지
            if (manager != null && targetInsect != null)
            {
                manager.ReleaseInsect(targetInsect);
                targetInsect = null;
            }
        }

        private void OnDestroy()
        {
            NpcVisualBuilder.CleanupMaterials(transform);
        }
    }
}
