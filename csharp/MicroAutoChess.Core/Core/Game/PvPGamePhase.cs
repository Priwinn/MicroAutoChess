namespace MicroAutoChess.Core
{
    /// <summary>
    /// Phases of a PvP autochess game round.
    /// </summary>
    public enum PvPGamePhase
    {
        /// <summary>Players buy/sell/reroll/reposition units. Timer-driven transition to combat.</summary>
        PREPARATION,
        /// <summary>Combat is running between matched pairs. Limited bench/shop actions allowed.</summary>
        COMBAT,
        /// <summary>Round results applied (damage, XP, gold income).</summary>
        ROUND_END,
        /// <summary>Game is over — one player remains.</summary>
        GAME_OVER
    }
}
