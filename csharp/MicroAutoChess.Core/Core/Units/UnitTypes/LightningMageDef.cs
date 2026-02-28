using MicroAutoChess.Core.Spells;
using MicroAutoChess.Core.Traits;

namespace MicroAutoChess.Core.UnitTypes
{
    public static class LightningMageDef
    {
        public static UnitStats CreateStats() => new UnitStats(
            health: 650, attack: 35, spellPower: 1, defense: 20, resistance: 25, range: 4)
        {
            Spell = SpellsFactory.GetSpellInstanceByName("Lightning"),
            MaxMana = 70
        };

        public static string Symbol => "LM";
        public static TraitType[] Traits => new[] { TraitType.ARCANE, TraitType.ELEMENTAL };
    }
}
