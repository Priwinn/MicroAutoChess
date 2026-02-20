from .abstract import AbstractSpell
from .fireball import FireballSpell
from .spin_slash import SpinSlashSpell
from .self_heal import SelfHealSpell
from .assassin_blink import AssassinBlinkSpell
from .attack_speed_buff import AttackSpeedBuffSpell

# Simple factory to get spell instances by name for visualizer/event handling
_SPELL_CLASS_MAP = {
    'Fireball': FireballSpell,
    'Spin Slash': SpinSlashSpell,
    'Heal': SelfHealSpell,
    'Assassin Blink': AssassinBlinkSpell,
    'Attack Speed Buff': AttackSpeedBuffSpell,
}

def get_spell_instance_by_name(name: str):
    cls = _SPELL_CLASS_MAP.get(name)
    if cls is None:
        return None
    try:
        return cls()
    except Exception:
        return None

__all__ = [
    'AbstractSpell', 'FireballSpell', 'SpinSlashSpell', 'SelfHealSpell',
    'AssassinBlinkSpell', 'AttackSpeedBuffSpell', 'get_spell_instance_by_name'
]
