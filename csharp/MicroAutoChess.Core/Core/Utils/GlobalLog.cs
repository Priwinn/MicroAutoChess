using System.Collections.Generic;

namespace MicroAutoChess.Core
{
    public static class GlobalLog
    {
        public static List<CombatEvent> CombatLog { get; } = new List<CombatEvent>();
    }
}
