using System.Collections.Generic;

namespace InsectGame.NPC
{
    /// <summary>
    /// 대결 상대가 전투 중에 하는 말 — 컷인(칭호·도발), 전투 중 한마디, 결과 한마디.
    ///
    /// 예전엔 간부전도 야생전과 똑같이 "○○와 마주쳤다" 한 줄로 시작해서, 관장과의 마지막 대결이
    /// 필드의 인분무와 구별되지 않았다. 대사는 스토리 비트(대결 전 <c>talk_*</c>·대결 뒤 <c>duel_*_win</c>)가
    /// 이미 들고 있으니 여기엔 <b>전투 한가운데에서만 할 수 있는 말</b>만 둔다 — 겹치면 같은 말을 두 번 듣는다.
    ///
    /// 키는 대결 ID다. 간부는 <c>storyNpcId</c> 그대로이고(<see cref="NpcBossDuels"/>), 한 인물이 여러 번
    /// 싸우는 경우(라이벌 단계)는 별도 키를 쓴다 — 그래서 초상 인물(<see cref="Lines.npcId"/>)을 따로 든다.
    ///
    /// 순수 데이터 + 순수 계산이다(<c>DuelBanterTests</c>가 씬 없이 검증한다). 그리기는 <c>BattleScreenUI.Duel</c>.
    /// </summary>
    public static class DuelBanter
    {
        public struct Lines
        {
            /// <summary>초상 인물 — <c>NpcDialogueUI</c>의 스토리 초상 표와 같은 ID.</summary>
            public string npcId;
            /// <summary>표시명 — 스토리 대사창의 이름표와 같아야 한다(테스트가 대조한다).</summary>
            public string name;
            /// <summary>컷인 칭호. 1막 하수는 정체가 밝혀지기 전이라 조직명을 쓰지 않는다.</summary>
            public string title;
            /// <summary>컷인 도발 — 첫 합 전에.</summary>
            public string intro;
            /// <summary>상대 곤충이 절반 아래로 — 흔들리기 시작한다.</summary>
            public string half;
            /// <summary>상대 곤충이 위기 — 무너지기 직전.</summary>
            public string crisis;
            /// <summary>내 곤충이 위기 — 상대가 몰아붙인다.</summary>
            public string pressing;
            /// <summary>플레이어가 이겼을 때 상대의 마지막 말.</summary>
            public string defeat;
            /// <summary>플레이어가 졌을 때 상대의 말.</summary>
            public string victory;
        }

        public enum Moment { None, Half, Crisis, Pressing }

        public const float HalfRatio = 0.5f;
        public const float CrisisRatio = 0.2f;
        public const float PressingRatio = 0.25f;

        /// <summary>한 전투에서 이미 나온 순간 — 각 순간은 한 번만 말한다.</summary>
        public struct Tracker
        {
            public bool half;
            public bool crisis;
            public bool pressing;
        }

        /// <summary>
        /// 이번 HP 비율로 <b>새로</b> 넘은 순간 하나. 한 번에 절반과 위기를 같이 넘으면 위기만 말한다
        /// (절반 대사는 건너뛴다 — "제법이군" 다음 프레임에 "이럴 리가"가 겹치면 둘 다 안 읽힌다).
        /// 상대가 먼저다: 같은 교환에서 둘 다 넘었으면 상대 쪽을 말하고, 내 쪽은 다음 호출에서 나온다.
        /// 0 이하(쓰러짐)는 말하지 않는다 — 그 자리는 결과 대사가 맡는다.
        /// </summary>
        public static Moment Next(ref Tracker tracker, float enemyRatio, float playerRatio)
        {
            if (enemyRatio > 0f)
            {
                if (!tracker.crisis && enemyRatio <= CrisisRatio)
                {
                    tracker.crisis = true;
                    tracker.half = true;
                    return Moment.Crisis;
                }
                if (!tracker.half && enemyRatio <= HalfRatio)
                {
                    tracker.half = true;
                    return Moment.Half;
                }
            }
            if (!tracker.pressing && playerRatio > 0f && playerRatio <= PressingRatio)
            {
                tracker.pressing = true;
                return Moment.Pressing;
            }
            return Moment.None;
        }

        public static string LineFor(Lines lines, Moment moment)
        {
            switch (moment)
            {
                case Moment.Half: return lines.half;
                case Moment.Crisis: return lines.crisis;
                case Moment.Pressing: return lines.pressing;
                default: return null;
            }
        }

        public static bool TryGet(string duelId, out Lines lines)
        {
            lines = default;
            return !string.IsNullOrEmpty(duelId) && Table.TryGetValue(duelId, out lines);
        }

        /// <summary>표의 키 전체 — 테스트용.</summary>
        public static IEnumerable<string> Ids => Table.Keys;

