using InsectGame.Data;

namespace InsectGame.NPC
{
    /// <summary>
    /// 곤충 잡는 아이 NPC의 포획 판단 규칙 — 순수 정적 로직 (EditMode 테스트 대상).
    /// UnityEngine 의존 없음. 거리/예약 등 상황 값은 호출자가 계산해서 전달한다.
    /// </summary>
    public static class NpcCatchRules
    {
        /// <summary>플레이어가 곤충에 이 거리 안으로 접근해 있으면 아이는 양보한다(가로채기 금지).</summary>
        public const float PlayerClaimRadius = 8f;

        /// <summary>아이가 곤충을 발견하는 스캔 반경.</summary>
        public const float KidSpotRadius = 12f;

        /// <summary>포획 성공 후 다음 사냥까지 기본 쿨다운(초). 튜닝 프로필로 덮어쓸 수 있음.</summary>
        public const float DefaultCatchCooldownSeconds = 45f;

        /// <summary>Rare 이상 레어도는 아이가 잡지 않고 구경만 한다(플레이어 몫 보호).</summary>
        public static bool ShouldWatchOnly(InsectRarity rarity)
        {
            return rarity >= InsectRarity.Rare;
        }

        /// <summary>
        /// 아이가 해당 곤충을 포획 대상으로 삼을 수 있는지.
        /// - Rare 이상: 구경만 (false)
        /// - 상호작용 불가(도주/전투/이미 engaged): false
        /// - 플레이어가 곤충 근처(PlayerClaimRadius 미만)에 있으면: false (플레이어 우선권)
        /// - 다른 아이가 이미 예약: false
        /// </summary>
        public static bool CanKidTarget(InsectRarity rarity, bool canBeEngaged, float playerToInsectDistance, bool reservedByOtherKid)
        {
            if (ShouldWatchOnly(rarity)) return false;
            if (!canBeEngaged) return false;
            if (playerToInsectDistance < PlayerClaimRadius) return false;
            if (reservedByOtherKid) return false;
            return true;
        }
    }
}
