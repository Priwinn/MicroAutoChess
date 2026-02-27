using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MicroAutoChess.Core;
using System.IO;
using SD = System.Drawing;
using System;
using System.Linq;
using System.Timers;
using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia;

namespace MicroAutoChess.AvaloniaApp
{
    public partial class MainWindow : Window
    {
        private Avalonia.Controls.Image _image;
        private Board _board;
        private Player p1;
        private Player p2;
        private SkiaBoardVisualizer _skiaVisualizer;
        private CombatEngine _engine;
        private PvERoundManager _pveManager;
        private System.Timers.Timer? _engineTimer;
        private System.Timers.Timer _renderTimer;
        private int _lastMouseX;
        private int _lastMouseY;
        private System.Collections.Generic.List<(MicroAutoChess.Core.UnitType ut, int? cost)> _spawnSpecs;
        private double _engineFps;
        private double _engineIntervalSeconds;
        private DateTime _lastRenderTick;
        private double _simProgressAccum = 0.0;
        private bool _paused = true;

        public MainWindow()
        {
            InitializeComponent();
            _image = this.FindControl<Image>("RenderImage");

            // Force fullscreen so we can compute scaled cell radius from actual window size
            this.WindowState = WindowState.FullScreen;
            // Defer heavy initialization until window is opened so Bounds/ClientSize are valid
            this.Opened += OnOpened;
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            // Create PvE manager and setup the first round (ports Python visualize_combat usage)
            _pveManager = new PvERoundManager(Levels.LEVELS.Cast<object>().ToList());
            var setup = _pveManager.SetupRound();
            _board = setup.Item1;
            // team1 units (enemy placements)
            var team1Units = setup.Item2;
            var team2Units = setup.Item3;
            p1 = new Player(1, Team.TEAM_1, new GameParams() { UnitLevelUp = false });
            p2 = new Player(2, Team.TEAM_2);
            p1.MaxUnitsOnBoard = _board.Size.Item1 * _board.Size.Item2;
            p2.MaxUnitsOnBoard = _board.Size.Item1 * _board.Size.Item2;
            //disable unit level up for Enemy team
            p1.GameParams.UnitLevelUp = false;
            _board.AddPlayerByRef(ref p1, Team.TEAM_1);
            _board.AddPlayerByRef(ref p2, Team.TEAM_2);
            foreach (var u in team1Units)
            {
                _board.PlaceUnit(u, u.Position.Value);
            }
            foreach (var u in team2Units)
            {
                _board.PlaceUnit(u, u.Position.Value);
            }

            // Use actual client size to compute visualizer scale
            var bounds = this.ClientSize;
            int w = Math.Max(1, (int)bounds.Width);
            int h = Math.Max(1, (int)bounds.Height);

            // pick a base cell radius that will be scaled inside the visualizer constructor
            int baseCell = Math.Max(20, (int)(Math.Min(w, h) * 0.03));

            // Use Skia-based visualizer only (legacy BoardVisualizer removed)
            _skiaVisualizer = new SkiaBoardVisualizer(_board, renderFps: 30, cellRadius: baseCell, margin: 16, windowWidth: w, windowHeight: h);

            _engine = new CombatEngine(_board, p1, p2, combatSeed: 123);

            _engineFps = 10.0;
            _engineIntervalSeconds = 1.0 / Math.Max(1.0, _engineFps);

            // Start render timer; engine steps will be advanced synchronously inside RenderFrameAsync
            _lastRenderTick = DateTime.UtcNow;
            _simProgressAccum = 0.0;

            _engineTimer = null;
            // sample spawn specs: compute real costs from Unit.GetCost()
            _spawnSpecs = new System.Collections.Generic.List<(MicroAutoChess.Core.UnitType, int?)>();
            var spawnTypes = new MicroAutoChess.Core.UnitType[] { MicroAutoChess.Core.UnitType.WARRIOR, MicroAutoChess.Core.UnitType.ARCHER, MicroAutoChess.Core.UnitType.MAGE, MicroAutoChess.Core.UnitType.TANK, MicroAutoChess.Core.UnitType.ASSASSIN };
            foreach (var ut in spawnTypes)
            {
                var tmp = new Unit(ut, UnitRarity.COMMON, Team.TEAM_2);
                _spawnSpecs.Add((ut, tmp.GetCost()));
            }

            // track mouse on the render image
            _image.PointerMoved += (s, ev) => {
                var pt = ev.GetPosition(_image);
                _lastMouseX = Math.Max(0, (int)pt.X);
                _lastMouseY = Math.Max(0, (int)pt.Y);
                if (_skiaVisualizer.IsDragging) _skiaVisualizer.UpdateDragHover(_lastMouseX, _lastMouseY); 
            };
            // handle clicks for pause/start and spawn buttons
            _image.PointerPressed += (s, ev) => {
                var pt = ev.GetPosition(_image);
                int mx = Math.Max(0, (int)pt.X);
                int my = Math.Max(0, (int)pt.Y);
                var pauseRect = _skiaVisualizer.GetPauseButtonRect();
                var speedUpRect = _skiaVisualizer.GetSpeedUpRect();
                var speedDownRect = _skiaVisualizer.GetSpeedDownRect();
                // spawn buttons: check before pause toggle so clicking a spawn doesn't toggle pause
                if (_pveManager != null && _spawnSpecs != null && _skiaVisualizer != null && _engine != null)
                {
                    int total = _spawnSpecs.Count;
                    for (int i = 0; i < total; i++)
                    {
                        var r = _skiaVisualizer.GetSpawnButtonRect(i, total: total);
                        if (mx >= r.Left && mx <= r.Right && my >= r.Top && my <= r.Bottom)
                        {
                            // spawn button clicked — route through PlayerAction
                            if (!_paused && _engine.FrameNumber > 0) { ev.Pointer.Capture(null); return; }

                            var buyAction = new PlayerAction(2, PlayerActionType.BUY_UNIT) { ShopIndex = i };
                            ProcessPveAction(buyAction);

                            ev.Pointer.Capture(null);
                            return;
                        }
                    }
                }

                // speed up clicked
                if (mx >= speedUpRect.Left && mx <= speedUpRect.Right && my >= speedUpRect.Top && my <= speedUpRect.Bottom)
                {
                    _engineFps = Math.Min(80, _engineFps * 2.0);
                    _engineIntervalSeconds = 1.0 / _engineFps;
                    ev.Pointer.Capture(null);
                    return;
                }
                // speed down clicked
                if (mx >= speedDownRect.Left && mx <= speedDownRect.Right && my >= speedDownRect.Top && my <= speedDownRect.Bottom)
                {
                    _engineFps = Math.Max(1, _engineFps * 0.5);
                    _engineIntervalSeconds = 1.0 / _engineFps;
                    ev.Pointer.Capture(null);
                    return;
                }
                if (mx >= pauseRect.Left && mx <= pauseRect.Right && my >= pauseRect.Top && my <= pauseRect.Bottom)
                {
                    // toggle paused state
                    bool wasPaused = _paused;
                    _paused = !_paused;
                    if (wasPaused && !_paused)
                    {
                        // transitioning paused -> running: reset damage meter and unit info
                        _skiaVisualizer.DamageDone.Clear();
                        _skiaVisualizer.UnitInfo.Clear();
                        // when starting the match (frame 0) capture player2 starting placements
                        if (_engine != null && _engine.FrameNumber == 0 && p2 != null && _board != null)
                        {
                            p2.UnitsOnBoard.Clear();
                            var units2 = _board.GetUnitsByTeam(Team.TEAM_2);
                            foreach (var u in units2)
                            {
                                if (u.Position != null)
                                {
                                    u.InitialPosition = u.Position;
                                    p2.UnitsOnBoard[u.Position.Value] = u;
                                }
                            }
                            p2.Bench.Clear();
                            for (int bi = 0; bi < _board.BenchSize; bi++)
                            {
                                var bu = _board.GetBenchUnit(Team.TEAM_2, bi);
                                if (bu != null)
                                {
                                    bu.InitialPosition = (-2, bi);
                                    p2.Bench[bi] = bu;
                                }
                            }
                        }

                    }
                    // consume event
                    ev.Pointer.Capture(null);
                }
                // If combat hasn't started (frame 0) and paused, allow dragging units
                if (_paused && _engine != null && _engine.FrameNumber == 0 && _skiaVisualizer != null)
                {
                    bool began = _skiaVisualizer.BeginDragAt(mx, my);
                    if (began) { ev.Pointer.Capture(null); return; }
                }
            };

            _image.PointerReleased += (s, ev) => {
                var pt = ev.GetPosition(_image);
                int mx = Math.Max(0, (int)pt.X);
                int my = Math.Max(0, (int)pt.Y);
                if (_skiaVisualizer.IsDragging)
                {
                    int total = _spawnSpecs.Count;
                    try
                    {
                        var dragged = _skiaVisualizer.DraggedUnit;
                        var dragFrom = _skiaVisualizer.DragFrom;
                        if (dragged != null && dragFrom.HasValue && _skiaVisualizer.IsMouseOverShop(mx, my, total))
                        {
                            // Sell via PlayerAction: process SELL_UNIT (unit is still in place)
                            _skiaVisualizer.ClearDragState();

                            var sellAction = new PlayerAction(2, PlayerActionType.SELL_UNIT) { TargetUnit = dragged };
                            ProcessPveAction(sellAction);
                        }
                        else
                        {
                            // Move via PlayerAction
                            EndDragAt(mx, my);
                        }
                    }
                    catch
                    {
                        EndDragAt(mx, my);
                    }

                    ev.Pointer.Capture(null);
                }

            };

            _renderTimer = new System.Timers.Timer(16); // ~60 FPS render
            // Post render work to the UI thread so rendering executes synchronously on main thread
            _renderTimer.Elapsed += (s, ev) => Dispatcher.UIThread.Post(() => RenderFrame());
            _renderTimer.Start();
        }

