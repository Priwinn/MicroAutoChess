using MicroAutoChess.Core.Spells;
using MicroAutoChess.Core.Traits;

namespace MicroAutoChess.Core.UnitTypes
{
    public static class WarriorDef
    {
        public static UnitStats CreateStats() => new UnitStats(
            health: 1200, attack: 65, spellPower: 1, defense: 40, resistance: 40, range: 1)
        {
            Spell = SpellsFactory.GetSpellInstanceByName("Spin Slash"),
            AttackSpeed = 1.0
        };

        public static string Symbol => "W";
        public static TraitType[] Traits => new[] { TraitType.VANGUARD, TraitType.DUELIST };
    }
}
