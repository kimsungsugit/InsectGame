using UnityEngine;

namespace InsectGame.Core
{
    public enum WeatherType
    {
        Clear,
        Rain,
        Fog,
        Wind
    }

    public class WeatherSystem : MonoBehaviour
    {
        [SerializeField] private WeatherType currentWeather = WeatherType.Clear;

        public WeatherType CurrentWeather => currentWeather;

        public void SetWeather(WeatherType weather)
        {
            currentWeather = weather;
        }
    }
}
