using System.Collections.Generic;

namespace InsectGame.Story
{
    // 스토리 한 조각(비트) — JSON(Assets/Resources/Story.json)에 저작, JsonUtility로 파싱.
    // 퀘스트/대화/리전을 갈아엎지 않고 관찰(구독)만 하는 가산 레이어. 필드 컨벤션은 데이터 클래스
    // 관례를 따라 public(GameSaveData/TutorialQuest/InsectLoreEntry와 동형 — JsonUtility 직렬화).
    //
    // **모든 비트는 일생 1회다 — 비트별 토글은 없다.** 반복 여부는 StoryDirector의
    // seenBeatIds(story_progress.json)가 전역으로 정하고, 열람한 비트는 EvaluateTriggers가
    // 무조건 건너뛴다. 예전엔 `oneShot` 필드가 있었지만 **읽는 코드가 한 곳도 없어서**
    // `"oneShot": false`로 저작해도 아무 일이 일어나지 않았다(데이터가 동작을 거짓말했다).
    // 되살리려면 onComplete 재지급부터 막아야 한다 — 앰비언트 비트(talk_elder 등)도 캔디 5개를
    // 주므로 그대로 반복시키면 말 걸기 무한 파밍이 된다. 그건 정리가 아니라 별도 설계다.
    [System.Serializable]
    public class StoryBeat
    {
        public string beatId;
        public string chapterId;
        public int order;
        // 선행 비트(옵션). 비면 무조건 충족. 채워지면 그 비트를 이미 열람해야 발화.
        public string prerequisiteBeatId;
        // 리전 잠금(옵션). 채워지면 현재 리전이 이 ID일 때만 발화. 무param CaptureInsect/BattleWin의
        // 늦발화 얼룩(엉뚱한 리전에서 옛 비트가 뜨는 것) 차단. 비면 무제약 — JsonUtility 누락 필드는
        // 기본값(null)이라 기존 비트 전부 호환. story_lint 검사 7이 대상 존재·무가드 권고를 검증.
        public string requiredRegionId;
        // 퀘스트 잠금(옵션). 채워지면 그 튜토리얼 퀘스트를 **완료해야** 발화한다.
        // 스토리를 튜토리얼과 갈라 놓는 장치다 — ch1_intro가 이걸 써서, 기본 조작(이동·포획·
        // 컬렉션·도감)을 익히기 전에는 마을 어르신이 이야기를 열어주지 않는다.
        // 비면 무제약. JsonUtility는 JSON에 없는 필드를 건드리지 않으므로 기존 비트 전부 호환.
        public string requiredQuestId;
        // 진행 게이트(옵션). 채워지면 그 비트를 **이미 열람해야** 발화한다.
        // prerequisiteBeatId가 체인의 '순서'를 맡는다면 이쪽은 '단계'를 맡는다 — 둘은 AND다.
        //
        // 여운(echo) 비트가 이걸 쓴다. 그것들은 "같은 NPC의 직전 여운"만 prereq로 물고 있어서
        // **진행과 무관하게 말을 반복해 걸기만 하면 뒷 챕터 대사가 나왔다** — 시작 지역인 초원에서
        // 라온에게 세 번 말하면 12장 복귀 대사가, 어르신에게 세 번 말하면 11장의 최대 반전이,
        // 숲에서 세라에게 네 번 말하면 **엔딩 에필로그 전문**이 보상까지 딸려 나왔다.
        // StoryBible 6장은 여운이 '뒤늦게' 뜨는 것만 대비했지 조기 발화는 보지 못했다.
        //
        // **가리키는 비트는 재발화 트리거여야 한다**(RegionEnter/SubAreaEnter/BattleWin 등).
        // QuestComplete·GuardianDefeat처럼 일생 1회 발화하는 비트를 게이트로 지목하면 그 순간을
        // 놓친 세이브가 영구 정지한다 — story_lint 검사 16이 막는다.
        //
        // 비면 무제약. JsonUtility는 JSON에 없는 필드를 건드리지 않으므로 기존 비트 전부 호환.
        public string requiredBeatId;
        public StoryTrigger trigger;
        // 이름/초상만 참조(대사는 lines[]에 저작). NpcDialogueDatabase 앰비언트와 분리.
        public string speakerNpcId;
        public List<StoryLine> lines = new List<StoryLine>();
        // 분기(옵션) — 데이터 모델만 보존(story_lint 검사 4). 현 렌더러는 lines[] 순차 표시.
        public List<StoryChoice> choices = new List<StoryChoice>();
        public StoryReward onComplete;
        // 대사가 끝난 뒤 재생할 컷신(옵션). CutsceneLibrary의 ID여야 한다 — story_lint 검사 9가
        // 실재성을 고정한다(오타는 런타임에 LogWarning만 찍고 조용히 안 나온다).
        // JsonUtility는 JSON에 없는 필드를 건드리지 않으므로 기존 비트 전부 null로 남아 호환된다.
        public string cutsceneId;
        // 대사가 끝난 뒤 재생할 **영상 파일**(옵션). StoryVideoLibrary의 ID여야 한다 — story_lint 검사 25가
        // 실재성·switch 배선·파일 배치를 본다. **cutsceneId·stageExitId와 같은 비트에 두지 않는다**(검사 13) —
        // 셋 다 StoryBeatCompleted를 구독해 조작·모달을 뺏으므로 서로의 복구를 덮어쓴다.
        // 대사 **뒤**의 마무리다 — "그 대사가 끝난 다음 화면에 무엇이 남아야 하는가". 대사 **앞**은 introVideoId.
        public string videoId;
        // 대사 **앞**에 재생할 영상 파일(옵션) — 설명을 영상이 맡는다(초등 고학년 대상: "영상이 설명하고, 대사는 짧게").
        // StoryVideoLibrary의 ID여야 한다(검사 25). 순서는 **영상 → NPC 등장 연출(stageEnterId) → 「지난 이야기」 카드 → 대사**다 —
        // NpcDialogueUI의 연출 게이트(IStoryStagePrelude) 하나를 StoryPreludeChain이 영상·연출 차례로 이어 준다.
        // 영상은 어떤 경로로 끝나든(끝·건너뛰기·파일 없음·디코더 오류·시간 초과·비활성) 대사를 연다 — 못 열면 그 비트가
        // pendingBeatId에 갇혀 캠페인이 멈춘다(StoryVideoDirector.TryPlayPrelude).
        // **videoId와 같은 비트에 두지 않는다**(검사 13 — 한 지휘자가 두 편을 한 비트에 틀 수 없다). 영상이 설명하므로
        // 이 비트의 대사는 **6줄 이하**다(검사 36). 저널 다시보기에서는 대사를 다시 읽어도 이 영상이 앞에 붙지 않는다
        // (「▶ 영상」 버튼이 따로 튼다). 비면 영상 없음 — 기존 비트는 전부 null로 호환된다.
        public string introVideoId;
        // 대사 **앞**에 재생할 NPC 연출(옵션) — 등장·다가옴. StoryStageLibrary의 ID여야 한다.
        // 대사가 없는 비트에는 무의미하다(모달 자체가 안 뜨므로 게이트가 걸리지 않는다).
        public string stageEnterId;
        // 대사 **뒤**에 재생할 NPC 연출(옵션) — 퇴장·안내.
        // **cutsceneId와 같은 비트에 함께 두지 않는다** — 둘 다 조작·카메라를 뺏어 다툰다.
        // story_lint 검사 13이 그 조합을 막는다. **duelAfter와도 함께 두지 않는다**(검사 35 — 상대가 퇴장해 버린다).
        public string stageExitId;
        // HUD 목표의 "왜"(옵션) — 이 비트가 다음 목표로 뽑혔을 때 할 일 아래에 붙는 짧은 이유 한 줄
        // ("모래언덕으로" ← "상자에 갇힌 곤충이 있대"). 목표 문구는 트리거가 만들고(StoryObjectiveResolver),
        // 이유만 저작한다. 스파인 비트에 비어 있으면 story_lint가 잡는다. 기존 비트는 null — 이유 없이 할 일만 뜬다.
        public string why;
        // 대사가 끝나면 **곧바로** 이 상대와 대결한다(옵션) — 값은 대결 상대의 storyNpcId다.
        // 명부회 간부(NpcBossDuels)면 간부전, 라온(NpcRivalDuels)이면 그 자리에서 열 수 있는 라이벌 단계다.
        // 예전엔 대치 대사가 "다시 말을 걸면 붙는다"(talk_*)로 끝나 이야기 속 싸움이 한 박자 뒤로 밀려 있었다.
        //
        // **순서**: 대사 → 이 비트의 영상·컷신·NPC 연출 → (선택지가 있으면) 고른 결과 대사 → 미뤄 둔 다른 대사 → 대결.
        // StoryDuelLauncher가 StoryBeatCompleted에서 대기열에 넣었다가 모달·전투 화면·스토리 큐가 모두 빈 첫 순간에 연다.
        // 시작하지 못하면(재도전 대기·출전 곤충 없음) 조용히 버린다 — 그 상대에게 다시 말을 걸면 시작되는 길이 따로 있다
        // (간부: WorldInteractionController → TryStartBossDuel, 라온: 대화창 [승부하기]).
        //
        // **stageExitId와 함께 두지 않는다** — 퇴장 연출이 대결 상대를 걸어 나가게 한다. 대상이 대결 표에 있는지, 라온이면
        // 이 비트를 본 직후 그 리전에서 단계가 열리는지는 story_lint 검사 35가 본다(오타는 런타임에 로그 한 줄로 조용히 사라진다).
        // 대화창은 이 값을 읽어 마지막 버튼을 「승부!」로 바꿀 수 있다. 비면 대결 없음 — 기존 비트는 전부 null로 호환된다.
        public string duelAfter;
    }

