using Avalonia.Controls.ApplicationLifetimes;
using MicroAutoChess.Core;
using MicroAutoChess.Core.Agents;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MicroAutoChess.PvPApp
{
    /// <summary>
    /// Coordinates a PvP game session: creates the PvPGameManager, instantiates
    /// IGamePlayer implementations (human/AI), opens a PlayerWindow per human player,
    /// and drives phase transitions.
    ///
    /// Combat is stepped centrally by the orchestrator so multiple human windows
    /// don't double-advance the same combat engine.
    /// </summary>
    public class PvPOrchestrator
    {
        private readonly PvPGameManager _manager;
        private readonly List<IGamePlayer> _players = new();
        private readonly List<PlayerWindow> _humanWindows = new();

        // Tracks which players have finished their combat animation
        private readonly HashSet<int> _combatFinished = new();

        public PvPGameManager Manager => _manager;
        public IReadOnlyList<IGamePlayer> Players => _players;

        // Combat stepping state — render-driven (matches PvE approach)
        private DateTime _lastCombatAdvance;
        private double _combatSimAccum;
        private double _engineFps = 10.0;

        // Preparation-phase action tick timer
        private System.Timers.Timer? _prepTimer;
        private double _prepTickRate = 50.0; // actions per second during preparation

        public PvPOrchestrator(
            List<(int playerId, GamePlayerType type)> playerConfigs,
            GameParams? gameParams = null,
            int? masterSeed = null)
        {
            _manager = new PvPGameManager(playerConfigs.Count, gameParams, masterSeed: masterSeed);

            foreach (var config in playerConfigs)
            {
                IGamePlayer player = config.type switch
                {
                    GamePlayerType.AI => new RandomAIPlayer(config.playerId),
                    GamePlayerType.Human => new HumanPlayer(config.playerId),
                    _ => throw new ArgumentException($"Unsupported player type: {config.type}")
                };
                _players.Add(player);
            }
        }

        /// <summary>
        /// Initialize the game, open windows for human players, and start round 1.
        /// </summary>
        public void Start(IClassicDesktopStyleApplicationLifetime desktop)
        {
            _manager.StartGame();

            // Notify all players of initial preparation phase
            foreach (var p in _players)
                p.OnPreparationStart();

            // Initialize AI agents for preparation
            foreach (var ai in _players.OfType<AIPlayer>())
                ai.BeginPreparation(_manager.GetGameStateForPlayer(ai.PlayerId));

            // Start the preparation tick timer
            StartPrepTimer();

            // Create windows for human players
            bool first = true;
            foreach (var p in _players.OfType<HumanPlayer>())
            {
                var window = new PlayerWindow(this, p.PlayerId);
                _humanWindows.Add(window);
                p.Window = window;

                if (first)
                {
                    desktop.MainWindow = window;
                    first = false;
                }
                else
                {
                    window.Show();
                }
            }
        }

        /// <summary>
        /// Called by a human player's window when they click "Start Combat".
        /// Transitions the game to COMBAT phase.
        /// </summary>
        public void RequestBeginCombat()
        {
            if (_manager.Phase != PvPGamePhase.PREPARATION) return;

            // Stop preparation tick timer and clear any unprocessed actions
            StopPrepTimer();
            _manager.ClearAllActionQueues();

            _combatFinished.Clear();
            _manager.BeginCombatPhase();

            foreach (var p in _players)
                p.OnCombatStart();

            // Initialize render-driven combat clock
            _lastCombatAdvance = DateTime.UtcNow;
            _combatSimAccum = 0.0;
        }

        /// <summary>
        /// Advance all combats by one frame. Called by the central combat timer.
        /// Returns true if any combat is still ongoing.
        /// </summary>
        public bool StepCombats()
        {
            if (_manager.Phase != PvPGamePhase.COMBAT) return false;
            return _manager.StepAllCombats();
        }

        /// <summary>
        /// Finalize combats and end the round. Called when StepCombats returns false.
        /// </summary>
        public void FinishCombatRound()
        {
            if (_manager.Phase != PvPGamePhase.COMBAT) return;

            _manager.FinalizeAllCombats();
            _manager.EndRound();

            if (_manager.Phase == PvPGamePhase.GAME_OVER)
            {
                var winnerId = _manager.GetWinnerId();
                foreach (var p in _players)
                    p.OnGameOver(winnerId);
            }
            else
            {
                foreach (var p in _players)
                    p.OnRoundEnd();

                // Preparation phase: notify players and start tick timer
                foreach (var p in _players)
                    p.OnPreparationStart();

                // Initialize AI agents for preparation
                foreach (var ai in _players.OfType<AIPlayer>())
                    ai.BeginPreparation(_manager.GetGameStateForPlayer(ai.PlayerId));

                StartPrepTimer();
            }
        }

        public double EngineFps
        {
            get => _engineFps;
            set => _engineFps = Math.Clamp(value, 1.0, 80.0);
        }

        /// <summary>
        /// Advance combat engines based on elapsed wall-clock time.
        /// Called from the render loop. Returns fractional progress (0..1)
        /// for smooth interpolation between engine frames.
        /// </summary>
        public float AdvanceCombat()
        {
            if (_manager.Phase != PvPGamePhase.COMBAT)
                return 0f;

            var now = DateTime.UtcNow;
            double dt = (now - _lastCombatAdvance).TotalSeconds;
            _lastCombatAdvance = now;

            // Clamp dt to avoid huge jumps (e.g. after breakpoint or lag spike)
            dt = Math.Min(dt, 0.25);

            _combatSimAccum += dt * _engineFps;
            while (_combatSimAccum >= 1.0)
            {
                bool ongoing = StepCombats();
                _combatSimAccum -= 1.0;
                if (!ongoing)
                {
                    _combatSimAccum = 0.0;
                    FinishCombatRound();
                    return 0f;
                }
            }

            return (float)Math.Clamp(_combatSimAccum, 0.0, 1.0);
        }

        // =================================================================
        // Preparation-phase tick timer
        // =================================================================

        private void StartPrepTimer()
        {
            StopPrepTimer();
            _prepTimer = new System.Timers.Timer(1000.0 / _prepTickRate);
            _prepTimer.AutoReset = false;
            _prepTimer.Elapsed += OnPrepTick;
            _prepTimer.Start();
        }

        private void StopPrepTimer()
        {
            _prepTimer?.Stop();
            _prepTimer?.Dispose();
            _prepTimer = null;
        }

        private void OnPrepTick(object? s, System.Timers.ElapsedEventArgs e)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_manager.Phase != PvPGamePhase.PREPARATION)
                {
                    StopPrepTimer();
                    return;
                }

                // Query each AI player for their next action and enqueue it
                foreach (var ai in _players.OfType<AIPlayer>())
                {
                    var state = _manager.GetGameStateForPlayer(ai.PlayerId);
                    var action = ai.GetNextAction(state);
                    if (action != null)
                        _manager.EnqueueAction(action);
                }

                _manager.ProcessPendingActions();

                // Restart timer for next tick (non-overlapping)
                _prepTimer?.Start();
            });
        }
    }
}
