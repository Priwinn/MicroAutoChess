# from numba.experimental import jitclass
# from src.core.units import Unit
from constant_types import DamageType
from damage import Damage
from typing import *
from typing import TYPE_CHECKING
if TYPE_CHECKING:
    from units import Unit

class AbstractSpell:
    def __init__(self, name: str):
        self.name = name
        self.spell_delay: int = 2 # Delay before the spell is executed, in frames
        self.ranged: bool = False
        self.spell_power: float = 1.0  # Multiplier for spell effects based on caster's stats
        self.target: Optional['Unit'] = None
        self.target_position: Optional[tuple] = None
        
    
    def prepare(self, source, board):
        """Prepare the spell for execution. Can be used to find the target of the spell."""
        raise NotImplementedError("Subclasses must implement this method")

    def execute(self, source, board, frame_number: int, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
        """Execute the spell on the target unit."""
        raise NotImplementedError("Subclasses must implement this method")

    def __str__(self):
        return f"{self.name} Spell"

    def description(self):
        return "No description available."


class FireballSpell(AbstractSpell):
    def __init__(self):
        super().__init__("Fireball")
        self.ranged = True
        self.damage = 250
        self.range = 5
        self.spell_delay: int = 2  # Delay before the spell is executed, in frames
        

    def prepare(self, source, board):
        self.target = source.current_target
        if self.target is None:
            return False
        return True


    def execute(self, source, board, frame_number: int, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
        """Execute the fireball spell. Deal damage to the target unit and  adjacent units take half damage."""

        if self.target and self.target.is_alive():
            damage = self.damage*source.base_stats.spell_power
            if can_crit and crit_roll < crit_rate:
                damage *= crit_dmg
            damage_obj = Damage(value=damage, crit=(can_crit and crit_roll < crit_rate), dmg_type=DamageType.MAGICAL, frame_number=frame_number)
            self.target.take_damage(damage_obj, source=source, spell_name=self.name)
            for adjacent in board.get_adjacent_cells(self.target.position):
                # TODO: deal with planned cells and movement
                # right now moving units are still in the original cell during spell execution
                if not adjacent.is_empty() and not adjacent.is_planned() \
                    and adjacent.unit.is_alive() and adjacent.unit != self.target and adjacent.unit.team == self.target.team:
                    damage_obj = Damage(value=damage/2, crit=(can_crit and crit_roll < crit_rate), dmg_type=DamageType.MAGICAL, frame_number=frame_number)
                    adjacent.unit.take_damage(damage_obj, source=source, spell_name=self.name)
        else:
            print(f"Target {self.target.unit_type.value} is already defeated.")

    def description(self):
        return f"Deals {self.damage * self.spell_power} magical damage to a target and {self.damage/2 * self.spell_power} damage to adjacent enemies."

class SpinSlashSpell(AbstractSpell):
    def __init__(self):
        super().__init__("Spin Slash")
        self.spell_delay: int = 2
        self.ranged = False
        self.damage = 100  # Base damage

    def prepare(self, source, board):
        self.target = source  # Target is self for area effect around the caster
        return True

    def execute(self, source, board, frame_number: int, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
        """Execute the spin slash spell."""
        for cell in board.get_adjacent_cells(source.position):
            if not cell.is_empty() and not cell.is_planned() and cell.unit.team != source.team and cell.unit.is_alive():
                damage = self.damage * source.base_stats.spell_power
                if can_crit and crit_roll < crit_rate:
                    damage *= crit_dmg
                damage_obj = Damage(value=damage, crit=(can_crit and crit_roll < crit_rate), dmg_type=DamageType.PHYSICAL, frame_number=frame_number)
                cell.unit.take_damage(damage_obj, source=source, spell_name=self.name)

    def description(self):
        return f"Deals {self.damage * self.spell_power} physical damage to all adjacent enemy units."

class SelfHealSpell(AbstractSpell):
    def __init__(self):
        super().__init__("Heal")
        self.spell_delay: int = 1
        self.ranged = False
        self.heal_amount = 100  # Base heal amount, can be modified by source's spell power

    def prepare(self, source, board):
        self.target = source
        return True

    def execute(self, source, board, frame_number: int, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
        """Execute the heal spell."""
        if self.target.is_alive():
            heal_amount = self.heal_amount * source.base_stats.spell_power
            damage_obj = Damage(value=heal_amount, crit=False, frame_number=frame_number, heal=True)
            source.heal(damage_obj, source=source, spell_name=self.name)
            # print(f"{source.unit_type.value} casted {self.name} on {source.unit_type.value} for {heal_amount} health.")
        else:
            print(f"Target {self.target.unit_type.value} is already defeated.")

    def description(self):
        return f"Heals the caster for {self.heal_amount * self.spell_power} health."

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
            valid_positions = [pos for pos in adjacent_positions if ((board.get_cell(pos).is_empty() and not board.get_cell(pos).is_planned()) or pos == source.position)]
            # Find the farthest valid position to the source
            valid_positions.sort(key=lambda pos: board.l1_distance(source.position, pos), reverse=True)
            if valid_positions:
                board.move_unit(source.position, valid_positions[0])
            else:
                #Try within 2 range of weakest enemy
                valid_positions = board.get_positions_in_l1_range(self.target.position, 2)
                valid_positions = [pos for pos in valid_positions if ((board.get_cell(pos).is_empty() and not board.get_cell(pos).is_planned()) or pos == source.position)]
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
    
    def description(self):
        return f"Blinks to the weakest enemy within {self.range} cells and deals {self.damage * self.spell_power} physical damage. Range increases by 1 each cast."


class AttackSpeedBuffSpell(AbstractSpell):
    def __init__(self):
        super().__init__("Attack Speed Buff")
        self.spell_delay: int = 1
        self.ranged = False
        self.buff_amount = 0.25  # 20% attack speed increase
        self.spell_power = 1.0  # This can be modified by the caster's stats

    def prepare(self, source, board):
        self.target = source
        return True

    def execute(self, source, board, frame_number: int, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
        """Execute the attack speed buff spell."""
        if self.target.is_alive():
            source.buffs["attack_speed"] = source.buffs.get("attack_speed", 1.0) + self.buff_amount
            # print(f"{source.unit_type.value} casted {self.name} on {target.unit_type.value}, increasing attack speed from {original_attack_speed} to {target.base_stats.attack_speed}.")
        else:
            print(f"Target {self.target.unit_type.value} is already defeated.")

    def description(self):
        return f"Increases the caster's attack speed by {self.buff_amount * 100}%. Stacks with multiple casts."
