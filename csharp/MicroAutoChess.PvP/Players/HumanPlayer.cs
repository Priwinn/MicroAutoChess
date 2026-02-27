using MicroAutoChess.Core;

namespace MicroAutoChess.PvPApp
{
    /// <summary>
    /// A local human player that interacts through a PlayerWindow.
    /// The window handles all UI input; this class just satisfies the IGamePlayer
    /// interface so the orchestrator can treat all player types uniformly.
    /// </summary>
    public class HumanPlayer : IGamePlayer
    {
        public int PlayerId { get; }
        public GamePlayerType PlayerType => GamePlayerType.Human;

        /// <summary>
        /// The UI window for this human player. Set by the orchestrator after creation.
        /// </summary>
        public PlayerWindow? Window { get; set; }

        public HumanPlayer(int playerId)
        {
            PlayerId = playerId;
        }

        public void OnPreparationStart()
        {
            Window?.OnPreparationPhase();
        }

        public void OnCombatStart()
        {
            Window?.OnCombatPhase();
        }

        public void OnRoundEnd()
        {
            // Window render loop picks up new state automatically
        }

        public void OnGameOver(int? winnerId)
        {
            Window?.OnGameOver(winnerId);
        }
    }
}
