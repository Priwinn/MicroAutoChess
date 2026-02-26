using System;
using System.Collections.Generic;
using System.Linq;

namespace MicroAutoChess.Core
{
    public enum CellType
    {
        EMPTY = 0,
        PLANNED = 1,
        UNIT = 2,
        OBSTACLE = 3
    }

    public class BoardCell
    {
        public (int, int) Position { get; set; }
        public Unit? Unit { get; set; }
        public CellType CellType { get; set; } = CellType.EMPTY;

        public BoardCell((int, int) pos)
        {
            Position = pos;
        }

        public bool IsEmpty() => Unit == null && CellType == CellType.EMPTY;
        public bool IsPlanned() => CellType == CellType.PLANNED;
        public bool IsOccupied() => CellType == CellType.UNIT;

        public void PlaceUnit(Unit unit)
        {
            if (!(IsEmpty() || IsPlanned()))
                throw new ArgumentException($"Cell {Position} is not empty or planned");
            Unit = unit;
            CellType = CellType.UNIT;
            unit.Position = Position;
        }

        public Unit RemoveUnit()
        {
            var u = Unit;
            Unit = null;
            CellType = CellType.EMPTY;
            if (u != null)
            {
                return u;
            }
            throw new ArgumentException($"Tried to remove unit from cell {Position}");
        }

        public void SetPlanned()
        {
            if (!IsEmpty())
                throw new ArgumentException($"Cell {Position} is not empty");
            CellType = CellType.PLANNED;
        }
    }

    public class Board
    {
        public (int, int) Size { get; set; }
        public int BenchSize { get; set; } = 9;
        public int Width { get; set; }
        public int Height { get; set; }
        public Dictionary<(int, int), BoardCell> Cells { get; set; } = new Dictionary<(int, int), BoardCell>();
        public double RangeOffset { get; set; } = 0.0;

        public Dictionary<Team, Player> Players { get; set; } = new Dictionary<Team, Player>();
        // Pathfinding occupied cache
        private byte[]? _occupiedCache = null;
        private bool _occupiedCacheDirty = true;

        public Board((int, int)? size = null)
        {
            Size = size ?? (7, 8);
            Width = Size.Item1;
            Height = Size.Item2;
            Players[Team.TEAM_1] = new Player(1);
            Players[Team.TEAM_2] = new Player(2);
            for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                Cells[(x, y)] = new BoardCell((x, y));
        }

