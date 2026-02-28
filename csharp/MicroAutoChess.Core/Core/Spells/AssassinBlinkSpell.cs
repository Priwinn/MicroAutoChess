using System;
using System.Collections.Generic;

namespace MicroAutoChess.Core.Spells
{
    public class AssassinBlinkSpell : AbstractSpell
    {
        new public int Range { get; private set; } = 3;
        public double Damage { get; private set; } = 100.0;

        public AssassinBlinkSpell() : base("Assassin Blink")
        {
            SpellDelay = 3;
            Ranged = true;
        }

        public override bool Prepare(Core.Unit source, Core.Board board)
        {
            if (!source.Position.HasValue) return false;
            Core.Unit? weakest = null;
            double weakestHealth = double.PositiveInfinity;
            var cells = board.GetCellsInL1Range(source.Position.Value, Range);
            foreach (var cell in cells)
            {
                if (cell != null && cell.Unit != null && cell.Unit.IsAlive() && cell.Unit.Team != source.Team)
                {
                    if (cell.Unit.CurrentHealth < weakestHealth)
                    {
                        weakestHealth = cell.Unit.CurrentHealth;
                        weakest = cell.Unit;
                    }
                }
            }
            Target = weakest;
            return weakest != null;
        }

        public override void Execute(Core.Unit source, Core.Board board, int frameNumber, double critRate = 0.0, double critDmg = 0.0, bool canCrit = false, double critRoll = 0.0)
        {
            if (Target == null) return;
            var tgt = Target;
            var adjacentPositions = tgt.Position.HasValue ? board.GetAdjacentPositions(tgt.Position.Value) : new System.Collections.Generic.List<(int,int)>();
            var valid = new List<(int, int)>();
            foreach (var pos in adjacentPositions)
            {
                var c = board.GetCell(pos);
                if ((c.IsEmpty() && !c.IsPlanned()) || (source.Position.HasValue && pos == source.Position.Value)) valid.Add(((int)pos.Item1, (int)pos.Item2));
            }
            if (source.Position.HasValue)
            {
                valid.Sort((a, bpos) => board.L1Distance(source.Position.Value, bpos).CompareTo(board.L1Distance(source.Position.Value, a)));
                if (valid.Count > 0) board.MoveUnit(source.Position.Value, valid[0]);
            }
            else
            {
                var alt = tgt.Position.HasValue ? board.GetPositionsInL1Range(tgt.Position.Value, 2) : new System.Collections.Generic.List<(int,int)>();
                var val2 = new List<(int, int)>();
                foreach (var pos in alt)
                {
                    var c2 = board.GetCell(pos);
                    if ((c2.IsEmpty() && !c2.IsPlanned()) || (source.Position.HasValue && pos == source.Position.Value)) val2.Add(((int)pos.Item1, (int)pos.Item2));
                }
                if (source.Position.HasValue)
                {
                    val2.Sort((a, bpos) => board.L1Distance(source.Position.Value, bpos).CompareTo(board.L1Distance(source.Position.Value, a)));
                    if (val2.Count > 0) board.MoveUnit(source.Position.Value, val2[0]);
                }
            }
            double dmg = Damage * SpellPower;
            var d = new MicroAutoChess.Core.Damage { Value = dmg, Crit = false, FrameNumber = frameNumber, DmgType = MicroAutoChess.Core.DamageType.PHYSICAL, SpellName = Name };
            tgt.TakeDamage(d, source);
            source.CurrentTarget = tgt;
            Range += 1;
        }

        public override string Description() => $"Blinks to the weakest enemy within {Range} cells and deals {(int)(Damage * SpellPower)} physical damage. Range increases by 1 each cast.";

        public override void RoundReset()
        {
            base.RoundReset();
            Range = 3;
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
