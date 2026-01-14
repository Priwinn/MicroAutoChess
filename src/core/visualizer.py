import math
import random
import pygame
from typing import Tuple
from collections import defaultdict
from combat import CombatEngine
from constant_types import CombatAction, CombatEventType


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
        pygame.font.init()
        self.board = board
        self.cell_radius = cell_radius
        self.margin = margin
        self.bg_color = (30, 30, 30)
        self.grid_color = (80, 80, 80)
        self.team_colors = {1: (60, 140, 220), 2: (220, 100, 100)}
        self.font = pygame.font.SysFont('Arial', max(12, int(cell_radius * 0.6)))
        self.render_fps = render_fps

        # Estimate window size
        # reserve extra space to the right for charts / debug panels
        self.right_panel_width = max(320, int(self.cell_radius * 6))
        w = int(cell_radius * math.sqrt(3) * (self.board.width + 0.5)) + margin * 2 + self.right_panel_width
        h = int(cell_radius * 1.5 * (self.board.height + 1)) + margin * 2
        self.window_size = (w, h)
        # Add extra left offset so the grid doesn't sit flush to the window edge
        self.left_offset = int(self.window_size[0] * 0.06)

        pygame.init()
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
        self.damage_font = pygame.font.SysFont('Arial', max(10, int(cell_radius * 0.5)))
        # store lightweight unit info (symbol, team) so dead units can still be shown in charts
        self.unit_info = {}

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
                if ev.event_type == CombatEventType.SPELL_EXECUTED and ev.spell_name == 'Fireball'and id(ev) not in self._seen_events:
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
            # per-chart width and spacing
            each_w = max(140, int(self.window_size[0] * 0.12))
            spacing = 10
            total_w = each_w * 2 + spacing
            chart_x = self.window_size[0] - total_w - self.margin
            chart_y = self.margin
            chart_h = self.window_size[1] - 2 * self.margin

            # background panel covering both charts
            pygame.draw.rect(self.screen, (40, 40, 40), (chart_x, chart_y, total_w, chart_h))
            # main title above the charts
            title_surf = self.font.render('Damage Meter', True, (230, 230, 230))
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
            pad_top = 28
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

    def present(self):
        pygame.display.flip()
        self.clock.tick(self.render_fps)

    def close(self):
        pygame.quit()
