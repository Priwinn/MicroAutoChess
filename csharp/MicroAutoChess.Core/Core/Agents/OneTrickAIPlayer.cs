using System;
using System.Collections.Generic;
using System.Linq;

namespace MicroAutoChess.Core.Agents
{
    /// <summary>
    /// AI player that picks a single UnitType at the start and only ever buys
    /// that type. If the shop has none, it rerolls and checks again, repeating
    /// until it runs out of gold.
    /// </summary>
    public class OneTrickAIPlayer : AIPlayer
    {
        private readonly Random _rng;
        private UnitType _chosenType;
        private bool _doneBuying;
        private List<(int, int)>? _shuffledPositions;
        private int _posIdx;

        public OneTrickAIPlayer(int playerId, UnitType? forcedType = null, int? seed = null)
            : base(playerId)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
            // Allow forcing a type for tests; otherwise pick randomly at first preparation.
            if (forcedType.HasValue)
                _chosenType = forcedType.Value;
            else
                _chosenType = PickRandomUnitType();
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

            // Phase 1: Buy our chosen type or reroll until gold runs out
            if (!_doneBuying)
            {
                var action = TryBuyOrReroll(state.PlayerId, player, state.Params);
                if (action != null) return action;
                _doneBuying = true;
            }

            // Phase 2: Place bench units onto the board
            return TryPlaceOne(state.PlayerId, player, board, state.Params);
        }

        private PlayerAction? TryBuyOrReroll(int playerId, Player player, GameParams gameParams)
        {
            // Look for a shop slot with our chosen type that we can afford
            for (int i = 0; i < player.ShopUnits.Count; i++)
            {
                var unit = player.ShopUnits[i];
                if (unit != null && unit.UnitType == _chosenType && player.Gold >= unit.GetCost())
                    return new PlayerAction(playerId, PlayerActionType.BUY_UNIT) { ShopIndex = i };
            }

            // No matching unit available — try to reroll if we can afford it
            if (player.Gold >= gameParams.RerollCost)
                return new PlayerAction(playerId, PlayerActionType.REROLL_SHOP);

            // Can't buy or reroll — done
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

        private UnitType PickRandomUnitType()
        {
            var types = Enum.GetValues(typeof(UnitType)).Cast<UnitType>()
                .Where(t => t != UnitType.NONE).ToArray();
            return types[_rng.Next(types.Length)];
        }
    }
}
