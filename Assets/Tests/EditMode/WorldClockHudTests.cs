#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using InsectGame.Battle;
using InsectGame.Core;
using InsectGame.Data;
using InsectGame.UI;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 시각·날씨 칩(<see cref="WorldClockHUD"/>)의 순수 규칙 — 시각 표기, 알림 문구 고르기, 칩·알림의 자리(필드·나의 섬),
    /// 그리고 같은 하늘을 전투 화면에 옮긴 낮·밤·날씨 보정 칩(<see cref="BattleHudStack"/>)의 자리·문구.
    /// 그리기(IMGUI)는 검증할 수 없으니 판정만 고정한다. 화면은 QA 빌드로 눈으로 본다.
    /// </summary>
    [TestFixture]
    public class WorldClockHudTests
    {
        private static readonly string[] Regions = { null, "meadow", "pond", "frostline", "mountain", "dunes", "emberfall" };

        private static WeatherType[] AllWeathers => (WeatherType[])Enum.GetValues(typeof(WeatherType));
        private static DayPhase[] AllPhases => (DayPhase[])Enum.GetValues(typeof(DayPhase));

        private static int ToMinutes(string hhmm)
        {
            Assert.IsTrue(Regex.IsMatch(hhmm, @"^([01]\d|2[0-3]):[0-5]0$"), "형식이 어긋났다: " + hhmm);
            return int.Parse(hhmm.Substring(0, 2)) * 60 + int.Parse(hhmm.Substring(3, 2));
        }

        // ── 시각 표기 ──

        [TestCase(0f, "00:00")]
        [TestCase(6f, "06:00")]
        [TestCase(11f, "11:00")]
        [TestCase(12f, "12:00")]
        [TestCase(17f, "17:00")]
        [TestCase(21f, "21:00")]
        [TestCase(22.25f, "22:10")]   // 22:15 → 10분 칸으로 내림
        [TestCase(22.17f, "22:10")]   // 22:10.2
        [TestCase(22.16f, "22:00")]   // 22:09.6 — 반올림이면 22:10이다. 내림이라 22:00
        [TestCase(9.99f, "09:50")]
        [TestCase(23.99f, "23:50")]
        [TestCase(23.9999f, "23:50")]
        [TestCase(0.16f, "00:00")]
        [TestCase(0.17f, "00:10")]
        [TestCase(24f, "00:00")]      // 하루가 감긴다 — 24:00은 없다
        [TestCase(48.5f, "00:30")]
        [TestCase(-1f, "23:00")]
        public void FormatClock_KnownHours(float hour, string expected)
        {
            Assert.AreEqual(expected, WorldClockRules.FormatClock(hour));
        }

        [Test]
        public void FormatClock_WholeDay_IsWellFormedAndNeverGoesBackwards()
        {
            int previous = -1;
            for (int i = 0; i < 2400; i++)
            {
                int minutes = ToMinutes(WorldClockRules.FormatClock(i * 0.01f));
                Assert.GreaterOrEqual(minutes, previous, $"{i * 0.01f}시에 시각이 뒤로 갔다");
                previous = minutes;
            }
            Assert.AreEqual(23 * 60 + 50, previous, "하루의 마지막 칸은 23:50이다");
        }

        [Test]
        public void ClockStepIndex_CoversTheDayInTenMinuteSteps()
        {
            Assert.AreEqual(144, WorldClockRules.ClockStepsPerDay);
            Assert.AreEqual(0, WorldClockRules.ClockStepIndex(0f));
            Assert.AreEqual(143, WorldClockRules.ClockStepIndex(23.99f));
            Assert.AreEqual(0, WorldClockRules.ClockStepIndex(24f), "정각 24시는 다음 날 0시다");
            Assert.AreEqual(WorldClockRules.ClockStepIndex(5.01f), WorldClockRules.ClockStepIndex(29.01f), "하루 뒤도 같은 칸");
            for (int i = 0; i < 2400; i++)
            {
                int step = WorldClockRules.ClockStepIndex(i * 0.01f);
                Assert.That(step, Is.InRange(0, 143));
            }
        }

        // ── 시간대 이름 ──

        [TestCase(6f, "아침 06:00")]
        [TestCase(10.99f, "아침 10:50")]
        [TestCase(11f, "낮 11:00")]
        [TestCase(16.99f, "낮 16:50")]
        [TestCase(17f, "저녁 17:00")]
        [TestCase(20.99f, "저녁 20:50")]
        [TestCase(21f, "밤 21:00")]
        [TestCase(22.17f, "밤 22:10")]
        [TestCase(0f, "밤 00:00")]
        [TestCase(5.99f, "밤 05:50")]
        public void TimeLabel_NamesThePhaseThenTheTime(float hour, string expected)
        {
            Assert.AreEqual(expected, WorldClockRules.TimeLabel(hour));
        }

        [Test]
        public void TimeLabel_ShownTimeNeverContradictsThePhaseName()
        {
            // 반올림했다면 20:55가 "저녁 21:00"으로 보였을 것이다. 내림이라 이름과 시각은 정시 경계에서 함께 바뀐다.
            for (int i = 0; i < 24 * 60; i++)
            {
                float hour = i / 60f + 0.005f;   // 그 분의 18초 지점
                string[] parts = WorldClockRules.TimeLabel(hour).Split(' ');
                Assert.AreEqual(2, parts.Length);

                int shown = ToMinutes(parts[1]);
                int real = Mathf.FloorToInt(hour * 60f);
                Assert.LessOrEqual(shown, real, parts[1]);
                Assert.Less(real - shown, WorldClockRules.ClockStepMinutes, parts[1]);

                DayPhase realPhase = GameClock.PhaseOfHour(hour);
                Assert.AreEqual(WeatherForecast.DisplayName(realPhase), parts[0]);
                Assert.AreEqual(realPhase, GameClock.PhaseOfHour(shown / 60f), "표시된 시각이 다른 시간대에 속한다: " + parts[1]);
            }
        }

        // ── 알림: 시간대 ──

        [TestCase(DayPhase.Day, DayPhase.Evening, "해가 저물어 갑니다")]
        [TestCase(DayPhase.Evening, DayPhase.Night, "밤이 되었습니다 — 야행성 곤충이 깨어납니다")]
        [TestCase(DayPhase.Night, DayPhase.Morning, "아침이 밝았습니다")]
        public void ChooseNotice_PhaseChange_SaysTheNewPhase(DayPhase from, DayPhase to, string text)
        {
            ClockNotice notice = WorldClockRules.ChooseNotice(
                new SkyState(from, WeatherType.Clear), new SkyState(to, WeatherType.Clear), "meadow", false);
            Assert.IsTrue(notice.HasValue);
            Assert.AreEqual(text, notice.Text);
            Assert.IsTrue(notice.AboutPhase);
            Assert.AreEqual(to, notice.Phase);
        }

        [Test]
        public void ChooseNotice_MorningToDay_SaysDaytime()
        {
            ClockNotice notice = WorldClockRules.ChooseNotice(
                new SkyState(DayPhase.Morning, WeatherType.Clear), new SkyState(DayPhase.Day, WeatherType.Clear), null, false);
            StringAssert.Contains("낮", notice.Text);
        }

        [Test]
        public void ChooseNotice_PhaseChange_IsTheSameInEveryRegion()
        {
            // 시간대는 어느 지역에서나 같다 — 기후는 날씨에만 걸린다.
            foreach (string region in Regions)
            {
                ClockNotice notice = WorldClockRules.ChooseNotice(
                    new SkyState(DayPhase.Day, WeatherType.Rain), new SkyState(DayPhase.Evening, WeatherType.Rain), region, false);
                Assert.AreEqual(WorldClockRules.PhaseNotice(DayPhase.Evening), notice.Text, region ?? "null");
            }
        }

        [Test]
        public void ChooseNotice_PhaseWinsWhenPhaseAndWeatherChangeTogether()
        {
            ClockNotice notice = WorldClockRules.ChooseNotice(
                new SkyState(DayPhase.Day, WeatherType.Clear), new SkyState(DayPhase.Evening, WeatherType.Rain), "meadow", false);
            Assert.IsTrue(notice.AboutPhase);
            Assert.AreEqual(WorldClockRules.PhaseNotice(DayPhase.Evening), notice.Text);
            Assert.AreEqual(WeatherType.Rain, notice.Weather, "색을 고르는 쪽이 새 날씨도 볼 수 있다");
        }

        // ── 알림: 날씨 ──

        [TestCase(WeatherType.Clear, WeatherType.Rain, "meadow", "비가 내리기 시작합니다")]
        [TestCase(WeatherType.Clear, WeatherType.Rain, null, "비가 내리기 시작합니다")]
        [TestCase(WeatherType.Clear, WeatherType.Snow, "meadow", "눈이 내리기 시작합니다")]
        [TestCase(WeatherType.Rain, WeatherType.Fog, "meadow", "안개가 끼기 시작합니다")]
        [TestCase(WeatherType.Fog, WeatherType.Wind, "pond", "센바람이 불기 시작합니다")]
        [TestCase(WeatherType.Rain, WeatherType.Clear, "meadow", "비가 그쳤습니다")]
        [TestCase(WeatherType.Snow, WeatherType.Clear, "meadow", "눈이 그쳤습니다")]
        [TestCase(WeatherType.Fog, WeatherType.Clear, "meadow", "안개가 걷혔습니다")]
        [TestCase(WeatherType.Wind, WeatherType.Clear, "meadow", "바람이 잦아들었습니다")]
        public void ChooseNotice_WeatherChange_SaysWhatHappened(WeatherType from, WeatherType to, string region, string text)
        {
            ClockNotice notice = WorldClockRules.ChooseNotice(
                new SkyState(DayPhase.Day, from), new SkyState(DayPhase.Day, to), region, false);
            Assert.IsTrue(notice.HasValue);
            Assert.AreEqual(text, notice.Text);
            Assert.IsFalse(notice.AboutPhase);
            Assert.AreEqual(to, notice.Weather);
        }

        [TestCase("frostline")]
        [TestCase("mountain")]
        public void ChooseNotice_ColdRegion_RainIsSnow(string region)
        {
            ClockNotice start = WorldClockRules.ChooseNotice(
                new SkyState(DayPhase.Day, WeatherType.Clear), new SkyState(DayPhase.Day, WeatherType.Rain), region, false);
            Assert.AreEqual("눈이 내리기 시작합니다", start.Text, "세계는 비가 와도 이 지역에선 눈이다");
            Assert.AreEqual(WeatherType.Snow, start.Weather);

            ClockNotice stop = WorldClockRules.ChooseNotice(
                new SkyState(DayPhase.Day, WeatherType.Rain), new SkyState(DayPhase.Day, WeatherType.Clear), region, false);
            Assert.AreEqual("눈이 그쳤습니다", stop.Text);
        }

        [TestCase("frostline")]
        [TestCase("mountain")]
        public void ChooseNotice_ColdRegion_RainToSnow_IsNotAChange(string region)
        {
            // 둘 다 "눈"으로 보인다 — 알릴 것이 없다.
            Assert.IsFalse(WorldClockRules.ChooseNotice(
                new SkyState(DayPhase.Day, WeatherType.Rain), new SkyState(DayPhase.Day, WeatherType.Snow), region, false).HasValue);
            Assert.IsFalse(WorldClockRules.ChooseNotice(
                new SkyState(DayPhase.Day, WeatherType.Snow), new SkyState(DayPhase.Day, WeatherType.Rain), region, false).HasValue);
        }

        [TestCase("dunes")]
        [TestCase("emberfall")]
        public void ChooseNotice_WarmRegion_SnowIsWind(string region)
        {
            ClockNotice start = WorldClockRules.ChooseNotice(
                new SkyState(DayPhase.Day, WeatherType.Clear), new SkyState(DayPhase.Day, WeatherType.Snow), region, false);
            Assert.AreEqual("센바람이 불기 시작합니다", start.Text, "눈이 오지 않는 지역이다");
            Assert.AreEqual(WeatherType.Wind, start.Weather);

            // 사막에서 센바람 ↔ 눈은 둘 다 센바람이다.
            Assert.IsFalse(WorldClockRules.ChooseNotice(
                new SkyState(DayPhase.Day, WeatherType.Wind), new SkyState(DayPhase.Day, WeatherType.Snow), region, false).HasValue);
            Assert.IsFalse(WorldClockRules.ChooseNotice(
                new SkyState(DayPhase.Day, WeatherType.Snow), new SkyState(DayPhase.Day, WeatherType.Wind), region, false).HasValue);
        }

        [Test]
        public void ChooseNotice_NoticeAppears_ExactlyWhenTheVisibleWeatherChanges()
        {
            // 모든 지역 × 모든 (이전, 새) 날씨 — 알림이 뜨는 조건은 "지역 기준으로 보이는 날씨가 달라졌다" 하나다.
            foreach (string region in Regions)
                foreach (WeatherType before in AllWeathers)
                    foreach (WeatherType after in AllWeathers)
                    {
                        ClockNotice notice = WorldClockRules.ChooseNotice(
                            new SkyState(DayPhase.Day, before), new SkyState(DayPhase.Day, after), region, false);
                        bool visibleChange = WeatherForecast.EffectiveIn(before, region) != WeatherForecast.EffectiveIn(after, region);
                        Assert.AreEqual(visibleChange, notice.HasValue, $"{region ?? "null"}: {before} → {after}");
                        if (notice.HasValue)
                            Assert.AreEqual(WeatherForecast.EffectiveIn(after, region), notice.Weather);
                    }
        }

        [Test]
        public void ChooseNotice_NothingChanged_ReturnsNone()
        {
            foreach (string region in Regions)
                foreach (DayPhase phase in AllPhases)
                    foreach (WeatherType weather in AllWeathers)
                        Assert.IsFalse(WorldClockRules.ChooseNotice(
                            new SkyState(phase, weather), new SkyState(phase, weather), region, false).HasValue);
        }

        [Test]
        public void ChooseNotice_Indoors_NeverNotifies()
        {
            // 서브에리어(동굴·섬)는 하늘이 안 보인다.
            foreach (string region in Regions)
                foreach (DayPhase before in AllPhases)
                    foreach (DayPhase after in AllPhases)
                        foreach (WeatherType weather in AllWeathers)
                            Assert.IsFalse(WorldClockRules.ChooseNotice(
                                new SkyState(before, WeatherType.Clear), new SkyState(after, weather), region, true).HasValue);
        }

        [Test]
        public void ClockNotice_Default_HasNoValue()
        {
            Assert.IsFalse(default(ClockNotice).HasValue);
            Assert.IsNull(default(ClockNotice).Text);
        }

        [Test]
        public void Notices_AreShortHangulLines_ThatTheImguiFontCanDraw()
        {
            // IMGUI 폰트가 이모지를 못 그린다 — 한글 음절·공백·줄표만 쓴다. 한 줄이다.
            var texts = new System.Collections.Generic.List<string>();
            foreach (DayPhase phase in AllPhases) texts.Add(WorldClockRules.PhaseNotice(phase));
            foreach (WeatherType before in AllWeathers)
                foreach (WeatherType after in AllWeathers)
                    if (before != after) texts.Add(WorldClockRules.WeatherNotice(before, after));

            foreach (string text in texts)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(text));
                Assert.LessOrEqual(text.Length, 30, text);
                foreach (char c in text)
                {
                    bool hangul = c >= '가' && c <= '힣';
                    Assert.IsTrue(hangul || c == ' ' || c == '—', $"'{c}' (U+{(int)c:X4}) in \"{text}\"");
                }
            }
        }

        // ── 배치 ──

        [Test]
        public void ChipBelow_StacksUnderTheAnchor_InTheSameColumn()
        {
            var capturePanel = new Rect(1460f, 32f, 440f, 226f);
            Rect desktop = WorldClockRules.ChipBelow(capturePanel, false);
            Assert.AreEqual(capturePanel.x, desktop.x);
            Assert.AreEqual(capturePanel.width, desktop.width);
            Assert.AreEqual(WorldClockRules.DesktopChipHeight, desktop.height);
            Assert.AreEqual(capturePanel.yMax + UITheme.Space.S, desktop.y, 0.001f);
            Assert.IsFalse(desktop.Overlaps(capturePanel));

            var shortcutBar = new Rect(732f, 58f, 324f, 312f);
            Rect mobile = WorldClockRules.ChipBelow(shortcutBar, true);
            Assert.AreEqual(shortcutBar.xMax, mobile.xMax, 0.001f);
            Assert.AreEqual(WorldClockRules.MobileChipHeight, mobile.height);
            Assert.IsFalse(mobile.Overlaps(shortcutBar));
        }

        [Test]
        public void ChipBelow_RealAnchors_NeverOverlapThePanelAbove()
        {
            // 화면 크기와 무관하게 성립해야 한다 — 실제 앵커(포획 아이템 패널·단축 바)로 잰다.
            Rect capturePanel = KeyGuideHUD.CaptureItemsRect;
            Assert.IsFalse(WorldClockRules.ChipBelow(capturePanel, false).Overlaps(capturePanel));

            Rect bar = QuickAccessBarUI.ShortcutBarRect;
            Assert.IsFalse(WorldClockRules.ChipBelow(bar, true).Overlaps(bar));
        }

        [Test]
        public void NoticeBelow_SitsUnderTheChip_RightAligned_AndStaysInsideTheAvailableWidth()
        {
            var chip = new Rect(1460f, 266f, 440f, 64f);
            Rect desktop = WorldClockRules.NoticeBelow(chip, false, chip.xMax - 24f);
            Assert.AreEqual(chip.xMax, desktop.xMax, 0.001f);
            Assert.GreaterOrEqual(desktop.y, chip.yMax);
            Assert.AreEqual(WorldClockRules.DesktopNoticeWidth, desktop.width);
            Assert.IsFalse(desktop.Overlaps(chip));

            // 좁은 화면 — 왼쪽 안전 가장자리를 넘지 않는다.
            Rect narrow = WorldClockRules.NoticeBelow(chip, false, 300f);
            Assert.AreEqual(300f, narrow.width);
            Assert.AreEqual(chip.xMax, narrow.xMax, 0.001f);

            Rect degenerate = WorldClockRules.NoticeBelow(chip, true, -50f);
            Assert.GreaterOrEqual(degenerate.width, 1f, "폭이 0이하로 내려가지 않는다");
        }

        // ── 어느 하늘인가 — 섬은 세계 날씨 ──

        [Test]
        public void SkyRegionId_OnIslandOrInSubArea_IsTheWorldSky()
        {
            Assert.IsNull(WorldClockRules.SkyRegionId("frostline", true), "섬에서는 떠나온 설산이 아니라 세계 날씨다");
            Assert.AreEqual("frostline", WorldClockRules.SkyRegionId("frostline", false));
            Assert.IsNull(WorldClockRules.SkyRegionId(null, false));
        }

        [TestCase("meadow")]
        [TestCase("frostline")]
        [TestCase("dunes")]
        public void SkyRegionId_AgreesWithTheBattleEnvironment(string regionId)
        {
            // 필드 칩과 전투 칩이 같은 하늘을 말해야 한다 — 갈리면 칩은 "비"인데 전투는 "눈" 성향으로 돈다.
            var region = new RegionData { regionId = regionId };
            var island = new SubAreaData { subAreaId = "player_island", detached = true };
            Assert.AreEqual(BattleEnvironment.WeatherRegionId(region, null), WorldClockRules.SkyRegionId(regionId, false));
            Assert.AreEqual(BattleEnvironment.WeatherRegionId(region, island), WorldClockRules.SkyRegionId(regionId, true));
        }

        [Test]
        public void ChooseNotice_OnIsland_SpeaksOfTheWorldWeather()
        {
            // 설산에서 섬으로 건너왔어도 섬은 세계 날씨다 — 세계가 비면 섬도 "비"다(설산이었다면 "눈").
            string island = WorldClockRules.SkyRegionId("frostline", true);
            ClockNotice notice = WorldClockRules.ChooseNotice(
                new SkyState(DayPhase.Day, WeatherType.Clear), new SkyState(DayPhase.Day, WeatherType.Rain), island, false);
            Assert.AreEqual("비가 내리기 시작합니다", notice.Text);
            Assert.AreEqual(WeatherType.Rain, notice.Weather);

            ClockNotice night = WorldClockRules.ChooseNotice(
                new SkyState(DayPhase.Evening, WeatherType.Clear), new SkyState(DayPhase.Night, WeatherType.Clear), island, false);
            Assert.AreEqual(WorldClockRules.PhaseNotice(DayPhase.Night), night.Text, "섬에서도 밤이 오면 알린다");
        }

        // ── 섬 HUD 판(IslandHudLayout) — 모양 셋 ──

        [TestCase(IslandHudForm.Strip)]
        [TestCase(IslandHudForm.Column)]
        [TestCase(IslandHudForm.Grid)]
        public void IslandHudLayout_Buttons_StayInsideTheirPanel_WithoutOverlap(IslandHudForm form)
        {
            Rect quick = form == IslandHudForm.Strip ? DesktopQuickBar(1920f, 1080f)
                : form == IslandHudForm.Column ? MobileQuickBar(1080f, 57.6f, 0f)
                : MobileQuickBar(1920f, 32.4f, 0f);
            foreach (bool visiting in new[] { false, true })
            {
                Rect panel = IslandHudLayout.Panel(form, quick, visiting, 1015.2f);
                Assert.IsFalse(panel.Overlaps(quick), $"{form} visiting={visiting}: 단축 바와 떨어져 선다");
                int count = visiting ? IslandHudLayout.VisitButtonCount : IslandHudLayout.OwnButtonCount;
                for (int i = 0; i < count; i++)
                {
                    Rect b = IslandButton(panel, form, visiting, i);
                    AssertInside(panel, b);
                    Assert.GreaterOrEqual(b.height, UIScale.MinTouchHeight, $"{form} 버튼 {i}");
                    for (int j = 0; j < i; j++)
                        Assert.IsFalse(b.Overlaps(IslandButton(panel, form, visiting, j)), $"{form} visiting={visiting}: {i}·{j}");
                }
            }

            Rect own = IslandHudLayout.OwnPanel(form, quick, 1015.2f);
            Rect harvest = IslandHudLayout.OwnButton(own, form, IslandHudLayout.HarvestIndex);
            for (int i = 0; i < IslandHudLayout.OwnButtonCount; i++)
                Assert.GreaterOrEqual(harvest.width, IslandHudLayout.OwnButton(own, form, i).width - 0.001f, "수확 칸이 가장 넓다(수확물 숫자)");
            switch (form)
            {
                case IslandHudForm.Strip:
                    Assert.AreEqual(IslandHudLayout.HarvestWidth, harvest.width);
                    Assert.IsFalse(IslandHudLayout.DesktopInfo(own).Overlaps(own), "정보 줄은 버튼 줄 위");
                    break;
                case IslandHudForm.Column:
                    Assert.AreEqual(IslandHudLayout.MobileOwnColumnHeight, own.height, "7줄이 다 들어가는 높이");
                    Assert.AreEqual(quick.x, own.x, 0.001f, "단축 바 아래 한 열");
                    break;
                case IslandHudForm.Grid:
                    // 단축 바 왼쪽에 윗변을 맞추고 높이가 단축 바와 같다 — 둘이 위쪽 한 띠에 나란히 선다.
                    Assert.AreEqual(quick.y, own.y, 0.001f);
                    Assert.AreEqual(quick.height, own.height, 0.001f);
                    Assert.LessOrEqual(own.xMax, quick.x - IslandHudLayout.AnchorGap + 0.001f);
                    Assert.AreEqual(own.width - 16f, harvest.width, 0.001f, "수확은 한 줄을 다 쓴다");
                    break;
            }
        }

        [Test]
        public void IslandMobileChip_PortraitUnderTheColumn_LandscapeAtItsFieldPlace()
        {
            Rect quickP = MobileQuickBar(1080f, 57.6f, 0f);
            Rect column = IslandHudLayout.OwnPanel(IslandHudForm.Column, quickP, 1804.8f);
            Rect chipP = WorldClockRules.IslandMobileChip(quickP, column, true);
            Assert.AreEqual(column.x, chipP.x, 0.001f, "섬 HUD 열과 한 열로 선다");
            Assert.AreEqual(column.width, chipP.width, 0.001f);
            Assert.AreEqual(column.yMax + UITheme.Space.S, chipP.y, 0.001f, "열 바로 아래");

            // 가로 섬은 HUD가 단축 바 왼쪽이라 단축 바 아래가 빈다 — 칩은 필드와 같은 자리다.
            Rect quickL = MobileQuickBar(1920f, 32.4f, 0f);
            Rect grid = IslandHudLayout.OwnPanel(IslandHudForm.Grid, quickL, 1015.2f);
            Assert.AreEqual(WorldClockRules.ChipBelow(quickL, true), WorldClockRules.IslandMobileChip(quickL, grid, false));
        }

        // 나의 섬 화면 전체(섬 HUD·결과 토스트·시각 칩·알림과 남의 자리)의 겹침은 HudOverlapSweepTests가 화면 18장에서 잰다
        // (예전 IslandScreen_* — 결과 토스트는 이제 가운데 무대(HudStage)에 선다).

        // ── 퀘스트 칩·목표 행(QuestChipLayout) — 단축 바·시각 칩·포획 아이템 패널·미니맵·리전 배너·잡기 버튼·조이스틱 자리와 ──
        // 2026-10-03 전엔 데스크톱 16:9(가상 폭 1920)에서 좌하단 칩(x 16~416)이 단축 바(x 360~1560)와 56px 겹쳤고,
        // 목표 행은 칩 아래(안전 영역 밖, 1080 화면에서 y 1054~1111)에 그려져 거의 안 보였다. 가로 모바일에서는 미니맵 아래 목표 행
        // (y 526~583)이 가상 조이스틱의 시작 영역(좌하단 사분면, y 540부터)으로 43px 내려가 있었다.

        // 칩·목표 행·복원 버튼이 다른 필드 HUD와 겹치지 않는지는 HudOverlapSweepTests가 잰다(예전 QuestChip_ExpandedDoneOrCollapsed_ClearOfFieldHud).

        [Test]
        public void QuestChip_Desktop_StopsAtTheBar_AndKeepsFourHundredWhereThereIsRoom()
        {
            // 16:9 — 단축 바 왼쪽 끝까지만(334). 그 전엔 400이라 단축 바와 56px 겹쳤다.
            Rect bar1920 = DesktopBar(1920f, 1080f);
            float w1920 = QuestChipLayout.Width(QuestStackPlace.BottomLeft, 1920f, 0f, 0f, 16f, bar1920);
            Assert.AreEqual(bar1920.x - UITheme.Space.S - 16f, w1920, 0.001f);
            Assert.Less(w1920, QuestChipLayout.DesktopMaxWidth);
            Assert.GreaterOrEqual(w1920, QuestChipLayout.DesktopMinBesideWidth);

            // 21:9 — 자리가 넉넉하면 예전 그대로 400.
            Assert.AreEqual(QuestChipLayout.DesktopMaxWidth, QuestChipLayout.Width(QuestStackPlace.BottomLeft, 2560f, 0f, 0f, 16f, DesktopBar(2560f, 1080f)), 0.001f);

            // 목표 행이 없으면 칩은 예전처럼 바닥에 붙는다 — 있을 때만 그 높이만큼 올라간다.
            float bottom = QuestChipLayout.DesktopBottom(16f, bar1920, 1047.6f);
            Assert.AreEqual(1047.6f, bottom, 0.001f);
            Assert.AreEqual(1047.6f - QuestChipHeight,
                QuestChipLayout.Chip(QuestStackPlace.BottomLeft, 16f, w1920, QuestChipHeight, 0f, 0f, bottom).y, 0.001f);
            Rect lifted = QuestChipLayout.Chip(QuestStackPlace.BottomLeft, 16f, w1920, QuestChipHeight, QuestRowHeight, 0f, bottom);
            Assert.AreEqual(1047.6f, QuestChipLayout.Row(lifted, w1920, QuestRowHeight).yMax, 0.001f, "목표 행이 바닥에 닿는다");

            // 세로 모바일은 바뀌지 않았다 — 미니맵 아래(StackBelowY), 폭 500.
            Rect mobileChip = QuestChipLayout.Chip(QuestStackPlace.UnderMinimap, 16f,
                QuestChipLayout.Width(QuestStackPlace.UnderMinimap, 1080f, 0f, 0f, 16f, Rect.zero),
                QuestChipHeight, QuestRowHeight, QuestChipLayout.MobileTop(QuestStackPlace.UnderMinimap, 57.6f), 0f);
            Assert.AreEqual(16f, mobileChip.x, 0.001f);
            Assert.AreEqual(437.6f, mobileChip.y, 0.001f);
            Assert.AreEqual(QuestChipLayout.MobileMaxWidth, mobileChip.width, 0.001f);
        }

        [Test]
        public void QuestChip_DesktopTooNarrowBesideTheBar_StacksAboveIt()
        {
            // 데스크톱 레이아웃인데 창이 세로에 가깝다(가상 폭 1080) — 단축 바가 안전 폭을 다 쓴다. 칩은 그 위로 올라간다.
            UISafeLayout.SafeBox vertical = UISafeLayout.Compute(1140f, 0f, 0f);
            UISafeLayout.SafeBox horizontal = UISafeLayout.ComputeWithMargin(1080f, 0f, 0f, UISafeLayout.MarginX);
            Rect bar = QuickAccessBarUI.ShortcutBarRectIn(false, horizontal, vertical);
            float width = QuestChipLayout.Width(QuestStackPlace.BottomLeft, 1080f, 0f, 0f, 16f, bar);
            float bottom = QuestChipLayout.DesktopBottom(16f, bar, vertical.End);
            Rect chip = QuestChipLayout.Chip(QuestStackPlace.BottomLeft, 16f, width, QuestChipHeight, QuestRowHeight, 0f, bottom);
            Rect row = QuestChipLayout.Row(chip, width, QuestRowHeight);
            Assert.AreEqual(QuestChipLayout.DesktopMaxWidth, width, 0.001f);
            Assert.IsFalse(chip.Overlaps(bar));
            Assert.IsFalse(row.Overlaps(bar));
            Assert.LessOrEqual(row.yMax, bar.y);
            Assert.GreaterOrEqual(chip.y, vertical.Start);
        }

        [Test]
        public void ShortcutBarRectIn_MatchesThePanelHarness()
        {
            // TopPanel(324, 312, Right) / BottomPanel(1200, 76)와 같은 값 — 순수 계산으로 뗐을 뿐 자리는 그대로다.
            Rect mobile = QuickAccessBarUI.ShortcutBarRectIn(true,
                UISafeLayout.ComputeWithMargin(1080f, 0f, 0f, UISafeLayout.MarginX), UISafeLayout.Compute(1920f, 0f, 0f));
            Assert.AreEqual(732f, mobile.x, 0.001f);
            Assert.AreEqual(57.6f, mobile.y, 0.001f);
            Assert.AreEqual(324f, mobile.width, 0.001f);
            Assert.AreEqual(312f, mobile.height, 0.001f);
            Rect desktop = QuickAccessBarUI.ShortcutBarRectIn(false,
                UISafeLayout.ComputeWithMargin(1920f, 0f, 0f, UISafeLayout.MarginX), UISafeLayout.Compute(1080f, 0f, 0f));
            Assert.AreEqual(360f, desktop.x, 0.001f);
            Assert.AreEqual(1080f - 32.4f - 76f, desktop.y, 0.001f);
            Assert.AreEqual(1200f, desktop.width, 0.001f);
            Assert.AreEqual(76f, desktop.height, 0.001f);
            // 실제 화면에서도 프로퍼티와 같다.
            Assert.AreEqual(QuickAccessBarUI.ShortcutBarRect,
                QuickAccessBarUI.ShortcutBarRectIn(UIScale.IsMobileLayout, UISafeLayout.HorizontalBox, UISafeLayout.VerticalBox));
        }

        [Test]
        public void QuestChip_MobileLandscape_BesideTheMinimap_AboveTheJoystick()
        {
            // 1280×720 → 가상 1920×1080. 미니맵(16~236 × 182~402) 오른쪽, ContentTop+260부터.
            UISafeLayout.SafeBox vertical = UISafeLayout.Compute(1080f, 0f, 0f);
            Rect bar = QuickAccessBarUI.ShortcutBarRectIn(true, UISafeLayout.ComputeWithMargin(1920f, 0f, 0f, UISafeLayout.MarginX), vertical);
            QuestStackPlace place = QuestChipLayout.PlaceFor(true, false);
            Assert.AreEqual(QuestStackPlace.BesideMinimap, place);
            float x = QuestChipLayout.Left(place, 16f);
            float width = QuestChipLayout.Width(place, 1920f, 0f, 0f, 16f, bar);
            Rect chip = QuestChipLayout.Chip(place, x, width, QuestChipHeight, QuestRowHeight, QuestChipLayout.MobileTop(place, vertical.Start), 0f);
            Rect row = QuestChipLayout.Row(chip, width, QuestRowHeight);
            Rect minimap = new Rect(16f, vertical.Start + MinimapUI.TopOffset, MinimapUI.PanelSize, MinimapUI.PanelSize);

            Assert.AreEqual(minimap.xMax + UITheme.Space.S, chip.x, 0.001f, "미니맵 오른쪽");
            Assert.AreEqual(vertical.Start + QuestChipLayout.BesideMinimapTop, chip.y, 0.001f, "위쪽 가운데 줄(리전 배너·내기 점수판) 아래");
            Assert.AreEqual(QuestChipLayout.MobileMaxWidth, width, 0.001f);
            Assert.LessOrEqual(Mathf.Abs(chip.yMax - minimap.yMax), 4f, "칩 아랫변이 미니맵 아랫변과 거의 맞는다");
            Assert.Less(row.yMax, 1080f * 0.5f, "목표 행까지 화면 가운데 줄(조이스틱 시작 영역) 위에서 끝난다");
            Assert.Less(row.xMax, 1920f * 0.5f - 120f, "화면 가운데(캐릭터)와 떨어져 있다");

            // 세로 모바일은 그대로 미니맵 아래다.
            Assert.AreEqual(QuestStackPlace.UnderMinimap, QuestChipLayout.PlaceFor(true, true));
            Assert.AreEqual(QuestStackPlace.BottomLeft, QuestChipLayout.PlaceFor(false, false));
            Assert.AreEqual(QuestChipLayout.MobileTop(QuestStackPlace.UnderMinimap, 57.6f),
                57.6f + MinimapUI.TopOffset + MinimapUI.PanelSize + UITheme.Space.S, 0.001f, "= MinimapUI.StackBelowY");
        }

        // ── 가상 조이스틱 — 필드 HUD 위에서는 시작하지 않는다 ──

        [Test]
        public void JoystickCanBeginAt_OnlyInTheLowerLeftQuadrant_AndNeverOnHud()
        {
            // 화면 좌표는 Y-up 픽셀(Touch.position). 1280×720, 왼쪽 인셋 40·아래 인셋 20.
            const float w = 1280f, h = 720f, left = 40f, bottom = 20f;
            Assert.IsTrue(VirtualJoystickUI.CanBeginAt(new Vector2(500f, 300f), w, h, left, bottom, false), "좌하단, HUD 아님");
            Assert.IsFalse(VirtualJoystickUI.CanBeginAt(new Vector2(500f, 300f), w, h, left, bottom, true),
                "좌하단이라도 HUD(대화·동굴 입구 버튼 등) 위 — 그 버튼과 함께 캐릭터가 움직이면 안 된다");
            // 유휴 힌트 원("여기를 누르면 움직인다") 안은 HUD가 덮어도 시작한다 — 꿈속 섬 안내 카드가 세로 화면에서 이 자리를 덮는다.
            Vector2 hint = VirtualJoystickUI.HintCenter(w, h, left, bottom);
            float r = VirtualJoystickUI.HintRadius(w, h);
            Assert.AreEqual(720f * 0.14f, r, 0.001f, "짧은 변의 14%");
            Assert.AreEqual(left + r * 1.25f, hint.x, 0.001f);
            Assert.AreEqual(bottom + r * 1.25f, hint.y, 0.001f);
            Assert.IsTrue(VirtualJoystickUI.CanBeginAt(hint, w, h, left, bottom, true), "힌트 원 가운데");
            Assert.IsTrue(VirtualJoystickUI.CanBeginAt(hint + new Vector2(r * 0.9f, 0f), w, h, left, bottom, true), "힌트 원 가장자리 안");
            Assert.IsFalse(VirtualJoystickUI.CanBeginAt(hint + new Vector2(r * 1.1f, 0f), w, h, left, bottom, true), "힌트 원 밖");
            Assert.IsFalse(VirtualJoystickUI.CanBeginAt(new Vector2(900f, 150f), w, h, left, bottom, false), "오른쪽 절반");
            Assert.IsFalse(VirtualJoystickUI.CanBeginAt(new Vector2(200f, 500f), w, h, left, bottom, false), "위쪽 절반");
            Assert.IsFalse(VirtualJoystickUI.CanBeginAt(new Vector2(30f, 150f), w, h, left, bottom, false), "왼쪽 노치 안");
            Assert.IsFalse(VirtualJoystickUI.CanBeginAt(new Vector2(200f, 10f), w, h, left, bottom, false), "제스처 바 안");
            // 사분면 자체는 줄이지 않았다 — 가운데 줄 바로 아래·바로 왼쪽까지 시작된다.
            Assert.IsTrue(VirtualJoystickUI.CanBeginAt(new Vector2(639f, 359f), w, h, left, bottom, false));
        }

        // 조이스틱 자리를 늘 덮는 HUD가 없는지·잠깐 뜨는 것을 다 띄워도 절반 넘게 비는지는 HudOverlapSweepTests가 잰다.

        // ── 전투 화면의 낮·밤·날씨 보정 칩 ──

        [TestCase(1920f, 1080f, false)]
        [TestCase(1440f, 1080f, false)]
        [TestCase(2560f, 1080f, false)]
        [TestCase(1080f, 1920f, true)]
        [TestCase(1080f, 2400f, true)]
        public void BattleEnvironmentChip_UnderItsHpCard_ClearOfCardsGaugeDeckAndEachOther(float width, float height, bool portrait)
        {
            var safe = new Rect(48f, 48f, width - 96f, height - 96f);
            Rect playerCard = DuelHudLayout.HpCard(safe, true);
            Rect enemyCard = DuelHudLayout.HpCard(safe, false);
            float deckHeight = portrait ? 610f : 300f;   // BattleScreenUI.DrawSkillPanel의 하단 기술 판
            var deck = new Rect(safe.x, safe.yMax - deckHeight, safe.width, deckHeight);
            var gauge = new Rect(enemyCard.x, enemyCard.yMax + UITheme.Space.S, enemyCard.width, BattleHudStack.LedgerGaugeHeight);
            float grown = 1f + BattleHudStack.PulseAmount;

            foreach (bool ledger in new[] { false, true })
                foreach (float textWidth in new[] { 0f, 160f, 420f, 2000f })
                {
                    string at = $"{width}x{height} ledger={ledger} text={textWidth}";
                    Rect mine = BattleHudStack.EnvironmentChip(playerCard, true, false, textWidth);
                    Rect theirs = BattleHudStack.EnvironmentChip(enemyCard, false, ledger, textWidth);

                    // 자기 카드의 바깥 모서리에 붙어 카드 폭 안에 선다.
                    Assert.AreEqual(playerCard.x, mine.x, 0.001f, at);
                    Assert.AreEqual(enemyCard.xMax, theirs.xMax, 0.001f, at);
                    Assert.LessOrEqual(mine.xMax, playerCard.xMax + 0.001f, at);
                    Assert.GreaterOrEqual(theirs.x, enemyCard.x - 0.001f, at);
                    Assert.GreaterOrEqual(mine.width, BattleHudStack.EnvironmentChipMinWidth - 0.001f, at);

                    // 커졌다 돌아오는 순간(최대 배율)까지 포함해 무엇도 밟지 않는다.
                    Rect mineGrown = new Rect(mine.x, mine.y, mine.width, mine.height * grown);
                    Rect theirsGrown = new Rect(theirs.x, theirs.y, theirs.width, theirs.height * grown);
                    foreach (Rect r in new[] { mine, theirs, mineGrown, theirsGrown })
                    {
                        Assert.IsFalse(r.Overlaps(playerCard), "내 HP 카드 — " + at);
                        Assert.IsFalse(r.Overlaps(enemyCard), "상대 HP 카드 — " + at);
                        Assert.IsFalse(r.Overlaps(deck), "기술 판 — " + at);
                        if (ledger) Assert.IsFalse(r.Overlaps(gauge), "장부 게이지 — " + at);
                    }
                    Assert.IsFalse(mineGrown.Overlaps(theirsGrown), "두 칩끼리 — " + at);

                    // 대결 말풍선은 칩이 있을 때만 그 아래로 내려간다.
                    Assert.GreaterOrEqual(BattleHudStack.BubbleTop(enemyCard, ledger, true), theirsGrown.yMax, at);
                }
        }

        [Test]
        public void BattleEnvironmentChip_NoEffect_LeavesTheStackWhereItWas()
        {
            // 보정이 없으면 자리도 비우지 않는다 — 말풍선은 칩이 생기기 전과 같은 자리다(카드 아래, 장부가 있으면 그 아래).
            var card = new Rect(1436f, 116f, 460f, DuelHudLayout.HpCardHeight);
            Assert.AreEqual(card.yMax + UITheme.Space.S, BattleHudStack.BubbleTop(card, false, false), 0.001f);
            Assert.AreEqual(card.yMax + UITheme.Space.S + 26f + UITheme.Space.S, BattleHudStack.BubbleTop(card, true, false), 0.001f);
            Assert.AreEqual(BattleHudStack.EnvironmentChipTop(card, true), BattleHudStack.BubbleTop(card, true, false), 0.001f);
        }

        [Test]
        public void BattleEnvironmentChip_Pulse_IsOneGentleBumpThenRests()
        {
            float end = BattleHudStack.PulseDelay + BattleHudStack.PulseSeconds;
            Assert.LessOrEqual(end, 2f, "전투 시작 1~2초 안에 끝난다");
            Assert.AreEqual(1f, BattleHudStack.EnvironmentPulse(0f, false));
            Assert.AreEqual(1f, BattleHudStack.EnvironmentPulse(end + 0.01f, false));
            Assert.AreEqual(1f, BattleHudStack.EnvironmentPulse(60f, false));

            float peak = 1f;
            for (int i = 0; i <= 300; i++)
            {
                float scale = BattleHudStack.EnvironmentPulse(i * 0.01f, false);
                Assert.That(scale, Is.InRange(1f, 1f + BattleHudStack.PulseAmount + 1e-4f));
                peak = Mathf.Max(peak, scale);
                Assert.AreEqual(1f, BattleHudStack.EnvironmentPulse(i * 0.01f, true), "움직임 줄이기면 커지지 않는다");
            }
            Assert.Greater(peak, 1.05f, "눈에 띌 만큼은 커진다");
        }

        [TestCase("밤 · 야행성", 10, true, "밤 · 야행성 +10%")]
        [TestCase("비 · 비를 싫어함", -10, false, "비 · 비를 싫어함 −10%")]
        [TestCase("아침 · 주행성 · 센바람을 좋아함", 10, true, "아침 · 주행성 · 센바람을 좋아함 +10%")]
        [TestCase("안개 · 안개를 싫어함", 0, false, "안개 · 안개를 싫어함 −0%")]   // 반올림으로 0이어도 방향은 남는다
        [TestCase("", 10, true, "+10%")]
        [TestCase(null, -10, false, "−10%")]
        public void BattleEnvironmentLabel_ReasonThenSignedPercent_WithoutEmoji(string reason, int percent, bool favorable, string expected)
        {
            string label = BattleHudStack.EnvironmentLabel(reason, percent, favorable);
            Assert.AreEqual(expected, label);
            // IMGUI 폰트가 이모지를 못 그린다 — 한글·공백·가운뎃점·숫자·부호·퍼센트만.
            foreach (char c in label)
            {
                bool ok = (c >= '가' && c <= '힣') || char.IsDigit(c) || c == ' ' || c == '·' || c == '+' || c == '−' || c == '%';
                Assert.IsTrue(ok, $"'{c}' (U+{(int)c:X4}) in \"{label}\"");
            }
        }

        // ── 도우미 ──

        // 모바일 단축 바 — QuickAccessBarUI.ShortcutBarRect와 같은 모양(우상단, 폭 324, 2열 4줄 = 높이 312).
        private static Rect MobileQuickBar(float screenW, float contentTop, float rightInset)
            => new Rect(screenW - rightInset - 24f - 324f, contentTop, 324f, 312f);

        // 데스크톱 단축 바 — 하단 가운데 폭 1200, 한 줄(16 + 버튼 60).
        private static Rect DesktopQuickBar(float screenW, float screenH)
        {
            float margin = Mathf.Clamp(screenH * 0.03f, 24f, 64f);
            float h = 16f + QuickAccessBarUI.BarButtonHeight;
            return new Rect((screenW - 1200f) * 0.5f, screenH - margin - h, 1200f, h);
        }

        // 퀘스트 칩 높이 — TutorialQuestUI가 스타일에서 파생하는 값(여백 10 + 제목 ceil(34×1.35) + 6 + 진행 ceil(26×1.35) + 10).
        private const float QuestChipHeight = 10f + 46f + 6f + 36f + 10f;
        // 목표 행 — TutorialQuestUI.ObjectiveRowHeight(ceil(27×1.35) + 10×2).
        private const float QuestRowHeight = 37f + 20f;

        // 데스크톱 단축 바 — 진짜 순수 계산(인셋 0).
        private static Rect DesktopBar(float w, float h)
            => QuickAccessBarUI.ShortcutBarRectIn(false, UISafeLayout.ComputeWithMargin(w, 0f, 0f, UISafeLayout.MarginX),
                UISafeLayout.Compute(h, 0f, 0f));

        private static Rect IslandButton(Rect panel, IslandHudForm form, bool visiting, int index)
            => visiting ? IslandHudLayout.VisitButton(panel, form, index) : IslandHudLayout.OwnButton(panel, form, index);

        private static void AssertInside(Rect outer, Rect inner)
        {
            Assert.GreaterOrEqual(inner.xMin, outer.xMin - 0.01f);
            Assert.GreaterOrEqual(inner.yMin, outer.yMin - 0.01f);
            Assert.LessOrEqual(inner.xMax, outer.xMax + 0.01f);
            Assert.LessOrEqual(inner.yMax, outer.yMax + 0.01f);
        }
    }
}
#endif
