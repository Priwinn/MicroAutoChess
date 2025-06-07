"""
Combat mockup demonstration with simultaneous actions.
"""

import sys
import os
sys.path.append(os.path.join(os.path.dirname(__file__), '..', '..'))

from src.core.units import Unit, UnitType, UnitRarity, UnitStats
from src.core.board import Board, HexBoard
from src.core.combat import CombatEngine, CombatEvent, CombatAction


class CombatVisualizer:
    """Simple text-based combat visualizer."""
    
    def __init__(self, board: Board):
        self.board = board
    
    def print_board(self, title: str = ""):
        """Print the current state of the board."""
        self.board.print_board(title=title)
    
    def print_unit_stats(self, units: list, team_name: str):
        """Print unit statistics."""
        print(f"\n{team_name} Units:")
        print("-" * 50)
        for i, unit in enumerate(units):
            if unit.is_alive():
                health_ratio = unit.current_health / unit.get_max_health()
                health_bar = "█" * int(health_ratio * 10) + "░" * (10 - int(health_ratio * 10))
                print(f"{i+1}. {unit.unit_type.value.capitalize():<10} "
                      f"HP: {unit.current_health:3.1f}/{unit.get_max_health():<3.1f} [{health_bar:<10}] "
                      f"ATK: {unit.get_attack():<3.1f} DEF: {unit.get_defense():<3.1f} "
                      f"RNG: {unit.base_stats.range:<2.1f} POS: {unit.position} "
                      f"Mana: {unit.current_mana:<3.1f}/{unit.base_stats.max_mana:<3.1f}")
            else:
                print(f"{i+1}. {unit.unit_type.value.capitalize():<10} [DEFEATED]")
    
    def print_combat_log(self, events: list, max_events: int = 15):
        """Print recent combat events."""
        print(f"\nCombat Events (showing last {max_events}):")
        print("-" * 60)
        
        recent_events = events[-max_events:] if len(events) > max_events else events
        current_round = 0
        
        for event in recent_events:
            if event.round_number != current_round:
                current_round = event.round_number
                print(f"\n--- Round {current_round} ---")
            
            if event.action == CombatAction.ATTACK:
                print(f"  ⚔️  {event.description}")
            elif event.action == CombatAction.MOVE:
                print(f"  🏃 {event.description}")
            elif "defeated" in event.description.lower():
                print(f"  💀 {event.description}")
            else:
                print(f"  ℹ️  {event.description}")


def create_mock_units():
    """Create mock units for combat demonstration."""
    
    # Define unit types for team 1
    unit_types = [UnitType.WARRIOR, UnitType.ARCHER, UnitType.TANK, UnitType.ASSASSIN]
    
    # Create Team 1 Units (uppercase symbols)
    team1_units = []
    for unit_type in unit_types:
        unit = Unit(
            unit_type=unit_type,
            rarity=UnitRarity.COMMON,
            team=1,
            level=1
        )
        unit.current_health = unit.get_max_health()
        team1_units.append(unit)
    
    #Define unit types for team 2
    unit_types = [UnitType.WARRIOR, UnitType.ARCHER, UnitType.TANK, UnitType.ASSASSIN]
    # Create Team 2 Units (lowercase symbols)
    team2_units = []
    for unit_type in unit_types:
        unit = Unit(
            unit_type=unit_type,
            rarity=UnitRarity.COMMON,
            team=2,
            level=1
        )
        unit.current_health = unit.get_max_health()
        team2_units.append(unit)
    
    return team1_units, team2_units


def setup_combat_scenario():
    """Set up the combat scenario."""
    print("Setting up Auto Chess Simultaneous Combat Mockup...")
    print("=" * 60)
    
    # Create board
    board = HexBoard(size=(8, 8))
    
    # Create units
    team1_units, team2_units = create_mock_units()
    
    # Position Team 1 units (top side)
    board.place_unit(team1_units[0], (3, 3))  # Warrior front
    board.place_unit(team1_units[2], (2, 3))  # Tank front
    board.place_unit(team1_units[3], (1, 2))  # Assassin second line
    board.place_unit(team1_units[1], (0, 0))  # Archer back

    
    # Position Team 2 units (bottom side)
    board.place_unit(team2_units[0], (4, 4))  # Warrior front
    board.place_unit(team2_units[2], (5, 4))  # Tank front
    board.place_unit(team2_units[3], (6, 5))  # Assassin second line
    board.place_unit(team2_units[1], (7, 7))  # Archer back
    
    return board, team1_units, team2_units


