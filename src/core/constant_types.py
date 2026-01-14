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

class CombatAction(Enum):
    MOVE = "move"
    ATTACK = "attack"
    CAST_SPELL = "cast_spell"
    WAIT = "wait"

class CombatEventType(Enum):
    """Types of events that can occur during the game."""
    START_EVENT = "start_event"
    END_EVENT = "end_event"
    OTHER_EVENT = "other_event"
    FAILED_ATTACK = "failed_attack"
    FAILED_MOVE = "failed_move"
    DAMAGE_DEALT = "damage_dealt"
    HEALING_DONE = "healing_done"
    UNIT_SPAWNED = "unit_spawned"
    ACTION_PLANNED = "action_planned"
    CONFLICT_RESOLVED = "conflict_resolved"
    UNIT_DIED = "unit_died"
    SPELL_EXECUTED = "spell_executed"
    MOVE_EXECUTED = "move_executed"
    STATUS_EFFECT_APPLIED = "status_effect_applied"
    STATUS_EFFECT_REMOVED = "status_effect_removed"