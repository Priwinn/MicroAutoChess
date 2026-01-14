"""
Combat mockup demonstration with simultaneous actions.
"""

import sys
import os
sys.path.append(os.path.join(os.path.dirname(__file__), '..', '..'))

from constant_types import CombatEventType
from units import Unit, UnitType, UnitRarity, UnitStats
from board import Board, HexBoard
from combat import CombatEngine, CombatEvent, CombatAction
import time


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
        current_frame = 0
        
        for event in recent_events:
            if event.frame_number != current_frame:
                current_frame = event.frame_number
                print(f"\n--- frame {current_frame} ---")
            
            if event.event_type == CombatEventType.DAMAGE_DEALT and event.spell_name == "BasicAttack":
                print(f"  ⚔️  {event.description}")
            elif event.event_type == CombatEventType.MOVE_EXECUTED:
                print(f"  🏃 {event.description}")
            elif "defeated" in event.description.lower():
                print(f"  💀 {event.description}")
            else:
                print(f"  ℹ️  {event.description}")


def create_mock_units():
    """Create mock units for combat demonstration."""
    
    # Define unit types for team 1
    unit_types = [UnitType.TANK, UnitType.ARCHER, UnitType.TANK, UnitType.ARCHER]
    
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


def setup_combat_scenario(debug: bool = False):
    """Set up the combat scenario."""
    if debug:
        print("Setting up Auto Chess Simultaneous Combat Mockup...")
        print("=" * 60)
    
    # Create board
    board = HexBoard(size=(7, 8))
    
    # Create units
    team1_units, team2_units = create_mock_units()
    
    # Position Team 1 units (top side)
    board.place_unit(team1_units[0], (3, 3))  # Warrior front
    board.place_unit(team1_units[2], (2, 3))  # Tank front
    board.place_unit(team1_units[3], (1, 2))  # Assassin second line
    board.place_unit(team1_units[1], (0, 0))  # Archer back

    
    # Position Team 2 units (bottom side)
    board.place_unit(team2_units[0], (3, 4))  # Warrior front
    board.place_unit(team2_units[2], (4, 4))  # Tank front
    board.place_unit(team2_units[3], (5, 5))  # Assassin second line
    board.place_unit(team2_units[1], (6, 7))  # Archer back
    
    return board, team1_units, team2_units


def run_combat_demonstration(debug: bool = False, combat_seed: int = 42):
    """Run the complete combat demonstration."""
    
    # Setup
    board, team1_units, team2_units = setup_combat_scenario(debug=debug)
    visualizer = CombatVisualizer(board)
    
    # Show initial setup
    if debug:
        visualizer.print_board("Initial Setup")
        print("Legend: Uppercase = Team 1, Lowercase = Team 2")
        print("W/w = Warrior (Melee), A/a = Archer (Ranged)")
        print("\nUnit Stats:")
        print("- Warrior: High health, melee range (1), balanced damage")
        print("- Archer: Lower health, ranged (3), good damage")
        
        visualizer.print_unit_stats(team1_units, "Team 1")
        visualizer.print_unit_stats(team2_units, "Team 2")
    
    
    # Create combat engine and simulate
    combat_engine = CombatEngine(board, combat_seed=combat_seed)

    winner = combat_engine.simulate_combat(team1_units, team2_units)

    # Show results
    if debug:
        print("\n" + "=" * 60)
        print("COMBAT COMPLETE!")
        print("=" * 60)
        visualizer.print_board("Final Board State")
        visualizer.print_unit_stats(team1_units, "Team 1 (Final)")
        visualizer.print_unit_stats(team2_units, "Team 2 (Final)")
    
    # Combat summary
    summary = combat_engine.get_combat_summary()
    if debug:
        print(f"\n🏆 Combat Results:")
        print("-" * 30)
        
        if winner == 1:
            print("🎉 TEAM 1 WINS!")
        elif winner == 2:
            print("🎉 TEAM 2 WINS!")
        else:
            print("🤝 DRAW!")
        
        print(f"⏱️  Total frames: {summary['total_frames']}")
        print(f"📊 Total Events: {summary['total_events']}")
    
        # Analyze the combat
        attack_count = sum(1 for e in summary['events'] if e.event_type == CombatEventType.DAMAGE_DEALT and e.spell_name == "BasicAttack")
        move_count = sum(1 for e in summary['events'] if e.event_type == CombatEventType.MOVE_EXECUTED)
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
    
    frame_num = 0
    max_frames = 500
    
    while frame_num < max_frames:
        frame_num += 1
        
        # Check win conditions
        team1_alive = any(u.is_alive() for u in team1_units)
        team2_alive = any(u.is_alive() for u in team2_units)
        
        if not team1_alive:
            print(f"\n🏆 Team 2 Wins after {frame_num-1} frames!")
            break
        if not team2_alive:
            print(f"\n🏆 Team 1 Wins after {frame_num-1} frames!")
            break
        
        print(f"\n{'='*20} frame {frame_num} {'='*20}")
        input("Press Enter to execute this frame...")
        
        # Execute one frame
        all_units = [u for u in team1_units + team2_units if u.is_alive()]
        combat_engine.frame_number = frame_num
        combat_engine._execute_delayed_frame(all_units)
        
        # Show results
        visualizer.print_board(f"After frame {frame_num}")
        
        # Show what happened this frame
        frame_events = [e for e in combat_engine.combat_log if e.frame_number == frame_num]
        if frame_events:
            print(f"\nframe {frame_num} Events:")
            for event in frame_events:
                if event.event_type == CombatEventType.DAMAGE_DEALT:
                    print(f"  ⚔️  {event.description}")
                elif event.event_type == CombatEventType.MOVE_EXECUTED:
                    print(f"  🏃 {event.description}")
                elif event.event_type == CombatEventType.SPELL_EXECUTED:
                    print(f"  ✨ {event.description}")
                elif "defeated" in event.description.lower():
                    print(f"  💀 {event.description}")
        
        # Show current unit status
        visualizer.print_unit_stats(team1_units, "Team 1")
        visualizer.print_unit_stats(team2_units, "Team 2")
    
    print("\nInteractive demo completed!")

