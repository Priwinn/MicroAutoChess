using MicroAutoChess.Core.Spells;
using MicroAutoChess.Core.Traits;

namespace MicroAutoChess.Core.UnitTypes
{
    public static class ArcherDef
    {
        public static UnitStats CreateStats() => new UnitStats(
            health: 700, attack: 60, spellPower: 1, defense: 20, resistance: 20, range: 4)
        {
            Spell = SpellsFactory.GetSpellInstanceByName("Attack Speed Buff"),
            MaxMana = 70,
            AttackSpeed = 1.0
        };

        public static string Symbol => "A";
        public static TraitType[] Traits => new[] { TraitType.RANGER, TraitType.DUELIST };
    }
}