    /// <summary>
    /// 장 하나의 「지난 이야기」 — 그 장을 여는 비트(<see cref="openingBeatId"/>)의 대사 **앞에** 카드로 뜨고,
    /// 저널의 장 탭 머리에도 보인다. 플레이어가 며칠 만에 돌아와도 "어디까지 왔고 왜 여기 왔나"를 세 줄로 되짚게 한다.
    /// 장 ID가 아니라 여는 비트로 거는 이유: 장의 첫 발화가 늘 도착 비트는 아니다 — <c>gd_ruins</c>(7장)는
    /// 7장 개막보다 먼저 뜰 수 있어서, "처음 뜬 7장 비트"에 카드를 걸면 반전 전에 7장 요약이 나온다.
    /// </summary>
    [System.Serializable]
    public class StoryChapter
    {
        public string chapterId;
        /// <summary>"8장 · 모래언덕" — 저널 탭 라벨과 같은 형식.</summary>
        public string title;
        /// <summary>이 비트의 대사 앞에 카드를 띄운다. 비면 카드 없음(저널에만 보인다).</summary>
        public string openingBeatId;
        /// <summary>지난 이야기 — 세 줄 안팎, 한 줄 30자 안팎. 비면 카드 없음(1장처럼 지난 이야기가 없는 장).</summary>
        public List<string> recap = new List<string>();
        /// <summary>이번 장에서 할 일 한 줄.</summary>
        public string goal;
    }

