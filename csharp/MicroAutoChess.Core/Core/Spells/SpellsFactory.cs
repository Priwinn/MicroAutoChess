using System;
using System.Collections.Generic;

namespace MicroAutoChess.Core.Spells
{
    public static class SpellsFactory
    {
        public static AbstractSpell? GetSpellInstanceByName(string name)
        {
            return name switch
            {
                "Fireball" => new FireballSpell(),
                "Spin Slash" => new SpinSlashSpell(),
                "Heal" => new SelfHealSpell(),
                "Assassin Blink" => new AssassinBlinkSpell(),
                "Attack Speed Buff" => new AttackSpeedBuffSpell(),
                _ => null
            };
        }
    }
}
