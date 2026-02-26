using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;

namespace MicroAutoChess.Core
{
    public class PvERoundManager
    {
        // Configs may be LevelConfig objects or saved unit lists (List<Unit>)
        private List<object> _configs;
        public IReadOnlyList<object> Configs => _configs.AsReadOnly();

        public int InitialBudget { get; private set; }
        public int PlayerBudget { get; private set; }

        private int _roundIndex = 0;

        public PvERoundManager(List<object> configs, int initialBudget = 0)
        {
            _configs = configs ?? new List<object>();
            int firstInc = 0;
            if (_configs.Count > 0 && _configs[0] is LevelConfig lc) firstInc = lc.BudgetInc;
            InitialBudget = initialBudget + firstInc;
            PlayerBudget = InitialBudget;
            _roundIndex = 0;
        }

        private List<Unit> CloneUnitList(List<Unit>? units)
        {
            if (units == null) return new List<Unit>();
            var outList = new List<Unit>();
            foreach (var u in units)
            {
                var c = u.Clone();
                c.Position = null;
                outList.Add(c);
            }
            return outList;
        }

        public void AddConfig(List<Unit> units)
        {
            _configs.Add(CloneUnitList(units));
        }

        public int CurrentRound() => _roundIndex;
        public int NumRounds() => _configs.Count;

        public bool AdvanceRound()
        {
            if (_roundIndex + 1 < _configs.Count)
            {
                _roundIndex += 1;
                int inc = 0;
                if (_configs[_roundIndex] is LevelConfig lc) inc = lc.BudgetInc;
                PlayerBudget = PlayerBudget + inc;
                return true;
            }
            return false;
        }

        public void ResetToStart()
        {
            _roundIndex = 0;
            PlayerBudget = InitialBudget;
        }

        // Setup round returns (board, enemyUnits, playerUnits)
        public (Board, List<Unit>, List<Unit>) SetupRound()
        {
            if (_configs.Count == 0) throw new InvalidOperationException("No configs available");
            var cfg = _configs[_roundIndex];
            if (cfg is LevelConfig lc) return SetupBoardFromConfig(lc);
            if (cfg is List<Unit> units) // treat as snapshot list
            {
                var board = new HexBoard((7,8));
                var placed = new List<Unit>();
                foreach (var u in units)
                {
                    var nu = u.Clone();
                    nu.Position = u.Position;
                    if (nu.Position != null && board.IsValidPosition(nu.Position.Value))
                    {
                        board.PlaceBoardUnit(nu, nu.Position.Value);
                        placed.Add(nu);
                    }
                }
                return (board, placed, new List<Unit>());
            }
            throw new ArgumentException("Unsupported config type");
        }

        public (Board, Player, Player) ApplyRoundToBoard(Board board, Player player1, Player player2)
        {
            // clear board
            board.ResetBoard();

            // prepare enemy units (fresh clones) and place them as team 1
            var cfg = _configs[_roundIndex];
            List<Unit> enemyUnits = new List<Unit>();
            if (cfg is LevelConfig lc)
            {
                enemyUnits = PlaceUnitsFromConfig(board, lc, team: Team.TEAM_1);
            }
            else if (cfg is List<Unit> unitList)
            {
                // re-place provided units (assume they are pre-positioned)
                foreach (var u in unitList)
                {
                    var nu = u.Clone();
                    nu.Team = Team.TEAM_1;
                    if (nu.Position != null)
                    {
                        board.PlaceBoardUnit(nu, nu.Position.Value);
                        enemyUnits.Add(nu);
                    }
                }
            }

            // set player1 units-on-board from enemyUnits (mirror Python behavior where team1 is enemy)
            player1.UnitsOnBoard.Clear();
            foreach (var eu in enemyUnits)
            {
                if (eu.Position != null) player1.UnitsOnBoard[eu.Position.Value] = eu;
            }

            // reset/place player2 units on board using player's UnitsOnBoard dictionary
            foreach (var kv in player2.UnitsOnBoard)
            {
                var u = kv.Value;
                if (u == null) continue;
                u.RoundReset();
                if (u.Position != null) board.PlaceBoardUnit(u, u.Position.Value);
            }

            return (board, player1, player2);
        }

        // Helper: create board and place units from LevelConfig
        private (Board, List<Unit>, List<Unit>) SetupBoardFromConfig(LevelConfig cfg)
        {
            Board board;
            if (cfg.BoardType == "hex") board = new HexBoard(cfg.BoardSize);
            else if (cfg.BoardType == "square") board = new SquareBoard(cfg.BoardSize);
            else if (cfg.BoardType == "diagonal_square") board = new DiagonalSquareBoard(cfg.BoardSize);
            else board = new HexBoard(cfg.BoardSize);

            var enemy = PlaceUnitsFromConfig(board, cfg, team: Team.TEAM_1);
            return (board, enemy, new List<Unit>());
        }

        private List<Unit> PlaceUnitsFromConfig(Board board, LevelConfig cfg, Team team = Team.TEAM_1)
        {
            var placed = new List<Unit>();
            foreach (var kv in cfg.Units)
            {
                var pos = kv.Key;
                var ut = kv.Value;
                if (board.IsValidPosition(pos) && board.IsEmpty(pos))
                {
                    var u = new Unit(ut, UnitRarity.COMMON, team) { Position = pos };
                    u.CurrentHealth = u.GetMaxHealth();
                    board.PlaceBoardUnit(u, pos);
                    placed.Add(u);
                }
            }
            return placed;
        }

        // Spend budget; returns true if there was enough budget
        public bool SpendBudget(int amount)
        {
            if (amount <= 0) return true;
            if (PlayerBudget >= amount)
            {
                PlayerBudget -= amount;
                return true;
            }
            return false;
        }

        // Add budget (refunds, rewards)
        public void AddBudget(int amount)
        {
            if (amount <= 0) return;
            PlayerBudget += amount;
        }
    }
}
