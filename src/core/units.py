"""
Unit definitions and mechanics for auto chess.
"""

from enum import Enum
from dataclasses import dataclass
from typing import List, Dict, Optional
import numpy as np
import random
from src.core.spells import AbstractSpell, FireballSpell


class UnitType(Enum):
    """Available unit types in the game."""
    WARRIOR = "warrior"
    ARCHER = "archer"
    MAGE = "mage"
    TANK = "tank"
    ASSASSIN = "assassin"
    SUPPORT = "support"
    NONE = "none"



class UnitRarity(Enum):
    """Unit rarity levels affecting cost and power."""
    COMMON = 1
    UNCOMMON = 2
    RARE = 3
    EPIC = 4
    LEGENDARY = 5


@dataclass
class UnitStats:
    """Base statistics for a unit."""
    health: float
    attack: float
    defense: float
    resistance: float
    range: float
    crit_rate: float = 0.25
    crit_dmg: float = 1.5  
    mana: float = 0
    max_mana: float = 100
    move_speed: float = 1.0 
    attack_speed: float = 1.0


@dataclass
class Unit:
    """
    Represents a single unit in the auto chess game.
    """
    unit_type: UnitType
    rarity: UnitRarity
    team: int
    
    level: int = 1
    position: Optional[tuple] = None
    current_health: float = 100
    current_mana: float = 0
    basic_attack_mana: float = 10
    spell: AbstractSpell = FireballSpell()
    spell_crit: bool = False  # Whether the unit's spell can crit

    base_stats: UnitStats = None  # Base stats for the unit, initialized later 
    #TODO: not sure why we have base_stats and other stats, maybe we can remove base_stats

    
    def __post_init__(self):
        """Initialize unit with default stats if not provided."""
        if self.base_stats is None:
            self.base_stats = self._get_default_stats()
        
        if self.current_health is None:
            self.current_health = self.get_max_health()
    
    def _get_default_stats(self) -> UnitStats:
        """Get default stats based on unit type."""
        stat_templates = {
            UnitType.WARRIOR: UnitStats(health=100, attack=15, defense=8, resistance=8, range=1),
            UnitType.ARCHER: UnitStats(health=70, attack=12, defense=3, resistance=3, range=4, max_mana=100),
            UnitType.MAGE: UnitStats(health=60, attack=20, defense=2, resistance=2, range=4, max_mana=50),
            UnitType.TANK: UnitStats(health=150, attack=8, defense=12, resistance=12, range=1),
            UnitType.ASSASSIN: UnitStats(health=80, attack=12, defense=4, resistance=4, range=1, crit_rate=0.5),
            UnitType.SUPPORT: UnitStats(health=90, attack=5, defense=6, resistance=6, range=4, max_mana=80),
        }
        return stat_templates.get(self.unit_type, UnitStats(health=100, attack=10, defense=5, resistance=5, range=1))
    
    def get_max_health(self) -> float:
        """Get maximum health based on level."""
        return self.base_stats.health * (1 + (self.level - 1) * 0.5)

    def get_attack(self) -> float:
        """Get attack value based on level."""
        return self.base_stats.attack * (1 + (self.level - 1) * 0.3)

    def get_defense(self) -> float:
        """Get defense value based on level."""
        return self.base_stats.defense * (1 + (self.level - 1) * 0.2)

    def get_cost(self) -> int:
        """Get unit purchase cost."""
        base_cost = self.rarity.value
        return base_cost * (3 ** (self.level - 1))

    def get_sell_value(self) -> int:
        """Get unit sell value."""
        return self.rarity.value if self.level == 1 else self.get_cost()-1
    
    def is_alive(self) -> bool:
        """Check if unit is alive."""
        return self.current_health > 0
    
    def take_damage(self, damage: float) -> float:
        """Apply damage to unit, returns actual damage taken."""
        mitigated_damage = max(1, damage * 10 / (10 + self.get_defense()))
        actual_damage = min(mitigated_damage, self.current_health)
        self.current_health -= actual_damage
        return actual_damage

    def heal(self, amount: float):
        """Heal the unit."""
        max_health = self.get_max_health()
        self.current_health = min(max_health, self.current_health + amount)

    def get_basic_final_damage(self, crit_roll: float) -> float:
        """
        Calculate final damage dealt to a target unit.
        Includes crit chance and crit damage.
        """
        base_damage = self.get_attack()
        if crit_roll < self.base_stats.crit_rate:
            return int(base_damage * self.base_stats.crit_dmg)
        return base_damage
    
    def can_upgrade(self, other_units: List['Unit']) -> bool:
        """Check if unit can be upgraded with other units."""
        # Need 3 units of same type and level to upgrade
        same_type_level = [
            u for u in other_units 
            if (u.unit_type == self.unit_type and 
                u.level == self.level and 
                u != self)
        ]
        return len(same_type_level) >= 2
    
    def upgrade(self) -> 'Unit':
        """Create upgraded version of this unit."""
        upgraded = Unit(
            unit_type=self.unit_type,
            rarity=self.rarity,
            team=self.team,
            level=self.level + 1,
            position=self.position
        )
        return upgraded
    
    def to_array(self) -> np.ndarray:
        """Convert unit to numerical array representation."""
        # Encode unit as array for ML models
        type_encoding = [0] * len(UnitType)
        type_encoding[list(UnitType).index(self.unit_type)] = 1
        
        return np.array([
            *type_encoding,                    # Unit type (one-hot)
            self.rarity.value,                 # Rarity
            self.level,                        # Level
            self.current_health / self.get_max_health(),  # Health ratio
            self.current_mana / self.base_stats.max_mana if self.base_stats.max_mana > 0 else 0,  # Mana ratio
            self.get_attack(),                 # Attack
            self.get_defense(),                # Defense
            self.base_stats.range,             # Range
            self.base_stats.crit_rate,         # Crit rate
            self.base_stats.crit_dmg           # Crit damage
        ], dtype=np.float32)
    
    def clone(self) -> 'Unit':
        """Create a deep copy of the unit."""
        return Unit(
            unit_type=self.unit_type,
            rarity=self.rarity,
            level=self.level,
            team=self.team,
            position=self.position,
            current_health=self.current_health,
            current_mana=self.current_mana,
            base_stats=UnitStats(**self.base_stats.__dict__)
        )
    def _get_unit_symbol(self) -> str:
        """Get symbol for unit type."""
        symbols = {
            UnitType.WARRIOR: "W",
            UnitType.ARCHER: "A",
            UnitType.MAGE: "M",
            UnitType.TANK: "T",
            UnitType.ASSASSIN: "S",
            UnitType.SUPPORT: "H"
        }
        return symbols.get(self.unit_type, "U")
    
    def add_basic_attack_mana(self):
        """Add mana to the unit from basic attack. Overflow mana is allowed for mana gained via basic attacks."""
        self.current_mana += self.basic_attack_mana

    def premigitation_mana(self, dmg: float):
        """Add mana to the unit from premigitation damage. Overflow is not allowed for mana gained via damage taken."""
        self.current_mana = min(self.base_stats.max_mana, self.current_mana + 0.01 * dmg)

    def postmigitation_mana(self, dmg: float):
        """Add mana to the unit from postmigitation damage. Overflow is not allowed for mana gained via damage taken."""
        self.current_mana = min(self.base_stats.max_mana, self.current_mana + 0.07 * dmg)
