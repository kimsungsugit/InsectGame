namespace InsectGame.Story
{
    /// <summary>
    /// 스토리 영상 저작 — <b>실제 영상 파일(mp4)</b>을 스토리 비트 완료 뒤에 재생한다.
    /// 프로시저럴 컷신(<see cref="CutsceneLibrary"/>)과 의도적으로 같은 모양이다 — 저쪽은
    /// 카메라 워크를, 여기는 파일명과 자막 큐를 정의한다.
    ///
    /// 영상은 <c>Tools/Video/story_silhouettes.py</c>가 그린 실루엣 삽화이고 <c>Assets/StreamingAssets/Video/</c>에
    /// 둔다. 나중에 AI 영상으로 바꾸면 같은 파일명으로 덮어쓴다 — 여기는 안 바뀐다.
    /// 샷 리스트·프롬프트·자막 원문의 단일 출처는 <c>Docs/StoryVideos.md</c>다 — 여기 자막을
    /// 고치면 그쪽도 함께 고친다.
    ///
    /// 지킬 것:
    /// - <b>총 길이는 15초 이하</b>. <c>PlayerMovement.AutoUnfreezeTime</c>(20s)보다 4초 이상 짧아야
    ///   재생 중 조작이 살아나지 않는다(<c>StoryVideoLibraryTests</c>가 강제).
    /// - <b>자막에 「무명」을 쓰지 않는다</b> — 이름을 부르지 않는 것이 이 이야기의 규칙이다(StoryBible 2장).
    /// - <b>상수 선언과 <c>TryGet</c>의 case를 반드시 함께 넣는다.</b> 하나만 있으면 런타임에
    ///   LogWarning만 찍고 조용히 안 나온다. <c>story_lint</c> 검사 25가 본다.
    /// - 영상은 대사가 <b>끝난 뒤</b> 재생된다 — 도입이 아니라 마무리다. 자막은 "그 대사가 끝난
    ///   다음 화면에 무엇이 남아야 하는가"로 쓴다(StoryBible 7-1의 프롤로그 컷신 사고 참조).
    /// </summary>
    public static class StoryVideoLibrary
    {
        /// <summary>1막 개막 — 어르신에게 첫 파트너를 받은 직후. <c>cs_story_prologue</c>를 대체한다.</summary>
        public const string Ch1Prologue = "vid_ch1_prologue";
        /// <summary>ch2 — 연못에서 정체불명의 감시자가 처음 나타난다.</summary>
        public const string Ch2Watchers = "vid_ch2_watchers";
        /// <summary>ch3 — 세라 합류. 숲 동굴의 각인과 「봉인=기록」 가설.</summary>
        public const string Ch3Scholar = "vid_ch3_scholar";
        /// <summary>ch4 — 습지의 상자들. 남획 현장 최초 목격.</summary>
        public const string Ch4Crates = "vid_ch4_crates";
        /// <summary>ch5 — 산 정상에서 처음으로 유적이 보인다.</summary>
        public const string Ch5Summit = "vid_ch5_summit";
        /// <summary>ch8 — 모래언덕 창고. 명부회 첫 간부 대면 뒤 창고의 규모.</summary>
        public const string Ch8Vault = "vid_ch8_vault";
        /// <summary>ch9 — 얼음 서고. 세라의 과거(목록을 적던 손).</summary>
        public const string Ch9Archive = "vid_ch9_archive";
        /// <summary>ch10 — 잿불 갱도 붕괴. 먹이 라온을 끌어내고 장부를 덮는다.</summary>
        public const string Ch10Kiln = "vid_ch10_kiln";
        /// <summary>ch11 — 우듬지의 문양과 꽃밭. 예비 울타리 둘.</summary>
        public const string Ch11Crown = "vid_ch11_crown";
        /// <summary>ch12 — 삼천 칸의 장부방. 관장이 길을 비킨다.</summary>
        public const string Ch12Ledger = "vid_ch12_ledger";
        /// <summary>에필로그 — 후일담 몽타주. 3막의 문.</summary>
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
                case Ch8Vault: def = BuildCh8Vault(); return true;
                case Ch9Archive: def = BuildCh9Archive(); return true;
                case Ch10Kiln: def = BuildCh10Kiln(); return true;
                case Ch11Crown: def = BuildCh11Crown(); return true;
                case Ch12Ledger: def = BuildCh12Ledger(); return true;
                case FinEpilogue: def = BuildFinEpilogue(); return true;
                default: def = null; return false;
            }
        }

        /// <summary>
        /// 새벽 초원 → 풀잎 사이 곤충이 하나씩 흐려져 사라짐 → 손에 든 은빛 그물 → 손등에 앉는 작은 곤충.
        /// 어르신 대사("어제 있던 아이가 오늘은 보이질 않는단다")를 <b>되풀이하지 않고</b> 그 다음을 보여준다.
        /// </summary>
        private static StoryVideoDefinition BuildCh1Prologue()
        {
            return new StoryVideoDefinition(Ch1Prologue, "ch1_prologue.mp4", 12f, new[]
            {
                new StoryVideoCue(0.8f, 3.2f, "풀밭은 이렇게 넓은데, 움직이는 것이 눈에 잘 띄지 않는다."),
                new StoryVideoCue(5.2f, 2.8f, "잡는 법은 몸이 먼저 익혔다. 그리고 이제 혼자가 아니다."),
                new StoryVideoCue(8.6f, 3.0f, "이 아이와 함께라면, 그 이유를 찾을 수 있을지도 모른다."),
            });
        }

        /// <summary>연못 수면 → 갈대 너머 검은 옷 실루엣이 수첩에 적음 → 돌아서 안개로 사라짐.</summary>
        private static StoryVideoDefinition BuildCh2Watchers()
        {
            return new StoryVideoDefinition(Ch2Watchers, "ch2_watchers.mp4", 10f, new[]
            {
                new StoryVideoCue(1.0f, 3.0f, "이름은 묻지 않았다. 종만 물었다."),
                new StoryVideoCue(5.6f, 3.4f, "…곧 전부 장부에 오를 테니."),
            });
        }

        /// <summary>숲 동굴 벽의 각인 → 손끝이 문양을 훑음 → 문양이 희미하게 빛남 → 숲 너머 유적 방향.</summary>
        private static StoryVideoDefinition BuildCh3Scholar()
        {
            return new StoryVideoDefinition(Ch3Scholar, "ch3_scholar.mp4", 12f, new[]
            {
                new StoryVideoCue(1.0f, 3.0f, "유적 밖에서 나온 첫 각인이었다."),
                new StoryVideoCue(5.0f, 3.0f, "봉인에 금이 갔다. 사라지는 것들은 그 틈으로 빠져나가고 있다."),
                new StoryVideoCue(8.6f, 3.0f, "기록 하나하나가, 그 균열을 메우는 열쇠일지도 모른다."),
            });
        }

        /// <summary>안개 습지 → 반쯤 잠긴 나무 상자 열 지어 → 상자 틈으로 더듬이·날개 → 뚜껑의 판독 불가 표찰.</summary>
        private static StoryVideoDefinition BuildCh4Crates()
        {
            return new StoryVideoDefinition(Ch4Crates, "ch4_crates.mp4", 12f, new[]
            {
                new StoryVideoCue(1.2f, 3.0f, "많은 건 세어도 티가 안 난다 — 그래서 여기부터라고 했다."),
                new StoryVideoCue(5.4f, 2.6f, "상자는 열 개가 넘었다."),
                new StoryVideoCue(8.6f, 3.0f, "저게 다 살아 있는 것이라면."),
            });
        }

        /// <summary>능선의 뒷모습 실루엣 → 구름이 갈라지며 → 안개 아래 유적이 희미하게 빛남 → 유적으로 천천히 줌.</summary>
        private static StoryVideoDefinition BuildCh5Summit()
        {
            return new StoryVideoDefinition(Ch5Summit, "ch5_summit.mp4", 12f, new[]
            {
                new StoryVideoCue(1.4f, 3.0f, "여기까지 온 채집가는 처음이라고 했다."),
                new StoryVideoCue(5.6f, 3.0f, "안개 너머로 빛나는 것 — 모든 사라짐의 근원."),
                new StoryVideoCue(9.0f, 2.6f, "이제 마지막 장만 남았다."),
            });
        }

        /// <summary>모래에 반쯤 묻힌 창고 내부 → 천장까지 쌓인 상자 → 먼지 속 광선 → 상자 하나가 조용히 멈춰 있음.</summary>
        private static StoryVideoDefinition BuildCh8Vault()
        {
            return new StoryVideoDefinition(Ch8Vault, "ch8_vault.mp4", 12f, new[]
            {
                new StoryVideoCue(1.0f, 3.2f, "빈칸을 메우기 위해 — 전부, 지금 당장."),
                new StoryVideoCue(5.4f, 2.8f, "그 안의 아이들은 지금 몇이나 살아 있을까."),
                new StoryVideoCue(8.8f, 2.8f, "…장부에는 올라가 있다."),
            });
        }

        /// <summary>
        /// 얼음 속에 봉인된 곤충들 → 두 실루엣이 마주 섬 → 얼음 벽에 비친 젊은 손이 목록을 적는 회상 →
        /// 현재의 손이 그 위를 덮는다. 무대 연출은 배우가 하나뿐이라 세라를 세우지 못했다 — 영상이 그 자리를 맡는다.
        /// </summary>
        private static StoryVideoDefinition BuildCh9Archive()
        {
            return new StoryVideoDefinition(Ch9Archive, "ch9_archive.mp4", 14f, new[]
            {
                new StoryVideoCue(1.0f, 3.0f, "이 서고의 목록을 만든 것은 누구였나."),
                new StoryVideoCue(5.0f, 2.6f, "…나였다. 부정하지 않는다."),
                new StoryVideoCue(8.4f, 2.4f, "얼음은 멈춰 세울 뿐이다. 자라지도, 죽지도 못하게."),
                new StoryVideoCue(11.2f, 2.5f, "달라진 것은 속도가 아니라 방향이다."),
            });
        }

        /// <summary>
        /// 잿불 갱도 → 불똥과 함께 들보가 무너짐 → 검은 소매의 손이 재 속에서 다른 팔을 끌어냄 →
        /// 재 위에 놓인 상자 하나 → 들려 있던 장부가 덮인다. 비트에는 지문 한 줄뿐인 액션이다.
        /// </summary>
        private static StoryVideoDefinition BuildCh10Kiln()
        {
            return new StoryVideoDefinition(Ch10Kiln, "ch10_kiln.mp4", 15f, new[]
            {
                new StoryVideoCue(3.6f, 2.6f, "상자는 다 못 꺼냈다. 이 하나뿐이다."),
                new StoryVideoCue(7.4f, 3.4f, "나는 이름을 적는 사람이지, 가두는 사람이 아니었다."),
                new StoryVideoCue(11.6f, 2.8f, "여기서부턴, 둘이다."),
            });
        }

        /// <summary>거대수 꼭대기 신록 → 나무껍질 문양 클로즈업 → 시선이 내려가 멀리 유리온실 → 두 곳이 한 화면에.</summary>
        private static StoryVideoDefinition BuildCh11Crown()
        {
            return new StoryVideoDefinition(Ch11Crown, "ch11_crown.mp4", 12f, new[]
            {
                new StoryVideoCue(1.0f, 3.0f, "울타리는 하나가 아니었다."),
                new StoryVideoCue(5.0f, 3.2f, "실패할 것을 알면서 울타리를 치고, 실패한 뒤를 위해 두 칸을 남겼다."),
                new StoryVideoCue(9.0f, 2.6f, "우리에게."),
            });
        }

        /// <summary>빈 석판들이 둘러선 방 → 벽면 가득한 장부 선반 → 절반이 먼지에 바램 → 노인의 뒷모습이 길을 비켜 안쪽 어둠으로.</summary>
        private static StoryVideoDefinition BuildCh12Ledger()
        {
            return new StoryVideoDefinition(Ch12Ledger, "ch12_ledger.mp4", 14f, new[]
            {
                new StoryVideoCue(1.0f, 2.8f, "삼천 종. 삼십 년."),
                new StoryVideoCue(4.8f, 2.8f, "그중 절반이 창고에서 죽었다."),
                new StoryVideoCue(8.4f, 3.0f, "빈칸을 메우겠다고, 빈칸을 만들었다."),
                new StoryVideoCue(11.6f, 2.0f, "길이 열렸다. 안쪽에서 그것이 기다린다."),
            });
        }

        /// <summary>
        /// 후일담 몽타주 — 노인이 들판에서 곤충 하나를 놓아줌 / 초원의 두 실루엣 / 수량 칸 없는 새 장부에 펜 /
        /// 빈 석판 하나가 여전히 비어 있음 / 새벽 초원, 그물 없이 뻗는 손. 마지막 컷에는 자막을 두지 않는다.
        /// </summary>
        private static StoryVideoDefinition BuildFinEpilogue()
        {
            return new StoryVideoDefinition(FinEpilogue, "fin_epilogue.mp4", 15f, new[]
            {
                new StoryVideoCue(1.0f, 3.0f, "장부는 전부 넘겨졌다. 이름들을 한 종씩 찾아다니는 데 남은 평생이 걸릴 것이다."),
                new StoryVideoCue(4.8f, 2.6f, "새 장부에는 수량 칸이 없다."),
                new StoryVideoCue(8.0f, 2.8f, "빈칸은 사라지지 않았다. 종이 사라지면 자리는 또 생긴다."),
                new StoryVideoCue(11.2f, 2.6f, "그러니까 계속 만나요. 한 마리씩, 계속."),
            });
        }
    }
}
