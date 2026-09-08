using System.Collections.Generic;

namespace InsectGame.Core
{
    public enum QuestRewardKind
    {
        Candy,
        Exp,
        Item,
        Insect,
    }

    public struct QuestRewardEntry
    {
        public QuestRewardKind Kind;
        public string Label;
        public int Amount;
    }

    /// <summary>
    /// 퀘스트 보상을 사람이 읽는 형태로 바꾸는 순수 함수 모음.
    ///
    /// 왜 따로 빼는가 — 옛 <c>TutorialQuestUI</c>는 완료 배너에서 캔디·경험치·곤충 셋만
    /// 직접 문자열로 조립했고 <c>rewardItemId</c>/<c>rewardItemCount</c>는 아예 읽지도
    /// 않았다. 그래서 아이템을 주는 퀘스트 7개(net_gold·binding_net·spirit_blessing 등)가
    /// 실제로는 아이템을 지급하면서 화면에는 "캔디 20 + 경험치 25"만 띄웠다.
    /// 표시 지점이 늘 때마다 같은 실수를 반복하지 않도록 조립을 여기 한 곳에 모은다.
    ///
    /// 포함 조건은 <c>TutorialQuestManager.GrantRewards</c>의 지급 조건과 <b>정확히 같다.</b>
    /// 한쪽만 바뀌면 "받았는데 안 보이거나 / 보이는데 안 주는" 어긋남이 생긴다.
    /// </summary>
    public static class QuestRewardFormatter
    {
        // Format()이 매번 리스트를 할당하지 않도록 재사용하는 버퍼. Unity는 단일 스레드라 안전.
        private static readonly List<QuestRewardEntry> formatBuffer = new List<QuestRewardEntry>(4);

        /// <summary>
        /// 지급될 보상을 <paramref name="into"/>에 채운다. 호출부가 리스트를 재사용하면
        /// OnGUI에서 매 프레임 할당이 생기지 않는다(리스트는 여기서 Clear한다).
        /// </summary>
        /// <param name="itemNameResolver">
        /// 아이템 ID → 표시명. null이거나 빈 문자열을 돌려주면 ID를 그대로 쓴다.
        /// </param>
        public static void Collect(
            TutorialQuest quest,
            System.Func<string, string> itemNameResolver,
            List<QuestRewardEntry> into)
        {
            if (into == null) return;
            into.Clear();
            if (quest == null) return;

            if (HasCandy(quest))
            {
                into.Add(new QuestRewardEntry
                {
                    Kind = QuestRewardKind.Candy,
                    Label = "캔디 " + quest.rewardCandy,
                    Amount = quest.rewardCandy,
                });
            }

            if (HasExp(quest))
            {
                into.Add(new QuestRewardEntry
                {
                    Kind = QuestRewardKind.Exp,
                    Label = "경험치 " + quest.rewardExp,
                    Amount = quest.rewardExp,
                });
            }

            if (HasItem(quest))
            {
                string name = itemNameResolver != null ? itemNameResolver(quest.rewardItemId) : null;
                if (string.IsNullOrEmpty(name)) name = quest.rewardItemId;
                into.Add(new QuestRewardEntry
                {
                    Kind = QuestRewardKind.Item,
                    Label = quest.rewardItemCount > 1 ? name + " ×" + quest.rewardItemCount : name,
                    Amount = quest.rewardItemCount,
                });
            }

            // 곤충은 ID 유무로 판정한다 — 표시명이 비어 있어도 지급은 되기 때문이다.
            // (옛 UI는 표시명만 보고 판정해 이름 없는 보상 곤충을 통째로 숨겼다.)
            if (HasInsect(quest))
            {
                string name = string.IsNullOrEmpty(quest.rewardInsectDisplayName)
                    ? quest.rewardInsectId
                    : quest.rewardInsectDisplayName;
                // 레벨도 붙인다 — GrantRewards가 Mathf.Max(1, rewardInsectLevel)로 실제 적용하는데
                // 화면엔 종만 떠서 q_approach의 Lv.6 장수풍뎅이가 그냥 "장수풍뎅이"로 보였다.
                // 1이면(=기본) 군더더기라 생략한다. 음수·0도 지급은 1이므로 같은 취급.
                into.Add(new QuestRewardEntry
                {
                    Kind = QuestRewardKind.Insect,
                    Label = quest.rewardInsectLevel > 1
                        ? name + " Lv." + quest.rewardInsectLevel
                        : name,
                    Amount = 1,
                });
            }
        }

        // ── 포함 조건 (GrantRewards와 1:1) ──
        // Collect와 HasAny가 이 술어들을 공유한다. 조건을 두 곳에 복사해 두면 한쪽만 바뀌어
        // "받았는데 안 보이거나 / 보이는데 안 주는" 어긋남이 생긴다 — 이 파일이 존재하는 이유다.

        private static bool HasCandy(TutorialQuest quest) => quest.rewardCandy > 0;

        private static bool HasExp(TutorialQuest quest) => quest.rewardExp > 0;

        private static bool HasItem(TutorialQuest quest) =>
            !string.IsNullOrEmpty(quest.rewardItemId) && quest.rewardItemCount > 0;

        private static bool HasInsect(TutorialQuest quest) =>
            !string.IsNullOrEmpty(quest.rewardInsectId);

        /// <summary>보상 한 줄 요약 — "캔디 5 + 경험치 10 + 황금 채집망 ×1". 없으면 빈 문자열.</summary>
        public static string Format(TutorialQuest quest, System.Func<string, string> itemNameResolver)
        {
            Collect(quest, itemNameResolver, formatBuffer);
            if (formatBuffer.Count == 0) return string.Empty;

            string text = formatBuffer[0].Label;
            for (int i = 1; i < formatBuffer.Count; i++)
            {
                text += " + " + formatBuffer[i].Label;
            }
            return text;
        }

        /// <summary>지급될 보상이 하나라도 있는지. 표시 여부 판단용.</summary>
        public static bool HasAny(TutorialQuest quest)
        {
            if (quest == null) return false;
            return HasCandy(quest) || HasExp(quest) || HasItem(quest) || HasInsect(quest);
        }
    }
}
