using System;
using System.Collections.Generic;

namespace MicroAutoChess.Core.Spells
{
    public class FireballSpell : AbstractSpell
    {
        public double Damage { get; private set; } = 250.0;
        new public int Range { get; private set; } = 5;

        public FireballSpell() : base("Fireball")
        {
            Ranged = true;
            SpellDelay = 2;
        }

        public override bool Prepare(Core.Unit source, Core.Board board)
        {
            Target = source.CurrentTarget;
            return Target != null;
        }

        public override void Execute(Core.Unit source, Core.Board board, int frameNumber, double critRate = 0.0, double critDmg = 0.0, bool canCrit = false, double critRoll = 0.0)
        {
            if (Target == null) return;
            var tgt = Target;
            if (tgt.IsAlive())
            {
                double damage = Damage * SpellPower;
                if (canCrit && critRoll < critRate) damage *= critDmg;
                var dmg = new MicroAutoChess.Core.Damage { Value = damage, Crit = (canCrit && critRoll < critRate), FrameNumber = frameNumber, DmgType = MicroAutoChess.Core.DamageType.MAGICAL, SpellName = Name };
                tgt.TakeDamage(dmg, source);
                try
                {
                    var adjacent = tgt.Position.HasValue ? board.GetAdjacentCells(tgt.Position.Value) : new System.Collections.Generic.List<BoardCell>();
                    foreach (var cell in adjacent)
                    {
                        if (cell != null && cell.Unit != null && cell.Unit.IsAlive() && cell.Unit != tgt && cell.Unit.Team == tgt.Team)
                        {
                            var dmg2 = new MicroAutoChess.Core.Damage { Value = damage / 2.0, Crit = (canCrit && critRoll < critRate), FrameNumber = frameNumber, DmgType = MicroAutoChess.Core.DamageType.MAGICAL, SpellName = Name };
                            cell.Unit.TakeDamage(dmg2, source);
                        }
                    }
                }
                catch { }
            }
        }

        public override string Description() => $"Deals {(int)(Damage * SpellPower)} magical damage to a target and {(int)(Damage/2 * SpellPower)} to adjacent enemies.";

        public override Dictionary<string, object?>? ProjectileRenderCallback(Core.Unit source, Core.Board board)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "projectile",
                ["id"] = "Fireball",
                ["color_hint"] = Tuple.Create(200, 40, 40),
                ["glow"] = true
            };
        }

        public override Dictionary<string, object?>? OnHitRenderCallback(Core.Unit source, Core.Board board)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "aoe",
                ["radius_hex"] = 1,
                ["duration"] = 0.6,
                ["color_hint"] = (object?)null
            };
        }

        public override void UpdateLevel(int newLevel)
        {
            Damage = 250.0 + (newLevel - 1) * 125.0;
        }
    }
}
