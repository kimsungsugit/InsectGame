using InsectGame.Capture;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 화면 가운데 <see cref="HudStage"/>에 잠깐 서는 카드·알림. <b>값 순서가 우선순위다</b>(작을수록 먼저).
    /// 앞의 셋은 <b>고정</b> — 다른 담당 파일이 시간을 쥐고 있어 기다리게 할 수 없다. 무대 위 정해진 칸에 차례로 쌓인다.
    /// 나머지는 <b>차례</b> — 한 번에 하나만 서고, 고정이 서 있거나 앞 차례가 서 있으면 기다린다(시간도 멈춘다).
    /// </summary>
    public enum HudStageItem
    {
        /// <summary>포획 미니게임의 결과(PERFECT 등, CaptureMinigameController — 1.5초).</summary>
        MinigameResult = 0,
        /// <summary>서브에리어·섬에 들어선 알림(PlayerStatusHUD).</summary>
        PlaceAlert = 1,
        /// <summary>동굴 출입 토스트(SubAreaWorldBuilder — 3초).</summary>
        PlaceToast = 2,
        /// <summary>포획 결과 카드(성공·실패, CapturePopupUI).</summary>
        CaptureResult = 3,
        /// <summary>퀘스트 완료 알림(TutorialQuestUI).</summary>
        QuestDone = 4,
        /// <summary>다음 퀘스트 알림(TutorialQuestUI).</summary>
        QuestNext = 5,
        /// <summary>필드 소식 카드(FieldMomentsUI — 레벨 업·이로치 등).</summary>
        Moment = 6,
        /// <summary>섬 수확·좋아요 결과(IslandHudUI).</summary>
        IslandToast = 7,
        /// <summary>곤충잡이 대결 결과 글자(WorldInteractionController).</summary>
        DuelResult = 8,
        /// <summary>계정 처리 결과(AccountSettingsUI).</summary>
        AccountMessage = 9,
        /// <summary>필드 초대(WorldFieldMultiplayerUI).</summary>
        NetInvite = 10,
        /// <summary>필드 멀티 안내(WorldFieldMultiplayerUI).</summary>
        NetToast = 11,
        /// <summary>잠긴 리전에 부딪혔다는 안내(PlayerHintOverlay).</summary>
        RegionLock = 12,
    }

    /// <summary>
    /// 화면 가운데의 <b>무대</b> — 잠깐 뜨는 카드·알림이 서는 한 자리와, 누가 설지 정하는 중재.
    ///
    /// <b>왜 있나.</b> 포획 결과 카드·퀘스트 완료·다음 퀘스트·소식 카드·동굴 출입 알림·섬 토스트·대결 결과·계정 알림이 저마다
    /// 화면 위 가운데(ContentTop+0~+262)나 가운데 줄에 자리를 잡아, 한 번의 포획(카드 + 퀘스트 완료 + 레벨 업)이나 동굴 진입
    /// (알림 + 토스트 + 퀘스트 완료)마다 서로와 리전 배너·단축 바·미니맵을 덮었다(2026-10-03 전수 검사에서 204쌍). 자리를 하나로 모으고
    /// 차례를 세운다.
    ///
    /// <b>자리(<see cref="Area(HudFrame)"/>)</b>는 늘 떠 있는 HUD — 상태 패널·미니맵·퀘스트 칩·단축 바·시각 칩과 알림·포획 아이템 패널·리전 배너·
    /// 내기 점수판·잡기 버튼과 글자·상호작용 버튼·동굴 입구 버튼·필드 멀티 상태와 대화 기록 — 를 모두 피한 사각형이다. 섬 HUD 판(과 섬 정보 줄·
    /// 섬 시각 알림)은 <b>섬 HUD가 서 있을 때만</b> 피한다(<see cref="HudFrame.IslandHud"/>) — 필드에서도 피하면 세로 화면의 카드가 섬 열
    /// 왼쪽, 곧 화면 왼쪽 절반으로 밀린다(2026-10-03 QA 실측, 720×1280에서 x 18~378).
    /// 그 안에 서는 가운데 것들(대화 버튼·코치 배너·섬 안내 배너·근처 탐험가·세로 화면의 대화 기록)은 서 있는 카드와 겹치면 비켜선다
    /// (<see cref="OccupiedOver"/>).
    ///
    /// <b>중재</b>는 프레임 단위다 — 서고 싶은 쪽이 매 프레임 <see cref="Request"/>를 부르고, 이번 프레임과 직전 프레임에 원한 것 중
    /// 우선순위로 정한다(OnGUI 순서와 무관하게 같은 답이 나온다). 판정 자체는 <see cref="Granted"/>(순수)다.
    /// </summary>
    public static class HudStage
    {
        /// <summary>고정 항목 수(<see cref="HudStageItem"/>의 앞 셋).</summary>
        public const int FixedCount = 3;
        /// <summary>무대의 최대 폭 — 넓은 화면에서 카드가 화면 끝까지 퍼지지 않게.</summary>
        public const float MaxWidth = 1100f;

        // 고정 칸 — 늘 같은 자리(서 있는 것과 무관하게 칸을 비워 둔다. 동시에 서도 겹치지 않는다).
        /// <summary>미니게임 결과 칸 — 글자(72)와 그 둘레로 퍼지는 빛(최대 448×224)을 담는다.</summary>
        public const float MinigameSlotHeight = 232f;
        public const float MinigameSlotWidth = 448f;
        public const float PlaceAlertTop = MinigameSlotHeight + UITheme.Space.S;
        public const float PlaceAlertHeight = 76f;
        public const float PlaceToastTop = PlaceAlertTop + PlaceAlertHeight + UITheme.Space.XS;

        private static int frame = -1;
        private static int current;
        private static int previous;
        private const int ItemCount = (int)HudStageItem.RegionLock + 1;
        private static readonly Rect[] lastRect = new Rect[ItemCount];
        private static readonly int[] rectFrame = new int[ItemCount];

        private static void Roll()
        {
            int f = Time.frameCount;
            if (f == frame) return;
            previous = f == frame + 1 ? current : 0;
            current = 0;
            frame = f;
        }

        public static bool IsFixed(HudStageItem item) => (int)item < FixedCount;

        /// <summary>
        /// 이번 프레임에 무대에 서고 싶다 — 서 있는 동안 매 프레임(Update·OnGUI 어디서든) 부른다. 지금 서도 되면 true.
        /// 고정 항목은 늘 true다. 차례 항목이 false를 받으면 그리지 말고 시간도 멈출 것(기다리는 중이다).
        /// </summary>
        public static bool Request(HudStageItem item)
        {
            Roll();
            current |= 1 << (int)item;
            return Granted(item, current | previous);
        }

        /// <summary>
        /// <see cref="Request(HudStageItem)"/>와 같고, 지금 그리는 자리(<paramref name="footprint"/>)를 함께 알린다 —
        /// 가운데 것들이 <see cref="OccupiedOver"/>로 <b>실제로 겹칠 때만</b> 비켜서게. 그리는 자리(OnGUI)에서 부른다.
        /// </summary>
        public static bool Request(HudStageItem item, Rect footprint)
        {
            bool granted = Request(item);
            int i = (int)item;
            lastRect[i] = footprint;
            rectFrame[i] = Time.frameCount + 1;   // 0은 '한 번도 못 받았다'
            return granted;
        }

        /// <summary>무대에 무언가 서 있거나 서려고 한다.</summary>
        public static bool Occupied
        {
            get
            {
                Roll();
                return (current | previous) != 0;
            }
        }

        /// <summary>
        /// 지금 무대에 <b>서 있는</b>(차례를 받은) 카드가 <paramref name="area"/>와 겹치는가 — 무대 안의 가운데 것들(대화 버튼·코치 배너·
        /// 섬 안내 배너·근처 탐험가·세로 화면의 대화 기록)이 비켜설지. 기다리는 카드는 그려지지 않으니 세지 않는다.
        /// 자리를 아직 모르는 카드(이번·직전 프레임에 <see cref="Request(HudStageItem, Rect)"/>가 없었다)는 겹친다고 본다.
        /// </summary>
        public static bool OccupiedOver(Rect area)
        {
            Roll();
            int mask = current | previous;
            if (mask == 0) return false;
            int stampNow = Time.frameCount + 1;
            for (int i = 0; i < ItemCount; i++)
            {
                if ((mask & (1 << i)) == 0 || !Granted((HudStageItem)i, mask)) continue;
                int age = stampNow - rectFrame[i];
                bool known = rectFrame[i] != 0 && age >= 0 && age <= 1;
                if (!known || lastRect[i].Overlaps(area)) return true;
            }
            return false;
        }

        /// <summary>
        /// 순수 판정 — <paramref name="activeMask"/>(서고 싶은 항목들의 비트) 가운데 <paramref name="item"/>이 설 차례인가.
        /// 고정은 늘 선다. 차례 항목은 고정이 하나도 없고 자기보다 앞선 차례가 없을 때만 선다.
        /// </summary>
        public static bool Granted(HudStageItem item, int activeMask)
        {
            if (IsFixed(item)) return true;
            int fixedMask = (1 << FixedCount) - 1;
            if ((activeMask & fixedMask) != 0) return false;
            int ahead = (1 << (int)item) - 1;
            return (activeMask & ahead) == 0;
        }

        // ── 자리 ──

        /// <summary>
        /// 무대 사각형(가상 좌표) — 그 화면 기준. 섬 HUD가 서 있는지는 <paramref name="f"/>가 든다(<see cref="HudFrame.IslandHud"/> —
        /// 그리기가 넘기는 <see cref="HudFrame.Current"/>는 <see cref="HudPresence"/>에서 읽고, 테스트는 <see cref="HudFrame.WithIslandHud"/>로 세운다).
        /// </summary>
        public static Rect Area(HudFrame f) => Area(f, f.IslandHud);

        /// <summary>
        /// 무대 사각형(가상 좌표) — <b>순수 계산</b>. 늘 떠 있는 HUD를 모두 피한다 — 화면 모양마다 막는 것이 달라 셋으로 나눈다.
        /// <paramref name="islandHud"/>는 섬 HUD 판(<see cref="IslandHudUI"/>)이 서 있는가 — <b>그때만</b> 섬 판·섬 정보 줄·섬 시각 알림을 피한다.
        /// 필드에서 그것까지 피하면 세로 화면의 카드가 섬 열 왼쪽(화면 왼쪽 절반)으로 밀린다.
        /// <b>데스크톱</b>: 왼쪽 상태 패널과 오른쪽 시각 알림 사이, 내기 점수판 아래 ~ 동굴 입구 버튼(섬: 섬 정보 줄) 위.
        /// <b>가로 모바일</b>: 미니맵 옆 퀘스트 칩 오른쪽 ~ 습격 경고·시각 알림 왼쪽, 내기 점수판과 단축 바 왼쪽 띠(필드: 필드 멀티 상태·대화 기록,
        /// 섬: 섬 HUD 판) 아래 ~ 동굴 입구 버튼 위.
        /// <b>세로 모바일</b>: 퀘스트 목표 행(「왜」 줄이 붙은 두 줄 높이)과 오른쪽 열(단축 바 → 시각 칩·알림 → 필드 멀티 상태) 아래 ~ 동굴 입구 버튼·잡기 글자·상호작용 버튼 위.
        /// 필드는 왼쪽 끝 ~ 오른쪽 끝(화면 가운데에 선다), 섬은 왼쪽 끝 ~ 섬 HUD 열(과 그 아래 섬 시각 알림) 왼쪽.
        /// </summary>
        public static Rect Area(HudFrame f, bool islandHud)
        {
            float s = UITheme.Space.S;
            float x0, x1, y0, y1;
            Rect gate = SubAreaWorldBuilder.GateRect(f);
            Rect fieldNotice = WorldClockRules.NoticeBelow(f, WorldClockRules.FieldChip(f));
            if (!f.Mobile)
            {
                x0 = PlayerStatusHUD.PanelRect(f).xMax + s;
                x1 = fieldNotice.x - s;
                y0 = FieldMomentsUI.RaceChipRect(f).yMax + s;
                y1 = gate.y;
                if (islandHud) y1 = Mathf.Min(y1, IslandHudLayout.DesktopInfo(IslandHudLayout.OwnPanel(f)).y);
                y1 -= s;
            }
            else if (!f.Portrait)
            {
                Rect quest = QuestChipLayout.ChipRect(f, QuestChipLayout.ExpandedHeight, QuestChipLayout.RowHeightWithWhy);
                x0 = quest.xMax + s;
                x1 = Mathf.Min(CatchButtonLayout.WarnRect(f).x, fieldNotice.x) - s;
                // 단축 바 왼쪽 띠 — 섬에서는 섬 HUD 두 칸 판, 필드에서는 같은 자리의 필드 멀티 상태 판과 그 아래 대화 기록(가로는 늘 선다).
                float band = islandHud
                    ? IslandHudLayout.OwnPanel(f).yMax
                    : Mathf.Max(WorldFieldMultiplayerUI.StatusRect(f).yMax, WorldFieldMultiplayerUI.MessagesRect(f).yMax);
                y0 = Mathf.Max(FieldMomentsUI.RaceChipRect(f).yMax, band) + s;
                y1 = gate.y - s;
            }
            else
            {
                // 목표 행은 「왜」 둘째 줄이 붙은 큰 쪽(RowHeightWithWhy)을 피한다 — 칩을 펼친 쪽으로 재는 것과 같은 이유다.
                Rect quest = QuestChipLayout.ChipRect(f, QuestChipLayout.ExpandedHeight, QuestChipLayout.RowHeightWithWhy);
                Rect row = QuestChipLayout.Row(quest, quest.width, QuestChipLayout.RowHeightWithWhy);
                x0 = f.ContentLeft;
                if (islandHud)
                {
                    Rect island = IslandHudLayout.OwnPanel(f);
                    Rect islandNotice = WorldClockRules.NoticeBelow(f, WorldClockRules.IslandMobileChip(
                        QuickAccessBarUI.ShortcutBarRectFor(f), island, true));
                    x1 = Mathf.Min(islandNotice.x, island.x) - s;
                }
                else
                {
                    // 필드의 오른쪽 열(단축 바 → 시각 칩·알림 → 필드 멀티 상태)은 아래 y0보다 위에서 끝난다 — 가로로 피할 것이 없다.
                    x1 = f.ContentRight;
                }
                y0 = Mathf.Max(Mathf.Max(row.yMax, fieldNotice.yMax), WorldFieldMultiplayerUI.StatusRect(f).yMax) + s;
                y1 = Mathf.Min(gate.y, Mathf.Min(CatchButtonLayout.WarnRect(f).y,
                    WorldInteractionController.InteractButtonRect(f).y)) - s;
            }
            float w = Mathf.Max(1f, Mathf.Min(MaxWidth, x1 - x0));
            float cx = (x0 + x1) * 0.5f;
            return new Rect(cx - w * 0.5f, y0, w, Mathf.Max(1f, y1 - y0));
        }

        /// <summary>
        /// 무대 위 <paramref name="item"/>의 자리 — 가운데 정렬, 폭·높이는 무대 안으로 줄인다. 차례 항목은 무대 맨 위에,
        /// 고정 항목은 정해진 칸(미니게임 결과 → 리전 진입 알림 → 동굴 출입 토스트)에 선다. 섬 HUD를 피할지는 <paramref name="f"/>가 든다.
        /// </summary>
        public static Rect Place(HudFrame f, HudStageItem item, float width, float height)
        {
            return Place(Area(f), item, width, height);
        }

        /// <summary><see cref="Place(HudFrame, HudStageItem, float, float)"/>의 순수 몸통 — 무대 사각형을 받는다.</summary>
        public static Rect Place(Rect area, HudStageItem item, float width, float height)
        {
            float top = 0f;
            switch (item)
            {
                case HudStageItem.PlaceAlert: top = PlaceAlertTop; break;
                case HudStageItem.PlaceToast: top = PlaceToastTop; break;
            }
            float w = Mathf.Clamp(width, 1f, area.width);
            float h = Mathf.Clamp(height, 1f, Mathf.Max(1f, area.height - top));
            return new Rect(area.x + (area.width - w) * 0.5f, area.y + top, w, h);
        }

        /// <summary>정사각형에 가까운 큰 카드(포획 성공)를 무대에 맞춰 줄일 배율 — 1 이하.</summary>
        public static float FitScale(Rect area, float width, float height)
        {
            return Mathf.Min(1f, Mathf.Min(area.width / Mathf.Max(1f, width), area.height / Mathf.Max(1f, height)));
        }
    }

    /// <summary>지금 화면에 서 있는가를 다른 HUD가 보고 비켜서는 것들 — 가운데 안내 배너가 비켜설 버튼들과, 가운데 무대가 피할 섬 HUD.</summary>
    public enum HudPresenceItem
    {
        /// <summary>섬 안내 배너(IslandGuideUI) — 코치 배너가 비켜선다.</summary>
        IslandGuide = 0,
        /// <summary>가운데 대화 버튼(WorldInteractionController) — 안내 배너가 겹치면 비켜서고, 근처 탐험가 패널은 같은 자리라 비켜선다.</summary>
        Talk = 1,
        /// <summary>근처 탐험가 패널(WorldFieldMultiplayerUI, 대화 버튼과 같은 자리) — 안내 배너가 겹치면 비켜선다.</summary>
        Nearby = 2,
        /// <summary>동굴 입구·나가기 버튼(SubAreaWorldBuilder) — 안내 배너가 겹치면 비켜선다.</summary>
        Gate = 3,
        /// <summary>
        /// 섬 HUD 판(IslandHudUI — 내 섬·남의 섬. 꿈 섬에서는 숨는다) — 가운데 무대(<see cref="HudStage.Area(HudFrame)"/>)가 이때만 섬 HUD를 피한다.
        /// <see cref="HudFrame.Current"/>가 읽어 <see cref="HudFrame.IslandHud"/>에 담는다.
        /// </summary>
        IslandHud = 4,
    }

    /// <summary>
    /// 지금 화면에 서 있는가 — 서 있는 쪽이 매 프레임(Update든 OnGUI든) <see cref="Mark"/>를 부르고, 비켜설 쪽이 <see cref="IsShowing"/>으로 본다.
    /// 이번 프레임이나 직전 프레임에 불렀으면 서 있는 것으로 친다(OnGUI 순서와 무관하게 같은 답).
    /// </summary>
    public static class HudPresence
    {
        private const int ItemCount = (int)HudPresenceItem.IslandHud + 1;
        private static readonly int[] lastFrame = NeverMarked();

        private static int[] NeverMarked()
        {
            var frames = new int[ItemCount];
            for (int i = 0; i < frames.Length; i++) frames[i] = -10;
            return frames;
        }

        public static void Mark(HudPresenceItem item)
        {
            lastFrame[(int)item] = Time.frameCount;
        }

        public static bool IsShowing(HudPresenceItem item)
        {
            int d = Time.frameCount - lastFrame[(int)item];
            return d >= 0 && d <= 1;
        }
    }
}
