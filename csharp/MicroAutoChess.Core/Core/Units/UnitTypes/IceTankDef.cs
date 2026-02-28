using MicroAutoChess.Core.Spells;
using MicroAutoChess.Core.Traits;

namespace MicroAutoChess.Core.UnitTypes
{
    public static class IceTankDef
    {
        public static UnitStats CreateStats() => new UnitStats(
            health: 1400, attack: 55, spellPower: 1, defense: 55, resistance: 50, range: 1)
        {
            Spell = SpellsFactory.GetSpellInstanceByName("Bash"),
            AttackSpeed = 0.85
        };

        public static string Symbol => "IT";
        public static TraitType[] Traits => new[] { TraitType.VANGUARD, TraitType.ELEMENTAL };
    }
}
