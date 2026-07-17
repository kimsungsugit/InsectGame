namespace InsectGame.Data
{
    public enum InsectElement
    {
        None,
        Bug,
        Leaf,
        Water,
        Wind,
        Electric,
        Earth,
        Poison,
        Light,
        Dark,
        Metal
    }

    public static class InsectTypeChart
    {
        public const float StrongMultiplier = 1.5f;
        public const float ResistMultiplier = 0.67f;
        public const float SameTypeAttackBonus = 1.2f;

        public static float GetEffectiveness(InsectElement attack, InsectElement primary, InsectElement secondary)
        {
            if (attack == InsectElement.None) return 1f;

            float result = GetSingleEffectiveness(attack, primary);
            if (secondary != InsectElement.None && secondary != primary)
                result *= GetSingleEffectiveness(attack, secondary);
            return UnityEngine.Mathf.Clamp(result, 0.45f, 2.25f);
        }

        public static float GetSameTypeBonus(InsectElement attack, InsectElement primary, InsectElement secondary)
        {
            if (attack == InsectElement.None) return 1f;
            return attack == primary || attack == secondary ? SameTypeAttackBonus : 1f;
        }

        public static string GetDisplayName(InsectElement element)
        {
            switch (element)
            {
                case InsectElement.Bug: return "벌레";
                case InsectElement.Leaf: return "풀";
                case InsectElement.Water: return "물";
                case InsectElement.Wind: return "바람";
                case InsectElement.Electric: return "전기";
                case InsectElement.Earth: return "땅";
                case InsectElement.Poison: return "독";
                case InsectElement.Light: return "빛";
                case InsectElement.Dark: return "어둠";
                case InsectElement.Metal: return "강철";
                default: return "무속성";
            }
        }

        private static float GetSingleEffectiveness(InsectElement attack, InsectElement defense)
        {
            if (defense == InsectElement.None) return 1f;
            if (IsStrongAgainst(attack, defense)) return StrongMultiplier;
            if (IsStrongAgainst(defense, attack)) return ResistMultiplier;
            return 1f;
        }

        private static bool IsStrongAgainst(InsectElement attack, InsectElement defense)
        {
            switch (attack)
            {
                case InsectElement.Bug:
                    return defense == InsectElement.Leaf || defense == InsectElement.Dark;
                case InsectElement.Leaf:
                    return defense == InsectElement.Water || defense == InsectElement.Earth;
                case InsectElement.Water:
                    return defense == InsectElement.Earth || defense == InsectElement.Metal;
                case InsectElement.Wind:
                    return defense == InsectElement.Bug || defense == InsectElement.Leaf;
                case InsectElement.Electric:
                    return defense == InsectElement.Water || defense == InsectElement.Wind;
                case InsectElement.Earth:
                    return defense == InsectElement.Electric || defense == InsectElement.Poison || defense == InsectElement.Metal;
                case InsectElement.Poison:
                    return defense == InsectElement.Leaf || defense == InsectElement.Bug;
                case InsectElement.Light:
                    return defense == InsectElement.Dark || defense == InsectElement.Poison;
                case InsectElement.Dark:
                    return defense == InsectElement.Light || defense == InsectElement.Bug;
                case InsectElement.Metal:
                    return defense == InsectElement.Bug || defense == InsectElement.Wind;
                default:
                    return false;
            }
        }
    }
}
