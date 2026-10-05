using System;
using UnityEngine;

namespace InsectGame.Core
{
    public enum WeatherType
    {
        Clear,
        Rain,
        Fog,
        Wind,
        Snow   // 뒤에 덧붙였다 — 직렬화된 값(InsectSpawnCondition.allowedWeather)이 밀리지 않게
    }

    /// <summary>
    /// 세계 날씨 — 시간이 흐르면 <see cref="WeatherForecast"/>가 정한 대로 스스로 바뀐다.
    /// 규칙(다음 날씨·지속·지역 기후)은 순수 클래스에 있고 여기는 시계·전환 진행만 든다.
    ///
    /// <b>전환은 <see cref="Blend01"/>로 읽는다</b> — 0이면 <see cref="PreviousWeather"/>, 1이면 <see cref="CurrentWeather"/>.
    /// 하늘·안개·입자가 이 값으로 부드럽게 옮겨 간다. <see cref="CurrentWeather"/> 자체는 바뀌는 순간 곧바로 새 값이다
    /// (스폰·전투 보정은 전환을 기다리지 않는다).
    ///
    /// 저장하지 않는다 — 로드하면 처음 몇 분은 맑음이다(<see cref="WeatherForecast.FirstClearSeconds"/>).
    /// </summary>
    public class WeatherSystem : MonoBehaviour
    {
        [SerializeField] private WeatherType currentWeather = WeatherType.Clear;
        [SerializeField] private bool autoCycle = true;

        private WeatherType previousWeather = WeatherType.Clear;
        private float blend01 = 1f;
        private float secondsLeft = WeatherForecast.FirstClearSeconds;
        private bool held;   // SetWeather(hold: true) — 검수·꿈·연출이 날씨를 붙잡아 둔다

        public WeatherType CurrentWeather => currentWeather;
        public WeatherType PreviousWeather => previousWeather;

        /// <summary>전환 진행 — 0이면 이전 날씨, 1이면 지금 날씨.</summary>
        public float Blend01 => blend01;

        /// <summary>날씨가 바뀌었다 (이전, 새 날씨).</summary>
        public event Action<WeatherType, WeatherType> WeatherChanged;

        private void Update()
        {
            if (blend01 < 1f)
                blend01 = Mathf.Min(1f, blend01 + Time.deltaTime / WeatherForecast.TransitionSeconds);

            if (!autoCycle || held) return;
            secondsLeft -= Time.deltaTime;
            if (secondsLeft > 0f) return;

            WeatherType next = WeatherForecast.Next(currentWeather, UnityEngine.Random.value);
            Apply(next, WeatherForecast.RollDuration(next, UnityEngine.Random.value), instant: false);
        }

        /// <summary>
        /// 날씨를 바로 바꾼다. <paramref name="hold"/>가 참이면 <see cref="Release"/>를 부를 때까지 스스로 바뀌지 않는다
        /// (검수 캡처·꿈 프롤로그가 쓴다). 같은 날씨여도 붙잡기만 갱신한다.
        /// </summary>
        public void SetWeather(WeatherType weather, bool hold = false, bool instant = false)
        {
            held = hold;
            if (weather == currentWeather)
            {
                if (instant) blend01 = 1f;
                return;
            }
            Apply(weather, WeatherForecast.RollDuration(weather, UnityEngine.Random.value), instant);
        }

        /// <summary>붙잡은 날씨를 풀고 다시 스스로 바뀌게 한다.</summary>
        public void Release()
        {
            held = false;
            secondsLeft = Mathf.Max(secondsLeft, 30f);
        }

        private void Apply(WeatherType weather, float duration, bool instant)
        {
            WeatherType old = currentWeather;
            previousWeather = old;
            currentWeather = weather;
            secondsLeft = duration;
            blend01 = instant ? 1f : 0f;
            WeatherChanged?.Invoke(old, weather);
        }
    }
}
