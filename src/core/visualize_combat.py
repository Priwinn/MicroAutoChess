"""Run a pygame visualization of a combat scenario using the Board object.

Usage: `python src/core/visualize_combat.py`
"""
import math
import sys
import os
import time
import random
import pygame

# Ensure project root is on path when running this file directly
sys.path.append(os.path.join(os.path.dirname(__file__), '..', '..'))

import global_log
from visualizer import PygameBoardVisualizer
from combat import CombatEngine
from combat_test import setup_combat_scenario
from units import Unit
from constant_types import UnitType, UnitRarity
from pve_round_manager import PvERoundManager
from utils import setup_board_from_dict, setup_board_from_config
from levels import LEVELS


def main():
    # board, team1_units, team2_units = setup_combat_scenario(debug=False)
    # board, team1_units, team2_units = setup_board_from_dict((7, 8), TEAM1_POSITION_MAP)
    # snapshot player's starting units for resets
    # create PvE manager with initial enemy configuration; more configs can be added
    round_configs = LEVELS
    pve_manager = PvERoundManager(configs=round_configs)
    board, team1_units, team2_units = pve_manager.setup_round()

    render_fps = 60
    dt = 1/render_fps

    visual = PygameBoardVisualizer(board, render_fps=render_fps, cell_radius=40)
    engine = CombatEngine(board, combat_seed=42)
    engine.set_teams(team1_units, team2_units)

    running = True
    paused = True
    engine_fps = 10  # number of simulation frames (rounds) per second


    # initial planning for frame 0
    all_units = [u for u in team1_units + team2_units if u.is_alive()]
    # engine._plan_actions(all_units)
    sim_progress = 0.0
    # drag state for repositioning units before combat starts
    dragging = False
    dragged_unit = None
    drag_from = None
    drag_mouse_pos = (0, 0)


    try:
        while running:
            # ensure spawn button positions used for input match drawing
            try:
                visual.spawn_start_x = visual.left_offset + visual.margin
            except Exception:
                visual.spawn_start_x = None
            # control time using visual.clock

            for event in pygame.event.get():
                if event.type == pygame.QUIT:
                    running = False
                elif event.type == pygame.MOUSEBUTTONDOWN and event.button == 1:
                    # compute pause button and two speed-control buttons above it
                    try:
                        pause_r = visual.get_pause_button_rect()
                        btn_w, btn_h = pause_r.w, pause_r.h
                        spacing = 8
                        # top button: speed up (+)
                        speed_up_rect = pygame.Rect(pause_r.x, pause_r.y - (btn_h + spacing) * 2, btn_w, btn_h)
                        # middle button: slow down (-)
                        speed_down_rect = pygame.Rect(pause_r.x, pause_r.y - (btn_h + spacing) * 1, btn_w, btn_h)
                    except Exception:
                        speed_up_rect = speed_down_rect = None

                    # speed button clicks (handled before pause toggle)
                    try:
                        if speed_up_rect is not None and speed_up_rect.collidepoint(event.pos):
                            try:
                                engine_fps = min(40, int(engine_fps * 2))
                            except Exception:
                                engine_fps = 40
                            continue
                        if speed_down_rect is not None and speed_down_rect.collidepoint(event.pos):
                            try:
                                engine_fps = max(1, int(engine_fps // 2))
                            except Exception:
                                engine_fps = 10
                            continue
                    except Exception:
                        pass

                    # check if the pause/start button was clicked
                    try:
                        if visual.get_pause_button_rect().collidepoint(event.pos):
                            # if transitioning from paused -> running (Start clicked), reset damage meter
                            was_paused = paused
                            paused = not paused
                            if was_paused and not paused:
                                try:
                                    visual.damage_done.clear()
                                    visual.unit_info.clear()
                                except Exception:
                                    pass
                            continue
                    except Exception:
                        pass
                    # check spawn buttons

                    spawn_types = [UnitType.WARRIOR, UnitType.ARCHER, UnitType.MAGE, UnitType.TANK, UnitType.ASSASSIN]
                    spawn_specs = []
                    for ut in spawn_types:
                        tmp = Unit(unit_type=ut, rarity=UnitRarity.COMMON, team=2, level=1)
                        cost = tmp.get_cost()
                        spawn_specs.append((ut, cost))

                    for i, (ut, cost) in enumerate(spawn_specs):
                        rect = visual.get_spawn_button_rect(i, total=len(spawn_specs))
                        if rect.collidepoint(event.pos):
                            # check budget (managed by pve_manager)
                            if cost > pve_manager.player_budget:
                                break
                            #Only allow spawning if combat hasn't started yet
                            if not paused or engine.frame_number > 0:
                                break
                            # find first empty initial cell for team 2
                            valid = board.get_initial_positions(2)
                            target = None
                            for pos in valid:
                                c = board.get_cell(pos)
                                if c.is_empty():
                                    target = pos
                                    break
                            if target is not None:
                                # create unit and place
                                new_u = Unit(unit_type=ut, rarity=UnitRarity.COMMON, team=2, level=1)
                                new_u.current_health = new_u.get_max_health()
                                new_u.current_mana = 0
                                board.place_unit(new_u, target)
                                team2_units.append(new_u)
                                all_units.append(new_u)
                                all_units.sort(key=lambda u: (u.position[0]*board.size[1] + u.position[1]))

                                # deduct budget from PvE manager
                                pve_manager.player_budget = int(pve_manager.player_budget) - int(cost)

                                # update engine teams
                                engine.set_teams(team1_units, team2_units)

                            # consume click
                            break
                    # If combat hasn't started (frame 0) and paused, allow dragging units
                    try:
                        if paused and engine.frame_number == 0:
                            cell_pos = visual.get_cell_at_pixel(event.pos)
                            if cell_pos is not None:
                                cell = board.get_cell(cell_pos)
                                # Only allow dragging units that belong to player (team 2)
                                # and that are located in team 2's initial placement zone
                                if cell and cell.unit is not None and getattr(cell.unit, 'team', None) == 2:
                                    valid_starts = board.get_initial_positions(2)
                                    if cell_pos in valid_starts:
                                        # begin drag: remove unit from board and store
                                        dragged_unit = board.remove_unit(cell_pos)
                                        dragging = True
                                        drag_from = cell_pos
                                        drag_mouse_pos = event.pos
                                        # highlight player's placement zone while dragging
                                        try:
                                            visual.highlight_player_initial_zone = True
                                        except Exception:
                                            pass
                                        continue
                    except Exception:
                        pass
                elif event.type == pygame.MOUSEMOTION:
                    if dragging:
                        drag_mouse_pos = event.pos
                elif event.type == pygame.MOUSEBUTTONUP and event.button == 1:
                    if dragging:
                        try:
                            # compute shop rect (area covering spawn buttons + pause button)
                            try:
                                spawn0 = visual.get_spawn_button_rect(0, total=5)
                                pause_r = visual.get_pause_button_rect()
                                # align shop left with board left offset so it sits directly below the board
                                shop_x = visual.left_offset + visual.margin
                                shop_w = 5*spawn0.w + 4*visual.cell_radius//6.5  # 5 buttons + 4 spacings of 8px
                                # use a slightly taller shop rectangle to make selling easier
                                shop_h = int(spawn0.h * 1.5)
                                # anchor shop at bottom of window so it doesn't overlap the board
                                shop_y = visual.window_size[1] - spawn0.h - visual.margin
                                shop_rect = pygame.Rect(int(shop_x), int(shop_y), int(shop_w), int(shop_h))
                            except Exception:
                                shop_rect = None

                            sold = False
                            # If released over shop area -> sell for full refund
                            if shop_rect is not None and shop_rect.collidepoint(event.pos) and dragged_unit is not None:
                                try:
                                    refund = dragged_unit.get_cost()
                                except Exception:
                                    refund = getattr(dragged_unit, 'cost', 1)
                                pve_manager.player_budget = int(pve_manager.player_budget) + int(refund)
                                
                                # remove from team2 units list
                                if dragged_unit in team2_units:
                                    team2_units.remove(dragged_unit)
                                    all_units.remove(dragged_unit)

                                # update engine teams
                                engine.set_teams(team1_units, team2_units)
                                sold = True

                            if not sold:
                                # normal placement: try to place in a valid team 2 initial cell
                                target = visual.get_cell_at_pixel(event.pos)
                                placed = False
                                if target is not None and board.is_valid_position(target):
                                    # only allow placing within team 2's initial zone
                                    valid_targets = board.get_initial_positions(2)
                                    if target in valid_targets:
                                        tcell = board.get_cell(target)
                                        if tcell.is_empty():
                                            board.place_unit(dragged_unit, target)
                                            placed = True
                                        else:
                                            # If occupied by a friendly (team 2) unit, swap positions
                                            other = tcell.unit
                                            if getattr(other, 'team', None) == 2:
                                                try:
                                                    # remove the occupying unit, place dragged unit, then place the other at original slot
                                                    other_unit = board.remove_unit(target)
                                                    board.place_unit(dragged_unit, target)
                                                    # place other unit back to where dragged unit came from
                                                    board.place_unit(other_unit, drag_from)
                                                    placed = True
                                                    # keep unit lists consistent and update engine teams
                                                    try:
                                                        all_units.sort(key=lambda u: (u.position[0]*board.size[1] + u.position[1]))
                                                    except Exception:
                                                        pass
                                                    try:
                                                        engine.set_teams(team1_units, team2_units)
                                                    except Exception:
                                                        pass
                                                except Exception:
                                                    # fallback: return dragged unit to origin later
                                                    placed = False
                                if not placed:
                                    # return to original position
                                    board.place_unit(dragged_unit, drag_from)
                        except Exception:
                            try:
                                board.place_unit(dragged_unit, drag_from)
                            except Exception:
                                pass
                        # stop highlighting now that drag finished
                        try:
                            visual.highlight_player_initial_zone = False
                        except Exception:
                            pass
                        dragging = False
                        dragged_unit = None
                        drag_from = None
                elif event.type == pygame.KEYDOWN:
                    if event.key == pygame.K_SPACE:
                        paused = not paused
                    # single-step forward when paused
                    if event.key == pygame.K_n:
                        # advance one simulation frame
                        engine._execute_delayed_frame(all_units=all_units)
                        sim_progress = 0.0

            if not paused:
                sim_progress += dt * engine_fps
                # advance as many whole simulation frames as needed
                while sim_progress >= 1.0:
                    engine._execute_delayed_frame(all_units=all_units)
                    sim_progress -= 1.0

            # when the match actually starts (unpaused at frame 0), capture player starting positions
                if not paused and engine.frame_number == 0:
                    # capture current player and enemy unit starting positions
                    try:
                        pve_manager.initial_player = pve_manager._clone_unit_list(team2_units)
                    except Exception:
                        pass
                    player_positions = [u.position for u in team2_units if getattr(u, 'position', None) is not None]
                    # enemy_positions = [u.position for u in team1_units if getattr(u, 'position', None) is not None]
                    if player_positions:
                        try:
                            pve_manager.save_player_positions(player_positions)
                        except Exception:
                            pass
                    # if enemy_positions:
                    #     try:
                    #         pve_manager.save_enemy_positions(enemy_positions)
                    #     except Exception:
                    #         pass


            # draw with current in-frame progress
            visual.draw_board(engine=engine, sim_frame=engine.frame_number, sim_progress=sim_progress)
            # draw spawn buttons for player (team 2) with costs
            spawn_types = [UnitType.WARRIOR, UnitType.ARCHER, UnitType.MAGE, UnitType.TANK, UnitType.ASSASSIN]
            spawn_specs = []
            for ut in spawn_types:
                # compute cost using a temporary Unit instance (COMMON, level 1)
                try:
                    tmp = Unit(unit_type=ut, rarity=UnitRarity.COMMON, team=2, level=1)
                    cost = tmp.get_cost()
                except Exception:
                    cost = 1
                spawn_specs.append((ut, cost))
            # align spawn buttons to the left of the shop (directly under the board)
            try:
                visual.spawn_start_x = visual.left_offset + visual.margin
            except Exception:
                visual.spawn_start_x = None
            # query budget from PvE manager for display
            current_budget = int(pve_manager.player_budget)

            visual.draw_spawn_buttons(spawn_specs, budget=current_budget)
            # clear override so other code paths aren't affected
            try:
                visual.spawn_start_x = None
            except Exception:
                pass
            # draw budget text near spawn buttons
            try:
                if spawn_specs:
                    r0 = visual.get_spawn_button_rect(0, total=len(spawn_specs))
                    budget_val = int(pve_manager.player_budget)
                    budget_surf = visual.tooltip_font.render(f"Budget: {budget_val}", True, (240, 240, 160))
                    visual.screen.blit(budget_surf, (r0.x, r0.y - budget_surf.get_height() - 6))
            except Exception:
                pass
            # if dragging, draw the dragged unit at mouse
            try:
                if 'dragging' in locals() and dragging and dragged_unit is not None:
                    mx, my = pygame.mouse.get_pos()
                    # draw ghost circle + symbol
                    team = getattr(dragged_unit, 'team', 0)
                    color = visual.team_colors.get(team, (200, 200, 200))
                    pygame.draw.circle(visual.screen, color, (mx, my), int(visual.cell_radius * 0.45))
                    symbol = dragged_unit._get_unit_symbol()
                    text_surf = visual.font.render(symbol, True, (255, 255, 255))
                    text_rect = text_surf.get_rect(center=(mx, my))
                    visual.screen.blit(text_surf, text_rect)
                    # draw shop hint above shop area
                    try:
                        spawn0 = visual.get_spawn_button_rect(0, total=5)
                        pause_r = visual.get_pause_button_rect()
                        # align shop left with board left offset so it sits directly below the board
                        shop_x = visual.left_offset + visual.margin
                        shop_w = 5*spawn0.w + 4*visual.cell_radius//6.5  # 5 buttons + 4 spacings of 8px
                        shop_h = int(spawn0.h * 1.5)
                        # anchor shop at bottom of window so it doesn't overlap the board
                        shop_y = visual.window_size[1] - spawn0.h - visual.margin
                        shop_rect = pygame.Rect(int(shop_x), int(shop_y), int(shop_w), int(shop_h))
                        # highlight shop area slightly
                        highlight = pygame.Surface((shop_rect.w, shop_rect.h), pygame.SRCALPHA)
                        pygame.draw.rect(highlight, (80, 80, 80, 120), (0, 0, shop_rect.w, shop_rect.h), border_radius=6)
                        visual.screen.blit(highlight, (shop_rect.x, shop_rect.y))
                        # draw sell text on top of shop (inside area)
                        tip = "Drag here to sell unit"
                        # render tip at double the tooltip font size for emphasis
                        try:
                            big_font = pygame.font.SysFont('Arial', max(12, int(visual.tooltip_font.get_height() * 2)))
                        except Exception:
                            big_font = visual.tooltip_font
                        
                        pad = visual.cell_radius // 6
                        tip_surf = big_font.render(tip, True, (240, 240, 240))
                        bx = shop_rect.centerx - tip_surf.get_width() // 2
                        by = shop_rect.y + pad
                        # small background for text
                        
                        ts = pygame.Surface((tip_surf.get_width() + pad*2, tip_surf.get_height() + pad), pygame.SRCALPHA)
                        pygame.draw.rect(ts, (30, 30, 30, 220), (0, 0, ts.get_width(), ts.get_height()), border_radius=6)
                        ts.blit(tip_surf, (pad, 4))
                        visual.screen.blit(ts, (bx - pad, by))
                    except Exception:
                        pass
            except Exception:
                pass
            # draw pause/start button and handle mouse clicks in event loop
            # draw speed control buttons above pause/start
            try:
                pause_r = visual.get_pause_button_rect()
                btn_w, btn_h = pause_r.w, pause_r.h
                spacing = 8
                speed_up_rect = pygame.Rect(pause_r.x, pause_r.y - (btn_h + spacing) * 2, btn_w, btn_h)
                speed_down_rect = pygame.Rect(pause_r.x, pause_r.y - (btn_h + spacing) * 1, btn_w, btn_h)

                # draw up button
                surf_up = pygame.Surface((speed_up_rect.w, speed_up_rect.h), pygame.SRCALPHA)
                pygame.draw.rect(surf_up, (70, 70, 100), (0, 0, surf_up.get_width(), surf_up.get_height()), border_radius=6)
                pygame.draw.rect(surf_up, (140, 140, 140), (0, 0, surf_up.get_width(), surf_up.get_height()), 1, border_radius=6)
                plus = visual.tooltip_font.render('X2 SPEED', True, (255, 255, 255))
                surf_up.blit(plus, plus.get_rect(center=(surf_up.get_width()//2, surf_up.get_height()//2)))
                visual.screen.blit(surf_up, (speed_up_rect.x, speed_up_rect.y))

                # draw down button
                surf_dn = pygame.Surface((speed_down_rect.w, speed_down_rect.h), pygame.SRCALPHA)
                pygame.draw.rect(surf_dn, (70, 70, 100), (0, 0, surf_dn.get_width(), surf_dn.get_height()), border_radius=6)
                pygame.draw.rect(surf_dn, (140, 140, 140), (0, 0, surf_dn.get_width(), surf_dn.get_height()), 1, border_radius=6)
                minus = visual.tooltip_font.render('X0.5 SPEED', True, (255, 255, 255))
                surf_dn.blit(minus, minus.get_rect(center=(surf_dn.get_width()//2, surf_dn.get_height()//2)))
                visual.screen.blit(surf_dn, (speed_down_rect.x, speed_down_rect.y))

                # show current simulation FPS (engine_fps) between controls and pause button
                try:
                    fps_text = visual.tooltip_font.render(f"x{engine_fps}", True, (240, 240, 160))
                    tx = pause_r.centerx - fps_text.get_width() // 2
                    ty = speed_down_rect.y + speed_down_rect.h + 4
                    visual.screen.blit(fps_text, (tx, ty))
                except Exception:
                    pass
            except Exception:
                pass

            visual.draw_pause_button(paused)
            # Draw any deferred tooltips (spawn button tooltips should appear above pause/start)
            try:
                visual.flush_tooltips()
            except Exception:
                pass
            visual.present()

            

            # Check for win condition
            team1_alive = any(u.is_alive() and u.team == 1 for u in team1_units)
            team2_alive = any(u.is_alive() and u.team == 2 for u in team2_units)
            if (not team1_alive or not team2_alive) and engine.frame_number > 0:
                # player won -> advance to next enemy configuration if available
                if not team1_alive:
                    advanced = pve_manager.advance_round()
                    if advanced:
                        # apply next round: place new enemy config, keep player's current units
                        team1_units, team2_units = pve_manager.apply_round_to_board(board, player_units=team2_units, reset_player=True)
                        engine = CombatEngine(board, combat_seed=42)
                        engine.set_teams(team1_units, team2_units)
                        all_units = [u for u in team1_units + team2_units if u.is_alive()]
                        all_units.sort(key=lambda u: (u.position[0]*board.size[1] + u.position[1]))
                        # restart paused at beginning of next round
                        paused = True
                        sim_progress = 0.0
                        continue
                    else:
                        # no more rounds -> show magnificent win screen then exit
                        display_win_screen(visual)
                        pve_manager = PvERoundManager(configs=round_configs)
                        board, team1_units, team2_units = pve_manager.setup_round()
                        visual = PygameBoardVisualizer(board, render_fps=render_fps, cell_radius=40)
                        engine = CombatEngine(board, combat_seed=42)
                        engine.set_teams(team1_units, team2_units)
                        global_log.combat_log.clear()
                        # running = False

                else:
                    # player lost -> reset to round initial configuration and restore player's units
                    team1_units, team2_units = pve_manager.apply_round_to_board(board, player_units=None, reset_player=True)
                    engine = CombatEngine(board, combat_seed=42)
                    engine.set_teams(team1_units, team2_units)
                    all_units = [u for u in team1_units + team2_units if u.is_alive()]
                    all_units.sort(key=lambda u: (u.position[0]*board.size[1] + u.position[1]))
                    paused = True
                    sim_progress = 0.0
                    continue
    except KeyboardInterrupt:
        pass
    finally:
        visual.close()


def display_win_screen(visual: 'PygameBoardVisualizer', duration: float = 600.0):
    """Show a large celebratory win screen with confetti and fireworks.

    Blocks for `duration` seconds or until the user presses a key/mouse.
    """
    screen = visual.screen
    clock = visual.clock
    start = pygame.time.get_ticks() / 1000.0
    now = start

    # confetti: list of [x, y, vx, vy, w, h, color, rot]
    confetti = []
    # fireworks: list of [x,y,phase,start,colors,particles]
    fireworks = []

    def spawn_confetti(n=80):
        for i in range(n):
            x = random.uniform(0, visual.window_size[0])
            y = random.uniform(-visual.window_size[1]*0.5, 0)
            vx = random.uniform(-80, 80)
            vy = random.uniform(40, 220)
            w = random.randint(6, 14)
            h = random.randint(6, 14)
            color = (random.randint(100, 255), random.randint(80, 255), random.randint(80, 255))
            confetti.append([x, y, vx, vy, w, h, color, random.uniform(0, 360)])

    def spawn_firework():
        x = random.uniform(visual.window_size[0]*0.15, visual.window_size[0]*0.85)
        y = random.uniform(visual.window_size[1]*0.15, visual.window_size[1]*0.45)
        colors = [(random.randint(128, 255), random.randint(128, 255), random.randint(128, 255)) for _ in range(6)]
        particles = []
        for i in range(40):
            ang = random.uniform(0, 2*math.pi)
            speed = random.uniform(60, 320)
            particles.append([math.cos(ang)*speed, math.sin(ang)*speed, random.choice(colors)])
        fireworks.append([x, y, 0.0, pygame.time.get_ticks()/1000.0, colors, particles])

    # big title font
    try:
        big_font = pygame.font.SysFont('Arial', 72)
    except Exception:
        big_font = visual.font

    spawn_confetti(140)
    next_fire = now + 0.3

    running_win = True
    while running_win:
        for ev in pygame.event.get():
            if ev.type == pygame.QUIT:
                running_win = False
                break
            if ev.type in (pygame.KEYDOWN, pygame.MOUSEBUTTONDOWN):
                running_win = False
                break

        now = pygame.time.get_ticks() / 1000.0
        t = now - start
        # update confetti
        new_conf = []
        for c in confetti:
            c[0] += c[2] * (1/visual.render_fps)
            c[1] += c[3] * (1/visual.render_fps)
            c[3] += 30 * (1/visual.render_fps)  # gravity
            c[7] += 180 * (1/visual.render_fps)
            if c[1] < visual.window_size[1] + 50:
                new_conf.append(c)
        confetti[:] = new_conf

        # occasional spawn
        if random.random() < 0.08:
            spawn_confetti(8)

        # fireworks spawn
        if now >= next_fire:
            spawn_firework()
            next_fire = now + random.uniform(0.6, 1.4)

        # draw overlay
        screen.fill((8, 8, 24))

        # draw large title with glow and pulse
        pulse = 1.0 + 0.08 * math.sin(t * 6.0)
        title = "VICTORY!"
        title_surf = big_font.render(title, True, (255, 240, 120))
        tw, th = title_surf.get_size()
        sx = visual.window_size[0] // 2
        sy = int(visual.window_size[1] * 0.32)
        # glow layers
        for i, a in enumerate((220, 140, 80)):
            glow_s = pygame.Surface((int(tw * (1.0 + i*0.18)), int(th * (1.0 + i*0.18))), pygame.SRCALPHA)
            alpha = max(40, int(180 / (i+1)))
            pygame.draw.rect(glow_s, (255, 200, 80, alpha), glow_s.get_rect(), border_radius=16)
            rect = glow_s.get_rect(center=(sx, sy))
            screen.blit(glow_s, rect)

        ts = pygame.transform.rotozoom(title_surf, 0, pulse)
        rect = ts.get_rect(center=(sx, sy))
        screen.blit(ts, rect)

        # subtitle
        try:
            small = pygame.font.SysFont('Arial', 28)
        except Exception:
            small = visual.tooltip_font
        subtitle = "All rounds cleared — You are the most magnificent!"
        sub_s = small.render(subtitle, True, (220, 220, 255))
        screen.blit(sub_s, (visual.window_size[0]//2 - sub_s.get_width()//2, sy + int(th*0.7)))

        # draw confetti
        for c in confetti:
            x, y, vx, vy, w, h, color, rot = c
            r = pygame.Surface((int(w), int(h)), pygame.SRCALPHA)
            pygame.draw.rect(r, color, (0, 0, int(w), int(h)), border_radius=2)
            rr = pygame.transform.rotate(r, rot)
            screen.blit(rr, (int(x - rr.get_width()/2), int(y - rr.get_height()/2)))

        # draw fireworks
        alive_fw = []
        for fw in fireworks:
            x, y, phase, start_t, colors, parts = fw
            age = now - start_t
            # particles outward
            for p in parts:
                vx, vy, col = p
                px = x + vx * age * 0.004
                py = y + vy * age * 0.004 + 0.5 * 60 * (age**2)
                rad = max(1, int(3 + 2 * (1.0 - max(0.0, age/1.4))))
                if 0 <= px < visual.window_size[0] and 0 <= py < visual.window_size[1]:
                    pygame.draw.circle(screen, col, (int(px), int(py)), rad)
            if age < 1.6:
                alive_fw.append(fw)
        fireworks[:] = alive_fw

        # small instruction
        inst = visual.tooltip_font.render('Press any key or click to continue', True, (200, 200, 200))
        screen.blit(inst, (visual.window_size[0]//2 - inst.get_width()//2, int(visual.window_size[1]*0.88)))

        pygame.display.flip()
        clock.tick(visual.render_fps)

        if now - start >= duration:
            break


if __name__ == '__main__':
    main()
