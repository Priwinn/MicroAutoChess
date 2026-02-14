"""
Player representation and state management.
"""

import numpy as np
from typing import List, Dict, Optional, Tuple
from dataclasses import dataclass, field
from game_params import GameParams

from units import Unit, UnitType, UnitRarity



class Player:
    """
    Represents a player in the auto chess game.
    """
    health: int = 100
    gold: int = 10
    level: int = 1
    experience: int = 0
    max_units_on_board: int = 1


    
    # Shop
    shop_units: List[Unit] = field(default_factory=list)
    rerolls_this_turn: int = 0
    
    
    def __init__(self, player_id: int, game_params:GameParams = GameParams()) -> None:
        self.player_id = player_id
        self.game_params = game_params
        self.bench: Dict[int, Optional[Unit]] = {i:None for i in range(game_params.bench_size)}
        self.units_on_board: Dict[Tuple[int, int], Optional[Unit]] = {(i,j): None for i in range(game_params.board_size[0]) for j in range(game_params.board_size[1])}
        

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
        
        if len([u for u in self.bench.values() if u is not None]) >= 9:  # Max bench size
            return False
        
        self.gold -= cost
        self.add_unit_to_bench(unit)
        self.shop_units[shop_index] = None  # Remove from shop
        return True
    
    def sell_unit(self, unit: Unit) -> bool:
        """Sell unit for gold."""
        # Try remove from bench
        for k, v in list(self.bench.items()):
            if v is unit:
                self.bench[k] = None
                self.gold += unit.get_sell_value()
                return True

        # Try remove from board (units_on_board is a dict[position->unit])
        for k, v in list(self.units_on_board.items()):
            if v is unit:
                self.units_on_board[k] = None
                self.gold += unit.get_sell_value()
                return True

        return False
    
    def add_unit_to_bench(self, unit: Unit) -> bool:
        """Add unit to bench if space is available."""
        for i in sorted(self.bench.keys()):
            if self.bench[i] is None:
                unit.position = (-self.player_id, i)  
                self.bench[i] = unit
                
                return True
        return False
    
    def add_unit(self, unit: Unit) -> bool:
        """Add unit to bench or board."""
        # If there's space on the bench, add there
        if len([u for u in self.bench.values() if u is not None]) < len(self.bench):
            self.add_unit_to_bench(unit)
            if self.game_params.unit_level_up:
                self.check_unit_level_up()
            return True

        # Otherwise, try to place on the board into the first available position
        if len([u for u in self.units_on_board.values() if u is not None]) < self.max_units_on_board:
            i = 0
            while True:
                pos = (i, 0)
                if pos not in self.units_on_board:
                    unit.position = pos
                    self.units_on_board[pos] = unit
                    if self.game_params.unit_level_up:
                        self.check_unit_level_up()
                    return True
                i += 1
        return False
    
    def remove_unit(self, unit: Unit) -> bool:
        """Remove unit from bench or board."""
        for i in sorted(self.bench.keys()):
            if self.bench[i] is unit:
                self.bench[i] = None
                return True

        for pos, u in list(self.units_on_board.items()):
            if u is unit:
                # remove the mapping
                del self.units_on_board[pos]
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

    def check_unit_level_up(self):
        """Check if a unit can level up (combine 3 of the same unit)."""
        unit_counts: Dict[str, List[Unit]] = {}
        
        # Count units on bench and board
        for unit in list(self.bench.values()) + list(self.units_on_board.values()):
            if unit is None:
                continue
            key = f"{unit.unit_type}_{unit.rarity}"
            if key not in unit_counts:
                unit_counts[key] = []
            unit_counts[key].append(unit)
        
        # Check for level up opportunities
        for key, units in unit_counts.items():
            if len(units) >= 3:
                # Level up the first unit and remove 2 others. TODO: Handle permanent stats (e.g. gain AD per kill)
                leveled_up_unit = units[0].clone()
                leveled_up_unit.level_up()
                for i in range(3):
                    self.remove_unit(units[i])
                self.add_unit(leveled_up_unit)

    
    def take_damage(self, damage: int):
        """Take damage to health."""
        self.health = max(0, self.health - damage)
    
    def is_alive(self) -> bool:
        """Check if player is still alive."""
        return self.health > 0
    
    def get_total_unit_count(self) -> int:
        """Get total number of units owned."""
        return len([u for u in self.bench.values() if u is not None]) + len([u for u in self.units_on_board.values() if u is not None])
    
    def to_array(self) -> np.ndarray:
        """Convert player state to numerical array."""
        return np.array([
            self.health / 100.0,  # Normalized health
            self.gold / 100.0,    # Normalized gold
            self.level / 10.0,    # Normalized level
            self.experience / 20.0,  # Normalized experience
            len([u for u in self.bench.values() if u is not None]) / max(1, len(self.bench)),   # Bench usage
            len([u for u in self.units_on_board.values() if u is not None]) / max(1, max(1, len(self.units_on_board))),  # Board usage
            self.rerolls_this_turn / 10.0,   # Rerolls
        ], dtype=np.float32)

    def set_units_on_board_from_list(self, units: List[Unit]) -> None:
        """Populate `units_on_board` from a list. Fills positions starting at 0."""
        # Clear existing
        self.units_on_board.clear()
        for i, unit in enumerate(units):
            if unit is None:
                continue
            if hasattr(unit, 'position') and isinstance(unit.position, tuple) and len(unit.position) == 2 and unit.position is not None:
                pos = unit.position
            else:
                raise ValueError(f"Unit {unit} does not have a valid position attribute for board placement.")
            self.units_on_board[pos] = unit

    def get_units_on_board_list(self) -> List[Unit]:
        """Return the units on board as a list ordered by position (excluding empty slots)."""
        return [u for _, u in sorted(self.units_on_board.items(), key=lambda kv: (kv[0][0], kv[0][1])) if u is not None]
    

    
    def clone(self) -> 'Player':
        """Create a deep copy of the player."""
        cloned = Player()
        cloned.player_id = self.player_id
        cloned.health = self.health
        cloned.gold = self.gold
        cloned.level = self.level
        cloned.experience = self.experience
        cloned.max_units_on_board = self.max_units_on_board
        cloned.rerolls_this_turn = self.rerolls_this_turn

        # Clone bench (dict)
        cloned.bench = {k: (v.clone() if v else None) for k, v in self.bench.items()}

        # Clone units_on_board (dict)
        cloned.units_on_board = {k: (v.clone() if v else None) for k, v in self.units_on_board.items()}

        cloned.shop_units = [unit.clone() if unit else None for unit in self.shop_units]

        return cloned