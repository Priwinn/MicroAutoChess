using MicroAutoChess.Core.Spells;
using MicroAutoChess.Core.Traits;

namespace MicroAutoChess.Core.UnitTypes
{
    public static class AssassinDef
    {
        public static UnitStats CreateStats() => new UnitStats(
            health: 800, attack: 60, spellPower: 1, defense: 25, resistance: 30, range: 1)
        {
            Spell = SpellsFactory.GetSpellInstanceByName("Assassin Blink"),
            MaxMana = 50,
            CritRate = 0.5,
            AttackSpeed = 1.2
        };

        public static string Symbol => "S";
        public static TraitType[] Traits => new[] { TraitType.DUELIST, TraitType.WILD };
    }
}
