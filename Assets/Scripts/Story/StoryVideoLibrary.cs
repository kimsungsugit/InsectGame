namespace InsectGame.Story
{
    /// <summary>
    /// 스토리 영상 저작 — <b>실제 영상 파일(mp4)</b>의 파일명·길이·자막 큐. 프로시저럴 컷신(<see cref="CutsceneLibrary"/>)과
    /// 의도적으로 같은 모양이다 — 저쪽은 카메라 워크를, 여기는 파일명과 자막 큐를 정의한다.
    ///
    /// 영상은 그림책 화풍 렌더러 <c>Tools/Video/storybook/render.py</c>가 그린다(편마다 <c>v_&lt;파일명&gt;.py</c>, 그림은 <c>kit.py</c>,
    /// 소리는 <c>sound.py</c>). <b>소리는 mp4 안(AAC)에 들어 있다</b> — <see cref="StoryVideoDirector"/>가 <c>AudioSource</c>로 보내고
    /// 재생 중 월드 BGM을 낮춘다. 파일은 <c>Assets/StreamingAssets/Video/</c>에 둔다. 자막은 영상에 굽지 않고 여기서 얹는다.
    ///
    /// <b>렌더러가 이 파일을 정규식으로 읽는다</b> — 미리보기에 자막을 얹고, 샷 길이 합(<c>DURS</c>)이 여기 길이와 같은지 본다
    /// (story_lint 검사 25도 같은 모양을 읽는다). 그래서 정의는 아래 빌더들과 <b>똑같은 한 문장 모양</b>으로 적는다 —
    /// 생성자에 상수·<c>"파일.mp4"</c>·<c>길이f</c>·큐 배열(<c>StoryVideoCue(시각f, 길이f, "자막")</c>을 new로 나열)을 차례로 넘기고
    /// 세미콜론으로 끝낸다. 길이·시각은 <c>f</c> 접미 숫자 리터럴만 쓴다(상수·계산식은 정규식이 못 읽는다).
    /// (이 주석에 그 문장을 그대로 적지 않는 것도 같은 이유다 — 추출기가 주석을 정의로 센다.)
    /// 샷 리스트·자막 원문의 단일 출처는 <c>Docs/StoryVideos.md</c>다 — 여기 자막을 고치면 그쪽도 함께 고친다.
    ///
    /// 두 자리에 붙는다:
    /// <list type="bullet">
    /// <item><b>대사 앞</b>(<c>StoryBeat.introVideoId</c>) — <b>설명은 영상이 맡는다.</b> 대상이 초등 고학년이라 "영상이 설명하고, 대사는
    ///   짧게"다. 자막은 그 장면의 사실을 한 줄씩 말하고, 뒤에 오는 대사(6줄 이하 — story_lint 검사 36)는 인물의 반응만 맡는다.
    ///   이름 벽(ch6)·울타리(ch7)·그림자(종장)·되찾은 이름(결말) 네 편이다.</item>
    /// <item><b>대사 뒤</b>(<c>StoryBeat.videoId</c>) — 마무리. 자막은 "그 대사가 끝난 다음 화면에 무엇이 남아야 하는가"로 쓰고
    ///   방금 들은 대사를 되풀이하지 않는다(StoryBible 7-1의 프롤로그 컷신 사고 참조).</item>
    /// </list>
    ///
    /// 지킬 것:
    /// - <b>총 길이는 15초 이하</b>. <c>PlayerMovement.AutoUnfreezeTime</c>(20s)보다 4초 이상 짧아야
    ///   재생 중 조작이 살아나지 않는다(<c>StoryVideoLibraryTests</c>가 강제).
    /// - <b>자막 한 줄은 24자 이하</b>(공백·문장부호 포함) — 아이가 2~3초 안에 한 번에 읽는 길이다(같은 테스트).
    /// - <b>자막에 「무명」을 쓰지 않는다</b> — 이름을 부르지 않는 것이 이 이야기의 규칙이다(StoryBible 2장).
    ///   대상은 「그림자」라 부른다.
    /// - <b>상수 선언과 <c>TryGet</c>의 case를 반드시 함께 넣는다.</b> 하나만 있으면 런타임에
    ///   LogWarning만 찍고 조용히 안 나온다. <c>story_lint</c> 검사 25가 본다.
    /// - 파일명은 ID에서 <c>vid_</c>를 뺀 것이다(<c>vid_ch6_wall</c> → <c>ch6_wall.mp4</c>).
    /// </summary>
    public static class StoryVideoLibrary
    {
        /// <summary>1막 개막 — 어르신에게 첫 파트너를 받은 직후(대사 뒤). <c>cs_story_prologue</c>를 대체한다.</summary>
        public const string Ch1Prologue = "vid_ch1_prologue";
        /// <summary>ch2 — 연못에서 수첩에 숫자만 적는 사내가 처음 나타난다(대사 뒤).</summary>
        public const string Ch2Watchers = "vid_ch2_watchers";
        /// <summary>ch3 — 세라 합류. 숲 바위의 이름 무늬와 「도감에 적으면 이름이 빛난다」(대사 뒤).</summary>
        public const string Ch3Scholar = "vid_ch3_scholar";
        /// <summary>ch4 — 습지의 상자들. 상자 속 곤충을 처음 본다(대사 뒤).</summary>
        public const string Ch4Crates = "vid_ch4_crates";
        /// <summary>ch5 — 산 정상에서 처음으로 유적이 보인다(대사 뒤).</summary>
        public const string Ch5Summit = "vid_ch5_summit";
        /// <summary>
        /// ch6 — 「이름 벽」. <b>대사 앞</b>(<c>ch6_secret</c>의 <c>introVideoId</c>): 옛사람이 이름을 새겨 곤충을 지켰고, 이름이 바래면
        /// 곤충이 사라지고, 도감에 적으면 다시 빛난다 — 1막의 답을 영상이 설명하고 대사는 반응만 한다. 고대의 잠자리가 벽에서 돌아온다.
        /// </summary>
        public const string Ch6Wall = "vid_ch6_wall";
        /// <summary>
        /// ch7 — 「이름 벽은 울타리였다」. <b>대사 앞</b>(<c>ch7_opening</c> — 7장을 여는 비트): 영상 → 「지난 이야기」 카드 → 대사.
        /// 바래서 난 구멍으로 그림자가 숨어들었고, 벽이 다 차자 숨은 한 칸이 드러난다.
        /// </summary>
        public const string Ch7Fence = "vid_ch7_fence";
        /// <summary>ch8 — 모래언덕 창고. 명부회 첫 간부 대면 뒤 창고의 규모(대사 뒤).</summary>
        public const string Ch8Vault = "vid_ch8_vault";
        /// <summary>ch9 — 얼음 서고. 세라의 과거(목록을 적던 손)(대사 뒤).</summary>
        public const string Ch9Archive = "vid_ch9_archive";
        /// <summary>ch10 — 잿불 갱도 붕괴. 먹이 라온을 끌어내고 장부를 덮는다(대사 뒤).</summary>
        public const string Ch10Kiln = "vid_ch10_kiln";
        /// <summary>ch11 — 우듬지의 무늬와 꽃밭 온실. 비상 울타리 둘(대사 뒤).</summary>
        public const string Ch11Crown = "vid_ch11_crown";
        /// <summary>
        /// ch12 — 장부의 방. 하월이 길을 비킨다. <b>하월을 이긴 뒤</b>(<c>duel_chief_win</c>)에 튼다 — 대치(<c>ch12_confront</c>)는
        /// "이 아이를 넘어 보게"로 끝나고 곧바로 대결이 열리므로, 비켜서는 그림이 대결 앞에 오면 이야기가 거꾸로 간다(2026-10-04).
        /// </summary>
        public const string Ch12Ledger = "vid_ch12_ledger";
        /// <summary>
        /// 종장 — 그림자. <b>대사 앞</b>(<c>fin_unnamed</c> — 종장을 여는 비트, 선택지가 뒤에 온다): 빼앗은 모습으로 바뀌며
        /// "이름을 줘"라고 묻는다. 이름을 주면 무슨 일이 생기는지("만약" 장면)를 보여 주어 선택의 무게를 영상이 설명한다.
        /// </summary>
        public const string FinShadow = "vid_fin_shadow";
        /// <summary>
        /// 결말 — 이름이 돌아간다. <b>대사 앞</b>(<c>fin_seal</c> — 그림자를 이긴 <c>BattleWin</c>): 전투 결과 화면이 닫힌 뒤에
        /// 뜬다(<c>StoryDirector</c>가 BattleWin을 결과 화면 뒤로 미룬다). 마지막 샷(사마귀·해돋이)에는 자막을 두지 않는다.
        /// </summary>
        public const string FinReturn = "vid_fin_return";
        /// <summary>에필로그 — 후일담 몽타주. 1편과 수미상관(대사 뒤).</summary>
        public const string FinEpilogue = "vid_fin_epilogue";

        public static bool TryGet(string videoId, out StoryVideoDefinition def)
        {
            switch (videoId)
            {
                case Ch1Prologue: def = BuildCh1Prologue(); return true;
                case Ch2Watchers: def = BuildCh2Watchers(); return true;
                case Ch3Scholar: def = BuildCh3Scholar(); return true;
                case Ch4Crates: def = BuildCh4Crates(); return true;
                case Ch5Summit: def = BuildCh5Summit(); return true;
                case Ch6Wall: def = BuildCh6Wall(); return true;
                case Ch7Fence: def = BuildCh7Fence(); return true;
                case Ch8Vault: def = BuildCh8Vault(); return true;
                case Ch9Archive: def = BuildCh9Archive(); return true;
                case Ch10Kiln: def = BuildCh10Kiln(); return true;
                case Ch11Crown: def = BuildCh11Crown(); return true;
                case Ch12Ledger: def = BuildCh12Ledger(); return true;
                case FinShadow: def = BuildFinShadow(); return true;
                case FinReturn: def = BuildFinReturn(); return true;
                case FinEpilogue: def = BuildFinEpilogue(); return true;
                default: def = null; return false;
            }
        }

        /// <summary>
        /// 새벽 초원(곤충이 하나도 없다) → 풀잎 위 곤충이 하나씩 흐려져 사라짐 → 은빛 채집망을 쥔 손 → 손등에 내려앉는 작은 곤충.
        /// 어르신 대사("곤충들이 하나둘 사라지고 있어")를 받아 <b>그다음</b>을 보여준다 — 그래서 그물을 받았고, 이 아이와 찾으러 간다.
        /// </summary>
        private static StoryVideoDefinition BuildCh1Prologue()
        {
            return new StoryVideoDefinition(Ch1Prologue, "ch1_prologue.mp4", 12f, new[]
            {
                new StoryVideoCue(0.8f, 2.6f, "어제 있던 곤충이 오늘은 안 보인다."),
                new StoryVideoCue(3.8f, 2.6f, "하나둘, 흐려지듯 사라지고 있다."),
                new StoryVideoCue(6.4f, 2.2f, "그래서 이 그물을 받았다."),
                new StoryVideoCue(9.0f, 2.6f, "이 아이와 함께 찾으러 가자."),
            });
        }

        /// <summary>연못 수면 → 갈대 너머 명부회 사내가 수첩에 눈금을 그어 셈 → 돌아서 안개 속으로 사라짐.</summary>
        private static StoryVideoDefinition BuildCh2Watchers()
        {
            return new StoryVideoDefinition(Ch2Watchers, "ch2_watchers.mp4", 10f, new[]
            {
                new StoryVideoCue(3.0f, 3.0f, "수첩에는 곤충 숫자만 가득했다."),
                new StoryVideoCue(6.8f, 2.6f, "그는 안개 속으로 사라졌다."),
            });
        }

        /// <summary>숲 바위의 이름 무늬 → 바랜 칸을 세라의 손끝이 훑음 → 닿은 칸이 금빛으로 반짝임 → 숲 너머 유적.</summary>
        private static StoryVideoDefinition BuildCh3Scholar()
        {
            return new StoryVideoDefinition(Ch3Scholar, "ch3_scholar.mp4", 12f, new[]
            {
                new StoryVideoCue(0.8f, 2.2f, "바위에 곤충 이름이 새겨져 있다."),
                new StoryVideoCue(3.2f, 2.2f, "바랜 이름도 군데군데 보였다."),
                new StoryVideoCue(5.8f, 2.6f, "손끝이 닿자, 이름이 반짝였다."),
                new StoryVideoCue(8.9f, 2.6f, "답은 저 너머 유적에 있다."),
            });
        }

        /// <summary>안개 습지 → 반쯤 잠긴 나무 상자 줄 → 상자 틈의 큰 눈과 더듬이 → 뚜껑의 이름표와 물방울.</summary>
        private static StoryVideoDefinition BuildCh4Crates()
        {
            return new StoryVideoDefinition(Ch4Crates, "ch4_crates.mp4", 12f, new[]
            {
                new StoryVideoCue(1.0f, 2.6f, "습지 곳곳에 상자가 숨겨져 있었다."),
                new StoryVideoCue(6.2f, 2.6f, "틈 사이로 더듬이가 움직였다."),
                new StoryVideoCue(9.2f, 2.4f, "모두 살아 있는 곤충이었다."),
            });
        }

        /// <summary>능선 위 세 사람의 뒷모습 → 발아래 구름이 갈라짐 → 안개 아래 금빛 유적 → 유적으로 느린 줌.</summary>
        private static StoryVideoDefinition BuildCh5Summit()
        {
            return new StoryVideoDefinition(Ch5Summit, "ch5_summit.mp4", 12f, new[]
            {
                new StoryVideoCue(1.0f, 2.6f, "지나온 길이 모두 내려다보였다."),
                new StoryVideoCue(5.6f, 2.8f, "안개 너머로 고대 유적이 빛난다."),
                new StoryVideoCue(9.0f, 2.6f, "곤충 이름의 비밀이 저기 있다."),
            });
        }

        /// <summary>
        /// <b>대사 앞.</b> 신전의 거대한 이름 벽(아래에 세 사람이 작게) → 회상: 옛사람이 칸에 이름을 새기면 나비가 날아오름 →
        /// 한 칸이 바래고 풀잎 위 딱정벌레가 사라짐 → 도감의 펜에서 금빛 실이 날아가 빈칸이 다시 켜짐 → 고대의 잠자리가 벽에서 돌아옴.
        /// 뒤 대사는 "보세요! 사라졌던 고대의 잠자리예요!"로 이 마지막 샷을 받는다.
        /// </summary>
        private static StoryVideoDefinition BuildCh6Wall()
        {
            return new StoryVideoDefinition(Ch6Wall, "ch6_wall.mp4", 15f, new[]
            {
                new StoryVideoCue(0.6f, 2.2f, "벽 가득, 곤충 이름이 새겨져 있다."),
                new StoryVideoCue(3.0f, 2.8f, "옛사람들은 이름을 새겨 곤충을 지켰다."),
                new StoryVideoCue(6.1f, 2.6f, "이름이 바래면, 그 곤충이 사라졌다."),
                new StoryVideoCue(9.1f, 2.6f, "도감에 적으면, 이름이 다시 빛난다."),
                new StoryVideoCue(12.1f, 2.6f, "그리고 사라졌던 곤충이 돌아온다!"),
            });
        }

        /// <summary>
        /// <b>대사 앞</b>(7장을 여는 비트 — 영상 → 「지난 이야기」 카드 → 대사). 이름 벽이 울타리로 보이고, 바깥의 그림자를 막는다 →
        /// 이름이 바래면 말뚝 자리에 구멍 → 그림자가 그 구멍으로 숨어듦 → 벽이 다 차자 숨은 한 칸이 드러남.
        /// 뒤 대사는 "방금 봤어? 빈칸이 움직였어!"로 받는다.
        /// </summary>
        private static StoryVideoDefinition BuildCh7Fence()
        {
            return new StoryVideoDefinition(Ch7Fence, "ch7_fence.mp4", 14f, new[]
            {
                new StoryVideoCue(0.3f, 2.3f, "이름 벽은 사실 울타리였다."),
                new StoryVideoCue(2.8f, 2.5f, "바깥의 그림자를 막는 울타리."),
                new StoryVideoCue(5.4f, 2.5f, "이름이 바래면, 구멍이 난다."),
                new StoryVideoCue(8.2f, 2.6f, "그림자는 그 구멍으로 숨어들었다."),
                new StoryVideoCue(11.2f, 2.6f, "벽이 다 차자, 숨은 한 칸이 드러났다."),
            });
        }

        /// <summary>
        /// 모래에 반쯤 묻힌 창고 입구 → 천장까지 쌓인 상자 → 뚜껑이 열린 빈 상자들 → 상자 하나가 조용히 놓여 있음.
        /// 둘째 큐는 사양(2.6초)보다 0.2초 짧다 — 사양대로면 셋째 큐(6.6초)와 겹쳐 앞 자막이 뒤 자막을 가린다(겹침은 테스트가 금지).
        /// </summary>
        private static StoryVideoDefinition BuildCh8Vault()
        {
            return new StoryVideoDefinition(Ch8Vault, "ch8_vault.mp4", 12f, new[]
            {
                new StoryVideoCue(1.0f, 2.6f, "창고 안은 끝이 보이지 않았다."),
                new StoryVideoCue(4.2f, 2.4f, "천장까지 상자가 쌓여 있었다."),
                new StoryVideoCue(6.6f, 2.4f, "어떤 상자는 텅 비어 있었다."),
                new StoryVideoCue(9.4f, 2.2f, "이 상자는 너무 조용했다."),
            });
        }

        /// <summary>
        /// 얼음 속에 멈춘 곤충들 → 세라와 저울이 마주 섬 → 얼음에 비친 회상: 어린 세라의 손이 목록을 적음 →
        /// 지금의 손이 그 반영에 닿다가 멈춤. 세라의 대답은 아직 없다(대결 뒤로 남긴다).
        /// </summary>
        private static StoryVideoDefinition BuildCh9Archive()
        {
            return new StoryVideoDefinition(Ch9Archive, "ch9_archive.mp4", 14f, new[]
            {
                new StoryVideoCue(1.0f, 2.6f, "얼음 속에 곤충들이 멈춰 있다."),
                new StoryVideoCue(3.6f, 2.2f, "세라와 저울이 마주 섰다."),
                new StoryVideoCue(6.0f, 3.0f, "예전에 이 목록을 적은 건 세라였다."),
                new StoryVideoCue(10.2f, 3.0f, "세라는 아직 대답하지 못했다."),
            });
        }

        /// <summary>
        /// 잿불 갱도 → 들보가 무너짐 → 먹의 검은 소매가 돌무더기 속 라온의 팔을 끌어냄 → 재 위의 상자 하나 → 먹이 장부를 덮음.
        /// </summary>
        private static StoryVideoDefinition BuildCh10Kiln()
        {
            return new StoryVideoDefinition(Ch10Kiln, "ch10_kiln.mp4", 15f, new[]
            {
                new StoryVideoCue(2.8f, 2.6f, "천장이 무너져 내렸다."),
                new StoryVideoCue(6.2f, 3.0f, "먹이 라온을 끌어냈다."),
                new StoryVideoCue(9.8f, 2.4f, "꺼낸 상자는 하나뿐이었다."),
                new StoryVideoCue(12.2f, 2.4f, "먹은 장부를 덮었다."),
            });
        }

        /// <summary>
        /// 거대한 나무 꼭대기 → 나무껍질의 이름 무늬(ch3 바위와 같은 무늬) → 멀리 꽃밭 온실 → 둘이 한 화면에서 금빛으로.
        /// 둘째 큐는 사양(2.4초)보다 0.2초 짧다 — 사양대로면 셋째 큐(6.0초)와 겹친다. 줄인 끝(6.0초)이 둘째 샷의 끝과 같다.
        /// </summary>
        private static StoryVideoDefinition BuildCh11Crown()
        {
            return new StoryVideoDefinition(Ch11Crown, "ch11_crown.mp4", 12f, new[]
            {
                new StoryVideoCue(1.0f, 2.4f, "나무 꼭대기에도 이름이 있었다."),
                new StoryVideoCue(3.8f, 2.2f, "숲 바위와 같은 무늬였다."),
                new StoryVideoCue(6.0f, 2.6f, "저 멀리 꽃밭 온실에도."),
                new StoryVideoCue(9.0f, 2.6f, "둘 다 비상 울타리였다."),
            });
        }

        /// <summary>빈 석판이 둘러선 방 → 벽면 가득한 장부 선반 → 절반이 먼지에 바램 → 하월이 비켜서고 안쪽 어둠에 노란 눈 둘.</summary>
        private static StoryVideoDefinition BuildCh12Ledger()
        {
            return new StoryVideoDefinition(Ch12Ledger, "ch12_ledger.mp4", 14f, new[]
            {
                new StoryVideoCue(1.0f, 2.6f, "삼십 년 동안 쓴 장부들."),
                new StoryVideoCue(4.2f, 2.6f, "그 속 곤충의 절반이 죽었다."),
                new StoryVideoCue(7.4f, 2.6f, "지키려다, 오히려 잃었다."),
                // 직전 대사가 "길을 비켜 주마. 안쪽에서 그림자가 기다린다."라 같은 말을 되풀이하지 않는다.
                new StoryVideoCue(10.6f, 2.6f, "이제 안쪽으로 가는 길이 열렸다."),
            });
        }

        /// <summary>
        /// <b>대사 앞</b>(종장을 여는 비트 — 영상 → 「지난 이야기」 카드 → 대사 → 선택지). 울타리 맨 끝 빈칸의 그림자 →
        /// 사마귀·나비·반딧불이로 모습을 바꿈 → 다가와 빈 이름표를 내밂 → "만약" 장면: 이름을 받은 그림자가 울타리 안을 덮음 →
        /// 다시 지금: 도감의 곤충들이 줄지어 빛나고 마지막 한 칸만 비어 있다.
        /// </summary>
        private static StoryVideoDefinition BuildFinShadow()
        {
            return new StoryVideoDefinition(FinShadow, "fin_shadow.mp4", 14.4f, new[]
            {
                new StoryVideoCue(0.5f, 2.2f, "울타리의 맨 끝, 마지막 빈칸."),
                new StoryVideoCue(2.8f, 2.6f, "그림자는 빼앗은 모습으로 바뀐다."),
                new StoryVideoCue(5.6f, 2.4f, "그리고 묻는다. '이름을 줘.'"),
                new StoryVideoCue(8.4f, 2.8f, "이름을 받으면, 울타리 안에 자리를 얻는다."),
                new StoryVideoCue(11.4f, 2.6f, "그러니 절대, 이름을 주지 않는다."),
            });
        }

        /// <summary>
        /// <b>대사 앞</b>(그림자를 이긴 뒤 — 전투 결과 화면이 닫힌 다음). 빌린 모습들이 빛으로 빠져나옴 → 빛이 들판으로 흩어지고
        /// 말뚝 이름표가 하나씩 켜짐 → 작아진 그림자가 울타리 밖 안개로 밀려남 → 마지막 구멍에 새 말뚝, 들판에 색이 돌아옴 →
        /// 풀잎 위 초록 사마귀와 해돋이(자막 없음). 뒤 대사는 "마지막 칸이 메워졌어요!"로 받는다.
        /// </summary>
        private static StoryVideoDefinition BuildFinReturn()
        {
            return new StoryVideoDefinition(FinReturn, "fin_return.mp4", 14.6f, new[]
            {
                new StoryVideoCue(0.5f, 2.2f, "빼앗긴 이름들이 빠져나온다."),
                new StoryVideoCue(2.9f, 2.6f, "이름은 하나씩 주인에게 돌아갔다."),
                new StoryVideoCue(5.9f, 2.6f, "그림자는 울타리 밖으로 밀려났다."),
                new StoryVideoCue(8.9f, 2.6f, "마지막 구멍까지, 이제 다 메워졌다."),
            });
        }

        /// <summary>
        /// 후일담 몽타주 — 하월이 들판에서 딱정벌레를 놓아줌 / 라온과 어르신이 초원을 걸음 / 숫자 칸 없는 새 장부에 펜 /
        /// 꽃밭 속 빈 석판 하나 / 새벽 초원, 그물 없이 뻗은 손에 작은 곤충. 마지막 컷에는 자막을 두지 않는다.
        /// </summary>
        private static StoryVideoDefinition BuildFinEpilogue()
        {
            return new StoryVideoDefinition(FinEpilogue, "fin_epilogue.mp4", 15f, new[]
            {
                new StoryVideoCue(1.0f, 2.6f, "하월은 곤충을 하나씩 놓아주었다."),
                new StoryVideoCue(4.0f, 2.6f, "라온은 어르신 곁으로 돌아갔다."),
                new StoryVideoCue(7.0f, 2.6f, "새 장부에는 숫자 칸이 없다."),
                new StoryVideoCue(10.0f, 2.6f, "빈칸은 또 생길지도 모른다."),
            });
        }
    }
}
