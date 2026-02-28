using System;
using System.Collections.Generic;
using System.Linq;

namespace MicroAutoChess.Core.Agents
{
    /// <summary>
    /// AI player that picks two UnitTypes and only buys those.
    /// If no units of either type are in the shop, it rerolls and checks again,
    /// repeating until gold runs out.
    /// </summary>
    public class TwoTrickAIPlayer : AIPlayer
    {
        private readonly Random _rng;
        private readonly UnitType _type1;
        private readonly UnitType _type2;
        private bool _doneBuying;
        private List<(int, int)>? _shuffledPositions;
        private int _posIdx;

        public TwoTrickAIPlayer(int playerId, UnitType? forcedType1 = null, UnitType? forcedType2 = null, int? seed = null)
            : base(playerId)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();

            if (forcedType1.HasValue && forcedType2.HasValue)
            {
                _type1 = forcedType1.Value;
                _type2 = forcedType2.Value;
            }
            else
            {
                var picked = PickTwoRandomUnitTypes();
                _type1 = picked.Item1;
                _type2 = picked.Item2;
            }
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
                if (unit != null && (unit.UnitType == _type1 || unit.UnitType == _type2) && player.Gold >= unit.GetCost())
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

            for (int bi = 0; bi < gameParams.BenchSize; bi++)
            {
                var benchUnit = board.GetBenchUnit(player.team, bi);
                if (benchUnit == null) continue;

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

        private (UnitType, UnitType) PickTwoRandomUnitTypes()
        {
            var types = Enum.GetValues(typeof(UnitType)).Cast<UnitType>()
                .Where(t => t != UnitType.NONE).ToArray();
            var first = types[_rng.Next(types.Length)];
            UnitType second;
            do { second = types[_rng.Next(types.Length)]; } while (second == first);
            return (first, second);
        }
    }
}
