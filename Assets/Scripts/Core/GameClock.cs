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

        public float Time01 => time01;

        private void Awake()
        {
            time01 = startTime01;
        }

        private void Update()
        {
            if (paused) return;
            if (dayLengthMinutes <= 0f)
            {
                return;
            }

            float delta = Time.deltaTime / (dayLengthMinutes * 60f);
            time01 = (time01 + delta) % 1f;
        }

        private void OnApplicationPause(bool pauseStatus) { paused = pauseStatus; }
        private void OnApplicationFocus(bool hasFocus) { if (hasFocus) paused = false; }

        public int GetHour24()
        {
            return Mathf.FloorToInt(time01 * 24f);
        }

        public DayPhase GetDayPhase()
        {
            float hour = time01 * 24f;
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
