using System.Collections.Generic;

namespace MicroAutoChess.Core.Traits
{
    /// <summary>
    /// Central registry of all trait definitions and their breakpoints.
    /// </summary>
    public static class TraitRegistry
    {
        public static readonly Dictionary<TraitType, TraitDefinition> Definitions = new()
        {
            // ── VANGUARD (Warrior, Tank, Ice Tank, Earth Tank) ──
            // Frontline synergy: grants defense and resistance to Vanguard units
            {
                TraitType.VANGUARD, new TraitDefinition
                {
                    TraitType = TraitType.VANGUARD,
                    Name = "Vanguard",
                    Breakpoints = new List<TraitBreakpoint>
                    {
                        new() { RequiredCount = 2, Bonuses = new()
                        {
                            new() { Stat = TraitBonusStat.DEFENSE,    Value = 15 },
                            new() { Stat = TraitBonusStat.RESISTANCE, Value = 15 },
                        }},
                        new() { RequiredCount = 4, Bonuses = new()
                        {
                            new() { Stat = TraitBonusStat.DEFENSE,    Value = 35 },
                            new() { Stat = TraitBonusStat.RESISTANCE, Value = 35 },
                        }},
                    }
                }
            },

            // ── DUELIST (Warrior, Archer, Assassin) ──
            // Aggressive melee/ranged hybrids: grants attack speed
            {
                TraitType.DUELIST, new TraitDefinition
                {
                    TraitType = TraitType.DUELIST,
                    Name = "Duelist",
                    Breakpoints = new List<TraitBreakpoint>
                    {
                        new() { RequiredCount = 2, Bonuses = new()
                        {
                            new() { Stat = TraitBonusStat.ATTACK_SPEED, Value = 0.15, IsMultiplicative = false },
                        }},
                        new() { RequiredCount = 3, Bonuses = new()
                        {
                            new() { Stat = TraitBonusStat.ATTACK_SPEED, Value = 0.30, IsMultiplicative = false },
                        }},
                    }
                }
            },

            // ── RANGER (Archer, Forest Archer) ──
            // Ranged specialists: grants attack to Ranger units
            {
                TraitType.RANGER, new TraitDefinition
                {
                    TraitType = TraitType.RANGER,
                    Name = "Ranger",
                    Breakpoints = new List<TraitBreakpoint>
                    {
                        new() { RequiredCount = 2, Bonuses = new()
                        {
                            new() { Stat = TraitBonusStat.ATTACK, Value = 20 },
                        }},
                    }
                }
            },

            // ── ARCANE (Mage, Lightning Mage) ──
            // Spell casters: grants spell power to all team
            {
                TraitType.ARCANE, new TraitDefinition
                {
                    TraitType = TraitType.ARCANE,
                    Name = "Arcane",
                    Breakpoints = new List<TraitBreakpoint>
                    {
                        new() { RequiredCount = 2, Bonuses = new()
                        {
                            new() { Stat = TraitBonusStat.SPELL_POWER, Value = 0.30, IsMultiplicative = false, Scope = TraitBonusScope.ALL_TEAM },
                        }},
                    }
                }
            },

            // ── WILD (Assassin, Earth Tank, Forest Archer) ──
            // Nature synergy: grants bonus health to the whole team
            {
                TraitType.WILD, new TraitDefinition
                {
                    TraitType = TraitType.WILD,
                    Name = "Wild",
                    Breakpoints = new List<TraitBreakpoint>
                    {
                        new() { RequiredCount = 2, Bonuses = new()
                        {
                            new() { Stat = TraitBonusStat.HEALTH, Value = 150, Scope = TraitBonusScope.ALL_TEAM },
                        }},
                        new() { RequiredCount = 3, Bonuses = new()
                        {
                            new() { Stat = TraitBonusStat.HEALTH, Value = 300, Scope = TraitBonusScope.ALL_TEAM },
                        }},
                    }
                }
            },

            // ── ELEMENTAL (Mage, Tank, Lightning Mage, Ice Tank) ──
            // Elemental affinity: grants magic resistance to the whole team
            {
                TraitType.ELEMENTAL, new TraitDefinition
                {
                    TraitType = TraitType.ELEMENTAL,
                    Name = "Elemental",
                    Breakpoints = new List<TraitBreakpoint>
                    {
                        new() { RequiredCount = 2, Bonuses = new()
                        {
                            new() { Stat = TraitBonusStat.RESISTANCE, Value = 15, Scope = TraitBonusScope.ALL_TEAM },
                        }},
                        new() { RequiredCount = 3, Bonuses = new()
                        {
                            new() { Stat = TraitBonusStat.RESISTANCE, Value = 25, Scope = TraitBonusScope.ALL_TEAM },
                        }},
                        new() { RequiredCount = 4, Bonuses = new()
                        {
                            new() { Stat = TraitBonusStat.RESISTANCE, Value = 40, Scope = TraitBonusScope.ALL_TEAM },
                        }},
                    }
                }
            },
        };
    }
}
