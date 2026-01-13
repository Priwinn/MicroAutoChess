# from numba.experimental import jitclass
# from src.core.units import Unit
from src.core.constant_types import DamageType
from typing import *



class AbstractSpell:
    def __init__(self, name: str):
        self.name = name
        self.spell_delay: int = 0 # Delay before the spell is executed, in frames
        self.ranged: bool = False
        self.target: Optional['Unit'] = None
        self.target_position: Optional[tuple] = None
        
    
    def prepare(self, source, board):
        """Prepare the spell for execution. Can be used to find the target of the spell."""
        raise NotImplementedError("Subclasses must implement this method")

    def execute(self, source, board, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
        """Execute the spell on the target unit."""
        raise NotImplementedError("Subclasses must implement this method")

    def __str__(self):
        return f"{self.name} Spell"


class FireballSpell(AbstractSpell):
    def __init__(self):
        super().__init__("Fireball")
        self.ranged = True
        self.damage = 100
        self.range = 5
        self.spell_delay: int = 2  # Delay before the spell is executed, in frames

    def prepare(self, source, board):
        self.target = source.current_target
        if self.target is None:
            return False
        return True


    def execute(self, source, board, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
        """Execute the fireball spell. Deal damage to the target unit and  adjacent units take half damage."""
        if self.target and self.target.is_alive():
            damage = self.damage*source.base_stats.spell_power
            if can_crit and crit_roll < crit_rate:
                damage *= crit_dmg
            self.target.take_damage(damage, DamageType.MAGICAL)
            for adjacent in board.get_adjacent_cells(self.target.position):
                # TODO: deal with planned cells and movement
                if not adjacent.is_empty() and not adjacent.is_planned() \
                    and adjacent.unit.is_alive() and adjacent.unit != self.target and adjacent.unit.team == self.target.team:
                    adjacent.unit.take_damage(damage/2, DamageType.MAGICAL)

            # print(f"Casted {self.name} on {target.unit_type.value} for {mitigated_damage} damage.")
        else:
            print(f"Target {self.target.unit_type.value} is already defeated.")

class SelfHealSpell(AbstractSpell):
    def __init__(self):
        super().__init__("Heal")
        self.spell_delay: int = 1
        self.ranged = False
        self.heal_amount = 100  # Base heal amount, can be modified by source's spell power

    def prepare(self, source, board):
        self.target = source
        return True

    def execute(self, source, board, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
        """Execute the heal spell."""
        if self.target.is_alive():
            heal_amount = self.heal_amount * source.base_stats.spell_power
            source.heal(heal_amount)
            # print(f"{source.unit_type.value} casted {self.name} on {source.unit_type.value} for {heal_amount} health.")
        else:
            print(f"Target {self.target.unit_type.value} is already defeated.")

class AssassinBlinkSpell(AbstractSpell):
    """Teleport the assassin to the weakest enemy unit within 2 cells (note range is l2, hexes is l1). Increase range each cast."""
    def __init__(self):
        super().__init__("Assassin Blink")
        self.spell_delay: int = 1
        self.ranged = True
        self.range = 3  # Maximum distance to blink
        self.damage = 50  # Base damage

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

    def execute(self, source, board, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
        """Execute the blink spell."""


        if self.target and self.target.is_alive():
            # Move the assassin to the cell adjacent to the weakest enemy that is farthest to the source
            adjacent_positions = board.get_adjacent_positions(self.target.position)
            valid_positions = [pos for pos in adjacent_positions if (board.get_cell(pos).is_empty() and not board.get_cell(pos).is_planned())]
            # Find the farthest valid position to the source
            valid_positions.sort(key=lambda pos: board.l1_distance(source.position, pos), reverse=True)
            if valid_positions:
                board.move_unit(source.position, valid_positions[0])
            else:
                #Try within 2 range of weakest enemy
                valid_positions = board.get_positions_in_l1_range(self.target.position, 2)
                valid_positions = [pos for pos in valid_positions if (board.get_cell(pos).is_empty() and not board.get_cell(pos).is_planned())]
                valid_positions.sort(key=lambda pos: board.l1_distance(source.position, pos))
                if valid_positions:
                    board.move_unit(source.position, valid_positions[0])
            # Deal damage to the weakest enemy
            damage = self.damage * source.base_stats.spell_power
            self.target.take_damage(damage, DamageType.PHYSICAL)
            #Change source's target to the weakest enemy
            source.current_target = self.target
            
        # print(f"{source.unit_type.value} blinked to {weakest_enemy.unit_type.value}'s position at {weakest_enemy.position}.")
        self.range += 1  # Increase range for next blink


class AttackSpeedBuffSpell(AbstractSpell):
    def __init__(self):
        super().__init__("Attack Speed Buff")
        self.spell_delay: int = 1
        self.ranged = False
        self.buff_amount = 0.2  # 20% attack speed increase

    def prepare(self, source, board):
        self.target = source
        return True

    def execute(self, source, board, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
        """Execute the attack speed buff spell."""
        if self.target.is_alive():
            source.buffs["attack_speed"] = source.buffs.get("attack_speed", 1.0) + self.buff_amount
            # print(f"{source.unit_type.value} casted {self.name} on {target.unit_type.value}, increasing attack speed from {original_attack_speed} to {target.base_stats.attack_speed}.")
        else:
            print(f"Target {self.target.unit_type.value} is already defeated.")
