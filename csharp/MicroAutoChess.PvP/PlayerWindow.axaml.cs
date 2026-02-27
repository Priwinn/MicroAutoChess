using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using MicroAutoChess.AvaloniaApp;
using MicroAutoChess.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace MicroAutoChess.PvPApp
{
    public partial class PlayerWindow : Window
    {
        private readonly PvPOrchestrator _orchestrator;
        private readonly int _playerId;

        private Avalonia.Controls.Image _image;
        private PvPGameManager _manager;
        private Board _myBoard;
        private Player _myPlayer;
        private SkiaBoardVisualizer _skiaVisualizer;
        private System.Timers.Timer _renderTimer;
        private int _lastMouseX;
        private int _lastMouseY;
        private List<(UnitType ut, int? cost)> _shopSpecs;
        private bool _gameOver;

        // During combat, we may render a different board (the host's board)
        private Board? _combatBoard;
        private SkiaBoardVisualizer? _combatVisualizer;

        // Required by XAML loader; not used at runtime.
        public PlayerWindow() : this(null!, 0) { }

        public PlayerWindow(PvPOrchestrator orchestrator, int playerId)
        {
            InitializeComponent();
            _orchestrator = orchestrator;
            _playerId = playerId;
            _manager = orchestrator.Manager;
            _myBoard = _manager.GetBoard(playerId);
            _myPlayer = _manager.GetPlayer(playerId);

            _image = this.FindControl<Image>("RenderImage")!;
            _shopSpecs = new List<(UnitType, int?)>();

            Title = $"MicroAutoChess - PvP (Player {playerId})";
            WindowState = WindowState.FullScreen;
            Opened += OnOpened;
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            var bounds = this.ClientSize;
            int w = Math.Max(1, (int)bounds.Width);
            int h = Math.Max(1, (int)bounds.Height);
            int baseCell = Math.Max(20, (int)(Math.Min(w, h) * 0.03));

            _skiaVisualizer = new SkiaBoardVisualizer(
                _myBoard, renderFps: 30, cellRadius: baseCell,
                margin: 16, windowWidth: w, windowHeight: h);
            _skiaVisualizer.ViewerTeam = _myPlayer.team;

            UpdateShopSpecs();

            // Mouse tracking
            _image.PointerMoved += (s, ev) =>
            {
                var pt = ev.GetPosition(_image);
                _lastMouseX = Math.Max(0, (int)pt.X);
                _lastMouseY = Math.Max(0, (int)pt.Y);
                var viz = ActiveVisualizer;
                if (viz.IsDragging) viz.UpdateDragHover(_lastMouseX, _lastMouseY);
            };

            _image.PointerPressed += OnPointerPressed;
            _image.PointerReleased += OnPointerReleased;

            _renderTimer = new System.Timers.Timer(16); // ~60 FPS render
            _renderTimer.Elapsed += (s, ev) => Dispatcher.UIThread.Post(() => RenderFrame());
            _renderTimer.Start();
        }

        /// <summary>The visualizer to use for the current phase.</summary>
        private SkiaBoardVisualizer ActiveVisualizer =>
            _combatVisualizer ?? _skiaVisualizer;

        /// <summary>The board to render for the current phase.</summary>
        private Board ActiveBoard =>
            _combatBoard ?? _myBoard;

        // =====================================================================
        // Phase lifecycle (called by HumanPlayer via orchestrator)
        // =====================================================================

        public void OnPreparationPhase()
        {
            // Switch back to own board
            _combatBoard = null;
            _combatVisualizer = null;
            UpdateShopSpecs();
        }

        public void OnCombatPhase()
        {
            // Determine which board hosts our combat
            var hostId = _manager.GetCombatHostForPlayer(_playerId);
            if (hostId.HasValue && hostId.Value != _playerId)
            {
                // We're the non-host — view the host's board
                _combatBoard = _manager.GetBoard(hostId.Value);
                var bounds = this.ClientSize;
                int w = Math.Max(1, (int)bounds.Width);
                int h = Math.Max(1, (int)bounds.Height);
                int baseCell = Math.Max(20, (int)(Math.Min(w, h) * 0.03));
                _combatVisualizer = new SkiaBoardVisualizer(
                    _combatBoard, renderFps: 30, cellRadius: baseCell,
                    margin: 16, windowWidth: w, windowHeight: h);
                _combatVisualizer.ViewerTeam = _myPlayer.team;
            }
            else
            {
                // We're the host — use our own board
                _combatBoard = null;
                _combatVisualizer = null;
            }
        }

        public void OnGameOver(int? winnerId)
        {
            _gameOver = true;
            _combatBoard = null;
            _combatVisualizer = null;
        }

        // =====================================================================
        // Input handling
        // =====================================================================

        private void OnPointerPressed(object? s, Avalonia.Input.PointerPressedEventArgs ev)
        {
            var pt = ev.GetPosition(_image);
            int mx = Math.Max(0, (int)pt.X);
            int my = Math.Max(0, (int)pt.Y);
            var viz = ActiveVisualizer;
            var pauseRect = viz.GetPauseButtonRect();
            var speedUpRect = viz.GetSpeedUpRect();
            var speedDownRect = viz.GetSpeedDownRect();

            // Shop button clicks → BUY_UNIT
            if (_manager.Phase == PvPGamePhase.PREPARATION && _shopSpecs != null)
            {
                // Buy XP button
                var buyXpRect = viz.GetBuyXpButtonRect(_shopSpecs.Count);
                if (mx >= buyXpRect.Left && mx <= buyXpRect.Right && my >= buyXpRect.Top && my <= buyXpRect.Bottom)
                {
                    var buyXpAction = new PlayerAction(_playerId, PlayerActionType.BUY_EXPERIENCE);
                    _manager.EnqueueAction(buyXpAction);
                    ev.Pointer.Capture(null);
                    return;
                }

                // Reroll button
                var rerollRect = viz.GetRerollButtonRect(_shopSpecs.Count);
                if (mx >= rerollRect.Left && mx <= rerollRect.Right && my >= rerollRect.Top && my <= rerollRect.Bottom)
                {
                    var rerollAction = new PlayerAction(_playerId, PlayerActionType.REROLL_SHOP);
                    _manager.EnqueueAction(rerollAction);
                    UpdateShopSpecs();
                    ev.Pointer.Capture(null);
                    return;
                }

                int total = _shopSpecs.Count;
                for (int i = 0; i < total; i++)
                {
                    var r = viz.GetSpawnButtonRect(i, total: total);
                    if (mx >= r.Left && mx <= r.Right && my >= r.Top && my <= r.Bottom)
                    {
                        var buyAction = new PlayerAction(_playerId, PlayerActionType.BUY_UNIT) { ShopIndex = i };
                        _manager.EnqueueAction(buyAction);
                        UpdateShopSpecs();
                        ev.Pointer.Capture(null);
                        return;
                    }
                }
            }

            // Speed up
            if (mx >= speedUpRect.Left && mx <= speedUpRect.Right &&
                my >= speedUpRect.Top && my <= speedUpRect.Bottom)
            {
                _orchestrator.EngineFps = _orchestrator.EngineFps * 2.0;
                ev.Pointer.Capture(null);
                return;
            }

            // Speed down
            if (mx >= speedDownRect.Left && mx <= speedDownRect.Right &&
                my >= speedDownRect.Top && my <= speedDownRect.Bottom)
            {
                _orchestrator.EngineFps = _orchestrator.EngineFps * 0.5;
                ev.Pointer.Capture(null);
                return;
            }

            // Pause/Start button
            if (mx >= pauseRect.Left && mx <= pauseRect.Right &&
                my >= pauseRect.Top && my <= pauseRect.Bottom)
            {
                if (_manager.Phase == PvPGamePhase.PREPARATION)
                {
                    viz.DamageDone.Clear();
                    viz.UnitInfo.Clear();
                    _orchestrator.RequestBeginCombat();
                }
                ev.Pointer.Capture(null);
                return;
            }

            // Drag during PREPARATION
            if (_manager.Phase == PvPGamePhase.PREPARATION)
            {
                bool began = viz.BeginDragAt(mx, my);
                if (began) { ev.Pointer.Capture(null); return; }
            }
        }

        private void OnPointerReleased(object? s, Avalonia.Input.PointerReleasedEventArgs ev)
        {
            var pt = ev.GetPosition(_image);
            int mx = Math.Max(0, (int)pt.X);
            int my = Math.Max(0, (int)pt.Y);
            var viz = ActiveVisualizer;

            if (viz.IsDragging)
            {
                int total = _shopSpecs?.Count ?? 5;
                try
                {
                    var sellResult = TrySellDraggedUnit(mx, my, total);
                    if (!sellResult) EndDragAt(mx, my);
                }
                catch
                {
                    EndDragAt(mx, my);
                }
                ev.Pointer.Capture(null);
            }
        }

        private bool TrySellDraggedUnit(int mouseX, int mouseY, int spawnTotal = 5)
        {
            var viz = ActiveVisualizer;
            var dragged = viz.DraggedUnit;
            var dragFrom = viz.DragFrom;
            if (dragged == null || !dragFrom.HasValue) return false;
            if (!viz.IsMouseOverShop(mouseX, mouseY, spawnTotal)) return false;

            viz.ClearDragState();

            var sellAction = new PlayerAction(_playerId, PlayerActionType.SELL_UNIT) { TargetUnit = dragged };
            _manager.EnqueueAction(sellAction);
            UpdateShopSpecs();
            return true;
        }

        private bool EndDragAt(int mouseX, int mouseY)
        {
            var viz = ActiveVisualizer;
            var dragged = viz.DraggedUnit;
            var dragFrom = viz.DragFrom;
            if (dragged == null || !dragFrom.HasValue) return false;

            (int, int)? targetPos = viz.HitTestBoardCell(mouseX, mouseY);
            if (!targetPos.HasValue)
                targetPos = viz.HitTestBenchCell(mouseX, mouseY);

            bool moved = false;
            if (targetPos.HasValue)
            {
                // Bench→board at max units (and no swap target): show floating message instead
                if (dragFrom.Value.Item1 < 0 && targetPos.Value.Item1 >= 0
                    && _myPlayer.UnitsOnBoard.Count >= _myPlayer.MaxUnitsOnBoard
                    && _myBoard.Cells.TryGetValue(targetPos.Value, out var cell) && cell.Unit == null)
                {
                    var now = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;
                    viz.FloatingTexts.Add(new object[] {
                        _lastMouseX, _lastMouseY - 20,
                        "Level up to increase your army size!",
                        now, 2.0, 0.0, -40.0, new int[] { 255, 200, 60 }
                    });
                }
                else
                {
                    var moveAction = new PlayerAction(_playerId, PlayerActionType.MOVE_UNIT)
                    {
                        FromPosition = dragFrom.Value,
                        ToPosition = targetPos.Value
                    };
                    _manager.EnqueueAction(moveAction);
                    moved = true;
                }
            }

            viz.ClearDragState();
            return moved;
        }

        // =====================================================================
        // Rendering
        // =====================================================================

        private void UpdateShopSpecs()
        {
            _shopSpecs.Clear();
            foreach (var unit in _myPlayer.ShopUnits)
            {
                if (unit != null)
                    _shopSpecs.Add((unit.UnitType, unit.GetCost()));
                else
                    _shopSpecs.Add((default, null));
            }
        }

        private void RenderFrame()
        {
            // Keep shop specs in sync with actual player state (actions are processed asynchronously)
            if (_manager.Phase == PvPGamePhase.PREPARATION)
                UpdateShopSpecs();

            var bounds = this.ClientSize;
            int w = Math.Max(1, (int)bounds.Width);
            int h = Math.Max(1, (int)bounds.Height);

            // Advance combat engines first, then read state (matches PvE pattern:
            // step → read FrameNumber → use fractional remainder for interpolation)
            float simProgress = _orchestrator.AdvanceCombat();

            var viz = ActiveVisualizer;

            // Get combat engine AFTER advancing so FrameNumber is current
            CombatEngine? engine = null;
            if (_manager.Phase == PvPGamePhase.COMBAT)
            {
                var hostId = _manager.GetCombatHostForPlayer(_playerId);
                if (hostId.HasValue) engine = _manager.GetCombatEngine(hostId.Value);
            }

            bool isPaused = _manager.Phase != PvPGamePhase.COMBAT;
            int simFrame = engine?.FrameNumber ?? 0;

            // Create SkiaSharp surface
            var info = new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
            using var surface = SkiaSharp.SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(new SkiaSharp.SKColor(30, 30, 30));

            viz.Draw(canvas, w, h, engine, simFrame, simProgress, isPaused,
                _lastMouseX, _lastMouseY, _shopSpecs, _myPlayer.Gold,
                _orchestrator.EngineFps,
                player: _myPlayer,
                gameParams: _manager.Params);

            // Copy pixels to WriteableBitmap for Avalonia display
            var skInfo = new SkiaSharp.SKImageInfo(w, h, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
            int length = skInfo.RowBytes * skInfo.Height;
            IntPtr temp = Marshal.AllocHGlobal(length);
            try
            {
                surface.ReadPixels(skInfo, temp, skInfo.RowBytes, 0, 0);
                var buffer = new byte[length];
                Marshal.Copy(temp, buffer, 0, length);

                var wb = new WriteableBitmap(
                    new PixelSize(w, h), new Vector(96, 96),
                    PixelFormat.Bgra8888, AlphaFormat.Premul);
                using (var fb = wb.Lock())
                {
                    Marshal.Copy(buffer, 0, fb.Address, length);
                }

                _image.Source = wb;
            }
            finally
            {
                Marshal.FreeHGlobal(temp);
            }
        }
    }
}
