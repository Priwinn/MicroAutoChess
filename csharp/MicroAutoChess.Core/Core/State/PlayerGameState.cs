namespace MicroAutoChess.Core
{
    /// <summary>
    /// Read-only view of a single player's game state, provided to agents
    /// so they can make decisions without direct access to PvPGameManager.
    /// </summary>
    public class PlayerGameState
    {
        public int PlayerId { get; }
        public Player Player { get; }
        public Board Board { get; }
        public GameParams Params { get; }
        public int CurrentRound { get; }

        public PlayerGameState(int playerId, Player player, Board board, GameParams gameParams, int currentRound)
        {
            PlayerId = playerId;
            Player = player;
            Board = board;
            Params = gameParams;
            CurrentRound = currentRound;
        }
    }
}
