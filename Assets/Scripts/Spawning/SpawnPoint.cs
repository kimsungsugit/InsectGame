using UnityEngine;

namespace InsectGame.Spawning
{
    /// <summary>
    /// 리전(또는 서브에리어)의 스폰 표 — 서식 풀과 레벨 대역을 스포너에 넘긴다.
    ///
    /// <b>이제 자리를 정하지 않는다.</b> 예전엔 이 포인트 둘레 5m가 곧 스폰 자리였고 스포너가 8초마다 현재 리전
    /// 포인트를 플레이어 둘레 10~43m로 끌어왔다 — 그래서 리전을 옮기면 곤충이 새로 굴려졌다. 지금 자리는
    /// <see cref="InsectSpawner"/>가 리전 원판 전체에서 슬롯마다 한 번 정해 기록한다(<see cref="FieldSlot"/>).
    /// 레벨 대역(<c>requiredLevel + GetRegionLevelRange</c>)의 출처가 부트스트랩이라 표는 이 컴포넌트가 계속 나른다.
    /// 월드 좌표는 리전 정의(RegionData)를 못 찾는 씬에서만 리전 원을 어림하는 데 쓴다.
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        public string regionId;

        /// <summary>
        /// 서브에리어 전용 포인트인가.
        ///
        /// <b>regionId만으로는 구분할 수 없다</b> — 서브에리어 포인트도 부모 리전의 ID를
        /// 그대로 달고 있어서(부트스트랩이 그렇게 만든다) 리전 필터에 함께 걸린다.
        /// 그래서 명시 플래그를 둔다: 메인 필드 스폰 표를 모을 때 이걸로 거른다
        /// (전용종이 필드 풀에 섞이면 필드 한복판에 서브에리어 전용종이 뜬다).
        /// </summary>
        public bool isSubAreaPoint;

        public string[] regionInsectIds;
        public int regionMinLevel = 1;
        public int regionMaxLevel = 5;
    }
}
