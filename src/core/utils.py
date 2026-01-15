from typing import *
from board import Board, HexBoard
from constant_types import UnitRarity, UnitType
from units import Unit

TEAM1_POSITION_MAP1: Dict[Tuple[int, int], UnitType] = {
    (3, 3): UnitType.TANK,
    (2, 3): UnitType.TANK,
    (1, 2): UnitType.ARCHER,
    (0, 1): UnitType.MAGE,
}

TEAM1_POSITION_MAP2: Dict[Tuple[int, int], UnitType] = {
    (3, 3): UnitType.TANK,
    (2, 3): UnitType.WARRIOR,
    (1, 2): UnitType.ARCHER,
    (1, 1): UnitType.ASSASSIN,
}


def setup_board_from_dict(board_size, unit_dict: Dict[Tuple[int, int], UnitType]) -> Tuple[Board, List[Unit], List[Unit]]:
    """Helper function to set up the board from a dictionary mapping positions to units."""
    board = HexBoard(board_size)
    team1_units = []
    for position, unit_type in unit_dict.items():
        if board.is_empty(position):
            try:
                unit = Unit(unit_type=unit_type, rarity=UnitRarity.COMMON, team=1, level=1)
                board.place_unit(unit, position)
                team1_units.append(unit)
            except Exception as e:
                raise RuntimeError(f"Failed to place unit {unit.unit_type.value} at {position}: {e}")
            
    return board, team1_units, []   # Return empty list for team2_units for compatibility

def setup_board_from_config(config: Dict[str, Any]) -> Tuple[Board, List[Unit], List[Unit]]:
    """Set up the board based on a configuration dict with keys:
    - "board_size": tuple (width, height)
    - "units": mapping of position -> UnitType
    """
    board_size = config.get('board_size') if isinstance(config, dict) else None
    unit_dict = config.get('units') if isinstance(config, dict) else None
    if board_size is None or unit_dict is None:
        raise ValueError("config must be a dict with 'board_size' and 'units' keys")
    return setup_board_from_dict(board_size, unit_dict)

def get_units_from_config(config: Dict[str, Any]) -> List[Unit]:
    """Extract units from a configuration dict without placing them on a board.
    """
    unit_dict = config['units']
    units = []
    for position, unit_type in unit_dict.items():
        try:
            unit = Unit(unit_type=unit_type, rarity=UnitRarity.COMMON, team=1, level=1)
            units.append(unit)
        except Exception as e:
            raise RuntimeError(f"Failed to create unit {unit.unit_type.value} for position {position}: {e}")
    return units
