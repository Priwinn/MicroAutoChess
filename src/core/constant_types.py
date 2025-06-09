from enum import Enum
from dataclasses import dataclass


class UnitType(Enum):
    """Available unit types in the game."""
    WARRIOR = "warrior"
    ARCHER = "archer"
    MAGE = "mage"
    TANK = "tank"
    ASSASSIN = "assassin"
    SUPPORT = "support"
    NONE = "none"

class DamageType(Enum):
    """Types of damage that can be dealt."""
    PHYSICAL = "physical"
    MAGICAL = "magical"
    TRUE = "true"  # True damage ignores defense and resistance

class UnitRarity(Enum):
    """Unit rarity levels affecting cost and power."""
    COMMON = 1
    UNCOMMON = 2
    RARE = 3
    EPIC = 4
    LEGENDARY = 5