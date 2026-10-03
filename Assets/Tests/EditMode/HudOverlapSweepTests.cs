#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InsectGame.Capture;
using InsectGame.Core;
using InsectGame.Story;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 필드·동굴·나의 섬·남의 섬·「챔피언의 꿈」 섬 위에 <b>함께 뜰 수 있는 모든 IMGUI 요소의 모든 쌍</b>이 겹치지 않는지 —
    /// 화면 18장(데스크톱 6, 모바일 세로 2·가로 2 × 배율 0.667/1/1.333)에서 잰다.
    ///
    /// 자리는 전부 <b>그 화면 파일의 순수 배치 함수</b>를 부른다(식을 여기서 다시 세우지 않는다) — 배치를 고치면 이 검사가 따라온다.
    /// 함께 뜰 수 없는 쌍만 <see cref="Exclusion"/>이 뺀다. 뺄 때는 그 근거가 된 코드(숨김 조건)를 적는다 —
    /// 근거가 사라지면(숨김 조건을 지우면) 그 행도 지울 것.
    ///
    /// 2026-10-03 전수 검사 전엔 같은 화면들에서 204쌍이 겹쳤다(포획 결과 카드·퀘스트 완료·소식 카드·동굴 출입 알림이 화면 위 가운데와
    /// 가운데 줄에 저마다 자리를 잡았고, 필드 멀티 판은 픽셀 좌표로 우상단을 덮었다). 잠깐 뜨는 카드는 이제 가운데 무대(<see cref="HudStage"/>)
    /// 한 자리에 차례로 선다. 무대는 섬 HUD가 서 있을 때만 그것을 피하므로(<see cref="HudFrame.IslandHud"/>) 무대에 서는 것은 섬과 그 밖에서
    /// 따로 잰다 — 한때 필드에서도 섬 열을 피해 세로 화면의 카드가 왼쪽 절반으로 밀렸다.
    /// </summary>
    [TestFixture]
    public class HudOverlapSweepTests
    {
        [Flags]
        private enum Ctx
        {
            None = 0,
            Field = 1,          // 필드(본 마을·리전)
            Cave = 2,           // 동굴·숨겨진 장소(서브에리어)
            IslandOwn = 4,      // 나의 섬
            IslandVisit = 8,    // 남의 섬 구경
            Dream = 16,         // 「챔피언의 꿈」의 섬 걷기
            Island = IslandOwn | IslandVisit,
            Play = Field | Cave | IslandOwn | IslandVisit,
        }

        private enum StageKind { None, Queued, Fixed }

        // 쌍 제외 규칙이 알아보는 요소.
        private enum Kind
        {
            Other, StatusTab, StatusPanel, Minimap, Quest, Coach, IslandGuide, CatchButton, CatchRing,
            JoystickZone, JoystickHint, Talk, Prompt, Nearby, Gate, YieldsToStatusPanel,
        }

        private sealed class El
        {
            public string Name;
            public Rect Rect;
            public Ctx Ctx;
            public Kind Kind;
            public StageKind Stage;
            /// <summary>무대에 선 카드와 겹치면 그리지 않는다(<see cref="HudStage.OccupiedOver"/>).</summary>
            public bool YieldsToStage;
            /// <summary>조작이 묶였을 때(IsFrozen)만 뜬다.</summary>
            public bool FrozenOnly;
            /// <summary>조작이 묶이면 숨는다.</summary>
            public bool HiddenWhenFrozen;
            /// <summary>같은 묶음의 다른 변형과는 동시에 뜨지 않는다(한 번에 하나).</summary>
            public string Group;
            public int Variant;
            /// <summary>무대 항목 — 섬 HUD가 서 있는 판(<see cref="HudStage.Area(HudFrame, bool)"/>의 true)으로 잰 자리.</summary>
            public bool IslandHud;
        }

        // ── 화면 ──

        private static IEnumerable<TestCaseData> Screens()
        {
            yield return Screen("desktop 1280x720", 1280f, 720f, 0f, 0f, 0f, 0f, false);
            yield return Screen("desktop 1366x768", 1366f, 768f, 0f, 0f, 0f, 0f, false);
            yield return Screen("desktop 1920x1080", 1920f, 1080f, 0f, 0f, 0f, 0f, false);
            yield return Screen("desktop 1920x1200", 1920f, 1200f, 0f, 0f, 0f, 0f, false);
            yield return Screen("desktop 2560x1080", 2560f, 1080f, 0f, 0f, 0f, 0f, false);
            yield return Screen("desktop 2560x1440", 2560f, 1440f, 0f, 0f, 0f, 0f, false);
            foreach (float s in new[] { 2f / 3f, 1f, 4f / 3f })
            {
                string k = s.ToString("0.###");
                yield return Screen($"portrait 16x9 s{k}", 1080f * s, 1920f * s, 0f, 0f, 0f, 0f, true);
                yield return Screen($"portrait 20x9 notch s{k}", 1080f * s, 2400f * s, 0f, 0f, 100f * s, 40f * s, true);
                yield return Screen($"landscape 16x9 s{k}", 1920f * s, 1080f * s, 0f, 0f, 0f, 0f, true);
                yield return Screen($"landscape 20x9 notch s{k}", 2400f * s, 1080f * s, 100f * s, 100f * s, 0f, 30f * s, true);
            }
        }

        private static IEnumerable<TestCaseData> MobileScreens() => Screens().Where(c => (bool)c.Arguments[7]);

        private static TestCaseData Screen(string name, float pw, float ph, float safeLeft, float safeRight, float safeTop,
            float safeBottom, bool mobilePlatform)
            => new TestCaseData(name, pw, ph, safeLeft, safeRight, safeTop, safeBottom, mobilePlatform);

        private static HudFrame Frame(float pw, float ph, float safeLeft, float safeRight, float safeTop, float safeBottom, bool mobile)
            => HudFrame.ForScreen(pw, ph, safeLeft, safeRight, safeTop, safeBottom, mobile);

        // ── 요소 — 전부 화면 파일의 순수 배치 함수 ──

        private static List<El> Elements(HudFrame f)
        {
            var e = new List<El>();
            // 가운데 무대는 섬 HUD가 서 있을 때만 섬 HUD를 피한다(HudFrame.IslandHud) — 무대에 기대는 자리는 섬과 그 밖에서 따로 잰다.
            HudFrame onField = f.WithIslandHud(false);
            HudFrame onIsland = f.WithIslandHud(true);
            void Add(string name, Rect rect, Ctx ctx, Kind kind = Kind.Other, bool frozenHidden = false, bool yields = false,
                string group = null, int variant = 0)
            {
                e.Add(new El
                {
                    Name = name, Rect = rect, Ctx = ctx, Kind = kind, HiddenWhenFrozen = frozenHidden, YieldsToStage = yields,
                    Group = group, Variant = variant,
                });
            }

            // 왼쪽 위 — 상태 탭·패널(PlayerStatusHUD), 미니맵(MinimapUI).
            Add("상태 탭", PlayerStatusHUD.TabRect(f), Ctx.Play, Kind.StatusTab);
            Add("상태 패널(펼침)", PlayerStatusHUD.PanelRect(f), Ctx.Play, Kind.StatusPanel);
            Add("미니맵", MinimapUI.PanelRect(f), Ctx.Play, Kind.Minimap);

            // 단축 바(QuickAccessBarUI — IsInputBlocked: 조작이 묶이면 숨는다).
            Add("단축 바", QuickAccessBarUI.ShortcutBarRectFor(f), Ctx.Play, frozenHidden: true);

            // 퀘스트 칩·목표 행·복원 버튼(TutorialQuestUI) — 한 번에 한 모양.
            float rowH = QuestChipLayout.RowHeight;
            var chips = new (string name, float height, bool row)[]
            {
                ("펼친 칩", QuestChipLayout.ExpandedHeight, true), ("펼친 칩·행 없음", QuestChipLayout.ExpandedHeight, false),
                ("완료 칩", QuestChipLayout.DoneHeight, true), ("완료 칩·행 없음", QuestChipLayout.DoneHeight, false),
            };
            for (int i = 0; i < chips.Length; i++)
            {
                Rect chip = QuestChipLayout.ChipRect(f, chips[i].height, chips[i].row ? rowH : 0f);
                Add("퀘스트 " + chips[i].name, chip, Ctx.Play, Kind.Quest, group: "퀘스트", variant: i);
                if (chips[i].row)
                    Add("목표 행(" + chips[i].name + ")", QuestChipLayout.Row(chip, QuestChipLayout.StackWidth(f), rowH), Ctx.Play,
                        Kind.Quest, group: "퀘스트", variant: i);
            }
            Rect restore = QuestChipLayout.RestoreRect(f, rowH);
            Add("퀘스트 복원 버튼", restore, Ctx.Play, Kind.Quest, group: "퀘스트", variant: 10);
            Add("목표 행(복원 버튼)", QuestChipLayout.Row(restore, QuestChipLayout.StackWidth(f), rowH), Ctx.Play, Kind.Quest,
                group: "퀘스트", variant: 10);
            Add("퀘스트 복원 버튼·행 없음", QuestChipLayout.RestoreRect(f, 0f), Ctx.Play, Kind.Quest, group: "퀘스트", variant: 11);

            // 위 가운데 — 리전 배너(KeyGuideHUD)·내기 점수판(FieldMomentsUI — Hidden: 조작이 묶이면 숨는다).
            Add("리전 배너", KeyGuideHUD.RegionBannerRect(f), Ctx.Play, Kind.YieldsToStatusPanel);
            Add("내기 점수판", FieldMomentsUI.RaceChipRect(f), Ctx.Play, Kind.YieldsToStatusPanel, frozenHidden: true);
            if (!f.Mobile) Add("포획 아이템 패널", KeyGuideHUD.CaptureItemsRectFor(f), Ctx.Play);

            // 시각·날씨 칩과 변화 알림(WorldClockHUD — 조작이 묶이면 숨는다. 동굴엔 알림이 없다(Indoors)).
            // 세로 모바일 섬은 섬 HUD 열 아래로 옮긴다 — 아래 섬 쪽에서 따로 넣는다.
            Ctx clockOnIsland = f.Mobile && f.Portrait ? Ctx.None : Ctx.Island;
            Rect fieldChip = WorldClockRules.FieldChip(f);
            Add("시각 칩", fieldChip, Ctx.Field | Ctx.Cave | clockOnIsland, frozenHidden: true);
            Add("변화 알림", WorldClockRules.NoticeBelow(f, fieldChip), Ctx.Field | clockOnIsland, frozenHidden: true);

            // 안내 배너 — 첫 가이드 코치(GuidedTutorialController)·섬 안내(IslandGuideUI, 내 섬). 둘 다 무대에 카드가 서면 비켜선다.
            Add("코치 배너", GuidedTutorialController.CoachRect(f), Ctx.Play, Kind.Coach, yields: true);
            Add("섬 안내 배너", IslandGuideUI.CoachRect(f, false), Ctx.IslandOwn, Kind.IslandGuide, yields: true);

            // 섬 HUD(IslandHudUI — 조작이 묶이면 숨는다).
            Add("섬 HUD 판", IslandHudLayout.OwnPanel(f), Ctx.IslandOwn, frozenHidden: true);
            Add("섬 HUD 판(구경)", IslandHudLayout.VisitPanel(f), Ctx.IslandVisit, frozenHidden: true);
            if (!f.Mobile) Add("섬 정보 줄", IslandHudLayout.DesktopInfo(IslandHudLayout.OwnPanel(f)), Ctx.IslandOwn, frozenHidden: true);
            if (f.Mobile && f.Portrait)
            {
                foreach (bool visiting in new[] { false, true })
                {
                    Ctx ctx = visiting ? Ctx.IslandVisit : Ctx.IslandOwn;
                    string tag = visiting ? "(구경)" : "";
                    Rect chip = WorldClockRules.Chip(f, true, visiting);
                    Add("섬 시각 칩" + tag, chip, ctx, frozenHidden: true);
                    Add("섬 변화 알림" + tag, WorldClockRules.NoticeBelow(f, chip), ctx, frozenHidden: true);
                }
            }

            // 잡기 버튼·고리·글자(CaptureInputController — anyUI: 조작이 묶이면 숨는다. 꿈 섬에서도 뜬다). 글자는 한 번에 하나(feedbackMessage).
            Ctx catchCtx = Ctx.Play | Ctx.Dream;
            Add("잡기 버튼", CatchButtonLayout.ButtonRect(f), catchCtx, Kind.CatchButton, frozenHidden: true);
            Add("잡기 고리(최대)", CatchButtonLayout.SwingRect(f), catchCtx, Kind.CatchRing, frozenHidden: true);
            Add("잡기 놓침 글자", CatchButtonLayout.FeedbackRect(f, true), catchCtx, frozenHidden: true, group: "잡기 글자", variant: 0);
            Add("잡기 안내 글자", CatchButtonLayout.FeedbackRect(f, false), catchCtx, frozenHidden: true, group: "잡기 글자", variant: 1);
            Add("습격 경고 글자", CatchButtonLayout.WarnRect(f), catchCtx, frozenHidden: true, group: "잡기 글자", variant: 2);

            // 마을 상호작용(WorldInteractionController) — 대화 버튼·안내 글자는 무대에 카드가 서면 비켜선다. 마을 건물·주민은 본 월드에만 있다.
            Add("대화 버튼", WorldInteractionController.TalkRect(f), Ctx.Field, Kind.Talk, yields: true);
            Add("상호작용 안내 글자", WorldInteractionController.PromptRect(f), Ctx.Field, Kind.Prompt, yields: true);
            Add("상호작용 버튼", WorldInteractionController.InteractButtonRect(f), Ctx.Field);

            // 동굴 입구·나가기(SubAreaWorldBuilder — IsSubAreaActionBlocked: 조작이 묶이면 숨는다. 섬은 걸러진다(detached)).
            Add("동굴 입구 버튼", SubAreaWorldBuilder.GateRect(f), Ctx.Field, Kind.Gate, frozenHidden: true);
            Add("동굴 나가기 버튼", SubAreaWorldBuilder.GateRect(f), Ctx.Cave, Kind.Gate, frozenHidden: true);

            // 설정 버튼(AccountSettingsUI — 꿈에서는 숨는다).
            Add("설정 버튼", AccountSettingsUI.OpenButtonRect(f), Ctx.Play);

            // 이동 잠금 안내(PlayerHintOverlay — 조작이 묶였을 때만, 꿈에서는 숨는다).
            e.Add(new El { Name = "이동 잠금 글자", Rect = PlayerHintOverlay.FrozenRect(f), Ctx = Ctx.Play, FrozenOnly = true });

            // 가상 조이스틱(VirtualJoystickUI — 모바일 배치만, 조작이 묶이면 꺼진다).
            if (VirtualJoystickUI.EnabledFor(f))
            {
                Add("조이스틱 자리", VirtualJoystickUI.ZoneRect(f), Ctx.Play | Ctx.Dream, Kind.JoystickZone, frozenHidden: true);
                Add("조이스틱 안내 원", VirtualJoystickUI.HintRect(f), Ctx.Play | Ctx.Dream, Kind.JoystickHint, frozenHidden: true);
            }

            // 필드 멀티(WorldFieldMultiplayerUI.FieldVisible — 섬·꿈·창·조작 잠금에서 물러난다).
            Ctx net = Ctx.Field | Ctx.Cave;
            Add("멀티 필드 상태", WorldFieldMultiplayerUI.StatusRect(f), net, frozenHidden: true);
            Add("멀티 대화 기록", WorldFieldMultiplayerUI.MessagesRect(onField), net, Kind.YieldsToStatusPanel, frozenHidden: true,
                yields: f.Mobile && f.Portrait);   // 세로는 무대 윗변에 맞춘다 — 섬에서는 뜨지 않으니 필드 무대
            Add("멀티 근처 탐험가", WorldFieldMultiplayerUI.NearbyRect(f), net, Kind.Nearby, frozenHidden: true, yields: true);

            // 가운데 무대(HudStage) — 차례 항목은 한 번에 하나, 고정 항목은 정해진 칸. 섬(내 섬·남의 섬)에서는 섬 HUD가 서 있는 판으로,
            // 그 밖(필드·동굴)에서는 섬 HUD가 없는 판으로 잰다 — 섬 쪽 이름에는 "(섬)"을 붙인다(한 장소만이면 붙이지 않는다).
            void OnStage(string name, Func<HudFrame, Rect> place, Ctx ctx, StageKind kind)
            {
                Ctx outside = ctx & ~Ctx.Island;
                Ctx inside = ctx & Ctx.Island;
                if (outside != Ctx.None)
                    e.Add(new El { Name = name, Rect = place(onField), Ctx = outside, Stage = kind });
                if (inside != Ctx.None)
                    e.Add(new El
                    {
                        Name = outside != Ctx.None ? name + "(섬)" : name, Rect = place(onIsland), Ctx = inside, Stage = kind,
                        IslandHud = true,
                    });
            }
            void Queued(string name, Func<HudFrame, Rect> place, Ctx ctx) => OnStage(name, place, ctx, StageKind.Queued);
            void Fixed(string name, Func<HudFrame, Rect> place, Ctx ctx) => OnStage(name, place, ctx, StageKind.Fixed);
            Queued("포획 성공 카드", CapturePopupUI.SuccessRect, Ctx.Play);
            Queued("포획 실패 카드", CapturePopupUI.FailRect, Ctx.Play);
            Queued("퀘스트 완료 알림", x => TutorialQuestUI.QuestDoneRect(x, true), Ctx.Play);
            Queued("다음 퀘스트 알림", x => TutorialQuestUI.QuestNextRect(x, true), Ctx.Play);
            Queued("소식 카드", FieldMomentsUI.ToastFootprint, Ctx.Play);
            Queued("섬 결과 토스트", IslandHudUI.ToastRect, Ctx.Island);
            Queued("대결 결과 글자", WorldInteractionController.DuelResultRect, Ctx.Field);
            Queued("계정 알림", AccountSettingsUI.FieldMessageRect, Ctx.Play);
            Queued("멀티 초대", WorldFieldMultiplayerUI.InviteRect, net);
            Queued("멀티 안내", WorldFieldMultiplayerUI.ToastRect, net);
            Queued("잠긴 리전 글자", PlayerHintOverlay.BlockedRect, Ctx.Field);
            Fixed("미니게임 결과 칸", MinigameSlot, Ctx.Play);
            Fixed("장소 진입 알림", PlayerStatusHUD.SubAreaAlertRect, Ctx.Cave | Ctx.Island);
            Fixed("동굴 출입 토스트", SubAreaWorldBuilder.ToastRect, Ctx.Field | Ctx.Cave);

            // 「챔피언의 꿈」 섬 걷기(DreamPrologueDirector) — 필드 HUD는 다 숨고 잡기 버튼·조이스틱만 남는다.
            Add("꿈 안내 알약", DreamPrologueDirector.HintPillRect(f, DreamPrologueDirector.IslandPillTop), Ctx.Dream);
            Add("꿈 섬 카드", DreamPrologueDirector.IslandCardRect(f), Ctx.Dream);
            Add("꿈 건너뛰기", DreamPrologueDirector.SkipRect(f, false), Ctx.Dream);
            return e;
        }

        private static Rect MinigameSlot(HudFrame f)
            => HudStage.Place(f, HudStageItem.MinigameResult, HudStage.MinigameSlotWidth, HudStage.MinigameSlotHeight);

        // ── 함께 뜰 수 없는 쌍 ──

        /// <summary>
        /// 두 요소가 함께 뜰 수 없으면 그 근거, 뜰 수 있으면 null. 근거는 전부 코드의 숨김 조건이다.
        /// </summary>
        private static string Exclusion(El a, El b, HudFrame f)
        {
            if ((a.Ctx & b.Ctx) == 0) return "다른 장소";
            if (a.Group != null && a.Group == b.Group && a.Variant != b.Variant) return "한 번에 한 모양";
            if (a.FrozenOnly && b.HiddenWhenFrozen || b.FrozenOnly && a.HiddenWhenFrozen) return "조작 잠금";

            // 무대 — HudStage.Granted: 차례 항목은 한 번에 하나, 고정 항목이 서 있으면 차례 항목은 기다린다.
            if (a.Stage != StageKind.None && b.Stage != StageKind.None && !(a.Stage == StageKind.Fixed && b.Stage == StageKind.Fixed))
                return "무대 차례";
            // 무대 안에 서는 가운데 것들은 서 있는 카드와 겹치면 비켜선다(HudStage.OccupiedOver) — 겹치는 쌍은 함께 보이지 않는다.
            if (a.Stage != StageKind.None && b.YieldsToStage || b.Stage != StageKind.None && a.YieldsToStage) return "무대에 비켜섬";

            if (Pair(a, b, Kind.StatusTab, Kind.StatusPanel)) return "상태 탭↔패널(IsExpanded로 하나만)";
            if (Pair(a, b, Kind.StatusPanel, Kind.Minimap)) return "펼친 패널 아래 미니맵(LeftStackOccluded)";
            if (Pair(a, b, Kind.StatusPanel, Kind.YieldsToStatusPanel)) return "펼친 패널과 겹치면 비켜섬";
            if (f.Mobile && Pair(a, b, Kind.StatusPanel, Kind.Quest)) return "모바일 퀘스트 칩은 펼친 패널 밑에서 숨음";
            if (Pair(a, b, Kind.Coach, Kind.IslandGuide)) return "코치가 섬 안내에 비켜섬(HudPresence)";
            if (Pair(a, b, Kind.CatchRing, Kind.CatchButton)) return "고리는 잡기 버튼의 탭 효과";
            if (Pair(a, b, Kind.JoystickZone, Kind.JoystickHint)) return "안내 원은 조이스틱 자리 표시";
            // 조이스틱 자리는 그리는 것이 아니라 입력 영역이다 — 등록된 HUD 위에서는 시작하지 않는다(CanBeginAt). 따로 잰다.
            if (a.Kind == Kind.JoystickZone || b.Kind == Kind.JoystickZone) return "입력 영역";

            // 코치 배너는 대화 버튼·근처 탐험가·동굴 버튼이 서 있고 그 자리와 겹치면 비켜선다(GuidedTutorialController.YieldsNow).
            El coach = a.Kind == Kind.Coach ? a : b.Kind == Kind.Coach ? b : null;
            if (coach != null)
            {
                El other = coach == a ? b : a;
                if ((other.Kind == Kind.Talk || other.Kind == Kind.Prompt) && coach.Rect.Overlaps(WorldInteractionController.TalkRect(f)))
                    return "코치가 대화 버튼에 비켜섬";
                if (other.Kind == Kind.Nearby && coach.Rect.Overlaps(WorldFieldMultiplayerUI.NearbyRect(f)))
                    return "코치가 근처 탐험가에 비켜섬";
                if (other.Kind == Kind.Gate && coach.Rect.Overlaps(SubAreaWorldBuilder.GateRect(f)))
                    return "코치가 동굴 버튼에 비켜섬";
            }
            // 근처 탐험가 판은 대화 버튼과 같은 자리라 대화 버튼이 서면 비켜선다(HudPresence.Talk).
            if (Pair(a, b, Kind.Nearby, Kind.Talk) || Pair(a, b, Kind.Nearby, Kind.Prompt)) return "근처 탐험가가 대화 버튼에 비켜섬";
            return null;
        }

        private static bool Pair(El a, El b, Kind x, Kind y) => a.Kind == x && b.Kind == y || a.Kind == y && b.Kind == x;

        // ── 검사 ──

        [TestCaseSource(nameof(Screens))]
        public void EveryPairThatCanShowTogether_DoesNotOverlap(string name, float pw, float ph, float safeLeft, float safeRight,
            float safeTop, float safeBottom, bool mobile)
        {
            HudFrame f = Frame(pw, ph, safeLeft, safeRight, safeTop, safeBottom, mobile);
            List<El> els = Elements(f);
            var failures = new List<string>();
            var excluded = new Dictionary<string, int>();
            int measured = 0;
            for (int i = 0; i < els.Count; i++)
                for (int j = 0; j < i; j++)
                {
                    string why = Exclusion(els[i], els[j], f);
                    if (why != null)
                    {
                        excluded.TryGetValue(why, out int n);
                        excluded[why] = n + 1;
                        continue;
                    }
                    measured++;
                    if (els[i].Rect.Overlaps(els[j].Rect))
                        failures.Add($"{els[i].Name} {els[i].Rect} ↔ {els[j].Name} {els[j].Rect}");
                }

            TestContext.WriteLine($"[HudSweep] {name}: 요소 {els.Count}, 잰 쌍 {measured}, 뺀 쌍 " +
                string.Join(", ", excluded.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
            Assert.IsEmpty(failures, $"{name}: {failures.Count}쌍이 겹친다\n" + string.Join("\n", failures));
        }

        [TestCaseSource(nameof(Screens))]
        public void EveryElement_StaysInsideTheSafeArea(string name, float pw, float ph, float safeLeft, float safeRight,
            float safeTop, float safeBottom, bool mobile)
        {
            HudFrame f = Frame(pw, ph, safeLeft, safeRight, safeTop, safeBottom, mobile);
            Rect safe = Rect.MinMaxRect(f.SafeLeft, f.SafeTop, f.Width - f.SafeRight, f.Height - f.SafeBottom);
            var outside = new StringBuilder();
            foreach (El el in Elements(f))
            {
                Rect r = el.Rect;
                if (r.width < 1f || r.height < 1f) outside.AppendLine($"{el.Name} 크기가 없다 {r}");
                if (r.xMin < safe.xMin - 0.5f || r.yMin < safe.yMin - 0.5f || r.xMax > safe.xMax + 0.5f || r.yMax > safe.yMax + 0.5f)
                    outside.AppendLine($"{el.Name} {r} — 안전 영역 {safe}");
            }
            Assert.IsTrue(outside.Length == 0, name + ":\n" + outside);
        }

        [TestCaseSource(nameof(Screens))]
        public void Stage_HoldsTheFixedSlots_AndEveryCardStaysOnIt(string name, float pw, float ph, float safeLeft, float safeRight,
            float safeTop, float safeBottom, bool mobile)
        {
            HudFrame f = Frame(pw, ph, safeLeft, safeRight, safeTop, safeBottom, mobile);
            List<El> els = Elements(f);
            // 무대는 섬 HUD가 서 있을 때(섬)와 없을 때(필드·동굴)가 다르다 — 둘 다 고정 칸을 담고, 그 맥락의 카드가 그 안에 선다.
            foreach (bool islandHud in new[] { false, true })
            {
                HudFrame g = f.WithIslandHud(islandHud);
                string tag = name + (islandHud ? " 섬" : " 필드");
                Rect area = HudStage.Area(g);
                Assert.AreEqual(HudStage.Area(f, islandHud), area, tag + ": Area(f)는 화면이 든 맥락(IslandHud)을 따른다");
                TestContext.WriteLine($"[HudStage] {tag}: 무대 {area}");
                Assert.GreaterOrEqual(area.width, HudStage.MinigameSlotWidth, $"{tag}: 무대 폭이 미니게임 결과 빛보다 좁다 {area}");
                Assert.GreaterOrEqual(area.height, HudStage.PlaceToastTop + SubAreaWorldBuilder.ToastHeight,
                    $"{tag}: 고정 칸 셋이 무대에 다 안 들어간다 {area}");
                Assert.LessOrEqual(area.width, HudStage.MaxWidth + 0.01f);

                // 고정 칸은 줄지 않는다(차례 항목만 무대 크기에 맞춰 줄어든다).
                Rect slot = MinigameSlot(g);
                Assert.AreEqual(HudStage.MinigameSlotWidth, slot.width, 0.01f);
                Assert.AreEqual(HudStage.MinigameSlotHeight, slot.height, 0.01f);
                Assert.AreEqual(HudStage.PlaceAlertHeight, PlayerStatusHUD.SubAreaAlertRect(g).height, 0.01f);
                Assert.AreEqual(SubAreaWorldBuilder.ToastHeight, SubAreaWorldBuilder.ToastRect(g).height, 0.01f);

                // 결과 글자와 그 둘레로 퍼지는 빛(최대 448×224, 글자 가운데 기준)이 칸 안에 든다.
                Rect label = CaptureMinigameController.ResultLabelRect(g);
                AssertInside(slot, label, tag + " 미니게임 결과 글자");
                AssertInside(slot, new Rect(label.center.x - 224f, label.center.y - 112f, 448f, 224f), tag + " 미니게임 결과 빛");

                foreach (El el in els)
                    if (el.Stage != StageKind.None && el.IslandHud == islandHud)
                        AssertInside(area, el.Rect, $"{tag} {el.Name}");

                // 성공 카드는 정사각형 그대로 줄어든다(안의 배치가 640×640 절대 좌표다).
                Rect success = CapturePopupUI.SuccessRect(g);
                Assert.AreEqual(success.width, success.height, 0.01f, tag + ": 성공 카드는 정사각형");
            }
        }

        // ── 무대는 섬 HUD가 서 있을 때만 그것을 피한다 ──

        private static IEnumerable<TestCaseData> PortraitScreens() => Screens().Where(c => (float)c.Arguments[2] > (float)c.Arguments[1]);

        [TestCaseSource(nameof(PortraitScreens))]
        public void Stage_OnTheField_StandsInTheMiddle_AndOnTheIsland_ClearsTheIslandColumn(string name, float pw, float ph,
            float safeLeft, float safeRight, float safeTop, float safeBottom, bool mobile)
        {
            // 2026-10-03 QA 실측(720×1280): 필드에서 포획 성공·실패 카드와 소식 카드가 화면 가운데가 아니라 왼쪽 절반(x 18~378)에 붙었다.
            // 무대가 섬에서만 뜨는 섬 HUD 열(우측, 단축 바 아래)과 그 아래 섬 시각 알림을 필드에서도 피했다.
            HudFrame f = Frame(pw, ph, safeLeft, safeRight, safeTop, safeBottom, mobile);
            Assert.IsTrue(f.Mobile && f.Portrait, name);
            Assert.IsFalse(f.IslandHud, "테스트가 세운 화면은 필드(섬 HUD 없음)다");
            Assert.AreEqual(HudStage.Area(f, false), HudStage.Area(f));

            // 필드 — 화면 가로 가운데. 오른쪽 열(단축 바·시각 칩·알림·필드 멀티 상태)과는 겹치지 않는다(그 아래에서 시작한다).
            float mid = f.Width * 0.5f;
            Rect quick = QuickAccessBarUI.ShortcutBarRectFor(f);
            Rect chip = WorldClockRules.FieldChip(f);
            var rightColumn = new[] { quick, chip, WorldClockRules.NoticeBelow(f, chip), WorldFieldMultiplayerUI.StatusRect(f) };
            var onField = new[]
            {
                ("포획 성공 카드", CapturePopupUI.SuccessRect(f)), ("포획 실패 카드", CapturePopupUI.FailRect(f)),
                ("소식 카드", FieldMomentsUI.ToastFootprint(f)), ("퀘스트 완료 알림", TutorialQuestUI.QuestDoneRect(f, true)),
            };
            foreach ((string what, Rect r) in onField)
            {
                Assert.AreEqual(mid, r.center.x, 2f, $"{name}: 필드의 {what} {r}이(가) 화면 가로 가운데({mid})에 서지 않는다");
                foreach (Rect col in rightColumn)
                    Assert.IsFalse(r.Overlaps(col), $"{name}: 필드의 {what} {r}이(가) 오른쪽 열 {col}을 덮는다");
            }

            // 섬 — 섬 HUD 열(내 섬·남의 섬)과 그 아래 섬 시각 칩·알림을 피한다.
            HudFrame isle = f.WithIslandHud(true);
            var onIsland = new[]
            {
                ("포획 성공 카드", CapturePopupUI.SuccessRect(isle)), ("포획 실패 카드", CapturePopupUI.FailRect(isle)),
                ("소식 카드", FieldMomentsUI.ToastFootprint(isle)), ("섬 결과 토스트", IslandHudUI.ToastRect(isle)),
            };
            foreach (bool visiting in new[] { false, true })
            {
                Rect panel = visiting ? IslandHudLayout.VisitPanel(f) : IslandHudLayout.OwnPanel(f);
                Rect islandChip = WorldClockRules.Chip(f, true, visiting);
                Rect islandNotice = WorldClockRules.NoticeBelow(f, islandChip);
                foreach ((string what, Rect r) in onIsland)
                    foreach (Rect hud in new[] { panel, islandChip, islandNotice })
                        Assert.IsFalse(r.Overlaps(hud), $"{name}: 섬{(visiting ? "(구경)" : "")}의 {what} {r}이(가) 섬 HUD {hud}를 덮는다");
            }
        }

        [Test]
        public void StageGranted_FixedAlwaysStand_QueuedStandOneAtATimeByPriority()
        {
            int Mask(params HudStageItem[] items) => items.Aggregate(0, (m, i) => m | 1 << (int)i);

            // 고정은 늘 선다 — 다른 담당 파일이 시간을 쥐고 있어 기다리게 할 수 없다.
            Assert.IsTrue(HudStage.Granted(HudStageItem.MinigameResult, Mask(HudStageItem.MinigameResult, HudStageItem.CaptureResult)));
            Assert.IsTrue(HudStage.Granted(HudStageItem.PlaceToast, Mask(HudStageItem.PlaceAlert, HudStageItem.PlaceToast)));
            // 고정이 서 있으면 차례 항목은 기다린다 — 미니게임 결과 글자 다음에 포획 결과 카드.
            Assert.IsFalse(HudStage.Granted(HudStageItem.CaptureResult, Mask(HudStageItem.MinigameResult, HudStageItem.CaptureResult)));
            // 차례 항목끼리는 우선순위 — 포획 결과 → 퀘스트 완료 → 다음 퀘스트 → 소식.
            int capture = Mask(HudStageItem.CaptureResult, HudStageItem.QuestDone, HudStageItem.Moment);
            Assert.IsTrue(HudStage.Granted(HudStageItem.CaptureResult, capture));
            Assert.IsFalse(HudStage.Granted(HudStageItem.QuestDone, capture));
            Assert.IsFalse(HudStage.Granted(HudStageItem.Moment, capture));
            Assert.IsTrue(HudStage.Granted(HudStageItem.QuestDone, Mask(HudStageItem.QuestDone, HudStageItem.Moment)));
            Assert.IsTrue(HudStage.Granted(HudStageItem.Moment, Mask(HudStageItem.Moment, HudStageItem.RegionLock)));
            Assert.IsFalse(HudStage.Granted(HudStageItem.RegionLock, Mask(HudStageItem.Moment, HudStageItem.RegionLock)));
            Assert.IsTrue(HudStage.Granted(HudStageItem.RegionLock, Mask(HudStageItem.RegionLock)));

            // 고정 셋은 서로 다른 칸 — 위에서부터 미니게임 결과, 장소 진입 알림, 동굴 출입 토스트.
            var area = new Rect(100f, 200f, 800f, 700f);
            Rect mg = HudStage.Place(area, HudStageItem.MinigameResult, HudStage.MinigameSlotWidth, HudStage.MinigameSlotHeight);
            Rect alert = HudStage.Place(area, HudStageItem.PlaceAlert, 900f, HudStage.PlaceAlertHeight);
            Rect toast = HudStage.Place(area, HudStageItem.PlaceToast, 560f, 56f);
            Assert.IsFalse(mg.Overlaps(alert));
            Assert.IsFalse(alert.Overlaps(toast));
            Assert.IsFalse(mg.Overlaps(toast));
            Assert.AreEqual(area.width, alert.width, 0.01f, "넓은 것은 무대 폭으로 줄어든다");
            Assert.AreEqual(area.center.x, toast.center.x, 0.01f, "가운데 정렬");
            Assert.AreEqual(area.y, HudStage.Place(area, HudStageItem.QuestDone, 640f, 172f).y, 0.01f, "차례 항목은 무대 맨 위");
            Assert.AreEqual(0.5f, HudStage.FitScale(new Rect(0f, 0f, 320f, 900f), 640f, 640f), 0.0001f);
            Assert.AreEqual(1f, HudStage.FitScale(new Rect(0f, 0f, 2000f, 2000f), 640f, 640f), 0.0001f, "키우지는 않는다");
        }

        // ── 조이스틱 자리 — 늘 떠 있는 HUD는 비워 두고, 잠깐 뜨는 것을 다 띄워도 절반 넘게 빈다 ──

        [TestCaseSource(nameof(MobileScreens))]
        public void JoystickZone_NoStandingHudInside_AndPassingOverlaysLeaveMostOfItFree(string name, float pw, float ph,
            float safeLeft, float safeRight, float safeTop, float safeBottom, bool mobile)
        {
            // 조이스틱은 등록된 HUD 위에서 시작하지 않는다(VirtualJoystickUI.CanBeginAt). 그래서 사분면을 늘 덮는 HUD가 있으면
            // 그만큼 조작 자리가 줄어든다 — 늘 떠 있는 것(필드·섬)은 사분면에 하나도 없어야 하고, 한 장소에서 잠깐 뜨는 것(안내 배너·
            // 무대의 카드·대화 버튼·동굴 입구·꿈 카드·펼친 상태 패널)을 한꺼번에 다 띄워도 사분면의 절반 넘게 비어야 한다.
            // 장소마다 따로 센다 — 무대 자리가 섬과 그 밖에서 달라(섬 HUD가 서 있을 때만 피한다) 섞어 합치면 함께 뜰 수 없는 두 자리를 함께 센다.
            HudFrame f = Frame(pw, ph, safeLeft, safeRight, safeTop, safeBottom, mobile);
            Assert.IsTrue(VirtualJoystickUI.EnabledFor(f));
            Rect zone = VirtualJoystickUI.ZoneRect(f);
            Rect hint = VirtualJoystickUI.HintRect(f);
            List<El> els = Elements(f);

            var standingNames = new HashSet<string>
            {
                "상태 탭", "미니맵", "단축 바", "퀘스트 펼친 칩", "목표 행(펼친 칩)", "리전 배너", "내기 점수판", "시각 칩", "변화 알림",
                "섬 HUD 판", "섬 HUD 판(구경)", "섬 시각 칩", "섬 변화 알림", "섬 시각 칩(구경)", "섬 변화 알림(구경)", "잡기 버튼",
                "설정 버튼", "멀티 필드 상태",
            };
            if (!f.Portrait) standingNames.Add("멀티 대화 기록");   // 가로는 상태 판 아래에 늘 선다(세로는 무대에 비켜선다)
            foreach (El el in els.Where(x => standingNames.Contains(x.Name)))
            {
                Assert.IsFalse(el.Rect.Overlaps(zone), $"{name}: 늘 떠 있는 {el.Name} {el.Rect}이(가) 조이스틱 자리 {zone}를 덮는다");
                Assert.IsFalse(el.Rect.Contains(hint.center), $"{name}: 조이스틱 안내 원 가운데가 {el.Name}에 덮인다");
            }

            var passingNames = new HashSet<string>
            {
                "코치 배너", "섬 안내 배너", "대화 버튼", "상호작용 안내 글자", "상호작용 버튼", "동굴 입구 버튼", "멀티 근처 탐험가",
                "멀티 대화 기록", "꿈 섬 카드", "꿈 안내 알약", "상태 패널(펼침)",
            };
            foreach (Ctx place in new[] { Ctx.Field, Ctx.Cave, Ctx.IslandOwn, Ctx.IslandVisit, Ctx.Dream })
            {
                List<Rect> covers = els
                    .Where(x => (x.Ctx & place) != 0 && (passingNames.Contains(x.Name) || x.Stage != StageKind.None))
                    .Select(x => x.Rect).ToList();
                float free = FreeFraction(zone, covers);
                TestContext.WriteLine($"[Joystick] {name} {place}: 잠깐 뜨는 것을 전부 띄워도 비는 비율 {free:P0}");
                Assert.Greater(free, 0.5f, $"{name} {place}: 잠깐 뜨는 HUD가 조이스틱 자리의 절반 넘게 덮는다");
            }
        }

        [Test]
        public void Joystick_DesktopLayout_IsOff()
        {
            // 데스크톱 배치는 좌하단에 퀘스트 칩이 선다 — 조이스틱(과 안내 원)은 모바일 배치에서만 켠다.
            Assert.IsFalse(VirtualJoystickUI.EnabledFor(Frame(1920f, 1080f, 0f, 0f, 0f, 0f, false)));
            Assert.IsTrue(VirtualJoystickUI.EnabledFor(Frame(1920f, 1080f, 0f, 0f, 0f, 0f, true)));
            Assert.IsTrue(VirtualJoystickUI.EnabledFor(Frame(1080f, 1920f, 0f, 0f, 0f, 0f, false)), "세로로 긴 창은 모바일 배치");
        }

        [Test]
        public void JoystickRects_MatchTheTouchRule()
        {
            // 픽셀(Y-up) 규칙(CanBeginAt·HintCenter)과 가상(Y-down) 사각형이 같은 자리를 가리킨다.
            HudFrame f = Frame(1280f, 720f, 40f, 0f, 0f, 20f, true);
            Rect hint = VirtualJoystickUI.HintRect(f);
            Vector2 up = VirtualJoystickUI.HintCenter(1280f, 720f, 40f, 20f);
            Assert.AreEqual(up.x / f.Scale, hint.center.x, 0.01f);
            Assert.AreEqual((720f - up.y) / f.Scale, hint.center.y, 0.01f);
            Assert.AreEqual(VirtualJoystickUI.HintRadius(1280f, 720f) * 2f / f.Scale, hint.width, 0.01f);
            Rect zone = VirtualJoystickUI.ZoneRect(f);
            Assert.AreEqual(40f / f.Scale, zone.xMin, 0.01f);
            Assert.AreEqual(640f / f.Scale, zone.xMax, 0.01f);
            Assert.AreEqual(360f / f.Scale, zone.yMin, 0.01f);
            Assert.AreEqual(700f / f.Scale, zone.yMax, 0.01f);
        }

        // ── 도우미 ──

        private static float FreeFraction(Rect zone, List<Rect> covers)
        {
            int total = 0, free = 0;
            for (float y = zone.yMin + 2f; y < zone.yMax; y += 4f)
                for (float x = zone.xMin + 2f; x < zone.xMax; x += 4f)
                {
                    total++;
                    var p = new Vector2(x, y);
                    bool covered = false;
                    foreach (Rect r in covers)
                        if (r.Contains(p)) { covered = true; break; }
                    if (!covered) free++;
                }
            return total > 0 ? (float)free / total : 1f;
        }

        private static void AssertInside(Rect outer, Rect inner, string what)
        {
            Assert.IsTrue(inner.xMin >= outer.xMin - 0.01f && inner.yMin >= outer.yMin - 0.01f
                          && inner.xMax <= outer.xMax + 0.01f && inner.yMax <= outer.yMax + 0.01f,
                $"{what} {inner}이(가) {outer} 밖으로 나간다");
        }
    }
}
#endif