    [System.Serializable]
    public class StoryLine
    {
        // 화자 표시명("라온"·"세라"·"검은 옷의 사내"…). NpcDialogueDatabase.StoryPortraitId가 초상으로 푼다.
        // **"지문"은 인물이 아니라 해설이다** — 초상·이름표 없이 가운데 기울임으로 그린다
        // (StoryDialogueStaging.NarrationSpeaker). 오타는 조용히 초상만 사라지므로 story_lint 검사 27이 본다.
        public string speaker;
        public string text;
        // 줄 연출(옵션) — 쉼표로 여럿: shake·flash·pause·shout·whisper·slow·dark.
        // 해석은 StoryDialogueStaging.ParseFx 한 곳이고, 모르는 토큰은 story_lint 검사 28이 FAIL로 잡는다.
        // JsonUtility는 JSON에 없는 필드를 건드리지 않으므로 기존 줄은 전부 null(연출 없음)로 호환된다.
        public string fx;
    }

    [System.Serializable]
    public class StoryChoice
    {
        public string text;
        public string nextBeatId;
    }

    // 트리거 타입은 문자열(닫힌 enum) — StoryDirector 평가 switch가 전 케이스 처리.
    // RegionEnter / QuestComplete / LevelReach / CaptureInsect / BattleWin / SubAreaEnter / Immediate
    [System.Serializable]
    public class StoryTrigger
    {
        public string type;
        public string param;
    }

