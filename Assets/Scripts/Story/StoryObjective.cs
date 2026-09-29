using System.Collections.Generic;

namespace InsectGame.Story
{
    /// <summary>목표가 가리키는 대상의 종류. 자동 주행이 가능한지가 여기서 갈린다.</summary>
    public enum StoryObjectiveKind
    {
        /// <summary>스토리 NPC에게 말 걸기 — 월드 좌표가 있고 자동 주행 가능.</summary>
        TalkToNpc,
        /// <summary>리전 진입 — 리전 중심으로. 다른 리전이면 텔레포트가 먼저다.</summary>
        EnterRegion,
        /// <summary>서브에리어 진입 — 서브에리어 중심으로.</summary>
        EnterSubArea,
        /// <summary>수문장 격파 — 해당 리전으로.</summary>
        DefeatGuardian,
        /// <summary>
        /// <b>그 리전에서 무언가 하기</b> — 무param <c>CaptureInsect</c>/<c>BattleWin</c>에
        /// <c>requiredRegionId</c>가 붙은 경우다. 트리거 자체는 위치를 안 싣지만 리전 게이트가
        /// 사실상 위치다: 그 리전 밖에서는 아무리 잡고 이겨도 발화하지 않는다.
        ///
        /// 저작된 82비트 중 28건이 여기 해당한다(포획 16 + 전투 12, 전부 리전이 채워져 있다).
        /// 예전엔 전부 <see cref="Freeform"/>으로 떨어져 HUD가 "모험을 이어가세요"만 띄웠고,
        /// 하필 1막 전체(오프닝 직후 5단계 연속)가 그 구간이라 초보자에게 안내가 0이었다.
        /// </summary>
        ActInRegion,
        /// <summary>
        /// 고정 위치가 <b>정말로</b> 없는 목표(레벨·도감·퀘스트 완료). 문구만 띄우고 자동 주행은
        /// 막는다 — "어디로"가 없는 목표에 화살표를 띄우면 없는 곳을 가리키게 된다.
        /// 대신 임계값과 현재값을 함께 보여준다("도감 60종 기록 · 현재 42종").
        /// </summary>
        Freeform
    }

    /// <summary>
    /// "지금 무엇을 하면 이야기가 이어지는가" 한 건. <see cref="StoryDirector"/>가 도출하고
    /// HUD(목표 행·미니맵 쐐기)와 자동 주행이 소비한다.
    /// </summary>
    public readonly struct StoryObjective
    {
        public readonly string BeatId;
        public readonly StoryObjectiveKind Kind;
        /// <summary>대상 식별자 — npcId / regionId / subAreaId / 포획 지정 곤충 ID. 없으면 빈 문자열.</summary>
        public readonly string TargetId;
        /// <summary>비트의 requiredRegionId(비어 있을 수 있음). ActInRegion 목표의 위치가 이것이다.</summary>
        public readonly string RequiredRegionId;
        /// <summary>
        /// 원본 <c>trigger.type</c>. 라벨을 만들 때 Kind만으로는 부족해서 들고 간다 —
        /// Freeform 하나에 레벨·도감·퀘스트 세 가지가 겹쳐 있고 문구가 서로 다르다.
        /// </summary>
        public readonly string TriggerType;
        /// <summary>
        /// <c>LevelReach</c>/<c>DexProgress</c>의 임계값. 그 외엔 <c>-1</c>.
        /// <b>현재값은 여기 담지 않는다</b> — 이 구조체는 <c>StoryDirector</c>가 캐시하고
        /// 진행이 바뀔 때만 무효화하므로, 매 프레임 변하는 수치를 넣으면 화면에 낡은 값이 굳는다.
        /// 현재값은 <c>StoryObjectiveTracker</c>가 Refresh마다 직접 읽는다.
        /// </summary>
        public readonly int Threshold;

        public StoryObjective(string beatId, StoryObjectiveKind kind, string targetId,
            string requiredRegionId, string triggerType = null, int threshold = -1)
        {
            BeatId = beatId;
            Kind = kind;
            TargetId = targetId ?? string.Empty;
            RequiredRegionId = requiredRegionId ?? string.Empty;
            TriggerType = triggerType ?? string.Empty;
            Threshold = threshold;
        }

        /// <summary>
        /// 종류상 갈 곳이 있는가. <b>실제로 화살표를 띄울지는 트래커가 정한다</b> —
        /// ActInRegion은 이미 그 리전 안에 있으면 갈 곳이 없다.
        /// </summary>
        public bool HasWorldTarget => Kind != StoryObjectiveKind.Freeform;

        public bool IsValid => !string.IsNullOrEmpty(BeatId);
    }

