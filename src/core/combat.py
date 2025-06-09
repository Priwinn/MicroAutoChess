"""
Combat simulation engine for auto chess battles.
"""

import numpy as np
from typing import List, Tuple, Dict, Optional, Set
from dataclasses import dataclass
from enum import Enum
import random
import math
# from numba import jit
# from numba.experimental import jitclass

from src.core.board import Board
from src.core.units import Unit, UnitType
# from src.core.player import Player
# from src.core.spells import AbstractSpell, FireballSpell, SelfHealSpell


class CombatAction(Enum):
    MOVE = "move"
    ATTACK = "attack"
    CAST_SPELL = "cast_spell"
    WAIT = "wait"


@dataclass
class CombatEvent:
    """Represents an event during combat."""
    round_number: int
    attacker: Optional[Unit]
    target: Optional[Unit]
    action: CombatAction
    damage: float = 0
    position: Optional[Tuple[int, int]] = None
    description: str = ""


@dataclass
class PlannedAction:
    """Represents a planned action for a unit."""
    unit: Unit
    action_type: CombatAction
    target: Optional[Unit] = None
    target_position: Optional[Tuple[int, int]] = None
    resolution_round: int = 0  # When this action will execute
    planned_round: int = 0     # When this action was planned
    description: str = ""


@dataclass
class ActionTiming:
    """Configuration for how long different actions take to resolve. This is to account for animation and travel times."""
    attack_delay: float = 10      # Rounds until attack resolves
    move_delay: float = 4        # Rounds until movement resolves
    spell_delay: float = 10       # Rounds until spell resolves
    wait_delay: float = 2         # Rounds until wait resolves

    def get_delay(self, action_type: CombatAction) -> float:
        """Get the delay for a specific action type."""
        if action_type == CombatAction.ATTACK:
            return self.attack_delay
        elif action_type == CombatAction.MOVE:
            return self.move_delay
        elif action_type == CombatAction.CAST_SPELL:
            return self.spell_delay
        elif action_type == CombatAction.WAIT:
            return self.wait_delay
        


