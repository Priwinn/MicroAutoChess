using MicroAutoChess.Core.Spells;
using MicroAutoChess.Core.Traits;

namespace MicroAutoChess.Core.UnitTypes
{
    public static class EarthTankDef
    {
        public static UnitStats CreateStats() => new UnitStats(
            health: 1500, attack: 45, spellPower: 1, defense: 60, resistance: 55, range: 1)
        {
            Spell = SpellsFactory.GetSpellInstanceByName("Shockwave"),
            AttackSpeed = 0.8
        };

        public static string Symbol => "ET";
        public static TraitType[] Traits => new[] { TraitType.VANGUARD, TraitType.WILD };
    }
}