def run_combat_demonstration():
    """Run the complete combat demonstration."""
    
    # Setup
    board, team1_units, team2_units = setup_combat_scenario()
    visualizer = CombatVisualizer(board)
    
    # Show initial setup
    visualizer.print_board("Initial Setup")
    print("Legend: Uppercase = Team 1, Lowercase = Team 2")
    print("W/w = Warrior (Melee), A/a = Archer (Ranged)")
    print("\nUnit Stats:")
    print("- Warrior: High health, melee range (1), balanced damage")
    print("- Archer: Lower health, ranged (3), good damage")
    
    visualizer.print_unit_stats(team1_units, "Team 1")
    visualizer.print_unit_stats(team2_units, "Team 2")
    
    input("\nPress Enter to start simultaneous combat...")
    
    # Create combat engine and simulate
    combat_engine = CombatEngine(board, combat_seed=42)
    
    print("\n" + "=" * 60)
    print("SIMULTANEOUS COMBAT BEGINS!")
    print("=" * 60)
    print("Each round, all units plan actions simultaneously:")
    print("1. Plan phase: All units decide their actions")
    print("2. Attack phase: All attacks resolve simultaneously") 
    print("3. Move phase: All movements resolve simultaneously")
    print("4. Cleanup: Remove defeated units")
    print("-" * 60)
    
    winner = combat_engine.simulate_combat(team1_units, team2_units)
    
    # Show results
    print("\n" + "=" * 60)
    print("COMBAT COMPLETE!")
    print("=" * 60)
    
    visualizer.print_board("Final Board State")
    visualizer.print_unit_stats(team1_units, "Team 1 (Final)")
    visualizer.print_unit_stats(team2_units, "Team 2 (Final)")
    
    # Combat summary
    summary = combat_engine.get_combat_summary()
    print(f"\n🏆 Combat Results:")
    print("-" * 30)
    
    if winner == 1:
        print("🎉 TEAM 1 WINS!")
    elif winner == 2:
        print("🎉 TEAM 2 WINS!")
    else:
        print("🤝 DRAW!")
    
    print(f"⏱️  Total Rounds: {summary['total_rounds']}")
    print(f"📊 Total Events: {summary['total_events']}")
    
    # Analyze the combat
    attack_count = sum(1 for e in summary['events'] if e.action == CombatAction.ATTACK)
    move_count = sum(1 for e in summary['events'] if e.action == CombatAction.MOVE)
    total_damage = sum(e.damage for e in summary['events'] if e.damage > 0)
    
    print(f"⚔️  Total Attacks: {attack_count}")
    print(f"🏃 Total Moves: {move_count}")
    print(f"💥 Total Damage: {total_damage}")
    
    # Show combat log
    visualizer.print_combat_log(summary['events'])
    
    return winner, summary


def interactive_step_by_step():
    """Interactive step-by-step demonstration of simultaneous combat."""
    
    print("\n" + "=" * 60)
    print("INTERACTIVE STEP-BY-STEP DEMO")
    print("=" * 60)
    
    board, team1_units, team2_units = setup_combat_scenario()
    visualizer = CombatVisualizer(board)
    combat_engine = CombatEngine(board, combat_seed=42)
    
    visualizer.print_board("Starting Positions")
    visualizer.print_unit_stats(team1_units, "Team 1")
    visualizer.print_unit_stats(team2_units, "Team 2")
    
    round_num = 0
    max_rounds = 50
    
    while round_num < max_rounds:
        round_num += 1
        
        # Check win conditions
        team1_alive = any(u.is_alive() for u in team1_units)
        team2_alive = any(u.is_alive() for u in team2_units)
        
        if not team1_alive:
            print(f"\n🏆 Team 2 Wins after {round_num-1} rounds!")
            break
        if not team2_alive:
            print(f"\n🏆 Team 1 Wins after {round_num-1} rounds!")
            break
        
        print(f"\n{'='*20} Round {round_num} {'='*20}")
        input("Press Enter to execute this round...")
        
        # Execute one round
        all_units = [u for u in team1_units + team2_units if u.is_alive()]
        combat_engine.round_number = round_num
        combat_engine._execute_delayed_round(all_units)
        
        # Show results
        visualizer.print_board(f"After Round {round_num}")
        
        # Show what happened this round
        round_events = [e for e in combat_engine.combat_log if e.round_number == round_num]
        if round_events:
            print(f"\nRound {round_num} Events:")
            for event in round_events:
                if event.action == CombatAction.ATTACK:
                    print(f"  ⚔️  {event.description}")
                elif event.action == CombatAction.MOVE:
                    print(f"  🏃 {event.description}")
                elif "defeated" in event.description.lower():
                    print(f"  💀 {event.description}")
        
        # Show current unit status
        visualizer.print_unit_stats(team1_units, "Team 1")
        visualizer.print_unit_stats(team2_units, "Team 2")
    
    print("\nInteractive demo completed!")


if __name__ == "__main__":
    print("Auto Chess Simultaneous Combat Mockup")
    print("=" * 50)
    print()
    print("Choose demonstration mode:")
    print("1. Full Automatic Combat Simulation")  
    print("2. Interactive Step-by-Step Combat")
    print("3. Both")
    
    choice = input("\nEnter choice (1-3): ").strip()
    
    try:
        if choice in ['1', '3']:
            run_combat_demonstration()
        
        if choice in ['2', '3']:
            if choice == '3':
                input("\nPress Enter to start interactive demo...")
            interactive_step_by_step()
        
        if choice not in ['1', '2', '3']:
            print("Invalid choice, running automatic demo...")
            run_combat_demonstration()
            
    except Exception as e:
        print(f"Error during combat demonstration: {e}")
        import traceback
        traceback.print_exc()
    
    print("\nDemo completed!")