using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>
    /// 명부회 보스전의 「장부」 압박 — <b>순수 계산부</b>. MonoBehaviour 없이 돌아
    /// EditMode 테스트가 씬 없이 고정한다(<c>BlightPolicy</c>·<c>UISafeLayout</c>과 같은 계열).
    ///
    /// <b>왜 이 메커니즘인가.</b> 명부회의 강령은 "모든 종을 지금 당장 장부에 올려라"다.
    /// 그러니 그들이 싸우는 방식도 <b>적는 것</b>이어야 한다 — 스탯을 부풀려 벽을 만드는 대신,
    /// 플레이어가 <b>같은 행동을 되풀이하는 것</b>을 적어 두었다가 그 자리를 친다.
    ///
    /// 그래서 이 압박의 해제법이 곧 이 게임의 주제다. 명부회는 붙잡아 가두며 적고
    /// 플레이어는 만나고 놓아주며 새긴다 — <b>되풀이하지 않는 쪽이 장부에 안 잡힌다.</b>
    /// 메커니즘이 주장을 대신 논증한다.
    ///
    /// <b>계급이 곧 속도다.</b> 임계값은 인물마다 다르고 <see cref="NPC.NpcBossDuels"/>의
    /// <c>ledgerThreshold</c>가 든다 — 여기에 인물 ID를 하나도 두지 않는 이유다
    /// (<c>BlightPolicy</c>가 리전 ID를 안 두는 것과 같다. 신원의 단일 출처는 그 표다).
    /// 갓 들어온 말단은 받아 적기만 해서 느리고, 3,000종을 적은 관장의 손은 빠르다.
    /// </summary>
    public static class LedgerPressure
    {
        /// <summary>직전과 <b>같은 행동</b>을 했을 때 장부가 차는 양.</summary>
        public const int SameActionGain = 2;

        /// <summary>행동을 <b>바꿨을 때</b> 장부가 지워지는 양. 상쇄가 아니라 완화다 —
        /// 이득(2)보다 작아야 "매 턴 번갈아 쓰면 영원히 안전"이 되지 않는다.</summary>
        public const int VariedActionRelief = 1;

        /// <summary>「장부에 올랐다」가 터진 그 턴, 보스 공격의 피해 배율.</summary>
        public const float ReadDamageMultiplier = 1.6f;

        /// <summary>임계까지 이만큼 남으면 경고 상태 — 게이지가 색을 바꾼다.</summary>
        public const int WarnMargin = 2;

        /// <summary>
        /// 장부가 작동하는 최소 임계. 이보다 낮으면 <b>피할 방법이 없어진다</b> —
        /// 2면 한 번의 반복으로 즉시 터지고, 1 이하면 첫 턴부터 매 턴 터진다.
        /// 0은 "장부 없음"(야생 전투·아이 대결)이라 별도 의미를 갖는다.
        /// </summary>
        public const int MinThreshold = 3;

        /// <summary>이 전투에 장부가 걸려 있는가. 0은 장부 없음(야생·아이 대결).</summary>
        public static bool IsActive(int threshold)
        {
            return threshold >= MinThreshold;
        }

        /// <summary>
        /// 플레이어가 한 번 행동한 뒤의 장부 값.
        ///
        /// 첫 행동은 비교할 직전이 없으므로 <paramref name="repeatedAction"/>이 false다 —
        /// 전투 시작 첫 턴에 장부가 차기 시작하면 무엇을 잘못했는지 알 길이 없다.
        /// 상한은 임계다(넘겨 쌓아 두면 터진 뒤에도 곧바로 다시 터진다).
        /// </summary>
        public static int NextTally(int tally, int threshold, bool repeatedAction)
        {
            if (!IsActive(threshold)) return 0;
            int next = tally + (repeatedAction ? SameActionGain : -VariedActionRelief);
            return Mathf.Clamp(next, 0, threshold);
        }

        /// <summary>이번 턴 보스가 「장부에 올렸다」를 터뜨리는가.</summary>
        public static bool IsFull(int tally, int threshold)
        {
            return IsActive(threshold) && tally >= threshold;
        }

        /// <summary>임계가 코앞이다 — 게이지를 붉게 물들여 <b>미리 알려 준다</b>.
        /// 예고 없이 터지면 긴장이 아니라 사고다.</summary>
        public static bool IsWarning(int tally, int threshold)
        {
            return IsActive(threshold) && !IsFull(tally, threshold)
                && tally >= threshold - WarnMargin;
        }

        /// <summary>게이지 채움 비율 0~1.</summary>
        public static float Fill01(int tally, int threshold)
        {
            if (!IsActive(threshold)) return 0f;
            return Mathf.Clamp01((float)tally / threshold);
        }

        /// <summary>터진 턴의 피해 배율, 아니면 1.</summary>
        public static float DamageMultiplier(bool triggered)
        {
            return triggered ? ReadDamageMultiplier : 1f;
        }

        /// <summary>
        /// 이 행동이 <b>즉시 피해</b>를 주는가 — 장부를 여기서만 쓴다.
        ///
        /// 배율을 걸 피해가 없는 턴(버프·회복·독 부여·기절)에 정독이 터지면 <b>아무 일도
        /// 안 일어난 채 게이지만 비워진다.</b> 플레이어 쪽에서는 경고를 보고 각오했는데
        /// 그냥 넘어간 것이라, 압박이 실력이 아니라 운으로 읽힌다 —
        /// 실측에서 정독 4회 중 1회가 그렇게 낭비됐다(평균 배율이 1.19로 희석됐다).
        ///
        /// 그래서 <b>때리는 턴까지 미룬다.</b> "적어 둔 자리를 친다"는 말 그대로,
        /// 칠 것이 없으면 아직 쓰지 않는다.
        /// <c>null</c>(쿨다운 대체 기본공격)은 피해를 주므로 true다.
        /// </summary>
        public static bool DealsImmediateDamage(Data.SkillEffectType effectType, bool hasSkill)
        {
            if (!hasSkill) return true;                       // 쿨다운 대체타 — 기본 피해
            return effectType == Data.SkillEffectType.Damage;
        }

        /// <summary>
        /// 반복 판정에 쓰는 행동 키. 스킬은 인덱스, 기본공격·도주는 음수 상수다 —
        /// <b>스킬 인덱스와 겹치지 않아야</b> "기본공격 뒤 0번 스킬"이 반복으로 잘못 잡히지 않는다.
        /// </summary>
        public const int BasicAttackKey = -1;

        /// <summary>도주 시도(실패해 턴을 넘긴 경우). 기본공격과도 다른 행동으로 센다.</summary>
        public const int EscapeKey = -2;

        /// <summary>아직 아무 행동도 없음 — 첫 행동을 반복으로 잡지 않기 위한 센티넬.</summary>
        public const int NoActionKey = int.MinValue;
    }
}
