import math
import pygame
from typing import Tuple
from src.core.combat import CombatAction, CombatEngine


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

    def __init__(self, board, cell_radius: int = 28, margin: int = 16):
        pygame.font.init()
        self.board = board
        self.cell_radius = cell_radius
        self.margin = margin
        self.bg_color = (30, 30, 30)
        self.grid_color = (80, 80, 80)
        self.team_colors = {1: (60, 140, 220), 2: (220, 100, 100)}
        self.font = pygame.font.SysFont('Arial', max(12, int(cell_radius * 0.6)))

        # Estimate window size
        w = int(cell_radius * math.sqrt(3) * (self.board.width + 0.5)) + margin * 2
        h = int(cell_radius * 1.5 * (self.board.height + 1)) + margin * 2
        self.window_size = (w, h)

        pygame.init()
        self.screen = pygame.display.set_mode(self.window_size)
        pygame.display.set_caption('MicroAutoChess - Visualizer')
        self.clock = pygame.time.Clock()

    def draw_board(self, engine: CombatEngine = None, sim_frame: int = 0, sim_progress: float = 0.0):
        """Draw board and optionally animate pending move actions from engine.

        engine: CombatEngine instance or None
        sim_frame: current integer frame in simulation
        sim_progress: fraction [0,1) through the current frame
        """
        self.screen.fill(self.bg_color)

        moving_map = {}
        # if engine is not None:
        #     pending = engine.get_pending_actions()
        #     for action in pending:
        #         if action.action_type == CombatAction.MOVE and action.start_position and action.target_position:
        #             # Only animate if resolution is in the future
        #             if action.resolution_frame > sim_frame:
        #                 start_f = action.planned_frame
        #                 end_f = action.resolution_frame
        #                 denom = max(1, end_f - start_f)
        #                 t = (sim_frame + sim_progress - start_f) / denom
        #                 t = max(0.0, min(1.0, t))

        #                 q1, r1 = oddr_to_axial(action.start_position)
        #                 q2, r2 = oddr_to_axial(action.target_position)
        #                 x1, y1 = hex_to_pixel(q1, r1, self.cell_radius)
        #                 x2, y2 = hex_to_pixel(q2, r2, self.cell_radius)
        #                 # offsets
        #                 cx1 = x1 + self.margin + self.window_size[0] * 0.02
        #                 cy1 = y1 + self.margin + self.cell_radius + 4
        #                 cx2 = x2 + self.margin + self.window_size[0] * 0.02
        #                 cy2 = y2 + self.margin + self.cell_radius + 4

        #                 ix = int(cx1 + (cx2 - cx1) * t)
        #                 iy = int(cy1 + (cy2 - cy1) * t)
        #                 moving_map[action.unit.id] = (action.unit, ix, iy, action)

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
                # apply margin and center offset
                center_x = int(px + self.margin + self.window_size[0] * 0.02)
                center_y = int(py + self.margin + self.cell_radius + 4)

                corners = polygon_corners(center_x, center_y, self.cell_radius)
                pygame.draw.polygon(self.screen, self.grid_color, corners, 2)

                if cell and getattr(cell.unit, 'id', None) is not None and cell.unit.id not in moving_map:
                    unit = cell.unit
                    team = getattr(unit, 'team', 0)
                    color = self.team_colors.get(team, (200, 200, 200))

                    # Unit circle
                    pygame.draw.circle(self.screen, color, (center_x, center_y), int(self.cell_radius * 0.6))

                    # Unit symbol
                    symbol = unit._get_unit_symbol()
                    text_surf = self.font.render(symbol, True, (255, 255, 255))
                    text_rect = text_surf.get_rect(center=(center_x, center_y))
                    self.screen.blit(text_surf, text_rect)

                    # Health bar (above)
                    bar_w = int(self.cell_radius * 1.6)
                    bar_h = max(4, int(self.cell_radius * 0.18))
                    hb_x = center_x - bar_w // 2
                    hb_y = center_y - int(self.cell_radius * 0.9)
                    # Background
                    pygame.draw.rect(self.screen, (50, 50, 50), (hb_x, hb_y, bar_w, bar_h))
                    # Fill
                    hp_ratio = max(0.0, min(1.0, unit.current_health / unit.get_max_health()))
                    pygame.draw.rect(self.screen, (50, 200, 100), (hb_x, hb_y, int(bar_w * hp_ratio), bar_h))

                    # Mana bar (below)
                    mb_y = center_y + int(self.cell_radius * 0.7)
                    pygame.draw.rect(self.screen, (50, 50, 50), (hb_x, mb_y, bar_w, bar_h))
                    mana_ratio = 0.0
                    max_mana = getattr(unit.base_stats, 'max_mana', 0) or 1
                    mana_ratio = max(0.0, min(1.0, unit.current_mana / max_mana))
                    pygame.draw.rect(self.screen, (80, 140, 220), (hb_x, mb_y, int(bar_w * mana_ratio), bar_h))

        # Draw moving units on top
        # for _uid, (unit, ix, iy, action) in moving_map.items():
        #     team = getattr(unit, 'team', 0)
        #     color = self.team_colors.get(team, (200, 200, 200))
        #     pygame.draw.circle(self.screen, color, (ix, iy), int(self.cell_radius * 0.6))
        #     symbol = unit._get_unit_symbol()
        #     text_surf = self.font.render(symbol, True, (255, 255, 255))
        #     text_rect = text_surf.get_rect(center=(ix, iy))
        #     self.screen.blit(text_surf, text_rect)

        #     bar_w = int(self.cell_radius * 1.6)
        #     bar_h = max(4, int(self.cell_radius * 0.18))
        #     hb_x = ix - bar_w // 2
        #     hb_y = iy - int(self.cell_radius * 0.9)
        #     pygame.draw.rect(self.screen, (50, 50, 50), (hb_x, hb_y, bar_w, bar_h))
        #     hp_ratio = max(0.0, min(1.0, unit.current_health / unit.get_max_health()))
        #     pygame.draw.rect(self.screen, (50, 200, 100), (hb_x, hb_y, int(bar_w * hp_ratio), bar_h))
        #     mb_y = iy + int(self.cell_radius * 0.7)
        #     pygame.draw.rect(self.screen, (50, 50, 50), (hb_x, mb_y, bar_w, bar_h))
        #     max_mana = getattr(unit.base_stats, 'max_mana', 0) or 1
        #     mana_ratio = max(0.0, min(1.0, unit.current_mana / max_mana))
        #     pygame.draw.rect(self.screen, (80, 140, 220), (hb_x, mb_y, int(bar_w * mana_ratio), bar_h))

    def present(self):
        pygame.display.flip()
        self.clock.tick(30)

    def close(self):
        pygame.quit()
