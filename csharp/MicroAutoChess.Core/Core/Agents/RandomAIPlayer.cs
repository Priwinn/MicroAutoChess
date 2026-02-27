using System;
using System.Collections.Generic;
using System.Linq;

namespace MicroAutoChess.Core.Agents
{
    /// <summary>
    /// AI player that buys random units from the shop and places them
    /// at random valid board positions until it runs out of gold or space.
    /// Returns one action per GetNextAction call (buy first, then place).
    /// </summary>
    public class RandomAIPlayer : AIPlayer
    {
        private readonly Random _rng;
        private bool _doneBuying;
        private List<(int, int)>? _shuffledPositions;
        private int _posIdx;

        public RandomAIPlayer(int playerId, int? seed = null) : base(playerId)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
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

            // Phase 1: Buy one unit at a time
            if (!_doneBuying)
            {
                var buyAction = TryBuyOne(state.PlayerId, player);
                if (buyAction != null) return buyAction;
                _doneBuying = true;
            }

            // Phase 2: Place one bench unit at a time
            return TryPlaceOne(state.PlayerId, player, board, state.Params);
        }

        private PlayerAction? TryBuyOne(int playerId, Player player)
        {
            var available = new List<int>();
            for (int i = 0; i < player.ShopUnits.Count; i++)
            {
                if (player.ShopUnits[i] != null)
                {
                    int cost = player.ShopUnits[i]!.GetCost();
                    if (player.Gold >= cost)
                        available.Add(i);
                }
            }
            if (available.Count == 0) return null;

            int pick = available[_rng.Next(available.Count)];
            return new PlayerAction(playerId, PlayerActionType.BUY_UNIT) { ShopIndex = pick };
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

            // Find the next bench unit to place
            for (int bi = 0; bi < gameParams.BenchSize; bi++)
            {
                var benchUnit = board.GetBenchUnit(player.team, bi);
                if (benchUnit == null) continue;

                // Find next empty valid position
                while (_posIdx < _shuffledPositions.Count && !board.IsEmpty(_shuffledPositions[_posIdx]))
                    _posIdx++;

                if (_posIdx >= _shuffledPositions.Count) return null;

                return new PlayerAction(playerId, PlayerActionType.MOVE_UNIT)
                {
                    FromPosition = benchUnit.Position!.Value,
                    ToPosition = _shuffledPositions[_posIdx]
                };
            }

            return null; // no bench units left
        }
    }
}
