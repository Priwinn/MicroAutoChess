using System.Collections.Generic;

namespace MicroAutoChess.Core.Traits
{
    public enum TraitType
    {
        VANGUARD,
        DUELIST,
        RANGER,
        ARCANE,
        WILD,
        ELEMENTAL,
    }

    public enum TraitBonusStat
    {
        ATTACK,
        DEFENSE,
        RESISTANCE,
        ATTACK_SPEED,
        SPELL_POWER,
        HEALTH,
    }

    public enum TraitBonusScope
    {
        TRAIT_OWNERS,
        ALL_TEAM,
    }

    public class TraitBonus
    {
        public TraitBonusStat Stat { get; set; }
        public double Value { get; set; }
        public bool IsMultiplicative { get; set; }
        public TraitBonusScope Scope { get; set; } = TraitBonusScope.TRAIT_OWNERS;
    }

    public class TraitBreakpoint
    {
        public int RequiredCount { get; set; }
        public List<TraitBonus> Bonuses { get; set; } = new();
    }

    public class TraitDefinition
    {
        public TraitType TraitType { get; set; }
        public string Name { get; set; } = "";
        public List<TraitBreakpoint> Breakpoints { get; set; } = new();
    }
}