def time_combat_demo(iterations: int = 100):
    """Time the combat demonstration. Fixed seed for reproducibility."""
    
    times = []
    for i in range(iterations):
        iter_start = time.time()
        run_combat_demonstration(debug=False)
        iter_end = time.time()
        times.append(iter_end - iter_start)
    
    total_time = sum(times)
    avg_time = total_time / iterations
    stddev_time = (sum((t - avg_time) ** 2 for t in times) / iterations) ** 0.5
    
    print(f"\nTiming Results:")
    print(f"Total time for {iterations} combat demonstrations: {total_time:.2f} seconds")
    print(f"Average time per demonstration: {avg_time:.4f} seconds")
    print(f"Standard deviation: {stddev_time:.4f} seconds")

def time_combat_demo_with_winrates(iterations: int = 100):
    """Time the combat demonstration. Changes the seed for each iteration and tracks win rates."""

    times = []
    winners = []
    for i in range(iterations):
        iter_start = time.time()
        winner, _ = run_combat_demonstration(debug=False, combat_seed=i)
        iter_end = time.time()
        times.append(iter_end - iter_start)
        winners.append(winner)


    total_time = sum(times)
    avg_time = total_time / iterations
    stddev_time = (sum((t - avg_time) ** 2 for t in times) / iterations) ** 0.5
    
    print(f"\nTiming Results:")
    print(f"Total time for {iterations} combat demonstrations: {total_time:.2f} seconds")
    print(f"Average time per demonstration: {avg_time:.4f} seconds")
    print(f"Standard deviation: {stddev_time:.4f} seconds")
    print(f"Winner distribution: {winners.count(1)} Team 1, {winners.count(2)} Team 2, {winners.count(0)} Draw")
    print(f"Win rates: Team 1: {winners.count(1)/iterations:.2%}, Team 2: {winners.count(2)/iterations:.2%}, Draw: {winners.count(0)/iterations:.2%}")

def main(debug: bool = False):
    """Main function to run the combat demonstration."""
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
            run_combat_demonstration(debug=debug)

        if choice in ['2', '3']:
            if choice == '3':
                input("\nPress Enter to start interactive demo...")
            interactive_step_by_step()
        
        if choice not in ['1', '2', '3']:
            print("Invalid choice, running automatic demo...")
            run_combat_demonstration(debug=debug)

    except Exception as e:
        print(f"Error during combat demonstration: {e}")
        import traceback
        traceback.print_exc()
    
    print("\nDemo completed!")


if __name__ == "__main__":
    debug = True
    main(debug=debug)

    # Time the combat demonstration
    # time_combat_demo(iterations=100)  # Adjust iterations for timing

    # Time the combat demonstration with win rates
    time_combat_demo_with_winrates(iterations=100)  # Adjust iterations for timing