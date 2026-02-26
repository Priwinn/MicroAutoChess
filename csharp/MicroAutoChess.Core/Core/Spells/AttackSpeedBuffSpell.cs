using System;
using System.Collections.Generic;

namespace MicroAutoChess.Core.Spells
{
    public class AttackSpeedBuffSpell : AbstractSpell
    {
        public double BuffAmount { get; private set; } = 0.25;

        public AttackSpeedBuffSpell() : base("Attack Speed Buff")
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
            try
            {
                if (source.IsAlive())
                {
                    if (!source.Buffs.ContainsKey("attack_speed")) source.Buffs["attack_speed"] = 1.0;
                    source.Buffs["attack_speed"] = source.Buffs["attack_speed"] + BuffAmount;
                }
            }
            catch { }
        }

        public override string Description() => $"Increases the caster's attack speed by {BuffAmount * 100.0:0.0}%. Stacks with multiple casts.";

        public override Dictionary<string, object?>? OnHitRenderCallback(Core.Unit source, Core.Board board)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "particles",
                ["num"] = 40,
                ["base_color_hint"] = (object?)null,
                ["lifetime_range"] = Tuple.Create(0.7, 1.6),
                ["size_range"] = Tuple.Create(4, 7),
                ["speed_mult_range"] = Tuple.Create(0.8, 2.2)
            };
        }

        public override void UpdateLevel(int newLevel)
        {
            BuffAmount = 0.25 + (newLevel - 1) * 0.025;
        }
    }
}