    /// <summary>
    /// 목표 도출의 <b>순수</b> 부분. MonoBehaviour와 씬에서 떼어 놓아 PlayMode 테스트로 고정한다
    /// (<see cref="StoryDirector"/>는 진행 상태만 얹는다).
    /// </summary>
    public static class StoryObjectiveResolver
    {
        /// <summary>
        /// 다른 비트가 <c>prerequisiteBeatId</c>로 지목하는 비트 = <b>스파인</b>.
        /// 지목받지 않는 비트는 놓쳐도 체인이 안 끊기는 leaf(플레이버)라 목표로 우선하지 않는다.
        /// </summary>
        public static HashSet<string> CollectSpineBeatIds(IEnumerable<StoryBeat> beats)
        {
            var spine = new HashSet<string>();
            if (beats == null) return spine;
            foreach (StoryBeat beat in beats)
            {
                if (beat != null && !string.IsNullOrEmpty(beat.prerequisiteBeatId))
                    spine.Add(beat.prerequisiteBeatId);
            }
            return spine;
        }

        /// <summary>
        /// 선택지 결과 집합(어떤 비트의 <c>choices[].nextBeatId</c>로 지목된 비트).
        ///
        /// 이 비트들은 <b>플레이어가 고른 순간에만</b> 발화한다 — 목표로 안내하지도, 시작 시
        /// <c>Immediate</c> 일괄 평가에 걸리지도 않는다. 안 고른 쪽은 영영 미열람으로 남는데,
        /// 그걸 목표 후보에 두면 그 챕터가 끝난 뒤에도 HUD가 "모험을 이어가세요"에 굳는다.
        /// </summary>
        public static HashSet<string> CollectChoiceTargetIds(IEnumerable<StoryBeat> beats)
        {
            var targets = new HashSet<string>();
            if (beats == null) return targets;
            foreach (StoryBeat beat in beats)
            {
                if (beat == null || beat.choices == null) continue;
                foreach (StoryChoice choice in beat.choices)
                {
                    if (choice != null && !string.IsNullOrEmpty(choice.nextBeatId))
                        targets.Add(choice.nextBeatId);
                }
            }
            return targets;
        }

        /// <summary>
        /// 챕터 진행 순위. <c>chapterId</c>는 "ch1".."ch12" / "fin" / "side" / "npc" 규약이고,
        /// <b>문자열 정렬로는 ch10이 ch2보다 앞에 온다</b> — 숫자를 뽑아 비교한다.
        /// 본편이 아닌 챕터(fin/side/npc)는 뒤로 민다.
        ///
        /// (표시용 챕터 순서·라벨은 <c>StoryJournalUI</c>가 따로 갖는다. 저쪽은 탭을 어떤 순서로
        /// 그릴지, 여기는 어느 쪽이 이야기상 먼저인지 — 관심사가 다르므로 사본이 아니다.)
        /// </summary>
        public static int ChapterRank(string chapterId)
        {
            if (string.IsNullOrEmpty(chapterId)) return int.MaxValue;
            if (chapterId.StartsWith("ch") && int.TryParse(chapterId.Substring(2), out int n))
                return n;
            if (chapterId == FinaleChapterId) return 1000;   // 최종장 — 본편 뒤
            return 2000;                            // side / npc 등 곁이야기
        }

