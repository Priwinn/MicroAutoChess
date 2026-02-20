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
        self.projectile_animation: bool = False
        self.on_hit_animation: bool = False
    
    def prepare(self, source, board):
        """Prepare the spell for execution. Can be used to find the target of the spell."""
        raise NotImplementedError("Subclasses must implement this method")

    def execute(self, source, board, frame_number: int, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
        """Execute the spell on the target unit."""
        raise NotImplementedError("Subclasses must implement this method")

    def __str__(self):
        return f"{self.name} Spell"

    def description(self) -> str:
        return "No description available."
    
    def round_reset(self):
        """Reset any spell-specific state if needed."""
        self.target = None
        self.target_position = None

    def projectile_render_callback(self, source, board) -> Optional[Dict[str, Any]]:
        """Callback for rendering projectile animation if applicable."""
        return None

    def on_hit_render_callback(self, source, board) -> Optional[Dict[str, Any]]:
        """Callback for rendering on-hit animation if applicable."""
        return None
    
    def update_level(self, new_level: int):
        """Update spell attributes based on the new level of the caster."""
        # This method can be overridden by specific spells to scale with caster level
        pass
