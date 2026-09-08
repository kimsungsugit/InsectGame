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
        /// <summary>
        /// 꽃밭 온실 — 세라가 앞서 들어가 <b>바닥</b>을 가리킨다. 11장 우듬지와 **짝을 이루는 연출**이다:
        /// 예비 울타리 두 곳(꽃밭·우듬지)에서만 동행자가 앞서 가고, 대치가 아니라 발견이 된다.
        /// 다른 점은 사이(間)뿐 — 여기서는 아직 답을 모르므로 가리키기 전에 한 박자 멈춘다.
        /// </summary>
        public const string GardenScholarGlass = "st_garden_scholar_glass";

        // ── 오염 거점 대치 (NpcTalk 비트) ──
        // 위 1막 대치들과 같은 자리·같은 배우인데 **몸짓이 반대로 간다.** 1막에서는 막고
        // 가리키고 물러섰다면, 여기서는 손에서 도구를 내려놓는다 — 현장을 인정하는 장면이다.
        // 워프하지 않는 이유는 위와 같다(말을 건 상대라 이미 대화 거리).

        /// <summary>숲 그물터 — 청년이 그물을 걷다 멈추고 움찔한다. 셋 중 가장 먼저 흔들린다.</summary>
        public const string BlightForestConfront = "st_bl_forest_confront";
        /// <summary>산 채집장 — 여자가 뜰채를 내려놓고 능선을 가리킨다. 자기 현장을 시인한다.</summary>
        public const string BlightMountainConfront = "st_bl_mountain_confront";
        /// <summary>유적 창고 — 사내가 상자를 내밀다 멈추고 고개를 떨군다. 처음으로 대답이 없다.</summary>
        public const string BlightRuinsConfront = "st_bl_ruins_confront";

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
                case GardenScholarGlass: steps = BuildGardenScholarGlass(); return true;

                case BlightForestConfront: steps = BuildBlightForestConfront(); return true;
                case BlightMountainConfront: steps = BuildBlightMountainConfront(); return true;
                case BlightRuinsConfront: steps = BuildBlightRuinsConfront(); return true;

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
        ///
        /// <b><see cref="StoryStageStep.MoveTo"/>가 아니라 <see cref="StoryStageStep.Approach"/>인
        /// 이유</b>: 이 배우는 <b>플레이어가 말을 건 상대</b>라 어느 방향에 서 있을지 모른다.
        /// 예전엔 목적지가 `플레이어 + (0,0,1.6)`, 즉 <i>월드축 북쪽</i> 1.6m였다 — 플레이어가
        /// 북쪽에서 다가와 말을 걸었다면 그 점이 <b>플레이어 너머</b>라, 여자가 플레이어를 향해
        /// 곧장 걷다 콜라이더에 막혀 <b>8초를 제자리걸음</b>한 뒤에야 대사가 떴다(그동안 조작 잠김).
        /// `Approach`는 목적지를 배우–플레이어 직선 위에서 잡으므로 어느 방향에서 걸든 성립한다.
        /// </summary>
        private static StoryStageStep[] BuildCh5RuleBar()
        {
            return new[]
            {
                StoryStageStep.Approach("ledger_thug_rule", 1.8f),
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
        //
        // ── **워프 지점은 반드시 방 안이어야 한다** ──────────────────────────────
        //
        // 서브에리어는 벽으로 봉해진 정사각 방이고(`SubAreaWorldBuilder.CreateBoundaryWalls`),
        // 진입하면 플레이어가 **방 남쪽 입구에 고정 배치**된다(`FindSafeSpawnPosition`의
        // `origin + (0, 0.5, -8)`). 오프셋은 월드축이고 방도 축정렬이라, 워프 지점의 방 로컬
        // 좌표는 그냥 **(offset.x, offset.z - 8)**이다 — 저작 시점에 계산할 수 있다.
        //
        // 그 점이 벽 밖이면 배우가 벽 뒤에 떨어지고, 걸어 들어오려다 `IsBlockedAhead`에 막힌다.
        // Scripted 이동은 막혀도 포기하지 않으므로 **8초를 밀다가 타임아웃으로 "완료" 처리**되고,
        // `SnapToFinalPose`는 이미 지나간 이동 스텝을 소급하지 않는다(`ReturnToAnchor`만 한다).
        // 결과는 **대사만 뜨고 화자는 화면에 없는** 장면이다 — 예외도 경고도 없다.
        //
        // 1막의 `Ch1RivalEnter`가 쓰는 "뒤쪽 9m" 관용구를 여기 그대로 옮기면 이 함정을 밟는다.
        // 벽 없는 메인 필드에서 온 값이라, 플레이어가 남쪽 입구에 서 있는 방에서는 남벽 바깥이다
        // (실제로 ch7·ch12가 그랬고 ch11은 벽에 낀 채로 나왔다). **방에서는 옆에서 들인다** —
        // 가로로 멀리 두면 카메라 밖이면서 벽 안이다(`Ch9ScaleEnter`가 원래 그 형태였다).
        // `story_lint` 검사 21이 방별 벽 크기를 읽어 이 조건을 강제한다.

        /// <summary>
        /// 침묵의 자리. 세라가 <b>뒤따라</b> 들어온다 — 앞서 들어가면 플레이어가 이끌린 게 되고,
        /// 이 장면은 플레이어가 먼저 보고 세라가 뒤늦게 확인하는 순서라야 한다.
        /// 도착해서 마주 보고 움찔한다(지워진 개체를 본 반응).
        ///
        /// 등장은 <b>왼쪽 뒤 모서리</b>에서다. 곧장 뒤(옛 `-7`)는 방 남벽 <b>바깥</b>이라
        /// 세라가 벽에 막혀 못 들어왔다 — 위 블록 주석의 방 기하 규칙 참조.
        /// </summary>
        private static StoryStageStep[] BuildCh7ScholarFollow()
        {
            return new[]
            {
                StoryStageStep.Warp("ruins_scholar", new Vector3(-11.5f, 0f, -3.5f)),
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
        /// 얼음 서고 — <b>저울</b>의 첫 등장. 옆에서 걸어 나와 가리킨다.
        /// 이 장면의 사건은 대치가 아니라 "옛 동문을 알아보는 것"이다.
        ///
        /// <b>세라를 지목하지는 못한다.</b> 이 비트의 대사에는 세라가 나오지만 무대에 올리는
        /// 배우는 저울 하나뿐이고(그녀는 리전 앵커에 서 있다), 배우끼리 서로를 향하게 하는
        /// 액션도 없다 — 지금 있는 것은 플레이어를 보는 <c>FacePlayer</c>뿐이다.
        /// 방 안에 동행자를 함께 세우는 것은 6개 대치 전부에 걸린 별건이다(감사 P2로 남겼다).
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
        ///
        /// 왼쪽에서 들여 플레이어를 앞질러 간다. 옛 값(`-2.2`)은 플레이어 바로 뒤라
        /// <b>카메라와 플레이어 사이</b>에서 세라가 불쑥 생겨났고, 이 방은 가장 좁아서
        /// 그 자리가 남벽 안쪽 면에 거의 닿아 있었다(캡슐이 벽에 낀다).
        /// </summary>
        private static StoryStageStep[] BuildCh11ScholarLead()
        {
            return new[]
            {
                StoryStageStep.Warp("ruins_scholar", new Vector3(-8.5f, 0f, -1f)),
                StoryStageStep.MoveTo("ruins_scholar", new Vector3(0.8f, 0f, 3.4f), 1f),
                StoryStageStep.Face("ruins_scholar", 0.25f),
                StoryStageStep.Play("ruins_scholar", NpcGesture.Point),
            };
        }

        /// <summary>
        /// 온실은 이 게임에서 **가장 좁은 대치 무대**다 — 방 반쪽이 10m뿐이라(다른 대치는 11~14m)
        /// 워프·목적지를 안쪽으로 당겨 잡았다. 입구가 z=-8이므로 방 로컬 좌표는
        /// 워프 (-6.5, -7.5) · 도착 (0.7, -5.2)이고, 벽(±10)에서 각각 2.5m·4.8m 남는다.
        ///
        /// 이 방이 <c>CreateBoundaryWalls</c>가 아니라 <c>CreateGlassWall</c>로 봉해져 있어
        /// **story_lint 검사 21이 오랫동안 이 방을 아예 안 봤다**(<c>half is None</c>이면 건너뛴다).
        /// 유리에도 collider가 남아 있어 벽 밖 워프면 똑같이 배우가 막히는데도 그랬다.
        /// 이 연출을 붙이면서 <c>game_facts._boundary_half_size</c>가 두 호출을 다 읽게 고쳤다.
        /// </summary>
        private static StoryStageStep[] BuildGardenScholarGlass()
        {
            return new[]
            {
                StoryStageStep.Warp("ruins_scholar", new Vector3(-6.5f, 0f, 0.5f)),
                StoryStageStep.MoveTo("ruins_scholar", new Vector3(0.7f, 0f, 2.8f), 1f),
                StoryStageStep.Face("ruins_scholar", 0.3f),
                // 한 박자 — 11장 세라는 알아보고 가리키지만, 여기서는 아직 모른 채 들여다본다.
                StoryStageStep.Pause(0.35f),
                StoryStageStep.Play("ruins_scholar", NpcGesture.Point),
            };
        }

        /// <summary>
        /// 이름 없는 장부 — <b>관장 하월</b>. 유일하게 <b>뒤쪽 모서리</b>에서 다가온다:
        /// 이미 여기 있었고 플레이어가 들어오는 것을 보고 있었다는 뜻이다.
        /// 몸짓이 없다 — 마주 서서 한 박자 두는 것으로 끝낸다. 이 사람만 진명으로 불린다.
        ///
        /// 옛 값(`-11`)은 방 남벽에서 **6m 바깥**, 바닥 평면 밖이었다. 최종장 첫 대면인데
        /// 관장이 벽 뒤에서 8초를 밀다가 그대로 대사만 뜨고 있었다.
        /// </summary>
        private static StoryStageStep[] BuildCh12ChiefEnter()
        {
            return new[]
            {
                StoryStageStep.Warp("ledger_chief", new Vector3(10.5f, 0f, -3.5f)),
                StoryStageStep.MoveTo("ledger_chief", new Vector3(1.2f, 0f, 2.6f), 1.3f),
                StoryStageStep.Face("ledger_chief", 0.4f),
                StoryStageStep.Pause(0.7f),
            };
        }

        // ==================== 오염 거점 대치 ====================
        //
        // 1막 대치가 "여긴 우리 구역이다"였다면 여기는 "이게 우리가 한 일이다"다.
        // 그래서 <b>도구를 쓰는 몸짓(NetSwing/Offer)이 앞에 오고 시선이 뒤에 온다</b> —
        // 손이 먼저 멈추고 그 다음에 이쪽을 보는 순서가 "들켜서"가 아니라 "그만두려고"로 읽힌다.
        //
        // 이동 스텝은 없다. 시퀀스 상한 15초 안에 넉넉히 들어간다.

        /// <summary>
        /// 산. 여자는 1막에서 유일하게 한 걸음 다가왔던 인물이다(<c>Ch5RuleBar</c>).
        /// 여기서는 그 반대로 — 뜰채를 한 번 휘두르고(내려놓는 동작을 대신한다) 능선을 가리킨 뒤
        /// 이쪽을 본다. 가리키는 곳에 자기가 걷어 낸 자리가 있다.
        /// </summary>
        /// <summary>
        /// 숲. 여자·사내와 달리 이 청년은 <b>변명부터 한다.</b> 그물을 걷던 손(NetSwing)이
        /// 멈추고 움찔한 뒤(Recoil) 마주 본다 — 순서가 반대면 "들켰다"가 아니라 "맞선다"가 된다.
        /// 사이는 셋 중 가장 짧게 둔다. 무게를 잡을 위치의 인물이 아니다.
        /// </summary>
        private static StoryStageStep[] BuildBlightForestConfront()
        {
            return new[]
            {
                StoryStageStep.Play("ledger_thug_pin", NpcGesture.NetSwing),
                StoryStageStep.Play("ledger_thug_pin", NpcGesture.Recoil),
                StoryStageStep.Face("ledger_thug_pin", 0.25f),
                StoryStageStep.Pause(0.35f),
            };
        }

        private static StoryStageStep[] BuildBlightMountainConfront()
        {
            return new[]
            {
                StoryStageStep.Play("ledger_thug_rule", NpcGesture.NetSwing),
                StoryStageStep.Play("ledger_thug_rule", NpcGesture.Point),
                StoryStageStep.Face("ledger_thug_rule", 0.3f),
                StoryStageStep.Pause(0.5f),
            };
        }

        /// <summary>
        /// 유적. 사내는 1막 마지막에 처음으로 물러섰던 인물이다(<c>Ch6CordYield</c>).
        /// 여기서는 상자를 내밀다(Offer) 멈추고 고개를 떨군다(Nod). 가장 긴 사이를 뒤에 둔다 —
        /// 대사 첫 줄이 "…나도 안다"라서, 그 앞의 침묵이 길수록 그 말이 무겁다.
        /// </summary>
        private static StoryStageStep[] BuildBlightRuinsConfront()
        {
            return new[]
            {
                StoryStageStep.Play("ledger_thug_cord", NpcGesture.Offer),
                StoryStageStep.Face("ledger_thug_cord", 0.25f),
                StoryStageStep.Play("ledger_thug_cord", NpcGesture.Nod),
                StoryStageStep.Pause(0.7f),
            };
        }
    }
}
