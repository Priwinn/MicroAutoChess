using System;
using System.Collections.Generic;

namespace MicroAutoChess.Core.Spells
{
    public abstract class AbstractSpell
    {
        public string Name { get; }
        public double Range { get; set; } = 1.0;
        public int SpellDelay { get; set; } = 2;
        public bool Ranged { get; set; } = false;
        public double SpellPower { get; set; } = 1.0;
        public Core.Unit? Target { get; set; }
        public (int, int)? TargetPosition { get; set; }
        public bool ProjectileAnimation { get; set; } = false;
        public bool OnHitAnimation { get; set; } = false;

        protected AbstractSpell(string name)
        {
            Name = name;
        }

        public abstract bool Prepare(Core.Unit source, Core.Board board);

        public abstract void Execute(Core.Unit source, Core.Board board, int frameNumber, double critRate = 0.0, double critDmg = 0.0, bool canCrit = false, double critRoll = 0.0);

        public override string ToString() => $"{Name} Spell";

        public virtual string Description() => "No description available.";

        public virtual void RoundReset()
        {
            Target = null;
            TargetPosition = null;
        }

        public virtual Dictionary<string, object?>? ProjectileRenderCallback(Core.Unit source, Core.Board board) => null;

        public virtual Dictionary<string, object?>? OnHitRenderCallback(Core.Unit source, Core.Board board) => null;

        public virtual void UpdateLevel(int newLevel) { }

        /// <summary>Serialize spell state to a JSON-friendly dictionary of primitives.</summary>
        public virtual Dictionary<string, object?> ToJson()
        {
            return new Dictionary<string, object?>
            {
                ["name"] = Name,
                ["range"] = Range,
                ["spell_delay"] = SpellDelay,
                ["ranged"] = Ranged,
                ["spell_power"] = SpellPower
            };
        }
    }
}