        /// <summary>
        /// 같은 자격을 가진 두 비트 중 어느 쪽이 먼저인가(음수면 <paramref name="a"/>가 앞).
        /// 순위는 <b>(스파인 우선, 챕터, order, beatId)</b>다.
        ///
        /// <b>이 함수가 순서의 단일 출처다.</b> 목표 도출(<see cref="SelectObjectiveBeat"/>)과
        /// 실제 발화(<c>StoryDirector.EvaluateTriggers</c>)가 <b>같은 답</b>을 내야 한다(목표 쪽은 종장
        /// 한 가지만 덧붙인다 — <see cref="CompareObjectivePriority"/>) —
        /// 예전엔 발화 쪽이 <c>AllBeats()</c>(Dictionary.Values, 순서 비결정)를 훑어 <b>첫 일치</b>를
        /// 집었다. 그래서 동시에 자격을 갖는 비트가 있으면 HUD가 가리키는 것과 실제로 뜨는 것이
        /// 갈렸다: 마을 어르신에게 말을 걸었을 때 1막 개막(<c>ch1_intro</c>, 프롤로그 컷신)이 아니라
        /// 앰비언트 잡담(<c>talk_elder</c>)이 먼저 뜰 수 있었고, 어느 쪽이 뜰지는 실행마다 달랐다.
        /// </summary>
        public static int CompareBeatPriority(StoryBeat a, StoryBeat b, HashSet<string> spineBeatIds)
        {
            if (a == null) return b == null ? 0 : 1;
            if (b == null) return -1;

            int spineA = spineBeatIds != null && spineBeatIds.Contains(a.beatId) ? 0 : 1;
            int spineB = spineBeatIds != null && spineBeatIds.Contains(b.beatId) ? 0 : 1;
            if (spineA != spineB) return spineA - spineB;
            return CompareChapterOrderId(a, b);
        }

        /// <summary>최종장 챕터 ID. <see cref="ChapterRank"/>의 "fin"과 같은 값이다.</summary>
        public const string FinaleChapterId = "fin";

        /// <summary>
        /// <b>목표 도출 전용</b> 순위 — 종장(fin) leaf를 스파인과 같은 급으로 친 뒤 (챕터, order, beatId).
        ///
        /// 에필로그(<c>fin_epilogue</c>)는 아무도 prereq로 지목하지 않는 leaf라, 스파인 우선만 두면
        /// <c>fin_seal</c> 직후 남은 곁이야기 스파인(마을 주민 만남 12명·도감 100/140종·꽃밭 사슬)이 전부
        /// 엔딩보다 앞섰다 — HUD가 "세라에게 말 걸기" 대신 "꼬마 화가 달래에게 말 걸기"를 가리켰다.
        /// 종장 leaf는 <c>fin_seal</c> 뒤에만 자격을 얻으므로(선택 결과는 목표에서 빠진다) 캠페인 도중의
        /// 목표는 그대로다. 본편 leaf 전체를 앞세우면 안 된다 — 놓친 1장 동굴(<c>ch1_cave</c>)이 에필로그를 가로챈다.
        ///
        /// <b>발화 순서(<see cref="CompareBeatPriority"/>)에는 넣지 않는다.</b> 넣으면 엔딩 뒤 관장 하월에게
        /// 말을 걸 때 화해(<c>ch12_clash</c>, 종장 leaf)가 도발(<c>talk_chief</c>, 12장 leaf)보다 먼저 떠
        /// 서사가 거꾸로 흐른다. 그 경우 HUD가 가리킨 비트는 같은 행동을 한 번 더 하면 뜬다
        /// (「한 걸음에 시도가 2회」 — rules/testing.md).
        /// </summary>
        public static int CompareObjectivePriority(StoryBeat a, StoryBeat b, HashSet<string> spineBeatIds)
        {
            if (a == null) return b == null ? 0 : 1;
            if (b == null) return -1;

            int tierA = ObjectiveTier(a, spineBeatIds);
            int tierB = ObjectiveTier(b, spineBeatIds);
            if (tierA != tierB) return tierA - tierB;
            return CompareChapterOrderId(a, b);
        }

        // 0 = 이야기를 잇는 비트(스파인 또는 종장), 1 = leaf.
        private static int ObjectiveTier(StoryBeat beat, HashSet<string> spineBeatIds)
        {
            if (spineBeatIds != null && spineBeatIds.Contains(beat.beatId)) return 0;
            return beat.chapterId == FinaleChapterId ? 0 : 1;
        }

        // 급이 같을 때의 나머지 순서 — 두 비교가 공유한다.
        private static int CompareChapterOrderId(StoryBeat a, StoryBeat b)
        {
            int chapterA = ChapterRank(a.chapterId);
            int chapterB = ChapterRank(b.chapterId);
            if (chapterA != chapterB) return chapterA - chapterB;

            if (a.order != b.order) return a.order - b.order;

            // 앞 셋이 모두 같을 때 남는 비결정성을 없앤다.
            return string.CompareOrdinal(a.beatId, b.beatId);
        }