        public bool IsValidPosition((int, int) position)
        {
            var (x, y) = position;
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        public bool IsValidInitialPosition((int, int) position, Team team)
        {
            if (!IsValidPosition(position)) return false;
            var (x, y) = position;
            if (team == Team.TEAM_1) return y < Height / 2;
            if (team == Team.TEAM_2) return y >= Height / 2;
            return false;
        }

        private void InvalidateOccupiedCache() => _occupiedCacheDirty = true;

        private void RebuildOccupiedCache()
        {
            _occupiedCache = new byte[Width * Height];
            foreach (var kv in Cells)
            {
                var (x, y) = kv.Key;
                if (kv.Value.Unit != null) _occupiedCache[y * Width + x] = 1;
            }
            _occupiedCacheDirty = false;
        }

        public bool PlaceBoardUnit(Unit unit, (int, int) position)
        {
            if (!Cells.ContainsKey(position)) return false;
            var cell = Cells[position];
            if (cell == null || !cell.IsEmpty()) return false;
            cell.PlaceUnit(unit);
            Players[unit.Team].AddUnitToBoard(unit, position);
            InvalidateOccupiedCache();
            return true;
        }

        public bool PlaceUnit(Unit unit, (int, int) position)
        {
            if (position.Item1 < 0)
            {
                return AddBenchUnit(unit.Team, unit, position.Item2);
            }
            if (!Cells.ContainsKey(position)) return false;
            var cell = Cells[position];
            if (cell == null || !cell.IsEmpty()) return false;
            cell.PlaceUnit(unit);
            Players[unit.Team].AddUnitToBoard(unit, position);

            InvalidateOccupiedCache();
            return true;
        }

        public bool MoveUnit((int, int) fromPos, (int, int) toPos)
        {
            if (!Cells.ContainsKey(fromPos) || !Cells.ContainsKey(toPos)) return false;
            var fromCell = Cells[fromPos];
            var toCell = Cells[toPos];
            if (fromCell.Unit == null) return false;
            fromCell.Unit.PlannedPosition = null;
            if ((toCell.IsPlanned() && toCell.Unit != fromCell.Unit) || toCell.IsOccupied()) return false;
            var unit = fromCell.RemoveUnit();
            toCell.PlaceUnit(unit);
            InvalidateOccupiedCache();
            return true;
        }

        public List<Unit> GetUnits()
        {
            var list = new List<Unit>();
            foreach (var cell in Cells.Values) if (cell.Unit != null) list.Add(cell.Unit);
            return list;
        }

        public List<Unit> GetUnitsByTeam(Team team) => GetUnits().Where(u => u.Team == team).ToList();

        public Unit? GetBenchUnit(Team team, int benchIndex)
        {
            return Players[team].Bench.ContainsKey(benchIndex) ? Players[team].Bench[benchIndex] : null;
        }

        public Unit? RemoveBenchUnit(Team team, int benchIndex)
        {

            var unit = Players[team].Bench.ContainsKey(benchIndex) ? Players[team].Bench[benchIndex] : null;
            if (Players[team].Bench.ContainsKey(benchIndex)) Players[team].Bench[benchIndex] = null;
            if (unit != null && unit.Position != null) { Players[team].UnitsOnBoard[unit.Position!.Value] = null; unit.Position = null; }
            return unit;
        }

        public bool AddBenchUnit(Team team, Unit unit, int benchIndex = -1)
        {
            if (unit == null) return false;
            int size = BenchSize;
            if (benchIndex == -1)
            {
                for (int i = 0; i < size; i++){
                    if (!Players[team].Bench.ContainsKey(i) || Players[team].Bench[i] == null) 
                    {
                        unit.Position = (-(int) team, i);
                        Players[team].AddUnitToBench(unit);
                        return true; 
                    }
                } 
            }
            else
            {
                if (benchIndex >= 0 && benchIndex < size && (!Players[team].Bench.ContainsKey(benchIndex) || Players[team].Bench[benchIndex] == null))
                {
                    unit.Position = (-(int) team, benchIndex);
                    Players[team].AddUnitToBench(unit);
                    return true;
                }
            }
            return false;
        }

        public bool PlayerMoveUnit((int, int) fromPos, (int, int) toPos, Team team)
        {
            if ((fromPos.Item1 == -1 || toPos.Item1 == -1) && team != Team.TEAM_1) return false;
            if ((fromPos.Item1 == -2 || toPos.Item1 == -2) && team != Team.TEAM_2) return false;
            var validBoardPositions = GetInitialPositions(team);
            if ((fromPos.Item1 >= 0 && !validBoardPositions.Contains(fromPos)) || (toPos.Item1 >= 0 && !validBoardPositions.Contains(toPos))) return false;

            // bench to board
            if (fromPos.Item1 < 0 && toPos.Item1 >= 0)
            {
                var benchUnit = RemoveBenchUnit(team, fromPos.Item2);
                if (benchUnit == null) return false;
                var toUnit = Cells[toPos].Unit;
                if (toUnit != null && toUnit.Team == team) RemoveUnit(toPos);
                if (!PlaceBoardUnit(benchUnit, toPos)) return false;
                if (toUnit != null && toUnit.Team == team) AddBenchUnit(team, toUnit, benchIndex: fromPos.Item2);
                return true;
            }

            // board to bench
            if (fromPos.Item1 >= 0 && toPos.Item1 < 0)
            {
                var fromUnit = Cells[fromPos].Unit;
                if (fromUnit == null) return false;
                RemoveUnit(fromPos);
                var toUnit = GetBenchUnit(team, toPos.Item2);
                if (toUnit != null) { RemoveBenchUnit(team, toPos.Item2); PlaceBoardUnit(toUnit, fromPos); }
                return AddBenchUnit(team, fromUnit, benchIndex: toPos.Item2);
            }

            // board to board
            if (fromPos.Item1 >= 0 && toPos.Item1 >= 0)
            {
                var toCell = Cells[toPos];
                var fromCell = Cells[fromPos];
                var toUnit = toCell.Unit;
                var fromUnit = fromCell.Unit;
                if (fromUnit == null) return false;
                if (toUnit != null && toUnit.Team != team) return false;
                if (toUnit != null && toUnit.Team == team)
                {
                    var removedTo = toCell.RemoveUnit();
                    var removedFrom = fromCell.RemoveUnit();
                    toCell.PlaceUnit(removedFrom);
                    fromCell.PlaceUnit(removedTo);
                    InvalidateOccupiedCache();
                    return true;
                }
                else
                {
                    return MoveUnit(fromPos, toPos);
                }
            }

            // bench to bench
            if (fromPos.Item1 < 0 && toPos.Item1 < 0)
            {
                var fromBenchUnit = GetBenchUnit(team, fromPos.Item2);
                var toBenchUnit = GetBenchUnit(team, toPos.Item2);
                if (fromBenchUnit == null) return false;
                if (toBenchUnit != null && toBenchUnit.Team != team) return false;
                if (toBenchUnit != null && toBenchUnit.Team == team)
                {
                    RemoveBenchUnit(team, fromPos.Item2);
                    RemoveBenchUnit(team, toPos.Item2);
                    AddBenchUnit(team, fromBenchUnit, benchIndex: toPos.Item2);
                    AddBenchUnit(team, toBenchUnit, benchIndex: fromPos.Item2);
                    return true;
                }
                else
                {
                    RemoveBenchUnit(team, fromPos.Item2);
                    return AddBenchUnit(team, fromBenchUnit, benchIndex: toPos.Item2);
                }
            }

            return false;
        }

        public void AddPlayerByRef(ref Player player, Team team)
        {
            Players[team] = player;
            foreach (var u in GetUnitsByTeam(team)) RemoveUnit(u.Position!.Value);
            foreach (var kv in player.UnitsOnBoard) if (kv.Value != null) PlaceBoardUnit(kv.Value, kv.Key);
        }

        public virtual double L1Distance((int, int) pos1, (int, int) pos2) => throw new NotImplementedException();
        public virtual double L2Distance((int, int) pos1, (int, int) pos2) => throw new NotImplementedException();

        public double PathfindDistance((int, int) start, (int, int) target)
        {
            // Fallback pure C# A* implementation
            var path = FindPath(start, target);
            if (path == null || path.Count == 0) return double.PositiveInfinity;
            return path.Count - 1;
        }

        public double PathfindDistanceToRange((int, int) start, (int, int) target, double attackRange)
        {
            if (L2Distance(start, target) <= attackRange + attackRange * RangeOffset) return 0.0;
            var targetPositions = GetPositionsAtL2Distance(target, attackRange);
            targetPositions = targetPositions.Where(pos => Cells[pos].IsEmpty()).ToList();
            double minDistance = double.PositiveInfinity;
            foreach (var pos in targetPositions)
            {
                var dist = PathfindDistance(start, pos);
                if (dist < minDistance) minDistance = dist;
            }
            return minDistance;
        }

        public virtual List<(int, int)> GetAdjacentPositions((int, int) position) => throw new NotImplementedException();

        public List<BoardCell> GetAdjacentCells((int, int) position)
        {
            if (!IsValidPosition(position)) throw new ArgumentException($"Position {position} is out of bounds");
            var adjacent = GetAdjacentPositions(position);
            return adjacent.Where(pos => Cells.ContainsKey(pos)).Select(pos => Cells[pos]).ToList();
        }

        public BoardCell GetCell((int, int) position)
        {
            if (!Cells.ContainsKey(position)) throw new ArgumentException($"Position {position} is out of bounds");
            return Cells[position];
        }

        public virtual List<BoardCell> GetCellsInL1Range((int, int) position, int l1Range) => GetPositionsInL1Range(position, l1Range).Select(p => Cells[p]).ToList();

        public virtual List<(int, int)> GetPositionsInL1Range((int, int) position, int l1Range) => throw new NotImplementedException();

        public virtual List<(int, int)> GetPositionsInL2Range((int, int) position, double l2Range) => throw new NotImplementedException();

        public virtual List<(int, int)> GetPositionsAtL2Distance((int, int) position, double l2Distance) => throw new NotImplementedException();

        public List<(int, int)> GetInitialPositions(Team team)
        {
            int mid = Height / 2;
            var positions = new List<(int, int)>();
            if (team == Team.TEAM_1)
            {
                for (int x = 0; x < Width; x++) for (int y = 0; y < mid; y++) positions.Add((x, y));
            }
            else
            {
                for (int x = 0; x < Width; x++) for (int y = mid; y < Height; y++) positions.Add((x, y));
            }
            return positions;
        }

        public List<(int, int)> FindPath((int, int) start, (int, int) target)
        {
            var open = new PriorityQueue<(int, int), double>();
            var cameFrom = new Dictionary<(int, int), (int, int)?>();
            var gScore = new Dictionary<(int, int), double>();
            var fScore = new Dictionary<(int, int), double>();

            open.Enqueue(start, 0);
            cameFrom[start] = null;
            gScore[start] = 0.0;
            fScore[start] = L1Distance(start, target);

            while (open.Count > 0)
            {
                var current = open.Dequeue();
                if (current == target)
                {
                    var total = new List<(int, int)> { current };
                    while (cameFrom.ContainsKey(current) && cameFrom[current] != null)
                    {
                        current = cameFrom[current]!.Value;
                        total.Add(current);
                    }
                    total.Reverse();
                    return total;
                }

                foreach (var neighbor in GetAdjacentPositions(current))
                {
                    if (!Cells[neighbor].IsEmpty() && neighbor != target) continue;
                    double tentativeG = gScore[current] + 1.0;
                    if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + L1Distance(neighbor, target);
                        // PriorityQueue doesn't provide 'contains', just enqueue; duplicates are ok but inefficient
                        open.Enqueue(neighbor, fScore[neighbor]);
                    }
                }
            }
            return new List<(int, int)>();
        }

