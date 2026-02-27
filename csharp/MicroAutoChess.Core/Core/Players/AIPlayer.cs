namespace MicroAutoChess.Core
{
    /// <summary>
    /// Abstract AI player. Subclasses implement preparation-phase decision-making.
    /// The orchestrator queries GetNextAction each tick and enqueues the result.
    /// </summary>
    public abstract class AIPlayer : IGamePlayer
    {
        public int PlayerId { get; }
        public GamePlayerType PlayerType => GamePlayerType.AI;

        protected AIPlayer(int playerId)
        {
            PlayerId = playerId;
        }

        public void OnPreparationStart() { }
        public void OnCombatStart() { }
        public void OnRoundEnd() { }
        public void OnGameOver(int? winnerId) { }

        /// <summary>
        /// Called by the orchestrator once when preparation starts to reset per-round state.
        /// </summary>
        public abstract void BeginPreparation(PlayerGameState state);

        /// <summary>
        /// Called by the orchestrator each tick to get the next action.
        /// Returns null when the AI is done acting this round.
        /// </summary>
        public abstract PlayerAction? GetNextAction(PlayerGameState state);
    }
}