        /// <summary>
        /// trigger.type → 목표 종류. 알 수 없는 타입은 Freeform으로 떨어진다(안전한 쪽 —
        /// 위치를 모르면 화살표를 띄우지 않는다).
        ///
        /// 문자열 리터럴을 여기 다시 적지 않고 <see cref="StoryDirector"/>의 상수를 쓴다.
        /// 사본을 두면 저쪽 상수가 바뀔 때 여기가 조용히 Freeform으로 흘러 목표가 사라진다.
        /// </summary>
        public static StoryObjectiveKind KindOf(string triggerType, string requiredRegionId = null)
        {
            // 대결 승리도 "그 간부에게 가는 것"이 할 일이다 — param이 storyNpcId라 같은 경로로 안내한다.
            if (triggerType == StoryDirector.TriggerNpcTalk
                || triggerType == StoryDirector.TriggerDuelWin) return StoryObjectiveKind.TalkToNpc;
            if (triggerType == StoryDirector.TriggerRegionEnter) return StoryObjectiveKind.EnterRegion;
            if (triggerType == StoryDirector.TriggerSubAreaEnter) return StoryObjectiveKind.EnterSubArea;
            if (triggerType == StoryDirector.TriggerGuardianDefeat) return StoryObjectiveKind.DefeatGuardian;
            // 무param 포획/전투는 리전 게이트가 유일한 위치다 — 그게 있으면 거기로 안내한다.
            // 정화(RegionCleansed)도 같은 자리다: 할 일이 "그 리전에서 거점을 부수는 것"이라
            // 리전 중심으로 안내하면 맞다. 저작 시 param과 requiredRegionId를 같은 리전으로 둔다.
            if ((triggerType == StoryDirector.TriggerCaptureInsect
                    || triggerType == StoryDirector.TriggerBattleWin
                    || triggerType == StoryDirector.TriggerRegionCleansed)
                && !string.IsNullOrEmpty(requiredRegionId))
                return StoryObjectiveKind.ActInRegion;
            return StoryObjectiveKind.Freeform;
        }

        /// <summary>
        /// 이 스토리 NPC와 <b>이야기를 한 번이라도 나눴는가</b>. 그 인물이 화자인 비트나
        /// 그에게 말 거는 비트를 하나라도 열람했으면 true.
        ///
        /// 명부회 간부 보스전이 이걸 묻는다. 호출부가 "이번 대화에서 비트가 안 떴다"만 보면
        /// <b>이미 소개를 봤다</b>와 <b>아직 차례가 아니다</b>를 구분하지 못한다 — 집게·저울·
        /// 관장의 소개는 서브에리어 대치 비트에 걸려 있어서, 리전에 도착해 본진의 그들에게
        /// 말을 걸면 이름도 모르는 채 보스전이 시작됐다(최종 보스인 관장까지).
        ///
        /// 인물 목록을 코드에 박지 않고 저작 데이터에서 낸다 — <c>speakerNpcId</c>는 대치·격전
        /// 비트가, <c>trigger.param</c>은 여운 비트가 채운다.
        /// </summary>
        public static bool HasMetNpc(
            IEnumerable<StoryBeat> beats, System.Func<string, bool> isSeen, string npcId)
        {
            if (beats == null || isSeen == null || string.IsNullOrEmpty(npcId)) return false;

            foreach (StoryBeat beat in beats)
            {
                if (beat == null || string.IsNullOrEmpty(beat.beatId)) continue;
                if (!isSeen(beat.beatId)) continue;
                if (beat.speakerNpcId == npcId) return true;
                if (beat.trigger != null && beat.trigger.type == StoryDirector.TriggerNpcTalk
                    && beat.trigger.param == npcId) return true;
            }
            return false;
        }

        /// <summary>
        /// <c>LevelReach</c>/<c>DexProgress</c>의 임계값. 그 외 트리거이거나 파싱 실패면 <c>-1</c>.
        /// </summary>
        public static int ThresholdOf(string triggerType, string param)
        {
            if (triggerType != StoryDirector.TriggerLevelReach
                && triggerType != StoryDirector.TriggerDexProgress) return -1;
            return int.TryParse(param, out int n) ? n : -1;
        }

