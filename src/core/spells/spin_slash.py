from typing import *
from typing import TYPE_CHECKING
if TYPE_CHECKING:
    from units import Unit

from .abstract import AbstractSpell
from damage import Damage
from constant_types import DamageType


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

    def description(self) -> str:
        return f"Deals {int(self.damage * self.spell_power)} physical damage to all adjacent enemy units."

    def on_hit_render_callback(self, source, board) -> Optional[Dict[str, Any]]:
        # Spin slash also produces an AoE effect around caster
        return {
            'type': 'aoe',
            'radius_hex': 1,
            'duration': 0.45,
            'color_hint': None,
        }
    
    def update_level(self, new_level: int):
        self.damage = 100 + (new_level - 1) * 50  # Increase damage by 50 per level
