using System;
using System.Collections.Generic;

namespace MicroAutoChess.Core.Spells
{
    public class SingleTargetStunSpell : AbstractSpell
    {
        public int StunDuration { get; private set; } = 15;
        public double Damage { get; private set; } = 80.0;

        public SingleTargetStunSpell() : base("Bash")
        {
            SpellDelay = 2;
            Ranged = false;
        }

        public override bool Prepare(Core.Unit source, Core.Board board)
        {
            Target = source.CurrentTarget;
            return Target != null;
        }

        public override void Execute(Core.Unit source, Core.Board board, int frameNumber,
            double critRate = 0.0, double critDmg = 0.0, bool canCrit = false, double critRoll = 0.0)
        {
            if (Target == null || !Target.IsAlive()) return;

            bool isCrit = canCrit && critRoll < critRate;
            double damage = Damage * SpellPower;
            if (isCrit) damage *= critDmg;

            var dmg = new Damage
            {
                Value = damage, Crit = isCrit, FrameNumber = frameNumber,
                DmgType = DamageType.PHYSICAL, SpellName = Name
            };
            Target.TakeDamage(dmg, source, Name);

            if (Target.IsAlive())
            {
                var stun = StatusEffect.CreateStun(Name, source.Id, source.Team, StunDuration);
                Target.ApplyStatusEffect(stun, frameNumber);
            }
        }

        public override string Description() =>
            $"Deals {(int)(Damage * SpellPower)} physical damage and stuns the target for {StunDuration} frames.";

        public override Dictionary<string, object?>? OnHitRenderCallback(Core.Unit source, Core.Board board)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "particles",
                ["num"] = 15,
                ["base_color_hint"] = new int[] { 255, 220, 50 },
                ["lifetime_range"] = new double[] { 0.3, 0.7 }
            };
        }

        public override void UpdateLevel(int newLevel)
        {
            Damage = 80.0 + (newLevel - 1) * 40.0;
            StunDuration = 15 + (newLevel - 1) * 3;
        }

        public override Dictionary<string, object?> ToJson()
        {
            var d = base.ToJson();
            d["damage"] = Damage;
            d["stun_duration"] = StunDuration;
            return d;
        }
    }
}
