using System;
using System.Collections.Generic;
using System.Linq;

namespace MicroAutoChess.Core.Spells
{
    public class SingleTargetDotSpell : AbstractSpell
    {
        public double DotDamagePerTick { get; private set; } = 15.0;
        public int DotDuration { get; private set; } = 30;
        public int TickInterval { get; private set; } = 5;
        private Unit? _originalTarget;

        public SingleTargetDotSpell() : base("Plague")
        {
            SpellDelay = 2;
            Ranged = true;
            Range = 4;
        }

        public override bool Prepare(Core.Unit source, Core.Board board)
        {
            _originalTarget = source.CurrentTarget;

            // Find all alive enemies on the board
            var enemies = board.Cells.Values
                .Where(c => c.Unit != null && c.Unit.IsAlive() && c.Unit.Team != source.Team)
                .Select(c => c.Unit!)
                .ToList();

            if (enemies.Count == 0) return false;

            // Prioritize enemies not already affected by a DoT
            var noDotEnemies = enemies
                .Where(e => !e.ActiveStatusEffects.Any(se => se.EffectType == StatusEffectType.DOT))
                .ToList();

            Target = noDotEnemies.Count > 0 ? noDotEnemies[0] : enemies[0];
            TargetPosition = Target.Position;
            return true;
        }

        public override void Execute(Core.Unit source, Core.Board board, int frameNumber,
            double critRate = 0.0, double critDmg = 0.0, bool canCrit = false, double critRoll = 0.0)
        {
            if (Target == null || !Target.IsAlive()) return;

            double dmgPerTick = DotDamagePerTick * SpellPower;
            if (canCrit && critRoll < critRate)
                dmgPerTick *= critDmg;

            var dot = StatusEffect.CreateDoT(Name, source.Id, source.Team,
                dmgPerTick, DamageType.MAGICAL, DotDuration, TickInterval);
            Target.ApplyStatusEffect(dot, frameNumber);

            // Return to original target for basic attacks
            if (_originalTarget != null && _originalTarget.IsAlive())
                source.CurrentTarget = _originalTarget;
        }

        public override string Description() =>
            $"Applies a poison dealing {(int)(DotDamagePerTick * SpellPower)} magical damage every {TickInterval} frames for {DotDuration} frames. Prioritizes unaffected targets.";

        public override Dictionary<string, object?>? OnHitRenderCallback(Core.Unit source, Core.Board board)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "particles",
                ["duration"] = 0.4,
                ["color_hint"] = new int[] { 100, 220, 50 }
            };
        }

        public override void UpdateLevel(int newLevel)
        {
            DotDamagePerTick = 15.0 + (newLevel - 1) * 8.0;
            DotDuration = 30 + (newLevel - 1) * 10;
        }

        public override void RoundReset()
        {
            base.RoundReset();
            _originalTarget = null;
        }

        public override Dictionary<string, object?> ToJson()
        {
            var d = base.ToJson();
            d["dot_damage_per_tick"] = DotDamagePerTick;
            d["dot_duration"] = DotDuration;
            d["tick_interval"] = TickInterval;
            return d;
        }
    }
}
