using System;
using MicroAutoChess.Core.Spells;

namespace MicroAutoChess.Core
{
    public class UnitStats
    {
        public double Health { get; set; }
        public double Attack { get; set; }
        public double SpellPower { get; set; }
        public double Defense { get; set; }
        public double Resistance { get; set; }
        public double Range { get; set; }
        public double CritRate { get; set; } = 0.25;
        public double CritDmg { get; set; } = 1.5;
        public double Mana { get; set; } = 0;
        public double MaxMana { get; set; } = 100;
        public double MoveSpeed { get; set; } = 1.0;
        public double AttackSpeed { get; set; } = 1.0;
        public AbstractSpell? Spell { get; set; }
        public double InitialMana { get; set; } = 0;

        public UnitStats() { }

        public UnitStats(double health, double attack, double spellPower, double defense, double resistance, double range)
        {
            Health = health;
            Attack = attack;
            SpellPower = spellPower;
            Defense = defense;
            Resistance = resistance;
            Range = range;
        }
    }
}
