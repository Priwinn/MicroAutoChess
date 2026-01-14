from dataclasses import dataclass
from typing import *
from constant_types import DamageType

# RAW UNMITIGATED DAMAGE INFORMATION
@dataclass
class Damage:
    value: float
    frame_number: int
    dmg_type: Optional[DamageType] = None
    source_unit_id: Optional[int] = None
    target_unit_id: Optional[int] = None
    spell_name: Optional[str] = None
    crit: bool = False
    dot: bool = False
    heal: bool = False
    match_id: Optional[int] = None
    