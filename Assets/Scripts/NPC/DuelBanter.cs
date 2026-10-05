using System.Collections.Generic;

namespace InsectGame.NPC
{
    /// <summary>
    /// 대결 상대가 전투 중에 하는 말 — 컷인(칭호·도발), 전투 중 한마디, 결과 한마디.
    ///
    /// 예전엔 간부전도 야생전과 똑같이 "○○와 마주쳤다" 한 줄로 시작해서, 관장과의 마지막 대결이
    /// 필드의 인분무와 구별되지 않았다. 대사는 스토리 비트(대결 전 대치 <c>chN_confront</c> — 그 마지막 줄의 도전 뒤
    /// 곧바로 대결이 열린다(<c>StoryBeat.duelAfter</c>) — ·대결 뒤 <c>duel_*_win</c>)가 이미 들고 있으니 여기엔
    /// <b>전투 한가운데에서만 할 수 있는 말</b>만 둔다 — 겹치면 같은 말을 두 번 듣는다.
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
            /// <summary>
            /// <b>팀 대결의 교체 한마디</b> — 상대가 다음 곤충을 내보낼 때. [0]이 두 번째 곤충, [1]이 세 번째 곤충…
            /// 명부회 간부는 여럿을 데리고 싸운다(집게·저울 셋, 하월 다섯 — <c>NpcBossDuels</c>). 한 마리만 내는 상대(하수·라온)는
            /// 비워 둔다. 줄이 모자라면 마지막 줄을 되풀이한다 — <see cref="SendOutLine"/>이 고른다.
            /// 그리기는 ui-dev(<c>BattleScreenUI.Duel</c>)가 교체 순간에 말풍선으로 띄운다.
            /// </summary>
            public string[] sendOut;
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

        /// <summary>
        /// 상대 HP로 정하는 순간(흔들림·위기)을 <b>지금 나와 있는 곤충</b>으로 말해도 되는가 — 팀 대결이면 <b>에이스(마지막 곤충)</b>에서만.
        ///
        /// 간부는 여럿을 데리고 싸운다(집게·저울 셋, 하월 다섯 — <c>NpcBossDuels.teamInsectIds</c>). 대사는 그 사람의 마지막 패를 두고 쓴
        /// 말이라("이 손이… 밀린다고?", "삼십 년 전에도 이 아이는 이렇게 날았지"), 첫 곤충이 절반 아래로 떨어진 순간에 나오면 아직 셋이 남았는데
        /// 무너지는 소리를 한다. 앞 곤충들 사이는 교체 한마디(<see cref="Lines.sendOut"/>)가 맡는다.
        /// 내 곤충이 몰리는 순간(<see cref="Moment.Pressing"/>)은 상대 팀과 무관하다 — 막지 않는다.
        /// </summary>
        /// <param name="enemyTeamSize">상대 팀 크기(<c>InsectBattleController.EnemyTeamSize</c>). 1 이하면 한 마리 대결.</param>
        /// <param name="enemyTeamIndex">지금 나와 있는 상대의 순번(0부터, <c>EnemyTeamIndex</c>).</param>
        public static bool EnemyMomentsAllowed(int enemyTeamSize, int enemyTeamIndex)
        {
            if (enemyTeamSize <= 1) return true;
            return enemyTeamIndex >= enemyTeamSize - 1;
        }