        public List<(int, int)> FindPathGuided((int, int) start, (int, int) target)
        {
            var open = new PriorityQueue<(int, int), double>();
            var cameFrom = new Dictionary<(int, int), (int, int)?>();
            var gScore = new Dictionary<(int, int), double>();
            var fScore = new Dictionary<(int, int), double>();

            open.Enqueue(start, 0);
            cameFrom[start] = null;
            gScore[start] = 0.0;
            fScore[start] = L1Distance(start, target);

            while (open.Count > 0)
            {
                var current = open.Dequeue();
                if (current == target)
                {
                    var total = new List<(int, int)> { current };
                    while (cameFrom.ContainsKey(current) && cameFrom[current] != null)
                    {
                        current = cameFrom[current]!.Value;
                        total.Add(current);
                    }
                    total.Reverse();
                    return total;
                }
                foreach (var neighbor in GetAdjacentPositions(current))
                {
                    if (!Cells[neighbor].IsEmpty() && neighbor != target) continue;
                    double tentativeG = gScore[current] + 1.0;
                    double dl2 = Math.Max(L2Distance(current, target) - L2Distance(neighbor, target), 0.0);
                    tentativeG -= dl2 / 100.0;
                    if (Math.Abs(neighbor.Item1 - current.Item1) > Math.Abs(neighbor.Item2 - current.Item2)) tentativeG -= 0.02;
                    if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + L1Distance(neighbor, target);
                        open.Enqueue(neighbor, fScore[neighbor]);
                    }
                }
            }
            return new List<(int, int)>();
        }