    // 퀘스트 보상 필드 재사용(TutorialQuest와 동형). unlockQuestId는 데이터만 보존
    // (스토리→퀘스트 역주입은 설계상 배제 — story_lint 검사 5의 무결성 대상으로만).
    [System.Serializable]
    public class StoryReward
    {
        public int rewardCandy;
        public int rewardExp;
        public string rewardItemId;
        public int rewardItemCount;
        public string rewardInsectId;
        public string rewardInsectDisplayName;
        public int rewardInsectLevel;
        public string unlockQuestId;
    }

    /// <summary>
    /// 이야기 보상을 한 줄로 — "캔디 +5 · 은빛 채집망 ×2 · 하늘소 Lv.6". <b>순수 함수</b>다.
    /// 포함 조건은 <c>StoryDirector.GrantReward</c>의 지급 조건과 같아야 한다 — 어긋나면 "받았는데 안 뜨거나
    /// 뜨는데 안 준" 소식이 된다(퀘스트 쪽 <c>QuestRewardFormatter</c>가 같은 이유로 있다).
    /// </summary>
    public static class StoryRewardText
    {
        /// <param name="itemName">아이템 ID → 표시명. null이거나 빈 값을 주면 ID를 그대로 쓴다.</param>
        /// <param name="grantedInsectName">
        /// <b>실제로 준</b> 곤충의 표시명. 첫 파트너는 플레이어가 고른 종으로 바뀌므로 보상 데이터의 이름
        /// ("첫 파트너")이 아니라 지급부가 넘겨준 이름을 쓴다. 곤충을 못 줬으면(컬렉션 없음) null.
        /// </param>
        public static string Format(StoryReward reward, System.Func<string, string> itemName, string grantedInsectName)
        {
            if (reward == null) return string.Empty;
            var text = new System.Text.StringBuilder();

            if (reward.rewardCandy > 0) Append(text, "캔디 +" + reward.rewardCandy);
            if (reward.rewardExp > 0) Append(text, "경험치 +" + reward.rewardExp);
            if (!string.IsNullOrEmpty(reward.rewardItemId) && reward.rewardItemCount > 0)
            {
                string name = itemName != null ? itemName(reward.rewardItemId) : null;
                if (string.IsNullOrEmpty(name)) name = reward.rewardItemId;
                Append(text, name + " ×" + reward.rewardItemCount);
            }
            if (!string.IsNullOrEmpty(reward.rewardInsectId) && !string.IsNullOrEmpty(grantedInsectName))
            {
                int level = UnityEngine.Mathf.Max(1, reward.rewardInsectLevel);
                Append(text, level > 1 ? grantedInsectName + " Lv." + level : grantedInsectName);
            }
            return text.ToString();
        }

        private static void Append(System.Text.StringBuilder text, string part)
        {
            if (text.Length > 0) text.Append(" · ");
            text.Append(part);
        }
    }

    // JsonUtility 래퍼 — 루트 { "beats": [ ... ], "chapters": [ ... ] } (InsectLoreList와 동형).
    [System.Serializable]
    public class StoryList
    {
        public List<StoryBeat> beats = new List<StoryBeat>();
        // 장별 「지난 이야기」. 배열 순서가 곧 장 순서다. 옛 JSON엔 없어 빈 목록으로 남는다.
        public List<StoryChapter> chapters = new List<StoryChapter>();
    }
}
