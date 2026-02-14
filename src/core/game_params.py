from dataclasses import dataclass
from typing import Tuple

@dataclass
class GameParams:
    """Class to hold game parameters and configurations."""
    max_rounds: int = 10
    initial_player_health: int = 100
    initial_player_gold: int = 5
    initial_player_level: int = 1
    board_type: str = "hex"  # or "square"
    board_size: Tuple[int, int] = (7, 8) 
    bench_size: int = 9
    unit_level_up: bool = False  # Whether to automatically check for unit level-ups after adding units