using System;
using System.Collections.Generic;

namespace MicroAutoChess.Core.Spells
{
    public class SelfHealSpell : AbstractSpell
    {
        public double HealAmount { get; private set; } = 100.0;

        public SelfHealSpell() : base("Heal")
        {
            SpellDelay = 1;
            Ranged = false;
        }

        public override bool Prepare(Core.Unit source, Core.Board board)
        {
            Target = source;
            return true;
        }

        public override void Execute(Core.Unit source, Core.Board board, int frameNumber, double critRate = 0.0, double critDmg = 0.0, bool canCrit = false, double critRoll = 0.0)
        {
            if (Target == null) return;
            var t = Target;
            if (t.IsAlive())
            {
                double healAmount = HealAmount * SpellPower;
                var dmg = new MicroAutoChess.Core.Damage { Value = healAmount, Heal = true, FrameNumber = frameNumber, SpellName = Name };
                try { t.Heal(dmg, source); } catch { }
            }
        }

        public override string Description() => $"Heals the caster for {(int)(HealAmount * SpellPower)} health.";

        public override void UpdateLevel(int newLevel)
        {
            HealAmount = 100.0 + (newLevel - 1) * 50.0;
        }
    }
}
