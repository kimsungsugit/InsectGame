using UnityEngine;

namespace InsectGame.Core
{
    public struct WorldState
    {
        public DayPhase DayPhase;
        public WeatherType Weather;
        public int Hour24;
    }

    public class WorldStateProvider : MonoBehaviour
    {
        [SerializeField] private GameClock gameClock;
        [SerializeField] private WeatherSystem weatherSystem;

        public WorldState GetWorldState()
        {
            DayPhase phase = gameClock != null ? gameClock.GetDayPhase() : DayPhase.Day;
            int hour = gameClock != null ? gameClock.GetHour24() : 12;
            WeatherType weather = weatherSystem != null ? weatherSystem.CurrentWeather : WeatherType.Clear;

            return new WorldState
            {
                DayPhase = phase,
                Weather = weather,
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