        /// <summary>
        /// 위치로 안내할 수 없는(또는 이미 도착한) 목표의 한 줄 문구. <b>순수 함수다</b> —
        /// 이름 조회(곤충·리전·퀘스트)는 호출부가 미리 해서 넘긴다.
        ///
        /// 예전엔 이 자리가 통째로 "모험을 이어가세요" 한 문장이었다. 82비트 중 34개(41%)가
        /// 거기로 떨어졌고, 그중 28개는 <c>requiredRegionId</c>를 이미 갖고 있어서 어디로 가라고
        /// 말할 수 있었는데도 말하지 않았다.
        /// </summary>
        /// <param name="inTargetRegion">플레이어가 이미 그 리전 안에 있는가(있으면 지명을 뺀다).</param>
        /// <param name="current">진행형 목표의 현재값. 모르면 <c>-1</c>(그때는 임계값만 띄운다).</param>
        public static string DescribeActionObjective(
            string triggerType, string regionName, bool inTargetRegion,
            string insectName, string questTitle, int threshold, int current)
        {
            bool elsewhere = !string.IsNullOrEmpty(regionName) && !inTargetRegion;

            if (triggerType == StoryDirector.TriggerCaptureInsect)
            {
                if (!string.IsNullOrEmpty(insectName))
                    return elsewhere ? $"{regionName}에서 {insectName} 포획" : $"{insectName} 포획하기";
                return elsewhere ? $"{regionName}에서 곤충 포획" : "야생 곤충 1마리 포획";
            }

            if (triggerType == StoryDirector.TriggerBattleWin)
            {
                // 종을 지정한 비트(fin_seal 등)는 **무엇을 이겨야 하는지** 말해 준다.
                // 안 그러면 "이름 없는 자리에서 전투 승리"로 떨어져, 아무거나 이기면 되는 줄 안다.
                if (!string.IsNullOrEmpty(insectName))
                    return elsewhere ? $"{regionName}에서 {insectName} 쓰러뜨리기" : $"{insectName} 쓰러뜨리기";
                return elsewhere ? $"{regionName}에서 전투 승리" : "야생 곤충과 전투 승리";
            }

            if (triggerType == StoryDirector.TriggerRegionCleansed)
                return elsewhere ? $"{regionName}의 명부회 거점 무너뜨리기" : "명부회 거점 무너뜨리기";

            if (triggerType == StoryDirector.TriggerLevelReach)
                return current >= 0
                    ? $"트레이너 Lv.{threshold} 달성 · 현재 Lv.{current}"
                    : $"트레이너 Lv.{threshold} 달성";

            if (triggerType == StoryDirector.TriggerDexProgress)
                return current >= 0
                    ? $"도감 {threshold}종 기록 · 현재 {current}종"
                    : $"도감 {threshold}종 기록";

            if (triggerType == StoryDirector.TriggerQuestComplete)
                return !string.IsNullOrEmpty(questTitle) ? $"'{questTitle}' 완료하기" : "퀘스트 완료하기";

            // 알 수 없는 트리거 — 리전이라도 알면 그쪽으로, 아니면 마지막 폴백.
            return !string.IsNullOrEmpty(regionName) ? $"{regionName}(으)로" : "모험을 이어가세요";
        }

        /// <summary>
        /// 리전 목표의 한 줄 문구. <b>순수 함수다</b> — 해금·격파 판정은 호출부가 한다.
        ///
        /// 2막 도착 비트의 prereq는 전부 <b>앞 챕터 대치</b>라 수문장을 건너뛴다. 그래서
        /// <c>ch7_opening</c> 직후 목표가 "텅 빈 들(으)로"였는데 <c>hollow</c>는 유적 수문장을
        /// 넘기 전엔 잠겨 있다 — 들어가지도 못하는 곳을 가리켰다(hollow→nameless 6링크 전부).
        /// <c>gd_*</c>는 leaf라 목표로 안 뽑히니 이 문구가 유일한 안내다.
        /// </summary>
        /// <param name="gateRegionName">
        /// 목적지가 잠겨 있을 때 그 열쇠를 쥔 리전(<c>RegionManager.GetGatekeeperRegion</c>).
        /// 비면 잠기지 않은 것으로 본다.
        /// </param>
        /// <param name="gateGuardianLevel">그 리전 수문장 레벨. 0 이하면 권장 레벨을 뺀다.</param>
        public static string DescribeRegionObjective(
            string regionName, bool guardian, int guardianLevel,
            string gateRegionName = null, int gateGuardianLevel = 0)
        {
            string lvl = gateGuardianLevel > 0 ? $" · 권장 Lv.{gateGuardianLevel}" : string.Empty;
            if (!string.IsNullOrEmpty(gateRegionName))
                return $"{regionName}(으)로 가려면 {gateRegionName} 수문장 격파{lvl}";

            if (guardian)
                return guardianLevel > 0
                    ? $"{regionName} 수문장 격파 · 권장 Lv.{guardianLevel}"
                    : $"{regionName} 수문장 격파";
            return $"{regionName}(으)로";
        }

