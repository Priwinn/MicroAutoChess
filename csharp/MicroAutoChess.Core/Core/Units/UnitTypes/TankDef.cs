using MicroAutoChess.Core.Spells;
using MicroAutoChess.Core.Traits;

namespace MicroAutoChess.Core.UnitTypes
{
    public static class TankDef
    {
        public static UnitStats CreateStats() => new UnitStats(
            health: 1500, attack: 50, spellPower: 1, defense: 60, resistance: 60, range: 1)
        {
            Spell = SpellsFactory.GetSpellInstanceByName("Heal"),
            AttackSpeed = 0.8
        };

        public static string Symbol => "T";
        public static TraitType[] Traits => new[] { TraitType.VANGUARD, TraitType.ELEMENTAL };
    }
}
