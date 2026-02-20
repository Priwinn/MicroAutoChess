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
from player import Player
import time
from utils import setup_board_from_config, place_units_from_config
from levels import LEVELTANKSTEST, TANKSSOLUTION, TANKSSOLUTIONMIRROR


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
    positions = [(3, 3), (0, 0), (2, 3), (1, 2)]
    
    # Create Team 1 Units (uppercase symbols)
    team1_units = []
    for unit_type, position in zip(unit_types, positions):
        unit = Unit(
            unit_type=unit_type,
            rarity=UnitRarity.COMMON,
            team=1,
            level=1,
            position=position
        )
        unit.current_health = unit.get_max_health()
        team1_units.append(unit)
    
    #Define unit types for team 2
    unit_types = [UnitType.WARRIOR, UnitType.ARCHER, UnitType.TANK, UnitType.ASSASSIN]
    positions = [(3, 4), (6, 7), (4, 4), (5, 5)]
    # Create Team 2 Units (lowercase symbols)
    team2_units = []
    for unit_type, position in zip(unit_types, positions):
        unit = Unit(
            unit_type=unit_type,
            rarity=UnitRarity.COMMON,
            team=2,
            level=1,
            position=position
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
    board, team1_units, _ = setup_board_from_config(LEVELTANKSTEST)
    # board, team1_units, _ = setup_board_from_config(TANKSSOLUTIONMIRROR)
    team2_units = place_units_from_config(board, TANKSSOLUTION, team=2)
    
    # Create players and assign units on board
    player1 = Player(player_id=1)
    player1.set_units_on_board_from_list(team1_units)
    player2 = Player(player_id=2)
    player2.set_units_on_board_from_list(team2_units)
    
    # Position Team 1 units (top side)
    board.apply_player_units(player1)

    
    # Position Team 2 units (bottom side)
    board.apply_player_units(player2)
    
    return board, player1, player2

def run_combat_demonstration(debug: bool = False, combat_seed: int = 42):
    """Run the complete combat demonstration."""
    
    # Setup
    board, player1, player2 = setup_combat_scenario(debug=debug)
    visualizer = CombatVisualizer(board)
    
    # Show initial setup
    if debug:
        visualizer.print_board("Initial Setup")
        print("Legend: Uppercase = Team 1, Lowercase = Team 2")
        print("W/w = Warrior (Melee), A/a = Archer (Ranged)")
        print("\nUnit Stats:")
        print("- Warrior: High health, melee range (1), balanced damage")
        print("- Archer: Lower health, ranged (3), good damage")
        
        visualizer.print_unit_stats(player1.get_units_on_board_list(), "Team 1")
        visualizer.print_unit_stats(player2.get_units_on_board_list(), "Team 2")
    
    
    # Create combat engine and simulate
    combat_engine = CombatEngine(board, player1, player2, combat_seed=combat_seed)

    winner = combat_engine.simulate_combat()

    # Show results
    if debug:
        print("\n" + "=" * 60)
        print("COMBAT COMPLETE!")
        print("=" * 60)
        visualizer.print_board("Final Board State")
        visualizer.print_unit_stats(player1.get_units_on_board_list(), "Team 1 (Final)")
        visualizer.print_unit_stats(player2.get_units_on_board_list(), "Team 2 (Final)")
    
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
    
    board, player1, player2 = setup_combat_scenario()
    visualizer = CombatVisualizer(board)
    combat_engine = CombatEngine(board, player1, player2, combat_seed=42)
    
    visualizer.print_board("Starting Positions")
    visualizer.print_unit_stats(player1.get_units_on_board_list(), "Team 1")
    visualizer.print_unit_stats(player2.get_units_on_board_list(), "Team 2")
    
    max_frames = 500

    while combat_engine.frame_number < max_frames:
        frame_num = combat_engine.frame_number + 1

        # Check win conditions
        team1_alive = any(u.is_alive() for u in player1.get_units_on_board_list())
        team2_alive = any(u.is_alive() for u in player2.get_units_on_board_list())

        if not team1_alive:
            print(f"\n🏆 Team 2 Wins after {combat_engine.frame_number} frames!")
            break
        if not team2_alive:
            print(f"\n🏆 Team 1 Wins after {combat_engine.frame_number} frames!")
            break

        print(f"\n{'='*20} frame {frame_num} {'='*20}")
        input("Press Enter to execute this frame...")

        # Execute one frame
        combat_engine._execute_delayed_frame()

        # Show results
        visualizer.print_board(f"After frame {combat_engine.frame_number}")
        
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
        visualizer.print_unit_stats(player1.get_units_on_board_list(), "Team 1")
        visualizer.print_unit_stats(player2.get_units_on_board_list(), "Team 2")
    
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
    # main(debug=debug)

    # Time the combat demonstration
    # time_combat_demo(iterations=100)  # Adjust iterations for timing

    # Time the combat demonstration with win rates
    import cProfile
    from pstats import Stats

    pr = cProfile.Profile()
    pr.enable()

    time_combat_demo_with_winrates(iterations=10)

    pr.disable()
    stats = Stats(pr)
    stats.sort_stats('tottime').print_stats(10)
    # time_combat_demo_with_winrates(iterations=10)  # Adjust iterations for timing