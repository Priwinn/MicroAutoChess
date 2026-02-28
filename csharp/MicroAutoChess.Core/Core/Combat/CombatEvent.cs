using System;
using System.Collections.Generic;

namespace MicroAutoChess.Core
{
    public class CombatEvent
    {
        public int FrameNumber { get; set; }
        public Unit? Source { get; set; }
        public Unit? Target { get; set; }
        public CombatEventType EventType { get; set; } = CombatEventType.OTHER_EVENT;
        public string? SpellName { get; set; }
        public double Damage { get; set; } = 0;
        /// <summary>Pre-mitigation damage (before armor/resist reduction).</summary>
        public double RawDamage { get; set; } = 0;
        /// <summary>Amount of damage reduced by armor/resist.</summary>
        public double DamageMitigated { get; set; } = 0;
        public bool CritBool { get; set; } = false;
        public bool IsDot { get; set; } = false;
        public (int, int)? Position { get; set; }
        public string Description { get; set; } = string.Empty;
        public int? MatchId { get; set; }
        /// <summary>Reference to the spell instance that was executed (for render callbacks with state).</summary>
        public Spells.AbstractSpell? SpellInstance { get; set; }

        public CombatEvent() { }

        public override string ToString() => $"[{FrameNumber}] {EventType}: {Description} ({Damage})";
    }
}