        public List<(int, int)> FindPathToRangeGuided((int, int) start, (int, int) target, double attackRange)
        {
            var targetPositions = GetPositionsInL2Range(target, attackRange).Where(p => Cells[p].IsEmpty() || p == start).ToList();
            targetPositions.Sort((a, b) => Math.Abs(a.Item2 - start.Item2).CompareTo(Math.Abs(b.Item2 - start.Item2)));
            var shortest = new List<(int, int)>();
            int minLen = int.MaxValue;
            foreach (var pos in targetPositions)
            {
                var path = FindPathGuided(start, pos);
                if (path != null && path.Count > 0 && path.Count < minLen) { minLen = path.Count; shortest = path; }
            }
            return shortest;
        }

        public bool IsEmpty((int, int) position) => Cells[position].IsEmpty();

        public Unit RemoveUnit((int, int) position)
        {
            var cell = Cells[position];
            if (cell != null && !cell.IsEmpty()) { var u = cell.RemoveUnit(); InvalidateOccupiedCache(); return u; }
            throw new ArgumentException($"Tried to remove unit from empty cell {position}");
        }

        public void SetPlanned((int, int) position, Unit unit)
        {
            var cell = Cells[position];
            if (cell != null && cell.IsEmpty()) { cell.SetPlanned(); cell.Unit = unit; InvalidateOccupiedCache(); }
            else throw new ArgumentException($"Cell {position} is not empty or already planned");
        }

