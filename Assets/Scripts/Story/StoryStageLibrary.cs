using InsectGame.NPC;
using UnityEngine;

namespace InsectGame.Story
{
    /// <summary>
    /// 연출 저작. <see cref="CutsceneLibrary"/>와 <b>의도적으로 같은 모양</b>이다 —
    /// 저쪽은 카메라를, 여기는 배우를 움직인다. 에셋은 쓰지 않는다(이 게임엔 애니메이션 클립이 없다).
    ///
    /// 좌표는 전부 <b>플레이어 기준 상대</b>다. z 양수가 플레이어 앞, x 양수가 오른쪽.
    /// 카메라가 고정 yaw라 월드축 오프셋이 곧 화면 방향이다(컷신 오프셋 규약과 같음).
    ///
    /// <b>상수 선언과 <c>case</c>를 반드시 함께 넣는다.</b> 하나만 있으면 런타임에 LogWarning만 찍고
    /// 연출이 조용히 안 나온다 — 화면상 티가 안 나서 배포까지 살아남는다.
    /// <c>story_lint.py</c>가 둘을 대조한다.
    /// </summary>
    public static class StoryStageLibrary
    {
        /// <summary>1막 개막 — 마을 어르신이 플레이어를 알아보고 부른다.</summary>
        public const string Ch1ElderGreet = "st_ch1_elder_greet";
        /// <summary>1막 — 라온이 뛰어 들어온다(대사 앞).</summary>
        public const string Ch1RivalEnter = "st_ch1_rival_enter";
        /// <summary>1막 — 라온이 손을 흔들고 달려 나간다(대사 뒤).</summary>
        public const string Ch1RivalExit = "st_ch1_rival_exit";

        // ── 챕터 대치 — 이미 앞에 서 있는 상대 (NpcTalk 비트) ──
        // 워프하지 않는다. 플레이어가 말을 건 상대라 이미 대화 거리다 — 여기서 또 걸어오게 하면
        // 방금 마주 선 것을 두 번 하는 꼴이 된다(Ch1ElderGreet와 같은 이유).

        /// <summary>2막 전 · 연못 — 검은 옷의 사내가 인기척에 돌아본다.</summary>
        public const string Ch2CordTurn = "st_ch2_cord_turn";
        /// <summary>3막 전 · 숲 — 검은 옷의 여자가 길을 막고 돌아가라 가리킨다.</summary>
        public const string Ch3RuleBlock = "st_ch3_rule_block";
        /// <summary>4막 전 · 습지 — 사내가 상자를 내밀다 들킨 얼굴이 된다.</summary>
        public const string Ch4CordHaul = "st_ch4_cord_haul";
        /// <summary>5막 전 · 산 — 여자가 한 걸음 앞으로 나와 앞을 막는다.</summary>
        public const string Ch5RuleBar = "st_ch5_rule_bar";
        /// <summary>6막 전 · 유적 — 사내가 끄덕이고 한 박자 물러선다.</summary>
        public const string Ch6CordYield = "st_ch6_cord_yield";

        // ── 챕터 대치 — 서브에리어 등장 (SubAreaEnter 비트) ──
        // 진입 순간 배우는 25m 밖(자기 앵커)에 있다. 무대 밖으로 워프한 뒤 걸어 들여야 한다.
        // 퇴장은 저작하지 않는다 — StoryStageDirector가 비트 완료 시 앵커로 되돌린다.
        // (ReturnToAnchor로 걸려 보내면 대사를 닫은 뒤 최악 8초 조작이 잠긴다.)

        /// <summary>7장 · 침묵의 자리 — 세라가 뒤따라 들어와 멈춰 선다.</summary>
        public const string Ch7ScholarFollow = "st_ch7_scholar_follow";
        /// <summary>8장 · 모래언덕 창고 — <b>집게</b> 첫 등장. 상자 사이에서 걸어 나온다.</summary>
        public const string Ch8GripEnter = "st_ch8_grip_enter";
        /// <summary>9장 · 얼음 서고 — <b>저울</b> 첫 등장. 옆에서 나타나 세라를 지목한다.</summary>
        public const string Ch9ScaleEnter = "st_ch9_scale_enter";
        /// <summary>10장 · 잿불 가마 — <b>먹</b>이 느리게 걸어와 말없이 끄덕인다.</summary>
        public const string Ch10InkEnter = "st_ch10_ink_enter";
        /// <summary>11장 · 우듬지 꼭대기 — 세라가 앞서 달려가 위를 가리킨다.</summary>
        public const string Ch11ScholarLead = "st_ch11_scholar_lead";
        /// <summary>12장 · 이름 없는 장부 — <b>관장 하월</b>이 뒤에서 천천히 다가온다.</summary>
        public const string Ch12ChiefEnter = "st_ch12_chief_enter";

