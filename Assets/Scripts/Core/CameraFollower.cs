using UnityEngine;
using InsectGame.Spawning;

namespace InsectGame.Core
{
    public class CameraFollower : MonoBehaviour
    {
        [SerializeField] private Transform target;
        // 치비(작아진 캐릭터)에 맞춰 줌인 (0,12,-8)→(0,9,-6). Y:Z=1.5 비율 유지 → 부감 각도 동일,
        // 시선벡터 (0,-0.8,0.6) 보존 → 도구 망 디스크 회전(-20°) 정합 그대로.
        [SerializeField] private Vector3 offset = new Vector3(0f, 9f, -6f);
        [SerializeField] private float smoothSpeed = 6f;
        [SerializeField] private float lookAheadDistance = 2f;

        // SubArea 카메라 — 메인 월드와 동일 NormalOffset 사용. 옛 (0,16,-12)는 환경이 캐릭터로부터
        // 분리되어 "공중에 떠있는" 인상, (0,10,-14)도 측면 분리 인상 회귀(사용자 명시 보고).
        // 환경 차폐는 SubAreaEnv layer 제외(ResolveObstruction)로 차단, SetSubAreaMode는 baseline 리셋만.
        private static readonly Vector3 NormalOffset = new Vector3(0f, 9f, -6f);
        private static readonly Vector3 SubAreaOffset = new Vector3(0f, 9f, -6f);
        // 카메라-플레이어 사이 큰 정적 scenery(나무 trunk, 벽, 기둥 등) 시야 차단 시 카메라 당김.
        // 사용자 보고: 숲/유적 등에서 다 가려져서 안 보임 — 모든 필드 일괄 처리.
        [SerializeField] private float minObstructionDistance = 3.5f;
        [SerializeField] private float obstructionProbeRadius = 0.4f;
        // SphereCastNonAlloc 버퍼 — 매 LateUpdate RaycastHit[] 할당 회피.
        private readonly RaycastHit[] obstructionBuffer = new RaycastHit[16];

        // 화면 종횡비 기반 FOV 보정 캐시 — 자동회전(가로↔세로) 시 재계산.
        private Camera cam;
        private int lastScreenW = -1;
        private int lastScreenH = -1;

        private bool battleMode;
        private Vector3 battlePos;
        private Quaternion battleRot;
        private Vector3 normalPos;
        private Quaternion normalRot;
        private float battleTransition;

        private float shakeIntensity;
        private float shakeDuration;
        private float shakeTimer;
        private Vector3 baselinePos; // 쉐이크 영향 없는 기본 위치 (다음 프레임 Lerp 기준)
        private bool baselineValid;

        // 시네마틱 포커스 — 첫 조우 등에서 잠깐 대상 쪽으로 줌인 후 자동 복귀(focusTimer>0일 때만 동작).
        private Vector3 focusPoint;
        private float focusTimer;
        private float focusDuration;
        // 포커스 조기 릴리즈 — 스토리 모달을 벨(2.5s) 종료 전에 닫으면 현재 amt에서 짧게 이즈아웃(스냅 방지).
        private bool focusReleasing;
        private float releaseTimer;
        private float releaseDuration;
        private float releaseFromAmt;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            // SubArea 좌표 전환 시 옛 baselinePos에서 새 target으로 Lerp되어 첫 프레임 카메라 점프 차단.
            baselineValid = false;
        }

        public void EnterBattleMode(Vector3 playerPos, Vector3 enemyPos)
        {
            battleMode = true;
            focusTimer = 0f;   // 진행 중 시네마틱 포커스 취소(배틀 카메라 우선)
            focusReleasing = false;
            // 이탈 fade-out(battleTransition > 0) 진행 중 재진입 시 transition 보존 — 시각적 끊김 차단.
            // 새 진입(이미 normal)이면 0부터 시작.
            // 옛은 무조건 0 리셋 → 배틀→이탈→배틀 빠른 전환 시 카메라 끊김.
            normalPos = transform.position;
            normalRot = transform.rotation;

            Vector3 mid = (playerPos + enemyPos) / 2f;
            Vector3 dir = (enemyPos - playerPos).normalized;
            Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
            battlePos = mid + side * 4f + Vector3.up * 5f;
            battleRot = Quaternion.LookRotation(mid + Vector3.up * 0.5f - battlePos);
        }

