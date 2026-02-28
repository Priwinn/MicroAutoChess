using System;
using System.Collections.Generic;
using System.Linq;

namespace MicroAutoChess.Core.Traits
{
    /// <summary>
    /// Computes active trait breakpoints for a team and applies the resulting buffs.
    /// </summary>
    public static class TraitManager
    {
        /// <summary>
        /// Count unique unit types that contribute to each trait for a set of board units.
        /// Only distinct UnitTypes count (multiple copies of the same type don't stack).
        /// </summary>
        public static Dictionary<TraitType, int> CountTraits(IEnumerable<Unit> boardUnits)
        {
            var counts = new Dictionary<TraitType, int>();
            var seenTypes = new HashSet<UnitType>();

            foreach (var unit in boardUnits)
            {
                if (unit == null || !unit.IsAlive()) continue;
                if (!seenTypes.Add(unit.UnitType)) continue;

                foreach (var trait in Unit.GetTraits(unit.UnitType))
                {
                    counts.TryGetValue(trait, out int current);
                    counts[trait] = current + 1;
                }
            }

            return counts;
        }

        /// <summary>
        /// For each trait, find the highest satisfied breakpoint (or null if none met).
        /// Returns trait → (definition, active breakpoint) pairs.
        /// </summary>
        public static Dictionary<TraitType, (TraitDefinition Def, TraitBreakpoint Breakpoint)> GetActiveBreakpoints(
            Dictionary<TraitType, int> traitCounts)
        {
            var active = new Dictionary<TraitType, (TraitDefinition, TraitBreakpoint)>();

            foreach (var (traitType, count) in traitCounts)
            {
                if (!TraitRegistry.Definitions.TryGetValue(traitType, out var def)) continue;

                TraitBreakpoint? best = null;
                foreach (var bp in def.Breakpoints)
                {
                    if (count >= bp.RequiredCount)
                        best = bp;
                }

                if (best != null)
                    active[traitType] = (def, best);
            }

            return active;
        }

        /// <summary>
        /// Apply trait bonuses to a team's board units as status effects.
        /// </summary>
        public static void ApplyTraitBonuses(IEnumerable<Unit> boardUnits, Team team)
        {
            var unitList = boardUnits.Where(u => u != null && u.IsAlive()).ToList();
            var traitCounts = CountTraits(unitList);
            var activeBreakpoints = GetActiveBreakpoints(traitCounts);

            // Build lookup of which units have which traits
            var unitTraits = new Dictionary<int, HashSet<TraitType>>();
            foreach (var unit in unitList)
            {
                unitTraits[unit.Id] = new HashSet<TraitType>(Unit.GetTraits(unit.UnitType));
            }

            foreach (var (traitType, (def, breakpoint)) in activeBreakpoints)
            {
                foreach (var bonus in breakpoint.Bonuses)
                {
                    foreach (var unit in unitList)
                    {
                        bool shouldApply = bonus.Scope == TraitBonusScope.ALL_TEAM
                            || unitTraits[unit.Id].Contains(traitType);

                        if (!shouldApply) continue;

                        var statType = MapBonusStat(bonus.Stat);
                        var effect = StatusEffect.CreateStatBuff(
                            name: $"Trait:{def.Name}",
                            sourceId: -1,
                            sourceTeam: team,
                            stat: statType,
                            value: bonus.Value,
                            duration: 999999,
                            multiplicative: bonus.IsMultiplicative
                        );
                        unit.ActiveStatusEffects.Add(effect);
                    }
                }
            }
        }

        /// <summary>
        /// Remove all trait-granted status effects from a set of units.
        /// </summary>
        public static void RemoveTraitEffects(IEnumerable<Unit> units)
        {
            foreach (var unit in units)
            {
                if (unit == null) continue;
                unit.ActiveStatusEffects.RemoveAll(e => e.Name.StartsWith("Trait:"));
            }
        }

        /// <summary>
        /// Recalculate trait bonuses for a player: removes old trait effects from all units
        /// (board + bench), then reapplies based on current board composition.
        /// Call this whenever units move to/from the board during preparation.
        /// </summary>
        public static void RecalculateTraitBonuses(Player player)
        {
            // Remove old trait effects from ALL units (board + bench)
            var allUnits = new List<Unit>();
            foreach (var u in player.UnitsOnBoard.Values) if (u != null) allUnits.Add(u);
            foreach (var u in player.Bench.Values) if (u != null) allUnits.Add(u);
            RemoveTraitEffects(allUnits);

            // Apply new effects to board units only
            var boardUnits = player.UnitsOnBoard.Values.Where(u => u != null).Select(u => u!).ToList();
            if (boardUnits.Count > 0)
                ApplyTraitBonuses(boardUnits, player.team);

            // Reset health for all units (during prep all units should be at full health)
            foreach (var u in allUnits)
                u.CurrentHealth = u.GetMaxHealth();
        }

        private static StatType MapBonusStat(TraitBonusStat stat)
        {
            return stat switch
            {
                TraitBonusStat.ATTACK => StatType.ATTACK,
                TraitBonusStat.DEFENSE => StatType.DEFENSE,
                TraitBonusStat.RESISTANCE => StatType.RESISTANCE,
                TraitBonusStat.ATTACK_SPEED => StatType.ATTACK_SPEED,
                TraitBonusStat.SPELL_POWER => StatType.SPELL_POWER,
                TraitBonusStat.HEALTH => StatType.HEALTH,
                _ => throw new ArgumentException($"Cannot map {stat} to StatType"),
            };
        }

        /// <summary>
        /// Returns a summary of active traits for display purposes.
        /// Each entry has the trait name, current count, active breakpoint, and next breakpoint.
        /// </summary>
        public static List<TraitSummary> GetTraitSummary(IEnumerable<Unit> boardUnits)
        {
            var unitList = boardUnits.Where(u => u != null).ToList();
            var traitCounts = CountTraits(unitList);
            var summaries = new List<TraitSummary>();

            foreach (var (traitType, count) in traitCounts)
            {
                if (!TraitRegistry.Definitions.TryGetValue(traitType, out var def)) continue;

                TraitBreakpoint? activeBreakpoint = null;
                TraitBreakpoint? nextBreakpoint = null;

                foreach (var bp in def.Breakpoints)
                {
                    if (count >= bp.RequiredCount)
                        activeBreakpoint = bp;
                    else if (nextBreakpoint == null)
                        nextBreakpoint = bp;
                }

                summaries.Add(new TraitSummary
                {
                    TraitType = traitType,
                    Name = def.Name,
                    CurrentCount = count,
                    ActiveBreakpoint = activeBreakpoint?.RequiredCount,
                    NextBreakpoint = nextBreakpoint?.RequiredCount,
                    Breakpoints = def.Breakpoints.Select(bp => bp.RequiredCount).ToArray(),
                    IsActive = activeBreakpoint != null,
                });
            }

            summaries.Sort((a, b) =>
            {
                int cmp = (b.IsActive ? 1 : 0).CompareTo(a.IsActive ? 1 : 0);
                if (cmp != 0) return cmp;
                return b.CurrentCount.CompareTo(a.CurrentCount);
            });

            return summaries;
        }
    }

    public class TraitSummary
    {
        public TraitType TraitType { get; set; }
        public string Name { get; set; } = "";
        public int CurrentCount { get; set; }
        public int? ActiveBreakpoint { get; set; }
        public int? NextBreakpoint { get; set; }
        public int[] Breakpoints { get; set; } = Array.Empty<int>();
        public bool IsActive { get; set; }
    }
}
