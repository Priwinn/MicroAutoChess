using SkiaSharp;
using MicroAutoChess.Core;
using MicroAutoChess.Core.Spells;
using System.Collections.Generic;
using System;

namespace MicroAutoChess.AvaloniaApp
{
	// Skeleton Skia-based visualizer: copies layout/state and provides placeholder helpers
	public class SkiaBoardVisualizer
	{
		// Core references
		public Board Board { get; }

		// Layout / rendering parameters
		public int RenderFps { get; }
		public int CellRadius { get; private set; }
		public int Margin { get; }
		public int BenchGap { get; private set; }
		public int TopMargin { get; private set; }
		public int LeftOffset { get; private set; }
		public int WindowWidth { get; }
		public int WindowHeight { get; }

		// Visual defaults
		public SKColor BgColor { get; } = new SKColor(30, 30, 30);
		public SKColor GridColor { get; } = new SKColor(80, 80, 80);
		public Dictionary<Team, SKColor> TeamColors { get; } = new Dictionary<Team, SKColor>()
		{
			{Team.TEAM_1, new SKColor(60,140,220)},
			{Team.TEAM_2, new SKColor(220,100,100)}
		};

		// Fonts stored as sizes
		public int FontSize { get; }
		public int TooltipFontSize { get; }
		public int DamageFontSize { get; }
		public int TitleFontSize { get; }

		// Right/bottom panel sizes
		public int RightPanelWidth { get; private set; }
		public int BottomPanelHeight { get; private set; }

		// Optional override
		public int? SpawnStartX { get; set; }

		// Interaction / animation state
		public bool HighlightPlayerInitialZone { get; set; } = false;
		public (int, int)? HighlightHoveredCell { get; set; } = null;

		// Dragging state (for pre-combat unit repositioning)
		public bool IsDragging => _draggedUnit != null;
		private Unit? _draggedUnit = null;
		private (int, int)? _dragFrom = null;
		public Unit? DraggedUnit => _draggedUnit;
		public (int, int)? DragFrom => _dragFrom;
		public List<(int uid, double start, double duration, double speed)> SpinAnimations { get; } = new List<(int, double, double, double)>();
		public List<object> AoEAnimations { get; } = new List<object>();
		public List<object> Particles { get; } = new List<object>();
		public List<object> DeathAnimations { get; } = new List<object>();
		public List<object> FloatingTexts { get; } = new List<object>();
		// Deferred tooltip surfaces to draw last (spawn tooltips etc.)
		private List<(SKBitmap bmp, int x, int y)> _deferredTooltips = new List<(SKBitmap, int, int)>();

		// Event tracking
		private HashSet<int> _seenEvents = new HashSet<int>();
		private Random _rand = new Random();

		// Runtime state containers
		public Dictionary<int, double> DamageDone { get; } = new Dictionary<int, double>();
		public Dictionary<int, (string symbol, Team team)> UnitInfo { get; } = new Dictionary<int, (string, Team)>();

		// Performance flags
		public bool UseAntialiasing { get; set; } = false;

		/// <summary>
		/// The team of the viewing player. When TEAM_1, the board is mirrored
		/// so the viewer's units always appear at the bottom.
		/// </summary>
		public Team ViewerTeam { get; set; } = Team.TEAM_2;
		private int _mirrorSumX;
		private int _mirrorSumY;

		public SkiaBoardVisualizer(Board board, int renderFps = 30, int cellRadius = 28, int margin = 16, int? windowWidth = null, int? windowHeight = null, int? leftOffset = null, int? topMargin = null)
		{
			Board = board ?? throw new ArgumentNullException(nameof(board));
			RenderFps = renderFps;
			Margin = (int) cellRadius;

			int desiredW = windowWidth ?? 1280;
			int desiredH = windowHeight ?? 800;

			int origCell = cellRadius;
			int estRight = Math.Max(320, (int)(origCell * 6));
			int estBottom = Math.Max(60, (int)(origCell * 1.0));
			int estW = (int)(origCell * Math.Sqrt(3) * (Board.Width + 0.5)) + margin * 2 + estRight;
			int estH = (int)(origCell * 1.5 * (Board.Height + 3)) + margin * 2 + estBottom;

			double scale = Math.Min((double)desiredW / Math.Max(1, estW), (double)desiredH / Math.Max(1, estH));
			scale = Math.Max(0.5, Math.Min(scale, 4.0));

			CellRadius = Math.Max(10, (int)(origCell * scale));
			BenchGap = CellRadius * 2;
			TopMargin = margin + BenchGap;
			FontSize = Math.Max(12, (int)(cellRadius * 0.6));
			TooltipFontSize = Math.Max(9, (int)(CellRadius * 0.45));
			DamageFontSize = Math.Max(14, (int)(CellRadius * 0.6));
			TitleFontSize = Math.Max(18, (int)(CellRadius * 0.9));

			RightPanelWidth = Math.Max(480, (int)(CellRadius * 8));
			BottomPanelHeight = Math.Max(60, (int)(CellRadius * 1.0));

			WindowWidth = desiredW;
			WindowHeight = desiredH;

			LeftOffset = (int)(WindowWidth * 0.06) + (int)(WindowWidth * 0.04);

			if (leftOffset.HasValue) LeftOffset = leftOffset.Value;
			if (topMargin.HasValue) TopMargin = topMargin.Value;

			// Precompute board pixel extents for mirror support
			int minBPx = int.MaxValue, maxBPx = int.MinValue;
			int minBPy = int.MaxValue, maxBPy = int.MinValue;
			foreach (var cellKey in Board.Cells.Keys)
			{
				var (bpx, bpy) = Board.CoordToPixel(cellKey, CellRadius);
				if (bpx < minBPx) minBPx = bpx; if (bpx > maxBPx) maxBPx = bpx;
				if (bpy < minBPy) minBPy = bpy; if (bpy > maxBPy) maxBPy = bpy;
			}
			_mirrorSumX = (Board.Cells.Count > 0) ? minBPx + maxBPx : 0;
			_mirrorSumY = (Board.Cells.Count > 0) ? minBPy + maxBPy : 0;
		}

		/// <summary>
		/// CoordToPixel wrapper that mirrors both axes when ViewerTeam is TEAM_1,
		/// so the viewer's units always appear at the bottom of the board.
		/// </summary>
		public (int, int) CellPixel((int, int) cellPos)
		{
			var (px, py) = Board.CoordToPixel(cellPos, CellRadius);
			if (ViewerTeam == Team.TEAM_1) { px = _mirrorSumX - px; py = _mirrorSumY - py; }
			return (px, py);
		}

		// Drag helpers ---------------------------------------------------------
		public void ClearDragState()
		{
			_draggedUnit = null;
			_dragFrom = null;
			HighlightPlayerInitialZone = false;
			HighlightHoveredCell = null;
		}

		public (int, int)? HitTestBoardCell(int mouseX, int mouseY)
		{
			foreach (var kv in Board.Cells)
			{
				var cellPos = kv.Key;
				var cellPix = CellPixel(cellPos);
				int centerX = cellPix.Item1 + LeftOffset + Margin;
				int centerY = cellPix.Item2 + TopMargin + CellRadius + 4;
				var corners = Board.GetCellCorners((centerX, centerY), CellRadius);
				if (PointInPolygon(mouseX, mouseY, corners)) return cellPos;
			}
			return null;
		}

		public (int, int)? HitTestBenchCell(int mouseX, int mouseY)
		{
			for (int col = 0; col < Board.BenchSize; col++)
			{
				var topCenter = GetBenchCellCenter((-1, col));
				if (topCenter.HasValue)
				{
					var corners = GetBenchCellCorners(topCenter.Value, CellRadius);
					if (PointInPolygon(mouseX, mouseY, corners)) return (-1, col);
				}
				var botCenter = GetBenchCellCenter((-2, col));
				if (botCenter.HasValue)
				{
					var corners = GetBenchCellCorners(botCenter.Value, CellRadius);
					if (PointInPolygon(mouseX, mouseY, corners)) return (-2, col);
				}
			}
			return null;
		}

		public bool IsMouseOverShop(int mouseX, int mouseY, int spawnTotal = 4)
		{
			var r0 = GetSpawnButtonRect(0, spawnTotal);
			var rLast = GetSpawnButtonRect(Math.Max(0, spawnTotal - 1), spawnTotal);
			float shopLeft = r0.Left;
			float shopRight = rLast.Right;
			float shopTop = r0.Top;
			float shopBottom = r0.Bottom + (r0.Height * 0.5f);
			return mouseX >= shopLeft && mouseX <= shopRight && mouseY >= shopTop && mouseY <= shopBottom;
		}

		public bool BeginDragAt(int mouseX, int mouseY)
		{
			// hit-test bench cells first
			// Only allow dragging from viewer's bench
			int viewerBenchRow = -(int)ViewerTeam;
			for (int col = 0; col < Board.BenchSize; col++)
			{
				var benchCenter = GetBenchCellCenter((viewerBenchRow, col));
				if (benchCenter.HasValue)
				{
					var corners = GetBenchCellCorners(benchCenter.Value, CellRadius);
					if (PointInPolygon(mouseX, mouseY, corners))
					{
						var unit = Board.GetBenchUnit(ViewerTeam, col);
						if (unit != null) { _draggedUnit = unit; _dragFrom = (viewerBenchRow, col); HighlightPlayerInitialZone = true; return true; }
					}
				}
			}

			// then board cells
			foreach (var kv in Board.Cells)
			{
				var pos = kv.Key;
				var pix = CellPixel(pos);
				int centerX = pix.Item1 + LeftOffset + Margin;
				int centerY = pix.Item2 + TopMargin + CellRadius + 4;
				var corners = Board.GetCellCorners((centerX, centerY), CellRadius);
				if (PointInPolygon(mouseX, mouseY, corners))
				{
					var cell = Board.Cells[pos];
					if (cell != null && cell.Unit != null && cell.Unit.Team == ViewerTeam)
					{
						_draggedUnit = cell.Unit;
						_dragFrom = pos;
						HighlightPlayerInitialZone = true;
						return true;
					}
				}
			}
			return false;
		}

		public void UpdateDragHover(int mouseX, int mouseY)
		{
			// compute hovered board or bench cell under mouse and set HighlightHoveredCell
			(int, int)? pos = null;
			foreach (var kv in Board.Cells)
			{
				var cellPos = kv.Key;
				var cellPix = CellPixel(cellPos);
				int centerX = cellPix.Item1 + LeftOffset + Margin;
				int centerY = cellPix.Item2 + TopMargin + CellRadius + 4;
				var corners = Board.GetCellCorners((centerX, centerY), CellRadius);
				if (PointInPolygon(mouseX, mouseY, corners)) { pos = cellPos; break; }
			}
			if (!pos.HasValue)
			{
				for (int col = 0; col < Board.BenchSize; col++)
				{
					var topCenter = GetBenchCellCenter((-1, col));
					if (topCenter.HasValue)
					{
						var corners = GetBenchCellCorners(topCenter.Value, CellRadius);
						if (PointInPolygon(mouseX, mouseY, corners)) { pos = (-1, col); break; }
					}
					var botCenter = GetBenchCellCenter((-2, col));
					if (botCenter.HasValue)
					{
						var corners = GetBenchCellCorners(botCenter.Value, CellRadius);
						if (PointInPolygon(mouseX, mouseY, corners)) { pos = (-2, col); break; }
					}
				}
			}

			// If currently dragging, only show hover highlight when the hovered
			// position is a valid placement target for the dragged unit's player.
			if (IsDragging && _draggedUnit != null && pos.HasValue)
			{
				bool allowed = false;
				var team = _draggedUnit.Team;
				if (pos.Value.Item1 >= 0)
				{
					// board cell: must be within player's initial placement zone
					var valid = Board.GetInitialPositions(team);
					allowed = valid.Contains(pos.Value);
				}
				else
				{
					// bench cell: allowed if bench belongs to the same team (bench coords are -1 or -2)
					allowed = (pos.Value.Item1 == -(int)team);
				}
				HighlightHoveredCell = allowed ? pos : null;
			}
			else
			{
				HighlightHoveredCell = pos;
			}
		}

