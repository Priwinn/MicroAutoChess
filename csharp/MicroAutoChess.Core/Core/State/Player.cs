using System;
using System.Collections.Generic;

namespace MicroAutoChess.Core
{
    public class Player
    {
        public int PlayerId { get; set; }
        public Team team { get; set; }

        // Gameplay state
        public int Health { get; set; }
        public int Gold { get; set; }
        public int Level { get; set; }
        public int Experience { get; set; }
        public int MaxUnitsOnBoard { get; set; } = 5;

        // Shop / bench
        public System.Collections.Generic.List<Unit?> ShopUnits { get; set; } = new System.Collections.Generic.List<Unit?>();
        public int RerollsThisTurn { get; set; } = 0;

        public GameParams GameParams { get; set; }

        public Dictionary<int, Unit?> Bench { get; set; } = new Dictionary<int, Unit?>();
        public Dictionary<(int, int), Unit?> UnitsOnBoard { get; set; } = new Dictionary<(int, int), Unit?>();

        private static readonly Random _rand = new Random();

        public Player(int playerId, Team team, GameParams? gameParams = null)
        {
            PlayerId = playerId;
            this.team = team;
            GameParams = gameParams ?? new GameParams();
            Health = GameParams.InitialPlayerHealth;
            Gold = GameParams.InitialPlayerGold;
            Level = GameParams.InitialPlayerLevel;
            MaxUnitsOnBoard = Level;
            Experience = 0;

            // initialize bench slots
            for (int i = 0; i < GameParams.BenchSize; i++) Bench[i] = null;

            // initialize empty shop
            ShopUnits = new System.Collections.Generic.List<Unit?>(new Unit?[5]);
        }

        // --- Shop / bench helpers (ported from Python) ---
        private void GenerateShop()
        {
            ShopUnits.Clear();
            var types = Enum.GetValues(typeof(UnitType));
            var rarities = GameParams.UnitTypeRarities;
            for (int i = 0; i < 5; i++)
            {
                var ut = (UnitType)types.GetValue(_rand.Next(types.Length))!;
                var rarity = rarities.TryGetValue(ut, out var r) ? r : UnitRarity.COMMON;
                var u = new Unit(ut, rarity, team);
                ShopUnits.Add(u);
            }
        }

        public bool BuyUnit(int shopIndex)
        {
            if (shopIndex < 0 || shopIndex >= ShopUnits.Count) return false;
            var unit = ShopUnits[shopIndex];
            if (unit == null) return false;
            int cost = unit.GetCost();
            if (Gold < cost) return false;
            // bench capacity
            int used = 0; foreach (var v in Bench.Values) if (v != null) used++;
            if (used >= GameParams.BenchSize) return false;
            Gold -= cost;
            AddUnitToBench(unit);
            ShopUnits[shopIndex] = null;
            return true;
        }

        public bool SellUnit(Unit unit)
        {
            // bench
            foreach (var kv in new System.Collections.Generic.List<int>(Bench.Keys))
            {
                if (Bench[kv] == unit)
                {
                    Bench[kv] = null;
                    Gold += unit.GetSellValue();
                    return true;
                }
            }
            // board
            foreach (var kv in new System.Collections.Generic.List<(int,int)>(UnitsOnBoard.Keys))
            {
                if (UnitsOnBoard[kv] == unit)
                {
                    UnitsOnBoard.Remove(kv);
                    Gold += unit.GetSellValue();
                    return true;
                }
            }
            return false;
        }

        public bool AddUnitToBench(Unit unit, int benchIndex = -1)
        {
            if (benchIndex == -1)
            {
                for (int i = 0; i < GameParams.BenchSize; i++)
                {
                    if (!Bench.ContainsKey(i) || Bench[i] == null)
                    {
                        unit.Position = (-(int)team, i);
                        Bench[i] = unit;
                        return true;
                    }
                }
            }
            else
            {
                if (benchIndex >= 0 && benchIndex < GameParams.BenchSize && (!Bench.ContainsKey(benchIndex) || Bench[benchIndex] == null))
                {
                    unit.Position = (-(int)team, benchIndex);
                    Bench[benchIndex] = unit;
                    return true;
                }
            }
            return false;
        }

        public bool AddUnit(Unit unit)
        {
            int used = 0; foreach (var v in Bench.Values) if (v != null) used++;
            if (used < GameParams.BenchSize)
            {
                AddUnitToBench(unit);
                return true;
            }
            if (UnitsOnBoard.Count < MaxUnitsOnBoard)
            {
                int i = 0;
                while (true)
                {
                    var pos = (i, 0);
                    if (!UnitsOnBoard.ContainsKey(pos))
                    {
                        return AddUnitToBoard(unit, pos);
                    }
                    i++;
                }
            }
            return false;
        }

