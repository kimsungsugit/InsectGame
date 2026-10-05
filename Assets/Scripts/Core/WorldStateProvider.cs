using UnityEngine;

namespace InsectGame.Core
{
    public struct WorldState
    {
        public DayPhase DayPhase;
        public WeatherType Weather;
        public int Hour24;

        /// <summary>밤인가 — 야행성 곤충이 깨어나고 습격이 일어나는 시간대.</summary>
        public bool IsNight => DayPhase == DayPhase.Night;
    }

    public class WorldStateProvider : MonoBehaviour
    {
        [SerializeField] private GameClock gameClock;
        [SerializeField] private WeatherSystem weatherSystem;

        public GameClock Clock => gameClock;
        public WeatherSystem Weather => weatherSystem;

        /// <summary>세계 날씨 그대로의 상태 — 지역을 모를 때(길 위·UI)의 값이다.</summary>
        public WorldState GetWorldState()
        {
            return GetWorldState(null);
        }

        /// <summary>
        /// <paramref name="regionId"/> 지역에서 실제로 보이는 상태 — 같은 세계 날씨라도 설산에서는 비가 눈이 되고
        /// 사막에서는 눈이 센바람이 된다(<see cref="WeatherForecast.EffectiveIn"/>). 스폰·전투 보정이 이쪽을 읽는다.
        /// </summary>
        public WorldState GetWorldState(string regionId)
        {
            DayPhase phase = gameClock != null ? gameClock.GetDayPhase() : DayPhase.Day;
            int hour = gameClock != null ? gameClock.GetHour24() : 12;
            WeatherType weather = weatherSystem != null ? weatherSystem.CurrentWeather : WeatherType.Clear;

            return new WorldState
            {
                DayPhase = phase,
                Weather = WeatherForecast.EffectiveIn(weather, regionId),
                Hour24 = hour
            };
        }

        public void AutoWire(GameClock clock, WeatherSystem weather)
        {
            if (gameClock == null)
            {
                gameClock = clock;
            }

            if (weatherSystem == null)
            {
                weatherSystem = weather;
            }
        }
    }
}
