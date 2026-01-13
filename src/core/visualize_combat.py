"""Run a pygame visualization of a combat scenario using the Board object.

Usage: `python src/core/visualize_combat.py`
"""
import sys
import os
import time
import pygame

# Ensure project root is on path when running this file directly
sys.path.append(os.path.join(os.path.dirname(__file__), '..', '..'))

from src.core.visualizer import PygameBoardVisualizer
from src.core.combat import CombatEngine
from src.core.combat_test import setup_combat_scenario


def main():
    board, team1_units, team2_units = setup_combat_scenario(debug=False)

    visual = PygameBoardVisualizer(board, cell_radius=40)
    engine = CombatEngine(board, combat_seed=42)
    engine.set_teams(team1_units, team2_units)

    running = True
    paused = False
    fps = 60  # number of simulation frames (rounds) per second

    # initial planning for frame 0
    all_units = [u for u in team1_units + team2_units if u.is_alive()]
    engine._plan_actions(all_units)
    sim_progress = 0.0

    try:
        while running:
            # control time using visual.clock
            dt = visual.clock.tick(60) / 1000.0

            for event in pygame.event.get():
                if event.type == pygame.QUIT:
                    running = False
                elif event.type == pygame.KEYDOWN:
                    if event.key == pygame.K_SPACE:
                        paused = not paused
                    # single-step forward when paused
                    if event.key == pygame.K_n:
                        # advance one simulation frame
                        engine.frame_number += 1
                        engine._execute_queued_actions()
                        all_units = [u for u in team1_units + team2_units if u.is_alive()]
                        engine._plan_actions(all_units)
                        engine._cleanup_dead_units(all_units)
                        sim_progress = 0.0

            if not paused:
                sim_progress += dt * fps
                # advance as many whole simulation frames as needed
                while sim_progress >= 1.0:
                    engine.frame_number += 1
                    engine._execute_queued_actions()
                    all_units = [u for u in team1_units + team2_units if u.is_alive()]
                    engine._plan_actions(all_units)
                    engine._cleanup_dead_units(all_units)
                    sim_progress -= 1.0

            # draw with current in-frame progress
            visual.draw_board(engine=engine, sim_frame=engine.frame_number, sim_progress=sim_progress)
            visual.present()

            # Check for win condition
            team1_alive = any(u.is_alive() and u.team == 1 for u in team1_units)
            team2_alive = any(u.is_alive() and u.team == 2 for u in team2_units)
            if not team1_alive or not team2_alive:
                time.sleep(1.0)
                running = False

    except KeyboardInterrupt:
        pass
    finally:
        visual.close()


if __name__ == '__main__':
    main()
