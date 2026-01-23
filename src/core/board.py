"""
Game board representation and combat mechanics.
"""

import numpy as np
from typing import List, Optional, Tuple, Dict
from dataclasses import dataclass
from enum import Enum
from queue import PriorityQueue
# from src.numba_classes.numba_pq import NumbaPriorityQueue as PriorityQueue
# from src.numba_classes.numba_pq import PriorityQueue as PriorityQueue
# from numba import njit

# from src.numba_classes.numba_pq import PurePythonPriorityQueue as PriorityQueue
# from src.numba_classes.numba_pq import PriorityQueue as PriorityQueue
from units import Unit, UnitType



class CellType(Enum):
    EMPTY = 0
    PLANNED = 1 # A unit intends to move here but is in transit
    UNIT = 2
    OBSTACLE = 3


@dataclass
class BoardCell:
    """Represents a single cell on the game board."""
    position: Tuple[int, int]
    # DO NOT USE THIS TO CHECK IF CELL IS OCCUPIED
    # WHEN CHECK FOR AOE DAMAGE, CHECK IF A UNIT IS BEING HUT TWICE
    unit: Optional[Unit] = None     
    cell_type: CellType = CellType.EMPTY
    
    def is_empty(self) -> bool:
        return self.unit is None and self.cell_type == CellType.EMPTY
    
    def is_planned(self) -> bool:
        return self.cell_type == CellType.PLANNED
    
    def is_occupied(self) -> bool:
        return self.cell_type == CellType.UNIT
    
    
    def place_unit(self, unit: Unit):
        """Place a unit on this cell."""
        if not (self.is_empty() or self.is_planned()):
            raise ValueError(f"Cell {self.position} is not empty or planned")
        self.unit = unit
        self.cell_type = CellType.UNIT
        unit.position = self.position
    
    def remove_unit(self) -> Unit:
        """Remove unit from this cell."""
        unit = self.unit
        self.unit = None
        self.cell_type = CellType.EMPTY
        if unit:
            unit.position = None
        else:
            raise ValueError(f"Tried to remove unit from cell {self.position}")
        return unit
    
    def set_planned(self):
        """Set cell as planned for unit movement."""
        if not self.is_empty():
            raise ValueError(f"Cell {self.position} is not empty")
        self.cell_type = CellType.PLANNED


