"""
Player representation and state management.
"""

import numpy as np
from typing import List, Dict, Optional
from dataclasses import dataclass, field

from .units import Unit, UnitType, UnitRarity


@dataclass
class Player:
    """
    Represents a player in the auto chess game.
    """
    player_id: int
    health: int = 100
    gold: int = 10
    level: int = 1
    experience: int = 0
    
    # Units
    bench: List[Unit] = field(default_factory=list)
    units_on_board: List[Unit] = field(default_factory=list)
    
    # Shop
    shop_units: List[Unit] = field(default_factory=list)
    rerolls_this_turn: int = 0
    
    def __post_init__(self):
        """Initialize player with starting shop."""
        if not self.shop_units:
            self._generate_shop()
    
    def _generate_shop(self):
        """Generate shop units based on player level."""
        self.shop_units = []
        for _ in range(5):  # 5 shop slots
            unit_type = np.random.choice(list(UnitType))
            rarity = self._get_random_rarity()
            unit = Unit(unit_type=unit_type, rarity=rarity)
            self.shop_units.append(unit)
    
    def _get_random_rarity(self) -> UnitRarity:
        """Get random unit rarity based on player level."""
        # Higher level = better chance for rare units
        probabilities = {
            1: [0.6, 0.3, 0.1, 0.0, 0.0],  # Common, Uncommon, Rare, Epic, Legendary
            2: [0.5, 0.35, 0.13, 0.02, 0.0],
            3: [0.4, 0.35, 0.2, 0.05, 0.0],
            4: [0.3, 0.3, 0.25, 0.13, 0.02],
            5: [0.2, 0.25, 0.25, 0.25, 0.05]
        }
        
        level_probs = probabilities.get(min(self.level, 5), probabilities[5])
        rarity_index = np.random.choice(5, p=level_probs)
        return list(UnitRarity)[rarity_index]
    
    def buy_unit(self, shop_index: int) -> bool:
        """Buy unit from shop."""
        if shop_index >= len(self.shop_units):
            return False
        
        unit = self.shop_units[shop_index]
        cost = unit.get_cost()
        
        if self.gold < cost:
            return False
        
        if len(self.bench) >= 8:  # Max bench size
            return False
        
        self.gold -= cost
        self.bench.append(unit)
        self.shop_units[shop_index] = None  # Remove from shop
        return True
    
    def sell_unit(self, unit: Unit) -> bool:
        """Sell unit for gold."""
        if unit in self.bench:
            self.bench.remove(unit)
            self.gold += unit.get_sell_value()
            return True
        elif unit in self.units_on_board:
            self.units_on_board.remove(unit)
            self.gold += unit.get_sell_value()
            return True
        return False
    
    def reroll_shop(self) -> bool:
        """Reroll shop for new units."""
        reroll_cost = 2
        if self.gold < reroll_cost:
            return False
        
        self.gold -= reroll_cost
        self.rerolls_this_turn += 1
        self._generate_shop()
        return True
    
    def gain_experience(self, amount: int):
        """Gain experience and potentially level up."""
        self.experience += amount
        exp_needed = self.level * 2  # Simple exp formula
        
        if self.experience >= exp_needed:
            self.level += 1
            self.experience -= exp_needed
    
    def take_damage(self, damage: int):
        """Take damage to health."""
        self.health = max(0, self.health - damage)
    
    def is_alive(self) -> bool:
        """Check if player is still alive."""
        return self.health > 0
    
    def get_total_unit_count(self) -> int:
        """Get total number of units owned."""
        return len(self.bench) + len(self.units_on_board)
    
    def to_array(self) -> np.ndarray:
        """Convert player state to numerical array."""
        return np.array([
            self.health / 100.0,  # Normalized health
            self.gold / 100.0,    # Normalized gold
            self.level / 10.0,    # Normalized level
            self.experience / 20.0,  # Normalized experience
            len(self.bench) / 8.0,   # Bench usage
            len(self.units_on_board) / 8.0,  # Board usage
            self.rerolls_this_turn / 10.0,   # Rerolls
        ], dtype=np.float32)
    
    def clone(self) -> 'Player':
        """Create a deep copy of the player."""
        cloned = Player(
            player_id=self.player_id,
            health=self.health,
            gold=self.gold,
            level=self.level,
            experience=self.experience,
            rerolls_this_turn=self.rerolls_this_turn
        )
        
        # Clone units
        cloned.bench = [unit.clone() for unit in self.bench]
        cloned.units_on_board = [unit.clone() for unit in self.units_on_board]
        cloned.shop_units = [unit.clone() if unit else None for unit in self.shop_units]
        
        return cloned