        private static readonly Dictionary<string, Lines> Table = new Dictionary<string, Lines>
        {
            // ── 1막 하수 ── 이름도 조직도 모르는 채 싸운다. 칭호는 그들이 한 일로 부른다.
            ["ledger_thug_pin"] = new Lines
            {
                npcId = "ledger_thug_pin", name = "검은 옷의 청년", title = "숲 그물터 당번",
                intro = "…싸우라니까 싸우는 거야. 원망은 위에다 해.",
                half = "…생각보다 아프네.",
                crisis = "잠깐, 이건 내 몫이 아니라니까…!",
                pressing = "봐, 그물이 제일 빨라. 한 번에 끝나잖아.",
                defeat = "…졌다. 그물은 네가 걷어. 난 모르는 일이야.",
                victory = "…미안. 치라는 대로 칠 뿐이야.",
            },
            ["ledger_thug_rule"] = new Lines
            {
                npcId = "ledger_thug_rule", name = "검은 옷의 여자", title = "산길을 막는 자",
                intro = "하나씩은 느리다고 했지. 보여 주지.",
                half = "…제법이네. 그래도 방법이 틀렸어.",
                crisis = "이럴 리가— 계산이 안 맞아.",
                pressing = "봐. 한 번에 거두는 쪽이 빠르다고.",
                defeat = "…오늘 장부엔 내가 적히겠군. 진 쪽으로.",
                victory = "지키는 쪽은 우리야. 비켜.",
            },
            ["ledger_thug_cord"] = new Lines
            {
                npcId = "ledger_thug_cord", name = "검은 옷의 사내", title = "이름을 묻지 않는 자",
                intro = "이름은 필요 없다. 종만 대라.",
                half = "…종이 아니라 이름으로 싸우는군.",
                crisis = "이 녀석… 장부에 없는 움직임이다.",
                pressing = "끝이다. 곧 장부에 오를 테니.",
                defeat = "…이름이 이겼군. 적어 두지.",
                victory = "종 하나. 적었다.",
            },
            // ── 2막 명부회 ── 대결 뒤 스토리 비트(duel_*_win)가 바로 이어지므로 패배 대사는 짧게 끊는다.
            ["ledger_grip"] = new Lines
            {
                npcId = "ledger_grip", name = "집게", title = "명부회 포획반장",
                intro = "말은 끝났다. 손으로 증명해라.",
                half = "…버티는군. 그물에 걸린 놈들은 이렇게 안 버텼는데.",
                crisis = "이 손이… 밀린다고?",
                pressing = "그래, 그거다. 걸린 놈은 결국 이렇게 된다.",
                defeat = "크윽… 지네가 먼저 물러서다니.",
                victory = "봐라. 한 마리씩으로는 안 된다.",
            },
            ["ledger_scale"] = new Lines
            {
                npcId = "ledger_scale", name = "저울", title = "명부회 분류관",
                intro = "재 보자. 네 쪽 말이 몇 근인지.",
                half = "…오차 범위 밖이군.",
                crisis = "수치가 맞지 않는다. 다시 잰다—",
                pressing = "예상대로다. 네 곤충의 등급은 거기까지다.",
                defeat = "……측정 종료.",
                victory = "감상은 수치를 못 이긴다.",
            },
            ["ledger_chief"] = new Lines
            {
                npcId = "ledger_chief", name = "관장 하월", title = "명부회 관장",
                intro = "마지막 수업이다. 첫 장의 아이가 상대해 주지.",
                half = "…삼십 년 전에도 이 아이는 이렇게 날았지.",
                crisis = "그래. 한 마리씩 만난 손이… 이렇게 무겁구나.",
                pressing = "서두르게. 늦으면 전부 놓친다 — 나처럼.",
                defeat = "그 손을… 놓지 말게.",
                victory = "아직이군. …다시 오게.",
            },
            // ── 라온 라이벌(NpcRivalDuels) ── 한 인물이 단계마다 싸우므로 키가 단계 ID다.
            // 이기고 지는 말이 적대가 아니라 장난스럽다 — 라온은 끝까지 "누가 더 구하나"의 경쟁자다.
            ["rival_pond"] = new Lines
            {
                npcId = "catcher_rival", name = "라온", title = "초원의 라이벌",
                intro = "승부다! 오늘은 잡은 수 말고, 누가 더 센지로!",
                half = "와, 제법인데? 방금 그거 어떻게 한 거야?",
                crisis = "잠깐잠깐, 아직 안 끝났어!",
                pressing = "봐, 물가는 내가 먼저 다 훑었다니까!",
                defeat = "으악, 졌다! …다음엔 안 봐준다?",
                victory = "이겼다! 할머니한테 자랑해야지!",
            },
            ["rival_swamp"] = new Lines
            {
                npcId = "catcher_rival", name = "라온", title = "초원의 라이벌",
                intro = "습지에서 한 판 더! 이번엔 나도 준비 좀 했거든.",
                half = "…여치가 겁먹었어. 너 요즘 더 세졌지?",
                crisis = "버텨! 조금만 더 버텨!",
                pressing = "어때, 이번엔 내 차례지!",
                defeat = "하… 또 졌네. 너한테 지는 건 이상하게 안 분해.",
                victory = "봤지? 나도 놀고만 있던 거 아니라고!",
            },
            ["rival_frost"] = new Lines
            {
                npcId = "catcher_rival", name = "라온", title = "서릿길의 라이벌",
                intro = "손이 곱아도 승부는 승부야.",
                half = "…너, 이제 진짜 멀리 왔구나.",
                crisis = "아직… 아직 한 수 남았어!",
                pressing = "뒤만 보는 줄 알았지? 앞도 볼 줄 안다고!",
                defeat = "졌다. …다음엔 네 옆에서 싸우는 쪽이 좋겠다.",
                victory = "헤헤, 오늘은 내가 이겼다. 세라한테는 비밀!",
            },
            ["rival_final"] = new Lines
            {
                npcId = "catcher_rival", name = "라온", title = "동네 최강자",
                intro = "팔도 다 나았어. 이번엔 진짜 겨루자!",
                half = "그래, 이거지! 이게 네 진짜 실력이지!",
                crisis = "하하… 역시 세다. 그래도 안 져!",
                pressing = "누워 있는 동안 머릿속으로 백 판은 했거든!",
                defeat = "완패다. 최강자 자리, 네 거야.",
                victory = "동네 최강자는 아직 나다! 언제든 다시 와!",
            },
        };
    }
}
