using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>
    /// 방금 잡은 개체의 성질 — <see cref="QuestType.CaptureTrait"/> 판정의 입력.
    /// 포획 지점(<c>CaptureController</c>·전투 포획)이 <see cref="From"/>으로 만들어 넘긴다.
    /// </summary>
    public struct CaptureFacts
    {
        public InsectRarity rarity;
        public InsectElement primary;
        public InsectElement secondary;
        /// <summary>이 개체의 몸길이(mm) — <see cref="InsectSizeCalculator.SizeMm"/>.</summary>
        public float sizeMm;
        /// <summary>종 기준 대비 몸길이 배율(0.75~1.25) — <see cref="InsectSizeCalculator.SizeRatio"/>.</summary>
        public float sizeRatio;
        public bool shiny;
        /// <summary>
        /// 크기를 아는가. 저장된 개체(<see cref="PlayerInsectData"/>)가 없으면 몸길이는 중간값으로 채워질 뿐
        /// 실제 크기가 아니다 — 그런 포획이 크기 조건을 채우면 안 되므로 판정이 이 값을 본다.
        /// </summary>
        public bool sizeKnown;

        /// <summary>
        /// <paramref name="captured"/>가 null이면(개체 저장에 실패한 포획) 크기는 모르는 것으로 두고
        /// 이로치는 <paramref name="shinyFallback"/>을 쓴다. 등급·속성 조건은 종 데이터만으로 판정되므로 그대로 센다.
        /// </summary>
        public static CaptureFacts From(InsectData data, PlayerInsectData captured, bool shinyFallback = false)
        {
            var facts = new CaptureFacts();
            if (data == null) return facts;
            facts.rarity = data.rarity;
            facts.primary = data.primaryType;
            facts.secondary = data.secondaryType;
            facts.sizeKnown = captured != null;
            facts.sizeMm = InsectSizeCalculator.SizeMm(data, captured);
            facts.sizeRatio = InsectSizeCalculator.SizeRatio(data, captured);
            facts.shiny = captured != null ? captured.isShiny : shinyFallback;
            return facts;
        }
    }

    /// <summary>
    /// 방금 이긴 전투의 모습 — <see cref="QuestType.BattleFeat"/> 판정의 입력.
    /// 전투 컨트롤러가 <b>이긴 순간</b> 만들어 넘긴다(적이 풀로 돌아가기 전의 시작 시점 스냅샷으로).
    /// </summary>
    public struct BattleFacts
    {
        public InsectRarity enemyRarity;
        public InsectElement enemyPrimary;
        public InsectElement enemySecondary;
        public int enemyLevel;
        /// <summary>끝났을 때 싸우고 있던 내 곤충의 레벨.</summary>
        public int playerLevel;
        /// <summary>이 전투에서 내가 행동한 횟수(기술·기본 공격·스턴으로 건너뛴 차례 포함).</summary>
        public int playerActions;
        /// <summary>끝났을 때 내 곤충의 남은 HP 비율(0~1).</summary>
        public float playerHpRatio;

        public static BattleFacts From(InsectData enemy, int enemyLevel, int playerLevel,
            int playerActions, int playerHp, int playerMaxHp)
        {
            var facts = new BattleFacts
            {
                enemyLevel = enemyLevel,
                playerLevel = playerLevel,
                playerActions = playerActions,
                playerHpRatio = playerMaxHp > 0 ? Mathf.Clamp01((float)playerHp / playerMaxHp) : 0f,
            };
            if (enemy != null)
            {
                facts.enemyRarity = enemy.rarity;
                facts.enemyPrimary = enemy.primaryType;
                facts.enemySecondary = enemy.secondaryType;
            }
            return facts;
        }
    }

    /// <summary>
    /// 조건부 퀘스트(<see cref="QuestType.CaptureTrait"/>·<see cref="QuestType.BattleFeat"/>)의 <b>순수</b> 판정.
    /// MonoBehaviour와 떼어 놓아 테스트로 고정한다(<see cref="QuestRegionGate"/>와 같은 성격).
    ///
    /// <b>조건 필드의 0·None·false는 "조건 없음"이다.</b> 채운 조건은 전부 맞아야 한다(AND).
    /// 그래서 조건을 하나도 안 채운 조건부 퀘스트는 무엇이든 센다 — 의도한 게 아니라 저작 실수이므로
    /// <c>quest_lint</c> 검사 13이 막는다.
    /// </summary>
    public static class QuestTraitRules
    {
        // 화면에 찍히는 몸길이는 소수 첫째 자리다(InsectSizeCalculator.SizeLabel). "40.0mm"로 보이는 개체가
        // 실제로는 39.96이라 40 이상 퀘스트에 안 세어지면 플레이어에게는 버그다 — 보이는 값 기준으로 잰다.
        private const float SizeRoundStep = 10f;

        // 배율은 정수 롤(0~100)에서 나온 Lerp라 1.2가 1.1999999로 떨어질 수 있다.
        private const float RatioEpsilon = 1e-4f;

        public static bool HasCondition(TutorialQuest q)
        {
            if (q == null) return false;
            return q.minSizeMm > 0f || q.maxSizeMm > 0f
                || q.minSizeRatio > 0f || q.maxSizeRatio > 0f
                || q.requiredElement != InsectElement.None
                || q.requireShiny
                || q.minRarity > InsectRarity.Common
                || q.minLevelEdge > 0 || q.maxTurns > 0 || q.minHpPercent > 0
                || q.resetOnLoss;
        }

        public static bool Matches(TutorialQuest q, CaptureFacts f)
        {
            if (q == null || q.type != QuestType.CaptureTrait) return false;
            if (f.rarity < q.minRarity) return false;

            bool wantsSize = q.minSizeMm > 0f || q.maxSizeMm > 0f || q.minSizeRatio > 0f || q.maxSizeRatio > 0f;
            if (wantsSize && !f.sizeKnown) return false;

            float mm = Mathf.Round(f.sizeMm * SizeRoundStep) / SizeRoundStep;
            if (q.minSizeMm > 0f && mm < q.minSizeMm) return false;
            if (q.maxSizeMm > 0f && mm > q.maxSizeMm) return false;
            if (q.minSizeRatio > 0f && f.sizeRatio + RatioEpsilon < q.minSizeRatio) return false;
            if (q.maxSizeRatio > 0f && f.sizeRatio - RatioEpsilon > q.maxSizeRatio) return false;

            if (!HasElement(q.requiredElement, f.primary, f.secondary)) return false;
            if (q.requireShiny && !f.shiny) return false;
            return true;
        }

        public static bool Matches(TutorialQuest q, BattleFacts f)
        {
            if (q == null || q.type != QuestType.BattleFeat) return false;
            if (f.enemyRarity < q.minRarity) return false;
            if (!HasElement(q.requiredElement, f.enemyPrimary, f.enemySecondary)) return false;

            if (q.minLevelEdge > 0 && f.enemyLevel - f.playerLevel < q.minLevelEdge) return false;
            if (q.maxTurns > 0 && f.playerActions > q.maxTurns) return false;
            // 퍼센트는 정수 비교로 — 70%를 "정확히 70.0"으로 끝낸 전투가 부동소수점에 떨어지면 안 된다.
            if (q.minHpPercent > 0 && Mathf.RoundToInt(f.playerHpRatio * 100f) < q.minHpPercent) return false;
            return true;
        }

        private static bool HasElement(InsectElement wanted, InsectElement primary, InsectElement secondary)
        {
            if (wanted == InsectElement.None) return true;
            return primary == wanted || secondary == wanted;
        }
    }
}
