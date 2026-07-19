using System.Collections.Generic;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>월드 상호작용 포인트 종류 — 마을 건물 기능 연결용.</summary>
    public enum InteractionKind
    {
        ItemShop,
        Training,
        Gacha,
        Hospital
    }

    /// <summary>
    /// VillageBuilder가 생성하는 상호작용 지점 정의.
    /// WorldInteractionController가 근접 스캔 + E키/터치 발동에 소비한다.
    /// </summary>
    [System.Serializable]
    public class InteractionPointDef
    {
        public string id;
        public Vector3 worldPosition;
        public float radius = 2.5f;
        public string label;
        public InteractionKind kind;
    }

    public enum NpcKind
    {
        Villager,
        CatcherKid,
        StoryNpc
    }

    /// <summary>NPC 스폰 위치 정의 — VillageBuilder가 생성, NpcManager가 소비.</summary>
    [System.Serializable]
    public class NpcSpawnAnchor
    {
        public Vector3 position;
        public NpcKind kind;
        public string regionId;
        public float wanderRadius = 8f;
        // StoryNpc 전용 — 어르신/라온/세라 식별자(village_elder/catcher_rival/ruins_scholar).
        // 다가가 대화하면 StoryDirector NpcTalk 트리거로 해당 스토리를 발동한다.
        public string storyNpcId;
    }

    /// <summary>VillageBuilder.Build() 결과 — 부트스트랩이 NpcManager/WorldInteractionController에 전달.</summary>
    public class VillageBuildResult
    {
        public readonly List<NpcSpawnAnchor> npcAnchors = new List<NpcSpawnAnchor>();
        public readonly List<InteractionPointDef> interactions = new List<InteractionPointDef>();
    }
}
