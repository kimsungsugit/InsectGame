using UnityEngine;

namespace InsectGame.NPC
{
    /// <summary>
    /// NPC 걷기/스윙 애니메이터 — MonoBehaviour 아님(중앙 tick: NpcManager → NPC 컴포넌트 → Tick).
    /// PlayerMovement.AnimateWalk의 팔다리 sin 스윙 + 바디 밥 로직 이식.
    /// 생성자에서 transform.Find 1회 캐시 (노드명은 NpcVisualBuilder와 동기).
    /// </summary>
    public class NpcWalkAnimator
    {
        private readonly Transform armL;
        private readonly Transform armR;
        private readonly Transform legPivotL;
        private readonly Transform legPivotR;
        private readonly Transform body;
        private readonly Transform headPivot;
        private readonly Transform netHandle;
        private readonly Transform netRing;

        private readonly Quaternion netHandleBaseRot;
        private readonly Quaternion netRingBaseRot;

        // idle 미세 모션 위상차 — 전역 Time.time에 더해 이웃 NPC와 호흡/고개가 어긋나게(일제 동작 방지).
        private readonly float idlePhase;

        private float walkTimer;
        private float bodyBaseY = float.NaN;

        // 1회성 제스처 — 곡선은 NpcGesturePose(순수부)가 갖고 여기선 타이머만 굴린다.
        private NpcGesture activeGesture = NpcGesture.None;
        private float gestureTimer;
        private float gestureDuration;

        public bool IsSwinging => activeGesture == NpcGesture.NetSwing && gestureTimer > 0f;
        /// <summary>지금 몸짓을 재생 중인가 — 연출이 다음 스텝으로 넘어갈 시점 판단에 쓴다.</summary>
        public bool IsGesturing => gestureTimer > 0f;

        public NpcWalkAnimator(Transform root)
        {
            // 노드 캐시 — NPC 모델은 재생성되지 않으므로 생성자 1회로 충분 (PlayerMovement lazy 캐시 참고)
            armL = root.Find("ArmL");
            armR = root.Find("ArmR");
            legPivotL = root.Find("LegLPivot");
            legPivotR = root.Find("LegRPivot");
            body = root.Find("Body");
            headPivot = root.Find("HeadPivot");
            netHandle = root.Find("NetHandle");   // 아이만 존재 (주민은 null — Tick에서 null 가드)
            netRing = root.Find("NetRing");

            // 뜰채 base 회전 — NPC는 도구 교체가 없어 생성 시 1회 고정 캐시
            if (netHandle != null) netHandleBaseRot = netHandle.localRotation;
            if (netRing != null) netRingBaseRot = netRing.localRotation;

            // idle 위상차 — 위치 기반 결정적 값(할당 없음). 근처 NPC끼리 호흡/고개 타이밍이 어긋난다.
            Vector3 p = root.position;
            idlePhase = p.x * 0.7f + p.z * 1.3f;
        }

        /// <summary>뜰채 스윙 1회성 시작 (PlayerMovement.PlayCatchSwing 참고). Tick에서 타이머 처리.</summary>
        public void PlaySwing()
        {
            PlayGesture(NpcGesture.NetSwing);
        }

        /// <summary>
        /// 몸짓 1회 재생. 재생 중에 다시 부르면 <b>새 몸짓이 앞의 것을 즉시 대체한다</b> —
        /// 섞으면 관절이 어디로 갈지 알 수 없고, 연출은 한 번에 하나만 보이면 된다.
        /// 길이가 0인 값(None·미등록)이면 재생 중이던 것을 끄기만 한다.
        /// </summary>
        public void PlayGesture(NpcGesture gesture)
        {
            float duration = NpcGesturePose.DurationOf(gesture);
            if (duration <= 0f)
            {
                activeGesture = NpcGesture.None;
                gestureTimer = 0f;
                return;
            }
            activeGesture = gesture;
            gestureDuration = duration;
            gestureTimer = duration;
        }

