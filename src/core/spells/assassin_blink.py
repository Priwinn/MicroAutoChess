from typing import *
from typing import TYPE_CHECKING
if TYPE_CHECKING:
    from units import Unit

from .abstract import AbstractSpell
from constant_types import DamageType
from damage import Damage


class AssassinBlinkSpell(AbstractSpell):
    """Teleport the assassin to the weakest enemy unit within 2 cells (note range is l2, hexes is l1). Increase range each cast."""
    def __init__(self):
        super().__init__("Assassin Blink")
        self.spell_delay: int = 3
        self.ranged = True
        self.range = 3  # Maximum distance to blink
        self.damage = 100  # Base damage

    def prepare(self, source, board):
        # Find the weakest enemy unit within range
        weakest_enemy = None
        weakest_health = float('inf')
        for cell in board.get_cells_in_l1_range(source.position, self.range):
            if not cell.is_empty() and not cell.is_planned() and cell.unit.team != source.team and cell.unit.is_alive():
                if cell.unit.current_health < weakest_health:
                    weakest_health = cell.unit.current_health
                    weakest_enemy = cell.unit
        self.target = weakest_enemy
        return weakest_enemy is not None

    def execute(self, source, board, frame_number: int, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
        """Execute the blink spell."""


        if self.target and self.target.is_alive():
            # Move the assassin to the cell adjacent to the weakest enemy that is farthest to the source
            adjacent_positions = board.get_adjacent_positions(self.target.position)
            valid_positions = [pos for pos in adjacent_positions if ((board.cells[pos].is_empty() and not board.cells[pos].is_planned()) or pos == source.position)]
            # Find the farthest valid position to the source
            valid_positions.sort(key=lambda pos: board.l1_distance(source.position, pos), reverse=True)
            if valid_positions:
                board.move_unit(source.position, valid_positions[0])
            else:
                #Try within 2 range of weakest enemy
                valid_positions = board.get_positions_in_l1_range(self.target.position, 2)
                valid_positions = [pos for pos in valid_positions if ((board.cells[pos].is_empty() and not board.cells[pos].is_planned()) or pos == source.position)]
                valid_positions.sort(key=lambda pos: board.l1_distance(source.position, pos), reverse=True)
                if valid_positions:
                    board.move_unit(source.position, valid_positions[0])
            # Deal damage to the weakest enemy
            damage = self.damage * source.base_stats.spell_power
            damage_obj = Damage(value=damage, crit=False, dmg_type=DamageType.PHYSICAL, frame_number=frame_number)
            self.target.take_damage(damage_obj, source=source, spell_name=self.name)
            #Change source's target to the weakest enemy
            source.current_target = self.target
            
        # print(f"{source.unit_type.value} blinked to {weakest_enemy.unit_type.value}'s position at {weakest_enemy.position}.")
        self.range += 1  # Increase range for next blink
    
    def description(self) -> str:
        return f"Blinks to the weakest enemy within {self.range} cells and deals {int(self.damage * self.spell_power)} physical damage. Range increases by 1 each cast."

    def round_reset(self):
        """Reset any spell-specific state if needed."""
        super().round_reset()
        self.range = 3  # Reset range to initial value

    def update_level(self, new_level: int):
        self.damage = 100 + (new_level - 1) * 50  # Increase damage by 50 per level
