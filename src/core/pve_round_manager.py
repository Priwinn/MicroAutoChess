from typing import *
from board import Board
from constant_types import UnitType
from player import Player
from utils import get_units_from_config, place_units_from_config, setup_board_from_config
from units import Unit


class PvERoundManager:
    """Manage progressive PvE rounds with different enemy configurations.

    Each configuration is a dict with keys `board_size` and `units` where
    `units` is a mapping of position -> `UnitType`. For backward compatibility
    the manager will also accept the legacy tuple form `(board_size, units)`.
    """

    def __init__(self, configs: List[Dict[str, Any]], initial_budget: int = 0):
        # store provided configs (dicts). Legacy tuple formats are accepted too.
        self.configs: List[Any] = configs

        # store player's initial units snapshot for resets
        # self.initial_player: Optional[List[Unit]] = None
        # optional stored starting positions for player and enemy units (list of (x,y))
        # self.player_positions: Optional[List[Tuple[int, int]]] = None
        # self.enemy_positions: Optional[List[Tuple[int, int]]] = None

        # Budget handling: initial and current player budget
        self.initial_budget = int(initial_budget) + self.configs[0]['budget_inc']
        self.player_budget = int(self.initial_budget)

        self.round_index = 0

    def _clone_unit_list(self, units: Optional[List[Unit]]) -> List[Unit]:
        if not units:
            return []
        out = []
        for u in units:
            c = u.clone()
            # ensure position cleared
            c.position = None
            out.append(c)
        return out

    def _add_config(self, units: List[Unit]):
        self.configs.append(self._clone_unit_list(units))

    def current_round(self) -> int:
        return self.round_index

    def num_rounds(self) -> int:
        return len(self.configs)

    def advance_round(self) -> bool:
        """Advance to the next configuration. Returns True if advanced."""
        if self.round_index + 1 < len(self.configs):
            self.round_index += 1
            # apply budget increment for the new round if specified
            cfg = self.configs[self.round_index]
            inc = 0
            if isinstance(cfg, dict):
                inc = int(cfg.get('budget_inc', 0) or 0)

            self.player_budget = int(self.player_budget) + int(inc)

            return True
        return False

    def reset_to_start(self):
        self.round_index = 0
        # reset budget to initial
        self.player_budget = int(self.initial_budget)


    # def save_player_positions(self, positions: List[Tuple[int, int]]):
    #     """Store the player's preferred starting positions (ordered list)."""
    #     if positions is None:
    #         self.player_positions = None
    #     else:
    #         self.player_positions = list(positions)

    # def save_enemy_positions(self, positions: List[Tuple[int, int]]):
    #     """Store the enemy preferred starting positions (ordered list)."""
    #     if positions is None:
    #         self.enemy_positions = None
    #     else:
    #         self.enemy_positions = list(positions)

    # def get_player_snapshot(self) -> List[Unit]:
    #     """Return fresh clones of the player's initial units."""
    #     return self._clone_unit_list(self.initial_player)
    
    def setup_round(self) -> Tuple[Board, List[Unit], List[Unit]]:
        """Setup round on a new board and return (board, enemy_units, player_units).
        """
        return setup_board_from_config(self.configs[self.round_index])

    def apply_round_to_board(self, board: Board, player1: Player, player2: Player) -> Tuple[Player, Player]:
        """Clear board and place enemy and player units for the current round.

        If `player` is provided, the player's units are placed on the board.
        If `player` is None, the stored initial player units are placed.

        Returns (enemy_units_list, player_units_list) that were placed on board.
        """
        # TODO: needs to behave differently on win or lose. 
        # On win, player units should preserve any permanent stacks (not implemented yet) gained from the previous round,
        # but on lose player should be reset to the stored snapshot. 
        board.reset_board()

        # prepare enemy units (fresh clones)
        enemy_units = place_units_from_config(board, self.configs[self.round_index], team=1)
        # set player's units_on_board from the returned list
        player1.set_units_on_board_from_list(enemy_units)

        # reset/place player2 units on board using dict API
        for u in player2.get_units_on_board_list():
            u.round_reset()
            board.place_board_unit(u, u.position)

                

        return player1, player2
