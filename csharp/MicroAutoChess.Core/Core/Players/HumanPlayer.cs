namespace MicroAutoChess.Core
{
    /// <summary>
    /// A human player that delegates UI lifecycle events to an <see cref="IPlayerView"/>.
    /// The view is set after construction by the orchestrator / host application.
    /// </summary>
    public class HumanPlayer : IGamePlayer
    {
        public int PlayerId { get; }
        public GamePlayerType PlayerType => GamePlayerType.Human;

        /// <summary>
        /// The UI view for this human player. Set by the orchestrator after creation.
        /// </summary>
        public IPlayerView? View { get; set; }

        public HumanPlayer(int playerId)
        {
            PlayerId = playerId;
        }

        public void OnPreparationStart()
        {
            View?.OnPreparationPhase();
        }

        public void OnCombatStart()
        {
            View?.OnCombatPhase();
        }

        public void OnRoundEnd()
        {
        }

        public void OnGameOver(int? winnerId)
        {
            View?.OnGameOver(winnerId);
        }
    }
}