        public static bool TryGet(string stageId, out StoryStageStep[] steps)
        {
            switch (stageId)
            {
                case Ch1ElderGreet: steps = BuildCh1ElderGreet(); return true;
                case Ch1RivalEnter: steps = BuildCh1RivalEnter(); return true;
                case Ch1RivalExit: steps = BuildCh1RivalExit(); return true;

                case Ch2CordTurn: steps = BuildCh2CordTurn(); return true;
                case Ch3RuleBlock: steps = BuildCh3RuleBlock(); return true;
                case Ch4CordHaul: steps = BuildCh4CordHaul(); return true;
                case Ch5RuleBar: steps = BuildCh5RuleBar(); return true;
                case Ch6CordYield: steps = BuildCh6CordYield(); return true;

                case Ch7ScholarFollow: steps = BuildCh7ScholarFollow(); return true;
                case Ch8GripEnter: steps = BuildCh8GripEnter(); return true;
                case Ch9ScaleEnter: steps = BuildCh9ScaleEnter(); return true;
                case Ch10InkEnter: steps = BuildCh10InkEnter(); return true;
                case Ch11ScholarLead: steps = BuildCh11ScholarLead(); return true;
                case Ch12ChiefEnter: steps = BuildCh12ChiefEnter(); return true;

                default: steps = null; return false;
            }
        }

        /// <summary>
        /// 어르신은 <b>이미 옆에 있다</b> — 플레이어가 찾아왔거나 조우 접근이 데려왔다.
        /// 그래서 걷게 하지 않는다. 알아보고, 부르고, 한 박자 쉰다. 그게 전부다.
        /// 여기서 어르신을 또 걸어오게 하면 방금 다가온 것을 두 번 하는 꼴이 된다.
        /// </summary>
        private static StoryStageStep[] BuildCh1ElderGreet()
        {
            return new[]
            {
                StoryStageStep.Face("village_elder", 0.3f),
                StoryStageStep.Play("village_elder", NpcGesture.Wave),
                StoryStageStep.Pause(0.35f),   // 손짓이 끝나고 말이 시작되기까지의 사이
            };
        }

        /// <summary>
        /// 라온의 등장. 이 비트의 트리거는 <c>CaptureInsect</c>라 <b>위치와 무관하게</b> 터진다 —
        /// 그냥 두면 라온이 지도 반대편에 있는 채로 대사만 뜬다. 그래서 먼저 무대 밖(플레이어
        /// 뒤쪽 9m)으로 옮겨 세우고, 거기서 달려 들어온다.
        ///
        /// 뒤에서 오는 이유는 "언제 왔지" 하는 인상을 주기 위해서다. 앞에서 오면 카메라에
        /// 처음부터 잡혀 등장이 아니라 이동이 된다.
        /// </summary>
        private static StoryStageStep[] BuildCh1RivalEnter()
        {
            return new[]
            {
                StoryStageStep.Warp("catcher_rival", new Vector3(-2.2f, 0f, -9f)),
                StoryStageStep.MoveTo("catcher_rival", new Vector3(-1.6f, 0f, 1.4f), 1.1f),
                StoryStageStep.Face("catcher_rival", 0.2f),
                StoryStageStep.Play("catcher_rival", NpcGesture.NetSwing),   // 뜰채를 한 번 휘두르며 인사
            };
        }

        /// <summary>라온의 퇴장 — 손을 흔들고 제 자리로 달려 돌아간다.</summary>
        private static StoryStageStep[] BuildCh1RivalExit()
        {
            return new[]
            {
                StoryStageStep.Play("catcher_rival", NpcGesture.Wave),
                StoryStageStep.GoHome("catcher_rival"),
            };
        }

        // ==================== 챕터 대치 — 이미 앞에 서 있는 상대 ====================
        //
        // 명부회 하급자 둘(`ledger_thug_cord` 검은 옷의 사내 / `ledger_thug_rule` 검은 옷의 여자)이
        // 연못·숲·습지·산·유적에 번갈아 서 있다. 대사만 뜨면 그냥 서 있는 사람과 구분이 안 되므로,
        // **몸짓 하나로 그 자리의 성격**을 먼저 알려준다 — 경계 / 통제 / 들킴 / 차단 / 체념.

