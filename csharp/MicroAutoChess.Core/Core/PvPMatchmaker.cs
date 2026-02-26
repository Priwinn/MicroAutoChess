using System;
using System.Collections.Generic;
using System.Linq;

namespace MicroAutoChess.Core
{
    /// <summary>
    /// Represents one combat pairing for a round.
    /// </summary>
    public class MatchPairing
    {
        /// <summary>The player whose board hosts the fight (home arena).</summary>
        public int Player1Id { get; set; }

        /// <summary>The opponent (their units are cloned onto Player1's board as TEAM_1).</summary>
        public int Player2Id { get; set; }

        /// <summary>True if Player2 is a "ghost" (clone of an eliminated or already-used player).</summary>
        public bool IsGhostMatch { get; set; }
    }

    /// <summary>
    /// Handles pairing alive players each round for combat.
    /// Supports odd player counts via ghost armies (clone of a random opponent).
    /// Tracks recent opponents to avoid repeat matchups when possible.
    /// </summary>
    public class PvPMatchmaker
    {
        private readonly Random _rng;

        // playerId → list of recent opponent ids (most recent last)
        private readonly Dictionary<int, List<int>> _recentOpponents = new();

        // How many rounds back to consider for anti-repeat
        public int HistoryDepth { get; set; } = 3;

        public PvPMatchmaker(Random rng)
        {
            _rng = rng;
        }

        /// <summary>
        /// Generate pairings for a round. Each alive player fights exactly once.
        /// With odd count, one player fights a ghost army (clone of a random opponent).
        /// Returns a list of MatchPairings. Each pairing means:
        ///   - Player1 hosts the combat on their board
        ///   - Player2's units are cloned onto the board as the enemy
        /// </summary>
        public List<MatchPairing> GeneratePairings(List<int> alivePlayerIds)
        {
            if (alivePlayerIds.Count < 2)
                return new List<MatchPairing>();

            var ids = new List<int>(alivePlayerIds);
            Shuffle(ids);

            int? ghostSourceId = null;
            if (ids.Count % 2 != 0)
            {
                // Odd count: last player fights a ghost
                ghostSourceId = ids[_rng.Next(ids.Count - 1)]; // pick from paired players
            }

            var pairings = new List<MatchPairing>();

            // Pair up players, trying to avoid recent opponents
            var unpaired = new List<int>(ids);
            if (ghostSourceId.HasValue && unpaired.Count % 2 != 0)
            {
                // Remove the last player temporarily; they'll get the ghost match
                int ghostFighter = unpaired[^1];
                unpaired.RemoveAt(unpaired.Count - 1);

                PairPlayers(unpaired, pairings);

                pairings.Add(new MatchPairing
                {
                    Player1Id = ghostFighter,
                    Player2Id = ghostSourceId.Value,
                    IsGhostMatch = true
                });
            }
            else
            {
                PairPlayers(unpaired, pairings);
            }

            // Record history
            foreach (var p in pairings)
            {
                RecordOpponent(p.Player1Id, p.Player2Id);
                if (!p.IsGhostMatch)
                    RecordOpponent(p.Player2Id, p.Player1Id);
            }

            return pairings;
        }

        private void PairPlayers(List<int> players, List<MatchPairing> pairings)
        {
            // Simple greedy: try to avoid recent opponents
            var available = new HashSet<int>(players);
            var sorted = new List<int>(players);

            foreach (var pid in sorted)
            {
                if (!available.Contains(pid)) continue;
                available.Remove(pid);

                // Find best opponent: prefer someone not recently fought
                int? bestOpponent = null;
                int bestScore = int.MaxValue;
                foreach (var oid in available)
                {
                    int recency = GetRecency(pid, oid);
                    if (recency < bestScore)
                    {
                        bestScore = recency;
                        bestOpponent = oid;
                    }
                }

                if (!bestOpponent.HasValue) break;
                available.Remove(bestOpponent.Value);

                // Randomly decide who hosts
                if (_rng.Next(2) == 0)
                    pairings.Add(new MatchPairing { Player1Id = pid, Player2Id = bestOpponent.Value });
                else
                    pairings.Add(new MatchPairing { Player1Id = bestOpponent.Value, Player2Id = pid });
            }
        }

        /// <summary>
        /// Returns 0 if never fought recently, or (HistoryDepth - index) for how recently they fought.
        /// Lower = better (longer ago or never).
        /// </summary>
        private int GetRecency(int playerId, int opponentId)
        {
            if (!_recentOpponents.TryGetValue(playerId, out var history))
                return 0;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (history[i] == opponentId)
                    return history.Count - i;
            }
            return 0;
        }

        private void RecordOpponent(int playerId, int opponentId)
        {
            if (!_recentOpponents.ContainsKey(playerId))
                _recentOpponents[playerId] = new List<int>();
            _recentOpponents[playerId].Add(opponentId);
            // Trim to history depth
            while (_recentOpponents[playerId].Count > HistoryDepth)
                _recentOpponents[playerId].RemoveAt(0);
        }

        private void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
