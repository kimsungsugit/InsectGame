using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>
    /// 필드 HUD의 시각·날씨 칩("밤 22:10 | 비")과 시간대·날씨가 바뀔 때 잠깐 뜨는 알림 한 줄.
    ///
    /// <b>날씨는 지금 선 지역 기준이다</b>(<see cref="WeatherForecast.EffectiveIn"/>) — 설산에서는 비가 와도 "눈"이다.
    /// 동굴·방 같은 서브에리어 안은 하늘이 안 보이니 시각만 보이고(날씨 칸·알림 없음), <b>나의 섬은 하늘 아래다</b> —
    /// 섬 하늘도 낮·밤·날씨를 따르므로(<c>SubAreaEnvironment</c>) 칩과 알림이 그대로 뜨고, 지역이 없어 세계 날씨를 쓴다
    /// (<see cref="WorldClockRules.SkyRegionId"/>).
    ///
    /// <b>자리: 우측 열의 맨 아래.</b> 데스크톱은 우상단 포획 아이템 패널(<see cref="KeyGuideHUD.CaptureItemsRect"/>) 아래,
    /// 모바일은 우상단 단축 바(<see cref="QuickAccessBarUI.ShortcutBarRect"/>) 아래다. 좌측은 상태 패널·미니맵·퀘스트 칩,
    /// 상단 가운데는 리전 배너·내기 점수판·코치 배너·소식 카드 사다리, 좌하단은 가상 조이스틱, 우하단은 잡기 버튼이라 비는 곳이 이 열뿐이다.
    /// 세로 모바일 섬에서는 단축 바 아래를 섬 HUD 열이 차지하므로 그 열 아래로 내려간다(<see cref="WorldClockRules.IslandMobileChip"/>).
    /// 가로 모바일 섬(HUD가 단축 바 왼쪽 두 칸 판)과 데스크톱 섬(HUD가 하단 가운데 줄)은 필드와 같은 자리다.
    ///
    /// <b>비모달</b>이라 칩과 알림의 자리를 <see cref="FieldHudInput"/>에 등록한다 — 안 하면 칩을 누른 탭이 월드 클릭-이동으로 새어
    /// 캐릭터가 걸어간다. 판정(시각 표기·알림 문구·좌표)은 <see cref="WorldClockRules"/>에 있다.
    /// </summary>
    public class WorldClockHUD : MonoBehaviour
    {
        private const float NoticeSeconds = 3f;
        private const float NoticeFadeIn = 0.25f;
        private const float NoticeFadeOut = 0.4f;
        private const float NoticeSlide = 36f;

        [SerializeField] private WorldStateProvider worldState;
        [SerializeField] private RegionManager regionManager;
        [SerializeField] private PlayerMovement playerMovement;
        // 섬에서 남의 섬을 구경 중인지만 읽는다(모바일 섬 HUD 판의 높이가 달라 칩 자리가 바뀐다).
        [SerializeField] private IslandWorldBuilder islandWorld;
        private bool islandWorldSearched;

        // 실제로 구독해 둔 대상 — 해지는 이 참조로 한다(worldState가 바뀌어도 옛 대상에서 정확히 뗀다).
        private GameClock subscribedClock;
        private WeatherSystem subscribedWeather;
        private DayPhase lastPhase;

        private string noticeText = string.Empty;
        private Color noticeAccent;
        private float noticeTimer;
        // 날씨 알림이 말한 "그 자리에서 보이던 날씨" — 다른 리전·섬으로 옮겨 보이는 날씨가 달라지면 알림을 거둔다.
        private bool noticeAboutPhase;
        private WeatherType noticeWeather;

        // 시각 라벨은 10분 칸이 바뀔 때만 다시 만든다(현실 5초마다) — OnGUI 매 패스 문자열 할당 방지.
        private string timeLabel = string.Empty;
        private int timeLabelKey = -1;

        private GUIStyle timeStyle;
        private GUIStyle weatherStyle;
        private GUIStyle noticeStyle;
        private bool stylesReady;

        /// <summary>Bootstrap이 부른다. 구독까지 여기서 한다 — AddComponent가 부르는 OnEnable 시점엔 시계가 아직 없다.</summary>
        public void AutoWire(WorldStateProvider provider, RegionManager region, PlayerMovement movement)
        {
            if (worldState == null) worldState = provider;
            if (regionManager == null) regionManager = region;
            if (playerMovement == null) playerMovement = movement;
            SubscribeEvents();
        }

        /// <summary>
        /// 섬 월드 — 남의 섬 구경 중인지 보려고. 안 불러도 섬에 처음 들어갈 때 한 번 찾는다(그 뒤로는 찾지 않는다).
        /// </summary>
        public void AutoWire(IslandWorldBuilder world)
        {
            if (islandWorld == null) islandWorld = world;
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            if (subscribedClock != null) subscribedClock.DayPhaseChanged -= OnDayPhaseChanged;
            if (subscribedWeather != null) subscribedWeather.WeatherChanged -= OnWeatherChanged;
            subscribedClock = null;
            subscribedWeather = null;
            noticeTimer = 0f;
        }

        // `-=` 뒤 `+=`라 AutoWire와 OnEnable이 둘 다 불러도 중복 구독이 되지 않는다.
        private void SubscribeEvents()
        {
            if (subscribedClock != null) subscribedClock.DayPhaseChanged -= OnDayPhaseChanged;
            if (subscribedWeather != null) subscribedWeather.WeatherChanged -= OnWeatherChanged;

            subscribedClock = worldState != null ? worldState.Clock : null;
            subscribedWeather = worldState != null ? worldState.Weather : null;

            if (subscribedClock != null)
            {
                subscribedClock.DayPhaseChanged += OnDayPhaseChanged;
                lastPhase = subscribedClock.GetDayPhase();   // 구독 전에 지나간 변화는 알리지 않는다
            }
            if (subscribedWeather != null) subscribedWeather.WeatherChanged += OnWeatherChanged;
        }

        // ── 읽기 ──

        private GameClock Clock => worldState != null ? worldState.Clock : null;

        private WeatherType GlobalWeather
        {
            get
            {
                WeatherSystem weather = worldState != null ? worldState.Weather : null;
                return weather != null ? weather.CurrentWeather : WeatherType.Clear;
            }
        }

        private string CurrentRegionId
        {
            get
            {
                RegionData region = regionManager != null ? regionManager.CurrentRegion : null;
                return region != null ? region.regionId : null;
            }
        }

        private SubAreaData CurrentSubArea => regionManager != null ? regionManager.CurrentSubArea : null;

        // 동굴·방 — 하늘이 안 보인다. 나의 섬(분리 구역)은 하늘 아래라 여기 들지 않는다.
        private bool Indoors
        {
            get
            {
                SubAreaData area = CurrentSubArea;
                return area != null && !area.detached;
            }
        }

        private bool OnIsland
        {
            get
            {
                SubAreaData area = CurrentSubArea;
                return area != null && area.detached;
            }
        }

        // 날씨를 읽을 지역 — 섬(서브에리어)이면 null(세계 날씨). 섬에 있는 동안 CurrentRegion은 떠나온 리전에 붙어 있다.
        private string SkyRegionId => WorldClockRules.SkyRegionId(CurrentRegionId, CurrentSubArea != null);

        private bool VisitingIsland
        {
            get
            {
                if (islandWorld == null && !islandWorldSearched)
                {
                    islandWorldSearched = true;   // 배선이 안 됐을 때의 대비 — 한 번만 찾는다(없는 씬에서 매 프레임 훑지 않게)
                    islandWorld = FindFirstObjectByType<IslandWorldBuilder>();
                }
                return islandWorld != null && islandWorld.IsVisiting;
            }
        }

        // 꿈 연출·메뉴·대사·전투·포획이 화면을 덮었거나 조작이 묶였다 — 그리지 않고 알림 시간도 흐르지 않는다.
        private bool Covered =>
            DreamPrologueState.Active
            || ModalUIRegistry.IsAnyOpen()
            || (playerMovement != null && playerMovement.IsFrozen);

        // ── 변화 알림 ──

        private void OnDayPhaseChanged(DayPhase phase)
        {
            WeatherType weather = GlobalWeather;
            RaiseNotice(new SkyState(lastPhase, weather), new SkyState(phase, weather));
            lastPhase = phase;
        }

        private void OnWeatherChanged(WeatherType previous, WeatherType next)
        {
            DayPhase phase = Clock != null ? Clock.GetDayPhase() : lastPhase;
            RaiseNotice(new SkyState(phase, previous), new SkyState(phase, next));
        }

        // 연달아 오면 쌓지 않고 최신 하나로 바꿔 처음부터 다시 보여 준다.
        private void RaiseNotice(SkyState before, SkyState after)
        {
            if (DreamPrologueState.Active) return;   // 꿈 밖의 하늘이 꿈속에 비치지 않게
            ClockNotice notice = WorldClockRules.ChooseNotice(before, after, SkyRegionId, Indoors);
            if (!notice.HasValue) return;

            noticeText = notice.Text;
            noticeAboutPhase = notice.AboutPhase;
            noticeWeather = notice.Weather;
            noticeAccent = notice.AboutPhase ? PhaseAccent(notice.Phase) : WeatherAccent(notice.Weather);
            noticeTimer = NoticeSeconds;
        }

        private void Update()
        {
            if (noticeTimer <= 0f) return;
            // 알림은 열린 하늘 아래 얘기다 — 동굴·방으로 들어가면 거둔다(섬은 하늘 아래라 그대로 둔다).
            if (Indoors) { noticeTimer = 0f; return; }
            // 날씨 알림은 그 자리에서 보이던 날씨였다 — 옮겨 간 곳(다른 리전·섬)에서 보이는 날씨가 다르면 거둔다
            // (설산의 "눈이 내리기 시작합니다"가 비 오는 섬에 뜨지 않게). 시간대는 어디서나 같아 그대로 둔다.
            if (!noticeAboutPhase && WeatherForecast.EffectiveIn(GlobalWeather, SkyRegionId) != noticeWeather)
            {
                noticeTimer = 0f;
                return;
            }
            if (Covered) return;   // 가려진 동안은 시간을 태우지 않는다(창을 닫았을 때 이미 사라진 뒤면 못 본다)
            noticeTimer -= Time.unscaledDeltaTime;
        }

        // ── 그리기 ──

        private void OnGUI()
        {
            if (Clock == null || Covered) return;

            UIScale.Begin();
            EnsureStyles();

            bool mobile = UIScale.IsMobileLayout;
            Rect chip = ChipRect(mobile);

            // 버튼은 없지만 불투명 패널이다 — 안 등록하면 칩을 누른 탭이 클릭-이동으로 새어 캐릭터가 걸어간다.
            FieldHudInput.RegisterBlockingRect(chip);
            DrawChip(chip, mobile);
            if (noticeTimer > 0f) DrawNotice(chip, mobile);

            GUI.color = Color.white;
            UIScale.End();
        }

        // 필드는 우측 열의 맨 아래(데스크톱: 포획 아이템 패널 아래 · 모바일: 단축 바 아래). 세로 모바일 섬은 단축 바 아래를
        // 섬 HUD 열이 차지하므로 그 열 아래로 내려간다. 가로 모바일 섬(HUD가 단축 바 왼쪽)과 데스크톱 섬(HUD가 하단 가운데)은
        // 필드와 같은 자리다.
        private Rect ChipRect(bool mobile)
        {
            HudFrame frame = HudFrame.Current;
            bool island = OnIsland;
            // 남의 섬 여부는 세로 모바일 섬에서만 자리를 바꾼다 — 그때만 찾는다.
            bool visiting = island && frame.Mobile && frame.Portrait && VisitingIsland;
            return WorldClockRules.Chip(frame, island, visiting);
        }

        private void EnsureStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            UITheme t = UITheme.Instance;

            timeStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
            timeStyle.normal.textColor = t.textPrimary;

            weatherStyle = new GUIStyle(timeStyle);   // 색은 날씨마다 매 그리기에 정한다

            noticeStyle = new GUIStyle(GUI.skin.label)
            { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            noticeStyle.normal.textColor = t.textPrimary;
        }

        private void DrawChip(Rect chip, bool mobile)
        {
            UITheme t = UITheme.Instance;
            GameClock clock = Clock;
            float hour = clock.GetHourFloat();
            DayPhase phase = clock.GetDayPhase();
            bool indoors = Indoors;
            // 동굴·방은 하늘이 안 보인다 — 날씨를 말하지 않고, 아이콘은 시간대의 해·달만 쓴다. 섬은 세계 날씨다.
            WeatherType weather = indoors ? WeatherType.Clear : WeatherForecast.EffectiveIn(GlobalWeather, SkyRegionId);

            int step = WorldClockRules.ClockStepIndex(hour);
            if (step != timeLabelKey)
            {
                timeLabelKey = step;
                timeLabel = WorldClockRules.TimeLabel(hour);
            }

            UISurface.HudCard(chip);
            // 위쪽 액센트 줄 — 시간대 색. 얇아서 각진 채로 두고 긴 축을 반경만큼 물린다(ui-layout.md).
            UISurface.Flat(new Rect(chip.x + UITheme.Radius.Card, chip.y + 3f,
                chip.width - UITheme.Radius.Card * 2f, 3f), PhaseAccent(phase));

            float pad = UITheme.Space.M;
            float iconSize = chip.height - 20f;
            Rect icon = new Rect(chip.x + pad, chip.y + (chip.height - iconSize) * 0.5f + 1f, iconSize, iconSize);
            DrawSkyIcon(icon, phase, weather);

            float textX = icon.xMax + UITheme.Space.S;
            float textRight = chip.xMax - pad;
            float textY = chip.y + 8f;
            float textH = chip.height - 14f;
            int fontSize = mobile ? 26 : 28;
            timeStyle.fontSize = fontSize;
            weatherStyle.fontSize = fontSize;

            GUI.color = Color.white;
            if (indoors)
            {
                UIHelper.LabelFit(new Rect(textX, textY, textRight - textX, textH), timeLabel, timeStyle);
                return;
            }

            float timeW = (textRight - textX) * 0.56f;
            UIHelper.LabelFit(new Rect(textX, textY, timeW, textH), timeLabel, timeStyle);

            float dividerX = textX + timeW + UITheme.Space.XS;
            UISurface.Flat(new Rect(dividerX, chip.y + chip.height * 0.28f, 2f, chip.height * 0.44f), t.surfaceBorder);

            float weatherX = dividerX + UITheme.Space.S;
            weatherStyle.normal.textColor = WeatherAccent(weather);
            UIHelper.LabelFit(new Rect(weatherX, textY, textRight - weatherX, textH),
                WeatherForecast.DisplayName(weather), weatherStyle);
        }

        private void DrawNotice(Rect chip, bool mobile)
        {
            float elapsed = NoticeSeconds - noticeTimer;
            float appear = Mathf.Clamp01(elapsed / NoticeFadeIn);
            float alpha = appear * Mathf.Clamp01(noticeTimer / NoticeFadeOut);

            Rect rect = WorldClockRules.NoticeBelow(HudFrame.Current, chip);
            rect.x += (1f - appear) * (1f - appear) * NoticeSlide;   // 오른쪽 가장자리에서 미끄러져 들어온다
            FieldHudInput.RegisterBlockingRect(rect);

            // 알파는 GUI.color로만 곱한다(글자 색에 또 굽지 않는다 — 제곱으로 어두워진다).
            GUI.color = new Color(1f, 1f, 1f, alpha);
            UISurface.HudCard(rect);
            UISurface.Flat(new Rect(rect.x + UITheme.Radius.Card, rect.y + 3f,
                rect.width - UITheme.Radius.Card * 2f, 3f), noticeAccent);

            noticeStyle.fontSize = mobile ? 24 : 26;
            float pad = UITheme.Space.M;
            UIHelper.LabelFit(new Rect(rect.x + pad, rect.y + 10f, rect.width - pad * 2f, rect.height - 16f),
                noticeText, noticeStyle);
            GUI.color = Color.white;
        }

        // ── 색 — 전부 UITheme 토큰에서 파생한다 ──

        private static Color PhaseAccent(DayPhase phase)
        {
            UITheme t = UITheme.Instance;
            switch (phase)
            {
                case DayPhase.Morning: return t.accentAmber;
                case DayPhase.Day: return Color.Lerp(t.accentAmber, t.textPrimary, 0.3f);
                case DayPhase.Evening: return t.accentCoral;
                default: return t.itemRare;
            }
        }

        private static Color WeatherAccent(WeatherType weather)
        {
            UITheme t = UITheme.Instance;
            switch (weather)
            {
                case WeatherType.Rain: return t.itemRare;
                case WeatherType.Snow: return t.textPrimary;
                case WeatherType.Fog: return t.textSecondary;
                case WeatherType.Wind: return t.accentMint;
                default: return t.accentAmber;
            }
        }

        // ── 아이콘 — UIShapes의 원과 UISurface의 막대만으로 그린다(회전 없음: UIScale≠1이면 피벗이 어긋난다) ──
        // 좌표는 전부 정사각 상자(box) 안의 0~1 비율이다.

        private static void DrawSkyIcon(Rect box, DayPhase phase, WeatherType weather)
        {
            UITheme t = UITheme.Instance;
            Color cloud = Color.Lerp(t.textSecondary, t.textPrimary, 0.4f);
            switch (weather)
            {
                case WeatherType.Rain:
                    DrawCloud(box, 0f, cloud);
                    Drop(box, 0.30f, 0.70f, t.itemRare);
                    Drop(box, 0.50f, 0.78f, t.itemRare);
                    Drop(box, 0.70f, 0.70f, t.itemRare);
                    break;
                case WeatherType.Snow:
                    DrawCloud(box, 0f, cloud);
                    Dot(box, 0.30f, 0.78f, 0.10f, t.textPrimary);
                    Dot(box, 0.50f, 0.89f, 0.10f, t.textPrimary);
                    Dot(box, 0.70f, 0.78f, 0.10f, t.textPrimary);
                    break;
                case WeatherType.Fog:
                {
                    Color fog = Color.Lerp(cloud, t.textMuted, 0.5f);
                    DrawCloud(box, -0.04f, fog);
                    Bar(box, 0.14f, 0.70f, 0.62f, 0.07f, fog);
                    Bar(box, 0.26f, 0.84f, 0.62f, 0.07f, fog);
                    break;
                }
                case WeatherType.Wind:
                    Bar(box, 0.08f, 0.28f, 0.62f, 0.08f, t.accentMint);
                    Bar(box, 0.20f, 0.50f, 0.72f, 0.08f, t.accentMint);
                    Bar(box, 0.08f, 0.72f, 0.48f, 0.08f, t.accentMint);
                    break;
                default:
                    switch (phase)
                    {
                        case DayPhase.Morning: DrawHorizonSun(box, t.accentAmber); break;
                        case DayPhase.Evening: DrawHorizonSun(box, t.accentCoral); break;
                        case DayPhase.Day: DrawSun(box, t.accentAmber); break;
                        default: DrawMoon(box, t); break;
                    }
                    break;
            }
        }

        private static void Dot(Rect box, float cx, float cy, float diameter, Color color)
        {
            float d = diameter * box.width;
            UIShapes.Ellipse(new Rect(box.x + cx * box.width - d * 0.5f, box.y + cy * box.height - d * 0.5f, d, d), color);
        }

        private static void Drop(Rect box, float cx, float top, Color color)
        {
            UIShapes.Ellipse(new Rect(box.x + (cx - 0.035f) * box.width, box.y + top * box.height,
                0.07f * box.width, 0.16f * box.height), color);
        }

        // 가로 막대 — 몸통은 각지게, 양 끝은 원으로 덮어 둥글린다.
        private static void Bar(Rect box, float x, float y, float width, float height, Color color)
        {
            float th = Mathf.Max(2f, height * box.height);
            Rect r = new Rect(box.x + x * box.width, box.y + y * box.height, width * box.width, th);
            UIShapes.Ellipse(new Rect(r.x, r.y, th, th), color);
            UIShapes.Ellipse(new Rect(r.xMax - th, r.y, th, th), color);
            UISurface.Flat(new Rect(r.x + th * 0.5f, r.y, Mathf.Max(1f, r.width - th), th), color);
        }

        private static void DrawCloud(Rect box, float dy, Color color)
        {
            Dot(box, 0.34f, 0.38f + dy, 0.30f, color);
            Dot(box, 0.56f, 0.30f + dy, 0.40f, color);
            Dot(box, 0.74f, 0.42f + dy, 0.26f, color);
            UIShapes.Ellipse(new Rect(box.x + 0.12f * box.width, box.y + (0.36f + dy) * box.height,
                0.76f * box.width, 0.24f * box.height), color);
        }

        private static void DrawSun(Rect box, Color color)
        {
            Dot(box, 0.5f, 0.5f, 0.44f, color);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                Dot(box, 0.5f + Mathf.Cos(a) * 0.38f, 0.5f + Mathf.Sin(a) * 0.38f, 0.09f, color);
            }
        }

        // 지평선 위로 반쯤 뜬 해 — 그룹 클립으로 원의 아랫부분을 잘라 낸다(마스크용 배경색을 따로 칠하지 않는다).
        private static void DrawHorizonSun(Rect box, Color color)
        {
            GUI.BeginGroup(new Rect(box.x, box.y, box.width, box.height * 0.62f));
            Rect local = new Rect(0f, 0f, box.width, box.height);
            Dot(local, 0.5f, 0.62f, 0.5f, color);
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.PI + (i + 1) * Mathf.PI / 6f;   // 210°~330° — 위쪽 부채꼴만
                Dot(local, 0.5f + Mathf.Cos(a) * 0.40f, 0.62f + Mathf.Sin(a) * 0.40f, 0.09f, color);
            }
            GUI.EndGroup();
            UISurface.Flat(new Rect(box.x + box.width * 0.06f, box.y + box.height * 0.66f,
                box.width * 0.88f, Mathf.Max(2f, box.height * 0.07f)), color);
        }

        // 달 — 이지러진 모양은 배경색으로 덮어야 해서(반투명 카드라 색이 안 맞는다) 분화구 있는 보름달로 그린다.
        private static void DrawMoon(Rect box, UITheme t)
        {
            Color moon = Color.Lerp(t.textPrimary, t.accentAmber, 0.2f);
            Dot(box, 0.44f, 0.54f, 0.56f, moon);
            Dot(box, 0.34f, 0.44f, 0.12f, t.textMuted);
            Dot(box, 0.52f, 0.64f, 0.09f, t.textMuted);
            Dot(box, 0.56f, 0.42f, 0.07f, t.textMuted);
            Dot(box, 0.80f, 0.20f, 0.10f, t.textPrimary);
            Dot(box, 0.90f, 0.46f, 0.06f, t.textPrimary);
            Dot(box, 0.76f, 0.84f, 0.07f, t.textPrimary);
        }
    }
}