class Board:
    """
    Game board for auto chess combat.
    Manages unit positions and combat mechanics.
    """
    
    def __init__(self, size: Tuple[int, int] = (7, 8)):
        self.size = size
        self.width, self.height = size
        self.cells: Dict[Tuple[int, int], BoardCell] = {}
        
        # Initialize board cells
        for x in range(self.width):
            for y in range(self.height):
                self.cells[(x, y)] = BoardCell((x, y))
    
    def get_cell(self, position: Tuple[int, int]) -> BoardCell:
        """Get cell at position."""
        output = self.cells.get(position)
        if output is None:
            raise ValueError(f"Position {position} does not exist on the board")
        return output

    def is_valid_position(self, position: Tuple[int, int]) -> bool:
        """Check if position is within board bounds."""
        x, y = position
        return 0 <= x < self.width and 0 <= y < self.height
    
    def is_valid_initial_position(self, position: Tuple[int, int], team: int) -> bool:
        """Check if position is valid for initial unit placement based on team."""
        x, y = position
        if not self.is_valid_position(position):
            return False
        if team == 1:
            return y < self.height // 2  # Team 1 on top half
        elif team == 2:
            return y >= self.height // 2  # Team 2 on bottom half
        return False

    def place_unit(self, unit: Unit, position: Tuple[int, int]) -> bool:
        """Place unit at position."""
        if not self.is_valid_position(position):
            return False
        
        cell = self.get_cell(position)
        if not cell or not cell.is_empty():
            return False
        
        cell.place_unit(unit)
        return True
    
    def move_unit(self, from_pos: Tuple[int, int], to_pos: Tuple[int, int]) -> bool:
        """Move unit from one position to another."""
        if not (self.is_valid_position(from_pos) and self.is_valid_position(to_pos)):
            return False
        
        from_cell = self.get_cell(from_pos)
        to_cell = self.get_cell(to_pos)
        
        if not from_cell or not to_cell:
            return False
        
        if from_cell.unit is None:
            return False
        
        from_cell.unit.planned_position = None
        if (to_cell.is_planned() and to_cell.unit != from_cell.unit) or to_cell.is_occupied():
            return False
        
        unit = from_cell.remove_unit()
        if unit is None:
            return False
        to_cell.place_unit(unit)
        return True
    
    def get_units(self) -> List[Unit]:
        """Get all units on the board."""
        units = []
        for cell in self.cells.values():
            if cell.unit:
                units.append(cell.unit)
        return units
    
    def get_units_by_team(self, team: int) -> List[Unit]:
        """Get all units belonging to a specific team."""
        return [unit for unit in self.get_units() if getattr(unit, 'team', 0) == team]
    
    @staticmethod
    # @njit
    def l1_distance(pos1: Tuple[int, int], pos2: Tuple[int, int]) -> float:
        """Calculate Manhattan distance between two positions."""
        return abs(pos1[0] - pos2[0]) + abs(pos1[1] - pos2[1])

    @staticmethod
    # @njit
    def l2_distance(pos1: Tuple[int, int], pos2: Tuple[int, int]) -> float:
        """Calculate Euclidean distance between two positions."""
        return np.sqrt((pos1[0] - pos2[0]) ** 2 + (pos1[1] - pos2[1]) ** 2)
    
    def pathfind_distance(self, start: Tuple[int, int], target: Tuple[int, int]) -> float:
        """Calculate pathfinding distance between two positions using A*."""
        path = self.find_path(start, target)
        if not path:
            return float('inf')
        return len(path) - 1  # Number of steps is path length minus 1
    
    def pathfind_distance_to_range(self, start: Tuple[int, int], target: Tuple[int, int], attack_range: int) -> float:
        """Calculate pathfinding distance to get within attack range of target."""
        target_positions = self.get_positions_in_l2_range(target, attack_range)
        target_positions = [pos for pos in target_positions if self.get_cell(pos).is_empty() or pos == start]
        min_distance = float('inf')
        for pos in target_positions:
            distance = self.pathfind_distance(start, pos)
            if distance < min_distance:
                min_distance = distance
        return min_distance
    
    def get_adjacent_positions(self, position: Tuple[int, int]) -> List[Tuple[int, int]]:
        """Get valid adjacent positions."""
        x, y = position
        adjacent = [
            (x+1, y), (x-1, y), (x, y+1), (x, y-1),
            #(x+1, y+1), (x+1, y-1), (x-1, y+1), (x-1, y-1) # Uncomment for diagonal movement
        ]
        return [pos for pos in adjacent if self.is_valid_position(pos)]
    
    def get_adjacent_cells(self, position: Tuple[int, int]) -> List[BoardCell]:
        """Get adjacent cells for a given position."""
        """Returns a list of adjacent cell positions."""
        if not self.is_valid_position(position):
            raise ValueError(f"Position {position} is out of bounds")
        
        adjacent = self.get_adjacent_positions(position)
        return [self.get_cell(pos) for pos in adjacent if self.get_cell(pos) is not None]
    
    def get_cells_in_l1_range(self, position: Tuple[int, int], l1_range: int) -> List[BoardCell]:
        """Get all cells within a certain amount of steps from a position."""
        return [self.get_cell(pos) for pos in self.get_positions_in_l1_range(position, l1_range)]
    
    def get_positions_in_l1_range(self, position: Tuple[int, int], l1_range: int) -> List[Tuple[int, int]]:
        """Get all positions within a certain amount of steps from a position."""
        positions_in_range = []
        for dx in range(-l1_range, l1_range + 1):
            for dy in range(-l1_range + abs(dx), l1_range - abs(dx) + 1):
                new_pos = (position[0] + dx, position[1] + dy)
                if self.is_valid_position(new_pos):
                    positions_in_range.append(new_pos)
        return positions_in_range
    
    def get_positions_in_l2_range(self, position: Tuple[int, int], l2_range: float) -> List[Tuple[int, int]]:
        """Get all positions within a certain Euclidean distance from a position."""
        positions_in_range = []
        x0, y0 = position
        min_x = max(0, int(x0 - l2_range))
        max_x = min(self.width - 1, int(x0 + l2_range))
        min_y = max(0, int(y0 - l2_range))
        max_y = min(self.height - 1, int(y0 + l2_range))
        
        for x in range(min_x, max_x + 1):
            for y in range(min_y, max_y + 1):
                if self.l2_distance(position, (x, y)) <= l2_range:
                    positions_in_range.append((x, y))
        
        return positions_in_range

    def get_initial_positions(self, team: int) -> List[Tuple[int, int]]:
        """Return a list of board positions considered valid initial placement for a team.

        By convention: split the board horizontally. Team 1 uses the upper half (smaller y),
        Team 2 uses the lower half (larger y).
        """
        mid = self.height // 2
        positions = []
        if team == 1:
            for x in range(self.width):
                for y in range(0, mid):
                    if self.is_valid_position((x, y)):
                        positions.append((x, y))
        else:
            for x in range(self.width):
                for y in range(mid, self.height):
                    if self.is_valid_position((x, y)):
                        positions.append((x, y))
        return positions
    
    def find_path(self, start: Tuple[int, int], target: Tuple[int, int]) -> List[Tuple[int, int]]:
        """Simple pathfinding using A* algorithm."""

        if not self.is_valid_position(start) or not self.is_valid_position(target):
            raise ValueError("Start or target position is out of bounds")

        # A* algorithm setup
        open_set = PriorityQueue()
        open_set.put((0, start))
        came_from = {start: None}
        g_score = {start: 0.0}
        f_score = {start: self.l1_distance(start, target)}

        while not open_set.empty():
            current = open_set.get()[1]

            if current == target:
                total_path = [current]
                while current in came_from and came_from[current] is not None:
                    current = came_from[current]
                    total_path.append(current)
                total_path.reverse()
                return total_path

            for neighbor in self.get_adjacent_positions(current):

                if not self.get_cell(neighbor).is_empty() and neighbor != target:
                    continue
                tentative_g_score = g_score[current] + 1  # Assume cost is 1

                if neighbor not in g_score or tentative_g_score < g_score[neighbor]:
                    came_from[neighbor] = current
                    g_score[neighbor] = tentative_g_score
                    f_score[neighbor] = tentative_g_score + self.l1_distance(neighbor, target)
                    if neighbor not in [i for i in open_set.queue]:
                        open_set.put((f_score[neighbor], neighbor))

        return []


    def find_path_guided(self, start: Tuple[int, int], target: Tuple[int, int]) -> List[Tuple[int, int]]:
        """Simple pathfinding using A* algorithm. Prefer horizontal movement when distances are equal and
          prefer moves that get closer to target according to l2 distance."""
        
        if not self.is_valid_position(start) or not self.is_valid_position(target):
            raise ValueError("Start or target position is out of bounds")

        # A* algorithm setup
        open_set = PriorityQueue()
        open_set.put((0, start))
        came_from = {start: None}
        g_score = {start: 0.0}
        f_score = {start: self.l1_distance(start, target)}

        while not open_set.empty():
            current = open_set.get()[1]

            if current == target:
                total_path = [current]
                while current in came_from and came_from[current] is not None:
                    current = came_from[current]
                    total_path.append(current)
                total_path.reverse()
                return total_path

            for neighbor in self.get_adjacent_positions(current):

                if not self.get_cell(neighbor).is_empty() and neighbor != target:
                    continue
                tentative_g_score = g_score[current] + 1  # Assume cost is 1
                #Prefer move that is closer to target according to l2
                dl2 = max(self.l2_distance(current, target) - self.l2_distance(neighbor, target), 0)
                tentative_g_score -= dl2/100

                if abs(neighbor[0] - current[0]) > abs(neighbor[1] - current[1]):
                    tentative_g_score -= 0.02  # Horizontal movement is preferred


                if neighbor not in g_score or tentative_g_score < g_score[neighbor]:
                    came_from[neighbor] = current
                    g_score[neighbor] = tentative_g_score
                    f_score[neighbor] = tentative_g_score + self.l1_distance(neighbor, target)
                    if neighbor not in [i for i in open_set.queue]:
                        open_set.put((f_score[neighbor], neighbor))

        return []

    def find_path_to_range_guided(self, start: Tuple[int, int], target: Tuple[int, int], attack_range: int) -> List[Tuple[int, int]]:
        """Find path to get within attack range of target using A* algorithm with guided movement."""
        target_positions = self.get_positions_in_l2_range(target, attack_range)
        target_positions = [pos for pos in target_positions if self.get_cell(pos).is_empty() or pos == start]
        shortest_path = []
        min_length = float('inf')
        for pos in target_positions:
            path = self.find_path_guided(start, pos)
            if path and len(path) < min_length:
                min_length = len(path)
                shortest_path = path
        return shortest_path
    
    
    def to_array(self) -> np.ndarray:
        """Convert board to numerical array."""
        # Create 3D array: width x height x features
        array = np.zeros((self.width, self.height, 10), dtype=np.float32)
        
        for (x, y), cell in self.cells.items():
            if cell.unit:
                unit_array = cell.unit.to_array()
                array[x, y, :len(unit_array)] = unit_array[:10]  # Limit to 10 features
        
        return array

    def is_empty(self, position: Tuple[int, int]) -> bool:
        """Check if a cell is empty."""
        cell = self.get_cell(position)
        return cell.is_empty() if cell else False

    def remove_unit(self, position: Tuple[int, int]) -> Unit:
        """Remove unit from a specific position."""
        cell = self.get_cell(position)
        if cell and not cell.is_empty():
            return cell.remove_unit()
        raise ValueError(f"Tried to remove unit from empty cell {position}")
    
    def set_planned(self, position: Tuple[int, int], unit: Unit):
        """Set a cell as planned for unit movement."""
        cell = self.get_cell(position)
        if cell and cell.is_empty():
            cell.set_planned()
            cell.unit = unit
        else:
            raise ValueError(f"Cell {position} is not empty or already planned")

    def reset_board(self):
        """Reset the board to empty state."""
        for cell in self.cells.values():
            cell.unit = None
            cell.cell_type = CellType.EMPTY

    def clone(self) -> 'Board':
        """Create a deep copy of the board."""
        new_board = Board(self.size)
        for position, cell in self.cells.items():
            if cell.unit:
                cloned_unit = cell.unit.clone()
                new_board.place_unit(cloned_unit, position)
        return new_board
    
    def print_board(self, title: str = ""):
        """Print current board state."""
        if title:
            print(f"\n=== {title} ===")
        
        
        print("  ", end="")
        for x in range(self.width):
            print(f"{x:2}", end="")
        print()
        
        for y in range(self.height):
            print(f"{y} ", end="")
            for x in range(self.width):
                cell = self.get_cell((x, y))
                if cell and cell.unit:
                    # Display unit with team indicator
                    unit = cell.unit
                    symbol = unit._get_unit_symbol()
                    if hasattr(unit, 'team') and unit.team == 1:
                        symbol = symbol.upper()  # Team 1 uppercase
                    else:
                        symbol = symbol.lower()  # Team 2 lowercase
                    print(f"{symbol:>2}", end="")
                else:
                    print(" .", end="")
            print()
        print()
    

    
