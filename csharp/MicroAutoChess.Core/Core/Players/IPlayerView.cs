namespace MicroAutoChess.Core
{
    /// <summary>
    /// Abstraction for a human player's UI view. Implemented by platform-specific
    /// windows (e.g. PlayerWindow in PvP) so HumanPlayer can live in Core without
    /// depending on any UI framework.
    /// </summary>
    public interface IPlayerView
    {
        void OnPreparationPhase();
        void OnCombatPhase();
        void OnGameOver(int? winnerId);
    }
}
