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

            /// <summary>
            /// 최종전인가 — 보스 BGM을 간부 테마와 가른다. 호출부가 storyNpcId 문자열을
            /// 다시 비교하지 않게 표가 직접 말한다(문자열 비교는 표가 바뀌면 조용히 어긋난다).
            /// </summary>
            public bool isFinal;

            /// <summary>
            /// 「장부」가 차는 임계 — <b>이 인물이 얼마나 빨리 적는가</b>이고, 곧 계급이다.
            /// 플레이어가 같은 행동을 되풀이하면 차고(+2), 바꾸면 지워진다(-1). 임계에 닿으면
            /// 그 턴 보스의 공격이 <c>LedgerPressure.ReadDamageMultiplier</c>배가 된다.
            ///
            /// <b>신원은 여기에만 둔다.</b> <c>LedgerPressure</c>는 인물 ID를 하나도 모르는
            /// 순수 계산부다 — 임계를 그쪽에 switch로 박으면 이 표와 두 벌이 되어 어긋난다
            /// (<c>BlightPolicy</c>가 리전 ID를 안 두는 것과 같은 이유).
            ///
            /// 0이면 장부 없음. 다만 <b>명부회 보스는 전원 값을 갖는다</b> —
            /// 이 압박이 조직의 서명이라, 하나가 비면 그 인물만 다른 조직 사람처럼 싸운다
            /// (<c>NpcBossDuelTests</c>가 전원 <c>MinThreshold</c> 이상을 강제한다).
            /// </summary>
            public int ledgerThreshold;
        }

        private const float RetryCooldown = 120f;

        private static readonly BossDuel[] Table =
        {
            // ── 1막 하수 2인 ── 정체가 밝혀지기 전이라 이름 대신 인상으로 부른다.
            //
            // **레벨을 간부(54)와 크게 벌린다.** 1막 유적 구간이 Lv.28~35이라 그 위에 살짝 얹고,
            // 2막에서 집게를 만나면 20레벨 가까이 뛰어 "급이 다르다"가 숫자로 체감된다.
            // 하수를 강하게 만들면 1막에서 막히고, 간부와 비슷하게 두면 조직의 위계가 사라진다.
            //
            // 부리는 곤충도 하수답게 흔한 종이다 — 간부는 사막지네·고드름사마귀처럼 그 지역
            // 고유종을 쓰는데, 이들은 어디서나 잡히는 종을 그물로 쓸어 담아 쓴다.
            // 그 대비가 "장부에 올리기만 하면 된다"는 태도를 말해 준다.
            new BossDuel
            {
                storyNpcId = "ledger_thug_cord", displayName = "검은 옷의 사내",
                insectId = "hornet_asian", level = 34,
                rewardItemId = "net_silver", rewardCount = 2,
                retryCooldownSeconds = RetryCooldown,
                ledgerThreshold = 7,
            },
            new BossDuel
            {
                storyNpcId = "ledger_thug_rule", displayName = "검은 옷의 여자",
                insectId = "mantis_green", level = 32,
                rewardItemId = "wound_salve_great", rewardCount = 3,
                retryCooldownSeconds = RetryCooldown,
                ledgerThreshold = 7,
            },
            // 핀 — 숲 그물터 말단. **하수 중에서도 아래다.** 숲 입장이 Lv.12라 그 위에 살짝만
            // 얹는다(16) — 여기서 막히면 1막 초입에서 진행이 서고, 사내·여자(34/32)와 같은 급으로
            // 두면 "말단"이라는 배치 자체가 무너진다. 세 하수의 16/32/34가 곧 그들의 위계다.
            // 부리는 곤충도 숲 어디서나 우는 여름매미다 — 그물에 걸린 걸 그대로 쓴다.
            new BossDuel
            {
                storyNpcId = "ledger_thug_pin", displayName = "검은 옷의 청년",
                insectId = "cicada_summer", level = 16,
                rewardItemId = "net_silver", rewardCount = 1,
                retryCooldownSeconds = RetryCooldown,
                // 장부를 **가장 느리게** 쓴다(9). 여기가 플레이어가 이 압박을 처음 만나는 자리라
                // 실수해도 배울 여유가 있어야 한다 — 받아 적기만 하는 말단이라는 설정과도 맞는다.
                ledgerThreshold = 9,
            },
            // 집게 — 포획반장. 완력형이라 땅속을 헤집는 지네를 부린다.
            new BossDuel
            {
                storyNpcId = "ledger_grip", displayName = "집게",
                insectId = "centipede_sand", level = 54,
                rewardItemId = "net_gold", rewardCount = 2,
                retryCooldownSeconds = RetryCooldown,
                ledgerThreshold = 6,
            },
            // 저울 — 분류관. 곤충을 수치로만 보는 사람답게 미동도 없는 사마귀를 세운다.
            new BossDuel
            {
                storyNpcId = "ledger_scale", displayName = "저울",
                insectId = "mantis_icicle", level = 58,
                rewardItemId = "full_restore", rewardCount = 2,
                retryCooldownSeconds = RetryCooldown,
                // 분류관이라 **간부 중에서도 빠르다**(5). 곤충을 등급과 수치로만 보는 사람이니
                // 플레이어의 수도 수로 본다 — 같은 수가 두 번 나오는 순간 적힌다.
                ledgerThreshold = 5,
            },
            // 관장 하월 — 이름이 지워진 나방을 데리고 다닌다. 그가 만든 빈칸의 산 증거다.
            new BossDuel
            {
                storyNpcId = "ledger_chief", displayName = "관장 하월",
                insectId = "moth_effaced", level = 72,
                rewardItemId = "full_restore", rewardCount = 3,
                retryCooldownSeconds = RetryCooldown,
                isFinal = true,
                // 3,000종을 적은 손이다 — **가장 빠르다**(4). MinThreshold(3) 바로 위이고
                // 더 내리면 피할 방법이 사라진다(반복 두 번이면 이미 터진다).
                ledgerThreshold = 4,
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
