using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    public readonly struct PlayerStartPose
    {
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public bool IsFallback { get; }

        public PlayerStartPose(Vector3 position, Quaternion rotation, bool isFallback)
        {
            Position = position;
            Rotation = rotation;
            IsFallback = isFallback;
        }
    }

    /// <summary>월드 정의를 기준으로 플레이어의 기본 시작 위치와 방향을 계산한다.</summary>
    public static class PlayerStartPlacement
    {
        private const string MeadowRegionId = "meadow";
        private const float EntranceClearance = 2f;
        private const float GroundOffset = 0.1f;

        private static readonly PlayerStartPose fallbackPose = new PlayerStartPose(
            new Vector3(0f, GroundOffset, 0f), Quaternion.identity, true);

        public static PlayerStartPose FallbackPose => fallbackPose;

        public static PlayerStartPose ResolveMainVillageEntrance(RegionData[] regions)
        {
            RegionData meadow = FindRegion(regions, MeadowRegionId);
            if (meadow == null) return FallbackPose;

            Vector3 villageCenter = VillageBuilder.GetMainVillageCenter(
                meadow.centerPosition, meadow.radius);
            Vector3 position = villageCenter + Vector3.right *
                (VillageBuilder.MainVillageFootprintRadius + EntranceClearance);
            position.y = meadow.centerPosition.y + GroundOffset;

            Vector3 facingDirection = villageCenter - position;
            facingDirection.y = 0f;
            if (facingDirection.sqrMagnitude <= Mathf.Epsilon) return FallbackPose;

            Quaternion rotation = Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
            return new PlayerStartPose(position, rotation, false);
        }

        private static RegionData FindRegion(RegionData[] regions, string regionId)
        {
            if (regions == null) return null;

            foreach (RegionData region in regions)
            {
                if (region != null && region.regionId == regionId) return region;
            }

            return null;
        }
    }
}
