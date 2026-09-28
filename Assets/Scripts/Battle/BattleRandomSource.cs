using System;

namespace InsectGame.Battle
{
    /// <summary>
    /// Combat-owned random stream. Rendering, particle counts, frame rate and presentation
    /// speed cannot consume it. The historical raid interface also serves ordinary battles.
    /// </summary>
    public sealed class BattleRandomSource : IRaidRandomSource
    {
        private readonly Random random;

        public BattleRandomSource() : this(Guid.NewGuid().GetHashCode()) { }
        public BattleRandomSource(int seed) { random = new Random(seed); }

        public float Next01()
        {
            // Casting a double very close to one can round up; preserve the [0,1) contract.
            return Math.Min(0.99999994f, (float)random.NextDouble());
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            return random.Next(minInclusive, maxExclusive);
        }
    }
}