		// Primary draw entry used by the Avalonia host (placeholder)
		public void Draw(SKCanvas canvas, int width, int height, CombatEngine? engine, int simFrame, float simProgress, bool paused = false, int mouseX = 0, int mouseY = 0, System.Collections.Generic.List<(MicroAutoChess.Core.UnitType ut, int? cost)>? spawnSpecs = null, int? spawnBudget = null, double engineFps = 10.0, Player? player = null, GameParams? gameParams = null)
		{
			// Clear background
			canvas.Clear(BgColor);

			// Placeholder sequence mirroring BoardVisualizer
			var now = DateTime.UtcNow;

			// Collect animations from engine and reuse the same containers for drawing
			var highlightedPositions = new HashSet<(int, int)>();
			var movingMap = new Dictionary<int, (Unit unit, int ix, int iy, object action)>();
			var attackAnims = new List<object>();
			var spellProjectiles = new List<object>();
			CollectAnimationsFromEngine(engine, simFrame, simProgress, movingMap as Dictionary<int, (Unit, int, int, object)>, attackAnims, spellProjectiles);

			// Draw cells, units, animations, charts
			DrawCells(highlightedPositions, movingMap, now, canvas);
			DrawMovingUnits(movingMap, now, canvas);
			DrawAttackAndProjectiles(engine, attackAnims, spellProjectiles, canvas);
			DrawAoEAnimations(now, canvas);
			DrawParticles(now, canvas);
			DrawDeathAnimations(now, canvas);
			DrawDamageCharts(engine, canvas);
			DrawFloatingTexts(now, canvas);
			DrawSpeedButtons(canvas, engineFps);
			DrawPauseButton(canvas, paused);
			DrawSpawnButtons(canvas, spawnSpecs, spawnBudget, mouseX, mouseY);
			DrawShopPanel(canvas, spawnSpecs?.Count ?? 5, spawnBudget, player, gameParams, mouseX, mouseY);
			DrawHoverTooltip(canvas, mouseX, mouseY);
			if (_draggedUnit != null)
			{
				// update hover cell based on passed mouse coords
				UpdateDragHover(mouseX, mouseY);
				// draw ghost unit at mouse position
				DrawUnitWithLevelBorder(mouseX, mouseY, _draggedUnit, null, canvas);
				var symbol = _draggedUnit?.GetSymbol() ?? "?";
				DrawText(canvas, symbol, FontSize, new SKColor(255,255,255), mouseX, mouseY);

				// Draw sell/shop hint overlay (match Python visualizer placement and transparency)
				int spawnTotal = spawnSpecs?.Count ?? 5;
				var spawn0 = GetSpawnButtonRect(0, spawnTotal);
				// align shop left with board left offset so it sits directly below the board
				float shopX = LeftOffset + Margin;
				float shopW = spawnTotal * spawn0.Width + 4 * (CellRadius / 6.5f);
				int shopH = (int)(spawn0.Height * 1.5f);
				float shopY = WindowHeight - spawn0.Height - Margin;
				var shopRect = new SKRect(shopX, shopY, shopX + shopW, shopY + shopH);

				// semi-transparent highlight for shop area
				using (var paint = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(80, 80, 80, 120), IsAntialias = UseAntialiasing })
				{
					canvas.DrawRoundRect(shopRect, 6, 6, paint);
				}

				// draw sell hint text inside the shop area with small dark rounded background
				string tip = "Drag here to sell unit";
				int pad = Math.Max(4, CellRadius / 6);
				int bigSize = Math.Max(12, TooltipFontSize * 2);
				using (var textPaint = new SKPaint { Color = new SKColor(240, 240, 240), IsAntialias = UseAntialiasing, TextSize = bigSize })
				{
					var tb = new SKRect();
					textPaint.MeasureText(tip, ref tb);
					int tsW = (int)tb.Width;
					int tsH = (int)(tb.Height + 4);
					float bx = shopRect.MidX - tsW / 2 - pad;
					float by = shopRect.Top + pad;
					var bgRect = new SKRect(bx, by, bx + tsW + pad * 2, by + tsH + pad);
					using (var bg = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(30, 30, 30, 220), IsAntialias = UseAntialiasing }) canvas.DrawRoundRect(bgRect, 6, 6, bg);
					// draw text centered in bgRect
					float tx = bgRect.MidX;
					float ty = bgRect.MidY - (tb.MidY - tb.Top) / 2;
					DrawText(canvas, tip, bigSize, new SKColor(240,240,240), tx, ty);
				}
			}
			// draw any deferred tooltips last so they appear above other UI
			FlushDeferredTooltips(canvas);
		}

		// Placeholder helpers (to be implemented during the full port)
		private void ClearBackground(SKColor color) { /* no-op placeholder */ }

		// Ported helper matching Core.ColorRgb usage
		private SKColor ToSKColor(Core.ColorRgb c) => new SKColor((byte)Math.Clamp(c.R, 0, 255), (byte)Math.Clamp(c.G, 0, 255), (byte)Math.Clamp(c.B, 0, 255));

		private void ClearBackground(SKCanvas canvas, Core.ColorRgb color)
		{
			canvas.Clear(ToSKColor(color));
		}

		private void CollectAnimationsFromEngine(CombatEngine? engine, int simFrame, float simProgress, Dictionary<int, (Unit, int, int, object)> movingMap, List<object> attackAnims, List<object> spellProjectiles)
		{
			if (engine == null) return;
			var pending = engine.GetPendingActions();
			foreach (var action in pending)
			{
				if (action.ActionType == CombatAction.MOVE) CollectMoveAnimation(action, simFrame, simProgress, movingMap);
				else if (action.ActionType == CombatAction.ATTACK) CollectAttackAnimation(action, simFrame, simProgress, attackAnims);
				else if (action.ActionType == CombatAction.CAST_SPELL) CollectCastSpellAnimation(action, simFrame, simProgress, spellProjectiles);
			}


			double now = DateTime.Now.Subtract(DateTime.UnixEpoch).TotalSeconds;
			var log = engine.CombatLog;
			if (log != null)
			{
				foreach (var ev in log)
				{
					switch (ev.EventType)
					{
						case CombatEventType.ACTION_PLANNED:
							if (!string.IsNullOrEmpty(ev.SpellName)) HandleSpellPlanning(ev, now);
							break;
						case CombatEventType.SPELL_EXECUTED:
							HandleSpellExecution(ev, now);
							break;
						case CombatEventType.DAMAGE_DEALT:
						case CombatEventType.HEALING_DONE:
							HandleDamageEvent(ev, now);
							break;
						case CombatEventType.UNIT_DIED:
							HandleUnitDied(ev, now);
							break;
						default:
							break;
					}
				}
			}

		}

		private static int GetEventId(object ev) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(ev);

		private void HandleSpellPlanning(CombatEvent ev, double now)
		{
			if (ev == null) return;
			int id = GetEventId(ev);
			if (_seenEvents.Contains(id)) return;
			var source = ev.Source;
			if (source == null) return;
			int uid = source.Id;
			double spinDuration = 0.6;
			double spinSpeed = 360.0 / spinDuration;
			SpinAnimations.Add((uid, now, spinDuration, spinSpeed));
			_seenEvents.Add(id);
		}
		private void HandleSpellExecution(CombatEvent ev, double now)
		{
			if (ev == null) return;
			int id = GetEventId(ev);
			if (_seenEvents.Contains(id)) return;
			// Get spell instance by name (CombatEvent carries SpellName)
			AbstractSpell? spellInst = null;
			string? spellName = ev.SpellName;
			if (!string.IsNullOrEmpty(spellName)) spellInst = SpellsFactory.GetSpellInstanceByName(spellName);
			if (spellInst == null) return;

			// call on-hit render callback if provided by the spell
			Dictionary<string, object?>? desc = null;
			desc = spellInst.OnHitRenderCallback(ev.Source, Board);

			// determine position for effect
			var pos = ev.Position;
			var target = ev.Target;
			if (!pos.HasValue && target != null) pos = target.Position;
			if (desc == null || !pos.HasValue) return;

			// read descriptor 'type' as string
			string? dtype = null;
			if (desc.TryGetValue("type", out var typeObj) && typeObj != null) dtype = typeObj.ToString(); 

			if (dtype == "aoe")
			{
				int radius = 1;
				double duration = 0.6;
				object? rObj = null;
				object? durObj = null;
				object? colorObj = null;
				if (desc.TryGetValue("radius_hex", out var rv)) rObj = rv;
				if (desc.TryGetValue("duration", out var dv)) durObj = dv;
				if (desc.TryGetValue("color_hint", out var cv)) colorObj = cv;
				if (rObj != null) radius = Convert.ToInt32(rObj);
				if (durObj != null) duration = Convert.ToDouble(durObj);

				Team? team = ev.Source?.Team;
				object color = colorObj ?? (object)TeamColors.GetValueOrDefault(team ?? 0, new SKColor(200, 40, 40));
				AoEAnimations.Add(new object[] { pos.Value, radius, now, duration, color });
				_seenEvents.Add(id);
			}
			else if (dtype == "particles")
			{
				var src = ev.Source;
				if (src == null) return;
				var sPos = src.Position;
				if (!sPos.HasValue) return;
				(int, int) pixel = sPos.Value;

				var pxy = CellPixel(pixel);
				int cx = pxy.Item1 + LeftOffset + Margin;
				int cy = pxy.Item2 + TopMargin + CellRadius + 4;

				int num = 20;
				double minLife = 0.5, maxLife = 1.2;
				int minSize = 3, maxSize = 6;
				double speedMin = 0.6, speedMax = 1.6;
				object? baseColorObj = null;
				if (desc.TryGetValue("base_color_hint", out var bch)) baseColorObj = bch;
				if (desc.TryGetValue("num", out var numObj)) num = Convert.ToInt32(numObj);
				if (desc.TryGetValue("lifetime_range", out var lr) && lr is double[] rng && rng.Length >= 2) { minLife = rng[0]; maxLife = rng[1]; }

				int[] baseColor;
				if (baseColorObj is int[] iarrc && iarrc.Length >= 3) baseColor = iarrc;
				else if (baseColorObj is Core.ColorRgb cr) baseColor = new int[] { cr.R, cr.G, cr.B };
				else if (baseColorObj is List<int> il && il.Count >= 3) baseColor = il.ToArray();
				else
				{
					// fallback to team color when no explicit base_color_hint provided
					var teamCol = TeamColors.ContainsKey(src.Team) ? TeamColors[src.Team] : new SKColor(200, 200, 50);
					baseColor = new int[] { teamCol.Red, teamCol.Green, teamCol.Blue };
				}

				for (int i = 0; i < num; i++)
				{
					double ang = (2 * Math.PI * i / Math.Max(1, num)) + (_rand.NextDouble() * 0.8 - 0.4);
					double speed = _rand.NextDouble() * (CellRadius * (speedMax - speedMin)) + CellRadius * speedMin;
					double vx = Math.Cos(ang) * speed;
					double vy = Math.Sin(ang) * speed;
					double lifetime = _rand.NextDouble() * (maxLife - minLife) + minLife;
					int size = _rand.Next(minSize, maxSize + 1);
					int rcol = Math.Min(255, (int)(baseColor[0] * (0.9 + _rand.NextDouble() * 0.5)));
					int gcol = Math.Min(255, (int)(baseColor[1] * (0.9 + _rand.NextDouble() * 0.5)));
					int bcol = Math.Min(255, (int)(baseColor[2] * (0.9 + _rand.NextDouble() * 0.5)));
					var colorArr = new int[] { rcol, gcol, bcol };
					Particles.Add(new object[] { cx, cy, vx, vy, now, lifetime, colorArr, size });
				}
				_seenEvents.Add(id);
			}

		}
		private void HandleDamageEvent(CombatEvent ev, double now)
		{
			if (ev == null) return;
			int id = GetEventId(ev);
			if (_seenEvents.Contains(id)) return;
			double dmg = ev.Damage;
			if (dmg <= 0) return;

			var src = ev.Source;
			if (src != null)
			{
				int sid = src.Id;
				if (!DamageDone.ContainsKey(sid)) DamageDone[sid] = 0.0;
				DamageDone[sid] += dmg;
				string sym = src?.GetSymbol() ?? "?";
				Team team = src.Team;
				UnitInfo[sid] = (sym, team);
			}

			var pos = ev.Position;
			var target = ev.Target;
			if (!pos.HasValue && target != null) pos = target.Position;
			if (!pos.HasValue) return;

			// convert pos to pixel
			var pcell = pos.Value;
			var pix = CellPixel(pcell);
			int cx = pix.Item1 + LeftOffset + Margin;
			int cy = pix.Item2 + TopMargin + CellRadius + 4;

			string txt = ((int)dmg).ToString();
			if (ev.CritBool) txt += "*";

			double duration = 0.5;
			double spread = Math.PI / 3.0;
			double gap = Math.PI / 8.0;
			double offset;
			while (true)
			{
				offset = (_rand.NextDouble() * 2.0 * spread) - spread;
				if (Math.Abs(offset) >= gap) break;
			}
			double theta = -Math.PI / 2.0 + offset;
			double speed = CellRadius * (_rand.NextDouble() * 0.4 + 1.2);
			double vx = Math.Cos(theta) * speed;
			double vy = Math.Sin(theta) * speed;
			var txtColorObj = (ev.EventType == CombatEventType.HEALING_DONE) ? new int[] { 50, 200, 100 } : new int[] { 255, 140, 0 };
			FloatingTexts.Add(new object[] { cx, cy - (int)(CellRadius * 0.8), txt, now, duration, vx, vy, txtColorObj });
			_seenEvents.Add(id);
		}
		private void HandleUnitDied(CombatEvent ev, double now)
		{

			if (ev == null) return;
			int id = GetEventId(ev);
			if (_seenEvents.Contains(id)) return;
			var pos = ev.Position;
			var target = ev.Target;
			if (!pos.HasValue && target != null) pos = target.Position;
			if (!pos.HasValue) { _seenEvents.Add(id); return; }
			var pix = CellPixel(pos.Value);
			int cx = pix.Item1 + LeftOffset + Margin;
			int cy = pix.Item2 + TopMargin + CellRadius + 4;

			Team team = target.Team;
			object color = TeamColors.ContainsKey(team) ? (object)TeamColors[team] : (object)new SKColor(220, 220, 220);
			double duration = 0.6;
			int radius = (int)(CellRadius * 0.45);
			string symbol = target?.GetSymbol() ?? "?";
			DeathAnimations.Add(new object[] { cx, cy, now, duration, color, radius, symbol });
			_seenEvents.Add(id);

		}

		private void CollectMoveAnimation(PlannedAction action, int simFrame, float simProgress, Dictionary<int, (Unit, int, int, object)> movingMap)
		{
			if (action == null) return;
			if (!action.StartPosition.HasValue || !action.TargetPosition.HasValue) return;
			if (action.ResolutionFrame <= simFrame) return;
			int startF = action.PlannedFrame;
			int endF = action.ResolutionFrame;
			int denom = Math.Max(1, endF - startF);
			double t = (simFrame + simProgress - startF) / (double)denom;
			t = Math.Max(0.0, Math.Min(1.0, t));

			var s = CellPixel(action.StartPosition.Value);
			var e = CellPixel(action.TargetPosition.Value);
			int cx1 = s.Item1 + LeftOffset + Margin;
			int cy1 = s.Item2 + TopMargin + CellRadius + 4;
			int cx2 = e.Item1 + LeftOffset + Margin;
			int cy2 = e.Item2 + TopMargin + CellRadius + 4;

			int ix = (int)(cx1 + (cx2 - cx1) * t);
			int iy = (int)(cy1 + (cy2 - cy1) * t);
			movingMap[action.Unit.Id] = (action.Unit, ix, iy, action);
		}

		private void CollectAttackAnimation(PlannedAction action, int simFrame, float simProgress, List<object> attackAnims)
		{
			if (action == null) return;
			if (!action.StartPosition.HasValue || action.Target == null || !action.Target.Position.HasValue) return;
			if (action.ResolutionFrame <= simFrame) return;
			int startF = action.PlannedFrame;
			int endF = action.ResolutionFrame;
			int denom = Math.Max(1, endF - startF);
			double t = (simFrame + simProgress - startF) / (double)denom;
			t = Math.Max(0.0, Math.Min(1.0, t));

			var s = CellPixel(action.StartPosition.Value);
			var e = CellPixel(action.Target.Position.Value);
			int cx1 = s.Item1 + LeftOffset + Margin;
			int cy1 = s.Item2 + TopMargin + CellRadius + 4;
			int cx2 = e.Item1 + LeftOffset + Margin;
			int cy2 = e.Item2 + TopMargin + CellRadius + 4;

			double curX = cx1 + (cx2 - cx1) * t;
			double curY = cy1 + (cy2 - cy1) * t;
			attackAnims.Add((action, cx1, cy1, curX, curY, t));
		}

		private void CollectCastSpellAnimation(PlannedAction action, int simFrame, float simProgress, List<object> spellProjectiles)
		{
			if (action == null) return;
			Dictionary<string, object?>? desc = null;
			if (action.SpellInstance != null) desc = action.SpellInstance.ProjectileRenderCallback(action.Unit, Board);
			
			if (desc == null) return;
			if (!action.StartPosition.HasValue) return;
			if (action.ResolutionFrame <= simFrame) return;
			int startF = action.PlannedFrame;
			int endF = action.ResolutionFrame;
			int denom = Math.Max(1, endF - startF);
			double t = (simFrame + simProgress - startF) / (double)denom;
			t = Math.Max(0.0, Math.Min(1.0, t));

			var s = CellPixel(action.StartPosition.Value);
			int cx1 = s.Item1 + LeftOffset + Margin;
			int cy1 = s.Item2 + TopMargin + CellRadius + 4;

			(int, int)? targetPos = null;
			if (action.Target != null && action.Target.Position.HasValue) targetPos = action.Target.Position.Value;
			else if (action.TargetPosition.HasValue) targetPos = action.TargetPosition.Value;
			if (!targetPos.HasValue) return;

			var e = CellPixel(targetPos.Value);
			int cx2 = e.Item1 + LeftOffset + Margin;
			int cy2 = e.Item2 + TopMargin + CellRadius + 4;

			double curX = cx1 + (cx2 - cx1) * t;
			double curY = cy1 + (cy2 - cy1) * t;
			spellProjectiles.Add((action, cx1, cy1, cx2, cy2, curX, curY, t, desc));
		}

		private void DrawCells(HashSet<(int, int)> highlightedPositions, Dictionary<int, (Unit unit, int ix, int iy, object action)> movingMap, DateTime now, SKCanvas canvas)
		{
			// Draw bench first
			DrawBenchCells(canvas);

			// Precompute player-2 initial positions when highlighting is requested
			var player2Initial = new HashSet<(int,int)>();
			if (HighlightPlayerInitialZone) {
				foreach (var p in Board.GetInitialPositions(ViewerTeam)) player2Initial.Add(p);
			}

			List<(int,int)>? yellowHighlightedCorners = null;
			List<List<(int,int)>> initialZoneCornersList = new();

			for (int x = 0; x < Board.Width; x++)
			{
				for (int y = 0; y < Board.Height; y++)
				{
					var cell = Board.Cells[(x, y)];

					// Convert cell coordinates to pixel coordinates
					var pix = CellPixel((x, y));
					int px = pix.Item1;
					int py = pix.Item2;
					int centerX = (int)(px + LeftOffset + Margin);
					int centerY = (int)(py + TopMargin + CellRadius + 4);

					// Get corners from board helper (expects pixel center)
					var corners = Board.GetCellCorners((centerX, centerY), CellRadius);

					// If hovered, save corners to draw yellow highlight on top
					if (HighlightHoveredCell.HasValue && HighlightHoveredCell.Value == (x, y))
					{
						yellowHighlightedCorners = new List<(int,int)>(corners);
					}


					// If we're showing the player's initial placement zone while dragging,
					// defer those cells to be drawn on top after all normal cells.
					if (HighlightPlayerInitialZone && player2Initial.Contains((x, y)))
					{
						initialZoneCornersList.Add(new List<(int,int)>(corners));
					}
					else if (highlightedPositions.Contains((x, y)))
					{
						DrawPolygon(canvas, corners, new SKColor(255,255,255), 2);
					}
					else
					{
						DrawPolygon(canvas, corners, GridColor, 2);
					}

					// Draw unit if present, not moving, and not being dragged
					if (cell != null && cell.Unit != null && !movingMap.ContainsKey(cell.Unit.Id) && cell.Unit != _draggedUnit)
					{
						var unit = cell.Unit;
						var team = unit.Team;

						// draw unit with level border and decorations
						DrawUnitWithLevelBorder(centerX, centerY, unit, null, canvas);

						// Unit symbol
						string symbol = unit?.GetSymbol() ?? "?";

						// spin animation check
						double angle = 0.0;
						for (int i = SpinAnimations.Count - 1; i >= 0; --i)
						{
							var s = SpinAnimations[i];
							if (s.uid == unit.Id)
							{
								double elapsed = now.Subtract(DateTime.UnixEpoch).TotalSeconds - s.start;
								if (elapsed <= s.duration) angle = (elapsed * s.speed) % 360.0;
								else SpinAnimations.RemoveAt(i);
							}
						}

						if (Math.Abs(angle) > 1e-9)
							DrawTextRotated(canvas, symbol, FontSize, new SKColor(255,255,255), centerX, centerY, angle);
						else
							DrawText(canvas, symbol, FontSize, new SKColor(255,255,255), centerX, centerY);

						// health/mana bars
						int barW = (int)(CellRadius * 1.2);
						int barH = Math.Max(3, (int)(CellRadius * 0.12));
						int hbX = centerX - barW / 2;
						int hbY = centerY - (int)(CellRadius * 0.75);
						DrawRect(canvas, hbX, hbY, barW, barH, new SKColor(50,50,50));
						double hpRatio = Math.Max(0.0, Math.Min(1.0, unit.CurrentHealth / unit.GetMaxHealth()));
						DrawRect(canvas, hbX, hbY, (int)(barW * hpRatio), barH, new SKColor(50,200,100));

						int mbY = centerY - (int)(CellRadius * 0.63);
						DrawRect(canvas, hbX, mbY, barW, barH, new SKColor(50,50,50));
						double maxMana = unit.BaseStats?.MaxMana ?? 1.0;
						double manaRatio = Math.Max(0.0, Math.Min(1.0, unit.CurrentMana / maxMana));
						DrawRect(canvas, hbX, mbY, (int)(barW * manaRatio), barH, new SKColor(80,140,220));
					}
				}
			}

			// Draw initial zone highlights on top of normal cells
			foreach (var izCorners in initialZoneCornersList)
			{
				DrawPolygon(canvas, izCorners, new SKColor(255,255,255), 2);
			}

			// Draw yellow highlight on top if requested
			if (yellowHighlightedCorners != null)
			{
				DrawPolygon(canvas, yellowHighlightedCorners, new SKColor(255,255,0), 3);
			}
		}
		private void DrawMovingUnits(Dictionary<int, (Unit unit, int ix, int iy, object action)> movingMap, DateTime now, SKCanvas canvas)
		{
			foreach (var kv in movingMap)
			{
				var tup = kv.Value;
				var unit = tup.unit;
				int ix = tup.ix;
				int iy = tup.iy;

				// draw unit circle and decorations
				DrawUnitWithLevelBorder(ix, iy, unit, null, canvas);

				// spin animations (units can spin while moving)
				double angle = 0.0;
				for (int i = SpinAnimations.Count - 1; i >= 0; --i)
				{
					var s = SpinAnimations[i];
					if (s.uid == unit.Id)
					{
						double elapsed = now.Subtract(DateTime.UnixEpoch).TotalSeconds - s.start;
						if (elapsed <= s.duration) angle = (elapsed * s.speed) % 360.0;
						else SpinAnimations.RemoveAt(i);
					}
				}

				var symbol = unit?.GetSymbol() ?? "?";
				if (Math.Abs(angle) > 1e-9)
					DrawTextRotated(canvas, symbol, FontSize, new SKColor(255,255,255), ix, iy, angle);
				else
					DrawText(canvas, symbol, FontSize, new SKColor(255,255,255), ix, iy);

				// health / mana bars drawn above the moving unit
				int barW = (int)(CellRadius * 1.2);
				int barH = Math.Max(3, (int)(CellRadius * 0.12));
				int hbX = ix - barW / 2;
				int hbY = iy - (int)(CellRadius * 0.75);
				DrawRect(canvas, hbX, hbY, barW, barH, new SKColor(50,50,50));
				double hpRatio = Math.Max(0.0, Math.Min(1.0, unit.CurrentHealth / unit.GetMaxHealth()));
				DrawRect(canvas, hbX, hbY, (int)(barW * hpRatio), barH, new SKColor(50,200,100));

				int mbY = iy - (int)(CellRadius * 0.63);
				DrawRect(canvas, hbX, mbY, barW, barH, new SKColor(50,50,50));
				double maxMana = unit.BaseStats?.MaxMana ?? 1.0;
				double manaRatio = Math.Max(0.0, Math.Min(1.0, unit.CurrentMana / maxMana));
				DrawRect(canvas, hbX, mbY, (int)(barW * manaRatio), barH, new SKColor(80,140,220));
			}
		}
		private void DrawAttackAndProjectiles(CombatEngine? engine, List<object> attackAnims, List<object> spellProjectiles, SKCanvas canvas)
		{
			// Draw basic attack lines and projectile tip
			foreach (var o in attackAnims)
			{
				var tup = ((PlannedAction, int, int, double, double, double))o;
				var action = tup.Item1;
				int sx = tup.Item2;
				int sy = tup.Item3;
				double tx = tup.Item4;
				double ty = tup.Item5;
				double t = tup.Item6;

				var atk = action.Unit;
				var teamCol = TeamColors.ContainsKey(atk.Team) ? TeamColors[atk.Team] : new SKColor(220, 220, 80);
				float width = Math.Max(1, (float)(CellRadius * 0.12 * (1.0 - t) + 1));
				using (var paint = new SKPaint { Style = SKPaintStyle.Stroke, Color = teamCol, StrokeWidth = width, IsAntialias = UseAntialiasing })
				{
					canvas.DrawLine(sx, sy, (float)tx, (float)ty, paint);
				}
				// projectile tip (white)
				int tipR = Math.Max(2, (int)(CellRadius * 0.08));
				DrawFilledCircle(canvas, (int)tx, (int)ty, tipR, new SKColor(255,255,255));
			}

			// Draw spell projectiles (with optional glow and descriptor hints)
			foreach (var o in spellProjectiles)
			{
				var tup = ((PlannedAction, int, int, int, int, double, double, double, Dictionary<string, object?>))o;
				var action = tup.Item1;
				int sx = tup.Item2;
				int sy = tup.Item3;
				int tx = tup.Item4;
				int ty = tup.Item5;
				double curX = tup.Item6;
				double curY = tup.Item7;
				double t = tup.Item8;
				var desc = tup.Item9;

				var src = action.Unit;
				SKColor color = TeamColors.ContainsKey(src.Team) ? TeamColors[src.Team] : new SKColor(200, 40, 40);
				bool glow = true;
				if (desc != null)
				{
					if (desc.TryGetValue("color_hint", out var ch) && ch != null)
					{
						if (ch is Core.ColorRgb cr) color = ToSKColor(cr);
						else if (ch is int[] iarr && iarr.Length >= 3) color = new SKColor((byte)iarr[0], (byte)iarr[1], (byte)iarr[2]);
						else if (ch is System.Collections.Generic.List<int> ilist && ilist.Count >= 3) color = new SKColor((byte)ilist[0], (byte)ilist[1], (byte)ilist[2]);
					}
					if (desc.TryGetValue("glow", out var gObj) && gObj is bool gb) glow = gb;
				}

				int projRadius = Math.Max(6, (int)(CellRadius * 0.18));
				if (glow)
				{
					int glowR = projRadius * 2;
					int glowAlpha = Math.Max(40, (int)(180 * (1.0 - t)));
					var glowCol = new SKColor(color.Red, color.Green, color.Blue, (byte)glowAlpha);
					DrawFilledCircle(canvas, (int)curX, (int)curY, glowR, glowCol);
				}

				// core projectile
				DrawFilledCircle(canvas, (int)curX, (int)curY, projRadius, color);
				// white highlight
				int small = Math.Max(1, projRadius / 3);
				DrawFilledCircle(canvas, (int)curX, (int)curY, small, new SKColor(255,255,255));

			}
		}
		private void DrawAoEAnimations(DateTime now, SKCanvas canvas)
		{
			var remaining = new List<object>();

			// Helper to extract tuple-like elements from boxed objects
			static object? GetTupleElement(object o, int index)
			{
				if (o is object[] arr)
				{
					if (index >= 0 && index < arr.Length) return arr[index];
					return null;
				}
				var t = o.GetType();
				var prop = t.GetProperty($"Item{index + 1}");
				if (prop != null) return prop.GetValue(o);
				return null;
			}

			foreach (var item in AoEAnimations)
			{
				// Unpack: [pos, radius_hex, start, duration, color]
				var posObj = GetTupleElement(item, 0);
				var radiusObj = GetTupleElement(item, 1);
				var startObj = GetTupleElement(item, 2);
				var durationObj = GetTupleElement(item, 3);
				var colorObj = GetTupleElement(item, 4);

				if (posObj == null) continue;

				// Parse position
				(int, int) pos;
				if (posObj is ValueTuple<int, int> vpos) pos = vpos;
				else if (posObj is Tuple<int, int> tpos) pos = (tpos.Item1, tpos.Item2);
				else if (posObj is int[] iarr && iarr.Length >= 2) pos = (iarr[0], iarr[1]);
				else if (posObj is System.Collections.Generic.List<int> ilist && ilist.Count >= 2) pos = (ilist[0], ilist[1]);
				else continue;

				int radius = 1;
				if (radiusObj is int ri) radius = ri;
				else if (radiusObj is long rl) radius = (int)rl;
				else if (radiusObj is double rd) radius = (int)rd;

				double startSec = 0.0;
				if (startObj is double sd) startSec = sd;
				else if (startObj is float sf) startSec = sf;
				else if (startObj is long sl) startSec = sl;
				else if (startObj is DateTime sdt) startSec = sdt.Subtract(DateTime.UnixEpoch).TotalSeconds;

				double duration = 0.6;
				if (durationObj is double dd) duration = dd;
				else if (durationObj is float df) duration = df;
				else if (durationObj is int di) duration = di;

				// compute elapsed in seconds; many places store starts as seconds since UnixEpoch
				double nowSec = now.Subtract(DateTime.UnixEpoch).TotalSeconds;
				double elapsed = nowSec - startSec;

				if (elapsed <= duration)
				{
					int alpha = Math.Max(0, Math.Min(200, (int)(200 * (1.0 - elapsed / duration))));

					// determine color
					SKColor color = new SKColor(200, 40, 40);
					if (colorObj is Core.ColorRgb cr) color = ToSKColor(cr);
					else if (colorObj is int[] carr && carr.Length >= 3) color = new SKColor((byte)carr[0], (byte)carr[1], (byte)carr[2], (byte)alpha);
					else if (colorObj is System.Collections.Generic.List<int> clist && clist.Count >= 3) color = new SKColor((byte)clist[0], (byte)clist[1], (byte)clist[2], (byte)alpha);
					else if (colorObj is SKColor skc) color = new SKColor(skc.Red, skc.Green, skc.Blue, (byte)alpha);
					else color = new SKColor(color.Red, color.Green, color.Blue, (byte)alpha);

					// Draw filled polygons for each cell in L1 range
					var cells = Board.GetCellsInL1Range(pos, radius);
					foreach (var c in cells)
					{
						var pix = CellPixel(c.Position);
						int centerX = (int)(pix.Item1 + LeftOffset + Margin);
						int centerY = (int)(pix.Item2 + TopMargin + CellRadius + 4);
						var corners = Board.GetCellCorners((centerX, centerY), CellRadius);
						// build path
						using var path = new SKPath();
						bool first = true;
						foreach (var (cx, cy) in corners)
						{
							if (first) { path.MoveTo(cx, cy); first = false; }
							else path.LineTo(cx, cy);
						}
						path.Close();
						using var paint = new SKPaint { Style = SKPaintStyle.Fill, Color = color, IsAntialias = UseAntialiasing };
						canvas.DrawPath(path, paint);
					}

					remaining.Add(item);
				}
			}

			// Replace AoEAnimations with survivors
			AoEAnimations.Clear();
			foreach (var r in remaining) AoEAnimations.Add(r);
		}
		private void DrawParticles(DateTime now, SKCanvas canvas)
		{
			var remaining = new List<object>();

			static object? GetTupleElement(object o, int index)
			{
				if (o is object[] arr)
				{
					if (index >= 0 && index < arr.Length) return arr[index];
					return null;
				}
				var t = o.GetType();
				var prop = t.GetProperty($"Item{index + 1}");
				if (prop != null) return prop.GetValue(o);
				return null;
			}

			foreach (var p in Particles)
			{
				// p: [x0,y0,vx,vy,start,lifetime,color,size]
				var x0o = GetTupleElement(p, 0);
				var y0o = GetTupleElement(p, 1);
				var vxo = GetTupleElement(p, 2);
				var vyo = GetTupleElement(p, 3);
				var starto = GetTupleElement(p, 4);
				var lifeo = GetTupleElement(p, 5);
				var coloro = GetTupleElement(p, 6);
				var sizeo = GetTupleElement(p, 7);

				if (x0o == null || y0o == null || vxo == null || vyo == null || starto == null || lifeo == null) continue;

				double x0 = Convert.ToDouble(x0o);
				double y0 = Convert.ToDouble(y0o);
				double vx = Convert.ToDouble(vxo);
				double vy = Convert.ToDouble(vyo);

				double startSec = 0.0;
				if (starto is double sd) startSec = sd;
				else if (starto is float sf) startSec = sf;
				else if (starto is long sl) startSec = sl;
				else if (starto is DateTime sdt) startSec = sdt.Subtract(DateTime.UnixEpoch).TotalSeconds;

				double lifetime = Convert.ToDouble(lifeo);

				double nowSec = now.Subtract(DateTime.UnixEpoch).TotalSeconds;
				double elapsed = nowSec - startSec;

				if (elapsed <= lifetime)
				{
					double curX = x0 + vx * elapsed;
					double curY = y0 + vy * elapsed;
					int alpha = Math.Max(0, Math.Min(255, (int)(255 * (1.0 - elapsed / lifetime))));

					int size = 4;
					if (sizeo != null) size = Convert.ToInt32(sizeo);

					// determine color
					SKColor color = new SKColor(255,255,255);
					if (coloro is Core.ColorRgb cr) color = ToSKColor(cr);
					else if (coloro is int[] carr && carr.Length >= 3) color = new SKColor((byte)carr[0], (byte)carr[1], (byte)carr[2]);
					else if (coloro is System.Collections.Generic.List<int> clist && clist.Count >= 3) color = new SKColor((byte)clist[0], (byte)clist[1], (byte)clist[2]);

					// soft glow behind particle
					int glowSize = (int)(size * 2.5);
					int glowAlpha = Math.Max(0, Math.Min(220, (int)(alpha * 0.6)));
					var glowCol = new SKColor(color.Red, color.Green, color.Blue, (byte)glowAlpha);
					DrawFilledCircle(canvas, (int)(curX), (int)(curY), glowSize, glowCol);

					// bright core (white) with fade
					var coreCol = new SKColor(255,255,255, (byte)alpha);
					DrawFilledCircle(canvas, (int)(curX), (int)(curY), size, coreCol);

					remaining.Add(p);
				}

			}

			Particles.Clear();
			foreach (var r in remaining) Particles.Add(r);
		}
		private void DrawDeathAnimations(DateTime now, SKCanvas canvas)
		{
			var remaining = new List<object>();
			double nowSec = now.Subtract(DateTime.UnixEpoch).TotalSeconds;

			foreach (var item in DeathAnimations)
			{
				object[] arr = item as object[];
				if (arr == null || arr.Length < 7) continue;
				int cx = Convert.ToInt32(arr[0]);
				int cy = Convert.ToInt32(arr[1]);
				double start = Convert.ToDouble(arr[2]);
				double duration = Convert.ToDouble(arr[3]);
				object colorObj = arr[4];
				int radius = Convert.ToInt32(arr[5]);
				string symbol = arr[6]?.ToString() ?? "?";

				double elapsed = nowSec - start;
				if (elapsed <= duration)
				{
					double t = elapsed / Math.Max(1e-6, duration);
					int alpha = Math.Max(0, Math.Min(255, (int)(255 * (1.0 - t))));

					// determine color
					SKColor teamColor = new SKColor(220, 220, 220);
					if (colorObj is Core.ColorRgb cr) teamColor = ToSKColor(cr);
					else if (colorObj is int[] iarr && iarr.Length >= 3) teamColor = new SKColor((byte)iarr[0], (byte)iarr[1], (byte)iarr[2], (byte)alpha);
					else if (colorObj is System.Collections.Generic.List<int> il && il.Count >= 3) teamColor = new SKColor((byte)il[0], (byte)il[1], (byte)il[2], (byte)alpha);
					else if (colorObj is SKColor skc) teamColor = new SKColor(skc.Red, skc.Green, skc.Blue, (byte)alpha);
					else teamColor = new SKColor(teamColor.Red, teamColor.Green, teamColor.Blue, (byte)alpha);

					// draw main fading circle with alpha
					DrawFilledCircle(canvas, cx, cy, radius, teamColor);

					// draw fading symbol text
					var txtCol = new SKColor(255,255,255, (byte)alpha);
					DrawText(canvas, symbol, Math.Max(4, FontSize), txtCol, cx, cy);

					remaining.Add(item);
				}
			}

			DeathAnimations.Clear();
			foreach (var r in remaining) DeathAnimations.Add(r);
		}
		private void DrawDamageCharts(CombatEngine? engine, SKCanvas canvas)
		{
			if (engine == null || DamageDone == null || DamageDone.Count == 0) return;

			int spacing = 12;
			int totalW = RightPanelWidth;
			int eachW = Math.Max(180, (totalW - spacing) / 2);
			int chartX = WindowWidth - totalW - Margin;
			int chartY = Margin;
			int chartH = WindowHeight - 2 * Margin;

			// background panel
			DrawRect(canvas, chartX, chartY, totalW, chartH, new SKColor(40, 40, 40));

			// title (left aligned similar to WinForms visualizer)
			DrawTextLeft(canvas, "Damage Meter", Math.Max(12, TitleFontSize), new SKColor(230,230,230), chartX + 12, chartY + 6 + TitleFontSize/2);

			// bucket units by team using persistent UnitInfo so dead units remain visible
			var unitsByTeam = new Dictionary<Team, List<(string symbol, Team team, double dmg)>>()
			{
				{Team.TEAM_1, new List<(string symbol, Team team, double dmg)>()},
				{Team.TEAM_2, new List<(string symbol, Team team, double dmg)>()}
			};

			foreach (var kv in DamageDone)
			{
				int uid = kv.Key;
				double dmg = kv.Value;
				if (!UnitInfo.TryGetValue(uid, out var info)) continue;
				string sym = info.symbol;
				Team team = info.team;
				if (!unitsByTeam.ContainsKey(team)) unitsByTeam[team] = new List<(string symbol, Team team, double dmg)>();
				unitsByTeam[team].Add((sym, team, dmg));
			}

			int padLeft = 8;
			int padTop = TitleFontSize;
			int maxShow = 8;

			foreach (Team team in new Team[] { Team.TEAM_1, Team.TEAM_2 })
			{
				int idx = team == Team.TEAM_1 ? 0 : 1;
				int regionX = chartX + idx * (eachW + spacing) + padLeft;
				int regionW = eachW - padLeft - 6;
				int titleY = chartY + padTop;

				// collect and sort top entries
				var entries = unitsByTeam.ContainsKey(team) ? unitsByTeam[team] : new List<(string symbol, Team team, double dmg)>();
				entries.Sort((a,b) => b.Item3.CompareTo(a.Item3));
				if (entries.Count > maxShow) entries = entries.GetRange(0, maxShow);

				if (entries.Count == 0)
				{
					DrawText(canvas, "No damage", DamageFontSize, new SKColor(160,160,160), regionX + 6 + regionW/2, titleY + DamageFontSize);
					continue;
				}

				// compute per-region max
				double maxDamage = 1.0;
				foreach (var e in entries) if (e.Item3 > maxDamage) maxDamage = e.Item3;

				int fontH = DamageFontSize;
				int barHFixed = Math.Max(Math.Max(fontH + 4, (int)(CellRadius * 0.35)), 12);
				int gap = 6;
				int rowH = barHFixed + gap;

				for (int i = 0; i < entries.Count; i++)
				{
					var info = entries[i];
					int y = titleY + DamageFontSize + 6 + i * rowH;
					int bx = regionX;
					int by = y + 2;
					int bw = regionW;
					int bh = barHFixed;

					// background bar
					DrawRect(canvas, bx, by, bw, bh, new SKColor(30,30,30));

					int fillW = maxDamage > 0 ? (int)(bw * (info.Item3 / maxDamage)) : 0;
					var color = TeamColors.ContainsKey(info.Item2) ? TeamColors[info.Item2] : new SKColor(200,200,60);
					DrawRect(canvas, bx, by, fillW, bh, color);

					string label = $"{info.Item1} { (int)info.Item3 }";
					// draw label left inside bar
					DrawTextLeft(canvas, label, DamageFontSize, new SKColor(255,255,255), bx + 4, by + 4);
				}
			}
		}
		private void DrawFloatingTexts(DateTime now, SKCanvas canvas)
		{
			var remaining = new List<object>();
			double nowSec = now.Subtract(DateTime.UnixEpoch).TotalSeconds;

			foreach (var item in FloatingTexts)
			{
				// item: [x0, y0, txt, start, duration, vx, vy, color]
				if (!(item is object[] arr) || arr.Length < 8) continue;
				double x0 = Convert.ToDouble(arr[0]);
				double y0 = Convert.ToDouble(arr[1]);
				string txt = Convert.ToString(arr[2]) ?? "";
				double start = Convert.ToDouble(arr[3]);
				double duration = Convert.ToDouble(arr[4]);
				double vx = Convert.ToDouble(arr[5]);
				double vy = Convert.ToDouble(arr[6]);
				object colorObj = arr[7];

				double elapsed = nowSec - start;
				if (elapsed <= duration)
				{
					// Parabolic motion: x = x0 + vx*t ; y = y0 + vy*t + 0.5*g*t^2
					double g = 300.0;
					double curX = x0 + vx * elapsed;
					double curY = y0 + vy * elapsed + 0.5 * g * (elapsed * elapsed);
					int alpha = Math.Max(0, Math.Min(255, (int)(255 * (1.0 - elapsed / duration))));

					// outline paint (white) and fill paint (text color)
					SKColor fillColor = new SKColor(255, 140, 0);
					if (colorObj is int[] iarr && iarr.Length >= 3) fillColor = new SKColor((byte)iarr[0], (byte)iarr[1], (byte)iarr[2]);
					else if (colorObj is System.Collections.Generic.List<int> il && il.Count >= 3) fillColor = new SKColor((byte)il[0], (byte)il[1], (byte)il[2]);
					else if (colorObj is Core.ColorRgb cr) fillColor = ToSKColor(cr);

					var outlinePaint = new SKPaint { Color = new SKColor(255,255,255, (byte)alpha), IsAntialias = UseAntialiasing, TextSize = DamageFontSize };
					var fillPaint = new SKPaint { Color = new SKColor(fillColor.Red, fillColor.Green, fillColor.Blue, (byte)alpha), IsAntialias = UseAntialiasing, TextSize = DamageFontSize };

					// measure to center text
					var bounds = new SKRect();
					fillPaint.MeasureText(txt, ref bounds);

					// draw outline by drawing white text at offsets
					var off = new (int, int)[] { (-1,0), (1,0), (0,-1), (0,1) };
					foreach (var (ox, oy) in off)
					{
						canvas.DrawText(txt, (float)(curX - bounds.MidX + ox), (float)(curY - bounds.MidY + oy), outlinePaint);
					}

					// draw fill
					canvas.DrawText(txt, (float)(curX - bounds.MidX), (float)(curY - bounds.MidY), fillPaint);

					remaining.Add(item);
				}
			}

			FloatingTexts.Clear();
			foreach (var r in remaining) FloatingTexts.Add(r);
		}

		private void DrawHoverTooltip(SKCanvas canvas, int mouseX, int mouseY)
		{
			// Hit-test mouse pixel against board cells first, then bench cells
			(int, int)? pos = null;
			foreach (var kv in Board.Cells)
			{
				var cellPos = kv.Key;
				var cellPix = CellPixel(cellPos);
				int centerX = cellPix.Item1 + LeftOffset + Margin;
				int centerY = cellPix.Item2 + TopMargin + CellRadius + 4;
				var corners = Board.GetCellCorners((centerX, centerY), CellRadius);
				if (PointInPolygon(mouseX, mouseY, corners)) { pos = cellPos; break; }
			}
			if (!pos.HasValue)
			{
				for (int col = 0; col < Board.BenchSize; col++)
				{
					var topCenter = GetBenchCellCenter((-1, col));
					if (topCenter.HasValue)
					{
						var corners = GetBenchCellCorners(topCenter.Value, CellRadius);
						if (PointInPolygon(mouseX, mouseY, corners)) { pos = (-1, col); break; }
					}
					var botCenter = GetBenchCellCenter((-2, col));
					if (botCenter.HasValue)
					{
						var corners = GetBenchCellCorners(botCenter.Value, CellRadius);
						if (PointInPolygon(mouseX, mouseY, corners)) { pos = (-2, col); break; }
					}
				}
			}
			if (!pos.HasValue) return;
			Unit? unit = null;
			// bench support: pos.Item1 == -1 -> team1 top bench, -2 -> team2 bottom bench
			if (pos.Value.Item1 < 0)
			{
				Team team = pos.Value.Item1 == -1 ? Team.TEAM_1 : Team.TEAM_2;
				unit = Board.GetBenchUnit(team, pos.Value.Item2);
				if (unit == null) return;
			}
			else
			{
				var cell = Board.Cells[pos.Value];
				if (cell == null || cell.Unit == null) return;
				unit = cell.Unit;
			}

			// Title / basic stats
			string title = unit.UnitType.ToString();
			int curHp = (int)Math.Max(0, unit.CurrentHealth);
			int maxHp = (int)unit.GetMaxHealth();
			int curMana = (int)Math.Max(0, unit.CurrentMana);
			int maxMana = (int)(unit.BaseStats?.MaxMana ?? 0);

			// Spell info
			var spell = unit.BaseStats?.Spell;
			string spellName = spell != null ? spell.Name : "No Spell";
			string desc = spell != null ? spell.Description() ?? "No description available." : "No description available.";

			// Wrap description into lines using SKPaint measurements
			int pad = Math.Max(6, CellRadius / 2);
			int maxWidth = Math.Min(TooltipFontSize * 24, CellRadius * 6);
			var paintMeasure = new SKPaint { TextSize = TooltipFontSize, IsAntialias = UseAntialiasing };
			var words = desc.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			var descLines = new List<string>();
			string cur = "";
			foreach (var w in words)
			{
				var test = string.IsNullOrEmpty(cur) ? w : cur + " " + w;
				var rect = new SKRect();
				paintMeasure.MeasureText(test, ref rect);
				if (rect.Width > maxWidth && cur.Length > 0)
				{
					descLines.Add(cur);
					cur = w;
				}
				else cur = test;
			}
			if (!string.IsNullOrEmpty(cur)) descLines.Add(cur);

			// Stats block (ATK, AS, SP, RNG, DEF, RES, CR, CD) split into 4 columns
			var bs = unit.BaseStats;
			var stats = new List<(string, string)>
			{
				("ATK", ((int)unit.GetAttack()).ToString()),
				("AS", unit.GetAttackSpeed().ToString("0.00")),
				("SP", ((int)(bs?.SpellPower ?? 0)).ToString()),
				("RNG", ((int)(bs?.Range ?? 1)).ToString()),
				("DEF", ((int)unit.GetDefense()).ToString()),
				("RES", ((int)unit.GetResistance()).ToString()),
				("CR", ((bs?.CritRate ?? 0.0) * 100.0).ToString("0") + "%"),
				("CD", (bs?.CritDmg ?? 1.0).ToString("0.0") + "x")
			};
			int n = stats.Count;
			int perCol = (n + 3) / 4;
			var left = stats.GetRange(0, Math.Min(perCol, n));
			var midleft = n > perCol ? stats.GetRange(perCol, Math.Min(perCol, n - perCol)) : new List<(string,string)>();
			var midright = n > perCol*2 ? stats.GetRange(perCol*2, Math.Min(perCol, n - perCol*2)) : new List<(string,string)>();
			var right = n > perCol*3 ? stats.GetRange(perCol*3, Math.Min(perCol, n - perCol*3)) : new List<(string,string)>();

			// Measure text widths for layout
			var titleRect = new SKRect();
			using (var pt = new SKPaint { TextSize = Math.Max(10, TooltipFontSize + 2), IsAntialias = UseAntialiasing }) pt.MeasureText(title, ref titleRect);
			using var spellPaint = new SKPaint { TextSize = TooltipFontSize, IsAntialias = UseAntialiasing };
			var spellRect = new SKRect();
			spellPaint.MeasureText(spellName, ref spellRect);
			var hpRect = new SKRect();
			spellPaint.MeasureText($"HP: {curHp}/{maxHp}", ref hpRect);
			var manaRect = new SKRect();
			spellPaint.MeasureText($"Mana: {curMana}/{maxMana}", ref manaRect);

			// measure desc lines
			int descMaxW = 0;
			foreach (var l in descLines)
			{
				var r = new SKRect();
				paintMeasure.MeasureText(l, ref r);
				descMaxW = Math.Max(descMaxW, (int)r.Width);
			}

			// measure stat column widths
			int leftW = 0, midleftW = 0, midrightW = 0, rightW = 0;
			SKRect tmp = new SKRect();
			foreach (var s in left) spellPaint.MeasureText($"{s.Item1}: {s.Item2}", ref tmp); leftW = Math.Max(leftW, (int)tmp.Width);
			foreach (var s in midleft) spellPaint.MeasureText($"{s.Item1}: {s.Item2}", ref tmp); midleftW = Math.Max(midleftW, (int)tmp.Width);
			foreach (var s in midright) spellPaint.MeasureText($"{s.Item1}: {s.Item2}", ref tmp); midrightW = Math.Max(midrightW, (int)tmp.Width);
			foreach (var s in right) spellPaint.MeasureText($"{s.Item1}: {s.Item2}", ref tmp); rightW = Math.Max(rightW, (int)tmp.Width);
			int gapCols = 10;
			int statsBlockW = leftW + (midleftW > 0 ? gapCols + midleftW : 0) + (midrightW > 0 ? gapCols + midrightW : 0) + (rightW > 0 ? gapCols + rightW : 0);

			int contentW = Math.Max((int)titleRect.Width, Math.Max(descMaxW, Math.Max((int)spellRect.Width, Math.Max((int)hpRect.Width + Math.Min(200, CellRadius * 4) + 8, (int)manaRect.Width + Math.Min(200, CellRadius * 4) + 8))));
			contentW = Math.Max(contentW, statsBlockW);
			int boxW = contentW + pad * 2;

			// compute heights
			int spacing = (int)(CellRadius * 0.18);
			int bar_h = Math.Max(6, (int)(CellRadius * 0.18));
			int titleH = (int)titleRect.Height;
			int statsH = Math.Max(1, (left.Count > 0 ? left.Count : 0)) * (TooltipFontSize + 2);
			int descH = descLines.Count * (TooltipFontSize + 4);
			int boxH = pad + titleH + spacing + Math.Max(bar_h, (int)hpRect.Height) + spacing + Math.Max(bar_h, (int)manaRect.Height) + spacing + statsH + spacing + (int)spellRect.Height + spacing + descH + pad;

			// position near unit center
			var pix = pos.Value.Item1 < 0 ? GetBenchCellCenter((pos.Value.Item1, pos.Value.Item2)) ?? (LeftOffset + Margin, TopMargin + CellRadius) : CellPixel((pos.Value.Item1, pos.Value.Item2));
			int cx = pix.Item1 + LeftOffset + Margin;
			int cy = pix.Item2 + TopMargin + CellRadius + 4;
			int bx = cx + 16;
			int by = cy + 16;
			if (bx + boxW > WindowWidth - 4) bx = cx - boxW - 16;
			if (by + boxH > WindowHeight - 4) by = cy - boxH - 16;

			// draw background rounded rect + border
			using (var pb = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(20, 20, 20, 220), IsAntialias = UseAntialiasing }) canvas.DrawRoundRect(new SKRect(bx, by, bx + boxW, by + boxH), 6, 6, pb);
			using (var pborder = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(120, 120, 120, 200), StrokeWidth = 1, IsAntialias = UseAntialiasing }) canvas.DrawRoundRect(new SKRect(bx, by, bx + boxW, by + boxH), 6, 6, pborder);

			// draw title
			DrawText(canvas, title, Math.Max(10, TooltipFontSize + 2), new SKColor(250, 250, 210), bx + pad + contentW / 2, by + pad + titleH / 2);

			int yoff = by + pad + titleH + 2 * spacing;
			// HP bar + numeric
			int bar_x = bx + pad;
			int bar_y = yoff;
			int bar_w = Math.Min(200, CellRadius * 4);
			// background
			DrawRect(canvas, bar_x, bar_y, bar_w, bar_h, new SKColor(40, 40, 40));
			double hpRatio = Math.Max(0.0, Math.Min(1.0, unit.CurrentHealth / Math.Max(1.0, unit.GetMaxHealth())));
			DrawRect(canvas, bar_x, bar_y, (int)(bar_w * hpRatio), bar_h, new SKColor(50, 200, 100));
			// numeric right of bar
			DrawTextLeft(canvas, $"HP: {curHp}/{maxHp}", TooltipFontSize, new SKColor(230, 230, 230), bar_x + bar_w + 8, bar_y - (bar_h / 2) );
			yoff += Math.Max(bar_h, (int)hpRect.Height) + spacing;

			// Mana bar
			bar_x = bx + pad;
			bar_y = yoff;
			DrawRect(canvas, bar_x, bar_y, bar_w, bar_h, new SKColor(40, 40, 40));
			double manaRatio = 0.0;
			if (maxMana > 0) manaRatio = Math.Max(0.0, Math.Min(1.0, unit.CurrentMana / (double)maxMana));
			DrawRect(canvas, bar_x, bar_y, (int)(bar_w * manaRatio), bar_h, new SKColor(80, 140, 220));
			DrawTextLeft(canvas, $"Mana: {curMana}/{maxMana}", TooltipFontSize, new SKColor(230, 230, 230), bar_x + bar_w + 8, bar_y - (bar_h / 2));
			yoff += Math.Max(bar_h, (int)manaRect.Height) + spacing;

			// Draw stats columns
			int col_x = bx + pad;
			int col_y = yoff;
			// left
			for (int i = 0; i < left.Count; i++) DrawTextLeft(canvas, $"{left[i].Item1}: {left[i].Item2}", TooltipFontSize, new SKColor(220,220,220), col_x, col_y + i * (TooltipFontSize + 2));
			int cur_x = col_x + leftW + gapCols;
			if (midleft.Count > 0)
			{
				for (int i = 0; i < midleft.Count; i++) DrawTextLeft(canvas, $"{midleft[i].Item1}: {midleft[i].Item2}", TooltipFontSize, new SKColor(220,220,220), cur_x, col_y + i * (TooltipFontSize + 2));
				cur_x += midleftW + gapCols;
			}
			if (midright.Count > 0)
			{
				for (int i = 0; i < midright.Count; i++) DrawTextLeft(canvas, $"{midright[i].Item1}: {midright[i].Item2}", TooltipFontSize, new SKColor(220,220,220), cur_x, col_y + i * (TooltipFontSize + 2));
				cur_x += midrightW + gapCols;
			}
			if (right.Count > 0)
			{
				for (int i = 0; i < right.Count; i++) DrawTextLeft(canvas, $"{right[i].Item1}: {right[i].Item2}", TooltipFontSize, new SKColor(220,220,220), cur_x, col_y + i * (TooltipFontSize + 2));
			}
			// advance past stats
			yoff += statsH + spacing;

			// Spell name
			DrawTextLeft(canvas, spellName, TooltipFontSize, new SKColor(200,200,255), bx + pad, yoff);
			yoff += (int)spellRect.Height + spacing;

			// description lines
			foreach (var l in descLines)
			{
				DrawTextLeft(canvas, l, TooltipFontSize, new SKColor(230,230,230), bx + pad, yoff);
				yoff += TooltipFontSize + 4;
			}
		}

		// UI button helpers (pause/start and spawn row)
		public SKRect GetPauseButtonRect()
		{
			int btnW = Math.Max(80, (int)(CellRadius * 2.5));
			int btnH = Math.Max(44, (int)(CellRadius * 1.5));
			int bx = WindowWidth - btnW - Margin;
			int by = WindowHeight - btnH - Margin;
			return new SKRect(bx, by, bx + btnW, by + btnH);
		}

		public void DrawPauseButton(SKCanvas canvas, bool paused)
		{
			var r = GetPauseButtonRect();
			// background
			var varBg = paused ? new SKColor(70,70,70) : new SKColor(40,120,40);
			using (var p = new SKPaint { Style = SKPaintStyle.Fill, Color = varBg, IsAntialias = UseAntialiasing })
			{
				canvas.DrawRoundRect(r, 6, 6, p);
			}
			// border
			using (var p2 = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(160,160,160), StrokeWidth = 1, IsAntialias = UseAntialiasing })
			{
				canvas.DrawRoundRect(r, 6, 6, p2);
			}
			// label centered
			string label = paused ? "Start" : "Pause";
			DrawText(canvas, label, Math.Max(10, TooltipFontSize), new SKColor(255,255,255), r.MidX, r.MidY);
		}

		// Speed control UI (two buttons stacked above the pause button)
		public SKRect GetSpeedUpRect()
		{
			var pause = GetPauseButtonRect();
			int spacing = 8;
			int btnW = (int)(pause.Width);
			int btnH = Math.Max(28, (int)(CellRadius * 1.1));
			int bx = (int)pause.Left;
			int by = (int)(pause.Top - (btnH + spacing) * 2);
			return new SKRect(bx, by, bx + btnW, by + btnH);
		}

		public SKRect GetSpeedDownRect()
		{
			var pause = GetPauseButtonRect();
			int spacing = 8;
			int btnW = (int)(pause.Width);
			int btnH = Math.Max(28, (int)(CellRadius * 1.1));
			int bx = (int)pause.Left;
			int by = (int)(pause.Top - (btnH + spacing) * 1);
			return new SKRect(bx, by, bx + btnW, by + btnH);
		}

		public void DrawSpeedButtons(SKCanvas canvas, double engineFps)
		{
			var up = GetSpeedUpRect();
			var dn = GetSpeedDownRect();
			// up button
			using (var p = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(70, 70, 100), IsAntialias = UseAntialiasing }) canvas.DrawRoundRect(up, 6, 6, p);
			using (var p2 = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(140, 140, 140), StrokeWidth = 1, IsAntialias = UseAntialiasing }) canvas.DrawRoundRect(up, 6, 6, p2);
			DrawText(canvas, "X2 SPEED", Math.Max(10, TooltipFontSize - 1), new SKColor(255,255,255), up.MidX, up.MidY);

			// down button
			using (var p3 = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(70, 70, 100), IsAntialias = UseAntialiasing }) canvas.DrawRoundRect(dn, 6, 6, p3);
			using (var p4 = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(140, 140, 140), StrokeWidth = 1, IsAntialias = UseAntialiasing }) canvas.DrawRoundRect(dn, 6, 6, p4);
			DrawText(canvas, "X0.5 SPEED", Math.Max(10, TooltipFontSize - 1), new SKColor(255,255,255), dn.MidX, dn.MidY);

			// engine fps text above up button
			string fpsText = $"Engine FPS: {engineFps:0.##}";
			DrawText(canvas, fpsText, Math.Max(10, TooltipFontSize - 1), new SKColor(240, 240, 160), up.MidX, up.Top - Math.Max(6, TooltipFontSize));
		}

		public SKRect GetSpawnButtonRect(int index, int total = 4)
		{
			int btnW = (int)(CellRadius * 2.5);
			int btnH = (int)(CellRadius * 1.5);
			var pause = GetPauseButtonRect();
			int spacing = 8;
			int totalW = total * btnW + (total - 1) * spacing;
			int startX;
			if (SpawnStartX.HasValue) startX = SpawnStartX.Value;
			else
			{
				int gridPixelW = (int)(CellRadius * Math.Sqrt(3) * (Board.Width + 0.5));
				int boardLeft = LeftOffset + Margin;
				startX = boardLeft + (gridPixelW - totalW) / 2;
				startX = Math.Max(Margin, startX);
				startX = Math.Min(startX, WindowWidth - totalW - Margin);
			}
			int bx = startX + index * (btnW + spacing);
			int by = (int)pause.Top;
			return new SKRect(bx, by, bx + btnW, by + btnH);
		}

		/// <summary>
		/// Returns the rect for the Buy XP button, positioned to the left of the shop,
		/// stacked on top of the Reroll button.
		/// </summary>
		public SKRect GetBuyXpButtonRect(int shopTotal = 5)
		{
			var firstShop = GetSpawnButtonRect(0, shopTotal);
			int btnW = (int)(CellRadius * 2.8);
			int btnH = (int)(firstShop.Height * 0.48f);
			int gap = 4;
			int bx = (int)firstShop.Left - btnW - 12;
			int by = (int)firstShop.Top;
			return new SKRect(bx, by, bx + btnW, by + btnH);
		}

		/// <summary>
		/// Returns the rect for the Reroll button, positioned below Buy XP.
		/// </summary>
		public SKRect GetRerollButtonRect(int shopTotal = 5)
		{
			var buyXp = GetBuyXpButtonRect(shopTotal);
			int btnH = (int)buyXp.Height;
			int gap = (int)(CellRadius * 0.08f) + 4;
			float by = buyXp.Bottom + gap;
			return new SKRect(buyXp.Left, by, buyXp.Right, by + btnH);
		}

		/// <summary>
		/// Draws the shop panel: gold label on top, Buy XP button, Reroll button,
		/// all stacked to the left of the shop spawn buttons.
		/// </summary>
		public void DrawShopPanel(SKCanvas canvas, int shopTotal, int? gold, Player? player, GameParams? gameParams, int mouseX, int mouseY)
		{
			var buyXpRect = GetBuyXpButtonRect(shopTotal);
			var rerollRect = GetRerollButtonRect(shopTotal);

			int level = player?.Level ?? 1;
			int xp = player?.Experience ?? 0;
			int xpToNext = player?.XpToNextLevel() ?? 0;
			int rerollCost = gameParams?.RerollCost ?? 2;
			int buyXpCost = gameParams?.BuyXpCost ?? 4;
			int buyXpAmount = gameParams?.BuyXpAmount ?? 4;

			bool canBuyXp = gold.HasValue && gold.Value >= buyXpCost && xpToNext > 0;
			bool canReroll = gold.HasValue && gold.Value >= rerollCost;

			// Gold text above the two buttons
			if (gold.HasValue)
			{
				string goldText = $"Gold: {gold.Value}";
				float goldX = (buyXpRect.Left + buyXpRect.Right) / 2f;
				float goldY = buyXpRect.Top - Math.Max(8, TooltipFontSize * 0.7f);
				DrawText(canvas, goldText, Math.Max(12, TooltipFontSize + 2), new SKColor(255, 215, 0), goldX, goldY);
			}

			// Level / XP text below Reroll button
			{
				string lvlText = xpToNext > 0 ? $"Lv {level} ({xp}/{xpToNext} XP)" : $"Lv {level} (MAX)";
				float lvlX = (buyXpRect.Left + buyXpRect.Right) / 2f;
				float lvlY = rerollRect.Bottom + Math.Max(8, TooltipFontSize * 0.7f);
				DrawText(canvas, lvlText, Math.Max(10, TooltipFontSize - 1), new SKColor(180, 220, 255), lvlX, lvlY);
			}

			// Buy XP button
			{
				bool hovered = mouseX >= buyXpRect.Left && mouseX <= buyXpRect.Right && mouseY >= buyXpRect.Top && mouseY <= buyXpRect.Bottom;
				var bg = !canBuyXp ? new SKColor(60, 60, 60) : hovered ? new SKColor(50, 100, 160) : new SKColor(40, 80, 130);
				using (var p = new SKPaint { Style = SKPaintStyle.Fill, Color = bg, IsAntialias = UseAntialiasing }) canvas.DrawRoundRect(buyXpRect, 6, 6, p);
				using (var p2 = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(140, 140, 140), StrokeWidth = 1, IsAntialias = UseAntialiasing }) canvas.DrawRoundRect(buyXpRect, 6, 6, p2);
				string label = $"Buy XP ({buyXpCost}g)";
				var col = canBuyXp ? new SKColor(255, 255, 255) : new SKColor(140, 140, 140);
				DrawText(canvas, label, Math.Max(10, TooltipFontSize - 1), col, buyXpRect.MidX, buyXpRect.MidY);
			}

			// Reroll button
			{
				bool hovered = mouseX >= rerollRect.Left && mouseX <= rerollRect.Right && mouseY >= rerollRect.Top && mouseY <= rerollRect.Bottom;
				var bg = !canReroll ? new SKColor(60, 60, 60) : hovered ? new SKColor(130, 100, 50) : new SKColor(110, 85, 40);
				using (var p = new SKPaint { Style = SKPaintStyle.Fill, Color = bg, IsAntialias = UseAntialiasing }) canvas.DrawRoundRect(rerollRect, 6, 6, p);
				using (var p2 = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(140, 140, 140), StrokeWidth = 1, IsAntialias = UseAntialiasing }) canvas.DrawRoundRect(rerollRect, 6, 6, p2);
				string label = $"Reroll ({rerollCost}g)";
				var col = canReroll ? new SKColor(255, 255, 255) : new SKColor(140, 140, 140);
				DrawText(canvas, label, Math.Max(10, TooltipFontSize - 1), col, rerollRect.MidX, rerollRect.MidY);
			}
		}

		public void DrawSpawnButtons(SKCanvas canvas, System.Collections.Generic.List<(MicroAutoChess.Core.UnitType ut, int? cost)> specs, int? budget = null, int mouseX = 0, int mouseY = 0)
		{
			int total = specs?.Count ?? 0;
			if (total == 0) return;

			bool hoverDrawn = false;
			for (int i = 0; i < total; i++)
			{
				var rect = GetSpawnButtonRect(i, total);
				int w = (int)rect.Width; int h = (int)rect.Height;
				var (ut, cost) = specs[i];
				bool empty = !cost.HasValue;
				bool disabled = !empty && budget.HasValue && cost.Value > budget.Value;
				var bg = empty ? new SKColor(45,45,45) : disabled ? new SKColor(100,100,100) : new SKColor(60,60,60);
				using (var p = new SKPaint { Style = SKPaintStyle.Fill, Color = bg, IsAntialias = UseAntialiasing }) canvas.DrawRoundRect(rect, 6, 6, p);
				using (var p2 = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(140,140,140), StrokeWidth = 1, IsAntialias = UseAntialiasing }) canvas.DrawRoundRect(rect, 6, 6, p2);
				if (!empty)
				{
					string label = $"{ut} ({cost.Value})";
					DrawText(canvas, label, TooltipFontSize, disabled ? new SKColor(180,180,180) : new SKColor(255,255,255), rect.MidX, rect.MidY);
				}

				// hover tooltip: if mouse inside rect and not yet drawn
				if (!empty && !hoverDrawn && mouseX >= rect.Left && mouseX <= rect.Right && mouseY >= rect.Top && mouseY <= rect.Bottom)
				{
					hoverDrawn = true;
					// Build full tooltip (title + cost + spell description) by instantiating a temp Unit
					string title = ut.ToString();
					string costText = cost.HasValue ? $"Cost: {cost.Value}" : "";
					// instantiate temp unit to access base stats / spell description
					string desc = "No spell.";

					var tmp = new Unit(ut, UnitRarity.COMMON, Team.TEAM_2);
					var sp = tmp.BaseStats?.Spell;
					if (sp != null)
					{
						desc = sp.Description() ?? "No description available.";
					}


					int pad = Math.Max(6, CellRadius / 3);
					var titleSize = new SKRect();
					var costSize = new SKRect();
					using (var pt = new SKPaint { Color = new SKColor(250,250,210), IsAntialias = UseAntialiasing, TextSize = TooltipFontSize }) pt.MeasureText(title, ref titleSize);
					using (var pc = new SKPaint { Color = new SKColor(200,200,200), IsAntialias = UseAntialiasing, TextSize = TooltipFontSize }) pc.MeasureText(costText, ref costSize);
					// wrap description lines
					int maxWidth = Math.Min(TooltipFontSize * 24, CellRadius * 6);
					var measure = new SKPaint { TextSize = TooltipFontSize, IsAntialias = UseAntialiasing };
					var words = desc.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
					var descLines = new List<string>();
					string curLine = "";
					foreach (var word in words)
					{
						var test = string.IsNullOrEmpty(curLine) ? word : curLine + " " + word;
						var r = new SKRect();
						measure.MeasureText(test, ref r);
						if (r.Width > maxWidth && curLine.Length > 0)
						{
							descLines.Add(curLine);
							curLine = word;
						}
						else curLine = test;
					}
					if (!string.IsNullOrEmpty(curLine)) descLines.Add(curLine);

					int descMaxW = 0;
					var tmpRect = new SKRect();
					foreach (var l in descLines) { measure.MeasureText(l, ref tmpRect); descMaxW = Math.Max(descMaxW, (int)tmpRect.Width); }

					int contentW = Math.Max((int)titleSize.Width, Math.Max((int)costSize.Width, descMaxW));
					int boxW = contentW + pad * 2;
					int boxH = 3 * pad + (int)titleSize.Height + (int)costSize.Height + descLines.Count * (TooltipFontSize + 4) + pad;
					int bx = mouseX + 12;
					int by = mouseY + 12;
					if (bx + boxW > WindowWidth - 4) bx = mouseX - boxW - 12;
					if (by + boxH > WindowHeight - 4) by = mouseY - boxH - 12;

					var bmp = new SKBitmap(boxW, boxH, SKColorType.Rgba8888, SKAlphaType.Premul);
					using (var surf = new SKCanvas(bmp))
					{
							using (var pb = new SKPaint { Style = SKPaintStyle.Fill, Color = new SKColor(30,30,30,220), IsAntialias = UseAntialiasing }) surf.DrawRoundRect(new SKRect(0, 0, boxW, boxH), 6, 6, pb);
							using (var pborder = new SKPaint { Style = SKPaintStyle.Stroke, Color = new SKColor(120,120,120), StrokeWidth = 1, IsAntialias = UseAntialiasing }) surf.DrawRoundRect(new SKRect(0, 0, boxW, boxH), 6, 6, pborder);
						DrawTextLeft(surf, title, TooltipFontSize, new SKColor(250,250,210), pad, pad + (int)titleSize.Height / 2);
						DrawTextLeft(surf, costText, TooltipFontSize, new SKColor(200,200,200), pad, 2*pad + (int)titleSize.Height + (int)costSize.Height / 2);
						int yoff = 3*pad + (int)titleSize.Height + (int)costSize.Height;
						foreach (var l in descLines)
						{
							DrawTextLeft(surf, l, TooltipFontSize, new SKColor(230,230,230), pad, yoff);
							yoff += TooltipFontSize + 4;
						}
					}
					_deferredTooltips.Add((bmp, bx, by));
				}
			}
		}

		private void DrawBenchCells(SKCanvas canvas)
		{
			// Precompute column center X positions (align bench cells to columns)
			var colCenters = new List<int>();
			for (int i = 0; i < Board.BenchSize; i++) colCenters.Add((int)(i * CellRadius * 1.5 + LeftOffset + Margin));

			// Compute top/bottom bench Y positions based on first/last row centers
			var first = Board.CoordToPixel((0, 0), CellRadius);
			int first_center_y = (int)(first.Item2 + TopMargin + CellRadius + 4);
			var last = Board.CoordToPixel((0, Board.Height - 1), CellRadius);
			int last_center_y = (int)(last.Item2 + TopMargin + CellRadius + 4);

			int top_bench_y = first_center_y - BenchGap;
			int bottom_bench_y = last_center_y + BenchGap;

			// var bench1 = Board.Players[Team.TEAM_1].Bench ?? new System.Collections.Generic.Dictionary<int, Unit?>();
			// var bench2 = Board.Players[Team.TEAM_2].Bench ?? new System.Collections.Generic.Dictionary<int, Unit?>();

			void _draw_bench_cell(int cx, int cy, Unit? unit, SKColor color)
			{
				var corners = GetBenchCellCorners((cx, cy), CellRadius);
				DrawPolygon(canvas, corners, color, 2);
				if (unit != null && unit != _draggedUnit)
				{
					DrawUnitWithLevelBorder(cx, cy, unit, null, canvas);
					var symbol = unit?.GetSymbol() ?? "?";
					DrawText(canvas, symbol, FontSize, new SKColor(255,255,255), cx, cy);
				}
			}

			// Determine which team is at top vs bottom
			Team topTeam = ViewerTeam == Team.TEAM_1 ? Team.TEAM_2 : Team.TEAM_1;
			Team bottomTeam = ViewerTeam;
			int viewerBenchRow = -(int)ViewerTeam;

			for (int i = 0; i < Math.Min(colCenters.Count, Board.BenchSize); i++)
			{
				int displayI = ViewerTeam == Team.TEAM_1 ? (Board.BenchSize - 1 - i) : i;
				int cx = colCenters[displayI];
				Unit? unit = Board.Players[topTeam].Bench.ContainsKey(i) ? Board.Players[topTeam].Bench[i] : null;
				_draw_bench_cell(cx, top_bench_y, unit, GridColor);
			}
			for (int i = 0; i < Math.Min(colCenters.Count, Board.BenchSize); i++)
			{
				int displayI = ViewerTeam == Team.TEAM_1 ? (Board.BenchSize - 1 - i) : i;
				int cx = colCenters[displayI];
				Unit? unit = Board.Players[bottomTeam].Bench.ContainsKey(i) ? Board.Players[bottomTeam].Bench[i] : null;
				SKColor color = GridColor;
				if (HighlightHoveredCell.HasValue && HighlightHoveredCell.Value == (viewerBenchRow, i)) color = new SKColor(255,255,0);
				else if (HighlightPlayerInitialZone) color = new SKColor(255,255,255);
				_draw_bench_cell(cx, bottom_bench_y, unit, color);
			}
		}
		private List<(int,int)> GetBenchCellCorners((int, int) position, int cellRadius)
		{
			int x = position.Item1;
			int y = position.Item2;
			double r = cellRadius;
			var top_left = ((int)(x + r / Math.Sqrt(2)), (int)(y + r / Math.Sqrt(2)));
			var top_right = ((int)(x - r / Math.Sqrt(2)), (int)(y + r / Math.Sqrt(2)));
			var bottom_right = ((int)(x - r / Math.Sqrt(2)), (int)(y - r / Math.Sqrt(2)));
			var bottom_left = ((int)(x + r / Math.Sqrt(2)), (int)(y - r / Math.Sqrt(2)));
			return new List<(int,int)>{top_left, top_right, bottom_right, bottom_left};
		}

		private (int,int)? GetBenchCellCenter((int, int) benchIndex)
		{
			int row = benchIndex.Item1;
			int col = benchIndex.Item2;
			if (row != -1 && row != -2) return null;
			if (col < 0 || col >= Board.BenchSize) return null;
			int displayCol = ViewerTeam == Team.TEAM_1 ? (Board.BenchSize - 1 - col) : col;
			int centerX = (int)(displayCol * CellRadius * 1.5 + LeftOffset + Margin);
			// When mirrored, swap which bench is top vs bottom
			bool isTop = ViewerTeam == Team.TEAM_1 ? (row == -2) : (row == -1);
			int centerY;
			if (isTop) centerY = Margin + CellRadius + 4;
			else centerY = TopMargin + (int)(1.5 * CellRadius) + 4 + (int)(1.5 * CellRadius * Board.Height);
			return (centerX, centerY);
		}
		private void DrawUnitWithLevelBorder(int cx, int cy, Unit unit, int? radius, SKCanvas canvas)
		{
			int r = radius ?? (int)(CellRadius * 0.45);
			// filled circle with team color
			var teamCol = TeamColors.ContainsKey(unit.Team) ? TeamColors[unit.Team] : new SKColor(160, 160, 160);
			DrawFilledCircle(canvas, cx, cy, r, teamCol);
			// border based on level
			int level = Math.Max(1, unit.Level);
			var borderCol = LevelBorderColor(level);
			int borderThickness = Math.Max(2, (int)(CellRadius * 0.08));
			DrawCircleBorder(canvas, cx, cy, r, borderThickness, borderCol);
		}


		private SKColor LevelBorderColor(int level)
		{
			switch (level)
			{
				case 1: return new SKColor(205,127,50);
				case 2: return new SKColor(192,192,192);
				case 3: return new SKColor(212,175,55);
				default: return new SKColor(170,220,255);
			}
		}

		// Minimal SK drawing primitive wrappers used later
		private void DrawRect(SKCanvas canvas, int x, int y, int w, int h, SKColor color)
		{
			using var p = new SKPaint { Style = SKPaintStyle.Fill, Color = color };
			canvas.DrawRect(x, y, w, h, p);
		}

		private void DrawText(SKCanvas canvas, string text, int fontSize, SKColor color, float centerX, float centerY)
		{
			using var p = new SKPaint { Color = color, IsAntialias = UseAntialiasing, TextSize = fontSize };
			var bounds = new SKRect();
			p.MeasureText(text, ref bounds);
			canvas.DrawText(text, centerX - bounds.MidX, centerY - bounds.MidY, p);
		}

		private void DrawTextLeft(SKCanvas canvas, string text, int fontSize, SKColor color, float leftX, float topY)
		{
			using var p = new SKPaint { Color = color, IsAntialias = UseAntialiasing, TextSize = fontSize };
			var bounds = new SKRect();
			p.MeasureText(text, ref bounds);
			// Draw at top-left (leftX, topY)
			canvas.DrawText(text, leftX - bounds.Left, topY - bounds.Top, p);
		}

		private void DrawTextRight(SKCanvas canvas, string text, int fontSize, SKColor color, float rightX, float centerY)
		{
			using var p = new SKPaint { Color = color, IsAntialias = UseAntialiasing, TextSize = fontSize };
			var bounds = new SKRect();
			p.MeasureText(text, ref bounds);
			canvas.DrawText(text, rightX - bounds.Width - bounds.Left, centerY - bounds.MidY, p);
		}

		private void DrawPolygon(SKCanvas canvas, IEnumerable<(int, int)> corners, SKColor color, int thickness)
		{
			var pts = new List<SKPoint>();
			foreach (var (x, y) in corners) pts.Add(new SKPoint(x, y));
			if (pts.Count < 2) return;
			using var paint = new SKPaint { Style = SKPaintStyle.Stroke, Color = color, StrokeWidth = Math.Max(1, thickness), IsAntialias = UseAntialiasing, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
			using var path = new SKPath();
			path.MoveTo(pts[0]);
			for (int i = 1; i < pts.Count; i++) path.LineTo(pts[i]);
			path.Close();
			canvas.DrawPath(path, paint);
		}

		private void DrawFilledCircle(SKCanvas canvas, int cx, int cy, int radius, SKColor color)
		{
			using var p = new SKPaint { Style = SKPaintStyle.Fill, Color = color, IsAntialias = UseAntialiasing };
			canvas.DrawCircle(cx, cy, radius, p);
		}

		private void DrawCircleBorder(SKCanvas canvas, int cx, int cy, int radius, int thickness, SKColor color)
		{
			using var p = new SKPaint { Style = SKPaintStyle.Stroke, Color = color, StrokeWidth = Math.Max(1, thickness), IsAntialias = UseAntialiasing };
			canvas.DrawCircle(cx, cy, radius, p);
		}

		private void DrawTextRotated(SKCanvas canvas, string text, int fontSize, SKColor color, int centerX, int centerY, double angleDegrees)
		{
			canvas.Save();
			canvas.Translate(centerX, centerY);
			canvas.RotateDegrees((float)angleDegrees);
			using var p = new SKPaint { Color = color, IsAntialias = UseAntialiasing, TextSize = fontSize };
			var bounds = new SKRect();
			p.MeasureText(text, ref bounds);
			canvas.DrawText(text, -bounds.MidX, -bounds.MidY, p);
			canvas.Restore();
		}

		private bool PointInPolygon(int px, int py, System.Collections.Generic.List<(int, int)> corners)
		{
			if (corners == null || corners.Count < 3) return false;
			bool inside = false;
			int j = corners.Count - 1;
			for (int i = 0; i < corners.Count; j = i++)
			{
				int xi = corners[i].Item1, yi = corners[i].Item2;
				int xj = corners[j].Item1, yj = corners[j].Item2;
				bool intersect = ((yi > py) != (yj > py)) && (px < (xj - xi) * (py - yi) / (double)(yj - yi + 0.0) + xi);
				if (intersect) inside = !inside;
			}
			return inside;
		}

		private void FlushDeferredTooltips(SKCanvas canvas)
		{
			try
			{
				foreach (var t in _deferredTooltips)
				{
					canvas.DrawBitmap(t.bmp, t.x, t.y);
					t.bmp.Dispose();
				}
			}
			finally
			{
				_deferredTooltips.Clear();
			}
		}
	}
}

