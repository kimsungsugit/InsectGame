using System.Collections.Generic;
using InsectGame.Core;
using InsectGame.Story;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 스토리 저널 — 챕터별로 비트를 나열하고, 이미 열람한 것은 다시 읽을 수 있게 한다.
    ///
    /// 왜 필요한가: 스토리가 60비트로 늘면서 "내가 지금 어느 챕터인지 / 무엇을 놓쳤는지"를
    /// 볼 방법이 대화 모달 하나뿐이었다. 대화는 지나가면 사라지므로 진행 상황이 남지 않는다.
    ///
    /// 다시 읽기는 <see cref="NpcDialogueUI.ShowStoryReplay"/>를 쓴다 — 렌더러를 재사용하되
    /// <c>CompleteBeat</c>을 건너뛴다. <b>열람하지 않은 비트는 절대 열지 않는다</b>(잠금 표시만):
    /// 대사를 보여주면서 seen 마킹은 하지 않으므로 나중에 정상 트리거로 또 뜨고,
    /// 반대로 마킹까지 하면 그 자리에서 보상이 새어 나간다.
    ///
    /// <c>StoryBeat.order</c>가 여기서 처음 쓰인다 — 발화 순서는 prereq가 정하고 order는
    /// 문서용 메타였는데(StoryService가 Dictionary라 순서 보장이 없다), 저널의 **표시 순서**로는
    /// 정확히 이 값이 필요하다.
    ///
    /// <b>영상 다시보기</b>: 본 비트에 영상(대사 앞 <c>introVideoId</c>·뒤 <c>videoId</c> — story_lint 검사 13이 한 비트에 하나만 허용)이
    /// 있으면 「다시 읽기」 왼쪽에 「▶ 영상」이 선다. <see cref="StoryVideoDirector.PlayReplay"/>로 틀고(스토리 부수효과 없음) 저널은 그 아래 열린 채 둔다 —
    /// 영상이 모달 스택 맨 위라 ESC·「건너뛰기」는 영상만 닫고 저널로 돌아온다. 영상이 화면을 덮는 동안 저널은 그리지도
    /// 입력을 받지도 않는다(영상의 검은 판은 클릭을 먹지 않아 그 아래 버튼이 눌린다).
    /// </summary>
    public class StoryJournalUI : MonoBehaviour, IModalUI
    {
        private StoryDirector storyDirector;
        private NpcDialogueUI dialogueUI;
        private StoryVideoDirector videoDirector;

        // 대사 앞·뒤 영상이 둘 다 있는 비트는 이야기 순서대로 이어 튼다 — 앞 영상이 끝나면(건너뛰어도) 뒤 영상.
        // **실제 데이터에서는 생기지 않는다** — story_lint 검사 13이 한 비트에 introVideoId와 videoId를 함께 두는 것을 막는다.
        // 그 규칙이 풀리거나 검사 밖 데이터가 들어와도 뒤 영상이 조용히 사라지지 않게 남겨 둔 방어다.
        private string queuedReplayVideoId;
        // 영상을 열지 못했을 때 머리줄에 잠깐 띄우는 안내.
        private string replayNotice;
        private float replayNoticeUntil;
        private const float ReplayNoticeSeconds = 2.5f;

        private bool isOpen;
        private string selectedChapter;
        private Vector2 scroll;
        private readonly UIDirectScroll directScroll = new UIDirectScroll();

        // 챕터 → 비트 목록(order 오름차순). StoryService는 Dictionary라 순서가 없으므로 여기서 정렬해 캐시한다.
        // Story.json은 런타임에 바뀌지 않으니 1회 구성이면 충분하다.
        private List<string> chapterIds;
        private Dictionary<string, List<StoryBeat>> beatsByChapter;
        private int totalBeats;

        private bool stylesReady;
        private GUIStyle titleStyle, closeStyle, tabStyle, rowTitleStyle, rowMetaStyle, hintStyle, lockStyle, noticeStyle;
        private GUIStyle headSectionStyle, headBodyStyle, headGoalStyle;

        // 장 머리(지난 이야기·이번 목표) — 고른 장의 여는 비트를 이미 봤을 때만 그린다(스포일러 금지).
        // 줄 높이는 래핑으로 재야 해서(CalcHeight) (장, 열람 수, 목록 영역)이 바뀔 때만 다시 굽는다.
        private StoryJournalHeadLayout.Plan headPlan;
        private bool headVisible;
        private string headChapterId;
        private int headSeenCount = -1;
        private Rect headArea;
        private string[] headLines;
        private string headGoal;

        /// <summary>
        /// 챕터 탭의 표시 순서와 이름. <b>배열이지 Dictionary가 아니다</b> —
        /// <c>Dictionary</c>는 열거 순서를 보장하지 않아서, 탭 순서를 거기 맡기면 챕터가
        /// 뒤섞여 뜰 수 있다(스토리 엔진이 <c>StoryService.AllBeats()</c>의 Dictionary 순서를
        /// 못 믿어 prereq로 엮는 것과 같은 이유다). 순서가 의미를 가지면 배열로 적는다.
        ///
        /// Story.json의 chapterId는 현재 ch1…ch12/fin/bl/town/side/npc다. 여기 없는 chapterId는
        /// ID를 그대로 라벨로 쓰고 뒤에 붙는다 — 챕터를 추가해도 저널이 깨지지 않는다.
        /// </summary>
        private static readonly (string id, string label)[] ChapterOrder =
        {
            ("ch1", "1장 · 초원"),
            ("ch2", "2장 · 연못"),
            ("ch3", "3장 · 숲"),
            ("ch4", "4장 · 습지"),
            ("ch5", "5장 · 산"),
            ("ch6", "6장 · 고대 유적"),
            ("ch7", "7장 · 텅 빈 들"),
            ("ch8", "8장 · 모래언덕"),
            ("ch9", "9장 · 서릿길"),
            ("ch10", "10장 · 잿불 골짜기"),
            ("ch11", "11장 · 우듬지"),
            ("ch12", "12장 · 이름 없는 자리"),
            ("fin", "종장"),
            ("bl", "오염 거점"),
            ("town", "마을 이야기"),
            ("side", "곁이야기"),
            ("npc", "동행자와의 대화"),
        };

        public void AutoWire(StoryDirector director, NpcDialogueUI dialogue)
        {
            if (storyDirector == null) storyDirector = director;
            if (dialogueUI == null) dialogueUI = dialogue;
        }

        /// <summary>영상 다시보기 — 없으면 「▶ 영상」 버튼이 서지 않는다.</summary>
        public void AutoWire(StoryVideoDirector video)
        {
            if (videoDirector == null) videoDirector = video;
        }

        // 인덱스는 선택지 열람 여부에 따라 달라진다(안 고른 결과는 숨긴다). 세션당 1회 만들면
        // 저널을 먼저 연 뒤 고른 결과가 세션 내내 안 뜨고 진행률 분모도 어긋난다. 이벤트 구독 대신
        // **SeenCount 스냅샷**을 키로 쓴다 — 클라우드 반영(StoryDirector.ReloadFromDisk)은 이벤트 없이
        // seen 집합을 통째로 바꾸므로 구독으로는 못 잡는다. 탭 캐시(EnsureTabCache)와 같은 방식이다.
        private int indexSeenCount = -1;

        public bool IsOpen => isOpen;

        public void Toggle()
        {
            isOpen = !isOpen;
            if (isOpen)
            {
                EnsureIndex();
                if (string.IsNullOrEmpty(selectedChapter)) selectedChapter = LatestReachedChapter();
                scroll = Vector2.zero;
            }
            directScroll.Reset();
            queuedReplayVideoId = null;
            if (isOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }

        public void CloseModal()
        {
            isOpen = false;
            directScroll.Reset();
            queuedReplayVideoId = null;
            ModalUIRegistry.Unregister(this);
        }

        private void OnDisable()
        {
            // isOpen을 남겨 두면 레지스트리엔 없는데 열린 것으로 아는 상태가 된다
            // (ESC가 이 모달을 건너뛰어 영구히 안 닫힘). NpcDialogueUI가 같은 이유로 이렇게 한다.
            isOpen = false;
            directScroll.Reset();
            queuedReplayVideoId = null;
            ModalUIRegistry.Unregister(this);
        }

        /// <summary>영상 다시보기가 화면을 덮고 있는가(이어 틀 영상이 기다리는 한 프레임 포함).</summary>
        private bool VideoCovering => videoDirector != null && (videoDirector.IsPlaying || queuedReplayVideoId != null);

        private void Update()
        {
            // 대사 앞·뒤 영상을 이어 튼다 — 앞 영상이 끝난(또는 건너뛴) 다음 프레임에 뒤 영상.
            if (queuedReplayVideoId == null) return;
            if (!isOpen || videoDirector == null) { queuedReplayVideoId = null; return; }
            if (videoDirector.IsPlaying) return;
            string next = queuedReplayVideoId;
            queuedReplayVideoId = null;
            if (!videoDirector.PlayReplay(next)) ShowReplayNotice();
        }

        // ── 인덱스 ──

        private void EnsureIndex()
        {
            int seenNow = storyDirector != null ? storyDirector.SeenCount : 0;
            if (beatsByChapter != null && indexSeenCount == seenNow) return;
            indexSeenCount = seenNow;

            beatsByChapter = new Dictionary<string, List<StoryBeat>>();
            rowMetaCache.Clear();
            rowVideoCache.Clear();
            headerCache = null;
            totalBeats = 0;
            foreach (StoryBeat beat in StoryService.AllBeats())
            {
                if (beat == null || string.IsNullOrEmpty(beat.beatId)) continue;
                // 선택지 결과 중 안 고른 쪽은 영영 미열람이다 — 목록에 "조건 미상"으로 남기지 않는다.
                // 고른 쪽은 열람됐으니 그대로 올라와 다시 읽을 수 있다.
                if (storyDirector != null && storyDirector.IsChoiceTarget(beat.beatId)
                    && !storyDirector.HasSeen(beat.beatId)) continue;
                string chapter = string.IsNullOrEmpty(beat.chapterId) ? "etc" : beat.chapterId;
                if (!beatsByChapter.TryGetValue(chapter, out List<StoryBeat> list))
                {
                    list = new List<StoryBeat>();
                    beatsByChapter[chapter] = list;
                }
                list.Add(beat);
                totalBeats++;
            }

            foreach (KeyValuePair<string, List<StoryBeat>> pair in beatsByChapter)
            {
                pair.Value.Sort((a, b) => a.order.CompareTo(b.order));
            }

            // 챕터 탭 순서 — ChapterOrder 배열 순서가 먼저, 거기 없는 챕터는 뒤에 붙인다.
            chapterIds = new List<string>();
            for (int i = 0; i < ChapterOrder.Length; i++)
            {
                if (beatsByChapter.ContainsKey(ChapterOrder[i].id)) chapterIds.Add(ChapterOrder[i].id);
            }
            // 미등록 챕터는 Dictionary 순회라 그들끼리의 순서가 비결정적이다 — 정렬해 고정한다.
            List<string> extras = new List<string>();
            foreach (KeyValuePair<string, List<StoryBeat>> pair in beatsByChapter)
            {
                if (!chapterIds.Contains(pair.Key)) extras.Add(pair.Key);
            }
            extras.Sort(string.CompareOrdinal);
            chapterIds.AddRange(extras);
        }

        /// <summary>열람한 비트가 하나라도 있는 마지막 챕터 — 열었을 때 거기부터 보여준다.</summary>
        private string LatestReachedChapter()
        {
            if (chapterIds == null || chapterIds.Count == 0) return null;
            string latest = chapterIds[0];
            foreach (string chapter in chapterIds)
            {
                if (SeenIn(chapter) > 0) latest = chapter;
            }
            return latest;
        }

        private int SeenIn(string chapter)
        {
            if (storyDirector == null || beatsByChapter == null) return 0;
            if (!beatsByChapter.TryGetValue(chapter, out List<StoryBeat> list)) return 0;
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (storyDirector.HasSeen(list[i].beatId)) n++;
            }
            return n;
        }

        private static string ChapterLabel(string chapterId)
        {
            // 장 데이터(Story.json "chapters")의 제목이 있으면 그걸 쓴다 — 순서는 ChapterOrder 배열이 그대로 정한다.
            if (StoryService.TryGetChapter(chapterId, out StoryChapter chapter) && !string.IsNullOrWhiteSpace(chapter.title))
                return chapter.title;
            for (int i = 0; i < ChapterOrder.Length; i++)
            {
                if (ChapterOrder[i].id == chapterId) return ChapterOrder[i].label;
            }
            return chapterId;   // 미등록 챕터는 ID를 그대로 — 추가해도 저널이 안 깨진다
        }

        // ── 렌더 ──

        private void OnGUI()
        {
            if (!isOpen) return;
            // 다시보기 대화가 떠 있는 동안은 저널을 그리지 않는다 — 대화 모달이 위에 있어야 한다.
            if (dialogueUI != null && dialogueUI.IsOpen) return;
            // 영상 다시보기도 같다 — 영상은 화면 전체를 덮지만 검은 판은 클릭을 먹지 않아서, 여기서 그리면
            // 「건너뛰기」 밖을 누른 탭이 그 아래 저널 버튼(다시 읽기·▶ 영상·탭·X)으로 새어 든다.
            if (VideoCovering) return;
            UIScale.Begin();
            EnsureStyles();
            EnsureIndex();
            DrawPanel();
            UIScale.End();
        }

        private void DrawPanel()
        {
            UITheme t = UITheme.Instance;
            Rect panel = UISafeLayout.CenteredPanel(960f, 940f);
            float px = panel.x, py = panel.y, pw = panel.width, ph = panel.height;

            UISurface.Card(new Rect(px, py, pw, ph), t.panelBg, t.surfaceBorder);
            // 헤더 액센트 — 8px 얇은 바라 각진 채로 두고, 긴 축을 카드 반경만큼 물려 둥근 모서리를 뚫지 않게 한다.
            UISurface.Flat(
                new Rect(px + UITheme.Radius.Card, py + 3f, pw - UITheme.Radius.Card * 2f, 8f),
                t.accentAmber);
            GUI.color = Color.white;

            int seen = storyDirector != null ? storyDirector.SeenCount : 0;
            if (headerCache == null || headerSeen != seen)
            {
                headerSeen = seen;
                headerCache = seen + " / " + totalBeats + " 장면을 지나왔다";
            }
            GUI.Label(new Rect(px + 26f, py + 14f, pw - 220f, 50f), "여행의 기록", titleStyle);
            if (replayNotice != null && Time.unscaledTime < replayNoticeUntil)
                UIHelper.LabelFit(new Rect(px + 26f, py + 58f, pw - 220f, 28f), replayNotice, noticeStyle);
            else
                GUI.Label(new Rect(px + 26f, py + 58f, pw - 220f, 28f), headerCache, hintStyle);
            if (GUI.Button(new Rect(px + pw - 74f, py + 14f, 58f, 58f), "X", closeStyle)) { CloseModal(); return; }

            float bodyY = py + 96f;
            float bodyH = ph - (bodyY - py) - 20f;

            // 좌: 챕터 탭 / 우: 비트 목록
            float tabW = UIScale.IsMobileLayout ? 220f : 260f;
            DrawChapterTabs(new Rect(px + 20f, bodyY, tabW, bodyH));
            DrawBeatList(new Rect(px + 20f + tabW + 16f, bodyY, pw - 40f - tabW - 16f, bodyH));
        }

        // 탭 라벨과 챕터별 열람 수는 **저널을 열어 둔 동안 바뀌지 않는다** —
        // 다시 읽기(ShowStoryReplay)는 seen을 마킹하지 않기 때문이다.
        // 매 패스 다시 세면 챕터 15개가 각자 자기 비트를 훑고, 그 안의 HasSeen이
        // `seenBeatIds.Contains`(List O(진행도))라 **비트 72개 × 진행도만큼의 문자열 비교**가 든다.
        // 다 본 세이브면 패스당 5천 회가 넘고 OnGUI는 프레임당 두 패스 이상이다.
        // 무효화 키는 SeenCount — 저널 밖에서 비트를 하나 더 열람하면 자동으로 다시 굽는다.
        private string[] tabLabelCache;
        private bool[] tabUntouchedCache;
        private int tabCacheSeenCount = -1;
        private int tabCacheChapterCount = -1;

        private void EnsureTabCache()
        {
            int seenCount = storyDirector != null ? storyDirector.SeenCount : 0;
            if (tabLabelCache != null
                && tabCacheChapterCount == chapterIds.Count
                && tabCacheSeenCount == seenCount)
            {
                return;
            }

            tabCacheSeenCount = seenCount;
            tabCacheChapterCount = chapterIds.Count;
            tabLabelCache = new string[chapterIds.Count];
            tabUntouchedCache = new bool[chapterIds.Count];
            for (int i = 0; i < chapterIds.Count; i++)
            {
                string chapter = chapterIds[i];
                int done = SeenIn(chapter);
                int total = beatsByChapter[chapter].Count;
                tabLabelCache[i] = $"{ChapterLabel(chapter)}  {done}/{total}";
                tabUntouchedCache[i] = done == 0;   // 배경색 판정 — 세는 일을 두 번 하지 않는다
            }
        }

        private void DrawChapterTabs(Rect area)
        {
            if (chapterIds == null) return;
            EnsureTabCache();
            float rowH = Mathf.Max(UIScale.MinTouchHeight, 52f);
            float gap = 6f;
            // 챕터가 늘어도 영역 안에 들어오도록 행 높이를 줄인다 — 15개 안팎이라 스크롤보다 낫다
            // (rules/ui-layout.md: 고정 개수 행은 스크롤 대신 높이 축소).
            float need = chapterIds.Count * (rowH + gap);
            if (need > area.height)
            {
                rowH = Mathf.Max(34f, (area.height - gap * chapterIds.Count) / chapterIds.Count);
            }

            float y = area.y;
            for (int i = 0; i < chapterIds.Count; i++)
            {
                string chapter = chapterIds[i];
                bool selected = chapter == selectedChapter;
                bool untouched = tabUntouchedCache[i];

                GUI.backgroundColor = selected
                    ? UITheme.Instance.tabSelected
                    : (untouched ? UITheme.Instance.btnDisabled : UITheme.Instance.tabNormal);
                if (GUI.Button(new Rect(area.x, y, area.width, rowH),
                        tabLabelCache[i], tabStyle))
                {
                    selectedChapter = chapter;
                    scroll = Vector2.zero;
                    directScroll.Reset();
                }
                y += rowH + gap;
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawBeatList(Rect area)
        {
            if (string.IsNullOrEmpty(selectedChapter)
                || beatsByChapter == null
                || !beatsByChapter.TryGetValue(selectedChapter, out List<StoryBeat> list))
            {
                GUI.Label(area, "기록이 없다", hintStyle);
                return;
            }

            // 장 머리 — 목록 위. 아직 그 장에 들어서지 않았으면(여는 비트 미열람) 그리지 않고 목록이 영역을 다 쓴다.
            if (EnsureChapterHead(area))
            {
                DrawChapterHead();
                area = headPlan.List;
            }

            float rowH = RowHeight;
            float gap = RowGap;
            float contentH = list.Count * (rowH + gap);
            Rect view = new Rect(0f, 0f, area.width, contentH);
            directScroll.Handle(ref scroll, area, contentH, rowH * 0.5f);
            scroll = GUI.BeginScrollView(area, scroll, view, GUIStyle.none, GUIStyle.none);
            for (int i = 0; i < list.Count; i++)
            {
                DrawBeatRow(new Rect(0f, i * (rowH + gap), view.width, rowH), list[i]);
            }
            GUI.EndScrollView();

            UISurface.ScrollAffordance(area, scroll, contentH, UITheme.Instance.accentAmber);
        }

        private static float RowHeight => UIScale.IsMobileLayout ? 104f : 92f;
        private const float RowGap = 8f;

        /// <summary>
        /// 이 장의 머리(지난 이야기·이번 목표)를 보여도 되는가 — <b>스포일러 금지</b>. 장 데이터에 내용이 있고, 그 장을 여는 비트
        /// (<c>openingBeatId</c>)를 이미 봤을 때만. 여는 비트가 비어 있는 장은 그 장의 비트를 하나라도 봤을 때.
        /// </summary>
        private bool CanShowChapterHead(string chapterId, out StoryChapter chapter)
        {
            chapter = null;
            if (storyDirector == null || string.IsNullOrEmpty(chapterId)) return false;
            if (!StoryService.TryGetChapter(chapterId, out chapter) || chapter == null) return false;
            if (!StoryJournalHeadLayout.HasContent(chapter)) return false;
            return string.IsNullOrEmpty(chapter.openingBeatId)
                ? SeenIn(chapterId) > 0
                : storyDirector.HasSeen(chapter.openingBeatId);
        }

        /// <summary>장 머리 배치를 (장, 열람 수, 목록 영역)마다 한 번 굽는다. 그릴 게 있으면 true.</summary>
        private bool EnsureChapterHead(Rect area)
        {
            int seen = storyDirector != null ? storyDirector.SeenCount : 0;
            if (headChapterId == selectedChapter && headSeenCount == seen && headArea == area) return headVisible;
            headChapterId = selectedChapter;
            headSeenCount = seen;
            headArea = area;
            headVisible = CanShowChapterHead(selectedChapter, out StoryChapter chapter);
            if (!headVisible) return false;

            float textW = StoryJournalHeadLayout.TextWidth(area.width);
            List<string> lines = new List<string>();
            List<float> heights = new List<float>();
            if (chapter.recap != null)
            {
                for (int i = 0; i < chapter.recap.Count; i++)
                {
                    string line = chapter.recap[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string text = "• " + line.Trim();
                    lines.Add(text);
                    heights.Add(StoryJournalHeadLayout.BodyHeight(UIHelper.MeasureWrappedHeight(headBodyStyle, text, textW)));
                }
            }
            headLines = lines.ToArray();
            headGoal = string.IsNullOrWhiteSpace(chapter.goal) ? null : "이번 목표 · " + chapter.goal.Trim();
            float goalH = headGoal == null ? 0f
                : StoryJournalHeadLayout.BodyHeight(UIHelper.MeasureWrappedHeight(headGoalStyle, headGoal, textW));
            headPlan = StoryJournalHeadLayout.Layout(area, heights, goalH);
            return true;
        }

        private void DrawChapterHead()
        {
            UITheme t = UITheme.Instance;
            StoryJournalHeadLayout.Plan p = headPlan;
            UISurface.Card(p.Block, t.surfaceCard, t.surfaceBorder);
            if (p.Recap != null && p.Recap.Length > 0)
            {
                UIHelper.LabelFit(p.RecapHeader, "지난 이야기", headSectionStyle);
                for (int i = 0; i < p.Recap.Length && headLines != null && i < headLines.Length; i++)
                    UIHelper.LabelFit(p.Recap[i], headLines[i], headBodyStyle);
                if (p.HasGoal) UISurface.Flat(p.Divider, t.surfaceBorder);
            }
            if (p.HasGoal && headGoal != null) UIHelper.LabelFit(p.Goal, headGoal, headGoalStyle);
        }

        private void DrawBeatRow(Rect rect, StoryBeat beat)
        {
            if (beat == null) return;
            bool seen = storyDirector != null && storyDirector.HasSeen(beat.beatId);

            UISurface.Card(
                rect,
                seen ? new Color(0.12f, 0.14f, 0.20f, 0.95f) : new Color(0.08f, 0.09f, 0.12f, 0.85f),
                seen ? UITheme.Instance.surfaceBorder : new Color(0.18f, 0.19f, 0.24f, 0.8f));

            // 열람 여부 레일 — 5px라 각진 채로 두고 세로를 카드 반경만큼 물린다.
            UISurface.Flat(
                new Rect(rect.x + 3f, rect.y + 3f + UITheme.Radius.Card, 5f,
                    Mathf.Max(4f, rect.height - 6f - UITheme.Radius.Card * 2f)),
                seen ? UITheme.Instance.accentMint : new Color(0.3f, 0.31f, 0.36f));

            float textX = rect.x + StoryJournalRowLayout.TextLeft;

            if (seen)
            {
                // 영상 버튼은 본 비트에만 — 이 분기 밖(미열람)에는 아예 없다(스포일러 금지).
                bool hasVideo = HasReplayVideo(beat);
                float textW = StoryJournalRowLayout.TextWidth(rect, hasVideo);
                // 첫 줄을 제목처럼 쓴다 — 비트에 별도 제목 필드가 없고, 첫 대사가 늘 그 장면을 연다.
                string head = FirstLine(beat);
                rowTitleStyle.normal.textColor = Color.white;
                UIHelper.LabelFit(new Rect(textX, rect.y + 14f, textW, 34f), head, rowTitleStyle);
                UIHelper.LabelFit(new Rect(textX, rect.y + 50f, textW, 26f), RowMeta(beat), rowMetaStyle);

                // 「다시 읽기」는 영상 유무와 무관하게 늘 같은 자리(행 오른쪽 끝) — 행마다 버튼 열이 흔들리지 않게.
                GUI.backgroundColor = UITheme.Instance.btnPrimary;
                if (GUI.Button(StoryJournalRowLayout.ReplayButton(rect), "다시 읽기", tabStyle))
                {
                    ReplayBeat(beat);
                }
                if (hasVideo)
                {
                    GUI.backgroundColor = UITheme.Instance.accentCoral;
                    if (GUI.Button(StoryJournalRowLayout.VideoButton(rect), StoryJournalRowLayout.VideoLabel, tabStyle))
                    {
                        PlayBeatVideos(beat);
                    }
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                float textW = StoryJournalRowLayout.TextWidth(rect, false);
                UIHelper.LabelFit(new Rect(textX, rect.y + 14f, textW, 34f), "아직 지나지 않은 장면", lockStyle);
                UIHelper.LabelFit(new Rect(textX, rect.y + 50f, textW, 26f), HintFor(beat), rowMetaStyle);
            }
        }

        // ── 영상 다시보기 ──

        // 비트별 다시 볼 영상(이야기 순서) — 라이브러리 조회가 정의 객체를 새로 만들어서 행마다 매 패스 묻지 않는다.
        // 인덱스를 다시 구울 때(EnsureIndex) 함께 비운다.
        private readonly Dictionary<string, StoryJournalVideo.Pair> rowVideoCache = new Dictionary<string, StoryJournalVideo.Pair>();

        private StoryJournalVideo.Pair VideosOf(StoryBeat beat)
        {
            if (!rowVideoCache.TryGetValue(beat.beatId, out StoryJournalVideo.Pair pair))
            {
                pair = StoryJournalVideo.For(beat, IsKnownVideo);
                rowVideoCache[beat.beatId] = pair;
            }
            return pair;
        }

        private static bool IsKnownVideo(string videoId) => StoryVideoLibrary.TryGet(videoId, out _);

        private bool HasReplayVideo(StoryBeat beat) => videoDirector != null && VideosOf(beat).HasAny;

        private void PlayBeatVideos(StoryBeat beat)
        {
            if (videoDirector == null || beat == null) return;
            // 그리기 쪽과 같은 거름을 한 번 더 — 아직 안 본 비트의 영상은 열지 않는다(스포일러 금지).
            if (storyDirector == null || !storyDirector.HasSeen(beat.beatId)) return;
            StoryJournalVideo.Pair pair = VideosOf(beat);
            if (!pair.HasAny) return;

            directScroll.Reset();   // 누르던 손가락이 영상이 끝난 뒤 목록 드래그로 남지 않게
            queuedReplayVideoId = null;
            if (videoDirector.PlayReplay(pair.First))
            {
                queuedReplayVideoId = pair.Then;   // 뒤 영상은 앞 영상이 닫힌 다음 프레임에(Update)
                return;
            }
            // 앞 영상을 못 열면 뒤 영상이라도 — 둘 다 안 되면 머리줄에 잠깐 알린다.
            if (pair.Then == null || !videoDirector.PlayReplay(pair.Then)) ShowReplayNotice();
        }

        private void ShowReplayNotice()
        {
            replayNotice = "영상을 열 수 없어요. 잠시 뒤에 다시 눌러 주세요.";
            replayNoticeUntil = Time.unscaledTime + ReplayNoticeSeconds;
        }

        // ── 검수 캡처 전용 (StoryVideoVisualCapture) — 저장을 건드리지 않는다 ──

        /// <summary>이 장을 고르고 이 비트 행이 목록 위쪽에 보이게 연다.</summary>
        internal void OpenForCapture(string chapterId, string focusBeatId)
        {
            if (!isOpen) Toggle();
            EnsureIndex();
            if (beatsByChapter == null) return;
            if (!string.IsNullOrEmpty(chapterId) && beatsByChapter.ContainsKey(chapterId)) selectedChapter = chapterId;
            scroll = Vector2.zero;
            directScroll.Reset();
            if (string.IsNullOrEmpty(focusBeatId) || string.IsNullOrEmpty(selectedChapter)
                || !beatsByChapter.TryGetValue(selectedChapter, out List<StoryBeat> list)) return;
            int index = list.FindIndex(b => b != null && b.beatId == focusBeatId);
            // 한 행 위부터 보이게 — 그 위가 잠긴 행인지 본 행인지도 함께 찍힌다. 넘치는 값은 그리기의 Handle이 clamp한다.
            if (index > 0) scroll.y = (index - 1) * (RowHeight + RowGap);
        }

        /// <summary>「▶ 영상」을 누른 것과 같은 길. 영상이 시작됐으면 true.</summary>
        internal bool PressVideoForCapture(string beatId)
        {
            if (!StoryService.TryGetBeat(beatId, out StoryBeat beat) || beat == null) return false;
            PlayBeatVideos(beat);
            return videoDirector != null && videoDirector.IsPlaying;
        }

        /// <summary>이 행에 「▶ 영상」이 서는가(검수 README용).</summary>
        internal bool ShowsVideoButtonForCapture(string beatId)
        {
            if (!StoryService.TryGetBeat(beatId, out StoryBeat beat) || beat == null) return false;
            return storyDirector != null && storyDirector.HasSeen(beat.beatId) && HasReplayVideo(beat);
        }

        internal bool HasQueuedVideoForCapture => queuedReplayVideoId != null;

        // 행마다 OnGUI 패스마다 문자열을 만들면 스크롤뷰가 클립만 하고 루프는 전 행을 돌아 할당이 쌓인다.
        private readonly Dictionary<string, string> rowMetaCache = new Dictionary<string, string>();
        private string headerCache;
        private int headerSeen = -1;

        private string RowMeta(StoryBeat beat)
        {
            if (!rowMetaCache.TryGetValue(beat.beatId, out string meta))
            {
                meta = SpeakerOf(beat) + " · " + beat.lines.Count + "줄";
                rowMetaCache[beat.beatId] = meta;
            }
            return meta;
        }

        private static string FirstLine(StoryBeat beat)
        {
            if (beat.lines == null || beat.lines.Count == 0) return "(대사 없음)";
            StoryLine first = beat.lines[0];
            return first != null && !string.IsNullOrEmpty(first.text) ? first.text : "(대사 없음)";
        }

        private static string SpeakerOf(StoryBeat beat)
        {
            // 첫 **인물** 화자 — 지문으로 여는 장면이 목록에 "지문"이라는 사람으로 뜨지 않게 건너뛴다.
            if (beat.lines != null)
            {
                for (int i = 0; i < beat.lines.Count; i++)
                {
                    StoryLine line = beat.lines[i];
                    if (line != null && !string.IsNullOrEmpty(line.speaker)
                        && !StoryDialogueStaging.IsNarration(line.speaker))
                        return line.speaker;
                }
            }
            // 지문뿐인 비트(마을 징후 등)는 그 장면의 인물로 — ID("town_meadow")가 그대로 뜨지 않게 표시명을 쓴다.
            return string.IsNullOrEmpty(beat.speakerNpcId) ? "???"
                : InsectGame.NPC.NpcDialogueDatabase.StorySpeakerName(beat.speakerNpcId);
        }

        /// <summary>
        /// 미열람 비트의 힌트 — 대사는 숨기고 "어디서 열리는가"만 알린다.
        ///
        /// <b>case 라벨에 문자열 리터럴을 다시 적지 않는다.</b> <see cref="StoryDirector"/>의
        /// <c>const</c> 상수를 쓴다 — 사본을 두면 새 트리거 타입이 생겼을 때 여기가 조용히
        /// <c>default</c>로 흘러 "조건 미상"만 뜬다. 실제로 그렇게 어긋났다:
        /// <c>GuardianDefeat</c>·<c>DexProgress</c>가 뒤늦게 추가되면서 이 switch가 따라오지
        /// 않아 <b>82비트 중 9건</b>이 저널에서 아무 힌트도 주지 못했다
        /// (<c>StoryObjectiveResolver.KindOf</c>가 같은 이유로 상수를 쓴다).
        /// </summary>
        private static string HintFor(StoryBeat beat)
        {
            if (beat.trigger == null || string.IsNullOrEmpty(beat.trigger.type)) return "조건 미상";
            string param = beat.trigger.param ?? string.Empty;
            switch (beat.trigger.type)
            {
                case StoryDirector.TriggerRegionEnter: return "새 지역에 닿으면";
                case StoryDirector.TriggerSubAreaEnter: return "그 지역의 숨은 장소에서";
                case StoryDirector.TriggerCaptureInsect: return "곤충을 만나 기록하면";
                case StoryDirector.TriggerBattleWin: return "그곳에서 전투를 이기면";
                case StoryDirector.TriggerNpcTalk: return "동행자에게 말을 걸면";
                case StoryDirector.TriggerQuestComplete: return "퀘스트를 마치면";
                case StoryDirector.TriggerLevelReach:
                    return string.IsNullOrEmpty(param) ? "레벨이 오르면" : $"Lv.{param}에 닿으면";
                case StoryDirector.TriggerImmediate: return "여행을 시작하면";
                case StoryDirector.TriggerGuardianDefeat: return "그 지역의 수문장을 넘으면";
                case StoryDirector.TriggerDuelWin: return "명부회 간부와의 대결에서 이기면";
                case StoryDirector.TriggerDexProgress:
                    return string.IsNullOrEmpty(param) ? "도감이 채워지면" : $"도감에 {param}종을 새기면";
                default: return "조건 미상";
            }
        }

        private void ReplayBeat(StoryBeat beat)
        {
            if (dialogueUI == null || beat == null) return;
            // 열람 여부는 여기서 한 번 더 거른다 — ShowStoryReplay는 seen 마킹을 하지 않으므로
            // 미열람 비트를 넘기면 대사만 소비되고 나중에 정상 트리거로 또 뜬다.
            if (storyDirector == null || !storyDirector.HasSeen(beat.beatId)) return;
            dialogueUI.ShowStoryReplay(beat);
        }

        // ── 스타일 ──

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            titleStyle = Label(36, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            closeStyle = new GUIStyle(GUI.skin.button) { fontSize = 30, fontStyle = FontStyle.Bold };
            tabStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
            rowTitleStyle = Label(24, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            rowMetaStyle = Label(19, FontStyle.Normal, TextAnchor.MiddleLeft, UITheme.Instance.textSecondary);
            hintStyle = Label(20, FontStyle.Normal, TextAnchor.MiddleLeft, UITheme.Instance.textMuted);
            lockStyle = Label(23, FontStyle.Bold, TextAnchor.MiddleLeft, UITheme.Instance.textMuted);
            noticeStyle = Label(20, FontStyle.Bold, TextAnchor.MiddleLeft, UITheme.Instance.accentCoral);
            headSectionStyle = Label(StoryJournalHeadLayout.SectionFont, FontStyle.Bold, TextAnchor.MiddleLeft,
                UITheme.Instance.textSecondary);
            headBodyStyle = Label(StoryJournalHeadLayout.BodyFont, FontStyle.Normal, TextAnchor.UpperLeft, UITheme.Instance.textPrimary);
            headBodyStyle.wordWrap = true;
            headGoalStyle = Label(StoryJournalHeadLayout.BodyFont, FontStyle.Bold, TextAnchor.UpperLeft, UITheme.Instance.accentMint);
            headGoalStyle.wordWrap = true;
        }

        private static GUIStyle Label(int size, FontStyle fs, TextAnchor anchor, Color col)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, alignment = anchor, wordWrap = false };
            s.normal.textColor = col;
            return s;
        }
    }

    /// <summary>
    /// 저널 비트 행의 버튼 자리 — <b>순수 계산</b>. 「다시 읽기」는 늘 행 오른쪽 끝(영상 유무와 무관하게 같은 열),
    /// 「▶ 영상」은 그 왼쪽. 둘 다 터치 높이 하한(<see cref="UIScale.MinTouchHeight"/>) 이상이고 행 안에서 세로 가운데다.
    /// 글자 폭은 가장 왼쪽 버튼 앞에서 끊는다 — 패널이 좁아져도 글자가 버튼 밑으로 들어가지 않게(넘치면 LabelFit이 줄인다).
    /// </summary>
    public static class StoryJournalRowLayout
    {
        public const string VideoLabel = "▶ 영상";
        public const float ReplayWidth = 150f;
        public const float VideoWidth = 128f;
        public const float ButtonGap = 10f;
        /// <summary>행 오른쪽 끝과 「다시 읽기」 사이.</summary>
        public const float EdgePad = 18f;
        /// <summary>행 왼쪽 끝(열람 레일 포함)과 글자 사이.</summary>
        public const float TextLeft = 22f;
        /// <summary>글자와 가장 왼쪽 버튼 사이.</summary>
        public const float TextGap = 10f;
        public const float MinTextWidth = 120f;

        public static float ButtonHeight => Mathf.Max(UIScale.MinTouchHeight, 46f);

        public static Rect ReplayButton(Rect row)
        {
            float h = ButtonHeight;
            return new Rect(row.xMax - EdgePad - ReplayWidth, row.y + (row.height - h) * 0.5f, ReplayWidth, h);
        }

        public static Rect VideoButton(Rect row)
        {
            Rect replay = ReplayButton(row);
            return new Rect(replay.x - ButtonGap - VideoWidth, replay.y, VideoWidth, replay.height);
        }

        public static float TextWidth(Rect row, bool hasVideo)
        {
            float right = hasVideo ? VideoButton(row).x : ReplayButton(row).x;
            return Mathf.Max(MinTextWidth, right - TextGap - (row.x + TextLeft));
        }
    }

    /// <summary>
    /// 저널에서 다시 볼 영상 — <b>순수 계산</b>. 한 비트에 대사 앞 영상(<c>introVideoId</c>)과 뒤 영상(<c>videoId</c>)이
    /// 함께 있으면 이야기 순서(앞 → 뒤)로 둘 다, 하나면 그것 하나. 라이브러리가 모르는 ID는 뺀다(버튼을 눌러도 안 나오는 버튼을 세우지 않는다).
    /// 「둘 다」는 지금 데이터에선 없다 — story_lint 검사 13이 한 비트의 두 영상을 금지한다(규칙이 바뀔 때를 위한 방어).
    /// <b>본 비트인지는 부르는 쪽이 거른다</b> — 저널은 본 행에서만 이 값을 묻는다.
    /// </summary>
    public static class StoryJournalVideo
    {
        public readonly struct Pair
        {
            /// <summary>먼저 틀 영상. 없으면 null.</summary>
            public readonly string First;
            /// <summary>앞 영상이 닫힌 뒤 이어 틀 영상. 없으면 null.</summary>
            public readonly string Then;

            public Pair(string first, string then)
            {
                First = first;
                Then = then;
            }

            public bool HasAny => First != null;
        }

        public static Pair For(StoryBeat beat, System.Func<string, bool> isKnown)
        {
            if (beat == null) return default;
            string intro = Usable(beat.introVideoId, isKnown);
            string outro = Usable(beat.videoId, isKnown);
            if (intro != null && outro != null && intro != outro) return new Pair(intro, outro);
            return new Pair(intro ?? outro, null);
        }

        private static string Usable(string videoId, System.Func<string, bool> isKnown)
        {
            if (string.IsNullOrWhiteSpace(videoId)) return null;
            return isKnown == null || isKnown(videoId) ? videoId : null;
        }
    }

    /// <summary>
    /// 저널 장 머리(지난 이야기·이번 목표)의 자리 — <b>순수 계산</b>. 비트 목록 영역(<paramref name="area"/>) 맨 위에 블록을 놓고 목록은 그 아래를 쓴다.
    /// 블록은 목록 영역 높이의 <see cref="MaxShare"/>까지만 — 넘치면 본문 줄을 같은 비율로 줄인다(그리기의 LabelFit이 글자를 줄인다).
    /// 목록이 한 줄도 안 보이게 되는 일이 없다.
    /// </summary>
    public static class StoryJournalHeadLayout
    {
        public const int SectionFont = 20;
        public const int BodyFont = 22;
        public const float Pad = 14f;
        /// <summary>블록이 차지할 수 있는 목록 영역 높이의 몫.</summary>
        public const float MaxShare = 0.5f;
        public const float DividerHeight = 2f;

        public static float LineHeight(int fontSize) => Mathf.Ceil(fontSize * 1.35f);
        public static float TextWidth(float areaWidth) => Mathf.Max(1f, areaWidth - Pad * 2f);
        public static float BodyHeight(float measured) => Mathf.Max(LineHeight(BodyFont), Mathf.Ceil(measured));

        /// <summary>그릴 내용이 있는가 — 지난 이야기에 비지 않은 줄이 있거나 목표가 있다.</summary>
        public static bool HasContent(StoryChapter chapter)
        {
            if (chapter == null) return false;
            if (!string.IsNullOrWhiteSpace(chapter.goal)) return true;
            if (chapter.recap == null) return false;
            for (int i = 0; i < chapter.recap.Count; i++)
                if (!string.IsNullOrWhiteSpace(chapter.recap[i])) return true;
            return false;
        }

        public struct Plan
        {
            public Rect Block;
            public Rect RecapHeader;
            public Rect[] Recap;
            public Rect Divider;
            public Rect Goal;
            public bool HasGoal;
            /// <summary>블록 아래 남은 목록 영역.</summary>
            public Rect List;
            public bool Squeezed;
        }

        public static float FixedHeight(int recapCount, bool hasGoal)
        {
            float h = Pad;
            if (recapCount > 0)
                h += LineHeight(SectionFont) + UITheme.Space.XS + UITheme.Space.XS * (recapCount - 1);
            if (hasGoal && recapCount > 0) h += UITheme.Space.S + DividerHeight + UITheme.Space.S;
            return h + Pad;
        }

        public static Plan Layout(Rect area, IReadOnlyList<float> recapHeights, float goalHeight)
        {
            int n = recapHeights != null ? recapHeights.Count : 0;
            bool hasGoal = goalHeight > 0f;
            var p = new Plan { HasGoal = hasGoal, Recap = new Rect[n], List = area };
            if (n == 0 && !hasGoal) return p;

            float fixedH = FixedHeight(n, hasGoal);
            float body = hasGoal ? goalHeight : 0f;
            for (int i = 0; i < n; i++) body += Mathf.Max(0f, recapHeights[i]);
            float cap = Mathf.Max(1f, area.height * MaxShare);
            float k = 1f;
            if (fixedH + body > cap && body > 0f) k = Mathf.Clamp01((cap - fixedH) / body);
            float blockH = Mathf.Min(cap, fixedH + body * k);
            p.Squeezed = k < 1f;
            p.Block = new Rect(area.x, area.y, area.width, blockH);

            float x = area.x + Pad;
            float tw = TextWidth(area.width);
            float y = area.y + Pad;
            if (n > 0)
            {
                p.RecapHeader = new Rect(x, y, tw, LineHeight(SectionFont));
                y += p.RecapHeader.height + UITheme.Space.XS;
                for (int i = 0; i < n; i++)
                {
                    float h = Mathf.Max(1f, Mathf.Max(0f, recapHeights[i]) * k);
                    p.Recap[i] = new Rect(x, y, tw, h);
                    y += h + (i < n - 1 ? UITheme.Space.XS : 0f);
                }
                if (hasGoal)
                {
                    y += UITheme.Space.S;
                    p.Divider = new Rect(x, y, tw, DividerHeight);
                    y += DividerHeight + UITheme.Space.S;
                }
            }
            if (hasGoal) p.Goal = new Rect(x, y, tw, Mathf.Max(1f, goalHeight * k));

            float listTop = p.Block.yMax + UITheme.Space.S;
            p.List = new Rect(area.x, listTop, area.width, Mathf.Max(1f, area.yMax - listTop));
            return p;
        }
    }
}
