using System;
using InsectGame.Core;
using UnityEngine;

namespace InsectGame.Data
{
    [Serializable]
    public struct InsectSpawnCondition
    {
        public bool limitByDayPhase;
        public DayPhase[] allowedDayPhases;
        public bool limitByWeather;
        public WeatherType[] allowedWeather;
        [Range(0, 23)] public int startHourInclusive;
        [Range(0, 23)] public int endHourInclusive;

        public bool Matches(WorldState state)
        {
            if (limitByDayPhase && allowedDayPhases != null && allowedDayPhases.Length > 0)
            {
                bool match = false;
                foreach (DayPhase phase in allowedDayPhases)
                {
                    if (phase == state.DayPhase)
                    {
                        match = true;
                        break;
                    }
                }

                if (!match)
                {
                    return false;
                }
            }

            if (limitByWeather && allowedWeather != null && allowedWeather.Length > 0)
            {
                bool match = false;
                foreach (WeatherType weather in allowedWeather)
                {
                    if (weather == state.Weather)
                    {
                        match = true;
                        break;
                    }
                }

                if (!match)
                {
                    return false;
                }
            }

            if (startHourInclusive != endHourInclusive)
            {
                if (startHourInclusive < endHourInclusive)
                {
                    if (state.Hour24 < startHourInclusive || state.Hour24 > endHourInclusive)
                    {
                        return false;
                    }
                }
                else
                {
                    if (state.Hour24 > endHourInclusive && state.Hour24 < startHourInclusive)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
