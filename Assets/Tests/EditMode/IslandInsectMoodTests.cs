#if UNITY_EDITOR
using InsectGame.Core;
using InsectGame.Data;
using NUnit.Framework;

namespace InsectGame.Tests
{
    /// <summary>
    /// 섬 방목 곤충의 시간·날씨 기분(<see cref="IslandInsectMood"/>) — 야행성은 밤에 활발·낮엔 쉼, 주행성은 반대, 싫은 날씨엔 덜 움직이고
    /// 좋은 날씨엔 활발, 시간·날씨를 안 타는 종은 예전 배회 그대로.
    /// </summary>
    [TestFixture]
    public class IslandInsectMoodTests
    {
        private static readonly DayPhase[] Phases = (DayPhase[])System.Enum.GetValues(typeof(DayPhase));
        private static readonly WeatherType[] Weathers = (WeatherType[])System.Enum.GetValues(typeof(WeatherType));

        private static WorldState State(DayPhase phase, WeatherType weather)
            => new WorldState { DayPhase = phase, Weather = weather, Hour24 = 12 };

        private static InsectHabit Habit(InsectActivity activity, WeatherSet like = WeatherSet.None, WeatherSet dislike = WeatherSet.None)
            => new InsectHabit(activity, like, dislike, InsectTemperament.Docile);

        [Test]
        public void Nocturnal_IsLivelyAtNight_AndMostlyRestsByDay()
        {
            InsectHabit moth = Habit(InsectActivity.Nocturnal);
            IslandInsectMood.Mood night = IslandInsectMood.For(moth, State(DayPhase.Night, WeatherType.Clear));
            IslandInsectMood.Mood day = IslandInsectMood.For(moth, State(DayPhase.Day, WeatherType.Clear));

            Assert.Greater(night.Activity, day.Activity);
            Assert.Less(night.RestMax, day.RestMin, "밤에 쉬는 시간이 낮보다 길다");
            Assert.Greater(night.SpeedMultiplier, 1f, "밤엔 조금 빨라야 한다");
            Assert.Less(day.SpeedMultiplier, 1f, "낮엔 느려야 한다");
            Assert.Less(day.HoverMultiplier, 0.5f, "낮엔 나는 종도 땅에 낮게 내려앉는다");
            Assert.Greater(night.HoverMultiplier, day.HoverMultiplier);
        }

        [Test]
        public void Diurnal_IsTheMirrorImage()
        {
            InsectHabit bee = Habit(InsectActivity.Diurnal);
            IslandInsectMood.Mood day = IslandInsectMood.For(bee, State(DayPhase.Day, WeatherType.Clear));
            IslandInsectMood.Mood morning = IslandInsectMood.For(bee, State(DayPhase.Morning, WeatherType.Clear));
            IslandInsectMood.Mood night = IslandInsectMood.For(bee, State(DayPhase.Night, WeatherType.Clear));

            Assert.Greater(day.Activity, night.Activity);
            Assert.AreEqual(day.Activity, morning.Activity, 1e-6f, "주행성은 아침에도 제 시간이다");
            Assert.Greater(night.RestMin, day.RestMax);
            Assert.Less(night.SpeedMultiplier, day.SpeedMultiplier);
        }

        [Test]
        public void Weather_LikedLivensUp_DislikedSlowsDown()
        {
            InsectHabit h = Habit(InsectActivity.Any, WeatherSet.Fog, WeatherSet.Rain);
            float liked = IslandInsectMood.Activity(h, State(DayPhase.Day, WeatherType.Fog));
            float plain = IslandInsectMood.Activity(h, State(DayPhase.Day, WeatherType.Clear));
            float disliked = IslandInsectMood.Activity(h, State(DayPhase.Day, WeatherType.Rain));
            Assert.Greater(liked, plain);
            Assert.Less(disliked, plain);
            Assert.Greater(plain - disliked, liked - plain, "싫은 날씨가 좋은 날씨보다 눈에 띄게 바꾼다");

            IslandInsectMood.Mood rain = IslandInsectMood.For(h, State(DayPhase.Day, WeatherType.Rain));
            IslandInsectMood.Mood fog = IslandInsectMood.For(h, State(DayPhase.Day, WeatherType.Fog));
            Assert.Greater(rain.RestMin, fog.RestMin, "싫은 날씨에 덜 움직인다");
            Assert.Less(rain.SpeedMultiplier, fog.SpeedMultiplier);
        }

        [Test]
        public void TimelessSpecies_WanderExactlyAsBefore()
        {
            // 성향을 넣기 전의 배회 — 쉬기 1.2~4.5초 · 속도 ×1 · 나는 높이 ×1. 시간·날씨를 안 타는 종은 그대로여야 한다.
            foreach (DayPhase p in Phases)
                foreach (WeatherType w in Weathers)
                {
                    IslandInsectMood.Mood m = IslandInsectMood.For(InsectHabit.Neutral, State(p, w));
                    Assert.AreEqual(1.2f, m.RestMin, 1e-5f, $"{p}/{w}");
                    Assert.AreEqual(4.5f, m.RestMax, 1e-5f, $"{p}/{w}");
                    Assert.AreEqual(1f, m.SpeedMultiplier, 1e-5f, $"{p}/{w}");
                    Assert.AreEqual(1f, m.HoverMultiplier, 1e-5f, $"{p}/{w}");
                }
            IslandInsectMood.Mood neutral = IslandInsectMood.Neutral;
            Assert.AreEqual(1.2f, neutral.RestMin, 1e-5f);
            Assert.AreEqual(4.5f, neutral.RestMax, 1e-5f);
        }

        [Test]
        public void Mood_IsMonotonic_AndClamped()
        {
            IslandInsectMood.Mood prev = IslandInsectMood.FromActivity(0f);
            for (int i = 1; i <= 20; i++)
            {
                IslandInsectMood.Mood m = IslandInsectMood.FromActivity(i / 20f);
                Assert.LessOrEqual(m.RestMin, prev.RestMin + 1e-5f, "더 활발한데 더 오래 쉰다");
                Assert.LessOrEqual(m.RestMax, prev.RestMax + 1e-5f);
                Assert.GreaterOrEqual(m.SpeedMultiplier, prev.SpeedMultiplier - 1e-5f);
                Assert.GreaterOrEqual(m.HoverMultiplier, prev.HoverMultiplier - 1e-5f);
                Assert.LessOrEqual(m.RestMin, m.RestMax);
                Assert.Greater(m.SpeedMultiplier, 0f, "멈춰 버리면 배회가 아니다");
                prev = m;
            }
            Assert.AreEqual(1f, IslandInsectMood.FromActivity(3f).Activity, 1e-6f);
            Assert.AreEqual(0f, IslandInsectMood.FromActivity(-1f).Activity, 1e-6f);

            // 가장 졸린 경우(잠들 시간 + 싫은 날씨)도 0~1 안이다.
            InsectHabit h = Habit(InsectActivity.Nocturnal, WeatherSet.None, WeatherSet.Clear);
            float a = IslandInsectMood.Activity(h, State(DayPhase.Day, WeatherType.Clear));
            Assert.That(a, Is.InRange(0f, 1f));
        }
    }
}
#endif