        /// <summary>
        /// 지금 기다리고 있는 비트 하나를 <b>결정적으로</b> 고른다.
        ///
        /// <see cref="StoryService.AllBeats"/>는 <c>Dictionary.Values</c>라 순서가 비결정적이다 —
        /// 정렬 없이 첫 일치를 집으면 같은 세이브인데 실행마다 다른 목표가 뜬다.
        /// 순위는 (스파인 우선, 챕터, order, beatId)다. beatId까지 넣는 것은 앞 셋이 모두 같을 때
        /// 남는 비결정성을 없애기 위해서다.
        ///
        /// 스파인을 우선하되 <b>스파인이 없으면 leaf라도 고른다</b> — 캠페인 마지막 비트는
        /// 아무도 prerequisite로 지목하지 않아 스파인 집합에 없다. 스파인만 고집하면
        /// 최종장에서 목표가 사라진다.
        /// </summary>
        /// <param name="isQuestDone">
        /// 튜토리얼 퀘스트 완료 판정. <c>requiredQuestId</c>가 걸린 비트를 거르는 데 쓴다 —
        /// 없으면 <b>잠긴 목표를 안내하게 된다</b>(튜토리얼 중에 "마을 어르신에게 말 걸기"가 뜨는데
        /// 정작 가서 말을 걸면 아무 일도 안 일어난다). null이면 게이트를 무시한다.
        /// </param>
        public static StoryBeat SelectObjectiveBeat(
            IEnumerable<StoryBeat> beats,
            System.Func<string, bool> isSeen,
            HashSet<string> spineBeatIds,
            System.Func<string, bool> isQuestDone = null,
            HashSet<string> choiceTargetIds = null)
        {
            if (beats == null || isSeen == null) return null;

            StoryBeat best = null;

            foreach (StoryBeat beat in beats)
            {
                if (beat == null || string.IsNullOrEmpty(beat.beatId)) continue;
                if (isSeen(beat.beatId)) continue;
                // 선택지 결과는 고르는 순간 뜨는 것이지 찾아가는 목표가 아니다.
                if (choiceTargetIds != null && choiceTargetIds.Contains(beat.beatId)) continue;
                // prereq 미충족 = 아직 차례가 아니다.
                if (!string.IsNullOrEmpty(beat.prerequisiteBeatId) && !isSeen(beat.prerequisiteBeatId))
                    continue;
                // 퀘스트 게이트 미충족 = 지금 가도 열리지 않는다. 안내하면 안 된다.
                if (isQuestDone != null && !string.IsNullOrEmpty(beat.requiredQuestId)
                    && !isQuestDone(beat.requiredQuestId))
                    continue;
                // 진행 게이트 미충족 = 아직 그 단계가 아니다. 발화 쪽
                // (StoryDirector.BeatGateSatisfied)과 반드시 같은 답을 내야 한다 — 한쪽만 걸면
                // 잠긴 비트를 목표로 안내해 놓고, 가서 말을 걸면 아무 일도 일어나지 않는다.
                if (!string.IsNullOrEmpty(beat.requiredBeatId) && !isSeen(beat.requiredBeatId))
                    continue;

                // 발화 순서(CompareBeatPriority)에 종장 한 가지만 덧붙인 순위 — 까닭은 CompareObjectivePriority.
                if (best == null || CompareObjectivePriority(beat, best, spineBeatIds) < 0)
                    best = beat;
            }

            return best;
        }
    }

