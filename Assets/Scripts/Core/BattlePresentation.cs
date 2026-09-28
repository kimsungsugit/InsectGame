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
        public static float DeltaTime => Time.deltaTime * Speed;
    }
}