        private bool EndDragAt(int mouseX, int mouseY)
        {
            var dragged = _skiaVisualizer.DraggedUnit;
            var dragFrom = _skiaVisualizer.DragFrom;
            if (dragged == null || !dragFrom.HasValue) return false;

            (int, int)? targetPos = _skiaVisualizer.HitTestBoardCell(mouseX, mouseY);
            if (!targetPos.HasValue)
                targetPos = _skiaVisualizer.HitTestBenchCell(mouseX, mouseY);

            bool moved = false;
            if (targetPos.HasValue)
            {
                var moveAction = new PlayerAction(2, PlayerActionType.MOVE_UNIT)
                {
                    FromPosition = dragFrom.Value,
                    ToPosition = targetPos.Value
                };
                var result = ProcessPveAction(moveAction);
                moved = result.Success;
            }

            _skiaVisualizer.ClearDragState();
            return moved;
        }

        /// <summary>
        /// Process a PlayerAction in the PvE context.
        /// Routes BUY_UNIT, SELL_UNIT, MOVE_UNIT through the existing board/player/budget state.
        /// </summary>
        private PlayerActionResult ProcessPveAction(PlayerAction action)
        {
            switch (action.ActionType)
            {
                case PlayerActionType.BUY_UNIT:
                {
                    if (!action.ShopIndex.HasValue)
                        return PlayerActionResult.Fail("ShopIndex required");
                    int idx = action.ShopIndex.Value;
                    if (idx < 0 || idx >= _spawnSpecs.Count)
                        return PlayerActionResult.Fail("Invalid shop index");

                    var spec = _spawnSpecs[idx];
                    var ut = spec.ut;
                    var tmp = new Unit(ut, UnitRarity.COMMON, Team.TEAM_2);
                    int cost = tmp.GetCost();
                    if (_pveManager.PlayerBudget < cost)
                        return PlayerActionResult.Fail("Not enough budget");

                    // Find bench slot first
                    int benchIdx = -1;
                    for (int bi = 0; bi < _board.BenchSize; bi++)
                    {
                        if (_board.GetBenchUnit(Team.TEAM_2, bi) == null) { benchIdx = bi; break; }
                    }

                    if (benchIdx != -1)
                    {
                        var newU = new Unit(ut, UnitRarity.COMMON, Team.TEAM_2) { Position = (-2, benchIdx) };
                        newU.CurrentHealth = newU.GetMaxHealth();
                        _board.PlaceUnit(newU, (-2, benchIdx));
                    }
                    else
                    {
                        // Try to find an empty initial position for team 2
                        var valid = _board.GetInitialPositions(Team.TEAM_2);
                        (int, int)? targetPos = null;
                        foreach (var pos in valid)
                        {
                            if (_board.IsValidPosition(pos) && _board.IsEmpty(pos)) { targetPos = pos; break; }
                        }
                        if (!targetPos.HasValue)
                            return PlayerActionResult.Fail("No space available");

                        var newU = new Unit(ut, UnitRarity.COMMON, Team.TEAM_2) { Position = targetPos };
                        newU.CurrentHealth = newU.GetMaxHealth();
                        _board.PlaceUnit(newU, targetPos.Value);
                    }

                    _pveManager.SpendBudget(cost);
                    return PlayerActionResult.Ok($"Bought {ut}");
                }

                case PlayerActionType.SELL_UNIT:
                {
                    if (action.TargetUnit == null)
                        return PlayerActionResult.Fail("TargetUnit required");

                    var unit = action.TargetUnit;
                    int refund = unit.GetCost();

                    // Remove from board or bench
                    if (unit.Position.HasValue)
                    {
                        if (unit.Position.Value.Item1 < 0)
                        {
                            _board.RemoveBenchUnit(unit.Team, unit.Position.Value.Item2);
                        }
                        else
                        {
                            _board.RemoveUnit(unit.Position.Value);
                        }
                    }

                    _pveManager.AddBudget(refund);
                    return PlayerActionResult.Ok($"Sold {unit.UnitType} for {refund}");
                }

                case PlayerActionType.MOVE_UNIT:
                {
                    if (!action.FromPosition.HasValue || !action.ToPosition.HasValue)
                        return PlayerActionResult.Fail("FromPosition and ToPosition required");

                    bool moved = _board.PlayerMoveUnit(action.FromPosition.Value, action.ToPosition.Value, Team.TEAM_2);
                    return moved ? PlayerActionResult.Ok("Unit moved") : PlayerActionResult.Fail("Move failed");
                }

                default:
                    return PlayerActionResult.Fail($"Unsupported PvE action: {action.ActionType}");
            }
        }

