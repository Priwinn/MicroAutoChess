using MicroAutoChess.Core.Spells;
using MicroAutoChess.Core.Traits;

namespace MicroAutoChess.Core.UnitTypes
{
    public static class ForestArcherDef
    {
        public static UnitStats CreateStats() => new UnitStats(
            health: 750, attack: 50, spellPower: 1, defense: 20, resistance: 25, range: 4)
        {
            Spell = SpellsFactory.GetSpellInstanceByName("Plague"),
            MaxMana = 60,
            AttackSpeed = 1.0
        };

        public static string Symbol => "FA";
        public static TraitType[] Traits => new[] { TraitType.RANGER, TraitType.WILD };
    }
}
