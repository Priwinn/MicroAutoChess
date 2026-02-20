from typing import *
from typing import TYPE_CHECKING
if TYPE_CHECKING:
    from units import Unit

from .abstract import AbstractSpell


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
        else:
            print(f"Target {self.target.unit_type.value} is already defeated.")

    def description(self) -> str:
        return f"Increases the caster's attack speed by {self.buff_amount * 100:.1f}%. Stacks with multiple casts."
    def on_hit_render_callback(self, source, board):
        # Return particle burst descriptor used by visualizer to spawn particles
        return {
            'type': 'particles',
            'num': 40,
            'base_color_hint': None,  # visualizer will map team -> color
            'lifetime_range': (0.7, 1.6),
            'size_range': (4, 7),
            'speed_mult_range': (0.8, 2.2),
        }
    
    def update_level(self, new_level: int):
        self.buff_amount = 0.25 + (new_level - 1) * 0.025  # Increase buff by 2.5% per level
