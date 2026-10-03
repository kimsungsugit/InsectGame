using System.Globalization;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.UI
{
    /// <summary>하늘의 상태 한 장 — 시간대와 <b>세계</b> 날씨. 지역에 따라 다르게 보이는 것은 판정이 지역을 받아 입힌다.</summary>
    public readonly struct SkyState
    {
        public readonly DayPhase Phase;
        public readonly WeatherType Weather;

        public SkyState(DayPhase phase, WeatherType weather)
        {
            Phase = phase;
            Weather = weather;
        }
    }

    /// <summary>
    /// 알림 한 줄. <see cref="HasValue"/>가 거짓이면 알릴 변화가 없다(지역 기준으로 화면에 보이는 것이 그대로다).
    /// 색을 고르는 쪽(HUD)이 읽도록 <b>무엇에 대한 알림인지</b>(시간대/날씨)와 새 값을 함께 든다.
    /// </summary>
    public readonly struct ClockNotice
    {
        public readonly string Text;
        public readonly bool AboutPhase;
        public readonly DayPhase Phase;
        /// <summary>이 지역에서 보이는 새 날씨(<see cref="WeatherForecast.EffectiveIn"/> 적용 후).</summary>
        public readonly WeatherType Weather;

        public ClockNotice(string text, bool aboutPhase, DayPhase phase, WeatherType weather)
        {
            Text = text;
            AboutPhase = aboutPhase;
            Phase = phase;
            Weather = weather;
        }

        public bool HasValue => !string.IsNullOrEmpty(Text);
    }

    /// <summary>
    /// <see cref="WorldClockHUD"/>의 <b>순수 규칙</b> — 시각 표기, 알림 문구 고르기, 칩·알림의 자리.
    /// 그리기는 테스트할 수 없으니(IMGUI) 판정과 좌표 계산을 여기로 뺐다.
    ///
    /// <b>시각은 10분 단위로 <i>내림</i>한다.</b> 게임 하루가 12분이라 게임 1분이 현실 0.5초다 — 분 단위로 그리면 숫자가
    /// 1초에 두 번 바뀌어 읽을 수 없고, 반올림하면 20:55가 "21:00"으로 보여 시간대 이름("저녁")과 어긋난다.
    /// 내림이면 시간대 경계(정시)에서만 이름과 시각이 함께 바뀐다.
    /// </summary>
    public static class WorldClockRules
    {
        public const int ClockStepMinutes = 10;
        public const int ClockStepsPerDay = 24 * 60 / ClockStepMinutes;

        // ── 시각 표기 ──

        /// <summary>시각(0~24, 범위 밖은 감는다)이 하루 144칸(10분) 중 몇 번째 칸인가 — 라벨 캐시 키로도 쓴다.</summary>
        public static int ClockStepIndex(float hour)
        {
            float h = Mathf.Repeat(hour, 24f);
            int minutes = Mathf.Clamp(Mathf.FloorToInt(h * 60f), 0, 24 * 60 - 1);
            return minutes / ClockStepMinutes;
        }

        /// <summary>"22:10" — 24시간제, 두 자리씩. 24:00은 나오지 않는다(자정은 00:00).</summary>
        public static string FormatClock(float hour)
        {
            int minutes = ClockStepIndex(hour) * ClockStepMinutes;
            return (minutes / 60).ToString("00", CultureInfo.InvariantCulture)
                + ":" + (minutes % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>"밤 22:10" — 시간대 이름은 시계와 같은 식(<see cref="GameClock.PhaseOfHour"/>)으로 정한다.</summary>
        public static string TimeLabel(float hour)
        {
            DayPhase phase = GameClock.PhaseOfHour(Mathf.Repeat(hour, 24f));
            return WeatherForecast.DisplayName(phase) + " " + FormatClock(hour);
        }

        // ── 어느 하늘인가 ──

        /// <summary>
        /// 날씨를 읽을 지역 ID — 서브에리어(섬 포함)에 있으면 <c>null</c>(세계 날씨 그대로). 섬에 있는 동안
        /// <c>RegionManager.CurrentRegion</c>은 떠나온 리전에 붙어 있어서, 그걸 읽으면 설산에서 섬으로 건너간 사람에게 "눈"이 보인다.
        /// 전투 보정(<c>BattleEnvironment.WeatherRegionId</c>)과 같은 기준이다 — 전투 칩과 필드 칩의 날씨가 갈리지 않는다.
        /// </summary>
        public static string SkyRegionId(string currentRegionId, bool inSubArea)
        {
            return inSubArea ? null : currentRegionId;
        }

        // ── 변화 알림 ──

        /// <summary>
        /// 이전 → 새 하늘에서 알릴 한 줄. <b>화면에 보이는 것이 안 바뀌었으면 알리지 않는다</b> —
        /// 설산에서 비가 눈으로 바뀌어도(둘 다 "눈") 사막에서 눈이 센바람으로 바뀌어도(둘 다 "센바람") 지역 기준으로는 그대로다.
        /// 서브에리어 안(<paramref name="indoors"/>)은 하늘이 안 보이니 알리지 않는다.
        /// 시간대와 날씨가 한꺼번에 바뀌면 시간대가 이긴다(야행성 곤충 안내가 날씨보다 중요하다).
        /// </summary>
        public static ClockNotice ChooseNotice(SkyState previous, SkyState current, string regionId, bool indoors)
        {
            if (indoors) return default;

            WeatherType after = WeatherForecast.EffectiveIn(current.Weather, regionId);
            if (previous.Phase != current.Phase)
                return new ClockNotice(PhaseNotice(current.Phase), true, current.Phase, after);

            WeatherType before = WeatherForecast.EffectiveIn(previous.Weather, regionId);
            if (before == after) return default;
            return new ClockNotice(WeatherNotice(before, after), false, current.Phase, after);
        }

        public static string PhaseNotice(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Morning: return "아침이 밝았습니다";
                case DayPhase.Day: return "낮이 되었습니다";
                case DayPhase.Evening: return "해가 저물어 갑니다";
                default: return "밤이 되었습니다 — 야행성 곤충이 깨어납니다";
            }
        }

        /// <summary>날씨가 <paramref name="before"/>에서 <paramref name="after"/>로 바뀐 것을 말한다(둘은 달라야 한다).</summary>
        public static string WeatherNotice(WeatherType before, WeatherType after)
        {
            switch (after)
            {
                case WeatherType.Rain: return "비가 내리기 시작합니다";
                case WeatherType.Snow: return "눈이 내리기 시작합니다";
                case WeatherType.Fog: return "안개가 끼기 시작합니다";
                case WeatherType.Wind: return "센바람이 불기 시작합니다";
                default:
                    // 맑아졌다 — 무엇이 걷혔는지로 말한다.
                    switch (before)
                    {
                        case WeatherType.Rain: return "비가 그쳤습니다";
                        case WeatherType.Snow: return "눈이 그쳤습니다";
                        case WeatherType.Fog: return "안개가 걷혔습니다";
                        case WeatherType.Wind: return "바람이 잦아들었습니다";
                        default: return "하늘이 맑아졌습니다";
                    }
            }
        }

        // ── 배치 ──

        public const float DesktopChipHeight = 64f;
        public const float MobileChipHeight = 60f;
        public const float NoticeHeight = 72f;
        public const float DesktopNoticeWidth = 540f;
        public const float MobileNoticeWidth = 480f;

        /// <summary>
        /// 칩은 우측 열의 맨 아래에 붙는다 — 데스크톱은 우상단 포획 아이템 패널 아래, 모바일은 우상단 단축 바 아래.
        /// 폭과 x를 <paramref name="anchor"/>에 맞춰 위 패널과 한 줄로 선다.
        /// </summary>
        public static Rect ChipBelow(Rect anchor, bool mobile)
        {
            return new Rect(anchor.x, anchor.yMax + UITheme.Space.S, anchor.width,
                mobile ? MobileChipHeight : DesktopChipHeight);
        }

        /// <summary>알림 카드 — 칩 바로 아래, 칩과 오른쪽 끝을 맞춰 왼쪽으로 펼친다. <paramref name="availableWidth"/>는 칩 오른쪽 끝에서 왼쪽 안전 가장자리까지.</summary>
        public static Rect NoticeBelow(Rect chip, bool mobile, float availableWidth)
        {
            float w = Mathf.Min(mobile ? MobileNoticeWidth : DesktopNoticeWidth, Mathf.Max(1f, availableWidth));
            return new Rect(chip.xMax - w, chip.yMax + UITheme.Space.XS, w, NoticeHeight);
        }

        /// <summary>
        /// 나의 섬(모바일)의 칩 자리. <paramref name="islandPanel"/>은 지금 그려지는 섬 HUD 판(<see cref="IslandHudLayout.Panel"/>).
        ///
        /// <b>세로 화면: 판 바로 아래</b> — 섬 HUD 열이 단축 바 아래(필드 칩 자리)를 차지하므로 그 아래로 내려간다. "우측 열의 맨 아래"라는
        /// 필드 규칙 그대로다. 그 아래로 화면 59% 줄(안내 배너·소식 카드)까지 비어 있고, 왼쪽은 미니맵·퀘스트 칩, 우하단은 잡기 버튼이다.
        /// <b>가로 화면: 필드와 같은 자리(단축 바 아래).</b> 가로에서는 섬 HUD가 단축 바 <i>왼쪽</i> 두 칸 판이라
        /// (<see cref="IslandHudForm.Grid"/>) 단축 바 아래가 비어 있다. 판 아래로 내리면 화면 아래쪽 잡기 버튼에 가까워진다.
        /// </summary>
        public static Rect IslandMobileChip(Rect quick, Rect islandPanel, bool portrait)
        {
            return portrait
                ? new Rect(islandPanel.x, islandPanel.yMax + UITheme.Space.S, islandPanel.width, MobileChipHeight)
                : ChipBelow(quick, true);
        }

        /// <summary>필드의 칩 자리 — 데스크톱은 포획 아이템 패널 아래, 모바일은 단축 바 아래.</summary>
        public static Rect FieldChip(HudFrame f)
        {
            return f.Mobile
                ? ChipBelow(QuickAccessBarUI.ShortcutBarRectFor(f), true)
                : ChipBelow(KeyGuideHUD.CaptureItemsRectFor(f), false);
        }

        /// <summary>
        /// 지금 칩 자리 — 필드는 <see cref="FieldChip"/>. 세로 모바일 섬은 단축 바 아래를 섬 HUD 열이 차지하므로 그 열 아래
        /// (<see cref="IslandMobileChip"/>, 남의 섬이면 짧은 판 아래). 가로 모바일 섬과 데스크톱 섬은 필드와 같다.
        /// </summary>
        public static Rect Chip(HudFrame f, bool onIsland, bool visiting)
        {
            if (!onIsland || !f.Mobile || !f.Portrait) return FieldChip(f);
            Rect quick = QuickAccessBarUI.ShortcutBarRectFor(f);
            Rect panel = IslandHudLayout.Panel(IslandHudForm.Column, quick, visiting, f.ContentHeight);
            return IslandMobileChip(quick, panel, true);
        }

        /// <summary>알림 카드 — 칩 바로 아래, 왼쪽 안전 가장자리까지 펼칠 수 있다.</summary>
        public static Rect NoticeBelow(HudFrame f, Rect chip)
        {
            return NoticeBelow(chip, f.Mobile, chip.xMax - f.ContentLeft);
        }
    }
}