class CombatEngine:
    """
    Handles combat simulation between two teams of units.
    Actions are planned and then executed after their resolution delay.
    """
    
    def __init__(self, board: Board, combat_seed: Optional[int] = None, action_timing: Optional[ActionTiming] = None):
        self.board = board
        self.combat_log: List[CombatEvent] = []
        self.round_number = 0
        self.max_rounds = 500  # Prevent infinite combat
        
        # Action timing configuration
        self.action_timing = action_timing or ActionTiming()
        
        # Queue for delayed actions
        self.action_queue: List[PlannedAction] = []
        
        # Combat seed for deterministic behavior
        self.combat_seed = combat_seed
        self.rng = random.Random(combat_seed) if combat_seed is not None else random.Random()

    def set_teams(self, team1: List[Unit], team2: List[Unit]):
        """
        Set the teams for combat.
        Each team is a list of Unit objects.
        """
        if not team1 or not team2:
            raise ValueError("Both teams must have at least one unit.")
        
        # Assign teams to units
        for unit in team1:
            unit.team = 1
        for unit in team2:
            unit.team = 2    
        return team1, team2
    
    def simulate_combat(self, team1_units: List[Unit], team2_units: List[Unit]) -> int:
        """
        Simulate combat between two teams.
        Returns winning team (1 or 2) or 0 for draw.
        """
        # Set up teams
        team1_units, team2_units = self.set_teams(team1_units, team2_units)
        
        self.combat_log.clear()
        self.action_queue.clear()
        self.round_number = 0
        
        # Log combat start with seed info
        if self.combat_seed is not None:
            start_event = CombatEvent(
                round_number=0,
                attacker=None,
                target=None,
                action=CombatAction.WAIT,
                description=f"Combat started with seed: {self.combat_seed}, Action timing: {self.action_timing}"
            )
            self.combat_log.append(start_event)
        
        # Combat loop with delayed actions
        while self.round_number < self.max_rounds:
            self.round_number += 1
            
            # Get all living units
            all_units = [u for u in team1_units + team2_units if u.is_alive()]
            
            if not all_units:
                break
            
            # Check win conditions
            team1_alive = any(u.is_alive() and u.team == 1 for u in all_units)
            team2_alive = any(u.is_alive() and u.team == 2 for u in all_units)
            
            if not team1_alive:
                return 2  # Team 2 wins
            if not team2_alive:
                return 1  # Team 1 wins
            
            # Execute round with delayed actions
            self._execute_delayed_round(all_units)
        
        # Timeout - determine winner by remaining health
        team1_health = sum(u.current_health for u in team1_units if u.is_alive())
        team2_health = sum(u.current_health for u in team2_units if u.is_alive())
        
        if team1_health > team2_health:
            return 1
        elif team2_health > team1_health:
            return 2
        else:
            return 0  # Draw
            

    def _execute_delayed_round(self, all_units: List[Unit]):
        """
        Execute one round with delayed action resolution.
        """
        # Phase 1: Execute actions that are ready this round
        self._execute_queued_actions()
        
        # Phase 2: Plan actions for living units
        self._plan_actions(all_units)
        
        # Phase 3: Clean up dead units
        self._cleanup_dead_units(all_units)
    
    def _execute_queued_actions(self):
        """Execute all actions that are scheduled to resolve this round."""
        # Get actions that should resolve this round
        actions_to_execute = [action for action in self.action_queue 
                            if action.resolution_round <= self.round_number]
        
        # Remove executed actions from queue
        self.action_queue = [action for action in self.action_queue 
                           if action.resolution_round > self.round_number]
        
        if not actions_to_execute:
            return
        
        # Group actions by type for simultaneous execution
        attack_actions = [a for a in actions_to_execute if a.action_type == CombatAction.ATTACK]
        move_actions = [a for a in actions_to_execute if a.action_type == CombatAction.MOVE]
        spell_actions = [a for a in actions_to_execute if a.action_type == CombatAction.CAST_SPELL]
        
        # Execute in order: attacks, spells, then moves
        if attack_actions:
            self._execute_attacks_simultaneously(attack_actions)
        if spell_actions:
            self._execute_spells_simultaneously(spell_actions)
        if move_actions:
            self._execute_moves_simultaneously(move_actions)
    
    def _plan_actions(self, all_units: List[Unit]):
        """Plan new actions for units that don't have pending actions."""
        position_conflicts: Dict[Tuple[int, int], List[PlannedAction]] = {}

        for unit in all_units:
            if not unit.is_alive():
                continue
            
            # Check if unit already has a pending action
            has_pending_action = any(action.unit == unit for action in self.action_queue)
            
            if not has_pending_action:
                # Plan new action
                action = self._plan_unit_action(unit, all_units)
                if action and action.action_type != CombatAction.WAIT:
                    # Set timing for the action
                    delay = self.action_timing.get_delay(action.action_type)
                    if action.action_type == CombatAction.ATTACK:
                        initiative_needed = (delay - unit.basic_attack_overflow)
                        rounds_to_wait = initiative_needed / unit.get_attack_speed()
                        rounded_rounds = math.ceil(rounds_to_wait)
                        unit.basic_attack_overflow = (rounded_rounds * unit.get_attack_speed() - initiative_needed)
                        action.resolution_round = self.round_number + rounded_rounds
                    elif action.action_type == CombatAction.CAST_SPELL:
                        action.resolution_round = self.round_number + action.unit.base_stats.spell.spell_delay
                    else:
                        action.resolution_round = int(self.round_number + delay)

                    action.planned_round = self.round_number
                    
                    # Add to conflict map if it's a move action otherwise add to queue
                    if action.action_type == CombatAction.MOVE and action.target_position:
                        pos = action.target_position
                        if pos not in position_conflicts:
                            position_conflicts[pos] = []
                        position_conflicts[pos].append(action)
                    else:
                        self.action_queue.append(action)
                    
                    # Log action planning
                    plan_event = CombatEvent(
                        round_number=self.round_number,
                        attacker=action.unit,
                        target=action.target,
                        action=action.action_type,
                        description=f"{action.unit.unit_type.value} plans {action.action_type.value} to {action.target_position if action.target_position else action.target} (resolves in round {action.resolution_round})"
                    )
                    self.combat_log.append(plan_event)
        # Resolve movement conflicts after all actions are planned
        for pos, actions in position_conflicts.items():
            if len(actions) > 1:
                # Randomly select one action to keep
                chosen_action = self.rng.choice(actions)
                self.action_queue.append(chosen_action)
                self.board.set_planned(chosen_action.target_position)
                # Set other actions to WAIT
                for action in actions:
                    if action != chosen_action:
                        action.action_type = CombatAction.WAIT
                        action.resolution_round = self.round_number + int(self.action_timing.get_delay(CombatAction.MOVE)) # Set to wait to same delay as move so that it is fair (if wait was shorter, it would be unfair to the unit that is moving)
                        self.action_queue.append(action)
                        # Log the conflict resolution
                        conflict_event = CombatEvent(
                            round_number=self.round_number,
                            attacker=action.unit,
                            target=None,
                            action=CombatAction.WAIT,
                            description=f"Movement plan conflict at {pos}: {action.unit.unit_type.value} was set to WAIT."
                        )
                        self.combat_log.append(conflict_event)
                
                # Log conflict resolution
                conflict_event = CombatEvent(
                    round_number=self.round_number,
                    attacker=None,
                    target=None,
                    action=CombatAction.MOVE,
                    position=pos,
                    description=f"Movement plan conflict at {pos}: {chosen_action.unit.unit_type.value} was randomly chosen to be resolved."
                )
                self.combat_log.append(conflict_event)
            else:
                # No conflict, just add the single action
                self.action_queue.append(actions[0])
            




    def _plan_unit_action(self, unit: Unit, all_units: List[Unit]) -> Optional[PlannedAction]:
        """Plan what action a unit will take this round."""
        # Find enemies
        enemies = [u for u in all_units if u.is_alive() and u.team != unit.team]
        
        if not enemies or not unit.position:
            return PlannedAction(unit, CombatAction.WAIT)
        
        # If the unit has an unranged spell and enough mana, it casts it.
        if unit.current_mana >= unit.base_stats.max_mana and unit.base_stats.spell.ranged == False:
            unit.current_mana -= unit.base_stats.max_mana
            return PlannedAction(
                unit=unit,
                action_type=CombatAction.CAST_SPELL,
                target=unit #Right now non ranged spells target self, but this can be changed later
            )

        
        # Find target (closest enemy). 
        # TODO: Need to store the target to avoid re-targeting unless necessary.
        target = self._find_target(unit, enemies)
        
        if not target or not target.position:
            return PlannedAction(unit, CombatAction.WAIT)


        # Calculate distance to target
        distance = self.board.l2_distance(unit.position, target.position)

        # If the unit has a ranged spell and enough mana, it can cast it if the target is within spell range
        if unit.current_mana >= unit.base_stats.max_mana and unit.base_stats.spell.ranged and distance <= unit.base_stats.spell.range + 0.01:
            # If the unit has enough mana and a ranged spell, it can cast it
            # Plan spell cast
            unit.current_mana -= unit.base_stats.max_mana
            return PlannedAction(
                unit=unit,
                action_type=CombatAction.CAST_SPELL,
                target=target
            )

        if distance <= unit.base_stats.range + 0.66:  # Adding 0.66 because (0,0) 4 range reaches (3,4)  in that other game. It also reaches (0,5) and (1,5) but not (2,5), like the other game. Also ensures 1 range units only reach adjacent cells.
            # In range - plan attack
            return PlannedAction(
                unit=unit,
                action_type=CombatAction.ATTACK,
                target=target
            )
        else:
            # Out of range - plan movement
            target_position = self._plan_movement(unit, target)
            if target_position == unit.position:
                # No movement needed or no valid path
                return PlannedAction(unit, CombatAction.WAIT)
            else:
                # Move towards target
                return PlannedAction(
                    unit=unit,
                    action_type=CombatAction.MOVE,
                    target_position=target_position
                )
            
    def _plan_movement(self, unit: Unit, target: Unit) -> Optional[Tuple[int, int]]:
        """Plan where the unit should move to get closer to target."""
        if not unit.position or not target.position:
            return None
        
        # Find path towards target
        path = self.board.find_path(unit.position, target.position)
        
        if path:
            new_position = path[1]
        else:
            new_position = unit.position

        return new_position
                
    
    def _execute_attacks_simultaneously(self, attack_actions: List[PlannedAction]):
        """Execute all planned attacks simultaneously."""
        # Store units that are alive at the start of the round, if a unit is killed during the round, it should still be able to attack (simultaneous attacks)
        alive_units: List[bool] = [action.unit.is_alive() for action in attack_actions]
        for alive, action in zip(alive_units, attack_actions):
            if not alive or not action.target or not action.target.is_alive():
                # Log failed attack
                event = CombatEvent(
                    round_number=self.round_number,
                    attacker=action.unit,
                    target=action.target,
                    action=CombatAction.ATTACK,
                    description=f"Attack from {action.unit.unit_type.value} fails (target dead)" #TODO: might want to retarget to a different unit if possible (see fizzling)
                )
                self.combat_log.append(event)
                continue

            # Add mana from basic attack
            action.unit.add_basic_attack_mana()
            # Apply damage to the target
            actual_damage = action.target.take_damage(action.unit.get_basic_final_damage(self.rng.random()))
            
            # Log each attack separately
            event = CombatEvent(
                round_number=self.round_number,
                attacker=action.unit,
                target=action.target,
                action=CombatAction.ATTACK,
                damage=actual_damage,
                description=f"{action.unit.unit_type.value} deals {actual_damage} damage to {action.target.unit_type.value} (planned in round {action.planned_round})"
            )
            self.combat_log.append(event)  
    
    def _execute_spells_simultaneously(self, spell_actions: List[PlannedAction]):
        """Execute all planned spells simultaneously."""
        for action in spell_actions:
            if not action.unit.is_alive() or not action.target or not action.target.is_alive():
                continue
            action.unit.base_stats.spell.execute(action.unit, action.target, self.board, 
                                       crit_rate=action.unit.base_stats.crit_rate, 
                                       crit_dmg=action.unit.base_stats.crit_dmg, 
                                       can_crit=action.unit.spell_crit, 
                                       crit_roll=self.rng.random())
            

            event = CombatEvent(
                round_number=self.round_number,
                attacker=action.unit,
                target=action.target,
                action=CombatAction.CAST_SPELL,
                description=f"{action.unit.unit_type.value} casts a {action.unit.base_stats.spell.name} spell (planned in round {action.planned_round})"
            )
            self.combat_log.append(event)
    
    def _execute_moves_simultaneously(self, move_actions: List[PlannedAction]):
        """Execute all planned moves simultaneously."""
        # Filter out moves from dead units
        valid_moves = [action for action in move_actions if action.unit.is_alive()]
        
        
        # Execute successful moves
        for action in valid_moves:
            old_position = action.unit.position
            new_position = action.target_position
            
            if old_position and new_position:
                if self.board.move_unit(old_position, new_position):
                    event = CombatEvent(
                        round_number=self.round_number,
                        attacker=action.unit,
                        target=None,
                        action=CombatAction.MOVE,
                        position=new_position,
                        description=f"{action.unit.unit_type.value} moves to {new_position} (planned in round {action.planned_round})"
                    )
                    self.combat_log.append(event)
    
    def _cleanup_dead_units(self, all_units: List[Unit]):
        """Remove dead units from the board and log deaths."""
        for unit in all_units:
            if not unit.is_alive() and unit.position:
                # Log death
                death_event = CombatEvent(
                    round_number=self.round_number,
                    attacker=None,
                    target=unit,
                    action=CombatAction.WAIT,
                    description=f"{unit.unit_type.value} is defeated!"
                )
                self.combat_log.append(death_event)
                
                # Remove from board
                self.board.remove_unit(unit.position)
                
                # Cancel any pending actions from this dead unit
                self.action_queue = [action for action in self.action_queue if action.unit != unit]

    def _find_target(self, unit: Unit, enemies: List[Unit]) -> Optional[Unit]:
        """Find the best target for the unit."""
        if not enemies or not unit.position:
            return None
        # If the unit already has a target, it's still valid, and is within range + 1.66, return it
        # This allows the unit to keep attacking the same target if it is still valid and chase it if one cell movement is enough to reach it
        if unit.current_target and \
           unit.current_target in enemies and \
           self.board.l2_distance(unit.position, unit.current_target.position) <= unit.base_stats.range + 1.66:
            return unit.current_target
        
        # Targeting: closest enemy
        closest_enemy = None
        min_distance = float('inf')
        best_count = 1 # This should be unecessary, but just in case.

        for enemy in enemies:
            if not enemy.position:
                continue
            
            distance = self.board.l2_distance(unit.position, enemy.position)
            if distance < min_distance:
                min_distance = distance
                closest_enemy = enemy
                best_count = 1
            # If the distance is the same, randomly select one of the closest enemies, scaling by the count ensures that each member of the tie can be selected with equal probability
            if (distance == min_distance and self.rng.random() < 1/best_count):
                closest_enemy = enemy
                best_count += 1

        unit.current_target = closest_enemy  # Set current target for the unit
        
        return closest_enemy
    

    
    def get_combat_summary(self) -> Dict:
        """Get summary of the combat."""
        return {
            "total_rounds": self.round_number,
            "total_events": len(self.combat_log),
            "events": self.combat_log,
            "combat_seed": self.combat_seed,
            "action_timing": self.action_timing,
            "pending_actions": len(self.action_queue)
        }
    
    def set_combat_seed(self, seed: Optional[int]):
        """Set or change the combat seed."""
        self.combat_seed = seed
        self.rng = random.Random(seed)
    
    def get_combat_seed(self) -> Optional[int]:
        """Get the current combat seed."""
        return self.combat_seed
    
    def set_action_timing(self, action_timing: ActionTiming):
        """Set new action timing configuration."""
        self.action_timing = action_timing
    
    def get_pending_actions(self) -> List[PlannedAction]:
        """Get all currently pending actions."""
        return self.action_queue.copy()