using UnityEngine;

namespace InsectGame.Spawning
{
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private float radius = 5f;
        [SerializeField] private int maxLocalActive = 2;

        public string regionId;

        /// <summary>
        /// 서브에리어 전용 포인트인가.
        ///
        /// <b>regionId만으로는 구분할 수 없다</b> — 서브에리어 포인트도 부모 리전의 ID를
        /// 그대로 달고 있어서(부트스트랩이 그렇게 만든다) 리전 필터에 함께 걸린다.
        /// 그래서 명시 플래그를 둔다: 스폰 레벨 곡선 판정과 재배치 제외가 이걸 본다.
        /// </summary>
        public bool isSubAreaPoint;

        public string[] regionInsectIds;
        public int regionMinLevel = 1;
        public int regionMaxLevel = 5;

        private int localActiveCount;

        public bool CanSpawn => localActiveCount < maxLocalActive;

        public Vector3 GetRandomPosition()
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            return transform.position + new Vector3(offset.x, 0f, offset.y);
        }

        public void NotifySpawned()
        {
            localActiveCount = Mathf.Max(0, localActiveCount + 1);
        }

        public void NotifyDespawned()
        {
            localActiveCount = Mathf.Max(0, localActiveCount - 1);
        }

        /// <summary>
        /// 재동기화 시작 — 카운트를 0으로 두고 <see cref="NotifyLive"/>로 실제 개체를 다시 센다.
        ///
        /// 옛 <c>ResetCount</c>는 여기서 끝이라 <b>살아 있는 곤충이 붙어 있어도 0으로 밀었다.</b>
        /// 재배치가 8초마다 그걸 부르니 상한 2가 "동시 2마리"가 아니라 "8초당 2마리"가 되어
        /// 같은 반경 5m에 네 마리 넘게 뭉쳤고, 이후 <c>NotifyDespawned</c>의 0 클램프 때문에
        /// 카운터가 실제보다 영구히 낮게 남았다.
        /// </summary>
        public void BeginRecount()
        {
            localActiveCount = 0;
        }

        /// <summary>재동기화 중 이 포인트 소속으로 살아 있는 개체 하나를 센다.</summary>
        public void NotifyLive()
        {
            localActiveCount++;
        }
    }
}
