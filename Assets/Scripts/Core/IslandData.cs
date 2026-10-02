using System;
using System.Collections.Generic;

namespace InsectGame.Core
{
    /// <summary>섬에 놓는 물건의 분류 — 상점 탭과 같다.</summary>
    public enum IslandObjectCategory
    {
        Building,
        Furniture,
        Terrain,
        Tool,
    }

    /// <summary>
    /// 놓아두면 나는 효과. 같은 id를 여러 개 놓아도 <b>한 번만</b> 센다(<see cref="IslandYield.CollectEffects"/>) —
    /// 먹이통 열 개로 생산을 +50% 하는 섬이 되면 꾸미기가 아니라 도배가 된다.
    /// </summary>
    public enum IslandEffectKind
    {
        None,
        /// <summary>생산량 가산(0.05 = +5%).</summary>
        YieldBonus,
        /// <summary>누적 상한 시간 가산(시간).</summary>
        CapHours,
        /// <summary>친밀도가 쌓이는 속도 가산(0.25 = +25%).</summary>
        BondSpeed,
    }

    /// <summary>
    /// 카탈로그 한 줄. 가격은 둘 중 하나만 양수다 — 코인 물건과 다이아 물건을 섞지 않는다
    /// (확장만 병행가를 둔다 — <see cref="IslandCatalog.SizePrice"/>).
    /// </summary>
    public class IslandObjectDef
    {
        public string id;
        public string displayName;
        public string description;
        public IslandObjectCategory category;
        /// <summary>차지 칸(회전 0 기준). 가로 = x, 세로 = z.</summary>
        public int width = 1;
        public int depth = 1;
        public int coinPrice;
        public int gemPrice;
        public int comfort;
        public IslandEffectKind effect = IslandEffectKind.None;
        public float effectValue;
        /// <summary>이 섬 크기 단계부터 상점에 뜬다(큰 건물이 좁은 섬을 다 덮지 않게).</summary>
        public int requiredSizeLevel;
        /// <summary>true면 캐릭터가 지나갈 수 없다. 꽃밭·덤불처럼 낮은 것은 false.</summary>
        public bool blocksMovement = true;

        public bool IsPremium => gemPrice > 0;
    }

    [Serializable]
    public class IslandOwnedRecord
    {
        public string id;
        /// <summary>보유 총수 — 놓인 것까지 포함한다. 보관함 수량은 여기서 놓인 수를 뺀 값이다.</summary>
        public int count;
    }

    [Serializable]
    public class IslandPlacedRecord
    {
        public string id;
        /// <summary>차지 영역의 최소 모서리 칸. 섬 중심이 (0,0)이라 확장해도 기존 좌표가 그대로 유효하다.</summary>
        public int x;
        public int z;
        /// <summary>0~3, 90° 단위.</summary>
        public int rot;
    }

    [Serializable]
    public class IslandBondRecord
    {
        public string instanceId;
        public float hours;
    }

    /// <summary>
    /// island.json — 섬 상태 전부. 클라우드엔 이 파일이 블롭 하나(<c>GameSaveData.islandData</c>)로 올라간다.
    /// <b>새 필드는 의미 있는 기본값으로 선언할 것</b> — JsonUtility는 JSON에 없는 필드를 건드리지 않는다.
    /// </summary>
    [Serializable]
    public class IslandSave
    {
        public int version = 1;
        public int sizeLevel;
        /// <summary>기본 3칸 위에 산 방목 슬롯 수.</summary>
        public int extraSlots;
        public List<IslandOwnedRecord> owned = new List<IslandOwnedRecord>();
        public List<IslandPlacedRecord> placed = new List<IslandPlacedRecord>();
        /// <summary>방목 중인 곤충의 instanceId. 곤충은 보유 목록에 그대로 있다(배틀팀과 같은 구조).</summary>
        public List<string> released = new List<string>();
        public List<IslandBondRecord> bonds = new List<IslandBondRecord>();

        /// <summary>마지막 정산 시각(Unix 초, UTC). 0 = 한 번도 정산한 적 없음 → 첫 정산이 기준점만 잡는다.</summary>
        public long lastSettleUnix;
        /// <summary>마지막 수확 뒤로 쌓인 시간. 상한에 닿으면 생산이 멈춘다.</summary>
        public float accruedHours;
        public float pendingCandy;
        public float pendingCoin;
        public int harvestCount;

        /// <summary>가이드 진행 — <see cref="IslandGuideSteps"/>의 단계 번호. 끝났으면 guideDone.</summary>
        public int guideStep;
        public bool guideDone;
        /// <summary>스타터 키트를 받았는가. 퀘스트 보상과 별개로 섬 첫 진입에 한 번 준다.</summary>
        public bool starterGranted;
        /// <summary>다른 사람이 내 섬을 볼 수 있는가.</summary>
        public bool isPublic = true;
    }

    /// <summary>
    /// 공개용 축약본 — 서버(<c>islands/{uid}</c>)에 올라가고 방문자가 받는다.
    /// <b>instanceId·재화·개인 정보는 싣지 않는다.</b> 방목 곤충은 종·레벨·색다름만.
    /// </summary>
    [Serializable]
    public class IslandSnapshot
    {
        public int version = 1;
        public string ownerName = "";
        public int sizeLevel;
        public int comfort;
        public List<IslandPlacedRecord> placed = new List<IslandPlacedRecord>();
        public List<IslandSnapshotInsect> insects = new List<IslandSnapshotInsect>();
    }

    [Serializable]
    public class IslandSnapshotInsect
    {
        public string insectId;
        public int level = 1;
        public bool shiny;
    }
}
