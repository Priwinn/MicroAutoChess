using System;
using System.Collections.Generic;

namespace MicroAutoChess.Core.Spells
{
    public class SelfShieldSpell : AbstractSpell
    {
        public double ShieldAmount { get; private set; } = 300.0;
        public int ShieldDuration { get; private set; } = 60;

        public SelfShieldSpell() : base("Self Shield")
        {
            SpellDelay = 1;
            Ranged = false;
        }

        public override bool Prepare(Core.Unit source, Core.Board board)
        {
            Target = source;
            return true;
        }

        public override void Execute(Core.Unit source, Core.Board board, int frameNumber,
            double critRate = 0.0, double critDmg = 0.0, bool canCrit = false, double critRoll = 0.0)
        {
            if (!source.IsAlive()) return;
            double amount = ShieldAmount * SpellPower;
            var shield = StatusEffect.CreateShield(Name, source.Id, source.Team, amount, ShieldDuration);
            source.ApplyStatusEffect(shield, frameNumber);
        }

        public override string Description() =>
            $"Grants the caster a shield that absorbs {(int)(ShieldAmount * SpellPower)} damage for {ShieldDuration} frames.";

        public override Dictionary<string, object?>? OnHitRenderCallback(Core.Unit source, Core.Board board)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "particles",
                ["num"] = 30,
                ["base_color_hint"] = new int[] { 80, 180, 255 },
                ["lifetime_range"] = new double[] { 0.5, 1.0 }
            };
        }

        public override void UpdateLevel(int newLevel)
        {
            ShieldAmount = 300.0 + (newLevel - 1) * 150.0;
        }

        public override Dictionary<string, object?> ToJson()
        {
            var d = base.ToJson();
            d["shield_amount"] = ShieldAmount;
            d["shield_duration"] = ShieldDuration;
            return d;
        }
    }
}
