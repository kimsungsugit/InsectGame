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
    /// </summary>
    public class StoryJournalUI : MonoBehaviour, IModalUI
    {
        private StoryDirector storyDirector;
        private NpcDialogueUI dialogueUI;

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
        private GUIStyle titleStyle, closeStyle, tabStyle, rowTitleStyle, rowMetaStyle, hintStyle, lockStyle;

        /// <summary>
        /// 챕터 탭의 표시 순서와 이름. <b>배열이지 Dictionary가 아니다</b> —
        /// <c>Dictionary</c>는 열거 순서를 보장하지 않아서, 탭 순서를 거기 맡기면 챕터가
        /// 뒤섞여 뜰 수 있다(스토리 엔진이 <c>StoryService.AllBeats()</c>의 Dictionary 순서를
        /// 못 믿어 prereq로 엮는 것과 같은 이유다). 순서가 의미를 가지면 배열로 적는다.
        ///
        /// Story.json의 chapterId는 현재 ch1…ch12/fin/side/npc다. 여기 없는 chapterId는
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
            ("side", "곁이야기"),
            ("npc", "동행자와의 대화"),
        };

        public void AutoWire(StoryDirector director, NpcDialogueUI dialogue)
        {
            if (storyDirector == null) storyDirector = director;
            if (dialogueUI == null) dialogueUI = dialogue;
        }

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
            if (isOpen) ModalUIRegistry.Register(this);
            else ModalUIRegistry.Unregister(this);
        }

        public void CloseModal()
        {
            isOpen = false;
            directScroll.Reset();
            ModalUIRegistry.Unregister(this);
        }

        private void OnDisable()
        {
            // isOpen을 남겨 두면 레지스트리엔 없는데 열린 것으로 아는 상태가 된다
            // (ESC가 이 모달을 건너뛰어 영구히 안 닫힘). NpcDialogueUI가 같은 이유로 이렇게 한다.
            isOpen = false;
            directScroll.Reset();
            ModalUIRegistry.Unregister(this);
        }

        // ── 인덱스 ──

        private void EnsureIndex()
        {
            if (beatsByChapter != null) return;

            beatsByChapter = new Dictionary<string, List<StoryBeat>>();
            totalBeats = 0;
            foreach (StoryBeat beat in StoryService.AllBeats())
            {
                if (beat == null || string.IsNullOrEmpty(beat.beatId)) continue;
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
            GUI.Label(new Rect(px + 26f, py + 14f, pw - 220f, 50f), "여행의 기록", titleStyle);
            GUI.Label(new Rect(px + 26f, py + 58f, pw - 220f, 28f),
                $"{seen} / {totalBeats} 장면을 지나왔다", hintStyle);
            if (GUI.Button(new Rect(px + pw - 74f, py + 14f, 58f, 58f), "X", closeStyle)) { CloseModal(); return; }

            float bodyY = py + 96f;
            float bodyH = ph - (bodyY - py) - 20f;

            // 좌: 챕터 탭 / 우: 비트 목록
            float tabW = UIScale.IsMobileLayout ? 220f : 260f;
            DrawChapterTabs(new Rect(px + 20f, bodyY, tabW, bodyH));
            DrawBeatList(new Rect(px + 20f + tabW + 16f, bodyY, pw - 40f - tabW - 16f, bodyH));
        }

        private void DrawChapterTabs(Rect area)
        {
            if (chapterIds == null) return;
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
                int done = SeenIn(chapter);
                int total = beatsByChapter[chapter].Count;
                bool selected = chapter == selectedChapter;
                bool untouched = done == 0;

                GUI.backgroundColor = selected
                    ? UITheme.Instance.tabSelected
                    : (untouched ? UITheme.Instance.btnDisabled : UITheme.Instance.tabNormal);
                if (GUI.Button(new Rect(area.x, y, area.width, rowH),
                        $"{ChapterLabel(chapter)}  {done}/{total}", tabStyle))
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

            float rowH = UIScale.IsMobileLayout ? 104f : 92f;
            float gap = 8f;
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

            float textX = rect.x + 22f;
            float textW = rect.width - 200f;

            if (seen)
            {
                // 첫 줄을 제목처럼 쓴다 — 비트에 별도 제목 필드가 없고, 첫 대사가 늘 그 장면을 연다.
                string head = FirstLine(beat);
                rowTitleStyle.normal.textColor = Color.white;
                UIHelper.LabelFit(new Rect(textX, rect.y + 14f, textW, 34f), head, rowTitleStyle);
                UIHelper.LabelFit(new Rect(textX, rect.y + 50f, textW, 26f),
                    $"{SpeakerOf(beat)} · {beat.lines.Count}줄", rowMetaStyle);

                float bw = 150f;
                float bh = Mathf.Max(UIScale.MinTouchHeight, 46f);
                GUI.backgroundColor = UITheme.Instance.btnPrimary;
                if (GUI.Button(new Rect(rect.x + rect.width - bw - 18f, rect.y + (rect.height - bh) * 0.5f, bw, bh),
                        "다시 읽기", tabStyle))
                {
                    ReplayBeat(beat);
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                UIHelper.LabelFit(new Rect(textX, rect.y + 14f, textW, 34f), "아직 지나지 않은 장면", lockStyle);
                UIHelper.LabelFit(new Rect(textX, rect.y + 50f, textW, 26f), HintFor(beat), rowMetaStyle);
            }
        }

        private static string FirstLine(StoryBeat beat)
        {
            if (beat.lines == null || beat.lines.Count == 0) return "(대사 없음)";
            StoryLine first = beat.lines[0];
            return first != null && !string.IsNullOrEmpty(first.text) ? first.text : "(대사 없음)";
        }

        private static string SpeakerOf(StoryBeat beat)
        {
            if (beat.lines != null && beat.lines.Count > 0
                && beat.lines[0] != null && !string.IsNullOrEmpty(beat.lines[0].speaker))
            {
                return beat.lines[0].speaker;
            }
            return string.IsNullOrEmpty(beat.speakerNpcId) ? "???" : beat.speakerNpcId;
        }

        /// <summary>미열람 비트의 힌트 — 대사는 숨기고 "어디서 열리는가"만 알린다.</summary>
        private static string HintFor(StoryBeat beat)
        {
            if (beat.trigger == null || string.IsNullOrEmpty(beat.trigger.type)) return "조건 미상";
            string param = beat.trigger.param ?? string.Empty;
            switch (beat.trigger.type)
            {
                case "RegionEnter": return "새 지역에 닿으면";
                case "SubAreaEnter": return "그 지역의 숨은 장소에서";
                case "CaptureInsect": return "곤충을 만나 기록하면";
                case "BattleWin": return "그곳에서 전투를 이기면";
                case "NpcTalk": return "동행자에게 말을 걸면";
                case "QuestComplete": return "퀘스트를 마치면";
                case "LevelReach": return string.IsNullOrEmpty(param) ? "레벨이 오르면" : $"Lv.{param}에 닿으면";
                case "Immediate": return "여행을 시작하면";
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
        }

        private static GUIStyle Label(int size, FontStyle fs, TextAnchor anchor, Color col)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, alignment = anchor, wordWrap = false };
            s.normal.textColor = col;
            return s;
        }
    }
}