        public bool AddUnitToBoard(Unit unit, (int,int) position)
        {
            if (UnitsOnBoard.Count >= MaxUnitsOnBoard) return false;
            if (UnitsOnBoard.ContainsKey(position) && UnitsOnBoard[position] != null) return false;
            unit.Position = position;
            UnitsOnBoard[position] = unit;
            return true;
        }

        public bool RemoveUnit(Unit unit)
        {
            if (unit.Position == null) return false;
            if (unit.Position.Value.Item1 == -(int)team)
            {
                int benchIndex = unit.Position.Value.Item2;
                if (Bench.ContainsKey(benchIndex) && Bench[benchIndex] == unit)
                {
                    Bench[benchIndex] = null;
                    return true;
                }
            }
            else if (unit.Position.Value.Item1 >= 0) 
            {
                var pos = unit.Position.Value;
                if (UnitsOnBoard.ContainsKey(pos) && UnitsOnBoard[pos] == unit)
                {
                    UnitsOnBoard.Remove(pos);
                    return true;
                }
            }


            return false;


        }

        public bool RerollShop()
        {
            int rerollCost = 2;
            if (Gold < rerollCost) return false;
            Gold -= rerollCost;
            RerollsThisTurn += 1;
            GenerateShop();
            return true;
        }

        public void GainExperience(int amount)
        {
            Experience += amount;
            while (Level < GameParams.MaxLevel)
            {
                int needed = XpToNextLevel();
                if (needed <= 0 || Experience < needed) break;
                Experience -= needed;
                Level += 1;
                MaxUnitsOnBoard = Level;
            }
            // Clamp XP at max level
            if (Level >= GameParams.MaxLevel)
                Experience = 0;
        }

        /// <summary>XP required to reach the next level, or 0 if already max.</summary>
        public int XpToNextLevel()
        {
            if (Level >= GameParams.MaxLevel) return 0;
            return GameParams.XpPerLevel[Level];
        }

        public void TakeDamage(int damage)
        {
            Health = Math.Max(0, Health - damage);
        }

        public bool IsAlive() => Health > 0;

        public int GetTotalUnitCount()
        {
            int b = 0; foreach (var v in Bench.Values) if (v != null) b++;
            int onb = 0; foreach (var v in UnitsOnBoard.Values) if (v != null) onb++;
            return b + onb;
        }

        public float[] ToArray()
        {
            int benchUsed = 0; foreach (var v in Bench.Values) if (v != null) benchUsed++;
            int boardUsed = 0; foreach (var v in UnitsOnBoard.Values) if (v != null) boardUsed++;
            float[] arr = new float[] {
                Health / 100.0f,
                Gold / 100.0f,
                Level / 10.0f,
                Experience / 20.0f,
                benchUsed / Math.Max(1, GameParams.BenchSize),
                boardUsed / Math.Max(1, Math.Max(1, UnitsOnBoard.Count)),
                RerollsThisTurn / 10.0f
            };
            return arr;
        }

        public void SetUnitsOnBoardFromList(System.Collections.Generic.List<Unit> units)
        {
            UnitsOnBoard.Clear();
            foreach (var u in units)
            {
                if (u == null) continue;
                if (!u.Position.HasValue) throw new ArgumentException($"Unit {u} does not have a valid position attribute for board placement.");
                UnitsOnBoard[u.Position.Value] = u;
            }
        }

        public System.Collections.Generic.List<Unit> GetUnitsOnBoardList()
        {
            var list = new System.Collections.Generic.List<( (int,int) pos, Unit u)>();
            foreach (var kv in UnitsOnBoard) if (kv.Value != null) list.Add((kv.Key, kv.Value));
            list.Sort((a,b) => {
                int cmp = a.pos.Item1.CompareTo(b.pos.Item1);
                if (cmp != 0) return cmp;
                return a.pos.Item2.CompareTo(b.pos.Item2);
            });
            var outList = new System.Collections.Generic.List<Unit>();
            foreach (var t in list) outList.Add(t.u);
            return outList;
        }

        public Player Clone()
        {
            var p = new Player(PlayerId, team, GameParams);
            p.Health = Health;
            p.Gold = Gold;
            p.Level = Level;
            p.Experience = Experience;
            p.MaxUnitsOnBoard = MaxUnitsOnBoard;
            p.RerollsThisTurn = RerollsThisTurn;

            // clone bench
            p.Bench.Clear();
            for (int i = 0; i < GameParams.BenchSize; i++)
            {
                if (Bench.ContainsKey(i) && Bench[i] != null) p.Bench[i] = Bench[i]!.Clone();
                else p.Bench[i] = null;
            }

            // clone units on board
            p.UnitsOnBoard.Clear();
            foreach (var kv in UnitsOnBoard)
            {
                if (kv.Value != null) p.UnitsOnBoard[kv.Key] = kv.Value.Clone();
            }

            // clone shop
            p.ShopUnits = new System.Collections.Generic.List<Unit?>();
            foreach (var s in ShopUnits) p.ShopUnits.Add(s != null ? s.Clone() : null);

            return p;
        }
    }
}

