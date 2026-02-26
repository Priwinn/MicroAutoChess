using System;
using System.Collections.Generic;
using System.Linq;

namespace MicroAutoChess.Core
{
    /// <summary>
    /// A finite, shared pool of units that all players draw from when shopping.
    /// When a unit is bought it leaves the bag; when sold it returns.
    /// The pool size per (UnitType, Rarity) tier is configurable via GameParams.
    /// All randomness is driven through a settable seed.
    /// </summary>
    public class SharedUnitBag
    {
        // Pool: keyed by (UnitType, UnitRarity) → remaining count
        private readonly Dictionary<(UnitType, UnitRarity), int> _pool = new();
        private readonly GameParams _params;
        private Random _rng;
        private int? _seed;

        // Unit types that can appear in the shop
        private static readonly UnitType[] ShoppableTypes = new[]
        {
            UnitType.WARRIOR, UnitType.ARCHER, UnitType.MAGE,
            UnitType.TANK, UnitType.ASSASSIN, UnitType.SUPPORT
        };

        public SharedUnitBag(GameParams gameParams, int? seed = null)
        {
            _params = gameParams;
            _seed = seed;
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
            var sizes = _params.PoolSizes;
            foreach (var ut in ShoppableTypes)
            {
                foreach (var rarity in sizes.Keys)
                {
                    _pool[(ut, rarity)] = sizes[rarity];
                }
            }
        }

        /// <summary>Remaining count for a specific (type, rarity).</summary>
        public int GetRemaining(UnitType ut, UnitRarity rarity)
        {
            return _pool.TryGetValue((ut, rarity), out int count) ? count : 0;
        }

        /// <summary>Total units remaining across all types and rarities.</summary>
        public int TotalRemaining()
        {
            return _pool.Values.Sum();
        }

        /// <summary>
        /// Generate a shop (list of units) for a player at the given level.
        /// Units are removed from the pool. Returns up to shopSize units.
        /// </summary>
        public List<Unit> GenerateShop(int playerLevel, Team team, int shopSize = 5)
        {
            var shop = new List<Unit>();
            for (int i = 0; i < shopSize; i++)
            {
                var unit = DrawUnit(playerLevel, team);
                shop.Add(unit);
            }
            return shop;
        }

        /// <summary>
        /// Draw a single unit from the bag according to level-based rarity weights.
        /// Returns the drawn unit (removed from pool). If the chosen (type,rarity) is
        /// exhausted, falls back to any available unit at that rarity, then any available unit.
        /// </summary>
        private Unit DrawUnit(int playerLevel, Team team)
        {
            var rarity = RollRarity(playerLevel);

            // Collect all types available at this rarity
            var available = ShoppableTypes.Where(ut => GetRemaining(ut, rarity) > 0).ToList();
            if (available.Count == 0)
            {
                // Fallback: try any rarity that still has stock
                foreach (var fallbackRarity in _params.PoolSizes.Keys)
                {
                    available = ShoppableTypes.Where(ut => GetRemaining(ut, fallbackRarity) > 0).ToList();
                    if (available.Count > 0) { rarity = fallbackRarity; break; }
                }
            }

            if (available.Count == 0)
            {
                // Pool completely empty — return a unit without removing from pool
                var ut = ShoppableTypes[_rng.Next(ShoppableTypes.Length)];
                return new Unit(ut, UnitRarity.COMMON, team);
            }

            var chosenType = available[_rng.Next(available.Count)];
            _pool[(chosenType, rarity)]--;
            return new Unit(chosenType, rarity, team);
        }

        private UnitRarity RollRarity(int playerLevel)
        {
            int idx = Math.Clamp(playerLevel, 1, _params.RarityProbabilities.Length) - 1;
            var dist = _params.RarityProbabilities[idx];
            double roll = _rng.NextDouble();
            double accum = 0.0;
            for (int i = 0; i < dist.Length; i++)
            {
                accum += dist[i];
                if (roll <= accum) return (UnitRarity)(i + 1);
            }
            return UnitRarity.COMMON;
        }

        /// <summary>
        /// Return a unit to the bag (e.g. when sold or at round end if shop not bought).
        /// </summary>
        public void ReturnUnit(Unit unit)
        {
            var key = (unit.UnitType, unit.Rarity);
            if (_pool.ContainsKey(key))
                _pool[key]++;
            else
                _pool[key] = 1;
        }

        /// <summary>
        /// Return multiple units at once (convenience for returning unsold shop units).
        /// </summary>
        public void ReturnUnits(IEnumerable<Unit> units)
        {
            foreach (var u in units)
            {
                if (u != null) ReturnUnit(u);
            }
        }

        /// <summary>Reset RNG with a new seed.</summary>
        public void SetSeed(int seed)
        {
            _seed = seed;
            _rng = new Random(seed);
        }

        public int? GetSeed() => _seed;
    }
}
