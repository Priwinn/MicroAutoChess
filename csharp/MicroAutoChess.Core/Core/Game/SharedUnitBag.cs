using System;
using System.Collections.Generic;
using System.Linq;

namespace MicroAutoChess.Core
{
    /// <summary>
    /// A finite, shared pool of units that all players draw from when shopping.
    /// When a unit is bought it leaves the bag; when sold it returns.
    /// Each UnitType has a fixed rarity defined in GameParams.UnitTypeRarities.
    /// The pool size per rarity tier is configurable via GameParams.PoolSizes.
    /// All randomness is driven through a settable seed.
    /// </summary>
    public class SharedUnitBag
    {
        // Pool: keyed by UnitType → remaining count
        private readonly Dictionary<UnitType, int> _pool = new();
        private readonly GameParams _params;
        private Random _rng;
        private int? _seed;

        // Unit types that can appear in the shop
        private static readonly UnitType[] ShoppableTypes = new[]
        {
            UnitType.WARRIOR, UnitType.ARCHER, UnitType.MAGE,
            UnitType.TANK, UnitType.ASSASSIN,
            UnitType.LIGHTNING_MAGE, UnitType.ICE_TANK,
            UnitType.EARTH_TANK, UnitType.FOREST_ARCHER
        };

        public SharedUnitBag(GameParams gameParams, int? seed = null)
        {
            _params = gameParams;
            _seed = seed;
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
            var sizes = _params.PoolSizes;
            var rarities = _params.UnitTypeRarities;
            foreach (var ut in ShoppableTypes)
            {
                var rarity = rarities.TryGetValue(ut, out var r) ? r : UnitRarity.COMMON;
                _pool[ut] = sizes.TryGetValue(rarity, out int sz) ? sz : 0;
            }
        }

        /// <summary>Remaining count for a specific unit type.</summary>
        public int GetRemaining(UnitType ut)
        {
            return _pool.TryGetValue(ut, out int count) ? count : 0;
        }

        /// <summary>Remaining count for a specific (type, rarity) — legacy compat.</summary>
        public int GetRemaining(UnitType ut, UnitRarity rarity)
        {
            var fixedRarity = _params.UnitTypeRarities.TryGetValue(ut, out var r) ? r : UnitRarity.COMMON;
            if (fixedRarity != rarity) return 0;
            return GetRemaining(ut);
        }

        /// <summary>Total units remaining across all types.</summary>
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
        /// Draw a single unit from the bag. Rolls a rarity tier based on player level,
        /// then picks a unit whose fixed rarity matches that tier, weighted by remaining
        /// copies in the pool. Falls back to any available unit if the rolled tier has no stock.
        /// </summary>
        private Unit DrawUnit(int playerLevel, Team team)
        {
            var rarities = _params.UnitTypeRarities;

            List<UnitType> available = null!;
            // Try up to 500 rarity rolls to find matching units with stock (we could use a while loop but add a hard cap to prevent infinite loops in edge cases where the bag is nearly empty)
            for (int attempt = 0; attempt < 500; attempt++)
            {
                var rolledRarity = RollRarity(playerLevel);
                available = ShoppableTypes
                    .Where(ut => (rarities.TryGetValue(ut, out var r) ? r : UnitRarity.COMMON) == rolledRarity
                                 && GetRemaining(ut) > 0)
                    .ToList();
                if (available.Count > 0) break;
            }

            if (available.Count == 0)
            {
                throw new InvalidOperationException("Unit bag is empty! Cannot draw more units.");
            }

            // Weighted random selection proportional to remaining copies
            int totalWeight = 0;
            foreach (var ut in available)
                totalWeight += GetRemaining(ut);

            int roll = _rng.Next(totalWeight);
            int accum = 0;
            UnitType chosenType = available[0];
            foreach (var ut in available)
            {
                accum += GetRemaining(ut);
                if (roll < accum) { chosenType = ut; break; }
            }

            _pool[chosenType]--;
            var chosenRarity = rarities.TryGetValue(chosenType, out var cr) ? cr : UnitRarity.COMMON;
            return new Unit(chosenType, chosenRarity, team);
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
            var key = unit.UnitType;
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