class HexBoard(Board):
    """
    Hexagonal board implementation using offset coordinates.
    Uses odd-r offset coordinates where odd rows are shifted right.
    """
    
    def __init__(self, size: Tuple[int, int] = (7, 8)):
        super().__init__(size)
    
    
    def get_adjacent_positions(self, position: Tuple[int, int]) -> List[Tuple[int, int]]:
        """Get valid adjacent positions for hexagonal grid using odd-r offset coordinates."""
        x, y = position
        
        # For odd-r offset coordinates with point-up orientation
        if y % 2 == 1:  # Odd row
            adjacent = [
            (x, y-1),     # Northwest
            (x+1, y-1),   # Northeast
            (x+1, y),     # East
            (x+1, y+1),   # Southeast
            (x, y+1),     # Southwest
            (x-1, y)      # West
            ]
        else:  # Even row
            adjacent = [
            (x-1, y-1),   # Northwest
            (x, y-1),     # Northeast
            (x+1, y),     # East
            (x, y+1),     # Southeast
            (x-1, y+1),   # Southwest
            (x-1, y)      # West
            ]
        
        return [pos for pos in adjacent if self.is_valid_position(pos)]
    
    @staticmethod
    # @njit
    def l1_distance(pos1: Tuple[int, int], pos2: Tuple[int, int]) -> float:
        """Calculate Manhattan distance for hexagonal grid using odd-r offset coordinates."""
        x1, y1 = pos1
        x2, y2 = pos2

        # Convert to axial coordinates for easier distance calculation
        q1 = x1 - (y1 - (y1 & 1)) // 2
        r1 = y1
        q2 = x2 - (y2 - (y2 & 1)) // 2
        r2 = y2

        # Calculate hex distance in axial coordinates
        return (abs(q1 - q2) + abs(q1 + r1 - q2 - r2) + abs(r1 - r2)) // 2

    @staticmethod
    # @njit
    def l2_distance(pos1: Tuple[int, int], pos2: Tuple[int, int]) -> float:
        """Calculate Euclidean distance for hexagonal grid."""
        x1, y1 = pos1
        x2, y2 = pos2
        
        # Convert to axial coordinates
        q1, r1 = oddr_to_axial((x1, y1))
        q2, r2 = oddr_to_axial((x2, y2))

        # Convert to pixel coordinates, with center to center distance of 1 (divide everything by sqrt(3))
        x1 = (q1 + r1 / 2)
        y1 = r1 * 3**0.5/2
        x2 = (q2 + r2 / 2) 
        y2 = r2 * 3**0.5/2

        # Calculate Euclidean distance 
        return np.sqrt((x1 - x2) ** 2 + (y1 - y2) ** 2)
    
    def get_cells_in_l1_range(self, position: Tuple[int, int], l1_range: int) -> List[BoardCell]:
        """Get all cells within a certain amount of steps from a position in hexagonal grid."""
        results = []
        (q0, r0) = oddr_to_axial(position)
        for q in range(-l1_range, l1_range + 1):
            for r in range(max(-l1_range, -q - l1_range), min(l1_range, -q + l1_range) + 1):
                x,y = axial_to_oddr((q0 + q, r0 + r))
                if self.is_valid_position((x, y)):
                    cell = self.get_cell((x, y))
                    if cell:
                        results.append(cell)
        return results
    
    def get_positions_in_l1_range(self, position: Tuple[int, int], l1_range: int) -> List[Tuple[int, int]]:
        """Get all positions within a certain amount of steps from a position in hexagonal grid."""
        results = []
        (q0, r0) = oddr_to_axial(position)
        for q in range(-l1_range, l1_range + 1):
            for r in range(max(-l1_range, -q - l1_range), min(l1_range, -q + l1_range) + 1):
                x,y = axial_to_oddr((q0 + q, r0 + r))
                if self.is_valid_position((x, y)):
                    results.append((x, y))
        return results
    
    def get_positions_in_l2_range(self, position, l2_range):
        """Get all positions within a certain Euclidean distance from a position in hexagonal grid."""
        results = []
        (q0, r0) = oddr_to_axial(position)
        max_range = int(l2_range * 2)  # Approximate max range in hex steps, this is a safe overestimate
        for q in range(-max_range, max_range + 1):
            for r in range(max(-max_range, -q - max_range), min(max_range, -q + max_range) + 1):
                x,y = axial_to_oddr((q0 + q, r0 + r))
                if self.is_valid_position((x, y)):
                    if self.l2_distance(position, (x, y)) <= l2_range:
                        results.append((x, y))
        return results

    def create_hex_cell(self, content=""):
        """
        Returns a list of 5 strings representing the ASCII art for a single hex cell.
        The cell is 6 characters wide and 5 lines high.
        The middle (third) line shows the content, centered in a field of width 4.
        """
        # Ensure the content fits in 6 characters.
        content_str = str(content)[:6]
        # Each cell's 5 lines:
        cell = [
            "  _ /  \\ _  ",  # line 0: top point
            " /        \\ ",  # line 1: upper sides
            f"|{content_str:^10}|",  # line 2: content line
            f"|{' ':^10}|",  # line 3: empty line for symmetry
            " \\ _    _ / ",  # line 4: lower sides
            "    \\  /    "   # line 5: bottom point
        ]
        return cell

    def print_board(self,title: str = ""):
        """
        Generates an ASCII drawing of an odd-r hex grid with point-up hexes.
        """
        if title:
            print(f"\n=== {title} ===")
        # Each hex cell's dimensions
        cell_width = 12   # each cell drawn is 8 characters wide
        cell_height = 6  # each cell drawn is 5 lines tall
        
        # In a point-up hex grid the vertical stacking overlaps.
        # We use a vertical offset of 3 lines per row.
        vert_offset = 4
        canvas_height = (self.height - 1) * vert_offset + cell_height
        # For horizontal extent, odd rows get shifted by half cell width (3 spaces).
        canvas_width = cell_width * self.width + 10  # extra for shifted rows

        # Create a blank canvas (list of lists of characters)
        canvas = [[" " for _ in range(canvas_width)] for _ in range(canvas_height)]
        


        # For each hex cell, compute its top-left position and overlay its ASCII art onto the canvas.
        for r in range(self.height):
            for c in range(self.width):
                # For odd rows (1-indexed odd, i.e. r % 2 == 1 in 0-indexing),
                # we indent by half the cell width (3 spaces)
                x_offset = (cell_width//2 if (r % 2 == 1) else 0) + c * cell_width
                y_offset = r * vert_offset

                cell = self.get_cell((c, r))
                if not cell or cell.is_empty() or cell.is_planned():
                    cell_content = f" "
                else:
                    cell_content = cell.unit._get_unit_symbol().upper() if cell.unit.team == 1 else cell.unit._get_unit_symbol().lower()
                cell_art = self.create_hex_cell(cell_content)

                # Overlay the cell art onto the canvas:
                for i in range(cell_height):
                    # Compute the canvas row index
                    canvas_y = y_offset + i
                    # Skip if outside the canvas (should not happen)
                    if canvas_y >= canvas_height:
                        continue
                    line = cell_art[i]
                    for j, char in enumerate(line):
                        canvas_x = x_offset + j
                        if canvas_x < canvas_width:
                            # Only non-space characters overwrite what's there.
                            if char != " ":
                                canvas[canvas_y][canvas_x] = char
        
        # Convert canvas to string lines
        ascii_art = "\n".join("".join(row) for row in canvas)
        print(ascii_art)

# @njit
def oddr_to_axial(position: Tuple[int, int]) -> Tuple[int, int]:
    """Convert odd-r offset coordinates to axial coordinates."""
    x, y = position
    q = x - (y - (y & 1)) // 2
    r = y
    return (q, r)
# @njit
def axial_to_oddr(position: Tuple[int, int]) -> Tuple[int, int]:
    """Convert axial coordinates to odd-r offset coordinates."""
    q, r = position
    x = q + (r - (r & 1)) // 2
    y = r
    return (x, y)

#TODO: Triangular board?

            
        