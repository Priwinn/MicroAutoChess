"""
Unit definitions and mechanics for auto chess.
"""

from enum import Enum
from dataclasses import dataclass, field
from typing import *
import numpy as np
import random
# from numba.experimental import jitclass
# from numba import float32
from damage import Damage
from spells import AbstractSpell, FireballSpell, AttackSpeedBuffSpell, SelfHealSpell, AssassinBlinkSpell
from constant_types import CombatAction, CombatEventType, UnitType, UnitRarity, DamageType
from itertools import count
import global_log
from combat_event import CombatEvent




# spec = {
#     'health': float32,
#     'attack': float32,
#     'defense': float32,
#     'resistance': float32,
#     'range': float32,
#     'crit_rate': float32,
#     'crit_dmg': float32,
#     'mana': float32,
#     'max_mana': float32,
#     'move_speed': float32,
#     'attack_speed': float32,
#     'spell': AbstractSpell
# }
# @jitclass(spec)
@dataclass
class UnitStats:
    """Base statistics for a unit."""
    health: float
    attack: float
    spell_power: float
    defense: float
    resistance: float
    range: float
    crit_rate: float = 0.25
    crit_dmg: float = 1.5  
    mana: float = 0
    max_mana: float = 100
    move_speed: float = 1.0 
    attack_speed: float = 1.0
    spell: AbstractSpell = FireballSpell()