        public void ExitBattleMode()
        {
            battleMode = false;
            // battle→normal 전환 시 baselinePos가 옛 battlePos 잔존 → normal 첫 프레임 lerpFrom이
            // 멀리서 슬슬 들어오는 어색함. transition 보간이 별도 수행되므로 baseline은 reset.
            baselineValid = false;
        }

        // 외부에서 target 위치를 직접 점프시킨 경우(SubArea 진입/종료 등) 호출.
        // SetTarget과 동일하게 baselinePos stale 차단하나 target 자체는 변경 안 함.
        public void ResetBaseline()
        {
            baselineValid = false;
        }

        // SubArea(숲/유적 등) 진입 시 카메라 offset을 더 멀리·높이 조정 — 환경 밀집 차폐 완화.
        // 옛은 SubArea에서도 NormalOffset이라 트리/기둥에 시야 차단 빈번 → SphereCast가 카메라를
        // minObstructionDistance(3.5m)까지 당겨 캐릭터가 환경에 묻혀 안 보임. 사용자 보고와 정합.
        public void SetSubAreaMode(bool active)
        {
            offset = active ? SubAreaOffset : NormalOffset;
            baselineValid = false;
        }

        public bool InBattleMode => battleMode;

        /// <summary>카메라 쉐이크 트리거. intensity: 흔들림 강도(0.1~0.5 권장), duration: 지속 시간(초).</summary>
        public void Shake(float intensity, float duration)
        {
            shakeIntensity = Mathf.Max(shakeIntensity, intensity);
            shakeDuration = Mathf.Max(shakeDuration, duration);
            shakeTimer = shakeDuration;
        }

        /// <summary>짧은 시네마틱 포커스 — worldPoint 쪽으로 잠깐 줌인 후 자동 복귀(첫 조우 연출 등).
        /// 배틀 모드 중엔 무시. 팔로우를 대체하지 않고 focusTimer 동안만 데스티네이션을 편향한다.</summary>
        public void FocusOn(Vector3 worldPoint, float duration)
        {
            if (battleMode) return;
            focusPoint = worldPoint;
            focusDuration = Mathf.Max(0.2f, duration);
            focusTimer = focusDuration;
            focusReleasing = false;   // 진행 중 릴리즈가 새 포커스를 삼키지 않게
        }