        /// <summary>
        /// 연못. 첫 명부회 조우다 — 이쪽을 보고 있던 게 아니라 <b>일하다 들킨</b> 인상을 준다.
        /// 그래서 돌아보고(Face) 움찔(Recoil)한 뒤 한 박자 쉬고 말이 시작된다.
        /// </summary>
        private static StoryStageStep[] BuildCh2CordTurn()
        {
            return new[]
            {
                StoryStageStep.Face("ledger_thug_cord", 0.3f),
                StoryStageStep.Play("ledger_thug_cord", NpcGesture.Recoil),
                StoryStageStep.Pause(0.35f),
            };
        }

        /// <summary>숲. 이번엔 저쪽이 먼저 알고 있다 — 돌아가라고 가리킨다.</summary>
        private static StoryStageStep[] BuildCh3RuleBlock()
        {
            return new[]
            {
                StoryStageStep.Face("ledger_thug_rule", 0.25f),
                StoryStageStep.Play("ledger_thug_rule", NpcGesture.Point),
                StoryStageStep.Pause(0.35f),
            };
        }

        /// <summary>
        /// 습지. 남획 현장이다. <b>내미는 몸짓이 먼저</b> 나오고 그 다음에 이쪽을 본다 —
        /// 순서를 뒤집으면 "보여주려던 것"이 되어 들킨 장면이 안 된다.
        /// </summary>
        private static StoryStageStep[] BuildCh4CordHaul()
        {
            return new[]
            {
                StoryStageStep.Play("ledger_thug_cord", NpcGesture.Offer),
                StoryStageStep.Face("ledger_thug_cord", 0.2f),
                StoryStageStep.Play("ledger_thug_cord", NpcGesture.Recoil),
            };
        }

        /// <summary>
        /// 산. 말이 아니라 몸으로 막는다 — 유일하게 한 걸음 <b>다가오는</b> 대치다.
        /// 이동 스텝은 하나만 둔다(둘이면 최악 16초라 시퀀스 상한 15초에 잘린다).
        /// </summary>
        private static StoryStageStep[] BuildCh5RuleBar()
        {
            return new[]
            {
                StoryStageStep.MoveTo("ledger_thug_rule", new Vector3(0f, 0f, 1.6f), 0.9f),
                StoryStageStep.Face("ledger_thug_rule", 0.2f),
                StoryStageStep.Play("ledger_thug_rule", NpcGesture.Point),
            };
        }

        /// <summary>
        /// 유적. 다섯 번째 조우이고 여기서 처음으로 <b>물러선다</b> — 끄덕임 + 긴 사이.
        /// 2막에서 이들이 간부에게 자리를 넘긴다는 것을 몸짓으로 미리 알린다.
        /// </summary>
        private static StoryStageStep[] BuildCh6CordYield()
        {
            return new[]
            {
                StoryStageStep.Face("ledger_thug_cord", 0.25f),
                StoryStageStep.Play("ledger_thug_cord", NpcGesture.Nod),
                StoryStageStep.Pause(0.5f),
            };
        }

        // ==================== 챕터 대치 — 서브에리어 등장 ====================
        //
        // 트리거가 `SubAreaEnter`라 발화 시점에 배우는 자기 앵커(수십 m 밖)에 있다.
        // `Ch1RivalEnter`와 같은 형태로 무대 밖에 세운 뒤 걸어 들인다.
        //
        // **퇴장은 저작하지 않는다.** `StoryStageDirector`가 비트 완료 시 워프한 배우를 앵커로
        // 즉시 되돌린다 — `GoHome`으로 걸려 보내면 대사를 닫은 뒤 최악 8초 조작이 잠기는데
        // (AdvanceStep의 "알려진 UX 부채") 그 비용을 여섯 곳에 곱할 이유가 없다.
        //
        // 이동 스텝은 시퀀스당 **하나만** 둔다. 최악 8초라 둘이면 합이 상한(15초)에 닿아
        // 정상 재생이 하드 타임아웃에 잘린다.

        /// <summary>
        /// 침묵의 자리. 세라가 <b>뒤따라</b> 들어온다 — 앞서 들어가면 플레이어가 이끌린 게 되고,
        /// 이 장면은 플레이어가 먼저 보고 세라가 뒤늦게 확인하는 순서라야 한다.
        /// 도착해서 마주 보고 움찔한다(지워진 개체를 본 반응).
        /// </summary>
        private static StoryStageStep[] BuildCh7ScholarFollow()
        {
            return new[]
            {
                StoryStageStep.Warp("ruins_scholar", new Vector3(-1.8f, 0f, -7f)),
                StoryStageStep.MoveTo("ruins_scholar", new Vector3(-1.4f, 0f, 1.2f), 1.1f),
                StoryStageStep.Face("ruins_scholar", 0.2f),
                StoryStageStep.Play("ruins_scholar", NpcGesture.Recoil),
            };
        }

