namespace InsectGame.NPC
{
    /// <summary>
    /// 명부회 간부와의 보스 대결 정의 — storyNpcId 하나로 상대 곤충·레벨·보상을 결정한다.
    ///
    /// 아이 대결(<see cref="NpcDuelController.TryStartDuel"/>)과 나뉘는 이유:
    /// 아이는 "방금 잡은 곤충"이 상대라 매번 달라지고 레벨도 플레이어에 맞춰 흔들린다.
    /// 간부는 <b>고정 상대·고정 레벨</b>이라야 서사의 벽으로 기능한다 — 준비가 덜 되면 진다.
    ///
    /// 순수 데이터라 MonoBehaviour 밖에 둔다(EditMode 테스트가 씬 없이 표를 검증한다).
    /// 곤충 ID·아이템 ID는 각각 InsectExpansion2Definitions / ItemDatabase에 실재해야 한다.
    /// <b>고정하는 곳이 둘로 나뉜다</b>: 곤충·레벨·앵커·유일성은 <c>NpcBossDuelTests</c>가,
    /// <b>보상 아이템 실재성은 <c>quest_lint.py</c></b>가 본다(아이템 ID가 캡처아이템·상점
    /// 진열/지급·ItemDatabase 네 소스의 합집합이라 그 레지스트리를 이미 모으는 쪽에 붙였다 —
    /// C#에서 다시 모으면 사본이 생겨 어긋난다). 오타를 물면 런타임엔 조용히 실패해
    /// 승리 보상만 사라지므로 배포 전에 걸러야 한다.
    ///
    /// 먹(<c>ledger_ink</c>)은 여기 없다 — 잿불 골짜기에서 이탈해 아군이 되므로 싸울 상대가 아니다.
    /// </summary>
    public static class NpcBossDuels
    {
        public struct BossDuel
        {
            public string storyNpcId;
            public string displayName;
            public string insectId;
            public int level;
            public string rewardItemId;
            public int rewardCount;
            /// <summary>패배 후 재도전까지의 대기(초). 아이 대결(90초)보다 짧게 둘 이유가 없다.</summary>
            public float retryCooldownSeconds;
        }

        private const float RetryCooldown = 120f;

        private static readonly BossDuel[] Table =
        {
            // 집게 — 포획반장. 완력형이라 땅속을 헤집는 지네를 부린다.
            new BossDuel
            {
                storyNpcId = "ledger_grip", displayName = "집게",
                insectId = "centipede_sand", level = 54,
                rewardItemId = "net_gold", rewardCount = 2,
                retryCooldownSeconds = RetryCooldown,
            },
            // 저울 — 분류관. 곤충을 수치로만 보는 사람답게 미동도 없는 사마귀를 세운다.
            new BossDuel
            {
                storyNpcId = "ledger_scale", displayName = "저울",
                insectId = "mantis_icicle", level = 58,
                rewardItemId = "full_restore", rewardCount = 2,
                retryCooldownSeconds = RetryCooldown,
            },
            // 관장 하월 — 이름이 지워진 나방을 데리고 다닌다. 그가 만든 빈칸의 산 증거다.
            new BossDuel
            {
                storyNpcId = "ledger_chief", displayName = "관장 하월",
                insectId = "moth_effaced", level = 72,
                rewardItemId = "full_restore", rewardCount = 3,
                retryCooldownSeconds = RetryCooldown,
            },
        };

        /// <summary>표 전체 — 테스트/검증용. 호출부는 수정하지 않는다(값 복사 배열).</summary>
        public static BossDuel[] All()
        {
            BossDuel[] copy = new BossDuel[Table.Length];
            System.Array.Copy(Table, copy, Table.Length);
            return copy;
        }

        /// <summary>이 스토리 NPC가 보스 대결 상대인가.</summary>
        public static bool TryGet(string storyNpcId, out BossDuel duel)
        {
            duel = default;
            if (string.IsNullOrEmpty(storyNpcId)) return false;
            for (int i = 0; i < Table.Length; i++)
            {
                if (Table[i].storyNpcId == storyNpcId)
                {
                    duel = Table[i];
                    return true;
                }
            }
            return false;
        }

        public static bool IsBoss(string storyNpcId)
        {
            return TryGet(storyNpcId, out _);
        }
    }
}