        /// <summary>매 프레임 호출(40m 이내 NPC만) — 팔다리 sin 스윙 + 바디 밥 + 제스처 타이머.</summary>
        public void Tick(float time, float dt, bool walking)
        {
            if (walking) walkTimer += dt * 8f;
            else walkTimer = 0f;

            // idle 호흡 파형(-1..1) — 걷지 않을 때 완전 정지(조각상) 방지. 고정 배치 스토리 NPC에 특히 효과.
            float idle = walking ? 0f : Mathf.Sin((time + idlePhase) * 1.5f);
            float idleArm = idle * 1.2f;   // idle 시 어깨 미세 들썩 — 팔이 뻣뻣하게 굳지 않게

            float swing = walking ? Mathf.Sin(walkTimer) : 0f;
            float swingDeg = swing * 25f;
            // 걷기: 흡수 밥(0.06). idle: 몸통만 미세 상하(호흡, ~1.8cm) — Body는 torso 단독이라 가슴 부풂으로 읽힘.
            float bobY = walking ? Mathf.Abs(Mathf.Sin(walkTimer * 2f)) * 0.06f : idle * 0.018f;

            // 1회성 제스처 — 타이머만 여기서 굴리고 각도는 순수부에서 받는다.
            NpcPoseDelta pose = default;
            if (gestureTimer > 0f)
            {
                gestureTimer -= dt;
                if (gestureTimer <= 0f)
                {
                    // 마지막 프레임을 t=1(모든 채널 0)로 확정한다. 중간값에서 끊으면 그 NPC가
                    // 팔을 든 채로 영영 서 있게 된다 — 곡선이 양 끝에서 0인 이유이기도 하다.
                    gestureTimer = 0f;
                    pose = NpcGesturePose.Evaluate(activeGesture, 1f);
                    activeGesture = NpcGesture.None;
                }
                else
                {
                    float cp = 1f - Mathf.Clamp01(gestureTimer / gestureDuration); // 0→1
                    pose = NpcGesturePose.Evaluate(activeGesture, cp);
                }
            }

            // 제스처가 지배하지 않는 관절은 걷기/idle 파형을 그대로 쓴다(뜰채 스윙이 오른팔만 뺏던 동작).
            float leftArmDeg = pose.ownsLeftArm ? pose.leftArmDeg : swingDeg + idleArm;
            float rightArmDeg = pose.ownsRightArm ? pose.rightArmDeg : -swingDeg + idleArm;

            if (armL != null) armL.localRotation = Quaternion.Euler(leftArmDeg, 0f, 0f);
            if (armR != null) armR.localRotation = Quaternion.Euler(rightArmDeg, 0f, 0f);

            // 뜰채 = 오른팔과 동기 회전 (base 회전 보존). 팔과 **같은 각도**를 쓴다 —
            // 예전엔 idleArm이 팔에만 더해져 멈춰 있을 때 도구가 손에서 미세하게 어긋났다.
            if (netHandle != null)
                netHandle.localRotation = Quaternion.Euler(rightArmDeg, 0f, 0f) * netHandleBaseRot;
            if (netRing != null)
                netRing.localRotation = Quaternion.Euler(rightArmDeg, 0f, 0f) * netRingBaseRot;

            // 다리 (팔과 반대) — LegPivot 회전으로 Leg+Boot 함께 전파. 제스처는 전부 상체라 관여하지 않는다.
            if (legPivotL != null) legPivotL.localRotation = Quaternion.Euler(-swingDeg * 0.8f, 0f, 0f);
            if (legPivotR != null) legPivotR.localRotation = Quaternion.Euler(swingDeg * 0.8f, 0f, 0f);

            // 몸통 밥 (초기 Y baseline 1회 캐시)
            if (body != null)
            {
                Vector3 bp = body.localPosition;
                if (float.IsNaN(bodyBaseY)) bodyBaseY = bp.y;
                bp.y = bodyBaseY + (pose.ownsBody ? pose.bodyOffsetY : bobY);
                body.localPosition = bp;
            }

            // 머리 미세 흔들림 — 걷기: 좌우 흔들. idle: 아주 느린 고개 스윙(~14s 주기, ±4.5°) 주변 둘러보는 인상.
            if (headPivot != null)
            {
                float headYaw = walking
                    ? Mathf.Sin(walkTimer * 0.5f) * 3f
                    : Mathf.Sin((time + idlePhase) * 0.45f) * 4.5f;
                float headPitch = 0f;
                if (pose.ownsHead)
                {
                    headYaw = pose.headYawDeg;
                    headPitch = pose.headPitchDeg;
                }
                headPivot.localRotation = Quaternion.Euler(headPitch, headYaw, 0f);
            }
        }
    }
}
