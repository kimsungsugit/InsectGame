using UnityEngine;

namespace InsectGame.Core
{
    /// <summary>Presentation preferences only; never used by combat resolution or turn counters.</summary>
    public static class BattlePresentation
    {
        private const string SpeedKey = "BattlePresentation.Speed";
        private const string MotionKey = "BattlePresentation.ReducedMotion";
        private const string FlashKey = "BattlePresentation.ReducedFlashes";
        private static float speed = PlayerPrefs.GetInt(SpeedKey, 1) == 2 ? 2f : 1f;
        private static bool reducedMotion = PlayerPrefs.GetInt(MotionKey, 0) != 0;
        private static bool reducedFlashes = PlayerPrefs.GetInt(FlashKey, 0) != 0;

        public static float Speed
        {
            get => speed;
            set { speed = value >= 2f ? 2f : 1f; PlayerPrefs.SetInt(SpeedKey, (int)speed); PlayerPrefs.Save(); }
        }
        public static bool ReducedMotion
        {
            get => reducedMotion;
            set { reducedMotion = value; PlayerPrefs.SetInt(MotionKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
        public static bool ReducedFlashes
        {
            get => reducedFlashes;
            set { reducedFlashes = value; PlayerPrefs.SetInt(FlashKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
        public static float DeltaTime => Time.deltaTime * Speed * TimeScale;

        // ── 히트스톱·슬로모션 ──
        //
        // 전투 연출의 모든 시계(아레나 코루틴·페이즈 타이머·HP 보간)가 위 DeltaTime 하나를 쓴다.
        // 그래서 여기서 배율을 0으로 떨구면 **화면 전체가 같은 순간에 멈춘다** — 타격 순간 한두
        // 프레임 정지가 "맞았다"를 몸으로 느끼게 하는 장치다. Time.timeScale을 건드리지 않으므로
        // 필드·UI 트윈·오디오는 그대로 돈다(BattleScreenUI.OnDisable의 슬로모션 복구와도 무관).
        //
        // 만료는 **실제 시간**으로 센다 — 멈춘 시계로 세면 영영 안 풀린다.

        private static float stopUntil;
        private static float slowUntil;
        private static float slowScale = 1f;

        /// <summary>테스트용 시계 이음매. 기본은 실제 시간.</summary>
        internal static System.Func<float> Clock = () => Time.unscaledTime;

        /// <summary>
        /// 연출 시계 배율 — 히트스톱 중 0, 슬로모션 중 그 배율, 평소 1.
        /// 전투 결과·턴 카운터는 이 값을 보지 않는다(표시 전용).
        /// </summary>
        public static float TimeScale
        {
            get
            {
                float now = Clock();
                if (now < stopUntil) return 0f;
                if (now < slowUntil) return slowScale;
                return 1f;
            }
        }

        /// <summary>
        /// 타격 순간 연출을 잠깐 멈춘다(실제 초). 2배속이면 절반만 멈춘다 — 배속을 고른 사람은
        /// 멈춤도 짧기를 원한다. 겹쳐 부르면 더 늦게 끝나는 쪽을 따른다(연장이지 누적이 아니다).
        /// </summary>
        public static void HitStop(float seconds)
        {
            if (seconds <= 0f) return;
            float until = Clock() + seconds / Mathf.Max(1f, speed);
            if (until > stopUntil) stopUntil = until;
        }

        /// <summary>
        /// 일정 시간 연출을 느리게 돌린다(치명타·마무리 일격). 움직임 줄이기를 켠 사람에겐 생략한다 —
        /// 느려진 화면 흔들림·돌진이 멀미를 부르는 쪽이라서다.
        /// </summary>
        public static void SlowMotion(float scale, float seconds)
        {
            if (seconds <= 0f || reducedMotion) return;
            slowScale = Mathf.Clamp(scale, 0.05f, 1f);
            float until = Clock() + seconds / Mathf.Max(1f, speed);
            if (until > slowUntil) slowUntil = until;
        }

        /// <summary>전투가 끝나거나 아레나가 정리될 때 — 남은 정지가 다음 전투 첫 프레임을 먹지 않게.</summary>
        public static void ClearTimeEffects()
        {
            stopUntil = 0f;
            slowUntil = 0f;
            slowScale = 1f;
        }
    }
}