        public void ResetBoard()
        {
            foreach (var cell in Cells.Values) { cell.Unit = null; cell.CellType = CellType.EMPTY; }
            InvalidateOccupiedCache();
        }

        public Board Clone()
        {
            var nb = new Board((Width, Height));
            foreach (var kv in Cells)
            {
                if (kv.Value.Unit != null) nb.PlaceBoardUnit(kv.Value.Unit.Clone(), kv.Key);
            }
            return nb;
        }

        public virtual void PrintBoard(string title = "") { throw new NotImplementedException(); }
        public virtual (int, int) CoordToPixel((int, int) position, int cellRadius = 50) => throw new NotImplementedException();
        public virtual List<(int, int)> GetCellCorners((int, int) position, int cellRadius = 50) => throw new NotImplementedException();
    }

    public class SquareBoard : Board
    {
        public SquareBoard((int, int)? size = null) : base(size) { RangeOffset = 0.25; }

        public override double L1Distance((int, int) pos1, (int, int) pos2) => Math.Abs(pos1.Item1 - pos2.Item1) + Math.Abs(pos1.Item2 - pos2.Item2);
        public override double L2Distance((int, int) pos1, (int, int) pos2) => Math.Sqrt(Math.Pow(pos1.Item1 - pos2.Item1, 2) + Math.Pow(pos1.Item2 - pos2.Item2, 2));

        public override List<(int, int)> GetAdjacentPositions((int, int) position)
        {
            var (x, y) = position;
            var adjacent = new List<(int, int)> { (x+1,y), (x-1,y), (x,y+1), (x,y-1) };
            return adjacent.Where(p => IsValidPosition(p)).ToList();
        }

        public override List<(int, int)> GetPositionsInL1Range((int, int) position, int l1Range)
        {
            var results = new List<(int, int)>();
            for (int dx = -l1Range; dx <= l1Range; dx++)
            for (int dy = -l1Range + Math.Abs(dx); dy <= l1Range - Math.Abs(dx); dy++)
            {
                var np = (position.Item1 + dx, position.Item2 + dy);
                if (IsValidPosition(np)) results.Add(np);
            }
            return results;
        }

        public override List<(int, int)> GetPositionsInL2Range((int, int) position, double l2Range)
        {
            var results = new List<(int, int)>();
            var x0 = position.Item1; var y0 = position.Item2;
            int minX = Math.Max(0, (int)(x0 - l2Range));
            int maxX = Math.Min(Width - 1, (int)(x0 + l2Range));
            int minY = Math.Max(0, (int)(y0 - l2Range));
            int maxY = Math.Min(Height - 1, (int)(y0 + l2Range));
            for (int x = minX; x <= maxX; x++) for (int y = minY; y <= maxY; y++) if (L2Distance(position, (x,y)) <= l2Range) results.Add((x,y));
            return results;
        }

        public override List<(int, int)> GetPositionsAtL2Distance((int, int) position, double l2Distance)
        {
            var results = new List<(int, int)>();
            var x0 = position.Item1; var y0 = position.Item2;
            int minX = Math.Max(0, (int)(x0 - l2Distance));
            int maxX = Math.Min(Width - 1, (int)(x0 + l2Distance));
            int minY = Math.Max(0, (int)(y0 - l2Distance));
            int maxY = Math.Min(Height - 1, (int)(y0 + l2Distance));
            for (int x = minX; x <= maxX; x++) for (int y = minY; y <= maxY; y++)
            {
                var d = L2Distance(position, (x,y));
                if (l2Distance - d < 1 + 1e-9 && (d - l2Distance) < 1e-9) results.Add((x,y));
            }
            return results;
        }

        public override void PrintBoard(string title = "")
        {
            if (!string.IsNullOrEmpty(title)) ConsoleCompat.WriteLine($"=== {title} ===");
            ConsoleCompat.Write("  ");
            for (int x = 0; x < Width; x++) ConsoleCompat.Write($"{x,2}");
            ConsoleCompat.WriteLine();
            for (int y = 0; y < Height; y++)
            {
                ConsoleCompat.Write($"{y} ");
                for (int x = 0; x < Width; x++)
                {
                    var cell = Cells[(x, y)];
                    if (cell != null && cell.Unit != null)
                    {
                        var symbol = "U";
                        ConsoleCompat.Write($"{symbol,2}");
                    }
                    else ConsoleCompat.Write(" .");
                }
                ConsoleCompat.WriteLine();
            }
        }

        public override (int, int) CoordToPixel((int, int) position, int cellRadius = 50)
        {
            var (x, y) = position;
            var px = (int)(x * cellRadius * Math.Sqrt(2) + cellRadius / Math.Sqrt(2));
            var py = (int)(y * cellRadius * Math.Sqrt(2) + cellRadius / Math.Sqrt(2));
            return (px, py);
        }

        public override List<(int, int)> GetCellCorners((int, int) position, int cellRadius = 50)
        {
            var (x, y) = position;
            var topLeft = ((int)(x + cellRadius / Math.Sqrt(2)), (int)(y + cellRadius / Math.Sqrt(2)));
            var topRight = ((int)(x - cellRadius / Math.Sqrt(2)), (int)(y + cellRadius / Math.Sqrt(2)));
            var bottomRight = ((int)(x - cellRadius / Math.Sqrt(2)), (int)(y - cellRadius / Math.Sqrt(2)));
            var bottomLeft = ((int)(x + cellRadius / Math.Sqrt(2)), (int)(y - cellRadius / Math.Sqrt(2)));
            return new List<(int, int)> { topLeft, topRight, bottomRight, bottomLeft };
        }
    }

    // Minimal hex support: convert odd-r to axial coordinates and back
    internal static class HexHelpers
    {
        public static (int, int) OddrToAxial((int, int) position)
        {
            var (x, y) = position;
            int q = x - (y - (y & 1)) / 2;
            int r = y;
            return (q, r);
        }

        public static (int, int) AxialToOddr((int, int) pos)
        {
            var (q, r) = pos;
            int x = q + (r - (r & 1)) / 2;
            int y = r;
            return (x, y);
        }
    }

    public class HexBoard : Board
    {
        private Dictionary<((int, int), (int, int)), double> _l2Cache = new Dictionary<((int, int), (int, int)), double>();

        public HexBoard((int, int)? size = null) : base(size)
        {
            RangeOffset = 1.0 / 6.0;
            // precompute pixel coords and l2 distances
            var pixel = new Dictionary<(int, int), (double, double)>();
            for (int x = 0; x < Width; x++) for (int y = 0; y < Height; y++)
            {
                var axial = HexHelpers.OddrToAxial((x, y));
                double px = axial.Item1 + axial.Item2 / 2.0;
                double py = axial.Item2 * Math.Sqrt(3) / 2.0;
                pixel[(x, y)] = (px, py);
            }
            for (int x1 = 0; x1 < Width; x1++) for (int y1 = 0; y1 < Height; y1++)
            for (int x2 = 0; x2 < Width; x2++) for (int y2 = 0; y2 < Height; y2++)
            {
                var a = pixel[(x1, y1)]; var b = pixel[(x2, y2)];
                var dist = Math.Sqrt(Math.Pow(a.Item1 - b.Item1, 2) + Math.Pow(a.Item2 - b.Item2, 2));
                _l2Cache[((x1, y1), (x2, y2))] = dist;
            }
        }

        public override List<(int, int)> GetAdjacentPositions((int, int) position)
        {
            var (x, y) = position;
            List<(int, int)> adjacent;
            if (y % 2 == 1)
            {
                adjacent = new List<(int, int)> { (x, y-1), (x+1, y-1), (x+1, y), (x+1, y+1), (x, y+1), (x-1, y) };
            }
            else
            {
                adjacent = new List<(int, int)> { (x-1, y-1), (x, y-1), (x+1, y), (x, y+1), (x-1, y+1), (x-1, y) };
            }
            return adjacent.Where(p => IsValidPosition(p)).ToList();
        }

        public override double L1Distance((int, int) pos1, (int, int) pos2)
        {
            var (x1, y1) = pos1; var (x2, y2) = pos2;
            int q1 = x1 - (y1 - (y1 & 1)) / 2;
            int r1 = y1;
            int q2 = x2 - (y2 - (y2 & 1)) / 2;
            int r2 = y2;
            return (Math.Abs(q1 - q2) + Math.Abs(q1 + r1 - q2 - r2) + Math.Abs(r1 - r2)) / 2.0;
        }

        public override double L2Distance((int, int) pos1, (int, int) pos2) => _l2Cache[(pos1, pos2)];

        public override List<BoardCell> GetCellsInL1Range((int, int) position, int l1Range)
        {
            var results = new List<BoardCell>();
            var axial0 = HexHelpers.OddrToAxial(position);
            for (int q = -l1Range; q <= l1Range; q++)
            for (int r = Math.Max(-l1Range, -q - l1Range); r <= Math.Min(l1Range, -q + l1Range); r++)
            {
                var od = HexHelpers.AxialToOddr((axial0.Item1 + q, axial0.Item2 + r));
                if (IsValidPosition(od)) results.Add(Cells[od]);
            }
            return results;
        }

        public override List<(int, int)> GetPositionsInL1Range((int, int) position, int l1Range)
        {
            var results = new List<(int, int)>();
            var axial0 = HexHelpers.OddrToAxial(position);
            for (int q = -l1Range; q <= l1Range; q++)
            for (int r = Math.Max(-l1Range, -q - l1Range); r <= Math.Min(l1Range, -q + l1Range); r++)
            {
                var od = HexHelpers.AxialToOddr((axial0.Item1 + q, axial0.Item2 + r));
                if (IsValidPosition(od)) results.Add(od);
            }
            return results;
        }

        public override List<(int, int)> GetPositionsInL2Range((int, int) position, double l2Range)
        {
            var results = new List<(int, int)>();
            var axial0 = HexHelpers.OddrToAxial(position);
            int maxRange = (int)(l2Range * 2) + 1;
            for (int q = -maxRange; q <= maxRange; q++)
            for (int r = Math.Max(-maxRange, -q - maxRange); r <= Math.Min(maxRange, -q + maxRange); r++)
            {
                var od = HexHelpers.AxialToOddr((axial0.Item1 + q, axial0.Item2 + r));
                if (IsValidPosition(od) && L2Distance(position, od) <= l2Range) results.Add(od);
            }
            return results;
        }

        public override List<(int, int)> GetPositionsAtL2Distance((int, int) position, double l2Distance)
        {
            var results = new List<(int, int)>();
            var axial0 = HexHelpers.OddrToAxial(position);
            int maxRange = (int)(l2Distance * 2) + 1;
            for (int q = -maxRange; q <= maxRange; q++)
            for (int r = Math.Max(-maxRange, -q - maxRange); r <= Math.Min(maxRange, -q + maxRange); r++)
            {
                var od = HexHelpers.AxialToOddr((axial0.Item1 + q, axial0.Item2 + r));
                if (IsValidPosition(od))
                {
                    var d = L2Distance(position, od);
                    if (l2Distance - d < 1 + 1e-9 && (d - l2Distance) < 1e-9) results.Add(od);
                }
            }
            return results;
        }

        public override void PrintBoard(string title = "")
        {
            if (!string.IsNullOrEmpty(title)) ConsoleCompat.WriteLine($"\n=== {title} ===");
            int cellWidth = 12;
            int cellHeight = 6;
            int vertOffset = 4;
            int canvasHeight = (Height - 1) * vertOffset + cellHeight;
            int canvasWidth = cellWidth * Width + 10;

            // initialize canvas with spaces
            var canvas = new char[canvasHeight, canvasWidth];
            for (int i = 0; i < canvasHeight; i++) for (int j = 0; j < canvasWidth; j++) canvas[i, j] = ' ';

            for (int r = 0; r < Height; r++)
            {
                for (int c = 0; c < Width; c++)
                {
                    int xOffset = ((r % 2 == 1) ? (cellWidth / 2) : 0) + c * cellWidth;
                    int yOffset = r * vertOffset;

                    var cell = Cells[(c, r)];
                    string cellContent;
                    if (cell == null || cell.IsEmpty() || cell.IsPlanned()) cellContent = " ";
                    else
                    {
                        var u = cell.Unit!;
                        var sym = GetUnitSymbol(u.UnitType);
                        cellContent = (u.Team == Team.TEAM_1) ? sym.ToUpper() : sym.ToLower();
                    }

                    var cellArt = CreateHexCell(cellContent);
                    for (int i = 0; i < cellHeight; i++)
                    {
                        int canvasY = yOffset + i;
                        if (canvasY >= canvasHeight) continue;
                        var line = cellArt[i];
                        for (int j = 0; j < line.Length; j++)
                        {
                            int canvasX = xOffset + j;
                            if (canvasX >= canvasWidth) continue;
                            char ch = line[j];
                            if (ch != ' ') canvas[canvasY, canvasX] = ch;
                        }
                    }
                }
            }

            // convert canvas to lines
            var lines = new List<string>(canvasHeight);
            for (int i = 0; i < canvasHeight; i++)
            {
                var rowChars = new char[canvasWidth];
                for (int j = 0; j < canvasWidth; j++) rowChars[j] = canvas[i, j];
                lines.Add(new string(rowChars));
            }

            ConsoleCompat.WriteLine(string.Join("\n", lines));
        }

        private static string[] CreateHexCell(string content)
        {
            var contentStr = (content ?? " ").ToString();
            if (contentStr.Length > 10) contentStr = contentStr.Substring(0, 10);
            // 6 lines per cell
            return new string[]
            {
                "  _ /  \\ _  ",
                " /        \\ ",
                $"|{contentStr.PadLeft((10 + contentStr.Length)/2).PadRight(10)}|",
                $"|{new string(' ', 10)}|",
                " \\ _    _ / ",
                "    \\  /    "
            };
        }

        private static string GetUnitSymbol(UnitType ut)
        {
            return ut switch
            {
                UnitType.WARRIOR => "W",
                UnitType.ARCHER => "A",
                UnitType.MAGE => "M",
                UnitType.TANK => "T",
                UnitType.ASSASSIN => "S",
                UnitType.SUPPORT => "H",
                _ => "U",
            };
        }

        public override (int, int) CoordToPixel((int, int) position, int cellRadius = 50)
        {
            var axial = HexHelpers.OddrToAxial(position);
            int x = (int)(cellRadius * Math.Sqrt(3) * (axial.Item1 + axial.Item2 / 2.0));
            int y = (int)(cellRadius * 1.5 * axial.Item2);
            return (x, y);
        }

        public override List<(int, int)> GetCellCorners((int, int) position, int cellRadius = 50)
        {
            var corners = new List<(int, int)>();
            var (cx, cy) = position;
            for (int i = 0; i < 6; i++)
            {
                double angleDeg = 60 * i - 30;
                double angleRad = angleDeg * Math.PI / 180.0;
                int cornerX = (int)(cx + cellRadius * Math.Cos(angleRad));
                int cornerY = (int)(cy + cellRadius * Math.Sin(angleRad));
                corners.Add((cornerX, cornerY));
            }
            return corners;
        }
    }

    public class DiagonalSquareBoard : SquareBoard
    {
        public DiagonalSquareBoard((int, int)? size = null) : base(size) { }
        public override double L2Distance((int, int) pos1, (int, int) pos2) => Math.Max(Math.Abs(pos1.Item1 - pos2.Item1), Math.Abs(pos1.Item2 - pos2.Item2));
        public override List<(int, int)> GetAdjacentPositions((int, int) position)
        {
            var (x, y) = position;
            var adjacent = new List<(int, int)> { (x+1,y), (x-1,y), (x,y+1), (x,y-1), (x+1,y+1), (x+1,y-1), (x-1,y+1), (x-1,y-1) };
            return adjacent.Where(p => IsValidPosition(p)).ToList();
        }

        public override List<(int, int)> GetPositionsInL1Range((int, int) position, int l1Range)
        {
            var results = new List<(int, int)>();
            for (int dx = -l1Range; dx <= l1Range; dx++) for (int dy = -l1Range; dy <= l1Range; dy++) { var np = (position.Item1 + dx, position.Item2 + dy); if (IsValidPosition(np)) results.Add(np); }
            return results;
        }
    }
}