    /// <summary>마을 이야기 한 편(주민 한 명)에서 <b>지금</b> 해야 할 일.</summary>
    public enum TaleStepKind
    {
        /// <summary>이 NPC에게 걸린 대화 비트가 아예 없다.</summary>
        None,
        /// <summary>말을 걸면 새 이야기가 나온다 — 지도·머리 위 <c>!</c>.</summary>
        Talk,
        /// <summary>의뢰를 끝냈으니 알리러 가면 된다 — <c>?</c>. 퀘스트 게이트가 걸린 비트가 열린 상태다.</summary>
        Report,
        /// <summary>다음 대화가 <b>의뢰 완료만</b> 기다린다 — 따라가기는 의뢰(리전·진행)를 가리킨다.</summary>
        Errand,
        /// <summary>다음 대화가 본편 진행(<c>requiredBeatId</c>)을 기다린다 — 지금은 할 일이 없다.</summary>
        Waiting,
        /// <summary>이 NPC의 대화를 전부 들었다.</summary>
        Done,
    }

    /// <summary>
    /// 마을 이야기 따라가기·표식의 <b>순수</b> 판정부. <see cref="StoryObjectiveResolver"/>와 같은 이유로
    /// MonoBehaviour와 떼어 둔다 — 여기 게이트 판정은 발화 쪽(<c>StoryDirector.EvaluateTriggers</c>)과
    /// <b>같은 답</b>을 내야 한다. 한쪽만 다르면 <c>!</c>를 띄워 놓고 가서 말을 걸면 아무 일도 없다.
    ///
    /// 이야기 한 편은 <b>한 NPC에게 거는 <c>NpcTalk</c> 비트의 사슬</b>이다. 중간의 포획 징후 비트는
    /// 곁들임이라 따라가지 않는다 — 할 일은 의뢰(퀘스트)가 들고, 말할 차례는 대화 비트가 든다.
    /// </summary>
    public static class StoryTaleResolver
    {
        /// <summary>마을 이야기 챕터 ID. 저널 탭(<c>StoryJournalUI</c>)과 같은 값이다.</summary>
        public const string TownChapterId = "town";

        /// <summary>
        /// 이 NPC의 이야기에서 지금 할 일. <paramref name="beat"/>는 Talk/Report/Errand일 때 그 대화 비트,
        /// <paramref name="questId"/>는 Errand일 때 기다리는 의뢰다.
        /// </summary>
        public static TaleStepKind ResolveStep(
            IEnumerable<StoryBeat> beats, string npcId,
            System.Func<string, bool> isSeen, System.Func<string, bool> isQuestDone,
            out StoryBeat beat, out string questId)
        {
            beat = null;
            questId = null;
            if (beats == null || isSeen == null || string.IsNullOrEmpty(npcId)) return TaleStepKind.None;

            bool any = false, anyUnseen = false;
            StoryBeat talk = null, errand = null;

            foreach (StoryBeat b in beats)
            {
                if (b == null || b.trigger == null || string.IsNullOrEmpty(b.beatId)) continue;
                if (b.trigger.type != StoryDirector.TriggerNpcTalk || b.trigger.param != npcId) continue;

                any = true;
                if (isSeen(b.beatId)) continue;
                anyUnseen = true;

                // 순서(prereq)·단계(requiredBeatId)가 막혀 있으면 아직 차례가 아니다.
                if (!string.IsNullOrEmpty(b.prerequisiteBeatId) && !isSeen(b.prerequisiteBeatId)) continue;
                if (!string.IsNullOrEmpty(b.requiredBeatId) && !isSeen(b.requiredBeatId)) continue;

                bool questOpen = string.IsNullOrEmpty(b.requiredQuestId)
                    || (isQuestDone != null && isQuestDone(b.requiredQuestId));
                if (questOpen)
                {
                    if (talk == null || StoryObjectiveResolver.CompareBeatPriority(b, talk, null) < 0) talk = b;
                }
                else if (errand == null || StoryObjectiveResolver.CompareBeatPriority(b, errand, null) < 0)
                {
                    errand = b;
                }
            }

            if (talk != null)
            {
                beat = talk;
                return string.IsNullOrEmpty(talk.requiredQuestId) ? TaleStepKind.Talk : TaleStepKind.Report;
            }
            if (errand != null)
            {
                beat = errand;
                questId = errand.requiredQuestId;
                return TaleStepKind.Errand;
            }
            if (anyUnseen) return TaleStepKind.Waiting;
            return any ? TaleStepKind.Done : TaleStepKind.None;
        }

