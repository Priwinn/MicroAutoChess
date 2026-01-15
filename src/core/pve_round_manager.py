from typing import *
from board import Board
from utils import get_units_from_config, setup_board_from_config
from units import Unit


class PvERoundManager:
    """Manage progressive PvE rounds with different enemy configurations.

    Each configuration is a dict with keys `board_size` and `units` where
    `units` is a mapping of position -> `UnitType`. For backward compatibility
    the manager will also accept the legacy tuple form `(board_size, units)`.
    """

    def __init__(self, configs: List[Dict[str, Any]], initial_budget: int = 4):
        # store provided configs (dicts). Legacy tuple formats are accepted too.
        self.configs: List[Any] = configs

        # store player's initial units snapshot for resets
        self.initial_player = None
        # optional stored starting positions for player and enemy units (list of (x,y))
        self.player_positions: Optional[List[Tuple[int, int]]] = None
        self.enemy_positions: Optional[List[Tuple[int, int]]] = None

        # Budget handling: initial and current player budget
        self.initial_budget = int(initial_budget)
        # Allow first config to set a start budget via 'start_budget'
        first_cfg = configs[0] if configs else None
        try:
            if isinstance(first_cfg, dict) and 'start_budget' in first_cfg:
                self.player_budget = int(first_cfg.get('start_budget', self.initial_budget))
            else:
                self.player_budget = int(self.initial_budget)
        except Exception:
            self.player_budget = int(self.initial_budget)

        self.round_index = 0

    def _clone_unit_list(self, units: Optional[List[Unit]]) -> List[Unit]:
        if not units:
            return []
        out = []
        for u in units:
            try:
                c = u.clone()
                # ensure position cleared
                c.position = None
                out.append(c)
            except Exception:
                # best-effort: keep original reference
                out.append(u)
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
            try:
                inc = 0
                if isinstance(cfg, dict):
                    inc = int(cfg.get('budget_inc', 0) or 0)
                else:
                    # legacy tuple format: no budget_inc
                    inc = 0
                self.player_budget = int(self.player_budget) + int(inc)
            except Exception:
                pass
            return True
        return False

    def reset_to_start(self):
        self.round_index = 0
        # reset budget to initial
        try:
            # if first config defines a start_budget, use it
            first_cfg = self.configs[0] if self.configs else None
            if isinstance(first_cfg, dict) and 'start_budget' in first_cfg:
                self.player_budget = int(first_cfg.get('start_budget', self.initial_budget))
            else:
                self.player_budget = int(self.initial_budget)
        except Exception:
            self.player_budget = int(self.initial_budget)

    def save_player_positions(self, positions: List[Tuple[int, int]]):
        """Store the player's preferred starting positions (ordered list)."""
        if positions is None:
            self.player_positions = None
        else:
            self.player_positions = list(positions)

    def save_enemy_positions(self, positions: List[Tuple[int, int]]):
        """Store the enemy preferred starting positions (ordered list)."""
        if positions is None:
            self.enemy_positions = None
        else:
            self.enemy_positions = list(positions)

    def get_player_snapshot(self) -> List[Unit]:
        """Return fresh clones of the player's initial units."""
        return self._clone_unit_list(self.initial_player)
    
    def setup_round(self) -> Tuple[Board, List[Unit], List[Unit]]:
        """Setup round on a new board and return (board, enemy_units, player_units).
        """
        return setup_board_from_config(self.configs[self.round_index])

    def apply_round_to_board(self, board, player_units: Optional[List[Unit]] = None, reset_player: bool = False) -> Tuple[List[Unit], List[Unit]]:
        """Clear board and place enemy and player units for the current round.

        If `reset_player` is True the player's units are replaced with the stored
        initial snapshot. Otherwise, `player_units` (if provided) will be placed
        (their health/mana preserved by cloning).

        Returns (enemy_units_list, player_units_list) that were placed on board.
        """
        # clear all units from board
        try:
            for u in list(board.get_units()):
                if u and u.position is not None:
                    try:
                        board.remove_unit(u.position)
                    except Exception:
                        pass
        except Exception:
            pass

        # prepare enemy units (fresh clones)
        enemy_units = get_units_from_config(self.configs[self.round_index])
        # prefer stored enemy positions if available
        enemy_positions = self.enemy_positions or board.get_initial_positions(1)
        # place enemies into first available enemy positions
        ei = 0
        for u in enemy_units:
            while ei < len(enemy_positions):
                pos = enemy_positions[ei]
                ei += 1
                if board.is_empty(pos):
                    try:
                        u.team = 1
                        u.current_health = u.get_max_health()
                        u.current_mana = 0
                        board.place_unit(u, pos)
                    except Exception:
                        continue
                    break

        # prepare player units
        if reset_player or not player_units:
            player_units_to_place = self.get_player_snapshot()
        else:
            # clone provided player units and preserve current HP/mana
            player_units_to_place = []
            for pu in player_units:
                try:
                    c = pu.clone()
                    c.current_health = pu.current_health
                    c.current_mana = pu.current_mana
                    player_units_to_place.append(c)
                except Exception:
                    player_units_to_place.append(pu)

        # prefer stored player positions if available
        player_positions = self.player_positions or board.get_initial_positions(2)
        pi = 0
        # If there are more units than positions, remaining units will be placed in first available spots.
        for u in player_units_to_place:
            placed = False
            # try to place in the stored positions list first
            while pi < len(player_positions):
                pos = player_positions[pi]
                pi += 1
                if board.is_empty(pos):
                    try:
                        u.team = 2
                        if getattr(u, 'current_health', None) is None:
                            u.current_health = u.get_max_health()
                        if getattr(u, 'current_mana', None) is None:
                            u.current_mana = 0
                        board.place_unit(u, pos)
                        placed = True
                    except Exception:
                        continue
                    break
            if not placed:
                # fallback: find any empty initial player position
                for pos in board.get_initial_positions(2):
                    if board.is_empty(pos):
                        try:
                            u.team = 2
                            if getattr(u, 'current_health', None) is None:
                                u.current_health = u.get_max_health()
                            if getattr(u, 'current_mana', None) is None:
                                u.current_mana = 0
                            board.place_unit(u, pos)
                            placed = True
                        except Exception:
                            continue
                        break

        return enemy_units, player_units_to_place
