using MicroAutoChess.Core.Spells;
using MicroAutoChess.Core.Traits;

namespace MicroAutoChess.Core.UnitTypes
{
    public static class MageDef
    {
        public static UnitStats CreateStats() => new UnitStats(
            health: 600, attack: 40, spellPower: 1, defense: 20, resistance: 20, range: 4)
        {
            Spell = SpellsFactory.GetSpellInstanceByName("Fireball"),
            MaxMana = 50
        };

        public static string Symbol => "M";
        public static TraitType[] Traits => new[] { TraitType.ARCANE, TraitType.ELEMENTAL };
    }
}
