using UnityEngine;

namespace InsectGame.Core
{
    public class CameraFollower : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -8f);
        [SerializeField] private float smoothSpeed = 6f;
        [SerializeField] private float lookAheadDistance = 2f;

        private bool battleMode;
        private Vector3 battlePos;
        private Quaternion battleRot;
        private Vector3 normalPos;
        private Quaternion normalRot;
        private float battleTransition;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void EnterBattleMode(Vector3 playerPos, Vector3 enemyPos)
        {
            battleMode = true;
            battleTransition = 0f;
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
        }

        public bool InBattleMode => battleMode;

        private void LateUpdate()
        {
            if (target == null) return;

            if (battleMode)
            {
                battleTransition = Mathf.Clamp01(battleTransition + Time.deltaTime * 2.5f);
                float t = Mathf.SmoothStep(0f, 1f, battleTransition);

                Vector3 followPos = target.position + offset + target.forward * lookAheadDistance;
                transform.position = Vector3.Lerp(followPos, battlePos, t);
                Quaternion followRot = Quaternion.LookRotation(target.position + Vector3.up - followPos);
                transform.rotation = Quaternion.Slerp(followRot, battleRot, t);
            }
            else
            {
                if (battleTransition > 0f)
                {
                    battleTransition = Mathf.Clamp01(battleTransition - Time.deltaTime * 3f);
                    float t = Mathf.SmoothStep(0f, 1f, battleTransition);
                    Vector3 followPos = target.position + offset + target.forward * lookAheadDistance;
                    transform.position = Vector3.Lerp(followPos, battlePos, t);
                    Quaternion followRot = Quaternion.LookRotation(target.position + Vector3.up - followPos);
                    transform.rotation = Quaternion.Slerp(followRot, battleRot, t);
                }
                else
                {
                    Vector3 lookAhead = target.forward * lookAheadDistance;
                    Vector3 desiredPosition = target.position + offset + lookAhead;
                    transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
                    Vector3 lookTarget = target.position + Vector3.up * 1f;
                    transform.LookAt(lookTarget);
                }
            }
        }
    }
}
