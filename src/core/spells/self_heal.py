from typing import *
from typing import TYPE_CHECKING
if TYPE_CHECKING:
    from units import Unit

from .abstract import AbstractSpell
from damage import Damage


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
        else:
            print(f"Target {self.target.unit_type.value} is already defeated.")

    def description(self) -> str:
        return f"Heals the caster for {int(self.heal_amount * self.spell_power)} health."
    
    def update_level(self, new_level: int):
        self.heal_amount = 100 + (new_level - 1) * 50  # Increase heal by 50 per level