        private void RenderFrame()
        {
            var bounds = this.ClientSize;
            int w = Math.Max(1, (int)bounds.Width);
            int h = Math.Max(1, (int)bounds.Height);

            // Advance engine synchronously based on elapsed render time (matches Python approach)
            var now = DateTime.UtcNow;
            double dt = (now - _lastRenderTick).TotalSeconds;
            _lastRenderTick = now;

            // accumulate fractional simulation progress in units of simulation frames
            if (!_paused)
            {
                _simProgressAccum += dt * _engineFps;
                // advance whole simulation frames
                while (_simProgressAccum >= 1.0)
                {
                    _engine.ExecuteDelayedFrame();
                    _simProgressAccum -= 1.0;
                }
            }

            // fraction for interpolation inside current sim frame
            double simProgress = Math.Max(0.0, Math.Min(1.0, _simProgressAccum));

            // Create SKSurface with BGRA format to match Avalonia WriteableBitmap pixel format
            var info = new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
            using var surface = SkiaSharp.SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(new SkiaSharp.SKColor(30, 30, 30));
            // draw via Skia visualizer using the synchronized simFrame/simProgress
            // pass paused state, mouse coords and spawn specs for hover/tooltips
            _skiaVisualizer.Draw(canvas, w, h, _engine, _engine.FrameNumber, (float)simProgress, _paused, _lastMouseX, _lastMouseY, _spawnSpecs, _pveManager != null ? (int?)_pveManager.PlayerBudget : null, _engineFps);

            // Read pixels into managed buffer using ReadPixels
            var skInfo = new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
            int length = skInfo.RowBytes * skInfo.Height;
            IntPtr temp = Marshal.AllocHGlobal(length);
            try
            {
                surface.ReadPixels(skInfo, temp, skInfo.RowBytes, 0, 0);
                var buffer = new byte[length];
                Marshal.Copy(temp, buffer, 0, length);

                var wb = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
                using (var fb = wb.Lock())
                {
                    Marshal.Copy(buffer, 0, fb.Address, length);
                }

                // We're already running on the UI thread here; assign directly
                _image.Source = wb;
                // Check win/lose condition after rendering a non-zero frame
                bool team1Alive = p1 != null && p1.UnitsOnBoard.Values.Any(u => u != null && u.IsAlive());
                bool team2Alive = p2 != null && p2.UnitsOnBoard.Values.Any(u => u != null && u.IsAlive());
                if ((_engine != null) && _engine.FrameNumber > 0 && (!team1Alive || !team2Alive))
                {
                    // player won (enemy dead)
                    if (!team1Alive)
                    {
                        bool advanced = _pveManager.AdvanceRound(); 
                        if (advanced)
                        {
                            // apply next round to board, keep player's units
                            var res = _pveManager.ApplyRoundToBoard(_board, p1, p2);
                            _board = res.Item1;
                            p1 = res.Item2;
                            p2 = res.Item3;
                            _engine = new CombatEngine(_board, p1, p2, combatSeed: 123);
                            _paused = true;
                            _simProgressAccum = 0.0;
                        }
                        else
                        {
                            // no more rounds: reset to first round and recreate visualizer/engine
                            GlobalLog.CombatLog.Clear();
                            _pveManager = new PvERoundManager(Levels.LEVELS.Cast<object>().ToList());
                            var setup = _pveManager.SetupRound();
                            _board = setup.Item1;
                            var team1Units = setup.Item2;
                            var team2Units = setup.Item3;
                            p1 = new Player(1, Team.TEAM_1, p1.GameParams); 
                            p2 = new Player(2, Team.TEAM_2);
                            foreach (var u in team1Units) if (u.Position != null) p1.UnitsOnBoard[u.Position.Value] = u;
                            foreach (var u in team2Units) if (u.Position != null) p2.UnitsOnBoard[u.Position.Value] = u;
                            var csz = this.ClientSize; int ww = Math.Max(1, (int)csz.Width); int hh = Math.Max(1, (int)csz.Height);
                            int baseCell = Math.Max(20, (int)(Math.Min(ww, hh) * 0.03));
                            _skiaVisualizer = new SkiaBoardVisualizer(_board, renderFps: 30, cellRadius: baseCell, margin: 16, windowWidth: ww, windowHeight: hh);
                            _engine = new CombatEngine(_board, p1, p2, combatSeed: 123);
                            _paused = true;
                            _simProgressAccum = 0.0;
                        }
                    }
                    else
                    {
                        // player lost: reset to current round initial configuration and restore player's units
                        var res = _pveManager.ApplyRoundToBoard(_board, p1, p2);
                        _board = res.Item1;
                        p1 = res.Item2;
                        p2 = res.Item3;
                        _engine = new CombatEngine(_board, p1, p2, combatSeed: 123);
                        _paused = true;
                        _simProgressAccum = 0.0;
                    }
                }

            }
            finally
            {
                Marshal.FreeHGlobal(temp);
            }

        }

        private static (Board, Player, Player) CreateMockScenario()
        {
            var board = new HexBoard((7, 8));
            var player1 = new Player(1, Team.TEAM_1, new GameParams() { UnitLevelUp = false });
            var player2 = new Player(2, Team.TEAM_2);

            for (int x = 0; x < 7; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    var pos = (x, y);
                    var u = new Unit(UnitType.TANK, UnitRarity.COMMON, Team.TEAM_1) { Position = pos };
                    u.CurrentHealth = u.GetMaxHealth();
                    board.PlaceBoardUnit(u, pos);
                    player1.UnitsOnBoard[pos] = u;
                }
            }

            var team2Placements = new System.Collections.Generic.Dictionary<(int, int), UnitType>
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
            };

            foreach (var kv in team2Placements)
            {
                var pos = kv.Key;
                var ut = kv.Value;
                var u = new Unit(ut, UnitRarity.COMMON, Team.TEAM_2) { Position = pos };
                u.CurrentHealth = u.GetMaxHealth();
                board.PlaceBoardUnit(u, pos);
                player2.UnitsOnBoard[pos] = u;
            }

            return (board, player1, player2);
        }
    }
}