        /// <summary>
        /// 모래언덕 창고 — <b>집게</b>의 첫 등장이자 명부회 간부 첫 대면.
        /// 정면 9m에서 곧장 걸어 나온다(숨지 않는다 — 그들은 자기가 옳다고 믿는다).
        /// 인사 대신 뜰채를 한 번 휘두른다: 포획반장이라는 신분을 대사보다 먼저 알린다.
        /// </summary>
        private static StoryStageStep[] BuildCh8GripEnter()
        {
            return new[]
            {
                StoryStageStep.Warp("ledger_grip", new Vector3(0.4f, 0f, 9f)),
                StoryStageStep.MoveTo("ledger_grip", new Vector3(0.2f, 0f, 2.4f), 1.2f),
                StoryStageStep.Face("ledger_grip", 0.2f),
                StoryStageStep.Play("ledger_grip", NpcGesture.NetSwing),
            };
        }

        /// <summary>
        /// 얼음 서고 — <b>저울</b>의 첫 등장. 옆에서 나타나 플레이어가 아니라 <b>세라를</b> 가리킨다.
        /// 이 장면의 사건은 대치가 아니라 "옛 동문을 알아보는 것"이라, 시선이 플레이어를
        /// 지나쳐야 대사("너도 한때는 우리 방식이 옳다고 했잖아")가 제자리를 찾는다.
        /// </summary>
        private static StoryStageStep[] BuildCh9ScaleEnter()
        {
            return new[]
            {
                StoryStageStep.Warp("ledger_scale", new Vector3(7.5f, 0f, 2.5f)),
                StoryStageStep.MoveTo("ledger_scale", new Vector3(2.2f, 0f, 2f), 1.1f),
                StoryStageStep.Face("ledger_scale", 0.2f),
                StoryStageStep.Play("ledger_scale", NpcGesture.Point),
            };
        }

        /// <summary>
        /// 잿불 가마 — <b>먹</b>. 조직에서 유일하게 회의하는 사람이라 등장이 느리고 조용하다.
        /// 몸짓은 끄덕임 하나뿐이고, 그 뒤 가장 긴 사이가 온다 — 장부를 덮는 박자다.
        /// </summary>
        private static StoryStageStep[] BuildCh10InkEnter()
        {
            return new[]
            {
                StoryStageStep.Warp("ledger_ink", new Vector3(-1.2f, 0f, 8f)),
                StoryStageStep.MoveTo("ledger_ink", new Vector3(-0.8f, 0f, 2.6f), 1.2f),
                StoryStageStep.Play("ledger_ink", NpcGesture.Nod),
                StoryStageStep.Pause(0.6f),
            };
        }

        /// <summary>
        /// 우듬지 꼭대기 — 여기만 <b>동행자가 앞서</b> 간다. 대치가 아니라 발견의 장면이고,
        /// 세라가 먼저 올라가 위를 가리키는 것이 곧 떡밥 회수의 신호다(예비 울타리).
        /// </summary>
        private static StoryStageStep[] BuildCh11ScholarLead()
        {
            return new[]
            {
                StoryStageStep.Warp("ruins_scholar", new Vector3(0.6f, 0f, -2.2f)),
                StoryStageStep.MoveTo("ruins_scholar", new Vector3(0.8f, 0f, 3.4f), 1f),
                StoryStageStep.Face("ruins_scholar", 0.25f),
                StoryStageStep.Play("ruins_scholar", NpcGesture.Point),
            };
        }

        /// <summary>
        /// 이름 없는 장부 — <b>관장 하월</b>. 유일하게 뒤에서 다가온다: 이미 여기 있었고
        /// 플레이어가 들어오는 것을 보고 있었다는 뜻이다(라온의 등장과 같은 경로, 반대의 감정).
        /// 몸짓이 없다 — 마주 서서 한 박자 두는 것으로 끝낸다. 이 사람만 진명으로 불린다.
        /// </summary>
        private static StoryStageStep[] BuildCh12ChiefEnter()
        {
            return new[]
            {
                StoryStageStep.Warp("ledger_chief", new Vector3(1.6f, 0f, -11f)),
                StoryStageStep.MoveTo("ledger_chief", new Vector3(1.2f, 0f, 2.6f), 1.3f),
                StoryStageStep.Face("ledger_chief", 0.4f),
                StoryStageStep.Pause(0.7f),
            };
        }
    }
}
