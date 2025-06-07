class AbstractSpell:
    def __init__(self, name: str):
        self.name = name

    def execute(self, target, board, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
        """Execute the spell on the target unit."""
        raise NotImplementedError("Subclasses must implement this method")

    def __str__(self):
        return f"{self.name} Spell"


class FireballSpell(AbstractSpell):
    def __init__(self):
        super().__init__("Fireball")

    def execute(self, target, board, crit_rate: float = 0.0, crit_dmg: float = 0.0, can_crit: bool = False, crit_roll: float = 0.0):
        """Execute the fireball spell."""
        if target.is_alive():
            damage = 20
            if can_crit and crit_roll < crit_rate:
                damage *= crit_dmg
            mitigated_damage = target.take_damage(damage)
            print(f"Casted {self.name} on {target.unit_type.value} for {mitigated_damage} damage.")
        else:
            print(f"Target {target.unit_type.value} is already defeated.")

