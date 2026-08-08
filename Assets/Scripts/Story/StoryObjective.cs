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
        /// 고정 위치가 없는 목표(전투 승리·포획·레벨·도감). 문구만 띄우고 자동 주행은 막는다 —
        /// "어디로"가 없는 목표에 화살표를 띄우면 없는 곳을 가리키게 된다.
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
        /// <summary>대상 식별자 — npcId / regionId / subAreaId. Freeform이면 빈 문자열.</summary>
        public readonly string TargetId;
        /// <summary>비트의 requiredRegionId(비어 있을 수 있음). Freeform 목표의 위치 힌트로 쓴다.</summary>
        public readonly string RequiredRegionId;

        public StoryObjective(string beatId, StoryObjectiveKind kind, string targetId, string requiredRegionId)
        {
            BeatId = beatId;
            Kind = kind;
            TargetId = targetId ?? string.Empty;
            RequiredRegionId = requiredRegionId ?? string.Empty;
        }

        /// <summary>월드에 갈 곳이 있는가 — 자동 주행 버튼을 띄울지 가른다.</summary>
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
            if (chapterId == "fin") return 1000;   // 최종장 — 본편 뒤
            return 2000;                            // side / npc 등 곁이야기
        }

        /// <summary>
        /// trigger.type → 목표 종류. 알 수 없는 타입은 Freeform으로 떨어진다(안전한 쪽 —
        /// 위치를 모르면 화살표를 띄우지 않는다).
        ///
        /// 문자열 리터럴을 여기 다시 적지 않고 <see cref="StoryDirector"/>의 상수를 쓴다.
        /// 사본을 두면 저쪽 상수가 바뀔 때 여기가 조용히 Freeform으로 흘러 목표가 사라진다.
        /// </summary>
        public static StoryObjectiveKind KindOf(string triggerType)
        {
            if (triggerType == StoryDirector.TriggerNpcTalk) return StoryObjectiveKind.TalkToNpc;
            if (triggerType == StoryDirector.TriggerRegionEnter) return StoryObjectiveKind.EnterRegion;
            if (triggerType == StoryDirector.TriggerSubAreaEnter) return StoryObjectiveKind.EnterSubArea;
            if (triggerType == StoryDirector.TriggerGuardianDefeat) return StoryObjectiveKind.DefeatGuardian;
            return StoryObjectiveKind.Freeform;
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
            System.Func<string, bool> isQuestDone = null)
        {
            if (beats == null || isSeen == null) return null;

            StoryBeat best = null;
            int bestSpine = 0, bestChapter = 0, bestOrder = 0;

            foreach (StoryBeat beat in beats)
            {
                if (beat == null || string.IsNullOrEmpty(beat.beatId)) continue;
                if (isSeen(beat.beatId)) continue;
                // prereq 미충족 = 아직 차례가 아니다.
                if (!string.IsNullOrEmpty(beat.prerequisiteBeatId) && !isSeen(beat.prerequisiteBeatId))
                    continue;
                // 퀘스트 게이트 미충족 = 지금 가도 열리지 않는다. 안내하면 안 된다.
                if (isQuestDone != null && !string.IsNullOrEmpty(beat.requiredQuestId)
                    && !isQuestDone(beat.requiredQuestId))
                    continue;

                int spine = spineBeatIds != null && spineBeatIds.Contains(beat.beatId) ? 0 : 1; // 0이 우선
                int chapter = ChapterRank(beat.chapterId);

                if (best == null
                    || spine < bestSpine
                    || (spine == bestSpine && chapter < bestChapter)
                    || (spine == bestSpine && chapter == bestChapter && beat.order < bestOrder)
                    || (spine == bestSpine && chapter == bestChapter && beat.order == bestOrder
                        && string.CompareOrdinal(beat.beatId, best.beatId) < 0))
                {
                    best = beat;
                    bestSpine = spine;
                    bestChapter = chapter;
                    bestOrder = beat.order;
                }
            }

            return best;
        }
    }
}
