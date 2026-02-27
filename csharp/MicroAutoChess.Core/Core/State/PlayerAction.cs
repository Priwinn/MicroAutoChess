using System;

namespace MicroAutoChess.Core
{
    /// <summary>
    /// Types of player actions that go through the PvPGameManager.
    /// These are non-combat actions taken during PREPARATION or (limited) during COMBAT.
    /// </summary>
    public enum PlayerActionType
    {
        BUY_UNIT,
        SELL_UNIT,
        REROLL_SHOP,
        MOVE_UNIT,          // reposition a unit (board↔board, bench↔board, bench↔bench)
        BUY_EXPERIENCE,
        LOCK_SHOP,
    }

    /// <summary>
    /// Encapsulates a single player action request.
    /// The PvPGameManager validates and executes these.
    /// </summary>
    public class PlayerAction
    {
        public int PlayerId { get; }
        public PlayerActionType ActionType { get; }

        // BUY_UNIT: index of the unit slot in the shop (0-4)
        public int? ShopIndex { get; set; }

        // SELL_UNIT: the unit to sell (by reference)
        public Unit? TargetUnit { get; set; }

        // MOVE_UNIT: source and destination positions
        // Positions use the board convention: board cells = (x, y>=0), bench = (-playerTeamId, benchIndex)
        public (int, int)? FromPosition { get; set; }
        public (int, int)? ToPosition { get; set; }

        public PlayerAction(int playerId, PlayerActionType actionType)
        {
            PlayerId = playerId;
            ActionType = actionType;
        }

        public override string ToString()
        {
            return ActionType switch
            {
                PlayerActionType.BUY_UNIT => $"Player {PlayerId}: BUY slot {ShopIndex}",
                PlayerActionType.SELL_UNIT => $"Player {PlayerId}: SELL {TargetUnit?.UnitType}",
                PlayerActionType.REROLL_SHOP => $"Player {PlayerId}: REROLL",
                PlayerActionType.MOVE_UNIT => $"Player {PlayerId}: MOVE {FromPosition} → {ToPosition}",
                PlayerActionType.BUY_EXPERIENCE => $"Player {PlayerId}: BUY XP",
                PlayerActionType.LOCK_SHOP => $"Player {PlayerId}: LOCK SHOP",
                _ => $"Player {PlayerId}: {ActionType}"
            };
        }
    }

    /// <summary>
    /// Result of executing a player action.
    /// </summary>
    public class PlayerActionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        public static PlayerActionResult Ok(string message = "")
            => new() { Success = true, Message = message };

        public static PlayerActionResult Fail(string message)
            => new() { Success = false, Message = message };
    }
}
