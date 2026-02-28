using System;
using System.Collections.Generic;
using System.Linq;

namespace MicroAutoChess.Core.Spells
{
    public class LightningSpell : AbstractSpell
    {
        public double Damage { get; private set; } = 200.0;
        public double DamageReduction { get; private set; } = 0.25;
        public int Bounces { get; private set; } = 3;

        public LightningSpell() : base("Lightning")
        {
            Ranged = true;
            SpellDelay = 2;
            this.Range = 5;
        }

        public override bool Prepare(Core.Unit source, Core.Board board)
        {
            Target = source.CurrentTarget;
            return Target != null;
        }

        public override void Execute(Core.Unit source, Core.Board board, int frameNumber,
            double critRate = 0.0, double critDmg = 0.0, bool canCrit = false, double critRoll = 0.0)
        {
            if (Target == null) return;
            var tgt = Target;
            if (!tgt.IsAlive()) return;

            bool isCrit = canCrit && critRoll < critRate;
            double currentDamage = Damage * SpellPower;
            if (isCrit) currentDamage *= critDmg;

            var hitUnits = new HashSet<int>();
            var chainPositions = new List<(int, int)>();

            // Record source position
            if (source.Position.HasValue)
                chainPositions.Add(source.Position.Value);

            // Hit primary target
            var dmg = new Damage
            {
                Value = currentDamage, Crit = isCrit, FrameNumber = frameNumber,
                DmgType = DamageType.MAGICAL, SpellName = Name
            };
            tgt.TakeDamage(dmg, source, Name);
            hitUnits.Add(tgt.Id);
            if (tgt.Position.HasValue)
                chainPositions.Add(tgt.Position.Value);

            // Bounce to closest unhit enemies
            var lastHit = tgt;
            for (int b = 0; b < Bounces; b++)
            {
                currentDamage *= (1.0 - DamageReduction);
                var lastPos = lastHit.Position;
                if (!lastPos.HasValue) break;

                // Find closest alive enemy not yet hit
                Unit? next = null;
                double bestDist = double.MaxValue;
                foreach (var u in board.GetUnitsByTeam(tgt.Team))
                {
                    if (!u.IsAlive() || hitUnits.Contains(u.Id) || !u.Position.HasValue) continue;
                    double dist = board.L1Distance(lastPos.Value, u.Position.Value);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        next = u;
                    }
                }

                if (next == null) break;

                var bounceDmg = new Damage
                {
                    Value = currentDamage, Crit = isCrit, FrameNumber = frameNumber,
                    DmgType = DamageType.MAGICAL, SpellName = Name
                };
                next.TakeDamage(bounceDmg, source, Name);
                hitUnits.Add(next.Id);
                if (next.Position.HasValue)
                    chainPositions.Add(next.Position.Value);
                lastHit = next;
            }

            // Store chain positions for animation (retrieved via OnHitRenderCallback)
            _lastChainPositions = chainPositions;
        }

        private List<(int, int)>? _lastChainPositions;

        public override string Description() =>
            $"Deals {(int)(Damage * SpellPower)} magical damage to a target, then bounces to {Bounces} nearby enemies with {(int)(DamageReduction * 100)}% reduced damage each bounce.";

        public override Dictionary<string, object?>? ProjectileRenderCallback(Core.Unit source, Core.Board board)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "projectile",
                ["id"] = "Lightning",
                ["color_hint"] = Tuple.Create(100, 180, 255),
                ["glow"] = true
            };
        }

        public override Dictionary<string, object?>? OnHitRenderCallback(Core.Unit source, Core.Board board)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "chain",
                ["duration"] = 0.6,
                ["color_hint"] = Tuple.Create(100, 180, 255),
                ["chain_positions"] = _lastChainPositions
            };
        }

        public override void RoundReset()
        {
            base.RoundReset();
            _lastChainPositions = null;
        }

        public override void UpdateLevel(int newLevel)
        {
            Damage = 200.0 + (newLevel - 1) * 100.0;
            Bounces = 3 + (newLevel - 1);
        }

        public override Dictionary<string, object?> ToJson()
        {
            var d = base.ToJson();
            d["damage"] = Damage;
            d["damage_reduction"] = DamageReduction;
            d["bounces"] = Bounces;
            if (_lastChainPositions != null)
                d["chain_positions"] = _lastChainPositions.Select(p => new int[] { p.Item1, p.Item2 }).ToList();
            return d;
        }
    }
}
