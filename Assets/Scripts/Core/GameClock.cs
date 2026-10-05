using System;
using UnityEngine;

namespace InsectGame.Core
{
    public enum DayPhase
    {
        Morning,
        Day,
        Evening,
        Night
    }

    public class GameClock : MonoBehaviour
    {
        [SerializeField] private float dayLengthMinutes = 12f;
        [Range(0f, 1f)] [SerializeField] private float startTime01 = 0.25f;

        private float time01;
        private bool paused; // 모바일 백그라운드 시 시간 정지
        private bool held;   // SetTime01(hold: true) — 검수·꿈 연출이 시각을 붙잡아 둔다
        private DayPhase lastPhase;

        public float Time01 => time01;
        public float DayLengthMinutes => dayLengthMinutes;

        /// <summary>시간대가 바뀌었다(아침→낮→저녁→밤). 스폰이 새 시간대의 종으로 갈아입는 신호다.</summary>
        public event Action<DayPhase> DayPhaseChanged;

        private void Awake()
        {
            time01 = startTime01;
            lastPhase = GetDayPhase();
        }

        private void Update()
        {
            if (paused || held) return;
            if (dayLengthMinutes <= 0f)
            {
                return;
            }

            float delta = Time.deltaTime / (dayLengthMinutes * 60f);
            time01 = (time01 + delta) % 1f;
            NotifyPhaseChange();
        }

        private void OnApplicationPause(bool pauseStatus) { paused = pauseStatus; }
        private void OnApplicationFocus(bool hasFocus) { if (hasFocus) paused = false; }

        /// <summary>
        /// 시각을 바로 정한다(0~1, 0.5 = 정오). <paramref name="hold"/>가 참이면 <see cref="Release"/>까지 시계가 멈춘다.
        /// 검수 캡처와 꿈 프롤로그(고정 한낮)가 쓴다.
        /// </summary>
        public void SetTime01(float value, bool hold = false)
        {
            time01 = Mathf.Repeat(value, 1f);
            held = hold;
            NotifyPhaseChange();
        }

        /// <summary>붙잡은 시계를 다시 흐르게 한다.</summary>
        public void Release() { held = false; }

        private void NotifyPhaseChange()
        {
            DayPhase phase = GetDayPhase();
            if (phase == lastPhase) return;
            lastPhase = phase;
            DayPhaseChanged?.Invoke(phase);
        }

        public int GetHour24()
        {
            return Mathf.FloorToInt(time01 * 24f);
        }

        /// <summary>소수 시각(0~24). 조명이 시간대 경계에서 뚝 끊기지 않게 연속값으로 읽는다.</summary>
        public float GetHourFloat()
        {
            return time01 * 24f;
        }

        public DayPhase GetDayPhase()
        {
            return PhaseOfHour(time01 * 24f);
        }

        /// <summary>시각(0~24)의 시간대 — 순수 함수라 테스트가 시계 없이 경계를 본다. 아침 6~11 · 낮 11~17 · 저녁 17~21 · 밤 21~6.</summary>
        public static DayPhase PhaseOfHour(float hour)
        {
            if (hour >= 6f && hour < 11f)
            {
                return DayPhase.Morning;
            }
            if (hour >= 11f && hour < 17f)
            {
                return DayPhase.Day;
            }
            if (hour >= 17f && hour < 21f)
            {
                return DayPhase.Evening;
            }
            return DayPhase.Night;
        }
    }
}
