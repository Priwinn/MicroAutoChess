from typing import *
from board import Board, HexBoard
from constant_types import UnitRarity, UnitType
from units import Unit

LEVEL1 = {
    'board_size': (7, 8),
    'units': {
        (3, 3): UnitType.TANK,
        (2, 1): UnitType.ARCHER,
    },
    'budget_inc': 3,
}

LEVEL2 = {
    'board_size': (7, 8),
    'units': {
        (3, 3): UnitType.TANK,
        (2, 1): UnitType.MAGE,
        (2, 3): UnitType.WARRIOR,
    },
    'budget_inc': 0,
}

LEVEL3 = {
    'board_size': (7, 8),
    'units': {
        (3, 3): UnitType.TANK,
        (0, 0): UnitType.ARCHER,
        (1, 0): UnitType.ARCHER,
    },
    'budget_inc': 1,
}

LEVEL4 = {
    'board_size': (7, 8),
    'units': {
        (3, 3): UnitType.TANK,
        (2, 3): UnitType.WARRIOR,
        (0, 0): UnitType.ARCHER,
        (1, 0): UnitType.ARCHER,
    },
    'budget_inc': 0
}
LEVEL5 = {
    'board_size': (7, 8),
    'units': {
        (3, 3): UnitType.TANK,
        (2, 3): UnitType.TANK,
        (0, 2): UnitType.ASSASSIN,
        (1, 2): UnitType.ASSASSIN,
        (2, 2): UnitType.ASSASSIN,

    },
    'budget_inc': 1
}

LEVEL6 = {
    'board_size': (7, 8),
    'units': {
        (3, 3): UnitType.TANK,
        (2, 2): UnitType.ASSASSIN,
        (0, 2): UnitType.ASSASSIN,
        (1, 2): UnitType.ASSASSIN,
        (2, 2): UnitType.ASSASSIN,
        (3, 2): UnitType.ASSASSIN,

    },
    'budget_inc': 0
}

LEVEL6 = {
    'board_size': (7, 8),
    'units': {
        (0, 0): UnitType.ARCHER,
        (1, 0): UnitType.ARCHER,
        (2, 0): UnitType.ARCHER,
        (3, 0): UnitType.ARCHER,
        (4, 0): UnitType.ARCHER,
        (5, 0): UnitType.ARCHER,
        (6, 0): UnitType.ARCHER,

    },
    'budget_inc': 0
}

LEVEL7 = {
    'board_size': (7, 8),
    'units': {
        (0, 3): UnitType.TANK,
        (1, 3): UnitType.TANK,
        (2, 3): UnitType.TANK,
        (3, 3): UnitType.TANK,
        (4, 3): UnitType.TANK,
        (5, 3): UnitType.TANK,
        # (6, 3): UnitType.TANK,

    },
    'budget_inc': 0
}

LEVEL8 = {
    'board_size': (7, 8),
    'units': {
        (0, 3): UnitType.WARRIOR,
        (1, 3): UnitType.WARRIOR,
        (2, 3): UnitType.WARRIOR,
        (3, 3): UnitType.WARRIOR,
        (4, 3): UnitType.WARRIOR,
        (5, 3): UnitType.WARRIOR,
        (6, 3): UnitType.WARRIOR,
        (3, 3): UnitType.WARRIOR,

    },
    'budget_inc': 1
}

LEVEL9 = {
    'board_size': (7, 8),
    'units': {
        (0, 0): UnitType.MAGE,
        (1, 0): UnitType.MAGE,
        (2, 0): UnitType.MAGE,
        (3, 0): UnitType.MAGE,
        (4, 0): UnitType.MAGE,
        (5, 0): UnitType.MAGE,
        (6, 0): UnitType.MAGE,
        (3, 3): UnitType.TANK,

    },
    'budget_inc': 1
}

LEVELTANKS = {
    'board_size': (7, 8),
    'units': {(i,j): UnitType.TANK for i in range(7) for j in range(4)},
    'budget_inc': 10
}


LEVELS = [
    LEVEL1, 
    LEVEL2, 
    LEVEL3, 
    LEVEL4, 
    LEVEL5,
    LEVEL6,
    LEVEL7,
    LEVEL8,
    LEVEL9
]