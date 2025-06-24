# from numba.experimental import jitclass
from src.core.constant_types import DamageType



class AbstractSpell:
    def __init__(self, name: str):
        self.name = name
        self.spell_delay: int = 0 # Delay before the spell is executed, in frames
        self.ranged: bool = False

    def execute(self, source, target, board, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
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

    def execute(self, source, target, board, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
        """Execute the fireball spell. Deal damage to the target unit and  adjacent units take half damage."""
        if target.is_alive():
            damage = self.damage*source.base_stats.spell_power
            if can_crit and crit_roll < crit_rate:
                damage *= crit_dmg
            target.take_damage(damage, DamageType.MAGICAL)
            for adjacent in board.get_adjacent_cells(target.position):
                # TODO: deal with planned cells and movement
                if not adjacent.is_empty() and not adjacent.is_planned() \
                    and adjacent.unit.is_alive() and adjacent.unit != target and adjacent.unit.team == target.team:
                    adjacent.unit.take_damage(damage/2, DamageType.MAGICAL)

            # print(f"Casted {self.name} on {target.unit_type.value} for {mitigated_damage} damage.")
        else:
            print(f"Target {target.unit_type.value} is already defeated.")

class SelfHealSpell(AbstractSpell):
    def __init__(self):
        super().__init__("Heal")
        self.spell_delay: int = 1
        self.ranged = False
        self.heal_amount = 100  # Base heal amount, can be modified by source's spell power

    def execute(self, source, target, board, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
        """Execute the heal spell."""
        if target.is_alive():
            heal_amount = self.heal_amount * source.base_stats.spell_power
            source.heal(heal_amount)
            # print(f"{source.unit_type.value} casted {self.name} on {source.unit_type.value} for {heal_amount} health.")
        else:
            print(f"Target {target.unit_type.value} is already defeated.")

class AssassinBlinkSpell(AbstractSpell):
    """Teleport the assassin to the weakest enemy unit within 2 cells (note range is l2, hexes is l1). Increase range each cast."""
    def __init__(self):
        super().__init__("Assassin Blink")
        self.spell_delay: int = 1
        self.ranged = True
        self.range = 3  # Maximum distance to blink
        self.damage = 50  # Base damage

    def execute(self, source, target, board, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
        """Execute the blink spell."""
        # Find the weakest enemy unit within range
        weakest_enemy = None
        weakest_health = float('inf')
        for cell in board.get_cells_in_l1_range(target.position, self.range):
            if not cell.is_empty() and not cell.is_planned() and cell.unit.team != source.team and cell.unit.is_alive():
                if cell.unit.current_health < weakest_health:
                    weakest_health = cell.unit.current_health
                    weakest_enemy = cell.unit

        if weakest_enemy:
            # Move the assassin to the cell adjacent to the weakest enemy that is farthest to the source
            adjacent_positions = board.get_adjacent_positions(weakest_enemy.position)
            valid_positions = [pos for pos in adjacent_positions if board.get_cell(pos).is_empty()]
            # Find the farthest valid position to the source
            valid_positions.sort(key=lambda pos: board.l1_distance(source.position, pos), reverse=True)
            if valid_positions:
                board.move_unit(source.position, valid_positions[0])
            else:
                #Try within 2 range of weakest enemy
                valid_positions = board.get_positions_in_l1_range(weakest_enemy.position, 2)
                valid_positions = [pos for pos in valid_positions if board.get_cell(pos).is_empty()]
                valid_positions.sort(key=lambda pos: board.l1_distance(source.position, pos))
                if valid_positions:
                    board.move_unit(source.position, valid_positions[0])
            # Deal damage to the weakest enemy
            damage = self.damage * source.base_stats.spell_power
            weakest_enemy.take_damage(damage, DamageType.PHYSICAL)
            #Change source's target to the weakest enemy
            source.current_target = weakest_enemy
            
        # print(f"{source.unit_type.value} blinked to {weakest_enemy.unit_type.value}'s position at {weakest_enemy.position}.")
        self.range += 1  # Increase range for next blink


class AttackSpeedBuffSpell(AbstractSpell):
    def __init__(self):
        super().__init__("Attack Speed Buff")
        self.spell_delay: int = 1
        self.ranged = False
        self.buff_amount = 0.2  # 20% attack speed increase

    def execute(self, source, target, board, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
        """Execute the attack speed buff spell."""
        if target.is_alive():
            source.buffs["attack_speed"] = source.buffs.get("attack_speed", 1.0) + self.buff_amount
            # print(f"{source.unit_type.value} casted {self.name} on {target.unit_type.value}, increasing attack speed from {original_attack_speed} to {target.base_stats.attack_speed}.")
        else:
            print(f"Target {target.unit_type.value} is already defeated.")
