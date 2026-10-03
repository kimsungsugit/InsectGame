using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Battle
{
    /// <summary>
    /// 한 곤충이 이번 전투에서 받는 낮·밤·날씨 보정 — 배수와 칩 문구. 전투 화면이 이 값을 그대로 칩으로 그린다.
    /// 기본값(<c>default</c>)은 배수 0이라 <see cref="HasEffect"/>가 거짓이다 — "아직 안 정함"도 "영향 없음"으로 읽힌다.
    /// </summary>
    public struct BattleEnvironmentNote
    {
        public float Multiplier;   // 1 = 영향 없음, 1.1 = 유리, 0.9 = 불리
        public string Reason;      // 칩 문구 — "밤 · 야행성", "비 · 비를 싫어함" 같은 짧은 말(기호·퍼센트 없음). 영향 없으면 빈 문자열
        public bool HasEffect => !UnityEngine.Mathf.Approximately(Multiplier, 1f) && Multiplier > 0f;
        public int Percent => UnityEngine.Mathf.RoundToInt((Multiplier - 1f) * 100f);   // +10 / -10
        public static BattleEnvironmentNote None => new BattleEnvironmentNote { Multiplier = 1f, Reason = string.Empty };
    }

    /// <summary>
    /// 낮·밤·날씨 전투 보정의 <b>순수 규칙</b> — 어디에 걸리는가(<see cref="Applies"/>)와 무엇을 곱하고 어떻게 말하는가
    /// (<see cref="NoteFor(InsectData, WorldState)"/>). 배수 자체는 <see cref="InsectHabits.BattleStatMultiplier"/>가 단일 출처다
    /// (유리 ×1.10 · 불리 ×0.90). 컨트롤러(<c>InsectBattleController</c>)는 이 답을 받아 ATK·DEF에만 곱한다 — HP는 건드리지 않는다
    /// (HP바가 전투 시작에 튀지 않게).
    ///
    /// <b>야생 전투만.</b> 수문장·NPC 대결(보스 포함)·샌드박스(꿈 챔피언전)·레이드는 저작된 밸런스라 시계·날씨로 흔들지 않는다.
    /// 동굴 같은 실내 서브에리어도 제외다 — 그 안은 스폰도 시간·날씨를 안 본다. 나의 섬(분리 구역)은 하늘 아래라 걸린다.
    ///
    /// <b>날씨는 그 지역에서 보이는 날씨다</b>(<c>WorldStateProvider.GetWorldState(regionId)</c>) — 설산의 비는 눈이다.
    /// 섬은 지역이 없어 세계 날씨를 그대로 쓴다(<see cref="WeatherRegionId"/>).
    /// </summary>
    public static class BattleEnvironment
    {
        /// <summary>이 전투에 낮·밤·날씨 보정을 거는가 — 야생 실외 전투일 때만 참이다.</summary>
        public static bool Applies(bool duel, bool sandbox, bool guardian, bool inIndoorSubArea)
        {
            return !duel && !sandbox && !guardian && !inIndoorSubArea;
        }

        /// <summary>실내 서브에리어(동굴·방)인가. 분리 구역(나의 섬)은 하늘 아래라 실내가 아니다.</summary>
        public static bool IsIndoor(SubAreaData subArea)
        {
            return subArea != null && !subArea.detached;
        }

        /// <summary>
        /// 전투가 날씨를 읽을 지역 ID — 서브에리어(섬)에 있으면 null(세계 날씨), 아니면 지금 리전.
        /// 섬에 있는 동안 <c>RegionManager.CurrentRegion</c>은 마지막 리전에 붙어 있으므로 그걸 읽으면 안 된다.
        /// 필드 HUD(<c>WorldClockHUD</c>)와 같은 기준이라 칩과 HUD의 날씨가 갈리지 않는다.
        /// </summary>
        public static string WeatherRegionId(RegionData region, SubAreaData subArea)
        {
            if (subArea != null) return null;
            return region != null && !string.IsNullOrEmpty(region.regionId) ? region.regionId : null;
        }

        /// <summary>종 데이터의 이번 하늘 아래 보정(null이면 영향 없음).</summary>
        public static BattleEnvironmentNote NoteFor(InsectData data, WorldState state)
        {
            if (data == null) return BattleEnvironmentNote.None;
            return NoteFor(InsectHabits.For(data), state);
        }

        /// <summary>
        /// 성향의 이번 하늘 아래 보정. 문구는 시간대·날씨 가운데 <b>실제로 배수를 만든 쪽</b>만 적는다 —
        /// 제 시간에 싫은 날씨처럼 서로 지운 쪽은 배수가 1이라 아예 칩이 없다.
        /// </summary>
        public static BattleEnvironmentNote NoteFor(InsectHabit habit, WorldState state)
        {
            float multiplier = InsectHabits.BattleStatMultiplier(habit, state);
            if (Mathf.Approximately(multiplier, 1f) || !(multiplier > 0f)) return BattleEnvironmentNote.None;

            bool favorable = multiplier > 1f;
            int timeFit = InsectHabits.TimeFit(habit, state.DayPhase);
            int weatherFit = InsectHabits.WeatherFit(habit, state.Weather);
            // 합친 궁합(InsectHabits.Fit)과 같은 방향인 쪽이 배수를 만들었다. 둘 다 0이면 배수도 1이라 여기 오지 않는다.
            bool timeMade = favorable ? timeFit > 0 : timeFit < 0;
            bool weatherMade = favorable ? weatherFit > 0 : weatherFit < 0;

            return new BattleEnvironmentNote
            {
                Multiplier = multiplier,
                Reason = ReasonText(habit.Activity, state, timeMade, weatherMade, favorable)
            };
        }

        // ── 문구 ──
        //
        // 칩이 짧아야 한다 — 시간만이면 "밤 · 야행성", 날씨만이면 "비 · 비를 싫어함", 둘 다면 "밤 · 야행성 · 비를 좋아함".
        // 가장 긴 조합이 "아침 · 주행성 · 맑은 날을 좋아함"(20자, 공백 포함)이다 — BattleEnvironmentTests가 20자 상한을 본다.
        // 방향(+/−)과 퍼센트는 칩이 Percent로 따로 붙인다.

        private static string ReasonText(InsectActivity activity, WorldState state, bool timeMade, bool weatherMade,
            bool favorable)
        {
            string phase = WeatherForecast.DisplayName(state.DayPhase);
            string activityName = ActivityName(activity);
            string taste = WeatherObject(state.Weather) + (favorable ? " 좋아함" : " 싫어함");

            if (timeMade && weatherMade && activityName.Length > 0)
                return phase + " · " + activityName + " · " + taste;
            if (timeMade && activityName.Length > 0)
                return phase + " · " + activityName;
            if (weatherMade)
                return WeatherForecast.DisplayName(state.Weather) + " · " + taste;

            // 배수가 1이 아니면 둘 중 하나는 반드시 만든 쪽이다 — 표·규칙이 바뀌어 여기 와도 칩이 빈 채로 뜨지 않게 한다.
            return phase + " · " + WeatherForecast.DisplayName(state.Weather);
        }

        private static string ActivityName(InsectActivity activity)
        {
            switch (activity)
            {
                case InsectActivity.Nocturnal: return "야행성";
                case InsectActivity.Diurnal: return "주행성";
                default: return string.Empty;
            }
        }

        // 날씨를 목적어로 — "맑음을 좋아함"은 어색하고 "햇볕을 좋아함"은 밤(맑은 밤을 좋아하는 개미귀신·혜성나방)에 거짓이라
        // 맑은 날로 말한다.
        private static string WeatherObject(WeatherType weather)
        {
            switch (weather)
            {
                case WeatherType.Rain: return "비를";
                case WeatherType.Fog: return "안개를";
                case WeatherType.Wind: return "센바람을";
                case WeatherType.Snow: return "눈을";
                default: return "맑은 날을";
            }
        }
    }
}