        /// <summary>진행 중인 시네마틱 포커스를 부드럽게 조기 종료 — 스토리 모달을 벨 종료 전에 닫았을 때
        /// 카메라 잔류 제거. 현재 벨 amt에서 ~0.4s 이즈아웃(벨 경로를 두면 최대 절반 지속시간만큼 줌이 남는다).</summary>
        public void ReleaseFocus()
        {
            if (focusReleasing) return;    // 이미 릴리즈 중
            if (focusTimer <= 0f) return;  // 진행 중 포커스 없음(자연 종료했거나 시작 전)
            float bell = 1f - Mathf.Abs(2f * (focusTimer / focusDuration) - 1f);
            releaseFromAmt = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(bell));
            releaseDuration = 0.4f;
            releaseTimer = releaseDuration;
            focusReleasing = true;
            focusTimer = 0f;               // 벨 경로 중단 → 릴리즈 경로로 전환
        }

        private Vector3 GetShakeOffset()
        {
            if (shakeTimer <= 0f) return Vector3.zero;
            float decay = shakeTimer / shakeDuration;
            float curIntensity = shakeIntensity * decay;
            return new Vector3(
                (Random.value * 2f - 1f) * curIntensity,
                (Random.value * 2f - 1f) * curIntensity * 0.6f,
                (Random.value * 2f - 1f) * curIntensity);
        }

        private void LateUpdate()
        {
            ApplyAspectFov();
            if (target == null) return;

            Vector3 finalPos;
            Quaternion finalRot;

            if (battleMode)
            {
                battleTransition = Mathf.Clamp01(battleTransition + Time.deltaTime * 2.5f);
                float t = Mathf.SmoothStep(0f, 1f, battleTransition);

                Vector3 followPos = target.position + offset + target.forward * lookAheadDistance;
                finalPos = Vector3.Lerp(followPos, battlePos, t);
                Quaternion followRot = Quaternion.LookRotation(target.position + Vector3.up - followPos);
                finalRot = Quaternion.Slerp(followRot, battleRot, t);
            }
            else
            {
                if (battleTransition > 0f)
                {
                    battleTransition = Mathf.Clamp01(battleTransition - Time.deltaTime * 3f);
                    float t = Mathf.SmoothStep(0f, 1f, battleTransition);
                    Vector3 followPos = target.position + offset + target.forward * lookAheadDistance;
                    finalPos = Vector3.Lerp(followPos, battlePos, t);
                    Quaternion followRot = Quaternion.LookRotation(target.position + Vector3.up - followPos);
                    finalRot = Quaternion.Slerp(followRot, battleRot, t);
                }
                else
                {
                    Vector3 lookAhead = target.forward * lookAheadDistance;
                    Vector3 desiredPosition = target.position + offset + lookAhead;
                    Vector3 lookTarget = target.position + Vector3.up * 0.85f;
                    // 카메라 시야 차단 보정 — 나무/벽/기둥 등 정적 scenery가 카메라-플레이어
                    // 사이를 가리면 카메라를 플레이어 쪽으로 당겨 시야 확보.
                    desiredPosition = ResolveObstruction(lookTarget, desiredPosition);

                    // 시네마틱 포커스(첫 조우 등) — focusPoint 쪽으로 잠깐 줌인 후 복귀(0→1→0 벨 이즈).
                    // ReleaseFocus() 호출(모달 조기 닫힘) 시엔 현재 amt에서 짧게 이즈아웃.
                    float focusAmt = 0f;
                    if (focusReleasing)
                    {
                        releaseTimer -= Time.deltaTime;
                        focusAmt = releaseFromAmt * Mathf.Clamp01(releaseTimer / releaseDuration);
                        if (releaseTimer <= 0f) focusReleasing = false;
                    }
                    else if (focusTimer > 0f)
                    {
                        focusTimer -= Time.deltaTime;
                        float bell = 1f - Mathf.Abs(2f * (focusTimer / focusDuration) - 1f);
                        focusAmt = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(bell));
                    }
                    if (focusAmt > 0f)
                    {
                        Vector3 fp = focusPoint + Vector3.up * 0.85f;
                        lookTarget = Vector3.Lerp(lookTarget, (lookTarget + fp) * 0.5f, focusAmt * 0.8f);
                        Vector3 zoomPos = target.position + offset * (1f - focusAmt * 0.35f) + lookAhead;
                        desiredPosition = Vector3.Lerp(desiredPosition, ResolveObstruction(lookTarget, zoomPos), focusAmt);
                    }

                    // 쉐이크 이전의 깨끗한 위치를 기준으로 Lerp (쉐이크 누적 방지)
                    Vector3 lerpFrom = baselineValid ? baselinePos : transform.position;
                    finalPos = Vector3.Lerp(lerpFrom, desiredPosition, smoothSpeed * Time.deltaTime);
                    // LookAt은 쉐이크 없는 기본 위치 기준으로 계산
                    finalRot = Quaternion.LookRotation(lookTarget - finalPos, Vector3.up);
                }
            }

            // 쉐이크 이전 기본 위치 저장 (다음 프레임 Lerp 기준)
            baselinePos = finalPos;
            baselineValid = true;

            // 쉐이크 오프셋 적용
            if (shakeTimer > 0f)
            {
                shakeTimer -= Time.unscaledDeltaTime;
                finalPos += GetShakeOffset();
                if (shakeTimer <= 0f)
                {
                    shakeIntensity = 0f;
                    shakeDuration = 0f;
                }
            }

            transform.position = finalPos;
            transform.rotation = finalRot;
        }

        // 화면 종횡비에 맞춰 수직 FOV 보정. 세로(좁고 긴 화면)는 기본 60°,
        // 가로(와이드)는 줌인해 캐릭터가 작게 보이는 현상 방지. 수직 FOV를 고정하면
        // 가로 화면에서 피사체가 프레임의 작은 일부만 차지하는 문제가 생긴다.
        // 자동회전으로 종횡비가 바뀔 때마다(해상도 변화 감지) 1회만 재계산.
        private void ApplyAspectFov()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) return;
            if (Screen.width == lastScreenW && Screen.height == lastScreenH) return;
            lastScreenW = Screen.width;
            lastScreenH = Screen.height;

            if (Screen.height >= Screen.width)
            {
                cam.fieldOfView = 60f; // 세로: 기준값(현재 정상)
            }
            else
            {
                // 가로: 넓어질수록 줌인. 32~60° 범위로 클램프해 과도한 줌 방지.
                float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
                cam.fieldOfView = Mathf.Clamp(60f / Mathf.Sqrt(aspect), 32f, 60f);
            }
        }

        // 카메라 방향 SphereCast로 큰 정적 scenery 차단 검출 후 카메라 거리 단축.
        // 모든 필드 일괄 적용: forest trunk, swamp DeadTree, mountain rock, garden hedge/arch,
        // ruins wall/pillar 등 — Floor/Ground/Region/Path/Water 평면과 InsectEntity는 제외.
        private Vector3 ResolveObstruction(Vector3 lookTarget, Vector3 desiredPos)
        {
            Vector3 toCam = desiredPos - lookTarget;
            float desiredDist = toCam.magnitude;
            if (desiredDist <= minObstructionDistance) return desiredPos;

            Vector3 dirToCam = toCam / desiredDist;
            int hitCount = Physics.SphereCastNonAlloc(lookTarget, obstructionProbeRadius, dirToCam, obstructionBuffer, desiredDist);
            if (hitCount <= 0) return desiredPos;

            float minHitDist = desiredDist;
            for (int i = 0; i < hitCount; i++)
            {
                Collider h = obstructionBuffer[i].collider;
                if (h == null || h.isTrigger) continue;
                if (target != null && (h.transform.IsChildOf(target) || h.gameObject == target.gameObject)) continue;
                // 곤충은 차단 대상 아님 — 화면에 보여야 캡처/배틀 진입 가능
                if (h.GetComponentInParent<InsectEntity>() != null) continue;

                string n = h.gameObject.name;
                // 바닥/길/물/리전 plane 제외 — 카메라 위쪽에서 잡힐 일 거의 없지만 안전망.
                // 옛 EndsWith("_Floor")는 "Ruins_MossFloor_0"/"ArenaFloor" 회귀(suffix index/접두 없음).
                if (n == "Ground"
                    || n.StartsWith("Region_")
                    || n.StartsWith("Ground_")
                    || n.Contains("Floor")
                    || n.Contains("Ground")
                    || n.Contains("Path")
                    || n.Contains("Water")
                    || n.Contains("Bank")
                    || n.Contains("Creek")
                    || n.Contains("Snow")) continue;
                // 너무 낮은 collider(꽃/돌 등)는 시야 거의 안 가림
                if (h.bounds.size.y < 0.6f) continue;
                // SubArea 환경(나무/기둥/잎사귀 등)은 차폐 무시 — 캐릭터 가시성 우선.
                // 옛은 트리/기둥이 모두 차폐로 잡혀 카메라가 캐릭터까지 당겨져 환경에 묻힘.
                if (h.gameObject.layer == SubAreaWorldBuilder.GetSubAreaEnvLayer()) continue;

                float d = obstructionBuffer[i].distance;
                if (d < minHitDist) minHitDist = d;
            }

            // 최소 거리 보장 — 너무 가까이 가면 1인칭처럼 되어 어색
            float finalDist = Mathf.Max(minObstructionDistance, minHitDist - 0.3f);
            return lookTarget + dirToCam * finalDist;
        }
    }
}
