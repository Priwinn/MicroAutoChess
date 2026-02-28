using System;
using System.Collections.Generic;

namespace MicroAutoChess.Core.Spells
{
    public class AoEStunSpell : AbstractSpell
    {
        public int StunDuration { get; private set; } = 10;
        public double Damage { get; private set; } = 60.0;
        public int Radius { get; private set; } = 1;

        public AoEStunSpell() : base("Shockwave")
        {
            SpellDelay = 20;
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
            if (!source.IsAlive() || !source.Position.HasValue) return;

            bool isCrit = canCrit && critRoll < critRate;
            double damage = Damage * SpellPower;
            if (isCrit) damage *= critDmg;

            var cells = board.GetCellsInL1Range(source.Position.Value, Radius);
            foreach (var cell in cells)
            {
                if (cell.Unit != null && cell.Unit.IsAlive() && cell.Unit.Team != source.Team)
                {
                    var dmg = new Damage
                    {
                        Value = damage, Crit = isCrit, FrameNumber = frameNumber,
                        DmgType = DamageType.MAGICAL, SpellName = Name
                    };
                    cell.Unit.TakeDamage(dmg, source, Name);

                    if (cell.Unit.IsAlive())
                    {
                        var stun = StatusEffect.CreateStun(Name, source.Id, source.Team, StunDuration);
                        cell.Unit.ApplyStatusEffect(stun, frameNumber);
                    }
                }
            }
        }

        public override string Description() =>
            $"Deals {(int)(Damage * SpellPower)} magical damage and stuns all enemies within {Radius} hex for {StunDuration} frames.";

        public override Dictionary<string, object?>? OnHitRenderCallback(Core.Unit source, Core.Board board)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "aoe",
                ["radius_hex"] = Radius,
                ["duration"] = 0.5,
                ["color_hint"] = new int[] { 255, 220, 50 }
            };
        }

        public override void UpdateLevel(int newLevel)
        {
            Damage = 60.0 + (newLevel - 1) * 30.0;
            StunDuration = 10 + (newLevel - 1) * 3;
        }

        public override Dictionary<string, object?> ToJson()
        {
            var d = base.ToJson();
            d["damage"] = Damage;
            d["stun_duration"] = StunDuration;
            d["radius"] = Radius;
            return d;
        }
    }
}
