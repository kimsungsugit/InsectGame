#if UNITY_EDITOR
using System.Collections.Generic;
using InsectGame.Capture;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 포획 미니게임 세 종의 <b>규칙</b>. 화면은 IMGUI라 테스트로 못 보므로, 점수가 어떻게 나오는지만 고정한다.
    ///
    /// 셋 다 결과가 0~3점이고 그 값이 <c>CaptureMinigameProbability</c>로 들어간다. 그래서 여기서 지킬 것은
    /// ①잘하면 3점이 <b>실제로 가능</b>하고(시간이 모자라 불가능한 판이 없다) ②아무것도 안 하면 0점이라는 것이다.
    /// 어느 한쪽이 깨지면 포획 확률이 게임 종류에 따라 조용히 갈린다.
    /// </summary>
    [TestFixture]
    public class CaptureMinigameTests
    {
        private const float Step = 1f / 60f;
        private const int MaxTicks = 60 * 60;   // 어떤 판도 1분 안에는 끝나야 한다

        private static CaptureMinigameTuning Tuning(int rarity, float speed = 1f, float zone = 1f, float time = 1f)
            => new CaptureMinigameTuning(rarity, speed, zone, time);

        private static int RunUntilDone(CaptureMinigame game, System.Func<CaptureMinigame, CaptureMinigameInput> play)
        {
            int ticks = 0;
            while (!game.Done && ticks < MaxTicks)
            {
                game.Tick(Step, play(game));
                ticks++;
            }
            return ticks;
        }

        private static CaptureMinigameInput Idle(CaptureMinigame game) => new CaptureMinigameInput();

        // ── 게임 고르기 ──

        [Test]
        public void PickNext_NeverRepeatsThePreviousGame()
        {
            var random = new System.Random(7);
            CaptureMinigameKind last = CaptureMinigame.PickNext(null, random);
            for (int i = 0; i < 300; i++)
            {
                CaptureMinigameKind next = CaptureMinigame.PickNext(last, random);
                Assert.AreNotEqual(last, next, $"{i}번째 추첨이 직전 게임을 반복했다");
                last = next;
            }
        }

        [Test]
        public void PickNext_ReachesEveryGame()
        {
            var random = new System.Random(11);
            var seen = new HashSet<CaptureMinigameKind>();
            CaptureMinigameKind? last = null;
            for (int i = 0; i < 60; i++)
            {
                last = CaptureMinigame.PickNext(last, random);
                seen.Add(last.Value);
            }
            Assert.AreEqual(3, seen.Count);
        }

        [TestCase(CaptureMinigameKind.Sneak)]
        [TestCase(CaptureMinigameKind.Track)]
        [TestCase(CaptureMinigameKind.Toss)]
        public void Create_ReturnsTheRequestedGame(CaptureMinigameKind kind)
        {
            Assert.AreEqual(kind, CaptureMinigame.Create(kind, Tuning(0), new System.Random(1)).Kind);
        }

        [Test]
        public void Tuning_NonPositiveMultipliers_FallBackToOne()
        {
            // 0이 그대로 들어가면 제한 시간이 0이 되어 판이 열리자마자 끝난다.
            CaptureMinigameTuning t = new CaptureMinigameTuning(9, 0f, -1f, 0f);
            Assert.AreEqual(4, t.Rarity);
            Assert.AreEqual(1f, t.SpeedMult);
            Assert.AreEqual(1f, t.ZoneMult);
            Assert.AreEqual(1f, t.TimeMult);
        }

        // ── 살금살금 ──

        // 조심스러운 사람: 곤충이 가만히 있을 때만 다가간다(예고가 뜨면 바로 멈춘다).
        private static CaptureMinigameInput Careful(CaptureMinigame game)
        {
            var sneak = (SneakMinigame)game;
            return new CaptureMinigameInput { Down = sneak.State == SneakMinigame.Watch.Calm };
        }

        [Test]
        public void Sneak_CarefulPlay_ScoresThreeAtEveryRarity()
        {
            for (int rarity = 0; rarity <= 4; rarity++)
            {
                for (int seed = 0; seed < 25; seed++)
                {
                    var game = new SneakMinigame(Tuning(rarity), new System.Random(seed));
                    game.Tick(Step, new CaptureMinigameInput());   // 손을 뗀 상태에서 시작
                    RunUntilDone(game, Careful);

                    Assert.IsTrue(game.Done);
                    Assert.AreEqual(0, game.Spots, $"등급 {rarity} 시드 {seed}: 가만히 있을 때만 움직였는데 들켰다");
                    Assert.AreEqual(3, game.Hits, $"등급 {rarity} 시드 {seed}: 한 번도 안 들켰는데 시간이 모자랐다");
                }
            }
        }

        [Test]
        public void Sneak_HoldingThroughEverything_GetsSpottedTwiceAndInsectFlees()
        {
            // 전설 등급으로 잰다 — 걸음이 느려 두 번째로 돌아보기 전에는 절대 닿지 못한다
            // (일반 등급은 운이 좋으면 한 번만 들키고 도착한다).
            var game = new SneakMinigame(Tuning(4), new System.Random(3));
            game.Tick(Step, new CaptureMinigameInput());
            // 들킨 뒤엔 손을 한 번 떼야 다시 움직인다 — 굳은 시간이 끝나면 떼었다가 다시 누르는 사람.
            bool release = false;
            RunUntilDone(game, g =>
            {
                var sneak = (SneakMinigame)g;
                if (sneak.FreezeLeft > 0f) { release = true; return new CaptureMinigameInput { Down = true }; }
                if (release) { release = false; return new CaptureMinigameInput(); }
                return new CaptureMinigameInput { Down = true };
            });

            Assert.IsTrue(game.Done);
            Assert.IsTrue(game.Fled);
            Assert.AreEqual(SneakMinigame.MaxSpots, game.Spots);
            Assert.Less(game.Hits, 3);
        }

        [Test]
        public void Sneak_Spotted_PushesBackButNeverAcrossTheFlagBehind()
        {
            var game = new SneakMinigame(Tuning(0), new System.Random(5));
            game.Tick(Step, new CaptureMinigameInput());
            int ticks = 0;
            while (game.Spots == 0 && !game.Done && ticks++ < MaxTicks)
                game.Tick(Step, new CaptureMinigameInput { Down = true });

            Assert.AreEqual(1, game.Spots);
            // 어느 구간에서 들켰든 그 구간의 출발 깃발보다 뒤로는 안 간다.
            float top = game.Distance > SneakMinigame.FirstFlag ? 1f
                : (game.Distance > SneakMinigame.SecondFlag ? SneakMinigame.FirstFlag : SneakMinigame.SecondFlag);
            Assert.Less(game.Distance, top);
        }

        [Test]
        public void Sneak_HeldFromBeforeTheGameOpened_DoesNotMoveUntilReleased()
        {
            // 채집망을 고른 그 손가락이 눌린 채로 판이 열린다 — 그대로 걸어가면 안 된다.
            var game = new SneakMinigame(Tuning(0), new System.Random(2));
            for (int i = 0; i < 20; i++) game.Tick(Step, new CaptureMinigameInput { Down = true });
            Assert.AreEqual(1f, game.Distance);
        }

        [Test]
        public void Sneak_NoInput_TimesOutWithZero()
        {
            var game = new SneakMinigame(Tuning(0), new System.Random(1));
            RunUntilDone(game, Idle);
            Assert.IsTrue(game.Done);
            Assert.AreEqual(0, game.Hits);
            Assert.AreEqual(0f, game.TimeRatio);
        }

        // ── 가두기 ──

        [Test]
        public void Track_FollowingTheInsect_FillsTheGauge()
        {
            for (int rarity = 0; rarity <= 2; rarity++)
            {
                var game = new TrackMinigame(Tuning(rarity), new System.Random(rarity + 1));
                RunUntilDone(game, g => new CaptureMinigameInput { Down = true, Point = ((TrackMinigame)g).Insect });

                Assert.IsTrue(game.Done);
                Assert.AreEqual(3, game.Hits, $"등급 {rarity}: 곤충을 정확히 따라갔는데 게이지가 안 찼다");
                Assert.Greater(game.TimeLeft, 0f);
            }
        }

        [Test]
        public void Track_NoInput_ScoresNothingEvenIfInsectWandersIntoTheNet()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var game = new TrackMinigame(Tuning(0), new System.Random(seed));
                RunUntilDone(game, Idle);
                Assert.AreEqual(0, game.Hits, $"시드 {seed}: 손을 대지 않았는데 별이 생겼다");
            }
        }

        [Test]
        public void Track_HitsAreLatched_LosingTheInsectDoesNotTakeThemBack()
        {
            var game = new TrackMinigame(Tuning(0), new System.Random(4));
            int ticks = 0;
            while (game.Hits < 1 && !game.Done && ticks++ < MaxTicks)
                game.Tick(Step, new CaptureMinigameInput { Down = true, Point = game.Insect });
            Assert.AreEqual(1, game.Hits);

            RunUntilDone(game, Idle);   // 손을 떼면 게이지는 빠진다
            Assert.Less(game.Gauge, 1f / 3f);
            Assert.AreEqual(1, game.Hits);
        }

        [TestCase(0f, 0)]
        [TestCase(0.33f, 0)]
        [TestCase(0.34f, 1)]
        [TestCase(0.67f, 2)]
        [TestCase(0.99f, 2)]
        [TestCase(1f, 3)]
        public void Track_HitsForGauge_ThirdsOfTheGauge(float gauge, int expected)
        {
            Assert.AreEqual(expected, TrackMinigame.HitsForGauge(gauge));
        }

        [Test]
        public void Track_BetterNet_HasBiggerRadius()
        {
            float basic = new TrackMinigame(Tuning(1), new System.Random(1)).NetRadius;
            float gold = new TrackMinigame(Tuning(1, 0.55f, 1.6f, 1.6f), new System.Random(1)).NetRadius;
            Assert.Greater(gold, basic);
        }

        // ── 던지기 ──

        [Test]
        public void Toss_ThrowingWhereTheInsectIs_HitsWhenItBarelyMoves()
        {
            // 속도 배율을 거의 0으로 — 날아가는 0.45초 동안 곤충이 그물 반경을 못 벗어난다.
            var game = new TossMinigame(Tuning(0, 0.02f), new System.Random(1));
            RunUntilDone(game, g =>
            {
                var toss = (TossMinigame)g;
                return new CaptureMinigameInput { Pressed = !toss.InFlight, Point = toss.Insect };
            });

            Assert.IsTrue(game.Done);
            Assert.AreEqual(0, game.ThrowsLeft);
            Assert.AreEqual(3, game.Hits);
        }

        [Test]
        public void Toss_ThrowingAtAnEmptyCorner_Misses()
        {
            var game = new TossMinigame(Tuning(0), new System.Random(1));
            RunUntilDone(game, g => new CaptureMinigameInput { Pressed = true, Point = Vector2.zero });

            Assert.IsTrue(game.Done);
            Assert.AreEqual(0, game.ThrowsLeft);
            Assert.AreEqual(0, game.Hits);
        }

        [Test]
        public void Toss_OnlyOneNetInTheAirAtATime()
        {
            var game = new TossMinigame(Tuning(0), new System.Random(1));
            for (int i = 0; i < 5; i++)
                game.Tick(Step, new CaptureMinigameInput { Pressed = true, Point = Vector2.zero });
            Assert.AreEqual(TossMinigame.Throws - 1, game.ThrowsLeft);
        }

        [Test]
        public void Toss_NoThrows_TimesOutWithZero()
        {
            var game = new TossMinigame(Tuning(0), new System.Random(1));
            RunUntilDone(game, Idle);
            Assert.IsTrue(game.Done);
            Assert.AreEqual(0, game.Hits);
            Assert.AreEqual(TossMinigame.Throws, game.ThrowsLeft);
        }

        [Test]
        public void Toss_PathStaysInsideTheBoard()
        {
            for (float t = 0f; t < 40f; t += 0.05f)
            {
                Vector2 p = TossMinigame.PathPoint(t);
                Assert.IsTrue(p.x > 0f && p.x < CaptureMinigame.BoardWidth, $"t={t}: x {p.x}");
                Assert.IsTrue(p.y > 0f && p.y < CaptureMinigame.BoardHeight, $"t={t}: y {p.y}");
            }
        }
    }
}
#endif
