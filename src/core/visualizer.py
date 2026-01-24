import math
import random
import pygame
from typing import Tuple
from collections import defaultdict
from combat import CombatEngine
from constant_types import CombatAction, CombatEventType
from units import Unit, UnitRarity
import pygame.display


def oddr_to_axial(position: Tuple[int, int]) -> Tuple[int, int]:
    x, y = position
    q = x - (y - (y & 1)) // 2
    r = y
    return (q, r)


def hex_to_pixel(q: int, r: int, size: float) -> Tuple[float, float]:
    """Convert axial hex coords to pixel coords (pointy-top)."""
    x = size * math.sqrt(3) * (q + r / 2)
    y = size * 1.5 * r
    return x, y


def polygon_corners(center_x: float, center_y: float, size: float):
    """Return 6 points for a pointy-top hex centered at given pixel coords."""
    points = []
    for i in range(6):
        angle = math.pi / 180 * (60 * i - 30)
        px = center_x + size * math.cos(angle)
        py = center_y + size * math.sin(angle)
        points.append((px, py))
    return points


class PygameBoardVisualizer:
    """Render `Board` (both square and odd-r hex) using pygame.

    Draws hex cells, unit icons, health bars and mana bars.
    """

    def __init__(self, board, render_fps: int = 60, cell_radius: int = 28, margin: int = 16):
        # Initialize pygame before using display APIs
        pygame.init()
        pygame.font.init()
        self.board = board
        # Desired fullscreen/window resolution (use current screen resolution)
        info = pygame.display.Info()
        desired_w, desired_h = info.current_w, info.current_h

        # Start with requested cell radius, compute estimated original sizes
        orig_cell = cell_radius
        est_right = max(320, int(orig_cell * 6))
        est_bottom = max(60, int(orig_cell * 1.0))
        est_w = int(orig_cell * math.sqrt(3) * (self.board.width + 0.5)) + margin * 2 + est_right
        est_h = int(orig_cell * 1.5 * (self.board.height + 1)) + margin * 2 + est_bottom

        # Compute scale factor to fit the board into the desired window
        try:
            scale = min(float(desired_w) / max(1, est_w), float(desired_h) / max(1, est_h))
        except Exception:
            scale = 1.0

        # Limit scaling for sanity
        scale = max(0.5, min(scale, 4.0))

        # Apply scale to cell radius and use that for layout
        self.cell_radius = max(10, int(orig_cell * scale))
        self.margin = margin
        self.bg_color = (30, 30, 30)
        self.grid_color = (80, 80, 80)
        self.team_colors = {1: (60, 140, 220), 2: (220, 100, 100)}
        self.font = pygame.font.SysFont('Arial', max(12, int(cell_radius * 0.6)))
        self.render_fps = render_fps
        # Smaller font for hover tooltips
        # Fonts scaled relative to computed cell radius
        self.tooltip_font = pygame.font.SysFont('Arial', max(9, int(self.cell_radius * 0.45)))
        self.tooltip_max_width = int(self.cell_radius * 6.0)

        # Reserve extra space to the right for charts / debug panels and bottom for UI
        # Increase default right panel to provide a larger damage meter area
        self.right_panel_width = max(480, int(self.cell_radius * 8))
        self.bottom_panel_height = max(60, int(self.cell_radius * 1.0))

        # Force window size to desired resolution
        self.window_size = (desired_w, desired_h)
        # Add extra left offset so the grid doesn't sit flush to the window edge
        # Shift board slightly to the right for better centering
        base_offset = int(self.window_size[0] * 0.06)
        extra_shift = int(self.window_size[0] * 0.04)  # additional right shift
        self.left_offset = base_offset + extra_shift
        # Optional override for spawn button start x (align spawn buttons to shop)
        self.spawn_start_x: int | None = None

        self.screen = pygame.display.set_mode(self.window_size)
        pygame.display.set_caption('MicroAutoChess - Visualizer')
        self.clock = pygame.time.Clock()
        # Floating combat text state
        self._seen_events = set()
        self.floating_texts = []
        # AoE spell animations: list of [cx, cy, radius, start_time, duration, color]
        self.aoe_animations = []
        # Spin animations for units planning spells: list of [unit_id, start_time, duration, speed]
        self.spin_animations = []
        # Particles for spell effects: list of [x0,y0,vx,vy,start,lifetime,color,size]
        self.particles = []
        # damage done per unit id
        self.damage_done = defaultdict(float)
        # smaller font for damage numbers
        self.damage_font = pygame.font.SysFont('Arial', max(14, int(self.cell_radius * 0.6)))
        # larger font for chart titles
        self.title_font = pygame.font.SysFont('Arial', max(18, int(self.cell_radius * 0.9)))
        # store lightweight unit info (symbol, team) so dead units can still be shown in charts
        self.unit_info = {}
        # When true, render player's initial placement cells with a highlighted border
        self.highlight_player_initial_zone = False

    def draw_board(self, engine: CombatEngine = None, sim_frame: int = 0, sim_progress: float = 0.0):
        """Draw board and optionally animate pending move actions from engine.

        engine: CombatEngine instance or None
        sim_frame: current integer frame in simulation
        sim_progress: fraction [0,1) through the current frame
        """
        self.screen.fill(self.bg_color)

        moving_map = {}
        if engine is not None:
            pending = engine.get_pending_actions()
            for action in pending:
                if action.action_type == CombatAction.MOVE and action.start_position and action.target_position:
                    # Only animate if resolution is in the future
                    if action.resolution_frame > sim_frame:
                        start_f = action.planned_frame
                        end_f = action.resolution_frame
                        denom = max(1, end_f - start_f)
                        t = (sim_frame + sim_progress - start_f) / denom
                        t = max(0.0, min(1.0, t))

                        q1, r1 = oddr_to_axial(action.start_position)
                        q2, r2 = oddr_to_axial(action.target_position)
                        x1, y1 = hex_to_pixel(q1, r1, self.cell_radius)
                        x2, y2 = hex_to_pixel(q2, r2, self.cell_radius)
                        # offsets (apply left offset + margin)
                        cx1 = x1 + self.left_offset + self.margin
                        cy1 = y1 + self.margin + self.cell_radius + 4
                        cx2 = x2 + self.left_offset + self.margin
                        cy2 = y2 + self.margin + self.cell_radius + 4

                        ix = int(cx1 + (cx2 - cx1) * t)
                        iy = int(cy1 + (cy2 - cy1) * t)
                        moving_map[action.unit.id] = (action.unit, ix, iy, action)

            # Collect attack animations (lines from source toward target)
            attack_anims = []
            for action in pending:
                if action.action_type == CombatAction.ATTACK and action.start_position and action.target and getattr(action.target, 'position', None):
                    if action.resolution_frame > sim_frame:
                        start_f = action.planned_frame
                        end_f = action.resolution_frame
                        denom = max(1, end_f - start_f)
                        t = (sim_frame + sim_progress - start_f) / denom
                        t = max(0.0, min(1.0, t))

                        q1, r1 = oddr_to_axial(action.start_position)
                        q2, r2 = oddr_to_axial(action.target.position)
                        x1, y1 = hex_to_pixel(q1, r1, self.cell_radius)
                        x2, y2 = hex_to_pixel(q2, r2, self.cell_radius)
                        cx1 = x1 + self.left_offset + self.margin
                        cy1 = y1 + self.margin + self.cell_radius + 4
                        cx2 = x2 + self.left_offset + self.margin
                        cy2 = y2 + self.margin + self.cell_radius + 4

                        cur_x = cx1 + (cx2 - cx1) * t
                        cur_y = cy1 + (cy2 - cy1) * t
                        attack_anims.append((action, cx1, cy1, cur_x, cur_y, t))

            # Collect spell projectile animations (e.g., Fireball) for pending spell casts
            spell_projectiles = []
            for action in pending:
                if action.action_type == CombatAction.CAST_SPELL and getattr(action, 'spell_instance', None) is not None:
                    spell_name = getattr(action.spell_instance, 'name', None)
                    # only animate Fireball projectiles here
                    if (spell_name == 'Fireball' or spell_name == 'Spin Slash') and action.start_position is not None:
                        # resolve only if in the future
                        if action.resolution_frame > sim_frame:
                            start_f = action.planned_frame
                            end_f = action.resolution_frame
                            denom = max(1, end_f - start_f)
                            t = (sim_frame + sim_progress - start_f) / denom
                            t = max(0.0, min(1.0, t))

                            # source pixel
                            q1, r1 = oddr_to_axial(action.start_position)
                            x1, y1 = hex_to_pixel(q1, r1, self.cell_radius)
                            cx1 = x1 + self.left_offset + self.margin
                            cy1 = y1 + self.margin + self.cell_radius + 4

                            # target: prefer explicit target unit position, fallback to target_position
                            target_pos = None
                            if getattr(action, 'target', None) and getattr(action.target, 'position', None):
                                target_pos = action.target.position
                            elif getattr(action, 'target_position', None):
                                target_pos = action.target_position

                            if target_pos is not None:
                                q2, r2 = oddr_to_axial(target_pos)
                                x2, y2 = hex_to_pixel(q2, r2, self.cell_radius)
                                cx2 = x2 + self.left_offset + self.margin
                                cy2 = y2 + self.margin + self.cell_radius + 4

                                cur_x = cx1 + (cx2 - cx1) * t
                                cur_y = cy1 + (cy2 - cy1) * t
                                spell_projectiles.append((action, cx1, cy1, cx2, cy2, cur_x, cur_y, t))

            # Collect damage events from engine combat log and create floating texts
            now = pygame.time.get_ticks() / 1000.0
            for ev in engine.combat_log:
                # Spell planning -> start spin animation
                if getattr(ev, 'event_type', None) == CombatEventType.ACTION_PLANNED and getattr(ev, 'spell_name', None) and id(ev) not in self._seen_events:
                    source = getattr(ev, 'source', None)
                    if source is not None and getattr(source, 'id', None) is not None:
                        spin_duration = 0.6
                        spin_speed = 360.0/spin_duration  # degrees per second
                        self.spin_animations.append([source.id, now, spin_duration, spin_speed])
                        self._seen_events.add(id(ev))
                # Spell execution: Fireball AoE
                if ev.event_type == CombatEventType.SPELL_EXECUTED and (ev.spell_name == 'Fireball' or ev.spell_name == 'Spin Slash') and id(ev) not in self._seen_events:
                    pos = getattr(ev, 'position', None)
                    if pos is None and getattr(ev, 'target', None) and getattr(ev.target, 'position', None):
                        pos = ev.target.position
                    if pos is not None:
                        # store AoE as board position + hex radius (1 => adjacent cells)
                        radius_hex = 1
                        duration = 0.6
                        # choose color from source team if available, otherwise default red
                        team = ev.source.team
                        color = self.team_colors.get(team, (200, 40, 40))
                        self.aoe_animations.append([pos, radius_hex, now, duration, color])
                        self._seen_events.add(id(ev))

                # Particle effect for Attack Speed Buff
                if ev.event_type == CombatEventType.SPELL_EXECUTED and ev.spell_name == 'Attack Speed Buff' and id(ev) not in self._seen_events:
                    src = getattr(ev, 'source', None)
                    if src is not None and getattr(src, 'position', None):
                        q, r = oddr_to_axial(src.position)
                        px, py = hex_to_pixel(q, r, self.cell_radius)
                        cx = int(px + self.left_offset + self.margin)
                        cy = int(py + self.margin + self.cell_radius + 4)
                        # emit particles around source
                        team = getattr(src, 'team', None)
                        base_color = self.team_colors.get(team, (200, 200, 50))
                        # Bigger burst: more particles, larger sizes, longer life and a soft glow
                        num_particles = 40
                        for i in range(num_particles):
                            ang = (2 * math.pi * i / num_particles) + random.uniform(-0.4, 0.4)
                            speed = random.uniform(self.cell_radius * 0.8, self.cell_radius * 2.2)
                            vx = math.cos(ang) * speed
                            vy = math.sin(ang) * speed
                            lifetime = random.uniform(0.7, 1.6)
                            size = random.randint(4, 7)
                            # slightly vary color brightness
                            color = (min(255, int(base_color[0] * random.uniform(0.9, 1.4))),
                                     min(255, int(base_color[1] * random.uniform(0.9, 1.4))),
                                     min(255, int(base_color[2] * random.uniform(0.9, 1.4))))
                            self.particles.append([cx, cy, vx, vy, now, lifetime, color, size])
                        self._seen_events.add(id(ev))

                if getattr(ev, 'damage', 0) and ev.damage > 0 and id(ev) not in self._seen_events:
                    # Determine position to display damage: prefer explicit position, then target position
                    pos = None
                    if getattr(ev, 'position', None):
                        pos = ev.position
                    elif getattr(ev, 'target', None) and getattr(ev.target, 'position', None):
                        pos = ev.target.position

                    # accumulate damage done by source (for damage meter)
                    src = getattr(ev, 'source', None)
                    if src is not None and getattr(src, 'id', None) is not None:
                            try:
                                self.damage_done[src.id] += float(ev.damage)
                                # remember unit symbol/team for charts even if unit later dies
                                try:
                                    self.unit_info[src.id] = {'symbol': src._get_unit_symbol(), 'team': getattr(src, 'team', None)}
                                except Exception:
                                    # best-effort: ignore if unit lacks helpers
                                    pass
                            except Exception:
                                pass

                    if pos is not None:
                        q, r = oddr_to_axial(pos)
                        px, py = hex_to_pixel(q, r, self.cell_radius)
                        cx = int(px + self.left_offset + self.margin)
                        cy = int(py + self.margin + self.cell_radius + 4)
                        txt = str(int(ev.damage)) + ("*" if getattr(ev, 'crit_bool', False) else "")
                        # start slightly above the unit center
                        # initial position slightly above center
                        x0 = cx
                        y0 = cy - int(self.cell_radius * 0.8)
                        duration = 0.5
                        # random launch angle around upward (-pi/2) with wider spread
                        # avoid angles very close to vertical by rejecting a small central gap
                        spread = math.pi / 3
                        gap = math.pi / 8
                        while True:
                            offset = random.uniform(-spread, spread)
                            if abs(offset) >= gap:
                                break
                        theta = -math.pi / 2 + offset
                        speed = self.cell_radius * random.uniform(1.2, 1.6)
                        vx = math.cos(theta) * speed
                        vy = math.sin(theta) * speed
                        # Choose color based on event type (green for healing, orange for damage)
                        if getattr(ev, 'event_type', None) == CombatEventType.HEALING_DONE:
                            txt_color = (50, 200, 100)
                        else:
                            txt_color = (255, 140, 0)
                        self.floating_texts.append([x0, y0, txt, now, duration, vx, vy, txt_color])
                        self._seen_events.add(id(ev))

            # If any pending action for a unit was cancelled (converted to WAIT and start_position cleared),
            # ensure we don't animate that unit.
            # cancelled_ids = set()
            # for p in engine.get_pending_actions():
            #     if p.action_type == CombatAction.WAIT:
            #         cancelled_ids.add(p.unit.id)
            # for cid in cancelled_ids:
            #     if cid in moving_map:
            #         moving_map.pop(cid, None)

        # Draw each cell (but skip drawing units that are currently moving - they'll be drawn interpolated)
        # Precompute highlighted positions when requested (player initial placement zone)
        highlighted_positions = set()
        if getattr(self, 'highlight_player_initial_zone', False):
            try:
                highlighted_positions = set(self.board.get_initial_positions(2))
            except Exception:
                highlighted_positions = set()

        for x in range(self.board.width):
            for y in range(self.board.height):
                cell = self.board.get_cell((x, y))

                # Convert odd-r offset to axial, then to pixel
                q, r = oddr_to_axial((x, y))
                px, py = hex_to_pixel(q, r, self.cell_radius)
                # apply left offset and margin when computing cell center
                center_x = int(px + self.left_offset + self.margin)
                center_y = int(py + self.margin + self.cell_radius + 4)

                corners = polygon_corners(center_x, center_y, self.cell_radius)
                # If highlighting is enabled and this cell is in the player's initial zone,
                # draw a white border to indicate valid placement while dragging.
                if (x, y) in highlighted_positions:
                    pygame.draw.polygon(self.screen, (255, 255, 255), corners, 2)
                else:
                    pygame.draw.polygon(self.screen, self.grid_color, corners, 2)

                if cell and getattr(cell.unit, 'id', None) is not None and cell.unit.id not in moving_map:
                    unit = cell.unit
                    team = getattr(unit, 'team', 0)
                    color = self.team_colors.get(team, (200, 200, 200))

                    # Unit circle (smaller relative to cell)
                    pygame.draw.circle(self.screen, color, (center_x, center_y), int(self.cell_radius * 0.45))

                    # Unit symbol
                    symbol = unit._get_unit_symbol()
                    # Check for active spin for this unit
                    angle = 0.0
                    for s in list(self.spin_animations):
                        uid, start, duration, speed = s
                        if uid == unit.id:
                            elapsed = now - start
                            if elapsed <= duration:
                                angle = (elapsed * speed) % 360.0
                            else:
                                self.spin_animations.remove(s)
                    text_surf = self.font.render(symbol, True, (255, 255, 255))
                    if angle != 0.0:
                        rot = pygame.transform.rotate(text_surf, angle)
                        rrect = rot.get_rect(center=(center_x, center_y))
                        self.screen.blit(rot, rrect)
                    else:
                        text_rect = text_surf.get_rect(center=(center_x, center_y))
                        self.screen.blit(text_surf, text_rect)

                    # Health bar (above) - slightly smaller than before
                    bar_w = int(self.cell_radius * 1.2)
                    bar_h = max(3, int(self.cell_radius * 0.12))
                    hb_x = center_x - bar_w // 2
                    hb_y = center_y - int(self.cell_radius * 0.75)
                    # Background
                    pygame.draw.rect(self.screen, (50, 50, 50), (hb_x, hb_y, bar_w, bar_h))
                    # Fill
                    hp_ratio = max(0.0, min(1.0, unit.current_health / unit.get_max_health()))
                    pygame.draw.rect(self.screen, (50, 200, 100), (hb_x, hb_y, int(bar_w * hp_ratio), bar_h))

                    # Mana bar (below)
                    mb_y = center_y - int(self.cell_radius * 0.63)
                    pygame.draw.rect(self.screen, (50, 50, 50), (hb_x, mb_y, bar_w, bar_h))
                    mana_ratio = 0.0
                    max_mana = getattr(unit.base_stats, 'max_mana', 0) or 1
                    mana_ratio = max(0.0, min(1.0, unit.current_mana / max_mana))
                    pygame.draw.rect(self.screen, (80, 140, 220), (hb_x, mb_y, int(bar_w * mana_ratio), bar_h))

        # Draw moving units on top
        for _uid, (unit, ix, iy, action) in moving_map.items():
            team = getattr(unit, 'team', 0)
            color = self.team_colors.get(team, (200, 200, 200))
            pygame.draw.circle(self.screen, color, (ix, iy), int(self.cell_radius * 0.45))
            symbol = unit._get_unit_symbol()
            # Check for active spin for this unit (moving units can also spin)
            angle = 0.0
            for s in list(self.spin_animations):
                uid, start, duration, speed = s
                if uid == unit.id:
                    elapsed = now - start
                    if elapsed <= duration:
                        angle = (elapsed * speed) % 360.0
                    else:
                        self.spin_animations.remove(s)
            text_surf = self.font.render(symbol, True, (255, 255, 255))
            if angle != 0.0:
                rot = pygame.transform.rotate(text_surf, angle)
                rrect = rot.get_rect(center=(ix, iy))
                self.screen.blit(rot, rrect)
            else:
                text_rect = text_surf.get_rect(center=(ix, iy))
                self.screen.blit(text_surf, text_rect)

            bar_w = int(self.cell_radius * 1.2)
            bar_h = max(3, int(self.cell_radius * 0.12))
            hb_x = ix - bar_w // 2
            hb_y = iy - int(self.cell_radius * 0.75)
            pygame.draw.rect(self.screen, (50, 50, 50), (hb_x, hb_y, bar_w, bar_h))
            hp_ratio = max(0.0, min(1.0, unit.current_health / unit.get_max_health()))
            pygame.draw.rect(self.screen, (50, 200, 100), (hb_x, hb_y, int(bar_w * hp_ratio), bar_h))
            mb_y = iy - int(self.cell_radius * 0.63)
            pygame.draw.rect(self.screen, (50, 50, 50), (hb_x, mb_y, bar_w, bar_h))
            max_mana = getattr(unit.base_stats, 'max_mana', 0) or 1
            mana_ratio = max(0.0, min(1.0, unit.current_mana / max_mana))
            pygame.draw.rect(self.screen, (80, 140, 220), (hb_x, mb_y, int(bar_w * mana_ratio), bar_h))
            # Damage meter for moving unit
            # (inline damage meter removed; consolidated charts to the right)

        # Draw attack animations on top (lines from attacker toward target)
        if engine is not None:
            for action, sx, sy, tx, ty, t in attack_anims:
                atk = action.unit
                color = self.team_colors.get(getattr(atk, 'team', None), (220, 220, 80))
                # line thickness fades slightly with progress
                width = max(1, int(self.cell_radius * 0.12 * (1.0 - t) + 1))
                pygame.draw.line(self.screen, color, (sx, sy), (tx, ty), width)
                # projectile dot at tip
                pygame.draw.circle(self.screen, (255, 255, 255), (int(tx), int(ty)), max(2, int(self.cell_radius * 0.08)))

            # Draw spell projectiles (Fireball)
            for action, sx, sy, tx, ty, cur_x, cur_y, t in spell_projectiles:
                src = action.unit
                color = self.team_colors.get(getattr(src, 'team', None), (200, 40, 40))
                # projectile larger than basic attack and with team color
                proj_radius = max(6, int(self.cell_radius * 0.18))
                # optional glow behind projectile
                glow_surf = pygame.Surface((proj_radius*4, proj_radius*4), pygame.SRCALPHA)
                glow_alpha = max(40, int(180 * (1.0 - t)))
                pygame.draw.circle(glow_surf, (color[0], color[1], color[2], glow_alpha), (proj_radius*2, proj_radius*2), proj_radius*2)
                self.screen.blit(glow_surf, (int(cur_x - proj_radius*2), int(cur_y - proj_radius*2)))
                # core
                pygame.draw.circle(self.screen, color, (int(cur_x), int(cur_y)), proj_radius)
                # white highlight
                pygame.draw.circle(self.screen, (255, 255, 255), (int(cur_x), int(cur_y)), max(1, proj_radius//3))

        # Draw floating damage texts and AoE animations, remove expired
        now = pygame.time.get_ticks() / 1000.0
        # AoE animations
        aoe_remaining = []
        for aoe in self.aoe_animations:
            pos, radius_hex, start, duration, color = aoe
            elapsed = now - start
            if elapsed <= duration:
                alpha = max(0, min(200, int(200 * (1.0 - elapsed / duration))))
                # draw hex filled polygons for each position in l1 range
                # Use hex-aware API to get cells in range (returns BoardCell objects)
                cells = self.board.get_cells_in_l1_range(pos, radius_hex)
                positions = [c.position for c in cells]
                surf = pygame.Surface(self.window_size, pygame.SRCALPHA)
                for p in positions:
                    q, r = oddr_to_axial(p)
                    px, py = hex_to_pixel(q, r, self.cell_radius)
                    center_x = int(px + self.left_offset + self.margin)
                    center_y = int(py + self.margin + self.cell_radius + 4)
                    corners = polygon_corners(center_x, center_y, self.cell_radius)
                    int_corners = [(int(x), int(y)) for x, y in corners]
                    pygame.draw.polygon(surf, (color[0], color[1], color[2], alpha), int_corners)
                self.screen.blit(surf, (0, 0))
                aoe_remaining.append(aoe)
        self.aoe_animations = aoe_remaining

        # Update and draw particles
        particle_remaining = []
        for p in self.particles:
            x0, y0, vx, vy, start, lifetime, color, size = p
            elapsed = now - start
            if elapsed <= lifetime:
                cur_x = x0 + vx * elapsed
                cur_y = y0 + vy * elapsed
                alpha = max(0, min(255, int(255 * (1.0 - elapsed / lifetime))))
                # soft glow behind particle
                glow_size = int(size * 2.5)
                glow_alpha = max(0, min(220, int(alpha * 0.6)))
                glow_surf = pygame.Surface((glow_size*2, glow_size*2), pygame.SRCALPHA)
                pygame.draw.circle(glow_surf, (color[0], color[1], color[2], glow_alpha), (glow_size, glow_size), glow_size)
                self.screen.blit(glow_surf, (int(cur_x - glow_size), int(cur_y - glow_size)))
                # bright core
                core_surf = pygame.Surface((size*2, size*2), pygame.SRCALPHA)
                core_alpha = alpha
                pygame.draw.circle(core_surf, (255, 255, 255, core_alpha), (size, size), size)
                self.screen.blit(core_surf, (int(cur_x - size), int(cur_y - size)))
                particle_remaining.append(p)
        self.particles = particle_remaining

        # Draw two damage charts (one per team) side-by-side to the right of the board
        if engine is not None and self.damage_done:
            # Use the reserved right panel width for charts so the damage meter is larger
            spacing = 12
            total_w = int(self.right_panel_width)
            each_w = max(180, int((total_w - spacing) / 2))
            chart_x = self.window_size[0] - total_w - self.margin
            chart_y = self.margin
            chart_h = self.window_size[1] - 2 * self.margin

            # background panel covering both charts
            pygame.draw.rect(self.screen, (40, 40, 40), (chart_x, chart_y, total_w, chart_h))
            # main title above the charts
            title_surf = self.title_font.render('Damage Meter', True, (230, 230, 230))
            self.screen.blit(title_surf, (chart_x + 8, chart_y + 6))

            # gather units from board and update unit_info for alive units
            try:
                board_units = engine.board.get_units()
            except Exception:
                board_units = []

            for u in board_units:
                try:
                    self.unit_info[u.id] = {'symbol': u._get_unit_symbol(), 'team': getattr(u, 'team', None)}
                except Exception:
                    pass

            # bucket units by team using persistent unit_info so dead units remain visible
            units_by_team = {1: [], 2: []}
            for uid, dmg in self.damage_done.items():
                info = self.unit_info.get(uid)
                if info is None:
                    # no metadata; skip
                    continue
                team = info.get('team', 0)
                units_by_team.setdefault(team, []).append((info, float(dmg)))

            # render each team's chart in its own column
            pad_left = 8
            pad_top = self.title_font.get_height()
            max_show = 8
            region_h = chart_h - pad_top - 8

            for idx, team in enumerate((1, 2)):
                region_x = chart_x + idx * (each_w + spacing) + pad_left
                region_w = each_w - pad_left - 6
                # reserve space for the section title above bars
                title_y = chart_y + pad_top
                title_h = self.damage_font.get_height()
                region_y = title_y + title_h + 6

                # section title (team colored)
                team_title = f"Team {team}"
                title_col = self.team_colors.get(team, (200, 200, 200))
                t_surf = self.damage_font.render(team_title, True, title_col)
                self.screen.blit(t_surf, (region_x, title_y))

                entries = units_by_team.get(team, [])
                entries = sorted(entries, key=lambda x: x[1], reverse=True)[:max_show]

                if not entries:
                    no_surf = self.damage_font.render('No data', True, (160, 160, 160))
                    self.screen.blit(no_surf, (region_x + 6, region_y + 6))
                    continue

                max_damage = max((d for _, d in entries), default=1.0) or 1.0
                # fixed bar height for consistent chart appearance - slightly taller than damage font
                font_h = self.damage_font.get_height()
                bar_h_fixed = max(font_h + 4, int(self.cell_radius * 0.35), 12)
                gap = 6
                row_h = bar_h_fixed + gap
                for i, (info, dmg) in enumerate(entries):
                    y = region_y + i * row_h
                    bx = region_x
                    by = y + 2
                    bw = region_w
                    bh = bar_h_fixed
                    pygame.draw.rect(self.screen, (30, 30, 30), (bx, by, bw, bh))
                    fill_w = int(bw * (dmg / max_damage)) if max_damage > 0 else 0
                    color = self.team_colors.get(info.get('team', None), (200, 200, 60))
                    pygame.draw.rect(self.screen, color, (bx, by, fill_w, bh))
                    label = f"{info.get('symbol', '?')} {int(dmg)}"
                    lab_surf = self.damage_font.render(label, True, (255, 255, 255))
                    self.screen.blit(lab_surf, (bx + 4, by - 1))

        remaining = []
        for item in self.floating_texts:
            # item: [x0, y0, txt, start, duration, vx, vy, color]
            x0, y0, txt, start, duration, vx, vy, txt_color = item
            elapsed = now - start
            if elapsed <= duration:
                # Parabolic motion: x = x0 + vx*t ; y = y0 + vy*t + 0.5*g*t^2
                g = 300.0
                cur_x = x0 + vx * elapsed
                cur_y = y0 + vy * elapsed + 0.5 * g * (elapsed ** 2)
                alpha = max(0, min(255, int(255 * (1.0 - elapsed / duration))))
                # Render text with white outline + fill at (cur_x, cur_y)
                outline_color = (255, 255, 255)
                fill_color = txt_color
                # thinner outline: fewer offset blits
                outline_surf = self.damage_font.render(txt, True, outline_color).convert_alpha()
                fill_surf = self.damage_font.render(txt, True, fill_color).convert_alpha()
                # apply alpha fade
                outline_surf.set_alpha(alpha)
                fill_surf.set_alpha(alpha)
                off_positions = [(-1, 0), (1, 0), (0, -1), (0, 1)]
                for ox, oy in off_positions:
                    orect = outline_surf.get_rect(center=(int(cur_x + ox), int(cur_y + oy)))
                    self.screen.blit(outline_surf, orect)
                frect = fill_surf.get_rect(center=(int(cur_x), int(cur_y)))
                self.screen.blit(fill_surf, frect)
                remaining.append([x0, y0, txt, start, duration, vx, vy, txt_color])
        self.floating_texts = remaining

        # Hover tooltip: show spell description when hovering a unit
        try:
            mx, my = pygame.mouse.get_pos()
            closest = None
            closest_d = float('inf')
            hovered_unit = None
            # find nearest cell center within reasonable radius
            for x in range(self.board.width):
                for y in range(self.board.height):
                    cell = self.board.get_cell((x, y))
                    q, r = oddr_to_axial((x, y))
                    px, py = hex_to_pixel(q, r, self.cell_radius)
                    center_x = int(px + self.left_offset + self.margin)
                    center_y = int(py + self.margin + self.cell_radius + 4)
                    dx = mx - center_x
                    dy = my - center_y
                    d2 = dx * dx + dy * dy
                    if d2 < closest_d:
                        closest_d = d2
                        closest = (center_x, center_y, cell)

            if closest is not None:
                cx, cy, cell = closest
                # Accept hover if within cell radius
                if closest_d <= (self.cell_radius * 0.9) ** 2 and cell is not None and getattr(cell, 'unit', None) is not None:
                    hovered_unit = cell.unit

            if hovered_unit is not None:
                # Build tooltip contents: unit name (title), spell name, description, and unit health/mana
                spell = getattr(hovered_unit.base_stats, 'spell', None)
                try:
                    title = hovered_unit.unit_type.value
                    title = title[0].upper() + title[1:]  # Capitalize first letter
                except Exception:
                    title = "Unit"

                try:
                    spell_name = spell.name if spell is not None else 'No Spell'
                except Exception:
                    spell_name = 'No Spell'
                # Ensure first letter is capitalized (preserve the rest)
                if spell_name:
                    spell_name = spell_name[0].upper() + spell_name[1:]

                try:
                    desc = spell.description() if spell is not None else "No description available."
                except Exception:
                    desc = "No description available."

                # Numeric health/mana
                cur_hp = int(max(0, hovered_unit.current_health))
                max_hp = int(hovered_unit.get_max_health())
                cur_mana = int(max(0, hovered_unit.current_mana))
                max_mana = int(getattr(hovered_unit.base_stats, 'max_mana', 0) or 0)

                # Wrap description to multiple lines using tooltip font
                max_width = self.tooltip_max_width
                words = desc.split()
                cur = ''
                desc_lines = []
                for w in words:
                    test = (cur + ' ' + w).strip()
                    surf = self.tooltip_font.render(test, True, (255, 255, 255))
                    if surf.get_width() > max_width and cur:
                        desc_lines.append(cur)
                        cur = w
                    else:
                        cur = test
                if cur:
                    desc_lines.append(cur)

                # Render tooltip box slightly offset from mouse so it doesn't sit under cursor
                pad = self.cell_radius // 2
                title_surf = self.tooltip_font.render(title, True, (250, 250, 210))
                desc_surfs = [self.tooltip_font.render(l, True, (230, 230, 230)) for l in desc_lines]
                # numeric text for hp/mana
                nm_font = self.tooltip_font
                hp_text = f"HP: {cur_hp}/{max_hp}"
                mana_text = f"Mana: {cur_mana}/{max_mana}" if max_mana > 0 else "Mana: 0/0"
                hp_surf = nm_font.render(hp_text, True, (230, 230, 230))
                mana_surf = nm_font.render(mana_text, True, (230, 230, 230))
                # spell name surface (render below bars)
                spell_surf = self.tooltip_font.render(spell_name, True, (200, 200, 255))

                # Determine box width and height; include space for two small bars and spell name
                bar_w = min(200, max_width)
                bar_h = max(6, int(self.cell_radius * 0.18))

                # Prepare stats block (three columns) and include its size when computing tooltip dimensions
                bs = hovered_unit.base_stats
                stats = [
                    ("ATK", str(int(hovered_unit.get_attack()))),
                    ("AS", f"{hovered_unit.get_attack_speed():.2f}"),
                    ("SP", f"{getattr(bs, 'spell_power', 0):.0f}"),
                    ("RNG", str(int(getattr(bs, 'range', 1)))),
                    ("DEF", str(int(hovered_unit.get_defense()))),
                    ("RES", str(int(hovered_unit.get_resistance()))),
                    ("CR", f"{getattr(bs, 'crit_rate', 0.0)*100:.0f}%"),
                    ("CD", f"{getattr(bs, 'crit_dmg', 1.0):.1f}x"),

                ]
                stat_font = self.tooltip_font
                # Split into four roughly-equal columns
                n = len(stats)
                per_col = (n + 3) // 4
                left_stats = stats[:per_col]
                midleft_stats = stats[per_col:per_col * 2]
                midright_stats = stats[per_col * 2:per_col * 3]
                right_stats = stats[per_col * 3:]

                left_surfs = [stat_font.render(f"{k}: {v}", True, (220, 220, 220)) for k, v in left_stats]
                midleft_surfs = [stat_font.render(f"{k}: {v}", True, (220, 220, 220)) for k, v in midleft_stats]
                midright_surfs = [stat_font.render(f"{k}: {v}", True, (220, 220, 220)) for k, v in midright_stats]
                right_surfs = [stat_font.render(f"{k}: {v}", True, (220, 220, 220)) for k, v in right_stats]

                left_w = max((s.get_width() for s in left_surfs), default=0)
                midleft_w = max((s.get_width() for s in midleft_surfs), default=0)
                midright_w = max((s.get_width() for s in midright_surfs), default=0)
                right_w = max((s.get_width() for s in right_surfs), default=0)
                gap_cols = 10
                stats_block_w = left_w + (midleft_w and gap_cols + midleft_w or 0) + (midright_w and gap_cols + midright_w or 0) + (right_w and gap_cols + right_w or 0)
                # height of stats block is the max column height
                left_h = sum(s.get_height() + 2 for s in left_surfs)
                midleft_h = sum(s.get_height() + 2 for s in midleft_surfs)
                midright_h = sum(s.get_height() + 2 for s in midright_surfs)
                right_h = sum(s.get_height() + 2 for s in right_surfs)
                stats_block_h = max(left_h, midleft_h, midright_h, right_h)

                content_w = max(title_surf.get_width(), spell_surf.get_width(), hp_surf.get_width() + bar_w + 8, mana_surf.get_width() + bar_w + 8,
                                *(s.get_width() for s in desc_surfs), stats_block_w)
                box_w = content_w + pad * 2
                # compute height: title + gap + (bar_h + text) for hp + gap + (bar_h + text) for mana + gap + stats block + gap + spell name + gap + desc lines
                spacing = 6
                box_h = (pad + title_surf.get_height() + spacing + max(bar_h, hp_surf.get_height()) + spacing +
                         max(bar_h, mana_surf.get_height()) + spacing + stats_block_h + spacing + spell_surf.get_height() + spacing + sum(s.get_height() + 4 for s in desc_surfs) + pad)
                bx = mx + 16
                by = my + 16
                # clamp to window bounds
                if bx + box_w > self.window_size[0] - 4:
                    bx = mx - box_w - 16
                if by + box_h > self.window_size[1] - 4:
                    by = my - box_h - 16

                # background
                tooltip_surf = pygame.Surface((box_w, box_h), pygame.SRCALPHA)
                pygame.draw.rect(tooltip_surf, (20, 20, 20, 220), (0, 0, box_w, box_h), border_radius=6)
                # border
                pygame.draw.rect(tooltip_surf, (140, 140, 140, 200), (0, 0, box_w, box_h), 1, border_radius=6)

                yoff = pad
                # title
                tooltip_surf.blit(title_surf, (pad, yoff))
                yoff += title_surf.get_height() + spacing

                # HP bar + numeric
                bar_x = pad
                bar_y = yoff + max(0, (max(0, bar_h - hp_surf.get_height()) // 2))
                # draw bar background
                pygame.draw.rect(tooltip_surf, (40, 40, 40), (bar_x, bar_y, bar_w, bar_h))
                hp_ratio = max(0.0, min(1.0, hovered_unit.current_health / float(max(1, hovered_unit.get_max_health()))))
                pygame.draw.rect(tooltip_surf, (50, 200, 100), (bar_x, bar_y, int(bar_w * hp_ratio), bar_h))
                # numeric on the right of bar
                tooltip_surf.blit(hp_surf, (bar_x + bar_w + 8, yoff - hp_surf.get_height()//2))
                yoff += max(bar_h, hp_surf.get_height()) + spacing

                # Mana bar + numeric
                bar_x = pad
                bar_y = yoff + max(0, (max(0, bar_h - mana_surf.get_height()) // 3))
                pygame.draw.rect(tooltip_surf, (40, 40, 40), (bar_x, bar_y, bar_w, bar_h))
                mana_ratio = 0.0
                if max_mana > 0:
                    try:
                        mana_ratio = max(0.0, min(1.0, hovered_unit.current_mana / float(max_mana)))
                    except Exception:
                        mana_ratio = 0.0
                pygame.draw.rect(tooltip_surf, (80, 140, 220), (bar_x, bar_y, int(bar_w * mana_ratio), bar_h))
                tooltip_surf.blit(mana_surf, (bar_x + bar_w + 8, yoff - mana_surf.get_height()//3))
                yoff += max(bar_h, mana_surf.get_height()) + spacing


                # Render surfaces for each stat column
                stat_font = self.tooltip_font
                left_surfs = [stat_font.render(f"{k}: {v}", True, (220, 220, 220)) for k, v in left_stats]
                midleft_surfs = [stat_font.render(f"{k}: {v}", True, (220, 220, 220)) for k, v in midleft_stats]
                midright_surfs = [stat_font.render(f"{k}: {v}", True, (220, 220, 220)) for k, v in midright_stats]
                right_surfs = [stat_font.render(f"{k}: {v}", True, (220, 220, 220)) for k, v in right_stats]

                # Compute widths
                left_w = max((s.get_width() for s in left_surfs), default=0)
                midleft_w = max((s.get_width() for s in midleft_surfs), default=0)
                midright_w = max((s.get_width() for s in midright_surfs), default=0)
                right_w = max((s.get_width() for s in right_surfs), default=0)
                gap_cols = 12
                stats_block_w = left_w + (midleft_w and gap_cols + midleft_w or 0) + (midright_w and gap_cols + midright_w or 0) + (right_w and gap_cols + right_w or 0)

                # Ensure tooltip width accommodates stats block
                content_w = max(content_w, stats_block_w + pad * 2)
                box_w = content_w + pad * 2

                # Draw stat lines (three columns)
                col_x = pad
                # left column
                for i, s in enumerate(left_surfs):
                    tooltip_surf.blit(s, (col_x, yoff + i * (s.get_height() + 2)))
                # middle columns
                if midleft_surfs:
                    midleft_x = pad + left_w + gap_cols
                    for i, s in enumerate(midleft_surfs):
                        tooltip_surf.blit(s, (midleft_x, yoff + i * (s.get_height() + 2)))
                if midright_surfs:
                    midright_x = pad + left_w + gap_cols + midleft_w + gap_cols
                    for i, s in enumerate(midright_surfs):
                        tooltip_surf.blit(s, (midright_x, yoff + i * (s.get_height() + 2)))
                # right column
                if right_surfs:
                    right_x = pad + left_w + gap_cols + midleft_w + gap_cols + midright_w + gap_cols
                    for i, s in enumerate(right_surfs):
                        tooltip_surf.blit(s, (right_x, yoff + i * (s.get_height() + 2)))

                # advance yoff by the stats block height
                yoff += stats_block_h + spacing


                # Spell name (below bars)
                try:
                    spell_surf = self.tooltip_font.render(spell_name, True, (200, 200, 255))
                    tooltip_surf.blit(spell_surf, (pad, yoff))
                    yoff += spell_surf.get_height() + spacing
                except Exception:
                    pass

                # description lines
                for s in desc_surfs:
                    tooltip_surf.blit(s, (pad, yoff))
                    yoff += s.get_height() + 4

                self.screen.blit(tooltip_surf, (bx, by))
        except Exception:
            # Never crash rendering on hover
            pass
    def present(self):
        pygame.display.flip()
        self.clock.tick(self.render_fps)

    def close(self):
        pygame.quit()

    def get_pause_button_rect(self):
        """Return a pygame.Rect for the pause/start button placed below the board's bottom-right."""
        # approximate board pixel size (matches estimation in __init__)
        btn_w = max(80, int(self.cell_radius * 2.5))
        # make pause/start button taller for easier interaction
        btn_h = max(44, int(self.cell_radius * 1.5))
        # place at bottom-right of the window with margin
        bx = self.window_size[0] - btn_w - self.margin
        by = self.window_size[1] - btn_h - self.margin
        return pygame.Rect(int(bx), int(by), int(btn_w), int(btn_h))

    def draw_pause_button(self, paused: bool):
        """Draw a Start/Pause button. Call this after drawing the board.

        paused: True -> show 'Start' (since simulation is paused)
        paused: False -> show 'Pause'
        """
        try:
            rect = self.get_pause_button_rect()
            surf = pygame.Surface((rect.w, rect.h), pygame.SRCALPHA)
            # background color depends on state
            bg = (70, 70, 70) if paused else (40, 120, 40)
            pygame.draw.rect(surf, bg, (0, 0, rect.w, rect.h), border_radius=6)
            pygame.draw.rect(surf, (160, 160, 160), (0, 0, rect.w, rect.h), 1, border_radius=6)
            label = 'Start' if paused else 'Pause'
            lab_surf = self.tooltip_font.render(label, True, (255, 255, 255))
            lab_rect = lab_surf.get_rect(center=(rect.w // 2, rect.h // 2))
            surf.blit(lab_surf, lab_rect)
            self.screen.blit(surf, (rect.x, rect.y))
            # Hover tooltip for pause/start button
            try:
                mx, my = pygame.mouse.get_pos()
                if rect.collidepoint((mx, my)):
                    tip = 'Start simulation' if paused else 'Pause simulation'
                    tip_surf = self.tooltip_font.render(tip, True, (230, 230, 230))
                    pw = tip_surf.get_width() + 12
                    ph = tip_surf.get_height() + 8
                    bx = mx + 12
                    by = my + 12
                    if bx + pw > self.window_size[0] - 4:
                        bx = mx - pw - 12
                    if by + ph > self.window_size[1] - 4:
                        by = my - ph - 12
                    surf2 = pygame.Surface((pw, ph), pygame.SRCALPHA)
                    pygame.draw.rect(surf2, (30, 30, 30, 220), (0, 0, pw, ph), border_radius=6)
                    pygame.draw.rect(surf2, (120, 120, 120), (0, 0, pw, ph), 1, border_radius=6)
                    surf2.blit(tip_surf, (6, 4))
                    self.screen.blit(surf2, (bx, by))
            except Exception:
                pass
        except Exception:
            pass

    def get_spawn_button_rect(self, index: int, total: int = 4) -> pygame.Rect:
        """Return a rect for one of the spawn buttons arranged below the board.

        index: 0-based index of the button
        total: total buttons in the row
        """
        btn_w = int(self.cell_radius * 2.5)
        btn_h = int(self.cell_radius * 1.5)
        # start x aligned with pause button area left edge
        base_rect = self.get_pause_button_rect()
        spacing = 8
        total_w = total * btn_w + (total - 1) * spacing
        # Allow overriding the spawn row start x (useful to align with shop/board)
        if getattr(self, 'spawn_start_x', None) is not None:
            start_x = int(self.spawn_start_x)
        else:
            # Center the spawn row horizontally under the board grid area.
            grid_pixel_w = int(self.cell_radius * math.sqrt(3) * (self.board.width + 0.5))
            board_left = int(self.left_offset + self.margin)
            start_x = board_left + (grid_pixel_w - total_w) // 2
            # Clamp to window bounds / margins
            start_x = max(self.margin, start_x)
            start_x = min(start_x, self.window_size[0] - total_w - self.margin)
        bx = start_x + index * (btn_w + spacing)
        by = base_rect.y
        return pygame.Rect(int(bx), int(by), int(btn_w), int(btn_h))

    def draw_spawn_buttons(self, unit_specs: list, budget: int | None = None):
        """Draw spawn buttons for provided `unit_specs` list.

        `unit_specs` items may be either a UnitType or a (UnitType, cost) tuple.
        If `budget` is provided, buttons with cost > budget are drawn disabled.
        """
        total = len(unit_specs)
        mx, my = pygame.mouse.get_pos()
        hover_drawn = False
        for i, spec in enumerate(unit_specs):
            rect = self.get_spawn_button_rect(i, total=total)
            surf = pygame.Surface((rect.w, rect.h), pygame.SRCALPHA)

            # Determine label and cost
            if isinstance(spec, tuple) and len(spec) >= 2:
                ut, cost = spec[0], spec[1]
            else:
                ut, cost = spec, None

            # disabled if budget provided and cost > budget
            disabled = False
            if budget is not None and cost is not None and cost > budget:
                disabled = True

            bg_col = (100, 100, 100) if disabled else (60, 60, 60)
            pygame.draw.rect(surf, bg_col, (0, 0, rect.w, rect.h), border_radius=6)
            pygame.draw.rect(surf, (140, 140, 140), (0, 0, rect.w, rect.h), 1, border_radius=6)
            # label
            try:
                label = ut.value.capitalize() if hasattr(ut, 'value') else str(ut).capitalize()
            except Exception:
                label = str(ut)
            if cost is not None:
                label = f"{label} ({int(cost)})"
            col = (180, 180, 180) if disabled else (255, 255, 255)
            lab_surf = self.tooltip_font.render(label, True, col)
            lab_rect = lab_surf.get_rect(center=(rect.w // 2, rect.h // 2))
            surf.blit(lab_surf, lab_rect.topleft)
            self.screen.blit(surf, (rect.x, rect.y))

            # If mouse is hovering this button, draw a tooltip describing the unit and cost
            if not hover_drawn and rect.collidepoint((mx, my)):
                hover_drawn = True
                # attempt to instantiate a temporary unit to get spell description
                try:
                    tmp_unit = Unit(unit_type=ut, rarity=UnitRarity.COMMON, team=2, level=1)
                    spell = getattr(tmp_unit.base_stats, 'spell', None)
                    desc = spell.description() if spell is not None else 'No spell.'
                except Exception:
                    desc = 'No description available.'

                title = ut.value.capitalize() if hasattr(ut, 'value') else str(ut).capitalize()
                cost_text = f"Cost: {int(cost)}" if cost is not None else ''

                # Wrap description
                max_width = min(self.tooltip_max_width, rect.w * 4)
                words = desc.split()
                cur = ''
                desc_lines = []
                for w in words:
                    test = (cur + ' ' + w).strip()
                    surf_test = self.tooltip_font.render(test, True, (255, 255, 255))
                    if surf_test.get_width() > max_width and cur:
                        desc_lines.append(cur)
                        cur = w
                    else:
                        cur = test
                if cur:
                    desc_lines.append(cur)

                # build tooltip surface
                pad = 6
                title_s = self.tooltip_font.render(title, True, (250, 250, 210))
                cost_s = self.tooltip_font.render(cost_text, True, (200, 200, 200)) if cost_text else None
                desc_surfs = [self.tooltip_font.render(l, True, (230, 230, 230)) for l in desc_lines]
                content_w = max(title_s.get_width(), *(s.get_width() for s in desc_surfs), cost_s.get_width() if cost_s else 0)
                box_w = content_w + pad * 2
                box_h = pad + title_s.get_height() + 6 + (cost_s.get_height() if cost_s else 0) + sum(s.get_height() + 4 for s in desc_surfs) + pad
                bx = mx + 12
                by = my + 12
                if bx + box_w > self.window_size[0] - 4:
                    bx = mx - box_w - 12
                if by + box_h > self.window_size[1] - 4:
                    by = my - box_h - 12

                tsurf = pygame.Surface((box_w, box_h), pygame.SRCALPHA)
                pygame.draw.rect(tsurf, (30, 30, 30, 220), (0, 0, box_w, box_h), border_radius=6)
                pygame.draw.rect(tsurf, (120, 120, 120), (0, 0, box_w, box_h), 1, border_radius=6)
                yoff = pad
                tsurf.blit(title_s, (pad, yoff))
                yoff += title_s.get_height() + 6
                if cost_s:
                    tsurf.blit(cost_s, (pad, yoff))
                    yoff += cost_s.get_height() + 6
                for s in desc_surfs:
                    tsurf.blit(s, (pad, yoff))
                    yoff += s.get_height() + 4

                self.screen.blit(tsurf, (bx, by))


    def get_cell_center(self, position: Tuple[int, int]) -> Tuple[int, int]:
        """Return pixel center (x,y) for a board cell position (x,y)."""
        q, r = oddr_to_axial(position)
        px, py = hex_to_pixel(q, r, self.cell_radius)
        center_x = int(px + self.left_offset + self.margin)
        center_y = int(py + self.margin + self.cell_radius + 4)
        return center_x, center_y

    def get_cell_at_pixel(self, pixel: Tuple[int, int]) -> Tuple[int, int]:
        """Return board cell position (x,y) under given pixel coords, or None if none.

        Uses a simple nearest-center check within cell radius.
        """
        mx, my = pixel
        closest = None
        closest_d = float('inf')
        for x in range(self.board.width):
            for y in range(self.board.height):
                cx, cy = self.get_cell_center((x, y))
                dx = mx - cx
                dy = my - cy
                d2 = dx * dx + dy * dy
                if d2 < closest_d:
                    closest_d = d2
                    closest = (x, y)
        if closest is None:
            return None
        if closest_d <= (self.cell_radius * 0.9) ** 2:
            return closest
        return None