@dataclass
class Unit:
    """
    Represents a single unit in the auto chess game.
    """
    _ids = count(0)
    unit_type: UnitType
    rarity: UnitRarity
    team: int
    
    level: int = 1
    position: Optional[tuple] = None
    planned_position: Optional[tuple] = None  # Position planned for next move
    current_health: float = None
    current_mana: float = 0
    basic_attack_mana: float = 10
    cost : int = 1
    
    spell_crit: bool = False  # Whether the unit's spell can crit
    basic_attack_overflow: float = 0.0  # Saves overflow initiative (unit gains initiative equal to basic attack each round). Basic attack takes 10 initiative.
    current_target: Optional['Unit'] = None  # Current target unit for attacks, it will be prioritized in case of ties
    buffs: Dict[str, float] = field(default_factory=dict)  # Buffs applied to the unit, e.g. attack speed, damage

    base_stats: UnitStats = None  # Base stats for the unit, initialized later 
    #TODO: not sure why we have base_stats and other stats, maybe we can remove base_stats

    
    def __post_init__(self):
        """Initialize unit with default stats if not provided."""
        if self.base_stats is None:
            self.base_stats = self._get_default_stats()
        
        if self.current_health is None:
            self.current_health = self.get_max_health()
        # TODO UPDATE SPELL STATS LIVE INSTEAD
        self.base_stats.spell.spell_power = self.base_stats.spell_power
        self.id = next(self._ids)
    
    def _get_default_stats(self) -> UnitStats:
        """Get default stats based on unit type."""
        stat_templates = {
            UnitType.WARRIOR: UnitStats(health=1200, attack=65, spell_power=1, defense=40, resistance=40, range=1),
            UnitType.ARCHER: UnitStats(health=700, attack=60, spell_power=1, defense=20, resistance=20, range=4, max_mana=70, spell=AttackSpeedBuffSpell(), attack_speed= 1.0),
            UnitType.MAGE: UnitStats(health=600, attack=40, spell_power=2.5, defense=20, resistance=20, range=4, max_mana=50),
            UnitType.TANK: UnitStats(health=1500, attack=50, spell_power=1, defense=60, resistance=60, range=1, spell= SelfHealSpell(), attack_speed= 0.8),
            UnitType.ASSASSIN: UnitStats(health=800, attack=60, spell_power=1, defense=25, resistance=30, range=1, max_mana=50, spell=AssassinBlinkSpell(), crit_rate=0.5, attack_speed= 1.2),
            UnitType.SUPPORT: UnitStats(health=900, attack=25, spell_power=1, defense=20, resistance=20, range=4, max_mana=80),
        }
        return stat_templates.get(self.unit_type, UnitStats(health=100, attack=10, spell_power=1, defense=5, resistance=5, range=1))

    def get_max_health(self) -> float:
        """Get maximum health based on level."""
        return self.base_stats.health * (1 + (self.level - 1) * 0.5)

    def get_attack(self) -> float:
        """Get attack value based on level."""
        return self.base_stats.attack * (1 + (self.level - 1) * 0.3)

    def get_defense(self) -> float:
        """Get defense value based on level."""
        return self.base_stats.defense
    
    def get_resistance(self) -> float:
        """Get resistance value based on level."""
        return self.base_stats.resistance
    
    def get_attack_speed(self) -> float:
        """Get attack speed value including buffs and items."""
        return self.base_stats.attack_speed * self.buffs.get("attack_speed", 1.0)

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

    # TODO LOG COMBAT EVENTS HERE
    def take_damage(self, damage_obj: Damage, source: 'Unit' = None, spell_name: str = "") -> float:
        """Apply damage to unit, returns actual damage taken."""
        if damage_obj.heal:
            raise ValueError("Damage object indicates healing, use heal() method instead.")
        self.premigitation_mana(damage_obj.value)
        # mitigated_damage = 0
        if damage_obj.dmg_type == DamageType.PHYSICAL:
            mitigated_damage = max(1, damage_obj.value * 100 / (100 + self.get_defense()))
        elif damage_obj.dmg_type == DamageType.MAGICAL:
            mitigated_damage = max(1, damage_obj.value * 100 / (100 + self.get_resistance()))
        elif damage_obj.dmg_type == DamageType.TRUE:
            mitigated_damage = damage_obj.value
        if (type(damage_obj.dmg_type) is not DamageType) :
            global_log.log_error(f"Damage type error: {damage_obj.dmg_type} is not of type DamageType")
        actual_damage = min(mitigated_damage, self.current_health)
        self.current_health -= actual_damage
        self.postmigitation_mana(actual_damage)
        combat_event = CombatEvent(
            frame_number=damage_obj.frame_number,  # This should be set by the combat engine
            source=source,
            target=self,
            event_type=CombatEventType.DAMAGE_DEALT,
            spell_name=spell_name if spell_name else None,
            damage=actual_damage,
            crit_bool=damage_obj.crit,
            position=self.position,
            description=f"{source.unit_type.value if source else 'Unknown'} dealt {actual_damage} {damage_obj.dmg_type.value} damage to {self.unit_type.value}{' with a crit' if damage_obj.crit else ''}.",
            match_id=None
        )
        global_log.combat_log.append(combat_event)
        return actual_damage

    def heal(self, damage_obj: Damage, source: 'Unit' = None, spell_name: str = "") -> None:
        """Heal the unit."""
        if not damage_obj.heal:
            raise ValueError("Damage object does not indicate healing, use take_damage() method instead.")
        max_health = self.get_max_health()
        self.current_health = min(max_health, self.current_health + damage_obj.value)
        combat_event = CombatEvent(
            frame_number=0,  # This should be set by the combat engine
            source=source,
            target=self,
            event_type=CombatEventType.HEALING_DONE,
            spell_name=spell_name,
            damage=damage_obj.value,
            crit_bool=False,
            position=self.position,
            description=f"{self.unit_type.value} healed for {damage_obj.value} health.",
            match_id=None
        )
        global_log.combat_log.append(combat_event)

    def get_basic_final_damage(self, crit_roll: float) -> Tuple[float, bool]:
        """
        Calculate final damage dealt to a target unit.
        Includes crit chance and crit damage.
        """
        base_damage = self.get_attack()
        if crit_roll < self.base_stats.crit_rate:
            return int(base_damage * self.base_stats.crit_dmg), True
        return base_damage, False
    
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
    def __str__(self):
        """String representation of the unit."""
        return (f"({self.unit_type.value}, L{self.level}, "
                f"HP: {self.current_health:.1f}/{self.get_max_health()}, "
                f"Mana: {self.current_mana:.1f}/{self.base_stats.max_mana}), "
                f"Pos: {self.position}")
