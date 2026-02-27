using System.Collections.Generic;

namespace MicroAutoChess.Core
{
    public class LevelConfig
    {
        public (int, int) BoardSize { get; set; }
        public string BoardType { get; set; } = "hex";
        public Dictionary<(int, int), UnitType> Units { get; set; } = new Dictionary<(int, int), UnitType>();
        public int BudgetInc { get; set; } = 0;

        public LevelConfig() { }

        public LevelConfig((int, int) boardSize, Dictionary<(int, int), UnitType> units, int budgetInc = 0, string boardType = "hex")
        {
            BoardSize = boardSize;
            Units = units ?? new Dictionary<(int, int), UnitType>();
            BudgetInc = budgetInc;
            BoardType = boardType;
        }
    }

    public static class Levels
    {
        public static readonly LevelConfig LEVEL1 = new LevelConfig((7, 8), new Dictionary<(int, int), UnitType>
        {
            [(3,3)] = UnitType.TANK,
            [(2,1)] = UnitType.ARCHER
        }, budgetInc: 3, boardType: "hex");

        public static readonly LevelConfig LEVEL2 = new LevelConfig((7,8), new Dictionary<(int,int), UnitType>
        {
            [(3,3)] = UnitType.TANK,
            [(2,1)] = UnitType.MAGE,
            [(2,3)] = UnitType.WARRIOR
        }, budgetInc:0);

        public static readonly LevelConfig LEVEL3 = new LevelConfig((7,8), new Dictionary<(int,int), UnitType>
        {
            [(3,3)] = UnitType.TANK,
            [(0,0)] = UnitType.ARCHER,
            [(1,0)] = UnitType.ARCHER
        }, budgetInc:1);

        public static readonly LevelConfig LEVEL4 = new LevelConfig((7,8), new Dictionary<(int,int), UnitType>
        {
            [(3,3)] = UnitType.TANK,
            [(2,3)] = UnitType.WARRIOR,
            [(0,0)] = UnitType.ARCHER,
            [(1,0)] = UnitType.ARCHER
        }, budgetInc:0);

        public static readonly LevelConfig LEVEL5 = new LevelConfig((7,8), new Dictionary<(int,int), UnitType>
        {
            [(3,3)] = UnitType.TANK,
            [(2,3)] = UnitType.TANK,
            [(0,2)] = UnitType.ASSASSIN,
            [(1,2)] = UnitType.ASSASSIN,
            [(2,2)] = UnitType.ASSASSIN
        }, budgetInc:1);

        public static readonly LevelConfig LEVEL6 = new LevelConfig((7,8), new Dictionary<(int,int), UnitType>
        {
            [(0,0)] = UnitType.ARCHER,
            [(1,0)] = UnitType.ARCHER,
            [(2,0)] = UnitType.ARCHER,
            [(3,0)] = UnitType.ARCHER,
            [(4,0)] = UnitType.ARCHER,
            [(5,0)] = UnitType.ARCHER,
            [(6,0)] = UnitType.ARCHER
        }, budgetInc:0);

        public static readonly LevelConfig LEVEL7 = new LevelConfig((7,8), new Dictionary<(int,int), UnitType>
        {
            [(0,3)] = UnitType.TANK,
            [(1,3)] = UnitType.TANK,
            [(2,3)] = UnitType.TANK,
            [(3,3)] = UnitType.TANK,
            [(4,3)] = UnitType.TANK,
            [(5,3)] = UnitType.TANK
        }, budgetInc:0);

        public static readonly LevelConfig LEVEL8 = new LevelConfig((7,8), new Dictionary<(int,int), UnitType>
        {
            [(0,3)] = UnitType.WARRIOR,
            [(1,3)] = UnitType.WARRIOR,
            [(2,3)] = UnitType.WARRIOR,
            [(3,3)] = UnitType.WARRIOR,
            [(4,3)] = UnitType.WARRIOR,
            [(5,3)] = UnitType.WARRIOR,
            [(6,3)] = UnitType.WARRIOR
        }, budgetInc:1);

        public static readonly LevelConfig LEVEL9 = new LevelConfig((7,8), new Dictionary<(int,int), UnitType>
        {
            [(0,0)] = UnitType.MAGE,
            [(1,0)] = UnitType.MAGE,
            [(2,0)] = UnitType.MAGE,
            [(3,0)] = UnitType.MAGE,
            [(4,0)] = UnitType.MAGE,
            [(5,0)] = UnitType.MAGE,
            [(6,0)] = UnitType.MAGE,
            [(3,3)] = UnitType.TANK
        }, budgetInc:1);

        public static readonly LevelConfig LEVELTANKSTEST = new LevelConfig((7,8), GenerateTankGrid(7,4), budgetInc:9);
        public static readonly LevelConfig LEVELTANKS = new LevelConfig((7,8), GenerateTankGrid(7,4), budgetInc:2);

        public static readonly LevelConfig TANKSSOLUTION = new LevelConfig((7,8), new Dictionary<(int,int), UnitType>
        {
            [(1,4)] = UnitType.TANK,
            [(2,4)] = UnitType.TANK,
            [(3,4)] = UnitType.TANK,
            [(4,4)] = UnitType.TANK,
            [(5,4)] = UnitType.TANK,
            [(6,4)] = UnitType.TANK,
            [(6,6)] = UnitType.ARCHER,
            [(5,7)] = UnitType.ARCHER,
            [(6,7)] = UnitType.ARCHER
        }, budgetInc:9);

        public static readonly LevelConfig TANKSSOLUTIONMIRROR = new LevelConfig((7,8), new Dictionary<(int,int), UnitType>
        {
            [(1,3)] = UnitType.TANK,
            [(2,3)] = UnitType.TANK,
            [(3,3)] = UnitType.TANK,
            [(4,3)] = UnitType.TANK,
            [(5,3)] = UnitType.TANK,
            [(0,3)] = UnitType.TANK,
            [(0,1)] = UnitType.ARCHER,
            [(1,0)] = UnitType.ARCHER,
            [(0,0)] = UnitType.ARCHER
        }, budgetInc:9);

        public static readonly List<LevelConfig> LEVELS = new List<LevelConfig>
        {
            LEVELTANKSTEST,
            LEVEL1,
            LEVEL2,
            LEVEL3,
            LEVEL4,
            LEVEL5,
            LEVEL6,
            LEVEL7,
            LEVEL8,
            LEVEL9,
            LEVELTANKS
        };

        private static Dictionary<(int,int), UnitType> GenerateTankGrid(int width, int rows)
        {
            var d = new Dictionary<(int,int), UnitType>();
            for (int i = 0; i < width; i++) for (int j = 0; j < rows; j++) d[(i,j)] = UnitType.TANK;
            return d;
        }
    }
}
