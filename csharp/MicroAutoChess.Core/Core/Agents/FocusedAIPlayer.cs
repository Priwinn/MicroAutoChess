using System;
using System.Collections.Generic;
using System.Linq;

namespace MicroAutoChess.Core.Agents
{
    /// <summary>
    /// Generalized AI player that only buys from a specified set of UnitTypes.
    /// If no matching units are in the shop it rerolls, repeating until gold runs out.
    /// When placing units it prioritizes higher-level units first.
    /// </summary>
    public class FocusedAIPlayer : AIPlayer
    {
        private readonly Random _rng;
        private readonly HashSet<UnitType> _targetTypes;
        private bool _doneBuying;
        private List<(int, int)>? _shuffledPositions;
        private int _posIdx;

        public FocusedAIPlayer(int playerId, IEnumerable<UnitType> targetTypes, int? seed = null)
            : base(playerId)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
            _targetTypes = new HashSet<UnitType>(targetTypes.Where(t => t != UnitType.NONE));
            if (_targetTypes.Count == 0)
                _targetTypes = new HashSet<UnitType>(
                    Enum.GetValues(typeof(UnitType)).Cast<UnitType>().Where(t => t != UnitType.NONE));
        }

        public override void BeginPreparation(PlayerGameState state)
        {
            _doneBuying = false;
            _shuffledPositions = null;
            _posIdx = 0;
        }

        public override PlayerAction? GetNextAction(PlayerGameState state)
        {
            var player = state.Player;
            var board = state.Board;

            if (!_doneBuying)
            {
                var action = TryBuyOrReroll(state.PlayerId, player, state.Params);
                if (action != null) return action;
                _doneBuying = true;
            }

            return TryPlaceOne(state.PlayerId, player, board, state.Params);
        }

        private PlayerAction? TryBuyOrReroll(int playerId, Player player, GameParams gameParams)
        {
            for (int i = 0; i < player.ShopUnits.Count; i++)
            {
                var unit = player.ShopUnits[i];
                if (unit != null && _targetTypes.Contains(unit.UnitType) && player.Gold >= unit.GetCost())
                    return new PlayerAction(playerId, PlayerActionType.BUY_UNIT) { ShopIndex = i };
            }

            if (player.Gold >= gameParams.RerollCost)
                return new PlayerAction(playerId, PlayerActionType.REROLL_SHOP);

            return null;
        }

        private PlayerAction? TryPlaceOne(int playerId, Player player, Board board, GameParams gameParams)
        {
            if (_shuffledPositions == null)
            {
                _shuffledPositions = board.GetInitialPositions(player.team).ToList();
                for (int i = _shuffledPositions.Count - 1; i > 0; i--)
                {
                    int j = _rng.Next(i + 1);
                    (_shuffledPositions[i], _shuffledPositions[j]) = (_shuffledPositions[j], _shuffledPositions[i]);
                }
                _posIdx = 0;
            }

            // Collect bench units and sort by level descending so higher-level units are placed first
            var benchUnits = new List<(int benchIndex, Unit unit)>();
            for (int bi = 0; bi < gameParams.BenchSize; bi++)
            {
                var u = board.GetBenchUnit(player.team, bi);
                if (u != null) benchUnits.Add((bi, u));
            }
            benchUnits.Sort((a, b) => b.unit.Level.CompareTo(a.unit.Level));

            foreach (var (bi, benchUnit) in benchUnits)
            {
                while (_posIdx < _shuffledPositions.Count && !board.IsEmpty(_shuffledPositions[_posIdx]))
                    _posIdx++;

                if (_posIdx >= _shuffledPositions.Count) return null;

                return new PlayerAction(playerId, PlayerActionType.MOVE_UNIT)
                {
                    FromPosition = benchUnit.Position!.Value,
                    ToPosition = _shuffledPositions[_posIdx]
                };
            }

            return null;
        }
    }
}