        /// <summary>
        /// 마을 이야기 주민 목록 — <see cref="TownChapterId"/> 챕터의 <c>NpcTalk</c> 대상.
        /// 인물 목록을 코드에 박지 않는다(주민을 늘려도 여기는 그대로다). 순서는 처음 등장한 비트의
        /// <c>order</c> 순이라 저작 순서(초원 → 연못 → …)와 같다.
        /// </summary>
        public static List<string> CollectTaleNpcIds(IEnumerable<StoryBeat> beats)
        {
            var firstOrder = new Dictionary<string, int>();
            if (beats != null)
            {
                foreach (StoryBeat b in beats)
                {
                    if (b == null || b.trigger == null || b.chapterId != TownChapterId) continue;
                    if (b.trigger.type != StoryDirector.TriggerNpcTalk || string.IsNullOrEmpty(b.trigger.param)) continue;
                    if (!firstOrder.TryGetValue(b.trigger.param, out int o) || b.order < o)
                        firstOrder[b.trigger.param] = b.order;
                }
            }
            var ids = new List<string>(firstOrder.Keys);
            ids.Sort((a, c) => firstOrder[a] != firstOrder[c]
                ? firstOrder[a].CompareTo(firstOrder[c]) : string.CompareOrdinal(a, c));
            return ids;
        }

        /// <summary>
        /// 이 의뢰(questId)를 기다리는 대화 비트의 NPC — 퀘스트 목록의 [따라가기]가 쓴다. 없으면 null.
        /// <b>마을 이야기(town) 비트만 본다.</b> 본편도 퀘스트 게이트를 쓴다 — <c>ch1_intro</c>가
        /// <c>q_move</c>를 물어서, 거르지 않으면 튜토리얼 "첫 걸음!" 행에 [따라가기]가 떠 마을 어르신을
        /// "의뢰"로 따라갔다.
        /// </summary>
        public static string FindNpcForQuest(IEnumerable<StoryBeat> beats, string questId)
        {
            if (beats == null || string.IsNullOrEmpty(questId)) return null;
            foreach (StoryBeat b in beats)
            {
                if (b == null || b.trigger == null || b.chapterId != TownChapterId || b.requiredQuestId != questId) continue;
                if (b.trigger.type == StoryDirector.TriggerNpcTalk && !string.IsNullOrEmpty(b.trigger.param))
                    return b.trigger.param;
            }
            return null;
        }

        /// <summary>
        /// 의뢰 진행 한 줄("연못에서 곤충 포획 3/5"). <b>순수 함수</b> — 이름·진행은 호출부가 읽어 넘긴다.
        /// </summary>
        /// <param name="inRegion">이미 그 리전 안이면 지명을 뺀다(스토리 목표 문구와 같은 규칙).</param>
        public static string DescribeErrand(InsectGame.Core.QuestType type, string regionName,
            bool inRegion, int current, int target)
        {
            string verb;
            switch (type)
            {
                case InsectGame.Core.QuestType.Capture:
                case InsectGame.Core.QuestType.CaptureRarity: verb = "곤충 포획"; break;
                case InsectGame.Core.QuestType.CaptureRare: verb = "희귀 곤충 포획"; break;
                case InsectGame.Core.QuestType.Battle: verb = "전투 승리"; break;
                case InsectGame.Core.QuestType.RaidBattle: verb = "레이드 승리"; break;
                case InsectGame.Core.QuestType.VisitSubArea: verb = "숨은 장소 방문"; break;
                case InsectGame.Core.QuestType.NpcDuel: verb = "대결 승리"; break;
                default: verb = "의뢰 진행"; break;
            }
            string progress = target > 0 ? $" {UnityEngine.Mathf.Clamp(current, 0, target)}/{target}" : string.Empty;
            return !inRegion && !string.IsNullOrEmpty(regionName)
                ? $"{regionName}에서 {verb}{progress}"
                : $"{verb}{progress}";
        }
    }
}
