using System;

namespace InsectGame.NPC
{
    /// <summary>
    /// 라온과의 라이벌 대결 — 장마다 한 번씩, 스토리를 따라 강해진다.
    ///
    /// 1장에서 라온이 "승부다!"를 외치고 엔딩 뒤에 "이번엔 진짜 겨루자. 도전은 네 쪽에서 해"
    /// (<c>post_rival_rematch</c>)로 끝나는데, 그 사이에 라온과 실제로 붙는 길이 하나도 없었다.
    ///
    /// <b>간부 표(<see cref="NpcBossDuels"/>)와 나눈 이유</b>: 간부는 인물당 한 번이고 장부를 들고
    /// 오염 거점과 얽힌다. 라온은 한 인물이 <b>여러 단계</b>로 싸우고 장부가 없다. 같은 표에 두면
    /// "명부회 보스는 전원 장부를 든다"(NpcBossDuelTests)가 깨지고, storyNpcId 하나로 상대를 찾는
    /// <c>TryGet</c>이 단계를 못 가른다.
    ///
    /// 라온은 전부 <b>여치</b>를 부린다 — 풀벌레 소리만 듣고 "저건 여치" 하던 할머니(<c>camp_pond</c>)의 손자다.
    ///
    /// 격파 기록은 간부 격파 집합(<c>NpcDuelController</c>)에 단계 ID로 함께 적는다. 그 집합이 이미
    /// 클라우드에 오르므로 세이브 구조를 늘리지 않는다 — 단계 ID는 <see cref="StagePrefix"/>로 시작해
    /// 인물 ID와 겹치지 않는다. 순수 데이터 + 순수 선택이다(<c>NpcRivalDuelTests</c>).
    /// </summary>
    public static class NpcRivalDuels
    {
        public const string StagePrefix = "rival_";
        /// <summary>패배 후 재도전 대기(초) — 아이 대결과 같다.</summary>
        public const float RetryCooldownSeconds = 90f;

        public struct Stage
        {
            /// <summary>단계 ID — <see cref="DuelBanter"/> 키이자 격파 기록 ID.</summary>
            public string stageId;
            public string storyNpcId;
            /// <summary>이 리전에 서 있을 때만 — 비면 어디서든(엔딩 뒤 재대결).</summary>
            public string regionId;
            /// <summary>이 비트를 봤으면 열린다.</summary>
            public string openBeatId;
            /// <summary>이 비트를 봤으면 닫힌다 — 비면 안 닫힌다.</summary>
            public string closeBeatId;
            public string insectId;
            public int level;
            public string rewardItemId;
            public int rewardCount;
        }

        private static readonly Stage[] Table =
        {
            // 연못 — 라온이 "물가 곤충들은 초원 애들이랑 또 다르지?"로 다시 나타난 뒤(ch2_water). 구간 Lv.6~20.
            new Stage
            {
                stageId = "rival_pond", storyNpcId = "catcher_rival", regionId = "pond",
                openBeatId = "ch2_water",
                insectId = "katydid_leaf", level = 14,
                rewardItemId = "net_silver", rewardCount = 2,
            },
            // 습지 — 셋이 뭉친 뒤(ch4_reach_swamp). 구간 Lv.20~37.
            new Stage
            {
                stageId = "rival_swamp", storyNpcId = "catcher_rival", regionId = "swamp",
                openBeatId = "ch4_reach_swamp",
                insectId = "katydid_leaf", level = 30,
                rewardItemId = "wound_salve_great", rewardCount = 3,
            },
            // 서릿길 — 라온이 다치기 전 마지막 기회. 잿불 골짜기 갱도(ch10_confront) 뒤로는 닫힌다 —
            // 월드의 라온 앵커는 그대로 서 있어서, 닫지 않으면 뼈가 상한 라온과 싸우게 된다. 구간 Lv.50~56.
            new Stage
            {
                stageId = "rival_frost", storyNpcId = "catcher_rival", regionId = "frostline",
                openBeatId = "ch9_arrive", closeBeatId = "ch10_confront",
                insectId = "katydid_snowfield", level = 55,
                rewardItemId = "full_restore", rewardCount = 1,
            },
            // 엔딩 뒤 — "동네 최강자 자리, 내가 먼저 걸어 둘게"(post_rival_rematch). 관장(72)보다 위.
            new Stage
            {
                stageId = "rival_final", storyNpcId = "catcher_rival", regionId = "",
                openBeatId = "post_rival_rematch",
                insectId = "katydid_canopy", level = 76,
                rewardItemId = "full_restore", rewardCount = 3,
            },
        };

        /// <summary>표 전체 — 테스트/검증용(값 복사 배열).</summary>
        public static Stage[] All()
        {
            Stage[] copy = new Stage[Table.Length];
            Array.Copy(Table, copy, Table.Length);
            return copy;
        }

        public static bool TryGetStage(string stageId, out Stage stage)
        {
            for (int i = 0; i < Table.Length; i++)
            {
                if (Table[i].stageId == stageId)
                {
                    stage = Table[i];
                    return true;
                }
            }
            stage = default;
            return false;
        }

        /// <summary>
        /// 지금 이 인물에게 걸 수 있는 단계 — 표 순서대로 첫 번째. 열렸고, 안 닫혔고, 리전이 맞고,
        /// 아직 안 이긴 단계다. 앞 단계를 건너뛴 세이브도 뒤 단계가 막히지 않는다(각 단계는 독립이다) —
        /// 엔딩 뒤 연못에 가면 연못 단계가, 다른 곳에선 재대결이 뜬다.
        /// </summary>
        public static bool TrySelect(string storyNpcId, string currentRegionId,
            Func<string, bool> beatSeen, Func<string, bool> stageDefeated, out Stage stage)
        {
            stage = default;
            if (string.IsNullOrEmpty(storyNpcId) || beatSeen == null) return false;
            for (int i = 0; i < Table.Length; i++)
            {
                Stage s = Table[i];
                if (s.storyNpcId != storyNpcId) continue;
                if (!string.IsNullOrEmpty(s.regionId) && s.regionId != currentRegionId) continue;
                if (!beatSeen(s.openBeatId)) continue;
                if (!string.IsNullOrEmpty(s.closeBeatId) && beatSeen(s.closeBeatId)) continue;
                if (stageDefeated != null && stageDefeated(s.stageId)) continue;
                stage = s;
                return true;
            }
            return false;
        }
    }
}
