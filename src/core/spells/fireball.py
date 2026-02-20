from constant_types import DamageType
from damage import Damage
from typing import *
from typing import TYPE_CHECKING
if TYPE_CHECKING:
    from units import Unit

from .abstract import AbstractSpell


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

    def description(self) -> str:
        return f"Deals {int(self.damage * self.spell_power)} magical damage to a target and {int(self.damage/2 * self.spell_power)} damage to adjacent enemies."
    def projectile_render_callback(self, source, board):
        # Return a lightweight descriptor for visualizer to render a projectile
        return {
            'type': 'projectile',
            'id': 'Fireball',
            'color_hint': (200, 40, 40),
            'glow': True,
        }

    def on_hit_render_callback(self, source, board):
        # AoE on hit descriptor (hex radius 1)
        return {
            'type': 'aoe',
            'radius_hex': 1,
            'duration': 0.6,
            'color_hint': None,  # visualizer will prefer team color when provided
        }
    
    def update_level(self, new_level: int):
        self.damage = 250 + (new_level - 1) * 125  # Increase damage by 100 per level
