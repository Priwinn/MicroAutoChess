from dataclasses import dataclass
from typing import *
from typing import TYPE_CHECKING
from constant_types import CombatAction, CombatEventType

# Avoid importing `Unit` at runtime to prevent circular imports.
# Use TYPE_CHECKING so that the import is only for type checkers/static analysis.
if TYPE_CHECKING:
    from units import Unit

@dataclass
class CombatEvent:
    """Represents an event during combat."""
    frame_number: int
    source: Optional['Unit'] = None
    target: Optional['Unit'] = None
    event_type: CombatEventType = CombatEventType.OTHER_EVENT
    spell_name: Optional[str] = None
    damage: float = 0
    crit_bool: bool = False
    position: Optional[Tuple[int, int]] = None
    description: str = ""
    match_id: Optional[int] = None