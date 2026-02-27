namespace MicroAutoChess.Core
{
    /// <summary>
    /// The type of game player — determines how actions are generated.
    /// </summary>
    public enum GamePlayerType
    {
        /// <summary>A local human player with a UI window for input.</summary>
        Human,
        /// <summary>An AI player that auto-plays during preparation.</summary>
        AI,
        /// <summary>A remote player connected over the network (not yet implemented).</summary>
        Remote,
    }

    /// <summary>
    /// Abstraction for any participant in a PvP game.
    /// Implementations include local human players (with UI) and AI players.
    /// 
    /// Players do not have direct access to the game manager. Human players
    /// enqueue actions via the manager's queue; AI players are queried for
    /// actions by the orchestrator each tick.
    /// </summary>
    public interface IGamePlayer
    {
        /// <summary>The player's unique ID (matches PvPGameManager's player IDs).</summary>
        int PlayerId { get; }

        /// <summary>The type of this player.</summary>
        GamePlayerType PlayerType { get; }

        /// <summary>Called when a new preparation phase starts.</summary>
        void OnPreparationStart();

        /// <summary>Called when the combat phase begins.</summary>
        void OnCombatStart();

        /// <summary>Called when the round ends.</summary>
        void OnRoundEnd();

        /// <summary>Called when the game is over.</summary>
        void OnGameOver(int? winnerId);
    }
}
