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
        private const float SwingDuration = 0.6f;   // 뜰채 스윙 1회성 액션 시간 (CatcherKidNpc CatchSwing과 동기)
        private const float SwingMaxDeg = 72f;      // PlayerMovement.CatchSwingMaxDeg 이식

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
        private float swingTimer;

        public bool IsSwinging => swingTimer > 0f;

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
            swingTimer = SwingDuration;
        }

        /// <summary>매 프레임 호출(40m 이내 NPC만) — 팔다리 sin 스윙 + 바디 밥 + 스윙 타이머.</summary>
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

            // 오른팔 — 기본은 걷기 스윙, 뜰채 스윙 중엔 큰 sin 아크 오버라이드
            float rightArmDeg = -swingDeg;
            if (swingTimer > 0f)
            {
                swingTimer -= dt;
                float cp = 1f - Mathf.Clamp01(swingTimer / SwingDuration); // 0→1
                rightArmDeg = Mathf.Sin(cp * Mathf.PI) * SwingMaxDeg;      // 0→peak→0
            }

            if (armL != null) armL.localRotation = Quaternion.Euler(swingDeg + idleArm, 0f, 0f);
            if (armR != null) armR.localRotation = Quaternion.Euler(rightArmDeg + idleArm, 0f, 0f);

            // 뜰채 = 오른팔과 동기 회전 (base 회전 보존)
            if (netHandle != null)
                netHandle.localRotation = Quaternion.Euler(rightArmDeg, 0f, 0f) * netHandleBaseRot;
            if (netRing != null)
                netRing.localRotation = Quaternion.Euler(rightArmDeg, 0f, 0f) * netRingBaseRot;

            // 다리 (팔과 반대) — LegPivot 회전으로 Leg+Boot 함께 전파
            if (legPivotL != null) legPivotL.localRotation = Quaternion.Euler(-swingDeg * 0.8f, 0f, 0f);
            if (legPivotR != null) legPivotR.localRotation = Quaternion.Euler(swingDeg * 0.8f, 0f, 0f);

            // 몸통 밥 (초기 Y baseline 1회 캐시)
            if (body != null)
            {
                Vector3 bp = body.localPosition;
                if (float.IsNaN(bodyBaseY)) bodyBaseY = bp.y;
                bp.y = bodyBaseY + bobY;
                body.localPosition = bp;
            }

            // 머리 미세 흔들림 — 걷기: 좌우 흔들. idle: 아주 느린 고개 스윙(~14s 주기, ±4.5°) 주변 둘러보는 인상.
            if (headPivot != null)
            {
                float headTilt = walking
                    ? Mathf.Sin(walkTimer * 0.5f) * 3f
                    : Mathf.Sin((time + idlePhase) * 0.45f) * 4.5f;
                headPivot.localRotation = Quaternion.Euler(0f, headTilt, 0f);
            }
        }
    }
}
