///Notes: we need to block unit level up during battle and check for level up on preparation phase start.
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace MicroAutoChess.Core
{
    /// <summary>
    /// Result of a single combat pairing within a round.
    /// </summary>
    public class CombatResult
    {
        public int Player1Id { get; set; }
        public int Player2Id { get; set; }
        /// <summary>1 = Player1 won, 2 = Player2 won, 0 = draw.</summary>
        public int Winner { get; set; }
        /// <summary>Damage dealt to the loser's health pool.</summary>
        public int DamageToLoser { get; set; }
        /// <summary>Number of surviving enemy units (used for damage calc).</summary>
        public int SurvivingUnits { get; set; }
        public bool IsGhostMatch { get; set; }
    }

    /// <summary>
    /// Central manager for an N-player PvP autochess game.
    /// 
    /// All player actions route through this manager. The game flows:
    ///   PREPARATION → COMBAT → ROUND_END → (repeat or GAME_OVER)
    /// 
    /// Key responsibilities:
    ///   - Shared unit bag for shops
    ///   - Matchmaking between players each round
    ///   - Processing player actions (buy/sell/reroll/move) with thread safety
    ///   - Timer-based phase transitions
    ///   - Running combat via CombatEngine with settable seeds
    ///   - Synchronized frame stepping for combat animation
    ///   - Applying round results (damage, gold income, XP)
    ///   - Tracking eliminations / win condition
    /// </summary>
    public class PvPGameManager
    {
        // --- Configuration ---
        public GameParams Params { get; }
        public int? MasterSeed { get; private set; }

        // --- State ---
        public PvPGamePhase Phase { get; private set; } = PvPGamePhase.PREPARATION;
        public int CurrentRound { get; private set; } = 0;

        // Players keyed by PlayerId
        private readonly Dictionary<int, Player> _players = new();
        // Each player has their own board (their "arena")
        private readonly Dictionary<int, Board> _boards = new();
        // Track which players are still alive
        private readonly HashSet<int> _alivePlayers = new();
        // Whether a player's shop is locked (persists across rerolls into next round)
        private readonly HashSet<int> _lockedShops = new();
        // Elimination order (first eliminated = index 0)
        private readonly List<int> _eliminationOrder = new();

        // Per-player action queues (players enqueue, tick processes one per player)
        private readonly Dictionary<int, ConcurrentQueue<PlayerAction>> _actionQueues = new();
        private readonly Dictionary<int, PlayerActionResult?> _lastActionResults = new();

        // Timer tracking
        private DateTime _phaseStartTime;

        // Shared systems
        public SharedUnitBag UnitBag { get; private set; }
        private PvPMatchmaker _matchmaker;
        private Random _rng;

        // Current round combat results
        private List<CombatResult> _lastRoundResults = new();
        public IReadOnlyList<CombatResult> LastRoundResults => _lastRoundResults.AsReadOnly();

        // Current round match pairings
        private List<MatchPairing> _currentPairings = new();
        public IReadOnlyList<MatchPairing> CurrentPairings => _currentPairings.AsReadOnly();

        // Combat engines for the current round (one per pairing)
        private readonly Dictionary<int, CombatEngine> _combatEngines = new();

        // Event log for player actions
        private readonly List<string> _actionLog = new();
        public IReadOnlyList<string> ActionLog => _actionLog.AsReadOnly();

        // =====================================================================
        // Construction
        // =====================================================================

        public PvPGameManager(int playerCount, GameParams? gameParams = null, int? masterSeed = null)
        {
            if (playerCount < 2) throw new ArgumentException("Need at least 2 players");
            Params = gameParams ?? new GameParams();
            MasterSeed = masterSeed;
            _rng = masterSeed.HasValue ? new Random(masterSeed.Value) : new Random();

            // Derive sub-seeds deterministically from master seed
            int bagSeed = _rng.Next();
            int matchmakerSeed = _rng.Next();

            UnitBag = new SharedUnitBag(Params, seed: bagSeed);
            _matchmaker = new PvPMatchmaker(new Random(matchmakerSeed));

            for (int i = 1; i <= playerCount; i++)
            {
                var player = new Player(i, (Team)(((i - 1) % 2) + 1), Params);
                _players[i] = player;
                _alivePlayers.Add(i);

                var board = CreateBoard();
                Team oppositeTeam = player.team == Team.TEAM_1 ? Team.TEAM_2 : Team.TEAM_1;
                board.Players[player.team] = player;
                board.Players[oppositeTeam] = new Player(0, oppositeTeam);
                _boards[i] = board;

                _actionQueues[i] = new ConcurrentQueue<PlayerAction>();
                _lastActionResults[i] = null;
            }
        }

        // =====================================================================
        // Accessors
        // =====================================================================

        public Player GetPlayer(int playerId) => _players[playerId];
        public Board GetBoard(int playerId) => _boards[playerId];
        public bool IsPlayerAlive(int playerId) => _alivePlayers.Contains(playerId);
        public int AlivePlayerCount => _alivePlayers.Count;
        public IReadOnlyCollection<int> AlivePlayerIds => _alivePlayers;
        public IReadOnlyList<int> EliminationOrder => _eliminationOrder.AsReadOnly();
        public int PlayerCount => _players.Count;

        /// <summary>Build a PlayerGameState snapshot for a given player.</summary>
        public PlayerGameState GetGameStateForPlayer(int playerId)
        {
            return new PlayerGameState(playerId, _players[playerId], _boards[playerId], Params, CurrentRound);
        }

        /// <summary>Get the CombatEngine for a player's current-round fight (only valid during COMBAT phase).</summary>
        public CombatEngine? GetCombatEngine(int playerId)
        {
            return _combatEngines.TryGetValue(playerId, out var engine) ? engine : null;
        }

        /// <summary>
        /// Get the host player ID for a given player's combat pairing.
        /// The host's board contains the active CombatEngine.
        /// Returns null if the player has no active pairing.
        /// </summary>
        public int? GetCombatHostForPlayer(int playerId)
        {
            if (_combatEngines.ContainsKey(playerId)) return playerId;
            var pairing = _currentPairings?.FirstOrDefault(p => p.Player2Id == playerId);
            if (pairing != null && _combatEngines.ContainsKey(pairing.Player1Id))
                return pairing.Player1Id;
            return null;
        }

        /// <summary>Seconds remaining in the current phase timer.</summary>
        public double GetRemainingPhaseTime()
        {
            double duration = Phase switch
            {
                PvPGamePhase.PREPARATION => Params.PreparationTimerSeconds,
                PvPGamePhase.COMBAT => Params.CombatTimerSeconds,
                _ => 0.0,
            };
            double elapsed = (DateTime.UtcNow - _phaseStartTime).TotalSeconds;
            return Math.Max(0.0, duration - elapsed);
        }

        /// <summary>Whether the current phase timer has expired.</summary>
        public bool IsPhaseTimerExpired()
        {
            return GetRemainingPhaseTime() <= 0.0;
        }

        // =====================================================================
        // Game flow
        // =====================================================================

        /// <summary>
        /// Start the game: enter round 1 PREPARATION phase, generate shops for all players.
        /// </summary>
        public void StartGame()
        {
            CurrentRound = 1;
            Phase = PvPGamePhase.PREPARATION;
            _phaseStartTime = DateTime.UtcNow;

            foreach (var pid in _alivePlayers)
            {
                GenerateShopForPlayer(pid);
            }

            _actionLog.Add($"[Round {CurrentRound}] Game started — PREPARATION phase");
        }

        /// <summary>
        /// Transition from PREPARATION to COMBAT phase.
        /// Generates matchmaking pairings and sets up combat boards.
        /// </summary>
        public void BeginCombatPhase()
        {
            if (Phase != PvPGamePhase.PREPARATION) return;
            Phase = PvPGamePhase.COMBAT;
            _phaseStartTime = DateTime.UtcNow;
            _combatEngines.Clear();
            _lastRoundResults.Clear();

            // Generate match pairings
            _currentPairings = _matchmaker.GeneratePairings(_alivePlayers.ToList());
            _actionLog.Add($"[Round {CurrentRound}] COMBAT phase — {_currentPairings.Count} matches");

            // Derive a per-round combat seed from master RNG
            int roundCombatSeed = _rng.Next();
            var combatSeedRng = new Random(roundCombatSeed);

            // Set up a CombatEngine for each pairing
            foreach (var pairing in _currentPairings)
            {
                SetupCombatForPairing(pairing, combatSeedRng.Next());
            }
        }

        /// <summary>
        /// Run all combats to completion (synchronous).
        /// Call this after BeginCombatPhase().
        /// </summary>
        public void RunAllCombats()
        {
            if (Phase != PvPGamePhase.COMBAT) return;

            foreach (var pairing in _currentPairings)
            {
                if (_combatEngines.TryGetValue(pairing.Player1Id, out var engine))
                {
                    int result = engine.SimulateCombat();
                    RecordCombatResult(pairing, engine, result);
                }
            }

            _actionLog.Add($"[Round {CurrentRound}] Combats resolved: {_lastRoundResults.Count} results");
        }

        /// <summary>
        /// Advance one combat frame for all active pairings simultaneously.
        /// Returns true if any combat is still ongoing.
        /// Use this for synchronized animation across all matches.
        /// </summary>
        public bool StepAllCombats()
        {
            if (Phase != PvPGamePhase.COMBAT) return false;

            bool anyOngoing = false;
            foreach (var pairing in _currentPairings)
            {
                if (!_combatEngines.TryGetValue(pairing.Player1Id, out var engine))
                    continue;

                var board = _boards[pairing.Player1Id];
                bool team1Alive = board.GetUnitsByTeam(Team.TEAM_1).Any(u => u.IsAlive());
                bool team2Alive = board.GetUnitsByTeam(Team.TEAM_2).Any(u => u.IsAlive());
                if (!team1Alive || !team2Alive || engine.FrameNumber >= engine.MaxFrames)
                    continue;

                engine.ExecuteDelayedFrame();

                // Re-check
                team1Alive = board.GetUnitsByTeam(Team.TEAM_1).Any(u => u.IsAlive());
                team2Alive = board.GetUnitsByTeam(Team.TEAM_2).Any(u => u.IsAlive());
                if (team1Alive && team2Alive && engine.FrameNumber < engine.MaxFrames)
                    anyOngoing = true;
            }
            return anyOngoing;
        }

        /// <summary>
        /// Advance a single combat frame for a specific pairing (for animated playback).
        /// Returns true if combat is still ongoing.
        /// </summary>
        public bool StepCombat(int playerId)
        {
            if (Phase != PvPGamePhase.COMBAT) return false;
            if (!_combatEngines.TryGetValue(playerId, out var engine)) return false;

            var board = _boards[playerId];
            bool team1Alive = board.GetUnitsByTeam(Team.TEAM_1).Any(u => u.IsAlive());
            bool team2Alive = board.GetUnitsByTeam(Team.TEAM_2).Any(u => u.IsAlive());
            if (!team1Alive || !team2Alive || engine.FrameNumber >= engine.MaxFrames)
                return false;

            engine.ExecuteDelayedFrame();

            team1Alive = board.GetUnitsByTeam(Team.TEAM_1).Any(u => u.IsAlive());
            team2Alive = board.GetUnitsByTeam(Team.TEAM_2).Any(u => u.IsAlive());
            return team1Alive && team2Alive && engine.FrameNumber < engine.MaxFrames;
        }

        /// <summary>
        /// Finalize all combats that haven't been recorded yet (e.g. after frame-stepping).
        /// Call this before EndRound() if using StepAllCombats/StepCombat.
        /// </summary>
        public void FinalizeAllCombats()
        {
            if (Phase != PvPGamePhase.COMBAT) return;

            foreach (var pairing in _currentPairings)
            {
                // Skip if already recorded
                if (_lastRoundResults.Any(r => r.Player1Id == pairing.Player1Id && r.Player2Id == pairing.Player2Id))
                    continue;

                if (_combatEngines.TryGetValue(pairing.Player1Id, out var engine))
                {
                    var board = _boards[pairing.Player1Id];
                    bool team1Alive = board.GetUnitsByTeam(Team.TEAM_1).Any(u => u.IsAlive());
                    bool team2Alive = board.GetUnitsByTeam(Team.TEAM_2).Any(u => u.IsAlive());

                    int result;
                    if (team1Alive && !team2Alive) result = 1;       // TEAM_1 won
                    else if (!team1Alive && team2Alive) result = 2;  // TEAM_2 won
                    else result = 0;                                  // draw or both dead

                    // If combat wasn't fully stepped, run remaining frames
                    if (team1Alive && team2Alive && engine.FrameNumber < engine.MaxFrames)
                    {
                        result = engine.SimulateCombat();
                    }

                    RecordCombatResult(pairing, engine, result);
                }
            }
        }

        /// <summary>
        /// End the round: apply combat damage, give income/XP, check eliminations,
        /// then either start next round or declare game over.
        /// </summary>
        public void EndRound()
        {
            if (Phase != PvPGamePhase.COMBAT) return;
            Phase = PvPGamePhase.ROUND_END;

            // If combats weren't finalized yet, do it now
            if (_lastRoundResults.Count == 0)
                RunAllCombats();
            else
                FinalizeAllCombats();

            // Apply damage to losers
            foreach (var result in _lastRoundResults)
            {
                int loserId = -1;
                if (result.Winner == 1) loserId = result.Player2Id;
                else if (result.Winner == 2) loserId = result.Player1Id;
                // draw: no damage

                if (loserId > 0 && _players.ContainsKey(loserId) && _alivePlayers.Contains(loserId))
                {
                    _players[loserId].TakeDamage(result.DamageToLoser);
                    _actionLog.Add($"[Round {CurrentRound}] Player {loserId} takes {result.DamageToLoser} damage (HP: {_players[loserId].Health})");

                    if (!_players[loserId].IsAlive())
                    {
                        _alivePlayers.Remove(loserId);
                        _eliminationOrder.Add(loserId);
                        ReturnPlayerUnitsToBag(loserId);
                        _actionLog.Add($"[Round {CurrentRound}] Player {loserId} eliminated!");
                    }
                }
            }

            // Income and XP for survivors
            foreach (var pid in _alivePlayers)
            {
                var player = _players[pid];
                int interest = Math.Min(Params.InterestCap, player.Gold / Math.Max(1, Params.InterestPerGold));
                int income = Params.BaseGoldIncome + interest;
                player.Gold += income;
                player.GainExperience(Params.XpPerRound);
                _actionLog.Add($"[Round {CurrentRound}] Player {pid}: +{income} gold (interest: {interest}), +{Params.XpPerRound} XP → Level {player.Level}");
            }

            // Reset units for surviving players
            foreach (var pid in _alivePlayers)
            {
                ResetPlayerBoardForNextRound(pid);
            }

            // Check win condition
            if (_alivePlayers.Count <= 1)
            {
                Phase = PvPGamePhase.GAME_OVER;
                _actionLog.Add($"[Round {CurrentRound}] GAME OVER");
                return;
            }

            // Start next round
            CurrentRound++;
            if (CurrentRound > Params.MaxRounds)
            {
                Phase = PvPGamePhase.GAME_OVER;
                _actionLog.Add($"Max rounds reached — GAME OVER");
                return;
            }

            Phase = PvPGamePhase.PREPARATION;
            _phaseStartTime = DateTime.UtcNow;

            // Generate new shops (unless locked)
            foreach (var pid in _alivePlayers)
            {
                if (_lockedShops.Contains(pid))
                {
                    _lockedShops.Remove(pid); // lock consumed, shop persists
                }
                else
                {
                    ReturnShopUnitsToBag(pid);
                    GenerateShopForPlayer(pid);
                }
            }

            _actionLog.Add($"[Round {CurrentRound}] PREPARATION phase");
        }

        /// <summary>Get the winner (only valid when Phase == GAME_OVER).</summary>
        public int? GetWinnerId()
        {
            if (Phase != PvPGamePhase.GAME_OVER) return null;
            return _alivePlayers.Count == 1 ? _alivePlayers.First() : null;
        }

        /// <summary>Get final standings: 1st place = winner, last = first eliminated.</summary>
        public List<int> GetFinalStandings()
        {
            var standings = new List<int>();
            standings.AddRange(_alivePlayers);
            for (int i = _eliminationOrder.Count - 1; i >= 0; i--)
                standings.Add(_eliminationOrder[i]);
            return standings;
        }

        // =====================================================================
        // Player Actions — all go through here (thread-safe)
        // =====================================================================

        /// <summary>
        /// Enqueue a player action for processing on the next tick.
        /// Actions are not executed immediately — they sit in a per-player queue
        /// and are processed one per player per tick by ProcessPendingActions().
        /// </summary>
        public void EnqueueAction(PlayerAction action)
        {
            if (_actionQueues.TryGetValue(action.PlayerId, out var queue))
                queue.Enqueue(action);
        }

        /// <summary>
        /// Get the result of the last action processed for a given player.
        /// Returns null if no action has been processed yet.
        /// </summary>
        public PlayerActionResult? GetLastActionResult(int playerId)
        {
            return _lastActionResults.TryGetValue(playerId, out var result) ? result : null;
        }

        /// <summary>
        /// Process one queued action per player. Call this at a fixed tick rate
        /// from the orchestrator. All alive players are processed each tick.
        /// </summary>
        public void ProcessPendingActions()
        {
            foreach (var playerId in _alivePlayers)
            {
                if (!_actionQueues.TryGetValue(playerId, out var queue)) continue;
                if (!queue.TryDequeue(out var action)) continue;

                _lastActionResults[playerId] = ExecuteAction(action);
            }
        }

        /// <summary>
        /// Clear all pending actions for every player. Call before phase transitions.
        /// </summary>
        public void ClearAllActionQueues()
        {
            foreach (var q in _actionQueues.Values)
            {
                while (q.TryDequeue(out _)) { }
            }
        }

        private PlayerActionResult ExecuteAction(PlayerAction action)
        {
            if (Phase == PvPGamePhase.GAME_OVER)
                return PlayerActionResult.Fail("Game is over");

            if (Phase == PvPGamePhase.ROUND_END)
                return PlayerActionResult.Fail("Round is ending — wait for next phase");

            if (!_players.ContainsKey(action.PlayerId))
                return PlayerActionResult.Fail("Invalid player ID");

            if (!_alivePlayers.Contains(action.PlayerId))
                return PlayerActionResult.Fail("Player is eliminated");

            // During COMBAT, only allow limited actions (bench/shop/leveling, no board moves)
            if (Phase == PvPGamePhase.COMBAT)
            {
                if (action.ActionType == PlayerActionType.MOVE_UNIT)
                    return PlayerActionResult.Fail("Cannot move units on the board during combat");
            }

            return action.ActionType switch
            {
                PlayerActionType.BUY_UNIT => HandleBuyUnit(action),
                PlayerActionType.SELL_UNIT => HandleSellUnit(action),
                PlayerActionType.REROLL_SHOP => HandleRerollShop(action),
                PlayerActionType.MOVE_UNIT => HandleMoveUnit(action),
                PlayerActionType.BUY_EXPERIENCE => HandleBuyExperience(action),
                PlayerActionType.LOCK_SHOP => HandleLockShop(action),
                _ => PlayerActionResult.Fail($"Unknown action type: {action.ActionType}")
            };
        }

        // =====================================================================
        // Action Handlers
        // =====================================================================

        private PlayerActionResult HandleBuyUnit(PlayerAction action)
        {
            if (Phase != PvPGamePhase.PREPARATION && Phase != PvPGamePhase.COMBAT)
                return PlayerActionResult.Fail("Cannot buy units during this phase");

            if (!action.ShopIndex.HasValue)
                return PlayerActionResult.Fail("ShopIndex required");

            var player = _players[action.PlayerId];
            int idx = action.ShopIndex.Value;

            if (idx < 0 || idx >= player.ShopUnits.Count)
                return PlayerActionResult.Fail("Invalid shop index");

            var unit = player.ShopUnits[idx];
            if (unit == null)
                return PlayerActionResult.Fail("Shop slot is empty");

            int cost = unit.GetCost();
            if (player.Gold < cost)
                return PlayerActionResult.Fail("Not enough gold");

            // Check bench capacity
            int benchUsed = player.Bench.Values.Count(v => v != null);
            if (benchUsed >= Params.BenchSize)
                return PlayerActionResult.Fail("Bench is full");

            player.Gold -= cost;
            unit.Team = player.team;

            var board = _boards[action.PlayerId];
            board.AddBenchUnit(player.team, unit);

            player.ShopUnits[idx] = null;

            _actionLog.Add($"[Round {CurrentRound}] {action}");
            return PlayerActionResult.Ok($"Bought {unit.UnitType} ({unit.Rarity})");
        }

        private PlayerActionResult HandleSellUnit(PlayerAction action)
        {
            if (Phase != PvPGamePhase.PREPARATION && Phase != PvPGamePhase.COMBAT)
                return PlayerActionResult.Fail("Cannot sell units during this phase");

            if (action.TargetUnit == null)
                return PlayerActionResult.Fail("TargetUnit required");

            var player = _players[action.PlayerId];
            var unit = action.TargetUnit;

            // During COMBAT, can only sell bench units (not board units in active combat)
            if (Phase == PvPGamePhase.COMBAT)
            {
                bool onBench = player.Bench.Values.Any(u => u == unit);
                if (!onBench)
                    return PlayerActionResult.Fail("During combat, can only sell units on the bench");
            }

            bool owned = player.Bench.Values.Any(u => u == unit) ||
                         player.UnitsOnBoard.Values.Any(u => u == unit);
            if (!owned)
                return PlayerActionResult.Fail("Unit does not belong to this player");

            int refund = unit.GetSellValue();

            if (unit.Position.HasValue && unit.Position.Value.Item1 >= 0)
            {
                var board = _boards[action.PlayerId];
                board.RemoveUnit(unit.Position.Value);
            }

            player.RemoveUnit(unit);
            player.Gold += refund;
            UnitBag.ReturnUnit(unit);

            _actionLog.Add($"[Round {CurrentRound}] {action} (+{refund} gold)");
            return PlayerActionResult.Ok($"Sold {unit.UnitType} for {refund} gold");
        }

        private PlayerActionResult HandleRerollShop(PlayerAction action)
        {
            if (Phase != PvPGamePhase.PREPARATION && Phase != PvPGamePhase.COMBAT)
                return PlayerActionResult.Fail("Cannot reroll during this phase");

            var player = _players[action.PlayerId];
            if (player.Gold < Params.RerollCost)
                return PlayerActionResult.Fail("Not enough gold to reroll");

            player.Gold -= Params.RerollCost;
            ReturnShopUnitsToBag(action.PlayerId);
            GenerateShopForPlayer(action.PlayerId);

            _actionLog.Add($"[Round {CurrentRound}] {action}");
            return PlayerActionResult.Ok("Shop rerolled");
        }

        private PlayerActionResult HandleMoveUnit(PlayerAction action)
        {
            // MOVE_UNIT is blocked during COMBAT by ProcessAction — only runs during PREPARATION
            if (Phase != PvPGamePhase.PREPARATION)
                return PlayerActionResult.Fail("Can only move units during PREPARATION phase");

            if (!action.FromPosition.HasValue || !action.ToPosition.HasValue)
                return PlayerActionResult.Fail("FromPosition and ToPosition required");

            var player = _players[action.PlayerId];
            var board = _boards[action.PlayerId];

            bool moved = board.PlayerMoveUnit(action.FromPosition.Value, action.ToPosition.Value, player.team);
            if (!moved)
                return PlayerActionResult.Fail("Move failed — invalid position or unit");

            _actionLog.Add($"[Round {CurrentRound}] {action}");
            return PlayerActionResult.Ok("Unit moved");
        }

        private PlayerActionResult HandleBuyExperience(PlayerAction action)
        {
            if (Phase != PvPGamePhase.PREPARATION && Phase != PvPGamePhase.COMBAT)
                return PlayerActionResult.Fail("Cannot buy XP during this phase");

            var player = _players[action.PlayerId];
            if (player.Gold < Params.BuyXpCost)
                return PlayerActionResult.Fail("Not enough gold");

            player.Gold -= Params.BuyXpCost;
            player.GainExperience(Params.BuyXpAmount);

            _actionLog.Add($"[Round {CurrentRound}] {action}");
            return PlayerActionResult.Ok($"Bought {Params.BuyXpAmount} XP (Level {player.Level})");
        }

        private PlayerActionResult HandleLockShop(PlayerAction action)
        {
            if (Phase != PvPGamePhase.PREPARATION && Phase != PvPGamePhase.COMBAT)
                return PlayerActionResult.Fail("Cannot lock shop during this phase");

            if (_lockedShops.Contains(action.PlayerId))
            {
                _lockedShops.Remove(action.PlayerId);
                _actionLog.Add($"[Round {CurrentRound}] Player {action.PlayerId}: UNLOCK SHOP");
                return PlayerActionResult.Ok("Shop unlocked");
            }
            else
            {
                _lockedShops.Add(action.PlayerId);
                _actionLog.Add($"[Round {CurrentRound}] {action}");
                return PlayerActionResult.Ok("Shop locked — will persist next round");
            }
        }

        // =====================================================================
        // Internal helpers
        // =====================================================================

        private Board CreateBoard()
        {
            Board board;
            var size = Params.BoardSize;
            board = Params.BoardType switch
            {
                "hex" => new HexBoard(size),
                "square" => new SquareBoard(size),
                "diagonal_square" => new DiagonalSquareBoard(size),
                _ => new HexBoard(size)
            };
            board.BenchSize = Params.BenchSize;
            return board;
        }

        private void GenerateShopForPlayer(int playerId)
        {
            var player = _players[playerId];
            var shopUnits = UnitBag.GenerateShop(player.Level, player.team);
            player.ShopUnits.Clear();
            foreach (var u in shopUnits)
                player.ShopUnits.Add(u);
        }

        private void ReturnShopUnitsToBag(int playerId)
        {
            var player = _players[playerId];
            foreach (var u in player.ShopUnits)
            {
                if (u != null) UnitBag.ReturnUnit(u);
            }
            player.ShopUnits.Clear();
            for (int i = 0; i < Params.ShopSize; i++) player.ShopUnits.Add(null);
        }

        private void ReturnPlayerUnitsToBag(int playerId)
        {
            var player = _players[playerId];
            foreach (var u in player.Bench.Values)
            {
                if (u != null) UnitBag.ReturnUnit(u);
            }
            foreach (var u in player.UnitsOnBoard.Values)
            {
                if (u != null) UnitBag.ReturnUnit(u);
            }
            ReturnShopUnitsToBag(playerId);
        }

        private void SetupCombatForPairing(MatchPairing pairing, int combatSeed)
        {
            var hostPlayer = _players[pairing.Player1Id];
            var opponentPlayer = _players[pairing.Player2Id];
            var board = _boards[pairing.Player1Id];

            board.ResetBoard();

            // Preserve original team identities so colors stay consistent.
            // The player who was TEAM_1 (blue) stays TEAM_1 in combat, etc.
            var team1Source = hostPlayer.team == Team.TEAM_1 ? hostPlayer : opponentPlayer;
            var team2Source = hostPlayer.team == Team.TEAM_1 ? opponentPlayer : hostPlayer;

            var combatTeam1 = new Player(team1Source.PlayerId, Team.TEAM_1, Params);
            var combatTeam2 = new Player(team2Source.PlayerId, Team.TEAM_2, Params);

            // Copy bench from real players so it renders during combat
            foreach (var kv in team1Source.Bench)
                combatTeam1.Bench[kv.Key] = kv.Value;
            foreach (var kv in team2Source.Bench)
                combatTeam2.Bench[kv.Key] = kv.Value;

            // Assign combat players to the board BEFORE placing any units,
            // so that PlaceBoardUnit side effects target these throwaway
            // combat players, not the real persistent ones.
            board.Players[Team.TEAM_1] = combatTeam1;
            board.Players[Team.TEAM_2] = combatTeam2;

            // Clone TEAM_1 source's units — already in TEAM_1 zone (top half)
            foreach (var kv in team1Source.UnitsOnBoard)
            {
                if (kv.Value == null) continue;
                var clone = kv.Value.Clone();
                clone.Team = Team.TEAM_1;
                clone.InitialPosition = clone.Position;
                clone.RoundReset();
                if (clone.Position.HasValue)
                    board.PlaceBoardUnit(clone, clone.Position.Value);
            }

            // Clone TEAM_2 source's units — already in TEAM_2 zone (bottom half)
            foreach (var kv in team2Source.UnitsOnBoard)
            {
                if (kv.Value == null) continue;
                var clone = kv.Value.Clone();
                clone.Team = Team.TEAM_2;
                clone.InitialPosition = clone.Position;
                clone.RoundReset();
                if (clone.Position.HasValue)
                    board.PlaceBoardUnit(clone, clone.Position.Value);
            }

            var engine = new CombatEngine(board, combatTeam1, combatTeam2, combatSeed: combatSeed);
            _combatEngines[pairing.Player1Id] = engine;
        }

        private void RecordCombatResult(MatchPairing pairing, CombatEngine engine, int result)
        {
            // result: 1 = TEAM_1 won, 2 = TEAM_2 won, 0 = draw
            // Map to CombatResult.Winner: 1 = Player1Id (host) won, 2 = Player2Id (opponent) won
            var hostPlayer = _players[pairing.Player1Id];
            int mappedWinner = 0;
            if (result == 1)
                mappedWinner = hostPlayer.team == Team.TEAM_1 ? 1 : 2;
            else if (result == 2)
                mappedWinner = hostPlayer.team == Team.TEAM_2 ? 1 : 2;

            int survivingWinnerUnits = 0;
            var board = _boards[pairing.Player1Id];
            if (result == 1)
                survivingWinnerUnits = board.GetUnitsByTeam(Team.TEAM_1).Count(u => u.IsAlive());
            else if (result == 2)
                survivingWinnerUnits = board.GetUnitsByTeam(Team.TEAM_2).Count(u => u.IsAlive());

            int damage = Params.BaseCombatDamage + survivingWinnerUnits;

            _lastRoundResults.Add(new CombatResult
            {
                Player1Id = pairing.Player1Id,
                Player2Id = pairing.Player2Id,
                Winner = mappedWinner,
                DamageToLoser = damage,
                SurvivingUnits = survivingWinnerUnits,
                IsGhostMatch = pairing.IsGhostMatch
            });
        }

        private void ResetPlayerBoardForNextRound(int playerId)
        {
            var player = _players[playerId];
            var board = _boards[playerId];
            board.ResetBoard();

            // Restore board player mapping (combat may have overwritten it)
            Team oppositeTeam = player.team == Team.TEAM_1 ? Team.TEAM_2 : Team.TEAM_1;
            board.Players[player.team] = player;
            board.Players[oppositeTeam] = new Player(0, oppositeTeam);

            // Snapshot units using their actual Position (cell-tracking keeps it
            // current even after board-to-board moves whose dict keys may be stale).
            var units = player.UnitsOnBoard.Values
                .Where(u => u != null && u.Position.HasValue).ToList();
            player.UnitsOnBoard.Clear();

            foreach (var u in units)
            {
                u.InitialPosition = u.Position;
                u.RoundReset();
                u.Team = player.team;
                board.PlaceBoardUnit(u, u.Position!.Value);
            }
        }

        /// <summary>Set a new master seed and re-derive sub-system seeds.</summary>
        public void SetMasterSeed(int seed)
        {
            MasterSeed = seed;
            _rng = new Random(seed);
            UnitBag.SetSeed(_rng.Next());
            _matchmaker = new PvPMatchmaker(new Random(_rng.Next()));
        }
    }
}
