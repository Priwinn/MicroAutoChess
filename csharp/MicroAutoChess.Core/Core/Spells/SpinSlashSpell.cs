using System;
using System.Collections.Generic;

namespace MicroAutoChess.Core.Spells
{
    public class SpinSlashSpell : AbstractSpell
    {
        public double Damage { get; private set; } = 100.0;

        public SpinSlashSpell() : base("Spin Slash")
        {
            SpellDelay = 2;
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
                var adj = source.Position.HasValue ? board.GetAdjacentCells(source.Position.Value) : new System.Collections.Generic.List<BoardCell>();
                foreach (var cell in adj)
                {
                    if (cell != null && cell.Unit != null && cell.Unit.IsAlive() && cell.Unit.Team != source.Team)
                    {
                        double dmg = Damage * SpellPower;
                        if (canCrit && critRoll < critRate) dmg *= critDmg;
                        var d = new MicroAutoChess.Core.Damage { Value = dmg, Crit = (canCrit && critRoll < critRate), FrameNumber = frameNumber, DmgType = MicroAutoChess.Core.DamageType.PHYSICAL, SpellName = Name };
                        cell.Unit.TakeDamage(d, source);
                    }
                }
            }
            catch { }
        }

        public override string Description() => $"Deals {(int)(Damage * SpellPower)} physical damage to all adjacent enemy units.";

        public override Dictionary<string, object?>? OnHitRenderCallback(Core.Unit source, Core.Board board)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "aoe",
                ["radius_hex"] = 1,
                ["duration"] = 0.45,
                ["color_hint"] = (object?)null
            };
        }

        public override void UpdateLevel(int newLevel)
        {
            Damage = 100.0 + (newLevel - 1) * 50.0;
        }

        public override Dictionary<string, object?> ToJson()
        {
            var d = base.ToJson();
            d["damage"] = Damage;
            return d;
        }
    }
}
