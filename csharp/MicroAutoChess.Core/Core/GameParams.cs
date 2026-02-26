using System.Collections.Generic;

namespace MicroAutoChess.Core
{
    public class GameParams
    {
        public int MaxRounds { get; set; } = 10;
        public int InitialPlayerHealth { get; set; } = 100;
        public int InitialPlayerGold { get; set; } = 5;
        public int InitialPlayerLevel { get; set; } = 1;
        public string BoardType { get; set; } = "hex";
        public (int Width, int Height) BoardSize { get; set; } = (7, 8);
        public int BenchSize { get; set; } = 9;
        public bool UnitLevelUp { get; set; } = true;

        // PvP parameters
        public int PlayerCount { get; set; } = 8;
        public int ShopSize { get; set; } = 5;
        public int RerollCost { get; set; } = 2;

        // Economy
        public int BaseGoldIncome { get; set; } = 5;
        public int InterestPerGold { get; set; } = 10;
        public int InterestCap { get; set; } = 5;
        public int XpPerRound { get; set; } = 2;
        public int BuyXpCost { get; set; } = 4;
        public int BuyXpAmount { get; set; } = 4;
        public int BaseCombatDamage { get; set; } = 2;

        // Timer durations (seconds)
        public double PreparationTimerSeconds { get; set; } = 30.0;
        public double CombatTimerSeconds { get; set; } = 60.0;

        // Shared unit bag pool sizes per rarity
        public Dictionary<UnitRarity, int> PoolSizes { get; set; } = new()
        {
            { UnitRarity.COMMON,    29 },
            { UnitRarity.UNCOMMON,  22 },
            { UnitRarity.RARE,      16 },
            { UnitRarity.EPIC,      10 },
            { UnitRarity.LEGENDARY,  5 },
        };

        // Level-based rarity roll probabilities (rows = player level 1-5+, cols = COMMON..LEGENDARY)
        public double[][] RarityProbabilities { get; set; } = new[]
        {
            new[] { 1.0,  0.0,  0.0,  0.0,  0.0  },  // Level 1
            new[] { 0.6,  0.3,  0.1,  0.0,  0.0  },  // Level 2
            new[] { 0.5,  0.35, 0.13, 0.02, 0.0  },  // Level 3
            new[] { 0.4,  0.35, 0.2,  0.05, 0.0  },  // Level 4
            new[] { 0.3,  0.3,  0.25, 0.13, 0.02 },  // Level 5+
        };
    }
}