        /// <summary>
        /// 팀 대결판 <see cref="Next(ref Tracker, float, float)"/> — 에이스가 나오기 전엔 상대 HP 순간을 건너뛴다(트래커도 건드리지 않아
        /// 에이스가 나온 뒤 처음부터 센다). 그리기(<c>BattleScreenUI.Duel</c>)가 팀 크기·순번을 넘겨 부른다.
        /// </summary>
        public static Moment Next(ref Tracker tracker, float enemyRatio, float playerRatio, int enemyTeamSize, int enemyTeamIndex)
        {
            float enemy = EnemyMomentsAllowed(enemyTeamSize, enemyTeamIndex) ? enemyRatio : 1f;
            return Next(ref tracker, enemy, playerRatio);
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

        /// <summary>
        /// 상대가 <paramref name="incomingIndex"/>번째 곤충(0부터 — 첫 곤충은 0, 처음 교체해 나오는 곤충이 1)을 내보낼 때의 한마디.
        /// 첫 곤충(0)·줄이 없는 상대는 null이다(첫 곤충의 말은 컷인 도발 <see cref="Lines.intro"/>가 맡는다).
        /// 준비한 줄보다 팀이 길면 마지막 줄을 쓴다 — 팀 크기가 바뀌어도 말이 끊기지 않게.
        /// </summary>
        public static string SendOutLine(Lines lines, int incomingIndex)
        {
            if (incomingIndex <= 0 || lines.sendOut == null || lines.sendOut.Length == 0) return null;
            int i = incomingIndex - 1;
            if (i >= lines.sendOut.Length) i = lines.sendOut.Length - 1;
            return lines.sendOut[i];
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
                sendOut = new[]
                {
                    "다음이다. 물러서지 마라.",
                    // 셋째가 에이스 지네(centipede_sand)다 — 패배 한마디("지네가 먼저 물러서다니")와 이어진다.
                    "마지막이다. 지네야, 끝까지 물고 늘어져라!",
                },
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
                sendOut = new[]
                {
                    "다음 표본. 수치는 거짓말을 안 한다.",
                    "마지막 표본이다. 이번엔 틀림없이 잰다.",
                },
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
                sendOut = new[]
                {
                    "다음 장을 넘기지.",
                    "이 아이도 내 장부에 적힌 이름이다.",
                    "삼십 년 치 장부다. 아직 남았네.",
                    // 다섯째가 이름 잃은 나방(moth_effaced) — 대치의 "이 아이를 넘어 보게"가 가리킨 그 아이다.
                    "마지막 장이다. 이름 잃은 아이야, 나가거라.",
                },
            },
            // ── 라온 라이벌(NpcRivalDuels) ── 한 인물이 단계마다 싸우므로 키가 단계 ID다.
            // 이기고 지는 말이 적대가 아니라 장난스럽다 — 라온은 끝까지 "누가 더 구하나"의 경쟁자다.
            // 초원은 **첫 전투**일 수 있다(첫 만남 대사 직후 곧바로 붙는다) — 말을 쉽고 짧게 둔다.
            ["rival_meadow"] = new Lines
            {
                npcId = "catcher_rival", name = "라온", title = "초원의 라이벌",
                intro = "첫 판이다! 내 여치, 얕보면 큰코다쳐!",
                half = "어? 생각보다 세잖아!",
                crisis = "잠깐, 잠깐! 아직이야!",
                pressing = "봤지? 사흘 걸려 잡은 보람이 있다니까!",
                defeat = "졌다! …그래도 재밌었어. 또 하자!",
                victory = "이겼다! 다음엔 너도 더 세져서 와!",
            },
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
            ["rival_hollow"] = new Lines
            {
                npcId = "catcher_rival", name = "라온", title = "텅 빈 들의 라이벌",
                intro = "여기 너무 조용해. 우리가 시끄럽게 해 주자!",
                half = "좋아, 이 소리! 들판이 깨어나는 것 같아.",
                crisis = "버텨, 여치야! 여기서 지면 창피해!",
                pressing = "조용한 데서는 내 여치가 더 잘 들어!",
                defeat = "졌다. 그래도 들판에 소리가 났잖아.",
                victory = "내가 이겼다! 들판아, 들었지?",
            },
            ["rival_dunes"] = new Lines
            {
                npcId = "catcher_rival", name = "라온", title = "모래언덕의 라이벌",
                intro = "덥다! 빨리 끝내고 상자 열러 가자!",
                half = "모래바람 속에서도 잘 버티네!",
                crisis = "모래가 눈에… 아니, 핑계 아니야!",
                pressing = "상자 생각하니까 힘이 나거든!",
                defeat = "졌어. 이 힘은 상자 여는 데 쓸게.",
                victory = "이겼다! 이 기세로 창고까지 가자!",
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
            ["rival_ember"] = new Lines
            {
                npcId = "catcher_rival", name = "라온", title = "잿불의 라이벌",
                intro = "땅이 뜨거워. 금방 끝내고 애들 구하러 가자!",
                half = "역시 너야. 등 맡기기 딱 좋겠어.",
                crisis = "아직 아니야! 갱도 가기 전에 한 방!",
                pressing = "불 속에서도 내 여치는 안 물러서!",
                defeat = "졌다. 됐어, 이제 진짜 구하러 가자.",
                victory = "이겼다! 이 기분으로 갱도까지 간다!",
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
